using System;
using System.Linq;

public sealed class ProductionCompletionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(DungeonSaveSectionPayload.ReadOrNew<DungeonProductionBillSaveData>(save, ProductionBillsSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("production-completion-save-join:" + error.Message); }
    }

    public static void Validate(DungeonProductionBillSaveData owner, GameplayOutcomeLedgerSaveData incoming)
    {
        if (owner?.bills == null || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("production-completion-save-state-missing");
        foreach (var bill in owner.bills)
        {
            if (bill?.completionObservations == null || bill.completionObservations.Count > 1)
                throw new InvalidOperationException("production-completion-owner-list-invalid");
            foreach (var observation in bill.completionObservations)
            {
                observation.Validate(bill.billId, bill.cycleSequence, bill.recipeId, bill.buildingInstanceId);
                var fact = new ProductionCycleCompletedFact(observation.fact);
                var receipt = ProductionCompletionFactObservation.Capture(fact);
                if (observation.publicationKey != receipt.ResultKey.ToString())
                    throw new InvalidOperationException("production-completion-publication-key-conflict");
                ValidatePhysicalOwner(bill, observation.fact);
                var known = incoming.tombstones.Where(row => row != null && row.producerId == receipt.ResultKey.ProducerId
                    && row.operationId == receipt.ResultKey.OperationId.Value && row.commitRevision == receipt.ResultKey.CommitRevision
                    && row.localResultIndex == receipt.ResultKey.LocalResultIndex).ToList();
                foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null
                    && row.producerId == receipt.ResultKey.ProducerId && row.operationId == receipt.ResultKey.OperationId.Value
                    && row.commitRevision == receipt.ResultKey.CommitRevision && row.localResultIndex == receipt.ResultKey.LocalResultIndex))
                    known.Add(new GameplayOutcomeTombstoneSnapshot
                    {
                        runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
                        commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                        temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
                        parentRunId = row.parentRunId, parentSequence = row.parentSequence, deliveryMetadataVersion = row.deliveryMetadataVersion,
                        deliveryClockId = row.deliveryClockId, recordedAbsoluteHour = row.recordedAbsoluteHour,
                        publishedRevision = row.publishedRevision, deliveryFaultCount = row.deliveryFaultCount,
                        deliveryDelayReason = row.deliveryDelayReason, canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                    });
                if (known.Count > 1 || known.Any(row => row.ownerRevision != bill.cycleSequence)
                    || observation.publicationId.Length > 0 && (known.Count != 1
                        || observation.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(known[0].runId), known[0].sequence).ToString()
                        || observation.publicationFingerprint != known[0].canonicalHash))
                    throw new InvalidOperationException("production-completion-publication-proof-conflict");
                var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new ProductionCompletedOutcomeDescriptor() },
                    new IGameplayOutcomeAdapterRegistration[] { new ProductionCompletedOutcomeAdapter() });
                var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits());
                var candidate = ledger.PrepareGameplayOutcomeRestore(new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
                    nextSequence = incoming.nextSequence, knownResultKeyHighWater = known.Count, tombstones = known });
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                var recorder = new GameplayOutcomeRecorder(ledger, registry, new DungeonStory.Foundation.GameEventBus());
                var prepared = recorder.TryPrepare(receipt, out var token); recorder.CancelPrepared(token);
                if (known.Count == 0 ? !prepared.Success : prepared.Code != OutcomePrepareCode.AlreadyTerminal)
                    throw new InvalidOperationException("production-completion-canonical-fact-conflict:" + prepared.Code + ":" + prepared.DetailCode);
            }
        }
    }

    private static void ValidatePhysicalOwner(ProductionBillSaveData bill, ProductionCycleCompletedFactData fact)
    {
        if (fact.wipInputCommitId != bill.wipInputCommitId || fact.wipInputQuantity != bill.wipInputQuantity
            || fact.wipInputMassGrams != bill.wipInputMassGrams || fact.processCleanWaterMassGrams != bill.processCleanWaterMassGrams
            || fact.processWastewaterMassGrams != bill.processWastewaterMassGrams)
            throw new InvalidOperationException("production-completion-input-owner-conflict");
        var batch = bill.preparedOutput;
        if (batch?.phase == ProductionPreparedOutputPhase.Unresolved)
        {
            if (!bill.outputOutcomeResolved || bill.resolvedOutputs == null
                || bill.resolvedOutputs.Any(row => row.committedAmount != row.amount)
                || bill.resolvedOutputs.Count(row => row.amount > 0) != fact.lines.Count
                || fact.lines.Any(line => !bill.resolvedOutputs.Any(row => row.outputLineId == line.outputLineId
                    && row.itemId == line.itemId && row.outputCapabilityFingerprint == line.capabilityFingerprint
                    && row.committedAmount == line.quantity && row.committedMassGrams == line.massGrams))
                || bill.resolvedOutputs.Sum(row => row.committedAmount) != fact.lines.Sum(row => row.quantity)
                || bill.resolvedOutputs.Sum(row => row.committedMassGrams) != fact.lines.Sum(row => row.massGrams))
                throw new InvalidOperationException("production-completion-exact-owner-conflict");
            return;
        }
        if (batch == null || batch.phase is not (ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending or ProductionPreparedOutputPhase.Completed)
            || fact.batchCommitId != batch.batchCommitId || fact.outcomeFingerprint != batch.outcomeFingerprint
            || fact.plannedOutputFingerprint != batch.admissionFingerprint || fact.destinationId != batch.destinationId
            || fact.declaredLossMassGrams != batch.totalDeclaredLossMassGrams
            || fact.declaredExternalInputMassGrams != batch.totalDeclaredExternalInputMassGrams
            || batch.lines == null || fact.lines.Count != batch.lines.Count(line => line != null
                && ProductionOutputRoleRules.IsPhysical(line.role) && line.rollSucceeded && line.quantity > 0 && line.exactMassGrams > 0)
            || fact.lines.Any(line => !batch.lines.Any(physical => physical != null
                && physical.outputLineId == line.outputLineId && physical.itemId == line.itemId
                && physical.outputCapabilityFingerprint == line.capabilityFingerprint && physical.quantity == line.quantity
                && physical.exactMassGrams == line.massGrams && line.commitIds.Count == 1 && line.commitIds[0] == physical.lineCommitId))
            || batch.physicalCandidates == null || fact.stacks.Count != batch.physicalCandidates.Count
            || fact.stacks.Any(stack => !batch.physicalCandidates.Any(physical => physical.stackId == stack.stackId
                && physical.outputLineId == stack.outputLineId && physical.itemId == stack.itemId
                && physical.quantity == stack.quantity && physical.massGrams == stack.massGrams)))
            throw new InvalidOperationException("production-completion-physical-owner-conflict");
    }
}

