#if UNITY_EDITOR
using UnityEngine;

public static class EditorItemCatalogFactory
{
    public static ResourceDungeonItemCatalogProvider Create()
    {
        ItemDefinitionSO[] definitions = Resources.LoadAll<ItemDefinitionSO>(
            ItemDefinitionSO.UnifiedResourcePath);
        return new ResourceDungeonItemCatalogProvider(
            new ResourceItemDefinitionCatalog(definitions));
    }
}
#endif
