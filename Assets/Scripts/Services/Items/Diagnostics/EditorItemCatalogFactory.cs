#if UNITY_EDITOR
public static class EditorItemCatalogFactory
{
    public static ResourceDungeonItemCatalogProvider Create()
    {
        ResourceGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        return new ResourceDungeonItemCatalogProvider(
            new ResourceItemDefinitionCatalog(content));
    }
}
#endif
