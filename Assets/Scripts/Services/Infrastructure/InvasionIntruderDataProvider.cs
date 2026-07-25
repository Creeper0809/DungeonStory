using System;

public sealed class ResourceInvasionIntruderDataProvider : IInvasionIntruderDataProvider
{
    private const string DefaultIntruderPath = "SO/Character/Intruders/Intruder_Breakthrough";

    private readonly IResourcesAssetLoader resourcesAssetLoader;

    public ResourceInvasionIntruderDataProvider(IResourcesAssetLoader resourcesAssetLoader)
    {
        this.resourcesAssetLoader = resourcesAssetLoader
            ?? throw new ArgumentNullException(nameof(resourcesAssetLoader));
    }

    public CharacterSO GetRequiredIntruderData(CharacterSO configuredData)
    {
        if (configuredData != null)
        {
            return configuredData;
        }

        return resourcesAssetLoader.LoadRequired<CharacterSO>(DefaultIntruderPath);
    }
}
