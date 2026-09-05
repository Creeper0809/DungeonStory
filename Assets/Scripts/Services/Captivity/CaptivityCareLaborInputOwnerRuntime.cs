using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum CaptivityCareLaborInputKind
{
    Care = 0,
    LaborTool = 1
}

public sealed class CaptivityCareLaborInputOwnerDescriptor
{
    public CaptivityCareLaborInputOwnerDescriptor(
        CaptivityCareLaborInputKind kind,
        string captiveId,
        string housingFacilityId,
        Vector2Int position,
        long capacityGrams)
    {
        if (!Enum.IsDefined(typeof(CaptivityCareLaborInputKind), kind)
            || !IsCanonical(captiveId)
            || !IsCanonical(housingFacilityId)
            || capacityGrams <= 0L)
        {
            throw new ArgumentException(
                "Captivity care/labor ownership requires a canonical captive, live housing identity, and positive exact grams.");
        }

        Kind = kind;
        CaptiveId = captiveId;
        HousingFacilityId = housingFacilityId;
        Position = position;
        CapacityGrams = capacityGrams;
    }

    public CaptivityCareLaborInputKind Kind { get; }
    public string CaptiveId { get; }
    public string HousingFacilityId { get; }
    public Vector2Int Position { get; }
    public long CapacityGrams { get; }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class CaptivityCareLaborInputOwnerProjection
{
    public CaptivityCareLaborInputOwnerProjection(
        IReadOnlyList<FacilityBufferDestinationClaim> claims,
        IReadOnlyList<FacilityBufferCapacityProfile> profiles)
    {
        Claims = claims ?? throw new ArgumentNullException(nameof(claims));
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public IReadOnlyList<FacilityBufferDestinationClaim> Claims { get; }
    public IReadOnlyList<FacilityBufferCapacityProfile> Profiles { get; }
}

public static class CaptivityCareLaborInputOwnerAuthority
{
    public const string OwnerDomain = "captivity.care-labor";
    public const long CapacitySchemaRevision = 1L;
    public const string TerminalReleaseReasonCode =
        "captivity-care-labor-owner-retired";

    public static string FormatCareDestinationId(string captiveId) =>
        "captive-care:" + RequireCanonical(captiveId, nameof(captiveId));

    public static string FormatLaborToolDestinationId(string captiveId) =>
        "captive-labor-tool:" + RequireCanonical(captiveId, nameof(captiveId));

    public static CaptivityCareLaborInputOwnerProjection BuildProjection(
        IEnumerable<CaptivityCareLaborInputOwnerDescriptor> source)
    {
        CaptivityCareLaborInputOwnerDescriptor[] descriptors =
            (source ?? Array.Empty<CaptivityCareLaborInputOwnerDescriptor>())
            .OrderBy(value => value?.CaptiveId, StringComparer.Ordinal)
            .ThenBy(value => value?.Kind)
            .ToArray();
        if (descriptors.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "Captivity care/labor owner descriptors cannot contain null.");
        }

        string[] destinations = descriptors
            .Select(value => FormatDestinationId(value.Kind, value.CaptiveId))
            .ToArray();
        if (destinations.Distinct(StringComparer.Ordinal).Count()
            != destinations.Length)
        {
            throw new InvalidOperationException(
                "Captivity care/labor owner destinations must be unique.");
        }

        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        for (int index = 0; index < descriptors.Length; index++)
        {
            CaptivityCareLaborInputOwnerDescriptor descriptor =
                descriptors[index];
            string destinationId = destinations[index];
            string operationId = "captivity-care-labor-input-owner:"
                + FormatKindToken(descriptor.Kind) + ":"
                + descriptor.CaptiveId;
            claims.Add(new FacilityBufferDestinationClaim(
                destinationId,
                descriptor.Position,
                OwnerDomain,
                operationId,
                descriptor.HousingFacilityId,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired));
            profiles.Add(new FacilityBufferCapacityProfile(
                destinationId,
                descriptor.Position,
                OwnerDomain,
                operationId,
                descriptor.HousingFacilityId,
                new PhysicalMassGrams(descriptor.CapacityGrams),
                CapacitySchemaRevision));
        }

        return new CaptivityCareLaborInputOwnerProjection(claims, profiles);
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

    private static string FormatDestinationId(
        CaptivityCareLaborInputKind kind,
        string captiveId) => kind switch
        {
            CaptivityCareLaborInputKind.Care =>
                FormatCareDestinationId(captiveId),
            CaptivityCareLaborInputKind.LaborTool =>
                FormatLaborToolDestinationId(captiveId),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string FormatKindToken(
        CaptivityCareLaborInputKind kind) => kind switch
        {
            CaptivityCareLaborInputKind.Care => "care",
            CaptivityCareLaborInputKind.LaborTool => "labor-tool",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Captivity care/labor destination requires a canonical captive identity.",
                parameterName);
        }
        return value;
    }
}

public interface ICaptivityCareLaborInputOwnerRuntime
{
    bool TryReconcileLive(
        IReadOnlyList<CaptiveState> states,
        out string failureReason);

