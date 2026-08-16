using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BlueprintResearchRestoreCandidate
{
    internal BlueprintResearchRestoreCandidate(
        BlueprintResearchState research,
        KnowledgeResidueRestoreCandidate knowledge)
    {
        Research = research ?? throw new ArgumentNullException(nameof(research));
        Knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
    }

    internal BlueprintResearchState Research { get; }
    internal KnowledgeResidueRestoreCandidate Knowledge { get; }
}

public sealed class BlueprintResearchSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonResearchSaveData,
        BlueprintResearchRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "research.blueprints";
    private const int CurrentVersion = 5;

    private static readonly string[] Dependencies =
    {
        WorkOrdersSaveSection.Id
    };

    private readonly BlueprintResearchRuntime runtime;
    private readonly IFacilityShopCatalog facilityCatalog;
    private readonly IKnowledgeResidueProcessingRuntime knowledgeProcessing;
    private readonly IResearchProjectCatalog projectCatalog;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IFacilityBufferDestinationClaimCommand destinationClaims;

    public BlueprintResearchSaveSection(
        ProgressionSceneRuntimeReferences runtimeReferences,
        IFacilityShopCatalog facilityCatalog,
        IKnowledgeResidueProcessingRuntime knowledgeProcessing,
        IResearchProjectCatalog projectCatalog,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IFacilityBufferDestinationClaimCommand destinationClaims)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(BlueprintResearchSaveSection)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.facilityCatalog = facilityCatalog
            ?? throw new ArgumentNullException(nameof(facilityCatalog));
        this.knowledgeProcessing = knowledgeProcessing
            ?? throw new ArgumentNullException(nameof(knowledgeProcessing));
        this.projectCatalog = projectCatalog
            ?? throw new ArgumentNullException(nameof(projectCatalog));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonResearchSaveData CapturePayload()
    {
        bool usesProjectAuthority = projectCatalog.Projects.Count > 0;
        return new DungeonResearchSaveData
        {
            tasks = usesProjectAuthority
                ? new List<DungeonResearchTaskSaveData>()
                : runtime.State.Tasks
                    .Where(task => task?.Blueprint != null)
                    .Select(task => new DungeonResearchTaskSaveData
                    {
                        blueprintId = task.Blueprint.id,
                        progress = task.Progress
                    })
                    .ToList(),
            completedBlueprintIds = usesProjectAuthority
                ? new List<int>()
                : runtime.State.CompletedBlueprintIds.OrderBy(id => id).ToList(),
            unlockedBuildingIds = runtime.State.UnlockedBuildingIds
                .OrderBy(id => id)
                .ToList(),
            unlockedRecipeIds = runtime.State.UnlockedRecipeIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            knowledgeTasks = knowledgeProcessing.Capture().ToList(),
            projectProgress = runtime.State.Projects.ProgressById.Values
                .OrderBy(progress => progress.ProjectId.Value, StringComparer.Ordinal)
                .Select(progress =>
                {
                    ResearchProjectSO project = projectCatalog.TryGet(
                            progress.ProjectId,
                            out ResearchProjectSO found)
                        ? found
                        : throw new InvalidOperationException(
                            $"Research progress references missing project '{progress.ProjectId.Value}'.");
                    return new DungeonResearchProjectProgressSaveData
                    {
                        projectId = progress.ProjectId.Value,
                        progress = progress.Progress,
                        requiredWorkAtCapture = project.RequiredWork
                    };
                })
                .ToList(),
            completedProjectIds = runtime.State.Projects.CompletedProjectIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            projectQueue = runtime.State.Projects.Queue
                .Select(entry => new DungeonResearchQueueEntrySaveData
                {
                    projectId = entry.ProjectId.Value,
                    suspendedReason = entry.SuspendedReason
                })
                .ToList(),
            activeProjectId = runtime.State.Projects.ActiveProjectId.Value,
            materializeLegacyBlueprintItems = false
        };
    }

    protected override BlueprintResearchRestoreCandidate BuildRestoreCandidate(
        DungeonResearchSaveData source)
    {
        RequireCollections(source);
        if (source.materializeLegacyBlueprintItems)
        {
            throw new InvalidOperationException(
                "Research V5 cannot materialize legacy blueprint items.");
        }

        Dictionary<int, FacilityBlueprintSO> blueprints = BuildBlueprintIndex();
        Dictionary<string, ResearchProjectSO> projects = BuildProjectIndex();
        HashSet<int> buildings = facilityCatalog.Buildings
            .Where(building => building != null)
            .Select(building => building.id)
            .ToHashSet();
        HashSet<string> recipes = CollectResearchRecipeIds(blueprints, projects);

        BlueprintResearchState restored = new BlueprintResearchState();
        RestoreBlueprintState(source, restored, blueprints, buildings, recipes);
        RestoreProjectState(source, restored, projects);
        KnowledgeResidueRestoreCandidate knowledge =
            knowledgeProcessing.PrepareRestore(source.knowledgeTasks);
        return new BlueprintResearchRestoreCandidate(restored, knowledge);
    }

    protected override void PublishRestoreCandidate(
        BlueprintResearchRestoreCandidate candidate)
    {
        BlueprintResearchRestoreCandidate required = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        if (!restoreWorldCandidates.TryGetGrid(out Grid candidateGrid)
            || !restoreWorldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> candidateBuildings))
        {
            throw new InvalidOperationException(
                "Research restore requires the detached facility-world candidate before destination publication.");
        }

        RoomLayout candidateRooms = RoomDetector.Build(candidateGrid);
        FacilityBufferDestinationClaim[] archiveClaims =
            ResearchBlueprintArchiveDestinationAuthority.BuildClaims(
                candidateBuildings.Where(building =>
                    building != null
                    && building.IsDetachedRestoreCandidate
                    && ResearchBlueprintArchiveDestinationAuthority
                        .IsAuthoredArchiveFacility(building)
                    && candidateRooms.TryGetRoom(
                        building,
                        out RoomInstance room)
                    && ResearchBlueprintArchiveDestinationAuthority
                        .IsEligibleRoom(room)));
        if (!destinationClaims.TryReplaceOwnedClaims(
                ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                archiveClaims,
                out FacilityBufferDestinationClaimFailureCode failureCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Research archive destination restore failed: "
                + $"{failureCode}: {failureReason}");
        }

        runtime.ReplaceStateFromRestore(required.Research);
        knowledgeProcessing.Restore(required.Knowledge);
    }

    private Dictionary<int, FacilityBlueprintSO> BuildBlueprintIndex()
    {
        FacilityBlueprintSO[] source = facilityCatalog.Blueprints
            .Where(blueprint => blueprint != null)
            .ToArray();
        if (source.GroupBy(blueprint => blueprint.id).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                "Research restore cannot use a blueprint catalog with duplicate IDs.");
        }
        return source.ToDictionary(blueprint => blueprint.id);
    }

    private Dictionary<string, ResearchProjectSO> BuildProjectIndex()
    {
        ResearchProjectSO[] source = projectCatalog.Projects
            .Where(project => project != null)
            .ToArray();
        foreach (ResearchProjectSO project in source)
        {
            RequireCanonicalId(project.ProjectId.Value, "research project catalog");
        }
        if (source.GroupBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                "Research restore cannot use a project catalog with duplicate IDs.");
        }
        return source.ToDictionary(
            project => project.ProjectId.Value,
            StringComparer.Ordinal);
    }

    private static HashSet<string> CollectResearchRecipeIds(
        IReadOnlyDictionary<int, FacilityBlueprintSO> blueprints,
        IReadOnlyDictionary<string, ResearchProjectSO> projects)
    {
        return blueprints.Values
            .SelectMany(blueprint => blueprint.Unlocks
                .OfType<BlueprintRecipeUnlock>())
            .Concat(projects.Values.SelectMany(project => project.Unlocks
                .OfType<BlueprintRecipeUnlock>()))
            .Select(unlock => unlock.recipeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void RestoreBlueprintState(
        DungeonResearchSaveData source,
        BlueprintResearchState restored,
        IReadOnlyDictionary<int, FacilityBlueprintSO> blueprints,
        ISet<int> buildings,
        ISet<string> recipes)
    {
        HashSet<int> taskIds = new HashSet<int>();
        foreach (DungeonResearchTaskSaveData task in source.tasks)
        {
            if (task == null
                || !taskIds.Add(task.blueprintId)
                || !blueprints.TryGetValue(
                    task?.blueprintId ?? -1,
                    out FacilityBlueprintSO blueprint))
            {
                throw new InvalidOperationException(
                    $"Research task has a null, duplicate, or unknown blueprint id '{task?.blueprintId ?? -1}'.");
            }
            if (!IsFiniteInRange(task.progress, 0f, blueprint.researchWorkRequired))
            {
                throw new InvalidOperationException(
                    $"Research blueprint {task.blueprintId} has invalid progress.");
            }
            restored.RestoreTask(blueprint, task.progress);
        }

        HashSet<int> completedBlueprintIds = new HashSet<int>();
        foreach (int id in source.completedBlueprintIds)
        {
            if (!completedBlueprintIds.Add(id)
                || !blueprints.TryGetValue(id, out FacilityBlueprintSO blueprint))
            {
                throw new InvalidOperationException(
                    $"Completed research blueprint id '{id}' is duplicate or unknown.");
            }
            restored.RestoreCompletedBlueprintId(id);
            ApplyUnlocks(restored, blueprint.Unlocks);
        }

        HashSet<int> unlockedBuildings = new HashSet<int>();
        foreach (int id in source.unlockedBuildingIds)
        {
            if (!unlockedBuildings.Add(id) || !buildings.Contains(id))
            {
                throw new InvalidOperationException(
                    $"Unlocked research building id '{id}' is duplicate or unknown.");
            }
            restored.RestoreUnlockedBuildingId(id);
        }

        HashSet<string> unlockedRecipes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in source.unlockedRecipeIds)
        {
            RequireCanonicalId(id, "unlocked research recipe");
            if (!unlockedRecipes.Add(id) || !recipes.Contains(id))
            {
                throw new InvalidOperationException(
                    $"Unlocked research recipe id '{id}' is duplicate or unknown.");
            }
            restored.UnlockRecipe(id);
        }
    }

    private static void RestoreProjectState(
        DungeonResearchSaveData source,
        BlueprintResearchState restored,
        IReadOnlyDictionary<string, ResearchProjectSO> projects)
    {
        HashSet<string> progressIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonResearchProjectProgressSaveData saved in source.projectProgress)
        {
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Research project progress collection contains null.");
            }
            RequireCanonicalId(saved.projectId, "research progress project");
            if (!progressIds.Add(saved.projectId)
                || !projects.TryGetValue(saved.projectId, out ResearchProjectSO project))
            {
                throw new InvalidOperationException(
                    $"Research progress project '{saved.projectId}' is duplicate or unknown.");
            }
            if (!float.IsFinite(saved.requiredWorkAtCapture)
                || saved.requiredWorkAtCapture < 1f
                || !IsFiniteInRange(
                    saved.progress,
                    0f,
                    saved.requiredWorkAtCapture))
            {
                throw new InvalidOperationException(
                    $"Research project '{saved.projectId}' has invalid captured work.");
            }
            restored.Projects.RestoreProgress(
                project,
                ResearchSaveValidation.RestoreProgressRatio(
                    saved.progress,
                    saved.requiredWorkAtCapture,
                    project.RequiredWork));
        }

        HashSet<string> completedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in source.completedProjectIds)
        {
            RequireCanonicalId(id, "completed research project");
            if (!completedIds.Add(id)
                || !projects.TryGetValue(id, out ResearchProjectSO project))
            {
                throw new InvalidOperationException(
                    $"Completed research project '{id}' is duplicate or unknown.");
            }
            restored.Projects.RestoreCompleted(project.ProjectId);
            ApplyUnlocks(restored, project.Unlocks);
            if (project.Blueprint != null)
            {
                restored.RestoreCompletedBlueprintId(project.Blueprint.id);
            }
        }

        HashSet<string> queuedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonResearchQueueEntrySaveData saved in source.projectQueue)
        {
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Research queue contains null.");
            }
            RequireCanonicalId(saved.projectId, "queued research project");
            RequireCanonicalTextOrEmpty(saved.suspendedReason, "research suspension reason");
            if (!queuedIds.Add(saved.projectId)
                || completedIds.Contains(saved.projectId)
                || !projects.ContainsKey(saved.projectId))
            {
                throw new InvalidOperationException(
                    $"Queued research project '{saved.projectId}' is duplicate, completed, or unknown.");
            }
            restored.Projects.RestoreQueueEntry(
                new ResearchProjectId(saved.projectId),
                saved.suspendedReason);
        }

        if (!string.IsNullOrEmpty(source.activeProjectId))
        {
            RequireCanonicalId(source.activeProjectId, "active research project");
            if (!queuedIds.Contains(source.activeProjectId))
            {
                throw new InvalidOperationException(
                    $"Active research project '{source.activeProjectId}' is not queued.");
            }
        }
        restored.Projects.RestoreActive(
            new ResearchProjectId(source.activeProjectId));
    }

    private static void ApplyUnlocks(
        BlueprintResearchState restored,
        IEnumerable<BlueprintUnlock> unlocks)
    {
        foreach (BlueprintBuildingUnlock unlock in
                 (unlocks ?? Array.Empty<BlueprintUnlock>())
                 .OfType<BlueprintBuildingUnlock>())
        {
            restored.RestoreUnlockedBuildingId(unlock.buildingId);
        }
        foreach (BlueprintRecipeUnlock unlock in
                 (unlocks ?? Array.Empty<BlueprintUnlock>())
                 .OfType<BlueprintRecipeUnlock>())
        {
            restored.UnlockRecipe(unlock.recipeId);
        }
    }

    private static void RequireCollections(DungeonResearchSaveData source)
    {
        if (source == null
            || source.tasks == null
            || source.completedBlueprintIds == null
            || source.unlockedBuildingIds == null
            || source.unlockedRecipeIds == null
            || source.knowledgeTasks == null
            || source.projectProgress == null
            || source.completedProjectIds == null
            || source.projectQueue == null
            || source.activeProjectId == null)
        {
            throw new InvalidOperationException(
                "Research V5 payload is missing a required field or collection.");
        }
    }

    private static bool IsFiniteInRange(float value, float minimum, float maximum)
        => ResearchSaveValidation.IsFiniteInRange(value, minimum, maximum);

    private static void RequireCanonicalId(string value, string label)
        => ResearchSaveValidation.RequireCanonicalId(value, label);

    private static void RequireCanonicalTextOrEmpty(string value, string label)
        => ResearchSaveValidation.RequireCanonicalTextOrEmpty(value, label);
}
