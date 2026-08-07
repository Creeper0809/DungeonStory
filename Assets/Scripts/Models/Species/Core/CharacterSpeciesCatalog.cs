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
    ICharacterSpeciesEnvironmentCatalog,
    ICharacterLifeDefinitionCatalog,
    IReproductionDefinitionCatalog
{
    public const string ResourcePath = "SO/Character/Species";

    private readonly IReadOnlyList<CharacterSpeciesSO> all;
    private readonly IReadOnlyDictionary<string, CharacterSpeciesSO> byTag;
    private readonly IReadOnlyList<AgeConditionDefinition> biologicalAgeConditions;
    private readonly IReadOnlyList<AgeConditionDefinition> constructAgeConditions;
    private readonly IReadOnlyDictionary<string, AgeConditionDefinition> ageConditionsById;

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
        AgeConditionDefinition[] ageConditions = content
            .GetAll<AgeConditionDefinitionSO>()
            .Select(ToDefinition)
            .OrderBy(value => value.ConditionId, StringComparer.Ordinal)
            .ToArray();
        biologicalAgeConditions = ageConditions
            .Where(value => !value.ConstructCondition)
            .ToArray();
        constructAgeConditions = ageConditions
            .Where(value => value.ConstructCondition)
            .ToArray();
        ageConditionsById = ageConditions.ToDictionary(
            value => value.ConditionId,
            StringComparer.Ordinal);
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

    public SpeciesLifeHistoryDefinition RequireLifeHistory(
        CharacterSpeciesId speciesId)
    {
        if (!TryGet(speciesId, out CharacterSpeciesSO species)
            || species.lifeHistory == null)
        {
            throw new KeyNotFoundException(
                $"Unknown authored species life history '{speciesId.Value}'.");
        }

        SpeciesLifeHistorySO source = species.lifeHistory;
        return new SpeciesLifeHistoryDefinition(
            source.SpeciesId,
            source.infantEndAgeYears,
            source.adolescentStartAgeYears,
            source.adultAgeYears,
            source.elderAgeYears,
            source.untreatedExpectedLifeYears,
            source.construct);
    }

    public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(bool construct) =>
        construct ? constructAgeConditions : biologicalAgeConditions;

    public AgeConditionDefinition RequireAgeCondition(string conditionId)
    {
        string normalized = conditionId?.Trim() ?? string.Empty;
        return ageConditionsById.TryGetValue(
                normalized,
                out AgeConditionDefinition definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Unknown authored age condition '{normalized}'.");
    }

    public ReproductionDefinition RequireReproduction(
        CharacterSpeciesId speciesId)
    {
        if (!TryGet(speciesId, out CharacterSpeciesSO species)
            || species.reproduction == null)
        {
            throw new KeyNotFoundException(
                $"Unknown authored reproduction profile '{speciesId.Value}'.");
        }

        ReproductionProfileSO source = species.reproduction;
        return new ReproductionDefinition(
            source.SpeciesId,
            source.mode,
            source.baseSuccessChance,
            source.viableTemperatureMinimum,
            source.viableTemperatureMaximum,
            source.phases.Select(value => new ReproductionPhaseDefinition
            {
                phase = value.phase,
                durationDays = value.durationDays
            }).ToArray());
    }

    private static AgeConditionDefinition ToDefinition(AgeConditionDefinitionSO source)
    {
        if (source == null || source.ValidateDefinition().Count > 0)
        {
            throw new InvalidOperationException(
                "The root catalog contains an invalid age-condition definition.");
        }

        return new AgeConditionDefinition(
            source.conditionId,
            source.constructCondition,
            source.affectedAnatomyNodeIds
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }
}
