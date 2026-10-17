using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class AtomicItemConsumptionService :
    IAtomicItemConsumptionService,
    IAtomicItemConsumptionEvidenceService,
    IIdempotentAtomicItemConsumptionService
{
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IItemQuantityLeaseMutation leaseMutations;

    public AtomicItemConsumptionService(
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter,
        IItemQuantityLeaseMutation leaseMutations)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
        this.leaseMutations = leaseMutations
            ?? throw new ArgumentNullException(nameof(leaseMutations));
    }

    public bool TryConsumeReserved(
        IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId,
        out DomainFailure failure)
    {
        return TryConsumeReservedWithEvidence(
            consumptions,
            reservationOwnerId,
            out _,
            out failure);
    }

    public bool TryConsumeReservedWithEvidence(
        IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId,
        out AtomicItemConsumptionReceipt receipt,
        out DomainFailure failure) => TryConsume(consumptions, reservationOwnerId,
            retainReceipt: false, out receipt, out failure);

    public bool TryConsumeReservedOnce(IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId, out PhysicalEffectReceiptSaveData receipt, out DomainFailure failure)
    {
        receipt = null;
        if (!TryConsume(consumptions, reservationOwnerId, retainReceipt: true, out _, out failure))
            return false;
        repository.TryGetPhysicalEffectReceipt(PhysicalEffectReceiptKind.ReservedConsumption,
            reservationOwnerId?.Trim(), out receipt);
        return true;
    }

    private bool TryConsume(IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId, bool retainReceipt,
        out AtomicItemConsumptionReceipt receipt, out DomainFailure failure)
    {
        receipt = null;
        failure = DomainFailure.None;
        string owner = reservationOwnerId?.Trim() ?? string.Empty;
        if (consumptions != null && consumptions.Any(value => !value.IsValid))
        {
            failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed,
                "Invalid atomic consumption line.");
            return false;
        }
        ReservedItemConsumption[] costs = (consumptions
                ?? Array.Empty<ReservedItemConsumption>())
            .GroupBy(value => value.StackId, StringComparer.Ordinal)
            .Select(group => new ReservedItemConsumption(
                group.Key,
                group.Sum(value => value.Quantity)))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (costs.Length == 0)
        {
            if (retainReceipt && repository.TryGetPhysicalEffectReceipt(
                    PhysicalEffectReceiptKind.ReservedConsumption, owner, out _))
            {
                failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed,
                    "physical-consumption-operation-conflict");
                return false;
            }
            receipt = new AtomicItemConsumptionReceipt(
                owner,
                Array.Empty<AtomicItemConsumptionLine>());
            return true;
        }
        if (owner.Length == 0)
        {
            failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed);
            return false;
        }

        string requestFingerprint = PhysicalEffectReceiptRules.ConsumptionFingerprint(costs.Select(
            value => new KeyValuePair<string, int>(value.StackId, value.Quantity)));
        if (retainReceipt && repository.TryGetPhysicalEffectReceipt(
                PhysicalEffectReceiptKind.ReservedConsumption, owner, out var previous))
        {
            if (!string.Equals(previous.requestFingerprint, requestFingerprint, StringComparison.Ordinal))
            {
                failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed,
                    "physical-consumption-operation-conflict");
                return false;
            }
            receipt = new AtomicItemConsumptionReceipt(owner, previous.lines.Select(value =>
                new AtomicItemConsumptionLine(value.stackId, value.itemId, value.quantity,
                    value.quantityBefore, value.quantityAfter)).ToArray());
            return true;
        }
        if (retainReceipt && !repository.CanRecordPhysicalEffectReceipt(
                PhysicalEffectReceiptKind.ReservedConsumption, owner))
        {
            failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed,
                "physical-effect-receipt-capacity-exhausted");
            return false;
        }

        List<(WorldItemStackRecord record, int quantity)> resolved = new();
        List<AtomicItemConsumptionLine> evidenceLines = new();
        foreach (ReservedItemConsumption cost in costs)
        {
            if (!repository.RecordsById.TryGetValue(
                    cost.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.quantity < cost.Quantity)
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed);
                return false;
            }
            resolved.Add((record, cost.Quantity));
            evidenceLines.Add(new AtomicItemConsumptionLine(
                record.stackId,
                record.itemId,
                cost.Quantity,
                record.quantity,
                record.quantity - cost.Quantity));
        }

        if (resolved.Any(value =>
                FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    value.record.components)))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                owner,
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
                    .ToString());
            return false;
        }

        PhysicalEffectReceiptSaveData durable = null;
        if (retainReceipt)
        {
            durable = new PhysicalEffectReceiptSaveData
            {
                kind = (int)PhysicalEffectReceiptKind.ReservedConsumption,
                operationId = owner,
                requestFingerprint = requestFingerprint,
                lines = evidenceLines.Select(value => new PhysicalEffectReceiptLineSaveData
                {
                    stackId = value.StackId, itemId = value.ItemId, quantity = value.RequestedQuantity,
                    quantityBefore = value.QuantityBefore, quantityAfter = value.QuantityAfter
                }).ToList()
            };
            durable.commitId = PhysicalEffectReceiptRules.CommitId(durable);
            PhysicalEffectReceiptRules.Validate(durable);
        }
        // The lease owner validates the entire exact vector before its first
        // mutation. A multi-stack lease must not consume its unrelated prefix.
        if (!leaseMutations.TryConsumeExactSlices(owner, costs, out failure))
            return false;

        HashSet<Vector2Int> changedPositions = new();
        foreach ((WorldItemStackRecord record, int quantity) in resolved)
        {
            changedPositions.Add(record.position);
            record.quantity -= quantity;
            if (record.quantity > 0) continue;
            if (!string.IsNullOrWhiteSpace(record.itemInstanceId))
            {
                repository.TryMarkEquipmentLostBySourceStack(record.stackId);
                repository.TryMarkModuleLostBySourceStack(record.stackId);
            }
            repository.Remove(record);
        }
        repository.MarkChanged();
        if (durable != null) repository.RecordPhysicalEffectReceipt(durable);
        foreach (Vector2Int position in changedPositions)
        {
            markerPresenter.RefreshAt(position);
        }
        receipt = new AtomicItemConsumptionReceipt(owner, evidenceLines);
        return true;
    }
}
