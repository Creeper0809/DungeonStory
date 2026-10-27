using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation.Facts;

/// <summary>The exact debit decided by the item owner, frozen before observer publication.</summary>
public sealed class PhysicalItemDisposedFact
{
    public PhysicalItemDisposedFact(in PhysicalItemBatchDispositionReceipt receipt, int absoluteDay)
    {
        if (!receipt.IsCommitted || receipt.OwnerRevision <= 0 || absoluteDay < 1
            || !IsPhysicalOperationId(receipt.OperationId)
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

    // 저장 영수증의 URI escape와 긴 합성 키는 보존한다. 기록 계층의 제한된
    // ID 표현으로만 변환하며, 이미 유효했던 기록 키는 그대로 유지한다.
    public string ObservationOperationId => ObservationIdentity(Receipt.OperationId, "physical-operation");
    public string ObservationCommitId => ObservationIdentity(Receipt.CommitId, "physical-commit");

    private static string ObservationIdentity(string source, string kind)
    {
        if (DomainFactValueSyntax.IsIdentity(source)) return source;
        using var sha = SHA256.Create();
        return kind + ":sha256:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }

    private static bool IsPhysicalOperationId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 2048) return false;
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
                or '.' or ':' or '_' or '-' or '/') continue;
            if (c != '%' || index + 2 >= value.Length || !Hex(value[index + 1]) || !Hex(value[index + 2]))
                return false;
            index += 2;
        }
        return true;
        static bool Hex(char c) => c is >= '0' and <= '9' or >= 'A' and <= 'F';
    }

    internal static PhysicalItemDisposedFact FromSave(PhysicalItemBatchDispositionSaveData saved) => new(
        new PhysicalItemBatchDispositionReceipt((PhysicalItemDispositionKind)saved.kind,
            saved.operationId, saved.reasonCode, saved.requestFingerprint, saved.sourceStackIds,
            saved.quantity, saved.inputMassGrams, saved.ownerRevision,
            saved.sourceFacts.Select(PhysicalItemSourceFactSaveCodec.FromSave).ToArray()), saved.defaultObservationDay);
}
