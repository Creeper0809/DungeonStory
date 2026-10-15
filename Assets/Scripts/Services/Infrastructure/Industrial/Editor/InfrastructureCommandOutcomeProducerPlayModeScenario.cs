#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class InfrastructureCommandOutcomeProducerPlayModeScenario
{
    private const string FilterItemId = "material:lumber";
    private const string PayloadId =
        "qa:phase80-infrastructure-command-payload";

    internal delegate void PublishConveyorDestination(
        string destinationId,
        BuildableObject output,
        string outputFacilityId,
        Vector2Int outputDropPosition,
        PhysicalMassGrams capacity,
        long capacityRevision);

    public static void Verify(
        IReadOnlyList<BuildableObject> buildings,
        IPowerInfrastructureQuery powerQuery,
        IPowerInfrastructureCommand powerCommands,
        IPowerInfrastructurePersistence powerPersistence,
        IFluidInfrastructureCommand fluidCommands,
        IFluidInfrastructurePersistence fluidPersistence,
        IConveyorInfrastructureCommand conveyorCommands,
        IConveyorInfrastructurePersistence conveyorPersistence,
        IAutomationInfrastructureQuery automationQuery,
        IAutomationInfrastructureCommand automationCommands,
        IAutomationInfrastructurePersistence automationPersistence,
        IResourceEconomyContentCatalog productionCatalog,
        IProductionBillOrderCommand productionOrders,
        IProductionBillQuery productionQuery,
        IProductionBillWorkExecution productionWork,
        BlueprintResearchRuntime blueprintResearch,
        IInfrastructureObservationPersistence outcomePersistence,
        IGameplayOutcomeQuery outcomeQuery,
        IWorldItemStackRuntime items,
        IItemTransferService itemTransfers,
        IPhysicalItemMassQuery itemMass,
        PublishConveyorDestination publishDestination,
        ICollection<string> report)
    {
        Require(buildings != null && buildings.Count > 0,
            "Infrastructure command producer fixture has no buildings.");
        Require(powerQuery != null
                && powerCommands != null
                && powerPersistence != null
                && fluidCommands != null
                && fluidPersistence != null
                && conveyorCommands != null
                && conveyorPersistence != null
                && automationQuery != null
                && automationCommands != null
                && automationPersistence != null
                && productionCatalog != null
                && productionOrders != null
                && productionQuery != null
                && productionWork != null
                && blueprintResearch != null
                && outcomePersistence != null
                && outcomeQuery != null
                && items != null
                && itemTransfers != null
                && itemMass != null
                && publishDestination != null
                && report != null,
            "Infrastructure command producer fixture is missing a dependency.");

        long firstOwnerRevision = outcomePersistence.Capture()
            .nextOwnerRevision;
        int changed = 0;
        int noOps = 0;

        BuildableObject powerConnection = buildings.FirstOrDefault(building =>
            powerQuery.TryGetNode(building, out PowerNodeSnapshot snapshot)
            && snapshot.HasControllableConnection);
        Require(powerConnection != null,
            "No live controllable power connection exists in the fixture.");
        Require(powerQuery.TryGetNode(
                powerConnection,
                out PowerNodeSnapshot connectionBefore),
            "The controllable power node disappeared.");
        bool desiredConnection = !connectionBefore.ConnectionEnabled;
        ExecuteChanged(
            "power-connection",
            InfrastructureCommandOutcomeKind.PowerConnectionChanged,
            powerConnection,
            () => powerCommands.SetConnectionEnabled(
                powerConnection,
                desiredConnection),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "power-connection-no-op",
            powerConnection,
            () => powerCommands.SetConnectionEnabled(
                powerConnection,
                desiredConnection),
            () => JsonUtility.ToJson(powerPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        BuildableObject powerConsumer = buildings.FirstOrDefault(building =>
            building?.BuildingData?.GetAbility<BuildingPowerConsumerAbility>()
                != null
            && powerQuery.TryGetNode(building, out _));
        PowerNodeSnapshot priorityBefore = default;
        Require(powerConsumer != null
                && powerQuery.TryGetNode(
                    powerConsumer,
                    out priorityBefore),
            "No live power consumer exists in the fixture.");
        PowerPriority desiredPriority = priorityBefore.Priority
            == PowerPriority.Critical
                ? PowerPriority.Optional
                : PowerPriority.Critical;
        ExecuteChanged(
            "power-priority",
            InfrastructureCommandOutcomeKind.PowerPriorityChanged,
            powerConsumer,
            () => powerCommands.SetPriority(powerConsumer, desiredPriority),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "power-priority-no-op",
            powerConsumer,
            () => powerCommands.SetPriority(powerConsumer, desiredPriority),
            () => JsonUtility.ToJson(powerPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        BuildableObject breaker = FindByCode(buildings, "I05");
        _ = powerQuery.TryGetNode(breaker, out _);
        DungeonPowerInfrastructureSaveData powerSave =
            powerPersistence.Capture();
        PowerNodeSaveData breakerRow = powerSave.nodes.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                GetNodeId(breaker),
                StringComparison.Ordinal));
        breakerRow.heat = 0f;
        breakerRow.fault = 10f;
        breakerRow.breakerTripped = true;
        powerPersistence.Restore(powerPersistence.PrepareRestore(powerSave));
        ExecuteChanged(
            "power-breaker-reset",
            InfrastructureCommandOutcomeKind.PowerBreakerReset,
            breaker,
            () => powerCommands.ResetBreaker(breaker),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "power-breaker-reset-no-op",
            breaker,
            () => powerCommands.ResetBreaker(breaker),
            () => JsonUtility.ToJson(powerPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        BuildableObject waterTransfer = FindByCode(buildings, "I10");
        DungeonFluidInfrastructureSaveData fluidSave =
            fluidPersistence.Capture();
        FluidNodeSaveData fluidRow = fluidSave.nodes.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                GetNodeId(waterTransfer),
                StringComparison.Ordinal));
        fluidRow.blockage = 25f;
        fluidRow.leak = 15f;
        fluidRow.transferWork = 3f;
        fluidPersistence.Restore(fluidPersistence.PrepareRestore(fluidSave));
        ExecuteChanged(
            "fluid-blockage-clear",
            InfrastructureCommandOutcomeKind.FluidBlockageCleared,
            waterTransfer,
            () => fluidCommands.ClearBlockage(waterTransfer),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "fluid-blockage-clear-no-op",
            waterTransfer,
            () => fluidCommands.ClearBlockage(waterTransfer),
            () => JsonUtility.ToJson(fluidPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        WaterContainerTransferMode desiredTransferMode =
            fluidRow.transferMode == WaterContainerTransferMode.FeedNetwork
                ? WaterContainerTransferMode.BottleFromNetwork
                : WaterContainerTransferMode.FeedNetwork;
        ExecuteChanged(
            "water-transfer-mode",
            InfrastructureCommandOutcomeKind.WaterTransferModeChanged,
            waterTransfer,
            () => fluidCommands.SetWaterTransferMode(
                waterTransfer,
                desiredTransferMode),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "water-transfer-mode-no-op",
            waterTransfer,
            () => fluidCommands.SetWaterTransferMode(
                waterTransfer,
                desiredTransferMode),
            () => JsonUtility.ToJson(fluidPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        ExecuteChanged(
            "fluid-leak-repair",
            InfrastructureCommandOutcomeKind.FluidLeakRepaired,
            waterTransfer,
            () => fluidCommands.RepairLeak(waterTransfer),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "fluid-leak-repair-no-op",
            waterTransfer,
            () => fluidCommands.RepairLeak(waterTransfer),
            () => JsonUtility.ToJson(fluidPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        BuildableObject conveyorSegment = buildings.First(building =>
            building?.BuildingData?.GetAbility<
                BuildingConveyorSegmentAbility>() != null);
        ExecuteChanged(
            "conveyor-node-enabled",
            InfrastructureCommandOutcomeKind.ConveyorNodeEnabledChanged,
            conveyorSegment,
            () => conveyorCommands.SetNodeEnabled(conveyorSegment, false),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "conveyor-node-enabled-no-op",
            conveyorSegment,
            () => conveyorCommands.SetNodeEnabled(conveyorSegment, false),
            () => JsonUtility.ToJson(conveyorPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        BuildableObject conveyorOutput = FindByCode(buildings, "C03");
        string outputFacilityId = GetNodeId(conveyorOutput);
        string destinationId =
            "qa:phase80-infrastructure-command:" + outputFacilityId;
        Vector2Int outputPosition = conveyorOutput.BuildingData
            .GetGridPosList(conveyorOutput.centerPos)
            .First();
        PhysicalMassGrams capacity = itemMass.GetQuantityMass(
            (ItemDefinitionId)FilterItemId,
            PhysicalItemMassSubject.ForDefinition(
                (ItemDefinitionId)FilterItemId),
            4);
        publishDestination(
            destinationId,
            conveyorOutput,
            outputFacilityId,
            outputPosition,
            capacity,
            1L);
        ExecuteChanged(
            "conveyor-destination",
            InfrastructureCommandOutcomeKind.ConveyorDestinationChanged,
            conveyorOutput,
            () => conveyorCommands.SetPortDestination(
                conveyorOutput,
                destinationId),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "conveyor-destination-no-op",
            conveyorOutput,
            () => conveyorCommands.SetPortDestination(
                conveyorOutput,
                destinationId),
            () => JsonUtility.ToJson(conveyorPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        BuildableObject overflow = FindByCode(buildings, "C09");
        ExecuteChanged(
            "conveyor-overflow-policy",
            InfrastructureCommandOutcomeKind.ConveyorOverflowPolicyChanged,
            overflow,
            () => conveyorCommands.SetOverflowPolicy(
                overflow,
                ConveyorOverflowPolicy.ManualApproval,
                string.Empty),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "conveyor-overflow-policy-no-op",
            overflow,
            () => conveyorCommands.SetOverflowPolicy(
                overflow,
                ConveyorOverflowPolicy.ManualApproval,
                string.Empty),
            () => JsonUtility.ToJson(conveyorPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        ExecuteChanged(
            "conveyor-filter",
            InfrastructureCommandOutcomeKind.ConveyorFilterChanged,
            conveyorSegment,
            () => conveyorCommands.SetFilter(
                conveyorSegment,
                new[] { FilterItemId },
                Array.Empty<StockCategory>(),
                false),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "conveyor-filter-no-op",
            conveyorSegment,
            () => conveyorCommands.SetFilter(
                conveyorSegment,
                new[] { FilterItemId },
                Array.Empty<StockCategory>(),
                false),
            () => JsonUtility.ToJson(conveyorPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        HashSet<string> stacksBefore = items.GetStacksAt(
                overflow.centerPos,
                includeStored: true)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        Require(items.SpawnItemAt(
                FilterItemId,
                1,
                overflow.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            && spawned == 1,
            "Could not spawn the overflow approval custody fixture.");
        WorldItemStackSnapshot overflowStack = items.GetStacksAt(
                overflow.centerPos,
                includeStored: true)
            .Single(value => value.ItemId == FilterItemId
                && !stacksBefore.Contains(value.StackId));
        Require(itemTransfers.TryBeginTransit(
                new ItemStackId(overflowStack.StackId),
                overflow.centerPos,
                PayloadId,
                out _,
                out DomainFailure transitFailure),
            "Could not begin overflow approval custody: "
            + transitFailure.Code);
        DungeonConveyorInfrastructureSaveData conveyorSave =
            conveyorPersistence.Capture();
        conveyorSave.payloads.Add(new ConveyorPayloadSaveData
        {
            payloadId = PayloadId,
            itemStackId = overflowStack.StackId,
            segmentBuildingInstanceId = GetNodeId(overflow),
            destinationId = "qa:phase80-infrastructure-command-unreachable",
            progress = 0f,
            lastMovedAt = 0f,
            stalledSince = 0f,
            routeVersion = 0,
            stallReason = ConveyorStallReason.None,
            overflowApproved = false
        });
        conveyorPersistence.Restore(
            conveyorPersistence.PrepareRestore(conveyorSave));
        ExecuteChanged(
            "conveyor-overflow-approval",
            InfrastructureCommandOutcomeKind.ConveyorOverflowApproved,
            overflow,
            () => conveyorCommands.ApproveOverflow(PayloadId),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "conveyor-overflow-approval-no-op",
            overflow,
            () => conveyorCommands.ApproveOverflow(PayloadId),
            () => JsonUtility.ToJson(conveyorPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        BuildableObject automationFacility = buildings.First(building =>
            building?.BuildingData?.GetAbility<BuildingAutomationAbility>()
                != null);
        Require(automationQuery.TryGetFacility(
                automationFacility,
                out AutomationFacilitySnapshot automationBefore),
            "The live automation facility is unavailable.");
        AutomationMode desiredMode = automationBefore.Mode
            == AutomationMode.Automatic
                ? AutomationMode.PoweredAssist
                : AutomationMode.Automatic;
        ExecuteChanged(
            "automation-mode",
            InfrastructureCommandOutcomeKind.AutomationModeChanged,
            automationFacility,
            () => automationCommands.SetMode(
                automationFacility,
                desiredMode),
            outcomePersistence,
            outcomeQuery);
        changed++;
        ExecuteNoOp(
            "automation-mode-no-op",
            automationFacility,
            () => automationCommands.SetMode(
                automationFacility,
                desiredMode),
            () => JsonUtility.ToJson(automationPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        DungeonAutomationSaveData automationSave =
            automationPersistence.Capture();
        AutomationFacilitySaveData automationRow =
            automationSave.facilities.Single(value => string.Equals(
                value.buildingInstanceId,
                GetNodeId(automationFacility),
                StringComparison.Ordinal));
        automationRow.maintenance = 50f;
        automationRow.fault = 20f;
        automationPersistence.Restore(
            automationPersistence.PrepareRestore(automationSave));
        ExecuteChanged(
            "automation-maintenance",
            InfrastructureCommandOutcomeKind.AutomationMaintained,
            automationFacility,
            () => automationCommands.Maintain(automationFacility, 10f),
            outcomePersistence,
            outcomeQuery);
        changed++;

        automationSave = automationPersistence.Capture();
        automationRow = automationSave.facilities.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                GetNodeId(automationFacility),
                StringComparison.Ordinal));
        automationRow.maintenance = 100f;
        automationRow.fault = 0f;
        automationPersistence.Restore(
            automationPersistence.PrepareRestore(automationSave));
        ExecuteNoOp(
            "automation-maintenance-no-op",
            automationFacility,
            () => automationCommands.Maintain(automationFacility, 10f),
            () => JsonUtility.ToJson(automationPersistence.Capture()),
            outcomePersistence,
            outcomeQuery);
        noOps++;

        Require(automationQuery is VContainer.Unity.ITickable,
            "The live automation runtime does not expose its tick entry point.");
        VContainer.Unity.ITickable automationTick =
            (VContainer.Unity.ITickable)automationQuery;
        EnsureAutomationFacilityPowered(
            buildings,
            powerQuery,
            powerPersistence,
            automationFacility);
        ProductionBillId productionBillId = PrepareAutomationProductionProbe(
            automationFacility,
            productionCatalog,
            productionOrders,
            productionQuery,
            productionWork,
            blueprintResearch,
            items);
        automationSave = automationPersistence.Capture();
        automationRow = automationSave.facilities.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                GetNodeId(automationFacility),
                StringComparison.Ordinal));
        automationRow.mode = AutomationMode.Automatic;
        automationRow.maintenance = 100f;
        automationRow.fault = 0f;
        automationPersistence.Restore(
            automationPersistence.PrepareRestore(automationSave));
        ProductionBillSnapshot productionBefore = RequireProductionBill(
            productionQuery,
            automationFacility,
            productionBillId);
        TickUntilProductionProgress(
            automationTick,
            productionQuery,
            automationFacility,
            productionBillId,
            productionBefore.CompletedWork);
        ProductionBillSnapshot productionRunning = RequireProductionBill(
            productionQuery,
            automationFacility,
            productionBillId);
        Require(productionRunning.CompletedWork > productionBefore.CompletedWork,
            "The authored automatic facility did not produce a real work result before its maintenance fault.");

        automationSave = automationPersistence.Capture();
        automationRow = automationSave.facilities.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                GetNodeId(automationFacility),
                StringComparison.Ordinal));
        automationRow.mode = AutomationMode.Automatic;
        automationRow.maintenance = 0f;
        automationRow.fault = 99.99f;
        automationPersistence.Restore(
            automationPersistence.PrepareRestore(automationSave));
        float firstThresholdWork = ExecuteMaintenanceThresholdEntry(
            "automation-maintenance-threshold-entry",
            automationFacility,
            automationTick,
            automationQuery,
            automationPersistence,
            productionQuery,
            productionBillId,
            outcomePersistence,
            outcomeQuery);
        changed++;

        DungeonAutomationSaveData enteredSave =
            automationPersistence.Capture();
        long revisionAfterFirstEntry = outcomePersistence.Capture()
            .nextOwnerRevision;
        automationPersistence.Restore(
            automationPersistence.PrepareRestore(enteredSave));
        TickMany(automationTick);
        ProductionBillSnapshot productionAfterRestore = RequireProductionBill(
            productionQuery,
            automationFacility,
            productionBillId);
        Require(
            outcomePersistence.Capture().nextOwnerRevision
                == revisionAfterFirstEntry
            && automationQuery.TryGetFacility(
                automationFacility,
                out AutomationFacilitySnapshot restoredThreshold)
            && restoredThreshold.Status.Code
                == InfrastructureStatusCode.MaintenanceRequired
            && Mathf.Approximately(
                productionAfterRestore.CompletedWork,
                firstThresholdWork),
            "Restoring a thresholded automation facility duplicated its entry outcome or lost its status.");

        ExecuteChanged(
            "automation-maintenance-threshold-repair",
            InfrastructureCommandOutcomeKind.AutomationMaintained,
            automationFacility,
            () => automationCommands.Maintain(automationFacility, 20f),
            outcomePersistence,
            outcomeQuery);
        changed++;

        TickUntilProductionProgress(
            automationTick,
            productionQuery,
            automationFacility,
            productionBillId,
            firstThresholdWork);
        ProductionBillSnapshot productionResumed = RequireProductionBill(
            productionQuery,
            automationFacility,
            productionBillId);
        Require(productionResumed.CompletedWork > firstThresholdWork,
            "Repairing the thresholded facility did not resume its real production result.");

        automationSave = automationPersistence.Capture();
        automationRow = automationSave.facilities.Single(value =>
            string.Equals(
                value.buildingInstanceId,
                GetNodeId(automationFacility),
                StringComparison.Ordinal));
        automationRow.mode = AutomationMode.Automatic;
        automationRow.maintenance = 0f;
        automationRow.fault = 99.99f;
        automationPersistence.Restore(
            automationPersistence.PrepareRestore(automationSave));
        float secondThresholdWork = ExecuteMaintenanceThresholdEntry(
            "automation-maintenance-threshold-reentry",
            automationFacility,
            automationTick,
            automationQuery,
            automationPersistence,
            productionQuery,
            productionBillId,
            outcomePersistence,
            outcomeQuery);
        changed++;

        report.Add(
            "automationMaintenanceProduction=PASS;facility="
            + GetNodeId(automationFacility)
            + ";bill=" + productionBillId.Value
            + ";running="
            + InvariantFloat(
                productionRunning.CompletedWork)
            + ";threshold="
            + InvariantFloat(firstThresholdWork)
            + ";resumed="
            + InvariantFloat(
                productionResumed.CompletedWork)
            + ";reentry="
            + InvariantFloat(secondThresholdWork)
            + ";steadyDuplicate=0;restoreDuplicate=0");

        DungeonInfrastructureObservationSaveData finalOwner =
            outcomePersistence.Capture();
        Require(changed == 16
                && noOps == 13
                && finalOwner.nextOwnerRevision
                    == firstOwnerRevision + changed
                && finalOwner.pending.Count == 0,
            "Infrastructure command producer coverage did not converge.");
        report.Add(
            "infrastructureCommandProducers=PASS;changed=" + changed
            + ";noOps=" + noOps
            + ";ownerRevision=" + firstOwnerRevision + "->"
            + finalOwner.nextOwnerRevision);
    }

    private static ProductionBillId PrepareAutomationProductionProbe(
        BuildableObject facility,
        IResourceEconomyContentCatalog catalog,
        IProductionBillOrderCommand orders,
        IProductionBillQuery query,
        IProductionBillWorkExecution work,
        BlueprintResearchRuntime research,
        IWorldItemStackRuntime items)
    {
        BuildingProductionWorkstationAbility workstation = facility
            .BuildingData
            ?.GetProductionWorkstationAbility();
        Require(workstation != null,
            "The authored automation fixture has no production workstation.");
        ProductionRecipeSO recipe = catalog.Recipes
            .Where(candidate => IsProductionProbeCandidate(
                facility.BuildingData,
                candidate))
            .OrderByDescending(candidate => candidate.RequiredWork)
            .ThenBy(candidate => candidate.RecipeId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(recipe != null,
            "The authored automation fixture has no support-free physical production recipe.");
        if (!string.IsNullOrWhiteSpace(recipe.RequiredResearchId)
            && !research.State.Projects.IsCompleted(
                new ResearchProjectId(recipe.RequiredResearchId)))
        {
            research.State.Projects.RestoreCompleted(
                new ResearchProjectId(recipe.RequiredResearchId));
        }

        ProductionBillCommandResult added = orders.AddBill(
            facility,
            recipe.RecipeId,
            ProductionOrderMode.RepeatCount,
            1);
        Require(added.Succeeded,
            "Could not create the automation production probe: "
            + added.Failure.Code);
        ProductionBillSnapshot bill = RequireProductionBill(
            query,
            facility,
            added.BillId);
        foreach (ItemAmountDefinition input in bill.Inputs)
        {
            Require(items.SpawnItemAt(
                    input.ItemId,
                    input.Amount,
                    facility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    bill.MaterialDestinationId,
                    out int spawned)
                && spawned == input.Amount,
                "Could not publish exact automation production input: "
                + input.ItemId);
        }

        ProductionWorkBeginResult begun = work.BeginWork(
            null,
            facility,
            recipe.WorkTypeId);
        Require(begun.Succeeded
                && begun.Bill.BillId == added.BillId
                && begun.Bill.MaterialsConsumed,
            "The automation production probe did not consume its exact inputs: "
            + begun.Failure.Code);
        return added.BillId;
    }

    internal static bool IsProductionProbeCandidate(
        BuildingSO facility,
        ProductionRecipeSO candidate)
    {
        BuildingProductionWorkstationAbility workstation = facility
            ?.GetProductionWorkstationAbility();
        return workstation != null
            && candidate != null
            && candidate.ProcessKind == ProductionProcessKind.WorkOnly
            && candidate.RequiredSupportTags.Count == 0
            && candidate.CleanWaterPerCycle <= 0f
            && candidate.Inputs.Count > 0
            && candidate.Outputs.Count > 0
            && candidate.Outputs.All(output => output != null
                && ProductionOutputRoleRules.IsPhysical(output.Role))
            && facility.Facility != null
            && facility.Facility.SupportsWork(candidate.WorkTypeId)
            && string.Equals(
                workstation.WorkstationTag,
                candidate.WorkstationTag,
                StringComparison.Ordinal);
    }

    private static ProductionBillSnapshot RequireProductionBill(
        IProductionBillQuery query,
        BuildableObject facility,
        ProductionBillId billId)
    {
        ProductionBillSnapshot result = query.GetBills(facility)
            .SingleOrDefault(candidate => candidate.BillId == billId);
        Require(result != null,
            "The automation production probe bill disappeared: "
            + billId.Value);
        return result;
    }

    private static void TickUntilProductionProgress(
        VContainer.Unity.ITickable automationTick,
        IProductionBillQuery query,
        BuildableObject facility,
        ProductionBillId billId,
        float completedBefore)
    {
        for (int attempt = 0; attempt < 512; attempt++)
        {
            automationTick.Tick();
            if (RequireProductionBill(
                    query,
                    facility,
                    billId).CompletedWork > completedBefore)
            {
                return;
            }
        }
    }

    private static void EnsureAutomationFacilityPowered(
        IReadOnlyList<BuildableObject> buildings,
        IPowerInfrastructureQuery powerQuery,
        IPowerInfrastructurePersistence powerPersistence,
        BuildableObject automationFacility)
    {
        Require(powerQuery is VContainer.Unity.ITickable,
            "The live power runtime does not expose its tick entry point.");
        DungeonPowerInfrastructureSaveData save = powerPersistence.Capture();
        Dictionary<string, PowerNodeSaveData> rows = save.nodes
            .ToDictionary(
                value => value.buildingInstanceId,
                StringComparer.Ordinal);
        int poweredSources = 0;
        foreach (BuildableObject building in buildings.Where(value =>
                     value != null && !value.IsGridDestroyed))
        {
            string nodeId = GetNodeId(building);
            if (!rows.TryGetValue(nodeId, out PowerNodeSaveData row))
                continue;
            row.connectionState = 1;
            row.breakerTripped = false;
            row.heat = 0f;
            row.fault = 0f;
            BuildingPowerProducerAbility producer = building.BuildingData
                ?.GetAbility<BuildingPowerProducerAbility>();
            if (producer != null)
            {
                if (producer.requiresFuel)
                    row.fuelSeconds = Mathf.Max(row.fuelSeconds, 600f);
                poweredSources++;
            }
            BuildingPowerStorageAbility storage = building.BuildingData
                ?.GetAbility<BuildingPowerStorageAbility>();
            if (storage != null)
                row.storedPower = storage.capacity;
        }
        Require(poweredSources > 0,
            "The live fixture has no authored power producer.");
        powerPersistence.Restore(powerPersistence.PrepareRestore(save));
        VContainer.Unity.ITickable powerTick =
            (VContainer.Unity.ITickable)powerQuery;
        for (int attempt = 0; attempt < 64; attempt++)
            powerTick.Tick();
        Require(
            powerQuery.TryGetNode(
                automationFacility,
                out PowerNodeSnapshot automationPower)
            && automationPower.Powered,
            "The live automation facility did not receive authored network power.");
    }

    private static float ExecuteMaintenanceThresholdEntry(
        string label,
        BuildableObject facility,
        VContainer.Unity.ITickable automationTick,
        IAutomationInfrastructureQuery automationQuery,
        IAutomationInfrastructurePersistence automationPersistence,
        IProductionBillQuery productionQuery,
        ProductionBillId productionBillId,
        IInfrastructureObservationPersistence outcomePersistence,
        IGameplayOutcomeQuery outcomeQuery)
    {
        DungeonInfrastructureObservationSaveData before =
            outcomePersistence.Capture();
        long revision = before.nextOwnerRevision;
        TickUntilOwnerRevision(
            automationTick,
            outcomePersistence,
            revision + 1L);
        DungeonInfrastructureObservationSaveData after =
            outcomePersistence.Capture();
        Require(
            after.nextOwnerRevision == revision + 1L
            && after.pending.Count == 0,
            label + " did not commit exactly one owner revision.");
        Require(
            automationQuery.TryGetFacility(
                facility,
                out AutomationFacilitySnapshot thresholded)
            && thresholded.Fault >= 100f
            && thresholded.Status.Code
                == InfrastructureStatusCode.MaintenanceRequired,
            label + " did not enter MaintenanceRequired at fault 100.");
        AutomationFacilitySaveData saved = automationPersistence.Capture()
            .facilities.Single(value => string.Equals(
                value.buildingInstanceId,
                GetNodeId(facility),
                StringComparison.Ordinal));
        Require(saved.fault >= 100f,
            label + " did not persist its thresholded fault.");
        ProductionBillSnapshot productionAtThreshold = RequireProductionBill(
            productionQuery,
            facility,
            productionBillId);
        Require(
            TryFindOutcome(outcomeQuery, facility, revision, out var exact)
            && exact.facts.Any(value =>
                value.factId
                    == InfrastructureCommandOutcomeIds.CommandKindFact.Value
                && value.value
                    == "automation-maintenance-required-entered")
            && exact.facts.Any(value =>
                value.factId
                    == InfrastructureCommandOutcomeIds.BeforeValueFact.Value
                && value.value.Contains("fault=99.99", StringComparison.Ordinal)
                && value.value.Contains(
                    "cause=continuous-wear",
                    StringComparison.Ordinal))
            && exact.facts.Any(value =>
                value.factId
                    == InfrastructureCommandOutcomeIds.AfterValueFact.Value
                && value.value.Contains("fault=100", StringComparison.Ordinal)
                && value.value.Contains(
                    "status=MaintenanceRequired",
                    StringComparison.Ordinal)),
            label + " lost its typed cause or before/after state facts.");

        long stableRevision = after.nextOwnerRevision;
        TickMany(automationTick);
        ProductionBillSnapshot productionAfterSteadyWear =
            RequireProductionBill(
                productionQuery,
                facility,
                productionBillId);
        Require(
            outcomePersistence.Capture().nextOwnerRevision == stableRevision
            && Mathf.Approximately(
                productionAfterSteadyWear.CompletedWork,
                productionAtThreshold.CompletedWork),
            label + " emitted repeatedly or advanced production while the facility stayed at the threshold.");
        return productionAtThreshold.CompletedWork;
    }

    private static void TickUntilOwnerRevision(
        VContainer.Unity.ITickable automationTick,
        IInfrastructureObservationPersistence persistence,
        long expectedRevision)
    {
        for (int attempt = 0;
             attempt < 256
             && persistence.Capture().nextOwnerRevision < expectedRevision;
             attempt++)
        {
            automationTick.Tick();
        }
    }

    private static void TickMany(VContainer.Unity.ITickable automationTick)
    {
        for (int attempt = 0; attempt < 64; attempt++)
            automationTick.Tick();
    }

    private static void ExecuteChanged(
        string label,
        InfrastructureCommandOutcomeKind kind,
        BuildableObject facility,
        Func<InfrastructureCommandResult> execute,
        IInfrastructureObservationPersistence persistence,
        IGameplayOutcomeQuery query)
    {
        DungeonInfrastructureObservationSaveData before =
            persistence.Capture();
        long revision = before.nextOwnerRevision;
        Require(!TryFindOutcome(query, facility, revision, out _),
            label + " fixture revision already exists in the ledger.");
        InfrastructureCommandResult result = execute();
        DungeonInfrastructureObservationSaveData after =
            persistence.Capture();
        Require(result.Succeeded,
            label + " failed: " + result.Failure.Code + ":"
            + string.Join(",", result.Failure.Parameters.ToArray()));
        Require(after.nextOwnerRevision == revision + 1L
                && after.pending.Count == 0,
            label + " did not commit exactly one shared owner revision.");
        Require(TryFindOutcome(query, facility, revision, out var exact)
                && exact.facts.Any(value =>
                    value.factId
                        == InfrastructureCommandOutcomeIds.CommandKindFact.Value
                    && string.Equals(
                        value.value,
                        InfrastructureCommandOutcomeIds.StableKind(kind),
                        StringComparison.Ordinal)),
            label + " did not publish the expected canonical outcome.");
    }

    private static void ExecuteNoOp(
        string label,
        BuildableObject facility,
        Func<InfrastructureCommandResult> execute,
        Func<string> captureDomain,
        IInfrastructureObservationPersistence persistence,
        IGameplayOutcomeQuery query)
    {
        DungeonInfrastructureObservationSaveData before =
            persistence.Capture();
        long revision = before.nextOwnerRevision;
        string domainBefore = captureDomain();
        Require(!TryFindOutcome(query, facility, revision, out _),
            label + " fixture revision already exists in the ledger.");
        InfrastructureCommandResult result = execute();
        string domainAfter = captureDomain();
        DungeonInfrastructureObservationSaveData after =
            persistence.Capture();
        Require(result.Succeeded,
            label + " failed: " + result.Failure.Code + ":"
            + string.Join(",", result.Failure.Parameters.ToArray()));
        Require(after.nextOwnerRevision == revision
                && after.pending.Count == before.pending.Count
                && string.Equals(
                    domainBefore,
                    domainAfter,
                    StringComparison.Ordinal)
                && !TryFindOutcome(query, facility, revision, out _),
            label + " consumed a revision, emitted an outcome, or mutated state.");
    }

    private static bool TryFindOutcome(
        IGameplayOutcomeQuery query,
        BuildableObject facility,
        long ownerRevision,
        out GameplayOutcomeSnapshot exact)
    {
        GameplayEntityId entity = new(
            InfrastructureCommandOutcomeIds.FacilityKind,
            GetNodeId(facility));
        exact = query.GetForEntity(
                entity,
                OutcomeCursor.FirstPage(64),
                OutcomeFilter.All)
            .Items
            .Select(value => value.Exact)
            .SingleOrDefault(value => value != null
                && value.outcomeTypeId
                    == InfrastructureCommandOutcomeIds.Applied.Value
                && value.ownerRevision == ownerRevision);
        return exact != null;
    }

    private static BuildableObject FindByCode(
        IEnumerable<BuildableObject> buildings,
        string code)
    {
        BuildableObject result = buildings.SingleOrDefault(building =>
            string.Equals(
                building?.BuildingData?.GetAbility<
                    BuildingFacilityPartAbility>()?.code,
                code,
                StringComparison.Ordinal));
        Require(result != null,
            "The infrastructure command fixture is missing " + code + ".");
        return result;
    }

    private static string GetNodeId(BuildableObject building) =>
        building?.RequirePersistentInstanceId().Value ?? string.Empty;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    private static string InvariantFloat(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

}
#endif
