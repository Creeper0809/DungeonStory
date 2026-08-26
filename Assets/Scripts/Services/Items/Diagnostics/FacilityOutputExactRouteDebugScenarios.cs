#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FacilityOutputExactRouteDebugScenarios
{
    private const string BatchId = "production-output-batch:qa:00000001:digest";
    private const string LineId = "output:main";
    private const string ItemId = "feed:qa";
    private const string ComponentFingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static void RunAll()
    {
        VerifyExactSplitReplayAndAcknowledgement();
        VerifyPreparedFingerprintMismatchFailsClosed();
        VerifyStatefulSplitAndUniquePartialRejection();
        VerifyMultiStepPartialRouteRestore();
        VerifyRoutableDescendantPartitionRestore();
        VerifyDeliveryRevisionOverlayRoundTripAndTamperGuards();
        VerifyDeliveryOverlayParticipantLifecycle();
        VerifyWarehouseFallbackRecoveryRestore();
        VerifyRestoreAcknowledgementAndRollback();
        VerifyCheckpointGcStablePublishRollbackAndRestore();
        VerifyCheckpointGcPhysicalDeferral();
        VerifyCheckpointGcCoverageReplayAndStaleGuards();
        VerifyCheckpointGcApiAndRawSaveGuards();
    }

    private static void VerifyExactSplitReplayAndAcknowledgement()
    {
        Fixture fixture = new();
        fixture.Publish(quantity: 5);
        FacilityOutputExactRouteRequest request = fixture.Request(
            "route:qa:split",
            quantity: 3,
            targetDestinationId: "warehouse:qa:food",
            targetPosition: new Vector2Int(20, 8),
            ComponentFingerprint);
        Require(
            fixture.Route.TryRoute(
                request,
                out FacilityOutputExactRouteReceipt receipt,
                out FacilityOutputExactRouteFailure failure),
            "Exact route failed: " + failure.Reason);
        Require(
            receipt.Slices.Count == 2
            && receipt.Slices.Select(value => value.SourceOffsetQuantity)
                .SequenceEqual(new[] { 0, 2 })
            && receipt.Slices.All(value => value.RoutedOffsetQuantity == 0)
            && receipt.TotalQuantity == 3
            && receipt.TotalMassGrams == 3_000L,
            "Exact route did not preserve the canonical source partition.");
        foreach (FacilityOutputExactRouteSliceReceipt slice in receipt.Slices)
        {
            WorldItemStackRecord routed = fixture.Repository.RecordsById[
                slice.RoutedStackId];
            Require(
                routed.state == WorldItemStackState.Loose
                && routed.position == Fixture.SourcePosition
                && routed.destinationId == request.TargetDestinationId
                && routed.hasDestinationPosition
                && routed.destinationPosition == request.TargetPosition
                && FacilityOutputExactRouteCustodyCodec.TryRead(
                    routed.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                && metadata.OriginPosition == Fixture.SourcePosition
                && metadata.ComponentFingerprint == ComponentFingerprint
                && metadata.Phase ==
                    FacilityOutputExactRouteCustodyPhase.PhysicalPending
                && FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(
                    routed.components),
                "Exact route teleported cargo or exposed it before acknowledgement.");
        }
        Require(
            fixture.Route.TryRoute(request, out var replay, out _)
            && replay.PhysicalReceiptFingerprint ==
                receipt.PhysicalReceiptFingerprint
            && fixture.Repository.Records.Count == 4,
            "Exact route replay duplicated or replaced physical stacks.");
        FacilityOutputExactRouteRequest conflict = fixture.Request(
            request.RouteOperationId,
            3,
            "warehouse:qa:other",
            request.TargetPosition,
            ComponentFingerprint);
        Require(
            !fixture.Route.TryRoute(conflict, out _, out var conflictFailure)
            && conflictFailure.Code ==
                FacilityOutputExactRouteFailureCode.OperationConflict,
            "Conflicting operation replay was accepted.");
        Require(
            fixture.Route.TryAcknowledge(
                receipt.RouteOperationId,
                receipt.PhysicalReceiptFingerprint,
                out _,
                out failure),
            "Exact route acknowledgement failed: " + failure.Reason);
        Require(
            receipt.Slices.All(slice =>
                FacilityOutputExactRouteCustodyCodec.TryRead(
                    fixture.Repository.RecordsById[slice.RoutedStackId].components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                && metadata.Phase == FacilityOutputExactRouteCustodyPhase.Routable
                && !FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(
                    fixture.Repository.RecordsById[slice.RoutedStackId].components)),
            "Acknowledgement did not atomically expose every routed slice.");
    }

    private static void VerifyPreparedFingerprintMismatchFailsClosed()
    {
        Fixture fixture = new();
        fixture.Publish(quantity: 2);
        FacilityOutputExactRouteRequest request = fixture.Request(
            "route:qa:fingerprint-mismatch",
            1,
            "warehouse:qa:food",
            new Vector2Int(15, 5),
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        Require(
            !fixture.Route.TryRoute(request, out _, out var failure)
            && failure.Code == FacilityOutputExactRouteFailureCode.ComponentMismatch
            && fixture.Repository.Records.All(value =>
                value.state == WorldItemStackState.FacilityOutputBuffer
                && value.position == Fixture.SourcePosition),
            "Prepared component fingerprint mismatch mutated physical output.");
    }

    private static void VerifyStatefulSplitAndUniquePartialRejection()
    {
        ItemInstanceComponentSaveData state = new()
        {
            componentTypeId = "item-state:qa-feed-lot",
            schemaVersion = 1,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = "lot",
                    kind = ItemStateValueKind.String,
                    stringValue = "qa"
                }
            }
        };
        Fixture stateful = new();
        stateful.Publish(2, new[] { state });
        Require(
            stateful.Route.TryRoute(
                stateful.Request(
                    "route:qa:stateful",
                    1,
                    string.Empty,
                    new Vector2Int(13, 7),
                    ComponentFingerprint),
                out FacilityOutputExactRouteReceipt receipt,
                out _)
            && stateful.Repository.Records.Count == 2
            && stateful.Repository.Records.All(record =>
                record.components.Count(component => string.Equals(
                    component.componentTypeId,
                    state.componentTypeId,
                    StringComparison.Ordinal)) == 1)
            && receipt.Slices.Single().RoutedOffsetQuantity == 0,
            "Stateful partial route did not clone the exact business component.");

        Fixture unique = new();
        unique.Publish(2);
        unique.Repository.Records.Single().itemInstanceId = "instance:qa:unique";
        Require(
            !unique.Route.TryRoute(
                unique.Request(
                    "route:qa:unique-partial",
                    1,
                    "warehouse:qa:food",
                    new Vector2Int(14, 7),
                    ComponentFingerprint),
                out _,
                out FacilityOutputExactRouteFailure failure)
            && failure.Code ==
                FacilityOutputExactRouteFailureCode.UniquePartialForbidden
            && unique.Repository.Records.Single().quantity == 2,
            "Unique partial route was not rejected without mutation.");
    }

    private static void VerifyRestoreAcknowledgementAndRollback()
    {
        Fixture fixture = new();
        fixture.Publish(quantity: 2);
        FacilityOutputExactRouteRequest request = fixture.Request(
            "route:qa:restore",
            2,
            "warehouse:qa:food",
            new Vector2Int(18, 6),
            ComponentFingerprint);
        Require(
            fixture.Route.TryRoute(request, out var receipt, out _),
            "Restore fixture route failed.");
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> outbox =
            fixture.Route.CaptureOutbox();
        FacilityOutputExactRouteService restored = fixture.CreateRouteService();
        FacilityOutputExactRouteRestoreCandidate candidate =
            restored.BuildRestoreCandidate(outbox, fixture.CapturePhysical());
        restored.BeginRestoreCandidate();
        restored.RestoreCandidate(candidate);
        restored.AcknowledgeRestoredRoute(
            receipt.RouteOperationId,
            receipt.PhysicalReceiptFingerprint);
        restored.DiscardRestoreCandidate();
        Require(
            receipt.Slices.All(slice =>
                FacilityOutputExactRouteCustodyCodec.TryRead(
                    fixture.Repository.RecordsById[slice.RoutedStackId].components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                && metadata.Phase ==
                    FacilityOutputExactRouteCustodyPhase.PhysicalPending),
            "Restore discard did not roll physical custody back atomically.");

        candidate = restored.BuildRestoreCandidate(
            outbox,
            fixture.CapturePhysical());
        restored.BeginRestoreCandidate();
        restored.RestoreCandidate(candidate);
        restored.AcknowledgeRestoredRoute(
            receipt.RouteOperationId,
            receipt.PhysicalReceiptFingerprint);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.CaptureOutbox().Single().phase ==
                FacilityOutputExactRoutePhase.Routable
            && receipt.Slices.All(slice =>
                FacilityOutputExactRouteCustodyCodec.TryRead(
                    fixture.Repository.RecordsById[slice.RoutedStackId].components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                && metadata.Phase == FacilityOutputExactRouteCustodyPhase.Routable),
            "Restore publish did not join Routable outbox and physical custody.");
    }

    private static void VerifyMultiStepPartialRouteRestore()
    {
        Fixture fixture = new();
        fixture.Publish(quantity: 5);
        Require(
            fixture.Route.TryRoute(
                fixture.Request(
                    "route:qa:multi-step:first",
                    1,
                    "warehouse:qa:food",
                    new Vector2Int(16, 6),
                    ComponentFingerprint),
                out _,
                out _),
            "First partial route failed.");
        Require(
            fixture.Route.TryRoute(
                fixture.Request(
                    "route:qa:multi-step:second",
                    2,
                    "warehouse:qa:food",
                    new Vector2Int(16, 6),
                    ComponentFingerprint,
                    sourceOffsetQuantity: 1),
                out FacilityOutputExactRouteReceipt second,
                out _),
            "Second partial route failed from a split remainder.");
        FacilityOutputExactRouteSliceReceipt splitSource = second.Slices
            .Single(value => value.SourceOffsetQuantity == 1);
        Require(
            FacilityOutputExactRouteCustodyCodec.TryRead(
                fixture.Repository.RecordsById[splitSource.RoutedStackId].components,
                out FacilityOutputExactRouteCustodyMetadata metadata)
            && metadata.CurrentSourceStackId == splitSource.SourceStackId,
            "Split remainder lost the exact current physical source lineage.");

        FacilityOutputExactRouteService restored = fixture.CreateRouteService();
        FacilityOutputExactRouteRestoreCandidate candidate =
            restored.BuildRestoreCandidate(
                fixture.Route.CaptureOutbox(),
                fixture.CapturePhysical());
        restored.BeginRestoreCandidate();
        restored.RestoreCandidate(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.CaptureOutbox().Count == 2,
            "Multi-step partial route did not survive exact restore join.");
    }

    private static void VerifyRoutableDescendantPartitionRestore()
    {
        Fixture fixture = new();
        fixture.Publish(quantity: 4);
        FacilityOutputExactRouteRequest request = fixture.Request(
            "route:qa:routable-descendants",
            4,
            "warehouse:qa:food",
            new Vector2Int(19, 7),
            ComponentFingerprint);
        Require(
            fixture.Route.TryRoute(
                request,
                out FacilityOutputExactRouteReceipt receipt,
                out FacilityOutputExactRouteFailure failure)
            && fixture.Route.TryAcknowledge(
                receipt.RouteOperationId,
                receipt.PhysicalReceiptFingerprint,
                out _,
                out failure),
            "Routable descendant fixture failed: " + failure.Reason);

        List<WorldItemStackSaveData> physical = fixture.CapturePhysical()
            .ToList();
        FacilityOutputExactRouteSliceReceipt splitSlice = receipt.Slices
            .First(value => value.RoutedQuantity == 2);
        WorldItemStackSaveData carried = physical.Single(value =>
            value.stackId == splitSlice.RoutedStackId);
        FacilityOutputExactRouteCustodyMetadata storedPart = default;
        FacilityOutputExactRouteCustodyMetadata carriedPart = default;
        Require(
            FacilityOutputExactRouteCustodyCodec.TryRead(
                carried.components,
                out FacilityOutputExactRouteCustodyMetadata whole)
            && whole.TryPartitionRoutablePrefix(
                carried.stackId,
                1,
                1_000L,
                1_000L,
                out storedPart,
                out carriedPart),
            "Routable custody could not be partitioned for restore QA.");

        carried.quantity = 1;
        carried.state = WorldItemStackState.Carried;
        carried.destinationId = "character:qa:carrier";
        carried.hasDestinationPosition = true;
        carried.destinationGridX = 12;
        carried.destinationGridY = 7;
        carried.components = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(carried.components, carriedPart);
        WorldItemStackSaveData stored = ClonePhysical(carried);
        stored.stackId = "world-item-stack:qa:routable-descendant";
        stored.state = WorldItemStackState.Stored;
        stored.destinationId = receipt.TargetDestinationId;
        stored.hasDestinationPosition = false;
        stored.components = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(stored.components, storedPart);
        physical.Add(stored);

        FacilityOutputExactRouteSliceReceipt recoverySlice = receipt.Slices
            .First(value => value.RoutedStackId != splitSlice.RoutedStackId);
        WorldItemStackSaveData recovery = physical.Single(value =>
            value.stackId == recoverySlice.RoutedStackId);
        recovery.state = WorldItemStackState.Loose;
        recovery.destinationId = receipt.TargetDestinationId;
        recovery.hasDestinationPosition = true;
        recovery.destinationGridX = receipt.TargetPosition.x;
        recovery.destinationGridY = receipt.TargetPosition.y;
        recovery.dropDisposition =
            WorldItemDropDisposition.TransientCarryRecoveryDrop;
        recovery.recoveryOwnerOperationId =
            "haul:character:qa:carrier:000000000001";
        recovery.recoverySourceStackId = recovery.stackId;
        recovery.recoveryCarrierPersistentId = "character:qa:carrier";
        recovery.recoveryInterruptionKind =
            WorldItemCarryInterruptionKind.Downed;
        recovery.droppedAtGameTime = 100d;
        recovery.recoveryDeadlineGameTime = 200d;

        DungeonGameRestoreReport routedRecoveryReport = new();
        PhysicalItemSaveValidation.ValidateRecoveryDrop(
            recovery,
            recovery.stackId,
            routedRecoveryReport);
        Require(
            routedRecoveryReport.Success,
            "Generic physical validation rejected an exact-route recovery destination: "
            + string.Join(" | ", routedRecoveryReport.Errors));
        WorldItemStackSaveData ordinaryRecovery = ClonePhysical(recovery);
        ordinaryRecovery.stackId = "world-item-stack:qa:ordinary-recovery";
        ordinaryRecovery.components = new List<ItemInstanceComponentSaveData>();
        DungeonGameRestoreReport ordinaryRecoveryReport = new();
        PhysicalItemSaveValidation.ValidateRecoveryDrop(
            ordinaryRecovery,
            ordinaryRecovery.stackId,
            ordinaryRecoveryReport);
        Require(
            !ordinaryRecoveryReport.Success,
            "Generic physical validation accepted a non-routed recovery destination.");

        FacilityOutputExactRouteService restored = fixture.CreateRouteService();
        FacilityOutputExactRouteRestoreCandidate candidate =
            restored.BuildRestoreCandidate(
                fixture.Route.CaptureOutbox(),
                physical);
        Require(candidate != null,
            "Routable carried/stored/recovery descendants failed restore join.");

        stored.components = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(stored.components, carriedPart);
        bool rejectedOverlap = false;
        try
        {
            restored.BuildRestoreCandidate(
                fixture.Route.CaptureOutbox(),
                physical);
        }
        catch (InvalidOperationException)
        {
            rejectedOverlap = true;
        }
        Require(rejectedOverlap,
            "Overlapping routed descendant custody was accepted on restore.");
    }

    private static WorldItemStackSaveData ClonePhysical(
        WorldItemStackSaveData source) => new()
    {
        stackId = source.stackId,
        itemInstanceId = source.itemInstanceId,
        itemId = source.itemId,
        quantity = source.quantity,
        state = source.state,
        gridX = source.gridX,
        gridY = source.gridY,
        reservedByPersistentId = source.reservedByPersistentId,
        destinationId = source.destinationId,
        aggregationCohortId = source.aggregationCohortId,
        sourceStorageDestinationId = source.sourceStorageDestinationId,
        hasDestinationPosition = source.hasDestinationPosition,
        destinationGridX = source.destinationGridX,
        destinationGridY = source.destinationGridY,
        forbidden = source.forbidden,
        components = source.components.Select(value => value.Clone()).ToList(),
        dropDisposition = source.dropDisposition,
        recoveryOwnerOperationId = source.recoveryOwnerOperationId,
        recoverySourceStackId = source.recoverySourceStackId,
        recoveryCarrierPersistentId = source.recoveryCarrierPersistentId,
        recoveryInterruptionKind = source.recoveryInterruptionKind,
        droppedAtGameTime = source.droppedAtGameTime,
        recoveryDeadlineGameTime = source.recoveryDeadlineGameTime
    };

    private static void VerifyDeliveryRevisionOverlayRoundTripAndTamperGuards()
    {
        Fixture fixture = new();
        fixture.Publish(quantity: 5);
        FacilityOutputExactRouteRequest request = fixture.Request(
            "route:qa:delivery-overlay",
            quantity: 5,
            targetDestinationId: "warehouse:qa:food",
            targetPosition: new Vector2Int(17, 9),
            componentFingerprint: ComponentFingerprint);
        Require(fixture.Route.TryRoute(
                request,
                out FacilityOutputExactRouteReceipt receipt,
                out FacilityOutputExactRouteFailure failure)
            && fixture.Route.TryAcknowledge(
                receipt.RouteOperationId,
                receipt.PhysicalReceiptFingerprint,
                out _,
                out _),
            "Delivery overlay fixture route failed: " + failure.Reason);

        FacilityOutputExactRouteOutboxSaveData route =
            fixture.Route.CaptureOutbox().Single();
        string expectedFingerprint =
            FacilityOutputExactRouteDeliveryRevisionFingerprint.CreateInitial(
                request.RouteOperationId,
                request.RequestFingerprint,
                receipt.PhysicalReceiptFingerprint,
                request.TargetDestinationId,
                request.TargetPosition.x,
                request.TargetPosition.y);
        Require(route.currentDeliveryRevision == 0L
            && route.currentDeliveryRevisionFingerprint == expectedFingerprint
            && route.currentDeliveryRerouteOperationId.Length == 0
            && route.currentTargetDestinationId == request.TargetDestinationId
            && route.currentTargetPositionX == request.TargetPosition.x
            && route.currentTargetPositionY == request.TargetPosition.y
            && route.currentTargetAuthorityFingerprint.Length == 0,
            "Initial delivery overlay did not round-trip through the Items outbox.");

        List<WorldItemStackSaveData> physical = fixture.CapturePhysical().ToList();
        WorldItemStackSaveData[] descendants = physical
            .Where(value => value.state == WorldItemStackState.Loose)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        Require(descendants.Length == receipt.Slices.Count
            && descendants.All(value =>
                FacilityOutputExactRouteCustodyCodec.TryRead(
                    value.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                && metadata.CurrentDeliveryRevision == 0L
                && metadata.CurrentDeliveryRevisionFingerprint
                    == expectedFingerprint
                && metadata.CurrentTargetDestinationId
                    == request.TargetDestinationId
                && metadata.CurrentTargetPosition == request.TargetPosition),
            "Initial delivery overlay was not identical on every descendant.");
        FacilityOutputExactRouteService restored = fixture.CreateRouteService();
        restored.BuildRestoreCandidate(new[] { route }, physical);

        FacilityOutputExactRouteOutboxSaveData tamperedOutbox = route.Clone();
        tamperedOutbox.currentDeliveryRevisionFingerprint = new string('9', 64);
        bool outboxRejected = false;
        try
        {
            restored.BuildRestoreCandidate(new[] { tamperedOutbox }, physical);
        }
        catch (InvalidOperationException)
        {
            outboxRejected = true;
        }
        Require(outboxRejected,
            "Tampered current delivery revision fingerprint was restored.");

        List<WorldItemStackSaveData> tamperedPhysical = physical
            .Select(ClonePhysical)
            .ToList();
        ItemInstanceComponentSaveData custody = tamperedPhysical
            .Where(value => value.state == WorldItemStackState.Loose)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .First()
            .components.Single(FacilityOutputExactRouteCustodyCodec.IsCustody);
        custody.values.Single(value => string.Equals(
                value.key,
                "current-delivery-revision-fingerprint",
                StringComparison.Ordinal))
            .stringValue = new string('8', 64);
        bool descendantRejected = false;
        try
        {
            restored.BuildRestoreCandidate(new[] { route }, tamperedPhysical);
        }
        catch (InvalidOperationException)
        {
            descendantRejected = true;
        }
        Require(descendantRejected,
            "Descendant delivery overlay mismatch was restored.");
    }

    private static void VerifyDeliveryOverlayParticipantLifecycle()
    {
        Fixture fixture = new();
        fixture.Publish(quantity: 5);
        FacilityOutputExactRouteRequest request = fixture.Request(
            "route:qa:delivery-participant",
            quantity: 5,
            targetDestinationId: "warehouse:qa:origin",
            targetPosition: new Vector2Int(11, 6),
            componentFingerprint: ComponentFingerprint);
        Require(fixture.Route.TryRoute(request, out var receipt, out var failure)
            && fixture.Route.TryAcknowledge(
                receipt.RouteOperationId,
                receipt.PhysicalReceiptFingerprint,
                out _,
                out _),
            "Delivery participant fixture route failed: " + failure.Reason);
        IFacilityOutputExactRouteDeliveryOverlayParticipant participant =
            fixture.Route;
        FacilityOutputExactRouteDeliveryRevisionSnapshot initial =
            participant.CaptureCurrentDelivery(request.RouteOperationId);

        IFacilityOutputExactRouteDeliveryOverlayCandidate fault = participant
            .PrepareDeliveryOverlay(
                request.RouteOperationId,
                initial.Revision,
                initial.RevisionFingerprint,
                receipt.PhysicalReceiptFingerprint,
                1L,
                new string('b', 64),
                "production-output-delivery-reroute:qa:one",
                "warehouse:qa:rerouted",
                19,
                7,
                new string('c', 64));
        Require(fault.Status == FacilityOutputExactRouteDeliveryOverlayStatus.Prepared,
            "Stable Loose delivery overlay did not prepare.");
        int routedDescendantCount = fixture.Repository.Records.Count(value =>
            FacilityOutputExactRouteCustodyCodec.TryRead(
                value.components,
                out FacilityOutputExactRouteCustodyMetadata metadata)
            && metadata.RouteOperationId == receipt.RouteOperationId);
        Require(routedDescendantCount > 1
            && fault.DeliverySubjects.Count == routedDescendantCount
            && fault.DeliverySubjects.Sum(value => value.Quantity)
                == receipt.TotalQuantity
            && fault.DeliverySubjects.Sum(value => value.ExactMassGrams)
                == receipt.TotalMassGrams
            && fault.DeliverySubjects.All(value =>
                value.RouteOperationId == receipt.RouteOperationId
                && value.PhysicalReceiptFingerprint
                    == receipt.PhysicalReceiptFingerprint
                && value.ComponentFingerprint == ComponentFingerprint),
            "Delivery overlay admission subjects did not cover the exact physical partition.");
        IPreparedOutputCheckpointGcParticipant gc = fixture.Route;
        PreparedOutputCheckpointGcResult gcWhileDeliveryActive =
            gc.PrepareCheckpointGarbageCollection(
                new PreparedOutputCheckpointGcContext(
                    1L,
                    new string('a', 64),
                    "slot:qa:delivery-active"),
                out _);
        Require(gcWhileDeliveryActive.Status ==
                PreparedOutputCheckpointGcStatus.Deferred,
            "Checkpoint GC did not defer while a delivery overlay was active.");
        fixture.Route.FailNextDeliveryOverlayAfterCustodySwapForEditorTest();
        bool injectedRejected = false;
        try { participant.PublishDeliveryOverlay(fault); }
        catch (InvalidOperationException) { injectedRejected = true; }
        Require(injectedRejected
            && participant.CaptureCurrentDelivery(request.RouteOperationId)
                .Revision == 0L
            && fixture.Repository.Records.All(value =>
                value.destinationId == request.TargetDestinationId
                && FacilityOutputExactRouteCustodyCodec.TryRead(
                    value.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                && metadata.CurrentDeliveryRevision == 0L),
            "Injected delivery overlay failure was not rolled back atomically.");
        participant.RollbackDeliveryOverlay(fault);

        IFacilityOutputExactRouteDeliveryOverlayCandidate prepared = participant
            .PrepareDeliveryOverlay(
                request.RouteOperationId,
                initial.Revision,
                initial.RevisionFingerprint,
                receipt.PhysicalReceiptFingerprint,
                1L,
                new string('b', 64),
                "production-output-delivery-reroute:qa:one",
                "warehouse:qa:rerouted",
                19,
                7,
                new string('c', 64));
        participant.PublishDeliveryOverlay(prepared);
        participant.CompleteDeliveryOverlay(prepared);
        FacilityOutputExactRouteOutboxSaveData published =
            fixture.Route.CaptureOutbox().Single();
        Require(published.targetDestinationId == request.TargetDestinationId
            && published.physicalReceiptFingerprint == receipt.PhysicalReceiptFingerprint
            && published.currentDeliveryRevision == 1L
            && published.currentTargetDestinationId == "warehouse:qa:rerouted"
            && fixture.Repository.Records
                .Where(value => value.state == WorldItemStackState.Loose)
                .All(value => FacilityOutputExactRouteCustodyCodec.TryRead(
                        value.components,
                        out FacilityOutputExactRouteCustodyMetadata metadata)
                    && metadata.CurrentDeliveryRevision == 1L),
            "Delivery overlay publish mutated immutable receipt or missed descendants.");

        IFacilityOutputExactRouteDeliveryOverlayCandidate replay = participant
            .PrepareDeliveryOverlay(
                request.RouteOperationId,
                initial.Revision,
                initial.RevisionFingerprint,
                receipt.PhysicalReceiptFingerprint,
                1L,
                new string('b', 64),
                "production-output-delivery-reroute:qa:one",
                "warehouse:qa:rerouted",
                19,
                7,
                new string('c', 64));
        Require(replay.Status == FacilityOutputExactRouteDeliveryOverlayStatus.Replay,
            "Identical delivery overlay was not replay-safe.");
        participant.PublishDeliveryOverlay(replay);
        participant.CompleteDeliveryOverlay(replay);

        WorldItemStackRecord unstable = fixture.Repository.Records.First(
            value => value.state == WorldItemStackState.Loose);
        unstable.state = WorldItemStackState.Stored;
        unstable.hasDestinationPosition = false;
        unstable.destinationPosition = default;
        fixture.Repository.MarkChanged();
        IFacilityOutputExactRouteDeliveryOverlayCandidate deferred = participant
            .PrepareDeliveryOverlay(
                request.RouteOperationId,
                1L,
                new string('b', 64),
                receipt.PhysicalReceiptFingerprint,
                2L,
                new string('f', 64),
                "production-output-delivery-reroute:qa:two",
                "warehouse:qa:next",
                23,
                9,
                new string('1', 64));
        Require(deferred.Status ==
                FacilityOutputExactRouteDeliveryOverlayStatus.Deferred
            && deferred.Reason ==
                FacilityOutputExactRouteDeliveryOverlayReason
                    .PhysicalStateNotStable,
            "Stored custody did not return typed delivery-overlay deferral.");
        unstable.state = WorldItemStackState.Loose;
        unstable.hasDestinationPosition = true;
        unstable.destinationPosition = new Vector2Int(19, 7);
        fixture.Repository.MarkChanged();

        bool conflictRejected = false;
        try
        {
            participant.PrepareDeliveryOverlay(
                request.RouteOperationId,
                initial.Revision,
                initial.RevisionFingerprint,
                receipt.PhysicalReceiptFingerprint,
                1L,
                new string('d', 64),
                "production-output-delivery-reroute:qa:conflict",
                "warehouse:qa:other",
                21,
                8,
                new string('e', 64));
        }
        catch (InvalidOperationException) { conflictRejected = true; }
        Require(conflictRejected,
            "Conflicting delivery overlay replay was accepted.");

        fixture.Route.BeginRestoreCandidate();
        IFacilityOutputExactRouteDeliveryOverlayCandidate restoreDeferred =
            participant.PrepareDeliveryOverlay(
                request.RouteOperationId,
                1L,
                new string('b', 64),
                receipt.PhysicalReceiptFingerprint,
                2L,
                new string('f', 64),
                "production-output-delivery-reroute:qa:restore-busy",
                "warehouse:qa:restore-next",
                25,
                10,
                new string('1', 64));
        Require(restoreDeferred.Status ==
                FacilityOutputExactRouteDeliveryOverlayStatus.Deferred
            && restoreDeferred.Reason ==
                FacilityOutputExactRouteDeliveryOverlayReason.AuthorityBusy,
            "Delivery overlay did not defer during restore staging.");
        fixture.Route.DiscardRestoreCandidate();

        PreparedOutputCheckpointGcResult preparedGc =
            gc.PrepareCheckpointGarbageCollection(
                new PreparedOutputCheckpointGcContext(
                    1L,
                    new string('2', 64),
                    "slot:qa:gc-active"),
                out IPreparedOutputCheckpointGcCandidate gcCandidate);
        Require(preparedGc.Status == PreparedOutputCheckpointGcStatus.Applied
            && gcCandidate != null,
            "Checkpoint GC mutual-exclusion fixture did not prepare.");
        IFacilityOutputExactRouteDeliveryOverlayCandidate gcDeferred =
            participant.PrepareDeliveryOverlay(
                request.RouteOperationId,
                1L,
                new string('b', 64),
                receipt.PhysicalReceiptFingerprint,
                2L,
                new string('f', 64),
                "production-output-delivery-reroute:qa:gc-busy",
                "warehouse:qa:gc-next",
                27,
                11,
                new string('1', 64));
        Require(gcDeferred.Status ==
                FacilityOutputExactRouteDeliveryOverlayStatus.Deferred
            && gcDeferred.Reason ==
                FacilityOutputExactRouteDeliveryOverlayReason.AuthorityBusy,
            "Delivery overlay did not defer while checkpoint GC was active.");
        gc.CompleteCheckpointGarbageCollection(gcCandidate);
    }

    private static void VerifyWarehouseFallbackRecoveryRestore()
    {
        Fixture fixture = new();
        fixture.Publish(quantity: 2);
        FacilityOutputExactRouteRequest request = fixture.Request(
            "route:qa:warehouse-fallback-recovery",
            2,
            string.Empty,
            new Vector2Int(21, 8),
            ComponentFingerprint);
        Require(
            fixture.Route.TryRoute(
                request,
                out FacilityOutputExactRouteReceipt receipt,
                out FacilityOutputExactRouteFailure failure)
            && fixture.Route.TryAcknowledge(
                receipt.RouteOperationId,
                receipt.PhysicalReceiptFingerprint,
                out _,
                out failure),
            "Warehouse fallback recovery fixture failed: " + failure.Reason);

        List<WorldItemStackSaveData> physical = fixture.CapturePhysical()
            .ToList();
        WorldItemStackSaveData recovery = physical.Single(value =>
            value.stackId == receipt.Slices.Single().RoutedStackId);
        recovery.state = WorldItemStackState.Loose;
        recovery.destinationId = "warehouse:qa:fallback";
        recovery.hasDestinationPosition = true;
        recovery.destinationGridX = 21;
        recovery.destinationGridY = 8;
        recovery.dropDisposition =
            WorldItemDropDisposition.TransientCarryRecoveryDrop;
        recovery.recoveryOwnerOperationId =
            "haul:character:qa:carrier:000000000002";
        recovery.recoverySourceStackId = recovery.stackId;
        recovery.recoveryCarrierPersistentId = "character:qa:carrier";
        recovery.recoveryInterruptionKind =
            WorldItemCarryInterruptionKind.Downed;
        recovery.droppedAtGameTime = 100d;
        recovery.recoveryDeadlineGameTime = 200d;
        Require(
            FacilityOutputExactRouteCustodyCodec.TryRead(
                recovery.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            && custody.TargetDestinationId.Length == 0,
            "Warehouse fallback fixture unexpectedly authored a custody target.");

        FacilityOutputExactRouteService restored = fixture.CreateRouteService();
        Require(
            restored.BuildRestoreCandidate(
                fixture.Route.CaptureOutbox(),
                physical) != null,
            "Saved warehouse fallback intent did not restore exact-route recovery custody.");
    }

    private static void VerifyCheckpointGcStablePublishRollbackAndRestore()
    {
        const string digest =
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        Fixture fault = CreateRoutableFixture(
            "route:qa:gc-fault",
            WorldItemStackState.Loose,
            out FacilityOutputExactRouteReceipt faultReceipt);
        PreparedOutputCheckpointGcContext context = new(1L, digest, "slot:qa");
        IPreparedOutputCheckpointGcParticipant faultGc = fault.Route;
        PreparedOutputCheckpointGcResult prepared = faultGc
            .PrepareCheckpointGarbageCollection(context, out var candidate);
        Require(prepared.Status == PreparedOutputCheckpointGcStatus.Applied
            && prepared.CollectedBatchCount == 1
            && candidate.BatchCommitIds.SequenceEqual(new[] { BatchId })
            && candidate.RouteOperationIds.SequenceEqual(
                new[] { faultReceipt.RouteOperationId }),
            "Stable whole-batch checkpoint GC was not prepared exactly.");
        fault.Route.FailNextCheckpointGcAfterCustodyStripForEditorTest();
        PreparedOutputCheckpointGcResult faulted = faultGc
            .PublishCheckpointGarbageCollection(candidate);
        Require(faulted.Status == PreparedOutputCheckpointGcStatus.Deferred
            && faulted.Reason ==
                PreparedOutputCheckpointGcReason.ParticipantPublishFailed
            && fault.Route.CaptureOutbox().Count == 1
            && fault.Repository.Records.All(record =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    record.components)),
            "Injected GC publish fault did not restore custody and outbox authority.");

        ItemInstanceComponentSaveData business = new()
        {
            componentTypeId = "item-state:qa-gc-business",
            schemaVersion = 1,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = "lot",
                    kind = ItemStateValueKind.String,
                    stringValue = "preserve"
                }
            }
        };
        Fixture stable = new();
        stable.Publish(2, new[] { business });
        FacilityOutputExactRouteRequest request = stable.Request(
            "route:qa:gc-stored",
            2,
            "warehouse:qa:food",
            new Vector2Int(24, 9),
            ComponentFingerprint);
        Require(stable.Route.TryRoute(request, out var receipt, out _)
            && stable.Route.TryAcknowledge(
                receipt.RouteOperationId,
                receipt.PhysicalReceiptFingerprint,
                out _,
                out _),
            "Stored GC fixture did not become Routable.");
        WorldItemStackRecord stored = stable.Repository.Records.Single();
        stored.state = WorldItemStackState.Stored;
        stored.destinationId = request.TargetDestinationId;
        stored.hasDestinationPosition = false;
        stored.destinationPosition = default;
        stable.Repository.MarkChanged();
        GcPhysicalSnapshot before = new(
            stored.stackId,
            stored.itemId,
            stored.quantity,
            stored.state,
            stored.position,
            stored.destinationId);
        IPreparedOutputCheckpointGcParticipant stableGc = stable.Route;
        prepared = stableGc.PrepareCheckpointGarbageCollection(
            context,
            out candidate);
        PreparedOutputCheckpointGcResult published = stableGc
            .PublishCheckpointGarbageCollection(candidate);
        Require(prepared.Status == PreparedOutputCheckpointGcStatus.Applied
            && published.Status == PreparedOutputCheckpointGcStatus.Applied
            && stable.Route.CaptureOutbox().Count == 0
            && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                stored.components)
            && stored.components.Count(component => string.Equals(
                component.componentTypeId,
                business.componentTypeId,
                StringComparison.Ordinal)) == 1
            && before.Matches(
                stored.stackId,
                stored.itemId,
                stored.quantity,
                stored.state,
                stored.position,
                stored.destinationId),
            "Checkpoint GC changed stored business payload or physical identity.");

        FacilityOutputExactRouteService restored = stable.CreateRouteService();
        FacilityOutputExactRouteRestoreCandidate restoreCandidate = restored
            .BuildRestoreCandidate(
                stable.Route.CaptureOutbox(),
                stable.CapturePhysical(),
                1L,
                digest);
        restored.BeginRestoreCandidate();
        restored.RestoreCandidate(restoreCandidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(restored.LastConfirmedCheckpointSequence == 1L
            && restored.LastConfirmedCheckpointDigest == digest,
            "Items checkpoint sequence/digest did not survive exact restore publication.");
    }

    private static void VerifyCheckpointGcPhysicalDeferral()
    {
        const string digest =
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        PreparedOutputCheckpointGcContext context = new(1L, digest, "slot:qa");
        Fixture carried = CreateRoutableFixture(
            "route:qa:gc-carried",
            WorldItemStackState.Carried,
            out _);
        IPreparedOutputCheckpointGcParticipant carriedGc = carried.Route;
        PreparedOutputCheckpointGcResult result = carriedGc
            .PrepareCheckpointGarbageCollection(context, out var candidate);
        Require(result.Status == PreparedOutputCheckpointGcStatus.Deferred
            && result.Reason ==
                PreparedOutputCheckpointGcReason.PhysicalStateNotStable
            && candidate == null,
            "Carried Routable custody was not deferred as one whole batch.");

        Fixture reserved = CreateRoutableFixture(
            "route:qa:gc-reserved",
            WorldItemStackState.Loose,
            out _);
        reserved.Repository.Records.Single().reservedQuantity = 1;
        reserved.Repository.MarkChanged();
        IPreparedOutputCheckpointGcParticipant reservedGc = reserved.Route;
        result = reservedGc.PrepareCheckpointGarbageCollection(
            context,
            out candidate);
        Require(result.Status == PreparedOutputCheckpointGcStatus.Deferred
            && result.Reason ==
                PreparedOutputCheckpointGcReason.PhysicalStateNotStable
            && candidate == null,
            "Reserved Routable custody was not deferred as one whole batch.");
    }

    private static void VerifyCheckpointGcCoverageReplayAndStaleGuards()
    {
        const string digest =
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        PreparedOutputCheckpointGcContext context = new(1L, digest, "slot:qa");
        Fixture gap = CreateRoutableFixture(
            "route:qa:gc-gap",
            WorldItemStackState.Loose,
            out _);
        WorldItemStackRecord gapRecord = gap.Repository.Records.Single();
        Require(FacilityOutputExactRouteCustodyCodec.TryRead(
                gapRecord.components,
                out FacilityOutputExactRouteCustodyMetadata gapMetadata),
            "Gap fixture lost custody.");
        gapRecord.components = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(
                gapRecord.components,
                gapMetadata.WithSlice(
                    gapMetadata.Phase,
                    gapMetadata.TargetDestinationId,
                    gapMetadata.CurrentSourceStackId,
                    gapMetadata.SourceOffsetQuantity + 1,
                    gapMetadata.Quantity,
                    gapMetadata.MassGrams,
                    gapMetadata.RouteOperationId,
                    gapMetadata.RequestFingerprint,
                    gapMetadata.PhysicalReceiptFingerprint));
        gap.Repository.MarkChanged();
        IPreparedOutputCheckpointGcParticipant gapGc = gap.Route;
        PreparedOutputCheckpointGcResult result = gapGc
            .PrepareCheckpointGarbageCollection(context, out var candidate);
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
            && result.Reason ==
                PreparedOutputCheckpointGcReason.PartialAuthorityCoverage
            && candidate == null,
            "Checkpoint GC accepted a gap in routed descendant coverage.");

        Fixture grams = CreateRoutableFixture(
            "route:qa:gc-gram-mismatch",
            WorldItemStackState.Loose,
            out _);
        WorldItemStackRecord gramRecord = grams.Repository.Records.Single();
        Require(FacilityOutputExactRouteCustodyCodec.TryRead(
                gramRecord.components,
                out FacilityOutputExactRouteCustodyMetadata gramMetadata),
            "Gram fixture lost custody.");
        gramRecord.components = FacilityOutputExactRouteCustodyCodec
            .ReplaceAuthority(
                gramRecord.components,
                gramMetadata.WithSlice(
                    gramMetadata.Phase,
                    gramMetadata.TargetDestinationId,
                    gramMetadata.CurrentSourceStackId,
                    gramMetadata.SourceOffsetQuantity,
                    gramMetadata.Quantity,
                    gramMetadata.MassGrams - 1L,
                    gramMetadata.RouteOperationId,
                    gramMetadata.RequestFingerprint,
                    gramMetadata.PhysicalReceiptFingerprint));
        grams.Repository.MarkChanged();
        IPreparedOutputCheckpointGcParticipant gramGc = grams.Route;
        result = gramGc.PrepareCheckpointGarbageCollection(
            context,
            out candidate);
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
            && result.Reason ==
                PreparedOutputCheckpointGcReason.PartialAuthorityCoverage
            && candidate == null,
            "Checkpoint GC accepted a one-gram custody mismatch.");

        Fixture stale = CreateRoutableFixture(
            "route:qa:gc-stale",
            WorldItemStackState.Loose,
            out _);
        IPreparedOutputCheckpointGcParticipant staleGc = stale.Route;
        Require(staleGc.PrepareCheckpointGarbageCollection(
                context,
                out candidate).Status == PreparedOutputCheckpointGcStatus.Applied,
            "Stale fixture did not prepare.");
        stale.Repository.MarkChanged();
        result = staleGc.PublishCheckpointGarbageCollection(candidate);
        Require(result.Status == PreparedOutputCheckpointGcStatus.Deferred
            && result.Reason ==
                PreparedOutputCheckpointGcReason.LiveAuthorityChanged
            && stale.Route.CaptureOutbox().Count == 1,
            "Repository revision drift mutated a prepared GC candidate.");

        Fixture replay = CreateRoutableFixture(
            "route:qa:gc-replay",
            WorldItemStackState.Loose,
            out _);
        IPreparedOutputCheckpointGcParticipant replayGc = replay.Route;
        Require(replayGc.PrepareCheckpointGarbageCollection(
                context,
                out candidate).Status == PreparedOutputCheckpointGcStatus.Applied
            && replayGc.PublishCheckpointGarbageCollection(candidate).Status
                == PreparedOutputCheckpointGcStatus.Applied,
            "Replay fixture did not publish checkpoint GC.");
        Require(replayGc.PrepareCheckpointGarbageCollection(
                context,
                out _).Status == PreparedOutputCheckpointGcStatus.AlreadyApplied
            && replayGc.PrepareCheckpointGarbageCollection(
                new PreparedOutputCheckpointGcContext(
                    1L,
                    "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                    "slot:qa"),
                out _).Reason ==
                PreparedOutputCheckpointGcReason.ReplayDigestMismatch,
            "Checkpoint replay digest guard failed.");
    }

    private static Fixture CreateRoutableFixture(
        string operationId,
        WorldItemStackState state,
        out FacilityOutputExactRouteReceipt receipt)
    {
        Fixture fixture = new();
        fixture.Publish(2);
        FacilityOutputExactRouteRequest request = fixture.Request(
            operationId,
            2,
            "warehouse:qa:food",
            new Vector2Int(23, 9),
            ComponentFingerprint);
        Require(fixture.Route.TryRoute(request, out receipt, out _)
            && fixture.Route.TryAcknowledge(
                receipt.RouteOperationId,
                receipt.PhysicalReceiptFingerprint,
                out _,
                out _),
            "Checkpoint GC fixture did not become Routable.");
        WorldItemStackRecord record = fixture.Repository.Records.Single();
        record.state = state;
        if (state is WorldItemStackState.Carried
            or WorldItemStackState.InTransit)
        {
            record.destinationId = "character:qa:carrier";
            record.hasDestinationPosition = true;
            record.destinationPosition = request.TargetPosition;
        }
        fixture.Repository.MarkChanged();
        return fixture;
    }

    private static void VerifyCheckpointGcApiAndRawSaveGuards()
    {
        string complete =
            "{\"version\":13,\"lastConfirmedExactRouteCheckpointSequence\":0,"
            + "\"lastConfirmedExactRouteCheckpointDigest\":\"\","
            + "\"pendingExactOutputRoutes\":[]}";
        Require(HasRawJsonProperty(
                complete,
                "lastConfirmedExactRouteCheckpointSequence")
            && HasRawJsonProperty(
                complete,
                "lastConfirmedExactRouteCheckpointDigest"),
            "Complete V13 physical save fixture is missing checkpoint scalars.");
        bool missingRejected = false;
        string missingDigest =
            "{\"version\":13,"
            + "\"lastConfirmedExactRouteCheckpointSequence\":0,"
            + "\"pendingExactOutputRoutes\":[]}";
        missingRejected = !HasRawJsonProperty(
            missingDigest,
            "lastConfirmedExactRouteCheckpointDigest");
        Require(missingRejected,
            "V13 physical save accepted a missing checkpoint digest scalar.");

        string[] mutators =
        {
            nameof(IPreparedOutputCheckpointGcParticipant
                .PrepareCheckpointGarbageCollection),
            nameof(IPreparedOutputCheckpointGcParticipant
                .PublishCheckpointGarbageCollection),
            nameof(IPreparedOutputCheckpointGcParticipant
                .RollbackCheckpointGarbageCollection),
            nameof(IPreparedOutputCheckpointGcParticipant
                .CompleteCheckpointGarbageCollection)
        };
        Require(mutators.All(name => typeof(FacilityOutputExactRouteService)
                .GetMethod(name) == null),
            "Items checkpoint GC mutator leaked as a public concrete API.");
    }

    private static bool HasRawJsonProperty(string json, string propertyName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            return false;
        string token = "\"" + propertyName + "\"";
        return json.IndexOf(token, StringComparison.Ordinal) >= 0;
    }

    private sealed class GcPhysicalSnapshot
    {
        internal GcPhysicalSnapshot(
            string stackId,
            string itemId,
            int quantity,
            WorldItemStackState state,
            Vector2Int position,
            string destinationId)
        {
            StackId = stackId;
            ItemId = itemId;
            Quantity = quantity;
            State = state;
            Position = position;
            DestinationId = destinationId;
        }

        private string StackId { get; }
        private string ItemId { get; }
        private int Quantity { get; }
        private WorldItemStackState State { get; }
        private Vector2Int Position { get; }
        private string DestinationId { get; }

        internal bool Matches(
            string stackId,
            string itemId,
            int quantity,
            WorldItemStackState state,
            Vector2Int position,
            string destinationId) =>
            StackId == stackId
            && ItemId == itemId
            && Quantity == quantity
            && State == state
            && Position == position
            && DestinationId == destinationId;
    }

    private sealed class Fixture
    {
        internal const string SourceDestination = "production:qa:output";
        internal static readonly Vector2Int SourcePosition = new(9, 4);
        private readonly FakeCatalog catalog = new();
        private readonly FakeMassQuery mass = new();
        private readonly FacilityBufferMassAdmissionService admission;
        private readonly FacilityBufferPlannedOutputPublicationService publication;

        internal Fixture()
        {
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            FacilityBufferDestinationClaimRegistry claims = new();
            Require(
                claims.TryClaim(
                    new FacilityBufferDestinationClaim(
                        SourceDestination,
                        SourcePosition,
                        "production.generic",
                        SourceDestination,
                        "building:qa:feedbench",
                        FacilityBufferDestinationAnchorKind.LiveBuilding),
                    out _,
                    out _),
                "Exact-route fixture could not claim the source buffer.");
            admission = new FacilityBufferMassAdmissionService(
                claims,
                new EmptyOccupancy(),
                mass);
            Require(
                admission.TryReplaceOwnedProfiles(
                    "production.generic",
                    new[]
                    {
                        new FacilityBufferCapacityProfile(
                            SourceDestination,
                            SourcePosition,
                            "production.generic",
                            SourceDestination,
                            "building:qa:feedbench",
                            new PhysicalMassGrams(10_000L),
                            1L)
                    },
                    out _,
                    out _),
                "Exact-route fixture could not publish source capacity.");
            publication = new FacilityBufferPlannedOutputPublicationService(
                Repository,
                catalog,
                mass,
                admission);
            Route = CreateRouteService();
        }

        internal WorldItemRepository Repository { get; }
        internal FacilityOutputExactRouteService Route { get; }

        internal FacilityOutputExactRouteService CreateRouteService() => new(
            Repository,
            mass,
            EditorNullItemMarkerPresenter.Instance);

        internal void Publish(
            int quantity,
            IReadOnlyList<ItemInstanceComponentSaveData> runtimeComponents = null)
        {
            IReadOnlyList<ItemInstanceComponentSaveData> components =
                runtimeComponents ?? Array.Empty<ItemInstanceComponentSaveData>();
            PhysicalItemMassSubject subject =
                PhysicalItemMassSubjectAdapter.Create(
                    mass,
                    (ItemDefinitionId)ItemId,
                    string.Empty,
                    components);
            FacilityBufferPlannedOutputRequest request = new(
                "publication:qa:" + quantity,
                BatchId,
                "outcome:qa",
                SourceDestination,
                SourcePosition,
                "production.generic",
                SourceDestination,
                "building:qa:feedbench",
                1L,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        LineId,
                        subject,
                        quantity,
                        components,
                        ComponentFingerprint)
                });
            Require(
                admission.TryReservePlannedOutput(
                    request,
                    out FacilityBufferPlannedOutputToken token,
                    out _,
                    out _)
                && publication.TryPublishFullBatch(
                    token,
                    out FacilityBufferPlannedOutputPublicationReceipt receipt,
                    out _,
                    out _)
                && admission.TryCommitPlannedOutput(
                    token,
                    receipt,
                    out _,
                    out _,
                    out _)
                && publication.TryAcknowledgePublishedBatch(
                    receipt,
                    out _,
                    out _),
                "Exact-route fixture could not publish acknowledged output.");
        }

        internal FacilityOutputExactRouteRequest Request(
            string operationId,
            int quantity,
            string targetDestinationId,
            Vector2Int targetPosition,
            string componentFingerprint,
            int sourceOffsetQuantity = 0) => new(
            operationId,
            BatchId,
            SourceDestination,
            targetDestinationId,
            targetPosition,
            new[]
            {
                new FacilityOutputExactRouteSliceRequest(
                    LineId,
                    ProductionPreparedOutputIdentity.BuildLineCommitId(
                        BatchId,
                        LineId),
                    ItemId,
                    sourceOffsetQuantity,
                    quantity,
                    checked(quantity * 1_000L),
                    componentFingerprint)
            });

        internal IReadOnlyList<WorldItemStackSaveData> CapturePhysical() =>
            Repository.Records
                .OrderBy(value => value.stackId, StringComparer.Ordinal)
                .Select(value => new WorldItemStackSaveData
                {
                    stackId = value.stackId,
                    itemInstanceId = value.itemInstanceId,
                    itemId = value.itemId,
                    quantity = value.quantity,
                    state = value.state,
                    gridX = value.position.x,
                    gridY = value.position.y,
                    destinationId = value.destinationId,
                    hasDestinationPosition = value.hasDestinationPosition,
                    destinationGridX = value.destinationPosition.x,
                    destinationGridY = value.destinationPosition.y,
                    components = value.components.Select(component =>
                        component.Clone()).ToList()
                })
                .ToArray();
    }

    private sealed class FakeCatalog : IDungeonItemCatalogProvider
    {
        private readonly DungeonItemDefinition definition = new(
            ItemId,
            "QA Feed",
            string.Empty,
            StockCategory.Food,
            1,
            null,
            1f,
            2);

        public IReadOnlyList<DungeonItemDefinition> All => new[] { definition };
        public DungeonItemDefinition GetDefinition(string itemId) =>
            string.Equals(itemId, ItemId, StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(itemId);
        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition result)
        {
            bool found = string.Equals(itemId, ItemId, StringComparison.Ordinal);
            result = found ? definition : null;
            return found;
        }
    }

    private sealed class FakeMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(1_000L);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(1_000L);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(1_000L);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            new PhysicalMassGrams(1_000L).Multiply(lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new PhysicalMassGrams(1_000L).Multiply(quantity);
    }

    private sealed class EmptyOccupancy : IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(string destinationId) =>
            new(0L, 0L);
        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "not-used";
            return false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
