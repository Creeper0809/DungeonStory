using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Immutable input for the final output-buffer review. The throughput envelope
/// is the authored-reachable maximum support/work-speed envelope; it must not
/// be replaced with the supports currently attached to one scene instance.
/// </summary>
public sealed class ProductionOutputClearanceCapacityReviewInput
{
    public ProductionOutputClearanceCapacityReviewInput(
        string definitionId,
        string workstationTag,
        int authoredWholeCycles,
        long maximumCycleCompletionFootprintGrams,
        ProductionFacilityWorkstationLaneCapacityProfile laneProfile,
        ProductionOutputThroughputEnvelopeSnapshot throughputEnvelope,
        string upstreamSourceDigest)
    {
        RequireCanonical(definitionId, nameof(definitionId));
        RequireCanonical(workstationTag, nameof(workstationTag));
        if (authoredWholeCycles is < 2 or > 4)
            throw new ArgumentOutOfRangeException(nameof(authoredWholeCycles));
        if (maximumCycleCompletionFootprintGrams <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCycleCompletionFootprintGrams));
        }
        LaneProfile = laneProfile
            ?? throw new ArgumentNullException(nameof(laneProfile));
        if (!LaneProfile.IsSpecified)
        {
            throw new ArgumentException(
                "Output-clearance review requires explicit lane authority.",
                nameof(laneProfile));
        }
        if (!string.Equals(
                throughputEnvelope.DefinitionId,
                definitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                throughputEnvelope.WorkstationTag,
                workstationTag,
                StringComparison.Ordinal)
            || throughputEnvelope.PeakOutputMassGramsPerHour <= 0L)
        {
            throw new ArgumentException(
                "Output-clearance throughput envelope key is inconsistent.",
                nameof(throughputEnvelope));
        }
        if (!IsLowercaseSha256(upstreamSourceDigest))
        {
            throw new ArgumentException(
                "Output-clearance review upstream digest must be lowercase SHA-256.",
                nameof(upstreamSourceDigest));
        }

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        AuthoredWholeCycles = authoredWholeCycles;
        MaximumCycleCompletionFootprintGrams =
            maximumCycleCompletionFootprintGrams;
        ThroughputEnvelope = throughputEnvelope;
        UpstreamSourceDigest = upstreamSourceDigest;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-capacity-review-input@1");
        digest.Append(DefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(AuthoredWholeCycles);
        digest.Append(MaximumCycleCompletionFootprintGrams);
        digest.Append(LaneProfile.SourceDigest);
        digest.Append(ThroughputEnvelope.PeakOutputMassGramsPerHour);
        digest.Append(ThroughputEnvelope.SourceDigest);
        digest.Append(UpstreamSourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public int AuthoredWholeCycles { get; }
    public long MaximumCycleCompletionFootprintGrams { get; }
    public ProductionFacilityWorkstationLaneCapacityProfile LaneProfile { get; }
    public ProductionOutputThroughputEnvelopeSnapshot ThroughputEnvelope { get; }
    public string UpstreamSourceDigest { get; }
    public string SourceDigest { get; }

    private static void RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.IndexOf(';') >= 0
            || value.IndexOf('\r') >= 0
            || value.IndexOf('\n') >= 0)
        {
            throw new ArgumentException(
                "Output-clearance capacity review identity must be canonical.",
                parameterName);
        }
    }

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!(character is >= '0' and <= '9'
                || character is >= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}

public sealed class ProductionOutputClearanceCapacityReviewRow
{
    internal ProductionOutputClearanceCapacityReviewRow(
        ProductionOutputClearanceCapacityReviewInput input,
        ProductionOutputClearanceProfileRecord profile,
        ProductionOutputClearanceCapacityGateAssessment assessment)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Assessment = assessment;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-capacity-review-row@2");
        digest.Append(Input.SourceDigest);
        digest.Append(Profile.SourceDigest);
        digest.Append(Assessment.SourceDigest);
        digest.AppendEnum(Assessment.Disposition);
        digest.Append(Assessment.FailureCode);
        digest.Append(Assessment.DiagnosticCode);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceCapacityReviewInput Input { get; }
    public ProductionOutputClearanceProfileRecord Profile { get; }
    public ProductionOutputClearanceCapacityGateAssessment Assessment { get; }
    public bool IsCritical => Assessment.IsBlockingCritical;
    public bool RequiresBackpressure => Assessment.RequiresBackpressure;
    public string SourceDigest { get; }
}

