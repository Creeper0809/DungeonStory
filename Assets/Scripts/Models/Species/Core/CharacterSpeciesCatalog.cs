using System;
using System.Collections.Generic;
using System.Linq;

public static class CharacterSpeciesCatalogRequirements
{
    public static IReadOnlyList<CharacterSpeciesSO> Normalize(
        IEnumerable<CharacterSpeciesSO> authored)
    {
        return CharacterSpeciesDefinitionCatalogRequirements.Normalize(authored);
    }
}

public interface ICharacterSpeciesCatalog
{
    IReadOnlyList<CharacterSpeciesSO> All { get; }
    bool TryGet(CharacterSpeciesId speciesId, out CharacterSpeciesSO species);
}

public sealed class ResourceCharacterSpeciesCatalog :
    ICharacterSpeciesCatalog,
    ICharacterSpeciesDefinitionCatalog,
    ICharacterSpeciesEnvironmentCatalog
{
    public const string ResourcePath = "SO/Character/Species";

    private readonly IReadOnlyList<CharacterSpeciesSO> all;
    private readonly IReadOnlyDictionary<string, CharacterSpeciesSO> byTag;

    public ResourceCharacterSpeciesCatalog(IGameContentDefinitionSource content)
    {
        all = CharacterSpeciesCatalogRequirements.Normalize(
            (content ?? throw new ArgumentNullException(nameof(content)))
                .GetAll<CharacterSpeciesSO>());
        byTag = all
            .Where(value => !string.IsNullOrWhiteSpace(value.speciesTag))
            .GroupBy(value => value.speciesTag.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CharacterSpeciesSO> All => all;
    public IReadOnlyList<CharacterSpeciesDefinitionSO> Definitions => all;

    public bool TryGet(CharacterSpeciesId speciesId, out CharacterSpeciesSO species)
    {
        return byTag.TryGetValue(speciesId.Value, out species);
    }

    public bool TryGetDefinition(
        CharacterSpeciesId speciesId,
        out CharacterSpeciesDefinitionSO species)
    {
        bool found = TryGet(speciesId, out CharacterSpeciesSO authored);
        species = authored;
        return found;
    }

    public bool TryGetThermalProfile(
        CharacterSpeciesId speciesId,
        out SpeciesThermalProfile profile)
    {
        if (TryGet(speciesId, out CharacterSpeciesSO species))
        {
            profile = species.environment.ToThermalProfile();
            return true;
        }

        profile = default;
        return false;
    }

    public SpeciesThermalProfile GetRequiredThermalProfile(
        CharacterSpeciesId speciesId)
    {
        return TryGetThermalProfile(speciesId, out SpeciesThermalProfile profile)
            ? profile
            : throw new KeyNotFoundException(
                $"Unknown authored character species '{speciesId.Value}'.");
    }
}
