#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

// Real reward handler/runtime/finalizer and native observers, with a controlled
// physical port and campaign. This is not a natural-play or whole-world save test.
public static class OffenseStockRewardObserverDebugScenarios
{
    public static void RunBatch()
    {
        foreach (int fault in new[] { 1, 2, 3 }) VerifyRollback(fault);
        Debug.Log("B117_STOCK_PARENT_ATOMIC_ROLLBACK=PASS");
        VerifyRecovery(false);
        VerifyRecovery(true);
        Debug.Log("B117_STOCK_PARENT_JSON_REPLAY=PASS");
        using (var partial = new Scenario())
        {
            partial.Items.Limit = 2;
            var result = partial.FinalizeResult();
            Require(result.grantedRewards.All(row => !row.success && row.grantedAmount == 2
                && row.stockSupply.Snapshot().deliveredAmount == 2), "Partial stock result lost its actual quantity.");
            Require(partial.Quantity == 4 && result.itemReceipts.Single().quantity == 4, "Partial physical rewards were lost.");
            Require(partial.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes
                .Concat(partial.Outcomes.Ledger.CaptureGameplayOutcomes().outbox)
                .Count(row => row.producerId == StockProducer && row.status == GameplayOutcomeStatus.PartiallySucceeded) == 2,
                "Partial stock native outcomes were not partial.");
            Validate(partial.Capture(), true);
        }
        Debug.Log("B117_STOCK_PARTIAL_RECEIPT=PASS");
        OffenseRewardDebugScenarios.RunStockRewardObserverFocused();
        Debug.Log("B117_OFFENSE_STOCK_BATCH=PASS");
    }

