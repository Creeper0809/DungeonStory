using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public sealed class AbilityHaul : MonoBehaviour
{
    private const int MaximumPathResolveFrames = 240;
    private const int MaximumMovementAttempts = 5;
    private const double ActivePlanLeaseSeconds = 45d;
    private const double LeaseHeartbeatIntervalSeconds = 10d;
    private static readonly WaitForSeconds MovementRetryDelay =
        new WaitForSeconds(0.15f);

    private CharacterActor actor;
    private AbilityMove move;
    private Coroutine haulingRoutine;
    private bool haulExecutionActive;
    private bool restoredDeliveryPending;
    private WorldItemHaulPlan activePlan;
    private WorldItemHaulPlanUnloadReason unloadReason;
    private bool lastMoveSucceeded;
    private string executionStage = "대기";
    private int routineHeartbeat;
    private string activePathDebug = string.Empty;
    private bool haulingHarnessEquippedForCurrentRun;
    private string lastFailureReason = string.Empty;
    private readonly HashSet<string> pickedLeaseIds =
        new HashSet<string>(System.StringComparer.Ordinal);
    private readonly HashSet<string> releasedLeaseIds =
        new HashSet<string>(System.StringComparer.Ordinal);
    private System.Action haulMovementProgressCallback;
    private double nextLeaseHeartbeatAt;
    private ICharacterProficiencyCommand proficiencyCommands;
    private IGameCalendar calendar;
    private ICharacterSpeciesCommand speciesCommands;
    private IWorkOrderQuery workOrders;
#if UNITY_EDITOR
    public System.Action<WorldItemHaulPlan> DebugBeforeHaulRoutineStart;
#endif
    private IWorldItemStackRuntime ItemRuntime => actor?.WorldItemStackRuntime;

    public bool IsHauling => haulExecutionActive;
    public string CurrentPlanSummary => activePlan != null && activePlan.IsValid
        ? activePlan.Summary
        : "운반 계획 없음";
    public string CurrentUnloadReason => ToDisplayText(unloadReason);
    public string CurrentExecutionStage => executionStage;
    public int RoutineHeartbeat => routineHeartbeat;
    public string ActivePathDebug => activePathDebug;
    public string LastFailureReason => lastFailureReason;
    public bool HasBoundDeliveryIntent => activePlan != null
        && activePlan.IsDeliveryOnlyResume;

    public int GetInTransitQuantity(string destinationId, string itemId)
    {
        if (activePlan == null
            || actor == null
            || string.IsNullOrWhiteSpace(destinationId)
            || string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(
                activePlan.PrimaryDestinationId,
                destinationId,
                System.StringComparison.Ordinal))
        {
            return 0;
        }

        int total = 0;
        foreach (string operationId in GetActivePlanOwnerOperationIds())
        {
            if (!ItemRuntime.TryCaptureHaulDeliveryIntent(
                    operationId,
                    out HaulDeliveryIntentSaveData intent)
                || intent == null
                || !string.Equals(
                    intent.destinationId,
                    destinationId,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            foreach (HaulDeliveryItemCommitmentSaveData commitment in
                     intent.commitments
                     ?? new System.Collections.Generic.List<HaulDeliveryItemCommitmentSaveData>())
            {
                if (commitment != null
                    && string.Equals(
                        commitment.itemId,
                        itemId,
                        System.StringComparison.Ordinal))
                {
                    total = checked(total + Mathf.Max(0, commitment.quantity));
                }
            }
        }
        return total;
    }

    private void Awake()
    {
        CacheReferences();
    }

    [Inject]
    public void ConstructProficiencyProgression(
        ICharacterProficiencyCommand commands,
        IGameCalendar gameCalendar,
        ICharacterSpeciesCommand speciesCommands)
    {
        proficiencyCommands = commands;
        calendar = gameCalendar;
        this.speciesCommands = speciesCommands
            ?? throw new System.ArgumentNullException(nameof(speciesCommands));
    }

    [Inject]
    public void ConstructHaulDestinationAuthority(IWorkOrderQuery workOrderQuery)
    {
        workOrders = workOrderQuery
            ?? throw new System.ArgumentNullException(nameof(workOrderQuery));
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            StopHauling("disabled");
        }
    }

    public static AbilityHaul Ensure(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        AbilityHaul ability = actor.GetComponent<AbilityHaul>();
        if (ability == null && Application.isPlaying)
        {
            ability = actor.gameObject.AddComponent<AbilityHaul>();
        }

        ability?.CacheReferences();
        return ability;
    }

    public bool CanStartHauling(out string failureReason)
    {
        failureReason = string.Empty;
        CacheReferences();
        if (actor == null || move == null || ItemRuntime == null)
        {
            failureReason = "hauling-dependencies-unavailable";
            return false;
        }
        if (restoredDeliveryPending && activePlan?.IsDeliveryOnlyResume == true)
        {
            string operationId = GetActivePlanOwnerOperationIds().SingleOrDefault();
            if (!string.IsNullOrWhiteSpace(operationId)
                && ItemRuntime.TryCaptureHaulDeliveryIntent(operationId, out _))
            {
                return true;
            }
            failureReason = "restored-haul-delivery-intent-missing";
            return false;
        }
        return ItemRuntime.HasAvailableHaulJob(actor);
    }

    public void StartHauling()
    {
        CacheReferences();
        IWorldItemStackRuntime itemRuntime = ItemRuntime;
        if (actor == null || move == null || itemRuntime == null)
        {
            EndAiAction(
                CharacterAiActionTerminalKind.Failed,
                AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "Hauling dependencies are unavailable."));
            return;
        }

        if (IsHauling)
        {
            return;
        }

        WorldItemHaulPlan reservedPlan;
        string reason;
        bool resumingRestoredDelivery = restoredDeliveryPending
            && activePlan?.IsDeliveryOnlyResume == true;
        if (resumingRestoredDelivery)
        {
            reservedPlan = activePlan;
        }
        else if (!itemRuntime.TryReserveBestHaulPlan(
                     actor,
                     out reservedPlan,
                     out reason))
        {
            actor.Brain?.SetActionPhase("운반 대기", null, reason);
            EndAiAction(
                CharacterAiActionTerminalKind.Failed,
                AIActionFailure.Create(
                    AIActionFailureKind.NoWork,
                    reason));
            return;
        }

        activePlan = reservedPlan;
        if (!TryRenewActivePlanLeases(out reason))
        {
            string[] operationIds = GetActivePlanOwnerOperationIds();
            actor.Brain?.SetActionPhase("운반 대기", null, reason);
            if (resumingRestoredDelivery)
                ReturnCarriedItemsAfterInterruptedHaul(reason);
            ReleaseActivePlanReservations(ItemReservationReleaseReason.Cancelled);
            restoredDeliveryPending = false;
            activePlan = null;
            foreach (string operationId in operationIds)
                itemRuntime.ReleaseHaulDeliveryIntent(operationId);
            EndAiAction(
                CharacterAiActionTerminalKind.Failed,
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    reason));
            return;
        }
        actor.CarryInventory?.TryPrepareHaulingHarness(
            out haulingHarnessEquippedForCurrentRun);
        unloadReason = WorldItemHaulPlanUnloadReason.None;
        lastFailureReason = string.Empty;
        pickedLeaseIds.Clear();
        if (resumingRestoredDelivery)
        {
            foreach (WorldItemReservedStackQuantity reservation in
                     activePlan.ReservedStackQuantities)
            {
                if (!string.IsNullOrWhiteSpace(reservation.LeaseId))
                    pickedLeaseIds.Add(reservation.LeaseId);
            }
        }
        releasedLeaseIds.Clear();
        executionStage = "운반 시작";
        routineHeartbeat = 0;
        activePathDebug = string.Empty;
        restoredDeliveryPending = false;
        haulExecutionActive = true;
#if UNITY_EDITOR
        DebugBeforeHaulRoutineStart?.Invoke(activePlan);
#endif
        Coroutine started = StartCoroutine(HaulRoutine(activePlan));
        // StartCoroutine may complete before returning. Preserve the terminal
        // state written by FinishHauling instead of storing a stale handle.
        haulingRoutine = haulExecutionActive ? started : null;
    }

    private bool TryRenewActivePlanLeases(out string failureReason)
    {
        failureReason = string.Empty;
        if (activePlan == null
            || ItemRuntime is not IWorldItemQuantityLeaseRuntime leaseRuntime)
        {
            failureReason = "quantity lease runtime missing";
            return false;
        }

        if (actor?.GameClock == null)
        {
            failureReason = "game clock missing";
            return false;
        }

        double requestedUntil = actor.GameClock.Time + ActivePlanLeaseSeconds;
        HashSet<string> renewed = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (WorldItemReservedStackQuantity reservation in activePlan.ReservedStackQuantities)
        {
            if (string.IsNullOrWhiteSpace(reservation.LeaseId)
                || releasedLeaseIds.Contains(reservation.LeaseId)
                || !renewed.Add(reservation.LeaseId))
            {
                continue;
            }

            if (!leaseRuntime.TryRevalidateQuantityLease(
                    reservation.LeaseId,
                    out failureReason)
                || !leaseRuntime.TryRenewQuantityLease(
                    reservation.LeaseId,
                    requestedUntil,
                    out failureReason))
            {
                return false;
            }
        }

        return renewed.Count > 0;
    }

    public void StopHauling(string reason)
    {
        string[] operationIds = GetActivePlanOwnerOperationIds();
        haulExecutionActive = false;
        restoredDeliveryPending = false;
        if (haulingRoutine != null)
        {
            StopCoroutine(haulingRoutine);
            haulingRoutine = null;
        }

        ReturnCarriedItemsAfterInterruptedHaul(reason);
        ReleaseActivePlanReservations(ItemReservationReleaseReason.Cancelled);
        actor?.CarryInventory?.CompleteHaulingHarness(
            haulingHarnessEquippedForCurrentRun,
            applyWear: false);
        haulingHarnessEquippedForCurrentRun = false;
        activePlan = null;
        foreach (string operationId in operationIds)
            ItemRuntime?.ReleaseHaulDeliveryIntent(operationId);
        unloadReason = WorldItemHaulPlanUnloadReason.Interrupted;
        executionStage = "중단";
        activePathDebug = string.Empty;
    }

    public HaulDeliveryIntentSaveData CaptureDeliveryIntentForSave()
    {
        string operationId = GetActivePlanOwnerOperationIds().SingleOrDefault();
        if (string.IsNullOrWhiteSpace(operationId))
            return null;
        if (ItemRuntime == null
            || !ItemRuntime.TryCaptureHaulDeliveryIntent(
                operationId,
                out HaulDeliveryIntentSaveData intent))
        {
            throw new System.InvalidOperationException(
                $"Active haul plan '{operationId}' has no delivery intent authority.");
        }
        return intent.HasCommittedPickup ? intent : null;
    }

    public bool TryRebindRestoredDeliveryIntent(
        HaulDeliveryIntentSaveData intent,
        IReadOnlyList<ItemQuantityLease> restoredLeases,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        out string failureReason)
    {
        return TryRebindRestoredDeliveryIntent(
            intent,
            restoredLeases,
            workOrders,
            destinationClaims,
            out failureReason);
    }

    internal bool TryRebindRestoredDeliveryIntent(
        HaulDeliveryIntentSaveData intent,
        IReadOnlyList<ItemQuantityLease> restoredLeases,
        IWorkOrderQuery destinationWorkOrders,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        out string failureReason)
    {
        failureReason = string.Empty;
        CacheReferences();
        string actorId = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (actor == null
            || move == null
            || ItemRuntime == null
            || intent == null
            || !intent.HasCommittedPickup
            || !System.Enum.IsDefined(
                typeof(WorldItemHaulDestinationKind),
                intent.destinationKind)
            || !string.Equals(
                intent.ownerCharacterId?.Trim(),
                actorId,
                System.StringComparison.Ordinal))
        {
            failureReason = "haul-restore-actor-or-intent-mismatch";
            return false;
        }
        if (activePlan != null || haulExecutionActive || restoredDeliveryPending)
        {
            failureReason = "haul-restore-already-bound";
            return false;
        }

        CharacterCarryInventory carry = actor.CarryInventory;
        CharacterCarriedItemSaveData[] carried = carry?.Items
            .Where(item => item != null
                && string.Equals(
                    item.ownerOperationId?.Trim(),
                    intent.operationId?.Trim(),
                    System.StringComparison.Ordinal))
            .OrderBy(item => item.carriedStackId, System.StringComparer.Ordinal)
            .ToArray() ?? System.Array.Empty<CharacterCarriedItemSaveData>();
        HaulDeliveryItemCommitmentSaveData[] commitments = intent.commitments
            .Where(value => value != null)
            .OrderBy(value => value.carriedStackId, System.StringComparer.Ordinal)
            .ToArray();
        if (carried.Length != commitments.Length)
        {
            failureReason = "haul-restore-carry-count-mismatch";
            return false;
        }

        List<WorldItemReservedStackQuantity> reservations = new();
        HashSet<string> matchedLeaseIds = new(System.StringComparer.Ordinal);
        string expectedCohort =
            $"haul:{intent.destinationKind}:{intent.destinationId?.Trim()}";
        foreach (HaulDeliveryItemCommitmentSaveData commitment in commitments)
        {
            CharacterCarriedItemSaveData physical = carried.FirstOrDefault(item =>
                string.Equals(
                    item.carriedStackId,
                    commitment.carriedStackId,
                    System.StringComparison.Ordinal));
            ItemQuantityLease lease = (restoredLeases
                    ?? System.Array.Empty<ItemQuantityLease>())
                .SingleOrDefault(candidate => candidate != null
                    && candidate.purpose == ItemReservationPurpose.Hauling
                    && candidate.remainingQuantity == commitment.quantity
                    && candidate.slices != null
                    && candidate.slices.Count == 1
                    && string.Equals(
                        candidate.ownerOperationId,
                        intent.operationId,
                        System.StringComparison.Ordinal)
                    && string.Equals(
                        candidate.ownerCharacterId,
                        intent.ownerCharacterId,
                        System.StringComparison.Ordinal)
                    && string.Equals(
                        candidate.aggregationCohortId,
                        expectedCohort,
                        System.StringComparison.Ordinal)
                    && candidate.slices.Any(slice => slice != null
                        && slice.quantity == commitment.quantity
                        && string.Equals(
                            slice.stackId,
                            commitment.carriedStackId,
                            System.StringComparison.Ordinal)
                        && string.Equals(
                            slice.expectedStackSignature,
                            commitment.expectedStackSignature,
                            System.StringComparison.Ordinal)));
            if (physical == null
                || physical.quantity != commitment.quantity
                || !string.Equals(physical.itemId, commitment.itemId,
                    System.StringComparison.Ordinal)
                || !string.Equals(
                    ItemReservationSignature.Create(
                        physical.itemId,
                        physical.components),
                    commitment.expectedStackSignature,
                    System.StringComparison.Ordinal)
                || !string.Equals(
                    physical.sourceStackId?.Trim(),
                    commitment.sourceStackId?.Trim(),
                    System.StringComparison.Ordinal)
                || lease == null
                || !matchedLeaseIds.Add(lease.leaseId))
            {
                failureReason =
                    $"haul-restore-commitment-mismatch:{commitment.carriedStackId}";
                return false;
            }
            reservations.Add(new WorldItemReservedStackQuantity(
                commitment.carriedStackId,
                commitment.itemId,
                commitment.quantity,
                actor.GetNowXY(),
                intent.destinationKind,
                intent.destinationId,
                lease.leaseId,
                intent.operationId));
        }
        if (matchedLeaseIds.Count != (restoredLeases?.Count ?? 0))
        {
            failureReason = "haul-restore-unrelated-lease";
            return false;
        }

        if (!HaulDeliveryOperationIdentity.TryParse(
                intent.operationId,
                intent.ownerCharacterId,
                out _))
        {
            failureReason = "haul-restore-operation-id-invalid:" + intent.operationId;
            return false;
        }

        if (!TryGetGrid(out Grid grid)
            || !WorldItemHaulDestinationAuthority.TryResolve(
                grid,
                actor.WorldRegistry,
                destinationWorkOrders,
                destinationClaims,
                intent.destinationKind,
                intent.destinationId,
                new Vector2Int(intent.dropGridX, intent.dropGridY),
                out WorldItemHaulDestinationAuthority.Resolution destination,
                out failureReason))
        {
            return false;
        }
        Vector2Int savedDeliveryPosition = new(
            intent.deliveryGridX,
            intent.deliveryGridY);
        Vector2Int savedDropPosition = new(intent.dropGridX, intent.dropGridY);
        if (destination.DeliveryPosition != savedDeliveryPosition
            || destination.DropPosition != savedDropPosition
            || !string.Equals(
                destination.DestinationId,
                intent.destinationId?.Trim(),
                System.StringComparison.Ordinal))
        {
            failureReason = "haul-restore-destination-authority-mismatch:"
                + intent.destinationId;
            return false;
        }
        WorldItemHaulPlanLeg delivery = new(
            reservations[0],
            actor.GetNowXY(),
            destination.Warehouse,
            destination.DeliveryPosition,
            destination.DropPosition);
        activePlan = new WorldItemHaulPlan(
            System.Array.Empty<WorldItemHaulPlanLeg>(),
            new[] { delivery },
            reservations,
            totalWeight: 0f,
            expectedDetourCost: 0,
            primaryDestination: intent.destinationKind,
            primaryDestinationId: intent.destinationId,
            deliveryOnlyResume: true);
        pickedLeaseIds.Clear();
        foreach (WorldItemReservedStackQuantity reservation in reservations)
            pickedLeaseIds.Add(reservation.LeaseId);
        releasedLeaseIds.Clear();
        unloadReason = WorldItemHaulPlanUnloadReason.None;
        lastFailureReason = string.Empty;
        restoredDeliveryPending = true;
        haulExecutionActive = false;
        executionStage = "restore-delivery-bound";
        return true;
    }

    public void ClearRestoredDeliveryIntentBinding()
    {
        if (!restoredDeliveryPending && activePlan?.IsDeliveryOnlyResume != true)
            return;
        restoredDeliveryPending = false;
        haulExecutionActive = false;
        if (haulingRoutine != null)
        {
            StopCoroutine(haulingRoutine);
            haulingRoutine = null;
        }
        activePlan = null;
        pickedLeaseIds.Clear();
        releasedLeaseIds.Clear();
        executionStage = "restore-delivery-rollback";
    }

    public bool TryPrepareForRestoreRetirement(
        CharacterCarryInventory restoredReplacement,
        out string failureReason)
    {
        failureReason = "retiring carry inventory unavailable";
        CacheReferences();
        CharacterCarryInventory retiring = actor?.CarryInventory;
        if (retiring == null
            || !retiring.TryRelinquishToRestoredAuthority(
                restoredReplacement,
                out failureReason))
        {
            return false;
        }

        CompleteRestoreRetirementState();
        failureReason = string.Empty;
        return true;
    }

    public void PrepareForRestoreRetirementWithoutReplacement()
    {
        CacheReferences();
        // This actor does not exist in the restored snapshot. Its current-world
        // carry is intentionally discarded with that world, never materialized
        // into the restored physical item authority.
        actor?.CarryInventory?.RemoveAllItems();
        CompleteRestoreRetirementState();
    }

    private void CompleteRestoreRetirementState()
    {
        haulExecutionActive = false;
        if (haulingRoutine != null)
        {
            StopCoroutine(haulingRoutine);
            haulingRoutine = null;
        }
        move?.CancelActiveMovement();
        actor?.CarryInventory?.CompleteHaulingHarness(
            haulingHarnessEquippedForCurrentRun,
            applyWear: false);
        haulingHarnessEquippedForCurrentRun = false;

        // The item restore participant has already rebuilt reservation authority.
        // Old runtime lease IDs must be forgotten, not released into the restored
        // ledger.  The replacement actor owns the persisted carry snapshot.
        activePlan = null;
        pickedLeaseIds.Clear();
        releasedLeaseIds.Clear();
        unloadReason = WorldItemHaulPlanUnloadReason.Interrupted;
        executionStage = "restore-retirement";
        activePathDebug = string.Empty;
    }

    private IEnumerator HaulRoutine(WorldItemHaulPlan plan)
    {
        routineHeartbeat++;
        executionStage = "계획 확인";
        IWorldItemStackRuntime itemRuntime = ItemRuntime;
        CharacterCarryInventory carry = actor != null
            ? actor.CarryInventory
            : null;
        if (carry == null || itemRuntime == null || plan == null || !plan.IsValid)
        {
            lastFailureReason = "haul-plan-or-carry-unavailable";
            StopHauling(lastFailureReason);
            EndAiAction(
                CharacterAiActionTerminalKind.Failed,
                AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "Haul plan or carry inventory is unavailable."));
            yield break;
        }

        // A restored delivery begins after the grandfather ledger has been
        // rebuilt. Validate and heartbeat that exact lease before movement;
        // a mismatched or expired slice must never be replaced by a new plan.
        if (plan.IsDeliveryOnlyResume
            && !TryRenewActivePlanLeases(out string restoredLeaseFailure))
        {
            lastFailureReason = "restore-delivery-lease-invalid:"
                + restoredLeaseFailure;
            unloadReason = WorldItemHaulPlanUnloadReason.PickupReservationLost;
            FinishHauling();
            yield break;
        }

        move.CancelActiveMovement();
        if (!TryGetGrid(out Grid grid))
        {
            actor.Brain?.SetActionPhase("운반 실패", null, "그리드 없음");
            lastFailureReason = "hauling-grid-unavailable";
            StopHauling(lastFailureReason);
            EndAiAction(
                CharacterAiActionTerminalKind.Failed,
                AIActionFailure.Create(
                    AIActionFailureKind.NoGrid,
                    "Hauling grid is unavailable."));
            yield break;
        }

        AIAction expectedAction = GetExpectedHaulAction();
        int pickedStackCount = plan.IsDeliveryOnlyResume
            ? plan.ReservedStackQuantities.Count
            : 0;
        IReadOnlyList<WorldItemHaulPlanLeg> pickupLegs = plan.PickupLegs;
        for (int pickupIndex = 0; pickupIndex < pickupLegs.Count; pickupIndex++)
        {
            WorldItemHaulPlanLeg pickup = pickupLegs[pickupIndex];
            if (!pickup.IsValid)
            {
                continue;
            }

            actor.Brain?.SetActionPhase(
                "물건 가지러 이동",
                null,
                $"{pickup.ItemPosition} · {pickedStackCount + 1}/{plan.PickupLegs.Count}");
            bool pickupReached = IsActorAt(pickup.PickupStandPosition);
            if (!pickupReached)
            {
                executionStage = "픽업 이동 요청";
                routineHeartbeat++;
                yield return MoveTo(
                    grid,
                    pickup.PickupStandPosition,
                    expectedAction);
                executionStage = "픽업 이동 반환";
                routineHeartbeat++;
                pickupReached = lastMoveSucceeded;
            }

            if (!pickupReached)
            {
                lastFailureReason = $"pickup-unreachable:{pickup.PickupStandPosition}";
                unloadReason = WorldItemHaulPlanUnloadReason.Interrupted;
                break;
            }

            if (!TryRenewActivePlanLeases(out string renewalReason))
            {
                lastFailureReason = "pickup-lease-expired:" + renewalReason;
                unloadReason = WorldItemHaulPlanUnloadReason.PickupReservationLost;
                break;
            }

            actor.Brain?.SetActionPhase(
                "물건 줍는 중",
                null,
                $"{pickup.Reservation.Quantity}개");
            if (!itemRuntime.TryPickupReservedStackQuantity(
                    actor,
                    carry,
                    pickup.Reservation,
                    out int pickedUp,
                    out string pickupReason))
            {
                lastFailureReason = string.IsNullOrWhiteSpace(pickupReason)
                    ? $"pickup-failed:{pickup.Reservation.StackId}"
                    : pickupReason;
                actor.Brain?.SetActionPhase("운반 건너뜀", null, pickupReason);
                ReleasePlanLease(
                    pickup.Reservation.LeaseId,
                    ItemReservationReleaseReason.Replanned);
                unloadReason = WorldItemHaulPlanUnloadReason.PickupReservationLost;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(pickup.Reservation.LeaseId))
                pickedLeaseIds.Add(pickup.Reservation.LeaseId);
            if (!itemRuntime.TryCommitHaulPickup(
                    pickup.Reservation.OwnerOperationId,
                    carry,
                    out string commitmentFailure))
            {
                lastFailureReason = commitmentFailure;
                unloadReason = WorldItemHaulPlanUnloadReason.PickupReservationLost;
                break;
            }
            pickedStackCount++;
            executionStage = "픽업 완료";
            routineHeartbeat++;
            yield return null;
            if (pickedUp < pickup.Reservation.Quantity
                || carry.GetLoadRatio(
                    itemRuntime.CatalogProvider,
                    itemRuntime.HaulingSettingsProvider) >= 0.98f)
            {
                unloadReason = WorldItemHaulPlanUnloadReason.LoadLimitReached;
                break;
            }
        }

        ReleaseUnpickedPlanReservations();
        if (!carry.HasItems || pickedStackCount == 0)
        {
            if (unloadReason == WorldItemHaulPlanUnloadReason.None)
                unloadReason = WorldItemHaulPlanUnloadReason.NoPickupCandidate;
            actor.Brain?.SetActionPhase("운반 실패", null, "집을 물건 없음");
            FinishHauling();
            yield break;
        }

        IReadOnlyList<WorldItemHaulPlanLeg> deliveryLegs = plan.DeliveryLegs;
        bool foundValidDelivery = false;
        for (int deliveryIndex = 0; deliveryIndex < deliveryLegs.Count; deliveryIndex++)
        {
            WorldItemHaulPlanLeg delivery = deliveryLegs[deliveryIndex];
            if (!delivery.IsValid)
            {
                continue;
            }
            foundValidDelivery = true;

            actor.Brain?.SetActionPhase(
                delivery.DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer
                    ? "목적지로 이동"
                    : "창고로 이동",
                null,
                delivery.DeliveryPosition.ToString());
            bool deliveryReached = IsActorAt(delivery.DeliveryPosition);
            if (!deliveryReached)
            {
                executionStage = "배송 이동 요청";
                routineHeartbeat++;
                yield return MoveTo(
                    grid,
                    delivery.DeliveryPosition,
                    expectedAction);
                executionStage = "배송 이동 반환";
                routineHeartbeat++;
                deliveryReached = lastMoveSucceeded;
            }

            if (!deliveryReached)
            {
                lastFailureReason = $"delivery-unreachable:{delivery.DeliveryPosition}";
                unloadReason = WorldItemHaulPlanUnloadReason.JobChanged;
                actor.Brain?.SetActionPhase(
                    "운반 중단",
                    null,
                    "목적지까지 이동할 수 없음");
                break;
            }

            actor.Brain?.SetActionPhase("물품 내려놓는 중", null, delivery.DeliveryPosition.ToString());
            executionStage = "배송 입고";
            routineHeartbeat++;
            string[] ownerOperationIds = GetActivePlanOwnerOperationIds();
            int deliveredQuantity = carry.Items
                .Where(item => item != null
                    && ownerOperationIds.Contains(
                        item.ownerOperationId?.Trim() ?? string.Empty,
                        System.StringComparer.Ordinal))
                .Sum(item => Mathf.Max(0, item.quantity));
            string depositReason;
            bool deposited;
            if (delivery.DestinationKind == WorldItemHaulDestinationKind.FacilityBuffer)
            {
                deposited = itemRuntime.TryDepositCarriedItemsToFacility(
                    actor,
                    carry,
                    delivery.DropPosition,
                    delivery.DestinationId,
                    ownerOperationIds,
                    out depositReason);
            }
            else
            {
                deposited = itemRuntime.TryDepositCarriedItems(
                    actor,
                    carry,
                    delivery.Warehouse,
                    ownerOperationIds,
                    out depositReason);
            }

            if (!deposited)
            {
                lastFailureReason = string.IsNullOrWhiteSpace(depositReason)
                    ? "deposit-failed"
                    : depositReason;
                unloadReason = WorldItemHaulPlanUnloadReason.DepositRejected;
                actor.AddLog("운반 정리 실패: " + (string.IsNullOrWhiteSpace(depositReason) ? "입고 실패" : depositReason));
            }
            else
            {
                actor.AddLog($"바닥 물건 {pickedStackCount}묶음을 정리했다.");
                unloadReason = WorldItemHaulPlanUnloadReason.Completed;
                AwardHaulingProficiency(deliveredQuantity);
            }

            break;
        }

        if (!foundValidDelivery
            && unloadReason == WorldItemHaulPlanUnloadReason.None)
        {
            lastFailureReason = "delivery-leg-missing";
            unloadReason = WorldItemHaulPlanUnloadReason.DeliveryUnavailable;
        }

        FinishHauling();
    }

    private IEnumerator MoveTo(
        Grid grid,
        Vector2Int target,
        AIAction expectedAction)
    {
        lastMoveSucceeded = false;
        executionStage = "경로 준비";
        routineHeartbeat++;
        if (grid == null || actor == null)
        {
            yield break;
        }

        if (IsActorAt(target))
        {
            lastMoveSucceeded = true;
            yield break;
        }

        IGridPathSearchBroker broker = actor.PathSearchBroker;
        if (broker == null)
        {
            yield break;
        }

        GridTraversalContext traversalContext =
            GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(actor),
                DoorAccessOverrideKind.None,
                GridMovementIntent.General);
        for (int movementAttempt = 0;
             movementAttempt < MaximumMovementAttempts;
             movementAttempt++)
        {
            if (IsActorAt(target))
            {
                lastMoveSucceeded = true;
                yield break;
            }

            Queue<GridMoveStep> path = null;
            executionStage = $"경로 요청 {movementAttempt + 1}";
            routineHeartbeat++;
            for (int frame = 0; frame < MaximumPathResolveFrames; frame++)
            {
                if (IsActionCancelled(expectedAction))
                {
                    yield break;
                }

                GridPathRequestStatus status = broker.RequestMovePathTo(
                    grid,
                    actor.GetNowXY(),
                    target,
                    out path,
                    GridPathSearchPriority.Urgent,
                    traversalContext);
                if (status == GridPathRequestStatus.Reachable)
                {
                    activePathDebug = DescribePath(path);
                    executionStage = $"경로 준비 완료 {path?.Count ?? 0}단계";
                    routineHeartbeat++;
                    break;
                }

                if (status == GridPathRequestStatus.Unreachable)
                {
                    actor.Brain?.SetActionPhase(
                        "운반 경로 없음",
                        null,
                        target.ToString());
                    yield break;
                }

                if (frame == 0)
                {
                    actor.Brain?.SetActionPhase(
                        "운반 경로 계산 중",
                        null,
                        target.ToString());
                }

                yield return null;
                routineHeartbeat++;
            }

            if (path == null)
            {
                actor.Brain?.SetActionPhase(
                    "운반 경로 지연",
                    null,
                    target.ToString());
                yield break;
            }

            executionStage = $"경로 이동 중 {path.Count}단계";
            routineHeartbeat++;
            nextLeaseHeartbeatAt = actor.GameClock != null
                ? actor.GameClock.Time + LeaseHeartbeatIntervalSeconds
                : double.PositiveInfinity;
            haulMovementProgressCallback ??= OnHaulMovementProgress;
            yield return move.MoveByPath(
                path,
                expectedAction,
                haulMovementProgressCallback);
            executionStage = "경로 이동 반환";
            routineHeartbeat++;
            if (IsActorAt(target)
                && !IsActionCancelled(expectedAction))
            {
                lastMoveSucceeded = true;
                yield break;
            }

            if (IsActionCancelled(expectedAction))
            {
                yield break;
            }

            actor.Brain?.SetActionPhase(
                "운반 경로 다시 계산",
                null,
                $"{movementAttempt + 1}/{MaximumMovementAttempts}");
            yield return MovementRetryDelay;
        }
    }

    private void OnHaulMovementProgress()
    {
        if (actor?.GameClock == null
            || actor.GameClock.Time < nextLeaseHeartbeatAt)
        {
            return;
        }

        if (!TryRenewActivePlanLeases(out string renewalReason))
        {
            lastFailureReason = "movement-lease-expired:" + renewalReason;
            unloadReason = WorldItemHaulPlanUnloadReason.PickupReservationLost;
            move?.CancelActiveMovement();
            return;
        }

        nextLeaseHeartbeatAt = actor.GameClock.Time
            + LeaseHeartbeatIntervalSeconds;
    }

    private AIAction GetExpectedHaulAction()
    {
        AIAction current = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        return current?.actionset is AIHaul ? current : null;
    }

    private static string DescribePath(IEnumerable<GridMoveStep> path)
    {
        return path == null
            ? "none"
            : string.Join(
                ">",
                path.Where(step => step.IsValid)
                    .Select(step =>
                        $"{step.From}->{step.To}:{step.MoveType}:"
                        + $"{step.MovementOccupant?.GetType().Name ?? "none"}"));
    }

    private bool TryGetGrid(out Grid grid)
    {
        grid = null;
        return actor?.WorldRegistry != null
            && actor.WorldRegistry.TryGetGrid(out grid);
    }

    private bool IsActionCancelled(AIAction expectedAction)
    {
        return expectedAction != null
            && (actor == null || actor.Brain == null || actor.Brain.bestAction != expectedAction);
    }

    private bool IsActorAt(Vector2Int gridPosition)
    {
        return actor != null && actor.GetNowXY() == gridPosition;
    }

    private void FinishHauling()
    {
        string[] operationIds = GetActivePlanOwnerOperationIds();
        CharacterCarryInventory carry = actor?.CarryInventory;
        if (unloadReason == WorldItemHaulPlanUnloadReason.Completed
            && carry?.HasItems == true)
        {
            lastFailureReason = "delivery-completed-with-carried-items";
            unloadReason = WorldItemHaulPlanUnloadReason.DepositRejected;
        }

        if (unloadReason != WorldItemHaulPlanUnloadReason.Completed)
        {
            ReturnCarriedItemsAfterInterruptedHaul(unloadReason.ToString());
        }

        ReleaseActivePlanReservations(
            unloadReason == WorldItemHaulPlanUnloadReason.Completed
                ? ItemReservationReleaseReason.Completed
                : ItemReservationReleaseReason.Cancelled);
        actor?.CarryInventory?.CompleteHaulingHarness(
            haulingHarnessEquippedForCurrentRun,
            applyWear: unloadReason == WorldItemHaulPlanUnloadReason.Completed);
        haulingHarnessEquippedForCurrentRun = false;
        activePlan = null;
        restoredDeliveryPending = false;
        haulExecutionActive = false;
        haulingRoutine = null;
        foreach (string operationId in operationIds)
            ItemRuntime?.ReleaseHaulDeliveryIntent(operationId);
        CharacterAiActionTerminalKind terminalKind = unloadReason ==
                WorldItemHaulPlanUnloadReason.Completed
            ? CharacterAiActionTerminalKind.Completed
            : unloadReason == WorldItemHaulPlanUnloadReason.Interrupted
                ? CharacterAiActionTerminalKind.Cancelled
                : CharacterAiActionTerminalKind.Failed;
        EndAiAction(
            terminalKind,
            terminalKind == CharacterAiActionTerminalKind.Failed
                ? AIActionFailure.Create(
                    ResolveFailureKind(unloadReason),
                    $"Hauling ended before delivery: {unloadReason}; "
                    + $"detail={lastFailureReason}.")
                : AIActionFailure.None);
    }

    private void ReturnCarriedItemsAfterInterruptedHaul(string reason)
    {
        CharacterCarryInventory carry = actor?.CarryInventory;
        if (carry?.HasItems != true || activePlan == null)
        {
            return;
        }

        string[] ownerOperationIds = GetActivePlanOwnerOperationIds();
        if (ownerOperationIds.Length == 0)
        {
            return;
        }

        if (ItemRuntime is not IWorldItemCarryRecoveryRuntime recovery)
        {
            lastFailureReason = $"carried-item-recovery-unavailable:{reason}";
            Debug.LogError(
                $"[AbilityHaul] {lastFailureReason}; actor={actor?.BuildingCharacterId}");
            return;
        }

        if (!recovery.TryDropCarriedItems(
                actor,
                carry,
                ownerOperationIds,
                out string failureReason))
        {
            lastFailureReason =
                $"carried-item-recovery-failed:{reason}:{failureReason}";
            Debug.LogError(
                $"[AbilityHaul] {lastFailureReason}; actor={actor?.BuildingCharacterId}");
        }
    }

    private string[] GetActivePlanOwnerOperationIds()
    {
        return activePlan?.ReservedStackQuantities
            .Select(reservation =>
                reservation.OwnerOperationId?.Trim() ?? string.Empty)
            .Where(ownerId => ownerId.Length > 0)
            .Distinct(System.StringComparer.Ordinal)
            .ToArray()
            ?? System.Array.Empty<string>();
    }

    private void ReleaseUnpickedPlanReservations()
    {
        if (activePlan == null)
            return;

        foreach (WorldItemReservedStackQuantity reservation in
                 activePlan.ReservedStackQuantities)
        {
            if (!pickedLeaseIds.Contains(reservation.LeaseId))
            {
                ReleasePlanLease(
                    reservation.LeaseId,
                    ItemReservationReleaseReason.Replanned);
            }
        }
    }

    private void ReleaseActivePlanReservations(ItemReservationReleaseReason reason)
    {
        if (activePlan == null)
        {
            return;
        }

        IReadOnlyList<WorldItemReservedStackQuantity> reservations =
            activePlan.ReservedStackQuantities;
        for (int index = 0; index < reservations.Count; index++)
        {
            WorldItemReservedStackQuantity reservation = reservations[index];
            ReleasePlanLease(reservation.LeaseId, reason);
        }
        pickedLeaseIds.Clear();
        releasedLeaseIds.Clear();
    }

    private void ReleasePlanLease(
        string leaseId,
        ItemReservationReleaseReason reason)
    {
        string normalizedLeaseId = leaseId?.Trim() ?? string.Empty;
        if (normalizedLeaseId.Length == 0
            || releasedLeaseIds.Contains(normalizedLeaseId))
        {
            return;
        }

        if (ItemRuntime is IWorldItemQuantityLeaseRuntime leaseRuntime)
        {
            leaseRuntime.ReleaseQuantityLease(normalizedLeaseId, reason);
        }
        releasedLeaseIds.Add(normalizedLeaseId);
    }

    private static AIActionFailureKind ResolveFailureKind(
        WorldItemHaulPlanUnloadReason reason)
    {
        return reason switch
        {
            WorldItemHaulPlanUnloadReason.NoPickupCandidate =>
                AIActionFailureKind.NoWork,
            WorldItemHaulPlanUnloadReason.PickupReservationLost =>
                AIActionFailureKind.ResourceUnavailable,
            WorldItemHaulPlanUnloadReason.DepositRejected =>
                AIActionFailureKind.ResourceUnavailable,
            WorldItemHaulPlanUnloadReason.DeliveryUnavailable =>
                AIActionFailureKind.NoDestination,
            WorldItemHaulPlanUnloadReason.JobChanged =>
                AIActionFailureKind.NoPath,
            _ => AIActionFailureKind.CannotStart
        };
    }

    private void EndAiAction(
        CharacterAiActionTerminalKind terminalKind,
        AIActionFailure failure)
    {
        if (actor != null && actor.Brain != null)
        {
            AIAction expectedAction = actor.Brain.bestAction;
            if (terminalKind == CharacterAiActionTerminalKind.Failed)
            {
                actor.Brain.ReportRuntimeActionFailure(
                    failure.HasFailure
                        ? failure
                        : AIActionFailure.Create(AIActionFailureKind.Unknown),
                    requestImmediateReplan: false);
            }
            actor.Brain.EndExpectedAction(
                expectedAction,
                terminalKind,
                clearFailures: terminalKind == CharacterAiActionTerminalKind.Completed);
        }
    }

    private void AwardHaulingProficiency(int deliveredQuantity)
    {
        if (deliveredQuantity <= 0
            || proficiencyCommands == null
            || calendar == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
        {
            return;
        }
        ProficiencyWorkProfile profile = new(
            BuiltInCharacterProficiencyIds.Fieldwork);
        proficiencyCommands.AddApprovedWork(
            id,
            profile,
            approvedWork: deliveredQuantity,
            difficultyMultiplier: 1f,
            outcome: ProficiencyWorkOutcome.Success,
            learningMultiplier: CharacterProficiencyLearningRules.Resolve(
                actor,
                profile,
                BuiltInWorkTypeIds.Haul),
            repetitionMultiplier: 1f,
            absoluteHour: calendar.AbsoluteHour);
        if (!speciesCommands.RecordCompletedWork(
                id,
                BuiltInWorkTypeIds.Haul.Value,
                deliveredQuantity,
                out DomainFailure failure))
            throw new System.InvalidOperationException(
                $"Species hauling wear projection failed: {failure.Code}");
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }

    private static string ToDisplayText(WorldItemHaulPlanUnloadReason reason)
    {
        if (reason == WorldItemHaulPlanUnloadReason.PickupReservationLost)
            return "집기 예약 상실";
        if (reason == WorldItemHaulPlanUnloadReason.DeliveryUnavailable)
            return "배송 목적지 없음";
        if (reason == WorldItemHaulPlanUnloadReason.DepositRejected)
            return "목적지 입고 거부";

        return reason switch
        {
            WorldItemHaulPlanUnloadReason.LoadLimitReached => "적재 한도 도달",
            WorldItemHaulPlanUnloadReason.NoPickupCandidate => "집을 후보 없음",
            WorldItemHaulPlanUnloadReason.JobChanged => "경로 또는 목적지 변경",
            WorldItemHaulPlanUnloadReason.Idle => "대기 전 적재물 정리",
            WorldItemHaulPlanUnloadReason.Interrupted => "운반 중단",
            WorldItemHaulPlanUnloadReason.Completed => "배송 완료",
            _ => "진행 중"
        };
    }
}
