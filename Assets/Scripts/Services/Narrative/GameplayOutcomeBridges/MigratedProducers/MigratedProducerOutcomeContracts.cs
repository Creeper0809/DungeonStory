using System;
using System.Collections.Generic;

public enum MigratedProducerOutcomeKind
{
    CharacterBodyHealthDownedEvent = 1,
    CharacterBodyHealthRecoveredEvent = 2,
    CharacterDeathEvent = 3,
    CharacterKilledEvent = 4,
    CombatAttackResult = 5,
    DefenseFacilityStateChangedEvent = 6,
    DefenseFacilityTriggeredEvent = 7,
    DefenseFrontCollapsedEvent = 8,
    HealthThresholdCrossedEvent = 9,
    InvasionDungeonBreachedEvent = 10,
    InvasionFacilityDamagedEvent = 11,
    InvasionResolvedEvent = 12,
    InvasionStartedEvent = 13,
    ExteriorVisitorReceptionAppliedEvent = 14,
    FactionRouteArrivedEvent = 15,
    FactionRouteCargoDeliveryReceipt = 16,
    FactionRouteSettlementReceipt = 17,
    FactionTrustChangedEvent = 18,
    FestivalCelebratedEvent = 19,
    OffenseBattleCommandResult = 20,
    OffenseExpeditionArrivalReceipt = 21,
    OffenseExpeditionNodeResult = 22,
    OffenseExpeditionResult = 23,
    RunResultReadyEvent = 24,
    RunVariableActivatedEvent = 25,
    RunVariableExpiredEvent = 26,
    V20ResolvedEventResult = 27,
    CareerMentorshipAwardCommitReceipt = 28,
    CharacterAgeConditionChangedEvent = 29,
    CharacterConsumablesMealResult = 30,
    CharacterConsumablesSubstanceResult = 31,
    CharacterDetoxTreatmentResult = 32,
    CharacterLifeStageChangedEvent = 33,
    CharacterPrimitiveSurvivalCompletedEvent = 34,
    CharacterProficiencyAwardCommitReceipt = 35,
    CharacterTabooIncidentEvent = 36,
    CharacterWaterConsumedEvent = 37,
    MealMissedEvent = 38,
    ObservedFuneralLifeEventReceipt = 39,
    ObservedLastLessonLifeEventReceipt = 40,
    ObservedLineageCompressionLifeEventReceipt = 41,
    ObservedMealIncidentCaptureResult = 42,
    ObservedQuietPromotionLifeEventReceipt = 43,
    OwnerRunEndedEvent = 44,
    RestOutcomeIdentityEvent = 45,
    AutoProcurementResult = 46,
    BuildingRetailPurchaseCommitResult = 47,
    FacilityCrimeEvent = 48,
    FacilityShopPurchaseResult = 49,
    FacilityVisitEvent = 50,
    MetaUpgradePurchasedEvent = 51,
    OperatingDayReportEvent = 52,
    PhysicalItemTransformReceipt = 53,
    RegularCustomerRecruitResult = 54,
    RegularCustomerVisitResult = 55,
    ServiceModeChangeResult = 56,
    StockSupplyResult = 57
}

