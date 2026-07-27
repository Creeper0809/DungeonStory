using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public enum StaffDiscontentStage
{
    Stable,
    LowSatisfaction,
    EfficiencyDrop,
    WorkDisruption,
    Departure,
    LocalRebellion
}

public enum StaffDiscontentOutcome
{
    None,
    Warning,
    EfficiencyPenalty,
    WorkDisruption,
    PermanentDeparture,
    LocalRebellion,
    OwnerThreat
}

public enum StaffRebellionResponseType
{
    AutoSuppress,
    SuppressCommand,
    Isolate,
    Calm
}

[Serializable]
public class StaffDiscontentRules
{
    public float lowMoodThreshold = 50f;
    public float efficiencyDropMoodThreshold = 35f;
    public float workDisruptionMoodThreshold = 25f;
    public float departureMoodThreshold = 15f;
    public float rebellionMoodThreshold = 8f;
    public int sustainedLowMoodForEfficiencyDrop = 2;
    public int sustainedLowMoodForWorkDisruption = 3;
    public int sustainedLowMoodForDeparture = 4;
    public int ownerThreatEscalationDays = 2;
    public float calmMoodRecovery = 25f;
    public float lowSatisfactionMultiplier = 0.95f;
    public float efficiencyDropMultiplier = 0.8f;
    public float workDisruptionMultiplier = 0.6f;

    public static StaffDiscontentRules CreateDefault()
    {
        return new StaffDiscontentRules();
    }
}

public sealed class StaffDiscontentSnapshot
{
    public StaffDiscontentSnapshot(
        string staffId,
        string displayName,
        StaffDiscontentStage stage,
        StaffDiscontentOutcome outcome,
        float mood,
        int lowMoodDays,
        bool permanentLoss,
        bool departed,
        bool localRebellion,
        bool ownerThreat,
        bool isolated,
        bool suppressed)
    {
        this.staffId = staffId?.Trim() ?? string.Empty;
        this.displayName = displayName ?? string.Empty;
        this.stage = stage;
        this.outcome = outcome;
        this.mood = Mathf.Clamp(mood, 0f, 100f);
        this.lowMoodDays = Mathf.Max(0, lowMoodDays);
        this.permanentLoss = permanentLoss;
        this.departed = departed;
        this.localRebellion = localRebellion;
        this.ownerThreat = ownerThreat;
        this.isolated = isolated;
        this.suppressed = suppressed;
    }

    public string staffId { get; }
    public string displayName { get; }
    public StaffDiscontentStage stage { get; }
    public StaffDiscontentOutcome outcome { get; }
    public float mood { get; }
    public int lowMoodDays { get; }
    public bool permanentLoss { get; }
    public bool departed { get; }
    public bool localRebellion { get; }
    public bool ownerThreat { get; }
    public bool isolated { get; }
    public bool suppressed { get; }

    public string ToSummaryText()
    {
        return $"{displayName} / {stage} / 기분 {mood:0.#} / 저기분 {lowMoodDays}일";
    }
}

public sealed class StaffDiscontentRecord
{
    public StaffDiscontentRecord(string staffId, CharacterActor staff)
    {
        StaffId = staffId;
        DisplayName = StaffDiscontentService.GetStaffDisplayName(staff, staffId);
    }

