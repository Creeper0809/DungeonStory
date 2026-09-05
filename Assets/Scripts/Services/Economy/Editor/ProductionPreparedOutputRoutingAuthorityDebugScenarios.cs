#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputRoutingAuthorityDebugScenarios
{
    private const string DestinationId =
        "production-output:building:routing-authority-fixture";
    private static readonly ProductionBillId BillId =
        (ProductionBillId)"production-bill:7101";
    private static readonly BuildingInstanceId FacilityId =
        (BuildingInstanceId)"building:routing-authority-fixture";

    [MenuItem("DungeonStory/Debug/Economy/Run Prepared Output Routing Authority")]
    public static void RunAll()
    {
        VerifyExternalInputDispositionRoundTrip();
        VerifyDurableRouteOutboxAndCheckpointGc();
        VerifyDeliveryRevisionRerouteAuthority();
        VerifyRestoreTransactionRollback();
        Debug.Log("V27_PRODUCTION_PREPARED_OUTPUT_ROUTING_AUTHORITY=PASS");
    }

    private static void VerifyExternalInputDispositionRoundTrip()
    {
        ProductionPreparedOutputBatchSaveData batch = CreateCompletedBatch();
        ProductionPreparedOutputLineSaveData externalInput = new()
        {
            outputLineId = "output:declared-external-input",
            role = ProductionOutputRole.DeclaredExternalInput,
            itemId = string.Empty,
            quantity = 0,
            componentPayload =
                "process-addition@1|mode=residual|externalInputKind="
                + "AbstractProcessAddition|reason=fixture-addition|"
                + "physicalSource=false|equation=" + Digest('7'),
            componentFingerprint = Digest('6'),
            qualityPermille = 1000,
            rollKind = "process-addition",
            rollValue = 0L,
            rollUpperExclusive = 1L,
            rollSucceeded = true,
            exactMassGrams = 200L
        };
        externalInput.lineCommitId =
            ProductionPreparedOutputIdentity.BuildLineCommitId(
                batch.batchCommitId,
                externalInput.outputLineId);
        batch.totalDeclaredExternalInputMassGrams = 200L;
        batch.lines.Add(externalInput);
        batch.lines = batch.lines
            .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
            .ToList();
        ProductionPreparedOutputContract.ValidateForBill(
            batch,
            BillId,
            batch.recipeId,
            batch.cycleSequence,
            batch.destinationId);

        ProductionPreparedOutputRoutingAuthority authority = new();
        authority.PublishCommittedBatch(batch, FacilityId);
        IProductionPreparedOutputRoutingBatchQuery query = authority;
        Require(
            query.TryCaptureBatch(
                batch.batchCommitId,
                out ProductionPreparedOutputRoutingBatchSnapshot snapshot)
            && snapshot.Lines.Count == 1
            && snapshot.NonPhysicalDispositions.Count == 2
            && snapshot.TotalDeclaredLossMassGrams == 90L
            && snapshot.TotalDeclaredExternalInputMassGrams == 200L
            && snapshot.NonPhysicalDispositions.Single(value =>
                    value.Role == ProductionOutputRole.DeclaredExternalInput)
                .ExactMassGrams == 200L,
            "declared external input entered physical routing or lost its exact receipt");

        ProductionPreparedOutputRoutingSaveData saved = authority.Capture();
        string canonical = JsonUtility.ToJson(saved);
        ProductionPreparedOutputRoutingSaveData decoded =
            JsonUtility.FromJson<ProductionPreparedOutputRoutingSaveData>(
                canonical);
        ProductionPreparedOutputRoutingAuthority restored = new();
        restored.Restore(restored.BuildRestoreCandidate(decoded));
        Require(
            string.Equals(
                canonical,
                JsonUtility.ToJson(restored.Capture()),
                StringComparison.Ordinal)
            && ((IProductionPreparedOutputRoutingBatchQuery)restored)
                .TryCaptureBatch(batch.batchCommitId, out var restoredBatch)
            && restoredBatch.TotalDeclaredExternalInputMassGrams == 200L,
            "declared external-input routing receipt did not round-trip exactly");

        ProductionPreparedOutputRoutingSaveData drift = authority.Capture();
        drift.batches[0].totalDeclaredExternalInputMassGrams++;
        RequireThrows(
            () => restored.BuildRestoreCandidate(drift),
            "declared external-input total drift was accepted");

        ProductionPreparedOutputRoutingSaveData receiptDrift =
            authority.Capture();
        receiptDrift.batches[0].nonPhysicalDispositions.Single(value =>
                value.role == ProductionOutputRole.DeclaredExternalInput)
            .exactMassGrams++;
        RequireThrows(
            () => restored.BuildRestoreCandidate(receiptDrift),
            "declared external-input receipt drift was accepted");
    }

    private static void VerifyDeliveryRevisionRerouteAuthority()
    {
        ProductionPreparedOutputRoutingAuthority authority = new();
        authority.PublishCommittedBatch(CreateCompletedBatch(), FacilityId);
        ProductionPreparedOutputRoutingLineSnapshot line = authority.CaptureAll()
            .Single();
        ProductionPreparedOutputRouteRequestSnapshot route = authority.PrepareRoute(
            line.BatchCommitId,
            line.LineCommitId,
            string.Empty,
            0,
            0,
            1);
        IProductionPreparedOutputDeliveryRerouteParticipant reroutes = authority;
        ProductionPreparedOutputDeliveryRevisionSnapshot pending = reroutes
            .CaptureCurrentDelivery(route.RouteOperationId);
        Require(pending.Revision == 0L
                && pending.TargetKind == ProductionPreparedOutputDeliveryTargetKind
                    .WarehouseSelectionPending
                && string.IsNullOrEmpty(pending.TargetDestinationId)
                && string.IsNullOrEmpty(
                    pending.OriginalPhysicalReceiptFingerprint),
            "initial empty target was not represented as warehouse selection pending");
        RequireThrows(
            () => reroutes.PrepareDeliveryReroute(
                route.RouteOperationId,
                pending.Revision,
                pending.RevisionFingerprint,
                Digest('0'),
                ProductionPreparedOutputDeliveryRerouteReason
                    .DestinationInvalidated,
                "warehouse:before-physical-ack",
                1,
                1,
                Digest('1')),
            "physically pending route accepted a delivery reroute");

        ProductionPreparedOutputPhysicalRouteReceipt receipt = CreateReceipt(
            route,
            "stack:reroute-source",
            "stack:reroute-loose",
            0,
            1,
            200L);
        authority.CommitPhysicalRoute(receipt);
        authority.AcknowledgePhysicalRoute(
            route.RouteOperationId,
            receipt.PhysicalReceiptFingerprint);
        ProductionPreparedOutputDeliveryRevisionSnapshot initial = reroutes
            .CaptureCurrentDelivery(route.RouteOperationId);
        Require(initial.Revision == 0L
                && initial.OriginalPhysicalReceiptFingerprint
                    == receipt.PhysicalReceiptFingerprint,
            "physical acknowledgement did not finalize initial delivery revision");

        IProductionPreparedOutputDeliveryRerouteCandidate candidate = reroutes
            .PrepareDeliveryReroute(
                route.RouteOperationId,
                initial.Revision,
                initial.RevisionFingerprint,
                receipt.PhysicalReceiptFingerprint,
                ProductionPreparedOutputDeliveryRerouteReason
                    .DestinationInvalidated,
                "warehouse:reroute-a",
                14,
                9,
                Digest('2'));
        IProductionPreparedOutputDeliveryRerouteCandidate conflict = reroutes
            .PrepareDeliveryReroute(
                route.RouteOperationId,
                initial.Revision,
                initial.RevisionFingerprint,
                receipt.PhysicalReceiptFingerprint,
                ProductionPreparedOutputDeliveryRerouteReason
                    .DestinationInvalidated,
                "warehouse:reroute-b",
                15,
                9,
                Digest('3'));
        Require(candidate.ExpectedCurrentRevision == 0L
                && candidate.ExpectedCurrentRevisionFingerprint
                    == initial.RevisionFingerprint
                && candidate.PreviousRevisionFingerprint
                    == initial.RevisionFingerprint
                && candidate.NextRevision == 1L
                && candidate.RerouteOperationId.StartsWith(
                    "production-output-delivery-reroute:",
                    StringComparison.Ordinal),
            "detached reroute candidate lost its revision join keys");
        reroutes.PublishDeliveryReroute(candidate);
        RequireThrows(
            () => reroutes.PublishDeliveryReroute(conflict),
            "same delivery revision published a conflicting target");
        ProductionPreparedOutputDeliveryRevisionSnapshot published = reroutes
            .CaptureCurrentDelivery(route.RouteOperationId);
        ProductionPreparedOutputRouteRequestSnapshot immutableOriginal = authority
            .CaptureRouteOperations().Single();
        Require(published.Revision == 1L
                && published.TargetKind ==
                    ProductionPreparedOutputDeliveryTargetKind.ExactRerouteTarget
                && published.TargetDestinationId == "warehouse:reroute-a"
                && published.PreviousRevisionFingerprint
                    == initial.RevisionFingerprint
                && published.OriginalPhysicalReceiptFingerprint
                    == receipt.PhysicalReceiptFingerprint
                && immutableOriginal.RequestFingerprint == route.RequestFingerprint
                && immutableOriginal.PhysicalReceiptFingerprint
                    == receipt.PhysicalReceiptFingerprint
                && string.IsNullOrEmpty(immutableOriginal.TargetDestinationId),
            "delivery reroute mutated the immutable original request or receipt");
        reroutes.RollbackDeliveryReroute(candidate);
        Require(reroutes.CaptureCurrentDelivery(route.RouteOperationId).Revision == 0L,
            "delivery reroute rollback did not restore the detached previous image");
        reroutes.PublishDeliveryReroute(candidate);
        reroutes.CompleteDeliveryReroute(candidate);

        IProductionPreparedOutputDeliveryRerouteCandidate replay = reroutes
            .PrepareDeliveryReroute(
                route.RouteOperationId,
                initial.Revision,
                initial.RevisionFingerprint,
                receipt.PhysicalReceiptFingerprint,
                ProductionPreparedOutputDeliveryRerouteReason
                    .DestinationInvalidated,
                "warehouse:reroute-a",
                14,
                9,
                Digest('2'));
        reroutes.PublishDeliveryReroute(replay);
        reroutes.CompleteDeliveryReroute(replay);
        Require(reroutes.CaptureCurrentDelivery(route.RouteOperationId).Revision == 1L,
            "identical delivery reroute replay appended a duplicate revision");
        RequireThrows(
            () => reroutes.PrepareDeliveryReroute(
                route.RouteOperationId,
                initial.Revision,
                initial.RevisionFingerprint,
                receipt.PhysicalReceiptFingerprint,
                ProductionPreparedOutputDeliveryRerouteReason.WarehouseRetarget,
                string.Empty,
                16,
                9,
                Digest('4')),
            "post-invalidation delivery revision accepted an empty target");

        ProductionPreparedOutputRoutingSaveData saved = authority.Capture();
        ProductionPreparedOutputRoutingAuthority restored = new();
        restored.Restore(restored.BuildRestoreCandidate(saved));
        ProductionPreparedOutputDeliveryRevisionSnapshot restoredCurrent =
            ((IProductionPreparedOutputDeliveryRerouteParticipant)restored)
            .CaptureCurrentDelivery(route.RouteOperationId);
        Require(restoredCurrent.RevisionFingerprint ==
                published.RevisionFingerprint,
            "delivery revision chain did not round-trip exactly");
        ProductionPreparedOutputRoutingSaveData tampered = authority.Capture();
        tampered.batches[0].lines[0].routeOperations[0]
            .deliveryRevisions[1].targetPositionX++;
        RequireThrows(
            () => restored.BuildRestoreCandidate(tampered),
            "tampered delivery revision fingerprint was accepted");
        ProductionPreparedOutputRoutingSaveData duplicate = authority.Capture();
        duplicate.batches[0].lines[0].routeOperations[0]
            .deliveryRevisions.Add(duplicate.batches[0].lines[0]
                .routeOperations[0].deliveryRevisions[1].Clone());
        RequireThrows(
            () => restored.BuildRestoreCandidate(duplicate),
            "duplicate delivery revision was accepted");
        ProductionPreparedOutputRoutingSaveData wrongReceipt = authority.Capture();
        wrongReceipt.batches[0].lines[0].routeOperations[0]
            .deliveryRevisions[1].originalPhysicalReceiptFingerprint = Digest('9');
        RequireThrows(
            () => restored.BuildRestoreCandidate(wrongReceipt),
            "delivery revision accepted a changed original physical receipt");
        ProductionPreparedOutputRoutingSaveData missing = authority.Capture();
        missing.batches[0].lines[0].routeOperations[0].deliveryRevisions = null;
        RequireThrows(
            () => restored.BuildRestoreCandidate(missing),
            "current routing schema accepted a missing delivery revision chain");

        ProductionPreparedOutputRoutingAuthority consumerAuthority = new();
        consumerAuthority.PublishCommittedBatch(CreateCompletedBatch(), FacilityId);
        ProductionPreparedOutputRoutingLineSnapshot consumerLine = consumerAuthority
            .CaptureAll().Single();
        ProductionPreparedOutputRouteRequestSnapshot consumerRoute = consumerAuthority
            .PrepareRoute(
                consumerLine.BatchCommitId,
                consumerLine.LineCommitId,
                "consumer:meal-prep",
                5,
                6,
                1);
        ProductionPreparedOutputDeliveryRevisionSnapshot consumerInitial =
            ((IProductionPreparedOutputDeliveryRerouteParticipant)consumerAuthority)
            .CaptureCurrentDelivery(consumerRoute.RouteOperationId);
        Require(consumerInitial.TargetKind ==
                    ProductionPreparedOutputDeliveryTargetKind.InitialExactTarget
                && consumerInitial.TargetDestinationId == "consumer:meal-prep",
            "initial nonempty consumer target was not preserved exactly");
    }

    private static void VerifyDurableRouteOutboxAndCheckpointGc()
    {
        ProductionPreparedOutputRoutingAuthority authority = new();
        ProductionPreparedOutputBatchSaveData completed = CreateCompletedBatch();
        authority.PublishCommittedBatch(completed, FacilityId);
        authority.PublishCommittedBatch(completed.Clone(), FacilityId);
        RequireThrows(
            () => authority.PublishCommittedBatch(
                completed.Clone(),
                (BuildingInstanceId)"building:wrong-routing-owner"),
            "wrong facility owner was accepted");

        ProductionPreparedOutputRoutingLineSnapshot line = authority
            .CaptureDestination(DestinationId)
            .Single();
        Require(line.Role == ProductionOutputRole.Main
                && line.ItemId == "feed:silage"
                && line.RemainingQuantity == 3
                && line.RemainingMassGrams == 600L
                && line.OriginalQuantity == 3
                && line.OriginalMassGrams == 600L
                && line.RoutedQuantity == 0
                && line.RoutedMassGrams == 0L
                && line.CycleSequence == 1
                && line.ComponentFingerprint == Digest('c')
                && line.OutputCapabilityId ==
                    ProductionOutputCapabilityIds.StandardDefinition
                && line.OutputCapabilityVersion ==
                    ProductionOutputCapabilityIds.StandardDefinitionVersion
                && line.OutputComponentCodecId ==
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec
                && line.OutputComponentCodecVersion ==
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion
                && line.OutputCapabilityFingerprint ==
                    ProductionOutputCapabilityDescriptorFingerprint.Capture(
                        "output:main",
                        "feed:silage",
                        ProductionOutputCapabilityIds.StandardDefinition,
                        ProductionOutputCapabilityIds.StandardDefinitionVersion,
                        ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion)
                && line.OwnerBillId == BillId.Value
                && line.OwnerFacilityId == FacilityId.Value,
            "physical Main line was not retained exactly");
        Require(authority.CaptureBill(BillId).Count == 1,
            "bill-owned routing query did not isolate its exact batch");
        Require(authority.CaptureDestination(DestinationId)
                .All(value => ProductionOutputRoleRules.IsPhysical(value.Role)),
            "DeclaredLoss entered physical routing authority");
        IProductionPreparedOutputRoutingBatchQuery batchQuery = authority;
        Require(batchQuery.TryCaptureBatch(
                    line.BatchCommitId,
                    out ProductionPreparedOutputRoutingBatchSnapshot initialBatch)
                && initialBatch.BatchCommitId == line.BatchCommitId
                && initialBatch.OwnerFacilityId == FacilityId.Value
                && initialBatch.SourceDestinationId == DestinationId
                && initialBatch.Lines.Count == 1
                && initialBatch.RouteOperations.Count == 0
                && initialBatch.PhysicalReceipts.Count == 0
                && initialBatch.NonPhysicalDispositions.Count == 1
                && initialBatch.TotalDeclaredLossMassGrams == 90L
                && initialBatch.NonPhysicalDispositions[0].Role ==
                    ProductionOutputRole.DeclaredLoss
                && initialBatch.NonPhysicalDispositions[0].CanonicalPayload ==
                    "declared-loss"
                && initialBatch.NonPhysicalDispositions[0]
                    .DispositionFingerprint == Digest('b')
                && initialBatch.RemainingQuantity == 3
                && initialBatch.RemainingMassGrams == 600L
                && !initialBatch.IsDrainAcknowledged,
            "immutable batch query lost initial routing ownership");
        Require(!batchQuery.TryCaptureBatch(
                "production-output-batch:missing",
                out _),
            "immutable batch query synthesized a missing batch");

        ProductionPreparedOutputRouteRequestSnapshot first =
            authority.PrepareRoute(
            line.BatchCommitId,
            line.LineCommitId,
            "loose:warehouse-route",
            12,
            7,
            1);
        ProductionPreparedOutputRouteRequestSnapshot replay = authority.PrepareRoute(
            line.BatchCommitId,
            line.LineCommitId,
            "loose:warehouse-route",
            12,
            7,
            1);
        Require(first.RouteOperationId == replay.RouteOperationId
                && first.RequestFingerprint == replay.RequestFingerprint
                && first.SourceOffsetQuantity == 0
                && first.SourceOffsetMassGrams == 0L
                && first.RoutedQuantity == 1
                && first.RoutedMassGrams == 200L,
            "same logical route replay was not deterministic");
        FacilityOutputExactRouteRequest itemsRequest = new(
            first.RouteOperationId,
            first.BatchCommitId,
            first.SourceDestinationId,
            first.TargetDestinationId,
            new Vector2Int(first.TargetPositionX, first.TargetPositionY),
            new[]
            {
                new FacilityOutputExactRouteSliceRequest(
                    first.OutputLineId,
                    first.LineCommitId,
                    first.ItemId,
                    first.SourceOffsetQuantity,
                    first.RoutedQuantity,
                    first.RoutedMassGrams,
                    first.ComponentFingerprint)
            });
        Require(itemsRequest.RequestFingerprint == first.RequestFingerprint,
            "Economy and Items route-request fingerprints diverged");
        RequireThrows(() => authority.PrepareRoute(
                line.BatchCommitId,
                line.LineCommitId,
                "loose:different-target",
                12,
                7,
                1),
            "different target was accepted over a pending route");

        RequireThrows(
            () => authority.CommitPhysicalRoute(CreateReceipt(
                first, "stack:source:a", "stack:routed:a", 0, 1, 201L)),
            "one-gram physical receipt mismatch was accepted");
        RequireThrows(
            () => authority.CommitPhysicalRoute(CreateReceipt(
                first, "stack:source:a", "stack:routed:a", 1, 1, 200L)),
            "wrong source offset was accepted");
        RequireThrows(
            () => authority.CommitPhysicalRoute(CreateReceipt(
                first,
                "stack:source:a",
                "stack:routed:a",
                0,
                1,
                200L,
                routedOffset: 1)),
            "nonzero routed-stack lineage offset was accepted");
        RequireThrows(
            () => authority.CommitPhysicalRoute(CreateReceipt(
                first,
                "stack:source:a",
                "stack:routed:a",
                0,
                1,
                200L,
                "loose:wrong-receipt-target")),
            "wrong physical receipt target was accepted");
        ProductionPreparedOutputPhysicalRouteReceipt firstReceipt = CreateReceipt(
            first, "stack:source:a", "stack:routed:a", 0, 1, 200L);
        authority.CommitPhysicalRoute(firstReceipt);
        authority.CommitPhysicalRoute(firstReceipt);
        RequireThrows(
            () => authority.CommitPhysicalRoute(CreateReceipt(
                first, "stack:source:a", "stack:routed:tampered", 0, 1, 200L)),
            "different physical receipt replay was accepted");
        Require(authority.HasOutstandingForBill(BillId)
                && !authority.CanRetireBill(BillId),
            "physically applied route prematurely released its bill owner");
        Require(batchQuery.TryCaptureBatch(
                    line.BatchCommitId,
                    out ProductionPreparedOutputRoutingBatchSnapshot appliedBatch)
                && appliedBatch.RouteOperations.Count == 1
                && appliedBatch.PhysicalReceipts.Count == 1
                && appliedBatch.PhysicalReceipts[0]
                    .PhysicalReceiptFingerprint
                    == firstReceipt.PhysicalReceiptFingerprint
                && appliedBatch.PhysicalReceipts[0].Slices.Count == 1
                && appliedBatch.PhysicalReceipts[0].Slices[0]
                    .RoutedStackId == "stack:routed:a"
                && appliedBatch.RemainingQuantity == 2
                && appliedBatch.RemainingMassGrams == 400L
                && !appliedBatch.IsDrainAcknowledged,
            "immutable batch query lost exact physical route receipt custody");

        ProductionPreparedOutputRoutingSaveData saved = authority.Capture();
        Require(saved.version == 8
                && saved.batches.Single().totalDeclaredLossMassGrams == 90L
                && saved.batches.Single().nonPhysicalDispositions.Count == 1,
            "routing save omitted the frozen non-physical disposition receipt");
        ProductionPreparedOutputRoutingSaveData tamperedDisposition =
            authority.Capture();
        tamperedDisposition.batches.Single().nonPhysicalDispositions.Single()
            .exactMassGrams++;
        RequireThrows(
            () => authority.BuildRestoreCandidate(tamperedDisposition),
            "routing restore accepted a tampered non-physical disposition");
        ProductionPreparedOutputRoutingAuthority restored = new();
        restored.Restore(restored.BuildRestoreCandidate(saved));
        ProductionPreparedOutputRouteRequestSnapshot restoredFirst = restored
            .CaptureRouteOperations().Single();
        Require(restoredFirst.Phase == ProductionPreparedOutputRoutePhase
                    .PhysicalAppliedAwaitingItemsAck
                && restoredFirst.PhysicalReceiptFingerprint
                    == firstReceipt.PhysicalReceiptFingerprint,
            "restore lost the applied physical receipt watermark");
        RequireThrows(
            () => restored.AcknowledgePhysicalRoute(
                restoredFirst.RouteOperationId,
                Digest('f')),
            "wrong Items acknowledgement fingerprint was accepted");
        restored.AcknowledgePhysicalRoute(
            restoredFirst.RouteOperationId,
            restoredFirst.PhysicalReceiptFingerprint);
        restored.AcknowledgePhysicalRoute(
            restoredFirst.RouteOperationId,
            restoredFirst.PhysicalReceiptFingerprint);

        ProductionPreparedOutputRouteRequestSnapshot second = restored.PrepareRoute(
            line.BatchCommitId,
            line.LineCommitId,
            "loose:warehouse-route",
            12,
            7,
            2);
        Require(second.SourceOffsetQuantity == 1
                && second.SourceOffsetMassGrams == 200L,
            "second route did not start at the exact routed offset");
        ProductionPreparedOutputPhysicalRouteReceipt secondReceipt = CreateReceipt(
            second, "stack:source:b", "stack:routed:b", 1, 2, 400L);
        restored.CommitPhysicalRoute(secondReceipt);
        restored.AcknowledgePhysicalRoute(
            second.RouteOperationId,
            secondReceipt.PhysicalReceiptFingerprint);
        Require(!restored.HasOutstandingForBill(BillId)
                && restored.CanRetireBill(BillId)
                && restored.Capture().batches.Count == 1,
            "drained route did not retain its checkpoint tombstone correctly");
        IProductionPreparedOutputRoutingBatchQuery restoredBatchQuery = restored;
        Require(restoredBatchQuery.TryCaptureBatch(
                    line.BatchCommitId,
                    out ProductionPreparedOutputRoutingBatchSnapshot drainedBatch)
                && drainedBatch.RouteOperations.Count == 2
                && drainedBatch.PhysicalReceipts.Count == 2
                && drainedBatch.NonPhysicalDispositions.Count == 1
                && drainedBatch.TotalDeclaredLossMassGrams == 90L
                && drainedBatch.RemainingQuantity == 0
                && drainedBatch.RemainingMassGrams == 0L
                && drainedBatch.IsDrainAcknowledged,
            "immutable batch query did not preserve the checkpoint tombstone");

        PreparedOutputCheckpointGcContext checkpoint = new(
            1L,
            Digest('1'),
            "fixture-slot");
        IPreparedOutputCheckpointGcParticipant checkpointGc = restored;
        PreparedOutputCheckpointGcResult prepared = checkpointGc
            .PrepareCheckpointGarbageCollection(
                checkpoint,
                out IPreparedOutputCheckpointGcCandidate candidate);
        Require(prepared.Status == PreparedOutputCheckpointGcStatus.Applied
                && candidate != null
                && candidate.BatchCommitIds.Count == 1
                && candidate.RouteOperationIds.Count == 2,
            "checkpoint GC did not prepare the exact whole batch");
        PreparedOutputCheckpointGcResult published = checkpointGc
            .PublishCheckpointGarbageCollection(candidate);
        checkpointGc.CompleteCheckpointGarbageCollection(candidate);
        Require(published.Status == PreparedOutputCheckpointGcStatus.Applied
                && published.CollectedBatchCount == 1
                && restored.Capture().batches.Count == 0,
            "checkpoint-safe GC did not retire the exact drained batch");
        Require(checkpointGc.PrepareCheckpointGarbageCollection(
                    checkpoint,
                    out _).Status
                == PreparedOutputCheckpointGcStatus.AlreadyApplied,
            "identical checkpoint replay was not idempotent");
        PreparedOutputCheckpointGcContext conflictingReplay = new(
            1L,
            Digest('2'),
            "fixture-slot");
        Require(checkpointGc.PrepareCheckpointGarbageCollection(
                    conflictingReplay,
                    out _).Status
                == PreparedOutputCheckpointGcStatus.Corruption,
            "same checkpoint sequence accepted different serialized bytes");
        PreparedOutputCheckpointGcContext skipped = new(
            3L,
            Digest('3'),
            "fixture-slot");
        Require(checkpointGc.PrepareCheckpointGarbageCollection(skipped, out _).Status
                == PreparedOutputCheckpointGcStatus.Corruption,
            "non-contiguous checkpoint sequence was accepted");
    }

    private static void VerifyRestoreTransactionRollback()
    {
        ProductionPreparedOutputRoutingAuthority authority = new();
        authority.PublishCommittedBatch(CreateCompletedBatch(), FacilityId);
        authority.BeginRestoreCandidate();
        authority.Restore(new ProductionPreparedOutputRoutingSaveData
        {
            version = ProductionPreparedOutputRoutingSaveData.CurrentVersion,
            lastConfirmedCheckpointSequence = 8L,
            lastConfirmedCheckpointDigest = Digest('8'),
            batches = new List<ProductionPreparedOutputRoutingBatchSaveData>()
        });
        authority.PublishRestoreCandidate();
        Require(authority.CaptureAll().Count == 0,
            "restore candidate did not publish its detached image");
        authority.RollbackPublishedRestoreCandidate();
        Require(authority.CaptureAll().Count == 1
                && authority.Capture().lastConfirmedCheckpointSequence == 0L,
            "restore rollback did not restore routing owners and checkpoint");

        ProductionPreparedOutputRoutingSaveData fingerprintDrift =
            authority.Capture();
        fingerprintDrift.batches[0].lines[0].outputCapabilityFingerprint =
            Digest('9');
        RequireThrows(
            () => authority.BuildRestoreCandidate(fingerprintDrift),
            "routing restore accepted output capability fingerprint drift");
        ProductionPreparedOutputRoutingSaveData versionDrift = authority.Capture();
        versionDrift.batches[0].lines[0].outputCapabilityVersion++;
        RequireThrows(
            () => authority.BuildRestoreCandidate(versionDrift),
            "routing restore accepted output capability version drift");
        Require(authority.CaptureAll().Count == 1
                && authority.Capture().lastConfirmedCheckpointSequence == 0L,
            "invalid routing restore mutated live capability provenance");
    }

    private static ProductionPreparedOutputPhysicalRouteReceipt CreateReceipt(
        ProductionPreparedOutputRouteRequestSnapshot request,
        string sourceStackId,
        string routedStackId,
        int sourceOffset,
        int quantity,
        long massGrams,
        string targetDestinationId = null,
        int routedOffset = 0)
    {
        ProductionPreparedOutputPhysicalRouteSliceReceipt[] slices =
        {
            new(
                sourceStackId,
                routedStackId,
                request.OutputLineId,
                request.LineCommitId,
                request.ItemId,
                sourceOffset,
                routedOffset,
                quantity,
                massGrams,
                request.ComponentFingerprint)
        };
        ProductionPreparedOutputPhysicalRouteReceipt unsigned = new(
            request.RouteOperationId,
            request.RequestFingerprint,
            string.Empty,
            request.BatchCommitId,
            request.SourceDestinationId,
            targetDestinationId ?? request.TargetDestinationId,
            request.TargetPositionX,
            request.TargetPositionY,
            quantity,
            massGrams,
            slices);
        string fingerprint = ProductionPreparedOutputRoutingAuthority
            .ComputePhysicalReceiptFingerprint(unsigned);
        return new ProductionPreparedOutputPhysicalRouteReceipt(
            request.RouteOperationId,
            request.RequestFingerprint,
            fingerprint,
            request.BatchCommitId,
            request.SourceDestinationId,
            targetDestinationId ?? request.TargetDestinationId,
            request.TargetPositionX,
            request.TargetPositionY,
            quantity,
            massGrams,
            slices);
    }

    private static ProductionPreparedOutputBatchSaveData CreateCompletedBatch()
    {
        string outcome = Digest('a');
        string batchCommitId = ProductionPreparedOutputIdentity.BuildBatchCommitId(
            BillId,
            1,
            outcome);
        ProductionPreparedOutputLineSaveData loss = new()
        {
            outputLineId = "output:declared-loss",
            role = ProductionOutputRole.DeclaredLoss,
            itemId = string.Empty,
            quantity = 0,
            componentPayload = "declared-loss",
            componentFingerprint = Digest('b'),
            qualityPermille = 1000,
            rollKind = "fixture",
            rollValue = 0,
            rollUpperExclusive = 1,
            rollSucceeded = true,
            exactMassGrams = 90L
        };
        ProductionPreparedOutputLineSaveData main = new()
        {
            outputLineId = "output:main",
            role = ProductionOutputRole.Main,
            itemId = "feed:silage",
            outputCapabilityId = ProductionOutputCapabilityIds.StandardDefinition,
            outputCapabilityVersion =
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
            outputComponentCodecId =
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            outputComponentCodecVersion =
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
            outputCapabilityFingerprint =
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    "output:main",
                    "feed:silage",
                    ProductionOutputCapabilityIds.StandardDefinition,
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
            quantity = 3,
            componentPayload = "generic-silage",
            componentFingerprint = Digest('c'),
            qualityPermille = 1000,
            rollKind = "fixture",
            rollValue = 0,
            rollUpperExclusive = 1,
            rollSucceeded = true,
            exactMassGrams = 600L
        };
        foreach (ProductionPreparedOutputLineSaveData line in new[] { loss, main })
        {
            line.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
                batchCommitId,
                line.outputLineId);
        }
        ProductionPreparedOutputBatchSaveData batch = new()
        {
            phase = ProductionPreparedOutputPhase.Completed,
            billId = BillId.Value,
            cycleSequence = 1,
            recipeId = "recipe:silage",
            destinationId = DestinationId,
            recipeDefinitionDigest = Digest('d'),
            migrationProfileDigest = Digest('a'),
            capacitySourceDigest = Digest('f'),
            maximumMassProofDigest = Digest('9'),
            maximumBatchMassGrams = 600L,
            capacityClaimDigest = Digest('8'),
            outputBufferCycleCapacity = 4,
            projectedPortfolioCapacityGrams = 2_400L,
            requiredMinimumCapacityGrams = 2_400L,
            outcomeFingerprint = outcome,
            admissionFingerprint = Digest('e'),
            batchCommitId = batchCommitId,
            totalPhysicalMassGrams = 600L,
            totalDeclaredLossMassGrams = 90L,
            lines = new List<ProductionPreparedOutputLineSaveData> { loss, main },
            physicalCandidates = new List<
                ProductionPreparedOutputPhysicalCandidateSaveData>
            {
                new()
                {
                    stackId = "stack:routing-authority:0001",
                    batchCommitId = batchCommitId,
                    outputLineId = main.outputLineId,
                    lineCommitId = main.lineCommitId,
                    itemId = main.itemId,
                    quantity = 3,
                    massGrams = 600L,
                    destinationId = DestinationId,
                    state = ProductionPreparedPhysicalCandidateState
                        .FacilityOutputBuffer
                }
            }
        };
        ProductionPreparedOutputContract.ValidateForBill(
            batch,
            BillId,
            "recipe:silage",
            1,
            DestinationId);
        return batch;
    }

    private static string Digest(char value) => new(value, 64);

    private static void RequireThrows(Action action, string message)
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
}
#endif
