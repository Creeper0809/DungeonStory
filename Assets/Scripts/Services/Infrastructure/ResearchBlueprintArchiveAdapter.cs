using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("연구 설계도 보관")]
public sealed class BuildingResearchArchiveAbility : BuildingAbility
{
    [Min(1), InspectorName("설계도 보관량")]
    public int capacity = 8;
}

public static class ResearchBlueprintArchiveDestinationAuthority
{
    public const string OwnerDomain = "research.blueprint-archive";
    public const long CapacitySchemaRevision = 1L;

    public static FacilityBufferDestinationClaim[] BuildClaims(
        IEnumerable<BuildableObject> buildings) =>
        (buildings ?? Array.Empty<BuildableObject>())
        .Where(IsAuthoredArchiveFacility)
        .OrderBy(building => building.RequirePersistentInstanceId().Value,
            StringComparer.Ordinal)
        .Select(CreateClaim)
        .ToArray();

    public static FacilityBufferDestinationClaim CreateClaim(
        BuildableObject archive)
    {
        if (!IsAuthoredArchiveFacility(archive))
        {
            throw new InvalidOperationException(
                "Research archive destination requires a live authored facility authority.");
        }

        string facilityId = archive.RequirePersistentInstanceId().Value;
        string destinationId = BuildDestinationId(facilityId);
        return new FacilityBufferDestinationClaim(
            destinationId,
            archive.centerPos,
            OwnerDomain,
            destinationId,
            facilityId,
            FacilityBufferDestinationAnchorKind.LiveBuilding,
            FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);
    }

