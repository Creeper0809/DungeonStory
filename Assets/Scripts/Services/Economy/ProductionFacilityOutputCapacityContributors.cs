using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct ProductionFacilityOutputMaximumMassRequest
{
    public ProductionFacilityOutputMaximumMassRequest(
        string outputLineId,
        string itemId,
        string capabilityId,
        int maximumQuantity)
    {
        if (!ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || !Canonical(itemId)
            || !Canonical(capabilityId)
            || maximumQuantity <= 0)
        {
            throw new ArgumentException(
                "Facility output maximum-mass request is invalid.");
        }
        OutputLineId = outputLineId;
        ItemId = itemId;
        CapabilityId = capabilityId;
        MaximumQuantity = maximumQuantity;
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public string CapabilityId { get; }
    public int MaximumQuantity { get; }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class ProductionFacilityOutputCapacityBranch
{
    public ProductionFacilityOutputCapacityBranch(
        string branchId,
        IReadOnlyList<ProductionFacilityOutputMaximumMassRequest> outputs,
        string semanticSourceDigest = "")
    {
        if (string.IsNullOrWhiteSpace(branchId)
            || !string.Equals(branchId, branchId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Facility output capacity branch ID must be canonical.",
                nameof(branchId));
        }
        ProductionFacilityOutputMaximumMassRequest[] ordered = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.ItemId, StringComparer.Ordinal)
            .ThenBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Facility output capacity branch requires unique output lines.");
        }
        string canonicalDigest = semanticSourceDigest ?? string.Empty;
        if (canonicalDigest.Length != 0 && canonicalDigest.Length != 64)
        {
            throw new ArgumentException(
                "Facility output capacity branch semantic digest must be empty or SHA-256.",
                nameof(semanticSourceDigest));
        }
        BranchId = branchId;
        Outputs = Array.AsReadOnly(ordered);
        SemanticSourceDigest = canonicalDigest;
    }

    public string BranchId { get; }
    public IReadOnlyList<ProductionFacilityOutputMaximumMassRequest> Outputs { get; }
    public string SemanticSourceDigest { get; }
}

public sealed class ProductionFacilityOutputCapacityContribution
{
    public ProductionFacilityOutputCapacityContribution(
        string contributorId,
        int contractVersion,
        bool appliesToFacility,
        IReadOnlyList<ProductionFacilityOutputCapacityBranch> branches)
    {
        if (string.IsNullOrWhiteSpace(contributorId)
            || !string.Equals(
                contributorId,
                contributorId.Trim(),
                StringComparison.Ordinal)
            || contractVersion <= 0)
        {
            throw new ArgumentException(
                "Facility output capacity contributor metadata is invalid.");
        }
        ProductionFacilityOutputCapacityBranch[] ordered = (branches
                ?? throw new ArgumentNullException(nameof(branches)))
            .OrderBy(value => value?.BranchId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.BranchId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || appliesToFacility == (ordered.Length == 0))
        {
            throw new InvalidOperationException(
                "Facility output capacity contribution branches are invalid.");
        }
        ContributorId = contributorId;
        ContractVersion = contractVersion;
        AppliesToFacility = appliesToFacility;
        Branches = Array.AsReadOnly(ordered);
        SourceDigest = CaptureDigest();
    }

    public string ContributorId { get; }
    public int ContractVersion { get; }
    public bool AppliesToFacility { get; }
    public IReadOnlyList<ProductionFacilityOutputCapacityBranch> Branches { get; }
    public string SourceDigest { get; }

    private string CaptureDigest()
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-capacity-contribution@1");
        digest.Append(ContributorId);
        digest.Append(ContractVersion);
        digest.Append(AppliesToFacility);
        digest.Append(Branches.Count);
        foreach (ProductionFacilityOutputCapacityBranch branch in Branches)
        {
            digest.Append(branch.BranchId);
            digest.Append(branch.SemanticSourceDigest);
            digest.Append(branch.Outputs.Count);
            foreach (ProductionFacilityOutputMaximumMassRequest output in branch.Outputs)
            {
                digest.Append(output.OutputLineId);
                digest.Append(output.ItemId);
                digest.Append(output.CapabilityId);
                digest.Append(output.MaximumQuantity);
            }
        }
        return digest.ComputeSha256();
    }
}

public sealed class ProductionFacilityOutputCapacityBranchMassSnapshot
{
    internal ProductionFacilityOutputCapacityBranchMassSnapshot(
        string branchId,
        IReadOnlyList<ProductionOutputMaximumMassProjection> projections,
        long maximumMassGrams,
        string sourceDigest)
    {
        BranchId = branchId;
        Projections = Array.AsReadOnly((projections
                ?? throw new ArgumentNullException(nameof(projections)))
            .ToArray());
        MaximumMassGrams = maximumMassGrams;
        SourceDigest = sourceDigest;
        if (Projections.Count == 0
            || MaximumMassGrams <= 0L
            || string.IsNullOrEmpty(SourceDigest)
            || SourceDigest.Length != 64)
        {
            throw new InvalidOperationException(
                "Facility output branch mass snapshot is invalid.");
        }
    }

