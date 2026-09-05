using System;
using System.Collections.Generic;
using System.Linq;

public enum ProductionOutputClearanceMeasurementSourceKind
{
    Recipe = 1,
    CapacityContributor = 2
}

public enum ProductionOutputClearanceMeasurementGapReason
{
    None = 0,
    MeasurementCapabilityUnregistered = 1,
    ExecutionAuthorityUnsupported = 2,
    PhysicalPayloadUnsupported = 3
}

/// <summary>
/// One reachable, single-completion physical footprint. It contains only
/// immutable execution identities and physical capability IDs; it never
/// fabricates an item stack or erases a unique component payload.
/// </summary>
public sealed class ProductionOutputClearanceMeasurementSourceBranch
{
    public ProductionOutputClearanceMeasurementSourceBranch(
        ProductionOutputClearanceMeasurementSourceKind sourceKind,
        string sourceCapabilityId,
        int sourceCapabilityVersion,
        string producerId,
        string branchId,
        long maximumSingleCompletionMassGrams,
        IReadOnlyList<string> outputCapabilityIds,
        string upstreamSourceDigest)
    {
        RequireCanonical(sourceCapabilityId, nameof(sourceCapabilityId));
        RequireCanonical(producerId, nameof(producerId));
        RequireCanonical(branchId, nameof(branchId));
        RequireDigest(upstreamSourceDigest, nameof(upstreamSourceDigest));
        if (!Enum.IsDefined(typeof(
                ProductionOutputClearanceMeasurementSourceKind), sourceKind)
            || sourceCapabilityVersion <= 0
            || maximumSingleCompletionMassGrams <= 0L)
        {
            throw new ArgumentException(
                "Clearance measurement source branch is invalid.");
        }

        string[] orderedCapabilities = (outputCapabilityIds
                ?? throw new ArgumentNullException(nameof(outputCapabilityIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (orderedCapabilities.Length == 0
            || orderedCapabilities.Any(value => !Canonical(value))
            || orderedCapabilities.Distinct(StringComparer.Ordinal).Count()
                != orderedCapabilities.Length)
        {
            throw new InvalidOperationException(
                "Clearance measurement output capabilities are empty, invalid, or duplicated.");
        }

        SourceKind = sourceKind;
        SourceCapabilityId = sourceCapabilityId;
        SourceCapabilityVersion = sourceCapabilityVersion;
        ProducerId = producerId;
        BranchId = branchId;
        MaximumSingleCompletionMassGrams =
            maximumSingleCompletionMassGrams;
        OutputCapabilityIds = Array.AsReadOnly(orderedCapabilities);
        UpstreamSourceDigest = upstreamSourceDigest;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-source@1");
        digest.Append((int)SourceKind);
        digest.Append(SourceCapabilityId);
        digest.Append(SourceCapabilityVersion);
        digest.Append(ProducerId);
        digest.Append(BranchId);
        digest.Append(MaximumSingleCompletionMassGrams);
        digest.Append(OutputCapabilityIds.Count);
        foreach (string capabilityId in OutputCapabilityIds)
            digest.Append(capabilityId);
        digest.Append(UpstreamSourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementSourceKind SourceKind { get; }
    public string SourceCapabilityId { get; }
    public int SourceCapabilityVersion { get; }
    public string ProducerId { get; }
    public string BranchId { get; }
    public long MaximumSingleCompletionMassGrams { get; }
    public IReadOnlyList<string> OutputCapabilityIds { get; }
    public string UpstreamSourceDigest { get; }
    public string SourceDigest { get; }

    internal static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static void RequireCanonical(string value, string parameterName)
    {
        if (!Canonical(value))
            throw new ArgumentException(
                "A canonical non-empty identifier is required.",
                parameterName);
    }

    internal static void RequireDigest(string value, string parameterName)
    {
        if (value == null
            || value.Length != 64
            || value.Any(character => !(character is >= '0' and <= '9'
                || character is >= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.",
                parameterName);
        }
    }
}

public sealed class ProductionOutputClearanceRecipeMeasurementBranch
{
    public ProductionOutputClearanceRecipeMeasurementBranch(
        string recipeId,
        string branchId,
        long maximumSingleCompletionMassGrams,
        IReadOnlyList<string> outputCapabilityIds,
        string upstreamSourceDigest)
    {
        Source = new ProductionOutputClearanceMeasurementSourceBranch(
            ProductionOutputClearanceMeasurementSourceKind.Recipe,
            ProductionOutputClearanceMeasurementPlanRegistry.RecipeSourceCapabilityId,
            ProductionOutputClearanceMeasurementPlanRegistry
                .RecipeSourceCapabilityVersion,
            recipeId,
            branchId,
            maximumSingleCompletionMassGrams,
            outputCapabilityIds,
            upstreamSourceDigest);
    }

    public ProductionOutputClearanceMeasurementSourceBranch Source { get; }
}

public sealed class ProductionOutputClearanceMeasurementFacilityContext
{
    public ProductionOutputClearanceMeasurementFacilityContext(
        string definitionId,
        string workstationTag,
        IReadOnlyList<ProductionOutputClearanceRecipeMeasurementBranch>
            recipeBranches,
        IReadOnlyList<ProductionFacilityOutputCapacityContribution>
            capacityContributions)
    {
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        ProductionOutputClearanceRecipeMeasurementBranch[] orderedRecipes =
            (recipeBranches
                ?? throw new ArgumentNullException(nameof(recipeBranches)))
            .OrderBy(value => value?.Source.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value?.Source.BranchId, StringComparer.Ordinal)
            .ThenBy(value => value?.Source.SourceDigest, StringComparer.Ordinal)
            .ToArray();
        ProductionFacilityOutputCapacityContribution[] orderedCapacity =
            (capacityContributions
                ?? throw new ArgumentNullException(nameof(capacityContributions)))
            .OrderBy(value => value?.ContributorId, StringComparer.Ordinal)
            .ToArray();
        if (orderedRecipes.Any(value => value == null)
            || orderedRecipes.Select(value => value.Source.SourceDigest)
                .Distinct(StringComparer.Ordinal).Count()
                != orderedRecipes.Length
            || orderedCapacity.Any(value => value == null)
            || orderedCapacity.Select(value => value.ContributorId)
                .Distinct(StringComparer.Ordinal).Count()
                != orderedCapacity.Length
            || orderedRecipes.Length == 0
                && !orderedCapacity.Any(value => value.AppliesToFacility))
        {
            throw new InvalidOperationException(
                "Clearance measurement facility sources are empty, null, or duplicated.");
        }

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        RecipeBranches = Array.AsReadOnly(orderedRecipes);
        CapacityContributions = Array.AsReadOnly(orderedCapacity);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-context@1");
        digest.Append(DefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(RecipeBranches.Count);
        foreach (ProductionOutputClearanceRecipeMeasurementBranch branch in
                 RecipeBranches)
            digest.Append(branch.Source.SourceDigest);
        digest.Append(CapacityContributions.Count);
        foreach (ProductionFacilityOutputCapacityContribution contribution in
                 CapacityContributions)
            digest.Append(contribution.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public IReadOnlyList<ProductionOutputClearanceRecipeMeasurementBranch>
        RecipeBranches { get; }
    public IReadOnlyList<ProductionFacilityOutputCapacityContribution>
        CapacityContributions { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceMeasurementCandidate
{
    internal ProductionOutputClearanceMeasurementCandidate(
        ProductionOutputClearanceMeasurementSourceBranch source,
        string measurementCapabilityId,
        string contributorId,
        int contributorContractVersion)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            measurementCapabilityId,
            nameof(measurementCapabilityId));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            contributorId,
            nameof(contributorId));
        if (contributorContractVersion <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(contributorContractVersion));
        MeasurementCapabilityId = measurementCapabilityId;
        ContributorId = contributorId;
        ContributorContractVersion = contributorContractVersion;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-candidate@1");
        digest.Append(Source.SourceDigest);
        digest.Append(MeasurementCapabilityId);
        digest.Append(ContributorId);
        digest.Append(ContributorContractVersion);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementSourceBranch Source { get; }
    public string MeasurementCapabilityId { get; }
    public string ContributorId { get; }
    public int ContributorContractVersion { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceMeasurementGap
{
    internal ProductionOutputClearanceMeasurementGap(
        ProductionOutputClearanceMeasurementSourceBranch source,
        ProductionOutputClearanceMeasurementGapReason reason,
        string detail)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        if (!Enum.IsDefined(typeof(
                ProductionOutputClearanceMeasurementGapReason), reason)
            || reason == ProductionOutputClearanceMeasurementGapReason.None
            || detail == null
            || !string.Equals(detail, detail.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Clearance measurement coverage gap is invalid.");
        }
        Reason = reason;
        Detail = detail;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-gap@1");
        digest.Append(Source.SourceDigest);
        digest.Append((int)Reason);
        digest.Append(Detail);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementSourceBranch Source { get; }
    public ProductionOutputClearanceMeasurementGapReason Reason { get; }
    public string Detail { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceMeasurementContribution
{
    private ProductionOutputClearanceMeasurementContribution(
        ProductionOutputClearanceMeasurementCandidate candidate,
        ProductionOutputClearanceMeasurementGap gap)
    {
        if ((candidate == null) == (gap == null))
            throw new ArgumentException(
                "Clearance measurement contribution must contain exactly one candidate or gap.");
        Candidate = candidate;
        Gap = gap;
    }

    public ProductionOutputClearanceMeasurementCandidate Candidate { get; }
    public ProductionOutputClearanceMeasurementGap Gap { get; }
    public bool IsSupported => Candidate != null;

    public static ProductionOutputClearanceMeasurementContribution Supported(
        ProductionOutputClearanceMeasurementSourceBranch source,
        string measurementCapabilityId,
        string contributorId,
        int contributorContractVersion) => new(
        new ProductionOutputClearanceMeasurementCandidate(
            source,
            measurementCapabilityId,
            contributorId,
            contributorContractVersion),
        null);

    public static ProductionOutputClearanceMeasurementContribution Unsupported(
        ProductionOutputClearanceMeasurementSourceBranch source,
        ProductionOutputClearanceMeasurementGapReason reason,
        string detail) => new(
        null,
        new ProductionOutputClearanceMeasurementGap(source, reason, detail));
}

public interface IProductionOutputClearanceMeasurementPlanContributor
{
    string ContributorId { get; }
    int ContractVersion { get; }
    string SourceCapabilityId { get; }
    int SourceCapabilityVersion { get; }
    string MeasurementCapabilityId { get; }

    ProductionOutputClearanceMeasurementContribution Capture(
        ProductionOutputClearanceMeasurementSourceBranch source);
}

/// <summary>
/// Declarative binding from an existing producer capability to the concrete
/// measurement execution capability that knows how to run that producer.
/// Registering a binding does not convert its payload to a generic item.
/// </summary>
public sealed class ProductionOutputClearanceMeasurementPlanContributor :
    IProductionOutputClearanceMeasurementPlanContributor
{
    public ProductionOutputClearanceMeasurementPlanContributor(
        string contributorId,
        int contractVersion,
        string sourceCapabilityId,
        int sourceCapabilityVersion,
        string measurementCapabilityId)
    {
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            contributorId,
            nameof(contributorId));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            sourceCapabilityId,
            nameof(sourceCapabilityId));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            measurementCapabilityId,
            nameof(measurementCapabilityId));
        if (contractVersion <= 0 || sourceCapabilityVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        ContributorId = contributorId;
        ContractVersion = contractVersion;
        SourceCapabilityId = sourceCapabilityId;
        SourceCapabilityVersion = sourceCapabilityVersion;
        MeasurementCapabilityId = measurementCapabilityId;
    }

    public string ContributorId { get; }
    public int ContractVersion { get; }
    public string SourceCapabilityId { get; }
    public int SourceCapabilityVersion { get; }
    public string MeasurementCapabilityId { get; }

    public ProductionOutputClearanceMeasurementContribution Capture(
        ProductionOutputClearanceMeasurementSourceBranch source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (!string.Equals(
                source.SourceCapabilityId,
                SourceCapabilityId,
                StringComparison.Ordinal)
            || source.SourceCapabilityVersion != SourceCapabilityVersion)
        {
            throw new InvalidOperationException(
                "Clearance measurement contributor received an unowned source capability.");
        }
        return ProductionOutputClearanceMeasurementContribution.Supported(
            source,
            MeasurementCapabilityId,
            ContributorId,
            ContractVersion);
    }
}

public sealed class ProductionOutputClearanceMeasurementPlan
{
    internal ProductionOutputClearanceMeasurementPlan(
        string definitionId,
        string workstationTag,
        IReadOnlyList<ProductionOutputClearanceMeasurementCandidate> candidates,
        string registryFingerprint,
        string contextSourceDigest)
    {
        ProductionOutputClearanceMeasurementCandidate[] ordered = (candidates
                ?? throw new ArgumentNullException(nameof(candidates)))
            .OrderByDescending(value =>
                value.Source.MaximumSingleCompletionMassGrams)
            .ThenBy(value => value.MeasurementCapabilityId,
                StringComparer.Ordinal)
            .ThenBy(value => value.Source.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value.Source.BranchId, StringComparer.Ordinal)
            .ThenBy(value => value.Source.SourceDigest, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Select(value => value.Source.SourceDigest)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Clearance measurement plan candidates are empty or duplicated.");
        }
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            registryFingerprint,
            nameof(registryFingerprint));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            contextSourceDigest,
            nameof(contextSourceDigest));

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        Candidates = Array.AsReadOnly(ordered);
        Winner = ordered[0];
        ContextSourceDigest = contextSourceDigest;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-plan@1");
        digest.Append(DefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(registryFingerprint);
        digest.Append(contextSourceDigest);
        digest.Append(Candidates.Count);
        foreach (ProductionOutputClearanceMeasurementCandidate candidate in
                 Candidates)
            digest.Append(candidate.SourceDigest);
        digest.Append(Winner.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public IReadOnlyList<ProductionOutputClearanceMeasurementCandidate>
        Candidates { get; }
    public ProductionOutputClearanceMeasurementCandidate Winner { get; }
    public string ContextSourceDigest { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceMeasurementPlanResult
{
    internal ProductionOutputClearanceMeasurementPlanResult(
        ProductionOutputClearanceMeasurementPlan plan,
        IReadOnlyList<ProductionOutputClearanceMeasurementGap> gaps,
        string sourceDigest)
    {
        ProductionOutputClearanceMeasurementGap[] orderedGaps = (gaps
                ?? throw new ArgumentNullException(nameof(gaps)))
            .OrderBy(value => value.Source.SourceCapabilityId,
                StringComparer.Ordinal)
            .ThenBy(value => value.Source.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value.Source.BranchId, StringComparer.Ordinal)
            .ThenBy(value => (int)value.Reason)
            .ToArray();
        if ((plan == null) == (orderedGaps.Length == 0))
            throw new InvalidOperationException(
                "A clearance measurement result must contain either one complete plan or typed gaps.");
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        Plan = plan;
        Gaps = Array.AsReadOnly(orderedGaps);
        SourceDigest = sourceDigest;
    }

    public ProductionOutputClearanceMeasurementPlan Plan { get; }
    public IReadOnlyList<ProductionOutputClearanceMeasurementGap> Gaps { get; }
    public string SourceDigest { get; }
    public bool IsComplete => Plan != null;
}

public interface IProductionOutputClearanceMeasurementPlanQuery
{
    string RegistryFingerprint { get; }

    ProductionOutputClearanceMeasurementPlanResult Capture(
        ProductionOutputClearanceMeasurementFacilityContext facility);
}

public sealed class ProductionOutputClearanceMeasurementPlanRegistry :
    IProductionOutputClearanceMeasurementPlanQuery
{
    public const string Schema =
        "production-output-clearance-measurement-plan-registry@1";
    public const string RecipeSourceCapabilityId =
        "production-output-clearance-source:recipe";
    public const int RecipeSourceCapabilityVersion = 1;

    private readonly Dictionary<string,
        IProductionOutputClearanceMeasurementPlanContributor> contributors;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery
        branchMasses;

    public ProductionOutputClearanceMeasurementPlanRegistry(
        IEnumerable<IProductionOutputClearanceMeasurementPlanContributor>
            contributors,
        IProductionFacilityOutputCapacityBranchMassQuery branchMasses)
    {
        this.branchMasses = branchMasses
            ?? throw new ArgumentNullException(nameof(branchMasses));
        IProductionOutputClearanceMeasurementPlanContributor[] ordered =
            (contributors ?? throw new ArgumentNullException(nameof(contributors)))
            .OrderBy(value => value?.SourceCapabilityId, StringComparer.Ordinal)
            .ThenBy(value => value?.ContributorId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null
                || !ProductionOutputClearanceMeasurementSourceBranch.Canonical(
                    value.ContributorId)
                || value.ContractVersion <= 0
                || !ProductionOutputClearanceMeasurementSourceBranch.Canonical(
                    value.SourceCapabilityId)
                || value.SourceCapabilityVersion <= 0
                || !ProductionOutputClearanceMeasurementSourceBranch.Canonical(
                    value.MeasurementCapabilityId))
            || ordered.Select(value => value.ContributorId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || ordered.Select(value => value.SourceCapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Clearance measurement contributors are invalid or duplicated.");
        }
        this.contributors = ordered.ToDictionary(
            value => value.SourceCapabilityId,
            value => value,
            StringComparer.Ordinal);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ordered.Length);
        foreach (IProductionOutputClearanceMeasurementPlanContributor contributor
                 in ordered)
        {
            digest.Append(contributor.SourceCapabilityId);
            digest.Append(contributor.SourceCapabilityVersion);
            digest.Append(contributor.ContributorId);
            digest.Append(contributor.ContractVersion);
            digest.Append(contributor.MeasurementCapabilityId);
            digest.Append(contributor.GetType().FullName ?? string.Empty);
        }
        RegistryFingerprint = digest.ComputeSha256();
    }

    public string RegistryFingerprint { get; }

    public ProductionOutputClearanceMeasurementPlanResult Capture(
        ProductionOutputClearanceMeasurementFacilityContext facility)
    {
        if (facility == null) throw new ArgumentNullException(nameof(facility));
        List<ProductionOutputClearanceMeasurementSourceBranch> sources = new();
        sources.AddRange(facility.RecipeBranches.Select(value => value.Source));
        foreach (ProductionFacilityOutputCapacityContribution contribution in
                 facility.CapacityContributions.Where(value =>
                     value.AppliesToFacility))
        {
            foreach (ProductionFacilityOutputCapacityBranch branch in
                     contribution.Branches)
            {
                ProductionFacilityOutputCapacityBranchMassSnapshot mass =
                    branchMasses.Capture(branch)
                    ?? throw new InvalidOperationException(
                        "Facility output branch-mass authority returned null.");
                if (!string.Equals(
                        mass.BranchId,
                        branch.BranchId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Facility output branch-mass authority drifted.");
                }
                sources.Add(new ProductionOutputClearanceMeasurementSourceBranch(
                    ProductionOutputClearanceMeasurementSourceKind
                        .CapacityContributor,
                    contribution.ContributorId,
                    contribution.ContractVersion,
                    contribution.ContributorId,
                    branch.BranchId,
                    mass.MaximumMassGrams,
                    branch.Outputs.Select(value => value.CapabilityId)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    mass.SourceDigest));
            }
        }
        ProductionOutputClearanceMeasurementSourceBranch[] orderedSources =
            sources
                .OrderBy(value => value.SourceCapabilityId,
                    StringComparer.Ordinal)
                .ThenBy(value => value.ProducerId, StringComparer.Ordinal)
                .ThenBy(value => value.BranchId, StringComparer.Ordinal)
                .ThenBy(value => value.SourceDigest, StringComparer.Ordinal)
                .ToArray();
        if (orderedSources.Length == 0
            || orderedSources.Select(value => value.SourceDigest)
                .Distinct(StringComparer.Ordinal).Count()
                != orderedSources.Length)
        {
            throw new InvalidOperationException(
                "Clearance measurement source coverage is empty or duplicated.");
        }

        List<ProductionOutputClearanceMeasurementCandidate> candidates = new();
        List<ProductionOutputClearanceMeasurementGap> gaps = new();
        foreach (ProductionOutputClearanceMeasurementSourceBranch source in
                 orderedSources)
        {
            if (!contributors.TryGetValue(
                    source.SourceCapabilityId,
                    out IProductionOutputClearanceMeasurementPlanContributor
                        contributor))
            {
                gaps.Add(new ProductionOutputClearanceMeasurementGap(
                    source,
                    ProductionOutputClearanceMeasurementGapReason
                        .MeasurementCapabilityUnregistered,
                    "no measurement capability owns this producer capability"));
                continue;
            }
            ProductionOutputClearanceMeasurementContribution result =
                contributor.Capture(source)
                ?? throw new InvalidOperationException(
                    "Clearance measurement contributor returned null: "
                    + contributor.ContributorId);
            if (result.IsSupported)
            {
                ProductionOutputClearanceMeasurementCandidate candidate =
                    result.Candidate;
                if (!ReferenceEquals(candidate.Source, source)
                    || !string.Equals(candidate.ContributorId,
                        contributor.ContributorId, StringComparison.Ordinal)
                    || candidate.ContributorContractVersion
                        != contributor.ContractVersion
                    || !string.Equals(candidate.MeasurementCapabilityId,
                        contributor.MeasurementCapabilityId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Clearance measurement contributor candidate identity drifted.");
                }
                candidates.Add(candidate);
            }
            else
            {
                if (!ReferenceEquals(result.Gap.Source, source))
                    throw new InvalidOperationException(
                        "Clearance measurement contributor gap identity drifted.");
                gaps.Add(result.Gap);
            }
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(RegistryFingerprint);
        digest.Append(facility.SourceDigest);
        digest.Append(orderedSources.Length);
        foreach (ProductionOutputClearanceMeasurementSourceBranch source in
                 orderedSources)
            digest.Append(source.SourceDigest);
        digest.Append(candidates.Count);
        foreach (ProductionOutputClearanceMeasurementCandidate candidate in
                 candidates.OrderBy(value => value.SourceDigest,
                     StringComparer.Ordinal))
            digest.Append(candidate.SourceDigest);
        digest.Append(gaps.Count);
        foreach (ProductionOutputClearanceMeasurementGap gap in gaps
                     .OrderBy(value => value.SourceDigest,
                         StringComparer.Ordinal))
            digest.Append(gap.SourceDigest);

        ProductionOutputClearanceMeasurementPlan plan = gaps.Count == 0
            ? new ProductionOutputClearanceMeasurementPlan(
                facility.DefinitionId,
                facility.WorkstationTag,
                candidates,
                RegistryFingerprint,
                facility.SourceDigest)
            : null;
        if (gaps.Count == 0)
            digest.Append(plan.SourceDigest);
        return new ProductionOutputClearanceMeasurementPlanResult(
            plan,
            gaps,
            digest.ComputeSha256());
    }
}
