#if UNITY_EDITOR
using System;
using UnityEditor;

public static class FacilityEvolutionInputOwnerDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/Debug/V27 Facility Evolution Input Owner")]
    public static void Run()
    {
        const string orderId = "facility-modification:fixture";
        string modification = FacilityEvolutionInputOwnerAuthority
            .DestinationFor(FacilityEvolutionInputKind.Modification, orderId);
        string relocation = FacilityEvolutionInputOwnerAuthority
            .DestinationFor(FacilityEvolutionInputKind.Relocation, orderId);
        Require(modification != relocation
            && modification.StartsWith(
                ReservedTargetDestinationIdentity.ExactFacilityInputPrefix,
                StringComparison.Ordinal));
        FacilityModificationOrder source = new()
        {
            orderId = orderId,
            facilityPersistentId = "building:fixture:evolution",
            destinationId = modification,
            inputCapacityGrams = 3000L,
            inputMassAuthorityRevision = 2L,
            inputCapacityFingerprint = "fixture-fingerprint"
        };
        FacilityModificationOrder clone = source.Clone();
        Require(clone.inputCapacityGrams == 3000L
            && clone.inputMassAuthorityRevision == 2L
            && clone.inputCapacityFingerprint == "fixture-fingerprint"
            && FacilityEvolutionInputOwnerAuthority.CapacitySchemaRevision > 0L);
        UnityEngine.Debug.Log(
            "V27 facility.evolution exact input-owner scenarios passed.");
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException(
            "V27 facility-evolution input-owner scenario failed.");
    }
}
#endif
