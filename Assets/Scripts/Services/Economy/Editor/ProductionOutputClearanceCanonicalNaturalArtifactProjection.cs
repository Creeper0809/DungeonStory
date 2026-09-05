#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Artifact-safe projection of one exact live output slice. Runtime identities are
/// deliberately absent: the raw receipt remains the forensic/join authority while
/// committed artifacts receive only deterministic semantic aliases.
/// </summary>
public sealed class ProductionOutputClearanceCanonicalNaturalOutputSliceRecord
{
    public const string Schema =
        "production-output-clearance-natural-artifact-output-slice@1";

    internal ProductionOutputClearanceCanonicalNaturalOutputSliceRecord(
        string observationId,
        string batchSemanticId,
        ProductionOutputClearanceExecutionOutputSliceSnapshot source,
        int sliceOrdinal)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        ObservationId = Canonical(observationId, nameof(observationId));
        BatchSemanticId = Canonical(batchSemanticId, nameof(batchSemanticId));
        OutputLineId = Canonical(source.OutputLineId, nameof(source.OutputLineId));
        ItemId = Canonical(source.ItemId, nameof(source.ItemId));
        if (sliceOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(sliceOrdinal));
        if (source.Quantity <= 0 || source.MassGrams <= 0L)
            throw new InvalidOperationException(
                "A canonical natural output slice must remain physical.");
        RequireDigest(source.CapabilityFingerprint, nameof(source));

        SliceOrdinal = sliceOrdinal;
        StackSemanticId = "natural-stack:" + ObservationId + ":"
            + OutputLineId + ":"
            + SliceOrdinal.ToString("D4", CultureInfo.InvariantCulture);
        ItemInstanceSemanticId = string.IsNullOrEmpty(source.ItemInstanceId)
            ? string.Empty
            : "natural-item-instance:" + ObservationId + ":"
                + OutputLineId + ":"
                + SliceOrdinal.ToString("D4", CultureInfo.InvariantCulture);
        Quantity = source.Quantity;
        MassGrams = source.MassGrams;
        CapabilityFingerprint = source.CapabilityFingerprint;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ObservationId);
        digest.Append(BatchSemanticId);
        digest.Append(OutputLineId);
        digest.Append(ItemId);
        digest.Append(ItemInstanceSemanticId);
        digest.Append(StackSemanticId);
        digest.Append(SliceOrdinal);
        digest.Append(Quantity);
        digest.Append(MassGrams);
        digest.Append(CapabilityFingerprint);
        SemanticDigest = digest.ComputeSha256();
    }

    public string ObservationId { get; }
    public string BatchSemanticId { get; }
    public string OutputLineId { get; }
    public string ItemId { get; }
    public string ItemInstanceSemanticId { get; }
    public string StackSemanticId { get; }
    public int SliceOrdinal { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string CapabilityFingerprint { get; }
    public string SemanticDigest { get; }

    private static string Canonical(string value, string parameter)
    {
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            value,
            parameter);
        if (value.Any(char.IsWhiteSpace))
            throw new ArgumentException(
                "A canonical natural artifact identity cannot contain whitespace.",
                parameter);
        return value;
    }

    private static void RequireDigest(string value, string parameter)
    {
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(value))
            throw new ArgumentException("A lowercase SHA-256 digest is required.", parameter);
    }
}

/// <summary>
/// Immutable committed-artifact row projected only after raw execution evidence
/// has formed an exact receipt/telemetry/physical-stack join.
/// </summary>
public sealed class ProductionOutputClearanceCanonicalNaturalObservationRecord
{
    public const string Schema =
        "production-output-clearance-natural-artifact-observation@1";

