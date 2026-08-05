using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            ? CharacterAiUtilityText.GetBranchDisplayToken(newBranch)
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
            || reason == CharacterAiInterruptReason.SurvivalEmergency
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
                ? CharacterAiDiagnosticsTextQuery.Get(
                    "CharacterAI.Intent.Inactive")
                : CharacterAiDiagnosticsTextQuery.Get(
                    "CharacterAI.Intent.InactiveWithBreak",
                    lastBreakReason);
        }

        string target = targetGridId >= 0
            ? CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Intent.Target",
                targetGridId)
            : CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Intent.NoTarget");
        string minimum = IsWithinMinimum(now)
            ? CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Intent.Minimum",
                Mathf.Max(0f, minUntil - now))
            : CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Intent.Interruptible");
        string expiry = expiresAt > 0f
            ? CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Intent.Expiry",
                Mathf.Max(0f, expiresAt - now))
            : CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Intent.NoExpiry");
        return CharacterAiDiagnosticsTextQuery.Get(
            "CharacterAI.Intent.Active",
            CharacterAiUtilityText.GetBranchLabel(branch),
            CharacterAiUtilityText.ResolveDisplayToken(label),
            target,
            minimum,
            expiry);
    }
}

public static class CharacterAiNaturalnessSettingsResolver
{
    public static CharacterAiNaturalnessSettingsSO Require(CharacterActor actor)
    {
        return actor?.NaturalnessSettings
            ?? throw new InvalidOperationException(
                "Character AI naturalness settings were not injected from the root content catalog.");
    }
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

    public CharacterAiWorldSignalSnapshot WithScheduleScore(float scheduleScore)
    {
        return new CharacterAiWorldSignalSnapshot(
            TimeOfDay,
            AreaType,
            scheduleScore,
            QueuePressure,
            SocialOpportunity,
            WeatherPressure,
            ExteriorRisk,
            FoodStockPressure,
            WaterStockPressure,
            PathConfidence,
            RecentFailurePressure,
            RecentMovementPressure,
            NearbyCharacters,
            NearbyWorkers,
            NearbyVisitors,
            NearbyWildlifeThreat);
    }

    public string ToCompactString()
    {
        return CharacterAiDiagnosticsTextQuery.Get(
            "CharacterAI.WorldSignals.Compact",
            FormatTime(TimeOfDay),
            FormatArea(AreaType),
            QueuePressure * 100f,
            PathConfidence * 100f,
            WeatherPressure * 100f,
            NearbyCharacters);
    }

    private static string FormatTime(TimeOfDay timeOfDay)
    {
        return timeOfDay switch
        {
            TimeOfDay.Morning => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Time.Morning"),
            TimeOfDay.Noon => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Time.Noon"),
            TimeOfDay.Evening => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Time.Evening"),
            TimeOfDay.Night => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Time.Night"),
            _ => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Time.Unknown")
        };
    }