    public string BranchId { get; }
    public IReadOnlyList<ProductionOutputMaximumMassProjection> Projections
        { get; }
    public long MaximumMassGrams { get; }
    public string SourceDigest { get; }
}

public interface IProductionFacilityOutputCapacityBranchMassQuery
{
    ProductionFacilityOutputCapacityBranchMassSnapshot Capture(
        ProductionFacilityOutputCapacityBranch branch);
}

public sealed class ProductionFacilityOutputCapacityBranchMassAuthority :
    IProductionFacilityOutputCapacityBranchMassQuery
{
    public const string Schema =
        "production-facility-output-capacity-branch-mass@1";
    private readonly IProductionOutputMaximumMassCapabilitySelector selector;

    public ProductionFacilityOutputCapacityBranchMassAuthority(
        IProductionOutputMaximumMassCapabilitySelector selector)
    {
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public ProductionFacilityOutputCapacityBranchMassSnapshot Capture(
        ProductionFacilityOutputCapacityBranch branch)
    {
        if (branch == null) throw new ArgumentNullException(nameof(branch));
        List<ProductionOutputMaximumMassProjection> projections = new(
            branch.Outputs.Count);
        long total = 0L;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(branch.BranchId);
        digest.Append(branch.Outputs.Count);
        foreach (ProductionFacilityOutputMaximumMassRequest output in
                 branch.Outputs)
        {
            ProductionOutputMaximumMassProjection projection =
                selector.CaptureForCapability(
                    output.OutputLineId,
                    output.ItemId,
                    output.CapabilityId,
                    output.MaximumQuantity);
            projections.Add(projection);
            total = checked(total + projection.MaximumMassGrams);
            digest.Append(output.OutputLineId);
            digest.Append(output.ItemId);
            digest.Append(output.CapabilityId);
            digest.Append(output.MaximumQuantity);
            digest.Append(projection.SourceDigest);
            digest.Append(projection.MaximumMassGrams);
            digest.Append(projection.MassAuthorityRevision);
        }
        digest.Append(total);
        return new ProductionFacilityOutputCapacityBranchMassSnapshot(
            branch.BranchId,
            projections,
            total,
            digest.ComputeSha256());
    }
}

public interface IProductionFacilityOutputCapacityContributor
{
    string ContributorId { get; }
    int ContractVersion { get; }

    ProductionFacilityOutputCapacityContribution Capture(
        ProductionFacilityCapacitySubject subject);
}

public readonly struct ProductionFacilityOutputCapacityAggregateSnapshot
{
    public ProductionFacilityOutputCapacityAggregateSnapshot(
        int applicableContributorCount,
        int branchCount,
        long maximumBatchMassGrams,
        string winningContributorId,
        string winningBranchId,
        string sourceDigest)
    {
        if (applicableContributorCount < 0
            || branchCount < 0
            || maximumBatchMassGrams < 0L
            || string.IsNullOrEmpty(sourceDigest)
            || sourceDigest.Length != 64
            || maximumBatchMassGrams == 0L
                != (string.IsNullOrEmpty(winningContributorId)
                    && string.IsNullOrEmpty(winningBranchId)))
        {
            throw new ArgumentException(
                "Facility output capacity aggregate is invalid.");
        }
        ApplicableContributorCount = applicableContributorCount;
        BranchCount = branchCount;
        MaximumBatchMassGrams = maximumBatchMassGrams;
        WinningContributorId = winningContributorId ?? string.Empty;
        WinningBranchId = winningBranchId ?? string.Empty;
        SourceDigest = sourceDigest;
    }

    public int ApplicableContributorCount { get; }
    public int BranchCount { get; }
    public long MaximumBatchMassGrams { get; }
    public string WinningContributorId { get; }
    public string WinningBranchId { get; }
    public string SourceDigest { get; }
}

public interface IProductionFacilityOutputCapacityContributorRegistry
{
    string RegistryFingerprint { get; }

    IReadOnlyList<ProductionFacilityOutputCapacityContribution>
        CaptureContributions(ProductionFacilityCapacitySubject subject);

    ProductionFacilityOutputCapacityAggregateSnapshot Capture(
        ProductionFacilityCapacitySubject subject);
}

public sealed class ProductionFacilityOutputCapacityContributorRegistry :
    IProductionFacilityOutputCapacityContributorRegistry
{
    public const string Schema =
        "production-facility-output-capacity-contributor-registry@1";
    private readonly IProductionFacilityOutputCapacityContributor[] contributors;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery
        branchMasses;

    public ProductionFacilityOutputCapacityContributorRegistry(
        IEnumerable<IProductionFacilityOutputCapacityContributor> contributors,
        IProductionOutputMaximumMassCapabilitySelector selector)
    {
        branchMasses = new ProductionFacilityOutputCapacityBranchMassAuthority(
            selector ?? throw new ArgumentNullException(nameof(selector)));
        IProductionFacilityOutputCapacityContributor[] source = (contributors
                ?? throw new ArgumentNullException(nameof(contributors)))
            .ToArray();
        if (source.Any(value => value == null
                || !Canonical(value.ContributorId)
                || value.ContractVersion <= 0))
        {
            throw new InvalidOperationException(
                "Facility output capacity contributor metadata is invalid.");
        }
        this.contributors = source
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .ToArray();
        if (this.contributors.Select(value => value.ContributorId)
            .Distinct(StringComparer.Ordinal).Count() != this.contributors.Length)
        {
            throw new InvalidOperationException(
                "Duplicate facility output capacity contributor ID.");
        }
        RegistryFingerprint = CaptureRegistryFingerprint();
    }

    public string RegistryFingerprint { get; }

    public ProductionFacilityOutputCapacityAggregateSnapshot Capture(
        ProductionFacilityCapacitySubject subject)
    {
        IReadOnlyList<ProductionFacilityOutputCapacityContribution>
            contributions = CaptureContributions(subject);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(RegistryFingerprint);
        digest.Append(subject.DefinitionId);
        digest.Append(subject.WorkstationTag);
        digest.Append(subject.OutputBufferCycleCapacity);
        digest.Append(contributions.Count);
        int applicable = 0;
        int branchCount = 0;
        long maximum = 0L;
        string winningContributor = string.Empty;
        string winningBranch = string.Empty;
        foreach (ProductionFacilityOutputCapacityContribution contribution
                 in contributions)
        {
            digest.Append(contribution.SourceDigest);
            if (!contribution.AppliesToFacility)
                continue;
            applicable++;
            branchCount = checked(branchCount + contribution.Branches.Count);
            foreach (ProductionFacilityOutputCapacityBranch branch in contribution.Branches)
            {
                ProductionFacilityOutputCapacityBranchMassSnapshot branchMass =
                    branchMasses.Capture(branch);
                digest.Append(contribution.ContributorId);
                digest.Append(branch.BranchId);
                digest.Append(branchMass.SourceDigest);
                digest.Append(branchMass.MaximumMassGrams);
                if (branchMass.MaximumMassGrams > maximum)
                {
                    maximum = branchMass.MaximumMassGrams;
                    winningContributor = contribution.ContributorId;
                    winningBranch = branch.BranchId;
                }
            }
        }
        digest.Append(applicable);
        digest.Append(branchCount);
        digest.Append(maximum);
        digest.Append(winningContributor);
        digest.Append(winningBranch);
        return new ProductionFacilityOutputCapacityAggregateSnapshot(
            applicable,
            branchCount,
            maximum,
            winningContributor,
            winningBranch,
            digest.ComputeSha256());
    }

    public IReadOnlyList<ProductionFacilityOutputCapacityContribution>
        CaptureContributions(ProductionFacilityCapacitySubject subject)
    {
        ProductionFacilityOutputCapacityContribution[] captured =
            new ProductionFacilityOutputCapacityContribution[contributors.Length];
        for (int index = 0; index < contributors.Length; index++)
        {
            IProductionFacilityOutputCapacityContributor contributor =
                contributors[index];
            ProductionFacilityOutputCapacityContribution contribution =
                contributor.Capture(subject)
                ?? throw new InvalidOperationException(
                    "Facility output capacity contributor returned null: "
                    + contributor.ContributorId);
            if (!string.Equals(
                    contribution.ContributorId,
                    contributor.ContributorId,
                    StringComparison.Ordinal)
                || contribution.ContractVersion != contributor.ContractVersion)
            {
                throw new InvalidOperationException(
                    "Facility output capacity contributor identity drifted: "
                    + contributor.ContributorId);
            }
            captured[index] = contribution;
        }
        return Array.AsReadOnly(captured);
    }

    private string CaptureRegistryFingerprint()
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(contributors.Length);
        foreach (IProductionFacilityOutputCapacityContributor contributor in contributors)
        {
            digest.Append(contributor.ContributorId);
            digest.Append(contributor.ContractVersion);
            digest.Append(contributor.GetType().FullName ?? string.Empty);
        }
        return digest.ComputeSha256();
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class EmptyProductionFacilityOutputCapacityContributorRegistry :
    IProductionFacilityOutputCapacityContributorRegistry
{
    public static readonly EmptyProductionFacilityOutputCapacityContributorRegistry
        Instance = new();
    private EmptyProductionFacilityOutputCapacityContributorRegistry()
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-capacity-contributor-registry@1");
        digest.Append(0);
        RegistryFingerprint = digest.ComputeSha256();
    }

    public string RegistryFingerprint { get; }

    public IReadOnlyList<ProductionFacilityOutputCapacityContribution>
        CaptureContributions(ProductionFacilityCapacitySubject subject) =>
        Array.Empty<ProductionFacilityOutputCapacityContribution>();

    public ProductionFacilityOutputCapacityAggregateSnapshot Capture(
        ProductionFacilityCapacitySubject subject)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-capacity-empty@1");
        digest.Append(RegistryFingerprint);
        digest.Append(subject.DefinitionId);
        digest.Append(subject.WorkstationTag);
        digest.Append(subject.OutputBufferCycleCapacity);
        return new ProductionFacilityOutputCapacityAggregateSnapshot(
            0,
            0,
            0L,
            string.Empty,
            string.Empty,
            digest.ComputeSha256());
    }
}
