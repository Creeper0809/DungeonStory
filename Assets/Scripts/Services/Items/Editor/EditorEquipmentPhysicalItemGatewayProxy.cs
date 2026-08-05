using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class EditorEquipmentPhysicalItemGatewayProxy :
    IEquipmentPhysicalItemGateway
{
    private IEquipmentPhysicalItemGateway target;

    public void Attach(IEquipmentPhysicalItemGateway target)
    {
        this.target = target ?? throw new ArgumentNullException(nameof(target));
    }

    private IEquipmentPhysicalItemGateway Target => target
        ?? throw new InvalidOperationException(
            "The editor physical-item gateway proxy has not been attached.");

    public bool SpawnItemAt(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned) => Target.SpawnItemAt(
            itemId,
            amount,
            position,
            state,
            destinationId,
            out spawned);

    public bool SpawnExistingUniqueItemAt(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId) => Target.SpawnExistingUniqueItemAt(
            itemId,
            itemInstanceId,
            position,
            state,
            destinationId,
            out stackId);

    public bool TryAbsorbUniqueItemStack(
        string stackId,
        ItemInstanceId expectedInstanceId) =>
        Target.TryAbsorbUniqueItemStack(stackId, expectedInstanceId);

    public bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason) => Target.TryRequestItemDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
        Target.GetAllStacks();

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason) => Target.TryConsumeFacilityItemBuffer(
            destinationId,
            costs,
            out failureReason);

    public bool DeleteStack(string stackId) => Target.DeleteStack(stackId);

    public bool TryConsumeStackQuantity(
        string stackId,
        int quantity,
        out WorldItemStackSnapshot consumed) =>
        Target.TryConsumeStackQuantity(stackId, quantity, out consumed);

    public bool TrySetInstanceComponent(
        string stackId,
        ItemInstanceComponentSaveData component) =>
        Target.TrySetInstanceComponent(stackId, component);

    public int ReleaseStacksByDestination(
        string destinationId,
        Vector2Int releasePosition) =>
        Target.ReleaseStacksByDestination(destinationId, releasePosition);
}
