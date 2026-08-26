using System;
using System.Collections.Generic;
using System.Linq;

public interface IFacilityBufferDestinationLifecycleCommand
{
    bool TryReplaceOwnedAuthorities(
        string ownerDomain,
        IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
        out string failureReason);
}

/// <summary>
/// Publishes destination ownership and positive mass limits as one reversible
/// owner-scoped transaction. It does not own either authority; it only prevents
/// a claim/profile tear between their validated replacement maps.
/// </summary>
public sealed class FacilityBufferDestinationLifecycleService :
    IFacilityBufferDestinationLifecycleCommand
{
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claimQuery;
    private readonly IFacilityBufferDestinationClaimCommand claimCommand;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacityQuery;
    private readonly IFacilityBufferMassCapacityCommand capacityCommand;

    public FacilityBufferDestinationLifecycleService(
        IFacilityBufferDestinationClaimAuthorityQuery claimQuery,
        IFacilityBufferDestinationClaimCommand claimCommand,
        IFacilityBufferMassCapacityAuthorityQuery capacityQuery,
        IFacilityBufferMassCapacityCommand capacityCommand)
    {
        this.claimQuery = claimQuery ?? throw new ArgumentNullException(nameof(claimQuery));
        this.claimCommand = claimCommand ?? throw new ArgumentNullException(nameof(claimCommand));
        this.capacityQuery = capacityQuery ?? throw new ArgumentNullException(nameof(capacityQuery));
        this.capacityCommand = capacityCommand ?? throw new ArgumentNullException(nameof(capacityCommand));
    }

    public bool TryReplaceOwnedAuthorities(
        string ownerDomain,
        IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
        out string failureReason)
    {
        failureReason = string.Empty;
        FacilityBufferDestinationClaim[] claims =
            (desiredClaims ?? Array.Empty<FacilityBufferDestinationClaim>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferCapacityProfile[] profiles =
            (desiredProfiles ?? Array.Empty<FacilityBufferCapacityProfile>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (claims.Length != profiles.Length
            || claims.Any(value => value == null)
            || profiles.Any(value => value == null))
        {
            failureReason = "facility-buffer-authority-pair-count-invalid";
            return false;
        }
        for (int index = 0; index < claims.Length; index++)
        {
            FacilityBufferDestinationClaim claim = claims[index];
            FacilityBufferCapacityProfile profile = profiles[index];
            if (!string.Equals(claim.DestinationId, profile.DestinationId, StringComparison.Ordinal)
                || claim.DropPosition != profile.DropPosition
                || !string.Equals(claim.OwnerDomain, ownerDomain, StringComparison.Ordinal)
                || !string.Equals(profile.OwnerDomain, ownerDomain, StringComparison.Ordinal)
                || !string.Equals(claim.OwnerOperationId, profile.OwnerOperationId, StringComparison.Ordinal)
                || !string.Equals(claim.OwnerFacilityId, profile.OwnerFacilityId, StringComparison.Ordinal))
            {
                failureReason =
                    "facility-buffer-authority-pair-mismatch:" + claim.DestinationId;
                return false;
            }
        }

        FacilityBufferDestinationClaim[] previousClaims = claimQuery
            .CaptureAuthorityClaims()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] previousProfiles = capacityQuery
            .CaptureAuthorityProfiles()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal))
            .ToArray();
        if (!claimCommand.TryReplaceOwnedClaims(
                ownerDomain,
                claims,
                out FacilityBufferDestinationClaimFailureCode claimFailure,
                out string claimReason))
        {
            failureReason = $"claim:{claimFailure}:{claimReason}";
            return false;
        }
        FacilityBufferMassAdmissionFailureCode capacityFailure;
        string capacityReason;
        try
        {
            if (capacityCommand.TryReplaceOwnedProfiles(
                    ownerDomain,
                    profiles,
                    out capacityFailure,
                    out capacityReason))
            {
                return true;
            }
        }
        catch
        {
            RollBackOwnerAuthorities(
                ownerDomain,
                previousClaims,
                previousProfiles);
            throw;
        }

        RollBackOwnerAuthorities(ownerDomain, previousClaims, previousProfiles);
        failureReason = $"capacity:{capacityFailure}:{capacityReason}";
        return false;
    }

    private void RollBackOwnerAuthorities(
        string ownerDomain,
        IReadOnlyList<FacilityBufferDestinationClaim> previousClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> previousProfiles)
    {
        if (!claimCommand.TryReplaceOwnedClaims(
                ownerDomain,
                previousClaims,
                out FacilityBufferDestinationClaimFailureCode rollbackFailure,
                out string rollbackReason))
        {
            throw new InvalidOperationException(
                $"Facility-buffer claim rollback failed ({rollbackFailure}): {rollbackReason}");
        }
        if (!capacityCommand.TryReplaceOwnedProfiles(
                ownerDomain,
                previousProfiles,
                out FacilityBufferMassAdmissionFailureCode profileRollbackFailure,
                out string profileRollbackReason))
        {
            throw new InvalidOperationException(
                $"Facility-buffer capacity rollback failed ({profileRollbackFailure}): {profileRollbackReason}");
        }
    }
}
