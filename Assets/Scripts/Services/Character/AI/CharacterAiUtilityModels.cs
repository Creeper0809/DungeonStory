using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct CharacterAiUtilityFactor
{
    public CharacterAiUtilityFactor(
        CharacterAiUtilityFactorKind kind,
        float score,
        float weight,
        string reason)
    {
        Kind = kind;
        Score = Mathf.Clamp01(score);
        Weight = Mathf.Max(0f, weight);
        Reason = reason ?? string.Empty;
    }

    public CharacterAiUtilityFactorKind Kind { get; }
    public float Score { get; }
    public float Weight { get; }
    public string Reason { get; }
    public float WeightedScore => Score * Weight;

    public override string ToString()
    {
        string label = CharacterAiUtilityText.GetFactorLabel(Kind);
        return string.IsNullOrWhiteSpace(Reason)
            ? $"{label} {Score:0.##}"
            : $"{label} {Score:0.##}({Reason})";
    }
}

public sealed class CharacterAiUtilityBreakdown
{
    private readonly List<CharacterAiUtilityFactor> factors = new List<CharacterAiUtilityFactor>();
    private readonly IReadOnlyList<CharacterAiUtilityFactor> factorsView;

    public CharacterAiUtilityBreakdown(
        CharacterAiIntentionType intention,
        string candidateLabel)
    {
        Intention = intention;
        CandidateLabel = candidateLabel ?? string.Empty;
        factorsView = ReadOnlyView.List(factors);
    }

    public CharacterAiIntentionType Intention { get; }
    public string CandidateLabel { get; private set; }
    public float FinalScore01 { get; private set; }
    public string RejectionReason { get; private set; } = string.Empty;
    public IReadOnlyList<CharacterAiUtilityFactor> Factors => factorsView;
    public bool HasFactors => factors.Count > 0;

    public void RenameCandidate(string label)
    {
        CandidateLabel = label ?? string.Empty;
    }

    public void Add(
        CharacterAiUtilityFactorKind kind,
        float score,
        float weight,
        string reason = "")
    {
        if (weight <= 0f)
        {
            return;
        }

        factors.Add(new CharacterAiUtilityFactor(kind, score, weight, reason));
    }

    public void Reject(string reason)
    {
        FinalScore01 = 0f;
        RejectionReason = reason ?? string.Empty;
    }