    internal ProductionOutputClearanceCanonicalNaturalObservationRecord(
        ProductionOutputClearanceNaturalShardEvidence evidence,
        IReadOnlyList<ProductionOutputClearanceCanonicalNaturalOutputSliceRecord>
            outputSlices,
        IReadOnlyList<string> routeBatchSemanticIds)
    {
        if (evidence == null)
            throw new ArgumentNullException(nameof(evidence));
        ProductionOutputClearanceNaturalObservationRecord source =
            evidence.Observation;
        ProductionOutputClearanceExecutionReceiptSnapshot receipt = evidence.Receipt;
        Fixture = source.Fixture;
        ObservationId = Fixture.ObservationId;
        PayloadKind = receipt.Descriptor.Payload.PayloadKind;
        FacilitySemanticId = "natural-facility:" + ObservationId + ":producer";
        OperationSemanticId = "natural-operation:" + ObservationId + ":"
            + PayloadKind + ":0000";
        BatchSemanticId = "natural-batch:" + ObservationId + ":aggregate";
        RouteBatchSemanticIds = Array.AsReadOnly((routeBatchSemanticIds
                ?? throw new ArgumentNullException(nameof(routeBatchSemanticIds)))
            .ToArray());
        OutputSlices = Array.AsReadOnly((outputSlices
                ?? throw new ArgumentNullException(nameof(outputSlices)))
            .ToArray());
        if (OutputSlices.Count == 0
            || OutputSlices.Any(value => value == null
                || !string.Equals(value.ObservationId, ObservationId,
                    StringComparison.Ordinal)
                || !string.Equals(value.BatchSemanticId, BatchSemanticId,
                    StringComparison.Ordinal))
            || RouteBatchSemanticIds.Count == 0
            || RouteBatchSemanticIds.Any(value => string.IsNullOrWhiteSpace(value))
            || RouteBatchSemanticIds.Distinct(StringComparer.Ordinal).Count()
                != RouteBatchSemanticIds.Count)
        {
            throw new InvalidOperationException(
                "Canonical natural artifact aliases are incomplete or duplicated.");
        }

        ActualBatchMassGrams = source.ActualBatchMassGrams;
        TopologySourceDigest = source.TopologySourceDigest;
        TopologyStable = source.TopologyStable;
        FacilityAttributionExact = source.FacilityAttributionExact;
        OwnerRosterKey = source.OwnerRosterKey;
        ActionEpochDelta = source.ActionEpochDelta;
        ActionStartDelta = source.ActionStartDelta;
        HaulStartDelta = source.HaulStartDelta;
        ClearanceMicroHours = source.ClearanceMicroHours;
        ClearanceMilliHours = source.ClearanceMilliHours;
        TelemetryCompletedCount = source.TelemetryCompletedCount;
        TelemetryActiveCount = source.TelemetryActiveCount;
        OrphanPickupCount = source.OrphanPickupCount;
        ConflictingPublicationCount = source.ConflictingPublicationCount;
        OverPickupCount = source.OverPickupCount;
        CapacityExceededCount = source.CapacityExceededCount;
        RestoreInterruptionCount = source.RestoreInterruptionCount;
        TelemetryClean = source.TelemetryClean;
        SchedulerProvenanceExact = source.SchedulerProvenanceExact;
        DeliveryExact = source.DeliveryExact;
        RandomStateDigest = source.RandomStateDigest;
        RandomDrawDelta = source.RandomDrawDelta;

        CanonicalSemanticDigestBuilder planned = new();
        planned.Append("production-output-clearance-natural-artifact-planned@1");
        planned.Append(receipt.Descriptor.SourceDigest);
        planned.Append(Fixture.SourceDigest);
        CanonicalPlannedOutputDigest = planned.ComputeSha256();

        CanonicalSemanticDigestBuilder outcome = new();
        outcome.Append("production-output-clearance-natural-artifact-outcome@1");
        outcome.Append(Fixture.SourceDigest);
        outcome.Append(OutputSlices.Count);
        foreach (ProductionOutputClearanceCanonicalNaturalOutputSliceRecord slice in
                 OutputSlices)
        {
            outcome.Append(slice.OutputLineId);
            outcome.Append(slice.ItemId);
            outcome.Append(!string.IsNullOrEmpty(slice.ItemInstanceSemanticId));
            outcome.Append(slice.Quantity);
            outcome.Append(slice.MassGrams);
            outcome.Append(slice.CapabilityFingerprint);
        }
        CanonicalOutcomeDigest = outcome.ComputeSha256();

        CanonicalSemanticDigestBuilder vector = new();
        vector.Append("production-output-clearance-natural-artifact-vector@1");
        vector.Append(receipt.Descriptor.SourceDigest);
        vector.Append(OutputSlices.Count);
        foreach (ProductionOutputClearanceCanonicalNaturalOutputSliceRecord slice in
                 OutputSlices)
            vector.Append(slice.SemanticDigest);
        CanonicalResolvedOutputVectorDigest = vector.ComputeSha256();

        CanonicalSemanticDigestBuilder receiptDigest = new();
        receiptDigest.Append("production-output-clearance-natural-artifact-receipt@1");
        receiptDigest.Append(evidence.Request.SourceDigest);
        receiptDigest.Append(FacilitySemanticId);
        receiptDigest.Append(OperationSemanticId);
        receiptDigest.Append(BatchSemanticId);
        receiptDigest.Append(RouteBatchSemanticIds.Count);
        foreach (string routeBatch in RouteBatchSemanticIds)
            receiptDigest.Append(routeBatch);
        receiptDigest.Append(CanonicalOutcomeDigest);
        receiptDigest.Append(CanonicalPlannedOutputDigest);
        receiptDigest.Append(CanonicalResolvedOutputVectorDigest);
        receiptDigest.Append(ActualBatchMassGrams);
        receiptDigest.Append(OutputSlices.Count);
        foreach (ProductionOutputClearanceCanonicalNaturalOutputSliceRecord slice in
                 OutputSlices)
            receiptDigest.Append(slice.SemanticDigest);
        receiptDigest.Append(receipt.HandlerId);
        receiptDigest.Append(receipt.HandlerVersion);
        receiptDigest.Append(true); // raw receipt/telemetry/physical join validated
        CanonicalReceiptDigest = receiptDigest.ComputeSha256();

        CanonicalSemanticDigestBuilder run = new();
        run.Append(Schema);
        run.Append(Fixture.SourceDigest);
        run.Append(ObservationId);
        run.Append(FacilitySemanticId);
        run.Append(OperationSemanticId);
        run.Append(BatchSemanticId);
        run.Append(CanonicalReceiptDigest);
        run.Append(CanonicalResolvedOutputVectorDigest);
        run.Append(ActualBatchMassGrams);
        run.Append(TopologySourceDigest);
        run.Append(TopologyStable);
        run.Append(FacilityAttributionExact);
        run.Append(OwnerRosterKey);
        run.Append(ActionEpochDelta);
        run.Append(ActionStartDelta);
        run.Append(HaulStartDelta);
        run.Append(ClearanceMicroHours);
        run.Append(ClearanceMilliHours);
        run.Append(TelemetryCompletedCount);
        run.Append(TelemetryActiveCount);
        run.Append(OrphanPickupCount);
        run.Append(ConflictingPublicationCount);
        run.Append(OverPickupCount);
        run.Append(CapacityExceededCount);
        run.Append(RestoreInterruptionCount);
        run.Append(TelemetryClean);
        run.Append(SchedulerProvenanceExact);
        run.Append(DeliveryExact);
        run.Append(RandomStateDigest);
        run.Append(RandomDrawDelta);
        CanonicalRunDigest = run.ComputeSha256();

        ProfileObservation = new ProductionOutputClearanceProfileObservation(
            Fixture.Plan.DefinitionId,
            Fixture.Plan.WorkstationTag,
            Fixture.DeterministicSeed,
            BatchSemanticId,
            ClearanceMicroHours,
            CanonicalRunDigest,
            ActualBatchMassGrams);
    }

