#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;

/// <summary>
/// Terminal state for the production half of a natural measurement. The
/// driver must reach this state only after an actual gameplay command has
/// completed and its domain receipt is queryable by action ID.
/// </summary>
public sealed class ProductionOutputClearanceNaturalProductionStageResult
{
    public bool IsTerminal { get; private set; }
    public bool Succeeded { get; private set; }
    public string FailureReason { get; private set; } = string.Empty;

    public void Complete()
    {
        EnsureMutable();
        IsTerminal = true;
        Succeeded = true;
    }

    public void Fail(string failureReason)
    {
        EnsureMutable();
        FailureReason = Canonical(failureReason, nameof(failureReason));
        IsTerminal = true;
        Succeeded = false;
    }

    private void EnsureMutable()
    {
        if (IsTerminal)
            throw new InvalidOperationException(
                "Natural production stage is already terminal.");
    }

    internal static string Canonical(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical natural-execution token is required.",
                parameter);
        }
        return value;
    }
}

/// <summary>
/// Actual runtime observations captured around scheduler-owned output
/// clearance. This type deliberately contains no descriptor-derived output
/// values. Receipt/output identity is joined by the assembler.
/// </summary>
public sealed class ProductionOutputClearanceNaturalClearanceWitness
{
    public ProductionOutputClearanceNaturalClearanceWitness(
        string topologySourceDigest,
        string topologyBeforeDigest,
        string topologyAfterDigest,
        string ownerRosterKey,
        long actionEpochDelta,
        long actionStartDelta,
        long haulStartDelta,
        FacilityOutputClearanceTelemetrySnapshot telemetry,
        bool schedulerProvenanceExact,
        bool deliveryExact,
        string randomStateDigest,
        long randomDrawDelta)
    {
        RequireDigest(topologySourceDigest, nameof(topologySourceDigest));
        RequireDigest(topologyBeforeDigest, nameof(topologyBeforeDigest));
        RequireDigest(topologyAfterDigest, nameof(topologyAfterDigest));
        RequireDigest(randomStateDigest, nameof(randomStateDigest));
        OwnerRosterKey = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(ownerRosterKey, nameof(ownerRosterKey));
        if (actionEpochDelta < 0L
            || actionStartDelta < 0L
            || haulStartDelta < 0L
            || randomDrawDelta < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actionEpochDelta),
                "Natural clearance deltas cannot be negative.");
        }

        TopologySourceDigest = topologySourceDigest;
        TopologyBeforeDigest = topologyBeforeDigest;
        TopologyAfterDigest = topologyAfterDigest;
        ActionEpochDelta = actionEpochDelta;
        ActionStartDelta = actionStartDelta;
        HaulStartDelta = haulStartDelta;
        Telemetry = telemetry;
        SchedulerProvenanceExact = schedulerProvenanceExact;
        DeliveryExact = deliveryExact;
        RandomStateDigest = randomStateDigest;
        RandomDrawDelta = randomDrawDelta;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-witness@1");
        digest.Append(TopologySourceDigest);
        digest.Append(TopologyBeforeDigest);
        digest.Append(TopologyAfterDigest);
        digest.Append(OwnerRosterKey);
        digest.Append(ActionEpochDelta);
        digest.Append(ActionStartDelta);
        digest.Append(HaulStartDelta);
        digest.Append(Telemetry.Completed?.Count ?? 0);
        foreach (FacilityOutputClearanceSampleSnapshot sample in
                 (Telemetry.Completed ?? Array.Empty<
                     FacilityOutputClearanceSampleSnapshot>()))
        {
            digest.Append(sample.BatchCommitId);
            digest.Append(sample.FacilityId);
            digest.Append(sample.BatchMassGrams);
            digest.Append(sample.PublishedAtMicroGameHours);
            digest.Append(sample.ClearedAtMicroGameHours);
        }
        digest.Append(Telemetry.ActiveBatchCount);
        digest.Append(Telemetry.OrphanPickupCount);
        digest.Append(Telemetry.ConflictingPublicationCount);
        digest.Append(Telemetry.OverPickupCount);
        digest.Append(Telemetry.CapacityExceededCount);
        digest.Append(Telemetry.RestoreInterruptionCount);
        digest.Append(SchedulerProvenanceExact);
        digest.Append(DeliveryExact);
        digest.Append(RandomStateDigest);
        digest.Append(RandomDrawDelta);
        SourceDigest = digest.ComputeSha256();
    }

    public string TopologySourceDigest { get; }
    public string TopologyBeforeDigest { get; }
    public string TopologyAfterDigest { get; }
    public string OwnerRosterKey { get; }
    public long ActionEpochDelta { get; }
    public long ActionStartDelta { get; }
    public long HaulStartDelta { get; }
    public FacilityOutputClearanceTelemetrySnapshot Telemetry { get; }
    public bool SchedulerProvenanceExact { get; }
    public bool DeliveryExact { get; }
    public string RandomStateDigest { get; }
    public long RandomDrawDelta { get; }
    public string SourceDigest { get; }

    private static void RequireDigest(string value, string parameter)
    {
        if (!ProductionOutputClearanceProfileObservation
                .IsLowercaseSha256(value))
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.", parameter);
    }
}

