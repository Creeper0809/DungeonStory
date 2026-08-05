using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Work;
using UnityEngine;

public sealed class ResearchWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider
{
    private readonly DefaultResearchWorkRuntimePort runtime;
    private readonly DungeonStory.Work.ResearchWorkExecutionHandler core;

    public ResearchWorkExecutionHandler(IBlueprintResearchWorkService researchWorkService)
    {
        runtime = new DefaultResearchWorkRuntimePort(
            researchWorkService
            ?? throw new ArgumentNullException(nameof(researchWorkService)));
        core = new DungeonStory.Work.ResearchWorkExecutionHandler(runtime);
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => core.WorkTypeIds;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        return target != null && core.IsAvailable(
            workTypeId,
            runtime.CaptureFacility(target),
            out reason);
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        ResearchWorkerHandle worker = runtime.CaptureWorker(context.Actor);
        ResearchFacilityHandle facility = runtime.CaptureFacility(context.Target);
        ResearchWorkPlan plan = core.CreatePlan(facility);
        yield return context.ExecuteWorkAmount(plan.RequiredWork, "연구");
        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        ResearchWorkProgressResult work = core.Apply(worker, facility, 1f);
        result.CompletedSuccessfully = work.Succeeded;
        if (!work.Succeeded)
        {
            context.Actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Research,
                CharacterActivityOutcomes.Failed,
                $"연구 실패: {work.FailureCode}",
                context.Target,
                reasonCode: work.FailureCode,
                bubbleEligible: true));
            yield return new WaitForSeconds(0.2f);
            yield break;
        }

        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Research,
            work.Completed
                ? CharacterActivityOutcomes.Completed
                : CharacterActivityOutcomes.Progress,
            work.Completed
                ? $"연구 완료: {work.Label}"
                : $"연구 진행: {work.Label}",
            context.Target,
            reasonCode: work.Completed ? "blueprint-completed" : "research-progress",
            value: work.ProgressRatio));
    }
}

public sealed class DefaultResearchWorkRuntimePort : IResearchWorkRuntimePort
{
    private readonly IBlueprintResearchWorkService service;

    public DefaultResearchWorkRuntimePort(IBlueprintResearchWorkService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public ResearchWorkerHandle CaptureWorker(object runtimeWorker)
    {
        CharacterActor actor = runtimeWorker as CharacterActor
            ?? throw new InvalidOperationException("Research work requires a CharacterActor.");
        CharacterId id = actor.BuildingCharacterId;
        return new ResearchWorkerHandle(actor, id);
    }

    public ResearchFacilityHandle CaptureFacility(object runtimeFacility)
    {
        BuildableObject facility = runtimeFacility as BuildableObject
            ?? throw new InvalidOperationException("Research work requires a BuildableObject.");
        return new ResearchFacilityHandle(
            facility,
            facility.RequirePersistentInstanceId());
    }

    public bool HasResearchWork(ResearchFacilityHandle facility) =>
        service.HasResearchWorkFor(RequireFacility(facility));

    public ResearchWorkPlan CreatePlan(ResearchFacilityHandle facility)
    {
        BuildableObject target = RequireFacility(facility);
        float requiredWork = target.BuildingData != null
            ? Mathf.Max(
                0.1f,
                target.BuildingData.GetRequiredWork(BuiltInWorkTypeIds.Research))
            : 1f;
        return new ResearchWorkPlan(requiredWork, "연구");
    }

    public ResearchWorkProgressResult Apply(
        ResearchWorkerHandle worker,
        ResearchFacilityHandle facility,
        float seconds)
    {
        BlueprintResearchWorkResult result = service.ApplyResearchWork(
            RequireWorker(worker),
            RequireFacility(facility),
            seconds);
        string label = result.Blueprint != null
            ? result.Blueprint.DisplayName
            : result.Message;
        return new ResearchWorkProgressResult(
            result.Success,
            result.Completed,
            result.ProgressRatio,
            label,
            result.Success ? string.Empty : result.Message);
    }

    private static CharacterActor RequireWorker(ResearchWorkerHandle handle) =>
        handle?.RuntimeObject as CharacterActor
        ?? throw new InvalidOperationException("Research worker handle is invalid.");

    private static BuildableObject RequireFacility(ResearchFacilityHandle handle) =>
        handle?.RuntimeObject as BuildableObject
        ?? throw new InvalidOperationException("Research facility handle is invalid.");
}
