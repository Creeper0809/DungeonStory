using System;
using System.Collections.Generic;
using System.Linq;

public abstract class ProductionSpecialThroughputGapContributorBase :
    IProductionSpecialThroughputContributor
{
    private const string Schema =
        "production-special-throughput-gap-contributor@1";
    private readonly ProductionThroughputGapReason reason;
    private readonly string detail;
    private readonly string contributorSourceDigest;

    protected ProductionSpecialThroughputGapContributorBase(
        string contributorId,
        int contractVersion,
        string capacityContributorId,
        ProductionThroughputGapReason reason,
        string detail)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            contributorId,
            nameof(contributorId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            capacityContributorId,
            nameof(capacityContributorId));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        if (reason != ProductionThroughputGapReason.AuthoredCycleAuthorityMissing
            && reason != ProductionThroughputGapReason.ExecutionAuthorityUnsupported)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }
        if (string.IsNullOrWhiteSpace(detail)
            || !string.Equals(detail, detail.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical non-empty typed-gap detail is required.",
                nameof(detail));
        }

        ContributorId = contributorId;
        ContractVersion = contractVersion;
        CapacityContributorId = capacityContributorId;
        this.reason = reason;
        this.detail = detail;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ContributorId);
        digest.Append(ContractVersion);
        digest.Append(CapacityContributorId);
        digest.Append((int)reason);
        digest.Append(detail);
        contributorSourceDigest = digest.ComputeSha256();
    }

    public string ContributorId { get; }
    public int ContractVersion { get; }
    public string CapacityContributorId { get; }

    public ProductionSpecialThroughputContributorResult Capture(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution capacityContribution)
    {
        if (facility == null)
            throw new ArgumentNullException(nameof(facility));
        if (capacityContribution == null)
            throw new ArgumentNullException(nameof(capacityContribution));
        if (!string.Equals(
                capacityContribution.ContributorId,
                CapacityContributorId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Special throughput contributor received a foreign capacity authority: "
                + ContributorId + "/" + capacityContribution.ContributorId);
        }

        ProductionFacilityOutputCapacityContribution captured = facility
            .CapacityContributions.SingleOrDefault(value => string.Equals(
                value.ContributorId,
                CapacityContributorId,
                StringComparison.Ordinal));
        if (captured == null
            || !string.Equals(
                captured.SourceDigest,
                capacityContribution.SourceDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Special throughput contributor capacity is not part of the facility snapshot: "
                + ContributorId);
        }

        if (!capacityContribution.AppliesToFacility)
        {
            return new ProductionSpecialThroughputContributorResult(
                ContributorId,
                ContractVersion,
                CapacityContributorId,
                false,
                Array.Empty<ProductionSpecialThroughputCandidateSnapshot>(),
                Array.Empty<ProductionThroughputCoverageGap>(),
                contributorSourceDigest);
        }

        List<ProductionThroughputCoverageGap> gaps = new(
            capacityContribution.Branches.Count);
        foreach (ProductionFacilityOutputCapacityBranch branch in
                 capacityContribution.Branches.OrderBy(
                     value => value.BranchId,
                     StringComparer.Ordinal))
        {
            CanonicalSemanticDigestBuilder gapDigest = new();
            gapDigest.Append(Schema);
            gapDigest.Append(contributorSourceDigest);
            gapDigest.Append(facility.SourceDigest);
            gapDigest.Append(capacityContribution.SourceDigest);
            gapDigest.Append(branch.BranchId);
            gapDigest.Append((int)reason);
            gapDigest.Append(detail);
            gaps.Add(new ProductionThroughputCoverageGap(
                facility.DefinitionId,
                facility.WorkstationTag,
                ProductionThroughputProducerKind.CapacityContributor,
                CapacityContributorId,
                branch.BranchId,
                reason,
                detail,
                gapDigest.ComputeSha256()));
        }

        return new ProductionSpecialThroughputContributorResult(
            ContributorId,
            ContractVersion,
            CapacityContributorId,
            true,
            Array.Empty<ProductionSpecialThroughputCandidateSnapshot>(),
            gaps,
            contributorSourceDigest);
    }
}

public sealed class CertifiedSeedSpecialThroughputGapContributor :
    ProductionSpecialThroughputGapContributorBase
{
    public const string Id = "special-throughput:certified-seed";

    public CertifiedSeedSpecialThroughputGapContributor() : base(
        Id,
        1,
        CertifiedSeedFacilityOutputCapacityContributor.Id,
        ProductionThroughputGapReason.AuthoredCycleAuthorityMissing,
        "Certified-seed output has no authored cycle throughput authority.")
    {
    }
}

public sealed class CropHarvestSpecialThroughputGapContributor :
    ProductionSpecialThroughputGapContributorBase
{
    public const string Id = "special-throughput:crop-harvest";

    public CropHarvestSpecialThroughputGapContributor() : base(
        Id,
        1,
        CropHarvestFacilityOutputCapacityContributor.Id,
        ProductionThroughputGapReason.AuthoredCycleAuthorityMissing,
        "Crop-harvest output has no authored cycle throughput authority.")
    {
    }
}

public sealed class ApparelSpecialThroughputGapContributor :
    ProductionSpecialThroughputGapContributorBase
{
    public const string Id = "special-throughput:apparel";

    public ApparelSpecialThroughputGapContributor() : base(
        Id,
        1,
        ApparelFacilityOutputCapacityContributor.Id,
        ProductionThroughputGapReason.ExecutionAuthorityUnsupported,
        "Apparel execution does not expose an exact authored cycle authority.")
    {
    }
}

public sealed class CombatCraftSpecialThroughputGapContributor :
    ProductionSpecialThroughputGapContributorBase
{
    public const string Id = "special-throughput:combat-craft";

    public CombatCraftSpecialThroughputGapContributor() : base(
        Id,
        1,
        CombatCraftFacilityOutputCapacityContributor.Id,
        ProductionThroughputGapReason.ExecutionAuthorityUnsupported,
        "Combat-craft execution does not expose an exact authored cycle authority.")
    {
    }
}
