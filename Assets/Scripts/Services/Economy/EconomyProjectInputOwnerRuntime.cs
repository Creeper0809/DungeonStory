using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class EconomyProjectInputOwnerDescriptor
{
    public EconomyProjectInputOwnerDescriptor(
        string ownerDomain,
        string ownerOperationId,
        string destinationId,
        Vector2Int position,
        FacilityBufferDestinationAnchorKind anchorKind,
        string ownerFacilityId,
        IReadOnlyDictionary<string, int> requirements,
        long storedCapacityGrams = 0L,
        long storedMassAuthorityRevision = 0L,
        string storedCapacityFingerprint = "")
    {
        EconomyProjectInputOwnerAuthority.RequireSupportedDomain(ownerDomain);
        OwnerDomain = RequireCanonical(ownerDomain, nameof(ownerDomain));
        OwnerOperationId = RequireCanonical(
            ownerOperationId,
            nameof(ownerOperationId));
        DestinationId = RequireCanonical(destinationId, nameof(destinationId));
        Position = position;
        AnchorKind = anchorKind;
        OwnerFacilityId = ownerFacilityId ?? string.Empty;
        if (anchorKind == FacilityBufferDestinationAnchorKind.LiveFacility)
            OwnerFacilityId = RequireCanonical(
                OwnerFacilityId,
                nameof(ownerFacilityId));
        else if (anchorKind != FacilityBufferDestinationAnchorKind.ReservedTarget
                 || !string.IsNullOrEmpty(OwnerFacilityId))
            throw new ArgumentException(
                "Economy input owner requires LiveFacility or owner-neutral ReservedTarget authority.",
                nameof(anchorKind));
        if (requirements == null
            || requirements.Count == 0
            || requirements.Any(value =>
                !EconomyProjectInputOwnerAuthority.IsCanonical(value.Key)
                || value.Value <= 0))
            throw new ArgumentException(
                "Economy input owner requires a positive exact item vector.",
                nameof(requirements));
        Requirements = requirements
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => value.Value,
                StringComparer.Ordinal);
        StoredCapacityGrams = storedCapacityGrams;
        StoredMassAuthorityRevision = storedMassAuthorityRevision;
        StoredCapacityFingerprint = storedCapacityFingerprint ?? string.Empty;
    }

    public string OwnerDomain { get; }
    public string OwnerOperationId { get; }
    public string DestinationId { get; }
    public Vector2Int Position { get; }
    public FacilityBufferDestinationAnchorKind AnchorKind { get; }
    public string OwnerFacilityId { get; }
    public IReadOnlyDictionary<string, int> Requirements { get; }
    public long StoredCapacityGrams { get; }
    public long StoredMassAuthorityRevision { get; }
    public string StoredCapacityFingerprint { get; }

    public bool HasStoredProjection => StoredCapacityGrams > 0L
        || StoredMassAuthorityRevision > 0L
        || StoredCapacityFingerprint.Length > 0;

    private static string RequireCanonical(string value, string parameterName)
    {
        if (!EconomyProjectInputOwnerAuthority.IsCanonical(value))
            throw new ArgumentException(
                "Economy input ownership requires canonical IDs.",
                parameterName);
        return value;
    }
}

public interface IEconomyProjectInputOwnerRestoreRuntime
{
    bool TryReplaceForRestore(
        string ownerDomain,
        IReadOnlyList<EconomyProjectInputOwnerDescriptor> descriptors,
        out string failureReason);
}

