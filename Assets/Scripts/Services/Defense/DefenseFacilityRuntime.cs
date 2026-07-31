using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum DefenseArmingPolicy
{
    Manual = 0,
    Safe = 1,
    Alert = 2,
    Aggressive = 3
}

public enum DefenseFacilityOperationalState
{
    Disarmed = 0,
    Preparing = 1,
    Ready = 2,
    Detecting = 3,
    Triggered = 4,
    Cooldown = 5,
    Reloading = 6,
    Empty = 7,
    Unpowered = 8,
    Faulted = 9,
    Jammed = 10,
    Damaged = 11,
    Destroyed = 12
}

[Serializable]
public sealed class DefenseFacilityInstanceState
{
    public string facilityPersistentId = string.Empty;
    public int buildingId;
    public int gridX;
    public int gridY;
    public DefenseArmingPolicy armingPolicy = DefenseArmingPolicy.Safe;
    public DefenseFacilityOperationalState operationalState =
        DefenseFacilityOperationalState.Ready;
    [Range(0f, 100f)] public float condition = 100f;
    public int supply;
    public int activationCount;
    public float cooldownUntil;
    public bool forcedDangerousOperation;
    public int allowedGroups = (int)(
        DoorAccessGroup.Owner
        | DoorAccessGroup.Staff
        | DoorAccessGroup.Customer
        | DoorAccessGroup.Captive
        | DoorAccessGroup.CaptiveWildlife);
    public List<string> allowedPersistentIds = new List<string>();
    public DefenseFacilityGrowthData growth = new DefenseFacilityGrowthData();
    public string blockedReason = string.Empty;
}

[Serializable]
public sealed class DefenseFacilitySaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<DefenseFacilityInstanceState> facilities =
        new List<DefenseFacilityInstanceState>();
}

public readonly struct DefenseFacilitySnapshot
{
    public DefenseFacilitySnapshot(
        DefenseFacilityInstanceState state,
        float cooldownRemaining,
        bool powered,
        string destinationId)
    {
        PersistentId = state?.facilityPersistentId ?? string.Empty;
        ArmingPolicy = state?.armingPolicy ?? DefenseArmingPolicy.Safe;
        OperationalState = state?.operationalState
            ?? DefenseFacilityOperationalState.Ready;
        Condition = state?.condition ?? 100f;
        Supply = state?.supply ?? 0;
        ActivationCount = state?.activationCount ?? 0;
        CooldownRemaining = Mathf.Max(0f, cooldownRemaining);
        Powered = powered;
        BlockedReason = state?.blockedReason ?? string.Empty;
        SupplyDestinationId = destinationId ?? string.Empty;
    }

    public string PersistentId { get; }
    public DefenseArmingPolicy ArmingPolicy { get; }
    public DefenseFacilityOperationalState OperationalState { get; }
    public float Condition { get; }
    public int Supply { get; }
    public int ActivationCount { get; }
    public float CooldownRemaining { get; }
    public bool Powered { get; }
    public string BlockedReason { get; }
    public string SupplyDestinationId { get; }
}

public readonly struct DefenseActivationAuthorization
{
    public DefenseActivationAuthorization(
        bool allowed,
        bool jammed,
        bool misfired,
        float effectMultiplier)
    {
        Allowed = allowed;
        Jammed = jammed;
        Misfired = misfired;
        EffectMultiplier = Mathf.Max(0f, effectMultiplier);
    }

    public bool Allowed { get; }
    public bool Jammed { get; }
    public bool Misfired { get; }
    public float EffectMultiplier { get; }

    public static DefenseActivationAuthorization Granted =>
        new DefenseActivationAuthorization(true, false, false, 1f);
}

public readonly struct DefenseFacilityStateChangedEvent
{
    public DefenseFacilityStateChangedEvent(
        string facilityPersistentId,
        DefenseFacilityOperationalState state,
        string reason)
    {
        FacilityPersistentId = facilityPersistentId ?? string.Empty;
        State = state;
        Reason = reason ?? string.Empty;
    }

    public string FacilityPersistentId { get; }
    public DefenseFacilityOperationalState State { get; }
    public string Reason { get; }
}

