using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Wait", order = 0)]
public class AIWait : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Wait,
        "대기",
        CharacterAiActionTags.Patience);

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    [SerializeField] private float minDuration = 0.5f;
    [SerializeField] private float maxDuration = 1.2f;
    [SerializeField, Range(0f, 1f)] private float onDutyWorkAvailableScore = 0.15f;
    [SerializeField, Range(0f, 1f)] private float offDutyVisitAvailableScore = 0.1f;

    public override bool RequiresDestination => false;

    public override IReadOnlyCollection<string> GetSemanticTags(
        CharacterActor actor)
    {
        HashSet<string> tags = new(
            base.GetSemanticTags(actor),
            StringComparer.Ordinal);
        if (actor?.InjurySeverity > 0.001f)
            tags.Add("medical:rest-treatment");
        if (actor != null
            && actor.WorldRegistry != null
            && !actor.WorldRegistry.Characters.Any(other =>
                other != null
                && other != actor
                && !other.IsDead
                && Vector2Int.Distance(
                    actor.GetNowXY(),
                    other.GetNowXY()) <= 2f))
            tags.Add("rest:private");
        return tags;
    }

    public override void PrepareScoreContext(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        if (actor != null && actor.TryGetAbility(out AbilityWork work))
        {
            work.SeedDecisionContext(in context);
        }
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        bool hasWork = CharacterWorkRoleUtility.TryGetWork(
            actor,
            out AbilityWork work);
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot = new(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasWorkRole: hasWork,
            isOffDuty: hasWork && work.IsOffDuty,
            workUtility: hasWork && !work.IsOffDuty
                ? work.GetAnyWorkUtilityScore(null)
                : 0f,
            hasOffDutyVisitCandidate: hasWork
                && work.IsOffDuty
                && HasOffDutyVisitCandidate(actor, null));
        return DungeonStory.AI.AIWait.AdjustScore(
            snapshot,
            baseScore,
            onDutyWorkAvailableScore,
            offDutyVisitAvailableScore);
    }

    public override bool CanStart(CharacterActor actor)
    {
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot =
            AiDecisionSceneSnapshotFactory.CaptureBase(actor);
        return DungeonStory.AI.AIWait.CanStart(snapshot);
    }

    public override void Execute(CharacterActor actor)
    {
        if (CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
            && (work.IsOffDuty || work.ShouldUseRestProtection()))
        {
            float recovery = Mathf.Max(0f, work.RestRecoveryOnWait);
            work.RecoverOffDuty(recovery, recovery, recovery, 0f);
        }

        float minimumWait = Mathf.Max(0.75f, minDuration);
        float maximumWait = Mathf.Max(minimumWait, maxDuration);
        float duration = actor?.Brain != null
            ? actor.Brain.NextRandom(minimumWait, maximumWait)
            : minimumWait;
        bool survivalNeedDue = CharacterNeedAiThresholds
            .TryGetMostUrgentSurvivalRoutineNeed(
                actor,
                out CharacterCondition survivalCondition,
                out float survivalUtility,
                out float survivalValue,
                out float survivalThreshold);
        string behaviorName;
        string failureReason;
        bool ranIdleBehavior = survivalNeedDue
            ? IdleBehaviorRunner.TryRunStatic(
                actor,
                duration,
                out behaviorName,
                out failureReason)
            : IdleBehaviorRunner.TryRunDefault(
                actor,
                duration,
                true,
                out behaviorName,
                out failureReason);
        if (ranIdleBehavior)
        {
            if (survivalNeedDue)
            {
                actor?.Brain?.SetActionPhase(
                    "생존 욕구 시설 대기",
                    detail: $"{survivalCondition}={survivalValue:0.###}/{survivalThreshold:0.###}; utility={survivalUtility:0.###}");
                return;
            }

            string phaseDetail = CharacterMoodImpulseUtility.ShouldPreferAutonomousIdle(actor, out string moodReason)
                ? moodReason
                : "다음 행동을 찾으며 움직이는 중";
            actor?.Brain?.SetActionPhase(behaviorName, detail: phaseDetail);
            return;
        }

        actor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Wait,
            CharacterActivityOutcomes.Blocked,
            $"대기 이동 불가: {failureReason}",
            actionId: "wait:idle-behavior",
            reasonCode: "idle-movement-unavailable",
            sentiment: -0.35f,
            bubbleEligible: true));
        if (IdleBehaviorRunner.TryRunStatic(
            actor,
            duration,
            out behaviorName,
            out failureReason))
        {
            actor?.Brain?.SetActionPhase("갈 곳 찾는 중", detail: "이동 가능한 칸을 다시 확인하는 중");
            return;
        }

        actor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Wait,
            CharacterActivityOutcomes.Failed,
            $"대기 실패: {failureReason}",
            actionId: "wait:idle-behavior",
            reasonCode: "idle-behavior-failed",
            sentiment: -0.5f,
            bubbleEligible: true));

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }

    private static bool HasOffDutyVisitCandidate(CharacterActor actor, GridPathSearchResult searchResult)
    {
        if (actor == null)
        {
            return false;
        }

        FacilityRole interestRoles = actor.TryGetAbility(out AbilityShopping shopping)
            ? shopping.GetInterestRoles()
            : CharacterVisitPolicy.CustomerInterestRoles;
        return FacilityCandidateScorer.HasCandidate(actor, searchResult, FacilityRole.Meal)
            || FacilityCandidateScorer.HasCandidate(actor, searchResult, FacilityRole.Rest)
            || FacilityCandidateScorer.HasCandidate(actor, searchResult, interestRoles);
    }
}
