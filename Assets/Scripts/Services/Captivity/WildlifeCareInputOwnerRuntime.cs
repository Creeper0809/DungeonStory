using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeCareInputOwnerDescriptor
{
    public WildlifeCareInputOwnerDescriptor(
        string penPersistentId,
        Vector2Int position,
        int careAnimalCount,
        int foodUnitsPerAnimal,
        int waterUnitsPerAnimal,
        long maximumFeedUnitMassGrams,
        long maximumWaterUnitMassGrams,
        long massAuthorityRevision)
    {
        if (!IsCanonical(penPersistentId)
            || careAnimalCount <= 0
            || foodUnitsPerAnimal < 0
            || waterUnitsPerAnimal < 0
            || foodUnitsPerAnimal == 0 && waterUnitsPerAnimal == 0
            || foodUnitsPerAnimal > 0 && maximumFeedUnitMassGrams <= 0L
            || waterUnitsPerAnimal > 0 && maximumWaterUnitMassGrams <= 0L
            || massAuthorityRevision < 0L)
        {
            throw new ArgumentException(
                "Wildlife-care input ownership requires a canonical pen, active care demand, and positive exact masses.");
        }

        PenPersistentId = penPersistentId;
        Position = position;
        CareAnimalCount = careAnimalCount;
        FoodUnitsPerAnimal = foodUnitsPerAnimal;
        WaterUnitsPerAnimal = waterUnitsPerAnimal;
        MaximumFeedUnitMassGrams = maximumFeedUnitMassGrams;
        MaximumWaterUnitMassGrams = maximumWaterUnitMassGrams;
        MassAuthorityRevision = massAuthorityRevision;
    }

    public string PenPersistentId { get; }
    public Vector2Int Position { get; }
    public int CareAnimalCount { get; }
    public int FoodUnitsPerAnimal { get; }
    public int WaterUnitsPerAnimal { get; }
    public long MaximumFeedUnitMassGrams { get; }
    public long MaximumWaterUnitMassGrams { get; }
    public long MassAuthorityRevision { get; }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IWildlifeCareInputOwnerSource
{
    IReadOnlyList<WildlifeCareInputOwnerDescriptor> Capture();
}

/// <summary>
/// Projects every live beast pen with physical care demand into one exact
/// destination. The pen persistent identity, not a same-cell inference from
/// an individual animal, is the facility owner.
/// </summary>
public sealed class WildlifeCareInputOwnerSource :
    IWildlifeCareInputOwnerSource
{
    private readonly ICharacterAiWorldRegistry world;
    private readonly IWildlifeCaptureRuntime wildlife;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWasteProcessingRules wasteRules;
    private readonly IPhysicalItemMassQuery mass;
    private long cachedMassAuthorityRevision = long.MinValue;
    private long cachedMaximumFeedMass;
    private long cachedMaximumWaterMass;

    public WildlifeCareInputOwnerSource(
        ICharacterAiWorldRegistry world,
        IWildlifeCaptureRuntime wildlife,
        IResourceEconomyContentCatalog catalog,
        IWasteProcessingRules wasteRules,
        IPhysicalItemMassQuery mass)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.wildlife = wildlife
            ?? throw new ArgumentNullException(nameof(wildlife));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.wasteRules = wasteRules
            ?? throw new ArgumentNullException(nameof(wasteRules));
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
    }

    public IReadOnlyList<WildlifeCareInputOwnerDescriptor> Capture()
    {
        long massAuthorityRevision = mass.AuthorityRevision;
        EnsureMassEnvelopeCurrent(massAuthorityRevision);

        Dictionary<string, BuildableObject> pens = world.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && !value.IsGridDestroyed
                && value.PersistentInstanceId.IsValid
                && value.BuildingData?.GetBeastPenAbility()
                    is { IsValid: true })
            .GroupBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count() == 1
                    ? group.Single()
                    : throw new InvalidOperationException(
                        "Wildlife-care owner found duplicate live pen identity: "
                        + group.Key),
                StringComparer.Ordinal);

        List<WildlifeCareInputOwnerDescriptor> result = new();
        foreach (IGrouping<string, CapturedWildlifeState> group in
                 wildlife.CapturedAnimals
                     .Where(value => value != null
                         && !value.escaped
                         && value.transportState is not (
                             CapturedWildlifeTransportState.Released
                             or CapturedWildlifeTransportState.Escaped))
                     .OrderBy(value => value.penId, StringComparer.Ordinal)
                     .ThenBy(value => value.wildlifeId, StringComparer.Ordinal)
                     .GroupBy(value => value.penId, StringComparer.Ordinal))
        {
            if (!pens.TryGetValue(group.Key, out BuildableObject pen))
                continue;
            BuildingBeastPenAbility ability =
                pen.BuildingData.GetBeastPenAbility();
            int foodUnits = Mathf.Max(0, Mathf.CeilToInt(ability.dailyFood));
            int waterUnits = Mathf.Max(0, Mathf.CeilToInt(ability.dailyWater));
            if (foodUnits == 0 && waterUnits == 0)
                continue;
            if (foodUnits > 0 && cachedMaximumFeedMass <= 0L)
            {
                throw new InvalidOperationException(
                    "Wildlife-care food demand has no positive-mass feed definition.");
            }
            if (waterUnits > 0 && cachedMaximumWaterMass <= 0L)
            {
                throw new InvalidOperationException(
                    "Wildlife-care water demand has no positive-mass water definition.");
            }
            result.Add(new WildlifeCareInputOwnerDescriptor(
                group.Key,
                pen.centerPos,
                group.Count(),
                foodUnits,
                waterUnits,
                cachedMaximumFeedMass,
                cachedMaximumWaterMass,
                massAuthorityRevision));
        }
        return result;
    }

    private bool IsReachableFeed(ResourceItemDefinitionSO definition) =>
        definition.StockCategory == StockCategory.Food
        || wasteRules.TryGetLegacyWaste(
            definition.ItemId,
            out _,
            out _);

    private void EnsureMassEnvelopeCurrent(long expectedRevision)
    {
        if (cachedMassAuthorityRevision == expectedRevision)
            return;
        ResourceItemDefinitionSO[] definitions = (catalog.Items
                ?? Array.Empty<ResourceItemDefinitionSO>())
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        long maximumFeedMass = CaptureMaximumUnitMass(
            definitions.Where(IsReachableFeed));
        long maximumWaterMass = CaptureMaximumUnitMass(
            definitions.Where(value =>
                value.StockCategory == StockCategory.Water));
        if (mass.AuthorityRevision != expectedRevision)
        {
            throw new InvalidOperationException(
                "Wildlife-care mass authority changed during projection.");
        }
        cachedMaximumFeedMass = maximumFeedMass;
        cachedMaximumWaterMass = maximumWaterMass;
        cachedMassAuthorityRevision = expectedRevision;
    }

    private long CaptureMaximumUnitMass(
        IEnumerable<ResourceItemDefinitionSO> definitions)
    {
        long maximum = 0L;
        foreach (ResourceItemDefinitionSO definition in definitions)
        {
            long unitMass = mass.GetDefinitionUnitMass(
                (ItemDefinitionId)definition.ItemId).Value;
            if (unitMass <= 0L)
            {
                throw new InvalidOperationException(
                    "Wildlife-care input item mass must be positive: "
                    + definition.ItemId);
            }
            maximum = Math.Max(maximum, unitMass);
        }
        return maximum;
    }
}

