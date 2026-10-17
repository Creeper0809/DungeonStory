#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildingDemolitionG05ActualWorldDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Narrative/Run G05 Actual World Demolition")]
    public static void RunOrThrow()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A new create-only G05 demolition output directory is required.");
        }

        Directory.CreateDirectory(output);
        try
        {
            JObject report = new()
            {
                ["passed"] = true,
                ["test"] = "building-demolition-g05-actual-world",
                ["scope"] = "actual ProductionFacilityDestructiveDrainWorldRemovalPort and recovery runtime joined to the actual BuildingDemolitionDestructiveDrainParticipant/gameplay ledger; controlled upper journal/coordinator, fuel retirement faults, and exact registered fixture building",
                ["deferredAndConflict"] = VerifyDeferredAndConflict(),
                ["actualWorldRemoval"] = VerifyActualWorldRemoval(),
                ["savedRestore"] = VerifySavedRestore(),
                ["postWorldRemovalRetry"] = VerifyPostWorldRemovalRetry(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            };
            WriteReport(output, report);
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["passed"] = false,
                ["test"] = "building-demolition-g05-actual-world",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    private static JObject VerifyDeferredAndConflict()
    {
        using WorldFixture deferredWorld = new(
            "deferred",
            allowFuelRetirement: false,
            duplicateWorldIdentity: false);
        OutcomeGraph deferredGraph = OutcomeGraph.Create(
            "run:g05-deferred");
        ProductionFacilityDestructiveDrainEntrySaveData deferredEntry =
            PrepareAwaitingWorldRemoval(
                deferredGraph,
                deferredWorld,
                absoluteDay: 11);
        CaseJournal deferredJournal = new(deferredEntry);
        CaseCoordinator deferredCoordinator = new(deferredJournal);
        ProductionFacilityDestructiveDrainRecoveryRuntime deferredRuntime =
            CreateRuntime(
                deferredWorld,
                deferredGraph,
                deferredJournal,
                deferredCoordinator);
        ProductionFacilityDestructiveRemovalResult deferred =
            deferredRuntime.RequestAndDrive(
                deferredWorld.Building,
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition);
        Require(
            deferred.Status ==
                ProductionFacilityDestructiveRemovalStatus.DeferredAccepted
            && deferredWorld.IsWorldPresent
            && deferredGraph.Count(deferredWorld.FacilityId) == 0
            && deferredJournal.Entry.phase ==
                ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval
            && deferredCoordinator.WorldRecordCount == 0,
            "Deferred world removal changed the world or committed a result.");

        using WorldFixture conflictWorld = new(
            "conflict",
            allowFuelRetirement: true,
            duplicateWorldIdentity: true);
        OutcomeGraph conflictGraph = OutcomeGraph.Create(
            "run:g05-conflict");
        ProductionFacilityDestructiveDrainEntrySaveData conflictEntry =
            PrepareAwaitingWorldRemoval(
                conflictGraph,
                conflictWorld,
                absoluteDay: 12);
        CaseJournal conflictJournal = new(conflictEntry);
        CaseCoordinator conflictCoordinator = new(conflictJournal);
        ProductionFacilityDestructiveDrainRecoveryRuntime conflictRuntime =
            CreateRuntime(
                conflictWorld,
                conflictGraph,
                conflictJournal,
                conflictCoordinator);
        ProductionFacilityDestructiveRemovalResult conflict =
            conflictRuntime.RequestAndDrive(
                conflictWorld.Building,
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition);
        Require(
            conflict.Status ==
                ProductionFacilityDestructiveRemovalStatus.Conflict
            && conflictWorld.IsWorldPresent
            && conflictGraph.Count(conflictWorld.FacilityId) == 0
            && conflictJournal.Entry.phase ==
                ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval
            && conflictCoordinator.WorldRecordCount == 0,
            "World identity conflict changed the world or committed a result.");
        RequireThrows<InvalidOperationException>(
            conflictRuntime.ValidateBeforeCapture,
            "Capture guard accepted an unresolved G05 world conflict.");

        return new JObject
        {
            ["deferredStatus"] = deferred.Status.ToString(),
            ["deferredWorldPresent"] = deferredWorld.IsWorldPresent,
            ["deferredOutcomeCount"] = deferredGraph.Count(
                deferredWorld.FacilityId),
            ["conflictStatus"] = conflict.Status.ToString(),
            ["conflictWorldPresent"] = conflictWorld.IsWorldPresent,
            ["conflictOutcomeCount"] = conflictGraph.Count(
                conflictWorld.FacilityId),
            ["captureGuardRejectedConflict"] = true
        };
    }

    private static JObject VerifyActualWorldRemoval()
    {
        using WorldFixture world = new(
            "actual",
            allowFuelRetirement: true,
            duplicateWorldIdentity: false);
        OutcomeGraph graph = OutcomeGraph.Create("run:g05-actual");
        ProductionFacilityDestructiveDrainEntrySaveData entry =
            PrepareAwaitingWorldRemoval(graph, world, absoluteDay: 13);
        CaseJournal journal = new(entry);
        CaseCoordinator coordinator = new(journal);
        ProductionFacilityDestructiveDrainRecoveryRuntime runtime =
            CreateRuntime(world, graph, journal, coordinator);

        int before = graph.Count(world.FacilityId);
        ProductionFacilityDestructiveRemovalResult result =
            runtime.RequestAndDrive(
                world.Building,
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition);
        int after = graph.Count(world.FacilityId);
        GameplayOutcomeSnapshot exact = graph.Exact(world.FacilityId);
        Require(
            before == 0
            && result.Status == ProductionFacilityDestructiveRemovalStatus
                .RemovedAwaitingCheckpointGc
            && !world.IsWorldPresent
            && after == 1
            && journal.Entry.phase == ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc
            && coordinator.WorldRecordCount == 1
            && exact.operationId == entry.operationId
            && exact.ownerRevision == 1L
            && exact.absoluteDay == 13,
            "Actual world removal did not precede exactly one demolition result.");
        runtime.Tick();
        Require(
            graph.Count(world.FacilityId) == 1
            && coordinator.WorldRecordCount == 1,
            "Terminal G05 replay duplicated world removal or its result.");

        return new JObject
        {
            ["status"] = result.Status.ToString(),
            ["outcomeCountBeforeRemoval"] = before,
            ["outcomeCountAfterRemoval"] = after,
            ["worldPresentAfter"] = world.IsWorldPresent,
            ["terminalPhase"] = journal.Entry.phase.ToString(),
            ["operationId"] = exact.operationId,
            ["ownerRevision"] = exact.ownerRevision,
            ["absoluteDay"] = exact.absoluteDay,
            ["replayOutcomeCount"] = graph.Count(world.FacilityId)
        };
    }

    private static JObject VerifySavedRestore()
    {
        using WorldFixture world = new(
            "restore",
            allowFuelRetirement: true,
            duplicateWorldIdentity: false);
        OutcomeGraph original = OutcomeGraph.Create("run:g05-restore-source");
        GameplayOutcomeLedgerSaveData ledgerSave =
            original.Ledger.CaptureGameplayOutcomes();
        ProductionFacilityDestructiveDrainEntrySaveData originalEntry =
            PrepareAwaitingWorldRemoval(original, world, absoluteDay: 14);
        string entryJson = JsonUtility.ToJson(originalEntry);
        RequireThrows<InvalidOperationException>(
            () => original.Ledger.CaptureGameplayOutcomes(),
            "G05 save capture ignored an active demolition outcome reservation.");

        OutcomeGraph restored = OutcomeGraph.Restore(
            "run:g05-restore-fresh",
            ledgerSave);
        ProductionFacilityDestructiveDrainEntrySaveData restoredEntry =
            JsonUtility.FromJson<
                ProductionFacilityDestructiveDrainEntrySaveData>(entryJson);
        CaseJournal journal = new(restoredEntry);
        CaseCoordinator coordinator = new(journal);
        ProductionFacilityDestructiveDrainRecoveryRuntime runtime =
            CreateRuntime(world, restored, journal, coordinator);
        runtime.OnRestoreCompleted();
        Require(
            world.IsWorldPresent
            && restored.Count(world.FacilityId) == 0
            && coordinator.WorldRecordCount == 0,
            "Restore hook mutated the G05 world before the first tick.");

        runtime.Tick();
        Require(
            !world.IsWorldPresent
            && restored.Count(world.FacilityId) == 1
            && coordinator.WorldRecordCount == 1
            && journal.Entry.phase == ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc,
            "Fresh G05 runtime did not resume saved world removal exactly once.");
        runtime.Tick();
        Require(
            restored.Count(world.FacilityId) == 1
            && coordinator.WorldRecordCount == 1,
            "Fresh G05 terminal replay duplicated removal or outcome.");

        return new JObject
        {
            ["savedPhase"] = originalEntry.phase.ToString(),
            ["entryJsonRoundTrip"] = string.Equals(
                JsonUtility.ToJson(restoredEntry),
                entryJson,
                StringComparison.Ordinal),
            ["activeReservationCaptureRejected"] = true,
            ["baselineLedgerRoundTrip"] = true,
            ["beforeFirstTickWorldPresent"] = true,
            ["outcomeCountAfterResume"] = restored.Count(world.FacilityId),
            ["worldRemovalRecordCount"] = coordinator.WorldRecordCount,
            ["terminalPhase"] = journal.Entry.phase.ToString(),
            ["replayUnchanged"] = true
        };
    }

    private static JObject VerifyPostWorldRemovalRetry()
    {
        using WorldFixture world = new(
            "retry",
            allowFuelRetirement: true,
            duplicateWorldIdentity: false);
        OutcomeGraph graph = OutcomeGraph.Create(
            "run:g05-retry",
            rejectFirstCommit: true);
        ProductionFacilityDestructiveDrainEntrySaveData entry =
            PrepareAwaitingWorldRemoval(graph, world, absoluteDay: 15);
        CaseJournal journal = new(entry);
        CaseCoordinator coordinator = new(journal);
        ProductionFacilityDestructiveDrainRecoveryRuntime runtime =
            CreateRuntime(world, graph, journal, coordinator);

        ProductionFacilityDestructiveRemovalResult first =
            runtime.RequestAndDrive(
                world.Building,
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition);
        Require(
            first.Status ==
                ProductionFacilityDestructiveRemovalStatus.DeferredAccepted
            && !world.IsWorldPresent
            && graph.Count(world.FacilityId) == 0
            && journal.Entry.phase ==
                ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval
            && coordinator.WorldRecordCount == 0
            && graph.Rejector.CommitAttemptCount == 1
            && graph.Rejector.SuccessfulCommitCount == 0,
            "Post-world result rejection did not retain an explicit retry boundary.");

        runtime.Tick();
        Require(
            !world.IsWorldPresent
            && world.Port.AlreadyAppliedCount == 1
            && graph.Count(world.FacilityId) == 1
            && journal.Entry.phase == ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc
            && coordinator.WorldRecordCount == 1
            && graph.Rejector.CommitAttemptCount == 2
            && graph.Rejector.SuccessfulCommitCount == 1,
            "Post-world result retry did not converge exactly once.");
        runtime.Tick();
        Require(
            graph.Count(world.FacilityId) == 1
            && graph.Rejector.CommitAttemptCount == 2,
            "Terminal post-world retry replay duplicated the result.");

        return new JObject
        {
            ["firstStatus"] = first.Status.ToString(),
            ["worldRemovedBeforeOutcomeCommit"] = true,
            ["outcomeCountAfterRejectedCommit"] = 0,
            ["alreadyAppliedWorldCountOnRetry"] =
                world.Port.AlreadyAppliedCount,
            ["commitAttemptCount"] = graph.Rejector.CommitAttemptCount,
            ["successfulCommitCount"] = graph.Rejector.SuccessfulCommitCount,
            ["outcomeCountAfterRetry"] = graph.Count(world.FacilityId),
            ["terminalReplayUnchanged"] = true
        };
    }

    private static ProductionFacilityDestructiveDrainRecoveryRuntime
        CreateRuntime(
            WorldFixture world,
            OutcomeGraph graph,
            CaseJournal journal,
            CaseCoordinator coordinator) => new(
                journal,
                coordinator,
                new RejectUnexpectedAuthorityRevoke(),
                world.Port,
                world.World,
                new EditorFixedGameCalendar(day: 10, hour: 0),
                graph.Demolition);

    private static ProductionFacilityDestructiveDrainEntrySaveData
        PrepareAwaitingWorldRemoval(
            OutcomeGraph graph,
            WorldFixture world,
            int absoluteDay)
    {
        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                world.FacilityId);
        ProductionFacilityDestructiveDrainOutcomeSnapshot snapshot = new(
            BuildingDefinitionIdentity.Resolve(world.Definition),
            world.Definition.objectName,
            world.Position.x,
            world.Position.y,
            absoluteDay,
            ownerRevision: 1L);
        string lifecycle = ProductionFacilityDestructiveDrainCanonical
            .ComputeFingerprint("g05-world-removal:" + world.FacilityId.Value);
        ProductionFacilityDestructiveDrainPrepareContext prepare = new(
            operationId,
            ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
            world.FacilityId,
            ProductionOutputDestinationId.FromFacility(world.FacilityId),
            lifecycle,
            snapshot);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            graph.Demolition.Prepare(prepare);
        Require(
            plan.Owners.Count == 1,
            "G05 demolition participant did not prepare one owner.");
        ProductionFacilityDestructiveDrainOwnerSaveData owner = new()
        {
            ownerStableId = plan.Owners[0].OwnerStableId,
            disposition = plan.Owners[0].Disposition,
            targetDestinationId = plan.Owners[0].TargetDestinationId,
            stepOperationId = ProductionFacilityDestructiveDrainCanonical
                .BuildStepOperationId(
                    operationId,
                    plan.ParticipantId,
                    plan.Owners[0].OwnerStableId),
            phase = ProductionFacilityDestructiveDrainStepPhase.Planned,
            requestFingerprint = plan.Owners[0].RequestFingerprint,
            commitId = string.Empty,
            receiptFingerprint = string.Empty
        };
        ProductionFacilityDestructiveDrainStepContext planned = new(
            operationId,
            world.FacilityId,
            plan.ParticipantId,
            owner,
            plan.DurableContributionFingerprint,
            snapshot);
        Require(
            graph.Demolition.TryPrepareDurable(planned, out string failure),
            "G05 demolition owner prepare failed: " + failure);
        ProductionFacilityDestructiveDrainStepResult committed =
            graph.Demolition.TryCommit(planned);
        Require(
            committed.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied,
            "G05 demolition owner pre-removal commit failed.");
        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        owner.commitId = committed.CommitId;
        owner.receiptFingerprint = committed.ReceiptFingerprint;
        ProductionFacilityDestructiveDrainStepContext awaiting = new(
            operationId,
            world.FacilityId,
            plan.ParticipantId,
            owner,
            plan.DurableContributionFingerprint,
            snapshot);
        Require(
            graph.Demolition.TryAcknowledge(awaiting).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied,
            "G05 demolition owner pre-removal acknowledge failed.");
        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .OwnerAcknowledged;
        Require(
            graph.Count(world.FacilityId) == 0,
            "G05 demolition result committed before world removal.");

        return new ProductionFacilityDestructiveDrainEntrySaveData
        {
            operationId = operationId.Value,
            initiatingMutationOperationId =
                ProductionFacilityDestructiveDrainCanonical
                    .BuildInitiatingMutationOperationId(
                        ProductionFacilityDestructiveDrainCause
                            .ExplicitDemolition,
                        world.FacilityId),
            cause = ProductionFacilityDestructiveDrainCause
                .ExplicitDemolition,
            facilityId = world.FacilityId.Value,
            destinationId = ProductionOutputDestinationId
                .FromFacility(world.FacilityId).Value,
            phase = ProductionFacilityDestructiveDrainPhase
                .AwaitingWorldRemoval,
            preparedLifecycleFingerprint = lifecycle,
            expectedCurrentLifecycleFingerprint = lifecycle,
            revision = 7L,
            outcomeSnapshotVersion = snapshot.Version,
            outcomeBuildingDefinitionId = snapshot.BuildingDefinitionId,
            outcomeBuildingDisplayName = snapshot.BuildingDisplayName,
            outcomePositionX = snapshot.PositionX,
            outcomePositionY = snapshot.PositionY,
            outcomeAbsoluteDay = snapshot.AbsoluteDay,
            outcomeOwnerRevision = snapshot.OwnerRevision,
            participants = new List<
                ProductionFacilityDestructiveDrainParticipantSaveData>
            {
                new()
                {
                    participantId = plan.ParticipantId,
                    contractVersion = plan.ContractVersion,
                    preparedContributionFingerprint =
                        plan.DurableContributionFingerprint,
                    expectedCurrentContributionFingerprint =
                        plan.DurableContributionFingerprint,
                    planFingerprint = plan.PlanFingerprint,
                    owners = new List<
                        ProductionFacilityDestructiveDrainOwnerSaveData>
                    {
                        owner
                    }
                }
            }
        };
    }

    private static void WriteReport(string output, JObject report)
    {
        string path = Path.Combine(output, "report.json");
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(report.ToString(Formatting.Indented));
        writer.WriteLine();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class OutcomeGraph
    {
        private OutcomeGraph(
            GameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            bool rejectFirstCommit)
        {
            Registry = registry;
            Ledger = ledger;
            GameplayOutcomeRecorder recorder = new(
                ledger,
                registry,
                new GameEventBus());
            IBuildingDemolitionOutcomeCommitter committer =
                new BuildingDemolitionGameplayOutcomeBridge(recorder, ledger);
            if (rejectFirstCommit)
            {
                Rejector = new RejectOnceCommitter(committer)
                {
                    RejectNextCommit = true
                };
                committer = Rejector;
            }
            Demolition = new BuildingDemolitionDestructiveDrainParticipant(
                committer);
        }

        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal BuildingDemolitionDestructiveDrainParticipant Demolition
        {
            get;
        }
        internal RejectOnceCommitter Rejector { get; }

        internal static OutcomeGraph Create(
            string runId,
            bool rejectFirstCommit = false)
        {
            GameplayOutcomeRegistry registry = CreateRegistry();
            GameplayOutcomeLedger ledger = new(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(runId),
                1L);
            return new OutcomeGraph(registry, ledger, rejectFirstCommit);
        }

        internal static OutcomeGraph Restore(
            string runId,
            GameplayOutcomeLedgerSaveData save)
        {
            GameplayOutcomeRegistry registry = CreateRegistry();
            GameplayOutcomeLedger ledger = new(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(runId),
                1L);
            ledger.BeginRestoreCandidate();
            GameplayOutcomeLedgerRestoreCandidate candidate =
                ledger.PrepareGameplayOutcomeRestore(save);
            ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate();
            ledger.CompleteRestoreCandidate();
            return new OutcomeGraph(registry, ledger, rejectFirstCommit: false);
        }

        internal int Count(BuildingInstanceId facilityId) =>
            Ledger.GetForEntity(
                new GameplayEntityId(
                    BuildingDemolitionOutcomeIds.FacilityKind,
                    facilityId.Value),
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count;

        internal GameplayOutcomeSnapshot Exact(BuildingInstanceId facilityId) =>
            Ledger.GetForEntity(
                new GameplayEntityId(
                    BuildingDemolitionOutcomeIds.FacilityKind,
                    facilityId.Value),
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Single().Exact;

        private static GameplayOutcomeRegistry CreateRegistry() => new(
            new IGameplayOutcomeDescriptor[]
            {
                new BuildingDemolitionOutcomeDescriptor(
                    new DungeonStory.Narrative.Korean.KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new BuildingDemolitionOutcomeAdapter()
            });
    }

    private sealed class WorldFixture : IDisposable
    {
        private readonly GameObject textureRoot;
        private readonly GameObject buildingRoot;

        internal WorldFixture(
            string suffix,
            bool allowFuelRetirement,
            bool duplicateWorldIdentity)
        {
            FacilityId = (BuildingInstanceId)("building:qa:g05:" + suffix);
            Position = new Vector2Int(1, 0);
            Definition = ScriptableObject.CreateInstance<BuildingSO>();
            Definition.id = suffix switch
            {
                "deferred" => 1_995_000,
                "conflict" => 1_995_001,
                "actual" => 1_995_002,
                "restore" => 1_995_003,
                "retry" => 1_995_004,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(suffix),
                    suffix,
                    "Unknown G05 fixture identity.")
            };
            Definition.objectName = "G05 실제 철거 " + suffix;
            Definition.width = 1;
            Definition.height = 1;
            Definition.layer = GridLayer.Building;
            Definition.category = BuildingCategory.Shop;
            Definition.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
            Definition.unlocked = true;
            Definition.ReplaceAbilities(new BuildingAbilityCollection());
            Definition.Facility = new FacilityData();

            Grid = new Grid(4, 1);
            buildingRoot = new GameObject("G05ActualWorld_" + suffix);
            Building = buildingRoot.AddComponent<BuildableObject>();
            Building.ConstructDebugRules(AllowDamageRule.Instance);
            typeof(BuildableObject).GetField(
                    "facilityCandidateCache",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(Building, NoopFacilityStateChange.Instance);
            Building.RestorePersistentIdentity(FacilityId);
            Building.SetGrid(Grid);
            typeof(BuildableObject).GetProperty(
                    nameof(BuildableObject.BuildingData),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(Building, Definition);
            typeof(BuildableObject).GetProperty(
                    nameof(BuildableObject.id),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(Building, Definition.id);
            typeof(BuildableObject).GetProperty(
                    nameof(BuildableObject.centerPos),
                    BindingFlags.Instance | BindingFlags.Public
                        | BindingFlags.NonPublic)
                ?.SetValue(Building, Position);
            MutableBuildPositions(Building).Add(Position);
            Require(
                Grid.RegisterOccupant(
                    Building,
                    GridLayer.Building,
                    new[] { Position },
                    connectPositions: false),
                "Could not register the G05 fixture building.");

            textureRoot = new GameObject("G05ActualWorldTexture_" + suffix);
            GridTexture texture = textureRoot.AddComponent<GridTexture>();
            World = new TrackingBuildingWorld(
                Building,
                duplicateWorldIdentity);
            Port = new TrackingWorldRemovalPort(
                new ProductionFacilityDestructiveDrainWorldRemovalPort(
                    World,
                    new FixedGridTextureProvider(texture),
                    new ControlledFuelRetirement(allowFuelRetirement)));
        }

        internal BuildingInstanceId FacilityId { get; }
        internal Vector2Int Position { get; }
        internal BuildingSO Definition { get; }
        internal Grid Grid { get; }
        internal BuildableObject Building { get; }
        internal TrackingBuildingWorld World { get; }
        internal TrackingWorldRemovalPort Port { get; }
        internal bool IsWorldPresent =>
            World.Buildings.Count > 0
            && ReferenceEquals(
                Grid.GetGridCell(Position).GetOccupant(GridLayer.Building),
                Building);

        public void Dispose()
        {
            if (buildingRoot != null)
                UnityEngine.Object.DestroyImmediate(buildingRoot);
            if (Definition != null)
                UnityEngine.Object.DestroyImmediate(Definition);
            if (textureRoot != null)
                UnityEngine.Object.DestroyImmediate(textureRoot);
        }

        private static List<Vector2Int> MutableBuildPositions(
            BuildableObject building) =>
            (List<Vector2Int>)typeof(BuildableObject)
                .GetField(
                    "mutableBuildPoses",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(building)
            ?? throw new InvalidOperationException(
                "BuildableObject position authority was not found.");
    }

    private sealed class CaseJournal :
        IProductionFacilityDestructiveDrainJournalQuery
    {
        internal CaseJournal(
            ProductionFacilityDestructiveDrainEntrySaveData entry) =>
            Entry = entry?.Clone()
                ?? throw new ArgumentNullException(nameof(entry));

        internal ProductionFacilityDestructiveDrainEntrySaveData Entry
        {
            get;
            private set;
        }
        public int Version { get; private set; } = 1;

        public IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
            CaptureOpen() => Entry == null
            ? Array.Empty<ProductionFacilityDestructiveDrainEntrySaveData>()
            : new[] { Entry.Clone() };

        public bool TryGet(
            ProductionFacilityDestructiveDrainOperationId operationId,
            out ProductionFacilityDestructiveDrainEntrySaveData entry)
        {
            entry = null;
            if (Entry == null
                || !string.Equals(
                    Entry.operationId,
                    operationId.Value,
                    StringComparison.Ordinal))
            {
                return false;
            }
            entry = Entry.Clone();
            return true;
        }

        internal void Replace(
            ProductionFacilityDestructiveDrainEntrySaveData entry)
        {
            Entry = entry.Clone();
            Version++;
        }
    }

    private sealed class CaseCoordinator :
        IProductionFacilityDestructiveDrainCoordinator
    {
        private readonly CaseJournal journal;

        internal CaseCoordinator(CaseJournal journal) =>
            this.journal = journal
                ?? throw new ArgumentNullException(nameof(journal));

        internal int WorldRecordCount { get; private set; }

        public ProductionFacilityDestructiveDrainDriveResult
            DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause cause,
                BuildingInstanceId facilityId,
                ProductionFacilityDestructiveDrainOutcomeSnapshot
                    outcomeSnapshot = default) =>
            throw new InvalidOperationException(
                "G05 AwaitingWorldRemoval replay reached participant driving.");

        public ProductionFacilityDestructiveDrainDriveResult
            RecordAuthorityRevoked(
                ProductionFacilityDestructiveDrainOperationId operationId) =>
            throw new InvalidOperationException(
                "G05 AwaitingWorldRemoval replay reached authority revoke.");

        public ProductionFacilityDestructiveDrainDriveResult RecordWorldRemoved(
            ProductionFacilityDestructiveDrainOperationId operationId)
        {
            WorldRecordCount++;
            ProductionFacilityDestructiveDrainEntrySaveData current =
                journal.Entry.Clone();
            current.phase = ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc;
            current.revision++;
            journal.Replace(current);
            return new ProductionFacilityDestructiveDrainDriveResult(
                ProductionFacilityDestructiveDrainDriveStatus
                    .WorldRemovedAwaitingCheckpointGc,
                operationId,
                current.phase,
                current.revision,
                string.Empty);
        }

        public bool TryCollectCheckpointed(
            ProductionFacilityDestructiveDrainOperationId operationId,
            long expectedRevision,
            out string failureReason)
        {
            failureReason = "g05-checkpoint-collection-not-in-scope";
            return false;
        }
    }

    private sealed class RejectUnexpectedAuthorityRevoke :
        IProductionFacilityDestructiveDrainAuthorityRevoker
    {
        public ProductionFacilityDestructiveDrainAuthorityConvergenceResult
            TryConverge(
                BuildableObject facility,
                ProductionFacilityDestructiveDrainCause cause,
                ProductionFacilityDestructiveDrainOperationId operationId,
                long expectedRevision) =>
            throw new InvalidOperationException(
                "G05 AwaitingWorldRemoval replay reached authority convergence.");
    }

    private sealed class TrackingBuildingWorld : IBuildingWorldQuery
    {
        private readonly BuildableObject building;
        private readonly bool duplicate;

        internal TrackingBuildingWorld(
            BuildableObject building,
            bool duplicate)
        {
            this.building = building;
            this.duplicate = duplicate;
        }

        public int BuildingVersion => building == null || building.isDestroy
            ? 1
            : 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            building == null || building.isDestroy
                ? Array.Empty<BuildableObject>()
                : duplicate
                    ? new[] { building, building }
                    : new[] { building };
    }

    private sealed class TrackingWorldRemovalPort :
        IProductionFacilityDestructiveDrainWorldRemovalPort
    {
        private readonly IProductionFacilityDestructiveDrainWorldRemovalPort
            inner;

        internal TrackingWorldRemovalPort(
            IProductionFacilityDestructiveDrainWorldRemovalPort inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal int CallCount { get; private set; }
        internal int AlreadyAppliedCount { get; private set; }

        public ProductionFacilityWorldRemovalResult TryEnsureRemoved(
            BuildingInstanceId facilityId)
        {
            CallCount++;
            ProductionFacilityWorldRemovalResult result =
                inner.TryEnsureRemoved(facilityId);
            if (result.Disposition ==
                ProductionFacilityWorldRemovalDisposition.AlreadyApplied)
            {
                AlreadyAppliedCount++;
            }
            return result;
        }
    }

    private sealed class FixedGridTextureProvider : IGridTextureProvider
    {
        internal FixedGridTextureProvider(GridTexture texture) =>
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        public GridTexture Texture { get; }
    }

    private sealed class ControlledFuelRetirement :
        ISurvivalFacilityFuelRetirement
    {
        private readonly bool allow;
        internal ControlledFuelRetirement(bool allow) => this.allow = allow;

        public bool TryPrepareFacilityFuelRetirement(
            BuildableObject building,
            out string failureReason)
        {
            failureReason = allow
                ? string.Empty
                : "g05-injected-fuel-retirement-deferred";
            return allow;
        }
    }

    private sealed class RejectOnceCommitter :
        IBuildingDemolitionOutcomeCommitter
    {
        private readonly IBuildingDemolitionOutcomeCommitter inner;

        internal RejectOnceCommitter(
            IBuildingDemolitionOutcomeCommitter inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool RejectNextCommit { get; set; }
        internal int CommitAttemptCount { get; private set; }
        internal int SuccessfulCommitCount { get; private set; }

        public bool TryPrepare(
            in BuildingDemolitionOutcomeReceipt receipt,
            out PreparedBuildingDemolitionOutcome prepared,
            out string failureReason) =>
            inner.TryPrepare(receipt, out prepared, out failureReason);

        public OwnerOutcomeCommitResult Commit(
            in PreparedBuildingDemolitionOutcome prepared)
        {
            CommitAttemptCount++;
            if (RejectNextCommit)
            {
                RejectNextCommit = false;
                return new OwnerOutcomeCommitResult(
                    OwnerOutcomeCommitPhase.Rejected,
                    prepared.ResultKey,
                    default,
                    string.Empty,
                    "g05-injected-post-world-ledger-rejection");
            }
            OwnerOutcomeCommitResult result = inner.Commit(prepared);
            if (result.DurablyCommitted)
                SuccessfulCommitCount++;
            return result;
        }

        public OwnerOutcomeCommitResult Reconcile(
            GameplayResultKey resultKey) => inner.Reconcile(resultKey);

        public void Cancel(in PreparedBuildingDemolitionOutcome prepared) =>
            inner.Cancel(prepared);
    }

    private sealed class AllowDamageRule : IBuildingDamageRulePort
    {
        internal static readonly AllowDamageRule Instance = new();
        public bool ShouldBlockFacilityDamage(bool damaged) => false;
    }

    private sealed class NoopFacilityStateChange :
        IBuildingFacilityStateChangePort
    {
        internal static readonly NoopFacilityStateChange Instance = new();
        public void MarkDynamicStateDirty()
        {
        }
    }
}
#endif
