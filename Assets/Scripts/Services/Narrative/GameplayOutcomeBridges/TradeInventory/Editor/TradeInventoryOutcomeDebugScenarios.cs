using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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

        WasteProcessingRuntime rollbackRuntime = new(
            new WasteProcessingMaterialDependencies(inventory, catalog),
            new WasteProcessingOperationDependencies(production, clock, rules),
            new DungeonRuntimeAggregateRootStore(),
            new TestWastePolicyParticipant(
                canonicalCommit: false,
                acknowledged: false));
        WastePolicyCommandResult failed = rollbackRuntime.SetPolicy(desired);
        DungeonWasteProcessingSaveData rolledBack = rollbackRuntime.Capture();
        WastePolicyData original = rollbackRuntime.GetPolicy(
            WasteOriginKind.Plant);
        Require(
            !failed.Succeeded
            && original.disposition == WasteDispositionKind.Store
            && original.enabled
            && Mathf.Approximately(original.maximumFeedContamination, 79f)
            && rolledBack.nextOutcomeSequence == 1L
            && rolledBack.pendingPolicyOutcome == null,
            "A pre-canonical waste-policy outcome failure must restore the exact policy and sequence.");

        WasteProcessingRuntime source = new(
            new WasteProcessingMaterialDependencies(inventory, catalog),
            new WasteProcessingOperationDependencies(production, clock, rules),
            new DungeonRuntimeAggregateRootStore(),
            new TestWastePolicyParticipant(
                canonicalCommit: true,
                acknowledged: false));
        WastePolicyCommandResult committed = source.SetPolicy(desired);
        DungeonWasteProcessingSaveData interrupted = source.Capture();
        Require(
            committed.Succeeded
            && committed.OperationId == "waste-policy:1:command:1"
            && committed.OwnerRevision == 1L
            && interrupted.nextOutcomeSequence == 2L
            && interrupted.pendingPolicyOutcome != null
            && interrupted.pendingPolicyOutcome.gameplayOutcome != null,
            "A canonical-but-unacknowledged policy result must remain in the owner save outbox.");

        WasteProcessingRuntime restored = new(
            new WasteProcessingMaterialDependencies(inventory, catalog),
            new WasteProcessingOperationDependencies(production, clock, rules),
            new DungeonRuntimeAggregateRootStore(),
            new TestWastePolicyParticipant(
                canonicalCommit: true,
                acknowledged: true));
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
            && reconciled.nextOutcomeSequence == 2L
            && reconciled.pendingPolicyOutcome == null,
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
            diagnostics,
            null);
        CanonicalCommitIdentityDeferredParticipant participant = new(
            diagnostics);
        string operation = "qa:trade-inventory:post-commit-fault";
        Require(
            ((IOutcomeAwarePhysicalItemBatchDispositionService)service)
                .TryCommitPending(
                    new[] { new PhysicalItemTransformInput(stackId, 1) },
                    PhysicalItemDispositionKind.Sink,
                    operation,
                    "qa-post-commit-fault",
                    participant,
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
            catalog,
            hauling,
            repository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        DungeonPhysicalItemSaveData save = sourcePersistence.Capture();
        Require(
            save.pendingBatchDispositions.Count == 1
            && save.pendingBatchDispositions[0].gameplayOutcomeExpected
            && save.pendingBatchDispositions[0].gameplayOutcome == null,
            "The unresolved exact outcome key must survive physical capture.");

        WorldItemRepository restoredRepository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        WorldItemPersistenceService restoredPersistence = new(
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
            diagnostics,
            null);
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
        MutableOutcomeDiagnostics diagnostics = new();
        RetryOnlyOutcomeRecorder recorder = new(diagnostics);
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
            new DeferredRelocationOutcomeParticipant(diagnostics),
            recorder,
            diagnostics);
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
            catalog,
            new TestHaulingSettings(),
            repository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        DungeonPhysicalItemSaveData saved = persistence.Capture();
        Require(
            saved.physicalItemRelocations.Count == 1
            && saved.physicalItemRelocations[0].phase == (int)
                PhysicalItemRelocationJournalPhase.CanonicalOutcomeCommitted,
            "Commit-before-delivery relocation join was not saved.");

        WorldItemRepository restoredRepository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        WorldItemPersistenceService restoredPersistence = new(
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
            new DeferredRelocationOutcomeParticipant(diagnostics),
            recorder,
            diagnostics);
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

        diagnostics.State = GameplayOutcomeReplayState.Compacted;
        Require(
            restored.TryRelocateQuantity(
                stackId,
                1,
                destinationPosition,
                WorldItemStackState.Loose,
                string.Empty,
                operation,
                "qa-relocation-journal",
                out _,
                out _),
            "Compacted acknowledgement proof did not preserve replay identity.");
        DungeonPhysicalItemSaveData acknowledged =
            restoredPersistence.Capture();
        Require(
            acknowledged.physicalItemRelocations.Single().phase == (int)
                PhysicalItemRelocationJournalPhase.PublishedAcknowledged,
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

    private sealed class DeferredRelocationOutcomeParticipant :
        IPhysicalItemRelocationOutcomeParticipant
    {
        private readonly MutableOutcomeDiagnostics diagnostics;

        internal DeferredRelocationOutcomeParticipant(
            MutableOutcomeDiagnostics diagnostics) =>
            this.diagnostics = diagnostics;

        public bool TryPrepare(
            in PreparedPhysicalItemRelocationPreview preview,
            out IPreparedPhysicalItemGameplayOutcome prepared,
            out string failureReason)
        {
            GameplayResultKey key = new(
                "qa.trade-inventory.relocation",
                new GameplayOperationId(preview.Receipt.OperationId),
                0L,
                0);
            prepared = new Prepared(diagnostics, key);
            failureReason = string.Empty;
            return preview.IsValid;
        }

        private sealed class Prepared : IPreparedPhysicalItemGameplayOutcome
        {
            private readonly MutableOutcomeDiagnostics diagnostics;

            internal Prepared(
                MutableOutcomeDiagnostics diagnostics,
                GameplayResultKey key)
            {
                this.diagnostics = diagnostics;
                ResultKey = key;
            }

            public GameplayResultKey ResultKey { get; }
            public bool IsCanonicalReplay => false;

            public bool TryCommit(
                long expectedOwnerRevision,
                out PhysicalGameplayOutcomeAttachment attachment,
                out bool canonicalCommitted,
                out string failureReason)
            {
                diagnostics.State =
                    GameplayOutcomeReplayState.DeliveryFaultPending;
                diagnostics.SetIdentity(ResultKey);
                diagnostics.TryGetResultIdentity(
                    ResultKey,
                    out GameplayOutcomeReplayIdentity identity);
                attachment = new PhysicalGameplayOutcomeAttachment(
                    identity.ResultKey,
                    identity.OutcomeId,
                    identity.State,
                    identity.CanonicalPayloadHash);
                canonicalCommitted = true;
                failureReason = "qa-delivery-pending";
                return true;
            }

            public void Cancel()
            {
            }
        }
    }

    private sealed class RetryOnlyOutcomeRecorder : IGameplayOutcomeRecorder
    {
        private readonly MutableOutcomeDiagnostics diagnostics;

        internal RetryOnlyOutcomeRecorder(
            MutableOutcomeDiagnostics diagnostics) =>
            this.diagnostics = diagnostics;

        public int RetryPendingDeliveries(int maximumCount)
        {
            if (maximumCount > 0
                && diagnostics.State ==
                    GameplayOutcomeReplayState.DeliveryFaultPending)
            {
                diagnostics.State =
                    GameplayOutcomeReplayState.PublishedAcknowledged;
                return 1;
            }
            return 0;
        }

        public OutcomePrepareResult TryPrepare<TReceipt>(
            in TReceipt receipt,
            out PreparedOutcomeToken prepared) =>
            throw new NotSupportedException();
        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation) =>
            throw new NotSupportedException();
        public OutcomePrepareResult TryWriteReserved<TReceipt>(
            in TReceipt receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared) =>
            throw new NotSupportedException();
        public void CancelReservation(
            in PreparedOutcomeReservation reservation) =>
            throw new NotSupportedException();
        public void CancelPrepared(in PreparedOutcomeToken prepared) =>
            throw new NotSupportedException();
        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken prepared,
            long expectedOwnerRevision,
            out CommittedOutcomeToken committed) =>
            throw new NotSupportedException();
        public OutcomeDeliveryResult TryDeliver(
            in CommittedOutcomeToken committed) =>
            throw new NotSupportedException();
        public OutcomeAcknowledgeResult Acknowledge(
            in CommittedOutcomeToken committed) =>
            throw new NotSupportedException();
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

    private sealed class TestWastePolicyParticipant :
        IWastePolicyGameplayOutcomeParticipant
    {
        private readonly bool canonicalCommit;
        private readonly bool acknowledged;

        internal TestWastePolicyParticipant(
            bool canonicalCommit,
            bool acknowledged)
        {
            this.canonicalCommit = canonicalCommit;
            this.acknowledged = acknowledged;
        }

        public bool TryPrepare(
            in WastePolicyGameplayOutcomePreview preview,
            out IPreparedWastePolicyGameplayOutcome prepared,
            out string failureReason)
        {
            prepared = preview.IsValid
                ? new Prepared(preview, canonicalCommit, acknowledged)
                : null;
            failureReason = prepared == null
                ? "qa-waste-policy-preview-invalid"
                : string.Empty;
            return prepared != null;
        }

        private sealed class Prepared : IPreparedWastePolicyGameplayOutcome
        {
            private readonly bool canonicalCommit;
            private readonly bool acknowledged;

            internal Prepared(
                in WastePolicyGameplayOutcomePreview preview,
                bool canonicalCommit,
                bool acknowledged)
            {
                this.canonicalCommit = canonicalCommit;
                this.acknowledged = acknowledged;
                ResultKey = new WastePolicyOutcomeKey(
                    "trade-inventory.waste-policy-command-result",
                    preview.OperationId,
                    preview.OwnerRevision,
                    0);
            }

            public WastePolicyOutcomeKey ResultKey { get; }
            public bool IsCanonicalReplay => acknowledged;

            public bool TryCommit(
                long expectedOwnerRevision,
                out WastePolicyOutcomeAttachment attachment,
                out bool canonicalCommitted,
                out string failureReason)
            {
                canonicalCommitted = canonicalCommit;
                failureReason = canonicalCommit
                    ? acknowledged
                        ? string.Empty
                        : "qa-waste-policy-delivery-pending"
                    : "qa-waste-policy-pre-canonical-fault";
                attachment = canonicalCommit
                    ? new WastePolicyOutcomeAttachment(
                        ResultKey,
                        "run:qa-trade-inventory",
                        2L,
                        acknowledged ? 6 : 4,
                        acknowledged,
                        LowercaseHash)
                    : default;
                return canonicalCommit;
            }

            public void Cancel()
            {
            }
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
