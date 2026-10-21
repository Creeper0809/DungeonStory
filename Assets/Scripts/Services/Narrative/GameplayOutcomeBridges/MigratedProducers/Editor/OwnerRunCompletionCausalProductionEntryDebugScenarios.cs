#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled initial day/owner, then real completion -> live observer settlement.
// The two commits are ordered, NOT atomic siblings. Actor identity/progression,
// run.flow and meta.progression are persisted; this is not a full-world save.
public static class OwnerRunCompletionCausalProductionEntryDebugScenarios
{
    internal const string RestoreKind = "owner-family:owner-run-completion";
    internal const string OwnerKind = "owner-kind:owner-run-completion";
    internal const string CompleteActionId = "action:owner-run-completion";
    internal const string UpgradeActionId = "action:owner-run-legacy-upgrade";
    private const string Schema = "dungeonstory-causal-owner-run-completion-state@1";
    private const string FocusedOwner = "owner-run:causal-completion:focused";
    private const string Reason = "세 번째 운영일의 통제된 런 종료";
    private const int Day = 3;
    private const int Seed = 104729;
    private const int ExpectedCurrency = 105;
    private const string UpgradeId = MetaUpgradeIds.StartingFacilityCandidatePlusOne;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId, string ownerId = null, JObject ownerCheckpoint = null)
    {
        CausalGameplaySyntax.RequireId(causalRunId, nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        Require(persistent || ownerCheckpoint == null, "Owner checkpoint requires a persistent owner binding.");
        var context = new Context(persistent ? ownerId : "owner-run:" + causalRunId, ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                CompleteActionId, "run", MigratedProducerOutcomeKind.OwnerRunEndedEvent,
                context.CaptureState, () =>
                {
                    bool ended = context.CompleteRun();
                    Require(!ended || context.Stage == 1,
                        "Owner committed but post-commit settlement failed; campaign cannot claim completion.");
                    return ended;
                },
                () => context.RequireExact(MigratedProducerOutcomeKind.OwnerRunEndedEvent),
                context.CaptureOutcomeEvidence, lifetime.Release,
                persistent ? ownerId : null, persistent ? OwnerKind : null,
                persistent ? context.Checkpoint : null,
                persistent ? RestoreKind : null, persistent ? context : null,
                new[] { MigratedProducerOutcomeKind.RunResultReadyEvent },
                () => new[] { context.RequireExact(MigratedProducerOutcomeKind.RunResultReadyEvent) },
                new[] { "post-commit-observer" }),
            new DelegateProductionCausalActionFixture(
                UpgradeActionId, "meta", MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent,
                context.CaptureState, context.PurchaseUpgrade,
                () => context.RequireExact(MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent),
                context.CaptureOutcomeEvidence, lifetime.Release,
                persistent ? ownerId : null, persistent ? OwnerKind : null,
                persistent ? context.Checkpoint : null,
                persistent ? RestoreKind : null, persistent ? context : null)
        };
    }

    public static void RunBatchFromEnvironment()
    {
        string output = RequiredEnvironment("DUNGEONSTORY_OWNER_RUN_OUTPUT");
        Directory.CreateDirectory(output);
        JObject report = RunDirect(out JObject first);
        report["outcomeRelationContract"] = ProductionCausalPlanBatchExporter.VerifyOwnerCompletionRelationContract(report);
        WriteNew(Path.Combine(output, "first-checkpoint.json"), first);
        WriteNew(Path.Combine(output, "direct-report.json"), report);
        Debug.Log("OWNER_RUN_COMPLETION_CAUSAL=PASS; output=" + output);
    }

    public static void RunFreshFromEnvironment()
    {
        string output = RequiredEnvironment("DUNGEONSTORY_OWNER_RUN_OUTPUT");
        string input = RequiredEnvironment("DUNGEONSTORY_OWNER_RUN_CHECKPOINT");
        Directory.CreateDirectory(output);
        JObject first = JObject.Parse(File.ReadAllText(input));
        using var restored = new Context(FocusedOwner, first);
        Require(restored.Stage == 1 && !restored.CompleteRun(),
            "Fresh-process owner completion replay was not rejected.");
        JObject before = restored.Checkpoint();
        Require(restored.PurchaseUpgrade(), "Fresh-process authored upgrade failed.");
        restored.Validate();
        JObject terminal = restored.Checkpoint();
        using var final = new Context(FocusedOwner, terminal);
        final.VerifyTerminalReplay();
        WriteNew(Path.Combine(output, "fresh-report.json"), new JObject
        {
            ["status"] = "PASS", ["sourceCheckpoint"] = Path.GetFullPath(input),
            ["before"] = before, ["terminal"] = terminal,
            ["orderedOutcomes"] = restored.OrderedEvidence(),
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false,
            ["releaseGate"] = "REJECT_BEFORE_DPO"
        });
        Debug.Log("OWNER_RUN_COMPLETION_FRESH=PASS; output=" + output);
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        RunDirect(out _);
        if (log) Debug.Log("Owner completion -> observer settlement -> restored authored upgrade PASS");
        return true;
    }

    private static JObject RunDirect(out JObject first)
    {
        JObject initial;
        JObject terminal;
        JArray observations;
        var rejectionRows = new JArray();
        using (var context = new Context(FocusedOwner))
        {
            initial = context.Checkpoint();
            rejectionRows.Merge(context.VerifyRejectedCommand(MigratedProducerOutcomeKind.OwnerRunEndedEvent, false));
            Require(context.CompleteRun() && context.Stage == 1,
                "Real owner event did not complete its live settlement observer.");
            first = context.Checkpoint();
            Require(!context.CompleteRun() && JToken.DeepEquals(first, context.Checkpoint()),
                "Completed owner accepted replay.");
        }
        using (var context = new Context(FocusedOwner, RoundTrip(first)))
        {
            Require(context.Stage == 1 && !context.CompleteRun(), "Restored run-end projection was lost.");
            rejectionRows.Merge(context.VerifyRejectedCommand(MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent, true));
            Require(context.PurchaseUpgrade() && context.Stage == 2, "Authored upgrade purchase failed.");
            terminal = context.Checkpoint();
            observations = context.OrderedEvidence();
        }
        using (var context = new Context(FocusedOwner, RoundTrip(terminal)))
        {
            context.VerifyTerminalReplay();
        }
        var failureRows = new JArray();
        foreach (var fault in Faults())
        {
            JObject partial;
            using (var context = new Context(FocusedOwner + ":observer-" + fault))
            {
                context.Probe.TargetKind = MigratedProducerOutcomeKind.RunResultReadyEvent;
                context.Probe.FaultMode = fault;
                Require(context.CompleteRun() && context.Stage == -1,
                    "Observer rejection incorrectly rolled back the committed owner or became success.");
                context.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
                partial = context.Checkpoint();
                Require(!context.CompleteRun(), "Observer rejection made owner completion replayable.");
            }
            using var restored = new Context(FocusedOwner + ":observer-" + fault, RoundTrip(partial));
            Require(restored.Stage == -1 && !restored.CompleteRun(),
                "Fresh restore hid the incomplete post-commit settlement.");
            failureRows.Add(new JObject { ["fault"] = fault.ToString(),
                ["ownerCommitted"] = true, ["settlementCommitted"] = false,
                ["checkpoint"] = restored.Checkpoint() });
        }
        var mutations = new Dictionary<string, Action<JObject>>
        {
            ["run-seed"] = value => value["domainState"]["runSeed"] = Seed + 1,
            ["flow-outcome"] = value => value["domainState"]["runFlow"]["outcome"] = 0,
            ["flow-day"] = value => value["domainState"]["runFlow"]["currentDay"] = Day + 1,
            ["actor-template"] = value => value["domainState"]["actor"]["dataId"] = -1,
            ["settlement-currency"] = value => value["domainState"]["metaProgression"]["lifetimeEarnedCurrency"] = ExpectedCurrency + 1,
            ["owner-identity"] = value => value["ownerId"] = FocusedOwner + ":wrong"
        };
        var rejected = new JArray();
        foreach (var mutation in mutations)
        {
            JObject changed = RoundTrip(first);
            mutation.Value(changed);
            bool failed = false;
            try { using var ignored = new Context(FocusedOwner, changed); }
            catch (InvalidOperationException) { failed = true; }
            Require(failed, "Tampered checkpoint accepted: " + mutation.Key);
            rejected.Add(mutation.Key);
        }
        return new JObject
        {
            ["status"] = "PASS", ["schema"] = Schema,
            ["initial"] = initial, ["first"] = first, ["terminal"] = terminal,
            ["orderedOutcomes"] = observations,
            ["rejectionProbeCount"] = rejectionRows.Count, ["rejectionProbes"] = rejectionRows,
            ["observerFailureProbes"] = failureRows,
            ["tamperCases"] = rejected, ["actualGameplayProductionEntries"] = true,
            ["controlledSimulation"] = true, ["naturalPlayTelemetry"] = false,
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false,
            ["releaseGate"] = "REJECT_BEFORE_DPO"
        };
    }

    private static DomainFactDispatcher CreateMetaFacts(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(
            outcomes.Recorder,
            outcomes.Ledger);
        try
        {
            dispatcher.Subscribe(
                "fixture:meta-upgrade-purchased",
                new MigratedProducerDomainFactProbeObserver<
                    MetaUpgradePurchaseDomainFact>(
                    MetaProgressionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "fixture:meta-run-result",
                new MigratedProducerDomainFactProbeObserver<
                    MetaRunResultDomainFact>(
                    MetaProgressionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly GameObject host;
        private readonly OwnerRunManager manager;
        private readonly MetaProgressionRuntime meta;
        private readonly DungeonRunFlowRuntime flow;
        private readonly MetaProgressionSaveSection metaSave;
        private readonly RunFlowSaveSection flowSave;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly DomainFactDispatcher domainFacts;
        private readonly OwnerRunCompletionFactEditorFixture ownerFacts;
        private readonly DungeonRuntimeAggregateRootStore store;
        private readonly IOwnerCandidateCatalog ownerCatalog;
        private readonly IOwnerCharacterFactory ownerFactory;
        private readonly IGameEventBus events;
        private readonly IRunSeedProvider runSeed;
        private readonly ResourceGameContentCatalog content;
        private readonly CharacterSO archetype;
        private readonly MetaUpgradeDefinition upgrade;
        private readonly string initialName;
        private bool disposed;
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal OwnerRunManager Manager => manager;
        internal MigratedProducerOutcomeEditorFixture Outcomes => outcomes;
        internal DungeonRuntimeAggregateRootStore Store => store;
        internal OwnerRunCompletionFactEditorFixture DomainFacts => ownerFacts;
        internal RunFlowSaveSection FlowSave => flowSave;
        internal MetaProgressionRuntime Meta => meta;
        internal MetaProgressionSaveSection MetaSave => metaSave;
        private CharacterActor Actor => manager.CurrentOwnerActor;

        internal Context(
            string ownerId,
            JObject checkpoint = null,
            IGameClock clock = null)
        {
            this.ownerId = CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            store = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ProductionCausalOwnerCheckpoint.LedgerRunId(ownerId, OwnerKind), store);
            Probe = new MigratedProducerOutcomeProbeTransaction(outcomes.Transaction, outcomes.Recorder);
            domainFacts = CreateMetaFacts(outcomes, Probe);
            ownerFacts = new OwnerRunCompletionFactEditorFixture(outcomes, Probe);
            host = new GameObject("Causal owner run completion");
            try
            {
                content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
                IMetaUpgradeDefinitionCatalog catalog = new AuthoredGameplayCatalog(content);
                upgrade = catalog.Require(UpgradeId);
                Require(upgrade.cost == 80, "Authored upgrade cost changed; rebind expected evidence.");
                archetype = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                    "Assets/Resources/SO/Character/Owners/Owner_Slime.asset")
                    ?? throw new InvalidOperationException("Authored owner is missing.");
                events = new GameEventBus();
                ownerCatalog = new OwnerCatalog(archetype);
                ownerFactory = new OwnerFactory();
                runSeed = new FixedSeed();
                manager = host.AddComponent<OwnerRunManager>();
                // Owner construction precedes meta setup, but completion is
                // intentionally unavailable until the real meta participant is bound.
                manager.ConstructOwnerRunManager(
                    new DungeonStory.Foundation.UnityUiClock(),
                    ownerCatalog,
                    ownerFactory,
                    events);
                manager.SelectOwner(archetype);
                initialName = Actor.Identity.DisplayName;
                var invasion = new InvasionSceneRuntimeReferences(
                    host.AddComponent<InvasionThreatRuntime>(), host.AddComponent<InvasionDirectorRuntime>(), null);
                meta = host.AddComponent<MetaProgressionRuntime>();
                meta.Construct(new MetaRunResultBuilder(), new MetaRuntimeApplicationAdapter(events,
                    invasion, null, new EmptyCommittedHistory(), new ForbiddenPanel()),
                    clock ?? FrozenClock.Instance, catalog, store);
                meta.ConstructDomainFacts(domainFacts);
                meta.SetShowRunResultPanel(false);
                manager.ConstructOwnerRunManager(
                    new DungeonStory.Foundation.UnityUiClock(),
                    ownerCatalog, ownerFactory,
                    events, ownerFacts.Publisher, runSeed, store, content,
                    ownerFacts.Recovery, meta);
                var pacing = new ExperiencePacingRuntime(new ExperiencePacingApplicationAdapter(events, store, content));
                flow = new DungeonRunFlowRuntime(new OwnerProvider(manager), invasion, events, pacing, store, content);
                flowSave = new RunFlowSaveSection(flow, content);
                metaSave = new MetaProgressionSaveSection(meta);
                if (checkpoint == null)
                {
                    flow.RestoreState(DungeonRunFlowRules.ResolvePhaseForDay(Day, ((ICoreSessionRulesProvider)content).CoreSessionRules),
                        DungeonRunOutcome.None, Day, false, false, 0);
                    events.Publish(new OperatingDayStartedEvent(Day));
                }
                else Restore(checkpoint);
                // Start synchronizes OwnerRunManager.IsRunEnded from canonical run.flow.
                flow.Start();
                Validate();
                Probe.ClearObservations();
            }
            catch { Dispose(); throw; }
        }

        internal int Stage => !manager.IsRunEnded ? 0 : !meta.HasEnded ? -1
            : meta.State.GetUpgradeLevel(UpgradeId) == 0 ? 1 : 2;
        internal bool CompleteRun()
        {
            long nextSequence = outcomes.Ledger.CaptureGameplayOutcomes().nextSequence;
            bool completed = manager.CompleteRun(DungeonRunOutcome.Victory, Reason);
            Validate();
            if (completed)
            {
                Require(Exact()[0].sequence == nextSequence,
                    "Completion reused or skipped the next prepared identity.");
                if (Stage > 0) Require(Exact()[1].sequence == nextSequence + 1,
                    "Settlement did not immediately follow the committed owner identity.");
            }
            return completed;
        }
        internal void BindCompletionTransport(
            IDomainFactPublisher publisher,
            IDomainFactRecoveryPublisher recovery)
        {
            Require(!disposed, "Disposed owner fixture.");
            manager.AssertCompletionIdle();
            manager.ConstructOwnerRunManager(
                new DungeonStory.Foundation.UnityUiClock(),
                ownerCatalog,
                ownerFactory,
                events,
                publisher ?? throw new ArgumentNullException(nameof(publisher)),
                runSeed,
                store,
                content,
                recovery ?? throw new ArgumentNullException(nameof(recovery)),
                meta);
        }
        internal bool PurchaseUpgrade()
        {
            long nextSequence = outcomes.Ledger.CaptureGameplayOutcomes().nextSequence;
            bool purchased = meta.TryPurchaseUpgrade(UpgradeId, out _);
            Validate();
            if (purchased) Require(Exact().Last().sequence == nextSequence,
                "Upgrade reused or skipped the next prepared identity.");
            return purchased;
        }

        internal JArray VerifyRejectedCommand(MigratedProducerOutcomeKind kind, bool purchase)
        {
            var reports = new JArray();
            foreach (var fault in Faults())
            {
                JObject before = Checkpoint();
                JObject expected = fault == MigratedProducerOutcomeFaultMode.RejectCommit
                    ? ExpectedAfterCancelledReservation(before, prepared: true) : RoundTrip(before);
                // Reserve/Cancel deliberately retains diagnostic peak utilization.
                // Check the exact peak, not an ignored field or a broad normalization.
                // Neutral observer commit rejection is after preparation: exactly
                // one identity was allocated and must never be recycled. All
                // committed rows/IDs/outbox and owner state must still match.
                Probe.TargetKind = kind;
                Probe.FaultMode = fault;
                bool accepted;
                try { accepted = purchase ? PurchaseUpgrade() : CompleteRun(); }
                finally { Probe.FaultMode = MigratedProducerOutcomeFaultMode.None; }
                JObject after = Checkpoint();
                if (accepted || !JToken.DeepEquals(expected, after))
                {
                    string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_OWNER_RUN_OUTPUT");
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        WriteNew(Path.Combine(output, "rejection-" + kind + "-" + fault + "-before.json"), before);
                        WriteNew(Path.Combine(output, "rejection-" + kind + "-" + fault + "-after.json"), after);
                    }
                }
                Require(!accepted && JToken.DeepEquals(expected, after),
                    "Rejected command changed its canonical state or ledger: " + kind + "/" + fault
                    + "; differences=" + string.Join(",", after.Descendants().OfType<JValue>()
                        .Where(token => !JToken.DeepEquals(token, expected.SelectToken(token.Path)))
                        .Take(8).Select(token => token.Path + "=" + token)));
                reports.Add(new JObject { ["kind"] = kind.ToString(), ["fault"] = fault.ToString(),
                    ["accepted"] = accepted, ["before"] = before, ["after"] = after,
                    ["canonicalStateAndCommittedLedgerUnchanged"] = true,
                    ["reservationHighWaterExactlyExpected"] = true });
            }
            return reports;
        }

        private JObject ExpectedAfterCancelledReservation(JObject before, bool prepared = false)
        {
            GameplayOutcomeLedgerDiagnostics diagnostics = outcomes.Ledger.GetDiagnostics();
            Require(diagnostics.ReservedCount == 0 && diagnostics.PendingDeliveryCount == 0
                && diagnostics.TombstoneCount == 0 && diagnostics.KnownResultKeyCount == Exact().Count,
                "Reservation projection requires only acknowledged exact rows.");
            JObject expected = RoundTrip(before);
            expected["outcomeLedger"]["knownResultKeyHighWater"] = Math.Max(
                diagnostics.KnownResultKeyHighWater, diagnostics.KnownResultKeyCount + 1);
            // Fresh restore stores acknowledged rows cold, outside the live reservation pool.
            expected["outcomeLedger"]["exactPageUseHighWater"] = Math.Max(
                diagnostics.ExactPageUseHighWater, Exact().Count - diagnostics.DetachedExactCount + 1);
            if (prepared)
                expected["outcomeLedger"]["nextSequence"] = (long)before["outcomeLedger"]["nextSequence"] + 1L;
            return expected;
        }

        internal void VerifyTerminalReplay()
        {
            Require(Stage == 2, "Replay probe requires a completed upgrade.");
            JObject before = Checkpoint();
            Require(!CompleteRun() && JToken.DeepEquals(before, Checkpoint()),
                "Completed owner replay mutated state.");
            // Actual TryPurchaseUpgrade reserves before checking insufficient currency.
            JObject expected = ExpectedAfterCancelledReservation(before);
            Require(!PurchaseUpgrade() && JToken.DeepEquals(expected, Checkpoint()),
                "Rejected terminal upgrade changed more than its reservation diagnostics.");
        }

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            Validate();
            return new Dictionary<string, object>
            {
                ["ownerRun.ownerId"] = ownerId, ["ownerRun.stage"] = Stage,
                ["ownerRun.runSeed"] = Seed, ["ownerRun.absoluteDay"] = flow.CurrentDay,
                ["ownerRun.actorId"] = Actor.Identity.PersistentId,
                ["ownerRun.actorDataId"] = Actor.Identity.Data.id,
                ["ownerRun.actorName"] = Actor.Identity.DisplayName,
                ["ownerRun.phase"] = flow.Phase.ToString(),
                ["ownerRun.outcome"] = flow.Outcome.ToString(),
                ["ownerRun.ownerEnded"] = manager.IsRunEnded,
                ["ownerRun.settlementEnded"] = meta.HasEnded,
                ["ownerRun.completedRunCount"] = meta.State.CompletedRunCount,
                ["ownerRun.earnedCurrency"] = meta.State.LifetimeEarnedCurrency,
                ["ownerRun.spentCurrency"] = meta.State.SpentCurrency,
                ["ownerRun.availableCurrency"] = meta.State.AvailableCurrency,
                ["ownerRun.upgradeId"] = UpgradeId, ["ownerRun.upgradeCost"] = upgrade.cost,
                ["ownerRun.upgradeLevel"] = meta.State.GetUpgradeLevel(UpgradeId),
                ["ownerRun.facilityCandidateBonus"] = meta.GetStartingFacilityCandidateBonus(),
                ["ownerRun.ownerOutcomeCount"] = Count(MigratedProducerOutcomeKind.OwnerRunEndedEvent),
                ["ownerRun.settlementOutcomeCount"] = Count(MigratedProducerOutcomeKind.RunResultReadyEvent),
                ["ownerRun.upgradeOutcomeCount"] = Count(MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent),
                ["ownerRun.exactOutcomeCount"] = Exact().Count
            };
        }

        internal JObject Checkpoint()
        {
            Validate();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(ownerId, OwnerKind, RestoreKind,
                Domain(), outcomes.Ledger);
        }

        private JObject Domain() => new()
        {
            ["schemaVersion"] = 1, ["schema"] = Schema, ["stage"] = Stage,
            ["runSeed"] = Seed, ["upgradeId"] = UpgradeId,
            ["runFlowVersion"] = flowSave.SectionVersion,
            ["metaProgressionVersion"] = metaSave.SectionVersion,
            ["runFlow"] = JObject.Parse(flowSave.Capture()),
            ["metaProgression"] = JObject.Parse(metaSave.Capture()),
            ["actor"] = CaptureActor(),
            ["migratedOutcomeOwners"] = Dto(outcomes.Transaction.Capture())
        };

        private JObject CaptureActor()
        {
            var snapshot = Actor.Progression.CapturePersistentState();
            return new JObject
            {
                ["persistentId"] = Actor.Identity.PersistentId, ["dataId"] = Actor.Identity.Data.id,
                ["isOwner"] = Actor.IsOwner, ["displayName"] = Actor.Identity.DisplayName,
                ["characterType"] = (int)Actor.Identity.CharacterType, ["role"] = (int)Actor.Identity.Role,
                ["level"] = snapshot.Level, ["currentExperience"] = snapshot.CurrentExperience,
                ["growth"] = Dto(snapshot.GrowthState), ["narrative"] = Dto(snapshot.NarrativeLedger),
                ["acquiredTraits"] = Dto(snapshot.AcquiredTraitState)
            };
        }

        private void Restore(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint, ownerId, OwnerKind, RestoreKind, outcomes.Ledger);
            RequireFields(state, "schemaVersion", "schema", "stage", "runSeed", "upgradeId",
                "runFlowVersion", "metaProgressionVersion", "runFlow", "metaProgression", "actor", "migratedOutcomeOwners");
            Require((int)state["schemaVersion"] == 1 && (string)state["schema"] == Schema
                && (int)state["runSeed"] == Seed && (string)state["upgradeId"] == UpgradeId
                && (int)state["runFlowVersion"] == flowSave.SectionVersion
                && (int)state["metaProgressionVersion"] == metaSave.SectionVersion,
                "Saved run identity/version changed.");
            var owners = ReadDto<DungeonMigratedProducerOutcomeSaveData>(state["migratedOutcomeOwners"]);
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(owners));
            JObject actor = state["actor"] as JObject ?? throw new InvalidOperationException("Missing actor.");
            RequireFields(actor, "persistentId", "dataId", "isOwner", "displayName", "characterType", "role",
                "level", "currentExperience", "growth", "narrative", "acquiredTraits");
            Require((string)actor["persistentId"] == CharacterId.Owner.Value
                && (int)actor["dataId"] == archetype.id && (bool)actor["isOwner"]
                && (int)actor["characterType"] == (int)Actor.Identity.CharacterType
                && (int)actor["role"] == (int)CharacterRole.Owner,
                "Canonical owner character binding changed.");
            Actor.Progression.RestorePersistentState(new CharacterProgressionSnapshot(
                (int)actor["level"], (int)actor["currentExperience"],
                ReadDto<CharacterGrowthState>(actor["growth"]),
                ReadDto<CharacterNarrativeLedger>(actor["narrative"]),
                ReadDto<CharacterAcquiredTraitAggregateState>(actor["acquiredTraits"])));
            Require(JToken.DeepEquals(actor, CaptureActor()), "Actor identity/progression did not round-trip.");
            var report = new DungeonGameRestoreReport();
            IDungeonSaveRestoreStage metaStage = metaSave.StageRestore(state["metaProgression"].ToString(Formatting.None),
                metaSave.SectionVersion, report);
            IDungeonSaveRestoreStage flowStage = flowSave.StageRestore(state["runFlow"].ToString(Formatting.None),
                flowSave.SectionVersion, report);
            Require(report.Success, "Canonical save staging failed.");
            metaStage.Commit(report);
            flowStage.Commit(report);
            flow.Start();
            Require(report.Success && (int)state["stage"] == Stage
                && JToken.DeepEquals(state, Domain()), "Canonical restore changed the saved owner domain.");
        }

        internal GameplayOutcomeReplayIdentity RequireExact(MigratedProducerOutcomeKind kind)
        {
            GameplayOutcomeSnapshot exact = Exact().Single(value => value.outcomeTypeId == Type(kind));
            var key = new GameplayResultKey(exact.producerId,
                new GameplayOperationId(exact.operationId), exact.commitRevision, exact.localResultIndex);
            Require(outcomes.Ledger.TryGetResultIdentity(key, out var identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash, "Canonical owner-run outcome identity is missing.");
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JArray OrderedEvidence()
        {
            var records = new JArray();
            string previous = null;
            foreach (GameplayOutcomeSnapshot exact in Exact())
            {
                var key = new GameplayResultKey(exact.producerId,
                    new GameplayOperationId(exact.operationId), exact.commitRevision, exact.localResultIndex);
                Require(outcomes.Ledger.TryGetResultIdentity(key, out var identity)
                    && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                    && identity.HasCanonicalPayloadHash, "Exact run outcome lost its canonical identity.");
                JObject evidence = ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);
                records.Add(new JObject
                {
                    ["outcomeEvidence"] = evidence,
                    ["relation"] = exact.outcomeTypeId == Type(MigratedProducerOutcomeKind.RunResultReadyEvent)
                        ? "post-commit-observer" : previous == null ? "root-command" : "restored-resource-consumption",
                    ["predecessorOutcomeId"] = previous == null ? JValue.CreateNull() : new JValue(previous),
                    ["atomicSibling"] = false
                });
                previous = (string)evidence["outcomeId"];
            }
            return records;
        }

        private List<GameplayOutcomeSnapshot> Exact() => outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes
            .OrderBy(value => value.sequence).ToList();
        private int Count(MigratedProducerOutcomeKind kind) => Exact().Count(value => value.outcomeTypeId == Type(kind));

        internal void Validate()
        {
            Require(!disposed, "Disposed owner fixture.");
            int stage = Stage;
            bool ownerEnded = stage != 0;
            bool settled = stage > 0;
            bool purchased = stage == 2;
            Require(stage is >= -1 and <= 2 && Actor != null && Actor.IsOwner
                && Actor.Identity.PersistentId == CharacterId.Owner.Value && Actor.Identity.Data == archetype
                && Actor.Identity.DisplayName == initialName
                && flow.CurrentDay == Day && meta.RunProgress.CurrentDay == Day
                && flow.Outcome == (ownerEnded ? DungeonRunOutcome.Victory : DungeonRunOutcome.None)
                && (!ownerEnded || flow.Phase == DungeonRunPhase.Finished)
                && !flow.IsBossArmed && !flow.IsBossActive && flow.BossCycle == 0
                && meta.HasEnded == settled && meta.State.CompletedRunCount == (settled ? 1 : 0)
                && meta.State.LifetimeEarnedCurrency == (settled ? ExpectedCurrency : 0)
                && meta.State.SpentCurrency == (purchased ? upgrade.cost : 0)
                && meta.State.AvailableCurrency == (settled ? ExpectedCurrency : 0) - (purchased ? upgrade.cost : 0)
                && meta.State.GetUpgradeLevel(UpgradeId) == (purchased ? 1 : 0)
                && meta.GetStartingFacilityCandidateBonus() == (purchased ? 1 : 0),
                "Owner run, canonical flow, settlement, upgrade or identity diverged.");
            Require(Count(MigratedProducerOutcomeKind.OwnerRunEndedEvent) == (ownerEnded ? 1 : 0)
                && Count(MigratedProducerOutcomeKind.RunResultReadyEvent) == (settled ? 1 : 0)
                && Count(MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent) == (purchased ? 1 : 0)
                && Exact().Count == (ownerEnded ? 1 : 0) + (settled ? 1 : 0) + (purchased ? 1 : 0),
                "Owner run exact result counts diverged.");
            var owners = outcomes.Transaction.Capture().owners;
            Require(owners.Count == Exact().Count && owners.All(value => value.revision == 1),
                "Migrated outcome owners changed revision.");
            if (settled)
            {
                var result = meta.LatestResult;
                Require(result != null && result.ownerName == Actor.Identity.DisplayName
                    && result.endReason == Reason && result.outcome == DungeonRunOutcome.Victory
                    && result.survivedOperatingDays == Day && result.survivalSeconds == 0
                    && result.legacyCurrency == ExpectedCurrency && result.settlementCount == 0
                    && result.completedMilestoneIds.Count == 0 && result.committedChoices.Count == 0,
                    "Live event settlement contains unprovided history.");
            }
            else Require(meta.LatestResult == null, "Unsettled owner contains a settlement result.");
            var expectedKinds = new List<MigratedProducerOutcomeKind>();
            if (ownerEnded) expectedKinds.Add(MigratedProducerOutcomeKind.OwnerRunEndedEvent);
            if (settled) expectedKinds.Add(MigratedProducerOutcomeKind.RunResultReadyEvent);
            if (purchased) expectedKinds.Add(MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent);
            var records = Exact();
            for (int index = 0; index < records.Count; index++)
            {
                GameplayOutcomeSnapshot value = records[index];
                var kind = expectedKinds[index];
                string expectedSubject = kind == MigratedProducerOutcomeKind.OwnerRunEndedEvent ? CharacterId.Owner.Value
                    : kind == MigratedProducerOutcomeKind.RunResultReadyEvent ? "meta-run-result:sequence:1"
                    : "meta-upgrade-purchase:" + UpgradeId;
                string expectedName = kind == MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent
                    ? upgrade.title : Actor.Identity.DisplayName;
                Require(value.outcomeTypeId == Type(kind) && value.absoluteDay == Day
                    && value.status == GameplayOutcomeStatus.Succeeded && value.ownerRevision == 1
                    && value.sequence > (index == 0 ? 0 : records[index - 1].sequence)
                    && value.sequence < outcomes.Ledger.CaptureGameplayOutcomes().nextSequence
                    && value.participants.Count == 1 && value.subjects.Count == 1
                    && value.participants[0].entityId == expectedSubject
                    && value.subjects[0].entityId == expectedSubject
                    && value.participants[0].displayText == expectedName,
                    "Ordered run payload identity, subject, status or day changed: " + kind);
            }
            OrderedEvidence();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ownerFacts?.Dispose();
            domainFacts?.Dispose();
            flow?.Dispose();
            if (manager?.CurrentOwnerActor != null) Object.DestroyImmediate(manager.CurrentOwnerActor.gameObject);
            if (host != null) Object.DestroyImmediate(host);
        }
    }

    private sealed class OwnerCatalog : IOwnerCandidateCatalog
    {
        private readonly CharacterSO owner;
        internal OwnerCatalog(CharacterSO owner) => this.owner = owner;
        public IReadOnlyCollection<CharacterSO> OwnerCandidates => new[] { owner };
    }

    // Only actor construction is controlled. Completion and event handling are production code.
    private sealed class OwnerFactory : IOwnerCharacterFactory
    {
        public CharacterActor CreateOwner(CharacterSO data, GameObject prefab, Transform point, Vector2Int cell)
        {
            var gameObject = new GameObject("Causal authored owner");
            try
            {
                gameObject.AddComponent<SpriteRenderer>();
                var actor = gameObject.AddComponent<CharacterActor>();
                gameObject.GetComponent<CharacterIdentity>().SetPersistentId(CharacterId.Owner);
                gameObject.AddComponent<AbilityMove>();
                gameObject.AddComponent<AbilityWork>();
                gameObject.AddComponent<AbilityShopping>();
                gameObject.AddComponent<AIBrain>();
                CharacterAiEditorTestDependencies.Inject(gameObject);
                actor.RefreshAbilityCache();
                actor.Initialization(data);
                actor.SetLifecycleState(CharacterLifecycleState.Active);
                actor.Brain.UseOwnerWorkActions();
                return actor;
            }
            catch { Object.DestroyImmediate(gameObject); throw; }
        }
        public CharacterActor CreateOwnerDetached(CharacterSO data, GameObject prefab) =>
            throw new NotSupportedException("This scoped fixture does not restore a character world.");
    }

    private sealed class OwnerProvider : IOwnerRunManagerProvider
    {
        private readonly OwnerRunManager manager;
        internal OwnerProvider(OwnerRunManager manager) => this.manager = manager;
        public bool TryGetManager(out OwnerRunManager value) { value = manager; return value != null; }
    }
    private sealed class FixedSeed : IRunSeedProvider { public int RunSeed => Seed; }
    private sealed class FrozenClock : IGameClock
    {
        internal static readonly FrozenClock Instance = new();
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
    private sealed class EmptyCommittedHistory : ICommittedRunResultQuery
    {
        public CommittedRunResultSnapshot CaptureCommittedRunResult() =>
            new(Array.Empty<string>(), Array.Empty<CommittedRunChoiceSnapshot>());
    }
    private sealed class ForbiddenPanel : IRunResultPanelService
    {
        public RunResultPanel Show(RunResultSnapshot result) =>
            throw new InvalidOperationException("Headless causal verification must not open UI.");
    }

    private static string Type(MigratedProducerOutcomeKind kind) =>
        MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId.Value;
    private static MigratedProducerOutcomeFaultMode[] Faults() => new[]
        { MigratedProducerOutcomeFaultMode.RejectReservation, MigratedProducerOutcomeFaultMode.RejectCommit };
    private static JObject Dto(object value) => JObject.Parse(JsonUtility.ToJson(value));
    private static T ReadDto<T>(JToken value) => JsonUtility.FromJson<T>(value.ToString(Formatting.None));
    private static JObject RoundTrip(JObject value) => JObject.Parse(value.ToString(Formatting.None));
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
    private static void RequireFields(JObject value, params string[] fields) =>
        Require(value.Properties().Select(item => item.Name).OrderBy(item => item)
            .SequenceEqual(fields.OrderBy(item => item)), "Unexpected saved owner fields.");
    private static string RequiredEnvironment(string name)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(value) ? Path.GetFullPath(value)
            : throw new InvalidOperationException("Missing environment path: " + name);
    }
    private static void WriteNew(string path, JObject value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        writer.Write(value.ToString(Formatting.Indented));
        writer.Write('\n');
    }
}
#endif
