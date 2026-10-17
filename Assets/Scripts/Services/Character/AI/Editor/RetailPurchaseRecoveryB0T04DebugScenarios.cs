#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// B0 T04 fault boundaries. This uses the production Shop, AbilityShopping,
/// exact-lot inventory, and outcome transaction; only external ports fault.
/// </summary>
public static partial class RetailPurchaseRecoveryB0T04DebugScenarios
{
    private const int Cost = 25;
    private const int StartingMoney = 500;
    private const MigratedProducerOutcomeKind OutcomeKind =
        MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult;

    [MenuItem("DungeonStory/Debug/AI/Run B0 T04 Retail Purchase Recovery")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
            Debug.LogError("B0 T04 retail purchase recovery scenarios failed.");
    }

    public static bool RunAll(bool logSuccess)
    {
        var failures = new List<string>();
        Run("physical failure before parent outcome", PhysicalFailure, failures);
        Run("outcome failure after physical commit", OutcomeFailure, failures);
        Run("revenue failure after outcome commit", RevenueFailure, failures);
        Run("second unit failure preserves unit identity", SecondUnitFailure, failures);
        foreach (string failure in failures)
            Debug.LogError(failure);
        if (failures.Count == 0 && logSuccess)
            Debug.Log("B0 T04 retail purchase recovery scenarios passed.");
        return failures.Count == 0;
    }

    public static void RunOrThrow()
    {
        if (!RunAll(true))
            throw new InvalidOperationException(
                "B0 T04 retail purchase recovery scenarios failed.");
    }