public sealed class ProductionOutputClearanceNaturalClearanceStageResult
{
    public bool IsTerminal { get; private set; }
    public bool Succeeded { get; private set; }
    public string FailureReason { get; private set; } = string.Empty;
    public ProductionOutputClearanceNaturalClearanceWitness Witness
        { get; private set; }

    public void Complete(
        ProductionOutputClearanceNaturalClearanceWitness witness)
    {
        EnsureMutable();
        Witness = witness ?? throw new ArgumentNullException(nameof(witness));
        IsTerminal = true;
        Succeeded = true;
    }

    public void Fail(string failureReason)
    {
        EnsureMutable();
        FailureReason = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(failureReason, nameof(failureReason));
        Witness = null;
        IsTerminal = true;
        Succeeded = false;
    }

    private void EnsureMutable()
    {
        if (IsTerminal)
            throw new InvalidOperationException(
                "Natural clearance stage is already terminal.");
    }
}

/// <summary>
/// Implemented by the PlayMode fixture layer. The production stage must invoke
/// the real domain command and leave an actual completion receipt available to
/// the injected handler. The clearance stage must use scheduler-owned AI haul
/// and actual telemetry; it may not move stacks directly.
/// </summary>
public interface IProductionOutputClearanceNaturalLiveScenarioDriver
{
    string DriverId { get; }
    int ContractVersion { get; }
    string PayloadKind { get; }

    IEnumerator ExecuteProduction(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalProductionStageResult result);

    IEnumerator ExecuteClearance(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        ProductionOutputClearanceNaturalClearanceStageResult result);

    /// <summary>
    /// Releases the live fixture only after the executor has attempted the
    /// completion-receipt acknowledgement. Implementations must also accept an
    /// unsuccessful acknowledgement so exceptional and rejected runs cannot
    /// leak their fixture. This operation must be idempotent for an already
    /// aborted run and must never acknowledge or replace a domain receipt.
    /// </summary>
    bool TryFinalize(
        ProductionOutputClearanceNaturalExecutionRequest request,
        bool receiptAccepted,
        out string failureReason);
}

