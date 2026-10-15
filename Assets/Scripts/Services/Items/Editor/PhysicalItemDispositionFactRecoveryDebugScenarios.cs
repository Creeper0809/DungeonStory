#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class PhysicalItemDispositionFactRecoveryDebugScenarios
{
    public static string Run()
    {
        VerifyRecovery(false); VerifyRecovery(true);
        using var missing = new Fixture(subscribe: false);
        string before = missing.ItemState();
        Require(!missing.Consume(out _, out _) && missing.ItemState() == before
            && missing.Persistence.Capture().pendingBatchDispositions.Count == 0, "missing observer consumed items");
        using var rejected = new Fixture(fault: 3);
        before = rejected.ItemState();
        long rejectedSequence = rejected.Ledger.CaptureGameplayOutcomes().nextSequence;
        Require(!rejected.Consume(out _, out _) && rejected.ItemState() == before
            && rejected.Persistence.Capture().pendingBatchDispositions.Count == 0
            && rejected.Ledger.CaptureGameplayOutcomes().nextSequence == rejectedSequence, "rejection did not roll back exact debit");
        return "physical-disposition/observer-required/rejection-rollback/lost-before-and-after-commit/json-fresh-owner/automatic-replay/dedup/frozen-day-name/canonical-tamper=PASS";
    }

    private static void VerifyRecovery(bool commitFirst)
    {
        using var source = new Fixture(fault: commitFirst ? 2 : 1);
        Require(source.Consume(out var receipt, out var pending) && receipt.IsCommitted && pending.Length > 0,
            "uncertain publication hid or reversed committed debit");
        var physical = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(source.Persistence.Capture()));
        var ledger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(source.Ledger.CaptureGameplayOutcomes()));
        var row = physical.pendingBatchDispositions.Single();
        Require(row.defaultObservationDay == 3 && row.sourceFacts.Single().displayText == "소비 당시 이름"
            && !row.publicationDelivered && row.ownerRevision == receipt.OwnerRevision, "original debit context not frozen");
        CheckJoin(physical, ledger, true);
        var raw = Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(physical));
        ((Newtonsoft.Json.Linq.JObject)raw["pendingBatchDispositions"][0]).Remove("defaultObservationDay");
        bool rejected = false;
        try { PhysicalItemsSaveSection.ValidateRequiredCurrentSchemaShape(raw.ToString()); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "missing day defaulted silently");

        using var restored = new Fixture(physical, ledger);
        restored.Calendar.SetDateTime(19, 0); restored.Catalog.Name = "복원 뒤 이름";
        string before = restored.ItemState();
        restored.Service.OnRestoreCompleted(); restored.Service.Tick();
        var confirmed = restored.Persistence.Capture();
        var confirmedLedger = restored.Ledger.CaptureGameplayOutcomes();
        Require(confirmed.pendingBatchDispositions.Single().publicationDelivered && restored.ItemState() == before,
            "restore hook did not recover publication without a second debit");
        long sequence = confirmedLedger.nextSequence;
        Require(restored.Consume(out var replay, out _) && replay.CommitId == receipt.CommitId
            && restored.ItemState() == before && restored.Ledger.CaptureGameplayOutcomes().nextSequence == sequence,
            "same-command replay consumed or recorded twice");
        CheckJoin(confirmed, confirmedLedger, true);
        var saved = confirmed.pendingBatchDispositions.Single();
        saved.sourceFacts[0].displayText += "-변조"; CheckJoin(confirmed, confirmedLedger, false);
        saved.sourceFacts[0].displayText = "소비 당시 이름";
        saved.defaultObservationDay++; CheckJoin(confirmed, confirmedLedger, false); saved.defaultObservationDay--;
        saved.publicationFingerprint = new string('0', 64); CheckJoin(confirmed, confirmedLedger, false);
        Require(restored.Service.Acknowledge(receipt.CommitId, out _)
            && restored.Persistence.Capture().pendingBatchDispositions.Count == 0, "delivered debit cannot acknowledge");
        CheckJoin(restored.Persistence.Capture(), confirmedLedger, true);
        Require(source.Consume(out _, out _) && source.ItemState() == before, "same-process retry repeated debit");
    }

    private static void CheckJoin(DungeonPhysicalItemSaveData physical, GameplayOutcomeLedgerSaveData ledger, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        PhysicalItemDispositionFactPreflight.Validate(physical, ledger, report);
        Require(report.Success == expected, "physical disposition canonical join unexpected result");
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private sealed class Fixture : IDisposable
    {
        private readonly DomainFactDispatcher dispatcher = new();
        private readonly string stackId;
        public Fixture(DungeonPhysicalItemSaveData physical = null, GameplayOutcomeLedgerSaveData savedLedger = null,
            int fault = 0, bool subscribe = true)
        {
            Repository = new WorldItemRepository(new GuidPersistentIdGenerator(), new DungeonRuntimeAggregateRootStore());
            Persistence = new WorldItemPersistenceService(Catalog, new Hauling(), Repository,
                EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
            if (physical != null)
            { Persistence.RestoreForEditorTest(physical); stackId = physical.pendingBatchDispositions.Single().sourceStackIds.Single(); }
            else stackId = WorldItemRepositoryEditorAccess.AddStack(Repository, ItemCatalog.ItemId, 3,
                WorldItemStackState.Loose, position: new Vector2Int(2, 1));
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new TradeInventoryOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new TradeInventoryOutcomeAdapter() });
            Ledger = new GameplayOutcomeLedger(registry, GameplayOutcomeBufferLimits.Default);
            if (savedLedger != null)
            {
                var candidate = Ledger.PrepareGameplayOutcomeRestore(savedLedger);
                Ledger.BeginRestoreCandidate(); Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate(); Ledger.CompleteRestoreCandidate();
            }
            var recorder = new GameplayOutcomeRecorder(Ledger, registry, new GameEventBus());
            if (subscribe) dispatcher.Subscribe("physical-disposition-recovery", new GameplayDomainFactObserver<
                PhysicalItemDisposedFact, TradeInventoryOutcomeReceipt>(PhysicalItemDispositionFactObservation.Capture,
                registry, Ledger, recorder, new GameplayDomainFactCommitGroup(recorder, Ledger)));
            Service = new PhysicalItemBatchDispositionService(Repository, new Mass(), EditorNullItemMarkerPresenter.Instance,
                null, Catalog, fault == 0 ? dispatcher : new Fault(dispatcher, fault), dispatcher,
                new GameplayDomainFactDeliveryQuery(recorder, Ledger), Calendar);
        }
        public WorldItemRepository Repository { get; }
        public WorldItemPersistenceService Persistence { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public PhysicalItemBatchDispositionService Service { get; }
        public ItemCatalog Catalog { get; } = new();
        public EditorFixedGameCalendar Calendar { get; } = new(3);
        public bool Consume(out PhysicalItemBatchDispositionReceipt receipt, out string failure) => Service.TryCommitPending(
            new[] { new PhysicalItemTransformInput(stackId, 1) }, PhysicalItemDispositionKind.Sink,
            "qa:disposition:recovery", "qa-consume", out receipt, out failure);
        public string ItemState() => string.Join("|", Repository.Records.OrderBy(value => value.stackId)
            .Select(value => value.stackId + ":" + value.quantity + ":" + value.position + ":" + value.itemId));
        public void Dispose() => dispatcher.Dispose();
    }

    private sealed class Fault : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly int mode;
        public Fault(IDomainFactStagingPublisher inner, int mode) { this.inner = inner; this.mode = mode; }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out var prepared, out failure)) { stage = null; return false; }
            stage = new Stage(prepared, mode); return true;
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly IDomainFactStage inner;
            private readonly int mode;
            public Stage(IDomainFactStage inner, int mode) { this.inner = inner; this.mode = mode; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public void Cancel() => inner.Cancel();
            public DomainFactDelivery Commit()
            {
                if (mode == 2) Require(inner.Commit().MayBeCommitted, "fault requires committed publication");
                else inner.Cancel();
                if (mode == 3) return DomainFactDelivery.Reject("qa-explicit-reject");
                throw new InvalidOperationException("qa-physical-publication-response-lost");
            }
        }
    }
    private sealed class ItemCatalog : IDungeonItemCatalogProvider
    {
        public const string ItemId = "item:disposition:recovery";
        public string Name = "소비 당시 이름";
        public IReadOnlyList<DungeonItemDefinition> All => new[] { GetDefinition(ItemId) };
        public DungeonItemDefinition GetDefinition(string id) => id == ItemId
            ? new DungeonItemDefinition(ItemId, Name, "", StockCategory.General, 1, null, 1f, 20)
            : throw new KeyNotFoundException(id);
        public bool TryGetDefinition(string id, out DungeonItemDefinition value)
        { value = id == ItemId ? GetDefinition(id) : null; return value != null; }
    }
    private sealed class Mass : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId id) => new(1000);
        public PhysicalMassGrams GetPreparedStackUnitMass(PhysicalItemMassSubject subject) => new(1000);
        public PhysicalMassGrams GetStackUnitMass(ItemDefinitionId id, PhysicalItemMassSubject subject) => new(1000);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) => new(lot.Quantity * 1000L);
        public PhysicalMassGrams GetQuantityMass(ItemDefinitionId id, PhysicalItemMassSubject subject, int quantity) => new(quantity * 1000L);
    }
    private sealed class Hauling : IItemHaulingSettingsProvider
    {
        public float MaxCarryMultiplier => CharacterCarryTuning.DefaultMaxCarryMultiplier;
        public ItemHaulingSettingsSnapshot Capture() => new();
        public void Restore(ItemHaulingSettingsSnapshot saved) { }
    }
}
#endif
