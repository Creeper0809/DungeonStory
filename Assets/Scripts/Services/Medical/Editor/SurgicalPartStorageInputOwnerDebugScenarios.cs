#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SurgicalPartStorageInputOwnerDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Medical/Run Surgical Storage Input Owner Check")]
    public static void RunAll()
    {
        string owner = Read(
            "Assets/Scripts/Services/Medical/SurgicalPartStorageInputOwnerAuthority.cs");
        string runtime = Read(
            "Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs");
        Require(owner.Contains("medical.surgical-part-storage",
                StringComparison.Ordinal)
            && owner.Contains("FacilityBufferDestinationAnchorKind.LiveFacility",
                StringComparison.Ordinal)
            && owner.Contains("ExactGramRequired", StringComparison.Ordinal)
            && owner.Contains("MaxMassGrams <= 0L", StringComparison.Ordinal)
            && owner.IndexOf("TryReleaseAtOwnerPosition(", StringComparison.Ordinal)
                < owner.IndexOf("TryReplaceOwnedAuthorities(", StringComparison.Ordinal),
            "surgical storage owner lost its exact pair/release-before-revoke contract");
        Require(!runtime.Contains("TryConsumeFacilityBuffer(",
                StringComparison.Ordinal)
            && !runtime.Contains("TryRequestFacilityDelivery(",
                StringComparison.Ordinal)
            && runtime.Contains("PhysicalItemDispositionKind.Sink",
                StringComparison.Ordinal)
            && runtime.Contains("TryRequestItemDelivery(",
                StringComparison.Ordinal),
            "surgical fuel path regained a raw category/count bypass");
        Debug.Log("[SurgicalPartStorageInputOwner] exact owner and typed fuel checks passed.");
    }

    private static string Read(string path) => File.ReadAllText(
        Path.GetFullPath(path));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
