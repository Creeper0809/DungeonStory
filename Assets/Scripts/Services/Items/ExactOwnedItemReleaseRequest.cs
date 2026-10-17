using UnityEngine;

public readonly struct ExactOwnedItemReleaseRequest
{
    public ExactOwnedItemReleaseRequest(
        string stackId,
        string itemId,
        string itemInstanceId,
        int quantity,
        WorldItemStackState sourceState,
        string ownerDestinationId,
        string ownerDomain,
        string operationId,
        string reasonCode)
    {
        StackId = stackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        Quantity = quantity;
        SourceState = sourceState;
        OwnerDestinationId = ownerDestinationId ?? string.Empty;
        OwnerDomain = ownerDomain ?? string.Empty;
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
    }

    public string StackId { get; }
    public string ItemId { get; }
    public string ItemInstanceId { get; }
    public int Quantity { get; }
    public WorldItemStackState SourceState { get; }
    public string OwnerDestinationId { get; }
    public string OwnerDomain { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
}
