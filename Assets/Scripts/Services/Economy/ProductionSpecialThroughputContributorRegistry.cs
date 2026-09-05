using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Immutable facility input for special, non-recipe throughput contributors.
/// The capacity contributions are the physical output-branch authority; a
/// throughput contributor may explain their cycle, but may not invent branches.
/// </summary>
public sealed class ProductionSpecialThroughputFacilityContext
{
    public const string Schema =
        "production-special-throughput-facility-context@2";

    private readonly ProductionFacilityCapacitySubject facilitySubject;

    public ProductionSpecialThroughputFacilityContext(
        ProductionFacilityCapacitySubject facility,
        IReadOnlyList<ProductionFacilityOutputCapacityContribution>
            capacityContributions)
        : this(
            facility.DefinitionId,
            facility.WorkstationTag,
            capacityContributions,
            facility,
            true)
    {
    }

    public ProductionSpecialThroughputFacilityContext(
        string definitionId,
        string workstationTag,
        IReadOnlyList<ProductionFacilityOutputCapacityContribution>
            capacityContributions)
        : this(
            definitionId,
            workstationTag,
            capacityContributions,
            default,
            false)
    {
    }

    private ProductionSpecialThroughputFacilityContext(
        string definitionId,
        string workstationTag,
        IReadOnlyList<ProductionFacilityOutputCapacityContribution>
            capacityContributions,
        ProductionFacilityCapacitySubject facility,
        bool hasFacilitySubject)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        ProductionFacilityOutputCapacityContribution[] ordered =
            (capacityContributions
                ?? throw new ArgumentNullException(nameof(capacityContributions)))
            .OrderBy(value => value?.ContributorId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.ContributorId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Special throughput capacity contributions are null or duplicated.");
        }

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        CapacityContributions = Array.AsReadOnly(ordered);
        HasFacilitySubject = hasFacilitySubject;
        facilitySubject = facility;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(DefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(HasFacilitySubject);
        if (HasFacilitySubject)
        {
            if (!string.Equals(facility.DefinitionId, DefinitionId,
                    StringComparison.Ordinal)
                || !string.Equals(facility.WorkstationTag, WorkstationTag,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Special throughput facility subject identity drifted.");
            }
            digest.Append(facility.FacilityId.Value);
            digest.Append(facility.Position.x);
            digest.Append(facility.Position.y);
            digest.Append(facility.OutputBufferCycleCapacity);
            digest.Append(facility.WorkstationLaneProfile.SourceDigest);
            digest.Append(facility.ProcessFluidProfile.SourceDigest);
        }
        digest.Append(ordered.Length);
        foreach (ProductionFacilityOutputCapacityContribution contribution in ordered)
            digest.Append(contribution.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public IReadOnlyList<ProductionFacilityOutputCapacityContribution>
        CapacityContributions { get; }
    public bool HasFacilitySubject { get; }
    public string SourceDigest { get; }

    public ProductionFacilityCapacitySubject RequireFacilitySubject()
    {
        if (!HasFacilitySubject)
            throw new InvalidOperationException(
                "Special throughput context has no frozen facility subject.");
        return facilitySubject;
    }
}

/// <summary>
/// Exact coverage returned by one special throughput contributor. Every branch
/// owned by the bound capacity contributor must resolve to one candidate or one
/// typed gap; the registry validates that bijection before publication.
/// </summary>
public sealed class ProductionSpecialThroughputContributorResult
{
    public const string Schema =
        "production-special-throughput-contributor-result@1";

    public ProductionSpecialThroughputContributorResult(
        string contributorId,
        int contractVersion,
        string capacityContributorId,
        bool appliesToFacility,
        IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot> candidates,
        IReadOnlyList<ProductionThroughputCoverageGap> gaps,
        string contributorSourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            contributorId,
            nameof(contributorId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            capacityContributorId,
            nameof(capacityContributorId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            contributorSourceDigest,
            nameof(contributorSourceDigest));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));

        ProductionSpecialThroughputCandidateSnapshot[] orderedCandidates =
            (candidates ?? throw new ArgumentNullException(nameof(candidates)))
            .OrderBy(value => value?.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value?.BranchId, StringComparer.Ordinal)
            .ToArray();
        ProductionThroughputCoverageGap[] orderedGaps =
            (gaps ?? throw new ArgumentNullException(nameof(gaps)))
            .OrderBy(value => value?.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value?.BranchId, StringComparer.Ordinal)
            .ThenBy(value => value == null ? 0 : (int)value.Reason)
            .ToArray();
        if (orderedCandidates.Any(value => value == null)
            || orderedGaps.Any(value => value == null)
            || !appliesToFacility
                && (orderedCandidates.Length != 0 || orderedGaps.Length != 0)
            || appliesToFacility
                && orderedCandidates.Length == 0
                && orderedGaps.Length == 0)
        {
            throw new InvalidOperationException(
                "Special throughput contributor result applicability is invalid.");
        }

        ContributorId = contributorId;
        ContractVersion = contractVersion;
        CapacityContributorId = capacityContributorId;
        AppliesToFacility = appliesToFacility;
        Candidates = Array.AsReadOnly(orderedCandidates);
        Gaps = Array.AsReadOnly(orderedGaps);
        ContributorSourceDigest = contributorSourceDigest;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ContributorId);
        digest.Append(ContractVersion);
        digest.Append(CapacityContributorId);
        digest.Append(AppliesToFacility);
        digest.Append(ContributorSourceDigest);
        digest.Append(Candidates.Count);
        foreach (ProductionSpecialThroughputCandidateSnapshot candidate in Candidates)
        {
            digest.Append(candidate.DefinitionId);
            digest.Append(candidate.WorkstationTag);
            digest.Append(candidate.ProducerId);
            digest.Append(candidate.BranchId);
            digest.Append(candidate.PeakOutputMassGramsPerHour);
            digest.Append(candidate.SourceDigest);
        }
        digest.Append(Gaps.Count);
        foreach (ProductionThroughputCoverageGap gap in Gaps)
        {
            digest.Append(gap.DefinitionId);
            digest.Append(gap.WorkstationTag);
            digest.Append((int)gap.ProducerKind);
            digest.Append(gap.ProducerId);
            digest.Append(gap.BranchId);
            digest.Append((int)gap.Reason);
            digest.Append(gap.Detail);
            digest.Append(gap.SourceDigest);
        }
        SourceDigest = digest.ComputeSha256();
    }

    public string ContributorId { get; }
    public int ContractVersion { get; }
    public string CapacityContributorId { get; }
    public bool AppliesToFacility { get; }
    public IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot> Candidates
        { get; }
    public IReadOnlyList<ProductionThroughputCoverageGap> Gaps { get; }
    public string ContributorSourceDigest { get; }
    public string SourceDigest { get; }
}

public interface IProductionSpecialThroughputContributor
{
    string ContributorId { get; }
    int ContractVersion { get; }
    string CapacityContributorId { get; }

