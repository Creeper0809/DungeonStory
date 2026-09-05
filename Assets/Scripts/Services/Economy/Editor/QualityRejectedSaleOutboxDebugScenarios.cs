#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class QualityRejectedSaleOutboxDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Economy/Run Quality-Rejected Sale Outbox Contracts")]
    public static void RunAll() => Debug.Log(Verify());

    public static string Verify()
    {
        VerifyIdempotentMoneyCredit();
        VerifyIdempotentMoneyDebit();
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(catalog);
        DungeonRuntimeAggregateRootStore aggregateRootStore = new();
        WorldItemStackRuntime items = PhysicalItemDebugScenarios
            .CreateRuntimeForCrossDomainFixture(
                catalog,
                aggregateRootStore,
                out WorldItemRepository repository,
                out _,
                out _,
                out _,
                out _,
                out IPhysicalItemBatchDispositionService dispositions);
        PhysicalStockQuery query = new(repository, catalog, massQuery);

        const string itemId = "material:lumber";
        const string instanceId = "fixture:quality-rejected-sale:instance";
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            quantity: 1,
            state: WorldItemStackState.FacilityBuffer,
            destinationId: QualityRejectedOutputRules.MarketDestinationId,
            position: new Vector2Int(4, 3),
            itemInstanceId: instanceId);
        WorldItemStackSnapshot source = query.GetAllStacks().Single();
        FacilityBufferAcknowledgedOutputReleaseTarget target = new(
            QualityRejectedOutputRules.MarketDestinationId,
            new Vector2Int(4, 3));
        QualityRejectedSalePending pending =
            QualityRejectedSaleOutbox.CreatePrepared(
                sequence: 1,
                source,
                proceeds: 9,
                target,
                requiresCombatAuthority: true);

        Require(dispositions.TryCommitPending(
                new[] { new PhysicalItemTransformInput(stackId, 1) },
                PhysicalItemDispositionKind.Transfer,
                pending.operationId,
                pending.reasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "Physical quality-rejected sale commit failed: " + commitFailure);
        Require(repository.GetEditorTestQuantity(stackId) == 0,
            "Committed sale did not remove the exact physical source.");

        FaultPort first = new(dispositions, receipt)
        {
            FailNextAcknowledgement = true
        };
        Require(!QualityRejectedSaleOutbox.TryFinalizePending(
                pending,
                first,
                out _)
            && pending.phase
                == QualityRejectedSaleCommitPhase.UniqueAuthorityReleased
            && first.IncomePublications == 1
            && first.UniqueAuthorityReleases == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Injected acknowledgement failure did not preserve the terminal outbox owner.");

        QualityRejectedSalePending restored =
            JsonUtility.FromJson<QualityRejectedSalePending>(
                JsonUtility.ToJson(pending));
        DungeonResourceStockPolicySaveData save = new()
        {
            nextSaleSequence = 2,
            pendingRejectedSales = new List<QualityRejectedSalePending>
            {
                restored.Clone()
            }
        };
        ResourceStockPolicySaveSection.ValidatePhysicalRestoreCandidate(
            save,
            new CandidateQuery(ToCandidate(receipt)));
        VerifyWholeRegistryRestore(
            aggregateRootStore,
            items,
            repository,
            save,
            massQuery);

        FaultPort recovery = new(dispositions, receipt);
        Require(QualityRejectedSaleOutbox.TryFinalizePending(
                restored,
                recovery,
                out string recoveryFailure)
            && string.IsNullOrEmpty(recoveryFailure)
            && recovery.IncomePublications == 0
            && recovery.UniqueAuthorityReleases == 0
            && recovery.SuccessfulAcknowledgements == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "Restored terminal outbox replayed income/authority or failed exact acknowledgement.");

        Require(Throws(() =>
                ResourceStockPolicySaveSection.ValidatePhysicalRestoreCandidate(
                    save,
                    CandidateQuery.Empty)),
            "Restore accepted a quality-rejected sale owner without its physical receipt.");

        items.Dispose();
        return "QUALITY_REJECTED_SALE_OUTBOX_PASS";
    }

    private static void VerifyWholeRegistryRestore(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        WorldItemStackRuntime items,
        WorldItemRepository repository,
        DungeonResourceStockPolicySaveData save,
        IPhysicalItemMassQuery massQuery)
    {
        IResourceEconomyContentCatalog economyCatalog =
            new ResourceEconomyContentCatalog(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
        DungeonResourceStockPolicySaveData registrySave =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                JsonUtility.ToJson(save));
        registrySave.policies = economyCatalog.Items
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(value => new ResourceStockPolicyData
            {
                itemId = value.ItemId,
                enabled = false,
                minimumStock = 10,
                targetStock = 20,
                maximumStock = 40,
                surplusDisposition = StockSurplusDisposition.Hold,
                lastStatus = string.Empty
            })
            .ToList();
        RegistryStockPolicyRuntime runtime = new(registrySave);
        EconomyProjectInputOwnerFixtureAuthority inputOwners = new(massQuery);
        PhysicalItemsSaveSection physical = new(
            items,
            items,
            EmptyWorldCandidates.Instance,
            ProductionOutputLifecycleRestoreCandidatePublisher
                .IsolatedSectionFixtureOnly);
        ResourceStockPolicySaveSection stockPolicies = new(
            runtime,
            economyCatalog,
            items,
            inputOwners.RestoreRuntime);
        IDungeonSaveSection[] sections =
        {
            new RegistryDependencyStubSection(
                ModularFacilityWorldSaveSection.Id,
                DungeonSaveRestorePhase.World),
            new RegistryDependencyStubSection(
                CharacterWorldSaveSection.Id,
                DungeonSaveRestorePhase.Characters),
            physical,
            new RegistryDependencyStubSection(
                ProductionBillsSaveSection.Id,
                DungeonSaveRestorePhase.RuntimeState),
            stockPolicies
        };
        DungeonSaveSectionRegistry registry = new(
            sections,
            aggregateRootStore,
            new[] { (IDungeonRestoreTransactionParticipant)items }
                .Concat(inputOwners.RestoreParticipants)
                .ToArray());
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        DungeonGameRestoreReport report = new();

        Require(registry.RestoreAll(envelopes, report)
                && report.Success
                && !items.IsCandidateAvailable
                && runtime.Capture().pendingRejectedSales.Count == 1
                && runtime.Capture().pendingRejectedSales[0].phase
                    == QualityRejectedSaleCommitPhase.UniqueAuthorityReleased,
            "Whole-registry quality-rejected sale restore was not atomic: "
            + string.Join(" | ", report.Errors));

        List<DungeonSaveSectionEnvelope> missingReceipt = envelopes.Select(
            value => new DungeonSaveSectionEnvelope
            {
                sectionId = value.sectionId,
                sectionVersion = value.sectionVersion,
                restorePhase = value.restorePhase,
                optional = value.optional,
                payloadJson = value.payloadJson
            }).ToList();
        DungeonSaveSectionEnvelope physicalEnvelope = missingReceipt.Single(
            value => string.Equals(
                value.sectionId,
                PhysicalItemsSaveSection.Id,
                StringComparison.Ordinal));
        DungeonPhysicalItemSaveData physicalPayload =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                physicalEnvelope.payloadJson);
        physicalPayload.pendingBatchDispositions.Clear();
        physicalEnvelope.payloadJson = JsonUtility.ToJson(physicalPayload);
        DungeonGameRestoreReport rejected = new();
        Require(!registry.RestoreAll(missingReceipt, rejected)
                && !rejected.Success
                && !items.IsCandidateAvailable,
            "Whole-registry restore accepted a missing quality-sale receipt or leaked its candidate.");
    }

    private static void VerifyIdempotentMoneyCredit()
    {
        EconomyTransactionContext context = new(
            EconomyTransactionKind.SaleIncome,
            "quality-rejected-sale:fixture-credit",
            "material:lumber",
            "focused exact-once credit");

        GameSessionState rollbackState = new(startingMoney: 100);
        FaultLedger throwingLedger = new() { ThrowOnRecord = true };
        GameMoneyAccount rollbackAccount = new(
            new SessionProvider(rollbackState),
            throwingLedger);
        Require(ThrowsAny(() => rollbackAccount.TryCreditOnce(
                9,
                context,
                out _))
            && rollbackAccount.Balance == 100
            && throwingLedger.Records.Count == 0,
            "Ledger failure did not roll back the balance atomically.");

        GameSessionState replayState = new(startingMoney: 100);
        FaultLedger ledger = new();
        GameMoneyAccount account = new(new SessionProvider(replayState), ledger);
        Require(account.TryCreditOnce(9, context, out _)
            && account.TryCreditOnce(9, context, out _)
            && !account.TryCreditOnce(10, context, out _)
            && account.Balance == 109
            && ledger.Records.Count == 1,
            "Exact-once credit replay changed balance or duplicated its ledger record.");
    }

    private static void VerifyIdempotentMoneyDebit()
    {
        EconomyTransactionContext context = new(
            EconomyTransactionKind.FactionTradePurchase,
            "faction-route-settlement:00000001",
            "faction:dungeon:kobold",
            "focused exact-once debit");

        GameSessionState rollbackState = new(startingMoney: 100);
        FaultLedger throwingLedger = new() { ThrowOnRecord = true };
        GameMoneyAccount rollbackAccount = new(
            new SessionProvider(rollbackState),
            throwingLedger);
        Require(ThrowsAny(() => rollbackAccount.TrySpendOnce(
                9,
                context,
                out _,
                out _))
            && rollbackAccount.Balance == 100
            && throwingLedger.Records.Count == 0,
            "Ledger failure did not roll back the exact-once debit atomically.");

        GameSessionState replayState = new(startingMoney: 100);
        FaultLedger ledger = new();
        GameMoneyAccount account = new(new SessionProvider(replayState), ledger);
        Require(account.TrySpendOnce(9, context, out EconomyTransactionRecord first, out _)
            && account.TrySpendOnce(9, context, out EconomyTransactionRecord replay, out _)
            && !account.TrySpendOnce(10, context, out _, out _)
            && first != null
            && replay != null
            && first.transactionId == replay.transactionId
            && first.amount == -9
            && first.balanceBefore == 100
            && first.balanceAfter == 91
            && account.Balance == 91
            && ledger.Records.Count == 1,
            "Exact-once debit replay changed balance, receipt, or ledger cardinality.");

        GameSessionState insufficientState = new(startingMoney: 8);
        FaultLedger insufficientLedger = new();
        GameMoneyAccount insufficient = new(
            new SessionProvider(insufficientState),
            insufficientLedger);
        Require(!insufficient.TrySpendOnce(9, context, out _, out _)
            && insufficient.Balance == 8
            && insufficientLedger.Records.Count == 1
            && !insufficientLedger.Records[0].succeeded,
            "Insufficient exact-once debit mutated balance or omitted failure evidence.");
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot ToCandidate(
        PhysicalItemBatchDispositionReceipt receipt) => new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            receipt.RequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool ThrowsAny(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FaultPort : IQualityRejectedSaleCommandPort
    {
        private readonly IPhysicalItemBatchDispositionService dispositions;
        private readonly PhysicalItemBatchDispositionReceipt receipt;

        internal FaultPort(
            IPhysicalItemBatchDispositionService dispositions,
            PhysicalItemBatchDispositionReceipt receipt)
        {
            this.dispositions = dispositions;
            this.receipt = receipt;
        }

        internal bool FailNextAcknowledgement { get; set; }
        internal int IncomePublications { get; private set; }
        internal int UniqueAuthorityReleases { get; private set; }
        internal int SuccessfulAcknowledgements { get; private set; }

        public bool TryGetPendingRejectedSaleTransfer(
            string operationId,
            out PhysicalItemBatchDispositionReceipt discovered)
        {
            if (!string.Equals(
                    operationId,
                    receipt.OperationId,
                    StringComparison.Ordinal))
            {
                discovered = default;
                return false;
            }
            return dispositions.TryGetPending(operationId, out discovered);
        }

        public bool TryPublishRejectedSaleIncome(
            QualityRejectedSalePending pending,
            out string failureReason)
        {
            IncomePublications++;
            failureReason = string.Empty;
            return true;
        }

        public bool TryReleaseRejectedSaleUniqueAuthority(
            QualityRejectedSalePending pending,
            out string failureReason)
        {
            UniqueAuthorityReleases++;
            failureReason = string.Empty;
            return true;
        }

        public bool AcknowledgeRejectedSaleTransfer(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-quality-sale-ack-failure";
                return false;
            }
            bool acknowledged = dispositions.Acknowledge(
                commitId,
                out failureReason);
            if (acknowledged)
                SuccessfulAcknowledgements++;
            return acknowledged;
        }
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly CandidateQuery Empty = new();
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> pending;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] pending) =>
            this.pending = pending ?? Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => pending;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = pending.FirstOrDefault(value => value != null
                && string.Equals(
                    value.OperationId,
                    operationId,
                    StringComparison.Ordinal));
            return disposition != null;
        }
    }

    private sealed class RegistryStockPolicyRuntime :
        IResourceStockPolicyRuntime
    {
        private DungeonResourceStockPolicySaveData state;

        internal RegistryStockPolicyRuntime(
            DungeonResourceStockPolicySaveData state) =>
            this.state = Clone(state);

        public int Version { get; private set; }
        public IReadOnlyList<ResourceStockPolicyData> Policies =>
            state.policies;

        public ResourceStockPolicyData GetOrCreate(string itemId) =>
            throw new NotSupportedException("restore fixture only");

        public bool SetPolicy(
            ResourceStockPolicyData policy,
            out string failureReason)
        {
            failureReason = "restore fixture only";
            return false;
        }

        public int CountOwned(string itemId) => 0;

        public DungeonResourceStockPolicySaveData Capture() => Clone(state);

        public ResourceStockPolicyRestoreCandidate PrepareRestoreCandidate(
            DungeonResourceStockPolicySaveData saveData) =>
            new(new ResourceStockPolicyAggregateState(), Clone(saveData));

        public void PublishRestoreCandidate(
            ResourceStockPolicyRestoreCandidate candidate)
        {
            state = Clone(candidate.Payload);
            Version++;
        }

        private static DungeonResourceStockPolicySaveData Clone(
            DungeonResourceStockPolicySaveData value) =>
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                JsonUtility.ToJson(value));
    }

    private sealed class RegistryDependencyStubSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        internal RegistryDependencyStubSection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase,
            params string[] dependencies)
        {
            SectionId = sectionId;
            RestorePhase = restorePhase;
            this.dependencies = dependencies ?? Array.Empty<string>();
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => dependencies;
        public string Capture() => "{\"fixture\":true}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError(
                    "Invalid quality-sale registry dependency payload.");
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
    }

    private sealed class EmptyWorldCandidates : IRestoreWorldCandidateQuery
    {
        internal static readonly EmptyWorldCandidates Instance = new();

        public int Revision => 0;

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }

        public bool TryGetBuildings(
            out IReadOnlyList<BuildableObject> buildings)
        {
            buildings = Array.Empty<BuildableObject>();
            return true;
        }

        public bool TryGetCharacters(
            out IReadOnlyList<CharacterActor> characters)
        {
            characters = Array.Empty<CharacterActor>();
            return true;
        }

        public bool TryGetWildlife(
            out IReadOnlyList<WildlifeActor> wildlife)
        {
            wildlife = Array.Empty<WildlifeActor>();
            return true;
        }

        public bool TryGetExteriorZones(
            out IReadOnlyList<ExteriorZoneMarker> zones)
        {
            zones = Array.Empty<ExteriorZoneMarker>();
            return true;
        }
    }

    private sealed class SessionProvider : IGameSessionStateProvider
    {
        private readonly GameSessionState state;

        internal SessionProvider(GameSessionState state) => this.state = state;

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = state;
            return true;
        }
    }

    private sealed class FaultLedger : IEconomyTransactionLedger
    {
        private readonly List<EconomyTransactionRecord> records = new();

        internal bool ThrowOnRecord { get; set; }
        public IReadOnlyList<EconomyTransactionRecord> Records => records;

        public void RecordSuccess(
            EconomyTransactionContext context,
            int amount,
            int balanceBefore,
            int balanceAfter)
        {
            if (ThrowOnRecord)
                throw new InvalidOperationException("injected-ledger-failure");
            records.Add(new EconomyTransactionRecord
            {
                transactionId = $"fixture:{records.Count + 1}",
                kind = context.kind,
                sourceId = context.sourceId,
                targetId = context.targetId,
                description = context.description,
                amount = amount,
                balanceBefore = balanceBefore,
                balanceAfter = balanceAfter,
                succeeded = true
            });
        }

        public void RecordFailure(
            EconomyTransactionContext context,
            int amount,
            string reason,
            int balanceAfter)
        {
            if (ThrowOnRecord)
                throw new InvalidOperationException("injected-ledger-failure");
            records.Add(new EconomyTransactionRecord
            {
                transactionId = $"fixture:{records.Count + 1}",
                kind = context.kind,
                sourceId = context.sourceId,
                targetId = context.targetId,
                description = context.description,
                amount = -Math.Abs(amount),
                balanceBefore = balanceAfter,
                balanceAfter = balanceAfter,
                succeeded = false,
                failureReason = reason ?? string.Empty
            });
        }

        public int SumSince(float gameTime, bool income) => 0;

        public bool TryGetSuccessfulBySource(
            EconomyTransactionKind kind,
            string sourceId,
            out EconomyTransactionRecord record)
        {
            record = records.LastOrDefault(value => value != null
                && value.succeeded
                && value.kind == kind
                && string.Equals(
                    value.sourceId,
                    sourceId,
                    StringComparison.Ordinal));
            return record != null;
        }

        public EconomyTransactionLedgerSaveData Capture() => new()
        {
            nextSequence = records.Count + 1,
            records = records.Select(value => value.Clone()).ToList()
        };
    }
}
#endif
