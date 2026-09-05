#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

public static class
    ProductionOutputClearanceCanonicalNaturalArtifactProjectionDebugScenarios
{
    public static string Run()
    {
        Fixture fixture = new();
        ProductionOutputClearanceNaturalShardEvidence first = fixture.CreateEvidence(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            100L,
            reverseInput: false);
        ProductionOutputClearanceNaturalShardEvidence shuffledRawIds =
            fixture.CreateEvidence(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                100L,
                reverseInput: true);
        ProductionOutputClearanceNaturalShardEvidence changedMass =
            fixture.CreateEvidence(
                "cccccccccccccccccccccccccccccccc",
                101L,
                reverseInput: true);

        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot a =
            ProductionOutputClearanceCanonicalNaturalArtifactProjection.BuildFocused(
                fixture.PortfolioDigest,
                new[] { first });
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot b =
            ProductionOutputClearanceCanonicalNaturalArtifactProjection.BuildFocused(
                fixture.PortfolioDigest,
                new[] { shuffledRawIds });
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot mass =
            ProductionOutputClearanceCanonicalNaturalArtifactProjection.BuildFocused(
                fixture.PortfolioDigest,
                new[] { changedMass });

        Require(!string.Equals(first.SourceDigest, shuffledRawIds.SourceDigest,
                StringComparison.Ordinal),
            "Raw execution identities did not affect the forensic evidence digest.");
        Require(string.Equals(a.SourceDigest, b.SourceDigest,
                StringComparison.Ordinal)
            && string.Equals(a.CaptureCanonicalDebugText(),
                b.CaptureCanonicalDebugText(), StringComparison.Ordinal),
            "Raw GUID/stack shuffle changed the canonical artifact projection.");
        Require(string.Equals(
                a.Records.Single().CanonicalRunDigest,
                b.Records.Single().CanonicalRunDigest,
                StringComparison.Ordinal)
            && a.OutputSlices.Select(value => value.SemanticDigest)
                .SequenceEqual(
                    b.OutputSlices.Select(value => value.SemanticDigest),
                    StringComparer.Ordinal),
            "Raw GUID/stack shuffle changed an observation or slice digest.");
        Require(!string.Equals(a.SourceDigest, mass.SourceDigest,
                StringComparison.Ordinal)
            && !string.Equals(a.CaptureCanonicalDebugText(),
                mass.CaptureCanonicalDebugText(), StringComparison.Ordinal),
            "A semantic physical-mass change was hidden by canonical projection.");
        Require(!string.Equals(
                a.Records.Single().CanonicalRunDigest,
                mass.Records.Single().CanonicalRunDigest,
                StringComparison.Ordinal)
            && !a.OutputSlices.Select(value => value.SemanticDigest)
                .SequenceEqual(
                    mass.OutputSlices.Select(value => value.SemanticDigest),
                    StringComparer.Ordinal),
            "A semantic physical-mass change was hidden at observation or slice scope.");

        ProductionOutputClearanceCanonicalNaturalObservationRecord observation =
            a.Records.Single();
        Require(string.Equals(
                observation.FacilitySemanticId,
                "natural-facility:" + observation.ObservationId + ":producer",
                StringComparison.Ordinal)
            && string.Equals(
                observation.BatchSemanticId,
                "natural-batch:" + observation.ObservationId + ":aggregate",
                StringComparison.Ordinal)
            && observation.OutputSlices.Select(value => value.StackSemanticId)
                .SequenceEqual(new[]
                {
                    "natural-stack:" + observation.ObservationId
                        + ":output:main:0000",
                    "natural-stack:" + observation.ObservationId
                        + ":output:main:0001"
                }, StringComparer.Ordinal),
            "Canonical semantic aliases are not stable or ordinal.");

        bool exactJoinRejected = Rejects(() => new
            ProductionOutputClearanceNaturalShardEvidence(
                first.Request,
                first.Receipt,
                fixture.CreateObservation(
                    first.Receipt,
                    "batch:foreign",
                    first.Receipt.ActualBatchMassGrams)));
        Require(exactJoinRejected,
            "A raw receipt/observation batch mismatch bypassed exact evidence validation.");

        return "PASS canonicalNaturalProjection=raw-guid-shuffle-stable;"
            + "semantic-mass-sensitive;raw-exact-join-enforced;schema="
            + ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot.Schema;
    }

    private static bool Rejects(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }

    private sealed class Fixture
    {
        private const int Seed = 157181;
        private readonly ProductionOutputClearanceMeasurementFixture fixture;
        private readonly ProductionOutputClearanceNaturalPortfolioShardSnapshot shard;

        internal Fixture()
        {
            string digest = Digest("canonical-natural-debug-source");
            ProductionOutputClearanceMeasurementSourceBranch source = new(
                ProductionOutputClearanceMeasurementSourceKind.CapacityContributor,
                "debug-source:canonical-natural",
                1,
                "debug-producer:canonical-natural",
                "debug-branch:canonical-natural",
                1_000L,
                new[] { "output-capability:debug-canonical-natural" },
                digest);
            ProductionOutputClearanceMeasurementCandidate candidate =
                ProductionOutputClearanceNaturalProjectionEditorTestFactory
                    .CreateCandidate(
                source,
                "measurement:debug-canonical-natural",
                "contributor:debug-canonical-natural",
                1);
            ProductionOutputClearanceMeasurementPlan plan =
                ProductionOutputClearanceNaturalProjectionEditorTestFactory
                    .CreatePlan(
                "building:debug-canonical-natural",
                "workstation:debug-canonical-natural",
                new[] { candidate },
                digest,
                digest);
            int[] seeds = Enumerable.Range(0,
                    ProductionOutputClearanceMeasurementPortfolioAuthority
                        .RequiredSeedCount)
                .Select(index => checked(Seed + index))
                .ToArray();
            ProductionOutputClearanceMeasurementFixture[] fixtures = seeds
                .Select((seed, index) =>
                    ProductionOutputClearanceNaturalProjectionEditorTestFactory
                        .CreateFixture(
                        plan,
                        index,
                        seed))
                .ToArray();
            ProductionOutputClearanceExecutableDescriptor descriptor =
                ProductionOutputClearanceNaturalProjectionEditorTestFactory
                    .CreateDescriptor(
                plan,
                digest,
                2,
                new DebugPayload(digest));
            shard = new ProductionOutputClearanceNaturalPortfolioShardSnapshot(
                descriptor,
                fixtures,
                seeds);
            fixture = fixtures[0];
            PortfolioDigest = Digest("canonical-natural-debug-portfolio");
        }

        internal string PortfolioDigest { get; }

        internal ProductionOutputClearanceNaturalShardEvidence CreateEvidence(
            string rawIdentity,
            long sliceMass,
            bool reverseInput)
        {
            ProductionOutputClearanceNaturalExecutionRequest request = new(
                shard,
                fixture);
            string capability = Digest("canonical-natural-debug-capability");
            ProductionOutputClearanceExecutionOutputSliceSnapshot one = new(
                "output:main",
                "item:debug-canonical-natural",
                "item-instance:" + rawIdentity + ":one",
                "world-stack:" + rawIdentity + ":one",
                1,
                sliceMass,
                capability);
            ProductionOutputClearanceExecutionOutputSliceSnapshot two = new(
                "output:main",
                "item:debug-canonical-natural",
                "item-instance:" + rawIdentity + ":two",
                "world-stack:" + rawIdentity + ":two",
                1,
                sliceMass,
                capability);
            ProductionOutputClearanceExecutionOutputSliceSnapshot[] outputs =
                reverseInput ? new[] { two, one } : new[] { one, two };
            string batch = "batch:" + rawIdentity;
            long totalMass = checked(sliceMass * 2L);
            string resolved = Digest("resolved:" + rawIdentity);
            ProductionOutputClearanceExecutionReceiptSnapshot receipt = new(
                shard.Descriptor,
                request.ActionId,
                "facility:" + rawIdentity,
                "operation:" + rawIdentity,
                batch,
                Digest("outcome:" + rawIdentity),
                Digest("planned:" + rawIdentity),
                resolved,
                totalMass,
                outputs,
                Digest("receipt:" + rawIdentity),
                "handler:debug-canonical-natural",
                1,
                new[] { batch });
            ProductionOutputClearanceNaturalObservationRecord observation =
                CreateObservation(receipt, batch, totalMass);
            return new ProductionOutputClearanceNaturalShardEvidence(
                request,
                receipt,
                observation);
        }

        internal ProductionOutputClearanceNaturalObservationRecord CreateObservation(
            ProductionOutputClearanceExecutionReceiptSnapshot receipt,
            string batchCommitId,
            long totalMass) => new(
            fixture,
            receipt.RuntimeFacilityId,
            receipt.ResolvedOutputVectorDigest,
            totalMass,
            batchCommitId,
            Digest("topology:canonical-natural-debug"),
            true,
            true,
            "staff:01",
            1L,
            1L,
            1L,
            1_000L,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            true,
            true,
            true,
            Digest("random:canonical-natural-debug"),
            1L);
    }

    private sealed class DebugPayload :
        IProductionOutputClearanceExecutablePayload
    {
        internal DebugPayload(string sourceDigest)
        {
            SourceDigest = sourceDigest;
        }

        public string PayloadKind => "debug-canonical-natural";
        public string SourceDigest { get; }
    }

    private static string Digest(string value)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-canonical-natural-debug@1");
        digest.Append(value);
        return digest.ComputeSha256();
    }
}
#endif
