using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

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
        CharacterId typedStaffId = new CharacterId(staffId);
        if (!typedStaffId.IsValid
            || !string.Equals(
                typedStaffId.Value,
                staffId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Staff ID must be non-empty and canonical.",
                nameof(staffId));
        }
        if (string.IsNullOrWhiteSpace(displayName)
            || !string.Equals(
                displayName,
                displayName.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Display name must be non-empty and canonical.",
                nameof(displayName));
        }
        if (!Enum.IsDefined(typeof(StaffDiscontentStage), stage)
            || !Enum.IsDefined(typeof(StaffDiscontentOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage),
                "Staff-discontent stage and outcome must be defined values.");
        }
        if (float.IsNaN(mood)
            || float.IsInfinity(mood)
            || mood < 0f
            || mood > 100f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mood),
                "Mood must be finite and between 0 and 100.");
        }
        if (lowMoodDays < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lowMoodDays),
                "Low-mood day count cannot be negative.");
        }

        this.staffId = staffId;
        this.displayName = displayName;
        this.stage = stage;
        this.outcome = outcome;
        this.mood = mood;
        this.lowMoodDays = lowMoodDays;
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
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        StaffDiscontentRecord record = new StaffDiscontentRecord(snapshot.staffId, null)
        {
            DisplayName = snapshot.displayName,
            Stage = snapshot.stage,
            LastMood = snapshot.mood,
            LowMoodDays = snapshot.lowMoodDays,
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

    public bool TryCalm(
        CharacterActor staff,
        StaffDiscontentRules rules,
        float negotiationMultiplier,
        out string failureReason)
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
            Mathf.Max(0f, rules.calmMoodRecovery)
                * Mathf.Max(0f, negotiationMultiplier),
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
    private Dictionary<string, StaffDiscontentRecord> records =
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
            .OrderBy(record => record.StaffId, StringComparer.Ordinal)
            .Select(record => record.ToSnapshot())
            .ToList();
    }

    public void Restore(IEnumerable<StaffDiscontentSnapshot> savedRecords)
    {
        if (savedRecords == null)
        {
            throw new ArgumentNullException(nameof(savedRecords));
        }

        Dictionary<string, StaffDiscontentRecord> restored =
            new Dictionary<string, StaffDiscontentRecord>(StringComparer.Ordinal);
        foreach (StaffDiscontentSnapshot snapshot in savedRecords)
        {
            StaffDiscontentRecord record = StaffDiscontentRecord.FromSnapshot(snapshot);
            if (!restored.TryAdd(record.StaffId, record))
            {
                throw new InvalidOperationException($"Duplicate staff discontent ID '{record.StaffId}'.");
            }
        }

        records = restored;
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

public static class StaffDiscontentWorkSpeedAuthority
{
    public const string Schema = "staff-discontent-work-speed-authority@1";
    public const float MaximumMultiplier = 1f;

    public static float Resolve(
        StaffDiscontentStage stage,
        StaffDiscontentRules rules)
    {
        rules ??= StaffDiscontentRules.CreateDefault();
        if (!Finite(rules.lowSatisfactionMultiplier)
            || !Finite(rules.efficiencyDropMultiplier)
            || !Finite(rules.workDisruptionMultiplier))
        {
            throw new InvalidOperationException(
                "Staff discontent work-speed rules must be finite.");
        }
        return stage switch
        {
            StaffDiscontentStage.LowSatisfaction => Mathf.Clamp(
                rules.lowSatisfactionMultiplier,
                0.1f,
                MaximumMultiplier),
            StaffDiscontentStage.EfficiencyDrop => Mathf.Clamp(
                rules.efficiencyDropMultiplier,
                0.1f,
                MaximumMultiplier),
            StaffDiscontentStage.WorkDisruption => Mathf.Clamp(
                rules.workDisruptionMultiplier,
                0.05f,
                MaximumMultiplier),
            StaffDiscontentStage.Departure => 0f,
            StaffDiscontentStage.LocalRebellion => 0f,
            _ => MaximumMultiplier
        };
    }

    private static bool Finite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
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
        return StaffDiscontentWorkSpeedAuthority.Resolve(stage, rules);
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
