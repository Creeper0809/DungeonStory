using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct CharacterAiActionCandidate
{
    public CharacterAiActionCandidate(
        AIAction action,
        float score,
        AIActionFailure failure,
        string debugLabel,
        BuildableObject destination = null)
    {
        Action = action;
        Score = Mathf.Clamp01(score);
        Failure = failure;
        DebugLabel = debugLabel ?? string.Empty;
        Destination = destination;
    }

    public AIAction Action { get; }
    public AIActionSet ActionSet => Action != null ? Action.actionset : null;
    public float Score { get; }
    public AIActionFailure Failure { get; }
    public string DebugLabel { get; }
    public BuildableObject Destination { get; }
    public bool HasAction => Action != null && Action.actionset != null && Score > 0f;
}

public readonly struct CharacterAiJobCandidate
{
    public CharacterAiJobCandidate(
        CharacterAiBranch branch,
        string jobGiverName,
        CharacterAiActionCandidate actionCandidate,
        float domainScore,
        float utility,
        string reason,
        string breakdownSummary = "")
    {
        Branch = branch;
        JobGiverName = jobGiverName ?? string.Empty;
        ActionCandidate = actionCandidate;
        DomainScore = Mathf.Clamp01(domainScore);
        Utility = Mathf.Clamp01(utility);
        Reason = reason ?? string.Empty;
        BreakdownSummary = breakdownSummary ?? string.Empty;
    }

    public CharacterAiBranch Branch { get; }
    public string JobGiverName { get; }
    public CharacterAiActionCandidate ActionCandidate { get; }
    public float DomainScore { get; }
    public float Utility { get; }
    public string Reason { get; }
    public string BreakdownSummary { get; }
    public bool IsValid => Utility > 0f && ActionCandidate.HasAction;
    public string DebugSummary =>
        $"{JobGiverName} domain={DomainScore:0.###} action={ActionCandidate.Score:0.###} utility={Utility:0.###} {Reason} {BreakdownSummary}".Trim();
}

public abstract class CharacterAiJobGiver
{
    private readonly Predicate<AIActionSet> actionMatcher;

    protected CharacterAiJobGiver()
    {
        actionMatcher = MatchesAction;
    }

    public abstract CharacterAiBranch Branch { get; }
    public abstract string Name { get; }
    public virtual FacilityRole RequiredFacilityRoles => FacilityRole.None;

    public bool TryEvaluate(CharacterActor actor, out CharacterAiJobCandidate candidate)
    {
        CharacterAiDecisionContext context = CharacterAiDecisionContext.Capture(actor, Branch);
        return TryEvaluate(actor, in context, out candidate);
    }

    public bool TryEvaluate(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out CharacterAiJobCandidate candidate)
    {
        bool captureDetails = actor == null || actor.ShouldCollectDetailedAiDiagnostics;
        float domainScore = EvaluateDomain(
            actor,
            in context,
            captureDetails,
            out string domainReason);
        return TryEvaluate(
            actor,
            in context,
            domainScore,
            domainReason,
            out candidate);
    }

    internal float EvaluateDomain(
        CharacterActor actor,
        bool captureDetails,
        out string domainReason)
    {
        CharacterAiDecisionContext context =
            CharacterAiDecisionContext.Capture(actor, Branch);
        return EvaluateDomain(
            actor,
            in context,
            ResolveAvailableFacilityRoles(actor),
            captureDetails,
            out domainReason);
    }

    internal float EvaluateDomain(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        bool captureDetails,
        out string domainReason)
    {
        return EvaluateDomain(
            actor,
            in context,
            ResolveAvailableFacilityRoles(actor),
            captureDetails,
            out domainReason);
    }

