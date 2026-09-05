using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum FacilityEvolutionInputKind
{
    Modification = 0,
    Recalibration = 1,
    Relocation = 2
}

public sealed class FacilityEvolutionInputOwnerDescriptor
{
    public FacilityEvolutionInputOwnerDescriptor(
        FacilityEvolutionInputKind kind,
        string orderId,
        string destinationId,
        string facilityPersistentId,
        Vector2Int position,
        IReadOnlyDictionary<string, int> requirements,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint)
    {
        Kind = kind;
        OrderId = RequireCanonical(orderId, nameof(orderId));
        DestinationId = RequireCanonical(destinationId, nameof(destinationId));
        FacilityPersistentId = RequireCanonical(facilityPersistentId,
            nameof(facilityPersistentId));
        Position = position;
        Requirements = new Dictionary<string, int>(requirements
            ?? throw new ArgumentNullException(nameof(requirements)),
            StringComparer.Ordinal);
        StoredCapacityGrams = storedCapacityGrams;
        StoredMassAuthorityRevision = storedMassAuthorityRevision;
        StoredCapacityFingerprint = storedCapacityFingerprint ?? string.Empty;
    }

    public FacilityEvolutionInputKind Kind { get; }
    public string OrderId { get; }
    public string DestinationId { get; }
    public string FacilityPersistentId { get; }
    public Vector2Int Position { get; }
    public IReadOnlyDictionary<string, int> Requirements { get; }
    public long StoredCapacityGrams { get; }
    public long StoredMassAuthorityRevision { get; }
    public string StoredCapacityFingerprint { get; }

    private static string RequireCanonical(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                "Facility-evolution input ownership requires canonical IDs.",
                name);
        return value;
    }
}

public readonly struct FacilityEvolutionInputOwnerProjection
{
    public FacilityEvolutionInputOwnerProjection(long capacityGrams,
        long massAuthorityRevision, string capacityFingerprint)
    {
        if (capacityGrams <= 0L || massAuthorityRevision <= 0L
            || string.IsNullOrWhiteSpace(capacityFingerprint))
            throw new ArgumentException(
                "Facility-evolution projection must be positive and canonical.");
        CapacityGrams = capacityGrams;
        MassAuthorityRevision = massAuthorityRevision;
        CapacityFingerprint = capacityFingerprint;
    }
    public long CapacityGrams { get; }
    public long MassAuthorityRevision { get; }
    public string CapacityFingerprint { get; }
}

public interface IFacilityEvolutionInputOwnerRuntime
{
    bool TryOpen(FacilityEvolutionInputOwnerDescriptor descriptor,
        out FacilityEvolutionInputOwnerProjection projection,
        out string failureReason);
    bool TryRequest(FacilityEvolutionInputOwnerDescriptor descriptor,
        out string failureReason);
    bool TryValidateAuthority(FacilityEvolutionInputOwnerDescriptor descriptor,
        out string failureReason);
    bool TryPrepareTerminalRelease(FacilityEvolutionInputOwnerDescriptor descriptor,
        string reasonCode, out string failureReason);
    bool TryRevoke(FacilityEvolutionInputOwnerDescriptor descriptor,
        out string failureReason);
    bool TryReplaceForRestore(
        IReadOnlyList<FacilityEvolutionInputOwnerDescriptor> descriptors,
        out string failureReason);
}

public static class FacilityEvolutionInputOwnerAuthority
{
    public const string OwnerDomain = "facility.evolution";
    public const long CapacitySchemaRevision = 1L;
    public const string DestinationPrefix =
        ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
        + OwnerDomain + ":";

    public static string DestinationFor(FacilityEvolutionInputKind kind,
        string orderId) => DestinationPrefix + kind.ToString().ToLowerInvariant()
        + ":" + orderId;
}

