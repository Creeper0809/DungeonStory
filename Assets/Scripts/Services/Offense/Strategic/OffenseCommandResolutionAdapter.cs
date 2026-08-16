using System;
using System.Linq;

public sealed class OffenseCommandResolutionAdapter :
    IOffenseCommandResolutionAdapter
{
    private readonly IOffenseBattleRuntime battleRuntime;

    public OffenseCommandResolutionAdapter(IOffenseBattleRuntime battleRuntime)
    {
        this.battleRuntime = battleRuntime
            ?? throw new ArgumentNullException(nameof(battleRuntime));
    }

    public OffenseCommandExecutionResult Execute(
        OffenseCommandExecutionRequest request)
    {
        if (request == null
            || request.survivingExecutionStages <= 0
            || string.IsNullOrWhiteSpace(request.actorId))
        {
            return new OffenseCommandExecutionResult(
                OffenseCommandOutcome.IllegalTarget,
                false,
                request?.targetCombatantId,
                request == null
                    ? "Strategic command execution request is missing."
                    : request.survivingExecutionStages <= 0
                        ? "Strategic command has no surviving execution stage."
                        : "Strategic command actor is missing.");
        }

        string skillId = request.sourceSkillId;
        string executionTargetId = ResolveExecutionTarget(
            request,
            out string targetFailure);
        if (string.IsNullOrWhiteSpace(executionTargetId))
        {
            return new OffenseCommandExecutionResult(
                OffenseCommandOutcome.IllegalTarget,
                false,
                request.targetCombatantId,
                targetFailure);
        }
        bool accepted = battleRuntime.TryExecutePlannedCommand(
            request.directorTurn,
            request.actorId,
            executionTargetId,
            request.actionType,
            skillId,
            out OffenseBattleCommandResult result);
        if (!accepted)
        {
            bool targetMissing = battleRuntime.Session?.FindCombatant(
                executionTargetId) == null;
            return new OffenseCommandExecutionResult(
                targetMissing
                    ? OffenseCommandOutcome.IllegalTarget
                    : OffenseCommandOutcome.Unavailable,
                false,
                executionTargetId,
                result?.Message);
        }

        return new OffenseCommandExecutionResult(
            OffenseCommandOutcome.Executed,
            result.Amount > 0f || result.Accepted,
            executionTargetId,
            result.Message);
    }

    private string ResolveExecutionTarget(
        OffenseCommandExecutionRequest request,
        out string failure)
    {
        failure = string.Empty;
        OffenseBattleSession session = battleRuntime.Session;
        OffenseBattleCombatant actor = session?.FindCombatant(request.actorId);
        if (actor == null)
        {
            failure = "Strategic command actor is unavailable.";
            return string.Empty;
        }

        if (request.actionType is OffenseBattleActionType.Advance
            or OffenseBattleActionType.Guard
            or OffenseBattleActionType.Reload
            or OffenseBattleActionType.SwitchWeapon
            or OffenseBattleActionType.SetFireMode
            or OffenseBattleActionType.DeployCover)
        {
            return actor.PersistentId;
        }

        if (request.actionType == OffenseBattleActionType.BasicAttack)
        {
            if (string.IsNullOrWhiteSpace(request.targetCombatantId))
            {
                failure = "Strategic BasicAttack target is missing.";
                return string.Empty;
            }
            return request.targetCombatantId;
        }

        if (request.actionType != OffenseBattleActionType.Ability)
        {
            failure = $"Strategic action '{request.actionType}' is unsupported.";
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(request.sourceSkillId))
        {
            failure = "Strategic Ability source ID is missing.";
            return string.Empty;
        }

        CharacterCombatAbilityDefinition ability = actor?.Abilities?
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.Id,
                    request.sourceSkillId,
                    StringComparison.Ordinal));
        if (actor == null || ability == null)
        {
            failure = "Strategic command source ability is unavailable.";
            return string.Empty;
        }

        if (ability.TargetRule == OffenseBattleTargetRule.Self)
        {
            return actor.PersistentId;
        }
        if (ability.TargetRule == OffenseBattleTargetRule.Enemy)
        {
            return request.targetCombatantId;
        }

        OffenseBattleCombatant ally = session.Combatants
            .Where(candidate => candidate != null
                && candidate.Team == actor.Team
                && !candidate.IsDead
                && (ability.TargetPositions
                    & OffenseFormationUtility.ToMask(candidate.Formation)) != 0)
            .OrderBy(candidate => candidate.CurrentHealth
                / Math.Max(1f, candidate.Stats.MaxHealth))
            .ThenBy(candidate => candidate.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ally != null)
        {
            return ally.PersistentId;
        }

        failure = "Strategic command has no living ally in an allowed target position.";
        return string.Empty;
    }

    public OffenseTurnFinalizationResult FinalizeTurn(int directorTurn)
    {
        bool succeeded = battleRuntime.FinalizePlannedTurn(
            directorTurn,
            out string failureReason);
        return new OffenseTurnFinalizationResult(succeeded, failureReason);
    }
}
