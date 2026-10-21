using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;

public class RegularCustomerRuntime : MonoBehaviour
{
    [SerializeField] private RegularCustomerRules rules = RegularCustomerRules.CreateDefault();
    [SerializeField] private SettlementImmigrationPolicy immigrationPolicy =
        SettlementImmigrationPolicy.Balanced;

    private RegularCustomerState state = new RegularCustomerState();
    private DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IRecruitedCharacterActivationService characterActivationService;
    private ICharacterPopulationService characterPopulationService;
    private IGameEventBus gameEventBus;
    private IEmploymentContractRuntime employmentContracts;
    private IBuildingWorldQuery buildingWorld;
    private IGameSessionStateProvider gameDataProvider;
    private IDomainFactPublisher factPublisher;
    private IDomainFactRecoveryPublisher factRecovery;
    private IUiClock uiClock;
    private bool mutationActive;
    private readonly Dictionary<string, string> observationFailures = new(StringComparer.Ordinal);
    private IGameMoneyAccount money;
    private IOffenseQuery offense;
    private ISettlementPopulationCapacityQuery populationCapacity;
    private IDisposable offenseRewardSubscription;
    private IDisposable facilityVisitSubscription;
    private float nextRecruitDeliveryRetryAt;
    private readonly Dictionary<string, string> recruitDeliveryFailures =
        new(StringComparer.Ordinal);

    public event Action<RegularCustomerVisitEventSnapshot> Updated;
    public event Action<RegularCustomerSnapshot> BecameRegular;
    public event Action<RegularCustomerSnapshot> CandidateDiscovered;
    public event Action<RegularCustomerRecruitEventSnapshot> Recruited;

    public RegularCustomerState State => state;
    public RegularCustomerRules Rules => rules;
    public SettlementImmigrationPolicy ImmigrationPolicy => immigrationPolicy;

    public SettlementPopulationCapacitySnapshot CapturePopulationCapacity() =>
        populationCapacity?.CapturePopulationCapacity() ?? default;

    public SettlementPopulationAcceptance EvaluateImmigration() =>
        populationCapacity?.EvaluateImmigration(immigrationPolicy)
        ?? new SettlementPopulationAcceptance(
            true,
            string.Empty,
            "Population capacity is not available in this debug fixture.");

    public SettlementImmigrationPolicy CycleImmigrationPolicy()
    {
        AssertIdle();
        immigrationPolicy = immigrationPolicy switch
        {
            SettlementImmigrationPolicy.Conservative =>
                SettlementImmigrationPolicy.Balanced,
            SettlementImmigrationPolicy.Balanced =>
                SettlementImmigrationPolicy.Open,
            _ => SettlementImmigrationPolicy.Conservative
        };
        return immigrationPolicy;
    }

    internal void RestoreImmigrationPolicy(SettlementImmigrationPolicy policy)
    {
        if (!Enum.IsDefined(typeof(SettlementImmigrationPolicy), policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, null);
        }
        immigrationPolicy = policy;
    }

    [Inject]
    public void ConstructRecruitmentRuntime(
        IUiClock uiClock,
        RegularCustomerCharacterServices characterServices,
        IGameEventBus gameEventBus,
        IEmploymentContractRuntime employmentContracts,
        IBuildingWorldQuery buildingWorld,
        IGameSessionStateProvider gameDataProvider,
        IGameMoneyAccount money,
        IOffenseQuery offense,
        ISettlementPopulationCapacityQuery populationCapacity,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IDomainFactPublisher factPublisher,
        IDomainFactRecoveryPublisher factRecovery)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        characterServices = characterServices
            ?? throw new ArgumentNullException(nameof(characterServices));
        characterActivationService = characterServices.Activation;
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        characterPopulationService = characterServices.Population;
        this.employmentContracts = employmentContracts
            ?? throw new ArgumentNullException(nameof(employmentContracts));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.factPublisher = factPublisher ?? throw new ArgumentNullException(nameof(factPublisher));
        this.factRecovery = factRecovery ?? throw new ArgumentNullException(nameof(factRecovery));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.offense = offense ?? throw new ArgumentNullException(nameof(offense));
        this.populationCapacity = populationCapacity
            ?? throw new ArgumentNullException(nameof(populationCapacity));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        state = new RegularCustomerState(this.aggregateRootStore);
        SubscribeToScopedEvents();
    }

