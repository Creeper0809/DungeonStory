using System;
using System.Linq;

public static class EvolutionModuleFormulaDescriptor
{
    public static NarrativeFormulaCapabilityDescriptor ForOptionalDrawback(
        EvolutionModuleDefinition module,
        string conflictGroup,
        System.Collections.Generic.IEnumerable<string> forbiddenCapabilityIds = null)
    {
        if (module == null
            || module.BurdenKind != EvolutionModuleBurdenKind.OptionalDrawback)
            throw new ArgumentException(
                "An optional-drawback module is required.", nameof(module));
        string group = string.IsNullOrWhiteSpace(conflictGroup)
            ? throw new ArgumentException(
                "A drawback conflict group is required.", nameof(conflictGroup))
            : conflictGroup.Trim();
        string[] affinity = new[] { module.ModuleId, module.RoleTag }
            .Concat(module.NegativeEvidenceMarkers)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new NarrativeFormulaCapabilityDescriptor(
            module.ModuleId,
            new[]
            {
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.Magnitude,
                    1, module.MaximumDrawbackSeverity, 1, 0, 1),
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.Duration, 0, 0, 1, 0, 1),
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.Count, 1, 1, 1, 0, 1),
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.TargetCount, 1, 1, 1, 0, 1)
            },
            new[] { NarrativeFormulaParameterIds.Magnitude },
            baseCost: 1,
            triggerFrequencyCostPerUnit: 0,
            guaranteedProcCost: 0,
            areaCostPerExtraTarget: 0,
            multiEffectCostPerExtraEffect: 0,
            Array.Empty<NarrativeFormulaPairSynergyCost>(),
            narrativeAffinity: 1d,
            affinity,
            new[] { group },
            forbiddenCapabilityIds ?? module.ForbiddenSynergyModuleIds,
            module.ModuleId,
            module.ModuleId);
    }

    public static NarrativeFormulaModulePolarity ToNarrativePolarity(
        EvolutionModuleOfferPolarity value) => value switch
    {
        EvolutionModuleOfferPolarity.Positive => NarrativeFormulaModulePolarity.Positive,
        EvolutionModuleOfferPolarity.Drawback => NarrativeFormulaModulePolarity.Drawback,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
