using System;
using UnityEngine;

public enum EvolutionCatalystDropOrigin { Expedition, Defense }

public readonly struct EvolutionCatalystDroppedEvent
{
    public EvolutionCatalystDroppedEvent(string itemId, int quantity, Vector2Int position,
        EvolutionCatalystDropOrigin origin)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0
            || !Enum.IsDefined(typeof(EvolutionCatalystDropOrigin), origin))
            throw new ArgumentException("A catalyst drop requires its actual item, quantity, position and origin.");
        ItemId = itemId;
        Quantity = quantity;
        Position = position;
        Origin = origin;
    }
    public string ItemId { get; }
    public int Quantity { get; }
    public Vector2Int Position { get; }
    public EvolutionCatalystDropOrigin Origin { get; }
}
