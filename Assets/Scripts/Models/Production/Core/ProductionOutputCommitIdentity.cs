using System;

public static class ProductionOutputCommitIdentity
{
    public static string Format(
        ProductionBillId billId,
        int cycleSequence,
        string itemId,
        int outputOrdinal)
    {
        string item = itemId ?? string.Empty;
        if (!billId.IsValid
            || cycleSequence <= 0
            || outputOrdinal < 0
            || item.Length == 0
            || !string.Equals(item, item.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Production output commit identity is invalid.");
        }
        return $"production-output:{billId.Value}:{cycleSequence:D8}:{item}:{outputOrdinal:D8}";
    }
}
