using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Narrative.Korean;

public readonly struct PreparedProductionGameplayOutcome
{
    internal PreparedProductionGameplayOutcome(
        PreparedOwnerOutcome prepared,
        GameplayResultKey resultKey,
        long ownerRevision,
        bool replay)
    {
        Prepared = prepared;
        ResultKey = resultKey;
        OwnerRevision = ownerRevision;
        IsReplay = replay;
    }

    internal PreparedOwnerOutcome Prepared { get; }
    public GameplayResultKey ResultKey { get; }
    public long OwnerRevision { get; }
    public bool IsReplay { get; }
    public bool IsValid => IsReplay ? ResultKey.IsValid : Prepared.IsValid;
}

public sealed class ProductionCompletedOutcomeBridge
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly IGameCalendar calendar;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameplayOutcomeDisplayNameQuery displayNames;
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public ProductionCompletedOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameCalendar calendar,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameplayOutcomeDisplayNameQuery displayNames)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        this.displayNames = displayNames
            ?? throw new ArgumentNullException(nameof(displayNames));
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder,
            diagnostics);
    }

    public bool TryPreparePreparedOutput(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        in FacilityBufferPlannedOutputPublicationReceipt publication,
        out PreparedProductionGameplayOutcome prepared,
        out bool capacityDeferred,
        out string failureReason)
    {
        prepared = default;
        capacityDeferred = false;
        if (record == null
            || facility == null
            || record.preparedOutput == null
            || record.preparedOutput.phase !=
                ProductionPreparedOutputPhase.PublicationPrepared)
        {
            failureReason = "production-outcome-owner-not-publication-prepared";
            return false;
        }

        ProductionCompletedOutcomeReceipt receipt;
        try
        {
            receipt = CreatePreparedReceipt(record, facility, worker, publication);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "production-outcome-receipt-invalid:"
                + exception.Message;
            return false;
        }

        if (!transactions.TryPrepare(
                receipt,
                out PreparedOwnerOutcome token,
                out OwnerOutcomeCommitResult replay,
                out capacityDeferred,
                out failureReason))
        {
            return false;
        }
        prepared = new PreparedProductionGameplayOutcome(
            token,
            receipt.ResultKey,
            receipt.CycleSequence,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedProductionGameplayOutcome prepared) => prepared.IsReplay
        ? transactions.Reconcile(prepared.ResultKey)
        : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public void Cancel(in PreparedProductionGameplayOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }

    public bool TryRequireDurablePreparedOutcome(
        ProductionBillId billId,
        int cycleSequence,
        out string failureReason)
    {
        GameplayResultKey key;
        try
        {
            key = new GameplayResultKey(
                ProductionCombatOutcomeIds.ProductionProducerId,
                new GameplayOperationId(
                    $"production-cycle:{billId.Value}:{cycleSequence}"),
                cycleSequence,
                0);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "production-outcome-result-key-invalid:"
                + exception.Message;
            return false;
        }
        OwnerOutcomeCommitResult existing = transactions.Reconcile(key);
        if (existing.DurablyCommitted)
        {
            failureReason = string.Empty;
            return true;
        }
        failureReason = existing.DetailCode.Length == 0
            ? "production-outcome-not-durably-committed"
            : existing.DetailCode;
        return false;
    }

    public bool TryRequireAcknowledgedPreparedOutcome(
        ProductionBillId billId,
        int cycleSequence,
        out string failureReason)
    {
        GameplayResultKey key = new(
            ProductionCombatOutcomeIds.ProductionProducerId,
            new GameplayOperationId(
                $"production-cycle:{billId.Value}:{cycleSequence}"),
            cycleSequence,
            0);
        OwnerOutcomeCommitResult existing = transactions.Reconcile(key);
        if (existing.Acknowledged
            && existing.OutcomeId.IsValid
            && existing.CanonicalPayloadHash.Length == 64)
        {
            failureReason = string.Empty;
            return true;
        }
        failureReason = existing.DetailCode.Length == 0
            ? "production-outcome-awaiting-acknowledgement"
            : existing.DetailCode;
        return false;
    }

    public bool TryCommitExact(
        ProductionRecipeExecutionReceipt exactReceipt,
        string workerPersistentId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (exactReceipt == null)
        {
            failureReason = "production-exact-receipt-missing";
            return false;
        }
        ProductionCompletedOutcomeReceipt receipt;
        try
        {
            ProductionRecipeExecutionCorrelation correlation =
                exactReceipt.Correlation;
            GameplayEntityId facilityEntity = new(
                ProductionCombatOutcomeIds.FacilityKind,
                correlation.FacilityId.Value);
            if (!displayNames.TryGetCurrentName(
                    facilityEntity,
                    out KoreanNameSnapshot facilityDisplayName))
            {
                failureReason = "production-outcome-facility-display-snapshot-missing";
                return false;
            }
            KoreanNameSnapshot workerDisplayName = default;
            if (!string.IsNullOrEmpty(workerPersistentId)
                && !displayNames.TryGetCurrentName(
                    new GameplayEntityId(
                        ProductionCombatOutcomeIds.CharacterKind,
                        workerPersistentId),
                    out workerDisplayName))
            {
                failureReason = "production-outcome-worker-display-snapshot-missing";
                return false;
            }
            ProductionOutcomeLineSnapshot[] lines = exactReceipt.Outputs
                .Select(value => new ProductionOutcomeLineSnapshot(
                    value.OutputLineId,
                    value.CommitIds,
                    value.ItemId,
                    value.Quantity,
                    value.MassGrams,
                    value.CapabilityFingerprint))
                .ToArray();
            ProductionOutcomePhysicalStackSnapshot[] stacks =
                exactReceipt.PhysicalSlices
                    .Select(value => new ProductionOutcomePhysicalStackSnapshot(
                        value.StackId,
                        value.OutputLineId,
                        value.ItemId,
                        value.Quantity,
                        value.MassGrams,
                        value.ItemInstanceId,
                        value.CommitId,
                        value.ComponentSignature))
                    .ToArray();
            receipt = new ProductionCompletedOutcomeReceipt(
                correlation.BillId,
                correlation.CycleSequence,
                correlation.RecipeId,
                correlation.FacilityId,
                facilityDisplayName,
                workerPersistentId,
                workerDisplayName,
                exactReceipt.BatchCommitId,
                exactReceipt.OutcomeFingerprint,
                exactReceipt.PlannedOutputFingerprint,
                ProductionOutputDestinationId.FromFacility(
                    correlation.FacilityId).Value,
                exactReceipt.WipInputCommitId,
                exactReceipt.WipInputQuantity,
                exactReceipt.WipInputMassGrams,
                0L,
                0L,
                0L,
                0L,
                lines,
                stacks,
                Math.Max(1, calendar.Day));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "production-outcome-receipt-invalid:" + exception.Message;
            return false;
        }

        if (!transactions.TryPrepare(
                receipt,
                out PreparedOwnerOutcome prepared,
                out OwnerOutcomeCommitResult replay,
                out _,
                out failureReason))
        {
            return false;
        }
        if (replay.DurablyCommitted)
            return true;
        OwnerOutcomeCommitResult commit = transactions.Commit(
            prepared,
            receipt.CycleSequence);
        if (!commit.DurablyCommitted)
        {
            transactions.Cancel(prepared);
            failureReason = commit.DetailCode;
            return false;
        }
        return true;
    }

    private bool IsCommittedOrPublished(GameplayResultKey resultKey)
    {
        var rows = diagnostics.GetOutboxSnapshot();
        for (int index = 0; index < rows.Count; index++)
        {
            GameplayOutcomeOutboxSnapshot row = rows[index];
            if (row.ResultKey == resultKey
                && row.Lifecycle >= GameplayOutcomeRecordLifecycle.Committed)
            {
                return true;
            }
        }
        return false;
    }

    private ProductionCompletedOutcomeReceipt CreatePreparedReceipt(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        in FacilityBufferPlannedOutputPublicationReceipt publication)
    {
        ProductionPreparedOutputBatchSaveData batch = record.preparedOutput;
        if (!string.Equals(
                batch.batchCommitId,
                publication.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                batch.outcomeFingerprint,
                publication.OutcomeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                batch.admissionFingerprint,
                publication.PlannedOutputFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                batch.destinationId,
                publication.DestinationId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Prepared output publication preview does not match its bill batch.");
        }

        GameplayEntityId facilityEntity = new(
            ProductionCombatOutcomeIds.FacilityKind,
            facility.InstanceId.Value);
        if (!displayNames.TryGetCurrentName(
                facilityEntity,
                out KoreanNameSnapshot facilityName))
        {
            throw new InvalidOperationException(
                "The facility display snapshot is unavailable.");
        }
        string workerId = worker?.AuthorityKind ==
                ProductionWorkerAuthorityKind.Actor
            ? worker.PersistentId
            : string.Empty;
        KoreanNameSnapshot workerName = default;
        if (!string.IsNullOrEmpty(workerId)
            && !displayNames.TryGetCurrentName(
                new GameplayEntityId(
                    ProductionCombatOutcomeIds.CharacterKind,
                    workerId),
                out workerName))
        {
            throw new InvalidOperationException(
                "The worker display snapshot is unavailable.");
        }

        ProductionOutcomeLineSnapshot[] lines = batch.lines
            .Where(value => value != null
                && ProductionOutputRoleRules.IsPhysical(value.role)
                && value.rollSucceeded
                && value.quantity > 0
                && value.exactMassGrams > 0L)
            .Select(value => new ProductionOutcomeLineSnapshot(
                value.outputLineId,
                value.lineCommitId,
                value.itemId,
                value.quantity,
                value.exactMassGrams,
                value.outputCapabilityFingerprint))
            .ToArray();
        ProductionOutcomePhysicalStackSnapshot[] stacks = publication.Stacks
            .Select(value => new ProductionOutcomePhysicalStackSnapshot(
                value.StackId,
                value.OutputLineId,
                value.ItemDefinitionId.Value,
                value.Quantity,
                value.MassGrams,
                value.ItemInstanceId))
            .ToArray();
        return new ProductionCompletedOutcomeReceipt(
            record.billId,
            record.cycleSequence,
            record.recipeId,
            record.buildingInstanceId,
            facilityName,
            workerId,
            workerName,
            batch.batchCommitId,
            batch.outcomeFingerprint,
            publication.PlannedOutputFingerprint,
            batch.destinationId,
            record.wipInputCommitId,
            record.wipInputQuantity,
            record.wipInputMassGrams,
            record.processCleanWaterMassGrams,
            record.processWastewaterMassGrams,
            batch.totalDeclaredLossMassGrams,
            batch.totalDeclaredExternalInputMassGrams,
            lines,
            stacks,
            Math.Max(1, calendar.Day));
    }
}

public sealed class CombatDamageOutcomeBridge
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly GameplayOutcomeLedger ledger;

    public CombatDamageOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    public bool TryReserve(
        string attackOperationId,
        long attackRevision,
        int absoluteDay,
        out ReservedCombatDamageOutcome reserved,
        out bool capacityDeferred,
        out string failureReason)
    {
        reserved = default;
        capacityDeferred = false;
        failureReason = string.Empty;
        GameplayOperationId operationId;
        GameplayResultKey resultKey;
        try
        {
            operationId = new GameplayOperationId(attackOperationId);
            resultKey = new GameplayResultKey(
                ProductionCombatOutcomeIds.CombatProducerId,
                operationId,
                attackRevision,
                0);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "combat-outcome-result-key-invalid:" + exception.Message;
            return false;
        }

        OutcomeWriteRequirements requirements =
            CombatDamageOutcomeAdapter.CreateRequirements(
                resultKey,
                absoluteDay,
                ledger.CurrentWorldEpoch,
                attackRevision);
        OutcomePrepareResult result = recorder.TryReserve(
            requirements,
            out PreparedOutcomeReservation reservation);
        if (!result.Success)
        {
            capacityDeferred = result.Code == OutcomePrepareCode.CapacityDeferred;
            failureReason = "combat-outcome-reserve-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }

        reserved = new ReservedCombatDamageOutcome(
            reservation,
            attackOperationId,
            attackRevision);
        return true;
    }

    public void Cancel(in ReservedCombatDamageOutcome reserved)
    {
        if (reserved.IsValid)
            recorder.CancelReservation(reserved.Reservation);
    }

    public void CancelPrepared(in PreparedOutcomeToken prepared)
    {
        if (prepared.IsValid)
            recorder.CancelPrepared(prepared);
    }

    public bool TryPrepare(
        in CombatDamageOutcomeReceipt receipt,
        in ReservedCombatDamageOutcome reserved,
        out PreparedOutcomeToken prepared,
        out string failureReason)
    {
        prepared = default;
        failureReason = string.Empty;
        if (!reserved.IsValid
            || !string.Equals(
                reserved.OperationId,
                receipt.AttackOperationId,
                StringComparison.Ordinal)
            || reserved.AttackRevision != receipt.AttackRevision)
        {
            failureReason = "combat-outcome-reservation-owner-mismatch";
            return false;
        }

        OutcomePrepareResult result = recorder.TryWriteReserved(
            receipt,
            reserved.Reservation,
            out prepared);
        if (!result.Success)
        {
            recorder.CancelReservation(reserved.Reservation);
            failureReason = "combat-outcome-write-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }
        return true;
    }

    public bool TryCommit(
        in PreparedOutcomeToken prepared,
        long expectedOwnerRevision,
        out string failureReason)
    {
        failureReason = string.Empty;
        OutcomeCommitResult result = recorder.CommitPrepared(
            prepared,
            expectedOwnerRevision,
            out CommittedOutcomeToken committed);
        if (!result.Success)
        {
            recorder.CancelPrepared(prepared);
            failureReason = "combat-outcome-commit-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }

        OutcomeDeliveryResult delivery = recorder.TryDeliver(committed);
        if (delivery.Published)
            recorder.Acknowledge(committed);
        return true;
    }
}
