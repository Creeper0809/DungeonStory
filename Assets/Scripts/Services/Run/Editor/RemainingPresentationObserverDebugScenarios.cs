#if UNITY_EDITOR
using System;
using System.Globalization;
using UnityEngine;

public static class RemainingPresentationObserverDebugScenarios
{
    public static void RunForBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("B129 requires isolated batch execution.");
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CaptivityInteractionOutcomeDebugScenarios.RunPresentationForBatch();
            MilestonePressurePresentationDebugScenarios.RunForBatch();
            Debug.Log("B129_MILESTONE_PRESSURE_OBSERVERS=PASS");
            MixedApplicationPresentationOwnershipDebugScenarios.RunForBatch();
            Debug.Log("B129_REMAINING_PRESENTATION_OBSERVERS=PASS");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
#endif