    internal float EvaluateDomain(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        FacilityRole availableFacilityRoles,
        bool captureDetails,
        out string domainReason)
    {
        FacilityRole requiredRoles = RequiredFacilityRoles;
        if (requiredRoles != FacilityRole.None
            && (availableFacilityRoles & requiredRoles) == FacilityRole.None)
        {
            domainReason = "required facility role unavailable";
            return 0f;
        }

        float domainScore = GetDomainScore(
            actor,
            in context,
            out domainReason);
        domainScore = CharacterMoodImpulseUtility.ApplyJobGiverBias(
            actor,
            Branch,
            domainScore,
            out string moodReason,
            captureDetails);
        if (captureDetails)
        {
            domainReason = CharacterMoodImpulseUtility.AppendReason(
                domainReason,
                moodReason);
        }

        return Mathf.Clamp01(domainScore);
    }

    internal static FacilityRole ResolveAvailableFacilityRoles(
        CharacterActor actor)
    {
        AIBrain brain = actor != null ? actor.Brain : null;
        if (brain == null || !brain.TryGetRuntimeGrid(out Grid grid))
        {
            return FacilityRole.None;
        }

        return brain.RequireFacilityCandidateCache().GetAvailableRoles(grid);
    }

    internal bool TryEvaluate(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        float domainScore,
        string domainReason,
        out CharacterAiJobCandidate candidate)
    {
        bool captureDetails = actor == null || actor.ShouldCollectDetailedAiDiagnostics;
        if (captureDetails)
        {
            actor?.Blackboard?.RecordDecisionContext(context);
        }

        if (domainScore <= 0f)
        {
            candidate = CreateRejected(domainScore, domainReason);
            RecordRejectedBreakdown(actor, context, domainReason);
            return false;
        }

        AIBrain brain = actor != null ? actor.Brain : null;
        if (brain == null)
        {
            candidate = CreateRejected(domainScore, "AIBrain is missing.");
            RecordRejectedBreakdown(actor, context, "AIBrain is missing.");
            return false;
        }

        if (!brain.TryFindBestScoredAction(
                actionMatcher,
                in context,
                out CharacterAiActionCandidate actionCandidate))
        {
            candidate = CreateRejected(
                domainScore,
                !captureDetails
                    ? "실행 가능한 행동 없음"
                    : string.IsNullOrWhiteSpace(actionCandidate.DebugLabel)
                        ? actionCandidate.Failure.ToString()
                        : actionCandidate.DebugLabel,
                actionCandidate);
            RecordRejectedBreakdown(actor, context, candidate.Reason);
            return false;
        }

        CharacterAiUtilityBreakdown breakdown = captureDetails
            ? CreateBreakdown(
                actor,
                context,
                domainScore,
                actionCandidate.Score,
                actionCandidate.ActionSet)
            : null;
        float contextScore = breakdown != null
            ? breakdown.CalculateWeighted01()
            : CalculateContextScore(
                actor,
                context,
                domainScore,
                actionCandidate.Score,
                actionCandidate.ActionSet);
        float utility = CombineUtility(domainScore, actionCandidate.Score);
        utility = Mathf.Clamp01(utility * Mathf.Lerp(0.88f, 1.12f, contextScore));
        breakdown?.SetFinalScore(utility);
        if (captureDetails)
        {
            actor?.Blackboard?.RecordUtilityBreakdown(breakdown);
        }
        candidate = new CharacterAiJobCandidate(
            Branch,
            Name,
            actionCandidate,
            domainScore,
            utility,
            domainReason,
            breakdown != null ? breakdown.ToCompactString() : string.Empty);
        return candidate.IsValid;
    }

    public virtual bool MatchesAction(AIActionSet actionSet)
    {
        return actionSet != null && actionSet.Branch == Branch;
    }

    protected abstract float GetDomainScore(CharacterActor actor, out string reason);

