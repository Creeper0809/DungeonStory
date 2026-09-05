using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SensorProvenance =
    ProductionStockSensorDestructiveDrainCanonical.Provenance;

/// <summary>
/// Sixth destructive-drain participant. One upper owner coordinates both the
/// Items-owned stock-sensor socket custody and the Production-owned embedded
/// sensor. Lower receipts remain durable until the upper journal acknowledges
/// the exact composite receipt.
/// </summary>
public sealed class ProductionStockSensorDestructiveDrainParticipant :
    IProductionFacilityDestructiveDrainParticipant,
    IProductionFacilityDestructiveDrainDurablePrepareParticipant,
    IProductionFacilityDestructiveDrainCheckpointGcParticipant
{
    public const int CurrentContractVersion = 2;

    private const string ChildStepSuffix =
        ProductionStockSensorDestructiveDrainCanonical.ChildStepSuffix;

    private static readonly IReadOnlyList<string> Dependencies =
        Array.AsReadOnly(new[]
        {
            ProductionFacilityDestructiveDrainParticipantIds
                .PhysicalCustodyCarryRecovery
        });

    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly IProductionStockSensorDestructiveDrainPort sensor;
    private readonly IProductionInputDestinationCustodyDrainService inputDrain;
    private readonly IProductionAssemblyBridge facilities;
    private readonly IProductionStockSensorDestinationAuthorityRuntime
        destinationAuthority;
    private readonly IFacilityBufferDestinationClaimQuery claims;
    private readonly IFacilityBufferMassCapacityQuery capacities;
    private readonly IProductionInputDestinationCustodyDrainCheckpointGcPort
        inputCheckpointGc;
    private readonly IProductionStockSensorRemovalCheckpointGcPort
        sensorCheckpointGc;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public ProductionStockSensorDestructiveDrainParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionStockSensorDestructiveDrainPort sensor,
        IProductionInputDestinationCustodyDrainService inputDrain,
        IProductionAssemblyBridge facilities,
        IProductionStockSensorDestinationAuthorityRuntime destinationAuthority,
        IFacilityBufferDestinationClaimQuery claims,
        IFacilityBufferMassCapacityQuery capacities)
    {
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
        this.inputDrain = inputDrain
            ?? throw new ArgumentNullException(nameof(inputDrain));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.destinationAuthority = destinationAuthority
            ?? throw new ArgumentNullException(nameof(destinationAuthority));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        inputCheckpointGc = inputDrain as
            IProductionInputDestinationCustodyDrainCheckpointGcPort;
        sensorCheckpointGc = sensor as
            IProductionStockSensorRemovalCheckpointGcPort;
    }

    public string ParticipantId =>
        ProductionFacilityDestructiveDrainParticipantIds
            .StockSensorEmbeddedSalvage;

    public string CheckpointGcParticipantId => ParticipantId;

    public int ContractVersion => CurrentContractVersion;

    public IReadOnlyList<string> DependsOnParticipantIds => Dependencies;

    public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context)
    {
        ProductionOutputDestinationLifecycleContribution contribution =
            CaptureContribution(context.FacilityId);
        if (!contribution.HasAuthority)
        {
            ProductionFacilityDestructiveDrainOwnerPlan[] emptyOwners =
                Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>();
            return new ProductionFacilityDestructiveDrainParticipantPlan(
                ParticipantId,
                ContractVersion,
                contribution.DurableSemanticFingerprint,
                BuildPlanFingerprint(
                    context.FacilityId,
                    contribution.DurableSemanticFingerprint,
                    emptyOwners),
                emptyOwners);
        }

        CompositeCapture source = CaptureSource(context.OperationId,
            context.FacilityId);
        if (!source.HasOwner)
        {
            throw new InvalidOperationException(
                "production-stock-sensor-destructive-lifecycle-owner-conflict");
        }
        ProductionFacilityDestructiveDrainOwnerPlan[] owners = source.HasOwner
            ? new[]
            {
                new ProductionFacilityDestructiveDrainOwnerPlan(
                    source.OwnerStableId,
                    ProductionFacilityDestructiveDrainDisposition.Terminalize,
                    string.Empty,
                    source.RequestFingerprint)
            }
            : Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>();

        return new ProductionFacilityDestructiveDrainParticipantPlan(
            ParticipantId,
            ContractVersion,
            contribution.DurableSemanticFingerprint,
            BuildPlanFingerprint(
                context.FacilityId,
                contribution.DurableSemanticFingerprint,
                owners),
            owners);
    }

    [GameplayInternalOnly(
        "Persists the stock-sensor socket child and embedded removal source only after the upper destructive journal is durable.",
        "Production facility destructive-drain runtime only")]
    public bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!ValidateBaseContext(context)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.Planned
            || !string.IsNullOrEmpty(context.Owner.commitId)
            || !string.IsNullOrEmpty(context.Owner.receiptFingerprint))
        {
            failureReason =
                "production-stock-sensor-destructive-step-context-invalid";
            return false;
        }

        ProductionOutputDestinationLifecycleContribution contribution;
        try
        {
            contribution = CaptureContribution(context.FacilityId);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-stock-sensor-destructive-contribution-capture-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (!string.Equals(
                contribution.DurableSemanticFingerprint,
                context.ExpectedDurableContributionFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-stock-sensor-destructive-durable-contribution-drift";
            return false;
        }

        CompositeCapture source;
        try
        {
            source = CaptureSource(context.OperationId, context.FacilityId);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-stock-sensor-destructive-durable-capture-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (!source.HasOwner
            || !string.Equals(source.OwnerStableId,
                context.Owner.ownerStableId, StringComparison.Ordinal)
            || !string.Equals(source.StepOperationId,
                context.Owner.stepOperationId, StringComparison.Ordinal)
            || !string.Equals(source.RequestFingerprint,
                context.Owner.requestFingerprint, StringComparison.Ordinal))
        {
            failureReason =
                "production-stock-sensor-destructive-durable-plan-drift";
            return false;
        }

        ProductionInputDestinationCustodyDrainResult childPrepared;
        try
        {
            childPrepared = inputDrain.TryPrepare(source.ChildRequest);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-stock-sensor-destructive-child-prepare-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (childPrepared.Status is
                ProductionInputDestinationCustodyDrainStatus.Deferred
            or ProductionInputDestinationCustodyDrainStatus.Conflict
            || !inputDrain.TryCapture(
                source.ChildRequest.StepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData child)
            || !IsExactChild(child, source.ChildRequest))
        {
            failureReason = string.IsNullOrEmpty(childPrepared.FailureReason)
                ? "production-stock-sensor-destructive-child-prepare-invalid"
                : childPrepared.FailureReason;
            return false;
        }

        if (source.Installed != null && source.Removal == null)
        {
            if (!sensor.TryPrepareDurable(
                    context.FacilityId,
                    out ProductionStockSensorRemovalSaveData removal,
                    out failureReason)
                || removal?.phase != ProductionStockSensorRemovalPhase.Prepared
                || !SensorProvenance.Matches(source.Provenance, removal))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "production-stock-sensor-destructive-embedded-prepare-invalid"
                    : failureReason;
                return false;
            }
        }
        return true;
    }

    [GameplayInternalOnly(
        "Advances the stock-sensor physical child and embedded salvage to one composite receipt without acknowledging either lower owner.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!ValidateBaseContext(context)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.Planned
            || !string.IsNullOrEmpty(context.Owner.commitId)
            || !string.IsNullOrEmpty(context.Owner.receiptFingerprint)
            || !inputDrain.RequiresImmediateRecoveryBeforeGameplayTick)
        {
            return Conflict(current);
        }

        string childStep = context.Owner.stepOperationId + ChildStepSuffix;
        if (!inputDrain.TryCapture(
                childStep,
                out ProductionInputDestinationCustodyDrainSaveData child))
        {
            return Deferred(current);
        }
        if (!IsExactChildIdentity(child, context))
            return Conflict(current);

        if (!TryCaptureSensorState(
                context.FacilityId,
                out ProductionStockSensorPhysicalCommitSaveData pending,
                out ProductionInstalledStockSensorSaveData installed,
                out ProductionStockSensorRemovalSaveData removal,
                out SensorProvenance provenance)
            || !string.Equals(
                BuildRequestFingerprint(child.requestFingerprint, provenance),
                context.Owner.requestFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict(current);
        }

        bool replay = IsChildEffectCommitted(child.phase)
            && (removal == null || IsSensorEffectCommitted(removal.phase));

        if (pending != null)
        {
            if (!sensor.TryStabilizePendingInstallation(
                    context.FacilityId,
                    pending.operationId,
                    pending.requestFingerprint,
                    pending.commitId,
                    out installed,
                    out _)
                || !TryCaptureSensorState(
                    context.FacilityId,
                    out pending,
                    out installed,
                    out removal,
                    out SensorProvenance stabilized)
                || pending != null
                || !stabilized.Equals(provenance))
            {
                return Deferred(CaptureCurrentFingerprint(context.FacilityId));
            }
        }

        if (installed != null && removal == null)
        {
            if (!sensor.TryPrepareDurable(
                    context.FacilityId,
                    out removal,
                    out _)
                || removal?.phase != ProductionStockSensorRemovalPhase.Prepared
                || !SensorProvenance.Matches(provenance, removal))
            {
                return Deferred(CaptureCurrentFingerprint(context.FacilityId));
            }
        }

        ProductionFacilityDestructiveDrainStepResult childBoundary =
            DriveChildToEffect(context, child);
        if (childBoundary.Status is
                ProductionFacilityDestructiveDrainStepStatus.Deferred
            or ProductionFacilityDestructiveDrainStepStatus.Conflict)
        {
            return childBoundary;
        }
        if (!inputDrain.TryCapture(childStep, out child)
            || !IsExactChildIdentity(child, context)
            || !IsChildEffectCommitted(child.phase))
        {
            return Conflict(CaptureCurrentFingerprint(context.FacilityId));
        }

        if (removal?.phase == ProductionStockSensorRemovalPhase.Prepared)
        {
            if (!sensor.TryPublish(context.FacilityId, out removal, out _))
                return Deferred(CaptureCurrentFingerprint(context.FacilityId));
        }
        if (removal != null
            && !IsSensorEffectCommitted(removal.phase))
        {
            return Conflict(CaptureCurrentFingerprint(context.FacilityId));
        }

        if (!TryBuildCompositeTerminal(
                context.Owner.requestFingerprint,
                child,
                removal,
                out string commitId,
                out string receiptFingerprint))
        {
            return Conflict(CaptureCurrentFingerprint(context.FacilityId));
        }
        return new ProductionFacilityDestructiveDrainStepResult(
            replay
                ? ProductionFacilityDestructiveDrainStepStatus.Replay
                : ProductionFacilityDestructiveDrainStepStatus.Applied,
            commitId,
            receiptFingerprint,
            CaptureCurrentFingerprint(context.FacilityId));
    }

    [GameplayInternalOnly(
        "Acknowledges the exact physical child and embedded salvage only after the upper journal owns their composite receipt.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!ValidateBaseContext(context)
            || context.Owner.phase != ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
            || !HasCanonicalTerminalFields(
                context.Owner.commitId,
                context.Owner.receiptFingerprint))
        {
            return Conflict(current);
        }

        string childStep = context.Owner.stepOperationId + ChildStepSuffix;
        if (!inputDrain.TryCapture(childStep, out var child)
            || !IsExactChildIdentity(child, context)
            || !TryCaptureSensorState(
                context.FacilityId,
                out _,
                out _,
                out ProductionStockSensorRemovalSaveData removal,
                out SensorProvenance provenance)
            || !string.Equals(
                BuildRequestFingerprint(child.requestFingerprint, provenance),
                context.Owner.requestFingerprint,
                StringComparison.Ordinal)
            || !TryBuildCompositeTerminal(
                context.Owner.requestFingerprint,
                child,
                removal,
                out string expectedCommit,
                out string expectedReceipt)
            || !string.Equals(expectedCommit,
                context.Owner.commitId, StringComparison.Ordinal)
            || !string.Equals(expectedReceipt,
                context.Owner.receiptFingerprint, StringComparison.Ordinal))
        {
            return Conflict(current);
        }

        bool replay = IsChildAcknowledged(child.phase)
            && (removal == null || IsSensorAcknowledged(removal.phase));
        if (!IsChildAcknowledged(child.phase))
        {
            ProductionInputDestinationCustodyDrainResult acknowledged;
            try
            {
                acknowledged = inputDrain.TryAcknowledge(
                    child.stepOperationId,
                    child.receiptFingerprint);
            }
            catch
            {
                return Conflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (acknowledged.Status is
                    ProductionInputDestinationCustodyDrainStatus.Deferred
                or ProductionInputDestinationCustodyDrainStatus.Conflict
                || !inputDrain.TryCapture(childStep, out child)
                || !IsChildAcknowledged(child.phase))
            {
                return Conflict(CaptureCurrentFingerprint(context.FacilityId));
            }
        }

        if (removal != null && !IsSensorAcknowledged(removal.phase))
        {
            string sensorCommit = removal.outputCommitIds?.SingleOrDefault();
            if (string.IsNullOrEmpty(sensorCommit)
                || !sensor.TryAcknowledge(
                    context.FacilityId,
                    sensorCommit,
                    out removal,
                    out _)
                || removal?.phase != ProductionStockSensorRemovalPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc)
            {
                return Conflict(CaptureCurrentFingerprint(context.FacilityId));
            }
        }

        return new ProductionFacilityDestructiveDrainStepResult(
            replay
                ? ProductionFacilityDestructiveDrainStepStatus.Replay
                : ProductionFacilityDestructiveDrainStepStatus.Applied,
            context.Owner.commitId,
            context.Owner.receiptFingerprint,
            CaptureCurrentFingerprint(context.FacilityId));
    }

    public ProductionFacilityDestructiveDrainRecoveryResult Recover(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!ValidateBaseContext(context))
            return RecoveryConflict(current);

        string childStep = context.Owner.stepOperationId + ChildStepSuffix;
        bool hasChild = inputDrain.TryCapture(childStep, out var child);
        bool hasSensor = TryCaptureSensorState(
            context.FacilityId,
            out _,
            out _,
            out ProductionStockSensorRemovalSaveData removal,
            out SensorProvenance provenance);
        if (!hasSensor)
            return RecoveryConflict(current);

        if (!hasChild)
        {
            return context.Owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned
                ? new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    Deferred(current))
                : RecoveryConflict(current);
        }
        if (!IsExactChildIdentity(child, context)
            || !string.Equals(
                BuildRequestFingerprint(child.requestFingerprint, provenance),
                context.Owner.requestFingerprint,
                StringComparison.Ordinal))
        {
            return RecoveryConflict(current);
        }

        bool terminal = TryBuildCompositeTerminal(
            context.Owner.requestFingerprint,
            child,
            removal,
            out string commitId,
            out string receipt);
        switch (context.Owner.phase)
        {
            case ProductionFacilityDestructiveDrainStepPhase.Planned:
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    terminal
                        ? Replay(commitId, receipt, current)
                        : Deferred(current));

            case ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck:
                if (!terminal
                    || !string.Equals(commitId,
                        context.Owner.commitId, StringComparison.Ordinal)
                    || !string.Equals(receipt,
                        context.Owner.receiptFingerprint,
                        StringComparison.Ordinal))
                {
                    return RecoveryConflict(current);
                }
                bool acknowledged = IsChildAcknowledged(child.phase)
                    && (removal == null || IsSensorAcknowledged(removal.phase));
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    acknowledged
                        ? ProductionFacilityDestructiveDrainRecoveryAction
                            .AlreadyAcknowledged
                        : ProductionFacilityDestructiveDrainRecoveryAction
                            .ResumeAcknowledge,
                    Replay(commitId, receipt, current));

            case ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged:
                return terminal
                    && IsChildAcknowledged(child.phase)
                    && (removal == null || IsSensorAcknowledged(removal.phase))
                    && string.Equals(commitId,
                        context.Owner.commitId, StringComparison.Ordinal)
                    && string.Equals(receipt,
                        context.Owner.receiptFingerprint,
                        StringComparison.Ordinal)
                    ? new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .AlreadyAcknowledged,
                        Replay(commitId, receipt, current))
                    : RecoveryConflict(current);

            default:
                return RecoveryConflict(current);
        }
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
                entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                candidate)
    {
        candidate = null;
        if (activeCheckpointGcCandidate != null)
            return GcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "production-stock-sensor-checkpoint-gc-already-active");

        ProductionFacilityDestructiveDrainEntrySaveData[] ordered = (entries
                ?? Array.Empty<ProductionFacilityDestructiveDrainEntrySaveData>())
            .OrderBy(value => value?.operationId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.operationId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            return GcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "production-stock-sensor-checkpoint-gc-entry-invalid");
        }

        List<ProductionInputDestinationCustodyDrainSaveData> children = new();
        List<ProductionStockSensorRemovalSaveData> removals = new();
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in ordered)
        {
            ProductionFacilityDestructiveDrainParticipantSaveData[] rows =
                (entry.participants ?? new List<
                    ProductionFacilityDestructiveDrainParticipantSaveData>())
                .Where(value => value != null && string.Equals(
                    value.participantId,
                    ParticipantId,
                    StringComparison.Ordinal))
                .ToArray();
            if (rows.Length != 1)
            {
                return GcResult(context,
                    ProductionFacilityDestructiveDrainCheckpointGcStatus
                        .Corruption,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantTopologyMismatch,
                    "production-stock-sensor-checkpoint-gc-participant-row-invalid");
            }
            foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                     rows[0].owners ?? new List<
                         ProductionFacilityDestructiveDrainOwnerSaveData>())
            {
                BuildingInstanceId facilityId =
                    (BuildingInstanceId)entry.facilityId;
                if (owner == null
                    || owner.phase != ProductionFacilityDestructiveDrainStepPhase
                        .OwnerAcknowledged
                    || !inputDrain.TryCapture(
                        owner.stepOperationId + ChildStepSuffix,
                        out ProductionInputDestinationCustodyDrainSaveData child)
                    || !ChildMatchesUpper(child, entry, owner)
                    || !TryCaptureSensorState(
                        facilityId,
                        out _,
                        out _,
                        out ProductionStockSensorRemovalSaveData removal,
                        out SensorProvenance provenance)
                    || provenance.Present != (removal != null)
                    || removal != null
                        && removal.phase != ProductionStockSensorRemovalPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc
                    || !string.Equals(
                        BuildRequestFingerprint(
                            child.requestFingerprint, provenance),
                        owner.requestFingerprint,
                        StringComparison.Ordinal)
                    || !TryBuildCompositeTerminal(
                        owner.requestFingerprint,
                        child,
                        removal,
                        out string commitId,
                        out string receipt)
                    || !string.Equals(commitId, owner.commitId,
                        StringComparison.Ordinal)
                    || !string.Equals(receipt, owner.receiptFingerprint,
                        StringComparison.Ordinal))
                {
                    return GcResult(context,
                        ProductionFacilityDestructiveDrainCheckpointGcStatus
                            .Corruption,
                        ProductionFacilityDestructiveDrainCheckpointGcReason
                            .ParticipantPrepareFailed,
                        "production-stock-sensor-checkpoint-gc-upper-lower-gap");
                }
                children.Add(child.Clone());
                if (removal != null)
                    removals.Add(removal.Clone());
            }
        }
        if (children.Select(value => value.stepOperationId)
                .Distinct(StringComparer.Ordinal).Count() != children.Count
            || removals.Select(value => value.facilityId)
                .Distinct(StringComparer.Ordinal).Count() != removals.Count)
        {
            return GcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "production-stock-sensor-checkpoint-gc-owner-duplicate");
        }
        if ((children.Count > 0 && inputCheckpointGc == null)
            || (removals.Count > 0 && sensorCheckpointGc == null))
        {
            return GcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .MissingParticipant,
                "production-stock-sensor-checkpoint-gc-lower-port-missing");
        }

        IProductionInputDestinationCustodyDrainCheckpointGcCandidate
            childCandidate = null;
        IProductionStockSensorRemovalCheckpointGcCandidate sensorCandidate =
            null;
        if (inputCheckpointGc != null
            && children.Count > 0
            && !inputCheckpointGc.TryPrepareCheckpointGarbageCollection(
                children,
                out childCandidate,
                out string childFailure))
        {
            return GcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "production-stock-sensor-checkpoint-gc-child-prepare-failed:"
                + childFailure);
        }
        if (sensorCheckpointGc != null
            && !sensorCheckpointGc.TryPrepareCheckpointGarbageCollection(
                removals,
                out sensorCandidate,
                out string sensorFailure))
        {
            if (childCandidate != null)
                inputCheckpointGc.CompleteCheckpointGarbageCollection(
                    childCandidate);
            return GcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "production-stock-sensor-checkpoint-gc-source-prepare-failed:"
                + sensorFailure);
        }
        activeCheckpointGcCandidate = new CheckpointGcCandidate(
            context,
            ordered.Select(value => value.operationId).ToArray(),
            childCandidate,
            sensorCandidate);
        candidate = activeCheckpointGcCandidate;
        return GcResult(context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            "production-stock-sensor-checkpoint-gc-prepared");
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.Published)
            return GcResult(exact.Context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus
                    .AlreadyApplied,
                ProductionFacilityDestructiveDrainCheckpointGcReason.None,
                "production-stock-sensor-checkpoint-gc-already-published",
                exact.OperationIds.Count);
        if (exact.ChildCandidate != null && !exact.ChildPublished)
        {
            if (!inputCheckpointGc.TryPublishCheckpointGarbageCollection(
                    exact.ChildCandidate,
                    out string failure))
            {
                return GcResult(exact.Context,
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantPublishFailed,
                    "production-stock-sensor-checkpoint-gc-child-publish-failed:"
                    + failure);
            }
            exact.ChildPublished = true;
        }
        if (exact.SensorCandidate != null && !exact.SensorPublished)
        {
            if (!sensorCheckpointGc.TryPublishCheckpointGarbageCollection(
                    exact.SensorCandidate,
                    out string failure))
            {
                return GcResult(exact.Context,
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantPublishFailed,
                    "production-stock-sensor-checkpoint-gc-source-publish-failed:"
                    + failure);
            }
            exact.SensorPublished = true;
        }
        exact.Published = true;
        return GcResult(exact.Context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            "production-stock-sensor-checkpoint-gc-published",
            exact.OperationIds.Count);
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.SensorPublished)
        {
            sensorCheckpointGc.RollbackCheckpointGarbageCollection(
                exact.SensorCandidate);
            exact.SensorPublished = false;
        }
        if (exact.ChildPublished)
        {
            inputCheckpointGc.RollbackCheckpointGarbageCollection(
                exact.ChildCandidate);
            exact.ChildPublished = false;
        }
        exact.Published = false;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.SensorCandidate != null)
            sensorCheckpointGc.CompleteCheckpointGarbageCollection(
                exact.SensorCandidate);
        if (exact.ChildCandidate != null)
            inputCheckpointGc.CompleteCheckpointGarbageCollection(
                exact.ChildCandidate);
        activeCheckpointGcCandidate = null;
    }

    private CompositeCapture CaptureSource(
        ProductionFacilityDestructiveDrainOperationId operationId,
        BuildingInstanceId facilityId)
    {
        ProductionFacilityHandle facility = ResolveFacility(facilityId);
        string destinationId = ProductionStockSensorRuntime.BuildDestinationId(
            facilityId.Value);
        if (!inputDrain.TryCaptureSource(
                destinationId,
                out ProductionInputDestinationCustodySourceSnapshot physical,
                out string physicalFailure))
        {
            throw new InvalidOperationException(physicalFailure);
        }
        if (!TryCaptureSensorState(
                facilityId,
                out ProductionStockSensorPhysicalCommitSaveData pending,
                out ProductionInstalledStockSensorSaveData installed,
                out ProductionStockSensorRemovalSaveData removal,
                out SensorProvenance provenance))
        {
            throw new InvalidOperationException(
                "production-stock-sensor-destructive-source-conflict");
        }

        bool hasPhysical = physical.InputQuantity > 0
            || physical.SourceStacks.Count > 0
            || physical.SourceOperations.Count > 0
            || physical.SourceActors.Count > 0;
        bool hasEmbedded = pending != null || installed != null || removal != null;
        bool capable = !string.IsNullOrEmpty(
            facility.StockSensorInstallationItemId);
        if (!capable)
        {
            if (hasPhysical || hasEmbedded)
            {
                throw new InvalidOperationException(
                    "production-stock-sensor-destructive-incapable-owner-present");
            }
            return CompositeCapture.Empty;
        }
        if (removal?.phase == ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            throw new InvalidOperationException(
                "production-stock-sensor-destructive-terminal-orphan");
        }
        if (!hasPhysical && !hasEmbedded)
            return CompositeCapture.Empty;

        string owner = ProductionFacilityDestructiveDrainOwnerStableIds
            .StockSensor(facilityId.Value);
        string step = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(operationId, ParticipantId, owner);
        string childStep = step + ChildStepSuffix;
        string claimFingerprint = CaptureClaimFingerprint(facility);
        if (!inputDrain.TryBuildRequest(
                operationId.Value,
                childStep,
                owner,
                destinationId,
                facilityId.Value,
                facility.Position,
                claimFingerprint,
                physical,
                out ProductionInputDestinationCustodyDrainRequest child,
                out string buildFailure))
        {
            throw new InvalidOperationException(buildFailure);
        }
        return new CompositeCapture(
            owner,
            step,
            child,
            BuildRequestFingerprint(child.RequestFingerprint, provenance),
            pending,
            installed,
            removal,
            provenance);
    }

    private string CaptureClaimFingerprint(ProductionFacilityHandle facility)
    {
        if (!destinationAuthority.TryValidate(
                facility,
                out long capacityMassGrams,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        string destination = ProductionStockSensorRuntime.BuildDestinationId(
            facility.InstanceId.Value);
        if (!claims.TryGetClaim(
                destination,
                facility.Position,
                out FacilityBufferDestinationClaim claim)
            || !capacities.TryGetCapacityAuthorityFingerprint(
                destination,
                facility.Position,
                out string capacityFingerprint))
        {
            throw new InvalidOperationException(
                "production-stock-sensor-destructive-authority-fingerprint-missing");
        }
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-stock-sensor-socket-authority@1");
        digest.Append(claim.DestinationId);
        digest.Append(claim.DropPosition.x);
        digest.Append(claim.DropPosition.y);
        digest.Append(claim.OwnerDomain);
        digest.Append(claim.OwnerOperationId);
        digest.Append(claim.OwnerFacilityId);
        digest.AppendEnum(claim.AnchorKind);
        digest.Append(capacityMassGrams);
        digest.Append(capacityFingerprint);
        return digest.ComputeSha256();
    }

    private bool TryCaptureSensorState(
        BuildingInstanceId facilityId,
        out ProductionStockSensorPhysicalCommitSaveData pending,
        out ProductionInstalledStockSensorSaveData installed,
        out ProductionStockSensorRemovalSaveData removal,
        out SensorProvenance provenance)
    {
        pending = null;
        installed = null;
        removal = null;
        provenance = SensorProvenance.Absent;
        if (!sensor.TryCapturePendingInstallation(
                facilityId,
                out pending,
                out _)
            || !sensor.TryCapture(
                facilityId,
                out installed,
                out removal,
                out _))
        {
            return false;
        }
        return SensorProvenance.TryCreate(
            facilityId,
            pending,
            installed,
            removal,
            out provenance);
    }

    private ProductionFacilityDestructiveDrainStepResult DriveChildToEffect(
        ProductionFacilityDestructiveDrainStepContext context,
        ProductionInputDestinationCustodyDrainSaveData child)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (IsChildAcknowledged(child.phase))
            return Conflict(current);
        if (IsChildEffectCommitted(child.phase))
            return Replay(child.commitId, child.receiptFingerprint, current);

        int remaining = checked(
            (child.sourceActors?.Count ?? 0)
            + (child.sourceOperations?.Count ?? 0)
            + 4);
        while (remaining-- > 0)
        {
            ProductionInputDestinationCustodyDrainResult result;
            try
            {
                result = inputDrain.TryCommit(
                    child.stepOperationId,
                    child.requestFingerprint);
            }
            catch
            {
                return Conflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (result.Status ==
                ProductionInputDestinationCustodyDrainStatus.Conflict)
            {
                return Conflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (result.Status ==
                ProductionInputDestinationCustodyDrainStatus.Deferred)
            {
                return string.IsNullOrEmpty(result.CommitId)
                    && string.IsNullOrEmpty(result.ReceiptFingerprint)
                    ? Deferred(CaptureCurrentFingerprint(context.FacilityId))
                    : Conflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (!inputDrain.TryCapture(child.stepOperationId, out child)
                || !IsExactChildIdentity(child, context))
            {
                return Conflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (HasCanonicalTerminalFields(
                    result.CommitId,
                    result.ReceiptFingerprint))
            {
                return IsChildEffectCommitted(child.phase)
                    && string.Equals(child.commitId,
                        result.CommitId, StringComparison.Ordinal)
                    && string.Equals(child.receiptFingerprint,
                        result.ReceiptFingerprint, StringComparison.Ordinal)
                    ? Replay(
                        result.CommitId,
                        result.ReceiptFingerprint,
                        CaptureCurrentFingerprint(context.FacilityId))
                    : Conflict(CaptureCurrentFingerprint(context.FacilityId));
            }
        }
        return Conflict(CaptureCurrentFingerprint(context.FacilityId));
    }

    private ProductionOutputDestinationLifecycleContribution CaptureContribution(
        BuildingInstanceId facilityId)
    {
        ProductionOutputDestinationLifecycleContribution[] matches = lifecycle
            .Capture(facilityId)
            .Contributions
            .Where(value => value != null
                && string.Equals(
                    value.ContributorId,
                    ParticipantId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Stock-sensor lifecycle contribution cardinality is invalid.");
        }
        return matches[0];
    }

    private string CaptureCurrentFingerprint(BuildingInstanceId facilityId)
    {
        try
        {
            return CaptureContribution(facilityId)
                .DurableSemanticFingerprint;
        }
        catch
        {
            return ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint(
                    "stock-sensor-destructive-capture-conflict:"
                    + facilityId.Value);
        }
    }

    private ProductionFacilityHandle ResolveFacility(
        BuildingInstanceId facilityId)
    {
        ProductionFacilityHandle[] matches = (facilities.Facilities
                ?? Array.Empty<ProductionFacilityHandle>())
            .Where(value => value != null
                && !value.IsDestroyed
                && value.InstanceId.Equals(facilityId))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "production-stock-sensor-destructive-facility-resolution-conflict:"
                + facilityId.Value + ":"
                + matches.Length.ToString(CultureInfo.InvariantCulture));
        }
        return matches[0];
    }

    private bool ValidateBaseContext(
        ProductionFacilityDestructiveDrainStepContext context) =>
        string.Equals(context.ParticipantId, ParticipantId,
            StringComparison.Ordinal)
        && string.Equals(
            context.Owner.ownerStableId,
            ProductionFacilityDestructiveDrainOwnerStableIds
                .StockSensor(context.FacilityId.Value),
            StringComparison.Ordinal)
        && context.Owner.disposition ==
            ProductionFacilityDestructiveDrainDisposition.Terminalize
        && string.IsNullOrEmpty(context.Owner.targetDestinationId)
        && string.Equals(
            context.Owner.stepOperationId,
            ProductionFacilityDestructiveDrainCanonical.BuildStepOperationId(
                context.OperationId,
                ParticipantId,
                context.Owner.ownerStableId),
            StringComparison.Ordinal)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            context.Owner.requestFingerprint);

    private static string BuildRequestFingerprint(
        string childRequestFingerprint,
        SensorProvenance provenance) =>
        ProductionStockSensorDestructiveDrainCanonical
            .BuildRequestFingerprint(childRequestFingerprint, provenance);

    private static bool TryBuildCompositeTerminal(
        string upperRequestFingerprint,
        ProductionInputDestinationCustodyDrainSaveData child,
        ProductionStockSensorRemovalSaveData removal,
        out string commitId,
        out string receiptFingerprint) =>
        ProductionStockSensorDestructiveDrainCanonical
            .TryBuildCompositeTerminal(
                upperRequestFingerprint,
                child,
                removal,
                out commitId,
                out receiptFingerprint);

    private static string BuildPlanFingerprint(
        BuildingInstanceId facilityId,
        string contribution,
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerPlan> owners)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-stock-sensor-destructive-plan@2");
        digest.Append(facilityId.Value);
        digest.Append(contribution);
        digest.Append(owners?.Count ?? 0);
        foreach (ProductionFacilityDestructiveDrainOwnerPlan owner in
                 owners ?? Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>())
        {
            digest.Append(owner.OwnerStableId);
            digest.AppendEnum(owner.Disposition);
            digest.Append(owner.TargetDestinationId);
            digest.Append(owner.RequestFingerprint);
        }
        return digest.ComputeSha256();
    }

    private static bool IsExactChild(
        ProductionInputDestinationCustodyDrainSaveData state,
        ProductionInputDestinationCustodyDrainRequest request) =>
        state != null
        && ProductionInputDestinationCustodyDrainContract.IsValidSave(state)
        && string.Equals(state.parentOperationId,
            request.ParentOperationId, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId,
            request.StepOperationId, StringComparison.Ordinal)
        && string.Equals(state.ownerStableId,
            request.OwnerStableId, StringComparison.Ordinal)
        && string.Equals(state.billId,
            request.BillId, StringComparison.Ordinal)
        && string.Equals(state.facilityId,
            request.FacilityId, StringComparison.Ordinal)
        && string.Equals(state.sourceDestinationId,
            request.SourceDestinationId, StringComparison.Ordinal)
        && string.Equals(state.sourceClaimFingerprint,
            request.SourceClaimFingerprint, StringComparison.Ordinal)
        && string.Equals(state.sourceOwnershipFingerprint,
            request.SourceOwnershipFingerprint, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint,
            request.RequestFingerprint, StringComparison.Ordinal);

    private static bool IsExactChildIdentity(
        ProductionInputDestinationCustodyDrainSaveData child,
        ProductionFacilityDestructiveDrainStepContext context) =>
        child != null
        && ProductionInputDestinationCustodyDrainContract.IsValidSave(child)
        && string.Equals(child.parentOperationId,
            context.OperationId.Value, StringComparison.Ordinal)
        && string.Equals(child.stepOperationId,
            context.Owner.stepOperationId + ChildStepSuffix,
            StringComparison.Ordinal)
        && string.Equals(child.ownerStableId,
            context.Owner.ownerStableId, StringComparison.Ordinal)
        && string.Equals(child.billId,
            ProductionStockSensorRuntime.BuildDestinationId(
                context.FacilityId.Value),
            StringComparison.Ordinal)
        && string.Equals(child.facilityId,
            context.FacilityId.Value, StringComparison.Ordinal)
        && string.Equals(child.sourceDestinationId,
            ProductionStockSensorRuntime.BuildDestinationId(
                context.FacilityId.Value),
            StringComparison.Ordinal);

    private static bool ChildMatchesUpper(
        ProductionInputDestinationCustodyDrainSaveData child,
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        ProductionFacilityDestructiveDrainOwnerSaveData owner) => child != null
        && ProductionInputDestinationCustodyDrainContract.IsValidSave(child)
        && child.phase == ProductionInputDestinationCustodyDrainPhase
            .BillAcknowledgedAwaitingCheckpointGc
        && string.Equals(child.parentOperationId, entry.operationId,
            StringComparison.Ordinal)
        && string.Equals(child.stepOperationId,
            owner.stepOperationId + ChildStepSuffix,
            StringComparison.Ordinal)
        && string.Equals(child.ownerStableId, owner.ownerStableId,
            StringComparison.Ordinal)
        && string.Equals(child.billId,
            ProductionStockSensorRuntime.BuildDestinationId(entry.facilityId),
            StringComparison.Ordinal)
        && string.Equals(child.facilityId, entry.facilityId,
            StringComparison.Ordinal)
        && string.Equals(child.sourceDestinationId,
            ProductionStockSensorRuntime.BuildDestinationId(entry.facilityId),
            StringComparison.Ordinal);

    private CheckpointGcCandidate RequireCheckpointGcCandidate(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || !ReferenceEquals(activeCheckpointGcCandidate, exact))
            throw new InvalidOperationException(
                "Stock-sensor checkpoint-GC candidate is stale or foreign.");
        return exact;
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcResult
        GcResult(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus status,
            ProductionFacilityDestructiveDrainCheckpointGcReason reason,
            string message,
            int collected = 0) => new(
            status,
            reason,
            context.CheckpointSequence,
            message,
            collected);

    private sealed class CheckpointGcCandidate :
        IProductionFacilityDestructiveDrainCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<string> operationIds,
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                childCandidate,
            IProductionStockSensorRemovalCheckpointGcCandidate sensorCandidate)
        {
            Context = context;
            OperationIds = (operationIds ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            ChildCandidate = childCandidate;
            SensorCandidate = sensorCandidate;
        }

        public string ParticipantId => ProductionFacilityDestructiveDrainParticipantIds
            .StockSensorEmbeddedSalvage;
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> OperationIds { get; }
        internal ProductionFacilityDestructiveDrainCheckpointGcContext Context
            { get; }
        internal IProductionInputDestinationCustodyDrainCheckpointGcCandidate
            ChildCandidate { get; }
        internal IProductionStockSensorRemovalCheckpointGcCandidate
            SensorCandidate { get; }
        internal bool ChildPublished { get; set; }
        internal bool SensorPublished { get; set; }
        internal bool Published { get; set; }
    }

    private static bool IsChildEffectCommitted(
        ProductionInputDestinationCustodyDrainPhase phase) =>
        ProductionStockSensorDestructiveDrainCanonical
            .IsChildEffectCommitted(phase);

    private static bool IsChildAcknowledged(
        ProductionInputDestinationCustodyDrainPhase phase) =>
        ProductionStockSensorDestructiveDrainCanonical
            .IsChildAcknowledged(phase);

    private static bool IsSensorEffectCommitted(
        ProductionStockSensorRemovalPhase phase) =>
        ProductionStockSensorDestructiveDrainCanonical
            .IsSensorEffectCommitted(phase);

    private static bool IsSensorAcknowledged(
        ProductionStockSensorRemovalPhase phase) =>
        ProductionStockSensorDestructiveDrainCanonical
            .IsSensorAcknowledged(phase);

    private static bool HasCanonicalTerminalFields(
        string commitId,
        string receiptFingerprint) =>
        ProductionStockSensorDestructiveDrainCanonical
            .HasCanonicalTerminalFields(commitId, receiptFingerprint);

    private static ProductionFacilityDestructiveDrainStepResult Deferred(
        string current) => new(
        ProductionFacilityDestructiveDrainStepStatus.Deferred,
        string.Empty,
        string.Empty,
        current);

    private static ProductionFacilityDestructiveDrainStepResult Conflict(
        string current) => new(
        ProductionFacilityDestructiveDrainStepStatus.Conflict,
        string.Empty,
        string.Empty,
        current);

    private static ProductionFacilityDestructiveDrainStepResult Replay(
        string commit,
        string receipt,
        string current) => new(
        ProductionFacilityDestructiveDrainStepStatus.Replay,
        commit,
        receipt,
        current);

    private static ProductionFacilityDestructiveDrainRecoveryResult
        RecoveryConflict(string current) => new(
        ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
        Conflict(current));

    private readonly struct CompositeCapture
    {
        public static CompositeCapture Empty => default;

        public CompositeCapture(
            string ownerStableId,
            string stepOperationId,
            ProductionInputDestinationCustodyDrainRequest childRequest,
            string requestFingerprint,
            ProductionStockSensorPhysicalCommitSaveData pending,
            ProductionInstalledStockSensorSaveData installed,
            ProductionStockSensorRemovalSaveData removal,
            SensorProvenance provenance)
        {
            OwnerStableId = ownerStableId;
            StepOperationId = stepOperationId;
            ChildRequest = childRequest;
            RequestFingerprint = requestFingerprint;
            Pending = pending;
            Installed = installed;
            Removal = removal;
            Provenance = provenance;
        }

        public string OwnerStableId { get; }
        public string StepOperationId { get; }
        public ProductionInputDestinationCustodyDrainRequest ChildRequest { get; }
        public string RequestFingerprint { get; }
        public ProductionStockSensorPhysicalCommitSaveData Pending { get; }
        public ProductionInstalledStockSensorSaveData Installed { get; }
        public ProductionStockSensorRemovalSaveData Removal { get; }
        public SensorProvenance Provenance { get; }
        public bool HasOwner => ChildRequest != null;
    }

}
