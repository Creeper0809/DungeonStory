#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Narrow WIM-019 content validation for the complete authored wildlife slice.
/// It verifies only immutable migration-profile authoring and its public
/// validation boundary; behavior, movement, and save/restore are covered by
/// the separate focused runtime witnesses.
/// </summary>
public static class WildlifeMigrationProfileAuthoringDebugScenarios
{
    private static readonly string[] AuthoringRoots =
    {
        "Assets/Resources/SO/Wildlife/Species",
        "Assets/Resources/SO/V20/Ecology/Wildlife"
    };

    private static readonly ExpectedProfile[] ExpectedProfiles =
    {
        new("ash_crawler", WildlifeMigrationCapability.LoreOnly, 12, 0f, 0f, 0, 0f, 0f),
        new("carrion_drake", WildlifeMigrationCapability.Carcass, 12, 9f, 0f, 1, 0f, 0f),
        new("cave_hound", WildlifeMigrationCapability.GeneralHabitat, 12, 0f, 0f, 0, 0f, 0f),
        new("cave_rat", WildlifeMigrationCapability.GeneralHabitat, 12, 0f, 0f, 0, 0f, 0f),
        new("crystal_beetle", WildlifeMigrationCapability.LoreOnly, 12, 0f, 0f, 0, 0f, 0f),
        new("deep_goat", WildlifeMigrationCapability.GeneralHabitat, 12, 0f, 0f, 0, 0f, 0f),
        new("ember_lizard", WildlifeMigrationCapability.TemperatureBand, 8, 7f, 0f, 0, 20f, 35f),
        new("frost_ram", WildlifeMigrationCapability.TemperatureBand, 8, 7f, 0f, 0, -10f, 10f),
        new("glow_moth", WildlifeMigrationCapability.LoreOnly, 12, 0f, 0f, 0, 0f, 0f),
        new("mana_wisp", WildlifeMigrationCapability.LoreOnly, 12, 0f, 0f, 0, 0f, 0f),
        new("mire_leech", WildlifeMigrationCapability.FoulWater, 10, 8f, .25f, 0, 0f, 0f),
        new("moss_boar", WildlifeMigrationCapability.GeneralHabitat, 12, 0f, 0f, 0, 0f, 0f),
        new("rune_deer", WildlifeMigrationCapability.GeneralHabitat, 12, 0f, 0f, 0, 0f, 0f),
        new("shadow_hare", WildlifeMigrationCapability.GeneralHabitat, 12, 0f, 0f, 0, 0f, 0f),
        new("shadow_wolf", WildlifeMigrationCapability.GeneralHabitat, 12, 0f, 0f, 0, 0f, 0f),
        new("silk_spider", WildlifeMigrationCapability.GeneralHabitat, 12, 0f, 0f, 0, 0f, 0f),
        new("spore_elk", WildlifeMigrationCapability.LoreOnly, 12, 0f, 0f, 0, 0f, 0f),
        new("tunnel_mole", WildlifeMigrationCapability.LoreOnly, 12, 0f, 0f, 0, 0f, 0f)
    };

    [MenuItem("DungeonStory/WIM-019/Verify Migration Profile Authoring")]
    public static void RunFromMenu()
    {
        Debug.Log(Verify());
    }

    public static string Verify()
    {
        VerifyPublishedProfiles();
        VerifyPublicValidationBoundary();
        return "WIM019 migration-profile authoring: PASS; profiles=18; validation=public-boundary";
    }

    private static void VerifyPublishedProfiles()
    {
        Dictionary<string, ExpectedProfile> expected = ExpectedProfiles
            .ToDictionary(value => value.SpeciesId, StringComparer.Ordinal);
        WildlifeSpeciesSO[] published = AssetDatabase.FindAssets(
                "t:WildlifeSpeciesSO",
                AuthoringRoots)
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>(path))
            .Where(value => value != null)
            .ToArray();

        Require(published.Length == ExpectedProfiles.Length,
            "WIM019 requires exactly 18 wildlife assets in its two authored roots.");

        foreach (WildlifeSpeciesSO species in published)
        {
            Require(expected.TryGetValue(species.SpeciesId, out ExpectedProfile profile),
                "WIM019 found an unexpected wildlife species: '" + species.SpeciesId + "'.");
            Require(species.ValidateDefinition().Count == 0,
                "WIM019 asset validation failed for '" + species.SpeciesId + "': "
                + string.Join(" | ", species.ValidateDefinition()));

            AssertProfile(species.MigrationProfile, profile,
                "authored asset '" + species.SpeciesId + "'");
            AssertProfile(species.ToDefinition().MigrationProfile, profile,
                "immutable definition '" + species.SpeciesId + "'");
            expected.Remove(species.SpeciesId);
        }