    public static StaffDiscontentRecord FromSnapshot(StaffDiscontentSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.staffId))
        {
            return null;
        }

        StaffDiscontentRecord record = new StaffDiscontentRecord(snapshot.staffId, null)
        {
            DisplayName = string.IsNullOrWhiteSpace(snapshot.displayName)
                ? StaffDiscontentService.GetStaffDisplayName(null, snapshot.staffId)
                : snapshot.displayName,
            Stage = snapshot.stage,
            LastMood = Mathf.Clamp(snapshot.mood, 0f, 100f),
            LowMoodDays = Mathf.Max(0, snapshot.lowMoodDays),
            IsPermanentLoss = snapshot.permanentLoss,
            IsDeparted = snapshot.departed,
            IsInLocalRebellion = snapshot.localRebellion,
            IsOwnerThreat = snapshot.ownerThreat,
            IsIsolated = snapshot.isolated,
            IsSuppressed = snapshot.suppressed
        };
        return record;
    }

    public string StaffId { get; }
    public string DisplayName { get; private set; }
    public StaffDiscontentStage Stage { get; private set; } = StaffDiscontentStage.Stable;
    public float LastMood { get; private set; } = 100f;
    public int LowMoodDays { get; private set; }
    public int LocalRebellionDays { get; private set; }
    public bool IsPermanentLoss { get; private set; }
    public bool IsDeparted { get; private set; }
    public bool IsInLocalRebellion { get; private set; }
    public bool IsOwnerThreat { get; private set; }
    public bool IsIsolated { get; private set; }
    public bool IsSuppressed { get; private set; }

    public StaffDiscontentOutcome Update(CharacterActor staff, StaffDiscontentRules rules)
    {
        rules ??= StaffDiscontentRules.CreateDefault();
        if (staff != null)
        {
            DisplayName = StaffDiscontentService.GetStaffDisplayName(staff, StaffId);
        }

        if (IsDeparted || IsSuppressed)
        {
            return StaffDiscontentOutcome.None;
        }

        LastMood = StaffDiscontentService.GetMood(staff);
        LowMoodDays = LastMood <= rules.lowMoodThreshold ? LowMoodDays + 1 : 0;
        StaffDiscontentStage previousStage = Stage;
        Stage = StaffDiscontentService.EvaluateStage(LastMood, LowMoodDays, rules);

        if (IsInLocalRebellion)
        {
            Stage = StaffDiscontentStage.LocalRebellion;
            if (IsIsolated)
            {
                return StaffDiscontentOutcome.None;
            }

            LocalRebellionDays++;
            if (!IsOwnerThreat && LocalRebellionDays >= Mathf.Max(1, rules.ownerThreatEscalationDays))
            {
                IsOwnerThreat = true;
                return StaffDiscontentOutcome.OwnerThreat;
            }

            return StaffDiscontentOutcome.None;
        }

        if (Stage == StaffDiscontentStage.LocalRebellion)
        {
            IsPermanentLoss = true;
            IsInLocalRebellion = true;
            LocalRebellionDays = 1;
            return StaffDiscontentOutcome.LocalRebellion;
        }

        if (Stage == StaffDiscontentStage.Departure)
        {
            IsPermanentLoss = true;
            IsDeparted = true;
            return StaffDiscontentOutcome.PermanentDeparture;
        }

        if (Stage == previousStage)
        {
            return StaffDiscontentOutcome.None;
        }

        return Stage switch
        {
            StaffDiscontentStage.LowSatisfaction => StaffDiscontentOutcome.Warning,
            StaffDiscontentStage.EfficiencyDrop => StaffDiscontentOutcome.EfficiencyPenalty,
            StaffDiscontentStage.WorkDisruption => StaffDiscontentOutcome.WorkDisruption,
            _ => StaffDiscontentOutcome.None
        };
    }

    public bool MarkIsolated()
    {
        if (IsDeparted || IsSuppressed || !IsInLocalRebellion)
        {
            return false;
        }

        IsIsolated = true;
        IsOwnerThreat = false;
        return true;
    }

    public bool MarkSuppressed()
    {
        if (IsDeparted || IsSuppressed || !IsInLocalRebellion)
        {
            return false;
        }

        IsSuppressed = true;
        IsInLocalRebellion = false;
        IsOwnerThreat = false;
        IsPermanentLoss = true;
        return true;
    }

    public bool TryCalm(CharacterActor staff, StaffDiscontentRules rules, out string failureReason)
    {
        rules ??= StaffDiscontentRules.CreateDefault();
        failureReason = string.Empty;

        if (IsDeparted)
        {
            failureReason = "이미 이탈했습니다";
            return false;
        }

        if (IsPermanentLoss || IsInLocalRebellion)
        {
            failureReason = "이미 영구 손실 상태입니다";
            return false;
        }

        if (Stage == StaffDiscontentStage.Stable)
        {
            failureReason = "진정이 필요하지 않습니다";
            return false;
        }

        staff?.Stats?.ApplyMoodFactor(
            "management:calmed",
            "상담으로 진정됨",
            Mathf.Max(0f, rules.calmMoodRecovery),
            240f,
            1);
        LastMood = StaffDiscontentService.GetMood(staff);
        LowMoodDays = 0;
        Stage = StaffDiscontentService.EvaluateStage(LastMood, LowMoodDays, rules);
        return true;
    }

    public StaffDiscontentSnapshot ToSnapshot(StaffDiscontentOutcome outcome = StaffDiscontentOutcome.None)
    {
        return new StaffDiscontentSnapshot(
            StaffId,
            DisplayName,
            Stage,
            outcome,
            LastMood,
            LowMoodDays,
            IsPermanentLoss,
            IsDeparted,
            IsInLocalRebellion,
            IsOwnerThreat,
            IsIsolated,
            IsSuppressed);
    }
}

