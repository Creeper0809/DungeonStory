using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

// Query-only consumer: choosing an alert still routes through the existing command dispatcher.
public sealed class CampaignPresentationObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly IV20MilestoneWorldSnapshotQuery milestoneProjector;
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly ISeasonalEventQuery seasonalEvents;
    private readonly ISocietyEventQuery societyEvents;
    private readonly IFactionCampaignQuery factionCampaigns;
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly IReproductionService reproduction;
    private readonly ICharacterLifeQuery life;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IKinshipQuery kinship;
    private readonly IGriefTraumaService grief;
    private readonly IPopulationHealthQuery populationHealth;
    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly List<IDisposable> subscriptions = new();

    public CampaignPresentationObserver(IGameEventBus events,
        IV20MilestoneWorldSnapshotQuery milestoneProjector,
        ICharacterNarrativeQuery narratives,
        ICharacterNarrativeCatalog narrativeCatalog,
        ISeasonalEventQuery seasonalEvents,
        ISocietyEventQuery societyEvents,
        IFactionCampaignQuery factionCampaigns,
        V20StoryContentCatalog storyCatalog,
        IReproductionService reproduction,
        ICharacterLifeQuery life,
        IFacilityCapabilityQuery facilities,
        IKinshipQuery kinship,
        IGriefTraumaService grief,
        IPopulationHealthQuery populationHealth,
        IDiseaseDefinitionCatalog diseases)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.milestoneProjector = milestoneProjector ?? throw new ArgumentNullException(nameof(milestoneProjector));
        this.narratives = narratives ?? throw new ArgumentNullException(nameof(narratives));
        this.narrativeCatalog = narrativeCatalog ?? throw new ArgumentNullException(nameof(narrativeCatalog));
        this.seasonalEvents = seasonalEvents ?? throw new ArgumentNullException(nameof(seasonalEvents));
        this.societyEvents = societyEvents ?? throw new ArgumentNullException(nameof(societyEvents));
        this.factionCampaigns = factionCampaigns ?? throw new ArgumentNullException(nameof(factionCampaigns));
        this.storyCatalog = storyCatalog ?? throw new ArgumentNullException(nameof(storyCatalog));
        this.reproduction = reproduction ?? throw new ArgumentNullException(nameof(reproduction));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
        this.populationHealth = populationHealth ?? throw new ArgumentNullException(nameof(populationHealth));
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
    }

    public void Initialize()
    {
        if (subscriptions.Count != 0) return;
        try
        {
            subscriptions.Add(events.Subscribe<CampaignIncidentCapturedFact>(value =>
                TryObserve(() => PublishActionableAlerts(value.AbsoluteDay))));
            subscriptions.Add(events.Subscribe<CampaignDailyEvaluatedFact>(value =>
                TryObserve(() => PublishActionableAlerts(value.AbsoluteDay))));
            subscriptions.Add(events.Subscribe<EndlessCrisisEvaluatedFact>(value =>
                TryObserve(() => PublishEndlessCrisisAlert(value.AbsoluteDay, value.Crisis))));
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in subscriptions) subscription.Dispose();
        subscriptions.Clear();
    }

    private void Publish(EventAlertRequestedEvent notice) =>
        TryObserve(() => events.Publish(notice));

    private static void TryObserve(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        { Debug.LogError("campaign-presentation-observer:" + exception.GetType().Name + ":" + exception.Message); }
    }

    private void PublishEndlessCrisisAlert(
        int absoluteDay,
        EndlessCrisisSnapshot crisis)
    {
        if (crisis.InstanceId.Length == 0)
        {
            return;
        }

        string axes = string.Join(
            ", ",
            crisis.Axes.Select(value =>
                $"{value.DisplayName} {value.Multiplier * 100f:0}%"));
        string detail;
        bool resolved = false;
        string result = string.Empty;
        switch (crisis.Phase)
        {
            case EndlessCrisisLifecyclePhase.Active:
                string response = crisis.DirectResponseOwnerId.Length > 0
                    ? "\n이 위기에서 시작된 침입 대응이 진행 중입니다."
                    : crisis.PendingInvasionSourceOwnerId.Length > 0
                        ? "\n이 위기의 침입 후보가 대기 중입니다."
                        : string.Empty;
                detail = $"활성 압력: {axes}\n"
                    + $"압력 종료까지 {Math.Max(0, crisis.PressureEndAbsoluteDay - absoluteDay)}일."
                    + response;
                break;
            case EndlessCrisisLifecyclePhase.AwaitingDirectResponse:
                detail = $"압력 종료: {axes}\n"
                    + "이 위기에서 시작된 침입 대응이 끝나야 수복 기간이 시작됩니다. "
                    + "발생한 피해는 자동 복구되지 않습니다.";
                break;
            case EndlessCrisisLifecyclePhase.Recovery:
                detail = $"수복 중: {axes}\n"
                    + $"남은 수복 창 {Math.Max(0, crisis.RecoveryEndAbsoluteDay - absoluteDay)}일. "
                    + $"{crisis.RecoveryEndAbsoluteDay}일 이후 정기 평가에서 다음 위기가 가능해집니다. "
                    + "남은 피해·재고·관계는 직접 수복해야 합니다.";
                break;
            case EndlessCrisisLifecyclePhase.None:
                resolved = true;
                detail = $"위기와 수복 창이 종료되었습니다: {axes}";
                result = $"압력 종료일: {crisis.PressureEndAbsoluteDay}\n"
                    + $"마지막 실제 종료일: {crisis.LastActualEndAbsoluteDay}\n"
                    + $"수복 종료일: {crisis.RecoveryEndAbsoluteDay}\n"
                    + "발생한 피해와 소모는 유지됩니다.";
                break;
            default:
                throw new InvalidOperationException(
                    "Endless crisis alert received an unsupported phase.");
        }

        Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            "엔드리스 위기",
            detail,
            EventAlertImportance.High,
            "V20 엔드리스 위기",
            Array.Empty<EventAlertChoice>(),
            crisis.InstanceId,
            isResolved: resolved,
            resultSummary: result)));
    }

    private void PublishActionableAlerts(int absoluteDay)
    {
        PublishSeasonalContractAlerts();
        foreach (V20ActiveEventSaveData active in societyEvents.ActiveSocietyEvents
                     .Where(value => value != null
                         && !value.resolved
                         && (value.guestDelivery?.phase
                                 ?? GuestRequestDeliveryPhase.None)
                             == GuestRequestDeliveryPhase.None)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)storyCatalog).Require(active.definitionId);
            bool retirement = HasRetirementSchedule(definition);
            bool responseOwned = (active.observedIncidentResponse?.phase
                    ?? ObservedIncidentResponsePhase.None)
                != ObservedIncidentResponsePhase.None;
            EventAlertChoice[] choices = responseOwned
                ? Array.Empty<EventAlertChoice>()
                : SocietyChoices(
                        definition,
                        active.guestDelivery)
                    .Select(choice => new EventAlertChoice(
                        choice.title,
                        SocietyChoiceOutcome(definition, choice, retirement),
                        V21ContentAlertActionIds.Society(
                            active.instanceId,
                            choice.choiceId)))
                    .ToArray();
            string participant = ParticipantNames(active);
            string guestRequirements = definition is GuestRequestDefinitionSO guest
                ? FormatGuestFulfillRequirements(guest.serviceRequirements)
                : string.Empty;
            string delivery = FormatGuestDeliveryState(active.guestDelivery);
            string incidentResponse = FormatObservedIncidentResponseState(
                active.observedIncidentResponse);
            string risk = V20SocietyEventAlertProjection.FormatRisk(
                active.riskTier,
                active.riskReason);
            string introduction = active.hasObservedMealIncident
                ? $"확인된 발생 사실: "
                    + active.observedMealIncident.observedCause
                : definition.Description;
            string displayName = active.hasObservedMealIncident
                && active.observedMealIncident.incidentKind
                    == ServiceIncidentKind.Contamination
                    ? "오염 식사 섭취"
                    : definition.DisplayName;
            Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                displayName,
                $"{introduction}{participant}{guestRequirements}{delivery}"
                    + $"{incidentResponse}\n{risk}\n기한: {active.deadlineAbsoluteDay}일",
                V20SocietyEventAlertProjection.ToAlertImportance(
                    active.riskTier),
                "V21 사회 사건",
                choices,
                active.instanceId)));
        }

        foreach (FactionCampaignStateSaveData faction in factionCampaigns.Factions
                     .Where(value => value != null
                         && value.currentChapter >= 1
                         && value.currentChapter <= 6)
                     .OrderBy(value => value.factionId, StringComparer.Ordinal))
        {
            FactionChapterDefinitionSO chapter = storyCatalog.Chapters.Single(value =>
                string.Equals(value.factionId, faction.factionId, StringComparison.Ordinal)
                && value.chapterNumber == faction.currentChapter);
            EventAlertChoice[] choices = chapter.choices.Select(choice =>
                new EventAlertChoice(
                    choice.title,
                    choice.outcomeText,
                    V21ContentAlertActionIds.FactionChapter(
                        faction.factionId,
                        choice.choiceId))).ToArray();
            Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                chapter.DisplayName,
                chapter.Description,
                EventAlertImportance.High,
                "V21 세력 장",
                choices,
                $"{chapter.StableId}:{faction.currentChapter}")));
        }

        foreach (ReproductionProcess process in reproduction.Processes
                     .Where(value => value != null
                         && value.Status == ReproductionProcessStatus.Planned)
                     .OrderBy(value => value.ProcessId, StringComparer.Ordinal))
        {
            string parents = process.SecondParentId.IsValid
                ? $"{process.FirstParentId.Value} + {process.SecondParentId.Value}"
                : process.FirstParentId.Value;
            Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "번식 계획 승인",
                $"{parents}\n방식: {process.Mode}\n지원 시설: {process.SupportFacilityInstanceId}",
                EventAlertImportance.High,
                "V21 번식",
                BuildReproductionStartChoices(process),
                process.ProcessId)));
        }

        foreach (CharacterLifeRecord patient in life.Records
                     .Where(value => value != null
                         && (value.LifeStage == CharacterLifeStage.Elder
                             || value.AgeConditions.Count > 0))
                     .OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal))
        {
            List<EventAlertChoice> restorative = new();
            AddAgeTreatmentChoice(
                restorative,
                patient.CharacterId,
                AgeTreatmentKind.OrganRegeneration,
                "장기 재생",
                "building:8868");
            AddAgeTreatmentChoice(
                restorative,
                patient.CharacterId,
                AgeTreatmentKind.BloodRejuvenation,
                "혈액 회춘",
                "building:8869");
            AddAgeTreatmentChoice(
                restorative,
                patient.CharacterId,
                AgeTreatmentKind.RuneHibernation,
                "룬 동면",
                "building:8870");
            AddAgeTreatmentChoice(
                restorative,
                patient.CharacterId,
                AgeTreatmentKind.WholeBodyRegeneration,
                "전신 재생",
                "building:8871");
            if (restorative.Count > 0)
            {
                Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    "노화 치료 계획",
                    $"환자: {patient.CharacterId.Value}\n활성 노화 질환: {patient.AgeConditions.Count}",
                    EventAlertImportance.Medium,
                    "V21 의료",
                    restorative,
                    $"age-treatment:{patient.CharacterId.Value}")));
            }

            List<EventAlertChoice> stasis = new();
            AddAgeTreatmentChoice(
                stasis,
                patient.CharacterId,
                AgeTreatmentKind.TemporalStasis,
                "시간 고정",
                "building:8872");
            if (stasis.Count > 0)
            {
                Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    "시간 고정 치료",
                    $"환자: {patient.CharacterId.Value}\n지속 동력과 촉매가 필요한 장기 유지 치료입니다.",
                    EventAlertImportance.Medium,
                    "V21 의료",
                    stasis,
                    $"temporal-stasis:{patient.CharacterId.Value}")));
            }
        }

        CharacterId[] livingIds = milestoneProjector.LivingCharacters
            .Select(CharacterPersistentIdentity.Require)
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        bool traitAnalyzerOperational = facilities.FindOperational(
            FacilityCapabilityKind.Medical,
            "building:8879").Count > 0;
        if (traitAnalyzerOperational)
        {
            foreach (CharacterId subjectId in livingIds)
            {
                if (!narratives.TryGet(
                        subjectId,
                        out CharacterNarrativeSnapshot narrative)
                    || narrative.HeritableTraitsAnalyzed
                    || narrative.LatentHeritableTraitIds.Count == 0)
                {
                    continue;
                }
                Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    "잠재 형질 분석 가능",
                    $"대상: {subjectId.Value}\n형질 분석기와 검사 키트 1개가 필요합니다.",
                    EventAlertImportance.Medium,
                    "V21 유전",
                    new[]
                    {
                        new EventAlertChoice(
                            "형질 검사",
                            "검사 키트 1개를 사용해 잠재 유전 특성을 공개합니다.",
                            V21ContentAlertActionIds.TraitAnalysis(subjectId))
                    },
                    $"trait-analysis:{subjectId.Value}")));
            }
        }
        PublishCulturalPracticeAlerts(livingIds, absoluteDay);
        foreach (CharacterTombstoneSaveData tombstone in kinship.Tombstones
                     .Where(value => value != null
                         && absoluteDay >= value.deathAbsoluteDay
                         && absoluteDay - value.deathAbsoluteDay <= 7)
                     .OrderBy(value => value.deathAbsoluteDay)
                     .ThenBy(value => value.characterId, StringComparer.Ordinal))
        {
            CharacterId deceasedId = new(tombstone.characterId);
            if (!livingIds.Any(value => grief.TryGet(
                    value,
                    out CharacterGriefAggregate state)
                && state.NeedsFuneral(deceasedId)))
            {
                continue;
            }
            Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "장례 준비",
                $"사망자: {deceasedId.Value}\n사망일: {tombstone.deathAbsoluteDay}일\n7일 안에 장례를 치르면 남은 슬픔을 줄입니다.",
                EventAlertImportance.High,
                "V21 장례",
                new[]
                {
                    new EventAlertChoice(
                        "장례 개최",
                        "문화에 맞는 추모 시설과 장례 준비품을 사용합니다.",
                        V21ContentAlertActionIds.Funeral(deceasedId))
                },
                $"funeral:{deceasedId.Value}")));
        }

        bool counselingOperational = facilities.FindOperational(
            FacilityCapabilityKind.Medical,
            "building:8885").Count > 0;
        if (counselingOperational)
        {
            foreach (CharacterId patientId in livingIds)
            {
                if (!grief.TryGet(patientId, out CharacterGriefAggregate state)
                    || state.Trauma <= 0f)
                {
                    continue;
                }
                Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    "상담 치료 가능",
                    $"대상: {patientId.Value}\n현재 트라우마: {state.Trauma:0.#}",
                    EventAlertImportance.Medium,
                    "V21 상담",
                    new[]
                    {
                        new EventAlertChoice(
                            "상담 시작",
                            "상담실과 트라우마 치료 꾸러미를 사용합니다.",
                            V21ContentAlertActionIds.Counseling(patientId))
                    },
                    $"counsel:{patientId.Value}")));
            }
        }

        PublishDiseaseResponseAlerts(livingIds, absoluteDay);
    }

    private static string FormatObservedIncidentResponseState(
        ObservedIncidentResponseSaveData response)
    {
        if (response == null
            || response.phase == ObservedIncidentResponsePhase.None)
        {
            return string.Empty;
        }
        string state = response.phase switch
        {
            ObservedIncidentResponsePhase.Accepted => "현장 명령 수락 대기",
            ObservedIncidentResponsePhase.ExternalOperationStarted =>
                "실제 현장 작업 진행 중",
            ObservedIncidentResponsePhase.ReceiptCommitted =>
                "실제 완료 receipt 확인됨",
            ObservedIncidentResponsePhase.Failed => "현장 작업 실패",
            ObservedIncidentResponsePhase.Cancelled => "현장 작업 취소",
            ObservedIncidentResponsePhase.EffectsPublished => "결과 반영 완료",
            _ => throw new ArgumentOutOfRangeException(nameof(response.phase))
        };
        string failure = string.IsNullOrEmpty(response.failureReason)
            ? string.Empty
            : $"\n실패 근거: {response.failureReason}";
        return $"\n대응: {response.choiceId}\n대응 상태: {state}{failure}";
    }

    private void PublishSeasonalContractAlerts()
    {
        foreach (V20ActiveEventSaveData occurrence in seasonalEvents
                     .ActiveSeasonalEvents
                     .Where(value => value != null && !value.resolved)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            FactionContractDefinitionSO contract = storyCatalog.Contracts
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.seasonalEventId,
                        occurrence.definitionId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.factionId,
                        occurrence.contextFactionId,
                        StringComparison.Ordinal));
            if (contract == null
                || !factionCampaigns.TryGetFaction(
                    occurrence.contextFactionId,
                    out FactionCampaignStateSaveData faction)
                || string.Equals(
                        faction.lastSeasonalContractId,
                        contract.StableId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        faction.lastSeasonalOccurrenceId,
                        occurrence.instanceId,
                        StringComparison.Ordinal))
            {
                continue;
            }

            string requirements = string.Join(
                ", ",
                FactionContractMaterialRules.BuildMaterialRequirements(contract)
                    .Select(value => $"{value.Key} {value.Value}개"));
            Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                contract.DisplayName,
                $"{contract.Description}\n납품: {requirements}\n기한: {occurrence.deadlineAbsoluteDay}일",
                EventAlertImportance.High,
                "V21 계절 세력 요청",
                new[]
                {
                    new EventAlertChoice(
                        "실제 납품 계약을 수락한다",
                        "기한 안에 물자를 실제 인도하면 우호와 의무 토큰을 얻고, 수락 후 실패하면 불만이 오른다.",
                        V21ContentAlertActionIds
                            .SeasonalFactionContractAccept(
                                occurrence.instanceId,
                                contract.StableId))
                },
                occurrence.instanceId)));
        }
    }

    private string ParticipantNames(V20ActiveEventSaveData active)
    {
        string[] participantIds = (active?.participantCharacterIds
                ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (participantIds.Length == 0)
        {
            return string.Empty;
        }

        string names = string.Join(
            ", ",
            participantIds.Select(participantId =>
            {
                CharacterActor actor = milestoneProjector.LivingCharacters
                    .FirstOrDefault(value => value?.Identity != null
                        && string.Equals(
                            value.Identity.PersistentId,
                            participantId,
                            StringComparison.Ordinal));
                return string.IsNullOrWhiteSpace(actor?.Identity?.DisplayName)
                    ? participantId
                    : actor.Identity.DisplayName;
            }));
        return "\n대상: " + names;
    }

    private static bool HasRetirementSchedule(V20AuthoredContentSO definition) =>
        SocietyChoices(definition, null)
            .Where(value => value != null)
            .SelectMany(value => value.effects ?? new List<V20ContentEffect>())
            .Any(value => value != null
                && value.kind == V20ContentEffectKind.RetirementSchedule);

    private static string RetirementChoiceOutcome(V20ChoiceDefinition choice)
    {
        V20ContentEffect schedule = (choice?.effects
                ?? new List<V20ContentEffect>())
            .Single(value => value != null
                && value.kind == V20ContentEffectKind.RetirementSchedule);
        string timing = schedule.durationDays == 0
            ? "선택 당일 은퇴"
            : $"선택 후 {schedule.durationDays}일에 은퇴";
        return $"{choice.outcomeText}\n은퇴 시점: {timing}";
    }

    private static string SocietyChoiceOutcome(
        V20AuthoredContentSO definition,
        V20ChoiceDefinition choice,
        bool retirement)
    {
        string outcome = retirement
            ? RetirementChoiceOutcome(choice)
            : choice?.outcomeText ?? string.Empty;
        V20ContentRequirementSet requirements = definition is GuestRequestDefinitionSO guest
            && string.Equals(choice?.choiceId, "fulfill", StringComparison.Ordinal)
            ? guest.serviceRequirements
            : choice?.requirements;
        string items = FormatItemRequirements(requirements);
        if (string.IsNullOrWhiteSpace(items))
        {
            return outcome;
        }

        string label = definition is GuestRequestDefinitionSO
            ? "이행 시 요구 물품"
            : "요구 물품";
        return $"{outcome}\n{label}: {items}";
    }

    private static string FormatGuestFulfillRequirements(
        V20ContentRequirementSet requirements)
    {
        string items = FormatItemRequirements(requirements);
        return string.IsNullOrWhiteSpace(items)
            ? string.Empty
            : "\n요청 이행 시 요구 물품: " + items;
    }

    private static string FormatItemRequirements(
        V20ContentRequirementSet requirements) => string.Join(
        ", ",
        (requirements?.items ?? new List<V20ItemAmountRequirement>())
        .Where(value => value != null
            && !string.IsNullOrWhiteSpace(value.itemDefinitionId)
            && value.amount > 0)
        .OrderBy(value => value.itemDefinitionId, StringComparer.Ordinal)
        .ThenBy(value => value.amount)
        .ThenBy(value => value.consume)
        .Select(value => $"{value.itemDefinitionId} x{value.amount} "
            + (value.consume ? "(소비)" : "(보유 필요)")));

    private void PublishCulturalPracticeAlerts(
        IReadOnlyList<CharacterId> livingIds, int absoluteDay)
    {
        absoluteDay = Math.Max(1, absoluteDay);
        foreach (CulturalPracticeDefinitionSO practice in
                 narrativeCatalog.Practices
                     .Where(value => value != null
                         && (absoluteDay - 1) % 10
                             == (int)(PersistentEntityId.GetStableHash32(
                                 value.StableId) % 10u))
                     .OrderBy(value => value.StableId, StringComparer.Ordinal))
        {
            CharacterId[] members = livingIds
                .Where(id => narratives.TryGet(
                        id,
                        out CharacterNarrativeSnapshot narrative)
                    && string.Equals(
                        narrative.CultureId.Value,
                        practice.cultureId,
                        StringComparison.Ordinal)
                    && narratives.CanPerformPractice(
                        id,
                        practice.StableId,
                        absoluteDay,
                        out _))
                .Take(4)
                .ToArray();
            if (members.Length == 0)
            {
                continue;
            }

            List<EventAlertChoice> choices = new()
            {
                new EventAlertChoice(
                    "관습 수행",
                    "해당 문화 구성원이 준비물을 사용해 관습을 수행합니다.",
                    V21ContentAlertActionIds.CulturalPractice(
                        practice.StableId,
                        members))
            };
            choices.Add(new EventAlertChoice(
                "이번에는 생략",
                "준비를 포기하고 이 관습을 지키지 못한 결과를 적용합니다.",
                V21ContentAlertActionIds.CulturalPracticeNeglect(
                    practice.StableId,
                    members)));
            CharacterId guest = livingIds.FirstOrDefault(id =>
                !members.Contains(id)
                && narratives.TryGet(
                    id,
                    out CharacterNarrativeSnapshot narrative)
                && !string.Equals(
                    narrative.CultureId.Value,
                    practice.cultureId,
                    StringComparison.Ordinal)
                && narratives.CanPerformPractice(
                    id,
                    practice.StableId,
                    absoluteDay,
                    out _));
            if (guest.IsValid)
            {
                choices.Add(new EventAlertChoice(
                    "타문화 주민과 함께 수행",
                    "타문화 주민 한 명이 실제로 참여하며 해당 문화의 동화 일수를 얻습니다.",
                    V21ContentAlertActionIds.CulturalPractice(
                        practice.StableId,
                        members.Take(3).Append(guest))));
            }

            string requirements = string.Join(
                ", ",
                (practice.requirements?.items
                    ?? new List<V20ItemAmountRequirement>())
                .Where(value => value != null)
                .Select(value =>
                    $"{value.itemDefinitionId} x{value.amount}"));
            Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                practice.DisplayName,
                $"{practice.Description}\n준비물: {requirements}\n재사용 대기: 10일",
                EventAlertImportance.Medium,
                "V21 문화 관습",
                choices,
                $"practice:{practice.StableId}:{absoluteDay}")));
        }
    }

    private void PublishDiseaseResponseAlerts(IReadOnlyList<CharacterId> livingIds, int absoluteDay)
    {
        foreach (CharacterId patientId in livingIds)
        {
            if (!populationHealth.TryGetCharacterSnapshot(
                    patientId,
                    out PopulationCharacterHealthSnapshot health))
                continue;
            foreach (ActiveDiseaseSnapshot active in health.ActiveDiseases
                         .Where(value => absoluteDay >= value.SymptomDay
                             && absoluteDay < value.RecoveryDay)
                         .OrderBy(value => value.DiseaseId, StringComparer.Ordinal))
            {
                DiseaseDefinition disease = diseases.Require(active.DiseaseId);
                List<EventAlertChoice> choices = new();
                foreach (string responseId in disease.FieldResponseIds)
                {
                    string definitionId = responseId.StartsWith(
                            "response:vaccine:",
                            StringComparison.Ordinal)
                        ? "building:8876"
                        : responseId.StartsWith(
                            "response:isolate:",
                            StringComparison.Ordinal)
                            ? "building:8874"
                            : "building:8873";
                    BuildableObject facility = facilities.FindOperational(
                            FacilityCapabilityKind.Medical,
                            definitionId)
                        .FirstOrDefault();
                    if (facility == null) continue;
                    choices.Add(new EventAlertChoice(
                        responseId,
                        "가동 시설과 해당 대응 물자를 다시 검사한 뒤 질병 대응을 수행합니다.",
                        V21ContentAlertActionIds.DiseaseResponse(
                            patientId,
                            disease.Id,
                            responseId,
                            facility.PersistentInstanceId.Value)));
                }
                if (choices.Count == 0) continue;
                Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    $"질병 대응: {disease.DisplayName}",
                    $"환자: {patientId.Value}\n증상: {disease.SymptomProfileId}\n중증도: {active.Severity:0.#}",
                    EventAlertImportance.High,
                    "V21 질병 대응",
                    choices,
                    $"disease-response:{patientId.Value}:{disease.Id}")));
            }
        }
    }

    private void AddAgeTreatmentChoice(
        ICollection<EventAlertChoice> choices,
        CharacterId patientId,
        AgeTreatmentKind treatment,
        string label,
        string buildingDefinitionId)
    {
        BuildableObject facility = facilities.FindOperational(
                FacilityCapabilityKind.Medical,
                buildingDefinitionId)
            .FirstOrDefault();
        if (facility == null)
        {
            return;
        }
        choices.Add(new EventAlertChoice(
            label,
            "환자·의료진·가동 시설·물리 의료품을 요구하는 수술 주문을 생성합니다.",
            V21ContentAlertActionIds.AgeTreatment(
                patientId,
                treatment,
                facility.PersistentInstanceId.Value)));
    }

    private static IReadOnlyList<V20ChoiceDefinition> SocietyChoices(
        V20AuthoredContentSO definition,
        GuestRequestDeliverySaveData delivery)
    {
        if (definition is GuestRequestDefinitionSO)
        {
            if (delivery?.phase is GuestRequestDeliveryPhase.AwaitingVenue
                    or GuestRequestDeliveryPhase.Delivering)
            {
                return new[]
                {
                    new V20ChoiceDefinition
                    {
                        choiceId = "decline",
                        title = "요청 취소",
                        outcomeText = "운반 중인 물품을 회수한 뒤 요청을 거절합니다."
                    }
                };
            }
            if (delivery?.phase is GuestRequestDeliveryPhase.ReleasePending
                    or GuestRequestDeliveryPhase.PhysicalCommitted
                    or GuestRequestDeliveryPhase.RewardPublished)
            {
                return Array.Empty<V20ChoiceDefinition>();
            }
            return new[]
            {
                new V20ChoiceDefinition
                {
                    choiceId = "fulfill",
                    title = "요청 이행",
                    outcomeText = "요구 시설과 물품을 사용해 요청을 이행합니다."
                },
                new V20ChoiceDefinition
                {
                    choiceId = "decline",
                    title = "거절",
                    outcomeText = "요청을 거절하고 실패 결과를 적용합니다."
                }
            };
        }
        return definition switch
        {
            LifeEventDefinitionSO life => life.choices,
            ServiceIncidentDefinitionSO incident => incident.responses,
            _ => Array.Empty<V20ChoiceDefinition>()
        };
    }

    private static string FormatGuestDeliveryState(
        GuestRequestDeliverySaveData delivery)
    {
        if (delivery == null
            || delivery.phase == GuestRequestDeliveryPhase.None)
        {
            return string.Empty;
        }
        string venue = string.IsNullOrEmpty(delivery.venueFacilityInstanceId)
            ? "시설 배정 대기"
            : $"시설 {delivery.venueFacilityInstanceId} "
                + $"({delivery.destinationX}, {delivery.destinationY})";
        string reason = string.IsNullOrWhiteSpace(delivery.failureReason)
            ? string.Empty
            : " · " + delivery.failureReason;
        return $"\n납품 진행: {delivery.phase} · {venue}{reason}";
    }

    private static IReadOnlyList<EventAlertChoice> BuildReproductionStartChoices(
        ReproductionProcess process)
    {
        if (process.Mode == ReproductionMode.GolemAssembly)
        {
            return new[]
            {
                new EventAlertChoice(
                    "시작",
                    "시설과 물품을 다시 검증하고 조립 과정을 시작합니다.",
                    V21ContentAlertActionIds.ReproductionStart(
                        process.ProcessId))
            };
        }
        return new[]
        {
            new EventAlertChoice(
                "일반 시작",
                "시설과 필수 물품을 다시 검증하고 번식 과정을 시작합니다.",
                V21ContentAlertActionIds.ReproductionStart(
                    process.ProcessId)),
            new EventAlertChoice(
                "생식 치료 후 시작",
                "생식 치료제 1개를 추가 소비해 수태 성공률과 임신 안정성을 높입니다.",
                V21ContentAlertActionIds.ReproductionStart(
                    process.ProcessId,
                    useFertilityTreatment: true))
        };
    }

}
