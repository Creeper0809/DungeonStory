using System.Collections.Generic;
using UnityEngine;

public readonly struct WildlifeHaulAssignmentSnapshot
{
    public WildlifeHaulAssignmentSnapshot(
        string wildlifeId,
        CapturedWildlifeHaulPhase phase,
        float nextDecisionAt,
        string operationId,
        string leaseId,
        string sourceStackId,
        string cargoStackId,
        string itemId,
        string expectedStackSignature,
        int quantity,
        Vector2Int pickupStandPosition,
        WorldItemHaulDestinationKind destinationKind,
        string destinationId,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition,
        Vector2Int penPosition)
    {
        WildlifeId = wildlifeId ?? string.Empty;
        Phase = phase;
        NextDecisionAt = nextDecisionAt;
        OperationId = operationId ?? string.Empty;
        LeaseId = leaseId ?? string.Empty;
        SourceStackId = sourceStackId ?? string.Empty;
        CargoStackId = cargoStackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        ExpectedStackSignature = expectedStackSignature ?? string.Empty;
        Quantity = quantity;
        PickupStandPosition = pickupStandPosition;
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
        DeliveryPosition = deliveryPosition;
        DropPosition = dropPosition;
        PenPosition = penPosition;
    }

    public string WildlifeId { get; }
    public CapturedWildlifeHaulPhase Phase { get; }
    public float NextDecisionAt { get; }
    public string OperationId { get; }
    public string LeaseId { get; }
    public string SourceStackId { get; }
    public string CargoStackId { get; }
    public string ItemId { get; }
    public string ExpectedStackSignature { get; }
    public int Quantity { get; }
    public Vector2Int PickupStandPosition { get; }
    public WorldItemHaulDestinationKind DestinationKind { get; }
    public string DestinationId { get; }
    public Vector2Int DeliveryPosition { get; }
    public Vector2Int DropPosition { get; }
    public Vector2Int PenPosition { get; }

    internal WildlifeHaulAssignmentSnapshot WithWildlife(
        string wildlifeId,
        Vector2Int penPosition) => new(
            wildlifeId,
            Phase,
            NextDecisionAt,
            OperationId,
            LeaseId,
            SourceStackId,
            CargoStackId,
            ItemId,
            ExpectedStackSignature,
            Quantity,
            PickupStandPosition,
            DestinationKind,
            DestinationId,
            DeliveryPosition,
            DropPosition,
            penPosition);

    internal static WildlifeHaulAssignmentSnapshot FromPayload(
        CapturedWildlifeHaulPayloadSnapshot value,
        string wildlifeId,
        Vector2Int penPosition) => new(
            wildlifeId,
            value.Phase,
            value.NextDecisionAt,
            value.OperationId,
            value.LeaseId,
            value.SourceStackId,
            value.CargoStackId,
            value.ItemId,
            value.ExpectedStackSignature,
            value.Quantity,
            value.PickupStandPosition,
            value.DestinationKind switch
            {
                CapturedWildlifeHaulDestinationKind.Warehouse =>
                    WorldItemHaulDestinationKind.Warehouse,
                CapturedWildlifeHaulDestinationKind.FacilityBuffer =>
                    WorldItemHaulDestinationKind.FacilityBuffer,
                _ => throw new System.InvalidOperationException(
                    $"Unknown captured wildlife haul destination '{value.DestinationKind}'.")
            },
            value.DestinationId,
            value.DeliveryPosition,
            value.DropPosition,
            penPosition);
}

public interface IWildlifeHaulRoleQuery
{
    bool TryGetHaul(string wildlifeId, out WildlifeHaulAssignmentSnapshot assignment);
    void CopyHaulAssignments(List<WildlifeHaulAssignmentSnapshot> destination);
}

public interface IWildlifeHaulRoleCommand
{
    bool TryAssignHaul(string wildlifeId, out string failureReason);
    bool TryClearHaul(string wildlifeId, out string failureReason);
}

public interface IWildlifeHaulRoleStateCommand : IWildlifeHaulRoleQuery
{
    bool TryReplaceHaul(
        string wildlifeId,
        CapturedWildlifeHaulPhase expectedPhase,
        string expectedOperationId,
        WildlifeHaulAssignmentSnapshot replacement,
        string status);

    void ClearRecoveredHaul(string wildlifeId, string status);

    void RetireUnavailableHaul(string wildlifeId);
}
