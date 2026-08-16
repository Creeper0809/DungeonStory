using System;
using UnityEngine;

/// <summary>
/// V27 runtime labor authority. The V23 calculator remains the frozen Before
/// model; production recipes and construction pay the period-preserving
/// 45 / 20 scale exactly once. Equipment and apparel remain on their frozen
/// Before authority until their later vertical slices are approved.
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

    public float CalculateConstruction(BuildingSO building) =>
        ScaleRequiredWork(before.CalculateConstruction(building));

    public float CalculateRecipe(ProductionRecipeSO recipe) =>
        ScaleRequiredWork(before.CalculateRecipe(recipe));

    public float CalculateRecipe(
        ProductionRecipeSO recipe,
        ProductionProcessClass processClass) =>
        ScaleRequiredWork(before.CalculateRecipe(recipe, processClass));

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
}
