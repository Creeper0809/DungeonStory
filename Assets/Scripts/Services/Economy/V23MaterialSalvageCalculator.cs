using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum DismantleTargetKind
{
    Apparel = 0,
    CombatEquipment = 1,
    GeneralFacility = 2,
    PrecisionIndustrialFacility = 3,
    ArcaneFacility = 4
}

public readonly struct MaterialSalvageResult
{
    public MaterialSalvageResult(
        float requiredWork,
        IReadOnlyList<ItemAmountDefinition> recoveredMaterials)
    {
        RequiredWork = Mathf.Max(0.1f, requiredWork);
        RecoveredMaterials = recoveredMaterials
            ?? Array.Empty<ItemAmountDefinition>();
    }

    public float RequiredWork { get; }
    public IReadOnlyList<ItemAmountDefinition> RecoveredMaterials { get; }
}

public interface IMaterialSalvageCalculator
{
    MaterialSalvageResult Calculate(
        DismantleTargetKind targetKind,
        float originalWork,
        IEnumerable<ItemAmountDefinition> originalInputs,
        float workerSkill);
}

public sealed class V23MaterialSalvageCalculator : IMaterialSalvageCalculator
{
    private readonly IMaterialEconomicProfileCatalog materials;

    public V23MaterialSalvageCalculator(
        IMaterialEconomicProfileCatalog materials)
    {
        this.materials = materials
            ?? throw new ArgumentNullException(nameof(materials));
    }

    public MaterialSalvageResult Calculate(
        DismantleTargetKind targetKind,
        float originalWork,
        IEnumerable<ItemAmountDefinition> originalInputs,
        float workerSkill)
    {
        (float workRatio, float baseRecovery) = targetKind switch
        {
            DismantleTargetKind.Apparel => (0.20f, 0.50f),
            DismantleTargetKind.CombatEquipment => (0.25f, 0.60f),
            DismantleTargetKind.GeneralFacility => (0.25f, 0.75f),
            DismantleTargetKind.PrecisionIndustrialFacility => (0.30f, 0.70f),
            DismantleTargetKind.ArcaneFacility => (0.35f, 0.65f),
            _ => (0.25f, 0.60f)
        };
        float skillAdjustment = Mathf.Lerp(
            -0.10f,
            0.10f,
            Mathf.Clamp01(workerSkill / 100f));
        List<ItemAmountDefinition> recovered = (originalInputs
                ?? Array.Empty<ItemAmountDefinition>())
            .Where(value => value != null
                && value.Amount > 0
                && !materials.IsConsumableDuringCraft(value.ItemId))
            .Select(value => new
            {
                value.ItemId,
                Amount = Mathf.FloorToInt(value.Amount * Mathf.Clamp01(
                    baseRecovery
                    * materials.GetSalvageRetention(value.ItemId) / 0.60f
                    + skillAdjustment))
            })
            .Where(value => value.Amount > 0)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(value => new ItemAmountDefinition(value.ItemId, value.Amount))
            .ToList();
        return new MaterialSalvageResult(
            Mathf.Max(0.1f, originalWork) * workRatio,
            recovered);
    }
}
