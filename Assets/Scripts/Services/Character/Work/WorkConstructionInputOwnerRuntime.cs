using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WorkConstructionInputOwnerDescriptor
{
    public WorkConstructionInputOwnerDescriptor(
        string orderId,
        string destinationId,
        string constructionSitePersistentId,
        Vector2Int position,
        IReadOnlyDictionary<string, int> requirements,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint)
    {
        OrderId = RequireCanonical(orderId, nameof(orderId));
        DestinationId = RequireCanonical(destinationId, nameof(destinationId));
        ConstructionSitePersistentId = RequireCanonical(
            constructionSitePersistentId,
            nameof(constructionSitePersistentId));
        Position = position;
        Requirements = new Dictionary<string, int>(
            requirements ?? throw new ArgumentNullException(nameof(requirements)),
            StringComparer.Ordinal);
        StoredCapacityGrams = storedCapacityGrams;
        StoredMassAuthorityRevision = storedMassAuthorityRevision;
        StoredCapacityFingerprint = storedCapacityFingerprint ?? string.Empty;
    }

    public string OrderId { get; }
    public string DestinationId { get; }
    public string ConstructionSitePersistentId { get; }
    public Vector2Int Position { get; }
    public IReadOnlyDictionary<string, int> Requirements { get; }
    public long StoredCapacityGrams { get; }
    public long StoredMassAuthorityRevision { get; }
    public string StoredCapacityFingerprint { get; }

    private static string RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Construction input ownership requires canonical IDs.",
                parameterName);
        }
        return value;
    }
}

public readonly struct WorkConstructionInputOwnerProjection
{
    public WorkConstructionInputOwnerProjection(
        long capacityGrams,
        long massAuthorityRevision,
        string capacityFingerprint)
    {
        if (capacityGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(capacityGrams));
        if (massAuthorityRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(massAuthorityRevision));
        if (string.IsNullOrWhiteSpace(capacityFingerprint))
            throw new ArgumentException(nameof(capacityFingerprint));
        CapacityGrams = capacityGrams;
        MassAuthorityRevision = massAuthorityRevision;
        CapacityFingerprint = capacityFingerprint;
    }

    public long CapacityGrams { get; }
    public long MassAuthorityRevision { get; }
    public string CapacityFingerprint { get; }
}

public interface IWorkConstructionInputOwnerRuntime
{
    bool TryOpen(WorkConstructionInputOwnerDescriptor descriptor,
        out WorkConstructionInputOwnerProjection projection,
        out string failureReason);
    bool TryRequestItem(WorkConstructionInputOwnerDescriptor descriptor,
        string itemId, int quantity, out int requested,
        out string failureReason);
    bool TryValidateAuthority(WorkConstructionInputOwnerDescriptor descriptor,
        out string failureReason);
    bool TryPrepareTerminalRelease(WorkConstructionInputOwnerDescriptor descriptor,
        string reasonCode, out string failureReason);
    bool TryRevoke(WorkConstructionInputOwnerDescriptor descriptor,
        out string failureReason);
    bool TryReplaceForRestore(
        IReadOnlyList<WorkConstructionInputOwnerDescriptor> descriptors,
        out string failureReason);
}

public static class WorkConstructionInputOwnerAuthority
{
    public const string OwnerDomain = "work.construction";
    public const long CapacitySchemaRevision = 1L;
    public const string DestinationPrefix =
        ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
        + OwnerDomain + ":";

    public static string DestinationFor(string orderId) =>
        DestinationPrefix + orderId;
}