    public ProductionOutputClearanceMeasurementFixture Fixture { get; }
    public string ObservationId { get; }
    public string PayloadKind { get; }
    public string FacilitySemanticId { get; }
    public string OperationSemanticId { get; }
    public string BatchSemanticId { get; }
    public IReadOnlyList<string> RouteBatchSemanticIds { get; }
    public IReadOnlyList<ProductionOutputClearanceCanonicalNaturalOutputSliceRecord>
        OutputSlices { get; }
    public string CanonicalOutcomeDigest { get; }
    public string CanonicalPlannedOutputDigest { get; }
    public string CanonicalResolvedOutputVectorDigest { get; }
    public string CanonicalReceiptDigest { get; }
    public long ActualBatchMassGrams { get; }
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
    public string CanonicalRunDigest { get; }
    public ProductionOutputClearanceProfileObservation ProfileObservation { get; }
}

public sealed class ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot
{
    public const string Schema =
        "production-output-clearance-natural-artifact-portfolio@1";

    internal ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot(
        string measurementPortfolioDigest,
        IReadOnlyList<ProductionOutputClearanceCanonicalNaturalObservationRecord>
            records)
    {
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                measurementPortfolioDigest))
        {
            throw new ArgumentException(
                "A measurement-portfolio SHA-256 digest is required.",
                nameof(measurementPortfolioDigest));
        }
        MeasurementPortfolioDigest = measurementPortfolioDigest;
        ProductionOutputClearanceCanonicalNaturalObservationRecord[] ordered =
            (records ?? throw new ArgumentNullException(nameof(records)))
            .OrderBy(value => value?.Fixture.Plan.DefinitionId,
                StringComparer.Ordinal)
            .ThenBy(value => value?.Fixture.Plan.WorkstationTag,
                StringComparer.Ordinal)
            .ThenBy(value => value?.Fixture.SeedIndex)
            .ThenBy(value => value?.Fixture.DeterministicSeed)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null)
            || ordered.Select(value => value.ObservationId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Canonical natural artifact records are empty, null, or duplicated.");
        }
        Records = Array.AsReadOnly(ordered);
        OutputSlices = Array.AsReadOnly(ordered
            .SelectMany(value => value.OutputSlices)
            .ToArray());
        ProfileObservations = Array.AsReadOnly(ordered
            .Select(value => value.ProfileObservation)
            .ToArray());

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(MeasurementPortfolioDigest);
        digest.Append(Records.Count);
        foreach (ProductionOutputClearanceCanonicalNaturalObservationRecord record in
                 Records)
            digest.Append(record.CanonicalRunDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string MeasurementPortfolioDigest { get; }
    public IReadOnlyList<ProductionOutputClearanceCanonicalNaturalObservationRecord>
        Records { get; }
    public IReadOnlyList<ProductionOutputClearanceCanonicalNaturalOutputSliceRecord>
        OutputSlices { get; }
    public IReadOnlyList<ProductionOutputClearanceProfileObservation>
        ProfileObservations { get; }
    public string SourceDigest { get; }

    /// <summary>
    /// Deterministic token stream for focused A/B tests. Production CSV formatting
    /// remains owned by the coordinator writer.
    /// </summary>
    public string CaptureCanonicalDebugText()
    {
        StringBuilder text = new();
        Append(text, Schema);
        Append(text, MeasurementPortfolioDigest);
        Append(text, SourceDigest);
        foreach (ProductionOutputClearanceCanonicalNaturalObservationRecord record in
                 Records)
        {
            Append(text, record.ObservationId);
            Append(text, record.FacilitySemanticId);
            Append(text, record.OperationSemanticId);
            Append(text, record.BatchSemanticId);
            Append(text, record.CanonicalOutcomeDigest);
            Append(text, record.CanonicalPlannedOutputDigest);
            Append(text, record.CanonicalResolvedOutputVectorDigest);
            Append(text, record.CanonicalReceiptDigest);
            Append(text, record.CanonicalRunDigest);
            Append(text, record.ActualBatchMassGrams.ToString(
                CultureInfo.InvariantCulture));
            foreach (string route in record.RouteBatchSemanticIds)
                Append(text, route);
            foreach (ProductionOutputClearanceCanonicalNaturalOutputSliceRecord slice in
                     record.OutputSlices)
            {
                Append(text, slice.OutputLineId);
                Append(text, slice.ItemId);
                Append(text, slice.ItemInstanceSemanticId);
                Append(text, slice.StackSemanticId);
                Append(text, slice.SliceOrdinal.ToString(
                    CultureInfo.InvariantCulture));
                Append(text, slice.Quantity.ToString(CultureInfo.InvariantCulture));
                Append(text, slice.MassGrams.ToString(CultureInfo.InvariantCulture));
                Append(text, slice.CapabilityFingerprint);
                Append(text, slice.SemanticDigest);
            }
        }
        return text.ToString();
    }

    private static void Append(StringBuilder text, string value)
    {
        string token = value ?? string.Empty;
        text.Append(token.Length.ToString(CultureInfo.InvariantCulture));
        text.Append(':');
        text.Append(token);
        text.Append('\n');
    }
}

