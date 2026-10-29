#if UNITY_EDITOR
public static class EditorItemCatalogFactory
{
    public static ResourceDungeonItemCatalogProvider Create()
        => new ResourceDungeonItemCatalogProvider(CreateDefinitions());

    public static ResourceItemDefinitionCatalog CreateDefinitions()
    {
        ResourceGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        return new ResourceItemDefinitionCatalog(content);
    }
}
#endif
