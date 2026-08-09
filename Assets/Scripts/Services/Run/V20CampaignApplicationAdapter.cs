using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public readonly struct V20ContentEffectsResolvedEvent
{
    public V20ContentEffectsResolvedEvent(
        string definitionId,
        string resolutionId,
        IReadOnlyList<V20ContentEffect> effects,
        bool physicalEffectsApplied)
    {
        DefinitionId = definitionId ?? string.Empty;
        ResolutionId = resolutionId ?? string.Empty;
        Effects = effects ?? Array.Empty<V20ContentEffect>();
        PhysicalEffectsApplied = physicalEffectsApplied;
    }

    public string DefinitionId { get; }
    public string ResolutionId { get; }
    public IReadOnlyList<V20ContentEffect> Effects { get; }
    public bool PhysicalEffectsApplied { get; }
}

public sealed class V20CampaignApplicationAdapter : IStartable, IDisposable
{
    private readonly IContentResolutionService contentResolution;
    private readonly IRunMilestoneCommand milestones;
    private readonly IRunMilestoneQuery milestoneQuery;
    private readonly IEndlessCrisisCommand endless;
    private readonly IGameCalendar calendar;
    private readonly IRunSeedProvider runSeed;
    private readonly IGameEventBus events;
    private readonly V20MilestoneWorldSnapshotProjector milestoneProjector;
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly ICharacterCultureGameplayQuery cultureGameplay;
    private readonly ISocietyEventQuery societyEvents;
    private readonly IFactionCampaignQuery factionCampaigns;
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly IReproductionService reproduction;
    private readonly IFestivalDefinitionCatalog festivals;
    private readonly ICharacterLifeQuery life;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IKinshipQuery kinship;
    private readonly IGriefTraumaService grief;
    private readonly IPopulationHealthQuery populationHealth;
    private readonly IDiseaseDefinitionCatalog diseases;
    private IDisposable dayStartedSubscription;

    public V20CampaignApplicationAdapter(
        IContentResolutionService contentResolution,
        IRunMilestoneCommand milestones,
        IRunMilestoneQuery milestoneQuery,
        IEndlessCrisisCommand endless,
        IGameCalendar calendar,
        IRunSeedProvider runSeed,
        IGameEventBus events,
        V20MilestoneWorldSnapshotProjector milestoneProjector,
        ICharacterNarrativeQuery narratives,
        ICharacterNarrativeCatalog narrativeCatalog,
        ICharacterCultureGameplayQuery cultureGameplay,
        ISocietyEventQuery societyEvents,
        IFactionCampaignQuery factionCampaigns,
        V20StoryContentCatalog storyCatalog,
        IReproductionService reproduction,
        IFestivalDefinitionCatalog festivals,
        ICharacterLifeQuery life,
        IFacilityCapabilityQuery facilities,
        IKinshipQuery kinship,
        IGriefTraumaService grief,
        IPopulationHealthQuery populationHealth,
        IDiseaseDefinitionCatalog diseases)
    {
        this.contentResolution = contentResolution
            ?? throw new ArgumentNullException(nameof(contentResolution));
        this.milestones = milestones ?? throw new ArgumentNullException(nameof(milestones));
        this.milestoneQuery = milestoneQuery ?? throw new ArgumentNullException(nameof(milestoneQuery));
        this.endless = endless ?? throw new ArgumentNullException(nameof(endless));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.runSeed = runSeed ?? throw new ArgumentNullException(nameof(runSeed));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.milestoneProjector = milestoneProjector
            ?? throw new ArgumentNullException(nameof(milestoneProjector));
        this.narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        this.narrativeCatalog = narrativeCatalog
            ?? throw new ArgumentNullException(nameof(narrativeCatalog));
        this.cultureGameplay = cultureGameplay
            ?? throw new ArgumentNullException(nameof(cultureGameplay));
        this.societyEvents = societyEvents
            ?? throw new ArgumentNullException(nameof(societyEvents));
        this.factionCampaigns = factionCampaigns
            ?? throw new ArgumentNullException(nameof(factionCampaigns));
        this.storyCatalog = storyCatalog
            ?? throw new ArgumentNullException(nameof(storyCatalog));
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.festivals = festivals
            ?? throw new ArgumentNullException(nameof(festivals));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
        this.populationHealth = populationHealth
            ?? throw new ArgumentNullException(nameof(populationHealth));
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
    }

