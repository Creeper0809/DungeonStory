using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

public static class NarrativeFormulaParameterIds
{
    public const string Magnitude = "magnitude";
    public const string Duration = "duration";
    public const string Count = "count";
    public const string TargetCount = "targetCount";

    public static readonly IReadOnlyList<string> Required = Array.AsReadOnly(new[]
    {
        Magnitude, Duration, Count, TargetCount
    });
}

public sealed class NarrativeFormulaEvidence
{
    public NarrativeFormulaEvidence(
        string evidenceId,
        string eventGroupKey,
        string actionKey,
        string relationshipKey,
        string domainKey,
        int attainedMilestoneCount,
        double importancePoints,
        int influenceUseCount)
    {
        EvidenceId = RequireCanonical(evidenceId, nameof(evidenceId));
        EventGroupKey = NormalizeOptional(eventGroupKey);
        ActionKey = NormalizeOptional(actionKey);
        RelationshipKey = NormalizeOptional(relationshipKey);
        DomainKey = NormalizeOptional(domainKey);
        if (attainedMilestoneCount < 0) throw new ArgumentOutOfRangeException(nameof(attainedMilestoneCount));
        if (influenceUseCount < 0) throw new ArgumentOutOfRangeException(nameof(influenceUseCount));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(importancePoints))
            throw new ArgumentOutOfRangeException(nameof(importancePoints));
        AttainedMilestoneCount = attainedMilestoneCount;
        ImportancePoints = importancePoints;
        InfluenceUseCount = influenceUseCount;
    }

    public string EvidenceId { get; }
    public string EventGroupKey { get; }
    public string ActionKey { get; }
    public string RelationshipKey { get; }
    public string DomainKey { get; }
    public int AttainedMilestoneCount { get; }
    public double ImportancePoints { get; }
    public int InfluenceUseCount { get; }
    public double ReuseMultiplier => 1d / (1d + InfluenceUseCount);

    private static string RequireCanonical(string value, string name)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || !string.Equals(normalized, value, StringComparison.Ordinal))
            throw new ArgumentException("A canonical non-empty value is required.", name);
        return normalized;
    }

    private static string NormalizeOptional(string value) => value?.Trim() ?? string.Empty;
}

public sealed class NarrativeFormulaStrengthPolicy
{
    public NarrativeFormulaStrengthPolicy(
        int formulaVersion,
        int baseBudget,
        int powerScale,
        double softCapK,
        double minimumImportance,
        double maximumImportance,
        IEnumerable<double> milestoneWeights)
    {
        if (formulaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(formulaVersion));
        if (baseBudget < 0) throw new ArgumentOutOfRangeException(nameof(baseBudget));
        if (powerScale < 0) throw new ArgumentOutOfRangeException(nameof(powerScale));
        if (!NarrativeFormulaGuard.IsFinitePositive(softCapK)) throw new ArgumentOutOfRangeException(nameof(softCapK));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(minimumImportance)
            || !NarrativeFormulaGuard.IsFiniteNonNegative(maximumImportance)
            || maximumImportance < minimumImportance)
            throw new ArgumentOutOfRangeException(nameof(maximumImportance));
        double[] weights = (milestoneWeights ?? throw new ArgumentNullException(nameof(milestoneWeights))).ToArray();
        if (weights.Length == 0 || weights.Any(value => !NarrativeFormulaGuard.IsFiniteNonNegative(value)))
            throw new ArgumentException("At least one finite non-negative milestone weight is required.", nameof(milestoneWeights));
        FormulaVersion = formulaVersion;
        BaseBudget = baseBudget;
        PowerScale = powerScale;
        SoftCapK = softCapK;
        MinimumImportance = minimumImportance;
        MaximumImportance = maximumImportance;
        MilestoneWeights = Array.AsReadOnly(weights);
    }

    public int FormulaVersion { get; }
    public int BaseBudget { get; }
    public int PowerScale { get; }
    public double SoftCapK { get; }
    public double MinimumImportance { get; }
    public double MaximumImportance { get; }
    public IReadOnlyList<double> MilestoneWeights { get; }
}

public sealed class NarrativeFormulaStrength
{
    public NarrativeFormulaStrength(double diversity, double milestones, double importance, double raw, double power, int budget)
    {
        Diversity = diversity;
        Milestones = milestones;
        Importance = importance;
        Raw = raw;
        Power = power;
        Budget = budget;
    }

