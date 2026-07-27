using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BlueprintResearchSaveSection :
    DungeonJsonSaveSection<DungeonResearchSaveData>
{
    public const string Id = "research.blueprints";

    private readonly IBlueprintResearchRuntimeProvider runtimeProvider;
    private readonly IFacilityShopCatalog facilityCatalog;
    private readonly IKnowledgeResidueProcessingRuntime knowledgeProcessing;
    private readonly IResearchProjectCatalog projectCatalog;

    public BlueprintResearchSaveSection(
        IBlueprintResearchRuntimeProvider runtimeProvider,
        IFacilityShopCatalog facilityCatalog,
        IKnowledgeResidueProcessingRuntime knowledgeProcessing,
        IResearchProjectCatalog projectCatalog)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.facilityCatalog = facilityCatalog
            ?? throw new ArgumentNullException(nameof(facilityCatalog));
        this.knowledgeProcessing = knowledgeProcessing
            ?? throw new ArgumentNullException(nameof(knowledgeProcessing));
        this.projectCatalog = projectCatalog
            ?? throw new ArgumentNullException(nameof(projectCatalog));
    }

    public override string SectionId => Id;
    public override int SectionVersion => 3;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[] { WorkOrdersSaveSection.Id };

    protected override DungeonResearchSaveData CapturePayload()
    {
        DungeonResearchSaveData destination = new DungeonResearchSaveData();
        if (runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            bool usesProjectAuthority = projectCatalog.Projects.Count > 0;
            destination.tasks = usesProjectAuthority
                ? new List<DungeonResearchTaskSaveData>()
                : runtime.State.Tasks
                    .Where(task => task?.Blueprint != null)
                    .Select(task => new DungeonResearchTaskSaveData
                    {
                        blueprintId = task.Blueprint.id,
                        progress = task.Progress
                    })
                    .ToList();
            destination.completedBlueprintIds = usesProjectAuthority
                ? new List<int>()
                : runtime.State.CompletedBlueprintIds.OrderBy(id => id).ToList();
            destination.unlockedBuildingIds =
                runtime.State.UnlockedBuildingIds.OrderBy(id => id).ToList();
            destination.unlockedRecipeIds = runtime.State.UnlockedRecipeIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            destination.projectProgress = runtime.State.Projects.ProgressById.Values
                .OrderBy(progress => progress.ProjectId.Value, StringComparer.Ordinal)
                .Select(progress => new DungeonResearchProjectProgressSaveData
                {
                    projectId = progress.ProjectId.Value,
                    progress = progress.Progress
                })
                .ToList();
            destination.completedProjectIds = runtime.State.Projects.CompletedProjectIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            destination.projectQueue = runtime.State.Projects.Queue
                .Select(entry => new DungeonResearchQueueEntrySaveData
                {
                    projectId = entry.ProjectId.Value,
                    suspendedReason = entry.SuspendedReason
                })
                .ToList();
            destination.activeProjectId = runtime.State.Projects.ActiveProjectId.Value;
        }

        destination.materializeLegacyBlueprintItems = false;
        destination.knowledgeTasks = knowledgeProcessing.Capture().ToList();
        return destination;
    }

    protected override bool SupportsSectionVersion(int sectionVersion)
    {
        return sectionVersion is 1 or 2 or 3;
    }

    protected override DungeonResearchSaveData MigratePayload(
        DungeonResearchSaveData payload,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion == 1)
        {
            payload.knowledgeTasks ??= new List<KnowledgeResidueTaskSaveData>();
        }

        payload.tasks ??= new List<DungeonResearchTaskSaveData>();
        payload.completedBlueprintIds ??= new List<int>();
        payload.projectProgress ??= new List<DungeonResearchProjectProgressSaveData>();
        payload.completedProjectIds ??= new List<string>();
        payload.projectQueue ??= new List<DungeonResearchQueueEntrySaveData>();
        payload.activeProjectId ??= string.Empty;
        if (sectionVersion < 3)
        {
            MigrateLegacyBlueprintResearch(payload, report);
            payload.materializeLegacyBlueprintItems = true;
        }

        return payload;
    }

    protected override void RestorePayload(
        DungeonResearchSaveData source,
        DungeonGameRestoreReport report)
    {
        if (!runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            report.AddWarning("Research runtime was not present; research state was skipped.");
            return;
        }

        runtime.State.ClearForRestore();
        Dictionary<int, FacilityBlueprintSO> blueprints = facilityCatalog.Blueprints
            .Where(blueprint => blueprint != null)
            .GroupBy(blueprint => blueprint.id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (DungeonResearchTaskSaveData task in source.tasks
                     ?? new List<DungeonResearchTaskSaveData>())
        {
            if (task == null
                || !blueprints.TryGetValue(
                    task.blueprintId,
                    out FacilityBlueprintSO blueprint))
            {
                report.AddWarning(
                    $"Research blueprint {task?.blueprintId ?? -1} no longer exists.");
                continue;
            }

            runtime.State.RestoreTask(blueprint, task.progress);
        }

        foreach (int id in source.completedBlueprintIds ?? new List<int>())
        {
            runtime.State.RestoreCompletedBlueprintId(id);
            if (!blueprints.TryGetValue(id, out FacilityBlueprintSO blueprint))
            {
                continue;
            }

            foreach (BlueprintBuildingUnlock unlock in
                     blueprint.Unlocks.OfType<BlueprintBuildingUnlock>())
            {
                runtime.State.RestoreUnlockedBuildingId(unlock.buildingId);
            }

            foreach (BlueprintRecipeUnlock unlock in
                     blueprint.Unlocks.OfType<BlueprintRecipeUnlock>())
            {
                runtime.State.UnlockRecipe(unlock.recipeId);
            }
        }

        foreach (int id in source.unlockedBuildingIds ?? new List<int>())
        {
            runtime.State.RestoreUnlockedBuildingId(id);
        }

        foreach (string id in source.unlockedRecipeIds ?? new List<string>())
        {
            runtime.State.UnlockRecipe(id);
        }

        RestoreProjectState(runtime, source, report);
        runtime.RefreshProjectQueueAfterRestore();
        if (source.materializeLegacyBlueprintItems)
        {
            runtime.EnsureAcquiredBlueprintItemsMaterialized();
        }

        knowledgeProcessing.Restore(source.knowledgeTasks, report);
    }

    private void MigrateLegacyBlueprintResearch(
        DungeonResearchSaveData payload,
        DungeonGameRestoreReport report)
    {
        Dictionary<int, ResearchProjectSO> projectByBlueprintId = projectCatalog.Projects
            .Where(project => project?.Blueprint != null)
            .GroupBy(project => project.Blueprint.id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (DungeonResearchTaskSaveData task in payload.tasks
                     ?? new List<DungeonResearchTaskSaveData>())
        {
            if (task == null
                || !projectByBlueprintId.TryGetValue(
                    task.blueprintId,
                    out ResearchProjectSO project))
            {
                continue;
            }

            payload.projectProgress.Add(new DungeonResearchProjectProgressSaveData
            {
                projectId = project.ProjectId.Value,
                progress = task.progress
            });
            payload.projectQueue.Add(new DungeonResearchQueueEntrySaveData
            {
                projectId = project.ProjectId.Value
            });
        }

        foreach (int blueprintId in payload.completedBlueprintIds ?? new List<int>())
        {
            if (projectByBlueprintId.TryGetValue(
                    blueprintId,
                    out ResearchProjectSO project)
                && !payload.completedProjectIds.Contains(project.ProjectId.Value))
            {
                payload.completedProjectIds.Add(project.ProjectId.Value);
            }
        }

        payload.tasks.Clear();
        payload.completedBlueprintIds.Clear();
        report.AddWarning(
            "기존 설계도 연구 데이터를 새 연구 프로젝트 진행도와 물리 설계도로 변환했습니다.");
    }

    private void RestoreProjectState(
        BlueprintResearchRuntime runtime,
        DungeonResearchSaveData source,
        DungeonGameRestoreReport report)
    {
        foreach (DungeonResearchProjectProgressSaveData saved in source.projectProgress
                     ?? new List<DungeonResearchProjectProgressSaveData>())
        {
            ResearchProjectId projectId = new ResearchProjectId(saved?.projectId);
            if (!projectCatalog.TryGet(projectId, out ResearchProjectSO project))
            {
                report.AddWarning($"Research project '{saved?.projectId}' no longer exists.");
                continue;
            }

            runtime.State.Projects.RestoreProgress(project, saved.progress);
        }

        foreach (string savedId in source.completedProjectIds ?? new List<string>())
        {
            ResearchProjectId projectId = new ResearchProjectId(savedId);
            if (projectCatalog.TryGet(
                    projectId,
                    out ResearchProjectSO completedProject))
            {
                runtime.State.Projects.RestoreCompleted(projectId);
                if (completedProject.Blueprint != null)
                {
                    runtime.State.RestoreCompletedBlueprintId(
                        completedProject.Blueprint.id);
                }
            }
            else
            {
                report.AddWarning($"Research project '{savedId}' no longer exists.");
            }
        }

        foreach (DungeonResearchQueueEntrySaveData saved in source.projectQueue
                     ?? new List<DungeonResearchQueueEntrySaveData>())
        {
            ResearchProjectId projectId = new ResearchProjectId(saved?.projectId);
            if (!projectCatalog.TryGet(projectId, out _))
            {
                report.AddWarning($"Queued research project '{saved?.projectId}' no longer exists.");
                continue;
            }

            runtime.State.Projects.RestoreQueueEntry(projectId, saved.suspendedReason);
        }

        runtime.State.Projects.RestoreActive(
            new ResearchProjectId(source.activeProjectId));
    }
}
