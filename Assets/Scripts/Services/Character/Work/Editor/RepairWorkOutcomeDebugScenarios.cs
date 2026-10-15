#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class RepairWorkOutcomeDebugScenarios
{
    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds
        { get; } = new[]
        {
            MigratedProducerOutcomeKind.RepairWorkResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            Require(
                WorkAmountDebugScenarios.RunAll(logSuccess: false),
                "Repair work owner/save/retry scenarios failed.");
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
        Debug.Log("REPAIR_WORK_OUTCOME=PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
