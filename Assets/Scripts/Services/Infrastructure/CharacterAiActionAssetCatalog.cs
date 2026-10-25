using System;
using System.Linq;

public interface ICharacterAiActionAssetCatalog
{
    T GetRequiredAction<T>(CharacterAiBranch expectedBranch)
        where T : AIActionSet;
}

public sealed class ResourceCharacterAiActionAssetCatalog : ICharacterAiActionAssetCatalog
{
    private readonly IGameContentCatalog content;

    public ResourceCharacterAiActionAssetCatalog(IGameContentCatalog content)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public T GetRequiredAction<T>(CharacterAiBranch expectedBranch)
        where T : AIActionSet
    {
        Type requiredType = typeof(T);
        if (requiredType.IsAbstract)
        {
            throw new ArgumentException(
                $"Required AI action type must be concrete; received {requiredType.Name}.",
                nameof(T));
        }
        if (expectedBranch == CharacterAiBranch.None
            || !Enum.IsDefined(typeof(CharacterAiBranch), expectedBranch))
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedBranch),
                expectedBranch,
                "Required AI action branch must be a declared non-None branch.");
        }

        T[] registered = content.GetAll<T>()
            .Where(candidate => candidate != null
                && candidate.GetType() == requiredType)
            .ToArray();
        T[] branchMatches = registered
            .Where(candidate => candidate.Branch == expectedBranch)
            .ToArray();
        if (registered.Length == 0)
        {
            throw new InvalidOperationException(
                $"Required AI action type {requiredType.Name} is not registered in the root content catalog.");
        }
        if (branchMatches.Length == 0)
        {
            string actualBranches = string.Join(
                ", ",
                registered.Select(candidate => candidate.Branch)
                    .Distinct()
                    .OrderBy(branch => branch));
            throw new InvalidOperationException(
                $"Required AI action type {requiredType.Name} has wrong branch; "
                + $"expected={expectedBranch} registered=[{actualBranches}].");
        }

        T[] requiredDefaults = branchMatches
            .Where(candidate => candidate.CompositionRole
                == CharacterAiActionCompositionRole.RequiredDefault)
            .ToArray();
        if (requiredDefaults.Length != 1)
        {
            throw new InvalidOperationException(
                $"Required AI action type {requiredType.Name} with branch {expectedBranch} "
                + "must have exactly one RequiredDefault in the root content catalog; "
                + $"found {requiredDefaults.Length} among {branchMatches.Length} matching assets.");
        }

        return requiredDefaults[0];
    }
}
