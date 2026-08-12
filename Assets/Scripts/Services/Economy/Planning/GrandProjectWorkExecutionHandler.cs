using System;
using System.Collections;
using System.Collections.Generic;

public sealed class GrandProjectWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Supported =
    {
        BuiltInWorkTypeIds.GrandProject
    };

    private readonly IGrandProjectRuntime runtime;
    private readonly IProjectWorkforceRuntime projectWorkforce;

    public GrandProjectWorkExecutionHandler(
        IGrandProjectRuntime runtime,
        IProjectWorkforceRuntime projectWorkforce)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.projectWorkforce = projectWorkforce
            ?? throw new ArgumentNullException(nameof(projectWorkforce));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Supported;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        GrandProjectWorkSnapshot work = default;
        bool available = workTypeId == BuiltInWorkTypeIds.GrandProject
            && runtime.TryGetWork(GetFacilityId(target), out work)
            && work.Available
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            && projectWorkforce.CanJoin(
                work.ProjectId,
                characterId.Value,
                SettlementLaborBalanceRules.GetMaximumWorkers(ProjectScale.GrandProject));
        reason = available
            ? string.Empty
            : !work.Available
                ? work.UnavailableReason
                : "대형 사업의 동시 작업자 슬롯이 가득 찼습니다.";
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return workTypeId == BuiltInWorkTypeIds.GrandProject
            && runtime.TryGetWork(
                GetFacilityId(target),
                out GrandProjectWorkSnapshot work)
            && work.Available
                ? 52f
                : 0f;
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (!runtime.TryGetWork(
                GetFacilityId(context.Target),
                out GrandProjectWorkSnapshot work)
            || !work.Available)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        if (!CharacterPersistentIdentity.TryGet(
                context.Actor,
                out CharacterId characterId)
            || !projectWorkforce.TryJoin(
                work.ProjectId,
                characterId.Value,
                ProjectScale.GrandProject,
                SettlementLaborBalanceRules.GetMaximumWorkers(ProjectScale.GrandProject),
                out ProjectWorkerLease workforceLease,
                out _))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        using (workforceLease)
        {
        bool progressApplied = true;
        bool completed = false;
        yield return context.ExecutePersistentWorkAmount(
            work.RequiredWork,
            work.CompletedWork,
            work.DisplayName,
            delta =>
            {
                bool succeeded = runtime.ApplyWork(
                    GetFacilityId(context.Target),
                    delta * projectWorkforce.GetContributionMultiplier(
                        work.ProjectId,
                        characterId.Value),
                    out bool projectCompleted);
                progressApplied &= succeeded;
                completed |= projectCompleted;
                return succeeded;
            });

        result.CompletedSuccessfully = progressApplied && completed;
        result.CompletionEffectsAlreadyApplied = completed;
        }
    }

    private static BuildingInstanceId GetFacilityId(BuildableObject target) =>
        target != null ? target.PersistentInstanceId : default;
}
