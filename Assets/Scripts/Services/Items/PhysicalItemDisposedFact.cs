using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>The exact debit decided by the item owner, frozen before observer publication.</summary>
public sealed class PhysicalItemDisposedFact
{
    public PhysicalItemDisposedFact(in PhysicalItemBatchDispositionReceipt receipt, int absoluteDay)
    {
        if (!receipt.IsCommitted || receipt.OwnerRevision <= 0 || absoluteDay < 1
            || !DomainFactValueSyntax.IsIdentity(receipt.OperationId)
            || !DomainFactValueSyntax.IsIdentity(receipt.ReasonCode)
            || string.IsNullOrEmpty(receipt.RequestFingerprint)
            || receipt.SourceFacts == null || receipt.SourceFacts.Count == 0
            || receipt.SourceFacts.Any(value => !value.IsValid))
            throw new ArgumentException("physical-disposition-fact-invalid");
        var ids = new HashSet<string>(receipt.SourceFacts.Select(value => value.StackId), StringComparer.Ordinal);
        if (ids.Count != receipt.SourceFacts.Count || ids.Count != receipt.SourceStackIds.Count
            || !ids.SetEquals(receipt.SourceStackIds)
            || receipt.SourceFacts.Sum(value => (long)value.Quantity) != receipt.Quantity
            || receipt.SourceFacts.Sum(value => value.MassGrams) != receipt.InputMassGrams)
            throw new ArgumentException("physical-disposition-fact-source-mismatch");
        if (!receipt.SourceStackIds.SequenceEqual(receipt.SourceFacts.Select(value => value.StackId))
            || !receipt.SourceStackIds.SequenceEqual(receipt.SourceStackIds.OrderBy(value => value, StringComparer.Ordinal))
            || receipt.RequestFingerprint != $"{(int)receipt.Kind}:{receipt.ReasonCode}:"
                + string.Join(",", receipt.SourceFacts.Select(value => $"{value.StackId}={value.Quantity}")))
            throw new ArgumentException("physical-disposition-fact-request-mismatch");
        Receipt = new PhysicalItemBatchDispositionReceipt(receipt.Kind, receipt.OperationId, receipt.ReasonCode,
            receipt.RequestFingerprint, receipt.SourceStackIds, receipt.Quantity, receipt.InputMassGrams,
            receipt.OwnerRevision, receipt.SourceFacts);
        AbsoluteDay = absoluteDay;
    }
    public PhysicalItemBatchDispositionReceipt Receipt { get; }
    public int AbsoluteDay { get; }

    internal static PhysicalItemDisposedFact FromSave(PhysicalItemBatchDispositionSaveData saved) => new(
        new PhysicalItemBatchDispositionReceipt((PhysicalItemDispositionKind)saved.kind,
            saved.operationId, saved.reasonCode, saved.requestFingerprint, saved.sourceStackIds,
            saved.quantity, saved.inputMassGrams, saved.ownerRevision,
            saved.sourceFacts.Select(PhysicalItemSourceFactSaveCodec.FromSave).ToArray()), saved.defaultObservationDay);
}
