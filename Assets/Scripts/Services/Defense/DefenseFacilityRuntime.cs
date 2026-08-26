using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IDefenseFacilityRuntime
{
    DefenseFacilitySnapshot GetSnapshot(DefenseFacility facility);
    bool CanActivate(
        DefenseFacility facility,
        CharacterActor target,
        DefenseTriggerTiming timing,
        out DomainFailure failure);
    bool TryBeginActivation(
        DefenseFacility facility,
        CharacterActor target,
        DefenseTriggerTiming timing,
        out DefenseActivationAuthorization authorization,
        out DomainFailure failure);
    void CompleteActivation(
        DefenseFacility facility,
        DefenseActivationAuthorization authorization);
    bool SetArmingPolicy(
        DefenseFacility facility,
        DefenseArmingPolicy policy);
    bool SetAllowed(
        DefenseFacility facility,
        DoorAccessGroup group,
        bool allowed);
    bool SetAllowed(
        DefenseFacility facility,
        string persistentId,
        bool allowed);
    bool TryRequestReload(
        DefenseFacility facility,
        out DomainFailure failure);
    bool TryClearJam(DefenseFacility facility, out DomainFailure failure);
    bool TryRepair(
        DefenseFacility facility,
        float condition,
        out DomainFailure failure);
}

public sealed class DefenseFacilityRuntime : IDefenseFacilityRuntime
{
    private const string MixedDefenseAmmunitionBoxItemId =
        "supply:defense-mixed-ammo-box";
    private const int MixedDefenseAmmunitionUnitsPerBox = 8;

    private readonly IWorldItemStackRuntime items;
    private readonly IDefenseFacilityPhysicalItemGateway physicalItems;
    private readonly IPowerInfrastructureQuery power;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private readonly IDefenseFacilityNetworkRuntime facilityNetwork;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private DefenseFacilityAggregateState Current =>
        aggregateRootStore.GetOrCreate(
            () => new DefenseFacilityAggregateState());
    private DefenseFacilityAggregateState Writable =>
        aggregateRootStore.GetOrCreateWritable(
            () => new DefenseFacilityAggregateState(),
            state => state.DeepClone());

    internal IReadOnlyCollection<DefenseFacilityState> States => Current.States;

