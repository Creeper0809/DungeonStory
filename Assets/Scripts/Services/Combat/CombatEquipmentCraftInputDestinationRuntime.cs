using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface ICombatEquipmentCraftInputDestinationRuntime
{
    bool TryOpen(
        CombatEquipmentCraftOrderSaveData order,
        BuildableObject facility,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason);

    bool TryRequest(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason);

    bool TryClear(
        CombatEquipmentCraftOrderSaveData order,
        string reasonCode,
        out string failureReason);

    bool TryClose(
        CombatEquipmentCraftOrderSaveData order,
        string reasonCode,
        out string failureReason);

    bool TryValidateProjection(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason);

    bool TryValidateAuthority(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason);

    bool TryReplace(
        IReadOnlyList<CombatEquipmentCraftInputDestinationProjection> desired,
        out string failureReason);
}

public sealed class CombatEquipmentCraftInputDestinationProjection
{
    public CombatEquipmentCraftInputDestinationProjection(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements)
    {
        Order = order ?? throw new ArgumentNullException(nameof(order));
        Requirements = requirements
            ?? throw new ArgumentNullException(nameof(requirements));
    }

    public CombatEquipmentCraftOrderSaveData Order { get; }
    public IReadOnlyDictionary<string, int> Requirements { get; }
}

internal static class CombatEquipmentCraftInputDestinationAuthority
{
    internal const string OwnerDomain = "combat.equipment-crafting";
    internal const long CapacitySchemaRevision = 1L;

    internal static string FormatDestinationId(string orderId) =>
        ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
        + OwnerDomain + ":" + (orderId ?? string.Empty);
}

