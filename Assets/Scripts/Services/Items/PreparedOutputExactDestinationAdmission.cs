using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum PreparedOutputExactDestinationTargetKind
{
    Warehouse = 0,
    FacilityBuffer = 1
}

public enum PreparedOutputExactDestinationAdmissionFailureCode
{
    None = 0,
    InvalidRequest = 1,
    SourceChanged = 2,
    AuthorityMissing = 3,
    AuthorityStale = 4,
    CapacityUnavailable = 5,
    ReplayConflict = 6,
    CandidateMismatch = 7,
    InvalidPhase = 8,
    RollbackFailed = 9
}

public enum PreparedOutputExactDestinationAdmissionHandoffMode
{
    HaulPlannerAdmissionRequired = 0,
    FacilityBufferAdmissionParticipantOwned = 1
}

public readonly struct PreparedOutputExactDestinationAuthoritySnapshot
{
    internal PreparedOutputExactDestinationAuthoritySnapshot(
        PreparedOutputExactDestinationTargetKind kind,
        string destinationId,
        Vector2Int position,
        string fingerprint,
        long capacityRevision,
        long massAuthorityRevision,
        long maxMassGrams,
        long reservedMassGrams)
    {
        Kind = kind;
        DestinationId = destinationId;
        Position = position;
        Fingerprint = fingerprint;
        CapacityRevision = capacityRevision;
        MassAuthorityRevision = massAuthorityRevision;
        MaxMassGrams = maxMassGrams;
        ReservedMassGrams = reservedMassGrams;
    }

    public PreparedOutputExactDestinationTargetKind Kind { get; }
    public string DestinationId { get; }
    public Vector2Int Position { get; }
    public string Fingerprint { get; }
    public long CapacityRevision { get; }
    public long MassAuthorityRevision { get; }
    public long MaxMassGrams { get; }
    public long ReservedMassGrams { get; }
}

public readonly struct PreparedOutputExactDestinationLotSlice
{
    public PreparedOutputExactDestinationLotSlice(
        string sourceStackId,
        int exactQuantity,
        long expectedSourceReservationRevision,
        string expectedComponentFingerprint,
        long expectedExactMassGrams)
    {
        SourceStackId = sourceStackId;
        ExactQuantity = exactQuantity;
        ExpectedSourceReservationRevision = expectedSourceReservationRevision;
        ExpectedComponentFingerprint = expectedComponentFingerprint;
        ExpectedExactMassGrams = expectedExactMassGrams;
    }

    public string SourceStackId { get; }
    public int ExactQuantity { get; }
    public long ExpectedSourceReservationRevision { get; }
    public string ExpectedComponentFingerprint { get; }
    public long ExpectedExactMassGrams { get; }
}

public readonly struct PreparedOutputExactDestinationAdmissionRequest
{
    private readonly IReadOnlyList<PreparedOutputExactDestinationLotSlice> slices;

    public PreparedOutputExactDestinationAdmissionRequest(
        string admissionOperationId,
        string expectedRouteOperationId,
        string expectedPhysicalReceiptFingerprint,
        string expectedNextDeliveryRevisionFingerprint,
        IReadOnlyList<PreparedOutputExactDestinationLotSlice> exactLotSlices,
        PreparedOutputExactDestinationAuthoritySnapshot targetAuthority)
    {
        AdmissionOperationId = admissionOperationId;
        ExpectedRouteOperationId = expectedRouteOperationId;
        ExpectedPhysicalReceiptFingerprint = expectedPhysicalReceiptFingerprint;
        ExpectedNextDeliveryRevisionFingerprint =
            expectedNextDeliveryRevisionFingerprint;
        PreparedOutputExactDestinationLotSlice[] copied = (exactLotSlices
                ?? throw new ArgumentNullException(nameof(exactLotSlices)))
            .OrderBy(value => value.SourceStackId, StringComparer.Ordinal)
            .ToArray();
        slices = Array.AsReadOnly(copied);
        TargetAuthority = targetAuthority;
    }

    public string AdmissionOperationId { get; }
    public string ExpectedRouteOperationId { get; }
    public string ExpectedPhysicalReceiptFingerprint { get; }
    public string ExpectedNextDeliveryRevisionFingerprint { get; }
    public IReadOnlyList<PreparedOutputExactDestinationLotSlice> ExactLotSlices =>
        slices ?? Array.Empty<PreparedOutputExactDestinationLotSlice>();
    public int TotalQuantity => ExactLotSlices.Sum(value => value.ExactQuantity);
    public long TotalMassGrams => ExactLotSlices.Sum(
        value => value.ExpectedExactMassGrams);
    public PreparedOutputExactDestinationAuthoritySnapshot TargetAuthority { get; }
}

