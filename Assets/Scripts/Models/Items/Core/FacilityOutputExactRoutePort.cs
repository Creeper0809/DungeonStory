using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum FacilityOutputExactRouteFailureCode
{
    None = 0,
    InvalidRequest = 1,
    OperationConflict = 2,
    SourceUnavailable = 3,
    PublicationAuthorityInvalid = 4,
    RangeUnavailable = 5,
    ItemMismatch = 6,
    ComponentMismatch = 7,
    MassMismatch = 8,
    UniquePartialForbidden = 9,
    RepositoryTransactionFailed = 10,
    PendingRouteMissing = 11,
    PhaseMismatch = 12,
    ReceiptMismatch = 13,
    ProtectedRouteBypass = 14,
    RestoreCandidateInvalid = 15
}

public readonly struct FacilityOutputExactRouteFailure
{
    public FacilityOutputExactRouteFailure(
        FacilityOutputExactRouteFailureCode code,
        string reason)
    {
        Code = code;
        Reason = reason ?? string.Empty;
    }

    public FacilityOutputExactRouteFailureCode Code { get; }
    public string Reason { get; }
    public bool IsFailure => Code != FacilityOutputExactRouteFailureCode.None;
    public static FacilityOutputExactRouteFailure None => default;
}

public sealed class FacilityOutputExactRouteSliceRequest
{
    public FacilityOutputExactRouteSliceRequest(
        string outputLineId,
        string lineCommitId,
        string itemId,
        int sourceOffsetQuantity,
        int quantity,
        long exactMassGrams,
        string componentFingerprint)
    {
        OutputLineId = RequireCanonical(outputLineId, nameof(outputLineId));
        LineCommitId = RequireCanonical(lineCommitId, nameof(lineCommitId));
        ItemId = RequireCanonical(itemId, nameof(itemId));
        ComponentFingerprint = RequireCanonical(
            componentFingerprint,
            nameof(componentFingerprint));
        if (sourceOffsetQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceOffsetQuantity));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (exactMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(exactMassGrams));
        SourceOffsetQuantity = sourceOffsetQuantity;
        Quantity = quantity;
        ExactMassGrams = exactMassGrams;
    }

    public string OutputLineId { get; }
    public string LineCommitId { get; }
    public string ItemId { get; }
    public int SourceOffsetQuantity { get; }
    public int Quantity { get; }
    public long ExactMassGrams { get; }
    public string ComponentFingerprint { get; }
    public int EndOffsetQuantity => checked(SourceOffsetQuantity + Quantity);

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Exact-route identifiers must be canonical and non-empty.",
                parameter);
        }
        return value;
    }
}

public sealed class FacilityOutputExactRouteRequest
{
    private readonly IReadOnlyList<FacilityOutputExactRouteSliceRequest> slices;

