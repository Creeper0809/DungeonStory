using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public enum RegularCustomerStatus
{
    Visitor,
    Regular,
    RecruitCandidate,
    Recruited
}

[Flags]
public enum RecruitCapability
{
    None = 0,
    Staff = 1 << 0,
    Defense = 1 << 1,
    Expedition = 1 << 2,
    All = Staff | Defense | Expedition
}

[Serializable]
public class RegularCustomerRules
{
    public int regularVisitThreshold = 2;
    public float regularAverageSatisfactionThreshold = 65f;
    public int recruitCandidateVisitThreshold = 2;
    public float recruitCandidateAverageSatisfactionThreshold = 65f;
    public RecruitCapability defaultRecruitCapabilities = RecruitCapability.All;

    public static RegularCustomerRules CreateDefault()
    {
        return new RegularCustomerRules();
    }
}

public sealed class RegularCustomerSnapshot
{
    public RegularCustomerSnapshot(
        string customerId,
        string displayName,
        string speciesTag,
        int visitCount,
        float averageSatisfaction,
        RegularCustomerStatus status,
        RecruitCapability recruitCapabilities)
    {
        this.customerId = customerId?.Trim() ?? string.Empty;
        this.displayName = displayName ?? string.Empty;
        this.speciesTag = speciesTag ?? string.Empty;
        this.visitCount = Mathf.Max(0, visitCount);
        this.averageSatisfaction = Mathf.Clamp(averageSatisfaction, 0f, 100f);
        this.status = status;
        this.recruitCapabilities = recruitCapabilities;
    }

    public string customerId { get; }
    public string displayName { get; }
    public string speciesTag { get; }
    public int visitCount { get; }
    public float averageSatisfaction { get; }
    public RegularCustomerStatus status { get; }
    public RecruitCapability recruitCapabilities { get; }

    public string ToSummaryText()
    {
        return $"{displayName} / {speciesTag} / 방문 {visitCount}회 / 만족도 {averageSatisfaction:0.#} / {status}";
    }
}

public sealed class RegularCustomerRecord
{
    private float satisfactionTotal;

    public RegularCustomerRecord(string customerId, CharacterActor customer, RecruitCapability recruitCapabilities)
    {
        customer = CharacterActorCollection.GetCanonical(customer);
        CustomerId = customerId;
        ActiveActor = customer;
        DisplayName = RegularCustomerService.GetCustomerDisplayName(customer, customerId);
        SpeciesTag = RegularCustomerService.GetCustomerSpeciesTag(customer);
        SourceData = RegularCustomerService.GetCustomerData(customer);
        RecruitCapabilities = recruitCapabilities == RecruitCapability.None
            ? RecruitCapability.All
            : recruitCapabilities;
    }

    public RegularCustomerRecord(
        string customerId,
        string displayName,
        string speciesTag,
        CharacterSO sourceData,
        int visitCount,
        float averageSatisfaction,
        bool isRegular,
        bool isRecruitCandidate,
        bool isRecruited,
        RecruitCapability recruitCapabilities)
    {
        CustomerId = customerId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"Customer {customerId}" : displayName;
        SpeciesTag = string.IsNullOrWhiteSpace(speciesTag) ? "Unknown" : speciesTag;
        SourceData = sourceData;
        VisitCount = Mathf.Max(0, visitCount);
        satisfactionTotal = Mathf.Clamp(averageSatisfaction, 0f, 100f) * VisitCount;
        IsRegular = isRegular || isRecruitCandidate || isRecruited;
        IsRecruitCandidate = isRecruitCandidate || isRecruited;
        IsRecruited = isRecruited;
        RecruitCapabilities = recruitCapabilities == RecruitCapability.None
            ? RecruitCapability.All
            : recruitCapabilities;
    }

    public string CustomerId { get; }
    public string DisplayName { get; private set; }
    public string SpeciesTag { get; private set; }
    public CharacterSO SourceData { get; private set; }
    public CharacterActor ActiveActor { get; private set; }
    public int VisitCount { get; private set; }
    public float AverageSatisfaction => VisitCount > 0 ? satisfactionTotal / VisitCount : 0f;
    public bool IsRegular { get; private set; }
    public bool IsRecruitCandidate { get; private set; }
    public bool IsRecruited { get; private set; }
    public RecruitCapability RecruitCapabilities { get; }
    public RegularCustomerStatus Status
    {
        get
        {
            if (IsRecruited) return RegularCustomerStatus.Recruited;
            if (IsRecruitCandidate) return RegularCustomerStatus.RecruitCandidate;
            if (IsRegular) return RegularCustomerStatus.Regular;
            return RegularCustomerStatus.Visitor;
        }
    }

