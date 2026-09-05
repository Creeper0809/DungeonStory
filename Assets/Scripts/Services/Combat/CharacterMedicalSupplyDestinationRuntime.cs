using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface ICharacterMedicalSupplyDestinationRuntime
{
    bool TryEnsure(
        CharacterMedicalOrder order,
        BuildableObject facility,
        out string failureReason);

    bool TryReplace(
        IReadOnlyList<CharacterMedicalOrder> orders,
        IReadOnlyDictionary<string, Vector2Int> facilityPositions,
        out string failureReason);

    bool TryRevoke(CharacterMedicalOrder order, out string failureReason);
    bool TryValidate(CharacterMedicalOrder order, out string failureReason);
}

internal static class CharacterMedicalSupplyDestinationAuthority
{
    internal const string OwnerDomain = "medical.character-supply";
    internal const long CapacitySchemaRevision = 1L;

    internal static string FormatOwnerStableId(string orderId) =>
        "character-medical-order:" + (orderId ?? string.Empty);

    internal static string FormatOwnerOperationId(
        string orderId,
        int destinationSequence) =>
        "character-medical-supply-destination:" + (orderId ?? string.Empty)
        + $":{destinationSequence:D8}";

    internal static string FormatParentOperationId(
        string orderId,
        int destinationSequence) =>
        "character-medical-supply-drain:" + (orderId ?? string.Empty)
        + $":{destinationSequence:D8}";

    internal static string FormatStepOperationId(
        string orderId,
        int destinationSequence) =>
        FormatParentOperationId(orderId, destinationSequence) + ":custody";

    internal static string FormatDestinationId(
        string orderId,
        int destinationSequence) =>
        ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
        + "medical.character-supply:" + (orderId ?? string.Empty)
        + $":{destinationSequence:D8}";
}

