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
    public static string DescribeV19GraphMetrics()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResearchProjectSO target = projects.Single(project =>
            project.ProjectId.Value == "research:medical:temporal-stasis");
        HashSet<ResearchProjectSO> closure = new();
        AddClosure(target, closure);
        float work = closure.Sum(project => project.RequiredWork);
        int projectsWithoutInlineUnlocks = projects.Count(project => project.Unlocks.Count == 0);
        return $"V19_RESEARCH_GRAPH count={projects.Length};closure={closure.Count};"
            + $"work={work:F0};days={work / SettlementLaborBalanceRules.BaselineWuPerAdultDay:F1};"
            + $"projectsWithoutInlineUnlocks={projectsWithoutInlineUnlocks}";
    }

    private static void AddClosure(
        ResearchProjectSO project,
        ISet<ResearchProjectSO> closure)
    {
        if (project == null || !closure.Add(project))
        {
            return;
        }
        foreach (ResearchProjectSO prerequisite in project.Prerequisites)
        {
            AddClosure(prerequisite, closure);
        }
    }

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
        List<string> failures = new List<string>();
        Verify("180개 프로젝트와 설계도 규칙", VerifyCatalog, failures);
        Verify("180개 결정적 자동 배치", () => VerifyLayout(LoadProjects()), failures);
        Verify("100개 합성 그래프 배치", () => VerifySyntheticLayout(100), failures);
        Verify("250개 합성 그래프 배치", () => VerifySyntheticLayout(250), failures);
        Verify("선행 자동 큐와 설계도 우회", VerifyQueueRules, failures);
        Verify("연구 시설 수용력 중단·재개", VerifyFacilityCapacitySuspension, failures);
        Verify("큐 제거 후 진행률 보존", VerifyProgressPersistence, failures);
        Verify("현행 저장 왕복과 V5 이전 거부", VerifySaveRoundTripAndLegacyRejection, failures);

        Verify(
            "Discarded restore candidate preserves live research",
            VerifyDiscardedRestoreLeavesLiveResearchUntouched,
            failures);
        Verify(
            "Research archive destination claim restore and rollback",
            VerifyResearchArchiveDestinationClaimRestoreAndRollback,
            failures);

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
        ResearchProjectSO temporalStasis = projects.SingleOrDefault(project =>
            project.ProjectId.Value == "research:medical:temporal-stasis");
        HashSet<ResearchProjectSO> temporalClosure = new();
        AddClosure(temporalStasis, temporalClosure);
        float temporalClosureWork = temporalClosure.Sum(project => project.RequiredWork);
        bool valid = projects.Length == 180
            && catalog.Validate().Count == 0
            && required == 4
            && shortcut == 3
            && projects.Count(project => project.Blueprint != null) == 7
            && blueprints.Length == 7
            && blueprints.All(blueprint => blueprint.Unlocks.Count == 0)
            && projectUnlockCount > 0
            && temporalStasis != null
            && temporalClosure.Count == 90
            && Mathf.Approximately(temporalClosureWork, 95448f)
            && Mathf.Abs(
                temporalClosureWork / SettlementLaborBalanceRules.BaselineWuPerAdultDay
                - 964.1212f
                    * SettlementLaborBalanceRules.HistoricalTheoreticalCapacityWuPerAdultDay
                    / SettlementLaborBalanceRules.BaselineWuPerAdultDay) < 0.05f
            && archiveConfigured;
        if (!valid)
        {
            Debug.LogError(
                $"Research catalog diagnostic: total={projects.Length}, "
                + $"catalogErrors={catalog.Validate().Count}, required={required}, "
                + $"shortcut={shortcut}, blueprintProjects={projects.Count(project => project.Blueprint != null)}, "
                + $"blueprints={blueprints.Length}, unlocks={projectUnlockCount}, "
                + $"temporal={(temporalStasis != null)}, closure={temporalClosure.Count}, "
                + $"closureWork={temporalClosureWork:0.####}, archive={archiveConfigured}.");
        }
        return valid;
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

    private static bool VerifyFacilityCapacitySuspension()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResourceResearchProjectCatalog catalog = new ResourceResearchProjectCatalog(projects);
        MutableCapacityQuery capacity = new MutableCapacityQuery();
        using RuntimeScope scope = new RuntimeScope(
            catalog,
            new MutableArchiveQuery(),
            capacity);
        ResearchProjectSO project = projects.First(candidate =>
            candidate.ProjectId.Value == "research:survival:sanitation");

        ResearchQueueCommandResult queued = scope.Runtime.EnqueueProject(project.ProjectId);
        bool blocked = queued.Succeeded
            && !scope.Runtime.TryGetActiveProject(out _, out string initialBlocker)
            && initialBlocker.Contains("기초 0/1", StringComparison.Ordinal)
            && scope.Runtime.State.Projects.Queue[0].IsSuspended;

        capacity.Set(ResearchFacilityCapabilityId.Basic, 1);
        scope.Runtime.RefreshProjectQueueAfterRestore();
        bool resumed = scope.Runtime.TryGetActiveProject(
            out ResearchProjectSO active,
            out _)
            && active == project;
        ResearchProjectProgressState progress =
            scope.Runtime.State.Projects.GetProgress(project.ProjectId);
        progress.Add(13f, project);

        capacity.Set(ResearchFacilityCapabilityId.Basic, 0);
        bool pausedAgain = !scope.Runtime.TryGetActiveProject(out _, out _)
            && Mathf.Approximately(progress.Progress, 13f)
            && scope.Runtime.State.Projects.Queue[0].IsSuspended;
        capacity.Set(ResearchFacilityCapabilityId.Basic, 1);
        scope.Runtime.RefreshProjectQueueAfterRestore();
        bool resumedWithProgress = scope.Runtime.TryGetActiveProject(out active, out _)
            && active == project
            && Mathf.Approximately(progress.Progress, 13f);

        return blocked && resumed && pausedAgain && resumedWithProgress;
    }

    private static bool VerifySaveRoundTripAndLegacyRejection()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResourceResearchProjectCatalog catalog = new ResourceResearchProjectCatalog(projects);
        MutableArchiveQuery archive = new MutableArchiveQuery();
        ResearchProjectSO sanitation = projects.First(project =>
            project.ProjectId.Value == "research:survival:sanitation");
        ResearchProjectSO guard = projects.First(project =>
            project.ProjectId.Value == "research:defense:watch");
        ResearchProjectSO completed = projects.First(project =>
            project.ProjectId.Value == "research:arcane:records");

        using RuntimeScope source = new RuntimeScope(catalog, archive);
        source.Runtime.State.Projects.GetProgress(sanitation.ProjectId)
            .Restore(17f, sanitation);
        source.Runtime.State.Projects.RestoreQueueEntry(sanitation.ProjectId, string.Empty);
        source.Runtime.State.Projects.RestoreQueueEntry(guard.ProjectId, "검증 중단");
        source.Runtime.State.Projects.RestoreActive(sanitation.ProjectId);
        source.Runtime.State.Projects.RestoreCompleted(completed.ProjectId);

        BlueprintResearchSaveSection sourceSection = new BlueprintResearchSaveSection(
            new ProgressionSceneRuntimeReferences(null, source.Runtime, null),
            new EditorCatalog(),
            new EmptyKnowledgeRuntime(),
            catalog,
            CreateEmptyRestoreWorldCandidates(),
            new FacilityBufferDestinationClaimRegistry());
        string captured = sourceSection.Capture();

        using RuntimeScope restored = new RuntimeScope(catalog, archive);
        BlueprintResearchSaveSection restoredSection = new BlueprintResearchSaveSection(
            new ProgressionSceneRuntimeReferences(null, restored.Runtime, null),
            new EditorCatalog(),
            new EmptyKnowledgeRuntime(),
            catalog,
            CreateEmptyRestoreWorldCandidates(),
            new FacilityBufferDestinationClaimRegistry());
        DungeonGameRestoreReport restoreReport = new DungeonGameRestoreReport();
        restoredSection.Restore(captured, 5, restoreReport);
        bool roundTrip = restoreReport.Success
            && Mathf.Approximately(
                restored.Runtime.State.Projects.GetProgress(sanitation.ProjectId).Progress,
                17f)
            && restored.Runtime.State.Projects.ContainsInQueue(sanitation.ProjectId)
            && restored.Runtime.State.Projects.IsCompleted(completed.ProjectId)
            && restored.Runtime.State.Projects.ActiveProjectId.Equals(sanitation.ProjectId);

        bool rejectedLegacy = false;
        try
        {
            restoredSection.Restore(captured, 3, new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException exception)
        {
            rejectedLegacy = true;
            _ = exception.Message.Contains(
                DungeonSaveCompatibility.PreV21IncompatibilityReason,
                StringComparison.Ordinal);
        }

        string beforeInvalidRestore = JsonUtility.ToJson(restoredSection.Capture());
        DungeonResearchSaveData invalid = JsonUtility.FromJson<DungeonResearchSaveData>(captured);
        invalid.projectProgress[0].requiredWorkAtCapture = 0f;
        bool rejectedInvalid = false;
        try
        {
            restoredSection.StageRestore(
                JsonUtility.ToJson(invalid),
                5,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejectedInvalid = true;
        }

        bool invalidLeftLiveStateUntouched = string.Equals(
            beforeInvalidRestore,
            JsonUtility.ToJson(restoredSection.Capture()),
            StringComparison.Ordinal);
        bool strictContracts = restoredSection is IDungeonRollbackFreeSaveSection
            && restoredSection is IDungeonSaveSectionPreflight
            && restoredSection is IDungeonStagedSaveSection;

        return roundTrip
            && rejectedLegacy
            && rejectedInvalid
            && invalidLeftLiveStateUntouched
            && strictContracts;
    }

    private static bool VerifyDiscardedRestoreLeavesLiveResearchUntouched()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects);
        MutableArchiveQuery archive = new MutableArchiveQuery();
        ResearchProjectSO project = projects.First(candidate =>
            candidate.ProjectId.Value == "research:survival:sanitation");

        using RuntimeScope source = new RuntimeScope(catalog, archive);
        source.Runtime.State.Projects.GetProgress(project.ProjectId)
            .Restore(31f, project);
        source.Runtime.State.Projects.RestoreQueueEntry(
            project.ProjectId,
            string.Empty);
        string candidatePayload = new BlueprintResearchSaveSection(
            new ProgressionSceneRuntimeReferences(null, source.Runtime, null),
            new EditorCatalog(),
            new EmptyKnowledgeRuntime(),
            catalog,
            CreateEmptyRestoreWorldCandidates(),
            new FacilityBufferDestinationClaimRegistry()).Capture();

        using RuntimeScope target = new RuntimeScope(catalog, archive);
        target.Runtime.State.Projects.GetProgress(project.ProjectId)
            .Restore(7f, project);
        FacilityBufferDestinationClaimRegistry targetClaims =
            new FacilityBufferDestinationClaimRegistry();
        BlueprintResearchSaveSection targetSection =
            new BlueprintResearchSaveSection(
                new ProgressionSceneRuntimeReferences(
                    null,
                    target.Runtime,
                    null),
                new EditorCatalog(),
                new EmptyKnowledgeRuntime(),
                catalog,
                CreateEmptyRestoreWorldCandidates(),
                targetClaims);

        ResearchScenarioSaveSection workDependency =
            new ResearchScenarioSaveSection(
                WorkOrdersSaveSection.Id,
                DungeonSaveRestorePhase.RuntimeState);
        ResearchScenarioSaveSection lateFailure =
            new ResearchScenarioSaveSection(
                "research.debug.late-failure",
                DungeonSaveRestorePhase.LateRuntimeState,
                BlueprintResearchSaveSection.Id)
            {
                RemainingCommitFailures = 1
            };
        ResearchDiscardObserver observer =
            new ResearchDiscardObserver(target.Runtime, project.ProjectId);
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[]
            {
                workDependency,
                targetSection,
                lateFailure
            },
            target.RootStore,
            new IDungeonRestoreTransactionParticipant[]
            {
                targetClaims,
                observer
            });
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        envelopes.First(envelope => string.Equals(
                envelope.sectionId,
                BlueprintResearchSaveSection.Id,
                StringComparison.Ordinal))
            .payloadJson = candidatePayload;
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool restored = registry.RestoreAll(envelopes, report);

        return !restored
            && observer.DiscardCount == 1
            && Mathf.Approximately(observer.ObservedProgress, 7f)
            && !observer.ObservedQueued
            && Mathf.Approximately(
                target.Runtime.State.Projects.GetProgress(project.ProjectId).Progress,
                7f)
            && !target.Runtime.State.Projects.ContainsInQueue(project.ProjectId)
            && target.RootStore.PublishedRestoreRevision == 1;
    }

    private static bool VerifyResearchArchiveDestinationClaimRestoreAndRollback()
    {
        ResearchProjectSO[] projects = LoadProjects();
        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects);
        MutableArchiveQuery archive = new MutableArchiveQuery();
        using RuntimeScope source = new RuntimeScope(catalog, archive);
        string payload = new BlueprintResearchSaveSection(
            new ProgressionSceneRuntimeReferences(null, source.Runtime, null),
            new EditorCatalog(),
            new EmptyKnowledgeRuntime(),
            catalog,
            CreateEmptyRestoreWorldCandidates(),
            new FacilityBufferDestinationClaimRegistry()).Capture();

        using ResearchArchiveRestoreWorld world =
            ResearchArchiveRestoreWorld.Create();
        FacilityBufferDestinationClaim expected =
            ResearchBlueprintArchiveDestinationAuthority.CreateClaim(
                world.Archive);

        bool publishedExactlyOnce;
        using (RuntimeScope target = new RuntimeScope(catalog, archive))
        {
            FacilityBufferDestinationClaimRegistry claims =
                new FacilityBufferDestinationClaimRegistry();
            BlueprintResearchSaveSection section =
                CreateResearchSection(
                    target.Runtime,
                    catalog,
                    world.Candidates,
                    claims);
            DungeonSaveSectionRegistry registry =
                CreateResearchRegistry(
                    section,
                    target.RootStore,
                    claims);
            List<DungeonSaveSectionEnvelope> envelopes =
                CaptureWithResearchPayload(registry, payload);
            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            bool restored = registry.RestoreAll(envelopes, report);
            FacilityBufferDestinationClaim[] published = claims.CaptureClaims()
                .Where(claim => claim != null
                    && string.Equals(
                        claim.OwnerDomain,
                        ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                        StringComparison.Ordinal))
                .ToArray();
            publishedExactlyOnce = restored
                && report.Success
                && published.Length == 1
                && ResearchBlueprintArchiveDestinationAuthority.ClaimsMatch(
                    published[0],
                    expected);
        }

        bool rollbackRestoredPreviousImage;
        using (RuntimeScope target = new RuntimeScope(catalog, archive))
        {
            FacilityBufferDestinationClaimRegistry claims =
                new FacilityBufferDestinationClaimRegistry();
            FacilityBufferDestinationClaim sentinel =
                new FacilityBufferDestinationClaim(
                    "research-archive:building:research-rollback-sentinel",
                    new Vector2Int(7, 0),
                    ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                    "research-archive:building:research-rollback-sentinel",
                    ownerFacilityId: null,
                    FacilityBufferDestinationAnchorKind.ReservedTarget);
            if (!claims.TryReplaceOwnedClaims(
                    ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                    new[] { sentinel },
                    out _,
                    out _))
            {
                return false;
            }

            BlueprintResearchSaveSection section =
                CreateResearchSection(
                    target.Runtime,
                    catalog,
                    world.Candidates,
                    claims);
            DungeonSaveSectionRegistry registry =
                CreateResearchRegistry(
                    section,
                    target.RootStore,
                    claims,
                    new FailingResearchPublishParticipant());
            List<DungeonSaveSectionEnvelope> envelopes =
                CaptureWithResearchPayload(registry, payload);
            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            bool restored = registry.RestoreAll(envelopes, report);
            FacilityBufferDestinationClaim[] afterRollback =
                claims.CaptureClaims().ToArray();
            rollbackRestoredPreviousImage = !restored
                && !report.Success
                && afterRollback.Length == 1
                && ResearchBlueprintArchiveDestinationAuthority.ClaimsMatch(
                    afterRollback[0],
                    sentinel)
                && !claims.TryGetClaim(
                    expected.DestinationId,
                    expected.DropPosition,
                    out _);
        }

        return publishedExactlyOnce && rollbackRestoredPreviousImage;
    }

    private static BlueprintResearchSaveSection CreateResearchSection(
        BlueprintResearchRuntime runtime,
        IResearchProjectCatalog catalog,
        IRestoreWorldCandidateQuery candidates,
        IFacilityBufferDestinationClaimCommand claims) =>
        new BlueprintResearchSaveSection(
            new ProgressionSceneRuntimeReferences(null, runtime, null),
            new EditorCatalog(),
            new EmptyKnowledgeRuntime(),
            catalog,
            candidates,
            claims);

    private static DungeonSaveSectionRegistry CreateResearchRegistry(
        BlueprintResearchSaveSection section,
        DungeonRuntimeAggregateRootStore rootStore,
        FacilityBufferDestinationClaimRegistry claims,
        params IDungeonRestoreTransactionParticipant[] trailingParticipants)
    {
        IDungeonRestoreTransactionParticipant[] participants =
            new IDungeonRestoreTransactionParticipant[] { claims }
            .Concat(trailingParticipants
                ?? Array.Empty<IDungeonRestoreTransactionParticipant>())
            .ToArray();
        return new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[]
            {
                new ResearchScenarioSaveSection(
                    WorkOrdersSaveSection.Id,
                    DungeonSaveRestorePhase.RuntimeState),
                section
            },
            rootStore,
            participants);
    }

    private static List<DungeonSaveSectionEnvelope> CaptureWithResearchPayload(
        DungeonSaveSectionRegistry registry,
        string payload)
    {
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        envelopes.First(envelope => string.Equals(
                envelope.sectionId,
                BlueprintResearchSaveSection.Id,
                StringComparison.Ordinal))
            .payloadJson = payload;
        return envelopes;
    }

    private static IRestoreWorldCandidateQuery
        CreateEmptyRestoreWorldCandidates()
    {
        RestoreWorldCandidateIndex candidates =
            new RestoreWorldCandidateIndex();
        candidates.SetFacilityCandidate(
            new Grid(1, 1),
            Array.Empty<BuildableObject>());
        return candidates;
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
            : this(
                catalog,
                archive,
                UnrestrictedResearchFacilityCapacityQuery.Instance)
        {
        }

        public RuntimeScope(
            IResearchProjectCatalog catalog,
            IResearchBlueprintArchiveQuery archive,
            IResearchFacilityCapacityQuery capacity)
        {
            root = new GameObject("ResearchTreeScenarioRuntime");
            RootStore = new DungeonRuntimeAggregateRootStore();
            Runtime = root.AddComponent<BlueprintResearchRuntime>();
            Runtime.Construct(
                new FixedUnlockStateService(),
                new EditorCatalog(),
                new FacilityCandidateCacheStore(
                    CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null),
                new DungeonWorkforceReplanService(
                    CharacterAiEditorTestDependencies.WorldRegistry, facilityCandidateCache: null),
                new GameEventBus(),
                itemStackRuntime: null,
                projectCoordinator: new BlueprintResearchProjectCoordinator(
                    catalog,
                    archive,
                    capacity),
                worldDropZoneQuery: null,
                aggregateRootStore: RootStore,
                debugRules: DisabledDungeonDebugRuleQuery.Instance,
                uiClock: new DungeonStory.Foundation.UnityUiClock());
        }

        public BlueprintResearchRuntime Runtime { get; }
        public DungeonRuntimeAggregateRootStore RootStore { get; }

        public void Dispose()
        {
            Object.DestroyImmediate(root);
        }
    }

    private sealed class ResearchArchiveRestoreWorld : IDisposable
    {
        private readonly List<BuildableObject> buildings =
            new List<BuildableObject>();
        private readonly List<BuildingSO> syntheticDefinitions =
            new List<BuildingSO>();

        private ResearchArchiveRestoreWorld()
        {
            Grid = new Grid(8, 1);
        }

        private void Initialize()
        {
            for (int x = 0; x <= 6; x++)
            {
                if (!Grid.RegisterOccupant(
                        new ResearchHallwayOccupant(),
                        GridLayer.Hallway,
                        new[] { new Vector2Int(x, 0) },
                        false))
                {
                    throw new InvalidOperationException(
                        $"Research restore hallway registration failed at ({x},0).");
                }
            }

            BuildingSO door = CreateSyntheticDefinition(
                "Research restore door",
                990001,
                BuildingCategory.None,
                BuildingRuntimeArchetypeKind.Door,
                FacilityRole.None);
            BuildingSO researchFixture = CreateSyntheticDefinition(
                "Research restore fixture",
                990002,
                BuildingCategory.Special,
                BuildingRuntimeArchetypeKind.Facility,
                FacilityRole.Research);
            BuildingSO wall = CreateSyntheticDefinition(
                "Research restore wall",
                990003,
                BuildingCategory.Wall,
                BuildingRuntimeArchetypeKind.Generic,
                FacilityRole.None);
            BuildingSO archiveDefinition = AssetDatabase.FindAssets(
                    "t:BuildingSO",
                    new[] { "Assets/Resources/SO/Building/Modular" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                .Single(definition => definition != null
                    && definition.GetAbility<BuildingFacilityPartAbility>()?.code
                        == "Q03"
                    && definition.GetAbility<BuildingResearchArchiveAbility>()
                        != null);

            Place(door, new Vector2Int(1, 0), "door");
            Archive = Place(
                archiveDefinition,
                new Vector2Int(3, 0),
                "archive");
            Place(researchFixture, new Vector2Int(4, 0), "research-fixture");
            Place(wall, new Vector2Int(6, 0), "wall");

            RoomLayout rooms = RoomDetector.Build(Grid);
            if (!rooms.TryGetRoom(Archive, out RoomInstance archiveRoom)
                || !ResearchBlueprintArchiveDestinationAuthority
                    .IsEligibleRoom(archiveRoom))
            {
                throw new InvalidOperationException(
                    "Research restore candidate did not produce an eligible Q03 room.");
            }

            RestoreWorldCandidateIndex candidates =
                new RestoreWorldCandidateIndex();
            candidates.SetFacilityCandidate(Grid, buildings.ToArray());
            Candidates = candidates;
        }

        public Grid Grid { get; }
        public BuildableObject Archive { get; private set; }
        public IRestoreWorldCandidateQuery Candidates { get; private set; }

        public static ResearchArchiveRestoreWorld Create()
        {
            ResearchArchiveRestoreWorld world =
                new ResearchArchiveRestoreWorld();
            try
            {
                world.Initialize();
                return world;
            }
            catch
            {
                world.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            for (int index = buildings.Count - 1; index >= 0; index--)
            {
                BuildableObject building = buildings[index];
                if (building == null)
                    continue;
                if (building.IsDetachedRestoreCandidate)
                    building.DiscardDetachedRestore();
                else
                    Object.DestroyImmediate(building.gameObject);
            }
            foreach (BuildingSO definition in syntheticDefinitions)
            {
                if (definition != null)
                    Object.DestroyImmediate(definition);
            }
        }

        private BuildableObject Place(
            BuildingSO definition,
            Vector2Int position,
            string suffix)
        {
            BuildableObject building = new GridBuildingObjectFactory()
                .CreateDetached(Grid, definition, position)
                ?? throw new InvalidOperationException(
                    $"Research restore building '{suffix}' creation failed.");
            building.PrepareForDetachedRestore();
            CharacterAiEditorTestDependencies.InjectWithRoomPolicy(
                building,
                new RoomFacilityPolicyService(new RoomLayoutCache()));
            building.RestorePersistentIdentity(
                new BuildingInstanceId(
                    $"building:research-archive-restore:{suffix}"));
            building.SetGrid(Grid);
            building.Initialization(definition, position);
            if (!Grid.RegisterOccupant(
                    building,
                    definition.layer,
                    definition.GetGridPosList(position),
                    definition.Placement.IsMovement))
            {
                building.DiscardDetachedRestore();
                throw new InvalidOperationException(
                    $"Research restore building '{suffix}' registration failed.");
            }
            buildings.Add(building);
            return building;
        }

        private BuildingSO CreateSyntheticDefinition(
            string name,
            int id,
            BuildingCategory category,
            BuildingRuntimeArchetypeKind archetype,
            FacilityRole role)
        {
            BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
            syntheticDefinitions.Add(definition);
            definition.id = id;
            definition.objectName = name;
            definition.width = 1;
            definition.height = 1;
            definition.layer = GridLayer.Building;
            definition.category = category;
            definition.runtimeArchetype = archetype;
            definition.unlocked = true;
            definition.Facility = new FacilityData
            {
                roles = role,
                capacity = role == FacilityRole.None ? 0 : 1,
                useDuration = role == FacilityRole.None ? 0f : 1f,
                disabledWhenDamaged = true
            };
            return definition;
        }
    }

    private sealed class ResearchHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class FailingResearchPublishParticipant :
        IDungeonRestoreTransactionParticipant
    {
        public string ParticipantId => "999.research.debug.publish-failure";

        public void BeginRestoreCandidate()
        {
        }

        public void PublishRestoreCandidate()
        {
            throw new InvalidOperationException(
                "Injected research participant publication failure.");
        }

        public void DiscardRestoreCandidate()
        {
        }
    }

    private sealed class FixedUnlockStateService : IFacilityShopUnlockStateService
    {
        private readonly FacilityShopUnlockState state = new FacilityShopUnlockState();
        public FacilityShopUnlockState GetUnlockState() => state;
    }

    private sealed class ResearchScenarioSaveSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        public ResearchScenarioSaveSection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase,
            params string[] dependencies)
        {
            SectionId = sectionId;
            RestorePhase = restorePhase;
            this.dependencies = dependencies ?? Array.Empty<string>();
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => dependencies;
        public int RemainingCommitFailures { get; set; }

        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException("Research scenario version mismatch.");
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                if (RemainingCommitFailures <= 0)
                {
                    return;
                }

                RemainingCommitFailures--;
                throw new InvalidOperationException(
                    "Injected late research restore failure.");
            });
        }
    }

    private sealed class ResearchDiscardObserver :
        IDungeonRestoreTransactionParticipant
    {
        private readonly BlueprintResearchRuntime runtime;
        private readonly ResearchProjectId projectId;
        private bool hasCandidate;

        public ResearchDiscardObserver(
            BlueprintResearchRuntime runtime,
            ResearchProjectId projectId)
        {
            this.runtime = runtime;
            this.projectId = projectId;
        }

        public string ParticipantId => "research.debug.discard-observer";
        public int DiscardCount { get; private set; }
        public float ObservedProgress { get; private set; }
        public bool ObservedQueued { get; private set; }

        public void BeginRestoreCandidate()
        {
            hasCandidate = true;
        }

        public void PublishRestoreCandidate()
        {
            hasCandidate = false;
        }

        public void DiscardRestoreCandidate()
        {
            if (!hasCandidate)
            {
                return;
            }

            hasCandidate = false;
            DiscardCount++;
            ObservedProgress = runtime.State.Projects
                .GetProgress(projectId)
                .Progress;
            ObservedQueued = runtime.State.Projects.ContainsInQueue(projectId);
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

        public BlueprintResearchWorkResult ApplyApprovedWork(
            CharacterActor researcher,
            BuildableObject facility,
            float approvedWorkUnits) => default;

        public IReadOnlyList<KnowledgeResidueTaskSaveData> Capture() =>
            Array.Empty<KnowledgeResidueTaskSaveData>();

        public KnowledgeResidueRestoreCandidate PrepareRestore(
            IEnumerable<KnowledgeResidueTaskSaveData> tasks) =>
            new KnowledgeResidueRestoreCandidate(
                new KnowledgeResidueAggregateState());

        public void Restore(KnowledgeResidueRestoreCandidate candidate) { }
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

    private sealed class MutableCapacityQuery : IResearchFacilityCapacityQuery
    {
        private readonly Dictionary<ResearchFacilityCapabilityId, int> values =
            new Dictionary<ResearchFacilityCapabilityId, int>();

        public int Version { get; private set; }

        public void Set(ResearchFacilityCapabilityId capability, int value)
        {
            values[capability] = Mathf.Max(0, value);
            Version++;
        }

        public int GetAvailable(ResearchFacilityCapabilityId capability)
        {
            return values.TryGetValue(capability, out int value) ? value : 0;
        }

        public bool MeetsRequirements(ResearchProjectSO project, out string blocker)
        {
            ResearchFacilityRequirement? missing = project?.FacilityRequirements
                .FirstOrDefault(requirement =>
                    GetAvailable(requirement.capability) < requirement.requiredCount);
            if (!missing.HasValue || missing.Value.requiredCount <= 0)
            {
                blocker = string.Empty;
                return true;
            }

            ResearchFacilityRequirement requirement = missing.Value;
            blocker = $"연구 시설 수용력 부족: "
                + $"{ResearchFacilityCapacityQuery.GetDisplayName(requirement.capability)} "
                + $"{GetAvailable(requirement.capability)}/{requirement.requiredCount}";
            return false;
        }

        public string FormatRequirements(ResearchProjectSO project)
        {
            return string.Join(
                " · ",
                project?.FacilityRequirements.Select(requirement =>
                    $"{ResearchFacilityCapacityQuery.GetDisplayName(requirement.capability)} "
                    + $"{GetAvailable(requirement.capability)}/{requirement.requiredCount}")
                ?? Array.Empty<string>());
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
