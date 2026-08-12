using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public static class AutomationLaborAccountingRules
{
    public static float CalculateAcceptedWork(
        ProductionBillSnapshot before,
        ProductionBillSnapshot after,
        ProductionWorkExecutionResult execution)
    {
        if (before == null || !execution.Succeeded)
        {
            return 0f;
        }

        float remainingBefore = Mathf.Max(
            0f,
            before.RequiredWork - before.CompletedWork);
        if (execution.CycleCompleted
            || execution.Outcome == ProductionBillOutcomeCode.ProcessingStarted)
        {
            return remainingBefore;
        }

        return after == null
            ? 0f
            : Mathf.Clamp(
                after.CompletedWork - before.CompletedWork,
                0f,
                remainingBefore);
    }

    public static float CalculateNetAutomaticWork(
        ProductionBillSnapshot before,
        ProductionBillSnapshot after,
        ProductionWorkExecutionResult execution,
        float maintenanceBurdenWu)
    {
        if (float.IsNaN(maintenanceBurdenWu)
            || float.IsInfinity(maintenanceBurdenWu)
            || maintenanceBurdenWu < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maintenanceBurdenWu));
        }

        return Mathf.Max(
            0f,
            CalculateAcceptedWork(before, after, execution)
                - maintenanceBurdenWu);
    }
}