    bool TryReconcileRestore(
        IReadOnlyList<CaptiveState> states,
        out string failureReason);
}

/// <summary>
/// Owns the two physical destinations that are derived from current Captivity
/// V3 state. The persistent housing facility and its captive-housing capability
/// are the anchor; the performer runtime only unlocks care eligibility and does
/// not own or request the shared care lot.
/// </summary>
public sealed class CaptivityCareLaborInputOwnerRuntime :
    ICaptivityCareLaborInputOwnerRuntime
{
    private readonly IBuildingWorldQuery world;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IPhysicalItemMassQuery mass;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public CaptivityCareLaborInputOwnerRuntime(
        IBuildingWorldQuery world,
        IResourceEconomyContentCatalog catalog,
        IPhysicalItemMassQuery mass,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryReconcileLive(
        IReadOnlyList<CaptiveState> states,
        out string failureReason) => TryReconcile(
        states,
        requireLiveOwner: false,
        releaseRetired: true,
        out failureReason);

    public bool TryReconcileRestore(
        IReadOnlyList<CaptiveState> states,
        out string failureReason) => TryReconcile(
        states,
        requireLiveOwner: true,
        releaseRetired: false,
        out failureReason);

    private bool TryReconcile(
        IReadOnlyList<CaptiveState> states,
        bool requireLiveOwner,
        bool releaseRetired,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryBuildDesired(
                states,
                requireLiveOwner,
                out CaptivityCareLaborInputOwnerProjection desired,
                out failureReason)
            || !TryCaptureOwnedPairs(
                out FacilityBufferDestinationClaim[] existingClaims,
                out FacilityBufferCapacityProfile[] existingProfiles,
                out failureReason))
        {
            return false;
        }

        if (SetsMatch(
                existingClaims,
                existingProfiles,
                desired.Claims,
                desired.Profiles))
        {
            return true;
        }
        if (releaseRetired
            && !TryReleaseRetired(
                existingClaims,
                existingProfiles,
                desired.Claims,
                desired.Profiles,
                out failureReason))
        {
            return false;
        }
        if (lifecycle.TryReplaceOwnedAuthorities(
                CaptivityCareLaborInputOwnerAuthority.OwnerDomain,
                desired.Claims,
                desired.Profiles,
                out failureReason))
        {
            return true;
        }
        failureReason = "captivity-care-labor-authority-publish-failed:"
            + failureReason;
        return false;
    }

    private bool TryBuildDesired(
        IReadOnlyList<CaptiveState> states,
        bool requireLiveOwner,
        out CaptivityCareLaborInputOwnerProjection projection,
        out string failureReason)
    {
        projection = null;
        failureReason = string.Empty;
        try
        {
            Dictionary<string, BuildableObject> housingById =
                (world.Buildings ?? Array.Empty<BuildableObject>())
                .Where(value => value != null
                    && !value.isDestroy
                    && !value.IsGridDestroyed
                    && value.PersistentInstanceId.IsValid
                    && value.BuildingData?.GetCaptiveHousingAbility()
                        is { IsValid: true })
                .GroupBy(
                    value => value.PersistentInstanceId.Value,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count() == 1
                        ? group.Single()
                        : throw new InvalidOperationException(
                            "duplicate-live-captive-housing:" + group.Key),
                    StringComparer.Ordinal);
            CaptiveState[] active = (states ?? Array.Empty<CaptiveState>())
                .Where(value => value?.IsInCustody == true)
                .OrderBy(value => value.captiveId, StringComparer.Ordinal)
                .ToArray();
            if (active.Any(value => !IsCanonical(value.captiveId))
                || active.Select(value => value.captiveId)
                    .Distinct(StringComparer.Ordinal).Count() != active.Length)
            {
                failureReason =
                    "captivity-care-labor-active-captive-identity-invalid";
                return false;
            }

            bool needsCareMass = active.Any(value =>
                value.carePriorityUnlocked);
            bool needsLaborMass = active.Any(value =>
                value.pendingLaborPermissions != CaptiveLaborPermission.None
                || !string.IsNullOrEmpty(value.laborToolDestinationId));
            long careMass = needsCareMass ? CaptureMaximumFoodUnitMass() : 0L;
            long laborMass = needsLaborMass
                ? mass.GetDefinitionUnitMass(
                    (ItemDefinitionId)CaptivityItemDefinitions
                        .PrisonerWorkKitItemId).Value
                : 0L;
            if (needsCareMass && careMass <= 0L
                || needsLaborMass && laborMass <= 0L)
            {
                failureReason =
                    "captivity-care-labor-positive-mass-missing";
                return false;
            }

            List<CaptivityCareLaborInputOwnerDescriptor> descriptors = new();
            foreach (CaptiveState state in active)
            {
                bool careDemand = state.carePriorityUnlocked;
                bool laborDemand =
                    state.pendingLaborPermissions != CaptiveLaborPermission.None
                    || !string.IsNullOrEmpty(state.laborToolDestinationId);
                if (!careDemand && !laborDemand)
                    continue;
                if (!housingById.ContainsKey(state.housingBuildingId))
                {
                    if (requireLiveOwner)
                    {
                        failureReason =
                            "captivity-care-labor-live-housing-missing:"
                            + state.captiveId + ":" + state.housingBuildingId;
                        return false;
                    }
                    continue;
                }
                if (careDemand)
                {
                    descriptors.Add(new CaptivityCareLaborInputOwnerDescriptor(
                        CaptivityCareLaborInputKind.Care,
                        state.captiveId,
                        state.housingBuildingId,
                        state.housingPosition,
                        careMass));
                }
                if (laborDemand)
                {
                    string expected = CaptivityCareLaborInputOwnerAuthority
                        .FormatLaborToolDestinationId(state.captiveId);
                    if (state.pendingLaborPermissions ==
                            CaptiveLaborPermission.None
                        || !string.Equals(
                            state.laborToolDestinationId,
                            expected,
                            StringComparison.Ordinal))
                    {
                        failureReason =
                            "captivity-care-labor-pending-labor-identity-invalid:"
                            + state.captiveId;
                        return false;
                    }
                    descriptors.Add(new CaptivityCareLaborInputOwnerDescriptor(
                        CaptivityCareLaborInputKind.LaborTool,
                        state.captiveId,
                        state.housingBuildingId,
                        state.housingPosition,
                        laborMass));
                }
            }

            projection = CaptivityCareLaborInputOwnerAuthority.BuildProjection(
                descriptors);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException
                                           or KeyNotFoundException)
        {
            failureReason = "captivity-care-labor-projection-invalid:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private long CaptureMaximumFoodUnitMass()
    {
        ResourceItemDefinitionSO[] food = (catalog.Items
                ?? Array.Empty<ResourceItemDefinitionSO>())
            .Where(value => value != null
                && value.StockCategory == StockCategory.Food)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (food.Length == 0)
        {
            throw new InvalidOperationException(
                "Captive care has no physical Food definition.");
        }
        long maximum = 0L;
        foreach (ResourceItemDefinitionSO definition in food)
        {
            long unitMass = mass.GetDefinitionUnitMass(
                (ItemDefinitionId)definition.ItemId).Value;
            if (unitMass <= 0L)
            {
                throw new InvalidOperationException(
                    "Captive-care Food mass must be positive: "
                    + definition.ItemId);
            }
            maximum = Math.Max(maximum, unitMass);
        }
        return maximum;
    }

    private bool TryCaptureOwnedPairs(
        out FacilityBufferDestinationClaim[] ownedClaims,
        out FacilityBufferCapacityProfile[] ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CaptivityCareLaborInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CaptivityCareLaborInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        bool valid = ownedClaims.Length == ownedProfiles.Length
            && ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal)
            && ownedClaims.Zip(ownedProfiles, PairInternallyMatches)
                .All(value => value);
        failureReason = valid ? string.Empty
            : "captivity-care-labor-authority-pair-set-torn";
        return valid;
    }

    private bool TryReleaseRetired(
        IReadOnlyList<FacilityBufferDestinationClaim> existingClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> existingProfiles,
        IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
        out string failureReason)
    {
        Dictionary<string, FacilityBufferDestinationClaim> desiredClaimById =
            desiredClaims.ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        Dictionary<string, FacilityBufferCapacityProfile> desiredProfileById =
            desiredProfiles.ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        for (int index = 0; index < existingClaims.Count; index++)
        {
            FacilityBufferDestinationClaim claim = existingClaims[index];
            FacilityBufferCapacityProfile profile = existingProfiles[index];
            bool retained = desiredClaimById.TryGetValue(
                    claim.DestinationId,
                    out FacilityBufferDestinationClaim desiredClaim)
                && desiredProfileById.TryGetValue(
                    claim.DestinationId,
                    out FacilityBufferCapacityProfile desiredProfile)
                && CaptivityCareLaborInputOwnerAuthority.ClaimsMatch(
                    claim,
                    desiredClaim)
                && (CaptivityCareLaborInputOwnerAuthority.ProfilesMatch(
                        profile,
                        desiredProfile)
                    || CanRetainCapacityExpansion(profile, desiredProfile));
            if (retained)
                continue;
            if (!releases.TryReleaseAtOwnerPosition(
                    claim.DestinationId,
                    claim.DropPosition,
                    CaptivityCareLaborInputOwnerAuthority
                        .TerminalReleaseReasonCode,
                    out _,
                    out string releaseFailure))
            {
                failureReason =
                    "captivity-care-labor-terminal-release-failed:"
                    + claim.DestinationId + ":" + releaseFailure;
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool PairInternallyMatches(
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) => claim != null
        && profile != null
        && claim.DropPosition == profile.DropPosition
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.AdmissionPolicy ==
            FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && profile.MaxMassGrams > 0L
        && profile.CapacityRevision ==
            CaptivityCareLaborInputOwnerAuthority.CapacitySchemaRevision
        && string.Equals(claim.DestinationId, profile.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerDomain, profile.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId, profile.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, profile.OwnerFacilityId,
            StringComparison.Ordinal);

    private static bool CanRetainCapacityExpansion(
        FacilityBufferCapacityProfile existing,
        FacilityBufferCapacityProfile desired) => existing != null
        && desired != null
        && desired.MaxMassGrams >= existing.MaxMassGrams
        && PairInternallyMatches(
            new FacilityBufferDestinationClaim(
                existing.DestinationId,
                existing.DropPosition,
                existing.OwnerDomain,
                existing.OwnerOperationId,
                existing.OwnerFacilityId,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired),
            desired);

    private static bool SetsMatch(
        IReadOnlyList<FacilityBufferDestinationClaim> leftClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> leftProfiles,
        IReadOnlyList<FacilityBufferDestinationClaim> rightClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> rightProfiles) =>
        leftClaims.Count == rightClaims.Count
        && leftProfiles.Count == rightProfiles.Count
        && leftClaims.Zip(
            rightClaims,
            CaptivityCareLaborInputOwnerAuthority.ClaimsMatch)
            .All(value => value)
        && leftProfiles.Zip(
            rightProfiles,
            CaptivityCareLaborInputOwnerAuthority.ProfilesMatch)
            .All(value => value);

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>
/// Rebuilds the derived care/labor owner pairs from the staged current-format
/// Captivity V3 aggregate before the shared claim/profile candidates publish.
/// </summary>
public sealed class CaptivityCareLaborInputOwnerRestoreParticipant :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "218.world.captivity-care-labor-input-owner";

    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCareLaborInputOwnerRuntime owner;
    private bool active;
    private bool published;

    public CaptivityCareLaborInputOwnerRestoreParticipant(
        ICaptivityRuntime captivity,
        ICaptivityCareLaborInputOwnerRuntime owner)
    {
        this.captivity = captivity
            ?? throw new ArgumentNullException(nameof(captivity));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
        {
            throw new InvalidOperationException(
                "Captivity care/labor owner restore is already active.");
        }
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
        {
            throw new InvalidOperationException(
                "Captivity care/labor owner restore is not ready to publish.");
        }
        if (!owner.TryReconcileRestore(
                captivity.Captives,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Captivity care/labor owner restore join failed: "
                + failureReason);
        }
        published = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public void CompleteRestoreCandidate()
    {
        if (!active || !published)
        {
            throw new InvalidOperationException(
                "Captivity care/labor owner restore cannot complete.");
        }
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }
}