public interface IDefenseFacilityRuntime
{
    DefenseFacilitySnapshot GetSnapshot(DefenseFacility facility);
    bool CanActivate(
        DefenseFacility facility,
        CharacterActor target,
        DefenseTriggerTiming timing,
        out string failureReason);
    bool TryBeginActivation(
        DefenseFacility facility,
        CharacterActor target,
        DefenseTriggerTiming timing,
        out DefenseActivationAuthorization authorization,
        out string failureReason);
    void CompleteActivation(
        DefenseFacility facility,
        DefenseActivationAuthorization authorization);
    bool SetArmingPolicy(
        DefenseFacility facility,
        DefenseArmingPolicy policy,
        out string warning);
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
        out string failureReason);
    bool TryClearJam(DefenseFacility facility, out string failureReason);
    bool TryRepair(
        DefenseFacility facility,
        float condition,
        out string failureReason);
    DefenseFacilitySaveData Capture();
    void Restore(DefenseFacilitySaveData data);
}

public sealed class DefenseFacilityRuntime : IDefenseFacilityRuntime
{
    private const string MaintenancePartItemId = "material:iron-ingot";

    private readonly IWorldItemStackRuntime items;
    private readonly IElectricalNetworkRuntime power;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private readonly Dictionary<string, DefenseFacilityInstanceState> states =
        new Dictionary<string, DefenseFacilityInstanceState>(
            StringComparer.Ordinal);

