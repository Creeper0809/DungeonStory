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

    public override bool IsContinuous => true;
    public override float MinimumDuration => 0.5f;

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
        return FacilityCandidateScorer.SelectBest(
            actor,
            candidates,
            role,
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
            role,
            FacilityScoringContext.RequireFromActor(actor),
            out destination,
            out bool pending))
        {
            failure = AIActionFailure.None;
            return true;
        }

        DungeonStory.AI.AiActionDecision decision =
            DungeonStory.AI.AIFacilityRoleAction.ResolveDestination(
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

    private bool CanUseVisitorAction(CharacterActor actor)
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
            facilityNeed: GetOnDutySelfCareNeed(actor, role));
        return DungeonStory.AI.AIFacilityRoleAction.CanStart(snapshot);
    }

    private static float GetOnDutySelfCareNeed(CharacterActor actor, FacilityRole role)
    {
        return CharacterNeedAiThresholds.GetFacilityRoutineUtility(actor, role);
    }
}
