using System.Collections.Generic;

public interface IItemDefinitionCatalog
{
    IReadOnlyList<ItemDefinitionSO> All { get; }
    bool TryGet(ItemDefinitionId itemId, out ItemDefinitionSO definition);
    ItemDefinitionSO GetRequired(ItemDefinitionId itemId);
    IReadOnlyList<string> Validate();
}
