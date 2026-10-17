using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public enum PhysicalEffectReceiptKind
{
    ReservedConsumption = 1,
    ReleasedPublication = 2
}

[Serializable]
public sealed class PhysicalEffectReceiptLineSaveData
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public int quantityBefore;
    public int quantityAfter;
    public string outputLineId = string.Empty;
    public string itemInstanceId = string.Empty;
    public long massGrams;

    public PhysicalEffectReceiptLineSaveData Clone() =>
        (PhysicalEffectReceiptLineSaveData)MemberwiseClone();
}

/// <summary>
/// Historical physical commit evidence. Its exact vector remains valid after
/// the source/output stacks have moved, split, or been consumed, until its
/// parent atomically acknowledges completion and retires this recovery evidence.
/// </summary>
[Serializable]
public sealed class PhysicalEffectReceiptSaveData
{
    public int kind;
    public string operationId = string.Empty;
    public string commitId = string.Empty;
    public string requestFingerprint = string.Empty;
    public string destinationId = string.Empty;
    public string outcomeFingerprint = string.Empty;
    public int positionX;
    public int positionY;
    public long totalMassGrams;
    public List<PhysicalEffectReceiptLineSaveData> lines = new();

    public PhysicalEffectReceiptSaveData Clone()
    {
        var copy = (PhysicalEffectReceiptSaveData)MemberwiseClone();
        copy.lines = lines?.Select(value => value?.Clone()).ToList();
        return copy;
    }
}

public interface IPhysicalEffectReceiptQuery
{
    int AvailablePhysicalEffectReceiptCapacity { get; }
    bool TryGetPhysicalEffectReceipt(
        PhysicalEffectReceiptKind kind, string operationId,
        out PhysicalEffectReceiptSaveData receipt);
}

public interface IPhysicalEffectReceiptRestoreQuery
{
    bool IsPhysicalEffectReceiptCandidateAvailable { get; }
    bool TryGetPhysicalEffectReceiptCandidate(
        PhysicalEffectReceiptKind kind, string operationId,
        out PhysicalEffectReceiptSaveData receipt);
}

public static class PhysicalEffectReceiptRules
{
    public const int SchemaVersion = 1;
    public const int MaximumReceipts = 16384;
    public const int MaximumLines = 4096;

    public static string Key(PhysicalEffectReceiptKind kind, string operationId) =>
        ((int)kind).ToString(CultureInfo.InvariantCulture) + ":" + operationId;

    public static string Fingerprint(IEnumerable<string> fields)
    {
        var text = new StringBuilder();
        foreach (string field in fields)
        {
            string value = field ?? string.Empty;
            text.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(value).Append('|');
        }
        using var hash = SHA256.Create();
        return "sha256:" + BitConverter.ToString(hash.ComputeHash(
            Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
    }

    public static string ConsumptionFingerprint(
        IEnumerable<KeyValuePair<string, int>> costs) => Fingerprint(costs
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .SelectMany(value => new[] { value.Key, Number(value.Value) }));

    public static string PublicationFingerprint(
        string destinationId, string outcomeFingerprint, int x, int y) =>
        Fingerprint(new[] { destinationId, outcomeFingerprint, Number(x), Number(y) });

    public static string CommitId(PhysicalEffectReceiptSaveData receipt) =>
        "physical-effect:" + Fingerprint(CommitFields(receipt));

    public static void Validate(PhysicalEffectReceiptSaveData receipt)
    {
        if (receipt == null || !Enum.IsDefined(typeof(PhysicalEffectReceiptKind), receipt.kind)
            || !Canonical(receipt.operationId) || !Canonical(receipt.commitId)
            || !IsDigest(receipt.requestFingerprint) || receipt.destinationId == null
            || receipt.outcomeFingerprint == null || receipt.lines == null
            || receipt.lines.Count == 0 || receipt.lines.Count > MaximumLines
            || receipt.lines.Any(value => value == null || !Canonical(value.stackId)
                || !Canonical(value.itemId) || value.quantity <= 0 || value.quantityBefore < 0
                || value.quantityAfter < 0 || value.outputLineId == null
                || value.itemInstanceId == null || value.massGrams < 0)
            || receipt.lines.Select(value => value.stackId).Distinct(StringComparer.Ordinal).Count()
                != receipt.lines.Count)
            throw new InvalidOperationException("Physical effect receipt has an invalid exact vector.");

        if (receipt.kind == (int)PhysicalEffectReceiptKind.ReservedConsumption)
        {
            if (receipt.destinationId.Length != 0 || receipt.outcomeFingerprint.Length != 0
                || receipt.totalMassGrams != 0 || receipt.positionX != 0 || receipt.positionY != 0
                || receipt.lines.Any(value => value.quantityBefore < value.quantity
                    || value.quantityBefore - value.quantity != value.quantityAfter
                    || value.outputLineId.Length != 0 || value.itemInstanceId.Length != 0
                    || value.massGrams != 0)
                || receipt.requestFingerprint != ConsumptionFingerprint(receipt.lines.Select(value =>
                    new KeyValuePair<string, int>(value.stackId, value.quantity))))
                throw new InvalidOperationException("Physical consumption receipt is inconsistent.");
        }
        else
        {
            if (!Canonical(receipt.destinationId) || receipt.outcomeFingerprint.Length != 64
                || receipt.outcomeFingerprint.Any(value => !IsHex(value))
                || receipt.totalMassGrams <= 0
                || receipt.lines.Any(value => !Canonical(value.outputLineId) || value.massGrams <= 0
                    || value.quantityBefore != 0 || value.quantityAfter != value.quantity)
                || receipt.lines.Sum(value => value.massGrams) != receipt.totalMassGrams
                || receipt.requestFingerprint != PublicationFingerprint(receipt.destinationId,
                    receipt.outcomeFingerprint, receipt.positionX, receipt.positionY))
                throw new InvalidOperationException("Physical publication receipt is inconsistent.");
        }
        if (!string.Equals(receipt.commitId, CommitId(receipt), StringComparison.Ordinal))
            throw new InvalidOperationException("Physical effect receipt commit fingerprint mismatched.");
    }

    public static void ValidateCollection(int version, IReadOnlyList<PhysicalEffectReceiptSaveData> receipts)
    {
        if (version is not (0 or SchemaVersion) || receipts == null
            || receipts.Count > MaximumReceipts || version == 0 && receipts.Count != 0)
            throw new InvalidOperationException("Physical effect receipt schema is invalid.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var receipt in receipts)
        {
            Validate(receipt);
            if (!keys.Add(Key((PhysicalEffectReceiptKind)receipt.kind, receipt.operationId)))
                throw new InvalidOperationException("Physical effect receipt operation is duplicated.");
        }
    }

    private static IEnumerable<string> CommitFields(PhysicalEffectReceiptSaveData receipt)
    {
        yield return Number(receipt.kind);
        yield return receipt.operationId;
        yield return receipt.requestFingerprint;
        yield return receipt.destinationId;
        yield return receipt.outcomeFingerprint;
        yield return Number(receipt.positionX);
        yield return Number(receipt.positionY);
        yield return Number(receipt.totalMassGrams);
        foreach (var line in receipt.lines.OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            yield return line.stackId;
            yield return line.itemId;
            yield return Number(line.quantity);
            yield return Number(line.quantityBefore);
            yield return Number(line.quantityAfter);
            yield return line.outputLineId;
            yield return line.itemInstanceId;
            yield return Number(line.massGrams);
        }
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static bool Canonical(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 2048 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    private static bool IsHex(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f';
    private static bool IsDigest(string value) => value != null && value.Length == 71
        && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Skip(7).All(IsHex);
}
