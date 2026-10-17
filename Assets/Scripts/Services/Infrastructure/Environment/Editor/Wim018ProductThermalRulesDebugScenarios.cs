using System;
using UnityEditor;
using UnityEngine;

public static class Wim018ProductThermalRulesDebugScenarios
{
    private const string ExistingWildlifeRoot =
        "Assets/Resources/SO/Wildlife/Species";
    private const string V20WildlifeRoot =
        "Assets/Resources/SO/V20/Ecology/Wildlife";

    private static readonly ExpectedSpecies[] ExpectedDomesticableSpecies =
    {
        new ExpectedSpecies(ExpectedWildlife("Wildlife_CaveRat"), "cave_rat", 5f, 30f),
        new ExpectedSpecies(ExpectedWildlife("Wildlife_MossBoar"), "moss_boar", 5f, 30f),
        new ExpectedSpecies(ExpectedWildlife("Wildlife_RuneDeer"), "rune_deer", 5f, 30f),
        new ExpectedSpecies(ExpectedWildlife("Wildlife_ShadowHare"), "shadow_hare", 5f, 30f),
        new ExpectedSpecies(ExpectedWildlife("Wildlife_ShadowWolf"), "shadow_wolf", 5f, 30f),
        new ExpectedSpecies(ExpectedV20("wildlife_cave_hound"), "cave_hound", 5f, 30f),
        new ExpectedSpecies(ExpectedV20("wildlife_crystal_beetle"), "crystal_beetle", 5f, 30f),
        new ExpectedSpecies(ExpectedV20("wildlife_deep_goat"), "deep_goat", 5f, 30f),
        new ExpectedSpecies(ExpectedV20("wildlife_silk_spider"), "silk_spider", 5f, 30f),
        new ExpectedSpecies(ExpectedV20("wildlife_spore_elk"), "spore_elk", 5f, 30f),
        new ExpectedSpecies(ExpectedV20("wildlife_tunnel_mole"), "tunnel_mole", 5f, 30f),
        new ExpectedSpecies(ExpectedV20("wildlife_ember_lizard"), "ember_lizard", 20f, 35f),
        new ExpectedSpecies(ExpectedV20("wildlife_frost_ram"), "frost_ram", -10f, 10f)
    };

    [MenuItem("DungeonStory/QA/WIM/018 Product Thermal Rules")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("[PASS] WIM018_PRODUCT_THERMAL_RULES");
    }

    public static void Run()
    {
        VerifyPureRuleBoundaries();
        VerifyAuthoredDomesticableRanges();
    }

    private static void VerifyPureRuleBoundaries()
    {
        AssertClose("ordinary lower comfort", 1f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                5f, 5f, 30f));
        AssertClose("ordinary upper comfort", 1f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                30f, 5f, 30f));
        AssertClose("ordinary ten below", 0.5f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                -5f, 5f, 30f));
        AssertClose("ordinary ten above", 0.5f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                40f, 5f, 30f));
        AssertClose("ordinary floor below", 0.25f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                -10f, 5f, 30f));
        AssertClose("ordinary floor above", 0.25f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                45f, 5f, 30f));

        AssertClose("frost lower comfort", 1f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                -10f, -10f, 10f));
        AssertClose("frost upper comfort", 1f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                10f, -10f, 10f));
        AssertClose("frost ten above", 0.5f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                20f, -10f, 10f));
        AssertClose("frost floor above", 0.25f,
            WildlifeProductThermalRules.CalculateProductProgressMultiplier(
                25f, -10f, 10f));
    }

    private static void VerifyAuthoredDomesticableRanges()
    {
        Require(ExpectedDomesticableSpecies.Length == 13,
            "Expected exactly thirteen domesticable wildlife entries.");
        for (int index = 0; index < ExpectedDomesticableSpecies.Length; index++)
        {
            ExpectedSpecies expected = ExpectedDomesticableSpecies[index];
            WildlifeSpeciesSO species = AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>(
                expected.Path);
            Require(species != null,
                $"Missing domesticable wildlife asset '{expected.Path}'.");
            Require(string.Equals(species.SpeciesId, expected.SpeciesId,
                    StringComparison.Ordinal),
                $"Expected '{expected.SpeciesId}' at '{expected.Path}', found "
                    + $"'{species.SpeciesId}'.");
            Require(species.Husbandry.Domesticable,
                $"'{species.SpeciesId}' must remain domesticable.");
            AssertClose(species.SpeciesId + " authored minimum",
                expected.MinimumTemperatureC,
                species.ProductComfortMinimumTemperatureC);
            AssertClose(species.SpeciesId + " authored maximum",
                expected.MaximumTemperatureC,
                species.ProductComfortMaximumTemperatureC);
            AssertClose(species.SpeciesId + " husbandry minimum",
                expected.MinimumTemperatureC,
                species.Husbandry.ProductComfortMinimumTemperatureC);
            AssertClose(species.SpeciesId + " husbandry maximum",
                expected.MaximumTemperatureC,
                species.Husbandry.ProductComfortMaximumTemperatureC);
        }
    }

    private static string ExpectedWildlife(string assetName) =>
        ExistingWildlifeRoot + "/" + assetName + ".asset";

    private static string ExpectedV20(string assetName) =>
        V20WildlifeRoot + "/" + assetName + ".asset";

    private static void AssertClose(string label, float expected, float actual)
    {
        if (float.IsNaN(actual)
            || float.IsInfinity(actual)
            || Mathf.Abs(expected - actual) > 0.00001f)
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

    private sealed class ExpectedSpecies
    {
        public ExpectedSpecies(string path,
            string speciesId,
            float minimumTemperatureC,
            float maximumTemperatureC)
        {
            Path = path;
            SpeciesId = speciesId;
            MinimumTemperatureC = minimumTemperatureC;
            MaximumTemperatureC = maximumTemperatureC;
        }

        public string Path { get; }
        public string SpeciesId { get; }
        public float MinimumTemperatureC { get; }
        public float MaximumTemperatureC { get; }
    }
}
