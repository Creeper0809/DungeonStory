using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IEquipmentModuleInputOwnerRuntime
{
    bool TryReconcileLive(out string failureReason);
    bool TryReconcileRestore(out string failureReason);
    bool TryValidateFacility(BuildableObject facility, out string failureReason);
    bool TryRequestItem(BuildableObject facility, string itemId, int quantity,
        out int requested, out string failureReason);
}

public static class EquipmentModuleInputOwnerAuthority
{
    public const string OwnerDomain = "combat.equipment-module";
    public const long CapacitySchemaRevision = 1L;
    public const string DestinationPrefix =
        ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
        + OwnerDomain + ":";

    public static string DestinationFor(string facilityPersistentId) =>
        DestinationPrefix + facilityPersistentId;
}

public sealed class EquipmentModuleInputOwnerRuntime :
    IEquipmentModuleInputOwnerRuntime
{
    private const string MaterialTestCouponItemId =
        "component:material-test-coupon";

    private readonly IBuildingWorldQuery buildings;
    private readonly ICombatEquipmentCatalog equipment;
    private readonly IPhysicalItemMassQuery mass;
    private readonly IWorldItemStackRuntime deliveries;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;

    public EquipmentModuleInputOwnerRuntime(
        IBuildingWorldQuery buildings,
        ICombatEquipmentCatalog equipment,
        IPhysicalItemMassQuery mass,
        IWorldItemStackRuntime deliveries,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.deliveries = deliveries ?? throw new ArgumentNullException(nameof(deliveries));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases ?? throw new ArgumentNullException(nameof(releases));
    }

    public bool TryReconcileLive(out string failureReason) =>
        TryReconcile(releaseRetired: true, out failureReason);

    public bool TryReconcileRestore(out string failureReason) =>
        TryReconcile(releaseRetired: false, out failureReason);

    public bool TryValidateFacility(BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryCreatePair(facility, out FacilityBufferDestinationClaim expectedClaim,
                out FacilityBufferCapacityProfile expectedProfile,
                out _, out failureReason))
            return false;
        FacilityBufferDestinationClaim[] actualClaims = claims
            .CaptureAuthorityClaims().Where(value => value != null
                && string.Equals(value.DestinationId,
                    expectedClaim.DestinationId, StringComparison.Ordinal)).ToArray();
        FacilityBufferCapacityProfile[] actualProfiles = capacities
            .CaptureAuthorityProfiles().Where(value => value != null
                && string.Equals(value.DestinationId,
                    expectedClaim.DestinationId, StringComparison.Ordinal)).ToArray();
        if (actualClaims.Length != 1 || actualProfiles.Length != 1
            || !ClaimsMatch(expectedClaim, actualClaims[0])
            || !ProfilesMatch(expectedProfile, actualProfiles[0]))
        {
            failureReason = "equipment-module-input-authority-mismatch";
            return false;
        }
        return true;
    }

    public bool TryRequestItem(BuildableObject facility, string itemId,
        int quantity, out int requested, out string failureReason)
    {
        requested = 0;
        if (!TryValidateFacility(facility, out failureReason)
            || string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || quantity <= 0)
        {
            if (failureReason.Length == 0)
                failureReason = "equipment-module-input-request-invalid";
            return false;
        }
        return deliveries.TryRequestItemDelivery(itemId, quantity,
            facility.centerPos,
            EquipmentModuleInputOwnerAuthority.DestinationFor(
                facility.PersistentInstanceId.Value),
            out requested, out failureReason);
    }

    private bool TryReconcile(bool releaseRetired, out string failureReason)
    {
        failureReason = string.Empty;
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (BuildableObject facility in (buildings.Buildings
                     ?? Array.Empty<BuildableObject>())
                 .Where(EquipmentProgressionFacilityContract.IsProgressionFacility)
                 .OrderBy(value => value.PersistentInstanceId.Value,
                     StringComparer.Ordinal))
        {
            if (!TryCreatePair(facility, out FacilityBufferDestinationClaim claim,
                    out FacilityBufferCapacityProfile profile,
                    out _, out failureReason))
                return false;
            desiredClaims.Add(claim);
            desiredProfiles.Add(profile);
        }
        if (desiredClaims.Select(value => value.DestinationId)
            .Distinct(StringComparer.Ordinal).Count() != desiredClaims.Count)
        {
            failureReason = "equipment-module-input-live-owner-duplicate";
            return false;
        }

        if (releaseRetired)
        {
            Dictionary<string, FacilityBufferCapacityProfile> desiredById =
                desiredProfiles.ToDictionary(value => value.DestinationId,
                    StringComparer.Ordinal);
            foreach (FacilityBufferCapacityProfile existing in capacities
                         .CaptureAuthorityProfiles()
                         .Where(value => value != null && string.Equals(
                             value.OwnerDomain,
                             EquipmentModuleInputOwnerAuthority.OwnerDomain,
                             StringComparison.Ordinal))
                         .OrderBy(value => value.DestinationId,
                             StringComparer.Ordinal))
            {
                bool retire = !desiredById.TryGetValue(existing.DestinationId,
                    out FacilityBufferCapacityProfile desired)
                    || desired.MaxMassGrams < existing.MaxMassGrams;
                if (retire && !releases.TryReleaseAtOwnerPosition(
                        existing.DestinationId, existing.DropPosition,
                        "equipment-module-input-owner-retired", out _,
                        out failureReason))
                    return false;
            }
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            EquipmentModuleInputOwnerAuthority.OwnerDomain,
            desiredClaims, desiredProfiles, out failureReason);
    }

    private bool TryCreatePair(BuildableObject facility,
        out FacilityBufferDestinationClaim claim,
        out FacilityBufferCapacityProfile profile,
        out string fingerprint,
        out string failureReason)
    {
        claim = null;
        profile = null;
        fingerprint = string.Empty;
        failureReason = string.Empty;
        if (facility == null || facility.isDestroy
            || !facility.PersistentInstanceId.IsValid
            || !EquipmentProgressionFacilityContract.IsProgressionFacility(facility)
            || mass.AuthorityRevision <= 0L)
        {
            failureReason = "equipment-module-input-live-facility-invalid";
            return false;
        }
        string facilityId = facility.PersistentInstanceId.Value;
        string destinationId =
            EquipmentModuleInputOwnerAuthority.DestinationFor(facilityId);
        string tag = facility.GetProductionWorkstationTag();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-equipment-module-input-capacity@1");
        digest.Append(destinationId);
        digest.Append(facilityId);
        digest.Append(tag);
        digest.Append(facility.centerPos.x);
        digest.Append(facility.centerPos.y);
        digest.Append(mass.AuthorityRevision);
        try
        {
            long moduleMass = GetGenericMass(
                PhysicalItemIds.ForEquipmentModule(), 1, digest);
            long maximumEquipmentMass = equipment.All
                .Where(value => value != null && value.Weight > 0f)
                .Select(value => PhysicalMassGrams
                    .FromCanonicalKilograms(value.Weight).Value)
                .DefaultIfEmpty(0L).Max();
            long capacity = tag switch
            {
                EquipmentProgressionWorkstationTags.Appraisal => checked(
                    moduleMass
                    + GetGenericMass(MaterialTestCouponItemId, 1, digest)
                    + GetGenericMass(DurableToolItemRules.InspectionGauge, 1, digest)
                    + GetGenericMass(DurableToolItemRules.RuneIdentificationLens, 1, digest)),
                EquipmentProgressionWorkstationTags.Restoration => moduleMass,
                EquipmentProgressionWorkstationTags.RuneTuning => moduleMass,
                EquipmentProgressionWorkstationTags.PrecisionFitting => checked(
                    moduleMass + maximumEquipmentMass),
                EquipmentProgressionWorkstationTags.LineageArchive => checked(
                    maximumEquipmentMass * 2L
                    + GetGenericMass(EquipmentProgressionItemIds.LineageSeal,
                        1, digest)),
                _ => 0L
            };
            if (capacity <= 0L)
            {
                failureReason = "equipment-module-input-capacity-not-positive";
                return false;
            }
            digest.Append(maximumEquipmentMass);
            digest.Append(capacity);
            fingerprint = digest.ComputeSha256();
            claim = new FacilityBufferDestinationClaim(destinationId,
                facility.centerPos,
                EquipmentModuleInputOwnerAuthority.OwnerDomain,
                facilityId, facilityId,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);
            profile = new FacilityBufferCapacityProfile(destinationId,
                facility.centerPos,
                EquipmentModuleInputOwnerAuthority.OwnerDomain,
                facilityId, facilityId, new PhysicalMassGrams(capacity),
                EquipmentModuleInputOwnerAuthority.CapacitySchemaRevision);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "equipment-module-input-projection-failed:"
                + exception.GetType().Name;
            return false;
        }
    }

    private long GetGenericMass(string itemId, int quantity,
        CanonicalSemanticDigestBuilder digest)
    {
        ItemDefinitionId definitionId = (ItemDefinitionId)itemId;
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            mass, definitionId, string.Empty,
            Array.Empty<ItemInstanceComponentSaveData>());
        long grams = mass.GetQuantityMass(definitionId, subject, quantity).Value;
        digest.Append(itemId);
        digest.Append(quantity);
        digest.Append(grams);
        return grams;
    }

    private static bool ClaimsMatch(FacilityBufferDestinationClaim left,
        FacilityBufferDestinationClaim right) =>
        left != null && right != null
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && left.DropPosition == right.DropPosition
        && string.Equals(left.OwnerDomain, right.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerOperationId, right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerFacilityId, right.OwnerFacilityId,
            StringComparison.Ordinal)
        && left.AnchorKind == right.AnchorKind
        && left.AdmissionPolicy == right.AdmissionPolicy;

    private static bool ProfilesMatch(FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left != null && right != null
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && left.DropPosition == right.DropPosition
        && string.Equals(left.OwnerDomain, right.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerOperationId, right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerFacilityId, right.OwnerFacilityId,
            StringComparison.Ordinal)
        && left.MaxMassGrams == right.MaxMassGrams
        && left.CapacityRevision == right.CapacityRevision;
}