    protected virtual float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        return GetDomainScore(actor, out reason);
    }

    protected virtual float CombineUtility(float domainScore, float actionScore)
    {
        return Mathf.Clamp01(domainScore * actionScore);
    }

    public static float Need(CharacterActor actor, CharacterCondition condition)
    {
        if (CharacterNeedCatalog.TryGet(condition, out CharacterNeedDefinition definition))
        {
            return definition.GetUrgency(actor);
        }

        CharacterStats stats = actor != null ? actor.Stats : null;
        if (stats == null
            || stats.Stats == null
            || !stats.Stats.TryGetValue(condition, out float value))
        {
            return 0.5f;
        }

        return Mathf.Clamp01(1f - value / 100f);
    }

    public static float StatRatio(CharacterActor actor, CharacterCondition condition)
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        if (stats == null
            || stats.Stats == null
            || !stats.Stats.TryGetValue(condition, out float value))
        {
            return 0.5f;
        }

        return Mathf.Clamp01(value / 100f);
    }

    protected static float InterestMultiplier(CharacterActor actor, AIActionSet actionSet)
    {
        return Mathf.Clamp01(CharacterAiPersonalityUtility.GetActionScoreMultiplier(actor, actionSet) / 2f);
    }

    private CharacterAiJobCandidate CreateRejected(
        float domainScore,
        string reason,
        CharacterAiActionCandidate actionCandidate = default)
    {
        return new CharacterAiJobCandidate(
            Branch,
            Name,
            actionCandidate,
            domainScore,
            0f,
            reason);
    }

    private CharacterAiUtilityBreakdown CreateBreakdown(
        CharacterActor actor,
        CharacterAiDecisionContext context,
        float domainScore,
        float actionScore,
        AIActionSet actionSet)
    {
        CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
            CharacterAiUtilityText.GetIntention(Branch),
            CharacterAiUtilityText.GetBranchLabel(Branch),
            actor == null || actor.ShouldCollectDetailedAiDiagnostics);
        float memoryScore = actor != null && actor.AiMemory != null
            ? Mathf.Clamp01(0.5f + actor.AiMemory.GetMomentumScore(Branch))
            : 0.5f;
        breakdown.Add(CharacterAiUtilityFactorKind.Need, domainScore, 0.28f, "욕구 강도");
        breakdown.Add(CharacterAiUtilityFactorKind.Priority, context.GetPriorityScore(Branch), 0.18f, "현재 우선순위");
        breakdown.Add(CharacterAiUtilityFactorKind.Personality, context.GetPersonalityScore(Branch), 0.13f, "성격 영향");
        breakdown.Add(CharacterAiUtilityFactorKind.Memory, memoryScore, 0.12f, "최근 행동");
        breakdown.Add(CharacterAiUtilityFactorKind.Reservation, actionScore, 0.2f, "실행 가능성");
        breakdown.Add(CharacterAiUtilityFactorKind.Queue, Mathf.Clamp01(1f - context.QueuePressure), 0.04f, "혼잡 회피");
        breakdown.Add(CharacterAiUtilityFactorKind.Weather, Mathf.Clamp01(1f - context.WeatherPressure), 0.03f, "날씨 부담");
        breakdown.Add(CharacterAiUtilityFactorKind.PathConfidence, context.PathConfidence, 0.04f, "경로 신뢰");
        breakdown.Add(CharacterAiUtilityFactorKind.Schedule, context.ScheduleScore, 0.04f, "일정 흐름");
        breakdown.Add(CharacterAiUtilityFactorKind.Fatigue, Mathf.Clamp01(1f - context.RecentFailurePressure), 0.03f, "최근 실패");
        breakdown.Add(
            CharacterAiUtilityFactorKind.Momentum,
            actionSet != null && actor?.Blackboard?.CommittedAction == actionSet ? 1f : 0.5f,
            0.09f,
            "유지 보너스");
        return breakdown;
    }

    private float CalculateContextScore(
        CharacterActor actor,
        CharacterAiDecisionContext context,
        float domainScore,
        float actionScore,
        AIActionSet actionSet)
    {
        float memoryScore = actor != null && actor.AiMemory != null
            ? Mathf.Clamp01(0.5f + actor.AiMemory.GetMomentumScore(Branch))
            : 0.5f;
        float momentumScore = actionSet != null
            && actor?.Blackboard?.CommittedAction == actionSet
                ? 1f
                : 0.5f;
        const float totalWeight = 1.18f;
        float weightedScore =
            domainScore * 0.28f
            + context.GetPriorityScore(Branch) * 0.18f
            + context.GetPersonalityScore(Branch) * 0.13f
            + memoryScore * 0.12f
            + actionScore * 0.2f
            + Mathf.Clamp01(1f - context.QueuePressure) * 0.04f
            + Mathf.Clamp01(1f - context.WeatherPressure) * 0.03f
            + context.PathConfidence * 0.04f
            + context.ScheduleScore * 0.04f
            + Mathf.Clamp01(1f - context.RecentFailurePressure) * 0.03f
            + momentumScore * 0.09f;
        return Mathf.Clamp01(weightedScore / totalWeight);
    }

    private void RecordRejectedBreakdown(
        CharacterActor actor,
        CharacterAiDecisionContext context,
        string reason)
    {
        if (actor != null && !actor.ShouldCollectDetailedAiDiagnostics)
        {
            return;
        }

        CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
            CharacterAiUtilityText.GetIntention(Branch),
            CharacterAiUtilityText.GetBranchLabel(Branch));
        breakdown.Add(CharacterAiUtilityFactorKind.Need, context.GetPriorityScore(Branch), 0.5f, "기본 욕구");
        breakdown.Reject(reason);
        actor?.Blackboard?.RecordUtilityBreakdown(breakdown);
    }
}

