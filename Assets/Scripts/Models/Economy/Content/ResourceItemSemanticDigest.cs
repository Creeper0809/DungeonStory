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

            case AmmunitionItemFeature ammunition:
                RequireCanonicalRequired(
                    ammunition.ammunitionKindId,
                    "ammunition kind ID");
                canonical.Append("ammunition@1");
                canonical.Append(ammunition.ammunitionKindId);
                return;

            case FoodItemFeature food:
                RequireEnum(food.quality, "food quality");
                RequireEnum(food.qualityBand, "food quality band");
                RequireEnum(food.servingRole, "food serving role");
                RequireFiniteRange(food.nutrition, 0f, float.MaxValue,
                    "food nutrition");
                RequireFiniteRange(food.mood, float.MinValue, float.MaxValue,
                    "food mood");
                RequireFiniteRange(food.freshnessSeconds, 0f, float.MaxValue,
                    "food freshness");
                canonical.Append("food@1");
                canonical.AppendEnum(food.quality);
                canonical.AppendEnum(food.qualityBand);
                canonical.AppendEnum(food.servingRole);
                canonical.AppendFloat(food.nutrition);
                canonical.AppendFloat(food.mood);
                canonical.AppendFloat(food.freshnessSeconds);
                canonical.Append(food.preserved);
                return;

            case MedicineItemFeature medicine:
                RequireFiniteRange(medicine.treatmentPotency, 0f,
                    float.MaxValue, "medicine treatment potency");
                RequireFiniteRange(medicine.infectionReduction, 0f,
                    float.MaxValue, "medicine infection reduction");
                RequireFiniteRange(medicine.detoxReduction, 0f,
                    float.MaxValue, "medicine detox reduction");
                RequireFiniteRange(medicine.painReduction, 0f,
                    float.MaxValue, "medicine pain reduction");
                canonical.Append("medicine@1");
                canonical.Append(medicine.supportsInjuryTreatment);
                canonical.AppendFloat(medicine.treatmentPotency);
                canonical.AppendFloat(medicine.infectionReduction);
                canonical.AppendFloat(medicine.detoxReduction);
                canonical.AppendFloat(medicine.painReduction);
                return;

            case PackagedLotItemFeature packaged:
                RequireEnum(packaged.tareDisposition,
                    "package tare disposition");
                RequireCanonicalOptional(packaged.containerItemId,
                    "package container item ID");
                canonical.Append("packaged-lot@1");
                canonical.Append(packaged.packageTareGrams);
                canonical.AppendEnum(packaged.tareDisposition);
                canonical.Append(packaged.containerItemId);
                return;

            case VaccineItemFeature vaccine:
                RequireCanonicalRequired(vaccine.diseaseId,
                    "vaccine disease ID");
                canonical.Append("vaccine@1");
                canonical.Append(vaccine.diseaseId);
                canonical.Append(vaccine.doses);
                return;

            case PathogenSampleItemFeature pathogen:
                RequireCanonicalRequired(pathogen.diseaseId,
                    "pathogen disease ID");
                canonical.Append("pathogen-sample@1");
                canonical.Append(pathogen.diseaseId);
                return;

            case MedicalProcedureSupplyItemFeature procedure:
                RequireCanonicalRequired(procedure.procedureId,
                    "medical procedure ID");
                canonical.Append("medical-procedure-supply@1");
                canonical.Append(procedure.procedureId);
                return;

            case CropTreatmentItemFeature treatment:
                RequireEnum(treatment.treatmentKind, "crop treatment kind");
                RequireFiniteRange(treatment.requiredWork, 0f,
                    float.MaxValue, "crop treatment work");
                RequireFiniteRange(treatment.effectAmount, 0f,
                    float.MaxValue, "crop treatment effect");
                canonical.Append("crop-treatment@1");
                canonical.AppendEnum(treatment.treatmentKind);
                canonical.Append(treatment.quantityPerApplication);
                canonical.AppendFloat(treatment.requiredWork);
                canonical.AppendFloat(treatment.effectAmount);
                canonical.Append(treatment.cooldownDays);
                return;

            case SubstanceItemFeature substance:
                RequireCanonicalRequired(substance.substanceId,
                    "substance ID");
                RequireEnum(substance.useClass, "substance use class");
                RequireFiniteRange(substance.addictionChance, 0f, 1f,
                    "substance addiction chance");
                RequireFiniteRange(substance.overdoseChance, 0f, 1f,
                    "substance overdose chance");
                RequireFiniteRange(substance.toleranceGain, 0f,
                    float.MaxValue, "substance tolerance gain");
                RequireFiniteRange(substance.withdrawalPerHour, 0f,
                    float.MaxValue, "substance withdrawal");
                RequireFiniteRange(substance.moodEffect, float.MinValue,
                    float.MaxValue, "substance mood effect");
                RequireFiniteRange(substance.workSpeedEffect, float.MinValue,
                    float.MaxValue, "substance work-speed effect");
                RequireFiniteRange(substance.combatEffect, float.MinValue,
                    float.MaxValue, "substance combat effect");
                RequireFiniteRange(substance.durationSeconds, 0f,
                    float.MaxValue, "substance duration");
                canonical.Append("substance@1");
                canonical.Append(substance.substanceId);
                canonical.AppendEnum(substance.useClass);
                canonical.AppendFloat(substance.addictionChance);
                canonical.AppendFloat(substance.overdoseChance);
                canonical.AppendFloat(substance.toleranceGain);
                canonical.AppendFloat(substance.withdrawalPerHour);
                canonical.AppendFloat(substance.moodEffect);
                canonical.AppendFloat(substance.workSpeedEffect);
                canonical.AppendFloat(substance.combatEffect);
                canonical.AppendFloat(substance.durationSeconds);
                return;

            case EvolutionCatalystItemFeature catalyst:
                RequireCanonicalOptional(catalyst.family,
                    "evolution catalyst family");
                canonical.Append("evolution-catalyst@1");
                canonical.Append(catalyst.family);
                canonical.Append(catalyst.potency);
                canonical.Append(catalyst.residue);
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

    private static void RequireCanonicalOptional(string value, string role)
    {
        if (value == null
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
