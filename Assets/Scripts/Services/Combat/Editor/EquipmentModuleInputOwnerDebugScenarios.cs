#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class EquipmentModuleInputOwnerDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/Debug/V27 Equipment Module Input Owner")]
    public static void Run()
    {
        const string facilityId = "building:fixture:module-bench";
        string destination = EquipmentModuleInputOwnerAuthority
            .DestinationFor(facilityId);
        Require(destination ==
            "facility-input:exact:combat.equipment-module:" + facilityId);
        Require(destination.StartsWith(
            ReservedTargetDestinationIdentity.ExactFacilityInputPrefix,
            StringComparison.Ordinal));
        Require(EquipmentModuleInputOwnerAuthority.CapacitySchemaRevision > 0L);
        Debug.Log("V27 combat.equipment-module exact input-owner scenarios passed.");
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException(
            "V27 equipment-module input-owner scenario failed.");
    }
}
#endif
