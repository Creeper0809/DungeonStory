using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ServiceSupportLinkSnapshot
{
    public BuildableObject Hub { get; set; }
    public BuildableObject Support { get; set; }
    public string HubTag { get; set; } = string.Empty;
    public string SupportId { get; set; } = string.Empty;
    public IReadOnlyList<string> FeatureTags { get; set; } =
        Array.Empty<string>();
}

public interface IServiceRoomLinkRuntime
{
    int Version { get; }
    IReadOnlyList<ServiceSupportLinkSnapshot> GetLinks(BuildableObject hub);
    bool TryGetLinkForSupport(
        BuildableObject support,
        out ServiceSupportLinkSnapshot link);
    bool HasFeatures(
        BuildableObject hub,
        IReadOnlyList<string> requiredFeatureTags,
        out string failureReason);
    bool TryResolveFeature(
        BuildableObject hub,
        string featureTag,
        out BuildableObject support,
        out BuildingServiceSupportAbility ability);
}

public sealed class ServiceRoomLinkRuntime : IServiceRoomLinkRuntime
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IRoomLayoutCache rooms;
    private readonly Dictionary<BuildableObject, List<ServiceSupportLinkSnapshot>>
        linksByHub =
            new Dictionary<BuildableObject, List<ServiceSupportLinkSnapshot>>();
    private readonly Dictionary<BuildableObject, ServiceSupportLinkSnapshot>
        linksBySupport =
            new Dictionary<BuildableObject, ServiceSupportLinkSnapshot>();
    private readonly Dictionary<Grid, int> observedGridVersions =
        new Dictionary<Grid, int>();
    private int observedBuildingVersion = int.MinValue;

    public ServiceRoomLinkRuntime(
        IBuildingWorldQuery buildings,
        IRoomLayoutCache rooms)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
    }

    public int Version { get; private set; }

    public IReadOnlyList<ServiceSupportLinkSnapshot> GetLinks(
        BuildableObject hub)
    {
        EnsureLinks();
        return hub != null
            && linksByHub.TryGetValue(
                hub,
                out List<ServiceSupportLinkSnapshot> links)
                    ? links
                    : Array.Empty<ServiceSupportLinkSnapshot>();
    }

    public bool TryGetLinkForSupport(
        BuildableObject support,
        out ServiceSupportLinkSnapshot link)
    {
        EnsureLinks();
        link = null;
        return support != null
            && linksBySupport.TryGetValue(support, out link);
    }

    public bool HasFeatures(
        BuildableObject hub,
        IReadOnlyList<string> requiredFeatureTags,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (requiredFeatureTags == null || requiredFeatureTags.Count == 0)
        {
            return true;
        }

        for (int index = 0; index < requiredFeatureTags.Count; index++)
        {
            string featureTag = requiredFeatureTags[index]?.Trim();
            if (string.IsNullOrWhiteSpace(featureTag))
            {
                continue;
            }

            if (!TryResolveFeature(
                    hub,
                    featureTag,
                    out _,
                    out _))
            {
                failureReason = $"같은 방에 연결된 '{featureTag}' 시설이 없습니다.";
                return false;
            }
        }

        return true;
    }

    public bool TryResolveFeature(
        BuildableObject hub,
        string featureTag,
        out BuildableObject support,
        out BuildingServiceSupportAbility ability)
    {
        support = null;
        ability = null;
        if (hub == null || string.IsNullOrWhiteSpace(featureTag))
        {
            return false;
        }

        string normalized = featureTag.Trim();
        foreach (ServiceSupportLinkSnapshot link in GetLinks(hub))
        {
            BuildingServiceSupportAbility candidate =
                link.Support.GetServiceSupportAbility();
            if (candidate == null || !candidate.Provides(normalized))
            {
                continue;
            }

            support = link.Support;
            ability = candidate;
            return true;
        }

        return false;
    }

    private void EnsureLinks()
    {
        IReadOnlyList<BuildableObject> worldBuildings =
            buildings.Buildings ?? Array.Empty<BuildableObject>();
        bool dirty = observedBuildingVersion != buildings.BuildingVersion;
        if (!dirty)
        {
            Grid[] currentGrids = worldBuildings
                .Where(building => building?.Grid != null)
                .Select(building => building.Grid)
                .Distinct()
                .ToArray();
            dirty = currentGrids.Length != observedGridVersions.Count;
            for (int index = 0; !dirty && index < currentGrids.Length; index++)
            {
                Grid grid = currentGrids[index];
                dirty = !observedGridVersions.TryGetValue(
                        grid,
                        out int version)
                    || version != grid.StructuralVersion;
            }
        }

        if (dirty)
        {
            Rebuild(worldBuildings);
        }
    }

    private void Rebuild(IReadOnlyList<BuildableObject> worldBuildings)
    {
        linksByHub.Clear();
        linksBySupport.Clear();
        observedGridVersions.Clear();

        BuildableObject[] hubs = worldBuildings
            .Where(IsOperational)
            .Where(building => building.GetServiceHubAbility() != null)
            .OrderBy(GetStableBuildingKey, StringComparer.Ordinal)
            .ToArray();
        BuildableObject[] supports = worldBuildings
            .Where(IsOperational)
            .Where(building => building.GetServiceSupportAbility() != null)
            .OrderBy(GetStableBuildingKey, StringComparer.Ordinal)
            .ToArray();

        foreach (BuildableObject hub in hubs)
        {
            linksByHub[hub] = new List<ServiceSupportLinkSnapshot>();
        }

        foreach (BuildableObject support in supports)
        {
            if (!rooms.TryGetRoom(support, out RoomInstance room)
                || room == null
                || !room.IsUsable)
            {
                continue;
            }

            BuildingServiceSupportAbility supportAbility =
                support.GetServiceSupportAbility();
            BuildableObject target = hubs
                .Where(hub => hub.Grid == support.Grid)
                .Where(hub =>
                    rooms.TryGetRoom(hub, out RoomInstance targetRoom)
                    && ReferenceEquals(room, targetRoom))
                .Where(hub => supportAbility.SupportsHub(
                    hub.GetServiceHubAbility().ServiceHubTag))
                .OrderBy(hub => ManhattanDistance(
                    hub.centerPos,
                    support.centerPos))
                .ThenBy(GetStableBuildingKey, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target == null)
            {
                continue;
            }

            ServiceSupportLinkSnapshot link =
                new ServiceSupportLinkSnapshot
                {
                    Hub = target,
                    Support = support,
                    HubTag = target.GetServiceHubAbility().ServiceHubTag,
                    SupportId = supportAbility.SupportId,
                    FeatureTags = (supportAbility.featureTags
                            ?? Array.Empty<string>())
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Select(tag => tag.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(tag => tag, StringComparer.Ordinal)
                        .ToArray()
                };
            linksByHub[target].Add(link);
            linksBySupport[support] = link;
        }

        foreach (List<ServiceSupportLinkSnapshot> links in linksByHub.Values)
        {
            links.Sort((left, right) => string.CompareOrdinal(
                GetStableBuildingKey(left.Support),
                GetStableBuildingKey(right.Support)));
        }

        foreach (Grid grid in worldBuildings
                     .Where(building => building?.Grid != null)
                     .Select(building => building.Grid)
                     .Distinct())
        {
            observedGridVersions[grid] = grid.StructuralVersion;
        }

        observedBuildingVersion = buildings.BuildingVersion;
        unchecked
        {
            Version++;
        }
    }

    private static bool IsOperational(BuildableObject building) =>
        building != null
        && !building.IsGridDestroyed
        && building.BuildingData != null;

    private static int ManhattanDistance(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    internal static string GetStableBuildingKey(BuildableObject building)
    {
        if (building == null)
        {
            return string.Empty;
        }

        string persistentId =
            building.GetComponent<FacilityEvolutionStateComponent>()
                ?.FacilityPersistentId?.Trim() ?? string.Empty;
        return persistentId.Length > 0
            ? persistentId
            : $"{building.id:D6}:{building.centerPos.x:D6}:"
                + $"{building.centerPos.y:D6}";
    }
}