    public double Diversity { get; }
    public double Milestones { get; }
    public double Importance { get; }
    public double Raw { get; }
    public double Power { get; }
    public int Budget { get; }
}

public enum NarrativeFormulaDrawbackSelectionKind
{
    PlayerChoice = 0,
    Automatic = 1
}

/// <summary>
/// Immutable system-owned limits for crediting an inseparable drawback. The
/// credit is local to one generated instance and never changes narrative
/// strength or any persistent/global budget.
/// </summary>
public sealed class NarrativeFormulaDrawbackCreditPolicy
{
    public NarrativeFormulaDrawbackCreditPolicy(
        double playerChoiceMaximumBudgetFraction,
        double automaticMaximumBudgetFraction,
        int absoluteMaximumCredit,
        bool requireNegativeEvidenceForAutomatic)
    {
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(playerChoiceMaximumBudgetFraction)
            || playerChoiceMaximumBudgetFraction > 1d)
            throw new ArgumentOutOfRangeException(nameof(playerChoiceMaximumBudgetFraction));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(automaticMaximumBudgetFraction)
            || automaticMaximumBudgetFraction > playerChoiceMaximumBudgetFraction)
            throw new ArgumentOutOfRangeException(nameof(automaticMaximumBudgetFraction));
        if (absoluteMaximumCredit < 0 || absoluteMaximumCredit > NarrativeFormulaCore.MaximumFormulaBudget)
            throw new ArgumentOutOfRangeException(nameof(absoluteMaximumCredit));
        PlayerChoiceMaximumBudgetFraction = playerChoiceMaximumBudgetFraction;
        AutomaticMaximumBudgetFraction = automaticMaximumBudgetFraction;
        AbsoluteMaximumCredit = absoluteMaximumCredit;
        RequireNegativeEvidenceForAutomatic = requireNegativeEvidenceForAutomatic;
    }

    public double PlayerChoiceMaximumBudgetFraction { get; }
    public double AutomaticMaximumBudgetFraction { get; }
    public int AbsoluteMaximumCredit { get; }
    public bool RequireNegativeEvidenceForAutomatic { get; }

    public NarrativeFormulaDrawbackResolution Resolve(
        int narrativeBudget,
        NarrativeFormulaDrawbackOption option)
    {
        if (narrativeBudget < 0 || narrativeBudget > NarrativeFormulaCore.MaximumFormulaBudget)
            throw new ArgumentOutOfRangeException(nameof(narrativeBudget));
        if (option == null) return NarrativeFormulaDrawbackResolution.None;
        if (!option.Reachable || !option.Mandatory || option.SeparatelyRemovable
            || option.CancelsSelectedBenefit)
            throw new InvalidOperationException(
                $"Drawback '{option.DrawbackId}' is not an inseparable reachable cost.");
        double fraction = option.SelectionKind == NarrativeFormulaDrawbackSelectionKind.PlayerChoice
            ? PlayerChoiceMaximumBudgetFraction
            : AutomaticMaximumBudgetFraction;
        bool creditEligible = option.SelectionKind != NarrativeFormulaDrawbackSelectionKind.Automatic
            || !RequireNegativeEvidenceForAutomatic
            || option.HasNegativeNarrativeEvidence;
        int fractionalCap = (int)Math.Floor(narrativeBudget * fraction);
        int accepted = creditEligible
            ? Math.Min(option.RequestedCredit, Math.Min(AbsoluteMaximumCredit, fractionalCap))
            : 0;
        return new NarrativeFormulaDrawbackResolution(
            option.DrawbackId, option.RequestedCredit, accepted,
            option.SelectionKind, creditEligible);
    }
}

