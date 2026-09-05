using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputExactRouteLifecycleDebugScenarios
{
    private const string RouteOperationId = "route:qa:exact-lifecycle";
    private const string BatchCommitId = "batch:qa:exact-lifecycle";
    private const string LineCommitId = "line:qa:exact-lifecycle";
    private const string OutputLineId = "output:qa:exact-lifecycle";
    private const string ItemId = "resource:qa-exact-lifecycle";
    private const string ComponentFingerprint = "component:qa-exact-lifecycle";
    private const string SourceDestinationId =
        "production-output:building:qa-exact-lifecycle";
    private const string TargetDestinationId =
        "warehouse:qa-exact-lifecycle";

    [MenuItem(
        "DungeonStory/Debug/Economy/Run Prepared Output Exact Route Lifecycle Contracts")]
    public static void RunAll()
    {
        VerifyCompleteLifecycleAndReplay();
        VerifyDeliveryFailureRemainsDeferred();
        VerifyMissingAndDriftConflict();
        VerifyExactQuantityPreservesGramQuantum();
        Debug.Log(
            "Prepared-output exact-route lifecycle contracts passed.");
    }

    private static void VerifyCompleteLifecycleAndReplay()
    {
        List<string> trace = new();
        ProductionPreparedOutputRouteRequestSnapshot initial =
            CreateOperation(ProductionPreparedOutputRoutePhase.PhysicalPending);
        FakeRoutingAuthority routing = new(initial, trace);
        FakeExactRoutePort items = new(trace);
        FakeDeliveryCoordinator delivery = new(routing, trace, succeed: true);
        ProductionPreparedOutputExactRouteLifecycle lifecycle = new(
            routing,
            items,
            delivery);

        ProductionPreparedOutputExactRouteLifecycleResult applied =
            lifecycle.TryProgress(initial);

        Require(
            applied.Status ==
                ProductionPreparedOutputExactRouteLifecycleStatus.Applied
            && applied.Reason ==
                ProductionPreparedOutputExactRouteLifecycleReason.None
            && applied.Completed
            && string.Equals(
                applied.RouteOperationId,
                RouteOperationId,
                StringComparison.Ordinal)
            && IsDigest(applied.PhysicalReceiptFingerprint)
            && string.Equals(
                applied.TargetAuthorityFingerprint,
                FakeDeliveryCoordinator.AuthorityFingerprint,
                StringComparison.Ordinal),
            "The complete exact-route lifecycle did not reach durable delivery authority.");
        Require(
            trace.SequenceEqual(
                new[]
                {
                    "items-route",
                    "economy-commit",
                    "items-ack",
                    "economy-ack",
                    "delivery-authority"
                },
                StringComparer.Ordinal),
            "The exact-route lifecycle crossed its authorities out of order: "
            + string.Join(",", trace));
        Require(
            routing.Current.Phase ==
                ProductionPreparedOutputRoutePhase
                    .ItemsAcknowledgedAwaitingCheckpointGc
            && routing.Current.CurrentDeliveryTargetKind ==
                ProductionPreparedOutputDeliveryTargetKind.InitialExactTarget
            && string.Equals(
                routing.Current.CurrentTargetAuthorityFingerprint,
                FakeDeliveryCoordinator.AuthorityFingerprint,
                StringComparison.Ordinal),
            "The successful lifecycle did not retain its completed delivery authority.");

        int traceCount = trace.Count;
        ProductionPreparedOutputExactRouteLifecycleResult replay =
            lifecycle.TryProgress(initial);
        Require(
            replay.Status ==
                ProductionPreparedOutputExactRouteLifecycleStatus.Replay
            && replay.Completed
            && trace.Count == traceCount,
            "A completed exact-route lifecycle did not replay as a no-op.");
    }

    private static void VerifyDeliveryFailureRemainsDeferred()
    {
        List<string> trace = new();
        ProductionPreparedOutputRouteRequestSnapshot acknowledged =
            CreateOperation(
                ProductionPreparedOutputRoutePhase
                    .ItemsAcknowledgedAwaitingCheckpointGc,
                physicalReceiptFingerprint: Digest('a'));
        FakeRoutingAuthority routing = new(acknowledged, trace);
        FakeDeliveryCoordinator delivery = new(
            routing,
            trace,
            succeed: false);
        ProductionPreparedOutputExactRouteLifecycle lifecycle = new(
            routing,
            new FakeExactRoutePort(trace),
            delivery);

        ProductionPreparedOutputExactRouteLifecycleResult result =
            lifecycle.TryProgress(acknowledged);

        Require(
            result.Status ==
                ProductionPreparedOutputExactRouteLifecycleStatus.Deferred
            && result.Reason ==
                ProductionPreparedOutputExactRouteLifecycleReason
                    .DeliveryAuthorityUnavailable
            && !result.Completed
            && string.IsNullOrEmpty(result.TargetAuthorityFingerprint)
            && routing.Current.Phase ==
                ProductionPreparedOutputRoutePhase
                    .ItemsAcknowledgedAwaitingCheckpointGc
            && string.IsNullOrEmpty(
                routing.Current.CurrentTargetAuthorityFingerprint),
            "Delivery-authority failure incorrectly completed or conflicted the route.");
    }

    private static void VerifyMissingAndDriftConflict()
    {
        ProductionPreparedOutputRouteRequestSnapshot valid =
            CreateOperation(ProductionPreparedOutputRoutePhase.PhysicalPending);
        List<string> missingTrace = new();
        ProductionPreparedOutputExactRouteLifecycle missingLifecycle = new(
            new FakeRoutingAuthority(null, missingTrace),
            new FakeExactRoutePort(missingTrace),
            new FakeDeliveryCoordinator(null, missingTrace, succeed: true));
        ProductionPreparedOutputExactRouteLifecycleResult missing =
            missingLifecycle.TryProgress(valid);
        Require(
            missing.Status ==
                ProductionPreparedOutputExactRouteLifecycleStatus.Conflict
            && missing.Reason ==
                ProductionPreparedOutputExactRouteLifecycleReason
                    .RouteSnapshotMissing,
            "A missing Economy route snapshot was not a typed conflict.");

        List<string> driftTrace = new();
        ProductionPreparedOutputRouteRequestSnapshot drifted =
            CreateOperation(
                ProductionPreparedOutputRoutePhase.PhysicalPending,
                requestFingerprintOverride: Digest('f'));
        FakeRoutingAuthority driftRouting = new(drifted, driftTrace);
        ProductionPreparedOutputExactRouteLifecycle driftLifecycle = new(
            driftRouting,
            new FakeExactRoutePort(driftTrace),
            new FakeDeliveryCoordinator(
                driftRouting,
                driftTrace,
                succeed: true));
        ProductionPreparedOutputExactRouteLifecycleResult drift =
            driftLifecycle.TryProgress(drifted);
        Require(
            drift.Status ==
                ProductionPreparedOutputExactRouteLifecycleStatus.Conflict
            && drift.Reason ==
                ProductionPreparedOutputExactRouteLifecycleReason
                    .RouteSnapshotConflict
            && driftTrace.Count == 0,
            "A drifted route request mutated authority before failing closed.");
    }

    private static void VerifyExactQuantityPreservesGramQuantum()
    {
        ProductionPreparedOutputExactRouteLifecycle lifecycle = new(
            new FakeRoutingAuthority(null, new List<string>()),
            new FakeExactRoutePort(new List<string>()),
            new FakeDeliveryCoordinator(null, new List<string>(), succeed: true));
        ProductionPreparedOutputRoutingLineSnapshot line = new(
            BatchCommitId,
            "bill:qa-exact-lifecycle",
            "recipe:qa-exact-lifecycle",
            "building:qa-exact-lifecycle",
            1,
            LineCommitId,
            OutputLineId,
            ProductionOutputRole.Main,
            ItemId,
            SourceDestinationId,
            ComponentFingerprint,
            ProductionOutputCapabilityIds.StandardDefinition,
            ProductionOutputCapabilityIds.StandardDefinitionVersion,
            ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                OutputLineId,
                ItemId,
                ProductionOutputCapabilityIds.StandardDefinition,
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
            6,
            1000L,
            6,
            1000L,
            0,
            0L);

        int exactQuantity = lifecycle.ResolveExactQuantity(line, 5);
        long numerator = checked(line.OriginalMassGrams * exactQuantity);
        Require(
            exactQuantity == 3
            && numerator % line.OriginalQuantity == 0L
            && numerator / line.OriginalQuantity == 500L
            && lifecycle.ResolveExactQuantity(line, 2) == 0,
            "Exact route quantity did not preserve the indivisible gram quantum.");
    }

    private static ProductionPreparedOutputRouteRequestSnapshot CreateOperation(
        ProductionPreparedOutputRoutePhase phase,
        string physicalReceiptFingerprint = "",
        string requestFingerprintOverride = null)
    {
        const int quantity = 3;
        const long massGrams = 500L;
        FacilityOutputExactRouteRequest request = new(
            RouteOperationId,
            BatchCommitId,
            SourceDestinationId,
            TargetDestinationId,
            new Vector2Int(7, 11),
            new[]
            {
                new FacilityOutputExactRouteSliceRequest(
                    OutputLineId,
                    LineCommitId,
                    ItemId,
                    0,
                    quantity,
                    massGrams,
                    ComponentFingerprint)
            });
        return new ProductionPreparedOutputRouteRequestSnapshot(
            RouteOperationId,
            requestFingerprintOverride ?? request.RequestFingerprint,
            BatchCommitId,
            LineCommitId,
            OutputLineId,
            ItemId,
            ComponentFingerprint,
            SourceDestinationId,
            TargetDestinationId,
            7,
            11,
            0,
            0L,
            quantity,
            massGrams,
            phase,
            physicalReceiptFingerprint,
            0L,
            ProductionPreparedOutputDeliveryTargetKind.InitialExactTarget,
            Digest('b'),
            TargetDestinationId,
            7,
            11,
            string.Empty);
    }

    private static ProductionPreparedOutputRouteRequestSnapshot With(
        ProductionPreparedOutputRouteRequestSnapshot source,
        ProductionPreparedOutputRoutePhase phase,
        string physicalReceiptFingerprint,
        string targetAuthorityFingerprint = "") => new(
        source.RouteOperationId,
        source.RequestFingerprint,
        source.BatchCommitId,
        source.LineCommitId,
        source.OutputLineId,
        source.ItemId,
        source.ComponentFingerprint,
        source.SourceDestinationId,
        source.TargetDestinationId,
        source.TargetPositionX,
        source.TargetPositionY,
        source.SourceOffsetQuantity,
        source.SourceOffsetMassGrams,
        source.RoutedQuantity,
        source.RoutedMassGrams,
        phase,
        physicalReceiptFingerprint,
        string.IsNullOrEmpty(targetAuthorityFingerprint) ? 0L : 1L,
        ProductionPreparedOutputDeliveryTargetKind.InitialExactTarget,
        string.IsNullOrEmpty(targetAuthorityFingerprint)
            ? Digest('b')
            : Digest('c'),
        source.TargetDestinationId,
        source.TargetPositionX,
        source.TargetPositionY,
        targetAuthorityFingerprint);

    private static string Digest(char value) => new(value, 64);

    private static bool IsDigest(string value) =>
        !string.IsNullOrEmpty(value) && value.Length == 64;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakeRoutingAuthority :
        IProductionPreparedOutputRoutingAuthority
    {
        private readonly List<string> trace;
        private ProductionPreparedOutputRouteRequestSnapshot? current;

        internal FakeRoutingAuthority(
            ProductionPreparedOutputRouteRequestSnapshot? current,
            List<string> trace)
        {
            this.current = current;
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        internal ProductionPreparedOutputRouteRequestSnapshot Current =>
            current ?? default;

        public void PublishCommittedBatch(
            ProductionPreparedOutputBatchSaveData completedBatch,
            BuildingInstanceId ownerFacilityId) =>
            throw new NotSupportedException();

        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureAll() =>
            Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>();

        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureBill(ProductionBillId ownerBillId) => CaptureAll();

        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureDestination(string destinationId) => CaptureAll();

        public bool HasOutstandingForBill(ProductionBillId ownerBillId) =>
            current.HasValue;

        public bool CanRetireBill(ProductionBillId ownerBillId) =>
            !current.HasValue;

        public ProductionPreparedOutputRouteRequestSnapshot PrepareRoute(
            string batchCommitId,
            string lineCommitId,
            string targetDestinationId,
            int targetPositionX,
            int targetPositionY,
            int routedQuantity) => throw new NotSupportedException();

        public IReadOnlyList<ProductionPreparedOutputRouteRequestSnapshot>
            CaptureRouteOperations() => current.HasValue
            ? new[] { current.Value }
            : Array.Empty<ProductionPreparedOutputRouteRequestSnapshot>();

        public void CommitPhysicalRoute(
            ProductionPreparedOutputPhysicalRouteReceipt receipt)
        {
            Require(current.HasValue, "Economy commit has no route.");
            ProductionPreparedOutputRouteRequestSnapshot value = current.Value;
            Require(
                value.Phase == ProductionPreparedOutputRoutePhase.PhysicalPending
                && string.Equals(
                    value.RouteOperationId,
                    receipt.RouteOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.RequestFingerprint,
                    receipt.RequestFingerprint,
                    StringComparison.Ordinal)
                && value.RoutedQuantity == receipt.TotalQuantity
                && value.RoutedMassGrams == receipt.TotalMassGrams,
                "Economy received a drifted physical route receipt.");
            trace.Add("economy-commit");
            current = With(
                value,
                ProductionPreparedOutputRoutePhase
                    .PhysicalAppliedAwaitingItemsAck,
                receipt.PhysicalReceiptFingerprint);
        }

        public void AcknowledgePhysicalRoute(
            string routeOperationId,
            string physicalReceiptFingerprint)
        {
            Require(current.HasValue, "Economy acknowledgement has no route.");
            ProductionPreparedOutputRouteRequestSnapshot value = current.Value;
            Require(
                value.Phase == ProductionPreparedOutputRoutePhase
                    .PhysicalAppliedAwaitingItemsAck
                && string.Equals(
                    value.RouteOperationId,
                    routeOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.PhysicalReceiptFingerprint,
                    physicalReceiptFingerprint,
                    StringComparison.Ordinal),
                "Economy received a drifted Items acknowledgement.");
            trace.Add("economy-ack");
            current = With(
                value,
                ProductionPreparedOutputRoutePhase
                    .ItemsAcknowledgedAwaitingCheckpointGc,
                physicalReceiptFingerprint);
        }

        internal void PublishDeliveryAuthority(string authorityFingerprint)
        {
            Require(current.HasValue, "Delivery authority has no Economy route.");
            ProductionPreparedOutputRouteRequestSnapshot value = current.Value;
            current = With(
                value,
                value.Phase,
                value.PhysicalReceiptFingerprint,
                authorityFingerprint);
        }
    }

    private sealed class FakeExactRoutePort : IFacilityOutputExactRoutePort
    {
        private readonly List<string> trace;
        private FacilityOutputExactRouteReceipt receipt;

        internal FakeExactRoutePort(List<string> trace) =>
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));

        public bool TryRoute(
            FacilityOutputExactRouteRequest request,
            out FacilityOutputExactRouteReceipt published,
            out FacilityOutputExactRouteFailure failure)
        {
            FacilityOutputExactRouteSliceRequest slice = request.Slices.Single();
            FacilityOutputExactRouteSliceReceipt physicalSlice = new(
                "stack:qa-exact-lifecycle-source",
                "stack:qa-exact-lifecycle-routed",
                slice.OutputLineId,
                slice.LineCommitId,
                slice.ItemId,
                slice.SourceOffsetQuantity,
                0,
                slice.Quantity,
                slice.ExactMassGrams,
                slice.ComponentFingerprint);
            string fingerprint = FacilityOutputExactRouteFingerprint
                .CreatePhysicalReceipt(request, new[] { physicalSlice });
            receipt = new FacilityOutputExactRouteReceipt(
                request.RouteOperationId,
                request.RequestFingerprint,
                fingerprint,
                request.BatchCommitId,
                request.SourceDestinationId,
                request.TargetDestinationId,
                request.TargetPosition,
                request.TotalQuantity,
                request.TotalMassGrams,
                new[] { physicalSlice });
            trace.Add("items-route");
            published = receipt;
            failure = FacilityOutputExactRouteFailure.None;
            return true;
        }

        public bool TryAcknowledge(
            string routeOperationId,
            string physicalReceiptFingerprint,
            out FacilityOutputExactRouteReceipt acknowledged,
            out FacilityOutputExactRouteFailure failure)
        {
            if (receipt == null
                || !string.Equals(
                    receipt.RouteOperationId,
                    routeOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.PhysicalReceiptFingerprint,
                    physicalReceiptFingerprint,
                    StringComparison.Ordinal))
            {
                acknowledged = null;
                failure = new FacilityOutputExactRouteFailure(
                    FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                    "qa-exact-route-receipt-mismatch");
                return false;
            }
            trace.Add("items-ack");
            acknowledged = receipt;
            failure = FacilityOutputExactRouteFailure.None;
            return true;
        }

        public bool TryForgetRoutable(
            string routeOperationId,
            string physicalReceiptFingerprint,
            out FacilityOutputExactRouteFailure failure)
        {
            failure = new FacilityOutputExactRouteFailure(
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass,
                "qa-forget-not-supported");
            return false;
        }

        public IReadOnlyList<FacilityOutputExactRoutePendingSnapshot>
            CapturePendingRoutes() =>
            Array.Empty<FacilityOutputExactRoutePendingSnapshot>();
    }

    private sealed class FakeDeliveryCoordinator :
        IProductionPreparedOutputDeliveryCoordinator
    {
        internal const string AuthorityFingerprint =
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

        private readonly FakeRoutingAuthority routing;
        private readonly List<string> trace;
        private readonly bool succeed;

        internal FakeDeliveryCoordinator(
            FakeRoutingAuthority routing,
            List<string> trace,
            bool succeed)
        {
            this.routing = routing;
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
            this.succeed = succeed;
        }

        public ProductionPreparedOutputDeliveryCoordinationResult
            TryApplyExactTarget(
                string routeOperationId,
                ProductionPreparedOutputDeliveryRerouteReason reason,
                string targetDestinationId,
                int targetPositionX,
                int targetPositionY)
        {
            Require(
                reason == ProductionPreparedOutputDeliveryRerouteReason
                    .InitialTargetAuthorityConfirmed
                && string.Equals(
                    routeOperationId,
                    RouteOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    targetDestinationId,
                    TargetDestinationId,
                    StringComparison.Ordinal),
                "Lifecycle requested a drifted exact delivery authority.");
            trace.Add("delivery-authority");
            if (!succeed)
            {
                return new ProductionPreparedOutputDeliveryCoordinationResult(
                    ProductionPreparedOutputDeliveryCoordinationStatus.Deferred,
                    ProductionPreparedOutputDeliveryCoordinationReason
                        .AdmissionUnavailable,
                    routeOperationId,
                    string.Empty,
                    0L,
                    string.Empty,
                    targetDestinationId,
                    targetPositionX,
                    targetPositionY,
                    "qa-delivery-authority-deferred");
            }
            routing.PublishDeliveryAuthority(AuthorityFingerprint);
            return new ProductionPreparedOutputDeliveryCoordinationResult(
                ProductionPreparedOutputDeliveryCoordinationStatus.Applied,
                ProductionPreparedOutputDeliveryCoordinationReason.None,
                routeOperationId,
                "reroute:qa-exact-lifecycle",
                1L,
                Digest('c'),
                targetDestinationId,
                targetPositionX,
                targetPositionY,
                string.Empty);
        }

        public ProductionPreparedOutputDeliveryCoordinationResult
            TryApplyCompatibleWarehouse(
                string routeOperationId,
                string itemId,
                int originPositionX,
                int originPositionY) => throw new InvalidOperationException(
                "The exact-target fixture must not request warehouse selection.");
    }
}
