#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionRecipeSemanticDigestDebugScenarios
{
    private static readonly string[] ReviewedRecipeIds =
    {
        "recipe:charcoal",
        "recipe:dog-food",
        "recipe:dog-food-fresh",
        "recipe:hay-feed",
        "recipe:malt",
        "recipe:milling-flour",
        "recipe:silage",
        "recipe:sawmill-lumber",
        "recipe:starch",
        "recipe:steel-ingot",
        "recipe:treated-lumber"
    };

    private static readonly IReadOnlyDictionary<string, string> ExpectedDigestById =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["recipe:charcoal"] =
                "ca75e40b76660cc9447e66578fcd2b4599a39001500f6c5615603046a04e1a6b",
            ["recipe:dog-food"] =
                "04813f61418c9f9da8a03ea5754223f86e6683c8d475be54af1d4b40bc23e84c",
            ["recipe:dog-food-fresh"] =
                "b693d8871fab2ad6ff66629ff0d6b2f90503ac3aebc98bafbce778e68fdbc0de",
            ["recipe:hay-feed"] =
                "aa447e81c18ef96e1c72a2da6c641c066d536567ed524932ae6c478f4c79bdf6",
            ["recipe:malt"] =
                "51655f5a53fd2dd77846097c338e3c6c8585b4c1d2345bb5d0501bc59583d508",
            ["recipe:milling-flour"] =
                "650b0e5d57ea8683aef882054c80190586c7e286665b56e82c5ca6f662a0a794",
            ["recipe:sawmill-lumber"] =
                "054f0f983ebcd318652313e27f450bb9f1f4b7fff4ed85c8e49dc8c6938facb7",
            ["recipe:silage"] =
                "617a4b9b9114f4b1315af3ab29ccea641845b9bd6de106f757640490175bab90",
            ["recipe:starch"] =
                "a2f2743afc611336c0002e63360ac4561c8db41627d7b01098a62b5229982d19",
            ["recipe:steel-ingot"] =
                "270d23468b688941ec3d2c88b5a546507fdb88d84e4cc6f1217798a801b84f75",
            ["recipe:treated-lumber"] =
                "36691d0b72537b8f7825d9057f95c410b2db2e13ecbc03a1395a8dbb74c11867"
        };

    [MenuItem("DungeonStory/V27/Production/Run Recipe Semantic Digest Scenarios")]
    public static void RunAll()
    {
        ProductionRecipeSO[] recipes = CaptureReviewedRecipes();
        string[] first = recipes
            .Select(ProductionRecipeSemanticDigest.Capture)
            .ToArray();
        string[] second = recipes
            .Reverse()
            .Select(ProductionRecipeSemanticDigest.Capture)
            .Reverse()
            .ToArray();
        Require(first.SequenceEqual(second, StringComparer.Ordinal),
            "Recipe semantic digests changed with capture order.");
        Require(first.All(IsLowercaseSha256),
            "Recipe semantic digest is not canonical lowercase SHA-256.");
        Require(first.Distinct(StringComparer.Ordinal).Count() == first.Length,
            "Reviewed recipes have duplicate semantic digests.");
        for (int index = 0; index < recipes.Length; index++)
        {
            Require(ExpectedDigestById.TryGetValue(
                    recipes[index].RecipeId,
                    out string expected)
                && string.Equals(first[index], expected, StringComparison.Ordinal),
                $"Recipe '{recipes[index].RecipeId}' semantic digest drifted: "
                + first[index] + ".");
        }

        ProductionRecipeSO dogFood = recipes.Single(value => string.Equals(
            value.RecipeId,
            "recipe:dog-food",
            StringComparison.Ordinal));
        ProductionRecipeSO displayClone = Clone(dogFood);
        ProductionRecipeSO orderClone = Clone(dogFood);
        ProductionRecipeSO workClone = Clone(dogFood);
        ProductionRecipeSO invalidClone = Clone(dogFood);
        ProductionRecipeSO staleClone = Clone(dogFood);
        try
        {
            string original = ProductionRecipeSemanticDigest.Capture(dogFood);
            SetString(displayClone, "displayName", "digest-neutral-display-change");
            Require(string.Equals(
                    ProductionRecipeSemanticDigest.Capture(displayClone),
                    original,
                    StringComparison.Ordinal),
                "Presentation-only recipe text changed the semantic digest.");

            SerializedObject ordered = new(orderClone);
            SerializedProperty inputs = ordered.FindProperty("inputs");
            Require(inputs != null && inputs.arraySize == 2,
                "Dog-food digest fixture no longer has two inputs.");
            inputs.MoveArrayElement(0, 1);
            ordered.ApplyModifiedPropertiesWithoutUndo();
            Require(string.Equals(
                    ProductionRecipeSemanticDigest.Capture(orderClone),
                    original,
                    StringComparison.Ordinal),
                "Input insertion order changed the semantic digest.");

            SetFloat(workClone, "requiredWork", dogFood.RequiredWork + 1f);
            Require(!string.Equals(
                    ProductionRecipeSemanticDigest.Capture(workClone),
                    original,
                    StringComparison.Ordinal),
                "Required WU drift did not change the semantic digest.");

            SerializedObject invalid = new(invalidClone);
            SerializedProperty invalidInputs = invalid.FindProperty("inputs");
            invalidInputs.GetArrayElementAtIndex(1)
                .FindPropertyRelative("itemId").stringValue =
                invalidInputs.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("itemId").stringValue;
            invalid.ApplyModifiedPropertiesWithoutUndo();
            Expect<InvalidOperationException>(() =>
                ProductionRecipeSemanticDigest.Capture(invalidClone));

            ProductionPreparedOutputBatchSaveData sourceBound = new()
            {
                phase = ProductionPreparedOutputPhase
                    .ResolvedWaitingForOutputSpace,
                recipeDefinitionDigest = original
            };
            ProductionPreparedOutputSourceRevisionGuard.ValidateResolvedBatch(
                sourceBound,
                dogFood,
                "digest-fixture");
            sourceBound.recipeDefinitionDigest = new string('f', 64);
            ExpectMessage<InvalidOperationException>(
                () => ProductionPreparedOutputSourceRevisionGuard
                    .ValidateResolvedBatch(
                        sourceBound,
                        dogFood,
                        "digest-fixture"),
                ProductionPreparedOutputSourceRevisionGuard
                    .StaleFailureToken);
            sourceBound.recipeDefinitionDigest = original;
            SetFloat(staleClone, "requiredWork", dogFood.RequiredWork + 1f);
            ExpectMessage<InvalidOperationException>(
                () => ProductionPreparedOutputSourceRevisionGuard
                    .ValidateResolvedBatch(
                        sourceBound,
                        staleClone,
                        "digest-fixture"),
                ProductionPreparedOutputSourceRevisionGuard
                    .StaleFailureToken);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(displayClone);
            UnityEngine.Object.DestroyImmediate(orderClone);
            UnityEngine.Object.DestroyImmediate(workClone);
            UnityEngine.Object.DestroyImmediate(invalidClone);
            UnityEngine.Object.DestroyImmediate(staleClone);
        }

        Debug.Log("[ProductionRecipeSemanticDigest] focused scenarios passed. "
            + CaptureCurrentRows());
    }

    public static string CaptureCurrentRows() => string.Join(
        ";",
        CaptureReviewedRecipes().Select(recipe =>
            recipe.RecipeId + "="
            + ProductionRecipeSemanticDigest.Capture(recipe)));

    private static ProductionRecipeSO[] CaptureReviewedRecipes()
    {
        ProductionRecipeSO[] recipes = Resources
            .LoadAll<ProductionRecipeSO>(ProductionRecipeSO.ResourcePath)
            .Where(value => value != null
                && ReviewedRecipeIds.Contains(
                    value.RecipeId,
                    StringComparer.Ordinal))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Length == ReviewedRecipeIds.Length,
            $"Expected {ReviewedRecipeIds.Length} reviewed recipes, got {recipes.Length}.");
        return recipes;
    }

    private static ProductionRecipeSO Clone(ProductionRecipeSO source)
    {
        ProductionRecipeSO clone = ScriptableObject.CreateInstance<
            ProductionRecipeSO>();
        EditorJsonUtility.FromJsonOverwrite(
            EditorJsonUtility.ToJson(source),
            clone);
        return clone;
    }

    private static void SetString(
        ProductionRecipeSO recipe,
        string propertyName,
        string value)
    {
        SerializedObject serialized = new(recipe);
        SerializedProperty property = serialized.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Recipe property '{propertyName}' is missing.");
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(
        ProductionRecipeSO recipe,
        string propertyName,
        float value)
    {
        SerializedObject serialized = new(recipe);
        SerializedProperty property = serialized.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Recipe property '{propertyName}' is missing.");
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool IsLowercaseSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

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