public sealed class EquipmentModuleInputOwnerLifecycleRuntime :
    IStartable, ITickable, IDungeonSaveCaptureGuard
{
    private readonly IEquipmentModuleInputOwnerRuntime owner;
    private string unresolvedFailure = string.Empty;

    public EquipmentModuleInputOwnerLifecycleRuntime(
        IEquipmentModuleInputOwnerRuntime owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void Start() => Reconcile();
    public void Tick() => Reconcile();
    public void ValidateBeforeCapture()
    {
        Reconcile();
        if (unresolvedFailure.Length > 0)
            throw new InvalidOperationException(
                "Equipment-module input ownership is not capture-safe: "
                + unresolvedFailure);
    }
    private void Reconcile() => unresolvedFailure =
        owner.TryReconcileLive(out string failureReason)
            ? string.Empty : failureReason;
}

public sealed class EquipmentModuleInputOwnerRestoreParticipant :
    IDungeonRestoreTransactionParticipant
{
    private readonly IEquipmentModuleInputOwnerRuntime owner;
    private bool active;
    private bool published;

    public EquipmentModuleInputOwnerRestoreParticipant(
        IEquipmentModuleInputOwnerRuntime owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string ParticipantId =>
        "219.world.combat-equipment-module-input-owner";
    public void BeginRestoreCandidate()
    {
        if (active) throw new InvalidOperationException(
            "Equipment-module input-owner restore is already active.");
        active = true;
        published = false;
    }
    public void PublishRestoreCandidate()
    {
        string failureReason = string.Empty;
        if (!active || published || !owner.TryReconcileRestore(
                out failureReason))
            throw new InvalidOperationException(
                "Equipment-module input-owner restore join failed: "
                + (!active || published ? "transaction-state-invalid" : failureReason));
        published = true;
    }
    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }
    public void CompleteRestoreCandidate()
    {
        if (!active || !published) throw new InvalidOperationException(
            "Equipment-module input-owner restore cannot complete.");
        active = false;
        published = false;
    }
    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }
}
