using System.Collections.Generic;
using UnityEngine;

internal sealed class WorldItemStackRecord
{
    public string stackId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public WorldItemStackState state;
    public Vector2Int position;
    public string reservedByPersistentId = string.Empty;
    public int reservedQuantity;
    public long reservationRevision;
    public string destinationId = string.Empty;
    public string aggregationCohortId = string.Empty;
    public string sourceStorageDestinationId = string.Empty;
    public bool hasDestinationPosition;
    public Vector2Int destinationPosition;
    public bool forbidden;
    public string sourceCharacterId = string.Empty;
    public string sourceDisplayName = string.Empty;
    public string sourceSpeciesTag = string.Empty;
    public string sourceDeathReason = string.Empty;
    public bool emergencyButcheryAllowed;
    public WasteOriginKind wasteOrigin;
    public float contamination;
    public List<ItemInstanceComponentSaveData> components = new();
    public WorldItemDropDisposition dropDisposition;
    public string recoveryOwnerOperationId = string.Empty;
    public string recoverySourceStackId = string.Empty;
    public string recoveryCarrierPersistentId = string.Empty;
    public WorldItemCarryInterruptionKind recoveryInterruptionKind;
    public double droppedAtGameTime;
    public double recoveryDeadlineGameTime;
}