public static class CharacterAiRoutinePriority
{
    private const float SevereNeed = 0.65f;
    private const float MildNeed = 0.25f;

    public static float GetPriority(
        CharacterActor actor,
        CharacterAiBranch routineBranch,
        out string reason)
    {
        CharacterAiDecisionContext context =
            CharacterAiDecisionContext.Capture(actor, routineBranch);
        return GetPriority(actor, routineBranch, in context, out reason);
    }

    public static float GetPriority(
        CharacterActor actor,
        CharacterAiBranch routineBranch,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        if (actor == null || !actor.CanRunAi)
        {
            reason = "AI cannot run";
            return 0f;
        }

        float priority = routineBranch switch
        {
            CharacterAiBranch.SurvivalNeeds => GetSurvivalPriority(actor, in context, out reason),
            CharacterAiBranch.DutyWork => GetDutyPriority(actor, in context, out reason),
            CharacterAiBranch.LeisureVisit => GetLeisurePriority(actor, in context, out reason),
            CharacterAiBranch.Idle => GetIdlePriority(actor, out reason),
            _ => ReturnNoPriority(out reason)
        };

        priority = CharacterMoodImpulseUtility.ApplyRoutineBias(
            actor,
            routineBranch,
            priority,
            out string moodReason,
            actor.ShouldCollectDetailedAiDiagnostics);
        if (actor.ShouldCollectDetailedAiDiagnostics)
        {
            reason = CharacterMoodImpulseUtility.AppendReason(reason, moodReason);
        }
        bool captureDetails = actor.ShouldCollectDetailedAiDiagnostics;
        CharacterAiUtilityBreakdown breakdown = captureDetails
            ? context.CreateRoutineBreakdown(
                routineBranch,
                Mathf.Clamp01(priority / 100f))
            : null;
        float contextualPriority = breakdown != null
            ? breakdown.FinalScore01
            : context.CalculateRoutineScore01(
                routineBranch,
                Mathf.Clamp01(priority / 100f));
        priority = Mathf.Lerp(priority, contextualPriority * 100f, 0.25f);
        if (captureDetails)
        {
            actor.Blackboard?.RecordUtilityBreakdown(breakdown);
            reason = CharacterMoodImpulseUtility.AppendReason(reason, breakdown.ToCompactString(3));
        }
        return priority;
    }

