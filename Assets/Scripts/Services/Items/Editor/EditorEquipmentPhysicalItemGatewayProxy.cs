using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class EditorEquipmentPhysicalItemGatewayProxy :
    IEquipmentPhysicalItemGateway
{
    private IEquipmentPhysicalItemGateway target;

    public bool FailNextAcknowledgement { get; set; }
    public int AcknowledgementAttempts { get; private set; }
    public int SuccessfulAcknowledgements { get; private set; }

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

    public bool SpawnItemAtWithComponents(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned) => Target.SpawnItemAtWithComponents(
            itemId,
            amount,
            position,
            state,
            destinationId,
            components,
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

    public bool TryCommitBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => Target.TryCommitBatchPhysicalDisposition(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

    public bool TryCommitPendingBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) => Target.TryCommitPendingBatchPhysicalDisposition(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

    public bool TryGetPendingBatchPhysicalDisposition(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt) =>
        Target.TryGetPendingBatchPhysicalDisposition(
            operationId,
            out receipt);

    public bool AcknowledgeBatchPhysicalDisposition(
        string commitId,
        out string failureReason)
    {
        AcknowledgementAttempts++;
        if (FailNextAcknowledgement)
        {
            FailNextAcknowledgement = false;
            failureReason = "Injected editor appraisal acknowledgement failure.";
            return false;
        }

        bool acknowledged = Target.AcknowledgeBatchPhysicalDisposition(
            commitId,
            out failureReason);
        if (acknowledged)
        {
            SuccessfulAcknowledgements++;
        }
        return acknowledged;
    }

    public bool TrySetInstanceComponent(
        string stackId,
        ItemInstanceComponentSaveData component) =>
        Target.TrySetInstanceComponent(stackId, component);

    public bool TryRemoveInstanceComponent(
        string stackId,
        string componentTypeId) => Target.TryRemoveInstanceComponent(
            stackId,
            componentTypeId);

    public int ReleaseStacksByDestination(
        string destinationId,
        Vector2Int releasePosition) =>
        Target.ReleaseStacksByDestination(destinationId, releasePosition);
}
