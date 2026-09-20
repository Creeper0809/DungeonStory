using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Cold capture/restore join for the research producer's durable sequence.
/// Retired payloads still have replay tombstones, so compaction cannot erase a
/// committed command from this check. This is not a physical wear/state digest.
/// </summary>
public sealed class ResearchOutcomeSavePreflight :
    IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData saveData, DungeonGameRestoreReport report)
    {
        if (saveData == null) throw new ArgumentNullException(nameof(saveData));
        if (report == null) throw new ArgumentNullException(nameof(report));
        DungeonResearchSaveData research =
            DungeonSaveSectionPayload.ReadOrNew<DungeonResearchSaveData>(
                saveData,
                BlueprintResearchSaveSection.Id);
        GameplayOutcomeLedgerSaveData ledger =
            DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(
                saveData,
                GameplayOutcomeLedgerSaveSection.Id);
        ValidateSequence(research, ledger, report);
        ValidateKnowledgeCompletionJoin(
            research,
            DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                saveData,
                PhysicalItemsSaveSection.Id),
            ledger,
            report);
    }

    internal static void ValidateSequence(DungeonResearchSaveData research,
        GameplayOutcomeLedgerSaveData ledger, DungeonGameRestoreReport report)
    {
        if (research == null || ledger == null || report == null)
            throw new ArgumentNullException("Research/outcome save join requires detached payloads and report.");
        if (research.outcomeSequence < 0)
        {
            report.AddError("research-outcome-sequence-negative");
            return;
        }
        HashSet<long> revisions = new();
        long maximum = 0;
        AddOutcomes(ledger.outbox);
        AddOutcomes(ledger.exactOutcomes);
        if (ledger.tombstones != null)
            foreach (var retired in ledger.tombstones)
                if (retired != null && retired.producerId == ResearchWorkOutcomeIds.ProducerId)
                    Add(retired.operationId, retired.commitRevision, retired.localResultIndex, retired.ownerRevision);

        // Positive, unique revisions bounded by maximum are contiguous iff their
        // count equals maximum. No allocation/iteration from an untrusted sequence.
        if (maximum != research.outcomeSequence || revisions.Count != research.outcomeSequence)
            report.AddError("research-outcome-sequence-join-mismatch: research="
                + research.outcomeSequence.ToString(CultureInfo.InvariantCulture)
                + ", ledgerMax=" + maximum.ToString(CultureInfo.InvariantCulture)
                + ", ledgerCount=" + revisions.Count.ToString(CultureInfo.InvariantCulture));

        void AddOutcomes(List<GameplayOutcomeSnapshot> source)
        {
            if (source == null) return;
            foreach (var outcome in source)
            {
                if (outcome == null || outcome.producerId != ResearchWorkOutcomeIds.ProducerId) continue;
                if (outcome.outcomeTypeId != ResearchWorkOutcomeIds.WorkApplied.Value)
                    report.AddError("research-outcome-type-join-mismatch");
                Add(outcome.operationId, outcome.commitRevision, outcome.localResultIndex, outcome.ownerRevision);
            }
        }

        void Add(string operation, long revision, int localIndex, long ownerRevision)
        {
            if (revision <= 0 || ownerRevision != revision || localIndex != 0
                || operation != "research-work:" + revision.ToString(CultureInfo.InvariantCulture))
            {
                report.AddError("research-outcome-result-key-join-invalid");
                return;
            }
            if (!revisions.Add(revision)) report.AddError("research-outcome-result-key-join-duplicate");
            maximum = Math.Max(maximum, revision);
        }
    }

    internal static void ValidateKnowledgeCompletionJoin(
        DungeonResearchSaveData research,
        DungeonPhysicalItemSaveData physical,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonGameRestoreReport report)
    {
        if (research == null || physical == null || ledger == null
            || report == null)
            throw new ArgumentNullException(
                "Knowledge completion save join requires detached payloads and report.");

        Dictionary<string, PhysicalItemBatchDispositionSaveData> pending =
            (physical.pendingBatchDispositions
                    ?? new List<PhysicalItemBatchDispositionSaveData>())
                .Where(value => value != null)
                .GroupBy(value => value.operationId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);
        Dictionary<string, KnowledgeResidueTaskSaveData> owners =
            (research.knowledgeTasks
                    ?? new List<KnowledgeResidueTaskSaveData>())
                .Where(value => value != null)
                .GroupBy(value => value.sinkOperationId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);

        foreach (KnowledgeResidueTaskSaveData task in owners.Values)
        {
            bool hasPending = pending.TryGetValue(
                task.sinkOperationId,
                out PhysicalItemBatchDispositionSaveData input);
            if (!hasPending)
            {
                if (task.dispositionPhase !=
                    KnowledgeResidueDispositionPhase.AwaitingInput)
                    report.AddError(
                        "knowledge-completion-physical-owner-missing:"
                        + task.taskId);
                continue;
            }

            bool typedCompletion = IsTypedCompletion(input, task);
            if (task.completionOwnerRevision > 0)
            {
                if (task.dispositionPhase !=
                        KnowledgeResidueDispositionPhase.OutcomePublished
                    || !typedCompletion
                    || input.outcomeOwnerRevision
                        != task.completionOwnerRevision
                    || !HasCompletionOutcome(
                        ledger,
                        task.sinkOperationId,
                        task.completionOwnerRevision))
                    report.AddError(
                        "knowledge-completion-durable-join-mismatch:"
                        + task.taskId);
            }
            else if (typedCompletion
                && !HasCompletionOutcome(
                    ledger,
                    task.sinkOperationId,
                    input.outcomeOwnerRevision))
            {
                report.AddError(
                    "knowledge-completion-torn-owner-ledger-missing:"
                    + task.taskId);
            }
        }

        foreach (PhysicalItemBatchDispositionSaveData input in pending.Values)
        {
            if (input.gameplayOutcomeExpected
                && string.Equals(
                    input.expectedOutcomeProducerId,
                    KnowledgeCompletionOutcomeIds.ProducerId,
                    StringComparison.Ordinal)
                && !owners.ContainsKey(input.operationId))
                report.AddError(
                    "knowledge-completion-research-owner-missing:"
                    + input.operationId);
        }
    }

    private static bool IsTypedCompletion(
        PhysicalItemBatchDispositionSaveData input,
        KnowledgeResidueTaskSaveData task) =>
        input != null
        && task != null
        && input.gameplayOutcomeExpected
        && string.Equals(
            input.expectedOutcomeProducerId,
            KnowledgeCompletionOutcomeIds.ProducerId,
            StringComparison.Ordinal)
        && string.Equals(
            input.expectedOutcomeOperationId,
            task.sinkOperationId,
            StringComparison.Ordinal)
        && input.expectedOutcomeCommitRevision > 0
        && input.expectedOutcomeCommitRevision == input.outcomeOwnerRevision
        && input.expectedOutcomeLocalResultIndex == 0
        && input.gameplayOutcome != null
        && string.Equals(
            input.gameplayOutcome.producerId,
            input.expectedOutcomeProducerId,
            StringComparison.Ordinal)
        && string.Equals(
            input.gameplayOutcome.operationId,
            input.expectedOutcomeOperationId,
            StringComparison.Ordinal)
        && input.gameplayOutcome.commitRevision
            == input.expectedOutcomeCommitRevision
        && input.gameplayOutcome.localResultIndex
            == input.expectedOutcomeLocalResultIndex;

    private static bool HasCompletionOutcome(
        GameplayOutcomeLedgerSaveData ledger,
        string operationId,
        long revision) =>
        (ledger.outbox ?? new List<GameplayOutcomeSnapshot>())
            .Concat(ledger.exactOutcomes
                ?? new List<GameplayOutcomeSnapshot>())
            .Any(value => value != null
                && string.Equals(
                    value.producerId,
                    KnowledgeCompletionOutcomeIds.ProducerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.operationId,
                    operationId,
                    StringComparison.Ordinal)
                && value.commitRevision == revision
                && value.ownerRevision == revision
                && value.localResultIndex == 0
                && string.Equals(
                    value.outcomeTypeId,
                    KnowledgeCompletionOutcomeIds.Completed.Value,
                    StringComparison.Ordinal));
}
