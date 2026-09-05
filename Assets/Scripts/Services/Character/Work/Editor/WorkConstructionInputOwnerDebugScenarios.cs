#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WorkConstructionInputOwnerDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/Debug/V27 Work Construction Input Owner")]
    public static void Run()
    {
        const string orderId = "work:000001";
        string destination = WorkConstructionInputOwnerAuthority
            .DestinationFor(orderId);
        WorkConstructionInputOwnerDescriptor descriptor = new(
            orderId, destination, "building:construction:fixture",
            new Vector2Int(3, 4),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["resource:stone"] = 2
            },
            2000L, 1L, "fixture-fingerprint");
        Require(destination.StartsWith(
            ReservedTargetDestinationIdentity.ExactFacilityInputPrefix,
            StringComparison.Ordinal));
        Require(descriptor.Requirements.Count == 1
            && descriptor.StoredCapacityGrams > 0L
            && descriptor.StoredMassAuthorityRevision > 0L);
        WorkOrderSaveData record = new()
        {
            workOrderId = orderId,
            constructionSitePersistentId = descriptor.ConstructionSitePersistentId,
            materialDestinationId = destination,
            materialBufferCapacityGrams = descriptor.StoredCapacityGrams,
            materialMassAuthorityRevision = descriptor.StoredMassAuthorityRevision,
            materialCapacityFingerprint = descriptor.StoredCapacityFingerprint
        };
        Require(record.materialBufferCapacityGrams == 2000L
            && record.materialMassAuthorityRevision == 1L
            && record.materialCapacityFingerprint == "fixture-fingerprint"
            && DungeonWorkOrderSaveData.CurrentVersion == 8);
        Debug.Log("V27 work.construction exact input-owner scenarios passed.");
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException(
            "V27 work.construction input-owner scenario failed.");
    }
}
#endif
