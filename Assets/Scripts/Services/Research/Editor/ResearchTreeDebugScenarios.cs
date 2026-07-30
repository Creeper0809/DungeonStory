#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ResearchTreeDebugScenarios
{
    // Shared entry point for data, layout, queue, and PlayMode regression coverage.
    [MenuItem("DungeonStory/Debug/Research/Run Research Tree Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Research Tree scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        ResearchProjectAssetBuilder.Rebuild();
        List<string> failures = new List<string>();
        Verify("118개 프로젝트와 설계도 규칙", VerifyCatalog, failures);
        Verify("118개 결정적 자동 배치", () => VerifyLayout(LoadProjects()), failures);
        Verify("100개 합성 그래프 배치", () => VerifySyntheticLayout(100), failures);
        Verify("250개 합성 그래프 배치", () => VerifySyntheticLayout(250), failures);
        Verify("선행 자동 큐와 설계도 우회", VerifyQueueRules, failures);
        Verify("큐 제거 후 진행률 보존", VerifyProgressPersistence, failures);
        Verify("V3 저장 왕복과 V2 이관", VerifySaveRoundTripAndMigration, failures);

        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }
        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("Research Tree EditMode scenarios passed.");
        }
        return failures.Count == 0;
    }

    private static bool VerifyCatalog()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResourceResearchProjectCatalog catalog = new ResourceResearchProjectCatalog(projects);
        int required = projects.Count(project =>
            project.BlueprintRule == ResearchBlueprintRule.Required);
        int shortcut = projects.Count(project =>
            project.BlueprintRule == ResearchBlueprintRule.Shortcut);
        bool archiveConfigured = AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Any(building => building != null
                && building.GetAbility<BuildingFacilityPartAbility>()?.code == "Q03"
                && building.GetAbility<BuildingResearchArchiveAbility>()?.capacity == 8);
        FacilityBlueprintSO[] blueprints = projects
            .Where(project => project.Blueprint != null)
            .Select(project => project.Blueprint)
            .Distinct()
            .ToArray();
        int projectUnlockCount = projects.Sum(project => project.Unlocks.Count);
        return projects.Length == 118
            && catalog.Validate().Count == 0
            && required == 4
            && shortcut == 3
            && projects.Count(project => project.Blueprint != null) == 7
            && blueprints.Length == 7
            && blueprints.All(blueprint => blueprint.Unlocks.Count == 0)
            && projectUnlockCount > 0
            && archiveConfigured;
    }

    private static bool VerifySyntheticLayout(int count)
    {
        List<ResearchProjectSO> projects = new List<ResearchProjectSO>(count);
        try
        {
            for (int index = 0; index < count; index++)
            {
                ResearchProjectSO project = ScriptableObject.CreateInstance<ResearchProjectSO>();
                List<ResearchProjectSO> prerequisites = new List<ResearchProjectSO>();
                if (index > 0)
                {
                    prerequisites.Add(projects[index - 1]);
                }
                if (index > 5 && index % 7 == 0)
                {
                    prerequisites.Add(projects[index - 5]);
                }
                project.Configure(
                    $"synthetic:{count}:{index:000}",
                    $"합성 연구 {index + 1}",
                    "자동 배치 검증용 연구",
                    (ResearchField)(index % Enum.GetValues(typeof(ResearchField)).Length),
                    40f + index,
                    ResearchBlueprintRule.None,
                    null,
                    prerequisites);
                projects.Add(project);
            }

            return VerifyLayout(projects.ToArray());
        }
        finally
        {
            foreach (ResearchProjectSO project in projects)
            {
                Object.DestroyImmediate(project);
            }
        }
    }

    private static bool VerifyLayout(IReadOnlyList<ResearchProjectSO> projects)
    {
        ResearchGraphLayoutService service = new ResearchGraphLayoutService();
        ResearchGraphLayout first = service.Build(projects);
        Dictionary<string, Rect> firstRects = first.NodeRects
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        service.ClearCache();
        ResearchGraphLayout second = service.Build(projects);
        bool deterministic = firstRects.Count == second.NodeRects.Count
            && firstRects.All(pair =>
                second.NodeRects.TryGetValue(pair.Key, out Rect value)
                && value == pair.Value);
        bool noOverlap = first.NodeRects.Values
            .SelectMany((left, index) => first.NodeRects.Values
                .Skip(index + 1)
                .Select(right => (left, right)))
            .All(pair => !pair.left.Overlaps(pair.right));
        bool noPassThrough = first.Edges.All(edge =>
            !SegmentPassesThroughUnrelatedNode(edge, first.NodeRects));
        return deterministic
            && noOverlap
            && noPassThrough
            && first.NodeRects.Count == projects.Count;
    }

    private static bool SegmentPassesThroughUnrelatedNode(
        ResearchGraphEdge edge,
        IReadOnlyDictionary<string, Rect> rects)
    {
        for (int segment = 0; segment + 1 < edge.Points.Count; segment++)
        {
            Vector2 from = edge.Points[segment];
            Vector2 to = edge.Points[segment + 1];
            foreach (KeyValuePair<string, Rect> pair in rects)
            {
                if (pair.Key == edge.From.Value || pair.Key == edge.To.Value)
                {
                    continue;
                }

                Rect inset = new Rect(
                    pair.Value.xMin + 0.5f,
                    pair.Value.yMin + 0.5f,
                    pair.Value.width - 1f,
                    pair.Value.height - 1f);
                bool horizontal = Mathf.Approximately(from.y, to.y)
                    && from.y > inset.yMin
                    && from.y < inset.yMax
                    && Mathf.Max(from.x, to.x) > inset.xMin
                    && Mathf.Min(from.x, to.x) < inset.xMax;
                bool vertical = Mathf.Approximately(from.x, to.x)
                    && from.x > inset.xMin
                    && from.x < inset.xMax
                    && Mathf.Max(from.y, to.y) > inset.yMin
                    && Mathf.Min(from.y, to.y) < inset.yMax;
                if (horizontal || vertical)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool VerifyQueueRules()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResourceResearchProjectCatalog catalog = new ResourceResearchProjectCatalog(projects);
        MutableArchiveQuery archive = new MutableArchiveQuery();
        using RuntimeScope scope = new RuntimeScope(catalog, archive);

        ResearchProjectSO support = projects.First(project =>
            project.ProjectId.Value == "research:survival:support");
        ResearchQueueCommandResult blocked = scope.Runtime.EnqueueProject(support.ProjectId);
        ResearchProjectSO medical = projects.First(project =>
            project.ProjectId.Value == "research:survival:medical");
        ResearchQueueCommandResult transitiveBlocked =
            scope.Runtime.EnqueueProject(medical.ProjectId);
        bool noPartialQueue = !transitiveBlocked.Succeeded
            && scope.Runtime.State.Projects.Queue.Count == 0
            && transitiveBlocked.Message.Contains(
                support.DisplayName,
                StringComparison.Ordinal);
        archive.ArchivedBlueprintIds.Add(support.Blueprint.id);
        ResearchQueueCommandResult required = scope.Runtime.EnqueueProject(support.ProjectId);
        bool requiredChain = required.Succeeded
            && scope.Runtime.State.Projects.Queue.Count == 2
            && scope.Runtime.State.Projects.Queue[0].ProjectId.Value
                == "research:survival:sanitation";

        ResearchProjectSO shortcut = projects.First(project =>
            project.ProjectId.Value == "research:commerce:secure-trade");
        archive.ArchivedBlueprintIds.Add(shortcut.Blueprint.id);
        using RuntimeScope shortcutScope = new RuntimeScope(catalog, archive);
        ResearchQueueCommandResult shortcutResult =
            shortcutScope.Runtime.EnqueueProject(shortcut.ProjectId);
        bool shortcutOnly = shortcutResult.Succeeded
            && shortcutScope.Runtime.State.Projects.Queue.Count == 1
            && shortcutScope.Runtime.State.Projects.Queue[0].ProjectId.Equals(shortcut.ProjectId);

        return !blocked.Succeeded
            && noPartialQueue
            && requiredChain
            && shortcutOnly;
    }

    private static bool VerifyProgressPersistence()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResourceResearchProjectCatalog catalog = new ResourceResearchProjectCatalog(projects);
        using RuntimeScope scope = new RuntimeScope(catalog, new MutableArchiveQuery());
        ResearchProjectSO project = projects.First(candidate =>
            candidate.ProjectId.Value == "research:survival:sanitation");
        ResearchQueueCommandResult queued = scope.Runtime.EnqueueProject(project.ProjectId);
        ResearchProjectProgressState progress =
            scope.Runtime.State.Projects.GetProgress(project.ProjectId);
        progress.Add(13f, project);
        ResearchQueueCommandResult removed = scope.Runtime.RemoveProject(project.ProjectId);
        ResearchQueueCommandResult requeued = scope.Runtime.EnqueueProject(project.ProjectId);
        return queued.Succeeded
            && removed.Succeeded
            && requeued.Succeeded
            && Mathf.Approximately(
                scope.Runtime.State.Projects.GetProgress(project.ProjectId).Progress,
                13f);
    }

    private static bool VerifySaveRoundTripAndMigration()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResourceResearchProjectCatalog catalog = new ResourceResearchProjectCatalog(projects);
        MutableArchiveQuery archive = new MutableArchiveQuery();
        ResearchProjectSO sanitation = projects.First(project =>
            project.ProjectId.Value == "research:survival:sanitation");
        ResearchProjectSO guard = projects.First(project =>
            project.ProjectId.Value == "research:defense:watch");

        using RuntimeScope source = new RuntimeScope(catalog, archive);
        source.Runtime.State.Projects.GetProgress(sanitation.ProjectId)
            .Restore(17f, sanitation);
        source.Runtime.State.Projects.RestoreQueueEntry(sanitation.ProjectId, string.Empty);
        source.Runtime.State.Projects.RestoreQueueEntry(guard.ProjectId, "검증 중단");
        source.Runtime.State.Projects.RestoreActive(sanitation.ProjectId);
        source.Runtime.State.Projects.RestoreCompleted(guard.ProjectId);

        BlueprintResearchSaveSection sourceSection = new BlueprintResearchSaveSection(
            new FixedRuntimeProvider(source.Runtime),
            new EditorCatalog(),
            new EmptyKnowledgeRuntime(),
            catalog);
        string captured = sourceSection.Capture();

        using RuntimeScope restored = new RuntimeScope(catalog, archive);
        BlueprintResearchSaveSection restoredSection = new BlueprintResearchSaveSection(
            new FixedRuntimeProvider(restored.Runtime),
            new EditorCatalog(),
            new EmptyKnowledgeRuntime(),
            catalog);
        DungeonGameRestoreReport restoreReport = new DungeonGameRestoreReport();
        restoredSection.Restore(captured, 3, restoreReport);
        bool roundTrip = restoreReport.Success
            && Mathf.Approximately(
                restored.Runtime.State.Projects.GetProgress(sanitation.ProjectId).Progress,
                17f)
            && restored.Runtime.State.Projects.ContainsInQueue(sanitation.ProjectId)
            && restored.Runtime.State.Projects.IsCompleted(guard.ProjectId)
            && restored.Runtime.State.Projects.ActiveProjectId.Equals(sanitation.ProjectId);

        ResearchProjectSO legacyQueued = projects.First(project =>
            project.Blueprint != null
            && project.ProjectId.Value == "research:survival:support");
        ResearchProjectSO legacyCompleted = projects.First(project =>
            project.Blueprint != null
            && project.ProjectId.Value == "research:defense:fortification");
        DungeonResearchSaveData legacyPayload = new DungeonResearchSaveData
        {
            tasks = new List<DungeonResearchTaskSaveData>
            {
                new DungeonResearchTaskSaveData
                {
                    blueprintId = legacyQueued.Blueprint.id,
                    progress = 11f
                }
            },
            completedBlueprintIds = new List<int>
            {
                legacyCompleted.Blueprint.id
            }
        };

        using RuntimeScope migrated = new RuntimeScope(catalog, archive);
        BlueprintResearchSaveSection migrationSection = new BlueprintResearchSaveSection(
            new FixedRuntimeProvider(migrated.Runtime),
            new EditorCatalog(),
            new EmptyKnowledgeRuntime(),
            catalog);
        DungeonGameRestoreReport migrationReport = new DungeonGameRestoreReport();
        migrationSection.Restore(
            JsonUtility.ToJson(legacyPayload),
            2,
            migrationReport);
        bool migratedLegacy = migrationReport.Success
            && Mathf.Approximately(
                migrated.Runtime.State.Projects.GetProgress(legacyQueued.ProjectId).Progress,
                11f)
            && migrated.Runtime.State.Projects.ContainsInQueue(legacyQueued.ProjectId)
            && migrated.Runtime.State.Projects.IsCompleted(legacyCompleted.ProjectId)
            && migrated.Runtime.State.Tasks.Count == 0
            && migrationReport.Warnings.Any(message =>
                message.Contains("새 연구 프로젝트", StringComparison.Ordinal));

        return roundTrip && migratedLegacy;
    }

    private static ResearchProjectSO[] LoadProjects()
    {
        return AssetDatabase.FindAssets(
                string.Empty,
                new[] { "Assets/Resources/SO/Research/Projects" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null)
            .OrderBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Verify(
        string name,
        Func<bool> scenario,
        ICollection<string> failures)
    {
        try
        {
            if (scenario())
            {
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        failures.Add($"Research Tree scenario failed: {name}");
    }

    private sealed class RuntimeScope : IDisposable
    {
        private readonly GameObject root;

        public RuntimeScope(
            IResearchProjectCatalog catalog,
            IResearchBlueprintArchiveQuery archive)
        {
            root = new GameObject("ResearchTreeScenarioRuntime");
            Runtime = root.AddComponent<BlueprintResearchRuntime>();
            Runtime.Construct(
                new FixedUnlockStateService(),
                new EditorCatalog(),
                new FacilityCandidateCacheStore(
                    CharacterAiEditorTestDependencies.WorldRegistry),
                new DungeonWorkforceReplanService(
                    CharacterAiEditorTestDependencies.WorldRegistry),
                new GameEventBus(),
                projectCatalog: catalog,
                blueprintArchiveQuery: archive);
        }

        public BlueprintResearchRuntime Runtime { get; }

        public void Dispose()
        {
            Object.DestroyImmediate(root);
        }
    }

    private sealed class FixedUnlockStateService : IFacilityShopUnlockStateService
    {
        private readonly FacilityShopUnlockState state = new FacilityShopUnlockState();
        public FacilityShopUnlockState GetUnlockState() => state;
    }

    private sealed class FixedRuntimeProvider : IBlueprintResearchRuntimeProvider
    {
        private readonly BlueprintResearchRuntime runtime;

        public FixedRuntimeProvider(BlueprintResearchRuntime runtime)
        {
            this.runtime = runtime;
        }

        public bool TryGetRuntime(out BlueprintResearchRuntime result)
        {
            result = runtime;
            return result != null;
        }
    }

    private sealed class EmptyKnowledgeRuntime : IKnowledgeResidueProcessingRuntime
    {
        public IReadOnlyList<KnowledgeResidueTaskSnapshot> Tasks =>
            Array.Empty<KnowledgeResidueTaskSnapshot>();

        public bool TryQueueCodexAnalysis(out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool TryQueueRegionReconnaissance(string regionId, out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool HasProcessingWorkFor(BuildableObject facility) => false;

        public BlueprintResearchWorkResult ApplyWork(
            CharacterActor researcher,
            BuildableObject facility,
            float seconds) => default;

        public IReadOnlyList<KnowledgeResidueTaskSaveData> Capture() =>
            Array.Empty<KnowledgeResidueTaskSaveData>();

        public void Restore(
            IEnumerable<KnowledgeResidueTaskSaveData> tasks,
            DungeonGameRestoreReport report)
        {
        }
    }

    private sealed class MutableArchiveQuery : IResearchBlueprintArchiveQuery
    {
        public HashSet<int> ArchivedBlueprintIds { get; } = new HashSet<int>();
        public int Version => 1;

        public ResearchBlueprintArchiveStatus GetStatus(FacilityBlueprintSO blueprint)
        {
            bool archived = blueprint != null && ArchivedBlueprintIds.Contains(blueprint.id);
            return new ResearchBlueprintArchiveStatus(
                archived,
                false,
                archived ? "검증 보관대" : string.Empty,
                archived ? string.Empty : "필수 설계도가 연구실 보관대에 없습니다.");
        }

        public IReadOnlyList<BuildableObject> GetValidArchives() =>
            Array.Empty<BuildableObject>();

        public bool TryGetPreferredArchive(
            FacilityBlueprintSO blueprint,
            out BuildableObject archive,
            out string destinationId)
        {
            archive = null;
            destinationId = string.Empty;
            return false;
        }
    }

    private sealed class EditorCatalog : IFacilityShopCatalog
    {
        public IReadOnlyCollection<BuildingSO> Buildings => Load<BuildingSO>(
            "Assets/Resources/SO/Building");
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            Load<FacilityBlueprintSO>("Assets/Resources/SO/Blueprint");

        public BuildingSO FindBuildingById(int buildingId) =>
            Buildings.FirstOrDefault(building => building != null && building.id == buildingId);

        private static IReadOnlyCollection<T> Load<T>(string path)
            where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { path })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
        }
    }
}
#endif
