using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ProductionSupportLinkSnapshot
{
    public BuildableObject Workstation { get; set; }
    public BuildableObject Support { get; set; }
    public string WorkstationTag { get; set; } = string.Empty;
    public string SupportId { get; set; } = string.Empty;
    public IReadOnlyList<string> FeatureTags { get; set; } =
        Array.Empty<string>();
}

public interface IProductionWorkshopRuntime
{
    int Version { get; }
    IReadOnlyList<ProductionSupportLinkSnapshot> GetLinks(
        BuildableObject workstation);
    bool TryGetLinkForSupport(
        BuildableObject support,
        out ProductionSupportLinkSnapshot link);
    bool HasRequiredSupports(
        BuildableObject workstation,
        IReadOnlyList<string> requiredFeatureTags,
        out string failureReason);
    bool TryResolveSupport(
        BuildableObject workstation,
        string featureTag,
        ProductionSupportKind? requiredKind,
        out BuildableObject support,
        out BuildingProductionSupportAbility ability);
}

public sealed class ProductionWorkshopRuntime : IProductionWorkshopRuntime
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IRoomLayoutCache rooms;
    private readonly Dictionary<BuildableObject, List<ProductionSupportLinkSnapshot>>
        linksByWorkstation =
            new Dictionary<BuildableObject, List<ProductionSupportLinkSnapshot>>();
    private readonly Dictionary<BuildableObject, ProductionSupportLinkSnapshot>
        linksBySupport =
            new Dictionary<BuildableObject, ProductionSupportLinkSnapshot>();
    private int observedBuildingVersion = int.MinValue;
    private readonly Dictionary<Grid, int> observedGridVersions =
        new Dictionary<Grid, int>();

    public ProductionWorkshopRuntime(
        IBuildingWorldQuery buildings,
        IRoomLayoutCache rooms)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
    }

    public int Version { get; private set; }

    public IReadOnlyList<ProductionSupportLinkSnapshot> GetLinks(
        BuildableObject workstation)
    {
        EnsureLinks();
        return workstation != null
            && linksByWorkstation.TryGetValue(
                workstation,
                out List<ProductionSupportLinkSnapshot> links)
                    ? links
                    : Array.Empty<ProductionSupportLinkSnapshot>();
    }

    public bool HasRequiredSupports(
        BuildableObject workstation,
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

            if (!TryResolveSupport(
                    workstation,
                    featureTag,
                    null,
                    out _,
                    out _))
            {
                failureReason = $"연결 시설 부족: {featureTag}";
                return false;
            }
        }

        return true;
    }

    public bool TryGetLinkForSupport(
        BuildableObject support,
        out ProductionSupportLinkSnapshot link)
    {
        EnsureLinks();
        link = null;
        return support != null && linksBySupport.TryGetValue(support, out link);
    }

    public bool TryResolveSupport(
        BuildableObject workstation,
        string featureTag,
        ProductionSupportKind? requiredKind,
        out BuildableObject support,
        out BuildingProductionSupportAbility ability)
    {
        support = null;
        ability = null;
        if (workstation == null || string.IsNullOrWhiteSpace(featureTag))
        {
            return false;
        }

        string normalized = featureTag.Trim();
        foreach (ProductionSupportLinkSnapshot link in GetLinks(workstation))
        {
            BuildingProductionSupportAbility candidate =
                link.Support?.BuildingData.GetProductionSupportAbility();
            if (candidate == null
                || requiredKind.HasValue && candidate.kind != requiredKind.Value
                || !candidate.Provides(normalized))
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
            foreach (Grid grid in worldBuildings
                         .Where(building => building?.Grid != null)
                         .Select(building => building.Grid)
                         .Distinct())
            {
                if (!observedGridVersions.TryGetValue(
                        grid,
                        out int version)
                    || version != grid.StructuralVersion)
                {
                    dirty = true;
                    break;
                }
            }
        }

        if (!dirty)
        {
            return;
        }

        Rebuild(worldBuildings);
    }

    private void Rebuild(IReadOnlyList<BuildableObject> worldBuildings)
    {
        linksByWorkstation.Clear();
        linksBySupport.Clear();
        observedGridVersions.Clear();

        BuildableObject[] workstations = worldBuildings
            .Where(IsOperational)
            .Where(building => building.BuildingData
                .GetProductionWorkstationAbility() != null)
            .OrderBy(GetStableBuildingKey, StringComparer.Ordinal)
            .ToArray();
        BuildableObject[] supports = worldBuildings
            .Where(IsOperational)
            .Where(building => building.BuildingData
                .GetProductionSupportAbility() != null)
            .OrderBy(GetStableBuildingKey, StringComparer.Ordinal)
            .ToArray();

        foreach (BuildableObject workstation in workstations)
        {
            linksByWorkstation[workstation] =
                new List<ProductionSupportLinkSnapshot>();
        }

        foreach (BuildableObject support in supports)
        {
            if (!rooms.TryGetRoom(support, out RoomInstance room)
                || room == null
                || !room.IsUsable)
            {
                continue;
            }

            BuildingProductionSupportAbility supportAbility =
                support.BuildingData.GetProductionSupportAbility();
            BuildableObject target = workstations
                .Where(workstation => workstation.Grid == support.Grid)
                .Where(workstation =>
                    rooms.TryGetRoom(workstation, out RoomInstance targetRoom)
                    && ReferenceEquals(room, targetRoom))
                 .Where(workstation => supportAbility.SupportsWorkstation(
                     workstation.GetProductionWorkstationTag()))
                 .Where(workstation => linksByWorkstation[workstation]
                     .Count(link => string.Equals(
                         link.SupportId,
                         supportAbility.SupportId,
                         StringComparison.Ordinal))
                     < supportAbility.MaximumLinkedInstancesPerWorkstation)
                 .OrderBy(workstation => ManhattanDistance(
                    workstation.centerPos,
                    support.centerPos))
                .ThenBy(GetStableBuildingKey, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target == null)
            {
                continue;
            }

            ProductionSupportLinkSnapshot link =
                new ProductionSupportLinkSnapshot
            {
                Workstation = target,
                Support = support,
                WorkstationTag = target.GetProductionWorkstationTag(),
                SupportId = supportAbility.SupportId,
                FeatureTags = (supportAbility.featureTags
                        ?? Array.Empty<string>())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(tag => tag, StringComparer.Ordinal)
                    .ToArray()
            };
            linksByWorkstation[target].Add(link);
            linksBySupport[support] = link;
        }

        foreach (List<ProductionSupportLinkSnapshot> links
                 in linksByWorkstation.Values)
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

    private static bool IsOperational(BuildableObject building)
    {
        return building != null
            && !building.IsGridDestroyed
            && building.BuildingData != null;
    }

    private static int ManhattanDistance(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

    private static string GetStableBuildingKey(BuildableObject building)
    {
        if (building == null)
        {
            return string.Empty;
        }

        return $"{building.id:D6}:{building.centerPos.x:D6}:"
            + $"{building.centerPos.y:D6}";
    }
}
