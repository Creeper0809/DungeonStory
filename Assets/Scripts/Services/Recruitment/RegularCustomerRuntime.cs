using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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
    private IMigratedProducerOutcomeTransaction outcomeTransactions;
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
        RegularCustomerCharacterServices characterServices,
        IGameEventBus gameEventBus,
        IEmploymentContractRuntime employmentContracts,
        IBuildingWorldQuery buildingWorld,
        IGameSessionStateProvider gameDataProvider,
        IGameMoneyAccount money,
        IOffenseQuery offense,
        ISettlementPopulationCapacityQuery populationCapacity,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IMigratedProducerOutcomeTransaction outcomeTransactions)
    {
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
        this.outcomeTransactions = outcomeTransactions
            ?? throw new ArgumentNullException(nameof(outcomeTransactions));
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
        IRecruitedCharacterActivationService characterActivationService,
        IGameEventBus gameEventBus,
        IGameSessionStateProvider gameDataProvider,
        IMigratedProducerOutcomeTransaction outcomeTransactions)
    {
        this.characterActivationService = characterActivationService
            ?? throw new ArgumentNullException(nameof(characterActivationService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.outcomeTransactions = outcomeTransactions
            ?? throw new ArgumentNullException(nameof(outcomeTransactions));
        SubscribeToScopedEvents();
    }
#endif

    internal void ReplaceStateFromRestore(
        IEnumerable<RegularCustomerRecord> records)
    {
        state.ReplaceFromRecords(records);
    }

    internal RegularCustomerRestoreCandidate PrepareRestoreCandidate(
        IEnumerable<RegularCustomerRecord> records)
    {
        return state.PrepareRestoreCandidate(records);
    }

    internal void PublishRestoreCandidate(
        RegularCustomerRestoreCandidate candidate)
    {
        state.PublishRestoreCandidate(candidate);
    }

#if UNITY_EDITOR
    public void ConstructRestoreRootForDebug(
        DungeonRuntimeAggregateRootStore rootStore)
    {
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

    public void OnTriggerEvent(FacilityVisitEvent eventType)
    {
        CharacterActor visitor = eventType.visitorActor;
        if (!RegularCustomerService.IsTrackableCustomer(visitor))
        {
            return;
        }

        string customerId = RegularCustomerService.GetCustomerId(visitor);
        CharacterId persistentCustomerId = (CharacterId)customerId;
        if (!persistentCustomerId.IsValid
            || persistentCustomerId.Equals(CharacterId.Owner))
        {
            Debug.LogError(
                "regular-customer-visit-outcome-invalid-customer-id:"
                + customerId);
            return;
        }

        RegularCustomerRecord prior = state.Records.FirstOrDefault(record =>
            record != null
            && string.Equals(
                record.CustomerId,
                customerId,
                StringComparison.Ordinal));
        if (prior?.IsRecruited == true)
        {
            return;
        }
        if (prior != null && prior.VisitCount == int.MaxValue)
        {
            Debug.LogError(
                "regular-customer-visit-outcome-visit-sequence-exhausted:"
                + customerId);
            return;
        }
        if (outcomeTransactions == null)
        {
            Debug.LogError(
                "regular-customer-visit-outcome-transaction-unavailable");
            return;
        }
        if (!TryResolveCurrentOutcomeDay(out int absoluteDay))
        {
            Debug.LogError(
                "regular-customer-visit-outcome-day-unavailable");
            return;
        }

        int visitSequence = (prior?.VisitCount ?? 0) + 1;
        if (!outcomeTransactions.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.RegularCustomerVisitResult,
                CreateVisitOutcomeIdentity(customerId, visitSequence),
                absoluteDay,
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out string reserveFailure))
        {
            Debug.LogError(
                "regular-customer-visit-outcome-reservation-failed:"
                + reserveFailure);
            return;
        }

        RegularCustomerRestoreCandidate rollback =
            state.PrepareRestoreCandidate(state.Records);
        SettlementPopulationAcceptance capacity = EvaluateImmigration();
        RegularCustomerVisitResult result;
        try
        {
            result = state.RecordVisit(
                visitor,
                rules,
                allowRecruitCandidate: capacity.Accepted);
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            state.PublishRestoreCandidate(rollback);
            throw;
        }
        if (!result.Success)
        {
            outcomeTransactions.Cancel(prepared);
            state.PublishRestoreCandidate(rollback);
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(result.Record?.DisplayName)
            ? customerId
            : result.Record.DisplayName;
        MigratedProducerOutcomeCommitResult committed;
        try
        {
            committed = outcomeTransactions.CommitSingleSubject(
                prepared,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    customerId,
                    displayName,
                    MigratedProducerOutcomeIds.CustomerRole),
                CreateVisitOutcomeSummary(result));
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            state.PublishRestoreCandidate(rollback);
            throw;
        }
        if (!committed.DurablyCommitted)
        {
            state.PublishRestoreCandidate(rollback);
            Debug.LogError(
                "regular-customer-visit-outcome-commit-failed:"
                + committed.DetailCode);
            return;
        }

        PublishDurableVisitObservers(result);
    }

    private void PublishDurableVisitObservers(
        RegularCustomerVisitResult result)
    {
        PublishPostCommitObserver(
            () => Updated?.Invoke(new RegularCustomerVisitEventSnapshot(result)),
            "regular-customer-visit-updated-observer");

        if (result.BecameRegular)
        {
            RegularCustomerSnapshot snapshot = result.Record.ToSnapshot();
            PublishPostCommitObserver(
                () => BecameRegular?.Invoke(snapshot),
                "regular-customer-became-regular-observer");
            PublishPostCommitObserver(
                () => gameEventBus.RaiseAlert(
                    "단골 등장",
                    $"{snapshot.displayName}이 단골이 되었습니다.\n{snapshot.ToSummaryText()}",
                    EventAlertImportance.Low,
                    "단골"),
                "regular-customer-became-regular-alert");
        }

        if (result.BecameRecruitCandidate)
        {
            RegularCustomerSnapshot snapshot = result.Record.ToSnapshot();
            PublishPostCommitObserver(
                () => CandidateDiscovered?.Invoke(snapshot),
                "regular-customer-candidate-observer");
            PublishPostCommitObserver(
                () => gameEventBus.RaiseAlert(
                    "영입 후보",
                    $"{snapshot.displayName}을 영입할 수 있습니다.\n가능 역할: {RegularCustomerService.FormatCapabilities(snapshot.recruitCapabilities)}",
                    EventAlertImportance.Medium,
                    "영입"),
                "regular-customer-candidate-alert");
        }
    }

    public bool TryRecruit(string customerId, out RegularCustomerRecruitResult result)
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
            "손님 영입",
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

    public bool TryHireMercenary(
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
            "용병 계약",
            out result,
            out _);
    }

    private bool TryCommitRecruitment(
        RegularCustomerRecord candidate,
        int absoluteDay,
        RegularCustomerRecruitDeliveryKind deliveryKind,
        int mercenaryRolePremium,
        string alertTitle,
        out RegularCustomerRecruitResult result,
        out bool deliveredImmediately)
    {
        deliveredImmediately = false;
        if (candidate == null
            || outcomeTransactions == null
            || !TryResolveCurrentOutcomeDay(out int outcomeDay))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                outcomeTransactions == null
                    ? "영입 결과 원장 트랜잭션이 연결되지 않았습니다."
                    : "영입 결과 날짜를 확인할 수 없습니다.");
            return false;
        }

        string identity = CreateRecruitOutcomeIdentity(
            candidate.CustomerId,
            absoluteDay,
            deliveryKind);
        if (!outcomeTransactions.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.RegularCustomerRecruitResult,
                identity,
                outcomeDay,
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out string reserveFailure))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                "영입 결과 원장을 예약할 수 없습니다: " + reserveFailure);
            return false;
        }

        RegularCustomerRestoreCandidate rollback =
            state.PrepareRestoreCandidate(state.Records);
        try
        {
            if (!state.TryRecruit(
                    candidate.CustomerId,
                    absoluteDay,
                    rules,
                    out result,
                    deliveryKind,
                    mercenaryRolePremium))
            {
                outcomeTransactions.Cancel(prepared);
                state.PublishRestoreCandidate(rollback);
                return false;
            }

            MigratedProducerOutcomeCommitResult committed =
                outcomeTransactions.CommitSingleSubject(
                    prepared,
                    new MigratedProducerOutcomeSubject(
                        MigratedProducerOutcomeIds.CharacterKind,
                        candidate.CustomerId,
                        candidate.DisplayName,
                        MigratedProducerOutcomeIds.CustomerRole),
                    CreateRecruitOutcomeSummary(result.Record, deliveryKind));
            if (!committed.DurablyCommitted)
            {
                state.PublishRestoreCandidate(rollback);
                result = new RegularCustomerRecruitResult(
                    false,
                    candidate,
                    "영입 결과 원장 확정 실패: " + committed.DetailCode);
                return false;
            }
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            state.PublishRestoreCandidate(rollback);
            throw;
        }

        deliveredImmediately = TryDeliverRecruitment(
            result.Record,
            out string deliveryMessage);
        if (!deliveredImmediately)
        {
            RecordRecruitDeliveryFailure(result.Record, deliveryMessage);
            result = new RegularCustomerRecruitResult(
                true,
                result.Record,
                "영입은 확정되었고 캐릭터 배치를 재시도합니다: "
                + deliveryMessage);
        }

        RegularCustomerRecruitResult publishedResult = result;
        PublishPostCommitObserver(
            () => Recruited?.Invoke(
                new RegularCustomerRecruitEventSnapshot(publishedResult)),
            "regular-customer-recruited-observer");
        string alertBody = deliveredImmediately
            ? $"{result.Record.DisplayName} 영입 완료\n가능 역할: "
                + RegularCustomerService.FormatCapabilities(result.Capabilities)
            : $"{result.Record.DisplayName} 영입 확정; 캐릭터 배치 재시도 대기";
        PublishPostCommitObserver(
            () => gameEventBus.RaiseAlert(
                alertTitle,
                alertBody,
                EventAlertImportance.Medium,
                deliveryKind == RegularCustomerRecruitDeliveryKind.Mercenary
                    ? "고용"
                    : "영입"),
            "regular-customer-recruited-alert");
        return true;
    }

    private bool TryDeliverRecruitment(
        RegularCustomerRecord record,
        out string message)
    {
        try
        {
            return TryDeliverRecruitmentCore(record, out message);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            message = "영입 배치 예외: " + exception.GetType().Name;
            return false;
        }
    }

    private bool TryDeliverRecruitmentCore(
        RegularCustomerRecord record,
        out string message)
    {
        message = string.Empty;
        if (record == null || !record.IsRecruited)
        {
            message = "영입 기록이 없습니다.";
            return false;
        }
        if (!record.RecruitDeliveryPending)
        {
            message = string.Empty;
            return true;
        }

        IRecruitedCharacterActivationService activationService =
            ResolveCharacterActivationService();
        if (activationService == null
            || !activationService.TryActivate(
                record,
                out CharacterActor actor,
                out message))
        {
            message = activationService == null
                ? "영입 캐릭터 활성화 서비스가 연결되지 않았습니다."
                : message;
            return false;
        }

        if (record.RecruitDeliveryKind
                == RegularCustomerRecruitDeliveryKind.Mercenary
            && (employmentContracts == null
                || !employmentContracts.TryHireMercenary(
                    actor,
                    record.PendingMercenaryRolePremium,
                    record.RecruitedAbsoluteDay,
                    out message)))
        {
            message = employmentContracts == null
                ? "용병 계약 서비스가 연결되지 않았습니다."
                : message;
            return false;
        }

        if (!state.TryCompleteRecruitDelivery(record.CustomerId))
        {
            message = "영입 배치 완료 상태를 저장하지 못했습니다.";
            return false;
        }

        recruitDeliveryFailures.Remove(record.CustomerId);
        message = string.Empty;
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
        foreach (RegularCustomerRecord record in state.Records
                     .Where(candidate => candidate?.RecruitDeliveryPending == true)
                     .OrderBy(candidate => candidate.CustomerId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (!TryDeliverRecruitment(record, out string message))
            {
                RecordRecruitDeliveryFailure(record, message);
            }
        }
    }

    private static string CreateRecruitOutcomeIdentity(
        string customerId,
        int absoluteDay,
        RegularCustomerRecruitDeliveryKind deliveryKind) =>
        "regular-customer-recruit:customer=" + customerId
        + ":day=" + Math.Max(1, absoluteDay)
        + ":kind=" + deliveryKind;

    private static string CreateRecruitOutcomeSummary(
        RegularCustomerRecord record,
        RegularCustomerRecruitDeliveryKind deliveryKind) =>
        (record?.DisplayName ?? record?.CustomerId ?? "Unknown")
        + " 영입이 확정되었습니다. 배치=" + deliveryKind + ".";

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

    private static string CreateVisitOutcomeIdentity(
        string customerId,
        int visitSequence) =>
        "regular-customer-visit:customer=" + customerId
        + ":visit=" + visitSequence;

    private static string CreateVisitOutcomeSummary(
        RegularCustomerVisitResult result)
    {
        RegularCustomerRecord record = result.Record;
        return record.DisplayName
            + "의 방문 " + record.VisitCount + "회차가 기록되었습니다."
            + " 상태=" + record.Status + ".";
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
            CandidateDiscovered?.Invoke(snapshot);
            gameEventBus.RaiseAlert(
                "원정 영입 후보",
                $"{snapshot.displayName}이 원정 보상으로 영입 후보가 되었습니다.\n가능 역할: {RegularCustomerService.FormatCapabilities(snapshot.recruitCapabilities)}",
                EventAlertImportance.Medium,
                "영입");
        }
    }

    private void OnEnable()
    {
        nextRecruitDeliveryRetryAt = 0f;
        SubscribeToScopedEvents();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRecruitDeliveryRetryAt)
        {
            return;
        }

        nextRecruitDeliveryRetryAt = Time.unscaledTime + 5f;
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
