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

internal static class InfrastructureCommandCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:infrastructure-power-connection";
    internal const string DisconnectActionId =
        "action:infrastructure-power-disconnect";
    internal const string ReconnectActionId =
        "action:infrastructure-power-reconnect";

    private const string OwnerKind =
        "owner-kind:infrastructure-power-connection";
    private const string StateSchema =
        "dungeonstory-causal-infrastructure-power-owner-state@1";

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
                "An infrastructure checkpoint requires an owner ID.",
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
                DisconnectActionId,
                "infrastructure",
                InfrastructureCommandOutcomeIds.Applied,
                context.CaptureState,
                context.ExecuteDisconnect,
                context.RequireDisconnectOutcome,
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
                ReconnectActionId,
                "infrastructure",
                InfrastructureCommandOutcomeIds.Applied,
                () => new Dictionary<string, object>(),
                context.ExecuteReconnect,
                context.RequireReconnectOutcome,
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
            "infrastructure-power:causal-focused:primary";
        JObject disconnectedCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-infrastructure-power:disconnect",
                   PersistentOwnerId)))
        {
            IProductionCausalActionFixture disconnect = fixtures.Get(
                DisconnectActionId);
            IReadOnlyDictionary<string, object> before =
                disconnect.CaptureState();
            Require((int)before["infrastructure.stage"] == 0
                    && (bool)before["infrastructure.connectionEnabled"]
                    && (long)before[
                        "infrastructure.commandNextOwnerRevision"] == 1L,
                "Infrastructure power owner did not start connected.");
            Require(disconnect.Execute(),
                "The real power disconnect command did not execute.");
            Require(disconnect.RequireExactOutcome().HasCanonicalPayloadHash,
                "The disconnect command has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after =
                disconnect.CaptureState();
            Require((int)after["infrastructure.stage"] == 1
                    && !(bool)after[
                        "infrastructure.connectionEnabled"]
                    && (long)after[
                        "infrastructure.commandNextOwnerRevision"] == 2L
                    && (int)after[
                        "infrastructure.exactOutcomeCount"] == 1,
                "The disconnect command did not publish its exact owner state.");
            disconnectedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)disconnect)
                .CaptureOwnerCheckpoint();
        }

        JObject reconnectedCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-infrastructure-power:reconnect",
                   PersistentOwnerId,
                   disconnectedCheckpoint)))
        {
            IProductionCausalActionFixture disconnect = fixtures.Get(
                DisconnectActionId);
            IProductionCausalActionFixture reconnect = fixtures.Get(
                ReconnectActionId);
            IReadOnlyDictionary<string, object> restored =
                disconnect.CaptureState();
            Require((int)restored["infrastructure.stage"] == 1
                    && !(bool)restored[
                        "infrastructure.connectionEnabled"]
                    && !disconnect.Execute(),
                "Fresh composition restore lost or replayed the disconnect.");
            Require(reconnect.Execute(),
                "The real power reconnect command did not execute.");
            Require(reconnect.RequireExactOutcome().HasCanonicalPayloadHash,
                "The reconnect command has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after =
                disconnect.CaptureState();
            Require((int)after["infrastructure.stage"] == 2
                    && (bool)after["infrastructure.connectionEnabled"]
                    && (long)after[
                        "infrastructure.commandNextOwnerRevision"] == 3L
                    && (int)after[
                        "infrastructure.exactOutcomeCount"] == 2,
                "The reconnect command did not extend the same owner.");
            reconnectedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)reconnect)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-infrastructure-power:terminal",
                   PersistentOwnerId,
                   reconnectedCheckpoint)))
        {
            IProductionCausalActionFixture disconnect = fixtures.Get(
                DisconnectActionId);
            IProductionCausalActionFixture reconnect = fixtures.Get(
                ReconnectActionId);
            IReadOnlyDictionary<string, object> restored =
                disconnect.CaptureState();
            Require((int)restored["infrastructure.stage"] == 2
                    && (bool)restored[
                        "infrastructure.connectionEnabled"]
                    && !disconnect.Execute()
                    && !reconnect.Execute(),
                "Terminal infrastructure owner was not idempotent.");
        }

        JObject tamperedFacility =
            (JObject)disconnectedCheckpoint.DeepClone();
        ((JObject)tamperedFacility["domainState"])["facilityPersistentId"] =
            "building:tampered-infrastructure";
        RequireRestoreRejected(
            tamperedFacility,
            PersistentOwnerId,
            "A tampered infrastructure facility identity was accepted.");

        JObject tamperedRevision =
            (JObject)disconnectedCheckpoint.DeepClone();
        ((JObject)((JObject)tamperedRevision["domainState"])[
            "commandOutcomeState"])["nextOwnerRevision"] = 7L;
        RequireRestoreRejected(
            tamperedRevision,
            PersistentOwnerId,
            "A tampered infrastructure command-owner revision was accepted.");

        if (log)
            Debug.Log("Infrastructure power connection causal owner family PASS");
        return true;
    }

    public static void RunOwnerFamilyBatch()
    {
        try
        {
            Require(RunOwnerFamily(log: true),
                "Infrastructure power owner family returned false.");
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
                "run:causal-infrastructure-power:tampered",
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
        private readonly string facilityPersistentId;
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly BuildableObject facility;
        private readonly ElectricalNetworkRuntime power;
        private readonly InfrastructureObservationRuntime commandOutcomes;
        private readonly DomainFactDispatcher transitionFacts;
        private readonly IDisposable transitionBinding;
        private readonly GameplayOutcomeLedger ledger;
        private int stage;
        private long disconnectRevision;
        private long reconnectRevision;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            facilityPersistentId =
                "building:causal-infrastructure-power:" + scope;

            try
            {
                Grid grid = CreateGrid();
                facility = CreateFacility(grid);
                string ledgerRunId = HasPersistentOwner
                    ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                        this.ownerId,
                        OwnerKind)
                    : causalRunId + ":infrastructure-ledger";
                GameplayOutcomeRegistry registry = new(
                    new IGameplayOutcomeDescriptor[]
                    {
                        new InfrastructureCommandOutcomeDescriptor(
                            new KoreanJosaFormatter()),
                        new InfrastructureAutonomousOutcomeDescriptor(
                            new KoreanJosaFormatter())
                    },
                    new IGameplayOutcomeAdapterRegistration[]
                    {
                        new InfrastructureCommandOutcomeAdapter(),
                        new InfrastructureAutonomousOutcomeAdapter()
                    });
                FixedClock clock = new(29, 9);
                ledger = new GameplayOutcomeLedger(
                    registry,
                    GameplayOutcomeBufferLimits.Default,
                    new GameplayOutcomeRunId(ledgerRunId),
                    1L,
                    clock);
                GameplayOutcomeRecorder recorder = new(
                    ledger,
                    registry,
                    new GameEventBus());
                transitionFacts = new DomainFactDispatcher();
                transitionBinding = new IndustrialTransitionFactObserverBinding(
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger))
                    .Connect(transitionFacts);
                commandOutcomes = new InfrastructureObservationRuntime(
                    new DungeonRuntimeAggregateRootStore(),
                    transitionFacts,
                    transitionFacts,
                    clock);

                IBuildingWorldQuery world = new FixedBuildingWorld(facility);
                power = new ElectricalNetworkRuntime(
                    new IndustrialInfrastructureTopologyRuntime(world),
                    new FixedGridProvider(grid),
                    new RestoreWorldCandidateIndex(),
                    clock,
                    new EditorWarehouseStockRuntime(),
                    new NoFuelGateway(),
                    new AutomationPowerDemandRegistry(
                        new DungeonRuntimeAggregateRootStore()),
                    new DungeonRuntimeAggregateRootStore(),
                    new EmptyBufferLifecycle(),
                    new EmptyBufferClaims(),
                    new EmptyBufferRelease(),
                    NeutralSeasonalEventQuery.Instance,
                    NeutralMilestoneGameplayModifierQuery.Instance,
                    commandOutcomes);

                Require(power.TryGetNode(facility, out PowerNodeSnapshot initial)
                        && initial.HasControllableConnection
                        && initial.ConnectionEnabled,
                    "The causal infrastructure fixture has no connected power node.");
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
            PowerNodeSnapshot node = RequireNode();
            DungeonPowerInfrastructureSaveData powerState = power.Capture();
            DungeonInfrastructureObservationSaveData commandState =
                commandOutcomes.Capture();
            return new Dictionary<string, object>
            {
                ["infrastructure.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "infrastructure-power:ephemeral",
                ["infrastructure.facilityId"] = facilityPersistentId,
                ["infrastructure.displayName"] =
                    facility.BuildingData.objectName,
                ["infrastructure.connectionEnabled"] =
                    node.ConnectionEnabled,
                ["infrastructure.powerNodeCount"] = powerState.nodes.Count,
                ["infrastructure.commandNextOwnerRevision"] =
                    commandState.nextOwnerRevision,
                ["infrastructure.pendingOutcomeCount"] =
                    commandState.pending.Count,
                ["infrastructure.exactOutcomeCount"] = ledger
                    .CaptureGameplayOutcomes().exactOutcomes.Count,
                ["infrastructure.stage"] = stage,
                ["infrastructure.disconnected"] = stage >= 1,
                ["infrastructure.reconnected"] = stage >= 2
            };
        }

        internal bool ExecuteDisconnect()
        {
            ThrowIfDisposed();
            if (stage != 0 || !RequireNode().ConnectionEnabled)
                return false;
            long revision = commandOutcomes.Capture().nextOwnerRevision;
            InfrastructureCommandResult result = power.SetConnectionEnabled(
                facility,
                false);
            if (!result.Succeeded)
                return false;
            disconnectRevision = revision;
            stage = 1;
            ValidateState();
            return RequireDisconnectOutcome(false).HasCanonicalPayloadHash;
        }

        internal bool ExecuteReconnect()
        {
            ThrowIfDisposed();
            if (stage != 1 || RequireNode().ConnectionEnabled)
                return false;
            long revision = commandOutcomes.Capture().nextOwnerRevision;
            InfrastructureCommandResult result = power.SetConnectionEnabled(
                facility,
                true);
            if (!result.Succeeded)
                return false;
            reconnectRevision = revision;
            stage = 2;
            ValidateState();
            return RequireReconnectOutcome(false).HasCanonicalPayloadHash;
        }

        internal GameplayOutcomeReplayIdentity RequireDisconnectOutcome() =>
            RequireDisconnectOutcome(true);

        internal GameplayOutcomeReplayIdentity RequireReconnectOutcome() =>
            RequireReconnectOutcome(true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "This infrastructure context has no persistent owner.");
            }
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
            transitionBinding?.Dispose();
            transitionFacts?.Dispose();
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
        }

        private void RestoreDomainState(JObject domain)
        {
            RequireFields(
                domain,
                "infrastructure power domain state",
                "schemaVersion",
                "schema",
                "stage",
                "ownerId",
                "facilityPersistentId",
                "facilityDefinitionId",
                "facilityDisplayName",
                "powerState",
                "commandOutcomeState",
                "disconnectRevision",
                "reconnectRevision");
            if ((int)domain["schemaVersion"] != 1
                || !string.Equals(
                    (string)domain["schema"],
                    StateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["ownerId"],
                    ownerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["facilityPersistentId"],
                    facilityPersistentId,
                    StringComparison.Ordinal)
                || (int)domain["facilityDefinitionId"]
                    != facility.BuildingData.id
                || !string.Equals(
                    (string)domain["facilityDisplayName"],
                    facility.BuildingData.objectName,
                    StringComparison.Ordinal)
                || domain["powerState"] is not JObject powerJson
                || domain["commandOutcomeState"] is not JObject commandJson)
            {
                throw new InvalidOperationException(
                    "Infrastructure power checkpoint identity is invalid.");
            }

            int restoredStage = (int)domain["stage"];
            if (restoredStage is < 0 or > 2)
                throw new InvalidOperationException(
                    "Infrastructure power checkpoint stage is invalid.");
            DungeonPowerInfrastructureSaveData powerState =
                Deserialize<DungeonPowerInfrastructureSaveData>(
                    powerJson,
                    "power state");
            DungeonInfrastructureObservationSaveData commandState =
                Deserialize<DungeonInfrastructureObservationSaveData>(
                    commandJson,
                    "command outcome state");
            commandOutcomes.Restore(
                commandOutcomes.PrepareRestore(commandState));
            power.Restore(power.PrepareRestore(powerState));
            stage = restoredStage;
            disconnectRevision = (long)domain["disconnectRevision"];
            reconnectRevision = (long)domain["reconnectRevision"];
            ValidateState();
            if (!JToken.DeepEquals(domain, CaptureDomainState()))
            {
                throw new InvalidOperationException(
                    "Infrastructure power state changed during fresh restore.");
            }
        }

        private JObject CaptureDomainState() => new()
        {
            ["schemaVersion"] = 1,
            ["schema"] = StateSchema,
            ["stage"] = stage,
            ["ownerId"] = ownerId,
            ["facilityPersistentId"] = facilityPersistentId,
            ["facilityDefinitionId"] = facility.BuildingData.id,
            ["facilityDisplayName"] = facility.BuildingData.objectName,
            ["powerState"] = ParseJson(
                JsonUtility.ToJson(power.Capture()),
                "power state"),
            ["commandOutcomeState"] = ParseJson(
                JsonUtility.ToJson(commandOutcomes.Capture()),
                "command outcome state"),
            ["disconnectRevision"] = disconnectRevision,
            ["reconnectRevision"] = reconnectRevision
        };

        private void ValidateState()
        {
            ThrowIfDisposed();
            if (facility == null
                || !facility.PersistentInstanceId.IsValid
                || !string.Equals(
                    facility.PersistentInstanceId.Value,
                    facilityPersistentId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Infrastructure facility identity changed.");
            }
            PowerNodeSnapshot node = RequireNode();
            bool expectedConnection = stage != 1;
            DungeonPowerInfrastructureSaveData powerState = power.Capture();
            DungeonInfrastructureObservationSaveData commandState =
                commandOutcomes.Capture();
            PowerNodeSaveData savedNode = powerState.nodes.SingleOrDefault(
                value => string.Equals(
                    value.buildingInstanceId,
                    facilityPersistentId,
                    StringComparison.Ordinal));
            int exactCount = ledger.CaptureGameplayOutcomes()
                .exactOutcomes.Count;
            if (savedNode == null
                || powerState.nodes.Count != 1
                || node.ConnectionEnabled != expectedConnection
                || savedNode.connectionState != (expectedConnection ? 1 : 2)
                || commandState.nextOwnerRevision != stage + 1L
                || commandState.pending.Count != 0
                || exactCount != stage)
            {
                throw new InvalidOperationException(
                    "Infrastructure power state does not match its causal stage.");
            }
            if (stage == 0
                && (disconnectRevision != 0L || reconnectRevision != 0L))
            {
                throw new InvalidOperationException(
                    "Initial infrastructure state contains terminal revisions.");
            }
            if (stage >= 1)
                RequireDisconnectOutcome(false);
            if (stage >= 2)
                RequireReconnectOutcome(false);
            if (stage < 2 && reconnectRevision != 0L)
            {
                throw new InvalidOperationException(
                    "Reconnect revision appeared before the reconnect stage.");
            }
        }

        private PowerNodeSnapshot RequireNode()
        {
            if (!power.TryGetNode(facility, out PowerNodeSnapshot node)
                || node == null
                || !node.HasControllableConnection)
            {
                throw new InvalidOperationException(
                    "Infrastructure power node is unavailable.");
            }
            return node;
        }

        private GameplayOutcomeReplayIdentity RequireDisconnectOutcome(
            bool throwOnFailure) => RequireExact(
                disconnectRevision,
                "true",
                "false",
                throwOnFailure,
                "disconnect");

        private GameplayOutcomeReplayIdentity RequireReconnectOutcome(
            bool throwOnFailure) => RequireExact(
                reconnectRevision,
                "false",
                "true",
                throwOnFailure,
                "reconnect");

        private GameplayOutcomeReplayIdentity RequireExact(
            long revision,
            string before,
            string after,
            bool throwOnFailure,
            string label)
        {
            if (revision <= 0L)
            {
                if (throwOnFailure)
                {
                    throw new InvalidOperationException(
                        "The " + label
                        + " infrastructure revision is missing.");
                }
                return default;
            }
            GameplayResultKey key = InfrastructureCommandOutcomeIds.ResultKey(
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
                    == InfrastructureCommandOutcomeIds.Applied.Value
                && exact.ownerRevision == revision
                && exact.facts.Any(value =>
                    value.factId
                        == InfrastructureCommandOutcomeIds.CommandKindFact.Value
                    && value.value == "power-connection-changed")
                && exact.facts.Any(value =>
                    value.factId
                        == InfrastructureCommandOutcomeIds.TargetIdFact.Value
                    && value.value == facilityPersistentId)
                && exact.facts.Any(value =>
                    value.factId
                        == InfrastructureCommandOutcomeIds.BeforeValueFact.Value
                    && value.value == before)
                && exact.facts.Any(value =>
                    value.factId
                        == InfrastructureCommandOutcomeIds.AfterValueFact.Value
                    && value.value == after))
            {
                return identity;
            }
            if (throwOnFailure)
            {
                throw new InvalidOperationException(
                    "The " + label
                    + " infrastructure exact outcome is missing.");
            }
            return default;
        }

        private Grid CreateGrid()
        {
            Grid grid = new(8, 3);
            for (int y = 0; y < grid.height; y++)
            for (int x = 0; x < grid.width; x++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, y),
                    GridCellAreaType.ExteriorPath);
            }
            return grid;
        }

        private BuildableObject CreateFacility(Grid grid)
        {
            BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
            cleanup.Add(definition);
            definition.id = 97601;
            definition.name = "CausalInfrastructurePowerFacility";
            definition.objectName = "전력 제어 작업장";
            definition.width = 1;
            definition.height = 1;
            definition.unlocked = true;
            definition.layer = GridLayer.Building;
            definition.category = BuildingCategory.Resource;
            definition.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
            definition.ReplaceAbilities(new BuildingAbilityCollection());
            definition.AbilityModules.Add(new BuildingUtilityConnectionAbility
            {
                channels = UtilityChannel.Power,
                maxThroughput = 8f,
                normallyOpen = true
            });
            definition.AbilityModules.Add(new BuildingPowerConsumerAbility
            {
                demandPerSecond = 1f,
                priority = PowerPriority.Production,
                minimumSupplyFraction = 1f
            });

            GameObject host = new("CausalInfrastructurePowerFacilityHost");
            cleanup.Add(host);
            BuildableObject building = host.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.RestorePersistentIdentity(
                (BuildingInstanceId)facilityPersistentId);
            building.SetGrid(grid);
            building.Initialization(definition, new Vector2Int(3, 1));
            Require(grid.RegisterOccupant(
                    building,
                    definition.Placement.Layer,
                    building.buildPoses,
                    definition.Placement.IsMovement),
                "The causal infrastructure facility could not be registered on the live grid.");
            return building;
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
            IReadOnlyList<IProductionCausalActionFixture> fixtures)
        {
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));
        }

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

    private sealed class FixedBuildingWorld : IBuildingWorldQuery
    {
        private readonly BuildableObject[] buildings;

        internal FixedBuildingWorld(params BuildableObject[] buildings) =>
            this.buildings = (buildings ?? Array.Empty<BuildableObject>())
                .Where(value => value != null)
                .ToArray();

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;
    }

    private sealed class FixedGridProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        internal FixedGridProvider(Grid grid) =>
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));

        public GridSystemManager Manager => throw new InvalidOperationException(
            "The causal infrastructure fixture has no scene grid manager.");
        public Grid Grid => grid;
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }
        public bool TryGetGrid(out Grid resolved)
        {
            resolved = grid;
            return true;
        }
    }

    private sealed class FixedClock : IGameClock, IGameCalendar
    {
        private int day;
        private int hour;

        internal FixedClock(int day, int hour)
        {
            this.day = day;
            this.hour = hour;
        }

        public float DeltaTime => 1f / 60f;
        public float Time => AbsoluteHour * 60f;
        public int FrameCount => 1;
        public bool IsPaused => false;
        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds =>
            hour / 24f * GameCalendarRules.SecondsPerDay;
        public TimeOfDay TimeOfDay => TimeOfDay.Morning;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, 23);
        }
    }

    private sealed class EmptyBufferLifecycle :
        IFacilityBufferDestinationLifecycleCommand
    {
        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            failureReason = string.Empty;
            return desiredClaims != null
                && desiredProfiles != null
                && desiredClaims.Count == 0
                && desiredProfiles.Count == 0;
        }
    }

    private sealed class EmptyBufferClaims :
        IFacilityBufferDestinationClaimQuery
    {
        public long Revision => 0L;
        public bool TryGetClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim claim)
        {
            claim = null;
            return false;
        }
        public IReadOnlyList<FacilityBufferDestinationClaim> CaptureClaims() =>
            Array.Empty<FacilityBufferDestinationClaim>();
    }

    private sealed class EmptyBufferRelease :
        IFacilityBufferDestinationReleaseService
    {
        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            releasedQuantity = 0;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class NoFuelGateway : IPhysicalFacilityItemSinkGateway
    {
        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "The causal power fixture has no fueled producer.";
            return false;
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }

        public bool Acknowledge(string commitId, out string failureReason)
        {
            failureReason = "The causal power fixture has no fueled producer.";
            return false;
        }
    }

    private static T Deserialize<T>(JObject value, string label)
    {
        try
        {
            T result = JsonUtility.FromJson<T>(
                (value ?? throw new ArgumentNullException(nameof(value)))
                .ToString(Formatting.None));
            return result ?? throw new InvalidOperationException(
                "Infrastructure " + label + " deserialized to null.");
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or JsonException
                                           or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Could not restore infrastructure " + label + ".",
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
                "Could not serialize infrastructure " + label + ".",
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