public sealed class NarrativeFormulaDrawbackOption
{
    public NarrativeFormulaDrawbackOption(
        string drawbackId,
        int requestedCredit,
        NarrativeFormulaDrawbackSelectionKind selectionKind,
        bool reachable,
        bool mandatory,
        bool separatelyRemovable,
        bool cancelsSelectedBenefit,
        bool hasNegativeNarrativeEvidence)
    {
        string canonical = drawbackId?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, drawbackId, StringComparison.Ordinal))
            throw new ArgumentException("A canonical drawback ID is required.", nameof(drawbackId));
        if (requestedCredit < 0) throw new ArgumentOutOfRangeException(nameof(requestedCredit));
        if (!Enum.IsDefined(typeof(NarrativeFormulaDrawbackSelectionKind), selectionKind))
            throw new ArgumentOutOfRangeException(nameof(selectionKind));
        DrawbackId = canonical;
        RequestedCredit = requestedCredit;
        SelectionKind = selectionKind;
        Reachable = reachable;
        Mandatory = mandatory;
        SeparatelyRemovable = separatelyRemovable;
        CancelsSelectedBenefit = cancelsSelectedBenefit;
        HasNegativeNarrativeEvidence = hasNegativeNarrativeEvidence;
    }

    public string DrawbackId { get; }
    public int RequestedCredit { get; }
    public NarrativeFormulaDrawbackSelectionKind SelectionKind { get; }
    public bool Reachable { get; }
    public bool Mandatory { get; }
    public bool SeparatelyRemovable { get; }
    public bool CancelsSelectedBenefit { get; }
    public bool HasNegativeNarrativeEvidence { get; }
}

public sealed class NarrativeFormulaDrawbackResolution
{
    public static NarrativeFormulaDrawbackResolution None { get; } = new(
        string.Empty, 0, 0, NarrativeFormulaDrawbackSelectionKind.PlayerChoice, false);

    public NarrativeFormulaDrawbackResolution(
        string drawbackId,
        int requestedCredit,
        int acceptedCredit,
        NarrativeFormulaDrawbackSelectionKind selectionKind,
        bool creditEligible)
    {
        if (requestedCredit < 0 || acceptedCredit < 0 || acceptedCredit > requestedCredit)
            throw new ArgumentOutOfRangeException(nameof(acceptedCredit));
        DrawbackId = drawbackId ?? string.Empty;
        RequestedCredit = requestedCredit;
        AcceptedCredit = acceptedCredit;
        SelectionKind = selectionKind;
        CreditEligible = creditEligible;
    }

    public string DrawbackId { get; }
    public int RequestedCredit { get; }
    public int AcceptedCredit { get; }
    public NarrativeFormulaDrawbackSelectionKind SelectionKind { get; }
    public bool CreditEligible { get; }
    public bool HasDrawback => DrawbackId.Length > 0;
}

public sealed class NarrativeFormulaQuantizedRange
{
    public NarrativeFormulaQuantizedRange(
        string parameterId,
        long minimumUnits,
        long maximumUnits,
        long quantumUnits,
        int decimalPlaces,
        int costPerQuantum)
    {
        ParameterId = parameterId?.Trim() ?? string.Empty;
        if (!NarrativeFormulaParameterIds.Required.Contains(ParameterId, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown formula parameter '{ParameterId}'.", nameof(parameterId));
        if (minimumUnits < 0 || maximumUnits < minimumUnits || quantumUnits <= 0
            || (maximumUnits - minimumUnits) % quantumUnits != 0)
            throw new ArgumentOutOfRangeException(nameof(maximumUnits), "Range bounds must be non-negative and quantum aligned.");
        if (decimalPlaces < 0 || decimalPlaces > 6) throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
        if (costPerQuantum <= 0) throw new ArgumentOutOfRangeException(nameof(costPerQuantum));
        MinimumUnits = minimumUnits;
        MaximumUnits = maximumUnits;
        QuantumUnits = quantumUnits;
        DecimalPlaces = decimalPlaces;
        CostPerQuantum = costPerQuantum;
    }

    public string ParameterId { get; }
    public long MinimumUnits { get; }
    public long MaximumUnits { get; }
    public long QuantumUnits { get; }
    public int DecimalPlaces { get; }
    public int CostPerQuantum { get; }
    public int StepCount => checked((int)((MaximumUnits - MinimumUnits) / QuantumUnits));

    public decimal ToDecimal(long units)
    {
        RequireContains(units);
        decimal divisor = 1m;
        for (int index = 0; index < DecimalPlaces; index++) divisor *= 10m;
        return units / divisor;
    }

    public string Format(long units) => ToDecimal(units).ToString(CultureInfo.InvariantCulture);

    public void RequireContains(long units)
    {
        if (units < MinimumUnits || units > MaximumUnits || (units - MinimumUnits) % QuantumUnits != 0)
            throw new InvalidOperationException($"Parameter '{ParameterId}' units '{units}' are outside the registered quantized range.");
    }
}

public sealed class NarrativeFormulaPairSynergyCost
{
    public NarrativeFormulaPairSynergyCost(string otherCapabilityId, int cost)
    {
        string canonical = otherCapabilityId?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, otherCapabilityId, StringComparison.Ordinal))
            throw new ArgumentException("A canonical paired capability ID is required.", nameof(otherCapabilityId));
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        OtherCapabilityId = canonical;
        Cost = cost;
    }