#if UNITY_EDITOR
    public void ConstructRecruitmentRuntime(
        IUiClock uiClock,
        IRecruitedCharacterActivationService characterActivationService,
        IGameEventBus gameEventBus,
        IGameSessionStateProvider gameDataProvider,
        IDomainFactPublisher factPublisher,
        IDomainFactRecoveryPublisher factRecovery)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.characterActivationService = characterActivationService
            ?? throw new ArgumentNullException(nameof(characterActivationService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.factPublisher = factPublisher ?? throw new ArgumentNullException(nameof(factPublisher));
        this.factRecovery = factRecovery ?? throw new ArgumentNullException(nameof(factRecovery));
        SubscribeToScopedEvents();
    }
#endif

    internal void ReplaceStateFromRestore(
        IEnumerable<RegularCustomerRecord> records)
    {
        AssertIdle();
        state.ReplaceFromRecords(records);
    }

    internal RegularCustomerRestoreCandidate PrepareRestoreCandidate(
        IEnumerable<RegularCustomerRecord> records,
        IEnumerable<RegularCustomerPendingFactData> pendingFacts = null)
    {
        AssertIdle();
        return state.PrepareRestoreCandidate(records, pendingFacts);
    }

    internal void PublishRestoreCandidate(
        RegularCustomerRestoreCandidate candidate)
    {
        AssertIdle();
        state.PublishRestoreCandidate(candidate);
    }

#if UNITY_EDITOR
    public void ConstructRestoreRootForDebug(
        IUiClock uiClock,
        DungeonRuntimeAggregateRootStore rootStore)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        aggregateRootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
        state = new RegularCustomerState(aggregateRootStore);
    }

    public void ReplaceStateForDebug(
        IEnumerable<RegularCustomerRecord> records)
    {
        state.ReplaceFromRecords(records);
    }

    public void ReplaceWithEmptyStateForDebug()
    {
        ReplaceStateForDebug(Array.Empty<RegularCustomerRecord>());
    }
