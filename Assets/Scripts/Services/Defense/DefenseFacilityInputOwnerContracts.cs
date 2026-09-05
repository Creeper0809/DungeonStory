using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class DefenseFacilityInputOwnerDescriptor
{
    public DefenseFacilityInputOwnerDescriptor(
        string facilityPersistentId,
        int buildingId,
        Vector2Int position,
        bool usesPhysicalSupply,
        string supplyItemId,
        StockCategory supplyCategory,
        int supplyCapacity,
        int capacityLevel)
    {
        if (!IsCanonical(facilityPersistentId))
            throw new ArgumentException(
                "Defense input ownership requires a canonical facility ID.",
                nameof(facilityPersistentId));
        if (buildingId <= 0)
            throw new ArgumentOutOfRangeException(nameof(buildingId));
        if (supplyCapacity < 0 || capacityLevel < 0)
            throw new ArgumentOutOfRangeException(
                supplyCapacity < 0 ? nameof(supplyCapacity) : nameof(capacityLevel));
        if (usesPhysicalSupply && !IsCanonical(supplyItemId))
            throw new ArgumentException(
                "Physical defense supply requires one canonical item ID.",
                nameof(supplyItemId));

        FacilityPersistentId = facilityPersistentId;
        BuildingId = buildingId;
        Position = position;
        UsesPhysicalSupply = usesPhysicalSupply;
        SupplyItemId = supplyItemId ?? string.Empty;
        SupplyCategory = supplyCategory;
        SupplyCapacity = supplyCapacity;
        CapacityLevel = capacityLevel;
    }

    public string FacilityPersistentId { get; }
    public int BuildingId { get; }
    public Vector2Int Position { get; }
    public bool UsesPhysicalSupply { get; }
    public string SupplyItemId { get; }
    public StockCategory SupplyCategory { get; }
    public int SupplyCapacity { get; }
    public int CapacityLevel { get; }
    public int EffectiveSupplyCapacity => checked(SupplyCapacity + CapacityLevel);

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IDefenseFacilityInputOwnerSource
{
    long Revision { get; }
    IReadOnlyList<DefenseFacilityInputOwnerDescriptor> Capture();
}

public sealed class DefenseFacilityInputOwnerBuildingSource :
    IDefenseFacilityInputOwnerSource
{
    private readonly IBuildingWorldQuery buildings;

    public DefenseFacilityInputOwnerBuildingSource(IBuildingWorldQuery buildings)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
    }

    public long Revision => buildings.BuildingVersion;

    public IReadOnlyList<DefenseFacilityInputOwnerDescriptor> Capture() =>
        (buildings.Buildings ?? Array.Empty<BuildableObject>())
        .OfType<DefenseFacility>()
        .Where(value => value != null
            && !value.isDestroy
            && !value.IsGridDestroyed
            && value.Defense?.IsDefenseFacility == true)
        .OrderBy(
            value => value.RequirePersistentInstanceId().Value,
            StringComparer.Ordinal)
        .Select(value =>
        {
            DefenseFacilityData data = value.Defense;
            DefenseFacilityGrowthData growth = data.growth
                ?? new DefenseFacilityGrowthData();
            return new DefenseFacilityInputOwnerDescriptor(
                value.RequirePersistentInstanceId().Value,
                value.id,
                value.centerPos,
                data.UsesPhysicalSupply,
                data.supplyItemId,
                data.supplyCategory,
                data.supplyCapacity,
                growth.capacityLevel);
        })
        .ToArray();
}

public sealed class DefenseFacilityInputOwnerProjection
{
    public DefenseFacilityInputOwnerProjection(
        IReadOnlyList<FacilityBufferDestinationClaim> claims,
        IReadOnlyList<FacilityBufferCapacityProfile> profiles)
    {
        Claims = claims ?? throw new ArgumentNullException(nameof(claims));
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public IReadOnlyList<FacilityBufferDestinationClaim> Claims { get; }
    public IReadOnlyList<FacilityBufferCapacityProfile> Profiles { get; }
}

public static class DefenseFacilityInputOwnerAuthority
{
    public const string OwnerDomain = "combat.defense-facility";
    public const string MixedDefenseAmmunitionBoxItemId =
        "supply:defense-mixed-ammo-box";
    public const int MixedDefenseAmmunitionUnitsPerBox = 8;
    public const long CapacitySchemaRevision = 1L;

    public static DefenseFacilityInputOwnerProjection BuildProjection(
        IEnumerable<DefenseFacilityInputOwnerDescriptor> source,
        IPhysicalItemMassQuery massQuery)
    {
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));

