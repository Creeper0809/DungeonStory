using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface IBalanceWorkCalculator : IRecipeBalanceWorkCalculator
{
    float CalculateConstruction(BuildingSO building);
    float CalculateEquipment(
        CombatEquipmentDefinitionSO definition,
        string primaryMaterialItemId);
    float CalculateApparel(
        ApparelDefinitionSO definition,
        TextileMaterialDefinitionSO material,
        ApparelSizeClass size,
        ApparelModificationKind modifications);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class V23BalanceWorkCalculator : IBalanceWorkCalculator
{
    private readonly IMaterialEconomicProfileCatalog materials;

    public V23BalanceWorkCalculator(IMaterialEconomicProfileCatalog materials)
    {
        this.materials = materials
            ?? throw new ArgumentNullException(nameof(materials));
    }

    public float CalculateConstruction(BuildingSO building)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        return CalculateConstruction(
            building,
            building.GetConstructionMaterials());
    }

    public float CalculateConstruction(
        BuildingSO building,
        IEnumerable<ItemAmountDefinition> constructionMaterials)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        ConstructionBalanceClass balanceClass = ResolveConstructionClass(building);
        float baseWork = balanceClass switch
        {
            ConstructionBalanceClass.Structure => 20f,
            ConstructionBalanceClass.Decoration => 28f,
            ConstructionBalanceClass.Furnishing => 32f,
            ConstructionBalanceClass.Storage => 48f,
            ConstructionBalanceClass.Workstation => 110f,
            ConstructionBalanceClass.Service => 130f,
            ConstructionBalanceClass.Environment => 160f,
            ConstructionBalanceClass.Defense => 180f,
            ConstructionBalanceClass.Medical => 200f,
            ConstructionBalanceClass.Precision => 230f,
            ConstructionBalanceClass.Industrial => 280f,
            ConstructionBalanceClass.Arcane => 360f,
            ConstructionBalanceClass.Landmark => 900f,
            _ => 32f
        };
        int cells = Mathf.Max(1, building.width) * Mathf.Max(1, building.height);
        float footprint = Mathf.Clamp(1f + 0.30f * (cells - 1), 1f, 2.5f);
        int additionalCapabilities = Mathf.Max(0, (building.Abilities?.Count ?? 0) - 1);
        float capability = Mathf.Clamp(
            1f + 0.10f * additionalCapabilities,
            1f,
            1.5f);
        float materialFactor = WeightedMaterialFactor(constructionMaterials);
        return RoundTo(baseWork * footprint * capability * materialFactor, 4f);
    }

    public float CalculateRecipe(
        ProductionRecipeSO recipe) => CalculateRecipe(
        recipe,
        ResolveProductionProcessClass(recipe));

    public float CalculateRecipe(
        ProductionRecipeSO recipe,
        ProductionProcessClass processClass)
    {
        if (recipe == null)
        {
            throw new ArgumentNullException(nameof(recipe));
        }

        float baseWork = CalculateRecipeBaseWork(recipe, processClass);
        return RoundTo(
            baseWork * WeightedMaterialFactor(recipe.Inputs),
            2f);
    }

    public static float CalculateRecipeBaseWork(
        ProductionRecipeSO recipe,
        ProductionProcessClass processClass)
    {
        if (recipe == null)
        {
            throw new ArgumentNullException(nameof(recipe));
        }

        float processWork = processClass switch
        {
            ProductionProcessClass.Gathering => 4f,
            ProductionProcessClass.CuttingGrindingWashing => 8f,
            ProductionProcessClass.CookingSimpleMixing => 10f,
            ProductionProcessClass.SpinningWeavingWoodworking => 12f,
            ProductionProcessClass.ForgingHeavyAssembly => 18f,
            ProductionProcessClass.Chemical => 22f,
            ProductionProcessClass.Precision => 28f,
            ProductionProcessClass.Medical => 30f,
            ProductionProcessClass.Rune => 36f,
            ProductionProcessClass.HeavyIndustrial => 44f,
            _ => 10f
        };
        // A reusable package is physical custody, not an additional processing
        // operation. Counting it as another ingredient would make adding an
        // exact tare-return contract silently increase recurring recipe labor.
        // This also preserves the frozen pre-package V23 work authority.
        int inputKinds = recipe.Inputs.Count(input =>
            input != null
            && !input.ItemId.StartsWith("container:", StringComparison.Ordinal));
        int outputKinds = recipe.Outputs.Count;
        float expectedOutput = Mathf.Max(1f, recipe.Outputs.Sum(output =>
            output.Amount * output.Probability));
        float assemblyComplexity = Mathf.Max(0, recipe.RequiredSupportTags.Count) * 4f
            + (recipe.ProcessKind == ProductionProcessKind.PassiveBatch ? 6f : 0f)
            + (recipe.CleanWaterPerCycle > 0f ? 3f : 0f)
            + (recipe.WastewaterPerCycle > 0f ? 3f : 0f);
        float direct = processWork
            + Mathf.Max(0, inputKinds - 1) * 3f
            + Mathf.Max(0, outputKinds - 1) * 2f
            + assemblyComplexity;
        return RoundTo(direct * Mathf.Pow(expectedOutput, 0.65f), 2f);
    }

    public float CalculateEquipment(
        CombatEquipmentDefinitionSO definition,
        string primaryMaterialItemId)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        float form = ResolveEquipmentFormWork(definition);
        int componentUnits = definition.RequiredComponentInputs.Sum(value => value.Amount);
        int precisionStage = Mathf.Clamp(
            definition.Tier
            + (definition.Era >= EquipmentEra.MatureIndustrial ? 1 : 0)
            + (definition.Era == EquipmentEra.RuneAbyssal ? 1 : 0),
            0,
            4);
        float materialFactor = materials.GetWorkFactor(primaryMaterialItemId);
        return RoundTo(
            (form
             + definition.PrimaryMaterialAmount * 8f
             + componentUnits * 12f
             + precisionStage * 16f)
            * materialFactor,
            4f);
    }

    public float CalculateApparel(
        ApparelDefinitionSO definition,
        TextileMaterialDefinitionSO material,
        ApparelSizeClass size,
        ApparelModificationKind modifications)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }
        if (material == null)
        {
            throw new ArgumentNullException(nameof(material));
        }

        int occupied = CountBits((uint)definition.OccupiedPoints);
        int area = Mathf.Clamp(Mathf.CeilToInt(definition.TailoringCoefficient), 1, 5);
        float modificationWork = 0f;
        if ((modifications & ApparelModificationKind.TailOpening) != 0)
            modificationWork += 4f;
        if ((modifications & ApparelModificationKind.WingSlits) != 0)
            modificationWork += 8f;
        if ((modifications & ApparelModificationKind.HornClearance) != 0)
            modificationWork += 3f;
        float sizeFactor = size switch
        {
            ApparelSizeClass.Small => 0.75f,
            ApparelSizeClass.Large => 1.30f,
            _ => 1f
        };
        return RoundTo(
            (10f + area * 12f + occupied * 4f + modificationWork)
            * sizeFactor
            * materials.GetWorkFactor(material.PhysicalItemId),
            2f);
    }

    public static ConstructionBalanceClass ResolveConstructionClass(
        BuildingSO building)
    {
        if (building != null && building.id is >= 9201 and <= 9209)
            return ConstructionBalanceClass.Landmark;
        if (building?.GetAbility<BuildingArcaneSurgeryAbility>() != null)
            return ConstructionBalanceClass.Arcane;
        if (building?.GetAbility<BuildingMedicalAbility>() != null
            || building?.Abilities?.OfType<ISurgicalFacilityAbility>().Any() == true)
        {
            return ConstructionBalanceClass.Medical;
        }
        if (building?.ResearchFacilityCommand is
            ResearchFacilityCommandKind.ResonanceTuning)
            return ConstructionBalanceClass.Arcane;
        if (building?.ResearchFacilityCommand is
            ResearchFacilityCommandKind.AgingAssessment
            or ResearchFacilityCommandKind.BiologicalAgeMeasurement
            or ResearchFacilityCommandKind.GeriatricCare
            or ResearchFacilityCommandKind.ChronicCare
            or ResearchFacilityCommandKind.PathogenDiagnosis
            or ResearchFacilityCommandKind.Serology)
            return ConstructionBalanceClass.Medical;

        return building?.EffectiveUseClassification switch
        {
            FacilityUseClassification.Structure => ConstructionBalanceClass.Structure,
            FacilityUseClassification.Storage => ConstructionBalanceClass.Storage,
            FacilityUseClassification.Production => ConstructionBalanceClass.Workstation,
            FacilityUseClassification.Service => ConstructionBalanceClass.Service,
            FacilityUseClassification.Environment => ConstructionBalanceClass.Environment,
            FacilityUseClassification.Logistics => ConstructionBalanceClass.Industrial,
            FacilityUseClassification.Combat => ConstructionBalanceClass.Defense,
            FacilityUseClassification.DomainCommand => ConstructionBalanceClass.Precision,
            FacilityUseClassification.EventVenue => ConstructionBalanceClass.Service,
            FacilityUseClassification.Decoration => ConstructionBalanceClass.Decoration,
            _ => ConstructionBalanceClass.Furnishing
        };
    }

    public static ProductionProcessClass ResolveProductionProcessClass(
        ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (!recipe.HasAuthoredProcessClass)
        {
            throw new InvalidOperationException(
                $"Recipe '{recipe.RecipeId}' has no authored production process class.");
        }
        return recipe.ProcessClass;
    }

    private float WeightedMaterialFactor(IEnumerable<ItemAmountDefinition> inputs)
    {
        ItemAmountDefinition[] values = (inputs ?? Array.Empty<ItemAmountDefinition>())
            .Where(value => value != null && value.Amount > 0)
            .ToArray();
        int total = values.Sum(value => value.Amount);
        return total <= 0
            ? 1f
            : values.Sum(value => materials.GetWorkFactor(value.ItemId) * value.Amount)
                / total;
    }

    private static float ResolveEquipmentFormWork(
        CombatEquipmentDefinitionSO definition)
    {
        if (definition.Era == EquipmentEra.RuneAbyssal)
            return 150f;
        if (definition is CombatWeaponSO weapon)
        {
            if (weapon.GunpowderWeapon) return 70f;
            if (weapon.Kind == CombatEquipmentKind.RangedWeapon)
                return definition.OccupiedHands >= 2 || definition.Weight >= 4f
                    ? 55f
                    : 40f;
            if (definition.Weight <= 1.2f) return 20f;
            if (definition.OccupiedHands >= 2 || definition.Weight >= 4f) return 55f;
            return 30f;
        }
        if (definition.Kind == CombatEquipmentKind.Shield)
            return definition.Weight >= 6f ? 110f : 55f;
        if (definition.Kind == CombatEquipmentKind.Armor)
        {
            if (definition.Weight < 4f) return 45f;
            if (definition.Weight < 8f) return 75f;
            return 110f;
        }
        return 30f;
    }

    private static int CountBits(uint value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
    }

    private static float RoundTo(float value, float step) =>
        Mathf.Max(step, Mathf.Round(Mathf.Max(0f, value) / step) * step);
}
