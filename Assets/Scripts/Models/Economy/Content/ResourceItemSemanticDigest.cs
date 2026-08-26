using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Canonical gameplay-semantic digest for definition-only resource items used
/// by prepared production output. Presentation and Unity object identity are
/// excluded; mass, stacking, value and every supported behavior are explicit.
/// </summary>
public static class ResourceItemSemanticDigest
{
    public const string SchemaToken = "resource-item-semantic@1";

    private const ResourceIngredientTag DefinedIngredientTags =
        ResourceIngredientTag.Plant
        | ResourceIngredientTag.Fungus
        | ResourceIngredientTag.Milk
        | ResourceIngredientTag.Egg
        | ResourceIngredientTag.Meat
        | ResourceIngredientTag.Blood
        | ResourceIngredientTag.Fat
        | ResourceIngredientTag.Fiber
        | ResourceIngredientTag.Wood
        | ResourceIngredientTag.Mineral
        | ResourceIngredientTag.Arcane
        | ResourceIngredientTag.Spoiled
        | ResourceIngredientTag.Forbidden
        | ResourceIngredientTag.Fuel
        | ResourceIngredientTag.Feed
        | ResourceIngredientTag.Sweet
        | ResourceIngredientTag.Salted;

    public static string Capture(ResourceItemDefinitionSO item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        RequireCanonicalRequired(item.AuthoredItemId, "item ID");
        if (!item.StableId.IsValid
            || !string.Equals(
                item.AuthoredItemId,
                item.ItemId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Resource item has a noncanonical item ID.");
        }
        RequireEnum(item.AuthoredStockCategory, "stock category");
        if (item.AuthoredMaxStack < 1)
            throw new InvalidOperationException(
                "Resource item has a nonpositive authored max stack.");
        if (item.AuthoredUnitPrice < 0)
            throw new InvalidOperationException(
                "Resource item has a negative authored unit price.");

        PhysicalMassGrams unitMass = PhysicalMassGrams
            .FromCanonicalKilograms(item.AuthoredUnitWeight);
        ItemFeatureDefinition[] features = CaptureFeatures(item);

        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(SchemaToken);
        canonical.Append("resource-item-definition");
        canonical.Append(item.ItemId);
        canonical.AppendEnum(item.AuthoredStockCategory);
        canonical.Append(unitMass.Value);
        canonical.Append(item.AuthoredMaxStack);
        canonical.Append(item.AuthoredUnitPrice);
        canonical.Append(features.Length);

        foreach (ItemFeatureDefinition feature in features)
            AppendFeature(canonical, feature);

        return canonical.ComputeSha256();
    }

    private static ItemFeatureDefinition[] CaptureFeatures(
        ResourceItemDefinitionSO item)
    {
        ItemFeatureDefinition[] features = (item.Features
                ?? Array.Empty<ItemFeatureDefinition>())
            .ToArray();
        if (features.Any(value => value == null))
            throw new InvalidOperationException(
                $"Resource item '{item.ItemId}' has a null feature.");

        string[] validation = item.ValidateDefinition().ToArray();
        if (validation.Length > 0)
        {
            throw new InvalidOperationException(
                $"Resource item '{item.ItemId}' is invalid: "
                + string.Join(";", validation));
        }

        ItemFeatureDefinition[] ordered = features
            .OrderBy(value => value.FeatureId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Select(value => value.FeatureId)
            .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                $"Resource item '{item.ItemId}' has duplicate features.");
        }
        foreach (ItemFeatureDefinition feature in ordered)
            RequireCanonicalRequired(feature.FeatureId, "feature ID");
        return ordered;
    }

    private static void AppendFeature(
        CanonicalSemanticDigestBuilder canonical,
        ItemFeatureDefinition feature)
    {
        canonical.Append(feature.FeatureId);
        switch (feature)
        {
            case ProductionItemFeature production:
                RequireEnum(production.kind, "production kind");
                if ((production.ingredientTags & ~DefinedIngredientTags) != 0)
                {
                    throw new InvalidOperationException(
                        "Resource item has undefined ingredient tag bits.");
                }
                canonical.Append("production@1");
                canonical.AppendEnum(production.kind);
                canonical.Append((long)production.ingredientTags);
                canonical.Append(production.sharedIntermediate);
                return;

            case MarketItemFeature market:
                RequireFiniteRange(market.saleRate, 0f, 1f, "market sale rate");
                canonical.Append("market@1");
                canonical.AppendFloat(market.saleRate);
                return;

            case ResearchGateItemFeature research:
                RequireCanonicalRequired(
                    research.requiredResearchId,
                    "required research ID");
                canonical.Append("research-gate@1");
                canonical.Append(research.requiredResearchId);
                return;

            case FacilitySupplyItemFeature supply:
                RequireFiniteRange(
                    supply.fuelValue,
                    0f,
                    float.MaxValue,
                    "facility fuel value");
                RequireFiniteRange(
                    supply.nutritionValue,
                    0f,
                    float.MaxValue,
                    "facility nutrition value");
                canonical.Append("facility-supply@1");
                canonical.AppendFloat(supply.fuelValue);
                canonical.AppendFloat(supply.nutritionValue);
                canonical.Append(supply.feedEligible);
                return;

            default:
                throw new InvalidOperationException(
                    "Prepared-output resource item has unsupported semantic "
                    + $"feature '{feature.GetType().FullName}'.");
        }
    }

    private static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        string role)
    {
        if (float.IsNaN(value)
            || float.IsInfinity(value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"Resource item has invalid {role}.");
        }
    }

    private static void RequireCanonicalRequired(string value, string role)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resource item has a noncanonical {role}.");
        }
    }

    private static void RequireEnum<T>(T value, string role)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
        {
            throw new InvalidOperationException(
                $"Resource item has an invalid {role}.");
        }
    }
}

/// <summary>
/// Binds the empty runtime-component payload to the exact live item definition.
/// A payload copied across an item revision therefore cannot pass restore or
/// publication validation even though its component collection remains empty.
/// </summary>
public static class ProductionPreparedOutputComponentProfileDigest
{
    public const string SchemaToken =
        "production-prepared-output-component-profile@1";
    public const string StaleFailureToken =
        "prepared-output-item-revision-stale";

    private const string PayloadPrefix =
        "production-prepared-output-components@1|kind=generic-definition|item=";
    private const string PayloadSuffix = "|components=0";

    public static string BuildCanonicalPayload(ResourceItemDefinitionSO item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        string itemId = item.ItemId;
        int byteCount = Encoding.UTF8.GetByteCount(itemId);
        return PayloadPrefix
            + byteCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":"
            + itemId
            + PayloadSuffix;
    }

    public static string Capture(
        ResourceItemDefinitionSO item,
        string canonicalPayload)
    {
        string expectedPayload = BuildCanonicalPayload(item);
        if (!string.Equals(
                canonicalPayload,
                expectedPayload,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Prepared output has a noncanonical definition-only payload.");
        }

        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(SchemaToken);
        canonical.Append(ResourceItemSemanticDigest.Capture(item));
        canonical.Append(expectedPayload);
        return canonical.ComputeSha256();
    }

    public static void Validate(
        ResourceItemDefinitionSO item,
        string canonicalPayload,
        string savedProfileDigest,
        string context)
    {
        string current = Capture(item, canonicalPayload);
        if (!string.Equals(
                savedProfileDigest,
                current,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                (context ?? string.Empty) + ":" + StaleFailureToken);
        }
    }
}