    public string OtherCapabilityId { get; }
    public int Cost { get; }
}

public sealed class NarrativeFormulaGenerationCostContext
{
    public NarrativeFormulaGenerationCostContext(
        int triggerFrequencyUnits,
        bool guaranteedProc,
        int targetCount)
    {
        if (triggerFrequencyUnits < 0) throw new ArgumentOutOfRangeException(nameof(triggerFrequencyUnits));
        if (targetCount < 1) throw new ArgumentOutOfRangeException(nameof(targetCount));
        TriggerFrequencyUnits = triggerFrequencyUnits;
        GuaranteedProc = guaranteedProc;
        TargetCount = targetCount;
    }

    public int TriggerFrequencyUnits { get; }
    public bool GuaranteedProc { get; }
    public int TargetCount { get; }
}

public sealed class NarrativeFormulaCapabilityDescriptor
{
    private readonly IReadOnlyDictionary<string, NarrativeFormulaQuantizedRange> ranges;

    public NarrativeFormulaCapabilityDescriptor(
        string capabilityId,
        IEnumerable<NarrativeFormulaQuantizedRange> parameterRanges,
        IEnumerable<string> appliedParameterIds,
        int baseCost,
        int triggerFrequencyCostPerUnit,
        int guaranteedProcCost,
        int areaCostPerExtraTarget,
        int multiEffectCostPerExtraEffect,
        IEnumerable<NarrativeFormulaPairSynergyCost> pairSynergyCosts,
        double narrativeAffinity,
        IEnumerable<string> affinityKeys,
        IEnumerable<string> conflictGroups,
        IEnumerable<string> forbiddenSynergies,
        string formatterId,
        string applicatorId)
    {
        CapabilityId = RequireCanonical(capabilityId, nameof(capabilityId));
        NarrativeFormulaQuantizedRange[] rangeArray = (parameterRanges ?? throw new ArgumentNullException(nameof(parameterRanges))).ToArray();
        if (rangeArray.Length != NarrativeFormulaParameterIds.Required.Count
            || rangeArray.Select(value => value?.ParameterId).Distinct(StringComparer.Ordinal).Count() != rangeArray.Length
            || NarrativeFormulaParameterIds.Required.Any(id => rangeArray.All(value => value == null || !string.Equals(value.ParameterId, id, StringComparison.Ordinal))))
            throw new ArgumentException("Every descriptor must register each required parameter range exactly once.", nameof(parameterRanges));
        if (baseCost < 0 || triggerFrequencyCostPerUnit < 0 || guaranteedProcCost < 0
            || areaCostPerExtraTarget < 0 || multiEffectCostPerExtraEffect < 0)
            throw new ArgumentOutOfRangeException(nameof(baseCost));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(narrativeAffinity)) throw new ArgumentOutOfRangeException(nameof(narrativeAffinity));
        ranges = new ReadOnlyDictionary<string, NarrativeFormulaQuantizedRange>(
            rangeArray.ToDictionary(value => value.ParameterId, StringComparer.Ordinal));
        IReadOnlyList<string> applied = RequireCanonicalSet(
            appliedParameterIds,
            nameof(appliedParameterIds),
            requireNonEmpty: true);
        if (applied.Any(value => !NarrativeFormulaParameterIds.Required.Contains(value, StringComparer.Ordinal))
            || NarrativeFormulaParameterIds.Required.Where(value => !applied.Contains(value, StringComparer.Ordinal))
                .Any(value => ranges[value].StepCount != 0))
            throw new ArgumentException(
                "Applied parameters must be known, and unapplied parameter ranges must be fixed.",
                nameof(appliedParameterIds));
        AppliedParameterIds = Array.AsReadOnly(NarrativeFormulaParameterIds.Required
            .Where(value => applied.Contains(value, StringComparer.Ordinal)).ToArray());
        BaseCost = baseCost;
        TriggerFrequencyCostPerUnit = triggerFrequencyCostPerUnit;
        GuaranteedProcCost = guaranteedProcCost;
        AreaCostPerExtraTarget = areaCostPerExtraTarget;
        MultiEffectCostPerExtraEffect = multiEffectCostPerExtraEffect;
        NarrativeFormulaPairSynergyCost[] synergyCosts =
            (pairSynergyCosts ?? throw new ArgumentNullException(nameof(pairSynergyCosts))).ToArray();
        if (synergyCosts.Any(value => value == null)
            || synergyCosts.Select(value => value.OtherCapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != synergyCosts.Length
            || synergyCosts.Any(value => string.Equals(
                value.OtherCapabilityId, CapabilityId, StringComparison.Ordinal)))
            throw new ArgumentException("Pair-synergy capability IDs must be distinct.", nameof(pairSynergyCosts));
        PairSynergyCosts = Array.AsReadOnly(synergyCosts
            .OrderBy(value => value.OtherCapabilityId, StringComparer.Ordinal).ToArray());
        NarrativeAffinity = narrativeAffinity;
        AffinityKeys = RequireCanonicalSet(affinityKeys, nameof(affinityKeys), requireNonEmpty: true);
        ConflictGroups = RequireCanonicalSet(conflictGroups, nameof(conflictGroups), requireNonEmpty: true);
        ForbiddenSynergies = RequireCanonicalSet(forbiddenSynergies, nameof(forbiddenSynergies), requireNonEmpty: false);
        if (ForbiddenSynergies.Contains(CapabilityId, StringComparer.Ordinal))
            throw new ArgumentException("A capability cannot forbid itself.", nameof(forbiddenSynergies));
        FormatterId = RequireCanonical(formatterId, nameof(formatterId));
        ApplicatorId = RequireCanonical(applicatorId, nameof(applicatorId));
    }

