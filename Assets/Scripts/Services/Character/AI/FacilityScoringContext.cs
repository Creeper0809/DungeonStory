using System;

public readonly struct FacilityScoringContext
{
    private readonly ISocialReputationBiasService reputationBiasService;
    private readonly IRoomFacilityPolicy roomFacilityPolicy;
    private readonly bool ignoreReputationBias;

    public FacilityScoringContext(
        ISocialReputationBiasService reputationBiasService,
        IRoomFacilityPolicy roomFacilityPolicy)
    {
        this.reputationBiasService = reputationBiasService
            ?? throw new ArgumentNullException(nameof(reputationBiasService));
        this.roomFacilityPolicy = roomFacilityPolicy
            ?? throw new ArgumentNullException(nameof(roomFacilityPolicy));
        ignoreReputationBias = false;
    }

    private FacilityScoringContext(
        IRoomFacilityPolicy roomFacilityPolicy,
        bool ignoreReputationBias)
    {
        reputationBiasService = null;
        this.roomFacilityPolicy = roomFacilityPolicy
            ?? throw new ArgumentNullException(nameof(roomFacilityPolicy));
        this.ignoreReputationBias = ignoreReputationBias;
    }

    public bool IsConfigured => roomFacilityPolicy != null
        && (ignoreReputationBias || reputationBiasService != null);

    public static FacilityScoringContext WithoutReputationBiasForIsolatedTest(
        IRoomFacilityPolicy roomFacilityPolicy)
    {
        return new FacilityScoringContext(roomFacilityPolicy, true);
    }

    public static FacilityScoringContext RequireFromActor(CharacterActor actor)
    {
        if (actor == null || actor.Brain == null)
        {
            throw new InvalidOperationException(
                $"{nameof(FacilityScoringContext)} requires an actor with {nameof(AIBrain)}.");
        }

        return actor.Brain.RequireFacilityScoringContext();
    }

    public float GetReputationBias(CharacterActor actor, BuildableObject building)
    {
        if (ignoreReputationBias)
        {
            return 0f;
        }

        if (reputationBiasService == null)
        {
            throw new InvalidOperationException(
                $"{nameof(FacilityScoringContext)} requires {nameof(ISocialReputationBiasService)}.");
        }

        return reputationBiasService.GetFacilityUtilityBias(actor, building);
    }

    public bool IsFacilityRoleAvailable(
        BuildableObject building,
        FacilityRole requestedRole,
        out string rejectReason)
    {
        return RequireRoomFacilityPolicy()
            .IsFacilityRoleAvailable(building, requestedRole, out rejectReason);
    }

    public float GetRoomUtilityScore(BuildableObject building, FacilityRole role)
    {
        return RequireRoomFacilityPolicy().GetRoomUtilityScore(building, role);
    }

    private IRoomFacilityPolicy RequireRoomFacilityPolicy()
    {
        return roomFacilityPolicy
            ?? throw new InvalidOperationException(
                $"{nameof(FacilityScoringContext)} requires {nameof(IRoomFacilityPolicy)}.");
    }
}