internal sealed class AutomationRuntime :
    IAutomationInfrastructureQuery,
    IAutomationInfrastructureCommand,
    IAutomationInfrastructurePersistence,
    ITickable
{
    private const float TickInterval = 0.25f;
    private const float SecondsPerGameHour = 7.5f;

    private readonly IBuildingWorldQuery buildings;
    private readonly IPowerInfrastructureQuery power;
    private readonly IProductionBillQuery productionQuery;
    private readonly IProductionBillWorkExecution productionWork;
    private readonly IGameClock clock;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly ISettlementLaborAccountingService laborAccounting;
    private readonly AutomationStateSession stateSession;
    private IReadOnlyList<AutomationFacilitySnapshot> facilities =
        Array.Empty<AutomationFacilitySnapshot>();
    private readonly List<BuildableObject> automationFacilities =
        new List<BuildableObject>();
    private int buildingVersion = int.MinValue;
    private int projectedRestoreRevision;
    private float accumulated;

    public AutomationRuntime(
        IBuildingWorldQuery buildings,
        IPowerInfrastructureQuery power,
        IProductionBillQuery productionQuery,
        IProductionBillWorkExecution productionWork,
        IGameClock clock,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ISettlementLaborAccountingService laborAccounting,
        IMilestoneGameplayModifierQuery milestoneModifiers = null)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.power = power
            ?? throw new ArgumentNullException(nameof(power));
        this.productionQuery = productionQuery
            ?? throw new ArgumentNullException(nameof(productionQuery));
        this.productionWork = productionWork
            ?? throw new ArgumentNullException(nameof(productionWork));
        this.clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.milestoneModifiers = milestoneModifiers
            ?? NeutralMilestoneGameplayModifierQuery.Instance;
        this.laborAccounting = laborAccounting
            ?? throw new ArgumentNullException(nameof(laborAccounting));
        stateSession = new AutomationStateSession(this.aggregateRootStore);
        projectedRestoreRevision =
            this.aggregateRootStore.PublishedRestoreRevision;
    }

    public int Version => stateSession.Version;

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
            return InfrastructureCommandResult.Failed(
                FailureCode.AutomationFacilityUnavailable);
        }

        if ((int)mode > (int)ability.maximumMode)
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.AutomationModeUnsupported,
                mode.ToString());
        }

        EnsureState(facilityId).SetMode(mode);
        Touch();
        return InfrastructureCommandResult.Success();
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
            return InfrastructureCommandResult.Failed(
                FailureCode.AutomationFacilityUnavailable);
        }

        AutomationFacilityStateSession state = EnsureState(facilityId);
        float applied = Mathf.Max(0f, amount);
        if (applied <= 0f)
        {
            return InfrastructureCommandResult.Failed(
                FailureCode.AutomationMaintenanceRequired);
        }

        state.ApplyMaintenance(applied);
        Touch();
        return InfrastructureCommandResult.Success();
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

        AutomationFacilityStateSession state = EnsureState(facilityId);
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
        return stateSession.Capture();
    }

    public AutomationRestoreCandidate PrepareRestore(
        DungeonAutomationSaveData snapshot)
    {
        IndustrialInfrastructureSaveValidation.RequireValid(snapshot);
        return AutomationStateSession.CreateRestoreCandidate(
            snapshot?.facilities);
    }

    public void Restore(AutomationRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        stateSession.Restore(candidate);
        if (!aggregateRootStore.IsRestoreStaging)
        {
            ResetProjectionAfterRestore();
            EnsureFacilities();
            RefreshSnapshots();
        }
    }

    private void TickFacility(BuildableObject facility, float deltaTime)
    {
        string facilityId =
            IndustrialInfrastructureIdentity.GetNodeId(facility);
        AutomationFacilityStateSession state = EnsureState(facilityId);
        BuildingAutomationAbility ability =
            facility.BuildingData.GetAbility<BuildingAutomationAbility>();
        if (state.Mode == AutomationMode.Manual)
        {
            state.SetStatus(InfrastructureStatus.None);
            return;
        }

        if (!power.IsPowered(facility))
        {
            state.SetStatus(new InfrastructureStatus(
                InfrastructureStatusCode.PowerUnavailable));
            return;
        }

        float maintenanceDrain = Mathf.Max(
                0f,
                ability.maintenancePerGameHour)
            * Mathf.Clamp(
                milestoneModifiers.AutomaticMaintenanceWorkMultiplier,
                0.1f,
                1f)
            * deltaTime
            / SecondsPerGameHour;
        float maintenance = Mathf.Max(
            0f,
            state.Maintenance - maintenanceDrain);
        float fault = state.Fault;
        if (maintenance <= 25f)
        {
            fault = Mathf.Clamp(
                fault + (25f - maintenance) * 0.006f * deltaTime,
                0f,
                100f);
        }
        state.SetCondition(maintenance, fault);

        if (state.Fault >= 100f)
        {
            state.SetStatus(new InfrastructureStatus(
                InfrastructureStatusCode.MaintenanceRequired,
                state.Fault.ToString("0.###")));
            return;
        }

        if (state.Mode != AutomationMode.Automatic)
        {
            state.SetStatus(InfrastructureStatus.None);
            return;
        }

        IReadOnlyList<ProductionBillSnapshot> bills =
            productionQuery.GetBills(facility);
        ProductionBillSnapshot bill = bills.FirstOrDefault(candidate =>
            string.IsNullOrWhiteSpace(candidate.ReservedWorkerId)
            && candidate.Status is ProductionBillStatus.Ready
                or ProductionBillStatus.InProgress);
        if (bill == null)
        {
            state.SetStatus(new InfrastructureStatus(
                bills.Count > 0
                    ? InfrastructureStatusCode.ProductionMaterialUnavailable
                    : InfrastructureStatusCode.ProductionOrderUnavailable));
            return;
        }

        if (!bill.MaterialsConsumed)
        {
            ProductionWorkBeginResult begin = productionWork.BeginWork(
                null,
                facility,
                bill.WorkTypeId);
            if (!begin.Succeeded)
            {
                state.SetStatus(new InfrastructureStatus(
                    InfrastructureStatusCode.ProductionMaterialUnavailable));
                return;
            }
            bill = begin.Bill;
        }

        float work = Mathf.Max(
                0.01f,
                ability.automaticWorkPerSecond)
            * ResolveConditionMultiplier(state)
            * deltaTime;
        ProductionWorkExecutionResult execution = productionWork.ExecuteWork(
                null,
                facility,
                bill.BillId,
                work);
        if (!execution.Succeeded)
        {
            state.SetStatus(new InfrastructureStatus(
                InfrastructureStatusCode.ProductionOutputUnavailable));
            return;
        }

        RecordAutomaticWork(
            facility,
            facilityId,
            bill,
            execution,
            maintenanceDrain);

        state.SetStatus(InfrastructureStatus.None);
    }

    private void RecordAutomaticWork(
        BuildableObject facility,
        string facilityId,
        ProductionBillSnapshot before,
        ProductionWorkExecutionResult execution,
        float maintenanceBurdenWu)
    {
        if (before == null)
        {
            return;
        }

        ProductionBillSnapshot after = productionQuery
            .GetBills(facility)
            .FirstOrDefault(candidate => candidate.BillId == before.BillId);
        float acceptedWork = AutomationLaborAccountingRules.CalculateNetAutomaticWork(
            before,
            after,
            execution,
            maintenanceBurdenWu);

        if (acceptedWork <= 0f)
        {
            return;
        }

        string operationId =
            $"automation:{projectedRestoreRevision}:{facilityId}:{before.BillId.Value}";
        long sequence = unchecked((uint)clock.FrameCount);
        EmergencyAccountingResult recorded = laborAccounting.Record(
            new SettlementLaborContribution(
                operationId,
                sequence,
                SettlementLaborContributionChannel.DomainAutomation,
                EmergencyWuUnits.FromWu(acceptedWork),
                before.WorkTypeId.Value));
        if (!recorded.Success)
        {
            throw new InvalidOperationException(
                $"{recorded.Code}: {recorded.Message}");
        }
    }

    private void EnsureFacilities()
    {
        EnsureRestoreProjectionCurrent();
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
        AutomationFacilityStateSession state)
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
            BuildingId = new BuildingInstanceId(facilityId),
            Mode = state.Mode,
            Powered = powered,
            Operational = state.Mode == AutomationMode.Manual
                || powered && state.Fault < 100f,
            WorkRate = workRate * ResolveConditionMultiplier(state),
            Maintenance = state.Maintenance,
            Fault = state.Fault,
            Status = state.Status
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

    private AutomationFacilityStateSession EnsureState(string facilityId) =>
        stateSession.GetOrCreate(facilityId);

    private static float ResolveConditionMultiplier(
        AutomationFacilityStateSession state)
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
            stateSession.IncrementVersion();
        }
    }

    private void EnsureRestoreProjectionCurrent()
    {
        int revision = aggregateRootStore.PublishedRestoreRevision;
        if (projectedRestoreRevision == revision)
        {
            return;
        }

        projectedRestoreRevision = revision;
        ResetProjectionAfterRestore();
    }

    private void ResetProjectionAfterRestore()
    {
        buildingVersion = int.MinValue;
        automationFacilities.Clear();
        facilities = Array.Empty<AutomationFacilitySnapshot>();
        accumulated = 0f;
    }
}
