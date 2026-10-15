using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>Immutable source slice captured by the item owner before consumption.</summary>
public readonly struct PhysicalItemDispositionSourceFact
{
    public PhysicalItemDispositionSourceFact(string stackId, string itemDefinitionId,
        string itemInstanceId, int quantity, long massGrams, Vector2Int sourcePosition, string displayName)
    {
        StackId = stackId?.Trim() ?? string.Empty;
        ItemDefinitionId = itemDefinitionId?.Trim() ?? string.Empty;
        ItemInstanceId = itemInstanceId?.Trim() ?? string.Empty;
        Quantity = quantity;
        MassGrams = massGrams;
        SourcePosition = sourcePosition;
        DisplayName = displayName ?? string.Empty;
    }

    public string StackId { get; }
    public string ItemDefinitionId { get; }
    public string ItemInstanceId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public Vector2Int SourcePosition { get; }
    public string DisplayName { get; }
    public bool IsValid => DomainFactValueSyntax.IsIdentity(StackId)
        && DomainFactValueSyntax.IsIdentity(ItemDefinitionId)
        && (string.IsNullOrEmpty(ItemInstanceId) || DomainFactValueSyntax.IsIdentity(ItemInstanceId))
        && Quantity > 0 && MassGrams > 0
        && DomainFactValueSyntax.IsText(DisplayName, DomainFactValueSyntax.MaximumNameCharacters, false);
}
