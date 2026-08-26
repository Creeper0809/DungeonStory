#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResourceItemSemanticDigestDebugScenarios
{
    private static readonly string[] ReviewedItemIds =
    {
        "feed:dog-food",
        "feed:hay",
        "feed:silage",
        "material:charcoal",
        "material:flour",
        "material:lumber",
        "material:malt",
        "material:starch",
        "material:steel-ingot",
        "material:treated-lumber",
        "waste:plant-rot"
    };

    private static readonly IReadOnlyDictionary<string, string>
        ExpectedDigestById = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["feed:dog-food"] =
                "e5ee865c1f24be6c2a9ac46e3ad0258c52335ec1a89cb9e5c5193ed3052aed04",
            ["feed:hay"] =
                "13f2e77f7ccd6bb5e6bff0397e56655eb6441d79866394600a79f0d8e66e8046",
            ["feed:silage"] =
                "1514a4883f0daf8095e28088956373600f821a95fe1bb71fb09320b61f747df7",
            ["material:charcoal"] =
                "f27c43c51d9554cdaf5f3e50eeebeb8747d183676203d31e4723cf034c77a6aa",
            ["material:flour"] =
                "f784915e8c60f273394927acb06d334c1f1402e82dbb94602a97b4bdf07e7f23",
            ["material:lumber"] =
                "3fd4c81dbb8e3022d358c37932703492c60ced336a34480d3e5dafa2b1e8bd69",
            ["material:malt"] =
                "b9a2d2505fe274c75303e193e6db5388cd0d96d5f5d50c44302f6403829eb2cd",
            ["material:starch"] =
                "edb03de6fd7579558ec60965c63cd390b229da39d45c9d0df23dd7712ada1815",
            ["material:steel-ingot"] =
                "bed3e9fe32a944834fb62d481a5daf3a7d08e70b0e15f9ef98ed62c097d29ea0",
            ["material:treated-lumber"] =
                "7f75d57223d01694c898bfc0807762bc16a4d26234ec0f92740f50f450cbc610",
            ["waste:plant-rot"] =
                "044648d7807a8f969cc4292425d03d3d43c51afb906b75d348156bf648429665"
        };

    [MenuItem("DungeonStory/V27/Production/Run Resource Item Semantic Digest Scenarios")]
    public static void RunAll()
    {
        ResourceItemDefinitionSO[] items = CaptureReviewedItems();
        string[] first = items.Select(ResourceItemSemanticDigest.Capture).ToArray();
        string[] second = items.Reverse()
            .Select(ResourceItemSemanticDigest.Capture)
            .Reverse()
            .ToArray();
        Require(first.SequenceEqual(second, StringComparer.Ordinal),
            "Resource item semantic digests changed with capture order.");
        Require(first.All(IsLowercaseSha256),
            "Resource item semantic digest is not lowercase SHA-256.");
        Require(first.Distinct(StringComparer.Ordinal).Count() == first.Length,
            "Reviewed resource items have duplicate semantic digests.");

        for (int index = 0; index < items.Length; index++)
        {
            Require(ExpectedDigestById.TryGetValue(
                    items[index].ItemId,
                    out string expected)
                && string.Equals(first[index], expected, StringComparison.Ordinal),
                $"Resource item '{items[index].ItemId}' semantic digest drifted: "
                + first[index] + ".");
        }

        ResourceItemDefinitionSO source = items.Single(value =>
            string.Equals(value.ItemId, "feed:hay", StringComparison.Ordinal));
        ResourceItemDefinitionSO displayClone = Clone(source);
        ResourceItemDefinitionSO massClone = Clone(source);
        ResourceItemDefinitionSO stackClone = Clone(source);
        ResourceItemDefinitionSO priceClone = Clone(source);
        ResourceItemDefinitionSO marketClone = Clone(source);
        ResourceItemDefinitionSO unsupportedClone = Clone(source);
        try
        {
            string original = ResourceItemSemanticDigest.Capture(source);
            SetString(displayClone, "displayName", "digest-neutral-display");
            Require(string.Equals(
                    ResourceItemSemanticDigest.Capture(displayClone),
                    original,
                    StringComparison.Ordinal),
                "Presentation-only item text changed the semantic digest.");

            SetFloat(massClone, "unitWeight", source.UnitWeight + 0.001f);
            RequireChanged(massClone, original, "unit mass");
            SetInteger(stackClone, "maxStack", source.MaxStack + 1);
            RequireChanged(stackClone, original, "max stack");
            SetInteger(priceClone, "unitPrice", source.UnitPrice + 1);
            RequireChanged(priceClone, original, "unit price");

            MarketItemFeature market = marketClone
                .GetFeatureOrDefault<MarketItemFeature>();
            Require(market != null, "Hay fixture has no market feature.");
            market.saleRate = Math.Max(0f, market.saleRate - 0.01f);
            RequireChanged(marketClone, original, "market sale rate");

            unsupportedClone.SetFeature(new FoodItemFeature
            {
                nutrition = 1f,
                freshnessSeconds = 1f
            });
            Expect<InvalidOperationException>(() =>
                ResourceItemSemanticDigest.Capture(unsupportedClone));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(displayClone);
            UnityEngine.Object.DestroyImmediate(massClone);
            UnityEngine.Object.DestroyImmediate(stackClone);
            UnityEngine.Object.DestroyImmediate(priceClone);
            UnityEngine.Object.DestroyImmediate(marketClone);
            UnityEngine.Object.DestroyImmediate(unsupportedClone);
        }

        Debug.Log("[ResourceItemSemanticDigest] focused scenarios passed. "
            + CaptureCurrentRows());
    }

    public static string CaptureCurrentRows() => string.Join(
        ";",
        CaptureReviewedItems().Select(item => item.ItemId + "="
            + ResourceItemSemanticDigest.Capture(item)));

    private static ResourceItemDefinitionSO[] CaptureReviewedItems()
    {
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(
                ResourceItemDefinitionSO.ResourcePath)
            .Where(value => value != null
                && ReviewedItemIds.Contains(value.ItemId, StringComparer.Ordinal))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        Require(items.Length == ReviewedItemIds.Length,
            $"Expected {ReviewedItemIds.Length} reviewed items, got {items.Length}.");
        return items;
    }

    private static ResourceItemDefinitionSO Clone(
        ResourceItemDefinitionSO source)
    {
        ResourceItemDefinitionSO clone = ScriptableObject.CreateInstance<
            ResourceItemDefinitionSO>();
        EditorJsonUtility.FromJsonOverwrite(
            EditorJsonUtility.ToJson(source),
            clone);
        return clone;
    }

    private static void RequireChanged(
        ResourceItemDefinitionSO item,
        string original,
        string role)
    {
        Require(!string.Equals(
                ResourceItemSemanticDigest.Capture(item),
                original,
                StringComparison.Ordinal),
            $"Resource item {role} drift did not change the digest.");
    }

    private static void SetString(
        ResourceItemDefinitionSO item,
        string propertyName,
        string value)
    {
        SerializedObject serialized = new(item);
        SerializedProperty property = serialized.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Item property '{propertyName}' is missing.");
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(
        ResourceItemDefinitionSO item,
        string propertyName,
        float value)
    {
        SerializedObject serialized = new(item);
        SerializedProperty property = serialized.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Item property '{propertyName}' is missing.");
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInteger(
        ResourceItemDefinitionSO item,
        string propertyName,
        int value)
    {
        SerializedObject serialized = new(item);
        SerializedProperty property = serialized.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Item property '{propertyName}' is missing.");
        property.intValue = value;
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
