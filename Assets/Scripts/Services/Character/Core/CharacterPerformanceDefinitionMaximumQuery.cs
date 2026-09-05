using System;
using System.Linq;

public readonly struct CharacterPerformanceDefinitionMaximumSnapshot
{
    public CharacterPerformanceDefinitionMaximumSnapshot(
        string formulaId,
        double maximumValue,
        double functionalCapacityMaximum,
        double proficiencyMaximum,
        double gameplayEffectMaximum,
        string sourceDigest)
    {
        if (string.IsNullOrWhiteSpace(formulaId)
            || !string.Equals(formulaId, formulaId.Trim(), StringComparison.Ordinal)
            || double.IsNaN(maximumValue)
            || double.IsInfinity(maximumValue)
            || maximumValue < 0d
            || double.IsNaN(functionalCapacityMaximum)
            || double.IsInfinity(functionalCapacityMaximum)
            || functionalCapacityMaximum < 0d
            || double.IsNaN(proficiencyMaximum)
            || double.IsInfinity(proficiencyMaximum)
            || proficiencyMaximum < 0d
            || double.IsNaN(gameplayEffectMaximum)
            || double.IsInfinity(gameplayEffectMaximum)
            || gameplayEffectMaximum < 0d
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Character performance definition maximum is invalid.");
        }

        FormulaId = formulaId;
        MaximumValue = maximumValue;
        FunctionalCapacityMaximum = functionalCapacityMaximum;
        ProficiencyMaximum = proficiencyMaximum;
        GameplayEffectMaximum = gameplayEffectMaximum;
        SourceDigest = sourceDigest;
    }

    public string FormulaId { get; }
    public double MaximumValue { get; }
    public double FunctionalCapacityMaximum { get; }
    public double ProficiencyMaximum { get; }
    public double GameplayEffectMaximum { get; }
    public string SourceDigest { get; }
}

public interface ICharacterPerformanceDefinitionMaximumQuery
{
    CharacterPerformanceDefinitionMaximumSnapshot Capture(string formulaId);
}

public readonly struct CharacterFunctionalCapacityDefinitionBoundsSnapshot
{
    public CharacterFunctionalCapacityDefinitionBoundsSnapshot(
        CharacterFunctionalCapacityId capacityId,
        double rawStateMaximum,
        double projectedMaximum,
        string sourceDigest)
    {
        if (double.IsNaN(rawStateMaximum)
            || double.IsInfinity(rawStateMaximum)
            || rawStateMaximum < 0d
            || double.IsNaN(projectedMaximum)
            || double.IsInfinity(projectedMaximum)
            || projectedMaximum < rawStateMaximum
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Functional-capacity definition bounds are invalid.");
        }
        CapacityId = capacityId;
        RawStateMaximum = rawStateMaximum;
        ProjectedMaximum = projectedMaximum;
        SourceDigest = sourceDigest;
    }

    public CharacterFunctionalCapacityId CapacityId { get; }
    public double RawStateMaximum { get; }
    public double ProjectedMaximum { get; }
    public string SourceDigest { get; }
}

public interface ICharacterFunctionalCapacityDefinitionBoundsQuery
{
    CharacterFunctionalCapacityDefinitionBoundsSnapshot Capture(
        CharacterFunctionalCapacityId capacityId);
}

public sealed class CharacterFunctionalCapacityDefinitionBoundsQuery :
    ICharacterFunctionalCapacityDefinitionBoundsQuery
{
    public const string Schema =
        "character-functional-capacity-definition-bounds@1";

    private readonly IGameplayEffectResultBoundsQuery effectBounds;

    public CharacterFunctionalCapacityDefinitionBoundsQuery(
        IGameplayEffectResultBoundsQuery effectBounds)
    {
        this.effectBounds = effectBounds
            ?? throw new ArgumentNullException(nameof(effectBounds));
    }

    public CharacterFunctionalCapacityDefinitionBoundsSnapshot Capture(
        CharacterFunctionalCapacityId capacityId)
    {
        string targetId = CharacterFunctionalCapacityIds.GetStableId(capacityId);
        double rawStateMaximum =
            CharacterAnatomyStateBounds.MaximumFunctionalEfficiency;
        double effectMaximum = effectBounds.RequireFiniteMaximum(targetId);
        double projectedMaximum = Math.Max(rawStateMaximum, effectMaximum);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.AppendEnum(capacityId);
        digest.Append(targetId);
        digest.AppendFloat(
            CharacterAnatomyStateBounds.MaximumInstalledPartEfficiency);
        digest.AppendFloat(CharacterAnatomyStateBounds.MinimumModuleBonus);
        digest.AppendFloat(CharacterAnatomyStateBounds.MaximumModuleBonus);
        digest.AppendDouble(rawStateMaximum);
        digest.AppendDouble(effectMaximum);
        digest.AppendDouble(projectedMaximum);
        return new CharacterFunctionalCapacityDefinitionBoundsSnapshot(
            capacityId,
            rawStateMaximum,
            projectedMaximum,
            digest.ComputeSha256());
    }
}

