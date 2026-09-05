#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CropPhysicalTransactionFixture
{
    private const string PlotId = "building:crop-plot:qa";
    private const string DestinationId = "crop-plot|qa";
    private const string CropId = "crop:twilight-grain";
    private const string SeedItemId = "seed-lot:twilight-grain";
    private const string WaterItemId = "resource:clean-water";
    private const string CertificationKitItemId = "supply:certified-seed-kit";
    private const string TreatmentItemId = "supply:botanical-pesticide";

    [MenuItem("Tools/DungeonStory/Economy/Verify Crop Physical Transactions")]
    public static void VerifyFromMenu()
    {
        if (!Run())
            throw new InvalidOperationException(
                "Crop physical transaction fixture failed.");
        Debug.Log("Crop physical transaction fixture passed.");
    }

    public static bool Run()
    {
        if (!CertifiedSeedOperatingDayGate.TryAdvance(0, 7, out int daySeven)
            || daySeven != 7
            || CertifiedSeedOperatingDayGate.TryAdvance(
                daySeven,
                7,
                out int duplicateDay)
            || duplicateDay != 7
            || CertifiedSeedOperatingDayGate.TryAdvance(
                duplicateDay,
                6,
                out int staleDay)
            || staleDay != 7
            || !CertifiedSeedOperatingDayGate.TryAdvance(
                staleDay,
                8,
                out int dayEight)
            || dayEight != 8)
            return false;

        if (!VerifySeedDeliveryReachabilityContracts())
            return false;
        if (!VerifyCertifiedSeedPlannedDeliveryRecovery())
            return false;

        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [WaterItemId] = 2
        };
        FixtureGateway missing = new(catalog);
        CropPhysicalCommitSaveData missingOwner = new();
        string loneSeed = missing.AddSeed();
        if (CropPhysicalTransactionOutbox.TryCommitOrResume(
                missingOwner,
                CropPhysicalTransactionOutbox.FormatSowOperationId(PlotId, 0),
                CropPhysicalTransactionOutbox.SowReasonCode,
                0,
                DestinationId,
                requirements,
                SeedItemId,
                CropId,
                missing,
                out _,
                out _)
            || missing.Quantity(loneSeed) != 1
            || missingOwner.phase != CropPhysicalCommitPhase.None)
            return false;

        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        string seedStack = gateway.AddSeed();
        string waterA = gateway.Add(WaterItemId, 1);
        string waterB = gateway.Add(WaterItemId, 1);
        CropPhysicalCommitSaveData owner = new();
        string operation = CropPhysicalTransactionOutbox.FormatSowOperationId(
            PlotId,
            0);
        if (!CropPhysicalTransactionOutbox.TryCommitOrResume(
                owner,
                operation,
                CropPhysicalTransactionOutbox.SowReasonCode,
                0,
                DestinationId,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out SeedLotState seedLot,
                out _)
            || seedLot == null
            || owner.phase != CropPhysicalCommitPhase.InputCommitted
            || gateway.Quantity(seedStack) != 0
            || gateway.Quantity(waterA) != 0
            || gateway.Quantity(waterB) != 0)
            return false;

        owner.ecologyBeforeFingerprint = "absent";
        owner.ecologyAfterFingerprint = "qa-after";
        owner.phase = CropPhysicalCommitPhase.OutcomePublished;
        if (CropPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                owner,
                gateway,
                out _))
            return false;

        CropPlotSaveData serializedOwner = new()
        {
            buildingInstanceId = PlotId,
            cropId = CropId,
            phase = CropPlotPhase.ReadyToSow,
            materialsConsumed = true,
            nextSowOperationSequence = 0,
            pendingSow = owner.DeepClone()
        };
        CropPlotSaveData restored = JsonUtility.FromJson<CropPlotSaveData>(
            JsonUtility.ToJson(serializedOwner));
        CropPhysicalOwnerValidationSnapshot restoredOwner = new()
        {
            ExpectedOperationId = operation,
            Owner = restored.pendingSow.DeepClone()
        };
        PhysicalItemRestoreCandidateDispositionSnapshot candidate =
            ToCandidate(owner);
        Validate(new[] { restoredOwner }, candidate);
        if (!Reject(new[] { restoredOwner })
            || !Reject(
                Array.Empty<CropPhysicalOwnerValidationSnapshot>(),
                candidate)
            || !Reject(
                new[] { restoredOwner },
                Copy(candidate, candidate.InputMassGrams + 1L))
            || !CropPhysicalTransactionOutbox.TryCommitOrResume(
                restoredOwner.Owner,
                operation,
                CropPhysicalTransactionOutbox.SowReasonCode,
                0,
                DestinationId,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out _,
                out _)
            || !CropPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                restoredOwner.Owner,
                gateway,
                out _))
            return false;
        bool executionReceiptVerified = VerifyCropPlanExecutionReceipt(
            restoredOwner.Owner);
        CropPhysicalTransactionOutbox.Clear(restoredOwner.Owner);
        Validate(Array.Empty<CropPhysicalOwnerValidationSnapshot>());
        return Reject(
            Array.Empty<CropPhysicalOwnerValidationSnapshot>(),
            candidate)
            && executionReceiptVerified
            && VerifyCertifiedOwner(catalog)
            && VerifyDestroyedPlotLoss(catalog)
            && VerifyDestroyedFacilityLoss(catalog)
            && VerifyPreparedHarvestOwnerSnapshots()
            && VerifyCompletionDeliveryLedger()
            && VerifyCompletionDeliveryRestoreJoin()
            && VerifyCropTreatment(catalog)
            && VerifyFacilityMutationFence();
    }

    private static bool VerifyFacilityMutationFence()
    {
        BuildingInstanceId facilityId = (BuildingInstanceId)PlotId;
        const string OperationId = "qa:crop-facility-mutation";
        ProductionFacilityMutationEpochRuntime mutations = new();
        if (!mutations.TryBegin(
                facilityId,
                OperationId,
                out long epoch,
                out _)
            || ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                mutations,
                facilityId,
                out DomainFailure blocked)
            || blocked.Parameters.Length != 2
            || !string.Equals(
                blocked.Parameters[1],
                "production-facility-mutation-open:transient-topology:"
                + OperationId + ":" + epoch,
                StringComparison.Ordinal)
            || !mutations.TryEnd(
                facilityId,
                OperationId,
                epoch,
                out _)
            || !ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                mutations,
                facilityId,
                out DomainFailure reopened)
            || reopened.IsFailure)
        {
            return false;
        }

        return true;
    }

    private static bool VerifyCropPlanExecutionReceipt(
        CropPhysicalCommitSaveData sow)
    {
        const string ActionId = "qa:crop-cycle:receipt";
        CropCycleExecutionReceiptSaveData active =
            CropPlanExecutionReceiptAuthority.Begin(
                ActionId,
                explicitCorrelation: true,
                PlotId,
                indoor: false,
                sow);
        if (active.status != CropCycleExecutionReceiptStatus.Active
            || !active.explicitCorrelation
            || active.completed
            || active.inputs.Count == 0)
            return false;

        try
        {
            CropPlanExecutionReceiptAuthority.Validate(
                active,
                requireCompleted: true);
            return false;
        }
        catch (InvalidOperationException)
        {
        }

        string harvestLine = CropHarvestOutputMaximumAuthority
            .HarvestOutputLineId(CropId);
        string seedLine = CropHarvestOutputMaximumAuthority
            .SeedOutputLineId(CropId);
        ProductionOutputCapabilitySaveData harvestCapability =
            CreateCapability(
                harvestLine,
                "resource:twilight-grain",
                ProductionOutputCapabilityIds.StandardDefinition,
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion);
        ProductionOutputCapabilitySaveData seedCapability = CreateCapability(
            seedLine,
            SeedItemId,
            ProductionOutputCapabilityIds.CropHarvestSeedLot,
            ProductionOutputCapabilityIds.CropHarvestSeedLotVersion,
            ProductionOutputCapabilityIds.SeedLotStateCodec,
            ProductionOutputCapabilityIds.SeedLotStateCodecVersion);
        string harvestOperation = CropPlotRuntime.FormatHarvestOperationId(
            new BuildingInstanceId(PlotId),
            0);
        CropHarvestOutputSaveData harvest = new()
        {
            phase = CropHarvestOutputPhase
                .OutputRestoredAwaitingFinalization,
            operationSequence = 0,
            operationId = harvestOperation,
            cropId = CropId,
            returnedSeedLot = sow.seedLot.Clone(),
            harvestCapability = harvestCapability,
            seedCapability = seedCapability,
            outputPublication = new ProductionDomainOutputPublicationSaveData
            {
                batchCommitId = CropPlotRuntime.HarvestOutputBatchCommitPrefix
                    + harvestOperation,
                outcomeFingerprint = new string('a', 64),
                plannedOutputFingerprint = new string('b', 64),
                outputMassGrams = 1400L,
                outputAcknowledged = true,
                stacks = new List<ProductionDomainPublishedStackSaveData>
                {
                    new()
                    {
                        outputLineId = harvestLine,
                        itemId = "resource:twilight-grain",
                        stackId = "world-item-stack:qa:crop-output",
                        quantity = 3,
                        massGrams = 1350L
                    },
                    new()
                    {
                        outputLineId = seedLine,
                        itemId = SeedItemId,
                        itemInstanceId = "item-instance:qa:returned-seed",
                        stackId = "world-item-stack:qa:returned-seed",
                        quantity = 1,
                        massGrams = 50L
                    }
                }
            }
        };

        CropCycleExecutionReceiptSaveData completed =
            CropPlanExecutionReceiptAuthority.Complete(active, harvest);
        CropPlanExecutionReceipt publicReceipt =
            CropPlanExecutionReceiptAuthority.ProjectTerminal(
                ActionId,
                completed);
        if (!publicReceipt.Succeeded
            || publicReceipt.InputQuantity != sow.inputQuantity
            || publicReceipt.InputMassGrams != sow.inputMassGrams
            || publicReceipt.OutputMassGrams != 1400L
            || publicReceipt.Outputs.Count != 2
            || publicReceipt.Outputs.Sum(output => output.MassGrams) != 1400L
            || publicReceipt.Outputs.Any(output =>
                string.IsNullOrEmpty(output.CapabilityFingerprint)))
            return false;

        CropCycleExecutionReceiptSaveData roundTrip = JsonUtility.FromJson<
            CropCycleExecutionReceiptSaveData>(JsonUtility.ToJson(completed));
        CropPlanExecutionReceiptAuthority.Validate(
            roundTrip,
            requireCompleted: true);
        if (!string.Equals(
                roundTrip.sourceDigest,
                completed.sourceDigest,
                StringComparison.Ordinal))
            return false;

        CropCycleExecutionReceiptSaveData capabilityTamper =
            completed.DeepClone();
        capabilityTamper.harvestCapability.itemId = SeedItemId;
        if (!RejectExecutionReceipt(capabilityTamper))
            return false;
        CropCycleExecutionReceiptSaveData massTamper = completed.DeepClone();
        massTamper.outputs[0].massGrams++;
        if (!RejectExecutionReceipt(massTamper))
            return false;
        CropCycleExecutionReceiptSaveData scalarGarbage = new()
        {
            inputMassGrams = 1L
        };
        if (!RejectExecutionReceipt(scalarGarbage))
            return false;

        CropCycleExecutionReceiptSaveData failed =
            CropPlanExecutionReceiptAuthority.Fail(
                CropPlanExecutionReceiptAuthority.Begin(
                    ActionId + ":failed",
                    explicitCorrelation: true,
                    PlotId,
                    indoor: false,
                    sow),
                CropCycleExecutionReceiptStatus.FailedCropDeath,
                "crop-cycle-failed-crop-death");
        CropPlanExecutionReceipt failedReceipt =
            CropPlanExecutionReceiptAuthority.ProjectTerminal(
                ActionId + ":failed",
                failed);
        CropCycleExecutionReceiptSaveData failedBeforeSow =
            CropPlanExecutionReceiptAuthority.FailBeforeSow(
                ActionId + ":destroyed-before-sow",
                PlotId,
                CropId,
                indoor: false,
                "crop-cycle-failed-plot-destroyed-before-sow");
        CropPlanExecutionReceipt failedBeforeSowReceipt =
            CropPlanExecutionReceiptAuthority.ProjectTerminal(
                ActionId + ":destroyed-before-sow",
                failedBeforeSow);
        return !failedReceipt.Succeeded
            && failedReceipt.Status
                == CropCycleExecutionReceiptStatus.FailedCropDeath
            && failedReceipt.Outputs.Count == 0
            && string.Equals(
                failedReceipt.FailureReasonCode,
                "crop-cycle-failed-crop-death",
                StringComparison.Ordinal)
            && !failedBeforeSowReceipt.Succeeded
            && failedBeforeSowReceipt.Inputs.Count == 0
            && failedBeforeSowReceipt.InputMassGrams == 0L
            && failedBeforeSowReceipt.Status
                == CropCycleExecutionReceiptStatus.FailedPlotDestroyed;
    }

    private static ProductionOutputCapabilitySaveData CreateCapability(
        string outputLineId,
        string itemId,
        string capabilityId,
        int capabilityVersion,
        string codecId,
        int codecVersion)
    {
        ProductionOutputCapabilityDescriptor descriptor = new(
            outputLineId,
            itemId,
            capabilityId,
            capabilityVersion,
            codecId,
            codecVersion,
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                outputLineId,
                itemId,
                capabilityId,
                capabilityVersion,
                codecId,
                codecVersion));
        return ProductionOutputCapabilitySaveData.Freeze(descriptor);
    }

    private static bool RejectExecutionReceipt(
        CropCycleExecutionReceiptSaveData receipt)
    {
        try
        {
            CropPlanExecutionReceiptAuthority.Validate(
                receipt,
                requireCompleted: false);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyCompletionDeliveryLedger()
    {
        WorkCompletionIdentityDeliveryRequest first =
            CreateCompletionDeliveryRequest(0);
        WorkCompletionIdentityDeliveryLedger ledger = new();
        ledger.BeginApply(first);
        bool captureBlocked;
        try
        {
            ledger.Capture();
            captureBlocked = false;
        }
        catch (InvalidOperationException)
        {
            captureBlocked = true;
        }
        finally
        {
            ledger.EndApply(first);
        }
        if (!captureBlocked
            || ledger.Inspect(first, out _) !=
                WorkCompletionIdentityDeliveryStatus.Applied
            || ledger.Commit(first, out _) !=
                WorkCompletionIdentityDeliveryStatus.Applied
            || ledger.Inspect(first, out _) !=
                WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            || ledger.Commit(first, out _) !=
                WorkCompletionIdentityDeliveryStatus.AlreadyApplied)
            return false;

        WorkCompletionIdentityDeliveryRequest conflicting = new(
            first.DeliveryId,
            first.ProducerStreamId,
            first.OperationSequence,
            first.Character,
            first.WorkId,
            first.ProductId + ":conflict",
            first.Origin,
            first.AbsoluteDay);
        WorkCompletionIdentityDeliveryRequest gap =
            CreateCompletionDeliveryRequest(2);
        if (ledger.Inspect(conflicting, out _) !=
                WorkCompletionIdentityDeliveryStatus.Conflict
            || ledger.Inspect(gap, out _) !=
                WorkCompletionIdentityDeliveryStatus.Conflict)
            return false;

        WorkCompletionIdentityDeliveryLedger restored = new();
        restored.Restore(ledger.Capture());
        WorkCompletionIdentityDeliveryRequest second =
            CreateCompletionDeliveryRequest(1);
        if (restored.Inspect(first, out _) !=
                WorkCompletionIdentityDeliveryStatus.AlreadyApplied
            || restored.Commit(second, out _) !=
                WorkCompletionIdentityDeliveryStatus.Applied
            || restored.Inspect(first, out _) !=
                WorkCompletionIdentityDeliveryStatus.Conflict)
            return false;

        if (!restored.RetireProducerStream(first.ProducerStreamId)
            || restored.Capture().Count != 0
            || !restored.RetireProducerStream(first.ProducerStreamId))
            return false;

        WorkCompletionIdentityDeliveryLedger terminal = new();
        if (terminal.Commit(
                first,
                out _,
                WorkCompletionIdentityDeliveryDisposition
                    .TerminalRecipientUnavailable)
            != WorkCompletionIdentityDeliveryStatus.Applied
            || terminal.Capture().Single().disposition !=
                WorkCompletionIdentityDeliveryDisposition
                    .TerminalRecipientUnavailable)
            return false;

        WorkCompletionIdentityDeliveryCursorSaveData duplicate =
            ToCompletionCursor(second);
        try
        {
            restored.Restore(new[] { duplicate, duplicate.Clone() });
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyCompletionDeliveryRestoreJoin()
    {
        const string CompletionPlotId =
            "building:crop-plot:qa:completion-delivery";
        CropHarvestOutputSaveData pending = new()
        {
            phase = CropHarvestOutputPhase.Frozen,
            operationSequence = 0,
            operationId = CropPlotRuntime.FormatHarvestOperationId(
                new BuildingInstanceId(CompletionPlotId),
                0),
            harvesterId = "character:qa:harvester",
            outcomeId = "normal",
            completionAbsoluteDay = 3
        };
        WorkCompletionIdentityDeliveryRequest request =
            CropPlotRuntime.CreateHarvestCompletionDelivery(
                new BuildingInstanceId(CompletionPlotId),
                pending);
        pending.completionDeliveryId = request.DeliveryId;
        pending.completionDeliveryFingerprint = request.PayloadFingerprint;
        CropPlotSaveData plot = new()
        {
            buildingInstanceId = CompletionPlotId,
            nextHarvestOperationSequence = 0,
            pendingHarvest = pending
        };
        WorkCompletionIdentityDeliveryCursorSaveData current =
            ToCompletionCursor(request);

        ValidateCompletionJoin(new[] { plot });
        ValidateCompletionJoin(new[] { plot }, new[] { current });
        plot.pendingHarvest.completionEventPublished = true;
        ValidateCompletionJoin(new[] { plot }, new[] { current });

        WorkCompletionIdentityDeliveryRequest next =
            CreateCompletionDeliveryRequest(
                1,
                CompletionPlotId,
                "character:qa:harvester");
        WorkCompletionIdentityDeliveryCursorSaveData ahead =
            ToCompletionCursor(next);
        WorkCompletionIdentityDeliveryCursorSaveData wrongFingerprint =
            current.Clone();
        wrongFingerprint.payloadFingerprint = new string('0', 64);
        if (!RejectCompletionJoin(new[] { plot })
            || !RejectCompletionJoin(new[] { plot }, new[] { ahead })
            || !RejectCompletionJoin(
                new[] { plot },
                new[] { wrongFingerprint }))
            return false;

        plot.pendingHarvest = new CropHarvestOutputSaveData();
        plot.nextHarvestOperationSequence = 1;
        ValidateCompletionJoin(new[] { plot }, new[] { current });
        if (!RejectCompletionJoin(new[] { plot }, new[] { ahead })
            || !RejectCompletionJoin(
                Array.Empty<CropPlotSaveData>(),
                new[] { current })
            || !RejectCompletionJoin(
                new[] { plot, new CropPlotSaveData
                {
                    buildingInstanceId = CompletionPlotId,
                    nextHarvestOperationSequence = 1,
                    pendingHarvest = new CropHarvestOutputSaveData()
                } },
                new[] { current }))
            return false;

        WorkCompletionIdentityDeliveryCursorSaveData unrelated = new()
        {
            producerStreamId = "research-completion:qa",
            operationSequence = 0,
            deliveryId = "identity-event:research:qa:000000",
            payloadFingerprint = new string('a', 64)
        };
        ValidateCompletionJoin(new[] { plot }, new[] { current, unrelated });

        CropHarvestOutputSaveData workerless = pending.DeepClone();
        workerless.harvesterId = string.Empty;
        workerless.completionDeliveryId = string.Empty;
        workerless.completionDeliveryFingerprint = string.Empty;
        workerless.completionEventPublished = false;
        plot.pendingHarvest = workerless;
        plot.nextHarvestOperationSequence = 0;
        ValidateCompletionJoin(new[] { plot });
        plot.pendingHarvest.completionDeliveryId = request.DeliveryId;
        if (!RejectCompletionJoin(new[] { plot }))
            return false;
        plot.pendingHarvest.completionDeliveryId = string.Empty;
        if (!RejectCompletionJoin(new[] { plot }, new[] { current }))
            return false;

        CropHarvestOutputSaveData nextPending = new()
        {
            phase = CropHarvestOutputPhase.Frozen,
            operationSequence = 1,
            operationId = CropPlotRuntime.FormatHarvestOperationId(
                new BuildingInstanceId(CompletionPlotId),
                1),
            harvesterId = "character:qa:harvester",
            outcomeId = "normal",
            completionAbsoluteDay = 3
        };
        WorkCompletionIdentityDeliveryRequest nextRequest =
            CropPlotRuntime.CreateHarvestCompletionDelivery(
                new BuildingInstanceId(CompletionPlotId),
                nextPending);
        nextPending.completionDeliveryId = nextRequest.DeliveryId;
        nextPending.completionDeliveryFingerprint =
            nextRequest.PayloadFingerprint;
        plot.pendingHarvest = nextPending;
        plot.nextHarvestOperationSequence = 1;
        ValidateCompletionJoin(new[] { plot }, new[] { current });
        WorkCompletionIdentityDeliveryCursorSaveData wrongPrevious =
            current.Clone();
        wrongPrevious.deliveryId += ":wrong";
        if (!RejectCompletionJoin(new[] { plot }, new[] { wrongPrevious })
            || !RejectCompletionJoin(
                new CropPlotSaveData[] { null },
                Array.Empty<
                    WorkCompletionIdentityDeliveryCursorSaveData>())
            || !RejectCompletionJoin(
                new[] { new CropPlotSaveData
                {
                    buildingInstanceId = " "
                } }))
            return false;

        return true;
    }

    private static WorkCompletionIdentityDeliveryRequest
        CreateCompletionDeliveryRequest(
            int sequence,
            string plotId = "building:crop-plot:qa:ledger",
            string characterId = "character:qa:ledger")
    {
        string operationId = CropPlotRuntime.FormatHarvestOperationId(
            new BuildingInstanceId(plotId),
            sequence);
        return new WorkCompletionIdentityDeliveryRequest(
            CropPlotRuntime.HarvestCompletionDeliveryPrefix + operationId,
            CropPlotRuntime.HarvestCompletionStreamPrefix + plotId,
            sequence,
            new CharacterId(characterId),
            BuiltInWorkTypeIds.Harvest.Value,
            plotId + ":normal",
            CharacterCommandOrigin.Autonomous,
            3);
    }

    private static WorkCompletionIdentityDeliveryCursorSaveData
        ToCompletionCursor(WorkCompletionIdentityDeliveryRequest request) =>
        new()
        {
            producerStreamId = request.ProducerStreamId,
            operationSequence = request.OperationSequence,
            deliveryId = request.DeliveryId,
            payloadFingerprint = request.PayloadFingerprint
        };

    private static void ValidateCompletionJoin(
        IReadOnlyCollection<CropPlotSaveData> plots,
        IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData> deliveries =
            null) =>
        CropHarvestCompletionDeliveryRestoreJoin.Validate(
            plots,
            deliveries ??
                Array.Empty<WorkCompletionIdentityDeliveryCursorSaveData>());

    private static bool RejectCompletionJoin(
        IReadOnlyCollection<CropPlotSaveData> plots,
        IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData> deliveries =
            null)
    {
        try
        {
            ValidateCompletionJoin(plots, deliveries);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyPreparedHarvestOwnerSnapshots()
    {
        const string NormalPlotId = "building:crop-plot:qa:harvest-normal";
        const string GoldenPlotId = "building:crop-plot:qa:harvest-golden";

        CropHarvestOwnerValidationSnapshot normal = CreateHarvestOwnerSnapshot(
            NormalPlotId,
            golden: false);
        CropEcologyPreparedHarvestSnapshot normalEcology =
            CreateEcologyHarvestReceipt(normal);
        ValidatePreparedHarvestOwners(
            new[] { normal },
            new[] { normalEcology });

        CropHarvestOwnerValidationSnapshot normalAcknowledged =
            CloneHarvestOwnerSnapshot(normal);
        normalAcknowledged.Owner.ecologyAcknowledged = true;
        ValidatePreparedHarvestOwners(new[] { normalAcknowledged });

        if (!RejectPreparedHarvestOwners(new[] { normal })
            || !RejectPreparedHarvestOwners(
                Array.Empty<CropHarvestOwnerValidationSnapshot>(),
                new[] { normalEcology })
            || !RejectPreparedHarvestOwners(
                new[] { normal, CloneHarvestOwnerSnapshot(normal) },
                new[] { normalEcology })
            || !RejectPreparedHarvestOwners(
                new[] { normal },
                new[] { normalEcology, normalEcology })
            || !RejectPreparedHarvestOwners(
                new[] { normal },
                new[]
                {
                    CreateEcologyHarvestReceipt(
                        normal,
                        plotId: NormalPlotId + ":mismatch")
                })
            || !RejectPreparedHarvestOwners(
                new[] { normal },
                new[]
                {
                    CreateEcologyHarvestReceipt(
                        normal,
                        fingerprint: "ecology-outcome:mismatch")
                })
            || !RejectPreparedHarvestOwners(
                new[] { normal },
                new[]
                {
                    CreateEcologyHarvestReceipt(normal, committed: true)
                })
            || !RejectPreparedHarvestOwners(
                new[] { normal },
                new[]
                {
                    CreateEcologyHarvestReceipt(
                        normal,
                        returnedSeedLot: CreateHarvestSeedLot(
                            generation: normal.Owner.returnedSeedLot.generation + 1))
                }))
        {
            return false;
        }

        CropHarvestOwnerValidationSnapshot golden = CreateHarvestOwnerSnapshot(
            GoldenPlotId,
            golden: true);
        CropEcologyPreparedHarvestSnapshot goldenEcology =
            CreateEcologyHarvestReceipt(golden);
        GoldenHarvestPreparedResolution goldenReceipt =
            CreateGoldenHarvestReceipt(golden);
        ValidatePreparedHarvestOwners(
            new[] { golden },
            new[] { goldenEcology },
            new[] { goldenReceipt });

        CropHarvestOwnerValidationSnapshot ecologyAcknowledged =
            CloneHarvestOwnerSnapshot(golden);
        ecologyAcknowledged.Owner.ecologyAcknowledged = true;
        ValidatePreparedHarvestOwners(
            new[] { ecologyAcknowledged },
            goldenReceipts: new[] { goldenReceipt });

        CropHarvestOwnerValidationSnapshot goldenAcknowledged =
            CloneHarvestOwnerSnapshot(golden);
        goldenAcknowledged.Owner.goldenAcknowledged = true;
        ValidatePreparedHarvestOwners(
            new[] { goldenAcknowledged },
            new[] { goldenEcology });

        CropHarvestOwnerValidationSnapshot fullyAcknowledged =
            CloneHarvestOwnerSnapshot(golden);
        fullyAcknowledged.Owner.ecologyAcknowledged = true;
        fullyAcknowledged.Owner.goldenAcknowledged = true;
        ValidatePreparedHarvestOwners(new[] { fullyAcknowledged });

        return RejectPreparedHarvestOwners(
                Array.Empty<CropHarvestOwnerValidationSnapshot>(),
                goldenReceipts: new[] { goldenReceipt })
            && RejectPreparedHarvestOwners(
                new[] { normal },
                new[] { normalEcology },
                new[] { CreateGoldenHarvestReceipt(normal) })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[] { goldenReceipt, goldenReceipt })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(
                        golden,
                        fieldId: GoldenPlotId + ":mismatch")
                })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(
                        golden,
                        characterId: "character:qa:mismatch")
                })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(
                        golden,
                        traitDefinitionId: "trait:qa:mismatch")
                })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(
                        golden,
                        fingerprint: "golden-outcome:mismatch")
                })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(golden, committed: true)
                })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(
                        golden,
                        outcome: ExtremeRiskOutcome.Loss)
                })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(
                        golden,
                        primaryMultiplier:
                            golden.Owner.goldenPrimaryMultiplier + 0.25f)
                })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(
                        golden,
                        secondaryMultiplier:
                            golden.Owner.goldenSecondaryMultiplier + 0.25f)
                })
            && RejectPreparedHarvestOwners(
                new[] { golden },
                new[] { goldenEcology },
                new[]
                {
                    CreateGoldenHarvestReceipt(
                        golden,
                        rollHash: golden.Owner.goldenRollHash + 1UL)
                });
    }

    private static CropHarvestOwnerValidationSnapshot
        CreateHarvestOwnerSnapshot(string plotId, bool golden)
    {
        const int OperationSequence = 7;
        SeedLotState returnedSeedLot = CreateHarvestSeedLot();
        return new CropHarvestOwnerValidationSnapshot
        {
            PlotId = plotId,
            Owner = new CropHarvestOutputSaveData
            {
                phase = CropHarvestOutputPhase.Frozen,
                operationSequence = OperationSequence,
                operationId = "crop-harvest:" + plotId + ":000007",
                cropId = CropId,
                harvesterId = golden ? "character:qa:golden-harvester" : string.Empty,
                ecologyOutcomeFingerprint = "ecology-outcome:" + plotId,
                ecologyCommitted = false,
                ecologyAcknowledged = false,
                goldenPrepared = golden,
                goldenTraitDefinitionId = golden
                    ? "trait:qa:golden-harvest"
                    : string.Empty,
                goldenOutcomeFingerprint = golden
                    ? "golden-outcome:" + plotId
                    : string.Empty,
                goldenOutcome = golden
                    ? ExtremeRiskOutcome.Jackpot
                    : ExtremeRiskOutcome.Normal,
                goldenPrimaryMultiplier = golden ? 2f : 0f,
                goldenSecondaryMultiplier = golden ? 0.75f : 0f,
                goldenRollHash = golden ? 0xA11CEUL : 0UL,
                goldenCommitted = false,
                goldenAcknowledged = false,
                returnedSeedLot = returnedSeedLot
            }
        };
    }

    private static CropHarvestOwnerValidationSnapshot
        CloneHarvestOwnerSnapshot(CropHarvestOwnerValidationSnapshot source) =>
        new()
        {
            PlotId = source.PlotId,
            Owner = source.Owner.DeepClone()
        };

    private static SeedLotState CreateHarvestSeedLot(int generation = 3) => new()
    {
        cropId = CropId,
        cultivarGenomeId = "genome:twilight-grain:qa-harvest",
        generation = generation,
        pathogenLoad = 12.5f
    };

    private static CropEcologyPreparedHarvestSnapshot
        CreateEcologyHarvestReceipt(
        CropHarvestOwnerValidationSnapshot owner,
        string plotId = null,
        string fingerprint = null,
        bool? committed = null,
        SeedLotState returnedSeedLot = null) => new(
            owner.Owner.operationId,
            plotId ?? owner.PlotId,
            fingerprint ?? owner.Owner.ecologyOutcomeFingerprint,
            committed ?? owner.Owner.ecologyCommitted,
            new CropHarvestEcologyResult(
                1.25f,
                1,
                returnedSeedLot ?? owner.Owner.returnedSeedLot.Clone()));

    private static GoldenHarvestPreparedResolution CreateGoldenHarvestReceipt(
        CropHarvestOwnerValidationSnapshot owner,
        string fieldId = null,
        string characterId = null,
        string traitDefinitionId = null,
        string fingerprint = null,
        bool? committed = null,
        ExtremeRiskOutcome? outcome = null,
        float? primaryMultiplier = null,
        float? secondaryMultiplier = null,
        ulong? rollHash = null) => new(
            owner.Owner.operationId,
            characterId ?? owner.Owner.harvesterId,
            traitDefinitionId ?? owner.Owner.goldenTraitDefinitionId,
            fieldId ?? owner.PlotId,
            fingerprint ?? owner.Owner.goldenOutcomeFingerprint,
            committed ?? owner.Owner.goldenCommitted,
            new ExtremeRiskResolution(
                outcome ?? owner.Owner.goldenOutcome,
                primaryMultiplier ?? owner.Owner.goldenPrimaryMultiplier,
                secondaryMultiplier ?? owner.Owner.goldenSecondaryMultiplier,
                0f,
                rollHash ?? owner.Owner.goldenRollHash));

    private static void ValidatePreparedHarvestOwners(
        IReadOnlyCollection<CropHarvestOwnerValidationSnapshot> owners,
        IReadOnlyList<CropEcologyPreparedHarvestSnapshot> ecologyReceipts = null,
        IReadOnlyList<GoldenHarvestPreparedResolution> goldenReceipts = null) =>
        CropPhysicalRestoreGuard.ValidatePreparedHarvestOwnerSnapshots(
            owners,
            ecologyReceipts ?? Array.Empty<CropEcologyPreparedHarvestSnapshot>(),
            goldenReceipts ?? Array.Empty<GoldenHarvestPreparedResolution>());

    private static bool RejectPreparedHarvestOwners(
        IReadOnlyCollection<CropHarvestOwnerValidationSnapshot> owners,
        IReadOnlyList<CropEcologyPreparedHarvestSnapshot> ecologyReceipts = null,
        IReadOnlyList<GoldenHarvestPreparedResolution> goldenReceipts = null)
    {
        try
        {
            ValidatePreparedHarvestOwners(
                owners,
                ecologyReceipts,
                goldenReceipts);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyCropTreatment(
        IDungeonItemCatalogProvider catalog)
    {
        const int Sequence = 7;
        const string TreatmentPlotId =
            "building:crop-plot:qa:treatment:colon-safe";
        const string TreatmentDestination =
            "crop-plot|qa|treatment";
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        string treatmentStack = gateway.Add(
            TreatmentItemId,
            1,
            TreatmentDestination);
        CropEcologyPlotSaveData ecologyBefore = CreateTreatmentEcology(
            TreatmentPlotId,
            pestPressure: 55f);
        CropTreatmentOrderSaveData owner = CreateTreatmentOwner(
            TreatmentPlotId,
            TreatmentDestination,
            Sequence);
        owner.ecologyBeforeFingerprint =
            CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
                new[] { ecologyBefore },
                TreatmentPlotId);

        if (!CropTreatmentPhysicalOutbox.TryCommitOrResume(
                owner,
                gateway,
                out _)
            || owner.phase != CropTreatmentOrderPhase.InputCommitted
            || gateway.Quantity(treatmentStack) != 0
            || owner.sourceStackIds.Count != 1
            || owner.inputMassGrams <= 0L
            || !gateway.HasPending(owner.operationId))
            return false;

        PhysicalItemRestoreCandidateDispositionSnapshot inputCandidate =
            ToTreatmentCandidate(owner);
        CropTreatmentOwnerValidationSnapshot inputState =
            CreateTreatmentSnapshot(
            TreatmentPlotId,
            Sequence,
            owner.DeepClone());
        CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
            new[] { inputState },
            new CandidateQuery(inputCandidate));
        CropPhysicalRestoreGuard.ValidateTreatmentEcologyEnvelope(
            TreatmentPlotId,
            inputState.Owner,
            new[] { ecologyBefore });
        CropEcologyPlotSaveData ecologyMismatch = CreateTreatmentEcology(
            TreatmentPlotId,
            pestPressure: 54f);
        if (!RejectTreatmentEcology(inputState, ecologyMismatch))
            return false;

        RecordingTreatmentTare tare = new();
        if (!CropTreatmentPhysicalOutbox.EnsureTareOutputs(
                owner,
                new Vector2Int(4, 9),
                tare,
                out _)
            || !CropTreatmentPhysicalOutbox.EnsureTareOutputs(
                owner,
                new Vector2Int(4, 9),
                tare,
                out _)
            || tare.CallCount != 2
            || !string.Equals(
                tare.LastParentCommitId,
                owner.commitId,
                StringComparison.Ordinal))
            return false;

        CropEcologyPlotSaveData ecologyAfter = CreateTreatmentEcology(
            TreatmentPlotId,
            pestPressure: 20f);
        owner.ecologyAfterFingerprint =
            CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
                new[] { ecologyAfter },
                TreatmentPlotId);
        owner.phase = CropTreatmentOrderPhase.OutcomePublished;
        if (CropTreatmentPhysicalOutbox.TryAcknowledgeOutcome(
                owner,
                gateway,
                out _))
            return false;

        CropTreatmentOrderSaveData restored = JsonUtility.FromJson<
            CropTreatmentOrderSaveData>(JsonUtility.ToJson(owner));
        CropTreatmentOwnerValidationSnapshot restoredState =
            CreateTreatmentSnapshot(
            TreatmentPlotId,
            Sequence,
            restored);
        CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
            new[] { restoredState },
            new CandidateQuery(inputCandidate));
        if (!RejectTreatmentOwners(new[] { restoredState })
            || !RejectTreatmentOwners(
                Array.Empty<CropTreatmentOwnerValidationSnapshot>(),
                inputCandidate)
            || !RejectTreatmentOwners(
                new[] { restoredState },
                Copy(inputCandidate, inputCandidate.InputMassGrams + 1L))
            || !RejectTreatmentOwners(
                new[] { restoredState },
                Copy(
                    inputCandidate,
                    inputCandidate.InputMassGrams,
                    PhysicalItemDispositionKind.Transfer))
            || !RejectTreatmentOwners(
                new[] { restoredState },
                Copy(
                    inputCandidate,
                    inputCandidate.InputMassGrams,
                    inputCandidate.Kind,
                    inputCandidate.RequestFingerprint + ":tampered"))
            || !CropTreatmentPhysicalOutbox.TryCommitOrResume(
                restored,
                gateway,
                out _)
            || gateway.Quantity(treatmentStack) != 0
            || !CropTreatmentPhysicalOutbox.TryAcknowledgeOutcome(
                restored,
                gateway,
                out _)
            || gateway.HasPending(restored.operationId))
            return false;

        CropTreatmentPhysicalOutbox.Clear(restored);
        restoredState.Owner = restored;
        CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
            new[] { restoredState },
            new CandidateQuery());
        return VerifyDestroyedTreatmentLoss(catalog);
    }

    private static bool VerifyDestroyedTreatmentLoss(
        IDungeonItemCatalogProvider catalog)
    {
        const int Sequence = 11;
        const string TreatmentPlotId =
            "building:crop-plot:qa:treatment:destroyed";
        const string TreatmentDestination =
            "crop-plot|qa|treatment|destroyed";
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        string treatmentStack = gateway.Add(
            TreatmentItemId,
            1,
            TreatmentDestination);
        CropTreatmentOrderSaveData owner = CreateTreatmentOwner(
            TreatmentPlotId,
            TreatmentDestination,
            Sequence);
        owner.ecologyBeforeFingerprint =
            CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
                new[]
                {
                    CreateTreatmentEcology(TreatmentPlotId, 55f)
                },
                TreatmentPlotId);
        if (!CropTreatmentPhysicalOutbox.TryCommitOrResume(
                owner,
                gateway,
                out _)
            || gateway.Quantity(treatmentStack) != 0)
            return false;

        PhysicalItemRestoreCandidateDispositionSnapshot candidate =
            ToTreatmentCandidate(owner);
        if (CropTreatmentPhysicalOutbox.TryAcknowledgeDestroyedPlotLoss(
                owner,
                gateway,
                out _)
            || owner.phase
                != CropTreatmentOrderPhase.PlotDestroyedLossPending
            || owner.terminalDisposition
                != CropTreatmentTerminalDisposition.DestroyedWithPlotLoss
            || !CropTreatmentPhysicalOutbox.ValidateDestroyedPlotLoss(
                owner,
                out _))
            return false;

        CropTreatmentOrderSaveData restored = JsonUtility.FromJson<
            CropTreatmentOrderSaveData>(JsonUtility.ToJson(owner));
        CropTreatmentOwnerValidationSnapshot restoredState =
            CreateTreatmentSnapshot(
            TreatmentPlotId,
            Sequence,
            restored);
        CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
            new[] { restoredState },
            new CandidateQuery(candidate));
        CropTreatmentOrderSaveData tampered = restored.DeepClone();
        tampered.terminalLossMassGrams = checked(
            tampered.terminalLossMassGrams + 1L);
        if (CropTreatmentPhysicalOutbox.ValidateDestroyedPlotLoss(
                tampered,
                out _)
            || !CropTreatmentPhysicalOutbox.TryAcknowledgeDestroyedPlotLoss(
                restored,
                gateway,
                out _)
            || gateway.HasPending(restored.operationId))
            return false;

        CropTreatmentPhysicalOutbox.Clear(restored);
        return restored.phase == CropTreatmentOrderPhase.None
            && restored.terminalDisposition
                == CropTreatmentTerminalDisposition.None
            && restored.terminalLossMassGrams == 0L;
    }

    private static CropTreatmentOrderSaveData CreateTreatmentOwner(
        string plotId,
        string destinationId,
        int sequence) => new()
    {
        phase = CropTreatmentOrderPhase.Working,
        operationSequence = sequence,
        operationId = CropTreatmentPhysicalOutbox.FormatOperationId(
            plotId,
            sequence),
        reasonCode = CropTreatmentPhysicalOutbox.ReasonCode,
        destinationId = destinationId,
        itemId = TreatmentItemId,
        treatmentKind = CropTreatmentKind.BotanicalPesticide,
        quantity = 1,
        requiredWork = 5f,
        completedWork = 5f,
        effectAmount = 35f,
        cooldownDays = 2,
        scheduledAbsoluteDay = 9
    };

    private static CropTreatmentOwnerValidationSnapshot
        CreateTreatmentSnapshot(
        string plotId,
        int sequence,
        CropTreatmentOrderSaveData owner) => new()
    {
        PlotId = plotId,
        NextOperationSequence = sequence,
        Owner = owner
    };

    private static CropEcologyPlotSaveData CreateTreatmentEcology(
        string plotId,
        float pestPressure) => new()
    {
        plotId = plotId,
        cropId = CropId,
        cultivarGenomeId = "genome:twilight-grain:base",
        currentGroup = CropFamilyGroup.Grain,
        fertility = 80f,
        pestPressure = pestPressure,
        diseasePressure = 10f
    };

    private static bool RejectTreatmentOwners(
        IReadOnlyCollection<CropTreatmentOwnerValidationSnapshot> owners,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
    {
        try
        {
            CropPhysicalRestoreGuard.ValidateTreatmentOwnerSnapshots(
                owners,
                new CandidateQuery(receipts));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool RejectTreatmentEcology(
        CropTreatmentOwnerValidationSnapshot owner,
        CropEcologyPlotSaveData ecology)
    {
        try
        {
            CropPhysicalRestoreGuard.ValidateTreatmentEcologyEnvelope(
                owner.PlotId,
                owner.Owner,
                new[] { ecology });
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyDestroyedPlotLoss(
        IDungeonItemCatalogProvider catalog)
    {
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [WaterItemId] = 2
        };
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        string seedStack = gateway.AddSeed();
        string waterStack = gateway.Add(WaterItemId, 2);
        CropPhysicalCommitSaveData owner = new();
        string operation = CropPhysicalTransactionOutbox.FormatSowOperationId(
            PlotId + ":destroyed",
            0);
        if (!CropPhysicalTransactionOutbox.TryCommitOrResume(
                owner,
                operation,
                CropPhysicalTransactionOutbox.SowReasonCode,
                0,
                DestinationId,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out _,
                out _)
            || gateway.Quantity(seedStack) != 0
            || gateway.Quantity(waterStack) != 0)
            return false;

        owner.ecologyBeforeFingerprint = "absent";
        PhysicalItemRestoreCandidateDispositionSnapshot pending =
            ToCandidate(owner);
        if (CropPhysicalTransactionOutbox.TryAcknowledgeDestroyedPlotLoss(
                owner,
                gateway,
                out _)
            || owner.phase
                != CropPhysicalCommitPhase.PlotDestroyedLossPending
            || owner.terminalDisposition
                != CropWipTerminalDisposition.DestroyedWithPlotLoss
            || owner.terminalLossQuantity != owner.inputQuantity
            || owner.terminalLossMassGrams != owner.inputMassGrams
            || !string.Equals(
                owner.terminalOperationId,
                CropPhysicalTransactionOutbox
                    .FormatDestroyedPlotLossOperationId(operation),
                StringComparison.Ordinal)
            || !CropPhysicalTransactionOutbox.ValidateDestroyedPlotLoss(
                owner,
                out _))
            return false;

        CropPhysicalCommitSaveData restored = JsonUtility.FromJson<
            CropPhysicalCommitSaveData>(JsonUtility.ToJson(owner));
        CropPhysicalOwnerValidationSnapshot restoredOwner = new()
        {
            ExpectedOperationId = operation,
            Owner = restored
        };
        Validate(new[] { restoredOwner }, pending);

        CropPhysicalCommitSaveData tampered = restored.DeepClone();
        tampered.terminalLossMassGrams = checked(
            tampered.terminalLossMassGrams + 1L);
        if (CropPhysicalTransactionOutbox.ValidateDestroyedPlotLoss(
                tampered,
                out _)
            || !CropPhysicalTransactionOutbox.TryAcknowledgeDestroyedPlotLoss(
                restored,
                gateway,
                out _)
            || gateway.HasPending(operation))
            return false;

        CropPhysicalTransactionOutbox.Clear(restored);
        return restored.phase == CropPhysicalCommitPhase.None
            && restored.terminalDisposition
                == CropWipTerminalDisposition.None
            && restored.terminalLossMassGrams == 0L;
    }

    private static bool VerifyDestroyedFacilityLoss(
        IDungeonItemCatalogProvider catalog)
    {
        const string OrderId = "certified-seed-order:00000017";
        string Destination = CertifiedSeedInputOwnerAuthority.BuildDestinationId(
            "building:greenhouse:destroyed",
            CropId,
            17);
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [CertificationKitItemId] = 1
        };
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        gateway.AddSeed(Destination);
        gateway.Add(CertificationKitItemId, 1, Destination);
        CropPhysicalCommitSaveData owner = new();
        string operation =
            CropPhysicalTransactionOutbox.FormatCertifiedOperationId(OrderId);
        if (!CropPhysicalTransactionOutbox.TryCommitOrResume(
                owner,
                operation,
                CropPhysicalTransactionOutbox.CertifiedReasonCode,
                17,
                Destination,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out _,
                out _)
            || CropPhysicalTransactionOutbox
                .TryAcknowledgeDestroyedFacilityLoss(owner, gateway, out _)
            || owner.phase
                != CropPhysicalCommitPhase.FacilityDestroyedLossPending
            || owner.terminalDisposition
                != CropWipTerminalDisposition.DestroyedWithFacilityLoss
            || !CropPhysicalTransactionOutbox.ValidateDestroyedFacilityLoss(
                owner,
                out _))
        {
            return false;
        }

        CropPhysicalCommitSaveData restored = JsonUtility.FromJson<
            CropPhysicalCommitSaveData>(JsonUtility.ToJson(owner));
        return CropPhysicalTransactionOutbox
                .TryAcknowledgeDestroyedFacilityLoss(restored, gateway, out _)
            && !gateway.HasPending(operation);
    }

    private static bool VerifyCertifiedOwner(
        IDungeonItemCatalogProvider catalog)
    {
        const int Sequence = 3;
        const string OrderId = "certified-seed-order:00000003";
        string Destination = CertifiedSeedInputOwnerAuthority.BuildDestinationId(
            "building:greenhouse:qa",
            CropId,
            3);
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [CertificationKitItemId] = 1
        };
        FixtureGateway gateway = new(catalog);
        gateway.AddSeed(Destination);
        gateway.Add(CertificationKitItemId, 1, Destination);
        CropPhysicalCommitSaveData owner = new();
        string operation =
            CropPhysicalTransactionOutbox.FormatCertifiedOperationId(OrderId);
        if (!CropPhysicalTransactionOutbox.TryCommitOrResume(
                owner,
                operation,
                CropPhysicalTransactionOutbox.CertifiedReasonCode,
                Sequence,
                Destination,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out SeedLotState source,
                out _))
            return false;
        SeedLotState certified = source.Clone();
        certified.pathogenLoad = Mathf.Max(0f, certified.pathogenLoad - 30f);
        CertifiedSeedWorldSaveData payload = new()
        {
            nextOrderSequence = 4,
            lastProcessedOperatingDay = 17,
            orders = new List<CertifiedSeedOrderSaveData>
            {
                new()
                {
                    orderId = OrderId,
                    orderSequence = Sequence,
                    actionId = "qa:certified-seed",
                    facilityInstanceId = "building:greenhouse:qa",
                    cropId = CropId,
                    destinationId = Destination,
                    phase = CertifiedSeedOrderPhase.InputCommitted,
                    pendingInput = owner.DeepClone(),
                    certifiedSeedLot = certified,
                    outputCapability = ProductionOutputCapabilitySaveData.Freeze(
                        new ProductionOutputCapabilityDescriptor(
                            CertifiedSeedOutputCapability.OutputLineId,
                            SeedItemId,
                            ProductionOutputCapabilityIds.CertifiedSeed,
                            ProductionOutputCapabilityIds.CertifiedSeedVersion,
                            ProductionOutputCapabilityIds.SeedLotStateCodec,
                            ProductionOutputCapabilityIds.SeedLotStateCodecVersion,
                            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                                CertifiedSeedOutputCapability.OutputLineId,
                                SeedItemId,
                                ProductionOutputCapabilityIds.CertifiedSeed,
                                ProductionOutputCapabilityIds.CertifiedSeedVersion,
                                ProductionOutputCapabilityIds.SeedLotStateCodec,
                                ProductionOutputCapabilityIds.SeedLotStateCodecVersion)))
                }
            }
        };
        CertifiedSeedWorldSaveData restored =
            JsonUtility.FromJson<CertifiedSeedWorldSaveData>(
                JsonUtility.ToJson(payload));
        if (restored == null
            || restored.version != CertifiedSeedWorldSaveData.CurrentVersion
            || restored.lastProcessedOperatingDay != 17
            || restored.orders.Count != 1
            || restored.orders[0].pendingInput.inputMassGrams
                != owner.inputMassGrams
            || restored.orders[0].outputCapability == null
            || restored.orders[0].outputCapability.IsEmpty
            || !string.Equals(
                restored.orders[0].outputCapability.outputLineId,
                CertifiedSeedOutputCapability.OutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                restored.orders[0].outputCapability.itemId,
                SeedItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                restored.orders[0].outputCapability.capabilityId,
                ProductionOutputCapabilityIds.CertifiedSeed,
                StringComparison.Ordinal)
            || !string.Equals(
                restored.orders[0].outputCapability.fingerprint,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    restored.orders[0].outputCapability.outputLineId,
                    restored.orders[0].outputCapability.itemId,
                    restored.orders[0].outputCapability.capabilityId,
                    restored.orders[0].outputCapability.capabilityVersion,
                    restored.orders[0].outputCapability.componentCodecId,
                    restored.orders[0].outputCapability.componentCodecVersion),
                StringComparison.Ordinal))
            return false;
        PhysicalItemRestoreCandidateDispositionSnapshot receipt =
            ToCandidate(owner);
        CropPhysicalOwnerValidationSnapshot snapshot = new()
        {
            ExpectedOperationId = operation,
            Owner = restored.orders[0].pendingInput
        };
        Validate(new[] { snapshot }, receipt);
        return VerifyCertifiedCapabilityRestore(restored, gateway, catalog)
            && Reject(
                Array.Empty<CropPhysicalOwnerValidationSnapshot>(),
                receipt)
            && !string.IsNullOrWhiteSpace(
                restored.orders[0].certifiedSeedLot.cultivarGenomeId);
    }

    private static bool VerifyCertifiedCapabilityRestore(
        CertifiedSeedWorldSaveData payload,
        FixtureGateway gateway,
        IDungeonItemCatalogProvider itemCatalog)
    {
        CropDefinitionSO crop = ScriptableObject.CreateInstance<CropDefinitionSO>();
        try
        {
            crop.Configure(
                CropId,
                "QA Certified Crop",
                "food:qa-certified-harvest",
                string.Empty,
                24f,
                1f,
                1f,
                0f,
                1,
                true,
                new Vector2(0f, 30f));
            crop.ConfigureEcology(
                SeedItemId,
                null,
                default,
                default);
            ResourceEconomyContentCatalog content = new(
                Array.Empty<ResourceItemDefinitionSO>(),
                Array.Empty<ProductionRecipeSO>(),
                new[] { crop },
                Array.Empty<CraftMaterialDefinitionSO>());
            ProductionOutputHandlerRegistry registry = new(
                new IProductionOutputCapability[]
                {
                    new StandardDefinitionProductionOutputCapability(
                        content,
                        new ProductionPreparedOutputComponentCodec()),
                    new CertifiedSeedOutputCapability(content)
                });
            CertifiedSeedRuntime runtime = new(
                Proxy<IFacilityCapabilityQuery>(),
                Proxy<IBuildingWorldQuery>(),
                content,
                Proxy<IStockQuery>(),
                Proxy<IProductionItemGateway>(),
                Proxy<IItemTransferService>(),
                Proxy<IPhysicalSeedLotGateway>(),
                registry,
                Proxy<IProductionDomainOutputPublicationService>(),
                new DungeonRuntimeAggregateRootStore(),
                new ProductionFacilityMutationEpochRuntime(),
                Proxy<ICertifiedSeedInputOwnerRuntime>());
            CertifiedSeedRestoreCandidate candidate = runtime.BuildRestore(payload);
            runtime.Restore(candidate);
            CertifiedSeedWorldSaveData captured = runtime.Capture();
            if (captured.version != CertifiedSeedWorldSaveData.CurrentVersion
                || captured.lastProcessedOperatingDay != 17
                || captured.orders.Count != 1
                || !string.Equals(
                    captured.orders[0].outputCapability.fingerprint,
                    payload.orders[0].outputCapability.fingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            CertifiedSeedWorldSaveData drift = JsonUtility.FromJson<
                CertifiedSeedWorldSaveData>(JsonUtility.ToJson(payload));
            drift.orders[0].outputCapability.componentCodecVersion++;
            try
            {
                runtime.BuildRestore(drift);
                return false;
            }
            catch (InvalidOperationException)
            {
                return VerifyCertifiedRestoredOutputCompletesWithoutFacility(
                    payload,
                    gateway,
                    itemCatalog,
                    content,
                    registry)
                    && VerifyCertifiedDestroyedFacilityTerminates(
                        payload,
                        itemCatalog,
                        content,
                        registry);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crop);
        }
    }

    private static bool VerifyCertifiedRestoredOutputCompletesWithoutFacility(
        CertifiedSeedWorldSaveData source,
        FixtureGateway gateway,
        IDungeonItemCatalogProvider itemCatalog,
        IResourceEconomyContentCatalog content,
        IProductionOutputCapabilityRegistry registry)
    {
        CertifiedSeedWorldSaveData restored = JsonUtility.FromJson<
            CertifiedSeedWorldSaveData>(JsonUtility.ToJson(source));
        CertifiedSeedOrderSaveData order = restored.orders.Single();
        order.phase = CertifiedSeedOrderPhase
            .OutputRestoredAwaitingInputAcknowledgement;
        order.pendingInput.phase = CropPhysicalCommitPhase.OutcomePublished;
        ItemInstanceComponentSaveData seedState =
            SeedLotItemStateCodec.Encode(order.certifiedSeedLot);
        PhysicalItemMassQuery mass = new(itemCatalog);
        long grams = mass.GetQuantityMass(
                (ItemDefinitionId)SeedItemId,
                PhysicalItemMassSubjectAdapter.Create(
                    mass,
                    (ItemDefinitionId)SeedItemId,
                    string.Empty,
                    new[] { seedState }),
                1)
            .Value;
        order.outputPublication = new ProductionDomainOutputPublicationSaveData
        {
            schemaVersion = ProductionDomainOutputPublicationSaveData
                .CurrentSchemaVersion,
            publicationOperationId =
                CertifiedSeedRuntime.OutputPublicationOperationPrefix
                + order.orderId + ":0000",
            batchCommitId = CertifiedSeedRuntime.CertifiedOutputBatchCommitPrefix
                + order.orderId,
            outcomeFingerprint = CertifiedSeedRuntime
                .CreateOutputOutcomeFingerprint(
                    order,
                    SeedItemId,
                    seedState),
            maximumMassProofDigest = new string('c', 64),
            maximumBatchMassGrams = grams,
            capacitySourceDigest = new string('a', 64),
            requiredMinimumCapacityGrams = grams,
            outputMassGrams = grams,
            admissionTokenId = "facility-buffer-planned-output:qa-certified",
            plannedOutputFingerprint = new string('b', 64),
            destinationId = "production-output:building:greenhouse:qa",
            destinationX = 3,
            destinationY = 4,
            ownerDomain = "production-output-buffer",
            ownerOperationId = "production-output-owner:building:greenhouse:qa",
            ownerFacilityId = order.facilityInstanceId,
            capacityRevision = 1L,
            outputPublished = true,
            admissionCommitted = true,
            outputAcknowledged = true,
            stacks = new List<ProductionDomainPublishedStackSaveData>
            {
                new()
                {
                    outputLineId = CertifiedSeedOutputCapability.OutputLineId,
                    itemId = SeedItemId,
                    stackId = "world-item-stack:qa-certified-output",
                    quantity = 1,
                    massGrams = grams
                }
            }
        };
        CertifiedSeedRuntime runtime = new(
            Proxy<IFacilityCapabilityQuery>(),
            Proxy<IBuildingWorldQuery>(),
            content,
            Proxy<IStockQuery>(),
            Proxy<IProductionItemGateway>(),
            Proxy<IItemTransferService>(),
            gateway,
            registry,
            Proxy<IProductionDomainOutputPublicationService>(),
            new DungeonRuntimeAggregateRootStore(),
            new ProductionFacilityMutationEpochRuntime(),
            Proxy<ICertifiedSeedInputOwnerRuntime>());
        CertifiedSeedRestoreCandidate candidate = runtime.BuildRestore(restored);
        runtime.Restore(candidate);
        return runtime.CompleteDeliveredPlans(
                   restored.lastProcessedOperatingDay + 1) == 1
            && runtime.Capture().orders.Count == 0
            && !gateway.HasPending(order.pendingInput.operationId);
    }

    private static bool VerifyCertifiedDestroyedFacilityTerminates(
        CertifiedSeedWorldSaveData source,
        IDungeonItemCatalogProvider itemCatalog,
        IResourceEconomyContentCatalog content,
        IProductionOutputCapabilityRegistry registry)
    {
        CertifiedSeedWorldSaveData payload = JsonUtility.FromJson<
            CertifiedSeedWorldSaveData>(JsonUtility.ToJson(source));
        CertifiedSeedOrderSaveData order = payload.orders.Single();
        FixtureGateway gateway = new(itemCatalog);
        gateway.AddSeed(order.destinationId);
        gateway.Add(CertificationKitItemId, 1, order.destinationId);
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [CertificationKitItemId] = 1
        };
        CropPhysicalCommitSaveData input = new();
        if (!CropPhysicalTransactionOutbox.TryCommitOrResume(
                input,
                CropPhysicalTransactionOutbox.FormatCertifiedOperationId(
                    order.orderId),
                CropPhysicalTransactionOutbox.CertifiedReasonCode,
                order.orderSequence,
                order.destinationId,
                requirements,
                SeedItemId,
                CropId,
                gateway,
                out SeedLotState seed,
                out _))
        {
            return false;
        }
        order.phase = CertifiedSeedOrderPhase.InputCommitted;
        order.pendingInput = input;
        order.certifiedSeedLot = seed;
        order.outputPublication = new ProductionDomainOutputPublicationSaveData();
        CertifiedSeedRuntime runtime = new(
            Proxy<IFacilityCapabilityQuery>(),
            Proxy<IBuildingWorldQuery>(),
            content,
            Proxy<IStockQuery>(),
            Proxy<IProductionItemGateway>(),
            Proxy<IItemTransferService>(),
            gateway,
            registry,
            Proxy<IProductionDomainOutputPublicationService>(),
            new DungeonRuntimeAggregateRootStore(),
            new ProductionFacilityMutationEpochRuntime(),
            Proxy<ICertifiedSeedInputOwnerRuntime>());
        runtime.Restore(runtime.BuildRestore(payload));
        return runtime.CompleteDeliveredPlans(
                   payload.lastProcessedOperatingDay + 1) == 1
            && runtime.Capture().orders.Count == 0
            && !gateway.HasPending(input.operationId);
    }

    private static T Proxy<T>() where T : class =>
        BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<T>();

    private static bool VerifySeedDeliveryReachabilityContracts()
    {
        const string inaccessibleStackId = "world-item-stack:qa-seed-inaccessible";
        const string reachableStackId = "world-item-stack:qa-seed-reachable";
        Vector2Int destinationPosition = new(12, 4);
        WorldItemStackSnapshot inaccessible = CreateSeedSelectionSnapshot(
            inaccessibleStackId,
            pathogenLoad: 1f,
            generation: 4,
            position: new Vector2Int(2, 2));
        WorldItemStackSnapshot reachable = CreateSeedSelectionSnapshot(
            reachableStackId,
            pathogenLoad: 20f,
            generation: 1,
            position: new Vector2Int(3, 2));
        IStockQuery stock = SeedSelectionStockProxy.Create(
            inaccessible,
            reachable);
        IItemTransferService transfers = SeedSelectionTransferProxy.Create(
            out SeedSelectionTransferProxy transferProbe);
        SeedDeliveryReachabilityProbe reachability = new(new Dictionary<string,
            WorldItemDeliveryReachabilityStatus>(StringComparer.Ordinal)
        {
            [inaccessibleStackId] = WorldItemDeliveryReachabilityStatus.Unreachable,
            [reachableStackId] = WorldItemDeliveryReachabilityStatus.Reachable
        });
        IFacilityBufferDestinationReleaseService release = SeedDestinationReleaseProxy
            .Create(releasedQuantity: 1, out SeedDestinationReleaseProxy releaseProbe);
        IWorldItemStackRuntime noIntents = SeedWorldRuntimeProxy.Create(
            Array.Empty<HaulDeliveryIntentSaveData>());
        PhysicalSeedLotGateway gateway = new(
            stock,
            transfers,
            noIntents,
            reachability,
            release);
        if (!gateway.RequestBestSeedLot(
                SeedItemId,
                CropId,
                destinationPosition,
                DestinationId,
                out int requested,
                out DomainFailure requestFailure)
            || requestFailure.IsFailure
            || requested != 1
            || transferProbe.CallCount != 1
            || !string.Equals(
                transferProbe.RequestedStackId,
                reachableStackId,
                StringComparison.Ordinal)
            || !reachability.AssessedStackIds.SequenceEqual(
                new[] { inaccessibleStackId, reachableStackId },
                StringComparer.Ordinal))
        {
            return false;
        }

        IItemTransferService failingTransfers = SeedSelectionTransferProxy.Create(
            out SeedSelectionTransferProxy failingTransferProbe,
            failAll: true);
        DomainFailure expectedTransferFailure = new(
            FailureCode.ItemTransferRequestFailed,
            "qa-transfer-race");
        failingTransferProbe.Failure = expectedTransferFailure;
        PhysicalSeedLotGateway transferFailureGateway = new(
            SeedSelectionStockProxy.Create(reachable),
            failingTransfers,
            noIntents,
            reachability,
            release);
        if (transferFailureGateway.RequestBestSeedLot(
                SeedItemId,
                CropId,
                destinationPosition,
                DestinationId,
                out int failedRequested,
                out DomainFailure preservedFailure)
            || failedRequested != 0
            || !preservedFailure.Equals(expectedTransferFailure)
            || failingTransferProbe.CallCount != 1)
        {
            return false;
        }

        WorldItemStackSnapshot pending = CreateSeedSelectionSnapshot(
            inaccessibleStackId,
            pathogenLoad: 1f,
            generation: 4,
            position: new Vector2Int(2, 2));
        pending.DestinationId = DestinationId;
        SeedDeliveryReachabilityProbe pendingUnreachable = new(new Dictionary<string,
            WorldItemDeliveryReachabilityStatus>(StringComparer.Ordinal)
        {
            [inaccessibleStackId] = WorldItemDeliveryReachabilityStatus.Unreachable
        });
        PhysicalSeedLotGateway recoveryGateway = new(
            SeedSelectionStockProxy.Create(pending),
            transfers,
            noIntents,
            pendingUnreachable,
            release);
        if (!recoveryGateway.TryReleaseUnreachableSeedDelivery(
                SeedItemId,
                CropId,
                destinationPosition,
                DestinationId,
                out bool released,
                out DomainFailure releaseFailure)
            || releaseFailure.IsFailure
            || !released
            || releaseProbe.CallCount != 1
            || !string.Equals(
                releaseProbe.DestinationId,
                DestinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                releaseProbe.ReasonCode,
                "seed-lot-delivery-unreachable-retry",
                StringComparison.Ordinal))
        {
            return false;
        }

        IFacilityBufferDestinationReleaseService protectedRelease =
            SeedDestinationReleaseProxy.Create(
                releasedQuantity: 1,
                out SeedDestinationReleaseProxy protectedReleaseProbe);
        HaulDeliveryIntentSaveData committedIntent = new()
        {
            destinationId = DestinationId,
            commitments = new List<HaulDeliveryItemCommitmentSaveData>
            {
                new() { quantity = 1 }
            }
        };
        PhysicalSeedLotGateway protectedGateway = new(
            SeedSelectionStockProxy.Create(pending),
            transfers,
            SeedWorldRuntimeProxy.Create(new[] { committedIntent }),
            pendingUnreachable,
            protectedRelease);
        return protectedGateway.TryReleaseUnreachableSeedDelivery(
                   SeedItemId,
                   CropId,
                   destinationPosition,
                   DestinationId,
                   out bool protectedReleased,
                   out DomainFailure protectedFailure)
               && !protectedFailure.IsFailure
               && !protectedReleased
               && protectedReleaseProbe.CallCount == 0;
    }

    private static bool VerifyCertifiedSeedPlannedDeliveryRecovery()
    {
        return VerifyCertifiedSeedImmediateRecoveryAndIdempotence()
            && VerifyCertifiedSeedDeferredAndCommittedProtection()
            && VerifyCertifiedSeedReleaseFailurePreservesOrder();
    }

    private static bool VerifyCertifiedSeedImmediateRecoveryAndIdempotence()
    {
        using CertifiedSeedRecoveryHarness fixture =
            new(WorldItemDeliveryReachabilityStatus.Reachable);
        if (!fixture.TryPlan(out DomainFailure initialFailure)
            || initialFailure.IsFailure
            || !fixture.HasSinglePlannedOrder(out CertifiedSeedOrderSaveData order)
            || fixture.Ledger.SeedRequestCount != 1
            || fixture.Ledger.KitRequestCount != 1
            || fixture.Ledger.PrioritizeCount != 1
            || fixture.Ledger.CountPending(SeedItemId, order.destinationId) != 1
            || fixture.Ledger.CountPending(
                CertificationKitItemId,
                order.destinationId) != 1)
        {
            return false;
        }

        string orderId = order.orderId;
        int orderSequence = order.orderSequence;
        string destinationId = order.destinationId;
        fixture.Ledger.SetReachability(
            CertifiedSeedRecoveryHarness.HighSeedStackId,
            WorldItemDeliveryReachabilityStatus.Unreachable);
        fixture.Ledger.AddSeed(
            CertifiedSeedRecoveryHarness.LowSeedStackId,
            pathogenLoad: 20f,
            generation: 1,
            WorldItemDeliveryReachabilityStatus.Reachable);

        if (!fixture.TryPlan(out DomainFailure recoveryFailure)
            || recoveryFailure.IsFailure
            || fixture.Ledger.ReleaseCallCount != 1
            || fixture.Ledger.SeedRequestCount != 2
            || fixture.Ledger.KitRequestCount != 2
            || fixture.Ledger.PrioritizeCount != 2
            || !string.Equals(
                fixture.Ledger.LastRequestedSeedStackId,
                CertifiedSeedRecoveryHarness.LowSeedStackId,
                StringComparison.Ordinal)
            || fixture.Ledger.CountPending(SeedItemId, destinationId) != 1
            || fixture.Ledger.CountPending(
                CertificationKitItemId,
                destinationId) != 1
            || !fixture.HasSinglePlannedOrder(out CertifiedSeedOrderSaveData recovered)
            || !string.Equals(recovered.orderId, orderId, StringComparison.Ordinal)
            || recovered.orderSequence != orderSequence
            || !string.Equals(
                recovered.destinationId,
                destinationId,
                StringComparison.Ordinal))
        {
            return false;
        }

        int releases = fixture.Ledger.ReleaseCallCount;
        int seedRequests = fixture.Ledger.SeedRequestCount;
        int kitRequests = fixture.Ledger.KitRequestCount;
        int priorities = fixture.Ledger.PrioritizeCount;
        if (!fixture.TryPlan(out DomainFailure repeatedFailureA)
            || repeatedFailureA.IsFailure
            || !fixture.TryPlan(out DomainFailure repeatedFailureB)
            || repeatedFailureB.IsFailure
            || fixture.Runtime.CompleteDeliveredPlans(1) != 0
            || fixture.Runtime.CompleteDeliveredPlans(1) != 0
            || fixture.Ledger.ReleaseCallCount != releases
            || fixture.Ledger.SeedRequestCount != seedRequests
            || fixture.Ledger.KitRequestCount != kitRequests
            || fixture.Ledger.PrioritizeCount != priorities
            || fixture.Ledger.CountPending(SeedItemId, destinationId) != 1
            || fixture.Ledger.CountPending(
                CertificationKitItemId,
                destinationId) != 1)
        {
            return false;
        }
        return true;
    }

    private static bool VerifyCertifiedSeedDeferredAndCommittedProtection()
    {
        using CertifiedSeedRecoveryHarness deferred =
            new(WorldItemDeliveryReachabilityStatus.Reachable);
        if (!deferred.TryPlan(out _)
            || !deferred.HasSinglePlannedOrder(out CertifiedSeedOrderSaveData order))
        {
            return false;
        }
        deferred.Ledger.SetReachability(
            CertifiedSeedRecoveryHarness.HighSeedStackId,
            WorldItemDeliveryReachabilityStatus.Deferred);
        int seedRequests = deferred.Ledger.SeedRequestCount;
        int kitRequests = deferred.Ledger.KitRequestCount;
        int priorities = deferred.Ledger.PrioritizeCount;
        if (!deferred.TryPlan(out DomainFailure deferredFailure)
            || deferredFailure.IsFailure
            || deferred.Ledger.ReleaseCallCount != 0
            || deferred.Ledger.SeedRequestCount != seedRequests
            || deferred.Ledger.KitRequestCount != kitRequests
            || deferred.Ledger.PrioritizeCount != priorities
            || deferred.Ledger.CountPending(SeedItemId, order.destinationId) != 1)
        {
            return false;
        }

        using CertifiedSeedRecoveryHarness committed =
            new(WorldItemDeliveryReachabilityStatus.Reachable);
        if (!committed.TryPlan(out _)
            || !committed.HasSinglePlannedOrder(out CertifiedSeedOrderSaveData committedOrder))
        {
            return false;
        }
        committed.Ledger.SetReachability(
            CertifiedSeedRecoveryHarness.HighSeedStackId,
            WorldItemDeliveryReachabilityStatus.Unreachable);
        committed.Ledger.Intents.Add(new HaulDeliveryIntentSaveData
        {
            destinationId = committedOrder.destinationId,
            commitments = new List<HaulDeliveryItemCommitmentSaveData>
            {
                new() { quantity = 1 }
            }
        });
        if (!committed.TryPlan(out DomainFailure committedFailure)
            || committedFailure.IsFailure
            || committed.Ledger.ReleaseCallCount != 0
            || committed.Ledger.SeedRequestCount != 1
            || committed.Ledger.KitRequestCount != 1
            || committed.Ledger.CountPending(
                SeedItemId,
                committedOrder.destinationId) != 1)
        {
            return false;
        }
        return true;
    }

    private static bool VerifyCertifiedSeedReleaseFailurePreservesOrder()
    {
        using CertifiedSeedRecoveryHarness fixture =
            new(WorldItemDeliveryReachabilityStatus.Reachable);
        if (!fixture.TryPlan(out _)
            || !fixture.HasSinglePlannedOrder(out CertifiedSeedOrderSaveData order))
        {
            return false;
        }
        fixture.Ledger.SetReachability(
            CertifiedSeedRecoveryHarness.HighSeedStackId,
            WorldItemDeliveryReachabilityStatus.Unreachable);
        fixture.Ledger.AddSeed(
            CertifiedSeedRecoveryHarness.LowSeedStackId,
            pathogenLoad: 20f,
            generation: 1,
            WorldItemDeliveryReachabilityStatus.Reachable);
        fixture.Ledger.FailRelease = true;
        if (fixture.TryPlan(out DomainFailure failure)
            || failure.Code != FailureCode.ItemTransferRequestFailed
            || failure.Parameters.Length < 1
            || !string.Equals(
                failure.Parameters[0],
                "seed-delivery-recovery-release-failed",
                StringComparison.Ordinal)
            || fixture.Ledger.ReleaseCallCount != 1
            || fixture.Ledger.SeedRequestCount != 1
            || fixture.Ledger.KitRequestCount != 1
            || fixture.Ledger.PrioritizeCount != 1
            || fixture.Ledger.CountPending(SeedItemId, order.destinationId) != 1
            || !fixture.HasSinglePlannedOrder(out CertifiedSeedOrderSaveData preserved)
            || !string.Equals(
                preserved.orderId,
                order.orderId,
                StringComparison.Ordinal))
        {
            return false;
        }

        fixture.Ledger.FailRelease = false;
        return fixture.TryPlan(out DomainFailure retryFailure)
            && !retryFailure.IsFailure
            && fixture.Ledger.ReleaseCallCount == 2
            && fixture.Ledger.SeedRequestCount == 2
            && fixture.Ledger.KitRequestCount == 2
            && fixture.Ledger.CountPending(SeedItemId, order.destinationId) == 1
            && fixture.Ledger.CountPending(
                CertificationKitItemId,
                order.destinationId) == 1;
    }

    private sealed class CertifiedSeedRecoveryHarness : IDisposable
    {
        internal const string HighSeedStackId =
            "world-item-stack:qa-certified-recovery-high";
        internal const string LowSeedStackId =
            "world-item-stack:qa-certified-recovery-low";
        private const string FacilityId =
            "building:qa:certified-seed-recovery";
        private const string ActionId =
            "action:qa-certified-seed-recovery";

        private readonly GameObject facilityObject;
        private readonly BuildingSO buildingDefinition;
        private readonly CropDefinitionSO cropDefinition;

        internal CertifiedSeedRecoveryHarness(
            WorldItemDeliveryReachabilityStatus initialStatus)
        {
            Ledger = new CertifiedSeedRecoveryLedger();
            Ledger.AddSeed(
                HighSeedStackId,
                pathogenLoad: 1f,
                generation: 4,
                initialStatus);
            Ledger.AddKit("world-item-stack:qa-certified-recovery-kit");

            buildingDefinition = CreateBuildingDefinition();
            cropDefinition = ScriptableObject.CreateInstance<CropDefinitionSO>();
            cropDefinition.Configure(
                CropId,
                "QA certified recovery crop",
                "food:qa-certified-recovery",
                string.Empty,
                24f,
                1f,
                1f,
                0f,
                1,
                true,
                new Vector2(0f, 30f));
            cropDefinition.ConfigureEcology(
                SeedItemId,
                null,
                default,
                default);
            ResourceEconomyContentCatalog content = new(
                Array.Empty<ResourceItemDefinitionSO>(),
                Array.Empty<ProductionRecipeSO>(),
                new[] { cropDefinition },
                Array.Empty<CraftMaterialDefinitionSO>());

            facilityObject = new GameObject("QA Certified Seed Recovery Facility");
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.RestorePersistentIdentity((BuildingInstanceId)FacilityId);
            facility.Initialization(buildingDefinition, new Vector2Int(12, 4));

            IStockQuery stock = CertifiedSeedRecoveryStockProxy.Create(Ledger);
            IItemTransferService transfers =
                CertifiedSeedRecoveryTransferProxy.Create(Ledger);
            IWorldItemDeliveryReachabilityQuery reachability =
                CertifiedSeedRecoveryReachabilityProxy.Create(Ledger);
            IWorldItemStackRuntime world =
                CertifiedSeedRecoveryWorldProxy.Create(Ledger);
            IFacilityBufferDestinationReleaseService release =
                CertifiedSeedRecoveryReleaseProxy.Create(Ledger);
            PhysicalSeedLotGateway seedLots = new(
                stock,
                transfers,
                world,
                reachability,
                release);

            Runtime = new CertifiedSeedRuntime(
                CertifiedSeedRecoveryFacilityProxy.Create(facility),
                CertifiedSeedRecoveryBuildingWorldProxy.Create(facility),
                content,
                stock,
                CertifiedSeedRecoveryProductionItemsProxy.Create(Ledger),
                transfers,
                seedLots,
                Proxy<IProductionOutputCapabilityRegistry>(),
                Proxy<IProductionDomainOutputPublicationService>(),
                new DungeonRuntimeAggregateRootStore(),
                new ProductionFacilityMutationEpochRuntime(),
                CertifiedSeedRecoveryInputOwnerProxy.Create());
        }

        internal CertifiedSeedRuntime Runtime { get; }
        internal CertifiedSeedRecoveryLedger Ledger { get; }

        internal bool TryPlan(out DomainFailure failure) => Runtime.TryPlan(
            ActionId,
            CropId,
            FacilityId,
            out failure);

        internal bool HasSinglePlannedOrder(out CertifiedSeedOrderSaveData order)
        {
            CertifiedSeedWorldSaveData snapshot = Runtime.Capture();
            order = snapshot.orders.Count == 1 ? snapshot.orders[0] : null;
            return order != null
                && order.phase == CertifiedSeedOrderPhase.Planned;
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(buildingDefinition);
            UnityEngine.Object.DestroyImmediate(cropDefinition);
        }

        private static BuildingSO CreateBuildingDefinition()
        {
            BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
            definition.id = 99_891;
            definition.objectName = "QA certified recovery facility";
            definition.ConfigureAuthoredContentIdentity(
                "building-definition:qa:certified-seed-recovery",
                1,
                "Certified-seed planned-delivery recovery fixture.");
            BuildingAbilityCollection abilities = new();
            abilities.Add(new BuildingProductionWorkstationAbility
            {
                workstationTag = CertifiedSeedFacilityEligibility.WorkstationTag,
                lanePolicy = ProductionWorkstationLanePolicy
                    .ManualWithDetachedBatchProcessors,
                manualWorkLaneCount = 1,
                automaticWorkLaneCount = 0
            });
            abilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 2,
                physicalOutputBufferCycleCapacity = 4,
                allowOverflowDump = false
            });
            definition.ReplaceAbilities(abilities);
            return definition;
        }
    }

    public sealed class CertifiedSeedRecoveryLedger
    {
        private readonly List<WorldItemStackSnapshot> stacks = new();
        private readonly Dictionary<string, WorldItemDeliveryReachabilityStatus>
            reachability = new(StringComparer.Ordinal);

        internal List<HaulDeliveryIntentSaveData> Intents { get; } = new();
        internal bool FailRelease { get; set; }
        internal int SeedRequestCount { get; set; }
        internal int KitRequestCount { get; set; }
        internal int PrioritizeCount { get; set; }
        internal int ReleaseCallCount { get; set; }
        internal string LastRequestedSeedStackId { get; set; } = string.Empty;
        internal IReadOnlyList<WorldItemStackSnapshot> Stacks => stacks;

        internal void AddSeed(
            string stackId,
            float pathogenLoad,
            int generation,
            WorldItemDeliveryReachabilityStatus status)
        {
            stacks.Add(CreateSeedSelectionSnapshot(
                stackId,
                pathogenLoad,
                generation,
                new Vector2Int(2 + stacks.Count, 2)));
            reachability[stackId] = status;
        }

        internal void AddKit(string stackId)
        {
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = stackId,
                ItemId = CertificationKitItemId,
                Quantity = 1,
                State = WorldItemStackState.Loose,
                Position = new Vector2Int(5, 2),
                DestinationId = string.Empty,
                Components = Array.Empty<ItemInstanceComponentSaveData>()
            });
        }

        internal void SetReachability(
            string stackId,
            WorldItemDeliveryReachabilityStatus status) =>
            reachability[stackId] = status;

        internal WorldItemDeliveryReachabilityStatus Assess(string stackId) =>
            reachability.TryGetValue(stackId, out var status)
                ? status
                : WorldItemDeliveryReachabilityStatus.Invalid;

        internal int CountPending(string itemId, string destinationId) => stacks
            .Where(value => value.Quantity > 0
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);

        internal bool TryRetargetStack(
            string stackId,
            int quantity,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out DomainFailure failure)
        {
            WorldItemStackSnapshot stack = stacks.SingleOrDefault(value =>
                string.Equals(value.StackId, stackId, StringComparison.Ordinal));
            if (stack == null
                || quantity != 1
                || stack.AvailableQuantity < quantity
                || !string.IsNullOrEmpty(stack.DestinationId))
            {
                requested = 0;
                failure = new DomainFailure(
                    FailureCode.ItemTransferStackUnavailable,
                    stackId ?? string.Empty);
                return false;
            }
            stack.DestinationId = destinationId;
            stack.DestinationPosition = destinationPosition;
            stack.HasDestinationPosition = true;
            requested = quantity;
            failure = DomainFailure.None;
            SeedRequestCount++;
            LastRequestedSeedStackId = stackId;
            return true;
        }

        internal bool TryRetargetItem(
            string itemId,
            int quantity,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out DomainFailure failure)
        {
            WorldItemStackSnapshot stack = stacks.FirstOrDefault(value =>
                value.Quantity >= quantity
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal)
                && string.IsNullOrEmpty(value.DestinationId));
            if (stack == null || quantity <= 0)
            {
                requested = 0;
                failure = new DomainFailure(
                    FailureCode.ItemTransferStackUnavailable,
                    itemId ?? string.Empty);
                return false;
            }
            stack.DestinationId = destinationId;
            stack.DestinationPosition = destinationPosition;
            stack.HasDestinationPosition = true;
            requested = quantity;
            failure = DomainFailure.None;
            KitRequestCount++;
            return true;
        }

        internal bool TryRelease(
            string destinationId,
            out int released,
            out string failureReason)
        {
            ReleaseCallCount++;
            released = 0;
            if (FailRelease)
            {
                failureReason = "qa-certified-seed-release-failure";
                return false;
            }
            foreach (WorldItemStackSnapshot stack in stacks.Where(value =>
                         string.Equals(
                             value.DestinationId,
                             destinationId,
                             StringComparison.Ordinal)))
            {
                released += stack.Quantity;
                stack.DestinationId = string.Empty;
                stack.DestinationPosition = default;
                stack.HasDestinationPosition = false;
            }
            failureReason = string.Empty;
            return true;
        }
    }

    public class CertifiedSeedRecoveryStockProxy : DispatchProxy
    {
        private CertifiedSeedRecoveryLedger ledger;

        internal static IStockQuery Create(CertifiedSeedRecoveryLedger value)
        {
            IStockQuery contract = DispatchProxy.Create<
                IStockQuery,
                CertifiedSeedRecoveryStockProxy>();
            ((CertifiedSeedRecoveryStockProxy)(object)contract).ledger = value;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == nameof(IStockQuery.GetAllStacks))
                return ledger.Stacks;
            throw new InvalidOperationException(
                "Unexpected recovery stock call: " + targetMethod.Name);
        }
    }

    public class CertifiedSeedRecoveryProductionItemsProxy : DispatchProxy
    {
        private CertifiedSeedRecoveryLedger ledger;

        internal static IProductionItemGateway Create(
            CertifiedSeedRecoveryLedger value)
        {
            IProductionItemGateway contract = DispatchProxy.Create<
                IProductionItemGateway,
                CertifiedSeedRecoveryProductionItemsProxy>();
            ((CertifiedSeedRecoveryProductionItemsProxy)(object)contract).ledger =
                value;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == nameof(IProductionItemGateway.CountPending))
            {
                return ledger.CountPending(
                    (string)arguments[0],
                    (string)arguments[1]);
            }
            if (targetMethod.Name == nameof(IProductionItemGateway.PrioritizeDestination))
            {
                ledger.PrioritizeCount++;
                return null;
            }
            throw new InvalidOperationException(
                "Unexpected recovery production-item call: " + targetMethod.Name);
        }
    }

    public class CertifiedSeedRecoveryTransferProxy : DispatchProxy
    {
        private CertifiedSeedRecoveryLedger ledger;

        internal static IItemTransferService Create(
            CertifiedSeedRecoveryLedger value)
        {
            IItemTransferService contract = DispatchProxy.Create<
                IItemTransferService,
                CertifiedSeedRecoveryTransferProxy>();
            ((CertifiedSeedRecoveryTransferProxy)(object)contract).ledger = value;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == nameof(IItemTransferService.TryRequestStackDelivery))
            {
                bool success = ledger.TryRetargetStack(
                    ((ItemStackId)arguments[0]).Value,
                    (int)arguments[1],
                    (Vector2Int)arguments[2],
                    (string)arguments[3],
                    out int requested,
                    out DomainFailure failure);
                arguments[4] = requested;
                arguments[5] = failure;
                return success;
            }
            if (targetMethod.Name == nameof(IItemTransferService.TryRequestItemDelivery))
            {
                bool success = ledger.TryRetargetItem(
                    (string)arguments[0],
                    (int)arguments[1],
                    (Vector2Int)arguments[2],
                    (string)arguments[3],
                    out int requested,
                    out DomainFailure failure);
                arguments[4] = requested;
                arguments[5] = failure;
                return success;
            }
            if (targetMethod.Name == nameof(IItemTransferService.PrioritizeDestination))
            {
                ledger.PrioritizeCount++;
                return null;
            }
            throw new InvalidOperationException(
                "Unexpected recovery transfer call: " + targetMethod.Name);
        }
    }

    public class CertifiedSeedRecoveryReachabilityProxy : DispatchProxy
    {
        private CertifiedSeedRecoveryLedger ledger;

        internal static IWorldItemDeliveryReachabilityQuery Create(
            CertifiedSeedRecoveryLedger value)
        {
            IWorldItemDeliveryReachabilityQuery contract = DispatchProxy.Create<
                IWorldItemDeliveryReachabilityQuery,
                CertifiedSeedRecoveryReachabilityProxy>();
            ((CertifiedSeedRecoveryReachabilityProxy)(object)contract).ledger =
                value;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name
                == nameof(IWorldItemDeliveryReachabilityQuery.AssessExactStackDelivery))
            {
                WorldItemDeliveryReachabilityStatus status = ledger.Assess(
                    ((ItemStackId)arguments[0]).Value);
                arguments[4] = status == WorldItemDeliveryReachabilityStatus.Reachable
                    ? string.Empty
                    : "qa-" + status.ToString().ToLowerInvariant();
                return status;
            }
            throw new InvalidOperationException(
                "Unexpected recovery reachability call: " + targetMethod.Name);
        }
    }

    public class CertifiedSeedRecoveryWorldProxy : DispatchProxy
    {
        private CertifiedSeedRecoveryLedger ledger;

        internal static IWorldItemStackRuntime Create(
            CertifiedSeedRecoveryLedger value)
        {
            IWorldItemStackRuntime contract = DispatchProxy.Create<
                IWorldItemStackRuntime,
                CertifiedSeedRecoveryWorldProxy>();
            ((CertifiedSeedRecoveryWorldProxy)(object)contract).ledger = value;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name
                == nameof(IWorldItemStackRuntime.CaptureHaulDeliveryIntentsByDestination))
            {
                string destinationId = (string)arguments[0];
                return ledger.Intents.Where(value => string.Equals(
                        value.destinationId,
                        destinationId,
                        StringComparison.Ordinal))
                    .ToArray();
            }
            throw new InvalidOperationException(
                "Unexpected recovery world call: " + targetMethod.Name);
        }
    }

    public class CertifiedSeedRecoveryReleaseProxy : DispatchProxy
    {
        private CertifiedSeedRecoveryLedger ledger;

        internal static IFacilityBufferDestinationReleaseService Create(
            CertifiedSeedRecoveryLedger value)
        {
            IFacilityBufferDestinationReleaseService contract = DispatchProxy.Create<
                IFacilityBufferDestinationReleaseService,
                CertifiedSeedRecoveryReleaseProxy>();
            ((CertifiedSeedRecoveryReleaseProxy)(object)contract).ledger = value;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name
                == nameof(IFacilityBufferDestinationReleaseService.TryReleaseAtOwnerPosition))
            {
                bool success = ledger.TryRelease(
                    (string)arguments[0],
                    out int released,
                    out string failureReason);
                arguments[3] = released;
                arguments[4] = failureReason;
                return success;
            }
            throw new InvalidOperationException(
                "Unexpected recovery release call: " + targetMethod.Name);
        }
    }

    public class CertifiedSeedRecoveryFacilityProxy : DispatchProxy
    {
        private BuildableObject facility;

        internal static IFacilityCapabilityQuery Create(BuildableObject value)
        {
            IFacilityCapabilityQuery contract = DispatchProxy.Create<
                IFacilityCapabilityQuery,
                CertifiedSeedRecoveryFacilityProxy>();
            ((CertifiedSeedRecoveryFacilityProxy)(object)contract).facility = value;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == nameof(IFacilityCapabilityQuery.FindOperational))
                return new[] { facility };
            throw new InvalidOperationException(
                "Unexpected recovery facility call: " + targetMethod.Name);
        }
    }

    public class CertifiedSeedRecoveryBuildingWorldProxy : DispatchProxy
    {
        private BuildableObject facility;

        internal static IBuildingWorldQuery Create(BuildableObject value)
        {
            IBuildingWorldQuery contract = DispatchProxy.Create<
                IBuildingWorldQuery,
                CertifiedSeedRecoveryBuildingWorldProxy>();
            ((CertifiedSeedRecoveryBuildingWorldProxy)(object)contract).facility =
                value;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == "get_Buildings")
                return new[] { facility };
            if (targetMethod.Name == "get_BuildingVersion")
                return 1;
            throw new InvalidOperationException(
                "Unexpected recovery building-world call: " + targetMethod.Name);
        }
    }

    public class CertifiedSeedRecoveryInputOwnerProxy : DispatchProxy
    {
        internal static ICertifiedSeedInputOwnerRuntime Create() =>
            DispatchProxy.Create<
                ICertifiedSeedInputOwnerRuntime,
                CertifiedSeedRecoveryInputOwnerProxy>();

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == nameof(ICertifiedSeedInputOwnerRuntime.TryEnsure)
                || targetMethod.Name == nameof(ICertifiedSeedInputOwnerRuntime.TryRetire)
                || targetMethod.Name
                    == nameof(ICertifiedSeedInputOwnerRuntime.TryReplaceForRestore))
            {
                arguments[arguments.Length - 1] = string.Empty;
                return true;
            }
            throw new InvalidOperationException(
                "Unexpected recovery input-owner call: " + targetMethod.Name);
        }
    }

    private static WorldItemStackSnapshot CreateSeedSelectionSnapshot(
        string stackId,
        float pathogenLoad,
        int generation,
        Vector2Int position) => new()
    {
        StackId = stackId,
        ItemId = SeedItemId,
        Quantity = 1,
        State = WorldItemStackState.Loose,
        Position = position,
        DestinationId = string.Empty,
        Components = new[]
        {
            SeedLotItemStateCodec.Encode(new SeedLotState
            {
                cropId = CropId,
                cultivarGenomeId = $"genome:qa:{stackId}",
                generation = generation,
                pathogenLoad = pathogenLoad
            })
        }
    };

    public class SeedSelectionStockProxy : DispatchProxy
    {
        private IReadOnlyList<WorldItemStackSnapshot> stacks =
            Array.Empty<WorldItemStackSnapshot>();

        internal static IStockQuery Create(
            params WorldItemStackSnapshot[] values)
        {
            IStockQuery contract = DispatchProxy.Create<
                IStockQuery,
                SeedSelectionStockProxy>();
            ((SeedSelectionStockProxy)(object)contract).stacks = values
                ?? Array.Empty<WorldItemStackSnapshot>();
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (string.Equals(
                    targetMethod.Name,
                    nameof(IStockQuery.GetAllStacks),
                    StringComparison.Ordinal))
            {
                return stacks;
            }
            throw new InvalidOperationException(
                $"Unexpected stock call: {targetMethod.Name}");
        }
    }

    public class SeedSelectionTransferProxy : DispatchProxy
    {
        private bool failAll;
        internal int CallCount { get; private set; }
        internal string RequestedStackId { get; private set; } = string.Empty;
        internal DomainFailure Failure { get; set; } = new(
            FailureCode.ItemTransferRequestFailed,
            "qa-transfer-failure");

        internal static IItemTransferService Create(
            out SeedSelectionTransferProxy probe,
            bool failAll = false)
        {
            IItemTransferService contract = DispatchProxy.Create<
                IItemTransferService,
                SeedSelectionTransferProxy>();
            probe = (SeedSelectionTransferProxy)(object)contract;
            probe.failAll = failAll;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (!string.Equals(
                    targetMethod.Name,
                    nameof(IItemTransferService.TryRequestStackDelivery),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected transfer call: {targetMethod.Name}");
            }
            CallCount++;
            RequestedStackId = ((ItemStackId)arguments[0]).Value;
            int quantity = (int)arguments[1];
            arguments[4] = failAll ? 0 : quantity;
            arguments[5] = failAll ? Failure : DomainFailure.None;
            return !failAll;
        }
    }

    private sealed class SeedDeliveryReachabilityProbe :
        IWorldItemDeliveryReachabilityQuery
    {
        private readonly IReadOnlyDictionary<string,
            WorldItemDeliveryReachabilityStatus> statuses;

        internal SeedDeliveryReachabilityProbe(
            IReadOnlyDictionary<string, WorldItemDeliveryReachabilityStatus> statuses) =>
            this.statuses = statuses;

        internal List<string> AssessedStackIds { get; } = new();

        public WorldItemDeliveryReachabilityStatus AssessExactStackDelivery(
            ItemStackId stackId,
            int quantity,
            Vector2Int destinationPosition,
            string destinationId,
            out string failureReason)
        {
            AssessedStackIds.Add(stackId.Value);
            failureReason = string.Empty;
            return statuses.TryGetValue(stackId.Value, out var status)
                ? status
                : WorldItemDeliveryReachabilityStatus.Invalid;
        }
    }

    public class SeedDestinationReleaseProxy : DispatchProxy
    {
        private int releasedQuantity;
        internal int CallCount { get; private set; }
        internal string DestinationId { get; private set; } = string.Empty;
        internal string ReasonCode { get; private set; } = string.Empty;

        internal static IFacilityBufferDestinationReleaseService Create(
            int releasedQuantity,
            out SeedDestinationReleaseProxy probe)
        {
            IFacilityBufferDestinationReleaseService contract = DispatchProxy.Create<
                IFacilityBufferDestinationReleaseService,
                SeedDestinationReleaseProxy>();
            probe = (SeedDestinationReleaseProxy)(object)contract;
            probe.releasedQuantity = releasedQuantity;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (!string.Equals(
                    targetMethod.Name,
                    nameof(IFacilityBufferDestinationReleaseService
                        .TryReleaseAtOwnerPosition),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected release call: {targetMethod.Name}");
            }
            CallCount++;
            DestinationId = (string)arguments[0];
            ReasonCode = (string)arguments[2];
            arguments[3] = releasedQuantity;
            arguments[4] = string.Empty;
            return true;
        }
    }

    public class SeedWorldRuntimeProxy : DispatchProxy
    {
        private IReadOnlyList<HaulDeliveryIntentSaveData> intents =
            Array.Empty<HaulDeliveryIntentSaveData>();

        internal static IWorldItemStackRuntime Create(
            IReadOnlyList<HaulDeliveryIntentSaveData> intents)
        {
            IWorldItemStackRuntime contract = DispatchProxy.Create<
                IWorldItemStackRuntime,
                SeedWorldRuntimeProxy>();
            ((SeedWorldRuntimeProxy)(object)contract).intents = intents
                ?? Array.Empty<HaulDeliveryIntentSaveData>();
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (string.Equals(
                    targetMethod.Name,
                    nameof(IWorldItemStackRuntime
                        .CaptureHaulDeliveryIntentsByDestination),
                    StringComparison.Ordinal))
            {
                return intents;
            }
            throw new InvalidOperationException(
                $"Unexpected world runtime call: {targetMethod.Name}");
        }
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot ToCandidate(
        CropPhysicalCommitSaveData owner) => new(
        PhysicalItemDispositionKind.Transfer,
        owner.operationId,
        owner.reasonCode,
        ItemFingerprint(owner),
        owner.inputs.OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => value.sourceStackId)
            .ToArray(),
        owner.inputQuantity,
        owner.inputMassGrams,
        owner.commitId);

    private static string ItemFingerprint(CropPhysicalCommitSaveData owner) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{owner.reasonCode}:"
        + string.Join(",", owner.inputs
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => $"{value.sourceStackId}={value.quantity}"));

    private static PhysicalItemRestoreCandidateDispositionSnapshot Copy(
        PhysicalItemRestoreCandidateDispositionSnapshot source,
        long mass,
        PhysicalItemDispositionKind? kind = null,
        string requestFingerprint = null) => new(
        kind ?? source.Kind,
        source.OperationId,
        source.ReasonCode,
        requestFingerprint ?? source.RequestFingerprint,
        source.SourceStackIds,
        source.Quantity,
        mass,
        source.CommitId);

    private static PhysicalItemRestoreCandidateDispositionSnapshot
        ToTreatmentCandidate(CropTreatmentOrderSaveData owner) => new(
            PhysicalItemDispositionKind.Sink,
            owner.operationId,
            owner.reasonCode,
            owner.requestFingerprint,
            owner.sourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal).ToArray(),
            owner.quantity,
            owner.inputMassGrams,
            owner.commitId);

    private static void Validate(
        IReadOnlyCollection<CropPhysicalOwnerValidationSnapshot> owners,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts) =>
        CropPhysicalRestoreGuard.ValidateOwnerSnapshots(
            owners,
            new CandidateQuery(receipts));

    private static bool Reject(
        IReadOnlyCollection<CropPhysicalOwnerValidationSnapshot> owners,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
    {
        try
        {
            Validate(owners, receipts);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            values;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values) =>
            this.values = values
                ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot value)
        {
            value = values.FirstOrDefault(candidate => string.Equals(
                candidate.OperationId,
                operationId,
                StringComparison.Ordinal));
            return value != null;
        }
    }

    private sealed class FixtureGateway :
        IPhysicalSeedLotGateway,
        IPhysicalFacilityItemSinkGateway
    {
        private readonly WorldItemRepository repository;
        private readonly WorldItemQueryService query;
        private readonly IPhysicalItemBatchDispositionService dispositions;
        private readonly PhysicalFacilityItemSinkGateway sink;

        internal FixtureGateway(IDungeonItemCatalogProvider catalog)
        {
            repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemMassQuery mass = new(catalog);
            query = new WorldItemQueryService(
                catalog,
                mass,
                repository,
                EditorNullItemMarkerPresenter.Instance);
            dispositions = new PhysicalItemBatchDispositionService(
                repository,
                mass,
                EditorNullItemMarkerPresenter.Instance);
            sink = new PhysicalFacilityItemSinkGateway(
                new PhysicalStockQuery(repository, catalog, mass),
                dispositions);
        }

        internal bool FailNextAcknowledgement { get; set; }

        internal string Add(
            string itemId,
            int quantity,
            string destinationId = DestinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                quantity,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId);

        internal string AddSeed(string destinationId = DestinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                SeedItemId,
                1,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId,
                components: new[]
                {
                    SeedLotItemStateCodec.Encode(new SeedLotState
                    {
                        cropId = CropId,
                        cultivarGenomeId = "genome:twilight-grain:base",
                        generation = 0,
                        pathogenLoad = 20f
                    })
                });

        internal int Quantity(string stackId) =>
            repository.GetEditorTestQuantity(stackId);

        internal bool HasPending(string operationId) =>
            dispositions.TryGetPending(operationId, out _);

        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => sink.TryCommitSinkPending(
                destinationId,
                itemId,
                quantity,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            sink.TryGetPending(operationId, out receipt);

        public bool Acknowledge(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }
            return sink.Acknowledge(commitId, out failureReason);
        }

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            query.GetAllStacks();

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => dispositions.TryCommitPending(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            dispositions.TryGetPending(operationId, out receipt);

        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }
            return dispositions.Acknowledge(commitId, out failureReason);
        }

        public bool CanSpawnSeedLot(
            string seedItemId,
            int amount,
            Vector2Int position,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return false;
        }

        public bool RequestBestSeedLot(
            string seedItemId,
            string cropId,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out DomainFailure failure)
        {
            requested = 0;
            failure = DomainFailure.None;
            return false;
        }

        public bool TryReleaseUnreachableSeedDelivery(
            string seedItemId,
            string cropId,
            Vector2Int destinationPosition,
            string destinationId,
            out bool released,
            out DomainFailure failure)
        {
            released = false;
            failure = DomainFailure.None;
            return true;
        }

        public bool SpawnSeedLot(
            string seedItemId,
            int amount,
            SeedLotState seedLot,
            Vector2Int position) => false;
    }

    private sealed class RecordingTreatmentTare :
        IPackagedLotTareDispositionService
    {
        internal int CallCount { get; private set; }
        internal string LastParentCommitId { get; private set; } = string.Empty;

        public bool EnsureTerminalSinkOutputs(
            IReadOnlyDictionary<string, int> consumedItems,
            Vector2Int outputPosition,
            string parentCommitId,
            out PackagedLotTareOutputReceipt receipt,
            out string failureReason)
        {
            CallCount++;
            LastParentCommitId = parentCommitId ?? string.Empty;
            receipt = default;
            failureReason = string.Empty;
            return consumedItems != null
                && consumedItems.TryGetValue(
                    TreatmentItemId,
                    out int quantity)
                && quantity == 1;
        }
    }
}
#endif
