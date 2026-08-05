using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ResearchProjectCoordinatorRules
{
    public static ResearchNodeEvaluation EvaluateNodeState(
        bool projectExists,
        bool completed,
        bool active,
        bool queued,
        bool queueSuspended,
        string queueBlocker,
        ResearchBlueprintRule blueprintRule,
        bool blueprintArchived,
        bool blueprintInTransit,
        string blueprintBlocker,
        bool prerequisitesComplete,
        string prerequisiteBlocker,
        bool facilityRequirementsMet,
        string facilityBlocker)
    {
        if (!projectExists)
        {
            return new ResearchNodeEvaluation(
                ResearchNodeState.Locked,
                prerequisiteBlocker);
        }
        if (completed)
        {
            return new ResearchNodeEvaluation(ResearchNodeState.Completed);
        }
        if (active)
        {
            return new ResearchNodeEvaluation(ResearchNodeState.Active);
        }
        if (queued)
        {
            return new ResearchNodeEvaluation(
                queueSuspended
                    ? ResearchNodeState.Suspended
                    : ResearchNodeState.Queued,
                queueBlocker);
        }
        if (blueprintRule == ResearchBlueprintRule.Shortcut
            && blueprintArchived)
        {
            return facilityRequirementsMet
                ? new ResearchNodeEvaluation(ResearchNodeState.ShortcutAvailable)
                : new ResearchNodeEvaluation(
                    ResearchNodeState.Locked,
                    facilityBlocker);
        }
        if (!prerequisitesComplete)
        {
            return new ResearchNodeEvaluation(
                ResearchNodeState.Locked,
                prerequisiteBlocker);
        }
        if (blueprintRule == ResearchBlueprintRule.Required
            && !blueprintArchived)
        {
            return new ResearchNodeEvaluation(
                blueprintInTransit
                    ? ResearchNodeState.BlueprintInTransit
                    : ResearchNodeState.Locked,
                blueprintBlocker);
        }
        return facilityRequirementsMet
            ? new ResearchNodeEvaluation(ResearchNodeState.Available)
            : new ResearchNodeEvaluation(
                ResearchNodeState.Locked,
                facilityBlocker);
    }

    public static bool ArePrerequisitesCompleted(
        ResearchProjectRuntimeState state,
        IResearchProjectDefinition project)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return project != null && project.PrerequisiteIds.All(state.IsCompleted);
    }

    public static ResearchProjectId FindFirstMissingPrerequisite(
        ResearchProjectRuntimeState state,
        IResearchProjectDefinition project)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return project?.PrerequisiteIds.FirstOrDefault(id => !state.IsCompleted(id))
            ?? default;
    }

    public static IReadOnlyList<ResearchProjectId> CollectDependencyOrder(
        ResearchProjectId target,
        Func<ResearchProjectId, IResearchProjectDefinition> resolve,
        Func<IResearchProjectDefinition, bool> shortcutActive)
    {
        if (resolve == null)
        {
            throw new ArgumentNullException(nameof(resolve));
        }

        List<ResearchProjectId> ordered = new List<ResearchProjectId>();
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(ResearchProjectId id)
        {
            if (!id.IsValid || !visited.Add(id.Value))
            {
                return;
            }

            IResearchProjectDefinition project = resolve(id);
            if (project == null)
            {
                return;
            }

            bool skipDependencies = project.BlueprintRule == ResearchBlueprintRule.Shortcut
                && shortcutActive != null
                && shortcutActive(project);
            if (!skipDependencies)
            {
                foreach (ResearchProjectId prerequisite in project.PrerequisiteIds
                             .OrderBy(item => item.Value, StringComparer.Ordinal))
                {
                    Visit(prerequisite);
                }
            }

            ordered.Add(project.ProjectId);
        }

        Visit(target);
        return ordered;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ResearchNodeEvaluation
{
    public ResearchNodeEvaluation(
        ResearchNodeState state,
        string blocker = null)
    {
        State = state;
        Blocker = blocker ?? string.Empty;
    }

    public ResearchNodeState State { get; }
    public string Blocker { get; }
}
