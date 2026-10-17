#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PlannedOutputProvenanceLifecycleDebugScenarios
{
    private const string PreparedBatchPrefix = "production-output-batch:";

    public static void RunAll()
    {
        VerifyIntactAcknowledgementRemainsExactCandidate();
        VerifyTerminalSplitBecomesHistoricalOnly();
        VerifyTerminalPartialConsumptionBecomesHistoricalOnly();
        VerifyPendingAndPreparedTopologyRemainStrict();
        VerifyFrozenHistoryTamperIsRejected();
        VerifyRetainedOwnerRejectsHistoricalOnlyBatch();
    }

    private static void VerifyIntactAcknowledgementRemainsExactCandidate()
    {
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture fixture =
            CreateAcknowledgedFixture("history-batch:intact");

        ValidateHistoricalMarkers(fixture);
        IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> exact =
            fixture.Publication.CaptureAcknowledgedRestoreBatchesForEditorTest();
        Require(
            exact.Count == 1
            && exact[0].Stacks.Count == fixture.Repository.Records.Count,
            "An intact acknowledged batch was not exposed as one exact restore candidate.");
    }

    private static void VerifyTerminalSplitBecomesHistoricalOnly()
    {
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture fixture =
            CreateAcknowledgedFixture("history-batch:split");
        WorldItemStackRecord source = fixture.Repository.Records
            .First(record => record.quantity > 1);
        Require(
            PlannedOutputPublicationComponentCodec.TryRead(
                source.components,
                out PlannedOutputPublicationMetadata before)
            && before.Acknowledged,
            "Split fixture has no acknowledged provenance marker.");

        // Synthetic raw-record mutation: production TryExtractReservedQuantity
        // clones these same non-custody components onto both split records.
        WorldItemStackRecord child = CloneSplitRecord(
            fixture.Repository,
            source,
            quantity: 1);
        source.quantity = checked(source.quantity - child.quantity);
        fixture.Repository.Add(child);
        fixture.Repository.MarkChanged();

        Require(
            PlannedOutputPublicationComponentCodec.TryRead(
                source.components,
                out PlannedOutputPublicationMetadata remainder)
            && PlannedOutputPublicationComponentCodec.TryRead(
                child.components,
                out PlannedOutputPublicationMetadata extracted)
            && remainder.Acknowledged
            && extracted.Acknowledged
            && remainder.BatchCommitId == before.BatchCommitId
            && extracted.BatchCommitId == before.BatchCommitId
            && remainder.OutputLineId == before.OutputLineId
            && extracted.OutputLineId == before.OutputLineId
            && remainder.StackOrdinal == before.StackOrdinal
            && extracted.StackOrdinal == before.StackOrdinal
            && remainder.Quantity == before.Quantity
            && extracted.Quantity == before.Quantity,
            "A terminal split did not preserve the frozen provenance on both records.");
        ValidateHistoricalMarkers(fixture);
        Require(
            fixture.Publication
                .CaptureAcknowledgedRestoreBatchesForEditorTest().Count == 0,
            "A split terminal history was exposed as an exact live restore batch.");
    }

    private static void VerifyTerminalPartialConsumptionBecomesHistoricalOnly()
    {
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture fixture =
            CreateAcknowledgedFixture("history-batch:partial-consume");
        WorldItemStackRecord consumed = fixture.Repository.Records
            .First(record => record.quantity > 1);
        Require(
            PlannedOutputPublicationComponentCodec.TryRead(
                consumed.components,
                out PlannedOutputPublicationMetadata before),
            "Partial-consumption fixture has no provenance marker.");

        // Synthetic raw-record mutation: production consumption decrements live
        // quantity without rewriting immutable acknowledged provenance.
        consumed.quantity = checked(consumed.quantity - 1);
        fixture.Repository.MarkChanged();

        Require(
            PlannedOutputPublicationComponentCodec.TryRead(
                consumed.components,
                out PlannedOutputPublicationMetadata after)
            && after.Acknowledged
            && after.BatchCommitId == before.BatchCommitId
            && after.OutputLineId == before.OutputLineId
            && after.StackOrdinal == before.StackOrdinal
            && after.Quantity == before.Quantity
            && consumed.quantity != after.Quantity,
            "Partial consumption rewrote or lost the frozen provenance envelope.");
        ValidateHistoricalMarkers(fixture);
        Require(
            fixture.Publication
                .CaptureAcknowledgedRestoreBatchesForEditorTest().Count == 0,
            "A partially consumed terminal history was exposed as an exact restore batch.");
    }

    private static void VerifyPendingAndPreparedTopologyRemainStrict()
    {
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture pending =
            new();
        FacilityBufferPlannedOutputToken pendingToken = pending.Reserve(
            "history-batch:pending-strict");
        Require(
            pending.Publication.TryPublishFullBatch(
                pendingToken,
                out _,
                out _,
                out string pendingFailure),
            "Pending strict fixture could not publish: " + pendingFailure);
        pending.Repository.Records.First(record => record.quantity > 1).quantity--;
        pending.Repository.MarkChanged();
        RequireThrows(
            () => pending.Publication
                .CapturePendingRestoreBatchesForEditorTest(),
            "A quantity-drifted pending publication was accepted as exact.");
        RequireThrows(
            () => ValidateHistoricalMarkers(pending),
            "Raw lifecycle validation accepted a quantity-drifted pending publication.");

        string preparedId = PreparedBatchPrefix
            + "qa:lifecycle:"
            + new string('a', 64);
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture prepared =
            CreateAcknowledgedFixture(preparedId);
        WorldItemStackRecord preparedSource = prepared.Repository.Records
            .First(record => record.quantity > 1);
        WorldItemStackRecord preparedChild = CloneSplitRecord(
            prepared.Repository,
            preparedSource,
            quantity: 1);
        preparedSource.quantity--;
        prepared.Repository.Add(preparedChild);
        prepared.Repository.MarkChanged();
        RequireThrows(
            () => prepared.Publication
                .CaptureAcknowledgedRestoreBatchesForEditorTest(),
            "A split prepared-prefix acknowledgement was downgraded to terminal history.");
        RequireThrows(
            () => ValidateHistoricalMarkers(prepared),
            "Raw lifecycle validation accepted a split prepared-prefix acknowledgement.");
    }

    private static void VerifyFrozenHistoryTamperIsRejected()
    {
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture fixture =
            CreateAcknowledgedFixture("history-batch:tamper");
        WorldItemStackRecord source = fixture.Repository.Records
            .First(record => record.quantity > 1);
        WorldItemStackRecord child = CloneSplitRecord(
            fixture.Repository,
            source,
            quantity: 1);
        source.quantity--;
        fixture.Repository.Add(child);
        fixture.Repository.MarkChanged();

        ItemInstanceComponentSaveData marker = child.components.Single(
            PlannedOutputPublicationComponentCodec.IsAnyMarker);
        ItemStateValueSaveData frozenBatchQuantity = marker.values.Single(value =>
            string.Equals(
                value.key,
                "batch-quantity",
                StringComparison.Ordinal));
        frozenBatchQuantity.integerValue = checked(
            frozenBatchQuantity.integerValue + 1L);

        RequireThrows(
            () => ValidateHistoricalMarkers(fixture),
            "Conflicting frozen terminal provenance was accepted by raw validation.");
        RequireThrows(
            () => fixture.Publication
                .CaptureAcknowledgedRestoreBatchesForEditorTest(),
            "Conflicting frozen terminal provenance was silently omitted from exact capture.");
    }

    private static void VerifyRetainedOwnerRejectsHistoricalOnlyBatch()
    {
        PhysicalItemExactSourcePublicationPlan plan = new(
            "qa.exact-source",
            "historical-only",
            new Vector2Int(9, 4),
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                    "line:a",
                    PhysicalItemMassSubject.ForDefinition(
                        (ItemDefinitionId)"item:qa:a"),
                    5),
                new FacilityBufferPlannedOutputSlice(
                    "line:b",
                    PhysicalItemMassSubject.ForDefinition(
                        (ItemDefinitionId)"item:qa:b"),
                    2)
            });
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture fixture =
            CreateAcknowledgedFixture(plan.BatchCommitId);
        WorldItemStackRecord source = fixture.Repository.Records
            .First(record => record.quantity > 1);
        WorldItemStackRecord child = CloneSplitRecord(
            fixture.Repository,
            source,
            quantity: 1);
        source.quantity--;
        fixture.Repository.Add(child);
        fixture.Repository.MarkChanged();

        IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> exact =
            fixture.Publication.CaptureAcknowledgedRestoreBatchesForEditorTest();
        Require(
            exact.Count == 0,
            "Historical-only exact-source provenance remained an exact candidate.");
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery candidates =
            EmptyFacilityBufferAcknowledgedOutputRestoreCandidateQuery.Instance;
        FacilityBufferDestinationClaimRegistry claims = new();
        FacilityBufferMassAdmissionService admission = new(
            claims,
            new FacilityBufferPlannedOutputPublicationDebugScenarios
                .EmptyOccupancy(),
            fixture.Mass);
        FacilityBufferDestinationLifecycleService lifecycle = new(
            claims,
            claims,
            admission,
            admission);
        PhysicalItemExactSourcePublicationService exactSource = new(
            fixture.Repository,
            fixture.Mass,
            lifecycle,
            claims,
            admission,
            admission,
            fixture.Publication,
            new UnusedDestinationRelease(),
            new UnusedBatchDisposition(),
            candidates);
        PhysicalItemExactSourceRestoreDescriptor descriptor = new(
            plan,
            fixture.Repository.Records
                .Select(record => record.stackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        bool restored = exactSource.TryReplaceRestoreAuthorities(
            new[] { plan.OwnerDomain },
            new[] { descriptor },
            out string failureReason);

        Require(
            candidates.IsCandidateAvailable
            && !restored
            && string.Equals(
                failureReason,
                "physical-exact-source-restore-batch-missing-or-drifted:"
                + plan.BatchCommitId,
                StringComparison.Ordinal)
            && claims.CaptureClaims().Count == 0
            && admission.CaptureProfiles().Count == 0,
            "A retained owner accepted a historical-only acknowledged batch: "
            + failureReason);
    }

    private static FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture
        CreateAcknowledgedFixture(string batchCommitId)
    {
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture fixture =
            new();
        FacilityBufferPlannedOutputToken token = fixture.Reserve(batchCommitId);
        Require(
            fixture.Publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt publication,
                out _,
                out string publicationFailure),
            "Acknowledged fixture publication failed: " + publicationFailure);
        Require(
            fixture.Admission.TryCommitPlannedOutput(
                token,
                publication,
                out _,
                out _,
                out string admissionFailure),
            "Acknowledged fixture admission failed: " + admissionFailure);
        Require(
            fixture.Publication.TryAcknowledgePublishedBatch(
                publication,
                out _,
                out string acknowledgementFailure),
            "Acknowledged fixture conversion failed: " + acknowledgementFailure);
        return fixture;
    }

    private static void ValidateHistoricalMarkers(
        FacilityBufferPlannedOutputPublicationDebugScenarios.Fixture fixture)
    {
        List<ProductionPreparedOutputRoutingBatchSaveData> routingOwners =
            fixture.Repository.Records
                .Select(record => PlannedOutputPublicationComponentCodec.TryRead(
                    record.components,
                    out PlannedOutputPublicationMetadata metadata)
                        ? metadata
                        : default)
                .Where(metadata => metadata.Acknowledged
                    && metadata.BatchCommitId.StartsWith(
                        PreparedBatchPrefix,
                        StringComparison.Ordinal))
                .Select(metadata => metadata.BatchCommitId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(value => new ProductionPreparedOutputRoutingBatchSaveData
                {
                    batchCommitId = value,
                    lines = new List<ProductionPreparedOutputRoutingLineSaveData>()
                })
                .ToList();
        DungeonPhysicalItemSaveData physical = new()
        {
            stacks = fixture.Repository.Records
                .Where(record => record != null && record.quantity > 0)
                .OrderBy(record => record.stackId, StringComparer.Ordinal)
                .Select(WorldItemPersistenceService.CaptureDurableStack)
                .ToList(),
            pendingExactOutputRoutes = new List<
                FacilityOutputExactRouteOutboxSaveData>()
        };
        ProductionAcknowledgedOutputLifecycleSaveValidation.ValidateCore(
            physical,
            new ProductionPreparedOutputRoutingSaveData
            {
                batches = routingOwners
            });
    }

    private static WorldItemStackRecord CloneSplitRecord(
        WorldItemRepository repository,
        WorldItemStackRecord source,
        int quantity) => new()
    {
        stackId = repository.AllocateStackId(),
        itemInstanceId = source.itemInstanceId,
        itemId = source.itemId,
        quantity = quantity,
        state = source.state,
        position = source.position,
        destinationId = source.destinationId,
        aggregationCohortId = source.aggregationCohortId,
        sourceStorageDestinationId = source.sourceStorageDestinationId,
        hasDestinationPosition = source.hasDestinationPosition,
        destinationPosition = source.destinationPosition,
        forbidden = source.forbidden,
        sourceCharacterId = source.sourceCharacterId,
        sourceDisplayName = source.sourceDisplayName,
        sourceSpeciesTag = source.sourceSpeciesTag,
        sourceDeathReason = source.sourceDeathReason,
        emergencyButcheryAllowed = source.emergencyButcheryAllowed,
        wasteOrigin = source.wasteOrigin,
        contamination = source.contamination,
        components = (source.components
                ?? new List<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList()
    };

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

    private sealed class UnusedDestinationRelease :
        IFacilityBufferDestinationReleaseService
    {
        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            releasedQuantity = 0;
            failureReason = "unused";
            return false;
        }
    }

    private sealed class UnusedBatchDisposition :
        IPhysicalItemBatchDispositionService
    {
        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => Unsupported(
            out receipt,
            out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => Unsupported(
            out receipt,
            out failureReason);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            failureReason = "unused";
            return false;
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }

        private static bool Unsupported(
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "unused";
            return false;
        }
    }
}
#endif
