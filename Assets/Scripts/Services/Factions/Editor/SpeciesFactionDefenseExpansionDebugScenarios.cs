#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpeciesFactionDefenseExpansionDebugScenarios
{
    private static readonly string[] NewSpecies =
    {
        "Beastkin",
        "Demon",
        "Kobold",
        "Myconid",
        "Harpy",
        "Golem"
    };

    private static readonly string[] RequiredAnatomies =
    {
        "anatomy:humanoid",
        "anatomy:quadruped",
        "anatomy:slime",
        "anatomy:fungal",
        "anatomy:avian",
        "anatomy:construct"
    };

    private static readonly int[] NewDefenseBuildingIds =
    {
        1800, 1801, 1802, 1803, 1804, 1805
    };

    private static readonly string[] NewResearchIds =
    {
        "research:defense:supply",
        "research:defense:corridor-mechanisms",
        "research:defense:rune-identification",
        "research:defense:remote-control",
        "research:defense:siege-fortification",
        "research:defense:alliance-signals"
    };

    [MenuItem(
        "DungeonStory/Debug/Expansion/Build And Validate Species Factions Defense")]
    public static void BuildAndValidate()
    {
        CharacterSpeciesExpansionAssetBuilder.BuildAll();
        DungeonFactionAssetBuilder.BuildAll();
        SurgeryContentAssetBuilder.RebuildAll();
        P1DefenseFacilityAssetBuilder.EnsureP1DefenseAssets();
        ResearchProjectAssetBuilder.Rebuild();
        ValidateOnly();
    }

    [MenuItem(
        "DungeonStory/Debug/Expansion/Validate Species Factions Defense")]
    public static void ValidateOnly()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        List<string> errors = new List<string>();
        ValidateSpecies(errors);
        ValidateAnatomy(errors);
        ValidateFactions(errors);
        ValidateHumanBranches(errors);
        ValidateDefense(errors);
        ValidateResearch(errors);

        if (errors.Count > 0)
        {
            string report = string.Join(Environment.NewLine, errors);
            Debug.LogError(
                "Species/faction/defense expansion validation failed:"
                + Environment.NewLine
                + report);
            throw new InvalidOperationException(report);
        }

        Debug.Log(
            "Species/faction/defense expansion validation passed: "
            + "9 species, 6 dungeon factions, 5 human branches, "
            + "19 defense facilities, 135 research projects.");
    }

    private static void ValidateSpecies(ICollection<string> errors)
    {
        CharacterSpeciesSO[] species = FindAssets<CharacterSpeciesSO>(
            "Assets/Resources/SO/Character/Species");
        if (species.Length != 9)
        {
            errors.Add($"Expected 9 species assets, found {species.Length}.");
        }

        Dictionary<string, CharacterSpeciesSO> byTag = species
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.speciesTag))
            .GroupBy(value => value.speciesTag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        if (byTag.Count != 9)
        {
            errors.Add($"Expected 9 unique species, found {byTag.Count}.");
        }

        int selectable = byTag.Values.Count(value => value.ownerSelectable);
        if (selectable != 3)
        {
            errors.Add($"Expected 3 owner-selectable species, found {selectable}.");
        }

        foreach (string tag in NewSpecies)
        {
            if (!byTag.TryGetValue(tag, out CharacterSpeciesSO value))
            {
                errors.Add($"Missing species '{tag}'.");
                continue;
            }

            if (value.ownerSelectable)
                errors.Add($"{tag} must remain NPC-only in phase one.");
            if (string.IsNullOrWhiteSpace(value.homeFactionId))
                errors.Add($"{tag} has no home faction.");
            if (string.IsNullOrWhiteSpace(value.anatomyProfileId))
                errors.Add($"{tag} has no anatomy profile.");
            if (string.IsNullOrWhiteSpace(value.IncidentId))
                errors.Add($"{tag} has no stable incident ID.");
            if (value.preferredFacilityLabels == null
                || value.preferredFacilityLabels.Length < 3)
                errors.Add($"{tag} needs three preferred facilities.");
            if (value.dislikedEnvironmentLabels == null
                || value.dislikedEnvironmentLabels.Length < 3)
                errors.Add($"{tag} needs three disliked environments.");
            if (value.strongWorkTypeIds == null
                || value.strongWorkTypeIds.Length < 2)
                errors.Add($"{tag} needs at least two work strengths.");
            if (value.defenseAffinityTags == null
                || value.defenseAffinityTags.Length == 0)
                errors.Add($"{tag} has no defense affinity.");
        }
    }

    private static void ValidateAnatomy(ICollection<string> errors)
    {
        AnatomyProfileSO[] profiles = FindAssets<AnatomyProfileSO>(
            "Assets/Resources/SO/Medical/Anatomy");
        if (profiles.Length != 6)
        {
            errors.Add(
                $"Expected 6 anatomy profile assets, found {profiles.Length}.");
        }

        HashSet<string> ids = profiles
            .Where(value => value != null)
            .Select(value => value.ProfileId)
            .ToHashSet(StringComparer.Ordinal);
        if (ids.Count != 6)
        {
            errors.Add($"Expected 6 unique anatomy IDs, found {ids.Count}.");
        }

        foreach (string required in RequiredAnatomies)
        {
            if (!ids.Contains(required))
            {
                errors.Add($"Missing anatomy profile '{required}'.");
            }
        }
    }

    private static void ValidateFactions(ICollection<string> errors)
    {
        DungeonFactionDefinitionSO[] factions =
            FindAssets<DungeonFactionDefinitionSO>(
                "Assets/Resources/SO/Factions/Dungeons");
        if (factions.Length != 6)
        {
            errors.Add($"Expected 6 faction assets, found {factions.Length}.");
        }

        Dictionary<string, DungeonFactionDefinitionSO> byId = factions
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.StableId))
            .GroupBy(value => value.StableId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First());
        if (byId.Count != 6)
        {
            errors.Add($"Expected 6 dungeon factions, found {byId.Count}.");
        }

        foreach (string factionId in DungeonFactionIds.All)
        {
            if (!byId.TryGetValue(
                    factionId,
                    out DungeonFactionDefinitionSO faction))
            {
                errors.Add($"Missing dungeon faction '{factionId}'.");
                continue;
            }

            if (faction.crest == null)
                errors.Add($"{factionId} has no crest.");
            if (faction.tradeCargo == null || faction.tradeCargo.Count == 0)
                errors.Add($"{factionId} has no physical trade cargo.");
            if (faction.supplyCargo == null || faction.supplyCargo.Count == 0)
                errors.Add($"{factionId} has no physical supply cargo.");
        }
    }

    private static void ValidateHumanBranches(ICollection<string> errors)
    {
        string[] branchIds =
        {
            HumanInvasionBranchIds.RoyalArmy,
            HumanInvasionBranchIds.PioneerSupply,
            HumanInvasionBranchIds.RoyalOrdnance,
            HumanInvasionBranchIds.IntelligenceHunters,
            HumanInvasionBranchIds.RadiantOrder
        };
        if (branchIds.Distinct(StringComparer.Ordinal).Count() != 5)
        {
            errors.Add("Human invasion branch IDs are not five unique values.");
        }
    }

    private static void ValidateDefense(ICollection<string> errors)
    {
        BuildingSO[] defenses = FindAssets<BuildingSO>(
                "Assets/Resources/SO/Building")
            .Where(value => value != null
                && value.Defense != null
                && value.Defense.IsDefenseFacility)
            .ToArray();
        if (defenses.Length != 19)
        {
            errors.Add(
                $"Expected 19 active defense facilities, found {defenses.Length}.");
        }
        int uniqueDefenseIds = defenses
            .Select(value => value.id)
            .Distinct()
            .Count();
        if (uniqueDefenseIds != defenses.Length)
        {
            errors.Add(
                $"Expected unique defense building IDs, found "
                + $"{uniqueDefenseIds} IDs across {defenses.Length} assets.");
        }

        foreach (int id in NewDefenseBuildingIds)
        {
            BuildingSO building = defenses.FirstOrDefault(value => value.id == id);
            if (building == null)
            {
                errors.Add($"Missing new defense building ID {id}.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(building.Defense.facilityFamilyId))
                errors.Add($"Defense building {id} has no facility family.");
            if (building.Defense.affinityTags == null
                || building.Defense.affinityTags.Length == 0)
                errors.Add($"Defense building {id} has no species affinity.");
        }

        foreach (BuildingSO building in defenses)
        {
            DefenseFacilityData defense = building.Defense;
            if (defense.growth == null)
                errors.Add($"Defense building {building.id} has no growth state.");
            if (defense.conditionLossPerActivation < 0f)
                errors.Add($"Defense building {building.id} has invalid wear.");
        }
    }

    private static void ValidateResearch(ICollection<string> errors)
    {
        ResearchProjectSO[] projects = FindAssets<ResearchProjectSO>(
            "Assets/Resources/SO/Research/Projects");
        if (projects.Length != 135)
        {
            errors.Add(
                $"Expected 135 research assets, found {projects.Length}.");
        }

        Dictionary<string, ResearchProjectSO> byId = projects
            .Where(value => value != null && value.ProjectId.IsValid)
            .GroupBy(value => value.ProjectId.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First());
        if (byId.Count != 135)
        {
            errors.Add($"Expected 135 research projects, found {byId.Count}.");
        }

        foreach (string id in NewResearchIds)
        {
            if (!byId.ContainsKey(id))
                errors.Add($"Missing defense research '{id}'.");
        }
    }

    private static T[] FindAssets<T>(string root)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .ToArray();
    }
}
#endif
