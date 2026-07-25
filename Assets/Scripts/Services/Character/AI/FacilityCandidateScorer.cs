using System.Collections.Generic;
using UnityEngine;

public static class FacilityCandidateScorer
{
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
        if (role == FacilityRole.None)
        {
            return false;
        }

        FacilityScoringContext scoringContext = FacilityScoringContext.RequireFromActor(actor);
        foreach (BuildableObject building in GetCandidateSource(actor, searchResult, role))
        {
            if (IsReachableCandidate(actor, searchResult, building, role, scoringContext))
            {
                return true;
            }
        }

        return false;
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

        float bestScore = float.MinValue;
        CharacterAiUtilityBreakdown bestBreakdown = null;
        int bestId = int.MaxValue;
        foreach (BuildableObject building in GetCandidateSource(actor, searchResult, role))
        {
            if (!IsReachableCandidate(actor, searchResult, building, role, scoringContext))
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
        float distanceScore = GetDistanceScore(building, searchResult);
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
        breakdown = new CharacterAiUtilityBreakdown(
            CharacterAiUtilityText.GetIntention(GetBranchForRole(matchedRole)),
            GetFacilityLabel(building));
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

    private static IEnumerable<BuildableObject> GetCandidateSource(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        FacilityRole role)
    {
        if (searchResult != null)
        {
            return RequireFacilityCandidateCache(actor).GetCandidates(searchResult.sourceGrid, role);
        }

        if (actor != null)
        {
            return actor.GetReachableBuilding();
        }

        return System.Array.Empty<BuildableObject>();
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

    private static float GetDistanceScore(BuildableObject building, GridPathSearchResult searchResult)
    {
        if (building == null || searchResult == null)
        {
            return 0.5f;
        }

        int distance = searchResult.GetMoveDistanceTo(building);
        if (distance == int.MaxValue)
        {
            return 0f;
        }

        return 1f / (1f + distance);
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
