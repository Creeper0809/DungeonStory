using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class CertifiedSeedInputOwnerDescriptor
{
    public CertifiedSeedInputOwnerDescriptor(
        string orderId,
        string facilityPersistentId,
        Vector2Int position,
        string destinationId,
        string seedItemId)
    {
        RequireCanonical(orderId, nameof(orderId));
        RequireCanonical(facilityPersistentId, nameof(facilityPersistentId));
        RequireCanonical(destinationId, nameof(destinationId));
        RequireCanonical(seedItemId, nameof(seedItemId));
        OrderId = orderId;
        FacilityPersistentId = facilityPersistentId;
        Position = position;
        DestinationId = destinationId;
        SeedItemId = seedItemId;
    }

    public string OrderId { get; }
    public string FacilityPersistentId { get; }
    public Vector2Int Position { get; }
    public string DestinationId { get; }
    public string SeedItemId { get; }

    private static void RequireCanonical(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Certified-seed input ownership requires canonical IDs.",
                name);
        }
    }
}

public sealed class CertifiedSeedInputOwnerProjection
{
    internal CertifiedSeedInputOwnerProjection(
        IReadOnlyList<FacilityBufferDestinationClaim> claims,
        IReadOnlyList<FacilityBufferCapacityProfile> profiles)
    {
        Claims = claims ?? throw new ArgumentNullException(nameof(claims));
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public IReadOnlyList<FacilityBufferDestinationClaim> Claims { get; }
    public IReadOnlyList<FacilityBufferCapacityProfile> Profiles { get; }
}

public static class CertifiedSeedInputOwnerAuthority
{
    public const string OwnerDomain = "economy.certified-seed";
    public const long CapacitySchemaRevision = 1L;
    public const string CompletionReleaseReasonCode =
        "certified-seed-input-committed";
    public const string AbortedReleaseReasonCode =
        "certified-seed-input-order-aborted";
    public const string FacilityLostReleaseReasonCode =
        "certified-seed-input-facility-lost";

    public static bool RequiresDestinationAuthority(
        CertifiedSeedOrderPhase phase) =>
        phase is CertifiedSeedOrderPhase.Planned
            or CertifiedSeedOrderPhase
                .InputCommittedAwaitingDestinationRetirement;

    public static string BuildDestinationId(
        string facilityId,
        string cropId,
        int sequence)
    {
        RequireCanonical(facilityId, nameof(facilityId));
        RequireCanonical(cropId, nameof(cropId));
        if (sequence < 0)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        return ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
            + OwnerDomain + ":"
            + Uri.EscapeDataString(facilityId) + ":"
            + Uri.EscapeDataString(cropId) + ":"
            + sequence.ToString(
                "D8",
                System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string BuildOwnerOperationId(string orderId)
    {
        RequireCanonical(orderId, nameof(orderId));
        return "certified-seed-input-owner:" + orderId;
    }

    public static CertifiedSeedInputOwnerProjection BuildProjection(
        IEnumerable<CertifiedSeedInputOwnerDescriptor> source,
        IPhysicalItemMassQuery massQuery)
    {
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));
        CertifiedSeedInputOwnerDescriptor[] descriptors = (source
                ?? Array.Empty<CertifiedSeedInputOwnerDescriptor>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (descriptors.Any(value => value == null)
            || descriptors.Select(value => value.OrderId)
                .Distinct(StringComparer.Ordinal).Count() != descriptors.Length
            || descriptors.Select(value => value.DestinationId)
                .Distinct(StringComparer.Ordinal).Count() != descriptors.Length)
        {
            throw new InvalidOperationException(
                "Certified-seed input descriptors must be non-null and unique.");
        }

        long kitMass = RequireUnitMass(
            CertifiedSeedPhysicalTransformAuthority.CertificationKitItemId,
            massQuery);
        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        foreach (CertifiedSeedInputOwnerDescriptor descriptor in descriptors)
        {
            long seedMass = RequireUnitMass(descriptor.SeedItemId, massQuery);
            long maximumMass = checked(
                seedMass * CertifiedSeedPhysicalTransformAuthority
                    .SeedInputQuantity
                + kitMass * CertifiedSeedPhysicalTransformAuthority
                    .CertificationKitInputQuantity);
            string operationId = BuildOwnerOperationId(descriptor.OrderId);
            claims.Add(new FacilityBufferDestinationClaim(
                descriptor.DestinationId,
                descriptor.Position,
                OwnerDomain,
                operationId,
                descriptor.FacilityPersistentId,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired));
            profiles.Add(new FacilityBufferCapacityProfile(
                descriptor.DestinationId,
                descriptor.Position,
                OwnerDomain,
                operationId,
                descriptor.FacilityPersistentId,
                new PhysicalMassGrams(maximumMass),
                CapacitySchemaRevision));
        }
        return new CertifiedSeedInputOwnerProjection(claims, profiles);
    }

    public static bool ClaimsMatch(
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

    public static bool ProfilesMatch(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left != null
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

    private static long RequireUnitMass(
        string itemId,
        IPhysicalItemMassQuery massQuery)
    {
        RequireCanonical(itemId, nameof(itemId));
        long value = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)itemId).Value;
        if (value <= 0L)
        {
            throw new InvalidOperationException(
                "Certified-seed input item mass must be positive: " + itemId);
        }
        return value;
    }

    private static void RequireCanonical(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Certified-seed input ownership requires canonical IDs.",
                name);
        }
    }
}

