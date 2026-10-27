#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class R26ConveyorDestinationAuthoringDebugScenarios
{
    private const string NodeId = "building:r26-conveyor-port";
    private const string DestinationId =
        "facility-buffer:building:r26-conveyor-output";

    [MenuItem(
        "DungeonStory/Debug/Infrastructure/Run R26 Conveyor Destination Authoring Checks")]
    public static void RunAll()
    {
        VerifyAuthoringFieldContract();
        VerifyProgrammaticDescriptorContract();
        VerifyRuntimeSaveRoundTrip();
        Debug.Log(
            "R26_CONVEYOR_DESTINATION_AUTHORING_PASS;"
            + "inspectorHidden=true;serializedSeedPreserved=true;"
            + "descriptorWritePreserved=true;runtimeSaveRoundTrip=true");
    }

    private static void VerifyAuthoringFieldContract()
    {
        FieldInfo field = typeof(BuildingConveyorPortAbility).GetField(
            nameof(BuildingConveyorPortAbility.destinationId),
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.DeclaredOnly);
        Require(field != null,
            "Conveyor destination legacy seed field is missing.");
        Require(field.FieldType == typeof(string) && !field.IsNotSerialized,
            "Conveyor destination legacy seed changed name, type, or serialization.");
        Require(field.GetCustomAttribute<HideInInspector>(false) != null,
            "Persistent conveyor destination authoring is still exposed in the Inspector.");

        var ability = new BuildingConveyorPortAbility
        {
            destinationId = DestinationId
        };
        string serialized = JsonUtility.ToJson(ability);
        Require(serialized.IndexOf(
                "\"destinationId\"",
                StringComparison.Ordinal) >= 0,
            "Conveyor destination legacy seed is no longer serialized.");
        BuildingConveyorPortAbility restored =
            JsonUtility.FromJson<BuildingConveyorPortAbility>(serialized);
        Require(restored != null
                && string.Equals(
                    restored.destinationId,
                    DestinationId,
                    StringComparison.Ordinal),
            "Conveyor destination legacy seed serialization changed.");
    }

    private static void VerifyProgrammaticDescriptorContract()
    {
        IndustrialInfrastructureStressReport report =
            IndustrialInfrastructureStressProbe.Run(
                width: 2,
                height: 1,
                routeRequests: 1);
        Require(report.UtilityCellCount == 2
                && report.PayloadRouteCount == 1,
            "Programmatic conveyor descriptor destination routing changed.");
    }

    private static void VerifyRuntimeSaveRoundTrip()
    {
        var nodes = new Dictionary<string, ConveyorNodeRuntimeState>(
            StringComparer.Ordinal)
        {
            [NodeId] = new ConveyorNodeRuntimeState
            {
                DestinationId = DestinationId
            }
        };
        var payloads = new Dictionary<string, ConveyorPayloadRuntimeState>(
            StringComparer.Ordinal);

        DungeonConveyorInfrastructureSaveData captured =
            ConveyorPersistence.Capture(
                nodes,
                payloads,
                nextPayloadSequence: 1,
                currentTime: 12f);
        IndustrialInfrastructureSaveValidation.RequireValid(captured);
        Require(captured.nodes.Count == 1
                && string.Equals(
                    captured.nodes[0].destinationId,
                    DestinationId,
                    StringComparison.Ordinal),
            "Runtime conveyor destination was not captured in the node DTO.");

        string json = JsonUtility.ToJson(captured);
        DungeonConveyorInfrastructureSaveData serialized =
            JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(json);
        ConveyorRestoreState restored = ConveyorPersistence.Restore(
            serialized,
            currentTime: 30f);
        Require(restored.Nodes.TryGetValue(
                    NodeId,
                    out ConveyorNodeRuntimeState restoredNode)
                && string.Equals(
                    restoredNode.DestinationId,
                    DestinationId,
                    StringComparison.Ordinal),
            "Runtime conveyor destination changed across save and restore.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
