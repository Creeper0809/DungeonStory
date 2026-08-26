using System;
using System.Collections.Generic;

/// <summary>
/// Transient, non-save authority for synchronous facility topology mutations.
/// A freeze never survives save/restore; save commands are not allowed to run
/// concurrently with a topology command on Unity's main thread.
/// </summary>
public sealed class ProductionFacilityMutationEpochRuntime :
    IProductionFacilityMutationEpochAuthority
{
    private readonly Dictionary<BuildingInstanceId, Entry> active = new();
    private long revision;
    private long nextEpoch = 1L;

    public long Revision => revision;

    public bool IsFrozen(BuildingInstanceId facilityId) =>
        facilityId.IsValid && active.ContainsKey(facilityId);

    public bool TryBegin(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        out long epoch,
        out string failureReason)
    {
        epoch = 0L;
        failureReason = string.Empty;
        string owner = ownerOperationId ?? string.Empty;
        if (!facilityId.IsValid
            || owner.Length == 0
            || !string.Equals(owner, owner.Trim(), StringComparison.Ordinal))
        {
            failureReason = "production-facility-mutation-identity-invalid";
            return false;
        }
        if (active.TryGetValue(facilityId, out Entry existing))
        {
            failureReason = string.Equals(
                    existing.OwnerOperationId,
                    owner,
                    StringComparison.Ordinal)
                ? "production-facility-mutation-already-active"
                : "production-facility-mutation-owned-by-other";
            return false;
        }

        epoch = nextEpoch;
        nextEpoch = checked(nextEpoch + 1L);
        active.Add(facilityId, new Entry(owner, epoch));
        revision = checked(revision + 1L);
        return true;
    }

    public bool IsCurrent(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        long epoch) => facilityId.IsValid
        && epoch > 0L
        && active.TryGetValue(facilityId, out Entry entry)
        && entry.Epoch == epoch
        && string.Equals(
            entry.OwnerOperationId,
            ownerOperationId,
            StringComparison.Ordinal);

    public bool TryEnd(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        long epoch,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsCurrent(facilityId, ownerOperationId, epoch))
        {
            failureReason = "production-facility-mutation-epoch-stale";
            return false;
        }
        active.Remove(facilityId);
        revision = checked(revision + 1L);
        return true;
    }

    private readonly struct Entry
    {
        internal Entry(string ownerOperationId, long epoch)
        {
            OwnerOperationId = ownerOperationId;
            Epoch = epoch;
        }

        internal string OwnerOperationId { get; }
        internal long Epoch { get; }
    }
}
