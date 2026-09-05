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
                "9cad2eeb2481442402425eed01ebc91e025cbc72c1782bb3b93d127cb209f567",
            ["recipe:dog-food"] =
                "e5afa05a7c5e383fd903736a928d5497850edaaab29a477a64aacdb92249ae78",
            ["recipe:dog-food-fresh"] =
                "d141695b1c0dcdcb55fb18e2a9c52732a156cd6410e401c7c2b63d69dbb6a1ee",
            ["recipe:hay-feed"] =
                "85817fbf0fef73511dbc3b612584a6d7cababdf51aca48c28a6e9d37b3954c45",
            ["recipe:malt"] =
                "fab121be4cbbd79ddd12a55fe96ebcc91296e4e499fc7f06a630bfebfb13d428",
            ["recipe:milling-flour"] =
                "013f957faec18e35a0035cf84adcb37a527ac1d3e67690c8439e8d4b20f61eac",
            ["recipe:sawmill-lumber"] =
                "8e11dae80351753183191a57605b0981dd646f05b7b2db9e10f698081f1a444c",
            ["recipe:silage"] =
                "2ff2c10234ec7db1cdc9164cb2e0656117abacf62402bee32c8faf05214c36b6",
            ["recipe:starch"] =
                "a9fcd25c3c263a5c2f8e041985a47580e03fd1736ab2ed2024eb15d95ce329be",
            ["recipe:steel-ingot"] =
                "6fb3e8fde2ed801127a26723801264aec26621e1c57a9577c7c32cdb112a5ece",
            ["recipe:treated-lumber"] =
                "26fedf2922cd76cd28329799ead10893816af3407960d91856caa595242c42b3"
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
        List<string> digestDrifts = new();
        for (int index = 0; index < recipes.Length; index++)
        {
            string recipeId = recipes[index].RecipeId;
            if (!ExpectedDigestById.TryGetValue(recipeId, out string expected)
                || !string.Equals(first[index], expected, StringComparison.Ordinal))
            {
                digestDrifts.Add(recipeId + "=" + first[index]);
            }
        }
        Require(digestDrifts.Count == 0,
            "Reviewed recipe semantic digests drifted: "
            + string.Join(";", digestDrifts) + ".");

        ProductionRecipeSO dogFood = recipes.Single(value => string.Equals(
            value.RecipeId,
            "recipe:dog-food",
            StringComparison.Ordinal));
        ProductionRecipeSO displayClone = Clone(dogFood);
        ProductionRecipeSO orderClone = Clone(dogFood);
        ProductionRecipeSO workClone = Clone(dogFood);
        ProductionRecipeSO invalidClone = Clone(dogFood);
        ProductionRecipeSO staleClone = Clone(dogFood);
        ProductionRecipeSO workOnlyWithoutSpoilage = Clone(dogFood);
        ProductionRecipeSO silage = recipes.Single(value => string.Equals(
            value.RecipeId,
            "recipe:silage",
            StringComparison.Ordinal));
        ProductionRecipeSO passiveWithoutSpoilage = Clone(silage);
        ProductionRecipeSO passiveNoncanonicalSpoilage = Clone(silage);
        ProductionRecipeSO passiveOrphanSpoilage = Clone(silage);
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

            SetString(workOnlyWithoutSpoilage, "spoilageItemId", string.Empty);
            Require(IsLowercaseSha256(
                    ProductionRecipeSemanticDigest.Capture(
                        workOnlyWithoutSpoilage)),
                "Work-only recipe did not accept an explicitly empty spoilage contract.");

            SetString(passiveWithoutSpoilage, "spoilageItemId", string.Empty);
            Require(passiveWithoutSpoilage.SpoilageItemId.Length == 0,
                "Passive recipe silently substituted a spoilage fallback.");
            ExpectMessage<InvalidOperationException>(
                () => ProductionRecipeSemanticDigest.Capture(
                    passiveWithoutSpoilage),
                "noncanonical SpoilageItemId");

            SetString(
                passiveNoncanonicalSpoilage,
                "spoilageItemId",
                " waste:mixed-rot ");
            Require(string.Equals(
                    passiveNoncanonicalSpoilage.SpoilageItemId,
                    " waste:mixed-rot ",
                    StringComparison.Ordinal),
                "Passive recipe getter normalized a noncanonical authority ID.");
            ExpectMessage<InvalidOperationException>(
                () => ProductionRecipeSemanticDigest.Capture(
                    passiveNoncanonicalSpoilage),
                "noncanonical SpoilageItemId");

            ResourceItemDefinitionSO[] catalogItems = Resources
                .LoadAll<ResourceItemDefinitionSO>(
                    ItemDefinitionSO.UnifiedResourcePath);
            ExpectMessage<InvalidOperationException>(
                () => new ResourceEconomyContentCatalog(
                    catalogItems,
                    new[] { passiveWithoutSpoilage },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>()),
                "explicitly authored canonical spoilage item ID");
            ExpectMessage<InvalidOperationException>(
                () => new ResourceEconomyContentCatalog(
                    catalogItems,
                    new[] { passiveNoncanonicalSpoilage },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>()),
                "explicitly authored canonical spoilage item ID");
            SetString(
                passiveOrphanSpoilage,
                "spoilageItemId",
                "waste:missing-catalog-fixture");
            ExpectMessage<InvalidOperationException>(
                () => new ResourceEconomyContentCatalog(
                    catalogItems,
                    new[] { passiveOrphanSpoilage },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>()),
                "unknown spoilage item");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(displayClone);
            UnityEngine.Object.DestroyImmediate(orderClone);
            UnityEngine.Object.DestroyImmediate(workClone);
            UnityEngine.Object.DestroyImmediate(invalidClone);
            UnityEngine.Object.DestroyImmediate(staleClone);
            UnityEngine.Object.DestroyImmediate(workOnlyWithoutSpoilage);
            UnityEngine.Object.DestroyImmediate(passiveWithoutSpoilage);
            UnityEngine.Object.DestroyImmediate(passiveNoncanonicalSpoilage);
            UnityEngine.Object.DestroyImmediate(passiveOrphanSpoilage);
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
