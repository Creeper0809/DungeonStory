using System;
using System.Collections.Generic;
using System.Linq;

public static class CropHarvestCompletionDeliveryRestoreJoin
{
    public static void Validate(
        IEnumerable<CropPlotSaveData> plots,
        IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> deliveries,
        GameplayOutcomeLedgerSaveData ledger)
    {
        if (ledger?.outbox == null || ledger.exactOutcomes == null || ledger.tombstones == null)
            throw new InvalidOperationException("Crop completion join requires its saved outcome ledger.");
        var exact = ledger.outbox.Concat(ledger.exactOutcomes).Where(value => value != null)
            .ToLookup(value => (value.producerId, value.operationId, value.commitRevision, value.localResultIndex));
        var retired = ledger.tombstones.Where(value => value != null)
            .ToLookup(value => (value.producerId, value.operationId, value.commitRevision, value.localResultIndex));
        WorkCompletionIdentityDeliveryCursorSaveData[] cursors =
            WorkCompletionIdentityDeliveryLedger.ValidateAndClone(deliveries);
        Dictionary<string, WorkCompletionIdentityDeliveryCursorSaveData>
            byStream = cursors.ToDictionary(
                value => value.producerStreamId,
                StringComparer.Ordinal);
        HashSet<string> cropStreams = new(StringComparer.Ordinal);
        foreach (CropPlotSaveData plot in plots ?? Array.Empty<CropPlotSaveData>())
        {
            if (plot == null
                || string.IsNullOrWhiteSpace(plot.buildingInstanceId)
                || !string.Equals(
                    plot.buildingInstanceId,
                    plot.buildingInstanceId.Trim(),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop completion delivery join contains an invalid plot.");
            string streamId = CropHarvestCompletionObservation.StreamPrefix
                + plot.buildingInstanceId;
            CropPlotRetirementObserver.ValidateSavedPublicationKey(
                new BuildingInstanceId(plot.buildingInstanceId), plot.retirementPublicationKey);
            if (!cropStreams.Add(streamId))
                throw new InvalidOperationException(
                    "Crop completion delivery join contains duplicate plot streams.");
            byStream.TryGetValue(streamId, out WorkCompletionIdentityDeliveryCursorSaveData cursor);
            if (cursor != null)
                ValidateCursorOutcome(plot, cursor);
            CropHarvestOutputSaveData pending = plot.pendingHarvest;
            if (pending == null || pending.phase == CropHarvestOutputPhase.None)
            {
                if (cursor != null
                    && (plot.nextHarvestOperationSequence <= 0
                        || cursor.operationSequence >=
                            plot.nextHarvestOperationSequence
                        || !string.Equals(
                            cursor.deliveryId,
                            CropHarvestCompletionObservation.DeliveryPrefix
                                + CropPlotRuntime.FormatHarvestOperationId(
                                    new BuildingInstanceId(
                                        plot.buildingInstanceId),
                                    cursor.operationSequence),
                            StringComparison.Ordinal)))
                    throw new InvalidOperationException(
                        "Crop completion delivery cursor is ahead of its plot sequence.");
                CropCycleExecutionReceiptSaveData completed = plot.cycleExecutionReceipt;
                if (completed?.completed == true)
                {
                    if (!string.IsNullOrEmpty(completed.harvesterId)
                        && (cursor == null || cursor.operationSequence != completed.harvestOperationSequence))
                        throw new InvalidOperationException("Completed crop cycle has no matching terminal delivery cursor.");
                    GameplayResultKey childKey = new(EnvironmentOutcomeIds.CropPlanProducer,
                        new GameplayOperationId(completed.correlationId),
                        checked((long)completed.harvestOperationSequence + 1L), 0);
                    GameplayOutcomeSnapshot child = RequireCommitted(childKey, null);
                    ValidateCropChild(child, plot.buildingInstanceId, completed.harvesterId,
                        completed.harvestOperationId);
                }
                continue;
            }
            if (string.IsNullOrEmpty(pending.harvesterId))
            {
                if (!string.IsNullOrEmpty(pending.completionPublicationKey)
                    || !string.IsNullOrEmpty(pending.completionDeliveryId)
                    || !string.IsNullOrEmpty(
                        pending.completionDeliveryFingerprint))
                    throw new InvalidOperationException(
                        "Workerless crop owner has completion delivery provenance.");
                if (cursor != null
                    && (cursor.operationSequence >= pending.operationSequence
                        || !MatchesHistoricalDeliveryId(
                            plot.buildingInstanceId,
                            cursor)))
                    throw new InvalidOperationException(
                        "Workerless crop owner has a current or invalid completion cursor.");
                continue;
            }

            WorkCompletionIdentityDeliveryRequest request =
                CropHarvestCompletionObservation.CreateSavedRequest(
                    new BuildingInstanceId(plot.buildingInstanceId),
                    pending, plot.cycleExecutionReceipt);
            bool exactCurrent = cursor != null
                && cursor.operationSequence == request.OperationSequence
                && string.Equals(
                    cursor.deliveryId,
                    request.DeliveryId,
                    StringComparison.Ordinal)
                && string.Equals(
                    cursor.payloadFingerprint,
                    request.PayloadFingerprint,
                    StringComparison.Ordinal);
            bool previous = cursor == null && request.OperationSequence == 0
                || cursor != null
                && cursor.operationSequence == request.OperationSequence - 1
                && MatchesHistoricalDeliveryId(
                    plot.buildingInstanceId,
                    cursor);
            if (pending.completionEventPublished ? !exactCurrent
                : !exactCurrent && !previous)
                throw new InvalidOperationException(
                    "Crop completion owner and identity delivery cursor do not join.");
            GameplayResultKey pendingChild = WorkCompletionChildReference.Require(
                request.OperationId, request.TerminalOwnerId, request.ChildResultKey);
            if ((pending.completionEventPublished || exactCurrent) && !pending.cropOutcomeAcknowledged)
                throw new InvalidOperationException("Crop identity ACK precedes its terminal child ACK.");
            if (pending.cropOutcomeAcknowledged || exactCurrent || IsKnown(pendingChild))
            {
                GameplayOutcomeSnapshot child = RequireCommitted(pendingChild, request.AbsoluteDay);
                ValidateCropChild(child, plot.buildingInstanceId, pending.harvesterId, pending.operationId);
                if (child != null && !MatchesContribution(child, request.Character.Value, request.ContributionWorkUnits))
                    throw new InvalidOperationException("Crop completion contribution differs from its child.");
            }
            GameplayResultKey identityKey = WorkCompletionIdentityOutcomeIds.ResultKey(request);
            if (!exactCurrent && IsKnown(identityKey))
                throw new InvalidOperationException("Crop applied identity outcome has no matching delivery cursor.");
        }

        WorkCompletionIdentityDeliveryCursorSaveData orphan = cursors
            .FirstOrDefault(value =>
                value.producerStreamId.StartsWith(
                    CropHarvestCompletionObservation.StreamPrefix,
                    StringComparison.Ordinal)
                && !cropStreams.Contains(value.producerStreamId));
        if (orphan != null)
            throw new InvalidOperationException(
                "Crop completion delivery cursor has no owning crop plot: "
                + orphan.producerStreamId);

        bool IsKnown(GameplayResultKey key)
        {
            var tuple = (key.ProducerId, key.OperationId.Value, key.CommitRevision, key.LocalResultIndex);
            return exact[tuple].Any() || retired[tuple].Any();
        }

        GameplayOutcomeSnapshot RequireCommitted(GameplayResultKey key, int? day)
        {
            var tuple = (key.ProducerId, key.OperationId.Value, key.CommitRevision, key.LocalResultIndex);
            GameplayOutcomeSnapshot[] rows = exact[tuple].ToArray();
            GameplayOutcomeTombstoneSnapshot[] tombstones = retired[tuple].ToArray();
            if (rows.Length + tombstones.Length != 1
                || day.HasValue && (rows.Length == 1 && rows[0].absoluteDay != day.Value
                    || tombstones.Length == 1 && tombstones[0].absoluteDay != day.Value))
                throw new InvalidOperationException("Crop completion has no unique committed outcome with its frozen date: " + key);
            // Compaction retains identity/date, not the old worker/fact payload.
            return rows.Length == 1 ? rows[0] : null;
        }

        void ValidateCursorOutcome(CropPlotSaveData plot, WorkCompletionIdentityDeliveryCursorSaveData cursor)
        {
            GameplayResultKey identityKey = new(WorkCompletionIdentityOutcomeIds.ProducerId,
                new GameplayOperationId(cursor.deliveryId), checked((long)cursor.operationSequence + 1L), 0);
            if (cursor.disposition == WorkCompletionIdentityDeliveryDisposition.TerminalRecipientUnavailable)
            {
                if (IsKnown(identityKey))
                    throw new InvalidOperationException("Unavailable crop recipient also has an applied identity outcome.");
                return;
            }
            bool awaitingObservation = cursor.disposition == WorkCompletionIdentityDeliveryDisposition.EffectsAppliedPendingObservation;
            if (awaitingObservation)
            {
                var pending = plot.pendingHarvest;
                if (pending == null || pending.phase == CropHarvestOutputPhase.None || pending.completionEventPublished
                    || string.IsNullOrEmpty(pending.harvesterId))
                    throw new InvalidOperationException("Pending crop identity has no awaiting completion owner.");
                var frozen = CropHarvestCompletionObservation.CreateSavedRequest(
                    new BuildingInstanceId(plot.buildingInstanceId), pending, plot.cycleExecutionReceipt);
                if (frozen.PayloadFingerprint != cursor.payloadFingerprint || frozen.DeliveryId != cursor.deliveryId)
                    throw new InvalidOperationException("Pending crop identity differs from its completion owner.");
                if (!IsKnown(identityKey)) return;
            }
            if (!awaitingObservation && cursor.disposition != WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome)
                throw new InvalidOperationException("Crop completion has an unsupported delivery disposition.");
            GameplayOutcomeSnapshot identity = RequireCommitted(identityKey, null);
            if (identity == null) return; // Existing compacted replay contract; do not invent forgotten facts.
            var workers = identity.participants.Where(value =>
                value.roleId == WorkCompletionIdentityOutcomeIds.WorkerRole.Value).ToArray();
            var contributions = identity.metrics.Where(value =>
                value.metricId == WorkCompletionIdentityOutcomeIds.ContributionMetric.Value).ToArray();
            if (identity.outcomeTypeId != WorkCompletionIdentityOutcomeIds.Applied.Value
                || identity.status != GameplayOutcomeStatus.Succeeded
                || workers.Length != 1 || contributions.Length != 1
                || Fact(identity, WorkCompletionIdentityOutcomeIds.WorkIdFact.Value) != BuiltInWorkTypeIds.Harvest.Value
                || Fact(identity, WorkCompletionIdentityOutcomeIds.OriginFact.Value) != "autonomous"
                || Fact(identity, WorkCompletionIdentityOutcomeIds.ProducerStreamFact.Value) != cursor.producerStreamId
                || !identity.subjects.Any(value => value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value
                    && value.entityId == plot.buildingInstanceId))
                throw new InvalidOperationException("Crop completion cursor differs from its identity outcome.");
            WorkCompletionIdentityDeliveryRequest request = new(cursor.deliveryId, cursor.producerStreamId,
                cursor.operationSequence, new CharacterId(workers[0].entityId), BuiltInWorkTypeIds.Harvest.Value,
                Fact(identity, WorkCompletionIdentityOutcomeIds.ProductIdFact.Value), CharacterCommandOrigin.Autonomous,
                identity.absoluteDay, Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact.Value),
                Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact.Value),
                Fact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value), plot.buildingInstanceId,
                (float)contributions[0].value, Fact(identity, WorkCompletionIdentityOutcomeIds.SkillModifierProvenanceFact.Value));
            if (request.PayloadFingerprint != cursor.payloadFingerprint
                || !identity.provenance.Any(value => value.kindId == "identity-payload-fingerprint"
                    && value.value == request.PayloadFingerprint))
                throw new InvalidOperationException("Crop completion cursor fingerprint differs from its applied identity.");
            GameplayResultKey childKey = WorkCompletionChildReference.Require(
                request.OperationId, request.TerminalOwnerId, request.ChildResultKey);
            if (childKey.ProducerId != EnvironmentOutcomeIds.CropPlanProducer
                || request.TerminalOwnerId != childKey.ProducerId
                || childKey.CommitRevision != checked((long)cursor.operationSequence + 1L)
                || childKey.LocalResultIndex != 0)
                throw new InvalidOperationException("Crop completion refers to a different terminal owner/revision.");
            GameplayOutcomeSnapshot child = RequireCommitted(childKey, identity.absoluteDay);
            ValidateCropChild(child, plot.buildingInstanceId, request.Character.Value,
                CropPlotRuntime.FormatHarvestOperationId(new BuildingInstanceId(plot.buildingInstanceId), cursor.operationSequence));
            if (child != null && (child.sequence >= identity.sequence
                || !MatchesContribution(child, request.Character.Value, request.ContributionWorkUnits)))
                throw new InvalidOperationException("Crop child must precede identity and preserve its approved contribution.");
        }
    }

    private static string Fact(GameplayOutcomeSnapshot row, string id) =>
        row.facts.SingleOrDefault(value => value.factId == id)?.value ?? string.Empty;

    private static bool MatchesContribution(GameplayOutcomeSnapshot child, string worker, float contribution)
    {
        string prefix = worker + "=";
        string[] entries = Fact(child, EnvironmentOutcomeIds.HarvestContributorsFact.Value)
            .Split(',').Where(value => value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        return contribution == 0f ? entries.Length == 0
            : entries.Length == 1 && entries[0] == prefix + contribution.ToString(
                "R", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ValidateCropChild(GameplayOutcomeSnapshot child, string plot, string worker, string operation)
    {
        if (child != null && (child.outcomeTypeId != EnvironmentOutcomeIds.CropPlanTerminal.Value
            || child.status != GameplayOutcomeStatus.Succeeded
            || !child.participants.Any(value => value.entityKindId == EnvironmentOutcomeIds.FacilityKind.Value
                && value.entityId == plot)
            || Fact(child, EnvironmentOutcomeIds.HarvesterFact.Value) != worker
            || Fact(child, EnvironmentOutcomeIds.HarvestOperationFact.Value) != operation))
            throw new InvalidOperationException("Crop completion owner differs from its terminal child.");
    }

    private static bool MatchesHistoricalDeliveryId(
        string plotId,
        WorkCompletionIdentityDeliveryCursorSaveData cursor) =>
        cursor != null
        && string.Equals(
            cursor.deliveryId,
            CropHarvestCompletionObservation.DeliveryPrefix
                + CropPlotRuntime.FormatHarvestOperationId(
                    new BuildingInstanceId(plotId),
                    cursor.operationSequence),
            StringComparison.Ordinal);
}