public static class ProductionOutputClearanceNaturalEvidenceAssembler
{
    public static ProductionOutputClearanceNaturalShardEvidence Assemble(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        ProductionOutputClearanceNaturalClearanceWitness witness)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (receipt == null) throw new ArgumentNullException(nameof(receipt));
        if (witness == null) throw new ArgumentNullException(nameof(witness));
        if (!ReferenceEquals(request.Descriptor, receipt.Descriptor)
            || !string.Equals(request.ActionId, receipt.ActionId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Natural evidence receipt is owned by another request.");
        }

        FacilityOutputClearanceTelemetrySnapshot telemetry = witness.Telemetry;
        FacilityOutputClearanceSampleSnapshot[] completed =
            (telemetry.Completed ?? Array.Empty<
                FacilityOutputClearanceSampleSnapshot>())
            .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ToArray();
        string[] expectedRouteBatchCommitIds = receipt.RouteBatchCommitIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool routeIdentityExact = completed.Length
                == expectedRouteBatchCommitIds.Length
            && completed.Select(value => value.BatchCommitId)
                .SequenceEqual(expectedRouteBatchCommitIds, StringComparer.Ordinal);
        bool facilityExact = completed.All(value => string.Equals(
            value.FacilityId,
            receipt.RuntimeFacilityId,
            StringComparison.Ordinal));
        long completedMassGrams = completed.Aggregate(
            0L,
            (total, value) => checked(total + value.BatchMassGrams));
        long aggregateClearanceMicroHours = completed.Length == 0
            ? 0L
            : checked(completed.Max(value => value.ClearedAtMicroGameHours)
                - completed.Min(value => value.PublishedAtMicroGameHours));
        if (!routeIdentityExact
            || !facilityExact
            || completedMassGrams != receipt.ActualBatchMassGrams
            || completed.Any(value => value.ClearanceMicroHours <= 0L)
            || aggregateClearanceMicroHours <= 0L)
        {
            throw new InvalidOperationException(
                "Natural clearance telemetry does not exactly join its receipt.");
        }

        ProductionOutputClearanceNaturalObservationRecord observation = new(
            request.Fixture,
            receipt.RuntimeFacilityId,
            receipt.ResolvedOutputVectorDigest,
            receipt.ActualBatchMassGrams,
            receipt.BatchCommitId,
            witness.TopologySourceDigest,
            string.Equals(witness.TopologyBeforeDigest,
                witness.TopologyAfterDigest, StringComparison.Ordinal),
            true,
            witness.OwnerRosterKey,
            witness.ActionEpochDelta,
            witness.ActionStartDelta,
            witness.HaulStartDelta,
            aggregateClearanceMicroHours,
            completed.Length,
            telemetry.ActiveBatchCount,
            telemetry.OrphanPickupCount,
            telemetry.ConflictingPublicationCount,
            telemetry.OverPickupCount,
            telemetry.CapacityExceededCount,
            telemetry.RestoreInterruptionCount,
            telemetry.IsClean,
            witness.SchedulerProvenanceExact,
            witness.DeliveryExact,
            witness.RandomStateDigest,
            witness.RandomDrawDelta);
        if (!observation.IsExact)
            throw new InvalidOperationException(
                "Natural clearance observation is not exact.");
        return new ProductionOutputClearanceNaturalShardEvidence(
            request,
            receipt,
            observation);
    }
}