    public float CalculateWeighted01()
    {
        float totalWeight = factors.Sum(factor => factor.Weight);
        if (totalWeight <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(factors.Sum(factor => factor.WeightedScore) / totalWeight);
    }

    public void SetFinalScore(float score)
    {
        FinalScore01 = Mathf.Clamp01(score);
    }

    public string ToCompactString(int maxFactors = 5)
    {
        string candidate = string.IsNullOrWhiteSpace(CandidateLabel)
            ? CharacterAiUtilityText.GetIntentionLabel(Intention)
            : CandidateLabel;
        if (!string.IsNullOrWhiteSpace(RejectionReason))
        {
            return $"{candidate} 탈락: {RejectionReason}";
        }

        IEnumerable<string> factorRows = factors
            .OrderByDescending(factor => factor.Weight)
            .ThenByDescending(factor => factor.Score)
            .Take(Mathf.Max(1, maxFactors))
            .Select(factor => factor.ToString());
        string factorText = string.Join(", ", factorRows);
        return string.IsNullOrWhiteSpace(factorText)
            ? $"{candidate} {FinalScore01 * 100f:0.#}%"
            : $"{candidate} {FinalScore01 * 100f:0.#}% · {factorText}";
    }

    public string ToMultilineString(int maxFactors = 8)
    {
        string firstLine = ToCompactString(1);
        if (!string.IsNullOrWhiteSpace(RejectionReason))
        {
            return firstLine;
        }

        IEnumerable<string> rows = factors
            .OrderByDescending(factor => factor.Weight)
            .ThenByDescending(factor => factor.Score)
            .Take(Mathf.Max(1, maxFactors))
            .Select(factor => $" - {factor}");
        return $"{firstLine}\n{string.Join("\n", rows)}";
    }
}

public readonly struct CharacterAiDecisionContext
{
    private CharacterAiDecisionContext(
        CharacterActor actor,
        CharacterAiBranch branch,
        CharacterCondition strongestNeed,
        float strongestNeedUrgency,
        float moodUrgency,
        float healthUrgency,
        float injuryUrgency,
        float carryLoad,
        float workPriority,
        float haulPriority,
        float huntPriority,
        float foodStockPressure,
        float waterStockPressure,
        float roomScore,
        float exteriorRisk,
        float memoryMomentum,
        CharacterAiWorldSignalSnapshot worldSignals)
    {
        Actor = actor;
        Branch = branch;
        StrongestNeed = strongestNeed;
        StrongestNeedUrgency = Mathf.Clamp01(strongestNeedUrgency);
        MoodUrgency = Mathf.Clamp01(moodUrgency);
        HealthUrgency = Mathf.Clamp01(healthUrgency);
        InjuryUrgency = Mathf.Clamp01(injuryUrgency);
        CarryLoad = Mathf.Clamp01(carryLoad);
        WorkPriority = Mathf.Clamp01(workPriority);
        HaulPriority = Mathf.Clamp01(haulPriority);
        HuntPriority = Mathf.Clamp01(huntPriority);
        FoodStockPressure = Mathf.Clamp01(foodStockPressure);
        WaterStockPressure = Mathf.Clamp01(waterStockPressure);
        RoomScore = Mathf.Clamp01(roomScore);
        ExteriorRisk = Mathf.Clamp01(exteriorRisk);
        MemoryMomentum = Mathf.Clamp(memoryMomentum, -1f, 1f);
        WorldSignals = worldSignals;
    }

    public CharacterActor Actor { get; }
    public CharacterAiBranch Branch { get; }
    public CharacterCondition StrongestNeed { get; }
    public float StrongestNeedUrgency { get; }
    public float MoodUrgency { get; }
    public float HealthUrgency { get; }
    public float InjuryUrgency { get; }
    public float CarryLoad { get; }
    public float WorkPriority { get; }
    public float HaulPriority { get; }
    public float HuntPriority { get; }
    public float FoodStockPressure { get; }
    public float WaterStockPressure { get; }
    public float RoomScore { get; }
    public float ExteriorRisk { get; }
    public float MemoryMomentum { get; }
    public CharacterAiWorldSignalSnapshot WorldSignals { get; }
    public float ScheduleScore => WorldSignals.ScheduleScore;
    public float QueuePressure => WorldSignals.QueuePressure;
    public float SocialOpportunity => WorldSignals.SocialOpportunity;
    public float WeatherPressure => WorldSignals.WeatherPressure;
    public float PathConfidence => WorldSignals.PathConfidence;
    public float RecentFailurePressure => WorldSignals.RecentFailurePressure;
    public float RecentMovementPressure => WorldSignals.RecentMovementPressure;
    public float NearbyWildlifeThreat => WorldSignals.NearbyWildlifeThreat;

    public float EmergencyScore => Mathf.Clamp01(Mathf.Max(
        StrongestNeedUrgency,
        HealthUrgency,
        InjuryUrgency,
        FoodStockPressure * 0.9f,
        WaterStockPressure,
        ExteriorRisk * 0.75f,
        NearbyWildlifeThreat * 0.8f));

    public static CharacterAiDecisionContext Capture(
        CharacterActor actor,
        CharacterAiBranch branch = CharacterAiBranch.None)
    {
        CharacterCondition strongest = CharacterCondition.HUNGER;
        float strongestUrgency = 0f;
        if (CharacterNeedCatalog.TryGetStrongestUrgency(
                actor,
                CharacterNeedTag.Survival,
                out CharacterNeedDefinition strongestDefinition,
                out float weightedUrgency))
        {
            strongest = strongestDefinition.Condition;
            strongestUrgency = weightedUrgency;
        }

        float moodUrgency = CharacterNeedCatalog.GetUrgency(actor, CharacterCondition.MOOD);
        float healthUrgency = 0f;
        float injuryUrgency = 0f;
        if (actor != null)
        {
            healthUrgency = Mathf.Clamp01(1f - actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
            injuryUrgency = Mathf.Clamp01(actor.InjurySeverity);
        }

        float carryLoad = 0f;
        CharacterCarryInventory carry = actor != null ? actor.GetComponent<CharacterCarryInventory>() : null;
        if (carry != null)
        {
            carryLoad = Mathf.Clamp01(carry.GetCurrentWeight() / Mathf.Max(1f, carry.GetBaseCarryLimit()));
        }

        float workPriority = 0f;
        float haulPriority = 0f;
        float huntPriority = 0f;
        if (actor != null && actor.TryGetAbility(out AbilityWork work) && work.WorkPriorities != null)
        {
            workPriority = Mathf.Max(
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Operate)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Restock)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Construct)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Repair)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Clean)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Craft)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Reception)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.DrawWater)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Cook)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Treat)),
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Refuel)));
            haulPriority = GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul));
            huntPriority = GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Hunt));
        }

        CharacterAiMemoryRuntime memory = actor != null ? actor.AiMemory : null;
        float momentum = memory != null ? memory.GetMomentumScore(branch) : 0f;
        CharacterAiWorldSignalSnapshot worldSignals = actor?.WorldSignalQuery?.Capture(actor, branch)
            ?? CharacterAiWorldSignalSnapshot.Neutral;
        float foodPressure = worldSignals.FoodStockPressure;
        float waterPressure = worldSignals.WaterStockPressure;

        return new CharacterAiDecisionContext(
            actor,
            branch,
            strongest,
            strongestUrgency,
            moodUrgency,
            healthUrgency,
            injuryUrgency,
            carryLoad,
            workPriority,
            haulPriority,
            huntPriority,
            foodPressure,
            waterPressure,
            Mathf.Clamp01(0.5f + worldSignals.PathConfidence * 0.12f - worldSignals.QueuePressure * 0.08f),
            worldSignals.ExteriorRisk,
            momentum,
            worldSignals);
    }

    public CharacterAiUtilityBreakdown CreateRoutineBreakdown(
        CharacterAiBranch branch,
        float basePriority01)
    {
        CharacterAiIntentionType intention = CharacterAiUtilityText.GetIntention(branch);
        CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
            intention,
            CharacterAiUtilityText.GetBranchLabel(branch));
        switch (branch)
        {
            case CharacterAiBranch.SurvivalNeeds:
                breakdown.Add(CharacterAiUtilityFactorKind.Need, EmergencyScore, 0.45f, GetNeedLabel());
                breakdown.Add(CharacterAiUtilityFactorKind.Stock, Mathf.Max(FoodStockPressure, WaterStockPressure), 0.2f, "생존 재고");
                breakdown.Add(CharacterAiUtilityFactorKind.Risk, Mathf.Max(HealthUrgency, InjuryUrgency), 0.15f, "건강");
                breakdown.Add(CharacterAiUtilityFactorKind.Weather, Mathf.Clamp01(1f - WeatherPressure), 0.06f, "날씨 부담");
                breakdown.Add(CharacterAiUtilityFactorKind.Risk, Mathf.Clamp01(1f - NearbyWildlifeThreat), 0.06f, "동물 위협");
                break;
            case CharacterAiBranch.DutyWork:
                breakdown.Add(CharacterAiUtilityFactorKind.Priority, WorkPriority, 0.35f, "작업 우선순위");
                breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Clamp01(1f - EmergencyScore), 0.25f, "일할 여유");
                breakdown.Add(CharacterAiUtilityFactorKind.Personality, GetPersonalityScore(branch), 0.2f, "성실함");
                breakdown.Add(CharacterAiUtilityFactorKind.Schedule, ScheduleScore, 0.08f, "근무 시간");
                breakdown.Add(CharacterAiUtilityFactorKind.PathConfidence, PathConfidence, 0.06f, "경로 신뢰");
                breakdown.Add(CharacterAiUtilityFactorKind.Fatigue, Mathf.Clamp01(1f - RecentFailurePressure), 0.06f, "최근 실패");
                break;
            case CharacterAiBranch.LeisureVisit:
                breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Max(MoodUrgency, CharacterNeedCatalog.GetUrgency(Actor, CharacterCondition.FUN)), 0.35f, "기분/재미");
                breakdown.Add(CharacterAiUtilityFactorKind.Risk, Mathf.Clamp01(1f - EmergencyScore), 0.2f, "위험 여유");
                breakdown.Add(CharacterAiUtilityFactorKind.Personality, GetPersonalityScore(branch), 0.2f, "즐김 성향");
                breakdown.Add(CharacterAiUtilityFactorKind.Social, SocialOpportunity, 0.06f, "주변 사람");
                breakdown.Add(CharacterAiUtilityFactorKind.Queue, Mathf.Clamp01(1f - QueuePressure), 0.05f, "대기열");
                breakdown.Add(CharacterAiUtilityFactorKind.Weather, Mathf.Clamp01(1f - WeatherPressure), 0.04f, "날씨");
                break;
            case CharacterAiBranch.Idle:
                breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Clamp01(1f - Mathf.Max(basePriority01, EmergencyScore)), 0.45f, "급한 일 없음");
                breakdown.Add(CharacterAiUtilityFactorKind.Momentum, Mathf.Clamp01(0.5f + MemoryMomentum), 0.2f, "자연스러운 유지");
                breakdown.Add(CharacterAiUtilityFactorKind.Social, SocialOpportunity, 0.08f, "가벼운 상호작용");
                breakdown.Add(CharacterAiUtilityFactorKind.Queue, QueuePressure, 0.04f, "줄 서기");
                breakdown.Add(CharacterAiUtilityFactorKind.Weather, Mathf.Clamp01(1f - WeatherPressure), 0.04f, "걸을 만한 날씨");
                break;
            default:
                breakdown.Add(CharacterAiUtilityFactorKind.Priority, basePriority01, 0.5f, "기본 점수");
                break;
        }

        breakdown.Add(CharacterAiUtilityFactorKind.Momentum, Mathf.Clamp01(0.5f + MemoryMomentum), 0.1f, "최근 흐름");
        breakdown.SetFinalScore(Mathf.Lerp(basePriority01, breakdown.CalculateWeighted01(), 0.35f));
        return breakdown;
    }

    public float GetPriorityScore(CharacterAiBranch branch)
    {
        return branch switch
        {
            CharacterAiBranch.Work => WorkPriority,
            CharacterAiBranch.Wait => Mathf.Clamp01(1f - EmergencyScore),
            CharacterAiBranch.Eat => CharacterNeedCatalog.GetUrgency(Actor, CharacterCondition.HUNGER),
            CharacterAiBranch.Rest => Mathf.Max(CharacterNeedCatalog.GetUrgency(Actor, CharacterCondition.SLEEP), InjuryUrgency),
            CharacterAiBranch.Toilet => CharacterNeedCatalog.GetUrgency(Actor, CharacterCondition.EXCRETION),
            CharacterAiBranch.Hygiene => CharacterNeedCatalog.GetUrgency(Actor, CharacterCondition.HYGIENE),
            CharacterAiBranch.Shopping => Mathf.Max(CharacterNeedCatalog.GetUrgency(Actor, CharacterCondition.FUN), MoodUrgency),
            CharacterAiBranch.LookAround => Mathf.Max(0.25f, CharacterNeedCatalog.GetUrgency(Actor, CharacterCondition.FUN)),
            CharacterAiBranch.ExitDungeon => Mathf.Clamp01(MoodUrgency + 0.1f),
            _ => Mathf.Clamp01(1f - EmergencyScore)
        };
    }

    public float GetPersonalityScore(CharacterAiBranch branch)
    {
        CharacterAiPersonality personality = Actor != null && Actor.Identity != null && Actor.Identity.Data != null
            ? Actor.Identity.Data.aiPersonality
            : null;
        return personality != null
            ? Mathf.Clamp01(personality.GetRoutineMultiplier(CharacterAiUtilityText.GetIntention(branch)) * 0.5f)
            : 0.5f;
    }

    public string GetNeedLabel()
    {
        return CharacterNeedCatalog.TryGet(StrongestNeed, out CharacterNeedDefinition definition)
            ? definition.DisplayName
            : StrongestNeed.ToString();
    }

    private static float GetPriority01(WorkPriorityLevel priority)
    {
        return priority switch
        {
            WorkPriorityLevel.Priority1 => 1f,
            WorkPriorityLevel.Priority2 => 0.68f,
            WorkPriorityLevel.Priority3 => 0.35f,
            _ => 0f
        };
    }

}

