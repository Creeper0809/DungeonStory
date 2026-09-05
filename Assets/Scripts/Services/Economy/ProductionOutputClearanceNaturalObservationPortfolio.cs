using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionOutputClearanceNaturalObservationRecord
{
    public const string Schema =
        "production-output-clearance-natural-observation@4";

    public ProductionOutputClearanceNaturalObservationRecord(
        ProductionOutputClearanceMeasurementFixture fixture,
        string runtimeFacilityId,
        string resolvedOutputVectorDigest,
        long actualBatchMassGrams,
        string batchCommitId,
        string topologySourceDigest,
        bool topologyStable,
        bool facilityAttributionExact,
        string ownerRosterKey,
        long actionEpochDelta,
        long actionStartDelta,
        long haulStartDelta,
        long clearanceMicroHours,
        int telemetryCompletedCount,
        int telemetryActiveCount,
        int orphanPickupCount,
        int conflictingPublicationCount,
        int overPickupCount,
        int capacityExceededCount,
        int restoreInterruptionCount,
        bool telemetryClean,
        bool schedulerProvenanceExact,
        bool deliveryExact,
        string randomStateDigest,
        long randomDrawDelta)
    {
        Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            runtimeFacilityId,
            nameof(runtimeFacilityId));
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            batchCommitId,
            nameof(batchCommitId));
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            ownerRosterKey,
            nameof(ownerRosterKey));
        RequireDigest(resolvedOutputVectorDigest,
            nameof(resolvedOutputVectorDigest));
        RequireDigest(topologySourceDigest, nameof(topologySourceDigest));
        RequireDigest(randomStateDigest, nameof(randomStateDigest));
        long maximumBatchMassGrams =
            fixture.Winner.Source.MaximumSingleCompletionMassGrams;
        if (actualBatchMassGrams <= 0L
            || actualBatchMassGrams > maximumBatchMassGrams)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualBatchMassGrams),
                actualBatchMassGrams,
                "Natural clearance observation must publish a positive actual "
                + "batch no larger than its separately proven reachable maximum. "
                + "maximum=" + maximumBatchMassGrams);
        }

        if (actionEpochDelta < 0L
            || actionStartDelta < 0L
            || haulStartDelta < 0L
            || clearanceMicroHours <= 0L
            || telemetryCompletedCount < 0
            || telemetryActiveCount < 0
            || orphanPickupCount < 0
            || conflictingPublicationCount < 0
            || overPickupCount < 0
            || capacityExceededCount < 0
            || restoreInterruptionCount < 0
            || randomDrawDelta < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actionEpochDelta),
                "Natural clearance observation contains an invalid physical or diagnostic value.");
        }

        RuntimeFacilityId = runtimeFacilityId;
        ResolvedOutputVectorDigest = resolvedOutputVectorDigest;
        ActualBatchMassGrams = actualBatchMassGrams;
        BatchCommitId = batchCommitId;
        TopologySourceDigest = topologySourceDigest;
        TopologyStable = topologyStable;
        FacilityAttributionExact = facilityAttributionExact;
        OwnerRosterKey = ownerRosterKey;
        ActionEpochDelta = actionEpochDelta;
        ActionStartDelta = actionStartDelta;
        HaulStartDelta = haulStartDelta;
        ClearanceMicroHours = clearanceMicroHours;
        ClearanceMilliHours = checked((clearanceMicroHours + 999L) / 1_000L);
        TelemetryCompletedCount = telemetryCompletedCount;
        TelemetryActiveCount = telemetryActiveCount;
        OrphanPickupCount = orphanPickupCount;
        ConflictingPublicationCount = conflictingPublicationCount;
        OverPickupCount = overPickupCount;
        CapacityExceededCount = capacityExceededCount;
        RestoreInterruptionCount = restoreInterruptionCount;
        TelemetryClean = telemetryClean;
        SchedulerProvenanceExact = schedulerProvenanceExact;
        DeliveryExact = deliveryExact;
        RandomStateDigest = randomStateDigest;
        RandomDrawDelta = randomDrawDelta;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(Fixture.SourceDigest);
        digest.Append(Fixture.ObservationId);
        digest.Append(RuntimeFacilityId);
        digest.Append(ResolvedOutputVectorDigest);
        digest.Append(ActualBatchMassGrams);
        digest.Append(BatchCommitId);
        digest.Append(TopologySourceDigest);
        digest.Append(TopologyStable);
        digest.Append(FacilityAttributionExact);
        digest.Append(OwnerRosterKey);
        digest.Append(ActionEpochDelta);
        digest.Append(ActionStartDelta);
        digest.Append(HaulStartDelta);
        digest.Append(ClearanceMicroHours);
        digest.Append(ClearanceMilliHours);
        digest.Append(TelemetryCompletedCount);
        digest.Append(TelemetryActiveCount);
        digest.Append(OrphanPickupCount);
        digest.Append(ConflictingPublicationCount);
        digest.Append(OverPickupCount);
        digest.Append(CapacityExceededCount);
        digest.Append(RestoreInterruptionCount);
        digest.Append(TelemetryClean);
        digest.Append(SchedulerProvenanceExact);
        digest.Append(DeliveryExact);
        digest.Append(RandomStateDigest);
        digest.Append(RandomDrawDelta);
        RunSourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementFixture Fixture { get; }
    public string RuntimeFacilityId { get; }
    public string ResolvedOutputVectorDigest { get; }
    public long ActualBatchMassGrams { get; }
    public string BatchCommitId { get; }
    public string TopologySourceDigest { get; }
    public bool TopologyStable { get; }
    public bool FacilityAttributionExact { get; }
    public string OwnerRosterKey { get; }
    public long ActionEpochDelta { get; }
    public long ActionStartDelta { get; }
    public long HaulStartDelta { get; }
    public long ClearanceMicroHours { get; }
    public long ClearanceMilliHours { get; }
    public int TelemetryCompletedCount { get; }
    public int TelemetryActiveCount { get; }
    public int OrphanPickupCount { get; }
    public int ConflictingPublicationCount { get; }
    public int OverPickupCount { get; }
    public int CapacityExceededCount { get; }
    public int RestoreInterruptionCount { get; }
    public bool TelemetryClean { get; }
    public bool SchedulerProvenanceExact { get; }
    public bool DeliveryExact { get; }
    public string RandomStateDigest { get; }
    public long RandomDrawDelta { get; }
    public string RunSourceDigest { get; }

    public bool IsExact => TopologyStable
        && FacilityAttributionExact
        && TelemetryCompletedCount > 0
        && TelemetryActiveCount == 0
        && OrphanPickupCount == 0
        && ConflictingPublicationCount == 0
        && OverPickupCount == 0
        && CapacityExceededCount == 0
        && RestoreInterruptionCount == 0
        && TelemetryClean
        && SchedulerProvenanceExact
        && DeliveryExact;

    public ProductionOutputClearanceProfileObservation ToProfileObservation()
        => new(
            Fixture.Plan.DefinitionId,
            Fixture.Plan.WorkstationTag,
            Fixture.DeterministicSeed,
            BatchCommitId,
            ClearanceMicroHours,
            RunSourceDigest,
            ActualBatchMassGrams);

    private static void RequireDigest(string value, string parameterName)
    {
        if (!ProductionOutputClearanceProfileObservation
                .IsLowercaseSha256(value))
        {
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.",
                parameterName);
        }
    }
}