    public static void RunT04FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_T04_RETAIL_RECOVERY_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_T04_RETAIL_RECOVERY_REPORT_PATH is required.");
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "T04 retail-recovery report must be a create-only path under Artifacts/QA.");
        }

        RunOrThrow();
        RunBoundContinuationScenarios();
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        using FileStream stream = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(
            "U0_T04_RETAIL_RECOVERY=PASS\n"
            + "faults=physical-sink,outcome-commit,revenue-credit,second-unit\n"
            + "savedPhases=LotTaken,PhysicalCommitted,OutcomeCommitted\n"
            + "binding=shop-use-operation,cart-index,purchase-operation\n"
            + "terminal=physical-once,outcome-once,revenue-once,replay-unchanged\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_T04_RETAIL_RECOVERY=PASS; report=" + fullPath);
    }

    public static void RunBoundContinuationScenarios()
    {
        foreach (RetailPurchaseRecoveryPhase boundary in new[] {
            RetailPurchaseRecoveryPhase.LotTaken, RetailPurchaseRecoveryPhase.PhysicalCommitted,
            RetailPurchaseRecoveryPhase.OutcomeCommitted })
        {
            using var context = new Context(1);
            ShopUseExecutionState use = context.BindVisit();
            if (boundary == RetailPurchaseRecoveryPhase.LotTaken) context.Physical.FailNext = true;
            if (boundary == RetailPurchaseRecoveryPhase.PhysicalCommitted)
                context.Outcomes.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            if (boundary == RetailPurchaseRecoveryPhase.OutcomeCommitted) context.Money.FailNextCredit = true;
            bool rejected = false;
            try { context.RunBoundPurchase(); }
            catch (InvalidOperationException error) { rejected = error.Message.Contains("durably pending recovery"); }
            RetailPurchaseRecoverySaveData pending = context.OnlyPending();
            if (!rejected || pending.phase != boundary || pending.shopUseOperationId != use.operationId
                || pending.shopUseCartIndex != 0 || use.cart[0].pendingPurchaseOperationId != pending.operationId)
                throw new InvalidOperationException("Bound purchase did not preserve its exact pending slot: " + boundary);

            RetailPurchaseRecoverySaveData wrongSlot = pending.Clone();
            wrongSlot.shopUseCartIndex = 1;
            bool wrongSlotRejected = false;
            try { context.Shop.ValidatePurchaseBinding(wrongSlot); }
            catch (InvalidOperationException) { wrongSlotRejected = true; }
            if (!wrongSlotRejected) throw new InvalidOperationException("Wrong cart slot accepted.");

            context.RestoreOwners();
            context.Outcomes.FaultMode = MigratedProducerOutcomeFaultMode.None;
            BuildingRetailPurchaseCommitResult completed = context.RunBoundPurchase();
            ShopUseExecutionState resumed = context.Shop.FindActiveShopUse(context.BuyerId);
            if (!completed.Committed || !resumed.cart[0].Committed
                || resumed.cart[0].purchaseOperationId != pending.operationId
                || resumed.cart[0].pendingPurchaseOperationId.Length != 0
                || !context.IsSettled(1, 1, Cost))
                throw new InvalidOperationException("Bound recovery failed: " + boundary);
            context.RestoreOwners();
            int attempts = context.Physical.Attempts;
            int creditAttempts = context.Money.CreditAttempts;
            BuildingRetailPurchaseCommitResult replay = context.RunBoundPurchase();
            if (!replay.Committed || context.Physical.Attempts != attempts
                || context.Money.CreditAttempts != creditAttempts || !context.IsSettled(1, 1, Cost))
                throw new InvalidOperationException("Completed cart replay consumed or charged twice.");
        }
        Debug.Log("SHOP_BOUND_PURCHASE_CONTINUATION=PASS; boundaries=3; cart-json=1; replay-once=1; whole-world=not-tested");
    }

    internal static void RunT03OrThrow()
    {
        var failures = new List<string>();
        Run("T03 exact unique lot", () => TypedPurchase(unique: true), failures);
        Run("T03 exact fungible lot", () => TypedPurchase(unique: false), failures);
        Run("T03 unchanged rejection", UnchangedRejection, failures);
        Run("T03 saved delivery retry", SavedDeliveryRetry, failures);
        if (failures.Count != 0)
            throw new InvalidOperationException(string.Join(" | ", failures));
    }

    internal static bool VerifyAuthoredUniquePurchase() => TypedPurchase(unique: true);

    private static bool TypedPurchase(bool unique)
    {
        using var context = new Context(1, unique);
        string operation = context.PreviewOperationId();
        if (!context.PurchaseSucceeds(out BuildingRetailPurchaseCommitResult result))
            return false;
        RetailStockLotSnapshot lot = result.CommittedLot;
        GameplayResultKey key = context.Outcomes.LatestCommittedKey(OutcomeKind);
        if (!key.IsValid
            || !context.Fixture.Ledger.TryGetResultIdentity(
                key, out GameplayOutcomeReplayIdentity identity)
            || !context.Fixture.Ledger.TryGetExact(
                identity.OutcomeId, out GameplayOutcomeSnapshot exact))
            return false;
        bool Fact(string id, string value) => exact.facts.Any(row =>
            row.factId == id && row.value == value);
        string Optional(string value) => string.IsNullOrEmpty(value)
            ? "none" : value;
        return exact.status == GameplayOutcomeStatus.Succeeded
            && exact.participants.Count == 3
            && exact.participants.Any(row => row.entityId == context.BuyerId
                && row.roleId == "customer")
            && exact.participants.Any(row => row.entityId == context.FacilityId
                && row.roleId == "facility")
            && exact.metrics.Any(row => row.metricId == "quantity"
                && row.value == 1d && row.unitId == "count")
            && exact.metrics.Any(row => row.metricId == "mass"
                && row.value == lot.unitMassGrams && row.unitId == "gram")
            && exact.metrics.Any(row => row.metricId == "amount"
                && row.value == Cost && row.unitId == "currency")
            && exact.metrics.Any(row => row.metricId == "previous"
                && row.value == StartingMoney)
            && exact.metrics.Any(row => row.metricId == "current"
                && row.value == StartingMoney - Cost)
            && Fact("retail-sale-item-id", lot.saleItemId.ToString())
            && Fact("retail-item-definition-id", lot.itemDefinitionId)
            && Fact("retail-item-instance-id", Optional(lot.itemInstanceId))
            && Fact("retail-source-stack-id", Optional(lot.sourceStackId))
            && Fact("retail-source-operation-id", lot.sourceOperationId)
            && Fact("retail-component-fingerprint",
                Optional(lot.componentFingerprint))
            && Fact("retail-buyer-debit-operation-id", operation)
            && Fact("retail-purchase-result-key", key.ToString())
            && Fact("retail-physical-disposition",
                "retail-terminal-sink-committed")
            && (unique == !string.IsNullOrEmpty(lot.itemInstanceId))
            && context.IsSettled(1, 1, Cost);
    }

    private static bool UnchangedRejection()
    {
        using var context = new Context(1);
        context.Shopping.RestorePersistentState(2, 0, Cost - 1);
        bool rejected = context.PurchaseRejects(
            "shopping-funds-changed-before-commit");
        int pending = context.Shopping.CapturePendingRetailPurchases().Count;
        int commits = context.Outcomes.CommitCount(OutcomeKind);
        int exact = context.Fixture.Ledger.CaptureGameplayOutcomes()
            .exactOutcomes.Count;
        if (!rejected
            || context.Shopping.HoldingMoney != Cost - 1
            || context.StockCount != 1
            || pending != 0
            || commits != 0
            || exact != 0)
        {
            throw new InvalidOperationException(
                "T03 rejection boundary mismatch: rejected=" + rejected
                + ", money=" + context.Shopping.HoldingMoney
                + ", stock=" + context.StockCount
                + ", pending=" + pending
                + ", commits=" + commits
                + ", exact=" + exact + ".");
        }
        return true;
    }

    private static bool SavedDeliveryRetry()
    {
        using var context = new Context(1, deliveryFault: true);
        if (!context.PurchaseSucceeds(out _))
            return false;
        GameplayResultKey key = context.Outcomes.LatestCommittedKey(OutcomeKind);
        GameplayOutcomeLedgerSaveData saved =
            context.Fixture.Ledger.CaptureGameplayOutcomes();
        if (saved.outbox.Count != 1
            || saved.outbox[0].operationId != key.OperationId.Value)
            return false;
        int money = context.Shopping.HoldingMoney;
        int revenue = context.Money.Credits;
        context.RestoreOwners();
        context.DeliveryFault.Enabled = false;
        GameplayOutcomeLedger ledger = context.Fixture.Ledger;
        ledger.BeginRestoreCandidate();
        try
        {
            GameplayOutcomeLedgerRestoreCandidate candidate =
                ledger.PrepareGameplayOutcomeRestore(saved);
            ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate();
            ledger.CompleteRestoreCandidate();
        }
        catch
        {
            ledger.DiscardRestoreCandidate();
            throw;
        }
        return context.Fixture.Recorder.RetryPendingDeliveries(16) == 1
            && ledger.TryGetResultIdentity(
                key, out GameplayOutcomeReplayIdentity replayed)
            && replayed.State == GameplayOutcomeReplayState.PublishedAcknowledged
            && ledger.CaptureGameplayOutcomes().outbox.Count == 0
            && context.Shopping.HoldingMoney == money
            && context.Money.Credits == revenue
            && context.StockCount == 0
            && context.Shopping.CapturePendingRetailPurchases().Count == 0;
    }

    private static void Run(string name, Func<bool> scenario, List<string> failures)
    {
        try
        {
            if (!scenario()) failures.Add("B0 T04: " + name + " returned false.");
        }
        catch (Exception exception)
        {
            failures.Add("B0 T04: " + name + ": " + exception);
        }
    }

    private static bool PhysicalFailure()
    {
        using var context = new Context(1);
        context.Physical.FailNext = true;
        if (!context.PurchaseFails("durably pending recovery"))
            return false;
        RetailPurchaseRecoverySaveData saved = context.OnlyPending();
        if (saved.phase != RetailPurchaseRecoveryPhase.LotTaken
            || context.Shop.GetStockCount() != 0
            || context.Shopping.HoldingMoney != StartingMoney - Cost
            || context.Physical.Commits != 0
            || !context.PhysicalInstanceExists
            || context.Outcomes.CommitCount(OutcomeKind) != 0
            || context.Money.Credits != 0)
            return false;

        context.RestoreOwners();
        if (!context.Shopping.TryRecoverPendingRetailPurchases(
                context.Shop, out string failure))
            throw new InvalidOperationException(failure);
        if (!context.Shopping.TryRecoverPendingRetailPurchases(
                context.Shop, out failure))
            throw new InvalidOperationException(failure);
        return context.IsSettled(1, 1, Cost)
            && context.Physical.Attempts == 2
            && context.Physical.CommittedOperations.Single() == saved.operationId;
    }

    private static bool OutcomeFailure()
    {
        using var context = new Context(1);
        context.Outcomes.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
        if (!context.PurchaseFails("durably pending recovery"))
            return false;
        RetailPurchaseRecoverySaveData saved = context.OnlyPending();
        if (saved.phase != RetailPurchaseRecoveryPhase.PhysicalCommitted
            || context.Physical.Commits != 1
            || context.PhysicalInstanceExists
            || context.Outcomes.CommitCount(OutcomeKind) != 0
            || context.Money.Credits != 0)
            return false;

        context.RestoreOwners();
        context.Outcomes.FaultMode = MigratedProducerOutcomeFaultMode.None;
        if (!context.Shopping.TryRecoverPendingRetailPurchases(
                context.Shop, out string failure))
            throw new InvalidOperationException(failure);
        if (!context.Shopping.TryRecoverPendingRetailPurchases(
                context.Shop, out failure))
            throw new InvalidOperationException(failure);
        return context.IsSettled(1, 1, Cost)
            && context.Physical.Attempts == 1
            && context.Physical.CommittedOperations.Single() == saved.operationId;
    }

    private static bool RevenueFailure()
    {
        using var context = new Context(1);
        context.Money.FailNextCredit = true;
        if (!context.PurchaseFails("durably pending recovery"))
            return false;
        RetailPurchaseRecoverySaveData saved = context.OnlyPending();
        if (saved.phase != RetailPurchaseRecoveryPhase.OutcomeCommitted
            || context.Physical.Commits != 1
            || context.PhysicalInstanceExists
            || context.Outcomes.CommitCount(OutcomeKind) != 1
            || context.Money.Credits != 0)
            return false;

        context.RestoreOwners();
        if (!context.Shopping.TryRecoverPendingRetailPurchases(
                context.Shop, out string failure))
            throw new InvalidOperationException(failure);
        // A second retry after completion must not re-credit the same sale.
        if (!context.Shopping.TryRecoverPendingRetailPurchases(
                context.Shop, out failure))
            throw new InvalidOperationException(failure);
        return context.IsSettled(1, 1, Cost)
            && context.Physical.Attempts == 1
            && context.Outcomes.CommitCount(OutcomeKind) == 1
            && context.Money.CreditAttempts == 2;
    }

    private static bool SecondUnitFailure()
    {
        using var context = new Context(2);
        string firstId = context.PreviewOperationId();
        if (!context.PurchaseSucceeds()
            || context.Physical.CommittedOperations.Single() != firstId)
            return false;

        string secondId = context.PreviewOperationId();
        context.Physical.FailNext = true;
        if (!context.PurchaseFails("durably pending recovery"))
            return false;
        RetailPurchaseRecoverySaveData saved = context.OnlyPending();
        if (saved.phase != RetailPurchaseRecoveryPhase.LotTaken
            || saved.operationId != secondId
            || saved.operationId == firstId
            || context.Shop.GetStockCount() != 0
            || context.Outcomes.CommitCount(OutcomeKind) != 1
            || context.Money.Credits != Cost)
            return false;

        context.RestoreOwners();
        if (!context.Shopping.TryRecoverPendingRetailPurchases(
                context.Shop, out string failure))
            throw new InvalidOperationException(failure);
        return context.IsSettled(2, 2, Cost * 2)
            && context.Physical.Attempts == 3
            && context.Physical.CommittedOperations.SequenceEqual(
                new[] { firstId, secondId })
            && context.Money.CreditOperations.SequenceEqual(new[]
            {
                "retail-revenue:" + firstId,
                "retail-revenue:" + secondId
            });
    }

    private sealed partial class Context : IDisposable
    {
        private static readonly BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly CustomerAiDebugScenarios.CustomerAiScenarioWorld world;
        private readonly CharacterActor customer;
        private readonly RemainStock stock;
        private readonly AIAction action;
        private readonly int initialRevenue;
        private MigratedProducerOutcomeEditorFixture fixture;

        internal Context(int units, bool unique = true, bool deliveryFault = false)
        {
            world = new CustomerAiDebugScenarios.CustomerAiScenarioWorld();
            Shop = world.Place("S01_판매카운터", new Vector2Int(4, 0)) as Shop
                ?? throw new InvalidOperationException("Authored retail counter missing.");
            customer = world.CreateCustomer(
                "Slime", Vector2Int.zero, 90f, 90f, 10f, 20f);
            Shopping = customer.GetAbility<AbilityShopping>()
                ?? throw new InvalidOperationException("Shopping ability missing.");
            Shopping.RestorePersistentState(2, 0, StartingMoney);

            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = OutcomeKind,
                Enabled = deliveryFault
            };
            fixture = new MigratedProducerOutcomeEditorFixture(
                "b0-t04-retail:" + Guid.NewGuid().ToString("N"),
                deliveryFault: deliveryFault ? DeliveryFault : null);
            Outcomes = new MigratedProducerOutcomeProbeTransaction(
                fixture.Transaction, fixture.Recorder) { TargetKind = OutcomeKind };
            Facts = new RetailPurchaseFactEditorFixture(fixture, Outcomes);
            Shopping.ConstructRetailPurchaseFacts(
                Facts.Publisher, Facts.Recovery, new FixedGameSessionStateProvider(7));
            Physical = new PhysicalSinkProbe(
                CharacterAiEditorTestDependencies.RetailStockPhysical);
            Money = new MoneyAccountProbe(1000);
            initialRevenue = Money.Balance;
            SetPort(Shop, "retailStockPhysicalRuntime", Physical);
            SetPort(Shop, "moneyAccount", Money);

            ShopStockStateSnapshot authored = Shop.CreateStockSnapshot();
            SaleItem saleItem = null;
            RetailStockLotSnapshot fungibleSource = unique
                ? null
                : authored.lots.FirstOrDefault(value => value != null
                    && value.quantity > 0
                    && string.IsNullOrEmpty(value.itemInstanceId));
            if (!unique && fungibleSource != null)
                Shop.TryGetSaleItem(fungibleSource.saleItemId, out saleItem);
            foreach (int saleItemId in unique
                         ? Resources.LoadAll<SaleItem>("SO/Stock/Item")
                             .Where(value => value != null)
                             .Select(value => value.id)
                             .Distinct()
                             .OrderBy(value => value)
                         : Enumerable.Empty<int>())
            {
                if (Shop.TryGetSaleItem(
                        saleItemId,
                        out SaleItem authoredSaleItem)
                    && authoredSaleItem != null
                    && PhysicalItemIds.TryGetEquipmentDefinitionId(
                        authoredSaleItem.AuthoredItemDefinitionId,
                        out _))
                {
                    saleItem = authoredSaleItem;
                    break;
                }
            }
            if (saleItem == null)
            {
                throw new InvalidOperationException(
                    unique
                        ? "Retail counter has no authored equipment sale item."
                        : "Retail counter has no authored fungible lot.");
            }
            long unitMassGrams = CharacterAiEditorTestDependencies
                .PhysicalMass
                .GetDefinitionUnitMass(saleItem.ItemDefinitionId)
                .Value;
            var lots = new List<RetailStockLotSnapshot>();
            if (unique)
            {
                for (int ordinal = 0; ordinal < units; ordinal++)
                {
                    string sourceOperationId =
                        "retail-source:b0-t04:"
                        + Guid.NewGuid().ToString("N")
                        + $":{ordinal:D4}";
                    if (!Physical.TryCreateAuthoredUniqueLot(
                            saleItem.id,
                            saleItem.AuthoredItemDefinitionId,
                            unitMassGrams,
                            sourceOperationId,
                            out RetailStockLotSnapshot lot,
                            out string createFailure))
                    {
                        throw new InvalidOperationException(
                            "Could not create exact retail fixture: "
                            + createFailure);
                    }
                    lots.Add(lot);
                }
            }
            else
            {
                RetailStockLotSnapshot lot = fungibleSource.Clone();
                lot.quantity = units;
                lots.Add(lot);
            }
            UniqueInstanceIds = lots
                .Select(value => value.itemInstanceId)
                .ToArray();
            List<int> activated = new(
                authored.activatedAuthoredSaleItemIds);
            if (!activated.Contains(saleItem.id))
                activated.Add(saleItem.id);
            Shop.ApplyStockSnapshot(new ShopStockStateSnapshot
            {
                schemaVersion = ShopStockStateSnapshot.CurrentSchemaVersion,
                activatedAuthoredSaleItemIds = activated,
                lots = lots
            });
            stock = new RemainStock(
                saleItem.id,
                saleItem.itemName,
                Cost,
                units,
                Array.Empty<OnBuyItemSO>());
            AIActionSet set = Resources.Load<AIActionSet>("SO/AI/Action/Shopping")
                ?? throw new InvalidOperationException("Shopping action missing.");
            action = new AIAction(set, AIActionPlan.AtDestination(Shop));
            customer.ai.bestAction = action;
            customer.ai.isBestActionEnd = false;
        }

        internal Shop Shop { get; }
        internal AbilityShopping Shopping { get; }
        internal MigratedProducerOutcomeProbeTransaction Outcomes { get; private set; }
        internal PhysicalSinkProbe Physical { get; }
        internal MoneyAccountProbe Money { get; }
        internal MigratedProducerOutcomeEditorFixture Fixture => fixture;
        internal RetailPurchaseFactEditorFixture Facts { get; private set; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal string BuyerId => customer.Identity.PersistentId;
        internal string FacilityId => Shop.RequirePersistentInstanceId().Value;
        internal int StockCount => Shop.CreateStockSnapshot().lots
            .Where(value => value != null)
            .Sum(value => value.quantity);
        internal IReadOnlyList<string> UniqueInstanceIds { get; }
        internal bool PhysicalInstanceExists =>
            UniqueInstanceIds.Any(value =>
                !string.IsNullOrEmpty(value)
                && CharacterAiEditorTestDependencies.CombatEquipment
                    .TryGetInstance(value, out _));

        internal string PreviewOperationId()
        {
            if (!Shop.TryPreviewExactRetailLotOperationId(
                    stock.id,
                    out string operationId))
            {
                throw new InvalidOperationException(
                    "Exact retail unit missing.");
            }
            return operationId;
        }

        internal ShopUseExecutionState BindVisit()
        {
            ShopUseExecutionState use = Shop.BeginShopExecution(customer.BuildingVisitor, null);
            use.selectionCount = 1;
            use.selectionIndex = 1;
            use.Go(ShopUseExecutionPhase.Purchase);
            use.cart.Add(new ShopUseCartLine { saleItemId = stock.id, pricingCaptured = true,
                price = Cost, feedbackStarted = true, feedbackRemainingSeconds = 0f });
            return use;
        }

        internal BuildingRetailPurchaseCommitResult RunBoundPurchase()
        {
            ShopUseExecutionState use = Shop.FindActiveShopUse(BuyerId);
            if (StockCount > 0) PreparePhysicalExpectation();
            var result = new BuildingRetailPurchaseCommitResult();
            result.BindPricingContext(use.cart[0].Pricing);
            result.BindShopUse(use.operationId, 0);
            IEnumerator purchase = Shopping.BuyItem(stock, Cost, action, Shop, result);
            int steps = 0;
            while (purchase.MoveNext())
                if (++steps > 4) throw new InvalidOperationException("Bound purchase unexpectedly restarted feedback time.");
            return result;
        }

        internal bool PurchaseSucceeds()
        {
            return PurchaseSucceeds(out _);
        }

        internal bool PurchaseSucceeds(
            out BuildingRetailPurchaseCommitResult result)
        {
            PreparePhysicalExpectation();
            result = new BuildingRetailPurchaseCommitResult();
            IEnumerator purchase = Shopping.BuyItem(
                stock, Cost, action, Shop, result);
            return purchase.MoveNext() && !purchase.MoveNext()
                && result.IsResolved && result.Committed;
        }

        internal bool PurchaseRejects(string expectedFailure)
        {
            PreparePhysicalExpectation();
            var result = new BuildingRetailPurchaseCommitResult();
            IEnumerator purchase = Shopping.BuyItem(
                stock, Cost, action, Shop, result);
            return purchase.MoveNext() && !purchase.MoveNext()
                && result.IsResolved && !result.Committed
                && result.FailureCode == expectedFailure;
        }

        internal bool PurchaseFails(string expectedMessage)
        {
            PreparePhysicalExpectation();
            var result = new BuildingRetailPurchaseCommitResult();
            IEnumerator purchase = Shopping.BuyItem(
                stock, Cost, action, Shop, result);
            if (!purchase.MoveNext()) return false;
            try
            {
                purchase.MoveNext();
                return false;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message.Contains(expectedMessage)
                    && !result.IsResolved;
            }
        }

        internal RetailPurchaseRecoverySaveData OnlyPending() =>
            Shopping.CapturePendingRetailPurchases().Single();

        internal void RestoreOwners()
        {
            RetailPurchaseRecoverySaveData[] pending =
                DungeonSaveJson.Deserialize<RetailPurchaseRecoverySaveData[]>(
                    DungeonCharacterWorldSaveCodec.SerializeFields(Shopping.CapturePendingRetailPurchases().ToArray()));
            Shop.ApplyStockSnapshot(Shop.CreateStockSnapshot());
            var module = new ShopUseExecutionStateModule(Shop);
            string executionJson = module.CaptureState();
            ShopInteractionState decoded = DungeonSaveJson.Deserialize<ShopInteractionState>(executionJson);
            ShopInteractionState original = Shop.CaptureInteractionState();
            for (int index = 0; index < original.activeUses.Count; index++)
            {
                ShopUseExecutionState prior = original.activeUses[index], next = decoded.activeUses[index];
                if ((prior.patience == null) != (next.patience == null)
                    || prior.cart.Where((line, slot) => (line.committedLot == null)
                        != (next.cart[slot].committedLot == null)).Any())
                    throw new InvalidOperationException("Shop JSON changed absent patience/lot into an object: " + executionJson);
            }
            if (!module.TryRestoreState(module.CurrentVersion, executionJson, out string executionFailure))
                throw new InvalidOperationException(executionFailure + "; json=" + executionJson);
            Shopping.RestorePersistentState(
                2, 0, Shopping.HoldingMoney,
                pending);
            if (pending.Length == 1)
                Physical.ExpectedOperationId = pending[0].operationId;
        }

        private void PreparePhysicalExpectation()
        {
            Physical.ExpectedOperationId = PreviewOperationId();
        }

        internal bool IsSettled(int physical, int outcomes, int revenue) =>
            Shopping.CapturePendingRetailPurchases().Count == 0
            && Shop.GetStockCount() == 0
            && Shopping.HoldingMoney == StartingMoney - Cost * outcomes
            && Physical.Commits == physical
            && !PhysicalInstanceExists
            && Outcomes.CommitCount(OutcomeKind) == outcomes
            && Money.Credits == revenue
            && Money.Balance == initialRevenue + revenue;

        public void Dispose() { Facts.Dispose(); world.Dispose(); }

        private static void SetPort(object target, string name, object port)
        {
            FieldInfo field = target.GetType().GetField(name, InstancePrivate)
                ?? throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, port);
        }
    }

    private sealed class PhysicalSinkProbe : IRetailStockPhysicalRuntime
    {
        private readonly IRetailStockPhysicalRuntime inner;
        private readonly HashSet<string> committed = new(StringComparer.Ordinal);
        internal PhysicalSinkProbe(IRetailStockPhysicalRuntime inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        internal bool FailNext { get; set; }
        internal string ExpectedOperationId { get; set; }
        internal int Attempts { get; private set; }
        internal int Commits => committed.Count;
        internal List<string> CommittedOperations { get; } = new();

        public bool TryCommitExternalSink(
            RetailStockLotSnapshot lot, out string failureReason)
        {
            Attempts++;
            if (FailNext)
            {
                FailNext = false;
                failureReason = "b0-t04-injected-physical-failure";
                return false;
            }
            // The external port receives the exact one-unit lot, while the
            // operation ID is held by the purchase owner. The fixture sets its
            // expectation from Shop's production preview before each attempt.
            string operation = ExpectedOperationId;
            if (string.IsNullOrWhiteSpace(operation)
                || !operation.StartsWith(
                    "retail-unit:" + lot.sourceOperationId + ":ordinal:",
                    StringComparison.Ordinal))
            {
                failureReason = "b0-t04-operation-expectation-mismatch";
                return false;
            }
            if (!inner.TryCommitExternalSink(lot, out failureReason))
                return false;
            if (committed.Add(operation)) CommittedOperations.Add(operation);
            return true;
        }

        public bool TryCreateAuthoredUniqueLot(int saleItemId,
            string itemDefinitionId, long unitMassGrams,
            string sourceOperationId, out RetailStockLotSnapshot lot,
            out string failureReason) => inner.TryCreateAuthoredUniqueLot(
                saleItemId, itemDefinitionId, unitMassGrams,
                sourceOperationId, out lot, out failureReason);
        public bool TryPrepareExistingUniqueLot(RetailStockLotSnapshot lot,
            out string failureReason) =>
            inner.TryPrepareExistingUniqueLot(lot, out failureReason);
        public bool TryBindExistingUniqueLot(RetailStockLotSnapshot lot,
            out string failureReason) =>
            inner.TryBindExistingUniqueLot(lot, out failureReason);
        public bool TryRestoreBoundUniqueLot(RetailStockLotSnapshot lot,
            CombatEquipmentWorldState state, out string failureReason) =>
            inner.TryRestoreBoundUniqueLot(lot, state, out failureReason);
    }

    private sealed class MoneyAccountProbe : IIdempotentGameMoneyAccount
    {
        private readonly HashSet<string> credited = new(StringComparer.Ordinal);
        internal MoneyAccountProbe(int balance) => Balance = balance;
        internal bool FailNextCredit { get; set; }
        internal int Credits { get; private set; }
        internal int CreditAttempts { get; private set; }
        internal List<string> CreditOperations { get; } = new();
        public int Balance { get; private set; }
        public bool CanSpend(int amount) => Balance >= amount;
        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);
        public bool TrySpend(int amount, EconomyTransactionContext context,
            out string reason)
        {
            reason = CanSpend(amount) ? string.Empty : "insufficient funds";
            if (reason.Length != 0) return false;
            Balance -= amount;
            return true;
        }
        public bool TrySpendOnce(int amount, EconomyTransactionContext context,
            out EconomyTransactionRecord receipt, out string reason)
        {
            bool spent = TrySpend(amount, context, out reason);
            receipt = spent ? new EconomyTransactionRecord() : null;
            return spent;
        }
        public bool TryCreditOnce(int amount, EconomyTransactionContext context,
            out string reason)
        {
            CreditAttempts++;
            if (FailNextCredit)
            {
                FailNextCredit = false;
                reason = "b0-t04-injected-revenue-failure";
                return false;
            }
            reason = string.Empty;
            if (credited.Add(context.sourceId))
            {
                Balance += amount;
                Credits += amount;
                CreditOperations.Add(context.sourceId);
            }
            return true;
        }
        public void Add(int amount) => Balance += amount;
        public void Add(int amount, EconomyTransactionContext context) => Add(amount);
        public void SetBalance(int amount, EconomyTransactionContext context) =>
            Balance = amount;
    }
}

public static class RetailPurchaseT03DebugScenarios
{
    public static void RunOrThrow() =>
        RetailPurchaseRecoveryB0T04DebugScenarios.RunT03OrThrow();

    public static void RunFromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_T03_RETAIL_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_T03_RETAIL_REPORT_PATH is required.");
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "T03 retail report must be a create-only path under Artifacts/QA.");
        }

        RunOrThrow();
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        using FileStream stream = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(
            "U0_T03_RETAIL=PASS\n"
            + "lots=unique,fungible\n"
            + "typedReceipt=buyer,facility,quantity,mass,price,balance-before-after,exact-lot\n"
            + "rejection=diagnostic-preserved,money-unchanged,stock-unchanged,parent-zero\n"
            + "deliveryRecovery=saved-outbox,retry-once,no-second-debit,no-second-revenue\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_T03_RETAIL=PASS; report=" + fullPath);
    }
}
#endif
