#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputFullPersistenceDebugScenarios
{
    private const string RecipeId = "recipe:sawmill-lumber";
    private const string ItemId = "material:lumber";
    private const string FacilityId =
        "building:qa:sawmill-full-persistence";
    private const string WorkerId =
        "character:qa:sawmill-full-persistence";
    private const long ExpectedBatchMassGrams = 3_600L;
    private const long ExpectedCapacityGrams = 14_400L;

    private static readonly ScenarioConfig SawmillScenario = new(
        "sawmill",
        RecipeId,
        ItemId,
        FacilityId,
        WorkerId,
        "Assets/Resources/SO/Building/Modular/P03_제재소.asset",
        expectedOutputQuantity: 3,
        expectedBatchMassGrams: ExpectedBatchMassGrams,
        expectedCapacityGrams: ExpectedCapacityGrams,
        expectedInitialFreshnessSeconds: null);
    private static readonly ScenarioConfig GrainPorridgeScenario = new(
        "grain-porridge",
        "recipe:grain-porridge",
        "food:grain-porridge",
        "building:qa:cookbench-full-persistence",
        "character:qa:cookbench-full-persistence",
        "Assets/Resources/SO/Building/Modular/P15_조리대.asset",
        expectedOutputQuantity: 6,
        expectedBatchMassGrams: 3_600L,
        expectedCapacityGrams: 14_400L,
        expectedInitialFreshnessSeconds: 360d);
    private const double AgedFreshnessSeconds = 119.375d;

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Prepared Output Full Persistence")]
    public static void RunFromMenu()
    {
        VerifyAllCurrentFormatRoundTrips();
        Debug.Log("V27_SAWMILL_FULL_PERSISTENCE=PASS");
        Debug.Log(
            "V27_PREPARED_OUTPUT_FULL_PERSISTENCE=PASS sawmill=1; perishable=1");
    }

    public static void VerifyAllCurrentFormatRoundTrips()
    {
        VerifyFullCurrentFormatRoundTrip();
        VerifyPerishableCurrentFormatRoundTrip();
    }

    public static void VerifyFullCurrentFormatRoundTrip()
    {
        using RuntimeGraph source = new("Source", 7103, SawmillScenario);
        source.SeedOwnedCurrentFormatBill();
        VerifyStockSensorExactAdmissionAndRetry(source);
        ProductionBillRecord sourceRecord = source.SingleBill;
        source.PrepareWip(sourceRecord);

        ProductionPreparedOutputExecutionResult executed =
            source.PreparedOutput.Execute(
                sourceRecord,
                source.Recipe,
                source.FacilityHandle,
                source.WorkerHandle);
        Require(
            executed.IsValid
            && executed.CycleOutputCompleted
            && executed.Phase == ProductionPreparedOutputPhase.Completed,
            "Source real adapter did not complete the sawmill output.");
        RequireGraphState(source, "source");

        List<DungeonSaveSectionEnvelope> sourceEnvelopes =
            source.Registry.CaptureAll();
        string sourceLiveDurable = source.Lifecycle
            .Capture((BuildingInstanceId)FacilityId)
            .DurableSemanticFingerprint;
        string sourceDetachedDurable = CaptureDetachedLifecycleAggregate(
            source,
            sourceEnvelopes);
        Require(
            string.Equals(
                sourceLiveDurable,
                sourceDetachedDurable,
                StringComparison.Ordinal),
            "Source live and save-only five-contributor lifecycle aggregates diverged.");
        VerifyDestructiveDrainCrossAggregatePreflight(
            source,
            sourceEnvelopes,
            sourceDetachedDurable);
        string sourceItems = RequireEnvelope(
            sourceEnvelopes,
            PhysicalItemsSaveSection.Id).payloadJson;
        string sourceBills = RequireEnvelope(
            sourceEnvelopes,
            ProductionBillsSaveSection.Id).payloadJson;
        string sourceRouting = RequireEnvelope(
            sourceEnvelopes,
            ProductionPreparedOutputRoutingSaveSection.Id).payloadJson;
        string sourceWorld = RequireEnvelope(
            sourceEnvelopes,
            ModularFacilityWorldSaveSection.Id).payloadJson;
        Require(
            JsonUtility.FromJson<DungeonProductionBillSaveData>(sourceBills)
                ?.version == DungeonProductionBillSaveData.CurrentVersion
            && DungeonProductionBillSaveData.CurrentVersion == 22,
            "Source production owner is not a current-format V22 payload.");

        using RuntimeGraph destination = new(
            "Destination",
            7103,
            SawmillScenario);
        BuildableObject staleDestinationFacility = destination.Facility;
        int staleDestructionEvents = 0;
        staleDestinationFacility.OnBuildingDestroyed += () =>
            staleDestructionEvents++;
        DungeonGameRestoreReport report = new();
        Require(
            destination.Registry.RestoreAll(sourceEnvelopes, report)
            && report.Success,
            "Full registry restore failed: "
            + string.Join(" | ", report.Errors));
        Require(
            !destination.LifecycleRestoreCandidates.IsCandidateActive
            && destination.LifecycleRestoreCandidates.PublishedSourceCount == 0,
            "Successful full restore retained normalized candidate references.");

        BuildableObject replacementFacility = destination.CurrentFacility;
        Require(
            replacementFacility != null
            && !ReferenceEquals(replacementFacility, staleDestinationFacility)
            && string.Equals(
                replacementFacility.PersistentInstanceId.Value,
                staleDestinationFacility.PersistentInstanceId.Value,
                StringComparison.Ordinal)
            && staleDestinationFacility.IsGridDestroyed
            && staleDestructionEvents == 0,
            "Same-ID facility replacement did not retire the stale object without a gameplay destruction event.");

        RequireGraphState(destination, "destination");
        RequireAuthorityJoinState(destination, "destination joined");
        List<DungeonSaveSectionEnvelope> recaptured =
            destination.Registry.CaptureAll();
        string destinationLiveDurable = destination.Lifecycle
            .Capture((BuildingInstanceId)FacilityId)
            .DurableSemanticFingerprint;
        string destinationDetachedDurable = CaptureDetachedLifecycleAggregate(
            destination,
            recaptured);
        Require(
            string.Equals(
                sourceLiveDurable,
                destinationLiveDurable,
                StringComparison.Ordinal)
            && string.Equals(
                sourceDetachedDurable,
                destinationDetachedDurable,
                StringComparison.Ordinal)
            && string.Equals(
                sourceLiveDurable,
                destinationDetachedDurable,
                StringComparison.Ordinal),
            "Source/save/restored live five-contributor lifecycle aggregates diverged.");
        RequireExactSectionIdentity(
            sourceWorld,
            RequireEnvelope(
                recaptured,
                ModularFacilityWorldSaveSection.Id).payloadJson,
            "facility world");
        RequireExactSectionIdentity(
            sourceItems,
            RequireEnvelope(
                recaptured,
                PhysicalItemsSaveSection.Id).payloadJson,
            "physical items");
        RequireExactSectionIdentity(
            sourceBills,
            RequireEnvelope(
                recaptured,
                ProductionBillsSaveSection.Id).payloadJson,
            "production bills");
        RequireExactSectionIdentity(
            sourceRouting,
            RequireEnvelope(
                recaptured,
                ProductionPreparedOutputRoutingSaveSection.Id).payloadJson,
            "prepared-output routing");

        string lifecycleBeforeTick = destination.Lifecycle
            .Capture((BuildingInstanceId)FacilityId)
            .SemanticFingerprint;
        string sectionsBeforeTick = CaptureRuntimeSectionFingerprint(destination);
        destination.Production.Tick();
        Require(
            string.Equals(
                lifecycleBeforeTick,
                destination.Lifecycle.Capture((BuildingInstanceId)FacilityId)
                    .SemanticFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                sectionsBeforeTick,
                CaptureRuntimeSectionFingerprint(destination),
                StringComparison.Ordinal),
            "First production Tick mutated the same-ID restored authority graph.");
        destination.Production.Tick();
        Require(
            string.Equals(
                lifecycleBeforeTick,
                destination.Lifecycle.Capture((BuildingInstanceId)FacilityId)
                    .SemanticFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                sectionsBeforeTick,
                CaptureRuntimeSectionFingerprint(destination),
                StringComparison.Ordinal),
            "Second production Tick mutated the same-ID restored authority graph.");
        RequireAuthorityJoinState(destination, "destination after Tick");

        int stackCountBeforeReplay = destination.WorldItems.GetAllStacks().Count;
        string physicalBeforeReplay = RequireEnvelope(
            recaptured,
            PhysicalItemsSaveSection.Id).payloadJson;
        ProductionPreparedOutputExecutionResult replay =
            destination.PreparedOutput.Execute(
                destination.SingleBill,
                destination.Recipe,
                destination.FacilityHandle,
                destination.WorkerHandle);
        Require(
            replay.IsValid
            && replay.CycleOutputCompleted
            && replay.Phase == ProductionPreparedOutputPhase.Completed,
            "Completed prepared-output replay was not idempotent.");
        Require(
            destination.WorldItems.GetAllStacks().Count == stackCountBeforeReplay,
            "Completed replay minted an additional physical stack.");
        RequireExactSectionIdentity(
            physicalBeforeReplay,
            destination.PhysicalSection.Capture(),
            "physical items after completed replay");
        RequireGraphState(destination, "destination replay");
    }

    private static void VerifyPerishableCurrentFormatRoundTrip()
    {
        using RuntimeGraph source = new(
            "Perishable Source",
            7193,
            GrainPorridgeScenario);
        source.SeedOwnedCurrentFormatBill();
        ProductionBillRecord sourceRecord = source.SingleBill;
        source.PrepareWip(sourceRecord);

        ProductionPreparedOutputExecutionResult executed =
            source.PreparedOutput.Execute(
                sourceRecord,
                source.Recipe,
                source.FacilityHandle,
                source.WorkerHandle);
        Require(
            executed.IsValid
            && executed.CycleOutputCompleted
            && executed.Phase == ProductionPreparedOutputPhase.Completed,
            "Source real adapter did not complete the P15 grain-porridge output.");
        RequireGraphState(
            source,
            "perishable source initial",
            GrainPorridgeScenario.ExpectedInitialFreshnessSeconds);

        WorldItemStackSnapshot initialStack = source.WorldItems
            .GetAllStacks()
            .Single(value => string.Equals(
                value.ItemId,
                GrainPorridgeScenario.ItemId,
                StringComparison.Ordinal));
        Require(
            source.WorldItems.TrySetFoodFreshness(
                initialStack.StackId,
                AgedFreshnessSeconds,
                preserved: false,
                out string freshnessFailure),
            "Custody-safe grain-porridge freshness mutation failed: "
                + freshnessFailure);
        RequireGraphState(
            source,
            "perishable source aged",
            AgedFreshnessSeconds);

        List<DungeonSaveSectionEnvelope> sourceEnvelopes =
            source.Registry.CaptureAll();
        string sourceItems = RequireEnvelope(
            sourceEnvelopes,
            PhysicalItemsSaveSection.Id).payloadJson;
        string sourceBills = RequireEnvelope(
            sourceEnvelopes,
            ProductionBillsSaveSection.Id).payloadJson;
        string sourceRouting = RequireEnvelope(
            sourceEnvelopes,
            ProductionPreparedOutputRoutingSaveSection.Id).payloadJson;
        string sourceWorld = RequireEnvelope(
            sourceEnvelopes,
            ModularFacilityWorldSaveSection.Id).payloadJson;
        Require(
            JsonUtility.FromJson<DungeonProductionBillSaveData>(sourceBills)
                ?.version == DungeonProductionBillSaveData.CurrentVersion
            && DungeonProductionBillSaveData.CurrentVersion == 22,
            "Perishable production owner is not a current-format V22 payload.");

        using RuntimeGraph destination = new(
            "Perishable Destination",
            7193,
            GrainPorridgeScenario);
        BuildableObject staleDestinationFacility = destination.Facility;
        int staleDestructionEvents = 0;
        staleDestinationFacility.OnBuildingDestroyed += () =>
            staleDestructionEvents++;
        DungeonGameRestoreReport report = new();
        Require(
            destination.Registry.RestoreAll(sourceEnvelopes, report)
            && report.Success,
            "Perishable full registry restore failed: "
                + string.Join(" | ", report.Errors));
        Require(
            !destination.LifecycleRestoreCandidates.IsCandidateActive
            && destination.LifecycleRestoreCandidates.PublishedSourceCount == 0,
            "Successful perishable restore retained normalized candidate references.");

        BuildableObject replacementFacility = destination.CurrentFacility;
        Require(
            replacementFacility != null
            && !ReferenceEquals(replacementFacility, staleDestinationFacility)
            && string.Equals(
                replacementFacility.PersistentInstanceId.Value,
                staleDestinationFacility.PersistentInstanceId.Value,
                StringComparison.Ordinal)
            && staleDestinationFacility.IsGridDestroyed
            && staleDestructionEvents == 0,
            "Perishable same-ID facility replacement did not retire the stale object atomically.");

        RequireGraphState(
            destination,
            "perishable destination",
            AgedFreshnessSeconds);
        List<DungeonSaveSectionEnvelope> recaptured =
            destination.Registry.CaptureAll();
        RequireExactSectionIdentity(
            sourceWorld,
            RequireEnvelope(
                recaptured,
                ModularFacilityWorldSaveSection.Id).payloadJson,
            "perishable facility world");
        RequireExactSectionIdentity(
            sourceItems,
            RequireEnvelope(
                recaptured,
                PhysicalItemsSaveSection.Id).payloadJson,
            "perishable physical items");
        RequireExactSectionIdentity(
            sourceBills,
            RequireEnvelope(
                recaptured,
                ProductionBillsSaveSection.Id).payloadJson,
            "perishable production bills");
        RequireExactSectionIdentity(
            sourceRouting,
            RequireEnvelope(
                recaptured,
                ProductionPreparedOutputRoutingSaveSection.Id).payloadJson,
            "perishable prepared-output routing");

        int stackCountBeforeReplay = destination.WorldItems
            .GetAllStacks()
            .Count;
        int quantityBeforeReplay = destination.WorldItems
            .GetAllStacks()
            .Sum(value => value.Quantity);
        string physicalBeforeReplay = RequireEnvelope(
            recaptured,
            PhysicalItemsSaveSection.Id).payloadJson;
        ProductionPreparedOutputExecutionResult replay =
            destination.PreparedOutput.Execute(
                destination.SingleBill,
                destination.Recipe,
                destination.FacilityHandle,
                destination.WorkerHandle);
        Require(
            replay.IsValid
            && replay.CycleOutputCompleted
            && replay.Phase == ProductionPreparedOutputPhase.Completed,
            "Completed perishable prepared-output replay was not idempotent.");
        Require(
            destination.WorldItems.GetAllStacks().Count == stackCountBeforeReplay
            && destination.WorldItems.GetAllStacks().Sum(value => value.Quantity)
                == quantityBeforeReplay,
            "Completed perishable replay minted physical quantity or stacks.");
        RequireExactSectionIdentity(
            physicalBeforeReplay,
            destination.PhysicalSection.Capture(),
            "perishable physical items after completed replay");
        RequireGraphState(
            destination,
            "perishable destination replay",
            AgedFreshnessSeconds);
    }

    private static string CaptureRuntimeSectionFingerprint(RuntimeGraph graph)
    {
        List<DungeonSaveSectionEnvelope> captured = graph.Registry.CaptureAll();
        return RequireEnvelope(captured, PhysicalItemsSaveSection.Id).payloadJson
            + "\n" + RequireEnvelope(
                captured,
                ProductionBillsSaveSection.Id).payloadJson
            + "\n" + RequireEnvelope(
                captured,
                ProductionPreparedOutputRoutingSaveSection.Id).payloadJson;
    }

    private static void VerifyStockSensorExactAdmissionAndRetry(
        RuntimeGraph graph)
    {
        ProductionFacilityHandle facility = graph.FacilityHandle;
        string itemId = facility.StockSensorInstallationItemId;
        Require(!string.IsNullOrEmpty(itemId),
            "P03 fixture does not expose a stock-sensor capability.");
        string destinationId = ProductionStockSensorRuntime.BuildDestinationId(
            FacilityId);
        long onePanelMass = graph.Mass.GetQuantityMass(
            (ItemDefinitionId)itemId,
            PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)itemId),
            1).Value;
        FacilityBufferMassCapacitySnapshot capacity = default;
        Require(graph.StockSensors.TryReconcileDestinationAuthorities(
                out string reconcileFailure)
            && graph.Claims.TryGetClaim(
                destinationId,
                facility.Position,
                out FacilityBufferDestinationClaim claim)
            && graph.Admission.TryGetCapacity(
                destinationId,
                facility.Position,
                out capacity)
            && capacity.Profile.MaxMassGrams == onePanelMass
            && capacity.ReservedMassGrams == 0L
            && string.Equals(
                claim.OwnerDomain,
                ProductionStockSensorDestinationAuthorityRuntime.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerOperationId,
                destinationId,
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerFacilityId,
                FacilityId,
                StringComparison.Ordinal),
            "Stock-sensor exact one-panel authority is invalid: "
                + reconcileFailure);

        const string firstTransitOwner = "qa:sensor-transit:first";
        Require(graph.Spawner.Spawn(
                itemId,
                1,
                facility.Position,
                WorldItemStackState.InTransit,
                firstTransitOwner) == 1,
            "First stock-sensor transit panel did not spawn.");
        WorldItemStackSnapshot first = graph.WorldItems.GetAllStacks().Single(value =>
            value != null
            && value.State == WorldItemStackState.InTransit
            && string.Equals(
                value.DestinationId,
                firstTransitOwner,
                StringComparison.Ordinal));
        Require(graph.Transfers.TryCompleteTransitToFacilityBuffer(
                (ItemStackId)first.StackId,
                firstTransitOwner,
                facility.Position,
                destinationId,
                out FacilityBufferMassAdmissionReceipt firstReceipt,
                out DomainFailure firstFailure)
            && firstReceipt.CommittedMassGrams == onePanelMass
            && graph.Occupancy.Capture(destinationId).TotalMassGrams
                == onePanelMass,
            "First stock-sensor panel did not enter its exact gram socket: "
                + firstFailure.Code);
        ProductionFacilityHandle movedWhileOccupied = new(
            facility.RuntimeObject,
            facility.InstanceId,
            facility.Position + Vector2Int.right,
            facility.IsDestroyed,
            facility.StockSensorInstallationItemId,
            facility.AllowsOverflowDump,
            facility.OverflowOffset,
            facility.DefinitionId,
            facility.WorkstationTag,
            facility.OutputBufferCycleCapacity,
            facility.ProcessFluidProfile);
        Require(!graph.SensorAuthority.TryEnsure(
                movedWhileOccupied,
                out _,
                out string occupiedUpdateFailure)
            && occupiedUpdateFailure.Contains(
                "authority-update-not-empty",
                StringComparison.Ordinal),
            "A live sensor lot allowed same-ID authority anchor mutation.");

        const string secondTransitOwner = "qa:sensor-transit:second";
        Require(graph.Spawner.Spawn(
                itemId,
                1,
                facility.Position,
                WorldItemStackState.InTransit,
                secondTransitOwner) == 1,
            "Second stock-sensor transit panel did not spawn.");
        WorldItemStackSnapshot second = graph.WorldItems.GetAllStacks().Single(value =>
            value != null
            && value.State == WorldItemStackState.InTransit
            && string.Equals(
                value.DestinationId,
                secondTransitOwner,
                StringComparison.Ordinal));
        Require(!graph.Transfers.TryCompleteTransitToFacilityBuffer(
                (ItemStackId)second.StackId,
                secondTransitOwner,
                facility.Position,
                destinationId,
                out _,
                out DomainFailure fullFailure)
            && fullFailure.Code == FailureCode.ConveyorPortFull
            && graph.WorldItems.GetAllStacks().SingleOrDefault(value =>
                value != null
                && string.Equals(
                    value.StackId,
                    second.StackId,
                    StringComparison.Ordinal)) is WorldItemStackSnapshot retained
            && retained.State == WorldItemStackState.InTransit
            && string.Equals(
                retained.DestinationId,
                secondTransitOwner,
                StringComparison.Ordinal)
            && graph.Admission.TryGetCapacity(
                destinationId,
                facility.Position,
                out capacity)
            && capacity.ReservedMassGrams == 0L,
            "A full stock-sensor socket did not retain the same exact transit lot.");

        graph.StockSensors.FinalizeDeliveredSensors();
        Require(graph.StockSensors.Has(facility)
            && graph.Occupancy.Capture(destinationId).TotalMassGrams == 0L,
            "Installed sensor was not exact-once consumed from its socket.");
        Require(graph.Transfers.TryCompleteTransitToFacilityBuffer(
                (ItemStackId)second.StackId,
                secondTransitOwner,
                facility.Position,
                destinationId,
                out FacilityBufferMassAdmissionReceipt retryReceipt,
                out DomainFailure retryFailure)
            && retryReceipt.CommittedMassGrams == onePanelMass
            && graph.Occupancy.Capture(destinationId).TotalMassGrams
                == onePanelMass,
            "The same retained sensor lot did not retry after capacity cleared: "
                + retryFailure.Code);
        Require(graph.Items.ReleaseDestination(
                destinationId,
                facility.Position) == 1
            && graph.Occupancy.Capture(destinationId).TotalMassGrams == 0L,
            "Sensor retry fixture did not conservatively release its extra panel.");
        Require(graph.WorldItems.TryCommitBatchPhysicalDisposition(
                new[]
                {
                    new PhysicalItemTransformInput(second.StackId, 1)
                },
                PhysicalItemDispositionKind.Sink,
                "qa:stock-sensor-extra-panel-cleanup",
                "qa.stock-sensor-extra-panel-cleanup",
                out PhysicalItemBatchDispositionReceipt cleanupReceipt,
                out string cleanupFailure)
            && cleanupReceipt.IsCommitted
            && cleanupReceipt.InputMassGrams == onePanelMass,
            "Sensor retry fixture did not close its extra panel with a typed sink: "
                + cleanupFailure);
    }

    private static void RequireAuthorityJoinState(RuntimeGraph graph, string stage)
    {
        DungeonProductionBillSaveData bills = graph.Production.Capture();
        ProductionPreparedOutputBatchSaveData batch =
            bills.bills.Single().preparedOutput;
        ProductionFacilityHandle facility = graph.FacilityHandle;
        Require(
            graph.Claims.TryGetClaim(
                batch.destinationId,
                facility.Position,
                out FacilityBufferDestinationClaim claim)
            && string.Equals(
                claim.OwnerFacilityId,
                FacilityId,
                StringComparison.Ordinal),
            stage + " destination claim did not join the replacement facility.");
        Require(
            graph.Admission.TryGetCapacity(
                batch.destinationId,
                facility.Position,
                out FacilityBufferMassCapacitySnapshot capacity)
            && capacity.Profile.MaxMassGrams == ExpectedCapacityGrams,
            stage + " capacity profile did not join the replacement facility.");
        string sensorDestinationId =
            ProductionStockSensorRuntime.BuildDestinationId(FacilityId);
        long sensorMassGrams = graph.Mass.GetQuantityMass(
            (ItemDefinitionId)facility.StockSensorInstallationItemId,
            PhysicalItemMassSubject.ForDefinition(
                (ItemDefinitionId)facility.StockSensorInstallationItemId),
            1).Value;
        Require(graph.Claims.TryGetClaim(
                sensorDestinationId,
                facility.Position,
                out FacilityBufferDestinationClaim sensorClaim)
            && graph.Admission.TryGetCapacity(
                sensorDestinationId,
                facility.Position,
                out FacilityBufferMassCapacitySnapshot sensorCapacity)
            && sensorCapacity.Profile.MaxMassGrams == sensorMassGrams
            && sensorCapacity.ReservedMassGrams == 0L
            && graph.Occupancy.Capture(sensorDestinationId).TotalMassGrams == 0L
            && string.Equals(
                sensorClaim.OwnerDomain,
                ProductionStockSensorDestinationAuthorityRuntime.OwnerDomain,
                StringComparison.Ordinal)
            && graph.StockSensors.Has(facility),
            stage + " derived stock-sensor authority/state did not join the replacement facility.");
        Require(
            graph.Routing.CaptureDestination(batch.destinationId).Count == 1
            && graph.Occupancy.Capture(batch.destinationId).TotalMassGrams
                == ExpectedBatchMassGrams,
            stage + " routing or physical occupancy did not join the replacement facility.");
        ProductionOutputDestinationLifecycleSnapshot lifecycle =
            graph.Lifecycle.Capture((BuildingInstanceId)FacilityId);
        Require(
            lifecycle.HasAnyAuthority
            && lifecycle.OwnedMassGrams >= ExpectedBatchMassGrams
            && lifecycle.Blocks.Count > 0,
            stage + " lifecycle aggregate did not retain its owned output graph.");
    }

    private static void RequireGraphState(
        RuntimeGraph graph,
        string stage,
        double? expectedFreshnessSeconds = null)
    {
        ScenarioConfig config = graph.Config;
        DungeonProductionBillSaveData bills = graph.Production.Capture();
        Require(
            bills.version == DungeonProductionBillSaveData.CurrentVersion
            && bills.bills.Count == 1,
            stage + " production owner count/version drifted.");
        ProductionPreparedOutputBatchSaveData batch =
            bills.bills[0].preparedOutput;
        Require(
            batch != null
            && batch.schemaVersion ==
                ProductionPreparedOutputBatchSaveData.CurrentSchemaVersion
            && batch.phase == ProductionPreparedOutputPhase.Completed
            && batch.totalPhysicalMassGrams == config.ExpectedBatchMassGrams
            && batch.outputBufferCycleCapacity ==
                config.ExpectedOutputBufferCycleCapacity
            && batch.projectedPortfolioCapacityGrams ==
                config.ExpectedCapacityGrams
            && batch.requiredMinimumCapacityGrams ==
                config.ExpectedCapacityGrams
            && IsSha256(batch.recipeDefinitionDigest)
            && IsSha256(batch.migrationProfileDigest)
            && IsSha256(batch.capacitySourceDigest)
            && IsSha256(batch.outcomeFingerprint)
            && IsSha256(batch.admissionFingerprint),
            stage + " prepared-output digest/mass/capacity contract drifted.");
        ProductionPreparedOutputLineSaveData line = batch.lines.Single(value =>
            value != null
            && string.Equals(
                value.itemId,
                config.ItemId,
                StringComparison.Ordinal));
        Require(
            string.Equals(line.itemId, config.ItemId, StringComparison.Ordinal)
            && line.quantity == config.ExpectedOutputQuantity
            && line.exactMassGrams == config.ExpectedBatchMassGrams
            && IsSha256(line.componentFingerprint),
            stage + " exact output line drifted.");

        WorldItemStackSnapshot stack = graph.WorldItems.GetAllStacks()
            .Single(value => string.Equals(
                value.ItemId,
                config.ItemId,
                StringComparison.Ordinal));
        Require(
            string.Equals(stack.ItemId, config.ItemId, StringComparison.Ordinal)
            && stack.Quantity == config.ExpectedOutputQuantity
            && stack.State == WorldItemStackState.FacilityOutputBuffer
            && string.Equals(
                stack.DestinationId,
                batch.destinationId,
                StringComparison.Ordinal)
            && stack.Components.Count(component => component != null
                && string.Equals(
                    component.componentTypeId,
                    "item-state:facility-buffer-planned-output-provenance",
                    StringComparison.Ordinal)) == 1,
            stage + " physical stack/provenance drifted.");
        if (expectedFreshnessSeconds.HasValue)
        {
            Require(
                FoodFreshnessComponentCodec.TryRead(
                    stack.Components,
                    out double remainingSeconds,
                    out bool preserved)
                && remainingSeconds == expectedFreshnessSeconds.Value
                && !preserved,
                stage + " perishable freshness drifted.");
        }
        Require(
            graph.Mass.GetQuantityMass(
                (ItemDefinitionId)config.ItemId,
                PhysicalItemMassSubjectAdapter.Create(
                    graph.Mass,
                    (ItemDefinitionId)config.ItemId,
                    stack.ItemInstanceId,
                    stack.Components),
                stack.Quantity).Value == config.ExpectedBatchMassGrams,
            stage + " physical stack mass drifted.");

        ProductionPreparedOutputRoutingLineSnapshot routed =
            graph.Routing.CaptureBill(
                    (ProductionBillId)bills.bills[0].billId)
                .Single(value => string.Equals(
                    value.ItemId,
                    config.ItemId,
                    StringComparison.Ordinal));
        Require(
            string.Equals(routed.ItemId, config.ItemId, StringComparison.Ordinal)
            && routed.OriginalQuantity == config.ExpectedOutputQuantity
            && routed.RemainingQuantity == config.ExpectedOutputQuantity
            && routed.OriginalMassGrams == config.ExpectedBatchMassGrams
            && routed.RemainingMassGrams == config.ExpectedBatchMassGrams,
            stage + " routing aggregate drifted.");
        Require(
            graph.Admission.TryGetCapacity(
                batch.destinationId,
                graph.FacilityHandle.Position,
                out FacilityBufferMassCapacitySnapshot capacity)
            && capacity.Profile.MaxMassGrams == config.ExpectedCapacityGrams
            && capacity.ReservedMassGrams == 0L,
            stage + " output capacity authority drifted.");
    }

    private static void RequireExactSectionIdentity(
        string expected,
        string actual,
        string section)
    {
        Require(
            string.Equals(expected, actual, StringComparison.Ordinal),
            section + " JSON changed across capture/restore/recapture.");
    }

    private static DungeonSaveSectionEnvelope RequireEnvelope(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string id) => (envelopes ?? Array.Empty<DungeonSaveSectionEnvelope>())
        .Single(value => value != null
            && string.Equals(value.sectionId, id, StringComparison.Ordinal));

    private static DungeonProductionGenericBillTerminalDrainSaveData
        EmptyGenericTerminalPayload() => new()
        {
            version = DungeonProductionGenericBillTerminalDrainSaveData
                .CurrentVersion,
            entries = new List<ProductionGenericBillTerminalDrainSaveData>()
        };

    private static string CaptureDetachedLifecycleAggregate(
        RuntimeGraph graph,
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes)
    {
        ModularFacilityWorldSaveData world = JsonUtility.FromJson<ModularFacilityWorldSaveData>(
            RequireEnvelope(envelopes, ModularFacilityWorldSaveSection.Id).payloadJson);
        DungeonProductionBillSaveData production = JsonUtility.FromJson<DungeonProductionBillSaveData>(
            RequireEnvelope(envelopes, ProductionBillsSaveSection.Id).payloadJson);
        DungeonSaveSectionEnvelope genericTerminalEnvelope = envelopes
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.sectionId,
                    ProductionGenericBillTerminalDrainSaveSection.Id,
                    StringComparison.Ordinal));
        DungeonProductionGenericBillTerminalDrainSaveData genericTerminal =
            genericTerminalEnvelope == null
                ? EmptyGenericTerminalPayload()
                : JsonUtility.FromJson<
                    DungeonProductionGenericBillTerminalDrainSaveData>(
                    genericTerminalEnvelope.payloadJson);
        DungeonPhysicalItemSaveData physical = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
            RequireEnvelope(envelopes, PhysicalItemsSaveSection.Id).payloadJson);
        ProductionPreparedOutputRoutingSaveData routing =
            JsonUtility.FromJson<ProductionPreparedOutputRoutingSaveData>(
                RequireEnvelope(
                    envelopes,
                    ProductionPreparedOutputRoutingSaveSection.Id).payloadJson);
        return ProductionOutputDestinationDurableSaveProjector.ProjectAggregateFromSave(
            (BuildingInstanceId)FacilityId,
            world,
            production,
            genericTerminal,
            new DungeonCombatEquipmentSaveData
            {
                craftOrders = new List<CombatEquipmentCraftOrderSaveData>()
            },
            new CombatEquipmentMaintenanceSaveData(),
            new DungeonCharacterEnvironmentSaveData
            {
                apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
                apparelWorkOrderTerminalStates =
                    Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
            },
            physical,
            new DungeonCharacterWorldSaveData
            {
                actors = new List<DungeonCharacterSaveData>()
            },
            routing,
            graph.BuildingDefinitions,
            graph.CapacityProjector,
            graph.Mass);
    }

    private static void VerifyDestructiveDrainCrossAggregatePreflight(
        RuntimeGraph graph,
        IReadOnlyList<DungeonSaveSectionEnvelope> sourceEnvelopes,
        string expectedLifecycleFingerprint)
    {
        ProductionFacilityDestructiveDrainCrossAggregateSaveValidation validator =
            new(
                graph.BuildingDefinitions,
                graph.CapacityProjector,
                graph.Mass,
                new ProductionGenericBillTerminalDrainSaveValidation(),
                new CombatEquipmentTerminalDrainSaveValidation(),
                new ProductionApparelOrderTerminalDrainSaveValidation());
        BuildingInstanceId destructiveFacilityId =
            (BuildingInstanceId)FacilityId;
        ProductionFacilityDestructiveDrainOperationId destructiveOperationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                destructiveFacilityId);
        ModularFacilityWorldSaveData sourceWorld =
            JsonUtility.FromJson<ModularFacilityWorldSaveData>(
                RequireEnvelope(
                    sourceEnvelopes,
                    ModularFacilityWorldSaveSection.Id).payloadJson);
        DungeonProductionBillSaveData sourceProduction =
            JsonUtility.FromJson<DungeonProductionBillSaveData>(
                RequireEnvelope(
                    sourceEnvelopes,
                    ProductionBillsSaveSection.Id).payloadJson);
        DungeonPhysicalItemSaveData sourceItems =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                RequireEnvelope(
                    sourceEnvelopes,
                    PhysicalItemsSaveSection.Id).payloadJson);
        ProductionPreparedOutputRoutingSaveData sourceRouting =
            JsonUtility.FromJson<ProductionPreparedOutputRoutingSaveData>(
                RequireEnvelope(
                    sourceEnvelopes,
                    ProductionPreparedOutputRoutingSaveSection.Id).payloadJson);
        DungeonCharacterWorldSaveData sourceCharacters = new()
        {
            actors = new List<DungeonCharacterSaveData>()
        };
        DungeonCombatEquipmentSaveData sourceCombat = new()
        {
            craftOrders = new List<CombatEquipmentCraftOrderSaveData>()
        };
        CombatEquipmentMaintenanceSaveData sourceMaintenance = new();
        DungeonCharacterEnvironmentSaveData sourceEnvironment = new()
        {
            apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
            apparelWorkOrderTerminalStates =
                Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
        };
        IReadOnlyDictionary<string, string> preparedContributors =
            BuildContributorFingerprints(
                graph,
                destructiveFacilityId,
                sourceWorld,
                sourceProduction,
                sourceCombat,
                sourceMaintenance,
                sourceEnvironment,
                sourceItems,
                sourceCharacters,
                sourceRouting,
                worldRemoved: false);
        List<ProductionFacilityDestructiveDrainParticipantSaveData>
            preparedParticipants =
                ProductionFacilityDestructiveDrainParticipantRegistryDebugScenarios
                    .CreateSaveParticipants(destructiveOperationId);
        ApplyContributorFingerprints(
            preparedParticipants,
            preparedContributors,
            updatePrepared: true);
        ApplyPlannedOwners(
            preparedParticipants,
            ProductionFacilityDestructiveDrainPlannedOwnerSaveProjection
                .Project(
                    destructiveFacilityId,
                    sourceProduction,
                    sourceCombat,
                    sourceMaintenance,
                    sourceEnvironment,
                    sourceItems,
                    sourceCharacters,
                    sourceRouting),
            destructiveOperationId);
        ProductionFacilityDestructiveDrainParticipantSaveData physicalParticipant =
            preparedParticipants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery,
                StringComparison.Ordinal));
        DungeonPhysicalItemSaveData multiStackProjection =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(sourceItems));
        WorldItemStackSaveData originStack = multiStackProjection.stacks
            .First(value => value != null
                && value.quantity > 0
                && value.state == WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    value.destinationId,
                    ProductionOutputDestinationId
                        .FromFacility(destructiveFacilityId).Value,
                    StringComparison.Ordinal));
        WorldItemStackSaveData duplicateOrigin =
            JsonUtility.FromJson<WorldItemStackSaveData>(
                JsonUtility.ToJson(originStack));
        duplicateOrigin.stackId += ":second-owner-proof";
        multiStackProjection.stacks.Add(duplicateOrigin);
        IReadOnlyList<string> compositePhysicalOwners =
            ProductionFacilityDestructiveDrainPlannedOwnerSaveProjection
                .Project(
                    destructiveFacilityId,
                    sourceProduction,
                    sourceCombat,
                    sourceMaintenance,
                    sourceEnvironment,
                    multiStackProjection,
                    sourceCharacters,
                    sourceRouting)[ProductionFacilityDestructiveDrainParticipantIds
                        .PhysicalCustodyCarryRecovery];
        Require(physicalParticipant.owners.Count == 1
                && compositePhysicalOwners.Count == 1
                && string.Equals(
                    physicalParticipant.owners[0].ownerStableId,
                    ProductionFacilityDestructiveDrainOwnerStableIds
                        .PhysicalDestination(
                            ProductionOutputDestinationId
                                .FromFacility(destructiveFacilityId).Value),
                    StringComparison.Ordinal),
            "Physical destructive-drain stacks were not collapsed into one atomic destination owner.");
        DungeonProductionFacilityDestructiveDrainSaveData drain = new()
        {
            registryFingerprint =
                ProductionFacilityDestructiveDrainParticipantRegistry
                    .ExpectedRegistryFingerprint,
            entries = new List<ProductionFacilityDestructiveDrainEntrySaveData>
            {
                new()
                {
                    operationId =
                        ProductionFacilityDestructiveDrainOperationId
                            .FromFacility((BuildingInstanceId)FacilityId).Value,
                    initiatingMutationOperationId =
                        ProductionFacilityDestructiveDrainCanonical
                            .BuildInitiatingMutationOperationId(
                                ProductionFacilityDestructiveDrainCause
                                    .StructuralIntegrity,
                                (BuildingInstanceId)FacilityId),
                    cause = ProductionFacilityDestructiveDrainCause
                        .StructuralIntegrity,
                    facilityId = FacilityId,
                    destinationId = ProductionOutputDestinationId
                        .FromFacility((BuildingInstanceId)FacilityId).Value,
                    phase = ProductionFacilityDestructiveDrainPhase.Prepared,
                    preparedLifecycleFingerprint = expectedLifecycleFingerprint,
                    expectedCurrentLifecycleFingerprint =
                        expectedLifecycleFingerprint,
                    revision = 1L,
                    participants = preparedParticipants
                }
            }
        };
        List<DungeonSaveSectionEnvelope> complete = sourceEnvelopes
            .Where(value => value != null
                && !string.Equals(
                    value.sectionId,
                    CharacterWorldSaveSection.Id,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.sectionId,
                    CombatEquipmentSaveSection.Id,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.sectionId,
                    EquipmentMaintenanceSaveSection.Id,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.sectionId,
                    CharacterEnvironmentSaveSection.Id,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.sectionId,
                    ProductionGenericBillTerminalDrainSaveSection.Id,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.sectionId,
                    CombatEquipmentTerminalDrainSaveSection.Id,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.sectionId,
                    ProductionApparelOrderTerminalDrainSaveSection.Id,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.sectionId,
                    ProductionFacilityDestructiveDrainSaveSection.Id,
                    StringComparison.Ordinal))
            .Select(CloneEnvelope)
            .ToList();
        complete.Add(Envelope(
            CharacterWorldSaveSection.Id,
            new DungeonCharacterWorldSaveData
            {
                actors = new List<DungeonCharacterSaveData>()
            }));
        complete.Add(Envelope(
            CombatEquipmentSaveSection.Id,
            new DungeonCombatEquipmentSaveData
            {
                craftOrders = new List<CombatEquipmentCraftOrderSaveData>()
            }));
        complete.Add(Envelope(
            EquipmentMaintenanceSaveSection.Id,
            sourceMaintenance));
        complete.Add(Envelope(
            CharacterEnvironmentSaveSection.Id,
            new DungeonCharacterEnvironmentSaveData
            {
                apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
                apparelWorkOrderTerminalStates =
                    Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
            }));
        DungeonProductionGenericBillTerminalDrainSaveData
            emptyGenericTerminalDrains = new()
            {
                version =
                    DungeonProductionGenericBillTerminalDrainSaveData
                        .CurrentVersion,
                entries = new List<
                    ProductionGenericBillTerminalDrainSaveData>()
            };
        complete.Add(Envelope(
            ProductionGenericBillTerminalDrainSaveSection.Id,
            emptyGenericTerminalDrains));
        complete.Add(Envelope(
            CombatEquipmentTerminalDrainSaveSection.Id,
            new DungeonCombatEquipmentTerminalDrainSaveData
            {
                version =
                    DungeonCombatEquipmentTerminalDrainSaveData.CurrentVersion,
                entries = new List<CombatEquipmentTerminalDrainSaveData>()
            }));
        complete.Add(Envelope(
            ProductionApparelOrderTerminalDrainSaveSection.Id,
            new DungeonProductionApparelOrderTerminalDrainSaveData
            {
                version =
                    DungeonProductionApparelOrderTerminalDrainSaveData
                        .CurrentVersion,
                entries = new List<
                    ProductionApparelOrderTerminalDrainSaveData>()
            }));
        complete.Add(Envelope(
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain));
        complete.Add(Envelope(
            WorkOrdersSaveSection.Id,
            new DungeonWorkOrderSaveData
            {
                version = DungeonWorkOrderSaveData.CurrentVersion,
                nextOrderSequence = 1,
                orders = new List<WorkOrderSaveData>(),
                qualityPipelines = new List<QualityTargetPipelineSaveData>()
            }));

        DungeonGameRestoreReport valid = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            valid);
        Require(valid.Success,
            "Exact destructive-drain cross-aggregate preflight failed: "
            + string.Join(" | ", valid.Errors));

        CombatEquipmentMaintenanceSaveData repairMaintenance = new()
        {
            orders = new List<CombatEquipmentRepairOrder>
            {
                new()
                {
                    orderId = "equipment-repair:qa:combined-owner",
                    equipmentInstanceId = "equipment:qa:combined-owner",
                    facilityBuildingId = destructiveFacilityId.Value,
                    state = CombatEquipmentRepairOrderState.InProgress,
                    requiredWork = 10f,
                    completedWork = 3f
                }
            }
        };
        ReplacePayload(
            complete,
            EquipmentMaintenanceSaveSection.Id,
            repairMaintenance);
        IReadOnlyDictionary<string, string> repairContributors =
            BuildContributorFingerprints(
                graph,
                destructiveFacilityId,
                sourceWorld,
                sourceProduction,
                sourceCombat,
                repairMaintenance,
                sourceEnvironment,
                sourceItems,
                sourceCharacters,
                sourceRouting,
                worldRemoved: false);
        ApplyContributorFingerprints(
            preparedParticipants,
            repairContributors,
            updatePrepared: true);
        ApplyPlannedOwners(
            preparedParticipants,
            ProductionFacilityDestructiveDrainPlannedOwnerSaveProjection.Project(
                destructiveFacilityId,
                sourceProduction,
                sourceCombat,
                repairMaintenance,
                sourceEnvironment,
                sourceItems,
                sourceCharacters,
                sourceRouting),
            destructiveOperationId);
        string repairLifecycle = ProductionOutputDestinationDurableSaveProjector
            .ComposeAggregate(destructiveFacilityId, repairContributors);
        drain.entries[0].preparedLifecycleFingerprint = repairLifecycle;
        drain.entries[0].expectedCurrentLifecycleFingerprint = repairLifecycle;
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport combinedRepairValid = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            combinedRepairValid);
        Require(combinedRepairValid.Success,
            "A current-format repair owner failed combined combat validation: "
            + string.Join(" | ", combinedRepairValid.Errors));

        ProductionFacilityDestructiveDrainParticipantSaveData combatParticipant =
            preparedParticipants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting,
                StringComparison.Ordinal));
        ProductionFacilityDestructiveDrainOwnerSaveData repairOwner =
            combatParticipant.owners.Single(value => string.Equals(
                value.ownerStableId,
                ProductionFacilityDestructiveDrainOwnerStableIds
                    .EquipmentRepairOrder(repairMaintenance.orders[0].orderId),
                StringComparison.Ordinal));
        combatParticipant.owners.Remove(repairOwner);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport missingRepairOwner = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            missingRepairOwner);
        Require(!missingRepairOwner.Success
                && missingRepairOwner.Errors.Any(value => value.Contains(
                    "prepared-owner-source-bijection-mismatch",
                    StringComparison.Ordinal)),
            "A source-only repair owner passed prepared owner validation.");
        combatParticipant.owners.Add(repairOwner);
        combatParticipant.owners = combatParticipant.owners
            .OrderBy(value => value.ownerStableId, StringComparer.Ordinal)
            .ToList();

        CombatEquipmentMaintenanceSaveData duplicateRepair =
            JsonUtility.FromJson<CombatEquipmentMaintenanceSaveData>(
                JsonUtility.ToJson(repairMaintenance));
        duplicateRepair.orders.Add(repairMaintenance.orders[0].Clone());
        ReplacePayload(
            complete,
            EquipmentMaintenanceSaveSection.Id,
            duplicateRepair);
        DungeonGameRestoreReport duplicateRepairOwner = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            duplicateRepairOwner);
        Require(!duplicateRepairOwner.Success
                && duplicateRepairOwner.Errors.Any(value => value.Contains(
                    "Duplicate equipment maintenance repair order identity",
                    StringComparison.Ordinal)),
            "Duplicate repair owner identity passed combined combat projection.");

        CombatEquipmentMaintenanceSaveData staleRepair =
            JsonUtility.FromJson<CombatEquipmentMaintenanceSaveData>(
                JsonUtility.ToJson(repairMaintenance));
        staleRepair.orders[0].orderId += ":stale";
        ReplacePayload(
            complete,
            EquipmentMaintenanceSaveSection.Id,
            staleRepair);
        IReadOnlyDictionary<string, string> staleRepairContributors =
            BuildContributorFingerprints(
                graph,
                destructiveFacilityId,
                sourceWorld,
                sourceProduction,
                sourceCombat,
                staleRepair,
                sourceEnvironment,
                sourceItems,
                sourceCharacters,
                sourceRouting,
                worldRemoved: false);
        ApplyContributorFingerprints(
            preparedParticipants,
            staleRepairContributors,
            updatePrepared: true);
        string staleRepairLifecycle =
            ProductionOutputDestinationDurableSaveProjector.ComposeAggregate(
                destructiveFacilityId,
                staleRepairContributors);
        drain.entries[0].preparedLifecycleFingerprint = staleRepairLifecycle;
        drain.entries[0].expectedCurrentLifecycleFingerprint = staleRepairLifecycle;
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport staleRepairOwner = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            staleRepairOwner);
        Require(!staleRepairOwner.Success
                && staleRepairOwner.Errors.Any(value => value.Contains(
                    "prepared-owner-source-bijection-mismatch",
                    StringComparison.Ordinal)),
            "A stale repair journal owner passed combined combat validation.");

        ReplacePayload(
            complete,
            EquipmentMaintenanceSaveSection.Id,
            sourceMaintenance);
        ApplyContributorFingerprints(
            preparedParticipants,
            preparedContributors,
            updatePrepared: true);
        ApplyPlannedOwners(
            preparedParticipants,
            ProductionFacilityDestructiveDrainPlannedOwnerSaveProjection.Project(
                destructiveFacilityId,
                sourceProduction,
                sourceCombat,
                sourceMaintenance,
                sourceEnvironment,
                sourceItems,
                sourceCharacters,
                sourceRouting),
            destructiveOperationId);
        drain.entries[0].preparedLifecycleFingerprint = expectedLifecycleFingerprint;
        drain.entries[0].expectedCurrentLifecycleFingerprint =
            expectedLifecycleFingerprint;
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);

        Dictionary<string, DungeonSaveSectionEnvelope> registryPayload =
            complete.ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        DungeonGameRestoreReport registryValid = new();
        validator.Validate(registryPayload, registryValid);
        Require(registryValid.Success,
            "Exact destructive-drain registry preflight failed: "
            + string.Join(" | ", registryValid.Errors));

        ModularFacilityBuildingSaveData dismantleWorldTarget =
            sourceWorld.buildings.Single(value => value != null
                && string.Equals(
                    value.persistentInstanceId,
                    destructiveFacilityId.Value,
                    StringComparison.Ordinal));
        BuildingSO dismantleDefinition = graph.BuildingDefinitions.GetBuilding(
            dismantleWorldTarget.buildingId);
        const string dismantlePipelineId =
            "quality:qa:destructive-drain-owner";
        WorkOrderSaveData dismantleOwner = new()
        {
            workOrderId = "work:000001",
            workTypeId = BuiltInWorkTypeIds.Dismantle.Value,
            targetBuildingId = dismantleWorldTarget.buildingId,
            gridX = dismantleWorldTarget.centerX,
            gridY = dismantleWorldTarget.centerY,
            requiredWork = 1f,
            completedWork = 1f,
            materialDestinationId =
                "quality-recovery:" + dismantlePipelineId,
            qualityPipelineId = dismantlePipelineId,
            qualityRoll = new CraftQualityRollSaveData(),
            status = WorkOrderStatus.Blocked,
            destructiveDrainOperationId = destructiveOperationId.Value
        };
        QualityTargetPipelineSaveData dismantlePipeline = new()
        {
            pipelineId = dismantlePipelineId,
            definitionId = string.IsNullOrEmpty(
                dismantleDefinition.ContentDefinitionId)
                ? dismantleDefinition.id.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                : dismantleDefinition.ContentDefinitionId,
            facilityPipeline = true,
            currentRoll = new CraftQualityRollSaveData(),
            stage = QualityTargetPipelineStage.Recovering,
            footprintX = dismantleWorldTarget.centerX,
            footprintY = dismantleWorldTarget.centerY,
            footprintWidth = Math.Max(1, dismantleWorldTarget.width),
            footprintHeight = Math.Max(1, dismantleWorldTarget.height)
        };
        DungeonWorkOrderSaveData workOrderOwners = new()
        {
            version = DungeonWorkOrderSaveData.CurrentVersion,
            nextOrderSequence = 2,
            orders = new List<WorkOrderSaveData> { dismantleOwner },
            qualityPipelines = new List<QualityTargetPipelineSaveData>
            {
                dismantlePipeline
            }
        };
        drain.entries[0].cause =
            ProductionFacilityDestructiveDrainCause.ExplicitDemolition;
        drain.entries[0].initiatingMutationOperationId =
            ProductionFacilityDestructiveDrainCanonical
                .BuildInitiatingMutationOperationId(
                    ProductionFacilityDestructiveDrainCause
                        .ExplicitDemolition,
                    destructiveFacilityId);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);

        DungeonGameRestoreReport exactWorkOrderJoin = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            exactWorkOrderJoin);
        Require(exactWorkOrderJoin.Success,
            "Exact work-order destructive-drain join failed whole-save preflight: "
            + string.Join(" | ", exactWorkOrderJoin.Errors));
        DungeonGameRestoreReport exactWorkOrderRegistryJoin = new();
        validator.Validate(
            complete.ToDictionary(
                value => value.sectionId,
                StringComparer.Ordinal),
            exactWorkOrderRegistryJoin);
        Require(exactWorkOrderRegistryJoin.Success,
            "Exact work-order destructive-drain join failed registry preflight: "
            + string.Join(" | ", exactWorkOrderRegistryJoin.Errors));

        int exactGridX = dismantleOwner.gridX;
        dismantleOwner.gridX++;
        dismantlePipeline.footprintX++;
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameRestoreReport mismatchedWorkOrderTarget = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            mismatchedWorkOrderTarget);
        Require(!mismatchedWorkOrderTarget.Success
                && mismatchedWorkOrderTarget.Errors.Any(value =>
                    value.Contains(
                        "work-order-world-target-mismatch",
                        StringComparison.Ordinal)),
            "A work-order target different from its persistent facility passed preflight.");
        dismantleOwner.gridX = exactGridX;
        dismantlePipeline.footprintX--;

        dismantleOwner.qualityPipelineId =
            "quality:qa:destructive-drain-owner:missing";
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameRestoreReport missingWorkOrderPipeline = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            missingWorkOrderPipeline);
        Require(!missingWorkOrderPipeline.Success
                && missingWorkOrderPipeline.Errors.Any(value => value.Contains(
                    "work-order-pipeline-cardinality",
                    StringComparison.Ordinal)),
            "A destructive-drain work order without its continuation pipeline passed preflight.");
        dismantleOwner.qualityPipelineId = dismantlePipelineId;

        dismantlePipeline.footprintX++;
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameRestoreReport mismatchedWorkOrderPipeline = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            mismatchedWorkOrderPipeline);
        Require(!mismatchedWorkOrderPipeline.Success
                && mismatchedWorkOrderPipeline.Errors.Any(value =>
                    value.Contains(
                        "work-order-pipeline-identity-mismatch",
                        StringComparison.Ordinal)),
            "A destructive-drain work order joined a different pipeline footprint.");
        dismantlePipeline.footprintX--;

        string exactOperationId = dismantleOwner.destructiveDrainOperationId;
        dismantleOwner.destructiveDrainOperationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                (BuildingInstanceId)"building:qa:other-demolition").Value;
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameSaveData mismatchedWorkOrderSave = new()
        {
            sections = complete
        };
        string mismatchedBefore = JsonUtility.ToJson(mismatchedWorkOrderSave);
        DungeonGameRestoreReport mismatchedWorkOrder = new();
        validator.Validate(mismatchedWorkOrderSave, mismatchedWorkOrder);
        Require(!mismatchedWorkOrder.Success
                && mismatchedWorkOrder.Errors.Any(value => value.Contains(
                    "work-order-journal-cardinality",
                    StringComparison.Ordinal))
                && string.Equals(
                    mismatchedBefore,
                    JsonUtility.ToJson(mismatchedWorkOrderSave),
                    StringComparison.Ordinal),
            "A mismatched work-order operation passed or mutated whole-save preflight.");
        DungeonGameRestoreReport mismatchedWorkOrderRegistry = new();
        validator.Validate(
            complete.ToDictionary(
                value => value.sectionId,
                StringComparer.Ordinal),
            mismatchedWorkOrderRegistry);
        Require(!mismatchedWorkOrderRegistry.Success
                && mismatchedWorkOrderRegistry.Errors.Any(value =>
                    value.Contains(
                        "work-order-journal-cardinality",
                        StringComparison.Ordinal)),
            "A mismatched work-order operation passed registry preflight.");
        dismantleOwner.destructiveDrainOperationId = exactOperationId;

        drain.entries[0].cause =
            ProductionFacilityDestructiveDrainCause.StructuralIntegrity;
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameRestoreReport wrongCause = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            wrongCause);
        Require(!wrongCause.Success
                && wrongCause.Errors.Any(value => value.Contains(
                    "work-order-journal-identity-mismatch",
                    StringComparison.Ordinal)),
            "A dismantle work order joined a non-demolition destructive drain.");
        drain.entries[0].cause =
            ProductionFacilityDestructiveDrainCause.ExplicitDemolition;

        dismantleOwner.facilityRemovedForRetry = true;
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameRestoreReport prematureRemovalAck = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            prematureRemovalAck);
        Require(!prematureRemovalAck.Success
                && prematureRemovalAck.Errors.Any(value => value.Contains(
                    "work-order-terminal-phase-mismatch",
                    StringComparison.Ordinal)),
            "A work-order world-removal acknowledgement joined a non-terminal journal.");
        dismantleOwner.facilityRemovedForRetry = false;

        dismantleOwner.cancelRebuildAfterDestructiveDrain = true;
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameRestoreReport cancelStateMismatch = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            cancelStateMismatch);
        Require(!cancelStateMismatch.Success
                && cancelStateMismatch.Errors.Any(value => value.Contains(
                    "work-order-cancel-state-mismatch",
                    StringComparison.Ordinal)),
            "A cancelled destructive recovery retained a non-cancelled pipeline.");
        dismantlePipeline.stage = QualityTargetPipelineStage.Cancelled;
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameRestoreReport exactCancelledOwner = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            exactCancelledOwner);
        Require(exactCancelledOwner.Success,
            "A canonical pre-removal cancelled salvage owner failed preflight: "
            + string.Join(" | ", exactCancelledOwner.Errors));
        dismantleOwner.cancelRebuildAfterDestructiveDrain = false;
        dismantlePipeline.stage = QualityTargetPipelineStage.Recovering;

        WorkOrderSaveData duplicateDismantleOwner =
            JsonUtility.FromJson<WorkOrderSaveData>(
                JsonUtility.ToJson(dismantleOwner));
        duplicateDismantleOwner.workOrderId =
            "work:000002";
        workOrderOwners.orders.Add(duplicateDismantleOwner);
        workOrderOwners.nextOrderSequence = 3;
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        DungeonGameRestoreReport duplicateWorkOrderOwner = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            duplicateWorkOrderOwner);
        Require(!duplicateWorkOrderOwner.Success
                && duplicateWorkOrderOwner.Errors.Any(value => value.Contains(
                    "work-order-owner-duplicate",
                    StringComparison.Ordinal)),
            "Two dismantle work orders owned the same destructive-drain operation.");
        workOrderOwners.orders.Remove(duplicateDismantleOwner);
        workOrderOwners.nextOrderSequence = 2;

        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            workOrderOwners);
        List<DungeonSaveSectionEnvelope> workOrderWithoutJournal = complete
            .Where(value => !string.Equals(
                value.sectionId,
                ProductionFacilityDestructiveDrainSaveSection.Id,
                StringComparison.Ordinal))
            .Select(CloneEnvelope)
            .ToList();
        DungeonGameRestoreReport missingWorkOrderJournal = new();
        validator.Validate(
            new DungeonGameSaveData
            {
                sections = workOrderWithoutJournal
            },
            missingWorkOrderJournal);
        Require(!missingWorkOrderJournal.Success
                && missingWorkOrderJournal.Errors.Any(value => value.Contains(
                    "without its journal section",
                    StringComparison.Ordinal)),
            "A work-order destructive-drain owner passed without its journal.");
        DungeonGameRestoreReport missingWorkOrderRegistryJournal = new();
        validator.Validate(
            workOrderWithoutJournal.ToDictionary(
                value => value.sectionId,
                StringComparer.Ordinal),
            missingWorkOrderRegistryJournal);
        Require(!missingWorkOrderRegistryJournal.Success
                && missingWorkOrderRegistryJournal.Errors.Any(value =>
                    value.Contains(
                        "without its registry journal section",
                        StringComparison.Ordinal)),
            "A registry work-order destructive-drain owner passed without its journal.");

        drain.entries[0].cause =
            ProductionFacilityDestructiveDrainCause.StructuralIntegrity;
        drain.entries[0].initiatingMutationOperationId =
            ProductionFacilityDestructiveDrainCanonical
                .BuildInitiatingMutationOperationId(
                    ProductionFacilityDestructiveDrainCause
                        .StructuralIntegrity,
                    destructiveFacilityId);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        ReplacePayload(
            complete,
            WorkOrdersSaveSection.Id,
            new DungeonWorkOrderSaveData
            {
                version = DungeonWorkOrderSaveData.CurrentVersion,
                nextOrderSequence = 1,
                orders = new List<WorkOrderSaveData>(),
                qualityPipelines = new List<QualityTargetPipelineSaveData>()
            });

        string exactParticipantFingerprint =
            drain.entries[0].participants[0]
                .expectedCurrentContributionFingerprint;
        drain.entries[0].participants[0]
            .expectedCurrentContributionFingerprint = new string('e', 64);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport participantDrift = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            participantDrift);
        Require(!participantDrift.Success
            && participantDrift.Errors.Any(value => value.Contains(
                "contribution-mismatched participant",
                StringComparison.Ordinal)),
            "A destructive-drain participant contribution drift passed preflight.");
        drain.entries[0].participants[0]
            .expectedCurrentContributionFingerprint =
                exactParticipantFingerprint;

        ProductionFacilityDestructiveDrainParticipantSaveData removedParticipant =
            drain.entries[0].participants[^1];
        drain.entries[0].participants.RemoveAt(
            drain.entries[0].participants.Count - 1);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport missingParticipant = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            missingParticipant);
        Require(!missingParticipant.Success
            && missingParticipant.Errors.Any(value => value.Contains(
                "missing one or more required lifecycle participants",
                StringComparison.Ordinal)),
            "A destructive-drain entry missing a required participant passed preflight.");
        drain.entries[0].participants.Add(removedParticipant);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);

        ProductionFacilityDestructiveDrainParticipantSaveData ownerParticipant =
            drain.entries[0].participants.First(value =>
                value.owners.Count > 0);
        ProductionFacilityDestructiveDrainOwnerSaveData removedOwner =
            ownerParticipant.owners[0];
        ownerParticipant.owners.RemoveAt(0);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport missingOwnerSourceJoin = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            missingOwnerSourceJoin);
        Require(!missingOwnerSourceJoin.Success
            && missingOwnerSourceJoin.Errors.Any(value => value.Contains(
                "prepared-owner-source-bijection-mismatch",
                StringComparison.Ordinal)),
            "A Prepared destructive drain with a source-only owner passed preflight.");
        ownerParticipant.owners.Add(removedOwner);
        ownerParticipant.owners = ownerParticipant.owners
            .OrderBy(value => value.ownerStableId, StringComparer.Ordinal)
            .ToList();

        ProductionFacilityDestructiveDrainParticipantSaveData apparelParticipant =
            drain.entries[0].participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .ApparelWorkOrders,
                StringComparison.Ordinal));
        const string orphanOwnerId = "apparel-order:qa-orphan-owner";
        apparelParticipant.owners.Add(
            CreatePlannedOwner(
                destructiveOperationId,
                apparelParticipant.participantId,
                orphanOwnerId));
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport orphanJournalOwner = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            orphanJournalOwner);
        Require(!orphanJournalOwner.Success
            && orphanJournalOwner.Errors.Any(value => value.Contains(
                "prepared-owner-source-bijection-mismatch",
                StringComparison.Ordinal)),
            "A Prepared destructive drain with a journal-only owner passed preflight.");
        apparelParticipant.owners.RemoveAll(value => string.Equals(
            value.ownerStableId,
            orphanOwnerId,
            StringComparison.Ordinal));
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);

        DungeonGameRestoreReport absent = new();
        validator.Validate(
            new DungeonGameSaveData
            {
                sections = complete
                    .Where(value => !string.Equals(
                        value.sectionId,
                        ProductionFacilityDestructiveDrainSaveSection.Id,
                        StringComparison.Ordinal))
                    .Select(CloneEnvelope)
                    .ToList()
            },
            absent);
        Require(absent.Success,
            "A save without the unregistered destructive-drain envelope was rejected.");

        drain.entries[0].expectedCurrentLifecycleFingerprint =
            new string('f', 64);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport drifted = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            drifted);
        Require(!drifted.Success
            && drifted.Errors.Any(value => value.Contains(
                "lifecycle-fingerprint-mismatch",
                StringComparison.Ordinal)),
            "Destructive-drain fingerprint drift did not fail preflight.");

        drain.entries[0].expectedCurrentLifecycleFingerprint =
            expectedLifecycleFingerprint;
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        ModularFacilityWorldSaveData exactWorld = sourceWorld;
        ModularFacilityWorldSaveData missingWorld =
            JsonUtility.FromJson<ModularFacilityWorldSaveData>(
                JsonUtility.ToJson(exactWorld));
        missingWorld.buildings.Clear();
        ReplacePayload(
            complete,
            ModularFacilityWorldSaveSection.Id,
            missingWorld);
        DungeonGameRestoreReport missingFacility = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            missingFacility);
        Require(!missingFacility.Success,
            "A destructive drain without its pre-removal facility passed preflight.");

        ModularFacilityWorldSaveData duplicateWorld =
            JsonUtility.FromJson<ModularFacilityWorldSaveData>(
                JsonUtility.ToJson(exactWorld));
        duplicateWorld.buildings.Add(
            JsonUtility.FromJson<ModularFacilityBuildingSaveData>(
                JsonUtility.ToJson(duplicateWorld.buildings.Single())));
        ReplacePayload(
            complete,
            ModularFacilityWorldSaveSection.Id,
            duplicateWorld);
        DungeonGameRestoreReport duplicateFacility = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            duplicateFacility);
        Require(!duplicateFacility.Success,
            "A destructive drain with duplicate facility identity passed preflight.");

        ReplacePayload(
            complete,
            ModularFacilityWorldSaveSection.Id,
            missingWorld);
        DungeonProductionBillSaveData emptyProduction = new();
        DungeonPhysicalItemSaveData emptyItems = new();
        ProductionPreparedOutputRoutingSaveData emptyRouting = new();
        DungeonCombatEquipmentSaveData emptyCombat = new()
        {
            craftOrders = new List<CombatEquipmentCraftOrderSaveData>()
        };
        CombatEquipmentMaintenanceSaveData emptyMaintenance = new();
        DungeonCharacterEnvironmentSaveData emptyEnvironment = new()
        {
            apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
            apparelWorkOrderTerminalStates =
                Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
        };
        ReplacePayload(
            complete,
            ProductionBillsSaveSection.Id,
            emptyProduction);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);
        ReplacePayload(
            complete,
            ProductionPreparedOutputRoutingSaveSection.Id,
            emptyRouting);
        ReplacePayload(
            complete,
            CombatEquipmentSaveSection.Id,
            emptyCombat);
        ReplacePayload(
            complete,
            EquipmentMaintenanceSaveSection.Id,
            emptyMaintenance);
        ReplacePayload(
            complete,
            CharacterEnvironmentSaveSection.Id,
            emptyEnvironment);
        drain.entries[0].phase = ProductionFacilityDestructiveDrainPhase
            .WorldRemovedAwaitingCheckpointGc;
        IReadOnlyDictionary<string, string> absentContributors =
            BuildContributorFingerprints(
                graph,
                destructiveFacilityId,
                missingWorld,
                emptyProduction,
                emptyCombat,
                emptyMaintenance,
                emptyEnvironment,
                emptyItems,
                sourceCharacters,
                emptyRouting,
                worldRemoved: true);
        ApplyContributorFingerprints(
            drain.entries[0].participants,
            absentContributors,
            updatePrepared: false);
        AcknowledgeAllOwners(drain.entries[0].participants);
        ProductionFacilityDestructiveDrainParticipantSaveData
            acknowledgedSensorParticipant = drain.entries[0].participants.Single(
                value => string.Equals(
                    value.participantId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .StockSensorEmbeddedSalvage,
                    StringComparison.Ordinal));
        ProductionFacilityDestructiveDrainOwnerSaveData acknowledgedSensorOwner =
            acknowledgedSensorParticipant.owners.Single();
        ProductionInstalledStockSensorSaveData sourceInstalledSensor =
            sourceProduction.installedStockSensors.Single(value => value != null
                && string.Equals(value.facilityId,
                    destructiveFacilityId.Value,
                    StringComparison.Ordinal));
        string sensorDestination = ProductionStockSensorRuntime
            .BuildDestinationId(destructiveFacilityId.Value);
        string sensorChildStep =
            ProductionStockSensorDestructiveDrainCanonical
                .BuildChildStepOperationId(
                    acknowledgedSensorOwner.stepOperationId);
        string sensorSourceClaim =
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:stock-sensor-source-claim:" + destructiveFacilityId.Value);
        string sensorSourceOwnership =
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:stock-sensor-source-ownership:"
                + destructiveFacilityId.Value);
        string sensorChildRequest =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                destructiveOperationId.Value,
                sensorChildStep,
                acknowledgedSensorOwner.ownerStableId,
                sensorDestination,
                destructiveFacilityId.Value,
                sensorDestination,
                0,
                0,
                sensorSourceClaim,
                sensorSourceOwnership,
                Array.Empty<ProductionInputDestinationDrainStackSaveData>(),
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                0,
                0L);
        ProductionInputDestinationCustodyDrainSaveData terminalSensorChild = new()
        {
            parentOperationId = destructiveOperationId.Value,
            stepOperationId = sensorChildStep,
            ownerStableId = acknowledgedSensorOwner.ownerStableId,
            billId = sensorDestination,
            facilityId = destructiveFacilityId.Value,
            sourceDestinationId = sensorDestination,
            sourceClaimFingerprint = sensorSourceClaim,
            sourceOwnershipFingerprint = sensorSourceOwnership,
            requestFingerprint = sensorChildRequest,
            phase = ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc,
            sourceStacks = new List<ProductionInputDestinationDrainStackSaveData>(),
            sourceOperations = new List<
                ProductionInputDestinationDrainOperationSaveData>(),
            sourceActors = new List<ProductionInputDestinationDrainActorSaveData>(),
            completedActorIds = new List<string>(),
            releasedOperationIds = new List<string>(),
            releasedStackIds = new List<string>(),
            inputQuantity = 0,
            inputMassGrams = 0L,
            releasedQuantity = 0,
            releasedMassGrams = 0L,
            resultFingerprint =
                ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                    "qa:stock-sensor-empty-release:"
                    + destructiveFacilityId.Value)
        };
        terminalSensorChild.commitId =
            ProductionInputDestinationCustodyDrainFingerprint.CreateCommit(
                terminalSensorChild.stepOperationId,
                terminalSensorChild.requestFingerprint);
        terminalSensorChild.receiptFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateReceipt(
                terminalSensorChild.requestFingerprint,
                terminalSensorChild.resultFingerprint,
                0,
                0L,
                Array.Empty<string>(),
                Array.Empty<string>());
        ProductionStockSensorRemovalSaveData terminalSensorRemoval = new()
        {
            phase = ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
            facilityId = destructiveFacilityId.Value,
            itemId = sourceInstalledSensor.itemId,
            operationId = ProductionStockSensorRuntime.BuildRemovalOperationId(
                destructiveFacilityId.Value,
                sourceInstalledSensor.inputSourceStackId),
            reasonCode = ProductionStockSensorRuntime.RemovalReasonCode,
            installationSourceStackId =
                sourceInstalledSensor.inputSourceStackId,
            expectedOutputMassGrams = sourceInstalledSensor.embeddedMassGrams,
            outputQuantity = 1,
            outputMassGrams = sourceInstalledSensor.embeddedMassGrams,
            outputCommitIds = new List<string>()
        };
        terminalSensorRemoval.outputCommitIds.Add(
            ProductionStockSensorRuntime.BuildRemovalOutputCommitId(
                terminalSensorRemoval));
        Require(ProductionStockSensorDestructiveDrainCanonical.Provenance
                .TryCreate(
                    destructiveFacilityId,
                    null,
                    null,
                    terminalSensorRemoval,
                    out ProductionStockSensorDestructiveDrainCanonical.Provenance
                        sensorProvenance),
            "Fixture did not create canonical stock-sensor provenance.");
        acknowledgedSensorOwner.requestFingerprint =
            ProductionStockSensorDestructiveDrainCanonical
                .BuildRequestFingerprint(
                    terminalSensorChild.requestFingerprint,
                    sensorProvenance);
        Require(ProductionStockSensorDestructiveDrainCanonical
                .TryBuildCompositeTerminal(
                    acknowledgedSensorOwner.requestFingerprint,
                    terminalSensorChild,
                    terminalSensorRemoval,
                    out string sensorCommit,
                    out string sensorReceipt),
            "Fixture did not create a canonical stock-sensor terminal receipt.");
        acknowledgedSensorOwner.commitId = sensorCommit;
        acknowledgedSensorOwner.receiptFingerprint = sensorReceipt;
        emptyItems.pendingProductionInputDestinationDrains.Add(
            terminalSensorChild);
        emptyProduction.pendingStockSensorRemovals.Add(terminalSensorRemoval);
        acknowledgedSensorParticipant.expectedCurrentContributionFingerprint =
            ProductionOutputDestinationDurableSaveProjector.ProjectStockSensor(
                destructiveFacilityId,
                emptyProduction,
                emptyItems,
                sourceCharacters);
        ReplacePayload(
            complete,
            ProductionBillsSaveSection.Id,
            emptyProduction);
        ProductionBillSaveData sourceGenericBill = sourceProduction.bills.Single(
            value => value != null && string.Equals(
                value.buildingInstanceId,
                destructiveFacilityId.Value,
                StringComparison.Ordinal));
        ProductionFacilityDestructiveDrainParticipantSaveData
            acknowledgedGenericParticipant = drain.entries[0].participants.Single(
                value => string.Equals(
                    value.participantId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills,
                    StringComparison.Ordinal));
        ProductionFacilityDestructiveDrainOwnerSaveData acknowledgedGenericOwner =
            acknowledgedGenericParticipant.owners.Single(value => string.Equals(
                value.ownerStableId,
                ProductionFacilityDestructiveDrainOwnerStableIds.GenericBill(
                    sourceGenericBill.billId),
                StringComparison.Ordinal));
        string genericSourceFingerprint =
            ProductionGenericBillTerminalDrainCanonical
                .CreateSourceBillFingerprint(sourceGenericBill);
        string genericChildStep = acknowledgedGenericOwner.stepOperationId
            + ":input-destination-custody";
        string emptyInputOwnershipFingerprint =
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:empty-input-destination:" + sourceGenericBill.billId);
        string genericChildRequest =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                destructiveOperationId.Value,
                genericChildStep,
                acknowledgedGenericOwner.ownerStableId,
                sourceGenericBill.billId,
                destructiveFacilityId.Value,
                sourceGenericBill.materialDestinationId,
                0,
                0,
                genericSourceFingerprint,
                emptyInputOwnershipFingerprint,
                Array.Empty<ProductionInputDestinationDrainStackSaveData>(),
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                0,
                0L);
        ProductionInputDestinationCustodyDrainSaveData terminalGenericChild = new()
        {
            parentOperationId = destructiveOperationId.Value,
            stepOperationId = genericChildStep,
            ownerStableId = acknowledgedGenericOwner.ownerStableId,
            billId = sourceGenericBill.billId,
            facilityId = destructiveFacilityId.Value,
            sourceDestinationId = sourceGenericBill.materialDestinationId,
            ownerGridX = 0,
            ownerGridY = 0,
            sourceClaimFingerprint = genericSourceFingerprint,
            sourceOwnershipFingerprint = emptyInputOwnershipFingerprint,
            requestFingerprint = genericChildRequest,
            phase = ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc,
            sourceStacks = new List<ProductionInputDestinationDrainStackSaveData>(),
            sourceOperations = new List<
                ProductionInputDestinationDrainOperationSaveData>(),
            sourceActors = new List<ProductionInputDestinationDrainActorSaveData>(),
            completedActorIds = new List<string>(),
            releasedOperationIds = new List<string>(),
            releasedStackIds = new List<string>(),
            inputQuantity = 0,
            inputMassGrams = 0L,
            releasedQuantity = 0,
            releasedMassGrams = 0L,
            resultFingerprint = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint(
                    "qa:empty-input-release:" + sourceGenericBill.billId)
        };
        terminalGenericChild.commitId =
            ProductionInputDestinationCustodyDrainFingerprint.CreateCommit(
                terminalGenericChild.stepOperationId,
                terminalGenericChild.requestFingerprint);
        terminalGenericChild.receiptFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateReceipt(
                terminalGenericChild.requestFingerprint,
                terminalGenericChild.resultFingerprint,
                0,
                0L,
                Array.Empty<string>(),
                Array.Empty<string>());
        string genericProducerRequest =
            ProductionGenericBillTerminalDrainCanonical.CreateRequestFingerprint(
                destructiveOperationId.Value,
                acknowledgedGenericOwner.stepOperationId,
                acknowledgedGenericOwner.ownerStableId,
                sourceGenericBill,
                genericChildStep,
                genericChildRequest);
        ProductionGenericBillTerminalDrainSaveData terminalGenericProducer = new()
        {
            parentOperationId = destructiveOperationId.Value,
            stepOperationId = acknowledgedGenericOwner.stepOperationId,
            ownerStableId = acknowledgedGenericOwner.ownerStableId,
            billId = sourceGenericBill.billId,
            facilityId = destructiveFacilityId.Value,
            inputDestinationId = sourceGenericBill.materialDestinationId,
            sourceBill = ProductionGenericBillTerminalDrainCanonical.CloneBill(
                sourceGenericBill),
            sourceBillFingerprint = genericSourceFingerprint,
            inputDestinationDrainStepOperationId = genericChildStep,
            inputDestinationDrainRequestFingerprint = genericChildRequest,
            requestFingerprint = genericProducerRequest,
            phase = ProductionGenericBillTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
            inputDestinationDrainCommitId = terminalGenericChild.commitId,
            inputDestinationDrainReceiptFingerprint =
                terminalGenericChild.receiptFingerprint,
            releasedInputQuantity = 0,
            releasedInputMassGrams = 0L
        };
        terminalGenericProducer.wipTerminalCommitId =
            ProductionGenericBillTerminalDrainCanonical
                .RequiresWipTerminalReceipt(sourceGenericBill)
                ? ProductionGenericBillTerminalDrainCanonical
                    .CreateWipTerminalCommitId(
                        sourceGenericBill.billId,
                        sourceGenericBill.cycleSequence)
                : string.Empty;
        terminalGenericProducer.billTerminalEffectFingerprint =
            ProductionGenericBillTerminalDrainCanonical
                .CreateBillTerminalEffectFingerprint(
                    terminalGenericProducer.requestFingerprint,
                    terminalGenericProducer
                        .inputDestinationDrainReceiptFingerprint,
                    terminalGenericProducer.wipTerminalCommitId);
        terminalGenericProducer.commitId =
            ProductionGenericBillTerminalDrainCanonical.CreateCommitId(
                terminalGenericProducer.stepOperationId,
                terminalGenericProducer.requestFingerprint);
        terminalGenericProducer.receiptFingerprint =
            ProductionGenericBillTerminalDrainCanonical.CreateReceiptFingerprint(
                terminalGenericProducer.requestFingerprint,
                terminalGenericProducer.inputDestinationDrainReceiptFingerprint,
                terminalGenericProducer.billTerminalEffectFingerprint,
                terminalGenericProducer.commitId);
        acknowledgedGenericOwner.requestFingerprint = genericProducerRequest;
        acknowledgedGenericOwner.commitId = terminalGenericProducer.commitId;
        acknowledgedGenericOwner.receiptFingerprint =
            terminalGenericProducer.receiptFingerprint;
        emptyItems.pendingProductionInputDestinationDrains.Add(
            terminalGenericChild);
        emptyGenericTerminalDrains.entries.Add(terminalGenericProducer);
        ReplacePayload(
            complete,
            ProductionGenericBillTerminalDrainSaveSection.Id,
            emptyGenericTerminalDrains);
        if (ProductionGenericBillTerminalDrainCanonical
            .RequiresWipTerminalReceipt(sourceGenericBill))
        {
            emptyProduction.wipTerminalReceipts.Add(
                CreateFacilityDestroyedWipReceipt(sourceGenericBill));
            ReplacePayload(
                complete,
                ProductionBillsSaveSection.Id,
                emptyProduction);
        }
        ProductionFacilityDestructiveDrainParticipantSaveData
            acknowledgedPhysicalParticipant = drain.entries[0].participants.Single(
                value => string.Equals(
                    value.participantId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .PhysicalCustodyCarryRecovery,
                    StringComparison.Ordinal));
        ProductionFacilityDestructiveDrainOwnerSaveData acknowledgedPhysicalOwner =
            acknowledgedPhysicalParticipant.owners.Single();
        emptyItems.pendingProductionCustodyDrains.Add(new()
        {
            stepOperationId = acknowledgedPhysicalOwner.stepOperationId,
            ownerStableId = acknowledgedPhysicalOwner.ownerStableId,
            sourceDestinationId = ProductionOutputDestinationId
                .FromFacility(destructiveFacilityId).Value,
            ownerGridX = 0,
            ownerGridY = 0,
            requestFingerprint = acknowledgedPhysicalOwner.requestFingerprint,
            sourceOwnershipFingerprint = acknowledgedPhysicalParticipant
                .preparedContributionFingerprint,
            phase = ProductionPhysicalCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
            sourceStackIds = new List<string> { "stack:qa:drained" },
            sourceActorIds = new List<string>(),
            sourceHaulIntentOperationIds = new List<string>(),
            completedActorIds = new List<string>(),
            releasedHaulIntentOperationIds = new List<string>(),
            releasedStackIds = new List<string> { "stack:qa:drained" },
            inputQuantity = 1,
            inputMassGrams = 1L,
            releasedQuantity = 1,
            releasedMassGrams = 1L,
            resultFingerprint = new string('b', 64),
            commitId = acknowledgedPhysicalOwner.commitId,
            receiptFingerprint = acknowledgedPhysicalOwner.receiptFingerprint
        });
        ProductionFacilityDestructiveDrainParticipantSaveData
            acknowledgedCapacityParticipant = drain.entries[0].participants.Single(
                value => string.Equals(
                    value.participantId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CapacityRoutingOutbox,
                    StringComparison.Ordinal));
        ProductionPreparedOutputRoutingBatchSaveData sourceRoutingBatch =
            sourceRouting.batches.Single();
        ProductionFacilityDestructiveDrainOwnerSaveData
            acknowledgedCapacityOwner = acknowledgedCapacityParticipant.owners
                .Single(value => string.Equals(
                    value.ownerStableId,
                    ProductionFacilityDestructiveDrainOwnerStableIds.RoutingBatch(
                        sourceRoutingBatch.batchCommitId),
                    StringComparison.Ordinal));
        List<ProductionCapacityRoutingDrainLineSaveData> frozenLines =
            sourceRoutingBatch.lines
                .OrderBy(value => value.lineCommitId, StringComparer.Ordinal)
                .Select(value => new ProductionCapacityRoutingDrainLineSaveData
                {
                    lineCommitId = value.lineCommitId,
                    outputLineId = value.outputLineId,
                    itemId = value.itemId,
                    componentFingerprint = value.componentFingerprint,
                    outputCapabilityId = value.outputCapabilityId,
                    outputCapabilityVersion = value.outputCapabilityVersion,
                    outputComponentCodecId = value.outputComponentCodecId,
                    outputComponentCodecVersion =
                        value.outputComponentCodecVersion,
                    outputCapabilityFingerprint =
                        value.outputCapabilityFingerprint,
                    originalQuantity = value.originalQuantity,
                    originalMassGrams = value.originalMassGrams,
                    remainingQuantity = value.remainingQuantity,
                    remainingMassGrams = value.remainingMassGrams,
                    routedQuantity = value.routedQuantity,
                    routedMassGrams = value.routedMassGrams
                })
                .ToList();
        int frozenQuantity = frozenLines.Sum(value => value.originalQuantity);
        long frozenMass = frozenLines.Sum(value => value.originalMassGrams);
        List<string> frozenCustodyStackIds = sourceItems.stacks
            .Where(value => value != null
                && string.Equals(
                    value.destinationId,
                    sourceRoutingBatch.destinationId,
                    StringComparison.Ordinal))
            .Select(value => value.stackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        string capacityRequest =
            ProductionCapacityRoutingDrainFingerprint.CreateRequest(
                acknowledgedCapacityOwner.stepOperationId,
                acknowledgedCapacityOwner.ownerStableId,
                FacilityId,
                sourceRoutingBatch.destinationId,
                sourceRoutingBatch.batchCommitId,
                sourceRoutingBatch.outcomeFingerprint,
                sourceRoutingBatch.routingFingerprint,
                acknowledgedCapacityParticipant.preparedContributionFingerprint,
                frozenLines,
                Array.Empty<ProductionCapacityRoutingDrainRouteSaveData>(),
                Array.Empty<ProductionCapacityRoutingDrainSliceSaveData>(),
                Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>(),
                frozenCustodyStackIds,
                frozenQuantity,
                frozenMass);
        ProductionCapacityRoutingDrainSaveData terminalCapacityProducer = new()
        {
            stepOperationId = acknowledgedCapacityOwner.stepOperationId,
            ownerStableId = acknowledgedCapacityOwner.ownerStableId,
            facilityId = FacilityId,
            sourceDestinationId = sourceRoutingBatch.destinationId,
            batchCommitId = sourceRoutingBatch.batchCommitId,
            sourceOutcomeFingerprint = sourceRoutingBatch.outcomeFingerprint,
            sourceRoutingFingerprint = sourceRoutingBatch.routingFingerprint,
            sourceOwnershipFingerprint = acknowledgedCapacityParticipant
                .preparedContributionFingerprint,
            requestFingerprint = capacityRequest,
            phase = ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
            sourceLines = frozenLines,
            sourceRoutes = new List<ProductionCapacityRoutingDrainRouteSaveData>(),
            sourceSlices = new List<ProductionCapacityRoutingDrainSliceSaveData>(),
            sourceActorCarries = new List<
                ProductionCapacityRoutingDrainActorCarrySaveData>(),
            sourceCustodyStackIds = frozenCustodyStackIds,
            completedLineCommitIds = frozenLines
                .Select(value => value.lineCommitId)
                .ToList(),
            finalRouteOperationIds = new List<string>
            {
                "route:qa:capacity-drained"
            },
            preservedStackIds = new List<string>
            {
                "stack:qa:capacity-preserved"
            },
            stablePhysicalStackIds = new List<string>
            {
                "stack:qa:capacity-preserved"
            },
            inputQuantity = frozenQuantity,
            inputMassGrams = frozenMass,
            preservedQuantity = frozenQuantity,
            preservedMassGrams = frozenMass,
            observedRemovedBatchCommitId = sourceRoutingBatch.batchCommitId,
            resultFingerprint = new string('c', 64)
        };
        terminalCapacityProducer.commitId =
            ProductionCapacityRoutingDrainFingerprint.CreateCommitId(
                terminalCapacityProducer.stepOperationId,
                terminalCapacityProducer.requestFingerprint);
        terminalCapacityProducer.receiptFingerprint =
            ProductionCapacityRoutingDrainFingerprint.CreateReceipt(
                terminalCapacityProducer);
        acknowledgedCapacityOwner.requestFingerprint = capacityRequest;
        acknowledgedCapacityOwner.commitId = terminalCapacityProducer.commitId;
        acknowledgedCapacityOwner.receiptFingerprint =
            terminalCapacityProducer.receiptFingerprint;
        emptyItems.pendingCapacityRoutingDrains.Add(terminalCapacityProducer);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);
        drain.entries[0].expectedCurrentLifecycleFingerprint =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectAbsentFacilityAggregateFromSave(
                    (BuildingInstanceId)FacilityId,
                    missingWorld,
                    emptyProduction,
                    emptyCombat,
                    emptyMaintenance,
                    emptyEnvironment,
                    emptyItems,
                    new DungeonCharacterWorldSaveData
                    {
                        actors = new List<DungeonCharacterSaveData>()
                    },
                    emptyRouting);
        ReplacePayload(
            complete,
            ProductionFacilityDestructiveDrainSaveSection.Id,
            drain);
        DungeonGameRestoreReport absentLifecycle = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            absentLifecycle);
        Require(absentLifecycle.Success,
            "Exact world-removed absent lifecycle failed preflight: "
            + string.Join(" | ", absentLifecycle.Errors));

        string exactPhysicalProducerReceipt = emptyItems
            .pendingProductionCustodyDrains.Single().receiptFingerprint;
        emptyItems.pendingProductionCustodyDrains.Single()
            .receiptFingerprint = new string('f', 64);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);
        DungeonGameRestoreReport physicalProducerReceiptDrift = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            physicalProducerReceiptDrift);
        Require(!physicalProducerReceiptDrift.Success
            && physicalProducerReceiptDrift.Errors.Any(value => value.Contains(
                "physical-producer-receipt-mismatch",
                StringComparison.Ordinal)),
            "A mismatched physical drain producer receipt passed preflight.");
        emptyItems.pendingProductionCustodyDrains.Single()
            .receiptFingerprint = exactPhysicalProducerReceipt;

        ProductionPhysicalCustodyDrainSaveData orphanPhysicalProducer =
            emptyItems.pendingProductionCustodyDrains.Single().Clone();
        orphanPhysicalProducer.stepOperationId =
            "production-facility-destructive-drain-step:qa:orphan";
        orphanPhysicalProducer.sourceDestinationId =
            "production-output:building:qa:orphan";
        orphanPhysicalProducer.ownerStableId =
            "physical-destination:" +
            orphanPhysicalProducer.sourceDestinationId;
        emptyItems.pendingProductionCustodyDrains.Add(orphanPhysicalProducer);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);
        DungeonGameRestoreReport orphanPhysicalProducerReport = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            orphanPhysicalProducerReport);
        Require(!orphanPhysicalProducerReport.Success
            && orphanPhysicalProducerReport.Errors.Any(value => value.Contains(
                "physical-producer-orphan",
                StringComparison.Ordinal)),
            "A physical drain producer without a journal owner passed preflight.");
        emptyItems.pendingProductionCustodyDrains.Remove(orphanPhysicalProducer);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);

        ProductionCapacityRoutingDrainSaveData exactCapacityProducer =
            emptyItems.pendingCapacityRoutingDrains.Single();
        ProductionCapacityRoutingDrainPhase exactCapacityPhase =
            exactCapacityProducer.phase;
        exactCapacityProducer.phase =
            ProductionCapacityRoutingDrainPhase.ReleasingOperationAuthority;
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);
        DungeonGameRestoreReport transientCapacityProducer = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            transientCapacityProducer);
        Require(!transientCapacityProducer.Success
            && transientCapacityProducer.Errors.Any(value => value.Contains(
                "capacity-transient-save-phase",
                StringComparison.Ordinal)),
            "The cross-aggregate candidate validator accepted a transient capacity-routing save phase.");
        exactCapacityProducer.phase = exactCapacityPhase;
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);

        List<DungeonSaveSectionEnvelope> missingDrainSection = complete
            .Where(value => !string.Equals(
                value.sectionId,
                ProductionFacilityDestructiveDrainSaveSection.Id,
                StringComparison.Ordinal))
            .ToList();
        DungeonGameRestoreReport producerWithoutJournal = new();
        validator.Validate(
            new DungeonGameSaveData { sections = missingDrainSection },
            producerWithoutJournal);
        Require(!producerWithoutJournal.Success
            && producerWithoutJournal.Errors.Any(value => value.Contains(
                "without its journal section",
                StringComparison.Ordinal)),
            "A capacity-routing producer without its destructive journal passed preflight.");

        string exactCapacityProducerReceipt =
            exactCapacityProducer.receiptFingerprint;
        exactCapacityProducer.receiptFingerprint = new string('e', 64);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);
        DungeonGameRestoreReport capacityProducerReceiptDrift = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            capacityProducerReceiptDrift);
        Require(!capacityProducerReceiptDrift.Success
            && capacityProducerReceiptDrift.Errors.Any(value => value.Contains(
                "capacity-producer-receipt-mismatch",
                StringComparison.Ordinal)),
            "A mismatched capacity drain producer receipt passed preflight.");
        exactCapacityProducer.receiptFingerprint = exactCapacityProducerReceipt;

        emptyItems.pendingCapacityRoutingDrains.Remove(exactCapacityProducer);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);
        DungeonGameRestoreReport missingCapacityProducer = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            missingCapacityProducer);
        Require(!missingCapacityProducer.Success
            && missingCapacityProducer.Errors.Any(value => value.Contains(
                "capacity-producer-missing",
                StringComparison.Ordinal)),
            "An acknowledged capacity owner without producer evidence passed preflight.");
        emptyItems.pendingCapacityRoutingDrains.Add(exactCapacityProducer);

        ProductionCapacityRoutingDrainSaveData orphanCapacityProducer =
            exactCapacityProducer.Clone();
        orphanCapacityProducer.stepOperationId =
            "production-facility-destructive-drain-step:qa:capacity-orphan";
        orphanCapacityProducer.batchCommitId = "batch:qa:capacity-orphan";
        orphanCapacityProducer.ownerStableId =
            "routing-batch:" + orphanCapacityProducer.batchCommitId;
        emptyItems.pendingCapacityRoutingDrains.Add(orphanCapacityProducer);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);
        DungeonGameRestoreReport orphanCapacityProducerReport = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            orphanCapacityProducerReport);
        Require(!orphanCapacityProducerReport.Success
            && orphanCapacityProducerReport.Errors.Any(value => value.Contains(
                "capacity-producer-orphan",
                StringComparison.Ordinal)),
            "A capacity drain producer without a journal owner passed preflight.");
        emptyItems.pendingCapacityRoutingDrains.Remove(orphanCapacityProducer);
        ReplacePayload(
            complete,
            PhysicalItemsSaveSection.Id,
            emptyItems);

        DungeonProductionBillSaveData orphanProduction =
            JsonUtility.FromJson<DungeonProductionBillSaveData>(
                RequireEnvelope(
                    sourceEnvelopes,
                    ProductionBillsSaveSection.Id).payloadJson);
        ReplacePayload(
            complete,
            ProductionBillsSaveSection.Id,
            orphanProduction);
        DungeonGameRestoreReport orphanOwner = new();
        validator.Validate(
            new DungeonGameSaveData { sections = complete },
            orphanOwner);
        Require(!orphanOwner.Success
            && orphanOwner.Errors.Any(value => value.Contains(
                "absent-lifecycle-has-owner",
                StringComparison.Ordinal)),
            "World-removed lifecycle accepted an orphan production owner.");
    }

    private static IReadOnlyDictionary<string, string>
        BuildContributorFingerprints(
            RuntimeGraph graph,
            BuildingInstanceId facilityId,
            ModularFacilityWorldSaveData world,
        DungeonProductionBillSaveData production,
        DungeonCombatEquipmentSaveData combat,
        CombatEquipmentMaintenanceSaveData maintenance,
        DungeonCharacterEnvironmentSaveData environment,
            DungeonPhysicalItemSaveData items,
            DungeonCharacterWorldSaveData characters,
            ProductionPreparedOutputRoutingSaveData routing,
            bool worldRemoved)
    {
        string capacity;
        if (worldRemoved)
        {
            capacity = ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRouting(
                    facilityId,
                    null,
                    new FacilityBufferPhysicalOccupancySnapshot(0L, 0L),
                    routing,
                    items.pendingExactOutputRoutes);
        }
        else
        {
            capacity = ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    facilityId,
                    world,
                    production,
                    EmptyGenericTerminalPayload(),
                    items,
                    characters,
                    routing,
                    items.pendingExactOutputRoutes,
                    graph.BuildingDefinitions,
                    graph.CapacityProjector,
                    graph.Mass).Fingerprint;
        }
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProductionOutputDestinationDurableSaveProjector
                .ApparelContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectApparel(
                    facilityId,
                    environment),
            [ProductionOutputDestinationDurableSaveProjector
                .CapacityRoutingContributorId] = capacity,
            [ProductionOutputDestinationDurableSaveProjector
                .EquipmentContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectEquipment(
                    facilityId,
                    combat,
                    maintenance),
            [ProductionOutputDestinationDurableSaveProjector
                .GenericBillsContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectGenericBills(
                    facilityId,
                    production),
            [ProductionOutputDestinationDurableSaveProjector
                .PhysicalCustodyContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectPhysicalCustody(
                    facilityId,
                    items,
                    characters),
            [ProductionOutputDestinationDurableSaveProjector
                .StockSensorContributorId] =
                ProductionOutputDestinationDurableSaveProjector.ProjectStockSensor(
                    facilityId,
                    production,
                    items,
                    characters)
        };
    }

    private static void ApplyContributorFingerprints(
        IEnumerable<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants,
        IReadOnlyDictionary<string, string> contributors,
        bool updatePrepared)
    {
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData participant
                 in participants)
        {
            string fingerprint = contributors[participant.participantId];
            participant.expectedCurrentContributionFingerprint = fingerprint;
            if (updatePrepared)
                participant.preparedContributionFingerprint = fingerprint;
        }
    }

    private static void ApplyPlannedOwners(
        IEnumerable<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ownerSources,
        ProductionFacilityDestructiveDrainOperationId operationId)
    {
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData participant
                 in participants)
        {
            participant.owners = ownerSources[participant.participantId]
                .Select(ownerId => CreatePlannedOwner(
                    operationId,
                    participant.participantId,
                    ownerId))
                .OrderBy(value => value.ownerStableId, StringComparer.Ordinal)
                .ToList();
        }
    }

    private static ProductionFacilityDestructiveDrainOwnerSaveData
        CreatePlannedOwner(
            ProductionFacilityDestructiveDrainOperationId operationId,
            string participantId,
            string ownerStableId) => new()
        {
            ownerStableId = ownerStableId,
            disposition =
                ProductionFacilityDestructiveDrainDisposition.Terminalize,
            targetDestinationId = string.Empty,
            stepOperationId = ProductionFacilityDestructiveDrainCanonical
                .BuildStepOperationId(
                    operationId,
                    participantId,
                    ownerStableId),
            phase = ProductionFacilityDestructiveDrainStepPhase.Planned,
            requestFingerprint =
                ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                    "qa:planned-owner:" + participantId + ":" + ownerStableId),
            commitId = string.Empty,
            receiptFingerprint = string.Empty
        };

    private static ProductionWipTerminalReceiptSaveData
        CreateFacilityDestroyedWipReceipt(ProductionBillSaveData source)
    {
        long outputMass = (source.resolvedOutputs
                ?? new List<ProductionResolvedOutputSaveData>())
            .Where(value => value != null)
            .Aggregate(0L, (total, value) => checked(
                total + value.committedMassGrams));
        long availableMass = checked(
            source.wipInputMassGrams + source.processCleanWaterMassGrams);
        long loss = checked(availableMass - outputMass
            - source.processWastewaterMassGrams);
        if (loss < 0L)
        {
            throw new InvalidOperationException(
                "Facility-destroyed WIP fixture has negative declared loss.");
        }
        return new ProductionWipTerminalReceiptSaveData
        {
            commitId = ProductionGenericBillTerminalDrainCanonical
                .CreateWipTerminalCommitId(
                    source.billId,
                    source.cycleSequence),
            billId = source.billId,
            recipeId = source.recipeId,
            buildingInstanceId = source.buildingInstanceId,
            cycleSequence = source.cycleSequence,
            inputCommitId = source.wipInputCommitId,
            inputQuantity = source.wipInputQuantity,
            inputMassGrams = source.wipInputMassGrams,
            processCleanWaterMassGrams = source.processCleanWaterMassGrams,
            processWastewaterMassGrams = source.processWastewaterMassGrams,
            wastewaterComponents = (source.processWastewaterComponents
                    ?? new List<ProductionWastewaterComponentSaveData>())
                .Select(value => value?.Clone())
                .ToList(),
            committedOutputMassGrams = outputMass,
            reason = ProductionWipTerminalReason.FacilityDestroyed,
            lossKind = ProductionWipTerminalLossKind
                .ExplicitIrrecoverableProcessLoss,
            declaredLossMassGrams = loss
        };
    }

    private static void AcknowledgeAllOwners(
        IEnumerable<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants)
    {
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData participant
                 in participants)
        {
            foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                     participant.owners)
            {
                owner.phase =
                    ProductionFacilityDestructiveDrainStepPhase
                        .OwnerAcknowledged;
                owner.commitId = "qa-drain-commit:"
                    + ProductionFacilityDestructiveDrainCanonical
                        .ComputeFingerprint(owner.stepOperationId)
                        .Substring(0, 16);
                owner.receiptFingerprint =
                    ProductionFacilityDestructiveDrainCanonical
                        .ComputeFingerprint(
                            "qa:drain-receipt:" + owner.stepOperationId);
            }
        }
    }

    private static DungeonSaveSectionEnvelope Envelope<T>(
        string sectionId,
        T payload)
        where T : class => new()
        {
            sectionId = sectionId,
            sectionVersion = 1,
            restorePhase = DungeonSaveRestorePhase.RuntimeState,
            payloadJson = JsonUtility.ToJson(payload)
        };

    private static DungeonSaveSectionEnvelope CloneEnvelope(
        DungeonSaveSectionEnvelope source) => new()
    {
        sectionId = source.sectionId,
        sectionVersion = source.sectionVersion,
        restorePhase = source.restorePhase,
        optional = source.optional,
        payloadJson = source.payloadJson
    };

    private static void ReplacePayload<T>(
        IEnumerable<DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        T payload)
        where T : class
    {
        DungeonSaveSectionEnvelope envelope = envelopes.Single(value =>
            string.Equals(value.sectionId, sectionId, StringComparison.Ordinal));
        envelope.payloadJson = JsonUtility.ToJson(payload);
    }

    private static bool IsSha256(string value) => value?.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private sealed class ScenarioConfig
    {
        internal ScenarioConfig(
            string label,
            string recipeId,
            string itemId,
            string facilityId,
            string workerId,
            string buildingAssetPath,
            int expectedOutputQuantity,
            long expectedBatchMassGrams,
            long expectedCapacityGrams,
            double? expectedInitialFreshnessSeconds)
        {
            if (!Canonical(label)
                || !Canonical(recipeId)
                || !Canonical(itemId)
                || !Canonical(facilityId)
                || !Canonical(workerId)
                || !Canonical(buildingAssetPath)
                || expectedOutputQuantity <= 0
                || expectedBatchMassGrams <= 0L
                || expectedCapacityGrams <= 0L
                || expectedCapacityGrams != checked(
                    expectedBatchMassGrams *
                    ExpectedOutputBufferCycleCapacity)
                || expectedInitialFreshnessSeconds is < 0d
                || expectedInitialFreshnessSeconds.HasValue
                    && (double.IsNaN(expectedInitialFreshnessSeconds.Value)
                        || double.IsInfinity(
                            expectedInitialFreshnessSeconds.Value)))
            {
                throw new InvalidOperationException(
                    "Prepared-output full-persistence scenario config is invalid.");
            }

            Label = label;
            RecipeId = recipeId;
            ItemId = itemId;
            FacilityId = facilityId;
            WorkerId = workerId;
            BuildingAssetPath = buildingAssetPath;
            ExpectedOutputQuantity = expectedOutputQuantity;
            ExpectedBatchMassGrams = expectedBatchMassGrams;
            ExpectedCapacityGrams = expectedCapacityGrams;
            ExpectedInitialFreshnessSeconds =
                expectedInitialFreshnessSeconds;
        }

        internal string Label { get; }
        internal string RecipeId { get; }
        internal string ItemId { get; }
        internal string FacilityId { get; }
        internal string WorkerId { get; }
        internal string BuildingAssetPath { get; }
        internal int ExpectedOutputQuantity { get; }
        internal long ExpectedBatchMassGrams { get; }
        internal long ExpectedCapacityGrams { get; }
        internal double? ExpectedInitialFreshnessSeconds { get; }
        internal int ExpectedOutputBufferCycleCapacity => 4;

        private static bool Canonical(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RuntimeGraph : IDisposable
    {
        private readonly GameObject facilityObject;
        private readonly GameObject workerObject;
        private readonly GameObject researchObject;
        private readonly GameObject textureObject;
        private readonly MutableGridProvider gridProvider;

        internal RuntimeGraph(
            string suffix,
            int seed,
            ScenarioConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            RootStore = new DungeonRuntimeAggregateRootStore();
            LifecycleRestoreCandidates =
                new ProductionOutputLifecycleRestoreCandidateIndex(
                    new NoopDrainCandidateValidator());
            gridProvider = new MutableGridProvider(CreateFacilityWorldGrid());
            RestoreCandidates = new RestoreWorldCandidateIndex();
            IGameContentCatalog gameContent = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            Economy = new ResourceEconomyContentCatalog(gameContent);
            Require(
                Economy.TryGetRecipe(
                    Config.RecipeId,
                    out ProductionRecipeSO recipe),
                Config.Label + " recipe is missing.");
            Recipe = recipe;
            ItemCatalog = EditorItemCatalogFactory.Create();
            Mass = new PhysicalItemMassQuery(ItemCatalog);

            facilityObject = new GameObject(
                Config.Label + " Full Persistence " + suffix);
            Facility = facilityObject.AddComponent<BuildableObject>();
            Facility.RestorePersistentIdentity(
                (BuildingInstanceId)Config.FacilityId);
            CharacterAiEditorTestDependencies.Inject(Facility);
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                Config.BuildingAssetPath);
            Require(
                building != null,
                Config.Label + " building definition is missing.");
            BuildingDefinitions = new FixedBuildingDefinitionLookup(building);
            Vector2Int facilityPosition = new(11, 1);
            Facility.SetGrid(Grid);
            Facility.Initialization(building, facilityPosition);
            Require(
                Grid.RegisterOccupant(
                    Facility,
                    building.Placement.Layer,
                    Facility.buildPoses,
                    building.Placement.IsMovement),
                "P03 sawmill fixture could not register in the facility world.");

            workerObject = new GameObject(
                Config.Label + " Full Persistence Worker " + suffix);
            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.EnsureRuntimeState();
            worker.Identity.SetPersistentId(Config.WorkerId);
            Worker = worker;

            researchObject = new GameObject(
                Config.Label + " Full Persistence Research " + suffix)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            BlueprintResearchRuntime research =
                researchObject.AddComponent<BlueprintResearchRuntime>();
            research.enabled = false;
            ProgressionSceneRuntimeReferences progression = new(
                null,
                research,
                null);

            Clock = new UnityGameClock();
            FixedCharacterIdRegistry characterIds = new();
            FixedHaulingSettings hauling = new();
            ICharacterAiWorldRegistry worldRegistry =
                CharacterAiEditorTestDependencies.WorldRegistry;
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                RootStore);
            Claims = new FacilityBufferDestinationClaimRegistry();
            QuantityReservations = new ItemQuantityReservationService(
                Repository,
                EditorNullItemMarkerPresenter.Instance,
                Clock);
            IItemReservationService reservations = new ItemReservationService(
                Repository,
                EditorNullItemMarkerPresenter.Instance,
                QuantityReservations);
            Spawner = new WorldItemSpawner(
                ItemCatalog,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            Occupancy = new FacilityBufferPhysicalOccupancyQuery(
                Repository,
                Mass,
                QuantityReservations);
            Admission = new FacilityBufferMassAdmissionService(
                Claims,
                Occupancy,
                Mass);
            WorldItemQueryService query = new(
                ItemCatalog,
                Mass,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            IGridPathSearchBroker pathSearch = new GridPathSearchBroker(
                Clock,
                doorAccessQuery: null,
                performanceRecorder: null,
                costPolicy: null);
            IWorldItemHaulPlanningService haulPlanning =
                new WorldItemHaulPlanningService(
                    gridProvider,
                    ItemCatalog,
                    Mass,
                    hauling,
                    characterIds,
                    pathSearch,
                    worldRegistry,
                    Repository,
                    QuantityReservations,
                    Claims);
            WorldItemReadServices reads = new(
                ItemCatalog,
                Mass,
                hauling,
                query,
                EditorNullItemMarkerPresenter.Instance,
                new EditorCharacterAiPerformanceRecorder(),
                DisabledDungeonDebugRuleQuery.Instance,
                new FacilityOutputClearanceTelemetryRuntime());
            WorldItemWarehouseService warehouses = new(
                ItemCatalog,
                Repository,
                worldRegistry,
                Spawner,
                EditorNullItemMarkerPresenter.Instance,
                gridProvider,
                characterIds,
                reservations,
                QuantityReservations,
                massAdmission: null,
                facilityBufferMassAdmission: Admission);
            IBufferStackAggregationService bufferAggregation =
                new BufferStackAggregationService(
                    ItemCatalog,
                    Repository,
                    EditorNullItemMarkerPresenter.Instance,
                    QuantityReservations,
                    QuantityReservations);
            Transfers = new ItemTransferService(
                reads,
                characterIds,
                gridProvider,
                worldRegistry,
                Claims,
                new ResourceCombatEquipmentCatalog(gameContent),
                new GameEventBus(),
                Repository,
                Spawner,
                warehouses,
                QuantityReservations,
                QuantityReservations,
                bufferAggregation,
                warehouseMassAdmission: null,
                retailStockPhysical: null,
                facilityBufferMassAdmission: Admission);
            FacilityOutputExactRouteService exactRoute = new(
                Repository,
                Mass,
                EditorNullItemMarkerPresenter.Instance);
            WorldItems = WorldItemEditorTestFactory.Create(
                gridProvider,
                ItemCatalog,
                hauling,
                characterIds,
                new EmptyDropZoneQuery(),
                new EmptySpawnerProvider(),
                pathSearch,
                worldRegistry,
                Clock,
                Repository,
                reservations,
                Spawner,
                query,
                haulPlanning,
                EditorNullItemMarkerPresenter.Instance,
                Transfers,
                new EditorCharacterAiPerformanceRecorder(),
                QuantityReservations,
                exactRoute);

            IStockQuery stock = new PhysicalStockQuery(
                Repository,
                ItemCatalog,
                Mass);
            FacilityBufferDestinationReleaseService release = new(
                WorldItems,
                Transfers,
                worldRegistry);
            Items = new ProductionItemGateway(
                stock,
                Transfers,
                WorldItems,
                ItemCatalog,
                release);
            PhysicalItemBatchDispositionService dispositions = new(
                Repository,
                Mass,
                EditorNullItemMarkerPresenter.Instance,
                QuantityReservations);
            ProductionStockSensorPhysicalGateway stockSensorPhysical = new(
                new PhysicalFacilityItemSinkGateway(stock, dispositions));
            ProductionStockSensorRemovalOutputGateway sensorRemoval = new(
                new PhysicalItemSourcePublicationService(WorldItems, Mass));

            IProductionWorkshopRuntime workshops =
                new EmptyWorkshopRuntime();
            IWorkforceReplanService workforce =
                new EmptyWorkforceReplanService();
            IProductionInputLogisticsService inputLogistics =
                new ProductionInputLogisticsService(
                Economy,
                    Items,
                    progression,
                    workforce,
                    workshops);
            IProductionCycleUtilityService cycleUtilities =
                new WorkOnlyCycleUtilities(workshops);
            BuildingWorld = new CandidateAwareBuildingWorldQuery(
                gridProvider,
                RestoreCandidates);
            ProductionPreparedOutputComponentCodec componentCodec = new();
            ResourceItemDefinitionCatalog itemDefinitions = new(gameContent);
            PerishableFoodOutputCapability perishableCapability = new(
                itemDefinitions);
            ProductionOutputHandlerRegistry outputHandlers = new(
                new IProductionOutputCapability[]
                {
                    new StandardDefinitionProductionOutputCapability(
                        Economy,
                        componentCodec),
                    perishableCapability
                });
            IProductionAssemblyBridge bridge =
                new ProductionAssemblyBridgeAdapter(
                    Items,
                    Items,
                    stockSensorPhysical,
                    inputLogistics,
                    cycleUtilities,
                    workshops,
                    BuildingWorld,
                    new EmptyWarehouseQuery(),
                    workforce,
                    outputHandlers,
                    narrativeQualification: null,
                    performance: () =>
                        CharacterAiEditorTestDependencies.NeutralPerformance);
            Bridge = bridge;
            WorkerHandle = bridge.CaptureWorker(Worker);
            Require(
                FacilityHandle.OutputBufferCycleCapacity ==
                    Config.ExpectedOutputBufferCycleCapacity,
                Config.Label
                    + " output-buffer cycle authority is not four.");

            IProductionOutputPlanningService outputPlanning =
                new ProductionOutputPlanningService(Economy, bridge);
            ProductionMaximumOutputFactorCatalog maximumFactors = new(
                LoadAll<BuildingSO>("Assets/Resources/SO/Building"));
            ProductionOutputMaximumMassRegistry maximumMass = new(
                new IProductionOutputMaximumMassCapability[]
                {
                    new StandardDefinitionProductionOutputCapability(
                        Economy,
                        componentCodec),
                    perishableCapability
                },
                Mass);
            ProductionOutputBufferCapacityProjector capacity = new(
                Economy,
                bridge,
                maximumFactors,
                componentCodec,
                Mass,
                maximumMass);
            CapacityProjector = capacity;
            FacilityBufferDestinationLifecycleService lifecycle = new(
                Claims,
                Claims,
                Admission,
                Admission);
            ProductionOutputDestinationAuthorityRuntime destinations = new(
                Claims,
                Admission,
                Claims,
                Admission,
                lifecycle);
            Publication = new FacilityBufferPlannedOutputPublicationService(
                Repository,
                ItemCatalog,
                Mass,
                Admission);
            Routing = new ProductionPreparedOutputRoutingAuthority();
            PreparedOutput = new ProductionPreparedOutputExecutionAdapter(
                Economy,
                outputPlanning,
                bridge,
                new EmptyGrandProjectBenefits(),
                new CanonicalProductionOutputResolver(
                    new RandomStreamProvider(seed)),
                new ProductionPreparedOutputMaterializerRegistry(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        componentCodec,
                        new PerishableFoodPreparedOutputMaterializer()
                    },
                    outputHandlers),
                Mass,
                capacity,
                destinations,
                Admission,
                Occupancy,
                Admission,
                Publication,
                Routing);

            ProductionAggregateStateStore productionState = new(RootStore);
            State = productionState;
            ProductionInputDestinationClaimRuntime inputClaims = new(
                Claims,
                Admission,
                Claims,
                Admission,
                lifecycle);
            SensorAuthority =
                new ProductionStockSensorDestinationAuthorityRuntime(
                    Items,
                    Claims,
                    Claims,
                    Admission,
                    Admission,
                    Occupancy,
                    lifecycle);
            ProductionStockSensorRuntime stockSensors = new(
                bridge,
                productionState,
                sensorRemoval,
                SensorAuthority,
                new ProductionFacilityDestructiveDrainOpenOperationQuery(
                    RootStore));
            StockSensors = stockSensors;
            IProductionOutputExecutionService legacyOutput =
                new ProductionOutputExecutionService(
                    bridge,
                    new EmptyGrandProjectBenefits(),
                    outputPlanning,
                    new RandomStreamProvider(seed));
            IProductionBillSnapshotProjector snapshots =
                new ProductionBillSnapshotProjector(
                    Economy,
                    bridge,
                    outputPlanning,
                    PreparedOutput,
                    stockSensors,
                    new EmptyDistributionQuery());
            Production = new ProductionBillRuntime(
                new ProductionBillOrderDependencies(
                    Economy,
                    bridge,
                    stockSensors,
                    productionState,
                    inputClaims,
                    new ProductionFacilityMutationEpochRuntime()),
                new ProductionBillExecutionDependencies(
                    outputPlanning,
                    legacyOutput,
                    PreparedOutput,
                    PreparedOutput,
                    snapshots,
                    bridge,
                    Clock,
                    Routing));

            Lifecycle = new ProductionOutputDestinationLifecycleQuery(
                new ProductionBillLifecycleContributor(Production),
                new CombatEquipmentCraftLifecycleContributor(
                    () => EmptyCombatEquipmentQueue.Instance,
                    () => EmptyMaintenanceOrders.Instance),
                new ApparelWorkOrderLifecycleContributor(
                    EmptyApparelWorkOrders.Instance),
                new ProductionOutputCapacityRoutingLifecycleContributor(
                    Claims,
                    Admission,
                    Occupancy,
                    Routing,
                    EmptyExactRouteOutbox.Instance),
                new ProductionOutputPhysicalLifecycleContributor(
                    Repository,
                    worldRegistry),
                new ProductionStockSensorLifecycleContributor(
                    stockSensors,
                    Repository,
                    worldRegistry,
                    Mass));

            PhysicalSection = new PhysicalItemsSaveSection(
                WorldItems,
                WorldItems,
                RestoreCandidates,
                LifecycleRestoreCandidates);
            ProductionPreparedOutputRestoreJoin productionJoin = new(
                WorldItems,
                Publication);
            ProductionBillsSaveSection billsSection = new(
                Production,
                WorldItems,
                WorldItems,
                productionJoin,
                LifecycleRestoreCandidates,
                RestoreCandidates,
                new ProductionFacilityHandleQueryAdapter());
            ProductionPreparedOutputRoutingRestoreJoin routingJoin = new(
                WorldItems,
                new EmptyExactRouteReconciler(),
                Routing);
            ProductionPreparedOutputRoutingSaveSection routingSection = new(
                Routing,
                routingJoin,
                LifecycleRestoreCandidates);
            textureObject = new GameObject(
                "Sawmill Full Persistence Texture " + suffix)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            GridTexture texture = textureObject.AddComponent<GridTexture>();
            WorldService = new ModularFacilityWorldSaveService(
                id => id == building.id ? building : null,
                new GridBuildingObjectFactory(),
                InjectBuilding,
                new StaticGridTextureProvider(texture),
                new NoopFacilityRelocationWorldService(),
                EmptyGameSessionStateStore.Instance,
                gridProvider,
                RestoreCandidates);
            IDungeonSaveSection foundation = new DependencySection(
                FoundationSessionSaveSection.Id,
                DungeonSaveRestorePhase.Foundation);
            IDungeonSaveSection runVariables = new DependencySection(
                RunVariableSaveSection.Id,
                DungeonSaveRestorePhase.Foundation);
            IDungeonSaveSection metaProgression = new DependencySection(
                MetaProgressionSaveSection.Id,
                DungeonSaveRestorePhase.Foundation);
            IDungeonSaveSection facilities = new ModularFacilityWorldSaveSection(
                WorldService,
                gridProvider,
                LifecycleRestoreCandidates);
            IDungeonSaveSection characters = new ProjectionDependencySection<
                DungeonCharacterWorldSaveData>(
                CharacterWorldSaveSection.Id,
                DungeonSaveRestorePhase.Characters,
                new DungeonCharacterWorldSaveData(),
                LifecycleRestoreCandidates.SetCharacters,
                new[] { ModularFacilityWorldSaveSection.Id });
            IDungeonSaveSection combat = new ProjectionDependencySection<
                DungeonCombatEquipmentSaveData>(
                CombatEquipmentSaveSection.Id,
                DungeonSaveRestorePhase.RuntimeState,
                new DungeonCombatEquipmentSaveData(),
                LifecycleRestoreCandidates.SetCombat,
                new[] { PhysicalItemsSaveSection.Id });
            IDungeonSaveSection environment = new ProjectionDependencySection<
                DungeonCharacterEnvironmentSaveData>(
                CharacterEnvironmentSaveSection.Id,
                DungeonSaveRestorePhase.LateRuntimeState,
                new DungeonCharacterEnvironmentSaveData
                {
                    exposures = Array.Empty<CharacterEnvironmentExposure>(),
                    equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>(),
                    equippedApparel = Array.Empty<EquippedApparelSaveData>(),
                    apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
                    apparelWorkOrderTerminalStates =
                        Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
                },
                LifecycleRestoreCandidates.SetEnvironment,
                new[]
                {
                    CharacterWorldSaveSection.Id,
                    PhysicalItemsSaveSection.Id
                });
            IDungeonSaveSection maintenance = new ProjectionDependencySection<
                CombatEquipmentMaintenanceSaveData>(
                EquipmentMaintenanceSaveSection.Id,
                DungeonSaveRestorePhase.RuntimeState,
                new CombatEquipmentMaintenanceSaveData(),
                LifecycleRestoreCandidates.SetMaintenance,
                new[]
                {
                    CombatEquipmentSaveSection.Id,
                    PhysicalItemsSaveSection.Id,
                    ModularFacilityWorldSaveSection.Id
                });
            Registry = new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[]
                {
                    foundation,
                    runVariables,
                    metaProgression,
                    facilities,
                    characters,
                    PhysicalSection,
                    billsSection,
                    routingSection,
                    combat,
                    maintenance,
                    environment
                },
                RootStore,
                new IDungeonRestoreTransactionParticipant[]
                {
                    LifecycleRestoreCandidates,
                    WorldService,
                    exactRoute,
                    WorldItems,
                    Claims,
                    Admission,
                    PreparedOutput,
                    Routing
                });
        }

        internal ScenarioConfig Config { get; }
        internal DungeonRuntimeAggregateRootStore RootStore { get; }
        internal ProductionOutputLifecycleRestoreCandidateIndex
            LifecycleRestoreCandidates { get; }
        internal Grid Grid => gridProvider.Grid;
        internal RestoreWorldCandidateIndex RestoreCandidates { get; }
        internal ResourceEconomyContentCatalog Economy { get; }
        internal ProductionRecipeSO Recipe { get; }
        internal IDungeonItemCatalogProvider ItemCatalog { get; }
        internal IPhysicalItemMassQuery Mass { get; }
        internal IBuildingDefinitionLookup BuildingDefinitions { get; }
        internal ProductionOutputBufferCapacityProjector CapacityProjector { get; }
        internal IGameClock Clock { get; }
        internal BuildableObject Facility { get; }
        internal BuildableObject CurrentFacility => BuildingWorld.Buildings
                .Single(value => value != null
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    Config.FacilityId,
                    StringComparison.Ordinal));
        internal CharacterActor Worker { get; }
        internal IBuildingWorldQuery BuildingWorld { get; }
        internal IProductionAssemblyBridge Bridge { get; }
        internal ProductionFacilityHandle FacilityHandle =>
            Bridge.CaptureFacility(CurrentFacility);
        internal ProductionWorkerHandle WorkerHandle { get; }
        internal WorldItemRepository Repository { get; }
        internal IWorldItemSpawner Spawner { get; }
        internal ItemQuantityReservationService QuantityReservations { get; }
        internal ItemTransferService Transfers { get; }
        internal WorldItemStackRuntime WorldItems { get; }
        internal ProductionItemGateway Items { get; }
        internal FacilityBufferDestinationClaimRegistry Claims { get; }
        internal FacilityBufferMassAdmissionService Admission { get; }
        internal FacilityBufferPhysicalOccupancyQuery Occupancy { get; }
        internal ProductionStockSensorDestinationAuthorityRuntime
            SensorAuthority { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication { get; }
        internal ProductionPreparedOutputRoutingAuthority Routing { get; }
        internal ProductionPreparedOutputExecutionAdapter PreparedOutput { get; }
        internal ProductionAggregateStateStore State { get; }
        internal ProductionStockSensorRuntime StockSensors { get; }
        internal ProductionBillRuntime Production { get; }
        internal ProductionOutputDestinationLifecycleQuery Lifecycle { get; }
        internal ModularFacilityWorldSaveService WorldService { get; }
        internal PhysicalItemsSaveSection PhysicalSection { get; }
        internal DungeonSaveSectionRegistry Registry { get; }
        internal ProductionBillRecord SingleBill => State.Bills.Single();

        internal void SeedOwnedCurrentFormatBill()
        {
            const string billId = "production-bill:1";
            DungeonProductionBillSaveData seed = new()
            {
                version = DungeonProductionBillSaveData.CurrentVersion,
                nextBillSequence = 2,
                bills = new List<ProductionBillSaveData>
                {
                    new()
                    {
                        billId = billId,
                        recipeId = Config.RecipeId,
                        buildingInstanceId = Config.FacilityId,
                        mode = ProductionOrderMode.RepeatCount,
                        remainingCycles = 1,
                        targetStock = 0,
                        cycleSequence = 1,
                        materialDestinationId =
                            ProductionBillRuntime.DestinationPrefix + billId,
                        outputDestinationId =
                            ProductionBillRuntime.OutputDestinationPrefix
                            + Config.FacilityId,
                        preparedOutput =
                            new ProductionPreparedOutputBatchSaveData()
                    }
                }
            };
            Require(
                seed.version == DungeonProductionBillSaveData.CurrentVersion
                && seed.version == 22,
                "Production seed is not current V22.");
            Production.Restore(Production.BuildRestore(seed));
            Require(
                Production.Capture().bills.Count == 1,
                "Public BuildRestore/Restore did not retain one owned bill.");
        }

        internal void PrepareWip(ProductionBillRecord record)
        {
            ItemAmountDefinition[] inputs = Recipe.Inputs
                .Where(value => value != null)
                .ToArray();
            int inputQuantity = inputs.Sum(value => value.Amount);
            long inputMass = inputs.Aggregate(
                0L,
                (sum, value) => checked(sum + Mass.GetQuantityMass(
                    (ItemDefinitionId)value.ItemId,
                    PhysicalItemMassSubject.ForDefinition(
                        (ItemDefinitionId)value.ItemId),
                    value.Amount).Value));
            Require(
                inputQuantity > 0 && inputMass > 0L,
                Config.Label + " WIP input authority is empty.");
            record.SetMaterialsConsumed(true);
            record.SetWipInput(new ProductionWipInputReceipt(
                "physical-batch-disposition:1:production-wip-input:"
                + record.billId.Value + ":00000001:"
                + inputQuantity + ":" + inputMass,
                inputQuantity,
                inputMass));

            long cleanWaterMass = ProductionFluidMassRules.ToMassGrams(
                Recipe.CleanWaterPerCycle);
            long wastewaterMass = ProductionFluidMassRules.ToMassGrams(
                Recipe.WastewaterPerCycle);
            if (cleanWaterMass > 0L || wastewaterMass > 0L)
            {
                IReadOnlyList<ProcessWastewaterComponent>
                    wastewaterComponents = wastewaterMass > 0L
                        ? new[]
                        {
                            new ProcessWastewaterComponent(
                                Recipe.WastewaterComposition,
                                ProcessWastewaterSourceKind.Recipe,
                                Recipe.RecipeId,
                                Recipe.WastewaterPerCycle)
                        }
                        : Array.Empty<ProcessWastewaterComponent>();
                record.SetProcessFluidConsumed(true);
                record.SetProcessFluid(new ProductionProcessFluidReceipt(
                    cleanWaterMass,
                    wastewaterMass,
                    wastewaterComponents: wastewaterComponents));
            }
        }

        public void Dispose()
        {
            WorldItems?.Dispose();
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(researchObject);
            HashSet<GameObject> facilityObjects = new();
            if (facilityObject != null)
                facilityObjects.Add(facilityObject);
            if (gridProvider?.Grid != null)
            {
                foreach (BuildableObject building in gridProvider.Grid
                             .FindAllOccupants(null)
                             .OfType<BuildableObject>())
                {
                    if (building != null)
                        facilityObjects.Add(building.gameObject);
                }
            }
            foreach (GameObject value in facilityObjects)
            {
                if (value != null)
                    UnityEngine.Object.DestroyImmediate(value);
            }
            if (textureObject != null)
                UnityEngine.Object.DestroyImmediate(textureObject);
        }

        private sealed class FixedBuildingDefinitionLookup : IBuildingDefinitionLookup
        {
            private readonly BuildingSO definition;

            internal FixedBuildingDefinitionLookup(BuildingSO definition) =>
                this.definition = definition;

            public BuildingSO GetBuilding(int id)
            {
                if (definition == null || definition.id != id)
                    throw new InvalidOperationException(
                        "Full-persistence building definition fixture mismatch.");
                return definition;
            }
        }
    }

    private static Grid CreateFacilityWorldGrid()
    {
        Grid grid = new(
            32,
            DungeonSpaceExpansionCatalog.SupportedGridHeight);
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                Vector2Int position = new(x, y);
                GridCellAreaType area = x
                    < DungeonSpaceExpansionCatalog.InitialInteriorColumns
                    ? x == 0 && y == 0
                        ? GridCellAreaType.Entrance
                        : GridCellAreaType.DungeonInterior
                    : GridCellAreaType.BlockedExterior;
                grid.SetAreaType(position, area);
            }
        }
        return grid;
    }

    private static void InjectBuilding(BuildableObject building)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        CharacterAiEditorTestDependencies.Inject(building);
    }

    private static IReadOnlyList<T> LoadAll<T>(string folder)
        where T : UnityEngine.Object => AssetDatabase.FindAssets(
            "t:" + typeof(T).Name,
            new[] { folder })
        .Select(AssetDatabase.GUIDToAssetPath)
        .Select(AssetDatabase.LoadAssetAtPath<T>)
        .Where(value => value != null)
        .ToArray();

    private sealed class DependencySection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        internal DependencySection(
            string id,
            DungeonSaveRestorePhase phase)
        {
            SectionId = id;
            RestorePhase = phase;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";
        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != 1
                || !string.Equals(payloadJson, "{}", StringComparison.Ordinal))
            {
                report.AddError("Invalid dependency fixture payload: " + SectionId);
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) => StageRestore(
            payloadJson,
            sectionVersion,
            report).Commit(report);

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
    }

    private sealed class ProjectionDependencySection<TPayload> :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
        where TPayload : class
    {
        private readonly TPayload payload;
        private readonly Action<TPayload> publish;

        internal ProjectionDependencySection(
            string id,
            DungeonSaveRestorePhase phase,
            TPayload payload,
            Action<TPayload> publish,
            IReadOnlyList<string> dependsOn)
        {
            SectionId = id;
            RestorePhase = phase;
            this.payload = payload ?? throw new ArgumentNullException(nameof(payload));
            this.publish = publish ?? throw new ArgumentNullException(nameof(publish));
            DependsOn = dependsOn ?? Array.Empty<string>();
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn { get; }
        public string Capture() => JsonUtility.ToJson(payload);

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || JsonUtility.FromJson<TPayload>(payloadJson) == null)
            {
                report.AddError(
                    "Invalid projection dependency payload: " + SectionId);
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) => StageRestore(
                payloadJson,
                sectionVersion,
                report).Commit(report);

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            TPayload candidate = JsonUtility.FromJson<TPayload>(payloadJson)
                ?? throw new InvalidOperationException(
                    "Projection dependency payload deserialized to null: "
                    + SectionId);
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ => publish(candidate));
        }
    }

    private sealed class NoopDrainCandidateValidator :
        IProductionFacilityDestructiveDrainCandidateValidator
    {
        public void Validate(
            ProductionOutputLifecycleRestoreCandidateBundle bundle,
            DungeonProductionGenericBillTerminalDrainSaveData genericTerminalDrains,
            DungeonCombatEquipmentTerminalDrainSaveData combatTerminalDrains,
            DungeonProductionApparelOrderTerminalDrainSaveData apparelTerminalDrains,
            DungeonProductionFacilityDestructiveDrainSaveData drain)
        {
        }
    }

    private sealed class MutableGridProvider :
        IGridSystemProvider,
        IGridSystemPublisher
    {
        internal MutableGridProvider(Grid grid)
        {
            Grid = grid;
        }

        public GridSystemManager Manager => null;
        public Grid Grid { get; private set; }
        internal int Revision { get; private set; }
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return true;
        }

        public bool TryPublishGrid(
            Grid expectedCurrent,
            Grid replacement,
            out string failureReason)
        {
            if (!ReferenceEquals(Grid, expectedCurrent)
                || replacement == null)
            {
                failureReason = "Facility persistence grid publication expectation changed.";
                return false;
            }
            Grid = replacement;
            Revision = checked(Revision + 1);
            failureReason = string.Empty;
            return true;
        }

        public void CompleteGridPublication()
        {
        }
    }

    private sealed class CandidateAwareBuildingWorldQuery : IBuildingWorldQuery
    {
        private readonly MutableGridProvider gridProvider;
        private readonly IRestoreWorldCandidateQuery candidates;

        internal CandidateAwareBuildingWorldQuery(
            MutableGridProvider gridProvider,
            IRestoreWorldCandidateQuery candidates)
        {
            this.gridProvider = gridProvider
                ?? throw new ArgumentNullException(nameof(gridProvider));
            this.candidates = candidates
                ?? throw new ArgumentNullException(nameof(candidates));
        }

        public int BuildingVersion => gridProvider.Revision;

        public IReadOnlyList<BuildableObject> Buildings
        {
            get
            {
                if (candidates.TryGetBuildings(
                        out IReadOnlyList<BuildableObject> candidateBuildings))
                {
                    return candidateBuildings;
                }
                return gridProvider.Grid.FindAllOccupants(null)
                    .OfType<BuildableObject>()
                    .Where(value => value != null && !value.IsGridDestroyed)
                    .Distinct()
                    .OrderBy(
                        value => value.PersistentInstanceId.Value,
                        StringComparer.Ordinal)
                    .ToArray();
            }
        }
    }

    private sealed class StaticGridTextureProvider : IGridTextureProvider
    {
        internal StaticGridTextureProvider(GridTexture texture)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        }

        public GridTexture Texture { get; }
    }

    private sealed class NoopFacilityRelocationWorldService :
        IFacilityRelocationWorldService
    {
        public bool CanRelocate(
            BuildableObject source,
            Vector2Int destination,
            out string failureReason)
        {
            failureReason = "Relocation is outside the persistence fixture.";
            return false;
        }

        public bool TryPackAtDestination(
            BuildableObject source,
            Vector2Int destination,
            out string failureReason)
        {
            failureReason = "Relocation is outside the persistence fixture.";
            return false;
        }

        public bool TryCompleteRelocation(
            BuildableObject packedSource,
            out BuildableObject relocated,
            out string failureReason)
        {
            relocated = null;
            failureReason = "Relocation is outside the persistence fixture.";
            return false;
        }

        public void RestorePackedPresentation(BuildableObject packedSource)
        {
        }
    }

    private sealed class EmptyGameSessionStateStore : IGameSessionStateStore
    {
        internal static readonly EmptyGameSessionStateStore Instance = new();

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = null;
            return false;
        }

        public void Restore(GameSessionSnapshot snapshot) =>
            throw new InvalidOperationException(
                "Session restore is outside the persistence fixture.");
    }

    private sealed class EmptyCombatEquipmentQueue :
        ICombatEquipmentCraftQueueQuery
    {
        internal static readonly EmptyCombatEquipmentQueue Instance = new();
        public IReadOnlyList<CombatEquipmentCraftOrderSaveData> CraftQueue { get; }
            = Array.Empty<CombatEquipmentCraftOrderSaveData>();
    }

    private sealed class EmptyMaintenanceOrders :
        ICombatEquipmentMaintenanceOrderQuery
    {
        internal static readonly EmptyMaintenanceOrders Instance = new();
        public IReadOnlyList<CombatEquipmentRepairOrder> Orders { get; }
            = Array.Empty<CombatEquipmentRepairOrder>();
    }

    private sealed class EmptyApparelWorkOrders : IApparelWorkOrderQuery
    {
        internal static readonly EmptyApparelWorkOrders Instance = new();
        public int Version => 0;
        public IReadOnlyList<ApparelWorkOrderSaveData> Orders { get; }
            = Array.Empty<ApparelWorkOrderSaveData>();
    }

    private sealed class EmptyExactRouteOutbox :
        IFacilityOutputExactRouteOutboxQuery
    {
        internal static readonly EmptyExactRouteOutbox Instance = new();

        public IReadOnlyList<FacilityOutputExactRoutePendingSnapshot>
            CapturePendingRoutes() =>
            Array.Empty<FacilityOutputExactRoutePendingSnapshot>();
    }

    private sealed class FixedCharacterIdRegistry : ICharacterIdRegistry
    {
        public bool TryGetPersistentId(
            CharacterActor actor,
            out string persistentId)
        {
            persistentId = actor?.BuildingCharacterId.Value ?? string.Empty;
            return persistentId.Length > 0;
        }

        public string GetOrAssignPersistentId(CharacterActor actor) =>
            actor?.BuildingCharacterId.Value
            ?? throw new ArgumentNullException(nameof(actor));
    }

    private sealed class FixedHaulingSettings : IItemHaulingSettingsProvider
    {
        public float MaxCarryMultiplier { get; private set; } = 1.5f;
        public ItemHaulingSettingsSnapshot Capture() => new()
        {
            maxCarryMultiplier = MaxCarryMultiplier
        };

        public void Restore(ItemHaulingSettingsSnapshot snapshot)
        {
            snapshot?.Normalize();
            MaxCarryMultiplier = snapshot?.maxCarryMultiplier ?? 1.5f;
        }
    }

    private sealed class EmptySpawnerProvider : ICharacterSpawnerProvider
    {
        public bool TryGetSpawner(out CharacterSpawner spawner)
        {
            spawner = null;
            return false;
        }
    }

    private sealed class EmptyDropZoneQuery : IWorldDropZoneQuery
    {
        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }

        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class EmptyWarehouseQuery : IWarehouseWorldQuery
    {
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses { get; } =
            Array.Empty<IWarehouseFacility>();
    }

    private sealed class EmptyWorkforceReplanService :
        IWorkforceReplanService
    {
        public void RequestIdleWorkersToReplan(bool clearFailures = true) { }
        public void RequestOneWorkerToReplanFor(
            WorkTypeId workTypeId,
            bool clearFailures = true,
            bool forceInterrupt = false) { }
        public void RequestOneHaulerToReplan(
            bool clearFailures = true,
            bool forceInterrupt = false,
            CharacterId protectedCharacterId = default,
            bool forcePriorityWakeFanout = false) { }
    }

    private sealed class EmptyWorkshopRuntime : IProductionWorkshopRuntime
    {
        public int Version => 0;
        public IReadOnlyList<ProductionSupportLinkSnapshot> GetLinks(
            BuildableObject workstation) =>
            Array.Empty<ProductionSupportLinkSnapshot>();

        public bool TryGetLinkForSupport(
            BuildableObject support,
            out ProductionSupportLinkSnapshot link)
        {
            link = null;
            return false;
        }

        public bool HasRequiredSupports(
            BuildableObject workstation,
            IReadOnlyList<string> requiredFeatureTags,
            out string failureReason)
        {
            bool valid = requiredFeatureTags == null
                || requiredFeatureTags.All(string.IsNullOrWhiteSpace);
            failureReason = valid ? string.Empty : "fixture-support-missing";
            return valid;
        }

        public bool TryResolveSupport(
            BuildableObject workstation,
            string featureTag,
            ProductionSupportKind? requiredKind,
            out BuildableObject support,
            out BuildingProductionSupportAbility ability)
        {
            support = null;
            ability = null;
            return false;
        }
    }

    private sealed class WorkOnlyCycleUtilities :
        IProductionCycleUtilityService
    {
        private readonly IProductionWorkshopRuntime workshops;

        internal WorkOnlyCycleUtilities(IProductionWorkshopRuntime workshops)
        {
            this.workshops = workshops;
        }

        public bool ValidateCycleRequirements(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            IReadOnlyList<ProductionBillRecord> allBills,
            out string failureReason) => workshops.HasRequiredSupports(
            facility,
            recipe.RequiredSupportTags,
            out failureReason);

        public bool ValidateProcessingUtilities(
            string occupiedSupportNodeId,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryConsumeCycleUtilities(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out ProductionProcessFluidReceipt receipt,
            out string failureReason)
        {
            receipt = new ProductionProcessFluidReceipt(
                0L,
                0L,
                Array.Empty<ProductionManualWaterTransferSaveData>(),
                Array.Empty<ProcessWastewaterComponent>());
            failureReason = string.Empty;
            return true;
        }

        public bool AcknowledgeCycleUtilities(
            ProductionProcessFluidReceipt receipt,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryResolveBatchSupport(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            IReadOnlyList<ProductionBillRecord> allBills,
            out string supportNodeId,
            out string failureReason)
        {
            supportNodeId = string.Empty;
            failureReason = "fixture-batch-support-unavailable";
            return false;
        }

        public float ResolveTemperatureSpeed(
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out bool dangerous)
        {
            dangerous = false;
            return 1f;
        }

        public BuildableObject ResolveOccupiedBatchSupport(
            string occupiedSupportNodeId,
            BuildableObject facility) => null;
    }

    private sealed class EmptyGrandProjectBenefits :
        IGrandProjectBenefitQuery
    {
        public float ContractRewardMultiplier => 1f;
        public float DefensePreparationMultiplier => 1f;
        public int ExpeditionSupplyCapacityBonus => 0;
        public bool IsCompleted(string projectId) => false;
        public float GetProductionOutputMultiplier(string facilityTag) => 1f;
    }

    private sealed class EmptyDistributionQuery : IProductionDistributionQuery
    {
        public IReadOnlyList<ProductionConsumerRouteState> GetRouteStates(
            ProductionBillId billId) =>
            Array.Empty<ProductionConsumerRouteState>();
    }

    private sealed class EmptyExactRouteReconciler :
        IFacilityOutputExactRouteRestoreReconciler
    {
        public void AcknowledgeRestoredRoute(
            string routeOperationId,
            string physicalReceiptFingerprint)
        {
        }
    }
}
#endif
