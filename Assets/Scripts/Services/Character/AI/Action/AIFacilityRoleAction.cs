using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Facility Role", order = 0)]
public class AIFacilityRoleAction : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ToiletDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Toilet,
        "용변",
        CharacterAiActionTags.SelfCare);
    private static readonly CharacterAiActionDescriptor HygieneDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Hygiene,
        "위생",
        CharacterAiActionTags.SelfCare);
    private static readonly CharacterAiActionDescriptor GenericDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.LeisureVisit,
        "시설 이용",
        CharacterAiActionTags.SelfCare);

    [SerializeField] private FacilityRole role;

    public override CharacterAiActionDescriptor Descriptor => role switch
    {
        FacilityRole.Toilet => ToiletDescriptor,
        FacilityRole.Hygiene => HygieneDescriptor,
        _ => GenericDescriptor
    };

    public FacilityRole Role
    {
        get => role;
        set => role = value;
    }

    public override bool CanStart(CharacterActor actor)
    {
        return CanUseVisitorAction(actor);
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.GetAbility<AbilityShopping>()?.StartSopping();
    }

    public override IReadOnlyList<BuildableObject> GetDestinationCandidates(
        CharacterActor actor,
        GridPathSearchResult searchResult)
    {
        if (actor == null || role == FacilityRole.None)
        {
            return Array.Empty<BuildableObject>();
        }

        return FacilityCandidateScorer.GetCandidates(actor, searchResult, role);
    }

    public override BuildableObject SelectDestination(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> candidates)
    {
        GridPathSearchResult searchResult = actor != null && actor.Brain != null
            ? actor.Brain.GetPathSearch(actor)
            : null;
        return FacilityCandidateScorer.SelectBest(
            actor,
            candidates,
            role,
            searchResult,
            FacilityScoringContext.RequireFromActor(actor));
    }

    public override bool TryResolveDestinationWithFailure(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        if (FacilityCandidateScorer.TrySelectBest(
            actor,
            searchResult,
            role,
            FacilityScoringContext.RequireFromActor(actor),
            out destination))
        {
            failure = AIActionFailure.None;
            return true;
        }

        failure = AIActionFailure.Create(AIActionFailureKind.NoDestination);
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

    private bool CanUseVisitorAction(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (!actor.TryGetAbility(out AbilityShopping _))
        {
            return false;
        }

        if (CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work))
        {
            return work.IsOffDuty || CanUseOnDutySelfCare(actor, role);
        }

        return true;
    }

    private static bool CanUseOnDutySelfCare(CharacterActor actor, FacilityRole role)
    {
        if ((role & FacilityRole.Hygiene) != 0
            && FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Hygiene) >= 0.1f)
        {
            return true;
        }

        if ((role & FacilityRole.Toilet) != 0
            && FacilityCandidateScorer.GetNeedScore(actor, FacilityRole.Toilet) >= 0.1f)
        {
            return true;
        }

        return false;
    }
}
