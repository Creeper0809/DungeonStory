using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Environment;

// Pure join policy. The suppression operation remains the durable delivery owner.
public static class EnvironmentalFireCompletionIdentity
{
    public static bool RequiresIdentity(EnvironmentalFireSuppressionSaveRecord owner) =>
        owner != null && owner.phase == (int)EnvironmentalFireSuppressionPhase.Applied
        && (owner.mode == (int)EnvironmentalFireSuppressionMode.Water || owner.intensityAfter <= 0.0001f);

    public static GameplayResultKey ChildKey(EnvironmentalFireSuppressionSaveRecord owner) =>
        new(EnvironmentOutcomeIds.FireSuppressionProducer, new GameplayOperationId(owner.operationId),
            owner.outcomeRevision, 0);

    public static WorkCompletionIdentityDeliveryRequest CreateRequest(EnvironmentalFireSuppressionSaveRecord owner)
    {
        if (!RequiresIdentity(owner) || owner.legacyPreOutcome || owner.outcomeRevision <= 0
            || !GameplayOutcomeStableIdSyntax.IsValid(owner.targetId))
            throw new ArgumentException("Fire completion requires its frozen terminal suppression operation.");
        return WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(new WorkCompletedIdentityEvent(
            new CharacterId(owner.workerId), BuiltInWorkTypeIds.ThreatMitigation.Value, string.Empty,
            (CharacterCommandOrigin)owner.commandOrigin, owner.absoluteDay,
            owner.operationId, EnvironmentOutcomeIds.FireSuppressionProducer, ChildKey(owner).ToString(),
            owner.targetId, owner.approvedWork));
    }

    public static GameplayResultKey RequireCompletionChild(WorkCompletionIdentityDeliveryRequest request)
    {
        GameplayResultKey child = WorkCompletionChildReference.Require(
            request.OperationId, request.TerminalOwnerId, request.ChildResultKey);
        WorkCompletionIdentityDeliveryRequest canonical =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(request.ToEvent());
        if (request.WorkId != BuiltInWorkTypeIds.ThreatMitigation.Value
            || child.ProducerId != EnvironmentOutcomeIds.FireSuppressionProducer
            || request.TerminalOwnerId != child.ProducerId || child.LocalResultIndex != 0
            || request.ProductId != string.Empty
            || request.SkillModifierProvenance != WorkCompletionIdentityReferenceDefaults.None
            || request.PayloadFingerprint != canonical.PayloadFingerprint)
            throw new ArgumentException("Fire completion requires its canonical child/worker delivery.");
        return child;
    }

    public static bool MatchesCompletionChild(WorkCompletionIdentityDeliveryRequest request,
        GameplayOutcomeSnapshot child) => child != null
        && child.outcomeTypeId == EnvironmentOutcomeIds.FireSuppression.Value
        && child.status == GameplayOutcomeStatus.Succeeded && child.absoluteDay == request.AbsoluteDay
        && Key(child) == request.ChildResultKey
        && Fact(child, "fire-completion-required") == "true"
        && Fact(child, "fire-completion-target") == request.TargetEntityId
        && Fact(child, "fire-completion-origin") == WorkCompletionIdentityOutcomeIds.StableOrigin(request.Origin)
        && Fact(child, "fire-completion-work") == request.ContributionWorkUnits.ToString("R", CultureInfo.InvariantCulture)
        && child.participants.Count(value => value.roleId == EnvironmentOutcomeIds.ActorRole.Value
            && value.entityId == request.Character.Value) == 1;

