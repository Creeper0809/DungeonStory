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

    public GrandProjectWorkExecutionHandler(IGrandProjectRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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
            && work.Available;
        reason = available ? string.Empty : work.UnavailableReason;
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
                    delta,
                    out bool projectCompleted);
                progressApplied &= succeeded;
                completed |= projectCompleted;
                return succeeded;
            });

        result.CompletedSuccessfully = progressApplied && completed;
        result.CompletionEffectsAlreadyApplied = completed;
    }

    private static BuildingInstanceId GetFacilityId(BuildableObject target) =>
        target != null ? target.PersistentInstanceId : default;
}
