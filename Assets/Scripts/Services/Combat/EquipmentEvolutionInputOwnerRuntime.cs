using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IEquipmentEvolutionInputDeliveryGateway
{
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();

    bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);

    bool TryRequestStackDelivery(
        string stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
}

public sealed class EquipmentEvolutionInputDeliveryGateway :
    IEquipmentEvolutionInputDeliveryGateway
{
    private readonly IWorldItemStackRuntime items;

    public EquipmentEvolutionInputDeliveryGateway(IWorldItemStackRuntime items)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
        items.GetAllStacks();

    public bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason) => items.TryRequestItemDelivery(
        itemId,
        amount,
        destinationPosition,
        destinationId,
        out requested,
        out failureReason);

    public bool TryRequestStackDelivery(
        string stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason) => items.TryRequestStackDelivery(
        stackId,
        amount,
        destinationPosition,
        destinationId,
        out requested,
        out failureReason);
}

public sealed class EquipmentEvolutionInputOwnerDescriptor
{
    public EquipmentEvolutionInputOwnerDescriptor(
        string orderId,
        string destinationId,
        string facilityPersistentId,
        Vector2Int position,
        string equipmentInstanceId,
        string equipmentItemId,
        string equipmentSourceStackId,
        IReadOnlyList<ItemInstanceComponentSaveData> equipmentComponents,
        IReadOnlyDictionary<string, int> materialRequirements,
        long storedCapacityGrams,
        long storedMassAuthorityRevision,
        string storedCapacityFingerprint)
    {
        OrderId = RequireCanonical(orderId, nameof(orderId));
        DestinationId = RequireCanonical(destinationId, nameof(destinationId));
        FacilityPersistentId = RequireCanonical(
            facilityPersistentId,
            nameof(facilityPersistentId));
        EquipmentInstanceId = RequireCanonical(
            equipmentInstanceId,
            nameof(equipmentInstanceId));
        EquipmentItemId = RequireCanonical(
            equipmentItemId,
            nameof(equipmentItemId));
        EquipmentSourceStackId = RequireCanonical(
            equipmentSourceStackId,
            nameof(equipmentSourceStackId));
        Position = position;
        EquipmentComponents = (equipmentComponents
                ?? throw new ArgumentNullException(nameof(equipmentComponents)))
            .Where(value => value != null)
            .Select(value => value.Clone())
            .OrderBy(value => value.componentTypeId, StringComparer.Ordinal)
            .ThenBy(value => value.ToCanonicalString(), StringComparer.Ordinal)
            .ToArray();
        MaterialRequirements = new Dictionary<string, int>(
            materialRequirements
                ?? throw new ArgumentNullException(nameof(materialRequirements)),
            StringComparer.Ordinal);
        StoredCapacityGrams = storedCapacityGrams;
        StoredMassAuthorityRevision = storedMassAuthorityRevision;
        StoredCapacityFingerprint = storedCapacityFingerprint ?? string.Empty;
    }

    public string OrderId { get; }
    public string DestinationId { get; }
    public string FacilityPersistentId { get; }
    public Vector2Int Position { get; }
    public string EquipmentInstanceId { get; }
    public string EquipmentItemId { get; }
    public string EquipmentSourceStackId { get; }
    public IReadOnlyList<ItemInstanceComponentSaveData> EquipmentComponents { get; }
    public IReadOnlyDictionary<string, int> MaterialRequirements { get; }
    public long StoredCapacityGrams { get; }
    public long StoredMassAuthorityRevision { get; }
    public string StoredCapacityFingerprint { get; }

    private static string RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Equipment-evolution input ownership requires canonical IDs.",
                parameterName);
        }
        return value;
    }
}

