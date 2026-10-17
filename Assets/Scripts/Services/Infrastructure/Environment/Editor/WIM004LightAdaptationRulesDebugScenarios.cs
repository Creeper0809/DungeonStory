using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WIM004LightAdaptationRulesDebugScenarios
{
    private const string SpeciesRoot = "Assets/Resources/SO/Character/Species";
    private const string VampirePath =
        SpeciesRoot + "/Species_Vampire.asset";
    private const string MyconidPath =
        SpeciesRoot + "/Species_Myconid.asset";

    [MenuItem("DungeonStory/QA/WIM/004 Light Adaptation Rules")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("[PASS] WIM004_LIGHT_ADAPTATION_RULES");
    }

    public static void Run()
    {
        CharacterSpeciesSO vampire = LoadRequired(VampirePath);
        CharacterSpeciesSO myconid = LoadRequired(MyconidPath);

        VerifyEnabledSpecies(vampire, myconid);
        VerifyAuthoredDisabledSpecies(vampire, myconid);
        VerifyWorkCombinationRules();
    }

    private static void VerifyEnabledSpecies(
        CharacterSpeciesSO vampire,
        CharacterSpeciesSO myconid)
    {
        Require(vampire.environment.ToLightAdaptationProfile().Enabled,
            "Vampire light adaptation must be enabled.");
        Require(myconid.environment.ToLightAdaptationProfile().Enabled,
            "Myconid light adaptation must be enabled.");

        VerifyProjection("Vampire@0", vampire, 0f, 0f, 0f, 1f);
        VerifyProjection("Vampire@35", vampire, 35f, 0f, 0f, 1f);
        VerifyProjection("Vampire@55", vampire, 55f, 0.5f, -1.5f, 0.975f);
        VerifyProjection("Vampire@75", vampire, 75f, 1f, -3f, 0.95f);
        VerifyProjection("Vampire@100", vampire, 100f, 1f, -3f, 0.95f);

        VerifyProjection("Myconid@55", myconid, 55f, 0f, 0f, 1f);
        VerifyProjection("Myconid@75", myconid, 75f, 0.5f, -1.5f, 0.975f);
        VerifyProjection("Myconid@95", myconid, 95f, 1f, -3f, 0.95f);
    }

    private static void VerifyAuthoredDisabledSpecies(
        CharacterSpeciesSO vampire,
        CharacterSpeciesSO myconid)
    {
        CharacterSpeciesSO[] authored = AssetDatabase
            .FindAssets("t:CharacterSpeciesSO", new[] { SpeciesRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>)
            .Where(species => species != null)
            .ToArray();
        int enabledCount = authored.Count(species =>
            species.environment.ToLightAdaptationProfile().Enabled);
        Require(enabledCount == 2,
            $"Expected exactly two enabled light-adaptation species, found {enabledCount}.");

        foreach (CharacterSpeciesSO species in authored)
        {
            if (species == vampire || species == myconid)
            {
                continue;
            }

            Require(!species.environment.ToLightAdaptationProfile().Enabled,
                $"Non-target species '{species.speciesTag}' must keep light adaptation disabled.");
        }
    }

    private static void VerifyWorkCombinationRules()
    {
        AssertClose("precision visual floor", 0.8f,
            DungeonStory.Environment.CharacterEnvironmentRules
                .ResolveLightAdaptedWorkSpeed(0.9f, 0.8f, 0.95f, true));
        AssertClose("general light contribution", 0.855f,
            DungeonStory.Environment.CharacterEnvironmentRules
                .ResolveLightAdaptedWorkSpeed(0.9f, 1f, 0.95f, false));
        AssertClose("precision light contribution", 0.95f,
            DungeonStory.Environment.CharacterEnvironmentRules
                .ResolveLightAdaptedWorkSpeed(1f, 1f, 0.95f, true));
    }

    private static void VerifyProjection(
        string name,
        CharacterSpeciesSO species,
        float actualLight,
        float expectedDiscomfort,
        float expectedMood,
        float expectedWorkMultiplier)
    {
        SpeciesLightAdaptationProfile profile =
            species.environment.ToLightAdaptationProfile();
        DungeonStory.Environment.LightAdaptationProjection actual =
            DungeonStory.Environment.CharacterEnvironmentRules
                .ResolveLightAdaptation(
                    profile.Enabled,
                    actualLight,
                    profile.ComfortableMinimum,
                    profile.ComfortableMaximum,
                    profile.Sensitivity);

        AssertClose(name + " discomfort", expectedDiscomfort, actual.Discomfort);
        AssertClose(name + " mood", expectedMood, actual.MoodContribution);
        AssertClose(
            name + " work multiplier",
            expectedWorkMultiplier,
            actual.WorkSpeedMultiplier);
    }

    private static CharacterSpeciesSO LoadRequired(string path)
    {
        CharacterSpeciesSO species = AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>(path);
        if (species == null)
        {
            throw new InvalidOperationException(
                $"Required WIM004 species asset is missing at '{path}'.");
        }

        return species;
    }

    private static void AssertClose(string label, float expected, float actual)
    {
        if (Mathf.Abs(expected - actual) > 0.00001f)
        {
            throw new InvalidOperationException(
                $"{label}: expected {expected:0.#####}, actual {actual:0.#####}.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