    public static FacilityBufferCapacityProfile[] BuildProfiles(
        IEnumerable<BuildableObject> buildings,
        IEnumerable<FacilityBlueprintSO> blueprints,
        IPhysicalItemMassQuery massQuery)
    {
        BuildableObject[] archives = (buildings ?? Array.Empty<BuildableObject>())
            .Where(IsAuthoredArchiveFacility)
            .OrderBy(
                building => building.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .ToArray();
        if (archives.Length == 0)
        {
            return Array.Empty<FacilityBufferCapacityProfile>();
        }

        long maximumBlueprintUnitMass = ResolveMaximumBlueprintUnitMass(
            blueprints,
            massQuery);
        return archives.Select(archive =>
        {
            BuildingResearchArchiveAbility ability = archive.BuildingData
                .GetAbility<BuildingResearchArchiveAbility>();
            if (ability == null || ability.capacity <= 0)
            {
                throw new InvalidOperationException(
                    "Research archive capacity must be positive.");
            }

            string facilityId = archive.RequirePersistentInstanceId().Value;
            string destinationId = BuildDestinationId(facilityId);
            long maxMassGrams = checked(
                maximumBlueprintUnitMass * ability.capacity);
            return new FacilityBufferCapacityProfile(
                destinationId,
                archive.centerPos,
                OwnerDomain,
                destinationId,
                facilityId,
                new PhysicalMassGrams(maxMassGrams),
                CapacitySchemaRevision);
        }).ToArray();
    }

    public static long ResolveMaximumBlueprintUnitMass(
        IEnumerable<FacilityBlueprintSO> blueprints,
        IPhysicalItemMassQuery massQuery)
    {
        if (massQuery == null)
        {
            throw new ArgumentNullException(nameof(massQuery));
        }

        string[] itemIds = (blueprints ?? Array.Empty<FacilityBlueprintSO>())
            .Where(blueprint => blueprint != null)
            .Select(blueprint => blueprint.PhysicalItemId)
            .OrderBy(itemId => itemId, StringComparer.Ordinal)
            .ToArray();
        if (itemIds.Length == 0
            || itemIds.Any(itemId => string.IsNullOrWhiteSpace(itemId)
                || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
            || itemIds.Distinct(StringComparer.Ordinal).Count() != itemIds.Length)
        {
            throw new InvalidOperationException(
                "Research archive requires unique canonical physical blueprint item IDs.");
        }

        long[] unitMasses = itemIds.Select(itemId => massQuery
                .GetDefinitionUnitMass((ItemDefinitionId)itemId)
                .Value)
            .ToArray();
        if (unitMasses.Any(unitMass => unitMass <= 0L))
        {
            throw new InvalidOperationException(
                "Research archive blueprint unit mass must be positive.");
        }

        return unitMasses.Max();
    }

    public static string BuildDestinationId(string facilityId)
    {
        if (string.IsNullOrWhiteSpace(facilityId)
            || !string.Equals(
                facilityId,
                facilityId.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Research archive destination requires a canonical facility id.");
        }

        return ReservedTargetDestinationIdentity.ResearchArchivePrefix
            + facilityId;
    }

    public static bool IsAuthoredArchiveFacility(BuildableObject building) =>
        building != null
        && !building.IsGridDestroyed
        && building.BuildingData != null
        && building.BuildingData
            .GetAbility<BuildingResearchArchiveAbility>() != null;

    public static bool IsEligibleRoom(RoomInstance room) =>
        room != null
        && room.IsUsable
        && !room.IsSelfContained
        && room.SupportsFacilityRole(FacilityRole.Research);

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

public interface IResearchBlueprintArchiveQuery
{
    int Version { get; }
    ResearchBlueprintArchiveStatus GetStatus(FacilityBlueprintSO blueprint);
    IReadOnlyList<BuildableObject> GetValidArchives();
    bool TryGetPreferredArchive(
        FacilityBlueprintSO blueprint,
        out BuildableObject archive,
        out string destinationId);
}

public sealed class ResearchBlueprintArchiveQuery : IResearchBlueprintArchiveQuery
{
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IFacilityShopCatalog facilityCatalog;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferPhysicalOccupancyQuery occupancy;
    private readonly IFacilityBufferMassCapacityQuery capacities;

    public ResearchBlueprintArchiveQuery(
        IBuildingWorldQuery buildingWorld,
        ICharacterWorldQuery characterWorld,
        IRoomLayoutCache roomLayoutCache,
        IWorldItemStackRuntime itemRuntime,
        IFacilityShopCatalog facilityCatalog,
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferPhysicalOccupancyQuery occupancy,
        IFacilityBufferMassCapacityQuery capacities)
    {
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.facilityCatalog = facilityCatalog
            ?? throw new ArgumentNullException(nameof(facilityCatalog));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.occupancy = occupancy
            ?? throw new ArgumentNullException(nameof(occupancy));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
    }

    public int Version
    {
        get
        {
            unchecked
            {
                return buildingWorld.BuildingVersion * 397
                    ^ itemRuntime.ItemStackVersion * 31
                    ^ capacities.Revision.GetHashCode();
            }
        }
    }

    public ResearchBlueprintArchiveStatus GetStatus(FacilityBlueprintSO blueprint)
    {
        if (blueprint == null)
        {
            return new ResearchBlueprintArchiveStatus(
                false,
                false,
                string.Empty,
                "필요한 설계도 정의가 없습니다.");
        }

        string itemId = blueprint.PhysicalItemId;
        foreach (BuildableObject archive in GetValidArchives())
        {
            string destinationId = GetDestinationId(archive);
            bool archived = itemRuntime.GetAllStacks().Any(stack =>
                stack != null
                && stack.Quantity > 0
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
            if (archived)
            {
                return new ResearchBlueprintArchiveStatus(
                    true,
                    false,
                    GetArchiveName(archive),
                    string.Empty);
            }
        }

        WorldItemStackSnapshot transit = itemRuntime.GetAllStacks().FirstOrDefault(stack =>
            stack != null
            && stack.Quantity > 0
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal));
        if (transit != null)
        {
            string message = GetValidArchives().Count == 0
                ? "사용 가능한 연구실 보관대가 없습니다."
                : "설계도를 연구실 보관대로 운반 중입니다.";
            return new ResearchBlueprintArchiveStatus(
                false,
                true,
                $"격자 ({transit.Position.x}, {transit.Position.y})",
                message);
        }

        CharacterActor carrier = characterWorld.Characters.FirstOrDefault(actor =>
            actor != null
            && !actor.IsDead
            && actor.GetComponent<CharacterCarryInventory>()?.CountItem(itemId) > 0);
        if (carrier != null)
        {
            string carrierName = carrier.Identity?.DisplayName;
            if (string.IsNullOrWhiteSpace(carrierName))
            {
                carrierName = carrier.name;
            }

            return new ResearchBlueprintArchiveStatus(
                false,
                true,
                $"{carrierName} 운반 중",
                GetValidArchives().Count == 0
                    ? "사용 가능한 연구실 보관대가 없습니다."
                    : "설계도를 연구실 보관대로 운반 중입니다.");
        }

        return new ResearchBlueprintArchiveStatus(
            false,
            false,
            string.Empty,
            "물리 설계도를 보유하지 않았습니다.");
    }

    public IReadOnlyList<BuildableObject> GetValidArchives()
    {
        return buildingWorld.Buildings
            .Where(IsValidArchive)
            .OrderBy(building => building.centerPos.y)
            .ThenBy(building => building.centerPos.x)
            .ToArray();
    }

    public bool TryGetPreferredArchive(
        FacilityBlueprintSO blueprint,
        out BuildableObject archive,
        out string destinationId)
    {
        archive = GetValidArchives().FirstOrDefault(candidate =>
        {
            if (!IsCatalogBlueprint(blueprint))
            {
                return false;
            }

            string candidateDestination = GetDestinationId(candidate);
            if (!capacities.TryGetCapacity(
                    candidateDestination,
                    candidate.centerPos,
                    out FacilityBufferMassCapacitySnapshot capacity))
            {
                return false;
            }

            long requestedMass = massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)blueprint.PhysicalItemId).Value;
            FacilityBufferPhysicalOccupancySnapshot used =
                occupancy.Capture(candidateDestination);
            return checked(
                used.TotalMassGrams
                + capacity.ReservedMassGrams
                + requestedMass) <= capacity.Profile.MaxMassGrams;
        });
        destinationId = archive != null ? GetDestinationId(archive) : string.Empty;
        return archive != null;
    }

    private bool IsCatalogBlueprint(FacilityBlueprintSO blueprint) =>
        blueprint != null
        && facilityCatalog.Blueprints.Any(candidate =>
            candidate != null
            && candidate.id == blueprint.id
            && string.Equals(
                candidate.PhysicalItemId,
                blueprint.PhysicalItemId,
                StringComparison.Ordinal));

    public static string GetDestinationId(BuildableObject archive)
    {
        if (archive == null)
        {
            return string.Empty;
        }

        return ResearchBlueprintArchiveDestinationAuthority.BuildDestinationId(
            archive.RequirePersistentInstanceId().Value);
    }

    private bool IsValidArchive(BuildableObject building)
    {
        if (building == null
            || !building.isActiveAndEnabled
            || !ResearchBlueprintArchiveDestinationAuthority
                .IsAuthoredArchiveFacility(building)
            || !TryGetContainingRoom(building, out RoomInstance room))
        {
            return false;
        }

        return ResearchBlueprintArchiveDestinationAuthority.IsEligibleRoom(room);
    }

    private bool TryGetContainingRoom(
        BuildableObject building,
        out RoomInstance room)
    {
        room = null;
        if (building == null || building.Grid == null)
        {
            return false;
        }

        if (roomLayoutCache.TryGetRoom(building, out room))
        {
            return true;
        }

        IReadOnlyList<Vector2Int> occupiedCells = building.buildPoses;
        if (occupiedCells != null)
        {
            foreach (Vector2Int cell in occupiedCells)
            {
                if (roomLayoutCache.TryGetRoom(building.Grid, cell, out room))
                {
                    return true;
                }
            }
        }

        return roomLayoutCache.TryGetRoom(
            building.Grid,
            building.centerPos,
            out room);
    }

    private static string GetArchiveName(BuildableObject archive)
    {
        string name = archive?.BuildingData?.objectName;
        return string.IsNullOrWhiteSpace(name) ? "연구용책장" : name;
    }
}
