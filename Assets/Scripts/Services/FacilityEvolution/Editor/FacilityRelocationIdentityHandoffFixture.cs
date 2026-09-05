#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Diagnostics;
using Object = UnityEngine.Object;

public static class FacilityRelocationIdentityHandoffFixture
{
    public static void Verify()
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_Warehouse.asset");
        Require(definition != null,
            "Relocation identity fixture definition is missing.");

        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        IBuildingWorldRegistryPort worldPort = (IBuildingWorldRegistryPort)world;
        int initialBuildingCount = world.Buildings.Count;
        int initialWarehouseCount = world.Warehouses.Count;
        Grid grid = new(10, 3);
        GameObject sourceObject = null;
        Facility source = null;
        BuildableObject relocated = null;
        try
        {
            sourceObject = new GameObject("RelocationIdentitySource");
            source = sourceObject.AddComponent<Facility>();
            CharacterAiEditorTestDependencies.Inject(source);
            source.SetGrid(grid);
            source.Initialization(definition, new Vector2Int(3, 1));
            BuildingInstanceId survivorId = source.RequirePersistentInstanceId();
            Require(grid.RegisterOccupant(
                    source,
                    GridLayer.Construction,
                    source.buildPoses,
                    false),
                "Relocation identity fixture could not reserve its packed site.");

            ScenarioObjectResolver resolver = new(new AllowingMutationFence());
            FacilityRelocationWorldService service = new(
                new NullGridTextureProvider(),
                new GridBuildingObjectFactory(),
                resolver);
            Require(service.TryCompleteRelocation(
                        source,
                        out relocated,
                        out string failureReason)
                    && relocated != null,
                $"Relocation identity handoff failed: {failureReason}");
            Require(relocated.PersistentInstanceId.Equals(survivorId)
                    && world.Buildings.Count == initialBuildingCount + 1
                    && world.Buildings.Contains(relocated)
                    && !world.Buildings.Contains(source)
                    && world.Warehouses.Count == initialWarehouseCount + 1
                    && world.Warehouses.Contains(relocated as IWarehouseFacility)
                    && ReferenceEquals(
                        grid.GetGridCell(relocated.centerPos)
                            ?.GetOccupant(definition.Placement.Layer),
                        relocated)
                    && !resolver.IsFrozen(survivorId),
                "Relocation did not preserve identity, projection, or grid custody.");
        }
        finally
        {
            if (source != null)
                worldPort.UnregisterBuilding(source);
            if (relocated != null)
            {
                worldPort.UnregisterBuilding(relocated);
                relocated.DestroySelf();
            }
            if (sourceObject != null)
                Object.DestroyImmediate(sourceObject);
        }

        Require(world.Buildings.Count == initialBuildingCount
                && world.Warehouses.Count == initialWarehouseCount,
            "Relocation identity fixture leaked world registry projections.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class NullGridTextureProvider : IGridTextureProvider
    {
        public GridTexture Texture => null;
    }

    private sealed class ScenarioObjectResolver : IObjectResolver
    {
        private readonly IProductionFacilityMutationFence fence;
        private readonly ProductionFacilityMutationEpochRuntime epochs = new();
        private readonly IProductionFacilityRetargetTransaction retarget;

        internal ScenarioObjectResolver(IProductionFacilityMutationFence fence)
        {
            this.fence = fence ?? throw new ArgumentNullException(nameof(fence));
            retarget = new ProductionFacilityRetargetTransaction(
                new ProductionFacilityRetargetParticipantRegistry(
                    new IProductionFacilityRetargetParticipant[]
                    {
                        new ProductionFacilityEmptyLifecycleRetargetParticipant(
                            new EmptyLifecycle())
                    }),
                epochs);
        }

        internal bool IsFrozen(BuildingInstanceId facilityId) =>
            epochs.IsFrozen(facilityId);

        public object ApplicationOrigin => null;
        public DiagnosticsCollector Diagnostics { get; set; }

        public object Resolve(Type type, object key = null) =>
            throw new InvalidOperationException(
                "Relocation fixture cannot resolve " + type?.Name + ".");

        public bool TryResolve(Type type, out object resolved, object key = null)
        {
            if (type == typeof(IProductionFacilityMutationFence))
            {
                resolved = fence;
                return true;
            }
            if (type == typeof(IProductionFacilityRetargetTransaction))
            {
                resolved = retarget;
                return true;
            }
            resolved = null;
            return false;
        }

        public object Resolve(Registration registration) =>
            throw new InvalidOperationException(
                "Relocation fixture cannot resolve registrations.");

        public IScopedObjectResolver CreateScope(
            Action<IContainerBuilder> installation = null) =>
            throw new InvalidOperationException(
                "Relocation fixture cannot create scopes.");

        public void Inject(object instance)
        {
            if (instance is BuildableObject building)
                CharacterAiEditorTestDependencies.Inject(building);
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

    private sealed class EmptyLifecycle :
        IProductionOutputDestinationLifecycleQuery
    {
        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId facilityId)
        {
            string fingerprint = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint("qa:relocation-empty:" + facilityId.Value);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                Array.Empty<ProductionOutputDestinationLifecycleContribution>(),
                fingerprint);
        }
    }

    private sealed class AllowingMutationFence : IProductionFacilityMutationFence
    {
        public bool TryRequireNoAuthority(
            BuildableObject facility,
            ProductionFacilityMutationKind kind,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
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
}
#endif
