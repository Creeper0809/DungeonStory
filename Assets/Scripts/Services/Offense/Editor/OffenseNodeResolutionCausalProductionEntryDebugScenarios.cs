#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Characters;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled production-entry execution, not natural telemetry or a full-world save.
// Departure is an initial condition; battles, target rewards and arrivals are not executed.
public static class OffenseNodeResolutionCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind = "owner-family:offense-node-resolution";
    internal const string ClearApproachActionId = "action:offense-clear-approach-with-tools";
    internal const string SecureCacheActionId = "action:offense-secure-supply-cache";
    private const string OwnerKind = "owner-kind:offense-node-resolution";
    private const string Schema = "dungeonstory-causal-offense-node-resolution-owner-state@2";
    private const string TargetId = "food_farm";
    private const string ExpeditionId = "expedition:causal-node:farm";
    private const string ActorId = "character:causal-node:scout";
    private const string ApproachId = TargetId + ":approach-event";
    private const string CacheId = TargetId + ":cache";
    private const int AbsoluteDay = 46;
    private const MigratedProducerOutcomeKind Kind = MigratedProducerOutcomeKind.OffenseExpeditionNodeResult;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId, string ownerId = null, JObject ownerCheckpoint = null)
    {
        CausalGameplaySyntax.RequireId(causalRunId, nameof(causalRunId));
        if (string.IsNullOrWhiteSpace(ownerId) && ownerCheckpoint != null)
            throw new ArgumentException("A node checkpoint requires an owner ID.");
        var context = new Context(causalRunId, ownerId, ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Make(ClearApproachActionId, 1, context.CaptureState),
            Make(SecureCacheActionId, 2, () => new Dictionary<string, object>())
        };
        IProductionCausalActionFixture Make(string actionId, int revision,
            Func<IReadOnlyDictionary<string, object>> capture) =>
            new DelegateProductionCausalActionFixture(actionId, "offense",
                MigratedProducerOutcomeCatalog.Get(Kind).OutcomeTypeId,
                capture, () => context.Execute(revision),
                () => context.RequireOutcome(revision), context.CaptureEvidence,
                lifetime.Release, context.OwnerId,
                context.Persistent ? OwnerKind : string.Empty,
                context.Persistent ? context.CaptureOwnerCheckpoint : null,
                context.Persistent ? CausalOwnerRestoreKind : null,
                context.Persistent ? context : null);
    }

    internal static bool RunCausalOwnerFamily(bool log = false, Action<JObject> reportSink = null)
    {
        const string owner = "owner:causal-node:focused";
        JObject checkpoint = null;
        var observations = new JArray();
        for (int revision = 1; revision <= 2; revision++)
        {
            using var context = new Context("run:causal-node:step-" + revision, owner, checkpoint);
            JObject before = JObject.FromObject(context.CaptureState());
            context.AssertRejectedCommandDoesNotMutate(revision);
            Require(context.Execute(revision), "Production node resolution was rejected.");
            context.RequireOutcome(revision);
            checkpoint = context.CaptureOwnerCheckpoint();
            observations.Add(new JObject
            {
                ["revision"] = revision, ["before"] = before,
                ["after"] = JObject.FromObject(context.CaptureState()),
                ["exactEvidence"] = context.CaptureEvidence(context.RequireOutcome(revision))
            });
            if (revision == 1)
            {
                RequireTamperRejected(owner, checkpoint, value =>
                    ((JObject)((JObject)value["domainState"])["progression"])["currentExperience"] = -1);
                RequireTamperRejected(owner, checkpoint, value =>
                    ((JArray)((JObject)((JObject)value["domainState"])["migratedOutcomeOwners"])["owners"]).Clear());
                RequireTamperRejected(owner, checkpoint, value =>
                    ((JArray)((JObject)((JArray)((JObject)((JObject)value["domainState"])["offense"])["activeExpeditions"])[0])["completedNodeIds"]).RemoveAt(1));
                RequireTamperRejected(owner, checkpoint, value =>
                    ((JArray)((JObject)((JObject)value["domainState"])["campaign"])["knownTargetIds"]).Clear());
                RequireTamperRejected(owner, checkpoint, value =>
                    ((JArray)((JObject)((JArray)((JObject)((JObject)value["domainState"])["offense"])["activeExpeditions"])[0])["consumedSupplies"]).Clear());
            }
        }
        using (var restored = new Context("run:causal-node:terminal", owner, checkpoint))
        {
            JObject before = restored.CaptureOwnerCheckpoint();
            Require(!restored.Execute(1) && !restored.Execute(2), "Resolved nodes replayed.");
            Require(JToken.DeepEquals(before, restored.CaptureOwnerCheckpoint()), "Replay changed checkpoint.");
        }
        if (log) Debug.Log("Offense approach -> canonical save/fresh composition -> cache causal owner PASS");
        reportSink?.Invoke(new JObject
        {
            ["schema"] = "dungeonstory-offense-node-direct-probe@1", ["status"] = "PASS",
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false,
            ["naturalPlayTelemetry"] = false, ["fullWorldSaveClaimed"] = false,
            ["observations"] = observations, ["terminalCheckpoint"] = checkpoint,
            ["reservationRejectionChecks"] = 2, ["commitRejectionChecks"] = 2,
            ["semanticTamperChecks"] = 5, ["replayRejected"] = true
        });
        return true;
    }

    public static void RunCausalBatch() => RunCausalOwnerFamily(log: true, reportSink: value =>
        WriteNew(EnvironmentPath("DUNGEONSTORY_OFFENSE_NODE_REPORT_PATH"), value));

    public static void RunCausalAndWriteFirstBatch()
    {
        RunCausalBatch();
        WriteFirstCheckpointBatch();
    }

    public static void WriteFirstCheckpointBatch()
    {
        using var context = new Context("run:causal-node:fresh-first", "owner:causal-node:fresh-process", null);
        context.AssertRejectedCommandDoesNotMutate(1);
        Require(context.Execute(1), "First fresh-process node failed.");
        WriteNew(EnvironmentPath("DUNGEONSTORY_OFFENSE_NODE_CHECKPOINT_PATH"), context.CaptureOwnerCheckpoint());
        Debug.Log("Offense first-node fresh-process checkpoint PASS");
    }

    public static void ContinueCheckpointBatch()
    {
        JObject saved = JObject.Parse(File.ReadAllText(EnvironmentPath("DUNGEONSTORY_OFFENSE_NODE_CHECKPOINT_PATH")));
        using var context = new Context("run:causal-node:fresh-second", "owner:causal-node:fresh-process", saved);
        JObject before = JObject.FromObject(context.CaptureState());
        context.AssertRejectedCommandDoesNotMutate(2);
        Require(!context.Execute(1) && context.Execute(2), "Fresh-process continuation did not resolve only cache.");
        JObject terminal = context.CaptureOwnerCheckpoint();
        using var restored = new Context("run:causal-node:fresh-terminal", "owner:causal-node:fresh-process", terminal);
        JObject beforeReplay = restored.CaptureOwnerCheckpoint();
        Require((long)beforeReplay["outcomeLedger"]["worldEpoch"]
            == (long)terminal["outcomeLedger"]["worldEpoch"] + 1L,
            "Fresh composition did not advance the ledger restore epoch.");
        Require(!restored.Execute(1) && !restored.Execute(2)
            && JToken.DeepEquals(beforeReplay, restored.CaptureOwnerCheckpoint()), "Fresh-process terminal replay changed state.");
        WriteNew(EnvironmentPath("DUNGEONSTORY_OFFENSE_NODE_REPORT_PATH"), new JObject
        {
            ["schema"] = "dungeonstory-offense-node-fresh-process-probe@1", ["status"] = "PASS",
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false,
            ["before"] = before, ["after"] = JObject.FromObject(context.CaptureState()),
            ["exactEvidence"] = context.CaptureEvidence(context.RequireOutcome(2)),
            ["terminalCheckpoint"] = terminal
        });
        Debug.Log("Offense second-node restored fresh-process continuation PASS");
    }

    private static string EnvironmentPath(string key)
    {
        string value = Environment.GetEnvironmentVariable(key);
        Require(!string.IsNullOrWhiteSpace(value), "Required output path missing: " + key);
        return Path.GetFullPath(value);
    }
    private static void WriteNew(string path, JObject value)
    {
        using var output = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write));
        output.Write(value.ToString());
    }

    private static void RequireTamperRejected(string owner, JObject checkpoint, Action<JObject> mutate)
    {
        var altered = (JObject)checkpoint.DeepClone();
        mutate(altered);
        bool rejected = false;
        try { using var ignored = new Context("run:causal-node:tamper", owner, altered); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Semantic node checkpoint tamper was accepted.");
    }

    private sealed class Context : IDisposable
    {
        private readonly GameObject root;
        private readonly GameObject actorObject;
        private readonly CharacterActor actor;
        private readonly OffenseExpeditionRuntime runtime;
        private readonly OffenseBattleRuntime battle;
        private readonly OffenseCampaignRuntime campaign;
        private readonly OffenseSaveService save;
        private readonly MigratedProducerOutcomeEditorFixture outcome;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly EditorOffenseNodeFactFixture nodeFacts;
        private readonly DomainFactDispatcher battleFacts;
        private readonly IDisposable battleCommandBinding;
        private readonly IDisposable skillActivationBinding;
        private readonly OffenseBattleCommandPublicationRuntime
            battleCommandPublications;
        private readonly CharacterSkillActivationPublicationRuntime
            skillActivationPublications;
        private bool disposed;

        internal Context(string runId, string ownerId, JObject checkpoint)
        {
            OwnerId = string.IsNullOrWhiteSpace(ownerId) ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            try
            {
                outcome = new MigratedProducerOutcomeEditorFixture(
                    Persistent
                        ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                            OwnerId,
                            OwnerKind)
                        : runId + ":node-ledger",
                    includeCharacterSkillActivation: true);
                probe = new MigratedProducerOutcomeProbeTransaction(outcome.Transaction, outcome.Recorder)
                { TargetKind = Kind };
                battleFacts = new DomainFactDispatcher();
                var battleCommits = new GameplayDomainFactCommitGroup(
                    outcome.Recorder,
                    outcome.Ledger);
                battleCommandBinding = new OffenseBattleCommandFactObserver(
                        outcome.Transaction,
                        battleCommits)
                    .Connect(battleFacts);
                skillActivationBinding = CharacterSkillActivationFactMapping
                    .CreateBinding(
                        outcome.Registry,
                        outcome.Ledger,
                        outcome.Recorder,
                        battleCommits)
                    .Connect(battleFacts);
                battleCommandPublications =
                    new OffenseBattleCommandPublicationRuntime(
                        battleFacts,
                        battleFacts);
                skillActivationPublications =
                    new CharacterSkillActivationPublicationRuntime(
                        battleFacts,
                        battleFacts);
                var events = new GameEventBus();
                root = new GameObject("Causal canonical expedition node owner");
                runtime = root.AddComponent<OffenseExpeditionRuntime>();
                var rewards = root.AddComponent<OffenseRewardRuntime>();
                actorObject = new GameObject("원정 정찰대원");
                actorObject.AddComponent<SpriteRenderer>();
                CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
                actor = actorObject.AddComponent<CharacterActor>();
                actorObject.AddComponent<AbilityMove>();
                actorObject.AddComponent<AbilityWork>();
                actor.RefreshAbilityCache();
                CharacterAiEditorTestDependencies.Inject(actorObject);
                actor.Identity.SetPersistentId(ActorId);
                actor.Initialization(OffenseEditorTestDependencies.RequireCharacterArchetype("Orc"));
                actor.characterType = CharacterType.NPC;
                actor.SetLifecycleState(CharacterLifecycleState.Active);
                actor.BeginExpedition();
                var lookup = new StagedCharacterLookup(actor);
                nodeFacts = new EditorOffenseNodeFactFixture(
                    events,
                    outcome.Transaction,
                    outcome.Recorder,
                    outcome.Ledger,
                    probe,
                    lookup);
                runtime.ConstructNodePublications(
                    new FixedCalendar(),
                    nodeFacts.Publications);
                ICombatEquipmentRuntime equipment = OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
                battle = new OffenseBattleRuntime(lookup,
                    EditorRuntimeReferenceFixtures.DungeonWithRunVariables, events,
                    OffenseEditorTestDependencies.CreateCombatResolution(), equipment, null, null, null,
                    OffenseEditorTestDependencies.CreateEnemyEncounterFactory(equipment),
                    new EnemyTacticalDecisionService(new EnemyCombatContentCatalog(
                        new ResourceGameContentCatalog(new UnityGameContentRootLoader()))),
                    new UnusedArrivalPort(), CharacterAiEditorTestDependencies.NeutralPerformance);
                var battleCalendar = new FixedCalendar();
                battle.ConstructProficiencyProgression(
                    null,
                    battleCalendar,
                    battleCommandPublications);
                battle.ConstructSkillActivationRuntime(
                    new CharacterSkillActivationTransaction(
                        new CharacterSkillActivationEffectExecutor(),
                        battleCalendar,
                        battleFacts,
                        skillActivationPublications));
                var catalog = OffenseEditorTestDependencies.CreateCampaignCatalog();
                Require(catalog.TryGet(TargetId, out var target), "Authored food_farm target missing.");
                campaign = new OffenseCampaignRuntime();
                campaign.ConfigureTargets(catalog.Targets);
                campaign.Reset(0);
                OffenseWorldMapService.RevealTargetsInRange(campaign.MutableState, catalog.Targets);
                save = new OffenseSaveService(new OffenseSceneRuntimeReferences(null, rewards, runtime, null, null),
                    catalog, lookup, battle);
                if (checkpoint == null)
                {
                    var run = new OffenseExpeditionRun(ExpeditionId, target, new[] { actor }, 14f, target.durationSeconds,
                        OffenseRouteGenerator.Create(target),
                        new OffenseSupplyLoadout(new Dictionary<OffenseSupplyType, int>
                        { [OffenseSupplyType.Rations] = 4, [OffenseSupplyType.Tools] = 1 }),
                        new OffenseExpeditionPreparation());
                    // The actor has already departed; persist that same initial condition.
                    run.MarkDepartureCompleted();
                    runtime.PublishPersistentState(new List<OffenseExpeditionRun> { run }, new List<OffenseExpeditionResult>());
                    Require(runtime.TryChooseRouteNode(ExpeditionId, ApproachId, out _), "Initial authored route entry failed.");
                }
                else Restore(checkpoint);
                ValidateState();
            }
            catch { Dispose(); throw; }
        }

        internal string OwnerId { get; }
        internal bool Persistent => OwnerId.Length > 0;
        private OffenseExpeditionRun Run => runtime.ActiveExpeditions.Single();
        private int Stage => Run.CompletedNodeIds.Contains(CacheId) ? 2
            : Run.CompletedNodeIds.Contains(ApproachId) ? 1 : 0;
        private int OutcomeCount => outcome.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ValidateState();
            return new Dictionary<string, object>
            {
                ["offenseNode.ownerId"] = Persistent ? OwnerId : "owner:ephemeral-offense-node",
                ["offenseNode.stage"] = Stage,
                ["offenseNode.absoluteDay"] = AbsoluteDay,
                ["offenseNode.expeditionId"] = ExpeditionId,
                ["offenseNode.actorId"] = ActorId,
                ["offenseNode.targetKnown"] = campaign.State.KnowTarget(TargetId),
                ["offenseNode.currentNodeId"] = Run.CurrentNodeId,
                ["offenseNode.completedNodeCount"] = Run.CompletedNodeIds.Count,
                ["offenseNode.light"] = (double)Run.Light,
                ["offenseNode.rations"] = Run.Supplies.Get(OffenseSupplyType.Rations),
                ["offenseNode.tools"] = Run.Supplies.Get(OffenseSupplyType.Tools),
                ["offenseNode.consumedRations"] = Consumed(OffenseSupplyType.Rations),
                ["offenseNode.consumedTools"] = Consumed(OffenseSupplyType.Tools),
                ["offenseNode.generalLoot"] = Stock(StockCategory.General),
                ["offenseNode.foodLoot"] = Stock(StockCategory.Food),
                ["offenseNode.actorLevel"] = actor.Progression.Level,
                ["offenseNode.actorExperience"] = actor.Progression.CurrentExperience,
                ["offenseNode.actorStress"] = (double)Run.MemberStates[0].Stress,
                ["offenseNode.actorNarrativeCount"] = NodeFacts().Count,
                ["offenseNode.exactOutcomeCount"] = OutcomeCount
            };
        }

        internal bool Execute(int revision)
        {
            if (revision != Stage + 1 || revision > 2) return false;
            bool resolved = runtime.TryResolveCurrentNode(ExpeditionId, revision == 1, out var result, out _);
            if (!resolved) return false;
            Require(result.GainedLoot && result.UsedSupply == (revision == 1), "Node result flags disagree.");
            // Arrival at the second node is part of the first action's observed transition.
            // It occurs after the first durable result; commit-fault tests do not claim to roll back this later command.
            if (revision == 1)
                Require(runtime.TryChooseRouteNode(ExpeditionId, CacheId, out _), "Authored cache entry failed.");
            ValidateState();
            return true;
        }

        internal void AssertRejectedCommandDoesNotMutate(int revision)
        {
            JObject before = CaptureDomainState();
            foreach (var fault in new[] { MigratedProducerOutcomeFaultMode.RejectReservation,
                         MigratedProducerOutcomeFaultMode.RejectCommit })
            {
                probe.FaultMode = fault;
                bool rejected = false;
                try { Execute(revision); }
                catch (InvalidOperationException) { rejected = true; }
                finally { probe.FaultMode = MigratedProducerOutcomeFaultMode.None; }
                Require(rejected && JToken.DeepEquals(before, CaptureDomainState()) && OutcomeCount == revision - 1,
                    "Rejected node resolution changed journey, progression, owner or exact ledger.");
                ValidateState();
            }
        }

        internal GameplayOutcomeReplayIdentity RequireOutcome(int revision)
        {
            var definition = MigratedProducerOutcomeCatalog.Get(Kind);
            string hash = NarrativeInferenceHash.ComputeSha256Utf8(
                definition.ProducerId + "|offense-expedition-node:" + ExpeditionId).Substring(0, 32);
            var key = new GameplayResultKey(definition.ProducerId,
                new GameplayOperationId("migrated:" + (int)Kind + ":" + hash + ":" + revision), revision, 0);
            Require(outcome.Ledger.TryGetResultIdentity(key, out var identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash, "Exact node result is absent or unacknowledged.");
            Require(outcome.Ledger.TryGetExact(identity.OutcomeId, out var exact)
                && exact.outcomeTypeId == definition.OutcomeTypeId.Value
                && exact.ownerRevision == revision, "Exact node identity changed.");
            string nodeId = revision == 1 ? ApproachId : CacheId;
            string nodeKind = revision == 1 ? "Event" : "Cache";
            string summary = "원정 지점 해결: expedition=" + ExpeditionId
                + "; node=" + nodeId + "; kind=" + nodeKind;
            Require(exact.absoluteDay == AbsoluteDay && exact.status == GameplayOutcomeStatus.Succeeded
                && exact.participants.Count == 6 && exact.subjects.Count == 6
                && exact.participants[0].entityKindId == "expedition"
                && exact.participants[0].entityId == ExpeditionId
                && exact.participants[0].roleId == "expedition"
                && exact.participants[0].displayText == Run.Target.title
                && exact.subjects.Any(value => value.entityKindId == "expedition"
                    && value.entityId == ExpeditionId)
                && exact.participants.Any(value => value.entityKindId == "world-location"
                    && value.entityId == TargetId && value.roleId == "target")
                && exact.participants.Any(value => value.entityKindId == "world-location"
                    && value.entityId == "border-trade"
                    && value.roleId == "destination")
                && exact.participants.Any(value => value.entityKindId == "faction"
                    && value.entityId == "human" && value.roleId == "faction")
                && exact.participants.Any(value => value.entityKindId == "world-location"
                    && value.entityId == nodeId && value.roleId == "target")
                && exact.participants.Any(value => value.entityKindId == "character"
                    && value.entityId == ActorId && value.roleId == "member")
                && exact.metrics.Count == 2
                && exact.metrics.Any(value => value.metricId == "status"
                    && value.referenceId == ExpeditionId)
                && exact.metrics.Any(value => value.metricId == "output-quantity"
                    && value.referenceId == ExpeditionId && value.value == 1d)
                && exact.facts.Count == 4
                && exact.facts.Count(f => f.factId == "summary" && f.value == summary) == 1
                && exact.facts.Count(f => f.factId == "source-type" && f.value == nameof(OffenseExpeditionNodeResult)) == 1
                && exact.facts.Count(f => f.factId == "node-id" && f.value == nodeId) == 1
                && exact.facts.Count(f => f.factId == "detail"
                    && f.value.Contains("kind=" + nodeKind, StringComparison.Ordinal)
                    && f.value.Contains("gainedLoot=True", StringComparison.Ordinal)) == 1
                && exact.rootOperationId == ExpeditionId
                && exact.causationRelationId == "expedition-node-resolved",
                "Node payload day, subject, source or summary does not match the actual resolved node: "
                + JsonUtility.ToJson(exact));
            return identity;
        }

        internal JObject CaptureEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcome.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            Require(Persistent, "Ephemeral node fixture cannot create owner checkpoint.");
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(OwnerId, OwnerKind,
                CausalOwnerRestoreKind, CaptureDomainState(), outcome.Ledger);
        }

        internal JObject CaptureDomainState() => new()
        {
            ["schemaVersion"] = 2, ["schema"] = Schema, ["stage"] = Stage,
            ["absoluteDay"] = AbsoluteDay, ["actorId"] = ActorId,
            ["offense"] = Dto(save.Capture()), ["campaign"] = Dto(campaign.Capture()),
            ["progression"] = Dto(ProgressionState.Capture(actor.Progression)),
            ["migratedOutcomeOwners"] = Dto(outcome.Transaction.Capture()),
            ["nodePublications"] = Dto(nodeFacts.Publications.Capture())
        };

        private void Restore(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(checkpoint, OwnerId,
                OwnerKind, CausalOwnerRestoreKind, outcome.Ledger);
            Require(state.Properties().Select(p => p.Name).OrderBy(x => x).SequenceEqual(new[]
                { "schemaVersion", "schema", "stage", "absoluteDay", "actorId", "offense", "campaign",
                    "progression", "migratedOutcomeOwners", "nodePublications" }.OrderBy(x => x)), "Unexpected node checkpoint fields.");
            Require((int)state["schemaVersion"] == 2 && (string)state["schema"] == Schema
                && (int)state["absoluteDay"] == AbsoluteDay && (string)state["actorId"] == ActorId,
                "Node checkpoint identity changed.");
            var migrated = ReadDto<DungeonMigratedProducerOutcomeSaveData>(state["migratedOutcomeOwners"]);
            outcome.Transaction.Restore(outcome.Transaction.PrepareRestore(migrated));
            nodeFacts.Publications.Restore(
                ReadDto<OffenseNodePublicationSaveData>(
                    state["nodePublications"]));
            campaign.PublishRestoreCandidate(campaign.BuildRestoreCandidate(
                ReadDto<DungeonOffenseCampaignSaveData>(state["campaign"])));
            ReadDto<ProgressionState>(state["progression"]).Restore(actor.Progression);
            var report = new DungeonGameRestoreReport();
            save.PublishRestoreCandidate(save.BuildRestoreCandidate(ReadDto<DungeonOffenseSaveData>(state["offense"]),
                report, Array.Empty<OffenseRegionState>(), campaign.State));
            Require(report.Success && (int)state["stage"] == Stage, "Checkpoint stage disagrees with authored route.");
            Require(JToken.DeepEquals(state, CaptureDomainState()), "Canonical restore changed saved state.");
        }

        private int Stock(StockCategory category) => Run.CarriedStock.TryGetValue(category, out int n) ? n : 0;
        private int Consumed(OffenseSupplyType type) => Run.ConsumedSupplies.TryGetValue(type, out int n) ? n : 0;
        private List<CharacterNarrativeFact> NodeFacts() => actor.Progression.CapturePersistentState()
            .NarrativeLedger.Facts.Where(f => f.domain == CharacterNarrativeDomain.Expedition).ToList();

        private void ValidateState()
        {
            Require(!disposed && runtime.ActiveExpeditions.Count == 1, "Node owner lost its sole expedition.");
            int stage = Stage;
            ValidateNodePublications(stage);
            var owners = outcome.Transaction.Capture().owners;
            var completed = new[] { TargetId + ":entrance", ApproachId, CacheId }.Take(stage + 1);
            int awardedXp = 0;
            var facts = NodeFacts();
            for (int i = 1; i <= stage; i++)
            {
                string id = i == 1 ? ApproachId : CacheId;
                Require(Run.Route.TryGetNode(id, out var node), "Expected authored node absent.");
                int xp = new OffenseExpeditionExperienceRules().CalculateNodeExperience(node, Run.Target.campaignOrder);
                awardedXp += xp;
                var fact = facts.SingleOrDefault(f => f.factId == "node:" + node.Kind);
                Require(fact != null && fact.count == 1 && fact.totalValue == xp && fact.subjectId == TargetId
                    && fact.outcome == "resolved" && fact.lastEventContext != null
                    && fact.lastEventContext.actorId == ActorId && fact.lastEventContext.chainId == ExpeditionId
                    && fact.lastEventContext.usedObjectId == id
                    && fact.lastEventContext.eventInstanceId == ExpeditionId + ":node:" + id + ":" + ActorId,
                    "Actor narrative is not grounded in the resolved node.");
                RequireOutcome(i);
            }
            var expected = CharacterProgressionRules.AddExperience(1, 0, awardedXp);
            float light = Run.Preparation.StartingLight - 20.75f - (stage > 0 ? 21f : 0f);
            Require(Run.ExpeditionId == ExpeditionId && Run.Target.id == TargetId
                && campaign.State.KnowTarget(TargetId) && !campaign.State.TruthRevealed
                && campaign.State.CompletedTargetCount == 0 && Run.Target.campaignOrder == 1
                && Run.MemberActors.Count == 1 && ReferenceEquals(Run.MemberActors[0], actor) && !actor.IsDead
                && actor.Identity.PersistentId == ActorId && actor.Progression.Level == expected.Level
                && actor.Progression.CurrentExperience == expected.CurrentExperience && facts.Count == stage
                && Run.CompletedNodeIds.OrderBy(x => x).SequenceEqual(completed.OrderBy(x => x))
                && Run.CurrentNodeId == (stage == 0 ? ApproachId : CacheId)
                && Run.Phase == (stage < 2 ? OffenseExpeditionPhase.ResolvingNode : OffenseExpeditionPhase.ChoosingRoute)
                && Mathf.Approximately(Run.Light, light)
                && Run.Supplies.Get(OffenseSupplyType.Rations) == (stage == 0 ? 3 : 2)
                && Run.Supplies.Get(OffenseSupplyType.Tools) == (stage == 0 ? 1 : 0)
                && Consumed(OffenseSupplyType.Rations) == (stage == 0 ? 1 : 2)
                && Consumed(OffenseSupplyType.Tools) == (stage == 0 ? 0 : 1)
                && Run.ConsumedSupplies.Values.Sum() == (stage == 0 ? 1 : 3)
                && Stock(StockCategory.General) == (stage == 0 ? 0 : 4)
                && Stock(StockCategory.Food) == (stage < 2 ? 0 : 7)
                && Run.CarriedStock.Values.Sum() == (stage == 0 ? 0 : stage == 1 ? 4 : 11)
                && Run.MemberStates[0].TotalDamageTaken == 0f
                && Run.MemberStates[0].Stress == (stage == 0 ? 0f : 4f)
                && OutcomeCount == stage && owners.Count == (stage == 0 ? 0 : 1)
                && (stage == 0 || owners[0].revision == stage)
                && runtime.ResultHistory.Count == 0 && !battle.HasActiveBattle,
                "Node journey, actor progression, resources or exact outcomes diverged.");
        }

        private void ValidateNodePublications(int stage)
        {
            OffenseNodePublicationSaveData saved =
                nodeFacts.Publications.Capture();
            Require(
                saved.version == OffenseNodePublicationSaveData.CurrentVersion
                && saved.observations.Count == stage,
                "Node publication owner count diverged from the authored route.");
            for (int index = 0; index < stage; index++)
            {
                OffenseNodeObservationData row = saved.observations[index];
                string nodeId = index == 0 ? ApproachId : CacheId;
                Require(
                    row != null
                    && row.fact != null
                    && row.fact.resolved
                    && row.fact.sequence == index + 1L
                    && row.fact.expeditionId == ExpeditionId
                    && row.fact.targetId == TargetId
                    && row.fact.nodeId == nodeId
                    && row.fact.absoluteDay == AbsoluteDay
                    && !row.pending
                    && !row.notificationPending
                    && row.publicationKey == OffenseNodeFactObserver.Key(
                        new OffenseNodeResolutionFact(row.fact)).ToString()
                    && row.experience.Count == 1
                    && row.experience[0].characterId == ActorId,
                    "Node publication fact, identity or experience provenance diverged.");
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            nodeFacts?.Dispose();
            battle?.Dispose();
            skillActivationBinding?.Dispose();
            battleCommandBinding?.Dispose();
            battleFacts?.Dispose();
            if (actorObject != null) Object.DestroyImmediate(actorObject);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    [Serializable]
    private sealed class ProgressionState
    {
        public int level;
        public int currentExperience;
        public CharacterGrowthState growthState;
        public CharacterNarrativeLedger narrativeLedger;
        public CharacterAcquiredTraitAggregateState acquiredTraitState;

        internal static ProgressionState Capture(CharacterProgression progression)
        {
            CharacterProgressionSnapshot snapshot = progression.CapturePersistentState();
            return new ProgressionState
            {
                level = snapshot.Level, currentExperience = snapshot.CurrentExperience,
                growthState = snapshot.GrowthState.Clone(),
                narrativeLedger = snapshot.NarrativeLedger.Clone(),
                acquiredTraitState = snapshot.AcquiredTraitState.Clone()
            };
        }
        internal void Restore(CharacterProgression progression)
        {
            Require(level > 0 && currentExperience >= 0 && growthState != null
                && narrativeLedger != null && acquiredTraitState != null, "Invalid saved progression.");
            progression.RestorePersistentState(new CharacterProgressionSnapshot(level, currentExperience,
                growthState, narrativeLedger, acquiredTraitState));
        }
    }

    // This is only the staged identity binder needed by OffenseSaveService, not a substitute world-save service.
    private sealed class StagedCharacterLookup :
        ICharacterWorldSaveService,
        ICharacterLifetimeQuery
    {
        private readonly CharacterActor actor;
        internal StagedCharacterLookup(CharacterActor actor) => this.actor = actor;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters => new[] { actor };
        public bool TryGetPersistentId(CharacterActor candidate, out string id)
        { id = ReferenceEquals(candidate, actor) ? ActorId : string.Empty; return id.Length > 0; }
        public string GetOrAssignPersistentId(CharacterActor candidate) =>
            ReferenceEquals(candidate, actor) ? ActorId : throw new InvalidOperationException("Unknown staged actor.");
        public bool TryGetRestoredActor(string id, out CharacterActor value)
        { value = id == ActorId ? actor : null; return value != null; }
        public DungeonCharacterWorldSaveData Capture(Grid grid) => throw Unsupported();
        public void ValidateRestorePayload(Grid grid, DungeonCharacterWorldSaveData source) => throw Unsupported();
        public CharacterWorldRestoreCandidate PrepareRestoreCandidate(Grid grid,
            DungeonCharacterWorldSaveData source) => throw Unsupported();
        public void StageRestoreCandidate(CharacterWorldRestoreCandidate candidate) => throw Unsupported();
    }

    private sealed class UnusedArrivalPort : IOffenseReturnArrivalRuntime
    {
        public IReadOnlyList<OffenseReturnArrivalState> Arrivals => throw Unsupported();
        public IReadOnlyList<OffenseExpeditionArrivalReceipt> GetSettlementReceipts(string expeditionId) => throw Unsupported();
        public void BeginExpeditionReturn(string expeditionId) => throw Unsupported();
        public void RegisterReturningMember(string expeditionId) => throw Unsupported();
        public void CompleteReturningMember(string expeditionId) => throw Unsupported();
        public void SealExpeditionReturn(string expeditionId) => throw Unsupported();
        public void RegisterBattlePrisonerCandidates(string expeditionId,
            IEnumerable<EnemyIndividualSaveData> individuals) => throw Unsupported();
        public void DiscardBattlePrisonerCandidates(string expeditionId) => throw Unsupported();
        public int QueueArrival(string expeditionId, string targetId, OffenseReturnArrivalKind kind, int amount) => throw Unsupported();
        public DungeonOffenseReturnArrivalSaveData Capture() => throw Unsupported();
        public OffenseReturnArrivalRestoreCandidate BuildRestoreCandidate(
            DungeonOffenseReturnArrivalSaveData source, DungeonGameRestoreReport report) => throw Unsupported();
        public void PublishRestoreCandidate(OffenseReturnArrivalRestoreCandidate candidate) => throw Unsupported();
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        public int Day => AbsoluteDay;
        public int Hour => 9;
        public int Year => 1;
        public int DayOfYear => Day;
        public Season Season => default;
        public int DayOfSeason => Day;
        public long AbsoluteHour => Day * 24L + Hour;
        public float ElapsedSeconds => 0;
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning => true;
        public CalendarDateTime Current => default;
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) => default;
        public void Start() { }
        public void SetDateTime(int day, int hour)
        { Require(day == Day && hour == Hour, "Fixed node calendar cannot advance."); }
    }

    private static NotSupportedException Unsupported() =>
        new("The non-battle node scenario does not execute full-world saves or return arrivals.");
    private static JObject Dto(object value) => JObject.Parse(JsonUtility.ToJson(value));
    private static T ReadDto<T>(JToken token)
    {
        Require(token is JObject, "Missing node owner DTO.");
        T value = JsonUtility.FromJson<T>(token.ToString());
        Require(JToken.DeepEquals(token, Dto(value)), "Non-canonical or incomplete node owner DTO.");
        return value;
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
