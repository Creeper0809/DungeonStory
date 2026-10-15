using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// Detached observer-side validation. No gameplay effect, publication or owner mutation occurs here.
public static class SurgeryResolvedFactRestoreJoin
{
    public static void Validate(IEnumerable<SurgeryOrder> source, GameplayOutcomeLedgerSaveData incoming)
    {
        if (source == null || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("surgery-observation-join-missing");
        SurgeryOrder[] orders = source.ToArray();
        foreach (var order in orders) SurgerySaveValidation.ValidateObservationState(order);
        if (orders.Length == 0) return;
        string producer = SurgeryOutcomeIds.ResultKey(orders[0].orderId).ProducerId;
        var exact = incoming.exactOutcomes.Concat(incoming.outbox).Where(row => row != null && row.producerId == producer)
            .ToLookup(row => (row.producerId, row.operationId));
        var retired = incoming.tombstones.Where(row => row != null && row.producerId == producer)
            .ToLookup(row => (row.producerId, row.operationId));
        GameplayOutcomeLedger comparison = null;
        GameplayOutcomeRecorder recorder = null;
        foreach (var order in orders)
        {
            var key = SurgeryOutcomeIds.ResultKey(order.orderId);
            var tuple = (key.ProducerId, key.OperationId.Value);
            var liveRows = exact[tuple].ToArray();
            var tombstones = retired[tuple].ToArray();
            if (liveRows.Length + tombstones.Length > 1)
                throw new InvalidOperationException("surgery-observation-duplicate-canonical-result");
            var live = liveRows.SingleOrDefault();
            var row = live == null ? tombstones.SingleOrDefault() : RetiredCopy(live);
            if (order.resolutionPublicationKey.Length > 0 && order.resolutionPublicationKey != key.ToString())
                throw new InvalidOperationException("surgery-observation-key-conflict");
            if (order.resolvedFact.IsEmpty)
            {
                if (row != null) throw new InvalidOperationException("surgery-observation-canonical-without-fact");
                continue;
            }
            if (row == null && order.resolutionFactAccepted)
                throw new InvalidOperationException("surgery-observation-accepted-proof-missing");
            if (row != null)
            {
                if (row.runId != incoming.runId || row.sequence < 0 || row.sequence >= incoming.nextSequence
                    || row.commitRevision != key.CommitRevision || row.ownerRevision != key.CommitRevision
                    || row.localResultIndex != key.LocalResultIndex || row.absoluteDay != order.completionAbsoluteDay
                    || order.resolutionFactAccepted && (order.resolutionPublicationId !=
                        new GameplayOutcomeId(new GameplayOutcomeRunId(row.runId), row.sequence).ToString()
                        || order.resolutionFingerprint != row.canonicalHash)
                    || order.resolutionFactDelivered && incoming.outbox.Contains(live))
                    throw new InvalidOperationException("surgery-observation-canonical-proof-conflict");
            }
            if (comparison == null)
            {
                var registry = new GameplayOutcomeRegistry(
                    new IGameplayOutcomeDescriptor[] { new SurgeryOutcomeDescriptor(new KoreanJosaFormatter()) },
                    new IGameplayOutcomeAdapterRegistration[] { new SurgeryOutcomeAdapter() });
                comparison = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                    smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
                    retainedSmallPageCount: 1, retainedLargePageCount: 1));
                recorder = new GameplayOutcomeRecorder(comparison, registry, new GameEventBus());
            }
            var subset = new GameplayOutcomeLedgerSaveData
            {
                runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
                nextSequence = incoming.nextSequence, knownResultKeyHighWater = row == null ? 0 : 1
            };
            if (row != null) subset.tombstones.Add(Clone(row));
            var candidate = comparison.PrepareGameplayOutcomeRestore(subset);
            comparison.BeginRestoreCandidate(); comparison.PublishGameplayOutcomeRestore(candidate);
            comparison.PublishRestoreCandidate(); comparison.CompleteRestoreCandidate();
            var receipt = SurgeryResolvedFactMapping.Capture(new SurgeryResolvedFact(order.resolvedFact));
            var prepared = recorder.TryPrepare(receipt, out var token);
            recorder.CancelPrepared(token);
            if (row == null ? !prepared.Success : prepared.Code != OutcomePrepareCode.AlreadyTerminal)
                throw new InvalidOperationException("surgery-observation-canonical-fact-conflict:" + prepared.DetailCode);
        }
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row) => new()
    {
        runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
        commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
        temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
        parentRunId = row.parentRunId, parentSequence = row.parentSequence,
        deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
        recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
        deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
        canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
    };

    private static GameplayOutcomeTombstoneSnapshot Clone(GameplayOutcomeTombstoneSnapshot row) => new()
    {
        runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
        commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
        temporalMetadataVersion = row.temporalMetadataVersion, absoluteDay = row.absoluteDay,
        parentRunId = row.parentRunId, parentSequence = row.parentSequence,
        deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
        recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
        deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
        canonicalHash = row.canonicalHash, terminalTier = row.terminalTier
    };
}
