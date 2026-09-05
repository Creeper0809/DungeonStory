using System;

/// <summary>
/// Immutable identity of one FacilityBuffer authority about to receive grams.
/// Fence sources inspect this owner-neutral subject rather than depending on
/// another domain's save or aggregate types.
/// </summary>
public readonly struct FacilityBufferDestinationAdmissionFenceSubject
{
    public FacilityBufferDestinationAdmissionFenceSubject(
        string destinationId,
        string ownerDomain,
        string ownerOperationId,
        string ownerFacilityId)
    {
        DestinationId = destinationId ?? string.Empty;
        OwnerDomain = ownerDomain ?? string.Empty;
        OwnerOperationId = ownerOperationId ?? string.Empty;
        OwnerFacilityId = ownerFacilityId ?? string.Empty;
    }

    public string DestinationId { get; }
    public string OwnerDomain { get; }
    public string OwnerOperationId { get; }
    public string OwnerFacilityId { get; }

    public bool IsCanonical =>
        IsCanonicalRequired(DestinationId)
        && IsCanonicalRequired(OwnerDomain)
        && IsCanonicalRequired(OwnerOperationId)
        && (OwnerFacilityId.Length == 0
            || IsCanonicalRequired(OwnerFacilityId));

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public readonly struct FacilityBufferDestinationAdmissionFenceSnapshot
{
    public FacilityBufferDestinationAdmissionFenceSnapshot(
        string sourceId,
        string operationId,
        long revision)
    {
        if (!IsCanonicalRequired(sourceId)
            || !IsCanonicalRequired(operationId)
            || revision <= 0L)
        {
            throw new ArgumentException(
                "Facility-buffer admission fence snapshot is invalid.");
        }

        SourceId = sourceId;
        OperationId = operationId;
        Revision = revision;
    }

    public string SourceId { get; }
    public string OperationId { get; }
    public long Revision { get; }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>
/// One independently registered authority that can fence new FacilityBuffer
/// admissions while it drains or transfers an existing owner. Same-kind future
/// owners register another source; admission code does not gain owner branches.
/// </summary>
public interface IFacilityBufferDestinationAdmissionFenceSource
{
    string SourceId { get; }
    long Revision { get; }

    bool TryCaptureOpenFence(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        out FacilityBufferDestinationAdmissionFenceSnapshot snapshot);
}

public interface IFacilityBufferDestinationAdmissionFenceQuery
{
    long Revision { get; }

    bool TryCaptureOpenFence(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        out FacilityBufferDestinationAdmissionFenceSnapshot snapshot);
}
