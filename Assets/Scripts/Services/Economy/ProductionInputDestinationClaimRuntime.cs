using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionInputDestinationClaimRuntime :
    IProductionInputDestinationClaimRuntime
{
    public const string OwnerDomain = "economy.production";
    public const long InputBufferCapacitySchemaRevision = 1L;

    private readonly IFacilityBufferDestinationClaimQuery query;
    private readonly IFacilityBufferMassCapacityQuery capacityQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claimAuthority;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacityAuthority;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;

    public ProductionInputDestinationClaimRuntime(
        IFacilityBufferDestinationClaimQuery query,
        IFacilityBufferMassCapacityQuery capacityQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claimAuthority,
        IFacilityBufferMassCapacityAuthorityQuery capacityAuthority,
        IFacilityBufferDestinationLifecycleCommand lifecycle)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.capacityQuery = capacityQuery
            ?? throw new ArgumentNullException(nameof(capacityQuery));
        this.claimAuthority = claimAuthority
            ?? throw new ArgumentNullException(nameof(claimAuthority));
        this.capacityAuthority = capacityAuthority
            ?? throw new ArgumentNullException(nameof(capacityAuthority));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    public bool TryValidateClaim(
        ProductionBillRecord record,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryFindOwnedPair(record, out _, out _, out failureReason))
            return false;
        return true;
    }

    public bool TryClaim(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        long maxInputBufferMassGrams,
        out string failureReason)
    {
        if (!TryCreatePair(
                record,
                facility,
                maxInputBufferMassGrams,
                out FacilityBufferDestinationClaim claim,
                out FacilityBufferCapacityProfile profile,
                out failureReason))
        {
            return false;
        }

        List<FacilityBufferDestinationClaim> claims = CaptureOwnedClaims();
        List<FacilityBufferCapacityProfile> profiles = CaptureOwnedProfiles();
        if (claims.Any(value => string.Equals(
                value.DestinationId,
                claim.DestinationId,
                StringComparison.Ordinal))
            || profiles.Any(value => string.Equals(
                value.DestinationId,
                profile.DestinationId,
                StringComparison.Ordinal)))
        {
            failureReason =
                $"production-input-authority-duplicate:{claim.DestinationId}";
            return false;
        }
        claims.Add(claim);
        profiles.Add(profile);
        if (lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                claims,
                profiles,
                out failureReason))
        {
            return true;
        }

        failureReason = $"production-input-authority-claim-failed:{failureReason}";
        return false;
    }

    public bool TryEnsureCapacity(
        ProductionBillRecord record,
        long minimumInputBufferMassGrams,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (minimumInputBufferMassGrams <= 0L
            || !TryFindAuthorityPair(
                record,
                out FacilityBufferDestinationClaim claim,
                out FacilityBufferCapacityProfile profile,
                out failureReason))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-input-capacity-invalid"
                : failureReason;
            return false;
        }
        if (profile.MaxMassGrams >= minimumInputBufferMassGrams)
            return true;

        List<FacilityBufferDestinationClaim> claims = CaptureOwnedClaims();
        List<FacilityBufferCapacityProfile> profiles = CaptureOwnedProfiles();
        int profileIndex = profiles.FindIndex(value => string.Equals(
            value.DestinationId,
            profile.DestinationId,
            StringComparison.Ordinal));
        if (profileIndex < 0)
        {
            failureReason =
                $"production-input-capacity-profile-missing:{profile.DestinationId}";
            return false;
        }
        profiles[profileIndex] = CreateProfile(
            claim,
            minimumInputBufferMassGrams);
        if (lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                claims,
                profiles,
                out failureReason))
        {
            return true;
        }

        failureReason = $"production-input-capacity-expand-failed:{failureReason}";
        return false;
    }

    public bool TryRevoke(
        ProductionBillRecord record,
        out string failureReason)
    {
        if (!TryFindAuthorityPair(
                record,
                out FacilityBufferDestinationClaim claim,
                out _,
                out failureReason))
            return false;

        FacilityBufferDestinationClaim[] claims = CaptureOwnedClaims()
            .Where(value => !string.Equals(
                value.DestinationId,
                claim.DestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] profiles = CaptureOwnedProfiles()
            .Where(value => !string.Equals(
                value.DestinationId,
                claim.DestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                claims,
                profiles,
                out failureReason))
        {
            return true;
        }

        failureReason = $"production-input-authority-revoke-failed:{failureReason}";
        return false;
    }

    public bool TryRevokeIfPresent(
        ProductionBillRecord record,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsCanonicalRecord(record))
        {
            failureReason = "production-input-authority-record-invalid";
            return false;
        }

        FacilityBufferDestinationClaim[] matchingClaims = CaptureOwnedClaims()
            .Where(value => string.Equals(
                value.DestinationId,
                record.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = CaptureOwnedProfiles()
            .Where(value => string.Equals(
                value.DestinationId,
                record.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length == 0 && matchingProfiles.Length == 0)
            return true;
        if (matchingClaims.Length != 1
            || matchingProfiles.Length != 1
            || !ProfileMatches(
                record,
                matchingClaims[0],
                matchingProfiles[0]))
        {
            failureReason =
                $"production-input-authority-pair-invalid:{record.materialDestinationId}:"
                + $"{matchingClaims.Length}:{matchingProfiles.Length}";
            return false;
        }

        FacilityBufferDestinationClaim[] remainingClaims = CaptureOwnedClaims()
            .Where(value => !string.Equals(
                value.DestinationId,
                record.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] remainingProfiles = CaptureOwnedProfiles()
            .Where(value => !string.Equals(
                value.DestinationId,
                record.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                remainingClaims,
                remainingProfiles,
                out failureReason))
        {
            return true;
        }

        failureReason =
            $"production-input-authority-revoke-failed:{failureReason}";
        return false;
    }

    public bool TryReplace(
        IReadOnlyList<ProductionBillRecord> records,
        IReadOnlyList<ProductionFacilityHandle> facilities,
        IReadOnlyDictionary<string, long> inputBufferMassGramsByBillId,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, ProductionFacilityHandle> byId = new(
            StringComparer.Ordinal);
        foreach (ProductionFacilityHandle facility in
                 facilities ?? Array.Empty<ProductionFacilityHandle>())
        {
            if (facility == null
                || !facility.InstanceId.IsValid
                || !byId.TryAdd(facility.InstanceId.Value, facility))
            {
                failureReason =
                    "production-input-claim-restore-facility-invalid-or-duplicate";
                return false;
            }
        }

        ProductionBillRecord[] orderedRecords =
            (records ?? Array.Empty<ProductionBillRecord>())
            .OrderBy(value => value?.billId.Value, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyDictionary<string, long> capacities =
            inputBufferMassGramsByBillId
            ?? new Dictionary<string, long>(StringComparer.Ordinal);
        if (capacities.Count != orderedRecords.Length)
        {
            failureReason = "production-input-capacity-restore-count-invalid";
            return false;
        }

        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (ProductionBillRecord record in orderedRecords)
        {
            if (record == null
                || !record.buildingInstanceId.IsValid
                || !byId.TryGetValue(
                    record.buildingInstanceId.Value,
                    out ProductionFacilityHandle facility)
                || !capacities.TryGetValue(
                    record.billId.Value,
                    out long maxInputBufferMassGrams)
                || !TryCreatePair(
                    record,
                    facility,
                    maxInputBufferMassGrams,
                    out FacilityBufferDestinationClaim claim,
                    out FacilityBufferCapacityProfile profile,
                    out failureReason))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? $"production-input-claim-restore-facility-missing:{record?.billId.Value ?? "null"}"
                    : failureReason;
                return false;
            }
            desiredClaims.Add(claim);
            desiredProfiles.Add(profile);
        }

        if (lifecycle.TryReplaceOwnedAuthorities(
                OwnerDomain,
                desiredClaims,
                desiredProfiles,
                out failureReason))
        {
            return true;
        }

        failureReason = $"production-input-authority-restore-failed:{failureReason}";
        return false;
    }

    private bool TryFindOwnedPair(
        ProductionBillRecord record,
        out FacilityBufferDestinationClaim claim,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        claim = null;
        profile = null;
        failureReason = string.Empty;
        if (!IsCanonicalRecord(record))
        {
            failureReason = "production-input-claim-record-invalid";
            return false;
        }

        FacilityBufferDestinationClaim[] matches = query.CaptureClaims()
            .Where(value => value != null
                && string.Equals(
                    value.DestinationId,
                    record.materialDestinationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            failureReason =
                $"production-input-claim-count-invalid:{record.materialDestinationId}:{matches.Length}";
            return false;
        }

        claim = matches[0];
        if (!string.Equals(claim.OwnerDomain, OwnerDomain, StringComparison.Ordinal)
            || !string.Equals(
                claim.OwnerOperationId,
                record.billId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.OwnerFacilityId,
                record.buildingInstanceId.Value,
                StringComparison.Ordinal)
            || claim.AnchorKind != FacilityBufferDestinationAnchorKind.LiveFacility)
        {
            failureReason =
                $"production-input-claim-owner-mismatch:{record.materialDestinationId}";
            claim = null;
            return false;
        }
        if (!capacityQuery.TryGetCapacity(
                record.materialDestinationId,
                claim.DropPosition,
                out FacilityBufferMassCapacitySnapshot capacity)
            || !ProfileMatches(record, claim, capacity.Profile))
        {
            failureReason =
                $"production-input-capacity-owner-mismatch:{record.materialDestinationId}";
            claim = null;
            return false;
        }
        profile = capacity.Profile;
        return true;
    }

    private bool TryFindAuthorityPair(
        ProductionBillRecord record,
        out FacilityBufferDestinationClaim claim,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        claim = null;
        profile = null;
        failureReason = string.Empty;
        if (!IsCanonicalRecord(record))
        {
            failureReason = "production-input-authority-record-invalid";
            return false;
        }

        FacilityBufferDestinationClaim[] claims = CaptureOwnedClaims()
            .Where(value => string.Equals(
                value.DestinationId,
                record.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] profiles = CaptureOwnedProfiles()
            .Where(value => string.Equals(
                value.DestinationId,
                record.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (claims.Length != 1
            || profiles.Length != 1
            || !ProfileMatches(record, claims[0], profiles[0]))
        {
            failureReason =
                $"production-input-authority-pair-invalid:{record.materialDestinationId}:"
                + $"{claims.Length}:{profiles.Length}";
            return false;
        }
        claim = claims[0];
        profile = profiles[0];
        return true;
    }

    private static bool TryCreatePair(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        long maxInputBufferMassGrams,
        out FacilityBufferDestinationClaim claim,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        claim = null;
        profile = null;
        failureReason = string.Empty;
        if (!IsCanonicalRecord(record)
            || maxInputBufferMassGrams <= 0L
            || facility == null
            || facility.IsDestroyed
            || !facility.InstanceId.IsValid
            || !facility.InstanceId.Equals(record.buildingInstanceId))
        {
            failureReason = "production-input-claim-authority-invalid";
            return false;
        }

        claim = new FacilityBufferDestinationClaim(
            record.materialDestinationId,
            facility.Position,
            OwnerDomain,
            record.billId.Value,
            facility.InstanceId.Value,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        profile = CreateProfile(claim, maxInputBufferMassGrams);
        return true;
    }

    private static FacilityBufferCapacityProfile CreateProfile(
        FacilityBufferDestinationClaim claim,
        long maxInputBufferMassGrams) => new(
        claim.DestinationId,
        claim.DropPosition,
        claim.OwnerDomain,
        claim.OwnerOperationId,
        claim.OwnerFacilityId,
        new PhysicalMassGrams(maxInputBufferMassGrams),
        InputBufferCapacitySchemaRevision);

    private static bool ProfileMatches(
        ProductionBillRecord record,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) =>
        claim != null
        && profile != null
        && profile.MaxMassGrams > 0L
        && profile.CapacityRevision == InputBufferCapacitySchemaRevision
        && claim.DropPosition == profile.DropPosition
        && string.Equals(claim.DestinationId, profile.DestinationId, StringComparison.Ordinal)
        && string.Equals(claim.OwnerDomain, OwnerDomain, StringComparison.Ordinal)
        && string.Equals(profile.OwnerDomain, OwnerDomain, StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, record.billId.Value, StringComparison.Ordinal)
        && string.Equals(profile.OwnerOperationId, record.billId.Value, StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, record.buildingInstanceId.Value, StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, record.buildingInstanceId.Value, StringComparison.Ordinal);

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

    private static bool IsCanonicalRecord(ProductionBillRecord record) =>
        record != null
        && record.billId.IsValid
        && record.buildingInstanceId.IsValid
        && !string.IsNullOrWhiteSpace(record.materialDestinationId)
        && string.Equals(
            record.materialDestinationId,
            ProductionBillRuntime.DestinationPrefix + record.billId.Value,
            StringComparison.Ordinal);
}
