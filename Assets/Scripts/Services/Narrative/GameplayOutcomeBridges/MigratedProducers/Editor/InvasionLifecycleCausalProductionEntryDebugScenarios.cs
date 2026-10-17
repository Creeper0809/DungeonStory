#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    internal const string InvasionLifecycleCausalOwnerRestoreKind =
        "owner-family:invasion-lifecycle";
    internal const string InvasionStartActionId =
        "action:invasion-start";
    internal const string InvasionBreachActionId =
        "action:invasion-breach-dungeon";
    internal const string InvasionDamageActionId =
        "action:invasion-damage-facility";
    internal const string InvasionFrontCollapseActionId =
        "action:defense-front-collapse";
    internal const string InvasionResolveActionId =
        "action:invasion-resolve-defense-failed";

    private const string InvasionLifecycleOwnerKind =
        "owner-kind:invasion-lifecycle";
    private const string InvasionLifecycleDomainSchema =
        "dungeonstory-causal-invasion-lifecycle-owner-state@2";
    private const string InvasionDamageFacilityId =
        "building:causal:invasion-damage-target";
    private const string InvasionOwnerCharacterId =
        "character:migrated-defense-target:causal-invasion-owner";
    private const int InvasionAbsoluteDay = 21;
    private static readonly Vector2Int InvasionInteriorCell =
        new(1, 0);
    private static readonly Vector2Int InvasionDamageFacilityCell =
        new(2, 0);
    private static readonly Vector2Int InvasionOwnerCell =
        new(7, 0);

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateInvasionLifecycleCausalFixtures(
            string causalRunId,
            string ownerId = null,
            JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
        {
            throw new ArgumentException(
                "A causal run ID is required.",
                nameof(causalRunId));
        }

        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "An invasion checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new InvasionLifecycleContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            5,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            InvasionLifecycleFixture(
                InvasionStartActionId,
                MigratedProducerOutcomeKind.InvasionStartedEvent,
                context,
                context.StartInvasion,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.InvasionStartedEvent,
                    1),
                lifetime),
            InvasionLifecycleFixture(
                InvasionBreachActionId,
                MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
                context,
                context.BreachDungeon,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
                    2),
                lifetime),
            InvasionLifecycleFixture(
                InvasionDamageActionId,
                MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
                context,
                context.DamageFacility,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
                    3),
                lifetime),
            InvasionLifecycleFixture(
                InvasionFrontCollapseActionId,
                MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
                context,
                context.CollapseDefenseFront,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
                    4),
                lifetime),
            InvasionLifecycleFixture(
                InvasionResolveActionId,
                MigratedProducerOutcomeKind.InvasionResolvedEvent,
                context,
                context.ResolveDefenseFailed,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.InvasionResolvedEvent,
                    5),
                lifetime)
        };
    }

    internal static bool RunInvasionLifecycleCausalOwnerFamily(
        bool log = false)
    {
        const string OwnerId = "invasion:lifecycle:focused";
        JObject startedCheckpoint;
        using (var fixtures = new InvasionLifecycleFixtureSet(
                   CreateInvasionLifecycleCausalFixtures(
                       "run:causal-invasion:start",
                       OwnerId)))
        {
            IProductionCausalActionFixture start =
                fixtures.Get(InvasionStartActionId);
            Require(
                (int)start.CaptureState()["invasionLifecycle.stage"] == 0,
                "Invasion owner did not start before its first spawn.");
            Require(start.Execute(), "The production invasion start failed.");
            Require(
                start.RequireExactOutcome().HasCanonicalPayloadHash,
                "The invasion-start outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = start.CaptureState();
            Require(
                (int)after["invasionLifecycle.stage"] == 1
                && (int)after["invasionLifecycle.activeIntruders"] == 1,
                "The started invasion did not retain one active intruder.");
            startedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)start)
                .CaptureOwnerCheckpoint();
        }

        JObject breachedCheckpoint;
        using (var fixtures = new InvasionLifecycleFixtureSet(
                   CreateInvasionLifecycleCausalFixtures(
                       "run:causal-invasion:breach",
                       OwnerId,
                       startedCheckpoint)))
        {
            IProductionCausalActionFixture start =
                fixtures.Get(InvasionStartActionId);
            IProductionCausalActionFixture breach =
                fixtures.Get(InvasionBreachActionId);
            Require(!start.Execute(),
                "Fresh composition replayed the invasion start.");
            Require(breach.Execute(),
                "The production dungeon-breach command failed.");
            Require(
                breach.RequireExactOutcome().HasCanonicalPayloadHash,
                "The invasion-breach outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = breach.CaptureState();
            Require(
                (int)after["invasionLifecycle.stage"] == 2
                && (bool)after["invasionLifecycle.hasBreachedDungeon"]
                && (int)after["invasionLifecycle.activeIntruders"] == 1,
                "The breached invasion lost its active intruder or breach state.");
            breachedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)breach)
                .CaptureOwnerCheckpoint();
        }

        JObject damagedCheckpoint;
        using (var fixtures = new InvasionLifecycleFixtureSet(
                   CreateInvasionLifecycleCausalFixtures(
                       "run:causal-invasion:damage",
                       OwnerId,
                       breachedCheckpoint)))
        {
            IProductionCausalActionFixture breach =
                fixtures.Get(InvasionBreachActionId);
            IProductionCausalActionFixture damage =
                fixtures.Get(InvasionDamageActionId);
            Require(!breach.Execute(),
                "Fresh composition replayed the dungeon breach.");
            Require(damage.Execute(),
                "The production facility-damage command failed.");
            Require(
                damage.RequireExactOutcome().HasCanonicalPayloadHash,
                "The invasion-damage outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = damage.CaptureState();
            Require(
                (int)after["invasionLifecycle.stage"] == 3
                && (int)after["invasionLifecycle.facilityDamageCount"] == 1
                && (bool)after["invasionLifecycle.facilityDamaged"],
                "The damaged invasion did not retain its physical facility state.");
            damagedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)damage)
                .CaptureOwnerCheckpoint();
        }

        JObject collapsedCheckpoint;
        using (var fixtures = new InvasionLifecycleFixtureSet(
                   CreateInvasionLifecycleCausalFixtures(
                       "run:causal-invasion:front-collapse",
                       OwnerId,
                       damagedCheckpoint)))
        {
            IProductionCausalActionFixture collapse =
                fixtures.Get(InvasionFrontCollapseActionId);
            Require(collapse.Execute(),
                "The production defense-front collapse command failed.");
            Require(
                collapse.RequireExactOutcome().HasCanonicalPayloadHash,
                "The defense-front collapse outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = collapse.CaptureState();
            Require(
                (int)after["invasionLifecycle.stage"] == 4
                && (bool)after["invasionLifecycle.frontCollapsed"]
                && (int)after["invasionLifecycle.activeIntruders"] == 1,
                "The collapsed defense front lost its invasion owner state.");
            collapsedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)collapse)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new InvasionLifecycleFixtureSet(
                   CreateInvasionLifecycleCausalFixtures(
                       "run:causal-invasion:resolve",
                       OwnerId,
                       collapsedCheckpoint)))
        {
            IProductionCausalActionFixture collapse =
                fixtures.Get(InvasionFrontCollapseActionId);
            IProductionCausalActionFixture resolve =
                fixtures.Get(InvasionResolveActionId);
            Require(!collapse.Execute(),
                "Fresh composition replayed the defense-front collapse.");
            Require(resolve.Execute(),
                "The production invasion terminal command failed.");
            Require(
                resolve.RequireExactOutcome().HasCanonicalPayloadHash,
                "The invasion-resolution outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = resolve.CaptureState();
            Require(
                (int)after["invasionLifecycle.stage"] == 5
                && (int)after["invasionLifecycle.activeIntruders"] == 0
                && string.Equals(
                    (string)after["invasionLifecycle.resolutionStatus"],
                    GameplayOutcomeStatus.Failed.ToString(),
                    StringComparison.Ordinal),
                "The terminal invasion remained active or changed its result status.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)resolve)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new InvasionLifecycleFixtureSet(
                   CreateInvasionLifecycleCausalFixtures(
                       "run:causal-invasion:terminal",
                       OwnerId,
                       terminalCheckpoint)))
        {
            IProductionCausalActionFixture resolve =
                fixtures.Get(InvasionResolveActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)resolve)
                .CaptureOwnerCheckpoint();
            Require(
                !fixtures.Get(InvasionStartActionId).Execute()
                && !fixtures.Get(InvasionBreachActionId).Execute()
                && !fixtures.Get(InvasionDamageActionId).Execute()
                && !fixtures.Get(InvasionFrontCollapseActionId).Execute()
                && !resolve.Execute(),
                "Terminal invasion owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)resolve)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected invasion replay mutated the checkpoint.");
        }

        JObject breachTamper = (JObject)breachedCheckpoint.DeepClone();
        ((JObject)breachTamper["domainState"])["invasion"]![
            "activeIntruders"]![0]!["hasBreachedDungeonInterior"] = false;
        ((JObject)breachTamper["domainState"])["invasion"]![
            "activeIntruders"]![0]!["state"] =
            (int)InvasionIntruderState.Entering;
        Require(
            RejectInvasionLifecycleCheckpoint(
                OwnerId,
                breachTamper,
                "breach-tamper"),
            "An invasion checkpoint with a false breach state was accepted.");

        JObject facilityTamper = (JObject)damagedCheckpoint.DeepClone();
        ((JObject)facilityTamper["domainState"])["facilityDamaged"] = false;
        Require(
            RejectInvasionLifecycleCheckpoint(
                OwnerId,
                facilityTamper,
                "facility-tamper"),
            "An invasion checkpoint with a false facility state was accepted.");

        JObject runtimeTamper = (JObject)damagedCheckpoint.DeepClone();
        ((JObject)runtimeTamper["domainState"])["runtimeId"] =
            "invasion:tampered-runtime";
        Require(
            RejectInvasionLifecycleCheckpoint(
                OwnerId,
                runtimeTamper,
                "runtime-tamper"),
            "An invasion checkpoint with a detached runtime ID was accepted.");

        if (log)
        {
            Debug.Log(
                "Invasion start -> restored breach -> restored facility damage -> restored defense-front collapse -> restored terminal resolution causal owner family PASS");
        }
        return true;
    }

    public static void RunG06FromBatch()
    {
        try
        {
            using var context = new InvasionLifecycleContext(
                "run:u0-g06-milestone-pressure",
                ownerId: null,
                checkpoint: null,
                milestonePressure: true);
            G06ScenarioReport report = context.RunG06MilestonePressure();
            WriteG06Report(report);
            Debug.Log(
                "[AuditRemediation][G06] PASS "
                + report.operationId
                + " -> "
                + report.invasionRuntimeId);
        }
        catch (Exception exception)
        {
            WriteG06Report(new G06ScenarioReport
            {
                passed = false,
                failure = exception.GetType().Name + ": " + exception.Message,
                fullAuditClosure = false,
                humanApprovalClaimed = false,
                trainingEligible = false
            });
            Debug.LogException(exception);
            throw;
        }
    }

    private static void WriteG06Report(G06ScenarioReport report)
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "G06 verification requires its isolated output directory.");
        }

        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "report.json");
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented));
    }

    private sealed class G06ScenarioReport
    {
        public string rowId = "G06";
        public bool passed;
        public string failure = string.Empty;
        public int nonApplicableOperationCount;
        public int sameDayRedeliveryOperationCount;
        public int milestoneAlertCount;
        public int scheduledAbsoluteDay;
        public int actualStartAbsoluteDay;
        public string sourceOwnerId = string.Empty;
        public string operationId = string.Empty;
        public string invasionRuntimeId = string.Empty;
        public string outcomeRunId = string.Empty;
        public long outcomeSequence;
        public string rootOperationId = string.Empty;
        public string causationRelationId = string.Empty;
        public bool scheduledSaveRoundTrip;
        public bool startedSaveRoundTrip;
        public bool terminalReplayNoDuplicate;
        public bool fullAuditClosure;
        public bool humanApprovalClaimed;
        public bool trainingEligible;
    }

    public static void RunInvasionLifecycleCausalBatch()
    {
        try
        {
            Require(
                RunInvasionLifecycleCausalOwnerFamily(log: true),
                "Invasion lifecycle causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static IProductionCausalActionFixture InvasionLifecycleFixture(
        string actionId,
        MigratedProducerOutcomeKind kind,
        InvasionLifecycleContext context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "invasion",
            kind,
            context.CaptureState,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner
                ? context.CaptureOwnerCheckpoint
                : null,
            context.HasPersistentOwner
                ? InvasionLifecycleCausalOwnerRestoreKind
                : null,
            context.HasPersistentOwner ? context : null);

    private static bool RejectInvasionLifecycleCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new InvasionLifecycleFixtureSet(
                CreateInvasionLifecycleCausalFixtures(
                    "run:causal-invasion:" + suffix,
                    ownerId,
                    checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class InvasionLifecycleContext : IDisposable
    {
        private readonly string ownerId;
        private readonly OutcomeHarness outcomes;
        private readonly CausalInvasionClock clock;
        private readonly Grid grid;
        private readonly GameEventBus events;
        private readonly BuildingFixture damageTarget;
        private readonly ActorFixture ownerActor;
        private readonly CausalInvasionContext invasionContext;
        private readonly CausalInvasionIntruderFactory intruderFactory;
        private readonly GameObject directorObject;
        private readonly InvasionDirectorRuntime director;
        private readonly IInvasionIntruderPatternDefinitionCatalog patterns;
        private readonly InvasionCampaignRuntime campaign;
        private readonly MilestonePressureApplicationAdapter pressure;
        private string runtimeId = string.Empty;
        private bool disposed;

        internal InvasionLifecycleContext(
            string causalRunId,
            string ownerId,
            JObject checkpoint,
            bool milestonePressure = false)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    InvasionLifecycleOwnerKind)
                : causalRunId + ":invasion-ledger";
            outcomes = new OutcomeHarness(
                MigratedProducerOutcomeKind.InvasionStartedEvent,
                "causal-invasion",
                includeCombatDamage: false,
                faultMode: MigratedProducerOutcomeFaultMode.None,
                deliveryFault: false,
                stableRunId: ledgerRunId);
            clock = new CausalInvasionClock(InvasionAbsoluteDay);
            grid = new Grid(8, 2);
            for (int y = 0; y < grid.height; y++)
            {
                for (int x = 0; x < grid.width; x++)
                {
                    grid.SetAreaType(
                        new Vector2Int(x, y),
                        GridCellAreaType.DungeonInterior);
                }
            }
            damageTarget = new BuildingFixture(
                grid,
                "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset",
                InvasionDamageFacilityCell,
                InvasionDamageFacilityId);
            ownerActor = new ActorFixture(
                CharacterType.NPC,
                "Causal Invasion Owner",
                "causal-invasion-owner");
            ownerActor.Actor.transform.position = grid.GetWorldPos(
                InvasionOwnerCell);
            invasionContext = new CausalInvasionContext(
                grid,
                ownerActor.Actor,
                damageTarget.Building);
            var content = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            patterns = new AuthoredGameplayCatalog(content);
            intruderFactory = new CausalInvasionIntruderFactory(
                patterns,
                damageTarget.Building);

            CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset")
                ?? throw new InvalidOperationException(
                    "Authored invasion intruder fixture is missing.");
            EnemyArchetypeDefinitionSO archetype = AssetDatabase
                .LoadAssetAtPath<EnemyArchetypeDefinitionSO>(
                    "Assets/Resources/SO/V20/Combat/Enemies/enemy_neutral-mercenary.asset")
                ?? throw new InvalidOperationException(
                    "Authored enemy archetype fixture is missing.");
            events = new GameEventBus();
            var randomStreams = new RandomStreamProvider(7319);
            if (milestonePressure)
            {
                campaign = new InvasionCampaignRuntime(
                    new EmptyOffenseWorldSimulation(),
                    events,
                    randomStreams,
                    new InvasionAggregateStateStore(
                        outcomes.AggregateRootStore));
                campaign.Start();
                var run = new V20CampaignRuntime(
                    outcomes.AggregateRootStore,
                    new V20StoryContentCatalog(content));
                RunMilestoneWorldSaveData milestoneState =
                    run.CaptureMilestones();
                milestoneState.activePressureIds.Clear();
                milestoneState.activePressureIds.Add(
                    "pressure:ending:truth-revealed");
                run.PublishMilestones(
                    run.PrepareMilestones(milestoneState));
                if (!run.HasPressure("ending:truth-revealed"))
                {
                    throw new InvalidOperationException(
                        "The G06 milestone pressure was not activated.");
                }
                var life = new CharacterLifeRuntime(
                    outcomes.AggregateRootStore,
                    EmptyCharacterLifeDefinitions.Instance,
                    randomStreams);
                pressure = new MilestonePressureApplicationAdapter(
                    run,
                    campaign,
                    run,
                    run,
                    run,
                    life,
                    life,
                    events);
                pressure.Start();
            }
            directorObject = new GameObject("Causal invasion director");
            director = directorObject.AddComponent<InvasionDirectorRuntime>();
            director.Construct(
                invasionContext,
                new FixedIntruderDataProvider(data),
                intruderFactory,
                new DefenseStatusRuntimeService(
                    new DefenseStatusRuntimeFactory()),
                clock,
                randomStreams,
                events,
                new OffenseRegionRuntime(),
                new NoopTreasuryDefenseRuntime(),
                externalInfluence: null,
                campaignRuntime: campaign,
                new EmptyFacilityCapabilities(),
                new InvasionSignalHornDurableEquipmentRuntime(
                    new MissingDurableEquipmentPolicies(),
                    new MissingDurableEquipmentSlots(),
                    new MissingDurableEquipmentUse()),
                new FixedCharacterPerformance(),
                outcomes.InvasionFacts,
                clock);
            director.ConfigureEnemyIndividuals(
                new FixtureEnemyArchetypeCatalog(archetype),
                new FixtureEnemyIndividuals(data, archetype));
            director.ConfigureEquipment(CharacterAiEditorTestDependencies.CombatEquipment);

            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                outcomes.Probe.ClearObservations();
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
            ? InvasionLifecycleOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            InvasionIntruderRuntime active = ActiveIntruderOrNull();
            int stage = Stage;
            return new Dictionary<string, object>
            {
                ["invasionLifecycle.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "invasion-lifecycle:ephemeral",
                ["invasionLifecycle.stage"] = stage,
                ["invasionLifecycle.runtimeId"] = runtimeId,
                ["invasionLifecycle.ownerCharacterId"] =
                    InvasionOwnerCharacterId,
                ["invasionLifecycle.facilityId"] =
                    InvasionDamageFacilityId,
                ["invasionLifecycle.calendarDay"] =
                    clock.AbsoluteDay,
                ["invasionLifecycle.activeIntruders"] =
                    director.ActiveIntruders.Count,
                ["invasionLifecycle.intruderState"] = active != null
                    ? active.State.ToString()
                    : stage == 5
                        ? InvasionIntruderState.Finished.ToString()
                        : "Absent",
                ["invasionLifecycle.intruderHealth"] = active != null
                    ? active.IntruderActor.CurrentHealth
                    : 0f,
                ["invasionLifecycle.hasBreachedDungeon"] = active != null
                    ? active.HasBreachedDungeonInterior
                    : TypeCount(
                        MigratedProducerOutcomeKind
                            .InvasionDungeonBreachedEvent) == 1,
                ["invasionLifecycle.facilityDamageCount"] = active != null
                    ? active.FacilityDamageCount
                    : TypeCount(
                        MigratedProducerOutcomeKind
                            .InvasionFacilityDamagedEvent),
                ["invasionLifecycle.facilityDamaged"] =
                    damageTarget.Building.IsDamaged,
                ["invasionLifecycle.frontCollapsed"] = TypeCount(
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent) == 1,
                ["invasionLifecycle.startOwnerRevision"] = OwnerRevision(
                    MigratedProducerOutcomeKind.InvasionStartedEvent,
                    runtimeId),
                ["invasionLifecycle.breachOwnerRevision"] = OwnerRevision(
                    MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
                    runtimeId),
                ["invasionLifecycle.damageOwnerRevision"] = OwnerRevision(
                    MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
                    runtimeId.Length == 0
                        ? string.Empty
                        : runtimeId + ":facility-damage:"
                            + InvasionDamageFacilityId),
                ["invasionLifecycle.frontCollapseOwnerRevision"] = OwnerRevision(
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
                    runtimeId.Length == 0
                        ? string.Empty
                        : runtimeId + ":front-collapse"),
                ["invasionLifecycle.resolveOwnerRevision"] = OwnerRevision(
                    MigratedProducerOutcomeKind.InvasionResolvedEvent,
                    runtimeId),
                ["invasionLifecycle.resolutionStatus"] = stage == 5
                    ? GameplayOutcomeStatus.Failed.ToString()
                    : "Pending",
                ["invasionLifecycle.exactOutcomeCount"] =
                    ExactOutcomeCount()
            };
        }

        internal bool StartInvasion()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            var threat = new InvasionThreatSnapshot(
                25f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(10f, 5f, 5f, 5f),
                0f,
                0f);
            if (!director.TrySpawnIntruder(
                    threat,
                    out CharacterActor intruder)
                || intruder == null
                || director.ActiveIntruders.Count != 1)
            {
                return false;
            }
            runtimeId = director.ActiveIntruders.Single().RuntimeId;
            bool valid = Stage == 1;
            if (valid)
                ValidateState();
            return valid;
        }

        internal G06ScenarioReport RunG06MilestonePressure()
        {
            ThrowIfDisposed();
            if (campaign == null || pressure == null)
            {
                throw new InvalidOperationException(
                    "G06 requires the milestone-pressure campaign fixture.");
            }

            const int NonApplicableDay = 19;
            const int ScheduledDay = 20;
            const string SourceOwnerId =
                "milestone-pressure:truth-revealed:20";
            var alerts = new List<EventAlertRequest>();
            using IDisposable subscription = events.Subscribe<
                EventAlertRequestedEvent>(value =>
                {
                    if (value.request != null)
                        alerts.Add(value.request);
                });

            events.Publish(new OperatingDayStartedEvent(NonApplicableDay));
            events.Publish(new OperatingDayEndedEvent(NonApplicableDay));
            int nonApplicableCount = campaign.Operations.Count;
            Require(
                nonApplicableCount == 0,
                "A non-applicable milestone-pressure day scheduled an invasion.");

            events.Publish(new OperatingDayStartedEvent(ScheduledDay));
            events.Publish(new OperatingDayEndedEvent(ScheduledDay));
            ScheduledInvasionOperationState scheduled = campaign.Operations
                .Single();
            string operationId = scheduled.operationId;
            Require(
                scheduled.scheduledDay == ScheduledDay
                && scheduled.sourceAbsoluteDay == ScheduledDay
                && string.Equals(
                    scheduled.sourceOwnerId,
                    SourceOwnerId,
                    StringComparison.Ordinal)
                && string.IsNullOrEmpty(
                    scheduled.startedInvasionRuntimeId),
                "Milestone pressure did not create the exact scheduled operation.");

            events.Publish(new OperatingDayEndedEvent(ScheduledDay));
            int redeliveryCount = campaign.Operations.Count;
            int milestoneAlertCount = alerts.Count(value => string.Equals(
                value.SourceId,
                SourceOwnerId,
                StringComparison.Ordinal));
            Require(
                redeliveryCount == 1
                && campaign.Operations.Single().operationId == operationId
                && milestoneAlertCount == 1,
                "Same-day milestone-pressure redelivery duplicated its operation or alert.");

            InvasionCampaignSaveData scheduledSave = JsonUtility.FromJson<
                InvasionCampaignSaveData>(
                    JsonUtility.ToJson(campaign.Capture()));
            campaign.ReplaceFromValidatedSnapshot(scheduledSave);
            ScheduledInvasionOperationState scheduledAfterRestore =
                campaign.Operations.Single();
            Require(
                scheduledAfterRestore.operationId == operationId
                && scheduledAfterRestore.sourceOwnerId == SourceOwnerId
                && scheduledAfterRestore.sourceAbsoluteDay == ScheduledDay
                && string.IsNullOrEmpty(
                    scheduledAfterRestore.startedInvasionRuntimeId),
                "Scheduled milestone-pressure identity did not survive restore.");

            events.Publish(new OperatingDayStartedEvent(InvasionAbsoluteDay));
            Require(
                StartInvasion(),
                "The scheduled milestone-pressure operation did not start an invasion.");
            GameplayOutcomeReplayIdentity identity = outcomes.RequireExact(
                MigratedProducerOutcomeKind.InvasionStartedEvent,
                GameplayOutcomeReplayState.PublishedAcknowledged);
            Require(
                outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact),
                "The actual invasion-start ledger row is missing.");
            Require(
                exact.absoluteDay == InvasionAbsoluteDay
                && exact.rootOperationId == operationId
                && exact.causationRelationId
                    == "milestone-pressure-invasion-start"
                && exact.parentSequence == 0L
                && string.IsNullOrEmpty(exact.parentRunId)
                && exact.participants.Any(value =>
                    value.entityKindId
                        == MigratedProducerOutcomeIds.OperationKind.Value
                    && value.entityId == operationId
                    && value.roleId
                        == MigratedProducerOutcomeIds.SourceRole.Value)
                && exact.facts.Any(value =>
                    value.factId
                        == MigratedProducerOutcomeIds.ReasonFact.Value
                    && value.value == SourceOwnerId),
                "The actual invasion start detached from its scheduled milestone pressure.");

            ScheduledInvasionOperationState started = campaign.Operations
                .Single();
            InvasionFactPublicationData startObservation = outcomes
                .InvasionFacts.Capture().observations.Single(value =>
                    value.fact.runtimeId == runtimeId
                    && value.fact.stage == InvasionLifecycleStage.Started);
            Require(
                started.operationId == operationId
                && started.startedInvasionRuntimeId == runtimeId
                && started.startedPublicationKey
                    == startObservation.publicationKey
                && !startObservation.pending,
                "The scheduled operation did not retain its exact actual-start identity.");

            InvasionCampaignSaveData startedSave = JsonUtility.FromJson<
                InvasionCampaignSaveData>(
                    JsonUtility.ToJson(campaign.Capture()));
            campaign.ReplaceFromValidatedSnapshot(startedSave);
            ScheduledInvasionOperationState startedAfterRestore =
                campaign.Operations.Single();
            Require(
                startedAfterRestore.operationId == operationId
                && startedAfterRestore.startedInvasionRuntimeId == runtimeId
                && startedAfterRestore.startedPublicationKey
                    == startObservation.publicationKey,
                "Actual invasion-start identity did not survive campaign restore.");

            int exactBeforeReplay = ExactOutcomeCount();
            Require(
                !StartInvasion()
                && campaign.Operations.Count == 1
                && ExactOutcomeCount() == exactBeforeReplay,
                "Terminal invasion-start replay created a duplicate operation or outcome.");

            return new G06ScenarioReport
            {
                passed = true,
                nonApplicableOperationCount = nonApplicableCount,
                sameDayRedeliveryOperationCount = redeliveryCount,
                milestoneAlertCount = milestoneAlertCount,
                scheduledAbsoluteDay = ScheduledDay,
                actualStartAbsoluteDay = InvasionAbsoluteDay,
                sourceOwnerId = SourceOwnerId,
                operationId = operationId,
                invasionRuntimeId = runtimeId,
                outcomeRunId = identity.OutcomeId.RunId.Value,
                outcomeSequence = identity.OutcomeId.Sequence,
                rootOperationId = exact.rootOperationId,
                causationRelationId = exact.causationRelationId,
                scheduledSaveRoundTrip = true,
                startedSaveRoundTrip = true,
                terminalReplayNoDuplicate = true,
                fullAuditClosure = false,
                humanApprovalClaimed = false,
                trainingEligible = false
            };
        }

        internal bool BreachDungeon()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            InvasionIntruderRuntime active = RequireActiveIntruder();
            IInvasionIntruderExecutionHost host = active;
            host.State = InvasionIntruderState.Entering;
            active.transform.position = grid.GetWorldPos(InvasionInteriorCell);
            active.IntruderActor.SetLifecycleState(
                CharacterLifecycleState.Active);
            new InvasionIntruderExecutionCoordinator(active)
                .MarkDungeonBreached(grid, InvasionInteriorCell);
            if (active.HasBreachedDungeonInterior)
                host.State = InvasionIntruderState.Searching;
            bool valid = Stage == 2;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool DamageFacility()
        {
            ThrowIfDisposed();
            if (Stage != 2)
                return false;
            InvasionIntruderRuntime active = RequireActiveIntruder();
            active.transform.position = grid.GetWorldPos(InvasionInteriorCell);
            bool committed = active.TryDamageNearbyFacility(
                grid,
                damageTarget.Building);
            bool valid = committed && Stage == 3;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool ResolveDefenseFailed()
        {
            ThrowIfDisposed();
            if (Stage != 4)
                return false;
            RequireActiveIntruder().ResolveDefenseFailed(ownerActor.Actor);
            bool valid = Stage == 5;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool CollapseDefenseFront()
        {
            ThrowIfDisposed();
            if (Stage != 3)
                return false;
            InvasionIntruderRuntime active = RequireActiveIntruder();
            active.gameObject.SetActive(false);
            var store = new DefenseEngagementStore();
            var engagement = new DefenseEngagement
            {
                Id = store.AllocateId(),
                Intruder = active,
                LeadGuard = ownerActor.Actor,
                State = DefenseEngagementState.Engaged,
                StatusText = "engaged"
            };
            store.Add(engagement);
            new DefenseEngagementCombatRuntime(store, events, clock)
                .CollapseFront(
                    engagement,
                    "causal-invasion-defense-front-collapsed");
            bool valid = engagement.State == DefenseEngagementState.Completed
                && store.Engagements.Count == 0
                && active.State == InvasionIntruderState.FrontBroken
                && Stage == 4;
            if (valid)
                ValidateState();
            return valid;
        }

        internal GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            int expectedSequence)
        {
            GameplayOutcomeReplayIdentity identity = outcomes.RequireExact(
                kind,
                GameplayOutcomeReplayState.PublishedAcknowledged);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            GameplayOutcomeStatus expectedStatus = kind
                == MigratedProducerOutcomeKind.InvasionResolvedEvent
                    ? GameplayOutcomeStatus.Failed
                    : GameplayOutcomeStatus.Succeeded;
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.commitRevision != 1L
                || exact.ownerRevision != 1L
                || exact.absoluteDay != InvasionAbsoluteDay
                || exact.status != expectedStatus
                || !string.Equals(
                    exact.producerId,
                    definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || exact.subjects?.Count != ExpectedSubjectCount(kind)
                || exact.participants?.Count != ExpectedSubjectCount(kind)
                || exact.metrics?.Count != 6
                || exact.facts == null
                || !exact.facts.Any(value => string.Equals(
                    value.factId,
                    MigratedProducerOutcomeIds.SummaryFact.Value,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Invasion production entry did not publish its exact outcome: "
                    + kind);
            }
            Require(
                exact.subjects.Any(value =>
                    value.entityKindId
                        == MigratedProducerOutcomeIds.OperationKind.Value
                    && value.entityId == runtimeId)
                && exact.participants.Any(value =>
                    value.roleId
                        == MigratedProducerOutcomeIds.IntruderRole.Value),
                "Invasion outcome lost its root or intruder subject: "
                    + kind);
            Require(
                exact.metrics.Select(value => value.metricId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(
                        RequiredLifecycleMetricIds(),
                        StringComparer.Ordinal),
                "Invasion outcome lost a before/after lifecycle metric: "
                    + kind);
            Require(
                exact.rootOperationId == runtimeId
                && exact.causationRelationId.Length > 0
                && (expectedSequence == 1
                    ? exact.parentSequence == 0L
                        && string.IsNullOrEmpty(exact.parentRunId)
                    : exact.parentSequence == expectedSequence - 1L
                        && exact.parentRunId == exact.runId),
                "Invasion outcome causation chain detached at: " + kind);
            if (kind == MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent)
            {
                Require(
                    exact.participants.Any(value =>
                        value.entityId == InvasionDamageFacilityId
                        && value.roleId == MigratedProducerOutcomeIds
                            .AttackedFacilityRole.Value),
                    "Facility damage lost its attacked-facility role.");
                RequireEntityQueryFinds(
                    new GameplayEntityId(
                        MigratedProducerOutcomeIds.FacilityKind,
                        InvasionDamageFacilityId),
                    exact);
            }
            if (kind == MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent
                || kind == MigratedProducerOutcomeKind.InvasionResolvedEvent)
            {
                Require(
                    exact.participants.Any(value =>
                        value.roleId == MigratedProducerOutcomeIds
                            .DefenderRole.Value),
                    "Defense stage lost its defender role: " + kind);
            }
            GameplayOutcomeParticipantSnapshot intruder = exact.participants
                .Single(value => value.roleId
                    == MigratedProducerOutcomeIds.IntruderRole.Value);
            RequireEntityQueryFinds(
                new GameplayEntityId(
                    MigratedProducerOutcomeIds.CharacterKind,
                    intruder.entityId),
                exact);
            return identity;
        }

        private static int ExpectedSubjectCount(
            MigratedProducerOutcomeKind kind) => kind switch
        {
            MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent => 3,
            MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent => 3,
            MigratedProducerOutcomeKind.InvasionResolvedEvent => 3,
            _ => 2
        };

        private static IEnumerable<string> RequiredLifecycleMetricIds() =>
            new[]
            {
                MigratedProducerOutcomeIds.DamageIndexBeforeMetric.Value,
                MigratedProducerOutcomeIds.DamageIndexAfterMetric.Value,
                MigratedProducerOutcomeIds.StateBeforeMetric.Value,
                MigratedProducerOutcomeIds.StateAfterMetric.Value,
                MigratedProducerOutcomeIds.RiskBeforeMetric.Value,
                MigratedProducerOutcomeIds.RiskAfterMetric.Value
            }.OrderBy(value => value, StringComparer.Ordinal);

        private void RequireEntityQueryFinds(
            GameplayEntityId entity,
            GameplayOutcomeSnapshot expected)
        {
            GameplayOutcomeQueryPage page = outcomes.Ledger.GetForEntity(
                entity,
                OutcomeCursor.FirstPage(20),
                OutcomeFilter.All);
            Require(
                page.Items.Any(value => value.Exact != null
                    && value.Exact.runId == expected.runId
                    && value.Exact.sequence == expected.sequence),
                "Entity perspective query did not find invasion result for "
                    + entity.Value);
        }

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(
                outcomes.Ledger,
                identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral invasion fixtures have no checkpoint.");
            }
            ValidateState();
            DungeonInvasionSaveData invasion = CaptureInvasionSave();
            InvasionFactPublicationSaveData publicationState =
                outcomes.InvasionFacts.Capture();
            InvasionFactPreflight.ValidateOwners(
                publicationState,
                invasion);
            InvasionFactPreflight.ValidateJournal(
                publicationState,
                outcomes.Ledger.CaptureGameplayOutcomes(),
                outcomes.Transaction.Capture());
            string publicationJson =
                DungeonCharacterWorldSaveCodec.SerializeFields(
                    publicationState);
            InvasionFactPublicationSaveSection.ValidateJson(publicationJson);
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                InvasionLifecycleOwnerKind,
                InvasionLifecycleCausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 2,
                    ["schema"] = InvasionLifecycleDomainSchema,
                    ["stage"] = Stage,
                    ["runtimeId"] = runtimeId,
                    ["facilityId"] = InvasionDamageFacilityId,
                    ["ownerCharacterId"] = InvasionOwnerCharacterId,
                    ["calendarDay"] = clock.AbsoluteDay,
                    ["invasion"] = JObject.Parse(
                        JsonUtility.ToJson(invasion)),
                    ["facilityRuntimeState"] = JObject.Parse(
                        JsonUtility.ToJson(
                            damageTarget.Building.FacilityState.Clone())),
                    ["facilityDamaged"] =
                        damageTarget.Building.IsDamaged,
                    ["outcomeOwners"] = JObject.Parse(
                        JsonUtility.ToJson(
                            outcomes.Transaction.Capture())),
                    ["invasionPublications"] = JObject.Parse(
                        publicationJson)
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            pressure?.Dispose();
            campaign?.Dispose();
            intruderFactory?.Dispose();
            if (directorObject != null)
                Object.DestroyImmediate(directorObject);
            ownerActor?.Dispose();
            damageTarget?.Dispose();
            outcomes?.Dispose();
        }

        private int Stage
        {
            get
            {
                int starts = TypeCount(
                    MigratedProducerOutcomeKind.InvasionStartedEvent);
                int breaches = TypeCount(
                    MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent);
                int damages = TypeCount(
                    MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent);
                int collapses = TypeCount(
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent);
                int resolutions = TypeCount(
                    MigratedProducerOutcomeKind.InvasionResolvedEvent);
                int exact = ExactOutcomeCount();
                long startRevision = OwnerRevision(
                    MigratedProducerOutcomeKind.InvasionStartedEvent,
                    runtimeId);
                long breachRevision = OwnerRevision(
                    MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
                    runtimeId);
                long damageRevision = OwnerRevision(
                    MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
                    runtimeId.Length == 0
                        ? string.Empty
                        : runtimeId + ":facility-damage:"
                            + InvasionDamageFacilityId);
                long collapseRevision = OwnerRevision(
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
                    runtimeId.Length == 0
                        ? string.Empty
                        : runtimeId + ":front-collapse");
                long resolveRevision = OwnerRevision(
                    MigratedProducerOutcomeKind.InvasionResolvedEvent,
                    runtimeId);
                if (starts == 0 && breaches == 0 && damages == 0
                    && collapses == 0
                    && resolutions == 0 && exact == 0
                    && startRevision == 0 && breachRevision == 0
                    && damageRevision == 0 && collapseRevision == 0
                    && resolveRevision == 0)
                {
                    return 0;
                }
                if (starts == 1 && breaches == 0 && damages == 0
                    && collapses == 0
                    && resolutions == 0 && exact == 1
                    && startRevision == 1 && breachRevision == 0
                    && damageRevision == 0 && collapseRevision == 0
                    && resolveRevision == 0)
                {
                    return 1;
                }
                if (starts == 1 && breaches == 1 && damages == 0
                    && collapses == 0
                    && resolutions == 0 && exact == 2
                    && startRevision == 1 && breachRevision == 1
                    && damageRevision == 0 && collapseRevision == 0
                    && resolveRevision == 0)
                {
                    return 2;
                }
                if (starts == 1 && breaches == 1 && damages == 1
                    && collapses == 0
                    && resolutions == 0 && exact == 3
                    && startRevision == 1 && breachRevision == 1
                    && damageRevision == 1 && collapseRevision == 0
                    && resolveRevision == 0)
                {
                    return 3;
                }
                if (starts == 1 && breaches == 1 && damages == 1
                    && collapses == 1
                    && resolutions == 0 && exact == 4
                    && startRevision == 1 && breachRevision == 1
                    && damageRevision == 1 && collapseRevision == 1
                    && resolveRevision == 0)
                {
                    return 4;
                }
                if (starts == 1 && breaches == 1 && damages == 1
                    && collapses == 1
                    && resolutions == 1 && exact == 5
                    && startRevision == 1 && breachRevision == 1
                    && damageRevision == 1 && collapseRevision == 1
                    && resolveRevision == 1)
                {
                    return 5;
                }
                return -1;
            }
        }

        private int TypeCount(MigratedProducerOutcomeKind kind)
        {
            string outcomeType = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        outcomeType,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes().exactOutcomes.Count;

        private long OwnerRevision(
            MigratedProducerOutcomeKind kind,
            string identity)
        {
            if (string.IsNullOrWhiteSpace(identity))
                return 0L;
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            string expectedKey = definition.ProducerId + "|" + identity;
            MigratedProducerOwnerRevisionSaveData owner = outcomes.Transaction
                .Capture().owners.SingleOrDefault(value => string.Equals(
                    value.ownerKey,
                    expectedKey,
                    StringComparison.Ordinal));
            return owner?.revision ?? 0L;
        }

        private DungeonInvasionSaveData CaptureInvasionSave()
        {
            var result = new DungeonInvasionSaveData
            {
                activeIntruders = director.CapturePersistentState(grid)
                    .Select(InvasionSaveRuntimeAdapter.ToIntruderSaveData)
                    .ToList()
            };
            return result;
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint
                .RestoreFamilyCommon(
                    checkpoint,
                    ownerId,
                    InvasionLifecycleOwnerKind,
                    InvasionLifecycleCausalOwnerRestoreKind,
                    outcomes.Ledger);
            RequireInvasionLifecycleFields(
                state,
                "invasion owner state",
                "schemaVersion",
                "schema",
                "stage",
                "runtimeId",
                "facilityId",
                "ownerCharacterId",
                "calendarDay",
                "invasion",
                "facilityRuntimeState",
                "facilityDamaged",
                "outcomeOwners",
                "invasionPublications");
            if ((int)state["schemaVersion"] != 2
                || !string.Equals(
                    (string)state["schema"],
                    InvasionLifecycleDomainSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["facilityId"],
                    InvasionDamageFacilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["ownerCharacterId"],
                    InvasionOwnerCharacterId,
                    StringComparison.Ordinal)
                || (int)state["calendarDay"] != InvasionAbsoluteDay
                || state["invasion"] is not JObject savedInvasion
                || state["facilityRuntimeState"]
                    is not JObject savedFacilityState
                || state["outcomeOwners"] is not JObject savedOwners
                || state["invasionPublications"]
                    is not JObject savedPublications)
            {
                throw new InvalidOperationException(
                    "Invasion checkpoint identity changed.");
            }

            runtimeId = ((string)state["runtimeId"] ?? string.Empty).Trim();
            damageTarget.Building.RestoreFacilityState(
                JsonUtility.FromJson<FacilityRuntimeState>(
                    savedFacilityState.ToString(Formatting.None)));
            damageTarget.Building.SetDamaged(
                (bool)state["facilityDamaged"]);
            outcomes.Transaction.Restore(
                outcomes.Transaction.PrepareRestore(
                    JsonUtility.FromJson<
                        DungeonMigratedProducerOutcomeSaveData>(
                            savedOwners.ToString(Formatting.None))));
            string publicationJson = savedPublications.ToString(
                Formatting.None);
            InvasionFactPublicationSaveSection.ValidateJson(publicationJson);
            InvasionFactPublicationSaveData publicationState =
                DungeonSaveJson.Deserialize<
                    InvasionFactPublicationSaveData>(publicationJson);
            DungeonInvasionSaveData invasion = JsonUtility.FromJson<
                DungeonInvasionSaveData>(
                    savedInvasion.ToString(Formatting.None));
            if (invasion?.activeIntruders == null
                || invasion.activeIntruders.Count > 1)
            {
                throw new InvalidOperationException(
                    "Invasion checkpoint contains an invalid active-intruder set.");
            }
            InvasionFactPreflight.ValidateOwners(
                publicationState,
                invasion);
            InvasionFactPreflight.ValidateJournal(
                publicationState,
                outcomes.Ledger.CaptureGameplayOutcomes(),
                outcomes.Transaction.Capture());
            outcomes.InvasionFacts.Restore(publicationState);
            var report = new DungeonGameRestoreReport();
            int restored = director.PrepareRestoreCandidates(
                invasion.activeIntruders.Select(
                    InvasionSaveRuntimeAdapter.ToRuntimeState),
                report);
            if (!report.Success || restored != invasion.activeIntruders.Count)
            {
                throw new InvalidOperationException(
                    "Invasion intruder restore failed: "
                    + string.Join(" | ", report.Errors));
            }
            director.PublishRestoreCandidates();
            director.CompleteRestoreCandidates();
            outcomes.Probe.ClearObservations();
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Invasion checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            InvasionFactPublicationSaveData publicationState =
                outcomes.InvasionFacts.Capture();
            InvasionIntruderRuntime active = ActiveIntruderOrNull();
            bool activeShape = stage is >= 1 and <= 4
                ? active != null
                    && string.Equals(
                        active.RuntimeId,
                        runtimeId,
                        StringComparison.Ordinal)
                    && active.IntruderActor != null
                    && active.IntruderActor.CurrentHealth > 0f
                : active == null;
            bool breachShape = stage switch
            {
                0 or 1 => active == null
                    || !active.HasBreachedDungeonInterior,
                2 or 3 or 4 => active != null
                    && active.HasBreachedDungeonInterior,
                5 => TypeCount(
                    MigratedProducerOutcomeKind
                        .InvasionDungeonBreachedEvent) == 1,
                _ => false
            };
            bool damageShape = stage switch
            {
                0 or 1 or 2 => !damageTarget.Building.IsDamaged
                    && (active == null || active.FacilityDamageCount == 0),
                3 or 4 => damageTarget.Building.IsDamaged
                    && active != null
                    && active.FacilityDamageCount == 1,
                5 => damageTarget.Building.IsDamaged
                    && TypeCount(
                        MigratedProducerOutcomeKind
                            .InvasionFacilityDamagedEvent) == 1,
                _ => false
            };
            bool collapseShape = stage switch
            {
                0 or 1 or 2 or 3 => TypeCount(
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent) == 0,
                4 => active != null
                    && active.State == InvasionIntruderState.FrontBroken
                    && TypeCount(
                        MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent) == 1,
                5 => TypeCount(
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent) == 1,
                _ => false
            };
            if (stage < 0
                || !activeShape
                || !breachShape
                || !damageShape
                || !collapseShape
                || (stage == 0 && runtimeId.Length != 0)
                || (stage > 0 && !runtimeId.StartsWith(
                    "invasion:",
                    StringComparison.Ordinal))
                || publicationState.observations.Count != Math.Max(stage, 0)
                || publicationState.observations.Any(value =>
                    value == null
                    || value.pending
                    || value.fact == null
                    || !string.Equals(
                        value.fact.runtimeId,
                        runtimeId,
                        StringComparison.Ordinal)
                    || value.fact.sequence
                        != publicationState.observations.IndexOf(value) + 1L)
                || !string.Equals(
                    damageTarget.Building.RequirePersistentInstanceId().Value,
                    InvasionDamageFacilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    ownerActor.Actor.Identity.PersistentId,
                    InvasionOwnerCharacterId,
                    StringComparison.Ordinal)
                || clock.AbsoluteDay != InvasionAbsoluteDay)
            {
                throw new InvalidOperationException(
                    "Invasion owner, intruder, facility and ledger state diverged: "
                    + "stage=" + stage
                    + "; runtime=" + runtimeId
                    + "; active=" + director.ActiveIntruders.Count
                    + "; breached="
                    + (active?.HasBreachedDungeonInterior ?? false)
                    + "; damageCount="
                    + (active?.FacilityDamageCount ?? 0)
                    + "; facilityDamaged="
                    + damageTarget.Building.IsDamaged);
            }
        }

        private InvasionIntruderRuntime ActiveIntruderOrNull() =>
            director.ActiveIntruders.Count == 1
                ? director.ActiveIntruders[0]
                : null;

        private InvasionIntruderRuntime RequireActiveIntruder() =>
            ActiveIntruderOrNull()
            ?? throw new InvalidOperationException(
                "The invasion owner has no single active intruder.");

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class InvasionLifecycleFixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal InvasionLifecycleFixtureSet(
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

    private sealed class CausalInvasionContext : IInvasionIntruderContext
    {
        private readonly Grid grid;
        private readonly CharacterActor owner;
        private readonly BuildableObject facility;

        internal CausalInvasionContext(
            Grid grid,
            CharacterActor owner,
            BuildableObject facility)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.facility = facility
                ?? throw new ArgumentNullException(nameof(facility));
        }

        public bool TryGetGrid(out Grid value)
        {
            value = grid;
            return true;
        }

        public bool TryGetOwner(out CharacterActor value)
        {
            value = owner;
            return true;
        }

        public bool TryResolveBuilding(
            BuildingInstanceId id,
            out BuildableObject building)
        {
            building = id.Equals(facility.RequirePersistentInstanceId())
                ? facility
                : null;
            return building != null;
        }

        public bool TryResolveEntry(out InvasionIntruderEntry entry)
        {
            entry = new InvasionIntruderEntry(
                Vector2Int.zero,
                new Vector3(-1f, 0f, 0f),
                Vector3.zero);
            return true;
        }

        public InvasionIntruderSettings ApplyRunVariables(
            InvasionIntruderSettings source) => source;
    }

    private sealed class CausalInvasionIntruderFactory :
        IInvasionIntruderFactory,
        IDisposable
    {
        private readonly IInvasionIntruderPatternDefinitionCatalog patterns;
        private readonly DefenseBreachPlanner breachPlanner = new();
        private readonly CausalStructuralIntegrity structuralIntegrity = new();
        private readonly DefenseRaidAwarenessRuntime raidAwareness;
        private readonly List<InvasionIntruderRuntime> live = new();

        internal CausalInvasionIntruderFactory(
            IInvasionIntruderPatternDefinitionCatalog patterns,
            BuildableObject facility)
        {
            this.patterns = patterns
                ?? throw new ArgumentNullException(nameof(patterns));
            raidAwareness = new DefenseRaidAwarenessRuntime(
                new CausalBuildingWorld(facility));
        }

        public InvasionIntruderRuntime Create(
            GameObject intruderPrefab,
            Vector3 position) => CreateRuntime(position);

        public InvasionIntruderRuntime CreateDetached(
            GameObject intruderPrefab,
            Vector3 position) => CreateRuntime(position);

        public void Publish(InvasionIntruderRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            runtime.gameObject.SetActive(true);
        }

        public void PublishDetached(InvasionIntruderRuntime runtime) =>
            Publish(runtime);

        public void DestroyDetached(InvasionIntruderRuntime runtime)
        {
            live.Remove(runtime);
            if (runtime != null)
                Object.DestroyImmediate(runtime.gameObject);
        }

        public InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject)
        {
            if (intruderObject == null)
                throw new ArgumentNullException(nameof(intruderObject));
            InvasionIntruderRuntime runtime =
                intruderObject.GetComponent<InvasionIntruderRuntime>()
                ?? intruderObject.AddComponent<InvasionIntruderRuntime>();
            Configure(runtime);
            if (!live.Contains(runtime))
                live.Add(runtime);
            return runtime;
        }

        public void Dispose()
        {
            for (int index = live.Count - 1; index >= 0; index--)
            {
                if (live[index] != null)
                    Object.DestroyImmediate(live[index].gameObject);
            }
            live.Clear();
        }

        private InvasionIntruderRuntime CreateRuntime(Vector3 position)
        {
            var value = new GameObject("Causal invasion intruder");
            value.transform.position = position;
            value.AddComponent<AbilityMove>();
            value.AddComponent<CharacterActor>();
            InvasionIntruderRuntime runtime =
                value.AddComponent<InvasionIntruderRuntime>();
            CharacterAiEditorTestDependencies.Inject(value);
            Configure(runtime);
            live.Add(runtime);
            return runtime;
        }

        private void Configure(InvasionIntruderRuntime runtime)
        {
            runtime.ConfigureContent(patterns);
            runtime.ConfigureTacticalServices(
                breachPlanner,
                structuralIntegrity,
                raidAwareness,
                CausalFacilityNetwork.Instance);
        }
    }

    private sealed class CausalBuildingWorld : IBuildingWorldQuery
    {
        internal CausalBuildingWorld(BuildableObject facility) =>
            Buildings = new[]
            {
                facility ?? throw new ArgumentNullException(nameof(facility))
            };

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class CausalStructuralIntegrity :
        IBuildingStructuralIntegrityRuntime
    {
        public bool TryGet(
            BuildableObject building,
            out BuildingStructuralIntegritySnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public bool IsBreachable(BuildableObject building) => false;

        public BuildingStructuralDamageResult ApplyDamage(
            BuildableObject building,
            float damage) => new(
                false,
                false,
                0f,
                default,
                "causal-fixture-no-structural-target");

        public bool TryApplyRepairWork(
            BuildableObject building,
            float workAmount,
            out bool completed,
            out BuildingStructuralIntegritySnapshot snapshot)
        {
            completed = false;
            snapshot = default;
            return false;
        }
    }

    private sealed class CausalFacilityNetwork :
        IDefenseFacilityNetworkRuntime
    {
        internal static readonly CausalFacilityNetwork Instance = new();
        public int Version => 0;

        public DefenseFacilityNetworkSnapshot GetSnapshot(
            DefenseFacility facility) => new(
                facility,
                null,
                null,
                null,
                null,
                false);

        public bool HasAutomaticControl(DefenseFacility facility) => false;
        public bool HasMaintenanceCoverage(DefenseFacility facility) => false;
        public bool HasSupplyCoverage(DefenseFacility facility) => false;
        public void DetectIntruder(
            string raidId,
            Vector2Int position,
            IDefenseRaidAwarenessRuntime awareness)
        {
        }
    }

    private sealed class CausalInvasionClock : IGameClock, IGameCalendar
    {
        private readonly float time;

        internal CausalInvasionClock(int absoluteDay)
        {
            AbsoluteDay = absoluteDay;
            time = absoluteDay * GameCalendarRules.SecondsPerDay + 10f;
        }

        internal int AbsoluteDay { get; }
        public float DeltaTime => 1f;
        public float Time => time;
        public int FrameCount => 0;
        public bool IsPaused => false;
        public int Day => AbsoluteDay;
        public int Hour => 1;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public long AbsoluteHour => Current.AbsoluteHour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public float ElapsedSeconds => 10f;
        public TimeOfDay TimeOfDay => TimeOfDay.Night;
        public bool IsRunning => false;
        public CalendarDateTime GetRegionalTime(int offset) => GameCalendarRules.ProjectRegional(Day, Hour, offset);
        public void Start() => throw new InvalidOperationException("The causal invasion fixture clock is fixed.");
        public void SetDateTime(int day, int hour) => throw new InvalidOperationException("The causal invasion fixture clock is fixed.");
    }

    private sealed class EmptyOffenseWorldSimulation : IOffenseWorldSimulation
    {
        public int WorldSeed => 0;
        public int WorldDay => 1;
        public float WorldHour => 0f;
        public OffenseHexCoord DungeonCoord => new(0, 0);
        public IReadOnlyCollection<OffenseHexTileState> Tiles =>
            Array.Empty<OffenseHexTileState>();
        public IReadOnlyCollection<OffenseWorldSiteStateData> Sites =>
            Array.Empty<OffenseWorldSiteStateData>();
        public IReadOnlyCollection<OffenseUrgentSiteStateData> UrgentSites =>
            Array.Empty<OffenseUrgentSiteStateData>();
        public event Action Changed
        {
            add { }
            remove { }
        }

        public void Initialize(int runSeed) { }
        public void AdvanceHours(float hours) { }
        public bool TryGetTile(
            OffenseHexCoord coord,
            out OffenseHexTileState tile)
        {
            tile = null;
            return false;
        }
        public bool TryGetSite(
            string siteId,
            out OffenseWorldSiteStateData site)
        {
            site = null;
            return false;
        }
        public bool TryGetUrgentSite(
            string siteId,
            out OffenseUrgentSiteStateData site)
        {
            site = null;
            return false;
        }
        public bool TryRevealSite(string siteId) => false;
        public bool TryEngageSite(string siteId) => false;
        public bool TryResolveSite(string siteId) => false;
        public bool TryRegisterStrategicSite(OffenseWorldSiteStateData site) =>
            false;
        public bool TrySpawnUrgentSite(
            string definitionId,
            OffenseHexCoord coord,
            out string siteId)
        {
            siteId = string.Empty;
            return false;
        }
        public bool TryMitigateUrgentSite(string siteId, float amount) => false;
        public bool TryDestroyUrgentSite(string siteId) => false;
        public bool TryFindPath(
            OffenseHexCoord start,
            OffenseHexCoord goal,
            OffenseTravelProfile profile,
            out IReadOnlyList<OffenseHexCoord> path,
            out float totalCost)
        {
            path = Array.Empty<OffenseHexCoord>();
            totalCost = 0f;
            return false;
        }
        public int GetMinimumStepDistance(
            OffenseHexCoord start,
            OffenseHexCoord goal) => start.DistanceTo(goal);
        public OffenseWorldSaveData Capture() => new();
        public void Restore(OffenseWorldSaveData snapshot) { }
    }

    private sealed class EmptyCharacterLifeDefinitions :
        ICharacterLifeDefinitionCatalog
    {
        internal static readonly EmptyCharacterLifeDefinitions Instance = new();

        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) =>
            throw new InvalidOperationException(
                "The G06 fixture has no character-life records.");
        public AgeConditionDefinition RequireAgeCondition(
            string conditionId) =>
            throw new InvalidOperationException(
                "The G06 fixture has no age conditions.");
        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(
            bool construct) => Array.Empty<AgeConditionDefinition>();
    }

    private static void RequireInvasionLifecycleFields(
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
}
#endif
