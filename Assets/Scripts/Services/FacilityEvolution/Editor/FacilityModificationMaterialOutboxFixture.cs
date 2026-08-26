using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FacilityModificationMaterialOutboxFixture
{
    private const string BindingItemId = "resource:dark-resin";
    private const string DestinationId =
        "facility-input:modification:qa";

    public static bool Run()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        if (!VerifyAtomicMissingInput(catalog))
        {
            return false;
        }

        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemMassQuery massQuery = new(catalog);
        PhysicalItemBatchDispositionService inner = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance);
        FailOnce service = new(inner) { FailNext = true };
        WorldItemQueryService query = new(
            catalog,
            massQuery,
            repository,
            EditorNullItemMarkerPresenter.Instance);

        string catalystItemId = EvolutionCatalystItemId.BuildCatalyst(
            "industry",
            1);
        string bindingA = Add(
            repository,
            BindingItemId,
            1,
            new Vector2Int(2, 3));
        string bindingB = Add(
            repository,
            BindingItemId,
            1,
            new Vector2Int(3, 3));
        string catalyst = Add(
            repository,
            catalystItemId,
            1,
            new Vector2Int(4, 3));
        FacilityModificationOrder order = CreateOrder(catalystItemId);

        if (FacilityModificationMaterialOutbox.TryCommitOrFinalize(
                order,
                query.GetAllStacks(),
                service,
                out _)
            || !order.materialsConsumed
            || !order.materialTransferOutcomePublished
            || order.state != EvolutionReforgeOrderState.Ready
            || order.materialTransferInputs.Count != 3
            || !service.TryGetPending(
                order.materialTransferOperationId,
                out PhysicalItemBatchDispositionReceipt receipt)
            || repository.GetEditorTestQuantity(bindingA) != 0
            || repository.GetEditorTestQuantity(bindingB) != 0
            || repository.GetEditorTestQuantity(catalyst) != 0)
        {
            return false;
        }

        string saved = JsonUtility.ToJson(order);
        FacilityModificationOrder restored =
            JsonUtility.FromJson<FacilityModificationOrder>(saved);
        if (!FacilityModificationMaterialOutbox.TryCommitOrFinalize(
                restored,
                query.GetAllStacks(),
                service,
                out _)
            || !restored.materialsConsumed
            || restored.materialTransferOperationId.Length != 0
            || restored.materialTransferInputs.Count != 0
            || !FacilityModificationMaterialOutbox.TryCommitOrFinalize(
                restored,
                query.GetAllStacks(),
                service,
                out _)
            || repository.GetEditorTestQuantity(bindingA) != 0
            || repository.GetEditorTestQuantity(bindingB) != 0
            || repository.GetEditorTestQuantity(catalyst) != 0)
        {
            return false;
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            order.materialTransferRequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        FacilityEvolutionPendingMaterialRestoreGuard
            .ValidateModificationMaterialOwnerSet(
                new[] { order },
                new CandidateQuery(candidate));
        if (!Reject(new[] { order }, new CandidateQuery())
            || !Reject(
                Array.Empty<FacilityModificationOrder>(),
                new CandidateQuery(candidate)))
        {
            return false;
        }

        PhysicalItemRestoreCandidateDispositionSnapshot massMismatch =
            Copy(candidate, inputMassGrams: candidate.InputMassGrams + 1);
        PhysicalItemRestoreCandidateDispositionSnapshot sourceMismatch =
            Copy(
                candidate,
                sourceStackIds: candidate.SourceStackIds
                    .Select((value, index) => index == 0
                        ? value + ":mismatch"
                        : value)
                    .ToArray());
        PhysicalItemRestoreCandidateDispositionSnapshot fingerprintMismatch =
            Copy(
                candidate,
                requestFingerprint:
                    candidate.RequestFingerprint + ":mismatch");
        return Reject(new[] { order }, new CandidateQuery(massMismatch))
            && Reject(new[] { order }, new CandidateQuery(sourceMismatch))
            && Reject(
                new[] { order },
                new CandidateQuery(fingerprintMismatch));
    }

    private static bool VerifyAtomicMissingInput(
        IDungeonItemCatalogProvider catalog)
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemMassQuery massQuery = new(catalog);
        PhysicalItemBatchDispositionService service = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance);
        WorldItemQueryService query = new(
            catalog,
            massQuery,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        string binding = Add(
            repository,
            BindingItemId,
            2,
            new Vector2Int(1, 1));
        FacilityModificationOrder order = CreateOrder(
            EvolutionCatalystItemId.BuildCatalyst("industry", 1));

        return !FacilityModificationMaterialOutbox.TryCommitOrFinalize(
                order,
                query.GetAllStacks(),
                service,
                out _)
            && repository.GetEditorTestQuantity(binding) == 2
            && string.IsNullOrEmpty(order.materialTransferOperationId)
            && !order.materialsConsumed;
    }

    private static FacilityModificationOrder CreateOrder(
        string catalystItemId) => new()
    {
        orderId = "facility-modification:qa",
        facilityPersistentId = "facility:qa",
        bindingItemId = BindingItemId,
        bindingAmount = 2,
        catalystItemId = catalystItemId,
        catalystAmount = 1,
        destinationId = DestinationId,
        state = EvolutionReforgeOrderState.WaitingForMaterials,
        candidate = new FacilityGenerationCandidate
        {
            candidateId = "candidate:qa",
            targetGeneration = 1,
            benefitModuleId = "facility:output",
            historyHash = "history:qa"
        }
    };

    private static string Add(
        WorldItemRepository repository,
        string itemId,
        int quantity,
        Vector2Int position) =>
        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            quantity,
            WorldItemStackState.FacilityBuffer,
            position: position,
            destinationId: DestinationId);

    private static bool Reject(
        IReadOnlyList<FacilityModificationOrder> orders,
        CandidateQuery query)
    {
        try
        {
            FacilityEvolutionPendingMaterialRestoreGuard
                .ValidateModificationMaterialOwnerSet(orders, query);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot Copy(
        PhysicalItemRestoreCandidateDispositionSnapshot source,
        string requestFingerprint = null,
        IReadOnlyList<string> sourceStackIds = null,
        long? inputMassGrams = null) => new(
        source.Kind,
        source.OperationId,
        source.ReasonCode,
        requestFingerprint ?? source.RequestFingerprint,
        sourceStackIds ?? source.SourceStackIds,
        source.Quantity,
        inputMassGrams ?? source.InputMassGrams,
        source.CommitId);

    private sealed class CandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> values;

        public CandidateQuery(
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
            value = values.FirstOrDefault(candidate => string.Equals(
                candidate.OperationId,
                operationId,
                StringComparison.Ordinal));
            return value != null;
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
            out string failureReason) => inner.TryCommit(
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
            out string failureReason) => inner.TryCommitPending(
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
