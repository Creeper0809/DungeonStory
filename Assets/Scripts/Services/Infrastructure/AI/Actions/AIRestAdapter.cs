using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Rest", order = 0)]
public class AIRest : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Rest,
        "휴식",
        CharacterAiActionTags.SelfCare);

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool IsContinuous => true;
    public override float MinimumDuration => 0.5f;
    public override bool CanStart(CharacterActor actor)
    {
        return CanUseVisitorAction(actor);
    }

    public override bool CanStart(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        return CanStart(actor);
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.GetAbility<AbilityShopping>()?.StartSopping();
    }

    public override bool CanContinue(
        CharacterActor actor,
        AIAction runningAction,
        out string stopReason)
    {
        stopReason = string.Empty;
        return actor != null
            && runningAction != null
            && runningAction.HasStarted
            && actor.TryGetAbility(out AbilityShopping _);
    }

    public override IReadOnlyList<BuildableObject> GetDestinationCandidates(
        CharacterActor actor,
        GridPathSearchResult searchResult)
    {
        if (actor == null)
        {
            return Array.Empty<BuildableObject>();
        }

        return FacilityCandidateScorer.GetCandidates(
            actor,
            searchResult,
            FacilityRole.Rest);
    }

    public override BuildableObject SelectDestination(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> candidates)
    {
        return FacilityCandidateScorer.SelectBest(
            actor,
            candidates,
            FacilityRole.Rest,
            null,
            FacilityScoringContext.RequireFromActor(actor));
    }

    public override bool TryResolveDestinationWithFailure(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        if (FacilityCandidateScorer.TrySelectBestIncremental(
            actor,
            searchResult,
            FacilityRole.Rest,
            FacilityScoringContext.RequireFromActor(actor),
            out destination,
            out bool pending))
        {
            failure = AIActionFailure.None;
            return true;
        }

        DungeonStory.AI.AiActionDecision decision =
            DungeonStory.AI.AIRest.ResolveDestination(false, pending);
        failure = AIActionFailure.Create(decision.FailureKind, decision.Detail);
        return false;
    }

    public override bool TryReserveDestination(
        CharacterActor actor,
        BuildableObject destination,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (destination == null)
        {
            return true;
        }

        if (destination.TryReserveVisit(actor, out string failureReason))
        {
            return true;
        }

        failure = AIActionFailure.Create(
            AIActionFailureKind.DestinationOccupied,
            failureReason,
            destination);
        return false;
    }

    public override void RefreshDestinationReservation(CharacterActor actor, BuildableObject destination)
    {
        destination?.RefreshVisitReservation(actor);
    }

    public override void ReleaseDestinationReservation(CharacterActor actor, BuildableObject destination)
    {
        destination?.ReleaseVisitReservation(actor);
    }

    public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)
    {
        if (actor != null && actor.TryGetAbility(out AbilityShopping shopping))
        {
            shopping.StopShopping(reason);
        }
    }

    private static bool CanUseVisitorAction(CharacterActor actor)
    {
        bool hasShopping = actor != null
            && actor.TryGetAbility(out AbilityShopping _);
        bool hasWork = CharacterWorkRoleUtility.TryGetWork(
            actor,
            out AbilityWork work);
        bool restProtection = hasWork
            && !work.IsOffDuty
            && work.ShouldUseRestProtection();
        float sleepUtility = hasWork
            && !work.IsOffDuty
            && !restProtection
                ? CharacterNeedAiThresholds.GetRoutineUtility(
                    actor,
                    CharacterCondition.SLEEP)
                : 0f;
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot = new(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasShopping: hasShopping,
            hasWorkRole: hasWork,
            isOffDuty: hasWork && work.IsOffDuty,
            shouldUseRestProtection: restProtection,
            sleepUtility: sleepUtility,
            expeditionRecoveryNeed: hasWork
                && !work.IsOffDuty
                && !restProtection
                && sleepUtility <= 0f
                ? FacilityCandidateScorer.GetExpeditionRecoveryNeed(actor)
                : 0f);
        return DungeonStory.AI.AIRest.CanStart(snapshot);
    }
}