    private static string StockProducer => MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.StockSupplyResult).ProducerId;

    private static void VerifyRollback(int fault)
    {
        using var value = new Scenario();
        value.Transport.RejectStockReserve = fault == 1;
        value.Transport.RejectBatch = fault == 2;
        var expected = value.Capture();
        // Reservations intentionally retain peak usage diagnostics even when canceled.
        // Keep exact assertions for these two counters and every gameplay/native field.
        expected.ledger.knownResultKeyHighWater = fault == 1 ? 1 : fault == 2 ? 3 : 0;
        expected.ledger.exactPageUseHighWater = expected.ledger.knownResultKeyHighWater;
        string before = JsonUtility.ToJson(expected);
        Reject(() => value.FinalizeResult(fault == 3 ? () => throw new InvalidOperationException("B117-return-rejected") : null));
        string after = JsonUtility.ToJson(value.Capture());
        Require(after == before && value.Notifications == 0,
            "Rejected parent left physical/reward/meta/campaign/history/native changes; fault=" + fault
            + "; expected=" + before + "; actual=" + after);
        value.Transport.RejectStockReserve = value.Transport.RejectBatch = false;
        value.FinalizeResult();
        Require(value.Quantity == 6 && value.Stock == 6 && value.History.Count == 1 && value.Meta.RunProgress.OffenseSuccessCount == 1,
            "Retry did not apply exactly one parent.");
        Require(value.NativeCount == 3, "Parent and two independent stock facts did not commit together.");
        Require(value.Notifications == 0, "Presentation escaped before parent acknowledgement processing.");
        Require(value.Publications.RecoverPending(out _) && value.Notifications == 2, "Stock presentation was not delivered once.");
        value.FinalizeResult();
        value.Publications.RecoverPending(out _);
        Require(value.Quantity == 6 && value.Items.SpawnCalls == 4 && value.Notifications == 2,
            "Duplicate parent re-granted or re-notified stock.");
    }

    private static void VerifyRecovery(bool after)
    {
        Bundle saved;
        using (var original = new Scenario())
        {
            original.Transport.UnknownBefore = !after;
            original.Transport.UnknownAfter = after;
            int fences = 0;
            original.Transport.DuringBatch = () =>
            {
                Reject(() => original.Publications.Capture());
                Reject(() => original.FinalizeResult());
                fences++;
            };
            original.FinalizeResult();
            Require(fences == 1 && original.Quantity == 6 && original.Stock == 6 && original.Notifications == 0
                && original.NativeCount == (after ? 3 : 0), "Unknown acknowledgement changed effects or leaked presentation.");
            saved = RoundTrip(original.Capture());
            Require(saved.publications.observations.Single().pending
                && saved.publications.observations.Single().stockPublicationKeys.Distinct().Count() == 2,
                "Missing pending parent or independent grant keys.");
            Validate(saved, true);
            var invalid = RoundTrip(saved);
            invalid.publications.observations[0].stockPublicationKeys[0] += "tamper";
            Validate(invalid, false);
            invalid = RoundTrip(saved);
            invalid.publications.observations[0].fact.result.grantedRewards[0].stockSupply.deliveredAmount++;
            Validate(invalid, false);
            if (after)
            {
                invalid = RoundTrip(saved);
                invalid.ledger.exactOutcomes.RemoveAll(row => row.producerId == StockProducer);
                invalid.ledger.outbox.RemoveAll(row => row.producerId == StockProducer);
                invalid.ledger.tombstones.RemoveAll(row => row.producerId == StockProducer);
                Validate(invalid, false);
                invalid = RoundTrip(saved);
                invalid.publications.observations[0].fact.result.grantedRewards[0].stockSupply.sourceLabel += "tamper";
                invalid.history[0].grantedRewards[0].stockSupply.sourceLabel += "tamper";
                Validate(invalid, false);
            }
        }
        using var restored = new Scenario(saved);
        Require(restored.Publications.RecoverPending(out string failure), "Fresh replay failed: " + failure);
        Require(restored.Quantity == 6 && restored.Stock == 6 && restored.NativeCount == 3
            && restored.Items.SpawnCalls == 0 && restored.Notifications == 2, "Recovery replayed effects or lost native records.");
        string settled = JsonUtility.ToJson(restored.Capture());
        restored.FinalizeResult();
        Require(restored.Publications.RecoverPending(out _) && restored.Items.SpawnCalls == 0
            && restored.Notifications == 2 && settled == JsonUtility.ToJson(restored.Capture()), "Settled replay was not idempotent.");
        Validate(restored.Capture(), true);
    }

    private static void Validate(Bundle value, bool expected)
    {
        var aggregate = new DungeonOffenseAggregateSaveData
        {
            resultPublications = value.publications,
            expedition = new DungeonOffenseSaveData { resultHistory = value.history }
        };
        var report = new DungeonGameRestoreReport();
        OffenseExpeditionResultFactPreflight.Validate(aggregate, value.ledger, value.counters, report);
        Require(report.Errors.Count == 0 == expected, "Parent/stock preflight expectation failed: " + string.Join(";", report.Errors));
    }

    [Serializable]
    private sealed class Bundle
    {
        public DungeonPhysicalItemSaveData physical;
        public OffenseExpeditionResultPublicationSaveData publications;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
        public List<DungeonOffenseExpeditionResultSaveData> history;
        public DungeonOffenseCampaignSaveData campaign;
        public int stock, metaSuccess;
    }
    private static Bundle RoundTrip(Bundle value) => JsonUtility.FromJson<Bundle>(JsonUtility.ToJson(value));
    private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected operation rejection was absent.");
    }

    private sealed class Scenario : IDisposable
    {
        private readonly GameObject root = new("B117 stock reward observer fixture");
        private readonly EditorOffenseExpeditionResultFactFixture observations;
        private readonly IDisposable notification;
        private readonly OffenseRewardRuntime rewards;
        private readonly OffenseExpeditionResultFinalizer finalizer;
        private readonly OffenseExpeditionRun expedition;
        private readonly OffenseExpeditionResult result;
        private readonly Campaign campaign = new();
        internal readonly MigratedProducerOutcomeEditorFixture Outcomes = new("run:b117:stock");
        internal readonly List<OffenseExpeditionResult> History = new();
        internal readonly PhysicalPort Items;
        internal readonly Transport Transport;
        internal readonly OffenseExpeditionResultPublicationRuntime Publications;
        internal readonly MetaProgressionRuntime Meta;
        internal int Notifications;
        internal int Quantity => Items.State.stacks.Sum(row => row.quantity);
        internal int Stock => rewards.State.StockGrantedByCategory.TryGetValue(StockCategory.Food, out int value) ? value : 0;
        internal int NativeCount => Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count
            + Outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count + Outcomes.Ledger.CaptureGameplayOutcomes().tombstones.Count;

        internal Scenario(Bundle saved = null)
        {
            var events = new GameEventBus();
            observations = new EditorOffenseExpeditionResultFactFixture(events, Outcomes.Transaction, Outcomes.Recorder, Outcomes.Ledger);
            Transport = new Transport(observations.Facts);
            Publications = new OffenseExpeditionResultPublicationRuntime(Transport, observations.Facts, events);
            notification = events.Subscribe<StockSupplyEvent>(_ => Notifications++);
            var physical = DispatchProxy.Create<IWorldItemStackRuntime, PhysicalPort>();
            Items = (PhysicalPort)(object)physical;
            rewards = root.AddComponent<OffenseRewardRuntime>();
            rewards.Construct(new Context(), new OffenseRewardGrantService(
                WarehouseFeatureCommandServiceT05DebugScenarios.DefaultProxy.Create<IOffenseRewardSelector>(),
                new IOffenseRewardGrantHandler[] { new OffenseStockRewardGrantHandler(Items, physical) }));
            rewards.ConstructTransactionItems(physical);
            Meta = root.AddComponent<MetaProgressionRuntime>();
            Meta.Construct(new MetaRunResultBuilder(), new MetaApplicationPort(), new UnityGameClock(),
                new MetaCatalog(), new DungeonRuntimeAggregateRootStore());
            finalizer = new OffenseExpeditionResultFinalizer(new OffenseSceneRuntimeReferences(null, rewards, null, null, null),
                new ProgressionSceneRuntimeReferences(null, null, Meta), campaign);
            finalizer.ConstructFactPublication(EditorFixedGameCalendar.Instance, Publications);
            var target = new OffenseTargetDefinition
            {
                id = "target:b117", title = "원정 재고 검사", requiredMembers = 1, requiredPower = 10f, durationSeconds = 10f,
                rewards = new[] { new OffenseRewardPreview("식량", 3, new OffenseStockRewardSpec(StockCategory.Food)),
                    new OffenseRewardPreview("식량", 3, new OffenseStockRewardSpec(StockCategory.Food)) }
            };
            expedition = new OffenseExpeditionRun("expedition:b117", target, Array.Empty<CharacterActor>(), 0f);
            result = new OffenseExpeditionResult(expedition.ExpeditionId, target.id, target.title, true,
                20f, 10f, 1f, 5f, Array.Empty<OffenseExpeditionMemberSnapshot>(), Array.Empty<string>());
            if (saved != null)
            {
                var candidate = Outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.ledger);
                Outcomes.Ledger.BeginRestoreCandidate(); Outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
                Outcomes.Ledger.PublishRestoreCandidate(); Outcomes.Ledger.CompleteRestoreCandidate();
                Outcomes.Transaction.Restore(Outcomes.Transaction.PrepareRestore(saved.counters));
                Publications.Restore(saved.publications);
                Items.State = saved.physical;
                rewards.PublishRestoreCandidate(OffenseRewardRuntime.BuildRestoreCandidate(0,
                    new Dictionary<StockCategory, int> { [StockCategory.Food] = saved.stock }, Array.Empty<int>(), Array.Empty<int>()));
                for (int index = 0; index < saved.metaSuccess; index++) Meta.RecordOffenseSuccess();
                campaign.PublishRestoreCandidate(campaign.BuildRestoreCandidate(saved.campaign));
                History.AddRange(saved.history.Select(OffenseSaveService.RestoreResult));
            }
        }
        internal OffenseExpeditionResult FinalizeResult(Action finish = null) => finalizer.Finalize(expedition, result, History, finish);
        internal Bundle Capture() => new()
        {
            physical = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(Items.State)),
            publications = Publications.Capture(), ledger = Outcomes.Ledger.CaptureGameplayOutcomes(),
            counters = Outcomes.Transaction.Capture(), history = History.Select(OffenseSaveService.CaptureResult).ToList(),
            campaign = campaign.Capture(), stock = Stock, metaSuccess = Meta.RunProgress.OffenseSuccessCount
        };
        public void Dispose() { notification.Dispose(); observations.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
    }

    public class PhysicalPort : DispatchProxy, IExpeditionRewardItemSink
    {
        internal DungeonPhysicalItemSaveData State = new();
        internal int SpawnCalls, Limit = int.MaxValue;
        public bool SpawnStock(StockCategory category, int amount, string label, out string itemId, out int spawned)
        {
            SpawnCalls++;
            itemId = "resource:food";
            spawned = Math.Min(amount, Limit);
            if (spawned > 0) State.stacks.Add(new WorldItemStackSaveData
            { stackId = "stack:b117:" + State.stacks.Count, itemId = itemId, quantity = spawned });
            return spawned == amount;
        }
        public bool SpawnLoot(string itemId, int amount, out int spawned) { spawned = 0; throw new InvalidOperationException("Unexpected loot route."); }
        protected override object Invoke(MethodInfo method, object[] arguments)
        {
            if (method.Name == "Capture") return JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(State));
            if (method.Name == "Restore")
            { State = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(arguments[0])); return null; }
            throw new InvalidOperationException("Unexpected physical call: " + method.Name);
        }
    }
    private sealed class Context : IOffenseRewardContextBuilder
    {
        public OffenseRewardContext Create(OffenseTargetDefinition target, OffenseRewardState state,
            OffenseRewardDebugContext unused, string expeditionId = "") => new()
        { target = target, rewardState = state, expeditionId = expeditionId };
    }
    private sealed class MetaCatalog : IMetaUpgradeDefinitionCatalog
    {
        public IReadOnlyCollection<MetaUpgradeDefinition> All => Array.Empty<MetaUpgradeDefinition>();
        public MetaUpgradeDefinition Get(string id) => null;
        public MetaUpgradeDefinition Require(string id) => throw new KeyNotFoundException(id);
    }
    private sealed class MetaApplicationPort : IMetaRuntimeApplicationPort
    {
        public void Bind(IMetaRuntimeEventSink runtime) { }
        public void Unbind(IMetaRuntimeEventSink runtime) { }
        public MetaRunEnvironmentSnapshot CaptureRunEnvironment() => new(1f, DungeonDifficulty.Normal, DungeonSurvivalPressure.Standard);
        public void PublishUpgradePurchased(MetaUpgradePurchasedEvent value, string message) { }
        public void PublishRunResult(RunResultReadyEvent value) { }
        public void ShowRunResult(RunResultSnapshot value) { }
    }
    private sealed class Campaign : IOffenseCampaignCommands, IOffenseCampaignRuntime
    {
        private OffenseWorldMapState state = new();
        internal Campaign() { state.Reset(); state.AddKnownTarget("target:b117"); }
        public IOffenseWorldMapStateView State => state;
        public DungeonOffenseCampaignSaveData Capture() => new()
        {
            reconLevel = state.ReconLevel, selectedTargetId = state.SelectedTargetId,
            knownTargetIds = state.KnownTargetIds.ToList(), completedTargetIds = state.CompletedTargetIds.ToList(),
            revealedTruthTargetId = state.RevealedTruthTargetId
        };
        public OffenseCampaignRestoreCandidate BuildRestoreCandidate(DungeonOffenseCampaignSaveData value)
        {
            var candidate = new OffenseWorldMapState();
            candidate.Restore(value.reconLevel, value.selectedTargetId, value.knownTargetIds, value.completedTargetIds, value.revealedTruthTargetId);
            return new OffenseCampaignRestoreCandidate(candidate);
        }
        public void PublishRestoreCandidate(OffenseCampaignRestoreCandidate value) => state = value.State;
        public bool TryUpgradeRecon(out string message) { message = "unused"; return false; }
        public bool TrySelectTarget(string id, out OffenseTargetSnapshot snapshot, out string message)
        { snapshot = null; message = "unused"; return false; }
        public bool TryRecordSuccessfulExpedition(string id, out OffenseTargetSnapshot snapshot, out string message)
        { snapshot = null; message = string.Empty; return state.MarkTargetCompleted(id); }
        public bool TryRecordStrategicTruthReveal(string id, out string message) { message = "unused"; return false; }
    }
    private sealed class Transport : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal bool RejectStockReserve, RejectBatch, UnknownBefore, UnknownAfter;
        internal Action DuringBatch;
        internal Transport(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T value, out DomainFactLease lease, out string failure)
        {
            if (RejectStockReserve && value is OffenseStockRewardCompletedFact)
            { lease = default; failure = "B117-stock-reserve-rejected"; return false; }
            return inner.TryReserve(value, out lease, out failure);
        }
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T value) => inner.Publish(lease, value);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
        {
            DuringBatch?.Invoke();
            failure = "B117-controlled-batch";
            if (RejectBatch) return false;
            if (UnknownBefore)
            { for (int index = 0; index < results.Length; index++) results[index] = DomainFactDelivery.Uncertain(failure); return true; }
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (UnknownAfter && accepted)
                for (int index = 0; index < results.Length; index++) results[index] = DomainFactDelivery.Uncertain("B117-unknown-after");
            return accepted;
        }
        public DomainFactDelivery Replay<T>(in T value) => inner.Replay(value);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }
}
#endif