public sealed class WildlifeCareInputOwnerProjection
{
    public WildlifeCareInputOwnerProjection(
        IReadOnlyList<FacilityBufferDestinationClaim> claims,
        IReadOnlyList<FacilityBufferCapacityProfile> profiles)
    {
        Claims = claims ?? throw new ArgumentNullException(nameof(claims));
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public IReadOnlyList<FacilityBufferDestinationClaim> Claims { get; }
    public IReadOnlyList<FacilityBufferCapacityProfile> Profiles { get; }
}

public static class WildlifeCareInputOwnerAuthority
{
    public const string OwnerDomain = "captivity.wildlife-care";
    public const long CapacitySchemaRevision = 1L;
    public const string TerminalReleaseReasonCode =
        "captivity-wildlife-care-owner-retired";

    public static string FormatDestinationId(string penPersistentId)
    {
        if (!IsCanonical(penPersistentId))
        {
            throw new ArgumentException(
                "Wildlife-care destination requires a canonical pen identity.",
                nameof(penPersistentId));
        }
        return ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
            + OwnerDomain + ":" + penPersistentId;
    }

    public static WildlifeCareInputOwnerProjection BuildProjection(
        IEnumerable<WildlifeCareInputOwnerDescriptor> source)
    {
        WildlifeCareInputOwnerDescriptor[] descriptors =
            (source ?? Array.Empty<WildlifeCareInputOwnerDescriptor>())
            .OrderBy(value => value?.PenPersistentId, StringComparer.Ordinal)
            .ToArray();
        if (descriptors.Any(value => value == null)
            || descriptors.Select(value => value.PenPersistentId)
                .Distinct(StringComparer.Ordinal).Count() != descriptors.Length)
        {
            throw new InvalidOperationException(
                "Wildlife-care owner descriptors must be non-null and unique by pen identity.");
        }

        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        foreach (WildlifeCareInputOwnerDescriptor descriptor in descriptors)
        {
            long capacity = checked(
                checked((long)descriptor.CareAnimalCount
                    * descriptor.FoodUnitsPerAnimal
                    * descriptor.MaximumFeedUnitMassGrams)
                + checked((long)descriptor.CareAnimalCount
                    * descriptor.WaterUnitsPerAnimal
                    * descriptor.MaximumWaterUnitMassGrams));
            if (capacity <= 0L)
            {
                throw new InvalidOperationException(
                    "Wildlife-care input capacity must be positive: "
                    + descriptor.PenPersistentId);
            }

            string destinationId = FormatDestinationId(
                descriptor.PenPersistentId);
            string operationId = "wildlife-care-input-owner:"
                + descriptor.PenPersistentId;
            claims.Add(new FacilityBufferDestinationClaim(
                destinationId,
                descriptor.Position,
                OwnerDomain,
                operationId,
                descriptor.PenPersistentId,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired));
            profiles.Add(new FacilityBufferCapacityProfile(
                destinationId,
                descriptor.Position,
                OwnerDomain,
                operationId,
                descriptor.PenPersistentId,
                new PhysicalMassGrams(capacity),
                CapacitySchemaRevision));
        }
        return new WildlifeCareInputOwnerProjection(claims, profiles);
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

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IWildlifeCareInputOwnerRuntime
{
    bool TryReconcileLive(out string failureReason);
    bool TryReconcileRestore(out string failureReason);
}

public sealed class WildlifeCareInputOwnerRuntime :
    IWildlifeCareInputOwnerRuntime
{
    private readonly IWildlifeCareInputOwnerSource source;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public WildlifeCareInputOwnerRuntime(
        IWildlifeCareInputOwnerSource source,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases
            ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryReconcileLive(out string failureReason) =>
        TryReconcile(releaseRetired: true, out failureReason);

    public bool TryReconcileRestore(out string failureReason) =>
        TryReconcile(releaseRetired: false, out failureReason);

    private bool TryReconcile(
        bool releaseRetired,
        out string failureReason)
    {
        failureReason = string.Empty;
        WildlifeCareInputOwnerProjection desired;
        try
        {
            desired = WildlifeCareInputOwnerAuthority.BuildProjection(
                source.Capture());
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "wildlife-care-input-projection-invalid:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }

        if (!TryCaptureOwnedPairs(
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
                WildlifeCareInputOwnerAuthority.OwnerDomain,
                desired.Claims,
                desired.Profiles,
                out failureReason))
        {
            return true;
        }
        failureReason = "wildlife-care-input-authority-publish-failed:"
            + failureReason;
        return false;
    }

    private bool TryCaptureOwnedPairs(
        out FacilityBufferDestinationClaim[] ownedClaims,
        out FacilityBufferCapacityProfile[] ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                WildlifeCareInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                WildlifeCareInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (ownedClaims.Length != ownedProfiles.Length
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason = "wildlife-care-input-authority-pair-set-torn";
            return false;
        }
        failureReason = string.Empty;
        return true;
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
                && WildlifeCareInputOwnerAuthority.ClaimsMatch(
                    claim,
                    desiredClaim)
                && (WildlifeCareInputOwnerAuthority.ProfilesMatch(
                        profile,
                        desiredProfile)
                    || CanRetainCapacityExpansion(profile, desiredProfile));
            if (retained)
                continue;
            if (!releases.TryReleaseAtOwnerPosition(
                    claim.DestinationId,
                    claim.DropPosition,
                    WildlifeCareInputOwnerAuthority
                        .TerminalReleaseReasonCode,
                    out _,
                    out string releaseFailure))
            {
                failureReason =
                    "wildlife-care-input-terminal-release-failed:"
                    + claim.DestinationId + ":" + releaseFailure;
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool CanRetainCapacityExpansion(
        FacilityBufferCapacityProfile existing,
        FacilityBufferCapacityProfile desired) =>
        existing != null
        && desired != null
        && existing.DropPosition == desired.DropPosition
        && desired.MaxMassGrams >= existing.MaxMassGrams
        && existing.CapacityRevision == desired.CapacityRevision
        && string.Equals(existing.DestinationId, desired.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(existing.OwnerDomain, desired.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(existing.OwnerOperationId, desired.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(existing.OwnerFacilityId, desired.OwnerFacilityId,
            StringComparison.Ordinal);

    private static bool SetsMatch(
        IReadOnlyList<FacilityBufferDestinationClaim> leftClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> leftProfiles,
        IReadOnlyList<FacilityBufferDestinationClaim> rightClaims,
        IReadOnlyList<FacilityBufferCapacityProfile> rightProfiles) =>
        leftClaims.Count == rightClaims.Count
        && leftProfiles.Count == rightProfiles.Count
        && leftClaims.Zip(
            rightClaims,
            WildlifeCareInputOwnerAuthority.ClaimsMatch).All(value => value)
        && leftProfiles.Zip(
            rightProfiles,
            WildlifeCareInputOwnerAuthority.ProfilesMatch).All(value => value);
}

public sealed class WildlifeCareInputOwnerLifecycleRuntime :
    IStartable,
    ITickable,
    IDungeonSaveCaptureGuard
{
    private readonly IWildlifeCareInputOwnerRuntime owner;
    private string unresolvedFailure = string.Empty;

    public WildlifeCareInputOwnerLifecycleRuntime(
        IWildlifeCareInputOwnerRuntime owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void Start() => Reconcile();
    public void Tick() => Reconcile();

    public void ValidateBeforeCapture()
    {
        Reconcile();
        if (unresolvedFailure.Length > 0)
        {
            throw new InvalidOperationException(
                "Wildlife-care input ownership is not capture-safe: "
                + unresolvedFailure);
        }
    }

    private void Reconcile()
    {
        unresolvedFailure = owner.TryReconcileLive(out string failureReason)
            ? string.Empty
            : failureReason;
    }
}

/// <summary>
/// Joins the staged Circus wildlife aggregate to the claim/profile restore
/// candidates before those shared authorities publish. The projection is
/// derived from the current-format wildlife state and live pen identity.
/// </summary>
public sealed class WildlifeCareInputOwnerRestoreParticipant :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "219.world.captivity-wildlife-care-input-owner";

    private readonly IWildlifeCareInputOwnerRuntime owner;
    private bool active;
    private bool published;

    public WildlifeCareInputOwnerRestoreParticipant(
        IWildlifeCareInputOwnerRuntime owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
            throw new InvalidOperationException(
                "Wildlife-care input owner restore is already active.");
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
            throw new InvalidOperationException(
                "Wildlife-care input owner restore is not ready to publish.");
        if (!owner.TryReconcileRestore(out string failureReason))
            throw new InvalidOperationException(
                "Wildlife-care input owner restore join failed: "
                + failureReason);
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
            throw new InvalidOperationException(
                "Wildlife-care input owner restore cannot complete.");
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }
}
