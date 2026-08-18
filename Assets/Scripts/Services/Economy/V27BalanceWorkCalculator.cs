using System;
using UnityEngine;

/// <summary>
/// V27 runtime labor authority. The V23 calculator remains the frozen Before
/// model. Construction uses the approved per-building authored WU selected by
/// the V27 integer WU/BOM redistribution audit; recurring production uses its
/// authored batch WU. Re-deriving either value here would create a second
/// gameplay authority and make the ledger disagree with live work orders.
/// </summary>
public sealed class V27BalanceWorkCalculator : IBalanceWorkCalculator
{
    public const float EffectiveLaborScale = 2.25f;

    private readonly V23BalanceWorkCalculator before;

    public V27BalanceWorkCalculator(IMaterialEconomicProfileCatalog materials)
    {
        before = new V23BalanceWorkCalculator(
            materials ?? throw new ArgumentNullException(nameof(materials)));
    }

    public float CalculateConstruction(BuildingSO building)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        BuildingWorkAmountAbility ability =
            building.GetAbility<BuildingWorkAmountAbility>()
            ?? throw new InvalidOperationException(
                $"Building '{building.ContentDefinitionId}' has no authored construction WU authority.");
        float work = ability.constructionWorkRequired;
        if (float.IsNaN(work) || float.IsInfinity(work) || work <= 0f)
        {
            throw new InvalidOperationException(
                $"Building '{building.ContentDefinitionId}' has invalid authored construction WU {work}.");
        }
        return work;
    }

    public float CalculateRecipe(ProductionRecipeSO recipe) =>
        RequireRecurringWork(recipe);

    public float CalculateRecipe(
        ProductionRecipeSO recipe,
        ProductionProcessClass processClass) =>
        RequireRecurringWork(recipe);

    public float CalculateEquipment(
        CombatEquipmentDefinitionSO definition,
        string primaryMaterialItemId) =>
        before.CalculateEquipment(definition, primaryMaterialItemId);

    public float CalculateApparel(
        ApparelDefinitionSO definition,
        TextileMaterialDefinitionSO material,
        ApparelSizeClass size,
        ApparelModificationKind modifications) =>
        before.CalculateApparel(definition, material, size, modifications);

    public static float ScaleRequiredWork(float beforeWork)
    {
        if (float.IsNaN(beforeWork)
            || float.IsInfinity(beforeWork)
            || beforeWork <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(beforeWork),
                "V27 labor authority requires finite positive Before work.");
        }

        float scaled = beforeWork * EffectiveLaborScale;
        if (float.IsNaN(scaled) || float.IsInfinity(scaled) || scaled <= 0f)
        {
            throw new OverflowException("V27 labor scaling overflowed.");
        }
        return scaled;
    }

    public static float RequireRecurringWork(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        float work = recipe.RequiredWork;
        if (float.IsNaN(work) || float.IsInfinity(work) || work <= 0f)
        {
            throw new InvalidOperationException(
                $"Recurring recipe '{recipe.RecipeId}' has invalid authored WU {work}.");
        }
        return work;
    }
}
