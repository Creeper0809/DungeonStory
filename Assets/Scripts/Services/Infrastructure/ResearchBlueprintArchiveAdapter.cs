using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("연구 설계도 보관")]
public sealed class BuildingResearchArchiveAbility : BuildingAbility
{
    [Min(1), InspectorName("설계도 보관량")]
    public int capacity = 8;
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

    public ResearchBlueprintArchiveQuery(
        IBuildingWorldQuery buildingWorld,
        ICharacterWorldQuery characterWorld,
        IRoomLayoutCache roomLayoutCache,
        IWorldItemStackRuntime itemRuntime)
    {
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
    }

    public int Version
    {
        get
        {
            unchecked
            {
                return buildingWorld.BuildingVersion * 397
                    ^ itemRuntime.ItemStackVersion;
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
            BuildingResearchArchiveAbility ability =
                candidate.BuildingData?.GetAbility<BuildingResearchArchiveAbility>();
            int used = itemRuntime.GetAllStacks().Count(stack =>
                stack != null
                && stack.Quantity > 0
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    GetDestinationId(candidate),
                    StringComparison.Ordinal));
            return used < Mathf.Max(1, ability?.capacity ?? 1);
        });
        destinationId = archive != null ? GetDestinationId(archive) : string.Empty;
        return archive != null;
    }

    public static string GetDestinationId(BuildableObject archive)
    {
        if (archive == null)
        {
            return string.Empty;
        }

        return $"research-archive:{archive.RequirePersistentInstanceId().Value}";
    }

    private bool IsValidArchive(BuildableObject building)
    {
        if (building == null
            || !building.isActiveAndEnabled
            || building.BuildingData?.GetAbility<BuildingResearchArchiveAbility>() == null
            || !TryGetContainingRoom(building, out RoomInstance room))
        {
            return false;
        }

        return room.IsUsable
            && !room.IsSelfContained
            && room.SupportsFacilityRole(FacilityRole.Research);
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
