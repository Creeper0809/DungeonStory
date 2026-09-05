using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class ItemFeatureDefinition
{
    public abstract string FeatureId { get; }

    // Fail closed for future feature kinds. A feature may opt into the generic
    // definition-only production codec only when it introduces no per-instance
    // physical state at output creation time.
    public virtual bool RequiresProductionOutputInstanceState => true;

    public virtual IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        yield break;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionItemFeature : ItemFeatureDefinition
{
    public ResourceItemKind kind;
    public ResourceIngredientTag ingredientTags;
    public bool sharedIntermediate;

    public override string FeatureId => "production";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (sharedIntermediate && kind != ResourceItemKind.Intermediate)
        {
            yield return "Shared intermediates must use ResourceItemKind.Intermediate.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MarketItemFeature : ItemFeatureDefinition
{
    [Range(0f, 1f)] public float saleRate = 0.6f;
    public override string FeatureId => "market";
    public override bool RequiresProductionOutputInstanceState => false;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchGateItemFeature : ItemFeatureDefinition
{
    public string requiredResearchId = string.Empty;
    public override string FeatureId => "research-gate";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (string.IsNullOrWhiteSpace(requiredResearchId))
        {
            yield return "Research-gate feature has no research ID.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class FoodItemFeature : ItemFeatureDefinition
{
    public MealQualityTier quality = MealQualityTier.Simple;
    public MealQualityBand qualityBand = MealQualityBand.Simple;
    public MealServingRole servingRole = MealServingRole.FullMeal;
    [Min(0f)] public float nutrition;
    public float mood;
    [Min(0f)] public float freshnessSeconds;
    public bool preserved;

    public override string FeatureId => "food";
    public override bool RequiresProductionOutputInstanceState =>
        freshnessSeconds > 0f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MedicineItemFeature : ItemFeatureDefinition
{
    public bool supportsInjuryTreatment;
    [Min(0.1f)] public float treatmentPotency = 1f;
    [Min(0f)] public float infectionReduction;
    [Min(0f)] public float detoxReduction;
    [Min(0f)] public float painReduction;

    public override string FeatureId => "medicine";
    public override bool RequiresProductionOutputInstanceState => false;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class PackagedLotItemFeature : ItemFeatureDefinition
{
    [Min(1)] public int packageTareGrams = 1;
    public PackageTareDisposition tareDisposition =
        PackageTareDisposition.ReusableContainerReturn;
    public string containerItemId = string.Empty;

    public override string FeatureId => "packaged-lot";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (packageTareGrams <= 0)
        {
            yield return "Packaged lot tare mass must be positive.";
        }
        if (tareDisposition is PackageTareDisposition.None
            or PackageTareDisposition.BulkInfrastructureNotInUnit)
        {
            yield return "Packaged lot requires a physical tare disposition.";
        }

        bool requiresPhysicalOutput = tareDisposition is
            PackageTareDisposition.ReusableContainerReturn
            or PackageTareDisposition.DisposableWasteByproduct
            or PackageTareDisposition.TransferredWithOutput;
        if (requiresPhysicalOutput
            && (string.IsNullOrWhiteSpace(containerItemId)
                || !string.Equals(
                    containerItemId,
                    containerItemId.Trim(),
                    StringComparison.Ordinal)))
        {
            yield return "Packaged lot physical tare output requires a canonical item ID.";
        }
        if (owner != null
            && string.Equals(owner.ItemId, containerItemId, StringComparison.Ordinal))
        {
            yield return "Packaged lot cannot return itself as its tare output.";
        }

        long totalUnitGrams = 0L;
        bool canonicalMass = owner != null;
        if (canonicalMass)
        {
            try
            {
                totalUnitGrams = PhysicalMassGrams
                    .FromCanonicalKilograms(owner.UnitWeight)
                    .Value;
            }
            catch (Exception exception) when (
                exception is ArgumentOutOfRangeException
                || exception is InvalidOperationException
                || exception is OverflowException)
            {
                canonicalMass = false;
            }
        }
        if (!canonicalMass)
        {
            yield return "Packaged lot owner has no canonical gram mass.";
            yield break;
        }
        if (packageTareGrams >= totalUnitGrams)
        {
            yield return "Packaged lot tare must be smaller than total unit mass.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class VaccineItemFeature : ItemFeatureDefinition
{
    public string diseaseId = string.Empty;
    [Min(1)] public int doses = 1;

    public override string FeatureId => "vaccine";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (string.IsNullOrWhiteSpace(diseaseId))
        {
            yield return "Vaccine feature has no disease ID.";
        }
        if (doses < 1)
        {
            yield return "Vaccine feature must contain at least one dose.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class PathogenSampleItemFeature : ItemFeatureDefinition
{
    public string diseaseId = string.Empty;

    public override string FeatureId => "pathogen-sample";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (string.IsNullOrWhiteSpace(diseaseId))
        {
            yield return "Pathogen sample feature has no disease ID.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MedicalProcedureSupplyItemFeature : ItemFeatureDefinition
{
    public string procedureId = string.Empty;
    public override string FeatureId => "medical-procedure-supply";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (string.IsNullOrWhiteSpace(procedureId))
        {
            yield return "Medical procedure supply has no procedure ID.";
        }
    }
}

public enum CropTreatmentKind
{
    PestLure = 0,
    BotanicalPesticide = 1,
    Fungicide = 2
}

public readonly struct CropTreatmentPolicy
{
    public CropTreatmentPolicy(
        CropTreatmentKind kind,
        int quantityPerApplication,
        float requiredWork,
        float effectAmount,
        int cooldownDays)
    {
        Kind = kind;
        QuantityPerApplication = quantityPerApplication;
        RequiredWork = requiredWork;
        EffectAmount = effectAmount;
        CooldownDays = cooldownDays;
    }

    public CropTreatmentKind Kind { get; }
    public int QuantityPerApplication { get; }
    public float RequiredWork { get; }
    public float EffectAmount { get; }
    public int CooldownDays { get; }
    public bool IsValid => QuantityPerApplication > 0
        && RequiredWork > 0f
        && !float.IsNaN(RequiredWork)
        && !float.IsInfinity(RequiredWork)
        && EffectAmount > 0f
        && !float.IsNaN(EffectAmount)
        && !float.IsInfinity(EffectAmount)
        && CooldownDays >= 0;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CropTreatmentItemFeature : ItemFeatureDefinition
{
    public CropTreatmentKind treatmentKind;
    [Min(1)] public int quantityPerApplication = 1;
    [Min(0.1f)] public float requiredWork = 1f;
    [Min(0.1f)] public float effectAmount = 1f;
    [Min(0)] public int cooldownDays = 1;
    public override string FeatureId => "crop-treatment";
    public override bool RequiresProductionOutputInstanceState => false;

    public CropTreatmentPolicy ToPolicy() => new(
        treatmentKind,
        quantityPerApplication,
        requiredWork,
        effectAmount,
        cooldownDays);

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (!Enum.IsDefined(typeof(CropTreatmentKind), treatmentKind))
            yield return "Crop treatment kind is invalid.";
        if (!ToPolicy().IsValid)
            yield return "Crop treatment quantity, work, effect, or cooldown is invalid.";
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SubstanceItemFeature : ItemFeatureDefinition
{
    public string substanceId = string.Empty;
    public SubstanceUseClass useClass;
    [Range(0f, 1f)] public float addictionChance;
    [Range(0f, 1f)] public float overdoseChance;
    [Min(0f)] public float toleranceGain;
    [Min(0f)] public float withdrawalPerHour;
    public float moodEffect;
    public float workSpeedEffect;
    public float combatEffect;
    [Min(1f)] public float durationSeconds = 120f;

    public override string FeatureId => "substance";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (owner is not ResourceItemDefinitionSO)
        {
            yield return "Substance features require a resource-item definition owner.";
        }

        string normalizedId = substanceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            yield return "Substance feature has no stable substance ID.";
        }
        if (durationSeconds < 1f)
        {
            yield return "Substance duration must be at least one second.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class FacilitySupplyItemFeature : ItemFeatureDefinition
{
    [Min(0f)] public float fuelValue;
    [Min(0f)] public float nutritionValue;
    public bool feedEligible;

    public override string FeatureId => "facility-supply";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (fuelValue <= 0f && (!feedEligible || nutritionValue <= 0f))
        {
            yield return "Facility-supply feature provides neither fuel nor feed value.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class EquipmentItemFeature : ItemFeatureDefinition
{
    public string equipmentDefinitionId = string.Empty;
    public override string FeatureId => "equipment";

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (string.IsNullOrWhiteSpace(equipmentDefinitionId))
        {
            yield return "Equipment feature has no equipment definition ID.";
        }

        if (owner.MaxStack != 1)
        {
            yield return "Equipment items must have a max stack of one.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AmmunitionItemFeature : ItemFeatureDefinition
{
    public string ammunitionKindId = string.Empty;
    public override string FeatureId => "ammunition";
    public override bool RequiresProductionOutputInstanceState => true;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (string.IsNullOrWhiteSpace(ammunitionKindId)
            || !string.Equals(
                ammunitionKindId,
                ammunitionKindId.Trim(),
                StringComparison.Ordinal))
        {
            yield return "Ammunition feature has a noncanonical ammunition kind ID.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class InstallationItemFeature : ItemFeatureDefinition
{
    public int buildingDefinitionId = -1;
    public override string FeatureId => "facility-installation";

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (buildingDefinitionId < 0)
        {
            yield return "Facility-installation feature has no building definition ID.";
        }

        if (owner.MaxStack != 1)
        {
            yield return "Facility installation kits must have a max stack of one.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BlueprintItemFeature : ItemFeatureDefinition
{
    public int blueprintDefinitionId = -1;
    public string targetResearchId = string.Empty;
    public override string FeatureId => "research-blueprint";

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (blueprintDefinitionId < 0)
        {
            yield return "Research-blueprint feature has no blueprint definition ID.";
        }

        if (owner.MaxStack != 1)
        {
            yield return "Research blueprints must have a max stack of one.";
        }
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class EvolutionCatalystItemFeature : ItemFeatureDefinition
{
    public const int MaximumPotency = 5;

    public string family = string.Empty;
    [Range(1, MaximumPotency)] public int potency = 1;
    public bool residue;
    public override string FeatureId => "evolution-catalyst";
    public override bool RequiresProductionOutputInstanceState => false;

    public override IEnumerable<string> Validate(ItemDefinitionSO owner)
    {
        if (!residue && string.IsNullOrWhiteSpace(family))
        {
            yield return "Evolution catalyst has no family.";
        }

        if (potency < 1 || potency > MaximumPotency)
        {
            yield return $"Evolution catalyst potency must be 1-{MaximumPotency}.";
        }
    }
}

/// <summary>
/// Canonical immutable authoring definition for every physical item. Optional behavior is
/// composed through feature records; per-instance mutable state never belongs on this asset.
/// </summary>
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class ItemDefinitionSO : DataScriptableObject
{
    public const string UnifiedResourcePath = "SO";

    [Header("Identity")]
    [SerializeField] private string itemId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;

    [Header("Physical")]
    [SerializeField] private StockCategory stockCategory = StockCategory.General;
    [Min(0.01f), SerializeField] private float unitWeight = 1f;
    [Min(1), SerializeField] private int maxStack = 75;
    [SerializeField] private Sprite sprite;

    [Header("Economy")]
    [Min(0), SerializeField] private int unitPrice = 1;

    [Header("Capabilities")]
    [SerializeReference] private List<ItemFeatureDefinition> features = new();

    public ItemDefinitionId StableId => new(itemId);
    public string ItemId => StableId.Value;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public StockCategory StockCategory => stockCategory;
    public int UnitPrice => Mathf.Max(0, unitPrice);
    public float UnitWeight => Mathf.Max(0.01f, unitWeight);
    public int MaxStack => Mathf.Max(1, maxStack);
    public Sprite Sprite => sprite;
    public IReadOnlyList<ItemFeatureDefinition> Features => features;

    // Semantic capture reads authored values before gameplay accessors clamp or
    // normalize them, so malformed authority fails instead of hashing a repair.
    internal string AuthoredItemId => itemId;
    internal StockCategory AuthoredStockCategory => stockCategory;
    internal float AuthoredUnitWeight => unitWeight;
    internal int AuthoredMaxStack => maxStack;
    internal int AuthoredUnitPrice => unitPrice;

    public bool TryGetFeature<T>(out T feature) where T : ItemFeatureDefinition
    {
        feature = features?.OfType<T>().FirstOrDefault();
        return feature != null;
    }

    public T GetFeatureOrDefault<T>() where T : ItemFeatureDefinition =>
        features?.OfType<T>().FirstOrDefault();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (!StableId.IsValid)
        {
            errors.Add("Item definition has no stable ID.");
        }

        foreach (IGrouping<string, ItemFeatureDefinition> duplicate in (features ?? new())
                     .Where(feature => feature != null)
                     .GroupBy(feature => feature.FeatureId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Duplicate feature '{duplicate.Key}'.");
        }

        foreach (ItemFeatureDefinition feature in features ?? new())
        {
            if (feature == null)
            {
                errors.Add("Null item feature.");
                continue;
            }

            errors.AddRange(feature.Validate(this).Select(message => $"{feature.FeatureId}: {message}"));
        }

        return errors;
    }

    public DungeonItemDefinition ToDungeonItemDefinition()
    {
        string equipmentId = TryGetFeature(out EquipmentItemFeature equipment)
            ? equipment.equipmentDefinitionId?.Trim() ?? string.Empty
            : string.Empty;
        PackagedLotItemFeature packagedLot =
            GetFeatureOrDefault<PackagedLotItemFeature>();
        return new DungeonItemDefinition(
            ItemId,
            DisplayName,
            Description,
            StockCategory,
            UnitPrice,
            Sprite,
            UnitWeight,
            MaxStack,
            equipmentId,
            this is ResourceItemDefinitionSO resource
                ? resource.Kind
                : ResourceItemKind.Raw,
            packagedLot?.packageTareGrams ?? 0,
            packagedLot?.tareDisposition ?? PackageTareDisposition.None,
            packagedLot?.containerItemId ?? string.Empty);
    }

#if UNITY_EDITOR
    public void ConfigureUnitPrice(int price)
    {
        unitPrice = Mathf.Max(0, price);
    }

    public void ConfigureCore(
        string stableId,
        string name,
        string itemDescription,
        StockCategory category,
        int price,
        float weight,
        int stackLimit,
        Sprite icon = null)
    {
        itemId = ItemDefinitionId.Normalize(stableId);
        displayName = name?.Trim() ?? string.Empty;
        description = itemDescription?.Trim() ?? string.Empty;
        stockCategory = category;
        unitPrice = Mathf.Max(0, price);
        unitWeight = Mathf.Max(0.01f, weight);
        maxStack = Mathf.Max(1, stackLimit);
        sprite = icon;
    }

    public void SetFeature<T>(T feature) where T : ItemFeatureDefinition
    {
        features ??= new List<ItemFeatureDefinition>();
        features.RemoveAll(existing => existing is T);
        if (feature != null)
        {
            features.Add(feature);
        }
    }

    public void RemoveFeature<T>() where T : ItemFeatureDefinition
    {
        features?.RemoveAll(existing => existing is T);
    }
#endif
}
