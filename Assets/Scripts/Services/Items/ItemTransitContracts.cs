using UnityEngine;

public readonly struct ItemTransitDestination
{
    public ItemTransitDestination(
        WorldItemStackState state,
        Vector2Int position,
        string destinationId)
    {
        State = state;
        Position = position;
        DestinationId = destinationId?.Trim() ?? string.Empty;
    }

    public WorldItemStackState State { get; }
    public Vector2Int Position { get; }
    public string DestinationId { get; }
}

public readonly struct ItemTransitStackSnapshot
{
    public ItemTransitStackSnapshot(
        ItemStackId stackId,
        string itemId,
        int quantity,
        bool forbidden,
        float contamination)
    {
        StackId = stackId;
        ItemId = itemId ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
        Forbidden = forbidden;
        Contamination = Mathf.Clamp(contamination, 0f, 100f);
    }

    public ItemStackId StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public bool Forbidden { get; }
    public float Contamination { get; }
    public bool IsValid => StackId.IsValid && Quantity > 0;
}
