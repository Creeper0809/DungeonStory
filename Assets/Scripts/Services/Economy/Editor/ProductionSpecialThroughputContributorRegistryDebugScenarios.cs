#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionSpecialThroughputContributorRegistryDebugScenarios
{
    private const string DefinitionId = "building:qa-special-throughput";
    private const string WorkstationTag = "workstation:qa-special-throughput";
    private const string SyntheticProviderId =
        "special-throughput:qa-synthetic";
    private const string SyntheticCapacityId =
        "production-facility-output-capacity:qa-synthetic";
    private const string CertifiedProviderId =
        "special-throughput:qa-certified-seed-gap";
    private const string CertifiedCapacityId =
        "production-facility-output-capacity:certified-seed";
    private const string SyntheticBranchId = "qa-synthetic-output";
    private const string CertifiedBranchId = "certified-seed:crop:qa";

    [MenuItem(
        "DungeonStory/V27/Production/Validate Special Throughput Contributor Registry")]
    public static void Validate()
    {
        VerifySyntheticCandidateAndCertifiedSeedGap();
        VerifyNonApplicableFacilityProducesNoRows();
        VerifyUnregisteredApplicableCapacityProducesTypedGaps();
        VerifyProductionGapOwnersPublishExactCoverage();
        VerifyInputOrderDoesNotChangeAggregate();
        VerifyDuplicateRegistrationFailsLoudly();
        VerifyOrphanAndApplicabilityDriftFailLoudly();
        VerifyBranchCoverageAndCandidateGapCollisionFailLoudly();
        Debug.Log(
            "[ProductionSpecialThroughputContributorRegistry] focused scenarios passed.");
    }

    private static void VerifyNonApplicableFacilityProducesNoRows()
    {
        DelegateContributor contributor = new(
            SyntheticProviderId,
            1,
            SyntheticCapacityId,
            (facility, capacity) => NonApplicableResult(
                SyntheticProviderId,
                1,
                capacity.ContributorId));
        ProductionFacilityOutputCapacityContribution capacity = new(
            SyntheticCapacityId,
            1,
            false,
            Array.Empty<ProductionFacilityOutputCapacityBranch>());
        ProductionSpecialThroughputAggregate aggregate = Registry(contributor)
            .Capture(Context(capacity));

        Require(aggregate.Contributions.Count == 1
            && !aggregate.Contributions[0].AppliesToFacility
            && aggregate.Candidates.Count == 0
            && aggregate.Gaps.Count == 0,
            "A non-applicable facility published special throughput rows.");
    }

    private static void VerifyUnregisteredApplicableCapacityProducesTypedGaps()
    {
        ProductionFacilityOutputCapacityContribution capacity = new(
            SyntheticCapacityId,
            1,
            true,
            new[]
            {
                Branch(SyntheticBranchId),
                Branch("qa-synthetic-output-second")
            });
        ProductionSpecialThroughputAggregate first = Registry()
            .Capture(Context(capacity));
        ProductionSpecialThroughputAggregate repeat = Registry()
            .Capture(Context(capacity));

        Require(first.Contributions.Count == 0
            && first.Candidates.Count == 0
            && first.Gaps.Count == 2
            && first.Gaps.All(value => value.Reason
                == ProductionThroughputGapReason
                    .SpecialThroughputProviderUnregistered)
            && first.Gaps.All(value => string.Equals(
                value.ProducerId,
                SyntheticCapacityId,
                StringComparison.Ordinal))
            && first.Gaps.Select(value => value.BranchId).SequenceEqual(
                new[]
                {
                    SyntheticBranchId,
                    "qa-synthetic-output-second"
                }.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal),
            "An applicable capacity without an owner was silently omitted.");
        Require(string.Equals(
                first.SourceDigest,
                repeat.SourceDigest,
                StringComparison.Ordinal)
            && first.Gaps.Select(GapToken).SequenceEqual(
                repeat.Gaps.Select(GapToken),
                StringComparer.Ordinal),
            "Unregistered-provider typed gaps were not deterministic.");
    }

    private static void VerifyProductionGapOwnersPublishExactCoverage()
    {
        IProductionSpecialThroughputContributor[] contributors =
        {
            new CertifiedSeedSpecialThroughputGapContributor(),
            new CropHarvestSpecialThroughputGapContributor(),
            new ApparelSpecialThroughputGapContributor(),
            new CombatCraftSpecialThroughputGapContributor()
        };
        ProductionFacilityOutputCapacityContribution[] capacities =
        {
            Capacity(
                CertifiedSeedFacilityOutputCapacityContributor.Id,
                "certified-seed:qa"),
            Capacity(
                CropHarvestFacilityOutputCapacityContributor.Id,
                "crop-harvest:qa"),
            Capacity(
                ApparelFacilityOutputCapacityContributor.Id,
                "apparel:qa"),
            Capacity(
                CombatCraftFacilityOutputCapacityContributor.Id,
                "combat-craft:qa")
        };

        ProductionSpecialThroughputAggregate aggregate = new
            ProductionSpecialThroughputContributorRegistry(contributors)
            .Capture(Context(capacities));

        Require(aggregate.Contributions.Count == 4
            && aggregate.Candidates.Count == 0
            && aggregate.Gaps.Count == 4
            && aggregate.Gaps.Count(value => value.Reason
                == ProductionThroughputGapReason.AuthoredCycleAuthorityMissing)
                == 2
            && aggregate.Gaps.Count(value => value.Reason
                == ProductionThroughputGapReason.ExecutionAuthorityUnsupported)
                == 2
            && aggregate.Gaps.All(value => value.Reason
                != ProductionThroughputGapReason
                    .SpecialThroughputProviderUnregistered),
            "Production special-throughput owners did not publish exact typed gaps.");
    }

    private static void VerifySyntheticCandidateAndCertifiedSeedGap()
    {
        ProductionSpecialThroughputContributorRegistry registry = Registry(
            CertifiedGapContributor(),
            SyntheticCandidateContributor());
        ProductionSpecialThroughputAggregate aggregate = registry.Capture(
            Context(CertifiedCapacity(), SyntheticCapacity()));

        Require(aggregate.Contributions.Count == 2
            && aggregate.Candidates.Count == 1
            && aggregate.Gaps.Count == 1,
            "Synthetic candidate and certified-seed gap were not captured exactly.");
        Require(string.Equals(
                aggregate.Candidates[0].ProducerId,
                SyntheticCapacityId,
                StringComparison.Ordinal)
            && string.Equals(
                aggregate.Candidates[0].BranchId,
                SyntheticBranchId,
                StringComparison.Ordinal)
            && aggregate.Candidates[0].PeakOutputMassGramsPerHour == 1_250L,
            "Synthetic candidate lost exact producer, branch, or peak provenance.");
        Require(string.Equals(
                aggregate.Gaps[0].ProducerId,
                CertifiedCapacityId,
                StringComparison.Ordinal)
            && string.Equals(
                aggregate.Gaps[0].BranchId,
                CertifiedBranchId,
                StringComparison.Ordinal)
            && aggregate.Gaps[0].Reason
                == ProductionThroughputGapReason.AuthoredCycleAuthorityMissing,
            "Certified-seed did not retain its authored-cycle typed gap.");

        ProductionAuthoredThroughputFacilitySubject subject = aggregate
            .CreateFacilitySubject(
                ProductionFacilityWorkstationLaneCapacityProfile
                    .SingleManualWithDetachedBatchProcessors,
                ProductionFacilityProcessFluidCapacityProfile.Empty,
                Array.Empty<ProductionRecipeSO>());
        Require(subject.SpecialCandidates.Count == 1
            && subject.SpecialGaps.Count == 1
            && subject.SpecialCandidates[0].SourceDigest
                == aggregate.Candidates[0].SourceDigest
            && subject.SpecialGaps[0].SourceDigest
                == aggregate.Gaps[0].SourceDigest,
            "Immutable aggregate did not inject exact rows into the facility subject.");
    }

    private static void VerifyInputOrderDoesNotChangeAggregate()
    {
        ProductionSpecialThroughputAggregate first = Registry(
                SyntheticCandidateContributor(),
                CertifiedGapContributor())
            .Capture(Context(SyntheticCapacity(), CertifiedCapacity()));
        ProductionSpecialThroughputAggregate shuffled = Registry(
                CertifiedGapContributor(),
                SyntheticCandidateContributor())
            .Capture(Context(CertifiedCapacity(), SyntheticCapacity()));

        Require(string.Equals(
                first.SourceDigest,
                shuffled.SourceDigest,
                StringComparison.Ordinal)
            && first.Contributions.Select(value => value.SourceDigest)
                .SequenceEqual(
                    shuffled.Contributions.Select(value => value.SourceDigest),
                    StringComparer.Ordinal)
            && first.Candidates.Select(CandidateToken).SequenceEqual(
                shuffled.Candidates.Select(CandidateToken),
                StringComparer.Ordinal)
            && first.Gaps.Select(GapToken).SequenceEqual(
                shuffled.Gaps.Select(GapToken),
                StringComparer.Ordinal),
            "Registration or capacity input order changed the aggregate.");
    }

    private static void VerifyDuplicateRegistrationFailsLoudly()
    {
        Expect<InvalidOperationException>(() => Registry(
            SyntheticCandidateContributor(),
            new DelegateContributor(
                SyntheticProviderId,
                2,
                "production-facility-output-capacity:qa-other",
                (facility, capacity) => NonApplicableResult(
                    SyntheticProviderId,
                    2,
                    capacity.ContributorId))));

        Expect<InvalidOperationException>(() => Registry(
            SyntheticCandidateContributor(),
            new DelegateContributor(
                "special-throughput:qa-duplicate-owner",
                1,
                SyntheticCapacityId,
                (facility, capacity) => SyntheticCandidateResult(
                    "special-throughput:qa-duplicate-owner",
                    capacity))));
    }

    private static void VerifyOrphanAndApplicabilityDriftFailLoudly()
    {
        Expect<InvalidOperationException>(() => Registry(
                SyntheticCandidateContributor())
            .Capture(Context(CertifiedCapacity())));

        DelegateContributor applicabilityDrift = new(
            "special-throughput:qa-applicability-drift",
            1,
            SyntheticCapacityId,
            (facility, capacity) => NonApplicableResult(
                "special-throughput:qa-applicability-drift",
                1,
                capacity.ContributorId));
        Expect<InvalidOperationException>(() => Registry(applicabilityDrift)
            .Capture(Context(SyntheticCapacity())));
    }

    private static void VerifyBranchCoverageAndCandidateGapCollisionFailLoudly()
    {
        DelegateContributor orphanBranch = new(
            "special-throughput:qa-orphan-branch",
            1,
            SyntheticCapacityId,
            (facility, capacity) => Result(
                "special-throughput:qa-orphan-branch",
                1,
                capacity.ContributorId,
                true,
                new[]
                {
                    Candidate(
                        facility,
                        capacity.ContributorId,
                        "qa-orphan-output",
                        10L)
                },
                Array.Empty<ProductionThroughputCoverageGap>(),
                "orphan-branch"));
        Expect<InvalidOperationException>(() => Registry(orphanBranch)
            .Capture(Context(SyntheticCapacity())));

        DelegateContributor collision = new(
            "special-throughput:qa-collision",
            1,
            SyntheticCapacityId,
            (facility, capacity) => Result(
                "special-throughput:qa-collision",
                1,
                capacity.ContributorId,
                true,
                new[]
                {
                    Candidate(
                        facility,
                        capacity.ContributorId,
                        SyntheticBranchId,
                        10L)
                },
                new[]
                {
                    Gap(
                        facility,
                        capacity.ContributorId,
                        SyntheticBranchId,
                        ProductionThroughputGapReason
                            .AuthoredCycleAuthorityMissing,
                        "collision")
                },
                "candidate-gap-collision"));
        Expect<InvalidOperationException>(() => Registry(collision)
            .Capture(Context(SyntheticCapacity())));

        ProductionFacilityOutputCapacityContribution twoBranches = new(
            SyntheticCapacityId,
            1,
            true,
            new[]
            {
                Branch(SyntheticBranchId),
                Branch("qa-synthetic-output-second")
            });
        Expect<InvalidOperationException>(() => Registry(
                SyntheticCandidateContributor())
            .Capture(Context(twoBranches)));
    }

    private static DelegateContributor SyntheticCandidateContributor() => new(
        SyntheticProviderId,
        1,
        SyntheticCapacityId,
        (facility, capacity) => SyntheticCandidateResult(
            SyntheticProviderId,
            capacity,
            facility));

    private static ProductionSpecialThroughputContributorResult
        SyntheticCandidateResult(
            string contributorId,
            ProductionFacilityOutputCapacityContribution capacity,
            ProductionSpecialThroughputFacilityContext facility = null)
    {
        ProductionSpecialThroughputFacilityContext resolved = facility
            ?? Context(capacity);
        return Result(
            contributorId,
            1,
            capacity.ContributorId,
            true,
            new[]
            {
                Candidate(
                    resolved,
                    capacity.ContributorId,
                    SyntheticBranchId,
                    1_250L)
            },
            Array.Empty<ProductionThroughputCoverageGap>(),
            "synthetic-candidate");
    }

    private static DelegateContributor CertifiedGapContributor() => new(
        CertifiedProviderId,
        1,
        CertifiedCapacityId,
        (facility, capacity) => Result(
            CertifiedProviderId,
            1,
            capacity.ContributorId,
            true,
            Array.Empty<ProductionSpecialThroughputCandidateSnapshot>(),
            new[]
            {
                Gap(
                    facility,
                    capacity.ContributorId,
                    CertifiedBranchId,
                    ProductionThroughputGapReason.AuthoredCycleAuthorityMissing,
                    "certified seed has no authored cycle authority")
            },
            "certified-seed-cycle-gap"));

    private static ProductionSpecialThroughputContributorResult
        NonApplicableResult(
            string contributorId,
            int version,
            string capacityContributorId) => Result(
        contributorId,
        version,
        capacityContributorId,
        false,
        Array.Empty<ProductionSpecialThroughputCandidateSnapshot>(),
        Array.Empty<ProductionThroughputCoverageGap>(),
        "not-applicable");

    private static ProductionSpecialThroughputContributorResult Result(
        string contributorId,
        int version,
        string capacityContributorId,
        bool applies,
        IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot> candidates,
        IReadOnlyList<ProductionThroughputCoverageGap> gaps,
        string sourceToken) => new(
        contributorId,
        version,
        capacityContributorId,
        applies,
        candidates,
        gaps,
        Digest(sourceToken));

    private static ProductionSpecialThroughputCandidateSnapshot Candidate(
        ProductionSpecialThroughputFacilityContext facility,
        string producerId,
        string branchId,
        long peak) => new(
        facility.DefinitionId,
        facility.WorkstationTag,
        producerId,
        branchId,
        peak,
        Digest(producerId + ":" + branchId + ":" + peak));

    private static ProductionThroughputCoverageGap Gap(
        ProductionSpecialThroughputFacilityContext facility,
        string producerId,
        string branchId,
        ProductionThroughputGapReason reason,
        string detail) => new(
        facility.DefinitionId,
        facility.WorkstationTag,
        ProductionThroughputProducerKind.CapacityContributor,
        producerId,
        branchId,
        reason,
        detail,
        Digest(producerId + ":" + branchId + ":" + reason + ":" + detail));

    private static ProductionSpecialThroughputContributorRegistry Registry(
        params IProductionSpecialThroughputContributor[] contributors) => new(
        contributors);

    private static ProductionSpecialThroughputFacilityContext Context(
        params ProductionFacilityOutputCapacityContribution[] contributions) => new(
        DefinitionId,
        WorkstationTag,
        contributions);

    private static ProductionFacilityOutputCapacityContribution
        SyntheticCapacity() => new(
        SyntheticCapacityId,
        1,
        true,
        new[] { Branch(SyntheticBranchId) });

    private static ProductionFacilityOutputCapacityContribution
        CertifiedCapacity() => new(
        CertifiedCapacityId,
        1,
        true,
        new[] { Branch(CertifiedBranchId) });

    private static ProductionFacilityOutputCapacityContribution Capacity(
        string contributorId,
        string branchId) => new(
        contributorId,
        1,
        true,
        new[] { Branch(branchId) });

    private static ProductionFacilityOutputCapacityBranch Branch(string branchId) =>
        new(
            branchId,
            new[]
            {
                new ProductionFacilityOutputMaximumMassRequest(
                    "output:" + branchId,
                    "resource:qa-special-output",
                    ProductionOutputCapabilityIds.StandardDefinition,
                    1)
            });

    private static string CandidateToken(
        ProductionSpecialThroughputCandidateSnapshot candidate) =>
        candidate.ProducerId + "\n" + candidate.BranchId + "\n"
        + candidate.PeakOutputMassGramsPerHour + "\n" + candidate.SourceDigest;

    private static string GapToken(ProductionThroughputCoverageGap gap) =>
        gap.ProducerId + "\n" + gap.BranchId + "\n" + gap.Reason + "\n"
        + gap.SourceDigest;

    private static string Digest(string token)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(
            "production-special-throughput-contributor-registry-debug@1");
        digest.Append(token);
        return digest.ComputeSha256();
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

    private sealed class DelegateContributor :
        IProductionSpecialThroughputContributor
    {
        private readonly Func<
            ProductionSpecialThroughputFacilityContext,
            ProductionFacilityOutputCapacityContribution,
            ProductionSpecialThroughputContributorResult> capture;

        public DelegateContributor(
            string contributorId,
            int contractVersion,
            string capacityContributorId,
            Func<
                ProductionSpecialThroughputFacilityContext,
                ProductionFacilityOutputCapacityContribution,
                ProductionSpecialThroughputContributorResult> capture)
        {
            ContributorId = contributorId;
            ContractVersion = contractVersion;
            CapacityContributorId = capacityContributorId;
            this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        }

        public string ContributorId { get; }
        public int ContractVersion { get; }
        public string CapacityContributorId { get; }

        public ProductionSpecialThroughputContributorResult Capture(
            ProductionSpecialThroughputFacilityContext facility,
            ProductionFacilityOutputCapacityContribution capacityContribution) =>
            capture(facility, capacityContribution);
    }
}
#endif