    public DefenseFacilityRuntime(
        IWorldItemStackRuntime items,
        IDefenseFacilityPhysicalItemGateway physicalItems,
        IGameClock clock,
        IGameEventBus events,
        IPowerInfrastructureQuery power,
        IDefenseFacilityNetworkRuntime facilityNetwork,
        IFacilityCapabilityQuery facilities,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.facilityNetwork = facilityNetwork
            ?? throw new ArgumentNullException(nameof(facilityNetwork));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public DefenseFacilitySnapshot GetSnapshot(DefenseFacility facility)
    {
        DefenseFacilityState state = GetOrCreate(facility);
        RefreshPassiveState(facility, state);
        return new DefenseFacilitySnapshot(
            state,
            state.cooldownUntil - clock.Time,
            IsPowered(facility),
            BuildSupplyDestinationId(facility));
    }

    public bool CanActivate(
        DefenseFacility facility,
        CharacterActor target,
        DefenseTriggerTiming timing,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (facility == null || facility.Defense == null)
        {
            failure = new DomainFailure(FailureCode.DefenseFacilityUnavailable);
            return false;
        }

        DefenseFacilityState state = GetOrCreate(facility);
        RefreshPassiveState(facility, state);
        if (!CanTarget(state, target, timing))
        {
            failure = new DomainFailure(
                state.armingPolicy == DefenseArmingPolicy.Manual
                    ? FailureCode.DefenseManualActivationRequired
                    : FailureCode.DefenseTargetDisallowed);
            SetBlockedFailure(state, failure);
            return false;
        }

        if (state.condition < 25f && !state.forcedDangerousOperation)
        {
            state.operationalState = DefenseFacilityOperationalState.Damaged;
            failure = new DomainFailure(
                FailureCode.DefenseConditionCritical,
                state.condition.ToString("0", CultureInfo.InvariantCulture));
            SetBlockedFailure(state, failure);
            return false;
        }

        if (facility.Defense.requiresPower && !IsPowered(facility))
        {
            state.operationalState =
                DefenseFacilityOperationalState.Unpowered;
            failure = new DomainFailure(FailureCode.DefensePowerUnavailable);
            SetBlockedFailure(state, failure);
            PublishState(state);
            return false;
        }

        if (facility.BuildingData?.id == 1805
            && facilityNetwork?.HasAutomaticControl(facility) != true
            && facilities.FindOperational(
                ResearchFacilityCommandKind.DefenseControl).Count == 0)
        {
            state.operationalState =
                DefenseFacilityOperationalState.Preparing;
            failure = new DomainFailure(
                FailureCode.DefenseAutomaticControlUnavailable);
            SetBlockedFailure(state, failure);
            PublishState(state);
            return false;
        }

        if (state.cooldownUntil > clock.Time)
        {
            state.operationalState = DefenseFacilityOperationalState.Cooldown;
            failure = new DomainFailure(
                FailureCode.DefenseCooldownActive,
                (state.cooldownUntil - clock.Time).ToString(
                    "0.0",
                    CultureInfo.InvariantCulture));
            SetBlockedFailure(state, failure);
            return false;
        }

        if (!EnsureSupply(facility, state, out failure))
        {
            SetBlockedFailure(state, failure);
            return false;
        }

        state.operationalState = DefenseFacilityOperationalState.Ready;
        state.blockedReason = string.Empty;
        return true;
    }

    public bool TryBeginActivation(
        DefenseFacility facility,
        CharacterActor target,
        DefenseTriggerTiming timing,
        out DefenseActivationAuthorization authorization,
        out DomainFailure failure)
    {
        authorization = default;
        if (!CanActivate(facility, target, timing, out failure))
        {
            return false;
        }

        DefenseFacilityState state = GetOrCreate(facility);
        DefenseFacilityData data = facility.Defense;
        if (data.UsesPhysicalSupply)
        {
            state.supply = Mathf.Max(
                0,
                state.supply - Mathf.Max(1, data.supplyPerActivation));
        }

        state.activationCount++;
        float wear = Mathf.Max(0f, data.conditionLossPerActivation);
        state.condition = Mathf.Clamp(state.condition - wear, 0f, 100f);
        float wearRisk = Mathf.InverseLerp(75f, 0f, state.condition);
        bool jammed = Roll(
            state,
            "jam",
            Mathf.Clamp01(data.baseJamChance + wearRisk * 0.22f));
        bool misfired = !jammed && Roll(
            state,
            "misfire",
            Mathf.Clamp01(data.baseMisfireChance + wearRisk * 0.15f));
        float multiplier = jammed ? 0f : misfired ? 0.5f : 1f;
        state.operationalState = jammed
            ? DefenseFacilityOperationalState.Jammed
            : DefenseFacilityOperationalState.Triggered;
        SetBlockedFailure(
            state,
            jammed
                ? new DomainFailure(FailureCode.DefenseMechanicalJam)
                : misfired
                    ? new DomainFailure(FailureCode.DefensePartialMisfire)
                    : DomainFailure.None);
        authorization = new DefenseActivationAuthorization(
            true,
            jammed,
            misfired,
            multiplier);
        PublishState(state);
        return true;
    }

    public void CompleteActivation(
        DefenseFacility facility,
        DefenseActivationAuthorization authorization)
    {
        if (facility == null)
        {
            return;
        }

        DefenseFacilityState state = GetOrCreate(facility);
        if (authorization.Jammed)
        {
            return;
        }

        state.cooldownUntil = DefenseFacilityRules.ResolveCooldown(
            clock.Time,
            facility.Defense.cooldownSeconds,
            state.growth.resetSpeedLevel);
        state.operationalState = state.cooldownUntil > clock.Time
            ? DefenseFacilityOperationalState.Cooldown
            : DefenseFacilityOperationalState.Ready;
        PublishState(state);
    }

    public bool SetArmingPolicy(
        DefenseFacility facility,
        DefenseArmingPolicy policy)
    {
        DefenseFacilityState state = GetOrCreate(facility);
        state.armingPolicy = policy;
        state.operationalState = policy == DefenseArmingPolicy.Manual
            ? DefenseFacilityOperationalState.Disarmed
            : DefenseFacilityOperationalState.Preparing;
        PublishState(state);
        return true;
    }

    public bool SetAllowed(
        DefenseFacility facility,
        DoorAccessGroup group,
        bool allowed)
    {
        if (group == DoorAccessGroup.None)
        {
            return false;
        }

        DefenseFacilityState state = GetOrCreate(facility);
        DoorAccessGroup groups = (DoorAccessGroup)state.allowedGroups;
        groups = allowed ? groups | group : groups & ~group;
        state.allowedGroups = (int)groups;
        return true;
    }

    public bool TryRequestReload(
        DefenseFacility facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (facility?.Defense?.UsesPhysicalSupply != true)
        {
            failure = new DomainFailure(
                FailureCode.DefensePhysicalSupplyUnsupported);
            return false;
        }

        DefenseFacilityState state = GetOrCreate(facility);
        int capacity = Mathf.Max(
            1,
            facility.Defense.supplyCapacity
                + state.growth.capacityLevel);
        if (state.supply >= capacity)
        {
            failure = new DomainFailure(FailureCode.DefenseSupplyCapacityFull);
            return false;
        }

        RequestMissingSupply(facility, state, capacity);
        string destinationId = BuildSupplyDestinationId(facility);
        bool requested = HasPendingSupply(destinationId);
        state.operationalState = requested
            ? DefenseFacilityOperationalState.Reloading
            : DefenseFacilityOperationalState.Empty;
        failure = new DomainFailure(
            requested
                ? FailureCode.DefenseSupplyDeliveryPending
                : FailureCode.DefenseSupplyUnavailable);
        SetBlockedFailure(state, failure);
        PublishState(state);
        return requested;
    }

    public bool SetAllowed(
        DefenseFacility facility,
        string persistentId,
        bool allowed)
    {
        string normalized = persistentId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return false;
        }

        DefenseFacilityState state = GetOrCreate(facility);
        state.allowedPersistentIds ??= new List<string>();
        state.allowedPersistentIds.RemoveAll(
            value => string.Equals(value, normalized, StringComparison.Ordinal));
        if (allowed)
        {
            state.allowedPersistentIds.Add(normalized);
        }

        return true;
    }

