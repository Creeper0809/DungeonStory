using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ResearchWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Research };
    private readonly IBlueprintResearchWorkService researchWorkService;

    public ResearchWorkExecutionHandler(IBlueprintResearchWorkService researchWorkService)
    {
        this.researchWorkService = researchWorkService
            ?? throw new ArgumentNullException(nameof(researchWorkService));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        return target != null && researchWorkService.HasResearchWorkFor(target);
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        float requiredWork = context.Target?.BuildingData != null
            ? Mathf.Max(
                0.1f,
                context.Target.BuildingData.GetRequiredWork(BuiltInWorkTypeIds.Research))
            : 1f;
        yield return context.ExecuteWorkAmount(requiredWork, "연구");
        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        BlueprintResearchWorkResult researchResult = researchWorkService.ApplyResearchWork(
            null,
            context.Target,
            1f);
        result.CompletedSuccessfully = researchResult.Success;
        if (!researchResult.Success)
        {
            context.Actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Research,
                CharacterActivityOutcomes.Failed,
                $"연구 실패: {researchResult.Message}",
                context.Target,
                reasonCode: researchResult.Message,
                bubbleEligible: true));
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        string blueprintName = researchResult.Blueprint != null
            ? researchResult.Blueprint.DisplayName
            : "설계도";
        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Research,
            researchResult.Completed
                ? CharacterActivityOutcomes.Completed
                : CharacterActivityOutcomes.Progress,
            researchResult.Completed
                ? $"연구 완료: {blueprintName}"
                : $"연구 진행: {blueprintName} {Mathf.RoundToInt(researchResult.ProgressRatio * 100f)}%",
            context.Target,
            reasonCode: researchResult.Completed ? "blueprint-completed" : "research-progress",
            value: researchResult.ProgressRatio));
    }
}
