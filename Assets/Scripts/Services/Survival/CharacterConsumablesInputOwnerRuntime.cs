using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterConsumablesInputOwnerProjection
{
    internal CharacterConsumablesInputOwnerProjection(
        IReadOnlyList<FacilityBufferDestinationClaim> claims,
        IReadOnlyList<FacilityBufferCapacityProfile> profiles)
    {
        Claims = claims ?? throw new ArgumentNullException(nameof(claims));
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public IReadOnlyList<FacilityBufferDestinationClaim> Claims { get; }
    public IReadOnlyList<FacilityBufferCapacityProfile> Profiles { get; }
}

public static class CharacterConsumablesInputOwnerAuthority
{
    public const string OwnerDomain =
        CharacterConsumablesInputDestinationIdentity.OwnerDomain;
    public const long CapacitySchemaRevision = 1L;
    public const string CapabilityRemovedReleaseReasonCode =
        CharacterConsumablesInputDestinationIdentity
            .CapabilityRemovedReleaseReasonCode;
    public const string FacilityLostReleaseReasonCode =
        CharacterConsumablesInputDestinationIdentity
            .FacilityLostReleaseReasonCode;
    public static string BuildOwnerOperationId(
        CharacterConsumablesInputOwnerDescriptor descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));
        return "character-consumables-input-owner:"
            + CharacterConsumablesInputDestinationIdentity.KindSegment(
                descriptor.Kind) + ":"
            + Uri.EscapeDataString(descriptor.FacilityPersistentId) + ":"
            + Uri.EscapeDataString(descriptor.ItemDefinitionId);
    }

    public static CharacterConsumablesInputOwnerProjection BuildProjection(
        IEnumerable<CharacterConsumablesInputOwnerDescriptor> source,
        IPhysicalItemMassQuery massQuery)
    {
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));
        CharacterConsumablesInputOwnerDescriptor[] descriptors = (source
                ?? Array.Empty<CharacterConsumablesInputOwnerDescriptor>())
            .OrderBy(value => value?.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (descriptors.Any(value => value == null)
            || descriptors.Select(value => value.DestinationId)
                .Distinct(StringComparer.Ordinal).Count() != descriptors.Length)
        {
            throw new InvalidOperationException(
                "Character-consumables input descriptors must be non-null and unique.");
        }

        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        foreach (CharacterConsumablesInputOwnerDescriptor descriptor in descriptors)
        {
            long exactUnitGrams = massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)descriptor.ItemDefinitionId).Value;
            if (exactUnitGrams <= 0L)
            {
                throw new InvalidOperationException(
                    "Character-consumables input item mass must be positive: "
                    + descriptor.ItemDefinitionId);
            }
            string operationId = BuildOwnerOperationId(descriptor);
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
                new PhysicalMassGrams(exactUnitGrams),
                CapacitySchemaRevision));
        }
        return new CharacterConsumablesInputOwnerProjection(claims, profiles);
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

}

public sealed class CharacterConsumablesInputOwnerDescriptorSource :
    ICharacterConsumablesInputOwnerDescriptorSource
{
    private readonly IItemDefinitionCatalog catalog;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;

    public CharacterConsumablesInputOwnerDescriptorSource(
        IItemDefinitionCatalog catalog,
        ICharacterAiWorldRegistry world,
        IRestoreWorldCandidateQuery restoreWorldCandidates)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
    }

    public IReadOnlyList<CharacterConsumablesInputOwnerDescriptor>
        BuildLiveInputOwnerDescriptors() => BuildForBuildings(world.Buildings);

    public IReadOnlyList<CharacterConsumablesInputOwnerDescriptor>
        BuildRestoreInputOwnerDescriptors()
    {
        if (!restoreWorldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> buildings)
            || buildings == null)
        {
            throw new InvalidOperationException(
                "Character-consumables input owner restore requires the detached facility world.");
        }
        return BuildForBuildings(buildings);
    }

    internal IReadOnlyList<CharacterConsumablesInputOwnerDescriptor>
        BuildForBuildings(IEnumerable<BuildableObject> buildings)
    {
        ItemDefinitionSO[] mealItems = catalog.All
            .Where(value => value != null
                && value.TryGetFeature(out FoodItemFeature _))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        ItemDefinitionSO[] recreationalItems = catalog.All
            .Where(value => value != null
                && value.TryGetFeature(out SubstanceItemFeature feature)
                && feature.useClass == SubstanceUseClass.Recreational)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        List<CharacterConsumablesInputOwnerDescriptor> result = new();
        foreach (BuildableObject facility in (buildings
                     ?? Array.Empty<BuildableObject>())
                 .Where(value => value != null && !value.isDestroy)
                 .OrderBy(
                     value => value.RequirePersistentInstanceId().Value,
                     StringComparer.Ordinal))
        {
            BuildingInstanceId facilityId =
                facility.RequirePersistentInstanceId();
            if (facility.SupportsFacilityRole(FacilityRole.Meal))
            {
                AddDescriptors(
                    result,
                    CharacterConsumablesInputKind.Meal,
                    facilityId,
                    facility.centerPos,
                    mealItems);
            }
            if (facility.BuildingData?
                    .GetAbility<BuildingRecreationalSubstanceServiceAbility>()?
                    .IsValid == true)
            {
                AddDescriptors(
                    result,
                    CharacterConsumablesInputKind.RecreationalSubstance,
                    facilityId,
                    facility.centerPos,
                    recreationalItems);
            }
        }
        return result
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddDescriptors(
        ICollection<CharacterConsumablesInputOwnerDescriptor> destination,
        CharacterConsumablesInputKind kind,
        BuildingInstanceId facilityId,
        Vector2Int position,
        IEnumerable<ItemDefinitionSO> items)
    {
        foreach (ItemDefinitionSO item in items)
        {
            destination.Add(new CharacterConsumablesInputOwnerDescriptor(
                kind,
                facilityId.Value,
                position,
                item.ItemId));
        }
    }
}