public static class ProductionOutputClearanceCanonicalNaturalArtifactProjection
{
    public static ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot
        Build(
            ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio,
            ProductionOutputClearanceNaturalObservationPortfolioSnapshot accepted,
            IReadOnlyList<ProductionOutputClearanceNaturalShardEvidence> evidence)
    {
        if (portfolio == null)
            throw new ArgumentNullException(nameof(portfolio));
        if (accepted == null)
            throw new ArgumentNullException(nameof(accepted));
        if (!ReferenceEquals(accepted.Portfolio, portfolio))
            throw new InvalidOperationException(
                "Canonical artifact projection received another accepted portfolio.");

        ProductionOutputClearanceNaturalShardEvidence[] orderedEvidence =
            ValidateAndOrderEvidence(evidence);
        if (orderedEvidence.Length != portfolio.Fixtures.Count
            || accepted.Records.Count != portfolio.Fixtures.Count)
        {
            throw new InvalidOperationException(
                "Canonical artifact projection requires the complete frozen portfolio.");
        }
        Dictionary<string, ProductionOutputClearanceNaturalShardEvidence> byObservation =
            orderedEvidence.ToDictionary(
                value => value.Request.Fixture.ObservationId,
                StringComparer.Ordinal);
        for (int index = 0; index < accepted.Records.Count; index++)
        {
            ProductionOutputClearanceNaturalObservationRecord record =
                accepted.Records[index];
            if (!byObservation.TryGetValue(record.Fixture.ObservationId,
                    out ProductionOutputClearanceNaturalShardEvidence value)
                || !ReferenceEquals(value.Observation, record))
            {
                throw new InvalidOperationException(
                    "Accepted observations and exact raw evidence are not a bijection.");
            }
        }

        return BuildValidated(portfolio.SourceDigest, orderedEvidence);
    }

