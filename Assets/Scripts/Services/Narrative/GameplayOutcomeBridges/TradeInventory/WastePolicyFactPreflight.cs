using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// The observer owns native identity/hash checks. The policy owner stores only domain facts and opaque proofs.
public sealed class WastePolicyFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(DungeonSaveSectionPayload.ReadOrNew<DungeonWasteProcessingSaveData>(save, WasteProcessingSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("waste-policy-save-join:" + error.Message); }
    }

    public static void Validate(DungeonWasteProcessingSaveData owner, GameplayOutcomeLedgerSaveData incoming)
    {
        if (owner?.pendingPolicyChanges == null || owner.policies == null || owner.nextPolicySequence <= 0
            || owner.pendingPolicyChanges.Count > 1 || incoming?.tombstones == null
            || incoming.outbox == null || incoming.exactOutcomes == null)
            throw new InvalidOperationException("waste-policy-save-state-missing");
        string producer = TradeInventoryOutcomeDefinitions.Get(TradeInventoryOutcomeKind.WastePolicyCommand).ProducerId;
        var known = incoming.tombstones.Where(row => row != null && row.producerId == producer).ToList();
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null && row.producerId == producer))
            known.Add(new GameplayOutcomeTombstoneSnapshot
            {
                runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                ownerRevision = row.ownerRevision, temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
                absoluteDay = row.absoluteDay, parentRunId = row.parentRunId, parentSequence = row.parentSequence,
                deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
                recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
                deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
                canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
            });
        if (known.Any(row => row.ownerRevision < 1 || row.ownerRevision >= owner.nextPolicySequence
                || row.commitRevision != row.ownerRevision || row.localResultIndex != 0
                || !owner.policies.Any(policy => policy != null
                    && row.operationId == WastePolicyChangedFact.OperationIdFor(policy.origin, row.ownerRevision)))
            || known.GroupBy(row => row.ownerRevision).Any(group => group.Count() != 1))
            throw new InvalidOperationException("waste-policy-native-sequence-conflict");

        foreach (var pending in owner.pendingPolicyChanges)
        {
            if (pending == null) throw new InvalidOperationException("waste-policy-pending-missing");
            pending.Validate(owner.nextPolicySequence);
            var fact = new WastePolicyChangedFact(pending.fact);
            if (!WastePolicyChangedFact.SamePolicy(fact.AfterPolicy(), owner.policies.SingleOrDefault(row => row?.origin == fact.Origin)))
                throw new InvalidOperationException("waste-policy-applied-owner-conflict");
            var receipt = WastePolicyFactObservation.Capture(fact);
            if (pending.publicationKey != receipt.ResultKey.ToString()) throw new InvalidOperationException("waste-policy-key-conflict");
            var found = known.Where(row => row.ownerRevision == fact.OwnerRevision).ToList();
            if (pending.publicationId.Length > 0 && (found.Count != 1
                || pending.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(found[0].runId), found[0].sequence).ToString()
                || pending.publicationFingerprint != found[0].canonicalHash))
                throw new InvalidOperationException("waste-policy-proof-conflict");
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new TradeInventoryOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new TradeInventoryOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4, retainedSmallPageCount: 2, retainedLargePageCount: 2));
            var candidate = ledger.PrepareGameplayOutcomeRestore(new GameplayOutcomeLedgerSaveData
            {
                runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
                nextSequence = incoming.nextSequence, knownResultKeyHighWater = found.Count, tombstones = found
            });
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var prepared = recorder.TryPrepare(receipt, out var token);
            recorder.CancelPrepared(token);
            if (found.Count == 0 ? !prepared.Success : prepared.Code != OutcomePrepareCode.AlreadyTerminal)
                throw new InvalidOperationException("waste-policy-canonical-fact-conflict:" + prepared.Code + ":" + prepared.DetailCode);
        }
    }
}
