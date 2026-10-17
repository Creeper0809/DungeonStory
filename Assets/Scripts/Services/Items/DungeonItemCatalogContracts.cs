using System.Collections.Generic;

public interface IDungeonItemCatalogProvider
{
    IReadOnlyList<DungeonItemDefinition> All { get; }
    DungeonItemDefinition GetDefinition(string itemId);
    bool TryGetDefinition(string itemId, out DungeonItemDefinition definition);
}