public static class CharacterAiUtilityText
{
    public static CharacterAiIntentionType GetIntention(CharacterAiBranch branch)
    {
        return branch switch
        {
            CharacterAiBranch.SurvivalNeeds => CharacterAiIntentionType.Survive,
            CharacterAiBranch.DutyWork => CharacterAiIntentionType.Work,
            CharacterAiBranch.LeisureVisit => CharacterAiIntentionType.Leisure,
            CharacterAiBranch.ExitDungeon => CharacterAiIntentionType.Exit,
            CharacterAiBranch.Eat => CharacterAiIntentionType.Survive,
            CharacterAiBranch.Rest => CharacterAiIntentionType.Recover,
            CharacterAiBranch.Toilet => CharacterAiIntentionType.Survive,
            CharacterAiBranch.Hygiene => CharacterAiIntentionType.Survive,
            CharacterAiBranch.Work => CharacterAiIntentionType.Work,
            CharacterAiBranch.Shopping => CharacterAiIntentionType.Shop,
            CharacterAiBranch.LookAround => CharacterAiIntentionType.Leisure,
            CharacterAiBranch.Wait => CharacterAiIntentionType.Idle,
            CharacterAiBranch.Idle => CharacterAiIntentionType.Idle,
            CharacterAiBranch.LockedAction => CharacterAiIntentionType.None,
            CharacterAiBranch.SoftLock => CharacterAiIntentionType.None,
            CharacterAiBranch.InterruptCheck => CharacterAiIntentionType.None,
            CharacterAiBranch.Emergency => CharacterAiIntentionType.Survive,
            CharacterAiBranch.RoutineUtility => CharacterAiIntentionType.None,
            _ => CharacterAiIntentionType.None
        };
    }

