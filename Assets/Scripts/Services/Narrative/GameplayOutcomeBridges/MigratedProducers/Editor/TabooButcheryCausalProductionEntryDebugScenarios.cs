#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class TabooButcheryCausalProductionEntryDebugScenarios
{
    internal const string OwnerKind = "owner-kind:taboo-butchery";
    internal const string RestoreKind = "owner-family:taboo-butchery";
    internal const string ControlledDeathActionId = "action:invoke-controlled-starvation-death";
    internal const string ButcherCorpseActionId = "action:butcher-authorized-humanoid-corpse";
    private const string Schema = "dungeonstory-causal-taboo-butchery-owner-state@1";
    private const string FocusedOwner = "taboo-owner:focused";
    private const int Day = 7;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId, string ownerId = null, JObject ownerCheckpoint = null)
    {
        CausalGameplaySyntax.RequireId(causalRunId, nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        Require(persistent || ownerCheckpoint == null, "Owner checkpoint requires a persistent binding.");
        var context = new Context(persistent ? ownerId : "taboo-butchery:" + causalRunId, ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                ControlledDeathActionId, "survival", MigratedProducerOutcomeKind.CharacterDeathEvent,
                context.CaptureState, context.Kill,
                () => context.RequireExact(MigratedProducerOutcomeKind.CharacterDeathEvent),
                context.CaptureOutcomeEvidence, lifetime.Release,
                persistent ? ownerId : null, persistent ? OwnerKind : null,
                persistent ? context.Checkpoint : null,
                persistent ? RestoreKind : null, persistent ? context : null,
                new[] { MigratedProducerOutcomeKind.HealthThresholdCrossedEvent },
                () => new[] { context.RequireExact(MigratedProducerOutcomeKind.HealthThresholdCrossedEvent) },
                new[] { "atomic-sibling" }),
            new DelegateProductionCausalActionFixture(
                ButcherCorpseActionId, "survival", MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
                context.CaptureState, context.Butcher,
                () => context.RequireExact(MigratedProducerOutcomeKind.PhysicalItemTransformReceipt),
                context.CaptureOutcomeEvidence, lifetime.Release,
                persistent ? ownerId : null, persistent ? OwnerKind : null,
                persistent ? context.Checkpoint : null,
                persistent ? RestoreKind : null, persistent ? context : null,
                new[] { MigratedProducerOutcomeKind.CharacterTabooIncidentEvent },
                () => new[] { context.RequireExact(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent) },
                new[] { "atomic-sibling" })
        };
    }

    public static void RunBatchFromEnvironment()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_TABOO_OWNER_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output), "Create-only owner output is required.");
        Directory.CreateDirectory(output);
        JObject initial, first, terminal;
        using (var context = new Context(FocusedOwner))
        {
            initial = context.Checkpoint();
            WriteNew(Path.Combine(output, "initial-checkpoint.json"), initial);
            Require(context.Kill(), "Real character death failed.");
            first = context.Checkpoint();
            WriteNew(Path.Combine(output, "first-checkpoint.json"), first);
        }
        using (var context = new Context(FocusedOwner, Copy(first)))
        {
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(first, context.Checkpoint());
            Require(context.Butcher(), "Restored corpse did not complete real butchery.");
            terminal = context.Checkpoint();
            WriteNew(Path.Combine(output, "terminal-checkpoint.json"), terminal);
        }
        using (var context = new Context(FocusedOwner, Copy(terminal)))
        {
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(terminal, context.Checkpoint());
            JObject restoredBeforeReplay = context.Checkpoint();
            Require(!context.Kill() && !context.Butcher(), "Terminal owner replayed a consumed corpse.");
            Require(JToken.DeepEquals(restoredBeforeReplay, context.Checkpoint()), "Terminal replay mutated owner state.");
        }
        WriteNew(Path.Combine(output, "direct-report.json"), new JObject
        {
            ["status"] = "PASS_DIRECT_CONTINUITY", ["initial"] = initial,
            ["first"] = first, ["terminal"] = terminal,
            ["deathWasProductionCommand"] = true, ["corpseWasProductionObserver"] = true,
            ["controlledSimulation"] = true, ["naturalPlayTelemetry"] = false,
            ["wholeWorldSave"] = false, ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false, ["releaseGate"] = "REJECT_BEFORE_DPO"
        });
        Debug.Log("TABOO_OWNER_DIRECT=PASS; death=2; butchery=2; restores=2");
    }

    public static void RunFreshFromEnvironment()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_TABOO_OWNER_OUTPUT");
        string input = Environment.GetEnvironmentVariable("DUNGEONSTORY_TABOO_OWNER_INPUT");
        Require(!string.IsNullOrWhiteSpace(output) && !string.IsNullOrWhiteSpace(input), "Fresh paths required.");
        Directory.CreateDirectory(output);
        JObject first = JObject.Parse(File.ReadAllText(input));
        using var context = new Context(FocusedOwner, first);
        ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(first, context.Checkpoint());
        Require(context.Stage == 1 && context.Butcher(), "Fresh-process restored butchery failed.");
        WriteNew(Path.Combine(output, "fresh-report.json"), new JObject
        {
            ["status"] = "PASS_FRESH_CONTINUITY", ["terminal"] = context.Checkpoint(),
            ["wholeWorldSave"] = false, ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false, ["releaseGate"] = "REJECT_BEFORE_DPO"
        });
        Debug.Log("TABOO_OWNER_FRESH=PASS");
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly CustomerAiDebugScenarios.CustomerAiScenarioWorld world;
        private readonly List<CharacterActor> actors = new();
        private readonly GameEventBus events = new();
        private readonly CharacterVitalsActivityObserver vitalsActivities;
        private readonly DomainFactDispatcher primitiveFacts;
        private readonly DomainFactDispatcher physicalFacts;
        private readonly PrimitiveSurvivalActivityObserver primitiveActivities;
        private readonly WildlifeCarcassActivityObserver wildlifeActivities;
        private readonly Clock clock = new();
        private readonly Calendar calendar = new();
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly CharacterBodyHealthRuntime health;
        private readonly WorldItemStackRuntime items;
        private readonly WildlifeCarcassService carcasses;
        private readonly CharacterDeprivationRuntime deprivation;
        private readonly WorldFilthRuntime filth;
        private readonly WorldWaterRuntime water;
        private readonly CharacterActor victim, butcher, witness;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly ControlledIds ids;
        private readonly DomainFactDispatcher notifications = new(
            new IDomainFactObserverBinding[]
            {
                new CharacterDeprivationExperienceFactBinding()
            });
        private bool disposed;
        private string VictimId => "character:world:taboo-victim:" + ownerId;
        // Production initialization reserves this stable ID for the dungeon owner.
        private string ButcherId => "owner";
        private string WitnessId => "character:world:taboo-witness:" + ownerId;
        private static string Type(MigratedProducerOutcomeKind kind) => MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId.Value;
        private IReadOnlyList<GameplayOutcomeSnapshot> Exact => outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes;
        internal int Stage => Exact.All(value => value.outcomeTypeId != Type(MigratedProducerOutcomeKind.CharacterDeathEvent))
            ? 0 : Exact.Any(value => value.outcomeTypeId == Type(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent)) ? 2 : 1;

        internal Context(string ownerId, JObject checkpoint = null)
        {
            this.ownerId = CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            ids = new ControlledIds(ownerId);
            outcomes = new MigratedProducerOutcomeEditorFixture(ProductionCausalOwnerCheckpoint.LedgerRunId(ownerId, OwnerKind));
            probe = new MigratedProducerOutcomeProbeTransaction(outcomes.Transaction, outcomes.Recorder);
            primitiveFacts = outcomes.CreatePrimitiveSurvivalFacts(probe);
            primitiveActivities = new PrimitiveSurvivalActivityObserver(events);
            primitiveActivities.Start();
            var commits = new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger);
            notifications.Subscribe(
                "log:character.fatality",
                new MigratedProducerDomainFactProbeObserver<CharacterFatalityFact>(
                    CharacterFatalityFactMapping.CaptureDeath,
                    outcomes.Transaction,
                    commits,
                    probe));
            notifications.Subscribe(
                "log:character.fatal-critical-transition",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterFatalCriticalTransitionFact>(
                    CharacterFatalityFactMapping.CaptureCritical,
                    outcomes.Transaction,
                    commits,
                    probe));
            notifications.Subscribe(
                "log:character.fatal-downed",
                new MigratedProducerDomainFactProbeObserver<CharacterFatalDownedFact>(
                    CharacterFatalityFactMapping.CaptureDowned,
                    outcomes.Transaction,
                    commits,
                    probe));
            world = new CustomerAiDebugScenarios.CustomerAiScenarioWorld();
            try
            {
                health = new CharacterBodyHealthRuntime(new UnityRealtimeClock(), CharacterAiEditorTestDependencies.WorldRegistry, clock, events,
                    new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                    new ResourceAnatomyProfileCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader())),
                    outcomes.AggregateRootStore,
                    calendar);
                vitalsActivities = new CharacterVitalsActivityObserver(
                    events,
                    CharacterAiEditorTestDependencies.WorldRegistry);
                vitalsActivities.Initialize();
                butcher = CreateActor(ButcherId, "Orc", new Vector2Int(0, 0), CharacterRole.Owner);
                witness = CreateActor(WitnessId, "Slime", new Vector2Int(1, 0), staff: true);
                if (checkpoint == null || (int?)checkpoint["domainState"]?["stage"] == 0)
                    victim = CreateActor(VictimId, "Orc", new Vector2Int(0, 0));
                health.ConfigureFactPublication(notifications, notifications);
                var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
                var itemCatalog = new ResourceItemDefinitionCatalog(content);
                items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    new ResourceDungeonItemCatalogProvider(itemCatalog), outcomes.AggregateRootStore,
                    out WorldItemRepository repository, out _, out _, out _, out _, out _, out _, ids);
                physicalFacts = outcomes.CreatePhysicalTransformationFacts(probe);
                var transforms = new PhysicalItemTransformService(repository,
                    new WorldItemSpawner(items.CatalogProvider, repository, EditorNullItemMarkerPresenter.Instance),
                    items.MassQuery, items.CatalogProvider, EditorNullItemMarkerPresenter.Instance,
                    new FixedGameSessionStateProvider(Day), physicalFacts, clock: new DungeonStory.Foundation.UnityUiClock());
                carcasses = new WildlifeCarcassService(items, transforms,
                    new ResourceWildlifeSpeciesCatalogProvider(content, itemCatalog), events);
                wildlifeActivities = new WildlifeCarcassActivityObserver(events);
                wildlifeActivities.Start();
                carcasses.ConstructFactPublisher(calendar, physicalFacts);
                deprivation = TabooButcheryCausalTestPorts.Create(world.Grid, items, health,
                    outcomes.AggregateRootStore, events, clock, calendar, primitiveFacts, notifications,
                    out filth, out water);
                if (checkpoint != null) Restore(checkpoint);
                Validate();
            }
            catch { Dispose(); throw; }
        }

        private CharacterActor CreateActor(string id, string species, Vector2Int position,
            CharacterRole role = CharacterRole.Regular, bool staff = false)
        {
            var go = new GameObject("Causal taboo " + id);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<AbilityMove>();
            go.AddComponent<AbilityShopping>();
            if (staff || role == CharacterRole.Owner) go.AddComponent<AbilityWork>();
            go.AddComponent<CharacterCarryInventory>();
            var brain = go.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();
            var actor = go.AddComponent<CharacterActor>();
            actors.Add(actor);
            CharacterAiEditorTestDependencies.Inject(go, scheduling: null, gameClock: clock,
                pathSearchBroker: null, frameWorkBudget: null);
            Invoke(typeof(CharacterActor), "Awake", actor);
            actor.Identity.SetPersistentId(id);
            go.transform.position = world.Grid.GetWorldPos(position);
            CharacterSO definition = staff
                ? new ResourceGameContentCatalog(new UnityGameContentRootLoader()).GetAll<CharacterSO>()
                    .Single(value => value.characterType == CharacterType.NPC && value.role == CharacterRole.Regular
                        && string.Equals(value.SpeciesTag, species, StringComparison.OrdinalIgnoreCase))
                : CharacterAiEditorTestDependencies.RequireAuthoredCharacterDefinition(species, role);
            actor.Initialization(definition);
            Require(actor.Identity.PersistentId == id, "Authored actor initialization changed the expected canonical identity.");
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Stats.ConstructCharacterVitals(new CharacterStatsVitalsService(health, health, events,
                new CharacterDeathEventFactory(CharacterAiEditorTestDependencies.WorldRegistry, calendar),
                TabooButcheryCausalTestPorts.Guard<IOwnerRunLifecycleService>()));
            actor.Stats.RecalculateVitals(resetCurrentHealth: true);
            actor.stats[CharacterCondition.HYGIENE] = 75f;
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(actor);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacterLifetime(actor);
            return actor;
        }

        internal bool Kill()
        {
            if (Stage != 0) return false;
            victim.Die(CharacterDeathCauseCode.Starvation, "survival:causal-starvation");
            Require(victim.IsDead && Stage == 1, "Death command did not durably kill the victim.");
            WorldItemStackSnapshot corpse = items.GetAllStacks().Single(value => value.ItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId);
            Require(corpse.SourceCharacterId == VictimId && !corpse.EmergencyButcheryAllowed,
                "Real death observer lost corpse provenance or pre-authorized butchery.");
            int count = Exact.Count;
            Require(items.SetEmergencyButcheryAllowed(corpse.StackId, true) && Exact.Count == count,
                "Permission command failed or fabricated an outcome.");
            Validate();
            return true;
        }

        internal bool Butcher()
        {
            if (Stage != 1) return false;
            Require(carcasses.TryButcherNextCarcass(butcher.BuildingVisitor, null, out int produced, out string reason)
                && produced == 6, "Production butchery: " + reason);
            Validate();
            return Stage == 2;
        }

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            Validate();
            IReadOnlyList<WorldItemStackSnapshot> physical = items.GetAllStacks();
            WorldItemStackSnapshot corpse = physical.SingleOrDefault(value =>
                value.ItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId);
            DungeonCharacterBodyHealthSaveData body = health.Capture();
            float victimHealth = body.characters.Single(value => value.characterId == VictimId).currentHealth;
            DungeonDarkSurvivalSaveData survival = deprivation.Capture();
            int tabooMemoryCount = survival.characters
                .SingleOrDefault(value => value.characterId == ButcherId)?.tabooMemories?.Count ?? 0;
            float bloodAmount = survival.filth
                .Where(value => value.type == WorldFilthType.Blood)
                .Sum(value => value.amount);
            float butcherGuilt = butcher.Stats.GetMoodSnapshot().Factors
                .SingleOrDefault(value => value.Id == "mood:butchery-guilt")?.Value ?? 0f;
            float witnessMood = witness.Stats.GetMoodSnapshot().Factors
                .SingleOrDefault(value => value.Id == "survival:witness:" + ButcherId)?.Value ?? 0f;
            JObject witnessState = CaptureActor(witness);
            int witnessRumorCount = ((JArray)witnessState["socialMemory"]?["recentRumors"] ?? new JArray()).Count;
            int witnessFactCount = witness.Progression.NarrativeLedger.Facts.Count(value =>
                value.factId == "survival/taboo-witness" && value.subjectId == ButcherId);
            JObject sequence = ids.Capture();
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["tabooButchery.ownerId"] = ownerId,
                ["tabooButchery.stage"] = Stage,
                ["tabooButchery.absoluteDay"] = Day,
                ["tabooButchery.butcherId"] = ButcherId,
                ["tabooButchery.butcherName"] = butcher.Identity.DisplayName,
                ["tabooButchery.butcherSpecies"] = butcher.SpeciesTag,
                ["tabooButchery.victimId"] = VictimId,
                ["tabooButchery.victimName"] = CharacterAiEditorTestDependencies
                    .RequireAuthoredCharacterDefinition("Orc").characterName,
                ["tabooButchery.victimSpecies"] = "Orc",
                ["tabooButchery.victimHealth"] = StateNumber(victimHealth),
                ["tabooButchery.victimDead"] = Stage > 0,
                ["tabooButchery.witnessId"] = WitnessId,
                ["tabooButchery.witnessName"] = witness.Identity.DisplayName,
                ["tabooButchery.witnessSpecies"] = witness.SpeciesTag,
                ["tabooButchery.corpseStackId"] = corpse?.StackId ?? string.Empty,
                ["tabooButchery.corpseQuantity"] = corpse?.Quantity ?? 0,
                ["tabooButchery.corpseSourceId"] = corpse?.SourceCharacterId ?? string.Empty,
                ["tabooButchery.corpseSourceName"] = corpse?.SourceDisplayName ?? string.Empty,
                ["tabooButchery.corpseSourceSpecies"] = corpse?.SourceSpeciesTag ?? string.Empty,
                ["tabooButchery.corpseDeathReason"] = corpse?.SourceDeathReason ?? string.Empty,
                ["tabooButchery.corpseAuthorized"] = corpse?.EmergencyButcheryAllowed ?? false,
                ["tabooButchery.bloodAmount"] = StateNumber(bloodAmount),
                ["tabooButchery.meatQuantity"] = Quantity(physical, DarkSurvivalItemDefinitions.HumanoidMeatItemId),
                ["tabooButchery.boneQuantity"] = Quantity(physical, DarkSurvivalItemDefinitions.BoneItemId),
                ["tabooButchery.butcherHygiene"] = StateNumber(
                    butcher.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f)),
                ["tabooButchery.butcherGuilt"] = StateNumber(butcherGuilt),
                ["tabooButchery.butcherTabooMemoryCount"] = tabooMemoryCount,
                ["tabooButchery.witnessMood"] = StateNumber(witnessMood),
                ["tabooButchery.witnessFactCount"] = witnessFactCount,
                ["tabooButchery.witnessRumorCount"] = witnessRumorCount,
                ["tabooButchery.exactOutcomeCount"] = Exact.Count,
                ["tabooButchery.physicalStackCursor"] = (int)sequence["stacks"]
            };
        }

        internal GameplayOutcomeReplayIdentity RequireExact(MigratedProducerOutcomeKind kind)
        {
            GameplayOutcomeSnapshot exact = Exact.Single(value => value.outcomeTypeId == Type(kind));
            var key = new GameplayResultKey(exact.producerId, new GameplayOperationId(exact.operationId),
                exact.commitRevision, exact.localResultIndex);
            Require(outcomes.Ledger.TryGetResultIdentity(key, out GameplayOutcomeReplayIdentity identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash, "Canonical taboo outcome identity is missing.");
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        private static int Quantity(IEnumerable<WorldItemStackSnapshot> stacks, string itemId) =>
            stacks.Where(value => value.ItemId == itemId).Sum(value => value.Quantity);

        internal JObject Checkpoint()
        {
            Validate();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(ownerId, OwnerKind, RestoreKind, Domain(), outcomes.Ledger);
        }

        internal void ConfigureFault(MigratedProducerOutcomeKind kind, MigratedProducerOutcomeFaultMode fault)
        { probe.TargetKind = kind; probe.FaultMode = fault; }

        internal JObject RejectionState()
        {
            JObject domain = Domain();
            // Reservation revisions and ID cursors are monotonic diagnostics, not
            // compensated gameplay state. Report them separately, never reset them.
            domain.Remove("migratedOutcomeOwners"); domain.Remove("idSequence");
            domain["exactOutcomes"] = JArray.FromObject(Exact);
            if (victim != null) domain["transientVictim"] = CaptureActor(victim);
            return domain;
        }

        internal void TestDatedMoodBoundary()
        {
            JObject before = Checkpoint();
            Action[] rejects =
            {
                () => butcher.ApplyMoodFactor("qa:negative-actor", "test", 3f, 60f, 1, -1),
                () => butcher.ApplyResolvedMoodFactor("qa:negative-resolved", "test", 3f, 60f, 1, true, -1),
                () => butcher.Stats.ApplyResolvedMoodFactor("qa:negative-stats", "test", 3f, 60f, 1, true, -1),
                () => ((CharacterMoodPolicyService)Field(typeof(CharacterActor), "moodPolicy").GetValue(butcher))
                    .ApplySeconds(butcher, "qa:negative-policy", 3f, 60f, "test", 1, -1)
            };
            foreach (Action action in rejects)
            {
                bool rejected = false;
                try { action(); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected && JToken.DeepEquals(before, Checkpoint()), "Negative mood date mutated or was accepted.");
            }
            butcher.ApplyMoodFactor("qa:explicit-day", "test", 3f, 60f, 1, 11);
            butcher.ApplyMoodFactor("qa:legacy-day", "test", 3f, 60f, 1);
            Require(butcher.Progression.NarrativeLedger.Facts.Single(value => value.factId == "qa:explicit-day").lastDay == 11
                && butcher.Progression.NarrativeLedger.Facts.Single(value => value.factId == "qa:legacy-day").lastDay == 0,
                "Explicit or legacy mood date contract changed.");
        }

        private JObject Domain() => new()
        {
            ["schema"] = Schema, ["schemaVersion"] = 1, ["stage"] = Stage, ["day"] = Day,
            ["idSequence"] = ids.Capture(),
            ["actors"] = new JArray(actors.Where(CharacterWorldPersistenceRules.IsPersistentActor)
                .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal).Select(CaptureActor)),
            ["physical"] = Dto(items.Capture()), ["bodyHealth"] = Dto(health.Capture()),
            ["deprivation"] = Dto(deprivation.Capture()),
            ["migratedOutcomeOwners"] = Dto(outcomes.Transaction.Capture())
        };

        private JObject CaptureActor(CharacterActor actor) => Dto((DungeonCharacterSaveData)
            Invoke(typeof(CharacterWorldSaveService), "CaptureActor", null, world.Grid, actor));

        private void Restore(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(checkpoint, ownerId, OwnerKind, RestoreKind, outcomes.Ledger);
            RequireFields(state, "schema", "schemaVersion", "stage", "day", "idSequence", "actors", "physical", "bodyHealth", "deprivation", "migratedOutcomeOwners");
            Require((string)state["schema"] == Schema && (int)state["schemaVersion"] == 1
                && (int)state["day"] == Day && (int)state["stage"] == Stage, "Owner schema/day/stage differs.");
            var rows = (JArray)state["actors"];
            Require(rows.Count == actors.Count(CharacterWorldPersistenceRules.IsPersistentActor), "Persistent actor count changed.");
            foreach (JToken row in rows)
            {
                var dto = Read<DungeonCharacterSaveData>(row);
                CharacterActor actor = actors.Single(value => value.Identity.PersistentId == dto.persistentId);
                Require(dto.dataId == actor.Identity.Data.id && dto.displayName == actor.Identity.DisplayName
                    && dto.characterType == actor.Identity.CharacterType && dto.role == actor.Identity.Data.role && dto.isOwner == actor.IsOwner,
                    "Authored actor identity changed.");
                Require(dto.acquiredTraits != null && !dto.acquiredTraits.HasPersistentData,
                    "Acquired trait authorities are not part of this owner.");
                var report = new DungeonGameRestoreReport();
                Invoke(typeof(CharacterWorldRestorePayloadValidator), "ValidateActor", null, world.Grid, dto,
                    new Dictionary<int, CharacterSO> { [dto.dataId] = actor.Identity.Data }, report, false);
                Require(report.Success, "Actor restore: " + string.Join("; ", report.Errors));
                Invoke(typeof(CharacterWorldSaveService), "ApplyActorState", null, world.Grid, actor, dto);
            }
            items.Restore(Read<DungeonPhysicalItemSaveData>(state["physical"]));
            ids.Restore((JObject)state["idSequence"]);
            health.PublishRestore(health.PrepareRestore(Read<DungeonCharacterBodyHealthSaveData>(state["bodyHealth"])));
            deprivation.PublishRestoreCandidate(deprivation.BuildRestoreCandidate(Read<DungeonDarkSurvivalSaveData>(state["deprivation"])));
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(Read<DungeonMigratedProducerOutcomeSaveData>(state["migratedOutcomeOwners"])));
            Validate();
            JObject actual = Domain();
            string mismatch = state.Properties().FirstOrDefault(property => !JToken.DeepEquals(property.Value, actual[property.Name]))?.Name;
            Require(mismatch == null, "Canonical taboo owner did not round-trip: " + mismatch
                + "; expected=" + state[mismatch ?? "schema"] + "; actual=" + actual[mismatch ?? "schema"]);
        }

        private void Validate()
        {
            int stage = Stage;
            // Kill changes aggregate HP, not the intact authored anatomical nodes;
            // the separate anatomy-based downed condition is not crossed here.
            string[] expectedTypes = stage == 0 ? Array.Empty<string>() : stage == 1
                ? new[] { Type(MigratedProducerOutcomeKind.CharacterDeathEvent), Type(MigratedProducerOutcomeKind.HealthThresholdCrossedEvent) }
                : new[] { Type(MigratedProducerOutcomeKind.CharacterDeathEvent), Type(MigratedProducerOutcomeKind.HealthThresholdCrossedEvent),
                    Type(MigratedProducerOutcomeKind.PhysicalItemTransformReceipt), Type(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent) };
            Require(Exact.Select(value => value.outcomeTypeId).OrderBy(value => value).SequenceEqual(expectedTypes.OrderBy(value => value)),
                "Unexpected exact outcomes: " + string.Join(",", Exact.Select(value => value.outcomeTypeId)));
            Require(Exact.All(value => value.absoluteDay == Day), "Exact outcome calendar day differs.");
            var body = health.Capture().characters;
            Require(body.Count == 3 && body.Select(value => value.characterId).OrderBy(value => value)
                .SequenceEqual(new[] { ButcherId, VictimId, WitnessId }.OrderBy(value => value)),
                "Canonical health owner/tombstone identities differ.");
            var victimHealth = body.Single(value => value.characterId == VictimId);
            Require(victimHealth.currentHealth == (stage == 0 ? victimHealth.maxHealth : 0f),
                "Actual victim HP or saved death tombstone is missing.");
            var physical = items.GetAllStacks();
            if (stage == 0) Require(physical.Count == 0 && victim != null && !victim.IsDead, "Initial actor/physical state invalid.");
            else if (stage == 1)
            {
                Require(physical.Count == 1 && physical[0].ItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId
                    && physical[0].Quantity == 1 && physical[0].SourceCharacterId == VictimId && physical[0].EmergencyButcheryAllowed
                    && physical[0].SourceSpeciesTag == "Orc"
                    && physical[0].SourceDisplayName == CharacterAiEditorTestDependencies.RequireAuthoredCharacterDefinition("Orc").characterName,
                    "Saved corpse/permission is not the actual death result.");
            }
            else
            {
                Require(Exact.Single(value => value.outcomeTypeId == Type(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent))
                    .participants.Single().displayText == butcher.Identity.DisplayName, "Taboo participant lost canonical character display name.");
                Require(physical.Count == 2 && physical.Single(value => value.ItemId == DarkSurvivalItemDefinitions.HumanoidMeatItemId).Quantity == 4
                    && physical.Single(value => value.ItemId == DarkSurvivalItemDefinitions.BoneItemId).Quantity == 2,
                    "Butchery outputs differ from actual recipe.");
                Require(deprivation.Capture().characters.Single(value => value.characterId == ButcherId).tabooMemories.Count == 1,
                    "Real taboo observer did not persist the butcher memory.");
                Require(butcher.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f) == 57f,
                    "Butcher hygiene does not match actual consequence.");
                Require(witness.Progression.NarrativeLedger.Facts.Any(value => value.factId == "survival/taboo-witness" && value.subjectId == ButcherId),
                    "Nearby living witness did not receive the real observer.");
                Require(butcher.Progression.NarrativeLedger.Facts.Single(value => value.factId == "survival/taboo").lastDay == Day
                    && witness.Progression.NarrativeLedger.Facts.Single(value => value.factId == "survival/taboo-witness").lastDay == Day
                    && witness.Progression.NarrativeLedger.Facts.Single(value => value.factId == "survival:witness:" + ButcherId).lastDay == Day,
                    "Taboo or witness narrative lost its actual calendar day.");
                JObject witnessState = CaptureActor(witness);
                Require(((JArray)witnessState["moodFactors"]).Any(value => (string)value["id"] == "survival:witness:" + ButcherId
                    && (float)value["value"] < 0f && (float)value["remainingSeconds"] > 0f), "Witness mood was not persisted.");
                Require(((JArray)witnessState["socialMemory"]["recentRumors"]).Any(value => (string)value["sourceActorId"] == WitnessId
                    && (string)value["targetCharacterId"] == ButcherId && (string)value["source"] == "direct-character-experience"
                    && (float)value["sentiment"] < 0f && (float)value["remainingSeconds"] == 0f), "Permanent direct witness relationship missing.");
                Require(((JArray)witnessState["socialMemory"]["characterSentiments"]).Count == 1,
                    "Witness sentiment aggregate missing.");
            }
            if (stage > 0) Require(deprivation.Capture().filth.Count == 1
                && deprivation.Capture().filth[0].type == WorldFilthType.Blood
                && deprivation.Capture().filth[0].amount == 12f
                && deprivation.Capture().filth[0].sourceCharacterId == string.Empty,
                "Real death observer blood state or canonical nonpersistent source normalization missing.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            vitalsActivities?.Dispose();
            primitiveActivities?.Dispose();
            wildlifeActivities?.Dispose();
            deprivation?.Dispose();
            physicalFacts?.Dispose();
            primitiveFacts?.Dispose();
            notifications.Dispose();
            if (filth != null)
            {
                var targets = Field(typeof(WorldFilthRuntime), "workTargets").GetValue(filth) as IDictionary;
                foreach (Object target in (targets?.Values.Cast<Object>() ?? Array.Empty<Object>()).ToArray())
                    if (target is Component component && component != null) Object.DestroyImmediate(component.gameObject);
                var root = Field(typeof(WorldFilthRuntime), "visualRoot").GetValue(filth) as GameObject;
                if (root != null) Object.DestroyImmediate(root);
            }
            foreach (CharacterActor actor in actors)
            {
                if (actor == null) continue;
                CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(actor);
                CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacterLifetime(actor);
                Object.DestroyImmediate(actor.gameObject);
            }
            world?.Dispose();
        }
    }

    internal static double StateNumber(float value) => double.Parse(
        value.ToString("G9", System.Globalization.CultureInfo.InvariantCulture),
        System.Globalization.CultureInfo.InvariantCulture);

    // Controlled simulation identity source, not a replacement for a game rule.
    // Its cursor is checkpointed so a fresh process issues the same physical IDs.
    private sealed class ControlledIds : IPersistentIdGenerator
    {
        private readonly string scope;
        private int instances, stacks;
        internal ControlledIds(string scope) => this.scope = scope;
        public ItemInstanceId NewItemInstanceId() => new("item-instance:causal-taboo:" + scope + ":" + (++instances).ToString("D4"));
        public ItemStackId NewItemStackId() => new("stack:causal-taboo:" + scope + ":" + (++stacks).ToString("D4"));
        public CharacterId NewCharacterId() => throw new NotSupportedException("Actor identities are supplied.");
        public BuildingInstanceId NewBuildingInstanceId() => throw new NotSupportedException("Building identity is outside this owner.");
        public WildlifeHabitatPatchId NewWildlifeHabitatPatchId() => throw new NotSupportedException("Habitat identity is outside this owner.");
        internal JObject Capture() => new() { ["instances"] = instances, ["stacks"] = stacks };
        internal void Restore(JObject state)
        {
            RequireFields(state, "instances", "stacks");
            Require(state["instances"].Type == JTokenType.Integer && state["stacks"].Type == JTokenType.Integer,
                "Identity cursor must contain integers.");
            instances = (int)state["instances"]; stacks = (int)state["stacks"];
            Require(instances >= 0 && stacks >= 0, "Negative identity cursor.");
        }
    }

    private sealed class Clock : IGameClock
    { public float DeltaTime => 0f; public float Time => Day * GameCalendarRules.SecondsPerDay; public int FrameCount => 0; public bool IsPaused => false; }
    private sealed class Calendar : IGameCalendar
    {
        public int Day => TabooButcheryCausalProductionEntryDebugScenarios.Day;
        public int Hour => 0; public int Year => 1; public int DayOfYear => Day; public Season Season => default;
        public int DayOfSeason => Day; public long AbsoluteHour => Day * 24L; public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => default; public bool IsRunning => false; public CalendarDateTime Current => default;
        public CalendarDateTime GetRegionalTime(int offset) => throw new NotSupportedException();
        public void Start() => throw new NotSupportedException();
        public void SetDateTime(int day, int hour) => throw new NotSupportedException();
    }
    private static JObject Dto(object value) => JObject.Parse(JsonUtility.ToJson(value));
    private static T Read<T>(JToken value)
    {
        T result = JsonConvert.DeserializeObject<T>(value.ToString(Formatting.None), new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        Require(JToken.DeepEquals(value, Dto(result)), "Incomplete or changed canonical DTO: " + typeof(T).Name);
        return result;
    }
    private static JObject Copy(JObject value) => JObject.Parse(value.ToString(Formatting.None));
    private static void RequireFields(JObject value, params string[] names) => Require(value.Properties().Select(p => p.Name).OrderBy(n => n)
        .SequenceEqual(names.OrderBy(n => n)), "Unknown/missing owner field.");
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Editor binding missing: " + type.Name + "." + name);
    private static object Invoke(Type type, string name, object target, params object[] args)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Production method missing: " + type.Name + "." + name);
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
    private static void WriteNew(string path, JToken value)
    { using var stream = new FileStream(path, FileMode.CreateNew); using var writer = new StreamWriter(stream); writer.Write(value.ToString(Formatting.Indented)); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