public abstract class ProductionOutputClearanceSpecialNaturalExecutor<TPayload> :
    IProductionOutputClearanceNaturalMeasurementExecutor
    where TPayload : class, IProductionOutputClearanceExecutablePayload
{
    private readonly IProductionOutputClearanceNaturalLiveScenarioDriver driver;
    private readonly IProductionOutputClearanceNaturalMeasurementHandler handler;
    private readonly ProductionOutputClearanceNaturalMeasurementHandlerRegistry
        handlerRegistry;

    protected ProductionOutputClearanceSpecialNaturalExecutor(
        string executorId,
        int contractVersion,
        string payloadKind,
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        IProductionOutputClearanceNaturalMeasurementHandler handler)
    {
        ExecutorId = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(executorId, nameof(executorId));
        PayloadKind = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(payloadKind, nameof(payloadKind));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        ContractVersion = contractVersion;
        this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        if (!string.Equals(driver.PayloadKind, PayloadKind,
                StringComparison.Ordinal)
            || !string.Equals(handler.PayloadKind, PayloadKind,
                StringComparison.Ordinal)
            || driver.ContractVersion <= 0
            || handler.ContractVersion <= 0)
        {
            throw new InvalidOperationException(
                "Natural executor dependencies do not own its payload kind.");
        }
    }

    protected ProductionOutputClearanceSpecialNaturalExecutor(
        string executorId,
        int contractVersion,
        string payloadKind,
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry
            handlerRegistry)
    {
        ExecutorId = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(executorId, nameof(executorId));
        PayloadKind = ProductionOutputClearanceNaturalProductionStageResult
            .Canonical(payloadKind, nameof(payloadKind));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        ContractVersion = contractVersion;
        this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
        this.handlerRegistry = handlerRegistry
            ?? throw new ArgumentNullException(nameof(handlerRegistry));
        if (!string.Equals(driver.PayloadKind, PayloadKind,
                StringComparison.Ordinal)
            || driver.ContractVersion <= 0
            || !handlerRegistry.PayloadKinds.Contains(
                PayloadKind,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Natural executor dependencies do not own its payload kind.");
        }
    }

    public string ExecutorId { get; }
    public int ContractVersion { get; }
    public string PayloadKind { get; }

    public IEnumerator Execute(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalExecutionResult result)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (result.IsTerminal)
            throw new InvalidOperationException(
                "A terminal natural execution result cannot be reused.");
        if (request.Descriptor.Payload is not TPayload
            || !string.Equals(request.Descriptor.Payload.PayloadKind,
                PayloadKind, StringComparison.Ordinal))
        {
            result.Fail("natural-executor-payload-mismatch");
            yield break;
        }

        ProductionOutputClearanceNaturalProductionStageResult production = new();
        IEnumerator productionExecution = driver.ExecuteProduction(
            request,
            production);
        if (productionExecution == null)
            throw new InvalidOperationException(
                "Natural production driver returned a null coroutine: "
                + driver.DriverId);
        try
        {
            while (productionExecution.MoveNext())
                yield return productionExecution.Current;
        }
        finally
        {
            (productionExecution as IDisposable)?.Dispose();
        }
        if (!production.IsTerminal)
            throw new InvalidOperationException(
                "Natural production driver returned without a terminal result: "
                + driver.DriverId);
        if (!production.Succeeded)
        {
            result.Fail("natural-production-failed:" + production.FailureReason);
            yield break;
        }

        bool receiptAccepted = false;
        ProductionOutputClearanceNaturalShardEvidence acceptedEvidence = null;
        try
        {
            if (!TryCaptureCompleted(
                    request.Descriptor,
                    request.ActionId,
                    out ProductionOutputClearanceExecutionReceiptSnapshot receipt,
                    out string captureFailure)
                || receipt == null)
            {
                result.Fail("natural-receipt-capture-failed:"
                    + RequireFailure(captureFailure));
                yield break;
            }

            ProductionOutputClearanceNaturalClearanceStageResult clearance = new();
            IEnumerator clearanceExecution = driver.ExecuteClearance(
                request,
                receipt,
                clearance);
            if (clearanceExecution == null)
                throw new InvalidOperationException(
                    "Natural clearance driver returned a null coroutine: "
                    + driver.DriverId);
            try
            {
                while (clearanceExecution.MoveNext())
                    yield return clearanceExecution.Current;
            }
            finally
            {
                (clearanceExecution as IDisposable)?.Dispose();
            }
            if (!clearance.IsTerminal)
                throw new InvalidOperationException(
                    "Natural clearance driver returned without a terminal result: "
                    + driver.DriverId);
            if (!clearance.Succeeded || clearance.Witness == null)
            {
                result.Fail("natural-clearance-failed:"
                    + RequireFailure(clearance.FailureReason));
                yield break;
            }

            ProductionOutputClearanceNaturalShardEvidence evidence =
                ProductionOutputClearanceNaturalEvidenceAssembler.Assemble(
                    request,
                    receipt,
                    clearance.Witness);
            if (!TryAcknowledgeAccepted(
                    receipt,
                    out string acknowledgeFailure))
            {
                result.Fail("natural-receipt-acknowledge-failed:"
                    + RequireFailure(acknowledgeFailure));
                yield break;
            }
            receiptAccepted = true;
            acceptedEvidence = evidence;
        }
        finally
        {
            if (!driver.TryFinalize(
                    request,
                    receiptAccepted,
                    out string finalizeFailure))
            {
                throw new InvalidOperationException(
                    "Natural live scenario finalization failed: "
                    + RequireFailure(finalizeFailure));
            }
        }
        result.Complete(acceptedEvidence
            ?? throw new InvalidOperationException(
                "Natural accepted evidence was not retained."));
    }

    private static string RequireFailure(string value) =>
        ProductionOutputClearanceNaturalProductionStageResult.Canonical(
            string.IsNullOrWhiteSpace(value)
                ? "unspecified"
                : value,
            nameof(value));

    private bool TryCaptureCompleted(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        string actionId,
        out ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason) => handlerRegistry != null
        ? handlerRegistry.TryCaptureCompleted(
            descriptor,
            actionId,
            out receipt,
            out failureReason)
        : handler.TryCaptureCompleted(
            descriptor,
            actionId,
            out receipt,
            out failureReason);

    private bool TryAcknowledgeAccepted(
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason) => handlerRegistry != null
        ? handlerRegistry.TryAcknowledgeAccepted(receipt, out failureReason)
        : handler.TryAcknowledgeAccepted(receipt, out failureReason);
}

