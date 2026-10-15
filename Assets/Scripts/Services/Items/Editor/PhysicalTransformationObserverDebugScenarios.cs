#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Short actual-owner checks, not natural gameplay or training evidence.</summary>
public static class PhysicalTransformationObserverDebugScenarios
{
    public static void RunFocused()
    {
        VerifyRecovery(false, false);
        VerifyRecovery(false, true);
        VerifyRecovery(true, false);
        VerifyRecovery(true, true);
        using (var missing = new Fixture(false, null, subscribe: false))
        {
            string before = missing.State();
            Require(!missing.Transform(out _) && missing.State() == before
                && missing.Items.Capture().physicalItemTransformations.Count == 0,
                "Missing observer applied physical effects.");
        }
        VerifyActivityLifecycle();
        VerifyDistinctRefinementCommands();
        Debug.Log(Wim030CrystalBeetleCarapaceDebugScenarios.Verify());
        Debug.Log(MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.VerifyE11TabooButcheryFocused());
        Debug.Log("PHYSICAL_TRANSFORM_OBSERVERS_FOCUSED_PASS; actualOwner=true; physicalAndCarcass=true; jsonFreshOwner=true; unknownBeforeAndAfterCommit=true; duplicateEffects=0");
    }

    private static void VerifyRecovery(bool carcass, bool commitFirst)
    {
        using var original = new Fixture(carcass, commitFirst);
        Require(original.Transform(out var receipt), "Initial owner command failed.");
        string applied = original.State();
        var physical = Copy(original.Items.Capture());
        var wildlife = new DungeonWildlifeSaveData
        { carcassProcessing = original.Carcasses.CaptureProcessing().ToList() };
        wildlife = Copy(wildlife);
        var ledger = Copy(original.Outcomes.Ledger.CaptureGameplayOutcomes());
        var counters = Copy(original.Outcomes.Transaction.Capture());
        Require(physical.physicalItemTransformations.Count == 1
            && !physical.physicalItemTransformations[0].publicationConfirmed
            && (!carcass || wildlife.carcassProcessing.Count == 1 && !wildlife.carcassProcessing[0].publicationConfirmed),
            "Unknown acknowledgement lost the frozen owner journal.");
        int expected = carcass ? 2 : 1;
        Require(ledger.outbox.Count + ledger.exactOutcomes.Count + ledger.tombstones.Count == (commitFirst ? expected : 0),
            "Fault fixture did not exercise the selected durable boundary.");
        PhysicalTransformationFactPreflight.Validate(physical, wildlife, ledger, counters);
        var raw = JObject.Parse(JsonUtility.ToJson(physical));
        ((JObject)raw["physicalItemTransformations"][0]["fact"]).Remove("wholeStack");
        ExpectRejected(() => PhysicalItemsSaveSection.ValidateRequiredCurrentSchemaShape(raw.ToString()));
        if (carcass)
        {
            var rawWildlife = JObject.Parse(JsonUtility.ToJson(wildlife));
            ((JObject)rawWildlife["carcassProcessing"][0]).Remove("publicationConfirmed");
            ExpectRejected(() => WildlifeSaveSection.ValidateProcessingShape(rawWildlife.ToString()));
        }
        using var restored = new Fixture(carcass, null, physical, wildlife, ledger, counters);
        restored.Session.TryGetSessionState(out var session);
        session.day.Value = 19;
        restored.Calendar.SetDateTime(19, 0);
        int events = 0;
        using var subscription = restored.Events.Subscribe<WildlifeCarcassProcessedEvent>(_ => events++);
        restored.Transforms.OnRestoreCompleted();
        restored.Transforms.Tick();
        restored.Carcasses.RecoverPendingProcessing();
        restored.Carcasses.RecoverPendingProcessing();
        var confirmedPhysical = restored.Items.Capture();
        var confirmedWildlife = new DungeonWildlifeSaveData
        { carcassProcessing = restored.Carcasses.CaptureProcessing().ToList() };
        var confirmedLedger = restored.Outcomes.Ledger.CaptureGameplayOutcomes();
        Require(restored.State() == applied && events == 0
            && confirmedPhysical.physicalItemTransformations.Single().publicationConfirmed
            && (!carcass || confirmedWildlife.carcassProcessing.Single().publicationConfirmed)
            && confirmedPhysical.physicalItemTransformations.Single().fact.absoluteDay == 3
            && confirmedLedger.outbox.Count + confirmedLedger.exactOutcomes.Count + confirmedLedger.tombstones.Count == expected,
            "Fresh-owner recovery repeated effects/events or changed frozen day.");
        PhysicalTransformationFactPreflight.Validate(confirmedPhysical, confirmedWildlife, confirmedLedger,
            restored.Outcomes.Transaction.Capture());
        string canonicalBefore = JsonUtility.ToJson(confirmedLedger);
        if (carcass)
            Require(!restored.Transform(out _), "Carcass replay consumed another source.");
        else
        {
            Require(restored.Transform(out var replay) && replay.WasReplayed && !replay.PublicationPending,
                "Partial transform did not return its original receipt.");
            Require(!restored.Transforms.TryTransformQuantity(restored.SourceId, 2, restored.Outputs,
                "qa:physical-transform-recovery", "qa-transform", out _, out _, out _),
                "Changed quantity reused a completed operation identity.");
        }
        Require(restored.State() == applied
            && JsonUtility.ToJson(restored.Outcomes.Ledger.CaptureGameplayOutcomes()) == canonicalBefore,
            "Repeated recovery or command changed physical/ledger authority.");
        confirmedPhysical.physicalItemTransformations.Single().fact.reasonCode += "-tampered";
        ExpectRejected(() => PhysicalTransformationFactPreflight.Validate(confirmedPhysical, confirmedWildlife,
            confirmedLedger, restored.Outcomes.Transaction.Capture()));
        Debug.Log($"PHYSICAL_TRANSFORM_RECOVERY=PASS; carcass={carcass}; committedBeforeAckLoss={commitFirst}; count={expected}; duplicateEffects=0");
    }

