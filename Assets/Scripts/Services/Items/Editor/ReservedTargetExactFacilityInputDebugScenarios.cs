#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class ReservedTargetExactFacilityInputDebugScenarios
{
    private const string ExactFacilityInputPrefix = "facility-input:exact:";

    [MenuItem(
        "DungeonStory/Debug/Items/Run Reserved Target Exact Facility Input Contracts")]
    public static void RunAll()
    {
        VerifyAll();
        Debug.Log("V27_RESERVED_TARGET_EXACT_FACILITY_INPUT_PASS");
    }

    internal static void VerifyAll()
    {
        Require(
            ReservedTargetDestinationIdentity.RequiresExactClaim(
                ExactFacilityInputPrefix
                + "medical.character-supply:medical:1:00000001"),
            "The exact FacilityInput namespace did not require an exact claim.");
        Require(
            ReservedTargetDestinationIdentity.RequiresExactClaim(
                ReservedTargetDestinationIdentity.PhysicalSourceBufferPrefix
                + "exterior.merchant-cart:incident:0001"),
            "The physical exact-source namespace did not require an exact claim.");
        Require(
            !ReservedTargetDestinationIdentity.RequiresExactClaim(
                "facility-input:legacy-test"),
            "A legacy FacilityInput destination unexpectedly required an exact claim.");
        Require(
            !ReservedTargetDestinationIdentity.RequiresExactClaim(
                "facility-input:exactly:legacy-test"),
            "The exact FacilityInput namespace accepted a non-boundary prefix match.");
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