    public static string GetBranchLabel(CharacterAiBranch branch)
    {
        return branch switch
        {
            CharacterAiBranch.Critical => "중단 상태",
            CharacterAiBranch.LockedAction => "진행 중 행동",
            CharacterAiBranch.SoftLock => "의도 유지",
            CharacterAiBranch.InterruptCheck => "행동 중단 검사",
            CharacterAiBranch.MacroGoal => "장기 의도",
            CharacterAiBranch.Emergency => "긴급 대응",
            CharacterAiBranch.RoutineUtility => "일상 선택",
            CharacterAiBranch.SurvivalNeeds => "생존",
            CharacterAiBranch.DutyWork => "업무",
            CharacterAiBranch.LeisureVisit => "여가",
            CharacterAiBranch.ExitDungeon => "퇴장",
            CharacterAiBranch.Eat => "식사",
            CharacterAiBranch.Rest => "휴식",
            CharacterAiBranch.Work => "작업",
            CharacterAiBranch.Shopping => "소비",
            CharacterAiBranch.LookAround => "둘러보기",
            CharacterAiBranch.Wait => "대기",
            CharacterAiBranch.Idle => "잠깐 멈춤",
            CharacterAiBranch.Toilet => "화장실",
            CharacterAiBranch.Hygiene => "위생",
            CharacterAiBranch.StopCurrent => "이전 중단",
            CharacterAiBranch.ContinueCurrent => "이전 유지",
            _ => branch.ToString()
        };
    }

