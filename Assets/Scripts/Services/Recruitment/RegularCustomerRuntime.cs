using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class RegularCustomerRuntime : MonoBehaviour
{
    [SerializeField] private RegularCustomerRules rules = RegularCustomerRules.CreateDefault();

    private RegularCustomerState state = new RegularCustomerState();
    private DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IRecruitedCharacterActivationService characterActivationService;
    private ICharacterPopulationService characterPopulationService;
    private IGameEventBus gameEventBus;
    private IEmploymentContractRuntime employmentContracts;
    private IBuildingWorldQuery buildingWorld;
    private IGameSessionStateProvider gameDataProvider;
    private IGameMoneyAccount money;
    private IOffenseQuery offense;
    private IDisposable offenseRewardSubscription;
    private IDisposable facilityVisitSubscription;

    public event Action<RegularCustomerVisitEventSnapshot> Updated;
    public event Action<RegularCustomerSnapshot> BecameRegular;
    public event Action<RegularCustomerSnapshot> CandidateDiscovered;
    public event Action<RegularCustomerRecruitEventSnapshot> Recruited;

    public RegularCustomerState State => state;
    public RegularCustomerRules Rules => rules;

    [Inject]
    public void ConstructRecruitmentRuntime(
        RegularCustomerCharacterServices characterServices,
        IGameEventBus gameEventBus,
        IEmploymentContractRuntime employmentContracts,
        IBuildingWorldQuery buildingWorld,
        IGameSessionStateProvider gameDataProvider,
        IGameMoneyAccount money,
        IOffenseQuery offense,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
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
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.offense = offense ?? throw new ArgumentNullException(nameof(offense));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        state = new RegularCustomerState(this.aggregateRootStore);
        SubscribeToScopedEvents();
    }

#if UNITY_EDITOR
    public void ConstructRecruitmentRuntime(
        IRecruitedCharacterActivationService characterActivationService,
        IGameEventBus gameEventBus)
    {
        this.characterActivationService = characterActivationService
            ?? throw new ArgumentNullException(nameof(characterActivationService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
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
        RegularCustomerVisitResult result = state.RecordVisit(eventType.visitorActor, rules);
        if (!result.Success)
        {
            return;
        }

        Updated?.Invoke(new RegularCustomerVisitEventSnapshot(result));

        if (result.BecameRegular)
        {
            RegularCustomerSnapshot snapshot = result.Record.ToSnapshot();
            BecameRegular?.Invoke(snapshot);
            gameEventBus.RaiseAlert(
                "단골 등장",
                $"{snapshot.displayName}이 단골이 되었습니다.\n{snapshot.ToSummaryText()}",
                EventAlertImportance.Low,
                "단골");
        }

        if (result.BecameRecruitCandidate)
        {
            RegularCustomerSnapshot snapshot = result.Record.ToSnapshot();
            CandidateDiscovered?.Invoke(snapshot);
            gameEventBus.RaiseAlert(
                "영입 후보",
                $"{snapshot.displayName}을 영입할 수 있습니다.\n가능 역할: {RegularCustomerService.FormatCapabilities(snapshot.recruitCapabilities)}",
                EventAlertImportance.Medium,
                "영입");
        }
    }

    public bool TryRecruit(string customerId, out RegularCustomerRecruitResult result)
    {
        if (state.TryGetRecord(customerId, out RegularCustomerRecord candidate)
            && candidate.IsRecruitCandidate
            && !candidate.IsRecruited)
        {
            IRecruitedCharacterActivationService activationService = ResolveCharacterActivationService();
            if (activationService == null)
            {
                result = new RegularCustomerRecruitResult(
                    false,
                    candidate,
                    "영입 캐릭터 활성화 서비스가 연결되지 않았습니다.");
                return false;
            }

            if (!activationService.TryActivate(candidate, out _, out string activationMessage))
            {
                result = new RegularCustomerRecruitResult(false, candidate, activationMessage);
                return false;
            }
        }

        bool recruited = state.TryRecruit(customerId, out result);
        if (!recruited)
        {
            return false;
        }

        Recruited?.Invoke(new RegularCustomerRecruitEventSnapshot(result));
        gameEventBus.RaiseAlert(
            "손님 영입",
            $"{result.Record.DisplayName} 영입 완료\n가능 역할: {RegularCustomerService.FormatCapabilities(result.Capabilities)}",
            EventAlertImportance.Medium,
            "영입");
        return true;
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

        IRecruitedCharacterActivationService activationService =
            ResolveCharacterActivationService();
        string activationMessage =
            "용병 후보를 직원으로 배치할 수 없습니다.";
        if (activationService == null
            || !activationService.TryActivate(
                candidate,
                out CharacterActor actor,
                out activationMessage))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                activationMessage);
            return false;
        }

        int day = gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            && gameData?.day != null
            ? Mathf.Max(1, gameData.day.Value)
            : 1;
        if (!employmentContracts.TryHireMercenary(
                actor,
                ability.rolePremium,
                day,
                out string failureReason))
        {
            result = new RegularCustomerRecruitResult(
                false,
                candidate,
                failureReason);
            return false;
        }

        if (!state.TryRecruit(customerId, out result))
        {
            return false;
        }

        Recruited?.Invoke(new RegularCustomerRecruitEventSnapshot(result));
        gameEventBus.RaiseAlert(
            "용병 계약",
            $"{result.Record.DisplayName}과 용병 계약을 맺었습니다."
            + $"\n첫 일급 {firstDailyFee:N0}골드 지급",
            EventAlertImportance.Medium,
            "고용");
        return true;
    }

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
        SubscribeToScopedEvents();
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
