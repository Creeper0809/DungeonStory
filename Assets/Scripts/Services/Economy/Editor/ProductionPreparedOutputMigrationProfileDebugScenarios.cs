#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputMigrationProfileDebugScenarios
{
    [MenuItem("DungeonStory/V27/Production/Run Prepared Output Profile Scenarios")]
    public static void RunAll()
    {
        ProductionRecipeSO[] recipes = Resources
            .LoadAll<ProductionRecipeSO>(ProductionRecipeSO.ResourcePath)
            .Where(value => value != null
                && value.CaptureCanonicalOutputs().Any(output =>
                    output != null
                    && ProductionOutputRoleRules.IsPhysical(output.Role)
                    && output.Amount > 0
                    && output.Probability > 0f))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Length > 0,
            "Prepared-output profile audit found no recipes.");
        Require(recipes.Select(value => value.RecipeId)
                .Distinct(StringComparer.Ordinal).Count() == recipes.Length,
            "Prepared-output profile audit found duplicate recipe IDs.");
        List<string> invalidProfiles = new();
        foreach (ProductionRecipeSO recipe in recipes)
        {
            try
            {
                ProductionPreparedOutputMigrationScope
                    .ValidateCanonicalProfileOrThrow(recipe);
                string first = ProductionPreparedOutputMigrationScope
                    .CaptureProfileDigest(recipe);
                string second = ProductionPreparedOutputMigrationScope
                    .CaptureProfileDigest(recipe);
                Require(first.Length == 64
                    && string.Equals(first, second, StringComparison.Ordinal),
                    $"Prepared-output capability profile is nondeterministic for '{recipe.RecipeId}'.");
            }
            catch (InvalidOperationException exception)
            {
                invalidProfiles.Add(
                    recipe.RecipeId + " => " + exception.Message);
            }
        }
        Require(invalidProfiles.Count == 0,
            "Prepared-output profile coverage failed:\n"
            + string.Join("\n", invalidProfiles));

        ProductionRecipeSO source = recipes.First(value =>
            string.Equals(value.RecipeId, "recipe:hay-feed",
                StringComparison.Ordinal));
        string sourceDigest = ProductionPreparedOutputMigrationScope
            .CaptureProfileDigest(source);

        ProductionPreparedOutputBatchSaveData savedProfile = new()
        {
            phase = ProductionPreparedOutputPhase
                .ResolvedWaitingForOutputSpace,
            recipeId = source.RecipeId,
            migrationProfileDigest = sourceDigest
        };
        ProductionPreparedOutputMigrationScope.ValidateSavedProfileDigest(
            savedProfile,
            source,
            "migration-profile-fixture");
        savedProfile.migrationProfileDigest = new string('f', 64);
        ExpectMessage<InvalidOperationException>(
            () => ProductionPreparedOutputMigrationScope
                .ValidateSavedProfileDigest(
                    savedProfile,
                    source,
                    "migration-profile-fixture"),
            "prepared-output-capability-profile-stale");

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
            drifted.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.FoodProduction);
            drifted.ConfigureWorkshop(
                "workstation:feedbench",
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly,
                failedBatchItemId: "waste:mixed-rot");
            ProductionPreparedOutputMigrationScope
                .ValidateCanonicalProfileOrThrow(drifted);
            Require(!string.Equals(
                    sourceDigest,
                    ProductionPreparedOutputMigrationScope
                        .CaptureProfileDigest(drifted),
                    StringComparison.Ordinal),
                "Recipe semantic drift did not change its capability profile digest.");
            savedProfile.migrationProfileDigest = sourceDigest;
            ExpectMessage<InvalidOperationException>(
                () => ProductionPreparedOutputMigrationScope
                    .ValidateSavedProfileDigest(
                        savedProfile,
                        drifted,
                        "migration-profile-fixture"),
                "prepared-output-capability-profile-stale");
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
