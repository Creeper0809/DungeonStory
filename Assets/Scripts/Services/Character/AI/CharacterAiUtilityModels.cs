using System.Collections.Generic;
using System;
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
    private readonly List<CharacterAiUtilityFactor> factors;
    private readonly IReadOnlyList<CharacterAiUtilityFactor> factorsView;
    private float totalWeight;
    private float totalWeightedScore;

    public CharacterAiUtilityBreakdown(
        CharacterAiIntentionType intention,
        string candidateLabel,
        bool captureDetails = true)
    {
        Intention = intention;
        CandidateLabel = candidateLabel ?? string.Empty;
        factors = captureDetails ? new List<CharacterAiUtilityFactor>() : null;
        factorsView = factors != null
            ? ReadOnlyView.List(factors)
            : Array.Empty<CharacterAiUtilityFactor>();
    }

    public CharacterAiIntentionType Intention { get; }
    public string CandidateLabel { get; private set; }
    public float FinalScore01 { get; private set; }
    public string RejectionReason { get; private set; } = string.Empty;
    public IReadOnlyList<CharacterAiUtilityFactor> Factors => factorsView;
    public bool HasFactors => totalWeight > 0f;

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

        CharacterAiUtilityFactor factor =
            new CharacterAiUtilityFactor(kind, score, weight, reason);
        totalWeight += factor.Weight;
        totalWeightedScore += factor.WeightedScore;
        factors?.Add(factor);
    }

    public void Reject(string reason)
    {
        FinalScore01 = 0f;
        RejectionReason = reason ?? string.Empty;
    }

    public float CalculateWeighted01()
    {
        if (totalWeight <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(totalWeightedScore / totalWeight);
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

        IEnumerable<CharacterAiUtilityFactor> factorSource =
            factors ?? Enumerable.Empty<CharacterAiUtilityFactor>();
        IEnumerable<string> factorRows = factorSource
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

        IEnumerable<CharacterAiUtilityFactor> factorSource =
            factors ?? Enumerable.Empty<CharacterAiUtilityFactor>();
        IEnumerable<string> rows = factorSource
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
        float hungerUrgency,
        float sleepUrgency,
        float excretionUrgency,
        float hygieneUrgency,
        float funUrgency,
        float thirstUrgency,
        float expeditionStressUrgency,
        float carryLoad,
        float workPriority,
        float haulPriority,
        float huntPriority,
        bool isWorker,
        bool isOffDuty,
        bool hasShoppingAbility,
        bool canLookAround,
        bool shouldExitDungeon,
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
        HungerUrgency = Mathf.Clamp01(hungerUrgency);
        SleepUrgency = Mathf.Clamp01(sleepUrgency);
        ExcretionUrgency = Mathf.Clamp01(excretionUrgency);
        HygieneUrgency = Mathf.Clamp01(hygieneUrgency);
        FunUrgency = Mathf.Clamp01(funUrgency);
        ThirstUrgency = Mathf.Clamp01(thirstUrgency);
        ExpeditionStressUrgency = Mathf.Clamp01(expeditionStressUrgency);
        CarryLoad = Mathf.Clamp01(carryLoad);
        WorkPriority = Mathf.Clamp01(workPriority);
        HaulPriority = Mathf.Clamp01(haulPriority);
        HuntPriority = Mathf.Clamp01(huntPriority);
        IsWorker = isWorker;
        IsOffDuty = isOffDuty;
        HasShoppingAbility = hasShoppingAbility;
        CanLookAround = canLookAround;
        ShouldExitDungeon = shouldExitDungeon;
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
    public float HungerUrgency { get; }
    public float SleepUrgency { get; }
    public float ExcretionUrgency { get; }
    public float HygieneUrgency { get; }
    public float FunUrgency { get; }
    public float ThirstUrgency { get; }
    public float ExpeditionStressUrgency { get; }
    public float ExpeditionRecoveryUrgency =>
        Mathf.Max(InjuryUrgency, ExpeditionStressUrgency);
    public float RestUrgency =>
        Mathf.Max(
            Mathf.Max(SleepUrgency, MoodUrgency * 0.4f),
            ExpeditionRecoveryUrgency);
    public float ShoppingUrgency =>
        Mathf.Max(FunUrgency, MoodUrgency * 0.6f);
    public float CarryLoad { get; }
    public float WorkPriority { get; }
    public float HaulPriority { get; }
    public float HuntPriority { get; }
    public bool IsWorker { get; }
    public bool IsOffDuty { get; }
    public bool IsOnDutyWorker => IsWorker && !IsOffDuty;
    public bool HasShoppingAbility { get; }
    public bool CanLookAround { get; }
    public bool ShouldExitDungeon { get; }
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

    public float EmergencyScore
    {
        get
        {
            float emergency = Mathf.Max(StrongestNeedUrgency, HealthUrgency);
            emergency = Mathf.Max(emergency, InjuryUrgency);
            emergency = Mathf.Max(emergency, FoodStockPressure * 0.9f);
            emergency = Mathf.Max(emergency, WaterStockPressure);
            emergency = Mathf.Max(emergency, ExteriorRisk * 0.75f);
            emergency = Mathf.Max(emergency, NearbyWildlifeThreat * 0.8f);
            return Mathf.Clamp01(emergency);
        }
    }

    public CharacterAiDecisionContext WithBranch(CharacterAiBranch branch)
    {
        CharacterAiWorldSignalSnapshot worldSignals =
            WorldSignals.WithScheduleScore(
                CharacterAiScheduleUtility.Resolve(
                    IsWorker,
                    IsOffDuty,
                    branch,
                    WorldSignals.TimeOfDay));
        CharacterAiMemoryRuntime memory = Actor != null ? Actor.AiMemory : null;
        float momentum = memory != null ? memory.GetMomentumScore(branch) : 0f;
        return new CharacterAiDecisionContext(
            Actor,
            branch,
            StrongestNeed,
            StrongestNeedUrgency,
            MoodUrgency,
            HealthUrgency,
            InjuryUrgency,
            HungerUrgency,
            SleepUrgency,
            ExcretionUrgency,
            HygieneUrgency,
            FunUrgency,
            ThirstUrgency,
            ExpeditionStressUrgency,
            CarryLoad,
            WorkPriority,
            HaulPriority,
            HuntPriority,
            IsWorker,
            IsOffDuty,
            HasShoppingAbility,
            CanLookAround,
            ShouldExitDungeon,
            worldSignals.FoodStockPressure,
            worldSignals.WaterStockPressure,
            Mathf.Clamp01(
                0.5f
                + worldSignals.PathConfidence * 0.12f
                - worldSignals.QueuePressure * 0.08f),
            worldSignals.ExteriorRisk,
            momentum,
            worldSignals);
    }

    public static CharacterAiDecisionContext Capture(
        CharacterActor actor,
        CharacterAiBranch branch = CharacterAiBranch.None)
    {
        ICharacterAiPerformanceRecorder recorder =
            actor?.Brain?.PerformanceRecorder;
        bool collectTimings = recorder?.DetailedCollectionEnabled == true;
        long stageStarted = collectTimings
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
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
        float hungerUrgency = CharacterNeedCatalog.GetUrgency(actor, CharacterCondition.HUNGER);
        float sleepUrgency = CharacterNeedCatalog.GetUrgency(actor, CharacterCondition.SLEEP);
        float excretionUrgency = CharacterNeedCatalog.GetUrgency(actor, CharacterCondition.EXCRETION);
        float hygieneUrgency = CharacterNeedCatalog.GetUrgency(actor, CharacterCondition.HYGIENE);
        float funUrgency = CharacterNeedCatalog.GetUrgency(actor, CharacterCondition.FUN);
        float thirstUrgency = CharacterNeedCatalog.GetUrgency(actor, CharacterCondition.THIRST);
        float healthUrgency = 0f;
        float injuryUrgency = 0f;
        float expeditionStressUrgency = 0f;
        if (actor != null)
        {
            healthUrgency = Mathf.Clamp01(1f - actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
            injuryUrgency = Mathf.Clamp01(actor.InjurySeverity);
            expeditionStressUrgency = Mathf.Clamp01(
                (actor.Lifecycle?.ExpeditionRecovery?.stress ?? 0f) / 100f);
        }
        RecordCaptureStage(
            recorder,
            AiPerformanceCategory.DecisionContextNeeds,
            ref stageStarted);

        float carryLoad = 0f;
        CharacterCarryInventory carry = actor != null ? actor.GetComponent<CharacterCarryInventory>() : null;
        if (carry != null)
        {
            carryLoad = Mathf.Clamp01(carry.GetCurrentWeight() / Mathf.Max(1f, carry.GetBaseCarryLimit()));
        }

        float workPriority = 0f;
        float haulPriority = 0f;
        float huntPriority = 0f;
        AbilityWork work = null;
        bool isWorker = actor != null && actor.TryGetAbility(out work);
        bool isOffDuty = isWorker && work.IsOffDuty;
        if (isWorker && work.WorkPriorities != null)
        {
            workPriority = GetPriority01(
                work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Operate));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Restock)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Construct)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Repair)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Clean)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Craft)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Reception)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.DrawWater)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Cook)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Treat)));
            workPriority = Mathf.Max(
                workPriority,
                GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Refuel)));
            haulPriority = GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul));
            huntPriority = GetPriority01(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Hunt));
        }

        AbilityShopping shopping = null;
        bool hasShoppingAbility =
            actor != null && actor.TryGetAbility(out shopping);
        bool canLookAround = false;
        bool shouldExitDungeon = false;
        if (!isWorker && hasShoppingAbility)
        {
            shopping.GetDecisionState(
                out canLookAround,
                out shouldExitDungeon);
        }
        RecordCaptureStage(
            recorder,
            AiPerformanceCategory.DecisionContextAbilities,
            ref stageStarted);
        CharacterAiMemoryRuntime memory = actor != null ? actor.AiMemory : null;
        float momentum = memory != null ? memory.GetMomentumScore(branch) : 0f;
        CharacterAiWorldSignalSnapshot worldSignals = actor?.WorldSignalQuery?.Capture(actor, branch)
            ?? CharacterAiWorldSignalSnapshot.Neutral;
        RecordCaptureStage(
            recorder,
            AiPerformanceCategory.DecisionContextWorldSignal,
            ref stageStarted);
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
            hungerUrgency,
            sleepUrgency,
            excretionUrgency,
            hygieneUrgency,
            funUrgency,
            thirstUrgency,
            expeditionStressUrgency,
            carryLoad,
            workPriority,
            haulPriority,
            huntPriority,
            isWorker,
            isOffDuty,
            hasShoppingAbility,
            canLookAround,
            shouldExitDungeon,
            foodPressure,
            waterPressure,
            Mathf.Clamp01(0.5f + worldSignals.PathConfidence * 0.12f - worldSignals.QueuePressure * 0.08f),
            worldSignals.ExteriorRisk,
            momentum,
            worldSignals);
    }

    private static void RecordCaptureStage(
        ICharacterAiPerformanceRecorder recorder,
        AiPerformanceCategory category,
        ref long stageStarted)
    {
        if (stageStarted == 0L)
        {
            return;
        }

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        recorder.Record(
            category,
            (now - stageStarted)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency);
        stageStarted = now;
    }

    public CharacterAiUtilityBreakdown CreateRoutineBreakdown(
        CharacterAiBranch branch,
        float basePriority01)
    {
        CharacterAiIntentionType intention = CharacterAiUtilityText.GetIntention(branch);
        CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
            intention,
            CharacterAiUtilityText.GetBranchLabel(branch),
            Actor == null || Actor.ShouldCollectDetailedAiDiagnostics);
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
                breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Max(MoodUrgency, FunUrgency), 0.35f, "기분/재미");
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

    public float CalculateRoutineScore01(
        CharacterAiBranch branch,
        float basePriority01)
    {
        float weightedScore = 0f;
        float totalWeight = 0f;
        switch (branch)
        {
            case CharacterAiBranch.SurvivalNeeds:
                AddWeighted(ref weightedScore, ref totalWeight, EmergencyScore, 0.45f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Max(FoodStockPressure, WaterStockPressure),
                    0.2f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Max(HealthUrgency, InjuryUrgency),
                    0.15f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - WeatherPressure),
                    0.06f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - NearbyWildlifeThreat),
                    0.06f);
                break;
            case CharacterAiBranch.DutyWork:
                AddWeighted(ref weightedScore, ref totalWeight, WorkPriority, 0.35f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - EmergencyScore),
                    0.25f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    GetPersonalityScore(branch),
                    0.2f);
                AddWeighted(ref weightedScore, ref totalWeight, ScheduleScore, 0.08f);
                AddWeighted(ref weightedScore, ref totalWeight, PathConfidence, 0.06f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - RecentFailurePressure),
                    0.06f);
                break;
            case CharacterAiBranch.LeisureVisit:
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Max(MoodUrgency, FunUrgency),
                    0.35f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - EmergencyScore),
                    0.2f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    GetPersonalityScore(branch),
                    0.2f);
                AddWeighted(ref weightedScore, ref totalWeight, SocialOpportunity, 0.06f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - QueuePressure),
                    0.05f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - WeatherPressure),
                    0.04f);
                break;
            case CharacterAiBranch.Idle:
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - Mathf.Max(basePriority01, EmergencyScore)),
                    0.45f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(0.5f + MemoryMomentum),
                    0.2f);
                AddWeighted(ref weightedScore, ref totalWeight, SocialOpportunity, 0.08f);
                AddWeighted(ref weightedScore, ref totalWeight, QueuePressure, 0.04f);
                AddWeighted(
                    ref weightedScore,
                    ref totalWeight,
                    Mathf.Clamp01(1f - WeatherPressure),
                    0.04f);
                break;
            default:
                AddWeighted(ref weightedScore, ref totalWeight, basePriority01, 0.5f);
                break;
        }

        AddWeighted(
            ref weightedScore,
            ref totalWeight,
            Mathf.Clamp01(0.5f + MemoryMomentum),
            0.1f);
        float contextualScore = totalWeight > 0f
            ? Mathf.Clamp01(weightedScore / totalWeight)
            : 0f;
        return Mathf.Lerp(basePriority01, contextualScore, 0.35f);
    }

    private static void AddWeighted(
        ref float weightedScore,
        ref float totalWeight,
        float score,
        float weight)
    {
        weightedScore += Mathf.Clamp01(score) * Mathf.Max(0f, weight);
        totalWeight += Mathf.Max(0f, weight);
    }

    public float GetPriorityScore(CharacterAiBranch branch)
    {
        return branch switch
        {
            CharacterAiBranch.Work => WorkPriority,
            CharacterAiBranch.Wait => Mathf.Clamp01(1f - EmergencyScore),
            CharacterAiBranch.Eat => HungerUrgency,
            CharacterAiBranch.Rest => RestUrgency,
            CharacterAiBranch.Toilet => ExcretionUrgency,
            CharacterAiBranch.Hygiene => Mathf.Max(HygieneUrgency, ExpeditionStressUrgency * 0.75f),
            CharacterAiBranch.Shopping => ShoppingUrgency,
            CharacterAiBranch.LookAround => Mathf.Max(0.25f, FunUrgency),
            CharacterAiBranch.ExitDungeon => Mathf.Clamp01(MoodUrgency + 0.1f),
            _ => Mathf.Clamp01(1f - EmergencyScore)
        };
    }

    public float GetFacilityNeedScore(FacilityRole role)
    {
        if (role == FacilityRole.Meal)
        {
            return HungerUrgency;
        }

        if (role == FacilityRole.Rest)
        {
            return RestUrgency;
        }

        if (role == FacilityRole.Toilet)
        {
            return ExcretionUrgency;
        }

        if (role == FacilityRole.Hygiene)
        {
            return Mathf.Max(HygieneUrgency, ExpeditionStressUrgency * 0.75f);
        }

        if (role == FacilityRole.Purchase)
        {
            return ShoppingUrgency;
        }

        if (role == FacilityRole.Training || role == FacilityRole.Research)
        {
            return FunUrgency;
        }

        if (role == FacilityRole.Mana)
        {
            return MoodUrgency;
        }

        return 0.5f;
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
