using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Synchronous transaction boundary for the one irreversible gameplay effect of
/// the memory-erasure seal. The physical stack remains owned by the item
/// repository; this service only holds a short-lived rollback handle while the
/// acquired-trait aggregate publishes its new revision.
/// </summary>
public sealed class MemoryErasureSealTransactionService :
    IMemoryErasureSealTransactionService
{
    internal const string ConsumeReasonCode =
        "memory-erasure-seal-consume";
    internal const string AuditPrefix =
        "memory-erasure-seal-audit:";

    private readonly IGameContentDefinitionSource definitions;
    private readonly IGameCalendar calendar;
    private readonly IReversibleCarriedPhysicalItemBatchDispositionService
        physicalDisposition;
    private readonly IMemoryErasureOutcomeCommitter outcomeCommitter;

    public MemoryErasureSealTransactionService(
        IGameContentDefinitionSource definitions,
        IGameCalendar calendar,
        IReversibleCarriedPhysicalItemBatchDispositionService
            physicalDisposition,
        IMemoryErasureOutcomeCommitter outcomeCommitter = null)
    {
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.physicalDisposition = physicalDisposition
            ?? throw new ArgumentNullException(nameof(physicalDisposition));
        this.outcomeCommitter = outcomeCommitter;
    }

    [GameplayInternalOnly(
        "The confirmed use-order owns the selected carried stack and calls this synchronous cross-aggregate transaction.",
        "MemoryErasureSealCommandService")]
    public MemoryErasureSealUseResult TryErase(
        CharacterActor target,
        string traitInstanceId,
        CharacterCarryInventory carry,
        string carriedStackId,
        string operationId)
    {
        string targetId = target?.BuildingCharacterId.Value ?? string.Empty;
        string traitId = traitInstanceId?.Trim() ?? string.Empty;
        string stackId = carriedStackId?.Trim() ?? string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        string auditId = BuildAuditId(operation, targetId, traitId);
        if (target == null
            || target.Progression == null
            || carry == null
            || targetId.Length == 0
            || traitId.Length == 0
            || stackId.Length == 0
            || operation.Length == 0
            || !string.Equals(operation, operationId, StringComparison.Ordinal))
        {
            return Result(
                MemoryErasureSealUseStatus.InvalidRequest,
                operation,
                auditId,
                targetId,
                traitId,
                detail: "target, active trait, exact carried stack, or canonical operation is missing");
        }

        CharacterAcquiredTraitAggregateState before =
            target.Progression.CaptureAcquiredTraitState();
        if (before.revision == int.MaxValue)
        {
            return Result(
                MemoryErasureSealUseStatus.InvalidRequest,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                detail: "the acquired-trait revision cannot advance further");
        }
        if (!before.TryGetInstance(
                traitId,
                out CharacterAcquiredTraitInstanceState selected))
        {
            return Result(
                MemoryErasureSealUseStatus.TargetChanged,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                detail: "the selected acquired-trait instance no longer exists");
        }
        if (selected.erased)
        {
            bool sameCommit = string.Equals(
                selected.erasureAuditId,
                auditId,
                StringComparison.Ordinal);
            return Result(
                sameCommit
                    ? MemoryErasureSealUseStatus.AlreadyCompleted
                    : MemoryErasureSealUseStatus.TargetChanged,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                before.revision,
                selected.erasedAt,
                detail: sameCommit
                    ? "the same erasure transaction is already committed"
                    : "the selected acquired trait was erased by another transaction");
        }

        CharacterCarriedItemSaveData carried = carry.Items.SingleOrDefault(
            value => value != null
                && value.quantity == MemoryErasureSealItemRules.UseQuantity
                && string.Equals(
                    value.carriedStackId?.Trim(),
                    stackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.itemId,
                    MemoryErasureSealItemRules.ItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ownerOperationId?.Trim(),
                    operation,
                    StringComparison.Ordinal));
        if (carried == null)
        {
            return Result(
                MemoryErasureSealUseStatus.InvalidRequest,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                detail: "the exact one-unit carried seal is not owned by this operation");
        }

        CharacterAcquiredTraitSettingsSO settings;
        CharacterAcquiredTraitModuleSO[] modules;
        try
        {
            settings = definitions.RequireSingle<CharacterAcquiredTraitSettingsSO>();
            modules = definitions.GetAll<CharacterAcquiredTraitModuleSO>()
                .Where(value => value != null)
                .ToArray();
        }
        catch (Exception exception)
        {
            return Result(
                MemoryErasureSealUseStatus.InvalidRequest,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                detail: "acquired-trait definition authority failed: "
                    + exception.Message);
        }

        long erasedAt = Math.Max(0L, calendar.AbsoluteHour);
        CharacterAcquiredTraitAggregateState candidate = before.Clone();
        CharacterAcquiredTraitInstanceState mutable =
            candidate.instances.Single(value => value != null
                && string.Equals(
                    value.instanceId,
                    traitId,
                    StringComparison.Ordinal));
        candidate.revision = checked(before.revision + 1);
        mutable.erased = true;
        mutable.erasedAt = erasedAt;
        mutable.erasureAuditId = auditId;
        mutable.erasedRevision = candidate.revision;

        IReadOnlyList<CharacterAcquiredTraitValidationIssue> preflight =
            CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(
                candidate,
                target.Progression.NarrativeLedger,
                settings,
                modules);
        if (preflight.Count > 0)
        {
            return Result(
                MemoryErasureSealUseStatus.InvalidRequest,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                detail: "erasure candidate validation failed: "
                    + string.Join(" | ", preflight));
        }

        int absoluteDay = Math.Max(0, calendar.Day);
        ReservedMemoryErasureOutcome reservedOutcome = default;
        if (outcomeCommitter != null
            && !outcomeCommitter.TryReserveSuccess(
                operation,
                absoluteDay,
                out reservedOutcome,
                out string outcomeReserveFailure))
        {
            return Result(
                MemoryErasureSealUseStatus.InvalidRequest,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                detail: "memory-erasure outcome pre-reserve failed: "
                    + outcomeReserveFailure);
        }

        CharacterCarryInventorySaveData carryBefore = carry.Capture();
        IReversiblePhysicalItemDisposition physical = null;
        bool traitPublished = false;
        bool physicalAcknowledged = false;
        PreparedEvolutionOutcome preparedOutcome = default;
        try
        {
            if (!carry.TryConsumeCarriedStack(
                    stackId,
                    MemoryErasureSealItemRules.ItemId,
                    MemoryErasureSealItemRules.UseQuantity))
            {
                RestoreCarry(carry, carryBefore);
                outcomeCommitter?.Cancel(reservedOutcome);
                return Result(
                    MemoryErasureSealUseStatus.PhysicalCommitFailed,
                    operation,
                    auditId,
                    targetId,
                    traitId,
                    before.revision,
                    detail: "the carried-inventory mirror rejected the exact seal debit");
            }
            if (!physicalDisposition.TryCommitCarriedSinkReversible(
                    stackId,
                    MemoryErasureSealItemRules.UseQuantity,
                    operation,
                    ConsumeReasonCode,
                    out physical,
                    out string physicalFailure))
            {
                RestoreCarry(carry, carryBefore);
                outcomeCommitter?.Cancel(reservedOutcome);
                return Result(
                    MemoryErasureSealUseStatus.PhysicalCommitFailed,
                    operation,
                    auditId,
                    targetId,
                    traitId,
                    before.revision,
                    detail: physicalFailure);
            }

            if (!target.Progression.TryCommitAcquiredTraitState(
                    candidate,
                    before.revision,
                    settings,
                    modules,
                    out IReadOnlyList<CharacterAcquiredTraitValidationIssue>
                        commitIssues))
            {
                outcomeCommitter?.Cancel(reservedOutcome);
                return RollBack(
                    target,
                    before,
                    candidate.revision,
                    settings,
                    modules,
                    carry,
                    carryBefore,
                    physical,
                    traitPublished: false,
                    operation,
                    auditId,
                    targetId,
                    traitId,
                    "acquired-trait commit failed: "
                        + string.Join(" | ", commitIssues));
            }
            traitPublished = true;

            MemoryErasureSealUseResult committedResult = Result(
                MemoryErasureSealUseStatus.Succeeded,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                candidate.revision,
                erasedAt,
                physical.Receipt.CommitId,
                "exact seal consumed and only the selected acquired-trait source erased");
            MemoryErasureOutcomeReceipt outcomeReceipt = new(
                committedResult,
                absoluteDay);
            if (outcomeCommitter != null
                && !outcomeCommitter.TryWriteReservedSuccess(
                    outcomeReceipt,
                    reservedOutcome,
                    out preparedOutcome,
                    out string outcomeWriteFailure))
            {
                return RollBack(
                    target,
                    before,
                    candidate.revision,
                    settings,
                    modules,
                    carry,
                    carryBefore,
                    physical,
                    traitPublished,
                    operation,
                    auditId,
                    targetId,
                    traitId,
                    "memory-erasure outcome receipt write failed: "
                        + outcomeWriteFailure);
            }

            if (!physical.TryAcknowledge(out string acknowledgeFailure))
            {
                outcomeCommitter?.Cancel(preparedOutcome);
                return RollBack(
                    target,
                    before,
                    candidate.revision,
                    settings,
                    modules,
                    carry,
                    carryBefore,
                    physical,
                    traitPublished,
                    operation,
                    auditId,
                    targetId,
                    traitId,
                    "physical terminal receipt acknowledgement failed: "
                        + acknowledgeFailure);
            }
            physicalAcknowledged = true;

            if (outcomeCommitter != null
                && !outcomeCommitter.TryCommit(
                    preparedOutcome,
                    out string outcomeCommitFailure))
            {
                throw new InvalidOperationException(
                    "Memory-erasure domain committed without its mandatory outcome: "
                    + outcomeCommitFailure);
            }
            return committedResult;
        }
        catch (Exception exception)
        {
            outcomeCommitter?.Cancel(preparedOutcome);
            outcomeCommitter?.Cancel(reservedOutcome);
            if (physicalAcknowledged)
            {
                throw new InvalidOperationException(
                    "Memory-erasure outcome commit failed after the physical authority acknowledged its commit.",
                    exception);
            }
            if (physical == null)
            {
                RestoreCarry(carry, carryBefore);
                Debug.LogError(
                    $"Memory-erasure seal transaction failed before its physical commit: {exception}");
                return Result(
                    MemoryErasureSealUseStatus.PhysicalCommitFailed,
                    operation,
                    auditId,
                    targetId,
                    traitId,
                    before.revision,
                    detail: exception.Message);
            }

            CharacterAcquiredTraitAggregateState published =
                target.Progression.CaptureAcquiredTraitState();
            traitPublished = traitPublished
                || published.revision == candidate.revision
                && published.TryGetInstance(traitId, out var afterFailure)
                && string.Equals(
                    afterFailure.erasureAuditId,
                    auditId,
                    StringComparison.Ordinal);
            return RollBack(
                target,
                before,
                candidate.revision,
                settings,
                modules,
                carry,
                carryBefore,
                physical,
                traitPublished,
                operation,
                auditId,
                targetId,
                traitId,
                "transaction threw: " + exception.Message);
        }
    }

    internal static string BuildAuditId(
        string operationId,
        string targetCharacterId,
        string traitInstanceId) => AuditPrefix
        + NarrativeInferenceHash.ComputeSha256Utf8(
            (operationId?.Trim() ?? string.Empty)
            + "|" + (targetCharacterId?.Trim() ?? string.Empty)
            + "|" + (traitInstanceId?.Trim() ?? string.Empty));

    private static MemoryErasureSealUseResult RollBack(
        CharacterActor target,
        CharacterAcquiredTraitAggregateState before,
        int expectedPublishedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        CharacterCarryInventory carry,
        CharacterCarryInventorySaveData carryBefore,
        IReversiblePhysicalItemDisposition physical,
        bool traitPublished,
        string operation,
        string auditId,
        string targetId,
        string traitId,
        string originalFailure)
    {
        IReadOnlyList<CharacterAcquiredTraitValidationIssue> rollbackIssues =
            Array.Empty<CharacterAcquiredTraitValidationIssue>();
        bool traitRestored = !traitPublished;
        if (traitPublished)
        {
            try
            {
                traitRestored = target.Progression.TryRollbackAcquiredTraitState(
                    before,
                    expectedPublishedRevision,
                    settings,
                    modules,
                    out rollbackIssues);
            }
            catch (Exception exception)
            {
                CharacterAcquiredTraitAggregateState restored =
                    target.Progression.CaptureAcquiredTraitState();
                traitRestored = restored.revision == before.revision
                    && restored.TryGetInstance(
                        traitId,
                        out CharacterAcquiredTraitInstanceState restoredTrait)
                    && !restoredTrait.erased;
                rollbackIssues = new[]
                {
                    new CharacterAcquiredTraitValidationIssue(
                        CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                        "rollback notification threw: " + exception.Message)
                };
            }
        }
        bool physicalRestored = physical.TryRollback(
            out string physicalRollbackFailure);
        // A failed repository rollback must never be mirrored as carried cargo;
        // doing so would duplicate the same physical item authority.
        if (physicalRestored)
            RestoreCarry(carry, carryBefore);
        if (!traitRestored || !physicalRestored)
        {
            string detail = originalFailure
                + $"; rollback trait={traitRestored}, physical={physicalRestored}"
                + (traitRestored ? string.Empty
                    : "; trait=" + string.Join(" | ", rollbackIssues))
                + (physicalRestored ? string.Empty
                    : "; physical=" + physicalRollbackFailure);
            Debug.LogError(
                "Memory-erasure seal atomic recovery failed: " + detail);
            return Result(
                MemoryErasureSealUseStatus.RecoveryFailed,
                operation,
                auditId,
                targetId,
                traitId,
                before.revision,
                detail: detail);
        }

        return Result(
            MemoryErasureSealUseStatus.TraitCommitFailed,
            operation,
            auditId,
            targetId,
            traitId,
            before.revision,
            detail: originalFailure);
    }

    private static void RestoreCarry(
        CharacterCarryInventory carry,
        CharacterCarryInventorySaveData snapshot)
    {
        try
        {
            carry.Restore(snapshot);
        }
        catch (Exception exception)
        {
            // CharacterCarryInventory restores its authoritative list before
            // raising Changed. Keep the exact restored authority and expose the
            // observer failure loudly without attempting a second item copy.
            Debug.LogError(
                "Memory-erasure seal carry restore notification failed: "
                + exception);
        }
    }

    private static MemoryErasureSealUseResult Result(
        MemoryErasureSealUseStatus status,
        string operation,
        string auditId,
        string targetId,
        string traitId,
        int previousRevision = 0,
        int committedRevision = 0,
        long absoluteHour = 0L,
        string physicalCommitId = "",
        string detail = "") => new(
            status,
            operation,
            auditId,
            targetId,
            traitId,
            physicalCommitId,
            previousRevision,
            committedRevision,
            absoluteHour,
            detail);
}
