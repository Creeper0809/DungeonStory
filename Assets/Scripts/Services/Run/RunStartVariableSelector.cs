using System;
using System.Linq;
using UnityEngine;

public static class RunStrategyBlueprintIds
{
    public const int CommerceBasics = 6101;
    public const int FortressBasics = 6102;
    public const int ArcaneBasics = 6104;

    public static int ResolveForSpecies(string speciesTag)
    {
        if (string.Equals(speciesTag, "Slime", StringComparison.OrdinalIgnoreCase))
        {
            return CommerceBasics;
        }

        if (string.Equals(speciesTag, "Orc", StringComparison.OrdinalIgnoreCase))
        {
            return FortressBasics;
        }

        if (string.Equals(speciesTag, "Vampire", StringComparison.OrdinalIgnoreCase))
        {
            return ArcaneBasics;
        }

        return -1;
    }
}

public interface IRunStartVariableSelector
{
    RunStartVariableSnapshot Create(
        int seed,
        CharacterSO ownerData,
        DungeonDifficulty difficulty);
}

public sealed class RunStartVariableSelector : IRunStartVariableSelector
{
    private readonly IRunStartVariableCatalog catalog;
    private readonly IMetaProgressionRuntimeReader metaProgressionReader;

    public RunStartVariableSelector(
        IRunStartVariableCatalog catalog,
        IMetaProgressionRuntimeReader metaProgressionReader)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.metaProgressionReader = metaProgressionReader
            ?? throw new ArgumentNullException(nameof(metaProgressionReader));
    }

    public RunStartVariableSnapshot Create(
        int seed,
        CharacterSO ownerData,
        DungeonDifficulty difficulty)
    {
        System.Random startRandom = new System.Random(seed);
        int startingFacilityCount = 3 + metaProgressionReader.GetStartingFacilityCandidateBonus();
        BuildingSO[] buildings = catalog
            .Buildings
            .Where((building) => building != null
                && !building.IsGridMovement
                && !building.IsWall
                && FacilityShopService.GetBuildingStar(building) <= 1)
            .OrderBy((_) => startRandom.NextDouble())
            .Take(Mathf.Max(1, startingFacilityCount))
            .ToArray();
        CharacterSO[] customers = catalog
            .Characters
            .Where((character) => character != null && character.characterType == CharacterType.Customer)
            .OrderBy((_) => startRandom.NextDouble())
            .Take(3)
            .ToArray();
        FacilityBlueprintSO[] blueprintPool = catalog
            .Blueprints
            .Where((blueprint) => blueprint != null
                && blueprint.rarity == FacilityShopRarity.Common)
            .ToArray();
        int strategyBlueprintId = RunStrategyBlueprintIds.ResolveForSpecies(ownerData?.SpeciesTag);
        FacilityBlueprintSO strategyBlueprint = blueprintPool
            .FirstOrDefault((blueprint) => blueprint.id == strategyBlueprintId);
        FacilityBlueprintSO[] randomBlueprints = blueprintPool
            .Where((blueprint) => blueprint != strategyBlueprint)
            .OrderBy((_) => startRandom.NextDouble())
            .Take(strategyBlueprint != null ? 1 : 2)
            .ToArray();
        FacilityBlueprintSO[] blueprints = strategyBlueprint != null
            ? new[] { strategyBlueprint }.Concat(randomBlueprints).ToArray()
            : randomBlueprints;

        return new RunStartVariableSnapshot(
            seed,
            !string.IsNullOrWhiteSpace(ownerData?.SpeciesTag) ? ownerData.SpeciesTag : "Unknown",
            difficulty,
            buildings.Select((building) => building.id).DefaultIfEmpty(-1).Where((id) => id >= 0).ToArray(),
            customers.Select((customer) => customer.SpeciesTag).Where((tag) => !string.IsNullOrWhiteSpace(tag)).Distinct().ToArray(),
            blueprints.Select((blueprint) => blueprint.id).DefaultIfEmpty(-1).Where((id) => id >= 0).ToArray(),
            seed ^ 0x5F3759DF,
            ResolveLayoutId(ownerData, startRandom),
            difficulty switch
            {
                DungeonDifficulty.Easy => 0.85f,
                DungeonDifficulty.Hard => 1.2f,
                _ => 1f
            },
            OwnerDoctrineCatalog.ResolveFor(ownerData)?.id);
    }

    private static string ResolveLayoutId(CharacterSO ownerData, System.Random startRandom)
    {
        string species = !string.IsNullOrWhiteSpace(ownerData?.SpeciesTag) ? ownerData.SpeciesTag : string.Empty;
        if (species.Equals("Slime", StringComparison.OrdinalIgnoreCase))
        {
            return "wet-front";
        }

        if (species.Equals("Orc", StringComparison.OrdinalIgnoreCase))
        {
            return "training-front";
        }

        if (species.Equals("Vampire", StringComparison.OrdinalIgnoreCase))
        {
            return "research-front";
        }

        string[] defaultLayouts =
        {
            "compact-shop",
            "split-hall",
            "stairs-first"
        };
        return defaultLayouts[startRandom.Next(defaultLayouts.Length)];
    }
}
