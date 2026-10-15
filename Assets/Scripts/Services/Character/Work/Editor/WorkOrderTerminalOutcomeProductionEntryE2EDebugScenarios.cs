#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class WorkOrderTerminalOutcomeProductionEntryE2EDebugScenarios
{
    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        Array.AsReadOnly(new[]
        {
            MigratedProducerOutcomeKind.WorkOrderTerminalResult
        });

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            if (!WorkAmountDebugScenarios.RunAll(logSuccess: false))
            {
                throw new InvalidOperationException(
                    "Work-order terminal production-entry scenarios failed.");
            }
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
        if (!RunAll())
        {
            throw new InvalidOperationException(
                "Work-order terminal batch failed.");
        }
        UnityEngine.Debug.Log("[Work Order Terminal Outcome] PASS");
    }
}
#endif
