using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

public interface IItemDefinitionCatalog
{
    IReadOnlyList<ItemDefinitionSO> All { get; }
    bool TryGet(ItemDefinitionId itemId, out ItemDefinitionSO definition);
    ItemDefinitionSO GetRequired(ItemDefinitionId itemId);
    IReadOnlyList<string> Validate();
}

/// <summary>
/// The single authoritative item-definition index. Domain catalogs are read-only projections
/// over this index; they must not invent items when an ID is missing.
/// </summary>
public sealed class ResourceItemDefinitionCatalog : IItemDefinitionCatalog
{
    private readonly IReadOnlyList<ItemDefinitionSO> all;
    private readonly IReadOnlyDictionary<string, ItemDefinitionSO> byId;
    private readonly IReadOnlyList<string> validationErrors;

    [Inject]
    public ResourceItemDefinitionCatalog(IGameContentCatalog content)
        : this(content?.Items?.Definitions)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
    }

    public ResourceItemDefinitionCatalog(IEnumerable<ItemDefinitionSO> definitions)
    {
        all = (definitions ?? Array.Empty<ItemDefinitionSO>())
            .Where(definition => definition != null)
            .Distinct()
            .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
            .ToArray();

        List<string> errors = new();
        Dictionary<string, ItemDefinitionSO> index = new(StringComparer.Ordinal);
        foreach (ItemDefinitionSO definition in all)
        {
            foreach (string error in definition.ValidateDefinition())
            {
                errors.Add($"{definition.name}: {error}");
            }

            if (!definition.StableId.IsValid)
            {
                continue;
            }

            if (!index.TryAdd(definition.ItemId, definition))
            {
                errors.Add($"Duplicate item ID '{definition.ItemId}' on '{definition.name}'.");
            }
        }

        byId = index;
        validationErrors = errors;
    }

    public IReadOnlyList<ItemDefinitionSO> All => all;

    public bool TryGet(ItemDefinitionId itemId, out ItemDefinitionSO definition) =>
        byId.TryGetValue(itemId.Value, out definition);

    public ItemDefinitionSO GetRequired(ItemDefinitionId itemId)
    {
        if (!itemId.IsValid)
        {
            throw new ArgumentException("A valid item ID is required.", nameof(itemId));
        }

        return TryGet(itemId, out ItemDefinitionSO definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown item definition '{itemId.Value}'.");
    }

    public IReadOnlyList<string> Validate() => validationErrors;
}
