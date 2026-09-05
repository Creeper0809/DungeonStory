using System;
using System.Collections.Generic;
using System.Linq;

public interface IDurableFacilityEquipmentAdmissionFenceCommand
{
    bool TryOpen(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        string operationId,
        out string failureReason);

    bool TryClose(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        string operationId,
        out string failureReason);

    IReadOnlyList<DurableFacilityEquipmentAdmissionFenceRecord> CaptureAll();

    bool TryReplaceAll(
        IReadOnlyList<DurableFacilityEquipmentAdmissionFenceRecord> records,
        out string failureReason);
}

public sealed class DurableFacilityEquipmentAdmissionFenceRecord
{
    public DurableFacilityEquipmentAdmissionFenceRecord(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        string operationId)
    {
        if (!subject.IsCanonical
            || string.IsNullOrWhiteSpace(operationId)
            || !string.Equals(
                operationId,
                operationId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Durable equipment admission-fence record is invalid.");
        }
        Subject = subject;
        OperationId = operationId;
    }

    public FacilityBufferDestinationAdmissionFenceSubject Subject { get; }
    public string OperationId { get; }
}

public sealed class DurableFacilityEquipmentAdmissionFenceRegistry :
    IDurableFacilityEquipmentAdmissionFenceCommand,
    IFacilityBufferDestinationAdmissionFenceSource
{
    public const string StableSourceId =
        "items.durable-facility-equipment-slot";

    private Dictionary<string, OpenFence> byDestination =
        new(StringComparer.Ordinal);
    private long revision = 1L;

    public string SourceId => StableSourceId;
    public long Revision => revision;

    public IReadOnlyList<DurableFacilityEquipmentAdmissionFenceRecord>
        CaptureAll() => byDestination.Values
        .OrderBy(value => value.Subject.DestinationId, StringComparer.Ordinal)
        .Select(value => new DurableFacilityEquipmentAdmissionFenceRecord(
            value.Subject,
            value.OperationId))
        .ToArray();

    [GameplayInternalOnly(
        "Atomically restores the complete derived durable-equipment fence set.",
        "DurableFacilityEquipmentSlotRuntime restore participant only")]
    public bool TryReplaceAll(
        IReadOnlyList<DurableFacilityEquipmentAdmissionFenceRecord> records,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, OpenFence> desired =
            new(StringComparer.Ordinal);
        foreach (DurableFacilityEquipmentAdmissionFenceRecord record in
                 records ?? Array.Empty<
                     DurableFacilityEquipmentAdmissionFenceRecord>())
        {
            if (record == null
                || !record.Subject.IsCanonical
                || !Canonical(record.OperationId)
                || !desired.TryAdd(
                    record.Subject.DestinationId,
                    new OpenFence(record.Subject, record.OperationId)))
            {
                failureReason =
                    "durable-equipment-admission-fence-replacement-invalid";
                return false;
            }
        }
        if (MapsEqual(byDestination, desired))
            return true;
        long nextRevision;
        try
        {
            nextRevision = checked(revision + 1L);
        }
        catch (OverflowException)
        {
            failureReason =
                "durable-equipment-admission-fence-revision-overflow";
            return false;
        }
        byDestination = desired;
        revision = nextRevision;
        return true;
    }

    [GameplayInternalOnly(
        "Opens the derived admission fence before a durable equipment slot begins physical drain.",
        "DurableFacilityEquipmentSlotRuntime only")]
    public bool TryOpen(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        string operationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!subject.IsCanonical || !Canonical(operationId))
        {
            failureReason = "durable-equipment-admission-fence-input-invalid";
            return false;
        }
        if (byDestination.TryGetValue(
                subject.DestinationId,
                out OpenFence existing))
        {
            if (existing.Matches(subject, operationId))
                return true;
            failureReason =
                "durable-equipment-admission-fence-conflict:"
                + subject.DestinationId;
            return false;
        }
        long nextRevision;
        try
        {
            nextRevision = checked(revision + 1L);
        }
        catch (OverflowException)
        {
            failureReason =
                "durable-equipment-admission-fence-revision-overflow";
            return false;
        }
        Dictionary<string, OpenFence> desired = new(
            byDestination,
            StringComparer.Ordinal)
        {
            [subject.DestinationId] = new OpenFence(subject, operationId)
        };
        byDestination = desired;
        revision = nextRevision;
        return true;
    }

    [GameplayInternalOnly(
        "Closes the exact derived admission fence only after owner acknowledgement.",
        "DurableFacilityEquipmentSlotRuntime only")]
    public bool TryClose(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        string operationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!subject.IsCanonical || !Canonical(operationId))
        {
            failureReason = "durable-equipment-admission-fence-input-invalid";
            return false;
        }
        if (!byDestination.TryGetValue(
                subject.DestinationId,
                out OpenFence existing))
        {
            return true;
        }
        if (!existing.Matches(subject, operationId))
        {
            failureReason =
                "durable-equipment-admission-fence-close-conflict:"
                + subject.DestinationId;
            return false;
        }
        long nextRevision;
        try
        {
            nextRevision = checked(revision + 1L);
        }
        catch (OverflowException)
        {
            failureReason =
                "durable-equipment-admission-fence-revision-overflow";
            return false;
        }
        Dictionary<string, OpenFence> desired = new(
            byDestination,
            StringComparer.Ordinal);
        desired.Remove(subject.DestinationId);
        byDestination = desired;
        revision = nextRevision;
        return true;
    }

    public bool TryCaptureOpenFence(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        out FacilityBufferDestinationAdmissionFenceSnapshot snapshot)
    {
        snapshot = default;
        if (!subject.IsCanonical)
            throw new ArgumentException(
                "Durable equipment admission fence subject is invalid.",
                nameof(subject));
        if (!byDestination.TryGetValue(
                subject.DestinationId,
                out OpenFence existing)
            || !existing.SubjectEquals(subject))
        {
            return false;
        }
        snapshot = new FacilityBufferDestinationAdmissionFenceSnapshot(
            SourceId,
            existing.OperationId,
            revision);
        return true;
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool MapsEqual(
        IReadOnlyDictionary<string, OpenFence> left,
        IReadOnlyDictionary<string, OpenFence> right)
    {
        if (left.Count != right.Count)
            return false;
        foreach (KeyValuePair<string, OpenFence> pair in left)
        {
            if (!right.TryGetValue(pair.Key, out OpenFence other)
                || !pair.Value.Matches(
                    other.Subject,
                    other.OperationId))
            {
                return false;
            }
        }
        return true;
    }

    private sealed class OpenFence
    {
        internal OpenFence(
            FacilityBufferDestinationAdmissionFenceSubject subject,
            string operationId)
        {
            Subject = subject;
            OperationId = operationId;
        }

        internal FacilityBufferDestinationAdmissionFenceSubject Subject { get; }
        internal string OperationId { get; }

        internal bool Matches(
            FacilityBufferDestinationAdmissionFenceSubject subject,
            string operationId) => SubjectEquals(subject)
            && string.Equals(OperationId, operationId, StringComparison.Ordinal);

        internal bool SubjectEquals(
            FacilityBufferDestinationAdmissionFenceSubject subject) =>
            string.Equals(Subject.DestinationId, subject.DestinationId,
                StringComparison.Ordinal)
            && string.Equals(Subject.OwnerDomain, subject.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(Subject.OwnerOperationId,
                subject.OwnerOperationId,
                StringComparison.Ordinal)
            && string.Equals(Subject.OwnerFacilityId,
                subject.OwnerFacilityId,
                StringComparison.Ordinal);
    }
}
