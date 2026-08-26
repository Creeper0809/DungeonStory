using System;
using System.Collections.Generic;
using System.Linq;

public interface IProductionOutputDestinationAuthorityRuntime
{
    bool TryEnsure(
        ProductionFacilityHandle facility,
        long minimumMassCapacityGrams,
        out FacilityBufferCapacityProfile profile,
        out string failureReason);

    bool TryValidate(
        ProductionFacilityHandle facility,
        out FacilityBufferCapacityProfile profile,
        out string failureReason);

    bool TryReplaceProjected(
        IReadOnlyList<ProductionFacilityHandle> facilities,
        IReadOnlyDictionary<string, long> capacityGramsByFacilityId,
        out string failureReason);

    bool TryRevoke(
        BuildingInstanceId facilityId,
        out string failureReason);
}

/// <summary>
/// Owns the single facility-scoped claim/profile pair used by every generic
/// production bill sharing one physical output buffer. Capacity never shrinks
/// while the authority is live; terminal revocation is a separate operation.
/// </summary>
public sealed class ProductionOutputDestinationAuthorityRuntime :
    IProductionOutputDestinationAuthorityRuntime
{
    public const string OwnerDomain = "economy.production-output";
    public const long CapacitySchemaRevision = 2L;

    private readonly IFacilityBufferDestinationClaimQuery claimQuery;
    private readonly IFacilityBufferMassCapacityQuery capacityQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claimAuthority;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacityAuthority;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;

    public ProductionOutputDestinationAuthorityRuntime(
        IFacilityBufferDestinationClaimQuery claimQuery,
        IFacilityBufferMassCapacityQuery capacityQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claimAuthority,
        IFacilityBufferMassCapacityAuthorityQuery capacityAuthority,
        IFacilityBufferDestinationLifecycleCommand lifecycle)
    {
        this.claimQuery = claimQuery
            ?? throw new ArgumentNullException(nameof(claimQuery));
        this.capacityQuery = capacityQuery
            ?? throw new ArgumentNullException(nameof(capacityQuery));
        this.claimAuthority = claimAuthority
            ?? throw new ArgumentNullException(nameof(claimAuthority));
        this.capacityAuthority = capacityAuthority
            ?? throw new ArgumentNullException(nameof(capacityAuthority));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    public bool TryEnsure(
        ProductionFacilityHandle facility,
        long minimumMassCapacityGrams,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        profile = null;
        failureReason = string.Empty;
        if (!TryCreateClaim(
                facility,
                out FacilityBufferDestinationClaim desiredClaim,
                out failureReason)
            || minimumMassCapacityGrams <= 0L)
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-output-capacity-invalid"
                : failureReason;
            return false;
        }

        List<FacilityBufferDestinationClaim> claims = CaptureOwnedClaims();
        List<FacilityBufferCapacityProfile> profiles = CaptureOwnedProfiles();
        int claimIndex = claims.FindIndex(value => string.Equals(
            value.DestinationId,
            desiredClaim.DestinationId,
            StringComparison.Ordinal));
        int profileIndex = profiles.FindIndex(value => string.Equals(
            value.DestinationId,
            desiredClaim.DestinationId,
            StringComparison.Ordinal));
        if ((claimIndex < 0) != (profileIndex < 0))
        {
            failureReason =
                $"production-output-authority-partial:{desiredClaim.DestinationId}";
            return false;
        }

        long desiredCapacity = minimumMassCapacityGrams;
        if (profileIndex >= 0)
        {
            FacilityBufferCapacityProfile existing = profiles[profileIndex];
            if (!PairMatches(facility, claims[claimIndex], existing))
            {
                failureReason =
                    $"production-output-authority-conflict:{desiredClaim.DestinationId}";
                return false;
            }
            desiredCapacity = Math.Max(
                existing.MaxMassGrams,
                minimumMassCapacityGrams);
            if (existing.MaxMassGrams == desiredCapacity)
            {
                profile = existing;
                return true;
            }
            claims[claimIndex] = desiredClaim;
            profiles[profileIndex] = CreateProfile(
                desiredClaim,
                desiredCapacity);
        }
        else
        {
            claims.Add(desiredClaim);
            profiles.Add(CreateProfile(desiredClaim, desiredCapacity));
        }

        SortAuthorities(claims, profiles);
        if (!lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                claims,
                profiles,
                out failureReason))
        {
            failureReason =
                $"production-output-authority-publish-failed:{failureReason}";
            return false;
        }

        return TryValidate(facility, out profile, out failureReason)
            && profile.MaxMassGrams >= minimumMassCapacityGrams;
    }

    public bool TryValidate(
        ProductionFacilityHandle facility,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        profile = null;
        failureReason = string.Empty;
        if (!TryCreateClaim(
                facility,
                out FacilityBufferDestinationClaim expected,
                out failureReason))
        {
            return false;
        }

        FacilityBufferDestinationClaim[] claims = claimQuery.CaptureClaims()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                expected.DestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (claims.Length != 1
            || !PairMatches(facility, claims[0], expected))
        {
            failureReason =
                $"production-output-claim-invalid:{expected.DestinationId}:{claims.Length}";
            return false;
        }
        if (!capacityQuery.TryGetCapacity(
                expected.DestinationId,
                expected.DropPosition,
                out FacilityBufferMassCapacitySnapshot capacity)
            || !PairMatches(facility, claims[0], capacity.Profile))
        {
            failureReason =
                $"production-output-capacity-invalid:{expected.DestinationId}";
            return false;
        }
        profile = capacity.Profile;
        return true;
    }

    public bool TryReplaceProjected(
        IReadOnlyList<ProductionFacilityHandle> facilities,
        IReadOnlyDictionary<string, long> capacityGramsByFacilityId,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionFacilityHandle[] exactFacilities = (facilities
                ?? throw new ArgumentNullException(nameof(facilities)))
            .Where(value => value != null && !value.IsDestroyed)
            .OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyDictionary<string, long> exactCapacities =
            capacityGramsByFacilityId
            ?? throw new ArgumentNullException(nameof(capacityGramsByFacilityId));
        if (exactFacilities.Select(value => value.InstanceId.Value)
                .Distinct(StringComparer.Ordinal).Count() != exactFacilities.Length
            || exactFacilities.Length != exactCapacities.Count)
        {
            failureReason = "production-output-projected-authority-set-invalid";
            return false;
        }

        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        foreach (ProductionFacilityHandle facility in exactFacilities)
        {
            if (!exactCapacities.TryGetValue(
                    facility.InstanceId.Value,
                    out long capacityGrams)
                || capacityGrams <= 0L
                || !TryCreateClaim(
                    facility,
                    out FacilityBufferDestinationClaim claim,
                    out failureReason))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? $"production-output-projected-capacity-invalid:{facility.InstanceId.Value}"
                    : failureReason;
                return false;
            }
            claims.Add(claim);
            profiles.Add(CreateProfile(claim, capacityGrams));
        }

        SortAuthorities(claims, profiles);
        if (lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                claims,
                profiles,
                out failureReason))
        {
            return true;
        }
        failureReason =
            $"production-output-projected-authority-publish-failed:{failureReason}";
        return false;
    }

    public bool TryRevoke(
        BuildingInstanceId facilityId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!facilityId.IsValid)
        {
            failureReason = "production-output-revoke-facility-invalid";
            return false;
        }
        string destinationId = ProductionBillRuntime.OutputDestinationPrefix
            + facilityId.Value;
        List<FacilityBufferDestinationClaim> claims = CaptureOwnedClaims()
            .Where(value => !string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .ToList();
        List<FacilityBufferCapacityProfile> profiles = CaptureOwnedProfiles()
            .Where(value => !string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .ToList();
        SortAuthorities(claims, profiles);
        if (lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                claims,
                profiles,
                out failureReason))
        {
            return true;
        }
        failureReason = $"production-output-authority-revoke-failed:{failureReason}";
        return false;
    }

    private static bool TryCreateClaim(
        ProductionFacilityHandle facility,
        out FacilityBufferDestinationClaim claim,
        out string failureReason)
    {
        claim = null;
        failureReason = string.Empty;
        if (facility == null
            || facility.IsDestroyed
            || !facility.InstanceId.IsValid)
        {
            failureReason = "production-output-facility-invalid";
            return false;
        }
        string destinationId = ProductionBillRuntime.OutputDestinationPrefix
            + facility.InstanceId.Value;
        claim = new FacilityBufferDestinationClaim(
            destinationId,
            facility.Position,
            OwnerDomain,
            destinationId,
            facility.InstanceId.Value,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        return true;
    }

    private static FacilityBufferCapacityProfile CreateProfile(
        FacilityBufferDestinationClaim claim,
        long capacityGrams) => new(
        claim.DestinationId,
        claim.DropPosition,
        claim.OwnerDomain,
        claim.OwnerOperationId,
        claim.OwnerFacilityId,
        new PhysicalMassGrams(capacityGrams),
        CapacitySchemaRevision);

    private static bool PairMatches(
        ProductionFacilityHandle facility,
        FacilityBufferDestinationClaim claim,
        FacilityBufferDestinationClaim expected) =>
        facility != null
        && claim != null
        && expected != null
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.DropPosition == facility.Position
        && string.Equals(claim.DestinationId, expected.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerDomain, OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, expected.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, facility.InstanceId.Value,
            StringComparison.Ordinal);

    private static bool PairMatches(
        ProductionFacilityHandle facility,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) =>
        facility != null
        && claim != null
        && profile != null
        && profile.MaxMassGrams > 0L
        && profile.CapacityRevision == CapacitySchemaRevision
        && claim.DropPosition == profile.DropPosition
        && claim.DropPosition == facility.Position
        && string.Equals(claim.DestinationId, profile.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerDomain, OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerDomain, OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, profile.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, facility.InstanceId.Value,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, facility.InstanceId.Value,
            StringComparison.Ordinal);

    private List<FacilityBufferDestinationClaim> CaptureOwnedClaims() =>
        claimAuthority.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();

    private List<FacilityBufferCapacityProfile> CaptureOwnedProfiles() =>
        capacityAuthority.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();

    private static void SortAuthorities(
        List<FacilityBufferDestinationClaim> claims,
        List<FacilityBufferCapacityProfile> profiles)
    {
        claims.Sort((left, right) => StringComparer.Ordinal.Compare(
            left.DestinationId,
            right.DestinationId));
        profiles.Sort((left, right) => StringComparer.Ordinal.Compare(
            left.DestinationId,
            right.DestinationId));
    }
}