public sealed class ProductionOutputClearanceCombatCraftNaturalExecutor :
    ProductionOutputClearanceSpecialNaturalExecutor<
        ProductionOutputClearanceCombatCraftExecutablePayload>
{
    public const string Id = "natural-measurement-executor:combat-craft";
    public const int Version = 1;

    public ProductionOutputClearanceCombatCraftNaturalExecutor(
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        IProductionOutputClearanceNaturalMeasurementHandler handler)
        : base(Id, Version, "combat-craft", driver, handler)
    {
    }

    public ProductionOutputClearanceCombatCraftNaturalExecutor(
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers)
        : base(Id, Version, "combat-craft", driver, handlers)
    {
    }
}

public sealed class ProductionOutputClearanceApparelNaturalExecutor :
    ProductionOutputClearanceSpecialNaturalExecutor<
        ProductionOutputClearanceApparelExecutablePayload>
{
    public const string Id = "natural-measurement-executor:apparel";
    public const int Version = 1;

    public ProductionOutputClearanceApparelNaturalExecutor(
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        IProductionOutputClearanceNaturalMeasurementHandler handler)
        : base(Id, Version, "apparel", driver, handler)
    {
    }

    public ProductionOutputClearanceApparelNaturalExecutor(
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers)
        : base(Id, Version, "apparel", driver, handlers)
    {
    }
}

public sealed class ProductionOutputClearanceCropHarvestNaturalExecutor :
    ProductionOutputClearanceSpecialNaturalExecutor<
        ProductionOutputClearanceCropHarvestExecutablePayload>
{
    public const string Id = "natural-measurement-executor:crop-harvest";
    public const int Version = 1;

    public ProductionOutputClearanceCropHarvestNaturalExecutor(
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        IProductionOutputClearanceNaturalMeasurementHandler handler)
        : base(Id, Version, "crop-harvest", driver, handler)
    {
    }

    public ProductionOutputClearanceCropHarvestNaturalExecutor(
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers)
        : base(Id, Version, "crop-harvest", driver, handlers)
    {
    }
}

public sealed class ProductionOutputClearanceCertifiedSeedNaturalExecutor :
    ProductionOutputClearanceSpecialNaturalExecutor<
        ProductionOutputClearanceCertifiedSeedExecutablePayload>
{
    public const string Id = "natural-measurement-executor:certified-seed";
    public const int Version = 1;

    public ProductionOutputClearanceCertifiedSeedNaturalExecutor(
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        IProductionOutputClearanceNaturalMeasurementHandler handler)
        : base(Id, Version, "certified-seed", driver, handler)
    {
    }

    public ProductionOutputClearanceCertifiedSeedNaturalExecutor(
        IProductionOutputClearanceNaturalLiveScenarioDriver driver,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers)
        : base(Id, Version, "certified-seed", driver, handlers)
    {
    }
}
#endif
