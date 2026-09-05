using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Deterministic composition root for all FacilityBuffer admission fences.
/// Multiple sources claiming the same subject is an ownership corruption, not
/// a priority decision, and therefore fails loudly.
/// </summary>
public sealed class FacilityBufferDestinationAdmissionFenceQuery :
    IFacilityBufferDestinationAdmissionFenceQuery
{
    private readonly IFacilityBufferDestinationAdmissionFenceSource[] sources;

    public FacilityBufferDestinationAdmissionFenceQuery(
        IEnumerable<IFacilityBufferDestinationAdmissionFenceSource> sources)
    {
        this.sources = (sources
                ?? throw new ArgumentNullException(nameof(sources)))
            .OrderBy(value => value?.SourceId, StringComparer.Ordinal)
            .ToArray();
        if (this.sources.Any(value => value == null
                || !IsCanonicalRequired(value.SourceId))
            || this.sources
                .Select(value => value.SourceId)
                .Distinct(StringComparer.Ordinal).Count() != this.sources.Length)
        {
            throw new InvalidOperationException(
                "Facility-buffer admission fence sources are null, duplicate, or non-canonical.");
        }
    }

    public long Revision
    {
        get
        {
            long revision = 17L;
            foreach (IFacilityBufferDestinationAdmissionFenceSource source in sources)
            {
                revision = unchecked(revision * 486187739L + source.Revision);
            }
            return revision;
        }
    }

    public bool TryCaptureOpenFence(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        out FacilityBufferDestinationAdmissionFenceSnapshot snapshot)
    {
        snapshot = default;
        if (!subject.IsCanonical)
        {
            throw new ArgumentException(
                "Facility-buffer admission fence subject is invalid.",
                nameof(subject));
        }

        bool found = false;
        foreach (IFacilityBufferDestinationAdmissionFenceSource source in sources)
        {
            if (!source.TryCaptureOpenFence(subject, out var candidate))
                continue;
            if (found)
            {
                throw new InvalidOperationException(
                    "Multiple FacilityBuffer admission fence sources own destination '"
                    + subject.DestinationId + "'.");
            }
            if (!string.Equals(candidate.SourceId, source.SourceId,
                    StringComparison.Ordinal)
                || candidate.Revision <= 0L)
            {
                throw new InvalidOperationException(
                    "FacilityBuffer admission fence source returned an invalid snapshot: "
                    + source.SourceId);
            }
            snapshot = candidate;
            found = true;
        }
        return found;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>
/// Production FacilityBuffer admission source for the single composed
/// mutation authority. Transient topology mutations and durable destructive
/// drains therefore fence new exact-lot and planned-output reservations
/// through one production source identity.
/// </summary>
public sealed class ProductionFacilityMutationAdmissionFenceSource :
    IFacilityBufferDestinationAdmissionFenceSource
{
    public const string StableSourceId = "production.facility-mutation";

    private readonly IProductionFacilityMutationEpochQuery mutations;

    public ProductionFacilityMutationAdmissionFenceSource(
        IProductionFacilityMutationEpochQuery mutations)
    {
        this.mutations = mutations
            ?? throw new ArgumentNullException(nameof(mutations));
    }

    public string SourceId => StableSourceId;
    public long Revision => mutations.Revision;

    public bool TryCaptureOpenFence(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        out FacilityBufferDestinationAdmissionFenceSnapshot snapshot)
    {
        snapshot = default;
        if (!subject.IsCanonical)
        {
            throw new ArgumentException(
                "Production mutation admission fence subject is invalid.",
                nameof(subject));
        }

        BuildingInstanceId facilityId =
            (BuildingInstanceId)subject.OwnerFacilityId;
        if (subject.OwnerFacilityId.Length == 0
            || !facilityId.IsValid
            || !mutations.TryCaptureOpen(
                facilityId,
                out ProductionFacilityMutationFenceSnapshot pending))
        {
            return false;
        }

        snapshot = new FacilityBufferDestinationAdmissionFenceSnapshot(
            SourceId,
            pending.OperationId,
            pending.OperationRevision);
        return true;
    }
}

/// <summary>
/// Compatibility adapter from the existing production facility drain journal
/// into the owner-neutral admission fence contract.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainAdmissionFenceSource :
    IFacilityBufferDestinationAdmissionFenceSource
{
    public const string StableSourceId = "production.facility-destructive-drain";

    private readonly IProductionFacilityDestructiveDrainOpenOperationQuery open;

    public ProductionFacilityDestructiveDrainAdmissionFenceSource(
        IProductionFacilityDestructiveDrainOpenOperationQuery open)
    {
        this.open = open ?? throw new ArgumentNullException(nameof(open));
    }

    public string SourceId => StableSourceId;
    public long Revision => open.Revision;

    public bool TryCaptureOpenFence(
        FacilityBufferDestinationAdmissionFenceSubject subject,
        out FacilityBufferDestinationAdmissionFenceSnapshot snapshot)
    {
        snapshot = default;
        if (!subject.IsCanonical)
        {
            throw new ArgumentException(
                "Production admission fence subject is invalid.",
                nameof(subject));
        }

        BuildingInstanceId facilityId =
            (BuildingInstanceId)subject.OwnerFacilityId;
        if (subject.OwnerFacilityId.Length == 0
            || !facilityId.IsValid
            || !open.TryCapture(
                facilityId,
                out ProductionFacilityDestructiveDrainOpenOperationSnapshot pending))
        {
            return false;
        }

        snapshot = new FacilityBufferDestinationAdmissionFenceSnapshot(
            SourceId,
            pending.OperationId.Value,
            pending.Revision);
        return true;
    }
}