    public bool TryClearJam(
        DefenseFacility facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        DefenseFacilityState state = GetOrCreate(facility);
        if (state.pendingMaintenance.phase
            != DefenseFacilityPhysicalCommitPhase.None)
        {
            if (TryRecoverMaintenanceCommit(facility, state, out bool completed)
                && completed)
            {
                return true;
            }
            failure = new DomainFailure(
                FailureCode.DefenseMaintenanceDeliveryPending,
                "physical-commit-pending");
            return false;
        }
        if (state.operationalState != DefenseFacilityOperationalState.Jammed)
        {
            failure = new DomainFailure(FailureCode.DefenseNotJammed);
            return false;
        }

        string destinationId = BuildMaintenanceDestinationId(facility);
        bool consumed = DefenseFacilityPhysicalTransactionOutbox.TryCommitOrResume(
            state.pendingMaintenance,
            DefenseFacilityPhysicalCommitKind.MaintenanceSink,
            state.facilityPersistentId,
            state.nextMaintenanceOperationSequence,
            destinationId,
            DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId,
            1,
            state.supply,
            0,
            physicalItems,
            out _,
            out _);
        if (!consumed)
        {
            bool pending = HasPendingSupply(destinationId);
            if (!pending)
            {
                items.TryRequestItemDelivery(
                    DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId,
                    1,
                    facility.centerPos,
                    destinationId,
                    out _,
                    out _);
                pending = HasPendingSupply(destinationId);
            }

            failure = new DomainFailure(
                pending
                    ? FailureCode.DefenseMaintenanceDeliveryPending
                    : FailureCode.DefenseMaintenancePartMissing,
                DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId,
                "1");
            SetBlockedFailure(state, failure);
            PublishState(state);
            return false;
        }

        if (TryRecoverMaintenanceCommit(facility, state, out bool recovered)
            && recovered)
        {
            return true;
        }
        failure = new DomainFailure(
            FailureCode.DefenseMaintenanceDeliveryPending,
            "physical-acknowledgement-pending");
        return false;
    }