/// <summary>
/// Projects one character-medical order into the owner-neutral exact-gram
/// FacilityBuffer authority. The eligible supply set is capability-derived;
/// no medicine content ID is dispatched in this adapter.
/// </summary>
internal sealed class CharacterMedicalSupplyDestinationRuntime :
    ICharacterMedicalSupplyDestinationRuntime
{
    private readonly IResourceEconomyContentCatalog content;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;

    internal CharacterMedicalSupplyDestinationRuntime(
        IResourceEconomyContentCatalog content,
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    [GameplayInternalOnly(
        "Publishes or validates the exact one-item medical supply destination before delivery.",
        "CharacterMedicalSupplyCoordinator only")]
    public bool TryEnsure(
        CharacterMedicalOrder order,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || facility == null)
        {
            failureReason = "character-medical-supply-authority-owner-missing";
            return false;
        }

        string facilityId = facility.RequirePersistentInstanceId().Value;
        if (order.treatmentBufferCapacityGrams > 0L
            || order.treatmentMassAuthorityRevision > 0L
            || !string.IsNullOrEmpty(order.treatmentCapacityFingerprint))
        {
            return TryValidateProjection(order, facilityId, out failureReason)
                && TryFindOwnedPair(order, facility.centerPos, out _, out _,
                    out failureReason);
        }

        long previousCapacity = order.treatmentBufferCapacityGrams;
        long previousRevision = order.treatmentMassAuthorityRevision;
        string previousFingerprint = order.treatmentCapacityFingerprint;
        try
        {
            if (!TryProject(order, facilityId, out long capacity,
                    out string fingerprint, out failureReason))
            {
                return false;
            }

            order.treatmentBufferCapacityGrams = capacity;
            order.treatmentMassAuthorityRevision = massQuery.AuthorityRevision;
            order.treatmentCapacityFingerprint = fingerprint;
            FacilityBufferDestinationClaim claim = CreateClaim(
                order,
                facilityId,
                facility.centerPos);
            FacilityBufferCapacityProfile profile = CreateProfile(
                order,
                facilityId,
                facility.centerPos);
            if (!TryCaptureOwnedPairs(
                    out List<FacilityBufferDestinationClaim> ownedClaims,
                    out List<FacilityBufferCapacityProfile> ownedProfiles,
                    out failureReason))
            {
                return false;
            }
            if (ownedClaims.Any(value => string.Equals(
                    value.DestinationId,
                    claim.DestinationId,
                    StringComparison.Ordinal)))
            {
                failureReason =
                    "character-medical-supply-authority-destination-duplicate:"
                    + claim.DestinationId;
                return false;
            }

            ownedClaims.Add(claim);
            ownedProfiles.Add(profile);
            if (lifecycle.TryReplaceOwnedAuthorities(
                    CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
                    ownedClaims,
                    ownedProfiles,
                    out failureReason))
            {
                return true;
            }
            failureReason = "character-medical-supply-authority-publish-failed:"
                + failureReason;
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "character-medical-supply-authority-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(failureReason))
            {
                order.treatmentBufferCapacityGrams = previousCapacity;
                order.treatmentMassAuthorityRevision = previousRevision;
                order.treatmentCapacityFingerprint = previousFingerprint
                    ?? string.Empty;
            }
        }
    }

    public bool TryReplace(
        IReadOnlyList<CharacterMedicalOrder> orders,
        IReadOnlyDictionary<string, Vector2Int> facilityPositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (CharacterMedicalOrder order in
                 (orders ?? Array.Empty<CharacterMedicalOrder>())
                     .Where(value => value != null
                         && !string.IsNullOrEmpty(
                             value.treatmentMaterialDestinationId))
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            CharacterMedicalSupplyDestinationDrainJoinData[] activeJoins =
                (order.treatmentDestinationDrainJoins
                    ?? new List<
                        CharacterMedicalSupplyDestinationDrainJoinData>())
                .Where(value => value != null
                    && value.phase !=
                        CharacterMedicalSupplyDestinationDrainPhase
                            .ClosedAwaitingCheckpointGc)
                .ToArray();
            if (activeJoins.Length > 1)
            {
                failureReason =
                    "character-medical-supply-authority-active-drain-cardinality:"
                    + order.orderId;
                return false;
            }
            Vector2Int position;
            if (activeJoins.Length == 1)
            {
                CharacterMedicalSupplyDestinationDrainJoinData active =
                    activeJoins[0];
                if (!string.Equals(
                        active.ownerFacilityId,
                        order.treatmentFacilityId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        active.sourceDestinationId,
                        order.treatmentMaterialDestinationId,
                        StringComparison.Ordinal))
                {
                    failureReason =
                        "character-medical-supply-authority-active-drain-drift:"
                        + order.orderId;
                    return false;
                }
                position = new Vector2Int(active.ownerX, active.ownerY);
            }
            else if (!facilityPositions.TryGetValue(
                         order.treatmentFacilityId,
                         out position))
            {
                failureReason =
                    "character-medical-supply-authority-facility-missing:"
                    + order.orderId;
                return false;
            }
            if (!TryValidateProjection(
                    order,
                    order.treatmentFacilityId,
                    out failureReason))
            {
                return false;
            }
            desiredClaims.Add(CreateClaim(
                order,
                order.treatmentFacilityId,
                position));
            desiredProfiles.Add(CreateProfile(
                order,
                order.treatmentFacilityId,
                position));
        }

        return lifecycle.TryReplaceOwnedAuthorities(
            CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
            desiredClaims,
            desiredProfiles,
            out failureReason);
    }

    public bool TryRevoke(
        CharacterMedicalOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
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
                order.treatmentMaterialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = ownedProfiles
            .Where(value => string.Equals(
                value.DestinationId,
                order.treatmentMaterialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length == 0 && matchingProfiles.Length == 0)
        {
            return true;
        }
        if (matchingClaims.Length != 1 || matchingProfiles.Length != 1)
        {
            failureReason = "character-medical-supply-authority-pair-cardinality:"
                + order.orderId;
            return false;
        }
        FacilityBufferDestinationClaim removedClaim = matchingClaims[0];
        FacilityBufferCapacityProfile removedProfile = matchingProfiles[0];
        if (!PairMatches(
                order,
                removedClaim.DropPosition,
                removedClaim,
                removedProfile))
        {
            failureReason =
                "character-medical-supply-authority-revoke-pair-mismatch:"
                + order.orderId;
            return false;
        }
        ownedClaims.Remove(removedClaim);
        ownedProfiles.Remove(removedProfile);
        return lifecycle.TryReplaceOwnedAuthorities(
            CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
            ownedClaims,
            ownedProfiles,
            out failureReason);
    }

    public bool TryValidate(
        CharacterMedicalOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || string.IsNullOrEmpty(order.treatmentFacilityId))
        {
            failureReason = "character-medical-supply-authority-order-invalid";
            return false;
        }
        FacilityBufferDestinationClaim[] matches = claims
            .CaptureAuthorityClaims()
            .Where(value => value != null
                && string.Equals(
                    value.DestinationId,
                    order.treatmentMaterialDestinationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            failureReason = "character-medical-supply-authority-claim-cardinality:"
                + matches.Length;
            return false;
        }
        return TryFindOwnedPair(
            order,
            matches[0].DropPosition,
            out _,
            out _,
            out failureReason);
    }

    private bool TryValidateProjection(
        CharacterMedicalOrder order,
        string facilityId,
        out string failureReason)
    {
        if (!TryProject(order, facilityId, out long capacity,
                out string fingerprint, out failureReason))
        {
            return false;
        }
        if (order.treatmentBufferCapacityGrams != capacity
            || order.treatmentMassAuthorityRevision != massQuery.AuthorityRevision
            || !string.Equals(
                order.treatmentCapacityFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "character-medical-supply-authority-stored-projection-invalid:"
                + order.orderId;
            return false;
        }
        return true;
    }

    private bool TryProject(
        CharacterMedicalOrder order,
        string facilityId,
        out long capacity,
        out string fingerprint,
        out string failureReason)
    {
        capacity = 0L;
        fingerprint = string.Empty;
        failureReason = string.Empty;
        if (order == null
            || string.IsNullOrWhiteSpace(order.orderId)
            || order.treatmentDestinationSequence <= 0
            || string.IsNullOrWhiteSpace(facilityId)
            || !string.Equals(
                order.treatmentMaterialDestinationId,
                CharacterMedicalSupplyDestinationAuthority
                    .FormatDestinationId(
                        order.orderId,
                        order.treatmentDestinationSequence),
                StringComparison.Ordinal))
        {
            failureReason = "character-medical-supply-authority-identity-invalid";
            return false;
        }

        string[] itemIds = content.Items
            .Where(value => value != null
                && value.Kind == ResourceItemKind.Medicine
                && value.SupportsInjuryTreatment)
            .Select(value => value.ItemId)
            .Append(CharacterMedicalSupplyCoordinator.ExtractedBloodItemId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (itemIds.Length == 0)
        {
            failureReason = "character-medical-supply-authority-catalog-empty";
            return false;
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("character-medical-supply-capacity-v1");
        digest.Append(order.orderId);
        digest.Append(order.treatmentDestinationSequence);
        digest.Append(facilityId);
        digest.Append(order.treatmentMaterialDestinationId);
        digest.Append(massQuery.AuthorityRevision);
        foreach (string itemId in itemIds)
        {
            PhysicalMassGrams unitMass = massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)itemId);
            if (unitMass.Value <= 0L)
            {
                failureReason =
                    "character-medical-supply-authority-mass-not-positive:"
                    + itemId;
                return false;
            }
            capacity = Math.Max(capacity, unitMass.Value);
            digest.Append(itemId);
            digest.Append(unitMass.Value);
        }
        digest.Append(capacity);
        fingerprint = digest.ComputeSha256();
        return capacity > 0L;
    }

    private bool TryFindOwnedPair(
        CharacterMedicalOrder order,
        Vector2Int position,
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
                order.treatmentMaterialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                order.treatmentMaterialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length != 1 || matchingProfiles.Length != 1)
        {
            failureReason = "character-medical-supply-authority-pair-cardinality:"
                + matchingClaims.Length + ":" + matchingProfiles.Length;
            return false;
        }
        claim = matchingClaims[0];
        profile = matchingProfiles[0];
        if (!PairMatches(order, position, claim, profile))
        {
            claim = null;
            profile = null;
            failureReason = "character-medical-supply-authority-pair-mismatch:"
                + order.orderId;
            return false;
        }
        return true;
    }

    private bool TryCaptureOwnedPairs(
        out List<FacilityBufferDestinationClaim> ownedClaims,
        out List<FacilityBufferCapacityProfile> ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        failureReason = string.Empty;
        if (ownedClaims.Count != ownedProfiles.Count
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason =
                "character-medical-supply-authority-owner-set-mismatch";
            return false;
        }
        return true;
    }

    private static FacilityBufferDestinationClaim CreateClaim(
        CharacterMedicalOrder order,
        string facilityId,
        Vector2Int position) => new(
        order.treatmentMaterialDestinationId,
        position,
        CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
        CharacterMedicalSupplyDestinationAuthority.FormatOwnerOperationId(
            order.orderId,
            order.treatmentDestinationSequence),
        facilityId,
        FacilityBufferDestinationAnchorKind.LiveFacility,
        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);

    private static FacilityBufferCapacityProfile CreateProfile(
        CharacterMedicalOrder order,
        string facilityId,
        Vector2Int position) => new(
        order.treatmentMaterialDestinationId,
        position,
        CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
        CharacterMedicalSupplyDestinationAuthority.FormatOwnerOperationId(
            order.orderId,
            order.treatmentDestinationSequence),
        facilityId,
        new PhysicalMassGrams(order.treatmentBufferCapacityGrams),
        CharacterMedicalSupplyDestinationAuthority.CapacitySchemaRevision);

    private static bool PairMatches(
        CharacterMedicalOrder order,
        Vector2Int position,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) =>
        claim != null
        && profile != null
        && claim.DropPosition == position
        && profile.DropPosition == position
        && string.Equals(claim.DestinationId, order.treatmentMaterialDestinationId,
            StringComparison.Ordinal)
        && string.Equals(profile.DestinationId,
            order.treatmentMaterialDestinationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerDomain,
            CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerDomain,
            CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId,
            CharacterMedicalSupplyDestinationAuthority.FormatOwnerOperationId(
                order.orderId,
                order.treatmentDestinationSequence),
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerOperationId,
            CharacterMedicalSupplyDestinationAuthority.FormatOwnerOperationId(
                order.orderId,
                order.treatmentDestinationSequence),
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, order.treatmentFacilityId,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, order.treatmentFacilityId,
            StringComparison.Ordinal)
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.AdmissionPolicy
            == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && profile.MaxMassGrams == order.treatmentBufferCapacityGrams
        && profile.CapacityRevision
            == CharacterMedicalSupplyDestinationAuthority
                .CapacitySchemaRevision;
}
