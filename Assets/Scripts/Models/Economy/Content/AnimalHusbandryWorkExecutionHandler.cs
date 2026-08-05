using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

public sealed class AnimalHusbandryWorkHandle
{
    public AnimalHusbandryWorkHandle(
        object targetRuntimeObject,
        object workerRuntimeObject)
    {
        TargetRuntimeObject = targetRuntimeObject;
        WorkerRuntimeObject = workerRuntimeObject;
    }

    public object TargetRuntimeObject { get; }
    public object WorkerRuntimeObject { get; }
}

public interface IAnimalHusbandryWorkRuntimePort
{
    bool TryGetWork(
        AnimalHusbandryWorkHandle handle,
        out AnimalHusbandryWorkSnapshot work);

    bool ApplyWork(
        AnimalHusbandryWorkHandle handle,
        WildlifeInstanceId animalId,
        AnimalHusbandryWorkKind kind,
        float amount,
        out bool completed);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalHusbandryWorkExecutionHandler
{
    private static readonly WorkTypeId[] Supported =
    {
        BuiltInWorkTypeIds.AnimalCare
    };

    private readonly IAnimalHusbandryWorkRuntimePort runtime;

    public AnimalHusbandryWorkExecutionHandler(
        IAnimalHusbandryWorkRuntimePort runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Supported;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        AnimalHusbandryWorkHandle handle,
        out string reason)
    {
        AnimalHusbandryWorkSnapshot work = default;
        bool available = workTypeId == BuiltInWorkTypeIds.AnimalCare
            && runtime.TryGetWork(handle, out work)
            && work.Available;
        reason = available ? string.Empty : work.Failure.Code.ToString();
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        AnimalHusbandryWorkHandle handle)
    {
        if (workTypeId != BuiltInWorkTypeIds.AnimalCare
            || !runtime.TryGetWork(
                handle,
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

    public bool TryGetWork(
        AnimalHusbandryWorkHandle handle,
        out AnimalHusbandryWorkSnapshot work)
    {
        return runtime.TryGetWork(handle, out work) && work.Available;
    }

    public bool ApplyProgress(
        AnimalHusbandryWorkHandle handle,
        AnimalHusbandryWorkSnapshot work,
        float delta,
        out bool completed)
    {
        return runtime.ApplyWork(
            handle,
            work.AnimalId,
            work.Kind,
            delta,
            out completed);
    }
}