    public FacilityOutputExactRouteRequest(
        string routeOperationId,
        string batchCommitId,
        string sourceDestinationId,
        string targetDestinationId,
        Vector2Int targetPosition,
        IEnumerable<FacilityOutputExactRouteSliceRequest> slices)
    {
        RouteOperationId = RequireCanonical(
            routeOperationId,
            nameof(routeOperationId));
        BatchCommitId = RequireCanonical(batchCommitId, nameof(batchCommitId));
        SourceDestinationId = RequireCanonical(
            sourceDestinationId,
            nameof(sourceDestinationId));
        TargetDestinationId = RequireOptionalCanonical(
            targetDestinationId,
            nameof(targetDestinationId));
        TargetPosition = targetPosition;
        FacilityOutputExactRouteSliceRequest[] canonical = (slices
                ?? throw new ArgumentNullException(nameof(slices)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value?.SourceOffsetQuantity ?? -1)
            .ThenBy(value => value?.LineCommitId, StringComparer.Ordinal)
            .ToArray();
        if (canonical.Length == 0 || canonical.Any(value => value == null))
            throw new ArgumentException("Exact route requires output slices.", nameof(slices));
        foreach (IGrouping<string, FacilityOutputExactRouteSliceRequest> line in
                 canonical.GroupBy(value => value.OutputLineId, StringComparer.Ordinal))
        {
            FacilityOutputExactRouteSliceRequest previous = null;
            foreach (FacilityOutputExactRouteSliceRequest current in line)
            {
                if (previous != null
                    && current.SourceOffsetQuantity < previous.EndOffsetQuantity)
                {
                    throw new ArgumentException(
                        $"Exact-route line '{line.Key}' contains overlapping ranges.",
                        nameof(slices));
                }
                previous = current;
            }
        }
        this.slices = Array.AsReadOnly(canonical);
        TotalQuantity = canonical.Sum(value => value.Quantity);
        TotalMassGrams = canonical.Sum(value => value.ExactMassGrams);
        RequestFingerprint = FacilityOutputExactRouteFingerprint.CreateRequest(this);
    }

    public string RouteOperationId { get; }
    public string BatchCommitId { get; }
    public string SourceDestinationId { get; }
    public string TargetDestinationId { get; }
    public Vector2Int TargetPosition { get; }
    public IReadOnlyList<FacilityOutputExactRouteSliceRequest> Slices => slices;
    public int TotalQuantity { get; }
    public long TotalMassGrams { get; }
    public string RequestFingerprint { get; }

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Exact-route identifiers must be canonical and non-empty.",
                parameter);
        }
        return value;
    }

    private static string RequireOptionalCanonical(string value, string parameter)
    {
        string canonical = value ?? string.Empty;
        if (!string.Equals(canonical, canonical.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                "Exact-route identifiers must already be canonical.",
                parameter);
        return canonical;
    }
}

public sealed class FacilityOutputExactRouteSliceReceipt
{
    public FacilityOutputExactRouteSliceReceipt(
        string sourceStackId,
        string routedStackId,
        string outputLineId,
        string lineCommitId,
        string itemId,
        int sourceOffsetQuantity,
        int routedOffsetQuantity,
        int routedQuantity,
        long routedMassGrams,
        string componentFingerprint)
    {
        SourceStackId = Require(sourceStackId, nameof(sourceStackId));
        RoutedStackId = Require(routedStackId, nameof(routedStackId));
        OutputLineId = Require(outputLineId, nameof(outputLineId));
        LineCommitId = Require(lineCommitId, nameof(lineCommitId));
        ItemId = Require(itemId, nameof(itemId));
        ComponentFingerprint = Require(
            componentFingerprint,
            nameof(componentFingerprint));
        if (sourceOffsetQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceOffsetQuantity));
        if (routedOffsetQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(routedOffsetQuantity));
        if (routedQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(routedQuantity));
        if (routedMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(routedMassGrams));
        SourceOffsetQuantity = sourceOffsetQuantity;
        RoutedOffsetQuantity = routedOffsetQuantity;
        RoutedQuantity = routedQuantity;
        RoutedMassGrams = routedMassGrams;
    }

    public string SourceStackId { get; }
    public string RoutedStackId { get; }
    public string OutputLineId { get; }
    public string LineCommitId { get; }
    public string ItemId { get; }
    public int SourceOffsetQuantity { get; }
    public int RoutedOffsetQuantity { get; }
    public int RoutedQuantity { get; }
    public long RoutedMassGrams { get; }
    public string ComponentFingerprint { get; }

    private static string Require(string value, string parameter)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Receipt identifiers must be canonical.", parameter);
        return value;
    }
}

public sealed class FacilityOutputExactRouteReceipt
{
    private readonly IReadOnlyList<FacilityOutputExactRouteSliceReceipt> slices;

