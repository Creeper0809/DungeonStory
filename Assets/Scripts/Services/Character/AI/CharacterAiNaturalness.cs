using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

[Serializable]
public sealed class CharacterAiIntentState
{
    [SerializeField] private CharacterAiBranch branch = CharacterAiBranch.None;
    [SerializeField] private CharacterAiIntentionType intention = CharacterAiIntentionType.None;
    [SerializeField] private string label = string.Empty;
    [SerializeField] private int targetGridId = -1;
    [SerializeField] private float startedAt;
    [SerializeField] private float minUntil;
    [SerializeField] private float expiresAt;
    [SerializeField] private bool interruptible = true;
    [SerializeField] private string lastBreakReason = string.Empty;

    public CharacterAiBranch Branch => branch;
    public CharacterAiIntentionType Intention => intention;
    public string Label => label;
    public int TargetGridId => targetGridId;
    public float StartedAt => startedAt;
    public float MinUntil => minUntil;
    public float ExpiresAt => expiresAt;
    public bool Interruptible => interruptible;
    public string LastBreakReason => lastBreakReason;

    public bool IsActive(float now)
    {
        return branch != CharacterAiBranch.None
            && (expiresAt <= 0f || now <= expiresAt);
    }

    public bool IsWithinMinimum(float now)
    {
        return IsActive(now) && now < minUntil;
    }

    public bool Matches(AIAction action)
    {
        return action != null
            && action.actionset != null
            && Matches(action.actionset.Branch, action.destination);
    }

    public bool Matches(CharacterAiBranch candidateBranch, BuildableObject target)
    {
        if (branch == CharacterAiBranch.None || branch != candidateBranch)
        {
            return false;
        }

        int candidateTargetId = target != null ? target.GridId : -1;
        return targetGridId < 0 || candidateTargetId == targetGridId;
    }

    public void Begin(
        CharacterAiBranch newBranch,
        CharacterAiIntentionType newIntention,
        string newLabel,
        BuildableObject target,
        float minSeconds,
        float maxSeconds,
        bool canInterrupt,
        float now)
    {
        branch = newBranch;
        intention = newIntention;
        label = string.IsNullOrWhiteSpace(newLabel)
            ? CharacterAiUtilityText.GetBranchLabel(newBranch)
            : newLabel;
        targetGridId = target != null ? target.GridId : -1;
        startedAt = now;
        minUntil = now + Mathf.Max(0f, minSeconds);
        expiresAt = maxSeconds > 0f ? now + Mathf.Max(minSeconds, maxSeconds) : 0f;
        interruptible = canInterrupt;
        lastBreakReason = string.Empty;
    }

    public void Refresh(float minSeconds, float maxSeconds, float now)
    {
        if (!IsActive(now))
        {
            return;
        }

        minUntil = Mathf.Max(minUntil, now + Mathf.Max(0f, minSeconds));
        if (maxSeconds > 0f)
        {
            expiresAt = Mathf.Max(expiresAt, now + Mathf.Max(minSeconds, maxSeconds));
        }
    }

    public bool CanBreak(CharacterAiInterruptReason reason, float now)
    {
        if (!IsActive(now) || !IsWithinMinimum(now))
        {
            return true;
        }

        return reason == CharacterAiInterruptReason.Critical
            || reason == CharacterAiInterruptReason.DestinationInvalid
            || reason == CharacterAiInterruptReason.NoPath
            || reason == CharacterAiInterruptReason.FacilityUnavailable
            || reason == CharacterAiInterruptReason.MacroGoalChanged
            || reason == CharacterAiInterruptReason.MoodImpulseChanged
            || reason == CharacterAiInterruptReason.ManualReplan;
    }

    public void Break(CharacterAiInterruptReason reason, string detail)
    {
        lastBreakReason = string.IsNullOrWhiteSpace(detail)
            ? reason.ToString()
            : $"{reason}: {detail}";
        branch = CharacterAiBranch.None;
        intention = CharacterAiIntentionType.None;
        label = string.Empty;
        targetGridId = -1;
        minUntil = 0f;
        expiresAt = 0f;
        interruptible = true;
    }

