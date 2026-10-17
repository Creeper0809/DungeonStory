#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class DungeonSpaceExpansionDebugScenarios
{
    private const string ExpansionObserverId =
        "infrastructure.space-expansion-observer";
    private const string ReportPath =
        "Artifacts/QA/v27-balance-expansion-editmode.txt";

    // BDC-S01: exercises the same production command used by both launch modes,
    // then the live research-completion event and restore reconciliation hooks.
    public static void RunOrThrow()
    {
        VerifyNewRunTrigger(DungeonGameplayLaunchMode.NewRun);
        VerifyNewRunTrigger(DungeonGameplayLaunchMode.PreparedNewRun);
        VerifyResearchParentAndRestore();
        Debug.Log("BDC-S01 dungeon-space causal trigger scenarios PASS");
    }

    public static void RunBDCS01FromBatch()
    {
        const string selector = "DUNGEONSTORY_U0_BDCS01_SPACE_EXPANSION_VERIFY";
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(selector),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "BDC-S01 requires its dedicated isolated batch selector.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "BDC-S01 requires a create-only audit output directory.");
        }

        string reportPath = Path.Combine(
            Path.GetFullPath(output),
            "dungeon-space-bdcs01-report.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)
            ?? throw new InvalidOperationException(
                "BDC-S01 report directory is missing."));
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "BDC-S01 evidence is immutable: " + reportPath);
        }

        try
        {
            RunOrThrow();
            using (FileStream stream = new(
                       reportPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read))
            using (StreamWriter writer = new(stream))
            {
                writer.WriteLine("BDC_S01=PASS");
                writer.WriteLine("newRun=true");
                writer.WriteLine("preparedNewRun=true");
                writer.WriteLine("researchProjects=3");
                writer.WriteLine("restoreReconcile=true");
                writer.WriteLine("sameTargetNoOp=true");
                writer.WriteLine("exactResearchParents=true");
                writer.WriteLine("oldSaveMigrationAdded=false");
                writer.WriteLine("fullAuditExecuted=false");
                writer.WriteLine("humanApprovalRecorded=false");
                writer.WriteLine("trainingExecuted=false");
            }
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            using (FileStream stream = new(
                       reportPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read))
            using (StreamWriter writer = new(stream))
            {
                writer.WriteLine("BDC_S01=FAIL");
                writer.WriteLine(exception.ToString());
            }
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void VerifyNewRunTrigger(DungeonGameplayLaunchMode mode)
    {
        Require(mode is DungeonGameplayLaunchMode.NewRun
            or DungeonGameplayLaunchMode.PreparedNewRun,
            "A production new-run launch mode is required.");
        GameEventBus events = new();
        GameplayOutcomeRegistry registry = CreateBdcRegistry();
        GameplayOutcomeLedger ledger = new(
            registry,
            GameplayOutcomeBufferLimits.Default,
            new GameplayOutcomeRunId("run:bdc-space:" + mode),
            1L);
        GameplayOutcomeRecorder recorder = new(ledger, registry, events);
        DomainFactDispatcher facts = ConnectExpansionObserver(
            registry,
            ledger,
            recorder);
        TestGridAuthority grid = new(CreateInitialGrid());
        DungeonSpaceExpansionRuntime runtime = new(
            events, grid, grid, EditorFixedGameCalendar.Instance,
            facts,
            facts,
            new EditorBlueprintResearchStateService());
        try
        {
            Require(runtime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult first,
                    out string failure)
                && first.Changed,
                mode + " did not execute the production Tier-0 command: "
                + failure);
            RequireLayout(grid.Grid, 29, 60, new Vector2Int(17, 0));
            GameplayResultKey key = DungeonSpaceExpansionOutcomeIds.ResultKey(
                DungeonSpaceExpansionCatalog.TierZeroInitializationId,
                29L);
            Require(ledger.TryGetResultIdentity(key,
                    out GameplayOutcomeReplayIdentity identity)
                && ledger.TryGetExact(identity.OutcomeId,
                    out GameplayOutcomeSnapshot outcome)
                && outcome.outcomeTypeId
                    == DungeonSpaceExpansionOutcomeIds.Expanded.Value
                && outcome.participants.Any(participant => participant.roleId
                    == DungeonSpaceExpansionOutcomeIds.InitializationRole.Value)
                && !outcome.participants.Any(participant => participant.roleId
                    == DungeonSpaceExpansionOutcomeIds.UnlockingResearchRole.Value)
                && outcome.facts.Any(fact => fact.factId
                        == DungeonSpaceExpansionOutcomeIds.TriggerFact.Value
                    && fact.value
                        == DungeonSpaceExpansionTrigger.NewRunInitialization
                            .ToString())
                && string.IsNullOrEmpty(outcome.parentRunId),
                mode + " Tier-0 outcome was misrepresented as research.");
            Require(runtime.TryReconcileNewRunTierZero(out var repeated,
                    out failure)
                && !repeated.Changed
                && ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
                mode + " same-target reconciliation created another result.");
        }
        finally
        {
            runtime.Dispose();
            facts.Dispose();
            events.Clear();
        }
    }

    private static void VerifyResearchParentAndRestore()
    {
        ResearchProjectSO[] projects =
        {
            CreateBdcProject(DungeonSpaceExpansionCatalog.QuarryResearchId,
                "채석장"),
            CreateBdcProject(DungeonSpaceExpansionCatalog.StonecuttingResearchId,
                "석재 가공"),
            CreateBdcProject(DungeonSpaceExpansionCatalog.DeepMiningResearchId,
                "심부 채굴")
        };
        GameEventBus events = new();
        GameplayOutcomeRegistry registry = CreateBdcRegistry();
        const string RunId = "run:bdc-space-research";
        GameplayOutcomeLedger ledger = new(
            registry, GameplayOutcomeBufferLimits.Default,
            new GameplayOutcomeRunId(RunId), 1L);
        GameplayOutcomeRecorder recorder = new(ledger, registry, events);
        DomainFactDispatcher facts = ConnectExpansionObserver(
            registry,
            ledger,
            recorder);
        BlueprintResearchState state = new();
        TestGridAuthority grid = new(CreateInitialGrid());
        DungeonSpaceExpansionRuntime runtime = new(
            events, grid, grid, EditorFixedGameCalendar.Instance,
            facts,
            facts,
            new EditorBlueprintResearchStateService(state));
        DungeonSpaceExpansionRuntime restoredRuntime = null;
        GameEventBus restoredEvents = null;
        DomainFactDispatcher restoredFacts = null;
        try
        {
            Require(runtime.TryReconcileNewRunTierZero(out _, out _),
                "Research fixture could not enter Tier 0.");
            GameplayResultKey quarryParent = CommitBdcResearchParent(
                recorder, state, projects[0], 1L);
            DungeonResearchSaveData researchSave = new()
            {
                outcomeSequence = state.OutcomeSequence,
                completedProjectIds = state.Projects.CompletedProjectIds.ToList(),
                completedProjectResults = state.CompletedProjectResultKeys
                    .Select(pair => new DungeonResearchCompletionResultSaveData
                    {
                        projectId = pair.Key,
                        publicationKey = pair.Value
                    }).ToList()
            };
            researchSave = JsonUtility.FromJson<DungeonResearchSaveData>(
                JsonUtility.ToJson(researchSave));
            GameplayOutcomeLedgerSaveData ledgerSave =
                ledger.CaptureGameplayOutcomes();
            DungeonGameRestoreReport report = new();
            ResearchOutcomeSavePreflight.ValidateCompletedProjectResultKeys(
                researchSave, ledgerSave, report);
            Require(report.Success
                && researchSave.completedProjectResults.Count == 1,
                "Committed research parent did not survive save/outbox preflight.");

            BlueprintResearchState restoredState = new();
            restoredState.RestoreOutcomeSequence(researchSave.outcomeSequence);
            foreach (DungeonResearchCompletionResultSaveData row in
                     researchSave.completedProjectResults)
            {
                restoredState.Projects.RestoreCompleted(
                    new ResearchProjectId(row.projectId));
                restoredState.RecordCompletedProjectResultKey(row.projectId,
                    row.publicationKey);
            }
            restoredEvents = new GameEventBus();
            GameplayOutcomeLedger restoredLedger = new(
                registry, GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(RunId), 1L);
            GameplayOutcomeLedgerRestoreCandidate restoredLedgerCandidate =
                restoredLedger.PrepareGameplayOutcomeRestore(ledgerSave);
            restoredLedger.BeginRestoreCandidate();
            restoredLedger.PublishGameplayOutcomeRestore(
                restoredLedgerCandidate);
            restoredLedger.PublishRestoreCandidate();
            restoredLedger.CompleteRestoreCandidate();
            GameplayOutcomeRecorder restoredRecorder = new(
                restoredLedger, registry, restoredEvents);
            restoredFacts = ConnectExpansionObserver(
                registry,
                restoredLedger,
                restoredRecorder);
            TestGridAuthority restoredGrid = new(CreateTierZeroGrid());
            restoredRuntime = new DungeonSpaceExpansionRuntime(
                restoredEvents, restoredGrid, restoredGrid,
                EditorFixedGameCalendar.Instance,
                restoredFacts,
                restoredFacts,
                new EditorBlueprintResearchStateService(restoredState));
            restoredRuntime.Start();
            restoredRuntime.OnRestoreCompleted();
            restoredRuntime.Tick();
            RequireLayout(restoredGrid.Grid, 51, 68,
                new Vector2Int(17, 0));
            VerifyBdcParent(restoredLedger, projects[0], quarryParent, 51L);
            int count = restoredLedger.CaptureGameplayOutcomes()
                .exactOutcomes.Count;
            restoredEvents.Publish(new BlueprintResearchCompletedEvent(
                projects[0], default, quarryParent.ToString()));
            Require(!restoredRuntime.LastResult.Changed
                && restoredLedger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count == count,
                "Same-target quarry event duplicated the expansion.");

            for (int index = 1; index < projects.Length; index++)
            {
                GameplayResultKey parent = CommitBdcResearchParent(
                    restoredRecorder, restoredState, projects[index],
                    index + 1L);
                restoredEvents.Publish(new BlueprintResearchCompletedEvent(
                    projects[index], default, parent.ToString()));
                int expectedColumns = index == 1 ? 71 : 87;
                VerifyBdcParent(restoredLedger, projects[index], parent,
                    expectedColumns);
                RequireLayout(restoredGrid.Grid, expectedColumns,
                    index == 1 ? 88 : 104,
                    new Vector2Int(17, 0));
            }
            Require(restoredLedger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count == 7,
                "Tier 0, three research parents, and three expansions were not exact once.");
        }
        finally
        {
            restoredRuntime?.Dispose();
            runtime.Dispose();
            restoredFacts?.Dispose();
            facts.Dispose();
            restoredEvents?.Clear();
            events.Clear();
            foreach (ResearchProjectSO project in projects)
                UnityEngine.Object.DestroyImmediate(project);
        }
    }

    private static GameplayOutcomeRegistry CreateBdcRegistry() => new(
        new IGameplayOutcomeDescriptor[]
        {
            new ResearchWorkOutcomeDescriptor(),
            new DungeonSpaceExpansionOutcomeDescriptor(
                new KoreanJosaFormatter())
        },
        new IGameplayOutcomeAdapterRegistration[]
        {
            new ResearchWorkOutcomeAdapter(),
            new DungeonSpaceExpansionOutcomeAdapter()
        });

    private static DomainFactDispatcher ConnectExpansionObserver(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder)
    {
        DomainFactDispatcher facts = new();
        try
        {
            facts.Subscribe(
                ExpansionObserverId,
                new GameplayDomainFactObserver<
                    DungeonSpaceExpandedFact,
                    DungeonSpaceExpansionOutcomeReceipt>(
                    fact => DungeonSpaceExpansionFactObservation.Capture(
                        fact,
                        ledger),
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            return facts;
        }
        catch
        {
            facts.Dispose();
            throw;
        }
    }

    private static ResearchProjectSO CreateBdcProject(
        string projectId, string name)
    {
        ResearchProjectSO project =
            ScriptableObject.CreateInstance<ResearchProjectSO>();
        project.Configure(projectId, name, name, ResearchField.Mining,
            1f, ResearchBlueprintRule.None, null,
            Array.Empty<ResearchProjectSO>());
        return project;
    }

    private static GameplayResultKey CommitBdcResearchParent(
        GameplayOutcomeRecorder recorder,
        BlueprintResearchState state,
        ResearchProjectSO project,
        long revision)
    {
        GameplayResultKey key = new(
            ResearchWorkOutcomeIds.ProducerId,
            new GameplayOperationId("research-work:" + revision),
            revision, 0);
        ResearchWorkOutcomeReceipt receipt = new(
            key, 0, project.ProjectId.Value, project.DisplayName,
            string.Empty, string.Empty, string.Empty, string.Empty,
            0f, project.RequiredWork, project.RequiredWork, true,
            Array.Empty<BlueprintUnlockRecord>(),
            ResearchWorkCause.ImmediateCompletion);
        Require(recorder.TryPrepare(receipt,
                out PreparedOutcomeToken prepared).Success,
            "Research parent outcome prepare failed.");
        Require(recorder.CommitPrepared(prepared, revision,
                out CommittedOutcomeToken committed).Success,
            "Research parent outcome commit failed.");
        Require(recorder.TryDeliver(committed).Published
            && recorder.Acknowledge(committed).Success,
            "Research parent delivery failed.");
        state.Projects.RestoreCompleted(project.ProjectId);
        state.RestoreOutcomeSequence(revision);
        state.RecordCompletedProjectResultKey(
            project.ProjectId.Value,
            key.ToString());
        return key;
    }

    private static void VerifyBdcParent(
        GameplayOutcomeLedger ledger,
        ResearchProjectSO project,
        GameplayResultKey parent,
        long expansionRevision)
    {
        GameplayResultKey child = DungeonSpaceExpansionOutcomeIds.ResultKey(
            project.ProjectId.Value, expansionRevision);
        Require(ledger.TryGetResultIdentity(parent,
                out GameplayOutcomeReplayIdentity parentIdentity)
            && ledger.TryGetResultIdentity(child,
                out GameplayOutcomeReplayIdentity childIdentity)
            && ledger.TryGetExact(childIdentity.OutcomeId,
                out GameplayOutcomeSnapshot outcome)
            && outcome.parentRunId == parentIdentity.OutcomeId.RunId.Value
            && outcome.parentSequence == parentIdentity.OutcomeId.Sequence
            && outcome.facts.Any(fact => fact.factId
                    == DungeonSpaceExpansionOutcomeIds.TriggerFact.Value
                && fact.value
                    == DungeonSpaceExpansionTrigger.ResearchCompletion.ToString())
            && outcome.facts.Any(fact => fact.factId
                    == DungeonSpaceExpansionOutcomeIds.ParentOperationFact.Value
                && fact.value == parent.OperationId.Value),
            "Dungeon-space child did not preserve its exact committed research parent.");
    }

    [MenuItem("DungeonStory/V27/Verify Research-Gated Dungeon Expansion")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Research-gated dungeon expansion scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> rows = new List<string>();
        List<string> failures = new List<string>();
        Verify(
            "EXPANSION_RESEARCH_ASSETS_EXACT",
            VerifyResearchAssets,
            rows,
            failures);
        Verify(
            "EXPANSION_NEW_RUN_TIER_ZERO_27_TO_29_RESTORE_PRESERVED",
            VerifyNewRunTierZeroAndRestorePreservation,
            rows,
            failures);
        Verify(
            "EXPANSION_NEW_RUN_TIER_ZERO_REJECTS_NONCANONICAL_WIDTHS",
            VerifyNewRunTierZeroRejectsNoncanonicalWidths,
            rows,
            failures);
        Verify(
            "EXPANSION_EVENT_29_51_71_87_EXACT",
            VerifyResearchEventExpansion,
            rows,
            failures);
        Verify(
            "EXPANSION_OUTCOME_REJECTION_ROLLS_BACK_GRID",
            VerifyOutcomeCommitRejectionRollsBackGrid,
            rows,
            failures);
        Verify(
            "EXPANSION_POSTCOMMIT_OBSERVER_FAULT_ISOLATED",
            VerifyPostCommitObserverFaultIsolated,
            rows,
            failures);
        Verify(
            "EXPANSION_RESEARCH_STATE_RETRIES_PREPARE_DEFERRAL",
            VerifyResearchStateRetriesPrepareDeferral,
            rows,
            failures);
        Verify(
            "EXPANSION_RESTORE_HOOK_RECONCILES_COMPLETED_RESEARCH",
            VerifyRestoreHookReconcilesCompletedResearch,
            rows,
            failures);
        Verify(
            "EXPANSION_SAVE_V5_LAYOUT_ROUNDTRIP_EXACT",
            VerifySaveLayoutRoundTrip,
            rows,
            failures);
        Verify(
            "EXPANSION_OUT_OF_ORDER_DEEP_IDEMPOTENT",
            VerifyDeepCompletionDirectExpansion,
            rows,
            failures);
        Verify(
            "EXPANSION_GRID_COPY_ATOMIC_AND_OCCUPANTS_PRESERVED",
            VerifyDetachedExpansionCopy,
            rows,
            failures);
        Verify(
            "EXPANSION_SAVE_V5_LAYOUT_ROUNDTRIP_EXACT",
            VerifySaveLayoutRoundTrip,
            rows,
            failures);
        Verify(
            "EXPANSION_SAVE_RESEARCH_LAYOUT_AUTHORITY_EXACT",
            VerifySaveResearchLayoutAuthority,
            rows,
            failures);
        Verify(
            "EXPANSION_CAPTURE_AND_RESTORE_PREFLIGHT_EXACT",
            VerifyExpansionPreflightContract,
            rows,
            failures);
        Verify(
            "EXPANSION_E_KEY_DEVELOPER_ONLY",
            VerifyDeveloperKeyIsolation,
            rows,
            failures);
        Verify(
            "EXPANSION_GAMEPLAY_SCENE_SHA_UNCHANGED",
            VerifyGameplaySceneShaUnchanged,
            rows,
            failures);

        rows.Insert(
            0,
            failures.Count == 0
                ? "RESULT=PASS; failures=0; research=3; columns=29,51,71,87; sceneSeed=27;"
                : $"RESULT=FAIL; failures={failures.Count}; research=3; columns=29,51,71,87; sceneSeed=27;");
        rows.AddRange(failures.Select(failure => "DETAIL\t" + failure));
        WriteReport(rows);

        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }
        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("Research-gated dungeon expansion scenarios passed.");
        }

        return failures.Count == 0;
    }

    public static bool RunOutcomeIntegrationScenarios(bool logSuccess)
    {
        List<string> rows = new();
        List<string> failures = new();
        Verify(
            "EXPANSION_EVENT_29_51_71_87_EXACT",
            VerifyResearchEventExpansion,
            rows,
            failures);
        Verify(
            "EXPANSION_OUTCOME_REJECTION_ROLLS_BACK_GRID",
            VerifyOutcomeCommitRejectionRollsBackGrid,
            rows,
            failures);
        Verify(
            "EXPANSION_POSTCOMMIT_OBSERVER_FAULT_ISOLATED",
            VerifyPostCommitObserverFaultIsolated,
            rows,
            failures);
        Verify(
            "EXPANSION_RESEARCH_STATE_RETRIES_PREPARE_DEFERRAL",
            VerifyResearchStateRetriesPrepareDeferral,
            rows,
            failures);
        Verify(
            "EXPANSION_RESTORE_HOOK_RECONCILES_COMPLETED_RESEARCH",
            VerifyRestoreHookReconcilesCompletedResearch,
            rows,
            failures);
        foreach (string failure in failures)
            Debug.LogError(failure);
        if (failures.Count == 0 && logSuccess)
            Debug.Log("Dungeon-space outcome integration scenarios passed.");
        return failures.Count == 0;
    }

    private static bool VerifyResearchAssets()
    {
        Dictionary<string, ResearchProjectSO> projects = LoadProjects();
        Require(projects.Count == 180, $"Expected 180 research projects; found {projects.Count}.");

        VerifyProject(
            projects,
            DungeonSpaceExpansionCatalog.QuarryResearchId,
            numericId: 7082,
            requiredWork: 28f,
            expectedPrerequisites: new[]
            {
                "research:mining:surface"
            },
            expectedFacilities: new[]
            {
                ResearchFacilityCapabilityId.Basic
            });
        VerifyProject(
            projects,
            DungeonSpaceExpansionCatalog.StonecuttingResearchId,
            numericId: 7083,
            requiredWork: 42f,
            expectedPrerequisites: new[]
            {
                "research:mining:quarry"
            },
            expectedFacilities: new[]
            {
                ResearchFacilityCapabilityId.Basic,
                ResearchFacilityCapabilityId.Design
            });
        VerifyProject(
            projects,
            DungeonSpaceExpansionCatalog.DeepMiningResearchId,
            numericId: 7085,
            requiredWork: 60f,
            expectedPrerequisites: new[]
            {
                "research:mining:sorting"
            },
            expectedFacilities: new[]
            {
                ResearchFacilityCapabilityId.Basic,
                ResearchFacilityCapabilityId.Design,
                ResearchFacilityCapabilityId.Advanced
            });

        float totalWork = projects.Values.Sum(project => project.RequiredWork);
        Require(Mathf.Approximately(totalWork, 63173f),
            $"Expected research work total 63173; found {totalWork:0.###}.");
        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects.Values);
        Require(catalog.Validate().Count == 0,
            "The 180-project research catalog failed validation.");
        return true;
    }

    private static bool VerifyResearchEventExpansion()
    {
        Dictionary<string, ResearchProjectSO> projects = LoadProjects();
        Grid initial = CreateInitialGrid();
        TestGridAuthority authority = new TestGridAuthority(initial);
        GameEventBus events = new GameEventBus();
        BlueprintResearchState researchState = new();
        DungeonSpaceExpansionRuntime runtime =
            CreateRuntime(
                events,
                authority,
                out DomainFactDispatcher facts,
                out GameplayOutcomeRecorder recorder,
                researchStateService:
                    new EditorBlueprintResearchStateService(researchState));
        runtime.Start();
        try
        {
            ResearchProjectSO unrelated = projects["research:mining:surface"];
            events.Publish(new BlueprintResearchCompletedEvent(unrelated, default));
            Require(ReferenceEquals(authority.Grid, initial),
                "Unrelated research replaced the dungeon grid.");
            RequireLayout(authority.Grid, 27, 60, new Vector2Int(17, 0));

            Require(runtime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult tierZero,
                    out string tierZeroFailure),
                "New-run Tier-0 reconciliation failed: " + tierZeroFailure);
            Require(tierZero.Changed
                    && tierZero.PreviousInteriorColumns == 27
                    && tierZero.CurrentInteriorColumns == 29,
                "New-run Tier-0 reconciliation did not publish exact 27-to-29 columns.");
            RequireLayout(authority.Grid, 29, 60, new Vector2Int(17, 0));

            Grid beforeQuarry = authority.Grid;
            Publish(
                events,
                recorder,
                researchState,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId]);
            RequireLayout(authority.Grid, 51, 68, new Vector2Int(17, 0));
            Require(beforeQuarry.GetGridCell(new Vector2Int(46, 0)).AreaType
                    == GridCellAreaType.BlockedExterior,
                "Quarry expansion mutated the previously published grid in place.");

            Grid quarryGrid = authority.Grid;
            Publish(
                events,
                recorder,
                researchState,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId]);
            Require(ReferenceEquals(authority.Grid, quarryGrid),
                "Re-completing quarry expansion was not idempotent.");
            Require(!runtime.LastResult.Changed,
                "Idempotent quarry expansion reported a mutation.");

            Publish(
                events,
                recorder,
                researchState,
                projects[DungeonSpaceExpansionCatalog.StonecuttingResearchId]);
            RequireLayout(authority.Grid, 71, 88, new Vector2Int(17, 0));

            Publish(
                events,
                recorder,
                researchState,
                projects[DungeonSpaceExpansionCatalog.DeepMiningResearchId]);
            RequireLayout(authority.Grid, 87, 104, new Vector2Int(17, 0));
            Require(runtime.LastResult.Changed
                    && runtime.LastResult.AddedInteriorColumns == 16,
                "Deep expansion result did not report the exact +16 columns.");
            Require(authority.PublicationCount == 4,
                $"Expected Tier-0 plus 3 research publications; found {authority.PublicationCount}.");
            Require(authority.CompletionCount == 4,
                $"Expected Tier-0 plus 3 completed publications; found {authority.CompletionCount}.");
        }
        finally
        {
            runtime.Dispose();
            facts.Dispose();
            events.Clear();
        }

        return true;
    }

    private static bool VerifyNewRunTierZeroAndRestorePreservation()
    {
        Grid restoredTierZero = CreateTierZeroGrid();
        TestGridAuthority restoredAuthority = new(restoredTierZero);
        GameEventBus restoredEvents = new();
        DungeonSpaceExpansionRuntime restoredRuntime =
            CreateRuntime(
                restoredEvents,
                restoredAuthority,
                out DomainFactDispatcher restoredFacts,
                out _);
        restoredRuntime.Start();
        try
        {
            Require(ReferenceEquals(restoredAuthority.Grid, restoredTierZero)
                    && restoredAuthority.PublicationCount == 0,
                "Runtime startup mutated a current-format restored Tier-0 layout.");
            RequireLayout(restoredAuthority.Grid, 29, 60, new Vector2Int(17, 0));
        }
        finally
        {
            restoredRuntime.Dispose();
            restoredFacts.Dispose();
            restoredEvents.Clear();
        }

        Grid sceneSeed = CreateInitialGrid();
        TestGridAuthority newRunAuthority = new(sceneSeed);
        GameEventBus newRunEvents = new();
        DungeonSpaceExpansionRuntime newRunRuntime =
            CreateRuntime(
                newRunEvents,
                newRunAuthority,
                out DomainFactDispatcher newRunFacts,
                out _);
        try
        {
            Require(newRunRuntime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult result,
                    out string failureReason),
                "Explicit new-run Tier-0 reconciliation failed: " + failureReason);
            Require(result.Changed
                    && result.AddedInteriorColumns == 2
                    && newRunAuthority.PublicationCount == 1,
                "Explicit new-run reconciliation did not publish exactly one +2 expansion.");
            RequireLayout(newRunAuthority.Grid, 29, 60, new Vector2Int(17, 0));
            Require(newRunRuntime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult repeated,
                    out failureReason)
                    && !repeated.Changed
                    && newRunAuthority.PublicationCount == 1,
                "Repeated new-run Tier-0 reconciliation was not idempotent: "
                + failureReason);
        }
        finally
        {
            newRunRuntime.Dispose();
            newRunFacts.Dispose();
            newRunEvents.Clear();
        }

        return true;
    }

    private static bool VerifyDeepCompletionDirectExpansion()
    {
        Dictionary<string, ResearchProjectSO> projects = LoadProjects();
        Grid initial = CreateInitialGrid();
        TestGridAuthority authority = new TestGridAuthority(initial);
        GameEventBus events = new GameEventBus();
        BlueprintResearchState researchState = new();
        DungeonSpaceExpansionRuntime runtime =
            CreateRuntime(
                events,
                authority,
                out DomainFactDispatcher facts,
                out GameplayOutcomeRecorder recorder,
                researchStateService:
                    new EditorBlueprintResearchStateService(researchState));
        runtime.Start();
        try
        {
            Require(runtime.TryReconcileNewRunTierZero(out _, out string tierZeroFailure),
                "Direct-deep fixture could not reconcile Tier 0: " + tierZeroFailure);
            Publish(
                events,
                recorder,
                researchState,
                projects[DungeonSpaceExpansionCatalog.DeepMiningResearchId]);
            RequireLayout(authority.Grid, 87, 104, new Vector2Int(17, 0));
            Require(runtime.LastResult.Changed
                    && runtime.LastResult.AddedInteriorColumns == 58,
                "Direct deep-mining completion did not publish the exact 29-to-87 expansion.");

            Grid deepGrid = authority.Grid;
            Publish(
                events,
                recorder,
                researchState,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId]);
            Require(ReferenceEquals(authority.Grid, deepGrid)
                    && !runtime.LastResult.Changed,
                "Late quarry completion changed an already-expanded deep layout.");
            Publish(
                events,
                recorder,
                researchState,
                projects[DungeonSpaceExpansionCatalog.StonecuttingResearchId]);
            Require(ReferenceEquals(authority.Grid, deepGrid)
                    && !runtime.LastResult.Changed,
                "Late stonecutting completion changed an already-expanded deep layout.");
            Require(authority.PublicationCount == 2
                    && authority.CompletionCount == 2,
                $"Tier-0 plus direct deep expansion expected two publications; found {authority.PublicationCount}/{authority.CompletionCount}.");
        }
        finally
        {
            runtime.Dispose();
            facts.Dispose();
            events.Clear();
        }

        return true;
    }

    private static bool VerifyOutcomeCommitRejectionRollsBackGrid()
    {
        Grid initial = CreateInitialGrid();
        TestGridAuthority authority = new(initial);
        GameEventBus events = new();
        EditorDungeonSpaceExpansionFactPublisher publisher = new(
            rejectCommit: true);
        DungeonSpaceExpansionRuntime runtime = CreateRuntime(
            events,
            authority,
            out DomainFactDispatcher facts,
            out _,
            publisher);
        try
        {
            Require(!runtime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult result,
                    out string failureReason),
                "Injected outcome rejection unexpectedly succeeded.");
            Require(ReferenceEquals(authority.Grid, initial)
                    && authority.PublicationCount == 2
                    && authority.CompletionCount == 0
                    && !result.Changed
                    && publisher.PrepareCount == 1
                    && publisher.CommitCount == 1
                    && failureReason.Contains(
                        "injected-dungeon-space-commit-rejection",
                        StringComparison.Ordinal),
                "Outcome rejection did not restore the original grid atomically: "
                + failureReason);
        }
        finally
        {
            runtime.Dispose();
            facts.Dispose();
            events.Clear();
        }
        return true;
    }

    private static bool VerifyPostCommitObserverFaultIsolated()
    {
        Grid initial = CreateInitialGrid();
        TestGridAuthority authority = new(initial)
        {
            ThrowOnCompletion = true
        };
        GameEventBus events = new();
        EditorDungeonSpaceExpansionFactPublisher publisher = new();
        DungeonSpaceExpansionRuntime runtime = CreateRuntime(
            events,
            authority,
            out DomainFactDispatcher facts,
            out _,
            publisher);
        try
        {
            Require(runtime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult result,
                    out string failureReason),
                "Post-commit observer fault escaped the expansion command: "
                + failureReason);
            Require(result.Changed
                    && result.OutcomeOwnerRevision
                        == DungeonSpaceExpansionCatalog.InitialInteriorColumns
                    && authority.PublicationCount == 1
                    && authority.CompletionCount == 1
                    && publisher.PrepareCount == 1
                    && publisher.CommitCount == 1,
                "Post-commit observer fault changed the committed expansion result.");
            RequireLayout(
                authority.Grid,
                DungeonSpaceExpansionCatalog.InitialInteriorColumns,
                60,
                new Vector2Int(17, 0));
        }
        finally
        {
            runtime.Dispose();
            facts.Dispose();
            events.Clear();
        }
        return true;
    }

    private static bool VerifyResearchStateRetriesPrepareDeferral()
    {
        Dictionary<string, ResearchProjectSO> projects = LoadProjects();
        Grid tierZero = CreateTierZeroGrid();
        TestGridAuthority authority = new(tierZero);
        GameEventBus events = new();
        BlueprintResearchState researchState = new();
        researchState.Projects.RestoreCompleted(
            new ResearchProjectId(DungeonSpaceExpansionCatalog.QuarryResearchId));
        researchState.RecordCompletedProjectResultKey(
            DungeonSpaceExpansionCatalog.QuarryResearchId,
            SyntheticResearchKey(
                DungeonSpaceExpansionCatalog.QuarryResearchId).ToString());
        EditorBlueprintResearchStateService research = new(researchState);
        EditorDungeonSpaceExpansionFactPublisher publisher = new(
            deferPrepareCount: 1);
        DungeonSpaceExpansionRuntime runtime = CreateRuntime(
            events,
            authority,
            out DomainFactDispatcher facts,
            out GameplayOutcomeRecorder recorder,
            publisher,
            research);
        runtime.Start();
        try
        {
            Publish(
                events,
                recorder,
                researchState,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId]);
            Require(ReferenceEquals(authority.Grid, tierZero)
                    && runtime.HasPendingExpansion
                    && string.Equals(
                        runtime.PendingResearchProjectId,
                        DungeonSpaceExpansionCatalog.QuarryResearchId,
                        StringComparison.Ordinal)
                    && runtime.PendingFailureReason.Contains(
                        "injected-dungeon-space-prepare-deferral",
                        StringComparison.Ordinal)
                    && publisher.PrepareCount == 1
                    && publisher.CommitCount == 0,
                "A deferred prepare was not retained as an observable pending expansion.");

            for (int index = 0; index <= 60; index++)
                runtime.Tick();

            RequireLayout(
                authority.Grid,
                DungeonSpaceExpansionCatalog.BasicSectorTargetColumns,
                68,
                new Vector2Int(17, 0));
            Require(!runtime.HasPendingExpansion
                    && string.IsNullOrEmpty(runtime.PendingResearchProjectId)
                    && string.IsNullOrEmpty(runtime.PendingFailureReason)
                    && publisher.PrepareCount == 2
                    && publisher.CommitCount == 1
                    && authority.PublicationCount == 1
                    && authority.CompletionCount == 1,
                "Completed research did not retry the exact deferred expansion once.");

            for (int index = 0; index < 120; index++)
                runtime.Tick();
            Require(publisher.PrepareCount == 2
                    && authority.PublicationCount == 1,
                "A reconciled expansion kept retrying after success.");
        }
        finally
        {
            runtime.Dispose();
            facts.Dispose();
            events.Clear();
        }
        return true;
    }

    private static bool VerifyRestoreHookReconcilesCompletedResearch()
    {
        Grid tierZero = CreateTierZeroGrid();
        TestGridAuthority authority = new(tierZero);
        GameEventBus events = new();
        BlueprintResearchState researchState = new();
        researchState.Projects.RestoreCompleted(
            new ResearchProjectId(DungeonSpaceExpansionCatalog.DeepMiningResearchId));
        researchState.RecordCompletedProjectResultKey(
            DungeonSpaceExpansionCatalog.DeepMiningResearchId,
            SyntheticResearchKey(
                DungeonSpaceExpansionCatalog.DeepMiningResearchId).ToString());
        EditorDungeonSpaceExpansionFactPublisher publisher = new();
        DungeonSpaceExpansionRuntime runtime = CreateRuntime(
            events,
            authority,
            out DomainFactDispatcher facts,
            out GameplayOutcomeRecorder recorder,
            publisher,
            new EditorBlueprintResearchStateService(researchState));
        runtime.Start();
        try
        {
            CommitSyntheticResearchParent(
                recorder,
                researchState,
                LoadProjects()[DungeonSpaceExpansionCatalog.DeepMiningResearchId]);
            runtime.OnRestoreCompleted();
            runtime.Tick();

            RequireLayout(
                authority.Grid,
                DungeonSpaceExpansionCatalog.DeepSectorTargetColumns,
                DungeonSpaceExpansionCatalog.MaximumSupportedGridWidth,
                new Vector2Int(17, 0));
            Require(!runtime.HasPendingExpansion
                    && runtime.LastResult.Changed
                    && string.Equals(
                        runtime.LastResult.ResearchProjectId,
                        DungeonSpaceExpansionCatalog.DeepMiningResearchId,
                        StringComparison.Ordinal)
                    && publisher.PrepareCount == 1
                    && publisher.CommitCount == 1
                    && authority.PublicationCount == 1
                    && authority.CompletionCount == 1,
                "The restore-completed hook did not reconcile the highest completed expansion exactly once.");
        }
        finally
        {
            runtime.Dispose();
            facts.Dispose();
            events.Clear();
        }
        return true;
    }

    private static bool VerifyDetachedExpansionCopy()
    {
        Grid grid = CreateTierZeroGrid();
        TestOccupant occupant = new TestOccupant(901);
        Vector2Int occupied = new Vector2Int(20, 0);
        Require(grid.RegisterOccupant(
                occupant,
                GridLayer.Building,
                new[] { occupied },
                connectPositions: false),
            "Could not register the expansion-copy fixture occupant.");

        Grid expanded = grid.TryExpandGrid(6, 0);
        Require(expanded != null, "Grid expansion copy returned null.");
        Require(!ReferenceEquals(
                grid.GetGridCell(occupied),
                expanded.GetGridCell(occupied)),
            "Expanded grid reused a live GridCell reference.");
        Require(ReferenceEquals(
                expanded.GetGridCell(occupied).GetOccupant(GridLayer.Building),
                occupant),
            "Expanded grid lost the registered occupant.");
        Require(ReferenceEquals(
                grid.GetGridCell(occupied).GetOccupant(GridLayer.Building),
                occupant),
            "Building the expansion candidate mutated the live occupant registration.");
        return true;
    }

    private static bool VerifySaveLayoutRoundTrip()
    {
        Grid grid = CreateTierZeroGrid();
        foreach (DungeonSpaceExpansionDefinition definition in
                 DungeonSpaceExpansionCatalog.All)
        {
            int targetEnd = 17 + definition.TargetInteriorColumns;
            int widthDelta = Mathf.Max(0, targetEnd - grid.width);
            Grid expanded = grid.TryExpandGrid(widthDelta, 0);
            Require(expanded != null,
                $"Could not allocate tier {definition.Tier} save fixture.");
            for (int x = 17 + (definition.Tier == 1
                         ? DungeonSpaceExpansionCatalog.InitialInteriorColumns
                         : DungeonSpaceExpansionCatalog.All[definition.Tier - 2]
                             .TargetInteriorColumns);
                 x < targetEnd;
                 x++)
            {
                for (int y = 0; y < expanded.height; y++)
                {
                    expanded.SetAreaType(
                        new Vector2Int(x, y),
                        GridCellAreaType.DungeonInterior);
                }
            }
            grid = expanded;
        }

        ModularFacilityWorldSaveData original = new ModularFacilityWorldSaveData
        {
            version = ModularFacilityWorldSaveService.CurrentVersion,
            gridWidth = grid.width,
            gridHeight = grid.height,
            gridCells = grid.GetCells()
                .OrderBy(cell => cell.Position.y)
                .ThenBy(cell => cell.Position.x)
                .Select(ModularFacilityGridCellSaveData.From)
                .ToList()
        };
        string json = JsonUtility.ToJson(original);
        ModularFacilityWorldSaveData restored =
            JsonUtility.FromJson<ModularFacilityWorldSaveData>(json);
        Require(restored != null
                && restored.version == ModularFacilityWorldSaveService.CurrentVersion,
            "V5 facility layout did not survive JSON round-trip.");
        Require(DungeonSpaceGridLayout.TryCapture(
                restored,
                out DungeonInteriorLayoutSnapshot layout,
                out string failureReason),
            "Restored V5 layout is invalid: " + failureReason);
        Require(layout.StartX == 17
                && layout.ColumnCount == 87
                && layout.EntrancePosition == new Vector2Int(17, 0),
            $"Restored V5 layout mismatch: start={layout.StartX}; columns={layout.ColumnCount}; entrance={layout.EntrancePosition}.");
        return true;
    }

    private static bool VerifyDeveloperKeyIsolation()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string runtimeSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs"));
        string controllerSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "Assets/Scripts/Controllers/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs"));
        Require(!runtimeSource.Contains("GridExpand(", StringComparison.Ordinal),
            "Production expansion runtime calls the developer GridExpand command.");
        Require(controllerSource.Contains(
                "debugMode?.IsDeveloperModeEnabled != true",
                StringComparison.Ordinal)
                && controllerSource.Contains("gridSystem.GridExpand(2, 2);", StringComparison.Ordinal),
            "The E-key expansion is not visibly guarded as a developer-only command.");
        return true;
    }

    private static bool VerifyNewRunTierZeroRejectsNoncanonicalWidths()
    {
        foreach (int columns in new[] { 28, 30, 51 })
        {
            Grid original = CreateGridWithInteriorColumns(columns);
            TestGridAuthority authority = new(original);
            GameEventBus events = new();
            DungeonSpaceExpansionRuntime runtime = CreateRuntime(
                events,
                authority,
                out DomainFactDispatcher facts,
                out _);
            try
            {
                Require(!runtime.TryReconcileNewRunTierZero(
                        out _,
                        out string failureReason),
                    $"New-run Tier-0 reconciliation accepted noncanonical {columns}-column input.");
                Require(failureReason.Contains(
                        $"found {columns}",
                        StringComparison.Ordinal)
                    && ReferenceEquals(authority.Grid, original)
                    && authority.PublicationCount == 0
                    && authority.CompletionCount == 0,
                    $"Noncanonical {columns}-column input was not rejected atomically: {failureReason}");
            }
            finally
            {
                runtime.Dispose();
                facts.Dispose();
                events.Clear();
            }
        }
        return true;
    }

    private static bool VerifyGameplaySceneShaUnchanged()
    {
        const string expectedSha256 =
            "B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8";
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string scenePath = Path.Combine(
            projectRoot,
            "Assets/Scenes/GameplayScene.unity");
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(scenePath);
        string actual = BitConverter.ToString(sha.ComputeHash(stream))
            .Replace("-", string.Empty);
        Require(string.Equals(actual, expectedSha256, StringComparison.Ordinal),
            $"GameplayScene SHA drifted: expected={expectedSha256}; actual={actual}.");
        return true;
    }

    private static bool VerifySaveResearchLayoutAuthority()
    {
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                Array.Empty<string>()) == 29,
            "A save with no expansion research did not resolve to 29 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.QuarryResearchId }) == 51,
            "A quarry-completed save did not resolve to 51 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.StonecuttingResearchId }) == 71,
            "A stonecutting-only restored set did not resolve to 71 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.DeepMiningResearchId }) == 87,
            "A deep-mining-only restored set did not resolve to 87 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[]
                {
                    DungeonSpaceExpansionCatalog.DeepMiningResearchId,
                    DungeonSpaceExpansionCatalog.QuarryResearchId,
                    DungeonSpaceExpansionCatalog.StonecuttingResearchId
                }) == 87,
            "The highest completed mining expansion was not authoritative.");
        return true;
    }

    private static bool VerifyExpansionPreflightContract()
    {
        Require(typeof(IDungeonCapturedSavePreflightValidator).IsAssignableFrom(
                typeof(DungeonAggregateReferencePreflight)),
            "Dungeon aggregate expansion validation is missing from the captured-save boundary.");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string registrationSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs"));
        Require(registrationSource.Contains(
                ".As<IDungeonCapturedSavePreflightValidator>();",
                StringComparison.Ordinal),
            "Dungeon aggregate preflight is not registered for captured saves.");

        VerifyExpansionPreflight(
            completedResearchIds: Array.Empty<string>(),
            interiorColumns: 29,
            expectedSuccess: true,
            expectedErrorFragment: string.Empty);
        VerifyExpansionPreflight(
            completedResearchIds: Array.Empty<string>(),
            interiorColumns: 27,
            expectedSuccess: false,
            expectedErrorFragment: "expects 29 interior columns, save contains 27");
        VerifyExpansionPreflight(
            completedResearchIds: new[]
            {
                DungeonSpaceExpansionCatalog.QuarryResearchId
            },
            interiorColumns: 29,
            expectedSuccess: false,
            expectedErrorFragment: "expects 51 interior columns, save contains 29");
        VerifyExpansionPreflight(
            completedResearchIds: Array.Empty<string>(),
            interiorColumns: 51,
            expectedSuccess: false,
            expectedErrorFragment: "expects 29 interior columns, save contains 51");
        return true;
    }

    private static void VerifyExpansionPreflight(
        IReadOnlyList<string> completedResearchIds,
        int interiorColumns,
        bool expectedSuccess,
        string expectedErrorFragment)
    {
        DungeonResearchSaveData research = new()
        {
            completedProjectIds = completedResearchIds.ToList()
        };
        Grid grid = CreateGridWithInteriorColumns(interiorColumns);
        ModularFacilityWorldSaveData facilities = new()
        {
            version = ModularFacilityWorldSaveService.CurrentVersion,
            gridWidth = grid.width,
            gridHeight = grid.height,
            gridCells = grid.GetCells()
                .OrderBy(cell => cell.Position.y)
                .ThenBy(cell => cell.Position.x)
                .Select(ModularFacilityGridCellSaveData.From)
                .ToList()
        };
        DungeonGameRestoreReport report = new();
        MethodInfo validator = typeof(DungeonAggregateReferencePreflight)
            .GetMethod(
                "ValidateDungeonExpansionResearch",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Dungeon expansion aggregate preflight entry point is unavailable.");
        validator.Invoke(null, new object[] { research, facilities, report });
        Require(report.Success == expectedSuccess,
            $"Expansion preflight result mismatch for {interiorColumns} columns: "
            + string.Join(" | ", report.Errors));
        if (!expectedSuccess)
        {
            Require(report.Errors.Any(error => error.Contains(
                    expectedErrorFragment,
                    StringComparison.Ordinal)),
                $"Expansion preflight did not report '{expectedErrorFragment}': "
                + string.Join(" | ", report.Errors));
        }
    }

    private static void VerifyProject(
        IReadOnlyDictionary<string, ResearchProjectSO> projects,
        string stableId,
        int numericId,
        float requiredWork,
        IReadOnlyList<string> expectedPrerequisites,
        IReadOnlyList<ResearchFacilityCapabilityId> expectedFacilities)
    {
        Require(projects.TryGetValue(stableId, out ResearchProjectSO project),
            $"Research project is missing: {stableId}.");
        Require(project.id == numericId,
            $"Research numeric ID mismatch for {stableId}: {project.id}.");
        Require(Mathf.Approximately(project.RequiredWork, requiredWork),
            $"Research WU mismatch for {stableId}: {project.RequiredWork:0.###}.");
        string[] prerequisites = project.Prerequisites
            .Select(value => value.ProjectId.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(prerequisites.SequenceEqual(
                expectedPrerequisites.OrderBy(value => value, StringComparer.Ordinal)),
            $"Research prerequisite mismatch for {stableId}: {string.Join(",", prerequisites)}.");
        ResearchFacilityCapabilityId[] facilities = project.FacilityRequirements
            .Select(value => value.capability)
            .OrderBy(value => value)
            .ToArray();
        Require(facilities.SequenceEqual(expectedFacilities.OrderBy(value => value)),
            $"Research facility requirement mismatch for {stableId}: {string.Join(",", facilities)}.");
        Require(project.BlueprintRule == ResearchBlueprintRule.None
                && project.Blueprint == null,
            $"Expansion research unexpectedly requires a blueprint: {stableId}.");
    }

    private static Dictionary<string, ResearchProjectSO> LoadProjects()
    {
        return AssetDatabase.FindAssets(
                "t:ResearchProjectSO",
                new[] { "Assets/Resources/SO/Research/Projects" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null && project.ProjectId.IsValid)
            .ToDictionary(
                project => project.ProjectId.Value,
                project => project,
                StringComparer.Ordinal);
    }

    private static Grid CreateInitialGrid()
    {
        return CreateGridWithInteriorColumns(
            DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns);
    }

    private static Grid CreateGridWithInteriorColumns(int interiorColumns)
    {
        int width = Mathf.Max(60, 17 + interiorColumns);
        Grid grid = new Grid(width, DungeonSpaceExpansionCatalog.SupportedGridHeight);
        foreach (GridCell cell in grid.GetCells())
        {
            grid.SetAreaType(cell.Position, GridCellAreaType.BlockedExterior);
        }
        for (int x = 17; x < 17 + interiorColumns; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, y),
                    x == 17 && y == 0
                        ? GridCellAreaType.Entrance
                        : GridCellAreaType.DungeonInterior);
            }
        }
        return grid;
    }

    private static Grid CreateTierZeroGrid()
    {
        Grid initial = CreateInitialGrid();
        TestGridAuthority authority = new(initial);
        GameEventBus events = new();
        DungeonSpaceExpansionRuntime runtime = CreateRuntime(
            events,
            authority,
            out DomainFactDispatcher facts,
            out _);
        try
        {
            Require(runtime.TryReconcileNewRunTierZero(out _, out string failureReason),
                "Tier-0 fixture reconciliation failed: " + failureReason);
            return authority.Grid;
        }
        finally
        {
            runtime.Dispose();
            facts.Dispose();
            events.Clear();
        }
    }

    private static void Publish(
        GameEventBus events,
        GameplayOutcomeRecorder recorder,
        BlueprintResearchState researchState,
        ResearchProjectSO project)
    {
        CommitSyntheticResearchParent(recorder, researchState, project);
        events.Publish(new BlueprintResearchCompletedEvent(
            project,
            default,
            SyntheticResearchKey(project.ProjectId.Value).ToString()));
    }

    private static void CommitSyntheticResearchParent(
        GameplayOutcomeRecorder recorder,
        BlueprintResearchState researchState,
        ResearchProjectSO project)
    {
        if (recorder == null || researchState == null || project == null)
            throw new ArgumentNullException("Synthetic research parent dependencies are required.");
        GameplayResultKey key = SyntheticResearchKey(project.ProjectId.Value);
        ResearchWorkOutcomeReceipt receipt = new(
            key,
            0,
            project.ProjectId.Value,
            project.DisplayName,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0f,
            project.RequiredWork,
            project.RequiredWork,
            true,
            Array.Empty<BlueprintUnlockRecord>(),
            ResearchWorkCause.ImmediateCompletion);
        OutcomePrepareResult preparation = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken prepared);
        if (!preparation.Success)
        {
            Require(preparation.Code is OutcomePrepareCode.AlreadyCommitted
                    or OutcomePrepareCode.AlreadyPublished
                    or OutcomePrepareCode.AlreadyTerminal,
                "Synthetic research parent could not be prepared: "
                + preparation.Code + ":" + preparation.DetailCode);
        }
        else
        {
            Require(recorder.CommitPrepared(
                        prepared,
                        key.CommitRevision,
                        out CommittedOutcomeToken committed)
                    .Success
                && recorder.TryDeliver(committed).Published
                && recorder.Acknowledge(committed).Success,
                "Synthetic research parent did not commit exactly once.");
        }
        researchState.Projects.RestoreCompleted(project.ProjectId);
        researchState.RecordCompletedProjectResultKey(
            project.ProjectId.Value,
            key.ToString());
    }

    private static GameplayResultKey SyntheticResearchKey(string projectId)
    {
        int sequence = string.Equals(projectId,
                DungeonSpaceExpansionCatalog.QuarryResearchId,
                StringComparison.Ordinal) ? 1
            : string.Equals(projectId,
                DungeonSpaceExpansionCatalog.StonecuttingResearchId,
                StringComparison.Ordinal) ? 2 : 3;
        return new GameplayResultKey(
            ResearchWorkOutcomeIds.ProducerId,
            new GameplayOperationId("research-work:" + sequence),
            sequence,
            0);
    }

    private static DungeonSpaceExpansionRuntime CreateRuntime(
        GameEventBus events,
        TestGridAuthority authority,
        out DomainFactDispatcher facts,
        out GameplayOutcomeRecorder recorder,
        EditorDungeonSpaceExpansionFactPublisher publisher = null,
        IBlueprintResearchStateService researchStateService = null)
    {
        GameplayOutcomeRegistry registry = CreateBdcRegistry();
        GameplayOutcomeLedger ledger = new(
            registry,
            GameplayOutcomeBufferLimits.Default);
        recorder = new GameplayOutcomeRecorder(ledger, registry, events);
        facts = ConnectExpansionObserver(registry, ledger, recorder);
        IDomainFactStagingPublisher staging = facts;
        if (publisher != null)
        {
            publisher.Attach(facts);
            staging = publisher;
        }
        try
        {
            return new DungeonSpaceExpansionRuntime(
                events,
                authority,
                authority,
                EditorFixedGameCalendar.Instance,
                staging,
                facts,
                researchStateService ?? new EditorBlueprintResearchStateService());
        }
        catch
        {
            facts.Dispose();
            throw;
        }
    }

    private static void RequireLayout(
        Grid grid,
        int expectedColumns,
        int expectedWidth,
        Vector2Int expectedEntrance)
    {
        Require(grid != null && grid.width == expectedWidth,
            $"Grid width mismatch: expected {expectedWidth}; found {grid?.width ?? 0}.");
        Require(DungeonSpaceGridLayout.TryCapture(
                grid,
                out DungeonInteriorLayoutSnapshot layout,
                out string failureReason),
            "Grid layout is invalid: " + failureReason);
        Require(layout.ColumnCount == expectedColumns
                && layout.EntrancePosition == expectedEntrance,
            $"Layout mismatch: columns={layout.ColumnCount}; entrance={layout.EntrancePosition}.");
    }

    private static void Verify(
        string marker,
        Func<bool> scenario,
        ICollection<string> rows,
        ICollection<string> failures)
    {
        try
        {
            if (!scenario())
            {
                throw new InvalidOperationException("Scenario returned false.");
            }
            rows.Add("PASS\t" + marker);
        }
        catch (Exception exception)
        {
            rows.Add("FAIL\t" + marker);
            failures.Add(marker + ": " + exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void WriteReport(IEnumerable<string> rows)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string fullPath = Path.Combine(projectRoot, ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        File.WriteAllText(fullPath, string.Join("\n", rows) + "\n");
    }

    private sealed class EditorDungeonSpaceExpansionFactPublisher :
        IDomainFactStagingPublisher
    {
        private readonly bool rejectCommit;
        private int remainingPrepareDeferrals;
        private IDomainFactStagingPublisher inner;

        internal EditorDungeonSpaceExpansionFactPublisher(
            bool rejectCommit = false,
            int deferPrepareCount = 0)
        {
            if (deferPrepareCount < 0)
                throw new ArgumentOutOfRangeException(nameof(deferPrepareCount));
            this.rejectCommit = rejectCommit;
            remainingPrepareDeferrals = deferPrepareCount;
        }

        internal int PrepareCount { get; private set; }
        internal int CommitCount { get; private set; }

        internal void Attach(IDomainFactStagingPublisher publisher)
        {
            if (publisher == null) throw new ArgumentNullException(nameof(publisher));
            if (inner != null)
                throw new InvalidOperationException(
                    "A dungeon-space fact publisher fixture cannot be rebound.");
            inner = publisher;
        }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            if (inner == null)
                throw new InvalidOperationException(
                    "A dungeon-space fact publisher fixture is not attached.");
            PrepareCount++;
            if (remainingPrepareDeferrals > 0)
            {
                remainingPrepareDeferrals--;
                stage = null;
                failure = "injected-dungeon-space-prepare-deferral";
                return false;
            }
            if (!inner.TryStage(fact, out IDomainFactStage prepared, out failure))
            {
                stage = null;
                return false;
            }
            stage = new FaultStage(this, prepared);
            return true;
        }

        private sealed class FaultStage : IDomainFactStage
        {
            private readonly EditorDungeonSpaceExpansionFactPublisher owner;
            private readonly IDomainFactStage inner;

            internal FaultStage(
                EditorDungeonSpaceExpansionFactPublisher owner,
                IDomainFactStage inner)
            {
                this.owner = owner;
                this.inner = inner;
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                owner.CommitCount++;
                if (!owner.rejectCommit) return inner.Commit();
                inner.Cancel();
                return DomainFactDelivery.Reject(
                    "injected-dungeon-space-commit-rejection");
            }

            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class TestGridAuthority : IGridSystemProvider, IGridSystemPublisher
    {
        public TestGridAuthority(Grid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public GridSystemManager Manager => null;
        public Grid Grid { get; private set; }
        public int PublicationCount { get; private set; }
        public int CompletionCount { get; private set; }
        public bool ThrowOnCompletion { get; set; }

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return grid != null;
        }

        public bool TryPublishGrid(
            Grid expectedCurrent,
            Grid replacement,
            out string failureReason)
        {
            if (!ReferenceEquals(Grid, expectedCurrent) || replacement == null)
            {
                failureReason = "Grid publication expectation changed.";
                return false;
            }

            Grid = replacement;
            PublicationCount++;
            failureReason = string.Empty;
            return true;
        }

        public void CompleteGridPublication()
        {
            CompletionCount++;
            if (ThrowOnCompletion)
            {
                throw new InvalidOperationException(
                    "injected-dungeon-space-observer-fault");
            }
        }
    }

    private sealed class TestOccupant : IGridOccupant
    {
        public TestOccupant(int id)
        {
            GridId = id;
        }

        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => true;
        public bool IsGridMovement => false;
    }
}
#endif
