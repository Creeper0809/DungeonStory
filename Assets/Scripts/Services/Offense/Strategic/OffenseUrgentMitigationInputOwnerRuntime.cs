using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class OffenseUrgentMitigationInputProjection
{
    internal OffenseUrgentMitigationInputProjection(
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile,
        long massAuthorityRevision,
        string fingerprint)
    {
        Claim = claim ?? throw new ArgumentNullException(nameof(claim));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        MassAuthorityRevision = massAuthorityRevision;
        Fingerprint = fingerprint ?? string.Empty;
    }

    public FacilityBufferDestinationClaim Claim { get; }
    public FacilityBufferCapacityProfile Profile { get; }
    public long MassAuthorityRevision { get; }
    public string Fingerprint { get; }
}

public static class OffenseUrgentMitigationInputOwnerAuthority
{
    public const string OwnerDomain = "offense.urgent-mitigation";
    public const long CapacitySchemaRevision = 1L;
    public const string CancelledReleaseReasonCode =
        "offense-urgent-mitigation-cancelled";
    public const string FacilityLostReleaseReasonCode =
        "offense-urgent-mitigation-facility-lost";
    public const string CompletedReleaseReasonCode =
        "offense-urgent-mitigation-input-committed";

    public static string BuildDestinationId(string orderId)
    {
        RequireCanonical(orderId, nameof(orderId));
        return ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
            + OwnerDomain + ":" + Uri.EscapeDataString(orderId);
    }

    public static string BuildOwnerOperationId(string orderId)
    {
        RequireCanonical(orderId, nameof(orderId));
        return "offense-urgent-mitigation-input-owner:" + orderId;
    }

    public static OffenseUrgentMitigationInputProjection BuildProjection(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentSiteDefinitionSO definition,
        string facilityPersistentId,
        Vector2Int position,
        IPhysicalItemMassQuery massQuery)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));
        RequireCanonical(order.orderId, nameof(order.orderId));
        RequireCanonical(order.definitionId, nameof(order.definitionId));
        RequireCanonical(facilityPersistentId, nameof(facilityPersistentId));
        RequireCanonical(definition.urgentSiteId, nameof(definition.urgentSiteId));
        RequireCanonical(definition.mitigationItemId,
            nameof(definition.mitigationItemId));
        if (!string.Equals(
                order.definitionId,
                definition.urgentSiteId,
                StringComparison.Ordinal)
            || definition.mitigationItemAmount <= 0)
        {
            throw new InvalidOperationException(
                "Urgent mitigation input ownership requires one positive authored material line.");
        }

        string expectedDestination = BuildDestinationId(order.orderId);
        if (!string.Equals(
                order.destinationId,
                expectedDestination,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Urgent mitigation input destination identity is not canonical.");
        }

        PhysicalMassGrams unitMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)definition.mitigationItemId);
        if (unitMass.Value <= 0L || massQuery.AuthorityRevision <= 0L)
        {
            throw new InvalidOperationException(
                "Urgent mitigation input requires positive current mass authority.");
        }
        long capacity = checked(
            unitMass.Value * definition.mitigationItemAmount);
        string operationId = BuildOwnerOperationId(order.orderId);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("offense-urgent-mitigation-input-capacity-v1");
        digest.Append(order.orderId);
        digest.Append(order.siteId);
        digest.Append(order.definitionId);
        digest.Append(facilityPersistentId);
        digest.Append(position.x);
        digest.Append(position.y);
        digest.Append(expectedDestination);
        digest.Append(definition.mitigationItemId);
        digest.Append(definition.mitigationItemAmount);
        digest.Append(unitMass.Value);
        digest.Append(massQuery.AuthorityRevision);
        digest.Append(capacity);
        string fingerprint = digest.ComputeSha256();

        return new OffenseUrgentMitigationInputProjection(
            new FacilityBufferDestinationClaim(
                expectedDestination,
                position,
                OwnerDomain,
                operationId,
                facilityPersistentId,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired),
            new FacilityBufferCapacityProfile(
                expectedDestination,
                position,
                OwnerDomain,
                operationId,
                facilityPersistentId,
                new PhysicalMassGrams(capacity),
                CapacitySchemaRevision),
            massQuery.AuthorityRevision,
            fingerprint);
    }

    public static bool ClaimsMatch(
        FacilityBufferDestinationClaim left,
        FacilityBufferDestinationClaim right) => left != null
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

    public static bool ProfilesMatch(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) => left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && left.MaxMassGrams == right.MaxMassGrams
        && left.CapacityRevision == right.CapacityRevision
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerDomain, right.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerOperationId, right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerFacilityId, right.OwnerFacilityId,
            StringComparison.Ordinal);

    public static bool HasStoredProjection(
        OffenseUrgentMitigationOrderStateData order) => order != null
        && (order.inputBufferCapacityGrams > 0L
            || order.inputMassAuthorityRevision > 0L
            || !string.IsNullOrEmpty(order.inputCapacityFingerprint));

    public static bool StoredProjectionIsEmpty(
        OffenseUrgentMitigationOrderStateData order) => order != null
        && order.inputBufferCapacityGrams == 0L
        && order.inputMassAuthorityRevision == 0L
        && string.IsNullOrEmpty(order.inputCapacityFingerprint);

    public static void ClearStoredProjection(
        OffenseUrgentMitigationOrderStateData order)
    {
        if (order == null)
            return;
        order.inputBufferCapacityGrams = 0L;
        order.inputMassAuthorityRevision = 0L;
        order.inputCapacityFingerprint = string.Empty;
    }

    private static void RequireCanonical(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Urgent mitigation input ownership requires canonical IDs.",
                name);
        }
    }
}