    public float GetScoreBonus(float now, float maxBonus)
    {
        if (!IsActive(now) || maxBonus <= 0f)
        {
            return 0f;
        }

        float remainingRatio = expiresAt > now
            ? Mathf.Clamp01((expiresAt - now) / Mathf.Max(0.01f, expiresAt - startedAt))
            : 0.25f;
        return Mathf.Clamp(maxBonus * Mathf.Lerp(0.35f, 1f, remainingRatio), 0f, maxBonus);
    }

    public string ToDebugString(float now)
    {
        if (!IsActive(now))
        {
            return string.IsNullOrWhiteSpace(lastBreakReason)
                ? "의도 유지 없음"
                : $"의도 유지 없음 · 마지막 중단 {lastBreakReason}";
        }

        string target = targetGridId >= 0 ? $"대상 {targetGridId}" : "대상 없음";
        string minimum = IsWithinMinimum(now)
            ? $"최소 {Mathf.Max(0f, minUntil - now):0.0}s"
            : "중단 가능";
        string expiry = expiresAt > 0f ? $"만료 {Mathf.Max(0f, expiresAt - now):0.0}s" : "만료 없음";
        return $"{CharacterAiUtilityText.GetBranchLabel(branch)} · {label} · {target} · {minimum} · {expiry}";
    }
}

[CreateAssetMenu(menuName = "DungeonStory/AI/Naturalness Settings", order = 10)]
public sealed class CharacterAiNaturalnessSettingsSO : ScriptableObject
{
    private static CharacterAiNaturalnessSettingsSO defaults;

    [Header("Soft Lock")]
    [SerializeField, Min(0f)] private float softLockMinimumSeconds = 1.15f;
    [SerializeField, Min(0f)] private float softLockMaximumSeconds = 4.5f;
    [SerializeField, Range(0f, 0.5f)] private float softLockScoreBonus = 0.14f;

    [Header("Signals")]
    [SerializeField, Min(1f)] private float nearbyCharacterRadius = 4f;
    [SerializeField, Min(1f)] private float wildlifeThreatRadius = 7f;
    [SerializeField, Min(0f)] private float signalCacheSeconds = 0.25f;

    [Header("Micro Behavior")]
    [SerializeField, Range(0f, 1f)] private float queueWaitThreshold = 0.35f;
    [SerializeField, Range(0f, 1f)] private float shelterWeatherThreshold = 0.55f;
    [SerializeField, Range(0f, 1f)] private float stepAsideFailureThreshold = 0.35f;

    public static CharacterAiNaturalnessSettingsSO Defaults
    {
        get
        {
            if (defaults == null)
            {
                defaults = CreateInstance<CharacterAiNaturalnessSettingsSO>();
                defaults.hideFlags = HideFlags.HideAndDontSave;
            }

            return defaults;
        }
    }

    public float SoftLockMinimumSeconds => Mathf.Max(0f, softLockMinimumSeconds);
    public float SoftLockMaximumSeconds => Mathf.Max(SoftLockMinimumSeconds, softLockMaximumSeconds);
    public float SoftLockScoreBonus => Mathf.Clamp(softLockScoreBonus, 0f, 0.5f);
    public float NearbyCharacterRadius => Mathf.Max(1f, nearbyCharacterRadius);
    public float WildlifeThreatRadius => Mathf.Max(1f, wildlifeThreatRadius);
    public float SignalCacheSeconds => Mathf.Max(0f, signalCacheSeconds);
    public float QueueWaitThreshold => Mathf.Clamp01(queueWaitThreshold);
    public float ShelterWeatherThreshold => Mathf.Clamp01(shelterWeatherThreshold);
    public float StepAsideFailureThreshold => Mathf.Clamp01(stepAsideFailureThreshold);
}

