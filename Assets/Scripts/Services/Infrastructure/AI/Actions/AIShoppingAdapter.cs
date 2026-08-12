using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Shopping", order = 0)]
public class AIShopping : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Shopping,
        "쇼핑",
        CharacterAiActionTags.Shopping);

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
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

    public override IReadOnlyList<BuildableObject> GetDestinationCandidates(
        CharacterActor actor,
        GridPathSearchResult searchResult)
    {
        AbilityShopping shopping = actor != null ? actor.GetAbility<AbilityShopping>() : null;
        if (shopping == null)
        {
            return Array.Empty<BuildableObject>();
        }

        return FacilityCandidateScorer.GetCandidates(
            actor,
            searchResult,
            shopping.GetInterestRoles());
    }

    public override BuildableObject SelectDestination(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> candidates)
    {
        if (actor == null || candidates == null || candidates.Count == 0)
        {
            return null;
        }

        AbilityShopping shopping = actor.GetAbility<AbilityShopping>();
        if (shopping == null)
        {
            return null;
        }

        return FacilityCandidateScorer.SelectBest(
            actor,
            candidates,
            shopping.GetInterestRoles(),
            null,
            FacilityScoringContext.RequireFromActor(actor));
    }

    public override bool TryResolveDestinationWithFailure(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        AbilityShopping shopping = actor != null ? actor.GetAbility<AbilityShopping>() : null;
        if (shopping == null)
        {
            destination = null;
            DungeonStory.AI.AiActionDecision unsupported =
                DungeonStory.AI.AIShopping.ResolveDestination(
                    false,
                    false,
                    false);
            failure = AIActionFailure.Create(
                unsupported.FailureKind,
                unsupported.Detail);
            return false;
        }

        if (FacilityCandidateScorer.TrySelectBestIncremental(
            actor,
            searchResult,
            shopping.GetInterestRoles(),
            FacilityScoringContext.RequireFromActor(actor),
            shopping.CanVisitCandidate,
            out destination,
            out bool pending))
        {
            shopping.RecordVisitableFacilitySearchResult(true);
            failure = AIActionFailure.None;
            return true;
        }

        if (!pending)
        {
            // The shopping action has already paid for the precise facility
            // scan. Publish that result to visitor-state arbitration so the
            // look-around and exit actions do not repeat the same scan in this
            // decision window.
            shopping.RecordVisitableFacilitySearchResult(false);
        }

        DungeonStory.AI.AiActionDecision decision =
            DungeonStory.AI.AIShopping.ResolveDestination(
                true,
                false,
                pending);
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

    private static bool CanUseVisitorAction(CharacterActor actor)
    {
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot =
            AiDecisionSceneSnapshotFactory.CaptureBase(actor);
        return DungeonStory.AI.AIShopping.CanStart(snapshot);
    }
}
