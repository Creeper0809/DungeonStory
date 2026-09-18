using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NarrativeFormulaCoreDebugScenarios
{
    private const string CatalogSha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [MenuItem("DungeonStory/Narrative Formula/Run Core Formula Scenarios")]
    public static void RunAll()
    {
        VerifyStrengthAndReuseDecay();
        VerifyStrengthIsOrderIndependentAndSaturating();
        VerifyInvalidNumbersFailClosed();
        VerifyOptimizerBudgetAndMaximality();
        VerifyInstanceLocalDrawbackCredit();
        VerifyCompositionCostsAndConflicts();
        VerifyExactPostSelectionAllocation();
        VerifyBalancedMathematicalAllocation();
        VerifyModuleSelectionContract();
        VerifyRankingAndDeterministicSelection();
        Debug.Log("Narrative formula core scenarios passed.");
    }

    private static void VerifyInstanceLocalDrawbackCredit()
    {
        NarrativeFormulaCapabilityDescriptor descriptor = Descriptor(
            "tradeoff", "conflict:tradeoff");
        NarrativeFormulaGenerationCostContext context =
            new NarrativeFormulaGenerationCostContext(1, false, 1);
        NarrativeFormulaDrawbackCreditPolicy policy = new(
            playerChoiceMaximumBudgetFraction: 0.25d,
            automaticMaximumBudgetFraction: 0.10d,
            absoluteMaximumCredit: 3,
            requireNegativeEvidenceForAutomatic: true);
        NarrativeFormulaDrawbackOption applied = new(
            "drawback:cooldown:+2",
            requestedCredit: 3,
            NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
            reachable: true,
            mandatory: true,
            separatelyRemovable: false,
            cancelsSelectedBenefit: false,
            hasNegativeNarrativeEvidence: false);
        IReadOnlyList<NarrativeFormulaCandidate> candidates = NarrativeFormulaCore.Optimize(
            descriptor, context, 8, 4d, 1d, new[] { "fact:tradeoff" },
            maximumResults: 3, drawbackPolicy: policy, drawbackOption: applied);
        Require(candidates.Count > 0, "A valid attached drawback must produce candidates.");
        foreach (NarrativeFormulaCandidate candidate in candidates)
        {
            Require(candidate.DrawbackCredit == 2,
                "Player-choice drawback credit must be capped to 25% of the narrative budget.");
            Require(candidate.PositiveCost - candidate.DrawbackCredit == candidate.CalculatedCost,
                "Net cost must equal positive cost minus accepted local drawback credit.");
            Require(candidate.CalculatedCost <= 8 && candidate.PositiveCost <= 10,
                "Drawback credit must increase only this candidate's local spendable budget.");
            Require(!CanIncreaseAnyParameter(candidate, 10),
                "The optimizer must maximize against the local spendable budget.");
        }

        NarrativeFormulaDrawbackOption unsupportedAutomatic = new(
            "drawback:auto:no-negative-evidence",
            requestedCredit: 3,
            NarrativeFormulaDrawbackSelectionKind.Automatic,
            reachable: true,
            mandatory: true,
            separatelyRemovable: false,
            cancelsSelectedBenefit: false,
            hasNegativeNarrativeEvidence: false);
        NarrativeFormulaDrawbackResolution noCredit = policy.Resolve(
            20, unsupportedAutomatic);
        Require(noCredit.AcceptedCredit == 0,
            "Automatic drawbacks without negative narrative evidence must grant zero credit.");

        RequireThrows<InvalidOperationException>(() => policy.Resolve(
                20,
                new NarrativeFormulaDrawbackOption(
                    "drawback:optional", 2,
                    NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                    reachable: true,
                    mandatory: false,
                    separatelyRemovable: false,
                    cancelsSelectedBenefit: false,
                    hasNegativeNarrativeEvidence: true)),
            "Optional drawbacks must fail closed instead of granting credit.");
        RequireThrows<InvalidOperationException>(() => policy.Resolve(
                20,
                new NarrativeFormulaDrawbackOption(
                    "drawback:cancellation", 2,
                    NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                    reachable: true,
                    mandatory: true,
                    separatelyRemovable: false,
                    cancelsSelectedBenefit: true,
                    hasNegativeNarrativeEvidence: true)),
            "A drawback that merely cancels the selected benefit must fail closed.");
    }

    private static void VerifyStrengthAndReuseDecay()
    {
        NarrativeFormulaStrengthPolicy policy = Policy();
        NarrativeFormulaStrength empty = NarrativeFormulaCore.CalculateStrength(
            policy,
            Array.Empty<NarrativeFormulaEvidence>());
        Require(empty.Raw == 0d && empty.Power == 0d && empty.Budget == policy.BaseBudget,
            "Empty evidence must resolve to the base budget.");

        double first = NarrativeFormulaCore.CalculateStrength(policy, new[]
        {
            Evidence("fact:first", 0)
        }).Raw;
        double second = NarrativeFormulaCore.CalculateStrength(policy, new[]
        {
            Evidence("fact:second", 1)
        }).Raw;
        double third = NarrativeFormulaCore.CalculateStrength(policy, new[]
        {
            Evidence("fact:third", 2)
        }).Raw;
        RequireNearly(second, first / 2d, "Second use must contribute exactly 1/2.");
        RequireNearly(third, first / 3d, "Third use must contribute exactly 1/3.");
    }

    private static void VerifyStrengthIsOrderIndependentAndSaturating()
    {
        NarrativeFormulaStrengthPolicy policy = Policy();
        NarrativeFormulaEvidence fresh = Evidence("fact:fresh", 0);
        NarrativeFormulaEvidence reused = Evidence("fact:reused", 2);
        NarrativeFormulaStrength left = NarrativeFormulaCore.CalculateStrength(
            policy,
            new[] { fresh, reused });
        NarrativeFormulaStrength right = NarrativeFormulaCore.CalculateStrength(
            policy,
            new[] { reused, fresh });
        RequireNearly(left.Diversity, right.Diversity, "Diversity must not depend on evidence order.");
        RequireNearly(left.Raw, right.Raw, "Strength must not depend on evidence order.");
        Require(left.Budget == right.Budget, "Budget must not depend on evidence order.");

        List<NarrativeFormulaEvidence> growing = new List<NarrativeFormulaEvidence>();
        double previousPower = 0d;
        int previousBudget = policy.BaseBudget;
        for (int index = 0; index < 64; index++)
        {
            growing.Add(new NarrativeFormulaEvidence(
                "fact:growing:" + index,
                "event:" + index,
                "action:" + index,
                "relationship:" + index,
                "domain:" + index,
                5,
                4d,
                0));
            NarrativeFormulaStrength current = NarrativeFormulaCore.CalculateStrength(policy, growing);
            Require(current.Power >= previousPower && current.Power < 1d,
                "Saturated power must be monotonic and remain below one.");
            Require(current.Budget >= previousBudget
                    && current.Budget <= policy.BaseBudget + policy.PowerScale,
                "Budget must be monotonic and bounded by its theoretical limit.");
            previousPower = current.Power;
            previousBudget = current.Budget;
        }
    }

    private static void VerifyInvalidNumbersFailClosed()
    {
        RequireThrows<ArgumentOutOfRangeException>(() => new NarrativeFormulaStrengthPolicy(
            1, 0, 1, double.NaN, 0d, 1d, new[] { 1d }),
            "NaN soft-cap values must fail closed.");
        RequireThrows<ArgumentOutOfRangeException>(() => new NarrativeFormulaEvidence(
            "fact:nan", "event", "action", string.Empty, "domain", 1,
            double.PositiveInfinity, 0),
            "Infinite importance must fail closed.");
        RequireThrows<OverflowException>(() => new NarrativeFormulaCapabilityDescriptor(
                "overflow",
                Ranges(0, 5, 5, int.MaxValue),
                NarrativeFormulaParameterIds.Required,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                Array.Empty<NarrativeFormulaPairSynergyCost>(),
                1d,
                new[] { "domain" },
                new[] { "conflict:overflow" },
                Array.Empty<string>(),
                "overflow",
                "overflow")
            .CalculateContextCost(new NarrativeFormulaGenerationCostContext(2, true, 2), 2),
            "Cost overflow must fail closed.");
    }

    private static void VerifyOptimizerBudgetAndMaximality()
    {
        NarrativeFormulaCapabilityDescriptor descriptor = Descriptor("damage", "conflict:damage");
        NarrativeFormulaGenerationCostContext context =
            new NarrativeFormulaGenerationCostContext(1, false, 1);
        const int budget = 9;
        IReadOnlyList<NarrativeFormulaCandidate> candidates = NarrativeFormulaCore.Optimize(
            descriptor,
            context,
            budget,
            4d,
            1d,
            new[] { "fact:optimizer" });
        Require(candidates.Count > 0 && candidates.Count <= 3,
            "Optimizer must retain one to three legal candidates.");
        foreach (NarrativeFormulaCandidate candidate in candidates)
        {
            Require(candidate.CalculatedCost <= budget,
                "Optimizer emitted an over-budget candidate.");
            Require(NarrativeFormulaCore.CalculateCost(candidate.Allocations, context)
                    == candidate.CalculatedCost,
                "Stored and recomputed formula costs must match.");
            Require(!CanIncreaseAnyParameter(candidate, budget),
                "Optimizer left enough budget for another legal quantum.");
        }
    }

    private static void VerifyCompositionCostsAndConflicts()
    {
        NarrativeFormulaCapabilityDescriptor primary = Descriptor(
            "primary", "conflict:primary", new NarrativeFormulaPairSynergyCost("support", 2));
        NarrativeFormulaCapabilityDescriptor support = Descriptor(
            "support", "conflict:support", new NarrativeFormulaPairSynergyCost("primary", 2));
        NarrativeFormulaGenerationCostContext context =
            new NarrativeFormulaGenerationCostContext(2, true, 3);
        IReadOnlyList<NarrativeFormulaCandidate> composed = NarrativeFormulaCore.OptimizeCompositions(
            new[]
            {
                new NarrativeFormulaCapabilityInput(primary, 4d, 1d),
                new NarrativeFormulaCapabilityInput(support, 3d, 1d)
            },
            context,
            80,
            "primary",
            new[] { "fact:composition" },
            2,
            3);
        Require(composed.Any(value => value.Allocations.Count == 2),
            "A legal symmetric pair must be eligible for composition.");
        foreach (NarrativeFormulaCandidate candidate in composed)
            Require(NarrativeFormulaCore.CalculateCost(candidate.Allocations, context)
                    == candidate.CalculatedCost,
                "Composition cost must include trigger, certainty, target, multi-effect, and pair costs.");

        NarrativeFormulaCapabilityDescriptor blocked = Descriptor("blocked", "conflict:primary");
        IReadOnlyList<NarrativeFormulaCandidate> conflictResult = NarrativeFormulaCore.OptimizeCompositions(
            new[]
            {
                new NarrativeFormulaCapabilityInput(primary, 4d, 1d),
                new NarrativeFormulaCapabilityInput(blocked, 10d, 1d)
            },
            new NarrativeFormulaGenerationCostContext(0, false, 1),
            30,
            "primary",
            new[] { "fact:conflict" },
            2,
            3);
        Require(conflictResult.All(value => value.Allocations.Count == 1),
            "Capabilities sharing a conflict group must never be composed.");
    }

    private static void VerifyExactPostSelectionAllocation()
    {
        NarrativeFormulaCapabilityDescriptor primary = Descriptor(
            "selected-primary", "conflict:selected-primary",
            new NarrativeFormulaPairSynergyCost("selected-support", 1));
        NarrativeFormulaCapabilityDescriptor support = Descriptor(
            "selected-support", "conflict:selected-support",
            new NarrativeFormulaPairSynergyCost("selected-primary", 1));
        NarrativeFormulaGenerationCostContext context = new(1, false, 1);
        IReadOnlyList<NarrativeFormulaCandidate> exact =
            NarrativeFormulaCore.OptimizeExactComposition(
                new[]
                {
                    new NarrativeFormulaCapabilityInput(primary, 4d, 1d),
                    new NarrativeFormulaCapabilityInput(support, 2d, 1d)
                },
                context,
                30,
                new[] { "fact:exact" });
        Require(exact.Count > 0
                && exact.All(value => value.Allocations.Count == 2)
                && exact.All(value => value.Allocations
                    .Select(allocation => allocation.Descriptor.CapabilityId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(new[]
                    {
                        "selected-primary", "selected-support"
                    }, StringComparer.Ordinal)),
            "Post-selection allocation must preserve every selected capability identity.");
        Require(exact.All(value => !CanIncreaseAnyParameter(value, 30)),
            "Post-selection allocation must spend to the next-quantum boundary.");

        NarrativeFormulaCapabilityDescriptor blocked = Descriptor(
            "selected-blocked", "conflict:selected-primary");
        IReadOnlyList<NarrativeFormulaCandidate> illegal =
            NarrativeFormulaCore.OptimizeExactComposition(
                new[]
                {
                    new NarrativeFormulaCapabilityInput(primary, 1d, 1d),
                    new NarrativeFormulaCapabilityInput(blocked, 1d, 1d)
                },
                context,
                30,
                new[] { "fact:exact-conflict" });
        Require(illegal.Count == 0,
            "An exact conflicting selection must fail instead of silently dropping a module.");
    }

    private static void VerifyModuleSelectionContract()
    {
        NarrativeFormulaCapabilityDescriptor offense = Descriptor(
            "offer-offense", "conflict:offense");
        NarrativeFormulaCapabilityDescriptor support = Descriptor(
            "offer-support", "conflict:support");
        NarrativeFormulaCapabilityDescriptor drawback = Descriptor(
            "offer-drawback", "conflict:drawback");
        NarrativeFormulaModuleOffer[] offers =
        {
            Offer("module:offense", NarrativeFormulaModulePolarity.Positive,
                offense),
            new NarrativeFormulaModuleOffer(
                "module:support",
                NarrativeFormulaModulePolarity.Positive,
                "동료를 지원한다.",
                new NarrativeFormulaCapabilityInput(support, 1d, 1d),
                1,
                requiredCompanionModuleIds: new[] { "module:offense" }),
            Offer("module:drawback", NarrativeFormulaModulePolarity.Drawback,
                drawback)
        };
        NarrativeFormulaModuleSelectionRequest request = new(
            "module-selection:test:1",
            offers,
            new[] { "fact:offer:1", "fact:offer:2" },
            maximumPositiveModules: 2,
            maximumDrawbackModules: 1);
        bool valid = NarrativeFormulaModuleSelectionValidator.TryValidate(
            request,
            new NarrativeFormulaModuleSelectionChoice(
                request.SelectionId,
                new[] { "module:offense", "module:support" },
                new[] { "module:drawback" },
                new[] { "fact:offer:2" }),
            out NarrativeFormulaValidatedModuleSelection selected,
            out string validError);
        Require(valid && selected.PositiveModules.Count == 2
                && selected.DrawbackModules.Count == 1
                && string.IsNullOrEmpty(validError),
            "A legal offered module subset must validate exactly.");
        NarrativeFormulaDrawbackCreditPolicy creditPolicy = new(
            playerChoiceMaximumBudgetFraction: 0.25d,
            automaticMaximumBudgetFraction: 0.10d,
            absoluteMaximumCredit: 4,
            requireNegativeEvidenceForAutomatic: true);
        IReadOnlyList<NarrativeFormulaSelectedModuleResolution> resolved =
            NarrativeFormulaCore.OptimizeSelectedModules(
                selected,
                new NarrativeFormulaGenerationCostContext(1, false, 1),
                budget: 30,
                creditPolicy,
                NarrativeFormulaDrawbackSelectionKind.Automatic,
                hasNegativeNarrativeEvidence: true);
        Require(resolved.Count > 0
                && resolved.All(value => value.Positive.Allocations.Count == 2)
                && resolved.All(value => value.DrawbackSeverity != null)
                && resolved.All(value => value.Positive.DrawbackCredit is > 0 and <= 3)
                && resolved.All(value => value.Positive.CalculatedCost <= 30),
            "Validated selected benefits and drawbacks must be jointly quantified within bounded local credit.");

        Require(!NarrativeFormulaModuleSelectionValidator.TryValidate(
                request,
                new NarrativeFormulaModuleSelectionChoice(
                    request.SelectionId,
                    Array.Empty<string>(),
                    new[] { "module:drawback" },
                    new[] { "fact:offer:1" }),
                out _, out string negativeOnlyError)
                && negativeOnlyError.Contains("positive", StringComparison.OrdinalIgnoreCase),
            "A drawback-only response must fail closed.");
        Require(!NarrativeFormulaModuleSelectionValidator.TryValidate(
                request,
                new NarrativeFormulaModuleSelectionChoice(
                    request.SelectionId,
                    new[] { "module:support" },
                    Array.Empty<string>(),
                    new[] { "fact:offer:1" }),
                out _, out string companionError)
                && companionError.Contains("companion", StringComparison.OrdinalIgnoreCase),
            "A selected module with a missing required companion must fail closed.");
        Require(!NarrativeFormulaModuleSelectionValidator.TryValidate(
                request,
                new NarrativeFormulaModuleSelectionChoice(
                    request.SelectionId,
                    new[] { "module:invented" },
                    Array.Empty<string>(),
                    new[] { "fact:offer:1" }),
                out _, out string inventedError)
                && inventedError.Contains("not offered", StringComparison.OrdinalIgnoreCase),
            "An invented module ID must fail closed.");
    }

    private static void VerifyBalancedMathematicalAllocation()
    {
        NarrativeFormulaCapabilityDescriptor first = ScalingDescriptor("balanced-first");
        NarrativeFormulaCapabilityDescriptor second = ScalingDescriptor("balanced-second");
        NarrativeFormulaCandidate result = NarrativeFormulaCore.OptimizeExactComposition(
                new[]
                {
                    new NarrativeFormulaCapabilityInput(first, 1d, 1d),
                    new NarrativeFormulaCapabilityInput(second, 1d, 1d)
                },
                new NarrativeFormulaGenerationCostContext(0, false, 1),
                10,
                new[] { "fact:balanced" },
                maximumResults: 1)
            .Single();
        long firstMagnitude = result.Allocations.Single(value =>
                string.Equals(value.Descriptor.CapabilityId, first.CapabilityId,
                    StringComparison.Ordinal))
            .Parameters.Single(value => string.Equals(value.ParameterId,
                NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal)).Units;
        long secondMagnitude = result.Allocations.Single(value =>
                string.Equals(value.Descriptor.CapabilityId, second.CapabilityId,
                    StringComparison.Ordinal))
            .Parameters.Single(value => string.Equals(value.ParameterId,
                NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal)).Units;
        Require(result.CalculatedCost == 10 && Math.Abs(firstMagnitude - secondMagnitude) <= 1,
            "Equal selected capabilities must receive a balanced diminishing-return allocation.");
    }

    private static void VerifyRankingAndDeterministicSelection()
    {
        NarrativeFormulaCapabilityDescriptor descriptor = Descriptor("rank", "conflict:rank");
        NarrativeFormulaCandidate affinityOnly = Candidate(descriptor, 1, 10d, 0d);
        NarrativeFormulaCandidate betterComposite = Candidate(descriptor, 10, 9d, 0d);
        IReadOnlyList<NarrativeFormulaCandidate> ranked = NarrativeFormulaCore.Rank(
            new[] { affinityOnly, betterComposite },
            10);
        Require(ReferenceEquals(ranked[0], betterComposite),
            "Ranking must put the highest combined narrative/utilization score first.");

        NarrativeFormulaCandidate first = NarrativeFormulaCore.ChooseDeterministicWinner(
            ranked, "owner:1", "active:3", 1, CatalogSha256);
        IReadOnlyList<NarrativeFormulaCandidate> reranked = NarrativeFormulaCore.Rank(
            new[] { betterComposite, affinityOnly },
            10);
        NarrativeFormulaCandidate second = NarrativeFormulaCore.ChooseDeterministicWinner(
            reranked, "owner:1", "active:3", 1, CatalogSha256);
        Require(string.Equals(first.CanonicalSignature, second.CanonicalSignature, StringComparison.Ordinal),
            "Deterministic selection must not depend on caller enumeration order.");
    }

    private static NarrativeFormulaStrengthPolicy Policy()
    {
        return new NarrativeFormulaStrengthPolicy(
            1,
            3,
            20,
            8d,
            0d,
            4d,
            new[] { 1d, 1.5d, 2d, 3d, 5d });
    }

    private static NarrativeFormulaEvidence Evidence(string id, int influenceUseCount)
    {
        return new NarrativeFormulaEvidence(
            id,
            "event:shared",
            "action:shared",
            "relationship:shared",
            "domain:shared",
            3,
            3d,
            influenceUseCount);
    }

    private static NarrativeFormulaCapabilityDescriptor Descriptor(
        string id,
        string conflictGroup,
        params NarrativeFormulaPairSynergyCost[] pairCosts)
    {
        return new NarrativeFormulaCapabilityDescriptor(
            id,
            Ranges(0, 10, 5, 1),
            NarrativeFormulaParameterIds.Required,
            1,
            1,
            2,
            1,
            1,
            pairCosts,
            1d,
            new[] { "domain" },
            new[] { conflictGroup },
            Array.Empty<string>(),
            id,
            id);
    }

    private static NarrativeFormulaCapabilityDescriptor ScalingDescriptor(string id)
    {
        return new NarrativeFormulaCapabilityDescriptor(
            id,
            new[]
            {
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.Magnitude, 0, 10, 1, 0, 1),
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.Duration, 0, 0, 1, 0, 1),
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.Count, 1, 1, 1, 0, 1),
                new NarrativeFormulaQuantizedRange(
                    NarrativeFormulaParameterIds.TargetCount, 1, 1, 1, 0, 1)
            },
            new[] { NarrativeFormulaParameterIds.Magnitude },
            0,
            0,
            0,
            0,
            0,
            Array.Empty<NarrativeFormulaPairSynergyCost>(),
            1d,
            new[] { "domain" },
            new[] { "conflict:" + id },
            Array.Empty<string>(),
            id,
            id);
    }

    private static NarrativeFormulaQuantizedRange[] Ranges(
        long minimum,
        long maximum,
        long quantum,
        int costPerQuantum)
    {
        return NarrativeFormulaParameterIds.Required.Select(parameterId =>
            new NarrativeFormulaQuantizedRange(
                parameterId,
                minimum,
                maximum,
                quantum,
                0,
                costPerQuantum)).ToArray();
    }

    private static NarrativeFormulaCandidate Candidate(
        NarrativeFormulaCapabilityDescriptor descriptor,
        int cost,
        double affinity,
        double novelty)
    {
        return new NarrativeFormulaCandidate(
            descriptor,
            descriptor.Ranges.Values.Select(value =>
                new NarrativeFormulaParameterValue(value.ParameterId, value.MinimumUnits)),
            cost,
            affinity,
            novelty,
            new[] { "fact:rank" });
    }

    private static NarrativeFormulaModuleOffer Offer(
        string moduleId,
        NarrativeFormulaModulePolarity polarity,
        NarrativeFormulaCapabilityDescriptor descriptor) => new(
        moduleId,
        polarity,
        "검증용 모듈 설명",
        new NarrativeFormulaCapabilityInput(descriptor, 1d, 1d),
        1);

    private static bool CanIncreaseAnyParameter(
        NarrativeFormulaCandidate candidate,
        int budget)
    {
        foreach (NarrativeFormulaCapabilityAllocation allocation in candidate.Allocations)
        foreach (NarrativeFormulaParameterValue parameter in allocation.Parameters)
        {
            NarrativeFormulaQuantizedRange range = allocation.Descriptor.RequireRange(parameter.ParameterId);
            if (parameter.Units < range.MaximumUnits
                && candidate.CalculatedCost + range.CostPerQuantum <= budget)
                return true;
        }
        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireNearly(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) > 1e-10d)
            throw new InvalidOperationException(message + $" Expected {expected:R}, got {actual:R}.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