public sealed class WorkConstructionInputOwnerRuntime :
    IWorkConstructionInputOwnerRuntime
{
    private readonly IPhysicalItemMassQuery mass;
    private readonly IWorldItemStackRuntime deliveries;
    private readonly IBuildingWorldQuery buildings;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public WorkConstructionInputOwnerRuntime(
        IPhysicalItemMassQuery mass,
        IWorldItemStackRuntime deliveries,
        IBuildingWorldQuery buildings,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.deliveries = deliveries ?? throw new ArgumentNullException(nameof(deliveries));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryOpen(WorkConstructionInputOwnerDescriptor descriptor,
        out WorkConstructionInputOwnerProjection projection,
        out string failureReason)
    {
        projection = default;
        if (!TryProject(descriptor, out projection, out failureReason)
            || !TryCapture(out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
            return false;
        if (ownedClaims.Any(value => string.Equals(value.DestinationId,
                descriptor.DestinationId, StringComparison.Ordinal)))
        {
            failureReason = "work-construction-input-destination-duplicate";
            projection = default;
            return false;
        }
        ownedClaims.Add(CreateClaim(descriptor));
        ownedProfiles.Add(CreateProfile(descriptor, projection.CapacityGrams));
        if (lifecycle.TryReplaceOwnedAuthorities(
                WorkConstructionInputOwnerAuthority.OwnerDomain,
                ownedClaims, ownedProfiles, out failureReason))
            return true;
        projection = default;
        return false;
    }

    public bool TryRequestItem(WorkConstructionInputOwnerDescriptor descriptor,
        string itemId, int quantity, out int requested,
        out string failureReason)
    {
        requested = 0;
        if (!TryValidateAuthority(descriptor, out failureReason)
            || string.IsNullOrWhiteSpace(itemId)
            || !descriptor.Requirements.TryGetValue(itemId, out int required)
            || quantity <= 0 || quantity > required)
        {
            failureReason = failureReason.Length == 0
                ? "work-construction-input-request-invalid" : failureReason;
            return false;
        }
        return deliveries.TryRequestItemDelivery(itemId, quantity,
            descriptor.Position, descriptor.DestinationId,
            out requested, out failureReason);
    }

    public bool TryValidateAuthority(WorkConstructionInputOwnerDescriptor descriptor,
        out string failureReason) => TryValidateAuthority(
        descriptor,
        requireLiveSite: true,
        out failureReason);

    private bool TryValidateAuthority(
        WorkConstructionInputOwnerDescriptor descriptor,
        bool requireLiveSite,
        out string failureReason)
    {
        if (!TryProject(
                descriptor,
                requireLiveSite,
                out WorkConstructionInputOwnerProjection projection,
                out failureReason)
            || descriptor.StoredCapacityGrams != projection.CapacityGrams
            || descriptor.StoredMassAuthorityRevision != projection.MassAuthorityRevision
            || !string.Equals(descriptor.StoredCapacityFingerprint,
                projection.CapacityFingerprint, StringComparison.Ordinal))
        {
            if (failureReason.Length == 0)
                failureReason = "work-construction-input-projection-mismatch";
            return false;
        }
        FacilityBufferDestinationClaim claim = claims.CaptureAuthorityClaims()
            .SingleOrDefault(value => value != null && string.Equals(
                value.DestinationId, descriptor.DestinationId,
                StringComparison.Ordinal));
        FacilityBufferCapacityProfile profile = capacities.CaptureAuthorityProfiles()
            .SingleOrDefault(value => value != null && string.Equals(
                value.DestinationId, descriptor.DestinationId,
                StringComparison.Ordinal));
        if (!PairMatches(descriptor, claim, profile, projection.CapacityGrams))
        {
            failureReason = "work-construction-input-authority-mismatch";
            return false;
        }
        return true;
    }

    public bool TryPrepareTerminalRelease(
        WorkConstructionInputOwnerDescriptor descriptor,
        string reasonCode, out string failureReason)
    {
        // The owning site may already be destroyed when orphan cleanup runs.
        // At that terminal boundary the persisted descriptor and its exact
        // claim/profile pair remain the authority; active delivery and general
        // mutation validation continue to require the live site.
        if (!TryValidateAuthority(
                descriptor,
                requireLiveSite: false,
                out failureReason))
            return false;
        return releases.TryReleaseAtOwnerPosition(descriptor.DestinationId,
            descriptor.Position, reasonCode, out _, out failureReason);
    }

    public bool TryRevoke(WorkConstructionInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        if (!TryCapture(out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
            return false;
        FacilityBufferDestinationClaim[] foundClaims = ownedClaims.Where(value =>
            string.Equals(value.DestinationId, descriptor.DestinationId,
                StringComparison.Ordinal)).ToArray();
        FacilityBufferCapacityProfile[] foundProfiles = ownedProfiles.Where(value =>
            string.Equals(value.DestinationId, descriptor.DestinationId,
                StringComparison.Ordinal)).ToArray();
        if (foundClaims.Length == 0 && foundProfiles.Length == 0)
            return true;
        if (foundClaims.Length != 1 || foundProfiles.Length != 1
            || !PairMatches(descriptor, foundClaims[0], foundProfiles[0],
                descriptor.StoredCapacityGrams))
        {
            failureReason = "work-construction-input-revoke-pair-invalid";
            return false;
        }
        ownedClaims.Remove(foundClaims[0]);
        ownedProfiles.Remove(foundProfiles[0]);
        return lifecycle.TryReplaceOwnedAuthorities(
            WorkConstructionInputOwnerAuthority.OwnerDomain,
            ownedClaims, ownedProfiles, out failureReason);
    }

    public bool TryReplaceForRestore(
        IReadOnlyList<WorkConstructionInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        WorkConstructionInputOwnerDescriptor[] ordered = (descriptors
                ?? Array.Empty<WorkConstructionInputOwnerDescriptor>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal).ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.DestinationId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason = "work-construction-input-restore-set-invalid";
            return false;
        }
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (WorkConstructionInputOwnerDescriptor descriptor in ordered)
        {
            if (!TryProject(descriptor,
                    requireLiveSite: false,
                    out WorkConstructionInputOwnerProjection projection,
                    out failureReason)
                || descriptor.StoredCapacityGrams != projection.CapacityGrams
                || descriptor.StoredMassAuthorityRevision != projection.MassAuthorityRevision
                || !string.Equals(descriptor.StoredCapacityFingerprint,
                    projection.CapacityFingerprint, StringComparison.Ordinal))
            {
                if (failureReason.Length == 0)
                    failureReason = "work-construction-input-restore-projection-invalid";
                return false;
            }
            desiredClaims.Add(CreateClaim(descriptor));
            desiredProfiles.Add(CreateProfile(descriptor, projection.CapacityGrams));
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            WorkConstructionInputOwnerAuthority.OwnerDomain,
            desiredClaims, desiredProfiles, out failureReason);
    }

    private bool TryProject(WorkConstructionInputOwnerDescriptor descriptor,
        out WorkConstructionInputOwnerProjection projection,
        out string failureReason) => TryProject(
        descriptor,
        requireLiveSite: true,
        out projection,
        out failureReason);

    private bool TryProject(WorkConstructionInputOwnerDescriptor descriptor,
        bool requireLiveSite,
        out WorkConstructionInputOwnerProjection projection,
        out string failureReason)
    {
        projection = default;
        failureReason = string.Empty;
        if (descriptor == null
            || !string.Equals(descriptor.DestinationId,
                WorkConstructionInputOwnerAuthority.DestinationFor(descriptor.OrderId),
                StringComparison.Ordinal)
            || descriptor.Requirements.Count == 0
            || (requireLiveSite && !TryFindSite(descriptor)))
        {
            failureReason = "work-construction-input-descriptor-invalid";
            return false;
        }
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("work-construction-input-capacity@1");
        digest.Append(descriptor.OrderId);
        digest.Append(descriptor.DestinationId);
        digest.Append(descriptor.ConstructionSitePersistentId);
        digest.Append(descriptor.Position.x);
        digest.Append(descriptor.Position.y);
        digest.Append(mass.AuthorityRevision);
        long capacity = 0L;
        try
        {
            foreach (KeyValuePair<string, int> requirement in descriptor.Requirements
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(requirement.Key)
                    || !string.Equals(requirement.Key, requirement.Key.Trim(),
                        StringComparison.Ordinal) || requirement.Value <= 0)
                {
                    failureReason = "work-construction-input-requirement-invalid";
                    return false;
                }
                ItemDefinitionId itemId = (ItemDefinitionId)requirement.Key;
                PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                    mass, itemId, string.Empty,
                    Array.Empty<ItemInstanceComponentSaveData>());
                long lineMass = mass.GetQuantityMass(itemId, subject,
                    requirement.Value).Value;
                if (lineMass <= 0L)
                {
                    failureReason = "work-construction-input-mass-not-positive";
                    return false;
                }
                capacity = checked(capacity + lineMass);
                digest.Append(requirement.Key);
                digest.Append(requirement.Value);
                digest.Append(lineMass);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "work-construction-input-projection-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (capacity <= 0L || mass.AuthorityRevision <= 0L)
        {
            failureReason = "work-construction-input-projection-not-positive";
            return false;
        }
        digest.Append(capacity);
        projection = new WorkConstructionInputOwnerProjection(capacity,
            mass.AuthorityRevision, digest.ComputeSha256());
        return true;
    }

    private bool TryFindSite(WorkConstructionInputOwnerDescriptor descriptor) =>
        (buildings.Buildings ?? Array.Empty<BuildableObject>())
            .Count(value => value is ConstructionSite && !value.isDestroy
                && value.centerPos == descriptor.Position
                && string.Equals(value.PersistentInstanceId.Value,
                    descriptor.ConstructionSitePersistentId,
                    StringComparison.Ordinal)) == 1;

    private bool TryCapture(out List<FacilityBufferDestinationClaim> ownedClaims,
        out List<FacilityBufferCapacityProfile> ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims().Where(value => value != null
            && string.Equals(value.OwnerDomain,
                WorkConstructionInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal)).OrderBy(value => value.DestinationId,
            StringComparer.Ordinal).ToList();
        ownedProfiles = capacities.CaptureAuthorityProfiles().Where(value => value != null
            && string.Equals(value.OwnerDomain,
                WorkConstructionInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal)).OrderBy(value => value.DestinationId,
            StringComparer.Ordinal).ToList();
        if (ownedClaims.Count != ownedProfiles.Count
            || !ownedClaims.Select(value => value.DestinationId).SequenceEqual(
                ownedProfiles.Select(value => value.DestinationId),
                StringComparer.Ordinal))
        {
            failureReason = "work-construction-input-owner-set-torn";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static FacilityBufferDestinationClaim CreateClaim(
        WorkConstructionInputOwnerDescriptor descriptor) => new(
        descriptor.DestinationId, descriptor.Position,
        WorkConstructionInputOwnerAuthority.OwnerDomain, descriptor.OrderId,
        descriptor.ConstructionSitePersistentId,
        FacilityBufferDestinationAnchorKind.LiveBuilding,
        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);

    private static FacilityBufferCapacityProfile CreateProfile(
        WorkConstructionInputOwnerDescriptor descriptor, long capacityGrams) => new(
        descriptor.DestinationId, descriptor.Position,
        WorkConstructionInputOwnerAuthority.OwnerDomain, descriptor.OrderId,
        descriptor.ConstructionSitePersistentId,
        new PhysicalMassGrams(capacityGrams),
        WorkConstructionInputOwnerAuthority.CapacitySchemaRevision);

    private static bool PairMatches(WorkConstructionInputOwnerDescriptor descriptor,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile, long capacityGrams) =>
        claim != null && profile != null
        && string.Equals(claim.DestinationId, descriptor.DestinationId,
            StringComparison.Ordinal)
        && claim.DropPosition == descriptor.Position
        && string.Equals(claim.OwnerDomain,
            WorkConstructionInputOwnerAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, descriptor.OrderId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId,
            descriptor.ConstructionSitePersistentId, StringComparison.Ordinal)
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveBuilding
        && claim.AdmissionPolicy == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && string.Equals(profile.DestinationId, claim.DestinationId,
            StringComparison.Ordinal)
        && profile.DropPosition == claim.DropPosition
        && string.Equals(profile.OwnerDomain, claim.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerOperationId, claim.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, claim.OwnerFacilityId,
            StringComparison.Ordinal)
        && profile.MaxMassGrams == capacityGrams
        && profile.CapacityRevision == WorkConstructionInputOwnerAuthority.CapacitySchemaRevision;
}