    public string CapabilityId { get; }
    public IReadOnlyDictionary<string, NarrativeFormulaQuantizedRange> Ranges => ranges;
    public IReadOnlyList<string> AppliedParameterIds { get; }
    public int BaseCost { get; }
    public int TriggerFrequencyCostPerUnit { get; }
    public int GuaranteedProcCost { get; }
    public int AreaCostPerExtraTarget { get; }
    public int MultiEffectCostPerExtraEffect { get; }
    public IReadOnlyList<NarrativeFormulaPairSynergyCost> PairSynergyCosts { get; }
    public double NarrativeAffinity { get; }
    public IReadOnlyList<string> AffinityKeys { get; }
    public IReadOnlyList<string> ConflictGroups { get; }
    public IReadOnlyList<string> ForbiddenSynergies { get; }
    public string FormatterId { get; }
    public string ApplicatorId { get; }

    public NarrativeFormulaQuantizedRange RequireRange(string parameterId) =>
        ranges.TryGetValue(parameterId ?? string.Empty, out NarrativeFormulaQuantizedRange value)
            ? value
            : throw new InvalidOperationException($"Capability '{CapabilityId}' has no '{parameterId}' range.");

    public int CalculateContextCost(
        NarrativeFormulaGenerationCostContext context,
        int composedEffectCount)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (composedEffectCount < 1 || composedEffectCount > 3)
            throw new ArgumentOutOfRangeException(nameof(composedEffectCount));
        return checked(BaseCost
            + checked(context.TriggerFrequencyUnits * TriggerFrequencyCostPerUnit)
            + (context.GuaranteedProc ? GuaranteedProcCost : 0)
            + checked((context.TargetCount - 1) * AreaCostPerExtraTarget)
            + checked((composedEffectCount - 1) * MultiEffectCostPerExtraEffect));
    }

    private static string RequireCanonical(string value, string name)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || !string.Equals(normalized, value, StringComparison.Ordinal))
            throw new ArgumentException("A canonical non-empty value is required.", name);
        return normalized;
    }

    private static IReadOnlyList<string> RequireCanonicalSet(IEnumerable<string> values, string name, bool requireNonEmpty)
    {
        string[] result = (values ?? throw new ArgumentNullException(name)).ToArray();
        if ((requireNonEmpty && result.Length == 0)
            || result.Any(value => string.IsNullOrWhiteSpace(value) || !string.Equals(value.Trim(), value, StringComparison.Ordinal))
            || result.Distinct(StringComparer.Ordinal).Count() != result.Length)
            throw new ArgumentException("Canonical, distinct descriptor values are required.", name);
        Array.Sort(result, StringComparer.Ordinal);
        return Array.AsReadOnly(result);
    }
}

