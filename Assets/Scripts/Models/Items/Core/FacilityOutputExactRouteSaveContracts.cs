using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public enum FacilityOutputExactRoutePhase
{
    PhysicalPending = 1,
    Routable = 2
}

[Serializable]
public sealed class FacilityOutputExactRouteSliceSaveData
{
    public string sourceStackId = string.Empty;
    public string routedStackId = string.Empty;
    public string outputLineId = string.Empty;
    public string lineCommitId = string.Empty;
    public string itemId = string.Empty;
    public int sourceOffsetQuantity;
    public int routedOffsetQuantity;
    public int routedQuantity;
    public long routedMassGrams;
    public string componentFingerprint = string.Empty;

    public FacilityOutputExactRouteSliceSaveData Clone() => new()
    {
        sourceStackId = sourceStackId ?? string.Empty,
        routedStackId = routedStackId ?? string.Empty,
        outputLineId = outputLineId ?? string.Empty,
        lineCommitId = lineCommitId ?? string.Empty,
        itemId = itemId ?? string.Empty,
        sourceOffsetQuantity = sourceOffsetQuantity,
        routedOffsetQuantity = routedOffsetQuantity,
        routedQuantity = routedQuantity,
        routedMassGrams = routedMassGrams,
        componentFingerprint = componentFingerprint ?? string.Empty
    };
}

[Serializable]
public sealed class FacilityOutputExactRouteOutboxSaveData
{
    public FacilityOutputExactRoutePhase phase;
    public string routeOperationId = string.Empty;
    public string requestFingerprint = string.Empty;
    public string physicalReceiptFingerprint = string.Empty;
    public string batchCommitId = string.Empty;
    public string sourceDestinationId = string.Empty;
    public string targetDestinationId = string.Empty;
    public int targetPositionX;
    public int targetPositionY;
    public int totalQuantity;
    public long totalMassGrams;
    public long currentDeliveryRevision;
    public string currentDeliveryRevisionFingerprint = string.Empty;
    public string currentDeliveryRerouteOperationId = string.Empty;
    public string currentTargetDestinationId = string.Empty;
    public int currentTargetPositionX;
    public int currentTargetPositionY;
    public string currentTargetAuthorityFingerprint = string.Empty;
    public List<FacilityOutputExactRouteSliceSaveData> slices = new();

    public FacilityOutputExactRouteOutboxSaveData Clone() => new()
    {
        phase = phase,
        routeOperationId = routeOperationId ?? string.Empty,
        requestFingerprint = requestFingerprint ?? string.Empty,
        physicalReceiptFingerprint = physicalReceiptFingerprint ?? string.Empty,
        batchCommitId = batchCommitId ?? string.Empty,
        sourceDestinationId = sourceDestinationId ?? string.Empty,
        targetDestinationId = targetDestinationId ?? string.Empty,
        targetPositionX = targetPositionX,
        targetPositionY = targetPositionY,
        totalQuantity = totalQuantity,
        totalMassGrams = totalMassGrams,
        currentDeliveryRevision = currentDeliveryRevision,
        currentDeliveryRevisionFingerprint =
            currentDeliveryRevisionFingerprint ?? string.Empty,
        currentDeliveryRerouteOperationId =
            currentDeliveryRerouteOperationId ?? string.Empty,
        currentTargetDestinationId = currentTargetDestinationId ?? string.Empty,
        currentTargetPositionX = currentTargetPositionX,
        currentTargetPositionY = currentTargetPositionY,
        currentTargetAuthorityFingerprint =
            currentTargetAuthorityFingerprint ?? string.Empty,
        slices = (slices ?? new List<FacilityOutputExactRouteSliceSaveData>())
            .Select(value => value?.Clone())
            .ToList()
    };
}