public sealed class FacilityEvolutionInputOwnerRuntime :
    IFacilityEvolutionInputOwnerRuntime
{
    private readonly IPhysicalItemMassQuery mass;
    private readonly IWorldItemStackRuntime deliveries;
    private readonly IBuildingWorldQuery buildings;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public FacilityEvolutionInputOwnerRuntime(IPhysicalItemMassQuery mass,
        IWorldItemStackRuntime deliveries, IBuildingWorldQuery buildings,
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

    public bool TryOpen(FacilityEvolutionInputOwnerDescriptor descriptor,
        out FacilityEvolutionInputOwnerProjection projection,
        out string failureReason)
    {
        projection = default;
        if (!TryProject(descriptor, out projection, out failureReason)
            || !TryCapture(out List<FacilityBufferDestinationClaim> ownerClaims,
                out List<FacilityBufferCapacityProfile> ownerProfiles,
                out failureReason)) return false;
        if (ownerClaims.Any(value => string.Equals(value.DestinationId,
                descriptor.DestinationId, StringComparison.Ordinal)))
        {
            failureReason = "facility-evolution-input-destination-duplicate";
            projection = default;
            return false;
        }
        ownerClaims.Add(CreateClaim(descriptor));
        ownerProfiles.Add(CreateProfile(descriptor, projection.CapacityGrams));
        if (lifecycle.TryReplaceOwnedAuthorities(
                FacilityEvolutionInputOwnerAuthority.OwnerDomain,
                ownerClaims, ownerProfiles, out failureReason)) return true;
        projection = default;
        return false;
    }

    public bool TryRequest(FacilityEvolutionInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        if (!TryValidateAuthority(descriptor, out failureReason)) return false;
        foreach (KeyValuePair<string, int> requirement in descriptor.Requirements
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!deliveries.TryRequestItemDelivery(requirement.Key,
                    requirement.Value, descriptor.Position,
                    descriptor.DestinationId, out int requested,
                    out string requestFailure)
                || requested != requirement.Value)
            {
                failureReason = string.IsNullOrWhiteSpace(requestFailure)
                    ? "facility-evolution-input-delivery-incomplete:"
                        + requirement.Key : requestFailure;
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryValidateAuthority(FacilityEvolutionInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        if (!TryProject(descriptor,
                out FacilityEvolutionInputOwnerProjection projection,
                out failureReason)
            || descriptor.StoredCapacityGrams != projection.CapacityGrams
            || descriptor.StoredMassAuthorityRevision != projection.MassAuthorityRevision
            || !string.Equals(descriptor.StoredCapacityFingerprint,
                projection.CapacityFingerprint, StringComparison.Ordinal))
        {
            if (failureReason.Length == 0)
                failureReason = "facility-evolution-input-projection-mismatch";
            return false;
        }
        FacilityBufferDestinationClaim[] ownerClaims = claims
            .CaptureAuthorityClaims().Where(value => value != null
                && string.Equals(value.DestinationId, descriptor.DestinationId,
                    StringComparison.Ordinal)).ToArray();
        FacilityBufferCapacityProfile[] ownerProfiles = capacities
            .CaptureAuthorityProfiles().Where(value => value != null
                && string.Equals(value.DestinationId, descriptor.DestinationId,
                    StringComparison.Ordinal)).ToArray();
        if (ownerClaims.Length != 1 || ownerProfiles.Length != 1
            || !PairMatches(descriptor, ownerClaims[0], ownerProfiles[0],
                projection.CapacityGrams))
        {
            failureReason = "facility-evolution-input-authority-mismatch";
            return false;
        }
        return true;
    }

    public bool TryPrepareTerminalRelease(
        FacilityEvolutionInputOwnerDescriptor descriptor,
        string reasonCode, out string failureReason)
    {
        if (!TryValidateAuthority(descriptor, out failureReason)) return false;
        return releases.TryReleaseAtOwnerPosition(descriptor.DestinationId,
            descriptor.Position, reasonCode, out _, out failureReason);
    }

    public bool TryRevoke(FacilityEvolutionInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        if (!TryCapture(out List<FacilityBufferDestinationClaim> ownerClaims,
                out List<FacilityBufferCapacityProfile> ownerProfiles,
                out failureReason)) return false;
        FacilityBufferDestinationClaim[] matchingClaims = ownerClaims.Where(value =>
            string.Equals(value.DestinationId, descriptor.DestinationId,
                StringComparison.Ordinal)).ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = ownerProfiles.Where(value =>
            string.Equals(value.DestinationId, descriptor.DestinationId,
                StringComparison.Ordinal)).ToArray();
        if (matchingClaims.Length == 0 && matchingProfiles.Length == 0) return true;
        if (matchingClaims.Length != 1 || matchingProfiles.Length != 1
            || !PairMatches(descriptor, matchingClaims[0], matchingProfiles[0],
                descriptor.StoredCapacityGrams))
        {
            failureReason = "facility-evolution-input-revoke-pair-invalid";
            return false;
        }
        ownerClaims.Remove(matchingClaims[0]);
        ownerProfiles.Remove(matchingProfiles[0]);
        return lifecycle.TryReplaceOwnedAuthorities(
            FacilityEvolutionInputOwnerAuthority.OwnerDomain,
            ownerClaims, ownerProfiles, out failureReason);
    }

    public bool TryReplaceForRestore(
        IReadOnlyList<FacilityEvolutionInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        FacilityEvolutionInputOwnerDescriptor[] ordered = (descriptors
                ?? Array.Empty<FacilityEvolutionInputOwnerDescriptor>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal).ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.DestinationId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason = "facility-evolution-input-restore-set-invalid";
            return false;
        }
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (FacilityEvolutionInputOwnerDescriptor descriptor in ordered)
        {
            if (!TryProject(descriptor,
                    out FacilityEvolutionInputOwnerProjection projection,
                    out failureReason)
                || descriptor.StoredCapacityGrams != projection.CapacityGrams
                || descriptor.StoredMassAuthorityRevision != projection.MassAuthorityRevision
                || !string.Equals(descriptor.StoredCapacityFingerprint,
                    projection.CapacityFingerprint, StringComparison.Ordinal))
            {
                if (failureReason.Length == 0)
                    failureReason = "facility-evolution-input-restore-projection-invalid";
                return false;
            }
            desiredClaims.Add(CreateClaim(descriptor));
            desiredProfiles.Add(CreateProfile(descriptor, projection.CapacityGrams));
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            FacilityEvolutionInputOwnerAuthority.OwnerDomain,
            desiredClaims, desiredProfiles, out failureReason);
    }

    private bool TryProject(FacilityEvolutionInputOwnerDescriptor descriptor,
        out FacilityEvolutionInputOwnerProjection projection,
        out string failureReason)
    {
        projection = default;
        failureReason = string.Empty;
        if (descriptor == null || descriptor.Requirements.Count == 0
            || mass.AuthorityRevision <= 0L
            || !string.Equals(descriptor.DestinationId,
                FacilityEvolutionInputOwnerAuthority.DestinationFor(
                    descriptor.Kind, descriptor.OrderId), StringComparison.Ordinal)
            || (descriptor.Kind != FacilityEvolutionInputKind.Relocation
                && !HasLiveFacility(descriptor)))
        {
            failureReason = "facility-evolution-input-descriptor-invalid";
            return false;
        }
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("facility-evolution-input-capacity@1");
        digest.Append((int)descriptor.Kind);
        digest.Append(descriptor.OrderId);
        digest.Append(descriptor.DestinationId);
        digest.Append(descriptor.FacilityPersistentId);
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
                    failureReason = "facility-evolution-input-requirement-invalid";
                    return false;
                }
                ItemDefinitionId itemId = (ItemDefinitionId)requirement.Key;
                PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                    mass, itemId, string.Empty,
                    Array.Empty<ItemInstanceComponentSaveData>());
                long lineMass = mass.GetQuantityMass(itemId, subject,
                    requirement.Value).Value;
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
            failureReason = "facility-evolution-input-projection-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (capacity <= 0L)
        {
            failureReason = "facility-evolution-input-capacity-not-positive";
            return false;
        }
        digest.Append(capacity);
        projection = new FacilityEvolutionInputOwnerProjection(capacity,
            mass.AuthorityRevision, digest.ComputeSha256());
        return true;
    }

    private bool HasLiveFacility(FacilityEvolutionInputOwnerDescriptor descriptor) =>
        (buildings.Buildings ?? Array.Empty<BuildableObject>()).Count(value =>
            value != null && !value.isDestroy
            && value.centerPos == descriptor.Position
            && string.Equals(value.PersistentInstanceId.Value,
                descriptor.FacilityPersistentId, StringComparison.Ordinal)) == 1;

    private bool TryCapture(out List<FacilityBufferDestinationClaim> ownerClaims,
        out List<FacilityBufferCapacityProfile> ownerProfiles,
        out string failureReason)
    {
        ownerClaims = claims.CaptureAuthorityClaims().Where(value => value != null
            && string.Equals(value.OwnerDomain,
                FacilityEvolutionInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal)).OrderBy(value => value.DestinationId,
            StringComparer.Ordinal).ToList();
        ownerProfiles = capacities.CaptureAuthorityProfiles().Where(value => value != null
            && string.Equals(value.OwnerDomain,
                FacilityEvolutionInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal)).OrderBy(value => value.DestinationId,
            StringComparer.Ordinal).ToList();
        if (ownerClaims.Count != ownerProfiles.Count
            || !ownerClaims.Select(value => value.DestinationId).SequenceEqual(
                ownerProfiles.Select(value => value.DestinationId),
                StringComparer.Ordinal))
        {
            failureReason = "facility-evolution-input-owner-set-torn";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static FacilityBufferDestinationClaim CreateClaim(
        FacilityEvolutionInputOwnerDescriptor descriptor) => new(
        descriptor.DestinationId, descriptor.Position,
        FacilityEvolutionInputOwnerAuthority.OwnerDomain, descriptor.OrderId,
        descriptor.FacilityPersistentId,
        descriptor.Kind == FacilityEvolutionInputKind.Relocation
            ? FacilityBufferDestinationAnchorKind.ReservedTarget
            : FacilityBufferDestinationAnchorKind.LiveFacility,
        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);

    private static FacilityBufferCapacityProfile CreateProfile(
        FacilityEvolutionInputOwnerDescriptor descriptor, long capacity) => new(
        descriptor.DestinationId, descriptor.Position,
        FacilityEvolutionInputOwnerAuthority.OwnerDomain, descriptor.OrderId,
        descriptor.FacilityPersistentId, new PhysicalMassGrams(capacity),
        FacilityEvolutionInputOwnerAuthority.CapacitySchemaRevision);

    private static bool PairMatches(FacilityEvolutionInputOwnerDescriptor descriptor,
        FacilityBufferDestinationClaim claim, FacilityBufferCapacityProfile profile,
        long capacity) => claim != null && profile != null
        && string.Equals(claim.DestinationId, descriptor.DestinationId,
            StringComparison.Ordinal)
        && claim.DropPosition == descriptor.Position
        && string.Equals(claim.OwnerDomain,
            FacilityEvolutionInputOwnerAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, descriptor.OrderId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, descriptor.FacilityPersistentId,
            StringComparison.Ordinal)
        && claim.AnchorKind == (descriptor.Kind == FacilityEvolutionInputKind.Relocation
            ? FacilityBufferDestinationAnchorKind.ReservedTarget
            : FacilityBufferDestinationAnchorKind.LiveFacility)
        && claim.AdmissionPolicy == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && string.Equals(profile.DestinationId, claim.DestinationId,
            StringComparison.Ordinal)
        && profile.DropPosition == claim.DropPosition
        && string.Equals(profile.OwnerDomain, claim.OwnerDomain, StringComparison.Ordinal)
        && string.Equals(profile.OwnerOperationId, claim.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, claim.OwnerFacilityId,
            StringComparison.Ordinal)
        && profile.MaxMassGrams == capacity
        && profile.CapacityRevision ==
            FacilityEvolutionInputOwnerAuthority.CapacitySchemaRevision;
}

public sealed class FacilityEvolutionInputOwnerRestoreParticipant :
    IDungeonRestoreTransactionParticipant
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IFacilityEvolutionStateComponentFactory states;
    private readonly IFacilityEvolutionInputOwnerRuntime owner;
    private bool active;
    private bool published;

    public FacilityEvolutionInputOwnerRestoreParticipant(
        IBuildingWorldQuery buildings,
        IFacilityEvolutionStateComponentFactory states,
        IFacilityEvolutionInputOwnerRuntime owner)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.states = states ?? throw new ArgumentNullException(nameof(states));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string ParticipantId =>
        "219.world.facility-evolution-input-owner";

    public void BeginRestoreCandidate()
    {
        if (active) throw new InvalidOperationException(
            "Facility-evolution input-owner restore is already active.");
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
            throw new InvalidOperationException(
                "Facility-evolution input-owner restore is not ready.");
        List<FacilityEvolutionInputOwnerDescriptor> descriptors = new();
        foreach (BuildableObject facility in (buildings.Buildings
                     ?? Array.Empty<BuildableObject>())
                 .Where(value => value != null && !value.isDestroy)
                 .OrderBy(value => value.PersistentInstanceId.Value,
                     StringComparer.Ordinal))
        {
            FacilityEvolutionState state = states.GetOrAdd(facility)
                ?.InstanceEvolution;
            if (state?.modificationOrder != null)
                descriptors.Add(ToDescriptor(state.modificationOrder));
            if (state?.recalibrationOrder != null)
                descriptors.Add(ToDescriptor(state.recalibrationOrder));
            if (state?.relocationOrder != null)
                descriptors.Add(ToDescriptor(state.relocationOrder));
        }
        if (!owner.TryReplaceForRestore(descriptors, out string failureReason))
            throw new InvalidOperationException(
                "Facility-evolution input-owner restore join failed: "
                + failureReason);
        published = true;
    }

    private static FacilityEvolutionInputOwnerDescriptor ToDescriptor(
        FacilityModificationOrder order) => new(
        FacilityEvolutionInputKind.Modification, order.orderId,
        order.destinationId, order.facilityPersistentId,
        new Vector2Int(order.destinationX, order.destinationY),
        FacilityEvolutionRules.BuildRequirements(order),
        order.inputCapacityGrams, order.inputMassAuthorityRevision,
        order.inputCapacityFingerprint);

    private static FacilityEvolutionInputOwnerDescriptor ToDescriptor(
        FacilityRecalibrationOrder order) => new(
        FacilityEvolutionInputKind.Recalibration, order.orderId,
        order.destinationId, order.facilityPersistentId,
        new Vector2Int(order.destinationX, order.destinationY),
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [order.catalystItemId] = 1
        }, order.inputCapacityGrams, order.inputMassAuthorityRevision,
        order.inputCapacityFingerprint);

    private static FacilityEvolutionInputOwnerDescriptor ToDescriptor(
        FacilityRelocationOrder order) => new(
        FacilityEvolutionInputKind.Relocation, order.orderId,
        order.destinationId, order.facilityPersistentId,
        order.DestinationPosition,
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [order.packageItemId] = 1
        }, order.inputCapacityGrams, order.inputMassAuthorityRevision,
        order.inputCapacityFingerprint);

    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }
    public void CompleteRestoreCandidate()
    {
        if (!active || !published) throw new InvalidOperationException(
            "Facility-evolution input-owner restore cannot complete.");
        active = false;
        published = false;
    }
    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }
}