/// <summary>
/// Execution-free upper bound for an authored performance formula. Capacity,
/// proficiency and gameplay-effect maxima are read from the same definitions
/// used by runtime evaluation; no actor or content ID switch participates.
/// </summary>
public sealed class CharacterPerformanceDefinitionMaximumQuery :
    ICharacterPerformanceDefinitionMaximumQuery
{
    public const string Schema =
        "character-performance-definition-maximum@1";

    private readonly CharacterPerformanceFormulaCatalog formulas;
    private readonly IGameplayEffectResultBoundsQuery effectBounds;
    private readonly ICharacterFunctionalCapacityDefinitionBoundsQuery
        capacityBounds;

    public CharacterPerformanceDefinitionMaximumQuery(
        CharacterPerformanceFormulaCatalog formulas,
        IGameplayEffectResultBoundsQuery effectBounds,
        ICharacterFunctionalCapacityDefinitionBoundsQuery capacityBounds)
    {
        this.formulas = formulas
            ?? throw new ArgumentNullException(nameof(formulas));
        this.effectBounds = effectBounds
            ?? throw new ArgumentNullException(nameof(effectBounds));
        this.capacityBounds = capacityBounds
            ?? throw new ArgumentNullException(nameof(capacityBounds));
    }

    public CharacterPerformanceDefinitionMaximumSnapshot Capture(
        string formulaId)
    {
        CharacterPerformanceFormulaDefinitionSO formula =
            formulas.Require(formulaId);
        if (formula.ResultChannel is
                CharacterPerformanceResultChannel.AccidentRisk
                or CharacterPerformanceResultChannel.Consumption
                or CharacterPerformanceResultChannel.Exposure)
        {
            throw new InvalidOperationException(
                "Inverse performance formulas require a finite minimum query: "
                + formula.FormulaId);
        }

        CharacterPerformanceCapacityInput[] inputs = formula.CapacityInputs
            .Where(value => value != null)
            .OrderBy(value => value.CapacityId)
            .ToArray();
        if (inputs.Length != formula.CapacityInputs.Count)
        {
            throw new InvalidOperationException(
                "Performance maximum cannot project a null capacity input.");
        }

        double weightedTotal = 0d;
        double totalWeight = 0d;
        double bottleneckMaximum = double.PositiveInfinity;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(formula.FormulaId);
        digest.AppendEnum(formula.ResultChannel);
        digest.AppendFloat(formula.BaseValue);
        digest.Append(inputs.Length);
        foreach (CharacterPerformanceCapacityInput input in inputs)
        {
            CharacterFunctionalCapacityDefinitionBoundsSnapshot bounds =
                capacityBounds.Capture(input.CapacityId);
            string targetId = CharacterFunctionalCapacityIds.GetStableId(
                input.CapacityId);
            double maximum = bounds.ProjectedMaximum;
            if (double.IsNaN(maximum)
                || double.IsInfinity(maximum)
                || maximum < 0d)
            {
                throw new InvalidOperationException(
                    "Functional capacity has no finite maximum: " + targetId);
            }
            if ((input.Role & CharacterPerformanceInputRole.Contribution) != 0
                && input.Weight > 0f)
            {
                weightedTotal += maximum * input.Weight;
                totalWeight += input.Weight;
            }
            if ((input.Role & CharacterPerformanceInputRole.Bottleneck) != 0)
            {
                bottleneckMaximum = Math.Min(
                    bottleneckMaximum,
                    0.25d + 0.75d * maximum);
            }
            digest.AppendEnum(input.CapacityId);
            digest.AppendFloat(input.Weight);
            digest.AppendEnum(input.Role);
            digest.AppendFloat(input.RequiredThreshold);
            digest.AppendDouble(maximum);
            digest.Append(bounds.SourceDigest);
        }
        if (totalWeight <= 0d)
        {
            throw new InvalidOperationException(
                "Performance maximum requires a weighted capacity input: "
                + formula.FormulaId);
        }
        double weightedMaximum = weightedTotal / totalWeight;
        double capacityMaximum = Math.Min(
            weightedMaximum,
            bottleneckMaximum);

        double proficiencyMaximum = 1d;
        if (!string.IsNullOrEmpty(formula.PrimaryProficiencyId))
        {
            double primary = CharacterPerformanceProficiencyFactorAuthority
                .Resolve(
                    formula.ResultChannel,
                    ProficiencyProgressionRules.ResolveEffects(
                        ProficiencyProgressionRules.MasterCurrentCap));
            double secondary = string.IsNullOrEmpty(
                    formula.SecondaryProficiencyId)
                ? 1d
                : primary;
            proficiencyMaximum = primary
                * (1d - formula.SecondaryProficiencyWeight)
                + secondary * formula.SecondaryProficiencyWeight;
        }

        double gameplayEffectMaximum = string.IsNullOrEmpty(
                formula.GameplayEffectTargetId)
            ? 1d
            : Math.Max(
                1d,
                effectBounds.RequireFiniteMaximum(
                    formula.GameplayEffectTargetId));
        double maximumValue = checked(
            (double)formula.BaseValue
            * capacityMaximum
            * proficiencyMaximum
            * gameplayEffectMaximum);
        if (double.IsNaN(maximumValue)
            || double.IsInfinity(maximumValue)
            || maximumValue < 0d)
        {
            throw new InvalidOperationException(
                "Performance definition maximum is not finite: "
                + formula.FormulaId);
        }

        digest.Append(formula.PrimaryProficiencyId);
        digest.Append(formula.SecondaryProficiencyId);
        digest.AppendFloat(formula.SecondaryProficiencyWeight);
        digest.Append(formula.GameplayEffectTargetId);
        digest.AppendDouble(capacityMaximum);
        digest.AppendDouble(proficiencyMaximum);
        digest.AppendDouble(gameplayEffectMaximum);
        digest.AppendDouble(maximumValue);
        return new CharacterPerformanceDefinitionMaximumSnapshot(
            formula.FormulaId,
            maximumValue,
            capacityMaximum,
            proficiencyMaximum,
            gameplayEffectMaximum,
            digest.ComputeSha256());
    }

}
