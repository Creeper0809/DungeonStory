#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;

public static class FluidNetworkBatchDebugContract
{
    public static void Verify()
    {
        BuildingSO storageData = CreateNodeData(99801, "시험 배관 저장조", true);
        BuildingSO firstData = CreateNodeData(99802, "시험 배관 소비자 A", false);
        BuildingSO secondData = CreateNodeData(99803, "시험 배관 소비자 B", false);
        GameObject storageObject = new("Fluid Batch Storage");
        GameObject firstObject = new("Fluid Batch Consumer A");
        GameObject secondObject = new("Fluid Batch Consumer B");
        try
        {
            BuildableObject storage = CreateNode(
                storageObject, storageData, "building:test-fluid-batch-storage",
                new Vector2Int(0, 0));
            BuildableObject first = CreateNode(
                firstObject, firstData, "building:test-fluid-batch-a",
                new Vector2Int(1, 0));
            BuildableObject second = CreateNode(
                secondObject, secondData, "building:test-fluid-batch-b",
                new Vector2Int(2, 0));
            var runtime = new FluidNetworkRuntime(
                new IndustrialInfrastructureTopologyRuntime(
                    new BuildingWorldQuery(storage, first, second)),
                CreateNullProxy<IPowerInfrastructureQuery>(),
                CreateNullProxy<IWorldItemStackRuntime>(),
                CreateNullProxy<IPhysicalItemBatchDispositionService>(),
                CreateNullProxy<IWorldFilthQuery>(),
                CreateNullProxy<IGameClock>(),
                CreateNullProxy<IFacilityCapabilityQuery>(),
                CreateNullProxy<IBuildingFacilityStateChangePort>(),
                new DungeonRuntimeAggregateRootStore(),
                new EditorFluidFacilityInputOwnerAuthority());

            Require(runtime.TryAdd(
                    storage, WorldWaterQuality.Clean, 5f, out float seeded)
                    && Mathf.Approximately(seeded, 5f),
                "fluid batch fixture could not seed its shared network");
            float rejectedWater = GetNetwork(runtime, UtilityChannel.CleanWater)
                .CleanWater;
            int rejectedVersion = runtime.Version;
            bool rejected = runtime.TryCommitBatch(
                CreateDemands(first, second),
                out DomainFailure rejectedFailure);
            Require(!rejected
                    && rejectedFailure.Code == FailureCode.FluidInsufficientWater
                    && runtime.Version == rejectedVersion
                    && Mathf.Approximately(
                        GetNetwork(runtime, UtilityChannel.CleanWater).CleanWater,
                        rejectedWater)
                    && Mathf.Approximately(
                        GetNetwork(runtime, UtilityChannel.Wastewater).Wastewater,
                        0f),
                "shared fluid batch partially mutated before rejection");

            Require(runtime.TryAdd(
                    storage, WorldWaterQuality.Clean, 1f, out float additional)
                    && Mathf.Approximately(additional, 1f),
                "fluid batch fixture could not add its final water unit");
            int commitVersion = runtime.Version;
            bool committed = runtime.TryCommitBatch(
                CreateDemands(second, first),
                out DomainFailure commitFailure);
            Require(committed
                    && !commitFailure.IsFailure
                    && runtime.Version == commitVersion + 1
                    && Mathf.Approximately(
                        GetNetwork(runtime, UtilityChannel.CleanWater).CleanWater,
                        0f)
                    && Mathf.Approximately(
                        GetNetwork(runtime, UtilityChannel.Wastewater).Wastewater,
                        2f),
                "shared fluid batch did not commit exactly once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(storageObject);
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
            UnityEngine.Object.DestroyImmediate(storageData);
            UnityEngine.Object.DestroyImmediate(firstData);
            UnityEngine.Object.DestroyImmediate(secondData);
        }
    }

    private static FluidNetworkBatchDemand[] CreateDemands(
        BuildableObject first,
        BuildableObject second) =>
        new[]
        {
            new FluidNetworkBatchDemand(
                first, WorldWaterQuality.Clean, 3f, 1f),
            new FluidNetworkBatchDemand(
                second, WorldWaterQuality.Clean, 3f, 1f)
        };

    private static FluidNetworkSnapshot GetNetwork(
        FluidNetworkRuntime runtime,
        UtilityChannel channel) =>
        runtime.Networks.Single(network => network.Channel == channel);

    private static BuildingSO CreateNodeData(
        int id,
        string displayName,
        bool includeStorage)
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.id = id;
        data.objectName = displayName;
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingUtilityConnectionAbility
        {
            channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater
        });
        if (includeStorage)
        {
            abilities.Add(new BuildingWaterStorageAbility
            {
                channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater,
                cleanWaterCapacity = 10f,
                wastewaterCapacity = 10f
            });
        }
        data.ReplaceAbilities(abilities);
        return data;
    }

    private static BuildableObject CreateNode(
        GameObject host,
        BuildingSO data,
        string persistentId,
        Vector2Int position)
    {
        BuildableObject building = host.AddComponent<BuildableObject>();
        building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        typeof(BuildableObject).GetProperty(nameof(BuildableObject.BuildingData))
            ?.SetValue(building, data);
        typeof(BuildableObject).GetProperty(nameof(BuildableObject.centerPos))
            ?.SetValue(building, position);
        building.RestorePersistentIdentity(new BuildingInstanceId(persistentId));
        return building;
    }

    private static T CreateNullProxy<T>() where T : class =>
        DispatchProxy.Create<T, NullDispatchProxy>();

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class BuildingWorldQuery : IBuildingWorldQuery
    {
        public BuildingWorldQuery(params BuildableObject[] buildings)
        {
            Buildings = buildings ?? Array.Empty<BuildableObject>();
        }

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    public class NullDispatchProxy : DispatchProxy
    {
        public NullDispatchProxy()
        {
        }

        protected override object Invoke(MethodInfo method, object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (!parameterType.IsByRef)
                {
                    continue;
                }
                Type elementType = parameterType.GetElementType();
                arguments[i] = elementType.IsValueType
                    ? Activator.CreateInstance(elementType)
                    : null;
            }
            Type returnType = method.ReturnType;
            return returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }
    }
}
#endif