    private static string FormatArea(GridCellAreaType areaType)
    {
        return areaType switch
        {
            GridCellAreaType.DungeonInterior => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Area.DungeonInterior"),
            GridCellAreaType.Entrance => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Area.Entrance"),
            GridCellAreaType.DropZone => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Area.DropZone"),
            GridCellAreaType.ExteriorPath => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Area.ExteriorPath"),
            GridCellAreaType.BlockedExterior => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Area.BlockedExterior"),
            _ => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Area.Unknown")
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

public static class CharacterAiScheduleUtility
{
    public static float Resolve(
        CharacterActor actor,
        CharacterAiBranch branch,
        TimeOfDay timeOfDay)
    {
        bool isWorker = CharacterWorkRoleUtility.TryGetWork(
            actor,
            out AbilityWork work);
        bool offDuty = isWorker && work.IsOffDuty;
        return Resolve(isWorker, offDuty, branch, timeOfDay);
    }

    public static float Resolve(
        bool isWorker,
        bool offDuty,
        CharacterAiBranch branch,
        TimeOfDay timeOfDay)
    {
        if (branch == CharacterAiBranch.DutyWork
            || branch == CharacterAiBranch.Work)
        {
            return offDuty ? 0.12f : 0.82f;
        }

        if (branch == CharacterAiBranch.LeisureVisit
            || branch == CharacterAiBranch.Rest)
        {
            return offDuty ? 0.78f : 0.35f;
        }

        if (branch == CharacterAiBranch.Eat)
        {
            return timeOfDay == TimeOfDay.Morning
                || timeOfDay == TimeOfDay.Evening
                    ? 0.72f
                    : 0.5f;
        }

        if (branch == CharacterAiBranch.Idle)
        {
            return offDuty ? 0.65f : 0.35f;
        }

        return 0.5f;
    }
}

public sealed class DefaultCharacterAiWorldSignalQuery : ICharacterAiWorldSignalQuery
{
    private const int SpatialBucketWidth = 8;
    private const int WildlifeSpatialUpdatesPerFrame = 8;
    private const int MaxNearbyCharacterSamples = 12;
    private readonly Dictionary<int, CachedSignal> cache = new Dictionary<int, CachedSignal>();
    private readonly Dictionary<long, List<CharacterSpatialEntry>> characterBuckets =
        new Dictionary<long, List<CharacterSpatialEntry>>();
    private readonly Dictionary<long, List<WildlifeSpatialEntry>> wildlifeBuckets =
        new Dictionary<long, List<WildlifeSpatialEntry>>();
    private readonly Dictionary<int, CharacterSpatialEntry> characterEntries =
        new Dictionary<int, CharacterSpatialEntry>();
    private readonly Dictionary<int, WildlifeSpatialEntry> wildlifeEntries =
        new Dictionary<int, WildlifeSpatialEntry>();
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ISurvivalFoodQuery survivalFoodRuntime;
    private readonly ISurvivalEnvironmentQuery survivalEnvironment;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private readonly IGameClock gameClock;
    private int lastSpatialRefreshFrame = int.MinValue;
    private int indexedCharacterVersion = -1;
    private int indexedWildlifeVersion = -1;
    private int characterMembershipEpoch;
    private int wildlifeMembershipEpoch;
    private int wildlifeRefreshCursor;

    public DefaultCharacterAiWorldSignalQuery(
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        ISurvivalFoodQuery survivalFoodRuntime,
        ISurvivalEnvironmentQuery survivalEnvironment,
        ICharacterAiPerformanceRecorder performanceRecorder)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.survivalFoodRuntime = survivalFoodRuntime;
        this.survivalEnvironment = survivalEnvironment;
        this.performanceRecorder = performanceRecorder;
    }

    public CharacterAiWorldSignalSnapshot Capture(
        CharacterActor actor,
        CharacterAiBranch branch,
        BuildableObject target = null,
        GridPathSearchResult searchResult = null)
    {
        long started = performanceRecorder?.DetailedCollectionEnabled == true
            ? Stopwatch.GetTimestamp()
            : 0L;
        try
        {
            return CaptureCore(actor, branch, target, searchResult);
        }
        finally
        {
            if (started != 0L)
            {
                performanceRecorder.Record(
                    AiPerformanceCategory.WorldSignal,
                    (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
            }
        }
    }

    private CharacterAiWorldSignalSnapshot CaptureCore(
        CharacterActor actor,
        CharacterAiBranch branch,
        BuildableObject target,
        GridPathSearchResult searchResult)
    {
        if (actor == null)
        {
            return CharacterAiWorldSignalSnapshot.Neutral;
        }

        Vector2Int actorPosition = actor.GetNowXY();
        int cacheKey = BuildCacheKey(actor, actorPosition);
        float now = gameClock.Time;
        float cacheSeconds = CharacterAiNaturalnessSettingsResolver.Require(actor).SignalCacheSeconds;
        CharacterAiWorldSignalSnapshot snapshot;
        if (cache.TryGetValue(cacheKey, out CachedSignal cached)
            && now - cached.Time <= cacheSeconds)
        {
            snapshot = cached.Snapshot;
        }
        else
        {
            snapshot = CaptureBaseUncached(actor, actorPosition);
            cache[cacheKey] = new CachedSignal(now, snapshot);
        }

        snapshot = snapshot.WithScheduleScore(
            CharacterAiScheduleUtility.Resolve(
                actor,
                branch,
                snapshot.TimeOfDay));
        return target != null
            ? ApplyTargetSignals(snapshot, target, searchResult)
            : snapshot;
    }

    private int BuildCacheKey(CharacterActor actor, Vector2Int actorPosition)
    {
        unchecked
        {
            int hash = actor.GetInstanceID();
            hash = (hash * 397) ^ actorPosition.GetHashCode();
            hash = (hash * 397) ^ worldRegistry.CharacterVersion;
            hash = (hash * 397) ^ worldRegistry.WildlifeVersion;
            hash = (hash * 397) ^ worldRegistry.BuildingVersion;
            return hash;
        }
    }

    private CharacterAiWorldSignalSnapshot CaptureBaseUncached(
        CharacterActor actor,
        Vector2Int actorPosition)
    {
        GridCellAreaType areaType = ResolveAreaType(actorPosition);
        TimeOfDay timeOfDay = ResolveTimeOfDay();
        long proximityStarted = StartDetailedTiming();
        float socialOpportunity = ResolveSocialOpportunity(
            actor,
            actorPosition,
            out int nearbyCharacters,
            out int nearbyWorkers,
            out int nearbyVisitors);
        float wildlifeThreat = ResolveWildlifeThreat(actor, actorPosition);
        RecordDetailedTiming(
            AiPerformanceCategory.WorldSignalProximity,
            proximityStarted);

        long environmentStarted = StartDetailedTiming();
        SurvivalEnvironmentSnapshot environment = survivalEnvironment != null
            ? survivalEnvironment.GetEnvironmentSnapshot()
            : default;
        float weatherPressure = ResolveWeatherPressure(
            areaType,
            timeOfDay,
            environment,
            survivalEnvironment != null);
        float exteriorRisk = ResolveExteriorRisk(
            areaType,
            timeOfDay,
            weatherPressure,
            environment,
            survivalEnvironment != null);
        float recentFailurePressure = actor.AiMemory != null ? actor.AiMemory.GetRecentFailurePressure() : 0f;
        float recentMovementPressure = actor.AiMemory != null ? actor.AiMemory.GetRecentMovementPressure() : 0f;
        float foodStockPressure = 0f;
        float waterStockPressure = 0f;
        if (survivalFoodRuntime != null)
        {
            SurvivalFoodOverview survival = survivalFoodRuntime.GetOverview();
            foodStockPressure = CharacterAiSurvivalPressure.FromShortageDays(survival.ShortageDays);
            waterStockPressure = CharacterAiSurvivalPressure.FromShortageDays(survival.WaterShortageDays);
        }
        RecordDetailedTiming(
            AiPerformanceCategory.WorldSignalEnvironment,
            environmentStarted);

        return new CharacterAiWorldSignalSnapshot(
            timeOfDay,
            areaType,
            0.5f,
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
        return worldRegistry.TryGetSessionState(out GameSessionState data) && data.timeOfDay != null
            ? data.timeOfDay.Value
            : TimeOfDay.None;
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
        float radius = CharacterAiNaturalnessSettingsResolver.Require(actor).NearbyCharacterRadius;
        int maxDistance = Mathf.CeilToInt(radius);
        RefreshSpatialBucketsIfNeeded(actor);
        int centerBucket = FloorDiv(actorPosition.x, SpatialBucketWidth);
        int bucketRadius = Mathf.CeilToInt(radius / SpatialBucketWidth);
        for (int bucketOffset = -bucketRadius; bucketOffset <= bucketRadius; bucketOffset++)
        {
            long key = BuildSpatialKey(actorPosition.y, centerBucket + bucketOffset);
            if (!characterBuckets.TryGetValue(key, out List<CharacterSpatialEntry> bucket))
            {
                continue;
            }

            for (int index = 0; index < bucket.Count; index++)
            {
                CharacterSpatialEntry candidate = bucket[index];
                if (candidate.Actor == null
                    || candidate.Actor == actor
                    || candidate.Actor.IsDead
                    || candidate.MembershipEpoch != characterMembershipEpoch)
                {
                    continue;
                }

                int distance = Mathf.Abs(candidate.Position.x - actorPosition.x)
                    + Mathf.Abs(candidate.Position.y - actorPosition.y);
                if (distance > maxDistance)
                {
                    continue;
                }

                nearbyCharacters++;
                if (candidate.IsWorker)
                {
                    nearbyWorkers++;
                }
                else
                {
                    nearbyVisitors++;
                }

                if (nearbyCharacters >= MaxNearbyCharacterSamples)
                {
                    return 1f;
                }
            }
        }

        return Mathf.Clamp01(nearbyCharacters / 4f);
    }

    private static float ResolveWeatherPressure(
        GridCellAreaType areaType,
        TimeOfDay timeOfDay,
        SurvivalEnvironmentSnapshot environment,
        bool hasEnvironment)
    {
        bool exterior = areaType == GridCellAreaType.ExteriorPath
            || areaType == GridCellAreaType.DropZone
            || areaType == GridCellAreaType.Entrance
            || areaType == GridCellAreaType.BlockedExterior;
        if (!exterior)
        {
            return 0f;
        }

        if (hasEnvironment)
        {
            float temperaturePressure = Mathf.Clamp01(
                Mathf.Abs(environment.OutdoorTemperature - 20f) / 24f);
            return Mathf.Clamp01(
                environment.WeatherPressure01 * 0.8f
                + temperaturePressure * 0.2f);
        }

        return timeOfDay == TimeOfDay.Night ? 0.42f : 0.18f;
    }

    private static float ResolveExteriorRisk(
        GridCellAreaType areaType,
        TimeOfDay timeOfDay,
        float weatherPressure,
        SurvivalEnvironmentSnapshot environment,
        bool hasEnvironment)
    {
        bool exterior = areaType == GridCellAreaType.ExteriorPath
            || areaType == GridCellAreaType.DropZone
            || areaType == GridCellAreaType.Entrance
            || areaType == GridCellAreaType.BlockedExterior;
        if (!exterior)
        {
            return 0f;
        }

        float nightRisk = hasEnvironment
            ? environment.ExteriorNightDanger / 100f
            : 0.48f;
        if (timeOfDay != TimeOfDay.Night)
        {
            nightRisk *= 0.2f;
        }

        float healthRisk = hasEnvironment
            ? (environment.SanitationRisk + environment.DiseaseRisk) / 400f
            : 0f;
        return Mathf.Clamp01(nightRisk + weatherPressure * 0.35f + healthRisk);
    }

    private static float ResolvePathConfidence(BuildableObject target, GridPathSearchResult searchResult)
    {
        if (target == null || searchResult == null)
        {
            return searchResult != null ? 0.82f : 0.65f;
        }

        int travelCost = searchResult.GetMoveCostTo(target);
        if (travelCost == int.MaxValue)
        {
            return 0.45f;
        }

        float distance = travelCost
            / (float)DefaultGridTraversalCostPolicy.DryWalkCost;
        return Mathf.Clamp01(1f - Mathf.Max(0, distance - 4) / 26f);
    }

    private float ResolveWildlifeThreat(
        CharacterActor actor,
        Vector2Int actorPosition)
    {
        float radius = CharacterAiNaturalnessSettingsResolver.Require(actor).WildlifeThreatRadius;
        int maxDistance = Mathf.CeilToInt(radius);
        float threat = 0f;
        RefreshSpatialBucketsIfNeeded(null);
        int centerBucket = FloorDiv(actorPosition.x, SpatialBucketWidth);
        int bucketRadius = Mathf.CeilToInt(radius / SpatialBucketWidth);
        for (int bucketOffset = -bucketRadius; bucketOffset <= bucketRadius; bucketOffset++)
        {
            long key = BuildSpatialKey(actorPosition.y, centerBucket + bucketOffset);
            if (!wildlifeBuckets.TryGetValue(key, out List<WildlifeSpatialEntry> bucket))
            {
                continue;
            }

            for (int index = 0; index < bucket.Count; index++)
            {
                WildlifeSpatialEntry candidate = bucket[index];
                if (candidate.Actor == null
                    || !candidate.Actor.IsAlive
                    || candidate.MembershipEpoch != wildlifeMembershipEpoch)
                {
                    continue;
                }

                int distance = Mathf.Abs(candidate.Position.x - actorPosition.x)
                    + Mathf.Abs(candidate.Position.y - actorPosition.y);
                if (distance > maxDistance)
                {
                    continue;
                }

                float danger = candidate.IsDangerous ? 1f : 0.35f;
                threat = Mathf.Max(
                    threat,
                    danger * Mathf.Clamp01(1f - distance / radius));
            }
        }

        return threat;
    }

    private void RefreshSpatialBucketsIfNeeded(CharacterActor priorityActor)
    {
        long started = StartDetailedTiming();
        try
        {
            BeginMembershipRefreshIfNeeded();
            UpdateCharacterEntry(priorityActor);

            int frame = gameClock.FrameCount;
            if (lastSpatialRefreshFrame == frame)
            {
                return;
            }

            lastSpatialRefreshFrame = frame;

            IReadOnlyList<CharacterActor> characters = worldRegistry.Characters;
            if (characters.Count == 0)
            {
                RemoveStaleCharacterEntries();
            }

            IReadOnlyList<WildlifeActor> wildlife = worldRegistry.Wildlife;
            if (wildlife.Count == 0)
            {
                RemoveStaleWildlifeEntries();
            }
            int wildlifeUpdates = Mathf.Min(
                WildlifeSpatialUpdatesPerFrame,
                wildlife.Count);
            for (int count = 0; count < wildlifeUpdates; count++)
            {
                if (wildlifeRefreshCursor >= wildlife.Count)
                {
                    wildlifeRefreshCursor = 0;
                    RemoveStaleWildlifeEntries();
                }

                UpdateWildlifeEntry(wildlife[wildlifeRefreshCursor++]);
            }
        }
        finally
        {
            RecordDetailedTiming(
                AiPerformanceCategory.WorldSignalSpatialIndex,
                started);
        }
    }

    private long StartDetailedTiming()
    {
        return performanceRecorder?.DetailedCollectionEnabled == true
            ? Stopwatch.GetTimestamp()
            : 0L;
    }

    private void RecordDetailedTiming(
        AiPerformanceCategory category,
        long started)
    {
        if (started == 0L)
        {
            return;
        }

        performanceRecorder.Record(
            category,
            (Stopwatch.GetTimestamp() - started)
            * 1000.0
            / Stopwatch.Frequency);
    }

    private void BeginMembershipRefreshIfNeeded()
    {
        if (indexedCharacterVersion != worldRegistry.CharacterVersion)
        {
            indexedCharacterVersion = worldRegistry.CharacterVersion;
            characterMembershipEpoch++;
        }

        if (indexedWildlifeVersion != worldRegistry.WildlifeVersion)
        {
            indexedWildlifeVersion = worldRegistry.WildlifeVersion;
            wildlifeMembershipEpoch++;
            wildlifeRefreshCursor = 0;
        }
    }

    private void UpdateCharacterEntry(CharacterActor character)
    {
        if (character == null)
        {
            return;
        }

        int id = character.GetInstanceID();
        if (character.IsDead)
        {
            RemoveCharacterEntry(id);
            return;
        }

        Vector2Int position = character.GetNowXY();
        long bucketKey = BuildSpatialKey(
            position.y,
            FloorDiv(position.x, SpatialBucketWidth));
        if (!characterEntries.TryGetValue(id, out CharacterSpatialEntry entry))
        {
            entry = new CharacterSpatialEntry(character);
            characterEntries.Add(id, entry);
            AddCharacterToBucket(bucketKey, entry);
        }
        else if (entry.BucketKey != bucketKey)
        {
            RemoveCharacterFromBucket(entry);
            AddCharacterToBucket(bucketKey, entry);
        }

        entry.Position = position;
        entry.IsWorker = CharacterWorkRoleUtility.TryGetWork(character, out _);
        entry.BucketKey = bucketKey;
        entry.MembershipEpoch = characterMembershipEpoch;
    }

    private void UpdateWildlifeEntry(WildlifeActor animal)
    {
        if (animal == null)
        {
            return;
        }

        int id = animal.GetInstanceID();
        if (!animal.IsAlive)
        {
            RemoveWildlifeEntry(id);
            return;
        }

        Vector2Int position = animal.GridPosition;
        long bucketKey = BuildSpatialKey(
            position.y,
            FloorDiv(position.x, SpatialBucketWidth));
        if (!wildlifeEntries.TryGetValue(id, out WildlifeSpatialEntry entry))
        {
            entry = new WildlifeSpatialEntry(animal);
            wildlifeEntries.Add(id, entry);
            AddWildlifeToBucket(bucketKey, entry);
        }
        else if (entry.BucketKey != bucketKey)
        {
            RemoveWildlifeFromBucket(entry);
            AddWildlifeToBucket(bucketKey, entry);
        }

        entry.Position = position;
        entry.IsDangerous = animal.IsDangerous;
        entry.BucketKey = bucketKey;
        entry.MembershipEpoch = wildlifeMembershipEpoch;
    }

    private void RemoveStaleCharacterEntries()
    {
        using Dictionary<int, CharacterSpatialEntry>.Enumerator enumerator =
            characterEntries.GetEnumerator();
        List<int> staleIds = null;
        while (enumerator.MoveNext())
        {
            CharacterSpatialEntry entry = enumerator.Current.Value;
            if (entry.Actor == null
                || entry.Actor.IsDead
                || entry.MembershipEpoch != characterMembershipEpoch)
            {
                staleIds ??= new List<int>();
                staleIds.Add(enumerator.Current.Key);
            }
        }

        if (staleIds == null)
        {
            return;
        }

        for (int index = 0; index < staleIds.Count; index++)
        {
            RemoveCharacterEntry(staleIds[index]);
        }
    }

    private void RemoveStaleWildlifeEntries()
    {
        using Dictionary<int, WildlifeSpatialEntry>.Enumerator enumerator =
            wildlifeEntries.GetEnumerator();
        List<int> staleIds = null;
        while (enumerator.MoveNext())
        {
            WildlifeSpatialEntry entry = enumerator.Current.Value;
            if (entry.Actor == null
                || !entry.Actor.IsAlive
                || entry.MembershipEpoch != wildlifeMembershipEpoch)
            {
                staleIds ??= new List<int>();
                staleIds.Add(enumerator.Current.Key);
            }
        }

        if (staleIds == null)
        {
            return;
        }

        for (int index = 0; index < staleIds.Count; index++)
        {
            RemoveWildlifeEntry(staleIds[index]);
        }
    }

    private void RemoveCharacterEntry(int id)
    {
        if (!characterEntries.TryGetValue(id, out CharacterSpatialEntry entry))
        {
            return;
        }

        RemoveCharacterFromBucket(entry);
        characterEntries.Remove(id);
    }

    private void RemoveWildlifeEntry(int id)
    {
        if (!wildlifeEntries.TryGetValue(id, out WildlifeSpatialEntry entry))
        {
            return;
        }

        RemoveWildlifeFromBucket(entry);
        wildlifeEntries.Remove(id);
    }

    private void AddCharacterToBucket(
        long key,
        CharacterSpatialEntry entry)
    {
        if (!characterBuckets.TryGetValue(
                key,
                out List<CharacterSpatialEntry> bucket))
        {
            bucket = new List<CharacterSpatialEntry>(8);
            characterBuckets[key] = bucket;
        }

        entry.BucketKey = key;
        entry.BucketIndex = bucket.Count;
        bucket.Add(entry);
    }

    private void RemoveCharacterFromBucket(
        CharacterSpatialEntry entry)
    {
        if (!characterBuckets.TryGetValue(
                entry.BucketKey,
                out List<CharacterSpatialEntry> bucket))
        {
            entry.BucketIndex = -1;
            return;
        }

        int index = entry.BucketIndex;
        if (index < 0
            || index >= bucket.Count
            || !ReferenceEquals(bucket[index], entry))
        {
            index = bucket.IndexOf(entry);
            if (index < 0)
            {
                entry.BucketIndex = -1;
                return;
            }
        }

        int lastIndex = bucket.Count - 1;
        if (index != lastIndex)
        {
            CharacterSpatialEntry moved = bucket[lastIndex];
            bucket[index] = moved;
            moved.BucketIndex = index;
        }

        bucket.RemoveAt(lastIndex);
        entry.BucketIndex = -1;
        if (bucket.Count == 0)
        {
            characterBuckets.Remove(entry.BucketKey);
        }
    }

    private void AddWildlifeToBucket(
        long key,
        WildlifeSpatialEntry entry)
    {
        if (!wildlifeBuckets.TryGetValue(
                key,
                out List<WildlifeSpatialEntry> bucket))
        {
            bucket = new List<WildlifeSpatialEntry>(8);
            wildlifeBuckets[key] = bucket;
        }

        entry.BucketKey = key;
        entry.BucketIndex = bucket.Count;
        bucket.Add(entry);
    }

    private void RemoveWildlifeFromBucket(
        WildlifeSpatialEntry entry)
    {
        if (!wildlifeBuckets.TryGetValue(
                entry.BucketKey,
                out List<WildlifeSpatialEntry> bucket))
        {
            entry.BucketIndex = -1;
            return;
        }

        int index = entry.BucketIndex;
        if (index < 0
            || index >= bucket.Count
            || !ReferenceEquals(bucket[index], entry))
        {
            index = bucket.IndexOf(entry);
            if (index < 0)
            {
                entry.BucketIndex = -1;
                return;
            }
        }

        int lastIndex = bucket.Count - 1;
        if (index != lastIndex)
        {
            WildlifeSpatialEntry moved = bucket[lastIndex];
            bucket[index] = moved;
            moved.BucketIndex = index;
        }

        bucket.RemoveAt(lastIndex);
        entry.BucketIndex = -1;
        if (bucket.Count == 0)
        {
            wildlifeBuckets.Remove(entry.BucketKey);
        }
    }

    private static long BuildSpatialKey(int floor, int bucketX)
    {
        return ((long)floor << 32) ^ (uint)bucketX;
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
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

    private sealed class CharacterSpatialEntry
    {
        public CharacterSpatialEntry(CharacterActor actor)
        {
            Actor = actor;
        }

        public CharacterActor Actor { get; }
        public Vector2Int Position { get; set; }
        public bool IsWorker { get; set; }
        public long BucketKey { get; set; }
        public int BucketIndex { get; set; } = -1;
        public int MembershipEpoch { get; set; }
    }

    private sealed class WildlifeSpatialEntry
    {
        public WildlifeSpatialEntry(WildlifeActor actor)
        {
            Actor = actor;
        }

        public WildlifeActor Actor { get; }
        public Vector2Int Position { get; set; }
        public bool IsDangerous { get; set; }
        public long BucketKey { get; set; }
        public int BucketIndex { get; set; } = -1;
        public int MembershipEpoch { get; set; }
    }
}
