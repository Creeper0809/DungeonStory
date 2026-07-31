using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IRunCharacterCatalog
{
    IReadOnlyCollection<CharacterSO> Characters { get; }
}

public interface IOwnerCandidateCatalog
{
    IReadOnlyCollection<CharacterSO> OwnerCandidates { get; }
}

public interface IRunStartVariableCatalog
{
    IReadOnlyCollection<BuildingSO> Buildings { get; }
    IReadOnlyCollection<CharacterSO> Characters { get; }
    IReadOnlyCollection<FacilityBlueprintSO> Blueprints { get; }
}

public sealed class ResourceRunCharacterCatalog : IRunCharacterCatalog
{
    private const string CharacterRootPath = "SO/Character";

    private readonly IResourcesAssetLoader resourcesAssetLoader;
    private CharacterSO[] characters;

    public ResourceRunCharacterCatalog(IResourcesAssetLoader resourcesAssetLoader)
    {
        this.resourcesAssetLoader = resourcesAssetLoader
            ?? throw new ArgumentNullException(nameof(resourcesAssetLoader));
    }

    public IReadOnlyCollection<CharacterSO> Characters
    {
        get
        {
            characters ??= MergeExpandedSpeciesTemplates(
                resourcesAssetLoader
                    .LoadAllRequired<CharacterSO>(CharacterRootPath));

            return characters;
        }
    }

    private static CharacterSO[] MergeExpandedSpeciesTemplates(
        IEnumerable<CharacterSO> loaded)
    {
        List<CharacterSO> values = (loaded ?? Array.Empty<CharacterSO>())
            .Where(value => value != null)
            .ToList();
        Sprite fallbackSprite = values
            .Select(value => value.characterSprite)
            .FirstOrDefault(value => value != null);
        string[] tags =
        {
            "Beastkin",
            "Demon",
            "Kobold",
            "Myconid",
            "Harpy",
            "Golem"
        };
        foreach (string tag in tags)
        {
            if (values.Any(value => string.Equals(
                    value.SpeciesTag,
                    tag,
                    StringComparison.OrdinalIgnoreCase))
                || !CharacterSpeciesResourceLookup.TryGet(
                    tag,
                    out CharacterSpeciesSO species))
            {
                continue;
            }

            CharacterSO generated = UnityEngine.ScriptableObject
                .CreateInstance<CharacterSO>();
            generated.name = $"RuntimeCustomer_{tag}";
            generated.characterType = CharacterType.Customer;
            generated.role = CharacterRole.Regular;
            generated.id = 9000 + species.id;
            generated.characterName = species.displayName;
            generated.speciesTag = tag;
            generated.species = species;
            generated.baseStats = CharacterStatBlock.CreateDefault();
            generated.traits = Array.Empty<CharacterTraitSO>();
            generated.defaultWorkPriorities =
                WorkPriorityProfile.CreateDefault();
            foreach (string workTypeId in species.strongWorkTypeIds
                         ?? Array.Empty<string>())
            {
                WorkTypeId id = new WorkTypeId(workTypeId);
                if (id.IsValid)
                {
                    generated.defaultWorkPriorities.SetPriority(
                        id,
                        WorkPriorityLevel.Priority1);
                }
            }
            generated.aiPersonality = new CharacterAiPersonality();
            generated.characterSprite = fallbackSprite;
            generated.ConfigureGeneratedVisitProfile(
                1,
                3,
                tag == "Demon" ? 300 : 80,
                tag == "Demon" ? 650 : 320,
                tag is "Beastkin" or "Harpy" ? 5 : 4);
            values.Add(generated);
        }

        return values
            .OrderBy(value => value.id)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class ResourceOwnerCandidateCatalog : IOwnerCandidateCatalog
{
    private readonly IRunCharacterCatalog characterCatalog;

    public ResourceOwnerCandidateCatalog(IRunCharacterCatalog characterCatalog)
    {
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
    }

    public IReadOnlyCollection<CharacterSO> OwnerCandidates => characterCatalog
        .Characters
        .Where((candidate) => candidate != null && candidate.IsOwnerCandidate)
        .OrderBy((candidate) => candidate.id)
        .ToArray();
}

public sealed class RunStartVariableCatalog : IRunStartVariableCatalog
{
    private readonly IDataCatalog catalog;
    private readonly IRunCharacterCatalog characterCatalog;

    public RunStartVariableCatalog(IDataCatalog catalog, IRunCharacterCatalog characterCatalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
    }

    public IReadOnlyCollection<BuildingSO> Buildings => catalog
        .GetData<BuildingSO>()
        .Values
        .Where((building) => building != null)
        .ToArray();

    public IReadOnlyCollection<CharacterSO> Characters => characterCatalog.Characters;

    public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => catalog
        .GetData<FacilityBlueprintSO>()
        .Values
        .Where((blueprint) => blueprint != null)
        .ToArray();
}