    ProductionSpecialThroughputContributorResult Capture(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution capacityContribution);
}

/// <summary>
/// Frozen special-throughput rows ready for injection into
/// ProductionAuthoredThroughputFacilitySubject.
/// </summary>
public sealed class ProductionSpecialThroughputAggregate
{
    internal ProductionSpecialThroughputAggregate(
        string definitionId,
        string workstationTag,
        IReadOnlyList<ProductionSpecialThroughputContributorResult> contributions,
        IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot> candidates,
        IReadOnlyList<ProductionThroughputCoverageGap> gaps,
        string sourceDigest)
    {
        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        Contributions = Array.AsReadOnly(contributions.ToArray());
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Gaps = Array.AsReadOnly(gaps.ToArray());
        SourceDigest = sourceDigest;
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public IReadOnlyList<ProductionSpecialThroughputContributorResult>
        Contributions { get; }
    public IReadOnlyList<ProductionSpecialThroughputCandidateSnapshot> Candidates
        { get; }
    public IReadOnlyList<ProductionThroughputCoverageGap> Gaps { get; }
    public string SourceDigest { get; }

    public ProductionAuthoredThroughputFacilitySubject CreateFacilitySubject(
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        IReadOnlyList<ProductionRecipeSO> recipes) => new(
        DefinitionId,
        WorkstationTag,
        laneProfile,
        processFluidProfile,
        recipes,
        Candidates,
        Gaps);
}

/// <summary>
/// Deterministic registry for polymorphic, non-recipe throughput authorities.
/// Registration order and facility capacity-contribution order cannot affect
/// publication. Invalid or partial coverage fails before an aggregate exists.
/// </summary>
public sealed class ProductionSpecialThroughputContributorRegistry
{
    public const string Schema =
        "production-special-throughput-contributor-registry@1";