public interface ICertifiedSeedInputOwnerRuntime
{
    bool TryEnsure(
        CertifiedSeedInputOwnerDescriptor descriptor,
        out string failureReason);

    bool TryRetire(
        CertifiedSeedInputOwnerDescriptor descriptor,
        string reasonCode,
        out string failureReason);

    bool TryReplaceForRestore(
        IReadOnlyList<CertifiedSeedInputOwnerDescriptor> descriptors,
        out string failureReason);
}

public interface ICertifiedSeedInputOwnerDescriptorSource
{
    IReadOnlyList<CertifiedSeedInputOwnerDescriptor> BuildInputOwnerDescriptors(
        IReadOnlyList<CertifiedSeedOrderSaveData> orders);
}

/// <summary>
/// Owns the exact claim/profile pair for each live certified-seed input order.
/// Terminal retirement first releases unpicked, carried, and deposited custody
/// through the common physical release service, then revokes paired authority.
/// </summary>
public sealed class CertifiedSeedInputOwnerRuntime :
    ICertifiedSeedInputOwnerRuntime
{
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public CertifiedSeedInputOwnerRuntime(
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
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

    public bool TryEnsure(
        CertifiedSeedInputOwnerDescriptor descriptor,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            CertifiedSeedInputOwnerProjection addition =
                CertifiedSeedInputOwnerAuthority.BuildProjection(
                    new[] { descriptor },
                    massQuery);
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
                    descriptor.DestinationId,
                    StringComparison.Ordinal));
            if (existing >= 0)
            {
                bool matches = CertifiedSeedInputOwnerAuthority.ClaimsMatch(
                        currentClaims[existing],
                        addition.Claims[0])
                    && CertifiedSeedInputOwnerAuthority.ProfilesMatch(
                        currentProfiles[existing],
                        addition.Profiles[0]);
                failureReason = matches
                    ? string.Empty
                    : "certified-seed-input-owner-existing-pair-conflict:"
                        + descriptor.DestinationId;
                return matches;
            }

            return TryPublish(
                currentClaims.Append(addition.Claims[0]),
                currentProfiles.Append(addition.Profiles[0]),
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "certified-seed-input-owner-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    public bool TryRetire(
        CertifiedSeedInputOwnerDescriptor descriptor,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (descriptor == null
            || string.IsNullOrWhiteSpace(reasonCode)
            || !string.Equals(
                reasonCode,
                reasonCode.Trim(),
                StringComparison.Ordinal)
            || !TryCaptureOwnedPairs(
                out FacilityBufferDestinationClaim[] currentClaims,
                out FacilityBufferCapacityProfile[] currentProfiles,
                out failureReason))
        {
            if (failureReason.Length == 0)
                failureReason = "certified-seed-input-owner-retire-invalid";
            return false;
        }

        int index = Array.FindIndex(
            currentClaims,
            value => string.Equals(
                value.DestinationId,
                descriptor.DestinationId,
                StringComparison.Ordinal));
        if (index < 0)
            return true;
        CertifiedSeedInputOwnerProjection expected;
        try
        {
            expected = CertifiedSeedInputOwnerAuthority.BuildProjection(
                new[] { descriptor },
                massQuery);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "certified-seed-input-owner-retire-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        if (!CertifiedSeedInputOwnerAuthority.ClaimsMatch(
                currentClaims[index],
                expected.Claims[0])
            || !CertifiedSeedInputOwnerAuthority.ProfilesMatch(
                currentProfiles[index],
                expected.Profiles[0]))
        {
            failureReason = "certified-seed-input-owner-retire-pair-mismatch:"
                + descriptor.DestinationId;
            return false;
        }
        if (!releases.TryReleaseAtOwnerPosition(
                descriptor.DestinationId,
                descriptor.Position,
                reasonCode,
                out _,
                out string releaseFailure))
        {
            failureReason = "certified-seed-input-owner-terminal-release-failed:"
                + descriptor.DestinationId + ":" + releaseFailure;
            return false;
        }

        return TryPublish(
            currentClaims.Where((_, itemIndex) => itemIndex != index),
            currentProfiles.Where((_, itemIndex) => itemIndex != index),
            out failureReason);
    }

    public bool TryReplaceForRestore(
        IReadOnlyList<CertifiedSeedInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            CertifiedSeedInputOwnerProjection desired =
                CertifiedSeedInputOwnerAuthority.BuildProjection(
                    descriptors,
                    massQuery);
            return TryPublish(
                desired.Claims,
                desired.Profiles,
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason = "certified-seed-input-owner-restore-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private bool TryCaptureOwnedPairs(
        out FacilityBufferDestinationClaim[] ownedClaims,
        out FacilityBufferCapacityProfile[] ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CertifiedSeedInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CertifiedSeedInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (ownedClaims.Length != ownedProfiles.Length
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason = "certified-seed-input-owner-pair-set-torn";
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
            CertifiedSeedInputOwnerAuthority.OwnerDomain,
            desiredClaims.OrderBy(
                value => value.DestinationId,
                StringComparer.Ordinal).ToArray(),
            desiredProfiles.OrderBy(
                value => value.DestinationId,
                StringComparer.Ordinal).ToArray(),
            out failureReason);
        if (!published)
        {
            failureReason = "certified-seed-input-owner-publish-failed:"
                + failureReason;
        }
        return published;
    }

    private static bool IsProjectionException(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or OverflowException;
}