internal enum PreparedOutputExactDestinationAdmissionPhase
{
    Prepared = 0,
    Published = 1,
    Completed = 2,
    RolledBack = 3
}

internal readonly struct PreparedOutputExactDestinationAdmissionHandle
{
    internal PreparedOutputExactDestinationAdmissionHandle(
        PreparedOutputExactDestinationTargetKind kind,
        FacilityBufferMassAdmissionToken facilityToken,
        string exactLotFingerprint,
        string fingerprint)
    {
        Kind = kind;
        FacilityToken = facilityToken;
        ExactLotFingerprint = exactLotFingerprint;
        Fingerprint = fingerprint;
    }

    internal PreparedOutputExactDestinationTargetKind Kind { get; }
    internal FacilityBufferMassAdmissionToken FacilityToken { get; }
    internal string ExactLotFingerprint { get; }
    internal string Fingerprint { get; }
}

public sealed class PreparedOutputExactDestinationAdmissionCandidate
{
    internal PreparedOutputExactDestinationAdmissionCandidate(
        string participantId,
        PreparedOutputExactDestinationAdmissionRequest request,
        PreparedOutputExactDestinationAdmissionHandle handle)
    {
        ParticipantId = participantId;
        Request = request;
        Handle = handle;
        Phase = PreparedOutputExactDestinationAdmissionPhase.Prepared;
    }

    public string ParticipantId { get; }
    public PreparedOutputExactDestinationAdmissionRequest Request { get; }
    public string AdmissionFingerprint => Handle.Fingerprint;
    public long ExactMassGrams => Request.TotalMassGrams;
    public long ReservedMassGrams =>
        Handle.Kind == PreparedOutputExactDestinationTargetKind.FacilityBuffer
        && Phase == PreparedOutputExactDestinationAdmissionPhase.Prepared
            ? Request.TotalMassGrams
            : 0L;
    public PreparedOutputExactDestinationAdmissionHandoffMode HandoffMode =>
        Handle.Kind == PreparedOutputExactDestinationTargetKind.Warehouse
            ? PreparedOutputExactDestinationAdmissionHandoffMode
                .HaulPlannerAdmissionRequired
            : PreparedOutputExactDestinationAdmissionHandoffMode
                .FacilityBufferAdmissionParticipantOwned;
    public IReadOnlyList<string> UnderlyingTokenIds =>
        Handle.Kind == PreparedOutputExactDestinationTargetKind.FacilityBuffer
        && Phase == PreparedOutputExactDestinationAdmissionPhase.Prepared
        && !string.IsNullOrEmpty(Handle.FacilityToken.TokenId)
            ? new[] { Handle.FacilityToken.TokenId }
            : Array.Empty<string>();
    public string FacilityBufferTokenId =>
        Handle.Kind == PreparedOutputExactDestinationTargetKind.FacilityBuffer
            ? Handle.FacilityToken.TokenId
            : string.Empty;
    public bool HasFacilityBufferCommitReceipt { get; internal set; }
    public string FacilityBufferExactLotFingerprint =>
        HasFacilityBufferCommitReceipt ? Handle.ExactLotFingerprint : string.Empty;
    public long FacilityBufferCommittedMassGrams { get; internal set; }
    public bool IsPublished =>
        Phase is PreparedOutputExactDestinationAdmissionPhase.Published
            or PreparedOutputExactDestinationAdmissionPhase.Completed;
    public bool IsCompleted =>
        Phase == PreparedOutputExactDestinationAdmissionPhase.Completed;
    internal PreparedOutputExactDestinationAdmissionHandle Handle { get; }
    internal PreparedOutputExactDestinationAdmissionPhase Phase { get; set; }
}

