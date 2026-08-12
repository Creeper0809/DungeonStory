#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterAnatomyMedicalIntegrationDebugScenarios
{
    [MenuItem("DungeonStory/Debug/QA/Run Character Anatomy Medical Integration")]
    public static void RunFromMenu()
    {
        IReadOnlyList<string> errors = RunAll();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }
        Debug.Log("CHARACTER_ANATOMY_MEDICAL_INTEGRATION PASS");
    }

    public static IReadOnlyList<string> RunAll()
    {
        List<string> errors = new();
        CheckAdvancedPartMath(errors);
        CheckPredictivePrefetch(errors);
        CheckPhysicalFieldMedicalSupplies(errors);
        CheckFieldMedicalAndRescue(errors);
        return errors;
    }

    private static void CheckAdvancedPartMath(List<string> errors)
    {
        AnatomyNodeHealthState node = new AnatomyNodeHealthState
        {
            maxHealth = 100f,
            currentHealth = 80f,
            installedPartEfficiency = 1.35f,
            recoveryPolicy = PartRecoveryPolicy.MaintenanceOnly
        };
        Require(Mathf.Abs(node.FunctionalEfficiency - 1.08f) < 0.0001f,
            "1.35 efficiency at 80% health must produce 1.08 functional efficiency.", errors);
        Require(node.FunctionalEfficiency > 1f,
            "Functional efficiency must not clamp advanced parts to 1.0.", errors);
    }

    private static void CheckPredictivePrefetch(List<string> errors)
    {
        Require(ProductionMaterialPrefetchPolicy.CalculateBatchCount(12f, 3f, 20f) == 1,
            "Slow production should request one batch.", errors);
        Require(ProductionMaterialPrefetchPolicy.CalculateBatchCount(12f, 3f, 7f) == 3,
            "Fast production should request three batches.", errors);
        Require(ProductionMaterialPrefetchPolicy.CalculateBatchCount(12f, 3f, 0.5f) == 3,
            "Prefetch must remain capped at three batches.", errors);
    }

    private static void CheckFieldMedicalAndRescue(List<string> errors)
    {
        OffenseFieldMedicalRuntime runtime = new OffenseFieldMedicalRuntime();
        Require(runtime.TryApplyStabilization(
                "expedition:a", "character:a", "node:wing", "kit:instance:1", 3,
                out _),
            "First field stabilization should succeed.", errors);
        Require(!runtime.TryApplyStabilization(
                "expedition:a", "character:a", "node:wing", "kit:instance:2", 4,
                out _),
            "The same node must not be stabilized twice in one expedition.", errors);
        Require(runtime.TryAssignCarrier(
                "expedition:a", "character:a", "character:b",
                35f, 5f, 60f, 10f, out _),
            "A carrier with enough remaining capacity should accept one casualty.", errors);
        Require(!runtime.TryAssignCarrier(
                "expedition:a", "character:c", "character:b",
                10f, 0f, 60f, 10f, out _),
            "One carrier must not carry two casualties.", errors);
        Require(runtime.GetMovementTimeMultiplier("expedition:a") > 1f,
            "Casualty carrying must slow hex travel.", errors);
        Require(runtime.TrySetStranded(
                "expedition:a", new OffenseHexCoord(2, -1), 4f, 12f, "이동 불능"),
            "Expedition should enter stranded state.", errors);
        Require(runtime.TryDispatchRescue(
                "expedition:a", "expedition:rescue",
                new[] { "character:r1", "character:r2" }, out _),
            "A 1-5 member rescue expedition should dispatch.", errors);
        Require(runtime.TryMergeRescue(
                "expedition:rescue", new[] { "character:a" }, out _),
            "Rescue convoy should merge on arrival.", errors);
        Require(!runtime.IsStranded("expedition:a"),
            "Merged rescue must clear the active stranded state.", errors);

        OffenseWorldSaveData save = new OffenseWorldSaveData();
        runtime.Capture(save);
        OffenseFieldMedicalRuntime restored = new OffenseFieldMedicalRuntime();
        restored.PublishRestoreCandidate(
            restored.BuildRestoreCandidate(save));
        Require(restored.GetStabilizations("expedition:rescue").Count == 1,
            "Merged field stabilization must follow the rescue convoy through save/restore.", errors);
        restored.ClearOnDungeonArrival("expedition:rescue");
        Require(restored.GetStabilizations("expedition:rescue").Count == 0,
            "Field stabilization must clear when the rescue convoy reaches the dungeon.", errors);
    }

    private static void CheckPhysicalFieldMedicalSupplies(List<string> errors)
    {
        OffenseSupplyType[] kits =
        {
            OffenseSupplyType.FieldEmergencyKit,
            OffenseSupplyType.RuneSlimePatch,
            OffenseSupplyType.MycelialCulturePack,
            OffenseSupplyType.WingSplintKit,
            OffenseSupplyType.TemporaryPowerBypass,
            OffenseSupplyType.BloodSealKit,
            OffenseSupplyType.ManaCoreRestraint
        };
        string[] itemIds = kits
            .Select(OffenseSupplyCatalog.GetPhysicalItemId)
            .ToArray();
        Require(itemIds.All(id => !string.IsNullOrWhiteSpace(id)),
            "Every field medical supply must map to a physical item id.", errors);
        Require(itemIds.Distinct(StringComparer.Ordinal).Count() == kits.Length,
            "Field medical supplies must not share physical item ids.", errors);
        Require(OffenseSupplyCatalog.GetFieldMedicalKit("Slime")
                == OffenseSupplyType.RuneSlimePatch,
            "Slime expeditions must use rune slime patches.", errors);
        Require(OffenseSupplyCatalog.GetFieldMedicalKit("Golem")
                == OffenseSupplyType.TemporaryPowerBypass,
            "Golem expeditions must use temporary power bypasses.", errors);
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }
}
#endif
