#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Wim018AnimalHusbandryDebugScenarios
{
    private const string ReportPath =
        "Temp/wim-018-animal-husbandry.txt";

    [MenuItem(
        "Tools/DungeonStory/Debug/WIM-018 Animal Husbandry Focused")]
    public static void RunFromMenu()
    {
        VerifyRepresentativeBreedingSeasons();
        VerifyFailedCollectionRetainsReadyCycleAndWork();

        Directory.CreateDirectory("Temp");
        File.WriteAllText(
            ReportPath,
            "WIM018_ANIMAL_HUSBANDRY=PASS\n"
            + "season-gate=deep_goat:spring,frost_ram:winter\n"
            + "collection=zero-retains-ready-and-work,exact-retry-commits-once\n");
        Debug.Log($"WIM018_ANIMAL_HUSBANDRY=PASS; report={ReportPath}");
    }

    private static void VerifyRepresentativeBreedingSeasons()
    {
        WildlifeSpeciesDefinition spring = LoadSpecies(
            "Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_deep_goat.asset");
        WildlifeSpeciesDefinition winter = LoadSpecies(
            "Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_frost_ram.asset");

        Require(
            spring.BreedingSeason == Season.Spring
            && InvokeSeasonGate(spring, Season.Spring)
            && !InvokeSeasonGate(spring, Season.Winter),
            "Deep goat did not retain its authored spring-only conception gate.");
        Require(
            winter.BreedingSeason == Season.Winter
            && InvokeSeasonGate(winter, Season.Winter)
            && !InvokeSeasonGate(winter, Season.Spring),
            "Frost ram did not retain its authored winter-only conception gate.");
    }

    private static void VerifyFailedCollectionRetainsReadyCycleAndWork()
    {
        HusbandryAnimalState state = new()
        {
            PendingWorkKind = AnimalHusbandryWorkKind.CollectProduct,
            PendingProductItemId = new ItemDefinitionId("fiber:deep-goat-wool"),
            PendingWorkCompleted = 12f
        };

        bool failed = InvokeCollectionCommit(
            state,
            readyCycles: 2,
            requestedAmount: 2,
            spawnAccepted: false,
            spawnedAmount: 0,
            out int retainedCycles);
        Require(
            !failed
            && retainedCycles == 2
            && state.PendingWorkKind == AnimalHusbandryWorkKind.CollectProduct
            && state.PendingProductItemId.Equals(
                new ItemDefinitionId("fiber:deep-goat-wool"))
            && Mathf.Approximately(state.PendingWorkCompleted, 12f),
            "A zero-output collection failure consumed its ready cycle or completed work.");

        bool retried = InvokeCollectionCommit(
            state,
            retainedCycles,
            requestedAmount: 2,
            spawnAccepted: true,
            spawnedAmount: 2,
            out int committedCycles);
        Require(
            retried
            && committedCycles == 1
            && state.PendingWorkKind == AnimalHusbandryWorkKind.None
            && !state.PendingProductItemId.IsValid
            && Mathf.Approximately(state.PendingWorkCompleted, 0f),
            "An exact collection retry did not commit one cycle and retire its work.");
    }

    private static WildlifeSpeciesDefinition LoadSpecies(string path)
    {
        WildlifeSpeciesSO asset = AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>(path);
        return asset != null
            ? asset.ToDefinition()
            : throw new InvalidOperationException(
                $"Required authored wildlife species is missing: {path}");
    }

    private static bool InvokeSeasonGate(
        WildlifeSpeciesDefinition species,
        Season season)
    {
        MethodInfo method = typeof(AnimalHusbandryRuntime).GetMethod(
            "IsBreedingSeason",
            BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, "The husbandry breeding-season gate is missing.");
        return (bool)method.Invoke(null, new object[] { species, season });
    }

    private static bool InvokeCollectionCommit(
        HusbandryAnimalState state,
        int readyCycles,
        int requestedAmount,
        bool spawnAccepted,
        int spawnedAmount,
        out int remainingReadyCycles)
    {
        MethodInfo method = typeof(AnimalHusbandryRuntime).GetMethod(
            "TryCommitReadyCycleAfterSpawn",
            BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, "The husbandry exact-output commit gate is missing.");
        object[] arguments =
        {
            state,
            readyCycles,
            requestedAmount,
            spawnAccepted,
            spawnedAmount,
            0
        };
        bool result = (bool)method.Invoke(null, arguments);
        remainingReadyCycles = (int)arguments[5];
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
