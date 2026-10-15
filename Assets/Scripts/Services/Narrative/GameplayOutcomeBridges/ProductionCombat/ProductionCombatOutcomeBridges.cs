using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Narrative.Korean;

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
        if (reserved.HasDamageReservation)
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
        if (!reserved.HasDamageReservation
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
