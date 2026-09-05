using System;
using System.Collections.Generic;
using System.Linq;

public enum ProductionFacilityOutputEffectKind
{
    None = 0,
    ExternalPhysical = 1,
    StateMutation = 2,
    Service = 3,
    DeclaredNoOutput = 4
}

public enum ProductionFacilityOutputRouteKind
{
    None = 0,
    FacilityBuffer = 1,
    LooseWorld = 2,
    ExactTransform = 3,
    Warehouse = 4,
    CommandEffect = 5,
    InputTransfer = 6,
    NoOutput = 7
}

public sealed class ProductionFacilityOutputDispositionClaim
{
    public ProductionFacilityOutputDispositionClaim(
        string capabilityId,
        ProductionFacilityOutputEffectKind effectKind,
        ProductionFacilityOutputRouteKind routeKind,
        bool executionConnected,
        string reasonCode = "")
    {
        if (!Canonical(capabilityId)
            || !CanonicalOptional(reasonCode)
            || effectKind == ProductionFacilityOutputEffectKind.None
            || routeKind == ProductionFacilityOutputRouteKind.None
            || effectKind == ProductionFacilityOutputEffectKind.DeclaredNoOutput
                != (routeKind == ProductionFacilityOutputRouteKind.NoOutput))
        {
            throw new ArgumentException(
                "Production facility output disposition claim is invalid.");
        }

        CapabilityId = capabilityId;
        EffectKind = effectKind;
        RouteKind = routeKind;
        ExecutionConnected = executionConnected;
        ReasonCode = reasonCode;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-disposition-claim@2");
        digest.Append(CapabilityId);
        digest.Append((int)EffectKind);
        digest.Append((int)RouteKind);
        digest.Append(ExecutionConnected);
        digest.Append(ReasonCode);
        SourceDigest = digest.ComputeSha256();
    }

