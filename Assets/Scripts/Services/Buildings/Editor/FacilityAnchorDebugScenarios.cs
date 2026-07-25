using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FacilityAnchorDebugScenarios
{
    private const string CustomSlotPurpose = "debug.anchor.inspect";
    private const string CustomFallbackPurpose = "debug.anchor.fallback";

    [MenuItem("DungeonStory/Debug/Buildings/Run Facility Anchor Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
        {
            Debug.LogError("Facility anchor scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("기본 목적 카탈로그", VerifyBuiltInPurposeCatalog, errors);
        RunScenario("동일 목적 다중 슬롯", VerifyNearestSamePurposeSlot, errors);
        RunScenario("기존 기본 위치", VerifyBuiltInFallbacks, errors);
        RunScenario("사용자 목적 resolver", VerifyCustomPurposeResolver, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Facility anchor scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario())
            {
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        errors.Add(name);
    }

    private static bool VerifyBuiltInPurposeCatalog()
    {
        return FacilityAnchorPurposeCatalog.TryGet(FacilityAnchorPurposeIds.Use, out _)
            && FacilityAnchorPurposeCatalog.TryGet(FacilityAnchorPurposeIds.Work, out _)
            && FacilityAnchorPurposeCatalog.TryGet(FacilityAnchorPurposeIds.Checkout, out _)
            && FacilityAnchorPurposeCatalog.TryGet(FacilityAnchorPurposeIds.Exit, out _);
    }

    private static bool VerifyNearestSamePurposeSlot()
    {
        using AnchorScenarioWorld world = new AnchorScenarioWorld();
        world.Data.FacilityAnchors.Add(CustomSlotPurpose, new Vector2(-1f, 0f));
        world.Data.FacilityAnchors.Add(CustomSlotPurpose, new Vector2(1f, 0f));
        Vector3 left = world.Grid.GetWorldPos(new Vector2(world.Center.x - 1f, world.Center.y));
        Vector3 right = world.Grid.GetWorldPos(new Vector2(world.Center.x + 1f, world.Center.y));

        bool leftResolved = world.Building.TryGetFacilityAnchorWorldPosition(
            CustomSlotPurpose,
            left,
            out Vector3 leftResult);
        bool rightResolved = world.Building.TryGetFacilityAnchorWorldPosition(
            CustomSlotPurpose,
            right,
            out Vector3 rightResult);

        return world.Data.FacilityAnchors.Enumerate(CustomSlotPurpose).Count() == 2
            && leftResolved
            && rightResolved
            && Approximately(leftResult, left)
            && Approximately(rightResult, right);
    }

    private static bool VerifyBuiltInFallbacks()
    {
        using AnchorScenarioWorld world = new AnchorScenarioWorld();
        Vector3 expectedUse = world.Grid.GetWorldPos(new Vector2(world.Center.x - 1f, world.Center.y));
        Vector3 expectedWork = world.Grid.GetWorldPos(new Vector2(world.Center.x + 0.7f, world.Center.y));
        Vector3 expectedCheckout = world.Grid.GetWorldPos(new Vector2(world.Center.x + 0.5f, world.Center.y));

        bool useResolved = world.Building.TryGetFacilityAnchorWorldPosition(
            FacilityAnchorPurposeIds.Use,
            expectedUse,
            out Vector3 use);
        bool workResolved = world.Building.TryGetFacilityAnchorWorldPosition(
            FacilityAnchorPurposeIds.Work,
            expectedUse,
            out Vector3 work);
        bool checkoutResolved = world.Building.TryGetFacilityAnchorWorldPosition(
            FacilityAnchorPurposeIds.Checkout,
            expectedUse,
            out Vector3 checkout);

        return useResolved
            && workResolved
            && checkoutResolved
            && Approximately(use, expectedUse)
            && Approximately(work, expectedWork)
            && Approximately(checkout, expectedCheckout);
    }

    private static bool VerifyCustomPurposeResolver()
    {
        using AnchorScenarioWorld world = new AnchorScenarioWorld();
        FacilityAnchorPurposeCatalog.Unregister(CustomFallbackPurpose);
        bool registered = FacilityAnchorPurposeCatalog.Register(
            new FacilityAnchorPurposeDefinition(CustomFallbackPurpose, ResolveCustomFallback));

        try
        {
            bool resolved = world.Building.TryGetFacilityAnchorWorldPosition(
                CustomFallbackPurpose,
                world.Building.transform.position,
                out Vector3 result);
            Vector3 expected = world.Grid.GetWorldPos(new Vector2(world.Center.x - 1f, world.Center.y));
            return registered && resolved && Approximately(result, expected);
        }
        finally
        {
            FacilityAnchorPurposeCatalog.Unregister(CustomFallbackPurpose);
        }
    }

    private static bool ResolveCustomFallback(
        BuildableObject building,
        Vector3 fromWorld,
        out Vector3 worldPosition)
    {
        return building.TryGetHorizontalFootprintAnchorWorldPosition(0f, out worldPosition);
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return (left - right).sqrMagnitude <= 0.0001f;
    }

    private sealed class AnchorScenarioWorld : IDisposable
    {
        private readonly GameObject buildingObject;

        public AnchorScenarioWorld()
        {
            Grid = new Grid(10, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            Center = new Vector2Int(4, 0);
            Data = ScriptableObject.CreateInstance<BuildingSO>();
            Data.id = 99001;
            Data.objectName = "Anchor Scenario Facility";
            Data.width = 3;
            Data.height = 1;
            Data.layer = GridLayer.Building;
            Data.category = BuildingCategory.Special;
            Data.type = typeof(Facility);
            Data.Facility = new FacilityData();

            buildingObject = new GameObject("Anchor Scenario Facility");
            Building = buildingObject.AddComponent<Facility>();
            CharacterAiEditorTestDependencies.Inject(Building);
            Building.SetGrid(Grid);
            Building.Initialization(Data, Center);
            if (!Grid.RegisterOccupant(Building, GridLayer.Building, Data.GetGridPosList(Center), false))
            {
                throw new InvalidOperationException("Anchor scenario facility could not be registered.");
            }
        }

        public Grid Grid { get; }
        public Vector2Int Center { get; }
        public BuildingSO Data { get; }
        public Facility Building { get; }

        public void Dispose()
        {
            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(Data);
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }
}