    public static string GetIntentionLabel(CharacterAiIntentionType intention)
    {
        return intention switch
        {
            CharacterAiIntentionType.Survive => "생존",
            CharacterAiIntentionType.Recover => "회복",
            CharacterAiIntentionType.Work => "업무",
            CharacterAiIntentionType.Logistics => "물류",
            CharacterAiIntentionType.Guard => "경비",
            CharacterAiIntentionType.Hunt => "사냥",
            CharacterAiIntentionType.Leisure => "여가",
            CharacterAiIntentionType.Social => "사회",
            CharacterAiIntentionType.Shop => "구매",
            CharacterAiIntentionType.Exit => "퇴장",
            CharacterAiIntentionType.Idle => "대기",
            _ => "없음"
        };
    }

    public static string GetFactorLabel(CharacterAiUtilityFactorKind kind)
    {
        return kind switch
        {
            CharacterAiUtilityFactorKind.Need => "욕구",
            CharacterAiUtilityFactorKind.Priority => "우선순위",
            CharacterAiUtilityFactorKind.Personality => "성격",
            CharacterAiUtilityFactorKind.Memory => "기억",
            CharacterAiUtilityFactorKind.Distance => "거리",
            CharacterAiUtilityFactorKind.Risk => "위험",
            CharacterAiUtilityFactorKind.Room => "방",
            CharacterAiUtilityFactorKind.Stock => "재고",
            CharacterAiUtilityFactorKind.Crowd => "혼잡",
            CharacterAiUtilityFactorKind.Reservation => "예약",
            CharacterAiUtilityFactorKind.Momentum => "흐름",
            CharacterAiUtilityFactorKind.Queue => "대기열",
            CharacterAiUtilityFactorKind.Social => "사회",
            CharacterAiUtilityFactorKind.Weather => "날씨",
            CharacterAiUtilityFactorKind.PathConfidence => "경로",
            CharacterAiUtilityFactorKind.Fatigue => "피로",
            CharacterAiUtilityFactorKind.Novelty => "새로움",
            CharacterAiUtilityFactorKind.Schedule => "일정",
            _ => kind.ToString()
        };
    }
}
