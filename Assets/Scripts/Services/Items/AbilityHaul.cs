using System.Collections;
using System;
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
    private const double TransientRecoveryDeadlineSeconds = 15d;
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
    private long runtimeHaulStartCount;
    private long runtimeHaulTerminalCount;
    private string lastTerminalDiagnostics = string.Empty;
    private string capacityRoutingQuiescencePlanFingerprint = string.Empty;
    private readonly HashSet<string> pickedLeaseIds =
        new HashSet<string>(System.StringComparer.Ordinal);
    private readonly HashSet<string> releasedLeaseIds =
        new HashSet<string>(System.StringComparer.Ordinal);
    private System.Action haulMovementProgressCallback;
    private double nextLeaseHeartbeatAt;
    private double haulMovementWorkStartedAt = double.NaN;
    private ICharacterProficiencyCommand proficiencyCommands;
    private IGameCalendar calendar;
    private ICharacterSpeciesCommand speciesCommands;
    private IWorkOrderQuery workOrders;
    private IProductionCapacityRoutingDrainQuery capacityRoutingDrains;
#if UNITY_EDITOR
    public System.Action<WorldItemHaulPlan> DebugBeforeHaulRoutineStart;
    private int diagnosticUpdateHeartbeat;
#endif
#if UNITY_EDITOR
    private IWorldItemStackRuntime editorFixtureItemRuntime;
#endif
    private IWorldItemStackRuntime ItemRuntime =>
#if UNITY_EDITOR
        editorFixtureItemRuntime ??
#endif
        actor?.WorldItemStackRuntime;

    public bool IsHauling => haulExecutionActive;
    public string CurrentPlanSummary => activePlan != null && activePlan.IsValid
        ? activePlan.Summary
        : "운반 계획 없음";
    public string CurrentUnloadReason => ToDisplayText(unloadReason);
    public string CurrentExecutionStage => executionStage;
    public int RoutineHeartbeat => routineHeartbeat;
    public string ActivePathDebug => activePathDebug;
    public string LastFailureReason => lastFailureReason;
    public long RuntimeHaulStartCount => runtimeHaulStartCount;
    public long RuntimeHaulTerminalCount => runtimeHaulTerminalCount;
    public string LastTerminalDiagnostics => lastTerminalDiagnostics;
    public HaulInterruptionDisposition LastInterruptionDisposition { get; private set; }
    public bool HasBoundDeliveryIntent => activePlan != null
        && activePlan.IsDeliveryOnlyResume;

    public bool IsCapacityRoutingQuiescenceFrozen =>
        capacityRoutingQuiescencePlanFingerprint.Length > 0;