    public static void ValidateRestoreJoin(DungeonEnvironmentalFireSaveData fire,
        IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> deliveries, GameplayOutcomeLedgerSaveData ledger)
    {
        if (fire?.version != DungeonEnvironmentalFireSaveData.CurrentVersion || fire.suppressionOperations == null
            || ledger?.outbox == null || ledger.exactOutcomes == null || ledger.tombstones == null)
            throw new InvalidOperationException("Fire completion join requires current owners and ledger.");
        var owners = fire.suppressionOperations.ToDictionary(value => value.operationId, StringComparer.Ordinal);
        var targets = fire.activeFires.Concat(fire.history.Select(value => value.fire))
            .ToDictionary(value => value.fireId, value => value.targetId, StringComparer.Ordinal);
        var cursors = WorkCompletionIdentityDeliveryLedger.ValidateAndClone(deliveries)
            .ToDictionary(value => value.producerStreamId, StringComparer.Ordinal);
        GameplayOutcomeSnapshot[] rows = ledger.outbox.Concat(ledger.exactOutcomes).ToArray();
        var exact = rows.ToLookup(Key);
        var retired = ledger.tombstones.ToLookup(value => new GameplayResultKey(value.producerId,
            new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex).ToString());

        foreach (EnvironmentalFireSuppressionSaveRecord owner in owners.Values)
        {
            if (owner.legacyPreOutcome || !targets.TryGetValue(owner.fireId, out string target)
                || target != owner.targetId)
                throw new InvalidOperationException("Fire completion owner differs from its fire target.");
            string childKey = ChildKey(owner).ToString();
            bool childKnown = exact[childKey].Any() || retired[childKey].Any();
            if (owner.phase != (int)EnvironmentalFireSuppressionPhase.Applied)
            {
                if (owner.completionIdentityAcknowledged || childKnown && !owner.outcomePending)
                    throw new InvalidOperationException("Unapplied fire owner has an acknowledged result.");
                continue;
            }
            if (!childKnown && owner.outcomePending && !owner.completionIdentityAcknowledged)
                continue;
            GameplayOutcomeSnapshot child = RequireCommitted(childKey, owner.absoluteDay);
            if (child != null && (child.outcomeTypeId != EnvironmentOutcomeIds.FireSuppression.Value
                || child.status != GameplayOutcomeStatus.Succeeded
                || Fact(child, "fire-completion-target") != owner.targetId
                || Fact(child, "fire-completion-origin") != WorkCompletionIdentityOutcomeIds.StableOrigin((CharacterCommandOrigin)owner.commandOrigin)
                || Fact(child, "fire-completion-work") != owner.approvedWork.ToString("R", CultureInfo.InvariantCulture)
                || Fact(child, "fire-completion-required") != (RequiresIdentity(owner) ? "true" : "false")
                || !child.participants.Any(value => value.roleId == EnvironmentOutcomeIds.ActorRole.Value && value.entityId == owner.workerId)))
                throw new InvalidOperationException("Fire owner differs from its suppression child: " + owner.operationId);
            if (!RequiresIdentity(owner))
            {
                if (owner.completionIdentityAcknowledged)
                    throw new InvalidOperationException("Intermediate suppression has a completion acknowledgement.");
                continue;
            }
            Validate(owner, child);
        }
        foreach (GameplayOutcomeSnapshot row in rows)
        {
            bool suppression = row.outcomeTypeId == EnvironmentOutcomeIds.FireSuppression.Value;
            bool identity = row.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
                && Fact(row, WorkCompletionIdentityOutcomeIds.WorkIdFact.Value) == BuiltInWorkTypeIds.ThreatMitigation.Value;
            if (!suppression && !identity) continue;
            string operation = suppression ? row.operationId : Fact(row, WorkCompletionIdentityOutcomeIds.OperationIdFact.Value);
            if (!owners.TryGetValue(operation, out var owner)
                || suppression && Key(row) != ChildKey(owner).ToString()
                || identity && (!RequiresIdentity(owner)
                    || Key(row) != WorkCompletionIdentityOutcomeIds.ResultKey(CreateRequest(owner)).ToString()))
                throw new InvalidOperationException("Fire child/identity has no exact suppression owner.");
            if (identity) Validate(owner, RequireCommitted(ChildKey(owner).ToString(), owner.absoluteDay));
        }

        void Validate(EnvironmentalFireSuppressionSaveRecord owner, GameplayOutcomeSnapshot child)
        {
            WorkCompletionIdentityDeliveryRequest request = CreateRequest(owner);
            if (child != null && !MatchesCompletionChild(request, child))
                throw new InvalidOperationException("Fire completion differs from its terminal child.");
            string identityKey = WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString();
            bool identityKnown = exact[identityKey].Any() || retired[identityKey].Any();
            if (string.IsNullOrEmpty(owner.completionPublicationKey))
            {
                if (identityKnown || cursors.ContainsKey(request.ProducerStreamId) || owner.completionIdentityAcknowledged)
                    throw new InvalidOperationException("Fire completion lost its saved publication binding.");
            }
            else if (owner.completionPublicationKey != identityKey)
                throw new InvalidOperationException("Fire completion publication binding differs from its frozen request.");
            if (!cursors.TryGetValue(request.ProducerStreamId, out var cursor))
            {
                if (owner.completionIdentityAcknowledged || identityKnown)
                    throw new InvalidOperationException("Fire completion has no delivery cursor.");
                return; // Committed suppression; mandatory identity is durably pending here.
            }
            if (cursor.operationSequence != 0 || cursor.deliveryId != request.DeliveryId
                || cursor.payloadFingerprint != request.PayloadFingerprint)
                throw new InvalidOperationException("Fire completion cursor differs from its frozen request.");
            if (cursor.disposition == WorkCompletionIdentityDeliveryDisposition.TerminalRecipientUnavailable)
            {
                if (identityKnown) throw new InvalidOperationException("Unavailable fire recipient has an applied identity.");
                return;
            }
            bool awaitingObservation = cursor.disposition == WorkCompletionIdentityDeliveryDisposition.EffectsAppliedPendingObservation;
            if (awaitingObservation)
            {
                if (owner.completionIdentityAcknowledged)
                    throw new InvalidOperationException("Fire completion ACK precedes its identity observation.");
                if (!identityKnown) return;
            }
            if (!awaitingObservation && cursor.disposition != WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome)
                throw new InvalidOperationException("Fire completion has an unsupported delivery disposition.");
            GameplayOutcomeSnapshot identity = RequireCommitted(identityKey, request.AbsoluteDay);
            if (identity != null && (identity.outcomeTypeId != WorkCompletionIdentityOutcomeIds.Applied.Value
                || identity.status != GameplayOutcomeStatus.Succeeded || child != null && identity.sequence <= child.sequence
                || !identity.participants.Any(value => value.roleId == WorkCompletionIdentityOutcomeIds.WorkerRole.Value && value.entityId == request.Character.Value)
                || !identity.provenance.Any(value => value.kindId == "identity-payload-fingerprint" && value.value == request.PayloadFingerprint)
                || Fact(identity, WorkCompletionIdentityOutcomeIds.WorkIdFact.Value) != request.WorkId
                || Fact(identity, WorkCompletionIdentityOutcomeIds.ProductIdFact.Value) != "none"
                || Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact.Value) != request.OperationId
                || Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact.Value) != request.TerminalOwnerId
                || Fact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value) != request.ChildResultKey
                || Fact(identity, WorkCompletionIdentityOutcomeIds.OriginFact.Value) != WorkCompletionIdentityOutcomeIds.StableOrigin(request.Origin)
                || Fact(identity, WorkCompletionIdentityOutcomeIds.ProducerStreamFact.Value) != request.ProducerStreamId
                || !identity.subjects.Any(value => value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value && value.entityId == request.TargetEntityId)
                || !identity.metrics.Any(value => value.metricId == WorkCompletionIdentityOutcomeIds.ContributionMetric.Value && value.value == request.ContributionWorkUnits)))
                throw new InvalidOperationException("Fire completion identity differs from its child/cursor.");
        }
        GameplayOutcomeSnapshot RequireCommitted(string key, int day)
        {
            var live = exact[key].ToArray();
            var tomb = retired[key].ToArray();
            if (live.Length + tomb.Length != 1 || live.Length == 1 && live[0].absoluteDay != day
                || tomb.Length == 1 && tomb[0].absoluteDay != day)
                throw new InvalidOperationException("Fire completion has no unique committed outcome/date: " + key);
            return live.Length == 1 ? live[0] : null;
        }
    }

    private static string Fact(GameplayOutcomeSnapshot row, string id) => row.facts.SingleOrDefault(value => value.factId == id)?.value;
    private static string Key(GameplayOutcomeSnapshot row) => new GameplayResultKey(row.producerId,
        new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
}
