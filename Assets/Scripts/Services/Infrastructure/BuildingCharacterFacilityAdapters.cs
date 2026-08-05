using System.Collections;

public static class BuildingCharacterFacilityAdapter
{
    public static IEnumerator Interact(this Shop facility, CharacterActor actor) =>
        Require(facility).Interact(actor?.BuildingVisitor);

    public static FacilityAssignmentStatus GetWorkerAssignmentStatus(
        this Shop facility,
        CharacterActor actor) =>
        Require(facility).GetWorkerAssignmentStatus(actor?.BuildingVisitor);

    public static bool CanAssignWorker(
        this Shop facility,
        CharacterActor actor,
        out string failureReason) =>
        Require(facility).CanAssignWorker(actor?.BuildingVisitor, out failureReason);

    public static IEnumerator AllocateWorker(this Shop facility, CharacterActor actor) =>
        Require(facility).AllocateWorker(actor?.BuildingVisitor);

    public static void DeallocateWorker(this Shop facility, CharacterActor actor) =>
        Require(facility).DeallocateWorker(actor?.BuildingVisitor);

    public static IEnumerator Interact(this Facility facility, CharacterActor actor) =>
        Require(facility).Interact(actor?.BuildingVisitor);

    public static FacilityAssignmentStatus GetWorkerAssignmentStatus(
        this Facility facility,
        CharacterActor actor) =>
        Require(facility).GetWorkerAssignmentStatus(actor?.BuildingVisitor);

    public static bool CanAssignWorker(
        this Facility facility,
        CharacterActor actor,
        out string failureReason) =>
        Require(facility).CanAssignWorker(actor?.BuildingVisitor, out failureReason);

    public static IEnumerator AllocateWorker(this Facility facility, CharacterActor actor) =>
        Require(facility).AllocateWorker(actor?.BuildingVisitor);

    public static void DeallocateWorker(this Facility facility, CharacterActor actor) =>
        Require(facility).DeallocateWorker(actor?.BuildingVisitor);

    public static IEnumerator Interact(this Stair stair, CharacterActor actor) =>
        Require(stair).Interact(actor?.BuildingVisitor);

    public static IEnumerator Traverse(
        this Stair stair,
        CharacterActor actor,
        GridMoveStep step) =>
        Require(stair).Traverse(actor?.BuildingVisitor, step);

    public static FacilityAssignmentStatus GetWorkerAssignmentStatus(
        this ConstructionSite constructionSite,
        CharacterActor actor) =>
        Require(constructionSite).GetWorkerAssignmentStatus(actor?.BuildingVisitor);

    public static bool CanAssignWorker(
        this ConstructionSite constructionSite,
        CharacterActor actor,
        out string failureReason) =>
        Require(constructionSite).CanAssignWorker(
            actor?.BuildingVisitor,
            out failureReason);

    public static IEnumerator AllocateWorker(
        this ConstructionSite constructionSite,
        CharacterActor actor) =>
        Require(constructionSite).AllocateWorker(actor?.BuildingVisitor);

    public static void DeallocateWorker(
        this ConstructionSite constructionSite,
        CharacterActor actor) =>
        Require(constructionSite).DeallocateWorker(actor?.BuildingVisitor);

    private static TFacility Require<TFacility>(TFacility facility)
        where TFacility : class =>
        facility ?? throw new System.ArgumentNullException(nameof(facility));
}

public static class BuildingCharacterFacilityAbilityAdapter
{
    public static void ApplyUseCompleted(
        this BuildingExpeditionRecoveryAbility ability,
        CharacterActor actor,
        BuildableObject building) =>
        Require(ability).ApplyUseCompleted(
            actor?.BuildingVisitor,
            building);

    public static void ApplyUseCompleted(
        this BuildingTrainingAbility ability,
        CharacterActor actor,
        BuildableObject building) =>
        Require(ability).ApplyUseCompleted(
            actor?.BuildingVisitor,
            building);

    public static bool IsExteriorWorkAvailable(
        this BuildingReceptionAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeIsExteriorWorkAvailable(
            ability,
            actor,
            building,
            workTypeId);

    public static float GetExteriorWorkSeconds(
        this BuildingReceptionAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeGetExteriorWorkSeconds(ability, actor, building, workTypeId);

    public static float GetExteriorWorkUrgency(
        this BuildingReceptionAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeGetExteriorWorkUrgency(ability, actor, building, workTypeId);

    public static bool IsExteriorWorkAvailable(
        this BuildingPatrolPostAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeIsExteriorWorkAvailable(
            ability,
            actor,
            building,
            workTypeId);

    public static float GetExteriorWorkSeconds(
        this BuildingPatrolPostAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeGetExteriorWorkSeconds(ability, actor, building, workTypeId);

    public static float GetExteriorWorkUrgency(
        this BuildingPatrolPostAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeGetExteriorWorkUrgency(ability, actor, building, workTypeId);

    public static bool IsExteriorWorkAvailable(
        this BuildingOutdoorRestAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeIsExteriorWorkAvailable(
            ability,
            actor,
            building,
            workTypeId);

    public static float GetExteriorWorkSeconds(
        this BuildingOutdoorRestAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeGetExteriorWorkSeconds(ability, actor, building, workTypeId);

    public static float GetExteriorWorkUrgency(
        this BuildingOutdoorRestAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeGetExteriorWorkUrgency(ability, actor, building, workTypeId);

    public static bool IsExteriorWorkAvailable(
        this BuildingExteriorMaintenanceAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeIsExteriorWorkAvailable(
            ability,
            actor,
            building,
            workTypeId);

    public static float GetExteriorWorkSeconds(
        this BuildingExteriorMaintenanceAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeGetExteriorWorkSeconds(ability, actor, building, workTypeId);

    public static float GetExteriorWorkUrgency(
        this BuildingExteriorMaintenanceAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        InvokeGetExteriorWorkUrgency(ability, actor, building, workTypeId);

    private static bool InvokeIsExteriorWorkAvailable(
        IBuildingExteriorWorkRuntimeAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        Require(ability).IsExteriorWorkAvailable(
            actor?.BuildingVisitor,
            building,
            workTypeId);

    private static float InvokeGetExteriorWorkSeconds(
        IBuildingExteriorWorkRuntimeAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        Require(ability).GetExteriorWorkSeconds(
            actor?.BuildingVisitor,
            building,
            workTypeId);

    private static float InvokeGetExteriorWorkUrgency(
        IBuildingExteriorWorkRuntimeAbility ability,
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId) =>
        Require(ability).GetExteriorWorkUrgency(
            actor?.BuildingVisitor,
            building,
            workTypeId);

    private static TAbility Require<TAbility>(TAbility ability)
        where TAbility : class =>
        ability ?? throw new System.ArgumentNullException(nameof(ability));
}
