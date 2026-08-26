using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public enum ApparelLeaseAuthorityReleaseStatus
{
    Applied = 0,
    Replay = 1,
    Conflict = 2
}

public readonly struct ApparelLeaseAuthorityRow
{
    public ApparelLeaseAuthorityRow(
        string ownerOperationId,
        ItemReservationPurpose purpose,
        string aggregationCohortId,
        string stackId,
        int quantity,
        string expectedStackSignature)
    {
        OwnerOperationId = ownerOperationId ?? string.Empty;
        Purpose = purpose;
        AggregationCohortId = aggregationCohortId ?? string.Empty;
        StackId = stackId ?? string.Empty;
        Quantity = quantity;
        ExpectedStackSignature = expectedStackSignature ?? string.Empty;
    }

    public string OwnerOperationId { get; }
    public ItemReservationPurpose Purpose { get; }
    public string AggregationCohortId { get; }
    public string StackId { get; }
    public int Quantity { get; }
    public string ExpectedStackSignature { get; }
}

public sealed class ApparelLeaseAuthoritySnapshot
{
    internal ApparelLeaseAuthoritySnapshot(
        string ownerOperationId,
        ApparelLeaseAuthorityRow[] rows,
        string fingerprint)
    {
        OwnerOperationId = ownerOperationId;
        Rows = Array.AsReadOnly(rows ?? Array.Empty<ApparelLeaseAuthorityRow>());
        Fingerprint = fingerprint;
    }

    public string OwnerOperationId { get; }
    public IReadOnlyList<ApparelLeaseAuthorityRow> Rows { get; }
    public string Fingerprint { get; }
}

