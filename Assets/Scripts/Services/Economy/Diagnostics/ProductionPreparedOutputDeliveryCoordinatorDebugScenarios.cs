#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class ProductionPreparedOutputDeliveryCoordinatorDebugScenarios
{
    private const string RouteId = "route:qa:coordinator";
    private static readonly string InitialFingerprint = new('a', 64);
    private static readonly string NextFingerprint = new('b', 64);
    private static readonly string ReceiptFingerprint = new('c', 64);
    private static readonly string AuthorityFingerprint = new('d', 64);

    public static void RunAll()
    {
        VerifyAppliedReplayAndDeferred();
        VerifyCapacityRejectAndPublishFaultRollback();
    }

    private static void VerifyAppliedReplayAndDeferred()
    {
        Fixture applied = new();
        ProductionPreparedOutputDeliveryCoordinationResult result = applied
            .Coordinator.TryApplyExactTarget(
                RouteId,
                ProductionPreparedOutputDeliveryRerouteReason
                    .InitialTargetAuthorityConfirmed,
                "facility:qa:target",
                8,
                4);
        Require(result.Status ==
                ProductionPreparedOutputDeliveryCoordinationStatus.Applied
            && applied.Economy.Current.Revision == 1L
            && applied.Items.Current.Revision == 1L
            && applied.Admission.Completed
            && applied.Admission.LastRequest
                .ExpectedNextDeliveryRevisionFingerprint == NextFingerprint,
            "Delivery coordinator did not publish every participant.");
        result = applied.Coordinator.TryApplyExactTarget(
            RouteId,
            ProductionPreparedOutputDeliveryRerouteReason
                .InitialTargetAuthorityConfirmed,
            "facility:qa:target",
            8,
            4);
        Require(result.Status ==
                ProductionPreparedOutputDeliveryCoordinationStatus.Replay,
            "Committed delivery authority was not replay-safe.");
        applied.Admission.CapturedAuthorityFingerprint = new string('9', 64);
        result = applied.Coordinator.TryApplyExactTarget(
            RouteId,
            ProductionPreparedOutputDeliveryRerouteReason
                .InitialTargetAuthorityConfirmed,
            "facility:qa:target",
            8,
            4);
        Require(result.Status ==
                ProductionPreparedOutputDeliveryCoordinationStatus.Rejected
            && result.Reason ==
                ProductionPreparedOutputDeliveryCoordinationReason
                    .AuthorityConflict
            && applied.Admission.PrepareCount == 1
            && applied.Economy.Current.Revision == 1L
            && applied.Items.Current.Revision == 1L,
            "Same-target FacilityBuffer fingerprint refresh was not fail-closed before mass admission.");

        Fixture deferred = new();
        deferred.Items.Defer = true;
        result = deferred.Coordinator.TryApplyExactTarget(
            RouteId,
            ProductionPreparedOutputDeliveryRerouteReason
                .InitialTargetAuthorityConfirmed,
            "facility:qa:target",
            8,
            4);
        Require(result.Status ==
                ProductionPreparedOutputDeliveryCoordinationStatus.Deferred
            && deferred.Economy.Current.Revision == 0L
            && deferred.Items.Current.Revision == 0L
            && !deferred.Admission.Prepared,
            "Deferred Items authority mutated another participant.");
    }

    private static void VerifyCapacityRejectAndPublishFaultRollback()
    {
        Fixture rejected = new();
        rejected.Admission.CaptureFailure =
            PreparedOutputExactDestinationAdmissionFailureCode
                .CapacityUnavailable;
        ProductionPreparedOutputDeliveryCoordinationResult result = rejected
            .Coordinator.TryApplyExactTarget(
                RouteId,
                ProductionPreparedOutputDeliveryRerouteReason
                    .InitialTargetAuthorityConfirmed,
                "facility:qa:target",
                8,
                4);
        Require(result.Status ==
                ProductionPreparedOutputDeliveryCoordinationStatus.Rejected
            && result.Reason ==
                ProductionPreparedOutputDeliveryCoordinationReason
                    .TargetCapacityUnavailable
            && rejected.Economy.Current.Revision == 0L
            && rejected.Items.Current.Revision == 0L,
            "Capacity rejection changed live delivery authority.");

        Fixture fault = new();
        fault.Admission.FailPublish = true;
        bool threw = false;
        try
        {
            fault.Coordinator.TryApplyExactTarget(
                RouteId,
                ProductionPreparedOutputDeliveryRerouteReason
                    .InitialTargetAuthorityConfirmed,
                "facility:qa:target",
                8,
                4);
        }
        catch (InvalidOperationException) { threw = true; }
        Require(threw
            && fault.Items.RollbackCount == 1
            && fault.Admission.RollbackCount == 1
            && fault.Economy.Current.Revision == 0L
            && fault.Items.Current.Revision == 0L,
            "Admission publish fault did not roll back Items exactly.");

        Fixture rollbackFault = new();
        rollbackFault.Admission.FailPublish = true;
        rollbackFault.Admission.FailRollback = true;
        bool aggregated = false;
        try
        {
            rollbackFault.Coordinator.TryApplyExactTarget(
                RouteId,
                ProductionPreparedOutputDeliveryRerouteReason
                    .InitialTargetAuthorityConfirmed,
                "facility:qa:target",
                8,
                4);
        }
        catch (AggregateException) { aggregated = true; }
        Require(aggregated
            && rollbackFault.Admission.RollbackCount == 1
            && rollbackFault.Items.RollbackCount == 1
            && rollbackFault.Items.Current.Revision == 0L,
            "Admission rollback failure skipped the remaining Items rollback.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal Fixture()
        {
            Economy = new FakeEconomy();
            Items = new FakeItems();
            Admission = new FakeAdmission();
            Coordinator = new ProductionPreparedOutputDeliveryCoordinator(
                Economy,
                Items,
                Admission,
                new EmptyCatalog(),
                new EmptyWarehouses());
        }

        internal FakeEconomy Economy { get; }
        internal FakeItems Items { get; }
        internal FakeAdmission Admission { get; }
        internal ProductionPreparedOutputDeliveryCoordinator Coordinator { get; }
    }

    private sealed class FakeEconomy :
        IProductionPreparedOutputDeliveryRerouteParticipant
    {
        internal ProductionPreparedOutputDeliveryRevisionSnapshot Current =
            EconomyRevision(0L, InitialFingerprint, "facility:qa:origin", 1, 1);

        public ProductionPreparedOutputDeliveryRevisionSnapshot
            CaptureCurrentDelivery(string routeOperationId) => Current;

        public IProductionPreparedOutputDeliveryRerouteCandidate
            PrepareDeliveryReroute(
                string routeOperationId,
                long expectedCurrentRevision,
                string expectedCurrentRevisionFingerprint,
                string originalPhysicalReceiptFingerprint,
                ProductionPreparedOutputDeliveryRerouteReason reason,
                string targetDestinationId,
                int targetPositionX,
                int targetPositionY,
                string targetAuthorityFingerprint) => new EconomyCandidate(
                expectedCurrentRevision,
                expectedCurrentRevisionFingerprint,
                reason,
                targetDestinationId,
                targetPositionX,
                targetPositionY,
                targetAuthorityFingerprint);

        public void PublishDeliveryReroute(
            IProductionPreparedOutputDeliveryRerouteCandidate candidate)
        {
            Current = EconomyRevision(
                candidate.NextRevision,
                candidate.NextRevisionFingerprint,
                candidate.TargetDestinationId,
                candidate.TargetPositionX,
                candidate.TargetPositionY,
                candidate.TargetAuthorityFingerprint);
            ((EconomyCandidate)candidate).Published = true;
        }

        public void RollbackDeliveryReroute(
            IProductionPreparedOutputDeliveryRerouteCandidate candidate)
        {
            if (((EconomyCandidate)candidate).Published)
                Current = EconomyRevision(
                    0L, InitialFingerprint, "facility:qa:origin", 1, 1);
        }

        public void CompleteDeliveryReroute(
            IProductionPreparedOutputDeliveryRerouteCandidate candidate) { }
    }

    private sealed class EconomyCandidate :
        IProductionPreparedOutputDeliveryRerouteCandidate
    {
        internal EconomyCandidate(
            long expectedRevision,
            string expectedFingerprint,
            ProductionPreparedOutputDeliveryRerouteReason reason,
            string target,
            int x,
            int y,
            string authority)
        {
            ExpectedCurrentRevision = expectedRevision;
            ExpectedCurrentRevisionFingerprint = expectedFingerprint;
            Reason = reason;
            TargetDestinationId = target;
            TargetPositionX = x;
            TargetPositionY = y;
            TargetAuthorityFingerprint = authority;
        }

        public string RouteOperationId => RouteId;
        public string RerouteOperationId => "reroute:qa:one";
        public long ExpectedCurrentRevision { get; }
        public string ExpectedCurrentRevisionFingerprint { get; }
        public string PreviousRevisionFingerprint => InitialFingerprint;
        public long NextRevision => 1L;
        public string NextRevisionFingerprint => NextFingerprint;
        public string OriginalPhysicalReceiptFingerprint => ReceiptFingerprint;
        public ProductionPreparedOutputDeliveryRerouteReason Reason { get; }
        public string TargetDestinationId { get; }
        public int TargetPositionX { get; }
        public int TargetPositionY { get; }
        public string TargetAuthorityFingerprint { get; }
        internal bool Published { get; set; }
    }

    private sealed class FakeItems :
        IFacilityOutputExactRouteDeliveryOverlayParticipant
    {
        internal bool Defer { get; set; }
        internal int RollbackCount { get; private set; }
        internal FacilityOutputExactRouteDeliveryRevisionSnapshot Current =
            ItemsRevision(0L, InitialFingerprint, "facility:qa:origin", 1, 1);

        public FacilityOutputExactRouteDeliveryRevisionSnapshot
            CaptureCurrentDelivery(string routeOperationId) => Current;

        public IFacilityOutputExactRouteDeliveryOverlayCandidate
            PrepareDeliveryOverlay(
                string routeOperationId,
                long expectedCurrentRevision,
                string expectedCurrentRevisionFingerprint,
                string originalPhysicalReceiptFingerprint,
                long nextRevision,
                string nextRevisionFingerprint,
                string rerouteOperationId,
                string targetDestinationId,
                int targetPositionX,
                int targetPositionY,
                string targetAuthorityFingerprint) => new ItemsCandidate(
                Defer,
                expectedCurrentRevision,
                expectedCurrentRevisionFingerprint,
                new FacilityOutputExactRouteDeliveryRevisionSnapshot(
                    RouteId,
                    ReceiptFingerprint,
                    nextRevision,
                    nextRevisionFingerprint,
                    rerouteOperationId,
                    targetDestinationId,
                    targetPositionX,
                    targetPositionY,
                    targetAuthorityFingerprint));

        public void PublishDeliveryOverlay(
            IFacilityOutputExactRouteDeliveryOverlayCandidate candidate)
        {
            Current = candidate.Next;
            ((ItemsCandidate)candidate).Published = true;
        }

        public void RollbackDeliveryOverlay(
            IFacilityOutputExactRouteDeliveryOverlayCandidate candidate)
        {
            RollbackCount++;
            if (((ItemsCandidate)candidate).Published)
                Current = ItemsRevision(
                    0L, InitialFingerprint, "facility:qa:origin", 1, 1);
        }

        public void CompleteDeliveryOverlay(
            IFacilityOutputExactRouteDeliveryOverlayCandidate candidate) { }
    }

    private sealed class ItemsCandidate :
        IFacilityOutputExactRouteDeliveryOverlayCandidate
    {
        internal ItemsCandidate(
            bool deferred,
            long expectedRevision,
            string expectedFingerprint,
            FacilityOutputExactRouteDeliveryRevisionSnapshot next)
        {
            Status = deferred
                ? FacilityOutputExactRouteDeliveryOverlayStatus.Deferred
                : FacilityOutputExactRouteDeliveryOverlayStatus.Prepared;
            Reason = deferred
                ? FacilityOutputExactRouteDeliveryOverlayReason
                    .PhysicalStateNotStable
                : FacilityOutputExactRouteDeliveryOverlayReason.None;
            ExpectedCurrentRevision = expectedRevision;
            ExpectedCurrentRevisionFingerprint = expectedFingerprint;
            Next = next;
        }

        public FacilityOutputExactRouteDeliveryOverlayStatus Status { get; }
        public FacilityOutputExactRouteDeliveryOverlayReason Reason { get; }
        public string Message => Status.ToString();
        public string RouteOperationId => RouteId;
        public long ExpectedCurrentRevision { get; }
        public string ExpectedCurrentRevisionFingerprint { get; }
        public FacilityOutputExactRouteDeliveryRevisionSnapshot Next { get; }
        public IReadOnlyList<FacilityOutputExactRouteDeliverySubjectSnapshot>
            DeliverySubjects => new[]
            {
                new FacilityOutputExactRouteDeliverySubjectSnapshot(
                    "stack:qa:one", 2, 0L, new string('f', 64), 2_000L,
                    RouteId, ReceiptFingerprint)
            };
        internal bool Published { get; set; }
    }

    private sealed class FakeAdmission :
        IPreparedOutputExactDestinationAdmissionParticipant
    {
        internal PreparedOutputExactDestinationAdmissionFailureCode
            CaptureFailure { get; set; }
        internal bool FailPublish { get; set; }
        internal bool FailRollback { get; set; }
        internal string CapturedAuthorityFingerprint { get; set; } =
            AuthorityFingerprint;
        internal bool Prepared { get; private set; }
        internal int PrepareCount { get; private set; }
        internal bool Completed { get; private set; }
        internal int RollbackCount { get; private set; }
        internal PreparedOutputExactDestinationAdmissionRequest LastRequest
            { get; private set; }
        public string ParticipantId => "qa.admission";

        public bool TryCaptureTargetAuthority(
            PreparedOutputExactDestinationTargetKind kind,
            string destinationId,
            Vector2Int position,
            out PreparedOutputExactDestinationAuthoritySnapshot snapshot,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason)
        {
            failureCode = CaptureFailure;
            failureReason = failureCode == 0 ? string.Empty : "injected";
            snapshot = failureCode == 0
                ? new PreparedOutputExactDestinationAuthoritySnapshot(
                    kind, destinationId, position, CapturedAuthorityFingerprint,
                    1L, 1L, 10_000L, 0L)
                : default;
            return failureCode == 0;
        }

        public bool TryPrepare(
            PreparedOutputExactDestinationAdmissionRequest request,
            out PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason)
        {
            Prepared = true;
            PrepareCount++;
            LastRequest = request;
            candidate = new PreparedOutputExactDestinationAdmissionCandidate(
                ParticipantId,
                request,
                new PreparedOutputExactDestinationAdmissionHandle(
                    request.TargetAuthority.Kind,
                    default,
                    new string('1', 64),
                    new string('2', 64)));
            failureCode = 0;
            failureReason = string.Empty;
            return true;
        }

        public bool TryPublish(
            PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason)
        {
            failureCode = FailPublish
                ? PreparedOutputExactDestinationAdmissionFailureCode
                    .AuthorityStale
                : 0;
            failureReason = FailPublish ? "injected" : string.Empty;
            if (!FailPublish)
                candidate.Phase = PreparedOutputExactDestinationAdmissionPhase.Published;
            return !FailPublish;
        }

        public bool TryRollback(
            PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason)
        {
            RollbackCount++;
            if (FailRollback)
            {
                failureCode =
                    PreparedOutputExactDestinationAdmissionFailureCode
                        .RollbackFailed;
                failureReason = "injected rollback failure";
                return false;
            }
            candidate.Phase = PreparedOutputExactDestinationAdmissionPhase.RolledBack;
            failureCode = 0;
            failureReason = string.Empty;
            return true;
        }

        public bool TryComplete(
            PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason)
        {
            Completed = true;
            candidate.Phase = PreparedOutputExactDestinationAdmissionPhase.Completed;
            failureCode = 0;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class EmptyCatalog : IDungeonItemCatalogProvider
    {
        public IReadOnlyList<DungeonItemDefinition> All =>
            Array.Empty<DungeonItemDefinition>();
        public DungeonItemDefinition GetDefinition(string itemId) => null;
        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition definition)
        {
            definition = null;
            return false;
        }
    }

    private sealed class EmptyWarehouses : IWarehouseWorldQuery
    {
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses =>
            Array.Empty<IWarehouseFacility>();
    }

    private static ProductionPreparedOutputDeliveryRevisionSnapshot EconomyRevision(
        long revision,
        string fingerprint,
        string target,
        int x,
        int y,
        string authority = "") => new(
        RouteId,
        revision,
        revision == 0L
            ? ProductionPreparedOutputDeliveryTargetKind.InitialExactTarget
            : ProductionPreparedOutputDeliveryTargetKind.ExactRerouteTarget,
        revision == 0L
            ? ProductionPreparedOutputDeliveryRerouteReason.InitialRoute
            : ProductionPreparedOutputDeliveryRerouteReason
                .InitialTargetAuthorityConfirmed,
        revision == 0L ? string.Empty : "reroute:qa:one",
        revision == 0L ? string.Empty : InitialFingerprint,
        ReceiptFingerprint,
        target,
        x,
        y,
        authority,
        fingerprint);

    private static FacilityOutputExactRouteDeliveryRevisionSnapshot ItemsRevision(
        long revision,
        string fingerprint,
        string target,
        int x,
        int y,
        string authority = "") => new(
        RouteId,
        ReceiptFingerprint,
        revision,
        fingerprint,
        revision == 0L ? string.Empty : "reroute:qa:one",
        target,
        x,
        y,
        authority);
}
#endif