    public bool TryRepair(
        DefenseFacility facility,
        float condition,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        DefenseFacilityState state = GetOrCreate(facility);
        if (condition <= 0f)
        {
            failure = new DomainFailure(FailureCode.DefenseRepairAmountInvalid);
            return false;
        }

        state.condition = Mathf.Clamp(state.condition + condition, 0f, 100f);
        state.forcedDangerousOperation = false;
        RefreshPassiveState(facility, state);
        PublishState(state);
        return true;
    }

    public DefenseFacilitySaveData CaptureState()
    {
        return new DefenseFacilitySaveData
        {
            facilities = Current.States
                .OrderBy(value => value.facilityPersistentId, StringComparer.Ordinal)
                .Select(ToSaveData)
                .ToList()
        };
    }

    public DefenseFacilityRestoreCandidate PrepareRestoreState(
        DefenseFacilitySaveData data)
    {
        IReadOnlyList<string> errors = DefenseFacilitySaveRules.Validate(data);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Defense-facility restore candidate is invalid: "
                + string.Join(" | ", errors));
        }

        DefenseFacilityAggregateState aggregate = new();
        foreach (DefenseFacilityRecordSaveData source in data.facilities)
        {
            aggregate.Add(FromSaveData(
                source ?? throw new InvalidOperationException(
                    "Validated defense save contained a null record.")));
        }
        return new DefenseFacilityRestoreCandidate(aggregate);
    }

    public void PublishRestoreState(DefenseFacilityRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        aggregateRootStore.Replace(candidate.State);
    }

    private bool EnsureSupply(
        DefenseFacility facility,
        DefenseFacilityState state,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        DefenseFacilityData data = facility.Defense;
        if (!data.UsesPhysicalSupply)
        {
            return true;
        }

        if (state.pendingSupply.phase != DefenseFacilityPhysicalCommitPhase.None
            && !TryRecoverSupplyCommit(facility, state, out _))
        {
            failure = new DomainFailure(
                FailureCode.DefenseSupplyDeliveryPending,
                "physical-commit-pending");
            SetBlockedFailure(state, failure);
            PublishState(state);
            return false;
        }

        int required = Mathf.Max(1, data.supplyPerActivation);
        if (state.supply >= required)
        {
            return true;
        }

        string destinationId = BuildSupplyDestinationId(facility);
        int capacity = Mathf.Max(required, data.supplyCapacity)
            + Mathf.Max(0, state.growth.capacityLevel);
        int wanted = Mathf.Max(0, capacity - state.supply);
        if (wanted > 0)
        {
            string authoredItemId = data.supplyItemId?.Trim() ?? string.Empty;
            bool consumed = authoredItemId.Length > 0
                && TryBeginSupplyCommit(
                    facility,
                    state,
                    destinationId,
                    authoredItemId,
                    wanted,
                    wanted);
            if (!consumed
                && data.supplyCategory == StockCategory.Ammunition
                && wanted >= MixedDefenseAmmunitionUnitsPerBox)
            {
                int boxes = wanted / MixedDefenseAmmunitionUnitsPerBox;
                consumed = boxes > 0 && TryBeginSupplyCommit(
                    facility,
                    state,
                    destinationId,
                    MixedDefenseAmmunitionBoxItemId,
                    boxes,
                    boxes * MixedDefenseAmmunitionUnitsPerBox);
            }
            if (consumed
                && !TryRecoverSupplyCommit(facility, state, out _))
            {
                failure = new DomainFailure(
                    FailureCode.DefenseSupplyDeliveryPending,
                    "physical-acknowledgement-pending");
                SetBlockedFailure(state, failure);
                PublishState(state);
                return false;
            }
        }

        if (state.supply >= required)
        {
            state.operationalState = DefenseFacilityOperationalState.Ready;
            return true;
        }

        RequestMissingSupply(facility, state, capacity);
        state.operationalState = HasPendingSupply(destinationId)
            ? DefenseFacilityOperationalState.Reloading
            : DefenseFacilityOperationalState.Empty;
        failure = new DomainFailure(
            state.operationalState == DefenseFacilityOperationalState.Reloading
                ? FailureCode.DefenseSupplyDeliveryPending
                : FailureCode.DefenseSupplyUnavailable);
        SetBlockedFailure(state, failure);
        PublishState(state);
        return false;
    }

    private void RequestMissingSupply(
        DefenseFacility facility,
        DefenseFacilityState state,
        int capacity)
    {
        DefenseFacilityData data = facility.Defense;
        string destinationId = BuildSupplyDestinationId(facility);
        int pending = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        int wanted = Mathf.Max(0, capacity - state.supply - pending);
        if (wanted <= 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(data.supplyItemId))
        {
            items.TryRequestItemDelivery(
                data.supplyItemId,
                wanted,
                facility.centerPos,
                destinationId,
                out _,
                out _);
        }
    }

    private bool TryBeginSupplyCommit(
        DefenseFacility facility,
        DefenseFacilityState state,
        string destinationId,
        string itemId,
        int inputQuantity,
        int supplyUnitsGranted) =>
        DefenseFacilityPhysicalTransactionOutbox.TryCommitOrResume(
            state.pendingSupply,
            DefenseFacilityPhysicalCommitKind.SupplyTransfer,
            state.facilityPersistentId,
            state.nextSupplyOperationSequence,
            destinationId,
            itemId,
            inputQuantity,
            state.supply,
            supplyUnitsGranted,
            physicalItems,
            out _,
            out _);

    private bool TryRecoverMaintenanceCommit(
        DefenseFacility facility,
        DefenseFacilityState state,
        out bool completed)
    {
        completed = false;
        DefenseFacilityPhysicalCommitSaveData pending = state.pendingMaintenance;
        if (pending.phase == DefenseFacilityPhysicalCommitPhase.None)
        {
            return true;
        }
        string destinationId = BuildMaintenanceDestinationId(facility);
        if (!DefenseFacilityPhysicalTransactionOutbox.TryCommitOrResume(
                pending,
                DefenseFacilityPhysicalCommitKind.MaintenanceSink,
                state.facilityPersistentId,
                state.nextMaintenanceOperationSequence,
                destinationId,
                DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId,
                1,
                pending.supplyBefore,
                0,
                physicalItems,
                out _,
                out _))
        {
            return false;
        }
        if (pending.phase == DefenseFacilityPhysicalCommitPhase.IntentRecorded)
        {
            if (state.supply != pending.supplyBefore)
            {
                throw new InvalidOperationException(
                    $"Defense maintenance commit '{pending.operationId}' conflicts with supply state.");
            }
            state.operationalState = DefenseFacilityOperationalState.Preparing;
            state.blockedReason = string.Empty;
            pending.phase = DefenseFacilityPhysicalCommitPhase.OutcomePublished;
            PublishState(state);
        }
        if (!DefenseFacilityPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                pending,
                physicalItems,
                out _))
        {
            return false;
        }
        DefenseFacilityPhysicalTransactionOutbox.Clear(pending);
        state.nextMaintenanceOperationSequence = checked(
            state.nextMaintenanceOperationSequence + 1);
        completed = true;
        return true;
    }

    private bool TryRecoverSupplyCommit(
        DefenseFacility facility,
        DefenseFacilityState state,
        out bool completed)
    {
        completed = false;
        DefenseFacilityPhysicalCommitSaveData pending = state.pendingSupply;
        if (pending.phase == DefenseFacilityPhysicalCommitPhase.None)
        {
            return true;
        }
        string destinationId = BuildSupplyDestinationId(facility);
        if (!DefenseFacilityPhysicalTransactionOutbox.TryCommitOrResume(
                pending,
                DefenseFacilityPhysicalCommitKind.SupplyTransfer,
                state.facilityPersistentId,
                state.nextSupplyOperationSequence,
                destinationId,
                pending.itemId,
                pending.inputQuantity,
                pending.supplyBefore,
                pending.supplyUnitsGranted,
                physicalItems,
                out _,
                out _))
        {
            return false;
        }
        if (pending.phase == DefenseFacilityPhysicalCommitPhase.IntentRecorded)
        {
            if (state.supply != pending.supplyBefore)
            {
                throw new InvalidOperationException(
                    $"Defense supply commit '{pending.operationId}' conflicts with facility supply.");
            }
            state.supply = pending.supplyAfter;
            pending.phase = DefenseFacilityPhysicalCommitPhase.OutcomePublished;
            PublishState(state);
        }
        else if (state.supply != pending.supplyAfter)
        {
            throw new InvalidOperationException(
                $"Defense supply commit '{pending.operationId}' lost its published outcome.");
        }
        if (!DefenseFacilityPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                pending,
                physicalItems,
                out _))
        {
            return false;
        }
        DefenseFacilityPhysicalTransactionOutbox.Clear(pending);
        state.nextSupplyOperationSequence = checked(
            state.nextSupplyOperationSequence + 1);
        completed = true;
        return true;
    }

    private bool HasPendingSupply(string destinationId)
    {
        return items.GetAllStacks().Any(stack => stack != null
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal));
    }

    private void RefreshPassiveState(
        DefenseFacility facility,
        DefenseFacilityState state)
    {
        if (facility == null || facility.isDestroy)
        {
            state.operationalState = DefenseFacilityOperationalState.Destroyed;
            SetBlockedFailure(
                state,
                new DomainFailure(FailureCode.DefenseDestroyed));
            return;
        }

        if (state.operationalState == DefenseFacilityOperationalState.Jammed)
        {
            return;
        }

        if (state.condition < 25f && !state.forcedDangerousOperation)
        {
            state.operationalState = DefenseFacilityOperationalState.Damaged;
            SetBlockedFailure(
                state,
                new DomainFailure(
                    FailureCode.DefenseConditionCritical,
                    state.condition.ToString(
                        "0",
                        CultureInfo.InvariantCulture)));
        }
        else if (facility.Defense.requiresPower && !IsPowered(facility))
        {
            state.operationalState = DefenseFacilityOperationalState.Unpowered;
            SetBlockedFailure(
                state,
                new DomainFailure(FailureCode.DefensePowerUnavailable));
        }
        else if (state.cooldownUntil > clock.Time)
        {
            state.operationalState = DefenseFacilityOperationalState.Cooldown;
        }
        else if (state.armingPolicy == DefenseArmingPolicy.Manual)
        {
            state.operationalState = DefenseFacilityOperationalState.Disarmed;
        }
        else
        {
            state.operationalState = DefenseFacilityOperationalState.Ready;
            state.blockedReason = string.Empty;
        }
    }

    private bool CanTarget(
        DefenseFacilityState state,
        CharacterActor target,
        DefenseTriggerTiming timing)
    {
        if (state.armingPolicy == DefenseArmingPolicy.Manual)
        {
            return timing == DefenseTriggerTiming.GuardResponse;
        }

        if (target == null)
        {
            return true;
        }

        string persistentId = target.Identity?.PersistentId ?? string.Empty;
        if (state.allowedPersistentIds?.Contains(persistentId) == true)
        {
            return false;
        }

        DoorAccessGroup group = ResolveGroup(target);
        if ((((DoorAccessGroup)state.allowedGroups) & group) != 0)
        {
            return false;
        }

        return state.armingPolicy != DefenseArmingPolicy.Safe
            || group == DoorAccessGroup.Intruder;
    }

    private bool IsPowered(DefenseFacility facility)
    {
        return facility?.Defense?.requiresPower != true
            || power.IsPowered(facility);
    }

    private static void SetBlockedFailure(
        DefenseFacilityState state,
        DomainFailure failure)
    {
        state.blockedReason = failure.IsFailure
            ? failure.Code.ToString()
            : string.Empty;
    }

    private DefenseFacilityState GetOrCreate(DefenseFacility facility)
    {
        if (facility == null)
        {
            throw new ArgumentNullException(nameof(facility));
        }

        string key = ResolvePersistentId(facility);
        if (Writable.TryGet(key, out DefenseFacilityState state))
        {
            return state;
        }

        DefenseFacilityData data = facility.Defense;
        int capacity = Mathf.Max(0, data?.supplyCapacity ?? 0);
        state = new DefenseFacilityState
        {
            facilityPersistentId = key,
            buildingId = facility.id,
            gridX = facility.centerPos.x,
            gridY = facility.centerPos.y,
            armingPolicy = DefenseArmingPolicy.Safe,
            condition = 100f,
            supply = data != null && data.UsesPhysicalSupply
                ? Mathf.Clamp(data.initialSupply, 0, capacity)
                : 0,
            operationalState = DefenseFacilityOperationalState.Ready,
            growth = ToGrowthState(data?.growth)
        };
        Writable.Add(state);
        return state;
    }

    private void PublishState(DefenseFacilityState state)
    {
        events.Publish(new DefenseFacilityStateChangedEvent(
            state.facilityPersistentId,
            state.operationalState,
            state.blockedReason));
    }

    private static string BuildSupplyDestinationId(DefenseFacility facility)
    {
        return WorldItemStackRuntime.FacilityInputDestinationPrefix
            + "defense:"
            + ResolvePersistentId(facility);
    }

    private static string BuildMaintenanceDestinationId(
        DefenseFacility facility)
    {
        return WorldItemStackRuntime.FacilityInputDestinationPrefix
            + "defense-maintenance:"
            + ResolvePersistentId(facility);
    }

    private static string ResolvePersistentId(DefenseFacility facility)
    {
        return facility == null
            ? string.Empty
            : facility.RequirePersistentInstanceId().Value;
    }

    private static DoorAccessGroup ResolveGroup(CharacterActor actor)
    {
        if (actor?.IsOwner == true)
        {
            return DoorAccessGroup.Owner;
        }

        return actor?.characterType switch
        {
            CharacterType.Intruder => DoorAccessGroup.Intruder,
            CharacterType.Customer => DoorAccessGroup.Customer,
            _ => DoorAccessGroup.Staff
        };
    }

    private static bool Roll(
        DefenseFacilityState state,
        string channel,
        float chance)
    {
        return DefenseFacilityRules.Roll(
            state.facilityPersistentId,
            state.activationCount,
            channel,
            chance);
    }

    private static DefenseFacilityRecordSaveData ToSaveData(
        DefenseFacilityState source)
    {
        return new DefenseFacilityRecordSaveData
        {
            facilityPersistentId = source.facilityPersistentId ?? string.Empty,
            buildingId = source.buildingId,
            gridX = source.gridX,
            gridY = source.gridY,
            armingPolicy = source.armingPolicy,
            operationalState = source.operationalState,
            condition = source.condition,
            supply = source.supply,
            activationCount = source.activationCount,
            cooldownUntil = source.cooldownUntil,
            forcedDangerousOperation = source.forcedDangerousOperation,
            allowedGroups = source.allowedGroups,
            allowedPersistentIds = new List<string>(
                source.allowedPersistentIds ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            growth = ToGrowthSaveData(source.growth),
            blockedReason = source.blockedReason ?? string.Empty,
            nextMaintenanceOperationSequence =
                source.nextMaintenanceOperationSequence,
            pendingMaintenance = source.pendingMaintenance?.DeepClone()
                ?? new DefenseFacilityPhysicalCommitSaveData(),
            nextSupplyOperationSequence = source.nextSupplyOperationSequence,
            pendingSupply = source.pendingSupply?.DeepClone()
                ?? new DefenseFacilityPhysicalCommitSaveData()
        };
    }

    private static DefenseFacilityState FromSaveData(
        DefenseFacilityRecordSaveData source)
    {
        return new DefenseFacilityState
        {
            facilityPersistentId = source.facilityPersistentId,
            buildingId = source.buildingId,
            gridX = source.gridX,
            gridY = source.gridY,
            armingPolicy = source.armingPolicy,
            operationalState = source.operationalState,
            condition = source.condition,
            supply = source.supply,
            activationCount = source.activationCount,
            cooldownUntil = source.cooldownUntil,
            forcedDangerousOperation = source.forcedDangerousOperation,
            allowedGroups = source.allowedGroups,
            allowedPersistentIds = new List<string>(source.allowedPersistentIds),
            growth = new DefenseFacilityGrowthState
            {
                capacityLevel = source.growth.capacityLevel,
                resetSpeedLevel = source.growth.resetSpeedLevel,
                effectStrengthLevel = source.growth.effectStrengthLevel,
                detectionRangeLevel = source.growth.detectionRangeLevel,
                identificationLevel = source.growth.identificationLevel,
                outageResistanceLevel = source.growth.outageResistanceLevel
            },
            blockedReason = source.blockedReason,
            nextMaintenanceOperationSequence =
                source.nextMaintenanceOperationSequence,
            pendingMaintenance = source.pendingMaintenance.DeepClone(),
            nextSupplyOperationSequence = source.nextSupplyOperationSequence,
            pendingSupply = source.pendingSupply.DeepClone()
        };
    }

    private static DefenseFacilityGrowthState ToGrowthState(
        DefenseFacilityGrowthData source)
    {
        source ??= new DefenseFacilityGrowthData();
        return new DefenseFacilityGrowthState
        {
            capacityLevel = source.capacityLevel,
            resetSpeedLevel = source.resetSpeedLevel,
            effectStrengthLevel = source.effectStrengthLevel,
            detectionRangeLevel = source.detectionRangeLevel,
            identificationLevel = source.identificationLevel,
            outageResistanceLevel = source.outageResistanceLevel
        };
    }

    private static DefenseFacilityGrowthSaveData ToGrowthSaveData(
        DefenseFacilityGrowthState source)
    {
        source ??= new DefenseFacilityGrowthState();
        return new DefenseFacilityGrowthSaveData
        {
            capacityLevel = source.capacityLevel,
            resetSpeedLevel = source.resetSpeedLevel,
            effectStrengthLevel = source.effectStrengthLevel,
            detectionRangeLevel = source.detectionRangeLevel,
            identificationLevel = source.identificationLevel,
            outageResistanceLevel = source.outageResistanceLevel
        };
    }
}