public sealed class ProductionOutputClearanceNaturalObservationPortfolioSnapshot
{
    private ProductionOutputClearanceNaturalObservationPortfolioSnapshot(
        ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio,
        IReadOnlyList<ProductionOutputClearanceNaturalObservationRecord> records)
    {
        Portfolio = portfolio;
        Records = records;
        ProfileObservations = Array.AsReadOnly(records
            .Select(value => value.ToProfileObservation())
            .ToArray());

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-observation-portfolio@1");
        digest.Append(Portfolio.SourceDigest);
        digest.Append(Records.Count);
        foreach (ProductionOutputClearanceNaturalObservationRecord record in
                 Records)
            digest.Append(record.RunSourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementPortfolioSnapshot Portfolio
        { get; }
    public IReadOnlyList<ProductionOutputClearanceNaturalObservationRecord>
        Records { get; }
    public IReadOnlyList<ProductionOutputClearanceProfileObservation>
        ProfileObservations { get; }
    public string SourceDigest { get; }

    public static ProductionOutputClearanceNaturalObservationPortfolioSnapshot
        Build(
            ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio,
            IReadOnlyList<ProductionOutputClearanceNaturalObservationRecord>
                records)
    {
        if (portfolio == null)
            throw new ArgumentNullException(nameof(portfolio));
        ProductionOutputClearanceNaturalObservationRecord[] ordered = (records
                ?? throw new ArgumentNullException(nameof(records)))
            .OrderBy(value => value?.Fixture.Plan.DefinitionId,
                StringComparer.Ordinal)
            .ThenBy(value => value?.Fixture.Plan.WorkstationTag,
                StringComparer.Ordinal)
            .ThenBy(value => value?.Fixture.SeedIndex)
            .ThenBy(value => value?.Fixture.DeterministicSeed)
            .ToArray();
        if (ordered.Length != portfolio.Fixtures.Count
            || ordered.Any(value => value == null || !value.IsExact)
            || ordered.Select(value => value.Fixture.SourceDigest)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || ordered.Select(value => value.Fixture.ObservationId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || ordered.Select(value => value.Fixture.ObservationId + "\u001f"
                    + value.BatchCommitId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Natural clearance observation portfolio is incomplete, inexact, or duplicated.");
        }

        string[] expected = portfolio.Fixtures
            .Select(value => value.SourceDigest)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actual = ordered.Select(value => value.Fixture.SourceDigest)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Natural clearance observations do not exactly match the frozen fixture portfolio.");
        }

        return new ProductionOutputClearanceNaturalObservationPortfolioSnapshot(
            portfolio,
            Array.AsReadOnly(ordered));
    }
}
