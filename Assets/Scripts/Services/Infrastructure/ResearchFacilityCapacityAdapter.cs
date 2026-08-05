using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("연구 시설 수용력")]
public sealed class BuildingResearchCapacityAbility : BuildingAbility
{
    [SerializeField] private List<ResearchFacilityContribution> contributions =
        new List<ResearchFacilityContribution>();

    public IReadOnlyList<ResearchFacilityContribution> Contributions =>
        contributions ??= new List<ResearchFacilityContribution>();

    public void Configure(IEnumerable<ResearchFacilityContribution> values)
    {
        contributions = (values ?? Array.Empty<ResearchFacilityContribution>())
            .Where(value => value.capacity > 0)
            .GroupBy(value => value.capability)
            .Select(group => new ResearchFacilityContribution(
                group.Key,
                group.Sum(value => Mathf.Max(1, value.capacity))))
            .OrderBy(value => value.capability)
            .ToList();
    }
}

public interface IResearchFacilityCapacityQuery
{
    int Version { get; }
    int GetAvailable(ResearchFacilityCapabilityId capability);
    bool MeetsRequirements(ResearchProjectSO project, out string blocker);
    string FormatRequirements(ResearchProjectSO project);
}

public sealed class ResearchFacilityCapacityQuery : IResearchFacilityCapacityQuery
{
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IPowerInfrastructureQuery electricalNetwork;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly int[] available =
        new int[Enum.GetValues(typeof(ResearchFacilityCapabilityId)).Length];
    private int cachedSourceVersion = int.MinValue;

    public ResearchFacilityCapacityQuery(
        IBuildingWorldQuery buildingWorld,
        IRoomLayoutCache roomLayoutCache,
        IPowerInfrastructureQuery electricalNetwork,
        IFacilityCandidateCache facilityCandidateCache)
    {
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.electricalNetwork = electricalNetwork
            ?? throw new ArgumentNullException(nameof(electricalNetwork));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
    }

    public int Version
    {
        get
        {
            EnsureSnapshot();
            return cachedSourceVersion;
        }
    }

    public int GetAvailable(ResearchFacilityCapabilityId capability)
    {
        EnsureSnapshot();
        int index = (int)capability;
        return index >= 0 && index < available.Length ? available[index] : 0;
    }

    public bool MeetsRequirements(
        ResearchProjectSO project,
        out string blocker)
    {
        blocker = string.Empty;
        if (project == null)
        {
            blocker = "연구 정의가 없습니다.";
            return false;
        }

        EnsureSnapshot();
        if (ResearchFacilityCapacityRules.MeetsRequirements(
                project.FacilityRequirements,
                GetAvailableWithoutRefresh,
                out ResearchFacilityRequirement[] missing))
        {
            return true;
        }

        blocker = $"연구 시설 수용력 부족: {string.Join(", ", missing.Select(FormatRequirement))}";
        return false;
    }

    public string FormatRequirements(ResearchProjectSO project)
    {
        if (project == null || project.FacilityRequirements.Count == 0)
        {
            return "연구 시설 · 요구 없음";
        }

        EnsureSnapshot();
        string requirements = string.Join(
            " · ",
            project.FacilityRequirements
            .OrderBy(requirement => requirement.capability)
            .Select(FormatRequirement));
        return $"연구 시설 · {requirements}";
    }

    public static string GetDisplayName(ResearchFacilityCapabilityId capability)
    {
        return capability switch
        {
            ResearchFacilityCapabilityId.Basic => "기초",
            ResearchFacilityCapabilityId.Archive => "기록",
            ResearchFacilityCapabilityId.Specimen => "표본",
            ResearchFacilityCapabilityId.Design => "설계",
            ResearchFacilityCapabilityId.Reagent => "시약",
            ResearchFacilityCapabilityId.Arcane => "비전",
            ResearchFacilityCapabilityId.Advanced => "고급",
            _ => capability.ToString()
        };
    }

    private string FormatRequirement(ResearchFacilityRequirement requirement)
    {
        return $"{GetDisplayName(requirement.capability)} "
            + $"{GetAvailableWithoutRefresh(requirement.capability)}/"
            + $"{Mathf.Max(1, requirement.requiredCount)}";
    }

    private int GetAvailableWithoutRefresh(ResearchFacilityCapabilityId capability)
    {
        int index = (int)capability;
        return index >= 0 && index < available.Length ? available[index] : 0;
    }

    private void EnsureSnapshot()
    {
        int sourceVersion = CalculateSourceVersion();
        if (sourceVersion == cachedSourceVersion)
        {
            return;
        }

        Array.Clear(available, 0, available.Length);
        foreach (BuildableObject building in buildingWorld.Buildings)
        {
            if (!IsOperationalResearchFacility(building))
            {
                continue;
            }

            BuildingResearchCapacityAbility ability =
                building.BuildingData.GetAbility<BuildingResearchCapacityAbility>();
            foreach (ResearchFacilityContribution contribution in ability.Contributions)
            {
                int index = (int)contribution.capability;
                if (index >= 0 && index < available.Length)
                {
                    available[index] += Mathf.Max(1, contribution.capacity);
                }
            }
        }

        cachedSourceVersion = sourceVersion;
    }

    private int CalculateSourceVersion()
    {
        unchecked
        {
            int hash = (buildingWorld.BuildingVersion * 397)
                ^ electricalNetwork.Version;
            hash = (hash * 31) ^ facilityCandidateCache.DynamicStateVersion;
            HashSet<Grid> grids = new HashSet<Grid>();
            foreach (BuildableObject building in buildingWorld.Buildings)
            {
                if (building?.Grid != null && grids.Add(building.Grid))
                {
                    hash = (hash * 31) ^ building.Grid.StructuralVersion;
                }
            }
            return hash;
        }
    }

    private bool IsOperationalResearchFacility(BuildableObject building)
    {
        if (building == null
            || building.isDestroy
            || !building.isActiveAndEnabled
            || building.IsDamaged
            || building.BuildingData?.GetAbility<BuildingResearchCapacityAbility>() == null
            || !roomLayoutCache.TryGetRoom(building, out RoomInstance room)
            || !room.IsUsable
            || room.IsSelfContained
            || !room.SupportsFacilityRole(FacilityRole.Research))
        {
            return false;
        }

        return building.BuildingData.GetAbility<BuildingPowerConsumerAbility>() == null
            || electricalNetwork.IsPowered(building);
    }
}