public sealed class NarrativeFormulaParameterValue
{
    public NarrativeFormulaParameterValue(string parameterId, long units)
    {
        ParameterId = parameterId ?? throw new ArgumentNullException(nameof(parameterId));
        Units = units;
    }

    public string ParameterId { get; }
    public long Units { get; }
}

public sealed class NarrativeFormulaCapabilityInput
{
    public NarrativeFormulaCapabilityInput(
        NarrativeFormulaCapabilityDescriptor descriptor,
        double affinityScore,
        double noveltyScore)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(affinityScore))
            throw new ArgumentOutOfRangeException(nameof(affinityScore));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(noveltyScore))
            throw new ArgumentOutOfRangeException(nameof(noveltyScore));
        AffinityScore = affinityScore;
        NoveltyScore = noveltyScore;
    }

    public NarrativeFormulaCapabilityDescriptor Descriptor { get; }
    public double AffinityScore { get; }
    public double NoveltyScore { get; }
}

public sealed class NarrativeFormulaCapabilityAllocation
{
    public NarrativeFormulaCapabilityAllocation(
        NarrativeFormulaCapabilityDescriptor descriptor,
        IEnumerable<NarrativeFormulaParameterValue> parameters)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        NarrativeFormulaParameterValue[] values =
            (parameters ?? throw new ArgumentNullException(nameof(parameters))).ToArray();
        if (values.Length != NarrativeFormulaParameterIds.Required.Count
            || values.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.ParameterId)
                || !string.Equals(value.ParameterId.Trim(), value.ParameterId, StringComparison.Ordinal))
            || values.Select(value => value.ParameterId).Distinct(StringComparer.Ordinal).Count() != values.Length
            || NarrativeFormulaParameterIds.Required.Any(id => values.All(value =>
                !string.Equals(value.ParameterId, id, StringComparison.Ordinal))))
            throw new ArgumentException("Each allocation must contain every formula parameter exactly once.", nameof(parameters));
        foreach (NarrativeFormulaParameterValue value in values)
            descriptor.RequireRange(value.ParameterId).RequireContains(value.Units);
        Parameters = Array.AsReadOnly(values.OrderBy(value => value.ParameterId, StringComparer.Ordinal).ToArray());
    }

    public NarrativeFormulaCapabilityDescriptor Descriptor { get; }
    public IReadOnlyList<NarrativeFormulaParameterValue> Parameters { get; }
}

public sealed class NarrativeFormulaCandidate
{
    public NarrativeFormulaCandidate(
        NarrativeFormulaCapabilityDescriptor descriptor,
        IEnumerable<NarrativeFormulaParameterValue> parameters,
        int calculatedCost,
        double affinityScore,
        double noveltyScore,
        IEnumerable<string> evidenceIds)
        : this(
            new[] { new NarrativeFormulaCapabilityAllocation(descriptor, parameters) },
            calculatedCost,
            affinityScore,
            noveltyScore,
            evidenceIds)
    {
    }

    public NarrativeFormulaCandidate(
        IEnumerable<NarrativeFormulaCapabilityAllocation> allocations,
        int calculatedCost,
        double affinityScore,
        double noveltyScore,
        IEnumerable<string> evidenceIds)
        : this(allocations, calculatedCost, NarrativeFormulaDrawbackResolution.None,
            affinityScore, noveltyScore, evidenceIds)
    {
    }