    private static void VerifyDistinctRefinementCommands()
    {
        using var fixture = new Fixture(false, null);
        string residue = EvolutionCatalystItemId.BuildResidue(1);
        var position = new Vector2Int(10, 11);
        Require(fixture.Items.SpawnItemAt(residue, 6, position, WorldItemStackState.Loose,
            string.Empty, out int spawned) && spawned == 6, "Refinement fixture material missing.");
        var economy = new EvolutionCatalystEconomyRuntime(fixture.Items, fixture.Transforms,
            new EditorGameMoneyAccount(new GameSessionState(startingMoney: 0, startingDay: 3)));
        Require(economy.TryRefine(1, "offense", position, out string failure), "First refinement failed: " + failure);
        Require(economy.TryRefine(1, "offense", position, out failure), "Second refinement failed: " + failure);
        var journals = fixture.Items.Capture().physicalItemTransformations;
        Require(journals.Count == 2 && journals.Select(row => row.fact.operationId).Distinct().Count() == 2
            && fixture.Items.GetAllStacks().Where(row => row.ItemId == residue).Sum(row => row.Quantity) == 0
            && fixture.Items.GetAllStacks().Where(row => row.ItemId == EvolutionCatalystItemId.BuildCatalyst("offense", 1))
                .Sum(row => row.Quantity) == 2,
            "Distinct refinement commands reused an identity or failed to consume/produce exactly twice.");
        Debug.Log("PHYSICAL_TRANSFORM_DISTINCT_COMMANDS=PASS; refinements=2; inputConsumed=6; output=2");
    }

    private static void VerifyActivityLifecycle()
    {
        var events = new CountingEvents();
        var observer = new WildlifeCarcassActivityObserver(events);
        observer.Start(); observer.Start();
        Require(events.Active == 1, "Activity observer duplicated its subscription.");
        observer.Dispose(); observer.Dispose();
        Require(events.Active == 0, "Activity observer leaked its subscription.");
        observer.Start();
        Require(events.Active == 1, "Activity observer could not reconnect.");
        observer.Dispose();
    }

