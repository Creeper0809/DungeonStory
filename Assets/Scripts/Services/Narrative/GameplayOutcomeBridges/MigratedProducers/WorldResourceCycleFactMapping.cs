using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Receipt layout belongs to this observer, never to the resource owner.
public static class WorldResourceCycleFactMapping
{
    public static MigratedDomainFactCapture Capture(WorldResourceCycleCompletedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var nodeId = fact.NodeId;
        var workTypeId = fact.WorkTypeId;
        var pending = fact.CaptureDomainState();
        CraftContributionSaveData lead = (pending.contributions
                ?? new List<CraftContributionSaveData>())
            .Where(value => value != null && value.contributedWork > 0f)
            .OrderByDescending(value => value.contributedWork)
            .ThenBy(value => value.characterId, StringComparer.Ordinal)
            .FirstOrDefault();
        List<MigratedProducerOutcomeSubject> subjects = new()
        {
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.FacilityKind,
                nodeId.Value,
                nodeId.Value,
                MigratedProducerOutcomeIds.TargetRole)
        };
        if (lead != null)
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.CharacterKind,
                lead.characterId,
                lead.characterId,
                MigratedProducerOutcomeIds.ActorRole));
        }
        int outputQuantity = (pending.lines
                ?? new List<WorldResourceResolvedOutputLineSaveData>())
            .Where(value => value != null)
            .Sum(value => Math.Max(0, value.resolvedQuantity));
        double totalWork = (pending.contributions
                ?? new List<CraftContributionSaveData>())
            .Where(value => value != null)
            .Sum(value => Math.Max(0d, value.contributedWork));
        string contributors = string.Join(",", (pending.contributions
                ?? new List<CraftContributionSaveData>())
            .Where(value => value != null && value.contributedWork > 0f)
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId + "="
                + value.contributedWork.ToString("R", CultureInfo.InvariantCulture)));
        string outputs = string.Join(",", (pending.lines
                ?? new List<WorldResourceResolvedOutputLineSaveData>())
            .Where(value => value != null && value.resolvedQuantity > 0)
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .Select(value => value.outputLineId + "=" + value.itemId + "="
                + value.resolvedQuantity.ToString(CultureInfo.InvariantCulture)));
        MigratedProducerOutcomeDetails details = new(
            subjects.ToArray(),
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.PreviousMetric,
                    pending.sourceBefore,
                    MigratedProducerOutcomeIds.CountUnit,
                    subjects[0].EntityId),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentMetric,
                    pending.sourceAfter,
                    MigratedProducerOutcomeIds.CountUnit,
                    subjects[0].EntityId),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CompletedWorkMetric,
                    Math.Round(totalWork * 1000d),
                    MigratedProducerOutcomeIds.MilliWorkUnit,
                    subjects[0].EntityId),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.OutputQuantityMetric,
                    outputQuantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    subjects[0].EntityId),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.OutputMassMetric,
                    pending.physicalOutputMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    subjects[0].EntityId)
            },
            new[]
            {
                new GameplayOutcomeFact(new GameplayOutcomeFactId("operation-id"),
                    pending.operationId),
                new GameplayOutcomeFact(new GameplayOutcomeFactId("work-type-id"),
                    workTypeId.Value),
                new GameplayOutcomeFact(new GameplayOutcomeFactId("recipe-id"),
                    pending.recipeId),
                new GameplayOutcomeFact(new GameplayOutcomeFactId("source-kind"),
                    pending.renewableSource ? "renewable-patch" : "finite-cycle"),
                new GameplayOutcomeFact(new GameplayOutcomeFactId("source-patch-id"),
                    string.IsNullOrEmpty(pending.sourcePatchId)
                        ? "none"
                        : pending.sourcePatchId),
                new GameplayOutcomeFact(MigratedProducerOutcomeIds.ContributorsFact,
                    contributors.Length == 0 ? "none" : contributors),
                new GameplayOutcomeFact(MigratedProducerOutcomeIds.BatchFact,
                    string.IsNullOrEmpty(pending.outputBatchCommitId)
                        ? "none"
                        : pending.outputBatchCommitId),
                new GameplayOutcomeFact(new GameplayOutcomeFactId("output-vector"),
                    outputs.Length == 0 ? "none" : outputs)
            },
            "자원 작업이 실제 자원 차감과 산출물 생성으로 완료되었다.");
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.WorldResourceCycleResult,
            pending.operationId, pending.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded, details);
    }
}
