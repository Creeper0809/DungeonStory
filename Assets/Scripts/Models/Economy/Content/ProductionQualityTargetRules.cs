using System;
using System.Linq;

/// <summary>Only deterministic crafting-grade outputs implement this query.
/// Tier ordinals cross the Economy/scene boundary; no scene enum or RNG is read here.</summary>
public interface IProductionCraftQualityProjectionBridge
{
    bool TryResolveCraftQualityTier(ProductionOutputCapabilityDescriptor descriptor,
        float qualityModifier, float maximumScore, out int tier);
}

public static class ProductionQualityTargetRules
{
    public static bool IsValidTarget(int minimumTier) => minimumTier >= -1 && minimumTier <= 7;

    public static int CaptureFrozenTier(ProductionBillRecord record, IProductionAssemblyBridge bridge)
    {
        if (!record.outputOutcomeResolved || bridge is not IProductionCraftQualityProjectionBridge quality) return -1;
        int minimum = int.MaxValue;
        foreach (var output in record.resolvedOutputs)
        {
            var descriptor = bridge.CaptureOutputCapability(output.outputLineId, output.itemId);
            if (quality.TryResolveCraftQualityTier(descriptor, output.qualityModifier, 100f, out int tier))
                minimum = Math.Min(minimum, tier);
        }
        return minimum == int.MaxValue ? -1 : minimum;
    }

    public static int CaptureTier(ProductionRecipeSO recipe, ProductionFacilityHandle facility,
        IProductionAssemblyBridge bridge, IProductionOutputPlanningService planning, float maximumScore)
    {
        if (recipe == null || facility == null || bridge is not IProductionCraftQualityProjectionBridge quality)
            return -1; // Explicit absence of a grade capability, not a substitute grade.
        float modifier = planning.ResolveSupportModifier(facility, recipe,
            ProductionSupportModifierKind.Quality, 0f, multiply: false);
        int minimum = int.MaxValue;
        foreach (ProductionOutputDefinition output in recipe.CaptureCanonicalOutputs().Where(output =>
                     ProductionOutputRoleRules.IsPhysical(output.Role) && output.Amount > 0 && output.Probability > 0f))
        {
            var descriptor = bridge.CaptureOutputCapability(output.OutputLineId, output.ItemId);
            if (quality.TryResolveCraftQualityTier(descriptor, modifier, maximumScore, out int tier))
            {
                if (tier < 0 || tier > 7) throw new InvalidOperationException("Invalid production crafting grade projection.");
                minimum = Math.Min(minimum, tier);
            }
        }
        return minimum == int.MaxValue ? -1 : minimum;
    }

    public static DomainFailure Check(int minimumTier, int actualTier) => minimumTier < 0
        ? DomainFailure.None
        : actualTier < minimumTier
            ? new DomainFailure(FailureCode.QualityTargetUnreachable,
                "production-current-crafting-grade-below-target")
            : DomainFailure.None;
}
