using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Runtime player-order adapter for a confirmed memory-erasure seal use. The
/// order leases stored stock, drives the selected actor to its exact source,
/// picks one physical unit, and then invokes the synchronous erasure
/// transaction. Post-pickup replans retain the operation-owned cargo.
/// </summary>
public sealed class MemoryErasureSealCommandService :
    IMemoryErasureSealCommandService,
    IStartable,
    ITickable,
    IDisposable
{
    internal const string OperationPrefix =
        "memory-erasure-seal-use:";
    private const string ActionLabel = "기억 소거 인장 사용";
    private const float LeaseRenewalSeconds = 60f;
    private const double RecoveryDeadlineSeconds = 30d;

    private sealed class ActiveOrder
    {
        public CharacterActor Target;
        public CharacterCarryInventory Carry;
        public string OperationId = string.Empty;
        public string TraitInstanceId = string.Empty;
        public MemoryErasureSealOrderStage Stage;
        public WorldItemReservedStackQuantity Reservation;
        public Vector2Int PickupStand;
        public string CarriedStackId = string.Empty;
        public CharacterActionIntentLease ActionLease;
        public Coroutine Routine;
        public MemoryErasureSealUseResult PendingRecoveryResult;
        public bool HasPendingRecoveryResult;
        public WorldItemCarryInterruptionKind? PendingRecoveryKind;
        public bool PendingPrePickupRelease;
        public bool Terminal;
    }

    private readonly IMemoryErasureSealStoredPickupRuntime storedPickup;
    private readonly IWorldItemCarryRecoveryRuntime carryRecovery;
    private readonly IMemoryErasureSealTransactionService transaction;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly ICharacterAiWorldRegistry characters;
    private readonly IGameEventBus events;
    private readonly IGameClock clock;
    private readonly IMemoryErasureOutcomeCommitter outcomeCommitter;
    private readonly Dictionary<string, ActiveOrder> activeByCharacter =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, MemoryErasureSealUseResult>
        latestByCharacter = new(StringComparer.Ordinal);
    private IDisposable downedSubscription;
    private IDisposable deathSubscription;
    private bool disposed;

    public MemoryErasureSealCommandService(
        IWorldItemStackRuntime items,
        IMemoryErasureSealTransactionService transaction,
        IItemQuantityReservationService quantityReservations,
        ICharacterAiWorldRegistry characters,
        IGameEventBus events,
        IGameClock clock,
        IMemoryErasureOutcomeCommitter outcomeCommitter)
    {
        IWorldItemStackRuntime requiredItems = items
            ?? throw new ArgumentNullException(nameof(items));
        storedPickup = requiredItems as IMemoryErasureSealStoredPickupRuntime
            ?? throw new InvalidOperationException(
                "World item runtime lacks the memory-erasure seal stored-pickup port.");
        carryRecovery = requiredItems as IWorldItemCarryRecoveryRuntime
            ?? throw new InvalidOperationException(
                "World item runtime lacks the carried-item recovery port.");
        this.transaction = transaction
            ?? throw new ArgumentNullException(nameof(transaction));
        this.quantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.outcomeCommitter = outcomeCommitter
            ?? throw new ArgumentNullException(nameof(outcomeCommitter));
    }

    public event Action<MemoryErasureSealOrderSnapshot> OrderChanged;
    public event Action<MemoryErasureSealUseResult> UseCompleted;

    [GameplayInternalOnly(
        "Composition starts lifecycle subscriptions and save-derived cargo recovery.",
        "VContainer entry point")]
    public void Start()
    {
        if (disposed)
            return;
        downedSubscription ??=
            events.Subscribe<CharacterBodyHealthDownedEvent>(OnDowned);
        deathSubscription ??=
            events.Subscribe<CharacterDeathEvent>(OnDeath);
        RecoverSavedOperations();
    }

    [GameplayEntryPoint(
        "Warehouse memory-erasure seal Use target/trait selector and focused V25 verifier")]
    public IReadOnlyList<MemoryErasureSealTraitTarget> GetActiveTraitTargets(
        CharacterActor target)
    {
        if (target?.Progression == null)
            return Array.Empty<MemoryErasureSealTraitTarget>();
        return target.Progression.CaptureAcquiredTraitState()
            .CaptureActiveInstances()
            .Select(value => new MemoryErasureSealTraitTarget(
                value.instanceId,
                value.displayName,
                value.description,
                value.moduleIds?.ToArray() ?? Array.Empty<string>()))
            .ToArray();
    }

    [GameplayEntryPoint(
        "Separate warehouse target/active-trait confirmation dialog and focused V25 verifier")]
    public MemoryErasureSealCommandResult TryIssueConfirmedUse(
        string itemId,
        CharacterActor target,
        string traitInstanceId)
    {
        string targetId = target?.BuildingCharacterId.Value ?? string.Empty;
        string traitId = traitInstanceId?.Trim() ?? string.Empty;
        if (!string.Equals(
                itemId,
                MemoryErasureSealItemRules.ItemId,
                StringComparison.Ordinal))
        {
            return CommandResult(
                MemoryErasureSealCommandStatus.InvalidItem,
                targetId,
                traitId,
                detail: "only item:memory-erasure-seal may issue this order");
        }
        if (target == null
            || target.Progression == null
            || targetId.Length == 0
            || traitId.Length == 0)
        {
            return CommandResult(
                MemoryErasureSealCommandStatus.InvalidTarget,
                targetId,
                traitId,
                detail: "a persistent character and selected trait are required");
        }

        string operationId = BuildOperationId(targetId, traitId);
        CharacterAcquiredTraitAggregateState traits =
            target.Progression.CaptureAcquiredTraitState();
        if (!traits.TryGetInstance(
                traitId,
                out CharacterAcquiredTraitInstanceState selected))
        {
            return CommandResult(
                MemoryErasureSealCommandStatus.TraitUnavailable,
                targetId,
                traitId,
                operationId,
                "the selected acquired trait no longer exists");
        }
        if (selected.erased)
        {
            string expectedAudit =
                MemoryErasureSealTransactionService.BuildAuditId(
                    operationId,
                    targetId,
                    traitId);
            return CommandResult(
                string.Equals(
                    selected.erasureAuditId,
                    expectedAudit,
                    StringComparison.Ordinal)
                    ? MemoryErasureSealCommandStatus.AlreadyCompleted
                    : MemoryErasureSealCommandStatus.TraitUnavailable,
                targetId,
                traitId,
                operationId,
                "the selected acquired trait is already erased");
        }
        if (activeByCharacter.ContainsKey(targetId))
        {
            return CommandResult(
                MemoryErasureSealCommandStatus.OrderAlreadyActive,
                targetId,
                traitId,
                operationId,
                "the character already has an active memory-erasure order");
        }
        if (!CanAct(target))
        {
            return CommandResult(
                MemoryErasureSealCommandStatus.CharacterUnavailable,
                targetId,
                traitId,
                operationId,
                "the target character cannot accept a movement order");
        }

        CharacterCarryInventory carry = target.CarryInventory
            ?? CharacterCarryInventory.Ensure(target);
        if (carry == null)
        {
            return CommandResult(
                MemoryErasureSealCommandStatus.CharacterUnavailable,
                targetId,
                traitId,
                operationId,
                "the target character has no physical carry authority");
        }
        if (!target.Brain.TryBeginExternallyDrivenAction(
                operationId,
                CharacterActionIntentKind.ProtectedAction,
                ActionLabel,
                "인장 사용 준비",
                "확정된 기억 소거 인장 주문을 준비합니다.",
                out CharacterActionIntentLease actionLease))
        {
            return CommandResult(
                MemoryErasureSealCommandStatus.ActionOwnershipUnavailable,
                targetId,
                traitId,
                operationId,
                "a higher-priority character action owns the actor");
        }

        if (!storedPickup.TryReserveStoredMemoryErasureSeal(
                target,
                operationId,
                out WorldItemReservedStackQuantity reservation,
                out Vector2Int pickupStand,
                out string reservationFailure))
        {
            target.Brain.CancelExternallyDrivenAction(actionLease);
            return CommandResult(
                MemoryErasureSealCommandStatus.WarehouseSealUnavailable,
                targetId,
                traitId,
                operationId,
                reservationFailure ?? "the exact stored seal is unavailable");
        }

        ActiveOrder order = new()
        {
            Target = target,
            Carry = carry,
            OperationId = operationId,
            TraitInstanceId = traitId,
            Stage = MemoryErasureSealOrderStage.Reserved,
            Reservation = reservation,
            PickupStand = pickupStand,
            ActionLease = actionLease
        };
        activeByCharacter.Add(targetId, order);
        PublishOrder(order, "창고 인장 1개 예약 완료");
        try
        {
            order.Routine = target.StartCoroutine(MovePickupAndApply(order));
        }
        catch (Exception exception)
        {
            InterruptBeforePickup(
                order,
                "movement order startup failed: " + exception.Message);
            return CommandResult(
                MemoryErasureSealCommandStatus.CharacterUnavailable,
                targetId,
                traitId,
                operationId,
                "the physical pickup movement could not start");
        }
        return CommandResult(
            MemoryErasureSealCommandStatus.Accepted,
            targetId,
            traitId,
            operationId,
            "the confirmed physical pickup order was accepted");
    }

    public bool TryGetActiveOrder(
        CharacterActor target,
        out MemoryErasureSealOrderSnapshot order)
    {
        string targetId = target?.BuildingCharacterId.Value ?? string.Empty;
        if (activeByCharacter.TryGetValue(
                targetId,
                out ActiveOrder active)
            && !active.Terminal)
        {
            order = Snapshot(active, string.Empty);
            return true;
        }
        order = default;
        return false;
    }

    public bool TryGetLatestResult(
        CharacterActor target,
        out MemoryErasureSealUseResult result)
    {
        string targetId = target?.BuildingCharacterId.Value ?? string.Empty;
        return latestByCharacter.TryGetValue(targetId, out result);
    }

    [GameplayInternalOnly(
        "Composition ticks suspended post-pickup orders and persisted physical-cargo recovery.",
        "VContainer entry point")]
    public void Tick()
    {
        if (disposed)
            return;
        RecoverSavedOperations();
        foreach (ActiveOrder order in activeByCharacter.Values.ToArray())
        {
            if (order.Terminal)
                continue;
            if (IsDownedOrDead(order.Target))
            {
                InterruptForLifecycle(
                    order,
                    order.Target?.IsDead == true
                        ? WorldItemCarryInterruptionKind.Dead
                    : WorldItemCarryInterruptionKind.Downed);
                continue;
            }
            if (!CanAct(order.Target))
            {
                if (CaptureOwnedCargo(order).Length == 0)
                {
                    InterruptBeforePickup(
                        order,
                        "character became unavailable before pickup");
                }
                else if (order.Stage !=
                         MemoryErasureSealOrderStage.RecoveryPending)
                {
                    order.Routine = null;
                    SetStage(
                        order,
                        MemoryErasureSealOrderStage.SuspendedAfterPickup,
                        "운반 중인 인장을 보존하고 캐릭터 복귀를 기다립니다.");
                }
                continue;
            }
            if (order.Stage == MemoryErasureSealOrderStage.SuspendedAfterPickup
                && order.Routine == null
                && TryAcquireAction(order))
            {
                order.Routine = order.Target.StartCoroutine(
                    ApplyCarried(order));
            }
            else if (order.Stage ==
                          MemoryErasureSealOrderStage.RecoveryPending
                      && order.Routine == null)
            {
                if (order.PendingPrePickupRelease)
                    RetryReleaseBeforePickup(order);
                else
                    TryRecoverCargo(order, order.PendingRecoveryKind);
            }
        }
    }

    private IEnumerator MovePickupAndApply(ActiveOrder order)
    {
        SetStage(
            order,
            MemoryErasureSealOrderStage.MovingToWarehouse,
            "인장 보관 위치로 이동 중");
        GridPathRequestStatus pathStatus = GridPathRequestStatus.Pending;
        Queue<GridMoveStep> path = null;
        while (!order.Terminal && pathStatus == GridPathRequestStatus.Pending)
        {
            if (!CanContinueBeforePickup(order, out string interruption))
            {
                InterruptBeforePickup(order, interruption);
                yield break;
            }
            RenewReservation(order);
            if (!characters.TryGetGrid(out Grid grid))
            {
                InterruptBeforePickup(order, "runtime grid unavailable");
                yield break;
            }
            try
            {
                pathStatus = order.Target.PathSearchBroker.RequestMovePathTo(
                    grid,
                    order.Target.GetNowXY(),
                    order.PickupStand,
                    out path,
                    GridPathSearchPriority.Urgent,
                    GridTraversalContext.ForCharacter(
                        CharacterPersistentIdentity.Require(order.Target)));
            }
            catch (Exception exception)
            {
                InterruptBeforePickup(
                    order,
                    "path request failed: " + exception.Message);
                yield break;
            }
            if (pathStatus == GridPathRequestStatus.Pending)
                yield return null;
        }
        if (order.Terminal)
            yield break;
        if (pathStatus != GridPathRequestStatus.Reachable)
        {
            InterruptBeforePickup(order, "no path to the reserved warehouse seal");
            yield break;
        }

        if (path != null && path.Count > 0)
        {
            AbilityMove move = order.Target.GetAbility<AbilityMove>();
            IEnumerator movement = move.MoveByPath(path);
            while (!order.Terminal)
            {
                bool hasNext;
                try
                {
                    hasNext = movement.MoveNext();
                }
                catch (Exception exception)
                {
                    move.CancelActiveMovement();
                    InterruptBeforePickup(
                        order,
                        "movement execution failed: " + exception.Message);
                    yield break;
                }
                if (!hasNext)
                    break;
                if (!CanContinueBeforePickup(
                        order,
                        out string movementInterruption))
                {
                    move.CancelActiveMovement();
                    InterruptBeforePickup(order, movementInterruption);
                    yield break;
                }
                RenewReservation(order);
                yield return movement.Current;
            }
        }
        if (!CanContinueBeforePickup(order, out string afterMoveInterruption)
            || order.Target.GetNowXY() != order.PickupStand)
        {
            InterruptBeforePickup(
                order,
                afterMoveInterruption.Length > 0
                    ? afterMoveInterruption
                    : "movement ended before the exact pickup stand");
            yield break;
        }

        order.Target.Brain.UpdateExternallyDrivenAction(
            order.ActionLease,
            ActionLabel,
            "인장 집기",
            "예약한 인장 1개를 직접 집습니다.");
        if (!storedPickup.TryPickupStoredMemoryErasureSeal(
                order.Target,
                order.Carry,
                order.Reservation,
                out int pickedUp,
                out string pickupFailure)
            || pickedUp != MemoryErasureSealItemRules.UseQuantity)
        {
            if (CaptureOwnedCargo(order).Length == 0)
            {
                InterruptBeforePickup(order, pickupFailure);
            }
            else
            {
                order.PendingRecoveryResult = UseResult(
                    MemoryErasureSealUseStatus.RecoveryFailed,
                    order,
                    "picked seal source binding failed: " + pickupFailure);
                order.HasPendingRecoveryResult = true;
                order.PendingRecoveryKind = null;
                order.Routine = null;
                TryRecoverCargo(order, typedInterruption: null);
            }
            yield break;
        }

        if (!storedPickup.TryFinalizeStoredMemoryErasureSealPickup(
                order.OperationId,
                order.Reservation.StackId,
                ItemReservationReleaseReason.Completed,
                out string finalizePickupFailure))
        {
            order.PendingRecoveryResult = UseResult(
                MemoryErasureSealUseStatus.RecoveryFailed,
                order,
                "picked seal source-route finalization failed: "
                    + finalizePickupFailure);
            order.HasPendingRecoveryResult = true;
            order.PendingRecoveryKind = null;
            order.Routine = null;
            TryRecoverCargo(order, typedInterruption: null);
            yield break;
        }

        CharacterCarriedItemSaveData[] ownedCargo = CaptureOwnedCargo(order);
        if (ownedCargo.Length != 1
            || ownedCargo[0].quantity != MemoryErasureSealItemRules.UseQuantity)
        {
            order.PendingRecoveryResult = UseResult(
                MemoryErasureSealUseStatus.RecoveryFailed,
                order,
                "picked seal did not resolve to one exact operation-owned cargo stack");
            order.HasPendingRecoveryResult = true;
            SetStage(
                order,
                MemoryErasureSealOrderStage.RecoveryPending,
                order.PendingRecoveryResult.Detail);
            order.Routine = null;
            yield break;
        }
        order.CarriedStackId = ownedCargo[0].carriedStackId;
        order.Reservation = default;
        SetStage(
            order,
            MemoryErasureSealOrderStage.Carrying,
            "인장 1개를 집어 즉시 사용합니다.");
        yield return ApplyCarried(order);
    }

    private IEnumerator ApplyCarried(ActiveOrder order)
    {
        order.Routine = null;
        if (order.Terminal)
            yield break;
        if (IsDownedOrDead(order.Target))
        {
            InterruptForLifecycle(
                order,
                order.Target?.IsDead == true
                    ? WorldItemCarryInterruptionKind.Dead
                    : WorldItemCarryInterruptionKind.Downed);
            yield break;
        }
        if (!OwnsAction(order))
        {
            SetStage(
                order,
                MemoryErasureSealOrderStage.SuspendedAfterPickup,
                "다른 행동이 끝날 때까지 인장을 보존합니다.");
            yield break;
        }

        SetStage(
            order,
            MemoryErasureSealOrderStage.Applying,
            "선택한 획득 특성을 소거 중");
        MemoryErasureSealUseResult result;
        try
        {
            result = transaction.TryErase(
                order.Target,
                order.TraitInstanceId,
                order.Carry,
                order.CarriedStackId,
                order.OperationId);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Memory-erasure seal transaction escaped unexpectedly: {exception}");
            result = UseResult(
                MemoryErasureSealUseStatus.InvalidRequest,
                order,
                "transaction failed before commit: " + exception.Message);
        }
        if (result.Status == MemoryErasureSealUseStatus.Succeeded)
        {
            Complete(order, result, completedAction: true);
            yield break;
        }

        order.PendingRecoveryResult = result;
        order.HasPendingRecoveryResult = true;
        order.PendingRecoveryKind = null;
        order.Routine = null;
        TryRecoverCargo(order, typedInterruption: null);
    }

    private void RecoverSavedOperations()
    {
        foreach (CharacterActor actor in characters.AllCharacters
                     .Where(value => value != null
                         && value.Progression != null))
        {
            string targetId = actor.BuildingCharacterId.Value;
            if (targetId.Length == 0
                || activeByCharacter.ContainsKey(targetId))
            {
                continue;
            }
            CharacterCarryInventory carry = actor.CarryInventory;
            CharacterAcquiredTraitAggregateState acquired =
                actor.Progression.CaptureAcquiredTraitState();
            foreach (CharacterAcquiredTraitInstanceState trait in
                     acquired.instances.Where(value => value != null))
            {
                string operation = BuildOperationId(
                    targetId,
                    trait.instanceId);
                CharacterCarriedItemSaveData[] cargo = carry?.Items
                    .Where(value => value != null
                        && value.quantity > 0
                        && string.Equals(
                            value.ownerOperationId?.Trim(),
                            operation,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.itemId,
                            MemoryErasureSealItemRules.ItemId,
                            StringComparison.Ordinal))
                    .ToArray() ?? Array.Empty<CharacterCarriedItemSaveData>();
                if (cargo.Length == 0)
                {
                    ReleaseSavedUnpickedOperation(operation);
                    continue;
                }

                ActiveOrder restored = new()
                {
                    Target = actor,
                    Carry = carry,
                    OperationId = operation,
                    TraitInstanceId = trait.instanceId,
                    Stage = cargo.Length == 1 && cargo[0].quantity == 1
                        ? MemoryErasureSealOrderStage.SuspendedAfterPickup
                        : MemoryErasureSealOrderStage.RecoveryPending,
                    CarriedStackId = cargo.Length == 1
                        ? cargo[0].carriedStackId
                        : string.Empty
                };
                if (restored.Stage ==
                    MemoryErasureSealOrderStage.RecoveryPending)
                {
                    restored.PendingRecoveryResult = UseResult(
                        MemoryErasureSealUseStatus.RecoveryFailed,
                        restored,
                        "saved seal cargo was not exactly one physical unit");
                    restored.HasPendingRecoveryResult = true;
                    restored.PendingRecoveryKind = null;
                }
                activeByCharacter.Add(targetId, restored);
                PublishOrder(
                    restored,
                    restored.Stage ==
                        MemoryErasureSealOrderStage.SuspendedAfterPickup
                        ? "저장된 운반 인장을 복구해 사용을 재개합니다."
                        : restored.PendingRecoveryResult.Detail);
                break;
            }
        }
    }

    private bool CanContinueBeforePickup(
        ActiveOrder order,
        out string reason)
    {
        if (!CanAct(order.Target))
        {
            reason = "character lifecycle interrupted before pickup";
            return false;
        }
        if (!OwnsAction(order))
        {
            reason = "character action ownership was interrupted before pickup";
            return false;
        }
        if (!quantityReservations.Revalidate(
                order.Reservation.LeaseId,
                out ItemQuantityLease lease,
                out DomainFailure failure)
            || lease.remainingQuantity !=
                MemoryErasureSealItemRules.UseQuantity)
        {
            reason = "reserved seal lease became invalid: " + failure;
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private bool OwnsAction(ActiveOrder order) =>
        order?.Target?.Brain != null
        && order.ActionLease.IsValid
        && order.Target.Brain.UpdateExternallyDrivenAction(
            order.ActionLease,
            ActionLabel,
            order.Stage.ToString(),
            "기억 소거 인장 주문 진행 중");

    private bool TryAcquireAction(ActiveOrder order)
    {
        if (!CanAct(order.Target))
            return false;
        if (!order.Target.Brain.TryBeginExternallyDrivenAction(
                order.OperationId,
                CharacterActionIntentKind.ProtectedAction,
                ActionLabel,
                "인장 사용 재개",
                "운반 중인 인장을 보존한 채 주문을 재개합니다.",
                out CharacterActionIntentLease lease))
        {
            return false;
        }
        order.ActionLease = lease;
        return true;
    }

    private void RenewReservation(ActiveOrder order)
    {
        if (!quantityReservations.Renew(
                order.Reservation.LeaseId,
                clock.Time + LeaseRenewalSeconds,
                out DomainFailure failure))
        {
            Debug.LogWarning(
                $"Memory-erasure seal lease renewal failed for {order.OperationId}: {failure}");
        }
    }

    private void InterruptBeforePickup(ActiveOrder order, string reason)
    {
        if (order.Terminal)
            return;
        bool released = true;
        if (!string.IsNullOrWhiteSpace(order.Reservation.LeaseId))
        {
            released = storedPickup.TryFinalizeStoredMemoryErasureSealPickup(
                    order.OperationId,
                    order.Reservation.StackId,
                    ItemReservationReleaseReason.Cancelled,
                    out string releaseFailure);
            if (!released)
            {
                reason = (reason ?? string.Empty)
                    + "; lease/source-route release failed: "
                    + releaseFailure;
            }
        }
        MemoryErasureSealUseResult result = UseResult(
            MemoryErasureSealUseStatus.InterruptedBeforePickup,
            order,
            reason);
        if (!released)
        {
            order.PendingRecoveryResult = result;
            order.HasPendingRecoveryResult = true;
            order.PendingPrePickupRelease = true;
            order.Routine = null;
            SetStage(
                order,
                MemoryErasureSealOrderStage.RecoveryPending,
                reason);
            return;
        }
        Complete(order, result, completedAction: false);
    }

    private void RetryReleaseBeforePickup(ActiveOrder order)
    {
        if (!storedPickup.TryFinalizeStoredMemoryErasureSealPickup(
                order.OperationId,
                order.Reservation.StackId,
                ItemReservationReleaseReason.Cancelled,
                out string releaseFailure))
        {
            SetStage(
                order,
                MemoryErasureSealOrderStage.RecoveryPending,
                "lease/source-route release retry failed: " + releaseFailure);
            return;
        }

        order.PendingPrePickupRelease = false;
        MemoryErasureSealUseResult result = order.HasPendingRecoveryResult
            ? order.PendingRecoveryResult
            : UseResult(
                MemoryErasureSealUseStatus.InterruptedBeforePickup,
                order,
                "the unpicked seal lease and source route were restored");
        Complete(order, result, completedAction: false);
    }

    private void ReleaseSavedUnpickedOperation(string operationId)
    {
        if (!quantityReservations.TryGetLeasesByOwner(
                operationId,
                out IReadOnlyList<ItemQuantityLease> leases))
        {
            return;
        }
        string[] stackIds = leases
            .SelectMany(value => value?.slices
                ?? new List<ItemLeaseSlice>())
            .Where(value => value != null)
            .Select(value => value.stackId?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string stackId in stackIds)
        {
            if (!storedPickup.TryFinalizeStoredMemoryErasureSealPickup(
                    operationId,
                    stackId,
                    ItemReservationReleaseReason.WorldRestore,
                    out string failure))
            {
                Debug.LogError(
                    $"Saved memory-erasure seal reservation recovery failed for {operationId}:{stackId}: {failure}");
            }
        }
    }

    private void InterruptForLifecycle(
        ActiveOrder order,
        WorldItemCarryInterruptionKind kind)
    {
        if (order.Terminal)
            return;
        if (CaptureOwnedCargo(order).Length == 0)
        {
            InterruptBeforePickup(order, kind + " before pickup");
            return;
        }

        order.PendingRecoveryResult = UseResult(
            MemoryErasureSealUseStatus.InterruptedAfterPickup,
            order,
            kind + " after pickup; exact current-cell recovery required");
        order.HasPendingRecoveryResult = true;
        order.PendingRecoveryKind = kind;
        order.Routine = null;
        TryRecoverCargo(order, kind);
    }

    private void TryRecoverCargo(
        ActiveOrder order,
        WorldItemCarryInterruptionKind? typedInterruption)
    {
        if (order.Terminal)
            return;
        string failure;
        bool dropped;
        if (!typedInterruption.HasValue && IsDownedOrDead(order.Target))
        {
            typedInterruption = order.Target?.IsDead == true
                ? WorldItemCarryInterruptionKind.Dead
                : WorldItemCarryInterruptionKind.Downed;
        }
        if (typedInterruption.HasValue)
        {
            string[] owner = { order.OperationId };
            double droppedAt = Math.Max(0d, clock.Time);
            HaulCarryDropContext context = new(
                order.Target.BuildingCharacterId.Value,
                typedInterruption.Value,
                droppedAt,
                droppedAt + RecoveryDeadlineSeconds);
            dropped = carryRecovery.TryDropCarriedItems(
                order.Target,
                order.Carry,
                owner,
                context,
                out failure);
        }
        else
        {
            dropped = storedPickup
                .TryReturnCarriedMemoryErasureSealToStoredSource(
                order.Target,
                order.Carry,
                order.OperationId,
                out failure);
        }
        if (dropped)
        {
            MemoryErasureSealUseResult result =
                order.HasPendingRecoveryResult
                    ? order.PendingRecoveryResult
                    : UseResult(
                        MemoryErasureSealUseStatus.InterruptedAfterPickup,
                        order,
                        typedInterruption.HasValue
                            ? "operation-owned cargo recovered at the actor's current cell"
                            : "operation-owned cargo returned to its exact stored source");
            Complete(order, result, completedAction: false);
            return;
        }

        order.PendingRecoveryResult = UseResult(
            MemoryErasureSealUseStatus.RecoveryFailed,
            order,
            typedInterruption.HasValue
                ? "exact current-cell cargo recovery failed: " + failure
                : "exact stored-source cargo return failed: " + failure);
        order.HasPendingRecoveryResult = true;
        order.PendingRecoveryKind = typedInterruption;
        order.Routine = null;
        SetStage(
            order,
            MemoryErasureSealOrderStage.RecoveryPending,
            order.PendingRecoveryResult.Detail);
    }

    private void Complete(
        ActiveOrder order,
        MemoryErasureSealUseResult result,
        bool completedAction)
    {
        if (order.Terminal)
            return;
        order.Terminal = true;
        order.Routine = null;
        if (order.ActionLease.IsValid && order.Target?.Brain != null)
        {
            if (completedAction)
                order.Target.Brain.EndExternallyDrivenAction(order.ActionLease);
            else
                order.Target.Brain.CancelExternallyDrivenAction(order.ActionLease);
        }
        string targetId = order.Target?.BuildingCharacterId.Value ?? string.Empty;
        activeByCharacter.Remove(targetId);
        latestByCharacter[targetId] = result;
        if (result.Status == MemoryErasureSealUseStatus.Succeeded)
        {
            order.Target?.AddLog("기억 소거 인장을 사용해 선택한 획득 특성을 지웠습니다.");
        }
        PublishOrderSafely(new MemoryErasureSealOrderSnapshot(
            order.OperationId,
            targetId,
            order.TraitInstanceId,
            MemoryErasureSealOrderStage.None,
            order.CarriedStackId,
            result.Detail));
        try
        {
            MemoryErasureOutcomeReceipt outcomeReceipt = new(
                result,
                Mathf.Max(
                    0,
                    Mathf.FloorToInt(
                        clock.Time / GameCalendarRules.SecondsPerDay)));
            if (!outcomeCommitter.TryPrepareTerminal(
                    outcomeReceipt,
                    out PreparedEvolutionOutcome preparedOutcome,
                    out string outcomePrepareFailure))
            {
                Debug.LogError(
                    "Memory-erasure terminal outcome recording failed: "
                    + outcomePrepareFailure);
            }
            else if (!outcomeCommitter.TryCommit(
                         preparedOutcome,
                         out string outcomeCommitFailure))
            {
                Debug.LogError(
                    "Memory-erasure terminal outcome commit failed: "
                    + outcomeCommitFailure);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Memory-erasure terminal outcome receipt was invalid: "
                + exception);
        }
        try
        {
            events.Publish(new MemoryErasureSealUseCompletedEvent(result));
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Memory-erasure seal domain completion observer failed: "
                + exception);
        }
        try
        {
            UseCompleted?.Invoke(result);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Memory-erasure seal UI completion observer failed: "
                + exception);
        }
    }

    private void SetStage(
        ActiveOrder order,
        MemoryErasureSealOrderStage stage,
        string detail)
    {
        if (order.Terminal)
            return;
        order.Stage = stage;
        PublishOrder(order, detail);
    }

    private void PublishOrder(ActiveOrder order, string detail)
    {
        PublishOrderSafely(Snapshot(order, detail));
    }

    private void PublishOrderSafely(MemoryErasureSealOrderSnapshot snapshot)
    {
        try
        {
            OrderChanged?.Invoke(snapshot);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Memory-erasure seal order observer failed: " + exception);
        }
    }

    private static MemoryErasureSealOrderSnapshot Snapshot(
        ActiveOrder order,
        string detail) => new(
            order.OperationId,
            order.Target?.BuildingCharacterId.Value ?? string.Empty,
            order.TraitInstanceId,
            order.Stage,
            order.CarriedStackId.Length > 0
                ? order.CarriedStackId
                : order.Reservation.StackId,
            detail);

    private static CharacterCarriedItemSaveData[] CaptureOwnedCargo(
        ActiveOrder order) => order?.Carry?.Items
            .Where(value => value != null
                && value.quantity > 0
                && string.Equals(
                    value.ownerOperationId?.Trim(),
                    order.OperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.itemId,
                    MemoryErasureSealItemRules.ItemId,
                    StringComparison.Ordinal))
            .ToArray() ?? Array.Empty<CharacterCarriedItemSaveData>();

    private static bool CanAct(CharacterActor actor) => actor != null
        && actor.isActiveAndEnabled
        && actor.gameObject.activeInHierarchy
        && !actor.IsDead
        && actor.CurrentLifecycleState == CharacterLifecycleState.Active
        && actor.Brain != null
        && actor.PathSearchBroker != null
        && actor.GetAbility<AbilityMove>() != null;

    private static bool IsDownedOrDead(CharacterActor actor) => actor == null
        || actor.IsDead
        || actor.CurrentLifecycleState == CharacterLifecycleState.Downed
        || actor.CurrentLifecycleState == CharacterLifecycleState.Despawned;

    internal static string BuildOperationId(
        string targetCharacterId,
        string traitInstanceId) => OperationPrefix
        + NarrativeInferenceHash.ComputeSha256Utf8(
            (targetCharacterId?.Trim() ?? string.Empty)
            + "|" + (traitInstanceId?.Trim() ?? string.Empty));

    private static MemoryErasureSealCommandResult CommandResult(
        MemoryErasureSealCommandStatus status,
        string targetId,
        string traitId,
        string operationId = "",
        string detail = "") => new(
            status,
            operationId,
            targetId,
            traitId,
            detail);

    private static MemoryErasureSealUseResult UseResult(
        MemoryErasureSealUseStatus status,
        ActiveOrder order,
        string detail) => new(
            status,
            order.OperationId,
            MemoryErasureSealTransactionService.BuildAuditId(
                order.OperationId,
                order.Target?.BuildingCharacterId.Value ?? string.Empty,
                order.TraitInstanceId),
            order.Target?.BuildingCharacterId.Value ?? string.Empty,
            order.TraitInstanceId,
            string.Empty,
            order.Target?.Progression?.AcquiredTraitRevision ?? 0,
            order.Target?.Progression?.AcquiredTraitRevision ?? 0,
            0L,
            detail);

    private void OnDowned(CharacterBodyHealthDownedEvent downed)
    {
        if (downed.Actor == null)
            return;
        string targetId = downed.Actor.BuildingCharacterId.Value;
        if (activeByCharacter.TryGetValue(targetId, out ActiveOrder order))
            InterruptForLifecycle(order, WorldItemCarryInterruptionKind.Downed);
    }

    private void OnDeath(CharacterDeathEvent death)
    {
        if (activeByCharacter.TryGetValue(
                death.CharacterId.Value,
                out ActiveOrder order))
        {
            InterruptForLifecycle(order, WorldItemCarryInterruptionKind.Dead);
        }
    }

    [GameplayInternalOnly(
        "Composition disposal releases only unpicked leases; persisted carried cargo remains under the physical save authority.",
        "VContainer entry point")]
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        downedSubscription?.Dispose();
        deathSubscription?.Dispose();
        downedSubscription = null;
        deathSubscription = null;
        foreach (ActiveOrder order in activeByCharacter.Values.ToArray())
        {
            if (CaptureOwnedCargo(order).Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(order.Reservation.StackId))
                {
                    storedPickup.TryFinalizeStoredMemoryErasureSealPickup(
                        order.OperationId,
                        order.Reservation.StackId,
                        ItemReservationReleaseReason.Cancelled,
                        out _);
                }
                else
                {
                    ReleaseSavedUnpickedOperation(order.OperationId);
                }
            }
            if (order.ActionLease.IsValid && order.Target?.Brain != null)
                order.Target.Brain.CancelExternallyDrivenAction(order.ActionLease);
        }
        activeByCharacter.Clear();
    }
}