    public FacilityOutputExactRouteReceipt(
        string routeOperationId,
        string requestFingerprint,
        string physicalReceiptFingerprint,
        string batchCommitId,
        string sourceDestinationId,
        string targetDestinationId,
        Vector2Int targetPosition,
        int totalQuantity,
        long totalMassGrams,
        IEnumerable<FacilityOutputExactRouteSliceReceipt> slices)
    {
        RouteOperationId = Require(routeOperationId, nameof(routeOperationId));
        RequestFingerprint = Require(requestFingerprint, nameof(requestFingerprint));
        PhysicalReceiptFingerprint = Require(
            physicalReceiptFingerprint,
            nameof(physicalReceiptFingerprint));
        BatchCommitId = Require(batchCommitId, nameof(batchCommitId));
        SourceDestinationId = Require(
            sourceDestinationId,
            nameof(sourceDestinationId));
        TargetDestinationId = RequireOptional(
            targetDestinationId,
            nameof(targetDestinationId));
        TargetPosition = targetPosition;
        FacilityOutputExactRouteSliceReceipt[] canonical = (slices
                ?? throw new ArgumentNullException(nameof(slices)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value?.SourceOffsetQuantity ?? -1)
            .ThenBy(value => value?.SourceStackId, StringComparer.Ordinal)
            .ThenBy(value => value?.RoutedStackId, StringComparer.Ordinal)
            .ToArray();
        if (canonical.Length == 0 || canonical.Any(value => value == null))
            throw new ArgumentException("Route receipt requires physical slices.", nameof(slices));
        if (totalQuantity <= 0
            || totalMassGrams <= 0L
            || canonical.Sum(value => value.RoutedQuantity) != totalQuantity
            || canonical.Sum(value => value.RoutedMassGrams) != totalMassGrams)
        {
            throw new ArgumentException("Route receipt totals do not match its slices.");
        }
        TotalQuantity = totalQuantity;
        TotalMassGrams = totalMassGrams;
        this.slices = Array.AsReadOnly(canonical);
    }

    public string RouteOperationId { get; }
    public string RequestFingerprint { get; }
    public string PhysicalReceiptFingerprint { get; }
    public string BatchCommitId { get; }
    public string SourceDestinationId { get; }
    public string TargetDestinationId { get; }
    public Vector2Int TargetPosition { get; }
    public int TotalQuantity { get; }
    public long TotalMassGrams { get; }
    public IReadOnlyList<FacilityOutputExactRouteSliceReceipt> Slices => slices;

    private static string Require(string value, string parameter)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Receipt identifiers must be canonical.", parameter);
        return value;
    }