        Require(expected.Count == 0,
            "WIM019 is missing authored wildlife profiles: "
            + string.Join(", ", expected.Keys.OrderBy(value => value, StringComparer.Ordinal)));
    }

    private static void VerifyPublicValidationBoundary()
    {
        WildlifeSpeciesSO transient = ScriptableObject.CreateInstance<WildlifeSpeciesSO>();
        try
        {
            ExpectException<ArgumentNullException>(
                () => transient.ConfigureMigrationProfile(null),
                "null migration profile");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(transient);
        }

        ExpectException<InvalidOperationException>(
            () => _ = new WildlifeMigrationProfile(
                WildlifeMigrationCapability.Unspecified,
                detectionRadiusCells: 12),
            "unspecified capability 0");
        ExpectException<InvalidOperationException>(
            () => _ = new WildlifeMigrationProfile(
                (WildlifeMigrationCapability)999,
                detectionRadiusCells: 12),
            "unknown capability 999");
        ExpectException<InvalidOperationException>(
            () => _ = WildlifeMigrationProfile.GeneralHabitat(0),
            "detection radius 0");
        ExpectException<InvalidOperationException>(
            () => _ = WildlifeMigrationProfile.LoreOnly(33),
            "detection radius 33");
        ExpectException<InvalidOperationException>(
            () => _ = WildlifeMigrationProfile.TemperatureBand(float.NaN, 10f),
            "NaN temperature");
        ExpectException<InvalidOperationException>(
            () => _ = WildlifeMigrationProfile.TemperatureBand(20f, 10f),
            "reversed temperature range");

        Require(WildlifeMigrationProfile.GeneralHabitat().Validate().Count == 0,
            "Explicit general-habitat profile must be valid.");
        Require(WildlifeMigrationProfile.LoreOnly().Validate().Count == 0,
            "Explicit lore-only profile must be valid.");
    }

    private static void AssertProfile(
        WildlifeMigrationProfile actual,
        ExpectedProfile expected,
        string owner)
    {
        Require(actual != null, "WIM019 " + owner + " has no migration profile.");
        Require(actual.Capability == expected.Capability
            && actual.DetectionRadiusCells == expected.DetectionRadiusCells
            && Mathf.Approximately(actual.SignalScore, expected.SignalScore)
            && Mathf.Approximately(actual.MinimumWaterRemainingRatio, expected.MinimumWaterRemainingRatio)
            && actual.MinimumCarcassQuantity == expected.MinimumCarcassQuantity
            && Mathf.Approximately(actual.MinimumTemperatureC, expected.MinimumTemperatureC)
            && Mathf.Approximately(actual.MaximumTemperatureC, expected.MaximumTemperatureC),
            "WIM019 " + owner + " does not match its approved migration profile.");
    }

    private static void ExpectException<T>(Action action, string boundary)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException(
            "WIM019 public validation accepted " + boundary + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly struct ExpectedProfile
    {
        public ExpectedProfile(
            string speciesId,
            WildlifeMigrationCapability capability,
            int detectionRadiusCells,
            float signalScore,
            float minimumWaterRemainingRatio,
            int minimumCarcassQuantity,
            float minimumTemperatureC,
            float maximumTemperatureC)
        {
            SpeciesId = speciesId;
            Capability = capability;
            DetectionRadiusCells = detectionRadiusCells;
            SignalScore = signalScore;
            MinimumWaterRemainingRatio = minimumWaterRemainingRatio;
            MinimumCarcassQuantity = minimumCarcassQuantity;
            MinimumTemperatureC = minimumTemperatureC;
            MaximumTemperatureC = maximumTemperatureC;
        }

        public string SpeciesId { get; }
        public WildlifeMigrationCapability Capability { get; }
        public int DetectionRadiusCells { get; }
        public float SignalScore { get; }
        public float MinimumWaterRemainingRatio { get; }
        public int MinimumCarcassQuantity { get; }
        public float MinimumTemperatureC { get; }
        public float MaximumTemperatureC { get; }
    }
}
#endif
