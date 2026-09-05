#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WasteDirectFeedAuthorityDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Economy/Run Waste Direct Feed Authority Check")]
    public static void RunAll()
    {
        string runtime = File.ReadAllText(Path.GetFullPath(
            "Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs"));
        string adapter = File.ReadAllText(Path.GetFullPath(
            "Assets/Scripts/Services/Economy/Waste/WasteProcessingPortAdapters.cs"));
        Require(runtime.Contains("HasExactWildlifeCareAuthority(",
                StringComparison.Ordinal)
            && runtime.Contains("TryRequestStackDelivery(",
                StringComparison.Ordinal)
            && adapter.Contains("captivity.wildlife-care",
                StringComparison.Ordinal)
            && adapter.Contains("FacilityBufferDestinationAnchorKind.LiveFacility",
                StringComparison.Ordinal)
            && adapter.Contains("ExactGramRequired", StringComparison.Ordinal)
            && adapter.Contains("MaxMassGrams > 0L", StringComparison.Ordinal),
            "waste direct-feed is not joined to the delegated exact wildlife-care authority");
        Debug.Log("[WasteDirectFeedAuthority] delegated exact-authority check passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
