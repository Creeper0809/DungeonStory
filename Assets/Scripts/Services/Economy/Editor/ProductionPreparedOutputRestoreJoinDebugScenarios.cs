#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputRestoreJoinDebugScenarios
{
    private const string RecipeId = "recipe:hay-feed";
    private const string ItemId = "item:qa:prepared-output-restore";
    private const string DestinationId =
        "production:qa:prepared-output-restore-buffer";
    private const string OwnerDomain =
        ProductionOutputDestinationAuthorityRuntime.OwnerDomain;
    private const string OwnerFacilityId = "building:qa:prepared-output-restore";
    private const string OutputLineId = "output:main";
    private const int CycleSequence = 1;
    private const int Quantity = 2;
    private const long UnitMassGrams = 1_000L;
    private const string OutcomeFingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string RecipeDefinitionDigest =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ComponentFingerprint =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string CapacitySourceDigest =
        "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private const string ConflictingFingerprint =
        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private static readonly ProductionBillId BillId =
        (ProductionBillId)"production-bill:prepared-output-restore-qa";
    private static readonly Vector2Int DropPosition = new(9, 4);

    [MenuItem("DungeonStory/Debug/Economy/Run Prepared Output Restore Join")]
    public static void RunAll()
    {
        VerifyPublicationPreparedAdoptsAndAcknowledgesExactIncoming();
        VerifyPhysicalPendingJoinsExactIncoming();
        VerifyOrphanAndMissingIncomingFailLoud();
        VerifyIncomingMismatchesFailLoud();
        VerifyPersistedCandidateMismatchFailsLoud();
        VerifyCompletedPendingAndEmptyJoinFailLoud();
        Debug.Log("V27_PRODUCTION_PREPARED_OUTPUT_RESTORE_JOIN=PASS");
    }

    private static void
        VerifyPublicationPreparedAdoptsAndAcknowledgesExactIncoming()
    {
        Fixture fixture = new();
        DungeonProductionBillSaveData payload = fixture.CreatePayload(
            ProductionPreparedOutputPhase.PublicationPrepared);
        ProductionPreparedOutputRestoreJoin join = fixture.CreateJoin();

        ProductionPreparedOutputRestoreJoinPlan plan = join.Build(payload);
        ProductionPreparedOutputBatchSaveData normalized =
            plan.NormalizedPayload.bills.Single().preparedOutput;
        Require(
            ReferenceEquals(payload, plan.NormalizedPayload)
            && normalized.phase == ProductionPreparedOutputPhase.Completed
            && normalized.physicalCandidates.Count ==
                fixture.Incoming.Stacks.Count
            && plan.Acknowledgements.Count == 1
            && string.Equals(
                plan.Acknowledgements[0].BatchCommitId,
                fixture.Incoming.BatchCommitId,
                StringComparison.Ordinal),
            "PublicationPrepared did not adopt the exact incoming physical batch.");

        join.Acknowledge(plan);
        fixture.RequireDurableNonStackingProvenance();
    }

    private static void VerifyPhysicalPendingJoinsExactIncoming()
    {
        Fixture fixture = new();
        DungeonProductionBillSaveData payload = fixture.CreatePayload(
            ProductionPreparedOutputPhase
                .PhysicalBatchCommittedPublicationPending);
        ProductionPreparedOutputRestoreJoin join = fixture.CreateJoin();

        ProductionPreparedOutputRestoreJoinPlan plan = join.Build(payload);
        Require(
            plan.NormalizedPayload.bills.Single().preparedOutput.phase ==
                ProductionPreparedOutputPhase.Completed
            && plan.Acknowledgements.Count == 1,
            "PhysicalBatchCommittedPublicationPending did not join its exact batch.");
        join.Acknowledge(plan);
        fixture.RequireDurableNonStackingProvenance();
    }

    private static void VerifyOrphanAndMissingIncomingFailLoud()
    {
        Fixture orphanFixture = new();
        DungeonProductionBillSaveData orphanPayload = new()
        {
            bills = new List<ProductionBillSaveData>()
        };
        RequireThrows(
            () => orphanFixture.CreateJoin().Build(orphanPayload),
            "An incoming physical batch without a Production owner was accepted.");

        Fixture missingFixture = new();
        DungeonProductionBillSaveData missingPayload = missingFixture.CreatePayload(
            ProductionPreparedOutputPhase
                .PhysicalBatchCommittedPublicationPending);
        ProductionPreparedOutputRestoreJoin missingJoin = new(
            new SnapshotQuery(
                Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>()),
            missingFixture.Publication);
        RequireThrows(
            () => missingJoin.Build(missingPayload),
            "A PhysicalPending owner without an incoming physical batch was accepted.");
    }

    private static void VerifyIncomingMismatchesFailLoud()
    {
        VerifyIncomingMismatch(
            source => CloneBatch(
                source,
                totalMassGrams: source.TotalMassGrams + 1L,
                stackTransform: stack => CloneStack(
                    stack,
                    massGrams: stack.MassGrams + 1L)),
            "A one-gram incoming mismatch was accepted.");
        VerifyIncomingMismatch(
            source => CloneBatch(
                source,
                totalQuantity: source.TotalQuantity + 1,
                stackTransform: stack => CloneStack(
                    stack,
                    quantity: stack.Quantity + 1)),
            "An incoming quantity mismatch was accepted.");
        VerifyIncomingMismatch(
            source => CloneBatch(
                source,
                stackTransform: stack => CloneStack(
                    stack,
                    itemId: "item:qa:wrong-prepared-output")),
            "An incoming item mismatch was accepted.");
        VerifyIncomingMismatch(
            source => CloneBatch(
                source,
                stackTransform: stack => CloneStack(
                    stack,
                    destinationId:
                        "production:qa:wrong-prepared-output-buffer")),
            "An incoming destination mismatch was accepted.");
        VerifyIncomingMismatch(
            source => CloneBatch(
                source,
                plannedOutputFingerprint: ConflictingFingerprint,
                stackTransform: stack => CloneStack(
                    stack,
                    plannedOutputFingerprint: ConflictingFingerprint)),
            "An incoming admission fingerprint mismatch was accepted.");
        VerifyIncomingMismatch(
            source => new FacilityBufferPlannedOutputRestoreBatchSnapshot(
                source.BatchCommitId,
                source.OutcomeFingerprint,
                source.PlannedOutputFingerprint,
                source.TotalQuantity + 1,
                source.TotalMassGrams + UnitMassGrams,
                source.Stacks.Concat(new[]
                {
                    CloneStack(
                        source.Stacks.Single(),
                        stackOrdinal: source.Stacks.Max(value =>
                            value.StackOrdinal) + 1,
                        stackId: source.Stacks.Single().StackId + ":extra",
                        quantity: 1,
                        massGrams: UnitMassGrams)
                }).ToArray()),
            "An extra incoming physical stack was accepted.");
    }

    private static void VerifyIncomingMismatch(
        Func<FacilityBufferPlannedOutputRestoreBatchSnapshot,
            FacilityBufferPlannedOutputRestoreBatchSnapshot> mutate,
        string message)
    {
        Fixture fixture = new();
        FacilityBufferPlannedOutputRestoreBatchSnapshot conflicting =
            mutate(fixture.Incoming);
        ProductionPreparedOutputRestoreJoin join = new(
            new SnapshotQuery(new[] { conflicting }),
            fixture.Publication);
        RequireThrows(
            () => join.Build(fixture.CreatePayload(
                ProductionPreparedOutputPhase.PublicationPrepared)),
            message);
    }

    private static void VerifyPersistedCandidateMismatchFailsLoud()
    {
        Fixture fixture = new();
        DungeonProductionBillSaveData payload = fixture.CreatePayload(
            ProductionPreparedOutputPhase
                .PhysicalBatchCommittedPublicationPending);
        ProductionPreparedOutputPhysicalCandidateSaveData candidate =
            payload.bills.Single().preparedOutput.physicalCandidates.Single();
        candidate.stackId += ":mismatch";

        RequireThrows(
            () => fixture.CreateJoin().Build(payload),
            "A saved physical-candidate identity mismatch was accepted.");
    }

    private static void VerifyCompletedPendingAndEmptyJoinFailLoud()
    {
        Fixture completedFixture = new();
        RequireThrows(
            () => completedFixture.CreateJoin().Build(
                completedFixture.CreatePayload(
                    ProductionPreparedOutputPhase.Completed)),
            "Completed prepared output retained an unacknowledged physical marker.");

        Fixture emptyFixture = new();
        RequireThrows(
            () => EmptyProductionPreparedOutputRestoreJoin.Instance.Build(
                emptyFixture.CreatePayload(
                    ProductionPreparedOutputPhase.PublicationPrepared)),
            "The empty restore join accepted physical prepared-output authority.");
    }

    private sealed class Fixture
    {
        internal Fixture()
        {
            Mass = new FakeMassQuery();
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            FacilityBufferDestinationClaimRegistry claims = new();
            Require(
                claims.TryClaim(
                    new FacilityBufferDestinationClaim(
                        DestinationId,
                        DropPosition,
                        OwnerDomain,
                        DestinationId,
                        OwnerFacilityId,
                        FacilityBufferDestinationAnchorKind.LiveBuilding),
                    out _,
                    out _),
                "Restore-join fixture could not claim its output destination.");
            FacilityBufferMassAdmissionService admission = new(
                claims,
                new EmptyOccupancy(),
                Mass);
            Require(
                admission.TryReplaceOwnedProfiles(
                    OwnerDomain,
                    new[]
                    {
                        new FacilityBufferCapacityProfile(
                            DestinationId,
                            DropPosition,
                            OwnerDomain,
                            DestinationId,
                            OwnerFacilityId,
                            new PhysicalMassGrams(10_000L),
                            1L)
                    },
                    out _,
                    out _),
                "Restore-join fixture could not publish output capacity.");
            Publication = new FacilityBufferPlannedOutputPublicationService(
                Repository,
                new FakeCatalog(),
                Mass,
                admission);

            string batchCommitId = ProductionPreparedOutputIdentity
                .BuildBatchCommitId(BillId, CycleSequence, OutcomeFingerprint);
            FacilityBufferPlannedOutputRequest request = new(
                $"operation:{batchCommitId}",
                batchCommitId,
                OutcomeFingerprint,
                DestinationId,
                DropPosition,
                OwnerDomain,
                DestinationId,
                OwnerFacilityId,
                1L,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        OutputLineId,
                        PhysicalItemMassSubject.ForDefinition(
                            (ItemDefinitionId)ItemId),
                        Quantity)
                },
                CapacitySourceDigest,
                Quantity * UnitMassGrams * 4L);
            Require(
                admission.TryReservePlannedOutput(
                    request,
                    out FacilityBufferPlannedOutputToken token,
                    out _,
                    out _),
                "Restore-join fixture could not reserve exact output mass.");
            Require(
                Publication.TryPublishFullBatch(token, out _, out _, out _),
                "Restore-join fixture could not publish its physical batch.");
            Require(
                Publication.TryCapturePendingBatch(
                    batchCommitId,
                    out FacilityBufferPlannedOutputRestoreBatchSnapshot incoming,
                    out _,
                    out _),
                "Restore-join fixture could not capture its pending physical batch.");
            Incoming = incoming;
        }

        internal WorldItemRepository Repository { get; }
        internal IPhysicalItemMassQuery Mass { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication { get; }
        internal FacilityBufferPlannedOutputRestoreBatchSnapshot Incoming { get; }

        internal ProductionPreparedOutputRestoreJoin CreateJoin() => new(
            new SnapshotQuery(new[] { Incoming }),
            Publication);

        internal DungeonProductionBillSaveData CreatePayload(
            ProductionPreparedOutputPhase phase)
        {
            ProductionPreparedOutputBatchSaveData prepared = new()
            {
                phase = phase,
                billId = BillId.Value,
                cycleSequence = CycleSequence,
                recipeId = RecipeId,
                destinationId = DestinationId,
                recipeDefinitionDigest = RecipeDefinitionDigest,
                migrationProfileDigest = new string('f', 64),
                capacitySourceDigest = CapacitySourceDigest,
                outputBufferCycleCapacity = 4,
                projectedPortfolioCapacityGrams =
                    Quantity * UnitMassGrams * 4L,
                requiredMinimumCapacityGrams =
                    Quantity * UnitMassGrams * 4L,
                outcomeFingerprint = OutcomeFingerprint,
                admissionFingerprint = Incoming.PlannedOutputFingerprint,
                batchCommitId = Incoming.BatchCommitId,
                totalPhysicalMassGrams = Quantity * UnitMassGrams,
                totalDeclaredLossMassGrams = 0L,
                lines = new List<ProductionPreparedOutputLineSaveData>
                {
                    new()
                    {
                        outputLineId = OutputLineId,
                        role = ProductionOutputRole.Main,
                        itemId = ItemId,
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
                                OutputLineId,
                                ItemId,
                                ProductionOutputCapabilityIds.StandardDefinition,
                                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
                        quantity = Quantity,
                        componentPayload = string.Empty,
                        componentFingerprint = ComponentFingerprint,
                        qualityPermille = 1000,
                        rollKind = "inclusion",
                        rollValue = 0L,
                        rollUpperExclusive = 1L,
                        rollSucceeded = true,
                        exactMassGrams = Quantity * UnitMassGrams,
                        lineCommitId = ProductionPreparedOutputIdentity
                            .BuildLineCommitId(Incoming.BatchCommitId, OutputLineId)
                    }
                },
                physicalCandidates = phase is
                    ProductionPreparedOutputPhase
                        .PhysicalBatchCommittedPublicationPending
                    or ProductionPreparedOutputPhase.Completed
                    ? BuildCandidates(Incoming).ToList()
                    : new List<ProductionPreparedOutputPhysicalCandidateSaveData>()
            };
            return new DungeonProductionBillSaveData
            {
                bills = new List<ProductionBillSaveData>
                {
                    new()
                    {
                        billId = BillId.Value,
                        recipeId = RecipeId,
                        cycleSequence = CycleSequence,
                        outputDestinationId = DestinationId,
                        preparedOutput = prepared
                    }
                }
            };
        }

        internal void RequireDurableNonStackingProvenance()
        {
            FacilityBufferPlannedOutputPublicationEditorSnapshot snapshot =
                Publication.CaptureEditorTestSnapshot();
            Require(
                snapshot.Stacks.Count > 0
                && snapshot.Stacks.All(stack =>
                    stack.MarkerCount == 1
                    && !stack.MarkerAffectsStacking)
                && !Publication.TryCapturePendingBatch(
                    Incoming.BatchCommitId,
                    out _,
                    out _,
                    out _),
                "Restore acknowledgement did not leave durable non-stacking provenance.");
        }
    }

    private sealed class SnapshotQuery :
        IFacilityBufferPlannedOutputRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            FacilityBufferPlannedOutputRestoreBatchSnapshot> batches;

        internal SnapshotQuery(
            IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> source)
        {
            batches = Array.AsReadOnly((source
                    ?? Array.Empty<
                        FacilityBufferPlannedOutputRestoreBatchSnapshot>())
                .OrderBy(value => value?.BatchCommitId, StringComparer.Ordinal)
                .ToArray());
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
            Batches => batches;

        public bool TryGetBatch(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
        {
            batch = batches.SingleOrDefault(value =>
                value != null
                && string.Equals(
                    value.BatchCommitId,
                    batchCommitId,
                    StringComparison.Ordinal));
            return batch != null;
        }
    }

    private static ProductionPreparedOutputPhysicalCandidateSaveData[]
        BuildCandidates(FacilityBufferPlannedOutputRestoreBatchSnapshot incoming) =>
        incoming.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value =>
                new ProductionPreparedOutputPhysicalCandidateSaveData
                {
                    stackId = value.StackId,
                    batchCommitId = incoming.BatchCommitId,
                    outputLineId = value.OutputLineId,
                    lineCommitId = ProductionPreparedOutputIdentity
                        .BuildLineCommitId(
                            incoming.BatchCommitId,
                            value.OutputLineId),
                    itemId = value.ItemId,
                    quantity = value.Quantity,
                    massGrams = value.MassGrams,
                    destinationId = value.DestinationId,
                    state = ProductionPreparedPhysicalCandidateState
                        .FacilityOutputBuffer
                })
            .ToArray();

    private static FacilityBufferPlannedOutputRestoreBatchSnapshot CloneBatch(
        FacilityBufferPlannedOutputRestoreBatchSnapshot source,
        int? totalQuantity = null,
        long? totalMassGrams = null,
        string plannedOutputFingerprint = null,
        Func<FacilityBufferPlannedOutputRestoreStackSnapshot,
            FacilityBufferPlannedOutputRestoreStackSnapshot> stackTransform = null) =>
        new(
            source.BatchCommitId,
            source.OutcomeFingerprint,
            plannedOutputFingerprint ?? source.PlannedOutputFingerprint,
            totalQuantity ?? source.TotalQuantity,
            totalMassGrams ?? source.TotalMassGrams,
            source.Stacks.Select(value => stackTransform?.Invoke(value) ?? value)
                .ToArray());

    private static FacilityBufferPlannedOutputRestoreStackSnapshot CloneStack(
        FacilityBufferPlannedOutputRestoreStackSnapshot source,
        string plannedOutputFingerprint = null,
        string itemId = null,
        int? quantity = null,
        long? massGrams = null,
        string destinationId = null,
        int? stackOrdinal = null,
        string stackId = null) =>
        new(
            source.BatchCommitId,
            source.OutcomeFingerprint,
            plannedOutputFingerprint ?? source.PlannedOutputFingerprint,
            source.OutputLineId,
            stackOrdinal ?? source.StackOrdinal,
            stackId ?? source.StackId,
            itemId ?? source.ItemId,
            quantity ?? source.Quantity,
            massGrams ?? source.MassGrams,
            source.ComponentSignature,
            source.State,
            source.Position,
            destinationId ?? source.DestinationId);

    private sealed class FakeCatalog : IDungeonItemCatalogProvider
    {
        private readonly DungeonItemDefinition definition = new(
            ItemId,
            "Prepared Output Restore QA",
            string.Empty,
            StockCategory.General,
            1,
            null,
            1f,
            10);

        public IReadOnlyList<DungeonItemDefinition> All =>
            new[] { definition };

        public DungeonItemDefinition GetDefinition(string itemId) =>
            string.Equals(itemId, ItemId, StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(itemId);

        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition found)
        {
            found = string.Equals(itemId, ItemId, StringComparison.Ordinal)
                ? definition
                : null;
            return found != null;
        }
    }

    private sealed class FakeMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new PhysicalMassGrams(UnitMassGrams);

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            new PhysicalMassGrams(UnitMassGrams);

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) =>
            new PhysicalMassGrams(UnitMassGrams);

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            new PhysicalMassGrams(UnitMassGrams).Multiply(lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new PhysicalMassGrams(UnitMassGrams).Multiply(quantity);
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
}
#endif
