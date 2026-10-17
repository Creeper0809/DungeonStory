using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>Projects physical transfer snapshots from the existing repository records and mass authority.</summary>
internal sealed class WorldItemTransferPhysicalProjection
{
    private readonly IPhysicalItemMassQuery physicalMass;

    internal WorldItemTransferPhysicalProjection(
        IPhysicalItemMassQuery physicalMass)
    {
        this.physicalMass = physicalMass;
    }

    internal long GetPhysicalQuantityMass(
        string itemId,
        string itemInstanceId,
        IReadOnlyList<ItemInstanceComponentSaveData> businessComponents,
        int quantity)
    {
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            physicalMass,
            (ItemDefinitionId)itemId,
            itemInstanceId,
            businessComponents);
        return physicalMass.GetQuantityMass(
            (ItemDefinitionId)itemId,
            subject,
            quantity).Value;
    }

    internal static WorldItemStackRecord CloneRecord(WorldItemStackRecord source) => new()
    {
        stackId = source.stackId,
        itemInstanceId = source.itemInstanceId,
        itemId = source.itemId,
        quantity = source.quantity,
        state = source.state,
        position = source.position,
        reservedByPersistentId = source.reservedByPersistentId,
        reservedQuantity = source.reservedQuantity,
        reservationRevision = source.reservationRevision,
        destinationId = source.destinationId,
        aggregationCohortId = source.aggregationCohortId,
        sourceStorageDestinationId = source.sourceStorageDestinationId,
        hasDestinationPosition = source.hasDestinationPosition,
        destinationPosition = source.destinationPosition,
        forbidden = source.forbidden,
        sourceCharacterId = source.sourceCharacterId,
        sourceDisplayName = source.sourceDisplayName,
        sourceSpeciesTag = source.sourceSpeciesTag,
        sourceDeathReason = source.sourceDeathReason,
        emergencyButcheryAllowed = source.emergencyButcheryAllowed,
        wasteOrigin = source.wasteOrigin,
        contamination = source.contamination,
        components = (source.components ?? new List<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList(),
        dropDisposition = source.dropDisposition,
        recoveryOwnerOperationId = source.recoveryOwnerOperationId,
        recoverySourceStackId = source.recoverySourceStackId,
        recoveryCarrierPersistentId = source.recoveryCarrierPersistentId,
        recoveryInterruptionKind = source.recoveryInterruptionKind,
        droppedAtGameTime = source.droppedAtGameTime,
        recoveryDeadlineGameTime = source.recoveryDeadlineGameTime
    };
}