    public string CapabilityId { get; }
    public ProductionFacilityOutputEffectKind EffectKind { get; }
    public ProductionFacilityOutputRouteKind RouteKind { get; }
    public bool ExecutionConnected { get; }
    public string ReasonCode { get; }
    public string SourceDigest { get; }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool CanonicalOptional(string value) => value != null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class ProductionFacilityOutputDispositionContribution
{
    public ProductionFacilityOutputDispositionContribution(
        string contributorId,
        int contractVersion,
        IReadOnlyList<ProductionFacilityOutputDispositionClaim> claims)
    {
        if (!Canonical(contributorId) || contractVersion <= 0)
        {
            throw new ArgumentException(
                "Production facility output disposition contributor metadata is invalid.");
        }

        ProductionFacilityOutputDispositionClaim[] ordered = (claims
                ?? throw new ArgumentNullException(nameof(claims)))
            .OrderBy(value => value?.CapabilityId, StringComparer.Ordinal)
            .ThenBy(value => value?.EffectKind)
            .ThenBy(value => value?.RouteKind)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(Key).Distinct(StringComparer.Ordinal).Count()
                != ordered.Length)
        {
            throw new InvalidOperationException(
                "Production facility output disposition contribution contains duplicate or null claims.");
        }

        ContributorId = contributorId;
        ContractVersion = contractVersion;
        Claims = Array.AsReadOnly(ordered);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-disposition-contribution@1");
        digest.Append(ContributorId);
        digest.Append(ContractVersion);
        digest.Append(Claims.Count);
        foreach (ProductionFacilityOutputDispositionClaim claim in Claims)
            digest.Append(claim.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string ContributorId { get; }
    public int ContractVersion { get; }
    public IReadOnlyList<ProductionFacilityOutputDispositionClaim> Claims { get; }
    public string SourceDigest { get; }

    internal static string Key(ProductionFacilityOutputDispositionClaim value) =>
        value.CapabilityId + "|" + (int)value.EffectKind + "|" + (int)value.RouteKind;

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IProductionFacilityOutputDispositionContributor
{
    string ContributorId { get; }
    int ContractVersion { get; }

    ProductionFacilityOutputDispositionContribution Capture(
        BuildingSO definition);
}

public sealed class ProductionFacilityOutputDispositionRegistry
{
    private readonly IProductionFacilityOutputDispositionContributor[] contributors;

    public ProductionFacilityOutputDispositionRegistry(
        IEnumerable<IProductionFacilityOutputDispositionContributor> contributors)
    {
        IProductionFacilityOutputDispositionContributor[] source = (contributors
                ?? throw new ArgumentNullException(nameof(contributors)))
            .ToArray();
        if (source.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.ContributorId)
                || !string.Equals(
                    value.ContributorId,
                    value.ContributorId.Trim(),
                    StringComparison.Ordinal)
                || value.ContractVersion <= 0))
        {
            throw new InvalidOperationException(
                "Production facility output disposition contributor metadata is invalid.");
        }

        this.contributors = source
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .ToArray();
        if (this.contributors.Select(value => value.ContributorId)
            .Distinct(StringComparer.Ordinal).Count() != this.contributors.Length)
        {
            throw new InvalidOperationException(
                "Duplicate production facility output disposition contributor ID.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-disposition-registry@1");
        digest.Append(this.contributors.Length);
        foreach (IProductionFacilityOutputDispositionContributor contributor
                 in this.contributors)
        {
            digest.Append(contributor.ContributorId);
            digest.Append(contributor.ContractVersion);
            digest.Append(contributor.GetType().FullName ?? string.Empty);
        }
        RegistryFingerprint = digest.ComputeSha256();
    }

    public string RegistryFingerprint { get; }

    public IReadOnlyList<ProductionFacilityOutputDispositionClaim> Capture(
        BuildingSO definition)
        => CaptureSnapshot(definition).Claims;

    public ProductionFacilityOutputDispositionSnapshot CaptureSnapshot(
        BuildingSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        List<ProductionFacilityOutputDispositionClaim> claims = new();
        List<ProductionFacilityOutputDispositionContribution> contributions = new();
        foreach (IProductionFacilityOutputDispositionContributor contributor in contributors)
        {
            ProductionFacilityOutputDispositionContribution contribution =
                contributor.Capture(definition)
                ?? throw new InvalidOperationException(
                    "Production facility output disposition contributor returned null: "
                    + contributor.ContributorId);
            if (!string.Equals(
                    contribution.ContributorId,
                    contributor.ContributorId,
                    StringComparison.Ordinal)
                || contribution.ContractVersion != contributor.ContractVersion)
            {
                throw new InvalidOperationException(
                    "Production facility output disposition contributor identity drifted: "
                    + contributor.ContributorId);
            }
            contributions.Add(contribution);
            claims.AddRange(contribution.Claims);
        }

        ProductionFacilityOutputDispositionClaim[] ordered = claims
            .OrderBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ThenBy(value => value.EffectKind)
            .ThenBy(value => value.RouteKind)
            .ToArray();
        if (ordered.Select(ProductionFacilityOutputDispositionContribution.Key)
            .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Multiple contributors claimed the same production facility output disposition.");
        }
        return new ProductionFacilityOutputDispositionSnapshot(
            contributions,
            ordered);
    }
}

public sealed class ProductionFacilityOutputDispositionSnapshot
{
    internal ProductionFacilityOutputDispositionSnapshot(
        IReadOnlyList<ProductionFacilityOutputDispositionContribution> contributions,
        IReadOnlyList<ProductionFacilityOutputDispositionClaim> claims)
    {
        ProductionFacilityOutputDispositionContribution[] orderedContributions =
            (contributions ?? throw new ArgumentNullException(nameof(contributions)))
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .ToArray();
        ProductionFacilityOutputDispositionClaim[] orderedClaims = (claims
                ?? throw new ArgumentNullException(nameof(claims)))
            .OrderBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ThenBy(value => value.EffectKind)
            .ThenBy(value => value.RouteKind)
            .ToArray();
        Contributions = Array.AsReadOnly(orderedContributions);
        Claims = Array.AsReadOnly(orderedClaims);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-disposition-snapshot@1");
        digest.Append(Contributions.Count);
        foreach (ProductionFacilityOutputDispositionContribution contribution
                 in Contributions)
        {
            digest.Append(contribution.ContributorId);
            digest.Append(contribution.ContractVersion);
            digest.Append(contribution.SourceDigest);
        }
        digest.Append(Claims.Count);
        foreach (ProductionFacilityOutputDispositionClaim claim in Claims)
            digest.Append(claim.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public IReadOnlyList<ProductionFacilityOutputDispositionContribution>
        Contributions { get; }
    public IReadOnlyList<ProductionFacilityOutputDispositionClaim> Claims { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionFacilityOutputCensusRow
{
    internal ProductionFacilityOutputCensusRow(
        string definitionId,
        string workstationTag,
        int outputBufferCycleCapacity,
        ProductionFacilityWorkstationLaneCapacityProfile workstationLaneProfile,
        string processFluidSourceDigest,
        IReadOnlyList<string> recipeIds,
        IReadOnlyList<string> recipeSourceDigests,
        IReadOnlyList<string> capacityContributorIds,
        IReadOnlyList<string> capacityContributionSourceDigests,
        ProductionSpecialThroughputAggregate specialThroughput,
        ProductionFacilityOutputDispositionSnapshot disposition)
    {
        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        OutputBufferCycleCapacity = outputBufferCycleCapacity;
        ProductionFacilityWorkstationLaneCapacityProfile requiredLaneProfile =
            workstationLaneProfile
            ?? throw new ArgumentNullException(nameof(workstationLaneProfile));
        if (!requiredLaneProfile.IsSpecified)
        {
            throw new InvalidOperationException(
                "Production facility output census lane profile is unspecified.");
        }
        LanePolicy = requiredLaneProfile.Policy;
        ManualWorkLaneCount = requiredLaneProfile.ManualWorkLaneCount;
        AutomaticWorkLaneCount = requiredLaneProfile.AutomaticWorkLaneCount;
        WorkstationLaneSourceDigest = requiredLaneProfile.SourceDigest;
        if (!IsSha256(processFluidSourceDigest))
        {
            throw new InvalidOperationException(
                "Production facility process-fluid source digest is invalid.");
        }
        ProcessFluidSourceDigest = processFluidSourceDigest;
        RecipeIds = Freeze(recipeIds);
        RecipeSourceDigests = FreezeSha256(recipeSourceDigests);
        CapacityContributorIds = Freeze(capacityContributorIds);
        CapacityContributionSourceDigests = FreezeSha256(
            capacityContributionSourceDigests);
        if (RecipeIds.Count != RecipeSourceDigests.Count
            || CapacityContributorIds.Count
                != CapacityContributionSourceDigests.Count)
        {
            throw new InvalidOperationException(
                "Production facility output census identities and source digests are not one-to-one.");
        }
        ProductionSpecialThroughputAggregate requiredSpecialThroughput =
            specialThroughput
            ?? throw new ArgumentNullException(nameof(specialThroughput));
        if (!string.Equals(
                requiredSpecialThroughput.DefinitionId,
                DefinitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                requiredSpecialThroughput.WorkstationTag,
                WorkstationTag,
                StringComparison.Ordinal)
            || !IsSha256(requiredSpecialThroughput.SourceDigest))
        {
            throw new InvalidOperationException(
                "Production facility special-throughput census provenance drifted.");
        }
        SpecialThroughputContributorIds = Freeze(
            requiredSpecialThroughput.Contributions
                .Where(value => value.AppliesToFacility)
                .Select(value => value.ContributorId)
                .ToArray());
        SpecialThroughputCandidates = requiredSpecialThroughput.Candidates;
        SpecialThroughputGaps = requiredSpecialThroughput.Gaps;
        SpecialThroughputSourceDigest = requiredSpecialThroughput.SourceDigest;
        ProductionFacilityOutputDispositionSnapshot requiredDisposition =
            disposition ?? throw new ArgumentNullException(nameof(disposition));
        DispositionClaims = requiredDisposition.Claims;
        DispositionSourceDigest = requiredDisposition.SourceDigest;
        IsAutomaticProducer = RecipeIds.Count > 0
            || CapacityContributorIds.Count > 0;
        IsUnclassified = !IsAutomaticProducer && DispositionClaims.Count == 0;
        HasExecutionOrphan = DispositionClaims.Any(
            value => !value.ExecutionConnected);
        HasContentGap = DispositionClaims.Any(value =>
            value.ReasonCode.StartsWith("content-gap:", StringComparison.Ordinal));
        if (IsAutomaticProducer && DispositionClaims.Any(value =>
                value.EffectKind
                    == ProductionFacilityOutputEffectKind.DeclaredNoOutput))
        {
            throw new InvalidOperationException(
                "Production facility has both an automatic producer and a declared-no-output disposition: "
                + DefinitionId);
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-census-row@3");
        digest.Append(DefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(OutputBufferCycleCapacity);
        digest.Append((int)LanePolicy);
        digest.Append(ManualWorkLaneCount);
        digest.Append(AutomaticWorkLaneCount);
        digest.Append(WorkstationLaneSourceDigest);
        digest.Append(ProcessFluidSourceDigest);
        digest.Append(RecipeIds.Count);
        foreach (string value in RecipeIds)
            digest.Append(value);
        foreach (string value in RecipeSourceDigests)
            digest.Append(value);
        digest.Append(CapacityContributorIds.Count);
        foreach (string value in CapacityContributorIds)
            digest.Append(value);
        foreach (string value in CapacityContributionSourceDigests)
            digest.Append(value);
        digest.Append(SpecialThroughputContributorIds.Count);
        foreach (string value in SpecialThroughputContributorIds)
            digest.Append(value);
        digest.Append(SpecialThroughputCandidates.Count);
        foreach (ProductionSpecialThroughputCandidateSnapshot value in
                 SpecialThroughputCandidates)
            digest.Append(value.SourceDigest);
        digest.Append(SpecialThroughputGaps.Count);
        foreach (ProductionThroughputCoverageGap value in SpecialThroughputGaps)
            digest.Append(value.SourceDigest);
        digest.Append(SpecialThroughputSourceDigest);
        digest.Append(DispositionClaims.Count);
        foreach (ProductionFacilityOutputDispositionClaim value in DispositionClaims)
            digest.Append(value.SourceDigest);
        digest.Append(DispositionSourceDigest);
        digest.Append(IsAutomaticProducer);
        digest.Append(IsUnclassified);
        digest.Append(HasExecutionOrphan);
        digest.Append(HasContentGap);
        SourceDigest = digest.ComputeSha256();
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public int OutputBufferCycleCapacity { get; }
    public ProductionWorkstationLanePolicy LanePolicy { get; }
    public int ManualWorkLaneCount { get; }
    public int AutomaticWorkLaneCount { get; }
    public string WorkstationLaneSourceDigest { get; }
    public string ProcessFluidSourceDigest { get; }
    public IReadOnlyList<string> RecipeIds { get; }
    public IReadOnlyList<string> RecipeSourceDigests { get; }
    public IReadOnlyList<string> CapacityContributorIds { get; }
    public IReadOnlyList<string> CapacityContributionSourceDigests { get; }
    public IReadOnlyList<string> SpecialThroughputContributorIds { get; }
    public IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot>
        SpecialThroughputCandidates { get; }
    public IReadOnlyList<ProductionThroughputCoverageGap> SpecialThroughputGaps
        { get; }
    public string SpecialThroughputSourceDigest { get; }
    public IReadOnlyList<ProductionFacilityOutputDispositionClaim> DispositionClaims { get; }
    public string DispositionSourceDigest { get; }
    public bool IsAutomaticProducer { get; }
    public bool IsUnclassified { get; }
    public bool HasExecutionOrphan { get; }
    public bool HasContentGap { get; }
    public string SourceDigest { get; }

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string> values)
    {
        string[] ordered = (values ?? throw new ArgumentNullException(nameof(values)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Production facility output census identity list is not canonical and unique.");
        }
        return Array.AsReadOnly(ordered);
    }

    private static IReadOnlyList<string> FreezeSha256(
        IReadOnlyList<string> values)
    {
        string[] ordered = (values
                ?? throw new ArgumentNullException(nameof(values)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => !IsSha256(value))
            || ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Production facility output census source digests are not unique lowercase SHA-256 values.");
        }
        return Array.AsReadOnly(ordered);
    }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}

public sealed class ProductionFacilityOutputCensusSnapshot
{
    internal ProductionFacilityOutputCensusSnapshot(
        int rawDefinitionCount,
        int definitionCount,
        int activeDefinitionCount,
        int deprecatedDefinitionCount,
        string definitionScopeSourceDigest,
        string capacityContributorRegistryFingerprint,
        string specialThroughputRegistryFingerprint,
        string dispositionRegistryFingerprint,
        IReadOnlyList<ProductionFacilityOutputCensusRow> rows)
    {
        ProductionFacilityOutputCensusRow[] ordered = (rows
                ?? throw new ArgumentNullException(nameof(rows)))
            .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
            .ToArray();
        if (rawDefinitionCount < definitionCount
            || definitionCount < ordered.Length
            || activeDefinitionCount < 0
            || deprecatedDefinitionCount < 0
            || activeDefinitionCount + deprecatedDefinitionCount != definitionCount
            || !IsSha256(definitionScopeSourceDigest)
            || !IsSha256(capacityContributorRegistryFingerprint)
            || !IsSha256(specialThroughputRegistryFingerprint)
            || !IsSha256(dispositionRegistryFingerprint)
            || ordered.Select(value => value.DefinitionId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Production facility output census snapshot is invalid.");
        }

        RawDefinitionCount = rawDefinitionCount;
        DefinitionCount = definitionCount;
        ActiveDefinitionCount = activeDefinitionCount;
        DeprecatedDefinitionCount = deprecatedDefinitionCount;
        DefinitionScopeSourceDigest = definitionScopeSourceDigest;
        CapacityContributorRegistryFingerprint =
            capacityContributorRegistryFingerprint;
        SpecialThroughputRegistryFingerprint =
            specialThroughputRegistryFingerprint;
        DispositionRegistryFingerprint = dispositionRegistryFingerprint;
        Rows = Array.AsReadOnly(ordered);
        FacilityCount = ordered.Length;
        AutomaticProducerCount = ordered.Count(value => value.IsAutomaticProducer);
        NonProducerCount = FacilityCount - AutomaticProducerCount;
        RecipeOnlyProducerCount = ordered.Count(value => value.RecipeIds.Count > 0
            && value.CapacityContributorIds.Count == 0);
        SpecialProducerCount = ordered.Count(
            value => value.CapacityContributorIds.Count > 0);
        RecipeAndSpecialProducerCount = ordered.Count(value =>
            value.RecipeIds.Count > 0
            && value.CapacityContributorIds.Count > 0);
        UnclassifiedCount = ordered.Count(value => value.IsUnclassified);
        ExecutionOrphanCount = ordered.Count(value => value.HasExecutionOrphan);
        ContentGapCount = ordered.Count(value => value.HasContentGap);
        SpecialFacilityCount = ordered.Count(
            value => value.SpecialThroughputContributorIds.Count > 0);
        SpecialBranchCount = ordered.Sum(value =>
            value.SpecialThroughputCandidates.Count
            + value.SpecialThroughputGaps.Count);
        SpecialCandidateCount = ordered.Sum(
            value => value.SpecialThroughputCandidates.Count);
        SpecialGapCount = ordered.Sum(value => value.SpecialThroughputGaps.Count);
        SpecialUnregisteredGapCount = ordered.Sum(value =>
            value.SpecialThroughputGaps.Count(gap => gap.Reason
                == ProductionThroughputGapReason
                    .SpecialThroughputProviderUnregistered));
        SpecialAuthoredCycleGapCount = ordered.Sum(value =>
            value.SpecialThroughputGaps.Count(gap => gap.Reason
                == ProductionThroughputGapReason.AuthoredCycleAuthorityMissing));
        SpecialExecutionUnsupportedGapCount = ordered.Sum(value =>
            value.SpecialThroughputGaps.Count(gap => gap.Reason
                == ProductionThroughputGapReason.ExecutionAuthorityUnsupported));

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-facility-output-census@3");
        digest.Append(RawDefinitionCount);
        digest.Append(DefinitionCount);
        digest.Append(ActiveDefinitionCount);
        digest.Append(DeprecatedDefinitionCount);
        digest.Append(DefinitionScopeSourceDigest);
        digest.Append(CapacityContributorRegistryFingerprint);
        digest.Append(SpecialThroughputRegistryFingerprint);
        digest.Append(DispositionRegistryFingerprint);
        digest.Append(FacilityCount);
        digest.Append(AutomaticProducerCount);
        digest.Append(NonProducerCount);
        digest.Append(RecipeOnlyProducerCount);
        digest.Append(SpecialProducerCount);
        digest.Append(RecipeAndSpecialProducerCount);
        digest.Append(UnclassifiedCount);
        digest.Append(ExecutionOrphanCount);
        digest.Append(ContentGapCount);
        digest.Append(SpecialFacilityCount);
        digest.Append(SpecialBranchCount);
        digest.Append(SpecialCandidateCount);
        digest.Append(SpecialGapCount);
        digest.Append(SpecialUnregisteredGapCount);
        digest.Append(SpecialAuthoredCycleGapCount);
        digest.Append(SpecialExecutionUnsupportedGapCount);
        foreach (ProductionFacilityOutputCensusRow value in Rows)
            digest.Append(value.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public int RawDefinitionCount { get; }
    public int DefinitionCount { get; }
    public int ActiveDefinitionCount { get; }
    public int DeprecatedDefinitionCount { get; }
    public string DefinitionScopeSourceDigest { get; }
    public string CapacityContributorRegistryFingerprint { get; }
    public string SpecialThroughputRegistryFingerprint { get; }
    public string DispositionRegistryFingerprint { get; }
    public int FacilityCount { get; }
    public int AutomaticProducerCount { get; }
    public int NonProducerCount { get; }
    public int RecipeOnlyProducerCount { get; }
    public int SpecialProducerCount { get; }
    public int RecipeAndSpecialProducerCount { get; }
    public int UnclassifiedCount { get; }
    public int ExecutionOrphanCount { get; }
    public int ContentGapCount { get; }
    public int SpecialFacilityCount { get; }
    public int SpecialBranchCount { get; }
    public int SpecialCandidateCount { get; }
    public int SpecialGapCount { get; }
    public int SpecialUnregisteredGapCount { get; }
    public int SpecialAuthoredCycleGapCount { get; }
    public int SpecialExecutionUnsupportedGapCount { get; }
    public IReadOnlyList<ProductionFacilityOutputCensusRow> Rows { get; }
    public string SourceDigest { get; }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}

public interface IProductionFacilityOutputCensusQuery
{
    ProductionFacilityOutputCensusSnapshot Capture(
        IReadOnlyList<BuildingSO> definitions);
}

public sealed class ProductionFacilityOutputCensus :
    IProductionFacilityOutputCensusQuery
{
    private readonly ProductionRecipeSO[] recipes;
    private readonly IProductionFacilityOutputCapacityContributorRegistry
        capacityContributors;
    private readonly ProductionSpecialThroughputContributorRegistry
        specialThroughputContributors;
    private readonly ProductionFacilityOutputDispositionRegistry dispositions;

    public ProductionFacilityOutputCensus(
        IEnumerable<ProductionRecipeSO> recipes,
        IProductionFacilityOutputCapacityContributorRegistry capacityContributors,
        ProductionSpecialThroughputContributorRegistry specialThroughputContributors,
        ProductionFacilityOutputDispositionRegistry dispositions)
    {
        this.recipes = (recipes ?? throw new ArgumentNullException(nameof(recipes)))
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        if (this.recipes.Select(value => value.RecipeId)
            .Distinct(StringComparer.Ordinal).Count() != this.recipes.Length)
        {
            throw new InvalidOperationException(
                "Production facility output census received duplicate recipe IDs.");
        }

        this.capacityContributors = capacityContributors
            ?? throw new ArgumentNullException(nameof(capacityContributors));
        this.specialThroughputContributors = specialThroughputContributors
            ?? throw new ArgumentNullException(nameof(specialThroughputContributors));
        this.dispositions = dispositions
            ?? throw new ArgumentNullException(nameof(dispositions));
    }

    public ProductionFacilityOutputCensusSnapshot Capture(
        IReadOnlyList<BuildingSO> definitions)
    {
        BuildingSO[] all = (definitions
                ?? throw new ArgumentNullException(nameof(definitions)))
            .Where(value => value != null)
            .ToArray();
        BuildingSO[] authored = all
            .Where(value => value.id >= 0)
            .OrderBy(ProductionFacilityDefinitionIdentity.Resolve,
                StringComparer.Ordinal)
            .ToArray();
        if (authored.Select(ProductionFacilityDefinitionIdentity.Resolve)
            .Distinct(StringComparer.Ordinal).Count() != authored.Length)
        {
            throw new InvalidOperationException(
                "Production facility output census received duplicate authored building definition IDs.");
        }
        int deprecatedCount = authored.Count(
            value => value.IsDeprecatedCompatibilityAsset);
        CanonicalSemanticDigestBuilder scopeDigest = new();
        scopeDigest.Append("production-facility-output-census-definition-scope@1");
        scopeDigest.Append(all.Length);
        scopeDigest.Append(authored.Length);
        scopeDigest.Append(authored.Length - deprecatedCount);
        scopeDigest.Append(deprecatedCount);
        foreach (BuildingSO definition in authored)
        {
            scopeDigest.Append(
                ProductionFacilityDefinitionIdentity.Resolve(definition));
            scopeDigest.Append(definition.IsDeprecatedCompatibilityAsset);
        }

        List<ProductionFacilityOutputCensusRow> rows = new();
        foreach (BuildingSO definition in authored)
        {
            BuildingProductionWorkstationAbility workstation =
                definition.GetProductionWorkstationAbility();
            BuildingProductionBufferAbility buffer =
                definition.GetProductionBufferAbility();
            if ((workstation == null) != (buffer == null))
            {
                throw new InvalidOperationException(
                    "Production facility has only one half of the workstation/buffer pair: "
                    + ProductionFacilityDefinitionIdentity.Resolve(definition));
            }
            if (workstation == null)
                continue;
            if (buffer.physicalOutputBufferCycleCapacity is < 2 or > 4)
            {
                throw new InvalidOperationException(
                    "Production facility output cycle capacity is outside 2..4: "
                    + ProductionFacilityDefinitionIdentity.Resolve(definition));
            }

            string definitionId = ProductionFacilityDefinitionIdentity.Resolve(definition);
            ProductionFacilityCapacitySubject subject = new(
                (BuildingInstanceId)("building:audit-output-census:" + definitionId),
                UnityEngine.Vector2Int.zero,
                definitionId,
                workstation.WorkstationTag,
                buffer.physicalOutputBufferCycleCapacity,
                ProductionFacilityCapacitySubjectAdapter
                    .CaptureWorkstationLaneProfile(definition),
                ProductionFacilityCapacitySubjectAdapter.CaptureProcessFluidProfile(
                    definition));
            ProductionRecipeSO[] matchingRecipes = recipes
                .Where(value => string.Equals(
                    value.WorkstationTag,
                    subject.WorkstationTag,
                    StringComparison.Ordinal))
                .ToArray();
            ProductionFacilityOutputCapacityContribution[] allCapacity =
                capacityContributors.CaptureContributions(subject).ToArray();
            ProductionFacilityOutputCapacityContribution[] applicable = allCapacity
                .Where(value => value.AppliesToFacility)
                .ToArray();
            ProductionSpecialThroughputAggregate specialThroughput =
                specialThroughputContributors.Capture(
                    new ProductionSpecialThroughputFacilityContext(
                        subject,
                        allCapacity));

            rows.Add(new ProductionFacilityOutputCensusRow(
                definitionId,
                subject.WorkstationTag,
                subject.OutputBufferCycleCapacity,
                subject.WorkstationLaneProfile,
                subject.ProcessFluidProfile.SourceDigest,
                matchingRecipes.Select(value => value.RecipeId).ToArray(),
                matchingRecipes.Select(ProductionRecipeSemanticDigest.Capture)
                    .ToArray(),
                applicable.Select(value => value.ContributorId).ToArray(),
                applicable.Select(value => value.SourceDigest).ToArray(),
                specialThroughput,
                dispositions.CaptureSnapshot(definition)));
        }

        return new ProductionFacilityOutputCensusSnapshot(
            all.Length,
            authored.Length,
            authored.Length - deprecatedCount,
            deprecatedCount,
            scopeDigest.ComputeSha256(),
            capacityContributors.RegistryFingerprint,
            specialThroughputContributors.RegistryFingerprint,
            dispositions.RegistryFingerprint,
            rows);
    }
}

public sealed class CoreAbilityFacilityOutputDispositionContributor :
    IProductionFacilityOutputDispositionContributor
{
    public const string Id = "facility-output-disposition:core-abilities";
    public string ContributorId => Id;
    public int ContractVersion => 1;

    public ProductionFacilityOutputDispositionContribution Capture(
        BuildingSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        List<ProductionFacilityOutputDispositionClaim> claims = new();
        if (definition.GetAbility<BuildingCookingAbility>() != null)
        {
            claims.Add(Claim(
                "building-ability:cooking",
                ProductionFacilityOutputEffectKind.ExternalPhysical,
                ProductionFacilityOutputRouteKind.LooseWorld));
        }
        if (definition.GetAbility<BuildingButcherAbility>() != null)
        {
            claims.Add(Claim(
                "building-ability:butcher",
                ProductionFacilityOutputEffectKind.ExternalPhysical,
                ProductionFacilityOutputRouteKind.ExactTransform));
        }
        if (definition.GetAbility<BuildingProductionAbility>() != null)
        {
            claims.Add(Claim(
                "building-ability:production",
                ProductionFacilityOutputEffectKind.ExternalPhysical,
                ProductionFacilityOutputRouteKind.Warehouse));
        }
        if (definition.GetAbility<BuildingCropPlotAbility>() != null)
        {
            claims.Add(Claim(
                "building-ability:crop-plot",
                ProductionFacilityOutputEffectKind.ExternalPhysical,
                ProductionFacilityOutputRouteKind.LooseWorld));
        }
        if (definition.GetAbility<BuildingRecreationalSubstanceServiceAbility>()
                != null
            || definition.GetAbility<BuildingMercenaryHiringAbility>() != null)
        {
            claims.Add(Claim(
                "building-ability:recreational-or-hiring-service",
                ProductionFacilityOutputEffectKind.Service,
                ProductionFacilityOutputRouteKind.CommandEffect));
        }
        return new ProductionFacilityOutputDispositionContribution(
            ContributorId,
            ContractVersion,
            claims);
    }

    private static ProductionFacilityOutputDispositionClaim Claim(
        string capabilityId,
        ProductionFacilityOutputEffectKind effect,
        ProductionFacilityOutputRouteKind route) =>
        new(capabilityId, effect, route, true);
}

public sealed class AuthoredFacilityOutputDispositionContributor :
    IProductionFacilityOutputDispositionContributor
{
    public const string Id = "facility-output-disposition:authored";
    public string ContributorId => Id;
    public int ContractVersion => 1;

    public ProductionFacilityOutputDispositionContribution Capture(
        BuildingSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        BuildingProductionOutputDispositionAbility ability =
            definition.GetProductionOutputDispositionAbility();
        if (ability == null)
        {
            return new ProductionFacilityOutputDispositionContribution(
                ContributorId,
                ContractVersion,
                Array.Empty<ProductionFacilityOutputDispositionClaim>());
        }
        if (ability.dispositionKind
            != ProductionOutputDispositionAuthoringKind.DeclaredNoOutput)
        {
            throw new InvalidOperationException(
                "Unsupported authored production output disposition kind.");
        }
        return new ProductionFacilityOutputDispositionContribution(
            ContributorId,
            ContractVersion,
            new[]
            {
                new ProductionFacilityOutputDispositionClaim(
                    ability.OwnerCapabilityId,
                    ProductionFacilityOutputEffectKind.DeclaredNoOutput,
                    ProductionFacilityOutputRouteKind.NoOutput,
                    true,
                    ability.ReasonCode)
            });
    }
}

public interface IResearchFacilityCommandExecutionConnectionQuery
{
    bool IsConnected(ResearchFacilityCommandKind command);
}

public sealed class ResearchCommandFacilityOutputDispositionContributor :
    IProductionFacilityOutputDispositionContributor
{
    public const string Id = "facility-output-disposition:research-command";
    private readonly IResearchFacilityCommandExecutionConnectionQuery execution;

    public ResearchCommandFacilityOutputDispositionContributor(
        IResearchFacilityCommandExecutionConnectionQuery execution)
    {
        this.execution = execution
            ?? throw new ArgumentNullException(nameof(execution));
    }

    public string ContributorId => Id;
    public int ContractVersion => 1;

    public ProductionFacilityOutputDispositionContribution Capture(
        BuildingSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        ResearchFacilityCommandKind command = definition.ResearchFacilityCommand;
        if (command == ResearchFacilityCommandKind.None)
        {
            return new ProductionFacilityOutputDispositionContribution(
                ContributorId,
                ContractVersion,
                Array.Empty<ProductionFacilityOutputDispositionClaim>());
        }
        if (!Enum.IsDefined(typeof(ResearchFacilityCommandKind), command))
        {
            throw new InvalidOperationException(
                "Production facility has an unknown research command kind.");
        }
        return new ProductionFacilityOutputDispositionContribution(
            ContributorId,
            ContractVersion,
            new[]
            {
                new ProductionFacilityOutputDispositionClaim(
                    "research-facility-command:" + command,
                    ProductionFacilityOutputEffectKind.Service,
                    ProductionFacilityOutputRouteKind.CommandEffect,
                    execution.IsConnected(command))
            });
    }
}