    private static float GetSurvivalPriority(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        bool captureDetails = actor.ShouldCollectDetailedAiDiagnostics;
        float registeredNeed = context.StrongestNeedUrgency;
        float restNeed = context.RestUrgency;
        float recoveryNeed = context.ExpeditionRecoveryUrgency;
        float exitNeed = context.ShouldExitDungeon ? 1f : 0f;
        float strongestNeed = Mathf.Max(exitNeed, registeredNeed);
        strongestNeed = Mathf.Max(strongestNeed, restNeed);
        strongestNeed = Mathf.Max(strongestNeed, recoveryNeed);
        reason = captureDetails
            ? $"need={strongestNeed:0.###} strongest={context.StrongestNeed} rest={restNeed:0.###} exit={exitNeed:0.###}"
            : "생존 욕구";
        if (strongestNeed <= 0.05f)
        {
            return 0f;
        }

        if (strongestNeed >= SevereNeed)
        {
            return 95f + strongestNeed * 5f;
        }

        if (strongestNeed >= MildNeed)
        {
            return 35f + strongestNeed * 30f;
        }

        return strongestNeed * 25f;
    }

    private static float GetDutyPriority(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        if (!context.IsWorker)
        {
            reason = "not a worker";
            return 0f;
        }

        if (context.IsOffDuty)
        {
            reason = "off duty";
            return 0f;
        }

        float survivalPressure = GetSurvivalPressure(in context);
        float wellness = 1f - Mathf.Clamp01((survivalPressure - MildNeed) / (1f - MildNeed));
        float priority = Mathf.Lerp(8f, 82f, wellness);
        reason = actor.ShouldCollectDetailedAiDiagnostics
            ? $"onDuty survival={survivalPressure:0.###} wellness={wellness:0.###}"
            : "당직 업무";
        return priority;
    }

    private static float GetLeisurePriority(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        if (!CanUseLeisure(in context))
        {
            reason = "leisure unavailable";
            return 0f;
        }

        float funNeed = context.FunUrgency;
        float moodNeed = context.MoodUrgency;
        float shoppingNeed = context.ShoppingUrgency;
        float urgentSurvival = context.StrongestNeedUrgency;
        float leisureNeed = Mathf.Max(funNeed, moodNeed * 0.75f, shoppingNeed);
        float survivalWindow = Mathf.Clamp01(1f - urgentSurvival * 0.85f);
        float priority = Mathf.Clamp01(leisureNeed * survivalWindow) * 70f;
        reason = actor.ShouldCollectDetailedAiDiagnostics
            ? $"leisure={leisureNeed:0.###} fun={funNeed:0.###} mood={moodNeed:0.###} shopping={shoppingNeed:0.###} survivalWindow={survivalWindow:0.###}"
            : "여가 욕구";
        return priority;
    }

    private static float GetIdlePriority(CharacterActor actor, out string reason)
    {
        reason = "no stronger routine";
        return actor != null && actor.CanRunAi ? 1f : 0f;
    }

    private static float ReturnNoPriority(out string reason)
    {
        reason = "unsupported routine";
        return 0f;
    }

    private static float GetSurvivalPressure(
        in CharacterAiDecisionContext context)
    {
        return Mathf.Max(
            Mathf.Max(
                context.StrongestNeedUrgency,
                context.MoodUrgency * 0.8f),
            context.ExpeditionRecoveryUrgency);
    }

    private static bool CanUseLeisure(
        in CharacterAiDecisionContext context)
    {
        if (context.Actor == null)
        {
            return false;
        }

        if (context.IsWorker)
        {
            return context.IsOffDuty;
        }

        return context.HasShoppingAbility;
    }
}

public sealed class ExitDungeonJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.ExitDungeon;
    public override string Name => "ExitDungeonJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        bool shouldExit = actor != null
            && actor.CanRunAi
            && !CharacterWorkRoleUtility.TryGetWork(actor, out _)
            && actor.TryGetAbility(out AbilityShopping shopping)
            && shopping.ShouldExitDungeon();
        reason = shouldExit ? "exit intent" : "no exit intent";
        return shouldExit ? 1f : 0f;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        reason = context.ShouldExitDungeon ? "exit intent" : "no exit intent";
        return context.ShouldExitDungeon ? 1f : 0f;
    }
}