    private readonly IProductionSpecialThroughputContributor[] contributors;

    public ProductionSpecialThroughputContributorRegistry(
        IEnumerable<IProductionSpecialThroughputContributor> contributors)
    {
        IProductionSpecialThroughputContributor[] source = (contributors
                ?? throw new ArgumentNullException(nameof(contributors)))
            .ToArray();
        if (source.Any(value => value == null
                || !Canonical(value.ContributorId)
                || value.ContractVersion <= 0
                || !Canonical(value.CapacityContributorId)))
        {
            throw new InvalidOperationException(
                "Special throughput contributor metadata is invalid.");
        }
        this.contributors = source
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .ToArray();
        if (this.contributors.Select(value => value.ContributorId)
                .Distinct(StringComparer.Ordinal).Count()
                != this.contributors.Length)
        {
            throw new InvalidOperationException(
                "Duplicate special throughput contributor ID.");
        }
        if (this.contributors.Select(value => value.CapacityContributorId)
                .Distinct(StringComparer.Ordinal).Count()
                != this.contributors.Length)
        {
            throw new InvalidOperationException(
                "A capacity contributor has multiple special throughput owners.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(this.contributors.Length);
        foreach (IProductionSpecialThroughputContributor contributor in
                 this.contributors)
        {
            digest.Append(contributor.ContributorId);
            digest.Append(contributor.ContractVersion);
            digest.Append(contributor.CapacityContributorId);
        }
        RegistryFingerprint = digest.ComputeSha256();
    }

    public string RegistryFingerprint { get; }

    public ProductionSpecialThroughputAggregate Capture(
        ProductionSpecialThroughputFacilityContext facility)
    {
        if (facility == null)
            throw new ArgumentNullException(nameof(facility));

        Dictionary<string, ProductionFacilityOutputCapacityContribution>
            capacityById = facility.CapacityContributions.ToDictionary(
                value => value.ContributorId,
                StringComparer.Ordinal);
        List<ProductionSpecialThroughputContributorResult> results = new();
        List<ProductionSpecialThroughputCandidateSnapshot> candidates = new();
        List<ProductionThroughputCoverageGap> gaps = new();
        HashSet<string> candidateKeys = new(StringComparer.Ordinal);
        HashSet<string> gapKeys = new(StringComparer.Ordinal);

        HashSet<string> ownedCapacityContributorIds = new(
            contributors.Select(value => value.CapacityContributorId),
            StringComparer.Ordinal);
        foreach (ProductionFacilityOutputCapacityContribution capacity in
                 facility.CapacityContributions)
        {
            if (!capacity.AppliesToFacility
                || ownedCapacityContributorIds.Contains(capacity.ContributorId))
            {
                continue;
            }

            foreach (ProductionFacilityOutputCapacityBranch branch in capacity.Branches)
            {
                string key = Key(capacity.ContributorId, branch.BranchId);
                if (!gapKeys.Add(key))
                {
                    throw new InvalidOperationException(
                        "Unregistered special throughput capacity branch is duplicated: "
                        + key);
                }
                gaps.Add(CreateUnregisteredProviderGap(
                    facility,
                    capacity,
                    branch));
            }
        }

        foreach (IProductionSpecialThroughputContributor contributor in contributors)
        {
            if (!capacityById.TryGetValue(
                    contributor.CapacityContributorId,
                    out ProductionFacilityOutputCapacityContribution capacity))
            {
                throw new InvalidOperationException(
                    "Special throughput contributor has no captured capacity authority: "
                    + contributor.ContributorId + "/"
                    + contributor.CapacityContributorId);
            }

            ProductionSpecialThroughputContributorResult result =
                contributor.Capture(facility, capacity)
                ?? throw new InvalidOperationException(
                    "Special throughput contributor returned no result: "
                    + contributor.ContributorId);
            ValidateExactResult(facility, contributor, capacity, result);
            results.Add(result);

            foreach (ProductionSpecialThroughputCandidateSnapshot candidate in
                     result.Candidates)
            {
                string key = Key(candidate.ProducerId, candidate.BranchId);
                if (!candidateKeys.Add(key) || gapKeys.Contains(key))
                {
                    throw new InvalidOperationException(
                        "Special throughput candidate is duplicated or collides with a gap: "
                        + key);
                }
                candidates.Add(FreezeCandidate(
                    facility,
                    capacity,
                    result,
                    candidate));
            }
            foreach (ProductionThroughputCoverageGap gap in result.Gaps)
            {
                string key = Key(gap.ProducerId, gap.BranchId);
                if (!gapKeys.Add(key) || candidateKeys.Contains(key))
                {
                    throw new InvalidOperationException(
                        "Special throughput gap is duplicated or collides with a candidate: "
                        + key);
                }
                gaps.Add(FreezeGap(facility, capacity, result, gap));
            }
        }

        HashSet<string> expectedKeys = new(
            facility.CapacityContributions
                .Where(value => value.AppliesToFacility)
                .SelectMany(value => value.Branches.Select(branch =>
                    Key(value.ContributorId, branch.BranchId))),
            StringComparer.Ordinal);
        HashSet<string> publishedKeys = new(candidateKeys, StringComparer.Ordinal);
        publishedKeys.UnionWith(gapKeys);
        if (!expectedKeys.SetEquals(publishedKeys))
        {
            throw new InvalidOperationException(
                "Special throughput aggregate does not exactly cover all applicable capacity branches.");
        }

        ProductionSpecialThroughputContributorResult[] orderedResults = results
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .ToArray();
        ProductionSpecialThroughputCandidateSnapshot[] orderedCandidates = candidates
            .OrderBy(value => value.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value.BranchId, StringComparer.Ordinal)
            .ToArray();
        ProductionThroughputCoverageGap[] orderedGaps = gaps
            .OrderBy(value => value.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value.BranchId, StringComparer.Ordinal)
            .ThenBy(value => (int)value.Reason)
            .ToArray();

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(RegistryFingerprint);
        digest.Append(facility.SourceDigest);
        digest.Append(orderedResults.Length);
        foreach (ProductionSpecialThroughputContributorResult result in orderedResults)
            digest.Append(result.SourceDigest);
        digest.Append(orderedCandidates.Length);
        foreach (ProductionSpecialThroughputCandidateSnapshot candidate in
                 orderedCandidates)
            digest.Append(candidate.SourceDigest);
        digest.Append(orderedGaps.Length);
        foreach (ProductionThroughputCoverageGap gap in orderedGaps)
            digest.Append(gap.SourceDigest);

        return new ProductionSpecialThroughputAggregate(
            facility.DefinitionId,
            facility.WorkstationTag,
            orderedResults,
            orderedCandidates,
            orderedGaps,
            digest.ComputeSha256());
    }

    private static ProductionThroughputCoverageGap CreateUnregisteredProviderGap(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution capacity,
        ProductionFacilityOutputCapacityBranch branch)
    {
        const string detail =
            "No special throughput contributor owns this capacity branch.";
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-special-throughput-unregistered-provider-gap@1");
        digest.Append(facility.SourceDigest);
        digest.Append(capacity.SourceDigest);
        digest.Append(branch.BranchId);
        digest.Append((int)ProductionThroughputGapReason
            .SpecialThroughputProviderUnregistered);
        digest.Append(detail);
        return new ProductionThroughputCoverageGap(
            facility.DefinitionId,
            facility.WorkstationTag,
            ProductionThroughputProducerKind.CapacityContributor,
            capacity.ContributorId,
            branch.BranchId,
            ProductionThroughputGapReason.SpecialThroughputProviderUnregistered,
            detail,
            digest.ComputeSha256());
    }

    private static void ValidateExactResult(
        ProductionSpecialThroughputFacilityContext facility,
        IProductionSpecialThroughputContributor contributor,
        ProductionFacilityOutputCapacityContribution capacity,
        ProductionSpecialThroughputContributorResult result)
    {
        if (!string.Equals(result.ContributorId, contributor.ContributorId,
                StringComparison.Ordinal)
            || result.ContractVersion != contributor.ContractVersion
            || !string.Equals(
                result.CapacityContributorId,
                contributor.CapacityContributorId,
                StringComparison.Ordinal)
            || result.AppliesToFacility != capacity.AppliesToFacility)
        {
            throw new InvalidOperationException(
                "Special throughput result metadata or applicability drifted: "
                + contributor.ContributorId);
        }

        HashSet<string> authoredBranches = new(
            capacity.Branches.Select(value => value.BranchId),
            StringComparer.Ordinal);
        HashSet<string> coveredBranches = new(StringComparer.Ordinal);
        foreach (ProductionSpecialThroughputCandidateSnapshot candidate in
                 result.Candidates)
        {
            ValidateFacilityAndProducer(
                facility,
                capacity.ContributorId,
                candidate.DefinitionId,
                candidate.WorkstationTag,
                candidate.ProducerId,
                candidate.BranchId);
            if (!authoredBranches.Contains(candidate.BranchId)
                || !coveredBranches.Add(candidate.BranchId))
            {
                throw new InvalidOperationException(
                    "Special throughput candidate references an orphan or duplicate branch: "
                    + candidate.BranchId);
            }
        }
        foreach (ProductionThroughputCoverageGap gap in result.Gaps)
        {
            if (gap.ProducerKind
                    != ProductionThroughputProducerKind.CapacityContributor)
            {
                throw new InvalidOperationException(
                    "Special throughput gap has the wrong producer kind.");
            }
            ValidateFacilityAndProducer(
                facility,
                capacity.ContributorId,
                gap.DefinitionId,
                gap.WorkstationTag,
                gap.ProducerId,
                gap.BranchId);
            if (!authoredBranches.Contains(gap.BranchId)
                || !coveredBranches.Add(gap.BranchId))
            {
                throw new InvalidOperationException(
                    "Special throughput gap references an orphan, duplicate, or colliding branch: "
                    + gap.BranchId);
            }
        }

        if (capacity.AppliesToFacility
            && !authoredBranches.SetEquals(coveredBranches)
            || !capacity.AppliesToFacility && coveredBranches.Count != 0)
        {
            throw new InvalidOperationException(
                "Special throughput result does not exactly cover capacity branches: "
                + contributor.ContributorId);
        }
    }

    private static void ValidateFacilityAndProducer(
        ProductionSpecialThroughputFacilityContext facility,
        string expectedProducerId,
        string definitionId,
        string workstationTag,
        string producerId,
        string branchId)
    {
        if (!string.Equals(definitionId, facility.DefinitionId,
                StringComparison.Ordinal)
            || !string.Equals(workstationTag, facility.WorkstationTag,
                StringComparison.Ordinal)
            || !string.Equals(producerId, expectedProducerId,
                StringComparison.Ordinal)
            || string.IsNullOrEmpty(branchId))
        {
            throw new InvalidOperationException(
                "Special throughput row has orphan facility, producer, or branch provenance.");
        }
    }

    private static ProductionSpecialThroughputCandidateSnapshot FreezeCandidate(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution capacity,
        ProductionSpecialThroughputContributorResult result,
        ProductionSpecialThroughputCandidateSnapshot candidate)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-special-throughput-frozen-candidate@1");
        digest.Append(facility.SourceDigest);
        digest.Append(capacity.SourceDigest);
        digest.Append(result.SourceDigest);
        digest.Append(candidate.DefinitionId);
        digest.Append(candidate.WorkstationTag);
        digest.Append(candidate.ProducerId);
        digest.Append(candidate.BranchId);
        digest.Append(candidate.PeakOutputMassGramsPerHour);
        digest.Append(candidate.SourceDigest);
        return new ProductionSpecialThroughputCandidateSnapshot(
            candidate.DefinitionId,
            candidate.WorkstationTag,
            candidate.ProducerId,
            candidate.BranchId,
            candidate.PeakOutputMassGramsPerHour,
            digest.ComputeSha256());
    }

    private static ProductionThroughputCoverageGap FreezeGap(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution capacity,
        ProductionSpecialThroughputContributorResult result,
        ProductionThroughputCoverageGap gap)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-special-throughput-frozen-gap@1");
        digest.Append(facility.SourceDigest);
        digest.Append(capacity.SourceDigest);
        digest.Append(result.SourceDigest);
        digest.Append(gap.DefinitionId);
        digest.Append(gap.WorkstationTag);
        digest.Append((int)gap.ProducerKind);
        digest.Append(gap.ProducerId);
        digest.Append(gap.BranchId);
        digest.Append((int)gap.Reason);
        digest.Append(gap.Detail);
        digest.Append(gap.SourceDigest);
        return new ProductionThroughputCoverageGap(
            gap.DefinitionId,
            gap.WorkstationTag,
            gap.ProducerKind,
            gap.ProducerId,
            gap.BranchId,
            gap.Reason,
            gap.Detail,
            digest.ComputeSha256());
    }

    private static string Key(string producerId, string branchId) =>
        producerId + "\n" + branchId;

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
