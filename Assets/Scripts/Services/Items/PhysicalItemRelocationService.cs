using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public readonly struct PhysicalItemRelocationReceipt
{
    internal PhysicalItemRelocationReceipt(
        string operationId,
        string reasonCode,
        string sourceStackId,
        string destinationStackId,
        string itemId,
        int quantity,
        long massGrams,
        Vector2Int sourcePosition,
        Vector2Int destinationPosition)
    {
        OperationId = operationId;
        ReasonCode = reasonCode;
        SourceStackId = sourceStackId;
        DestinationStackId = destinationStackId;
        ItemId = itemId;
        Quantity = quantity;
        MassGrams = massGrams;
        SourcePosition = sourcePosition;
        DestinationPosition = destinationPosition;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string SourceStackId { get; }
    public string DestinationStackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public Vector2Int SourcePosition { get; }
    public Vector2Int DestinationPosition { get; }
    public bool IsCommitted => OperationId?.Length > 0
        && ReasonCode?.Length > 0
        && SourceStackId?.Length > 0
        && DestinationStackId?.Length > 0
        && ItemId?.Length > 0
        && Quantity > 0
        && MassGrams > 0L;
}

public interface IPhysicalItemRelocationService
{
    bool TryRelocateQuantity(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason);
}

/// <summary>
/// Exact identity-preserving movement inside the physical world. This is not
/// a Transform: input and output item identity, quantity, components, and gram
/// mass must remain equal.
/// </summary>
public sealed class PhysicalItemRelocationService : IPhysicalItemRelocationService
{
    private readonly WorldItemRepository repository;
    private readonly IPreparedPhysicalItemRelocationService relocations;
    private readonly IPhysicalItemRelocationOutcomeParticipant outcomes;
    private readonly IGameplayOutcomeRecorder outcomeRecorder;
    private readonly IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics;

    public PhysicalItemRelocationService(
        WorldItemRepository repository,
        IPreparedPhysicalItemRelocationService relocations,
        IPhysicalItemRelocationOutcomeParticipant outcomes,
        IGameplayOutcomeRecorder outcomeRecorder,
        IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.relocations = relocations
            ?? throw new ArgumentNullException(nameof(relocations));
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        this.outcomeRecorder = outcomeRecorder
            ?? throw new ArgumentNullException(nameof(outcomeRecorder));
        this.outcomeDiagnostics = outcomeDiagnostics
            ?? throw new ArgumentNullException(nameof(outcomeDiagnostics));
    }

    public bool TryRelocateQuantity(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        string reason = reasonCode?.Trim() ?? string.Empty;
        string source = sourceStackId?.Trim() ?? string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        string requestFingerprint = CreateRequestFingerprint(
            source,
            quantity,
            destinationPosition,
            destinationState,
            destination,
            operation,
            reason);
        if (repository.TryGetPhysicalItemRelocation(
                operation,
                out PhysicalItemRelocationSaveData existing))
        {
            return TryReplay(
                existing,
                requestFingerprint,
                out receipt,
                out failureReason);
        }
        if (!relocations.TryPrepare(
                source,
                quantity,
                destinationPosition,
                destinationState,
                destination,
                operation,
                reason,
                out IPreparedPhysicalItemRelocation preparedRelocation,
                out failureReason))
            return false;
        PreparedPhysicalItemRelocationPreview preview =
            preparedRelocation.Preview;
        if (!outcomes.TryPrepare(
                preview,
                out IPreparedPhysicalItemGameplayOutcome preparedOutcome,
                out failureReason))
        {
            preparedRelocation.Cancel();
            return false;
        }
        if (!repository.CanAddPhysicalItemRelocation(operation))
        {
            preparedOutcome.Cancel();
            preparedRelocation.Cancel();
            failureReason = "physical-relocation-journal-capacity-exhausted";
            return false;
        }
        if (!preparedRelocation.TryApply(
                out IReversiblePhysicalItemRelocation transaction,
                out failureReason))
        {
            preparedOutcome.Cancel();
            return false;
        }

        PhysicalItemRelocationSaveData journal = CreateJournal(
            preview,
            destinationState,
            destination,
            requestFingerprint,
            preparedOutcome.ResultKey);
        try
        {
            repository.AddPhysicalItemRelocation(journal);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            if (!transaction.TryRollback(out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Physical relocation journal failed and the physical mutation could not be rolled back: "
                    + rollbackFailure,
                    exception);
            }
            preparedOutcome.Cancel();
            failureReason = "physical-relocation-journal-write-failed:"
                + exception.Message;
            return false;
        }

        bool canonicalCommitted;
        PhysicalGameplayOutcomeAttachment attachment;
        try
        {
            _ = preparedOutcome.TryCommit(
                preview.OwnerRevision,
                out attachment,
                out canonicalCommitted,
                out failureReason);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            canonicalCommitted = TryGetCanonicalAttachment(
                preparedOutcome.ResultKey,
                out attachment);
            if (canonicalCommitted)
            {
                PersistCanonical(journal, attachment);
                transaction.TryAcknowledge(out _);
                failureReason =
                    "physical-relocation-outcome-reconciliation-pending:"
                    + exception.Message;
                receipt = transaction.Receipt;
                return true;
            }
            if (!transaction.TryRollback(out string rollbackFailure))
                throw new InvalidOperationException(
                    "Physical relocation outcome failed and the physical mutation could not be rolled back: "
                    + rollbackFailure,
                    exception);
            preparedOutcome.Cancel();
            repository.RemovePhysicalItemRelocation(operation);
            failureReason = "physical-relocation-outcome-commit-exception:"
                + exception.Message;
            return false;
        }
        if (!canonicalCommitted)
        {
            if (!transaction.TryRollback(out string rollbackFailure))
                throw new InvalidOperationException(
                    "Physical relocation outcome was rejected and the physical mutation could not be rolled back: "
                    + rollbackFailure);
            preparedOutcome.Cancel();
            repository.RemovePhysicalItemRelocation(operation);
            return false;
        }

        // Once the ledger commit is canonical the relocation must never roll
        // back. Delivery/acknowledgement reconciliation belongs to the ledger.
        if (canonicalCommitted)
        {
            if (!attachment.IsValid)
                TryGetCanonicalAttachment(preparedOutcome.ResultKey, out attachment);
            PersistCanonical(journal, attachment);
        }
        receipt = transaction.Receipt;
        if (!transaction.TryAcknowledge(out string acknowledgementFailure)
            && failureReason.Length == 0)
            failureReason = acknowledgementFailure;
        return true;
    }

    private bool TryReplay(
        PhysicalItemRelocationSaveData journal,
        string requestFingerprint,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason)
    {
        receipt = RestoreReceipt(journal);
        failureReason = string.Empty;
        if (!receipt.IsCommitted
            || !string.Equals(
                journal.requestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            receipt = default;
            failureReason = "physical-relocation-operation-conflict:"
                + journal.operationId;
            return false;
        }

        GameplayResultKey expected = RestoreExpectedResultKey(journal);
        if (!expected.IsValid)
        {
            receipt = default;
            failureReason = "physical-relocation-journal-result-key-invalid";
            return false;
        }
        outcomeRecorder.RetryPendingDeliveries(1);
        if (!TryGetCanonicalAttachment(expected, out var attachment))
        {
            receipt = default;
            failureReason =
                "physical-relocation-canonical-outcome-unresolved";
            return false;
        }
        if (journal.gameplayOutcome != null)
        {
            PhysicalGameplayOutcomeAttachment saved =
                PhysicalGameplayOutcomeSaveCodec.FromSave(
                    journal.gameplayOutcome);
            if (!saved.IsValid
                || !saved.ResultKey.Equals(attachment.ResultKey)
                || !saved.OutcomeId.Equals(attachment.OutcomeId)
                || !string.Equals(
                    saved.CanonicalPayloadHash,
                    attachment.CanonicalPayloadHash,
                    StringComparison.Ordinal))
            {
                receipt = default;
                failureReason =
                    "physical-relocation-canonical-outcome-conflict";
                return false;
            }
        }
        PersistCanonical(journal, attachment);
        if (!attachment.HasAcknowledgementProof)
        {
            failureReason =
                "physical-relocation-outcome-delivery-pending";
        }
        return true;
    }

    private void PersistCanonical(
        PhysicalItemRelocationSaveData journal,
        in PhysicalGameplayOutcomeAttachment attachment)
    {
        journal.phase = (int)(attachment.HasAcknowledgementProof
            ? PhysicalItemRelocationJournalPhase.PublishedAcknowledged
            : PhysicalItemRelocationJournalPhase.CanonicalOutcomeCommitted);
        if (attachment.IsValid)
        {
            journal.gameplayOutcome =
                PhysicalGameplayOutcomeSaveCodec.ToSave(attachment);
        }
        if (!repository.TryUpdatePhysicalItemRelocation(journal))
        {
            throw new InvalidOperationException(
                "physical-relocation-journal-canonical-update-missing:"
                + journal.operationId);
        }
    }

    private bool TryGetCanonicalAttachment(
        GameplayResultKey expected,
        out PhysicalGameplayOutcomeAttachment attachment)
    {
        attachment = default;
        try
        {
            if (!outcomeDiagnostics.TryGetResultIdentity(
                    expected,
                    out GameplayOutcomeReplayIdentity identity)
                || !identity.ResultKey.Equals(expected)
                || identity.State is < GameplayOutcomeReplayState.Committed
                    or > GameplayOutcomeReplayState.Forgotten
                || !identity.HasCanonicalPayloadHash)
            {
                return false;
            }
            attachment = new PhysicalGameplayOutcomeAttachment(
                identity.ResultKey,
                identity.OutcomeId,
                identity.State,
                identity.CanonicalPayloadHash);
            return attachment.IsValid;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            Debug.LogError(
                "Physical relocation canonical reconciliation failed: "
                + exception);
            return false;
        }
    }

    private static PhysicalItemRelocationSaveData CreateJournal(
        in PreparedPhysicalItemRelocationPreview preview,
        WorldItemStackState destinationState,
        string destinationId,
        string requestFingerprint,
        in GameplayResultKey resultKey)
    {
        PhysicalItemRelocationReceipt exact = preview.Receipt;
        return new PhysicalItemRelocationSaveData
        {
            operationId = exact.OperationId,
            reasonCode = exact.ReasonCode,
            requestFingerprint = requestFingerprint,
            phase = (int)PhysicalItemRelocationJournalPhase.DomainCommitted,
            sourceStackId = exact.SourceStackId,
            destinationStackId = exact.DestinationStackId,
            itemDefinitionId = exact.ItemId,
            itemInstanceId = preview.ItemInstanceId,
            quantity = exact.Quantity,
            massGrams = exact.MassGrams,
            sourceX = exact.SourcePosition.x,
            sourceY = exact.SourcePosition.y,
            destinationX = exact.DestinationPosition.x,
            destinationY = exact.DestinationPosition.y,
            destinationState = (int)destinationState,
            destinationId = destinationId,
            outcomeOwnerRevision = preview.OwnerRevision,
            displayText = preview.DisplayName.DisplayText,
            displaySnapshotRevision =
                preview.DisplayName.DisplaySnapshotRevision,
            pronunciationMode =
                (int)preview.DisplayName.PronunciationHint.Mode,
            pronunciationValue =
                preview.DisplayName.PronunciationHint.Value,
            explicitFinalConsonant = (int)preview.DisplayName
                .PronunciationHint.ExplicitFinalConsonant,
            pronunciationRevision =
                preview.DisplayName.PronunciationHint.Revision,
            locale = preview.DisplayName.Locale,
            expectedOutcomeProducerId = resultKey.ProducerId,
            expectedOutcomeOperationId = resultKey.OperationId.Value,
            expectedOutcomeCommitRevision = resultKey.CommitRevision,
            expectedOutcomeLocalResultIndex = resultKey.LocalResultIndex
        };
    }

    private static PhysicalItemRelocationReceipt RestoreReceipt(
        PhysicalItemRelocationSaveData journal) => new(
        journal.operationId,
        journal.reasonCode,
        journal.sourceStackId,
        journal.destinationStackId,
        journal.itemDefinitionId,
        journal.quantity,
        journal.massGrams,
        new Vector2Int(journal.sourceX, journal.sourceY),
        new Vector2Int(journal.destinationX, journal.destinationY));

    private static GameplayResultKey RestoreExpectedResultKey(
        PhysicalItemRelocationSaveData journal) => new(
        journal.expectedOutcomeProducerId,
        new GameplayOperationId(journal.expectedOutcomeOperationId),
        journal.expectedOutcomeCommitRevision,
        journal.expectedOutcomeLocalResultIndex);

    private static string CreateRequestFingerprint(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode)
    {
        StringBuilder canonical = new();
        Append(canonical, operationId);
        Append(canonical, reasonCode);
        Append(canonical, sourceStackId);
        Append(canonical, quantity.ToString(CultureInfo.InvariantCulture));
        Append(canonical, destinationPosition.x.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, destinationPosition.y.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, ((int)destinationState).ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, destinationId);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(
                sha.ComputeHash(Encoding.UTF8.GetBytes(
                    canonical.ToString())))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length)
            .Append(':')
            .Append(safe)
            .Append('|');
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;
}
