#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal static class DungeonSpaceExpansionCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:infrastructure-dungeon-space";
    internal const string QuarryActionId =
        "action:infrastructure-dungeon-space-quarry";
    internal const string StonecuttingActionId =
        "action:infrastructure-dungeon-space-stonecutting";

    private const string OwnerKind =
        "owner-kind:infrastructure-dungeon-space";
    private const string StateSchema =
        "dungeonstory-causal-dungeon-space-owner-state@1";
    private const int EntranceX = 17;
    private const int EntranceY = 0;
    private const int InitialGridWidth = 60;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A dungeon-space checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                QuarryActionId,
                "infrastructure",
                DungeonSpaceExpansionOutcomeIds.Expanded,
                context.CaptureState,
                context.ExecuteQuarry,
                context.RequireQuarryOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                StonecuttingActionId,
                "infrastructure",
                DungeonSpaceExpansionOutcomeIds.Expanded,
                () => new Dictionary<string, object>(),
                context.ExecuteStonecutting,
                context.RequireStonecuttingOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunOwnerFamily(bool log = false)
    {
        const string PersistentOwnerId =
            "dungeon-space:causal-focused:main-interior";
        JObject quarryCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-dungeon-space:quarry",
                   PersistentOwnerId)))
        {
            IProductionCausalActionFixture quarry = fixtures.Get(
                QuarryActionId);
            IReadOnlyDictionary<string, object> before =
                quarry.CaptureState();
            Require((int)before["dungeonSpace.stage"] == 0
                    && (int)before["dungeonSpace.interiorColumns"] == 29
                    && (int)before["dungeonSpace.gridWidth"] == 60,
                "Dungeon-space owner did not start at canonical Tier 0.");
            Require(quarry.Execute(),
                "The real quarry research event did not expand the Grid.");
            Require(quarry.RequireExactOutcome().HasCanonicalPayloadHash,
                "The quarry expansion has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = quarry.CaptureState();
            Require((int)after["dungeonSpace.stage"] == 1
                    && (int)after["dungeonSpace.interiorColumns"] == 51
                    && (int)after["dungeonSpace.gridWidth"] == 68
                    && (int)after["dungeonSpace.exactOutcomeCount"] == 2,
                "The quarry expansion did not publish its exact Grid state.");
            quarryCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)quarry)
                .CaptureOwnerCheckpoint();
        }

        JObject stonecuttingCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-dungeon-space:stonecutting",
                   PersistentOwnerId,
                   quarryCheckpoint)))
        {
            IProductionCausalActionFixture quarry = fixtures.Get(
                QuarryActionId);
            IProductionCausalActionFixture stonecutting = fixtures.Get(
                StonecuttingActionId);
            IReadOnlyDictionary<string, object> restored =
                quarry.CaptureState();
            Require((int)restored["dungeonSpace.stage"] == 1
                    && (int)restored["dungeonSpace.interiorColumns"] == 51
                    && !quarry.Execute(),
                "Fresh composition restore lost or replayed the quarry expansion.");
            Require(stonecutting.Execute(),
                "The real stonecutting research event did not expand the Grid.");
            Require(stonecutting.RequireExactOutcome().HasCanonicalPayloadHash,
                "The stonecutting expansion has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = quarry.CaptureState();
            Require((int)after["dungeonSpace.stage"] == 2
                    && (int)after["dungeonSpace.interiorColumns"] == 71
                    && (int)after["dungeonSpace.gridWidth"] == 88
                    && (int)after["dungeonSpace.exactOutcomeCount"] == 4,
                "The stonecutting expansion did not extend the same Grid owner.");
            stonecuttingCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)stonecutting)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-dungeon-space:terminal",
                   PersistentOwnerId,
                   stonecuttingCheckpoint)))
        {
            IProductionCausalActionFixture quarry = fixtures.Get(
                QuarryActionId);
            IProductionCausalActionFixture stonecutting = fixtures.Get(
                StonecuttingActionId);
            IReadOnlyDictionary<string, object> restored =
                quarry.CaptureState();
            Require((int)restored["dungeonSpace.stage"] == 2
                    && (int)restored["dungeonSpace.interiorColumns"] == 71
                    && !quarry.Execute()
                    && !stonecutting.Execute(),
                "Terminal dungeon-space owner was not idempotent.");
        }

        JObject tamperedOwner = (JObject)quarryCheckpoint.DeepClone();
        ((JObject)tamperedOwner["domainState"])["ownerId"] =
            "dungeon-space:tampered-owner";
        RequireRestoreRejected(
            tamperedOwner,
            PersistentOwnerId,
            "A tampered dungeon-space owner identity was accepted.");

        JObject tamperedGrid = (JObject)quarryCheckpoint.DeepClone();
        ((JObject)((JObject)tamperedGrid["domainState"])["gridState"])[
            "gridWidth"] = 69;
        RequireRestoreRejected(
            tamperedGrid,
            PersistentOwnerId,
            "A tampered dungeon-space grid width was accepted.");

        if (log)
            Debug.Log("Dungeon-space expansion causal owner family PASS");
        return true;
    }

    public static void RunOwnerFamilyBatch()
    {
        try
        {
            Require(RunOwnerFamily(log: true),
                "Dungeon-space owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void RequireRestoreRejected(
        JObject checkpoint,
        string ownerId,
        string message)
    {
        bool rejected = false;
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-dungeon-space:tampered",
                ownerId,
                checkpoint));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly GameEventBus events = new();
        private readonly TestGridAuthority gridAuthority;
        private readonly GameplayOutcomeLedger ledger;
        private readonly GameplayOutcomeRecorder recorder;
        private readonly DomainFactDispatcher facts;
        private readonly BlueprintResearchState researchState = new();
        private readonly DungeonSpaceExpansionRuntime runtime;
        private readonly ResearchProjectSO quarryProject;
        private readonly ResearchProjectSO stonecuttingProject;
        private int stage;
        private long quarryRevision;
        private long stonecuttingRevision;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            try
            {
                quarryProject = CreateResearchProject(
                    DungeonSpaceExpansionCatalog.QuarryResearchId,
                    "채석장");
                stonecuttingProject = CreateResearchProject(
                    DungeonSpaceExpansionCatalog.StonecuttingResearchId,
                    "석재 가공");
                gridAuthority = new TestGridAuthority(CreateTierZeroGrid());
                string ledgerRunId = HasPersistentOwner
                    ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                        this.ownerId,
                        OwnerKind)
                    : causalRunId + ":dungeon-space-ledger";
                GameplayOutcomeRegistry registry = new(
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
                ledger = new GameplayOutcomeLedger(
                    registry,
                    GameplayOutcomeBufferLimits.Default,
                    new GameplayOutcomeRunId(ledgerRunId),
                    1L);
                recorder = new GameplayOutcomeRecorder(
                    ledger,
                    registry,
                    events);
                facts = new DomainFactDispatcher();
                facts.Subscribe(
                    "infrastructure.space-expansion-observer",
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
                runtime = new DungeonSpaceExpansionRuntime(
                    events,
                    gridAuthority,
                    gridAuthority,
                    EditorFixedGameCalendar.Instance,
                    facts,
                    facts,
                    new EditorBlueprintResearchStateService(researchState));
                runtime.Start();

                if (checkpoint != null)
                {
                    JObject domain = ProductionCausalOwnerCheckpoint
                        .RestoreFamilyCommon(
                            checkpoint,
                            this.ownerId,
                            OwnerKind,
                            CausalOwnerRestoreKind,
                            ledger);
                    RestoreDomainState(domain);
                }
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            DungeonInteriorLayoutSnapshot layout = RequireLayout();
            return new Dictionary<string, object>
            {
                ["dungeonSpace.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "dungeon-space:ephemeral",
                ["dungeonSpace.stage"] = stage,
                ["dungeonSpace.tier"] = stage,
                ["dungeonSpace.interiorColumns"] = layout.ColumnCount,
                ["dungeonSpace.gridWidth"] = gridAuthority.Grid.width,
                ["dungeonSpace.entranceX"] = layout.EntrancePosition.x,
                ["dungeonSpace.entranceY"] = layout.EntrancePosition.y,
                ["dungeonSpace.exactOutcomeCount"] = ledger
                    .CaptureGameplayOutcomes().exactOutcomes.Count,
                ["dungeonSpace.quarryCompleted"] = stage >= 1,
                ["dungeonSpace.stonecuttingCompleted"] = stage >= 2
            };
        }

        internal bool ExecuteQuarry()
        {
            ThrowIfDisposed();
            if (stage != 0)
                return false;
            PublishResearch(quarryProject);
            DungeonSpaceExpansionResult result = runtime.LastResult;
            if (!result.Changed
                || !string.Equals(
                    result.ResearchProjectId,
                    DungeonSpaceExpansionCatalog.QuarryResearchId,
                    StringComparison.Ordinal)
                || result.PreviousInteriorColumns != 29
                || result.CurrentInteriorColumns != 51)
            {
                return false;
            }
            quarryRevision = result.OutcomeOwnerRevision;
            stage = 1;
            ValidateState();
            return RequireQuarryOutcome(false).HasCanonicalPayloadHash;
        }

        internal bool ExecuteStonecutting()
        {
            ThrowIfDisposed();
            if (stage != 1)
                return false;
            PublishResearch(stonecuttingProject);
            DungeonSpaceExpansionResult result = runtime.LastResult;
            if (!result.Changed
                || !string.Equals(
                    result.ResearchProjectId,
                    DungeonSpaceExpansionCatalog.StonecuttingResearchId,
                    StringComparison.Ordinal)
                || result.PreviousInteriorColumns != 51
                || result.CurrentInteriorColumns != 71)
            {
                return false;
            }
            stonecuttingRevision = result.OutcomeOwnerRevision;
            stage = 2;
            ValidateState();
            return RequireStonecuttingOutcome(false).HasCanonicalPayloadHash;
        }

        internal GameplayOutcomeReplayIdentity RequireQuarryOutcome() =>
            RequireQuarryOutcome(true);

        internal GameplayOutcomeReplayIdentity RequireStonecuttingOutcome() =>
            RequireStonecuttingOutcome(true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
                throw new InvalidOperationException(
                    "Ephemeral dungeon-space fixtures have no checkpoint.");
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                CaptureDomainState(),
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            runtime?.Dispose();
            facts?.Dispose();
            events.Clear();
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        private void PublishResearch(ResearchProjectSO project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            long revision = string.Equals(
                    project.ProjectId.Value,
                    DungeonSpaceExpansionCatalog.QuarryResearchId,
                    StringComparison.Ordinal)
                ? 1L
                : 2L;
            GameplayResultKey parentKey = new(
                ResearchWorkOutcomeIds.ProducerId,
                new GameplayOperationId("research-work:" + revision),
                revision,
                0);
            ResearchWorkOutcomeReceipt receipt = new(
                parentKey,
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
            Require(recorder.TryPrepare(
                    receipt,
                    out PreparedOutcomeToken prepared).Success,
                "Dungeon-space research parent prepare failed.");
            Require(recorder.CommitPrepared(
                    prepared,
                    revision,
                    out CommittedOutcomeToken committed).Success,
                "Dungeon-space research parent commit failed.");
            Require(recorder.TryDeliver(committed).Published
                    && recorder.Acknowledge(committed).Success,
                "Dungeon-space research parent delivery failed.");
            researchState.Projects.RestoreCompleted(project.ProjectId);
            researchState.RestoreOutcomeSequence(revision);
            researchState.RecordCompletedProjectResultKey(
                project.ProjectId.Value,
                parentKey.ToString());
            events.Publish(new BlueprintResearchCompletedEvent(
                project,
                default,
                parentKey.ToString()));
        }

        private ResearchProjectSO CreateResearchProject(
            string projectId,
            string displayName)
        {
            ResearchProjectSO project =
                ScriptableObject.CreateInstance<ResearchProjectSO>();
            cleanup.Add(project);
            project.name = "CausalDungeonSpace_" + displayName;
            project.Configure(
                projectId,
                displayName,
                displayName + " 연구를 완료한다.",
                ResearchField.Mining,
                1f,
                ResearchBlueprintRule.None,
                null,
                Array.Empty<ResearchProjectSO>());
            return project;
        }

        private JObject CaptureDomainState() => new()
        {
            ["schemaVersion"] = 1,
            ["schema"] = StateSchema,
            ["stage"] = stage,
            ["ownerId"] = ownerId,
            ["gridState"] = ParseJson(
                JsonUtility.ToJson(CaptureGridState()),
                "grid state"),
            ["quarryRevision"] = quarryRevision,
            ["stonecuttingRevision"] = stonecuttingRevision
        };

        private void RestoreDomainState(JObject domain)
        {
            RequireFields(
                domain,
                "dungeon-space domain state",
                "schemaVersion",
                "schema",
                "stage",
                "ownerId",
                "gridState",
                "quarryRevision",
                "stonecuttingRevision");
            if ((int)domain["schemaVersion"] != 1
                || !string.Equals(
                    (string)domain["schema"],
                    StateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["ownerId"],
                    ownerId,
                    StringComparison.Ordinal)
                || domain["gridState"] is not JObject gridJson)
            {
                throw new InvalidOperationException(
                    "Dungeon-space checkpoint identity is invalid.");
            }
            int restoredStage = (int)domain["stage"];
            if (restoredStage is < 0 or > 2)
                throw new InvalidOperationException(
                    "Dungeon-space checkpoint stage is invalid.");
            ModularFacilityWorldSaveData gridState =
                Deserialize<ModularFacilityWorldSaveData>(
                    gridJson,
                    "grid state");
            gridAuthority.Restore(PrepareGrid(gridState));
            stage = restoredStage;
            quarryRevision = (long)domain["quarryRevision"];
            stonecuttingRevision = (long)domain["stonecuttingRevision"];
            RestoreResearchParentState();
            ValidateState();
            if (!JToken.DeepEquals(domain, CaptureDomainState()))
            {
                throw new InvalidOperationException(
                    "Dungeon-space state changed during fresh restore.");
            }
        }

        private ModularFacilityWorldSaveData CaptureGridState() => new()
        {
            version = ModularFacilityWorldSaveService.CurrentVersion,
            gridWidth = gridAuthority.Grid.width,
            gridHeight = gridAuthority.Grid.height,
            gridCells = gridAuthority.Grid.GetCells()
                .Where(value => value != null)
                .OrderBy(value => value.Position.y)
                .ThenBy(value => value.Position.x)
                .Select(ModularFacilityGridCellSaveData.From)
                .ToList(),
            buildings = new List<ModularFacilityBuildingSaveData>()
        };

        private static Grid PrepareGrid(ModularFacilityWorldSaveData state)
        {
            if (state == null
                || state.version != ModularFacilityWorldSaveService.CurrentVersion
                || state.gridWidth < 1
                || state.gridHeight != DungeonSpaceExpansionCatalog
                    .SupportedGridHeight
                || state.gridCells == null
                || state.buildings == null
                || state.buildings.Count != 0
                || (long)state.gridCells.Count
                    != (long)state.gridWidth * state.gridHeight)
            {
                throw new InvalidOperationException(
                    "Dungeon-space grid checkpoint shape is invalid.");
            }
            Grid grid = new(state.gridWidth, state.gridHeight);
            var seen = new HashSet<Vector2Int>();
            foreach (ModularFacilityGridCellSaveData saved in state.gridCells)
            {
                if (saved == null)
                    throw new InvalidOperationException(
                        "Dungeon-space grid checkpoint contains a null cell.");
                Vector2Int position = new(saved.x, saved.y);
                if (!grid.IsValidGridPos(position) || !seen.Add(position))
                    throw new InvalidOperationException(
                        "Dungeon-space grid checkpoint cell identity is invalid.");
                grid.SetAreaType(position, saved.areaType);
                grid.SetTerrainType(position, saved.terrainType);
            }
            if (!DungeonSpaceGridLayout.TryCapture(
                    grid,
                    out _,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Dungeon-space restored Grid is invalid: " + failureReason);
            }
            return grid;
        }

        private void ValidateState()
        {
            ThrowIfDisposed();
            DungeonInteriorLayoutSnapshot layout = RequireLayout();
            int[] expectedColumns = { 29, 51, 71 };
            int[] expectedWidths = { 60, 68, 88 };
            int exactCount = ledger.CaptureGameplayOutcomes()
                .exactOutcomes.Count;
            if (layout.StartX != EntranceX
                || layout.EntrancePosition != new Vector2Int(
                    EntranceX,
                    EntranceY)
                || layout.ColumnCount != expectedColumns[stage]
                || gridAuthority.Grid.width != expectedWidths[stage]
                || exactCount != stage * 2)
            {
                throw new InvalidOperationException(
                    "Dungeon-space Grid state does not match its causal stage.");
            }
            if (stage == 0
                && (quarryRevision != 0L || stonecuttingRevision != 0L))
            {
                throw new InvalidOperationException(
                    "Initial dungeon-space state contains terminal revisions.");
            }
            if (stage >= 1)
                RequireQuarryOutcome(false);
            if (stage >= 2)
                RequireStonecuttingOutcome(false);
            if (stage < 2 && stonecuttingRevision != 0L)
            {
                throw new InvalidOperationException(
                    "Stonecutting revision appeared before its causal stage.");
            }
        }

        private void RestoreResearchParentState()
        {
            if (stage >= 1)
                RestoreResearchParent(quarryProject, 1L);
            if (stage >= 2)
                RestoreResearchParent(stonecuttingProject, 2L);
        }

        private void RestoreResearchParent(
            ResearchProjectSO project,
            long revision)
        {
            GameplayResultKey key = new(
                ResearchWorkOutcomeIds.ProducerId,
                new GameplayOperationId("research-work:" + revision),
                revision,
                0);
            Require(ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged,
                "Restored dungeon-space research parent is missing.");
            researchState.Projects.RestoreCompleted(project.ProjectId);
            researchState.RestoreOutcomeSequence(revision);
            researchState.RecordCompletedProjectResultKey(
                project.ProjectId.Value,
                key.ToString());
        }

        private DungeonInteriorLayoutSnapshot RequireLayout()
        {
            if (!runtime.TryCaptureLayout(
                    out DungeonInteriorLayoutSnapshot layout,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Dungeon-space layout is unavailable: " + failureReason);
            }
            return layout;
        }

        private GameplayOutcomeReplayIdentity RequireQuarryOutcome(
            bool throwOnFailure) => RequireExact(
                DungeonSpaceExpansionCatalog.QuarryResearchId,
                quarryRevision,
                29,
                51,
                throwOnFailure,
                "quarry");

        private GameplayOutcomeReplayIdentity RequireStonecuttingOutcome(
            bool throwOnFailure) => RequireExact(
                DungeonSpaceExpansionCatalog.StonecuttingResearchId,
                stonecuttingRevision,
                51,
                71,
                throwOnFailure,
                "stonecutting");

        private GameplayOutcomeReplayIdentity RequireExact(
            string researchProjectId,
            long revision,
            int beforeColumns,
            int afterColumns,
            bool throwOnFailure,
            string label)
        {
            if (revision <= 0L)
            {
                if (throwOnFailure)
                    throw new InvalidOperationException(
                        "The " + label + " expansion revision is missing.");
                return default;
            }
            GameplayResultKey key = DungeonSpaceExpansionOutcomeIds.ResultKey(
                researchProjectId,
                revision);
            if (ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact.outcomeTypeId
                    == DungeonSpaceExpansionOutcomeIds.Expanded.Value
                && exact.ownerRevision == revision
                && exact.facts.Any(value => value != null
                    && value.factId
                        == DungeonSpaceExpansionOutcomeIds
                            .ResearchProjectIdFact.Value
                    && value.value == researchProjectId)
                && HasMetric(
                    exact,
                    DungeonSpaceExpansionOutcomeIds
                        .PreviousInteriorColumnsMetric.Value,
                    beforeColumns)
                && HasMetric(
                    exact,
                    DungeonSpaceExpansionOutcomeIds
                        .CurrentInteriorColumnsMetric.Value,
                    afterColumns))
            {
                return identity;
            }
            if (throwOnFailure)
                throw new InvalidOperationException(
                    "The " + label
                    + " dungeon-space exact outcome is missing.");
            return default;
        }

        private static bool HasMetric(
            GameplayOutcomeSnapshot exact,
            string metricId,
            double expected) => exact.metrics.Any(value => value != null
                && string.Equals(
                    value.metricId,
                    metricId,
                    StringComparison.Ordinal)
                && Math.Abs(value.value - expected) < 0.0001d);

        private static Grid CreateTierZeroGrid()
        {
            Grid grid = new(
                InitialGridWidth,
                DungeonSpaceExpansionCatalog.SupportedGridHeight);
            foreach (GridCell cell in grid.GetCells())
            {
                grid.SetAreaType(
                    cell.Position,
                    GridCellAreaType.ExteriorPath);
                grid.SetTerrainType(
                    cell.Position,
                    GridCellTerrainType.Dry);
            }
            int end = EntranceX
                + DungeonSpaceExpansionCatalog.InitialInteriorColumns;
            for (int x = EntranceX; x < end; x++)
            for (int y = 0; y < grid.height; y++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, y),
                    x == EntranceX && y == EntranceY
                        ? GridCellAreaType.Entrance
                        : GridCellAreaType.DungeonInterior);
            }
            return grid;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Context));
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private sealed class TestGridAuthority :
        IGridSystemProvider,
        IGridSystemPublisher
    {
        internal TestGridAuthority(Grid grid) => Restore(grid);

        public GridSystemManager Manager => null;
        public Grid Grid { get; private set; }

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
            failureReason = string.Empty;
            return true;
        }

        public void CompleteGridPublication()
        {
        }

        internal void Restore(Grid grid) => Grid = grid
            ?? throw new ArgumentNullException(nameof(grid));
    }

    private static T Deserialize<T>(JObject value, string label)
        where T : class
    {
        try
        {
            T result = JsonUtility.FromJson<T>(
                (value ?? throw new ArgumentNullException(nameof(value)))
                .ToString(Formatting.None));
            return result ?? throw new InvalidOperationException(
                "Dungeon-space " + label + " deserialized to null.");
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or JsonException
                                           or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Could not restore dungeon-space " + label + ".",
                exception);
        }
    }

    private static JObject ParseJson(string json, string label)
    {
        try
        {
            return JObject.Parse(json ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Could not serialize dungeon-space " + label + ".",
                exception);
        }
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual="
                + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
