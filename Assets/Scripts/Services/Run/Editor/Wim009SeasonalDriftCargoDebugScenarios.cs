#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

public static class Wim009SeasonalDriftCargoDebugScenarios
{
    [MenuItem("DungeonStory/Debug/WIM-009/Run Seasonal Drift Cargo Contracts")]
    public static void Run()
    {
        SeasonalDriftCargoProfile authored = new()
        {
            itemId = "material:lumber",
            exactQuantity = 6
        };
        Require(authored.Validate("seasonal:spring-drift-cargo").Count == 0,
            "Approved cargo authoring must validate.");

        SeasonalDriftCargoSaveData state =
            SeasonalDriftCargoSaveData.FromProfile(authored);
        state.positionFrozen = true;
        state.positionX = 12;
        state.positionY = 4;
        state.spawned = true;
        state.sourceStackIds = new List<string> { "stack:wim009:1" };
        state.securedQuantity = 2;
        SeasonalDriftCargoRules.RequireValidFrozenState(
            state,
            authored,
            "seasonal-occurrence:1");

        WorldItemStackSnapshot stack = new()
        {
            Components = new[]
            {
                SeasonalDriftCargoRules.CreateComponent(
                    "seasonal-occurrence:1",
                    secured: true)
            }
        };
        Require(SeasonalDriftCargoRules.TryReadComponent(
                stack,
                out string occurrenceId,
                out bool secured)
            && occurrenceId == "seasonal-occurrence:1"
            && secured,
            "Cargo ownership marker must round-trip exactly.");

        state.expiredQuantity = 5;
        RequireThrows(() => SeasonalDriftCargoRules.RequireValidFrozenState(
            state,
            authored,
            "seasonal-occurrence:1"));

        UnityEngine.Debug.Log(
            "WIM009_SEASONAL_DRIFT_CARGO=PASS; item=material:lumber; quantity=6; duration=3..5");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Malformed drift-cargo state must be rejected.");
    }
}
#endif
