using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Durable destructive-drain join for both combat craft and repair orders.
/// The producer is persisted before its optional Items child, so every crash
/// window has a canonical producer-owned recovery anchor.
/// </summary>
public sealed class CombatEquipmentTerminalDestructiveDrainParticipant :
    IProductionFacilityDestructiveDrainParticipant,
    IProductionFacilityDestructiveDrainDurablePrepareParticipant,
    IProductionFacilityDestructiveDrainCheckpointGcParticipant
{
    private sealed class CheckpointGcCandidate :
        IProductionFacilityDestructiveDrainCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<string> operationIds,
            ICombatEquipmentTerminalDrainCheckpointGcAuthority producerPort,
            ICombatEquipmentTerminalDrainCheckpointGcCandidate producerCandidate,
            ICombatEquipmentTerminalSourceCheckpointGcAuthority sourcePort,
            ICombatEquipmentTerminalSourceCheckpointGcCandidate sourceCandidate,
            IProductionInputDestinationCustodyDrainCheckpointGcPort childPort,
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate childCandidate)
        {
            Context = context;
            OperationIds = operationIds
                ?? throw new ArgumentNullException(nameof(operationIds));
            ProducerPort = producerPort
                ?? throw new ArgumentNullException(nameof(producerPort));
            ProducerCandidate = producerCandidate
                ?? throw new ArgumentNullException(nameof(producerCandidate));
            SourcePort = sourcePort
                ?? throw new ArgumentNullException(nameof(sourcePort));
            SourceCandidate = sourceCandidate
                ?? throw new ArgumentNullException(nameof(sourceCandidate));
            ChildPort = childPort;
            ChildCandidate = childCandidate;
        }

        public string ParticipantId =>
            CombatEquipmentTerminalDrainCanonical.ParticipantId;
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> OperationIds { get; }
        internal ProductionFacilityDestructiveDrainCheckpointGcContext Context
            { get; }
        internal ICombatEquipmentTerminalDrainCheckpointGcAuthority ProducerPort
            { get; }
        internal ICombatEquipmentTerminalDrainCheckpointGcCandidate
            ProducerCandidate { get; }
        internal ICombatEquipmentTerminalSourceCheckpointGcAuthority SourcePort
            { get; }
        internal ICombatEquipmentTerminalSourceCheckpointGcCandidate SourceCandidate
            { get; }
        internal IProductionInputDestinationCustodyDrainCheckpointGcPort ChildPort
            { get; }
        internal IProductionInputDestinationCustodyDrainCheckpointGcCandidate
            ChildCandidate { get; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    public const int CurrentContractVersion = 1;
    private const string ChildSuffix = ":input-destination-custody";
    private static readonly IReadOnlyList<string> Dependencies =
        Array.AsReadOnly(Array.Empty<string>());

    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly ICombatEquipmentTerminalFacilitySourceQuery sources;
    private readonly ICombatEquipmentTerminalDrainQuery producerQuery;
    private readonly ICombatEquipmentTerminalDrainCommand producer;
    private readonly IProductionInputDestinationCustodyDrainService inputDrain;
    private readonly ICombatEquipmentTerminalFacilityQuery facilities;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public CombatEquipmentTerminalDestructiveDrainParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        ICombatEquipmentTerminalFacilitySourceQuery sources,
        ICombatEquipmentTerminalDrainQuery producerQuery,
        ICombatEquipmentTerminalDrainCommand producer,
        IProductionInputDestinationCustodyDrainService inputDrain,
        ICombatEquipmentTerminalFacilityQuery facilities)
    {
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
        this.producerQuery = producerQuery ?? throw new ArgumentNullException(nameof(producerQuery));
        this.producer = producer ?? throw new ArgumentNullException(nameof(producer));
        this.inputDrain = inputDrain ?? throw new ArgumentNullException(nameof(inputDrain));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
    }

    public string ParticipantId => CombatEquipmentTerminalDrainCanonical.ParticipantId;
    public int ContractVersion => CurrentContractVersion;
    public IReadOnlyList<string> DependsOnParticipantIds => Dependencies;
    public string CheckpointGcParticipantId => ParticipantId;

    public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context)
    {
        RequireDestination(context.FacilityId, context.DestinationId);
        ProductionOutputDestinationLifecycleContribution contribution =
            CaptureContribution(context.FacilityId);
        CombatEquipmentTerminalPreparedSource[] live = CaptureSources(context.FacilityId);
        if (contribution.HasAuthority != (live.Length > 0))
            throw new InvalidOperationException("combat-equipment-terminal-lifecycle-owner-conflict");

        if (live.Length == 0)
        {
            ProductionFacilityDestructiveDrainOwnerPlan[] emptyOwners =
                Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>();
            return new ProductionFacilityDestructiveDrainParticipantPlan(
                ParticipantId, ContractVersion,
                contribution.DurableSemanticFingerprint,
                CreatePlanFingerprint(
                    contribution.DurableSemanticFingerprint, emptyOwners),
                emptyOwners);
        }

        ProductionFacilityHandle facility = ResolveFacility(context.FacilityId);

        ProductionFacilityDestructiveDrainOwnerPlan[] owners = live
            .Select(source => CaptureRequest(context.OperationId, facility, source, out _))
            .Select(request => new ProductionFacilityDestructiveDrainOwnerPlan(
                request.Source.OwnerStableId,
                ProductionFacilityDestructiveDrainDisposition.Terminalize,
                string.Empty,
                request.RequestFingerprint))
            .OrderBy(value => value.OwnerStableId, StringComparer.Ordinal)
            .ToArray();
        return new ProductionFacilityDestructiveDrainParticipantPlan(
            ParticipantId, ContractVersion,
            contribution.DurableSemanticFingerprint,
            CreatePlanFingerprint(contribution.DurableSemanticFingerprint, owners),
            owners);
    }

    [GameplayInternalOnly(
        "Persists a frozen combat producer before its optional Items child after the upper journal owner is durable.",
        "Production facility destructive-drain runtime only")]
    public bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryValidateContext(context, out failureReason))
            return false;
        try
        {
            if (!string.Equals(CaptureCurrentFingerprint(context.FacilityId),
                    context.ExpectedDurableContributionFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = "combat-equipment-terminal-contribution-drift";
                return false;
            }
            ProductionFacilityHandle facility = ResolveFacility(context.FacilityId);
            CombatEquipmentTerminalPreparedSource source = CaptureSources(context.FacilityId)
                .SingleOrDefault(value => string.Equals(value.Source.OwnerStableId,
                    context.Owner.ownerStableId, StringComparison.Ordinal));
            if (source == null)
            {
                failureReason = "combat-equipment-terminal-durable-source-missing";
                return false;
            }
            CombatEquipmentTerminalDrainRequest request = CaptureRequest(
                context.OperationId, facility, source,
                out ProductionInputDestinationCustodyDrainRequest child);
            if (!string.Equals(request.StepOperationId, context.Owner.stepOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(request.RequestFingerprint,
                    context.Owner.requestFingerprint, StringComparison.Ordinal))
            {
                failureReason = "combat-equipment-terminal-durable-plan-drift";
                return false;
            }

            CombatEquipmentTerminalDrainResult prepared = producer.TryPrepare(request);
            if (prepared.Status is CombatEquipmentTerminalDrainStatus.Conflict
                or CombatEquipmentTerminalDrainStatus.Deferred
                || !producerQuery.TryCapture(request.StepOperationId,
                    out CombatEquipmentTerminalDrainSaveData state)
                || !ExactProducerRequest(state, request))
            {
                failureReason = string.IsNullOrEmpty(prepared.FailureReason)
                    ? "combat-equipment-terminal-producer-prepare-rejected"
                    : prepared.FailureReason;
                return false;
            }

            if (child == null)
                return true;
            ProductionInputDestinationCustodyDrainResult childPrepared =
                inputDrain.TryPrepare(child);
            if (childPrepared.Status is ProductionInputDestinationCustodyDrainStatus
                    .Conflict or ProductionInputDestinationCustodyDrainStatus.Deferred
                || !inputDrain.TryCapture(child.StepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData childState)
                || !ExactChild(childState, child))
            {
                failureReason = string.IsNullOrEmpty(childPrepared.FailureReason)
                    ? "combat-equipment-terminal-child-prepare-rejected"
                    : childPrepared.FailureReason;
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            failureReason = "combat-equipment-terminal-durable-prepare-failed:"
                + exception.GetType().Name;
            return false;
        }
    }

    [GameplayInternalOnly(
        "Drives the optional Items child and combat producer to one exact terminal receipt.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateContext(context, out _)
            || context.Owner.phase != ProductionFacilityDestructiveDrainStepPhase.Planned
            || !string.IsNullOrEmpty(context.Owner.commitId)
            || !string.IsNullOrEmpty(context.Owner.receiptFingerprint)
            || !inputDrain.RequiresImmediateRecoveryBeforeGameplayTick
            || !producerQuery.TryCapture(context.Owner.stepOperationId,
                out CombatEquipmentTerminalDrainSaveData state)
            || !ExactProducerState(state, context))
            return Conflict(current);

        if (HasChild(state))
        {
            ProductionFacilityDestructiveDrainStepResult child = DriveChild(context, state);
            if (child.Status is ProductionFacilityDestructiveDrainStepStatus.Deferred
                or ProductionFacilityDestructiveDrainStepStatus.Conflict)
                return child;
        }

        for (int remaining = 0; remaining < 4; remaining++)
        {
            CombatEquipmentTerminalDrainResult result;
            try { result = producer.TryProgress(context.Owner.stepOperationId); }
            catch { return Conflict(CaptureCurrentFingerprint(context.FacilityId)); }
            ProductionFacilityDestructiveDrainStepResult mapped = Map(context, result, false);
            if (mapped.Status != ProductionFacilityDestructiveDrainStepStatus.Deferred
                || result.Status == CombatEquipmentTerminalDrainStatus.Deferred)
                return mapped;
        }
        return Conflict(CaptureCurrentFingerprint(context.FacilityId));
    }

    [GameplayInternalOnly(
        "Acknowledges only the producer receipt already durably owned by the upper journal.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateContext(context, out _)
            || context.Owner.phase != ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
            || !Terminal(context.Owner.commitId, context.Owner.receiptFingerprint))
            return Conflict(current);
        try
        {
            return Map(context, producer.TryAcknowledge(
                context.Owner.stepOperationId, context.Owner.receiptFingerprint), true);
        }
        catch { return Conflict(CaptureCurrentFingerprint(context.FacilityId)); }
    }

    public ProductionFacilityDestructiveDrainRecoveryResult Recover(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateContext(context, out _))
            return RecoveryConflict(current);
        if (!producerQuery.TryCapture(context.Owner.stepOperationId,
                out CombatEquipmentTerminalDrainSaveData state))
        {
            if (inputDrain.TryCapture(context.Owner.stepOperationId + ChildSuffix, out _))
                return RecoveryConflict(current);
            return context.Owner.phase == ProductionFacilityDestructiveDrainStepPhase.Planned
                ? new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    Deferred(current))
                : context.Owner.phase == ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged
                    && Terminal(context.Owner.commitId, context.Owner.receiptFingerprint)
                    ? new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction.AlreadyAcknowledged,
                        Replay(context.Owner.commitId, context.Owner.receiptFingerprint, current))
                    : RecoveryConflict(current);
        }
        if (!ExactProducerState(state, context))
            return RecoveryConflict(current);

        bool producerTerminal = ProducerTerminal(state.phase);
        if (context.Owner.phase is ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
                or ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged
            && (!producerTerminal
                || !string.Equals(state.commitId, context.Owner.commitId,
                    StringComparison.Ordinal)
                || !string.Equals(state.receiptFingerprint,
                    context.Owner.receiptFingerprint, StringComparison.Ordinal)))
            return RecoveryConflict(current);

        return context.Owner.phase switch
        {
            ProductionFacilityDestructiveDrainStepPhase.Planned =>
                new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    producerTerminal
                        ? Replay(state.commitId, state.receiptFingerprint, current)
                        : Deferred(current)),
            ProductionFacilityDestructiveDrainStepPhase.EffectCommittedAwaitingOwnerAck
                when state.phase == CombatEquipmentTerminalDrainPhase
                    .TerminalEffectsCommittedAwaitingOwnerAcknowledgement =>
                new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeAcknowledge,
                    Replay(state.commitId, state.receiptFingerprint, current)),
            ProductionFacilityDestructiveDrainStepPhase.EffectCommittedAwaitingOwnerAck
                when state.phase == CombatEquipmentTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc =>
                new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.AlreadyAcknowledged,
                    Replay(state.commitId, state.receiptFingerprint, current)),
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged
                when state.phase == CombatEquipmentTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc =>
                new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.AlreadyAcknowledged,
                    Replay(state.commitId, state.receiptFingerprint, current)),
            _ => RecoveryConflict(current)
        };
    }

    [GameplayInternalOnly(
        "Prepares exact combat producer, source-receipt, and optional Items child rows for durable checkpoint collection.",
        "Production facility destructive-drain checkpoint GC coordinator only")]
    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData> entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        candidate = null;
        if (activeCheckpointGcCandidate != null)
        {
            return CheckpointGcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .LiveAuthorityChanged,
                "combat-equipment-terminal-checkpoint-gc-already-active");
        }
        if (producer is not ICombatEquipmentTerminalDrainCheckpointGcAuthority
                producerPort
            || producerPort.SourceCheckpointGcAuthority is not
                ICombatEquipmentTerminalSourceCheckpointGcAuthority sourcePort)
        {
            return CheckpointGcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .MissingParticipant,
                "combat-equipment-terminal-checkpoint-gc-port-missing");
        }

        ProductionFacilityDestructiveDrainEntrySaveData[] orderedEntries =
            (entries ?? Array.Empty<
                ProductionFacilityDestructiveDrainEntrySaveData>())
            .OrderBy(value => value?.operationId, StringComparer.Ordinal)
            .ToArray();
        if (orderedEntries.Any(value => value == null
                || value.phase != ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc
                || !ProductionFacilityDestructiveDrainOperationId.TryParse(
                    value.operationId, out _))
            || orderedEntries.Select(value => value.operationId)
                .Distinct(StringComparer.Ordinal).Count() != orderedEntries.Length)
        {
            return CheckpointGcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "combat-equipment-terminal-checkpoint-gc-entry-invalid");
        }

        List<CombatEquipmentTerminalDrainSaveData> producerRows = new();
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in
                 orderedEntries)
        {
            ProductionFacilityDestructiveDrainParticipantSaveData[] participants =
                (entry.participants ?? new List<
                    ProductionFacilityDestructiveDrainParticipantSaveData>())
                .Where(value => value != null && string.Equals(
                    value.participantId, ParticipantId, StringComparison.Ordinal))
                .ToArray();
            if (participants.Length != 1
                || participants[0].contractVersion != ContractVersion
                || participants[0].owners == null)
            {
                return CheckpointGcResult(context,
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantTopologyMismatch,
                    "combat-equipment-terminal-checkpoint-gc-owner-topology-invalid");
            }
            foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                     participants[0].owners.OrderBy(
                         value => value?.stepOperationId,
                         StringComparer.Ordinal))
            {
                if (!TryCaptureCheckpointGcProducerRow(entry, owner,
                        out CombatEquipmentTerminalDrainSaveData producerRow))
                {
                    return CheckpointGcResult(context,
                        ProductionFacilityDestructiveDrainCheckpointGcStatus
                            .Corruption,
                        ProductionFacilityDestructiveDrainCheckpointGcReason
                            .ParticipantPrepareFailed,
                        "combat-equipment-terminal-checkpoint-gc-producer-row-invalid");
                }
                producerRows.Add(producerRow);
            }
        }
        CombatEquipmentTerminalDrainSaveData[] orderedProducers = producerRows
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        if (orderedProducers.Select(value => value.stepOperationId)
                .Distinct(StringComparer.Ordinal).Count()
            != orderedProducers.Length)
        {
            return CheckpointGcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantTopologyMismatch,
                "combat-equipment-terminal-checkpoint-gc-producer-duplicate");
        }

        List<ProductionInputDestinationCustodyDrainSaveData> childRows = new();
        List<CombatEquipmentTerminalReceiptGcRow> sourceRows = new();
        foreach (CombatEquipmentTerminalDrainSaveData row in orderedProducers)
        {
            if (HasChild(row))
            {
                if (!inputDrain.TryCapture(
                        row.inputDestinationDrainStepOperationId,
                        out ProductionInputDestinationCustodyDrainSaveData child)
                    || !ExactChild(child, row)
                    || child.phase != ProductionInputDestinationCustodyDrainPhase
                        .BillAcknowledgedAwaitingCheckpointGc)
                {
                    return CheckpointGcResult(context,
                        ProductionFacilityDestructiveDrainCheckpointGcStatus
                            .Corruption,
                        ProductionFacilityDestructiveDrainCheckpointGcReason
                            .ParticipantPrepareFailed,
                        "combat-equipment-terminal-checkpoint-gc-child-row-invalid");
                }
                childRows.Add(child);
            }
            sourceRows.Add(new CombatEquipmentTerminalReceiptGcRow(
                CombatEquipmentTerminalFrozenSubject.FromSave(row.source),
                row.wipLossReceiptFingerprint,
                row.sourceRemovalReceiptFingerprint));
        }

        IProductionInputDestinationCustodyDrainCheckpointGcPort childPort = null;
        IProductionInputDestinationCustodyDrainCheckpointGcCandidate childCandidate =
            null;
        if (childRows.Count > 0)
        {
            childPort = inputDrain as
                IProductionInputDestinationCustodyDrainCheckpointGcPort;
            string childFailure = string.Empty;
            if (childPort == null
                || !childPort.TryPrepareCheckpointGarbageCollection(
                    childRows, out childCandidate, out childFailure))
            {
                return CheckpointGcResult(context,
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantPrepareFailed,
                    "combat-equipment-terminal-checkpoint-gc-child-prepare-failed:"
                    + childFailure);
            }
        }

        if (!sourcePort.TryPrepareCheckpointGarbageCollection(
                sourceRows,
                out ICombatEquipmentTerminalSourceCheckpointGcCandidate
                    sourceCandidate,
                out string sourceFailure))
        {
            if (childCandidate != null)
                childPort.CompleteCheckpointGarbageCollection(childCandidate);
            return CheckpointGcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "combat-equipment-terminal-checkpoint-gc-source-prepare-failed:"
                + sourceFailure);
        }
        if (!producerPort.TryPrepareCheckpointGarbageCollection(
                orderedProducers,
                out ICombatEquipmentTerminalDrainCheckpointGcCandidate
                    producerCandidate,
                out string producerFailure))
        {
            sourcePort.CompleteCheckpointGarbageCollection(sourceCandidate);
            if (childCandidate != null)
                childPort.CompleteCheckpointGarbageCollection(childCandidate);
            return CheckpointGcResult(context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "combat-equipment-terminal-checkpoint-gc-producer-prepare-failed:"
                + producerFailure);
        }

        string[] operationIds = orderedEntries.Select(value => value.operationId)
            .ToArray();
        CheckpointGcCandidate exact = new(
            context,
            Array.AsReadOnly(operationIds),
            producerPort,
            producerCandidate,
            sourcePort,
            sourceCandidate,
            childPort,
            childCandidate);
        activeCheckpointGcCandidate = exact;
        candidate = exact;
        return CheckpointGcResult(context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            operationIds.Length == 0
                ? ProductionFacilityDestructiveDrainCheckpointGcReason
                    .NoEligibleOperation
                : ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            string.Empty,
            operationIds.Length);
    }

    [GameplayInternalOnly(
        "Publishes prepared combat checkpoint collection child-first without touching the upper journal.",
        "Production facility destructive-drain checkpoint GC coordinator only")]
    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.Published)
        {
            return CheckpointGcResult(exact.Context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus
                    .AlreadyApplied,
                ProductionFacilityDestructiveDrainCheckpointGcReason.None,
                string.Empty,
                exact.OperationIds.Count);
        }

        if (exact.ChildCandidate != null
            && !exact.ChildPort.TryPublishCheckpointGarbageCollection(
                exact.ChildCandidate, out string childFailure))
        {
            return CheckpointGcResult(exact.Context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPublishFailed,
                "combat-equipment-terminal-checkpoint-gc-child-publish-failed:"
                + childFailure);
        }
        CombatEquipmentTerminalEffectResult sourceResult = exact.SourcePort
            .PublishCheckpointGarbageCollection(exact.SourceCandidate);
        if (sourceResult.Status is CombatEquipmentTerminalEffectStatus.Conflict
            or CombatEquipmentTerminalEffectStatus.Deferred)
        {
            return CheckpointGcResult(exact.Context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPublishFailed,
                "combat-equipment-terminal-checkpoint-gc-source-publish-failed:"
                + sourceResult.FailureReason);
        }
        CombatEquipmentTerminalDrainResult producerResult = exact.ProducerPort
            .PublishCheckpointGarbageCollection(exact.ProducerCandidate);
        if (producerResult.Status is CombatEquipmentTerminalDrainStatus.Conflict
            or CombatEquipmentTerminalDrainStatus.Deferred)
        {
            return CheckpointGcResult(exact.Context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPublishFailed,
                "combat-equipment-terminal-checkpoint-gc-producer-publish-failed:"
                + producerResult.FailureReason);
        }

        exact.Published = true;
        return CheckpointGcResult(exact.Context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            string.Empty,
            exact.OperationIds.Count);
    }

    [GameplayInternalOnly(
        "Restores only rows published by the exact combat checkpoint candidate in reverse order.",
        "Production facility destructive-drain checkpoint GC coordinator only")]
    public void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        exact.ProducerPort.RollbackCheckpointGarbageCollection(
            exact.ProducerCandidate);
        exact.SourcePort.RollbackCheckpointGarbageCollection(
            exact.SourceCandidate);
        if (exact.ChildCandidate != null)
        {
            exact.ChildPort.RollbackCheckpointGarbageCollection(
                exact.ChildCandidate);
        }
        exact.Published = false;
    }

    [GameplayInternalOnly(
        "Releases the exact combat checkpoint candidate after publish or rollback completes.",
        "Production facility destructive-drain checkpoint GC coordinator only")]
    public void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        exact.ProducerPort.CompleteCheckpointGarbageCollection(
            exact.ProducerCandidate);
        exact.SourcePort.CompleteCheckpointGarbageCollection(
            exact.SourceCandidate);
        if (exact.ChildCandidate != null)
        {
            exact.ChildPort.CompleteCheckpointGarbageCollection(
                exact.ChildCandidate);
        }
        exact.Completed = true;
        activeCheckpointGcCandidate = null;
    }

    private bool TryCaptureCheckpointGcProducerRow(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        ProductionFacilityDestructiveDrainOwnerSaveData owner,
        out CombatEquipmentTerminalDrainSaveData row)
    {
        row = null;
        if (entry == null
            || owner == null
            || owner.disposition !=
                ProductionFacilityDestructiveDrainDisposition.Terminalize
            || owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged
            || !string.IsNullOrEmpty(owner.targetDestinationId)
            || !Terminal(owner.commitId, owner.receiptFingerprint)
            || !producerQuery.TryCapture(owner.stepOperationId, out row)
            || row == null
            || row.phase != CombatEquipmentTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc
            || !CombatEquipmentTerminalDrainCanonical.IsValidSave(row)
            || !string.Equals(row.parentOperationId, entry.operationId,
                StringComparison.Ordinal)
            || !string.Equals(row.stepOperationId, owner.stepOperationId,
                StringComparison.Ordinal)
            || !string.Equals(row.source.ownerStableId, owner.ownerStableId,
                StringComparison.Ordinal)
            || !string.Equals(row.source.facilityId, entry.facilityId,
                StringComparison.Ordinal)
            || !string.Equals(row.requestFingerprint, owner.requestFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(row.commitId, owner.commitId,
                StringComparison.Ordinal)
            || !string.Equals(row.receiptFingerprint,
                owner.receiptFingerprint, StringComparison.Ordinal))
        {
            row = null;
            return false;
        }
        return true;
    }

    private CheckpointGcCandidate RequireCheckpointGcCandidate(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || exact.Completed
            || !ReferenceEquals(activeCheckpointGcCandidate, exact)
            || !string.Equals(exact.ParticipantId, CheckpointGcParticipantId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Combat equipment terminal checkpoint GC candidate is invalid.");
        }
        return exact;
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcResult
        CheckpointGcResult(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus status,
            ProductionFacilityDestructiveDrainCheckpointGcReason reason,
            string message,
            int collectedOperationCount = 0) => new(
        status,
        reason,
        context.CheckpointSequence,
        message,
        collectedOperationCount);

    private CombatEquipmentTerminalDrainRequest CaptureRequest(
        ProductionFacilityDestructiveDrainOperationId operation,
        ProductionFacilityHandle facility,
        CombatEquipmentTerminalPreparedSource prepared,
        out ProductionInputDestinationCustodyDrainRequest child)
    {
        CombatEquipmentTerminalFrozenSubject source = prepared?.Source;
        if (source == null || !string.Equals(source.FacilityId,
                facility.InstanceId.Value, StringComparison.Ordinal))
            throw new InvalidOperationException("combat-equipment-terminal-source-owner-invalid");
        string step = ProductionFacilityDestructiveDrainCanonical.BuildStepOperationId(
            operation, ParticipantId, source.OwnerStableId);
        child = null;
        string childStep = string.Empty;
        string childFingerprint = string.Empty;
        ProductionInputDestinationCustodySourceSnapshot snapshot =
            prepared.Custody;
        if (snapshot != null && snapshot.InputQuantity > 0)
        {
            childStep = step + ChildSuffix;
            string failure = string.Empty;
            if (snapshot.InputQuantity != source.PendingInputQuantity
                || snapshot.InputMassGrams != source.PendingInputMassGrams
                || !inputDrain.TryBuildRequest(
                    operation.Value, childStep, source.OwnerStableId,
                    source.SourceId, facility.InstanceId.Value,
                    facility.Position, source.SourceFingerprint, snapshot,
                    out child, out failure)
                || child == null
                || child.InputQuantity != source.PendingInputQuantity
                || child.InputMassGrams != source.PendingInputMassGrams)
                throw new InvalidOperationException(string.IsNullOrEmpty(failure)
                    ? "combat-equipment-terminal-child-capture-invalid" : failure);
            childFingerprint = child.RequestFingerprint;
        }
        string requestFingerprint = CombatEquipmentTerminalDrainCanonical
            .CreateRequestFingerprint(operation.Value, step, source,
                childStep, childFingerprint);
        return new CombatEquipmentTerminalDrainRequest(operation.Value, step,
            source, child, requestFingerprint);
    }

    private ProductionFacilityDestructiveDrainStepResult DriveChild(
        ProductionFacilityDestructiveDrainStepContext context,
        CombatEquipmentTerminalDrainSaveData producerState)
    {
        if (!inputDrain.TryCapture(producerState.inputDestinationDrainStepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData child)
            || !ExactChild(child, producerState))
            return Conflict(CaptureCurrentFingerprint(context.FacilityId));
        int remaining = checked((child.sourceActors?.Count ?? 0)
            + (child.sourceOperations?.Count ?? 0) + 4);
        while (remaining-- > 0)
        {
            ProductionInputDestinationCustodyDrainResult result;
            try { result = inputDrain.TryCommit(child.stepOperationId, child.requestFingerprint); }
            catch { return Conflict(CaptureCurrentFingerprint(context.FacilityId)); }
            if (result.Status == ProductionInputDestinationCustodyDrainStatus.Conflict)
                return Conflict(CaptureCurrentFingerprint(context.FacilityId));
            if (result.Status == ProductionInputDestinationCustodyDrainStatus.Deferred)
                return string.IsNullOrEmpty(result.CommitId)
                    && string.IsNullOrEmpty(result.ReceiptFingerprint)
                    ? Deferred(CaptureCurrentFingerprint(context.FacilityId))
                    : Conflict(CaptureCurrentFingerprint(context.FacilityId));
            if (!inputDrain.TryCapture(child.stepOperationId, out child)
                || !ExactChild(child, producerState))
                return Conflict(CaptureCurrentFingerprint(context.FacilityId));
            if (Terminal(result.CommitId, result.ReceiptFingerprint))
                return ChildTerminal(child.phase)
                    && string.Equals(child.commitId, result.CommitId, StringComparison.Ordinal)
                    && string.Equals(child.receiptFingerprint, result.ReceiptFingerprint,
                        StringComparison.Ordinal)
                    ? Replay(result.CommitId, result.ReceiptFingerprint,
                        CaptureCurrentFingerprint(context.FacilityId))
                    : Conflict(CaptureCurrentFingerprint(context.FacilityId));
        }
        return Conflict(CaptureCurrentFingerprint(context.FacilityId));
    }

    private ProductionFacilityDestructiveDrainStepResult Map(
        ProductionFacilityDestructiveDrainStepContext context,
        CombatEquipmentTerminalDrainResult result,
        bool requireJournalMatch)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (result.Status == CombatEquipmentTerminalDrainStatus.Conflict)
            return Conflict(current);
        if (result.Status == CombatEquipmentTerminalDrainStatus.Deferred)
            return string.IsNullOrEmpty(result.CommitId)
                && string.IsNullOrEmpty(result.ReceiptFingerprint)
                ? Deferred(current) : Conflict(current);
        if (!producerQuery.TryCapture(context.Owner.stepOperationId,
                out CombatEquipmentTerminalDrainSaveData state)
            || !ExactProducerState(state, context))
            return Conflict(current);
        if (!Terminal(result.CommitId, result.ReceiptFingerprint))
            return string.IsNullOrEmpty(result.CommitId)
                && string.IsNullOrEmpty(result.ReceiptFingerprint)
                && !ProducerTerminal(state.phase)
                ? Deferred(current) : Conflict(current);
        if (!ProducerTerminal(state.phase)
            || !string.Equals(state.commitId, result.CommitId, StringComparison.Ordinal)
            || !string.Equals(state.receiptFingerprint, result.ReceiptFingerprint,
                StringComparison.Ordinal)
            || requireJournalMatch
                && (!string.Equals(result.CommitId, context.Owner.commitId,
                        StringComparison.Ordinal)
                    || !string.Equals(result.ReceiptFingerprint,
                        context.Owner.receiptFingerprint, StringComparison.Ordinal)))
            return Conflict(current);
        return new ProductionFacilityDestructiveDrainStepResult(
            result.Status == CombatEquipmentTerminalDrainStatus.Applied
                ? ProductionFacilityDestructiveDrainStepStatus.Applied
                : ProductionFacilityDestructiveDrainStepStatus.Replay,
            result.CommitId, result.ReceiptFingerprint, current);
    }

    private CombatEquipmentTerminalPreparedSource[] CaptureSources(
        BuildingInstanceId facilityId)
    {
        CombatEquipmentTerminalPreparedSource[] values = (sources
                .CaptureFacilitySources(facilityId)
                ?? Array.Empty<CombatEquipmentTerminalPreparedSource>())
            .OrderBy(value => value?.Source?.OwnerStableId, StringComparer.Ordinal)
            .ToArray();
        if (values.Any(value => value?.Source == null
                || !string.Equals(value.Source.FacilityId, facilityId.Value,
                    StringComparison.Ordinal))
            || values.Select(value => value.Source.OwnerStableId)
                .Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException("combat-equipment-terminal-sources-invalid");
        return values;
    }

    private ProductionOutputDestinationLifecycleContribution CaptureContribution(
        BuildingInstanceId facilityId)
    {
        ProductionOutputDestinationLifecycleContribution[] values = lifecycle
            .Capture(facilityId).Contributions
            .Where(value => value != null
                && string.Equals(value.ContributorId, ParticipantId,
                    StringComparison.Ordinal)).ToArray();
        if (values.Length != 1)
            throw new InvalidOperationException("combat-equipment-terminal-contribution-missing");
        return values[0];
    }

    private string CaptureCurrentFingerprint(BuildingInstanceId facilityId) =>
        CaptureContribution(facilityId).DurableSemanticFingerprint;

    private ProductionFacilityHandle ResolveFacility(BuildingInstanceId facilityId)
    {
        ProductionFacilityHandle result = facilities.Capture(facilityId);
        if (result == null || result.IsDestroyed || !result.InstanceId.Equals(facilityId))
            throw new InvalidOperationException(
                "combat-equipment-terminal-facility-resolution-conflict");
        return result;
    }

    private bool TryValidateContext(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        bool owner = context.Owner != null
            && (context.Owner.ownerStableId.StartsWith("craft-order:", StringComparison.Ordinal)
                || context.Owner.ownerStableId.StartsWith("repair-order:", StringComparison.Ordinal));
        bool valid = string.Equals(context.ParticipantId, ParticipantId,
                StringComparison.Ordinal)
            && owner
            && context.Owner.disposition == ProductionFacilityDestructiveDrainDisposition.Terminalize
            && string.IsNullOrEmpty(context.Owner.targetDestinationId)
            && string.Equals(context.Owner.stepOperationId,
                ProductionFacilityDestructiveDrainCanonical.BuildStepOperationId(
                    context.OperationId, ParticipantId, context.Owner.ownerStableId),
                StringComparison.Ordinal)
            && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                context.Owner.requestFingerprint);
        failureReason = valid ? string.Empty : "combat-equipment-terminal-step-context-invalid";
        return valid;
    }

    private static bool ExactProducerRequest(
        CombatEquipmentTerminalDrainSaveData state,
        CombatEquipmentTerminalDrainRequest request) => state != null
        && CombatEquipmentTerminalDrainCanonical.IsValidSave(state)
        && string.Equals(state.parentOperationId, request.ParentOperationId, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId, request.StepOperationId, StringComparison.Ordinal)
        && string.Equals(state.source.ownerStableId, request.Source.OwnerStableId, StringComparison.Ordinal)
        && string.Equals(state.source.sourceFingerprint, request.Source.SourceFingerprint, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint, request.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(state.inputDestinationDrainStepOperationId,
            request.InputDestinationDrainStepOperationId, StringComparison.Ordinal)
        && string.Equals(state.inputDestinationDrainRequestFingerprint,
            request.InputDestinationDrainRequestFingerprint, StringComparison.Ordinal);

    private static bool ExactProducerState(
        CombatEquipmentTerminalDrainSaveData state,
        ProductionFacilityDestructiveDrainStepContext context) => state != null
        && CombatEquipmentTerminalDrainCanonical.IsValidSave(state)
        && string.Equals(state.parentOperationId, context.OperationId.Value, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId, context.Owner.stepOperationId, StringComparison.Ordinal)
        && string.Equals(state.source.ownerStableId, context.Owner.ownerStableId, StringComparison.Ordinal)
        && string.Equals(state.source.facilityId, context.FacilityId.Value, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint, context.Owner.requestFingerprint, StringComparison.Ordinal)
        && (state.source.pendingInputQuantity == 0
            ? string.IsNullOrEmpty(state.inputDestinationDrainStepOperationId)
            : string.Equals(state.inputDestinationDrainStepOperationId,
                context.Owner.stepOperationId + ChildSuffix, StringComparison.Ordinal));

    private static bool ExactChild(
        ProductionInputDestinationCustodyDrainSaveData state,
        ProductionInputDestinationCustodyDrainRequest request) => state != null
        && ProductionInputDestinationCustodyDrainContract.IsValidSave(state)
        && string.Equals(state.parentOperationId, request.ParentOperationId, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId, request.StepOperationId, StringComparison.Ordinal)
        && string.Equals(state.ownerStableId, request.OwnerStableId, StringComparison.Ordinal)
        && string.Equals(state.billId, request.BillId, StringComparison.Ordinal)
        && string.Equals(state.facilityId, request.FacilityId, StringComparison.Ordinal)
        && string.Equals(state.sourceDestinationId, request.SourceDestinationId,
            StringComparison.Ordinal)
        && string.Equals(state.sourceClaimFingerprint,
            request.SourceClaimFingerprint, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint, request.RequestFingerprint, StringComparison.Ordinal)
        && state.inputQuantity == request.InputQuantity
        && state.inputMassGrams == request.InputMassGrams
        && state.releasedQuantity == 0 && state.releasedMassGrams == 0L;

    private static bool ExactChild(
        ProductionInputDestinationCustodyDrainSaveData state,
        CombatEquipmentTerminalDrainSaveData producer) => state != null
        && ProductionInputDestinationCustodyDrainContract.IsValidSave(state)
        && string.Equals(state.parentOperationId, producer.parentOperationId, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId, producer.inputDestinationDrainStepOperationId, StringComparison.Ordinal)
        && string.Equals(state.ownerStableId, producer.source.ownerStableId, StringComparison.Ordinal)
        && string.Equals(state.billId, producer.source.sourceId, StringComparison.Ordinal)
        && string.Equals(state.facilityId, producer.source.facilityId, StringComparison.Ordinal)
        && string.Equals(state.sourceDestinationId, producer.source.inputDestinationId, StringComparison.Ordinal)
        && string.Equals(state.sourceClaimFingerprint,
            producer.source.sourceFingerprint, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint,
            producer.inputDestinationDrainRequestFingerprint, StringComparison.Ordinal)
        && state.inputQuantity == producer.source.pendingInputQuantity
        && state.inputMassGrams == producer.source.pendingInputMassGrams
        && (state.phase < ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            ? state.releasedQuantity == 0 && state.releasedMassGrams == 0L
            : state.releasedQuantity == producer.source.pendingInputQuantity
                && state.releasedMassGrams ==
                    producer.source.pendingInputMassGrams);

    private string CreatePlanFingerprint(
        string contribution,
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerPlan> owners) =>
        ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            string.Join("\n", new[]
            {
                "combat-equipment-terminal-participant-plan@1",
                ParticipantId,
                ContractVersion.ToString(CultureInfo.InvariantCulture),
                contribution
            }.Concat(owners.OrderBy(value => value.OwnerStableId, StringComparer.Ordinal)
                .Select(value => string.Join("|", value.OwnerStableId,
                    ((int)value.Disposition).ToString(CultureInfo.InvariantCulture),
                    value.TargetDestinationId, value.RequestFingerprint)))));

    private static void RequireDestination(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        if (!destinationId.Equals(ProductionOutputDestinationId.FromFacility(facilityId)))
            throw new InvalidOperationException("combat-equipment-terminal-destination-mismatch");
    }

    private static bool HasChild(CombatEquipmentTerminalDrainSaveData value) =>
        !string.IsNullOrEmpty(value?.inputDestinationDrainStepOperationId);
    private static bool Terminal(string commit, string receipt) =>
        ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(commit)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(receipt);
    private static bool ProducerTerminal(CombatEquipmentTerminalDrainPhase phase) =>
        phase is CombatEquipmentTerminalDrainPhase
            .TerminalEffectsCommittedAwaitingOwnerAcknowledgement
            or CombatEquipmentTerminalDrainPhase.OwnerAcknowledgedAwaitingCheckpointGc;
    private static bool ChildTerminal(ProductionInputDestinationCustodyDrainPhase phase) =>
        phase is ProductionInputDestinationCustodyDrainPhase.EffectCommittedAwaitingBillAck
            or ProductionInputDestinationCustodyDrainPhase.BillAcknowledgedAwaitingCheckpointGc;
    private static ProductionFacilityDestructiveDrainStepResult Deferred(string current) =>
        new(ProductionFacilityDestructiveDrainStepStatus.Deferred, string.Empty, string.Empty, current);
    private static ProductionFacilityDestructiveDrainStepResult Replay(
        string commit, string receipt, string current) =>
        new(ProductionFacilityDestructiveDrainStepStatus.Replay, commit, receipt, current);
    private static ProductionFacilityDestructiveDrainStepResult Conflict(string current) =>
        new(ProductionFacilityDestructiveDrainStepStatus.Conflict, string.Empty, string.Empty, current);
    private static ProductionFacilityDestructiveDrainRecoveryResult RecoveryConflict(string current) =>
        new(ProductionFacilityDestructiveDrainRecoveryAction.Conflict, Conflict(current));
}
