#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputMigrationProfileDebugScenarios
{
    private static readonly string[] ExpectedRecipeIds =
    {
        "recipe:charcoal",
        "recipe:dog-food",
        "recipe:dog-food-fresh",
        "recipe:hay-feed",
        "recipe:malt",
        "recipe:milling-flour",
        "recipe:sawmill-lumber",
        "recipe:silage",
        "recipe:starch",
        "recipe:steel-ingot",
        "recipe:treated-lumber"
    };

    private static readonly IReadOnlyDictionary<string, string>
        ExpectedProfileDigestByRecipe = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["recipe:charcoal"] =
                "c069d47f41a13617da391bdb06313c785ee45871d3fca6d12b01be28d89be82e",
            ["recipe:dog-food"] =
                "a59957f3b5f32a838173bdbb457fe2f29a08110ba83ca5befa14dfc8bee0d391",
            ["recipe:dog-food-fresh"] =
                "bc26a02c3b44c2267c3420c2798a525e987acec5c15f767760df9aa8cfe6e491",
            ["recipe:hay-feed"] =
                "07c90ff1501c5e315108a4c3b363a5595692dc6ccebe2e345f3de4c70013ffaa",
            ["recipe:malt"] =
                "e0c0dba7262768a5522f6d1a523a8aa004498f1b15cff015a16e62a38bfcaebd",
            ["recipe:milling-flour"] =
                "1e79511cd2f3e3ba9596f34c35b9fde90f9e6d3b12ded246f3f0f626e8794f3f",
            ["recipe:sawmill-lumber"] =
                "aff2ab2651af8d28bc86764c0edd151e22b1b7b91e6cc2bf20feea19aeb128fb",
            ["recipe:silage"] =
                "812c154cbb43ede8aa5204727aa5c3b6793999247fff889be7b5fdb9eef930de",
            ["recipe:starch"] =
                "22be8bfe79de5bcf09b8de327f710b7ce1067047fd6440e787aa7bafe724d6c1",
            ["recipe:steel-ingot"] =
                "378ab72821271e7fa9f6e92edfa4bb76c0b17acafac583da2cfe88da62eaa7dd",
            ["recipe:treated-lumber"] =
                "bd53ebcc71fc0d500e85d25aa43af19d4ba55a21e29928332e0cfce65afd32ab"
        };

    private const string ExpectedRegistryDigest =
        "2febcb4d2826b1c140d205c941698dfab836f3fa36f726097aebcf10857af3f4";

    [MenuItem("DungeonStory/V27/Production/Run Prepared Output Profile Scenarios")]
    public static void RunAll()
    {
        ProductionRecipeSO[] recipes = Resources
            .LoadAll<ProductionRecipeSO>(ProductionRecipeSO.ResourcePath)
            .Where(value => value != null
                && ProductionPreparedOutputMigrationScope.Contains(value.RecipeId))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Length == ExpectedRecipeIds.Length,
            $"Expected {ExpectedRecipeIds.Length} exact migration recipes, got {recipes.Length}.");
        for (int index = 0; index < recipes.Length; index++)
        {
            Require(string.Equals(
                    recipes[index].RecipeId,
                    ExpectedRecipeIds[index],
                    StringComparison.Ordinal),
                "Prepared-output migration recipe order or identity drifted.");
            ProductionPreparedOutputMigrationScope.ValidateExactProfileOrThrow(
                recipes[index]);
            Require(ExpectedProfileDigestByRecipe.TryGetValue(
                    recipes[index].RecipeId,
                    out string expectedProfileDigest)
                && string.Equals(
                    ProductionPreparedOutputMigrationScope.CaptureProfileDigest(
                        recipes[index].RecipeId),
                    expectedProfileDigest,
                    StringComparison.Ordinal),
                $"Prepared-output migration profile digest drifted for "
                + $"'{recipes[index].RecipeId}'.");
        }
        Require(string.Equals(
                ProductionPreparedOutputMigrationScope.CaptureRegistryDigest(),
                ExpectedRegistryDigest,
                StringComparison.Ordinal),
            "Prepared-output migration registry digest drifted.");

        ProductionPreparedOutputBatchSaveData savedProfile = new()
        {
            phase = ProductionPreparedOutputPhase
                .ResolvedWaitingForOutputSpace,
            recipeId = "recipe:hay-feed",
            migrationProfileDigest = ExpectedProfileDigestByRecipe[
                "recipe:hay-feed"]
        };
        ProductionPreparedOutputMigrationScope.ValidateSavedProfileDigest(
            savedProfile,
            "migration-profile-fixture");
        savedProfile.migrationProfileDigest = new string('f', 64);
        ExpectMessage<InvalidOperationException>(
            () => ProductionPreparedOutputMigrationScope
                .ValidateSavedProfileDigest(
                    savedProfile,
                    "migration-profile-fixture"),
            "prepared-output-migration-profile-stale");

        ProductionRecipeSO drifted = ScriptableObject.CreateInstance<ProductionRecipeSO>();
        try
        {
            drifted.Configure(
                "recipe:hay-feed",
                "drift fixture",
                string.Empty,
                "feedbench",
                "work:craft",
                string.Empty,
                1f,
                Array.Empty<ItemAmountDefinition>(),
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:main",
                        ProductionOutputRole.Main,
                        "feed:dog-food",
                        3,
                        1f)
                });
            drifted.ConfigureWorkshop(
                "workstation:feedbench",
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly,
                failedBatchItemId: "waste:mixed-rot");
            Require(
                ProductionPreparedOutputMigrationScope.Contains(drifted.RecipeId)
                && !ProductionPreparedOutputMigrationScope.MatchesExactProfile(drifted),
                "A same-scope recipe with a drifted compatible output item was accepted.");
            Expect<InvalidOperationException>(() =>
                ProductionPreparedOutputMigrationScope.ValidateExactProfileOrThrow(
                    drifted));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(drifted);
        }

        Debug.Log("[PreparedOutputMigrationProfile] focused scenarios passed.");
    }

    private static void Expect<T>(Action action) where T : Exception
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
            "Expected exception was not thrown: " + typeof(T).Name + ".");
    }

    private static void ExpectMessage<T>(Action action, string token)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T exception)
        {
            Require(exception.Message.Contains(token, StringComparison.Ordinal),
                $"Expected failure token '{token}', got '{exception.Message}'.");
            return;
        }
        throw new InvalidOperationException(
            "Expected exception was not thrown: " + typeof(T).Name + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
