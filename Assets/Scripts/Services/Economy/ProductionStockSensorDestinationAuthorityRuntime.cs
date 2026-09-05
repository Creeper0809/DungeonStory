using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Projects one exact physical delivery socket for every live production
/// facility that authors a stock-sensor installation item. The profile is a
/// derived runtime authority: one panel's current physical mass, never a save
/// DTO or a duplicated authored number.
/// </summary>
public sealed class ProductionStockSensorDestinationAuthorityRuntime :
    IProductionStockSensorDestinationAuthorityRuntime
{
    public const string OwnerDomain = "economy.production-sensor";
    public const long CapacitySchemaRevision = 1L;

    private readonly IProductionItemGateway items;
    private readonly IFacilityBufferDestinationClaimQuery claims;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claimAuthority;
    private readonly IFacilityBufferMassCapacityQuery capacities;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacityAuthority;
    private readonly IFacilityBufferPhysicalOccupancyQuery occupancy;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;

    public ProductionStockSensorDestinationAuthorityRuntime(
        IProductionItemGateway items,
        IFacilityBufferDestinationClaimQuery claims,
        IFacilityBufferDestinationClaimAuthorityQuery claimAuthority,
        IFacilityBufferMassCapacityQuery capacities,
        IFacilityBufferMassCapacityAuthorityQuery capacityAuthority,
        IFacilityBufferPhysicalOccupancyQuery occupancy,
        IFacilityBufferDestinationLifecycleCommand lifecycle)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.claimAuthority = claimAuthority
            ?? throw new ArgumentNullException(nameof(claimAuthority));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.capacityAuthority = capacityAuthority
            ?? throw new ArgumentNullException(nameof(capacityAuthority));
        this.occupancy = occupancy
            ?? throw new ArgumentNullException(nameof(occupancy));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    public bool TryEnsure(
        ProductionFacilityHandle facility,
        out long capacityMassGrams,
        out string failureReason)
    {
        capacityMassGrams = 0L;
        if (!TryCreatePair(
                facility,
                out FacilityBufferDestinationClaim desiredClaim,
                out FacilityBufferCapacityProfile desiredProfile,
                out failureReason))
        {
            return false;
        }
        capacityMassGrams = desiredProfile.MaxMassGrams;

        List<FacilityBufferDestinationClaim> ownedClaims = CaptureOwnedClaims();
        List<FacilityBufferCapacityProfile> ownedProfiles = CaptureOwnedProfiles();
        int claimIndex = ownedClaims.FindIndex(value => string.Equals(
            value.DestinationId,
            desiredClaim.DestinationId,
            StringComparison.Ordinal));
        int profileIndex = ownedProfiles.FindIndex(value => string.Equals(
            value.DestinationId,
            desiredClaim.DestinationId,
            StringComparison.Ordinal));
        if ((claimIndex < 0) != (profileIndex < 0))
        {
            failureReason = "production-sensor-authority-partial:"
                + desiredClaim.DestinationId;
            return false;
        }
        if (claimIndex >= 0)
        {
            if (!PairMatches(
                    facility,
                    ownedClaims[claimIndex],
                    ownedProfiles[profileIndex],
                    desiredProfile.MaxMassGrams))
            {
                if (!TryRequireReplaceable(
                        ownedClaims[claimIndex],
                        ownedProfiles[profileIndex],
                        out failureReason))
                {
                    return false;
                }
                ownedClaims[claimIndex] = desiredClaim;
                ownedProfiles[profileIndex] = desiredProfile;
            }
            else
            {
                return TryValidate(
                    facility,
                    out capacityMassGrams,
                    out failureReason);
            }
        }
        else
        {
            ownedClaims.Add(desiredClaim);
            ownedProfiles.Add(desiredProfile);
        }

        Sort(ownedClaims, ownedProfiles);
        if (!lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                ownedClaims,
                ownedProfiles,
                out failureReason))
        {
            failureReason = "production-sensor-authority-publish-failed:"
                + failureReason;
            return false;
        }
        return TryValidate(
            facility,
            out capacityMassGrams,
            out failureReason);
    }

    public bool TryValidate(
        ProductionFacilityHandle facility,
        out long capacityMassGrams,
        out string failureReason)
    {
        capacityMassGrams = 0L;
        if (!TryResolveExactCapacity(
                facility,
                out long expectedMassGrams,
                out failureReason))
        {
            return false;
        }
        string destinationId = ProductionStockSensorRuntime.BuildDestinationId(
            facility.InstanceId.Value);
        FacilityBufferDestinationClaim[] matchingClaims = claims.CaptureClaims()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length != 1
            || !capacities.TryGetCapacity(
                destinationId,
                facility.Position,
                out FacilityBufferMassCapacitySnapshot capacity)
            || !PairMatches(
                facility,
                matchingClaims[0],
                capacity.Profile,
                expectedMassGrams))
        {
            failureReason = "production-sensor-authority-invalid:"
                + destinationId + ":claim=" + matchingClaims.Length;
            return false;
        }
        capacityMassGrams = expectedMassGrams;
        failureReason = string.Empty;
        return true;
    }

    public bool TryReplaceProjected(
        IReadOnlyList<ProductionFacilityHandle> facilities,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionFacilityHandle[] capable = (facilities
                ?? throw new ArgumentNullException(nameof(facilities)))
            .Where(value => value != null
                && !value.IsDestroyed
                && !string.IsNullOrEmpty(value.StockSensorInstallationItemId))
            .OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        if (capable.Any(value => !value.InstanceId.IsValid)
            || capable.Select(value => value.InstanceId.Value)
                .Distinct(StringComparer.Ordinal).Count() != capable.Length)
        {
            failureReason = "production-sensor-projected-facility-set-invalid";
            return false;
        }

        List<FacilityBufferDestinationClaim> desiredClaims = new(capable.Length);
        List<FacilityBufferCapacityProfile> desiredProfiles = new(capable.Length);
        Dictionary<string, FacilityBufferDestinationClaim> existingClaims =
            CaptureOwnedClaims().ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        Dictionary<string, FacilityBufferCapacityProfile> existingProfiles =
            CaptureOwnedProfiles().ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        if (existingClaims.Count != existingProfiles.Count
            || existingClaims.Keys.Any(value => !existingProfiles.ContainsKey(value)))
        {
            failureReason = "production-sensor-projected-authority-partial";
            return false;
        }
        foreach (ProductionFacilityHandle facility in capable)
        {
            if (!TryCreatePair(
                    facility,
                    out FacilityBufferDestinationClaim claim,
                    out FacilityBufferCapacityProfile profile,
                    out failureReason))
            {
                return false;
            }
            if (existingClaims.TryGetValue(
                    claim.DestinationId,
                    out FacilityBufferDestinationClaim existingClaim)
                && (!PairMatches(
                        facility,
                        existingClaim,
                        existingProfiles[claim.DestinationId],
                        profile.MaxMassGrams)
                    && !TryRequireReplaceable(
                        existingClaim,
                        existingProfiles[claim.DestinationId],
                        out failureReason)))
            {
                return false;
            }
            desiredClaims.Add(claim);
            desiredProfiles.Add(profile);
        }
        Sort(desiredClaims, desiredProfiles);
        if (lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                desiredClaims,
                desiredProfiles,
                out failureReason))
        {
            return true;
        }
        failureReason = "production-sensor-projection-failed:" + failureReason;
        return false;
    }

    public bool TryRequireEmpty(
        ProductionFacilityHandle facility,
        out string failureReason)
    {
        if (!TryValidate(facility, out _, out failureReason))
            return false;
        string destinationId = ProductionStockSensorRuntime.BuildDestinationId(
            facility.InstanceId.Value);
        FacilityBufferPhysicalOccupancySnapshot physical = occupancy.Capture(
            destinationId);
        if (!capacities.TryGetCapacity(
                destinationId,
                facility.Position,
                out FacilityBufferMassCapacitySnapshot capacity))
        {
            failureReason = "production-sensor-capacity-missing:" + destinationId;
            return false;
        }
        if (physical.TotalMassGrams > 0L || capacity.ReservedMassGrams > 0L)
        {
            failureReason = "production-sensor-destination-not-empty:"
                + destinationId + ":physical=" + physical.TotalMassGrams
                + ":reserved=" + capacity.ReservedMassGrams;
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryRevoke(
        BuildingInstanceId facilityId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!facilityId.IsValid)
        {
            failureReason = "production-sensor-revoke-facility-invalid";
            return false;
        }
        string destinationId = ProductionStockSensorRuntime.BuildDestinationId(
            facilityId.Value);
        FacilityBufferDestinationClaim[] targets = claimAuthority
            .CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] targetProfiles = capacityAuthority
            .CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (targets.Length != 1
            || targetProfiles.Length != 1
            || !PairMatches(
                facilityId,
                targets[0],
                targetProfiles[0]))
        {
            failureReason = "production-sensor-revoke-authority-invalid:"
                + destinationId + ":claim=" + targets.Length
                + ":profile=" + targetProfiles.Length;
            return false;
        }
        FacilityBufferPhysicalOccupancySnapshot physical = occupancy.Capture(
            destinationId);
        if (!capacities.TryGetCapacity(
                destinationId,
                targets[0].DropPosition,
                out FacilityBufferMassCapacitySnapshot capacity)
            || physical.TotalMassGrams > 0L
            || capacity.ReservedMassGrams > 0L)
        {
            failureReason = "production-sensor-revoke-not-empty:"
                + destinationId;
            return false;
        }

        List<FacilityBufferDestinationClaim> remainingClaims =
            CaptureOwnedClaims().Where(value => !string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal)).ToList();
        List<FacilityBufferCapacityProfile> remainingProfiles =
            CaptureOwnedProfiles().Where(value => !string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal)).ToList();
        Sort(remainingClaims, remainingProfiles);
        if (!lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                remainingClaims,
                remainingProfiles,
                out failureReason))
        {
            failureReason = "production-sensor-revoke-failed:" + failureReason;
            return false;
        }
        bool removed = claimAuthority.CaptureAuthorityClaims().All(value =>
                value == null || !string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            && capacityAuthority.CaptureAuthorityProfiles().All(value =>
                value == null || !string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
        if (removed)
            return true;
        failureReason = "production-sensor-revoke-postcondition:" + destinationId;
        return false;
    }

    private bool TryCreatePair(
        ProductionFacilityHandle facility,
        out FacilityBufferDestinationClaim claim,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        claim = null;
        profile = null;
        if (!TryResolveExactCapacity(
                facility,
                out long capacityMassGrams,
                out failureReason))
        {
            return false;
        }
        string destinationId = ProductionStockSensorRuntime.BuildDestinationId(
            facility.InstanceId.Value);
        claim = new FacilityBufferDestinationClaim(
            destinationId,
            facility.Position,
            OwnerDomain,
            destinationId,
            facility.InstanceId.Value,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        profile = new FacilityBufferCapacityProfile(
            destinationId,
            facility.Position,
            OwnerDomain,
            destinationId,
            facility.InstanceId.Value,
            new PhysicalMassGrams(capacityMassGrams),
            CapacitySchemaRevision);
        return true;
    }

    private bool TryResolveExactCapacity(
        ProductionFacilityHandle facility,
        out long capacityMassGrams,
        out string failureReason)
    {
        capacityMassGrams = 0L;
        failureReason = string.Empty;
        if (facility == null
            || facility.IsDestroyed
            || !facility.InstanceId.IsValid
            || string.IsNullOrWhiteSpace(facility.StockSensorInstallationItemId)
            || !string.Equals(
                facility.StockSensorInstallationItemId,
                facility.StockSensorInstallationItemId.Trim(),
                StringComparison.Ordinal))
        {
            failureReason = "production-sensor-facility-capability-invalid";
            return false;
        }
        try
        {
            capacityMassGrams = items.GetDefinitionQuantityMassGrams(
                facility.StockSensorInstallationItemId,
                1);
        }
        catch (Exception exception)
        {
            failureReason = "production-sensor-mass-resolution-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        if (capacityMassGrams > 0L)
            return true;
        failureReason = "production-sensor-mass-nonpositive:"
            + facility.StockSensorInstallationItemId;
        return false;
    }

    private bool TryRequireReplaceable(
        FacilityBufferDestinationClaim existingClaim,
        FacilityBufferCapacityProfile existingProfile,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (existingClaim == null
            || existingProfile == null
            || !PairMatches(
                (BuildingInstanceId)existingClaim.OwnerFacilityId,
                existingClaim,
                existingProfile))
        {
            failureReason = "production-sensor-existing-authority-invalid";
            return false;
        }
        FacilityBufferPhysicalOccupancySnapshot physical = occupancy.Capture(
            existingClaim.DestinationId);
        long reservedMassGrams = capacities.TryGetCapacity(
                existingClaim.DestinationId,
                existingClaim.DropPosition,
                out FacilityBufferMassCapacitySnapshot capacity)
            ? capacity.ReservedMassGrams
            : 0L;
        if (physical.TotalMassGrams == 0L && reservedMassGrams == 0L)
            return true;
        failureReason = "production-sensor-authority-update-not-empty:"
            + existingClaim.DestinationId + ":physical="
            + physical.TotalMassGrams + ":reserved=" + reservedMassGrams;
        return false;
    }

    private static bool PairMatches(
        ProductionFacilityHandle facility,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile,
        long expectedMassGrams) => facility != null
        && PairMatches(facility.InstanceId, claim, profile)
        && claim.DropPosition == facility.Position
        && profile.DropPosition == facility.Position
        && profile.MaxMassGrams == expectedMassGrams;

    private static bool PairMatches(
        BuildingInstanceId facilityId,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) => facilityId.IsValid
        && claim != null
        && profile != null
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.DropPosition == profile.DropPosition
        && profile.CapacityRevision == CapacitySchemaRevision
        && string.Equals(claim.OwnerDomain, OwnerDomain, StringComparison.Ordinal)
        && string.Equals(profile.OwnerDomain, OwnerDomain, StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, claim.DestinationId, StringComparison.Ordinal)
        && string.Equals(profile.OwnerOperationId, claim.OwnerOperationId, StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, facilityId.Value, StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, facilityId.Value, StringComparison.Ordinal)
        && string.Equals(profile.DestinationId, claim.DestinationId, StringComparison.Ordinal);

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

    private static void Sort(
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
