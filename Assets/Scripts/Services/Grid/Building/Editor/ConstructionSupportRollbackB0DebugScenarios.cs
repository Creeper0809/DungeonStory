#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ConstructionSupportRollbackB0DebugScenarios
{
    public static void RunOrThrow()
    {
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO target = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/R01_간이침대.asset");
        Require(hallway != null && target != null
                && target.GetGridPosList(new Vector2Int(3, 0)).Count >= 2,
            "B0 BDC-B01 fixture assets are missing or no longer multi-cell.");

        VerifyOrderRejectionPreservesOnlyPreexistingSupport(hallway, target);
        VerifySiteRejectionPreservesOnlyPreexistingSupport(hallway, target);
        VerifyPartialSupportFailureRollsBackEarlierSupport(hallway, target);
        VerifySuccessKeepsSupportAndConstructionSite(hallway, target);
        Debug.Log("B0 BDC-B01 construction support rollback scenarios PASS");
    }

    private static void VerifyOrderRejectionPreservesOnlyPreexistingSupport(
        BuildingSO hallway,
        BuildingSO target)
    {
        Grid grid = CreateGrid();
        Vector2Int center = new(3, 0);
        IReadOnlyList<Vector2Int> footprint = target.GetGridPosList(center);
        BuildableObject existing = PlaceExistingHallway(
            grid,
            hallway,
            footprint[0]);
        var factory = new RecordingFactory(hallway);
        var orders = new BoundedWorkOrders(accept: false);
        var service = CreateService(grid, hallway, factory, orders);
        int sitesBefore = Resources.FindObjectsOfTypeAll<ConstructionSite>().Length;
        try
        {
            Require(!service.TryPlaceConstructionSite(
                        target,
                        center,
                        out string failure)
                    && failure == BoundedWorkOrders.Rejection
                    && ReferenceEquals(
                        grid.GetGridCell(footprint[0])
                            .GetOccupant(GridLayer.Hallway),
                        existing)
                    && footprint.Skip(1).All(position =>
                        !grid.GetGridCell(position)
                            .HasOccupantInLayer(GridLayer.Hallway))
                    && footprint.All(position =>
                        !grid.GetGridCell(position)
                            .HasOccupantInLayer(GridLayer.Construction))
                    && factory.DeletedHallways == footprint.Count - 1,
                "Order rejection leaked support hallway or removed preexisting support.");
            Require(Resources.FindObjectsOfTypeAll<ConstructionSite>().Length == sitesBefore,
                "Order rejection left an unregistered construction-site object in edit mode.");
        }
        finally
        {
            DestroyBuildables(grid);
        }
    }

    private static void VerifySiteRejectionPreservesOnlyPreexistingSupport(
        BuildingSO hallway,
        BuildingSO target)
    {
        Grid grid = CreateGrid();
        Vector2Int center = new(3, 0);
        IReadOnlyList<Vector2Int> footprint = target.GetGridPosList(center);
        BuildableObject existing = PlaceExistingHallway(
            grid,
            hallway,
            footprint[0]);
        var blocker = new ConstructionLayerBlocker();
        Require(grid.RegisterOccupant(
                blocker,
                GridLayer.Construction,
                new[] { footprint[0] },
                false),
            "Could not install the construction-layer blocker.");
        var factory = new RecordingFactory(hallway);
        var service = CreateService(
            grid,
            hallway,
            factory,
            new BoundedWorkOrders(accept: true));
        try
        {
            Require(!service.TryPlaceConstructionSite(target, center, out _)
                    && ReferenceEquals(
                        grid.GetGridCell(footprint[0])
                            .GetOccupant(GridLayer.Hallway),
                        existing)
                    && footprint.Skip(1).All(position =>
                        !grid.GetGridCell(position)
                            .HasOccupantInLayer(GridLayer.Hallway))
                    && factory.DeletedHallways == footprint.Count - 1,
                "Construction-site rejection leaked support hallway or removed preexisting support.");
        }
        finally
        {
            grid.RemoveOccupant(
                GridLayer.Construction,
                new[] { footprint[0] },
                false);
            DestroyBuildables(grid);
        }
    }

    private static void VerifyPartialSupportFailureRollsBackEarlierSupport(
        BuildingSO hallway,
        BuildingSO target)
    {
        Grid grid = CreateGrid();
        Vector2Int center = new(3, 0);
        IReadOnlyList<Vector2Int> footprint = target.GetGridPosList(center);
        var factory = new RecordingFactory(hallway)
        {
            FailHallwayCreateAt = 2
        };
        var service = CreateService(
            grid,
            hallway,
            factory,
            new BoundedWorkOrders(accept: true));
        try
        {
            Require(!service.TryPlaceConstructionSite(target, center, out _)
                    && footprint.All(position =>
                        !grid.GetGridCell(position)
                            .HasOccupantInLayer(GridLayer.Hallway))
                    && footprint.All(position =>
                        !grid.GetGridCell(position)
                            .HasOccupantInLayer(GridLayer.Construction))
                    && factory.DeletedHallways == 1,
                "Partial support creation failure did not roll back earlier support.");
        }
        finally
        {
            DestroyBuildables(grid);
        }
    }

    private static void VerifySuccessKeepsSupportAndConstructionSite(
        BuildingSO hallway,
        BuildingSO target)
    {
        Grid grid = CreateGrid();
        Vector2Int center = new(3, 0);
        IReadOnlyList<Vector2Int> footprint = target.GetGridPosList(center);
        var factory = new RecordingFactory(hallway);
        var orders = new BoundedWorkOrders(accept: true);
        var service = CreateService(grid, hallway, factory, orders);
        try
        {
            Require(service.TryPlaceConstructionSite(target, center, out _)
                    && orders.Created == 1
                    && footprint.All(position =>
                        grid.GetGridCell(position)
                            .HasOccupantInLayer(GridLayer.Hallway))
                    && footprint.All(position =>
                        grid.GetGridCell(position)
                            .HasOccupantInLayer(GridLayer.Construction))
                    && factory.DeletedHallways == 0,
                "Successful construction placement discarded support or its site.");
        }
        finally
        {
            DestroyBuildables(grid);
        }
    }

    private static GridBuildingPlacementService CreateService(
        Grid grid,
        BuildingSO hallway,
        IGridBuildingFactory factory,
        IWorkOrderRuntime orders) => new(
        grid,
        hallway,
        _ => null,
        factory,
        new BuildingPlacementValidator(
            new GridPlacementValidator(),
            () => new BuildingConditionContext(
                null,
                null,
                null,
                PlacementDebugRules.Instance)),
        orders,
        onConstructionSiteCreated: Inject);

    private static Grid CreateGrid()
    {
        var grid = new Grid(8, 1);
        for (int x = 0; x < grid.width; x++)
            grid.SetAreaType(new Vector2Int(x, 0), GridCellAreaType.DungeonInterior);
        return grid;
    }

    private static BuildableObject PlaceExistingHallway(
        Grid grid,
        BuildingSO hallway,
        Vector2Int position)
    {
        var service = new GridBuildingPlacementService(
            grid,
            hallway,
            null,
            new GridBuildingFactory(Inject),
            new BuildingPlacementValidator(
                new GridPlacementValidator(),
                () => new BuildingConditionContext(
                    null,
                    null,
                    null,
                    PlacementDebugRules.Instance)),
            workOrderRuntime: null);
        Require(service.TryPlaceBuildingImmediate(hallway, position, out _),
            "Could not place preexisting hallway support.");
        return grid.GetGridCell(position).GetOccupant(GridLayer.Hallway)
            as BuildableObject;
    }

    private static void Inject(BuildableObject value)
    {
        if (value != null)
            CharacterAiEditorTestDependencies.Inject(value);
    }

    private static void DestroyBuildables(Grid grid)
    {
        var entries = new HashSet<(BuildableObject Value, GridLayer Layer)>();
        for (int x = 0; x < grid.width; x++)
        {
            GridCell cell = grid.GetGridCell(new Vector2Int(x, 0));
            foreach (GridLayer layer in new[]
                     {
                         GridLayer.Hallway,
                         GridLayer.Construction,
                         GridLayer.Building
                     })
            {
                if (cell?.GetOccupant(layer) is BuildableObject value)
                    entries.Add((value, layer));
            }
        }
        foreach ((BuildableObject value, GridLayer layer) in entries)
        {
            if (value == null)
                continue;
            IReadOnlyList<Vector2Int> positions = value.buildPoses?.ToArray()
                ?? Array.Empty<Vector2Int>();
            grid.RemoveOccupant(
                value,
                layer,
                positions,
                layer != GridLayer.Construction
                    && value.BuildingData?.Placement.IsMovement == true);
            UnityEngine.Object.DestroyImmediate(value.gameObject);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecordingFactory : IGridBuildingFactory
    {
        private readonly BuildingSO hallway;
        private readonly GridBuildingFactory inner = new(Inject);
        private int hallwayCreates;

        internal RecordingFactory(BuildingSO hallway) =>
            this.hallway = hallway ?? throw new ArgumentNullException(nameof(hallway));

        internal int FailHallwayCreateAt { get; set; }
        internal int DeletedHallways { get; private set; }

        public BuildableObject Create(
            Grid grid,
            BuildingSO buildingData,
            Vector2Int selectPos)
        {
            if (ReferenceEquals(buildingData, hallway)
                && ++hallwayCreates == FailHallwayCreateAt)
            {
                return null;
            }
            return inner.Create(grid, buildingData, selectPos);
        }

        public BuildableObject CreateDetached(
            Grid grid,
            BuildingSO buildingData,
            Vector2Int selectPos) =>
            inner.CreateDetached(grid, buildingData, selectPos);

        public void PublishDetached(
            BuildableObject candidate,
            BuildingSO buildingData,
            Vector2Int selectPos) =>
            inner.PublishDetached(candidate, buildingData, selectPos);

        public void DiscardDetached(BuildableObject candidate) =>
            inner.DiscardDetached(candidate);

        public void DeleteVisual(BuildingSO buildingData, Vector2Int selectPos)
        {
            if (ReferenceEquals(buildingData, hallway))
                DeletedHallways++;
            inner.DeleteVisual(buildingData, selectPos);
        }
    }

    private sealed class BoundedWorkOrders : IWorkOrderRuntime
    {
        internal const string Rejection = "b0-bdc-b01-order-rejected";
        private readonly bool accept;

        internal BoundedWorkOrders(bool accept) => this.accept = accept;
        internal int Created { get; private set; }
        public int WorkOrderCandidateVersion => Created;
        public DungeonWorkOrderSaveData Capture() => new();
        public void ValidateRestorePayload(DungeonWorkOrderSaveData snapshot) { }
        public WorkOrderRestoreCandidate PrepareRestoreCandidate(
            DungeonWorkOrderSaveData snapshot) => null;
        public void PublishRestoreCandidate(WorkOrderRestoreCandidate candidate) { }

        public bool TryCreateConstructionOrder(
            ConstructionSite site,
            BuildingSO building,
            Vector2Int position,
            out string orderId,
            out string failureReason)
        {
            if (!accept)
            {
                orderId = string.Empty;
                failureReason = Rejection;
                return false;
            }
            Created++;
            orderId = $"work:b0-bdc-b01:{Created:D4}";
            failureReason = string.Empty;
            return true;
        }

        public bool TryAttachConstructionPlacement(
            string orderId,
            string placementOperationId,
            IReadOnlyList<string> supportBuildingIds,
            out string failureReason)
        {
            failureReason = string.Empty;
            return accept && Created > 0;
        }

        public bool TryGetOrderFor(
            BuildableObject target,
            WorkTypeId workTypeId,
            out WorkOrderProgressState order)
        {
            order = default;
            return false;
        }

        public bool ApplyWork(
            CharacterActor worker,
            BuildableObject target,
            WorkTypeId workTypeId,
            float amount,
            out bool completed,
            out bool appliedCompletionEffects,
            out string message)
        {
            completed = false;
            appliedCompletionEffects = false;
            message = string.Empty;
            return false;
        }

        public bool RefreshMaterialsReady(ConstructionSite site) => false;
        public bool CancelOrder(string orderId, bool refundDeliveredMaterials) => false;
        public bool DebugCompleteOrder(string orderId, out string message)
        {
            message = string.Empty;
            return false;
        }
        public int DebugCompleteAllOrders() => 0;
    }

    private sealed class ConstructionLayerBlocker : IGridOccupant
    {
        public int GridId => -9001;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;
    }

    private sealed class PlacementDebugRules : IDungeonDebugRuleQuery
    {
        internal static readonly PlacementDebugRules Instance = new();
        public bool IsExecutingCommand => false;
        public bool IsEnabled(DungeonDebugCheat cheat) =>
            cheat is DungeonDebugCheat.IgnorePlacementRules
                or DungeonDebugCheat.FreeConstruction;
        public bool ShouldFreezeNeed(CharacterCondition condition, float delta) => false;
        public bool ShouldBlockFriendlyDamage(CharacterActor actor) => false;
        public bool ShouldBlockFacilityDamage(bool damaged) => false;
        public bool ShouldSkipCosts() => true;
    }
}
#endif
