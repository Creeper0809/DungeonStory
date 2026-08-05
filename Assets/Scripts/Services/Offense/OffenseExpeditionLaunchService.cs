using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public readonly struct OffenseExpeditionLaunchDomain
{
    public OffenseExpeditionLaunchDomain(
        OffenseWorldMapRuntime worldMap,
        IOffenseStrategicTargetService targets,
        IOffenseFieldMedicalRuntime fieldMedical,
        IOffenseBattleRuntime battleRuntime,
        IOffensePreparationService preparationService)
    {
        WorldMap = worldMap;
        Targets = targets;
        FieldMedical = fieldMedical;
        BattleRuntime = battleRuntime;
        PreparationService = preparationService;
    }

    public OffenseWorldMapRuntime WorldMap { get; }
    public IOffenseStrategicTargetService Targets { get; }
    public IOffenseFieldMedicalRuntime FieldMedical { get; }
    public IOffenseBattleRuntime BattleRuntime { get; }
    public IOffensePreparationService PreparationService { get; }
}

public readonly struct OffenseExpeditionLaunchInfrastructure
{
    public OffenseExpeditionLaunchInfrastructure(
        IGameMoneyAccount gameMoney,
        IOffenseTravelRuntime strategicTravel,
        IExpeditionDepartureService departureService,
        IGameEventBus gameEventBus)
    {
        GameMoney = gameMoney;
        StrategicTravel = strategicTravel;
        DepartureService = departureService;
        GameEventBus = gameEventBus;
    }

    public IGameMoneyAccount GameMoney { get; }
    public IOffenseTravelRuntime StrategicTravel { get; }
    public IExpeditionDepartureService DepartureService { get; }
    public IGameEventBus GameEventBus { get; }
}

