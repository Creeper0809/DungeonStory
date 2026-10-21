#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class OffenseArrivalCausalProductionEntryDebugScenarios
{
    internal const string OwnerKind =
        "owner-kind:offense-expedition-arrival";
    internal const string RestoreKind =
        "owner-family:offense-expedition-arrival";
    internal const string FinalizeRewardActionId =
        "action:finalize-special-wildlife-expedition-result";
    internal const string SettleArrivalActionId =
        "action:settle-restored-special-wildlife-arrival";

    private const string Schema =
        "dungeonstory-causal-offense-expedition-arrival-owner-state@2";
    private const string TargetId = "rival_dungeon";
    private const string ExpeditionId =
        "expedition:causal-special-wildlife-arrival";
    private const string ArrivalId = "return:1";
    private const int Day = 37;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
            throw new ArgumentException("An arrival checkpoint requires an owner ID.", nameof(ownerCheckpoint));

        var context = new Context(
            causalRunId,
            persistent ? ownerId : "offense-arrival:" + causalRunId,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                FinalizeRewardActionId,
                "offense",
                MigratedProducerOutcomeKind.OffenseExpeditionResult,
                context.CaptureState,
                context.FinalizeReward,
                context.RequireResultOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                persistent ? context.OwnerId : null,
                persistent ? OwnerKind : null,
                persistent ? context.Checkpoint : null,
                persistent ? RestoreKind : null,
                persistent ? context : null),
            new DelegateProductionCausalActionFixture(
                SettleArrivalActionId,
                "offense",
                MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt,
                () => new Dictionary<string, object>(),
                context.SettleArrival,
                context.RequireArrivalOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                persistent ? context.OwnerId : null,
                persistent ? OwnerKind : null,
                persistent ? context.Checkpoint : null,
                persistent ? RestoreKind : null,
                persistent ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "owner:causal-offense-arrival:focused";
        JObject first;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-offense-arrival:first", ownerId)))
        {
            IProductionCausalActionFixture finalize = fixtures.Get(
                FinalizeRewardActionId);
            Require((int)finalize.CaptureState()["offenseArrival.stage"] == 0,
                "Offense-arrival owner did not start empty.");
            Require(finalize.Execute(),
                "The authored special-wildlife reward did not finalize.");
            Require(finalize.RequireExactOutcome().HasCanonicalPayloadHash,
                "The expedition-result outcome has no canonical hash.");
            IReadOnlyDictionary<string, object> after = finalize.CaptureState();
            Require(
                (int)after["offenseArrival.stage"] == 1
                && (string)after["offenseArrival.arrivalStage"]
                    == OffenseReturnArrivalStage.WaitingForParty.ToString()
                && (string)after["offenseArrival.resultReceiptResolution"]
                    == OffenseExpeditionArrivalResolution.Pending.ToString()
                && (int)after["offenseArrival.liveWildlifeCount"] == 0,
                "Reward finalization did not retain the pending delivery boundary.");
            first = ((IProductionCausalOwnerCheckpointFixture)finalize)
                .CaptureOwnerCheckpoint();
        }

        JObject terminal;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-offense-arrival:settle", ownerId, first)))
        {
            IProductionCausalActionFixture finalize = fixtures.Get(
                FinalizeRewardActionId);
            IProductionCausalActionFixture settle = fixtures.Get(
                SettleArrivalActionId);
            Require(!finalize.Execute(),
                "Fresh restore replayed expedition-result finalization.");
            Require(settle.Execute(),
                "Restored special wildlife did not materialize and settle.");
            Require(settle.RequireExactOutcome().HasCanonicalPayloadHash,
                "The arrival outcome has no canonical hash.");
            IReadOnlyDictionary<string, object> after = finalize.CaptureState();
            Require(
                (int)after["offenseArrival.stage"] == 2
                && (string)after["offenseArrival.arrivalStage"]
                    == OffenseReturnArrivalStage.Escaped.ToString()
                && (string)after["offenseArrival.resultReceiptResolution"]
                    == OffenseExpeditionArrivalResolution.Escaped.ToString()
                && (int)after["offenseArrival.materializedCount"] == 1
                && (int)after["offenseArrival.escapedCount"] == 1
                && (int)after["offenseArrival.liveWildlifeCount"] == 0
                && (int)after["offenseArrival.exactOutcomeCount"] == 2,
                "Arrival materialization, escape, result history and ledger diverged.");
            terminal = ((IProductionCausalOwnerCheckpointFixture)settle)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-offense-arrival:terminal", ownerId, terminal)))
        {
            IProductionCausalActionFixture finalize = fixtures.Get(
                FinalizeRewardActionId);
            IProductionCausalActionFixture settle = fixtures.Get(
                SettleArrivalActionId);
            JObject before = ((IProductionCausalOwnerCheckpointFixture)finalize)
                .CaptureOwnerCheckpoint();
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(
                terminal,
                before);
            Require(!finalize.Execute() && !settle.Execute(),
                "Terminal offense-arrival owner was not idempotent.");
            JObject after = ((IProductionCausalOwnerCheckpointFixture)settle)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected offense-arrival replay mutated its owner checkpoint.");
        }

        foreach (MigratedProducerOutcomeFaultMode fault in new[]
                 {
                     MigratedProducerOutcomeFaultMode.RejectReservation,
                     MigratedProducerOutcomeFaultMode.RejectCommit
                 })
        {
            using var context = new Context(
                "run:causal-offense-arrival:fault:" + fault,
                ownerId,
                first);
            Require(context.ExerciseArrivalFault(fault),
                "Arrival settlement did not preserve terminal physical state and retry after "
                + fault + ".");
        }

        JObject tampered = (JObject)first.DeepClone();
        ((JObject)tampered["domainState"])["targetId"] = "truth_core";
        Require(Reject(ownerId, tampered, "target-tamper"),
            "A changed authored arrival target was accepted.");

        if (log)
            Debug.Log("Authored special-wildlife reward -> save/restore -> physical arrival escape settlement causal owner PASS");
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Offense-arrival causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static bool Reject(string ownerId, JObject checkpoint, string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-offense-arrival:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string causalRunId;
        private readonly GameObject root;
        private readonly MutableClock clock = new();
        private readonly FixedCalendar calendar = new(Day);
        private readonly GameEventBus events = new();
        private readonly DungeonRuntimeAggregateRootStore aggregateRoots = new();
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly EditorOffenseExpeditionResultFactFixture resultFacts;
        private readonly EditorOffenseArrivalSettlementFactFixture arrivalFacts;
        private readonly Grid grid;
        private readonly MutableDropZone dropZone = new();
        private readonly ControlledWildlifeRuntime wildlife;
        private readonly OffenseReturnArrivalRuntime arrivals;
        private readonly ResourceOffenseCampaignCatalog authoredCatalog;
        private readonly FixedCampaignCatalog campaignCatalog;
        private readonly OffenseCampaignRuntime campaign;
        private readonly OffenseWorldMapRuntime worldMap;
        private readonly EditorOffenseTruthFactFixture truthFacts;
        private readonly OffenseRewardRuntime rewards;
        private readonly MetaProgressionRuntime meta;
        private readonly MetaProgressionSaveSection metaSave;
        private readonly OffenseExpeditionRuntime expeditions;
        private readonly OffenseExpeditionResultFinalizer finalizer;
        private readonly OffenseSaveService offenseSave;
        private readonly OffenseTargetDefinition target;
        private string materializedWildlifeId = string.Empty;
        private string materializedSpeciesId = string.Empty;
        private int materializedMaxHealth;
        private int materializedArrivalHealth;
        private Vector2Int materializedPosition;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.causalRunId = causalRunId;
            OwnerId = CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ProductionCausalOwnerCheckpoint.LedgerRunId(OwnerId, OwnerKind),
                aggregateRoots);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            resultFacts = new EditorOffenseExpeditionResultFactFixture(
                events,
                outcomes.Transaction,
                outcomes.Recorder,
                outcomes.Ledger,
                probe);
            arrivalFacts = new EditorOffenseArrivalSettlementFactFixture(
                events,
                outcomes.Transaction,
                outcomes.Recorder,
                outcomes.Ledger,
                probe);

            grid = new Grid(8, 8);
            for (int y = 0; y < grid.height; y++)
            for (int x = 0; x < grid.width; x++)
                grid.SetAreaType(new Vector2Int(x, y), GridCellAreaType.ExteriorPath);

            var content = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            authoredCatalog = new ResourceOffenseCampaignCatalog(content);
            campaignCatalog = new FixedCampaignCatalog(authoredCatalog.Targets);
            Require(authoredCatalog.TryGet(TargetId, out target),
                "The authored rival_dungeon target is missing.");
            OffenseRewardPreview[] special = target.rewards
                .Where(value => value?.GrantSpec is OffenseSpecialMonsterRewardSpec)
                .ToArray();
            Require(special.Length == 1 && special[0].amount == 1,
                "The authored rival_dungeon special-monster reward changed.");
            target.rewards = special;

            wildlife = new ControlledWildlifeRuntime(
                grid,
                clock,
                content,
                CharacterAiEditorTestDependencies.WorldRegistry);
            arrivals = new OffenseReturnArrivalRuntime(
                new OffenseReturnArrivalWorldServices(
                    new FixedGridProvider(grid),
                    dropZone,
                    TabooButcheryCausalTestPorts.Guard<ICharacterSpawnerProvider>(),
                    TabooButcheryCausalTestPorts.Guard<ICharacterSpawnObjectFactory>(),
                    TabooButcheryCausalTestPorts.Guard<IInvasionIntruderDataProvider>(),
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    TabooButcheryCausalTestPorts.Guard<IBuildingWorldQuery>(
                        (method, _) => method.Name == "get_Buildings"
                            ? Array.Empty<BuildableObject>()
                            : throw Unsupported(method.Name)),
                    aggregateRoots),
                new OffenseReturnArrivalDomainServices(
                    TabooButcheryCausalTestPorts.Guard<ICharacterBodyHealthQuery>(),
                    TabooButcheryCausalTestPorts.Guard<ICharacterBodyHealthCommand>(),
                    TabooButcheryCausalTestPorts.Guard<ICaptivityRuntime>(),
                    TabooButcheryCausalTestPorts.Guard<ICaptivityCommandService>(),
                    wildlife,
                    TabooButcheryCausalTestPorts.Guard<IWildlifeCaptureRuntime>(
                        (method, _) => method.Name == "IsCaptured"
                            ? false
                            : throw Unsupported(method.Name)),
                    TabooButcheryCausalTestPorts.Guard<IEnemyArchetypeCatalog>(),
                    TabooButcheryCausalTestPorts.Guard<IEnemyIndividualFactory>(),
                    TabooButcheryCausalTestPorts.Guard<ICombatEquipmentRuntime>(),
                    clock,
                    calendar,
                    events,
                    arrivalFacts.Settlements));

            root = new GameObject("Causal offense special-wildlife arrival owner");
            campaign = new OffenseCampaignRuntime();
            worldMap = root.AddComponent<OffenseWorldMapRuntime>();
            truthFacts = new EditorOffenseTruthFactFixture(
                events,
                causalRunId + ":truth");
            worldMap.Construct(
                new DungeonStory.Foundation.UnityUiClock(),
                events,
                null,
                campaign,
                campaign,
                campaignCatalog,
                truthFacts.Facts,
                truthFacts.Facts,
                calendar);
            RestoreInitialCampaign();

            rewards = root.AddComponent<OffenseRewardRuntime>();
            rewards.Construct(
                new ArrivalRewardContextBuilder(arrivals),
                new OffenseRewardGrantService(
                    TabooButcheryCausalTestPorts.Guard<IOffenseRewardSelector>(),
                    new IOffenseRewardGrantHandler[]
                    {
                        new OffenseSpecialMonsterRewardGrantHandler()
                    }));

            meta = root.AddComponent<MetaProgressionRuntime>();
            meta.Construct(
                new MetaRunResultBuilder(),
                EmptyMetaRuntimeApplicationPort.Instance,
                clock,
                EmptyMetaCatalog.Instance,
                aggregateRoots);
            metaSave = new MetaProgressionSaveSection(meta);

            expeditions = root.AddComponent<OffenseExpeditionRuntime>();
            var offenseReferences = new OffenseSceneRuntimeReferences(
                worldMap,
                rewards,
                expeditions,
                null,
                null);
            finalizer = new OffenseExpeditionResultFinalizer(
                offenseReferences,
                new ProgressionSceneRuntimeReferences(null, null, meta),
                worldMap,
                gameClock: clock,
                arrivals: arrivals);
            finalizer.ConstructFactPublication(
                calendar,
                resultFacts.Publications);
            expeditions.Construct(
                EmptyExpeditionMemberQuery.Instance,
                offenseReferences,
                EmptyOffensePanelService.Instance,
                events,
                finalizer);
            offenseSave = new OffenseSaveService(
                offenseReferences,
                campaignCatalog,
                TabooButcheryCausalTestPorts.Guard<ICharacterWorldSaveService>(),
                TabooButcheryCausalTestPorts.Guard<IOffenseBattleRuntime>(
                    (method, _) => method.Name == "CapturePersistentState"
                        ? null
                        : throw Unsupported(method.Name)));

            try
            {
                if (checkpoint != null)
                    Restore(checkpoint);
                probe.ClearObservations();
                Validate();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal string OwnerId { get; }

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            Validate();
            OffenseReturnArrivalState arrival = arrivals.Arrivals.SingleOrDefault();
            OffenseExpeditionArrivalReceipt receipt = expeditions.ResultHistory
                .SingleOrDefault()?.arrivalReceipts.SingleOrDefault();
            return new Dictionary<string, object>
            {
                ["offenseArrival.ownerId"] = OwnerId,
                ["offenseArrival.stage"] = Stage,
                ["offenseArrival.day"] = Day,
                ["offenseArrival.expeditionId"] = ExpeditionId,
                ["offenseArrival.targetId"] = TargetId,
                ["offenseArrival.targetTitle"] = target.title,
                ["offenseArrival.authoredRewardType"] =
                    OffenseRewardTypeIds.SpecialMonster,
                ["offenseArrival.arrivalId"] = arrival?.arrivalId ?? "none",
                ["offenseArrival.arrivalKind"] =
                    arrival?.kind.ToString() ?? "None",
                ["offenseArrival.arrivalStage"] =
                    arrival?.stage.ToString() ?? "None",
                ["offenseArrival.requestedAmount"] =
                    arrival?.requestedAmount ?? 0,
                ["offenseArrival.returnSealed"] =
                    arrival?.returnSealed ?? false,
                ["offenseArrival.materializedCount"] =
                    arrival?.materializedIds.Count ?? 0,
                ["offenseArrival.escapedCount"] =
                    arrival?.escapedIds.Count ?? 0,
                ["offenseArrival.liveWildlifeCount"] = wildlife.Wildlife.Count,
                ["offenseArrival.materializedWildlifeId"] =
                    materializedWildlifeId.Length > 0
                        ? materializedWildlifeId
                        : "none",
                ["offenseArrival.materializedSpeciesId"] =
                    materializedSpeciesId.Length > 0
                        ? materializedSpeciesId
                        : "none",
                ["offenseArrival.materializedMaxHealth"] = materializedMaxHealth,
                ["offenseArrival.materializedArrivalHealth"] =
                    materializedArrivalHealth,
                ["offenseArrival.resultHistoryCount"] =
                    expeditions.ResultHistory.Count,
                ["offenseArrival.resultReceiptResolution"] =
                    receipt?.resolution.ToString() ?? "None",
                ["offenseArrival.resultReceiptMaterialized"] =
                    receipt?.materializedAmount ?? 0,
                ["offenseArrival.resultReceiptSecured"] =
                    receipt?.securedAmount ?? 0,
                ["offenseArrival.resultReceiptEscaped"] =
                    receipt?.escapedAmount ?? 0,
                ["offenseArrival.metaOffenseSuccessCount"] =
                    meta.RunProgress.OffenseSuccessCount,
                ["offenseArrival.campaignTargetCompleted"] =
                    campaign.State.IsTargetCompleted(TargetId),
                ["offenseArrival.resultOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.OffenseExpeditionResult),
                ["offenseArrival.arrivalOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt),
                ["offenseArrival.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool FinalizeReward()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            dropZone.Enabled = false;
            var expedition = new OffenseExpeditionRun(
                ExpeditionId,
                target,
                Array.Empty<CharacterActor>(),
                target.requiredPower + 15f);
            var result = new OffenseExpeditionResult(
                ExpeditionId,
                TargetId,
                target.title,
                true,
                target.requiredPower + 15f,
                target.requiredPower,
                target.danger,
                target.durationSeconds,
                Array.Empty<OffenseExpeditionMemberSnapshot>(),
                new[] { "경쟁 던전의 특수 동물 운송 상자를 확보했다." });
            List<OffenseExpeditionResult> history = new();
            OffenseExpeditionResult finalized = finalizer.Finalize(
                expedition,
                result,
                history,
                () => arrivals.SealExpeditionReturn(ExpeditionId));
            resultFacts.FlushNotifications();
            if (finalized == null || history.Count != 1)
                return false;
            expeditions.PublishRestoreCandidate(
                expeditions.BuildRestoreCandidate(
                    Array.Empty<OffenseExpeditionRun>(), history));
            bool valid = Stage == 1;
            if (valid)
                Validate();
            return valid;
        }

        internal bool SettleArrival()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            dropZone.Enabled = true;
            clock.Advance(3f);
            arrivals.Tick();
            OffenseReturnArrivalState arrival = arrivals.Arrivals.Single();
            WildlifeActor actor = wildlife.Wildlife.SingleOrDefault();
            if (arrival.stage != OffenseReturnArrivalStage.AwaitingContainment
                || actor == null
                || !string.Equals(actor.SpeciesId, "rune_deer", StringComparison.Ordinal)
                || actor.CurrentHealth >= actor.MaxHealth
                || arrival.materializedIds.Count != 1
                || !string.Equals(
                    arrival.materializedIds[0],
                    actor.WildlifeId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            materializedWildlifeId = actor.WildlifeId;
            materializedSpeciesId = actor.SpeciesId;
            materializedMaxHealth = actor.MaxHealth;
            materializedArrivalHealth = actor.CurrentHealth;
            materializedPosition = actor.GridPosition;

            clock.Advance(134f);
            arrivals.Tick();
            bool valid = Stage == 2 && wildlife.Wildlife.Count == 0;
            if (valid)
                Validate();
            return valid;
        }

        internal bool ExerciseArrivalFault(
            MigratedProducerOutcomeFaultMode fault)
        {
            ThrowIfDisposed();
            if (Stage != 1
                || fault is not (
                    MigratedProducerOutcomeFaultMode.RejectReservation
                    or MigratedProducerOutcomeFaultMode.RejectCommit))
            {
                return false;
            }

            dropZone.Enabled = true;
            clock.Advance(3f);
            arrivals.Tick();
            OffenseReturnArrivalState arrival = arrivals.Arrivals.Single();
            WildlifeActor actor = wildlife.Wildlife.SingleOrDefault();
            if (arrival.stage != OffenseReturnArrivalStage.AwaitingContainment
                || actor == null
                || actor.CurrentHealth >= actor.MaxHealth)
            {
                return false;
            }
            materializedWildlifeId = actor.WildlifeId;
            materializedSpeciesId = actor.SpeciesId;
            materializedMaxHealth = actor.MaxHealth;
            materializedArrivalHealth = actor.CurrentHealth;
            materializedPosition = actor.GridPosition;

            probe.TargetKind =
                MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt;
            probe.FaultMode = fault;
            clock.Advance(134f);
            arrivals.Tick();
            OffenseExpeditionArrivalReceipt pending = expeditions.ResultHistory
                .Single().arrivalReceipts.Single();
            bool rejectedStatePreserved =
                OutcomeCount(MigratedProducerOutcomeKind
                    .OffenseExpeditionArrivalReceipt) == 0
                && arrival.stage
                    == OffenseReturnArrivalStage.AwaitingContainment
                && arrival.materializedIds.Count == 1
                && arrival.escapedIds.Count == 1
                && wildlife.Wildlife.Count == 0
                && pending.resolution
                    == OffenseExpeditionArrivalResolution.Pending;

            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            clock.Advance(0.1f);
            arrivals.Tick();
            bool retried = Stage == 2
                && arrival.stage == OffenseReturnArrivalStage.Escaped
                && expeditions.ResultHistory.Single().arrivalReceipts.Single()
                    .resolution == OffenseExpeditionArrivalResolution.Escaped;
            if (rejectedStatePreserved && retried)
                Validate();
            return rejectedStatePreserved && retried;
        }

        internal GameplayOutcomeReplayIdentity RequireResultOutcome() =>
            RequireOutcome(MigratedProducerOutcomeKind.OffenseExpeditionResult);

        internal GameplayOutcomeReplayIdentity RequireArrivalOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject Checkpoint()
        {
            ThrowIfDisposed();
            Validate();
            CaptureSavedForward(
                out DungeonOffenseSaveData offense,
                out DungeonOffenseReturnArrivalSaveData savedArrivals);
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                OwnerId,
                OwnerKind,
                RestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 2,
                    ["schema"] = Schema,
                    ["targetId"] = TargetId,
                    ["expeditionId"] = ExpeditionId,
                    ["arrivalId"] = ArrivalId,
                    ["stage"] = Stage,
                    ["clockTime"] = clock.Time,
                    ["campaign"] = JObject.FromObject(campaign.Capture()),
                    ["offense"] = JObject.Parse(JsonUtility.ToJson(offense)),
                    ["arrivals"] = JObject.Parse(JsonUtility.ToJson(
                        savedArrivals)),
                    ["resultPublications"] = JObject.Parse(JsonUtility.ToJson(
                        resultFacts.Publications.Capture())),
                    ["wildlife"] = JObject.Parse(JsonUtility.ToJson(
                        wildlife.Capture())),
                    ["metaProgression"] = JObject.Parse(metaSave.Capture()),
                    ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(
                        outcomes.Transaction.Capture())),
                    ["materializationEvidence"] = new JObject
                    {
                        ["wildlifeId"] = materializedWildlifeId,
                        ["speciesId"] = materializedSpeciesId,
                        ["maxHealth"] = materializedMaxHealth,
                        ["arrivalHealth"] = materializedArrivalHealth,
                        ["gridX"] = materializedPosition.x,
                        ["gridY"] = materializedPosition.y
                    }
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            arrivalFacts.Dispose();
            resultFacts.Dispose();
            truthFacts.Dispose();
            wildlife.Dispose();
            if (root != null)
                Object.DestroyImmediate(root);
        }

        private int Stage
        {
            get
            {
                int resultOutcomes = OutcomeCount(
                    MigratedProducerOutcomeKind.OffenseExpeditionResult);
                int arrivalOutcomes = OutcomeCount(
                    MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt);
                if (resultOutcomes == 0 && arrivalOutcomes == 0)
                    return 0;
                if (resultOutcomes == 1 && arrivalOutcomes == 0)
                    return 1;
                if (resultOutcomes == 1 && arrivalOutcomes == 1)
                    return 2;
                return -1;
            }
        }

        private void RestoreInitialCampaign()
        {
            Dictionary<string, OffenseTargetDefinition> byId =
                campaignCatalog.Targets.ToDictionary(value => value.id,
                    StringComparer.Ordinal);
            List<string> prerequisiteIds = new();
            string cursor = target.prerequisiteTargetId;
            while (!string.IsNullOrWhiteSpace(cursor))
            {
                prerequisiteIds.Add(cursor);
                cursor = byId.TryGetValue(cursor, out OffenseTargetDefinition value)
                    ? value.prerequisiteTargetId
                    : throw new InvalidOperationException(
                        "The authored offense prerequisite chain is broken.");
            }
            var source = new DungeonOffenseCampaignSaveData
            {
                reconLevel = OffenseWorldMapService.MaxReconLevel,
                knownTargetIds = prerequisiteIds
                    .Append(TargetId)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                completedTargetIds = prerequisiteIds
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            };
            campaign.PublishRestoreCandidate(
                campaign.BuildRestoreCandidate(source));
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact == null
                || exact.absoluteDay != Day
                || exact.subjects == null
                || exact.subjects.Count != 1)
            {
                throw new InvalidOperationException(
                    "Offense-arrival production entry did not publish its exact outcome: "
                    + kind);
            }
            return identity;
        }

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(value.outcomeTypeId, type,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes().exactOutcomes.Count;

        private void Restore(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                OwnerId,
                OwnerKind,
                RestoreKind,
                outcomes.Ledger);
            RequireFields(state,
                "offense-arrival owner state",
                "schemaVersion", "schema", "targetId", "expeditionId",
                "arrivalId", "stage", "clockTime", "campaign", "offense",
                "arrivals", "resultPublications", "wildlife",
                "metaProgression", "outcomeOwners", "materializationEvidence");
            if ((int)state["schemaVersion"] != 2
                || !string.Equals((string)state["schema"], Schema,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["targetId"], TargetId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["expeditionId"], ExpeditionId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["arrivalId"], ArrivalId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Offense-arrival checkpoint identity changed.");
            }

            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                Read<DungeonMigratedProducerOutcomeSaveData>(
                    state["outcomeOwners"])));
            resultFacts.Publications.Restore(
                Read<OffenseExpeditionResultPublicationSaveData>(
                    state["resultPublications"]));
            clock.Restore((float)state["clockTime"]);
            campaign.PublishRestoreCandidate(campaign.BuildRestoreCandidate(
                state["campaign"].ToObject<DungeonOffenseCampaignSaveData>()));

            DungeonOffenseSaveData offense = Read<DungeonOffenseSaveData>(
                state["offense"]);
            DungeonOffenseReturnArrivalSaveData savedArrivals =
                Read<DungeonOffenseReturnArrivalSaveData>(state["arrivals"]);
            OffenseArrivalSettlementRuntime.ProjectTerminalReceipts(
                offense,
                savedArrivals);
            Require(offense.activeExpeditions.Count == 0,
                "Arrival owner cannot restore an active expedition.");
            Dictionary<StockCategory, int> stock = offense.rewards.stockGranted
                .ToDictionary(value => value.category, value => value.amount);
            rewards.PublishRestoreCandidate(OffenseRewardRuntime.BuildRestoreCandidate(
                offense.rewards.moneyEarned,
                stock,
                offense.rewards.rareFacilityBuildingIds,
                offense.rewards.acquiredBlueprintIds));
            expeditions.PublishRestoreCandidate(
                expeditions.BuildRestoreCandidate(
                    Array.Empty<OffenseExpeditionRun>(),
                    offense.resultHistory.Select(RestoreResult)));

            DungeonGameRestoreReport report = new();
            OffenseReturnArrivalRestoreCandidate arrivalCandidate =
                arrivals.BuildRestoreCandidate(
                    savedArrivals,
                    report);
            Require(report.Success && arrivalCandidate != null,
                "Arrival checkpoint failed canonical validation: "
                + string.Join(" | ", report.Errors));
            arrivals.PublishRestoreCandidate(arrivalCandidate);
            wildlife.Restore(Read<DungeonWildlifeSaveData>(state["wildlife"]));

            IDungeonSaveRestoreStage metaStage = metaSave.StageRestore(
                state["metaProgression"].ToString(Formatting.None),
                metaSave.SectionVersion,
                new DungeonGameRestoreReport());
            metaStage.Commit(new DungeonGameRestoreReport());

            JObject evidence = (JObject)state["materializationEvidence"];
            RequireFields(evidence, "arrival materialization evidence",
                "wildlifeId", "speciesId", "maxHealth", "arrivalHealth",
                "gridX", "gridY");
            materializedWildlifeId = (string)evidence["wildlifeId"] ?? string.Empty;
            materializedSpeciesId = (string)evidence["speciesId"] ?? string.Empty;
            materializedMaxHealth = (int)evidence["maxHealth"];
            materializedArrivalHealth = (int)evidence["arrivalHealth"];
            materializedPosition = new Vector2Int(
                (int)evidence["gridX"],
                (int)evidence["gridY"]);

            if ((int)state["stage"] != Stage)
                throw new InvalidOperationException(
                    "Offense-arrival checkpoint stage disagrees with restored state.");
        }

        private void Validate()
        {
            int stage = Stage;
            if (stage is < 0 or > 2)
                throw new InvalidOperationException("Offense-arrival ledger shape is invalid.");
            CaptureSavedForward(
                out DungeonOffenseSaveData savedOffense,
                out _);
            OffenseReturnArrivalState arrival = arrivals.Arrivals.SingleOrDefault();
            OffenseExpeditionResult result = savedOffense.resultHistory
                .Select(RestoreResult)
                .SingleOrDefault();
            OffenseExpeditionArrivalReceipt receipt =
                result?.arrivalReceipts.SingleOrDefault();
            OffenseExpeditionResultPublicationSaveData resultPublication =
                resultFacts.Publications.Capture();
            bool resultPublicationValid = stage == 0
                ? resultPublication.observations.Count == 0
                : resultPublication.observations.Count == 1
                  && !resultPublication.observations[0].pending
                  && !resultPublication.observations[0].notificationPending
                  && string.Equals(
                      resultPublication.observations[0].fact.result.expeditionId,
                      ExpeditionId,
                      StringComparison.Ordinal);

            bool valid = stage switch
            {
                0 => arrival == null
                    && result == null
                    && meta.RunProgress.OffenseSuccessCount == 0
                    && !campaign.State.IsTargetCompleted(TargetId)
                    && wildlife.Wildlife.Count == 0
                    && materializedWildlifeId.Length == 0,
                1 => arrival != null
                    && string.Equals(arrival.arrivalId, ArrivalId,
                        StringComparison.Ordinal)
                    && string.Equals(arrival.expeditionId, ExpeditionId,
                        StringComparison.Ordinal)
                    && string.Equals(arrival.targetId, TargetId,
                        StringComparison.Ordinal)
                    && arrival.kind == OffenseReturnArrivalKind.SpecialWildlife
                    && arrival.requestedAmount == 1
                    && arrival.returnSealed
                    && arrival.stage == OffenseReturnArrivalStage.WaitingForParty
                    && arrival.materializedIds.Count == 0
                    && arrival.escapedIds.Count == 0
                    && result != null
                    && result.grantedRewards.Count == 1
                    && result.grantedRewards[0].success
                    && result.grantedRewards[0].grantedAmount == 1
                    && receipt?.resolution
                        == OffenseExpeditionArrivalResolution.Pending
                    && receipt.materializedAmount == 0
                    && wildlife.Wildlife.Count == 0
                    && meta.RunProgress.OffenseSuccessCount == 1
                    && campaign.State.IsTargetCompleted(TargetId)
                    && materializedWildlifeId.Length == 0,
                2 => arrival != null
                    && arrival.stage == OffenseReturnArrivalStage.Escaped
                    && arrival.materializedIds.Count == 1
                    && arrival.escapedIds.Count == 1
                    && arrival.settledMaterializedAmount == 1
                    && arrival.settledSecuredAmount == 0
                    && arrival.settledEscapedAmount == 1
                    && result != null
                    && receipt?.resolution
                        == OffenseExpeditionArrivalResolution.Escaped
                    && receipt.materializedAmount == 1
                    && receipt.securedAmount == 0
                    && receipt.escapedAmount == 1
                    && wildlife.Wildlife.Count == 0
                    && materializedWildlifeId.Length > 0
                    && string.Equals(materializedSpeciesId, "rune_deer",
                        StringComparison.Ordinal)
                    && materializedMaxHealth > materializedArrivalHealth
                    && materializedArrivalHealth > 0
                    && grid.IsValidGridPos(materializedPosition)
                    && meta.RunProgress.OffenseSuccessCount == 1
                    && campaign.State.IsTargetCompleted(TargetId),
                _ => false
            };
            if (!valid || !resultPublicationValid || ExactOutcomeCount() != stage)
                throw new InvalidOperationException(
                    "Offense reward, arrival, wildlife, result history, campaign, meta and ledger state diverged.");
        }

        private void CaptureSavedForward(
            out DungeonOffenseSaveData offense,
            out DungeonOffenseReturnArrivalSaveData savedArrivals)
        {
            offense = offenseSave.Capture();
            savedArrivals = arrivals.Capture();
            OffenseArrivalSettlementRuntime.ProjectTerminalReceipts(
                offense,
                savedArrivals);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(causalRunId);
        }
    }

    private sealed class ControlledWildlifeRuntime : IWildlifeRuntime, IDisposable
    {
        private readonly Grid grid;
        private readonly IGameClock clock;
        private readonly ICharacterAiWorldRegistry registry;
        private readonly IWildlifeSpeciesCatalogProvider species;
        private readonly List<WildlifeActor> actors = new();
        private int nextSequence = 1;

        internal ControlledWildlifeRuntime(
            Grid grid,
            IGameClock clock,
            IGameContentCatalog content,
            ICharacterAiWorldRegistry registry)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            species = new ResourceWildlifeSpeciesCatalogProvider(
                content,
                new ResourceItemDefinitionCatalog(content));
            Require(species.TryGetSpecies("rune_deer", out _),
                "The authored rune_deer species is missing.");
        }

        public IReadOnlyList<WildlifeActor> Wildlife => actors.AsReadOnly();

        public DungeonWildlifeSaveData Capture() => new()
        {
            nextSequence = nextSequence,
            wildlife = actors.Where(value => value != null)
                .Select(value => value.Capture()).ToList()
        };

        internal void Restore(DungeonWildlifeSaveData saveData)
        {
            ValidateRestorePayload(saveData);
            ClearActors();
            nextSequence = saveData.nextSequence;
            foreach (WildlifeSaveData row in saveData.wildlife)
            {
                Require(species.TryGetSpecies(row.speciesId, out WildlifeSpeciesDefinition definition),
                    "Restored wildlife species is not authored: " + row.speciesId);
                actors.Add(CreateActor(
                    definition,
                    row.wildlifeId,
                    new Vector2Int(row.gridX, row.gridY),
                    row));
            }
        }

        public void ValidateRestorePayload(DungeonWildlifeSaveData saveData)
        {
            if (saveData == null
                || saveData.version != DungeonWildlifeSaveData.CurrentVersion
                || saveData.nextSequence < 1
                || saveData.wildlife == null
                || saveData.wildlife.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.wildlifeId)
                    || !grid.IsValidGridPos(new Vector2Int(value.gridX, value.gridY))))
            {
                throw new InvalidOperationException(
                    "Controlled arrival wildlife payload is invalid.");
            }
        }

        public WildlifeRestoreCandidate BuildRestoreCandidate(
            DungeonWildlifeSaveData saveData) =>
            throw Unsupported(nameof(BuildRestoreCandidate));

        public void PublishRestoreCandidate(WildlifeRestoreCandidate candidate) =>
            throw Unsupported(nameof(PublishRestoreCandidate));

        public bool TrySpawnArrival(
            string speciesId,
            Vector2Int position,
            out WildlifeActor actor,
            out string message)
        {
            actor = null;
            message = string.Empty;
            if (!string.Equals(speciesId, "rune_deer", StringComparison.Ordinal)
                || !grid.IsValidGridPos(position)
                || !species.TryGetSpecies(speciesId, out WildlifeSpeciesDefinition definition))
            {
                message = "Authored arrival species or position is invalid.";
                return false;
            }
            string id = "wild:" + nextSequence++;
            actor = CreateActor(definition, id, position, null);
            actors.Add(actor);
            return true;
        }

        public bool TryRemoveArrival(string wildlifeId)
        {
            WildlifeActor actor = actors.FirstOrDefault(value => value != null
                && string.Equals(value.WildlifeId, wildlifeId,
                    StringComparison.Ordinal));
            if (actor == null)
                return false;
            actors.Remove(actor);
            actor.PrepareForDespawn();
            Object.DestroyImmediate(actor.gameObject);
            return true;
        }

        private WildlifeActor CreateActor(
            WildlifeSpeciesDefinition definition,
            string id,
            Vector2Int position,
            WildlifeSaveData saveData)
        {
            var gameObject = new GameObject("Arrival wildlife " + id);
            gameObject.AddComponent<SpriteRenderer>();
            WildlifeActor actor = gameObject.AddComponent<WildlifeActor>();
            actor.ConfigureRuntimeServices(
                TabooButcheryCausalTestPorts.Guard<IGridPathSearchBroker>(),
                registry,
                clock,
                new RandomStreamProvider(15485863),
                TabooButcheryCausalTestPorts.Guard<IDoorAccessQuery>());
            actor.Initialize(grid, definition, id, position, saveData);
            return actor;
        }

        private void ClearActors()
        {
            foreach (WildlifeActor actor in actors.ToArray())
            {
                if (actor == null)
                    continue;
                actor.PrepareForDespawn();
                Object.DestroyImmediate(actor.gameObject);
            }
            actors.Clear();
        }

        public void Dispose() => ClearActors();
        public bool DesignateHunt(string wildlifeId, bool designated, bool priority = false) =>
            throw Unsupported(nameof(DesignateHunt));
        public bool HasAvailableHuntJob(CharacterActor actor) => false;
        public bool TryReserveBestHuntJob(CharacterActor actor, out WildlifeHuntJob job, out string reason)
        { job = default; reason = "unsupported"; return false; }
        public void ReleaseHuntReservation(string wildlifeId, CharacterActor actor) =>
            throw Unsupported(nameof(ReleaseHuntReservation));
        public bool ApplyHuntHit(CharacterActor hunter, string wildlifeId, out string message)
        { message = "unsupported"; return false; }
        public bool CanAttackHuntTargetFrom(CharacterActor hunter, WildlifeActor target, Grid grid, Vector2Int attackerCell) => false;
        public bool NeedsHuntReload(CharacterActor hunter) => false;
        public float GetHuntReloadDuration(CharacterActor hunter) => 0f;
        public bool TryReloadHuntWeapon(CharacterActor hunter, out string message)
        { message = "unsupported"; return false; }
        public float GetHuntAttackInterval(CharacterActor hunter) => 0f;
        public bool TryButcherNextCarcass(CharacterActor butcher, BuildableObject building, out int produced, out string message)
        { produced = 0; message = "unsupported"; return false; }
        public bool HasButcherWorkAvailable(BuildableObject building) => false;
        public float GetButcherWorkUrgency() => 0f;
        public bool DebugSpawn(string speciesId, int amount, Vector2Int position, out int spawned, out string message)
        { spawned = 0; message = "unsupported"; return false; }
        public IReadOnlyList<WorldItemStackSnapshot> GetReachableFoodRaidTargets() => Array.Empty<WorldItemStackSnapshot>();
        public IReadOnlyList<WildlifeFoodRaidOrderSnapshot> GetFoodRaidOrders() => Array.Empty<WildlifeFoodRaidOrderSnapshot>();
        public bool TryBeginFoodRaid(string raidId, int wolfCount, out IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders, out string failureReason)
        { orders = Array.Empty<WildlifeFoodRaidOrderSnapshot>(); failureReason = "unsupported"; return false; }
        public bool TrySpawnDomesticBirth(string speciesId, Vector2Int position, out WildlifeActor actor, out string message)
        { actor = null; message = "unsupported"; return false; }
        public bool DebugDelete(string wildlifeId) => TryRemoveArrival(wildlifeId);
        public int DebugDeleteAll() { int count = actors.Count; ClearActors(); return count; }
    }

    private sealed class ArrivalRewardContextBuilder : IOffenseRewardContextBuilder
    {
        private readonly IOffenseReturnArrivalRuntime arrivals;
        internal ArrivalRewardContextBuilder(IOffenseReturnArrivalRuntime arrivals) =>
            this.arrivals = arrivals;
        public OffenseRewardContext Create(
            OffenseTargetDefinition target,
            OffenseRewardState state,
            OffenseRewardDebugContext debugContext,
            string expeditionId = "") => new()
        {
            target = target,
            rewardState = state,
            expeditionId = expeditionId,
            returnArrivalRuntime = arrivals
        };
    }

    private sealed class FixedCampaignCatalog : IOffenseCampaignCatalog
    {
        private readonly IReadOnlyList<OffenseTargetDefinition> targets;
        internal FixedCampaignCatalog(IEnumerable<OffenseTargetDefinition> targets) =>
            this.targets = targets.Select(value => value.CreateRuntimeCopy()).ToArray();
        public IReadOnlyList<OffenseTargetDefinition> Targets => targets;
        public bool TryGet(string targetId, out OffenseTargetDefinition definition)
        {
            OffenseTargetDefinition found = targets.FirstOrDefault(value =>
                string.Equals(value.id, targetId, StringComparison.Ordinal));
            definition = found?.CreateRuntimeCopy();
            return definition != null;
        }
    }

    private sealed class FixedGridProvider : IGridSystemProvider
    {
        private readonly Grid grid;
        internal FixedGridProvider(Grid grid) => this.grid = grid;
        public Grid Grid => grid;
        public GridSystemManager Manager => throw Unsupported(nameof(Manager));
        public bool TryGetGrid(out Grid value) { value = grid; return true; }
        public bool TryGetManager(out GridSystemManager value)
        { value = null; return false; }
    }

    private sealed class MutableDropZone : IWorldDropZoneQuery
    {
        internal bool Enabled { get; set; }
        public bool TryGetDeliveryDropoff(out Vector2Int position) =>
            TryGetExpeditionLootDropoff(out position);
        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = new Vector2Int(3, 4);
            return Enabled;
        }
        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        { entryPoint = default; return false; }
    }

    private sealed class MutableClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance(float delta)
        {
            DeltaTime = delta;
            Time += delta;
            FrameCount++;
        }
        internal void Restore(float time)
        {
            Time = Mathf.Max(0f, time);
            DeltaTime = 0f;
            FrameCount = 0;
        }
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        internal FixedCalendar(int day) => Day = day;
        public int Day { get; private set; }
        public int Hour { get; private set; } = 9;
        private CalendarDateTime Projection => GameCalendarRules.Project(Day, Hour);
        public int Year => Projection.Year;
        public int DayOfYear => Projection.DayOfYear;
        public Season Season => Projection.Season;
        public int DayOfSeason => Projection.DayOfSeason;
        public long AbsoluteHour => ((long)Day * 24L) + Hour;
        public float ElapsedSeconds => Day * GameCalendarRules.SecondsPerDay;
        public TimeOfDay TimeOfDay => global::TimeOfDay.Morning;
        public bool IsRunning => true;
        public CalendarDateTime Current => Projection;
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() { }
        public void SetDateTime(int day, int hour)
        { Day = Math.Max(1, day); Hour = Math.Clamp(hour, 0, 23); }
    }

    private sealed class EmptyMetaCatalog : IMetaUpgradeDefinitionCatalog
    {
        internal static readonly EmptyMetaCatalog Instance = new();
        public IReadOnlyCollection<MetaUpgradeDefinition> All =>
            Array.Empty<MetaUpgradeDefinition>();
        public MetaUpgradeDefinition Get(string id) => null;
        public MetaUpgradeDefinition Require(string id) =>
            throw new KeyNotFoundException(id);
    }

    private sealed class EmptyMetaRuntimeApplicationPort :
        IMetaRuntimeApplicationPort
    {
        internal static readonly EmptyMetaRuntimeApplicationPort Instance = new();
        public void Bind(IMetaRuntimeEventSink runtime) { }
        public void Unbind(IMetaRuntimeEventSink runtime) { }
        public MetaRunEnvironmentSnapshot CaptureRunEnvironment() => new(
            1f,
            DungeonDifficulty.Normal,
            DungeonSurvivalPressure.Standard);
        public void PublishUpgradePurchased(
            MetaUpgradePurchasedEvent purchasedEvent,
            string message) { }
        public void PublishRunResult(RunResultReadyEvent readyEvent) { }
        public void ShowRunResult(RunResultSnapshot result) { }
    }

    private sealed class EmptyExpeditionMemberQuery : IOffenseExpeditionMemberQuery
    {
        internal static readonly EmptyExpeditionMemberQuery Instance = new();
        public IReadOnlyList<CharacterActor> GetAvailableMemberActors() =>
            Array.Empty<CharacterActor>();
    }

    private sealed class EmptyOffensePanelService : IOffensePanelService
    {
        internal static readonly EmptyOffensePanelService Instance = new();
        public OffenseWorldMapPanel ShowWorldMap() => null;
        public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime) => null;
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IProductionCausalActionFixture[] fixtures;
        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures.ToArray();
        internal IProductionCausalActionFixture Get(string id) =>
            fixtures.Single(value => string.Equals(value.ActionId, id,
                StringComparison.Ordinal));
        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static OffenseExpeditionResult RestoreResult(
        DungeonOffenseExpeditionResultSaveData source) =>
        new(
            source.expeditionId,
            source.targetId,
            source.targetTitle,
            source.success,
            source.totalPower,
            source.requiredPower,
            source.danger,
            source.elapsedSeconds,
            source.members.Select(value => new OffenseExpeditionMemberSnapshot(
                value.name, value.speciesTag, value.power, value.survived,
                value.damageTaken)).ToArray(),
            source.rewardSummaries,
            source.grantedRewards.Select(value => new OffenseRewardGrantResult(
                value.category,
                value.label,
                value.requestedAmount,
                value.grantedAmount,
                value.success,
                value.detail,
                value.physicalItems.Select(item =>
                    new OffenseRewardPhysicalItemGrant(
                        item.itemId, item.quantity)).ToArray(),
                value.hasStockSupply
                    ? new OffenseStockRewardReceipt(value.stockSupply)
                    : null)).ToArray(),
            source.itemReceipts.Select(RestoreItemReceipt).ToArray(),
            source.treatmentReceipts.Select(value =>
                new OffenseExpeditionTreatmentReceipt(
                    value.kind,
                    value.characterId,
                    value.anatomyNodeId,
                    value.healedAmount)).ToArray(),
            source.arrivalReceipts.Select(value =>
                new OffenseExpeditionArrivalReceipt(
                    value.arrivalId,
                    value.kind,
                    value.requestedAmount,
                    value.materializedAmount,
                    value.securedAmount,
                    value.escapedAmount,
                    value.resolution)).ToArray(),
            source.currencyReceipts.Select(value =>
                new OffenseExpeditionCurrencyReceipt(
                    value.currencyId, value.amount, value.operationId)).ToArray());

    private static OffenseExpeditionItemReceipt RestoreItemReceipt(
        DungeonOffenseItemReceiptSaveData value)
    {
        DungeonOffenseItemValuationSaveData valuation = value.valuation;
        return new OffenseExpeditionItemReceipt(
            value.kind,
            value.itemId,
            value.quantity,
            value.instanceId,
            value.durabilityLoss,
            new OffenseItemValuationSnapshot(
                valuation.itemId,
                valuation.quantity,
                valuation.state,
                valuation.acquisitionMilliEwuPerUnit,
                valuation.recoverableMilliEwuPerUnit,
                valuation.basisId,
                valuation.selectedSourceId));
    }

    private static T Read<T>(JToken token) where T : class =>
        JsonUtility.FromJson<T>(token?.ToString(Formatting.None)
            ?? throw new InvalidOperationException("Checkpoint payload is missing."))
        ?? throw new InvalidOperationException("Checkpoint payload is invalid: " + typeof(T).Name);

    private static void RequireFields(
        JObject value,
        string label,
        params string[] fields)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
        string[] actual = value.Properties().Select(value => value.Name)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] expected = fields.OrderBy(value => value,
            StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException(label + " fields are not closed.");
    }

    private static Exception Unsupported(string method) =>
        new InvalidOperationException(
            "Offense-arrival owner attempted an uncomposed operation: " + method);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