public readonly struct ApparelLeaseAuthorityReleaseResult
{
    public ApparelLeaseAuthorityReleaseResult(
        ApparelLeaseAuthorityReleaseStatus status,
        int releasedLeaseCount,
        string liveFingerprint,
        string failureReason)
    {
        Status = status;
        ReleasedLeaseCount = releasedLeaseCount;
        LiveFingerprint = liveFingerprint ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public ApparelLeaseAuthorityReleaseStatus Status { get; }
    public int ReleasedLeaseCount { get; }
    public string LiveFingerprint { get; }
    public string FailureReason { get; }
}

public interface IApparelLeaseAuthorityQuery
{
    bool TryCapture(
        string ownerOperationId,
        out ApparelLeaseAuthoritySnapshot snapshot,
        out string failureReason);
}

public interface IApparelLeaseAuthorityCommand
{
    [GameplayInternalOnly(
        "Releases only the frozen exact lease authority owned by one durable apparel terminal operation.",
        "Apparel destructive terminal drain producer only")]
    ApparelLeaseAuthorityReleaseResult TryReleaseExact(
        string ownerOperationId,
        string expectedFingerprint,
        ItemReservationReleaseReason reason);
}

/// <summary>
/// Exact adapter between a future durable apparel terminal producer and the
/// Items-owned quantity reservation authority. It deliberately bypasses the
/// rebuildable cache in <see cref="LeasedItemReservationService"/>.
/// </summary>
public sealed class ApparelLeaseAuthorityPort :
    IApparelLeaseAuthorityQuery,
    IApparelLeaseAuthorityCommand
{
    private const string FingerprintDomain = "apparel-lease-authority@1|";

    private readonly IItemQuantityReservationService reservations;

    public ApparelLeaseAuthorityPort(
        IItemQuantityReservationService reservations)
    {
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
    }

    public bool TryCapture(
        string ownerOperationId,
        out ApparelLeaseAuthoritySnapshot snapshot,
        out string failureReason)
    {
        snapshot = null;
        failureReason = string.Empty;
        if (!TryRequireCanonical(ownerOperationId, out string owner))
        {
            failureReason = "apparel-lease-authority-owner-invalid";
            return false;
        }
        if (!reservations.TryGetLeasesByOwner(
                owner,
                out IReadOnlyList<ItemQuantityLease> leases)
            || leases == null
            || leases.Count == 0)
        {
            failureReason = "apparel-lease-authority-missing:" + owner;
            return false;
        }
        if (!TryCreateRows(owner, leases, out ApparelLeaseAuthorityRow[] rows))
        {
            failureReason = "apparel-lease-authority-live-set-invalid:" + owner;
            return false;
        }

        snapshot = new ApparelLeaseAuthoritySnapshot(
            owner,
            rows,
            CreateFingerprint(rows));
        return true;
    }

    [GameplayInternalOnly(
        "Releases only the frozen exact lease authority owned by one durable apparel terminal operation.",
        "Apparel destructive terminal drain producer only")]
    public ApparelLeaseAuthorityReleaseResult TryReleaseExact(
        string ownerOperationId,
        string expectedFingerprint,
        ItemReservationReleaseReason reason)
    {
        if (!TryRequireCanonical(ownerOperationId, out string owner)
            || !IsCanonicalFingerprint(expectedFingerprint))
        {
            return Conflict(
                "apparel-lease-authority-release-plan-invalid",
                string.Empty);
        }

        if (!reservations.TryGetLeasesByOwner(
                owner,
                out IReadOnlyList<ItemQuantityLease> leases)
            || leases == null
            || leases.Count == 0)
        {
            // The durable terminal producer owns the frozen fingerprint. Once
            // no live owner authority remains, repeating that effect is a no-op.
            return new ApparelLeaseAuthorityReleaseResult(
                ApparelLeaseAuthorityReleaseStatus.Replay,
                0,
                string.Empty,
                string.Empty);
        }
        if (!TryCreateRows(owner, leases, out ApparelLeaseAuthorityRow[] rows))
        {
            return Conflict(
                "apparel-lease-authority-live-set-invalid:" + owner,
                string.Empty);
        }
        ApparelLeaseAuthoritySnapshot live = new(
            owner,
            rows,
            CreateFingerprint(rows));
        if (!string.Equals(
                live.Fingerprint,
                expectedFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict(
                "apparel-lease-authority-live-set-conflict:" + owner,
                live.Fingerprint);
        }

        int released = reservations.ReleaseByOwner(owner, reason);
        if (released <= 0)
        {
            return Conflict(
                "apparel-lease-authority-release-not-applied:" + owner,
                live.Fingerprint);
        }
        if (reservations.TryGetLeasesByOwner(
                owner,
                out IReadOnlyList<ItemQuantityLease> remaining)
            && remaining != null
            && remaining.Count > 0)
        {
            return Conflict(
                "apparel-lease-authority-release-left-orphan:" + owner,
                live.Fingerprint);
        }

        return new ApparelLeaseAuthorityReleaseResult(
            ApparelLeaseAuthorityReleaseStatus.Applied,
            released,
            live.Fingerprint,
            string.Empty);
    }

    private static ApparelLeaseAuthorityReleaseResult Conflict(
        string failureReason,
        string liveFingerprint) =>
        new(
            ApparelLeaseAuthorityReleaseStatus.Conflict,
            0,
            liveFingerprint,
            failureReason);

    private static bool TryCreateRows(
        string owner,
        IReadOnlyList<ItemQuantityLease> leases,
        out ApparelLeaseAuthorityRow[] rows)
    {
        rows = Array.Empty<ApparelLeaseAuthorityRow>();
        Dictionary<StableRowKey, int> totals = new();
        try
        {
            foreach (ItemQuantityLease lease in leases)
            {
                if (lease == null
                    || !string.Equals(
                        lease.ownerOperationId,
                        owner,
                        StringComparison.Ordinal)
                    || !IsCanonicalToken(lease.ownerOperationId)
                    || !IsCanonicalToken(lease.aggregationCohortId)
                    || !Enum.IsDefined(
                        typeof(ItemReservationPurpose),
                        lease.purpose)
                    || lease.remainingQuantity <= 0
                    || lease.slices == null
                    || lease.slices.Count == 0)
                {
                    return false;
                }

                int leaseQuantity = 0;
                foreach (ItemLeaseSlice slice in lease.slices)
                {
                    if (slice == null
                        || slice.quantity <= 0
                        || !IsCanonicalToken(slice.stackId)
                        || !IsCanonicalToken(slice.expectedStackSignature))
                    {
                        return false;
                    }
                    leaseQuantity = checked(leaseQuantity + slice.quantity);
                    StableRowKey key = new(
                        owner,
                        lease.purpose,
                        lease.aggregationCohortId,
                        slice.stackId,
                        slice.expectedStackSignature);
                    totals[key] = totals.TryGetValue(key, out int current)
                        ? checked(current + slice.quantity)
                        : slice.quantity;
                }
                if (leaseQuantity != lease.remainingQuantity)
                    return false;
            }
        }
        catch (OverflowException)
        {
            return false;
        }

        rows = totals
            .Select(pair => new ApparelLeaseAuthorityRow(
                pair.Key.OwnerOperationId,
                pair.Key.Purpose,
                pair.Key.AggregationCohortId,
                pair.Key.StackId,
                pair.Value,
                pair.Key.ExpectedStackSignature))
            .OrderBy(value => value.OwnerOperationId, StringComparer.Ordinal)
            .ThenBy(value => (int)value.Purpose)
            .ThenBy(value => value.AggregationCohortId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .ThenBy(value => value.Quantity)
            .ThenBy(value => value.ExpectedStackSignature, StringComparer.Ordinal)
            .ToArray();
        return rows.Length > 0;
    }

    private static string CreateFingerprint(
        IEnumerable<ApparelLeaseAuthorityRow> rows)
    {
        StringBuilder canonical = new StringBuilder(256)
            .Append(FingerprintDomain);
        foreach (ApparelLeaseAuthorityRow row in rows)
        {
            AppendToken(canonical, row.OwnerOperationId);
            canonical.Append(((int)row.Purpose).ToString(
                    CultureInfo.InvariantCulture))
                .Append('|');
            AppendToken(canonical, row.AggregationCohortId);
            AppendToken(canonical, row.StackId);
            canonical.Append(row.Quantity.ToString(CultureInfo.InvariantCulture))
                .Append('|');
            AppendToken(canonical, row.ExpectedStackSignature);
        }
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical.ToString()));
        StringBuilder fingerprint = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
        {
            fingerprint.Append(value.ToString(
                "x2",
                CultureInfo.InvariantCulture));
        }
        return fingerprint.ToString();
    }

    private static void AppendToken(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(token)
            .Append('|');
    }

    private static bool TryRequireCanonical(string value, out string canonical)
    {
        canonical = value ?? string.Empty;
        return IsCanonicalToken(canonical);
    }

    private static bool IsCanonicalToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalFingerprint(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if ((current < '0' || current > '9')
                && (current < 'a' || current > 'f'))
            {
                return false;
            }
        }
        return true;
    }

    private readonly struct StableRowKey : IEquatable<StableRowKey>
    {
        public StableRowKey(
            string ownerOperationId,
            ItemReservationPurpose purpose,
            string aggregationCohortId,
            string stackId,
            string expectedStackSignature)
        {
            OwnerOperationId = ownerOperationId;
            Purpose = purpose;
            AggregationCohortId = aggregationCohortId;
            StackId = stackId;
            ExpectedStackSignature = expectedStackSignature;
        }

        public string OwnerOperationId { get; }
        public ItemReservationPurpose Purpose { get; }
        public string AggregationCohortId { get; }
        public string StackId { get; }
        public string ExpectedStackSignature { get; }

        public bool Equals(StableRowKey other) =>
            Purpose == other.Purpose
            && string.Equals(
                OwnerOperationId,
                other.OwnerOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                AggregationCohortId,
                other.AggregationCohortId,
                StringComparison.Ordinal)
            && string.Equals(StackId, other.StackId, StringComparison.Ordinal)
            && string.Equals(
                ExpectedStackSignature,
                other.ExpectedStackSignature,
                StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is StableRowKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Purpose;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(
                    OwnerOperationId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(
                    AggregationCohortId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(StackId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(
                    ExpectedStackSignature);
                return hash;
            }
        }
    }
}