    public void RecordVisit(CharacterActor customer, float satisfaction, RegularCustomerRules rules)
    {
        customer = CharacterActorCollection.GetCanonical(customer);
        if (customer != null)
        {
            ActiveActor = customer;
            DisplayName = RegularCustomerService.GetCustomerDisplayName(customer, CustomerId);
            SpeciesTag = RegularCustomerService.GetCustomerSpeciesTag(customer);
            SourceData = RegularCustomerService.GetCustomerData(customer) ?? SourceData;
        }

        VisitCount++;
        satisfactionTotal += Mathf.Clamp(satisfaction, 0f, 100f);

        if (!IsRegular && RegularCustomerService.MeetsRegularCondition(this, rules))
        {
            IsRegular = true;
        }

        if (!IsRecruitCandidate && RegularCustomerService.MeetsRecruitCandidateCondition(this, rules))
        {
            IsRegular = true;
            IsRecruitCandidate = true;
        }
    }

    public bool MarkRecruited()
    {
        if (IsRecruited || !IsRecruitCandidate)
        {
            return false;
        }

        IsRecruited = true;
        IsRegular = true;
        return true;
    }

    public bool MarkRecruitCandidate()
    {
        if (IsRecruited || IsRecruitCandidate)
        {
            return false;
        }

        IsRegular = true;
        IsRecruitCandidate = true;
        return true;
    }

    public RegularCustomerSnapshot ToSnapshot()
    {
        return new RegularCustomerSnapshot(
            CustomerId,
            DisplayName,
            SpeciesTag,
            VisitCount,
            AverageSatisfaction,
            Status,
            RecruitCapabilities);
    }
}