    public static ProductionOutputClearanceCanonicalNaturalObservationRecord
        ProjectSingle(ProductionOutputClearanceNaturalShardEvidence evidence)
    {
        ProductionOutputClearanceNaturalShardEvidence[] validated =
            ValidateAndOrderEvidence(new[] { evidence });
        return ProjectValidated(validated[0]);
    }

    internal static
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot
        BuildFocused(
            string measurementPortfolioDigest,
            IReadOnlyList<ProductionOutputClearanceNaturalShardEvidence> evidence)
    {
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                measurementPortfolioDigest))
            throw new ArgumentException(
                "A focused projection requires a portfolio digest.",
                nameof(measurementPortfolioDigest));
        return BuildValidated(
            measurementPortfolioDigest,
            ValidateAndOrderEvidence(evidence));
    }

    private static
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot
        BuildValidated(
            string measurementPortfolioDigest,
            IReadOnlyList<ProductionOutputClearanceNaturalShardEvidence> evidence)
    {
        ProductionOutputClearanceCanonicalNaturalObservationRecord[] records = evidence
            .Select(ProjectValidated)
            .ToArray();
        return new ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot(
            measurementPortfolioDigest,
            records);
    }

    private static ProductionOutputClearanceCanonicalNaturalObservationRecord
        ProjectValidated(ProductionOutputClearanceNaturalShardEvidence evidence)
    {
        string observationId = evidence.Request.Fixture.ObservationId;
        string batchSemanticId = "natural-batch:" + observationId + ":aggregate";
        ProductionOutputClearanceExecutionOutputSliceSnapshot[] semanticOutputs =
            evidence.Receipt.Outputs
                .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                .ThenBy(value => value.ItemId, StringComparer.Ordinal)
                .ThenBy(value => value.Quantity)
                .ThenBy(value => value.MassGrams)
                .ThenBy(value => value.CapabilityFingerprint,
                    StringComparer.Ordinal)
                .ThenBy(value => string.IsNullOrEmpty(value.ItemInstanceId) ? 0 : 1)
                .ToArray();
        Dictionary<string, int> ordinalByLine = new(StringComparer.Ordinal);
        List<ProductionOutputClearanceCanonicalNaturalOutputSliceRecord> slices =
            new(semanticOutputs.Length);
        foreach (ProductionOutputClearanceExecutionOutputSliceSnapshot output in
                 semanticOutputs)
        {
            int ordinal = ordinalByLine.TryGetValue(output.OutputLineId, out int next)
                ? next
                : 0;
            ordinalByLine[output.OutputLineId] = checked(ordinal + 1);
            slices.Add(new ProductionOutputClearanceCanonicalNaturalOutputSliceRecord(
                observationId,
                batchSemanticId,
                output,
                ordinal));
        }

        string[] routeAliases = BuildRouteBatchAliases(evidence.Receipt, observationId);
        return new ProductionOutputClearanceCanonicalNaturalObservationRecord(
            evidence,
            slices,
            routeAliases);
    }

    private static string[] BuildRouteBatchAliases(
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        string observationId)
    {
        bool aggregateIsOnlyRoute = receipt.RouteBatchCommitIds.Count == 1
            && string.Equals(receipt.RouteBatchCommitIds[0], receipt.BatchCommitId,
                StringComparison.Ordinal);
        if (aggregateIsOnlyRoute)
            return new[] { "natural-batch:" + observationId + ":aggregate" };
        return Enumerable.Range(0, receipt.RouteBatchCommitIds.Count)
            .Select(index => "natural-batch:" + observationId + ":route:"
                + index.ToString("D4", CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static ProductionOutputClearanceNaturalShardEvidence[]
        ValidateAndOrderEvidence(
            IReadOnlyList<ProductionOutputClearanceNaturalShardEvidence> evidence)
    {
        ProductionOutputClearanceNaturalShardEvidence[] ordered = (evidence
                ?? throw new ArgumentNullException(nameof(evidence)))
            .OrderBy(value => value?.Request.Fixture.Plan.DefinitionId,
                StringComparer.Ordinal)
            .ThenBy(value => value?.Request.Fixture.Plan.WorkstationTag,
                StringComparer.Ordinal)
            .ThenBy(value => value?.Request.Fixture.SeedIndex)
            .ThenBy(value => value?.Request.Fixture.DeterministicSeed)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null))
            throw new InvalidOperationException(
                "Canonical artifact projection requires exact raw evidence.");
        if (ordered.Select(value => value.Request.Fixture.ObservationId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new InvalidOperationException(
                "Canonical artifact projection received duplicate observations.");
        foreach (ProductionOutputClearanceNaturalShardEvidence value in ordered)
            ValidateExactRawEvidence(value);
        return ordered;
    }

    private static void ValidateExactRawEvidence(
        ProductionOutputClearanceNaturalShardEvidence evidence)
    {
        ProductionOutputClearanceNaturalExecutionRequest request = evidence.Request;
        ProductionOutputClearanceExecutionReceiptSnapshot receipt = evidence.Receipt;
        ProductionOutputClearanceNaturalObservationRecord observation =
            evidence.Observation;
        if (request == null || receipt == null || observation == null
            || !ReferenceEquals(request.Descriptor, receipt.Descriptor)
            || !ReferenceEquals(request.Fixture, observation.Fixture)
            || !string.Equals(request.ActionId, receipt.ActionId,
                StringComparison.Ordinal)
            || !string.Equals(receipt.RuntimeFacilityId,
                observation.RuntimeFacilityId, StringComparison.Ordinal)
            || !string.Equals(receipt.BatchCommitId,
                observation.BatchCommitId, StringComparison.Ordinal)
            || !string.Equals(receipt.ResolvedOutputVectorDigest,
                observation.ResolvedOutputVectorDigest, StringComparison.Ordinal)
            || receipt.ActualBatchMassGrams != observation.ActualBatchMassGrams
            || receipt.ActualBatchMassGrams != receipt.Outputs.Sum(value =>
                value?.MassGrams ?? 0L)
            || !observation.IsExact
            || receipt.Outputs.Count == 0
            || receipt.Outputs.Any(value => value == null
                || value.Quantity <= 0
                || value.MassGrams <= 0L
                || !Digest(value.SourceDigest)
                || !Digest(value.CapabilityFingerprint))
            || receipt.Outputs.Select(value => value.StackId)
                .Distinct(StringComparer.Ordinal).Count() != receipt.Outputs.Count
            || receipt.RouteBatchCommitIds.Count == 0
            || receipt.RouteBatchCommitIds.Distinct(StringComparer.Ordinal).Count()
                != receipt.RouteBatchCommitIds.Count
            || observation.TelemetryCompletedCount
                != receipt.RouteBatchCommitIds.Count
            || !Digest(evidence.SourceDigest)
            || !Digest(request.SourceDigest)
            || !Digest(receipt.SourceDigest)
            || !Digest(receipt.RuntimeReceiptDigest)
            || !Digest(observation.RunSourceDigest))
        {
            throw new InvalidOperationException(
                "Canonical artifact projection rejected inexact raw execution evidence.");
        }
    }

    private static bool Digest(string value) =>
        ProductionOutputClearanceProfileObservation.IsLowercaseSha256(value);
}
#endif