    private static string RequireOptional(string value, string parameter)
    {
        string canonical = value ?? string.Empty;
        if (!string.Equals(canonical, canonical.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Receipt identifiers must be canonical.", parameter);
        return canonical;
    }
}

public sealed class FacilityOutputExactRoutePendingSnapshot
{
    public FacilityOutputExactRoutePendingSnapshot(
        FacilityOutputExactRoutePhase phase,
        FacilityOutputExactRouteReceipt receipt,
        FacilityOutputExactRouteDeliveryRevisionSnapshot deliveryRevision = null)
    {
        if (phase is not (FacilityOutputExactRoutePhase.PhysicalPending
                or FacilityOutputExactRoutePhase.Routable))
            throw new ArgumentOutOfRangeException(nameof(phase));
        Phase = phase;
        Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        DeliveryRevision = deliveryRevision
            ?? FacilityOutputExactRouteDeliveryRevisionSnapshot.CreateInitial(
                Receipt.RouteOperationId,
                Receipt.RequestFingerprint,
                Receipt.PhysicalReceiptFingerprint,
                Receipt.TargetDestinationId,
                Receipt.TargetPosition.x,
                Receipt.TargetPosition.y);
        if (!string.Equals(
                DeliveryRevision.RouteOperationId,
                Receipt.RouteOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                DeliveryRevision.OriginalPhysicalReceiptFingerprint,
                Receipt.PhysicalReceiptFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Exact-route delivery revision conflicts with its physical receipt.",
                nameof(deliveryRevision));
        }
    }

    public FacilityOutputExactRoutePhase Phase { get; }
    public FacilityOutputExactRouteReceipt Receipt { get; }
    public FacilityOutputExactRouteDeliveryRevisionSnapshot DeliveryRevision { get; }
}

public interface IFacilityOutputExactRouteOutboxQuery
{
    IReadOnlyList<FacilityOutputExactRoutePendingSnapshot> CapturePendingRoutes();
}

public interface IFacilityOutputExactRoutePort :
    IFacilityOutputExactRouteOutboxQuery
{
    bool TryRoute(
        FacilityOutputExactRouteRequest request,
        out FacilityOutputExactRouteReceipt receipt,
        out FacilityOutputExactRouteFailure failure);
    bool TryAcknowledge(
        string routeOperationId,
        string physicalReceiptFingerprint,
        out FacilityOutputExactRouteReceipt receipt,
        out FacilityOutputExactRouteFailure failure);
    bool TryForgetRoutable(
        string routeOperationId,
        string physicalReceiptFingerprint,
        out FacilityOutputExactRouteFailure failure);
}

public sealed class FacilityOutputExactRouteBypassException : InvalidOperationException
{
    public FacilityOutputExactRouteBypassException(
        FacilityOutputExactRouteFailureCode code,
        string operation)
        : base($"Prepared output requires IFacilityOutputExactRoutePort: {operation}")
    {
        Code = code;
        Operation = operation ?? string.Empty;
    }

    public FacilityOutputExactRouteFailureCode Code { get; }
    public string Operation { get; }
}

public static class FacilityOutputExactRouteFingerprint
{
    public const string RequestDomain = "facility-output-exact-route-request-v1";
    public const string ReceiptDomain = "facility-output-exact-route-receipt-v1";

    public static string CreateRequest(FacilityOutputExactRouteRequest request)
    {
        List<string> tokens = new()
        {
            RequestDomain,
            request.RouteOperationId,
            request.BatchCommitId,
            request.SourceDestinationId,
            request.TargetDestinationId,
            request.TargetPosition.x.ToString(CultureInfo.InvariantCulture),
            request.TargetPosition.y.ToString(CultureInfo.InvariantCulture)
        };
        foreach (FacilityOutputExactRouteSliceRequest slice in request.Slices)
        {
            tokens.Add(slice.OutputLineId);
            tokens.Add(slice.LineCommitId);
            tokens.Add(slice.SourceOffsetQuantity.ToString(CultureInfo.InvariantCulture));
            tokens.Add(slice.Quantity.ToString(CultureInfo.InvariantCulture));
            tokens.Add(slice.ItemId);
            tokens.Add(slice.ComponentFingerprint);
            tokens.Add(slice.ExactMassGrams.ToString(CultureInfo.InvariantCulture));
        }
        return Hash(tokens);
    }

    public static string CreatePhysicalReceipt(
        FacilityOutputExactRouteRequest request,
        IEnumerable<FacilityOutputExactRouteSliceReceipt> slices)
    {
        List<string> tokens = new()
        {
            ReceiptDomain,
            request.RequestFingerprint,
            request.RouteOperationId,
            request.BatchCommitId,
            request.SourceDestinationId,
            request.TargetDestinationId,
            request.TargetPosition.x.ToString(CultureInfo.InvariantCulture),
            request.TargetPosition.y.ToString(CultureInfo.InvariantCulture),
            request.TotalQuantity.ToString(CultureInfo.InvariantCulture),
            request.TotalMassGrams.ToString(CultureInfo.InvariantCulture)
        };
        foreach (FacilityOutputExactRouteSliceReceipt slice in slices
                     .OrderBy(value => value.SourceStackId, StringComparer.Ordinal)
                     .ThenBy(value => value.RoutedStackId, StringComparer.Ordinal))
        {
            tokens.Add(slice.SourceStackId);
            tokens.Add(slice.RoutedStackId);
            tokens.Add(slice.OutputLineId);
            tokens.Add(slice.LineCommitId);
            tokens.Add(slice.SourceOffsetQuantity.ToString(CultureInfo.InvariantCulture));
            tokens.Add(slice.RoutedOffsetQuantity.ToString(CultureInfo.InvariantCulture));
            tokens.Add(slice.RoutedQuantity.ToString(CultureInfo.InvariantCulture));
            tokens.Add(slice.ItemId);
            tokens.Add(slice.ComponentFingerprint);
            tokens.Add(slice.RoutedMassGrams.ToString(CultureInfo.InvariantCulture));
        }
        return Hash(tokens);
    }

    private static string Hash(IEnumerable<string> tokens)
    {
        StringBuilder canonical = new();
        foreach (string value in tokens)
        {
            string token = value ?? string.Empty;
            canonical.Append(Encoding.UTF8.GetByteCount(token).ToString(
                CultureInfo.InvariantCulture));
            canonical.Append(':');
            canonical.Append(token);
            canonical.Append('|');
        }
        using SHA256 sha256 = SHA256.Create();
        byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(
            canonical.ToString()));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }
}
