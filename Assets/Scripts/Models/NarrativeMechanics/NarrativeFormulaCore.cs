using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public static class NarrativeFormulaCore
{
    public const int MaximumEnumeratedStates = 65536;
    public const int MaximumCapabilityInputs = 64;
    public const int MaximumFormulaBudget = 4096;
    public const long MaximumCompositionTransitions = 8388608;
    public const double RetentionDistance = 0.10d;

    public static NarrativeFormulaStrength CalculateStrength(
        NarrativeFormulaStrengthPolicy policy,
        IEnumerable<NarrativeFormulaEvidence> evidence)
    {
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        NarrativeFormulaEvidence[] ordered = (evidence ?? throw new ArgumentNullException(nameof(evidence)))
            .OrderBy(value => value?.EvidenceId, StringComparer.Ordinal).ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.EvidenceId).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new InvalidOperationException("Formula evidence must be non-null with distinct IDs.");

        Dictionary<string, double> diversityContributions = new Dictionary<string, double>(StringComparer.Ordinal);
        double milestones = 0d;
        double importance = 0d;
        foreach (NarrativeFormulaEvidence item in ordered)
        {
            if (item.ImportancePoints < policy.MinimumImportance || item.ImportancePoints > policy.MaximumImportance)
                throw new InvalidOperationException($"Evidence '{item.EvidenceId}' importance is outside policy bounds.");
            double reuse = item.ReuseMultiplier;
            AddDiversity("event", item.EventGroupKey, reuse);
            AddDiversity("action", item.ActionKey, reuse);
            AddDiversity("relationship", item.RelationshipKey, reuse);
            AddDiversity("domain", item.DomainKey, reuse);
            for (int index = 0; index < item.AttainedMilestoneCount; index++)
            {
                if (index >= policy.MilestoneWeights.Count)
                    throw new InvalidOperationException($"Evidence '{item.EvidenceId}' exceeds the authored milestone policy.");
                milestones += policy.MilestoneWeights[index] * reuse;
            }
            importance += item.ImportancePoints * reuse;
        }

        double diversity = diversityContributions.Values.Sum();
        double raw = 0.40d * diversity + 0.35d * milestones + 0.25d * importance;
        // Preserve the mathematical asymptote under IEEE-754 rounding: for very
        // large finite raw strengths Exp() underflows and 1 - 0 would otherwise
        // become the unreachable hard ceiling exactly.
        double power = Math.Min(
            0.9999999999999999d,
            1d - Math.Exp(-raw / policy.SoftCapK));
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(raw)
            || !NarrativeFormulaGuard.IsFiniteNonNegative(power)
            || power > 1d)
            throw new InvalidOperationException("Narrative strength produced a non-finite result.");
        int budget = checked(policy.BaseBudget + (int)Math.Floor(policy.PowerScale * power));
        return new NarrativeFormulaStrength(diversity, milestones, importance, raw, power, budget);

        void AddDiversity(string prefix, string key, double contribution)
        {
            if (key.Length == 0) return;
            string composite = prefix + ":" + key;
            if (!diversityContributions.TryGetValue(composite, out double existing)
                || contribution > existing)
                diversityContributions[composite] = contribution;
        }
    }

    public static IReadOnlyList<NarrativeFormulaCandidate> Optimize(
        NarrativeFormulaCapabilityDescriptor descriptor,
        NarrativeFormulaGenerationCostContext generationContext,
        int budget,
        double affinityScore,
        double noveltyScore,
        IEnumerable<string> evidenceIds,
        int maximumResults = 3,
        NarrativeFormulaDrawbackCreditPolicy drawbackPolicy = null,
        NarrativeFormulaDrawbackOption drawbackOption = null)
    {
        return OptimizeCompositions(
            new[] { new NarrativeFormulaCapabilityInput(descriptor, affinityScore, noveltyScore) },
            generationContext,
            budget,
            descriptor?.CapabilityId,
            evidenceIds,
            maximumCapabilityCount: 1,
            maximumResults: maximumResults,
            drawbackPolicy: drawbackPolicy,
            drawbackOption: drawbackOption);
    }

    /// <summary>
    /// Allocates numbers only for the exact capability identities already
    /// selected by a validated module-selection response. Unlike
    /// OptimizeCompositions this method never drops a selected capability and
    /// never chooses a semantic primary/companion subset on behalf of the LLM.
    /// </summary>
    public static IReadOnlyList<NarrativeFormulaCandidate> OptimizeExactComposition(
        IEnumerable<NarrativeFormulaCapabilityInput> selectedCapabilityInputs,
        NarrativeFormulaGenerationCostContext generationContext,
        int budget,
        IEnumerable<string> evidenceIds,
        int maximumResults = 3,
        NarrativeFormulaDrawbackCreditPolicy drawbackPolicy = null,
        NarrativeFormulaDrawbackOption drawbackOption = null)
    {
        if (generationContext == null) throw new ArgumentNullException(nameof(generationContext));
        if (budget < 0 || budget > MaximumFormulaBudget)
            throw new ArgumentOutOfRangeException(nameof(budget));
        if (maximumResults < 1 || maximumResults > 3)
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        if ((drawbackPolicy == null) != (drawbackOption == null))
            throw new InvalidOperationException(
                "Drawback policy and option must be supplied together.");
        NarrativeFormulaDrawbackResolution drawback = drawbackPolicy == null
            ? NarrativeFormulaDrawbackResolution.None
            : drawbackPolicy.Resolve(budget, drawbackOption);
        int positiveBudget = checked(budget + drawback.AcceptedCredit);
        if (positiveBudget > MaximumFormulaBudget)
            throw new InvalidOperationException(
                "Local drawback credit exceeds the bounded formula budget.");

        string[] canonicalEvidence = (evidenceIds
                ?? throw new ArgumentNullException(nameof(evidenceIds))).ToArray();
        if (canonicalEvidence.Length == 0
            || canonicalEvidence.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value.Trim(), value, StringComparison.Ordinal))
            || canonicalEvidence.Distinct(StringComparer.Ordinal).Count()
                != canonicalEvidence.Length)
            throw new InvalidOperationException(
                "Formula evidence IDs must be canonical and distinct.");
        NarrativeFormulaCapabilityInput[] inputs = (selectedCapabilityInputs
                ?? throw new ArgumentNullException(nameof(selectedCapabilityInputs)))
            .OrderBy(value => value?.Descriptor?.CapabilityId,
                StringComparer.Ordinal).ToArray();
        if (inputs.Length is < 1 or > 3 || inputs.Any(value => value == null)
            || inputs.Select(value => value.Descriptor.CapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != inputs.Length)
            throw new InvalidOperationException(
                "Exact composition requires one to three distinct selected capabilities.");
        if (!TryGetPairCost(inputs.Select(value => value.Descriptor).ToArray(),
                out int pairCost))
            return Array.Empty<NarrativeFormulaCandidate>();

        int contextCost = pairCost;
        foreach (NarrativeFormulaCapabilityInput input in inputs)
            contextCost = checked(contextCost
                + input.Descriptor.CalculateContextCost(
                    generationContext, inputs.Length));
        if (contextCost > positiveBudget)
            return Array.Empty<NarrativeFormulaCandidate>();

        Dictionary<string, IReadOnlyList<AxisState>> states = inputs.ToDictionary(
            value => value.Descriptor.CapabilityId,
            value => EnumerateAxisStates(value.Descriptor, positiveBudget),
            StringComparer.Ordinal);
        Dictionary<int, NarrativeFormulaCapabilityAllocation[]> totals =
            new() { [contextCost] = Array.Empty<NarrativeFormulaCapabilityAllocation>() };
        long transitions = 0;
        foreach (NarrativeFormulaCapabilityInput input in inputs)
        {
            Dictionary<int, NarrativeFormulaCapabilityAllocation[]> next = new();
            foreach (KeyValuePair<int, NarrativeFormulaCapabilityAllocation[]> current in totals)
            foreach (AxisState state in states[input.Descriptor.CapabilityId])
            {
                if (++transitions > MaximumCompositionTransitions)
                    throw new InvalidOperationException(
                        "Exact formula composition exceeds the bounded transition limit.");
                int total = checked(current.Key + state.VariableCost);
                if (total > positiveBudget) continue;
                NarrativeFormulaCapabilityAllocation[] allocations = current.Value
                    .Concat(new[]
                    {
                        new NarrativeFormulaCapabilityAllocation(
                            input.Descriptor, state.Parameters)
                    }).ToArray();
                if (!next.TryGetValue(total,
                        out NarrativeFormulaCapabilityAllocation[] existing)
                    || AllocationUtilityScore(allocations)
                        > AllocationUtilityScore(existing) + 1e-12d
                    || (Math.Abs(AllocationUtilityScore(allocations)
                            - AllocationUtilityScore(existing)) <= 1e-12d
                        && MinimumAdditionalCost(allocations)
                            > MinimumAdditionalCost(existing))
                    || (Math.Abs(AllocationUtilityScore(allocations)
                            - AllocationUtilityScore(existing)) <= 1e-12d
                        && MinimumAdditionalCost(allocations)
                            == MinimumAdditionalCost(existing)
                        && CompareAllocations(allocations, existing) < 0))
                    next[total] = allocations;
            }
            totals = next;
        }

        List<NarrativeFormulaCandidate> legal = new();
        foreach (KeyValuePair<int, NarrativeFormulaCapabilityAllocation[]> total in totals)
        {
            if (total.Key < drawback.AcceptedCredit
                || CanAddQuantum(total.Value, total.Key, positiveBudget))
                continue;
            legal.Add(new NarrativeFormulaCandidate(
                total.Value,
                total.Key,
                drawback,
                inputs.Sum(value => value.AffinityScore),
                inputs.Sum(value => value.NoveltyScore),
                canonicalEvidence));
        }
        return legal.Count == 0
            ? Array.Empty<NarrativeFormulaCandidate>()
            : RetainNearBest(legal, budget, maximumResults);
    }

    public static IReadOnlyList<NarrativeFormulaSelectedModuleResolution> OptimizeSelectedModules(
        NarrativeFormulaValidatedModuleSelection selection,
        NarrativeFormulaGenerationCostContext positiveContext,
        int budget,
        NarrativeFormulaDrawbackCreditPolicy drawbackPolicy,
        NarrativeFormulaDrawbackSelectionKind drawbackSelectionKind,
        bool hasNegativeNarrativeEvidence,
        int maximumResults = 3)
    {
        if (selection == null) throw new ArgumentNullException(nameof(selection));
        if (positiveContext == null) throw new ArgumentNullException(nameof(positiveContext));
        NarrativeFormulaCandidate drawbackSeverity = null;
        NarrativeFormulaDrawbackOption drawbackOption = null;
        if (selection.DrawbackModules.Count > 0)
        {
            if (drawbackPolicy == null)
                throw new InvalidOperationException("Selected drawbacks require an authored credit policy.");
            string drawbackId = "selected-drawbacks:"
                + NarrativeInferenceHash.ComputeSha256Utf8(string.Join("\n",
                    selection.DrawbackModules.Select(value => value.ModuleId)))
                    .Substring("sha256:".Length);
            NarrativeFormulaDrawbackOption capProbe = new(
                drawbackId,
                MaximumFormulaBudget,
                drawbackSelectionKind,
                reachable: true,
                mandatory: true,
                separatelyRemovable: false,
                cancelsSelectedBenefit: false,
                hasNegativeNarrativeEvidence: hasNegativeNarrativeEvidence);
            int severityBudget = drawbackPolicy.Resolve(budget, capProbe).AcceptedCredit;
            if (severityBudget <= 0) return Array.Empty<NarrativeFormulaSelectedModuleResolution>();
            drawbackSeverity = OptimizeExactComposition(
                    selection.DrawbackModules.Select(value => value.Capability),
                    new NarrativeFormulaGenerationCostContext(0, false, 1),
                    severityBudget,
                    selection.EvidenceFactIds,
                    maximumResults: 1)
                .SingleOrDefault();
            if (drawbackSeverity == null || drawbackSeverity.PositiveCost <= 0)
                return Array.Empty<NarrativeFormulaSelectedModuleResolution>();
            drawbackOption = new NarrativeFormulaDrawbackOption(
                drawbackId,
                drawbackSeverity.PositiveCost,
                drawbackSelectionKind,
                reachable: true,
                mandatory: true,
                separatelyRemovable: false,
                cancelsSelectedBenefit: false,
                hasNegativeNarrativeEvidence: hasNegativeNarrativeEvidence);
        }
        IReadOnlyList<NarrativeFormulaCandidate> positives = OptimizeExactComposition(
            selection.PositiveModules.Select(value => value.Capability),
            positiveContext,
            budget,
            selection.EvidenceFactIds,
            maximumResults,
            drawbackOption == null ? null : drawbackPolicy,
            drawbackOption);
        return positives.Select(value => new NarrativeFormulaSelectedModuleResolution(
            value,
            drawbackSeverity,
            selection.PositiveModules.Select(module => module.ModuleId),
            selection.DrawbackModules.Select(module => module.ModuleId))).ToArray();
    }

    public static IReadOnlyList<NarrativeFormulaCandidate> OptimizeCompositions(
        IEnumerable<NarrativeFormulaCapabilityInput> capabilityInputs,
        NarrativeFormulaGenerationCostContext generationContext,
        int budget,
        string primaryCapabilityId,
        IEnumerable<string> evidenceIds,
        int maximumCapabilityCount = 3,
        int maximumResults = 3,
        NarrativeFormulaDrawbackCreditPolicy drawbackPolicy = null,
        NarrativeFormulaDrawbackOption drawbackOption = null)
    {
        if (generationContext == null) throw new ArgumentNullException(nameof(generationContext));
        if (budget < 0 || budget > MaximumFormulaBudget) throw new ArgumentOutOfRangeException(nameof(budget));
        if (maximumCapabilityCount < 1 || maximumCapabilityCount > 3)
            throw new ArgumentOutOfRangeException(nameof(maximumCapabilityCount));
        if (maximumResults < 1 || maximumResults > 3)
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        if ((drawbackPolicy == null) != (drawbackOption == null))
            throw new InvalidOperationException(
                "Drawback policy and option must be supplied together.");
        NarrativeFormulaDrawbackResolution drawback = drawbackPolicy == null
            ? NarrativeFormulaDrawbackResolution.None
            : drawbackPolicy.Resolve(budget, drawbackOption);
        int positiveBudget = checked(budget + drawback.AcceptedCredit);
        if (positiveBudget > MaximumFormulaBudget)
            throw new InvalidOperationException("Local drawback credit exceeds the bounded formula budget.");
        string[] canonicalEvidence = (evidenceIds ?? throw new ArgumentNullException(nameof(evidenceIds))).ToArray();
        if (canonicalEvidence.Length == 0
            || canonicalEvidence.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value.Trim(), value, StringComparison.Ordinal))
            || canonicalEvidence.Distinct(StringComparer.Ordinal).Count() != canonicalEvidence.Length)
            throw new InvalidOperationException("Formula evidence IDs must be canonical and distinct.");
        NarrativeFormulaCapabilityInput[] inputs = (capabilityInputs
                ?? throw new ArgumentNullException(nameof(capabilityInputs)))
            .OrderBy(value => value?.Descriptor?.CapabilityId, StringComparer.Ordinal).ToArray();
        if (inputs.Length == 0 || inputs.Length > MaximumCapabilityInputs || inputs.Any(value => value == null)
            || inputs.Select(value => value.Descriptor.CapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != inputs.Length)
            throw new InvalidOperationException("Composition inputs must contain distinct capability descriptors.");
        string primary = Require(primaryCapabilityId, nameof(primaryCapabilityId));
        if (inputs.All(value => !string.Equals(value.Descriptor.CapabilityId, primary, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Primary capability '{primary}' is absent from the composition pool.");

        Dictionary<string, IReadOnlyList<AxisState>> states = inputs.ToDictionary(
            value => value.Descriptor.CapabilityId,
            value => EnumerateAxisStates(value.Descriptor, positiveBudget),
            StringComparer.Ordinal);
        List<NarrativeFormulaCandidate> legal = new List<NarrativeFormulaCandidate>();
        long transitions = 0;
        NarrativeFormulaCapabilityInput primaryInput = inputs.Single(value =>
            string.Equals(value.Descriptor.CapabilityId, primary, StringComparison.Ordinal));
        NarrativeFormulaCapabilityInput[] companions = inputs.Where(value => !ReferenceEquals(value, primaryInput)).ToArray();
        for (int capabilityCount = 1; capabilityCount <= maximumCapabilityCount; capabilityCount++)
        {
            foreach (NarrativeFormulaCapabilityInput[] selected in Choose(companions, capabilityCount - 1)
                .Select(values => new[] { primaryInput }.Concat(values).ToArray()))
            {
                if (!TryGetPairCost(selected.Select(value => value.Descriptor).ToArray(), out int pairCost))
                    continue;
                int contextCost = pairCost;
                foreach (NarrativeFormulaCapabilityInput input in selected)
                    contextCost = checked(contextCost
                        + input.Descriptor.CalculateContextCost(generationContext, selected.Length));
                if (contextCost > positiveBudget) continue;

                Dictionary<int, NarrativeFormulaCapabilityAllocation[]> totals =
                    new Dictionary<int, NarrativeFormulaCapabilityAllocation[]> { [contextCost] = Array.Empty<NarrativeFormulaCapabilityAllocation>() };
                foreach (NarrativeFormulaCapabilityInput input in selected)
                {
                    Dictionary<int, NarrativeFormulaCapabilityAllocation[]> next = new Dictionary<int, NarrativeFormulaCapabilityAllocation[]>();
                    foreach (KeyValuePair<int, NarrativeFormulaCapabilityAllocation[]> current in totals)
                    foreach (AxisState state in states[input.Descriptor.CapabilityId])
                    {
                        if (++transitions > MaximumCompositionTransitions)
                            throw new InvalidOperationException("Formula composition exceeds the bounded transition limit.");
                        int total = checked(current.Key + state.VariableCost);
                        if (total > positiveBudget) continue;
                        NarrativeFormulaCapabilityAllocation[] allocations = current.Value
                            .Concat(new[] { new NarrativeFormulaCapabilityAllocation(input.Descriptor, state.Parameters) }).ToArray();
                        if (!next.TryGetValue(total, out NarrativeFormulaCapabilityAllocation[] existing)
                            || AllocationUtilityScore(allocations)
                                > AllocationUtilityScore(existing) + 1e-12d
                            || (Math.Abs(AllocationUtilityScore(allocations)
                                    - AllocationUtilityScore(existing)) <= 1e-12d
                                && MinimumAdditionalCost(allocations) > MinimumAdditionalCost(existing))
                            || (Math.Abs(AllocationUtilityScore(allocations)
                                    - AllocationUtilityScore(existing)) <= 1e-12d
                                && MinimumAdditionalCost(allocations) == MinimumAdditionalCost(existing)
                                && CompareAllocations(allocations, existing) < 0))
                            next[total] = allocations;
                    }
                    totals = next;
                }

                foreach (KeyValuePair<int, NarrativeFormulaCapabilityAllocation[]> total in totals)
                {
                    if (total.Key < drawback.AcceptedCredit
                        || CanAddQuantum(total.Value, total.Key, positiveBudget)) continue;
                    legal.Add(new NarrativeFormulaCandidate(
                        total.Value,
                        total.Key,
                        drawback,
                        selected.Sum(value => value.AffinityScore),
                        selected.Sum(value => value.NoveltyScore),
                        canonicalEvidence));
                }
            }
        }
        return legal.Count == 0
            ? Array.Empty<NarrativeFormulaCandidate>()
            : RetainNearBest(legal, budget, maximumResults);
    }

    public static IReadOnlyList<NarrativeFormulaCandidate> Rank(
        IEnumerable<NarrativeFormulaCandidate> candidates,
        int budget)
    {
        if (budget < 0 || budget > MaximumFormulaBudget) throw new ArgumentOutOfRangeException(nameof(budget));
        NarrativeFormulaCandidate[] values = (candidates ?? throw new ArgumentNullException(nameof(candidates))).ToArray();
        if (values.Any(value => value == null || value.CalculatedCost < 0 || value.CalculatedCost > budget))
            throw new InvalidOperationException("Ranking received a null or over-budget candidate.");
        return values.OrderByDescending(value => NormalizedScore(value, budget))
            .ThenByDescending(value => value.AffinityScore)
            .ThenByDescending(value => value.BudgetUtilization(budget))
            .ThenByDescending(value => value.NoveltyScore)
            .ThenBy(value => value.CanonicalSignature, StringComparer.Ordinal)
            .ToArray();
    }

    public static int CalculateCost(
        IEnumerable<NarrativeFormulaCapabilityAllocation> allocations,
        NarrativeFormulaGenerationCostContext generationContext)
    {
        if (generationContext == null) throw new ArgumentNullException(nameof(generationContext));
        NarrativeFormulaCapabilityAllocation[] values = (allocations
                ?? throw new ArgumentNullException(nameof(allocations))).ToArray();
        if (values.Length < 1 || values.Length > 3
            || values.Any(value => value == null)
            || values.Select(value => value.Descriptor.CapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException("Cost calculation requires one to three distinct capability allocations.");
        if (!TryGetPairCost(values.Select(value => value.Descriptor).ToArray(), out int cost))
            throw new InvalidOperationException("Formula composition contains a forbidden or conflicting capability pair.");
        foreach (NarrativeFormulaCapabilityAllocation allocation in values)
        {
            cost = checked(cost + allocation.Descriptor.CalculateContextCost(generationContext, values.Length));
            foreach (NarrativeFormulaParameterValue parameter in allocation.Parameters)
            {
                NarrativeFormulaQuantizedRange range = allocation.Descriptor.RequireRange(parameter.ParameterId);
                range.RequireContains(parameter.Units);
                long steps = (parameter.Units - range.MinimumUnits) / range.QuantumUnits;
                cost = checked(cost + checked((int)steps) * range.CostPerQuantum);
            }
        }
        return cost;
    }

    public static IReadOnlyList<NarrativeFormulaCandidate> RetainNearBest(
        IEnumerable<NarrativeFormulaCandidate> candidates,
        int budget,
        int maximumResults = 3)
    {
        if (maximumResults < 1 || maximumResults > 3)
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        List<NarrativeFormulaCandidate> ranked = Rank(candidates, budget).ToList();
        if (ranked.Count == 0) return Array.Empty<NarrativeFormulaCandidate>();
        double best = NormalizedScore(ranked[0], budget);
        return ranked.Where(value => best - NormalizedScore(value, budget) <= RetentionDistance + 1e-12d)
            .Take(maximumResults).ToArray();
    }

    public static NarrativeFormulaCandidate ChooseDeterministicWinner(
        IEnumerable<NarrativeFormulaCandidate> rankedCandidates,
        string persistentOwnerId,
        string manifestationPosition,
        int formulaVersion,
        string catalogSha256)
    {
        NarrativeFormulaCandidate[] values = (rankedCandidates ?? throw new ArgumentNullException(nameof(rankedCandidates))).ToArray();
        if (values.Length == 0) throw new InvalidOperationException("A deterministic winner requires at least one candidate.");
        string seed = Require(persistentOwnerId, nameof(persistentOwnerId)) + "\n"
            + Require(manifestationPosition, nameof(manifestationPosition)) + "\n"
            + formulaVersion + "\n" + RequireSha256(catalogSha256);
        byte[] digest;
        using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        ulong value = 0;
        for (int index = 0; index < sizeof(ulong); index++) value = (value << 8) | digest[index];
        return values[(int)(value % (ulong)values.Length)];
    }

    public static double NormalizedScore(NarrativeFormulaCandidate candidate, int budget)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        double affinity = NormalizeNonNegative(candidate.AffinityScore);
        double utilization = candidate.BudgetUtilization(budget);
        double novelty = NormalizeNonNegative(candidate.NoveltyScore);
        return 0.80d * affinity + 0.15d * utilization + 0.05d * novelty;
    }

    private static double NormalizeNonNegative(double value)
    {
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        return value == 0d ? 0d : 1d - (1d / (1d + value));
    }

    private static string Require(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException("A canonical non-empty seed component is required.", name);
        return canonical;
    }

    private static string RequireSha256(string value)
    {
        string canonical = Require(value, nameof(value));
        if (canonical.Length != 64 || canonical.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("A 64-character catalog SHA-256 is required.", nameof(value));
        return canonical.ToLowerInvariant();
    }

    private sealed class AxisState
    {
        public int VariableCost;
        public int MinimumAdditionalCost;
        public double UtilityScore;
        public NarrativeFormulaParameterValue[] Parameters;
    }

    private static IReadOnlyList<AxisState> EnumerateAxisStates(
        NarrativeFormulaCapabilityDescriptor descriptor,
        int budget)
    {
        NarrativeFormulaQuantizedRange[] ranges = NarrativeFormulaParameterIds.Required
            .Select(descriptor.RequireRange).ToArray();
        long stateCount = 1;
        foreach (NarrativeFormulaQuantizedRange range in ranges)
        {
            stateCount = checked(stateCount * (range.StepCount + 1L));
            if (stateCount > MaximumEnumeratedStates)
                throw new InvalidOperationException($"Capability '{descriptor.CapabilityId}' exceeds the bounded optimizer state limit.");
        }
        Dictionary<int, AxisState> byCost = new Dictionary<int, AxisState>();
        long[] units = new long[ranges.Length];
        Enumerate(0);
        return byCost.OrderBy(value => value.Key).Select(value => value.Value).ToArray();

        void Enumerate(int index)
        {
            if (index < ranges.Length)
            {
                NarrativeFormulaQuantizedRange range = ranges[index];
                for (long value = range.MinimumUnits; value <= range.MaximumUnits; value += range.QuantumUnits)
                {
                    units[index] = value;
                    Enumerate(index + 1);
                }
                return;
            }
            int variableCost = 0;
            for (int rangeIndex = 0; rangeIndex < ranges.Length; rangeIndex++)
            {
                long steps = (units[rangeIndex] - ranges[rangeIndex].MinimumUnits) / ranges[rangeIndex].QuantumUnits;
                variableCost = checked(variableCost
                    + checked((int)steps) * ranges[rangeIndex].CostPerQuantum);
            }
            if (variableCost > budget) return;
            AxisState candidate = new AxisState
            {
                VariableCost = variableCost,
                MinimumAdditionalCost = ranges.Select((range, rangeIndex) =>
                        units[rangeIndex] < range.MaximumUnits ? range.CostPerQuantum : int.MaxValue)
                    .Min(),
                Parameters = ranges.Select((range, rangeIndex) =>
                    new NarrativeFormulaParameterValue(range.ParameterId, units[rangeIndex])).ToArray()
            };
            candidate.UtilityScore = ParameterUtilityScore(descriptor, candidate.Parameters);
            if (!byCost.TryGetValue(variableCost, out AxisState existing)
                || candidate.UtilityScore > existing.UtilityScore + 1e-12d
                || (Math.Abs(candidate.UtilityScore - existing.UtilityScore) <= 1e-12d
                    && candidate.MinimumAdditionalCost > existing.MinimumAdditionalCost)
                || (Math.Abs(candidate.UtilityScore - existing.UtilityScore) <= 1e-12d
                    && candidate.MinimumAdditionalCost == existing.MinimumAdditionalCost
                    && CompareParameters(candidate.Parameters, existing.Parameters) < 0))
                byCost[variableCost] = candidate;
        }
    }

    private static IEnumerable<NarrativeFormulaCapabilityInput[]> Choose(
        NarrativeFormulaCapabilityInput[] values,
        int count)
    {
        if (count == 0)
        {
            yield return Array.Empty<NarrativeFormulaCapabilityInput>();
            yield break;
        }
        if (count > values.Length) yield break;
        int[] indices = Enumerable.Range(0, count).ToArray();
        while (true)
        {
            yield return indices.Select(index => values[index]).ToArray();
            int cursor = count - 1;
            while (cursor >= 0 && indices[cursor] == values.Length - count + cursor) cursor--;
            if (cursor < 0) yield break;
            indices[cursor]++;
            for (int index = cursor + 1; index < count; index++) indices[index] = indices[index - 1] + 1;
        }
    }

    private static bool TryGetPairCost(
        IReadOnlyList<NarrativeFormulaCapabilityDescriptor> descriptors,
        out int cost)
    {
        cost = 0;
        for (int first = 0; first < descriptors.Count; first++)
        for (int second = first + 1; second < descriptors.Count; second++)
        {
            NarrativeFormulaCapabilityDescriptor left = descriptors[first];
            NarrativeFormulaCapabilityDescriptor right = descriptors[second];
            if (left.ConflictGroups.Intersect(right.ConflictGroups, StringComparer.Ordinal).Any()
                || left.ForbiddenSynergies.Contains(right.CapabilityId, StringComparer.Ordinal)
                || right.ForbiddenSynergies.Contains(left.CapabilityId, StringComparer.Ordinal))
                return false;
            NarrativeFormulaPairSynergyCost leftCost = left.PairSynergyCosts.SingleOrDefault(value =>
                string.Equals(value.OtherCapabilityId, right.CapabilityId, StringComparison.Ordinal));
            NarrativeFormulaPairSynergyCost rightCost = right.PairSynergyCosts.SingleOrDefault(value =>
                string.Equals(value.OtherCapabilityId, left.CapabilityId, StringComparison.Ordinal));
            if ((leftCost == null) != (rightCost == null)
                || (leftCost != null && leftCost.Cost != rightCost.Cost))
                throw new InvalidOperationException(
                    $"Pair-synergy cost '{left.CapabilityId}/{right.CapabilityId}' must be symmetric.");
            if (leftCost != null) cost = checked(cost + leftCost.Cost);
        }
        return true;
    }

    private static bool CanAddQuantum(
        IEnumerable<NarrativeFormulaCapabilityAllocation> allocations,
        int currentCost,
        int budget)
    {
        foreach (NarrativeFormulaCapabilityAllocation allocation in allocations)
        foreach (NarrativeFormulaParameterValue parameter in allocation.Parameters)
        {
            NarrativeFormulaQuantizedRange range = allocation.Descriptor.RequireRange(parameter.ParameterId);
            if (parameter.Units < range.MaximumUnits
                && currentCost + range.CostPerQuantum <= budget) return true;
        }
        return false;
    }

    private static int CompareAllocations(
        IEnumerable<NarrativeFormulaCapabilityAllocation> left,
        IEnumerable<NarrativeFormulaCapabilityAllocation> right) =>
        string.CompareOrdinal(AllocationSignature(left), AllocationSignature(right));

    private static int MinimumAdditionalCost(
        IEnumerable<NarrativeFormulaCapabilityAllocation> allocations)
    {
        int minimum = int.MaxValue;
        foreach (NarrativeFormulaCapabilityAllocation allocation in allocations)
        foreach (NarrativeFormulaParameterValue parameter in allocation.Parameters)
        {
            NarrativeFormulaQuantizedRange range = allocation.Descriptor.RequireRange(parameter.ParameterId);
            if (parameter.Units < range.MaximumUnits) minimum = Math.Min(minimum, range.CostPerQuantum);
        }
        return minimum;
    }

    private static double AllocationUtilityScore(
        IEnumerable<NarrativeFormulaCapabilityAllocation> allocations) =>
        (allocations ?? throw new ArgumentNullException(nameof(allocations)))
            .Sum(allocation => ParameterUtilityScore(
                allocation.Descriptor,
                allocation.Parameters));

    private static double ParameterUtilityScore(
        NarrativeFormulaCapabilityDescriptor descriptor,
        IEnumerable<NarrativeFormulaParameterValue> parameters)
    {
        NarrativeFormulaParameterValue[] values = (parameters
                ?? throw new ArgumentNullException(nameof(parameters))).ToArray();
        double total = 0d;
        int variableAxes = 0;
        foreach (NarrativeFormulaParameterValue parameter in values)
        {
            NarrativeFormulaQuantizedRange range = descriptor.RequireRange(parameter.ParameterId);
            if (range.MaximumUnits == range.MinimumUnits) continue;
            double progress = (double)(parameter.Units - range.MinimumUnits)
                / (range.MaximumUnits - range.MinimumUnits);
            total += Math.Sqrt(Math.Max(0d, Math.Min(1d, progress)));
            variableAxes++;
        }
        return variableAxes == 0 ? 0d : total / variableAxes;
    }

    private static string AllocationSignature(IEnumerable<NarrativeFormulaCapabilityAllocation> values) =>
        string.Join("|", values.Select(value => value.Descriptor.CapabilityId + ";"
            + string.Join(";", value.Parameters.Select(parameter =>
                parameter.ParameterId + "=" + parameter.Units))));

    private static int CompareParameters(
        IEnumerable<NarrativeFormulaParameterValue> left,
        IEnumerable<NarrativeFormulaParameterValue> right) =>
        string.CompareOrdinal(
            string.Join(";", left.Select(value => value.ParameterId + "=" + value.Units)),
            string.Join(";", right.Select(value => value.ParameterId + "=" + value.Units)));
}
