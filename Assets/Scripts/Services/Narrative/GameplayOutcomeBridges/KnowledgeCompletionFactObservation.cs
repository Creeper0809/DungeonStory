using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Translates a frozen knowledge-completion fact at the ledger boundary.</summary>
public static class KnowledgeCompletionFactObservation
{
    public static KnowledgeCompletionOutcomeReceipt Capture(
        KnowledgeResidueCompletedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));

        KnowledgeCompletionExperienceData experience = fact.Experience;
        PhysicalItemBatchDispositionReceipt input = fact.Physical.Receipt;
        var resultKey = new GameplayResultKey(
            KnowledgeCompletionOutcomeIds.ProducerId,
            new GameplayOperationId(input.OperationId),
            input.OwnerRevision,
            0);
        string taskLabel = experience.use == KnowledgeResidueUse.CodexAnalysis
            ? "도감 단서 분석"
            : "지역 기억 정찰";
        return new KnowledgeCompletionOutcomeReceipt(
            resultKey,
            experience.absoluteDay,
            experience.taskId,
            taskLabel,
            experience.facilityId,
            experience.facilityName,
            experience.use,
            experience.regionId,
            experience.regionName,
            experience.codexClue,
            experience.rewardBefore,
            experience.rewardAfter,
            input);
    }
    internal static void ValidateSavedJoin(DungeonResearchSaveData research,
        DungeonPhysicalItemSaveData physical, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (research?.knowledgeTasks == null || research.knowledgeTasks.Any(value => value == null)
                || physical?.pendingBatchDispositions == null || physical.pendingBatchDispositions.Any(value => value == null)
                || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("knowledge-save-shape-invalid");
            var owners = research.knowledgeTasks.ToDictionary(value => value.sinkOperationId, StringComparer.Ordinal);
            var debits = physical.pendingBatchDispositions.ToDictionary(value => value.operationId, StringComparer.Ordinal);
            foreach (var task in owners.Values)
            {
                bool hasDebit = debits.TryGetValue(task.sinkOperationId, out var debit);
                if (task.dispositionPhase == KnowledgeResidueDispositionPhase.AwaitingInput)
                {
                    if (hasDebit || task.completionOwnerRevision != 0 || task.completionPublicationKey != string.Empty
                        || task.completionFact?.IsEmpty != true)
                        throw new InvalidOperationException("knowledge-unapplied-owner-has-debit-or-fact");
                    continue;
                }
                var fact = KnowledgeResidueCompletionTransaction.RestoreFact(task);
                if (!hasDebit || !debit.publicationExpected || debit.defaultObservationDay != 0
                    || debit.publicationKey != task.completionPublicationKey)
                    throw new InvalidOperationException("knowledge-applied-debit-publication-missing");
                var normalized = new KnowledgeCompletionFactData { physical = debit }.Clone().physical;
                normalized.publicationExpected = false;
                normalized.publicationKey = normalized.publicationId = normalized.publicationFingerprint = string.Empty;
                normalized.publicationDelivered = false;
                normalized.defaultObservationDay = fact.Experience.absoluteDay;
                if (!KnowledgeResidueCompletedFact.PhysicalEquals(task.completionFact.physical, normalized))
                    throw new InvalidOperationException("knowledge-frozen-debit-conflict");
                ValidateCanonical(fact, task.completionPublicationKey, debit, incoming);
            }
            foreach (var debit in debits.Values)
                if ((debit.operationId.StartsWith("research-knowledge-residue-sink:", StringComparison.Ordinal)
                        || debit.reasonCode == KnowledgeResidueDestinationAuthority.SinkReasonCode
                        || GameplayDomainFactDeliveryQuery.TryReadKey(debit.publicationKey, out var key)
                            && key.ProducerId == KnowledgeCompletionOutcomeIds.ProducerId)
                    && !owners.ContainsKey(debit.operationId))
                    throw new InvalidOperationException("knowledge-debit-owner-missing");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("knowledge-completion-save-join:" + exception.Message); }
    }

    private static void ValidateCanonical(KnowledgeResidueCompletedFact fact, string publicationKey,
        PhysicalItemBatchDispositionSaveData debit, GameplayOutcomeLedgerSaveData incoming)
    {
        var receipt = Capture(fact);
        if (receipt.ResultKey.ToString() != publicationKey)
            throw new InvalidOperationException("knowledge-observer-key-conflict");
        if (string.IsNullOrEmpty(debit.publicationId) != string.IsNullOrEmpty(debit.publicationFingerprint)
            || debit.publicationDelivered && string.IsNullOrEmpty(debit.publicationId))
            throw new InvalidOperationException("knowledge-publication-proof-incomplete");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
            && value.producerId == receipt.ResultKey.ProducerId
            && value.operationId == receipt.ResultKey.OperationId.Value
            && value.commitRevision == receipt.ResultKey.CommitRevision && value.localResultIndex == 0).ToArray();
        var retired = incoming.tombstones.Where(value => value != null
            && value.producerId == receipt.ResultKey.ProducerId
            && value.operationId == receipt.ResultKey.OperationId.Value
            && value.commitRevision == receipt.ResultKey.CommitRevision && value.localResultIndex == 0).ToArray();
        if (rows.Length + retired.Length > 1)
            throw new InvalidOperationException("knowledge-canonical-duplicate");
        bool exists = rows.Length + retired.Length == 1;
        if (!exists && (!string.IsNullOrEmpty(debit.publicationId)
                || !string.IsNullOrEmpty(debit.publicationFingerprint) || debit.publicationDelivered))
            throw new InvalidOperationException("knowledge-publication-proof-without-ledger");
        // Detached comparison against canonical payloads, never a live commit or gameplay replay.
        var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new KnowledgeCompletionOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new KnowledgeCompletionOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 1, retainedLargePageCount: 1));
        if (exists)
        {
            string run = rows.Length == 1 ? rows[0].runId : retired[0].runId;
            long sequence = rows.Length == 1 ? rows[0].sequence : retired[0].sequence;
            long revision = rows.Length == 1 ? rows[0].ownerRevision : retired[0].ownerRevision;
            string hash = rows.Length == 1 ? rows[0].immutablePayloadHash : retired[0].canonicalHash;
            string id = new GameplayOutcomeId(new GameplayOutcomeRunId(run), sequence).ToString();
            bool delivered = retired.Length == 1 || incoming.exactOutcomes.Contains(rows[0]);
            if (run != incoming.runId || sequence >= incoming.nextSequence || revision != receipt.ResultKey.CommitRevision
                || rows.Length == 1 && rows[0].outcomeTypeId != KnowledgeCompletionOutcomeIds.Completed.Value
                || !string.IsNullOrEmpty(debit.publicationId) && debit.publicationId != id
                || !string.IsNullOrEmpty(debit.publicationFingerprint) && debit.publicationFingerprint != hash
                || debit.publicationDelivered && !delivered)
                throw new InvalidOperationException("knowledge-publication-proof-conflict");
            var comparison = new GameplayOutcomeLedgerSaveData
            {
                runId = run, worldEpoch = incoming.worldEpoch, nextSequence = checked(sequence + 1),
                knownResultKeyHighWater = 1,
                tombstones = new() { new GameplayOutcomeTombstoneSnapshot
                {
                    runId = run, sequence = sequence, producerId = receipt.ResultKey.ProducerId,
                    operationId = receipt.ResultKey.OperationId.Value,
                    commitRevision = receipt.ResultKey.CommitRevision, localResultIndex = 0, ownerRevision = revision,
                    terminalTier = NarrativeMemoryTier.Forgotten, canonicalHash = hash
                } }
            };
            var candidate = ledger.PrepareGameplayOutcomeRestore(comparison);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
        }
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var result = recorder.TryPrepare(receipt, out var token);
        recorder.CancelPrepared(token);
        if (exists ? result.Code != OutcomePrepareCode.AlreadyTerminal : !result.Success)
            throw new InvalidOperationException("knowledge-canonical-payload-conflict:" + result.Code + ":" + result.DetailCode);
    }
}