    public void Start()
    {
        dayStartedSubscription ??= events.Subscribe<OperatingDayStartedEvent>(
            OnDayStarted);
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
    }

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        RunMilestoneEvaluationSnapshot snapshot =
            milestoneProjector.Build(Math.Max(1, started.day));
        V20DailyEventContext context = new()
        {
            AbsoluteDay = Math.Max(1, started.day),
            RunSeed = runSeed.RunSeed,
            Season = GameCalendarRules.Project(Math.Max(1, started.day), 0).Season,
            Generation = Mathf.Max(
                0,
                Mathf.FloorToInt(snapshot.WorldMetrics.TryGetValue(
                    V20WorldMetricKind.CompletedGenerations,
                    out float generations)
                        ? generations
                        : 0f))
        };
        CopySnapshot(snapshot, context.Requirements);
        CharacterActor[] livingCharacters = milestoneProjector.LivingCharacters
            .Where(value => value?.Identity != null)
            .ToArray();
        SpeciesCultureId[] culturesPresent = livingCharacters
            .Select(value => new CharacterId(value.Identity.PersistentId))
            .Where(value => narratives.TryGet(value, out _))
            .Select(value =>
            {
                narratives.TryGet(value, out CharacterNarrativeSnapshot narrative);
                return narrative.CultureId;
            })
            .Where(value => value.IsValid)
            .Distinct()
            .ToArray();
        foreach (CharacterActor actor in livingCharacters)
        {
            context.ParticipantCharacterIds.Add(actor.Identity.PersistentId);
            context.ParticipantContentWeights[actor.Identity.PersistentId] =
                BuildContentWeights(actor, culturesPresent);
        }

