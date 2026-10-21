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
            EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
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
                unitPrice: 10,
                saleRateBasisPoints: 7000,
                absoluteDay: 1,
                outcomeResultKey: "fixture.stock-policy|fixture-operation|1|0",
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

        FailOnceCommandPort first = new(
            gateway,
            physicalReceipt.OwnerRevision)
        {
            FailNextAcknowledgement = true
        };
        Require(first.TryGetPendingSaleTransfer(operationId, out var discovered)
                && ResourceStockPolicySaleOutbox.ReceiptMatchesSaved(
                    pending,
                    discovered),
            "Focused receipt join mismatch before settlement: "
                + $"pending=[{pending.operationId}|{pending.reasonCode}|"
                + $"{pending.commitId}|{pending.quantity}|{pending.inputMassGrams}|"
                + $"{pending.physicalOutcomeOwnerRevision}|"
                + $"{string.Join(',', pending.sourceStackIds)}]; "
                + $"receipt=[{discovered?.operationId}|{discovered?.reasonCode}|"
                + $"{discovered?.commitId}|{discovered?.quantity}|"
                + $"{discovered?.inputMassGrams}|"
                + $"{discovered?.physicalOutcomeOwnerRevision}|"
                + $"{string.Join(',', discovered?.sourceStackIds ?? new List<string>())}]");
        Require(!ResourceStockPolicySaleOutbox.TryFinalizePending(
                pending,
                first,
                out string injectedFailure)
            && pending.phase ==
                ResourceStockPolicySaleCommitPhase.LedgerCommitted
            && first.IncomePublicationCount == 1
            && first.OutcomePublicationCount == 1
            && first.PublishedIncome == 7
            && repository.GetEditorPendingBatchDispositionCount() == 1
            && repository.GetEditorTestQuantity(sourceStackId) == 1,
            "Acknowledgement fault did not preserve exact sale outbox state: "
                + $"failure={injectedFailure}; phase={pending.phase}; "
                + $"income={first.IncomePublicationCount}; "
                + $"outcome={first.OutcomePublicationCount}; "
                + $"published={first.PublishedIncome}; "
                + $"pending={repository.GetEditorPendingBatchDispositionCount()}; "
                + $"quantity={repository.GetEditorTestQuantity(sourceStackId)}");

        ResourceStockPolicyPendingSale restored =
            JsonUtility.FromJson<ResourceStockPolicyPendingSale>(
                JsonUtility.ToJson(pending));
        FailOnceCommandPort recovery = new(
            gateway,
            physicalReceipt.OwnerRevision);
        Require(ResourceStockPolicySaleOutbox.TryFinalizePending(
                restored,
                recovery,
                out string recoveryFailure)
            && string.IsNullOrEmpty(recoveryFailure)
            && recovery.IncomePublicationCount == 0
            && recovery.OutcomePublicationCount == 0
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
        inputMassGrams = receipt.InputMassGrams,
        physicalOutcomeOwnerRevision = Math.Max(1L, receipt.OwnerRevision)
    };

    private static PhysicalItemRestoreCandidateDispositionSnapshot
        ToCandidateReceipt(PhysicalItemBatchDispositionReceipt receipt) => new(
            new PhysicalItemBatchDispositionSaveData
            {
                kind = (int)receipt.Kind,
                operationId = receipt.OperationId,
                reasonCode = receipt.ReasonCode,
                requestFingerprint = "stock-policy-sale-focused-fixture",
                sourceStackIds = receipt.SourceStackIds.ToList(),
                quantity = receipt.Quantity,
                inputMassGrams = receipt.InputMassGrams,
                commitId = receipt.CommitId,
                ownerRevision = Math.Max(1L, receipt.OwnerRevision)
            });

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
        private readonly long physicalOutcomeOwnerRevision;

        internal FailOnceCommandPort(
            IPhysicalFacilityItemBatchTransferGateway gateway,
            long physicalOutcomeOwnerRevision)
        {
            this.gateway = gateway
                ?? throw new ArgumentNullException(nameof(gateway));
            this.physicalOutcomeOwnerRevision = Math.Max(
                1L,
                physicalOutcomeOwnerRevision);
        }

        internal bool FailNextAcknowledgement { get; set; }
        internal int IncomePublicationCount { get; private set; }
        internal int OutcomePublicationCount { get; private set; }
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
            receipt.physicalOutcomeOwnerRevision =
                physicalOutcomeOwnerRevision;
            return true;
        }

        public bool TryPublishSaleIncome(
            ResourceStockPolicyPendingSale pending,
            out string failureReason)
        {
            IncomePublicationCount++;
            PublishedIncome += pending.proceeds;
            pending.economyTransactionId = "economy:stock-policy-fixture";
            pending.balanceBefore = 100;
            pending.balanceAfter = 100 + pending.proceeds;
            failureReason = string.Empty;
            return true;
        }

        public bool TryPublishSaleFact(
            ResourceStockPolicyPendingSale pending,
            out string failureReason)
        {
            OutcomePublicationCount++;
            pending.outcomeId = "run:stock-policy-fixture:1";
            pending.outcomeDigest = new string('a', 64);
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
