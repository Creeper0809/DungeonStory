using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IEquipmentOverclockRuntime
{
    IReadOnlyList<OverclockState> States { get; }
    bool TryActivateEquipment(
        string equipmentInstanceId,
        OverclockTier tier,
        out string failureReason);
    bool TryActivateFacility(
        BuildableObject facility,
        OverclockTier tier,
        out string failureReason);
    float GetPerformanceMultiplier(
        OverclockTargetKind targetKind,
        string targetId);
    bool TryRollActionMalfunction(
        OverclockTargetKind targetKind,
        string targetId);
    float GetOverload(OverclockTargetKind targetKind, string targetId);
    EquipmentOverclockSaveData Capture();
}

public interface IFacilityOverclockRuntime
{
    bool TryActivateFacility(
        BuildableObject facility,
        OverclockTier tier,
        out string failureReason);
    float GetFacilityPerformanceMultiplier(string facilityPersistentId);
    float GetFacilityOverload(string facilityPersistentId);
    bool TryRollFacilityActionMalfunction(string facilityPersistentId);
}

public sealed class EquipmentOverclockRuntime :
    IEquipmentOverclockRuntime,
    IFacilityOverclockRuntime,
    ITickable
{
    private const float DurationSeconds = 180f;

    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly IGameMoneyAccount money;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IFacilityEvolutionStateComponentFactory facilityStates;
    private readonly TreasuryEconomyAggregateStateStore stateStore;

    private Dictionary<string, OverclockState> states =>
        stateStore.Current.OverclockStates;

    public EquipmentOverclockRuntime(
        IGameClock clock,
        IRandomStreamProvider randomStreams,
        IGameMoneyAccount money,
        ICombatEquipmentRuntime equipment,
        IWorldItemStackRuntime worldItems,
        IFacilityEvolutionStateComponentFactory facilityStates,
        TreasuryEconomyAggregateStateStore stateStore)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        random = (randomStreams
            ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("economy-overclock");
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.facilityStates = facilityStates
            ?? throw new ArgumentNullException(nameof(facilityStates));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public IReadOnlyList<OverclockState> States => states.Values
        .OrderBy(state => state.targetKind)
        .ThenBy(state => state.targetId, StringComparer.Ordinal)
        .Select(state => state.Clone())
        .ToArray();

    public void Tick()
    {
        float delta = Mathf.Max(0f, clock.DeltaTime);
        if (delta <= 0f)
        {
            return;
        }

        foreach (OverclockState state in states.Values)
        {
            if (!state.Active)
            {
                continue;
            }

            state.remainingGameSeconds = Mathf.Max(
                0f,
                state.remainingGameSeconds - delta);
            if (state.remainingGameSeconds <= 0f)
            {
                state.tier = OverclockTier.None;
            }
        }
    }

    public bool TryActivateEquipment(
        string equipmentInstanceId,
        OverclockTier tier,
        out string failureReason)
    {
        string targetId = NormalizeId(equipmentInstanceId);
        if (!equipment.TryGetInstance(
                targetId,
                out CombatEquipmentInstance instance)
            || !equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "오버클럭할 장비를 찾지 못했습니다.";
            return false;
        }

        int value = ResolveEquipmentValue(instance, definition);
        return TryActivate(
            OverclockTargetKind.Equipment,
            targetId,
            value,
            tier,
            out failureReason);
    }

    public bool TryActivateFacility(
        BuildableObject facility,
        OverclockTier tier,
        out string failureReason)
    {
        if (facility == null || facility.isDestroy)
        {
            failureReason = "오버클럭할 시설을 찾지 못했습니다.";
            return false;
        }

        BuildingOverclockableAbility overclockable =
            facility.BuildingData?
                .GetAbility<BuildingOverclockableAbility>();
        bool treasuryDefense = facility.BuildingData?
            .GetAbility<BuildingTreasuryPoweredDefenseAbility>() != null;
        if (overclockable == null && !treasuryDefense)
        {
            failureReason = "이 시설은 오버클럭을 지원하지 않습니다.";
            return false;
        }

        if (overclockable != null && !overclockable.Allows(tier))
        {
            failureReason = "이 시설은 선택한 오버클럭 단계를 지원하지 않습니다.";
            return false;
        }

        FacilityEvolutionStateComponent state =
            facilityStates.GetOrAdd(facility);
        state.InitializeIfNeeded(facility);
        return TryActivate(
            OverclockTargetKind.Facility,
            state.FacilityPersistentId,
            facility.GetConstructionValue(),
            tier,
            out failureReason);
    }

    public float GetPerformanceMultiplier(
        OverclockTargetKind targetKind,
        string targetId)
    {
        return TryGetActive(targetKind, targetId, out OverclockState state)
            ? state.tier switch
            {
                OverclockTier.Controlled => 1.1f,
                OverclockTier.Aggressive => 1.2f,
                OverclockTier.Critical => 1.35f,
                _ => 1f
            }
            : 1f;
    }

    public float GetFacilityPerformanceMultiplier(string facilityPersistentId)
    {
        return GetPerformanceMultiplier(
            OverclockTargetKind.Facility,
            facilityPersistentId);
    }

    public bool TryRollActionMalfunction(
        OverclockTargetKind targetKind,
        string targetId)
    {
        if (!TryGetActive(targetKind, targetId, out OverclockState state))
        {
            return false;
        }

        float chance = state.tier switch
        {
            OverclockTier.Aggressive => 0.03f,
            OverclockTier.Critical => 0.08f,
            _ => 0f
        };
        if (!random.Chance(chance))
        {
            return false;
        }

        state.overload = Mathf.Clamp(state.overload + 10f, 0f, 100f);
        if (targetKind == OverclockTargetKind.Equipment
            && equipment.TryGetInstance(
                NormalizeId(targetId),
                out CombatEquipmentInstance instance)
            && equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            && definition.Kind is CombatEquipmentKind.Armor
                or CombatEquipmentKind.Shield)
        {
            equipment.TryApplyDurabilityDamage(instance.instanceId, 0.08f);
        }

        return true;
    }

    public float GetOverload(
        OverclockTargetKind targetKind,
        string targetId)
    {
        return states.TryGetValue(
                Key(targetKind, targetId),
                out OverclockState state)
            ? Mathf.Clamp(state.overload, 0f, 100f)
            : 0f;
    }

    public float GetFacilityOverload(string facilityPersistentId)
    {
        return GetOverload(
            OverclockTargetKind.Facility,
            facilityPersistentId);
    }

    public bool TryRollFacilityActionMalfunction(string facilityPersistentId)
    {
        return TryRollActionMalfunction(
            OverclockTargetKind.Facility,
            facilityPersistentId);
    }

    public EquipmentOverclockSaveData Capture()
    {
        return new EquipmentOverclockSaveData
        {
            states = states.Values
                .OrderBy(state => state.targetKind)
                .ThenBy(state => state.targetId, StringComparer.Ordinal)
                .Select(state => state.Clone())
                .ToList()
        };
    }

    internal void PopulateRestoreState(
        TreasuryEconomyAggregateState target,
        EquipmentOverclockSaveData saveData)
    {
        target = target ?? throw new ArgumentNullException(nameof(target));
        target.OverclockStates.Clear();
        foreach (OverclockState source in saveData?.states
                     ?? new List<OverclockState>())
        {
            string targetId = NormalizeId(source?.targetId);
            if (targetId.Length == 0)
            {
                continue;
            }

            OverclockState state = source.Clone();
            state.targetId = targetId;
            state.remainingGameSeconds = Mathf.Clamp(
                state.remainingGameSeconds,
                0f,
                DurationSeconds);
            state.overload = Mathf.Clamp(state.overload, 0f, 100f);
            if (state.remainingGameSeconds <= 0f)
            {
                state.tier = OverclockTier.None;
            }

            target.OverclockStates[Key(state.targetKind, targetId)] = state;
        }
    }

    private bool TryActivate(
        OverclockTargetKind targetKind,
        string targetId,
        int value,
        OverclockTier tier,
        out string failureReason)
    {
        if (tier == OverclockTier.None)
        {
            failureReason = "오버클럭 단계를 선택해야 합니다.";
            return false;
        }

        string key = Key(targetKind, targetId);
        if (states.TryGetValue(key, out OverclockState existing)
            && existing.Active)
        {
            failureReason = "이미 오버클럭 중이며 연장할 수 없습니다.";
            return false;
        }

        float overload = existing?.overload ?? 0f;
        if (overload >= 100f)
        {
            failureReason = "과부하 정비가 필요합니다.";
            return false;
        }

        float costRatio = tier switch
        {
            OverclockTier.Controlled => 0.15f,
            OverclockTier.Aggressive => 0.35f,
            OverclockTier.Critical => 0.7f,
            _ => 0f
        };
        float addedOverload = tier switch
        {
            OverclockTier.Controlled => 10f,
            OverclockTier.Aggressive => 25f,
            OverclockTier.Critical => 50f,
            _ => 0f
        };
        int cost = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, value) * costRatio));
        EconomyTransactionKind transactionKind =
            targetKind == OverclockTargetKind.Equipment
                ? EconomyTransactionKind.EquipmentOverclock
                : EconomyTransactionKind.FacilityOverclock;
        if (!money.TrySpend(
                cost,
                new EconomyTransactionContext(
                    transactionKind,
                    "overclock",
                    targetId,
                    $"{tier} 24시간 오버클럭"),
                out failureReason))
        {
            return false;
        }

        states[key] = new OverclockState
        {
            targetKind = targetKind,
            targetId = targetId,
            tier = tier,
            remainingGameSeconds = DurationSeconds,
            overload = Mathf.Clamp(overload + addedOverload, 0f, 100f),
            activationCost = cost
        };
        failureReason = string.Empty;
        return true;
    }

    private bool TryGetActive(
        OverclockTargetKind targetKind,
        string targetId,
        out OverclockState state)
    {
        return states.TryGetValue(Key(targetKind, targetId), out state)
            && state.Active;
    }

    private int ResolveEquipmentValue(
        CombatEquipmentInstance instance,
        CombatEquipmentDefinitionSO definition)
    {
        DungeonItemDefinition itemDefinition =
            worldItems.CatalogProvider.GetDefinition(definition.ItemId);
        float materialValue = equipment.TryGetDerivedStats(
                instance.instanceId,
                out CombatEquipmentDerivedStats stats)
            ? stats.ValueMultiplier
            : 1f;
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                Mathf.Max(1, itemDefinition.UnitPrice)
                * CombatQualityRules.GetMultiplier(instance.quality)
                * materialValue));
    }

    private static string Key(
        OverclockTargetKind targetKind,
        string targetId)
    {
        return $"{(int)targetKind}:{NormalizeId(targetId)}";
    }

    private static string NormalizeId(string value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