    public DefenseFacilityRuntime(
        IWorldItemStackRuntime items,
        IGameClock clock,
        IGameEventBus events,
        IElectricalNetworkRuntime power = null)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.power = power;
    }

    public DefenseFacilitySnapshot GetSnapshot(DefenseFacility facility)
    {
        DefenseFacilityInstanceState state = GetOrCreate(facility);
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
        out string failureReason)
    {
        failureReason = string.Empty;
        if (facility == null || facility.Defense == null)
        {
            failureReason = "방어시설이 아닙니다.";
            return false;
        }

        DefenseFacilityInstanceState state = GetOrCreate(facility);
        RefreshPassiveState(facility, state);
        if (!CanTarget(state, target, timing))
        {
            failureReason = state.armingPolicy == DefenseArmingPolicy.Manual
                ? "수동 무장 정책입니다."
                : "허용된 대상입니다.";
            state.blockedReason = failureReason;
            return false;
        }

        if (state.condition < 25f && !state.forcedDangerousOperation)
        {
            state.operationalState = DefenseFacilityOperationalState.Damaged;
            state.blockedReason = "건전도 25 미만: 자동 비활성";
            failureReason = state.blockedReason;
            return false;
        }

        if (facility.Defense.requiresPower && !IsPowered(facility))
        {
            state.operationalState =
                DefenseFacilityOperationalState.Unpowered;
            state.blockedReason = "전력 공급 없음";
            failureReason = state.blockedReason;
            PublishState(state);
            return false;
        }

        if (state.cooldownUntil > clock.Time)
        {
            state.operationalState = DefenseFacilityOperationalState.Cooldown;
            state.blockedReason =
                $"재사용 대기 {state.cooldownUntil - clock.Time:0.0}초";
            failureReason = state.blockedReason;
            return false;
        }

        if (!EnsureSupply(facility, state, out failureReason))
        {
            state.blockedReason = failureReason;
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
        out string failureReason)
    {
        authorization = default;
        if (!CanActivate(facility, target, timing, out failureReason))
        {
            return false;
        }

        DefenseFacilityInstanceState state = GetOrCreate(facility);
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
        state.blockedReason = jammed
            ? "기계 걸림: 정비 작업 필요"
            : misfired
                ? "부분 오작동"
                : string.Empty;
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

        DefenseFacilityInstanceState state = GetOrCreate(facility);
        if (authorization.Jammed)
        {
            return;
        }

        float resetBonus = 1f + Mathf.Max(0, state.growth.resetSpeedLevel) * 0.1f;
        state.cooldownUntil = clock.Time
            + Mathf.Max(0f, facility.Defense.cooldownSeconds) / resetBonus;
        state.operationalState = state.cooldownUntil > clock.Time
            ? DefenseFacilityOperationalState.Cooldown
            : DefenseFacilityOperationalState.Ready;
        PublishState(state);
    }

    public bool SetArmingPolicy(
        DefenseFacility facility,
        DefenseArmingPolicy policy,
        out string warning)
    {
        warning = policy switch
        {
            DefenseArmingPolicy.Alert =>
                "허가받지 않은 대상이 방어 구역에 들어오면 발동합니다.",
            DefenseArmingPolicy.Aggressive =>
                "명시적 허용 목록 외 모든 대상을 적으로 간주합니다.",
            DefenseArmingPolicy.Manual =>
                "플레이어 또는 경비 대응 명령으로만 발동합니다.",
            _ => string.Empty
        };
        DefenseFacilityInstanceState state = GetOrCreate(facility);
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

        DefenseFacilityInstanceState state = GetOrCreate(facility);
        DoorAccessGroup groups = (DoorAccessGroup)state.allowedGroups;
        groups = allowed ? groups | group : groups & ~group;
        state.allowedGroups = (int)groups;
        return true;
    }

    public bool TryRequestReload(
        DefenseFacility facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (facility?.Defense?.UsesPhysicalSupply != true)
        {
            failureReason = "실물 재장전이 필요한 방어시설이 아닙니다.";
            return false;
        }

        DefenseFacilityInstanceState state = GetOrCreate(facility);
        int capacity = Mathf.Max(
            1,
            facility.Defense.supplyCapacity
                + state.growth.capacityLevel);
        if (state.supply >= capacity)
        {
            failureReason = "보급이 이미 가득 찼습니다.";
            return false;
        }

        RequestMissingSupply(facility, state, capacity);
        string destinationId = BuildSupplyDestinationId(facility);
        bool requested = HasPendingSupply(destinationId);
        state.operationalState = requested
            ? DefenseFacilityOperationalState.Reloading
            : DefenseFacilityOperationalState.Empty;
        state.blockedReason = requested
            ? "보급 운반·재장전 대기"
            : "사용 가능한 보급 재고 없음";
        PublishState(state);
        failureReason = state.blockedReason;
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

        DefenseFacilityInstanceState state = GetOrCreate(facility);
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
        out string failureReason)
    {
        failureReason = string.Empty;
        DefenseFacilityInstanceState state = GetOrCreate(facility);
        if (state.operationalState != DefenseFacilityOperationalState.Jammed)
        {
            failureReason = "걸림 상태가 아닙니다.";
            return false;
        }

        string destinationId = BuildMaintenanceDestinationId(facility);
        bool consumed = items.TryConsumeFacilityItemBuffer(
            destinationId,
            new Dictionary<string, int>
            {
                [MaintenancePartItemId] = 1
            },
            out _);
        if (!consumed)
        {
            bool pending = HasPendingSupply(destinationId);
            if (!pending)
            {
                items.TryRequestItemDelivery(
                    MaintenancePartItemId,
                    1,
                    facility.centerPos,
                    destinationId,
                    out _,
                    out _);
                pending = HasPendingSupply(destinationId);
            }

            state.blockedReason = pending
                ? "정비 부품 운반 대기"
                : "정비용 철괴 1개 부족";
            PublishState(state);
            failureReason = state.blockedReason;
            return false;
        }

        state.operationalState = DefenseFacilityOperationalState.Preparing;
        state.blockedReason = string.Empty;
        PublishState(state);
        return true;
    }

    public bool TryRepair(
        DefenseFacility facility,
        float condition,
        out string failureReason)
    {
        failureReason = string.Empty;
        DefenseFacilityInstanceState state = GetOrCreate(facility);
        if (condition <= 0f)
        {
            failureReason = "복구할 건전도가 없습니다.";
            return false;
        }

        state.condition = Mathf.Clamp(state.condition + condition, 0f, 100f);
        state.forcedDangerousOperation = false;
        RefreshPassiveState(facility, state);
        PublishState(state);
        return true;
    }

    public DefenseFacilitySaveData Capture()
    {
        return new DefenseFacilitySaveData
        {
            facilities = states.Values
                .OrderBy(value => value.facilityPersistentId, StringComparer.Ordinal)
                .Select(Clone)
                .ToList()
        };
    }

    public void Restore(DefenseFacilitySaveData data)
    {
        states.Clear();
        foreach (DefenseFacilityInstanceState source in data?.facilities
                     ?? Enumerable.Empty<DefenseFacilityInstanceState>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.facilityPersistentId))
            {
                continue;
            }

            DefenseFacilityInstanceState clone = Clone(source);
            clone.condition = Mathf.Clamp(clone.condition, 0f, 100f);
            states[clone.facilityPersistentId] = clone;
        }
    }

    private bool EnsureSupply(
        DefenseFacility facility,
        DefenseFacilityInstanceState state,
        out string failureReason)
    {
        failureReason = string.Empty;
        DefenseFacilityData data = facility.Defense;
        if (!data.UsesPhysicalSupply)
        {
            return true;
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
            IReadOnlyDictionary<string, int> itemCost =
                string.IsNullOrWhiteSpace(data.supplyItemId)
                    ? null
                    : new Dictionary<string, int>
                    {
                        [data.supplyItemId.Trim()] = wanted
                    };
            IReadOnlyDictionary<StockCategory, int> categoryCost =
                itemCost == null
                    ? new Dictionary<StockCategory, int>
                    {
                        [data.supplyCategory] = wanted
                    }
                    : null;
            bool consumed = itemCost != null
                ? items.TryConsumeFacilityItemBuffer(
                    destinationId,
                    itemCost,
                    out _)
                : items.TryConsumeFacilityBuffer(
                    destinationId,
                    categoryCost,
                    out _);
            if (consumed)
            {
                state.supply += wanted;
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
        failureReason = state.operationalState
            == DefenseFacilityOperationalState.Reloading
                ? "보급품 운반·재장전 대기"
                : "보급품 없음";
        PublishState(state);
        return false;
    }

    private void RequestMissingSupply(
        DefenseFacility facility,
        DefenseFacilityInstanceState state,
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
        else
        {
            items.TryRequestFacilityDelivery(
                data.supplyCategory,
                wanted,
                facility.centerPos,
                destinationId,
                out _,
                out _);
        }
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
        DefenseFacilityInstanceState state)
    {
        if (facility == null || facility.isDestroy)
        {
            state.operationalState = DefenseFacilityOperationalState.Destroyed;
            state.blockedReason = "시설 파괴";
            return;
        }

        if (state.operationalState == DefenseFacilityOperationalState.Jammed)
        {
            return;
        }

        if (state.condition < 25f && !state.forcedDangerousOperation)
        {
            state.operationalState = DefenseFacilityOperationalState.Damaged;
            state.blockedReason = "건전도 25 미만: 자동 비활성";
        }
        else if (facility.Defense.requiresPower && !IsPowered(facility))
        {
            state.operationalState = DefenseFacilityOperationalState.Unpowered;
            state.blockedReason = "전력 공급 없음";
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
        DefenseFacilityInstanceState state,
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
            || power?.IsPowered(facility) == true;
    }

    private DefenseFacilityInstanceState GetOrCreate(DefenseFacility facility)
    {
        if (facility == null)
        {
            return new DefenseFacilityInstanceState();
        }

        string key = ResolvePersistentId(facility);
        if (states.TryGetValue(key, out DefenseFacilityInstanceState state))
        {
            return state;
        }

        DefenseFacilityData data = facility.Defense;
        int capacity = Mathf.Max(0, data?.supplyCapacity ?? 0);
        state = new DefenseFacilityInstanceState
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
            growth = data?.growth ?? new DefenseFacilityGrowthData()
        };
        states.Add(key, state);
        return state;
    }

    private void PublishState(DefenseFacilityInstanceState state)
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
        string persistentId = facility?
            .GetComponent<FacilityEvolutionStateComponent>()?
            .FacilityPersistentId?
            .Trim() ?? string.Empty;
        if (persistentId.Length > 0)
        {
            return persistentId;
        }

        return facility == null
            ? string.Empty
            : $"defense:{facility.id}:{facility.centerPos.x}:{facility.centerPos.y}";
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
        DefenseFacilityInstanceState state,
        string channel,
        float chance)
    {
        if (chance <= 0f)
        {
            return false;
        }

        int hash = CharacterGrowthRules.StableHash(
            state.facilityPersistentId
            + "|"
            + channel
            + "|"
            + state.activationCount);
        float sample = (hash & 0x7fffffff) / (float)int.MaxValue;
        return sample < chance;
    }

    private static DefenseFacilityInstanceState Clone(
        DefenseFacilityInstanceState source)
    {
        return new DefenseFacilityInstanceState
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
                source.allowedPersistentIds ?? new List<string>()),
            growth = source.growth ?? new DefenseFacilityGrowthData(),
            blockedReason = source.blockedReason ?? string.Empty
        };
    }
}
