using System;
using System.IO;
using System.Linq;

public interface ICharacterAiActionAssetCatalog
{
    AIActionSet GetRequiredAction(string resourcePath, CharacterAiBranch expectedBranch);
}

public sealed class ResourceCharacterAiActionAssetCatalog : ICharacterAiActionAssetCatalog
{
    private readonly IGameContentCatalog content;

    public ResourceCharacterAiActionAssetCatalog(IGameContentCatalog content)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public AIActionSet GetRequiredAction(string resourcePath, CharacterAiBranch expectedBranch)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("AI action resource path is required.", nameof(resourcePath));
        }

        string assetName = Path.GetFileName(resourcePath.Trim());
        AIActionSet[] matches = content.GetAll<AIActionSet>()
            .Where(candidate => candidate != null
                && string.Equals(candidate.name, assetName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Required AI action '{assetName}' must occur exactly once in the root content catalog; found {matches.Length}.");
        }

        AIActionSet actionSet = matches[0];
        if (actionSet.Branch != expectedBranch)
        {
            throw new InvalidOperationException(
                $"Required AI action asset has wrong branch: Resources/{resourcePath} "
                + $"expected={expectedBranch} actual={actionSet.Branch}");
        }

        return actionSet;
    }
}