public readonly struct CharacterAiWorldSignalSnapshot
{
    public CharacterAiWorldSignalSnapshot(
        TimeOfDay timeOfDay,
        GridCellAreaType areaType,
        float scheduleScore,
        float queuePressure,
        float socialOpportunity,
        float weatherPressure,
        float exteriorRisk,
        float foodStockPressure,
        float waterStockPressure,
        float pathConfidence,
        float recentFailurePressure,
        float recentMovementPressure,
        int nearbyCharacters,
        int nearbyWorkers,
        int nearbyVisitors,
        float nearbyWildlifeThreat)
    {
        TimeOfDay = timeOfDay;
        AreaType = areaType;
        ScheduleScore = Mathf.Clamp01(scheduleScore);
        QueuePressure = Mathf.Clamp01(queuePressure);
        SocialOpportunity = Mathf.Clamp01(socialOpportunity);
        WeatherPressure = Mathf.Clamp01(weatherPressure);
        ExteriorRisk = Mathf.Clamp01(exteriorRisk);
        FoodStockPressure = Mathf.Clamp01(foodStockPressure);
        WaterStockPressure = Mathf.Clamp01(waterStockPressure);
        PathConfidence = Mathf.Clamp01(pathConfidence);
        RecentFailurePressure = Mathf.Clamp01(recentFailurePressure);
        RecentMovementPressure = Mathf.Clamp01(recentMovementPressure);
        NearbyCharacters = Mathf.Max(0, nearbyCharacters);
        NearbyWorkers = Mathf.Max(0, nearbyWorkers);
        NearbyVisitors = Mathf.Max(0, nearbyVisitors);
        NearbyWildlifeThreat = Mathf.Clamp01(nearbyWildlifeThreat);
    }

    public static CharacterAiWorldSignalSnapshot Neutral => new CharacterAiWorldSignalSnapshot(
        TimeOfDay.None,
        GridCellAreaType.DungeonInterior,
        0.5f,
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        1f,
        0f,
        0f,
        0,
        0,
        0,
        0f);

    public TimeOfDay TimeOfDay { get; }
    public GridCellAreaType AreaType { get; }
    public float ScheduleScore { get; }
    public float QueuePressure { get; }
    public float SocialOpportunity { get; }
    public float WeatherPressure { get; }
    public float ExteriorRisk { get; }
    public float FoodStockPressure { get; }
    public float WaterStockPressure { get; }
    public float PathConfidence { get; }
    public float RecentFailurePressure { get; }
    public float RecentMovementPressure { get; }
    public int NearbyCharacters { get; }
    public int NearbyWorkers { get; }
    public int NearbyVisitors { get; }
    public float NearbyWildlifeThreat { get; }

    public bool IsExterior => AreaType == GridCellAreaType.ExteriorPath
        || AreaType == GridCellAreaType.DropZone
        || AreaType == GridCellAreaType.Entrance
        || AreaType == GridCellAreaType.BlockedExterior;

    public string ToCompactString()
    {
        return $"시간 {FormatTime(TimeOfDay)}"
            + $" · 구역 {FormatArea(AreaType)}"
            + $" · 대기 {QueuePressure * 100f:0}%"
            + $" · 경로 {PathConfidence * 100f:0}%"
            + $" · 날씨 {WeatherPressure * 100f:0}%"
            + $" · 주변 {NearbyCharacters}";
    }

    private static string FormatTime(TimeOfDay timeOfDay)
    {
        return timeOfDay switch
        {
            TimeOfDay.Morning => "아침",
            TimeOfDay.Noon => "낮",
            TimeOfDay.Evening => "저녁",
            TimeOfDay.Night => "밤",
            _ => "모름"
        };
    }

    private static string FormatArea(GridCellAreaType areaType)
    {
        return areaType switch
        {
            GridCellAreaType.DungeonInterior => "던전",
            GridCellAreaType.Entrance => "입구",
            GridCellAreaType.DropZone => "하차장",
            GridCellAreaType.ExteriorPath => "외부길",
            GridCellAreaType.BlockedExterior => "막힌 외부",
            _ => "모름"
        };
    }
}

