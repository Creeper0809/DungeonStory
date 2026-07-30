using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public enum ResearchBlueprintRule
{
    None = 0,
    Required = 1,
    Shortcut = 2
}

public enum ResearchField
{
    LifeAndSurvival = 0,
    CommerceAndCraft = 1,
    DefenseAndTactics = 2,
    RecordsAndArcane = 3,
    CaptivityAndEntertainment = 4,
    AuthorityAndHousing = 5,
    Agriculture = 6,
    Forestry = 7,
    Mining = 8,
    Husbandry = 9,
    Metallurgy = 10,
    Textiles = 11,
    Cuisine = 12,
    Pharmacology = 13,
    SurgeryAndTransplant = 14,
    IndustryAndAutomation = 15,
    WaterAndSanitation = 16
}

public enum ResearchNodeState
{
    Locked = 0,
    Available = 1,
    Queued = 2,
    Active = 3,
    Suspended = 4,
    BlueprintInTransit = 5,
    ShortcutAvailable = 6,
    Completed = 7
}

[Serializable]
public readonly struct ResearchProjectId : IEquatable<ResearchProjectId>
{
    [SerializeField] private readonly string value;

    public ResearchProjectId(string value)
    {
        this.value = Normalize(value);
    }

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public bool Equals(ResearchProjectId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is ResearchProjectId other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static implicit operator ResearchProjectId(string value) => new ResearchProjectId(value);
    public static implicit operator string(ResearchProjectId id) => id.Value;

    public static string Normalize(string candidate) =>
        candidate?.Trim() ?? string.Empty;
}

public interface IResearchProjectCatalog
{
    IReadOnlyList<ResearchProjectSO> Projects { get; }
    bool TryGet(ResearchProjectId projectId, out ResearchProjectSO project);
    bool TryGetForBlueprint(int blueprintId, out ResearchProjectSO project);
    IReadOnlyList<string> Validate();
}

public sealed class ResourceResearchProjectCatalog : IResearchProjectCatalog
{
    private readonly IReadOnlyList<ResearchProjectSO> projects;
    private readonly IReadOnlyDictionary<string, ResearchProjectSO> byId;
    private readonly IReadOnlyDictionary<int, ResearchProjectSO> byBlueprintId;

    [Inject]
    public ResourceResearchProjectCatalog(IResourcesAssetLoader resourcesAssetLoader)
        : this(resourcesAssetLoader?.LoadAllOptional<ResearchProjectSO>(ResearchProjectSO.ResourcePath))
    {
    }

    public ResourceResearchProjectCatalog(IEnumerable<ResearchProjectSO> source)
    {
        projects = (source ?? Array.Empty<ResearchProjectSO>())
            .Where(project => project != null)
            .OrderBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .ToArray();
        byId = projects
            .Where(project => project.ProjectId.IsValid)
            .GroupBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        byBlueprintId = projects
            .Where(project => project.Blueprint != null)
            .GroupBy(project => project.Blueprint.id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public IReadOnlyList<ResearchProjectSO> Projects => projects;

    public bool TryGet(ResearchProjectId projectId, out ResearchProjectSO project) =>
        byId.TryGetValue(projectId.Value, out project);

    public bool TryGetForBlueprint(int blueprintId, out ResearchProjectSO project) =>
        byBlueprintId.TryGetValue(blueprintId, out project);

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = projects.SelectMany(project => project.ValidateDefinition()).ToList();
        foreach (IGrouping<string, ResearchProjectSO> duplicate in projects
                     .GroupBy(project => project.ProjectId.Value, StringComparer.Ordinal)
                     .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
        {
            errors.Add($"중복 연구 프로젝트 ID: {duplicate.Key}");
        }

        HashSet<ResearchProjectSO> catalogSet = projects.ToHashSet();
        foreach (ResearchProjectSO project in projects)
        {
            foreach (ResearchProjectSO prerequisite in project.Prerequisites)
            {
                if (!catalogSet.Contains(prerequisite))
                {
                    errors.Add($"{project.ProjectId}: 카탈로그 밖 선행 연구 {prerequisite?.ProjectId.Value}");
                }
            }
        }

        if (ResearchGraphAlgorithms.TryFindCycle(projects, out IReadOnlyList<ResearchProjectSO> cycle))
        {
            errors.Add($"연구 그래프 순환: {string.Join(" -> ", cycle.Select(item => item.ProjectId.Value))}");
        }

        return errors;
    }
}

public static class ResearchGraphAlgorithms
{
    public static bool TryFindCycle(
        IEnumerable<ResearchProjectSO> projects,
        out IReadOnlyList<ResearchProjectSO> cycle)
    {
        Dictionary<ResearchProjectSO, byte> marks = new Dictionary<ResearchProjectSO, byte>();
        List<ResearchProjectSO> stack = new List<ResearchProjectSO>();
        foreach (ResearchProjectSO project in (projects ?? Array.Empty<ResearchProjectSO>())
                     .Where(item => item != null)
                     .OrderBy(item => item.ProjectId.Value, StringComparer.Ordinal))
        {
            if (Visit(project, marks, stack, out cycle))
            {
                return true;
            }
        }

        cycle = Array.Empty<ResearchProjectSO>();
        return false;
    }

    private static bool Visit(
        ResearchProjectSO project,
        IDictionary<ResearchProjectSO, byte> marks,
        IList<ResearchProjectSO> stack,
        out IReadOnlyList<ResearchProjectSO> cycle)
    {
        if (marks.TryGetValue(project, out byte mark))
        {
            if (mark == 2)
            {
                cycle = Array.Empty<ResearchProjectSO>();
                return false;
            }

            int start = stack.IndexOf(project);
            cycle = stack.Skip(Mathf.Max(0, start)).Concat(new[] { project }).ToArray();
            return true;
        }

        marks[project] = 1;
        stack.Add(project);
        foreach (ResearchProjectSO prerequisite in project.Prerequisites
                     .Where(item => item != null)
                     .OrderBy(item => item.ProjectId.Value, StringComparer.Ordinal))
        {
            if (Visit(prerequisite, marks, stack, out cycle))
            {
                return true;
            }
        }

        stack.RemoveAt(stack.Count - 1);
        marks[project] = 2;
        cycle = Array.Empty<ResearchProjectSO>();
        return false;
    }
}