public sealed class GetFoodJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Eat;
    public override string Name => "GetFoodJobGiver";
    public override FacilityRole RequiredFacilityRoles => FacilityRole.Meal;

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float hungerNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Meal);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"hungerNeed={hungerNeed:0.###}"
            : "허기";
        return hungerNeed;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        float hungerNeed = context.HungerUrgency;
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"hungerNeed={hungerNeed:0.###}"
            : "허기";
        return hungerNeed;
    }
}

public sealed class RestJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Rest;
    public override string Name => "RestJobGiver";
    public override FacilityRole RequiredFacilityRoles => FacilityRole.Rest;

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float restNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Rest);
        float recoveryNeed = FacilityCandidateScorer.GetExpeditionRecoveryNeed(actor);
        float domain = Mathf.Max(restNeed, recoveryNeed * 0.95f);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"restNeed={restNeed:0.###} recovery={recoveryNeed:0.###}"
            : "휴식";
        return domain;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        float restNeed = context.RestUrgency;
        float recoveryNeed = context.ExpeditionRecoveryUrgency;
        float domain = Mathf.Max(restNeed, recoveryNeed * 0.95f);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"restNeed={restNeed:0.###} recovery={recoveryNeed:0.###}"
            : "휴식";
        return domain;
    }
}

public sealed class ToiletJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Toilet;
    public override string Name => "ToiletJobGiver";
    public override FacilityRole RequiredFacilityRoles => FacilityRole.Toilet;

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float toiletNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Toilet);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"toiletNeed={toiletNeed:0.###}"
            : "배변";
        return toiletNeed;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        float toiletNeed = context.ExcretionUrgency;
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"toiletNeed={toiletNeed:0.###}"
            : "배변";
        return toiletNeed;
    }
}

public sealed class HygieneJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Hygiene;
    public override string Name => "HygieneJobGiver";
    public override FacilityRole RequiredFacilityRoles => FacilityRole.Hygiene;

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float hygieneNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Hygiene);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"hygieneNeed={hygieneNeed:0.###}"
            : "위생";
        return hygieneNeed;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        float hygieneNeed = context.GetFacilityNeedScore(FacilityRole.Hygiene);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"hygieneNeed={hygieneNeed:0.###}"
            : "위생";
        return hygieneNeed;
    }
}

public sealed class WorkJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Work;
    public override string Name => "WorkJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        if (!CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work))
        {
            reason = "not a worker";
            return 0f;
        }

        if (work.IsOffDuty)
        {
            reason = "off duty";
            return 0f;
        }

        float survivalPressure = Mathf.Max(
            Need(actor, CharacterCondition.HUNGER),
            Need(actor, CharacterCondition.SLEEP),
            Need(actor, CharacterCondition.EXCRETION),
            Need(actor, CharacterCondition.HYGIENE) * 0.7f,
            Need(actor, CharacterCondition.MOOD) * 0.8f,
            FacilityCandidateScorer.GetExpeditionRecoveryNeed(actor));
        float wellness = 1f - Mathf.Clamp01((survivalPressure - 0.25f) / 0.75f);
        float domain = Mathf.Lerp(0.2f, 1f, wellness);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"onDuty survivalPressure={survivalPressure:0.###} wellness={wellness:0.###}"
            : "당직 업무";
        return domain;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        if (!context.IsWorker)
        {
            reason = "not a worker";
            return 0f;
        }

        if (context.IsOffDuty)
        {
            reason = "off duty";
            return 0f;
        }

        float survivalPressure = Mathf.Max(
            context.HungerUrgency,
            context.SleepUrgency);
        survivalPressure = Mathf.Max(
            survivalPressure,
            context.ExcretionUrgency);
        survivalPressure = Mathf.Max(
            survivalPressure,
            context.HygieneUrgency * 0.7f);
        survivalPressure = Mathf.Max(
            survivalPressure,
            context.MoodUrgency * 0.8f);
        survivalPressure = Mathf.Max(
            survivalPressure,
            context.ExpeditionRecoveryUrgency);
        float wellness = 1f - Mathf.Clamp01((survivalPressure - 0.25f) / 0.75f);
        float domain = Mathf.Lerp(0.2f, 1f, wellness);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"onDuty survivalPressure={survivalPressure:0.###} wellness={wellness:0.###}"
            : "당직 업무";
        return domain;
    }
}

