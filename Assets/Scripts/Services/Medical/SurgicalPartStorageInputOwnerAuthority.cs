using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal interface ISurgicalPartStorageInputOwnerAuthority
{
    bool TryReconcile(out string failureReason);
    bool TryEnsure(
        string destinationId,
        Vector2Int position,
        out string failureReason);
    bool TryGetFuelItemId(out string itemId);
    string Fingerprint { get; }
}

internal sealed class SurgicalPartStorageInputOwnerAuthority :
    ISurgicalPartStorageInputOwnerAuthority
{
    internal const string OwnerDomain = "medical.surgical-part-storage";
    internal const long CapacityRevision = 1L;
    internal const string FuelPrefix = "surgery-organ-storage-fuel:";
    internal const string ReleaseReason =
        "medical-surgical-part-storage-owner-retired";

    private readonly IBuildingWorldQuery buildings;
    private readonly ISurgicalFacilityQuery facilities;
    private readonly IItemDefinitionCatalog catalog;
    private readonly IPhysicalItemMassQuery mass;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationReleaseService releases;
    private string fuelItemId = string.Empty;

    internal SurgicalPartStorageInputOwnerAuthority(
        IBuildingWorldQuery buildings,
        ISurgicalFacilityQuery facilities,
        IItemDefinitionCatalog catalog,
        IPhysicalItemMassQuery mass,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationReleaseService releases)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.releases = releases ?? throw new ArgumentNullException(nameof(releases));
    }

    public string Fingerprint { get; private set; } = string.Empty;

    public bool TryGetFuelItemId(out string itemId)
    {
        itemId = fuelItemId;
        return itemId.Length > 0;
    }

    public bool TryReconcile(out string failureReason)
    {
        failureReason = string.Empty;
        long massRevision = mass.AuthorityRevision;
        ItemDefinitionSO fuel = catalog.All
            .Where(value => value != null
                && value.StockCategory == StockCategory.Fuel)
            .Select(value => new
            {
                Definition = value,
                Grams = mass.GetDefinitionUnitMass(value.StableId).Value
            })
            .Where(value => value.Grams > 0L)
            .OrderByDescending(value => value.Grams)
            .ThenBy(value => value.Definition.ItemId, StringComparer.Ordinal)
            .Select(value => value.Definition)
            .FirstOrDefault();
        if (fuel == null || mass.AuthorityRevision != massRevision)
        {
            failureReason = "surgical-storage-positive-fuel-mass-missing";
            return false;
        }
        fuelItemId = fuel.ItemId;
        long fuelMass = mass.GetDefinitionUnitMass(fuel.StableId).Value;
        var desiredClaims = new List<FacilityBufferDestinationClaim>();
        var desiredProfiles = new List<FacilityBufferCapacityProfile>();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("surgical-part-storage-input-owner@1");
        digest.Append(massRevision);
        digest.Append(fuelItemId);
        digest.Append(fuelMass);
        foreach (BuildableObject storage in buildings.Buildings
                     .Where(value => value != null
                         && !value.isDestroy
                         && !value.IsGridDestroyed
                         && value.BuildingData?.GetAbility<BuildingOrganStorageAbility>() != null)
                     .OrderBy(value => facilities.GetFacilityId(value), StringComparer.Ordinal))
        {
            string facilityId = facilities.GetFacilityId(storage);
            BuildingStorageAbility authored = storage.BuildingData
                .GetAbility<BuildingStorageAbility>();
            long organCapacity = authored?.maxStoredMassGrams ?? 0L;
            if (string.IsNullOrWhiteSpace(facilityId) || organCapacity <= 0L)
            {
                failureReason = "surgical-storage-authored-mass-missing:" + facilityId;
                return false;
            }
            AddPair(facilityId, facilityId, storage.centerPos,
                organCapacity, desiredClaims, desiredProfiles, digest);
            BuildingOrganStorageAbility ability = storage.BuildingData
                .GetAbility<BuildingOrganStorageAbility>();
            if (ability.fuelPerDay > 0)
            {
                AddPair(FuelPrefix + facilityId, facilityId, storage.centerPos,
                    fuelMass, desiredClaims, desiredProfiles, digest);
            }
        }
        if (mass.AuthorityRevision != massRevision)
        {
            failureReason = "surgical-storage-mass-revision-changed";
            return false;
        }
        desiredClaims = desiredClaims.OrderBy(value => value.DestinationId,
            StringComparer.Ordinal).ToList();
        desiredProfiles = desiredProfiles.OrderBy(value => value.DestinationId,
            StringComparer.Ordinal).ToList();
        FacilityBufferDestinationClaim[] currentClaims = claims
            .CaptureAuthorityClaims().Where(value => value != null
                && value.OwnerDomain == OwnerDomain)
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal).ToArray();
        FacilityBufferCapacityProfile[] currentProfiles = capacities
            .CaptureAuthorityProfiles().Where(value => value != null
                && value.OwnerDomain == OwnerDomain)
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal).ToArray();
        if (currentClaims.Length != currentProfiles.Length)
        {
            failureReason = "surgical-storage-authority-pair-torn";
            return false;
        }
        for (int index = 0; index < currentClaims.Length; index++)
        {
            FacilityBufferDestinationClaim claim = currentClaims[index];
            FacilityBufferCapacityProfile profile = currentProfiles[index];
            int desiredIndex = desiredClaims.FindIndex(value =>
                value.DestinationId == claim.DestinationId);
            bool same = desiredIndex >= 0
                && ClaimsMatch(claim, desiredClaims[desiredIndex])
                && ProfilesMatch(profile, desiredProfiles[desiredIndex]);
            if (!same && !releases.TryReleaseAtOwnerPosition(
                    claim.DestinationId, claim.DropPosition, ReleaseReason,
                    out _, out string releaseFailure))
            {
                failureReason = "surgical-storage-release-failed:"
                    + claim.DestinationId + ":" + releaseFailure;
                return false;
            }
        }
        Fingerprint = digest.ComputeSha256();
        if (currentClaims.Length == desiredClaims.Count
            && currentClaims.Select((value, index) =>
                ClaimsMatch(value, desiredClaims[index])
                && ProfilesMatch(currentProfiles[index], desiredProfiles[index]))
                .All(value => value))
        {
            return true;
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            OwnerDomain, desiredClaims, desiredProfiles, out failureReason);
    }

    public bool TryEnsure(
        string destinationId,
        Vector2Int position,
        out string failureReason)
    {
        if (!TryReconcile(out failureReason)) return false;
        FacilityBufferDestinationClaim claim = claims.CaptureAuthorityClaims()
            .SingleOrDefault(value => value != null
                && value.OwnerDomain == OwnerDomain
                && value.DestinationId == destinationId
                && value.DropPosition == position);
        FacilityBufferCapacityProfile profile = capacities.CaptureAuthorityProfiles()
            .SingleOrDefault(value => value != null
                && value.OwnerDomain == OwnerDomain
                && value.DestinationId == destinationId
                && value.DropPosition == position);
        if (claim == null || profile == null
            || claim.AnchorKind != FacilityBufferDestinationAnchorKind.LiveFacility
            || claim.AdmissionPolicy != FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            || profile.MaxMassGrams <= 0L || profile.CapacityRevision != CapacityRevision)
        {
            failureReason = "surgical-storage-exact-pair-missing:" + destinationId;
            return false;
        }
        return true;
    }

    private static void AddPair(
        string destinationId, string facilityId, Vector2Int position,
        long capacity, ICollection<FacilityBufferDestinationClaim> claims,
        ICollection<FacilityBufferCapacityProfile> profiles,
        CanonicalSemanticDigestBuilder digest)
    {
        string operationId = "surgical-part-storage-input-owner:" + destinationId;
        claims.Add(new FacilityBufferDestinationClaim(destinationId, position,
            OwnerDomain, operationId, facilityId,
            FacilityBufferDestinationAnchorKind.LiveFacility,
            FacilityBufferDestinationAdmissionPolicy.ExactGramRequired));
        profiles.Add(new FacilityBufferCapacityProfile(destinationId, position,
            OwnerDomain, operationId, facilityId,
            new PhysicalMassGrams(capacity), CapacityRevision));
        digest.Append(destinationId);
        digest.Append(facilityId);
        digest.Append(position.x);
        digest.Append(position.y);
        digest.Append(capacity);
    }

    private static bool ClaimsMatch(
        FacilityBufferDestinationClaim left,
        FacilityBufferDestinationClaim right) =>
        left.DestinationId == right.DestinationId
        && left.DropPosition == right.DropPosition
        && left.OwnerOperationId == right.OwnerOperationId
        && left.OwnerFacilityId == right.OwnerFacilityId
        && left.AnchorKind == right.AnchorKind
        && left.AdmissionPolicy == right.AdmissionPolicy;

    private static bool ProfilesMatch(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left.DestinationId == right.DestinationId
        && left.DropPosition == right.DropPosition
        && left.OwnerOperationId == right.OwnerOperationId
        && left.OwnerFacilityId == right.OwnerFacilityId
        && left.MaxMassGrams == right.MaxMassGrams
        && left.CapacityRevision == right.CapacityRevision;
}