#endif

    internal void AssertIdle()
    {
        if (mutationActive) throw new InvalidOperationException("regular-customer-mutation-in-progress");
    }

    private void BeginMutation()
    {
        AssertIdle();
        mutationActive = true;
    }

    private RegularCustomerState CreateCandidateState()
    {
        var candidate = new RegularCustomerState();
        candidate.PublishRestoreCandidate(candidate.PrepareRestoreCandidate(state.Records, state.PendingFacts));
        return candidate;
    }

    public void OnTriggerEvent(FacilityVisitEvent eventType)
    {
        BeginMutation();
        try { RecordVisit(eventType); }
        finally { mutationActive = false; }
    }

    private void RecordVisit(FacilityVisitEvent eventType)
    {
        CharacterActor visitor = eventType.visitorActor;
        if (!RegularCustomerService.IsTrackableCustomer(visitor)) return;
        string customerId = RegularCustomerService.GetCustomerId(visitor);
        CharacterId id = (CharacterId)customerId;
        if (!id.IsValid || id.Equals(CharacterId.Owner))
        {
            Debug.LogError("regular-customer-visit-invalid-customer-id:" + customerId);
            return;
        }
        state.TryGetRecord(customerId, out var prior);
        if (prior?.IsRecruited == true) return;
        if (prior?.VisitCount == int.MaxValue)
        {
            Debug.LogError("regular-customer-visit-sequence-exhausted:" + customerId);
            return;
        }
        if (!TryResolveCurrentOutcomeDay(out int day))
        {
            Debug.LogError("regular-customer-visit-day-unavailable");
            return;
        }
        // Calculate on a detached owner candidate; no visible mutation before reservation.
        var candidate = CreateCandidateState();
        var result = candidate.RecordVisit(visitor, rules, EvaluateImmigration().Accepted);
        if (!result.Success) return;
        var raw = RegularCustomerFact.Capture(RegularCustomerFactStage.VisitRecorded,
            result.Record, day, result.BecameRegular, result.BecameRecruitCandidate);
        if (!TryPublishCandidate(candidate, raw, out string failure))
            Debug.LogError("regular-customer-visit-observation-rejected:" + failure);
    }

    private void PublishDurableVisitObservers(RegularCustomerFactData fact)
    {
        var snapshot = new RegularCustomerVisitEventSnapshot(fact);
        PublishPostCommitObserver(() => Updated?.Invoke(snapshot), "regular-customer-visit-updated-observer");
        if (fact.becameRegular)
        {
            PublishPostCommitObserver(() => BecameRegular?.Invoke(snapshot.customer), "regular-customer-became-regular-observer");
            PublishObservation(new RegularCustomerObservation(RegularCustomerObservationKind.BecameRegular, snapshot.customer));
        }
        if (fact.becameRecruitCandidate)
        {
            PublishPostCommitObserver(() => CandidateDiscovered?.Invoke(snapshot.customer), "regular-customer-candidate-observer");
            PublishObservation(new RegularCustomerObservation(RegularCustomerObservationKind.BecameRecruitCandidate, snapshot.customer));
        }
    }

    private void PublishObservation(RegularCustomerObservation observation) =>
        PublishPostCommitObserver(() => gameEventBus.Publish(observation), "regular-customer-presentation-observer");

    public bool TryRecruit(string customerId, out RegularCustomerRecruitResult result)
    {
        BeginMutation();
        try
        {
            RecoverPendingFacts();
            if (state.PendingFacts.Any(row => row.fact.customerId == customerId))
            {
                state.TryGetRecord(customerId, out var pending);
                result = new RegularCustomerRecruitResult(false, pending, "이 손님의 관찰 결과 확인을 기다리고 있습니다.");
                return false;
            }
            return TryRecruitCore(customerId, out result);
        }
        finally { mutationActive = false; }
    }

    private bool TryRecruitCore(string customerId, out RegularCustomerRecruitResult result)
    {
        int day = ResolveCurrentAbsoluteDay();
        if (!state.TryGetRecord(customerId, out RegularCustomerRecord candidate)
            || candidate == null
            || !candidate.IsRecruitCandidate
            || candidate.IsRecruited)
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                candidate?.IsRecruited == true
                    ? "이미 영입된 손님입니다."
                    : "영입 후보가 아닙니다.");
            return false;
        }

        SettlementPopulationAcceptance capacity = EvaluateImmigration();
        if (!capacity.Accepted)
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                $"정착 수용 불가 [{capacity.FailureCode}]: {capacity.Message}");
            return false;
        }

        if (!state.CanRecruitOnDay(day, rules, out int nextAllowedAbsoluteDay))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                $"다음 일반 영입은 {nextAllowedAbsoluteDay}일부터 가능합니다.");
            return false;
        }

        IRecruitedCharacterActivationService activationService =
            ResolveCharacterActivationService();
        string activationMessage = string.Empty;
        if (activationService == null
            || !activationService.TryValidateActivation(
                candidate,
                out activationMessage))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                activationService == null
                    ? "영입 캐릭터 활성화 서비스가 연결되지 않았습니다."
                    : activationMessage);
            return false;
        }

        return TryCommitRecruitment(
            candidate,
            day,
            RegularCustomerRecruitDeliveryKind.Staff,
            0,
            out result,
            out _);
    }

    public int GetMercenaryQuote(string customerId)
    {
        if (!state.TryGetRecord(
                customerId,
                out RegularCustomerRecord candidate)
            || candidate == null
            || candidate.IsRecruited
            || !TryGetMercenaryHiringAbility(
                candidate,
                out BuildingMercenaryHiringAbility ability)
            || employmentContracts == null)
        {
            return 0;
        }

        int expectedLevel =
            RecruitedCharacterActivationService.EstimateCampaignRecruitLevel(
                candidate,
                offense);
        return employmentContracts.QuoteMercenaryDailyCost(
            candidate.CustomerId,
            expectedLevel,
            ability.rolePremium);
    }

    public bool TryHireMercenary(string customerId, out RegularCustomerRecruitResult result, out int firstDailyFee)
    {
        BeginMutation();
        try
        {
            RecoverPendingFacts();
            if (state.PendingFacts.Any(row => row.fact.customerId == customerId))
            {
                firstDailyFee = 0;
                state.TryGetRecord(customerId, out var pending);
                result = new RegularCustomerRecruitResult(false, pending, "이 손님의 관찰 결과 확인을 기다리고 있습니다.");
                return false;
            }
            return TryHireMercenaryCore(customerId, out result, out firstDailyFee);
        }
        finally { mutationActive = false; }
    }

    private bool TryHireMercenaryCore(
        string customerId,
        out RegularCustomerRecruitResult result,
        out int firstDailyFee)
    {
        firstDailyFee = 0;
        if (!state.TryGetRecord(
                customerId,
                out RegularCustomerRecord candidate)
            || candidate == null
            || candidate.IsRecruited
            || !candidate.IsRecruitCandidate)
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                "용병 계약 후보가 아닙니다.");
            return false;
        }

        if (!TryGetMercenaryHiringAbility(
                candidate,
                out BuildingMercenaryHiringAbility ability))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                "용병을 고용할 수 있는 주점 시설이 필요합니다.");
            return false;
        }

        if (employmentContracts == null
            || money == null
            || gameDataProvider == null)
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                "용병 계약 서비스가 연결되지 않았습니다.");
            return false;
        }

        firstDailyFee = GetMercenaryQuote(customerId);
        if (firstDailyFee <= 0 || !money.CanSpend(firstDailyFee))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                $"첫 일급 {firstDailyFee:N0}골드가 필요합니다.");
            return false;
        }

        int day = ResolveCurrentAbsoluteDay();
        if (!state.CanRecruitOnDay(
                day,
                rules,
                out int nextAllowedAbsoluteDay))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                $"다음 영입 계약은 {nextAllowedAbsoluteDay}일부터 가능합니다.");
            return false;
        }

        IRecruitedCharacterActivationService activationService =
            ResolveCharacterActivationService();
        string activationMessage = string.Empty;
        if (activationService == null
            || !activationService.TryValidateActivation(
                candidate,
                out activationMessage))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                activationService == null
                    ? "영입 캐릭터 활성화 서비스가 연결되지 않았습니다."
                    : activationMessage);
            return false;
        }

        return TryCommitRecruitment(
            candidate,
            day,
            RegularCustomerRecruitDeliveryKind.Mercenary,
            ability.rolePremium,
            out result,
            out _);
    }

    private bool TryCommitRecruitment(
        RegularCustomerRecord candidate, int absoluteDay,
        RegularCustomerRecruitDeliveryKind deliveryKind, int mercenaryRolePremium,
        out RegularCustomerRecruitResult result, out bool deliveredImmediately)
    {
        deliveredImmediately = false;
        if (candidate == null || !TryResolveCurrentOutcomeDay(out int day))
        {
            result = new RegularCustomerRecruitResult(false, candidate, "영입 결과 날짜를 확인할 수 없습니다.");
            return false;
        }
        var preview = CreateCandidateState();
        if (!preview.TryRecruit(candidate.CustomerId, absoluteDay, rules, out result, deliveryKind, mercenaryRolePremium))
            return false;
        var raw = RegularCustomerFact.Capture(RegularCustomerFactStage.RecruitmentCommitted, result.Record, day);
        if (!TryPublishCandidate(preview, raw, out string failure))
        {
            result = new RegularCustomerRecruitResult(false, candidate, "영입 결과 관찰 실패: " + failure);
            return false;
        }
        state.TryGetRecord(candidate.CustomerId, out var committed);
        deliveredImmediately = !committed.RecruitDeliveryPending;
        result = new RegularCustomerRecruitResult(true, committed, deliveredImmediately
            ? "영입 완료" : "영입은 확정되었고 관찰 확인 또는 캐릭터 배치를 재시도합니다.");
        return true;
    }

    private bool TryPublishCandidate(RegularCustomerState candidate, RegularCustomerFactData raw, out string failure)
    {
        failure = "regular-customer-observer-unavailable";
        var fact = new RegularCustomerFact(raw);
        if (factPublisher == null || !factPublisher.TryReserve(fact, out var lease, out failure)) return false;
        var rollback = state.PrepareRestoreCandidate(state.Records, state.PendingFacts);
        var row = new RegularCustomerPendingFactData { publicationKey = lease.PublicationKey, fact = raw };
        try
        {
            candidate.AddPendingFact(row);
            state.PublishRestoreCandidate(candidate.PrepareRestoreCandidate(candidate.Records, candidate.PendingFacts));
        }
        catch { factPublisher.Cancel(lease); throw; }
        var delivery = PublishFact(lease, fact);
        if (delivery.Phase == DomainFactDeliveryPhase.Rejected)
        {
            state.PublishRestoreCandidate(rollback);
            failure = delivery.Detail;
            return false;
        }
        if (Acknowledged(row, delivery)) FinishFact(row);
        else Debug.LogWarning("regular-customer-observation-pending:" + raw.customerId + ":" + delivery.Detail);
        failure = string.Empty;
        return true;
    }

    private DomainFactDelivery PublishFact(DomainFactLease lease, RegularCustomerFact fact)
    {
        try { return factPublisher.Publish(lease, fact); }
        catch (Exception error) when (IsRecoverable(error))
        {
            factPublisher.Cancel(lease);
            return DomainFactDelivery.Uncertain("regular-customer-observation-exception:" + error.GetType().Name);
        }
    }

    private static bool IsRecoverable(Exception error) =>
        error is not OutOfMemoryException && error is not StackOverflowException && error is not AccessViolationException;

    private static bool Acknowledged(RegularCustomerPendingFactData row, DomainFactDelivery delivery) =>
        delivery.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && delivery.OperationKey == row.publicationKey
        && !string.IsNullOrWhiteSpace(delivery.PublicationId)
        && !string.IsNullOrWhiteSpace(delivery.Fingerprint);

    private void RecoverPendingFacts()
    {
        if (factRecovery == null) return;
        // A failed head blocks later observations of that person, not unrelated earned rewards.
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in state.PendingFacts)
        {
            if (blocked.Contains(row.fact.customerId)) continue;
            var deliveries = new DomainFactDelivery[1];
            bool accepted;
            string failure;
            try
            {
                accepted = factRecovery.ReplayBatch(new[] {
                    DomainFactReplay.Create(row.publicationKey, new RegularCustomerFact(row.fact))
                }, deliveries, out failure);
            }
            catch (Exception error) when (IsRecoverable(error))
            { accepted = false; failure = error.GetType().Name; }
            if (!accepted || !Acknowledged(row, deliveries[0]))
            {
                blocked.Add(row.fact.customerId);
                string detail = failure + ":" + deliveries[0].Detail;
                if (!observationFailures.TryGetValue(row.publicationKey, out string previous) || previous != detail)
                    Debug.LogWarning("regular-customer-observation-recovery-pending:" + row.fact.customerId + ":" + detail);
                observationFailures[row.publicationKey] = detail;
            }
            else FinishFact(row);
        }
    }

    private void FinishFact(RegularCustomerPendingFactData row)
    {
        var fact = row.fact;
        if (fact.stage == RegularCustomerFactStage.RecruitmentDelivered
            && !state.TryCompleteRecruitDelivery(fact.customerId, row.publicationKey))
            throw new InvalidOperationException("regular-customer-delivery-completion-state-conflict");
        state.RemovePendingFact(row.publicationKey);
        observationFailures.Remove(row.publicationKey);
        if (fact.stage == RegularCustomerFactStage.VisitRecorded) PublishDurableVisitObservers(fact);
        else if (fact.stage == RegularCustomerFactStage.RecruitmentCommitted)
        {
            state.TryGetRecord(fact.customerId, out var record);
            bool delivered = TryDeliverRecruitment(record, out string message);
            if (!delivered) RecordRecruitDeliveryFailure(record, message);
            state.TryGetRecord(fact.customerId, out record);
            var result = new RegularCustomerRecruitResult(true, record,
                delivered ? "영입 완료" : "영입은 확정되었고 캐릭터 배치를 재시도합니다: " + message);
            PublishPostCommitObserver(() => Recruited?.Invoke(new RegularCustomerRecruitEventSnapshot(result)),
                "regular-customer-recruited-observer");
            PublishObservation(new RegularCustomerObservation(RegularCustomerObservationKind.Recruited,
                record.ToSnapshot(), fact.deliveryKind, delivered));
        }
        else recruitDeliveryFailures.Remove(fact.customerId);
    }

    private bool TryDeliverRecruitment(RegularCustomerRecord record, out string message)
    {
        try { return TryDeliverRecruitmentCore(record, out message); }
        catch (Exception error) when (IsRecoverable(error))
        {
            message = "영입 배치 예외: " + error.GetType().Name;
            return false;
        }
    }

    private bool TryDeliverRecruitmentCore(RegularCustomerRecord record, out string message)
    {
        message = string.Empty;
        if (record == null || !record.IsRecruited) { message = "영입 기록이 없습니다."; return false; }
        if (!record.RecruitDeliveryPending) return true;
        if (state.PendingFacts.Any(row => row.fact.customerId == record.CustomerId))
        { message = "영입 관찰 응답 확인을 기다리고 있습니다."; return false; }
        if (factPublisher == null || !TryResolveCurrentOutcomeDay(out int day))
        { message = "영입 관찰 연결 또는 날짜를 확인할 수 없습니다."; return false; }

        var raw = RegularCustomerFact.Capture(RegularCustomerFactStage.RecruitmentDelivered, record, day);
        var fact = new RegularCustomerFact(raw);
        if (!factPublisher.TryReserve(fact, out var lease, out string failure))
        { message = "영입 배치 관찰을 예약할 수 없습니다: " + failure; return false; }
        var row = new RegularCustomerPendingFactData { publicationKey = lease.PublicationKey, fact = raw };
        try
        {
            if (!record.RecruitDeliveryApplied)
            {
                var activation = ResolveCharacterActivationService();
                if (activation == null || !activation.TryActivate(record, out CharacterActor actor, out message))
                {
                    factPublisher.Cancel(lease);
                    if (activation == null) message = "영입 캐릭터 활성화 서비스가 연결되지 않았습니다.";
                    return false;
                }
                string actorId = actor?.Identity?.PersistentId ?? "";
                if (actorId != record.CustomerId)
                {
                    factPublisher.Cancel(lease);
                    message = "영입 배치 캐릭터의 영속 ID가 계약과 일치하지 않습니다.";
                    return false;
                }
                if (record.RecruitDeliveryKind == RegularCustomerRecruitDeliveryKind.Mercenary
                    && (employmentContracts == null || !employmentContracts.TryHireMercenary(actor,
                        record.PendingMercenaryRolePremium, record.RecruitedAbsoluteDay, out message)))
                {
                    factPublisher.Cancel(lease);
                    if (employmentContracts == null) message = "용병 계약 서비스가 연결되지 않았습니다.";
                    return false;
                }
                if (!state.TryMarkRecruitDeliveryApplied(record.CustomerId, actorId))
                    throw new InvalidOperationException("regular-customer-delivery-applied-state-conflict");
            }
            // Saved before publication, after the owner effects. Retry never activates or charges again.
            state.AddPendingFact(row);
        }
        catch { factPublisher.Cancel(lease); throw; }
        var delivery = PublishFact(lease, fact);
        if (!Acknowledged(row, delivery))
        {
            message = "영입 배치 관찰 확인 대기: " + delivery.Detail;
            return false;
        }
        FinishFact(row);
        return true;
    }

    private void RecordRecruitDeliveryFailure(
        RegularCustomerRecord record,
        string message)
    {
        string customerId = record?.CustomerId ?? "<missing>";
        string normalized = string.IsNullOrWhiteSpace(message)
            ? "unknown-delivery-failure"
            : message.Trim();
        if (recruitDeliveryFailures.TryGetValue(
                customerId,
                out string previous)
            && string.Equals(previous, normalized, StringComparison.Ordinal))
        {
            return;
        }

        recruitDeliveryFailures[customerId] = normalized;
        Debug.LogError(
            "regular-customer-recruit-delivery-pending:customer="
            + customerId
            + ":reason="
            + normalized);
    }

    private void RetryPendingRecruitDeliveries()
    {
        BeginMutation();
        try
        {
            RecoverPendingFacts();
            foreach (var record in state.Records.Where(candidate => candidate?.RecruitDeliveryPending == true)
                         .OrderBy(candidate => candidate.CustomerId, StringComparer.Ordinal).ToArray())
            {
                if (!TryDeliverRecruitment(record, out string message)) RecordRecruitDeliveryFailure(record, message);
            }
        }
        finally { mutationActive = false; }
    }

