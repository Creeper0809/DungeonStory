#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    internal const string CombatLifeCycleOwnerRestoreKind =
        "owner-family:combat-life-cycle";
    internal const string NonlethalCombatInjuryActionId =
        "action:receive-nonlethal-combat-injury";
    internal const string RecoverBodyHealthActionId =
        "action:recover-body-health";
    internal const string FatalCombatInjuryActionId =
        "action:receive-fatal-combat-injury";

    private const string CombatLifeCycleOwnerKind =
        "owner-kind:combat-life-cycle";
    private const string CombatLifeCycleDomainStateSchema =
        "dungeonstory-causal-combat-life-cycle-owner-state@4";

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalCombatLifeCycleFixtures(
            string causalRunId,
            string protagonistId,
            string ownerId = null,
            JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.",
                nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A combat life-cycle checkpoint requires a stable owner ID.",
                nameof(ownerCheckpoint));
        }

        string ledgerRunId = persistent
            ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                ownerId,
                CombatLifeCycleOwnerKind)
            : causalRunId + ":combat-life-cycle-ledger";
        var harness = new OutcomeHarness(
            MigratedProducerOutcomeKind.CombatAttackResult,
            "causal-combat-life-cycle",
            includeCombatDamage: true,
            faultMode: MigratedProducerOutcomeFaultMode.None,
            deliveryFault: false,
            stableRunId: ledgerRunId,
            includeResearchWork: true,
            includeAcquiredTraitInference: true);
        CombatLifeCycleContext context;
        try
        {
            context = new CombatLifeCycleContext(
                harness, causalRunId, protagonistId,
                persistent ? ownerId : null, ownerCheckpoint);
        }
        catch
        {
            harness.Dispose();
            throw;
        }
        var lifetime = new SharedProductionCausalFixtureLifetime(
            3,
            () =>
            {
                context.Dispose();
                harness.Dispose();
            });

        DelegateProductionCausalActionFixture Fixture(
            string actionId,
            string domainId,
            MigratedProducerOutcomeKind primary,
            Func<bool> execute,
            Func<GameplayOutcomeReplayIdentity> requirePrimary,
            MigratedProducerOutcomeKind[] companions,
            Func<IReadOnlyList<GameplayOutcomeReplayIdentity>>
                requireCompanions) =>
            new(
                actionId,
                domainId,
                primary,
                context.CaptureState,
                execute,
                requirePrimary,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CombatLifeCycleOwnerRestoreKind
                    : null,
                context,
                companions,
                requireCompanions,
                captureNarrativeEvidence: context.CaptureNarrativeEvidence);

        return new IProductionCausalActionFixture[]
        {
            Fixture(
                NonlethalCombatInjuryActionId,
                "combat-injury",
                MigratedProducerOutcomeKind.CombatAttackResult,
                context.Injure,
                context.RequireLatestCombatAttack,
                new[]
                {
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent
                },
                () => new[]
                {
                    context.RequireLatest(
                        MigratedProducerOutcomeKind.HealthThresholdCrossedEvent),
                    context.RequireLatest(
                        MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent)
                }),
            Fixture(
                RecoverBodyHealthActionId,
                "injury-treatment",
                MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent,
                context.Recover,
                () => context.RequireLatest(
                    MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent),
                new[]
                {
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent
                },
                () => new[]
                {
                    context.RequireLatest(
                        MigratedProducerOutcomeKind.HealthThresholdCrossedEvent)
                }),
            Fixture(
                FatalCombatInjuryActionId,
                "combat-fatality",
                MigratedProducerOutcomeKind.CombatAttackResult,
                context.Kill,
                context.RequireLatestCombatAttack,
                new[]
                {
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
                    MigratedProducerOutcomeKind.CharacterDeathEvent,
                    MigratedProducerOutcomeKind.CharacterKilledEvent
                },
                () => new[]
                {
                    context.RequireLatest(
                        MigratedProducerOutcomeKind.HealthThresholdCrossedEvent),
                    context.RequireLatest(
                        MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent),
                    context.RequireLatest(
                        MigratedProducerOutcomeKind.CharacterDeathEvent),
                    context.RequireLatest(
                        MigratedProducerOutcomeKind.CharacterKilledEvent)
                })
        };
    }

    internal static IReadOnlyList<IProductionCausalActionFixture>
        RestoreCausalCombatLifeCycleFixtures(
            string causalRunId,
            string ownerId,
            JObject ownerCheckpoint)
    {
        if (ownerCheckpoint?["domainState"] is not JObject domainState)
        {
            throw new InvalidOperationException(
                "Combat life-cycle checkpoint domain state is missing.");
        }
        return CreateCausalCombatLifeCycleFixtures(
            causalRunId,
            CausalGameplaySyntax.RequireId(
                (string)domainState["protagonistId"],
                "combat life-cycle checkpoint protagonist ID"),
            ownerId,
            ownerCheckpoint);
    }

    // Selection capture must use the same restored actor and ledger as the
    // actual combat commands, never a second healthy actor with copied facts.
    private static CombatLifeCycleContext RequireLifeCycleContext(
        IProductionCausalActionFixture fixture) =>
        (fixture as IProductionCausalOwnerCheckpointFixture)?.OwnerRuntimeIdentity
            as CombatLifeCycleContext
        ?? throw new InvalidOperationException("INCOMPLETE_OWNER_CHECKPOINT: combat owner is unavailable.");

    internal static CharacterActor RequireLifeCycleActor(IProductionCausalActionFixture fixture) =>
        RequireLifeCycleContext(fixture).Actor;

    internal static GameplayOutcomeLedger RequireLifeCycleLedger(IProductionCausalActionFixture fixture) =>
        RequireLifeCycleContext(fixture).Ledger;

    internal static GameplayOutcomeRecorder RequireLifeCycleRecorder(IProductionCausalActionFixture fixture) =>
        RequireLifeCycleContext(fixture).Recorder;

    internal static GameplayOutcomeRegistry RequireLifeCycleRegistry(IProductionCausalActionFixture fixture) =>
        RequireLifeCycleContext(fixture).Registry;

    internal static IGameCalendar RequireLifeCycleCalendar(IProductionCausalActionFixture fixture) =>
        RequireLifeCycleContext(fixture).Calendar;

    internal static bool RunCombatLifeCycleCausalOwnerFamily(bool log = false)
    {
        const string OwnerId = "combat-life-cycle:causal:focused";
        const string ProtagonistId = "character:causal:combat-life-cycle";
        JObject injuryCheckpoint;
        using (var fixtures = new CombatLifeCycleFixtureSet(
                   CreateCausalCombatLifeCycleFixtures(
                       "run:causal-combat-life-cycle:injury",
                       ProtagonistId,
                       OwnerId)))
        {
            IProductionCausalActionFixture injury = fixtures.Get(
                NonlethalCombatInjuryActionId);
            Require((int)injury.CaptureState()["health.stage"] == 0,
                "Combat life-cycle owner did not start alive and uninjured.");
            Require(injury.Execute(),
                "The real nonlethal combat entry did not down the protagonist.");
            Require(injury.RequireExactOutcome().HasCanonicalPayloadHash
                    && injury.RequireExactCompanionOutcomes().Count == 2,
                "The injury transaction lost its exact primary or companions.");
            Require((int)injury.CaptureState()["health.stage"] == 1,
                "The nonlethal combat state did not reach the downed stage.");
            injuryCheckpoint = ((IProductionCausalOwnerCheckpointFixture)injury)
                .CaptureOwnerCheckpoint();
        }

        JObject recoveryCheckpoint;
        using (var fixtures = new CombatLifeCycleFixtureSet(
                   CreateCausalCombatLifeCycleFixtures(
                       "run:causal-combat-life-cycle:recovery",
                       ProtagonistId,
                       OwnerId,
                       injuryCheckpoint)))
        {
            IProductionCausalActionFixture injury = fixtures.Get(
                NonlethalCombatInjuryActionId);
            IProductionCausalActionFixture recovery = fixtures.Get(
                RecoverBodyHealthActionId);
            Require(!injury.Execute()
                    && (int)recovery.CaptureState()["health.stage"] == 1,
                "Fresh composition restore lost or replayed the injury stage.");
            Require(recovery.Execute(),
                "The real body-health heal entry did not recover the protagonist.");
            Require(recovery.RequireExactOutcome().HasCanonicalPayloadHash
                    && recovery.RequireExactCompanionOutcomes().Count == 1,
                "The recovery transaction lost its exact primary or threshold companion.");
            Require((int)recovery.CaptureState()["health.stage"] == 2,
                "The recovered combat state did not preserve the injury history.");
            recoveryCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)recovery)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new CombatLifeCycleFixtureSet(
                   CreateCausalCombatLifeCycleFixtures(
                       "run:causal-combat-life-cycle:fatal",
                       ProtagonistId,
                       OwnerId,
                       recoveryCheckpoint)))
        {
            IProductionCausalActionFixture injury = fixtures.Get(
                NonlethalCombatInjuryActionId);
            IProductionCausalActionFixture recovery = fixtures.Get(
                RecoverBodyHealthActionId);
            IProductionCausalActionFixture fatal = fixtures.Get(
                FatalCombatInjuryActionId);
            Require(!injury.Execute() && !recovery.Execute(),
                "Restored recovered owner accepted an earlier stage replay.");
            Require(fatal.Execute(),
                "The real fatal combat entry did not kill the protagonist.");
            Require(fatal.RequireExactOutcome().HasCanonicalPayloadHash
                    && fatal.RequireExactCompanionOutcomes().Count == 4,
                "The fatal transaction lost threshold/downed/death/killed evidence.");
            IReadOnlyDictionary<string, object> after = fatal.CaptureState();
            Require((int)after["health.stage"] == 3
                    && (int)after["health.exactOutcomeCount"] == 12,
                "The fatal combat state did not retain all twelve exact outcomes.");
            terminalCheckpoint = ((IProductionCausalOwnerCheckpointFixture)fatal)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new CombatLifeCycleFixtureSet(
                   CreateCausalCombatLifeCycleFixtures(
                       "run:causal-combat-life-cycle:terminal",
                       ProtagonistId,
                       OwnerId,
                       terminalCheckpoint)))
        {
            IProductionCausalActionFixture injury = fixtures.Get(
                NonlethalCombatInjuryActionId);
            IProductionCausalActionFixture recovery = fixtures.Get(
                RecoverBodyHealthActionId);
            IProductionCausalActionFixture fatal = fixtures.Get(
                FatalCombatInjuryActionId);
            JObject before = ((IProductionCausalOwnerCheckpointFixture)fatal)
                .CaptureOwnerCheckpoint();
            Require(!injury.Execute() && !recovery.Execute() && !fatal.Execute(),
                "Terminal combat life-cycle owner accepted a replay.");
            JObject after = ((IProductionCausalOwnerCheckpointFixture)fatal)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected terminal replay mutated the combat owner.");
        }

        JObject healthTamper = (JObject)recoveryCheckpoint.DeepClone();
        JObject healthState = (JObject)((JObject)healthTamper["domainState"])[
            "bodyHealth"];
        JObject protagonistHealth = ((JArray)healthState["characters"])
            .OfType<JObject>()
            .Single(value => string.Equals(
                (string)value["characterId"],
                ProtagonistId,
                StringComparison.Ordinal));
        protagonistHealth["currentHealth"] = 100f;
        Require(RejectCombatLifeCycleCheckpoint(
                OwnerId,
                ProtagonistId,
                healthTamper,
                "health-tamper"),
            "A combat checkpoint with fabricated health was accepted.");

        JObject identityTamper = (JObject)injuryCheckpoint.DeepClone();
        identityTamper["ownerId"] = "combat-life-cycle:causal:tampered";
        Require(RejectCombatLifeCycleCheckpoint(
                OwnerId,
                ProtagonistId,
                identityTamper,
                "identity-tamper"),
            "A combat checkpoint with changed owner identity was accepted.");

        if (log)
        {
            Debug.Log(
                "Nonlethal injury -> restored recovery -> restored fatal combat life-cycle PASS");
        }
        return true;
    }

    public static void RunCombatLifeCycleCausalBatch()
    {
        try
        {
            Require(RunCombatLifeCycleCausalOwnerFamily(log: true),
                "Combat life-cycle causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunD03FromBatch()
    {
        string reportPath = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_D03_COMBAT_CAUSAL_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(reportPath)
            || !Path.IsPathRooted(reportPath))
        {
            throw new InvalidOperationException(
                "D03 batch verification requires an absolute report path.");
        }
        if (File.Exists(reportPath))
        {
            throw new IOException("D03 batch report already exists: " + reportPath);
        }

        Require(RunCombatLifeCycleCausalOwnerFamily(log: true),
            "Combat life-cycle causal owner family returned false.");
        string directory = Path.GetDirectoryName(reportPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "D03 batch report path has no parent directory.");
        }
        Directory.CreateDirectory(directory);
        string report = string.Join(Environment.NewLine, new[]
        {
            "U0_D03_COMBAT_CAUSAL=PASS",
            "stages=nonlethal-injury,recovery,fatal-injury",
            "causation=stable-root,damage-direct-parent,derived-direct-effect,attack-atomic-sibling",
            "derived=threshold,downed,recovered,death,killed,actual-only",
            "restore=injury,recovery,terminal",
            "rejection=terminal-replay,health-tamper,owner-identity-tamper",
            "fullAuditClosure=false",
            "humanApprovalClaimed=false",
            "trainingEligible=false"
        }) + Environment.NewLine;
        File.WriteAllText(reportPath, report);
        Debug.Log("U0_D03_COMBAT_CAUSAL=PASS; report=" + reportPath);
    }

    private static bool RejectCombatLifeCycleCheckpoint(
        string ownerId,
        string protagonistId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new CombatLifeCycleFixtureSet(
                CreateCausalCombatLifeCycleFixtures(
                    "run:causal-combat-life-cycle:" + suffix,
                    protagonistId,
                    ownerId,
                    checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class CombatLifeCycleContext :
        IDisposable,
        IProductionCausalMemoryHistoryFixture
    {
        private readonly OutcomeHarness harness;
        private readonly string ownerId;
        private readonly List<Object> cleanup = new();
        private readonly CharacterBodyHealthRuntime health;
        private readonly LifeCycleSession session;
        private readonly IGameCalendar calendar;
        private readonly CharacterActor attacker;
        private readonly CharacterActor protagonist;
        private readonly CombatCommandResultApplier applier;
        private readonly string injuryOperationId;
        private readonly string fatalOperationId;
        private readonly GameEventBus events;
        private readonly CharacterVitalsActivityObserver vitalsActivities;
        private bool disposed;

        internal CombatLifeCycleContext(
            OutcomeHarness harness,
            string causalRunId,
            string protagonistId,
            string ownerId,
            JObject checkpoint)
        {
            this.harness = harness ?? throw new ArgumentNullException(nameof(harness));
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            ProtagonistId = CausalGameplaySyntax.RequireId(
                protagonistId,
                nameof(protagonistId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            AttackerId = "character:causal-combat-attacker:" + scope;
            injuryOperationId = "combat:causal-life-cycle:injury:" + scope;
            fatalOperationId = "combat:causal-life-cycle:fatal:" + scope;

            var clock = new UnityGameClock();
            events = new GameEventBus();
            vitalsActivities = new CharacterVitalsActivityObserver(
                events,
                CharacterAiEditorTestDependencies.WorldRegistry);
            vitalsActivities.Initialize();
            GameData sessionSettings = ScriptableObject.CreateInstance<GameData>();
            cleanup.Add(sessionSettings);
            session = new LifeCycleSession(sessionSettings);
            calendar = new GameCalendarRuntime(session, events, clock);
            calendar.SetDateTime(CharacterAiEditorTestDependencies.GameCalendar.Day,
                CharacterAiEditorTestDependencies.GameCalendar.Hour);
            health = new CharacterBodyHealthRuntime(new DungeonStory.Foundation.UnityRealtimeClock(),
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                harness.AggregateRootStore,
                calendar);
            attacker = CreateActor("철갑 추격자", AttackerId, health);
            protagonist = CreateActor("연화", ProtagonistId, health);
            // The life-cycle campaign starts from a stable, already-wounded
            // condition. A single localized nonlethal hit cannot remove more
            // health than the struck body part owns, so a pristine 100 HP
            // fixture can become downed through a critical torso while never
            // crossing the aggregate 20% health threshold. Starting at 25 HP
            // lets the real combat transaction truthfully emit both the
            // threshold and downed outcomes without fabricating either one.
            health.ApplyLocalizedDamage(
                protagonist,
                CombatBodyPart.Torso,
                amount: 20f,
                reason: "fixture:prior-torso-wound",
                allowDeath: false);
            health.RestoreLegacyVitalsProjection(
                protagonist,
                maximumHealth: 100f,
                currentHealth: 25f,
                injurySeverity: 0.75f);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                protagonist);
            health.ConfigureFactPublication(harness.Facts, harness.Facts);
            applier = new CombatCommandResultApplier(
                CharacterAiEditorTestDependencies.CombatEquipment,
                health,
                health,
                health,
                harness.CombatActions,
                new NoCoverDurabilityRegistry(),
                clock,
                new NoWorldUiHierarchy(),
                events,
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.WorldRegistry,
                new RoomFacilityPolicyService(new RoomLayoutCache()),
                calendar);
            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                harness.Probe.ClearObservations();
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
            ? CombatLifeCycleOwnerKind
            : string.Empty;
        internal string ProtagonistId { get; }
        internal string AttackerId { get; }
        internal CharacterActor Actor => protagonist;
        internal GameplayOutcomeLedger Ledger => harness.Ledger;
        internal GameplayOutcomeRegistry Registry => harness.Registry;
        internal GameplayOutcomeRecorder Recorder => harness.Recorder;
        internal IGameCalendar Calendar => calendar;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            return new Dictionary<string, object>
            {
                ["health.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-combat-life-cycle",
                ["health.actorId"] = ProtagonistId,
                ["health.attackerId"] = AttackerId,
                ["health.ownerSnapshot"] = JsonUtility.ToJson(health.Capture()),
                ["health.stage"] = Stage,
                ["health.currentHealth"] = health.GetVitals(protagonist).CurrentHealth,
                ["health.downed"] = health.GetSnapshot(protagonist).Downed,
                ["health.attackOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.CombatAttackResult),
                ["health.thresholdOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent),
                ["health.downedOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent),
                ["health.recoveredOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent),
                ["health.deathOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterDeathEvent),
                ["health.killedOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterKilledEvent),
                ["health.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool Injure() => ApplyCombatAtStage(
            expectedStage: 0,
            injuryOperationId,
            nonlethal: true);

        internal bool Recover()
        {
            ThrowIfDisposed();
            if (Stage != 1 || !health.GetSnapshot(protagonist).Downed)
                return false;
            health.Heal(protagonist, 40f, stopBleeding: true);
            bool valid = Stage == 2
                && !health.GetSnapshot(protagonist).Downed
                && health.GetVitals(protagonist).CurrentHealth > 20f;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool Kill() => ApplyCombatAtStage(
            expectedStage: 2,
            fatalOperationId,
            nonlethal: false);

        public bool EnsureCompactedMemoryHistory()
        {
            ThrowIfDisposed();
            GameplayEntityId subject = new(
                EvolutionOutcomeIds.CharacterKind,
                ProtagonistId);
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> existing =
                ProductionCausalNarrativeEvidence.Capture(
                    harness.Ledger,
                    subject,
                    16);
            if (existing.Any(value => string.Equals(
                    value.sourceKind,
                    "compacted",
                    StringComparison.Ordinal)))
            {
                return true;
            }

            ResearchProjectSO project =
                ScriptableObject.CreateInstance<ResearchProjectSO>();
            cleanup.Add(project);
            project.Configure(
                "research:module-source-memory",
                "전투 기록 정리",
                "반복된 전투 경험을 장기 기억으로 정리한다.",
                default,
                100f,
                ResearchBlueprintRule.None,
                null,
                Array.Empty<ResearchProjectSO>(),
                new BlueprintUnlockCollection());
            var state = new BlueprintResearchState();
            var shop = new FacilityShopUnlockState();
            using DomainFactDispatcher researchFacts = new();
            researchFacts.Subscribe(
                "research-work-gameplay-outcome",
                new GameplayDomainFactObserver<
                    ResearchWorkAppliedFact,
                    ResearchWorkOutcomeReceipt>(
                    ResearchWorkFactObservation.Capture,
                    harness.Registry,
                    harness.Ledger,
                    harness.Recorder,
                    new GameplayDomainFactCommitGroup(
                        harness.Recorder,
                        harness.Ledger)));
            var transaction = new BlueprintResearchOutcomeTransaction(
                researchFacts,
                researchFacts);
            for (int index = 0; index < 5; index++)
            {
                int day = Math.Max(calendar.Day, 10 + index);
                calendar.SetDateTime(day, calendar.Hour);
                if (!transaction.TryApply(
                        state,
                        shop,
                        EmptyResearchCatalog.Instance,
                        project,
                        null,
                        1f,
                        day,
                        ProtagonistId,
                        "연화",
                        "facility:module-source-archive",
                        "기록실",
                        out BlueprintResearchWorkResult result,
                        out _))
                {
                    throw new InvalidOperationException(
                        "Research memory history transaction failed: "
                        + result.Message);
                }
            }

            GameplayOutcomeLedgerDiagnostics diagnostics =
                harness.Ledger.GetDiagnostics();
            var request = new GameplayOutcomeConsolidationRequest(
                365,
                diagnostics.NextSequence - 1L,
                harness.Registry.ConsolidationPolicyVersion,
                harness.Ledger.CurrentWorldEpoch);
            if (!harness.Ledger.TrySchedule(request, out string scheduleFailure))
            {
                throw new InvalidOperationException(
                    "Trait-reaction memory consolidation schedule failed: "
                    + scheduleFailure);
            }
            int guard = 1024;
            while (harness.Ledger.PendingJobCount > 0 && guard-- > 0)
                harness.Ledger.ProcessSlice(32, 0L);
            if (guard <= 0 || harness.Ledger.PendingJobCount != 0)
                throw new InvalidOperationException(
                    "Trait-reaction memory consolidation did not drain.");

            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> compacted =
                ProductionCausalNarrativeEvidence.Capture(
                    harness.Ledger,
                    subject,
                    16);
            return compacted.Any(value => string.Equals(
                value.sourceKind,
                "compacted",
                StringComparison.Ordinal));
        }

        private sealed class EmptyResearchCatalog : IFacilityShopCatalog
        {
            internal static readonly EmptyResearchCatalog Instance = new();

            public IReadOnlyCollection<BuildingSO> Buildings =>
                Array.Empty<BuildingSO>();
            public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
                Array.Empty<FacilityBlueprintSO>();
            public BuildingSO FindBuildingById(int id) => null;
        }

        internal GameplayOutcomeReplayIdentity RequireLatestCombatAttack() =>
            RequireLatest(MigratedProducerOutcomeKind.CombatAttackResult);

        internal GameplayOutcomeReplayIdentity RequireLatest(
            MigratedProducerOutcomeKind kind) => harness.RequireExact(
                kind,
                GameplayOutcomeReplayState.PublishedAcknowledged);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(harness.Ledger, identity);

        internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
            CaptureNarrativeEvidence(
                GameplayEntityId subject,
                int maximumCount) =>
            ProductionCausalNarrativeEvidence.Capture(
                harness.Ledger,
                subject,
                maximumCount);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral combat life-cycle fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CombatLifeCycleOwnerKind,
                CombatLifeCycleOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 4,
                    ["schema"] = CombatLifeCycleDomainStateSchema,
                    ["stage"] = Stage,
                    ["protagonistId"] = ProtagonistId,
                    ["attackerId"] = AttackerId,
                    ["injuryOperationId"] = injuryOperationId,
                    ["fatalOperationId"] = fatalOperationId,
                    ["session"] = Serialize(session.Capture(), "game session"),
                    ["bodyHealth"] = Serialize(health.Capture(), "body health"),
                    ["outcomeOwners"] = Serialize(
                        harness.Transaction.Capture(),
                        "outcome owners"),
                    ["combatActionPending"] = Serialize(
                        harness.CombatActions.Capture(),
                        "combat action pending"),
                    ["attackerStats"] = CaptureStats(attacker),
                    ["protagonistStats"] = CaptureStats(protagonist)
                },
                harness.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            vitalsActivities.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(
                attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(
                protagonist);
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            }
        }

        private int Stage
        {
            get
            {
                int attack = OutcomeCount(
                    MigratedProducerOutcomeKind.CombatAttackResult);
                int threshold = OutcomeCount(
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent);
                int downed = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent);
                int recovered = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent);
                int death = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterDeathEvent);
                int killed = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterKilledEvent);
                float current = health.GetVitals(protagonist).CurrentHealth;
                bool isDowned = health.GetSnapshot(protagonist).Downed;
                if (attack == 0 && threshold == 0 && downed == 0
                    && recovered == 0 && death == 0 && killed == 0
                    && current >= 24.9f && current <= 25.1f && !isDowned)
                    return 0;
                if (attack == 1 && threshold == 1 && downed == 1
                    && recovered == 0 && death == 0 && killed == 0
                    && current > 0f && current <= 1.1f && isDowned)
                    return 1;
                if (attack == 1 && threshold == 2 && downed == 1
                    && recovered == 1 && death == 0 && killed == 0
                    && current > 20f && !isDowned)
                    return 2;
                if (attack == 2 && threshold == 3 && downed == 2
                    && recovered == 1 && death == 1 && killed == 1
                    && current <= 0f && isDowned)
                    return 3;
                return -1;
            }
        }

        private bool ApplyCombatAtStage(
            int expectedStage,
            string operationId,
            bool nonlethal)
        {
            ThrowIfDisposed();
            if (Stage != expectedStage || protagonist.IsDead)
                return false;
            var target = new CombatParticipantRef(protagonist);
            if (!applier.TryReserveDamageOutcome(
                    target,
                    operationId,
                    1L,
                    out ReservedCombatAction reserved,
                    out _,
                    out _))
            {
                return false;
            }
            var result = new CombatAttackResult(
                executed: true,
                hit: true,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: 100f,
                appliedDamage: 100f,
                bleeding: 0f,
                suppression: 3f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                nonlethal: nonlethal);
            CombatOutcomeApplyResult applied = applier.Apply(
                target,
                result,
                attacker,
                attacker.Identity.DisplayName,
                CombatDamageType.Slash,
                operationId,
                1L,
                reserved,
                new CombatOutcomeMechanicalMutation(
                    apply: () => string.Empty,
                    rollback: () => { },
                    complete: () => { }));
            int expectedAfter = nonlethal ? 1 : 3;
            bool valid = applied.Succeeded && Stage == expectedAfter;
            if (!valid)
            {
                Debug.LogWarning(
                    "Combat life-cycle transition rejected: succeeded="
                    + applied.Succeeded
                    + "; detail="
                    + applied.FailureReason
                    + "; stage="
                    + Stage
                    + "; health="
                    + health.GetVitals(protagonist).CurrentHealth
                    + "; downed="
                    + health.GetSnapshot(protagonist).Downed
                    + "; exact="
                    + ExactOutcomeCount()
                    + "; attack="
                    + OutcomeCount(MigratedProducerOutcomeKind.CombatAttackResult)
                    + "; threshold="
                    + OutcomeCount(MigratedProducerOutcomeKind.HealthThresholdCrossedEvent)
                    + "; downedOutcome="
                    + OutcomeCount(MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent)
                    + "; death="
                    + OutcomeCount(MigratedProducerOutcomeKind.CharacterDeathEvent)
                    + "; killed="
                    + OutcomeCount(MigratedProducerOutcomeKind.CharacterKilledEvent));
            }
            if (valid)
                ValidateState();
            return valid;
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CombatLifeCycleOwnerKind,
                CombatLifeCycleOwnerRestoreKind,
                harness.Ledger);
            RequireFields(
                state,
                "combat life-cycle owner state",
                "schemaVersion",
                "schema",
                "stage",
                "protagonistId",
                "attackerId",
                "injuryOperationId",
                "fatalOperationId",
                "session",
                "bodyHealth",
                "outcomeOwners",
                "combatActionPending",
                "attackerStats",
                "protagonistStats");
            if ((int)state["schemaVersion"] != 4
                || !string.Equals(
                    (string)state["schema"],
                    CombatLifeCycleDomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["protagonistId"],
                    ProtagonistId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["attackerId"],
                    AttackerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["injuryOperationId"],
                    injuryOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["fatalOperationId"],
                    fatalOperationId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat life-cycle checkpoint identity or operation changed.");
            }

            DungeonMigratedProducerOutcomeSaveData ownerData = Deserialize<
                DungeonMigratedProducerOutcomeSaveData>(
                    state["outcomeOwners"],
                    "outcome owners");
            harness.Transaction.Restore(
                harness.Transaction.PrepareRestore(ownerData));
            CombatActionPendingSaveData pending = Deserialize<
                CombatActionPendingSaveData>(
                    state["combatActionPending"],
                    "combat action pending");
            harness.CombatActions.PublishRestore(
                harness.CombatActions.PrepareRestore(pending));
            RestoreStats(attacker, state["attackerStats"]);
            RestoreStats(protagonist, state["protagonistStats"]);
            DungeonCharacterBodyHealthSaveData healthData = Deserialize<
                DungeonCharacterBodyHealthSaveData>(
                    state["bodyHealth"],
                    "body health");
            health.PublishRestore(health.PrepareRestore(healthData));
            session.Restore(Deserialize<GameSessionSaveData>(state["session"], "game session"));
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Combat life-cycle checkpoint stage disagrees with saved state.");
            }
        }

        private void ValidateState()
        {
            foreach (GameplayOutcomeSnapshot outcome in harness.Ledger.CaptureGameplayOutcomes().exactOutcomes)
            {
                if (outcome != null && outcome.absoluteDay > calendar.Day)
                    throw new InvalidOperationException("FUTURE_OUTCOME: combat checkpoint precedes its recorded facts.");
            }
            int stage = Stage;
            int expectedExact = stage switch
            {
                0 => 0,
                // Each combat attack also commits the production combat-damage
                // outcome in the same external batch. It is intentionally not
                // a narrative companion, but it remains part of the exact
                // owner ledger and therefore must survive every checkpoint.
                1 => 4,
                2 => 6,
                3 => 12,
                _ => -1
            };
            if (stage is < 0 or > 3
                || ExactOutcomeCount() != expectedExact
                || !string.Equals(
                    protagonist.Identity.PersistentId,
                    ProtagonistId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    attacker.Identity.PersistentId,
                    AttackerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat health, outcome owner and exact ledger diverged.");
            }
            if (stage >= 1)
            {
                ValidateCombatCausation(
                    injuryOperationId,
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent);
            }
            if (stage >= 3)
            {
                ValidateCombatCausation(
                    fatalOperationId,
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
                    MigratedProducerOutcomeKind.CharacterDeathEvent,
                    MigratedProducerOutcomeKind.CharacterKilledEvent);
            }
        }

        private void ValidateCombatCausation(
            string operationId,
            params MigratedProducerOutcomeKind[] expectedDerivedKinds)
        {
            List<GameplayOutcomeSnapshot> outcomes = harness.Ledger
                .CaptureGameplayOutcomes()
                .exactOutcomes;
            GameplayOutcomeSnapshot damage = outcomes.Single(value =>
                value != null
                && string.Equals(
                    value.outcomeTypeId,
                    ProductionCombatOutcomeIds.CombatDamageResolved.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.operationId,
                    operationId,
                    StringComparison.Ordinal));
            string attackTypeId = MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.CombatAttackResult)
                .OutcomeTypeId.Value;
            GameplayOutcomeSnapshot attack = outcomes.Single(value =>
                value != null
                && string.Equals(
                    value.outcomeTypeId,
                    attackTypeId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.rootOperationId,
                    operationId,
                    StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(attack.parentRunId)
                || attack.parentSequence != 0
                || !string.Equals(
                    attack.causationRelationId,
                    "atomic-sibling",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat attack result was not preserved as an unchained atomic sibling.");
            }

            var expectedTypes = new HashSet<string>(
                expectedDerivedKinds.Select(value =>
                    MigratedProducerOutcomeCatalog.Get(value)
                        .OutcomeTypeId.Value),
                StringComparer.Ordinal);
            List<GameplayOutcomeSnapshot> derived = outcomes.Where(value =>
                    value != null
                    && expectedTypes.Contains(value.outcomeTypeId)
                    && string.Equals(
                        value.rootOperationId,
                        operationId,
                        StringComparison.Ordinal))
                .ToList();
            if (derived.Count != expectedTypes.Count
                || derived.Select(value => value.outcomeTypeId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() != expectedTypes.Count
                || derived.Any(value =>
                    !string.Equals(
                        value.parentRunId,
                        damage.runId,
                        StringComparison.Ordinal)
                    || value.parentSequence != damage.sequence
                    || !string.Equals(
                        value.causationRelationId,
                        "direct-effect",
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Combat health/death results lost their direct damage parent or fabricated a sequential chain.");
            }
        }

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string typeId = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return harness.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        typeId,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => harness.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes.Count(value => value != null
                && !string.Equals(
                    value.outcomeTypeId,
                    EvolutionOutcomeIds.TraitReactionExecuted.Value,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.outcomeTypeId,
                    ResearchWorkOutcomeIds.WorkApplied.Value,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.outcomeTypeId,
                    EvolutionOutcomeIds.TraitInferenceCompleted.Value,
                    StringComparison.Ordinal));

        private CharacterActor CreateActor(
            string displayName,
            string id,
            CharacterBodyHealthRuntime bodyHealth)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    displayName,
                    "human");
            cleanup.Add(data);
            var actorObject = new GameObject(displayName);
            cleanup.Add(actorObject);
            CharacterActor result = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            result.EnsureRuntimeState();
            result.data = data;
            result.characterType = CharacterType.NPC;
            result.Identity.SetPersistentId(id);
            result.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    bodyHealth,
                    bodyHealth,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        calendar),
                    new NoopOwnerRunLifecycleService()));
            bodyHealth.ConfigureVitals(result, 100f, resetCurrentHealth: true);
            return result;
        }

        private static JObject CaptureStats(CharacterActor actor)
        {
            CharacterMoodSnapshot mood = actor?.Mood
                ?? throw new InvalidOperationException(
                    "Combat life-cycle actor mood is unavailable.");
            return new JObject
            {
                ["conditions"] = new JArray(actor.Stats.StatSnapshot
                    .OrderBy(value => (int)value.Key)
                    .Select(value => new JObject
                    {
                        ["condition"] = value.Key.ToString(),
                        ["value"] = value.Value
                    })),
                ["currentHealth"] = actor.Stats.CurrentHealth,
                ["lifecycleState"] = (int)actor.Lifecycle.CurrentState,
                ["lifecycleRevision"] = actor.Lifecycle.LifecycleRevision,
                ["injurySeverity"] = actor.Stats.InjurySeverity,
                ["baseMood"] = mood.BaseValue,
                ["moodFactors"] = new JArray(mood.Factors
                    .Where(value => value != null
                        && value.Kind == CharacterMoodFactorKind.Interaction)
                    .OrderBy(value => value.Id, StringComparer.Ordinal)
                    .Select(value => new JObject
                    {
                        ["id"] = value.Id,
                        ["label"] = value.Label,
                        ["value"] = value.Value,
                        ["remainingSeconds"] = value.RemainingSeconds
                    }))
            };
        }

        private static void RestoreStats(CharacterActor actor, JToken token)
        {
            if (actor == null || token is not JObject value)
            {
                throw new InvalidOperationException(
                    "Combat life-cycle actor stats are missing.");
            }
            RequireFields(
                value,
                "combat life-cycle actor stats",
                "conditions",
                "currentHealth",
                "lifecycleState",
                "lifecycleRevision",
                "injurySeverity",
                "baseMood",
                "moodFactors");
            if (value["conditions"] is not JArray conditions
                || value["moodFactors"] is not JArray factors)
            {
                throw new InvalidOperationException(
                    "Combat life-cycle actor stat arrays are invalid.");
            }
            if (value["lifecycleState"]?.Type != JTokenType.Integer
                || value["lifecycleRevision"]?.Type != JTokenType.Integer
                || (long)value["lifecycleRevision"] < 0)
                throw new InvalidOperationException("Combat lifecycle checkpoint has no valid lifecycle authority.");
            actor.Lifecycle.PrepareForDetachedRestore(actor);
            actor.Lifecycle.RestorePersistentState((CharacterLifecycleState)(int)value["lifecycleState"],
                (long)value["lifecycleRevision"]);
            actor.Lifecycle.PublishDetachedRestore();
            var restoredStats = new Dictionary<CharacterCondition, float>();
            foreach (JObject row in conditions.Cast<JObject>())
            {
                RequireFields(
                    row,
                    "combat life-cycle condition",
                    "condition",
                    "value");
                if (!Enum.TryParse(
                        (string)row["condition"],
                        ignoreCase: false,
                        out CharacterCondition condition)
                    || !restoredStats.TryAdd(condition, (float)row["value"]))
                {
                    throw new InvalidOperationException(
                        "Combat life-cycle condition is unknown or duplicated.");
                }
            }
            var restoredFactors = new List<CharacterMoodFactorSnapshot>();
            string previousFactor = string.Empty;
            foreach (JObject row in factors.Cast<JObject>())
            {
                RequireFields(
                    row,
                    "combat life-cycle mood factor",
                    "id",
                    "label",
                    "value",
                    "remainingSeconds");
                string id = (string)row["id"] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id)
                    || previousFactor.Length > 0
                    && string.CompareOrdinal(previousFactor, id) >= 0)
                {
                    throw new InvalidOperationException(
                        "Combat life-cycle mood factors are invalid or unordered.");
                }
                previousFactor = id;
                restoredFactors.Add(new CharacterMoodFactorSnapshot(
                    id,
                    (string)row["label"] ?? string.Empty,
                    (float)row["value"],
                    CharacterMoodFactorKind.Interaction,
                    (float)row["remainingSeconds"]));
            }
            actor.Stats.RestorePersistentState(
                restoredStats,
                (float)value["currentHealth"],
                (float)value["injurySeverity"],
                (float)value["baseMood"],
                restoredFactors);
        }

        private static JObject Serialize(object value, string label)
        {
            try
            {
                return JObject.Parse(JsonUtility.ToJson(
                    value ?? throw new ArgumentNullException(nameof(value))));
            }
            catch (Exception exception) when (
                exception is JsonException or ArgumentException)
            {
                throw new InvalidOperationException(
                    "Could not serialize combat life-cycle " + label + ".",
                    exception);
            }
        }

        private static T Deserialize<T>(JToken token, string label)
            where T : class
        {
            T value = JsonUtility.FromJson<T>(
                (token ?? throw new InvalidOperationException(
                    "Combat life-cycle " + label + " is missing."))
                .ToString(Formatting.None));
            return value ?? throw new InvalidOperationException(
                "Combat life-cycle " + label + " is invalid.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class LifeCycleSession : IGameSessionStateProvider
    {
        private readonly GameSessionState state;
        private readonly IGameSessionStateMutation mutation;

        internal LifeCycleSession(GameData settings)
        {
            state = GameSessionState.Create(settings, out IGameSessionStateMutation authority);
            mutation = authority;
        }

        public bool TryGetSessionState(out GameSessionState value) { value = state; return true; }
        internal GameSessionSaveData Capture() => GameSessionSaveData.From(state.Capture());
        internal void Restore(GameSessionSaveData saved)
        {
            GameSessionSnapshot snapshot = saved.ToSnapshot();
            int expectedHour = (int)Math.Floor(snapshot.ElapsedSeconds / GameCalendarRules.SecondsPerDay
                * GameCalendarRules.HoursPerDay);
            if (snapshot.ElapsedSeconds >= GameCalendarRules.SecondsPerDay || expectedHour != snapshot.Hour)
                throw new InvalidOperationException("INCONSISTENT_GAME_TIME: saved calendar and elapsed time disagree.");
            mutation.Restore(snapshot);
        }
    }

    private sealed class CombatLifeCycleFixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal CombatLifeCycleFixtureSet(
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

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = (value
                ?? throw new ArgumentNullException(nameof(value)))
            .Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }
}
#endif