public interface IPreparedOutputExactDestinationAdmissionParticipant
{
    string ParticipantId { get; }
    bool TryCaptureTargetAuthority(
        PreparedOutputExactDestinationTargetKind kind,
        string destinationId,
        Vector2Int position,
        out PreparedOutputExactDestinationAuthoritySnapshot snapshot,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryPrepare(
        PreparedOutputExactDestinationAdmissionRequest request,
        out PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryPublish(
        PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryRollback(
        PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryComplete(
        PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason);
}

/// <summary>
/// Reserves destination grams for an already custody-owned physical range.
/// Prepare never changes source availability, physical state, destination intent,
/// custody, or haul ownership. Rollback releases an uncommitted FacilityBuffer
/// token or its routed receipt. Warehouse preparation is only a
/// preflight: its durable token is owned later by the haul planner. After the Items
/// overlay publishes, Publish converts the one FacilityBuffer reservation into a
/// routed receipt. Complete only terminalizes after every participant published.
/// </summary>
public sealed class PreparedOutputExactDestinationAdmissionParticipant :
    IPreparedOutputExactDestinationAdmissionParticipant
{
    private const string StableParticipantId =
        "items.prepared-output-exact-destination-admission.v1";
    private readonly WorldItemWarehouseService destinations;
    private readonly Dictionary<string,
        PreparedOutputExactDestinationAdmissionCandidate> candidatesByOperation =
        new(StringComparer.Ordinal);

    public PreparedOutputExactDestinationAdmissionParticipant(
        WorldItemWarehouseService destinations)
    {
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
    }

    public string ParticipantId => StableParticipantId;

    public bool TryCaptureTargetAuthority(
        PreparedOutputExactDestinationTargetKind kind,
        string destinationId,
        Vector2Int position,
        out PreparedOutputExactDestinationAuthoritySnapshot snapshot,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason) => destinations.TryCapturePreparedOutputAuthority(
            kind,
            destinationId,
            position,
            out snapshot,
            out failureCode,
            out failureReason);

    public bool TryPrepare(
        PreparedOutputExactDestinationAdmissionRequest request,
        out PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        candidate = null;
        string operationId = request.AdmissionOperationId ?? string.Empty;
        if (candidatesByOperation.TryGetValue(
                operationId,
                out PreparedOutputExactDestinationAdmissionCandidate existing))
        {
            if (!AdmissionRequestsMatch(existing.Request, request))
            {
                failureCode =
                    PreparedOutputExactDestinationAdmissionFailureCode.ReplayConflict;
                failureReason =
                    "prepared-output destination admission operation conflicts";
                return false;
            }
            if (existing.Phase is PreparedOutputExactDestinationAdmissionPhase.Prepared
                or PreparedOutputExactDestinationAdmissionPhase.Published
                or PreparedOutputExactDestinationAdmissionPhase.Completed)
            {
                candidate = existing;
                failureCode =
                    PreparedOutputExactDestinationAdmissionFailureCode.None;
                failureReason = string.Empty;
                return true;
            }
            failureCode =
                PreparedOutputExactDestinationAdmissionFailureCode.InvalidPhase;
            failureReason =
                "prepared-output destination admission operation is terminal";
            return false;
        }
        if (!destinations.TryPreparePreparedOutputAdmission(
                request,
                out PreparedOutputExactDestinationAdmissionHandle handle,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        candidate = new PreparedOutputExactDestinationAdmissionCandidate(
            ParticipantId,
            request,
            handle);
        candidatesByOperation.Add(operationId, candidate);
        return true;
    }

    public bool TryPublish(
        PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!TryRequire(candidate, out failureCode, out failureReason))
            return false;
        if (candidate.Phase is PreparedOutputExactDestinationAdmissionPhase.Published
            or PreparedOutputExactDestinationAdmissionPhase.Completed)
            return true;
        if (candidate.Phase != PreparedOutputExactDestinationAdmissionPhase.Prepared)
            return FailPhase(out failureCode, out failureReason);
        if (!destinations.TryPublishPreparedOutputAdmission(
                candidate.Request,
                candidate.Handle,
                out long committedMassGrams,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        candidate.HasFacilityBufferCommitReceipt =
            candidate.Handle.Kind ==
            PreparedOutputExactDestinationTargetKind.FacilityBuffer;
        candidate.FacilityBufferCommittedMassGrams = committedMassGrams;
        candidate.Phase = PreparedOutputExactDestinationAdmissionPhase.Published;
        return true;
    }

    public bool TryRollback(
        PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!TryRequire(candidate, out failureCode, out failureReason))
            return false;
        if (candidate.Phase == PreparedOutputExactDestinationAdmissionPhase.RolledBack)
            return true;
        if (candidate.Phase == PreparedOutputExactDestinationAdmissionPhase.Completed)
            return FailPhase(out failureCode, out failureReason);
        if (candidate.Handle.Kind ==
                PreparedOutputExactDestinationTargetKind.Warehouse)
        {
            candidate.Phase = PreparedOutputExactDestinationAdmissionPhase.RolledBack;
            ForgetCandidate(candidate);
            failureCode = PreparedOutputExactDestinationAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
        if (!destinations.TryRollbackPreparedOutputAdmission(
                candidate.Handle,
                candidate.HasFacilityBufferCommitReceipt,
                out failureReason))
        {
            failureCode =
                PreparedOutputExactDestinationAdmissionFailureCode.RollbackFailed;
            return false;
        }
        candidate.Phase = PreparedOutputExactDestinationAdmissionPhase.RolledBack;
        ForgetCandidate(candidate);
        failureCode = PreparedOutputExactDestinationAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    public bool TryComplete(
        PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!TryRequire(candidate, out failureCode, out failureReason))
            return false;
        if (candidate.Phase == PreparedOutputExactDestinationAdmissionPhase.Completed)
            return true;
        if (candidate.Phase != PreparedOutputExactDestinationAdmissionPhase.Published)
            return FailPhase(out failureCode, out failureReason);
        candidate.Phase = PreparedOutputExactDestinationAdmissionPhase.Completed;
        ForgetCandidate(candidate);
        failureCode = PreparedOutputExactDestinationAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    private bool TryRequire(
        PreparedOutputExactDestinationAdmissionCandidate candidate,
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (IsCandidateOwnedOrTerminal(
                candidatesByOperation,
                ParticipantId,
                candidate))
        {
            failureCode = PreparedOutputExactDestinationAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
        failureCode =
            PreparedOutputExactDestinationAdmissionFailureCode.CandidateMismatch;
        failureReason = "prepared-output destination candidate owner mismatched";
        return false;
    }

    internal static bool IsCandidateOwnedOrTerminal(
        IReadOnlyDictionary<string,
            PreparedOutputExactDestinationAdmissionCandidate> candidates,
        string expectedParticipantId,
        PreparedOutputExactDestinationAdmissionCandidate candidate) =>
        candidate != null
        && string.Equals(candidate.ParticipantId, expectedParticipantId,
            StringComparison.Ordinal)
        && (candidate.Phase ==
                PreparedOutputExactDestinationAdmissionPhase.Completed
            || candidate.Phase ==
                PreparedOutputExactDestinationAdmissionPhase.RolledBack
            || IsExactActiveCandidate(candidates, candidate));

    internal static bool IsExactActiveCandidate(
        IReadOnlyDictionary<string,
            PreparedOutputExactDestinationAdmissionCandidate> candidates,
        PreparedOutputExactDestinationAdmissionCandidate candidate)
    {
        if (candidates == null
            || candidate == null
            || candidate.Phase is not
                PreparedOutputExactDestinationAdmissionPhase.Prepared
                and not PreparedOutputExactDestinationAdmissionPhase.Published)
        {
            return false;
        }
        string operationId = candidate.Request.AdmissionOperationId
            ?? string.Empty;
        return candidates.TryGetValue(
                operationId,
                out PreparedOutputExactDestinationAdmissionCandidate registered)
            && ReferenceEquals(registered, candidate);
    }

    private void ForgetCandidate(
        PreparedOutputExactDestinationAdmissionCandidate candidate)
    {
        string operationId = candidate?.Request.AdmissionOperationId
            ?? string.Empty;
        if (candidatesByOperation.TryGetValue(
                operationId,
                out PreparedOutputExactDestinationAdmissionCandidate registered)
            && ReferenceEquals(registered, candidate))
        {
            candidatesByOperation.Remove(operationId);
        }
    }

    private static bool FailPhase(
        out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = PreparedOutputExactDestinationAdmissionFailureCode.InvalidPhase;
        failureReason = "prepared-output destination candidate phase invalid";
        return false;
    }

    private static bool AdmissionRequestsMatch(
        PreparedOutputExactDestinationAdmissionRequest left,
        PreparedOutputExactDestinationAdmissionRequest right)
    {
        if (!string.Equals(left.AdmissionOperationId, right.AdmissionOperationId,
                StringComparison.Ordinal)
            || !string.Equals(left.ExpectedRouteOperationId,
                right.ExpectedRouteOperationId, StringComparison.Ordinal)
            || !string.Equals(left.ExpectedPhysicalReceiptFingerprint,
                right.ExpectedPhysicalReceiptFingerprint, StringComparison.Ordinal)
            || !string.Equals(left.ExpectedNextDeliveryRevisionFingerprint,
                right.ExpectedNextDeliveryRevisionFingerprint,
                StringComparison.Ordinal)
            || !AuthoritySnapshotsMatch(left.TargetAuthority, right.TargetAuthority)
            || left.ExactLotSlices.Count != right.ExactLotSlices.Count)
        {
            return false;
        }
        for (int index = 0; index < left.ExactLotSlices.Count; index++)
        {
            PreparedOutputExactDestinationLotSlice a = left.ExactLotSlices[index];
            PreparedOutputExactDestinationLotSlice b = right.ExactLotSlices[index];
            if (!string.Equals(a.SourceStackId, b.SourceStackId,
                    StringComparison.Ordinal)
                || a.ExactQuantity != b.ExactQuantity
                || a.ExpectedSourceReservationRevision
                    != b.ExpectedSourceReservationRevision
                || !string.Equals(a.ExpectedComponentFingerprint,
                    b.ExpectedComponentFingerprint, StringComparison.Ordinal)
                || a.ExpectedExactMassGrams != b.ExpectedExactMassGrams)
            {
                return false;
            }
        }
        return true;
    }

    private static bool AuthoritySnapshotsMatch(
        PreparedOutputExactDestinationAuthoritySnapshot left,
        PreparedOutputExactDestinationAuthoritySnapshot right) =>
        left.Kind == right.Kind
        && left.Position == right.Position
        && left.CapacityRevision == right.CapacityRevision
        && left.MassAuthorityRevision == right.MassAuthorityRevision
        && left.MaxMassGrams == right.MaxMassGrams
        && left.ReservedMassGrams == right.ReservedMassGrams
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(left.Fingerprint, right.Fingerprint,
            StringComparison.Ordinal);
}
