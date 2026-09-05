using System;
using System.Reflection;
using UnityEngine;
using VContainer;
using VContainer.Diagnostics;

public static class FacilityRelocationCompletionFenceFixture
{
    public static bool Run()
    {
        GameObject sourceObject = new("FacilityRelocationCompletionFenceFixture");
        BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            Grid grid = new(8, 8);
            TestBuildableObject source =
                sourceObject.AddComponent<TestBuildableObject>();
            source.InitializePackedCandidate(grid, definition, new Vector2Int(3, 3));

            RejectingMutationFence fence = new();
            FacilityRelocationWorldService service = new(
                new NullGridTextureProvider(),
                new UnusedBuildingObjectFactory(),
                new ScenarioObjectResolver(fence));

            bool completed = service.TryCompleteRelocation(
                source,
                out BuildableObject relocated,
                out string failureReason);

            return !completed
                && relocated == null
                && fence.RequireNoAuthorityCount == 1
                && fence.LastKind == ProductionFacilityMutationKind.Relocation
                && failureReason.Contains(
                    "qa-late-stock-sensor-delivery",
                    StringComparison.Ordinal)
                && !source.isDestroy
                && ReferenceEquals(source.Grid, grid)
                && source.centerPos == new Vector2Int(3, 3)
                && sourceObject != null
                && VerifySaveTopology();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static bool VerifySaveTopology()
    {
        const string facilityId = "building:qa-relocation";
        FacilityRelocationOrder order = new()
        {
            orderId = "facility-relocation:qa",
            facilityPersistentId = facilityId,
            sourceX = 1,
            sourceY = 2,
            destinationX = 5,
            destinationY = 6,
            phase = FacilityRelocationPhase.Dismantling,
            packageConsumed = false
        };
        FacilityEvolutionStateSnapshot snapshot = new()
        {
            hasRecordSnapshot = true,
            instanceEvolution = new FacilityEvolutionState
            {
                facilityPersistentId = facilityId,
                relocationOrder = order
            }
        };
        ModularFacilityBuildingSaveData entry = new()
        {
            persistentInstanceId = facilityId,
            layer = GridLayer.Building,
            hasRuntimeLayer = true,
            runtimeLayer = GridLayer.Building,
            relocationPacked = false,
            centerX = 1,
            centerY = 2
        };

        if (!Valid(entry, snapshot))
            return false;

        order.phase = FacilityRelocationPhase.WaitingForPackage;
        entry.runtimeLayer = GridLayer.Construction;
        entry.relocationPacked = true;
        entry.centerX = 5;
        entry.centerY = 6;
        if (!Valid(entry, snapshot))
            return false;

        order.phase = FacilityRelocationPhase.Reinstalling;
        order.packageConsumed = true;
        if (!Valid(entry, snapshot))
            return false;

        order.phase = FacilityRelocationPhase.Blocked;
        if (!Valid(entry, snapshot))
            return false;
        order.packageConsumed = false;
        entry.runtimeLayer = GridLayer.Building;
        entry.relocationPacked = false;
        entry.centerX = 1;
        entry.centerY = 2;
        if (!Valid(entry, snapshot))
            return false;

        order.orderId = " facility-relocation:qa";
        if (Valid(entry, snapshot))
            return false;
        order.orderId = "facility-relocation:qa";
        order.facilityPersistentId = "building:wrong";
        if (Valid(entry, snapshot))
            return false;
        order.facilityPersistentId = facilityId;
        entry.relocationPacked = true;
        if (Valid(entry, snapshot))
            return false;
        entry.relocationPacked = false;
        entry.centerX = 2;
        if (Valid(entry, snapshot))
            return false;
        entry.centerX = 1;
        order.destinationX = order.sourceX;
        order.destinationY = order.sourceY;
        return !Valid(entry, snapshot);
    }

    private static bool Valid(
        ModularFacilityBuildingSaveData entry,
        FacilityEvolutionStateSnapshot snapshot) =>
        ModularFacilityRelocationTopologyValidator.TryValidate(
            entry,
            snapshot,
            out _);

    private sealed class TestBuildableObject : BuildableObject
    {
        private static readonly PropertyInfo BuildingDataProperty =
            typeof(BuildableObject).GetProperty(
                nameof(BuildableObject.BuildingData),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "BuildableObject.BuildingData property is unavailable.");

        internal void InitializePackedCandidate(
            Grid candidateGrid,
            BuildingSO definition,
            Vector2Int position)
        {
            SetGrid(candidateGrid);
            BuildingDataProperty.SetValue(this, definition);
            centerPos = position;
        }
    }

    private sealed class RejectingMutationFence :
        IProductionFacilityMutationFence
    {
        internal int RequireNoAuthorityCount { get; private set; }
        internal ProductionFacilityMutationKind LastKind { get; private set; }

        public bool TryRequireNoAuthority(
            BuildableObject facility,
            ProductionFacilityMutationKind kind,
            out string failureReason)
        {
            RequireNoAuthorityCount++;
            LastKind = kind;
            failureReason = "qa-late-stock-sensor-delivery";
            return false;
        }

        public bool TryPrepareEmpty(
            BuildableObject facility,
            ProductionFacilityMutationKind kind,
            string operationId,
            out ProductionFacilityEmptyMutationCandidate candidate,
            out string failureReason)
        {
            candidate = null;
            failureReason = "unused";
            return false;
        }

        public bool TryCommitAuthorityRevoke(
            ProductionFacilityEmptyMutationCandidate candidate,
            out string failureReason)
        {
            failureReason = "unused";
            return false;
        }

        public bool TryAbort(
            ProductionFacilityEmptyMutationCandidate candidate,
            out string failureReason)
        {
            failureReason = "unused";
            return false;
        }

        public bool TryComplete(
            ProductionFacilityEmptyMutationCandidate candidate,
            out string failureReason)
        {
            failureReason = "unused";
            return false;
        }
    }

    private sealed class NullGridTextureProvider : IGridTextureProvider
    {
        public GridTexture Texture => null;
    }

    private sealed class UnusedBuildingObjectFactory :
        IGridBuildingObjectFactory
    {
        public BuildableObject Create(
            Grid grid,
            BuildingSO buildingData,
            Vector2Int selectPos) => throw new InvalidOperationException(
                "Relocation completion mutated the world before its final fence.");

        public BuildableObject CreateDetached(
            Grid grid,
            BuildingSO buildingData,
            Vector2Int selectPos) => throw new InvalidOperationException(
                "Relocation completion mutated the world before its final fence.");
    }

    private sealed class ScenarioObjectResolver : IObjectResolver
    {
        private readonly IProductionFacilityMutationFence fence;

        internal ScenarioObjectResolver(IProductionFacilityMutationFence fence) =>
            this.fence = fence ?? throw new ArgumentNullException(nameof(fence));

        public object ApplicationOrigin => null;
        public DiagnosticsCollector Diagnostics { get; set; }

        public object Resolve(Type type, object key = null) =>
            throw new InvalidOperationException(
                "Fixture resolver cannot resolve " + type?.Name + ".");

        public bool TryResolve(Type type, out object resolved, object key = null)
        {
            if (type == typeof(IProductionFacilityMutationFence))
            {
                resolved = fence;
                return true;
            }
            resolved = null;
            return false;
        }

        public object Resolve(Registration registration) =>
            throw new InvalidOperationException(
                "Fixture resolver cannot resolve registrations.");

        public IScopedObjectResolver CreateScope(
            Action<IContainerBuilder> installation = null) =>
            throw new InvalidOperationException(
                "Fixture resolver cannot create scopes.");

        public void Inject(object instance)
        {
        }

        public bool TryGetRegistration(
            Type type,
            out Registration registration,
            object key = null)
        {
            registration = null;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
