using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeSpeciesDefinition
{
    public WildlifeSpeciesDefinition(
        string speciesId,
        string displayName,
        string description,
        Sprite sprite,
        int maxHealth,
        float moveSpeed,
        float fearSensitivity,
        float aggression,
        int retaliationDamage,
        float spawnWeight,
        int herdSize,
        bool canEnterDungeon,
        float carcassWeight,
        IEnumerable<WildlifeButcherYield> butcherYields,
        WildlifeDietType diet = WildlifeDietType.Herbivore,
        IEnumerable<WildlifeHabitatType> preferredHabitats = null,
        float territoryRadius = 6f,
        float dailyFoodNeed = 1f,
        float dailyWaterNeed = 1f,
        float restPreference = 0.5f,
        float predationDrive = 0f,
        float fleePreference = 0.5f,
        WildlifeHusbandryProfile husbandry = null,
        IEnumerable<string> preySpeciesIds = null,
        IEnumerable<string> predatorSpeciesIds = null,
        string nestTag = "",
        Season breedingSeason = Season.Spring,
        string migrationPatternId = "",
        IEnumerable<string> diseaseVectorIds = null,
        IEnumerable<Season> activeSeasons = null)
    {
        SpeciesId = string.IsNullOrWhiteSpace(speciesId)
            ? throw new ArgumentException(
                "An authored wildlife species ID is required.",
                nameof(speciesId))
            : speciesId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException(
                "An authored wildlife display name is required.",
                nameof(displayName))
            : displayName.Trim();
        Description = description?.Trim() ?? string.Empty;
        Sprite = sprite;
        MaxHealth = Mathf.Max(1, maxHealth);
        MoveSpeed = Mathf.Max(0.1f, moveSpeed);
        FearSensitivity = Mathf.Clamp(fearSensitivity, 0f, 2f);
        Aggression = Mathf.Clamp(aggression, 0f, 2f);
        RetaliationDamage = Mathf.Max(0, retaliationDamage);
        SpawnWeight = Mathf.Max(0f, spawnWeight);
        HerdSize = Mathf.Max(1, herdSize);
        CanEnterDungeon = canEnterDungeon;
        CarcassWeight = Mathf.Max(0.1f, carcassWeight);
        Diet = diet;
        TerritoryRadius = Mathf.Clamp(territoryRadius, 2f, 18f);
        DailyFoodNeed = Mathf.Clamp(dailyFoodNeed, 0.1f, 4f);
        DailyWaterNeed = Mathf.Clamp(dailyWaterNeed, 0.1f, 4f);
        RestPreference = Mathf.Clamp01(restPreference);
        PredationDrive = Mathf.Clamp01(predationDrive);
        FleePreference = Mathf.Clamp01(fleePreference);
        Husbandry = husbandry
            ?? throw new ArgumentNullException(nameof(husbandry));
        PreferredHabitats = (preferredHabitats ?? Array.Empty<WildlifeHabitatType>())
            .Distinct()
            .ToArray();
        ButcherYields = (butcherYields ?? Array.Empty<WildlifeButcherYield>())
            .Where(yieldItem => yieldItem != null
                && yieldItem.amount > 0
                && !string.IsNullOrWhiteSpace(yieldItem.itemId))
            .Select(yieldItem => new WildlifeButcherYield
            {
                itemId = yieldItem.itemId.Trim(),
                amount = Mathf.Max(0, yieldItem.amount)
            })
            .ToArray();
        PreySpeciesIds = NormalizeIds(preySpeciesIds);
        PredatorSpeciesIds = NormalizeIds(predatorSpeciesIds);
        NestTag = nestTag?.Trim() ?? string.Empty;
        BreedingSeason = breedingSeason;
        MigrationPatternId = migrationPatternId?.Trim() ?? string.Empty;
        DiseaseVectorIds = NormalizeIds(diseaseVectorIds);
        ActiveSeasons = (activeSeasons ?? Array.Empty<Season>())
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
    }

    public string SpeciesId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public Sprite Sprite { get; }
    public int MaxHealth { get; }
    public float MoveSpeed { get; }
    public float FearSensitivity { get; }
    public float Aggression { get; }
    public int RetaliationDamage { get; }
    public float SpawnWeight { get; }
    public int HerdSize { get; }
    public bool CanEnterDungeon { get; }
    public float CarcassWeight { get; }
    public WildlifeDietType Diet { get; }
    public IReadOnlyList<WildlifeHabitatType> PreferredHabitats { get; }
    public float TerritoryRadius { get; }
    public float DailyFoodNeed { get; }
    public float DailyWaterNeed { get; }
    public float RestPreference { get; }
    public float PredationDrive { get; }
    public float FleePreference { get; }
    public WildlifeHusbandryProfile Husbandry { get; }
    public IReadOnlyList<WildlifeButcherYield> ButcherYields { get; }
    public IReadOnlyList<string> PreySpeciesIds { get; }
    public IReadOnlyList<string> PredatorSpeciesIds { get; }
    public string NestTag { get; }
    public Season BreedingSeason { get; }
    public string MigrationPatternId { get; }
    public IReadOnlyList<string> DiseaseVectorIds { get; }
    public IReadOnlyList<Season> ActiveSeasons { get; }
    public string CarcassItemId => WildlifeItemDefinitions.GetCarcassItemId(SpeciesId);
    public bool IsPredator => Aggression >= 0.75f;
    public bool IsDangerous => RetaliationDamage > 0 || Aggression >= 0.5f;

    public bool IsActiveIn(Season season) =>
        ActiveSeasons.Count == 0 || ActiveSeasons.Contains(season);

    public bool Hunts(string speciesId) =>
        PreySpeciesIds.Count == 0
            ? Diet == WildlifeDietType.Carnivore
            : PreySpeciesIds.Contains(speciesId?.Trim() ?? string.Empty);

    private static IReadOnlyList<string> NormalizeIds(
        IEnumerable<string> values) =>
        (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class WildlifeItemDefinitions
{
    public const string CarcassPrefix = "wild:carcass:";
    public const string HideItemId = "resource:hide";
    public const string FangItemId = "resource:fang";
    public const string RuneDustItemId = "resource:rune-dust";
    public const string RotItemId = "wild:rot";

    public static string GetCarcassItemId(string speciesId)
    {
        string normalized = string.IsNullOrWhiteSpace(speciesId) ? "unknown" : speciesId.Trim();
        return CarcassPrefix + normalized;
    }

    public static bool TryGetSpeciesIdFromCarcass(string itemId, out string speciesId)
    {
        speciesId = GetSpeciesIdFromCarcass(itemId);
        return !string.IsNullOrWhiteSpace(speciesId);
    }

    public static string GetSpeciesIdFromCarcass(string itemId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(CarcassPrefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return normalized.Substring(CarcassPrefix.Length).Trim();
    }
}
