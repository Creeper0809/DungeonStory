using System;
using System.Collections;
using System.Collections.Generic;

public sealed class AnimalHusbandryWorkRuntimePortAdapter :
    IAnimalHusbandryWorkRuntimePort
{
    private readonly IAnimalHusbandryQuery query;
    private readonly IAnimalHusbandryCommand commands;

    public AnimalHusbandryWorkRuntimePortAdapter(
        IAnimalHusbandryQuery query,
        IAnimalHusbandryCommand commands)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
    }

    public bool TryGetWork(
        AnimalHusbandryWorkHandle handle,
        out AnimalHusbandryWorkSnapshot work)
    {
        work = default;
        return TryUnwrap(handle, out BuildableObject target, out CharacterActor actor)
            && query.TryGetWork(target, actor, out work);
    }

    public bool ApplyWork(
        AnimalHusbandryWorkHandle handle,
        WildlifeInstanceId animalId,
        AnimalHusbandryWorkKind kind,
        float amount,
        out bool completed)
    {
        completed = false;
        return TryUnwrap(handle, out BuildableObject target, out CharacterActor actor)
            && commands.ApplyWork(
                target,
                actor,
                animalId,
                kind,
                amount,
                out completed);
    }

    private static bool TryUnwrap(
        AnimalHusbandryWorkHandle handle,
        out BuildableObject target,
        out CharacterActor actor)
    {
        target = handle?.TargetRuntimeObject as BuildableObject;
        actor = handle?.WorkerRuntimeObject as CharacterActor;
        return target != null;
    }
}

public sealed class AnimalHusbandryWorkExecutionAdapter :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private readonly AnimalHusbandryWorkExecutionHandler handler;

    public AnimalHusbandryWorkExecutionAdapter(
        AnimalHusbandryWorkExecutionHandler handler)
    {
        this.handler = handler
            ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds =>
        handler.WorkTypeIds;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        return handler.IsAvailable(
            workTypeId,
            Capture(target, actor),
            out reason);
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return handler.GetUrgency(
            workTypeId,
            Capture(target, actor));
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        AnimalHusbandryWorkHandle handle = Capture(
            context.Target,
            context.Actor);
        if (!handler.TryGetWork(
                handle,
                out AnimalHusbandryWorkSnapshot work))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        bool progressApplied = true;
        bool completed = false;
        yield return context.ExecutePersistentWorkAmount(
            work.RequiredWork,
            work.CompletedWork,
            work.Kind.ToString(),
            delta =>
            {
                bool succeeded = handler.ApplyProgress(
                    handle,
                    work,
                    delta,
                    out bool cycleCompleted);
                progressApplied &= succeeded;
                completed |= cycleCompleted;
                return succeeded;
            });
        result.CompletedSuccessfully = progressApplied && completed;
        result.CompletionEffectsAlreadyApplied = completed;
    }

    private static AnimalHusbandryWorkHandle Capture(
        BuildableObject target,
        CharacterActor actor)
    {
        return new AnimalHusbandryWorkHandle(target, actor);
    }
}
