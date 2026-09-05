#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ProductionStockSensorDestructiveDrainParticipantDebugScenarios
{
    private static readonly BuildingInstanceId FacilityId =
        (BuildingInstanceId)"building:qa-stock-sensor-composite";
    private static readonly ProductionFacilityDestructiveDrainOperationId
        OperationId = ProductionFacilityDestructiveDrainOperationId
            .FromFacility(FacilityId);

    [MenuItem(
        "DungeonStory/Debug/Economy/Run Stock Sensor Composite Destructive Drain Contracts")]
    public static void RunFromMenu() => RunAll();

    public static void RunAll()
    {
        VerifyZeroOwnerIsMutationFree();
        VerifyPhysicalOnly();
        VerifyEmbeddedOnly();
        VerifyCombined();
        VerifyPendingInstallationStabilizes();
        VerifyPublishFailureRecoversWithoutDuplicateChildEffect();
        VerifyAuthorityAndChildDriftFailClosed();
        VerifyCrossSaveExactJoin();
        VerifyPhysicalOnlyCheckpointGc();
        VerifyEmbeddedAndCombinedCheckpointGcTransaction();
        VerifyCheckpointGcSourcePublishFailureRollsBackChild();
        VerifySavePhysicalCustodyOwnership();
        Debug.Log(
            "Stock-sensor composite destructive-drain participant contracts passed.");
    }

    private static void VerifyZeroOwnerIsMutationFree()
    {
        Fixture fixture = Fixture.Create(false, SensorFixtureState.None);
        ProductionFacilityDestructiveDrainParticipantPlan first =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainParticipantPlan second =
            fixture.Participant.Prepare(CreatePrepareContext());

        Require(
            first.Owners.Count == 0
            && second.Owners.Count == 0
            && string.Equals(first.PlanFingerprint, second.PlanFingerprint,
                StringComparison.Ordinal)
            && fixture.Input.PrepareCalls == 0
            && fixture.Sensor.PrepareCalls == 0,
            "An empty sensor socket synthesized a destructive owner or mutated state.");
    }

    private static void VerifyPhysicalOnly()
    {
        Fixture fixture = Fixture.Create(true, SensorFixtureState.None);
        RunCompleteLifecycle(fixture, expectPhysicalMass: 1_000L,
            expectEmbedded: false);
    }

    private static void VerifyEmbeddedOnly()
    {
        Fixture fixture = Fixture.Create(false, SensorFixtureState.Installed);
        RunCompleteLifecycle(fixture, expectPhysicalMass: 0L,
            expectEmbedded: true);
    }

    private static void VerifyCombined()
    {
        Fixture fixture = Fixture.Create(true, SensorFixtureState.Installed);
        RunCompleteLifecycle(fixture, expectPhysicalMass: 1_000L,
            expectEmbedded: true);
    }

    private static void VerifyPendingInstallationStabilizes()
    {
        Fixture fixture = Fixture.Create(false, SensorFixtureState.Pending);
        ProductionFacilityDestructiveDrainOwnerSaveData owner =
            PrepareDurableOwner(fixture);
        Require(
            fixture.Sensor.StabilizeCalls == 0
            && fixture.Sensor.PrepareCalls == 0,
            "Durable prepare prematurely stabilized a pending installation.");

        ProductionFacilityDestructiveDrainStepResult committed =
            fixture.Participant.TryCommit(CreateStepContext(fixture, owner));
        Require(
            committed.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied
            && fixture.Sensor.StabilizeCalls == 1
            && fixture.Sensor.PrepareCalls == 1
            && fixture.Sensor.PublishCalls == 1
            && fixture.Sensor.Removal?.phase ==
                ProductionStockSensorRemovalPhase.OutputPublished,
            "Pending installation did not stabilize into one embedded salvage effect.");

        AcknowledgeUpper(fixture, owner, committed);
        Require(
            fixture.Sensor.Removal?.phase ==
                ProductionStockSensorRemovalPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
            "Pending installation terminal tombstone was not acknowledged.");
    }

    private static void VerifyPublishFailureRecoversWithoutDuplicateChildEffect()
    {
        Fixture fixture = Fixture.Create(true, SensorFixtureState.Installed);
        fixture.Sensor.PublishFailuresRemaining = 1;
        ProductionFacilityDestructiveDrainOwnerSaveData owner =
            PrepareDurableOwner(fixture);

        ProductionFacilityDestructiveDrainStepResult first =
            fixture.Participant.TryCommit(CreateStepContext(fixture, owner));
        Require(
            first.Status == ProductionFacilityDestructiveDrainStepStatus.Deferred
            && fixture.Input.TerminalEffectCount == 1
            && fixture.Sensor.PublishCalls == 1
            && fixture.Sensor.Removal?.phase ==
                ProductionStockSensorRemovalPhase.Prepared,
            "A sensor publish failure did not retain the committed physical child and prepared salvage.");

        ProductionFacilityDestructiveDrainRecoveryResult recovery =
            fixture.Participant.Recover(CreateStepContext(fixture, owner));
        Require(
            recovery.Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            "Prepared sensor salvage did not request forward commit recovery.");

        ProductionFacilityDestructiveDrainStepResult resumed =
            fixture.Participant.TryCommit(CreateStepContext(fixture, owner));
        Require(
            resumed.Status == ProductionFacilityDestructiveDrainStepStatus.Applied
            && fixture.Input.TerminalEffectCount == 1
            && fixture.Sensor.PublishCalls == 2
            && fixture.Sensor.SuccessfulPublicationCount == 1,
            "Publish retry duplicated the physical child or failed to publish one salvage.");
        AcknowledgeUpper(fixture, owner, resumed);
    }

    private static void VerifyAuthorityAndChildDriftFailClosed()
    {
        Fixture lifecycle = Fixture.Create(true, SensorFixtureState.Pending);
        ProductionFacilityDestructiveDrainParticipantPlan lifecyclePlan =
            lifecycle.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainOwnerSaveData lifecycleOwner =
            CreateOwner(lifecycle, lifecyclePlan);
        lifecycle.Lifecycle.DurableFingerprint = Digest('f');
        Require(
            !lifecycle.Participant.TryPrepareDurable(
                CreateStepContext(
                    lifecycle,
                    lifecycleOwner,
                    lifecyclePlan.DurableContributionFingerprint),
                out string lifecycleFailure)
            && lifecycleFailure.Contains("contribution-drift",
                StringComparison.Ordinal)
            && lifecycle.Input.PrepareCalls == 0
            && lifecycle.Sensor.PrepareCalls == 0
            && lifecycle.Sensor.StabilizeCalls == 0,
            "A changed durable sensor contribution reached a lower owner.");

        Fixture authority = Fixture.Create(true, SensorFixtureState.Installed);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            authority.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainOwnerSaveData owner =
            CreateOwner(authority, plan);
        authority.Capacity.AuthorityFingerprint = Digest('d');
        Require(
            !authority.Participant.TryPrepareDurable(
                CreateStepContext(authority, owner),
                out string authorityFailure)
            && authorityFailure.Contains("plan-drift", StringComparison.Ordinal)
            && authority.Input.PrepareCalls == 0
            && authority.Sensor.PrepareCalls == 0,
            "A changed socket authority fingerprint was accepted after upper planning.");

        Fixture child = Fixture.Create(true, SensorFixtureState.Installed);
        ProductionFacilityDestructiveDrainOwnerSaveData childOwner =
            PrepareDurableOwner(child);
        child.Input.TamperValidSourceClaim(
            childOwner.stepOperationId + ":input-destination-custody",
            Digest('e'));
        ProductionFacilityDestructiveDrainStepResult rejected =
            child.Participant.TryCommit(CreateStepContext(child, childOwner));
        Require(
            rejected.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict
            && child.Input.TerminalEffectCount == 0
            && child.Sensor.PublishCalls == 0,
            "A valid but mismatched child request was deferred or mutated instead of failing closed.");
    }

    private static void VerifyCrossSaveExactJoin()
    {
        Fixture physical = Fixture.Create(true, SensorFixtureState.None);
        ProductionFacilityDestructiveDrainOwnerSaveData physicalOwner =
            PrepareDurableOwner(physical);
        ValidateCrossSaveJoin(physical, physicalOwner);

        Fixture embedded = Fixture.Create(false, SensorFixtureState.Installed);
        ProductionFacilityDestructiveDrainOwnerSaveData embeddedOwner =
            PrepareDurableOwner(embedded);
        ValidateCrossSaveJoin(embedded, embeddedOwner);
        ProductionFacilityDestructiveDrainStepResult committed =
            embedded.Participant.TryCommit(
                CreateStepContext(embedded, embeddedOwner));
        Require(committed.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied,
            "Embedded cross-save fixture did not commit.");
        embeddedOwner.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        embeddedOwner.commitId = committed.CommitId;
        embeddedOwner.receiptFingerprint = committed.ReceiptFingerprint;
        ValidateCrossSaveJoin(embedded, embeddedOwner);

        ProductionFacilityDestructiveDrainOwnerSaveData missingChildOwner =
            embeddedOwner.Clone();
        RequireCrossSaveFailure(
            CreateEntry(missingChildOwner),
            embedded.Sensor.CaptureProduction(),
            Array.Empty<ProductionInputDestinationCustodyDrainSaveData>(),
            "A committed stock-sensor upper owner accepted a missing child.");

        ProductionFacilityDestructiveDrainOwnerSaveData tamperedTerminal =
            embeddedOwner.Clone();
        tamperedTerminal.receiptFingerprint = Digest('0');
        RequireCrossSaveFailure(
            CreateEntry(tamperedTerminal),
            embedded.Sensor.CaptureProduction(),
            embedded.Input.CaptureAll(),
            "A stock-sensor upper owner accepted a mismatched composite receipt.");

        AcknowledgeUpper(embedded, embeddedOwner, committed);
        embeddedOwner.phase =
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged;
        ValidateCrossSaveJoin(embedded, embeddedOwner);

        ProductionInputDestinationCustodyDrainSaveData tamperedChild =
            embedded.Input.CaptureAll().Single().Clone();
        tamperedChild.sourceDestinationId = "production-sensor:wrong-facility";
        RequireCrossSaveFailure(
            CreateEntry(embeddedOwner),
            embedded.Sensor.CaptureProduction(),
            new[] { tamperedChild },
            "A stock-sensor upper owner accepted a mismatched child identity.");
    }

    private static void VerifyPhysicalOnlyCheckpointGc()
    {
        Fixture fixture = Fixture.Create(true, SensorFixtureState.None);
        ProductionFacilityDestructiveDrainOwnerSaveData owner =
            AdvanceToCheckpointGc(fixture);
        string childStep = ProductionStockSensorDestructiveDrainCanonical
            .BuildChildStepOperationId(owner.stepOperationId);
        ProductionFacilityDestructiveDrainCheckpointGcContext context =
            CreateCheckpointGcContext(1L);
        ProductionFacilityDestructiveDrainCheckpointGcResult prepared = fixture
            .Participant.PrepareCheckpointGarbageCollection(
                context,
                new[] { CreateEntry(owner) },
                out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                    candidate);
        Require(
            prepared.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && fixture.Input.TryCapture(childStep, out _)
            && fixture.Sensor.Removal == null,
            "P0_SENSOR_GC_PHYSICAL_ONLY: prepare rejected or mutated an Items-only owner.");

        ProductionFacilityDestructiveDrainCheckpointGcResult published = fixture
            .Participant.PublishCheckpointGarbageCollection(candidate);
        Require(
            published.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && !fixture.Input.TryCapture(childStep, out _)
            && fixture.Sensor.Removal == null
            && fixture.Input.CheckpointGcPublishCount == 1
            && fixture.Sensor.CheckpointGcPublishCount == 1,
            "P0_SENSOR_GC_PHYSICAL_ONLY: Items-only checkpoint GC did not collect exactly its child.");
        fixture.Participant.CompleteCheckpointGarbageCollection(candidate);
    }

    private static void VerifyEmbeddedAndCombinedCheckpointGcTransaction()
    {
        foreach (bool physical in new[] { false, true })
        {
            Fixture fixture = Fixture.Create(
                physical,
                SensorFixtureState.Installed);
            ProductionFacilityDestructiveDrainOwnerSaveData owner =
                AdvanceToCheckpointGc(fixture);
            string childStep = ProductionStockSensorDestructiveDrainCanonical
                .BuildChildStepOperationId(owner.stepOperationId);
            ProductionInputDestinationCustodyDrainSaveData originalChild =
                fixture.Input.CaptureAll().Single().Clone();
            ProductionStockSensorRemovalSaveData originalRemoval =
                fixture.Sensor.Removal.Clone();
            ProductionFacilityDestructiveDrainCheckpointGcContext context =
                CreateCheckpointGcContext(physical ? 3L : 2L);
            ProductionFacilityDestructiveDrainCheckpointGcResult prepared =
                fixture.Participant.PrepareCheckpointGarbageCollection(
                    context,
                    new[] { CreateEntry(owner) },
                    out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                        candidate);
            Require(
                prepared.Status ==
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
                && fixture.Input.TryCapture(childStep, out _)
                && fixture.Sensor.Removal != null,
                "P0_SENSOR_GC_EMBEDDED_TRANSACTION: prepare mutated a lower row.");

            ProductionFacilityDestructiveDrainCheckpointGcResult published =
                fixture.Participant.PublishCheckpointGarbageCollection(candidate);
            Require(
                published.Status ==
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
                && !fixture.Input.TryCapture(childStep, out _)
                && fixture.Sensor.Removal == null,
                "P0_SENSOR_GC_EMBEDDED_TRANSACTION: publish did not collect child then removal.");

            fixture.Participant.RollbackCheckpointGarbageCollection(candidate);
            Require(
                fixture.Input.TryCapture(
                    childStep,
                    out ProductionInputDestinationCustodyDrainSaveData
                        restoredChild)
                && fixture.Sensor.Removal != null
                && RowsEqual(restoredChild, originalChild)
                && RowsEqual(fixture.Sensor.Removal, originalRemoval)
                && fixture.Sensor.CheckpointGcRollbackCount == 1
                && fixture.Input.CheckpointGcRollbackCount == 1,
                "P0_SENSOR_GC_EMBEDDED_TRANSACTION: rollback did not restore exact lower rows.");

            Require(
                fixture.Participant.PublishCheckpointGarbageCollection(candidate)
                    .Status ==
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
                && !fixture.Input.TryCapture(childStep, out _)
                && fixture.Sensor.Removal == null,
                "P0_SENSOR_GC_EMBEDDED_TRANSACTION: a rolled-back candidate could not republish exactly.");
            fixture.Participant.CompleteCheckpointGarbageCollection(candidate);
        }
    }

    private static void VerifyCheckpointGcSourcePublishFailureRollsBackChild()
    {
        Fixture fixture = Fixture.Create(true, SensorFixtureState.Installed);
        ProductionFacilityDestructiveDrainOwnerSaveData owner =
            AdvanceToCheckpointGc(fixture);
        string childStep = ProductionStockSensorDestructiveDrainCanonical
            .BuildChildStepOperationId(owner.stepOperationId);
        ProductionInputDestinationCustodyDrainSaveData originalChild = fixture
            .Input.CaptureAll().Single().Clone();
        ProductionStockSensorRemovalSaveData originalRemoval = fixture.Sensor
            .Removal.Clone();
        fixture.Sensor.FailNextCheckpointGcPublish = true;
        ProductionFacilityDestructiveDrainCheckpointGcContext context =
            CreateCheckpointGcContext(4L);
        Require(
            fixture.Participant.PrepareCheckpointGarbageCollection(
                context,
                new[] { CreateEntry(owner) },
                out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                    candidate).Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            "P0_SENSOR_GC_SOURCE_FAILURE: prepare failed.");

        ProductionFacilityDestructiveDrainCheckpointGcResult failed = fixture
            .Participant.PublishCheckpointGarbageCollection(candidate);
        Require(
            failed.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred
            && !fixture.Input.TryCapture(childStep, out _)
            && RowsEqual(fixture.Sensor.Removal, originalRemoval),
            "P0_SENSOR_GC_SOURCE_FAILURE: fault was not injected after child publication.");

        fixture.Participant.RollbackCheckpointGarbageCollection(candidate);
        Require(
            fixture.Input.TryCapture(
                childStep,
                out ProductionInputDestinationCustodyDrainSaveData restored)
            && RowsEqual(restored, originalChild)
            && RowsEqual(fixture.Sensor.Removal, originalRemoval)
            && fixture.Input.CheckpointGcRollbackCount == 1,
            "P0_SENSOR_GC_SOURCE_FAILURE: child was not restored after source publication failure.");
        fixture.Participant.CompleteCheckpointGarbageCollection(candidate);
    }

    private static void VerifySavePhysicalCustodyOwnership()
    {
        DungeonProductionBillSaveData production = new();
        DungeonPhysicalItemSaveData emptyItems = new();
        DungeonCharacterWorldSaveData emptyCharacters = new();
        string destination = ProductionStockSensorRuntime.BuildDestinationId(
            FacilityId.Value);
        string emptyFingerprint =
            ProductionOutputDestinationDurableSaveProjector.ProjectStockSensor(
                FacilityId,
                production,
                emptyItems,
                emptyCharacters);

        DungeonPhysicalItemSaveData directItems = new();
        directItems.stacks.Add(new WorldItemStackSaveData
        {
            stackId = "stack:qa-stock-sensor-direct",
            itemId = "item:qa-stock-sensor-panel",
            quantity = 1,
            state = WorldItemStackState.FacilityBuffer,
            destinationId = destination,
            gridX = 4,
            gridY = 7
        });
        string directFingerprint =
            ProductionOutputDestinationDurableSaveProjector.ProjectStockSensor(
                FacilityId,
                production,
                directItems,
                emptyCharacters);
        Require(
            !string.Equals(
                directFingerprint,
                emptyFingerprint,
                StringComparison.Ordinal),
            "A physical panel in the exact sensor socket was absent from the durable fingerprint.");
        RequireSensorPlannedOwner(
            production,
            directItems,
            emptyCharacters,
            expected: true,
            "A physical panel in the exact sensor socket did not project a planned owner.");
        RequireAbsentFacilityRejectsSensorCustody(
            production,
            directItems,
            emptyCharacters,
            "An absent facility accepted a physical panel in its sensor socket.");

        const string actorId = "character:qa-stock-sensor-carrier";
        const string operationId = "haul:qa-stock-sensor-carrier";
        const string carriedStackId = "stack:qa-stock-sensor-carried";
        const string sourceStackId = "stack:qa-stock-sensor-source";
        const string itemId = "item:qa-stock-sensor-panel";
        List<ItemInstanceComponentSaveData> components = new();
        string stackSignature = ItemReservationSignature.Create(
            itemId,
            components);
        DungeonPhysicalItemSaveData carriedItems = new();
        carriedItems.stacks.Add(new WorldItemStackSaveData
        {
            stackId = carriedStackId,
            itemId = itemId,
            quantity = 1,
            state = WorldItemStackState.Carried,
            destinationId = actorId,
            components = components
        });
        DungeonCharacterWorldSaveData carriedCharacters = new();
        carriedCharacters.actors.Add(new DungeonCharacterSaveData
        {
            persistentId = actorId,
            carryInventory = new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new()
                    {
                        carriedStackId = carriedStackId,
                        sourceStackId = sourceStackId,
                        ownerOperationId = operationId,
                        itemId = itemId,
                        quantity = 1,
                        components = new List<ItemInstanceComponentSaveData>()
                    }
                }
            },
            haulDeliveryIntent = new HaulDeliveryIntentSaveData
            {
                operationId = operationId,
                ownerCharacterId = actorId,
                destinationKind = WorldItemHaulDestinationKind.FacilityBuffer,
                destinationId = destination,
                commitments = new List<HaulDeliveryItemCommitmentSaveData>
                {
                    new()
                    {
                        carriedStackId = carriedStackId,
                        sourceStackId = sourceStackId,
                        itemId = itemId,
                        expectedStackSignature = stackSignature,
                        quantity = 1
                    }
                }
            }
        });
        string carriedFingerprint =
            ProductionOutputDestinationDurableSaveProjector.ProjectStockSensor(
                FacilityId,
                production,
                carriedItems,
                carriedCharacters);
        Require(
            !string.Equals(
                carriedFingerprint,
                emptyFingerprint,
                StringComparison.Ordinal)
            && !string.Equals(
                carriedFingerprint,
                directFingerprint,
                StringComparison.Ordinal),
            "Committed carried sensor custody was absent or aliased in the durable fingerprint.");
        RequireSensorPlannedOwner(
            production,
            carriedItems,
            carriedCharacters,
            expected: true,
            "Committed carried sensor custody did not project a planned owner.");
        RequireAbsentFacilityRejectsSensorCustody(
            production,
            carriedItems,
            carriedCharacters,
            "An absent facility accepted committed carried sensor custody.");

        WorldItemStackSaveData carriedStack = carriedItems.stacks.Single();
        carriedItems.stacks.Clear();
        RequireStockSensorProjectionFailure(
            production,
            carriedItems,
            carriedCharacters,
            "A committed sensor intent without its physical carried stack was accepted.");
        carriedItems.stacks.Add(carriedStack);

        CharacterCarriedItemSaveData carriedInventoryRow = carriedCharacters
            .actors.Single().carryInventory.items.Single();
        carriedCharacters.actors.Single().carryInventory.items.Clear();
        RequireStockSensorProjectionFailure(
            production,
            carriedItems,
            carriedCharacters,
            "A committed sensor intent without its carried-inventory row was accepted.");
        carriedCharacters.actors.Single().carryInventory.items.Add(
            carriedInventoryRow);

        HaulDeliveryItemCommitmentSaveData carriedCommitment =
            carriedCharacters.actors.Single().haulDeliveryIntent.commitments
                .Single();
        carriedCommitment.expectedStackSignature = "item:qa:wrong-signature";
        RequireStockSensorProjectionFailure(
            production,
            carriedItems,
            carriedCharacters,
            "A committed sensor intent with a drifted stack signature was accepted.");
        carriedCommitment.expectedStackSignature = stackSignature;

        carriedCharacters.actors.Single().haulDeliveryIntent.destinationId =
            "production-sensor:other-facility";
        RequireSensorPlannedOwner(
            production,
            carriedItems,
            carriedCharacters,
            expected: false,
            "An unrelated carried destination synthesized a sensor planned owner.");
        carriedCharacters.actors.Single().haulDeliveryIntent.destinationId =
            destination;

        DungeonPhysicalItemSaveData unrelatedItems = new();
        unrelatedItems.stacks.Add(new WorldItemStackSaveData
        {
            stackId = "stack:qa-stock-sensor-unrelated",
            itemId = itemId,
            quantity = 1,
            state = WorldItemStackState.FacilityBuffer,
            destinationId = "production-sensor:other-facility"
        });
        Require(
            string.Equals(
                ProductionOutputDestinationDurableSaveProjector
                    .ProjectStockSensor(
                        FacilityId,
                        production,
                        unrelatedItems,
                        emptyCharacters),
                emptyFingerprint,
                StringComparison.Ordinal),
            "An unrelated destination changed the sensor durable fingerprint.");
        RequireSensorPlannedOwner(
            production,
            unrelatedItems,
            emptyCharacters,
            expected: false,
            "An unrelated destination synthesized a sensor planned owner.");
    }

    private static void RequireSensorPlannedOwner(
        DungeonProductionBillSaveData production,
        DungeonPhysicalItemSaveData items,
        DungeonCharacterWorldSaveData characters,
        bool expected,
        string message)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> owners =
            ProductionFacilityDestructiveDrainPlannedOwnerSaveProjection
                .Project(
                    FacilityId,
                    production,
                    new DungeonCombatEquipmentSaveData(),
                    new CombatEquipmentMaintenanceSaveData(),
                    new DungeonCharacterEnvironmentSaveData
                    {
                        apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>()
                    },
                    items,
                    characters,
                    new ProductionPreparedOutputRoutingSaveData());
        IReadOnlyList<string> sensorOwners = owners[
            ProductionFacilityDestructiveDrainParticipantIds
                .StockSensorEmbeddedSalvage];
        bool matches = sensorOwners.Count == 1
            && string.Equals(
                sensorOwners[0],
                ProductionFacilityDestructiveDrainOwnerStableIds.StockSensor(
                    FacilityId.Value),
                StringComparison.Ordinal);
        IReadOnlyList<string> physicalOwners = owners[
            ProductionFacilityDestructiveDrainParticipantIds
                .PhysicalCustodyCarryRecovery];
        Require(
            (expected ? matches : sensorOwners.Count == 0)
            && physicalOwners.Count == 0,
            message);
    }

    private static void RequireStockSensorProjectionFailure(
        DungeonProductionBillSaveData production,
        DungeonPhysicalItemSaveData items,
        DungeonCharacterWorldSaveData characters,
        string message)
    {
        try
        {
            ProductionOutputDestinationDurableSaveProjector.ProjectStockSensor(
                FacilityId,
                production,
                items,
                characters);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void RequireAbsentFacilityRejectsSensorCustody(
        DungeonProductionBillSaveData production,
        DungeonPhysicalItemSaveData items,
        DungeonCharacterWorldSaveData characters,
        string message)
    {
        try
        {
            ProductionOutputDestinationDurableSaveProjector
                .ProjectAbsentFacilityAggregateFromSave(
                    FacilityId,
                    new ModularFacilityWorldSaveData(),
                    production,
                    new DungeonCombatEquipmentSaveData(),
                    new CombatEquipmentMaintenanceSaveData(),
                    new DungeonCharacterEnvironmentSaveData
                    {
                        apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>()
                    },
                    items,
                    characters,
                    new ProductionPreparedOutputRoutingSaveData());
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void ValidateCrossSaveJoin(
        Fixture fixture,
        ProductionFacilityDestructiveDrainOwnerSaveData owner)
    {
        ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
            .ValidateStockSensorCompositeProducerJoin(
                CreateEntry(owner),
                fixture.Sensor.CaptureProduction(),
                fixture.Input.CaptureAll(),
                new HashSet<string>(StringComparer.Ordinal));
    }

    private static void RequireCrossSaveFailure(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        DungeonProductionBillSaveData production,
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> children,
        string message)
    {
        try
        {
            ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
                .ValidateStockSensorCompositeProducerJoin(
                    entry,
                    production,
                    children,
                    new HashSet<string>(StringComparer.Ordinal));
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static ProductionFacilityDestructiveDrainEntrySaveData CreateEntry(
        ProductionFacilityDestructiveDrainOwnerSaveData owner) => new()
    {
        operationId = OperationId.Value,
        facilityId = FacilityId.Value,
        phase = ProductionFacilityDestructiveDrainPhase.Prepared,
        participants = new List<
            ProductionFacilityDestructiveDrainParticipantSaveData>
        {
            new()
            {
                participantId = ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage,
                contractVersion = ProductionStockSensorDestructiveDrainParticipant
                    .CurrentContractVersion,
                owners = new List<
                    ProductionFacilityDestructiveDrainOwnerSaveData>
                {
                    owner.Clone()
                }
            }
        }
    };

    private static void RunCompleteLifecycle(
        Fixture fixture,
        long expectPhysicalMass,
        bool expectEmbedded)
    {
        ProductionFacilityDestructiveDrainOwnerSaveData owner =
            PrepareDurableOwner(fixture);
        Require(
            fixture.Input.CapturedInputMass(owner.stepOperationId
                + ":input-destination-custody") == expectPhysicalMass
            && fixture.Input.CapturedPhase(owner.stepOperationId
                + ":input-destination-custody") ==
                ProductionInputDestinationCustodyDrainPhase.Prepared
            && fixture.Sensor.PrepareCalls == (expectEmbedded ? 1 : 0),
            "Durable prepare did not freeze the exact physical/embedded source set.");

        ProductionFacilityDestructiveDrainStepResult committed =
            fixture.Participant.TryCommit(CreateStepContext(fixture, owner));
        Require(
            committed.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied
            && IsTerminal(committed)
            && fixture.Input.TerminalEffectCount == 1
            && fixture.Input.CapturedPhase(owner.stepOperationId
                + ":input-destination-custody") ==
                ProductionInputDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingBillAck
            && (expectEmbedded
                ? fixture.Sensor.Removal?.phase ==
                    ProductionStockSensorRemovalPhase.OutputPublished
                    && fixture.Sensor.SuccessfulPublicationCount == 1
                : fixture.Sensor.Removal == null),
            "Composite commit did not produce one exact lower effect set.");

        ProductionFacilityDestructiveDrainStepResult replay =
            fixture.Participant.TryCommit(CreateStepContext(fixture, owner));
        Require(
            replay.Status == ProductionFacilityDestructiveDrainStepStatus.Replay
            && string.Equals(replay.CommitId, committed.CommitId,
                StringComparison.Ordinal)
            && string.Equals(replay.ReceiptFingerprint,
                committed.ReceiptFingerprint, StringComparison.Ordinal)
            && fixture.Input.TerminalEffectCount == 1
            && fixture.Sensor.SuccessfulPublicationCount ==
                (expectEmbedded ? 1 : 0),
            "Composite commit replay changed its receipt or duplicated a lower effect.");

        AcknowledgeUpper(fixture, owner, committed);
        Require(
            fixture.Input.CapturedPhase(owner.stepOperationId
                + ":input-destination-custody") ==
                ProductionInputDestinationCustodyDrainPhase
                    .BillAcknowledgedAwaitingCheckpointGc
            && (!expectEmbedded
                || fixture.Sensor.Removal?.phase ==
                    ProductionStockSensorRemovalPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc),
            "Upper acknowledgement did not retain acknowledged lower tombstones.");
    }

    private static ProductionFacilityDestructiveDrainOwnerSaveData
        PrepareDurableOwner(Fixture fixture)
    {
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        Require(
            plan.ParticipantId == fixture.Participant.ParticipantId
            && plan.ContractVersion ==
                ProductionStockSensorDestructiveDrainParticipant
                    .CurrentContractVersion
            && plan.Owners.Count == 1,
            "Sensor composite prepare did not produce one deterministic upper owner.");
        ProductionFacilityDestructiveDrainOwnerSaveData owner =
            CreateOwner(fixture, plan);
        Require(
            fixture.Participant.TryPrepareDurable(
                CreateStepContext(fixture, owner),
                out string failureReason),
            "Sensor composite durable prepare failed: " + failureReason);
        return owner;
    }

    private static ProductionFacilityDestructiveDrainOwnerSaveData CreateOwner(
        Fixture fixture,
        ProductionFacilityDestructiveDrainParticipantPlan plan)
    {
        ProductionFacilityDestructiveDrainOwnerPlan owner = plan.Owners.Single();
        return new ProductionFacilityDestructiveDrainOwnerSaveData
        {
            ownerStableId = owner.OwnerStableId,
            disposition = owner.Disposition,
            targetDestinationId = owner.TargetDestinationId,
            stepOperationId = ProductionFacilityDestructiveDrainCanonical
                .BuildStepOperationId(
                    OperationId,
                    fixture.Participant.ParticipantId,
                    owner.OwnerStableId),
            phase = ProductionFacilityDestructiveDrainStepPhase.Planned,
            requestFingerprint = owner.RequestFingerprint
        };
    }

    private static void AcknowledgeUpper(
        Fixture fixture,
        ProductionFacilityDestructiveDrainOwnerSaveData owner,
        ProductionFacilityDestructiveDrainStepResult committed)
    {
        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        owner.commitId = committed.CommitId;
        owner.receiptFingerprint = committed.ReceiptFingerprint;
        ProductionFacilityDestructiveDrainStepResult acknowledged =
            fixture.Participant.TryAcknowledge(
                CreateStepContext(fixture, owner));
        Require(
            acknowledged.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied
            && string.Equals(acknowledged.CommitId, committed.CommitId,
                StringComparison.Ordinal)
            && string.Equals(acknowledged.ReceiptFingerprint,
                committed.ReceiptFingerprint, StringComparison.Ordinal),
            "Composite upper acknowledgement failed.");

        ProductionFacilityDestructiveDrainStepResult replay =
            fixture.Participant.TryAcknowledge(CreateStepContext(fixture, owner));
        Require(
            replay.Status == ProductionFacilityDestructiveDrainStepStatus.Replay
            && fixture.Input.AcknowledgeEffectCount == 1
            && fixture.Sensor.SuccessfulAcknowledgementCount <= 1,
            "Composite acknowledgement replay duplicated a lower acknowledgement.");
    }

    private static ProductionFacilityDestructiveDrainOwnerSaveData
        AdvanceToCheckpointGc(Fixture fixture)
    {
        ProductionFacilityDestructiveDrainOwnerSaveData owner =
            PrepareDurableOwner(fixture);
        ProductionFacilityDestructiveDrainStepResult committed = fixture
            .Participant.TryCommit(CreateStepContext(fixture, owner));
        Require(
            committed.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied
            && IsTerminal(committed),
            "Checkpoint-GC fixture did not commit its composite effect.");
        AcknowledgeUpper(fixture, owner, committed);
        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .OwnerAcknowledged;
        owner.commitId = committed.CommitId;
        owner.receiptFingerprint = committed.ReceiptFingerprint;
        return owner;
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcContext
        CreateCheckpointGcContext(long sequence) => new(
        sequence,
        Digest((char)('a' + (int)sequence)),
        "slot:qa-stock-sensor-checkpoint-gc");

    private static bool RowsEqual(
        ProductionInputDestinationCustodyDrainSaveData left,
        ProductionInputDestinationCustodyDrainSaveData right) => left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private static bool RowsEqual(
        ProductionStockSensorRemovalSaveData left,
        ProductionStockSensorRemovalSaveData right) => left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private static ProductionFacilityDestructiveDrainPrepareContext
        CreatePrepareContext() => new(
        OperationId,
        ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
        FacilityId,
        ProductionOutputDestinationId.FromFacility(FacilityId),
        Digest('9'));

    private static ProductionFacilityDestructiveDrainStepContext
        CreateStepContext(
            Fixture fixture,
            ProductionFacilityDestructiveDrainOwnerSaveData owner) => new(
        OperationId,
        FacilityId,
        fixture.Participant.ParticipantId,
        owner,
        fixture.Lifecycle.DurableFingerprint);

    private static ProductionFacilityDestructiveDrainStepContext
        CreateStepContext(
            Fixture fixture,
            ProductionFacilityDestructiveDrainOwnerSaveData owner,
            string expectedDurableContributionFingerprint) => new(
        OperationId,
        FacilityId,
        fixture.Participant.ParticipantId,
        owner,
        expectedDurableContributionFingerprint);

    private static bool IsTerminal(
        ProductionFacilityDestructiveDrainStepResult result) =>
        ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
            result.CommitId)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            result.ReceiptFingerprint);

    private static string Digest(char value) => new(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private enum SensorFixtureState
    {
        None,
        Pending,
        Installed
    }

    private sealed class Fixture
    {
        private Fixture(
            FakeLifecycle lifecycle,
            FakeSensor sensor,
            FakeInputDrain input,
            FakeCapacity capacity,
            ProductionStockSensorDestructiveDrainParticipant participant)
        {
            Lifecycle = lifecycle;
            Sensor = sensor;
            Input = input;
            Capacity = capacity;
            Participant = participant;
        }

        internal FakeLifecycle Lifecycle { get; }
        internal FakeSensor Sensor { get; }
        internal FakeInputDrain Input { get; }
        internal FakeCapacity Capacity { get; }
        internal ProductionStockSensorDestructiveDrainParticipant Participant
        { get; }

        internal static Fixture Create(
            bool physical,
            SensorFixtureState sensorState)
        {
            bool hasOwner = physical || sensorState != SensorFixtureState.None;
            ProductionFacilityHandle facility = new(
                new object(),
                FacilityId,
                new Vector2Int(4, 7),
                false,
                "item:qa-stock-sensor",
                false,
                Vector2Int.zero,
                "building:qa-stock-sensor-composite",
                "workstation:qa-stock-sensor-composite",
                2);
            FakeLifecycle lifecycle = new(hasOwner);
            FakeSensor sensor = new(sensorState, facility);
            FakeInputDrain input = new(physical, facility);
            FakeCapacity capacity = new();
            ProductionStockSensorDestructiveDrainParticipant participant = new(
                lifecycle,
                sensor,
                input,
                BridgeProxy.Create(hasOwner ? facility : null),
                new FakeDestinationAuthority(),
                new FakeClaims(facility),
                capacity);
            return new Fixture(lifecycle, sensor, input, capacity, participant);
        }
    }

    private sealed class FakeLifecycle :
        IProductionOutputDestinationLifecycleQuery
    {
        private readonly bool hasAuthority;

        internal FakeLifecycle(bool hasAuthority) =>
            this.hasAuthority = hasAuthority;

        internal string DurableFingerprint { get; set; } = Digest('a');

        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId facilityId)
        {
            ProductionOutputDestinationId destination =
                ProductionOutputDestinationId.FromFacility(facilityId);
            ProductionOutputDestinationLifecycleContribution contribution = new(
                ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage,
                hasAuthority,
                hasAuthority ? 1L : 0L,
                hasAuthority ? 1 : 0,
                hasAuthority ? 1_000L : 0L,
                Array.Empty<ProductionOutputLifecycleBlock>(),
                DurableFingerprint,
                DurableFingerprint);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                destination,
                new[] { contribution },
                Digest('b'),
                Digest('c'));
        }
    }

    private sealed class FakeSensor :
        IProductionStockSensorDestructiveDrainPort,
        IProductionStockSensorRemovalCheckpointGcPort
    {
        private readonly ProductionFacilityHandle facility;
        private ProductionStockSensorPhysicalCommitSaveData pending;
        private ProductionInstalledStockSensorSaveData installed;
        private SensorCheckpointGcCandidate activeCheckpointGcCandidate;

        internal FakeSensor(
            SensorFixtureState state,
            ProductionFacilityHandle facility)
        {
            this.facility = facility;
            if (state == SensorFixtureState.Pending)
            {
                pending = new ProductionStockSensorPhysicalCommitSaveData
                {
                    phase = ProductionStockSensorCommitPhase.InputCommitted,
                    facilityId = facility.InstanceId.Value,
                    itemId = facility.StockSensorInstallationItemId,
                    destinationId = ProductionStockSensorRuntime.BuildDestinationId(
                        facility.InstanceId.Value),
                    operationId = "sensor-install:qa",
                    reasonCode = ProductionStockSensorRuntime.PhysicalReasonCode,
                    requestFingerprint = Digest('1'),
                    commitId = "sensor-install-commit:qa",
                    inputQuantity = 1,
                    inputMassGrams = 1_150L,
                    sourceStackIds = new List<string> { "stack:qa-panel" }
                };
            }
            else if (state == SensorFixtureState.Installed)
            {
                installed = CreateInstalled();
            }
        }

        internal ProductionStockSensorRemovalSaveData Removal { get; private set; }
        internal int StabilizeCalls { get; private set; }
        internal int PrepareCalls { get; private set; }
        internal int PublishCalls { get; private set; }
        internal int SuccessfulPublicationCount { get; private set; }
        internal int SuccessfulAcknowledgementCount { get; private set; }
        internal int PublishFailuresRemaining { get; set; }
        internal bool FailNextCheckpointGcPublish { get; set; }
        internal int CheckpointGcPublishCount { get; private set; }
        internal int CheckpointGcRollbackCount { get; private set; }

        internal DungeonProductionBillSaveData CaptureProduction() => new()
        {
            pendingStockSensorInstalls = pending == null
                ? new List<ProductionStockSensorPhysicalCommitSaveData>()
                : new List<ProductionStockSensorPhysicalCommitSaveData>
                {
                    pending.Clone()
                },
            installedStockSensors = installed == null
                ? new List<ProductionInstalledStockSensorSaveData>()
                : new List<ProductionInstalledStockSensorSaveData>
                {
                    installed.Clone()
                },
            pendingStockSensorRemovals = Removal == null
                ? new List<ProductionStockSensorRemovalSaveData>()
                : new List<ProductionStockSensorRemovalSaveData>
                {
                    Removal.Clone()
                }
        };

        public bool TryCapturePendingInstallation(
            BuildingInstanceId facilityId,
            out ProductionStockSensorPhysicalCommitSaveData pendingInstallation,
            out string failureReason)
        {
            failureReason = string.Empty;
            pendingInstallation = facilityId.Equals(facility.InstanceId)
                ? pending?.Clone()
                : null;
            return facilityId.Equals(facility.InstanceId);
        }

        public bool TryStabilizePendingInstallation(
            BuildingInstanceId facilityId,
            string expectedOperationId,
            string expectedRequestFingerprint,
            string expectedCommitId,
            out ProductionInstalledStockSensorSaveData installedRecord,
            out string failureReason)
        {
            StabilizeCalls++;
            failureReason = string.Empty;
            installedRecord = null;
            if (!facilityId.Equals(facility.InstanceId)
                || pending == null
                || !string.Equals(pending.operationId, expectedOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(pending.requestFingerprint,
                    expectedRequestFingerprint, StringComparison.Ordinal)
                || !string.Equals(pending.commitId, expectedCommitId,
                    StringComparison.Ordinal))
            {
                failureReason = "fixture-pending-install-mismatch";
                return false;
            }
            installed = CreateInstalled();
            pending = null;
            installedRecord = installed.Clone();
            return true;
        }

        public bool TryCapture(
            BuildingInstanceId facilityId,
            out ProductionInstalledStockSensorSaveData installedRecord,
            out ProductionStockSensorRemovalSaveData removal,
            out string failureReason)
        {
            failureReason = string.Empty;
            installedRecord = null;
            removal = null;
            if (!facilityId.Equals(facility.InstanceId))
                return false;
            installedRecord = installed?.Clone();
            removal = Removal?.Clone();
            return true;
        }

        public bool TryPrepareDurable(
            BuildingInstanceId facilityId,
            out ProductionStockSensorRemovalSaveData removal,
            out string failureReason)
        {
            PrepareCalls++;
            failureReason = string.Empty;
            removal = null;
            if (!facilityId.Equals(facility.InstanceId))
                return false;
            if (Removal != null)
            {
                removal = Removal.Clone();
                return true;
            }
            if (installed == null)
            {
                failureReason = "fixture-installed-sensor-missing";
                return false;
            }
            Removal = new ProductionStockSensorRemovalSaveData
            {
                phase = ProductionStockSensorRemovalPhase.Prepared,
                facilityId = installed.facilityId,
                itemId = installed.itemId,
                outputPositionX = facility.Position.x,
                outputPositionY = facility.Position.y,
                operationId = "sensor-removal:qa",
                reasonCode = ProductionStockSensorRuntime.RemovalReasonCode,
                installationSourceStackId = installed.inputSourceStackId,
                expectedOutputMassGrams = installed.embeddedMassGrams,
                outputQuantity = 0,
                outputMassGrams = 0L,
                outputCommitIds = new List<string>()
            };
            removal = Removal.Clone();
            return true;
        }

        public bool TryPublish(
            BuildingInstanceId facilityId,
            out ProductionStockSensorRemovalSaveData removal,
            out string failureReason)
        {
            PublishCalls++;
            failureReason = string.Empty;
            removal = null;
            if (!facilityId.Equals(facility.InstanceId) || Removal == null)
                return false;
            if (Removal.phase is ProductionStockSensorRemovalPhase.OutputPublished
                or ProductionStockSensorRemovalPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc)
            {
                removal = Removal.Clone();
                return true;
            }
            if (PublishFailuresRemaining > 0)
            {
                PublishFailuresRemaining--;
                failureReason = "fixture-sensor-publish-deferred";
                return false;
            }
            Removal.phase = ProductionStockSensorRemovalPhase.OutputPublished;
            Removal.outputQuantity = 1;
            Removal.outputMassGrams = Removal.expectedOutputMassGrams;
            Removal.outputCommitIds = new List<string>
            {
                "sensor-salvage-commit:qa"
            };
            installed = null;
            SuccessfulPublicationCount++;
            removal = Removal.Clone();
            return true;
        }

        public bool TryAcknowledge(
            BuildingInstanceId facilityId,
            string expectedOutputCommitId,
            out ProductionStockSensorRemovalSaveData removal,
            out string failureReason)
        {
            failureReason = string.Empty;
            removal = null;
            if (!facilityId.Equals(facility.InstanceId)
                || Removal?.outputCommitIds?.Count != 1
                || !string.Equals(Removal.outputCommitIds[0],
                    expectedOutputCommitId, StringComparison.Ordinal))
            {
                failureReason = "fixture-sensor-ack-mismatch";
                return false;
            }
            if (Removal.phase == ProductionStockSensorRemovalPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc)
            {
                removal = Removal.Clone();
                return true;
            }
            if (Removal.phase != ProductionStockSensorRemovalPhase.OutputPublished)
                return false;
            Removal.phase = ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
            SuccessfulAcknowledgementCount++;
            removal = Removal.Clone();
            return true;
        }

        public bool TryPrepareCheckpointGarbageCollection(
            IReadOnlyList<ProductionStockSensorRemovalSaveData> removals,
            out IProductionStockSensorRemovalCheckpointGcCandidate candidate,
            out string failureReason)
        {
            candidate = null;
            failureReason = string.Empty;
            if (activeCheckpointGcCandidate != null)
            {
                failureReason = "fixture-sensor-gc-already-active";
                return false;
            }
            ProductionStockSensorRemovalSaveData[] expected = (removals
                    ?? Array.Empty<ProductionStockSensorRemovalSaveData>())
                .Select(value => value?.Clone())
                .OrderBy(value => value?.facilityId, StringComparer.Ordinal)
                .ToArray();
            if (expected.Any(value => value == null)
                || expected.Length > 1
                || expected.Length == 0 && Removal != null
                || expected.Length == 1
                && (Removal == null || !RowsEqual(expected[0], Removal)))
            {
                failureReason = "fixture-sensor-gc-row-conflict";
                return false;
            }
            activeCheckpointGcCandidate = new SensorCheckpointGcCandidate(
                expected);
            candidate = activeCheckpointGcCandidate;
            return true;
        }

        public bool TryPublishCheckpointGarbageCollection(
            IProductionStockSensorRemovalCheckpointGcCandidate candidate,
            out string failureReason)
        {
            failureReason = string.Empty;
            SensorCheckpointGcCandidate exact = RequireCandidate(candidate);
            CheckpointGcPublishCount++;
            if (exact.Published)
                return true;
            if (FailNextCheckpointGcPublish)
            {
                FailNextCheckpointGcPublish = false;
                failureReason = "fixture-sensor-gc-publish-fault";
                return false;
            }
            if (exact.Rows.Count == 1
                && (Removal == null || !RowsEqual(Removal, exact.Rows[0]))
                || exact.Rows.Count == 0 && Removal != null)
            {
                failureReason = "fixture-sensor-gc-live-row-conflict";
                return false;
            }
            Removal = null;
            exact.Published = true;
            return true;
        }

        public void RollbackCheckpointGarbageCollection(
            IProductionStockSensorRemovalCheckpointGcCandidate candidate)
        {
            SensorCheckpointGcCandidate exact = RequireCandidate(candidate);
            if (!exact.Published)
                return;
            if (Removal != null)
                throw new InvalidOperationException(
                    "Fixture sensor GC rollback would overwrite a row.");
            Removal = exact.Rows.SingleOrDefault()?.Clone();
            exact.Published = false;
            CheckpointGcRollbackCount++;
        }

        public void CompleteCheckpointGarbageCollection(
            IProductionStockSensorRemovalCheckpointGcCandidate candidate)
        {
            RequireCandidate(candidate);
            activeCheckpointGcCandidate = null;
        }

        private SensorCheckpointGcCandidate RequireCandidate(
            IProductionStockSensorRemovalCheckpointGcCandidate candidate)
        {
            if (candidate is not SensorCheckpointGcCandidate exact
                || !ReferenceEquals(activeCheckpointGcCandidate, exact))
                throw new InvalidOperationException(
                    "Fixture sensor GC candidate is stale or foreign.");
            return exact;
        }

        private sealed class SensorCheckpointGcCandidate :
            IProductionStockSensorRemovalCheckpointGcCandidate
        {
            internal SensorCheckpointGcCandidate(
                IReadOnlyList<ProductionStockSensorRemovalSaveData> rows)
            {
                Rows = (rows
                        ?? Array.Empty<ProductionStockSensorRemovalSaveData>())
                    .Select(value => value.Clone()).ToArray();
            }

            internal IReadOnlyList<ProductionStockSensorRemovalSaveData> Rows
                { get; }
            internal bool Published { get; set; }
        }

        private ProductionInstalledStockSensorSaveData CreateInstalled() => new()
        {
            facilityId = facility.InstanceId.Value,
            itemId = facility.StockSensorInstallationItemId,
            inputOperationId = "sensor-install:qa",
            inputCommitId = "sensor-install-commit:qa",
            inputSourceStackId = "stack:qa-panel",
            embeddedMassGrams = 1_150L
        };
    }

    private sealed class FakeInputDrain :
        IProductionInputDestinationCustodyDrainService,
        IProductionInputDestinationCustodyDrainCheckpointGcPort
    {
        private readonly ProductionFacilityHandle facility;
        private readonly bool physical;
        private readonly Dictionary<string,
            ProductionInputDestinationCustodyDrainSaveData> states =
            new(StringComparer.Ordinal);
        private InputCheckpointGcCandidate activeCheckpointGcCandidate;

        internal FakeInputDrain(bool physical, ProductionFacilityHandle facility)
        {
            this.physical = physical;
            this.facility = facility;
        }

        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;
        internal int PrepareCalls { get; private set; }
        internal int TerminalEffectCount { get; private set; }
        internal int AcknowledgeEffectCount { get; private set; }
        internal int CheckpointGcPublishCount { get; private set; }
        internal int CheckpointGcRollbackCount { get; private set; }

        internal IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
            CaptureAll() => states.Values
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();

        internal ProductionInputDestinationCustodyDrainPhase CapturedPhase(
            string stepOperationId) => states[stepOperationId].phase;

        internal long CapturedInputMass(string stepOperationId) =>
            states[stepOperationId].inputMassGrams;

        internal void TamperValidSourceClaim(
            string stepOperationId,
            string sourceClaimFingerprint)
        {
            ProductionInputDestinationCustodyDrainSaveData state =
                states[stepOperationId];
            state.sourceClaimFingerprint = sourceClaimFingerprint;
            state.requestFingerprint =
                ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                    state.parentOperationId,
                    state.stepOperationId,
                    state.ownerStableId,
                    state.billId,
                    state.facilityId,
                    state.sourceDestinationId,
                    state.ownerGridX,
                    state.ownerGridY,
                    state.sourceClaimFingerprint,
                    state.sourceOwnershipFingerprint,
                    state.sourceStacks,
                    state.sourceOperations,
                    state.sourceActors,
                    state.inputQuantity,
                    state.inputMassGrams);
            Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(
                    state),
                "Fixture failed to produce a valid mismatched child request.");
        }

        public bool TryCaptureSource(
            string sourceDestinationId,
            out ProductionInputDestinationCustodySourceSnapshot snapshot,
            out string failureReason)
        {
            failureReason = string.Empty;
            ProductionInputDestinationDrainStackSaveData[] stacks = physical
                ? new[]
                {
                    new ProductionInputDestinationDrainStackSaveData
                    {
                        stackId = "stack:qa-socket-cargo",
                        itemId = facility.StockSensorInstallationItemId,
                        componentFingerprint = Digest('4'),
                        quantity = 1,
                        massGrams = 1_000L,
                        state = WorldItemStackState.FacilityBuffer,
                        positionX = facility.Position.x,
                        positionY = facility.Position.y,
                        sourceStorageDestinationId = sourceDestinationId,
                        destinationPositionX = facility.Position.x,
                        destinationPositionY = facility.Position.y,
                        reservationRevision = 1L
                    }
                }
                : Array.Empty<ProductionInputDestinationDrainStackSaveData>();
            snapshot = new ProductionInputDestinationCustodySourceSnapshot(
                sourceDestinationId,
                1L,
                Digest(physical ? '5' : '6'),
                stacks,
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                physical ? 1 : 0,
                physical ? 1_000L : 0L);
            return true;
        }

        public bool TryBuildRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            ProductionInputDestinationCustodySourceSnapshot snapshot,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            failureReason = string.Empty;
            string fingerprint =
                ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                    parentOperationId,
                    stepOperationId,
                    ownerStableId,
                    billId,
                    facilityId,
                    snapshot.SourceDestinationId,
                    ownerPosition.x,
                    ownerPosition.y,
                    sourceClaimFingerprint,
                    snapshot.SourceOwnershipFingerprint,
                    snapshot.SourceStacks,
                    snapshot.SourceOperations,
                    snapshot.SourceActors,
                    snapshot.InputQuantity,
                    snapshot.InputMassGrams);
            request = new ProductionInputDestinationCustodyDrainRequest(
                parentOperationId,
                stepOperationId,
                ownerStableId,
                billId,
                facilityId,
                snapshot.SourceDestinationId,
                ownerPosition.x,
                ownerPosition.y,
                sourceClaimFingerprint,
                snapshot.SourceOwnershipFingerprint,
                snapshot.SourceStacks,
                snapshot.SourceOperations,
                snapshot.SourceActors,
                snapshot.InputQuantity,
                snapshot.InputMassGrams,
                fingerprint);
            return ProductionInputDestinationCustodyDrainContract.IsValidRequest(
                request);
        }

        public bool TryCaptureRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            string sourceDestinationId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            TryCaptureSource(sourceDestinationId, out var snapshot,
                out failureReason);
            return TryBuildRequest(parentOperationId, stepOperationId,
                ownerStableId, billId, facilityId, ownerPosition,
                sourceClaimFingerprint, snapshot, out request,
                out failureReason);
        }

        public ProductionInputDestinationCustodyDrainResult TryPrepare(
            ProductionInputDestinationCustodyDrainRequest request)
        {
            PrepareCalls++;
            if (states.TryGetValue(request.StepOperationId, out var existing))
            {
                return string.Equals(existing.requestFingerprint,
                        request.RequestFingerprint, StringComparison.Ordinal)
                    ? Result(existing,
                        ProductionInputDestinationCustodyDrainStatus.Replay)
                    : Conflict("fixture-child-request-conflict");
            }
            ProductionInputDestinationCustodyDrainSaveData state = new()
            {
                parentOperationId = request.ParentOperationId,
                stepOperationId = request.StepOperationId,
                ownerStableId = request.OwnerStableId,
                billId = request.BillId,
                facilityId = request.FacilityId,
                sourceDestinationId = request.SourceDestinationId,
                ownerGridX = request.OwnerGridX,
                ownerGridY = request.OwnerGridY,
                sourceClaimFingerprint = request.SourceClaimFingerprint,
                sourceOwnershipFingerprint = request.SourceOwnershipFingerprint,
                requestFingerprint = request.RequestFingerprint,
                phase = ProductionInputDestinationCustodyDrainPhase.Prepared,
                sourceStacks = request.SourceStacks.Select(value => value.Clone())
                    .ToList(),
                sourceOperations = request.SourceOperations
                    .Select(value => value.Clone()).ToList(),
                sourceActors = request.SourceActors.Select(value => value.Clone())
                    .ToList(),
                completedActorIds = new List<string>(),
                releasedOperationIds = new List<string>(),
                releasedStackIds = new List<string>(),
                inputQuantity = request.InputQuantity,
                inputMassGrams = request.InputMassGrams
            };
            Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(
                    state),
                "Fixture prepared an invalid child.");
            states[state.stepOperationId] = state;
            return Result(state,
                ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryCommit(
            string stepOperationId,
            string requestFingerprint)
        {
            if (!states.TryGetValue(stepOperationId, out var state)
                || !string.Equals(state.requestFingerprint,
                    requestFingerprint, StringComparison.Ordinal))
                return Conflict("fixture-child-commit-conflict");
            switch (state.phase)
            {
                case ProductionInputDestinationCustodyDrainPhase.Prepared:
                    state.phase = ProductionInputDestinationCustodyDrainPhase
                        .ReleasingActors;
                    break;
                case ProductionInputDestinationCustodyDrainPhase.ReleasingActors:
                    state.completedActorIds = state.sourceActors
                        .Select(value => value.actorId).ToList();
                    state.phase = ProductionInputDestinationCustodyDrainPhase
                        .ReleasingOperationAuthority;
                    break;
                case ProductionInputDestinationCustodyDrainPhase
                    .ReleasingOperationAuthority:
                    state.releasedOperationIds = state.sourceOperations
                        .Select(value => value.operationId).ToList();
                    state.phase = ProductionInputDestinationCustodyDrainPhase
                        .ReleasingDestination;
                    break;
                case ProductionInputDestinationCustodyDrainPhase
                    .ReleasingDestination:
                    state.releasedStackIds = state.sourceStacks
                        .Select(value => value.stackId).ToList();
                    state.releasedQuantity = state.inputQuantity;
                    state.releasedMassGrams = state.inputMassGrams;
                    state.resultFingerprint = Digest('7');
                    state.commitId =
                        ProductionInputDestinationCustodyDrainFingerprint
                            .CreateCommit(
                                state.stepOperationId,
                                state.requestFingerprint);
                    state.receiptFingerprint =
                        ProductionInputDestinationCustodyDrainFingerprint
                            .CreateReceipt(
                                state.requestFingerprint,
                                state.resultFingerprint,
                                state.releasedQuantity,
                                state.releasedMassGrams,
                                state.releasedStackIds,
                                state.releasedOperationIds);
                    state.phase = ProductionInputDestinationCustodyDrainPhase
                        .EffectCommittedAwaitingBillAck;
                    TerminalEffectCount++;
                    break;
                default:
                    return Result(state,
                        ProductionInputDestinationCustodyDrainStatus.Replay);
            }
            Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(
                    state),
                "Fixture child transition became invalid.");
            return Result(state,
                ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (!states.TryGetValue(stepOperationId, out var state)
                || !string.Equals(state.receiptFingerprint,
                    receiptFingerprint, StringComparison.Ordinal))
                return Conflict("fixture-child-ack-conflict");
            if (state.phase == ProductionInputDestinationCustodyDrainPhase
                    .BillAcknowledgedAwaitingCheckpointGc)
            {
                return Result(state,
                    ProductionInputDestinationCustodyDrainStatus.Replay);
            }
            if (state.phase != ProductionInputDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingBillAck)
                return Conflict("fixture-child-ack-phase-conflict");
            state.phase = ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc;
            AcknowledgeEffectCount++;
            Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(
                    state),
                "Fixture acknowledged child became invalid.");
            return Result(state,
                ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint) =>
            Conflict("fixture-gc-not-used");

        public bool TryCapture(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData record)
        {
            record = null;
            if (!states.TryGetValue(stepOperationId, out var state))
                return false;
            record = state.Clone();
            return true;
        }

        public bool TryPrepareCheckpointGarbageCollection(
            IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> records,
            out IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                candidate,
            out string failureReason)
        {
            candidate = null;
            failureReason = string.Empty;
            if (activeCheckpointGcCandidate != null)
            {
                failureReason = "fixture-input-gc-already-active";
                return false;
            }
            ProductionInputDestinationCustodyDrainSaveData[] expected = (records
                    ?? Array.Empty<
                        ProductionInputDestinationCustodyDrainSaveData>())
                .Select(value => value?.Clone())
                .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
                .ToArray();
            if (expected.Any(value => value == null)
                || expected.Select(value => value.stepOperationId)
                    .Distinct(StringComparer.Ordinal).Count() != expected.Length
                || expected.Any(value => value.phase !=
                    ProductionInputDestinationCustodyDrainPhase
                        .BillAcknowledgedAwaitingCheckpointGc
                    || !states.TryGetValue(
                        value.stepOperationId,
                        out ProductionInputDestinationCustodyDrainSaveData live)
                    || !RowsEqual(value, live)))
            {
                failureReason = "fixture-input-gc-row-conflict";
                return false;
            }
            activeCheckpointGcCandidate = new InputCheckpointGcCandidate(
                expected);
            candidate = activeCheckpointGcCandidate;
            return true;
        }

        public bool TryPublishCheckpointGarbageCollection(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                candidate,
            out string failureReason)
        {
            failureReason = string.Empty;
            InputCheckpointGcCandidate exact = RequireCandidate(candidate);
            CheckpointGcPublishCount++;
            if (exact.Published)
                return true;
            if (exact.Rows.Any(value => !states.TryGetValue(
                    value.stepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData live)
                || !RowsEqual(value, live)))
            {
                failureReason = "fixture-input-gc-live-row-conflict";
                return false;
            }
            foreach (ProductionInputDestinationCustodyDrainSaveData row in
                     exact.Rows)
                states.Remove(row.stepOperationId);
            exact.Published = true;
            return true;
        }

        public void RollbackCheckpointGarbageCollection(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                candidate)
        {
            InputCheckpointGcCandidate exact = RequireCandidate(candidate);
            if (!exact.Published)
                return;
            if (exact.Rows.Any(value => states.ContainsKey(value.stepOperationId)))
                throw new InvalidOperationException(
                    "Fixture input GC rollback would overwrite a row.");
            foreach (ProductionInputDestinationCustodyDrainSaveData row in
                     exact.Rows)
                states.Add(row.stepOperationId, row.Clone());
            exact.Published = false;
            CheckpointGcRollbackCount++;
        }

        public void CompleteCheckpointGarbageCollection(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                candidate)
        {
            RequireCandidate(candidate);
            activeCheckpointGcCandidate = null;
        }

        private InputCheckpointGcCandidate RequireCandidate(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                candidate)
        {
            if (candidate is not InputCheckpointGcCandidate exact
                || !ReferenceEquals(activeCheckpointGcCandidate, exact))
                throw new InvalidOperationException(
                    "Fixture input GC candidate is stale or foreign.");
            return exact;
        }

        private sealed class InputCheckpointGcCandidate :
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate
        {
            internal InputCheckpointGcCandidate(
                IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
                    rows)
            {
                Rows = (rows ?? Array.Empty<
                        ProductionInputDestinationCustodyDrainSaveData>())
                    .Select(value => value.Clone()).ToArray();
            }

            internal IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
                Rows { get; }
            internal bool Published { get; set; }
        }

        private static ProductionInputDestinationCustodyDrainResult Result(
            ProductionInputDestinationCustodyDrainSaveData state,
            ProductionInputDestinationCustodyDrainStatus status) => new(
            status,
            state.commitId,
            state.receiptFingerprint,
            string.Empty);

        private static ProductionInputDestinationCustodyDrainResult Conflict(
            string reason) => new(
            ProductionInputDestinationCustodyDrainStatus.Conflict,
            string.Empty,
            string.Empty,
            reason);
    }

    private sealed class FakeDestinationAuthority :
        IProductionStockSensorDestinationAuthorityRuntime
    {
        public bool TryEnsure(
            ProductionFacilityHandle facility,
            out long capacityMassGrams,
            out string failureReason) =>
            TryValidate(facility, out capacityMassGrams, out failureReason);

        public bool TryValidate(
            ProductionFacilityHandle facility,
            out long capacityMassGrams,
            out string failureReason)
        {
            capacityMassGrams = 1_150L;
            failureReason = string.Empty;
            return facility != null && facility.InstanceId.Equals(FacilityId);
        }

        public bool TryReplaceProjected(
            IReadOnlyList<ProductionFacilityHandle> facilities,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryRequireEmpty(
            ProductionFacilityHandle facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryRevoke(
            BuildingInstanceId facilityId,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeClaims : IFacilityBufferDestinationClaimQuery
    {
        private readonly FacilityBufferDestinationClaim claim;

        internal FakeClaims(ProductionFacilityHandle facility)
        {
            claim = new FacilityBufferDestinationClaim(
                ProductionStockSensorRuntime.BuildDestinationId(
                    facility.InstanceId.Value),
                facility.Position,
                "production-stock-sensor",
                "stock-sensor-authority:qa",
                facility.InstanceId.Value,
                FacilityBufferDestinationAnchorKind.LiveFacility);
        }

        public long Revision => 1L;

        public bool TryGetClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim result)
        {
            result = string.Equals(destinationId, claim.DestinationId,
                    StringComparison.Ordinal)
                && dropPosition == claim.DropPosition
                ? claim
                : null;
            return result != null;
        }

        public IReadOnlyList<FacilityBufferDestinationClaim> CaptureClaims() =>
            new[] { claim };
    }

    private sealed class FakeCapacity : IFacilityBufferMassCapacityQuery
    {
        internal string AuthorityFingerprint { get; set; } = Digest('8');
        public long Revision => 1L;

        public bool TryGetCapacity(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferMassCapacitySnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public bool TryGetReceipt(
            string tokenId,
            out FacilityBufferMassAdmissionReceipt receipt)
        {
            receipt = default;
            return false;
        }

        public IReadOnlyList<FacilityBufferCapacityProfile> CaptureProfiles() =>
            Array.Empty<FacilityBufferCapacityProfile>();

        public bool TryGetCapacityAuthorityFingerprint(
            string destinationId,
            Vector2Int dropPosition,
            out string fingerprint)
        {
            fingerprint = AuthorityFingerprint;
            return string.Equals(
                    destinationId,
                    ProductionStockSensorRuntime.BuildDestinationId(
                        FacilityId.Value),
                    StringComparison.Ordinal)
                && dropPosition == new Vector2Int(4, 7);
        }
    }

    public class BridgeProxy : DispatchProxy
    {
        private ProductionFacilityHandle facility;

        internal static IProductionAssemblyBridge Create(
            ProductionFacilityHandle handle)
        {
            IProductionAssemblyBridge proxy =
                DispatchProxy.Create<IProductionAssemblyBridge, BridgeProxy>();
            ((BridgeProxy)proxy).facility = handle;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == "get_Facilities")
                return new[] { facility };
            if (targetMethod.Name == nameof(IProductionAssemblyBridge
                    .CaptureFacility))
            {
                if (args.Length == 1
                    && ReferenceEquals(args[0], facility.RuntimeObject))
                    return facility;
                throw new InvalidOperationException("Unknown fixture facility.");
            }
            throw new InvalidOperationException(
                "Unexpected production bridge call: " + targetMethod.Name);
        }
    }
}
#endif