#if UNITY_EDITOR
    [GameplayInternalOnly(
        "Binds one exact already-picked delivery plan to an isolated Editor fixture without widening the production haul start API.",
        "Capacity-routing actor transition focused Editor fixture only")]
    public bool TryBindCapacityRoutingEditorFixture(
        IWorldItemStackRuntime itemRuntime,
        WorldItemHaulPlan plan,
        IEnumerable<string> exactPickedLeaseIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        CacheReferences();
        string[] picked = (exactPickedLeaseIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] planned = plan?.ReservedStackQuantities
            .Select(value => value.LeaseId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        if (actor == null
            || itemRuntime == null
            || plan?.IsValid != true
            || picked.Length == 0
            || picked.Distinct(StringComparer.Ordinal).Count() != picked.Length
            || !picked.SequenceEqual(planned, StringComparer.Ordinal)
            || activePlan != null
            || IsCapacityRoutingQuiescenceFrozen)
        {
            failureReason = "capacity-routing-editor-fixture-plan-invalid";
            return false;
        }
        editorFixtureItemRuntime = itemRuntime;
        activePlan = plan;
        pickedLeaseIds.Clear();
        foreach (string leaseId in picked)
            pickedLeaseIds.Add(leaseId);
        releasedLeaseIds.Clear();
        haulExecutionActive = false;
        restoredDeliveryPending = true;
        unloadReason = WorldItemHaulPlanUnloadReason.None;
        executionStage = "capacity-routing-editor-fixture-bound";
        return true;
    }
#endif

    public bool OwnsHaulOperation(string operationId)
    {
        string canonical = operationId ?? string.Empty;
        return canonical.Length > 0
            && string.Equals(canonical, canonical.Trim(), System.StringComparison.Ordinal)
            && GetActivePlanOwnerOperationIds().Contains(
                canonical,
                System.StringComparer.Ordinal);
    }

    public IReadOnlyList<string> CaptureActiveHaulOperationIds() =>
        System.Array.AsReadOnly(GetActivePlanOwnerOperationIds()
            .OrderBy(value => value, System.StringComparer.Ordinal)
            .ToArray());

    [GameplayInternalOnly(
        "Freezes movement only after the active haul plan exactly matches a capacity-routing actor carry vector; leases, admissions, intents, cargo and plan remain authoritative.",
        "Production capacity-routing destructive-drain participant only")]
    public bool TryFreezeForCapacityRoutingQuiescence(
        string actorPersistentId,
        IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData>
            expectedCarries,
        out ProductionCapacityRoutingActorPlanSnapshot snapshot,
        out string failureReason)
    {
        snapshot = null;
        failureReason = string.Empty;
        CacheReferences();
        string actorId = actor?.Identity?.PersistentId ?? string.Empty;
        ProductionCapacityRoutingDrainActorCarrySaveData[] expected =
            (expectedCarries
                ?? System.Array.Empty<
                    ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.carriedStackId,
                System.StringComparer.Ordinal)
            .ToArray();
        if (actor == null
            || actor.CarryInventory == null
            || activePlan == null
            || !activePlan.IsValid
            || !string.Equals(
                actorId,
                actorPersistentId,
                System.StringComparison.Ordinal)
            || expected.Length == 0
            || expected.Any(value => !string.Equals(
                value.actorPersistentId,
                actorId,
                System.StringComparison.Ordinal)))
        {
            failureReason = "capacity-routing-haul-freeze-request-invalid";
            return false;
        }
        if (!actor.CarryInventory.TryPrepareCapacityRoutingExactPhysicalTransfer(
                expected,
                out _,
                out failureReason))
        {
            return false;
        }

        string[] expectedOperations = expected
            .Select(value => value.haulIntentOperationId)
            .Distinct(System.StringComparer.Ordinal)
            .OrderBy(value => value, System.StringComparer.Ordinal)
            .ToArray();
        string[] activeOperations = GetActivePlanOwnerOperationIds()
            .OrderBy(value => value, System.StringComparer.Ordinal)
            .ToArray();
        WorldItemReservedStackQuantity[] reservations = activePlan
            .ReservedStackQuantities
            .OrderBy(value => value.LeaseId, System.StringComparer.Ordinal)
            .ToArray();
        if (!activeOperations.SequenceEqual(
                expectedOperations,
                System.StringComparer.Ordinal)
            || reservations.Length == 0
            || reservations.Any(value =>
                string.IsNullOrWhiteSpace(value.LeaseId)
                || !expectedOperations.Contains(
                    value.OwnerOperationId,
                    System.StringComparer.Ordinal))
            || expectedOperations.Any(operationId => !reservations.Any(value =>
                string.Equals(
                    value.OwnerOperationId,
                    operationId,
                    System.StringComparison.Ordinal)
                && pickedLeaseIds.Contains(value.LeaseId))))
        {
            failureReason = "capacity-routing-haul-freeze-plan-conflict";
            return false;
        }

        WorldItemHaulDestinationKind? destinationKind = null;
        string destinationId = string.Empty;
        Vector2Int deliveryPosition = default;
        Vector2Int dropPosition = default;
        List<string> admissionTokenIds = new();
        foreach (string operationId in expectedOperations)
        {
            if (ItemRuntime == null
                || !ItemRuntime.TryCaptureHaulDeliveryIntent(
                    operationId,
                    out HaulDeliveryIntentSaveData intent)
                || intent == null
                || !string.Equals(
                    intent.ownerCharacterId,
                    actorId,
                    System.StringComparison.Ordinal))
            {
                failureReason =
                    "capacity-routing-haul-freeze-intent-missing:"
                    + operationId;
                return false;
            }
            string[] expectedStacks = expected
                .Where(value => string.Equals(
                    value.haulIntentOperationId,
                    operationId,
                    System.StringComparison.Ordinal))
                .Select(value => value.carriedStackId)
                .OrderBy(value => value, System.StringComparer.Ordinal)
                .ToArray();
            string[] intentStacks = (intent.commitments
                    ?? new List<HaulDeliveryItemCommitmentSaveData>())
                .Where(value => value != null && value.quantity > 0)
                .Select(value => value.carriedStackId)
                .OrderBy(value => value, System.StringComparer.Ordinal)
                .ToArray();
            Vector2Int intentDelivery = new(
                intent.deliveryGridX,
                intent.deliveryGridY);
            Vector2Int intentDrop = new(intent.dropGridX, intent.dropGridY);
            if (!expectedStacks.SequenceEqual(
                    intentStacks,
                    System.StringComparer.Ordinal)
                || destinationKind.HasValue
                && (destinationKind.Value != intent.destinationKind
                    || !string.Equals(
                        destinationId,
                        intent.destinationId,
                        System.StringComparison.Ordinal)
                    || deliveryPosition != intentDelivery
                    || dropPosition != intentDrop))
            {
                failureReason =
                    "capacity-routing-haul-freeze-destination-or-commitment-conflict:"
                    + operationId;
                return false;
            }
            destinationKind ??= intent.destinationKind;
            destinationId = intent.destinationId;
            deliveryPosition = intentDelivery;
            dropPosition = intentDrop;
            admissionTokenIds.AddRange((intent.warehouseAdmissions
                    ?? new List<WarehouseHaulAdmissionSaveData>())
                .Where(value => value != null
                    && !string.IsNullOrWhiteSpace(value.tokenId))
                .Select(value => value.tokenId));
        }

        snapshot = new ProductionCapacityRoutingActorPlanSnapshot(
            actorId,
            expectedOperations,
            reservations.Select(value => value.LeaseId),
            pickedLeaseIds,
            admissionTokenIds.Distinct(System.StringComparer.Ordinal),
            destinationKind ?? activePlan.PrimaryDestination,
            destinationId,
            deliveryPosition,
            dropPosition);
        if (capacityRoutingQuiescencePlanFingerprint.Length > 0)
        {
            if (string.Equals(
                    capacityRoutingQuiescencePlanFingerprint,
                    snapshot.Fingerprint,
                    System.StringComparison.Ordinal))
                return true;
            failureReason = "capacity-routing-haul-freeze-replay-conflict";
            snapshot = null;
            return false;
        }

        haulExecutionActive = false;
        haulMovementWorkStartedAt = double.NaN;
        if (haulingRoutine != null)
            StopCoroutine(haulingRoutine);
        haulingRoutine = null;
        move?.CancelActiveMovement();
        restoredDeliveryPending = true;
        executionStage = "시설 제거 운반 안정화";
        activePathDebug = string.Empty;
        capacityRoutingQuiescencePlanFingerprint = snapshot.Fingerprint;
        return true;
    }

    [GameplayInternalOnly(
        "Validates the exact frozen haul plan before a capacity-routing coordinator mutates any lease, admission or intent authority.",
        "Production capacity-routing actor authority release service only")]
    public bool TryValidateCapacityRoutingQuiescencePlan(
        IReadOnlyCollection<string> expectedOperationIds,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        out string failureReason)
    {
        failureReason = string.Empty;
        CacheReferences();
        string actorId = actor?.Identity?.PersistentId ?? string.Empty;
        string[] operations = (expectedOperationIds
                ?? System.Array.Empty<string>())
            .OrderBy(value => value, System.StringComparer.Ordinal)
            .ToArray();
        if (actor == null
            || activePlan == null
            || !IsCapacityRoutingQuiescenceFrozen
            || receipt == null
            || operations.Length == 0
            || operations.Distinct(System.StringComparer.Ordinal).Count()
                != operations.Length
            || !string.Equals(
                receipt.actorPersistentId,
                actorId,
                System.StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-haul-frozen-plan-unavailable";
            return false;
        }
        string[] activeOperations = GetActivePlanOwnerOperationIds()
            .OrderBy(value => value, System.StringComparer.Ordinal)
            .ToArray();
        string[] activeLeaseIds = activePlan.ReservedStackQuantities
            .Select(value => value.LeaseId)
            .OrderBy(value => value, System.StringComparer.Ordinal)
            .ToArray();
        if (!activeOperations.SequenceEqual(
                operations,
                System.StringComparer.Ordinal)
            || !activeLeaseIds.SequenceEqual(
                receipt.quantityLeaseIds,
                System.StringComparer.Ordinal)
            || !string.Equals(
                capacityRoutingQuiescencePlanFingerprint,
                receipt.activePlanFingerprint,
                System.StringComparison.Ordinal))
        {
            failureReason =
                "capacity-routing-haul-frozen-plan-conflict";
            return false;
        }
        return true;
    }

    [GameplayInternalOnly(
        "Clears a previously frozen active haul plan only after the durable actor receipt's exact leases, admissions and intents have been released.",
        "Production capacity-routing actor authority release service only")]
    public bool TryFinalizeCapacityRoutingQuiescence(
        IReadOnlyCollection<string> expectedOperationIds,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        out string failureReason)
    {
        failureReason = string.Empty;
        CacheReferences();
        string actorId = actor?.Identity?.PersistentId ?? string.Empty;
        string[] operations = (expectedOperationIds
                ?? System.Array.Empty<string>())
            .OrderBy(value => value, System.StringComparer.Ordinal)
            .ToArray();
        if (actor == null
            || receipt == null
            || operations.Length == 0
            || operations.Distinct(System.StringComparer.Ordinal).Count()
                != operations.Length
            || !string.Equals(
                receipt.actorPersistentId,
                actorId,
                System.StringComparison.Ordinal)
            || actor.CarryInventory?.Items.Any(item => item != null
                && item.quantity > 0
                && operations.Contains(
                    item.ownerOperationId,
                    System.StringComparer.Ordinal)) == true
            || operations.Any(operationId => ItemRuntime != null
                && ItemRuntime.TryCaptureHaulDeliveryIntent(
                    operationId,
                    out _)))
        {
            failureReason =
                "capacity-routing-haul-finalize-authority-still-live";
            return false;
        }

        if (activePlan != null)
        {
            string[] activeOperations = GetActivePlanOwnerOperationIds()
                .OrderBy(value => value, System.StringComparer.Ordinal)
                .ToArray();
            string[] activeLeaseIds = activePlan.ReservedStackQuantities
                .Select(value => value.LeaseId)
                .OrderBy(value => value, System.StringComparer.Ordinal)
                .ToArray();
            if (!activeOperations.SequenceEqual(
                    operations,
                    System.StringComparer.Ordinal)
                || !activeLeaseIds.SequenceEqual(
                    receipt.quantityLeaseIds,
                    System.StringComparer.Ordinal)
                || capacityRoutingQuiescencePlanFingerprint.Length > 0
                && !string.Equals(
                    capacityRoutingQuiescencePlanFingerprint,
                    receipt.activePlanFingerprint,
                    System.StringComparison.Ordinal))
            {
                failureReason =
                    "capacity-routing-haul-finalize-plan-conflict";
                return false;
            }
        }

        haulExecutionActive = false;
        haulMovementWorkStartedAt = double.NaN;
        restoredDeliveryPending = false;
        if (haulingRoutine != null)
            StopCoroutine(haulingRoutine);
        haulingRoutine = null;
        move?.CancelActiveMovement();
        actor.CarryInventory?.CompleteHaulingHarness(
            haulingHarnessEquippedForCurrentRun,
            applyWear: false);
        haulingHarnessEquippedForCurrentRun = false;
        activePlan = null;
        pickedLeaseIds.Clear();
        releasedLeaseIds.Clear();
        capacityRoutingQuiescencePlanFingerprint = string.Empty;
        unloadReason = WorldItemHaulPlanUnloadReason.Interrupted;
        executionStage = "시설 제거 운반 권위 해제";
        activePathDebug = string.Empty;
        return true;
    }

    public bool TryStopHaulingIfActiveOperationsSubsetOf(
        IReadOnlyCollection<string> allowedOperationIds,
        string reason,
        HaulInterruptionDisposition disposition,
        out string failureReason)
    {
        failureReason = string.Empty;
        HashSet<string> allowed = new HashSet<string>(
            (allowedOperationIds ?? System.Array.Empty<string>())
            .Select(value => value ?? string.Empty)
            .Where(value => value.Length > 0
                && string.Equals(
                    value,
                    value.Trim(),
                    System.StringComparison.Ordinal)),
            System.StringComparer.Ordinal);
        string[] active = GetActivePlanOwnerOperationIds();
        string foreign = active.FirstOrDefault(value => !allowed.Contains(value));
        if (!string.IsNullOrEmpty(foreign))
        {
            failureReason = "mixed-destination-active-plan:" + foreign;
            return false;
        }

        string foreignCarry = actor?.CarryInventory?.Items
            .Where(item => item != null && item.quantity > 0)
            .Select(item => item.ownerOperationId ?? string.Empty)
            .FirstOrDefault(value => value.Length > 0
                && !allowed.Contains(value));
        if (!string.IsNullOrEmpty(foreignCarry))
        {
            failureReason = "mixed-destination-carried-cargo:" + foreignCarry;
            return false;
        }

        return TryStopHauling(reason, disposition, out failureReason);
    }

    [GameplayInternalOnly(
        "Stops a live haul plan or conservatively drops a detached-restore carried slice whose exact operation IDs were frozen by a custody drain.",
        "Owner-neutral FacilityBuffer destination custody drain only")]
    public bool TryStopHaulingOrReleaseRestoredCarryIfOperationsSubsetOf(
        IReadOnlyCollection<string> allowedOperationIds,
        string reason,
        HaulInterruptionDisposition disposition,
        out string failureReason)
    {
        CacheReferences();
        if (activePlan != null)
        {
            return TryStopHaulingIfActiveOperationsSubsetOf(
                allowedOperationIds,
                reason,
                disposition,
                out failureReason);
        }

        failureReason = string.Empty;
        HashSet<string> allowed = new HashSet<string>(
            (allowedOperationIds ?? Array.Empty<string>())
            .Select(value => value ?? string.Empty)
            .Where(value => value.Length > 0
                && string.Equals(value, value.Trim(),
                    StringComparison.Ordinal)),
            StringComparer.Ordinal);
        CharacterCarryInventory carry = actor?.CarryInventory;
        if (actor == null || carry == null)
        {
            failureReason = "restored-carry-actor-or-inventory-missing";
            return false;
        }
        CharacterCarriedItemSaveData[] carried = carry.Items
            .Where(value => value != null && value.quantity > 0)
            .ToArray();
        if (carried.Length == 0)
        {
            return true;
        }
        string foreign = carried
            .Select(value => value.ownerOperationId ?? string.Empty)
            .FirstOrDefault(value => value.Length == 0
                || !allowed.Contains(value));
        if (foreign != null)
        {
            failureReason = "mixed-or-unowned-restored-carry:"
                + foreign;
            return false;
        }
        string[] ownerOperationIds = carried
            .Select(value => value.ownerOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!TryValidateDetachedRestoredCarryAuthority(
                carried,
                ownerOperationIds,
                out failureReason))
        {
            return false;
        }
        if (ItemRuntime is not IWorldItemCarryRecoveryRuntime recovery)
        {
            failureReason = "restored-carry-recovery-unavailable";
            return false;
        }

        WorldItemCarryInterruptionKind interruptionKind =
            ResolvePhysicalInterruptionKind();
        bool dropped;
        if (interruptionKind is WorldItemCarryInterruptionKind.Downed
            or WorldItemCarryInterruptionKind.Dead)
        {
            double droppedAt = actor.GameClock?.Time ?? 0d;
            HaulCarryDropContext context = new(
                actor.BuildingCharacterId.Value,
                interruptionKind,
                droppedAt,
                droppedAt + TransientRecoveryDeadlineSeconds);
            dropped = recovery.TryDropCarriedItems(
                actor,
                carry,
                ownerOperationIds,
                context,
                out failureReason);
        }
        else
        {
            dropped = recovery.TryDropCarriedItems(
                actor,
                carry,
                ownerOperationIds,
                out failureReason);
        }
        if (!dropped)
        {
            failureReason = "restored-carry-drop-failed:" + failureReason;
            return false;
        }

        foreach (string operationId in ownerOperationIds)
        {
            if (!ItemRuntime.ReleaseHaulDeliveryIntent(operationId))
            {
                failureReason =
                    "restored-carry-intent-release-failed:" + operationId;
                return false;
            }
        }

        actor.CarryInventory.CompleteHaulingHarness(
            haulingHarnessEquippedForCurrentRun,
            applyWear: false);
        haulingHarnessEquippedForCurrentRun = false;
        restoredDeliveryPending = false;
        haulExecutionActive = false;
        executionStage = "복원 운반 화물 회수 완료";
        return true;
    }

    private bool TryValidateDetachedRestoredCarryAuthority(
        IReadOnlyList<CharacterCarriedItemSaveData> carried,
        IReadOnlyList<string> ownerOperationIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        string actorId = actor?.BuildingCharacterId.Value ?? string.Empty;
        if (ItemRuntime == null || actorId.Length == 0)
        {
            failureReason = "restored-carry-authority-runtime-missing";
            return false;
        }

        Dictionary<string, WorldItemStackSnapshot> physicalById = ItemRuntime
            .GetAllStacks()
            .Where(value => value != null && value.Quantity > 0)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        foreach (string operationId in ownerOperationIds)
        {
            if (!ItemRuntime.TryCaptureHaulDeliveryIntent(
                    operationId,
                    out HaulDeliveryIntentSaveData intent)
                || intent == null
                || !intent.HasCommittedPickup
                || !string.Equals(
                    intent.ownerCharacterId,
                    actorId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "restored-carry-intent-authority-missing:" + operationId;
                return false;
            }

            CharacterCarriedItemSaveData[] operationCargo = carried
                .Where(value => string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal))
                .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                .ToArray();
            HaulDeliveryItemCommitmentSaveData[] commitments =
                (intent.commitments
                    ?? new List<HaulDeliveryItemCommitmentSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                .ToArray();
            if (operationCargo.Length == 0
                || operationCargo.Length != commitments.Length)
            {
                failureReason =
                    "restored-carry-intent-commitment-count-mismatch:"
                    + operationId;
                return false;
            }

            for (int index = 0; index < operationCargo.Length; index++)
            {
                CharacterCarriedItemSaveData item = operationCargo[index];
                HaulDeliveryItemCommitmentSaveData commitment =
                    commitments[index];
                string signature = ItemReservationSignature.Create(
                    item.itemId,
                    item.components);
                if (!string.Equals(
                        commitment.carriedStackId,
                        item.carriedStackId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        commitment.sourceStackId,
                        item.sourceStackId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        commitment.itemId,
                        item.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        commitment.expectedStackSignature,
                        signature,
                        StringComparison.Ordinal)
                    || commitment.quantity != item.quantity
                    || !physicalById.TryGetValue(
                        item.carriedStackId,
                        out WorldItemStackSnapshot physical)
                    || physical.State != WorldItemStackState.Carried
                    || !string.Equals(
                        physical.DestinationId,
                        actorId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        physical.ItemInstanceId ?? string.Empty,
                        item.itemInstanceId ?? string.Empty,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        physical.ItemId,
                        item.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        physical.ReservationSignature,
                        signature,
                        StringComparison.Ordinal)
                    || physical.Quantity != item.quantity)
                {
                    failureReason =
                        "restored-carry-physical-intent-mismatch:"
                        + operationId + ":" + item.carriedStackId;
                    return false;
                }
            }
        }
        return true;
    }
#if UNITY_EDITOR
    public bool HasHaulingRoutineForDiagnostics => haulingRoutine != null;
    public int UpdateHeartbeatForDiagnostics => diagnosticUpdateHeartbeat;
    public IReadOnlyList<WorldItemReservedStackQuantity>
        ActiveReservationsForDiagnostics => activePlan?.ReservedStackQuantities
            ?? System.Array.Empty<WorldItemReservedStackQuantity>();
#endif

    private void Update()
    {
#if UNITY_EDITOR
        diagnosticUpdateHeartbeat++;
#endif
        bool activeExecutionHeartbeat = haulExecutionActive
            && activePlan != null;
        bool suspendedDeliveryHeartbeat = restoredDeliveryPending
            && activePlan?.IsDeliveryOnlyResume == true
            && !haulExecutionActive;
        if ((!activeExecutionHeartbeat && !suspendedDeliveryHeartbeat)
            || actor?.GameClock == null
            || actor.GameClock.Time < nextLeaseHeartbeatAt)
        {
            return;
        }

        if (TryHeartbeatActivePlanLeases(out string failureReason))
        {
            return;
        }

        if (activeExecutionHeartbeat)
        {
            // Warehouse admissions have a shorter lease than quantity slices.
            // Movement can legitimately spend more than one heartbeat interval
            // inside a single grid step, so a step-completion callback is not a
            // sufficient renewal clock.  Keep the active route alive from the
            // frame clock and let the running coroutine retire or retain its
            // exact cargo through the normal typed terminal path on failure.
            lastFailureReason = "active-haul-lease-invalid:" + failureReason;
            unloadReason = WorldItemHaulPlanUnloadReason.PickupReservationLost;
            move?.CancelActiveMovement(lastFailureReason);
            return;
        }

        StopHauling(
            "pending-delivery-lease-invalid:" + failureReason,
            HaulInterruptionDisposition
                .ReleaseUnpickedAndDropCarriedAtActor);
    }

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

    [Inject]
    public void ConstructCapacityRoutingDrainGate(
        IProductionCapacityRoutingDrainQuery drainQuery)
    {
        capacityRoutingDrains = drainQuery
            ?? throw new System.ArgumentNullException(nameof(drainQuery));
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            if (IsCapacityRoutingQuiescenceFrozen)
                return;
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
        if (TryGetPendingCarriedCapacityDrain(out string drainingBatch))
        {
            failureReason = "capacity-routing-drain-pending:" + drainingBatch;
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

    private bool TryGetPendingCarriedCapacityDrain(out string batchCommitId)
    {
        batchCommitId = string.Empty;
        if (capacityRoutingDrains == null
            || actor?.CarryInventory?.Items == null)
        {
            return false;
        }

        foreach (CharacterCarriedItemSaveData item in actor.CarryInventory.Items
                     .Where(value => value != null && value.quantity > 0)
                     .OrderBy(value => value.carriedStackId,
                         System.StringComparer.Ordinal))
        {
            if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                    item.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                || !capacityRoutingDrains.IsBatchPending(
                    custody.BatchCommitId))
            {
                continue;
            }

            batchCommitId = custody.BatchCommitId;
            return true;
        }
        return false;
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
        if (TryGetPendingCarriedCapacityDrain(out string drainingBatch))
        {
            actor.Brain?.SetActionPhase(
                "운반 안정화 대기",
                null,
                "capacity-routing-drain-pending:" + drainingBatch);
            EndAiAction(
                CharacterAiActionTerminalKind.Cancelled,
                AIActionFailure.None);
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
            if (resumingRestoredDelivery
                && !TryReturnCarriedItemsAfterInterruptedHaul(
                    reason,
                    ResolvePhysicalInterruptionKind(),
                    out string recoveryFailure))
            {
                restoredDeliveryPending = true;
                haulExecutionActive = false;
                executionStage = "운반 중단 복구 대기";
                EndAiAction(
                    CharacterAiActionTerminalKind.Failed,
                    AIActionFailure.Create(
                        AIActionFailureKind.ResourceUnavailable,
                        recoveryFailure));
                return;
            }
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
        nextLeaseHeartbeatAt = actor.GameClock.Time
            + LeaseHeartbeatIntervalSeconds;
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
        runtimeHaulStartCount++;
        activePathDebug = string.Empty;
        restoredDeliveryPending = false;
        haulExecutionActive = true;
#if UNITY_EDITOR
        try
        {
            DebugBeforeHaulRoutineStart?.Invoke(activePlan);
        }
        catch (Exception)
        {
            if (!TryStopHauling(
                    "editor-before-haul-routine-hook-failed",
                    HaulInterruptionDisposition
                        .ReleaseUnpickedAndDropCarriedAtActor,
                    out string cleanupFailure))
            {
                Debug.LogError(
                    "[AbilityHaul] Editor pre-routine hook failed and haul "
                    + "authority cleanup also failed: " + cleanupFailure);
            }
            throw;
        }
        // Editor verification hooks may synchronously exercise the real
        // cancellation boundary after reservation but before the coroutine.
        // Never resurrect a plan the hook has already terminated.
        if (!haulExecutionActive || activePlan == null)
            return;
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
                // Pickup consumes the source quantity lease. From that point on,
                // the physical carry commitment and destination admission own
                // the delivery; trying to renew the consumed source lease turns
                // a valid carried load into a timing-dependent reservation loss.
                || pickedLeaseIds.Contains(reservation.LeaseId)
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

        bool renewedOperationAuthority = false;
        foreach (string operationId in GetActivePlanOwnerOperationIds())
        {
            if (!leaseRuntime.TryRenewWarehouseAdmissionsForHaul(
                    operationId,
                    out failureReason))
            {
                return false;
            }
            renewedOperationAuthority = true;
        }

        return renewed.Count > 0 || renewedOperationAuthority;
    }

    public void StopHauling(string reason)
    {
        StopHauling(
            reason,
            HaulInterruptionDisposition.ReleaseUnpickedAndDropCarriedAtActor);
    }

    public void StopHaulingForReplan(string reason)
    {
        HaulInterruptionDisposition disposition = actor != null
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && !actor.IsDead
                ? HaulInterruptionDisposition
                    .ReleaseUnpickedAndRetainCarriedForReplan
                : HaulInterruptionDisposition
                    .ReleaseUnpickedAndDropCarriedAtActor;
        StopHauling(reason, disposition);
    }

    public void StopHauling(
        string reason,
        HaulInterruptionDisposition disposition)
    {
        if (!TryStopHauling(reason, disposition, out string failureReason))
        {
            Debug.LogError(
                $"[AbilityHaul] haul interruption retained its authority: "
                + $"{failureReason}; actor={actor?.BuildingCharacterId}");
        }
    }

    public bool TryStopHauling(
        string reason,
        HaulInterruptionDisposition disposition,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (IsCapacityRoutingQuiescenceFrozen)
        {
            failureReason = "capacity-routing-haul-authority-release-frozen";
            return false;
        }
        if (disposition == HaulInterruptionDisposition
                .ReleaseUnpickedAndRetainCarriedForReplan
            && TrySuspendCarriedDeliveryForReplan(reason, stopRoutine: true))
        {
            return true;
        }

        if (!TryReturnCarriedItemsAfterInterruptedHaul(
                reason,
                ResolvePhysicalInterruptionKind(),
                out failureReason))
        {
            executionStage = "운반 중단 복구 대기";
            return false;
        }
        LastInterruptionDisposition = HaulInterruptionDisposition
            .ReleaseUnpickedAndDropCarriedAtActor;

        string[] operationIds = GetActivePlanOwnerOperationIds();
        haulExecutionActive = false;
        haulMovementWorkStartedAt = double.NaN;
        restoredDeliveryPending = false;
        if (haulingRoutine != null)
        {
            StopCoroutine(haulingRoutine);
            haulingRoutine = null;
        }

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
        return true;
    }

    private bool TrySuspendCarriedDeliveryForReplan(
        string reason,
        bool stopRoutine)
    {
        if (activePlan == null || actor?.CarryInventory?.HasItems != true)
            return false;

        HashSet<string> carriedOperations = actor.CarryInventory.Capture().items
            .Where(item => item != null
                && item.quantity > 0
                && !string.IsNullOrWhiteSpace(item.ownerOperationId))
            .Select(item => item.ownerOperationId.Trim())
            .ToHashSet(System.StringComparer.Ordinal);
        WorldItemReservedStackQuantity[] carriedReservations = activePlan
            .ReservedStackQuantities
            .Where(reservation => pickedLeaseIds.Contains(reservation.LeaseId)
                && carriedOperations.Contains(reservation.OwnerOperationId))
            .ToArray();
        if (carriedReservations.Length == 0)
            return false;

        HashSet<string> carriedLeaseIds = carriedReservations
            .Select(reservation => reservation.LeaseId)
            .ToHashSet(System.StringComparer.Ordinal);
        WorldItemHaulPlanLeg[] carriedDeliveryLegs = activePlan.DeliveryLegs
            .Where(leg => carriedLeaseIds.Contains(leg.Reservation.LeaseId))
            .ToArray();
        if (carriedDeliveryLegs.Length == 0)
            return false;

        LastInterruptionDisposition = HaulInterruptionDisposition
            .ReleaseUnpickedAndRetainCarriedForReplan;
        haulExecutionActive = false;
        haulMovementWorkStartedAt = double.NaN;
        if (stopRoutine && haulingRoutine != null)
            StopCoroutine(haulingRoutine);
        haulingRoutine = null;
        move?.CancelActiveMovement();
        ReleaseUnpickedPlanReservations();
        activePlan = new WorldItemHaulPlan(
            System.Array.Empty<WorldItemHaulPlanLeg>(),
            carriedDeliveryLegs,
            carriedReservations,
            activePlan.TotalWeight,
            activePlan.ExpectedDetourCost,
            activePlan.PrimaryDestination,
            activePlan.PrimaryDestinationId,
            deliveryOnlyResume: true,
            isPriority: activePlan.IsPriority);
        pickedLeaseIds.Clear();
        foreach (WorldItemReservedStackQuantity reservation in carriedReservations)
            pickedLeaseIds.Add(reservation.LeaseId);
        actor.CarryInventory.CompleteHaulingHarness(
            haulingHarnessEquippedForCurrentRun,
            applyWear: false);
        haulingHarnessEquippedForCurrentRun = false;
        restoredDeliveryPending = true;
        unloadReason = WorldItemHaulPlanUnloadReason.Interrupted;
        executionStage = "운반 화물 유지·재계획";
        activePathDebug = string.Empty;
        lastFailureReason = reason?.Trim() ?? string.Empty;
        actor.Brain?.RequestImmediateReplan(clearFailures: false);
        return true;
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
        if (!intent.HasCommittedPickup)
            return null;
        ExactWarehouseHaulAdmissionJoin.RetainCommittedAdmissions(intent);
        return intent;
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
        actor?.CarryInventory?.DiscardAllItemsForRestoredWorldReplacement();
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
                if (unloadReason == WorldItemHaulPlanUnloadReason.None)
                {
                    lastFailureReason =
                        $"pickup-unreachable:{pickup.PickupStandPosition}";
                    unloadReason = WorldItemHaulPlanUnloadReason.Interrupted;
                }
                break;
            }

            if (!TryRenewActivePlanLeases(out string renewalReason))
            {
                lastFailureReason = "pickup-lease-expired:" + renewalReason;
                unloadReason = WorldItemHaulPlanUnloadReason.PickupReservationLost;
                break;
            }
            // The explicit pre-pickup renewal establishes a fresh heartbeat
            // window. Without rebasing this deadline, Update can immediately
            // perform a second renewal after a long path-search frame and race
            // the shorter destination admission window.
            nextLeaseHeartbeatAt = actor.GameClock.Time
                + LeaseHeartbeatIntervalSeconds;

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
                if (unloadReason == WorldItemHaulPlanUnloadReason.None)
                {
                    lastFailureReason =
                        $"delivery-unreachable:{delivery.DeliveryPosition}";
                    unloadReason = WorldItemHaulPlanUnloadReason.JobChanged;
                }
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
                // A successful deposit has already transferred the exact carried
                // slices into their physical destination. Retire the logical
                // delivery intent immediately so a save captured before the
                // coroutine reaches FinishHauling cannot observe the same grams
                // as both deposited and still carried. FinishHauling repeats
                // this idempotently as a terminal safety net.
                foreach (string ownerOperationId in ownerOperationIds)
                    itemRuntime.ReleaseHaulDeliveryIntent(ownerOperationId);
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

                if (!TryHeartbeatActivePlanLeases(out string heartbeatFailure))
                {
                    lastFailureReason =
                        "path-search-lease-expired:" + heartbeatFailure;
                    unloadReason =
                        WorldItemHaulPlanUnloadReason.PickupReservationLost;
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
            // Do not postpone the existing heartbeat merely because path
            // search completed.  The warehouse admission may be closer to its
            // 15-second expiry than a new 10-second interval.  Renew when due,
            // otherwise preserve the already scheduled deadline.
            if (!TryHeartbeatActivePlanLeases(out string movementLeaseFailure))
            {
                lastFailureReason =
                    "movement-start-lease-expired:" + movementLeaseFailure;
                unloadReason =
                    WorldItemHaulPlanUnloadReason.PickupReservationLost;
                yield break;
            }
            haulMovementProgressCallback ??= OnHaulMovementProgress;
            haulMovementWorkStartedAt = actor.GameClock != null
                ? actor.GameClock.Time
                : double.NaN;
            yield return move.MoveByPath(
                path,
                expectedAction,
                haulMovementProgressCallback);
            haulMovementWorkStartedAt = double.NaN;
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
        ApplyHaulMovementNeedDepletion();
        if (!TryHeartbeatActivePlanLeases(out string renewalReason))
        {
            lastFailureReason = "movement-lease-expired:" + renewalReason;
            unloadReason = WorldItemHaulPlanUnloadReason.PickupReservationLost;
            move?.CancelActiveMovement();
        }
    }

    private void ApplyHaulMovementNeedDepletion()
    {
        if (actor?.GameClock == null
            || actor.Stats == null
            || double.IsNaN(haulMovementWorkStartedAt))
        {
            return;
        }

        double now = actor.GameClock.Time;
        float elapsedGameSeconds = Mathf.Max(
            0f,
            (float)(now - haulMovementWorkStartedAt));
        haulMovementWorkStartedAt = now;
        if (elapsedGameSeconds > 0f)
        {
            // Hauling is authored as work:haul/work:heavy-haul. Its physical
            // movement must consume the same sleep, bladder and hygiene work
            // budget as facility work; otherwise an always-busy hauler never
            // becomes tired enough to yield to a restored primary service.
            actor.Stats.ApplyWorkNeedDepletion(elapsedGameSeconds);
        }
    }

    private bool TryHeartbeatActivePlanLeases(out string failureReason)
    {
        failureReason = string.Empty;
        if (actor?.GameClock == null)
        {
            failureReason = "game clock missing";
            return false;
        }
        if (actor.GameClock.Time < nextLeaseHeartbeatAt)
            return true;
        if (!TryRenewActivePlanLeases(out failureReason))
            return false;
        nextLeaseHeartbeatAt = actor.GameClock.Time
            + LeaseHeartbeatIntervalSeconds;
        return true;
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
        if (IsCapacityRoutingQuiescenceFrozen)
        {
            lastFailureReason =
                "capacity-routing-haul-authority-release-frozen";
            return;
        }
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
            if (actor != null
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                && !actor.IsDead
                && TrySuspendCarriedDeliveryForReplan(
                    unloadReason.ToString(),
                    stopRoutine: false))
            {
                runtimeHaulTerminalCount++;
                lastTerminalDiagnostics =
                    $"{unloadReason}:{lastFailureReason}:stage={executionStage}";
                EndAiAction(
                    CharacterAiActionTerminalKind.Failed,
                    AIActionFailure.Create(
                        ResolveFailureKind(unloadReason),
                        $"Hauling retained carried cargo for replan: {unloadReason}; "
                        + $"detail={lastFailureReason}."));
                return;
            }

            if (!TryReturnCarriedItemsAfterInterruptedHaul(
                    unloadReason.ToString(),
                    ResolvePhysicalInterruptionKind(),
                    out string recoveryFailure))
            {
                runtimeHaulTerminalCount++;
                lastTerminalDiagnostics =
                    $"{unloadReason}:{recoveryFailure}:stage=recovery-pending";
                restoredDeliveryPending = true;
                haulExecutionActive = false;
                haulMovementWorkStartedAt = double.NaN;
                haulingRoutine = null;
                executionStage = "운반 중단 복구 대기";
                EndAiAction(
                    CharacterAiActionTerminalKind.Failed,
                    AIActionFailure.Create(
                        AIActionFailureKind.ResourceUnavailable,
                        recoveryFailure));
                return;
            }
        }

        runtimeHaulTerminalCount++;
        lastTerminalDiagnostics =
            $"{unloadReason}:{lastFailureReason}:stage={executionStage}";

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
        haulMovementWorkStartedAt = double.NaN;
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

    private bool TryReturnCarriedItemsAfterInterruptedHaul(
        string reason,
        WorldItemCarryInterruptionKind interruptionKind,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterCarryInventory carry = actor?.CarryInventory;
        if (carry?.HasItems != true || activePlan == null)
        {
            return true;
        }

        string[] ownerOperationIds = GetActivePlanOwnerOperationIds();
        if (ownerOperationIds.Length == 0)
        {
            return true;
        }

        if (ItemRuntime is not IWorldItemCarryRecoveryRuntime recovery)
        {
            lastFailureReason = $"carried-item-recovery-unavailable:{reason}";
            Debug.LogError(
                $"[AbilityHaul] {lastFailureReason}; actor={actor?.BuildingCharacterId}");
            failureReason = lastFailureReason;
            return false;
        }

        bool dropped;
        string dropFailure;
        if (interruptionKind is WorldItemCarryInterruptionKind.Downed
            or WorldItemCarryInterruptionKind.Dead)
        {
            double droppedAt = actor.GameClock?.Time ?? 0d;
            HaulCarryDropContext context = new(
                actor.BuildingCharacterId.Value,
                interruptionKind,
                droppedAt,
                droppedAt + TransientRecoveryDeadlineSeconds);
            dropped = recovery.TryDropCarriedItems(
                actor,
                carry,
                ownerOperationIds,
                context,
                out dropFailure);
        }
        else
        {
            dropped = recovery.TryDropCarriedItems(
                actor,
                carry,
                ownerOperationIds,
                out dropFailure);
        }

        if (!dropped)
        {
            lastFailureReason =
                $"carried-item-recovery-failed:{reason}:{dropFailure}";
            Debug.LogError(
                $"[AbilityHaul] {lastFailureReason}; actor={actor?.BuildingCharacterId}");
            failureReason = lastFailureReason;
            return false;
        }

        return true;
    }

    private WorldItemCarryInterruptionKind ResolvePhysicalInterruptionKind()
    {
        if (actor == null)
            return WorldItemCarryInterruptionKind.None;
        if (actor.CurrentLifecycleState == CharacterLifecycleState.Downed)
            return WorldItemCarryInterruptionKind.Downed;
        return actor.Stats?.IsDead == true
            ? WorldItemCarryInterruptionKind.Dead
            : WorldItemCarryInterruptionKind.None;
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
