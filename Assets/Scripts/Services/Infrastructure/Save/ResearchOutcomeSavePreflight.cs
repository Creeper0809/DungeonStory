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
        ValidateCompletedProjectResultKeys(research, ledger, report);
        ValidateSpaceExpansionPending(research,
            DungeonSaveSectionPayload.ReadOrNew<ModularFacilityWorldSaveData>(
                saveData, ModularFacilityWorldSaveSection.Id), ledger, report);
        ValidateKnowledgeCompletionJoin(
            research,
            DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                saveData,
                PhysicalItemsSaveSection.Id),
            ledger,
            report);
    }

    internal static void ValidateSpaceExpansionPending(DungeonResearchSaveData research,
        ModularFacilityWorldSaveData world, GameplayOutcomeLedgerSaveData ledger, DungeonGameRestoreReport report)
    {
        try
        {
            DungeonSpaceExpandedFact.ValidateSavedPublication(world);
            var pending = world.spaceExpansionPublication;
            if (!pending.active) return;
            if (pending.trigger == DungeonSpaceExpansionTrigger.ResearchCompletion
                && (research.completedProjectIds?.Contains(pending.projectId) != true
                    || research.completedProjectResults?.Count(value => value != null
                        && value.projectId == pending.projectId
                        && value.publicationKey == pending.parentResearchPublicationKey) != 1))
                throw new InvalidOperationException("space-pending-completed-parent-mismatch");
            DungeonSpaceExpansionFactObservation.ValidatePending(pending, ledger);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("space-pending-publication-invalid:" + exception.Message); }
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

        if (research.pendingWorkPublication?.active == true)
        {
            try
            {
                ResearchWorkPendingPublication pending = research.pendingWorkPublication;
                ResearchWorkAppliedFact fact = new(pending.fact);
                if (fact.Revision != research.outcomeSequence)
                    throw new InvalidOperationException("research-pending-sequence-mismatch");
                ValidatePendingOwner(research, fact);
                ResearchWorkFactObservation.ValidatePending(fact, pending.publicationKey, ledger);
                if (!revisions.Contains(fact.Revision))
                {
                    revisions.Add(fact.Revision);
                    maximum = Math.Max(maximum, fact.Revision);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
            { report.AddError("research-pending-publication-invalid:" + exception.Message); }
        }

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

    internal static void ValidateCompletedProjectResultKeys(
        DungeonResearchSaveData research,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonGameRestoreReport report)
    {
        var projects = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonResearchCompletionResultSaveData saved in
                 research.completedProjectResults
                     ?? new List<DungeonResearchCompletionResultSaveData>())
        {
            if (saved == null
                || !projects.Add(saved.projectId) || !keys.Add(saved.publicationKey)
                || research.completedProjectIds?.Contains(saved.projectId) != true
                || !ResearchWorkFactObservation.TryReadKey(saved.publicationKey, out GameplayResultKey key)
                || key.CommitRevision > research.outcomeSequence)
            {
                report.AddError("research-completion-parent-key-invalid");
                continue;
            }

            bool foundExact = (ledger.outbox
                    ?? new List<GameplayOutcomeSnapshot>())
                .Concat(ledger.exactOutcomes
                    ?? new List<GameplayOutcomeSnapshot>())
                .Any(outcome => outcome != null
                    && string.Equals(outcome.producerId,
                        key.ProducerId, StringComparison.Ordinal)
                    && string.Equals(outcome.operationId,
                        key.OperationId.Value, StringComparison.Ordinal)
                    && outcome.commitRevision == key.CommitRevision
                    && outcome.localResultIndex == 0
                    && string.Equals(outcome.outcomeTypeId,
                        ResearchWorkOutcomeIds.WorkApplied.Value,
                        StringComparison.Ordinal)
                    && outcome.participants?.Any(participant => participant != null
                        && string.Equals(participant.roleId,
                            ResearchWorkOutcomeIds.ProjectRole.Value,
                            StringComparison.Ordinal)
                        && string.Equals(participant.entityId,
                            saved.projectId, StringComparison.Ordinal)) == true
                    && outcome.facts?.Any(fact => fact != null
                        && string.Equals(fact.factId,
                            ResearchWorkOutcomeIds.CompletedFact.Value,
                            StringComparison.Ordinal)
                        && string.Equals(fact.value, "true",
                            StringComparison.Ordinal)) == true);
            bool retired = (ledger.tombstones
                    ?? new List<GameplayOutcomeTombstoneSnapshot>())
                .Any(value => value != null
                    && string.Equals(value.producerId,
                        key.ProducerId, StringComparison.Ordinal)
                    && string.Equals(value.operationId,
                        key.OperationId.Value, StringComparison.Ordinal)
                    && value.commitRevision == key.CommitRevision
                    && value.localResultIndex == 0);
            var pending = research.pendingWorkPublication;
            bool pendingMatch = pending?.active == true && pending.publicationKey == saved.publicationKey
                && pending.fact?.isProject == true && pending.fact.completed
                && pending.fact.subjectId == saved.projectId && pending.fact.revision == key.CommitRevision;
            if (!foundExact && !retired && !pendingMatch)
                report.AddError("research-completion-parent-ledger-missing:"
                    + saved.projectId);
        }
        if ((research.completedProjectIds ?? new()).Any(value => !projects.Contains(value)))
            report.AddError("research-completion-parent-reference-missing");
    }

    private static void ValidatePendingOwner(DungeonResearchSaveData research, ResearchWorkAppliedFact fact)
    {
        if (fact.Cause == ResearchWorkCause.KnowledgeProgress)
        {
            var task = research.knowledgeTasks?.SingleOrDefault(value => value?.taskId == fact.SubjectId);
            if (task == null || task.completedWork != fact.After || task.requiredWork != fact.Required
                || task.dispositionPhase != KnowledgeResidueDispositionPhase.AwaitingInput
                || task.facilityInstanceId != fact.FacilityId)
                throw new InvalidOperationException("research-pending-knowledge-owner-mismatch");
        }
        else if (fact.IsProject)
        {
            var progress = research.projectProgress?.SingleOrDefault(value => value?.projectId == fact.SubjectId);
            if (progress == null || progress.progress != fact.After || progress.requiredWorkAtCapture != fact.Required
                || (research.completedProjectIds?.Contains(fact.SubjectId) == true) != fact.Completed
                || fact.Completed && research.completedProjectResults?.Any(value => value?.projectId == fact.SubjectId
                    && value.publicationKey == research.pendingWorkPublication.publicationKey) != true)
                throw new InvalidOperationException("research-pending-project-owner-mismatch");
        }
        else
        {
            var task = research.tasks?.SingleOrDefault(value => value?.blueprintId == fact.BlueprintId);
            if ((research.completedBlueprintIds?.Contains(fact.BlueprintId) == true) != fact.Completed
                || !fact.Completed && (task == null || task.progress != fact.After)
                || task != null && task.progress != fact.After)
                throw new InvalidOperationException("research-pending-blueprint-owner-mismatch");
        }
        foreach (var unlock in fact.Unlocks)
        {
            if (unlock.UnlockTypeId == BlueprintUnlockTypeIds.Building
                && (!int.TryParse(unlock.ValueId, NumberStyles.None, CultureInfo.InvariantCulture, out int building)
                    || research.unlockedBuildingIds?.Contains(building) != true)
                || unlock.UnlockTypeId == BlueprintUnlockTypeIds.Recipe
                    && research.unlockedRecipeIds?.Contains(unlock.ValueId) != true)
                throw new InvalidOperationException("research-pending-unlock-owner-mismatch");
        }
    }

    internal static void ValidateKnowledgeCompletionJoin(
        DungeonResearchSaveData research, DungeonPhysicalItemSaveData physical,
        GameplayOutcomeLedgerSaveData ledger, DungeonGameRestoreReport report) =>
        KnowledgeCompletionFactObservation.ValidateSavedJoin(research, physical, ledger, report);
}