public readonly struct EquipmentEvolutionInputOwnerProjection
{
    public EquipmentEvolutionInputOwnerProjection(
        long capacityGrams,
        long massAuthorityRevision,
        string capacityFingerprint)
    {
        if (capacityGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(capacityGrams));
        if (massAuthorityRevision < 0L)
            throw new ArgumentOutOfRangeException(nameof(massAuthorityRevision));
        if (string.IsNullOrWhiteSpace(capacityFingerprint))
            throw new ArgumentException(
                "Capacity fingerprint must be non-empty.",
                nameof(capacityFingerprint));
        CapacityGrams = capacityGrams;
        MassAuthorityRevision = massAuthorityRevision;
        CapacityFingerprint = capacityFingerprint;
    }

    public long CapacityGrams { get; }
    public long MassAuthorityRevision { get; }
    public string CapacityFingerprint { get; }
}

public interface IEquipmentEvolutionInputOwnerRuntime
{
    bool TryOpen(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        out EquipmentEvolutionInputOwnerProjection projection,
        out string failureReason);

    bool TryRequest(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        out string failureReason);

    bool TryClose(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        string reasonCode,
        out string failureReason);

    bool TryValidateAuthority(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        out string failureReason);

    bool TryReplaceForRestore(
        IReadOnlyList<EquipmentEvolutionInputOwnerDescriptor> descriptors,
        out string failureReason);
}

public static class EquipmentEvolutionInputOwnerAuthority
{
    public const string OwnerDomain = "combat.equipment-evolution";
    public const long CapacitySchemaRevision = 1L;
    public const string ReforgeDestinationPrefix = "facility-reforge:";
    public const string ReattunementDestinationPrefix = "facility-reattune:";

    public static bool IsExpectedDestination(string orderId, string destinationId) =>
        string.Equals(
            destinationId,
            ReforgeDestinationPrefix + orderId,
            StringComparison.Ordinal)
        || string.Equals(
            destinationId,
            ReattunementDestinationPrefix + orderId,
            StringComparison.Ordinal);
}

