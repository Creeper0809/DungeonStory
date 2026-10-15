#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class WastePolicyObserverRecoveryDebugScenarios
{
    public static void RunForBatch()
    {
        Debug.Log(TradeInventoryOutcomeDebugScenarios.RunAll());
        Require(TradeInventoryProductionEntryExecutionDebugScenarios.RunAll(), "Production owner entries failed.");
        Debug.Log("B123_TRADE_PRODUCTION_ENTRIES=PASS");
        Require(TradeInventoryCausalProductionEntryDebugScenarios.RunCausalOwnerFamily(), "Causal owner family failed.");
        Debug.Log("B123_TRADE_CAUSAL_OWNER_RESTORE=PASS");
        foreach (bool committed in new[] { false, true }) Verify(committed);
        Debug.Log("B123_WASTE_POLICY_OBSERVER_RECOVERY=PASS");
    }

    private static WastePolicyData Target() => new()
    { origin = WasteOriginKind.Plant, disposition = WasteDispositionKind.Compost, enabled = false, maximumFeedContamination = 40f };

    private static void Verify(bool committed)
    {
        string json;
        using (var source = new Context(committed))
        {
            var before = source.owner.Capture();
            source.transport.BeforeCommit = () =>
            {
                Throws(() => source.owner.Capture());
                Throws(() => source.owner.SetPolicy(Target()));
                Throws(() => source.owner.BuildRestore(before));
            };
            Require(!source.owner.SetPolicy(Target()).Succeeded, "Lost ACK was reported as confirmed success.");
            var pending = source.Capture();
            Require(pending.owner.nextPolicySequence == 2 && pending.owner.pendingPolicyChanges.Count == 1
                && pending.owner.pendingPolicyChanges[0].fact.absoluteDay == 36
                && WastePolicyChangedFact.SamePolicy(Target(), source.owner.GetPolicy(WasteOriginKind.Plant)),
                "Unknown commit failed to preserve the applied policy and frozen fact.");
            WastePolicyFactPreflight.Validate(pending.owner, pending.ledger);
            var missing = JObject.Parse(JsonUtility.ToJson(pending.owner));
            ((JObject)missing["pendingPolicyChanges"][0]["fact"]).Remove("beforeEnabled");
            Throws(() => new WasteProcessingSaveSection(source.owner).ValidatePayload(missing.ToString(),
                DungeonWasteProcessingSaveData.CurrentVersion, new DungeonGameRestoreReport()));
            if (committed)
            {
                var altered = JsonUtility.FromJson<Checkpoint>(JsonUtility.ToJson(pending));
                altered.owner.pendingPolicyChanges[0].fact.absoluteDay++;
                Throws(() => WastePolicyFactPreflight.Validate(altered.owner, altered.ledger));
            }
            json = JsonUtility.ToJson(pending);
            source.transport.AllowRecovery = false;
            var another = Target(); another.enabled = true;
            Require(!source.owner.SetPolicy(another).Succeeded
                && JsonUtility.ToJson(source.Capture()) == json, "A later command bypassed the pending observation.");
        }
        using (var restored = new Context(null))
        {
            var pending = JsonUtility.FromJson<Checkpoint>(json);
            restored.Restore(pending);
            restored.calendar.SetDateTime(80, 9);
            int version = restored.owner.Version;
            Require(restored.owner.SetPolicy(Target()).Succeeded && restored.owner.Version == version,
                "Retry reapplied the policy instead of acknowledging its original command.");
            var result = restored.Capture();
            WastePolicyFactPreflight.Validate(result.owner, result.ledger);
            var row = result.ledger.exactOutcomes.Concat(result.ledger.outbox).Single();
            Require(result.owner.nextPolicySequence == 2 && result.owner.pendingPolicyChanges.Count == 0
                && row.absoluteDay == 36 && row.ownerRevision == 1 && row.operationId == "waste-policy:1:command:1",
                "Recovery changed original identity/date or failed to clear pending state.");
            if (committed)
            {
                var original = pending.ledger.exactOutcomes.Concat(pending.ledger.outbox).Single();
                Require(row.sequence == original.sequence && row.immutablePayloadHash == original.immutablePayloadHash
                    && result.ledger.nextSequence == pending.ledger.nextSequence, "Recovery duplicated a committed row.");
            }
            string stable = JsonUtility.ToJson(result);
            restored.owner.Tick(); restored.owner.Tick();
            Require(JsonUtility.ToJson(restored.Capture()) == stable, "Idle recovery changed completed state.");
            var rewind = JsonUtility.FromJson<Checkpoint>(stable); rewind.owner.nextPolicySequence = 1;
            Throws(() => WastePolicyFactPreflight.Validate(rewind.owner, rewind.ledger));
            var next = Target(); next.enabled = true;
            Require(restored.owner.SetPolicy(next).Succeeded && restored.owner.Capture().nextPolicySequence == 3,
                "A new policy command did not proceed after recovery.");
            var final = restored.Capture(); WastePolicyFactPreflight.Validate(final.owner, final.ledger);
            Require(final.ledger.exactOutcomes.Concat(final.ledger.outbox).Count() == 2, "New command record count differs.");
        }
        Debug.Log("B123_WASTE_ACK_" + (committed ? "AFTER" : "BEFORE") + "_JSON_FRESH_OWNER=PASS");
    }

    [Serializable] private sealed class Checkpoint
    { public DungeonWasteProcessingSaveData owner; public GameplayOutcomeLedgerSaveData ledger; }

    private sealed class Context : IDisposable
    {
        internal readonly DomainFactDispatcher facts = new();
        internal readonly Transport transport;
        internal readonly Calendar calendar = new();
        internal readonly GameplayOutcomeLedger ledger;
        internal readonly WasteProcessingRuntime owner;
        internal Context(bool? committed)
        {
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new TradeInventoryOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new TradeInventoryOutcomeAdapter() });
            ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(), new GameplayOutcomeRunId("run:b123-waste-policy"), 1L);
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            facts.Subscribe("waste-policy", new GameplayDomainFactObserver<WastePolicyChangedFact, TradeInventoryOutcomeReceipt>(
                WastePolicyFactObservation.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger)));
            transport = new Transport(facts, committed);
            var rules = new WasteProcessingRulesDefinition(5f, 80f, new[] { new WasteOriginRuleRecord
            {
                origin = WasteOriginKind.Plant, defaultDisposition = WasteDispositionKind.Store, defaultMaximumFeedContamination = 79f,
                supportedDispositions = new List<WasteDispositionKind> { WasteDispositionKind.Store, WasteDispositionKind.Compost }
            } }, Array.Empty<WasteRecipeBindingRecord>(), Array.Empty<WasteFeedRuleRecord>(), Array.Empty<LegacyWasteItemRuleRecord>());
            owner = new WasteProcessingRuntime(new WasteProcessingMaterialDependencies(
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IWasteProcessingInventoryPort>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IResourceEconomyContentCatalog>()),
                new WasteProcessingOperationDependencies(BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IWasteProcessingProductionPort>(),
                    new Clock(), rules), new DungeonRuntimeAggregateRootStore(), transport, transport, calendar);
        }
        internal Checkpoint Capture() => new() { owner = owner.Capture(), ledger = ledger.CaptureGameplayOutcomes() };
        internal void Restore(Checkpoint saved)
        {
            WastePolicyFactPreflight.Validate(saved.owner, saved.ledger);
            var candidate = ledger.PrepareGameplayOutcomeRestore(saved.ledger);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            var section = new WasteProcessingSaveSection(owner); var report = new DungeonGameRestoreReport();
            section.Restore(JsonUtility.ToJson(saved.owner), DungeonWasteProcessingSaveData.CurrentVersion, report);
            Require(report.Success, "Policy section restore failed.");
        }
        public void Dispose() => facts.Dispose();
    }

    private sealed class Transport : IDomainFactStagingPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner; private readonly bool? committed;
        internal Action BeforeCommit; internal bool AllowRecovery = true;
        internal Transport(DomainFactDispatcher inner, bool? committed) { this.inner = inner; this.committed = committed; }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out var prepared, out failure)) { stage = null; return false; }
            stage = new Stage(this, prepared); return true;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> requests, DomainFactDelivery[] results, out string failure)
        {
            if (!AllowRecovery) { failure = "fixture:recovery-unavailable"; return false; }
            return inner.ReplayBatch(requests, results, out failure);
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly Transport transport; private readonly IDomainFactStage inner;
            internal Stage(Transport transport, IDomainFactStage inner) { this.transport = transport; this.inner = inner; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit()
            {
                transport.BeforeCommit?.Invoke();
                if (!transport.committed.HasValue) return inner.Commit();
                if (transport.committed.Value) Require(inner.Commit().Completed, "Real observer commit failed before lost ACK.");
                return DomainFactDelivery.Uncertain("fixture:lost-ack");
            }
            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class Clock : IGameClock
    { public float DeltaTime => 0; public float Time => 0; public int FrameCount => 0; public bool IsPaused => true; }
    private sealed class Calendar : IGameCalendar
    {
        public int Day { get; private set; } = 36; public int Hour { get; private set; } = 9;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public int Year => Current.Year; public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season; public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour; public float ElapsedSeconds => 0;
        public TimeOfDay TimeOfDay => default; public bool IsRunning => false;
        public CalendarDateTime GetRegionalTime(int offset) => GameCalendarRules.ProjectRegional(Day, Hour, offset);
        public void Start() { }
        public void SetDateTime(int day, int hour) { Day = day; Hour = hour; }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Throws(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Expected an explicit validation failure."); }
}
#endif
