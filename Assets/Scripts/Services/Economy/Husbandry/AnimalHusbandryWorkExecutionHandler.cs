using System;
using System.Collections;
using System.Collections.Generic;

public sealed class AnimalHusbandryWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Supported =
    {
        BuiltInWorkTypeIds.AnimalCare
    };

    private readonly IAnimalHusbandryRuntime runtime;

    public AnimalHusbandryWorkExecutionHandler(
        IAnimalHusbandryRuntime runtime)
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
        AnimalHusbandryWorkSnapshot work = default;
        bool available = workTypeId == BuiltInWorkTypeIds.AnimalCare
            && runtime.TryGetWork(target, actor, out work)
            && work.Available;
        reason = available ? string.Empty : work.UnavailableReason;
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        if (workTypeId != BuiltInWorkTypeIds.AnimalCare
            || !runtime.TryGetWork(
                target,
                actor,
                out AnimalHusbandryWorkSnapshot work)
            || !work.Available)
        {
            return 0f;
        }

        return work.Kind switch
        {
            AnimalHusbandryWorkKind.Slaughter => 58f,
            AnimalHusbandryWorkKind.CollectProduct => 46f,
            AnimalHusbandryWorkKind.CollectManure => 38f,
            AnimalHusbandryWorkKind.Tame => 34f,
            _ => 0f
        };
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (!runtime.TryGetWork(
                context.Target,
                context.Actor,
                out AnimalHusbandryWorkSnapshot work)
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
                    context.Target,
                    context.Actor,
                    work.AnimalId,
                    work.Kind,
                    delta,
                    out bool cycleCompleted);
                progressApplied &= succeeded;
                completed |= cycleCompleted;
                return succeeded;
            });
        result.CompletedSuccessfully = progressApplied && completed;
        result.CompletionEffectsAlreadyApplied = completed;
    }
}
