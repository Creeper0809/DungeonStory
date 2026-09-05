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

public interface IProductionOutputDestinationCapacitySourceAuthority
{
    bool TryEnsureCapacitySource(
        ProductionFacilityHandle facility,
        ProductionOutputBufferCapacitySourceSnapshot capacitySource,
        out FacilityBufferCapacityProfile profile,
        out string failureReason);

    bool TryReplaceProjectedCapacitySources(
        IReadOnlyList<ProductionFacilityHandle> facilities,
        IReadOnlyDictionary<string, ProductionOutputBufferCapacitySourceSnapshot>
            sourcesByFacilityId,
        out string failureReason);
}

public static class ProductionOutputDestinationCapacitySourceExtensions
{
    public static bool TryEnsureCapacitySource(
        this IProductionOutputDestinationAuthorityRuntime authority,
        ProductionFacilityHandle facility,
        ProductionOutputBufferCapacitySourceSnapshot capacitySource,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        if (authority is IProductionOutputDestinationCapacitySourceAuthority exact)
        {
            return exact.TryEnsureCapacitySource(
                facility,
                capacitySource,
                out profile,
                out failureReason);
        }
        return authority.TryEnsure(
            facility,
            capacitySource.RequiredMinimumCapacityGrams,
            out profile,
            out failureReason);
    }

    public static bool TryReplaceProjectedCapacitySources(
        this IProductionOutputDestinationAuthorityRuntime authority,
        IReadOnlyList<ProductionFacilityHandle> facilities,
        IReadOnlyDictionary<string, ProductionOutputBufferCapacitySourceSnapshot>
            sourcesByFacilityId,
        out string failureReason)
    {
        if (authority is IProductionOutputDestinationCapacitySourceAuthority exact)
        {
            return exact.TryReplaceProjectedCapacitySources(
                facilities,
                sourcesByFacilityId,
                out failureReason);
        }
        Dictionary<string, long> legacy = sourcesByFacilityId
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.RequiredMinimumCapacityGrams,
                StringComparer.Ordinal);
        return authority.TryReplaceProjected(
            facilities,
            legacy,
            out failureReason);
    }
}

