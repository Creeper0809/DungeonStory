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

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        if (!CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work))
        {
            return Mathf.Clamp01(baseScore);
        }

        if (work.IsOffDuty)
        {
            GridPathSearchResult searchResult = actor != null && actor.Brain != null
                ? actor.Brain.GetPathSearch(actor)
                : null;
            if (HasOffDutyVisitCandidate(actor, searchResult))
            {
                return Mathf.Clamp01(Mathf.Min(baseScore, offDutyVisitAvailableScore));
            }

            return Mathf.Clamp01(baseScore);
        }

        if (!work.IsOffDuty)
        {
            GridPathSearchResult searchResult = actor.Brain != null
                ? actor.Brain.GetPathSearch(actor)
                : null;
            if (work.GetAnyWorkUtilityScore(searchResult) > 0f)
            {
                return Mathf.Clamp01(Mathf.Min(baseScore, onDutyWorkAvailableScore));
            }
        }

        return Mathf.Clamp01(baseScore);
    }

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null && CharacterWorkRoleUtility.TryGetWork(actor, out _);
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
        bool ranIdleBehavior = IdleBehaviorRunner.TryRunDefault(
            actor,
            duration,
            true,
            out string behaviorName,
            out string failureReason);
        if (ranIdleBehavior)
        {
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
