#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Internal;
using VContainer.Unity;

internal static class RuntimeTickAllocationProbe
{
    private readonly struct AllocationSample
    {
        public AllocationSample(string typeName, long bytes)
        {
            TypeName = typeName;
            Bytes = bytes;
        }

        public string TypeName { get; }
        public long Bytes { get; }
    }

    [MenuItem("Tools/DungeonStory/Performance/Measure Runtime Tick Allocations")]
    public static void MeasureAndLog()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Runtime tick allocation probe requires Play Mode.");
            return;
        }

        DungeonRuntimeLifetimeScope scope =
            LifetimeScope.Find<DungeonRuntimeLifetimeScope>() as DungeonRuntimeLifetimeScope;
        if (scope?.Container == null)
        {
            Debug.LogError("Runtime tick allocation probe could not find the gameplay container.");
            return;
        }

        List<AllocationSample> samples = new List<AllocationSample>();
        IReadOnlyList<ITickable> tickables = scope.Container
            .Resolve<ContainerLocal<IReadOnlyList<ITickable>>>()
            .Value;
        for (int index = 0; index < tickables.Count; index++)
        {
            ITickable tickable = tickables[index];
            if (tickable == null)
            {
                continue;
            }

            Type tickableType = tickable.GetType();
            try
            {
                tickable.Tick();
                long before = GC.GetAllocatedBytesForCurrentThread();
                tickable.Tick();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (allocated > 0)
                {
                    samples.Add(new AllocationSample(tickableType.FullName, allocated));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Tick allocation probe skipped {tickableType.FullName}: "
                    + exception.GetBaseException().Message);
            }
        }

        string report = samples.Count == 0
            ? "No managed allocations were measured in resolved ITickable entry points."
            : string.Join(
                "\n",
                samples
                    .OrderByDescending(sample => sample.Bytes)
                    .Select(sample => $"{sample.TypeName}: {sample.Bytes:N0} B"));
        Debug.Log($"Runtime tick allocation probe\n{report}");
    }
}
#endif
