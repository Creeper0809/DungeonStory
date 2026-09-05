using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputClearanceProfileCatalogDebugScenarios
{
    private const string DefinitionId = "building:qa:clearance";
    private const string WorkstationTag = "qa-clearance";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
    }

    [MenuItem("DungeonStory/V27/Verify Frozen Output-Clearance Profile Catalog")]
    public static void RunFromMenu() => Debug.Log(RunAll());

    public static string RunAll()
    {
        ProductionOutputClearanceProfileObservation[] observations =
            CreateObservations();
        ProductionOutputThroughputEnvelopeSnapshot envelope = new(
            DefinitionId,
            WorkstationTag,
            peakOutputMassGramsPerHour: 2_000L,
            sourceDigest: Digest('b'));

        IReadOnlyList<ProductionOutputClearanceProfileRecord> first =
            ProductionOutputClearanceProfileAggregator.BuildFrozen(
                observations,
                new[] { envelope },
                Enumerable.Range(1, 32).ToArray(),
                expectedProfileCount: 1);
        IReadOnlyList<ProductionOutputClearanceProfileRecord> shuffled =
            ProductionOutputClearanceProfileAggregator.BuildFrozen(
                observations.Reverse().ToArray(),
                new[] { envelope },
                Enumerable.Range(1, 32).Reverse().ToArray(),
                expectedProfileCount: 1);

        Require(first.Count == 1, "Aggregation did not emit one profile row.");
        ProductionOutputClearanceProfileRecord row = first[0];
        Require(
            row.P95HaulClearanceMilliHours == 31L,
            "Nearest-rank p95 for thirty-two ordered samples must be the thirty-first value.");
        Require(
            row.SampleCount == 32 && row.DistinctSeedCount == 32,
            "Profile row did not retain exact sample/seed provenance.");
        Require(
            row.PeakOutputMassGramsPerHour == 2_000L,
            "Profile row lost its throughput envelope.");
        Require(
            string.Equals(row.SourceDigest, shuffled[0].SourceDigest,
                StringComparison.Ordinal),
            "Input order changed the immutable profile digest.");

        ProductionOutputClearanceProfileCatalog catalog = new(first);
        string canonicalResource =
            ProductionOutputClearanceProfileResourceCodec.SerializeCanonical(first);
        ProductionOutputClearanceProfileCatalog restoredCatalog =
            ProductionOutputClearanceProfileResourceCodec.ParseRequired(
                canonicalResource,
                expectedProfileCount: 1);
        Require(
            string.Equals(
                catalog.AuthorityDigest,
                restoredCatalog.AuthorityDigest,
                StringComparison.Ordinal),
            "Canonical Resources round-trip changed catalog authority digest.");
        VerifyNaturalBootstrapIsolation();
        RequireThrows(
            () => ProductionOutputClearanceProfileResourceCodec.ParseRequired(
                canonicalResource + "\n",
                expectedProfileCount: 1),
            "Non-canonical trailing Resources whitespace was accepted.");
        RequireThrows(
            () => ProductionOutputClearanceProfileResourceCodec.ParseRequired(
                canonicalResource.Replace(
                    "\"profileCount\":1",
                    "\"unknown\":0,\"profileCount\":1"),
                expectedProfileCount: 1),
            "Unknown Resources JSON field was accepted.");
        RequireThrows(
            () => ProductionOutputClearanceProfileResourceCodec.ParseRequired(
                canonicalResource.Replace(
                    row.SourceDigest,
                    Digest('f')),
                expectedProfileCount: 1),
            "Resources row digest drift was accepted.");
        ProductionOutputClearanceProfileSnapshot captured = catalog.Capture(
            Subject("building:qa:clearance-instance", 91, -17));
        Require(
            captured.P95HaulClearanceMilliHours == 31L
            && captured.PeakOutputMassGramsPerHour == 2_000L
            && string.Equals(captured.SourceDigest, row.SourceDigest,
                StringComparison.Ordinal),
            "Catalog lookup depended on mutable facility instance state.");

        ProductionOutputClearanceRequirementAssessment assessment =
            ProductionOutputClearanceRequirementProjector.Assess(
                maximumCycleCompletionFootprintGrams: 1_000L,
                captured);
        Require(
            assessment.IsAccepted
            && assessment.RequiredCycleMilliCycles == 2_000L,
            "Aggregated profile did not feed the fixed-point requirement boundary.");

        ProductionOutputClearanceRequirementAssessment roundedWholeCycle =
            ProductionOutputClearanceRequirementProjector.Assess(
                1_000L,
                new ProductionOutputClearanceProfileSnapshot(
                    2_001L,
                    1_000L,
                    Digest('7')));
        Require(
            roundedWholeCycle.IsAccepted
            && roundedWholeCycle.RequiredWholeCycles == 3L
            && roundedWholeCycle.PublishedWholeCycles == 3L
            && roundedWholeCycle.PublishedCapacityGrams == 3_000L,
            "2.001 cycles did not round to three whole published cycles.");
        ProductionOutputClearanceRequirementAssessment backpressureBoundary =
            ProductionOutputClearanceRequirementProjector.Assess(
                1_000L,
                new ProductionOutputClearanceProfileSnapshot(
                    4_001L,
                    1_000L,
                    Digest('8')));
        Require(
            !backpressureBoundary.IsAccepted
            && backpressureBoundary.CanPublishBoundedCapacity
            && backpressureBoundary.RequiresBackpressure
            && backpressureBoundary.RequiredWholeCycles == 5L
            && backpressureBoundary.PublishedWholeCycles == 4L
            && backpressureBoundary.PublishedCapacityGrams == 4_000L
            && string.IsNullOrEmpty(backpressureBoundary.FailureCode)
            && string.Equals(
                backpressureBoundary.DiagnosticCode,
                ProductionOutputClearanceRequirementProjector
                    .BackpressureExpectedDiagnosticCode,
                StringComparison.Ordinal),
            "4.001 cycles did not remain visible as bounded backpressure.");

        ProductionOutputClearanceCapacityGateAssessment undersized =
            ProductionOutputClearanceCapacityGate.Assess(
                Subject("building:qa:undersized", 0, 0, cycleCapacity: 2),
                1_000L,
                new ProductionOutputClearanceProfileSnapshot(
                    2_001L,
                    1_000L,
                    Digest('9')));
        Require(
            !undersized.IsAccepted
            && string.Equals(
                undersized.FailureCode,
                ProductionOutputClearanceCapacityGate
                    .AuthoredCapacityUndersizedFailureCode,
                StringComparison.Ordinal)
            && undersized.AuthoredWholeCycles == 2
            && undersized.AuthoredCapacityGrams == 2_000L,
            "An authored two-cycle buffer accepted a measured three-cycle requirement.");
        ProductionOutputClearanceCapacityGateAssessment preserved =
            ProductionOutputClearanceCapacityGate.Assess(
                Subject("building:qa:preserved", 0, 0, cycleCapacity: 4),
                1_000L,
                new ProductionOutputClearanceProfileSnapshot(
                    2_000L,
                    1_000L,
                    Digest('a')));
        Require(
            preserved.IsAccepted
            && preserved.Requirement.RequiredWholeCycles == 2L
            && preserved.AuthoredWholeCycles == 4
            && preserved.AuthoredCapacityGrams == 4_000L,
            "An authored four-cycle buffer was silently shrunk to measured demand.");

        RequireThrows(
            () => catalog.Capture(new ProductionFacilityCapacitySubject(
                (BuildingInstanceId)"building:qa:missing",
                Vector2Int.zero,
                "building:qa:missing",
                WorkstationTag,
                2,
                ManualLaneProfile())),
            "Missing profile silently fell back.");
        RequireThrows(
            () => new ProductionOutputClearanceProfileCatalog(
                new[] { row, row }),
            "Duplicate profile key was accepted.");
        RequireThrows<ArgumentException>(
            () => new ProductionOutputClearanceProfileObservation(
                DefinitionId,
                "workstation;report-delimiter",
                deterministicSeed: 1,
                batchCommitId: "batch:qa:delimiter",
                clearanceMicroHours: 1L,
                runSourceDigest: Digest('d')),
            "A report-delimiter identity was accepted as canonical.");
        RequireThrows(
            () => ProductionOutputClearanceProfileAggregator.Build(
                observations.Concat(new[] { observations[0] }).ToArray(),
                new[] { envelope }),
            "Duplicate observation identity was accepted.");
        RequireThrows(
            () => ProductionOutputClearanceProfileAggregator.Build(
                observations,
                Array.Empty<ProductionOutputThroughputEnvelopeSnapshot>()),
            "Missing throughput envelope was accepted.");
        RequireThrows(
            () => ProductionOutputClearanceProfileAggregator.Build(
                observations,
                new[]
                {
                    envelope,
                    new ProductionOutputThroughputEnvelopeSnapshot(
                        "building:qa:orphan",
                        WorkstationTag,
                        1L,
                        Digest('c'))
                }),
            "Orphan throughput envelope was accepted.");
        RequireThrows(
            () => ProductionOutputClearanceProfileAggregator.Build(
                observations.Take(31).ToArray(),
                new[] { envelope }),
            "Insufficient deterministic seed evidence was accepted.");
        RequireThrows(
            () => ProductionOutputClearanceProfileAggregator.BuildFrozen(
                observations.Concat(new[]
                {
                    new ProductionOutputClearanceProfileObservation(
                        DefinitionId,
                        WorkstationTag,
                        deterministicSeed: 1,
                        batchCommitId: "batch:qa:duplicate-seed",
                        clearanceMicroHours: 1_001L,
                        runSourceDigest: Digest('e'))
                }).ToArray(),
                new[] { envelope },
                Enumerable.Range(1, 32).ToArray(),
                expectedProfileCount: 1),
            "Frozen profile accepted two batches for the same key and seed.");
        RequireThrows(
            () => ProductionOutputClearanceProfileAggregator.BuildFrozen(
                observations.Concat(new[]
                {
                    new ProductionOutputClearanceProfileObservation(
                        DefinitionId,
                        WorkstationTag,
                        deterministicSeed: 33,
                        batchCommitId: "batch:qa:33",
                        clearanceMicroHours: 33_000L,
                        runSourceDigest: Digest('f'))
                }).ToArray(),
                new[] { envelope },
                Enumerable.Range(1, 32).ToArray(),
                expectedProfileCount: 1),
            "Frozen profile accepted an unapproved extra seed cohort.");
        RequireThrows(
            () => ProductionOutputClearanceProfileAggregator.BuildFrozen(
                observations,
                new[] { envelope },
                Enumerable.Range(1, 32).ToArray(),
                expectedProfileCount: 2),
            "Frozen profile accepted an incomplete producer-key set.");

        ProductionOutputClearanceProfileRecord twentySampleRank =
            ProductionOutputClearanceProfileAggregator.Build(
                observations.Take(20).ToArray(),
                new[] { envelope },
                minimumDistinctSeeds: 3)[0];
        Require(
            twentySampleRank.P95HaulClearanceMilliHours == 19L,
            "Nearest-rank p95 for twenty ordered samples must be the nineteenth value.");

        ProductionOutputClearanceProfileObservation[] ceilObservations =
            observations
                .Select((value, index) =>
                    new ProductionOutputClearanceProfileObservation(
                        value.DefinitionId,
                        value.WorkstationTag,
                        value.DeterministicSeed,
                        value.BatchCommitId,
                        index == 30 ? 30_001L : value.ClearanceMicroHours,
                        value.RunSourceDigest))
                .ToArray();
        ProductionOutputClearanceProfileRecord ceilRow =
            ProductionOutputClearanceProfileAggregator.Build(
                ceilObservations,
                new[] { envelope })[0];
        Require(
            ceilRow.P95HaulClearanceMilliHours == 31L,
            "Selected raw micro-hour p95 was not quantized exactly once.");

        ProductionOutputClearanceProfileRecord changedThroughput =
            ProductionOutputClearanceProfileAggregator.Build(
                observations,
                new[]
                {
                    new ProductionOutputThroughputEnvelopeSnapshot(
                        DefinitionId,
                        WorkstationTag,
                        2_000L,
                        Digest('d'))
                })[0];
        Require(
            !string.Equals(row.SourceDigest, changedThroughput.SourceDigest,
                StringComparison.Ordinal),
            "Throughput provenance drift did not invalidate the profile digest.");

        return "PASS nearest-rank-p95 immutable-catalog canonical-resource strict-json natural-bootstrap-isolated shuffle missing duplicate exact-key-seed-cohort seed-gate digest-drift 2.001-to-3 4.001-backpressure-to-4 authored-undersize preserve-larger";
    }

    private static void VerifyNaturalBootstrapIsolation()
    {
        string name = ProductionOutputClearanceNaturalBootstrapProfileSource
            .EnvironmentVariable;
        string previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "wrong-contract");
            Require(
                !ProductionOutputClearanceNaturalBootstrapProfileSource
                    .IsRequested,
                "An unrecognized bootstrap token selected the QA authority.");

            Environment.SetEnvironmentVariable(
                name,
                ProductionOutputClearanceNaturalBootstrapProfileSource
                    .EnvironmentContract);
            Require(
                ProductionOutputClearanceNaturalBootstrapProfileSource
                    .IsRequested,
                "The exact natural 92x32 bootstrap contract was not selected.");
            var source =
                new ProductionOutputClearanceNaturalBootstrapProfileSource();
            ProductionFacilityCapacitySubject subject = Subject(
                "building:qa:bootstrap",
                0,
                0,
                cycleCapacity: 3);
            ProductionOutputClearanceProfileSnapshot profile =
                source.Capture(subject);
            ProductionOutputClearanceCapacityGateAssessment gate =
                ProductionOutputClearanceCapacityGate.Assess(
                    subject,
                    maximumCycleCompletionFootprintGrams: 1_000L,
                    profile);
            Require(
                gate.IsAccepted
                && gate.Requirement.RequiredWholeCycles == 2L
                && gate.AuthoredWholeCycles == 3
                && gate.AuthoredCapacityGrams == 3_000L
                && string.Equals(
                    source.AuthorityDigest,
                    source.AuthorityDigest.ToLowerInvariant(),
                    StringComparison.Ordinal),
                "Natural bootstrap changed authored capacity or lost canonical provenance.");
            VerifyNaturalRunIdentityBindsProfileAuthority(source.AuthorityDigest);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    private static void VerifyNaturalRunIdentityBindsProfileAuthority(
        string bootstrapAuthorityDigest)
    {
        ProductionOutputClearanceNaturalRunIdentity bootstrap = new(
            Digest('1'),
            ProductionOutputClearanceNaturalRunIdentity
                .OfficialGameplaySceneSha256,
            Digest('2'),
            Digest('3'),
            Digest('4'),
            92,
            Digest('5'),
            "natural-output-clearance-shard:qa:profile",
            Digest('6'),
            Digest('7'),
            Digest('8'),
            ProductionOutputClearanceNaturalRunIdentity.BootstrapProfileMode,
            bootstrapAuthorityDigest);
        ProductionOutputClearanceNaturalRunIdentity strict = new(
            Digest('1'),
            ProductionOutputClearanceNaturalRunIdentity
                .OfficialGameplaySceneSha256,
            Digest('2'),
            Digest('3'),
            Digest('4'),
            92,
            Digest('5'),
            "natural-output-clearance-shard:qa:profile",
            Digest('6'),
            Digest('7'),
            Digest('8'),
            ProductionOutputClearanceNaturalRunIdentity.StrictProfileMode,
            Digest('9'));
        Require(
            !bootstrap.SameAs(strict),
            "Bootstrap and strict profile authorities shared one run identity.");
        RequireArgumentThrows(
            () => new ProductionOutputClearanceNaturalRunIdentity(
                Digest('1'),
                ProductionOutputClearanceNaturalRunIdentity
                    .OfficialGameplaySceneSha256,
                Digest('2'), Digest('3'), Digest('4'), 92, Digest('5'),
                "natural-output-clearance-shard:qa:profile", Digest('6'),
                Digest('7'), Digest('8'), "unknown", Digest('9')),
            "A noncanonical natural profile mode was accepted.");
    }

    private static void RequireArgumentThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static ProductionOutputClearanceProfileObservation[]
        CreateObservations()
    {
        List<ProductionOutputClearanceProfileObservation> result = new();
        for (int index = 1; index <= 32; index++)
        {
            result.Add(new ProductionOutputClearanceProfileObservation(
                DefinitionId,
                WorkstationTag,
                deterministicSeed: index,
                batchCommitId: "batch:qa:" + index.ToString("D2"),
                clearanceMicroHours: checked(index * 1_000L),
                runSourceDigest: Digest((char)('0' + ((index - 1) % 10)))));
        }
        return result.ToArray();
    }

    private static ProductionFacilityCapacitySubject Subject(
        string facilityId,
        int x,
        int y,
        int cycleCapacity = 2) => new(
        (BuildingInstanceId)facilityId,
        new Vector2Int(x, y),
        DefinitionId,
        WorkstationTag,
        outputBufferCycleCapacity: cycleCapacity,
        workstationLaneProfile: ManualLaneProfile());

    private static ProductionFacilityWorkstationLaneCapacityProfile
        ManualLaneProfile() => new(
            ProductionWorkstationLanePolicy
                .ManualWithDetachedBatchProcessors,
            manualWorkLaneCount: 1,
            automaticWorkLaneCount: 0);

    private static string Digest(char value) => new(value, 64);

    private static void RequireThrows(Action action, string message)
        => RequireThrows<InvalidOperationException>(action, message);

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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
