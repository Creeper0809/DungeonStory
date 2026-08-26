#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResourceStockPolicySaleOutboxDebugScenarios
{
    private const string ItemId = "material:lumber";

    [MenuItem("DungeonStory/Debug/Economy/Run Stock Policy Sale Outbox Contracts")]
    public static void RunAll()
    {
        Debug.Log(Verify());
    }

    public static string Verify()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(catalog);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService dispositions = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance);
        PhysicalFacilityItemSinkGateway gateway = new(
            new PhysicalStockQuery(repository, catalog, massQuery),
            dispositions);
        string destinationId =
            ResourceStockPolicySaleOutbox.FormatDestinationId(ItemId);
        string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            quantity: 3,
            state: WorldItemStackState.FacilityBuffer,
            destinationId: destinationId,
            position: new Vector2Int(3, 2));
        int sequence = 1;
        string operationId = ResourceStockPolicySaleOutbox.FormatOperationId(
            ItemId,
            sequence);
        Require(gateway.TryCommitTransferPending(
                destinationId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [ItemId] = 2
                },
                operationId,
                ResourceStockPolicySaleOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt physicalReceipt,
                out string commitFailure),
            "Stock-policy physical transfer failed: " + commitFailure);

        ResourceStockPolicyPendingSale pending =
            ResourceStockPolicySaleOutbox.CreatePending(
                sequence,
                ItemId,
                proceeds: 7,
                ToSaleReceipt(physicalReceipt));
        PhysicalItemRestoreCandidateDispositionSnapshot candidateReceipt =
            ToCandidateReceipt(physicalReceipt);
        DungeonResourceStockPolicySaveData payload = new()
        {
            nextSaleSequence = 2,
            pendingSales = new List<ResourceStockPolicyPendingSale>
            {
                pending.Clone()
            }
        };
        ResourceStockPolicySaveSection.ValidatePhysicalRestoreCandidate(
            payload,
            new CandidateQuery(candidateReceipt));
        Require(Throws(() =>
                ResourceStockPolicySaveSection.ValidatePhysicalRestoreCandidate(
                    payload,
                    CandidateQuery.Empty)),
            "Stock-policy restore accepted a missing incoming Transfer.");
        Require(Throws(() =>
                ResourceStockPolicySaveSection.ValidatePhysicalRestoreCandidate(
                    new DungeonResourceStockPolicySaveData(),
                    new CandidateQuery(candidateReceipt))),
            "Stock-policy restore accepted an orphan incoming Transfer.");
        PhysicalItemRestoreCandidateDispositionSnapshot mismatched = new(
            candidateReceipt.Kind,
            candidateReceipt.OperationId,
            candidateReceipt.ReasonCode,
            candidateReceipt.RequestFingerprint,
            candidateReceipt.SourceStackIds,
            candidateReceipt.Quantity,
            candidateReceipt.InputMassGrams + 1L,
            candidateReceipt.CommitId);
        Require(Throws(() =>
                ResourceStockPolicySaveSection.ValidatePhysicalRestoreCandidate(
                    payload,
                    new CandidateQuery(mismatched))),
            "Stock-policy restore accepted mismatched physical mass provenance.");

        FailOnceCommandPort first = new(gateway)
        {
            FailNextAcknowledgement = true
        };
        Require(!ResourceStockPolicySaleOutbox.TryFinalizePending(
                pending,
                first,
                out _)
            && pending.phase ==
                ResourceStockPolicySaleCommitPhase.IncomePublished
            && first.IncomePublicationCount == 1
            && first.PublishedIncome == 7
            && repository.GetEditorPendingBatchDispositionCount() == 1
            && repository.GetEditorTestQuantity(sourceStackId) == 1,
            "Acknowledgement fault did not preserve exact sale outbox state.");

        ResourceStockPolicyPendingSale restored =
            JsonUtility.FromJson<ResourceStockPolicyPendingSale>(
                JsonUtility.ToJson(pending));
        FailOnceCommandPort recovery = new(gateway);
        Require(ResourceStockPolicySaleOutbox.TryFinalizePending(
                restored,
                recovery,
                out string recoveryFailure)
            && string.IsNullOrEmpty(recoveryFailure)
            && recovery.IncomePublicationCount == 0
            && recovery.SuccessfulAcknowledgements == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && repository.GetEditorTestQuantity(sourceStackId) == 1,
            "Sale outbox recovery did not perform acknowledgement-only completion.");

        return "Resource stock-policy exact Transfer/outbox contracts PASS";
    }

    private static ResourceStockPolicySaleTransferReceipt ToSaleReceipt(
        PhysicalItemBatchDispositionReceipt receipt) => new()
    {
        operationId = receipt.OperationId,
        reasonCode = receipt.ReasonCode,
        commitId = receipt.CommitId,
        sourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList(),
        quantity = receipt.Quantity,
        inputMassGrams = receipt.InputMassGrams
    };

    private static PhysicalItemRestoreCandidateDispositionSnapshot
        ToCandidateReceipt(PhysicalItemBatchDispositionReceipt receipt) => new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            "stock-policy-sale-focused-fixture",
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FailOnceCommandPort :
        IResourceStockPolicySaleCommandPort
    {
        private readonly IPhysicalFacilityItemBatchTransferGateway gateway;

        internal FailOnceCommandPort(
            IPhysicalFacilityItemBatchTransferGateway gateway) =>
            this.gateway = gateway
                ?? throw new ArgumentNullException(nameof(gateway));

        internal bool FailNextAcknowledgement { get; set; }
        internal int IncomePublicationCount { get; private set; }
        internal int PublishedIncome { get; private set; }
        internal int SuccessfulAcknowledgements { get; private set; }

        public bool TryGetPendingSaleTransfer(
            string operationId,
            out ResourceStockPolicySaleTransferReceipt receipt)
        {
            receipt = null;
            if (!gateway.TryGetPending(
                    operationId,
                    out PhysicalItemBatchDispositionReceipt physical))
            {
                return false;
            }
            receipt = ToSaleReceipt(physical);
            return true;
        }

        public bool TryPublishSaleIncome(
            int amount,
            string operationId,
            string itemId,
            out string failureReason)
        {
            IncomePublicationCount++;
            PublishedIncome += amount;
            failureReason = string.Empty;
            return true;
        }

        public bool AcknowledgeSaleTransfer(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-stock-policy-ack-failure";
                return false;
            }
            bool acknowledged = gateway.Acknowledge(
                commitId,
                out failureReason);
            if (acknowledged)
            {
                SuccessfulAcknowledgements++;
            }
            return acknowledged;
        }
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly CandidateQuery Empty = new();

        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> pending;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] pending)
        {
            this.pending = pending ?? Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => pending;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = pending.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.OperationId,
                    operationId,
                    StringComparison.Ordinal));
            return disposition != null;
        }
    }
}
#endif
