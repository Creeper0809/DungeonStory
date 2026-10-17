using System;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Only this observation boundary translates research facts into ledger identities and receipts.</summary>
public static class ResearchWorkFactObservation
{
    public static GameplayResultKey Key(long revision)
    {
        if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
        return new GameplayResultKey(ResearchWorkOutcomeIds.ProducerId,
            new GameplayOperationId("research-work:" + revision.ToString(CultureInfo.InvariantCulture)), revision, 0);
    }

    public static ResearchWorkOutcomeReceipt Capture(ResearchWorkAppliedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        return new ResearchWorkOutcomeReceipt(Key(fact.Revision), fact.AbsoluteDay,
            fact.SubjectId, fact.SubjectName, fact.ResearcherId, fact.ResearcherName,
            fact.FacilityId, fact.FacilityName, fact.Before, fact.After, fact.Required,
            fact.Completed, fact.Unlocks, fact.Cause, fact.Leap, fact.AftermathUntil, fact.Equipment);
    }

    public static bool TryReadKey(string serialized, out GameplayResultKey key)
    {
        key = default;
        string[] parts = (serialized ?? string.Empty).Split('|');
        if (parts.Length != 4 || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long revision)
            || revision <= 0) return false;
        key = Key(revision);
        if (key.ToString() == serialized) return true;
        key = default;
        return false;
    }

    internal static void ValidatePending(ResearchWorkAppliedFact fact, string publicationKey,
        GameplayOutcomeLedgerSaveData incoming)
    {
        var receipt = Capture(fact);
        if (receipt.ResultKey.ToString() != publicationKey)
            throw new InvalidOperationException("research-pending-observer-key-mismatch");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
            && value.producerId == receipt.ResultKey.ProducerId
            && value.operationId == receipt.ResultKey.OperationId.Value
            && value.commitRevision == fact.Revision && value.localResultIndex == 0).ToArray();
        var retired = incoming.tombstones.Where(value => value != null
            && value.producerId == receipt.ResultKey.ProducerId
            && value.operationId == receipt.ResultKey.OperationId.Value
            && value.commitRevision == fact.Revision && value.localResultIndex == 0).ToArray();
        if (rows.Length + retired.Length > 1)
            throw new InvalidOperationException("research-pending-canonical-duplicate");
        // Cold comparison only: one bounded scratch page and the incoming identity/hash.
        // No live ledger is captured, committed, published or mutated by this validator.
        var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new ResearchWorkOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new ResearchWorkOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 0, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 0));
        bool exists = rows.Length + retired.Length == 1;
        if (exists)
        {
            string run = rows.Length == 1 ? rows[0].runId : retired[0].runId;
            long sequence = rows.Length == 1 ? rows[0].sequence : retired[0].sequence;
            string hash = rows.Length == 1 ? rows[0].immutablePayloadHash : retired[0].canonicalHash;
            var comparison = new GameplayOutcomeLedgerSaveData
            {
                runId = run, worldEpoch = incoming.worldEpoch, nextSequence = checked(sequence + 1),
                knownResultKeyHighWater = 1,
                tombstones = new() { new GameplayOutcomeTombstoneSnapshot
                {
                    runId = run, sequence = sequence, producerId = receipt.ResultKey.ProducerId,
                    operationId = receipt.ResultKey.OperationId.Value, commitRevision = fact.Revision,
                    localResultIndex = 0, ownerRevision = fact.Revision,
                    terminalTier = NarrativeMemoryTier.Forgotten, canonicalHash = hash
                } }
            };
            var candidate = ledger.PrepareGameplayOutcomeRestore(comparison);
            ledger.BeginRestoreCandidate();
            ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate();
            ledger.CompleteRestoreCandidate();
        }
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var result = recorder.TryPrepare(receipt, out var token);
        recorder.CancelPrepared(token);
        if (exists ? result.Code != OutcomePrepareCode.AlreadyTerminal : !result.Success)
            throw new InvalidOperationException("research-pending-canonical-payload-mismatch:" + result.Code + ":" + result.DetailCode);
    }
}