public interface ICharacterAiWorldSignalQuery
{
    CharacterAiWorldSignalSnapshot Capture(
        CharacterActor actor,
        CharacterAiBranch branch,
        BuildableObject target = null,
        GridPathSearchResult searchResult = null);
}

public sealed class DefaultCharacterAiWorldSignalQuery : ICharacterAiWorldSignalQuery
{
    private readonly Dictionary<int, CachedSignal> cache = new Dictionary<int, CachedSignal>();
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ISurvivalFoodRuntime survivalFoodRuntime;
    private readonly IGameClock gameClock;

    public DefaultCharacterAiWorldSignalQuery(
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        ISurvivalFoodRuntime survivalFoodRuntime = null)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.survivalFoodRuntime = survivalFoodRuntime;
    }

    public CharacterAiWorldSignalSnapshot Capture(
        CharacterActor actor,
        CharacterAiBranch branch,
        BuildableObject target = null,
        GridPathSearchResult searchResult = null)
    {
        if (actor == null)
        {
            return CharacterAiWorldSignalSnapshot.Neutral;
        }

        Vector2Int actorPosition = actor.GetNowXY();
        int cacheKey = BuildCacheKey(actor, branch, actorPosition);
        float now = gameClock.Time;
        float cacheSeconds = CharacterAiNaturalnessSettingsSO.Defaults.SignalCacheSeconds;
        CharacterAiWorldSignalSnapshot snapshot;
        if (cache.TryGetValue(cacheKey, out CachedSignal cached)
            && now - cached.Time <= cacheSeconds)
        {
            snapshot = cached.Snapshot;
        }
        else
        {
            snapshot = CaptureBaseUncached(actor, branch, actorPosition);
            cache[cacheKey] = new CachedSignal(now, snapshot);
        }

        return target != null
            ? ApplyTargetSignals(snapshot, target, searchResult)
            : snapshot;
    }

    private int BuildCacheKey(CharacterActor actor, CharacterAiBranch branch, Vector2Int actorPosition)
    {
        unchecked
        {
            int hash = actor.GetInstanceID();
            hash = (hash * 397) ^ (int)branch;
            hash = (hash * 397) ^ actorPosition.GetHashCode();
            hash = (hash * 397) ^ worldRegistry.CharacterVersion;
            hash = (hash * 397) ^ worldRegistry.WildlifeVersion;
            hash = (hash * 397) ^ worldRegistry.BuildingVersion;
            return hash;
        }
    }

    private CharacterAiWorldSignalSnapshot CaptureBaseUncached(
        CharacterActor actor,
        CharacterAiBranch branch,
        Vector2Int actorPosition)
    {
        GridCellAreaType areaType = ResolveAreaType(actorPosition);
        TimeOfDay timeOfDay = ResolveTimeOfDay();
        float scheduleScore = ResolveScheduleScore(actor, branch, timeOfDay);
        float socialOpportunity = ResolveSocialOpportunity(
            actor,
            actorPosition,
            out int nearbyCharacters,
            out int nearbyWorkers,
            out int nearbyVisitors);
        float weatherPressure = ResolveWeatherPressure(areaType, timeOfDay);
        float exteriorRisk = ResolveExteriorRisk(areaType, timeOfDay, weatherPressure);
        float recentFailurePressure = actor.AiMemory != null ? actor.AiMemory.GetRecentFailurePressure() : 0f;
        float recentMovementPressure = actor.AiMemory != null ? actor.AiMemory.GetRecentMovementPressure() : 0f;
        float wildlifeThreat = ResolveWildlifeThreat(actorPosition);
        float foodStockPressure = 0f;
        float waterStockPressure = 0f;
        if (survivalFoodRuntime != null)
        {
            SurvivalFoodOverview survival = survivalFoodRuntime.GetOverview();
            foodStockPressure = CharacterAiSurvivalPressure.FromShortageDays(survival.ShortageDays);
            waterStockPressure = CharacterAiSurvivalPressure.FromShortageDays(survival.WaterShortageDays);
        }

        return new CharacterAiWorldSignalSnapshot(
            timeOfDay,
            areaType,
            scheduleScore,
            0f,
            socialOpportunity,
            weatherPressure,
            exteriorRisk,
            foodStockPressure,
            waterStockPressure,
            0.82f,
            recentFailurePressure,
            recentMovementPressure,
            nearbyCharacters,
            nearbyWorkers,
            nearbyVisitors,
            wildlifeThreat);
    }

    private static CharacterAiWorldSignalSnapshot ApplyTargetSignals(
        CharacterAiWorldSignalSnapshot baseSnapshot,
        BuildableObject target,
        GridPathSearchResult searchResult)
    {
        return new CharacterAiWorldSignalSnapshot(
            baseSnapshot.TimeOfDay,
            baseSnapshot.AreaType,
            baseSnapshot.ScheduleScore,
            ResolveQueuePressure(target),
            baseSnapshot.SocialOpportunity,
            baseSnapshot.WeatherPressure,
            baseSnapshot.ExteriorRisk,
            baseSnapshot.FoodStockPressure,
            baseSnapshot.WaterStockPressure,
            ResolvePathConfidence(target, searchResult),
            baseSnapshot.RecentFailurePressure,
            baseSnapshot.RecentMovementPressure,
            baseSnapshot.NearbyCharacters,
            baseSnapshot.NearbyWorkers,
            baseSnapshot.NearbyVisitors,
            baseSnapshot.NearbyWildlifeThreat);
    }

    private static class CharacterAiSurvivalPressure
    {
        public static float FromShortageDays(int days)
        {
            if (days <= 0) return 1f;
            if (days == 1) return 0.85f;
            if (days == 2) return 0.65f;
            if (days == 3) return 0.35f;
            return 0.1f;
        }
    }

    private GridCellAreaType ResolveAreaType(Vector2Int position)
    {
        if (!worldRegistry.TryGetGrid(out Grid grid))
        {
            return GridCellAreaType.DungeonInterior;
        }

        GridCell cell = grid.GetGridCell(position);
        return cell != null ? cell.AreaType : GridCellAreaType.DungeonInterior;
    }

    private TimeOfDay ResolveTimeOfDay()
    {
        return worldRegistry.TryGetGameData(out GameData data) && data.timeOfDay != null
            ? data.timeOfDay.Value
            : TimeOfDay.None;
    }

    private static float ResolveScheduleScore(CharacterActor actor, CharacterAiBranch branch, TimeOfDay timeOfDay)
    {
        bool isWorker = CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work);
        bool offDuty = isWorker && work.IsOffDuty;
        if (branch == CharacterAiBranch.DutyWork || branch == CharacterAiBranch.Work)
        {
            return offDuty ? 0.12f : 0.82f;
        }

        if (branch == CharacterAiBranch.LeisureVisit || branch == CharacterAiBranch.Rest)
        {
            return offDuty ? 0.78f : 0.35f;
        }

        if (branch == CharacterAiBranch.Eat)
        {
            return timeOfDay == TimeOfDay.Morning || timeOfDay == TimeOfDay.Evening ? 0.72f : 0.5f;
        }

        if (branch == CharacterAiBranch.Idle)
        {
            return offDuty ? 0.65f : 0.35f;
        }

        return 0.5f;
    }

    private static float ResolveQueuePressure(BuildableObject target)
    {
        if (target == null)
        {
            return 0f;
        }

        int capacity = Mathf.Max(1, target.EffectiveCapacity);
        if (capacity >= int.MaxValue)
        {
            return 0f;
        }

        int pressureCount = target.CurrentUserCount + target.ActiveVisitReservationCount;
        return Mathf.Clamp01((float)pressureCount / capacity);
    }

    private float ResolveSocialOpportunity(
        CharacterActor actor,
        Vector2Int actorPosition,
        out int nearbyCharacters,
        out int nearbyWorkers,
        out int nearbyVisitors)
    {
        nearbyCharacters = 0;
        nearbyWorkers = 0;
        nearbyVisitors = 0;
        float radius = CharacterAiNaturalnessSettingsSO.Defaults.NearbyCharacterRadius;
        int maxDistance = Mathf.CeilToInt(radius);
        IReadOnlyList<CharacterActor> registeredCharacters = worldRegistry.Characters;
        for (int i = 0; i < registeredCharacters.Count; i++)
        {
            CharacterActor candidate = registeredCharacters[i];
            if (candidate == null || candidate == actor || candidate.IsDead)
            {
                continue;
            }

            Vector2Int position = candidate.GetNowXY();
            int distance = Mathf.Abs(position.x - actorPosition.x) + Mathf.Abs(position.y - actorPosition.y);
            if (distance > maxDistance)
            {
                continue;
            }

            nearbyCharacters++;
            if (CharacterWorkRoleUtility.TryGetWork(candidate, out _))
            {
                nearbyWorkers++;
            }
            else
            {
                nearbyVisitors++;
            }
        }

        return Mathf.Clamp01(nearbyCharacters / 4f);
    }

    private static float ResolveWeatherPressure(GridCellAreaType areaType, TimeOfDay timeOfDay)
    {
        bool exterior = areaType == GridCellAreaType.ExteriorPath
            || areaType == GridCellAreaType.DropZone
            || areaType == GridCellAreaType.Entrance
            || areaType == GridCellAreaType.BlockedExterior;
        if (!exterior)
        {
            return 0f;
        }

        return timeOfDay == TimeOfDay.Night ? 0.42f : 0.18f;
    }

    private static float ResolveExteriorRisk(GridCellAreaType areaType, TimeOfDay timeOfDay, float weatherPressure)
    {
        bool exterior = areaType == GridCellAreaType.ExteriorPath
            || areaType == GridCellAreaType.DropZone
            || areaType == GridCellAreaType.Entrance
            || areaType == GridCellAreaType.BlockedExterior;
        if (!exterior)
        {
            return 0f;
        }

        float nightRisk = timeOfDay == TimeOfDay.Night ? 0.48f : 0.08f;
        return Mathf.Clamp01(nightRisk + weatherPressure * 0.35f);
    }

    private static float ResolvePathConfidence(BuildableObject target, GridPathSearchResult searchResult)
    {
        if (target == null || searchResult == null)
        {
            return searchResult != null ? 0.82f : 0.65f;
        }

        int distance = searchResult.GetMoveDistanceTo(target);
        if (distance == int.MaxValue)
        {
            return 0.45f;
        }

        return Mathf.Clamp01(1f - Mathf.Max(0, distance - 4) / 26f);
    }

    private float ResolveWildlifeThreat(Vector2Int actorPosition)
    {
        float radius = CharacterAiNaturalnessSettingsSO.Defaults.WildlifeThreatRadius;
        int maxDistance = Mathf.CeilToInt(radius);
        float threat = 0f;
        IReadOnlyList<WildlifeActor> registeredWildlife = worldRegistry.Wildlife;
        for (int i = 0; i < registeredWildlife.Count; i++)
        {
            WildlifeActor candidate = registeredWildlife[i];
            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            int distance = Mathf.Abs(candidate.GridPosition.x - actorPosition.x)
                + Mathf.Abs(candidate.GridPosition.y - actorPosition.y);
            if (distance > maxDistance)
            {
                continue;
            }

            float danger = candidate.IsDangerous ? 1f : 0.35f;
            threat = Mathf.Max(threat, danger * Mathf.Clamp01(1f - distance / radius));
        }

        return threat;
    }

    private readonly struct CachedSignal
    {
        public CachedSignal(float time, CharacterAiWorldSignalSnapshot snapshot)
        {
            Time = time;
            Snapshot = snapshot;
        }

        public float Time { get; }
        public CharacterAiWorldSignalSnapshot Snapshot { get; }
    }
}
