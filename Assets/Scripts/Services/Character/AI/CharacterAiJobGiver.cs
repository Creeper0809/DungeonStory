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
        string debugLabel)
    {
        Action = action;
        Score = Mathf.Clamp01(score);
        Failure = failure;
        DebugLabel = debugLabel ?? string.Empty;
    }

    public AIAction Action { get; }
    public AIActionSet ActionSet => Action != null ? Action.actionset : null;
    public float Score { get; }
    public AIActionFailure Failure { get; }
    public string DebugLabel { get; }
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
    public abstract CharacterAiBranch Branch { get; }
    public abstract string Name { get; }

    public bool TryEvaluate(CharacterActor actor, out CharacterAiJobCandidate candidate)
    {
        CharacterAiDecisionContext context = CharacterAiDecisionContext.Capture(actor, Branch);
        actor?.Blackboard?.RecordDecisionContext(context);
        float domainScore = GetDomainScore(actor, out string domainReason);
        domainScore = CharacterMoodImpulseUtility.ApplyJobGiverBias(
            actor,
            Branch,
            domainScore,
            out string moodReason);
        domainReason = CharacterMoodImpulseUtility.AppendReason(domainReason, moodReason);
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

        if (!brain.TryFindBestScoredAction(MatchesAction, out CharacterAiActionCandidate actionCandidate))
        {
            candidate = CreateRejected(
                domainScore,
                string.IsNullOrWhiteSpace(actionCandidate.DebugLabel)
                    ? actionCandidate.Failure.ToString()
                    : actionCandidate.DebugLabel);
            RecordRejectedBreakdown(actor, context, candidate.Reason);
            return false;
        }

        CharacterAiUtilityBreakdown breakdown = CreateBreakdown(
            actor,
            context,
            domainScore,
            actionCandidate.Score,
            actionCandidate.ActionSet);
        float contextScore = breakdown.CalculateWeighted01();
        float utility = CombineUtility(domainScore, actionCandidate.Score);
        utility = Mathf.Clamp01(utility * Mathf.Lerp(0.88f, 1.12f, contextScore));
        breakdown.SetFinalScore(utility);
        actor?.Blackboard?.RecordUtilityBreakdown(breakdown);
        candidate = new CharacterAiJobCandidate(
            Branch,
            Name,
            actionCandidate,
            domainScore,
            utility,
            domainReason,
            breakdown.ToCompactString());
        return candidate.IsValid;
    }

    public virtual bool MatchesAction(AIActionSet actionSet)
    {
        return actionSet != null && actionSet.Branch == Branch;
    }

    protected abstract float GetDomainScore(CharacterActor actor, out string reason);

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

    private CharacterAiJobCandidate CreateRejected(float domainScore, string reason)
    {
        return new CharacterAiJobCandidate(
            Branch,
            Name,
            default,
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
            CharacterAiUtilityText.GetBranchLabel(Branch));
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

    private void RecordRejectedBreakdown(
        CharacterActor actor,
        CharacterAiDecisionContext context,
        string reason)
    {
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
        if (actor == null || !actor.CanRunAi)
        {
            reason = "AI cannot run";
            return 0f;
        }

        float priority = routineBranch switch
        {
            CharacterAiBranch.SurvivalNeeds => GetSurvivalPriority(actor, out reason),
            CharacterAiBranch.DutyWork => GetDutyPriority(actor, out reason),
            CharacterAiBranch.LeisureVisit => GetLeisurePriority(actor, out reason),
            CharacterAiBranch.Idle => GetIdlePriority(actor, out reason),
            _ => ReturnNoPriority(out reason)
        };

        priority = CharacterMoodImpulseUtility.ApplyRoutineBias(
            actor,
            routineBranch,
            priority,
            out string moodReason);
        reason = CharacterMoodImpulseUtility.AppendReason(reason, moodReason);
        CharacterAiDecisionContext context = CharacterAiDecisionContext.Capture(actor, routineBranch);
        CharacterAiUtilityBreakdown breakdown = context.CreateRoutineBreakdown(
            routineBranch,
            Mathf.Clamp01(priority / 100f));
        priority = Mathf.Lerp(priority, breakdown.FinalScore01 * 100f, 0.25f);
        actor.Blackboard?.RecordUtilityBreakdown(breakdown);
        reason = CharacterMoodImpulseUtility.AppendReason(reason, breakdown.ToCompactString(3));
        return priority;
    }

    private static float GetSurvivalPriority(CharacterActor actor, out string reason)
    {
        IReadOnlyList<CharacterNeedDefinition> survivalNeeds = CharacterNeedCatalog.All
            .Where(definition => definition.HasTag(CharacterNeedTag.Survival))
            .ToArray();
        float registeredNeed = survivalNeeds
            .Select(definition => definition.GetUrgency(actor))
            .DefaultIfEmpty(0f)
            .Max();
        float restNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Rest);
        float recoveryNeed = FacilityCandidateScorer.GetExpeditionRecoveryNeed(actor);
        float exitNeed = ShouldExitDungeon(actor) ? 1f : 0f;
        float strongestNeed = Mathf.Max(
            exitNeed,
            registeredNeed,
            restNeed,
            recoveryNeed);
        string needDetails = string.Join(
            " ",
            survivalNeeds.Select(definition =>
                $"{definition.Id}={definition.GetUrgency(actor):0.###}"));
        reason = $"need={strongestNeed:0.###} {needDetails} rest={restNeed:0.###} exit={exitNeed:0.###}";
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

    private static float GetDutyPriority(CharacterActor actor, out string reason)
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

        float survivalPressure = GetSurvivalPressure(actor);
        float wellness = 1f - Mathf.Clamp01((survivalPressure - MildNeed) / (1f - MildNeed));
        float priority = Mathf.Lerp(8f, 82f, wellness);
        reason = $"onDuty survival={survivalPressure:0.###} wellness={wellness:0.###}";
        return priority;
    }

    private static float GetLeisurePriority(CharacterActor actor, out string reason)
    {
        if (!CanUseLeisure(actor))
        {
            reason = "leisure unavailable";
            return 0f;
        }

        float funNeed = CharacterAiJobGiver.Need(actor, CharacterCondition.FUN);
        float moodNeed = CharacterAiJobGiver.Need(actor, CharacterCondition.MOOD);
        float shoppingNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Purchase);
        float urgentSurvival = CharacterNeedCatalog.GetStrongestUrgency(
            actor,
            CharacterNeedTag.Survival);
        float leisureNeed = Mathf.Max(funNeed, moodNeed * 0.75f, shoppingNeed);
        float survivalWindow = Mathf.Clamp01(1f - urgentSurvival * 0.85f);
        float priority = Mathf.Clamp01(leisureNeed * survivalWindow) * 70f;
        reason = $"leisure={leisureNeed:0.###} fun={funNeed:0.###} mood={moodNeed:0.###} shopping={shoppingNeed:0.###} survivalWindow={survivalWindow:0.###}";
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

    private static float GetSurvivalPressure(CharacterActor actor)
    {
        return Mathf.Max(
            CharacterNeedCatalog.GetStrongestUrgency(actor, CharacterNeedTag.Survival),
            CharacterAiJobGiver.Need(actor, CharacterCondition.MOOD) * 0.8f,
            FacilityCandidateScorer.GetExpeditionRecoveryNeed(actor));
    }

    private static bool CanUseLeisure(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work))
        {
            return work.IsOffDuty;
        }

        return actor.TryGetAbility(out AbilityShopping _);
    }

    private static bool ShouldExitDungeon(CharacterActor actor)
    {
        return actor != null
            && !CharacterWorkRoleUtility.TryGetWork(actor, out _)
            && actor.TryGetAbility(out AbilityShopping shopping)
            && shopping.ShouldExitDungeon();
    }
}