#if UNITY_EDITOR
    internal void RetryPendingRecruitDeliveriesForDebug() => RetryPendingRecruitDeliveries();
#endif

    private bool TryGetMercenaryHiringAbility(
        RegularCustomerRecord candidate,
        out BuildingMercenaryHiringAbility ability)
    {
        ability = buildingWorld?.Buildings?
            .Where(building => building != null && !building.isDestroy)
            .Select(building => building.BuildingData?
                .GetAbility<BuildingMercenaryHiringAbility>())
            .Where(module => module != null
                && candidate != null
                && candidate.AverageSatisfaction
                    >= module.minimumCandidateSatisfaction)
            .OrderBy(module => module.rolePremium)
            .FirstOrDefault();
        return ability != null;
    }

    private IRecruitedCharacterActivationService ResolveCharacterActivationService()
    {
        return characterActivationService;
    }

    private int ResolveCurrentAbsoluteDay()
    {
        return gameDataProvider != null
            && gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            && gameData?.day != null
            ? Mathf.Max(1, gameData.day.Value)
            : 1;
    }

    private bool TryResolveCurrentOutcomeDay(out int absoluteDay)
    {
        absoluteDay = 0;
        return gameDataProvider != null
            && gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            && gameData?.day != null
            && (absoluteDay = gameData.day.Value) >= 0;
    }

    private static void PublishPostCommitObserver(
        Action observer,
        string label)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(label + ":" + exception.GetType().Name);
        }
    }

    public void OnTriggerEvent(OffenseRewardGrantedEvent eventType)
    {
        BeginMutation();
        try { ApplyOffenseRecruitRewards(eventType); }
        finally { mutationActive = false; }
    }

    private void ApplyOffenseRecruitRewards(OffenseRewardGrantedEvent eventType)
    {
        int rewardCandidates = eventType.grantResults?
            .Where(result => result != null
                && result.success
                && result.category == OffenseRewardCategory.RecruitCandidate)
            .Sum(result => Mathf.Max(0, result.grantedAmount)) ?? 0;
        if (rewardCandidates <= 0)
        {
            return;
        }

        List<RegularCustomerRecord> promoted = new List<RegularCustomerRecord>();
        if (characterPopulationService != null)
        {
            for (int index = 0; index < rewardCandidates; index++)
            {
                if (!characterPopulationService.TryCreateRecruitCandidate(
                        out WorldCharacterProfile profile,
                        out CharacterSO sourceData))
                {
                    break;
                }

                RegularCustomerRecord candidate =
                    state.AddRecruitCandidate(profile, sourceData);
                if (candidate != null)
                {
                    promoted.Add(candidate);
                }
            }
        }

        if (promoted.Count < rewardCandidates)
        {
            promoted.AddRange(state.PromoteBestVisitorsToRecruitCandidates(
                rewardCandidates - promoted.Count));
        }

        foreach (RegularCustomerRecord record in promoted)
        {
            RegularCustomerSnapshot snapshot = record.ToSnapshot();
            PublishPostCommitObserver(() => CandidateDiscovered?.Invoke(snapshot), "regular-customer-reward-candidate-observer");
            PublishObservation(new RegularCustomerObservation(RegularCustomerObservationKind.ExpeditionCandidate, snapshot));
        }
    }

    private void OnEnable()
    {
        nextRecruitDeliveryRetryAt = 0f;
        SubscribeToScopedEvents();
    }

    private void Update()
    {
        if (uiClock.Time < nextRecruitDeliveryRetryAt)
        {
            return;
        }

        nextRecruitDeliveryRetryAt = uiClock.Time + 5f;
        RetryPendingRecruitDeliveries();
    }

    private void OnDisable()
    {
        offenseRewardSubscription?.Dispose();
        offenseRewardSubscription = null;
        facilityVisitSubscription?.Dispose();
        facilityVisitSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        offenseRewardSubscription ??=
            gameEventBus.Subscribe<OffenseRewardGrantedEvent>(OnTriggerEvent);
        facilityVisitSubscription ??=
            gameEventBus.Subscribe<FacilityVisitEvent>(OnTriggerEvent);
    }
}
