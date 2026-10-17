using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

public static class RepairWorkCompletionObservation
{
    public const string TerminalOwnerId = RepairWorkCompletionFact.TerminalOwnerId;
    public static GameplayResultKey ResultKey(string operationId) =>
        MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.RepairWorkResult, operationId, 1L);

    public static WorkCompletionIdentityDeliveryRequest CreateCompletionRequest(
        RepairWorkOutcomeSaveData owner) => CreateCompletionRequest(new RepairWorkCompletionFact(owner));

    public static WorkCompletionIdentityDeliveryRequest CreateCompletionRequest(
        RepairWorkCompletionFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        RepairWorkOutcomeSaveData owner = fact.Snapshot();
        if (fact.ParentPublicationKey != ResultKey(owner.operationId).ToString())
            throw new InvalidOperationException("Repair completion requires its exact frozen child.");
        return CreateCompletionRequest(owner.operationId, owner.completionWorkerId,
            owner.completionOrigin, owner.completedAbsoluteDay, owner.targetEntityId,
            owner.completionContributionWork);
    }

    private static WorkCompletionIdentityDeliveryRequest CreateCompletionRequest(
        string operationId, string workerId, CharacterCommandOrigin origin, int day,
        string targetId, float work)
    {
        GameplayResultKey child = ResultKey(operationId);
        WorkCompletionIdentityDeliveryRequest referenced =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(new WorkCompletedIdentityEvent(
                new CharacterId(workerId), BuiltInWorkTypeIds.Repair.Value,
                string.Empty, origin, day,
                child.OperationId.Value, TerminalOwnerId, child.ToString(),
                targetId, work));
        // Keep the business operation, while the child/worker owns delivery identity.
        return new WorkCompletionIdentityDeliveryRequest(referenced.DeliveryId,
            referenced.ProducerStreamId, referenced.OperationSequence, referenced.Character,
            referenced.WorkId, referenced.ProductId, referenced.Origin, referenced.AbsoluteDay,
            operationId, TerminalOwnerId, child.ToString(), targetId, work);
    }

    public static GameplayResultKey RequireCompletionChild(
        WorkCompletionIdentityDeliveryRequest request)
    {
        if (request.WorkId != BuiltInWorkTypeIds.Repair.Value
            || request.TerminalOwnerId != TerminalOwnerId
            || !RepairWorkOutcomeValidation.TryParseSequence(request.OperationId, out _)
            || request.ChildResultKey != ResultKey(request.OperationId).ToString())
            throw new ArgumentException("Repair completion has no exact business-operation child.");
        GameplayResultKey child = ResultKey(request.OperationId);
        WorkCompletionIdentityDeliveryRequest canonical =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(new WorkCompletedIdentityEvent(
                request.Character, request.WorkId, request.ProductId, request.Origin,
                request.AbsoluteDay, child.OperationId.Value, TerminalOwnerId,
                child.ToString(), request.TargetEntityId, request.ContributionWorkUnits,
                request.SkillModifierProvenance));
        if (request.DeliveryId != canonical.DeliveryId
            || request.ProducerStreamId != canonical.ProducerStreamId
            || request.OperationSequence != 0 || request.ProductId != string.Empty
            || request.SkillModifierProvenance != WorkCompletionIdentityReferenceDefaults.None)
            throw new ArgumentException("Repair completion has a noncanonical child/worker delivery.");
        return child;
    }

    public static bool MatchesCompletionChild(WorkCompletionIdentityDeliveryRequest request,
        GameplayOutcomeSnapshot child) => child != null
        && child.outcomeTypeId == MigratedProducerOutcomeCatalog.Get(
            MigratedProducerOutcomeKind.RepairWorkResult).OutcomeTypeId.Value
        && child.status == GameplayOutcomeStatus.Succeeded
        && child.absoluteDay == request.AbsoluteDay
        && FactValue(child, MigratedProducerOutcomeIds.OperationFact.Value) == request.OperationId
        && FactValue(child, "repair-target-id") == request.TargetEntityId
        && FactValue(child, "repair-completion-worker") == request.Character.Value
        && FactValue(child, "repair-completion-origin") == WorkCompletionIdentityOutcomeIds.StableOrigin(request.Origin)
        && FactValue(child, "repair-completion-work") == request.ContributionWorkUnits.ToString("R", CultureInfo.InvariantCulture);

    private static string FactValue(GameplayOutcomeSnapshot row, string id) =>
        row.facts.SingleOrDefault(value => value.factId == id)?.value;

    public static void ValidateCompletionRestoreJoin(
        IEnumerable<RepairWorkOutcomeSaveData> owners,
        IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> deliveries,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (owners == null || ledger?.outbox == null || ledger.exactOutcomes == null
            || ledger.tombstones == null)
            throw new InvalidOperationException("Repair completion join requires saved owners and ledger.");
        var pending = owners.ToDictionary(value => value.operationId, StringComparer.Ordinal);
        var cursors = WorkCompletionIdentityDeliveryLedger.ValidateAndClone(deliveries)
            .ToDictionary(value => value.producerStreamId, StringComparer.Ordinal);
        GameplayOutcomeSnapshot[] rows = ledger.outbox.Concat(ledger.exactOutcomes).ToArray();
        var exact = rows.ToLookup(value => Key(value));
        var retired = ledger.tombstones.ToLookup(value =>
            new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId),
                value.commitRevision, value.localResultIndex).ToString());
        foreach (RepairWorkOutcomeSaveData owner in pending.Values)
        {
            if (!RepairWorkOutcomeValidation.IsValid(owner, false, out string failure))
                throw new InvalidOperationException(failure);
            string key = ResultKey(owner.operationId).ToString();
            bool childKnown = exact[key].Any() || retired[key].Any();
            if (string.IsNullOrEmpty(owner.publicationKey))
            {
                if (childKnown)
                    throw new InvalidOperationException("Repair owner lost its reserved publication: " + key);
                continue;
            }
            if (!owner.terminal || owner.publicationKey != key || owner.publicationConfirmed && !childKnown)
                throw new InvalidOperationException("Repair owner publication phase/key conflict: " + key);
            ValidatePublication(owner, ledger, counters);
            // A child may have committed before acknowledgement reached its owner.
            // Validate that exact frozen payload; never create a replacement key.
            if (childKnown)
            {
                var request = CreateCompletionRequest(owner.operationId, owner.completionWorkerId,
                    owner.completionOrigin, owner.completedAbsoluteDay, owner.targetEntityId,
                    owner.completionContributionWork);
                string completionKey = WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString();
                if (!string.IsNullOrEmpty(owner.completionPublicationKey)
                    && owner.completionPublicationKey != completionKey)
                    throw new InvalidOperationException("Repair completion publication key conflict: " + key);
                if (string.IsNullOrEmpty(owner.completionPublicationKey)
                    && (cursors.ContainsKey(request.ProducerStreamId)
                        || exact[completionKey].Any() || retired[completionKey].Any()))
                    throw new InvalidOperationException("Repair owner lost its reserved completion publication: " + key);
                Validate(request, mayBePending: true);
            }
        }
        foreach (GameplayOutcomeSnapshot child in rows.Where(row => row.outcomeTypeId ==
                     MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.RepairWorkResult).OutcomeTypeId.Value))
        {
            WorkCompletionIdentityDeliveryRequest request = CreateCompletionRequest(
                FactValue(child, MigratedProducerOutcomeIds.OperationFact.Value),
                FactValue(child, "repair-completion-worker"),
                ParseOrigin(FactValue(child, "repair-completion-origin")), child.absoluteDay,
                FactValue(child, "repair-target-id"),
                float.Parse(FactValue(child, "repair-completion-work"), CultureInfo.InvariantCulture));
            if (Key(child) != request.ChildResultKey)
                throw new InvalidOperationException("Repair child differs from its business operation.");
            Validate(request, pending.ContainsKey(request.OperationId));
        }
        // Also inspect identities whose child has been compacted or maliciously removed.
        foreach (GameplayOutcomeSnapshot identity in rows.Where(row =>
                     row.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
                     && FactValue(row, WorkCompletionIdentityOutcomeIds.WorkIdFact.Value) == BuiltInWorkTypeIds.Repair.Value
                     && FactValue(
                         row,
                         WorkCompletionIdentityOutcomeIds.TerminalOwnerFact.Value)
                        == TerminalOwnerId))
        {
            WorkCompletionIdentityDeliveryRequest request = CreateCompletionRequest(
                FactValue(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact.Value),
                identity.participants.Single(value => value.roleId == WorkCompletionIdentityOutcomeIds.WorkerRole.Value).entityId,
                ParseOrigin(FactValue(identity, WorkCompletionIdentityOutcomeIds.OriginFact.Value)),
                identity.absoluteDay,
                identity.subjects.Single(value => value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value).entityId,
                (float)identity.metrics.Single(value => value.metricId == WorkCompletionIdentityOutcomeIds.ContributionMetric.Value).value);
            if (Key(identity) != WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString())
                throw new InvalidOperationException("Repair identity differs from its canonical child/worker delivery.");
            Validate(request, pending.ContainsKey(request.OperationId));
        }

        void Validate(WorkCompletionIdentityDeliveryRequest request, bool mayBePending)
        {
            GameplayOutcomeSnapshot child = RequireCommitted(request.ChildResultKey, request.AbsoluteDay);
            if (child != null && !MatchesCompletionChild(request, child))
                throw new InvalidOperationException("Repair completion differs from its frozen child: " + request.OperationId);
            string identityKey = WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString();
            bool identityKnown = exact[identityKey].Any() || retired[identityKey].Any();
            if (!cursors.TryGetValue(request.ProducerStreamId, out var cursor))
            {
                if (!mayBePending || identityKnown)
                    throw new InvalidOperationException("Repair child/identity has no pending owner or delivery cursor: " + request.OperationId);
                return;
            }
            if (cursor.operationSequence != request.OperationSequence || cursor.deliveryId != request.DeliveryId
                || cursor.payloadFingerprint != request.PayloadFingerprint)
                throw new InvalidOperationException("Repair completion cursor differs from its frozen request: " + request.OperationId);
            if (cursor.disposition == WorkCompletionIdentityDeliveryDisposition.TerminalRecipientUnavailable)
            {
                if (identityKnown) throw new InvalidOperationException("Unavailable repair recipient has an applied identity.");
                return;
            }
            bool awaitingObservation = cursor.disposition == WorkCompletionIdentityDeliveryDisposition.EffectsAppliedPendingObservation;
            if (awaitingObservation)
            {
                if (!mayBePending)
                    throw new InvalidOperationException("Pending repair identity has no awaiting completion owner.");
                if (!identityKnown) return;
            }
            if (!awaitingObservation && cursor.disposition != WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome)
                throw new InvalidOperationException("Repair completion has an unsupported disposition.");
            GameplayOutcomeSnapshot identity = RequireCommitted(identityKey, request.AbsoluteDay);
            if (identity != null && (identity.outcomeTypeId != WorkCompletionIdentityOutcomeIds.Applied.Value
                || identity.status != GameplayOutcomeStatus.Succeeded
                || child != null && identity.sequence <= child.sequence
                || !identity.participants.Any(value => value.roleId == WorkCompletionIdentityOutcomeIds.WorkerRole.Value
                    && value.entityId == request.Character.Value)
                || !identity.provenance.Any(value => value.kindId == "identity-payload-fingerprint"
                    && value.value == request.PayloadFingerprint)
                || FactValue(identity, WorkCompletionIdentityOutcomeIds.WorkIdFact.Value) != request.WorkId
                || FactValue(identity, WorkCompletionIdentityOutcomeIds.ProductIdFact.Value) != "none"
                || FactValue(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact.Value) != request.OperationId
                || FactValue(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact.Value) != request.TerminalOwnerId
                || FactValue(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value) != request.ChildResultKey
                || FactValue(identity, WorkCompletionIdentityOutcomeIds.OriginFact.Value) != WorkCompletionIdentityOutcomeIds.StableOrigin(request.Origin)
                || FactValue(identity, WorkCompletionIdentityOutcomeIds.ProducerStreamFact.Value) != request.ProducerStreamId
                || !identity.subjects.Any(value => value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value
                    && value.entityId == request.TargetEntityId)
                || !identity.metrics.Any(value => value.metricId == WorkCompletionIdentityOutcomeIds.ContributionMetric.Value
                    && value.value == request.ContributionWorkUnits)))
                throw new InvalidOperationException("Repair completion identity differs from its child/cursor: " + request.OperationId);
        }
        GameplayOutcomeSnapshot RequireCommitted(string key, int day)
        {
            var live = exact[key].ToArray();
            var tomb = retired[key].ToArray();
            if (live.Length + tomb.Length != 1 || live.Length == 1 && live[0].absoluteDay != day
                || tomb.Length == 1 && tomb[0].absoluteDay != day)
                throw new InvalidOperationException("Repair completion has no unique committed outcome/date: " + key);
            return live.Length == 1 ? live[0] : null;
        }
        static string Key(GameplayOutcomeSnapshot row) => new GameplayResultKey(row.producerId,
            new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
        static CharacterCommandOrigin ParseOrigin(string value) => Enum.GetValues(typeof(CharacterCommandOrigin))
            .Cast<CharacterCommandOrigin>().Single(origin => WorkCompletionIdentityOutcomeIds.StableOrigin(origin) == value);
    }

    private static void ValidatePublication(RepairWorkOutcomeSaveData owner,
        GameplayOutcomeLedgerSaveData incoming, DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (counters == null)
            throw new InvalidOperationException("Repair publication restore requires observer counters.");
        var found = incoming.tombstones.Where(value => value != null && Key(value.producerId,
            value.operationId, value.commitRevision, value.localResultIndex) == owner.publicationKey).ToList();
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
            && Key(value.producerId, value.operationId, value.commitRevision, value.localResultIndex) == owner.publicationKey))
            found.Add(new GameplayOutcomeTombstoneSnapshot
            {
                runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                operationId = row.operationId, commitRevision = row.commitRevision,
                localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
                absoluteDay = row.absoluteDay, parentRunId = row.parentRunId, parentSequence = row.parentSequence,
                deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
                recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
                deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
                canonicalHash = row.immutablePayloadHash,
                terminalTier = NarrativeMemoryTier.Forgotten
            });
        if (found.Count > 1)
            throw new InvalidOperationException("Repair publication has duplicate committed keys.");
        var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var validationLedger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 2, retainedLargePageCount: 2));
        var subset = new GameplayOutcomeLedgerSaveData
        {
            runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
            nextSequence = incoming.nextSequence,
            knownResultKeyHighWater = found.Count, tombstones = found
        };
        var candidate = validationLedger.PrepareGameplayOutcomeRestore(subset);
        validationLedger.BeginRestoreCandidate(); validationLedger.PublishGameplayOutcomeRestore(candidate);
        validationLedger.PublishRestoreCandidate(); validationLedger.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(validationLedger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(),
            validationLedger, recorder, validationLedger);
        migrated.Restore(migrated.PrepareRestore(counters));
        var observer = new MigratedDomainFactObserver<RepairWorkCompletedFact>(RepairWorkCompletedFactMapping.Capture,
            migrated, new GameplayDomainFactCommitGroup(recorder, validationLedger));
        bool accepted = observer.TryPrepareReplay(owner.publicationKey, new RepairWorkCompletedFact(owner),
            out IDomainFactPreparedPublication publication, out string failure);
        publication?.Cancel();
        if (!accepted)
            throw new InvalidOperationException("Repair frozen publication payload conflict: " + failure);

        static string Key(string producer, string operation, long revision, int index) =>
            new GameplayResultKey(producer, new GameplayOperationId(operation), revision, index).ToString();
    }
}