public sealed class ShoppingJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Shopping;
    public override string Name => "ShoppingJobGiver";
    public override FacilityRole RequiredFacilityRoles => FacilityRole.Purchase;

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float visitNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Purchase);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"visitNeed={visitNeed:0.###}"
            : "방문 욕구";
        return visitNeed;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        float visitNeed = context.ShoppingUrgency;
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"visitNeed={visitNeed:0.###}"
            : "방문 욕구";
        return visitNeed;
    }
}

public sealed class LookAroundJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.LookAround;
    public override string Name => "LookAroundJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float hungerNeed = Need(actor, CharacterCondition.HUNGER);
        float sleepNeed = Need(actor, CharacterCondition.SLEEP);
        float excretionNeed = Need(actor, CharacterCondition.EXCRETION);
        float hygieneNeed = Need(actor, CharacterCondition.HYGIENE);
        float funNeed = Need(actor, CharacterCondition.FUN);
        float moodNeed = Need(actor, CharacterCondition.MOOD);
        float urgentNeed = Mathf.Max(hungerNeed, sleepNeed, excretionNeed, hygieneNeed * 0.7f);
        float curiosityWindow = Mathf.Clamp01(1f - urgentNeed);
        float domain = Mathf.Clamp01((0.15f + funNeed * 0.35f + moodNeed * 0.2f) * curiosityWindow);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"curiosityWindow={curiosityWindow:0.###} funNeed={funNeed:0.###} moodNeed={moodNeed:0.###}"
            : "둘러보기";
        return domain;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        float urgentNeed = Mathf.Max(
            context.HungerUrgency,
            context.SleepUrgency);
        urgentNeed = Mathf.Max(urgentNeed, context.ExcretionUrgency);
        urgentNeed = Mathf.Max(
            urgentNeed,
            context.HygieneUrgency * 0.7f);
        float curiosityWindow = Mathf.Clamp01(1f - urgentNeed);
        float domain = Mathf.Clamp01(
            (0.15f
             + context.FunUrgency * 0.35f
             + context.MoodUrgency * 0.2f)
            * curiosityWindow);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"curiosityWindow={curiosityWindow:0.###} funNeed={context.FunUrgency:0.###} moodNeed={context.MoodUrgency:0.###}"
            : "둘러보기";
        return domain;
    }
}

public sealed class WaitJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Wait;
    public override string Name => "WaitJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        if (!CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work))
        {
            reason = "not a worker";
            return 0f;
        }

        float strongestNeed = Mathf.Max(
            Need(actor, CharacterCondition.HUNGER),
            Need(actor, CharacterCondition.SLEEP),
            Need(actor, CharacterCondition.FUN),
            Need(actor, CharacterCondition.MOOD),
            Need(actor, CharacterCondition.EXCRETION),
            Need(actor, CharacterCondition.HYGIENE));
        float domain = work.IsOffDuty
            ? Mathf.Clamp01(0.35f + strongestNeed * 0.35f)
            : Mathf.Clamp01(0.05f + (1f - strongestNeed) * 0.25f);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"waitWindow={domain:0.###} strongestNeed={strongestNeed:0.###}"
            : "대기";
        return domain;
    }

    protected override float GetDomainScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        out string reason)
    {
        if (!context.IsWorker)
        {
            reason = "not a worker";
            return 0f;
        }

        float strongestNeed = Mathf.Max(
            context.HungerUrgency,
            context.SleepUrgency);
        strongestNeed = Mathf.Max(strongestNeed, context.FunUrgency);
        strongestNeed = Mathf.Max(strongestNeed, context.MoodUrgency);
        strongestNeed = Mathf.Max(strongestNeed, context.ExcretionUrgency);
        strongestNeed = Mathf.Max(strongestNeed, context.HygieneUrgency);
        float domain = context.IsOffDuty
            ? Mathf.Clamp01(0.35f + strongestNeed * 0.35f)
            : Mathf.Clamp01(0.05f + (1f - strongestNeed) * 0.25f);
        reason = actor != null && actor.ShouldCollectDetailedAiDiagnostics
            ? $"waitWindow={domain:0.###} strongestNeed={strongestNeed:0.###}"
            : "대기";
        return domain;
    }
}