    private static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }
    private static void ExpectRejected(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid saved fact was accepted.");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DomainFactDispatcher dispatcher;
        private readonly bool carcass;
        public Fixture(bool carcass, bool? loseResponse,
            DungeonPhysicalItemSaveData physical = null, DungeonWildlifeSaveData wildlife = null,
            GameplayOutcomeLedgerSaveData ledger = null, DungeonMigratedProducerOutcomeSaveData counters = null,
            bool subscribe = true)
        {
            this.carcass = carcass;
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository, out _, out _, out _);
            Outcomes = new MigratedProducerOutcomeEditorFixture("run:physical-transformation-observer");
            if (ledger != null)
            {
                var candidate = Outcomes.Ledger.PrepareGameplayOutcomeRestore(ledger);
                Outcomes.Ledger.BeginRestoreCandidate(); Outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
                Outcomes.Ledger.PublishRestoreCandidate(); Outcomes.Ledger.CompleteRestoreCandidate();
                Outcomes.Transaction.Restore(Outcomes.Transaction.PrepareRestore(counters));
            }
            dispatcher = subscribe ? Outcomes.CreatePhysicalTransformationFacts() : new DomainFactDispatcher();
            IDomainFactPublisher publisher = loseResponse.HasValue
                ? new LostResponse(dispatcher, loseResponse.Value) : dispatcher;
            Transforms = new PhysicalItemTransformService(repository,
                new WorldItemSpawner(Items.CatalogProvider, repository, EditorNullItemMarkerPresenter.Instance),
                Items.MassQuery, Items.CatalogProvider, EditorNullItemMarkerPresenter.Instance, Session, publisher);
            var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
            var definitions = new ResourceItemDefinitionCatalog(content);
            Carcasses = new WildlifeCarcassService(Items, Transforms,
                new ResourceWildlifeSpeciesCatalogProvider(content, definitions), Events);
            Carcasses.ConstructFactPublisher(Calendar, publisher);
            if (physical != null)
            {
                Items.Restore(physical);
                SourceId = physical.physicalItemTransformations.Single().fact.inputs[0].stackId;
                Carcasses.ReplaceFreshnessValidated(Array.Empty<WildlifeCarcassFreshnessSaveData>());
                var transaction = Carcasses.ApplyFreshnessRestoreCandidate(wildlife.carcasses, wildlife.carcassProcessing);
                Carcasses.CompleteFreshnessRestore(transaction);
            }
            else
            {
                string sourceItem = carcass
                    ? Wim030CrystalBeetleCarapaceAssetBuilder.CrystalBeetleCarcassItemId : "material:lumber";
                SourceId = WorldItemRepositoryEditorAccess.AddStack(repository, sourceItem, carcass ? 1 : 3,
                    WorldItemStackState.Loose, position: new Vector2Int(7, 11));
            }
            string outputId = Items.CatalogProvider.All.Where(definition => definition != null
                    && definition.ItemId != "material:lumber" && definition.MaxStack > 1
                    && !PhysicalItemIds.TryGetEquipmentDefinitionId(definition.ItemId, out _)
                    && !PhysicalItemIds.IsEquipmentModule(definition.ItemId)
                    && Items.MassQuery.GetDefinitionUnitMass((ItemDefinitionId)definition.ItemId).Value
                        <= Items.MassQuery.GetDefinitionUnitMass((ItemDefinitionId)"material:lumber").Value)
                .OrderBy(definition => definition.ItemId, StringComparer.Ordinal).First().ItemId;
            Outputs = new[] { new PhysicalItemTransformOutput(outputId, 1, new Vector2Int(7, 11)) };
        }
        public WorldItemStackRuntime Items { get; }
        public MigratedProducerOutcomeEditorFixture Outcomes { get; }
        public PhysicalItemTransformService Transforms { get; }
        public WildlifeCarcassService Carcasses { get; }
        public GameEventBus Events { get; } = new();
        public EditorFixedGameCalendar Calendar { get; } = new(3);
        public FixedGameSessionStateProvider Session { get; } = new(3);
        public string SourceId { get; }
        public PhysicalItemTransformOutput[] Outputs { get; }
        public bool Transform(out PhysicalItemTransformReceipt receipt)
        {
            receipt = default;
            if (carcass) return Carcasses.TryButcherNextCarcass(null, null, out _, out _);
            return Transforms.TryTransformQuantity(SourceId, 1, Outputs,
                "qa:physical-transform-recovery", "qa-transform", out receipt, out _, out _);
        }
        public string State() => string.Join("|", Items.GetAllStacks().OrderBy(s => s.StackId)
            .Select(s => s.StackId + ":" + s.ItemId + ":" + s.Quantity + ":" + s.Position));
        public void Dispose() { dispatcher.Dispose(); Items.Dispose(); }
    }

    private sealed class LostResponse : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly bool commitFirst;
        public LostResponse(DomainFactDispatcher inner, bool commitFirst)
        { this.inner = inner; this.commitFirst = commitFirst; }
        public bool TryReserve<T>(in T value, out DomainFactLease lease, out string failure) =>
            inner.TryReserve(value, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T value)
        {
            if (commitFirst)
            {
                var result = inner.Publish(lease, value);
                Require(result.MayBeCommitted, "Fault fixture failed before commit.");
            }
            else inner.Cancel(lease);
            return DomainFactDelivery.Uncertain("test:lost-acknowledgement");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> values, DomainFactDelivery[] results, out string failure)
        {
            if (commitFirst)
                Require(inner.PublishBatch(values, results, out _), "Fault cohort did not commit.");
            else foreach (var value in values) inner.Cancel(value.Lease);
            for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Uncertain("test:lost-acknowledgement");
            failure = "test:lost-acknowledgement";
            return true;
        }
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> values, DomainFactDelivery[] results, out string failure) =>
            inner.ReplayBatch(values, results, out failure);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class CountingEvents : IGameEventBus
    {
        public int Active;
        public IDisposable Subscribe<T>(Action<T> listener) { Active++; return new Subscription(this); }
        public void Publish<T>(T value) { }
        public void Clear() { Active = 0; }
        private sealed class Subscription : IDisposable
        {
            private CountingEvents owner;
            public Subscription(CountingEvents owner) => this.owner = owner;
            public void Dispose() { if (owner != null) { owner.Active--; owner = null; } }
        }
    }
}
#endif