        ContentResolutionRequest request = new()
        {
            ActionId = $"content:daily:{context.AbsoluteDay}",
            Kind = ContentResolutionRequestKind.DailyEvaluation,
            AbsoluteDay = context.AbsoluteDay,
            DailyContext = context,
            Requirements = snapshot
        };
        if (contentResolution.TryExecute(
                request,
                out ContentResolutionResult contentResult,
                out _))
        {
            foreach (V20ResolvedEventResult resolved in contentResult.Resolutions)
                events.Publish(new V20ContentEffectsResolvedEvent(
                    resolved.DefinitionId,
                    resolved.ResolutionId,
                    resolved.Effects,
                    physicalEffectsApplied: true));
        }
        PublishActionableAlerts();
        foreach (string milestoneId in milestones.Evaluate(snapshot))
        {
            events.Publish(new V20ContentEffectsResolvedEvent(
                milestoneId,
                "completed",
                Array.Empty<V20ContentEffect>(),
                physicalEffectsApplied: true));
        }
        if (milestoneQuery.Phase == RunProgressionPhase.EndlessAge
            && started.day % 10 == 0)
        {
            endless.ComposeNextEndlessCrisis(started.day, runSeed.RunSeed);
        }
    }

    private void PublishActionableAlerts()
    {
        foreach (V20ActiveEventSaveData active in societyEvents.ActiveSocietyEvents
                     .Where(value => value != null && !value.resolved)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)storyCatalog).Require(active.definitionId);
            EventAlertChoice[] choices = SocietyChoices(definition)
                .Select(choice => new EventAlertChoice(
                    choice.title,
                    choice.outcomeText,
                    V21ContentAlertActionIds.Society(
                        active.instanceId,
                        choice.choiceId)))
                .ToArray();
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                definition.DisplayName,
                $"{definition.Description}\n기한: {active.deadlineAbsoluteDay}일",
                EventAlertImportance.High,
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
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
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
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "번식 계획 승인",
                $"{parents}\n방식: {process.Mode}\n지원 시설: {process.SupportFacilityInstanceId}",
                EventAlertImportance.High,
                "V21 번식",
                BuildReproductionStartChoices(process),
                process.ProcessId)));
        }

        foreach (FestivalDefinitionSO festival in festivals.All
                     .Where(value => value != null
                         && value.season == calendar.Season
                         && value.dayOfSeason == calendar.DayOfSeason)
                     .OrderBy(value => value.StableId, StringComparer.Ordinal))
        {
            string itemSummary = string.Join(
                ", ",
                festival.requiredItems.Select(value =>
                    $"{value.itemDefinitionId} x{value.amount}"));
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                festival.displayName,
                $"{festival.description}\n준비물: {itemSummary}\n최소 참가자: {festival.minimumParticipants}",
                EventAlertImportance.High,
                "V21 축제",
                new[]
                {
                    new EventAlertChoice(
                        "축제 개최",
                        "가동 시설과 실물 준비품을 검사한 뒤 결과를 적용합니다.",
                        V21ContentAlertActionIds.Festival(festival.StableId))
                },
                $"festival:{festival.StableId}:{calendar.Year}")));
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
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
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
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
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
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
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
        PublishCulturalPracticeAlerts(livingIds);
        foreach (CharacterTombstoneSaveData tombstone in kinship.Tombstones
                     .Where(value => value != null
                         && calendar.Day >= value.deathAbsoluteDay
                         && calendar.Day - value.deathAbsoluteDay <= 7)
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
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
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
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
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

        PublishDiseaseResponseAlerts(livingIds);
    }

    private void PublishCulturalPracticeAlerts(
        IReadOnlyList<CharacterId> livingIds)
    {
        int absoluteDay = Math.Max(1, calendar.Day);
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
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                practice.DisplayName,
                $"{practice.Description}\n준비물: {requirements}\n재사용 대기: 10일",
                EventAlertImportance.Medium,
                "V21 문화 관습",
                choices,
                $"practice:{practice.StableId}:{absoluteDay}")));
        }
    }

    private void PublishDiseaseResponseAlerts(IReadOnlyList<CharacterId> livingIds)
    {
        foreach (CharacterId patientId in livingIds)
        {
            if (!populationHealth.TryGetCharacterSnapshot(
                    patientId,
                    out PopulationCharacterHealthSnapshot health))
                continue;
            foreach (ActiveDiseaseSnapshot active in health.ActiveDiseases
                         .Where(value => calendar.Day >= value.SymptomDay
                             && calendar.Day < value.RecoveryDay)
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
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
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
        V20AuthoredContentSO definition)
    {
        if (definition is GuestRequestDefinitionSO)
        {
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

    private Dictionary<string, float> BuildContentWeights(
        CharacterActor actor,
        IReadOnlyCollection<SpeciesCultureId> culturesPresent)
    {
        Dictionary<string, float> result = new(StringComparer.Ordinal);
        CharacterRuntimeProfile profile = actor?.Identity?.Profile;
        foreach (CharacterTraitSO trait in actor?.Identity?.Data?.traits
                     ?? Array.Empty<CharacterTraitSO>())
        {
            foreach (CharacterTraitEventWeight weight in trait?.eventWeights
                         ?? new List<CharacterTraitEventWeight>())
            {
                if (weight == null || !weight.IsValid) continue;
                result[weight.eventCategoryId] = Mathf.Clamp(
                    (result.TryGetValue(weight.eventCategoryId, out float current)
                        ? current
                        : 1f) * weight.multiplier,
                    0.1f,
                    10f);
            }
        }
        CharacterId id = new(actor?.Identity?.PersistentId);
        if (id.IsValid
            && narratives.TryGet(id, out CharacterNarrativeSnapshot narrative)
            && narrative.AmbitionStatus == CharacterAmbitionStatus.Active)
        {
            CharacterAmbitionDefinitionSO ambition = narrativeCatalog.Require(
                narrative.ActiveAmbitionId);
            foreach (V20WeightedId weight in ambition.relatedEventWeights
                         ?? new List<V20WeightedId>())
                result[weight.id] = Mathf.Clamp(
                    (result.TryGetValue(weight.id, out float current)
                        ? current
                        : 1f) * weight.weight,
                    0.1f,
                    10f);
        }
        if (id.IsValid)
        {
            MultiplyWeight(
                result,
                "service-incident:culturalinsult",
                cultureGameplay.GetServiceIncidentWeight(
                    id,
                    ServiceIncidentKind.CulturalInsult,
                    culturesPresent));
            MultiplyWeight(
                result,
                "service-incident:forbiddenmeal",
                cultureGameplay.GetServiceIncidentWeight(
                    id,
                    ServiceIncidentKind.ForbiddenMeal,
                    culturesPresent));
        }
        return result;
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

    private static void MultiplyWeight(
        IDictionary<string, float> weights,
        string id,
        float multiplier) => weights[id] = Mathf.Clamp(
        (weights.TryGetValue(id, out float current) ? current : 1f)
        * multiplier,
        0.1f,
        10f);

    private static void CopySnapshot(
        RunMilestoneEvaluationSnapshot source,
        RunMilestoneEvaluationSnapshot destination)
    {
        foreach (int id in source.CompletedResearchIds)
            destination.CompletedResearchIds.Add(id);
        foreach (string flag in source.WorldFlags)
            destination.WorldFlags.Add(flag);
        foreach (KeyValuePair<V20WorldMetricKind, float> pair in source.WorldMetrics)
            destination.WorldMetrics[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, int> pair in source.ItemQuantities)
            destination.ItemQuantities[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, int> pair in source.FacilityCounts)
            destination.FacilityCounts[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, FactionCampaignStateSaveData> pair in source.Factions)
            destination.Factions[pair.Key] = pair.Value;
        destination.EligibleCharacterCount = source.EligibleCharacterCount;
    }
}
