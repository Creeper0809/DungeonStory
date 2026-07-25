using System;

public interface ICharacterAiActionAssetCatalog
{
    AIActionSet GetRequiredAction(string resourcePath, CharacterAiBranch expectedBranch);
}

public sealed class ResourceCharacterAiActionAssetCatalog : ICharacterAiActionAssetCatalog
{
    private readonly IResourcesAssetLoader resourcesAssetLoader;

    public ResourceCharacterAiActionAssetCatalog(IResourcesAssetLoader resourcesAssetLoader)
    {
        this.resourcesAssetLoader = resourcesAssetLoader
            ?? throw new ArgumentNullException(nameof(resourcesAssetLoader));
    }

    public AIActionSet GetRequiredAction(string resourcePath, CharacterAiBranch expectedBranch)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("AI action resource path is required.", nameof(resourcePath));
        }

        AIActionSet actionSet = resourcesAssetLoader.LoadRequired<AIActionSet>(resourcePath);
        if (actionSet.Branch != expectedBranch)
        {
            throw new InvalidOperationException(
                $"Required AI action asset has wrong branch: Resources/{resourcePath} "
                + $"expected={expectedBranch} actual={actionSet.Branch}");
        }

        return actionSet;
    }
}
