#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionAuthoredThroughputEnvelopeDebugScenarios
{
    private const string WorkstationTag = "workstation:qa-throughput";

    [MenuItem("DungeonStory/V27/Production/Validate Authored Throughput Envelopes")]
    public static void Validate()
    {
        VerifyCalendarTimeScaleAuthority();
        VerifyAssignmentLocalBranchJoin();
        VerifyModeExclusiveMaximum();
        VerifyPassiveAssignmentBottleneck();
        VerifySpecialGapWithholdsEnvelope();
        VerifyShuffleDeterminism();
        VerifyInvalidAndOverflowFailLoud();
        Debug.Log(
            "[ProductionAuthoredThroughputEnvelope] focused scenarios passed.");
    }

    private static void VerifyCalendarTimeScaleAuthority()
    {
        ProductionThroughputTimeScaleSnapshot first =
            ProductionThroughputTimeScaleAuthority.Capture();
        ProductionThroughputTimeScaleSnapshot second =
            ProductionThroughputTimeScaleAuthority.Capture();
        Require(first.RealTimeMicrosecondsPerGameHour == 7_500_000L
                && first.RealTimeSecondsPerGameHour == 7.5m
                && string.Equals(first.SourceDigest, second.SourceDigest,
                    StringComparison.Ordinal),
            "The shared game-calendar throughput time scale is not exact and deterministic.");
    }

    private static void VerifyAssignmentLocalBranchJoin()
    {
        BuildingSO slowHeavy = Support(
            "support:qa-slow-heavy",
            "support:qa-branch",
            ProductionSupportKind.Passive,
            batchCapacity: 1,
            workSpeedMultiplier: 1f);
        BuildingSO fastLight = Support(
            "support:qa-fast-light",
            "support:qa-branch",
            ProductionSupportKind.Passive,
            batchCapacity: 1,
            workSpeedMultiplier: 10f);
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-assignment-local",
            ProductionProcessKind.WorkOnly,
            new[] { "support:qa-branch" });
        try
        {
            ProductionMaximumOutputFactorCatalog factors = new(
                new[] { fastLight, slowHeavy });
            ProductionRecipeThroughputCycleProjector projector = Projector(
                factors,
                new DelegateBranchQuery((candidate, assignment) =>
                {
                    string supportId = assignment.Supports.Single().SupportId;
                    long mass = string.Equals(
                            supportId,
                            "support:qa-slow-heavy",
                            StringComparison.Ordinal)
                        ? 100L
                        : 10L;
                    return CompleteBranch(candidate, assignment, mass);
                }),
                WorkRates(manual: 1_000L));

            ProductionRecipeThroughputProjectionResult result = projector
                .Capture(Subject("building:qa-assignment-local", recipe));
            Require(result.Gaps.Count == 0 && result.Candidates.Count == 2,
                "The exact-assignment branch fixture did not yield two candidates.");
            Require(result.Candidates.All(value =>
                    value.PeakOutputMassGramsPerHour == 100L),
                "Output mass from one assignment was cross-multiplied by another assignment's work speed.");
            Require(result.Candidates.All(value =>
                    string.Equals(
                        value.SupportAssignmentSourceDigest,
                        factors.CaptureFeasibleAssignments(recipe)
                            .Single(assignment => assignment.Supports.Any(
                                support => string.Equals(
                                    support.SupportId,
                                    value.MaximumOutputMassGrams == 100L
                                        ? "support:qa-slow-heavy"
                                        : "support:qa-fast-light",
                                    StringComparison.Ordinal)))
                            .SourceDigest,
                        StringComparison.Ordinal)),
                "Candidate provenance did not retain its exact support assignment.");
        }
        finally
        {
            Destroy(recipe, slowHeavy, fastLight);
        }
    }

    private static void VerifyModeExclusiveMaximum()
    {
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-mode-exclusive",
            ProductionProcessKind.WorkOnly);
        try
        {
            ProductionMaximumOutputFactorCatalog factors = new(
                Array.Empty<BuildingSO>());
            ProductionFacilityWorkstationLaneCapacityProfile lanes = new(
                ProductionWorkstationLanePolicy
                    .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors,
                manualWorkLaneCount: 2,
                automaticWorkLaneCount: 1);
            ProductionRecipeThroughputCycleProjector projector = Projector(
                factors,
                new DelegateBranchQuery(CompleteBranch100),
                WorkRates(manual: 1_000L, automatic: 3_000L));
            ProductionRecipeThroughputCycleCandidateSnapshot candidate =
                projector.Capture(Subject(
                        "building:qa-mode-exclusive",
                        recipe,
                        lanes))
                    .Candidates.Single();

            Require(candidate.ExecutionPath
                    == ProductionThroughputExecutionPath.Automatic
                && candidate.PeakOutputMassGramsPerHour == 300L,
                "Mode-exclusive manual/automatic throughput was summed instead of selecting the maximum lane mode.");
        }
        finally
        {
            Destroy(recipe);
        }
    }

    private static void VerifyPassiveAssignmentBottleneck()
    {
        BuildingSO twoLane = Support(
            "support:qa-batch-two",
            "support:qa-batch",
            ProductionSupportKind.BatchProcessor,
            batchCapacity: 2,
            workSpeedMultiplier: 1f);
        BuildingSO threeLane = Support(
            "support:qa-batch-three",
            "support:qa-batch",
            ProductionSupportKind.BatchProcessor,
            batchCapacity: 3,
            workSpeedMultiplier: 1f);
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-passive-bottleneck",
            ProductionProcessKind.PassiveBatch,
            new[] { "support:qa-batch" },
            "support:qa-batch",
            processingGameHours: 1f);
        try
        {
            ProductionMaximumOutputFactorCatalog factors = new(
                new[] { threeLane, twoLane });
            ProductionRecipeThroughputCycleProjector projector = Projector(
                factors,
                new DelegateBranchQuery(CompleteBranch100),
                WorkRates(manual: 10_000L));
            ProductionRecipeThroughputProjectionResult result = projector
                .Capture(Subject("building:qa-passive", recipe));

            Require(result.Gaps.Count == 0 && result.Candidates.Count == 2,
                "Passive assignment fixture did not yield two candidates.");
            Require(result.Candidates.All(value => value.Bottleneck
                    == ProductionThroughputBottleneck.DetachedBatchProcessor),
                "Passive throughput did not retain the detached processor bottleneck.");
            long[] peaks = result.Candidates
                .Select(value => value.PeakOutputMassGramsPerHour)
                .OrderBy(value => value)
                .ToArray();
            Require(peaks.SequenceEqual(new[] { 200L, 300L }),
                "Batch capacities from mutually exclusive support assignments were summed together.");
        }
        finally
        {
            Destroy(recipe, twoLane, threeLane);
        }
    }

    private static void VerifySpecialGapWithholdsEnvelope()
    {
        ProductionRecipeThroughputCycleProjector projector = EmptyProjector();
        ProductionSpecialThroughputCandidateSnapshot candidate = new(
            "building:qa-special-gap",
            WorkstationTag,
            "producer:qa-complete",
            "branch:complete",
            500L,
            Digest("special-complete"));
        ProductionThroughputCoverageGap gap = new(
            "building:qa-special-gap",
            WorkstationTag,
            ProductionThroughputProducerKind.CapacityContributor,
            "producer:qa-missing",
            "branch:missing",
            ProductionThroughputGapReason.AuthoredCycleAuthorityMissing,
            "missing authored cycle",
            Digest("special-gap"));
        ProductionAuthoredThroughputCoverageSnapshot result =
            new ProductionAuthoredThroughputEnvelopeAuthority(projector)
                .Capture(new[]
                {
                    Subject(
                        "building:qa-special-gap",
                        Array.Empty<ProductionRecipeSO>(),
                        specialCandidates: new[] { candidate },
                        specialGaps: new[] { gap })
                });

        Require(result.CompleteEnvelopes.Count == 0
            && result.Gaps.Count == 1
            && !result.IsComplete,
            "A facility with a typed special-producer gap published a partial envelope.");
        Expect<InvalidOperationException>(result.RequireComplete);
    }

    private static void VerifyShuffleDeterminism()
    {
        ProductionRecipeThroughputCycleProjector projector = EmptyProjector();
        ProductionAuthoredThroughputFacilitySubject first = Subject(
            "building:qa-shuffle-a",
            Array.Empty<ProductionRecipeSO>(),
            specialCandidates: new[]
            {
                Special("building:qa-shuffle-a", "producer:qa-a", 700L)
            });
        ProductionAuthoredThroughputFacilitySubject second = Subject(
            "building:qa-shuffle-b",
            Array.Empty<ProductionRecipeSO>(),
            specialCandidates: new[]
            {
                Special("building:qa-shuffle-b", "producer:qa-b", 900L)
            });
        ProductionAuthoredThroughputEnvelopeAuthority authority = new(projector);
        ProductionAuthoredThroughputCoverageSnapshot ordered = authority.Capture(
            new[] { first, second });
        ProductionAuthoredThroughputCoverageSnapshot shuffled = authority.Capture(
            new[] { second, first });

        Require(string.Equals(
                ordered.SourceDigest,
                shuffled.SourceDigest,
                StringComparison.Ordinal)
            && ordered.CompleteEnvelopes.Select(EnvelopeToken).SequenceEqual(
                shuffled.CompleteEnvelopes.Select(EnvelopeToken),
                StringComparer.Ordinal),
            "Facility input order changed the authored throughput coverage digest.");
    }

    private static void VerifyInvalidAndOverflowFailLoud()
    {
        Expect<ArgumentOutOfRangeException>(() =>
            _ = new ProductionThroughputTimeScaleSnapshot(
                0L,
                Digest("invalid-time")));

        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-overflow",
            ProductionProcessKind.WorkOnly);
        try
        {
            ProductionMaximumOutputFactorCatalog factors = new(
                Array.Empty<BuildingSO>());
            ProductionRecipeThroughputCycleProjector overflow = Projector(
                factors,
                new DelegateBranchQuery((candidate, assignment) =>
                    CompleteBranch(candidate, assignment, long.MaxValue)),
                WorkRates(manual: 2_000L));
            Expect<OverflowException>(() => overflow.Capture(
                Subject("building:qa-overflow", recipe)));

            ProductionRecipeThroughputCycleProjector drift = Projector(
                factors,
                new DelegateBranchQuery((candidate, assignment) =>
                    ProductionRecipeThroughputBranchQueryResult.Complete(
                        new[]
                        {
                            new ProductionRecipeThroughputBranchSnapshot(
                                candidate.RecipeId,
                                "branch:qa-drift",
                                Digest("wrong-assignment"),
                                100L,
                                new[] { ProductionOutputCapabilityIds.StandardDefinition },
                                Digest("drift-branch"))
                        },
                        Digest("drift-result"))),
                WorkRates(manual: 1_000L));
            Expect<InvalidOperationException>(() => drift.Capture(
                Subject("building:qa-drift", recipe)));
        }
        finally
        {
            Destroy(recipe);
        }
    }

    private static ProductionRecipeThroughputCycleProjector EmptyProjector() =>
        Projector(
            new ProductionMaximumOutputFactorCatalog(Array.Empty<BuildingSO>()),
            new DelegateBranchQuery((recipe, assignment) =>
                throw new InvalidOperationException(
                    "A special-only fixture queried a recipe branch.")),
            new DelegateWorkRateQuery((definitionId, recipe) =>
                throw new InvalidOperationException(
                    "A special-only fixture queried a work rate.")));

    private static ProductionRecipeThroughputCycleProjector Projector(
        IProductionMaximumOutputFactorCatalog factors,
        IProductionRecipeThroughputBranchQuery branches,
        IProductionRecipeWorkRateMaximumQuery rates) => new(
        factors,
        branches,
        rates,
        new ProductionThroughputTimeScaleSnapshot(
            1_000_000L,
            Digest("one-real-second-per-game-hour")));

    private static DelegateWorkRateQuery WorkRates(
        long manual,
        long automatic = 0L) => new((definitionId, recipe) =>
        ProductionRecipeWorkRateMaximumQueryResult.Complete(
            new ProductionRecipeWorkRateMaximumSnapshot(
                manual,
                automatic,
                Digest(definitionId + ":" + recipe.RecipeId + ":rate"))));

    private static ProductionRecipeThroughputBranchQueryResult
        CompleteBranch100(
            ProductionRecipeSO recipe,
            ProductionAuthoredSupportAssignmentSnapshot assignment) =>
        CompleteBranch(recipe, assignment, 100L);

    private static ProductionRecipeThroughputBranchQueryResult CompleteBranch(
        ProductionRecipeSO recipe,
        ProductionAuthoredSupportAssignmentSnapshot assignment,
        long mass)
    {
        string token = recipe.RecipeId + ":" + assignment.SourceDigest + ":"
            + mass;
        return ProductionRecipeThroughputBranchQueryResult.Complete(
            new[]
            {
                new ProductionRecipeThroughputBranchSnapshot(
                    recipe.RecipeId,
                    "branch:qa",
                    assignment.SourceDigest,
                    mass,
                    new[] { ProductionOutputCapabilityIds.StandardDefinition },
                    Digest(token + ":branch"))
            },
            Digest(token + ":result"));
    }

    private static ProductionAuthoredThroughputFacilitySubject Subject(
        string definitionId,
        ProductionRecipeSO recipe,
        ProductionFacilityWorkstationLaneCapacityProfile lanes = null) =>
        Subject(
            definitionId,
            new[] { recipe },
            lanes);

    private static ProductionAuthoredThroughputFacilitySubject Subject(
        string definitionId,
        IReadOnlyList<ProductionRecipeSO> recipes,
        ProductionFacilityWorkstationLaneCapacityProfile lanes = null,
        IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot>
            specialCandidates = null,
        IReadOnlyList<ProductionThroughputCoverageGap> specialGaps = null) => new(
        definitionId,
        WorkstationTag,
        lanes ?? ProductionFacilityWorkstationLaneCapacityProfile
            .SingleManualWithDetachedBatchProcessors,
        ProductionFacilityProcessFluidCapacityProfile.Empty,
        recipes,
        specialCandidates,
        specialGaps);

    private static ProductionSpecialThroughputCandidateSnapshot Special(
        string definitionId,
        string producerId,
        long peak) => new(
        definitionId,
        WorkstationTag,
        producerId,
        "branch:qa",
        peak,
        Digest(definitionId + ":" + producerId + ":" + peak));

    private static ProductionRecipeSO Recipe(
        string recipeId,
        ProductionProcessKind kind,
        IReadOnlyList<string> supportTags = null,
        string batchSupportTag = "",
        float processingGameHours = 0f)
    {
        ProductionRecipeSO recipe = ScriptableObject
            .CreateInstance<ProductionRecipeSO>();
        recipe.Configure(
            recipeId,
            recipeId,
            string.Empty,
            "qa-throughput",
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
            supportTags ?? Array.Empty<string>(),
            kind,
            batchSupportTag,
            processGameHours: processingGameHours,
            failedBatchItemId: kind == ProductionProcessKind.PassiveBatch
                ? "resource:qa-spoilage"
                : string.Empty);
        recipe.ConfigureProficiency(
            BuiltInCharacterProficiencyIds.Crafting);
        recipe.ConfigureProcessClass(
            ProductionProcessClass.CookingSimpleMixing);
        return recipe;
    }

    private static BuildingSO Support(
        string supportId,
        string featureTag,
        ProductionSupportKind kind,
        int batchCapacity,
        float workSpeedMultiplier)
    {
        BuildingSO support = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingProductionSupportAbility
        {
            supportId = supportId,
            featureTags = new[] { featureTag },
            compatibleWorkstationTags = new[] { WorkstationTag },
            kind = kind,
            batchCapacity = batchCapacity,
            maximumLinkedInstancesPerWorkstation = 1,
            workSpeedMultiplier = workSpeedMultiplier,
            outputMultiplier = 1f
        });
        support.ReplaceAbilities(abilities);
        return support;
    }

    private static string EnvelopeToken(
        ProductionOutputThroughputEnvelopeSnapshot envelope) =>
        envelope.DefinitionId + "\n" + envelope.WorkstationTag + "\n"
        + envelope.PeakOutputMassGramsPerHour + "\n" + envelope.SourceDigest;

    private static string Digest(string token)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(
            "production-authored-throughput-envelope-debug-scenario@1");
        digest.Append(token);
        return digest.ComputeSha256();
    }

    private static void Destroy(params UnityEngine.Object[] values)
    {
        foreach (UnityEngine.Object value in values)
        {
            if (value != null)
                UnityEngine.Object.DestroyImmediate(value);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected exception was not thrown: " + typeof(T).Name);
    }

    private sealed class DelegateBranchQuery :
        IProductionRecipeThroughputBranchQuery
    {
        private readonly Func<
            ProductionRecipeSO,
            ProductionAuthoredSupportAssignmentSnapshot,
            ProductionRecipeThroughputBranchQueryResult> capture;

        internal DelegateBranchQuery(Func<
            ProductionRecipeSO,
            ProductionAuthoredSupportAssignmentSnapshot,
            ProductionRecipeThroughputBranchQueryResult> capture)
        {
            this.capture = capture ?? throw new ArgumentNullException(
                nameof(capture));
        }

        public ProductionRecipeThroughputBranchQueryResult Capture(
            ProductionRecipeSO recipe,
            ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
            ProductionAuthoredSupportAssignmentSnapshot supportAssignment) =>
            capture(recipe, supportAssignment);
    }

    private sealed class DelegateWorkRateQuery :
        IProductionRecipeWorkRateMaximumQuery
    {
        private readonly Func<
            string,
            ProductionRecipeSO,
            ProductionRecipeWorkRateMaximumQueryResult> capture;

        internal DelegateWorkRateQuery(Func<
            string,
            ProductionRecipeSO,
            ProductionRecipeWorkRateMaximumQueryResult> capture)
        {
            this.capture = capture ?? throw new ArgumentNullException(
                nameof(capture));
        }

        public ProductionRecipeWorkRateMaximumQueryResult Capture(
            string facilityDefinitionId,
            string workstationTag,
            ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
            ProductionRecipeSO recipe) => capture(facilityDefinitionId, recipe);
    }
}
#endif
