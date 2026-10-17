using System;
using DungeonStory.Foundation.Facts;

public readonly struct CharacterCombatTransactionResult
{
    private CharacterCombatTransactionResult(
        bool succeeded,
        CombatAttackResult attackResult,
        string failureReason)
    {
        Succeeded = succeeded;
        AttackResult = attackResult;
        FailureReason = failureReason ?? string.Empty;
    }

    public bool Succeeded { get; }
    public CombatAttackResult AttackResult { get; }
    public string FailureReason { get; }

    public static CharacterCombatTransactionResult Success(
        in CombatAttackResult attackResult) =>
        new CharacterCombatTransactionResult(
            true,
            attackResult,
            string.Empty);

    public static CharacterCombatTransactionResult Failed(
        string failureReason,
        in CombatAttackResult attackResult = default) =>
        new CharacterCombatTransactionResult(
            false,
            attackResult,
            failureReason);
}

/// <summary>
/// Shared transaction boundary for non-command combat producers. It reserves
/// the attack and health outcomes before resolving, then commits resolver side
/// effects, body mutation and ledger rows as one rollback-capable operation.
/// </summary>
public sealed class CharacterCombatTransactionFacade
{
    private readonly ICombatResolutionService resolution;
    private readonly CombatCommandResultApplier resultApplier;

    public CharacterCombatTransactionFacade(
        ICombatResolutionService resolution,
        CombatCommandResultApplier resultApplier)
    {
        this.resolution = resolution
            ?? throw new ArgumentNullException(nameof(resolution));
        this.resultApplier = resultApplier
            ?? throw new ArgumentNullException(nameof(resultApplier));
    }

    public CharacterCombatTransactionResult ResolveAndApply(
        in CombatAttackRequest request,
        in CombatParticipantRef attacker,
        CharacterActor target,
        CombatDamageType damageType,
        string damageSource,
        CharacterCommandOrigin origin,
        long attackRevision = 0L,
        CombatOutcomeParentContext parent = default)
    {
        if (!attacker.IsValid
            || target == null
            || target.IsDead
            || !DomainFactValueSyntax.IsIdentity(request.EventId)
            || attackRevision < 0L)
        {
            return CharacterCombatTransactionResult.Failed(
                "combat-transaction-participant-or-operation-invalid");
        }
        string targetId;
        try
        {
            targetId = CharacterPersistentIdentity.Require(target).Value;
        }
        catch (InvalidOperationException)
        {
            return CharacterCombatTransactionResult.Failed(
                "combat-transaction-target-identity-missing");
        }
        if (!string.Equals(
                request.AttackerId,
                attacker.Id,
                StringComparison.Ordinal)
            || !string.Equals(
                request.DefenderId,
                targetId,
                StringComparison.Ordinal))
        {
            return CharacterCombatTransactionResult.Failed(
                "combat-transaction-request-participant-mismatch");
        }
        if (resolution is not ICombatResolutionTransactionService
            transactionalResolution)
        {
            return CharacterCombatTransactionResult.Failed(
                "combat-resolution-transaction-boundary-missing");
        }

        var targetRef = new CombatParticipantRef(target);
        if (!resultApplier.TryReserveDamageOutcome(
                targetRef,
                request.EventId,
                attackRevision,
                out ReservedCombatAction reserved,
                out _,
                out string reserveFailure,
                parent))
        {
            return CharacterCombatTransactionResult.Failed(reserveFailure);
        }

        CombatAttackResult result;
        try
        {
            result = transactionalResolution.ResolveDetached(request);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            resultApplier.CancelDamageOutcome(reserved);
            return CharacterCombatTransactionResult.Failed(
                "combat-resolution-detached-failed:"
                + exception.GetType().Name);
        }

        if (!result.Executed)
        {
            resultApplier.CancelDamageOutcome(reserved);
            if (!transactionalResolution.TryApplyResolvedResultMutation(
                    request,
                    result,
                    out string resolutionFailure))
            {
                return CharacterCombatTransactionResult.Failed(
                    resolutionFailure,
                    result);
            }
            transactionalResolution.CompleteResolvedResultMutation(request);
            return CharacterCombatTransactionResult.Failed(
                string.IsNullOrWhiteSpace(result.FailureReason)
                    ? "combat-attack-not-executed"
                    : result.FailureReason,
                result);
        }

        CombatAttackRequest detachedRequest = request;
        bool resolutionPending = true;
        var mechanical = new CombatOutcomeMechanicalMutation(
            apply: () =>
            {
                if (!transactionalResolution.TryApplyResolvedResultMutation(
                        detachedRequest,
                        result,
                        out string resolutionFailure))
                {
                    resolutionPending = false;
                    return resolutionFailure;
                }
                if (!resultApplier.TryApplyArmorDurabilityDamage(
                        result,
                        out string durabilityFailure))
                {
                    return durabilityFailure;
                }
                return string.Empty;
            },
            rollback: () =>
            {
                if (resolutionPending
                    && !transactionalResolution.TryRollbackResolvedResultMutation(
                        detachedRequest,
                        out string rollbackFailure))
                {
                    throw new InvalidOperationException(rollbackFailure);
                }
                resolutionPending = false;
            },
            complete: () =>
            {
                if (!resolutionPending)
                    return;
                transactionalResolution.CompleteResolvedResultMutation(
                    detachedRequest);
                resolutionPending = false;
            });

        CombatOutcomeApplyResult applied = resultApplier.Apply(
            targetRef,
            result,
            attacker,
            attacker.DisplayName,
            damageType,
            request.EventId,
            attackRevision,
            reserved,
            mechanical,
            CombatActionEvidenceData.Capture(request, result, damageType),
            damageSource,
            origin,
            parent);
        return applied.Succeeded
            ? CharacterCombatTransactionResult.Success(result)
            : CharacterCombatTransactionResult.Failed(
                applied.FailureReason,
                result);
    }
}
