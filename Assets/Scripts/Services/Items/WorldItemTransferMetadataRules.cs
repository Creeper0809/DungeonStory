using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal static class WorldItemTransferMetadataRules
{

    internal static List<ItemInstanceComponentSaveData> CloneComponents(
        IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(component => component != null)
        .Select(component => component.Clone())
        .ToList();

    internal static void ClearTransientRecoveryMetadata(WorldItemStackRecord record)
    {
        if (record == null)
            return;
        record.dropDisposition = WorldItemDropDisposition.None;
        record.recoveryOwnerOperationId = string.Empty;
        record.recoverySourceStackId = string.Empty;
        record.recoveryCarrierPersistentId = string.Empty;
        record.recoveryInterruptionKind = WorldItemCarryInterruptionKind.None;
        record.droppedAtGameTime = 0d;
        record.recoveryDeadlineGameTime = 0d;
    }

    internal static List<ItemInstanceComponentSaveData>
        CapturePhysicalBusinessComponents(
            IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(component => component != null
            && !PlannedOutputPublicationComponentCodec.IsAnyMarker(component)
            && !FacilityOutputExactRouteCustodyCodec.IsCustody(component))
        .Select(component => component.Clone())
        .ToList();
}