public sealed class OffenseExpeditionLaunchService
{
    public bool TryStart(
        OffenseExpeditionLaunchDomain domain,
        OffenseExpeditionLaunchInfrastructure infrastructure,
        IReadOnlyList<OffenseExpeditionRun> activeExpeditions,
        string targetId,
        IEnumerable<CharacterActor> members,
        OffenseSupplyLoadout supplies,
        OffenseExpeditionPreparation preparation,
        Action<OffenseExpeditionRun> commitExpedition,
        Action notifyStateChanged,
        out OffenseExpeditionRun expedition,
        out string message)
    {
        expedition = null;
        message = string.Empty;
        OffenseTargetDefinition target = domain.WorldMap != null
            ? OffenseWorldMapService.FindKnownTarget(
                domain.WorldMap.State,
                domain.WorldMap.TargetDefinitions,
                targetId)
            : null;
        bool isStrategicSite = domain.Targets.TryCreateTarget(
            targetId,
            out OffenseTargetDefinition worldTarget,
            out OffenseHexCoord strategicDestination);
        bool isRescue = domain.Targets.TryCreateRescueTarget(
            targetId,
            out OffenseTargetDefinition rescueTarget,
            out OffenseHexCoord rescueDestination,
            out string strandedExpeditionId);
        if (isRescue)
        {
            worldTarget = rescueTarget;
            strategicDestination = rescueDestination;
            isStrategicSite = true;
        }

        if (target == null && isStrategicSite)
        {
            target = worldTarget;
        }

        if (target == null)
        {
            message = "발견되지 않은 원정 대상입니다";
            return false;
        }

        if (!isStrategicSite
            && (domain.WorldMap == null
                || !OffenseWorldMapService.CanAttemptTarget(
                    domain.WorldMap.State,
                    target,
                    out message)))
        {
            return false;
        }

        if (activeExpeditions.Any(active => active?.Target != null
            && string.Equals(active.Target.id, target.id, StringComparison.Ordinal)))
        {
            message = "이미 해당 목표로 원정대가 출발했습니다.";
            return false;
        }

        if (activeExpeditions.Count > 0 && !isRescue)
        {
            message = "한 번에 하나의 원정대만 지휘할 수 있습니다.";
            return false;
        }

        if (isRescue
            && (activeExpeditions.Count != 1
                || !domain.FieldMedical.IsStranded(strandedExpeditionId)))
        {
            message = "구조 대상인 활성 조난 원정대를 찾을 수 없습니다.";
            return false;
        }

        if (domain.BattleRuntime == null)
        {
            message = "전투 런타임이 준비되지 않았습니다.";
            return false;
        }

        List<CharacterActor> party = OffenseExpeditionService
            .GetDistinctMembers(members)
            .ToList();
        if (party.Count < target.requiredMembers)
        {
            message = $"필요 인력 부족: {party.Count}/{target.requiredMembers}";
            return false;
        }

        if (party.Count > 5)
        {
            message = $"원정대는 최대 5명까지 참가할 수 있습니다. ({party.Count}/5)";
            return false;
        }

        foreach (CharacterActor member in party)
        {
            if (!OffenseExpeditionService.CanJoinExpedition(member, out string reason))
            {
                message = $"{member.name}: {reason}";
                return false;
            }
        }

        supplies ??= new OffenseSupplyLoadout();
        preparation ??= new OffenseExpeditionPreparation();
        string expeditionId = Guid.NewGuid().ToString("N");
        if (supplies.TotalCount > 0)
        {
            if (domain.PreparationService == null)
            {
                message = "던전 보급 시스템이 준비되지 않았습니다.";
                expedition = null;
                return false;
            }

            if (!domain.PreparationService.TryCommitLoadout(
                    supplies,
                    preparation,
                    expeditionId,
                    out message))
            {
                expedition = null;
                return false;
            }
        }

        float totalPower = OffenseExpeditionService.CalculatePartyPower(party);
        expedition = new OffenseExpeditionRun(
            expeditionId,
            target,
            party,
            totalPower,
            target.durationSeconds,
            OffenseRouteGenerator.Create(target),
            supplies,
            preparation);
        if (isStrategicSite)
        {
            expedition.BeginWorldTravel(target.id);
            if (!domain.Targets.TryPrepareTravel(
                    expedition,
                    strategicDestination,
                    pauseUntilDepartureCompletes:
                        infrastructure.DepartureService != null,
                    startsSiteAttack: !isRescue,
                    out message))
            {
                domain.PreparationService?.ReturnSupplies(supplies, expeditionId);
                expedition = null;
                return false;
            }
        }

        int allocatedFieldFunds = 0;
        if (isStrategicSite && preparation.FieldFunds > 0)
        {
            allocatedFieldFunds = preparation.FieldFunds;
            if (!infrastructure.GameMoney.TrySpend(
                    allocatedFieldFunds,
                    new EconomyTransactionContext(
                        EconomyTransactionKind.ExpeditionFieldFundAllocation,
                        expeditionId,
                        target.id,
                        "원정 현장 자금 배정"),
                    out message))
            {
                domain.PreparationService?.ReturnSupplies(supplies, expeditionId);
                infrastructure.StrategicTravel?.TryRemove(expeditionId);
                expedition = null;
                return false;
            }
        }

        if (infrastructure.DepartureService != null)
        {
            OffenseExpeditionRun createdExpedition = expedition;
            if (!infrastructure.DepartureService.TryBeginDeparture(
                    createdExpedition,
                    party,
                    () => domain.PreparationService == null
                        || domain.PreparationService.IsPackageReady(expeditionId),
                    () =>
                    {
                        if (domain.PreparationService != null
                            && !domain.PreparationService.TryConsumePackedSupplies(
                                expeditionId,
                                out string packingMessage))
                        {
                            infrastructure.GameEventBus.RaiseAlert(
                                "출정 보급 오류",
                                packingMessage,
                                EventAlertImportance.High,
                                "expedition");
                            return;
                        }

                        if (isStrategicSite)
                        {
                            infrastructure.StrategicTravel?.TryResumeAfterBattle(
                                createdExpedition.ExpeditionId);
                        }

                        createdExpedition.MarkDepartureCompleted();
                        notifyStateChanged();
                    },
                    out string departureMessage))
            {
                domain.PreparationService?.ReturnSupplies(supplies, expeditionId);
                if (isStrategicSite)
                {
                    infrastructure.StrategicTravel?.TryRemove(expeditionId);
                }

                RefundFieldFunds(
                    infrastructure.GameMoney,
                    expeditionId,
                    allocatedFieldFunds);
                message = departureMessage;
                expedition = null;
                return false;
            }

            commitExpedition(expedition);
            domain.Targets.RegisterRescueDispatch(
                isRescue,
                strandedExpeditionId,
                expedition,
                party);
            OffenseSupplyPackingSnapshot packing =
                domain.PreparationService?.GetPackingSnapshot(expeditionId) ?? default;
            message = packing.IsInTransit
                ? $"{target.title} 보급 운반 중 ({packing.Delivered}/{packing.Required})"
                : $"{target.title} 출정 집결 중";
            infrastructure.GameEventBus.RaiseAlert(
                "출정 집결",
                message,
                EventAlertImportance.Medium,
                "expedition");
            notifyStateChanged();
            return true;
        }

        if (supplies.TotalCount > 0)
        {
            domain.PreparationService?.ReturnSupplies(supplies, expeditionId);
            if (isStrategicSite)
            {
                infrastructure.StrategicTravel?.TryRemove(expeditionId);
            }

            RefundFieldFunds(
                infrastructure.GameMoney,
                expeditionId,
                allocatedFieldFunds);
            message = "물리 출정 집결 서비스가 없어 보급 원정을 시작할 수 없습니다.";
            expedition = null;
            return false;
        }

        List<CharacterActor> departedMembers = new List<CharacterActor>();
        foreach (CharacterActor member in party)
        {
            if (!member.BeginExpedition())
            {
                foreach (CharacterActor departed in departedMembers)
                {
                    departed.EndExpedition(alive: true);
                }

                domain.PreparationService?.ReturnSupplies(supplies, expeditionId);
                RefundFieldFunds(
                    infrastructure.GameMoney,
                    expeditionId,
                    allocatedFieldFunds);
                message = $"{member.name}: 원정 상태로 전환할 수 없습니다.";
                expedition = null;
                return false;
            }

            departedMembers.Add(member);
        }

        commitExpedition(expedition);
        domain.Targets.RegisterRescueDispatch(
            isRescue,
            strandedExpeditionId,
            expedition,
            party);
        expedition.MarkDepartureCompleted();
        if (isStrategicSite)
        {
            infrastructure.StrategicTravel?.TryResumeAfterBattle(expeditionId);
        }

        message = $"{target.title} 원정 출발: 경로를 선택하세요.";
        infrastructure.GameEventBus.RaiseAlert(
            "원정 출발",
            message,
            EventAlertImportance.Medium,
            "오펜스");
        notifyStateChanged();
        return true;
    }

    private static void RefundFieldFunds(
        IGameMoneyAccount gameMoney,
        string expeditionId,
        int amount)
    {
        int refund = Math.Max(0, amount);
        if (refund <= 0 || gameMoney == null)
        {
            return;
        }

        gameMoney.Add(
            refund,
            new EconomyTransactionContext(
                EconomyTransactionKind.ExpeditionFieldFundReturn,
                expeditionId,
                description: "출정 취소 현장 자금 반환"));
    }
}