public readonly struct StaffRebellionResponseResult
{
    public StaffRebellionResponseResult(
        bool success,
        StaffRebellionResponseType responseType,
        StaffDiscontentSnapshot snapshot,
        CharacterActor actor,
        string message)
    {
        Success = success;
        ResponseType = responseType;
        Snapshot = snapshot;
        Actor = actor;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public StaffRebellionResponseType ResponseType { get; }
    public StaffDiscontentSnapshot Snapshot { get; }
    public CharacterActor Actor { get; }
    public string Message { get; }
}

public sealed class StaffDiscontentState
{
    private readonly Dictionary<string, StaffDiscontentRecord> records =
        new Dictionary<string, StaffDiscontentRecord>(StringComparer.Ordinal);

    public IReadOnlyCollection<StaffDiscontentRecord> Records => records.Values;

    public StaffDiscontentRecord ProcessStaff(CharacterActor staff, StaffDiscontentRules rules, out StaffDiscontentOutcome outcome)
    {
        outcome = StaffDiscontentOutcome.None;
        if (!StaffDiscontentService.IsTrackableStaff(staff))
        {
            return null;
        }

        string staffId = StaffDiscontentService.GetStaffId(staff);
        StaffDiscontentRecord record = GetOrCreate(staffId, staff);
        outcome = record.Update(staff, rules);
        return record;
    }

    public bool TryGetRecord(CharacterActor staff, out StaffDiscontentRecord record)
    {
        record = null;
        if (!StaffDiscontentService.IsTrackableStaff(staff))
        {
            return false;
        }

        return records.TryGetValue(StaffDiscontentService.GetStaffId(staff), out record);
    }

    public bool TryGetRecord(string staffId, out StaffDiscontentRecord record)
    {
        record = null;
        return !string.IsNullOrWhiteSpace(staffId)
            && records.TryGetValue(staffId.Trim(), out record);
    }

    public bool IsPermanentLoss(CharacterActor staff)
    {
        return TryGetRecord(staff, out StaffDiscontentRecord record) && record.IsPermanentLoss;
    }

    public IReadOnlyList<StaffDiscontentSnapshot> CaptureSnapshots()
    {
        return records.Values
            .Where(record => record != null && !string.IsNullOrWhiteSpace(record.StaffId))
            .OrderBy(record => record.StaffId, StringComparer.Ordinal)
            .Select(record => record.ToSnapshot())
            .ToList();
    }

    public void Restore(IEnumerable<StaffDiscontentSnapshot> savedRecords)
    {
        records.Clear();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (StaffDiscontentSnapshot snapshot in savedRecords ?? Array.Empty<StaffDiscontentSnapshot>())
        {
            StaffDiscontentRecord record = StaffDiscontentRecord.FromSnapshot(snapshot);
            if (record == null)
            {
                continue;
            }

            if (!ids.Add(record.StaffId))
            {
                throw new InvalidOperationException($"Duplicate staff discontent ID '{record.StaffId}'.");
            }

            records[record.StaffId] = record;
        }
    }

    private StaffDiscontentRecord GetOrCreate(string staffId, CharacterActor staff)
    {
        if (!records.TryGetValue(staffId, out StaffDiscontentRecord record))
        {
            record = new StaffDiscontentRecord(staffId, staff);
            records[staffId] = record;
        }

        return record;
    }
}

public static class StaffDiscontentService
{
    public static bool IsTrackableStaff(CharacterActor staff)
    {
        CharacterIdentity identity = staff != null ? staff.Identity : null;
        return staff != null
            && identity != null
            && !identity.IsOwner
            && identity.CharacterType == CharacterType.NPC
            && !string.IsNullOrWhiteSpace(identity.PersistentId)
            && staff.TryGetAbility(out AbilityWork _);
    }

    public static string GetStaffId(CharacterActor staff)
    {
        if (staff == null)
        {
            return string.Empty;
        }

        CharacterIdentity identity = staff.Identity;
        return identity != null ? identity.PersistentId : string.Empty;
    }

    public static string GetStaffDisplayName(CharacterActor staff, string staffId)
    {
        CharacterIdentity identity = staff != null ? staff.Identity : null;
        if (!string.IsNullOrWhiteSpace(identity != null ? identity.DisplayName : null))
        {
            return identity.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(staff != null ? staff.name : null))
        {
            return staff.name;
        }

        return $"Staff {staffId}";
    }

    public static float GetMood(CharacterActor staff)
    {
        CharacterStats stats = staff != null ? staff.Stats : null;
        if (stats == null)
        {
            return 100f;
        }

        return stats.Stats.TryGetValue(CharacterCondition.MOOD, out float mood)
            ? Mathf.Clamp(mood, 0f, 100f)
            : 100f;
    }

    public static StaffDiscontentStage EvaluateStage(float mood, int lowMoodDays, StaffDiscontentRules rules)
    {
        rules ??= StaffDiscontentRules.CreateDefault();
        mood = Mathf.Clamp(mood, 0f, 100f);

        if (mood <= rules.rebellionMoodThreshold)
        {
            return StaffDiscontentStage.LocalRebellion;
        }

        if (mood <= rules.departureMoodThreshold
            || (mood <= rules.workDisruptionMoodThreshold
                && lowMoodDays >= Mathf.Max(1, rules.sustainedLowMoodForDeparture)))
        {
            return StaffDiscontentStage.Departure;
        }

        if (mood <= rules.workDisruptionMoodThreshold
            || lowMoodDays >= Mathf.Max(1, rules.sustainedLowMoodForWorkDisruption))
        {
            return StaffDiscontentStage.WorkDisruption;
        }

        if (mood <= rules.efficiencyDropMoodThreshold
            || lowMoodDays >= Mathf.Max(1, rules.sustainedLowMoodForEfficiencyDrop))
        {
            return StaffDiscontentStage.EfficiencyDrop;
        }

        if (mood <= rules.lowMoodThreshold)
        {
            return StaffDiscontentStage.LowSatisfaction;
        }

        return StaffDiscontentStage.Stable;
    }

    public static float GetWorkEfficiencyMultiplier(StaffDiscontentStage stage, StaffDiscontentRules rules)
    {
        rules ??= StaffDiscontentRules.CreateDefault();
        return stage switch
        {
            StaffDiscontentStage.LowSatisfaction => Mathf.Clamp(rules.lowSatisfactionMultiplier, 0.1f, 1f),
            StaffDiscontentStage.EfficiencyDrop => Mathf.Clamp(rules.efficiencyDropMultiplier, 0.1f, 1f),
            StaffDiscontentStage.WorkDisruption => Mathf.Clamp(rules.workDisruptionMultiplier, 0.05f, 1f),
            StaffDiscontentStage.Departure => 0f,
            StaffDiscontentStage.LocalRebellion => 0f,
            _ => 1f
        };
    }

    public static bool ShouldBlockWork(StaffDiscontentStage stage)
    {
        return stage == StaffDiscontentStage.WorkDisruption
            || stage == StaffDiscontentStage.Departure
            || stage == StaffDiscontentStage.LocalRebellion;
    }

    public static string GetBlockReason(StaffDiscontentStage stage)
    {
        return stage switch
        {
            StaffDiscontentStage.WorkDisruption => "태업/결근",
            StaffDiscontentStage.Departure => "이탈",
            StaffDiscontentStage.LocalRebellion => "반란",
            _ => string.Empty
        };
    }
}

public class StaffDiscontentRuntime : MonoBehaviour
{
    [SerializeField] private StaffDiscontentRules rules = StaffDiscontentRules.CreateDefault();

    private readonly StaffDiscontentState state = new StaffDiscontentState();
    private ICharacterWorldQuery characterWorldQuery;
    private DungeonStory.Foundation.IGameEventBus gameEventBus;
    private IDisposable operatingDayEndedSubscription;

    public StaffDiscontentState State => state;
    public StaffDiscontentRules Rules => rules;

    [Inject]
    public void Construct(
        ICharacterWorldQuery characterWorldQuery,
        DungeonStory.Foundation.IGameEventBus gameEventBus)
    {
        this.characterWorldQuery = characterWorldQuery
            ?? throw new ArgumentNullException(nameof(characterWorldQuery));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        ProcessAllStaff();
    }

    public StaffDiscontentRecord ProcessStaff(CharacterActor staff, out StaffDiscontentOutcome outcome)
    {
        StaffDiscontentRecord record = state.ProcessStaff(staff, rules, out outcome);
        if (record == null)
        {
            return null;
        }

        ApplyOutcome(staff, record, outcome);
        if (record.IsInLocalRebellion && !record.IsIsolated && !record.IsSuppressed)
        {
            DispatchAutoSuppress(staff);
        }
        return record;
    }

    public IReadOnlyList<StaffDiscontentSnapshot> CaptureSnapshots()
    {
        return state.CaptureSnapshots();
    }

    public void RestoreSnapshots(IEnumerable<StaffDiscontentSnapshot> savedRecords)
    {
        state.Restore(savedRecords);
    }

    public void ProcessAllStaff()
    {
        IReadOnlyList<CharacterActor> actors = RequireCharacterWorldQuery().Characters;
        foreach (CharacterActor staff in actors)
        {
            ProcessStaff(staff, out _);
        }
    }

    public float GetWorkEfficiencyMultiplier(CharacterActor staff)
    {
        if (!StaffDiscontentService.IsTrackableStaff(staff))
        {
            return 1f;
        }

        StaffDiscontentStage stage = state.TryGetRecord(staff, out StaffDiscontentRecord record)
            ? record.Stage
            : StaffDiscontentService.EvaluateStage(StaffDiscontentService.GetMood(staff), 0, rules);
        return StaffDiscontentService.GetWorkEfficiencyMultiplier(stage, rules);
    }

    public bool ShouldBlockWork(CharacterActor staff, out string reason)
    {
        reason = string.Empty;
        if (!StaffDiscontentService.IsTrackableStaff(staff))
        {
            return false;
        }

        StaffDiscontentStage stage = state.TryGetRecord(staff, out StaffDiscontentRecord record)
            ? record.Stage
            : StaffDiscontentService.EvaluateStage(StaffDiscontentService.GetMood(staff), 0, rules);
        if (!StaffDiscontentService.ShouldBlockWork(stage))
        {
            return false;
        }

        reason = StaffDiscontentService.GetBlockReason(stage);
        return true;
    }

    public bool IsRebellionTarget(CharacterActor target)
    {
        return state.TryGetRecord(target, out StaffDiscontentRecord record)
            && record.IsInLocalRebellion
            && !record.IsDeparted
            && !record.IsSuppressed;
    }

    public int DispatchAutoSuppress(CharacterActor rebel)
    {
        if (!IsRebellionTarget(rebel))
        {
            return 0;
        }

        IReadOnlyList<CharacterActor> characters = RequireCharacterWorldQuery().Characters;
        int assignedCount = 0;
        foreach (CharacterActor candidate in characters)
        {
            if (candidate == null
                || candidate == rebel
                || (candidate.Stats != null && candidate.Stats.IsDead)
                || !candidate.TryGetAbility(out AbilityWork work)
                || work.HasPrioritySuppressTarget
                || !work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard))
            {
                continue;
            }

            if (!WorkCommandResolver.TryResolveSuppressCommand(candidate, rebel, IsRebellionTarget, out _))
            {
                continue;
            }

            if (!work.TrySetPrioritySuppressTarget(rebel, null, out _))
            {
                continue;
            }

            assignedCount++;
        }

        if (assignedCount > 0 && state.TryGetRecord(rebel, out StaffDiscontentRecord record))
        {
            StaffRebellionResponseResult result = new StaffRebellionResponseResult(
                true,
                StaffRebellionResponseType.AutoSuppress,
                record.ToSnapshot(),
                null,
                $"자동 제압 배정: {assignedCount}명");
            gameEventBus.RaiseStaffComplaint(
                $"{record.DisplayName}: 자동 제압 {assignedCount}명 배정",
                EventAlertImportance.Medium);
        }

        return assignedCount;
    }

    public bool TryIsolateRebel(CharacterActor rebel, CharacterActor actor, out StaffRebellionResponseResult result)
    {
        if (!state.TryGetRecord(rebel, out StaffDiscontentRecord record)
            || !record.IsInLocalRebellion)
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Isolate, null, actor, "격리할 반란 대상이 없습니다");
            return false;
        }

        if (!record.MarkIsolated())
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Isolate, record.ToSnapshot(), actor, "격리할 수 없습니다");
            return false;
        }

        RecordSocial(rebel, CharacterActivityOutcomes.Completed, "반란 대응: 격리", "rebellion-isolated", 0.1f);
        result = new StaffRebellionResponseResult(true, StaffRebellionResponseType.Isolate, record.ToSnapshot(), actor, "격리 완료");
        gameEventBus.RaiseStaffComplaint($"{record.DisplayName}: 격리", EventAlertImportance.Medium);
        return true;
    }

    public bool TryCalmStaff(CharacterActor staff, CharacterActor actor, out StaffRebellionResponseResult result)
    {
        if (!state.TryGetRecord(staff, out StaffDiscontentRecord record))
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Calm, null, actor, "진정할 직원 기록이 없습니다");
            return false;
        }

        if (!record.TryCalm(staff, rules, out string failureReason))
        {
            result = new StaffRebellionResponseResult(false, StaffRebellionResponseType.Calm, record.ToSnapshot(), actor, failureReason);
            return false;
        }

        RecordSocial(staff, CharacterActivityOutcomes.Completed, "반란 대응: 진정", "rebellion-calmed", 0.35f);
        result = new StaffRebellionResponseResult(true, StaffRebellionResponseType.Calm, record.ToSnapshot(), actor, "진정 완료");
        gameEventBus.RaiseStaffComplaint($"{record.DisplayName}: 진정", EventAlertImportance.Low);
        return true;
    }

    public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)
    {
        if (!state.TryGetRecord(rebel, out StaffDiscontentRecord record))
        {
            return false;
        }

        if (!record.MarkSuppressed())
        {
            return false;
        }

        StaffRebellionResponseResult result = new StaffRebellionResponseResult(
            true,
            StaffRebellionResponseType.SuppressCommand,
            record.ToSnapshot(),
            defender,
            "제압 완료");
        gameEventBus.RaiseStaffComplaint($"{record.DisplayName}: 제압 완료", EventAlertImportance.Medium);
        return true;
    }

    private void ApplyOutcome(CharacterActor staff, StaffDiscontentRecord record, StaffDiscontentOutcome outcome)
    {
        if (outcome == StaffDiscontentOutcome.None)
        {
            return;
        }

        StaffDiscontentSnapshot snapshot = record.ToSnapshot(outcome);

        switch (outcome)
        {
            case StaffDiscontentOutcome.Warning:
                RecordSocial(staff, CharacterActivityOutcomes.Changed, "직원 불만: 만족도 낮음", "low-satisfaction", -0.45f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 만족도 낮음", EventAlertImportance.Low);
                break;
            case StaffDiscontentOutcome.EfficiencyPenalty:
                RecordSocial(staff, CharacterActivityOutcomes.Changed, "직원 불만: 효율 저하", "efficiency-penalty", -0.55f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 효율 저하", EventAlertImportance.Medium);
                break;
            case StaffDiscontentOutcome.WorkDisruption:
                RecordSocial(staff, CharacterActivityOutcomes.Blocked, "직원 불만: 태업/결근", "work-disruption", -0.7f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 태업/결근", EventAlertImportance.Medium);
                break;
            case StaffDiscontentOutcome.PermanentDeparture:
                RecordSocial(staff, CharacterActivityOutcomes.Departed, "직원 이탈: 영구 손실", "permanent-departure", -1f);
                staff?.Lifecycle?.SetLifecycleState(CharacterLifecycleState.Despawned);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 이탈", EventAlertImportance.High);
                break;
            case StaffDiscontentOutcome.LocalRebellion:
                RecordSocial(staff, CharacterActivityOutcomes.Started, "국지 반란: 주변 피해 시작", "local-rebellion", -1f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 국지 반란", EventAlertImportance.High);
                DispatchAutoSuppress(staff);
                break;
            case StaffDiscontentOutcome.OwnerThreat:
                RecordSocial(staff, CharacterActivityOutcomes.Changed, "반란 확산: 사장 위협", "owner-threat", -1f);
                gameEventBus.RaiseStaffComplaint($"{snapshot.displayName}: 반란 확산", EventAlertImportance.High);
                break;
        }
    }

    private static void RecordSocial(
        CharacterActor actor,
        string outcomeId,
        string factText,
        string reasonCode,
        float sentiment)
    {
        actor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Social,
            outcomeId,
            factText,
            actionId: "staff-discontent",
            reasonCode: reasonCode,
            sentiment: sentiment,
            bubbleEligible: true));
    }

    private ICharacterWorldQuery RequireCharacterWorldQuery()
    {
        if (characterWorldQuery == null)
        {
            throw new InvalidOperationException(
                $"{nameof(StaffDiscontentRuntime)} requires {nameof(ICharacterWorldQuery)} injection.");
        }

        return characterWorldQuery;
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        operatingDayEndedSubscription?.Dispose();
        operatingDayEndedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        operatingDayEndedSubscription ??=
            gameEventBus.Subscribe<OperatingDayEndedEvent>(OnTriggerEvent);
    }
}
