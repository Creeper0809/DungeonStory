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
        IInfrastructureCommandOutcomePersistence outcomePersistence,
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

        DungeonInfrastructureCommandOutcomeSaveData finalOwner =
            outcomePersistence.Capture();
        Require(changed == 13
                && noOps == 13
                && finalOwner.nextOwnerRevision
                    == firstOwnerRevision + changed
                && finalOwner.pendingOutcomes.Count == 0,
            "Infrastructure command producer coverage did not converge.");
        report.Add(
            "infrastructureCommandProducers=PASS;changed=" + changed
            + ";noOps=" + noOps
            + ";ownerRevision=" + firstOwnerRevision + "->"
            + finalOwner.nextOwnerRevision);
    }

    private static void ExecuteChanged(
        string label,
        InfrastructureCommandOutcomeKind kind,
        BuildableObject facility,
        Func<InfrastructureCommandResult> execute,
        IInfrastructureCommandOutcomePersistence persistence,
        IGameplayOutcomeQuery query)
    {
        DungeonInfrastructureCommandOutcomeSaveData before =
            persistence.Capture();
        long revision = before.nextOwnerRevision;
        Require(!TryFindOutcome(query, facility, revision, out _),
            label + " fixture revision already exists in the ledger.");
        InfrastructureCommandResult result = execute();
        DungeonInfrastructureCommandOutcomeSaveData after =
            persistence.Capture();
        Require(result.Succeeded,
            label + " failed: " + result.Failure.Code + ":"
            + string.Join(",", result.Failure.Parameters.ToArray()));
        Require(after.nextOwnerRevision == revision + 1L
                && after.pendingOutcomes.Count == 0,
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
        IInfrastructureCommandOutcomePersistence persistence,
        IGameplayOutcomeQuery query)
    {
        DungeonInfrastructureCommandOutcomeSaveData before =
            persistence.Capture();
        long revision = before.nextOwnerRevision;
        string domainBefore = captureDomain();
        Require(!TryFindOutcome(query, facility, revision, out _),
            label + " fixture revision already exists in the ledger.");
        InfrastructureCommandResult result = execute();
        string domainAfter = captureDomain();
        DungeonInfrastructureCommandOutcomeSaveData after =
            persistence.Capture();
        Require(result.Succeeded,
            label + " failed: " + result.Failure.Code + ":"
            + string.Join(",", result.Failure.Parameters.ToArray()));
        Require(after.nextOwnerRevision == revision
                && after.pendingOutcomes.Count == before.pendingOutcomes.Count
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
}
#endif
