using System;
using System.Reflection;
using UnityEngine;

public static class OffenseExpeditionAccessDebugScenarios
{
    public static string Run()
    {
        BlueprintResearchState state = new BlueprintResearchState();
        if (OffenseExpeditionAccessRules.IsUnlocked(state))
        {
            throw new InvalidOperationException(
                "A new run must not launch expeditions before field-rations research.");
        }

        state.Projects.Complete(
            new ResearchProjectId(OffenseExpeditionAccessRules.RequiredResearchId));
        if (!OffenseExpeditionAccessRules.IsUnlocked(state))
        {
            throw new InvalidOperationException(
                "Field-rations research did not unlock expedition launch.");
        }

        if (state.Projects.CompletedProjectIds.Count != 1)
        {
            throw new InvalidOperationException(
                "Expedition access validation changed unrelated research state.");
        }

        VerifyDirectRuntimeLaunchCannotBypassGate();

        return "PASS: world-map planning remains separate from the field-rations expedition launch gate.";
    }

    private static void VerifyDirectRuntimeLaunchCannotBypassGate()
    {
        GameObject host = new GameObject("Offense Expedition Access Probe");
        try
        {
            OffenseExpeditionRuntime runtime =
                host.AddComponent<OffenseExpeditionRuntime>();
            SetPrivateField(runtime, "expeditionResearchState", new BlueprintResearchState());
            SetPrivateField(runtime, "enforceExpeditionAccess", true);

            bool started = runtime.TryStartExpedition(
                "target:probe",
                Array.Empty<CharacterActor>(),
                out OffenseExpeditionRun expedition,
                out string message);
            if (started
                || expedition != null
                || !string.Equals(
                    message,
                    OffenseExpeditionAccessRules.BlockerMessage,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Direct runtime launch bypassed the field-rations gate.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void SetPrivateField(
        OffenseExpeditionRuntime runtime,
        string fieldName,
        object value)
    {
        FieldInfo field = typeof(OffenseExpeditionRuntime).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new MissingFieldException(
                nameof(OffenseExpeditionRuntime),
                fieldName);
        }

        field.SetValue(runtime, value);
    }
}
