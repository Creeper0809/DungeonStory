using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResourceStockSaleFact
{
    private readonly ResourceStockPolicyPendingSale snapshot;

    public ResourceStockSaleFact(
        ResourceStockPolicyPendingSale value,
        bool completed)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        snapshot = value.Clone();
        snapshot.outcomeResultKey = string.Empty;
        snapshot.outcomeId = string.Empty;
        snapshot.outcomeDigest = string.Empty;
        Validate(snapshot, completed);
        Completed = completed;
    }

    public bool Completed { get; }
    public string OperationId => snapshot.operationId;
    public int AbsoluteDay => snapshot.absoluteDay;

    public ResourceStockPolicyPendingSale CaptureDomainState() =>
        snapshot.Clone();

    private static void Validate(
        ResourceStockPolicyPendingSale value,
        bool completed)
    {
        if (value.sequence <= 0
            || !IsCanonicalRequired(value.itemId)
            || !string.Equals(
                value.operationId,
                ResourceStockPolicySaleOutbox.FormatOperationId(
                    value.itemId,
                    value.sequence),
                StringComparison.Ordinal)
            || !string.Equals(
                value.reasonCode,
                ResourceStockPolicySaleOutbox.TransferReason,
                StringComparison.Ordinal)
            || !string.Equals(
                value.destinationId,
                ResourceStockPolicySaleOutbox.FormatDestinationId(
                    value.itemId),
                StringComparison.Ordinal)
            || value.quantity <= 0
            || value.proceeds <= 0
            || value.unitPrice <= 0
            || value.saleRateBasisPoints is <= 0 or > 10000
            || value.absoluteDay < 0)
        {
            throw new ArgumentException(
                "A canonical stock-policy sale identity and plan are required.",
                nameof(value));
        }

        if (!completed)
        {
            if ((int)value.phase != 0
                || !string.IsNullOrEmpty(value.commitId)
                || value.sourceStackIds == null
                || value.sourceStackIds.Any(id => !IsCanonicalRequired(id))
                || !value.sourceStackIds.SequenceEqual(value.sourceStackIds.OrderBy(id => id, StringComparer.Ordinal))
                || value.sourceStackIds.Distinct(StringComparer.Ordinal).Count() != value.sourceStackIds.Count
                || value.inputMassGrams != 0L
                || value.physicalOutcomeOwnerRevision != 0L
                || !HasNoPayment(value.economyTransactionId,
                    value.balanceBefore, value.balanceAfter))
            {
                throw new ArgumentException(
                    "A planned stock-policy sale cannot contain committed physical or payment state.",
                    nameof(value));
            }
            return;
        }

        IReadOnlyList<string> sourceStackIds =
            (IReadOnlyList<string>)value.sourceStackIds
            ?? Array.Empty<string>();
        if (value.phase < ResourceStockPolicySaleCommitPhase.IncomePublished
            || !Enum.IsDefined(typeof(ResourceStockPolicySaleCommitPhase), value.phase)
            || !IsCanonicalRequired(value.commitId)
            || sourceStackIds.Count == 0
            || sourceStackIds.Any(stackId => !IsCanonicalRequired(stackId))
            || !sourceStackIds.SequenceEqual(
                sourceStackIds.OrderBy(stackId => stackId, StringComparer.Ordinal),
                StringComparer.Ordinal)
            || sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                != sourceStackIds.Count
            || value.inputMassGrams <= 0L
            || value.physicalOutcomeOwnerRevision <= 0L
            || !string.Equals(
                value.commitId,
                $"physical-batch-disposition:1:{value.operationId}:"
                    + $"{value.quantity}:{value.inputMassGrams}",
                StringComparison.Ordinal)
            || !HasCanonicalPayment(
                value.economyTransactionId,
                value.balanceBefore,
                value.balanceAfter,
                value.proceeds))
        {
            throw new ArgumentException(
                "A completed stock-policy sale requires canonical physical and payment state.",
                nameof(value));
        }
    }

    private static bool HasNoPayment(
        string transactionId,
        int balanceBefore,
        int balanceAfter) =>
        string.IsNullOrEmpty(transactionId)
        && balanceBefore == 0
        && balanceAfter == 0;

    private static bool HasCanonicalPayment(
        string transactionId,
        int balanceBefore,
        int balanceAfter,
        int proceeds) =>
        IsCanonicalRequired(transactionId)
        && balanceBefore >= 0
        && (long)balanceAfter == (long)balanceBefore + proceeds;

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class QualityRejectedSaleFact
{
    private readonly QualityRejectedSalePending snapshot;

    public QualityRejectedSaleFact(
        QualityRejectedSalePending value,
        bool completed)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        snapshot = value.Clone();
        snapshot.outcomeResultKey = string.Empty;
        snapshot.outcomeId = string.Empty;
        snapshot.outcomeDigest = string.Empty;
        Validate(snapshot, completed);
        Completed = completed;
    }

    public bool Completed { get; }
    public string OperationId => snapshot.operationId;
    public int AbsoluteDay => snapshot.absoluteDay;

    public QualityRejectedSalePending CaptureDomainState() =>
        snapshot.Clone();

    private static void Validate(
        QualityRejectedSalePending value,
        bool completed)
    {
        if (value.sequence <= 0
            || !IsCanonicalRequired(value.sourceStackId)
            || !string.Equals(
                value.operationId,
                QualityRejectedSaleContract.FormatOperationId(
                    value.sequence,
                    value.sourceStackId),
                StringComparison.Ordinal)
            || !string.Equals(
                value.reasonCode,
                QualityRejectedSaleContract.TransferReason,
                StringComparison.Ordinal)
            || !IsCanonicalRequired(value.itemId)
            || !IsCanonicalRequired(value.itemInstanceId)
            || !string.Equals(
                value.destinationId,
                QualityRejectedOutputRules.MarketDestinationId,
                StringComparison.Ordinal)
            || value.quantity != 1
            || value.proceeds <= 0
            || value.unitPrice <= 0
            || value.saleRateBasisPoints is <= 0 or > 10000
            || value.qualityTier < 0
            || value.absoluteDay < 0)
        {
            throw new ArgumentException(
                "A canonical rejected-quality sale identity and plan are required.",
                nameof(value));
        }

        if (!completed)
        {
            if (value.phase != QualityRejectedSaleCommitPhase.Prepared
                || !string.IsNullOrEmpty(value.commitId)
                || value.inputMassGrams != 0L
                || value.physicalOutcomeOwnerRevision != 0L
                || !HasNoPayment(value))
            {
                throw new ArgumentException(
                    "A planned rejected-quality sale cannot contain committed physical or payment state.",
                    nameof(value));
            }
            return;
        }

        if (!IsLowerSha256(value.componentFingerprint)
            || value.phase < QualityRejectedSaleCommitPhase.IncomePublished
            || !Enum.IsDefined(typeof(QualityRejectedSaleCommitPhase), value.phase)
            || !IsCanonicalRequired(value.commitId)
            || value.inputMassGrams <= 0L
            || value.physicalOutcomeOwnerRevision <= 0L
            || !string.Equals(
                value.commitId,
                $"physical-batch-disposition:1:{value.operationId}:1:"
                    + value.inputMassGrams,
                StringComparison.Ordinal)
            || !HasCanonicalPayment(value))
        {
            throw new ArgumentException(
                "A completed rejected-quality sale requires canonical physical and payment state.",
                nameof(value));
        }
    }

    private static bool HasNoPayment(QualityRejectedSalePending value) =>
        string.IsNullOrEmpty(value.economyTransactionId)
        && value.balanceBefore == 0
        && value.balanceAfter == 0;

    private static bool HasCanonicalPayment(
        QualityRejectedSalePending value) =>
        IsCanonicalRequired(value.economyTransactionId)
        && value.balanceBefore >= 0
        && (long)value.balanceAfter
            == (long)value.balanceBefore + value.proceeds;

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowerSha256(string value)
    {
        if (value?.Length != 64)
            return false;
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9')
                && character is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}
