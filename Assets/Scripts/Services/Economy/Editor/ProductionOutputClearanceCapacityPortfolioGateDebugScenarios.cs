#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputClearanceCapacityPortfolioGateDebugScenarios
{
    private static readonly int[] Seeds = Enumerable.Range(157181, 32).ToArray();
    private static readonly ProductionFacilityWorkstationLaneCapacityProfile Lane =
        new(
            ProductionWorkstationLanePolicy
                .ManualWithDetachedBatchProcessors,
            1,
            0);

    [MenuItem(
        "DungeonStory/V27/Production/Run Output Clearance Capacity Portfolio Focused Scenarios")]
    public static void RunFromMenu()
    {
        string result = RunAll();
        Debug.Log(result);
    }

    public static string RunAll()
    {
        ProductionOutputClearanceCapacityReviewInput[] inputs =
        {
            Input("building:qa-clearance-four", "qa-clearance-four", 4,
                1_000L, 1_000L, Hash('a')),
            Input("building:qa-clearance-over-four", "qa-clearance-over-four", 4,
                1_000L, 1_000L, Hash('b')),
            Input("building:qa-clearance-undersized", "qa-clearance-undersized", 2,
                1_000L, 1_000L, Hash('c')),
            Input("building:qa-clearance-over-four-undersized",
                "qa-clearance-over-four-undersized", 2,
                1_000L, 1_000L, Hash('d'))
        };
        int[] authoredBefore = inputs.Select(value => value.AuthoredWholeCycles)
            .ToArray();
        IReadOnlyList<ProductionOutputClearanceProfileRecord> profiles =
            Profiles(
                inputs,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [inputs[0].DefinitionId] = 4_000_000L,
                    [inputs[1].DefinitionId] = 4_001_000L,
                    [inputs[2].DefinitionId] = 2_001_000L,
                    [inputs[3].DefinitionId] = 4_001_000L
                });

        ProductionOutputClearanceCapacityReviewPortfolio first =
            ProductionOutputClearanceCapacityReviewPortfolio.Build(
                inputs,
                profiles);
        ProductionOutputClearanceCapacityReviewPortfolio shuffled =
            ProductionOutputClearanceCapacityReviewPortfolio.Build(
                inputs.Reverse().ToArray(),
                profiles.Reverse().ToArray());
        ProductionOutputClearanceCapacityReviewRow exactFour = first.Rows.Single(
            value => value.Input.DefinitionId == inputs[0].DefinitionId);
        ProductionOutputClearanceCapacityReviewRow overFour = first.Rows.Single(
            value => value.Input.DefinitionId == inputs[1].DefinitionId);
        ProductionOutputClearanceCapacityReviewRow undersized = first.Rows.Single(
            value => value.Input.DefinitionId == inputs[2].DefinitionId);
        ProductionOutputClearanceCapacityReviewRow pressureUndersized =
            first.Rows.Single(
                value => value.Input.DefinitionId == inputs[3].DefinitionId);

        Require(exactFour.Assessment.IsAccepted
                && exactFour.Assessment.Requirement.RequiredWholeCycles == 4L
                && exactFour.Assessment.AuthoredWholeCycles == 4,
            "An exact four-cycle p95 requirement must pass without changing authored capacity.");
        Require(!overFour.Assessment.IsAccepted
                && overFour.Assessment.CanPublishBoundedCapacity
                && overFour.Assessment.RequiresBackpressure
                && !overFour.Assessment.IsBlockingCritical
                && overFour.Assessment.Requirement.RequiredWholeCycles == 5L
                && overFour.Assessment.Requirement.PublishedWholeCycles == 4L
                && string.IsNullOrEmpty(overFour.Assessment.FailureCode)
                && string.Equals(
                    overFour.Assessment.DiagnosticCode,
                    ProductionOutputClearanceRequirementProjector
                        .BackpressureExpectedDiagnosticCode,
                    StringComparison.Ordinal),
            "A 4.001-cycle requirement must publish an explicit bounded backpressure result.");
        Require(!undersized.Assessment.IsAccepted
                && undersized.Assessment.Requirement.RequiredWholeCycles == 3L
                && undersized.Assessment.AuthoredWholeCycles == 2
                && string.Equals(
                    undersized.Assessment.FailureCode,
                    ProductionOutputClearanceCapacityGate
                        .AuthoredCapacityUndersizedFailureCode,
                    StringComparison.Ordinal),
            "An authored two-cycle buffer must report an exact undersized Critical when p95 requires three cycles.");
        Require(pressureUndersized.Assessment.IsBlockingCritical
                && !pressureUndersized.Assessment.CanPublishBoundedCapacity
                && pressureUndersized.Assessment.Requirement.RequiresBackpressure
                && pressureUndersized.Assessment.Requirement.RequiredWholeCycles == 5L
                && pressureUndersized.Assessment.Requirement.PublishedWholeCycles == 4L
                && string.Equals(
                    pressureUndersized.Assessment.FailureCode,
                    ProductionOutputClearanceCapacityGate
                        .AuthoredCapacityUndersizedFailureCode,
                    StringComparison.Ordinal)
                && string.IsNullOrEmpty(
                    pressureUndersized.Assessment.DiagnosticCode),
            "Backpressure must not hide authored capacity below the bounded four-cycle target.");
        Require(first.AcceptedCount == 1
                && first.BackpressureExpectedCount == 1
                && first.BlockingCriticalCount == 2
                && string.Equals(
                    first.SourceDigest,
                    shuffled.SourceDigest,
                    StringComparison.Ordinal)
                && first.Rows.Select(value => value.SourceDigest).SequenceEqual(
                    shuffled.Rows.Select(value => value.SourceDigest),
                    StringComparer.Ordinal),
            "Capacity review must be deterministic under input/profile shuffling.");
        Require(inputs.Select(value => value.AuthoredWholeCycles)
                .SequenceEqual(authoredBefore),
            "Capacity review must never mutate authored cycle capacity.");
        VerifyPromotionPreflight();

        ProductionOutputClearanceCapacityReviewInput supportChanged = Input(
            inputs[0].DefinitionId,
            inputs[0].WorkstationTag,
            inputs[0].AuthoredWholeCycles,
            inputs[0].MaximumCycleCompletionFootprintGrams,
            inputs[0].ThroughputEnvelope.PeakOutputMassGramsPerHour,
            Hash('d'));
        RequireRejects(
            () => ProductionOutputClearanceCapacityReviewPortfolio.Build(
                new[] { supportChanged, inputs[1], inputs[2], inputs[3] },
                profiles),
            "maximum reachable support/work-speed envelope");
        RequireRejects(
            () => ProductionOutputClearanceCapacityReviewPortfolio.Build(
                inputs.Take(3).ToArray(),
                profiles),
            "cardinality");
        RequireRejects(
            () => ProductionOutputClearanceCapacityReviewPortfolio.Build(
                inputs,
                profiles.Take(3).Concat(new[] { profiles[0] }).ToArray()),
            "duplicated");

        return "OUTPUT_CLEARANCE_CAPACITY_PORTFOLIO_GATE_PASS "
            + "rows=" + first.Rows.Count
            + ";accepted=" + first.AcceptedCount
            + ";backpressureExpected=" + first.BackpressureExpectedCount
            + ";blockingCritical=" + first.BlockingCriticalCount
            + ";exactFour=true;overFourBackpressure=true;"
            + "undersizedTyped=true;pressureUndersizedTyped=true;"
            + "promotionPreflight=true;"
            + "supportDigestDriftRejected=true;shuffleIdentity=true;"
            + "authoredMutation=0;sourceDigest=" + first.SourceDigest;
    }

    private static ProductionOutputClearanceCapacityReviewInput Input(
        string definitionId,
        string workstationTag,
        int authoredWholeCycles,
        long maximumCycleMassGrams,
        long peakGramsPerHour,
        string throughputDigest) => new(
        definitionId,
        workstationTag,
        authoredWholeCycles,
        maximumCycleMassGrams,
        Lane,
        new ProductionOutputThroughputEnvelopeSnapshot(
            definitionId,
            workstationTag,
            peakGramsPerHour,
            throughputDigest),
        Hash('e'));

    private static void VerifyPromotionPreflight()
    {
        string source = Hash('1');
        string scene = Hash('2');
        string candidate = Hash('3');
        PromotionFixture none = BuildPromotionFixture(
            0, source, scene, candidate);
        PromotionFixture one = BuildPromotionFixture(
            1, source, scene, candidate);
        PromotionFixture all = BuildPromotionFixture(
            ProductionOutputClearanceProfileResourceSource.ExpectedProfileCount,
            source,
            scene,
            candidate);
        ValidatePromotionFixture(none, source, scene, candidate);
        ValidatePromotionFixture(one, source, scene, candidate);
        ValidatePromotionFixture(all, source, scene, candidate);

        string report = one.Report;
        ProductionOutputClearanceFrozenProfilePipeline
            .ValidateGenerationReportForPromotion(
                report,
                source,
                scene,
                candidate,
                one.Catalog.AuthorityDigest,
                one.Catalog.Records);
        RequireRejects(
            () => ProductionOutputClearanceFrozenProfilePipeline
                .ValidateGenerationReportForPromotion(
                    report,
                    source,
                    scene,
                    Hash('4'),
                    one.Catalog.AuthorityDigest,
                    one.Catalog.Records),
            "candidateSha256");
        RequireRejects(
            () => ProductionOutputClearanceFrozenProfilePipeline
                .ValidateGenerationReportForPromotion(
                    report.Replace(
                        "blockingCritical=0",
                        "blockingCritical=1"),
                    source,
                    scene,
                    candidate,
                    one.Catalog.AuthorityDigest,
                    one.Catalog.Records),
            "blockingCritical");
        RequireRejects(
            () => ProductionOutputClearanceFrozenProfilePipeline
                .ValidateGenerationReportForPromotion(
                    report.Replace("accepted=91", "accepted=091"),
                    source,
                    scene,
                    candidate,
                    one.Catalog.AuthorityDigest,
                    one.Catalog.Records),
            "DISPOSITION_DENOMINATOR");
        RequireRejects(
            () => ProductionOutputClearanceFrozenProfilePipeline
                .ValidateGenerationReportForPromotion(
                    report.Replace(
                        "capacityReviewDigest=" + one.Review.SourceDigest,
                        "capacityReviewDigest=" + Hash('9')),
                    source,
                    scene,
                    candidate,
                    one.Catalog.AuthorityDigest,
                    one.Catalog.Records),
            "REVIEW_DRIFT");
        RequireRejects(
            () => ProductionOutputClearanceFrozenProfilePipeline
                .ValidateGenerationReportForPromotion(
                    report.Replace(";authoredCycles:4;lanePolicy:1",
                        ";authoredCycles:04;lanePolicy:1"),
                    source,
                    scene,
                    candidate,
                    one.Catalog.AuthorityDigest,
                    one.Catalog.Records),
            "REVIEW_INPUT_VALUE");
        string omittedPressure = report.Substring(
            0,
            report.IndexOf("backpressure[0]=", StringComparison.Ordinal))
            .Replace("accepted=91", "accepted=92")
            .Replace("backpressureExpected=1", "backpressureExpected=0");
        RequireRejects(
            () => ProductionOutputClearanceFrozenProfilePipeline
                .ValidateGenerationReportForPromotion(
                    omittedPressure,
                    source,
                    scene,
                    candidate,
                    one.Catalog.AuthorityDigest,
                    one.Catalog.Records),
            "REVIEW_DRIFT");
        RequireRejects(
            () => ProductionOutputClearanceFrozenProfilePipeline
                .ValidateGenerationReportForPromotion(
                    report.Replace("profiles=92\n", "profiles=92\r\n"),
                    source,
                    scene,
                    candidate,
                    one.Catalog.AuthorityDigest,
                    one.Catalog.Records),
            "ENCODING");
    }

    private static void ValidatePromotionFixture(
        PromotionFixture fixture,
        string source,
        string scene,
        string candidate) =>
        ProductionOutputClearanceFrozenProfilePipeline
            .ValidateGenerationReportForPromotion(
                fixture.Report,
                source,
                scene,
                candidate,
                fixture.Catalog.AuthorityDigest,
                fixture.Catalog.Records);

    private static PromotionFixture BuildPromotionFixture(
        int backpressureCount,
        string source,
        string scene,
        string candidate)
    {
        int expected = ProductionOutputClearanceProfileResourceSource
            .ExpectedProfileCount;
        ProductionOutputClearanceCapacityReviewInput[] inputs =
            new ProductionOutputClearanceCapacityReviewInput[expected];
        Dictionary<string, long> clearance = new(StringComparer.Ordinal);
        for (int index = 0; index < expected; index++)
        {
            string definition = "definition:promotion:"
                + index.ToString("D3", CultureInfo.InvariantCulture);
            inputs[index] = Input(
                definition,
                "workstation:promotion",
                4,
                1_000L,
                1_000L,
                Hash('a'));
            clearance.Add(
                definition,
                index < backpressureCount ? 4_001_000L : 4_000_000L);
        }
        IReadOnlyList<ProductionOutputClearanceProfileRecord> profiles =
            Profiles(inputs, clearance);
        ProductionOutputClearanceCapacityReviewPortfolio review =
            ProductionOutputClearanceCapacityReviewPortfolio.Build(
                inputs,
                profiles);
        ProductionOutputClearanceProfileCatalog catalog = new(profiles);
        StringBuilder report = new();
        report.Append("schema=v27-production-output-clearance-profile-generation@3\n")
            .Append("result=PASS\n")
            .Append("currentSourceDigest=").Append(source).Append('\n')
            .Append("gameplaySceneSha256=").Append(scene).Append('\n')
            .Append("naturalAcceptedDigest=").Append(Hash('4')).Append('\n')
            .Append("throughputAuthorityDigest=").Append(Hash('5')).Append('\n')
            .Append("capacityReviewDigest=").Append(review.SourceDigest).Append('\n')
            .Append("catalogAuthorityDigest=").Append(catalog.AuthorityDigest)
                .Append('\n')
            .Append("profiles=").Append(expected).Append('\n')
            .Append("seedsPerProfile=32\n")
            .Append("observations=2944\n")
            .Append("accepted=").Append(review.AcceptedCount).Append('\n')
            .Append("backpressureExpected=")
                .Append(review.BackpressureExpectedCount).Append('\n')
            .Append("blockingCritical=0\n")
            .Append("candidateSha256=").Append(candidate).Append('\n')
            .Append("secondWriteByteDiff=0\n")
            .Append(ProductionOutputClearanceFrozenProfilePipeline
                .BuildReviewInputReportLines(review))
            .Append(ProductionOutputClearanceFrozenProfilePipeline
                .BuildBackpressureReportLines(review));
        return new PromotionFixture(report.ToString(), review, catalog);
    }

    private sealed class PromotionFixture
    {
        public PromotionFixture(
            string report,
            ProductionOutputClearanceCapacityReviewPortfolio review,
            ProductionOutputClearanceProfileCatalog catalog)
        {
            Report = report;
            Review = review;
            Catalog = catalog;
        }

        public string Report { get; }
        public ProductionOutputClearanceCapacityReviewPortfolio Review { get; }
        public ProductionOutputClearanceProfileCatalog Catalog { get; }
    }

    private static IReadOnlyList<ProductionOutputClearanceProfileRecord> Profiles(
        IReadOnlyList<ProductionOutputClearanceCapacityReviewInput> inputs,
        IReadOnlyDictionary<string, long> clearanceMicroHours)
    {
        List<ProductionOutputClearanceProfileObservation> observations = new();
        foreach (ProductionOutputClearanceCapacityReviewInput input in inputs)
        {
            foreach (int seed in Seeds)
            {
                observations.Add(new ProductionOutputClearanceProfileObservation(
                    input.DefinitionId,
                    input.WorkstationTag,
                    seed,
                    "batch:qa-clearance:" + input.DefinitionId + ":" + seed,
                    clearanceMicroHours[input.DefinitionId],
                    Hash('f')));
            }
        }
        return ProductionOutputClearanceProfileAggregator.BuildFrozen(
            observations,
            inputs.Select(value => value.ThroughputEnvelope).ToArray(),
            Seeds,
            inputs.Count);
    }

    private static string Hash(char value) => new(value, 64);

    private static void RequireRejects(Action action, string messageFragment)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(
                    messageFragment,
                    StringComparison.OrdinalIgnoreCase),
                "Unexpected rejection reason: " + exception.Message);
            return;
        }
        throw new InvalidOperationException(
            "Expected output-clearance capacity review rejection: "
            + messageFragment);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
