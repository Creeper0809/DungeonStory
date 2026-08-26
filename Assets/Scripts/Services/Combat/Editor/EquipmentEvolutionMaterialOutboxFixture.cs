using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EquipmentEvolutionMaterialOutboxFixture
{
    private const string MaterialItemId = "resource:dark-resin";
    private static readonly string CatalystItemId =
        EvolutionCatalystItemId.BuildCatalyst("industry", 1);

    public static bool Run()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        return VerifyMissingInputIsAtomic(catalog)
            && VerifyReforgeOutbox(catalog)
            && VerifyReattunementOutbox(catalog);
    }

    private static bool VerifyMissingInputIsAtomic(
        IDungeonItemCatalogProvider catalog)
    {
        TestContext context = new(catalog);
        EvolutionReforgeOrder order = CreateReforgeOrder("missing");
        string material = context.Add(
            MaterialItemId,
            2,
            order.destinationId);

        return !EquipmentEvolutionMaterialOutbox.TryCommitOrFinalize(
                order,
                context.Query.GetAllStacks(),
                context.Service,
                "equipment-stack:missing",
                out _)
            && context.Repository.GetEditorTestQuantity(material) == 2
            && string.IsNullOrEmpty(order.materialTransferOperationId)
            && !order.materialsConsumed;
    }

    private static bool VerifyReforgeOutbox(
        IDungeonItemCatalogProvider catalog)
    {
        TestContext context = new(catalog, failAcknowledgement: true);
        EvolutionReforgeOrder order = CreateReforgeOrder("qa");
        string equipmentStack = context.Add(
            MaterialItemId,
            1,
            order.destinationId);
        string materialA = context.Add(
            MaterialItemId,
            1,
            order.destinationId);
        string materialB = context.Add(
            MaterialItemId,
            1,
            order.destinationId);
        string catalyst = context.Add(
            CatalystItemId,
            1,
            order.destinationId);

        if (EquipmentEvolutionMaterialOutbox.TryCommitOrFinalize(
                order,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                out _)
            || !order.materialsConsumed
            || !order.materialTransferOutcomePublished
            || order.materialTransferInputs.Count != 3
            || context.Repository.GetEditorTestQuantity(equipmentStack) != 1
            || context.Repository.GetEditorTestQuantity(materialA) != 0
            || context.Repository.GetEditorTestQuantity(materialB) != 0
            || context.Repository.GetEditorTestQuantity(catalyst) != 0
            || !context.Service.TryGetPending(
                order.materialTransferOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return false;
        }

        string json = JsonUtility.ToJson(order);
        EvolutionReforgeOrder restored =
            JsonUtility.FromJson<EvolutionReforgeOrder>(json);
        if (!EquipmentEvolutionMaterialOutbox.TryCommitOrFinalize(
                restored,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                out _)
            || !restored.materialsConsumed
            || !string.IsNullOrEmpty(restored.materialTransferOperationId)
            || restored.materialTransferInputs.Count != 0
            || !EquipmentEvolutionMaterialOutbox.TryCommitOrFinalize(
                restored,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                out _)
            || context.Repository.GetEditorTestQuantity(equipmentStack) != 1)
        {
            return false;
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate =
            Candidate(receipt, order.materialTransferRequestFingerprint);
        EquipmentEvolutionMaterialRestoreGuard.ValidateOwnerSet(
            new[] { order },
            Array.Empty<EquipmentReattunementOrder>(),
            new CandidateQuery(candidate));
        return Reject(
                new[] { order },
                Array.Empty<EquipmentReattunementOrder>(),
                new CandidateQuery())
            && Reject(
                Array.Empty<EvolutionReforgeOrder>(),
                Array.Empty<EquipmentReattunementOrder>(),
                new CandidateQuery(candidate))
            && Reject(
                new[] { order },
                Array.Empty<EquipmentReattunementOrder>(),
                new CandidateQuery(Copy(
                    candidate,
                    inputMassGrams: candidate.InputMassGrams + 1)))
            && Reject(
                new[] { order },
                Array.Empty<EquipmentReattunementOrder>(),
                new CandidateQuery(Copy(
                    candidate,
                    requestFingerprint:
                        candidate.RequestFingerprint + ":mismatch")))
            && Reject(
                new[] { order },
                Array.Empty<EquipmentReattunementOrder>(),
                new CandidateQuery(Copy(
                    candidate,
                    sourceStackIds: candidate.SourceStackIds
                        .Select((value, index) => index == 0
                            ? value + ":mismatch"
                            : value)
                        .ToArray())));
    }

    private static bool VerifyReattunementOutbox(
        IDungeonItemCatalogProvider catalog)
    {
        TestContext context = new(catalog, failAcknowledgement: true);
        EquipmentReattunementOrder order = CreateReattunementOrder("qa");
        string equipmentStack = context.Add(
            MaterialItemId,
            1,
            order.destinationId);
        string catalyst = context.Add(
            CatalystItemId,
            1,
            order.destinationId);
        if (EquipmentEvolutionMaterialOutbox.TryCommitOrFinalize(
                order,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                out _)
            || !order.materialsConsumed
            || order.materialTransferInputs.Count != 1
            || context.Repository.GetEditorTestQuantity(equipmentStack) != 1
            || context.Repository.GetEditorTestQuantity(catalyst) != 0
            || !context.Service.TryGetPending(
                order.materialTransferOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return false;
        }

        EquipmentReattunementOrder restored =
            JsonUtility.FromJson<EquipmentReattunementOrder>(
                JsonUtility.ToJson(order));
        if (!EquipmentEvolutionMaterialOutbox.TryCommitOrFinalize(
                restored,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                out _)
            || !restored.materialsConsumed
            || !string.IsNullOrEmpty(restored.materialTransferOperationId)
            || context.Repository.GetEditorTestQuantity(equipmentStack) != 1)
        {
            return false;
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate =
            Candidate(receipt, order.materialTransferRequestFingerprint);
        EquipmentEvolutionMaterialRestoreGuard.ValidateOwnerSet(
            Array.Empty<EvolutionReforgeOrder>(),
            new[] { order },
            new CandidateQuery(candidate));
        return Reject(
            Array.Empty<EvolutionReforgeOrder>(),
            new[] { order },
            new CandidateQuery(Copy(
                candidate,
                requestFingerprint:
                    candidate.RequestFingerprint + ":mismatch")));
    }

    private static EvolutionReforgeOrder CreateReforgeOrder(string suffix)
    {
        string orderId = "reforge:" + suffix;
        return new EvolutionReforgeOrder
        {
            orderId = orderId,
            equipmentInstanceId = "equipment:" + suffix,
            facilityPersistentId = "facility:smithy:qa",
            targetGeneration = 1,
            direction = EquipmentEvolutionDirection.Balanced,
            catalystItemId = CatalystItemId,
            catalystFamily = "industry",
            catalystPotency = 1,
            catalystSourceTags = new List<string> { "industry" },
            primaryMaterialItemId = MaterialItemId,
            primaryMaterialAmount = 1,
            bindingItemId = MaterialItemId,
            bindingAmount = 1,
            requiredWork = 10f,
            state = EvolutionReforgeOrderState.WaitingForMaterials,
            destinationId = "facility-reforge:" + orderId,
            lockedHistoryHash = "history:" + suffix,
            lockedDirection = EquipmentEvolutionDirection.Balanced
        };
    }

    private static EquipmentReattunementOrder CreateReattunementOrder(
        string suffix)
    {
        string orderId = "reattune:" + suffix;
        return new EquipmentReattunementOrder
        {
            orderId = orderId,
            equipmentInstanceId = "equipment:reattune:" + suffix,
            facilityPersistentId = "facility:smithy:qa",
            targetNodeId = "node:" + suffix,
            targetActive = true,
            resultingActiveNodeIds = new List<string> { "node:" + suffix },
            catalystItemId = CatalystItemId,
            catalystPotency = 1,
            requiredWork = 8f,
            state = EvolutionReforgeOrderState.WaitingForMaterials,
            destinationId = "facility-reattune:" + orderId,
            lockedStateHash = "state:" + suffix
        };
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot Candidate(
        PhysicalItemBatchDispositionReceipt receipt,
        string requestFingerprint) => new(
        receipt.Kind,
        receipt.OperationId,
        receipt.ReasonCode,
        requestFingerprint,
        receipt.SourceStackIds,
        receipt.Quantity,
        receipt.InputMassGrams,
        receipt.CommitId);

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

    private static bool Reject(
        IReadOnlyList<EvolutionReforgeOrder> reforge,
        IReadOnlyList<EquipmentReattunementOrder> reattunement,
        CandidateQuery query)
    {
        try
        {
            EquipmentEvolutionMaterialRestoreGuard.ValidateOwnerSet(
                reforge,
                reattunement,
                query);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class TestContext
    {
        internal TestContext(
            IDungeonItemCatalogProvider catalog,
            bool failAcknowledgement = false)
        {
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemMassQuery massQuery = new(catalog);
            PhysicalItemBatchDispositionService inner = new(
                Repository,
                massQuery,
                EditorNullItemMarkerPresenter.Instance);
            Service = new FailOnce(inner)
            {
                FailNext = failAcknowledgement
            };
            Query = new WorldItemQueryService(
                catalog,
                massQuery,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
        }

        internal WorldItemRepository Repository { get; }
        internal FailOnce Service { get; }
        internal WorldItemQueryService Query { get; }

        internal string Add(
            string itemId,
            int quantity,
            string destinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                itemId,
                quantity,
                WorldItemStackState.FacilityBuffer,
                position: Vector2Int.zero,
                destinationId: destinationId);
    }

    private sealed class CandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> values;

        internal CandidateQuery(
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

        internal FailOnce(IPhysicalItemBatchDispositionService inner) =>
            this.inner = inner;

        internal bool FailNext { get; set; }

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
