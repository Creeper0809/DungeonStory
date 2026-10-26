using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class R26BuildingConnectionGuardDebugScenarios
{
    [MenuItem("DungeonStory/R26/Validate Building Connection Guard")]
    public static void Run()
    {
        Debug.Log(ValidateOnly());
    }

    public static string ValidateOnly()
    {
        VerifyMissingDefinitionIdDoesNotQuery();
        VerifyLegacyZeroDefinitionIdIsPreserved();
        VerifyEntranceModeUsesEntranceQuery();
        VerifyGridRequiresBuildingOccupant();

        return "R26 building connection guard passed: missing definition IDs "
            + "are rejected, entrance mode is preserved, and matching non-building "
            + "occupants cannot satisfy a building definition query.";
    }

    private static void VerifyMissingDefinitionIdDoesNotQuery()
    {
        var connectivity = new RecordingConnectivity
        {
            DefinitionResult = true
        };
        var condition = new ConditionNeedToConnect
        {
            connectWithEntrance = false,
            associatedId = -1
        };

        bool satisfied = condition.IsSatisfy(
            connectivity,
            new List<Vector2Int> { Vector2Int.zero },
            null,
            out string error);

        Require(!satisfied, "A missing building definition ID was accepted.");
        Require(
            error.IndexOf("ID", StringComparison.Ordinal) >= 0,
            "A missing building definition ID did not report an explicit failure.");
        Require(
            connectivity.DefinitionCalls == 0
            && connectivity.EntranceCalls == 0,
            "A connectivity query ran before the missing definition ID was rejected.");
    }

    private static void VerifyEntranceModeUsesEntranceQuery()
    {
        var connectivity = new RecordingConnectivity
        {
            EntranceResult = true
        };
        var condition = new ConditionNeedToConnect
        {
            connectWithEntrance = true,
            associatedId = 0
        };

        bool satisfied = condition.IsSatisfy(
            connectivity,
            new List<Vector2Int> { Vector2Int.zero },
            null,
            out string error);

        Require(satisfied, "Entrance connection mode rejected a valid entrance path.");
        Require(string.IsNullOrEmpty(error), "Entrance connection mode returned an error.");
        Require(
            connectivity.EntranceCalls == 1
            && connectivity.DefinitionCalls == 0,
            "Entrance connection mode used the building definition query.");
    }

    private static void VerifyLegacyZeroDefinitionIdIsPreserved()
    {
        var connectivity = new RecordingConnectivity { DefinitionResult = true };
        var condition = new ConditionNeedToConnect { connectWithEntrance = false, associatedId = 0 };
        var restored = JsonUtility.FromJson<ConditionNeedToConnect>(JsonUtility.ToJson(condition));
        Require(restored.associatedId == 0 && restored.IsSatisfy(connectivity,
                new List<Vector2Int> { Vector2Int.zero }, null, out _)
            && connectivity.LastDefinitionId == 0 && connectivity.DefinitionCalls == 1,
            "A valid legacy building ID zero was changed or rejected.");
    }

    private static void VerifyGridRequiresBuildingOccupant()
    {
        const int definitionId = 0;
        Grid grid = new Grid(3, 1);
        var movement = new MovementOccupant(definitionId);
        Vector2Int target = new Vector2Int(1, 0);
        Require(
            grid.RegisterOccupant(
                movement,
                GridLayer.Character,
                new[] { target },
                connectPositions: false),
            "Movement occupant registration failed.");

        var adapter = new BuildingConnectivityQueryAdapter(grid);
        Require(
            !adapter.IsConnected(Vector2Int.zero, definitionId),
            "A matching movement occupant satisfied a building definition query.");
        Require(
            grid.IsConnected(Vector2Int.zero, definitionId),
            "The existing general occupant query no longer finds matching occupants.");

        GameObject owner = new GameObject("R26-building-connection-fixture");
        try
        {
            BuildableObject building = owner.AddComponent<BuildableObject>();
            SetBuildingDefinitionId(building, definitionId);
            Require(
                grid.RegisterOccupant(
                    building,
                    GridLayer.Building,
                    new[] { target },
                    connectPositions: false),
                "Building occupant registration failed.");
            Require(
                adapter.IsConnected(Vector2Int.zero, definitionId),
                "A matching building occupant did not satisfy the definition query.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void SetBuildingDefinitionId(
        BuildableObject building,
        int definitionId)
    {
        FieldInfo field = typeof(BuildableObject).GetField(
            "<id>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, "BuildableObject definition ID backing field is missing.");
        field.SetValue(building, definitionId);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingConnectivity : IBuildingConnectivityQueryPort
    {
        public bool EntranceResult { get; set; }
        public bool DefinitionResult { get; set; }
        public int EntranceCalls { get; private set; }
        public int DefinitionCalls { get; private set; }
        public int LastDefinitionId { get; private set; } = -1;

        public bool IsConnectedWithAny(
            IReadOnlyCollection<Vector2Int> positions)
        {
            EntranceCalls++;
            return EntranceResult;
        }

        public bool IsConnected(Vector2Int start, int associatedId)
        {
            DefinitionCalls++;
            LastDefinitionId = associatedId;
            return DefinitionResult;
        }
    }

    private sealed class MovementOccupant :
        IGridOccupant,
        IGridMovementOccupant
    {
        public MovementOccupant(int gridId)
        {
            GridId = gridId;
        }

        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
        public GridMoveType GridMoveType => GridMoveType.Walk;
    }
}
