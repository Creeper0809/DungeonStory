using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using VContainer.Unity;

/// <summary>
/// Turns persisted milestone pressure IDs into deterministic recurring world
/// consequences. The pressure schedule is derived from the absolute day, so it
/// remains stable after save restore without creating another shadow state.
/// </summary>
public sealed class MilestonePressureApplicationAdapter :
    IStartable,
    IDisposable
{
    private readonly IMilestoneGameplayModifierQuery milestones;
    private readonly IInvasionCampaignRuntime invasions;
    private readonly IFactionCampaignQuery factionQuery;
    private readonly IFactionCampaignCommand factionCommands;
    private readonly IContentWorkDelayCommand workDelays;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterLifeCommand lifeCommands;
    private readonly IGameEventBus events;
    private IDisposable dayEndedSubscription;

    public MilestonePressureApplicationAdapter(
        IMilestoneGameplayModifierQuery milestones,
        IInvasionCampaignRuntime invasions,
        IFactionCampaignQuery factionQuery,
        IFactionCampaignCommand factionCommands,
        IContentWorkDelayCommand workDelays,
        ICharacterLifeQuery life,
        ICharacterLifeCommand lifeCommands,
        IGameEventBus events)
    {
        this.milestones = milestones
            ?? throw new ArgumentNullException(nameof(milestones));
        this.invasions = invasions
            ?? throw new ArgumentNullException(nameof(invasions));
        this.factionQuery = factionQuery
            ?? throw new ArgumentNullException(nameof(factionQuery));
        this.factionCommands = factionCommands
            ?? throw new ArgumentNullException(nameof(factionCommands));
        this.workDelays = workDelays
            ?? throw new ArgumentNullException(nameof(workDelays));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.lifeCommands = lifeCommands
            ?? throw new ArgumentNullException(nameof(lifeCommands));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(
            value => ApplyPressure(Math.Max(1, value.day)));
    }

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    private void ApplyPressure(int absoluteDay)
    {
        ApplyAccordObligation(absoluteDay);
        ApplyInternalPressures(absoluteDay);
        ApplyTemporalAnomaly(absoluteDay);
        ApplyInvasionPressure(absoluteDay);
    }

    private void ApplyAccordObligation(int absoluteDay)
    {
        if (!milestones.HasPressure("ending:monster-accord")
            || absoluteDay % GameCalendarRules.DaysPerSeason != 0)
        {
            return;
        }

        foreach (FactionCampaignStateSaveData faction in factionQuery.Factions
                     .Where(value => value != null)
                     .OrderBy(value => value.factionId, StringComparer.Ordinal))
        {
            factionCommands.ApplyFactionChange(
                faction.factionId,
                rapportDelta: 0,
                grievanceDelta: 0,
                obligationDelta: 1);
        }

        Publish(
            absoluteDay,
            "공동방위 의무 발생",
            "대협약 회원 세력 모두에 공동방위 의무가 1 증가했습니다.",
            "monster-accord");
    }

    private void ApplyInternalPressures(int absoluteDay)
    {
        if (absoluteDay % GameCalendarRules.DaysPerSeason == 0
            && milestones.HasPressure("ending:sealed-paradise"))
        {
            workDelays.ApplyWorkDelay("farm", 3, absoluteDay);
            Publish(
                absoluteDay,
                "폐쇄 생태 불균형",
                "폐쇄 생태계의 균형 조정으로 농업 작업이 3일간 지연됩니다.",
                "sealed-paradise");
        }

        if (absoluteDay % 40 == 0
            && milestones.HasPressure("ending:eternal-lineage"))
        {
            workDelays.ApplyWorkDelay("global", 1, absoluteDay);
            Publish(
                absoluteDay,
                "계승권 분쟁",
                "계승권 조정으로 전체 작업이 하루 동안 지연됩니다.",
                "eternal-lineage");
        }

        if (absoluteDay % 20 == 0
            && milestones.HasPressure("ending:steel-apotheosis"))
        {
            workDelays.ApplyWorkDelay("repair", 2, absoluteDay);
            Publish(
                absoluteDay,
                "자동화 연쇄 고장",
                "연쇄 고장과 내부 점검으로 수리 작업이 2일간 지연됩니다.",
                "steel-apotheosis");
        }
    }

    private void ApplyTemporalAnomaly(int absoluteDay)
    {
        if (absoluteDay % GameCalendarRules.DaysPerSeason != 0
            || !milestones.HasPressure("ending:timeless-sanctuary"))
        {
            return;
        }

        int acceleratedMaintenanceDay = absoluteDay + 3;
        int changed = 0;
        foreach (CharacterLifeRecord record in life.Records
                     .Where(value => value != null
                         && value.RequestedAgingCareMode
                             == AgingCareMode.TemporalStasis)
                     .OrderBy(value => value.CharacterId.Value,
                         StringComparer.Ordinal))
        {
            if (record.TemporalStasisNextMaintenanceAbsoluteDay
                <= acceleratedMaintenanceDay)
            {
                continue;
            }

            lifeCommands.ConfigureTemporalStasis(
                record.CharacterId,
                record.TemporalStasisFacilityId,
                record.EffectiveAgingCareMode == AgingCareMode.TemporalStasis,
                acceleratedMaintenanceDay);
            changed++;
        }

        if (changed > 0)
        {
            Publish(
                absoluteDay,
                "시간 이상 현상",
                $"시간 고정 대상 {changed}명의 다음 촉매 교체일이 3일 뒤로 앞당겨졌습니다.",
                "timeless-sanctuary");
        }
    }

    private void ApplyInvasionPressure(int absoluteDay)
    {
        float threat = 0f;
        string source = string.Empty;
        string title = string.Empty;
        if (milestones.HasPressure("ending:truth-revealed")
            && absoluteDay % 20 == 0)
        {
            threat = 75f;
            source = "truth-revealed";
            title = "진실 수호자 보복";
        }
        if (milestones.HasPressure("ending:surface-hegemony")
            && absoluteDay % 15 == 0
            && threat < 90f)
        {
            threat = 90f;
            source = "surface-hegemony";
            title = "인간 연합 반격";
        }
        if (milestones.HasPressure("ending:dungeon-sovereignty")
            && absoluteDay % 10 == 0
            && threat < 100f)
        {
            threat = 100f;
            source = "dungeon-sovereignty";
            title = "국가 규모 공성전";
        }
        if (milestones.HasPressure("ending:arcane-ascension")
            && absoluteDay % 20 == 0
            && threat < 95f)
        {
            threat = 95f;
            source = "arcane-ascension";
            title = "비전 습격";
        }

        if (threat <= 0f
            || invasions.Operations.Any(value => value != null
                && value.scheduledDay == absoluteDay))
        {
            return;
        }

        ScheduledInvasionOperationState operation =
            invasions.ScheduleNextOperation(threat);
        if (operation != null)
        {
            Publish(
                absoluteDay,
                title,
                $"위협도 {threat:0}의 침입 작전 {operation.operationId}이 예약되었습니다.",
                source);
        }
    }

    private void Publish(
        int absoluteDay,
        string title,
        string detail,
        string source)
    {
        events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            title,
            detail,
            EventAlertImportance.High,
            "V21 이정표 압력",
            sourceId: $"milestone-pressure:{source}:{absoluteDay}")));
    }
}