/// <summary>
/// Owns exact positive-gram FacilityBuffer claim/profile pairs for the three
/// economy project families. The domain runtimes retain their existing typed
/// Sink/Transfer/WIP authorities; this class only owns destination admission
/// and carried-aware terminal release.
/// </summary>
public sealed class EconomyProjectInputOwnerRuntime :
    IEconomyProjectInputOwnerPort,
    IEconomyProjectInputOwnerRestoreRuntime
{
    private readonly IPhysicalItemMassQuery mass;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public EconomyProjectInputOwnerRuntime(
        IPhysicalItemMassQuery mass,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryEnsure(
        EconomyProjectInputOwnerDescriptor descriptor,
        out EconomyProjectInputOwnerProjection projection,
        out string failureReason)
    {
        projection = default;
        if (!TryProject(descriptor, out projection, out failureReason)
            || !TryCaptureOwnedPairs(
                descriptor.OwnerDomain,
                out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
            return false;
        if (descriptor.HasStoredProjection
            && !StoredProjectionMatches(descriptor, projection))
        {
            failureReason = "economy-input-owner-stored-projection-drift:"
                + descriptor.DestinationId;
            return false;
        }

        FacilityBufferDestinationClaim desiredClaim = CreateClaim(descriptor);
        FacilityBufferCapacityProfile desiredProfile = CreateProfile(
            descriptor,
            projection.CapacityGrams);
        int index = ownedClaims.FindIndex(value => string.Equals(
            value.DestinationId,
            descriptor.DestinationId,
            StringComparison.Ordinal));
        if (index >= 0)
        {
            if (PairMatches(
                    descriptor,
                    ownedClaims[index],
                    ownedProfiles[index],
                    projection.CapacityGrams))
                return true;

            bool safeExpansion = ClaimMatches(
                    ownedClaims[index],
                    desiredClaim)
                && desiredProfile.MaxMassGrams
                    >= ownedProfiles[index].MaxMassGrams
                && desiredProfile.CapacityRevision
                    == ownedProfiles[index].CapacityRevision;
            if (!safeExpansion
                && !TryRelease(
                    ownedClaims[index],
                    "economy-input-owner-authority-replaced",
                    out failureReason))
                return false;
            ownedClaims[index] = desiredClaim;
            ownedProfiles[index] = desiredProfile;
        }
        else
        {
            ownedClaims.Add(desiredClaim);
            ownedProfiles.Add(desiredProfile);
        }

        return lifecycle.TryReplaceOwnedAuthorities(
            descriptor.OwnerDomain,
            ownedClaims,
            ownedProfiles,
            out failureReason);
    }

    public bool TryEnsure(
        string ownerDomain,
        string ownerOperationId,
        string destinationId,
        Vector2Int position,
        EconomyProjectInputOwnerAnchorKind anchorKind,
        string ownerFacilityId,
        IReadOnlyDictionary<string, int> requirements,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint,
        out EconomyProjectInputOwnerProjection projection,
        out string failureReason) => TryEnsure(
        CreateDescriptor(ownerDomain, ownerOperationId, destinationId, position,
            anchorKind, ownerFacilityId, requirements, storedCapacityGrams,
            storedMassAuthorityRevision, storedCapacityFingerprint),
        out projection,
        out failureReason);

    public bool TryValidate(
        EconomyProjectInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        if (!TryProject(
                descriptor,
                out EconomyProjectInputOwnerProjection projection,
                out failureReason)
            || !descriptor.HasStoredProjection
            || !StoredProjectionMatches(descriptor, projection))
        {
            if (failureReason.Length == 0)
                failureReason = "economy-input-owner-projection-not-frozen";
            return false;
        }
        FacilityBufferDestinationClaim[] matchingClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                descriptor.DestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                descriptor.DestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length != 1
            || matchingProfiles.Length != 1
            || !PairMatches(
                descriptor,
                matchingClaims[0],
                matchingProfiles[0],
                projection.CapacityGrams))
        {
            failureReason = "economy-input-owner-authority-mismatch:"
                + descriptor.DestinationId;
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryValidate(
        string ownerDomain,
        string ownerOperationId,
        string destinationId,
        Vector2Int position,
        EconomyProjectInputOwnerAnchorKind anchorKind,
        string ownerFacilityId,
        IReadOnlyDictionary<string, int> requirements,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint,
        out string failureReason) => TryValidate(
        CreateDescriptor(ownerDomain, ownerOperationId, destinationId, position,
            anchorKind, ownerFacilityId, requirements, storedCapacityGrams,
            storedMassAuthorityRevision, storedCapacityFingerprint),
        out failureReason);

    public bool TryRetireDestination(
        string ownerDomain,
        string destinationId,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!EconomyProjectInputOwnerAuthority.IsSupportedDomain(ownerDomain)
            || !EconomyProjectInputOwnerAuthority.IsCanonical(destinationId)
            || !EconomyProjectInputOwnerAuthority.IsCanonical(reasonCode)
            || !TryCaptureOwnedPairs(
                ownerDomain,
                out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
        {
            if (failureReason.Length == 0)
                failureReason = "economy-input-owner-retire-invalid";
            return false;
        }
        int index = ownedClaims.FindIndex(value => string.Equals(
            value.DestinationId,
            destinationId,
            StringComparison.Ordinal));
        if (index < 0)
            return true;
        if (!TryRelease(ownedClaims[index], reasonCode, out failureReason))
            return false;
        ownedClaims.RemoveAt(index);
        ownedProfiles.RemoveAt(index);
        return lifecycle.TryReplaceOwnedAuthorities(
            ownerDomain,
            ownedClaims,
            ownedProfiles,
            out failureReason);
    }

    public bool TryReplaceForRestore(
        string ownerDomain,
        IReadOnlyList<EconomyProjectInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!EconomyProjectInputOwnerAuthority.IsSupportedDomain(ownerDomain))
        {
            failureReason = "economy-input-owner-restore-domain-invalid";
            return false;
        }
        EconomyProjectInputOwnerDescriptor[] ordered = (descriptors
                ?? Array.Empty<EconomyProjectInputOwnerDescriptor>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null
                || !string.Equals(
                    value.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
            || ordered.Select(value => value.DestinationId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason = "economy-input-owner-restore-set-invalid";
            return false;
        }
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (EconomyProjectInputOwnerDescriptor descriptor in ordered)
        {
            if (!TryProject(
                    descriptor,
                    out EconomyProjectInputOwnerProjection projection,
                    out failureReason)
                || !descriptor.HasStoredProjection
                || !StoredProjectionMatches(descriptor, projection))
            {
                if (failureReason.Length == 0)
                    failureReason = "economy-input-owner-restore-projection-drift:"
                        + descriptor.DestinationId;
                return false;
            }
            desiredClaims.Add(CreateClaim(descriptor));
            desiredProfiles.Add(CreateProfile(
                descriptor,
                projection.CapacityGrams));
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            ownerDomain,
            desiredClaims,
            desiredProfiles,
            out failureReason);
    }

    private bool TryProject(
        EconomyProjectInputOwnerDescriptor descriptor,
        out EconomyProjectInputOwnerProjection projection,
        out string failureReason)
    {
        projection = default;
        failureReason = string.Empty;
        if (descriptor == null)
        {
            failureReason = "economy-input-owner-descriptor-null";
            return false;
        }
        long revision = mass.AuthorityRevision;
        if (revision <= 0L)
        {
            failureReason = "economy-input-owner-mass-revision-not-positive";
            return false;
        }
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("economy-project-input-owner-capacity@1");
        digest.Append(descriptor.OwnerDomain);
        digest.Append(descriptor.OwnerOperationId);
        digest.Append(descriptor.DestinationId);
        digest.Append(descriptor.Position.x);
        digest.Append(descriptor.Position.y);
        digest.Append((int)descriptor.AnchorKind);
        digest.Append(descriptor.OwnerFacilityId ?? string.Empty);
        digest.Append(revision);
        long capacity = 0L;
        try
        {
            foreach (KeyValuePair<string, int> requirement in
                     descriptor.Requirements)
            {
                long unitGrams = mass.GetDefinitionUnitMass(
                    (ItemDefinitionId)requirement.Key).Value;
                if (unitGrams <= 0L)
                {
                    failureReason =
                        "economy-input-owner-item-mass-not-positive:"
                        + requirement.Key;
                    return false;
                }
                long lineGrams = checked(unitGrams * requirement.Value);
                capacity = checked(capacity + lineGrams);
                digest.Append(requirement.Key);
                digest.Append(requirement.Value);
                digest.Append(unitGrams);
                digest.Append(lineGrams);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "economy-input-owner-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        if (capacity <= 0L)
        {
            failureReason = "economy-input-owner-capacity-not-positive";
            return false;
        }
        digest.Append(capacity);
        projection = new EconomyProjectInputOwnerProjection(
            capacity,
            revision,
            digest.ComputeSha256());
        return true;
    }

    private static EconomyProjectInputOwnerDescriptor CreateDescriptor(
        string ownerDomain,
        string ownerOperationId,
        string destinationId,
        Vector2Int position,
        EconomyProjectInputOwnerAnchorKind anchorKind,
        string ownerFacilityId,
        IReadOnlyDictionary<string, int> requirements,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint) => new(
        ownerDomain,
        ownerOperationId,
        destinationId,
        position,
        anchorKind == EconomyProjectInputOwnerAnchorKind.LiveFacility
            ? FacilityBufferDestinationAnchorKind.LiveFacility
            : FacilityBufferDestinationAnchorKind.ReservedTarget,
        ownerFacilityId,
        requirements,
        storedCapacityGrams,
        storedMassAuthorityRevision,
        storedCapacityFingerprint);

    private bool TryCaptureOwnedPairs(
        string ownerDomain,
        out List<FacilityBufferDestinationClaim> ownedClaims,
        out List<FacilityBufferCapacityProfile> ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        if (ownedClaims.Count != ownedProfiles.Count
            || !ownedClaims.Select(value => value.DestinationId).SequenceEqual(
                ownedProfiles.Select(value => value.DestinationId),
                StringComparer.Ordinal))
        {
            failureReason = "economy-input-owner-authority-pair-torn:"
                + ownerDomain;
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private bool TryRelease(
        FacilityBufferDestinationClaim claim,
        string reasonCode,
        out string failureReason) => releases.TryReleaseAtOwnerPosition(
        claim.DestinationId,
        claim.DropPosition,
        reasonCode,
        out _,
        out failureReason);

    private static FacilityBufferDestinationClaim CreateClaim(
        EconomyProjectInputOwnerDescriptor descriptor) => new(
        descriptor.DestinationId,
        descriptor.Position,
        descriptor.OwnerDomain,
        descriptor.OwnerOperationId,
        OptionalOwnerFacilityId(descriptor),
        descriptor.AnchorKind,
        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);

    private static FacilityBufferCapacityProfile CreateProfile(
        EconomyProjectInputOwnerDescriptor descriptor,
        long capacityGrams) => new(
        descriptor.DestinationId,
        descriptor.Position,
        descriptor.OwnerDomain,
        descriptor.OwnerOperationId,
        OptionalOwnerFacilityId(descriptor),
        new PhysicalMassGrams(capacityGrams),
        EconomyProjectInputOwnerAuthority.CapacitySchemaRevision);

    private static string OptionalOwnerFacilityId(
        EconomyProjectInputOwnerDescriptor descriptor) =>
        descriptor.AnchorKind ==
            FacilityBufferDestinationAnchorKind.ReservedTarget
            ? null
            : descriptor.OwnerFacilityId;

    private static bool StoredProjectionMatches(
        EconomyProjectInputOwnerDescriptor descriptor,
        EconomyProjectInputOwnerProjection projection) =>
        descriptor.StoredCapacityGrams == projection.CapacityGrams
        && descriptor.StoredMassAuthorityRevision
            == projection.MassAuthorityRevision
        && string.Equals(
            descriptor.StoredCapacityFingerprint,
            projection.Fingerprint,
            StringComparison.Ordinal);

    private static bool PairMatches(
        EconomyProjectInputOwnerDescriptor descriptor,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile,
        long capacityGrams) =>
        ClaimMatches(claim, CreateClaim(descriptor))
        && profile != null
        && string.Equals(
            profile.DestinationId,
            descriptor.DestinationId,
            StringComparison.Ordinal)
        && profile.DropPosition == descriptor.Position
        && string.Equals(
            profile.OwnerDomain,
            descriptor.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            descriptor.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            OptionalOwnerFacilityId(descriptor),
            StringComparison.Ordinal)
        && profile.MaxMassGrams == capacityGrams
        && profile.CapacityRevision
            == EconomyProjectInputOwnerAuthority.CapacitySchemaRevision;

    private static bool ClaimMatches(
        FacilityBufferDestinationClaim left,
        FacilityBufferDestinationClaim right) =>
        left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && left.AnchorKind == right.AnchorKind
        && left.AdmissionPolicy == right.AdmissionPolicy
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerDomain, right.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerOperationId, right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerFacilityId, right.OwnerFacilityId,
            StringComparison.Ordinal);
}