public sealed class ExitDungeonJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.ExitDungeon;
    public override string Name => "ExitDungeonJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        reason = "exit intent";
        return actor != null && actor.CanRunAi ? 1f : 0f;
    }
}

public sealed class GetFoodJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Eat;
    public override string Name => "GetFoodJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float hungerNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Meal);
        reason = $"hungerNeed={hungerNeed:0.###}";
        return hungerNeed;
    }
}

public sealed class RestJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Rest;
    public override string Name => "RestJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float restNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Rest);
        float recoveryNeed = FacilityCandidateScorer.GetExpeditionRecoveryNeed(actor);
        float domain = Mathf.Max(restNeed, recoveryNeed * 0.95f);
        reason = $"restNeed={restNeed:0.###} recovery={recoveryNeed:0.###}";
        return domain;
    }
}

public sealed class ToiletJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Toilet;
    public override string Name => "ToiletJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float toiletNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Toilet);
        reason = $"toiletNeed={toiletNeed:0.###}";
        return toiletNeed;
    }
}

public sealed class HygieneJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Hygiene;
    public override string Name => "HygieneJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float hygieneNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Hygiene);
        reason = $"hygieneNeed={hygieneNeed:0.###}";
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
        reason = $"onDuty survivalPressure={survivalPressure:0.###} wellness={wellness:0.###}";
        return domain;
    }
}

public sealed class ShoppingJobGiver : CharacterAiJobGiver
{
    public override CharacterAiBranch Branch => CharacterAiBranch.Shopping;
    public override string Name => "ShoppingJobGiver";

    protected override float GetDomainScore(CharacterActor actor, out string reason)
    {
        float visitNeed = FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Purchase);
        reason = $"visitNeed={visitNeed:0.###}";
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
        reason = $"curiosityWindow={curiosityWindow:0.###} funNeed={funNeed:0.###} moodNeed={moodNeed:0.###}";
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
        reason = $"waitWindow={domain:0.###} strongestNeed={strongestNeed:0.###}";
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
