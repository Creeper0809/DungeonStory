using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public readonly struct OffenseExpeditionDecisionDomain
{
    public OffenseExpeditionDecisionDomain(
        IOffenseDecisionRuntime decisions,
        IOffenseTravelRuntime travel,
        IOffenseDecisionEffectExecutor effects,
        IOffenseFieldMobilityService fieldMobility)
    {
        Decisions = decisions;
        Travel = travel;
        Effects = effects;
        FieldMobility = fieldMobility;
    }

    public IOffenseDecisionRuntime Decisions { get; }
    public IOffenseTravelRuntime Travel { get; }
    public IOffenseDecisionEffectExecutor Effects { get; }
    public IOffenseFieldMobilityService FieldMobility { get; }
}

public readonly struct OffenseExpeditionDecisionEffects
{
    public OffenseExpeditionDecisionEffects(
        IOffenseWorldSimulation world,
        IGameMoneyAccount gameMoney,
        ICombatEquipmentRuntime equipment,
        IOffenseReturnSafetyRuntime returnSafety,
        IOffenseStrategicBattleLauncher battleLauncher)
    {
        World = world;
        GameMoney = gameMoney;
        Equipment = equipment;
        ReturnSafety = returnSafety;
        BattleLauncher = battleLauncher;
    }

    public IOffenseWorldSimulation World { get; }
    public IGameMoneyAccount GameMoney { get; }
    public ICombatEquipmentRuntime Equipment { get; }
    public IOffenseReturnSafetyRuntime ReturnSafety { get; }
    public IOffenseStrategicBattleLauncher BattleLauncher { get; }
}

public sealed class OffenseExpeditionDecisionService
{
    public bool TryResolve(
        OffenseExpeditionRun expedition,
        string choiceId,
        OffenseExpeditionDecisionDomain domain,
        OffenseExpeditionDecisionEffects effectServices,
        Action notifyStateChanged,
        out string message)
    {
        if (expedition == null
            || !expedition.UsesWorldTravel
            || domain.Decisions == null
            || domain.Travel == null)
        {
            message = "해결할 원정 사건이 없습니다.";
            return false;
        }

        string expeditionId = expedition.ExpeditionId;
        if (!domain.Decisions.TryGetActiveChoice(
                expeditionId,
                choiceId,
                out OffenseDecisionChoiceDefinition choice,
                out int deterministicRoll,
                out message))
        {
            return false;
        }

        OffenseDecisionEffectContext effectContext =
            new OffenseDecisionEffectContext(
                expedition,
                domain.Travel,
                effectServices.World,
                effectServices.GameMoney,
                deterministicRoll,
                effectServices.Equipment);
        IReadOnlyList<OffenseDecisionEffectDefinition> effects =
            choice.effects != null
                ? choice.effects
                : Array.Empty<OffenseDecisionEffectDefinition>();
        if (!domain.Effects.CanExecute(effects, effectContext, out message))
        {
            return false;
        }

        domain.Effects.Execute(effects, effectContext);
        if (!domain.Decisions.TryResolve(
                expeditionId,
                choiceId,
                out _,
                out message))
        {
            throw new InvalidOperationException(
                $"Decision state changed while resolving '{expeditionId}:{choiceId}'.");
        }

        domain.Travel.TryResumeAfterDecision(expeditionId);
        expedition.SetDecisionPaused(false);
        if (domain.FieldMobility.TryUpdate(expedition, out string mobilityMessage))
        {
            message = mobilityMessage;
            notifyStateChanged();
            return true;
        }

        if (effectContext.ForcesMovement)
        {
            domain.Travel.TryAdvanceOneStep(
                expeditionId,
                forcedMovement: true,
                out _,
                out _);
        }

        if (effectContext.StartsCombat
            && effectServices.ReturnSafety.CanGenerateForcedCombat(
                expeditionId,
                averageHealthRatio:
                    OffenseStrategicTravelEventHandler.GetAverageHealthRatio(expedition),
                hasDownedMember: expedition.MemberStates.Any(member =>
                    member?.Actor != null
                    && member.Actor.Lifecycle?.CurrentState
                        == CharacterLifecycleState.Downed),
                hasUsableWeaponForEveryActiveMember: true))
        {
            if (!effectServices.BattleLauncher.TryBegin(
                    expedition,
                    objectiveBattle: false,
                    out message))
            {
                domain.Travel.TryResumeAfterBattle(expeditionId);
                return false;
            }
        }

        string summary = effectContext.Results.Count > 0
            ? string.Join(" · ", effectContext.Results)
            : choice.directionLabel;
        message = string.IsNullOrWhiteSpace(summary)
            ? "선택 결과가 원정에 반영되었습니다."
            : summary;
        notifyStateChanged();
        return true;
    }
}