public sealed class CharacterConsumablesInputOwnerRuntime :
    ICharacterConsumablesInputOwnerRuntime
{
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public CharacterConsumablesInputOwnerRuntime(
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

    public bool TryReconcileLive(
        IReadOnlyList<CharacterConsumablesInputOwnerDescriptor> descriptors,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(reasonCode)
            || !string.Equals(
                reasonCode,
                reasonCode.Trim(),
                StringComparison.Ordinal))
        {
            failureReason =
                "character-consumables-input-owner-release-reason-invalid";
            return false;
        }

        CharacterConsumablesInputOwnerProjection desired;
        try
        {
            desired = CharacterConsumablesInputOwnerAuthority.BuildProjection(
                descriptors,
                massQuery);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason =
                "character-consumables-input-owner-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }

        if (!TryCaptureOwnedPairs(
                out FacilityBufferDestinationClaim[] currentClaims,
                out FacilityBufferCapacityProfile[] currentProfiles,
                out failureReason))
        {
            return false;
        }
        IReadOnlyDictionary<string, FacilityBufferDestinationClaim>
            desiredClaims = desired.Claims.ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        IReadOnlyDictionary<string, FacilityBufferCapacityProfile>
            desiredProfiles = desired.Profiles.ToDictionary(
                value => value.DestinationId,
                StringComparer.Ordinal);
        List<FacilityBufferDestinationClaim> retired = new();
        for (int index = 0; index < currentClaims.Length; index++)
        {
            FacilityBufferDestinationClaim currentClaim = currentClaims[index];
            if (!desiredClaims.TryGetValue(
                    currentClaim.DestinationId,
                    out FacilityBufferDestinationClaim desiredClaim)
                || !desiredProfiles.TryGetValue(
                    currentClaim.DestinationId,
                    out FacilityBufferCapacityProfile desiredProfile)
                || !CharacterConsumablesInputOwnerAuthority.ClaimsMatch(
                    currentClaim,
                    desiredClaim)
                || !CharacterConsumablesInputOwnerAuthority.ProfilesMatch(
                    currentProfiles[index],
                    desiredProfile))
            {
                retired.Add(currentClaim);
            }
        }
        foreach (FacilityBufferDestinationClaim claim in retired
                     .OrderBy(value => value.DestinationId, StringComparer.Ordinal))
        {
            if (!releases.TryReleaseAtOwnerPosition(
                    claim.DestinationId,
                    claim.DropPosition,
                    reasonCode,
                    out _,
                    out string releaseFailure))
            {
                failureReason =
                    "character-consumables-input-owner-terminal-release-failed:"
                    + claim.DestinationId + ":" + releaseFailure;
                return false;
            }
        }

        if (retired.Count == 0
            && currentClaims.Length == desired.Claims.Count
            && currentClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    desired.Claims.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            return true;
        }

        return TryPublish(desired, out failureReason);
    }

    public bool TryReplaceForRestore(
        IReadOnlyList<CharacterConsumablesInputOwnerDescriptor> descriptors,
        out string failureReason)
    {
        try
        {
            return TryPublish(
                CharacterConsumablesInputOwnerAuthority.BuildProjection(
                    descriptors,
                    massQuery),
                out failureReason);
        }
        catch (Exception exception) when (IsProjectionException(exception))
        {
            failureReason =
                "character-consumables-input-owner-restore-projection-failed:"
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
                CharacterConsumablesInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                CharacterConsumablesInputOwnerAuthority.OwnerDomain,
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
                "character-consumables-input-owner-pair-set-torn";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private bool TryPublish(
        CharacterConsumablesInputOwnerProjection projection,
        out string failureReason)
    {
        bool published = lifecycle.TryReplaceOwnedAuthorities(
            CharacterConsumablesInputOwnerAuthority.OwnerDomain,
            projection.Claims,
            projection.Profiles,
            out failureReason);
        if (!published)
        {
            failureReason =
                "character-consumables-input-owner-publish-failed:"
                + failureReason;
        }
        return published;
    }

    private static bool IsProjectionException(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or OverflowException;
}

public sealed class CharacterConsumablesInputOwnerLifecycleRuntime :
    IStartable,
    ITickable
{
    private const float ReconcileIntervalSeconds = 1f;
    private readonly ICharacterConsumablesInputOwnerDescriptorSource source;
    private readonly ICharacterConsumablesInputOwnerRuntime owners;
    private readonly IGameClock clock;
    private float nextReconcileAt;

    public CharacterConsumablesInputOwnerLifecycleRuntime(
        ICharacterConsumablesInputOwnerDescriptorSource source,
        ICharacterConsumablesInputOwnerRuntime owners,
        IGameClock clock)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.owners = owners ?? throw new ArgumentNullException(nameof(owners));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void Start()
    {
        ReconcileOrThrow();
        nextReconcileAt = clock.Time + ReconcileIntervalSeconds;
    }

    public void Tick()
    {
        if (clock.Time < nextReconcileAt
            && nextReconcileAt - clock.Time <= ReconcileIntervalSeconds * 2f)
        {
            return;
        }
        ReconcileOrThrow();
        nextReconcileAt = clock.Time + ReconcileIntervalSeconds;
    }

    public void ReconcileOrThrow()
    {
        if (!owners.TryReconcileLive(
                source.BuildLiveInputOwnerDescriptors(),
                CharacterConsumablesInputOwnerAuthority
                    .CapabilityRemovedReleaseReasonCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Character-consumables input owner reconcile failed: "
                + failureReason);
        }
    }
}
