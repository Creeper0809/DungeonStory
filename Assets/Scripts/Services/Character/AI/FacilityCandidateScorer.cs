using System.Collections.Generic;
using System.Diagnostics;
using DungeonStory.Foundation;
using UnityEngine;

public static class FacilityCandidateScorer
{
    private const int MaximumFullyScoredCandidates = 20;

    [System.ThreadStatic]
    private static BuildableObject[] scoringShortlist;

    [System.ThreadStatic]
    private static int[] scoringShortlistCosts;

    private static readonly FacilityRole[] ScoredRoles =
    {
        FacilityRole.Meal,
        FacilityRole.Purchase,
        FacilityRole.Rest,
        FacilityRole.Training,
        FacilityRole.Research,
        FacilityRole.Mana,
        FacilityRole.Logistics,
        FacilityRole.Toilet,
        FacilityRole.Hygiene
    };

    public static List<BuildableObject> GetCandidates(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        FacilityRole role)
    {
        List<BuildableObject> result = new List<BuildableObject>();
        IEnumerable<BuildableObject> source = GetCandidateSource(actor, searchResult, role);
        FacilityScoringContext scoringContext = FacilityScoringContext.RequireFromActor(actor);

        foreach (BuildableObject building in source)
        {
            if (searchResult != null && !searchResult.ContainsVisitableOccupant(building))
            {
                continue;
            }

            if (IsCandidate(actor, building, role, scoringContext, out _))
            {
                result.Add(building);
            }
        }

        return result;
    }

    public static BuildableObject SelectBest(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> candidates,
        FacilityRole role,
        GridPathSearchResult searchResult,
        FacilityScoringContext scoringContext)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        BuildableObject bestBuilding = null;
        CharacterAiUtilityBreakdown bestBreakdown = null;
        float bestScore = float.MinValue;
        int bestId = int.MaxValue;
        foreach (BuildableObject building in candidates)
        {
            if (building == null)
            {
                continue;
            }

            float score = ScoreCandidateWithBreakdown(
                actor,
                building,
                role,
                searchResult,
                scoringContext,
                out CharacterAiUtilityBreakdown breakdown);
            if (bestBuilding == null
                || score > bestScore
                || (Mathf.Approximately(score, bestScore) && building.id < bestId))
            {
                bestBuilding = building;
                bestBreakdown = breakdown;
                bestScore = score;
                bestId = building.id;
            }
        }

