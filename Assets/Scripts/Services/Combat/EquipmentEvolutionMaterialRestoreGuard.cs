using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cross-section restore gate for equipment-evolution WIP material Transfers.
/// Every saved owner must join one exact incoming Physical Items receipt and
/// every domain-prefixed receipt must have one owner.
/// </summary>
public sealed class EquipmentEvolutionMaterialRestoreGuard :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "223.world.equipment-evolution-materials";
    private readonly IEquipmentEvolutionRuntime evolution;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private bool active;
    private bool published;

    public EquipmentEvolutionMaterialRestoreGuard(
        IEquipmentEvolutionRuntime evolution,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.evolution = evolution
            ?? throw new ArgumentNullException(nameof(evolution));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
        {
            throw new InvalidOperationException(
                "Equipment evolution material restore validation is already active.");
        }
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
        {
            throw new InvalidOperationException(
                "Equipment evolution material restore validation is not ready to publish.");
        }

        ValidateOwnerSet(
            evolution.ReforgeOrders,
            evolution.ReattunementOrders,
            physicalCandidates);
        published = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public void CompleteRestoreCandidate()
    {
        if (!active || !published)
        {
            throw new InvalidOperationException(
                "Equipment evolution material restore validation cannot complete.");
        }
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public static void ValidateOwnerSet(
        IReadOnlyList<EvolutionReforgeOrder> reforgeOrders,
        IReadOnlyList<EquipmentReattunementOrder> reattunementOrders,
        IPhysicalItemRestoreCandidateQuery query)
    {
        Dictionary<string, ExpectedReceipt> owners =
            new(StringComparer.Ordinal);
        foreach (EvolutionReforgeOrder order in
                 reforgeOrders ?? Array.Empty<EvolutionReforgeOrder>())
        {
            if (order == null
                || string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                continue;
            }
            EquipmentEvolutionRestoreBuilder.Build(
                new EquipmentEvolutionSaveData
                {
                    reforgeOrders = new List<EvolutionReforgeOrder>
                    {
                        order.Clone()
                    }
                });
            AddOwner(
                owners,
                order.materialTransferOperationId,
                order.materialTransferCommitId,
                order.materialTransferRequestFingerprint,
                order.materialTransferMassGrams,
                order.materialTransferInputs,
                EquipmentEvolutionMaterialOutbox.ReforgeReasonCode);
        }

        foreach (EquipmentReattunementOrder order in
                 reattunementOrders
                 ?? Array.Empty<EquipmentReattunementOrder>())
        {
            if (order == null
                || string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                continue;
            }
            EquipmentEvolutionRestoreBuilder.Build(
                new EquipmentEvolutionSaveData
                {
                    reattunementOrders =
                        new List<EquipmentReattunementOrder>
                        {
                            order.Clone()
                        }
                });
            AddOwner(
                owners,
                order.materialTransferOperationId,
                order.materialTransferCommitId,
                order.materialTransferRequestFingerprint,
                order.materialTransferMassGrams,
                order.materialTransferInputs,
                EquipmentEvolutionMaterialOutbox.ReattunementReasonCode);
        }

        if (query == null || !query.IsCandidateAvailable)
        {
            if (owners.Count == 0)
            {
                return;
            }
            throw new InvalidOperationException(
                "Equipment evolution restore requires the incoming physical candidate.");
        }

        foreach (ExpectedReceipt expected in owners.Values)
        {
            if (!query.TryGetPendingBatchDisposition(
                    expected.OperationId,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || receipt.Kind != PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    receipt.ReasonCode,
                    expected.ReasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    expected.CommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RequestFingerprint,
                    expected.RequestFingerprint,
                    StringComparison.Ordinal)
                || receipt.Quantity != expected.Quantity
                || receipt.InputMassGrams != expected.MassGrams
                || !receipt.SourceStackIds.SequenceEqual(
                    expected.SourceStackIds,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Equipment evolution order has no exact incoming material Transfer receipt: "
                    + expected.OperationId);
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || (!receipt.OperationId.StartsWith(
                        "equipment-reforge-material:",
                        StringComparison.Ordinal)
                    && !receipt.OperationId.StartsWith(
                        "equipment-reattunement-material:",
                        StringComparison.Ordinal)))
            {
                continue;
            }

            if (!owners.ContainsKey(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Incoming equipment evolution material Transfer has no order owner: "
                    + receipt.OperationId);
            }
        }
    }

    private static void AddOwner(
        IDictionary<string, ExpectedReceipt> owners,
        string operationId,
        string commitId,
        string requestFingerprint,
        long massGrams,
        IReadOnlyList<EquipmentEvolutionMaterialTransferInput> inputs,
        string reasonCode)
    {
        ExpectedReceipt expected = new(
            operationId,
            commitId,
            requestFingerprint,
            massGrams,
            checked(inputs.Sum(input => input.quantity)),
            inputs.Select(input => input.sourceStackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            reasonCode);
        if (!owners.TryAdd(operationId, expected))
        {
            throw new InvalidOperationException(
                "Duplicate equipment evolution material operation: "
                + operationId);
        }
    }

    private sealed class ExpectedReceipt
    {
        internal ExpectedReceipt(
            string operationId,
            string commitId,
            string requestFingerprint,
            long massGrams,
            int quantity,
            IReadOnlyList<string> sourceStackIds,
            string reasonCode)
        {
            OperationId = operationId;
            CommitId = commitId;
            RequestFingerprint = requestFingerprint;
            MassGrams = massGrams;
            Quantity = quantity;
            SourceStackIds = sourceStackIds;
            ReasonCode = reasonCode;
        }

        internal string OperationId { get; }
        internal string CommitId { get; }
        internal string RequestFingerprint { get; }
        internal long MassGrams { get; }
        internal int Quantity { get; }
        internal IReadOnlyList<string> SourceStackIds { get; }
        internal string ReasonCode { get; }
    }
}
