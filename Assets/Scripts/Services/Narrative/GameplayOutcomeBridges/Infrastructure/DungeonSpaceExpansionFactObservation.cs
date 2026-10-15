using System;
using System.Linq;

public static class DungeonSpaceExpansionFactObservation
{
    public static DungeonSpaceExpansionOutcomeReceipt Capture(DungeonSpaceExpandedFact fact,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        var data = (fact ?? throw new ArgumentNullException(nameof(fact))).Capture();
        GameplayResultKey parentKey = default;
        GameplayOutcomeId parentId = default;
        if (data.trigger == DungeonSpaceExpansionTrigger.ResearchCompletion)
        {
            if (!ResearchWorkFactObservation.TryReadKey(data.parentResearchPublicationKey, out parentKey)
                || !diagnostics.TryGetResultIdentity(parentKey, out var parent)
                || parent.State < GameplayOutcomeReplayState.Committed || !parent.OutcomeId.IsValid
                || !parent.HasCanonicalPayloadHash)
                throw new InvalidOperationException("dungeon-space-parent-research-uncommitted");
            parentId = parent.OutcomeId;
        }
        return new DungeonSpaceExpansionOutcomeReceipt(data.projectId,
            DungeonSpaceExpansionOutcomeNames.Snapshot(data.projectId, data.projectName),
            fact.Revision, data.tier, data.previousColumns, data.currentColumns,
            data.previousWidth, data.currentWidth, data.absoluteDay, data.entranceX, data.entranceY,
            data.trigger, parentKey, parentId);
    }

    internal static void ValidatePending(DungeonSpaceExpansionPublicationData pending,
        GameplayOutcomeLedgerSaveData incoming)
    {
        var fact = new DungeonSpaceExpandedFact(pending);
        var key = DungeonSpaceExpansionOutcomeIds.ResultKey(pending.projectId, fact.Revision);
        if (key.ToString() != pending.publicationKey)
            throw new InvalidOperationException("space-pending-observer-key-mismatch");
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new ResearchWorkOutcomeDescriptor(),
                new DungeonSpaceExpansionOutcomeDescriptor(new DungeonStory.Narrative.Korean.KoreanJosaFormatter()) },
            new IGameplayOutcomeAdapterRegistration[] { new ResearchWorkOutcomeAdapter(), new DungeonSpaceExpansionOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 0, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 1, retainedLargePageCount: 0));
        var comparison = new GameplayOutcomeLedgerSaveData
        { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = 1 };
        bool exists = CopyIdentity(key);
        if (pending.trigger == DungeonSpaceExpansionTrigger.ResearchCompletion)
        {
            if (!ResearchWorkFactObservation.TryReadKey(pending.parentResearchPublicationKey, out var parent)
                || !CopyIdentity(parent))
                throw new InvalidOperationException("space-pending-parent-not-committed");
        }
        comparison.knownResultKeyHighWater = comparison.tombstones.Count;
        if (comparison.tombstones.Count > 0)
        {
            var candidate = ledger.PrepareGameplayOutcomeRestore(comparison);
            ledger.BeginRestoreCandidate();
            ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate();
            ledger.CompleteRestoreCandidate();
        }
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new DungeonStory.Foundation.GameEventBus());
        var prepared = recorder.TryPrepare(Capture(fact, ledger), out var token);
        recorder.CancelPrepared(token);
        if (exists ? prepared.Code != OutcomePrepareCode.AlreadyTerminal : !prepared.Success)
            throw new InvalidOperationException("space-pending-canonical-payload-mismatch:" + prepared.Code + ":" + prepared.DetailCode);

        bool CopyIdentity(GameplayResultKey identity)
        {
            var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
                && value.producerId == identity.ProducerId && value.operationId == identity.OperationId.Value
                && value.commitRevision == identity.CommitRevision && value.localResultIndex == identity.LocalResultIndex).ToArray();
            var retired = incoming.tombstones.Where(value => value != null
                && value.producerId == identity.ProducerId && value.operationId == identity.OperationId.Value
                && value.commitRevision == identity.CommitRevision && value.localResultIndex == identity.LocalResultIndex).ToArray();
            if (rows.Length + retired.Length > 1) throw new InvalidOperationException("space-pending-identity-duplicate");
            if (rows.Length + retired.Length == 0) return false;
            var saved = rows.Length == 1
                ? new GameplayOutcomeTombstoneSnapshot
                {
                    runId = rows[0].runId, sequence = rows[0].sequence, producerId = rows[0].producerId,
                    operationId = rows[0].operationId, commitRevision = rows[0].commitRevision,
                    localResultIndex = rows[0].localResultIndex, ownerRevision = rows[0].ownerRevision,
                    terminalTier = NarrativeMemoryTier.Forgotten, canonicalHash = rows[0].immutablePayloadHash
                } : retired[0];
            comparison.tombstones.Add(saved);
            comparison.nextSequence = Math.Max(comparison.nextSequence, checked(saved.sequence + 1));
            return true;
        }
    }
}
