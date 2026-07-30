using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class AutomationFacilityState
{
    public AutomationMode Mode = AutomationMode.Manual;
    public float Maintenance = 100f;
    public float Fault;
    public string BlockedReason = string.Empty;
}

internal sealed class AutomationRuntime : IAutomationRuntime, ITickable
{
    private const float TickInterval = 0.25f;
    private const float SecondsPerGameHour = 7.5f;

    private readonly IBuildingWorldQuery buildings;
    private readonly IElectricalNetworkRuntime power;
    private readonly IProductionBillRuntime production;
    private readonly IGameClock clock;
    private readonly AutomationPowerDemandRegistry powerDemand;
    private readonly Dictionary<string, AutomationFacilityState> states =
        new Dictionary<string, AutomationFacilityState>(
            StringComparer.Ordinal);
    private IReadOnlyList<AutomationFacilitySnapshot> facilities =
        Array.Empty<AutomationFacilitySnapshot>();
    private readonly List<BuildableObject> automationFacilities =
        new List<BuildableObject>();
    private int buildingVersion = int.MinValue;
    private float accumulated;

    public AutomationRuntime(
        IBuildingWorldQuery buildings,
        IElectricalNetworkRuntime power,
        IProductionBillRuntime production,
        IGameClock clock,
        AutomationPowerDemandRegistry powerDemand)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.power = power
            ?? throw new ArgumentNullException(nameof(power));
        this.production = production
            ?? throw new ArgumentNullException(nameof(production));
        this.clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
        this.powerDemand = powerDemand
            ?? throw new ArgumentNullException(nameof(powerDemand));
    }

    public int Version { get; private set; }

    public IReadOnlyList<AutomationFacilitySnapshot> Facilities
    {
        get
        {
            EnsureFacilities();
            RefreshSnapshots();
            return facilities;
        }
    }

    public void Tick()
    {
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        EnsureFacilities();
        accumulated += clock.DeltaTime;
        if (accumulated < TickInterval)
        {
            return;
        }

        float deltaTime = accumulated;
        accumulated = 0f;
        for (int i = 0; i < automationFacilities.Count; i++)
        {
            TickFacility(automationFacilities[i], deltaTime);
        }

        Touch();
    }

    public bool TryGetFacility(
        BuildableObject facility,
        out AutomationFacilitySnapshot snapshot)
    {
        snapshot = null;
        if (!TryResolve(
                facility,
                out string facilityId,
                out BuildingAutomationAbility ability))
        {
            return false;
        }

        snapshot = CreateSnapshot(
            facility,
            facilityId,
            ability,
            EnsureState(facilityId));
        return true;
    }

    public InfrastructureCommandResult SetMode(
        BuildableObject facility,
        AutomationMode mode)
    {
        if (!Enum.IsDefined(typeof(AutomationMode), mode)
            || !TryResolve(
                facility,
                out string facilityId,
                out BuildingAutomationAbility ability))
        {
            return InfrastructureCommandResult.Failure(
                "자동화 가능한 생산 시설을 선택해야 합니다.");
        }

        if ((int)mode > (int)ability.maximumMode)
        {
            return InfrastructureCommandResult.Failure(
                "이 시설은 선택한 자동화 단계를 지원하지 않습니다.");
        }

        EnsureState(facilityId).Mode = mode;
        powerDemand.SetMode(facilityId, mode);
        Touch();
        return InfrastructureCommandResult.Success(
            mode switch
            {
                AutomationMode.Manual => "수동 생산으로 전환했습니다.",
                AutomationMode.PoweredAssist => "전동 보조를 가동했습니다.",
                _ => "무인 자동 생산을 가동했습니다."
            });
    }

    public InfrastructureCommandResult Maintain(
        BuildableObject facility,
        float amount)
    {
        if (!TryResolve(
                facility,
                out string facilityId,
                out _))
        {
            return InfrastructureCommandResult.Failure(
                "자동화 가능한 생산 시설을 선택해야 합니다.");
        }

        AutomationFacilityState state = EnsureState(facilityId);
        float applied = Mathf.Max(0f, amount);
        if (applied <= 0f)
        {
            return InfrastructureCommandResult.Failure(
                "정비 작업량이 필요합니다.");
        }

        state.Maintenance = Mathf.Clamp(
            state.Maintenance + applied,
            0f,
            100f);
        state.Fault = Mathf.Max(0f, state.Fault - applied * 0.5f);
        state.BlockedReason = string.Empty;
        Touch();
        return InfrastructureCommandResult.Success(
            "자동화 설비를 정비했습니다.");
    }

    public float GetWorkSpeedMultiplier(BuildableObject facility)
    {
        if (!TryResolve(
                facility,
                out string facilityId,
                out BuildingAutomationAbility ability))
        {
            return 1f;
        }

        AutomationFacilityState state = EnsureState(facilityId);
        return state.Mode == AutomationMode.PoweredAssist
            && power.IsPowered(facility)
            && state.Fault < 100f
                ? Mathf.Max(0.01f, ability.assistedWorkMultiplier)
                    * ResolveConditionMultiplier(state)
                : 1f;
    }

    public DungeonAutomationSaveData Capture()
    {
        EnsureFacilities();
        return new DungeonAutomationSaveData
        {
            facilities = states
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new AutomationFacilitySaveData
                {
                    facilityId = pair.Key,
                    mode = pair.Value.Mode,
                    maintenance = pair.Value.Maintenance,
                    fault = pair.Value.Fault
                })
                .ToList()
        };
    }

    public void Restore(DungeonAutomationSaveData snapshot)
    {
        states.Clear();
        powerDemand.Clear();
        foreach (AutomationFacilitySaveData saved in snapshot?.facilities
                 ?? new List<AutomationFacilitySaveData>())
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.facilityId))
            {
                continue;
            }

            states[saved.facilityId.Trim()] =
                new AutomationFacilityState
                {
                    Mode = Enum.IsDefined(
                        typeof(AutomationMode),
                        saved.mode)
                            ? saved.mode
                            : AutomationMode.Manual,
                    Maintenance = Mathf.Clamp(
                        saved.maintenance,
                        0f,
                        100f),
                    Fault = Mathf.Clamp(saved.fault, 0f, 100f)
                };
            powerDemand.SetMode(
                saved.facilityId.Trim(),
                states[saved.facilityId.Trim()].Mode);
        }

        buildingVersion = int.MinValue;
        EnsureFacilities();
        RefreshSnapshots();
        Touch();
    }

    private void TickFacility(BuildableObject facility, float deltaTime)
    {
        string facilityId =
            IndustrialInfrastructureIdentity.GetNodeId(facility);
        AutomationFacilityState state = EnsureState(facilityId);
        BuildingAutomationAbility ability =
            facility.BuildingData.GetAbility<BuildingAutomationAbility>();
        if (state.Mode == AutomationMode.Manual)
        {
            state.BlockedReason = string.Empty;
            return;
        }

        if (!power.IsPowered(facility))
        {
            state.BlockedReason = "전력 부족";
            return;
        }

        float maintenanceDrain = Mathf.Max(
                0f,
                ability.maintenancePerGameHour)
            * deltaTime
            / SecondsPerGameHour;
        state.Maintenance = Mathf.Max(
            0f,
            state.Maintenance - maintenanceDrain);
        if (state.Maintenance <= 25f)
        {
            state.Fault = Mathf.Clamp(
                state.Fault
                + (25f - state.Maintenance) * 0.006f * deltaTime,
                0f,
                100f);
        }

        if (state.Fault >= 100f)
        {
            state.BlockedReason = "고장 수리 필요";
            return;
        }

        if (state.Mode != AutomationMode.Automatic)
        {
            state.BlockedReason = string.Empty;
            return;
        }

        IReadOnlyList<ProductionBillSnapshot> bills =
            production.GetBills(facility);
        ProductionBillSnapshot bill = bills.FirstOrDefault(candidate =>
            string.IsNullOrWhiteSpace(candidate.ReservedWorkerId)
            && candidate.Status is ProductionBillStatus.Ready
                or ProductionBillStatus.InProgress);
        if (bill == null)
        {
            state.BlockedReason = bills.Count > 0
                ? "재료 또는 출력 공간 대기"
                : "생산 주문 없음";
            return;
        }

        if (!bill.MaterialsConsumed
            && !production.TryBeginWork(
                null,
                facility,
                bill.WorkTypeId,
                out bill,
                out string beginFailure))
        {
            state.BlockedReason = string.IsNullOrWhiteSpace(beginFailure)
                ? "재료 대기"
                : beginFailure;
            return;
        }

        float work = Mathf.Max(
                0.01f,
                ability.automaticWorkPerSecond)
            * ResolveConditionMultiplier(state)
            * deltaTime;
        if (!production.ApplyWork(
                null,
                facility,
                bill.BillId,
                work,
                out _,
                out string message))
        {
            state.BlockedReason = string.IsNullOrWhiteSpace(message)
                ? "생산 진행 불가"
                : message;
            return;
        }

        state.BlockedReason = string.Empty;
    }

    private void EnsureFacilities()
    {
        if (buildingVersion == buildings.BuildingVersion)
        {
            return;
        }

        buildingVersion = buildings.BuildingVersion;
        automationFacilities.Clear();
        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building != null
                && !building.IsGridDestroyed
                && building.BuildingData?.GetAbility<
                    BuildingAutomationAbility>() != null)
            {
                automationFacilities.Add(building);
            }
        }

        automationFacilities.Sort((left, right) =>
            string.CompareOrdinal(
                IndustrialInfrastructureIdentity.GetNodeId(left),
                IndustrialInfrastructureIdentity.GetNodeId(right)));
        for (int i = 0; i < automationFacilities.Count; i++)
        {
            EnsureState(IndustrialInfrastructureIdentity.GetNodeId(
                automationFacilities[i]));
        }

        Touch();
    }

    private IReadOnlyList<BuildableObject> GetAutomationFacilities()
    {
        EnsureFacilities();
        return automationFacilities;
    }

    private void RefreshSnapshots()
    {
        facilities = GetAutomationFacilities()
            .Select(facility =>
            {
                string facilityId =
                    IndustrialInfrastructureIdentity.GetNodeId(facility);
                return CreateSnapshot(
                    facility,
                    facilityId,
                    facility.BuildingData
                        .GetAbility<BuildingAutomationAbility>(),
                    EnsureState(facilityId));
            })
            .ToArray();
    }

    private AutomationFacilitySnapshot CreateSnapshot(
        BuildableObject facility,
        string facilityId,
        BuildingAutomationAbility ability,
        AutomationFacilityState state)
    {
        bool powered = power.IsPowered(facility);
        float workRate = state.Mode switch
        {
            AutomationMode.PoweredAssist =>
                ability.assistedWorkMultiplier,
            AutomationMode.Automatic =>
                ability.automaticWorkPerSecond,
            _ => 0f
        };
        return new AutomationFacilitySnapshot
        {
            FacilityId = facilityId,
            Mode = state.Mode,
            Powered = powered,
            Operational = state.Mode == AutomationMode.Manual
                || powered && state.Fault < 100f,
            WorkRate = workRate * ResolveConditionMultiplier(state),
            Maintenance = state.Maintenance,
            Fault = state.Fault,
            BlockedReason = state.BlockedReason
        };
    }

    private bool TryResolve(
        BuildableObject facility,
        out string facilityId,
        out BuildingAutomationAbility ability)
    {
        ability = facility?.BuildingData
            ?.GetAbility<BuildingAutomationAbility>();
        if (facility == null
            || facility.IsGridDestroyed
            || ability == null)
        {
            facilityId = string.Empty;
            return false;
        }

        facilityId =
            IndustrialInfrastructureIdentity.GetNodeId(facility);
        return !string.IsNullOrWhiteSpace(facilityId);
    }

    private AutomationFacilityState EnsureState(string facilityId)
    {
        if (!states.TryGetValue(
                facilityId,
                out AutomationFacilityState state))
        {
            state = new AutomationFacilityState();
            states[facilityId] = state;
        }

        powerDemand.SetMode(facilityId, state.Mode);
        return state;
    }

    private static float ResolveConditionMultiplier(
        AutomationFacilityState state)
    {
        float maintenance = state.Maintenance >= 60f
            ? 1f
            : Mathf.Lerp(0.45f, 1f, state.Maintenance / 60f);
        float fault = Mathf.Lerp(1f, 0.35f, state.Fault / 100f);
        return Mathf.Clamp(maintenance * fault, 0.1f, 1f);
    }

    private void Touch()
    {
        unchecked
        {
            Version++;
        }
    }
}
