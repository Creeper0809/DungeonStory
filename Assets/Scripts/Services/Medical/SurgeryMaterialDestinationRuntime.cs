using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface ISurgeryMaterialDestinationRuntime
{
    bool TryClaim(
        SurgeryOrder order,
        BuildableObject facility,
        out string failureReason);

    bool TryReplace(
        IReadOnlyList<SurgeryOrder> orders,
        IReadOnlyDictionary<string, Vector2Int> facilityPositions,
        out string failureReason);

    bool TryRevoke(SurgeryOrder order, out string failureReason);
    bool TryValidate(SurgeryOrder order, out string failureReason);
}

/// <summary>
/// Medical adapter over the owner-neutral facility-buffer claim/capacity
/// lifecycle. Procedure and item definitions remain data; this adapter only
/// projects the complete physical payload of one active surgery order.
/// </summary>
public sealed class SurgeryMaterialDestinationRuntime :
    ISurgeryMaterialDestinationRuntime
{
    private readonly ISurgicalPartRuntime parts;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;

    public SurgeryMaterialDestinationRuntime(
        ISurgicalPartRuntime parts,
        IWorldItemStackRuntime items,
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle)
    {
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        if (!ReferenceEquals(items.MassQuery, massQuery))
        {
            throw new ArgumentException(
                "Surgery destinations must use the world-item mass authority.",
                nameof(massQuery));
        }
    }

    public bool TryClaim(
        SurgeryOrder order,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || facility == null)
        {
            failureReason = "surgery-material-authority-order-or-facility-missing";
            return false;
        }

        long priorCapacity = order.materialBufferCapacityGrams;
        long priorRevision = order.materialMassAuthorityRevision;
        string priorFingerprint = order.materialCapacityFingerprint;
        try
        {
            if (!TryCaptureCapacity(order, out long capacity, out failureReason))
            {
                return false;
            }
            order.materialBufferCapacityGrams = capacity;
            order.materialMassAuthorityRevision = massQuery.AuthorityRevision;
            order.materialCapacityFingerprint =
                SurgeryMaterialCapacityFingerprint.Create(order);

            FacilityBufferDestinationClaim claim =
                SurgeryMaterialDestinationAuthority.CreateClaim(
                    order,
                    facility.centerPos);
            FacilityBufferCapacityProfile profile = CreateProfile(
                order,
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
                    "surgery-material-authority-destination-duplicate:"
                    + claim.DestinationId;
                return false;
            }
            ownedClaims.Add(claim);
            ownedProfiles.Add(profile);
            if (lifecycle.TryReplaceOwnedAuthorities(
                    SurgeryMaterialDestinationAuthority.OwnerDomain,
                    ownedClaims,
                    ownedProfiles,
                    out failureReason))
            {
                return true;
            }
            failureReason = "surgery-material-authority-claim-failed:"
                + failureReason;
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "surgery-material-authority-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(failureReason))
            {
                order.materialBufferCapacityGrams = priorCapacity;
                order.materialMassAuthorityRevision = priorRevision;
                order.materialCapacityFingerprint = priorFingerprint
                    ?? string.Empty;
            }
        }
    }

    public bool TryReplace(
        IReadOnlyList<SurgeryOrder> orders,
        IReadOnlyDictionary<string, Vector2Int> facilityPositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        SurgeryOrder[] active = (orders ?? Array.Empty<SurgeryOrder>())
            .Where(value => value?.IsActive == true)
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyDictionary<string, Vector2Int> positions = facilityPositions
            ?? new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (SurgeryOrder order in active)
        {
            if (!positions.TryGetValue(order.facilityId, out Vector2Int position)
                || !TryValidateStoredProjection(order, out failureReason))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "surgery-material-authority-restore-facility-missing:"
                        + (order?.orderId ?? "<null>")
                    : failureReason;
                return false;
            }
            desiredClaims.Add(
                SurgeryMaterialDestinationAuthority.CreateClaim(order, position));
            desiredProfiles.Add(CreateProfile(order, position));
        }
        if (lifecycle.TryReplaceOwnedAuthorities(
                SurgeryMaterialDestinationAuthority.OwnerDomain,
                desiredClaims,
                desiredProfiles,
                out failureReason))
        {
            return true;
        }
        failureReason = "surgery-material-authority-restore-failed:"
            + failureReason;
        return false;
    }

    public bool TryRevoke(SurgeryOrder order, out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || !TryValidateStoredProjection(order, out failureReason)
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
        {
            // A crash/retry may observe the exact post-revoke state before the
            // Surgery aggregate records its terminal phase. Absence of both
            // halves is the idempotent committed result; partial absence is
            // still rejected below.
            return true;
        }
        if (matchingClaims.Length != 1
            || matchingProfiles.Length != 1
            || !PairMatches(order, matchingClaims[0], matchingProfiles[0]))
        {
            failureReason = "surgery-material-authority-revoke-pair-invalid:"
                + order.orderId;
            return false;
        }
        FacilityBufferDestinationClaim claim = matchingClaims[0];
        ownedClaims.RemoveAll(value => string.Equals(
            value.DestinationId,
            claim.DestinationId,
            StringComparison.Ordinal));
        ownedProfiles.RemoveAll(value => string.Equals(
            value.DestinationId,
            claim.DestinationId,
            StringComparison.Ordinal));
        if (lifecycle.TryReplaceOwnedAuthorities(
                SurgeryMaterialDestinationAuthority.OwnerDomain,
                ownedClaims,
                ownedProfiles,
                out failureReason))
        {
            return true;
        }
        failureReason = "surgery-material-authority-revoke-failed:"
            + failureReason;
        return false;
    }

    public bool TryValidate(SurgeryOrder order, out string failureReason) =>
        TryFindOwnedPair(order, out _, out _, out failureReason);

    private bool TryCaptureCapacity(
        SurgeryOrder order,
        out long capacityGrams,
        out string failureReason)
    {
        capacityGrams = 0L;
        failureReason = string.Empty;
        if (order == null
            || string.IsNullOrWhiteSpace(order.orderId)
            || order.materials == null)
        {
            failureReason = "surgery-material-capacity-order-invalid";
            return false;
        }
        try
        {
            foreach (IGrouping<string, SurgicalMaterialRequirement> group in
                     order.materials
                         .Where(value => value != null && !value.optional)
                         .GroupBy(value => value.itemId, StringComparer.Ordinal)
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                int quantity = group.Sum(value => Mathf.Max(1, value.quantity));
                PhysicalItemMassSubject subject =
                    PhysicalItemMassSubjectAdapter.Create(
                        massQuery,
                        (ItemDefinitionId)group.Key,
                        string.Empty,
                        Array.Empty<ItemInstanceComponentSaveData>());
                capacityGrams = checked(capacityGrams
                    + massQuery.GetQuantityMass(
                        (ItemDefinitionId)group.Key,
                        subject,
                        quantity).Value);
            }

            HashSet<string> exactStackIds = new(StringComparer.Ordinal);
            if (order.subject?.kind is SurgicalSubjectKind.HumanoidCorpse
                or SurgicalSubjectKind.WildlifeCorpse)
            {
                exactStackIds.Add(order.subject.subjectId);
            }
            if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId))
            {
                if (!parts.TryGet(
                        order.selectedPartInstanceId,
                        out SurgicalPartInstance selected)
                    || string.IsNullOrWhiteSpace(selected.worldStackId))
                {
                    failureReason =
                        "surgery-material-capacity-selected-part-missing";
                    return false;
                }
                exactStackIds.Add(selected.worldStackId);
            }
            WorldItemStackSnapshot[] allStacks = items.GetAllStacks()
                .Where(value => value != null && value.Quantity > 0)
                .ToArray();
            foreach (string stackId in exactStackIds.OrderBy(
                         value => value,
                         StringComparer.Ordinal))
            {
                WorldItemStackSnapshot stack = allStacks.SingleOrDefault(value =>
                    string.Equals(value.StackId, stackId, StringComparison.Ordinal));
                if (stack == null || stack.Quantity < 1)
                {
                    failureReason =
                        "surgery-material-capacity-exact-stack-missing:"
                        + stackId;
                    return false;
                }
                PhysicalItemMassSubject subject =
                    PhysicalItemMassSubjectAdapter.Create(
                        massQuery,
                        (ItemDefinitionId)stack.ItemId,
                        stack.ItemInstanceId,
                        stack.Components);
                capacityGrams = checked(capacityGrams
                    + massQuery.GetQuantityMass(
                        (ItemDefinitionId)stack.ItemId,
                        subject,
                        1).Value);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "surgery-material-capacity-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        if (capacityGrams <= 0L)
        {
            failureReason = "surgery-material-capacity-not-positive";
            return false;
        }
        return true;
    }

    private bool TryValidateStoredProjection(
        SurgeryOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || order.materialBufferCapacityGrams <= 0L
            || order.materialMassAuthorityRevision != massQuery.AuthorityRevision
            || !string.Equals(
                order.materialCapacityFingerprint,
                SurgeryMaterialCapacityFingerprint.Create(order),
                StringComparison.Ordinal))
        {
            failureReason = "surgery-material-authority-stored-projection-invalid:"
                + (order?.orderId ?? "<null>");
            return false;
        }
        return true;
    }

    private bool TryFindOwnedPair(
        SurgeryOrder order,
        out FacilityBufferDestinationClaim claim,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        claim = null;
        profile = null;
        failureReason = string.Empty;
        if (order == null || !TryValidateStoredProjection(order, out failureReason))
        {
            return false;
        }
        FacilityBufferDestinationClaim[] matchingClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                order.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                order.materialDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length != 1 || matchingProfiles.Length != 1)
        {
            failureReason = "surgery-material-authority-pair-cardinality:"
                + matchingClaims.Length.ToString(CultureInfo.InvariantCulture)
                + ":" + matchingProfiles.Length.ToString(
                    CultureInfo.InvariantCulture);
            return false;
        }
        claim = matchingClaims[0];
        profile = matchingProfiles[0];
        if (!PairMatches(order, claim, profile))
        {
            claim = null;
            profile = null;
            failureReason = "surgery-material-authority-pair-mismatch:"
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
        failureReason = string.Empty;
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                SurgeryMaterialDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                SurgeryMaterialDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        if (ownedClaims.Count != ownedProfiles.Count)
        {
            failureReason = "surgery-material-authority-partial-owner-set";
            return false;
        }
        for (int index = 0; index < ownedClaims.Count; index++)
        {
            if (!string.Equals(
                    ownedClaims[index].DestinationId,
                    ownedProfiles[index].DestinationId,
                    StringComparison.Ordinal))
            {
                failureReason = "surgery-material-authority-owner-set-mismatch";
                return false;
            }
        }
        return true;
    }

    private static FacilityBufferCapacityProfile CreateProfile(
        SurgeryOrder order,
        Vector2Int position) => new(
        order.materialDestinationId,
        position,
        SurgeryMaterialDestinationAuthority.OwnerDomain,
        order.orderId,
        order.facilityId,
        new PhysicalMassGrams(order.materialBufferCapacityGrams),
        SurgeryMaterialDestinationAuthority.InputBufferCapacitySchemaRevision);

    private static bool PairMatches(
        SurgeryOrder order,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) =>
        claim != null
        && profile != null
        && string.Equals(claim.DestinationId, order.materialDestinationId,
            StringComparison.Ordinal)
        && string.Equals(profile.DestinationId, order.materialDestinationId,
            StringComparison.Ordinal)
        && claim.DropPosition == profile.DropPosition
        && string.Equals(claim.OwnerDomain,
            SurgeryMaterialDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerDomain,
            SurgeryMaterialDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, order.orderId,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerOperationId, order.orderId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, order.facilityId,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, order.facilityId,
            StringComparison.Ordinal)
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.AdmissionPolicy
            == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && profile.MaxMassGrams == order.materialBufferCapacityGrams
        && profile.CapacityRevision
            == SurgeryMaterialDestinationAuthority
                .InputBufferCapacitySchemaRevision;

}