public interface ICharacterAiJobGiverCatalog
{
    CharacterAiJobGiver ExitDungeon { get; }
    CharacterAiJobGiver GetFood { get; }
    CharacterAiJobGiver Rest { get; }
    CharacterAiJobGiver Toilet { get; }
    CharacterAiJobGiver Hygiene { get; }
    CharacterAiJobGiver Work { get; }
    CharacterAiJobGiver Shopping { get; }
    CharacterAiJobGiver LookAround { get; }
    CharacterAiJobGiver Wait { get; }
    CharacterAiJobGiver Get(CharacterAiBranch branch);
}

public sealed class CharacterAiJobGiverCatalog : ICharacterAiJobGiverCatalog
{
    private readonly Dictionary<CharacterAiBranch, CharacterAiJobGiver> jobGivers =
        new Dictionary<CharacterAiBranch, CharacterAiJobGiver>();

    public CharacterAiJobGiverCatalog()
    {
        Register(new ExitDungeonJobGiver());
        Register(new GetFoodJobGiver());
        Register(new RestJobGiver());
        Register(new ToiletJobGiver());
        Register(new HygieneJobGiver());
        Register(new WorkJobGiver());
        Register(new ShoppingJobGiver());
        Register(new LookAroundJobGiver());
        Register(new WaitJobGiver());
    }

    public CharacterAiJobGiver ExitDungeon => Get(CharacterAiBranch.ExitDungeon);
    public CharacterAiJobGiver GetFood => Get(CharacterAiBranch.Eat);
    public CharacterAiJobGiver Rest => Get(CharacterAiBranch.Rest);
    public CharacterAiJobGiver Toilet => Get(CharacterAiBranch.Toilet);
    public CharacterAiJobGiver Hygiene => Get(CharacterAiBranch.Hygiene);
    public CharacterAiJobGiver Work => Get(CharacterAiBranch.Work);
    public CharacterAiJobGiver Shopping => Get(CharacterAiBranch.Shopping);
    public CharacterAiJobGiver LookAround => Get(CharacterAiBranch.LookAround);
    public CharacterAiJobGiver Wait => Get(CharacterAiBranch.Wait);

    public CharacterAiJobGiver Get(CharacterAiBranch branch)
    {
        return jobGivers.TryGetValue(branch, out CharacterAiJobGiver jobGiver)
            ? jobGiver
            : null;
    }

    public void Register(CharacterAiJobGiver jobGiver, bool replace = false)
    {
        if (jobGiver == null)
        {
            throw new ArgumentNullException(nameof(jobGiver));
        }

        if (jobGiver.Branch == CharacterAiBranch.None)
        {
            throw new InvalidOperationException("AI job givers require a concrete branch.");
        }

        if (!replace && jobGivers.ContainsKey(jobGiver.Branch))
        {
            throw new InvalidOperationException(
                $"An AI job giver is already registered for {jobGiver.Branch}.");
        }

        jobGivers[jobGiver.Branch] = jobGiver;
    }
}
