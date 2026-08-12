using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ItemDefinitionId : IEquatable<ItemDefinitionId>
{
    private readonly string value;

    public ItemDefinitionId(string value)
    {
        this.value = Normalize(value);
    }

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public bool Equals(ItemDefinitionId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is ItemDefinitionId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value;

    public static explicit operator ItemDefinitionId(string value) => new(value);

    public static string Normalize(string value) => value?.Trim() ?? string.Empty;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class ItemFeatureDefinition
{
    public abstract string FeatureId { get; }

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
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchGateItemFeature : ItemFeatureDefinition
{
    public string requiredResearchId = string.Empty;
    public override string FeatureId => "research-gate";

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
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class VaccineItemFeature : ItemFeatureDefinition
{
    public string diseaseId = string.Empty;
    [Min(1)] public int doses = 1;

    public override string FeatureId => "vaccine";

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

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CropTreatmentItemFeature : ItemFeatureDefinition
{
    public CropTreatmentKind treatmentKind;
    public override string FeatureId => "crop-treatment";
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
        return new DungeonItemDefinition(
            ItemId,
            DisplayName,
            Description,
            StockCategory,
            UnitPrice,
            Sprite,
            UnitWeight,
            MaxStack,
            equipmentId);
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
