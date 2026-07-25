using System;
using System.Collections.Generic;

public interface IDataScriptableObjectSource
{
    IReadOnlyCollection<DataScriptableObject> LoadAll();
}

public sealed class ResourceDataScriptableObjectSource : IDataScriptableObjectSource
{
    private const string DataRootPath = "SO";

    private readonly IResourcesAssetLoader resourcesAssetLoader;

    public ResourceDataScriptableObjectSource(IResourcesAssetLoader resourcesAssetLoader)
    {
        this.resourcesAssetLoader = resourcesAssetLoader
            ?? throw new ArgumentNullException(nameof(resourcesAssetLoader));
    }

    public IReadOnlyCollection<DataScriptableObject> LoadAll()
    {
        return resourcesAssetLoader.LoadAllRequired<DataScriptableObject>(DataRootPath);
    }
}