/// <summary>
/// Owns the LiveFacility claim and exact positive-gram profile for equipment
/// reforge/reattunement inputs. Equipment stays a unique physical lot while
/// materials cross the existing typed Transfer/WIP boundary.
/// </summary>
public sealed class EquipmentEvolutionInputOwnerRuntime :
    IEquipmentEvolutionInputOwnerRuntime
{
    private readonly IPhysicalItemMassQuery mass;
    private readonly IEquipmentEvolutionInputDeliveryGateway deliveries;
    private readonly IBuildingWorldQuery buildings;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public EquipmentEvolutionInputOwnerRuntime(
        IPhysicalItemMassQuery mass,
        IEquipmentEvolutionInputDeliveryGateway deliveries,
        IBuildingWorldQuery buildings,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.deliveries = deliveries
            ?? throw new ArgumentNullException(nameof(deliveries));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryOpen(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        out EquipmentEvolutionInputOwnerProjection projection,
        out string failureReason)
    {
        projection = default;
        if (!TryProject(descriptor, out projection, out failureReason)
            || !TryCaptureOwnedPairs(
                out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
        {
            return false;
        }
        if (ownedClaims.Any(value => string.Equals(
                value.DestinationId,
                descriptor.DestinationId,
                StringComparison.Ordinal)))
        {
            failureReason = "equipment-evolution-input-destination-duplicate:"
                + descriptor.DestinationId;
            return false;
        }
        ownedClaims.Add(CreateClaim(descriptor));
        ownedProfiles.Add(CreateProfile(descriptor, projection.CapacityGrams));
        if (lifecycle.TryReplaceOwnedAuthorities(
                EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
                ownedClaims,
                ownedProfiles,
                out failureReason))
        {
            return true;
        }
        failureReason = "equipment-evolution-input-authority-publish-failed:"
            + failureReason;
        projection = default;
        return false;
    }

    public bool TryRequest(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        if (!TryValidateAuthority(descriptor, out failureReason))
            return false;

        foreach (KeyValuePair<string, int> requirement in
                 descriptor.MaterialRequirements.OrderBy(
                     value => value.Key,
                     StringComparer.Ordinal))
        {
            if (!deliveries.TryRequestItemDelivery(
                    requirement.Key,
                    requirement.Value,
                    descriptor.Position,
                    descriptor.DestinationId,
                    out int requested,
                    out string requestFailure)
                || requested < requirement.Value)
            {
                failureReason = string.IsNullOrWhiteSpace(requestFailure)
                    ? "equipment-evolution-input-material-delivery-incomplete:"
                        + requirement.Key
                    : requestFailure;
                return false;
            }
        }

        if (!deliveries.TryRequestStackDelivery(
                descriptor.EquipmentSourceStackId,
                1,
                descriptor.Position,
                descriptor.DestinationId,
                out int requestedEquipment,
                out string equipmentFailure)
            || requestedEquipment != 1)
        {
            failureReason = string.IsNullOrWhiteSpace(equipmentFailure)
                ? "equipment-evolution-input-equipment-delivery-incomplete"
                : equipmentFailure;
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryClose(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (descriptor == null || string.IsNullOrWhiteSpace(reasonCode))
        {
            failureReason = "equipment-evolution-input-close-invalid";
            return false;
        }
        if (!releases.TryReleaseAtOwnerPosition(
                descriptor.DestinationId,
                descriptor.Position,
                reasonCode,
                out _,
                out failureReason)
            || !TryCaptureOwnedPairs(
                out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
        {
            return false;
        }

        FacilityBufferDestinationClaim[] matchingClaims = ownedClaims
            .Where(value => string.Equals(
                value.DestinationId,
                descriptor.DestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = ownedProfiles
            .Where(value => string.Equals(
                value.DestinationId,
                descriptor.DestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length == 0 && matchingProfiles.Length == 0)
            return true;
        if (matchingClaims.Length != 1
            || matchingProfiles.Length != 1
            || !PairMatches(
                descriptor,
                matchingClaims[0],
                matchingProfiles[0],
                descriptor.StoredCapacityGrams))
        {
            failureReason = "equipment-evolution-input-close-pair-invalid:"
                + descriptor.OrderId;
            return false;
        }
        ownedClaims.Remove(matchingClaims[0]);
        ownedProfiles.Remove(matchingProfiles[0]);
        return lifecycle.TryReplaceOwnedAuthorities(
            EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
            ownedClaims,
            ownedProfiles,
            out failureReason);
    }

    public bool TryValidateAuthority(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        if (!TryProject(
                descriptor,
                out EquipmentEvolutionInputOwnerProjection projection,
                out failureReason))
        {
            return false;
        }
        if (descriptor.StoredCapacityGrams != projection.CapacityGrams
            || descriptor.StoredMassAuthorityRevision
                != projection.MassAuthorityRevision
            || !string.Equals(
                descriptor.StoredCapacityFingerprint,
                projection.CapacityFingerprint,
                StringComparison.Ordinal))
        {
            failureReason = "equipment-evolution-input-stored-projection-invalid:"
                + descriptor.OrderId;
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
            failureReason = "equipment-evolution-input-authority-mismatch:"
                + descriptor.OrderId;
            return false;
        }
        return true;
    }

    public bool TryReplaceForRestore(
        IReadOnlyList<EquipmentEvolutionInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        failureReason = string.Empty;
        EquipmentEvolutionInputOwnerDescriptor[] ordered = (descriptors
                ?? Array.Empty<EquipmentEvolutionInputOwnerDescriptor>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.OrderId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || ordered.Select(value => value.DestinationId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason = "equipment-evolution-input-restore-owner-set-invalid";
            return false;
        }

        List<FacilityBufferDestinationClaim> desiredClaims = new(ordered.Length);
        List<FacilityBufferCapacityProfile> desiredProfiles = new(ordered.Length);
        foreach (EquipmentEvolutionInputOwnerDescriptor descriptor in ordered)
        {
            if (!TryProject(
                    descriptor,
                    out EquipmentEvolutionInputOwnerProjection projection,
                    out failureReason)
                || descriptor.StoredCapacityGrams != projection.CapacityGrams
                || descriptor.StoredMassAuthorityRevision
                    != projection.MassAuthorityRevision
                || !string.Equals(
                    descriptor.StoredCapacityFingerprint,
                    projection.CapacityFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = failureReason.Length == 0
                    ? "equipment-evolution-input-restore-projection-invalid:"
                        + descriptor.OrderId
                    : failureReason;
                return false;
            }
            desiredClaims.Add(CreateClaim(descriptor));
            desiredProfiles.Add(CreateProfile(
                descriptor,
                projection.CapacityGrams));
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
            desiredClaims,
            desiredProfiles,
            out failureReason);
    }

    private bool TryProject(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        out EquipmentEvolutionInputOwnerProjection projection,
        out string failureReason)
    {
        projection = default;
        failureReason = string.Empty;
        if (descriptor == null
            || !EquipmentEvolutionInputOwnerAuthority.IsExpectedDestination(
                descriptor.OrderId,
                descriptor.DestinationId)
            || !TryFindLiveFacility(descriptor, out _))
        {
            failureReason = "equipment-evolution-input-live-facility-invalid";
            return false;
        }

        WorldItemStackSnapshot[] equipmentStacks = deliveries.GetAllStacks()
            .Where(value => value != null && string.Equals(
                value.StackId,
                descriptor.EquipmentSourceStackId,
                StringComparison.Ordinal))
            .ToArray();
        if (equipmentStacks.Length != 1)
        {
            failureReason = "equipment-evolution-input-equipment-stack-cardinality:"
                + equipmentStacks.Length;
            return false;
        }
        WorldItemStackSnapshot equipmentStack = equipmentStacks[0];
        string[] expectedComponents = descriptor.EquipmentComponents
            .Select(value => value.ToCanonicalString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualComponents = (equipmentStack.Components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(value => value != null)
            .Select(value => value.ToCanonicalString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (equipmentStack.Quantity != 1
            || !string.Equals(
                equipmentStack.ItemId,
                descriptor.EquipmentItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                equipmentStack.ItemInstanceId,
                descriptor.EquipmentInstanceId,
                StringComparison.Ordinal)
            || !actualComponents.SequenceEqual(
                expectedComponents,
                StringComparer.Ordinal))
        {
            failureReason = "equipment-evolution-input-equipment-custody-invalid";
            return false;
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-equipment-evolution-input-capacity@1");
        digest.Append(descriptor.OrderId);
        digest.Append(descriptor.DestinationId);
        digest.Append(descriptor.FacilityPersistentId);
        digest.Append(descriptor.Position.x);
        digest.Append(descriptor.Position.y);
        digest.Append(descriptor.EquipmentInstanceId);
        digest.Append(descriptor.EquipmentSourceStackId);
        digest.Append(descriptor.EquipmentItemId);
        digest.Append(mass.AuthorityRevision);
        long capacity;
        try
        {
            PhysicalItemMassSubject equipmentSubject =
                PhysicalItemMassSubjectAdapter.Create(
                    mass,
                    (ItemDefinitionId)descriptor.EquipmentItemId,
                    descriptor.EquipmentInstanceId,
                    descriptor.EquipmentComponents);
            long equipmentMass = mass.GetQuantityMass(
                (ItemDefinitionId)descriptor.EquipmentItemId,
                equipmentSubject,
                1).Value;
            if (equipmentMass <= 0L)
            {
                failureReason = "equipment-evolution-input-equipment-mass-not-positive";
                return false;
            }
            capacity = equipmentMass;
            digest.Append(equipmentMass);
            foreach (string component in expectedComponents)
                digest.Append(component);

            if (descriptor.MaterialRequirements.Count == 0)
            {
                failureReason = "equipment-evolution-input-materials-empty";
                return false;
            }
            foreach (KeyValuePair<string, int> requirement in
                     descriptor.MaterialRequirements.OrderBy(
                         value => value.Key,
                         StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(requirement.Key)
                    || !string.Equals(
                        requirement.Key,
                        requirement.Key.Trim(),
                        StringComparison.Ordinal)
                    || requirement.Value <= 0)
                {
                    failureReason = "equipment-evolution-input-material-invalid";
                    return false;
                }
                ItemDefinitionId itemId = (ItemDefinitionId)requirement.Key;
                PhysicalItemMassSubject subject =
                    PhysicalItemMassSubjectAdapter.Create(
                        mass,
                        itemId,
                        string.Empty,
                        Array.Empty<ItemInstanceComponentSaveData>());
                long lineMass = mass.GetQuantityMass(
                    itemId,
                    subject,
                    requirement.Value).Value;
                if (lineMass <= 0L)
                {
                    failureReason =
                        "equipment-evolution-input-material-mass-not-positive:"
                        + requirement.Key;
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
            failureReason = "equipment-evolution-input-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        if (capacity <= 0L)
        {
            failureReason = "equipment-evolution-input-capacity-not-positive";
            return false;
        }
        digest.Append(capacity);
        projection = new EquipmentEvolutionInputOwnerProjection(
            capacity,
            mass.AuthorityRevision,
            digest.ComputeSha256());
        return true;
    }

    private bool TryFindLiveFacility(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        out BuildableObject facility)
    {
        BuildableObject[] matches = (buildings.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null
                && !value.isDestroy
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    descriptor.FacilityPersistentId,
                    StringComparison.Ordinal))
            .ToArray();
        facility = matches.Length == 1 ? matches[0] : null;
        return facility != null
            && facility.centerPos == descriptor.Position
            && facility.BuildingData?
                .GetAbility<BuildingEquipmentCraftingAbility>() != null;
    }

    private bool TryCaptureOwnedPairs(
        out List<FacilityBufferDestinationClaim> ownedClaims,
        out List<FacilityBufferCapacityProfile> ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        if (ownedClaims.Count != ownedProfiles.Count
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason = "equipment-evolution-input-owner-set-torn";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static FacilityBufferDestinationClaim CreateClaim(
        EquipmentEvolutionInputOwnerDescriptor descriptor) => new(
        descriptor.DestinationId,
        descriptor.Position,
        EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
        descriptor.OrderId,
        descriptor.FacilityPersistentId,
        FacilityBufferDestinationAnchorKind.LiveFacility,
        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);

    private static FacilityBufferCapacityProfile CreateProfile(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        long capacityGrams) => new(
        descriptor.DestinationId,
        descriptor.Position,
        EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
        descriptor.OrderId,
        descriptor.FacilityPersistentId,
        new PhysicalMassGrams(capacityGrams),
        EquipmentEvolutionInputOwnerAuthority.CapacitySchemaRevision);

    private static bool PairMatches(
        EquipmentEvolutionInputOwnerDescriptor descriptor,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile,
        long capacityGrams) => descriptor != null
        && claim != null
        && profile != null
        && string.Equals(
            claim.DestinationId,
            descriptor.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.DestinationId,
            descriptor.DestinationId,
            StringComparison.Ordinal)
        && claim.DropPosition == descriptor.Position
        && profile.DropPosition == descriptor.Position
        && string.Equals(
            claim.OwnerDomain,
            EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerDomain,
            EquipmentEvolutionInputOwnerAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerOperationId,
            descriptor.OrderId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            descriptor.OrderId,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerFacilityId,
            descriptor.FacilityPersistentId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            descriptor.FacilityPersistentId,
            StringComparison.Ordinal)
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.AdmissionPolicy
            == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && profile.MaxMassGrams == capacityGrams
        && profile.CapacityRevision
            == EquipmentEvolutionInputOwnerAuthority.CapacitySchemaRevision;
}
