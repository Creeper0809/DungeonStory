using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

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
    public ResourceResearchProjectCatalog(IGameContentCatalog content)
        : this((content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<ResearchProjectSO>())
    {
    }

    public ResourceResearchProjectCatalog(IEnumerable<ResearchProjectSO> source)
    {
        ResearchProjectSO[] materialized = (source
                ?? Array.Empty<ResearchProjectSO>())
            .Where(project => project != null)
            .ToArray();
        ResearchProjectSO invalid = materialized.FirstOrDefault(
            project => !project.ProjectId.IsValid);
        if (invalid != null)
        {
            throw new InvalidOperationException(
                $"Research project '{invalid.name}' has no stable project ID.");
        }

        IGrouping<string, ResearchProjectSO> duplicateProject = materialized
            .GroupBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateProject != null)
        {
            throw new InvalidOperationException(
                $"Duplicate research project ID: {duplicateProject.Key}");
        }

        IGrouping<int, ResearchProjectSO> duplicateBlueprintOwner = materialized
            .Where(project => project.Blueprint != null
                && project.Blueprint.id > 0)
            .GroupBy(project => project.Blueprint.id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateBlueprintOwner != null)
        {
            throw new InvalidOperationException(
                $"Blueprint ID {duplicateBlueprintOwner.Key} is owned by multiple research projects: "
                + string.Join(", ", duplicateBlueprintOwner
                    .Select(project => project.ProjectId.Value)
                    .OrderBy(value => value, StringComparer.Ordinal)));
        }

        ResearchProjectSO[] ordered = materialized
            .OrderBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ResearchProjectSO> projectIndex = ordered
            .ToDictionary(
                project => project.ProjectId.Value,
                project => project,
                StringComparer.Ordinal);
        Dictionary<int, ResearchProjectSO> blueprintIndex = ordered
            .Where(project => project.Blueprint != null
                && project.Blueprint.id > 0)
            .ToDictionary(
                project => project.Blueprint.id,
                project => project);
        projects = ordered;
        byId = projectIndex;
        byBlueprintId = blueprintIndex;
    }

    public IReadOnlyList<ResearchProjectSO> Projects => projects;

    public bool TryGet(ResearchProjectId projectId, out ResearchProjectSO project) =>
        byId.TryGetValue(projectId.Value, out project);

    public bool TryGetForBlueprint(int blueprintId, out ResearchProjectSO project) =>
        byBlueprintId.TryGetValue(blueprintId, out project);

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = projects.SelectMany(project => project.ValidateDefinition()).ToList();
        HashSet<ResearchProjectSO> catalogSet = projects.ToHashSet();
        foreach (ResearchProjectSO project in projects)
        {
            foreach (ResearchProjectSO prerequisite in project.Prerequisites)
            {
                if (!catalogSet.Contains(prerequisite))
                {
                    errors.Add($"{project.ProjectId}: missing prerequisite {prerequisite?.ProjectId.Value}");
                }
            }
        }

        if (ResearchGraphAlgorithms.TryFindCycle(projects, out IReadOnlyList<ResearchProjectSO> cycle))
        {
            errors.Add($"Research graph cycle: {string.Join(" -> ", cycle.Select(item => item.ProjectId.Value))}");
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
            cycle = stack.Skip(Math.Max(0, start)).Concat(new[] { project }).ToArray();
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