public sealed class FacilityOutputExactRouteDeliveryRevisionSnapshot
{
    public FacilityOutputExactRouteDeliveryRevisionSnapshot(
        string routeOperationId,
        string originalPhysicalReceiptFingerprint,
        long revision,
        string revisionFingerprint,
        string rerouteOperationId,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string targetAuthorityFingerprint)
    {
        RouteOperationId = RequireCanonical(routeOperationId, false);
        OriginalPhysicalReceiptFingerprint = RequireDigest(
            originalPhysicalReceiptFingerprint);
        if (revision < 0L)
            throw new ArgumentOutOfRangeException(nameof(revision));
        Revision = revision;
        RevisionFingerprint = RequireDigest(revisionFingerprint);
        RerouteOperationId = RequireCanonical(rerouteOperationId, revision == 0L);
        TargetDestinationId = RequireCanonical(
            targetDestinationId,
            revision == 0L);
        TargetPositionX = targetPositionX;
        TargetPositionY = targetPositionY;
        TargetAuthorityFingerprint = revision == 0L
            ? RequireEmpty(targetAuthorityFingerprint, nameof(targetAuthorityFingerprint))
            : RequireDigest(targetAuthorityFingerprint);
    }

    public string RouteOperationId { get; }
    public string OriginalPhysicalReceiptFingerprint { get; }
    public long Revision { get; }
    public string RevisionFingerprint { get; }
    public string RerouteOperationId { get; }
    public string TargetDestinationId { get; }
    public int TargetPositionX { get; }
    public int TargetPositionY { get; }
    public string TargetAuthorityFingerprint { get; }

    public static FacilityOutputExactRouteDeliveryRevisionSnapshot CreateInitial(
        string routeOperationId,
        string requestFingerprint,
        string physicalReceiptFingerprint,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY)
    {
        string target = RequireCanonical(targetDestinationId, true);
        string revisionFingerprint =
            FacilityOutputExactRouteDeliveryRevisionFingerprint.CreateInitial(
                routeOperationId,
                requestFingerprint,
                physicalReceiptFingerprint,
                target,
                targetPositionX,
                targetPositionY);
        return new FacilityOutputExactRouteDeliveryRevisionSnapshot(
            routeOperationId,
            physicalReceiptFingerprint,
            0L,
            revisionFingerprint,
            string.Empty,
            target,
            targetPositionX,
            targetPositionY,
            string.Empty);
    }

