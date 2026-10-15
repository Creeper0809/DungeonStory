#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CombatEquipmentCraftCompletionOutcomeDebugScenarios
{
    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds
        { get; } = new[]
        {
            MigratedProducerOutcomeKind.CombatEquipmentCraftCompletionResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            Require(
                CombatEquipmentCraftTransactionFixture.Run(),
                "Combat craft completion transaction failed: "
                + CombatEquipmentCraftTransactionFixture.LastFailureReason);
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void RunBatch()
    {
        _ = RunAll();
        Debug.Log("COMBAT_EQUIPMENT_CRAFT_COMPLETION_OUTCOME=PASS");
    }

    public static void RunEec06FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_EEC06_COMBAT_CRAFT_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_EEC06_COMBAT_CRAFT_REPORT_PATH is required.");
        }
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "EEC06 combat-craft report must be a create-only path under Artifacts/QA.");
        }
        Require(
            CombatEquipmentCraftTransactionFixture.RunEec06(),
            "EEC06 production combat-craft verification failed: "
            + CombatEquipmentCraftTransactionFixture.LastFailureReason);
        CombatEquipmentCraftTerminalAuthorityDebugScenarios.RunAll();

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        using FileStream report = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(report);
        writer.Write(
            "U0_EEC06_COMBAT_CRAFT=PASS\n"
            + "runtimeEntry=CombatEquipmentCraftingRuntime\n"
            + "commands=material-allowed,material-priority,order-queued,worker-policy,quality-target\n"
            + "actualCraft=material-physical-child,quality-child,unique-equipment-output,completion-parent\n"
            + "identity=input-source-stacks,output-stack,output-instance,contributor,quality-result-key\n"
            + "persistence=combat-aggregate-json,fresh-runtime,parent-only-resume\n"
            + "partialCraft=section-capture,absent-output-fields-null,fresh-wip-restore,no-second-consumption\n"
            + "strictRejection=former-default-objects,empty-component,empty-provenance,owner-unchanged\n"
            + "terminalRegression=zero-wip-remove,wip-crash-resume,frozen-source-drift\n"
            + "exactlyOnce=output1,quality1,parent1,replay0\n"
            + "summary=" + CombatEquipmentCraftTransactionFixture.LastEec06Summary + "\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_EEC06_COMBAT_CRAFT=PASS; report=" + fullPath);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
