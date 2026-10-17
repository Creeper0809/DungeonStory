#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Wim029WildlifeHaulDataDebugScenarios
{
    private const string ReportId = "wim-029-wildlife-haul-data";

    private static readonly string[] AuthoringRoots =
    {
        "Assets/Resources/SO/Wildlife/Species",
        "Assets/Resources/SO/V20/Ecology/Wildlife"
    };

    public static string RunAll()
    {
        List<string> rows = new();
        int passed = 0;

        Run(rows, ref passed, "PROFILE_DEEP_GOAT_VALUES_AND_SNAPSHOT",
            VerifyDeepGoatProfileValuesAndSnapshot);
        Run(rows, ref passed, "PROFILE_FROST_RAM_VALUES_AND_SNAPSHOT",
            VerifyFrostRamProfileValuesAndSnapshot);
        Run(rows, ref passed, "PROFILE_REJECTS_ZERO_CARGO_GRAMS",
            VerifyRejectsZeroCargoGrams);
        Run(rows, ref passed, "PROFILE_REJECTS_NEGATIVE_CARGO_GRAMS",
            VerifyRejectsNegativeCargoGrams);
        Run(rows, ref passed, "PROFILE_REJECTS_ZERO_LOADED_SPEED",
            VerifyRejectsZeroLoadedSpeed);
        Run(rows, ref passed, "PROFILE_REJECTS_LOADED_SPEED_ABOVE_ONE",
            VerifyRejectsLoadedSpeedAboveOne);
        Run(rows, ref passed, "PROFILE_REJECTS_NAN_LOADED_SPEED",
            VerifyRejectsNanLoadedSpeed);
        Run(rows, ref passed, "PROFILE_REJECTS_ZERO_DECISION_INTERVAL",
            VerifyRejectsZeroDecisionInterval);
        Run(rows, ref passed, "PROFILE_REJECTS_DECISION_INTERVAL_ABOVE_MAXIMUM",
            VerifyRejectsDecisionIntervalAboveMaximum);
        Run(rows, ref passed, "PROFILE_REJECTS_NAN_DECISION_INTERVAL",
            VerifyRejectsNanDecisionInterval);
        Run(rows, ref passed, "AUTHORED_ONLY_DEEP_GOAT_AND_FROST_RAM",
            VerifyOnlyApprovedSpeciesAuthorHaulProfiles);

        rows.Add($"{ReportId}; RESULT={(passed == rows.Count ? "PASS" : "FAIL")}; "
            + $"passed={passed}; failed={rows.Count - passed}; rows={rows.Count}");
        return string.Join(Environment.NewLine, rows);
    }

    [MenuItem("DungeonStory/WIM-029/Run Wildlife Haul Data Scenarios")]
    public static void RunFromMenu()
    {
        Debug.Log(RunAll());
    }

    private static void VerifyDeepGoatProfileValuesAndSnapshot()
    {
        WildlifeHaulRoleProfile profile = new(18000L, 0.8f, 0.5f);
        WildlifeHaulRoleProfile snapshot = profile.Snapshot();
        Require(!ReferenceEquals(profile, snapshot)
            && profile.MaxCargoMassGrams == 18000L
            && profile.LoadedMoveSpeedMultiplier == 0.8f
            && profile.DecisionIntervalSeconds == 0.5f
            && snapshot.MaxCargoMassGrams == 18000L
            && snapshot.LoadedMoveSpeedMultiplier == 0.8f
            && snapshot.DecisionIntervalSeconds == 0.5f
            && profile.Validate().Count == 0
            && snapshot.Validate().Count == 0,
            "Deep-goat haul profile values or independent snapshot drifted.");
    }

    private static void VerifyFrostRamProfileValuesAndSnapshot()
    {
        WildlifeHaulRoleProfile profile = new(24000L, 0.75f, 0.5f);
        WildlifeHaulRoleProfile snapshot = profile.Snapshot();
        Require(!ReferenceEquals(profile, snapshot)
            && profile.MaxCargoMassGrams == 24000L
            && profile.LoadedMoveSpeedMultiplier == 0.75f
            && profile.DecisionIntervalSeconds == 0.5f
            && snapshot.MaxCargoMassGrams == 24000L
            && snapshot.LoadedMoveSpeedMultiplier == 0.75f
            && snapshot.DecisionIntervalSeconds == 0.5f
            && profile.Validate().Count == 0
            && snapshot.Validate().Count == 0,
            "Frost-ram haul profile values or independent snapshot drifted.");
    }

    private static void VerifyRejectsZeroCargoGrams() =>
        RequireRejected(0L, 0.8f, 0.5f, "Zero cargo grams were accepted.");

    private static void VerifyRejectsNegativeCargoGrams() =>
        RequireRejected(-1L, 0.8f, 0.5f, "Negative cargo grams were accepted.");

    private static void VerifyRejectsZeroLoadedSpeed() =>
        RequireRejected(18000L, 0f, 0.5f, "Zero loaded speed was accepted.");

    private static void VerifyRejectsLoadedSpeedAboveOne() =>
        RequireRejected(18000L, 1.0001f, 0.5f, "Loaded speed above one was accepted.");

    private static void VerifyRejectsNanLoadedSpeed() =>
        RequireRejected(18000L, float.NaN, 0.5f, "NaN loaded speed was accepted.");

    private static void VerifyRejectsZeroDecisionInterval() =>
        RequireRejected(18000L, 0.8f, 0f, "Zero decision interval was accepted.");

    private static void VerifyRejectsDecisionIntervalAboveMaximum() =>
        RequireRejected(18000L, 0.8f, 0.5001f,
            "Decision interval above 0.5 seconds was accepted.");

    private static void VerifyRejectsNanDecisionInterval() =>
        RequireRejected(18000L, 0.8f, float.NaN,
            "NaN decision interval was accepted.");

    private static void VerifyOnlyApprovedSpeciesAuthorHaulProfiles()
    {
        Dictionary<string, ExpectedProfile> expected = new(StringComparer.Ordinal)
        {
            ["deep_goat"] = new ExpectedProfile(18000L, 0.8f, 0.5f),
            ["frost_ram"] = new ExpectedProfile(24000L, 0.75f, 0.5f)
        };
        WildlifeSpeciesSO[] published = AssetDatabase.FindAssets(
                "t:WildlifeSpeciesSO",
                AuthoringRoots)
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>(path))
            .Where(species => species != null)
            .ToArray();
        WildlifeSpeciesSO[] haulers = published
            .Where(species => species.HaulRoleProfile != null)
            .ToArray();

        Require(haulers.Length == 2,
            "Exactly deep_goat and frost_ram must author haul profiles.");
        foreach (WildlifeSpeciesSO species in published)
        {
            if (expected.TryGetValue(species.SpeciesId, out ExpectedProfile profile))
            {
                AssertProfile(species.HaulRoleProfile, profile,
                    "authored asset '" + species.SpeciesId + "'");
                WildlifeHaulRoleProfile definition = species.ToDefinition().HaulRoleProfile;
                AssertProfile(definition, profile,
                    "immutable definition '" + species.SpeciesId + "'");
                Require(!ReferenceEquals(species.HaulRoleProfile, definition),
                    "Definition snapshot aliased '" + species.SpeciesId + "' authoring.");
                expected.Remove(species.SpeciesId);
            }
            else
            {
                Require(species.HaulRoleProfile == null
                    && species.ToDefinition().HaulRoleProfile == null,
                    "Non-hauler '" + species.SpeciesId + "' authored a haul profile.");
            }
        }
        Require(expected.Count == 0,
            "Missing approved haul profiles: "
            + string.Join(", ", expected.Keys.OrderBy(value => value, StringComparer.Ordinal)));
    }

    private static void AssertProfile(
        WildlifeHaulRoleProfile actual,
        ExpectedProfile expected,
        string owner)
    {
        Require(actual != null
            && actual.MaxCargoMassGrams == expected.MaxCargoMassGrams
            && actual.LoadedMoveSpeedMultiplier == expected.LoadedMoveSpeedMultiplier
            && actual.DecisionIntervalSeconds == expected.DecisionIntervalSeconds
            && actual.Validate().Count == 0,
            ReportId + " " + owner + " does not match its approved haul profile.");
    }

    private static void RequireRejected(
        long cargoGrams,
        float loadedSpeed,
        float decisionInterval,
        string message)
    {
        try
        {
            _ = new WildlifeHaulRoleProfile(
                cargoGrams,
                loadedSpeed,
                decisionInterval);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Run(
        ICollection<string> rows,
        ref int passed,
        string name,
        Action scenario)
    {
        try
        {
            scenario();
            rows.Add(name + "=PASS");
            passed++;
        }
        catch (Exception exception)
        {
            rows.Add(name + "=FAIL; " + exception.Message);
        }
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
            long maxCargoMassGrams,
            float loadedMoveSpeedMultiplier,
            float decisionIntervalSeconds)
        {
            MaxCargoMassGrams = maxCargoMassGrams;
            LoadedMoveSpeedMultiplier = loadedMoveSpeedMultiplier;
            DecisionIntervalSeconds = decisionIntervalSeconds;
        }

        public long MaxCargoMassGrams { get; }
        public float LoadedMoveSpeedMultiplier { get; }
        public float DecisionIntervalSeconds { get; }
    }
}
#endif