/// <summary>
/// Deterministic, content-neutral join between exact producer scope, frozen
/// natural-haul p95 profiles and existing authored 2..4-cycle capacity. It
/// reports undersized and greater-than-four-cycle requirements without ever
/// mutating or silently expanding authored capacity. Greater-than-four demand
/// is a visible backpressure disposition; only structural or bounded authored
/// capacity failures block publication.
/// </summary>
public sealed class ProductionOutputClearanceCapacityReviewPortfolio
{
    public const int MinimumCertifiedSeedCount = 32;

    private ProductionOutputClearanceCapacityReviewPortfolio(
        IReadOnlyList<ProductionOutputClearanceCapacityReviewRow> rows)
    {
        ProductionOutputClearanceCapacityReviewRow[] ordered = rows
            .OrderBy(value => value.Input.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value.Input.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        Rows = Array.AsReadOnly(ordered);
        BlockingCriticalCount = ordered.Count(value => value.IsCritical);
        BackpressureExpectedCount = ordered.Count(
            value => value.RequiresBackpressure);
        AcceptedCount = ordered.Length
            - BlockingCriticalCount
            - BackpressureExpectedCount;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-capacity-review-portfolio@2");
        digest.Append(ordered.Length);
        foreach (ProductionOutputClearanceCapacityReviewRow row in ordered)
            digest.Append(row.SourceDigest);
        digest.Append(AcceptedCount);
        digest.Append(BackpressureExpectedCount);
        digest.Append(BlockingCriticalCount);
        SourceDigest = digest.ComputeSha256();
    }

    public IReadOnlyList<ProductionOutputClearanceCapacityReviewRow> Rows
        { get; }
    public int AcceptedCount { get; }
    public int BackpressureExpectedCount { get; }
    public int BlockingCriticalCount { get; }
    public int CriticalCount => BlockingCriticalCount;
    public string SourceDigest { get; }

    public static ProductionOutputClearanceCapacityReviewPortfolio BuildCurrent(
        ProductionOutputClearanceMeasurementScopeSnapshot scope,
        IReadOnlyList<ProductionOutputClearanceProfileRecord> profiles)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (scope.Gaps.Count != 0
            || scope.Plans.Count != scope.AuthoredScope.Facilities.Count)
        {
            throw new InvalidOperationException(
                "Current output-clearance scope is incomplete; capacity review cannot use a fallback row.");
        }

        Dictionary<string, ProductionFacilityOutputCensusRow> census = scope
            .AuthoredScope.Census.Rows
            .Where(value => value.IsAutomaticProducer)
            .ToDictionary(Key, StringComparer.Ordinal);
        Dictionary<string, ProductionOutputThroughputEnvelopeSnapshot> envelopes =
            scope.AuthoredScope.Coverage.CompleteEnvelopes
                .ToDictionary(Key, StringComparer.Ordinal);
        if (scope.AuthoredScope.Coverage.Gaps.Count != 0
            || census.Count != scope.Plans.Count
            || envelopes.Count != scope.Plans.Count)
        {
            throw new InvalidOperationException(
                "Current output-clearance census or maximum-support throughput envelope is incomplete.");
        }

        List<ProductionOutputClearanceCapacityReviewInput> inputs =
            new(scope.Plans.Count);
        foreach (ProductionOutputClearanceMeasurementPlan plan in scope.Plans)
        {
            string key = Key(plan.DefinitionId, plan.WorkstationTag);
            if (!census.TryGetValue(key, out ProductionFacilityOutputCensusRow row)
                || !envelopes.TryGetValue(
                    key,
                    out ProductionOutputThroughputEnvelopeSnapshot envelope))
            {
                throw new InvalidOperationException(
                    "Current output-clearance plan is not joined to exact census and throughput rows: "
                    + key);
            }

            CanonicalSemanticDigestBuilder upstream = new();
            upstream.Append("production-output-clearance-current-review-source@1");
            upstream.Append(scope.SourceDigest);
            upstream.Append(row.SourceDigest);
            upstream.Append(plan.SourceDigest);
            upstream.Append(envelope.SourceDigest);
            inputs.Add(new ProductionOutputClearanceCapacityReviewInput(
                plan.DefinitionId,
                plan.WorkstationTag,
                row.OutputBufferCycleCapacity,
                plan.Winner.Source.MaximumSingleCompletionMassGrams,
                new ProductionFacilityWorkstationLaneCapacityProfile(
                    row.LanePolicy,
                    row.ManualWorkLaneCount,
                    row.AutomaticWorkLaneCount),
                envelope,
                upstream.ComputeSha256()));
        }
        return Build(inputs, profiles);
    }

