using System;

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
            || string.IsNullOrWhiteSpace(request.actorId)
            || string.IsNullOrWhiteSpace(request.targetCombatantId))
        {
            return new OffenseCommandExecutionResult(
                OffenseCommandOutcome.IllegalTarget,
                false,
                request?.targetCombatantId);
        }

        string skillId = request.sourceSkillId;
        bool accepted = battleRuntime.TryExecutePlannedCommand(
            request.actorId,
            request.targetCombatantId,
            skillId,
            out OffenseBattleCommandResult result);
        if (!accepted)
        {
            bool targetMissing = battleRuntime.Session?.FindCombatant(
                request.targetCombatantId) == null;
            return new OffenseCommandExecutionResult(
                targetMissing
                    ? OffenseCommandOutcome.IllegalTarget
                    : OffenseCommandOutcome.Unavailable,
                false,
                request.targetCombatantId);
        }

        return new OffenseCommandExecutionResult(
            OffenseCommandOutcome.Executed,
            result.Amount > 0f || result.Accepted,
            request.targetCombatantId);
    }

    public void FinalizeTurn()
    {
        battleRuntime.FinalizePlannedTurn();
    }
}
