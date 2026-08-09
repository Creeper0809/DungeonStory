using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IOffenseExpeditionBattleCompletionHandler
{
    void Handle(
        IOffenseStrategicExpeditionHost host,
        OffenseBattleSession session);
}

public sealed class OffenseExpeditionBattleCompletionHandler :
    IOffenseExpeditionBattleCompletionHandler
{
    private readonly IOffenseBattleRuntime battleRuntime;
    private readonly IOffenseBattleDirector strategicBattleDirector;
    private readonly IOffenseTravelRuntime strategicTravel;
    private readonly IOffenseWorldSimulation strategicWorld;
    private readonly IOffenseReturnSafetyRuntime strategicReturnSafety;
    private readonly IOffenseFieldMobilityService fieldMobility;
    private readonly IGameEventBus gameEventBus;
    private readonly ICombatEquipmentRuntime combatEquipment;

    public OffenseExpeditionBattleCompletionHandler(
        IOffenseBattleRuntime battleRuntime,
        IOffenseBattleDirector strategicBattleDirector,
        IOffenseTravelRuntime strategicTravel,
        IOffenseWorldSimulation strategicWorld,
        IOffenseReturnSafetyRuntime strategicReturnSafety,
        IOffenseFieldMobilityService fieldMobility,
        IGameEventBus gameEventBus,
        ICombatEquipmentRuntime combatEquipment)
    {
        this.battleRuntime = battleRuntime
            ?? throw new ArgumentNullException(nameof(battleRuntime));
        this.strategicBattleDirector = strategicBattleDirector
            ?? throw new ArgumentNullException(nameof(strategicBattleDirector));
        this.strategicTravel = strategicTravel
            ?? throw new ArgumentNullException(nameof(strategicTravel));
        this.strategicWorld = strategicWorld
            ?? throw new ArgumentNullException(nameof(strategicWorld));
        this.strategicReturnSafety = strategicReturnSafety
            ?? throw new ArgumentNullException(nameof(strategicReturnSafety));
        this.fieldMobility = fieldMobility
            ?? throw new ArgumentNullException(nameof(fieldMobility));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.combatEquipment = combatEquipment
            ?? throw new ArgumentNullException(nameof(combatEquipment));
    }

    public void Handle(
        IOffenseStrategicExpeditionHost host,
        OffenseBattleSession session)
    {
        if (session == null)
        {
            return;
        }

        OffenseExpeditionRun expedition =
            host.FindActiveExpedition(session.ExpeditionId);
        if (expedition == null)
        {
            return;
        }
        OffenseRouteNode completedNode = expedition.CurrentNode;
        bool victory = session.Outcome == OffenseBattleOutcome.Victory;
        if (victory)
        {
            foreach (string rewardItemId in session.EncounterRules.RewardItemIds)
            {
                expedition.AddEncounterReward(rewardItemId);
            }
            foreach (OffenseBattleCombatant defeated in session.Combatants
                         .Where(value => value.Team == OffenseBattleTeam.Enemies
                             && value.IsDead))
            {
                foreach (CombatEquipmentInstance recovered in
                         combatEquipment.ConfiscateAllFromCharacter(
                             defeated.PersistentId))
                {
                    expedition.AddRecoveredEquipment(recovered.instanceId);
                }
            }
        }
        foreach (OffenseBattleCombatant combatant in session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Allies))
        {
            if (!battleRuntime.TryGetActor(combatant.PersistentId, out CharacterActor actor))
            {
                continue;
            }

            float healthDelta = combatant.CurrentHealth - actor.CurrentHealth;
            if (healthDelta < 0f)
            {
                actor.ApplyDamage(-healthDelta, "원정 전투 피해");
            }
            else if (healthDelta > 0f)
            {
                actor.Heal(healthDelta);
            }

            bool survived = !combatant.IsDead && !actor.IsDead;
            OffenseExpeditionMemberState member = expedition.MemberStates
                .FirstOrDefault(value => value.Actor == actor);
            if (member != null)
            {
                member.Formation = combatant.Formation;
            }
            expedition.RecordBattleMemberResult(actor, combatant.TotalDamageTaken, survived);
            if (survived)
            {
                int battleExperience = victory
                    ? OffenseExpeditionRuntime.CalculateNodeExperience(
                        completedNode,
                        Mathf.Clamp(expedition.Target.campaignOrder, 1, 6))
                    : 10;
                actor.Progression?.AddExperience(battleExperience);
                CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
                    actor,
                    CharacterSkillTrigger.BattleCompleted,
                    $"{session.BattleId}:battle-completed:{combatant.PersistentId}",
                    session,
                    combatant,
                    null));
                actor.Progression?.RecordNarrative(
                    CharacterNarrativeDomain.Combat,
                    victory ? "battle-victory" : "battle-survived",
                    expedition.Target.id,
                    victory ? "won" : "survived",
                    combatant.TotalDamageTaken,
                    triggerPassives: false);
            }
            actor.Stats?.ChangesStat(CharacterCondition.SLEEP, victory ? -8f : -20f);
            actor.Stats?.ApplyMoodFactor(
                victory ? "offense:battle-victory" : "offense:battle-failure",
                victory ? "원정 교전을 이겨냄" : "원정 전투에서 무너짐",
                victory ? 2f : -10f,
                240f,
                1);
        }

        if (session.Outcome == OffenseBattleOutcome.Retreated)
        {
            expedition.Retreat(out string retreatMessage);
            battleRuntime.ClearCompletedBattle();
            strategicBattleDirector?.Clear();
            strategicTravel?.TryRemove(expedition.ExpeditionId);
            host.CompleteExpedition(expedition, success: false, retreatMessage);
            return;
        }

        if (expedition.UsesWorldTravel)
        {
            bool objectiveBattle = expedition.WorldObjectiveBattleActive;
            expedition.CompleteWorldBattle(victory);
            battleRuntime.ClearCompletedBattle();
            strategicBattleDirector?.Clear();
            if (!victory)
            {
                strategicTravel?.TryRemove(expedition.ExpeditionId);
                host.CompleteExpedition(
                    expedition,
                    success: false,
                    "원정대가 교전에서 무너졌습니다.");
                return;
            }

            if (fieldMobility.TryUpdate(
                    expedition,
                    out string mobilityMessage))
            {
                gameEventBus.RaiseAlert(
                    "원정대 조난",
                    mobilityMessage,
                    EventAlertImportance.High,
                    "오펜스");
                host.NotifyStateChanged();
                return;
            }

            strategicTravel.TryResumeAfterBattle(expedition.ExpeditionId);
            if (!objectiveBattle)
            {
                host.NotifyStateChanged();
                return;
            }

            if (!strategicWorld.TryResolveSite(expedition.WorldSiteId))
            {
                strategicWorld.TryDestroyUrgentSite(expedition.WorldSiteId);
            }

            if (!strategicTravel.TryGetState(
                    expedition.ExpeditionId,
                    out OffenseTravelStateData travel))
            {
                host.CompleteExpedition(
                    expedition,
                    success: false,
                    "원정 이동 상태를 복구하지 못했습니다.");
                return;
            }

            int granted = strategicReturnSafety.GrantForObjective(
                expedition.ExpeditionId,
                travel.CurrentCoord,
                strategicWorld.DungeonCoord);
            if (!strategicTravel.TrySetDestination(
                    expedition.ExpeditionId,
                    travel.CurrentCoord,
                    string.Empty,
                    OffenseTravelProfile.Default,
                    startsSiteAttack: false,
                    out string holdReason))
            {
                host.CompleteExpedition(
                    expedition,
                    success: false,
                    holdReason);
                return;
            }

            gameEventBus.RaiseAlert(
                "목표 파괴",
                $"{expedition.Target.title} 격파. 안전 이동 {granted}칸이 지급되었습니다. 다음 목적지를 선택하세요.",
                EventAlertImportance.High,
                "오펜스");
            host.NotifyStateChanged();
            return;
        }

        expedition.CompleteBattleNode(victory);
        battleRuntime.ClearCompletedBattle();
        if (!expedition.IsComplete)
        {
            host.NotifyStateChanged();
            gameEventBus.RaiseAlert(
                "원정 교전 승리",
                $"{expedition.CurrentNode?.Title ?? expedition.Target.title}을 돌파했습니다. 다음 경로를 선택하세요.",
                EventAlertImportance.Low,
                "오펜스");
            return;
        }

        host.CompleteExpedition(
            expedition,
            victory && expedition.Phase == OffenseExpeditionPhase.Completed,
            victory ? "목표를 쓰러뜨리고 귀환했습니다." : "원정대가 전투에서 패배했습니다.");
    }

}