    public static ProductionOutputClearanceCapacityReviewPortfolio Build(
        IReadOnlyList<ProductionOutputClearanceCapacityReviewInput> inputs,
        IReadOnlyList<ProductionOutputClearanceProfileRecord> profiles)
    {
        ProductionOutputClearanceCapacityReviewInput[] orderedInputs = (inputs
                ?? throw new ArgumentNullException(nameof(inputs)))
            .OrderBy(value => value?.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value?.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceProfileRecord[] orderedProfiles = (profiles
                ?? throw new ArgumentNullException(nameof(profiles)))
            .OrderBy(value => value?.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value?.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        if (orderedInputs.Length == 0
            || orderedInputs.Any(value => value == null)
            || orderedProfiles.Any(value => value == null)
            || orderedInputs.Select(Key).Distinct(StringComparer.Ordinal).Count()
                != orderedInputs.Length
            || orderedProfiles.Select(Key).Distinct(StringComparer.Ordinal).Count()
                != orderedProfiles.Length)
        {
            throw new InvalidOperationException(
                "Output-clearance capacity review inputs or profiles are empty, null, or duplicated.");
        }
        if (orderedInputs.Length != orderedProfiles.Length)
        {
            throw new InvalidOperationException(
                "Output-clearance capacity review profile cardinality is incomplete.");
        }

        Dictionary<string, ProductionOutputClearanceProfileRecord> byKey =
            orderedProfiles.ToDictionary(Key, StringComparer.Ordinal);
        List<ProductionOutputClearanceCapacityReviewRow> rows =
            new(orderedInputs.Length);
        foreach (ProductionOutputClearanceCapacityReviewInput input in orderedInputs)
        {
            string key = Key(input);
            if (!byKey.Remove(
                    key,
                    out ProductionOutputClearanceProfileRecord profile))
            {
                throw new InvalidOperationException(
                    "Output-clearance capacity review is missing a frozen p95 profile: "
                    + key);
            }
            if (profile.SampleCount < MinimumCertifiedSeedCount
                || profile.DistinctSeedCount < MinimumCertifiedSeedCount
                || profile.SampleCount != profile.DistinctSeedCount)
            {
                throw new InvalidOperationException(
                    "Output-clearance capacity review profile is not a one-observation-per-seed certified cohort: "
                    + key);
            }
            if (profile.PeakOutputMassGramsPerHour
                    != input.ThroughputEnvelope.PeakOutputMassGramsPerHour
                || !string.Equals(
                    profile.ThroughputSourceDigest,
                    input.ThroughputEnvelope.SourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Output-clearance p95 profile drifted from the maximum reachable support/work-speed envelope: "
                    + key);
            }

            ProductionFacilityCapacitySubject subject = new(
                (BuildingInstanceId)("building:clearance-review:" + input.DefinitionId),
                Vector2Int.zero,
                input.DefinitionId,
                input.WorkstationTag,
                input.AuthoredWholeCycles,
                input.LaneProfile);
            ProductionOutputClearanceCapacityGateAssessment assessment =
                ProductionOutputClearanceCapacityGate.Assess(
                    subject,
                    input.MaximumCycleCompletionFootprintGrams,
                    profile.Snapshot);
            rows.Add(new ProductionOutputClearanceCapacityReviewRow(
                input,
                profile,
                assessment));
        }
        if (byKey.Count != 0)
        {
            throw new InvalidOperationException(
                "Output-clearance capacity review contains an orphan frozen p95 profile: "
                + byKey.Keys.OrderBy(value => value, StringComparer.Ordinal)
                    .First());
        }
        return new ProductionOutputClearanceCapacityReviewPortfolio(rows);
    }

    private static string Key(ProductionFacilityOutputCensusRow value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(
        ProductionOutputThroughputEnvelopeSnapshot value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(
        ProductionOutputClearanceCapacityReviewInput value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(ProductionOutputClearanceProfileRecord value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(string definitionId, string workstationTag) =>
        definitionId + "\n" + workstationTag;
}