public sealed class MigratedProducerOutcomeDefinition
{
    public MigratedProducerOutcomeDefinition(
        MigratedProducerOutcomeKind kind,
        string sourceTypeName,
        string outcomeTypeId,
        string producerId,
        string domainTag,
        string koreanLabel,
        float salience,
        NarrativeMemoryTier tier)
    {
        if (!Enum.IsDefined(typeof(MigratedProducerOutcomeKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        SourceTypeName = RequireText(sourceTypeName, nameof(sourceTypeName));
        OutcomeTypeId = new GameplayOutcomeTypeId(outcomeTypeId);
        ProducerId = GameplayOutcomeStableIdSyntax.Require(
            producerId,
            nameof(producerId));
        DomainTag = new GameplayOutcomeTagId(domainTag);
        KoreanLabel = RequireText(koreanLabel, nameof(koreanLabel));
        if (!float.IsFinite(salience) || salience < 0f || salience > 1f)
            throw new ArgumentOutOfRangeException(nameof(salience));
        if (tier is < NarrativeMemoryTier.Recent or > NarrativeMemoryTier.Episodic)
            throw new ArgumentOutOfRangeException(nameof(tier));
        Salience = salience;
        Tier = tier;
    }

    public MigratedProducerOutcomeKind Kind { get; }
    public string SourceTypeName { get; }
    public GameplayOutcomeTypeId OutcomeTypeId { get; }
    public string ProducerId { get; }
    public GameplayOutcomeTagId DomainTag { get; }
    public string KoreanLabel { get; }
    public float Salience { get; }
    public NarrativeMemoryTier Tier { get; }

    private static string RequireText(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > 192)
            throw new ArgumentException("A bounded non-empty value is required.", parameterName);
        return normalized;
    }
}

public static class MigratedProducerOutcomeCatalog
{
    private static readonly MigratedProducerOutcomeDefinition[] DefinitionsInternal =
    {
        D(MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent, "combat.character-downed", "전투 불능", "domain:combat", 0.92f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent, "combat.character-recovered", "전투 복귀", "domain:combat", 0.82f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.CharacterDeathEvent, "character.death", "죽음", "domain:character", 1f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.CharacterKilledEvent, "combat.character-killed", "살해 귀속", "domain:combat", 1f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.CombatAttackResult, "combat.attack-resolved", "공격 판정", "domain:combat", 0.55f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent, "defense.facility-state-changed", "방어 시설 상태 변화", "domain:defense", 0.62f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent, "defense.facility-triggered", "방어 시설 발동", "domain:defense", 0.72f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent, "defense.front-collapsed", "방어선 붕괴", "domain:defense", 0.95f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.HealthThresholdCrossedEvent, "character.health-threshold-crossed", "건강 임계 전이", "domain:character", 0.78f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent, "invasion.dungeon-breached", "던전 내부 돌파", "domain:invasion", 0.94f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent, "invasion.facility-damaged", "시설 습격 피해", "domain:invasion", 0.84f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.InvasionResolvedEvent, "invasion.resolved", "침공 종결", "domain:invasion", 0.96f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.InvasionStartedEvent, "invasion.started", "침공 시작", "domain:invasion", 0.96f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent, "external.visitor-reception-applied", "외부 방문객 응대", "domain:external", 0.64f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.FactionRouteArrivedEvent, "faction.route-arrived", "세력 교역로 도착", "domain:faction", 0.66f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt, "faction.route-cargo-delivered", "세력 화물 인도", "domain:faction", 0.7f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.FactionRouteSettlementReceipt, "faction.route-settled", "세력 교역 정산", "domain:faction", 0.7f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.FactionTrustChangedEvent, "faction.trust-changed", "세력 신뢰 변화", "domain:faction", 0.76f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.FestivalCelebratedEvent, "society.festival-celebrated", "축제 개최", "domain:society", 0.84f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.OffenseBattleCommandResult, "offense.battle-command-resolved", "원정 전투 명령", "domain:offense", 0.6f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt, "offense.expedition-arrived", "원정대 도착", "domain:offense", 0.78f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.OffenseExpeditionNodeResult, "offense.expedition-node-resolved", "원정 지점 해결", "domain:offense", 0.7f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.OffenseExpeditionResult, "offense.expedition-resolved", "원정 종결", "domain:offense", 0.9f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.RunResultReadyEvent, "run.result-ready", "런 결과 확정", "domain:run", 1f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.RunVariableActivatedEvent, "run.variable-activated", "런 변수 발현", "domain:run", 0.74f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.RunVariableExpiredEvent, "run.variable-expired", "런 변수 종료", "domain:run", 0.66f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.V20ResolvedEventResult, "run.campaign-event-resolved", "캠페인 사건 해결", "domain:run", 0.8f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.CareerMentorshipAwardCommitReceipt, "character.mentorship-awarded", "멘토링 보상", "domain:career", 0.72f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent, "character.age-condition-changed", "연령 상태 변화", "domain:life", 0.7f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.CharacterConsumablesMealResult, "survival.meal-consumed", "식사 결과", "domain:survival", 0.45f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult, "survival.substance-consumed", "기호품 섭취 결과", "domain:survival", 0.58f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult, "survival.detox-treated", "해독 치료", "domain:survival", 0.72f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent, "character.life-stage-changed", "생애 단계 변화", "domain:life", 0.9f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent, "survival.primitive-completed", "원시 생존 완수", "domain:survival", 0.82f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt, "character.proficiency-awarded", "숙련 보상", "domain:career", 0.68f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent, "survival.taboo-incident", "금기 사건", "domain:survival", 0.86f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.CharacterWaterConsumedEvent, "survival.water-consumed", "물 섭취", "domain:survival", 0.42f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.MealMissedEvent, "survival.meal-missed", "식사 누락", "domain:survival", 0.62f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt, "character.funeral-observed", "장례 목격", "domain:life", 0.94f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt, "character.last-lesson-observed", "마지막 가르침", "domain:life", 0.88f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.ObservedLineageCompressionLifeEventReceipt, "character.lineage-compression-observed", "계보 압축", "domain:life", 0.84f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult, "survival.meal-incident-observed", "식사 사건 목격", "domain:survival", 0.68f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt, "character.quiet-promotion-observed", "조용한 승진", "domain:career", 0.78f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.OwnerRunEndedEvent, "run.owner-ended", "사장 런 종료", "domain:run", 1f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.RestOutcomeIdentityEvent, "character.rest-identity-applied", "휴식 정체성 반응", "domain:life", 0.5f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.AutoProcurementResult, "economy.auto-procurement-resolved", "자동 조달", "domain:economy", 0.5f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult, "economy.retail-purchase-committed", "시설 소매 구매", "domain:economy", 0.58f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.FacilityCrimeEvent, "service.facility-crime", "시설 범죄", "domain:service", 0.9f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.FacilityShopPurchaseResult, "economy.facility-purchased", "시설 구매", "domain:economy", 0.76f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.FacilityVisitEvent, "service.facility-visited", "시설 방문", "domain:service", 0.48f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent, "meta.upgrade-purchased", "메타 업그레이드 구매", "domain:meta", 0.86f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.OperatingDayReportEvent, "service.operating-day-resolved", "영업일 정산", "domain:service", 0.74f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.PhysicalItemTransformReceipt, "inventory.item-transformed", "아이템 변환", "domain:inventory", 0.62f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.RegularCustomerRecruitResult, "service.regular-customer-recruited", "단골 영입", "domain:service", 0.82f, NarrativeMemoryTier.Core),
        D(MigratedProducerOutcomeKind.RegularCustomerVisitResult, "service.regular-customer-visited", "단골 방문", "domain:service", 0.64f, NarrativeMemoryTier.Episodic),
        D(MigratedProducerOutcomeKind.ServiceModeChangeResult, "service.mode-changed", "서비스 모드 변경", "domain:service", 0.56f, NarrativeMemoryTier.Recent),
        D(MigratedProducerOutcomeKind.StockSupplyResult, "inventory.stock-supplied", "재고 공급", "domain:inventory", 0.48f, NarrativeMemoryTier.Recent)
    };

    private static readonly Dictionary<MigratedProducerOutcomeKind, MigratedProducerOutcomeDefinition>
        ByKind = new();
    private static readonly Dictionary<GameplayOutcomeTypeId, MigratedProducerOutcomeDefinition>
        ByOutcomeType = new();
    private static readonly GameplayOutcomeTypeId[] SupportedTypesInternal;

    static MigratedProducerOutcomeCatalog()
    {
        if (DefinitionsInternal.Length != 57)
            throw new InvalidOperationException("The migrated producer outcome catalog must contain exactly 57 definitions.");
        SupportedTypesInternal = new GameplayOutcomeTypeId[DefinitionsInternal.Length];
        for (int index = 0; index < DefinitionsInternal.Length; index++)
        {
            MigratedProducerOutcomeDefinition definition = DefinitionsInternal[index];
            if (!ByKind.TryAdd(definition.Kind, definition)
                || !ByOutcomeType.TryAdd(definition.OutcomeTypeId, definition))
            {
                throw new InvalidOperationException("The migrated producer outcome catalog contains duplicate identities.");
            }
            SupportedTypesInternal[index] = definition.OutcomeTypeId;
        }
    }

    public static IReadOnlyList<MigratedProducerOutcomeDefinition> Definitions =>
        DefinitionsInternal;
    public static IReadOnlyList<GameplayOutcomeTypeId> SupportedOutcomeTypes =>
        SupportedTypesInternal;

    public static MigratedProducerOutcomeDefinition Get(
        MigratedProducerOutcomeKind kind) =>
        ByKind.TryGetValue(kind, out MigratedProducerOutcomeDefinition definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(kind));

    public static bool TryGet(
        GameplayOutcomeTypeId outcomeTypeId,
        out MigratedProducerOutcomeDefinition definition) =>
        ByOutcomeType.TryGetValue(outcomeTypeId, out definition);

    private static MigratedProducerOutcomeDefinition D(
        MigratedProducerOutcomeKind kind,
        string id,
        string label,
        string tag,
        float salience,
        NarrativeMemoryTier tier) => new(
        kind,
        kind.ToString(),
        id,
        "ledger." + id,
        tag,
        label,
        salience,
        tier);
}

public struct MigratedOutcomeFixedBuffer16<T>
{
    private T item0; private T item1; private T item2; private T item3;
    private T item4; private T item5; private T item6; private T item7;
    private T item8; private T item9; private T item10; private T item11;
    private T item12; private T item13; private T item14; private T item15;

    public int Count { get; private set; }

    public bool TryAdd(in T value)
    {
        switch (Count)
        {
            case 0: item0 = value; break; case 1: item1 = value; break;
            case 2: item2 = value; break; case 3: item3 = value; break;
            case 4: item4 = value; break; case 5: item5 = value; break;
            case 6: item6 = value; break; case 7: item7 = value; break;
            case 8: item8 = value; break; case 9: item9 = value; break;
            case 10: item10 = value; break; case 11: item11 = value; break;
            case 12: item12 = value; break; case 13: item13 = value; break;
            case 14: item14 = value; break; case 15: item15 = value; break;
            default: return false;
        }
        Count++;
        return true;
    }

    public T Get(int index) => index switch
    {
        0 when index < Count => item0, 1 when index < Count => item1,
        2 when index < Count => item2, 3 when index < Count => item3,
        4 when index < Count => item4, 5 when index < Count => item5,
        6 when index < Count => item6, 7 when index < Count => item7,
        8 when index < Count => item8, 9 when index < Count => item9,
        10 when index < Count => item10, 11 when index < Count => item11,
        12 when index < Count => item12, 13 when index < Count => item13,
        14 when index < Count => item14, 15 when index < Count => item15,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

public readonly struct MigratedProducerOutcomePayload
{
    internal MigratedProducerOutcomePayload(
        GameplayResultKey resultKey,
        GameplayOutcomeTypeId outcomeTypeId,
        int absoluteDay,
        GameplayOutcomeStatus status,
        long ownerRevision,
        in MigratedOutcomeFixedBuffer16<GameplayOutcomeParticipant> participants,
        in MigratedOutcomeFixedBuffer16<GameplayOutcomeMetric> metrics,
        in MigratedOutcomeFixedBuffer16<GameplayOutcomeSubjectLink> subjects,
        in MigratedOutcomeFixedBuffer16<GameplayOutcomeTagId> tags,
        in MigratedOutcomeFixedBuffer16<GameplayOutcomeProvenanceReference> provenance,
        in MigratedOutcomeFixedBuffer16<GameplayOutcomeFact> facts,
        in GameplayLocationReference location,
        in GameplayOutcomeCausation causation)
    {
        ResultKey = resultKey;
        OutcomeTypeId = outcomeTypeId;
        AbsoluteDay = absoluteDay;
        Status = status;
        OwnerRevision = ownerRevision;
        Participants = participants;
        Metrics = metrics;
        Subjects = subjects;
        Tags = tags;
        Provenance = provenance;
        Facts = facts;
        Location = location;
        Causation = causation;
    }

    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeTypeId OutcomeTypeId { get; }
    public int AbsoluteDay { get; }
    public GameplayOutcomeStatus Status { get; }
    public long OwnerRevision { get; }
    public MigratedOutcomeFixedBuffer16<GameplayOutcomeParticipant> Participants { get; }
    public MigratedOutcomeFixedBuffer16<GameplayOutcomeMetric> Metrics { get; }
    public MigratedOutcomeFixedBuffer16<GameplayOutcomeSubjectLink> Subjects { get; }
    public MigratedOutcomeFixedBuffer16<GameplayOutcomeTagId> Tags { get; }
    public MigratedOutcomeFixedBuffer16<GameplayOutcomeProvenanceReference> Provenance { get; }
    public MigratedOutcomeFixedBuffer16<GameplayOutcomeFact> Facts { get; }
    public GameplayLocationReference Location { get; }
    public GameplayOutcomeCausation Causation { get; }
}

public struct MigratedProducerOutcomePayloadBuilder
{
    private readonly GameplayResultKey resultKey;
    private readonly GameplayOutcomeTypeId outcomeTypeId;
    private readonly int absoluteDay;
    private readonly GameplayOutcomeStatus status;
    private readonly long ownerRevision;
    private MigratedOutcomeFixedBuffer16<GameplayOutcomeParticipant> participants;
    private MigratedOutcomeFixedBuffer16<GameplayOutcomeMetric> metrics;
    private MigratedOutcomeFixedBuffer16<GameplayOutcomeSubjectLink> subjects;
    private MigratedOutcomeFixedBuffer16<GameplayOutcomeTagId> tags;
    private MigratedOutcomeFixedBuffer16<GameplayOutcomeProvenanceReference> provenance;
    private MigratedOutcomeFixedBuffer16<GameplayOutcomeFact> facts;
    private GameplayLocationReference location;
    private GameplayOutcomeCausation causation;

    public MigratedProducerOutcomePayloadBuilder(
        GameplayResultKey resultKey,
        GameplayOutcomeTypeId outcomeTypeId,
        int absoluteDay,
        GameplayOutcomeStatus status,
        long ownerRevision)
    {
        this.resultKey = resultKey;
        this.outcomeTypeId = outcomeTypeId;
        this.absoluteDay = absoluteDay;
        this.status = status;
        this.ownerRevision = ownerRevision;
        participants = default;
        metrics = default;
        subjects = default;
        tags = default;
        provenance = default;
        facts = default;
        location = default;
        causation = default;
    }

    public bool AddParticipant(in GameplayOutcomeParticipant value) => participants.TryAdd(value);
    public bool AddMetric(in GameplayOutcomeMetric value) => metrics.TryAdd(value);
    public bool AddSubject(in GameplayOutcomeSubjectLink value) => subjects.TryAdd(value);
    public bool AddTag(GameplayOutcomeTagId value) => tags.TryAdd(value);
    public bool AddProvenance(in GameplayOutcomeProvenanceReference value) => provenance.TryAdd(value);
    public bool AddFact(in GameplayOutcomeFact value) => facts.TryAdd(value);
    public void SetLocation(in GameplayLocationReference value) => location = value;
    public void SetCausation(in GameplayOutcomeCausation value) => causation = value;

    public MigratedProducerOutcomeReceipt Build() => new(new MigratedProducerOutcomePayload(
        resultKey,
        outcomeTypeId,
        absoluteDay,
        status,
        ownerRevision,
        participants,
        metrics,
        subjects,
        tags,
        provenance,
        facts,
        location,
        causation));
}

public readonly struct MigratedProducerOutcomeReceipt
{
    public MigratedProducerOutcomeReceipt(MigratedProducerOutcomePayload payload) =>
        Payload = payload;
    public MigratedProducerOutcomePayload Payload { get; }
}
