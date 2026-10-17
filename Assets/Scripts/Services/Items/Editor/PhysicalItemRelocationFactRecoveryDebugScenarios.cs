#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class PhysicalItemRelocationFactRecoveryDebugScenarios
{
    public static string Run()
    {
        VerifyLostResponse(false);
        VerifyLostResponse(true);
        using var missing = new Fixture(subscribe: false);
        string before = missing.PhysicalState();
        Require(!missing.Move(out _, out _) && missing.PhysicalState() == before
            && missing.Persistence.Capture().physicalItemRelocations.Count == 0,
            "missing observer applied a physical movement");
        return "physical-relocation-observer/missing-observer/lost-before-and-after-commit/json-fresh-owner/dedup/frozen-day-name/canonical-tamper=PASS";
    }

    private static void VerifyLostResponse(bool commitFirst)
    {
        using var source = new Fixture(uncertain: true, commitFirst: commitFirst);
        Require(source.Move(out var moved, out string pending) && moved.IsCommitted && pending.Length > 0,
            "lost publication response rolled back or hid uncertainty");
        var physical = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(source.Persistence.Capture()));
        var ledger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(source.Ledger.CaptureGameplayOutcomes()));
        var journal = physical.physicalItemRelocations.Single();
        Require(journal.phase == (int)PhysicalItemRelocationJournalPhase.DomainCommitted
            && journal.absoluteDay == 3 && journal.displayText == "이동 당시 이름",
            "pending physical fact did not freeze its original context");
        CheckJoin(physical, ledger, true);
        var raw = Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(physical));
        ((Newtonsoft.Json.Linq.JObject)raw["physicalItemRelocations"][0]).Remove("absoluteDay");
        ExpectRejected(() => PhysicalItemsSaveSection.ValidateRequiredCurrentSchemaShape(raw.ToString()));

        using var restored = new Fixture(physical, ledger);
        restored.Calendar.SetDateTime(19, 0);
        restored.Catalog.Name = "복원 뒤 바뀐 이름";
        string stateBefore = restored.PhysicalState();
        int version = restored.Repository.ItemStackVersion;
        restored.RecoverAfterRestore();
        Require(restored.Persistence.Capture().physicalItemRelocations.Single().phase
            == (int)PhysicalItemRelocationJournalPhase.Delivered && restored.PreparedCalls == 0,
            "restore hook did not recover without repeating the movement command");
        Require(restored.Move(out var replayed, out _) && replayed.DestinationStackId == moved.DestinationStackId
            && restored.PreparedCalls == 0 && restored.PhysicalState() == stateBefore
            && restored.Repository.ItemStackVersion == version,
            "fresh-owner replay moved or consumed an item again");
        var confirmed = restored.Persistence.Capture();
        var confirmedLedger = restored.Ledger.CaptureGameplayOutcomes();
        Require(confirmed.physicalItemRelocations.Single().phase == (int)PhysicalItemRelocationJournalPhase.Delivered,
            "fresh-owner replay did not confirm delivery");
        CheckJoin(confirmed, confirmedLedger, true);
        long sequence = confirmedLedger.nextSequence;
        Require(restored.Move(out _, out _) && restored.PreparedCalls == 0
            && restored.Ledger.CaptureGameplayOutcomes().nextSequence == sequence,
            "repeated movement duplicated a publication");

        var tampered = confirmed.physicalItemRelocations.Single();
        string oldName = tampered.displayText;
        tampered.displayText += "-변조";
        CheckJoin(confirmed, confirmedLedger, false);
        tampered.displayText = oldName;
        tampered.absoluteDay++;
        CheckJoin(confirmed, confirmedLedger, false);
        tampered.absoluteDay--;
        tampered.destinationState = (int)WorldItemStackState.Stored;
        // A changed request and even a recomputed request hash still cannot alter the canonical fact.
        tampered.requestFingerprint = PhysicalItemRelocatedFact.FromSave(tampered).RequestFingerprint;
        CheckJoin(confirmed, confirmedLedger, false);
        Require(source.Move(out _, out _) && source.PhysicalState() == stateBefore,
            "same-process reconciliation re-applied movement");
    }

    private static void CheckJoin(DungeonPhysicalItemSaveData physical, GameplayOutcomeLedgerSaveData ledger, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        PhysicalItemRelocationFactPreflight.Validate(physical, ledger, report);
        Require(report.Success == expected, "physical relocation canonical save join unexpected result");
    }
    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }
    private static void ExpectRejected(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("missing physical relocation scalar was accepted");
    }

    private sealed class Fixture : IDisposable, IPreparedPhysicalItemRelocationService
    {
        private readonly DomainFactDispatcher dispatcher = new();
        private readonly PreparedPhysicalItemRelocationService prepared;
        private readonly PhysicalItemRelocationService service;
        private readonly string sourceId;
        public Fixture(DungeonPhysicalItemSaveData physical = null, GameplayOutcomeLedgerSaveData savedLedger = null,
            bool uncertain = false, bool commitFirst = false, bool subscribe = true)
        {
            Repository = new WorldItemRepository(new GuidPersistentIdGenerator(), new DungeonRuntimeAggregateRootStore());
            Persistence = new WorldItemPersistenceService(Catalog, new Hauling(), Repository,
                EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
            if (physical != null) { Persistence.RestoreForEditorTest(physical); sourceId = physical.physicalItemRelocations.Single().sourceStackId; }
            else sourceId = WorldItemRepositoryEditorAccess.AddStack(Repository, ItemCatalog.ItemId, 3,
                WorldItemStackState.Loose, position: new Vector2Int(2, 1));
            prepared = new PreparedPhysicalItemRelocationService(Repository, new Mass(), Catalog, EditorNullItemMarkerPresenter.Instance);
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
            if (subscribe) dispatcher.Subscribe("physical-relocation-recovery", new GameplayDomainFactObserver<
                PhysicalItemRelocatedFact, TradeInventoryOutcomeReceipt>(PhysicalItemRelocationFactObservation.Capture,
                registry, Ledger, recorder, new GameplayDomainFactCommitGroup(recorder, Ledger)));
            service = new PhysicalItemRelocationService(Repository, this,
                uncertain ? new LostResponse(dispatcher, commitFirst) : dispatcher, dispatcher, Calendar);
        }
        public WorldItemRepository Repository { get; }
        public WorldItemPersistenceService Persistence { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public ItemCatalog Catalog { get; } = new();
        public EditorFixedGameCalendar Calendar { get; } = new(3);
        public int PreparedCalls { get; private set; }
        public void RecoverAfterRestore() { service.OnRestoreCompleted(); service.Tick(); }
        public bool Move(out PhysicalItemRelocationReceipt receipt, out string failure) => service.TryRelocateQuantity(
            sourceId, 1, new Vector2Int(5, 1), WorldItemStackState.Loose, "", "qa:relocation:recovery", "qa-move", out receipt, out failure);
        public string PhysicalState() => string.Join("|", Repository.Records.OrderBy(row => row.stackId)
            .Select(row => row.stackId + ":" + row.quantity + ":" + row.position + ":" + row.itemId));
        public bool TryPrepare(string stack, int quantity, Vector2Int position, WorldItemStackState state,
            string destination, string operation, string reason, out IPreparedPhysicalItemRelocation value, out string failure)
        { PreparedCalls++; return prepared.TryPrepare(stack, quantity, position, state, destination, operation, reason, out value, out failure); }
        public void Dispose() => dispatcher.Dispose();
    }

    private sealed class LostResponse : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly bool commitFirst;
        public LostResponse(IDomainFactStagingPublisher inner, bool commitFirst) { this.inner = inner; this.commitFirst = commitFirst; }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out var actual, out failure)) { stage = null; return false; }
            stage = new Stage(actual, commitFirst); return true;
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly IDomainFactStage inner;
            private readonly bool commitFirst;
            public Stage(IDomainFactStage inner, bool commitFirst) { this.inner = inner; this.commitFirst = commitFirst; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public void Cancel() => inner.Cancel();
            public DomainFactDelivery Commit()
            {
                if (commitFirst) Require(inner.Commit().MayBeCommitted, "postcommit fault requires an actual commit");
                else inner.Cancel();
                throw new InvalidOperationException("injected-physical-publication-response-lost");
            }
        }
    }
    private sealed class ItemCatalog : IDungeonItemCatalogProvider
    {
        public const string ItemId = "item:relocation:recovery";
        public string Name = "이동 당시 이름";
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