    public NarrativeFormulaCandidate(
        IEnumerable<NarrativeFormulaCapabilityAllocation> allocations,
        int positiveCost,
        NarrativeFormulaDrawbackResolution drawback,
        double affinityScore,
        double noveltyScore,
        IEnumerable<string> evidenceIds)
    {
        NarrativeFormulaCapabilityAllocation[] allocationArray =
            (allocations ?? throw new ArgumentNullException(nameof(allocations))).ToArray();
        if (allocationArray.Length < 1 || allocationArray.Length > 3
            || allocationArray.Any(value => value == null)
            || allocationArray.Select(value => value.Descriptor.CapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != allocationArray.Length)
            throw new ArgumentException("A candidate requires one to three distinct capabilities.", nameof(allocations));
        drawback ??= NarrativeFormulaDrawbackResolution.None;
        if (positiveCost < 0 || drawback.AcceptedCredit > positiveCost)
            throw new ArgumentOutOfRangeException(nameof(positiveCost));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(affinityScore))
            throw new ArgumentOutOfRangeException(nameof(affinityScore));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(noveltyScore))
            throw new ArgumentOutOfRangeException(nameof(noveltyScore));
        string[] canonicalEvidence = (evidenceIds ?? throw new ArgumentNullException(nameof(evidenceIds))).ToArray();
        if (canonicalEvidence.Length == 0
            || canonicalEvidence.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value.Trim(), value, StringComparison.Ordinal))
            || canonicalEvidence.Distinct(StringComparer.Ordinal).Count() != canonicalEvidence.Length)
            throw new ArgumentException("Evidence IDs must be canonical and distinct.", nameof(evidenceIds));
        Allocations = Array.AsReadOnly(allocationArray);
        Descriptor = allocationArray[0].Descriptor;
        Parameters = allocationArray[0].Parameters;
        PositiveCost = positiveCost;
        DrawbackId = drawback.DrawbackId;
        DrawbackRequestedCredit = drawback.RequestedCredit;
        DrawbackCredit = drawback.AcceptedCredit;
        CalculatedCost = checked(positiveCost - drawback.AcceptedCredit);
        AffinityScore = affinityScore;
        NoveltyScore = noveltyScore;
        EvidenceIds = Array.AsReadOnly(canonicalEvidence.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        CanonicalSignature = string.Join("|", allocationArray.Select((allocation, index) =>
            (index == 0 ? "primary:" : string.Empty) + allocation.Descriptor.CapabilityId + ";"
            + string.Join(";", allocation.Parameters.Select(value =>
                $"{value.ParameterId}={value.Units.ToString(CultureInfo.InvariantCulture)}")))
            + "|drawback=" + DrawbackId
            + ";requested=" + DrawbackRequestedCredit.ToString(CultureInfo.InvariantCulture)
            + ";accepted=" + DrawbackCredit.ToString(CultureInfo.InvariantCulture));
    }

    public IReadOnlyList<NarrativeFormulaCapabilityAllocation> Allocations { get; }
    public NarrativeFormulaCapabilityDescriptor Descriptor { get; }
    public IReadOnlyList<NarrativeFormulaParameterValue> Parameters { get; }
    public int CalculatedCost { get; }
    public int PositiveCost { get; }
    public string DrawbackId { get; }
    public int DrawbackRequestedCredit { get; }
    public int DrawbackCredit { get; }
    public double AffinityScore { get; }
    public double NoveltyScore { get; }
    public IReadOnlyList<string> EvidenceIds { get; }
    public string CanonicalSignature { get; }
    public double BudgetUtilization(int budget)
    {
        int localBudget = checked(budget + DrawbackCredit);
        return localBudget <= 0 ? 0d : (double)PositiveCost / localBudget;
    }
}

public static class NarrativeFormulaGuard
{
    public static bool IsFiniteNonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    public static bool IsFinitePositive(double value) => IsFiniteNonNegative(value) && value > 0d;
}

public static class NarrativeFormulaNegativeEvidence
{
    private static readonly string[] Markers =
    {
        "failed", "blocked", "damaged", "injury", "wound", "broken",
        "loss", "lost", "death", "plague", "shortage", "accident",
        "실패", "부상", "파손", "손실", "죽음", "역병", "부족", "사고"
    };

    public static bool Matches(params string[] values) =>
        (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
        .Any(value => Markers.Any(marker => value.IndexOf(marker,
            StringComparison.OrdinalIgnoreCase) >= 0));
}