/// <summary>
/// Owns the single facility-scoped claim/profile pair used by every generic
/// production bill sharing one physical output buffer. Capacity never shrinks
/// while the authority is live; terminal revocation is a separate operation.
/// </summary>
public sealed class ProductionOutputDestinationAuthorityRuntime :
    IProductionOutputDestinationAuthorityRuntime,
    IProductionOutputDestinationCapacitySourceAuthority
{
    public const string OwnerDomain = "economy.production-output";
    public const long CapacitySchemaRevision = 3L;

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
        out string failureReason) => TryEnsureCore(
        facility,
        minimumMassCapacityGrams,
        string.Empty,
        out profile,
        out failureReason);

    private bool TryEnsureCore(
        ProductionFacilityHandle facility,
        long minimumMassCapacityGrams,
        string authorityDigest,
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
            if (existing.MaxMassGrams == desiredCapacity
                && string.Equals(
                    existing.AuthorityDigest,
                    authorityDigest,
                    StringComparison.Ordinal))
            {
                profile = existing;
                return true;
            }
            claims[claimIndex] = desiredClaim;
            profiles[profileIndex] = CreateProfile(
                desiredClaim,
                desiredCapacity,
                authorityDigest);
        }
        else
        {
            claims.Add(desiredClaim);
            profiles.Add(CreateProfile(
                desiredClaim,
                desiredCapacity,
                authorityDigest));
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

    public bool TryEnsureCapacitySource(
        ProductionFacilityHandle facility,
        ProductionOutputBufferCapacitySourceSnapshot capacitySource,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        profile = null;
        if (!IsLowercaseSha256(capacitySource.ClearanceGateDigest))
        {
            failureReason = "production-output-clearance-authority-invalid";
            return false;
        }
        return TryEnsureCore(
            facility,
            capacitySource.RequiredMinimumCapacityGrams,
            capacitySource.ClearanceGateDigest,
            out profile,
            out failureReason);
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

    public bool TryReplaceProjectedCapacitySources(
        IReadOnlyList<ProductionFacilityHandle> facilities,
        IReadOnlyDictionary<string, ProductionOutputBufferCapacitySourceSnapshot>
            sourcesByFacilityId,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionFacilityHandle[] exactFacilities = (facilities
                ?? throw new ArgumentNullException(nameof(facilities)))
            .Where(value => value != null && !value.IsDestroyed)
            .OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyDictionary<string, ProductionOutputBufferCapacitySourceSnapshot>
            exactSources = sourcesByFacilityId
            ?? throw new ArgumentNullException(nameof(sourcesByFacilityId));
        if (exactFacilities.Select(value => value.InstanceId.Value)
                .Distinct(StringComparer.Ordinal).Count() != exactFacilities.Length
            || exactFacilities.Length != exactSources.Count)
        {
            failureReason = "production-output-projected-source-set-invalid";
            return false;
        }

        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        foreach (ProductionFacilityHandle facility in exactFacilities)
        {
            if (!exactSources.TryGetValue(
                    facility.InstanceId.Value,
                    out ProductionOutputBufferCapacitySourceSnapshot source)
                || source.RequiredMinimumCapacityGrams <= 0L
                || !IsLowercaseSha256(source.ClearanceGateDigest)
                || !TryCreateClaim(
                    facility,
                    out FacilityBufferDestinationClaim claim,
                    out failureReason))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "production-output-projected-source-invalid:"
                        + facility.InstanceId.Value
                    : failureReason;
                return false;
            }
            claims.Add(claim);
            profiles.Add(CreateProfile(
                claim,
                source.RequiredMinimumCapacityGrams,
                source.ClearanceGateDigest));
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
            "production-output-projected-source-publish-failed:" + failureReason;
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
        FacilityBufferDestinationClaim[] targetClaims = claimAuthority
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
        if (targetClaims.Length != 1
            || targetProfiles.Length != 1
            || !PairMatches(
                facilityId,
                targetClaims[0],
                targetProfiles[0]))
        {
            failureReason = "production-output-authority-revoke-invalid:"
                + destinationId + ":claim=" + targetClaims.Length
                + ":profile=" + targetProfiles.Length;
            return false;
        }
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
            bool claimRemoved = claimAuthority.CaptureAuthorityClaims()
                .All(value => value == null || !string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
            bool profileRemoved = capacityAuthority.CaptureAuthorityProfiles()
                .All(value => value == null || !string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
            if (claimRemoved && profileRemoved)
                return true;
            failureReason = "production-output-authority-revoke-postcondition:"
                + destinationId;
            return false;
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
        long capacityGrams,
        string authorityDigest = "") => new(
        claim.DestinationId,
        claim.DropPosition,
        claim.OwnerDomain,
        claim.OwnerOperationId,
        claim.OwnerFacilityId,
        new PhysicalMassGrams(capacityGrams),
        CapacitySchemaRevision,
        authorityDigest);

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        foreach (char character in value)
        {
            if (!(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f'))
                return false;
        }
        return true;
    }

    private static bool PairMatches(
        BuildingInstanceId facilityId,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) =>
        facilityId.IsValid
        && claim != null
        && profile != null
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.DropPosition == profile.DropPosition
        && profile.CapacityRevision == CapacitySchemaRevision
        && string.Equals(claim.OwnerDomain, OwnerDomain, StringComparison.Ordinal)
        && string.Equals(profile.OwnerDomain, OwnerDomain, StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerOperationId,
            claim.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            claim.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerFacilityId,
            facilityId.Value,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            facilityId.Value,
            StringComparison.Ordinal)
        && string.Equals(
            profile.DestinationId,
            claim.DestinationId,
            StringComparison.Ordinal);

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
