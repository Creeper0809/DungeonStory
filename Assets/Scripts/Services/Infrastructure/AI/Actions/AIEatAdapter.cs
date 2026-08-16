using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Eat", order = 0)]
public class AIEat : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Eat,
        "식사",
        CharacterAiActionTags.SelfCare,
        "work:eat");

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
        if (actor == null) return Array.Empty<BuildableObject>();

        return FacilityCandidateScorer.GetCandidates(
            actor,
            searchResult,
            FacilityRole.Meal);
    }

    public override BuildableObject SelectDestination(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> candidates)
    {
        return FacilityCandidateScorer.SelectBest(
            actor,
            candidates,
            FacilityRole.Meal,
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
            FacilityRole.Meal,
            FacilityScoringContext.RequireFromActor(actor),
            out destination,
            out bool pending))
        {
            failure = AIActionFailure.None;
            return true;
        }

        DungeonStory.AI.AiActionDecision decision =
            DungeonStory.AI.AIEat.ResolveDestination(false, pending);
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
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot = new(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasShopping: hasShopping,
            hasWorkRole: hasWork,
            isOffDuty: hasWork && work.IsOffDuty,
            hungerUtility: actor != null
                ? CharacterNeedAiThresholds.GetRoutineUtility(
                    actor,
                    CharacterCondition.HUNGER)
                : 0f);
        return DungeonStory.AI.AIEat.CanStart(snapshot);
    }
}
