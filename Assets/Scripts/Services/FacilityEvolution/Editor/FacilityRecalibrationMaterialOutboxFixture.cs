using System;
using System.Collections.Generic;
using UnityEngine;

public static class FacilityRecalibrationMaterialOutboxFixture
{
    public static bool Run()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService inner = new(
            repository,
            new PhysicalItemMassQuery(catalog),
            EditorNullItemMarkerPresenter.Instance);
        WorldItemQueryService itemQuery = new(
            catalog,
            new PhysicalItemMassQuery(catalog),
            repository,
            EditorNullItemMarkerPresenter.Instance);
        FailOnce service = new(inner) { FailNext = true };

        string catalystItemId = EvolutionCatalystItemId.BuildCatalyst(
            "industry",
            1);
        const string destinationId = "facility-input:recalibration:qa";
        string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            catalystItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            position: new Vector2Int(4, 3),
            destinationId: destinationId);
        FacilityRecalibrationOrder order = new()
        {
            orderId = "facility-recalibration:qa",
            facilityPersistentId = "facility:qa",
            nodeId = "node:qa",
            catalystItemId = catalystItemId,
            catalystPotency = 1,
            destinationId = destinationId,
            state = EvolutionReforgeOrderState.WaitingForMaterials
        };

        if (FacilityRecalibrationMaterialOutbox.TryCommitOrFinalize(
                order,
                itemQuery.GetAllStacks(),
                service,
                out _)
            || !order.materialsConsumed
            || !order.materialTransferOutcomePublished
            || !service.TryGetPending(
                order.materialTransferOperationId,
                out PhysicalItemBatchDispositionReceipt receipt)
            || repository.GetEditorTestQuantity(sourceStackId) != 1)
        {
            return false;
        }

        string saved = JsonUtility.ToJson(order);
        FacilityRecalibrationOrder restored =
            JsonUtility.FromJson<FacilityRecalibrationOrder>(saved);
        if (!FacilityRecalibrationMaterialOutbox.TryCommitOrFinalize(
                restored,
                itemQuery.GetAllStacks(),
                service,
                out _)
            || repository.GetEditorTestQuantity(sourceStackId) != 1
            || !restored.materialsConsumed
            || restored.materialTransferOperationId.Length != 0
            || !FacilityRecalibrationMaterialOutbox.TryCommitOrFinalize(
                restored,
                itemQuery.GetAllStacks(),
                service,
                out _)
            || repository.GetEditorTestQuantity(sourceStackId) != 1)
        {
            return false;
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            "fixture",
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        FacilityEvolutionPendingMaterialRestoreGuard
            .ValidateRecalibrationMaterialOwnerSet(
                new[] { order },
                new Query(candidate));

        if (!Reject(new[] { order }, new Query())
            || !Reject(
                Array.Empty<FacilityRecalibrationOrder>(),
                new Query(candidate)))
        {
            return false;
        }

        PhysicalItemRestoreCandidateDispositionSnapshot massMismatch = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            "fixture",
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams + 1,
            receipt.CommitId);
        PhysicalItemRestoreCandidateDispositionSnapshot sourceMismatch = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            "fixture",
            new[] { sourceStackId + ":mismatch" },
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        return Reject(new[] { order }, new Query(massMismatch))
            && Reject(new[] { order }, new Query(sourceMismatch));
    }

    private static bool Reject(
        IReadOnlyList<FacilityRecalibrationOrder> orders,
        Query query)
    {
        try
        {
            FacilityEvolutionPendingMaterialRestoreGuard
                .ValidateRecalibrationMaterialOwnerSet(orders, query);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Query : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> values;

        public Query(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values)
        {
            this.values = values;
        }

        public bool IsCandidateAvailable => true;

        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot value)
        {
            foreach (PhysicalItemRestoreCandidateDispositionSnapshot item in values)
            {
                if (string.Equals(
                        item.OperationId,
                        operationId,
                        StringComparison.Ordinal))
                {
                    value = item;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }

    private sealed class FailOnce : IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        public FailOnce(IPhysicalItemBatchDispositionService inner)
        {
            this.inner = inner;
        }

        public bool FailNext { get; set; }

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) =>
            inner.TryCommit(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) =>
            inner.TryCommitPending(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            if (FailNext)
            {
                FailNext = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }

            return inner.Acknowledge(commitId, out failureReason);
        }
    }
}