/// <summary>
/// Owns the exact claim and positive gram profile for one combat-crafting
/// material destination. The recipe/catalog remains the input authority; this
/// adapter only freezes the current physical mass projection and delegates
/// physical delivery/release to the shared Items runtime.
/// </summary>
public sealed class CombatEquipmentCraftInputDestinationRuntime :
    ICombatEquipmentCraftInputDestinationRuntime
{
    private readonly IPhysicalItemMassQuery mass;
    private readonly IEquipmentPhysicalItemGateway items;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public CombatEquipmentCraftInputDestinationRuntime(
        IPhysicalItemMassQuery mass,
        IEquipmentPhysicalItemGateway items,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryOpen(
        CombatEquipmentCraftOrderSaveData order,
        BuildableObject facility,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || facility == null)
        {
            failureReason = "combat-craft-input-owner-or-facility-missing";
            return false;
        }
        string facilityId = facility.RequirePersistentInstanceId().Value;
        Vector2Int position = facility.centerPos;
        long previousCapacity = order.materialBufferCapacityGrams;
        long previousRevision = order.materialMassAuthorityRevision;
        string previousFingerprint = order.materialCapacityFingerprint;
        try
        {
            if (!TryProject(
                    order,
                    facilityId,
                    position,
                    requirements,
                    out long capacity,
                    out string fingerprint,
                    out failureReason))
            {
                return false;
            }
            order.materialBufferCapacityGrams = capacity;
            order.materialMassAuthorityRevision = mass.AuthorityRevision;
            order.materialCapacityFingerprint = fingerprint;

            if (!TryCaptureOwnedPairs(
                    out List<FacilityBufferDestinationClaim> ownedClaims,
                    out List<FacilityBufferCapacityProfile> ownedProfiles,
                    out failureReason))
            {
                return false;
            }
            if (ownedClaims.Any(value => string.Equals(
                    value.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal)))
            {
                failureReason = "combat-craft-input-destination-duplicate:"
                    + order.materialDestinationId;
                return false;
            }
            ownedClaims.Add(CreateClaim(order, position));
            ownedProfiles.Add(CreateProfile(order, position));
            if (lifecycle.TryReplaceOwnedAuthorities(
                    CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
                    ownedClaims,
                    ownedProfiles,
                    out failureReason))
            {
                return true;
            }
            failureReason = "combat-craft-input-authority-publish-failed:"
                + failureReason;
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "combat-craft-input-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        finally
        {
            if (failureReason.Length != 0)
            {
                order.materialBufferCapacityGrams = previousCapacity;
                order.materialMassAuthorityRevision = previousRevision;
                order.materialCapacityFingerprint = previousFingerprint
                    ?? string.Empty;
            }
        }
    }

    public bool TryRequest(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryValidateProjection(order, requirements, out failureReason)
            || !TryFindOwnedPair(order, out _, out _, out failureReason))
        {
            return false;
        }

        Vector2Int position = new(order.destinationX, order.destinationY);
        foreach (KeyValuePair<string, int> requirement in requirements
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!items.TryRequestItemDelivery(
                    requirement.Key,
                    requirement.Value,
                    position,
                    order.materialDestinationId,
                    out int requested,
                    out string requestFailure)
                || requested < requirement.Value)
            {
                failureReason = string.IsNullOrWhiteSpace(requestFailure)
                    ? "combat-craft-input-delivery-incomplete:"
                        + requirement.Key
                    : requestFailure;
                return false;
            }
        }
        return true;
    }

    public bool TryClear(
        CombatEquipmentCraftOrderSaveData order,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || string.IsNullOrWhiteSpace(order.materialDestinationId))
        {
            failureReason = "combat-craft-input-clear-owner-invalid";
            return false;
        }
        return releases.TryReleaseAtOwnerPosition(
            order.materialDestinationId,
            new Vector2Int(order.destinationX, order.destinationY),
            reasonCode,
            out _,
            out failureReason);
    }

    public bool TryClose(
        CombatEquipmentCraftOrderSaveData order,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || !TryClear(order, reasonCode, out failureReason)
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
                order.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = ownedProfiles
            .Where(value => string.Equals(
                value.DestinationId,
                order.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length == 0 && matchingProfiles.Length == 0)
            return true;
        if (matchingClaims.Length != 1
            || matchingProfiles.Length != 1
            || !PairMatches(order, matchingClaims[0], matchingProfiles[0]))
        {
            failureReason = "combat-craft-input-close-pair-invalid:"
                + order.orderId;
            return false;
        }
        ownedClaims.Remove(matchingClaims[0]);
        ownedProfiles.Remove(matchingProfiles[0]);
        return lifecycle.TryReplaceOwnedAuthorities(
            CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
            ownedClaims,
            ownedProfiles,
            out failureReason);
    }

    public bool TryValidateProjection(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || !TryProject(
                order,
                order.facilityPersistentId,
                new Vector2Int(order.destinationX, order.destinationY),
                requirements,
                out long capacity,
                out string fingerprint,
                out failureReason))
        {
            return false;
        }
        if (order.materialBufferCapacityGrams != capacity
            || order.materialMassAuthorityRevision != mass.AuthorityRevision
            || !string.Equals(
                order.materialCapacityFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            failureReason = "combat-craft-input-stored-projection-invalid:"
                + order.orderId;
            return false;
        }
        return true;
    }

    public bool TryValidateAuthority(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason) =>
        TryValidateProjection(order, requirements, out failureReason)
        && TryFindOwnedPair(order, out _, out _, out failureReason);

    public bool TryReplace(
        IReadOnlyList<CombatEquipmentCraftInputDestinationProjection> desired,
        out string failureReason)
    {
        failureReason = string.Empty;
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (CombatEquipmentCraftInputDestinationProjection projection in
                 (desired
                     ?? Array.Empty<
                         CombatEquipmentCraftInputDestinationProjection>())
                 .OrderBy(value => value?.Order?.orderId, StringComparer.Ordinal))
        {
            if (projection?.Order == null
                || !TryValidateProjection(
                    projection.Order,
                    projection.Requirements,
                    out failureReason))
            {
                failureReason = failureReason.Length == 0
                    ? "combat-craft-input-restore-projection-invalid"
                    : failureReason;
                return false;
            }
            Vector2Int position = new(
                projection.Order.destinationX,
                projection.Order.destinationY);
            desiredClaims.Add(CreateClaim(projection.Order, position));
            desiredProfiles.Add(CreateProfile(projection.Order, position));
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
            desiredClaims,
            desiredProfiles,
            out failureReason);
    }

    private bool TryProject(
        CombatEquipmentCraftOrderSaveData order,
        string facilityId,
        Vector2Int position,
        IReadOnlyDictionary<string, int> requirements,
        out long capacity,
        out string fingerprint,
        out string failureReason)
    {
        capacity = 0L;
        fingerprint = string.Empty;
        failureReason = string.Empty;
        if (order == null
            || string.IsNullOrWhiteSpace(order.orderId)
            || string.IsNullOrWhiteSpace(facilityId)
            || !string.Equals(
                order.materialDestinationId,
                CombatEquipmentCraftInputDestinationAuthority
                    .FormatDestinationId(order.orderId),
                StringComparison.Ordinal)
            || !string.Equals(
                order.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal)
            || order.destinationX != position.x
            || order.destinationY != position.y
            || requirements == null
            || requirements.Count == 0)
        {
            failureReason = "combat-craft-input-projection-identity-invalid";
            return false;
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-equipment-craft-input-capacity@1");
        digest.Append(order.orderId);
        digest.Append(facilityId);
        digest.Append(order.materialDestinationId);
        digest.Append(position.x);
        digest.Append(position.y);
        digest.Append(mass.AuthorityRevision);
        try
        {
            foreach (KeyValuePair<string, int> requirement in requirements
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(requirement.Key)
                    || !string.Equals(
                        requirement.Key,
                        requirement.Key.Trim(),
                        StringComparison.Ordinal)
                    || requirement.Value <= 0)
                {
                    failureReason =
                        "combat-craft-input-requirement-invalid";
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
                    failureReason = "combat-craft-input-mass-not-positive:"
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
            failureReason = "combat-craft-input-mass-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        if (capacity <= 0L)
        {
            failureReason = "combat-craft-input-capacity-not-positive";
            return false;
        }
        digest.Append(capacity);
        fingerprint = digest.ComputeSha256();
        return true;
    }

    private bool TryFindOwnedPair(
        CombatEquipmentCraftOrderSaveData order,
        out FacilityBufferDestinationClaim claim,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        claim = null;
        profile = null;
        failureReason = string.Empty;
        FacilityBufferDestinationClaim[] matchingClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                order?.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                order?.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length != 1 || matchingProfiles.Length != 1)
        {
            failureReason = "combat-craft-input-pair-cardinality:"
                + matchingClaims.Length + ":" + matchingProfiles.Length;
            return false;
        }
        claim = matchingClaims[0];
        profile = matchingProfiles[0];
        if (PairMatches(order, claim, profile))
            return true;
        claim = null;
        profile = null;
        failureReason = "combat-craft-input-pair-mismatch:"
            + (order?.orderId ?? "<null>");
        return false;
    }

    private bool TryCaptureOwnedPairs(
        out List<FacilityBufferDestinationClaim> ownedClaims,
        out List<FacilityBufferCapacityProfile> ownedProfiles,
        out string failureReason)
    {
        failureReason = string.Empty;
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        if (ownedClaims.Count != ownedProfiles.Count
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason = "combat-craft-input-owner-set-torn";
            return false;
        }
        return true;
    }

    private static FacilityBufferDestinationClaim CreateClaim(
        CombatEquipmentCraftOrderSaveData order,
        Vector2Int position) => new(
        order.materialDestinationId,
        position,
        CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
        order.orderId,
        order.facilityPersistentId,
        FacilityBufferDestinationAnchorKind.LiveFacility,
        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);

    private static FacilityBufferCapacityProfile CreateProfile(
        CombatEquipmentCraftOrderSaveData order,
        Vector2Int position) => new(
        order.materialDestinationId,
        position,
        CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
        order.orderId,
        order.facilityPersistentId,
        new PhysicalMassGrams(order.materialBufferCapacityGrams),
        CombatEquipmentCraftInputDestinationAuthority.CapacitySchemaRevision);

    private static bool PairMatches(
        CombatEquipmentCraftOrderSaveData order,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) => order != null
        && claim != null
        && profile != null
        && string.Equals(
            claim.DestinationId,
            order.materialDestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.DestinationId,
            order.materialDestinationId,
            StringComparison.Ordinal)
        && claim.DropPosition == profile.DropPosition
        && claim.DropPosition == new Vector2Int(
            order.destinationX,
            order.destinationY)
        && string.Equals(
            claim.OwnerDomain,
            CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerDomain,
            CombatEquipmentCraftInputDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerOperationId,
            order.orderId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            order.orderId,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerFacilityId,
            order.facilityPersistentId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            order.facilityPersistentId,
            StringComparison.Ordinal)
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.AdmissionPolicy
            == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && profile.MaxMassGrams == order.materialBufferCapacityGrams
        && profile.CapacityRevision
            == CombatEquipmentCraftInputDestinationAuthority
                .CapacitySchemaRevision;
}