        DefenseFacilityInputOwnerDescriptor[] descriptors =
            (source ?? Array.Empty<DefenseFacilityInputOwnerDescriptor>())
            .OrderBy(value => value?.FacilityPersistentId, StringComparer.Ordinal)
            .ToArray();
        if (descriptors.Any(value => value == null)
            || descriptors.Select(value => value.FacilityPersistentId)
                .Distinct(StringComparer.Ordinal).Count() != descriptors.Length)
        {
            throw new InvalidOperationException(
                "Defense input owner descriptors must be non-null with unique facility IDs.");
        }

        long maintenanceMass = RequireUnitMass(
            DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId,
            massQuery);
        List<FacilityBufferDestinationClaim> claims = new();
        List<FacilityBufferCapacityProfile> profiles = new();
        foreach (DefenseFacilityInputOwnerDescriptor descriptor in descriptors)
        {
            AddPair(
                descriptor,
                BuildMaintenanceDestinationId(descriptor.FacilityPersistentId),
                BuildOwnerOperationId(descriptor.FacilityPersistentId, "maintenance"),
                maintenanceMass,
                claims,
                profiles);

            if (!descriptor.UsesPhysicalSupply)
                continue;
            if (descriptor.EffectiveSupplyCapacity <= 0)
            {
                throw new InvalidOperationException(
                    "Physical defense supply capacity must be positive: "
                    + descriptor.FacilityPersistentId);
            }

            long authoredMass = checked(
                RequireUnitMass(descriptor.SupplyItemId, massQuery)
                * descriptor.EffectiveSupplyCapacity);
            long maximumBatchMass = authoredMass;
            if (descriptor.SupplyCategory == StockCategory.Ammunition
                && descriptor.EffectiveSupplyCapacity
                    >= MixedDefenseAmmunitionUnitsPerBox)
            {
                int boxCount = descriptor.EffectiveSupplyCapacity
                    / MixedDefenseAmmunitionUnitsPerBox;
                long mixedBoxMass = checked(
                    RequireUnitMass(MixedDefenseAmmunitionBoxItemId, massQuery)
                    * boxCount);
                maximumBatchMass = Math.Max(maximumBatchMass, mixedBoxMass);
            }

            AddPair(
                descriptor,
                BuildSupplyDestinationId(descriptor.FacilityPersistentId),
                BuildOwnerOperationId(descriptor.FacilityPersistentId, "supply"),
                maximumBatchMass,
                claims,
                profiles);
        }

        FacilityBufferDestinationClaim[] orderedClaims = claims
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferCapacityProfile[] orderedProfiles = profiles
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (!orderedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    orderedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Defense input owner claim/profile projection is torn.");
        }
        return new DefenseFacilityInputOwnerProjection(
            orderedClaims,
            orderedProfiles);
    }

    public static string BuildSupplyDestinationId(string facilityId) =>
        BuildDestinationId("defense:", facilityId);

    public static string BuildMaintenanceDestinationId(string facilityId) =>
        BuildDestinationId("defense-maintenance:", facilityId);

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

    private static void AddPair(
        DefenseFacilityInputOwnerDescriptor descriptor,
        string destinationId,
        string operationId,
        long maxMassGrams,
        ICollection<FacilityBufferDestinationClaim> claims,
        ICollection<FacilityBufferCapacityProfile> profiles)
    {
        if (maxMassGrams <= 0L)
            throw new InvalidOperationException(
                "Defense input owner capacity must be positive: " + destinationId);
        claims.Add(new FacilityBufferDestinationClaim(
            destinationId,
            descriptor.Position,
            OwnerDomain,
            operationId,
            descriptor.FacilityPersistentId,
            FacilityBufferDestinationAnchorKind.LiveFacility,
            FacilityBufferDestinationAdmissionPolicy.ExactGramRequired));
        profiles.Add(new FacilityBufferCapacityProfile(
            destinationId,
            descriptor.Position,
            OwnerDomain,
            operationId,
            descriptor.FacilityPersistentId,
            new PhysicalMassGrams(maxMassGrams),
            CapacitySchemaRevision));
    }

    private static long RequireUnitMass(
        string itemId,
        IPhysicalItemMassQuery massQuery)
    {
        if (!IsCanonical(itemId))
            throw new InvalidOperationException(
                "Defense input item ID is not canonical: " + itemId);
        long mass = massQuery.GetDefinitionUnitMass((ItemDefinitionId)itemId).Value;
        if (mass <= 0L)
            throw new InvalidOperationException(
                "Defense input item mass must be positive: " + itemId);
        return mass;
    }

    private static string BuildDestinationId(string kind, string facilityId)
    {
        if (!IsCanonical(facilityId))
            throw new ArgumentException(
                "Defense input destination requires a canonical facility ID.",
                nameof(facilityId));
        return WorldItemStackRuntime.FacilityInputDestinationPrefix
            + kind
            + facilityId;
    }

    private static string BuildOwnerOperationId(
        string facilityId,
        string kind) =>
        "defense-input-owner:" + facilityId + ":" + kind;

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