    private static string RequireCanonical(string value, bool allowEmpty)
    {
        string exact = value ?? string.Empty;
        if ((!allowEmpty && exact.Length == 0)
            || !string.Equals(exact, exact.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Delivery revision identity is not canonical.");
        }
        return exact;
    }

    private static string RequireDigest(string value)
    {
        string exact = value ?? string.Empty;
        if (exact.Length != 64 || exact.Any(character =>
                !(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f')))
        {
            throw new ArgumentException("Delivery revision fingerprint is invalid.");
        }
        return exact;
    }

    private static string RequireEmpty(string value, string parameter)
    {
        if (!string.IsNullOrEmpty(value))
            throw new ArgumentException(
                "Initial delivery revision cannot own a target authority fingerprint.",
                parameter);
        return string.Empty;
    }
}

public static class FacilityOutputExactRouteDeliveryRevisionFingerprint
{
    private const string DeliveryRevisionV1 =
        "prepared-output-delivery-revision-v1";

    public static string CreateInitial(
        string routeOperationId,
        string requestFingerprint,
        string physicalReceiptFingerprint,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY) => Digest(
        DeliveryRevisionV1,
        routeOperationId,
        requestFingerprint,
        "0",
        string.IsNullOrEmpty(targetDestinationId) ? "2" : "1",
        "1",
        string.Empty,
        string.Empty,
        physicalReceiptFingerprint,
        targetDestinationId,
        targetPositionX.ToString(CultureInfo.InvariantCulture),
        targetPositionY.ToString(CultureInfo.InvariantCulture),
        string.Empty);

    private static string Digest(params string[] values)
    {
        StringBuilder text = new();
        foreach (string value in values)
        {
            string exact = value ?? string.Empty;
            text.Append(Encoding.UTF8.GetByteCount(exact).ToString(
                CultureInfo.InvariantCulture));
            text.Append(':').Append(exact).Append('|');
        }
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }
}

public sealed class FacilityOutputExactRouteRestoreCandidate
{
    public FacilityOutputExactRouteRestoreCandidate(
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> routes,
        long checkpointSequence = 0L,
        string checkpointDigest = "")
    {
        Routes = Array.AsReadOnly((routes
                ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .Select(value => value?.Clone())
            .ToArray());
        CheckpointSequence = checkpointSequence;
        CheckpointDigest = checkpointDigest ?? string.Empty;
    }

    public FacilityOutputExactRouteRestoreCandidate(
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> routes,
        IFacilityOutputExactRoutePreparedRestoreState preparedState,
        long checkpointSequence = 0L,
        string checkpointDigest = "")
        : this(routes, checkpointSequence, checkpointDigest)
    {
        PreparedState = preparedState
            ?? throw new ArgumentNullException(nameof(preparedState));
    }

    public IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> Routes { get; }
    public IFacilityOutputExactRoutePreparedRestoreState PreparedState { get; }
    public long CheckpointSequence { get; }
    public string CheckpointDigest { get; }
}

public interface IFacilityOutputExactRoutePreparedRestoreState
{
}

public interface IFacilityOutputExactRouteOutboxPersistence
{
    long LastConfirmedCheckpointSequence { get; }
    string LastConfirmedCheckpointDigest { get; }
    IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> CaptureOutbox();

    FacilityOutputExactRouteRestoreCandidate BuildRestoreCandidate(
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> routes,
        IReadOnlyList<WorldItemStackSaveData> physicalStacks,
        long checkpointSequence = 0L,
        string checkpointDigest = "");

    void RestoreCandidate(
        FacilityOutputExactRouteRestoreCandidate candidate);
}

public interface IFacilityOutputExactRouteRestoreCandidateQuery
{
    bool IsCandidateAvailable { get; }
    long LastConfirmedCheckpointSequence { get; }
    string LastConfirmedCheckpointDigest { get; }
    IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> Routes { get; }

    bool TryGetRoute(
        string routeOperationId,
        out FacilityOutputExactRouteOutboxSaveData route);
}

public interface IFacilityOutputExactRouteDeliveryRevisionRestoreCandidateQuery
{
    IReadOnlyList<FacilityOutputExactRouteDeliveryRevisionSnapshot>
        CurrentDeliveryRevisions { get; }
    bool TryGetCurrentDeliveryRevision(
        string routeOperationId,
        out FacilityOutputExactRouteDeliveryRevisionSnapshot revision);
}

public enum FacilityOutputExactRouteDeliveryOverlayStatus
{
    Prepared = 1,
    Replay = 2,
    Deferred = 3
}

public enum FacilityOutputExactRouteDeliveryOverlayReason
{
    None = 0,
    AuthorityBusy = 1,
    PhysicalStateNotStable = 2
}

public sealed class FacilityOutputExactRouteDeliverySubjectSnapshot
{
    public FacilityOutputExactRouteDeliverySubjectSnapshot(
        string stackId,
        int quantity,
        long reservationRevision,
        string componentFingerprint,
        long exactMassGrams,
        string routeOperationId,
        string physicalReceiptFingerprint)
    {
        StackId = stackId ?? string.Empty;
        Quantity = quantity;
        ReservationRevision = reservationRevision;
        ComponentFingerprint = componentFingerprint ?? string.Empty;
        ExactMassGrams = exactMassGrams;
        RouteOperationId = routeOperationId ?? string.Empty;
        PhysicalReceiptFingerprint = physicalReceiptFingerprint ?? string.Empty;
    }

    public string StackId { get; }
    public int Quantity { get; }
    public long ReservationRevision { get; }
    public string ComponentFingerprint { get; }
    public long ExactMassGrams { get; }
    public string RouteOperationId { get; }
    public string PhysicalReceiptFingerprint { get; }
}

public interface IFacilityOutputExactRouteDeliveryOverlayCandidate
{
    FacilityOutputExactRouteDeliveryOverlayStatus Status { get; }
    FacilityOutputExactRouteDeliveryOverlayReason Reason { get; }
    string Message { get; }
    string RouteOperationId { get; }
    long ExpectedCurrentRevision { get; }
    string ExpectedCurrentRevisionFingerprint { get; }
    FacilityOutputExactRouteDeliveryRevisionSnapshot Next { get; }
    IReadOnlyList<FacilityOutputExactRouteDeliverySubjectSnapshot>
        DeliverySubjects { get; }
}

public interface IFacilityOutputExactRouteDeliveryOverlayParticipant
{
    FacilityOutputExactRouteDeliveryRevisionSnapshot CaptureCurrentDelivery(
        string routeOperationId);

    [GameplayInternalOnly(
        "Items delivery overlay must be coordinated with the Economy revision and downstream haul/admission participants.",
        "Prepared-output delivery reroute coordinator only")]
    IFacilityOutputExactRouteDeliveryOverlayCandidate PrepareDeliveryOverlay(
        string routeOperationId,
        long expectedCurrentRevision,
        string expectedCurrentRevisionFingerprint,
        string originalPhysicalReceiptFingerprint,
        long nextRevision,
        string nextRevisionFingerprint,
        string rerouteOperationId,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string targetAuthorityFingerprint);

    [GameplayInternalOnly(
        "Publishes only a detached exact-route Items overlay candidate.",
        "Prepared-output delivery reroute coordinator only")]
    void PublishDeliveryOverlay(
        IFacilityOutputExactRouteDeliveryOverlayCandidate candidate);

    [GameplayInternalOnly(
        "Rolls back the Items overlay when any reroute participant fails.",
        "Prepared-output delivery reroute coordinator only")]
    void RollbackDeliveryOverlay(
        IFacilityOutputExactRouteDeliveryOverlayCandidate candidate);

    [GameplayInternalOnly(
        "Completes the Items overlay only after the upper reroute transaction commits.",
        "Prepared-output delivery reroute coordinator only")]
    void CompleteDeliveryOverlay(
        IFacilityOutputExactRouteDeliveryOverlayCandidate candidate);
}

public sealed class EmptyFacilityOutputExactRouteOutboxPersistence :
    IFacilityOutputExactRouteOutboxPersistence
{
    public static readonly EmptyFacilityOutputExactRouteOutboxPersistence
        Instance = new();

    private EmptyFacilityOutputExactRouteOutboxPersistence()
    {
    }

    public IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> CaptureOutbox() =>
        Array.Empty<FacilityOutputExactRouteOutboxSaveData>();

    public long LastConfirmedCheckpointSequence => 0L;
    public string LastConfirmedCheckpointDigest => string.Empty;

    public FacilityOutputExactRouteRestoreCandidate BuildRestoreCandidate(
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> routes,
        IReadOnlyList<WorldItemStackSaveData> physicalStacks,
        long checkpointSequence = 0L,
        string checkpointDigest = "")
    {
        if ((routes ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .Count != 0)
        {
            throw new InvalidOperationException(
                "Exact-output-route restore requires the physical route authority.");
        }
        if (checkpointSequence != 0L || !string.IsNullOrEmpty(checkpointDigest))
        {
            throw new InvalidOperationException(
                "Empty exact-output-route authority cannot restore checkpoint state.");
        }
        return new FacilityOutputExactRouteRestoreCandidate(
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>());
    }

    public void RestoreCandidate(
        FacilityOutputExactRouteRestoreCandidate candidate)
    {
        if (candidate == null || candidate.Routes.Count != 0)
        {
            throw new InvalidOperationException(
                "The empty exact-output-route authority cannot stage routes.");
        }
    }
}
