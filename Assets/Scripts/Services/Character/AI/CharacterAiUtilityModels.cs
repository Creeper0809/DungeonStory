using System.Collections.Generic;
using System;
using System.Linq;
using Unity.Profiling;
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
        string reason = CharacterAiUtilityText.ResolveDisplayToken(Reason);
        return string.IsNullOrWhiteSpace(reason)
            ? CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Factor.Format.Score",
                label,
                Score)
            : CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Factor.Format.ScoreWithReason",
                label,
                Score,
                reason);
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
            : CharacterAiUtilityText.ResolveDisplayToken(CandidateLabel);
        if (!string.IsNullOrWhiteSpace(RejectionReason))
        {
            return CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Breakdown.Rejected",
                candidate,
                RejectionReason);
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
            ? CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Breakdown.Score",
                candidate,
                FinalScore01 * 100f)
            : CharacterAiDiagnosticsTextQuery.Get(
                "CharacterAI.Breakdown.ScoreWithFactors",
                candidate,
                FinalScore01 * 100f,
                factorText);
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
        return CharacterAiDiagnosticsTextQuery.Get(
            "CharacterAI.Breakdown.Multiline",
            firstLine,
            string.Join("\n", rows));
    }
}

public readonly struct CharacterAiDecisionContext
{
    private static readonly ProfilerMarker CaptureMarker =
        new ProfilerMarker("CharacterAi.DecisionContext");
    private static readonly ProfilerMarker NeedsCaptureMarker =
        new ProfilerMarker("CharacterAi.DecisionContext.Needs");
    private static readonly ProfilerMarker CarryCaptureMarker =
        new ProfilerMarker("CharacterAi.DecisionContext.Carry");
    private static readonly ProfilerMarker WorkCaptureMarker =
        new ProfilerMarker("CharacterAi.DecisionContext.WorkProfile");
    private static readonly ProfilerMarker VisitorCaptureMarker =
        new ProfilerMarker("CharacterAi.DecisionContext.VisitorProfile");
    private static readonly ProfilerMarker WorldSignalCaptureMarker =
        new ProfilerMarker("CharacterAi.DecisionContext.WorldSignal");
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
        using (CaptureMarker.Auto())
        {
        ICharacterAiPerformanceRecorder recorder =
            actor?.Brain?.PerformanceRecorder;
        bool collectTimings = recorder?.DetailedCollectionEnabled == true;
        long stageStarted = collectTimings
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long stageAllocatedAtStart = collectTimings
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        NeedsCaptureMarker.Begin();
        CharacterCondition strongest = CharacterCondition.HUNGER;
        float strongestUrgency = 0f;
        ICharacterNeedDefinitionCatalog needCatalog = actor != null && actor.Stats != null
            ? actor.Stats.NeedDefinitionCatalog
            : null;
        if (needCatalog != null && needCatalog.TryGetStrongestUrgency(
                actor,
                CharacterNeedTag.Survival,
                out CharacterNeedDefinition strongestDefinition,
                out float weightedUrgency))
        {
            strongest = strongestDefinition.Condition;
            strongestUrgency = weightedUrgency;
        }

        float moodUrgency = needCatalog?.GetUrgency(actor, CharacterCondition.MOOD) ?? 0.5f;
        float hungerUrgency = needCatalog?.GetUrgency(actor, CharacterCondition.HUNGER) ?? 0.5f;
        float sleepUrgency = needCatalog?.GetUrgency(actor, CharacterCondition.SLEEP) ?? 0.5f;
        float excretionUrgency = needCatalog?.GetUrgency(actor, CharacterCondition.EXCRETION) ?? 0.5f;
        float hygieneUrgency = needCatalog?.GetUrgency(actor, CharacterCondition.HYGIENE) ?? 0.5f;
        float funUrgency = needCatalog?.GetUrgency(actor, CharacterCondition.FUN) ?? 0.5f;
        float thirstUrgency = needCatalog?.GetUrgency(actor, CharacterCondition.THIRST) ?? 0.5f;
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
        NeedsCaptureMarker.End();
        RecordCaptureStage(
            recorder,
            AiPerformanceCategory.DecisionContextNeeds,
            ref stageStarted,
            ref stageAllocatedAtStart);

        CarryCaptureMarker.Begin();
        float carryLoad = 0f;
        CharacterCarryInventory carry = actor != null
            ? actor.CarryInventory
            : null;
        if (carry != null)
        {
            carryLoad = Mathf.Clamp01(carry.GetCurrentWeight() / Mathf.Max(1f, carry.GetBaseCarryLimit()));
        }
        CarryCaptureMarker.End();

        WorkCaptureMarker.Begin();
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
        WorkCaptureMarker.End();

        VisitorCaptureMarker.Begin();
        AbilityShopping shopping = null;
        bool hasShoppingAbility =
            actor != null && actor.TryGetAbility(out shopping);
        bool canLookAround = false;
        bool shouldExitDungeon = false;
        if (!isWorker && hasShoppingAbility)
        {
            // Keep the shared decision snapshot cheap. A precise visitor-state
            // query walks usable facilities and used to run for every routine
            // decision, even while the visitor still had ordinary shopping
            // work available. Destination actions perform that exact query
            // only when their branch is actually evaluated.
            shouldExitDungeon = shopping.HasNoRemainingVisits;
        }
        VisitorCaptureMarker.End();
        RecordCaptureStage(
            recorder,
            AiPerformanceCategory.DecisionContextAbilities,
            ref stageStarted,
            ref stageAllocatedAtStart);
        CharacterAiMemoryRuntime memory = actor != null ? actor.AiMemory : null;
        float momentum = memory != null ? memory.GetMomentumScore(branch) : 0f;
        WorldSignalCaptureMarker.Begin();
        CharacterAiWorldSignalSnapshot worldSignals = actor?.WorldSignalQuery?.Capture(actor, branch)
            ?? CharacterAiWorldSignalSnapshot.Neutral;
        WorldSignalCaptureMarker.End();
        RecordCaptureStage(
            recorder,
            AiPerformanceCategory.DecisionContextWorldSignal,
            ref stageStarted,
            ref stageAllocatedAtStart);
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
    }

    private static void RecordCaptureStage(
        ICharacterAiPerformanceRecorder recorder,
        AiPerformanceCategory category,
        ref long stageStarted,
        ref long stageAllocatedAtStart)
    {
        if (stageStarted == 0L)
        {
            return;
        }

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        long allocatedNow = GC.GetAllocatedBytesForCurrentThread();
        recorder.Record(
            category,
            (now - stageStarted)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency,
            Math.Max(0L, allocatedNow - stageAllocatedAtStart));
        stageStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        stageAllocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
    }

    public CharacterAiUtilityBreakdown CreateRoutineBreakdown(
        CharacterAiBranch branch,
        float basePriority01)
    {
        CharacterAiIntentionType intention = CharacterAiUtilityText.GetIntention(branch);
        CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
            intention,
            CharacterAiUtilityText.GetBranchDisplayToken(branch),
            Actor == null || Actor.ShouldCollectDetailedAiDiagnostics);
        switch (branch)
        {
            case CharacterAiBranch.SurvivalNeeds:
                breakdown.Add(CharacterAiUtilityFactorKind.Need, EmergencyScore, 0.45f, GetNeedLabel());
                breakdown.Add(CharacterAiUtilityFactorKind.Stock, Mathf.Max(FoodStockPressure, WaterStockPressure), 0.2f, "CharacterAI.Reason.SurvivalStock");
                breakdown.Add(CharacterAiUtilityFactorKind.Risk, Mathf.Max(HealthUrgency, InjuryUrgency), 0.15f, "CharacterAI.Reason.Health");
                breakdown.Add(CharacterAiUtilityFactorKind.Weather, Mathf.Clamp01(1f - WeatherPressure), 0.06f, "CharacterAI.Reason.WeatherBurden");
                breakdown.Add(CharacterAiUtilityFactorKind.Risk, Mathf.Clamp01(1f - NearbyWildlifeThreat), 0.06f, "CharacterAI.Reason.WildlifeThreat");
                break;
            case CharacterAiBranch.DutyWork:
                breakdown.Add(CharacterAiUtilityFactorKind.Priority, WorkPriority, 0.35f, "CharacterAI.Reason.WorkPriority");
                breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Clamp01(1f - EmergencyScore), 0.25f, "CharacterAI.Reason.WorkCapacity");
                breakdown.Add(CharacterAiUtilityFactorKind.Personality, GetPersonalityScore(branch), 0.2f, "CharacterAI.Reason.Diligence");
                breakdown.Add(CharacterAiUtilityFactorKind.Schedule, ScheduleScore, 0.08f, "CharacterAI.Reason.WorkHours");
                breakdown.Add(CharacterAiUtilityFactorKind.PathConfidence, PathConfidence, 0.06f, "CharacterAI.Reason.PathConfidence");
                breakdown.Add(CharacterAiUtilityFactorKind.Fatigue, Mathf.Clamp01(1f - RecentFailurePressure), 0.06f, "CharacterAI.Reason.RecentFailure");
                break;
            case CharacterAiBranch.LeisureVisit:
                breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Max(MoodUrgency, FunUrgency), 0.35f, "CharacterAI.Reason.MoodAndFun");
                breakdown.Add(CharacterAiUtilityFactorKind.Risk, Mathf.Clamp01(1f - EmergencyScore), 0.2f, "CharacterAI.Reason.RiskCapacity");
                breakdown.Add(CharacterAiUtilityFactorKind.Personality, GetPersonalityScore(branch), 0.2f, "CharacterAI.Reason.Enjoyment");
                breakdown.Add(CharacterAiUtilityFactorKind.Social, SocialOpportunity, 0.06f, "CharacterAI.Reason.NearbyPeople");
                breakdown.Add(CharacterAiUtilityFactorKind.Queue, Mathf.Clamp01(1f - QueuePressure), 0.05f, "CharacterAI.Reason.Queue");
                breakdown.Add(CharacterAiUtilityFactorKind.Weather, Mathf.Clamp01(1f - WeatherPressure), 0.04f, "CharacterAI.Reason.Weather");
                break;
            case CharacterAiBranch.Idle:
                breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Clamp01(1f - Mathf.Max(basePriority01, EmergencyScore)), 0.45f, "CharacterAI.Reason.NoUrgentTask");
                breakdown.Add(CharacterAiUtilityFactorKind.Momentum, Mathf.Clamp01(0.5f + MemoryMomentum), 0.2f, "CharacterAI.Reason.NaturalMomentum");
                breakdown.Add(CharacterAiUtilityFactorKind.Social, SocialOpportunity, 0.08f, "CharacterAI.Reason.LightInteraction");
                breakdown.Add(CharacterAiUtilityFactorKind.Queue, QueuePressure, 0.04f, "CharacterAI.Reason.Queueing");
                breakdown.Add(CharacterAiUtilityFactorKind.Weather, Mathf.Clamp01(1f - WeatherPressure), 0.04f, "CharacterAI.Reason.WalkableWeather");
                break;
            default:
                breakdown.Add(CharacterAiUtilityFactorKind.Priority, basePriority01, 0.5f, "CharacterAI.Reason.BaseScore");
                break;
        }

        breakdown.Add(CharacterAiUtilityFactorKind.Momentum, Mathf.Clamp01(0.5f + MemoryMomentum), 0.1f, "CharacterAI.Reason.RecentFlow");
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
            CharacterAiBranch.Drink => ThirstUrgency,
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
        ICharacterNeedDefinitionCatalog needCatalog = Actor != null && Actor.Stats != null
            ? Actor.Stats.NeedDefinitionCatalog
            : null;
        return needCatalog != null
            && needCatalog.TryGet(StrongestNeed, out CharacterNeedDefinition definition)
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

    public static string GetBranchDisplayToken(CharacterAiBranch branch)
    {
        return branch switch
        {
            CharacterAiBranch.Critical => "CharacterAI.Branch.Critical",
            CharacterAiBranch.LockedAction => "CharacterAI.Branch.LockedAction",
            CharacterAiBranch.SoftLock => "CharacterAI.Branch.SoftLock",
            CharacterAiBranch.InterruptCheck => "CharacterAI.Branch.InterruptCheck",
            CharacterAiBranch.MacroGoal => "CharacterAI.Branch.MacroGoal",
            CharacterAiBranch.Emergency => "CharacterAI.Branch.Emergency",
            CharacterAiBranch.RoutineUtility => "CharacterAI.Branch.RoutineUtility",
            CharacterAiBranch.SurvivalNeeds => "CharacterAI.Branch.SurvivalNeeds",
            CharacterAiBranch.DutyWork => "CharacterAI.Branch.DutyWork",
            CharacterAiBranch.LeisureVisit => "CharacterAI.Branch.LeisureVisit",
            CharacterAiBranch.ExitDungeon => "CharacterAI.Branch.ExitDungeon",
            CharacterAiBranch.Eat => "CharacterAI.Branch.Eat",
            CharacterAiBranch.Rest => "CharacterAI.Branch.Rest",
            CharacterAiBranch.Work => "CharacterAI.Branch.Work",
            CharacterAiBranch.Shopping => "CharacterAI.Branch.Shopping",
            CharacterAiBranch.LookAround => "CharacterAI.Branch.LookAround",
            CharacterAiBranch.Wait => "CharacterAI.Branch.Wait",
            CharacterAiBranch.Idle => "CharacterAI.Branch.Idle",
            CharacterAiBranch.Toilet => "CharacterAI.Branch.Toilet",
            CharacterAiBranch.Hygiene => "CharacterAI.Branch.Hygiene",
            CharacterAiBranch.StopCurrent => "CharacterAI.Branch.StopCurrent",
            CharacterAiBranch.ContinueCurrent => "CharacterAI.Branch.ContinueCurrent",
            _ => branch.ToString()
        };
    }

    public static string GetBranchLabel(CharacterAiBranch branch) =>
        ResolveDisplayToken(GetBranchDisplayToken(branch));

    public static string GetIntentionLabel(CharacterAiIntentionType intention)
    {
        return intention switch
        {
            CharacterAiIntentionType.Survive => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Survive"),
            CharacterAiIntentionType.Recover => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Recover"),
            CharacterAiIntentionType.Work => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Work"),
            CharacterAiIntentionType.Logistics => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Logistics"),
            CharacterAiIntentionType.Guard => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Guard"),
            CharacterAiIntentionType.Hunt => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Hunt"),
            CharacterAiIntentionType.Leisure => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Leisure"),
            CharacterAiIntentionType.Social => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Social"),
            CharacterAiIntentionType.Shop => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Shop"),
            CharacterAiIntentionType.Exit => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Exit"),
            CharacterAiIntentionType.Idle => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.Idle"),
            _ => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Intention.None")
        };
    }

    public static string GetFactorLabel(CharacterAiUtilityFactorKind kind)
    {
        return kind switch
        {
            CharacterAiUtilityFactorKind.Need => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Need"),
            CharacterAiUtilityFactorKind.Priority => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Priority"),
            CharacterAiUtilityFactorKind.Personality => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Personality"),
            CharacterAiUtilityFactorKind.Memory => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Memory"),
            CharacterAiUtilityFactorKind.Distance => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Distance"),
            CharacterAiUtilityFactorKind.Risk => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Risk"),
            CharacterAiUtilityFactorKind.Room => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Room"),
            CharacterAiUtilityFactorKind.Stock => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Stock"),
            CharacterAiUtilityFactorKind.Crowd => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Crowd"),
            CharacterAiUtilityFactorKind.Reservation => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Reservation"),
            CharacterAiUtilityFactorKind.Momentum => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Momentum"),
            CharacterAiUtilityFactorKind.Queue => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Queue"),
            CharacterAiUtilityFactorKind.Social => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Social"),
            CharacterAiUtilityFactorKind.Weather => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Weather"),
            CharacterAiUtilityFactorKind.PathConfidence => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.PathConfidence"),
            CharacterAiUtilityFactorKind.Fatigue => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Fatigue"),
            CharacterAiUtilityFactorKind.Novelty => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Novelty"),
            CharacterAiUtilityFactorKind.Schedule => CharacterAiDiagnosticsTextQuery.Get("CharacterAI.Factor.Schedule"),
            _ => kind.ToString()
        };
    }

    public static string ResolveDisplayToken(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !value.StartsWith("CharacterAI.", StringComparison.Ordinal))
        {
            return value ?? string.Empty;
        }

        return CharacterAiDiagnosticsTextQuery.Get(value);
    }
}
