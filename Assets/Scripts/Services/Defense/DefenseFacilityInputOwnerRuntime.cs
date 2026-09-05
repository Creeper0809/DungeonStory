using System;
using System.Collections.Generic;
using System.Linq;

public interface IDefenseFacilityInputOwnerRuntime
{
    bool TryEnsureAuthority(
        DefenseFacility facility,
        out string failureReason);

    bool TryReconcileLive(out string failureReason);

    bool TryReconcileRestore(
        IReadOnlyCollection<DefenseFacilityState> restoredStates,
        out string failureReason);
}

/// <summary>
/// Owns the paired exact destination claim and positive gram profile for every
/// live defense supply/maintenance buffer. Terminal reconciliation drains the
/// physical destination before retiring either authority; restore publication
/// rebuilds the derived pair in the claim/profile candidates.
/// </summary>
public sealed class DefenseFacilityInputOwnerRuntime :
    IDefenseFacilityInputOwnerRuntime
{
    private readonly IDefenseFacilityInputOwnerSource source;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public DefenseFacilityInputOwnerRuntime(
        IDefenseFacilityInputOwnerSource source,
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryEnsureAuthority(
        DefenseFacility facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (facility == null || facility.isDestroy || facility.IsGridDestroyed)
        {
            failureReason = "defense-input-owner-facility-not-live";
            return false;
        }

        string facilityId;
        try
        {
            facilityId = facility.RequirePersistentInstanceId().Value;
        }
        catch (Exception exception) when (exception is InvalidOperationException)
        {
            failureReason = "defense-input-owner-facility-id-invalid:"
                + exception.Message;
            return false;
        }

        IReadOnlyList<DefenseFacilityInputOwnerDescriptor> descriptors;
        try
        {
            descriptors = source.Capture();
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "defense-input-owner-source-invalid:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        if (descriptors.Count(value => value != null
                && string.Equals(
                    value.FacilityPersistentId,
                    facilityId,
                    StringComparison.Ordinal)) != 1)
        {
            failureReason = "defense-input-owner-facility-source-cardinality:"
                + facilityId;
            return false;
        }

        return TryReconcile(
            descriptors,
            releaseRetiredDestinations: true,
            out failureReason);
    }

    public bool TryReconcileLive(out string failureReason)
    {
        try
        {
            return TryReconcile(
                source.Capture(),
                releaseRetiredDestinations: true,
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "defense-input-owner-live-reconcile-invalid:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    public bool TryReconcileRestore(
        IReadOnlyCollection<DefenseFacilityState> restoredStates,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            IReadOnlyList<DefenseFacilityInputOwnerDescriptor> descriptors =
                source.Capture();
            if (!TryValidateStateJoin(
                    restoredStates,
                    descriptors,
                    out failureReason))
            {
                return false;
            }
            return TryReconcile(
                descriptors,
                releaseRetiredDestinations: false,
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "defense-input-owner-restore-reconcile-invalid:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private bool TryReconcile(
        IEnumerable<DefenseFacilityInputOwnerDescriptor> descriptors,
        bool releaseRetiredDestinations,
        out string failureReason)
    {
        failureReason = string.Empty;
        DefenseFacilityInputOwnerProjection desired =
            DefenseFacilityInputOwnerAuthority.BuildProjection(
                descriptors,
                massQuery);
        if (!TryCaptureOwnedPairs(
                out FacilityBufferDestinationClaim[] existingClaims,
                out FacilityBufferCapacityProfile[] existingProfiles,
                out failureReason))
        {
            return false;
        }
        if (SetsMatch(
                existingClaims,
                existingProfiles,
                desired.Claims,
                desired.Profiles))
        {
            return true;
        }

        if (releaseRetiredDestinations
            && !TryReleaseRetiredDestinations(
                existingClaims,
                existingProfiles,
                desired.Claims,
                desired.Profiles,
                out failureReason))
        {
            return false;
        }

        if (lifecycle.TryReplaceOwnedAuthorities(
                DefenseFacilityInputOwnerAuthority.OwnerDomain,
                desired.Claims,
                desired.Profiles,
                out failureReason))
        {
            return true;
        }
        failureReason = "defense-input-owner-authority-publish-failed:"
            + failureReason;
        return false;
    }

    private bool TryCaptureOwnedPairs(
        out FacilityBufferDestinationClaim[] ownedClaims,
        out FacilityBufferCapacityProfile[] ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                DefenseFacilityInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                DefenseFacilityInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (ownedClaims.Length != ownedProfiles.Length
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason = "defense-input-owner-authority-pair-set-torn";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private bool TryReleaseRetiredDestinations(
        IReadOnlyList<FacilityBufferDestinationClaim> existingClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> existingProfiles,
        IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, FacilityBufferDestinationClaim> desiredClaimById =
            desiredClaims.ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        Dictionary<string, FacilityBufferCapacityProfile> desiredProfileById =
            desiredProfiles.ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        for (int index = 0; index < existingClaims.Count; index++)
        {
            FacilityBufferDestinationClaim claim = existingClaims[index];
            FacilityBufferCapacityProfile profile = existingProfiles[index];
            bool retained = desiredClaimById.TryGetValue(
                    claim.DestinationId,
                    out FacilityBufferDestinationClaim desiredClaim)
                && desiredProfileById.TryGetValue(
                    claim.DestinationId,
                    out FacilityBufferCapacityProfile desiredProfile)
                && DefenseFacilityInputOwnerAuthority.ClaimsMatch(
                    claim,
                    desiredClaim)
                && (DefenseFacilityInputOwnerAuthority.ProfilesMatch(
                        profile,
                        desiredProfile)
                    || CanRetainForCapacityExpansion(profile, desiredProfile));
            if (retained)
                continue;

            if (!releases.TryReleaseAtOwnerPosition(
                    claim.DestinationId,
                    claim.DropPosition,
                    "defense-input-owner-authority-retired",
                    out _,
                    out string releaseFailure))
            {
                failureReason = "defense-input-owner-terminal-release-failed:"
                    + claim.DestinationId + ":" + releaseFailure;
                return false;
            }
        }
        return true;
    }

    private static bool CanRetainForCapacityExpansion(
        FacilityBufferCapacityProfile existing,
        FacilityBufferCapacityProfile desired) =>
        existing != null
        && desired != null
        && existing.DropPosition == desired.DropPosition
        && desired.MaxMassGrams >= existing.MaxMassGrams
        && existing.CapacityRevision == desired.CapacityRevision
        && string.Equals(existing.DestinationId, desired.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(existing.OwnerDomain, desired.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(existing.OwnerOperationId, desired.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(existing.OwnerFacilityId, desired.OwnerFacilityId,
            StringComparison.Ordinal);

    private static bool TryValidateStateJoin(
        IReadOnlyCollection<DefenseFacilityState> states,
        IReadOnlyList<DefenseFacilityInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, DefenseFacilityInputOwnerDescriptor> byId =
            descriptors.ToDictionary(
                value => value.FacilityPersistentId,
                StringComparer.Ordinal);
        foreach (DefenseFacilityState state in
                 states ?? Array.Empty<DefenseFacilityState>())
        {
            if (state == null)
                continue;
            if (!byId.TryGetValue(
                    state.facilityPersistentId ?? string.Empty,
                    out DefenseFacilityInputOwnerDescriptor descriptor))
            {
                bool pending = state.pendingMaintenance?.phase
                        != DefenseFacilityPhysicalCommitPhase.None
                    || state.pendingSupply?.phase
                        != DefenseFacilityPhysicalCommitPhase.None;
                if (pending)
                {
                    failureReason =
                        "defense-input-owner-pending-state-facility-missing:"
                        + state.facilityPersistentId;
                    return false;
                }
                continue;
            }
            if (state.buildingId != descriptor.BuildingId
                || state.gridX != descriptor.Position.x
                || state.gridY != descriptor.Position.y)
            {
                failureReason = "defense-input-owner-state-facility-mismatch:"
                    + state.facilityPersistentId;
                return false;
            }
        }
        return true;
    }

    private static bool SetsMatch(
        IReadOnlyList<FacilityBufferDestinationClaim> leftClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> leftProfiles,
        IReadOnlyList<FacilityBufferDestinationClaim> rightClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> rightProfiles) =>
        leftClaims.Count == rightClaims.Count
        && leftProfiles.Count == rightProfiles.Count
        && leftClaims.Zip(
                rightClaims,
                DefenseFacilityInputOwnerAuthority.ClaimsMatch)
            .All(value => value)
        && leftProfiles.Zip(
                rightProfiles,
                DefenseFacilityInputOwnerAuthority.ProfilesMatch)
            .All(value => value);

    private static bool IsProjectionException(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or OverflowException;
}
