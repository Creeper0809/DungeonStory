using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionGenericBillTerminalDrainSaveSectionDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/QA/Production Generic Terminal Drain Save")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log(
            "[ProductionGenericBillTerminalDrainSaveSectionDebugScenarios] PASS");
    }

    public static void RunAll()
    {
        ProductionGenericBillTerminalDrainSaveValidation validator = new();
        Fixture fixture = Fixture.Create("primary");

        DungeonProductionGenericBillTerminalDrainSaveData preparedPayload =
            Payload(fixture.Producer);
        validator.ValidateCrossAggregate(
            Bundle(Production(fixture.SourceBill)),
            preparedPayload,
            new ChildQuery(Array.Empty<
                ProductionInputDestinationCustodyDrainSaveData>()));

        ProductionInputDestinationCustodyDrainSaveData committedChild =
            CommitChild(fixture.Child, acknowledged: false);
        ProductionGenericBillTerminalDrainSaveData recorded =
            RecordChildReceipt(fixture.Producer, committedChild);
        validator.ValidateCrossAggregate(
            Bundle(Production(fixture.SourceBill), committedChild),
            Payload(recorded),
            new ChildQuery(new[] { committedChild }));

        ProductionInputDestinationCustodyDrainSaveData acknowledgedChild =
            CommitChild(fixture.Child, acknowledged: true);
        ProductionGenericBillTerminalDrainSaveData awaitingTerminal =
            RecordChildReceipt(fixture.Producer, acknowledgedChild);
        awaitingTerminal.phase = ProductionGenericBillTerminalDrainPhase
            .InputDestinationAcknowledgedAwaitingBillTerminal;
        ProductionWipTerminalReceiptSaveData wip = CreateWip(fixture.SourceBill);
        validator.ValidateCrossAggregate(
            Bundle(Production(null, wip), acknowledgedChild),
            Payload(awaitingTerminal),
            new ChildQuery(new[] { acknowledgedChild }));

        ProductionGenericBillTerminalDrainSaveData terminal =
            CommitProducer(awaitingTerminal);
        validator.ValidateCrossAggregate(
            Bundle(Production(null, wip), acknowledgedChild),
            Payload(terminal),
            new ChildQuery(new[] { acknowledgedChild }));

        ExpectFailure(() => validator.ValidateCrossAggregate(
            Bundle(Production(fixture.SourceBill), acknowledgedChild),
            preparedPayload,
            new ChildQuery(new[] { acknowledgedChild })),
            "Prepared producer accepted an acknowledged child.");
        ExpectFailure(() => validator.ValidateCrossAggregate(
            Bundle(Production(fixture.SourceBill), fixture.Child),
            new DungeonProductionGenericBillTerminalDrainSaveData
            {
                version = DungeonProductionGenericBillTerminalDrainSaveData
                    .CurrentVersion
            },
            new ChildQuery(new[] { fixture.Child })),
            "Orphan Items child was accepted.");

        ProductionGenericBillTerminalDrainSaveData quantityMismatch =
            recorded.Clone();
        quantityMismatch.releasedInputQuantity--;
        ExpectFailure(() => validator.ValidateCrossAggregate(
            Bundle(Production(fixture.SourceBill), committedChild),
            Payload(quantityMismatch),
            new ChildQuery(new[] { committedChild })),
            "Generic/child quantity mismatch was accepted.");

        ExpectFailure(() => validator.ValidateCrossAggregate(
            Bundle(Production(null), acknowledgedChild),
            Payload(terminal),
            new ChildQuery(new[] { acknowledgedChild })),
            "Terminal producer without exact WIP evidence was accepted.");

        DungeonProductionGenericBillTerminalDrainSaveData unordered = new()
        {
            version = DungeonProductionGenericBillTerminalDrainSaveData
                .CurrentVersion,
            entries = new List<ProductionGenericBillTerminalDrainSaveData>
            {
                Fixture.Create("z").Producer,
                Fixture.Create("a").Producer
            }
        };
        ExpectFailure(() => validator.ValidateOwnPayload(unordered),
            "Non-canonical producer order was accepted.");
    }

    private static DungeonProductionGenericBillTerminalDrainSaveData Payload(
        ProductionGenericBillTerminalDrainSaveData value) => new()
    {
        version = DungeonProductionGenericBillTerminalDrainSaveData
            .CurrentVersion,
        entries = new List<ProductionGenericBillTerminalDrainSaveData>
        {
            value.Clone()
        }
    };

    private static DungeonProductionBillSaveData Production(
        ProductionBillSaveData bill,
        ProductionWipTerminalReceiptSaveData wip = null) => new()
    {
        bills = bill == null
            ? new List<ProductionBillSaveData>()
            : new List<ProductionBillSaveData>
            {
                ProductionGenericBillTerminalDrainCanonical.CloneBill(bill)
            },
        wipTerminalReceipts = wip == null
            ? new List<ProductionWipTerminalReceiptSaveData>()
            : new List<ProductionWipTerminalReceiptSaveData> { wip.Clone() }
    };

    private static ProductionOutputLifecycleRestoreCandidateBundle Bundle(
        DungeonProductionBillSaveData production,
        params ProductionInputDestinationCustodyDrainSaveData[] children)
    {
        ProductionOutputLifecycleRestoreCandidateIndex index = new(
            new NoopDrainValidator());
        index.BeginRestoreCandidate();
        index.SetWorld(new ModularFacilityWorldSaveData());
        index.SetCharacters(new DungeonCharacterWorldSaveData());
        DungeonPhysicalItemSaveData physical = new();
        physical.pendingProductionInputDestinationDrains = (children
                ?? Array.Empty<
                    ProductionInputDestinationCustodyDrainSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToList();
        index.SetPhysicalItems(physical);
        index.SetProduction(production);
        index.SetRouting(new ProductionPreparedOutputRoutingSaveData());
        index.SetCombat(new DungeonCombatEquipmentSaveData());
        index.SetMaintenance(new CombatEquipmentMaintenanceSaveData());
        index.SetEnvironment(new DungeonCharacterEnvironmentSaveData
        {
            apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
            apparelWorkOrderTerminalStates =
                Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
        });
        Require(index.TryCapture(
                out ProductionOutputLifecycleRestoreCandidateBundle bundle),
            "Seven-source lifecycle bundle was not captured.");
        return bundle;
    }

    private static ProductionInputDestinationCustodyDrainSaveData CommitChild(
        ProductionInputDestinationCustodyDrainSaveData source,
        bool acknowledged)
    {
        ProductionInputDestinationCustodyDrainSaveData value = source.Clone();
        value.phase = acknowledged
            ? ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc
            : ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck;
        value.releasedStackIds = value.sourceStacks
            .Select(row => row.stackId)
            .ToList();
        value.releasedQuantity = value.inputQuantity;
        value.releasedMassGrams = value.inputMassGrams;
        value.resultFingerprint = new string('d', 64);
        value.commitId = ProductionInputDestinationCustodyDrainFingerprint
            .CreateCommit(value.stepOperationId, value.requestFingerprint);
        value.receiptFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateReceipt(
                value.requestFingerprint,
                value.resultFingerprint,
                value.releasedQuantity,
                value.releasedMassGrams,
                value.releasedStackIds,
                value.releasedOperationIds);
        Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(value),
            "Committed fixture child is invalid.");
        return value;
    }

    private static ProductionGenericBillTerminalDrainSaveData RecordChildReceipt(
        ProductionGenericBillTerminalDrainSaveData source,
        ProductionInputDestinationCustodyDrainSaveData child)
    {
        ProductionGenericBillTerminalDrainSaveData value = source.Clone();
        value.phase = ProductionGenericBillTerminalDrainPhase
            .InputDestinationReceiptRecordedAwaitingAcknowledgement;
        value.inputDestinationDrainCommitId = child.commitId;
        value.inputDestinationDrainReceiptFingerprint = child.receiptFingerprint;
        value.releasedInputQuantity = child.releasedQuantity;
        value.releasedInputMassGrams = child.releasedMassGrams;
        Require(ProductionGenericBillTerminalDrainCanonical.IsValidSave(value),
            "Recorded fixture producer is invalid.");
        return value;
    }

    private static ProductionGenericBillTerminalDrainSaveData CommitProducer(
        ProductionGenericBillTerminalDrainSaveData source)
    {
        ProductionGenericBillTerminalDrainSaveData value = source.Clone();
        value.wipTerminalCommitId = ProductionGenericBillTerminalDrainCanonical
            .CreateWipTerminalCommitId(
                value.billId,
                value.sourceBill.cycleSequence);
        value.billTerminalEffectFingerprint =
            ProductionGenericBillTerminalDrainCanonical
                .CreateBillTerminalEffectFingerprint(
                    value.requestFingerprint,
                    value.inputDestinationDrainReceiptFingerprint,
                    value.wipTerminalCommitId);
        value.commitId = ProductionGenericBillTerminalDrainCanonical
            .CreateCommitId(value.stepOperationId, value.requestFingerprint);
        value.receiptFingerprint = ProductionGenericBillTerminalDrainCanonical
            .CreateReceiptFingerprint(
                value.requestFingerprint,
                value.inputDestinationDrainReceiptFingerprint,
                value.billTerminalEffectFingerprint,
                value.commitId);
        value.phase = ProductionGenericBillTerminalDrainPhase
            .BillTerminalCommittedAwaitingOwnerAcknowledgement;
        Require(ProductionGenericBillTerminalDrainCanonical.IsValidSave(value),
            "Terminal fixture producer is invalid.");
        return value;
    }

    private static ProductionWipTerminalReceiptSaveData CreateWip(
        ProductionBillSaveData source)
    {
        long outputMass = source.resolvedOutputs.Sum(
            value => value.committedMassGrams);
        long loss = source.wipInputMassGrams
            + source.processCleanWaterMassGrams
            - outputMass
            - source.processWastewaterMassGrams;
        return new ProductionWipTerminalReceiptSaveData
        {
            commitId = ProductionGenericBillTerminalDrainCanonical
                .CreateWipTerminalCommitId(source.billId, source.cycleSequence),
            billId = source.billId,
            recipeId = source.recipeId,
            buildingInstanceId = source.buildingInstanceId,
            cycleSequence = source.cycleSequence,
            inputCommitId = source.wipInputCommitId,
            inputQuantity = source.wipInputQuantity,
            inputMassGrams = source.wipInputMassGrams,
            processCleanWaterMassGrams = source.processCleanWaterMassGrams,
            processWastewaterMassGrams = source.processWastewaterMassGrams,
            wastewaterComponents = source.processWastewaterComponents
                .Select(value => value.Clone())
                .ToList(),
            committedOutputMassGrams = outputMass,
            reason = ProductionWipTerminalReason.FacilityDestroyed,
            lossKind = ProductionWipTerminalLossKind
                .ExplicitIrrecoverableProcessLoss,
            declaredLossMassGrams = loss
        };
    }

    private static void ExpectFailure(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class NoopDrainValidator :
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

    private sealed class ChildQuery :
        IProductionInputDestinationCustodyDrainRestoreCandidateQuery
    {
        private readonly ProductionInputDestinationCustodyDrainSaveData[] values;

        internal ChildQuery(
            IEnumerable<ProductionInputDestinationCustodyDrainSaveData> source)
        {
            values = (source ?? Array.Empty<
                    ProductionInputDestinationCustodyDrainSaveData>())
                .Select(value => value?.Clone())
                .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
                .ToArray();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
            Drains => values;

        public bool TryGetDrain(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData drain)
        {
            drain = values.SingleOrDefault(value => string.Equals(
                value.stepOperationId,
                stepOperationId,
                StringComparison.Ordinal))?.Clone();
            return drain != null;
        }
    }

    private sealed class Fixture
    {
        internal ProductionBillSaveData SourceBill { get; private set; }
        internal ProductionInputDestinationCustodyDrainSaveData Child
        { get; private set; }
        internal ProductionGenericBillTerminalDrainSaveData Producer
        { get; private set; }

        internal static Fixture Create(string suffix)
        {
            string billId = "production-bill:qa:generic-save:" + suffix;
            string facilityId = "building:qa:generic-save:" + suffix;
            string destinationId = ProductionBillRuntime.DestinationPrefix
                + billId;
            ProductionBillSaveData bill = new()
            {
                billId = billId,
                recipeId = "recipe:qa:generic-save",
                buildingInstanceId = facilityId,
                mode = ProductionOrderMode.RepeatCount,
                remainingCycles = 1,
                materialsConsumed = true,
                cycleSequence = 1,
                wipInputCommitId = "production-wip-input:qa:" + suffix,
                wipInputQuantity = 3,
                wipInputMassGrams = 3_000L,
                outputOutcomeResolved = true,
                resolvedOutputs = new List<ProductionResolvedOutputSaveData>
                {
                    new()
                    {
                        outputLineId = "output:qa-generic-save",
                        itemId = "material:qa:generic-save-output",
                        outputCapabilityId =
                            ProductionOutputCapabilityIds.StandardDefinition,
                        outputCapabilityVersion =
                            ProductionOutputCapabilityIds.StandardDefinitionVersion,
                        outputComponentCodecId =
                            ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                        outputComponentCodecVersion =
                            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
                        outputCapabilityFingerprint =
                            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                                "output:qa-generic-save",
                                "material:qa:generic-save-output",
                                ProductionOutputCapabilityIds.StandardDefinition,
                                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
                        amount = 2,
                        committedAmount = 2,
                        committedMassGrams = 2_000L
                    }
                },
                preparedOutput = ProductionPreparedOutputBatchSaveData.Unresolved(),
                processWastewaterComponents = new List<
                    ProductionWastewaterComponentSaveData>(),
                processManualWaterTransfers = new List<
                    ProductionManualWaterTransferSaveData>(),
                materialDestinationId = destinationId,
                allowedMaterialIds = new List<string>(),
                allowedWorkerIds = new List<string>(),
                workerContributions = new List<CraftContributionSaveData>(),
                outputReservations = new List<ProductionOutputReservationSaveData>(),
                routePolicies = new List<ProductionConsumerRoutePolicy>(),
                selectedSupplies = new List<ProductionSelectedSupplySaveData>()
            };
            ProductionFacilityDestructiveDrainOperationId parentId =
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    (BuildingInstanceId)facilityId);
            string parent = parentId.Value;
            string owner = ProductionFacilityDestructiveDrainOwnerStableIds
                .GenericBill(billId);
            string step = ProductionFacilityDestructiveDrainCanonical
                .BuildStepOperationId(
                    parentId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills,
                    owner);
            string childStep = step + ":input-destination-custody";
            string sourceFingerprint = ProductionGenericBillTerminalDrainCanonical
                .CreateSourceBillFingerprint(bill);
            ProductionInputDestinationDrainStackSaveData stack = new()
            {
                stackId = "stack:qa:generic-save:" + suffix,
                itemId = "material:qa:generic-save",
                componentFingerprint = new string('c', 64),
                quantity = 3,
                massGrams = 3_000L,
                state = WorldItemStackState.Stored,
                positionX = 2,
                positionY = 3,
                sourceStorageDestinationId = "warehouse:qa:generic-save",
                destinationPositionX = 7,
                destinationPositionY = 9,
                reservationRevision = 4L
            };
            string ownershipFingerprint = new string('b', 64);
            string childRequest = ProductionInputDestinationCustodyDrainFingerprint
                .CreateRequest(
                    parent,
                    childStep,
                    owner,
                    billId,
                    facilityId,
                    destinationId,
                    7,
                    9,
                    sourceFingerprint,
                    ownershipFingerprint,
                    new[] { stack },
                    Array.Empty<
                        ProductionInputDestinationDrainOperationSaveData>(),
                    Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                    3,
                    3_000L);
            ProductionInputDestinationCustodyDrainSaveData child = new()
            {
                parentOperationId = parent,
                stepOperationId = childStep,
                ownerStableId = owner,
                billId = billId,
                facilityId = facilityId,
                sourceDestinationId = destinationId,
                ownerGridX = 7,
                ownerGridY = 9,
                sourceClaimFingerprint = sourceFingerprint,
                sourceOwnershipFingerprint = ownershipFingerprint,
                requestFingerprint = childRequest,
                phase = ProductionInputDestinationCustodyDrainPhase.Prepared,
                sourceStacks = new List<
                    ProductionInputDestinationDrainStackSaveData>
                {
                    stack
                },
                sourceOperations = new List<
                    ProductionInputDestinationDrainOperationSaveData>(),
                sourceActors = new List<
                    ProductionInputDestinationDrainActorSaveData>(),
                completedActorIds = new List<string>(),
                releasedOperationIds = new List<string>(),
                releasedStackIds = new List<string>(),
                inputQuantity = 3,
                inputMassGrams = 3_000L
            };
            string producerRequest = ProductionGenericBillTerminalDrainCanonical
                .CreateRequestFingerprint(
                    parent,
                    step,
                    owner,
                    bill,
                    childStep,
                    childRequest);
            ProductionGenericBillTerminalDrainSaveData producer = new()
            {
                parentOperationId = parent,
                stepOperationId = step,
                ownerStableId = owner,
                billId = billId,
                facilityId = facilityId,
                inputDestinationId = destinationId,
                sourceBill = ProductionGenericBillTerminalDrainCanonical
                    .CloneBill(bill),
                sourceBillFingerprint = sourceFingerprint,
                inputDestinationDrainStepOperationId = childStep,
                inputDestinationDrainRequestFingerprint = childRequest,
                requestFingerprint = producerRequest,
                phase = ProductionGenericBillTerminalDrainPhase
                    .PreparedAwaitingInputDestinationReceipt
            };
            Require(ProductionInputDestinationCustodyDrainContract
                    .IsValidSave(child),
                "Prepared fixture child is invalid.");
            Require(ProductionGenericBillTerminalDrainCanonical
                    .IsValidSave(producer),
                "Prepared fixture producer is invalid.");
            return new Fixture
            {
                SourceBill = bill,
                Child = child,
                Producer = producer
            };
        }
    }
}