public interface IOffenseUrgentMitigationInputOwnerRuntime
{
    bool TryEnsure(
        OffenseUrgentMitigationOrderStateData order,
        BuildableObject facility,
        out string failureReason);

    bool TryRetire(
        OffenseUrgentMitigationOrderStateData order,
        string reasonCode,
        out string failureReason);

    bool TryReplaceForRestore(
        IReadOnlyList<OffenseUrgentMitigationOrderStateData> orders,
        out string failureReason);

    bool TryValidateForCapture(
        IReadOnlyList<OffenseUrgentMitigationOrderStateData> orders,
        IReadOnlyList<BuildableObject> facilities,
        out string failureReason);
}

/// <summary>
/// Owns the exact claim/profile pair for each active urgent-mitigation input.
/// The offense order remains the saved operation authority. This projection is
/// derived from its exact material line and is terminally retired only after
/// the common release service preserves unpicked, carried and deposited cargo.
/// </summary>
public sealed class OffenseUrgentMitigationInputOwnerRuntime :
    IOffenseUrgentMitigationInputOwnerRuntime
{
    private readonly IOffenseContentCatalog content;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public OffenseUrgentMitigationInputOwnerRuntime(
        IOffenseContentCatalog content,
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    [GameplayInternalOnly(
        "Publishes the exact urgent-mitigation input pair before delivery.",
        "OffenseUrgentMitigationRuntime only")]
    public bool TryEnsure(
        OffenseUrgentMitigationOrderStateData order,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || facility == null || facility.isDestroy)
        {
            failureReason = "urgent-mitigation-input-owner-live-facility-missing";
            return false;
        }

        string facilityId;
        try
        {
            facilityId = facility.RequirePersistentInstanceId().Value;
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "urgent-mitigation-input-owner-facility-id-invalid:"
                + exception.Message;
            return false;
        }
        if (!string.Equals(
                order.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal)
            || order.facilityX != facility.centerPos.x
            || order.facilityY != facility.centerPos.y)
        {
            failureReason =
                "urgent-mitigation-input-owner-order-facility-drift:"
                + order.orderId;
            return false;
        }

        OffenseUrgentMitigationInputProjection projection;
        try
        {
            projection = BuildProjection(
                order,
                facilityId,
                facility.centerPos);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "urgent-mitigation-input-owner-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }

        long previousCapacity = order.inputBufferCapacityGrams;
        long previousRevision = order.inputMassAuthorityRevision;
        string previousFingerprint = order.inputCapacityFingerprint;
        bool assignedProjection = false;
        if (OffenseUrgentMitigationInputOwnerAuthority
            .StoredProjectionIsEmpty(order))
        {
            order.inputBufferCapacityGrams =
                projection.Profile.MaxMassGrams;
            order.inputMassAuthorityRevision =
                projection.MassAuthorityRevision;
            order.inputCapacityFingerprint = projection.Fingerprint;
            assignedProjection = true;
        }
        else if (!StoredProjectionMatches(order, projection))
        {
            failureReason =
                "urgent-mitigation-input-owner-stored-projection-drift:"
                + order.orderId;
            return false;
        }

        try
        {
            if (!TryCaptureOwnedPairs(
                    out FacilityBufferDestinationClaim[] currentClaims,
                    out FacilityBufferCapacityProfile[] currentProfiles,
                    out failureReason))
            {
                return false;
            }
            int existing = Array.FindIndex(
                currentClaims,
                value => string.Equals(
                    value.DestinationId,
                    projection.Claim.DestinationId,
                    StringComparison.Ordinal));
            if (existing >= 0)
            {
                bool matches = OffenseUrgentMitigationInputOwnerAuthority
                        .ClaimsMatch(currentClaims[existing], projection.Claim)
                    && OffenseUrgentMitigationInputOwnerAuthority
                        .ProfilesMatch(currentProfiles[existing], projection.Profile);
                failureReason = matches
                    ? string.Empty
                    : "urgent-mitigation-input-owner-existing-pair-conflict:"
                        + order.destinationId;
                return matches;
            }
            return TryPublish(
                currentClaims.Append(projection.Claim),
                currentProfiles.Append(projection.Profile),
                out failureReason);
        }
        finally
        {
            if (assignedProjection && !string.IsNullOrEmpty(failureReason))
            {
                order.inputBufferCapacityGrams = previousCapacity;
                order.inputMassAuthorityRevision = previousRevision;
                order.inputCapacityFingerprint = previousFingerprint
                    ?? string.Empty;
            }
        }
    }

    [GameplayInternalOnly(
        "Drains urgent-mitigation custody before terminal authority retirement.",
        "OffenseUrgentMitigationRuntime only")]
    public bool TryRetire(
        OffenseUrgentMitigationOrderStateData order,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || string.IsNullOrWhiteSpace(reasonCode)
            || !string.Equals(reasonCode, reasonCode.Trim(),
                StringComparison.Ordinal)
            || !TryCaptureOwnedPairs(
                out FacilityBufferDestinationClaim[] currentClaims,
                out FacilityBufferCapacityProfile[] currentProfiles,
                out failureReason))
        {
            if (failureReason.Length == 0)
                failureReason = "urgent-mitigation-input-owner-retire-invalid";
            return false;
        }

        int index = Array.FindIndex(
            currentClaims,
            value => string.Equals(
                value.DestinationId,
                order.destinationId,
                StringComparison.Ordinal));
        if (index < 0)
        {
            if (!OffenseUrgentMitigationInputOwnerAuthority
                    .StoredProjectionIsEmpty(order))
            {
                failureReason =
                    "urgent-mitigation-input-owner-retire-authority-missing:"
                    + order.destinationId;
                return false;
            }
            return true;
        }

        OffenseUrgentMitigationInputProjection expected;
        try
        {
            expected = BuildProjection(
                order,
                order.facilityPersistentId,
                new Vector2Int(order.facilityX, order.facilityY));
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "urgent-mitigation-input-owner-retire-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        if (!StoredProjectionMatches(order, expected)
            || !OffenseUrgentMitigationInputOwnerAuthority.ClaimsMatch(
                currentClaims[index], expected.Claim)
            || !OffenseUrgentMitigationInputOwnerAuthority.ProfilesMatch(
                currentProfiles[index], expected.Profile))
        {
            failureReason = "urgent-mitigation-input-owner-retire-pair-mismatch:"
                + order.destinationId;
            return false;
        }

        if (!releases.TryReleaseAtOwnerPosition(
                order.destinationId,
                expected.Claim.DropPosition,
                reasonCode,
                out _,
                out string releaseFailure))
        {
            failureReason =
                "urgent-mitigation-input-owner-terminal-release-failed:"
                + order.destinationId + ":" + releaseFailure;
            return false;
        }
        if (!TryPublish(
                currentClaims.Where((_, itemIndex) => itemIndex != index),
                currentProfiles.Where((_, itemIndex) => itemIndex != index),
                out failureReason))
        {
            return false;
        }
        OffenseUrgentMitigationInputOwnerAuthority.ClearStoredProjection(order);
        return true;
    }

    [GameplayInternalOnly(
        "Rebuilds derived urgent-mitigation owner pairs in a restore candidate.",
        "Offense aggregate restore only")]
    public bool TryReplaceForRestore(
        IReadOnlyList<OffenseUrgentMitigationOrderStateData> orders,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            if (!TryBuildDesired(
                    orders,
                    facilities: null,
                    requireLiveFacilityJoin: false,
                    out OffenseUrgentMitigationInputProjection[] desired,
                    out failureReason))
            {
                return false;
            }
            return TryPublish(
                desired.Select(value => value.Claim),
                desired.Select(value => value.Profile),
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason =
                "urgent-mitigation-input-owner-restore-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    [GameplayInternalOnly(
        "Rejects capture when saved orders and live owner pairs are torn.",
        "Offense aggregate capture only")]
    public bool TryValidateForCapture(
        IReadOnlyList<OffenseUrgentMitigationOrderStateData> orders,
        IReadOnlyList<BuildableObject> facilities,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            if (!TryBuildDesired(
                    orders,
                    facilities,
                    requireLiveFacilityJoin: true,
                    out OffenseUrgentMitigationInputProjection[] desired,
                    out failureReason)
                || !TryCaptureOwnedPairs(
                    out FacilityBufferDestinationClaim[] currentClaims,
                    out FacilityBufferCapacityProfile[] currentProfiles,
                    out failureReason))
            {
                return false;
            }
            FacilityBufferDestinationClaim[] desiredClaims = desired
                .Select(value => value.Claim)
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();
            FacilityBufferCapacityProfile[] desiredProfiles = desired
                .Select(value => value.Profile)
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();
            bool matches = currentClaims.Length == desiredClaims.Length
                && currentProfiles.Length == desiredProfiles.Length
                && currentClaims.Zip(
                    desiredClaims,
                    OffenseUrgentMitigationInputOwnerAuthority.ClaimsMatch)
                    .All(value => value)
                && currentProfiles.Zip(
                    desiredProfiles,
                    OffenseUrgentMitigationInputOwnerAuthority.ProfilesMatch)
                    .All(value => value);
            failureReason = matches
                ? string.Empty
                : "urgent-mitigation-input-owner-capture-pair-set-drift";
            return matches;
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason =
                "urgent-mitigation-input-owner-capture-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private bool TryBuildDesired(
        IReadOnlyList<OffenseUrgentMitigationOrderStateData> orders,
        IReadOnlyList<BuildableObject> facilities,
        bool requireLiveFacilityJoin,
        out OffenseUrgentMitigationInputProjection[] desired,
        out string failureReason)
    {
        failureReason = string.Empty;
        List<OffenseUrgentMitigationInputProjection> values = new();
        Dictionary<string, BuildableObject> liveFacilities =
            requireLiveFacilityJoin
                ? (facilities ?? Array.Empty<BuildableObject>())
                    .Where(value => value != null && !value.isDestroy)
                    .ToDictionary(
                        value => value.RequirePersistentInstanceId().Value,
                        StringComparer.Ordinal)
                : null;
        foreach (OffenseUrgentMitigationOrderStateData order in
                 (orders ?? Array.Empty<OffenseUrgentMitigationOrderStateData>())
                     .Where(value => value != null)
                     .OrderBy(value => value.destinationId,
                         StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(order.facilityPersistentId))
            {
                if (!OffenseUrgentMitigationInputOwnerAuthority
                        .StoredProjectionIsEmpty(order)
                    || order.status
                        != OffenseUrgentMitigationOrderStatus.WaitingForFacility)
                {
                    failureReason =
                        "urgent-mitigation-input-owner-unbound-state-invalid:"
                        + order.orderId;
                    desired = Array.Empty<OffenseUrgentMitigationInputProjection>();
                    return false;
                }
                continue;
            }

            Vector2Int position = new(order.facilityX, order.facilityY);
            if (requireLiveFacilityJoin
                && (!liveFacilities.TryGetValue(
                        order.facilityPersistentId,
                        out BuildableObject facility)
                    || facility.centerPos != position))
            {
                failureReason =
                    "urgent-mitigation-input-owner-live-facility-join-failed:"
                    + order.orderId;
                desired = Array.Empty<OffenseUrgentMitigationInputProjection>();
                return false;
            }

            OffenseUrgentMitigationInputProjection projection = BuildProjection(
                order,
                order.facilityPersistentId,
                position);
            if (!StoredProjectionMatches(order, projection))
            {
                failureReason =
                    "urgent-mitigation-input-owner-stored-projection-invalid:"
                    + order.orderId;
                desired = Array.Empty<OffenseUrgentMitigationInputProjection>();
                return false;
            }
            values.Add(projection);
        }
        desired = values.ToArray();
        return true;
    }

    private OffenseUrgentMitigationInputProjection BuildProjection(
        OffenseUrgentMitigationOrderStateData order,
        string facilityPersistentId,
        Vector2Int position)
    {
        OffenseUrgentSiteDefinitionSO definition = content.UrgentSites
            .SingleOrDefault(value => value != null && string.Equals(
                value.urgentSiteId,
                order.definitionId,
                StringComparison.Ordinal));
        if (definition == null)
        {
            throw new InvalidOperationException(
                "Urgent mitigation input definition is missing: "
                + order.definitionId);
        }
        return OffenseUrgentMitigationInputOwnerAuthority.BuildProjection(
            order,
            definition,
            facilityPersistentId,
            position,
            massQuery);
    }

    private static bool StoredProjectionMatches(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentMitigationInputProjection projection) => order != null
        && projection != null
        && order.inputBufferCapacityGrams == projection.Profile.MaxMassGrams
        && order.inputMassAuthorityRevision == projection.MassAuthorityRevision
        && string.Equals(
            order.inputCapacityFingerprint,
            projection.Fingerprint,
            StringComparison.Ordinal);

    private bool TryCaptureOwnedPairs(
        out FacilityBufferDestinationClaim[] ownedClaims,
        out FacilityBufferCapacityProfile[] ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                OffenseUrgentMitigationInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                OffenseUrgentMitigationInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (ownedClaims.Length != ownedProfiles.Length
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason =
                "urgent-mitigation-input-owner-authority-pair-set-torn";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private bool TryPublish(
        IEnumerable<FacilityBufferDestinationClaim> desiredClaims,
        IEnumerable<FacilityBufferCapacityProfile> desiredProfiles,
        out string failureReason)
    {
        bool published = lifecycle.TryReplaceOwnedAuthorities(
            OffenseUrgentMitigationInputOwnerAuthority.OwnerDomain,
            desiredClaims.OrderBy(
                value => value.DestinationId,
                StringComparer.Ordinal).ToArray(),
            desiredProfiles.OrderBy(
                value => value.DestinationId,
                StringComparer.Ordinal).ToArray(),
            out failureReason);
        if (!published)
        {
            failureReason = "urgent-mitigation-input-owner-publish-failed:"
                + failureReason;
        }
        return published;
    }

    private static bool IsProjectionException(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or OverflowException;
}