public readonly struct RegularCustomerVisitResult
{
    public RegularCustomerVisitResult(
        bool success,
        RegularCustomerRecord record,
        bool becameRegular,
        bool becameRecruitCandidate,
        string message)
    {
        Success = success;
        Record = record;
        BecameRegular = becameRegular;
        BecameRecruitCandidate = becameRecruitCandidate;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public RegularCustomerRecord Record { get; }
    public bool BecameRegular { get; }
    public bool BecameRecruitCandidate { get; }
    public string Message { get; }
}

public readonly struct RegularCustomerRecruitResult
{
    public RegularCustomerRecruitResult(bool success, RegularCustomerRecord record, string message)
    {
        Success = success;
        Record = record;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public RegularCustomerRecord Record { get; }
    public string Message { get; }
    public CharacterSO SourceData => Record != null ? Record.SourceData : null;
    public CharacterType ResultType => CharacterType.NPC;
    public RecruitCapability Capabilities => Record != null ? Record.RecruitCapabilities : RecruitCapability.None;
}

public sealed class RegularCustomerState
{
    private readonly Dictionary<string, RegularCustomerRecord> records =
        new Dictionary<string, RegularCustomerRecord>(StringComparer.Ordinal);
    private readonly List<RegularCustomerRecruitResult> recruitedCharacters = new List<RegularCustomerRecruitResult>();
    private readonly IReadOnlyCollection<RegularCustomerRecord> recordsView;
    private readonly IReadOnlyList<RegularCustomerRecruitResult> recruitedCharactersView;

    public RegularCustomerState()
    {
        recordsView = ReadOnlyView.Collection(records.Values);
        recruitedCharactersView = ReadOnlyView.List(recruitedCharacters);
    }

    public IReadOnlyCollection<RegularCustomerRecord> Records => recordsView;
    public IReadOnlyList<RegularCustomerRecruitResult> RecruitedCharacters => recruitedCharactersView;

    public RegularCustomerVisitResult RecordVisit(CharacterActor customer, RegularCustomerRules rules)
    {
        rules ??= RegularCustomerRules.CreateDefault();
        if (!RegularCustomerService.IsTrackableCustomer(customer))
        {
            return new RegularCustomerVisitResult(false, null, false, false, "추적 가능한 손님이 아닙니다");
        }

        string customerId = RegularCustomerService.GetCustomerId(customer);
        RegularCustomerRecord record = GetOrCreate(customerId, customer, rules);
        if (record.IsRecruited)
        {
            return new RegularCustomerVisitResult(false, record, false, false, "이미 영입된 손님입니다");
        }

        bool wasRegular = record.IsRegular;
        bool wasRecruitCandidate = record.IsRecruitCandidate;
        record.RecordVisit(customer, RegularCustomerService.GetSatisfaction(customer), rules);

        bool becameRegular = !wasRegular && record.IsRegular;
        bool becameRecruitCandidate = !wasRecruitCandidate && record.IsRecruitCandidate;
        return new RegularCustomerVisitResult(true, record, becameRegular, becameRecruitCandidate, "방문 기록 갱신");
    }

    public bool TryGetRecord(string customerId, out RegularCustomerRecord record)
    {
        return records.TryGetValue(customerId, out record);
    }

    public bool IsRecruited(string customerId)
    {
        return records.TryGetValue(customerId, out RegularCustomerRecord record) && record.IsRecruited;
    }

    public bool TryRecruit(string customerId, out RegularCustomerRecruitResult result)
    {
        if (!records.TryGetValue(customerId, out RegularCustomerRecord record))
        {
            result = new RegularCustomerRecruitResult(false, null, "단골 기록이 없습니다");
            return false;
        }

        if (record.IsRecruited)
        {
            result = new RegularCustomerRecruitResult(false, record, "이미 영입된 손님입니다");
            return false;
        }

        if (!record.IsRecruitCandidate)
        {
            result = new RegularCustomerRecruitResult(false, record, "영입 후보가 아닙니다");
            return false;
        }

        if (!record.MarkRecruited())
        {
            result = new RegularCustomerRecruitResult(false, record, "영입할 수 없습니다");
            return false;
        }

        result = new RegularCustomerRecruitResult(true, record, "영입 완료");
        recruitedCharacters.Add(result);
        return true;
    }

    public IReadOnlyList<RegularCustomerRecord> PromoteBestVisitorsToRecruitCandidates(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
        {
            return Array.Empty<RegularCustomerRecord>();
        }

        List<RegularCustomerRecord> promoted = new List<RegularCustomerRecord>(safeAmount);
        foreach (RegularCustomerRecord record in records.Values
            .Where(record => record != null
                && !record.IsRecruited
                && !record.IsRecruitCandidate)
            .OrderByDescending(record => record.AverageSatisfaction)
            .ThenByDescending(record => record.VisitCount)
            .ThenBy(record => record.CustomerId, StringComparer.Ordinal))
        {
            if (!record.MarkRecruitCandidate())
            {
                continue;
            }

            promoted.Add(record);
            if (promoted.Count >= safeAmount)
            {
                break;
            }
        }

        return promoted;
    }

    public void Restore(IEnumerable<RegularCustomerRecord> savedRecords)
    {
        records.Clear();
        recruitedCharacters.Clear();

        foreach (RegularCustomerRecord record in savedRecords ?? Array.Empty<RegularCustomerRecord>())
        {
            if (record == null || string.IsNullOrWhiteSpace(record.CustomerId))
            {
                continue;
            }

            records[record.CustomerId] = record;
            if (record.IsRecruited)
            {
                recruitedCharacters.Add(new RegularCustomerRecruitResult(true, record, "Restored"));
            }
        }
    }

    private RegularCustomerRecord GetOrCreate(string customerId, CharacterActor customer, RegularCustomerRules rules)
    {
        if (!records.TryGetValue(customerId, out RegularCustomerRecord record))
        {
            record = new RegularCustomerRecord(customerId, customer, rules.defaultRecruitCapabilities);
            records[customerId] = record;
        }

        return record;
    }
}

public readonly struct RegularCustomerVisitEventSnapshot
{
    public RegularCustomerVisitEventSnapshot(RegularCustomerVisitResult result)
    {
        success = result.Success;
        customer = result.Record?.ToSnapshot();
        becameRegular = result.BecameRegular;
        becameRecruitCandidate = result.BecameRecruitCandidate;
        message = result.Message ?? string.Empty;
    }

    public bool success { get; }
    public RegularCustomerSnapshot customer { get; }
    public bool becameRegular { get; }
    public bool becameRecruitCandidate { get; }
    public string message { get; }
}

public readonly struct RegularCustomerRecruitEventSnapshot
{
    public RegularCustomerRecruitEventSnapshot(RegularCustomerRecruitResult result)
    {
        success = result.Success;
        customer = result.Record?.ToSnapshot();
        sourceData = result.SourceData;
        resultType = result.ResultType;
        capabilities = result.Capabilities;
        message = result.Message ?? string.Empty;
    }

    public bool success { get; }
    public RegularCustomerSnapshot customer { get; }
    public CharacterSO sourceData { get; }
    public CharacterType resultType { get; }
    public RecruitCapability capabilities { get; }
    public string message { get; }
}

public static class RegularCustomerService
{
    public static bool IsTrackableCustomer(CharacterActor customer)
    {
        CharacterIdentity identity = GetIdentity(customer);
        return customer != null
            && identity != null
            && identity.CharacterType == CharacterType.Customer
            && identity.Data != null;
    }

    public static string GetCustomerId(CharacterActor customer)
    {
        if (customer == null)
        {
            return string.Empty;
        }

        CharacterIdentity identity = GetIdentity(customer);
        return identity != null ? identity.PersistentId : string.Empty;
    }

    public static string GetCustomerDisplayName(CharacterActor customer, string customerId)
    {
        CharacterIdentity identity = GetIdentity(customer);
        if (!string.IsNullOrWhiteSpace(identity != null ? identity.DisplayName : null))
        {
            return identity.DisplayName;
        }

        if (customer != null && !string.IsNullOrWhiteSpace(customer.name))
        {
            return customer.name;
        }

        return $"Customer {customerId}";
    }

    public static string GetCustomerSpeciesTag(CharacterActor customer)
    {
        CharacterIdentity identity = GetIdentity(customer);
        if (!string.IsNullOrWhiteSpace(identity != null ? identity.SpeciesTag : null))
        {
            return identity.SpeciesTag;
        }

        return "Unknown";
    }

    public static float GetSatisfaction(CharacterActor customer)
    {
        CharacterStats stats = customer != null ? customer.Stats : null;
        if (stats == null)
        {
            return 0f;
        }

        return stats.Stats.TryGetValue(CharacterCondition.MOOD, out float mood)
            ? Mathf.Clamp(mood, 0f, 100f)
            : 0f;
    }

    public static CharacterSO GetCustomerData(CharacterActor customer)
    {
        return GetIdentity(customer)?.Data;
    }

    private static CharacterIdentity GetIdentity(CharacterActor customer)
    {
        return customer != null ? customer.Identity : null;
    }

    public static bool MeetsRegularCondition(RegularCustomerRecord record, RegularCustomerRules rules)
    {
        rules ??= RegularCustomerRules.CreateDefault();
        return record != null
            && record.VisitCount >= Mathf.Max(1, rules.regularVisitThreshold)
            && record.AverageSatisfaction >= Mathf.Clamp(rules.regularAverageSatisfactionThreshold, 0f, 100f);
    }

    public static bool MeetsRecruitCandidateCondition(RegularCustomerRecord record, RegularCustomerRules rules)
    {
        rules ??= RegularCustomerRules.CreateDefault();
        return record != null
            && MeetsRegularCondition(record, rules)
            && record.VisitCount >= Mathf.Max(1, rules.recruitCandidateVisitThreshold)
            && record.AverageSatisfaction >= Mathf.Clamp(rules.recruitCandidateAverageSatisfactionThreshold, 0f, 100f);
    }

    public static bool CanSpawnAsCustomer(CharacterSO data, RegularCustomerState state)
    {
        if (data == null || state == null)
        {
            return true;
        }

        if (data.characterType != CharacterType.Customer)
        {
            return true;
        }

        // Recruitment belongs to a persistent person, never to the shared character template.
        return true;
    }

    public static string FormatCapabilities(RecruitCapability capabilities)
    {
        List<string> names = new List<string>();
        if ((capabilities & RecruitCapability.Staff) != 0) names.Add("직원");
        if ((capabilities & RecruitCapability.Defense) != 0) names.Add("방어");
        if ((capabilities & RecruitCapability.Expedition) != 0) names.Add("원정");
        return names.Count > 0 ? string.Join("/", names) : "없음";
    }
}

public class RegularCustomerRuntime : MonoBehaviour
{
    [SerializeField] private RegularCustomerRules rules = RegularCustomerRules.CreateDefault();

    private readonly RegularCustomerState state = new RegularCustomerState();
    private IRecruitedCharacterActivationService characterActivationService;
    private IGameEventBus gameEventBus;
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
        IRecruitedCharacterActivationService characterActivationService,
        IGameEventBus gameEventBus)
    {
        this.characterActivationService = characterActivationService
            ?? throw new ArgumentNullException(nameof(characterActivationService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

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

        IReadOnlyList<RegularCustomerRecord> promoted =
            state.PromoteBestVisitorsToRecruitCandidates(rewardCandidates);
        foreach (RegularCustomerRecord record in promoted)
        {
            RegularCustomerSnapshot snapshot = record.ToSnapshot();
            CandidateDiscovered?.Invoke(snapshot);
            gameEventBus.RaiseAlert(
                "?먯젙 ?꾨낫",
                $"{snapshot.displayName}???먯젙 蹂댁긽?쇰줈 ?곸엯 ?꾨낫媛 ?섏뿀?듬땲??\n媛????븷: {RegularCustomerService.FormatCapabilities(snapshot.recruitCapabilities)}",
                EventAlertImportance.Medium,
                "?곸엯");
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
