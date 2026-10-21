using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class TradeInventoryOutcomeDebugScenarios
{
    public static string RunAll()
    {
        VerifyDeclarativeDefinitions();
        VerifyAttachmentHashContract();
        VerifyPreparedRelocationRollback();
        VerifyRelocationJournalReplayAcrossRestore();
        VerifyPostLedgerCommitNeverRollsBackPhysicalState();
        VerifyWastePolicyOwnerRollbackAndReplay();
        return "PASS TradeInventoryOutcomeDebugScenarios";
    }

    private static void VerifyDeclarativeDefinitions()
    {
        Require(
            TradeInventoryOutcomeDefinitions.All.Count == 23,
            "The trade/inventory definition registry must contain 23 rows.");
        HashSet<string> slugs = new(StringComparer.Ordinal);
        HashSet<string> producers = new(StringComparer.Ordinal);
        for (int index = 0;
             index < TradeInventoryOutcomeDefinitions.All.Count;
             index++)
        {
            TradeInventoryOutcomeDefinition definition =
                TradeInventoryOutcomeDefinitions.All[index];
            Require(
                (int)definition.Kind == index + 1
                && slugs.Add(definition.Slug)
                && producers.Add(definition.ProducerId),
                "Definition rows must be contiguous and identity-unique.");
            Require(
                definition.RequiredRoles.Any(role => role.Equals(
                    definition.NarrativeSubjectRole)),
                "Every definition must own a required narrative subject role.");
        }
    }

    private static void VerifyAttachmentHashContract()
    {
        GameplayResultKey key = new(
            "qa.trade-inventory",
            new GameplayOperationId("qa:trade-inventory:hash"),
            1L,
            0);
        GameplayOutcomeId outcome = new(
            new GameplayOutcomeRunId("run:qa-trade-inventory"),
            1L);
        Require(
            new PhysicalGameplayOutcomeAttachment(
                key,
                outcome,
                GameplayOutcomeReplayState.Committed,
                LowercaseHash).IsValid,
            "A canonical lowercase SHA-256 attachment must be valid.");
        Require(
            !new PhysicalGameplayOutcomeAttachment(
                key,
                outcome,
                GameplayOutcomeReplayState.Committed,
                LowercaseHash.ToUpperInvariant()).IsValid,
            "Uppercase hashes must fail the canonical attachment contract.");
        Require(
            new PhysicalGameplayOutcomeAttachment(
                key,
                outcome,
                GameplayOutcomeReplayState.Compacted,
                LowercaseHash).HasAcknowledgementProof,
            "A compacted tombstone must retain exact acknowledgement proof.");
        Require(
            !new PhysicalGameplayOutcomeAttachment(
                key,
                outcome,
                GameplayOutcomeReplayState.Reserved,
                LowercaseHash).IsValid,
            "A reservation is not a canonical attachment lifecycle state.");
    }

    private static void VerifyWastePolicyOwnerRollbackAndReplay()
    {
        TestWasteInventory inventory = new();
        TestWasteProduction production = new();
        TestWasteCatalog catalog = new();
        TestWasteClock clock = new();
        TestWasteRules rules = new();
        WastePolicyData desired = new()
        {
            origin = WasteOriginKind.Plant,
            disposition = WasteDispositionKind.Compost,
            enabled = false,
            maximumFeedContamination = 40f
        };

        var rejectedObservations = new TestWastePolicyObservationPort(
            DomainFactDeliveryPhase.Rejected,
            DomainFactDeliveryPhase.Delivered);
        WasteProcessingRuntime rollbackRuntime = new(
            new WasteProcessingMaterialDependencies(inventory, catalog),
            new WasteProcessingOperationDependencies(production, clock, rules),
            new DungeonRuntimeAggregateRootStore(),
            rejectedObservations,
            rejectedObservations,
            EditorFixedGameCalendar.Instance);
        WastePolicyCommandResult failed = rollbackRuntime.SetPolicy(desired);
        DungeonWasteProcessingSaveData rolledBack = rollbackRuntime.Capture();
        WastePolicyData original = rollbackRuntime.GetPolicy(
            WasteOriginKind.Plant);
        Require(
            !failed.Succeeded
            && original.disposition == WasteDispositionKind.Store
            && original.enabled
            && Mathf.Approximately(original.maximumFeedContamination, 79f)
            && rolledBack.nextPolicySequence == 1L
            && rolledBack.pendingPolicyChanges.Count == 0
            && rejectedObservations.StageCount == 1,
            "A pre-canonical waste-policy outcome failure must restore the exact policy and sequence.");

        var pendingObservations = new TestWastePolicyObservationPort(
            DomainFactDeliveryPhase.AcceptedPendingDelivery,
            DomainFactDeliveryPhase.Delivered);
        WasteProcessingRuntime source = new(
            new WasteProcessingMaterialDependencies(inventory, catalog),
            new WasteProcessingOperationDependencies(production, clock, rules),
            new DungeonRuntimeAggregateRootStore(),
            pendingObservations,
            pendingObservations,
            EditorFixedGameCalendar.Instance);
        WastePolicyCommandResult committed = source.SetPolicy(desired);
        DungeonWasteProcessingSaveData interrupted = source.Capture();
        WastePolicyObservationSaveData pending =
            interrupted.pendingPolicyChanges.SingleOrDefault();
        Require(
            committed.Succeeded
            && committed.OperationId == "waste-policy:1:command:1"
            && committed.OwnerRevision == 1L
            && interrupted.nextPolicySequence == 2L
            && pending != null
            && pending.fact.operationId == committed.OperationId
            && pending.publicationKey == pendingObservations.LastPublicationKey
            && pending.publicationId == TestWastePolicyObservationPort.PublicationId
            && pending.publicationFingerprint == LowercaseHash,
            "A canonical-but-unacknowledged policy result must remain in the owner save outbox.");

        var deliveredObservations = new TestWastePolicyObservationPort(
            DomainFactDeliveryPhase.Delivered,
            DomainFactDeliveryPhase.Delivered);
        WasteProcessingRuntime restored = new(
            new WasteProcessingMaterialDependencies(inventory, catalog),
            new WasteProcessingOperationDependencies(production, clock, rules),
            new DungeonRuntimeAggregateRootStore(),
            deliveredObservations,
            deliveredObservations,
            EditorFixedGameCalendar.Instance);
        restored.Restore(restored.BuildRestore(interrupted));
        WastePolicyData beforeRetry = restored.GetPolicy(WasteOriginKind.Plant);
        restored.Tick();
        WastePolicyData afterRetry = restored.GetPolicy(WasteOriginKind.Plant);
        DungeonWasteProcessingSaveData reconciled = restored.Capture();
        Require(
            beforeRetry.disposition == desired.disposition
            && afterRetry.disposition == desired.disposition
            && beforeRetry.enabled == desired.enabled
            && afterRetry.enabled == desired.enabled
            && Mathf.Approximately(
                afterRetry.maximumFeedContamination,
                desired.maximumFeedContamination)
            && reconciled.nextPolicySequence == 2L
            && reconciled.pendingPolicyChanges.Count == 0
            && deliveredObservations.ReplayCount == 1
            && deliveredObservations.StageCount == 0,
            "Save/restore acknowledgement replay must not apply the policy mutation twice.");
    }

    private static void VerifyPostLedgerCommitNeverRollsBackPhysicalState()
    {
        TestCatalog catalog = new();
        TestMassQuery mass = new();
        MutableOutcomeDiagnostics diagnostics = new();
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            TestCatalog.ItemId,
            1,
            WorldItemStackState.Loose,
            position: new Vector2Int(2, 3));
        PhysicalItemBatchDispositionService service = new(
            repository,
            mass,
            EditorNullItemMarkerPresenter.Instance,
            null,
            catalog,
            null,
            null,
            new MutableOutcomeDeliveryQuery(diagnostics),
            null, clock: new DungeonStory.Foundation.UnityUiClock());
        CanonicalCommitIdentityDeferredParticipant participant = new(
            diagnostics);
        string operation = "qa:trade-inventory:post-commit-fault";
        Require(
            ((IJointPhysicalItemBatchDispositionService)service)
                .TryCommitPending(
                    new[] { new PhysicalItemTransformInput(stackId, 1) },
                    PhysicalItemDispositionKind.Sink,
                    operation,
                    "qa-post-commit-fault",
                    new PhysicalItemLegacyOutcomeEffect(participant),
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out _)
            && receipt.IsCommitted
            && repository.Records.All(record => record == null
                || !string.Equals(
                    record.stackId,
                    stackId,
                    StringComparison.Ordinal))
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "A post-ledger identity fault must retain physical commit and pending reconciliation.");

        TestHaulingSettings hauling = new();
        WorldItemPersistenceService sourcePersistence = new(
            new EquipmentEvolutionFormulaCatalogs(
                CharacterAiEditorTestDependencies.ContentDefinitions),
            catalog,
            hauling,
            repository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        DungeonPhysicalItemSaveData save = sourcePersistence.Capture();
        Require(
            save.pendingBatchDispositions.Count == 1
            && save.pendingBatchDispositions[0].publicationExpected
            && save.pendingBatchDispositions[0].ownerRevision
                == receipt.OwnerRevision
            && save.pendingBatchDispositions[0].publicationKey.Length > 0
            && save.pendingBatchDispositions[0].publicationId.Length == 0
            && save.pendingBatchDispositions[0].publicationFingerprint.Length
                == 0
            && !save.pendingBatchDispositions[0].publicationDelivered
            && save.pendingBatchDispositions[0].defaultObservationDay == 0,
            "The unresolved exact outcome key must survive physical capture.");

        WorldItemRepository restoredRepository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        WorldItemPersistenceService restoredPersistence = new(
            new EquipmentEvolutionFormulaCatalogs(
                CharacterAiEditorTestDependencies.ContentDefinitions),
            catalog,
            new TestHaulingSettings(),
            restoredRepository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        restoredPersistence.RestoreForEditorTest(save);
        PhysicalItemBatchDispositionService restored = new(
            restoredRepository,
            mass,
            EditorNullItemMarkerPresenter.Instance,
            null,
            catalog,
            null,
            null,
            new MutableOutcomeDeliveryQuery(diagnostics),
            null, clock: new DungeonStory.Foundation.UnityUiClock());
        // Simulate the interrupted window after canonical acknowledgement but
        // before the physical join attached its identity. Consolidation may
        // compact the ledger row before this independently saved pending row
        // is restored and reconciled.
        diagnostics.State = GameplayOutcomeReplayState.Compacted;
        Require(
            restored.Acknowledge(receipt.CommitId, out string failure)
            && restoredRepository.GetEditorPendingBatchDispositionCount() == 0,
            "A compacted acknowledged identity must reconcile and retire the pending row: "
            + failure);
    }

    private static void VerifyPreparedRelocationRollback()
    {
        TestCatalog catalog = new();
        TestMassQuery mass = new();
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        Vector2Int sourcePosition = new(4, 4);
        Vector2Int destinationPosition = new(5, 4);
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            TestCatalog.ItemId,
            2,
            WorldItemStackState.Loose,
            position: sourcePosition);
        PreparedPhysicalItemRelocationService service = new(
            repository,
            mass,
            catalog,
            EditorNullItemMarkerPresenter.Instance);
        Require(
            service.TryPrepare(
                stackId,
                1,
                destinationPosition,
                WorldItemStackState.Loose,
                string.Empty,
                "qa:trade-inventory:relocation",
                "qa-relocation",
                out IPreparedPhysicalItemRelocation prepared,
                out string prepareFailure)
            && prepared.Preview.IsValid
            && prepared.Preview.Receipt.DestinationStackId.Length > 0,
            "Relocation preview did not reserve an exact destination identity: "
            + prepareFailure);
        Require(
            prepared.TryApply(
                out IReversiblePhysicalItemRelocation transaction,
                out string applyFailure)
            && transaction.Receipt.DestinationStackId
                == prepared.Preview.Receipt.DestinationStackId
            && repository.Records.Any(record => record != null
                && record.position == destinationPosition),
            "Prepared relocation did not apply its exact preview: "
            + applyFailure);
        Require(
            transaction.TryRollback(out string rollbackFailure)
            && repository.Records.Single(record => record != null
                && string.Equals(
                    record.stackId,
                    stackId,
                    StringComparison.Ordinal)).quantity == 2
            && repository.Records.All(record => record == null
                || record.position != destinationPosition),
            "Prepared relocation did not restore its exact source: "
            + rollbackFailure);
    }

    private static void VerifyRelocationJournalReplayAcrossRestore()
    {
        TestCatalog catalog = new();
        TestMassQuery mass = new();
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new TradeInventoryOutcomeDescriptor(new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new TradeInventoryOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            GameplayOutcomeBufferLimits.Default);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        using DomainFactDispatcher dispatcher = new();
        dispatcher.Subscribe(
            "physical-relocation",
            new GameplayDomainFactObserver<
                PhysicalItemRelocatedFact,
                TradeInventoryOutcomeReceipt>(
                PhysicalItemRelocationFactObservation.Capture,
                registry,
                ledger,
                recorder,
                new GameplayDomainFactCommitGroup(recorder, ledger)));
        var deferredFacts = new DeliveryOverrideStagingPublisher(
            dispatcher,
            DomainFactDeliveryPhase.AcceptedPendingDelivery);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        Vector2Int sourcePosition = new(7, 8);
        Vector2Int destinationPosition = new(8, 8);
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            TestCatalog.ItemId,
            2,
            WorldItemStackState.Loose,
            position: sourcePosition);
        PhysicalItemRelocationService service = new(
            repository,
            new PreparedPhysicalItemRelocationService(
                repository,
                mass,
                catalog,
                EditorNullItemMarkerPresenter.Instance),
            deferredFacts,
            dispatcher,
            EditorFixedGameCalendar.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
        string operation = "qa:trade-inventory:relocation-journal";
        Require(
            service.TryRelocateQuantity(
                stackId,
                1,
                destinationPosition,
                WorldItemStackState.Loose,
                string.Empty,
                operation,
                "qa-relocation-journal",
                out PhysicalItemRelocationReceipt original,
                out _)
            && original.IsCommitted
            && repository.Records.Sum(value => value.quantity) == 2,
            "Relocation journal did not commit the exact physical move.");

        WorldItemPersistenceService persistence = new(
            new EquipmentEvolutionFormulaCatalogs(
                CharacterAiEditorTestDependencies.ContentDefinitions),
            catalog,
            new TestHaulingSettings(),
            repository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        DungeonPhysicalItemSaveData saved = persistence.Capture();
        Require(
            saved.physicalItemRelocations.Count == 1
            && saved.physicalItemRelocations[0].phase == (int)
                PhysicalItemRelocationJournalPhase.PublicationAccepted
            && saved.physicalItemRelocations[0].ownerRevision > 0L
            && saved.physicalItemRelocations[0].absoluteDay > 0
            && !string.IsNullOrWhiteSpace(
                saved.physicalItemRelocations[0].publicationKey)
            && !string.IsNullOrWhiteSpace(
                saved.physicalItemRelocations[0].publicationId)
            && saved.physicalItemRelocations[0].publicationFingerprint.Length
                == 64,
            "Commit-before-delivery relocation join was not saved.");

        WorldItemRepository restoredRepository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        WorldItemPersistenceService restoredPersistence = new(
            new EquipmentEvolutionFormulaCatalogs(
                CharacterAiEditorTestDependencies.ContentDefinitions),
            catalog,
            new TestHaulingSettings(),
            restoredRepository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        restoredPersistence.RestoreForEditorTest(saved);
        PhysicalItemRelocationService restored = new(
            restoredRepository,
            new PreparedPhysicalItemRelocationService(
                restoredRepository,
                mass,
                catalog,
                EditorNullItemMarkerPresenter.Instance),
            dispatcher,
            dispatcher,
            EditorFixedGameCalendar.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
        Require(
            restored.TryRelocateQuantity(
                stackId,
                1,
                destinationPosition,
                WorldItemStackState.Loose,
                string.Empty,
                operation,
                "qa-relocation-journal",
                out PhysicalItemRelocationReceipt replay,
                out _)
            && replay.IsCommitted
            && replay.DestinationStackId == original.DestinationStackId
            && restoredRepository.Records.Sum(value => value.quantity) == 2,
            "Restore retry did not return the same relocation without a second mutation.");
        Require(
            !restored.TryRelocateQuantity(
                stackId,
                1,
                destinationPosition + Vector2Int.up,
                WorldItemStackState.Loose,
                string.Empty,
                operation,
                "qa-relocation-journal",
                out _,
                out string conflict)
            && conflict.Contains("operation-conflict", StringComparison.Ordinal),
            "Same operation with a different payload did not fail closed.");

        DungeonPhysicalItemSaveData acknowledged =
            restoredPersistence.Capture();
        Require(
            acknowledged.physicalItemRelocations.Single().phase == (int)
                PhysicalItemRelocationJournalPhase.Delivered
            && acknowledged.physicalItemRelocations.Single().publicationKey
                == saved.physicalItemRelocations[0].publicationKey
            && acknowledged.physicalItemRelocations.Single().publicationId
                == saved.physicalItemRelocations[0].publicationId
            && acknowledged.physicalItemRelocations.Single()
                .publicationFingerprint
                == saved.physicalItemRelocations[0].publicationFingerprint,
            "Acknowledged relocation phase did not survive capture.");
    }

    private const string LowercaseHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private sealed class CanonicalCommitIdentityDeferredParticipant :
        IPhysicalItemBatchDispositionOutcomeParticipant
    {
        private readonly MutableOutcomeDiagnostics diagnostics;

        internal CanonicalCommitIdentityDeferredParticipant(
            MutableOutcomeDiagnostics diagnostics) =>
            this.diagnostics = diagnostics;

        public bool TryPrepare(
            in PhysicalItemBatchDispositionReceipt exactReceipt,
            long expectedOwnerRevision,
            out IPreparedPhysicalItemGameplayOutcome prepared,
            out string failureReason)
        {
            if (!exactReceipt.IsCommitted
                || exactReceipt.OwnerRevision != expectedOwnerRevision)
            {
                prepared = null;
                failureReason = "qa-receipt-invalid";
                return false;
            }
            GameplayResultKey key = new(
                "qa.trade-inventory.physical",
                new GameplayOperationId(exactReceipt.OperationId),
                expectedOwnerRevision,
                0);
            prepared = new Prepared(diagnostics, key);
            failureReason = string.Empty;
            return true;
        }

        private sealed class Prepared : IPreparedPhysicalItemGameplayOutcome
        {
            private readonly MutableOutcomeDiagnostics diagnostics;

            internal Prepared(
                MutableOutcomeDiagnostics diagnostics,
                GameplayResultKey resultKey)
            {
                this.diagnostics = diagnostics;
                ResultKey = resultKey;
            }

            public GameplayResultKey ResultKey { get; }
            public bool IsCanonicalReplay => false;

            public bool TryCommit(
                long expectedOwnerRevision,
                out PhysicalGameplayOutcomeAttachment attachment,
                out bool canonicalCommitted,
                out string failureReason)
            {
                diagnostics.SetIdentity(ResultKey);
                attachment = default;
                canonicalCommitted = true;
                failureReason = "qa-identity-read-deferred-after-commit";
                throw new InvalidOperationException(
                    "qa-fault-after-canonical-ledger-commit");
            }

            public void Cancel()
            {
            }
        }
    }

    private sealed class MutableOutcomeDiagnostics :
        IGameplayOutcomeDiagnosticsQuery
    {
        private GameplayResultKey resultKey;
        internal GameplayOutcomeReplayState State { get; set; } =
            GameplayOutcomeReplayState.Committed;

        internal void SetIdentity(GameplayResultKey key) => resultKey = key;

        internal bool TryGetPublication(
            string publicationKey,
            out GameplayOutcomeReplayIdentity identity)
        {
            identity = default;
            return resultKey.IsValid
                && string.Equals(
                    resultKey.ToString(),
                    publicationKey,
                    StringComparison.Ordinal)
                && TryGetResultIdentity(resultKey, out identity);
        }

        public GameplayOutcomeLedgerDiagnostics GetDiagnostics() => default;
        public IReadOnlyList<GameplayOutcomeOutboxSnapshot> GetOutboxSnapshot() =>
            Array.Empty<GameplayOutcomeOutboxSnapshot>();

        public bool TryGetResultIdentity(
            GameplayResultKey requested,
            out GameplayOutcomeReplayIdentity identity)
        {
            if (resultKey.IsValid && requested.Equals(resultKey))
            {
                identity = new GameplayOutcomeReplayIdentity(
                    resultKey,
                    new GameplayOutcomeId(
                        new GameplayOutcomeRunId("run:qa-trade-inventory"),
                        1L),
                    State,
                    LowercaseHash);
                return true;
            }
            identity = default;
            return false;
        }
    }

    private sealed class MutableOutcomeDeliveryQuery : IDomainFactDeliveryQuery
    {
        private readonly MutableOutcomeDiagnostics diagnostics;

        internal MutableOutcomeDeliveryQuery(
            MutableOutcomeDiagnostics diagnostics) =>
            this.diagnostics = diagnostics
                ?? throw new ArgumentNullException(nameof(diagnostics));

        public DomainFactDelivery Reconcile(string publicationKey)
        {
            if (diagnostics.TryGetPublication(
                    publicationKey,
                    out GameplayOutcomeReplayIdentity identity))
            {
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.Delivered,
                    publicationKey,
                    identity.OutcomeId.ToString(),
                    identity.CanonicalPayloadHash,
                    string.Empty);
            }
            return new DomainFactDelivery(
                DomainFactDeliveryPhase.InDoubt,
                publicationKey,
                null,
                null,
                "qa-publication-identity-pending");
        }
    }

    private sealed class DeliveryOverrideStagingPublisher :
        IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly DomainFactDeliveryPhase phase;

        internal DeliveryOverrideStagingPublisher(
            IDomainFactStagingPublisher inner,
            DomainFactDeliveryPhase phase)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (phase is DomainFactDeliveryPhase.Rejected
                or DomainFactDeliveryPhase.Delivered)
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }
            this.phase = phase;
        }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            if (!inner.TryStage(fact, out IDomainFactStage prepared, out failure))
            {
                stage = null;
                return false;
            }
            stage = new DeliveryOverrideStage(prepared, phase);
            return true;
        }

        private sealed class DeliveryOverrideStage : IDomainFactStage
        {
            private readonly IDomainFactStage inner;
            private readonly DomainFactDeliveryPhase phase;

            internal DeliveryOverrideStage(
                IDomainFactStage inner,
                DomainFactDeliveryPhase phase)
            {
                this.inner = inner;
                this.phase = phase;
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                DomainFactDelivery delivered = inner.Commit();
                return !delivered.MayBeCommitted
                    ? delivered
                    : new DomainFactDelivery(
                        phase,
                        delivered.OperationKey,
                        delivered.PublicationId,
                        delivered.Fingerprint,
                        "qa-physical-relocation-delivery-override");
            }

            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class TestCatalog : IDungeonItemCatalogProvider
    {
        internal const string ItemId = "item:qa:trade-inventory";
        private readonly DungeonItemDefinition definition = new(
            ItemId,
            "목재",
            string.Empty,
            StockCategory.General,
            1,
            null,
            1f,
            20);

        public IReadOnlyList<DungeonItemDefinition> All =>
            new[] { definition };
        public DungeonItemDefinition GetDefinition(string itemId) =>
            string.Equals(itemId, ItemId, StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(itemId);
        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition value)
        {
            value = string.Equals(itemId, ItemId, StringComparison.Ordinal)
                ? definition
                : null;
            return value != null;
        }
    }

    private sealed class TestMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(1_000L);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(1_000L);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(1_000L);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            new(checked(lot.Quantity * 1_000L));
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(quantity * 1_000L));
    }

    private sealed class TestHaulingSettings : IItemHaulingSettingsProvider
    {
        public float MaxCarryMultiplier =>
            CharacterCarryTuning.DefaultMaxCarryMultiplier;
        public ItemHaulingSettingsSnapshot Capture() => new();
        public void Restore(ItemHaulingSettingsSnapshot snapshot)
        {
        }
    }

    private sealed class TestWastePolicyObservationPort :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        internal const string PublicationId = "run:qa-trade-inventory/2";
        private readonly DomainFactDeliveryPhase commitPhase;
        private readonly DomainFactDeliveryPhase replayPhase;

        internal TestWastePolicyObservationPort(
            DomainFactDeliveryPhase commitPhase,
            DomainFactDeliveryPhase replayPhase)
        {
            this.commitPhase = commitPhase;
            this.replayPhase = replayPhase;
        }

        internal int StageCount { get; private set; }
        internal int ReplayCount { get; private set; }
        internal string LastPublicationKey { get; private set; } = string.Empty;

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            if (fact is not WastePolicyChangedFact policy)
            {
                stage = null;
                failure = "qa-waste-policy-fact-type-invalid";
                return false;
            }
            StageCount++;
            LastPublicationKey = "qa:waste-policy-observation:"
                + policy.OperationId;
            stage = new Stage(
                LastPublicationKey,
                policy.OwnerRevision,
                commitPhase);
            failure = string.Empty;
            return true;
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (facts == null || results == null
                || facts.Count != 1 || results.Length != 1)
            {
                failure = "qa-waste-policy-replay-shape-invalid";
                return false;
            }
            ReplayCount++;
            results[0] = Delivery(
                replayPhase,
                facts[0].PublicationKey,
                "qa-waste-policy-replay");
            failure = string.Empty;
            return replayPhase != DomainFactDeliveryPhase.Rejected;
        }

        private static DomainFactDelivery Delivery(
            DomainFactDeliveryPhase phase,
            string operationKey,
            string detail) => phase == DomainFactDeliveryPhase.Rejected
            ? DomainFactDelivery.Reject(detail)
            : new DomainFactDelivery(
                phase,
                operationKey,
                PublicationId,
                LowercaseHash,
                detail);

        private sealed class Stage : IDomainFactStage
        {
            private readonly DomainFactDeliveryPhase phase;
            private bool active = true;

            internal Stage(
                string operationKey,
                long ownerRevision,
                DomainFactDeliveryPhase phase)
            {
                OperationKey = operationKey;
                OwnerRevision = ownerRevision;
                this.phase = phase;
            }

            public string OperationKey { get; }
            public long OwnerRevision { get; }
            public bool IsReplay => false;
            public bool IsActive => active;

            public DomainFactDelivery Commit()
            {
                if (!active)
                    return DomainFactDelivery.Reject(
                        "qa-waste-policy-stage-inactive");
                active = false;
                return Delivery(
                    phase,
                    OperationKey,
                    phase == DomainFactDeliveryPhase.Rejected
                        ? "qa-waste-policy-pre-canonical-fault"
                        : "qa-waste-policy-delivery-pending");
            }

            public void Cancel() => active = false;
        }
    }

    private sealed class TestWasteInventory : IWasteProcessingInventoryPort
    {
        public bool HasExactWildlifeCareDestinationAuthority(
            string destinationId,
            Vector2Int destinationPosition) => false;
        public bool TryGetExactWildlifeCareDestinationPosition(
            string destinationId,
            out Vector2Int destinationPosition)
        {
            destinationPosition = default;
            return false;
        }
        public IReadOnlyList<WasteProcessingStackSnapshot> GetAllStacks() =>
            Array.Empty<WasteProcessingStackSnapshot>();
        public bool TryRequestStackDelivery(
            ItemStackId stackId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out DomainFailure failure)
        {
            requested = 0;
            failure = new DomainFailure(FailureCode.WasteFeedUnavailable);
            return false;
        }
    }

    private sealed class TestWasteProduction : IWasteProcessingProductionPort
    {
        public int CountBillsMatching(Func<string, bool> recipePredicate) => 0;
        public void EnsureSingleBill(ProductionRecipeSO recipe)
        {
        }
    }

    private sealed class TestWasteCatalog : IResourceEconomyContentCatalog
    {
        public IReadOnlyList<ResourceItemDefinitionSO> Items =>
            Array.Empty<ResourceItemDefinitionSO>();
        public IReadOnlyList<ProductionRecipeSO> Recipes =>
            Array.Empty<ProductionRecipeSO>();
        public IReadOnlyList<CropDefinitionSO> Crops =>
            Array.Empty<CropDefinitionSO>();
        public IReadOnlyList<CraftMaterialDefinitionSO> Materials =>
            Array.Empty<CraftMaterialDefinitionSO>();
        public IReadOnlyList<SubstanceDefinitionView> Substances =>
            Array.Empty<SubstanceDefinitionView>();
        public bool TryGetItem(
            string itemId,
            out ResourceItemDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetRecipe(
            string recipeId,
            out ProductionRecipeSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetCrop(string cropId, out CropDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetMaterial(
            string materialId,
            out CraftMaterialDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetSubstance(
            string substanceId,
            out SubstanceDefinitionView definition)
        {
            definition = default;
            return false;
        }
    }

    private sealed class TestWasteClock : IGameClock
    {
        public float DeltaTime => 1f;
        public float Time => 100f;
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class TestWasteRules : IWasteProcessingRules
    {
        private static readonly WasteOriginKind[] SupportedOrigins =
        {
            WasteOriginKind.Plant,
            WasteOriginKind.Animal,
            WasteOriginKind.Mixed,
            WasteOriginKind.Forbidden
        };

        public float TickIntervalSeconds => 5f;
        public float ToxicThreshold => 80f;
        public IReadOnlyCollection<WasteOriginKind> Origins => SupportedOrigins;
        public WastePolicyData CreateDefaultPolicy(WasteOriginKind origin) =>
            new()
            {
                origin = origin,
                disposition = WasteDispositionKind.Store,
                enabled = true,
                maximumFeedContamination = 79f
            };
        public bool IsSupported(
            WasteOriginKind origin,
            WasteDispositionKind disposition) =>
            origin is >= WasteOriginKind.Plant and <= WasteOriginKind.Forbidden
            && Enum.IsDefined(typeof(WasteDispositionKind), disposition);
        public bool TryGetRecipeId(
            WasteOriginKind origin,
            WasteDispositionKind disposition,
            out string recipeId)
        {
            recipeId = string.Empty;
            return false;
        }
        public bool IsWasteRecipe(string recipeId) => false;
        public bool TryGetFeedValues(
            WildlifeDietType diet,
            WasteOriginKind origin,
            out float nutrition,
            out float diseaseChance)
        {
            nutrition = 0f;
            diseaseChance = 0f;
            return false;
        }
        public bool TryGetLegacyWaste(
            string itemId,
            out WasteOriginKind origin,
            out float contamination)
        {
            origin = WasteOriginKind.Unknown;
            contamination = 0f;
            return false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
