using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public static class ProductionPreparedOutputRoutingRestoreJoinDebugScenarios
{
    private const string Operation = "route:qa:0001";
    private const string Batch = "batch:qa:0001";
    private const string Line = "line:qa:0001";
    private const string OutputLine = "output:qa:main";
    private const string Item = "food:qa";
    private const string Source = "production-output:building:qa";
    private const string Target = "production-output-route-target:qa";
    private const string SourceStack = "stack:qa:source";
    private const string RoutedStack = "stack:qa:routed";
    private static readonly string Component = new('a', 64);
    private static readonly FacilityOutputExactRouteRequest CanonicalRequest =
        CreateCanonicalRequest();
    private static readonly FacilityOutputExactRouteSliceReceipt[]
        CanonicalPhysicalSlices = CreateCanonicalPhysicalSlices();
    private static readonly string Request = CanonicalRequest.RequestFingerprint;
    private static readonly string Receipt =
        FacilityOutputExactRouteFingerprint.CreatePhysicalReceipt(
            CanonicalRequest,
            CanonicalPhysicalSlices);

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Prepared Output Route Restore Join")]
    public static void RunFromMenu()
    {
        VerifyValidAndPendingAcknowledgement();
        VerifyLegalCrashWindows();
        VerifyOrphanMissingExtraAndOneGramFailures();
        VerifyRangeDuplicateAndPhaseFailures();
        VerifyReconcileRollbackAndPublish();
        VerifyCheckpointAuthorityJoin();
        UnityEngine.Debug.Log(
            "Production prepared-output routing restore-join scenarios passed.");
    }

    private static void VerifyValidAndPendingAcknowledgement()
    {
        Fixture final = Create(
            ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc,
            FacilityOutputExactRoutePhase.Routable);
        ProductionPreparedOutputRoutingRestoreJoinPlan finalPlan =
            final.Join.Build(final.Owner);
        Require(finalPlan.JoinValidated && finalPlan.Acknowledgements.Count == 0,
            "routable final join did not validate exactly");

        Fixture warehouseFallback = Create(
            ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc,
            FacilityOutputExactRoutePhase.Routable);
        warehouseFallback.Owner.batches[0].lines[0].routeOperations[0]
            .targetDestinationId = string.Empty;
        warehouseFallback.Query.RoutesValue[0].targetDestinationId = string.Empty;
        FacilityOutputExactRouteRequest fallbackRequest =
            CreateCanonicalRequest(string.Empty);
        string fallbackReceipt =
            FacilityOutputExactRouteFingerprint.CreatePhysicalReceipt(
                fallbackRequest,
                CanonicalPhysicalSlices);
        warehouseFallback.Owner.batches[0].lines[0].routeOperations[0]
            .requestFingerprint = fallbackRequest.RequestFingerprint;
        warehouseFallback.Owner.batches[0].lines[0].routeOperations[0]
            .physicalReceiptFingerprint = fallbackReceipt;
        warehouseFallback.Query.RoutesValue[0].requestFingerprint =
            fallbackRequest.RequestFingerprint;
        warehouseFallback.Query.RoutesValue[0].physicalReceiptFingerprint =
            fallbackReceipt;
        Require(warehouseFallback.Join.Build(warehouseFallback.Owner).JoinValidated,
            "canonical empty warehouse-fallback target did not join");

        Fixture pending = Create(
            ProductionPreparedOutputRoutePhase
                .PhysicalAppliedAwaitingItemsAck,
            FacilityOutputExactRoutePhase.PhysicalPending);
        ProductionPreparedOutputRoutingRestoreJoinPlan pendingPlan =
            pending.Join.Build(pending.Owner);
        Require(pendingPlan.JoinValidated
            && pendingPlan.PhysicalCommits.Count == 0
            && pendingPlan.Acknowledgements.Count == 1
            && pendingPlan.Acknowledgements[0].RouteOperationId == Operation,
            "pending acknowledgement was not planned exactly once");
    }

    private static void VerifyLegalCrashWindows()
    {
        Fixture itemsCommittedFirst = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            FacilityOutputExactRoutePhase.PhysicalPending);
        ProductionPreparedOutputRoutingRestoreJoinPlan recoverCommit =
            itemsCommittedFirst.Join.Build(itemsCommittedFirst.Owner);
        Require(recoverCommit.JoinValidated
            && recoverCommit.PhysicalCommits.Count == 1
            && recoverCommit.Acknowledgements.Count == 1
            && recoverCommit.PhysicalCommits[0].RouteOperationId == Operation
            && recoverCommit.PhysicalCommits[0].PhysicalReceiptFingerprint == Receipt,
            "Items-first crash window did not reconstruct one exact owner commit");
        itemsCommittedFirst.Join.Reconcile(recoverCommit);
        Require(itemsCommittedFirst.Trace.SequenceEqual(new[]
            {
                "owner:commit", "items:acknowledge", "owner:acknowledge"
            }),
            "Items-first restore did not commit owner before both acknowledgements");

        Fixture itemsAcknowledgedFirst = Create(
            ProductionPreparedOutputRoutePhase
                .PhysicalAppliedAwaitingItemsAck,
            FacilityOutputExactRoutePhase.Routable);
        ProductionPreparedOutputRoutingRestoreJoinPlan recoverOwnerAck =
            itemsAcknowledgedFirst.Join.Build(itemsAcknowledgedFirst.Owner);
        Require(recoverOwnerAck.JoinValidated
            && recoverOwnerAck.PhysicalCommits.Count == 0
            && recoverOwnerAck.Acknowledgements.Count == 1,
            "Items-acknowledged crash window did not plan one idempotent acknowledgement");
        itemsAcknowledgedFirst.Join.Reconcile(recoverOwnerAck);
        Require(itemsAcknowledgedFirst.Trace.SequenceEqual(new[]
            {
                "items:acknowledge", "owner:acknowledge"
            }),
            "Items-acknowledged restore did not reconcile both staged owners");

        Fixture impossibleReverse = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            FacilityOutputExactRoutePhase.Routable);
        RequireThrows(() => impossibleReverse.Join.Build(impossibleReverse.Owner),
            "acknowledged Items receipt without an Economy physical commit");
    }

    private static void VerifyOrphanMissingExtraAndOneGramFailures()
    {
        Fixture orphan = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            null);
        FacilityOutputExactRouteOutboxSaveData orphanRoute = CreatePhysical(
            FacilityOutputExactRoutePhase.PhysicalPending);
        orphanRoute.routeOperationId = "route:qa:orphan";
        orphan.Query.RoutesValue.Add(orphanRoute);
        RequireThrows(() => orphan.Join.Build(orphan.Owner), "orphan");

        Fixture missing = Create(
            ProductionPreparedOutputRoutePhase
                .PhysicalAppliedAwaitingItemsAck,
            null);
        RequireThrows(() => missing.Join.Build(missing.Owner), "missing");

        Fixture extra = Create(
            ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc,
            FacilityOutputExactRoutePhase.Routable);
        extra.Query.RoutesValue[0].slices.Add(
            extra.Query.RoutesValue[0].slices[0].Clone());
        extra.Query.RoutesValue[0].slices[1].routedStackId =
            "stack:qa:routed-extra";
        RequireThrows(() => extra.Join.Build(extra.Owner), "extra");

        Fixture wrongGram = Create(
            ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc,
            FacilityOutputExactRoutePhase.Routable);
        wrongGram.Query.RoutesValue[0].totalMassGrams--;
        RequireThrows(() => wrongGram.Join.Build(wrongGram.Owner), "1g");

        Fixture pendingWrongGram = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            FacilityOutputExactRoutePhase.PhysicalPending);
        pendingWrongGram.Query.RoutesValue[0].slices[0].routedMassGrams--;
        RequireThrows(() => pendingWrongGram.Join.Build(pendingWrongGram.Owner),
            "pending recovery 1g tamper");

        Fixture pendingWrongComponent = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            FacilityOutputExactRoutePhase.PhysicalPending);
        pendingWrongComponent.Query.RoutesValue[0].slices[0]
            .componentFingerprint = new string('f', 64);
        RequireThrows(() => pendingWrongComponent.Join.Build(
            pendingWrongComponent.Owner), "pending recovery component tamper");

        Fixture pendingWrongReceipt = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            FacilityOutputExactRoutePhase.PhysicalPending);
        pendingWrongReceipt.Query.RoutesValue[0].physicalReceiptFingerprint =
            new string('f', 64);
        RequireThrows(() => pendingWrongReceipt.Join.Build(
            pendingWrongReceipt.Owner), "pending recovery receipt tamper");
    }

    private static void VerifyRangeDuplicateAndPhaseFailures()
    {
        Fixture gap = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            null);
        gap.Owner.batches[0].lines[0].routeOperations[0]
            .sourceOffsetQuantity = 1;
        RequireThrows(() => gap.Join.Build(gap.Owner), "gap");

        Fixture overlap = Create(
            ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc,
            FacilityOutputExactRoutePhase.Routable);
        ProductionPreparedOutputRoutingLineSaveData overlapLine =
            overlap.Owner.batches[0].lines[0];
        overlapLine.originalQuantity = 2;
        overlapLine.remainingQuantity = 0;
        overlapLine.routedQuantity = 2;
        overlapLine.originalMassGrams = 2_000L;
        overlapLine.remainingMassGrams = 0L;
        overlapLine.routedMassGrams = 2_000L;
        ProductionPreparedOutputRouteOperationSaveData second =
            overlapLine.routeOperations[0].Clone();
        second.routeOperationId = "route:qa:0002";
        second.sourceOffsetQuantity = 0;
        overlapLine.routeOperations.Add(second);
        RequireThrows(() => overlap.Join.Build(overlap.Owner), "overlap");

        Fixture duplicate = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            null);
        duplicate.Owner.batches[0].lines.Add(
            duplicate.Owner.batches[0].lines[0].Clone());
        RequireThrows(() => duplicate.Join.Build(duplicate.Owner), "duplicate");

        Fixture phase = Create(
            ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc,
            FacilityOutputExactRoutePhase.PhysicalPending);
        RequireThrows(() => phase.Join.Build(phase.Owner), "phase");
    }

    private static void VerifyReconcileRollbackAndPublish()
    {
        Fixture failure = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            FacilityOutputExactRoutePhase.PhysicalPending);
        ProductionPreparedOutputRoutingRestoreJoinPlan plan =
            failure.Join.Build(failure.Owner);
        failure.OwnerReconciler.ThrowOnAcknowledge = true;
        RequireThrows(() => failure.Join.Reconcile(plan), "reconcile fault");
        failure.ItemReconciler.Discard();
        failure.OwnerReconciler.Discard();
        Require(!failure.ItemReconciler.LiveAcknowledged
            && !failure.OwnerReconciler.LiveAcknowledged
            && !failure.OwnerReconciler.LiveCommitted,
            "faulted reconcile leaked a staged physical commit or acknowledgement");

        Fixture success = Create(
            ProductionPreparedOutputRoutePhase.PhysicalPending,
            FacilityOutputExactRoutePhase.PhysicalPending);
        ProductionPreparedOutputRoutingRestoreJoinPlan successPlan =
            success.Join.Build(success.Owner);
        success.Join.Reconcile(successPlan);
        success.ItemReconciler.Publish();
        success.OwnerReconciler.Publish();
        Require(success.ItemReconciler.LiveAcknowledged
            && success.OwnerReconciler.LiveAcknowledged
            && success.OwnerReconciler.LiveCommitted,
            "successful reconcile did not publish the staged commit and acknowledgements");
    }

    private static void VerifyCheckpointAuthorityJoin()
    {
        const string digest =
            "abababababababababababababababababababababababababababababababab";
        Fixture mismatch = Create(
            ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc,
            FacilityOutputExactRoutePhase.Routable);
        mismatch.Query.LastConfirmedCheckpointSequence = 1L;
        mismatch.Query.LastConfirmedCheckpointDigest = digest;
        RequireThrows(() => mismatch.Join.Build(mismatch.Owner),
            "Economy/Items checkpoint sequence mismatch");

        Fixture exact = Create(
            ProductionPreparedOutputRoutePhase
                .ItemsAcknowledgedAwaitingCheckpointGc,
            FacilityOutputExactRoutePhase.Routable);
        exact.Owner.lastConfirmedCheckpointSequence = 1L;
        exact.Owner.lastConfirmedCheckpointDigest = digest;
        exact.Query.LastConfirmedCheckpointSequence = 1L;
        exact.Query.LastConfirmedCheckpointDigest = digest;
        Require(exact.Join.Build(exact.Owner).JoinValidated,
            "Exact Economy/Items checkpoint authority did not join.");
    }

    private static Fixture Create(
        ProductionPreparedOutputRoutePhase ownerPhase,
        FacilityOutputExactRoutePhase? physicalPhase)
    {
        CandidateQuery query = new();
        if (physicalPhase.HasValue)
            query.RoutesValue.Add(CreatePhysical(physicalPhase.Value));
        List<string> trace = new();
        FakeReconciler items = new("items", trace);
        FakeReconciler owner = new("owner", trace);
        return new Fixture(
            CreateOwner(ownerPhase),
            query,
            items,
            owner,
            new ProductionPreparedOutputRoutingRestoreJoin(
                query,
                items,
                owner),
            trace);
    }

    private static ProductionPreparedOutputRoutingSaveData CreateOwner(
        ProductionPreparedOutputRoutePhase phase)
    {
        bool physicallyApplied = phase !=
            ProductionPreparedOutputRoutePhase.PhysicalPending;
        ProductionPreparedOutputRouteOperationSaveData operation = new()
        {
            routeOperationId = Operation,
            requestFingerprint = Request,
            physicalReceiptFingerprint = phase ==
                ProductionPreparedOutputRoutePhase.PhysicalPending
                    ? string.Empty
                    : Receipt,
            phase = phase,
            sourceOffsetQuantity = 0,
            sourceOffsetMassGrams = 0L,
            routedQuantity = 1,
            routedMassGrams = 1_000L,
            targetPositionX = 7,
            targetPositionY = 9,
            targetDestinationId = Target,
            physicalSlices = phase ==
                ProductionPreparedOutputRoutePhase.PhysicalPending
                    ? new List<ProductionPreparedOutputPhysicalRouteSliceSaveData>()
                    : new List<ProductionPreparedOutputPhysicalRouteSliceSaveData>
                    {
                        new()
                        {
                            sourceStackId = SourceStack,
                            routedStackId = RoutedStack,
                            sourceOffsetQuantity = 0,
                            routedOffsetQuantity = 0,
                            routedQuantity = 1,
                            routedMassGrams = 1_000L
                        }
                    }
        };
        return new ProductionPreparedOutputRoutingSaveData
        {
            version = ProductionPreparedOutputRoutingSaveData.CurrentVersion,
            batches = new List<ProductionPreparedOutputRoutingBatchSaveData>
            {
                new()
                {
                    batchCommitId = Batch,
                    ownerBillId = "bill:qa:0001",
                    ownerRecipeId = "recipe:qa:0001",
                    ownerFacilityId = "building:qa",
                    cycleSequence = 1,
                    outcomeFingerprint = new string('d', 64),
                    routingFingerprint = new string('e', 64),
                    destinationId = Source,
                    lines = new List<ProductionPreparedOutputRoutingLineSaveData>
                    {
                        new()
                        {
                            batchCommitId = Batch,
                            lineCommitId = Line,
                            outputLineId = OutputLine,
                            role = ProductionOutputRole.Main,
                            itemId = Item,
                            destinationId = Source,
                            componentFingerprint = Component,
                            originalQuantity = 2,
                            remainingQuantity = physicallyApplied ? 1 : 2,
                            routedQuantity = physicallyApplied ? 1 : 0,
                            originalMassGrams = 2_000L,
                            remainingMassGrams = physicallyApplied
                                ? 1_000L
                                : 2_000L,
                            routedMassGrams = physicallyApplied ? 1_000L : 0L,
                            routeOperations = new List<
                                ProductionPreparedOutputRouteOperationSaveData>
                            {
                                operation
                            }
                        }
                    }
                }
            }
        };
    }

    private static FacilityOutputExactRouteOutboxSaveData CreatePhysical(
        FacilityOutputExactRoutePhase phase) => new()
    {
        phase = phase,
        routeOperationId = Operation,
        requestFingerprint = Request,
        physicalReceiptFingerprint = Receipt,
        batchCommitId = Batch,
        sourceDestinationId = Source,
        targetDestinationId = Target,
        targetPositionX = 7,
        targetPositionY = 9,
        totalQuantity = 1,
        totalMassGrams = 1_000L,
        slices = new List<FacilityOutputExactRouteSliceSaveData>
        {
            new()
            {
                sourceStackId = SourceStack,
                routedStackId = RoutedStack,
                outputLineId = OutputLine,
                lineCommitId = Line,
                itemId = Item,
                sourceOffsetQuantity = 0,
                routedOffsetQuantity = 0,
                routedQuantity = 1,
                routedMassGrams = 1_000L,
                componentFingerprint = Component
            }
        }
    };

    private static FacilityOutputExactRouteRequest CreateCanonicalRequest(
        string targetDestinationId = Target) => new(
        Operation,
        Batch,
        Source,
        targetDestinationId,
        new UnityEngine.Vector2Int(7, 9),
        new[]
        {
            new FacilityOutputExactRouteSliceRequest(
                OutputLine,
                Line,
                Item,
                0,
                1,
                1_000L,
                Component)
        });

    private static FacilityOutputExactRouteSliceReceipt[]
        CreateCanonicalPhysicalSlices() => new[]
        {
            new FacilityOutputExactRouteSliceReceipt(
                SourceStack,
                RoutedStack,
                OutputLine,
                Line,
                Item,
                0,
                0,
                1,
                1_000L,
                Component)
        };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected failure: " + message);
    }

    private sealed class CandidateQuery :
        IFacilityOutputExactRouteRestoreCandidateQuery
    {
        internal List<FacilityOutputExactRouteOutboxSaveData> RoutesValue { get; }
            = new();
        public bool IsCandidateAvailable => true;
        public long LastConfirmedCheckpointSequence { get; set; }
        public string LastConfirmedCheckpointDigest { get; set; } = string.Empty;
        public IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> Routes =>
            RoutesValue;
        public bool TryGetRoute(
            string routeOperationId,
            out FacilityOutputExactRouteOutboxSaveData route)
        {
            route = RoutesValue.SingleOrDefault(value => string.Equals(
                value.routeOperationId,
                routeOperationId,
                StringComparison.Ordinal));
            return route != null;
        }
    }

    private sealed class FakeReconciler :
        IFacilityOutputExactRouteRestoreReconciler,
        IProductionPreparedOutputRoutingRestoreReconciler
    {
        private readonly string label;
        private readonly IList<string> trace;
        internal bool ThrowOnAcknowledge { get; set; }
        internal bool LiveAcknowledged { get; private set; }
        internal bool LiveCommitted { get; private set; }
        private bool stagedAcknowledged;
        private bool stagedCommitted;

        internal FakeReconciler(string label, IList<string> trace)
        {
            this.label = label;
            this.trace = trace;
        }

        public void CommitRestoredPhysicalRoute(
            ProductionPreparedOutputPhysicalRouteReceipt receipt)
        {
            Require(receipt.RouteOperationId == Operation
                && receipt.RequestFingerprint == Request
                && receipt.PhysicalReceiptFingerprint == Receipt,
                "reconciler received a conflicting physical commit");
            trace.Add(label + ":commit");
            stagedCommitted = true;
        }

        public void AcknowledgeRestoredRoute(
            string routeOperationId,
            string physicalReceiptFingerprint)
        {
            if (ThrowOnAcknowledge)
                throw new InvalidOperationException("injected-reconcile-fault");
            Require(routeOperationId == Operation
                && physicalReceiptFingerprint == Receipt,
                "reconciler received a conflicting identity");
            trace.Add(label + ":acknowledge");
            stagedAcknowledged = true;
        }

        internal void Publish()
        {
            LiveAcknowledged = stagedAcknowledged;
            LiveCommitted = stagedCommitted;
            stagedAcknowledged = false;
            stagedCommitted = false;
        }

        internal void Discard()
        {
            stagedAcknowledged = false;
            stagedCommitted = false;
        }
    }

    private sealed class Fixture
    {
        internal Fixture(
            ProductionPreparedOutputRoutingSaveData owner,
            CandidateQuery query,
            FakeReconciler itemReconciler,
            FakeReconciler ownerReconciler,
            ProductionPreparedOutputRoutingRestoreJoin join,
            List<string> trace)
        {
            Owner = owner;
            Query = query;
            ItemReconciler = itemReconciler;
            OwnerReconciler = ownerReconciler;
            Join = join;
            Trace = trace;
        }

        internal ProductionPreparedOutputRoutingSaveData Owner { get; }
        internal CandidateQuery Query { get; }
        internal FakeReconciler ItemReconciler { get; }
        internal FakeReconciler OwnerReconciler { get; }
        internal ProductionPreparedOutputRoutingRestoreJoin Join { get; }
        internal List<string> Trace { get; }
    }
}
