using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

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
    private readonly RegularCustomerProgressState progress;

    public RegularCustomerRecord(string customerId, CharacterActor customer, RecruitCapability recruitCapabilities)
    {
        customer = CharacterActorCollection.GetCanonical(customer);
        ActiveActor = customer;
        SourceData = RegularCustomerService.GetCustomerData(customer);
        progress = new RegularCustomerProgressState(
            customerId,
            RegularCustomerService.GetCustomerDisplayName(customer, customerId),
            RegularCustomerService.GetCustomerSpeciesTag(customer),
            0,
            0f,
            false,
            false,
            false,
            0,
            recruitCapabilities);
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
        int recruitedAbsoluteDay,
        RecruitCapability recruitCapabilities)
    {
        SourceData = sourceData;
        progress = new RegularCustomerProgressState(
            customerId,
            displayName,
            speciesTag,
            visitCount,
            averageSatisfaction,
            isRegular,
            isRecruitCandidate,
            isRecruited,
            recruitedAbsoluteDay,
            recruitCapabilities);
    }

    public string CustomerId => progress.CustomerId;
    public string DisplayName => progress.DisplayName;
    public string SpeciesTag => progress.SpeciesTag;
    public CharacterSO SourceData { get; private set; }
    public CharacterActor ActiveActor { get; private set; }
    public int VisitCount => progress.VisitCount;
    public float AverageSatisfaction => progress.AverageSatisfaction;
    public bool IsRegular => progress.IsRegular;
    public bool IsRecruitCandidate => progress.IsRecruitCandidate;
    public bool IsRecruited => progress.IsRecruited;
    public int RecruitedAbsoluteDay => progress.RecruitedAbsoluteDay;
    public RecruitCapability RecruitCapabilities => progress.RecruitCapabilities;
    public RegularCustomerStatus Status => progress.Status;

    public void RecordVisit(CharacterActor customer, float satisfaction, RegularCustomerRules rules)
    {
        RecordVisit(customer, satisfaction, rules, allowRecruitCandidate: true);
    }

    public void RecordVisit(
        CharacterActor customer,
        float satisfaction,
        RegularCustomerRules rules,
        bool allowRecruitCandidate)
    {
        customer = CharacterActorCollection.GetCanonical(customer);
        if (customer != null)
        {
            ActiveActor = customer;
            progress.UpdateIdentity(
                RegularCustomerService.GetCustomerDisplayName(customer, CustomerId),
                RegularCustomerService.GetCustomerSpeciesTag(customer));
            SourceData = RegularCustomerService.GetCustomerData(customer) ?? SourceData;
        }
        progress.RecordVisit(satisfaction, rules, allowRecruitCandidate);
    }

    public bool MarkRecruited(int absoluteDay)
    {
        return progress.MarkRecruited(absoluteDay);
    }

    public bool MarkRecruitCandidate()
    {
        return progress.MarkRecruitCandidate();
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

    internal RegularCustomerRecord DeepClone()
    {
        RegularCustomerRecord clone = new RegularCustomerRecord(
            CustomerId,
            DisplayName,
            SpeciesTag,
            SourceData,
            VisitCount,
            AverageSatisfaction,
            IsRegular,
            IsRecruitCandidate,
            IsRecruited,
            RecruitedAbsoluteDay,
            RecruitCapabilities)
        {
            ActiveActor = ActiveActor
        };
        return clone;
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

public sealed class RegularCustomerAggregateState
{
    internal readonly Dictionary<string, RegularCustomerRecord> Records =
        new Dictionary<string, RegularCustomerRecord>(StringComparer.Ordinal);
    internal readonly IReadOnlyCollection<RegularCustomerRecord> RecordsView;

    public RegularCustomerAggregateState()
    {
        RecordsView = ReadOnlyView.Collection(Records.Values);
    }

    public RegularCustomerAggregateState DeepClone()
    {
        RegularCustomerAggregateState clone =
            new RegularCustomerAggregateState();
        foreach (KeyValuePair<string, RegularCustomerRecord> pair in Records)
        {
            clone.Records.Add(pair.Key, pair.Value.DeepClone());
        }

        return clone;
    }
}

internal sealed class RegularCustomerAggregateRestoreCandidate :
    RegularCustomerRestoreCandidate
{
    internal RegularCustomerAggregateRestoreCandidate(
        RegularCustomerAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal RegularCustomerAggregateState State { get; }
}

public sealed class RegularCustomerState
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private RegularCustomerAggregateState localState;

    public RegularCustomerState()
    {
        localState = new RegularCustomerAggregateState();
    }

    internal RegularCustomerState(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IReadOnlyCollection<RegularCustomerRecord> Records =>
        Current.RecordsView;
    public IReadOnlyList<RegularCustomerRecruitResult> RecruitedCharacters =>
        Current.Records.Values
            .Where(record => record.IsRecruited)
            .OrderBy(record => record.CustomerId, StringComparer.Ordinal)
            .Select(record => new RegularCustomerRecruitResult(
                true,
                record,
                "Recruited"))
            .ToArray();

    public RegularCustomerVisitResult RecordVisit(CharacterActor customer, RegularCustomerRules rules)
    {
        return RecordVisit(customer, rules, allowRecruitCandidate: true);
    }

    public RegularCustomerVisitResult RecordVisit(
        CharacterActor customer,
        RegularCustomerRules rules,
        bool allowRecruitCandidate)
    {
        Dictionary<string, RegularCustomerRecord> records = Writable.Records;
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
        record.RecordVisit(
            customer,
            RegularCustomerService.GetSatisfaction(customer),
            rules,
            allowRecruitCandidate);

        bool becameRegular = !wasRegular && record.IsRegular;
        bool becameRecruitCandidate = !wasRecruitCandidate && record.IsRecruitCandidate;
        return new RegularCustomerVisitResult(true, record, becameRegular, becameRecruitCandidate, "방문 기록 갱신");
    }

    public bool TryGetRecord(string customerId, out RegularCustomerRecord record)
    {
        return Writable.Records.TryGetValue(customerId, out record);
    }

    public RegularCustomerRecord AddRecruitCandidate(
        WorldCharacterProfile profile,
        CharacterSO sourceData,
        RecruitCapability capabilities = RecruitCapability.All)
    {
        Dictionary<string, RegularCustomerRecord> records = Writable.Records;
        if (profile == null || string.IsNullOrWhiteSpace(profile.persistentId))
        {
            return null;
        }

        if (records.TryGetValue(profile.persistentId, out RegularCustomerRecord existing))
        {
            existing.MarkRecruitCandidate();
            return existing;
        }

        RegularCustomerRecord record = new RegularCustomerRecord(
            profile.persistentId,
            profile.displayName,
            sourceData != null ? sourceData.SpeciesTag : string.Empty,
            sourceData,
            1,
            70f,
            true,
            true,
            false,
            0,
            capabilities);
        records.Add(record.CustomerId, record);
        return record;
    }

    public bool IsRecruited(string customerId)
    {
        return Current.Records.TryGetValue(
                customerId,
                out RegularCustomerRecord record)
            && record.IsRecruited;
    }

    public bool TryRecruit(
        string customerId,
        int absoluteDay,
        RegularCustomerRules rules,
        out RegularCustomerRecruitResult result)
    {
        Dictionary<string, RegularCustomerRecord> records = Writable.Records;
        rules ??= RegularCustomerRules.CreateDefault();
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

        if (!CanRecruitOnDay(
                absoluteDay,
                rules,
                out int nextAllowedAbsoluteDay))
        {
            result = new RegularCustomerRecruitResult(
                false,
                record,
                $"다음 일반 영입은 {nextAllowedAbsoluteDay}일부터 가능합니다.");
            return false;
        }

        if (!record.MarkRecruited(absoluteDay))
        {
            result = new RegularCustomerRecruitResult(false, record, "영입할 수 없습니다");
            return false;
        }

        result = new RegularCustomerRecruitResult(true, record, "영입 완료");
        return true;
    }

    public bool CanRecruitOnDay(
        int absoluteDay,
        RegularCustomerRules rules,
        out int nextAllowedAbsoluteDay)
    {
        rules ??= RegularCustomerRules.CreateDefault();
        int currentDay = Math.Max(1, absoluteDay);
        int lastRecruitmentDay = Current.Records.Values
            .Where(candidate => candidate != null && candidate.IsRecruited)
            .Select(candidate => candidate.RecruitedAbsoluteDay)
            .DefaultIfEmpty(0)
            .Max();
        int cooldown = Math.Max(1, rules.recruitmentCooldownDays);
        nextAllowedAbsoluteDay = lastRecruitmentDay <= 0
            ? currentDay
            : lastRecruitmentDay + cooldown;
        return currentDay >= nextAllowedAbsoluteDay;
    }

    public bool TryRecruit(
        string customerId,
        out RegularCustomerRecruitResult result) =>
        TryRecruit(
            customerId,
            1,
            RegularCustomerRules.CreateDefault(),
            out result);

    public IReadOnlyList<RegularCustomerRecord> PromoteBestVisitorsToRecruitCandidates(int amount)
    {
        Dictionary<string, RegularCustomerRecord> records = Writable.Records;
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

    internal void ReplaceFromRecords(
        IEnumerable<RegularCustomerRecord> savedRecords)
    {
        PublishRestoreCandidate(PrepareRestoreCandidate(savedRecords));
    }

    internal RegularCustomerRestoreCandidate PrepareRestoreCandidate(
        IEnumerable<RegularCustomerRecord> savedRecords)
    {
        if (savedRecords == null)
        {
            throw new ArgumentNullException(nameof(savedRecords));
        }

        RegularCustomerAggregateState restored =
            new RegularCustomerAggregateState();

        foreach (RegularCustomerRecord record in savedRecords)
        {
            if (record == null)
            {
                throw new InvalidOperationException(
                    "Regular-customer restore contains an invalid record.");
            }

            CharacterId customerId = (CharacterId)record.CustomerId;
            if (!customerId.IsValid || customerId.Equals(CharacterId.Owner))
            {
                throw new InvalidOperationException(
                    $"Regular-customer restore contains invalid character ID '{record.CustomerId}'.");
            }

            if (!restored.Records.TryAdd(
                    record.CustomerId,
                    record.DeepClone()))
            {
                throw new InvalidOperationException(
                    $"Duplicate regular-customer ID '{record.CustomerId}'.");
            }
        }

        return new RegularCustomerAggregateRestoreCandidate(restored);
    }

    internal void PublishRestoreCandidate(
        RegularCustomerRestoreCandidate candidate)
    {
        if (candidate is not RegularCustomerAggregateRestoreCandidate prepared)
        {
            throw new InvalidOperationException(
                "Regular-customer restore candidate has the wrong owner.");
        }

        ReplaceAggregate(prepared.State);
    }

    private RegularCustomerRecord GetOrCreate(string customerId, CharacterActor customer, RegularCustomerRules rules)
    {
        Dictionary<string, RegularCustomerRecord> records = Writable.Records;
        if (!records.TryGetValue(customerId, out RegularCustomerRecord record))
        {
            record = new RegularCustomerRecord(customerId, customer, rules.defaultRecruitCapabilities);
            records[customerId] = record;
        }

        return record;
    }

    private RegularCustomerAggregateState Current =>
        aggregateRootStore != null
            ? aggregateRootStore.GetOrCreate(
                () => new RegularCustomerAggregateState())
            : localState;

    private RegularCustomerAggregateState Writable =>
        aggregateRootStore != null
            ? aggregateRootStore.GetOrCreateWritable(
                () => new RegularCustomerAggregateState(),
                state => state.DeepClone())
            : localState;

    private void ReplaceAggregate(RegularCustomerAggregateState state)
    {
        if (aggregateRootStore != null)
        {
            aggregateRootStore.Replace(state);
            return;
        }

        localState = state ?? throw new ArgumentNullException(nameof(state));
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
        return record != null
            && RegularCustomerProgressionRules.MeetsRegularCondition(
                record.VisitCount,
                record.AverageSatisfaction,
                rules);
    }

    public static bool MeetsRecruitCandidateCondition(RegularCustomerRecord record, RegularCustomerRules rules)
    {
        return record != null
            && RegularCustomerProgressionRules.MeetsRecruitCandidateCondition(
                record.VisitCount,
                record.AverageSatisfaction,
                rules);
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

public sealed class RegularCustomerCharacterServices
{
    public RegularCustomerCharacterServices(
        IRecruitedCharacterActivationService activation,
        ICharacterPopulationService population)
    {
        Activation = activation
            ?? throw new ArgumentNullException(nameof(activation));
        Population = population
            ?? throw new ArgumentNullException(nameof(population));
    }

    public IRecruitedCharacterActivationService Activation { get; }
    public ICharacterPopulationService Population { get; }
}