        RecordFacilityBreakdown(actor, bestBreakdown, bestScore);
        return bestBuilding;
    }

    public static bool HasCandidate(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        FacilityRole role)
    {
        return HasCandidate(actor, searchResult, role, null);
    }

    public static bool HasCandidate(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        FacilityRole role,
        System.Predicate<BuildableObject> additionalPredicate)
    {
        if (role == FacilityRole.None)
        {
            return false;
        }

        if (searchResult == null
            && additionalPredicate == null
            && actor != null
            && actor.Brain != null
            && actor.Brain.TryGetRuntimeGrid(out Grid activeGrid))
        {
            IFacilityCandidateCache cache =
                RequireFacilityCandidateCache(actor);
            FacilityRole availableRoles = cache.GetAvailableRoles(activeGrid);
            return (availableRoles & role) != 0
                || cache.HasPendingIndexBuild;
        }

        ICharacterAiPerformanceRecorder recorder = actor?.Brain?.PerformanceRecorder;
        long started = recorder?.DetailedCollectionEnabled == true
            ? Stopwatch.GetTimestamp()
            : 0L;
        IReadOnlyList<BuildableObject> source = null;
        int shortlistCount = 0;
        try
        {
            FacilityScoringContext scoringContext =
                FacilityScoringContext.RequireFromActor(actor);
            source = GetCandidateSource(actor, searchResult, role);
            shortlistCount = BuildScoringShortlist(actor, source);
            for (int sourceIndex = 0; sourceIndex < shortlistCount; sourceIndex++)
            {
                BuildableObject building = source.Count <= MaximumFullyScoredCandidates
                    ? source[sourceIndex]
                    : scoringShortlist[sourceIndex];
                if (IsReachableCandidate(
                        actor,
                        searchResult,
                        building,
                        role,
                        scoringContext)
                    && (additionalPredicate == null
                        || additionalPredicate(building)))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            ClearScoringShortlist(source, shortlistCount);
            if (started != 0L)
            {
                recorder.Record(
                    AiPerformanceCategory.FacilityAvailability,
                    (Stopwatch.GetTimestamp() - started)
                    * 1000.0
                    / Stopwatch.Frequency);
            }
        }
    }

    public static bool TrySelectBestIncremental(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        FacilityRole role,
        FacilityScoringContext scoringContext,
        out BuildableObject bestBuilding,
        out bool pending)
    {
        bestBuilding = null;
        pending = false;
        if (role == FacilityRole.None)
        {
            return false;
        }

        if (searchResult != null)
        {
            return TrySelectBest(
                actor,
                searchResult,
                role,
                scoringContext,
                out bestBuilding);
        }

        if (actor == null
            || actor.Brain == null
            || !actor.Brain.TryGetRuntimeGrid(out Grid grid))
        {
            return false;
        }

        double sliceMilliseconds = actor.FrameWorkBudget?.GetSliceMilliseconds(
                DynamicFrameWorkDomain.AiDecision,
                0.02,
                0.15)
            ?? 0.15;
        IFacilityCandidateCache cache =
            RequireFacilityCandidateCache(actor);
        if (!cache.TryGetNearestCandidates(
                grid,
                role,
                actor.GetNowXY(),
                MaximumFullyScoredCandidates,
                sliceMilliseconds,
                out IReadOnlyList<BuildableObject> candidates))
        {
            pending = true;
            return false;
        }

        return TrySelectBestFromCandidates(
            actor,
            candidates,
            role,
            scoringContext,
            out bestBuilding);
    }

    public static bool TrySelectBest(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        FacilityRole role,
        FacilityScoringContext scoringContext,
        out BuildableObject bestBuilding)
    {
        bestBuilding = null;
        if (role == FacilityRole.None)
        {
            return false;
        }

        ICharacterAiPerformanceRecorder recorder =
            actor?.Brain?.PerformanceRecorder;
        long sourceStarted = recorder?.DetailedCollectionEnabled == true
            ? Stopwatch.GetTimestamp()
            : 0L;
        IReadOnlyList<BuildableObject> source =
            GetCandidateSource(actor, searchResult, role);
        if (sourceStarted != 0L)
        {
            recorder.Record(
                AiPerformanceCategory.FacilityCandidateSource,
                (Stopwatch.GetTimestamp() - sourceStarted)
                * 1000.0
                / Stopwatch.Frequency);
        }

        float bestScore = float.MinValue;
        CharacterAiUtilityBreakdown bestBreakdown = null;
        int bestId = int.MaxValue;
        long loopStarted = recorder?.DetailedCollectionEnabled == true
            ? Stopwatch.GetTimestamp()
            : 0L;
        int shortlistCount = BuildScoringShortlist(actor, source);
        for (int sourceIndex = 0; sourceIndex < shortlistCount; sourceIndex++)
        {
            BuildableObject building = source.Count <= MaximumFullyScoredCandidates
                ? source[sourceIndex]
                : scoringShortlist[sourceIndex];
            if (building == null
                || (searchResult != null
                    && !searchResult.ContainsVisitableOccupant(building)))
            {
                continue;
            }

            float score = ScoreCandidateWithBreakdown(
                actor,
                building,
                role,
                searchResult,
                scoringContext,
                out CharacterAiUtilityBreakdown breakdown);
            if (score <= 0f)
            {
                continue;
            }

            if (bestBuilding == null
                || score > bestScore
                || (Mathf.Approximately(score, bestScore) && building.id < bestId))
            {
                bestBuilding = building;
                bestBreakdown = breakdown;
                bestScore = score;
                bestId = building.id;
            }
        }
        ClearScoringShortlist(source, shortlistCount);
        if (loopStarted != 0L)
        {
            recorder.Record(
                AiPerformanceCategory.FacilityCandidateLoop,
                (Stopwatch.GetTimestamp() - loopStarted)
                * 1000.0
                / Stopwatch.Frequency);
        }

        if (bestBuilding != null)
        {
            RecordFacilityBreakdown(actor, bestBreakdown, bestScore);
        }

        return bestBuilding != null;
    }

    private static bool TrySelectBestFromCandidates(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> candidates,
        FacilityRole role,
        FacilityScoringContext scoringContext,
        out BuildableObject bestBuilding)
    {
        bestBuilding = null;
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        float bestScore = float.MinValue;
        int bestId = int.MaxValue;
        CharacterAiUtilityBreakdown bestBreakdown = null;
        for (int index = 0; index < candidates.Count; index++)
        {
            BuildableObject building = candidates[index];
            if (building == null || building.isDestroy)
            {
                continue;
            }

            float score = ScoreCandidateWithBreakdown(
                actor,
                building,
                role,
                null,
                scoringContext,
                out CharacterAiUtilityBreakdown breakdown);
            if (score <= 0f)
            {
                continue;
            }

            if (bestBuilding == null
                || score > bestScore
                || (Mathf.Approximately(score, bestScore)
                    && building.id < bestId))
            {
                bestBuilding = building;
                bestBreakdown = breakdown;
                bestScore = score;
                bestId = building.id;
            }
        }

        if (bestBuilding != null)
        {
            RecordFacilityBreakdown(actor, bestBreakdown, bestScore);
        }

        return bestBuilding != null;
    }

    public static bool IsCandidate(
        CharacterActor actor,
        BuildableObject building,
        FacilityRole role,
        out string rejectReason)
    {
        return IsCandidate(
            actor,
            building,
            role,
            FacilityScoringContext.RequireFromActor(actor),
            out rejectReason);
    }

    public static bool IsCandidate(
        CharacterActor actor,
        BuildableObject building,
        FacilityRole role,
        FacilityScoringContext scoringContext,
        out string rejectReason)
    {
        rejectReason = string.Empty;
        if (building == null)
        {
            rejectReason = "시설 없음";
            return false;
        }

        if (!building.SupportsFacilityRole(role))
        {
            rejectReason = "role mismatch";
            return false;
        }

        if (!scoringContext.IsFacilityRoleAvailable(building, role, out rejectReason))
        {
            return false;
        }

        if (actor != null
            && actor.Blackboard != null
            && actor.Blackboard.IsFacilityCoolingDown(building, out float remainingSeconds))
        {
            rejectReason = $"AI facility cooldown {remainingSeconds:0.0}s";
            return false;
        }

        if (!building.CanVisit(actor, out rejectReason))
        {
            return false;
        }

        if (building is IRetailFacility shop
            && actor != null
            && actor.TryGetAbility(out AbilityShopping shopping)
            && !shopping.CanBuyFrom(shop, out rejectReason))
        {
            return false;
        }

        return true;
    }

    public static float ScoreCandidate(
        CharacterActor actor,
        BuildableObject building,
        FacilityRole role,
        GridPathSearchResult searchResult,
        FacilityScoringContext scoringContext)
    {
        return ScoreCandidateWithBreakdown(
            actor,
            building,
            role,
            searchResult,
            scoringContext,
            out _);
    }

    public static float ScoreCandidateWithBreakdown(
        CharacterActor actor,
        BuildableObject building,
        FacilityRole role,
        GridPathSearchResult searchResult,
        FacilityScoringContext scoringContext,
        out CharacterAiUtilityBreakdown breakdown)
    {
        AIBrain brain = actor?.Brain;
        bool useDecisionCache = brain != null
            && searchResult == null
            && !actor.ShouldCollectDetailedAiDiagnostics;
        if (useDecisionCache
            && brain.TryGetCachedFacilityScore(building, role, out float cachedScore))
        {
            breakdown = null;
            return cachedScore;
        }

        ICharacterAiPerformanceRecorder recorder = brain?.PerformanceRecorder;
        long started = recorder?.DetailedCollectionEnabled == true
            ? Stopwatch.GetTimestamp()
            : 0L;
        try
        {
            float score = ScoreCandidateWithBreakdownCore(
                actor,
                building,
                role,
                searchResult,
                scoringContext,
                out breakdown);
            if (useDecisionCache)
            {
                brain.CacheFacilityScore(building, role, score);
            }

            return score;
        }
        finally
        {
            if (started != 0L)
            {
                recorder.Record(
                    AiPerformanceCategory.FacilityScoring,
                    (Stopwatch.GetTimestamp() - started)
                    * 1000.0
                    / Stopwatch.Frequency);
            }
        }
    }

    private static float ScoreCandidateWithBreakdownCore(
        CharacterActor actor,
        BuildableObject building,
        FacilityRole role,
        GridPathSearchResult searchResult,
        FacilityScoringContext scoringContext,
        out CharacterAiUtilityBreakdown breakdown)
    {
        breakdown = null;
        if (!IsCandidate(actor, building, role, scoringContext, out _))
        {
            return 0f;
        }

        FacilityRole matchedRole = GetBestMatchedRole(actor, building, role);
        float desireScore = GetNeedScore(actor, matchedRole);
        float preferenceScore = GetPreferenceScore(actor, building, matchedRole);
        float stockScore = GetStockScore(building);
        float affordabilityScore = GetAffordabilityScore(actor, building);
        float crowdScore = GetCrowdScore(actor, building);
        float distanceScore = GetDistanceScore(actor, building, searchResult);
        float noveltyScore = GetNoveltyScore(actor, building);
        float reputationBias = GetReputationBias(actor, building, scoringContext);
        float roomScore = scoringContext.GetRoomUtilityScore(building, matchedRole);
        float facilityStateScore = GetFacilityStateScore(building);
        float memoryScore = actor != null && actor.AiMemory != null
            ? actor.AiMemory.GetFacilityMemoryScore(building)
            : 0.5f;
        CharacterAiWorldSignalSnapshot signals = actor?.WorldSignalQuery?.Capture(
                actor,
                GetBranchForRole(matchedRole),
                building,
                searchResult)
            ?? CharacterAiWorldSignalSnapshot.Neutral;
        float queueScore = Mathf.Clamp01(1f - signals.QueuePressure);
        float socialScore = ResolveSocialFacilityScore(matchedRole, signals);
        float weatherScore = Mathf.Clamp01(1f - signals.WeatherPressure);
        float pathScore = signals.PathConfidence;
        float fatigueScore = Mathf.Clamp01(1f - signals.RecentFailurePressure);
        float scheduleScore = signals.ScheduleScore;
        float speciesAffinityBias = GetSpeciesAffinityBias(actor, building);

        float score =
            desireScore * 0.26f
            + preferenceScore * 0.14f
            + stockScore * 0.1f
            + affordabilityScore * 0.07f
            + crowdScore * 0.06f
            + distanceScore * 0.05f
            + noveltyScore * 0.04f
            + roomScore * 0.1f
            + facilityStateScore * 0.04f
            + memoryScore * 0.04f
            + queueScore * 0.05f
            + socialScore * 0.03f
            + weatherScore * 0.02f
            + pathScore * 0.03f
            + fatigueScore * 0.02f
            + scheduleScore * 0.02f
            + reputationBias;

        float finalScore = ApplySpeciesAffinityBias(score, speciesAffinityBias);
        if (actor != null && !actor.ShouldCollectDetailedAiDiagnostics)
        {
            return finalScore;
        }

        breakdown = new CharacterAiUtilityBreakdown(
            CharacterAiUtilityText.GetIntention(GetBranchForRole(matchedRole)),
            GetFacilityLabel(building),
            true);
        breakdown.Add(CharacterAiUtilityFactorKind.Need, desireScore, 0.3f, FacilityRoleDisplayName(matchedRole));
        breakdown.Add(CharacterAiUtilityFactorKind.Personality, preferenceScore, 0.17f, "개인 취향");
        breakdown.Add(CharacterAiUtilityFactorKind.Stock, stockScore, 0.12f, "재고");
        breakdown.Add(CharacterAiUtilityFactorKind.Crowd, crowdScore, 0.08f, "혼잡");
        breakdown.Add(CharacterAiUtilityFactorKind.Distance, distanceScore, 0.06f, "거리");
        breakdown.Add(CharacterAiUtilityFactorKind.Queue, queueScore, 0.05f, "대기열");
        breakdown.Add(CharacterAiUtilityFactorKind.Room, roomScore, 0.11f, "방 환경");
        breakdown.Add(CharacterAiUtilityFactorKind.Memory, memoryScore, 0.05f, "최근 기억");
        breakdown.Add(CharacterAiUtilityFactorKind.Reservation, facilityStateScore, 0.04f, "시설 상태");
        breakdown.Add(CharacterAiUtilityFactorKind.PathConfidence, pathScore, 0.03f, "경로 신뢰");
        breakdown.Add(CharacterAiUtilityFactorKind.Social, socialScore, 0.03f, "주변 분위기");
        breakdown.Add(CharacterAiUtilityFactorKind.Novelty, noveltyScore, 0.03f, "새로움");
        breakdown.Add(CharacterAiUtilityFactorKind.Schedule, scheduleScore, 0.02f, "일정");
        breakdown.SetFinalScore(finalScore);
        return finalScore;
    }

    private static float GetSpeciesAffinityBias(CharacterActor actor, BuildableObject building)
    {
        CharacterIdentity identity = actor != null ? actor.Identity : null;
        string speciesTag = identity != null ? identity.SpeciesTag : string.Empty;
        BuildingSO data = building != null ? building.BuildingData : null;
        if (string.IsNullOrWhiteSpace(speciesTag) || data == null)
        {
            return 0f;
        }

        if (data.IsPreferredSpecies(speciesTag))
        {
            return 0.35f;
        }

        if (data.IsDislikedSpecies(speciesTag))
        {
            return -0.35f;
        }

        return 0f;
    }

    private static float ApplySpeciesAffinityBias(float score, float bias)
    {
        score = Mathf.Clamp01(score);
        if (bias > 0f)
        {
            return Mathf.Clamp01(score + (1f - score) * bias);
        }

        if (bias < 0f)
        {
            return Mathf.Clamp01(score + score * bias);
        }

        return score;
    }

    public static float GetNeedScore(CharacterActor actor, FacilityRole role)
    {
        if (HasMultipleRoles(role))
        {
            float highestNeed = 0f;
            foreach (FacilityRole scoredRole in ScoredRoles)
            {
                if ((role & scoredRole) == 0) continue;

                highestNeed = Mathf.Max(highestNeed, GetNeedScore(actor, scoredRole));
            }

            return highestNeed;
        }

        CharacterStats stats = actor != null ? actor.Stats : null;
        if (stats == null)
        {
            return 0.5f;
        }

        return role switch
        {
            FacilityRole.Meal => GetLowStatNeed(actor, CharacterCondition.HUNGER),
            FacilityRole.Purchase => Mathf.Max(
                GetLowStatNeed(actor, CharacterCondition.FUN),
                GetLowStatNeed(actor, CharacterCondition.MOOD) * 0.6f),
            FacilityRole.Rest => Mathf.Max(
                GetLowStatNeed(actor, CharacterCondition.SLEEP),
                GetLowStatNeed(actor, CharacterCondition.MOOD) * 0.4f,
                GetExpeditionRecoveryNeed(actor)),
            FacilityRole.Training => GetLowStatNeed(actor, CharacterCondition.FUN),
            FacilityRole.Research => GetLowStatNeed(actor, CharacterCondition.FUN),
            FacilityRole.Mana => GetLowStatNeed(actor, CharacterCondition.MOOD),
            FacilityRole.Toilet => GetLowStatNeed(actor, CharacterCondition.EXCRETION),
            FacilityRole.Hygiene => Mathf.Max(
                GetLowStatNeed(actor, CharacterCondition.HYGIENE),
                GetExpeditionStressNeed(actor) * 0.75f),
            _ => 0.5f
        };
    }

    public static float GetExpeditionRecoveryNeed(CharacterActor actor)
    {
        return Mathf.Max(
            actor != null ? actor.InjurySeverity : 0f,
            GetExpeditionStressNeed(actor));
    }

    private static IReadOnlyList<BuildableObject> GetCandidateSource(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        FacilityRole role)
    {
        if (searchResult != null)
        {
            return RequireFacilityCandidateCache(actor).GetCandidates(searchResult.sourceGrid, role);
        }

        if (actor != null
            && actor.Brain != null
            && actor.Brain.TryGetRuntimeGrid(out Grid grid))
        {
            return RequireFacilityCandidateCache(actor).GetCandidates(grid, role);
        }

        return System.Array.Empty<BuildableObject>();
    }

    private static int BuildScoringShortlist(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> source)
    {
        if (source == null)
        {
            return 0;
        }

        if (source.Count <= MaximumFullyScoredCandidates)
        {
            return source.Count;
        }

        scoringShortlist ??=
            new BuildableObject[MaximumFullyScoredCandidates];
        scoringShortlistCosts ??=
            new int[MaximumFullyScoredCandidates];

        Vector2Int origin = actor != null ? actor.GetNowXY() : Vector2Int.zero;
        int selectedCount = 0;
        int worstIndex = -1;
        int worstCost = int.MinValue;
        for (int sourceIndex = 0; sourceIndex < source.Count; sourceIndex++)
        {
            BuildableObject building = source[sourceIndex];
            if (building == null || building.isDestroy)
            {
                continue;
            }

            int cost = EstimateCandidateDistance(origin, building);
            if (selectedCount < MaximumFullyScoredCandidates)
            {
                scoringShortlist[selectedCount] = building;
                scoringShortlistCosts[selectedCount] = cost;
                if (cost > worstCost)
                {
                    worstCost = cost;
                    worstIndex = selectedCount;
                }

                selectedCount++;
                continue;
            }

            if (cost >= worstCost)
            {
                continue;
            }

            scoringShortlist[worstIndex] = building;
            scoringShortlistCosts[worstIndex] = cost;
            worstIndex = 0;
            worstCost = scoringShortlistCosts[0];
            for (int index = 1; index < selectedCount; index++)
            {
                if (scoringShortlistCosts[index] > worstCost)
                {
                    worstCost = scoringShortlistCosts[index];
                    worstIndex = index;
                }
            }
        }

        return selectedCount;
    }

    private static int EstimateCandidateDistance(
        Vector2Int origin,
        BuildableObject building)
    {
        IReadOnlyList<Vector2Int> positions = building.buildPoses;
        int best = int.MaxValue;
        if (positions != null)
        {
            for (int index = 0; index < positions.Count; index++)
            {
                Vector2Int candidate = positions[index];
                int distance = Mathf.Abs(origin.x - candidate.x)
                    + Mathf.Abs(origin.y - candidate.y) * 8;
                if (distance < best)
                {
                    best = distance;
                }
            }
        }

        return best != int.MaxValue
            ? best
            : Mathf.Abs(origin.x - building.centerPos.x)
                + Mathf.Abs(origin.y - building.centerPos.y) * 8;
    }

    private static void ClearScoringShortlist(
        IReadOnlyList<BuildableObject> source,
        int count)
    {
        if (source == null
            || source.Count <= MaximumFullyScoredCandidates
            || scoringShortlist == null)
        {
            return;
        }

        for (int index = 0; index < count; index++)
        {
            scoringShortlist[index] = null;
        }
    }

    private static IFacilityCandidateCache RequireFacilityCandidateCache(CharacterActor actor)
    {
        if (actor == null || actor.Brain == null)
        {
            throw new System.InvalidOperationException(
                $"{nameof(FacilityCandidateScorer)} requires an actor with {nameof(AIBrain)} for cached facility candidate lookup.");
        }

        return actor.Brain.RequireFacilityCandidateCache();
    }

    private static bool IsReachableCandidate(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        BuildableObject building,
        FacilityRole role,
        FacilityScoringContext scoringContext)
    {
        if (searchResult != null && !searchResult.ContainsVisitableOccupant(building))
        {
            return false;
        }

        return IsCandidate(actor, building, role, scoringContext, out _);
    }

    private static FacilityRole GetBestMatchedRole(
        CharacterActor actor,
        BuildableObject building,
        FacilityRole requestedRoles)
    {
        if (building == null || requestedRoles == FacilityRole.None)
        {
            return requestedRoles;
        }

        FacilityRole bestRole = FacilityRole.None;
        float highestNeed = float.MinValue;
        foreach (FacilityRole role in ScoredRoles)
        {
            if ((requestedRoles & role) == 0 || !building.SupportsFacilityRole(role))
            {
                continue;
            }

            float need = GetNeedScore(actor, role);
            if (need > highestNeed)
            {
                highestNeed = need;
                bestRole = role;
            }
        }

        return bestRole != FacilityRole.None ? bestRole : requestedRoles;
    }

    private static bool HasMultipleRoles(FacilityRole role)
    {
        int value = (int)role;
        return value != 0 && (value & (value - 1)) != 0;
    }

    private static float GetLowStatNeed(CharacterActor actor, CharacterCondition condition)
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        if (stats == null
            || stats.Stats == null
            || !stats.Stats.TryGetValue(condition, out float value))
        {
            return 0.5f;
        }

        return Mathf.Clamp01(1f - (value / 100f));
    }

    private static float GetExpeditionStressNeed(CharacterActor actor)
    {
        CharacterLifecycle lifecycle = actor != null ? actor.Lifecycle : null;
        return Mathf.Clamp01((lifecycle?.ExpeditionRecovery?.stress ?? 0f) / 100f);
    }

    private static float GetPreferenceScore(
        CharacterActor actor,
        BuildableObject building,
        FacilityRole matchedRole)
    {
        float speciesTagPreferenceScore = GetSpeciesTagPreferenceScore(actor, building);
        float modelPreferenceScore = GetCharacterModelPreferenceScore(actor, building, matchedRole);
        float personaPreferenceScore = actor != null && actor.PersonaRuntime != null
            ? actor.PersonaRuntime.GetFacilityTagPreference(building)
            : 0.5f;
        return Mathf.Clamp01((speciesTagPreferenceScore + modelPreferenceScore + personaPreferenceScore) / 3f);
    }

    private static float GetSpeciesTagPreferenceScore(CharacterActor actor, BuildableObject building)
    {
        CharacterIdentity identity = actor != null ? actor.Identity : null;
        string speciesTag = identity != null ? identity.SpeciesTag : string.Empty;
        if (string.IsNullOrWhiteSpace(speciesTag) || building.Facility == null)
        {
            return 0.5f;
        }

        if (building.BuildingData.IsDislikedSpecies(speciesTag))
        {
            return 0.1f;
        }

        if (building.BuildingData.IsPreferredSpecies(speciesTag))
        {
            return 1f;
        }

        return 0.5f;
    }

    private static float GetCharacterModelPreferenceScore(
        CharacterActor actor,
        BuildableObject building,
        FacilityRole matchedRole)
    {
        if (actor == null || building == null || building.Facility == null)
        {
            return 0.5f;
        }

        FacilityRole roles = matchedRole != FacilityRole.None
            ? matchedRole
            : building.Facility.roles;
        return actor.Stats != null ? actor.Stats.GetFacilityPreferenceScore(roles) : 0.5f;
    }

    private static float GetStockScore(BuildableObject building)
    {
        if (building.Facility == null || !building.BuildingData.RequiresStockForUse())
        {
            return 1f;
        }

        if (building is not IStockedFacility stockedFacility)
        {
            return 0f;
        }

        int max = Mathf.Max(1, building.GetInternalStockCapacity());
        return Mathf.Clamp01((float)stockedFacility.CurrentStock / max);
    }

    private static float GetAffordabilityScore(CharacterActor actor, BuildableObject building)
    {
        if (building is not IRetailFacility shop)
        {
            return 1f;
        }

        if (actor == null || !actor.TryGetAbility(out AbilityShopping shopping))
        {
            return 1f;
        }

        return shopping.GetAffordabilityScore(shop);
    }

    private static float GetCrowdScore(CharacterActor actor, BuildableObject building)
    {
        if (building.Facility == null || building.Facility.capacity <= 0)
        {
            return 1f;
        }

        CharacterStats stats = actor != null ? actor.Stats : null;
        float sensitivity = stats != null ? stats.GetCrowdSensitivityMultiplier() : 1f;
        int capacity = Mathf.Max(1, building.EffectiveCapacity);
        int pressureCount = building.CurrentUserCount + Mathf.Max(0, building.ActiveVisitReservationCount);
        return Mathf.Clamp01(1f - (((float)pressureCount / capacity) * sensitivity));
    }

    private static float ResolveSocialFacilityScore(
        FacilityRole role,
        CharacterAiWorldSignalSnapshot signals)
    {
        float nearby = signals.SocialOpportunity;
        if (role == FacilityRole.Purchase || role == FacilityRole.Meal)
        {
            return Mathf.Clamp01(0.45f + nearby * 0.45f - signals.QueuePressure * 0.2f);
        }

        if (role == FacilityRole.Rest || role == FacilityRole.Hygiene || role == FacilityRole.Toilet)
        {
            return Mathf.Clamp01(0.75f - nearby * 0.35f);
        }

        return Mathf.Clamp01(0.5f + nearby * 0.15f);
    }

    private static float GetDistanceScore(
        CharacterActor actor,
        BuildableObject building,
        GridPathSearchResult searchResult)
    {
        if (building == null)
        {
            return 0f;
        }

        if (searchResult != null)
        {
            int travelCost = searchResult.GetMoveCostTo(building);
            if (travelCost == int.MaxValue)
            {
                return 0f;
            }

            float distance = travelCost
                / (float)DefaultGridTraversalCostPolicy.DryWalkCost;
            return 1f / (1f + distance);
        }

        Vector2Int actorPosition = actor != null
            ? actor.GetNowXY()
            : building.centerPos;
        IReadOnlyList<Vector2Int> positions = building.buildPoses;
        int bestEstimate = int.MaxValue;
        if (positions != null)
        {
            for (int index = 0; index < positions.Count; index++)
            {
                Vector2Int candidate = positions[index];
                int horizontal = Mathf.Abs(actorPosition.x - candidate.x);
                int floors = Mathf.Abs(actorPosition.y - candidate.y);
                int estimate = horizontal + floors * 8;
                if (estimate < bestEstimate)
                {
                    bestEstimate = estimate;
                }
            }
        }

        if (bestEstimate == int.MaxValue)
        {
            bestEstimate = Mathf.Abs(actorPosition.x - building.centerPos.x)
                + Mathf.Abs(actorPosition.y - building.centerPos.y) * 8;
        }

        return 1f / (1f + bestEstimate);
    }

    private static float GetNoveltyScore(CharacterActor actor, BuildableObject building)
    {
        if (actor == null || !actor.TryGetAbility(out AbilityShopping shopping))
        {
            return 1f;
        }

        return shopping.HasVisited(building) ? 0.2f : 1f;
    }

    private static float GetFacilityStateScore(BuildableObject building)
    {
        if (building == null)
        {
            return 0f;
        }

        float score = Mathf.Clamp01(building.FacilityState.cleanliness / 100f);
        if (building.IsDamaged)
        {
            score *= 0.45f;
        }

        if (building.Facility != null && building.Facility.capacity > 0)
        {
            float pressure = Mathf.Clamp01((float)building.CurrentUserCount / building.Facility.capacity);
            score *= Mathf.Lerp(1f, 0.6f, pressure);
        }

        return Mathf.Clamp01(score);
    }

    private static float GetReputationBias(
        CharacterActor actor,
        BuildableObject building,
        FacilityScoringContext scoringContext)
    {
        return scoringContext.GetReputationBias(actor, building);
    }

    private static void RecordFacilityBreakdown(
        CharacterActor actor,
        CharacterAiUtilityBreakdown breakdown,
        float expectedScore)
    {
        if (actor == null || actor.Blackboard == null || breakdown == null)
        {
            return;
        }

        breakdown.SetFinalScore(Mathf.Approximately(expectedScore, float.MinValue) ? breakdown.FinalScore01 : expectedScore);
        actor.Blackboard.RecordUtilityBreakdown(breakdown);
    }

    private static CharacterAiBranch GetBranchForRole(FacilityRole role)
    {
        return role switch
        {
            FacilityRole.Meal => CharacterAiBranch.Eat,
            FacilityRole.Rest => CharacterAiBranch.Rest,
            FacilityRole.Toilet => CharacterAiBranch.Toilet,
            FacilityRole.Hygiene => CharacterAiBranch.Hygiene,
            FacilityRole.Purchase => CharacterAiBranch.Shopping,
            FacilityRole.Training => CharacterAiBranch.Work,
            FacilityRole.Research => CharacterAiBranch.Work,
            FacilityRole.Mana => CharacterAiBranch.Work,
            _ => CharacterAiBranch.Work
        };
    }

    private static string FacilityRoleDisplayName(FacilityRole role)
    {
        return role switch
        {
            FacilityRole.Meal => "식사",
            FacilityRole.Purchase => "구매",
            FacilityRole.Rest => "휴식",
            FacilityRole.Training => "훈련",
            FacilityRole.Research => "연구",
            FacilityRole.Mana => "마나",
            FacilityRole.Logistics => "물류",
            FacilityRole.Toilet => "화장실",
            FacilityRole.Hygiene => "위생",
            FacilityRole.Administration => "운영",
            FacilityRole.Security => "경비",
            _ => role.ToString()
        };
    }

    private static string GetFacilityLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "시설 없음";
        }

        return building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }
}
