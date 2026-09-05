using System;

public static class ProductionOutputCommitIdentity
{
    public const string Prefix = "production-output:";

    public static bool IsOwnedCommitId(string commitId) =>
        !string.IsNullOrWhiteSpace(commitId)
        && string.Equals(commitId, commitId.Trim(), StringComparison.Ordinal)
        && commitId.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Format(
        ProductionBillId billId,
        int cycleSequence,
        string outputLineId,
        string itemId,
        int outputOrdinal)
    {
        string line = outputLineId ?? string.Empty;
        string item = itemId ?? string.Empty;
        if (!billId.IsValid
            || cycleSequence <= 0
            || outputOrdinal < 0
            || !ProductionOutputDefinition.IsCanonicalOutputLineId(line)
            || item.Length == 0
            || !string.Equals(item, item.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Production output commit identity is invalid.");
        }
        return $"{Prefix}{billId.Value}:{cycleSequence:D8}:{line}:{item}:{outputOrdinal:D8}";
    }
}
