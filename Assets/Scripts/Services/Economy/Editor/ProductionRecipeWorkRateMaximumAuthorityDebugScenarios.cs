#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProductionRecipeWorkRateMaximumAuthorityDebugScenarios
{
    private const string FacilityId = "building:qa-work-rate";
    private const string WorkstationTag = "workstation:qa-work-rate";
    private const string FactorA = "work-rate:qa:a";
    private const string FactorB = "work-rate:qa:b";

    [MenuItem("DungeonStory/V27/Production/Validate Recipe Work Rate Maximum Authority")]
    public static void Validate()
    {
        VerifyMissingContributorIsTyped();
        VerifyBelowClampRoundsOnce();
        VerifyExactAndOverClamp();
        VerifyShuffleDigest();
        VerifyAutomaticLaneContracts();
        VerifyNonFiniteAndOverflowRejected();
        Debug.Log(
            "[ProductionRecipeWorkRateMaximumAuthority] focused scenarios passed.");
    }

    private static void VerifyMissingContributorIsTyped()
    {
        ProductionRecipeSO recipe = Recipe("recipe:qa-work-rate-missing");
        try
        {
            ProductionRecipeWorkRateMaximumAuthority authority = Authority(
                new[] { FactorA, FactorB },
                new[] { Contributor(FactorA, 1m) });
            ProductionRecipeWorkRateMaximumAuthorityResult detailed = authority
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    ManualLanes(),
                    recipe);
            Require(!detailed.HasSnapshot
                    && detailed.MissingReason
                    == ProductionRecipeWorkRateMaximumGapReason.MissingContributor
                    && string.Equals(
                        detailed.ContributorId,
                        FactorB,
                        StringComparison.Ordinal),
                "A missing required contributor was not retained as a typed gap.");

            ProductionRecipeWorkRateMaximumQueryResult contract = authority.Capture(
                FacilityId,
                WorkstationTag,
                ManualLanes(),
                recipe);
            Require(!contract.HasSnapshot
                    && contract.MissingReason
                    == ProductionThroughputGapReason.RecipeWorkRateMaximumMissing
                    && contract.Detail.Contains(
                        nameof(ProductionRecipeWorkRateMaximumGapReason
                            .MissingContributor),
                        StringComparison.Ordinal)
                    && contract.Detail.Contains(FactorB, StringComparison.Ordinal),
                "The public throughput contract discarded the typed gap detail.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyBelowClampRoundsOnce()
    {
        ProductionRecipeSO recipe = Recipe("recipe:qa-work-rate-rounding");
        try
        {
            ProductionRecipeWorkRateMaximumAuthority authority = Authority(
                new[] { FactorA },
                new[] { Contributor(FactorA, 1.2341m) });
            ProductionRecipeWorkRateMaximumAuthorityResult result = authority
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    ManualLanes(),
                    recipe);
            Require(result.HasSnapshot
                    && result.Snapshot.ManualMilliWuPerSecond == 1_235L
                    && result.Snapshot.AutomaticMilliWuPerSecond == 0L,
                "The below-clamp rate was not ceiled exactly once to mWU/s.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyExactAndOverClamp()
    {
        ProductionRecipeSO recipe = Recipe("recipe:qa-work-rate-clamp");
        try
        {
            ProductionRecipeWorkRateMaximumAuthority exact = Authority(
                new[] { FactorA },
                new[] { Contributor(FactorA, 8m) });
            ProductionRecipeWorkRateMaximumAuthority over = Authority(
                new[] { FactorA },
                new[] { Contributor(FactorA, 8.001m) });
            Require(exact.CaptureDetailed(
                        FacilityId,
                        WorkstationTag,
                        ManualLanes(),
                        recipe)
                    .Snapshot.ManualMilliWuPerSecond == 8_000L,
                "The exact runtime maximum did not remain 8,000 mWU/s.");
            Require(over.CaptureDetailed(
                        FacilityId,
                        WorkstationTag,
                        ManualLanes(),
                        recipe)
                    .Snapshot.ManualMilliWuPerSecond == 8_000L,
                "The final runtime maximum clamp did not cap an over-bound rate.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyShuffleDigest()
    {
        ProductionRecipeSO recipe = Recipe("recipe:qa-work-rate-shuffle");
        try
        {
            ProductionRecipeWorkRateMaximumAuthority first = Authority(
                new[] { FactorB, FactorA },
                new[]
                {
                    Contributor(FactorA, 1.1m),
                    Contributor(FactorB, 1.2m)
                });
            ProductionRecipeWorkRateMaximumAuthority second = Authority(
                new[] { FactorA, FactorB },
                new[]
                {
                    Contributor(FactorB, 1.2m),
                    Contributor(FactorA, 1.1m)
                });
            ProductionRecipeWorkRateMaximumSnapshot firstSnapshot = first
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    ManualLanes(),
                    recipe)
                .Snapshot;
            ProductionRecipeWorkRateMaximumSnapshot secondSnapshot = second
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    ManualLanes(),
                    recipe)
                .Snapshot;
            Require(firstSnapshot.ManualMilliWuPerSecond == 1_320L
                    && secondSnapshot.ManualMilliWuPerSecond == 1_320L
                    && string.Equals(
                        firstSnapshot.SourceDigest,
                        secondSnapshot.SourceDigest,
                        StringComparison.Ordinal),
                "Manifest or registry input order changed the canonical result.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyAutomaticLaneContracts()
    {
        ProductionRecipeSO recipe = Recipe("recipe:qa-work-rate-automatic");
        try
        {
            CountingAutomaticRateQuery automatic = new(
                Complete(1.25m, "automatic:complete"));
            ProductionRecipeWorkRateMaximumAuthority manual = Authority(
                new[] { FactorA },
                new[] { Contributor(FactorA, 1m) },
                automatic);
            ProductionRecipeWorkRateMaximumSnapshot manualSnapshot = manual
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    ManualLanes(),
                    recipe)
                .Snapshot;
            Require(manualSnapshot.AutomaticMilliWuPerSecond == 0L
                    && automatic.CallCount == 0,
                "A manual-only lane queried or published automatic work.");

            ProductionRecipeWorkRateMaximumSnapshot automaticSnapshot = manual
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    AutomaticLanes(),
                    recipe)
                .Snapshot;
            Require(automaticSnapshot.AutomaticMilliWuPerSecond == 1_250L
                    && automatic.CallCount == 1,
                "A mode-exclusive automatic lane did not use the injected query.");

            ProductionRecipeWorkRateMaximumAuthority mismatched = Authority(
                new[] { FactorA },
                new[] { Contributor(FactorA, 1m) },
                new CountingAutomaticRateQuery(
                    ProductionWorkRateMaximumContributorResult.Missing(
                        ProductionRecipeWorkRateMaximumGapReason
                            .AutomaticLaneMismatch,
                        "The authored automation ability disagrees with the lane.",
                        Digest("automatic:mismatch"))));
            ProductionRecipeWorkRateMaximumAuthorityResult mismatch = mismatched
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    AutomaticLanes(),
                    recipe);
            Require(!mismatch.HasSnapshot
                    && mismatch.MissingReason
                    == ProductionRecipeWorkRateMaximumGapReason
                        .AutomaticLaneMismatch,
                "An automatic lane mismatch was not retained as a typed gap.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyNonFiniteAndOverflowRejected()
    {
        Require(!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                    double.NaN,
                    out _,
                    out ProductionRecipeWorkRateMaximumGapReason nonFinite)
                && nonFinite == ProductionRecipeWorkRateMaximumGapReason
                    .NonFiniteOrNonPositiveUpperBound,
            "NaN was accepted as a work-rate upper bound.");
        Require(!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                    double.PositiveInfinity,
                    out _,
                    out nonFinite)
                && nonFinite == ProductionRecipeWorkRateMaximumGapReason
                    .NonFiniteOrNonPositiveUpperBound,
            "Infinity was accepted as a work-rate upper bound.");
        Require(!ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                    10_000_000_000d,
                    out _,
                    out ProductionRecipeWorkRateMaximumGapReason overflow)
                && overflow == ProductionRecipeWorkRateMaximumGapReason
                    .FixedPointOverflow,
            "An upper bound outside the fixed-point range was accepted.");

        ProductionRecipeSO recipe = Recipe("recipe:qa-work-rate-rejection");
        try
        {
            ProductionRecipeWorkRateMaximumAuthority rejected = Authority(
                new[] { FactorA },
                new[]
                {
                    new DelegateContributor(
                        FactorA,
                        _ => ProductionWorkRateMaximumContributorResult.Missing(
                            ProductionRecipeWorkRateMaximumGapReason
                                .NonFiniteOrNonPositiveUpperBound,
                            "The contributor maximum is non-finite.",
                            Digest("contributor:non-finite")))
                });
            ProductionRecipeWorkRateMaximumAuthorityResult result = rejected
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    ManualLanes(),
                    recipe);
            Require(!result.HasSnapshot
                    && result.MissingReason
                    == ProductionRecipeWorkRateMaximumGapReason
                        .NonFiniteOrNonPositiveUpperBound,
                "A contributor's non-finite typed rejection was discarded.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static ProductionRecipeWorkRateMaximumAuthority Authority(
        IReadOnlyList<string> manifestIds,
        IReadOnlyList<IProductionRecipeWorkRateMaximumContributor> contributors,
        IProductionAutomaticWorkRateMaximumQuery automatic = null) => new(
        new ProductionWorkRateContributorManifest(manifestIds),
        contributors,
        automatic);

    private static IProductionRecipeWorkRateMaximumContributor Contributor(
        string contributorId,
        decimal value) => new DelegateContributor(
        contributorId,
        _ => Complete(value, contributorId));

    private static ProductionWorkRateMaximumContributorResult Complete(
        decimal value,
        string provenance)
    {
        Require(ProductionWorkRateFixedPointUpperBound.TryFromDecimalUpperBound(
                value,
                out ProductionWorkRateFixedPointUpperBound upperBound,
                out ProductionRecipeWorkRateMaximumGapReason reason),
            "The QA upper bound is invalid: " + reason);
        return ProductionWorkRateMaximumContributorResult.Complete(
            upperBound,
            Digest(provenance));
    }

    private static ProductionFacilityWorkstationLaneCapacityProfile ManualLanes() =>
        new(
            ProductionWorkstationLanePolicy
                .ManualWithDetachedBatchProcessors,
            1,
            0);

    private static ProductionFacilityWorkstationLaneCapacityProfile AutomaticLanes() =>
        new(
            ProductionWorkstationLanePolicy
                .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors,
            1,
            1);

    private static ProductionRecipeSO Recipe(string recipeId)
    {
        ProductionRecipeSO recipe = ScriptableObject
            .CreateInstance<ProductionRecipeSO>();
        recipe.Configure(
            recipeId,
            recipeId,
            string.Empty,
            "qa-work-rate",
            "work:craft",
            string.Empty,
            1f,
            Array.Empty<ItemAmountDefinition>(),
            new[]
            {
                new ProductionOutputDefinition(
                    "output:qa-main",
                    ProductionOutputRole.Main,
                    "resource:qa-output",
                    1)
            });
        recipe.ConfigureWorkshop(
            WorkstationTag,
            Array.Empty<string>(),
            ProductionProcessKind.WorkOnly);
        recipe.ConfigureProficiency(BuiltInCharacterProficiencyIds.Crafting);
        recipe.ConfigureProcessClass(ProductionProcessClass.CookingSimpleMixing);
        return recipe;
    }

    private static string Digest(string value)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-work-rate-authority-qa@1");
        digest.Append(value);
        return digest.ComputeSha256();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class DelegateContributor :
        IProductionRecipeWorkRateMaximumContributor
    {
        private readonly Func<
            ProductionWorkRateMaximumSubject,
            ProductionWorkRateMaximumContributorResult> capture;

        internal DelegateContributor(
            string contributorId,
            Func<ProductionWorkRateMaximumSubject,
                ProductionWorkRateMaximumContributorResult> capture)
        {
            ContributorId = contributorId;
            this.capture = capture;
        }

        public string ContributorId { get; }

        public ProductionWorkRateMaximumContributorResult Capture(
            ProductionWorkRateMaximumSubject context) => capture(context);
    }

    private sealed class CountingAutomaticRateQuery :
        IProductionAutomaticWorkRateMaximumQuery
    {
        private readonly ProductionWorkRateMaximumContributorResult result;

        internal CountingAutomaticRateQuery(
            ProductionWorkRateMaximumContributorResult result)
        {
            this.result = result;
        }

        internal int CallCount { get; private set; }

        public ProductionWorkRateMaximumContributorResult Capture(
            ProductionWorkRateMaximumSubject context)
        {
            CallCount++;
            return result;
        }
    }
}
#endif
