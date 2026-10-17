#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FacilityWishlistProcurementCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:facility-wishlist-procurement";
    internal const string BudgetBlockedActionId =
        "action:auto-procurement-budget-blocked";
    internal const string PurchaseWishlistActionId =
        "action:auto-procure-wishlist-facility";

    private const string OwnerKind =
        "owner-kind:facility-wishlist-procurement";
    private const string DomainStateSchema =
        "dungeonstory-causal-facility-wishlist-procurement-owner-state@1";
    private const string WishlistRuleId =
        "wishlist:causal-defense-facility";
    private const int BlockedDay = 5;
    private const int PurchaseDay = 6;
    private const int InitialMoney = 1000;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
        {
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        }
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A facility wishlist checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                BudgetBlockedActionId,
                context,
                context.ProcessBlockedBudget,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.AutoProcurementResult,
                    expectedSequence: 1,
                    expectedDay: BlockedDay,
                    GameplayOutcomeStatus.Failed),
                lifetime),
            Fixture(
                PurchaseWishlistActionId,
                context,
                context.PurchaseWishlistFacility,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.AutoProcurementResult,
                    expectedSequence: 3,
                    expectedDay: PurchaseDay,
                    GameplayOutcomeStatus.Succeeded),
                lifetime,
                companionKind:
                    MigratedProducerOutcomeKind.FacilityShopPurchaseResult,
                requireCompanion: () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.FacilityShopPurchaseResult,
                    expectedSequence: 2,
                    expectedDay: PurchaseDay,
                    GameplayOutcomeStatus.Succeeded))
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue =
            "owner:causal-facility-wishlist-procurement:focused";
        JObject blockedCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-facility-wishlist:block",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture blocked = fixtures.Get(
                BudgetBlockedActionId);
            Require(
                (int)blocked.CaptureState()["facilityProcurement.stage"] == 0,
                "Facility wishlist owner did not start empty.");
            Require(blocked.Execute(),
                "The real zero-budget procurement pass was rejected.");
            GameplayOutcomeReplayIdentity outcome =
                blocked.RequireExactOutcome();
            Require(outcome.HasCanonicalPayloadHash,
                "The blocked procurement result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = blocked.CaptureState();
            Require(
                (int)after["facilityProcurement.stage"] == 1
                && (int)after["facilityProcurement.money"] == InitialMoney
                && (int)after["facilityProcurement.lastProcessedDay"]
                    == BlockedDay
                && (int)after["facilityProcurement.resultCount"] == 1
                && (int)after["facilityProcurement.purchasedResultCount"] == 0
                && (int)after["facilityProcurement.exactOutcomeCount"] == 1,
                "Blocked procurement did not preserve its failed result exactly.");
            blockedCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)blocked)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-facility-wishlist:purchase",
                   OwnerIdValue,
                   blockedCheckpoint)))
        {
            IProductionCausalActionFixture blocked = fixtures.Get(
                BudgetBlockedActionId);
            IProductionCausalActionFixture purchase = fixtures.Get(
                PurchaseWishlistActionId);
            Require(!blocked.Execute(),
                "Fresh composition replayed the blocked procurement day.");
            Require(purchase.Execute(),
                "The actual wishlist facility procurement was rejected.");
            Require(purchase.RequireExactOutcome().HasCanonicalPayloadHash,
                "The successful auto-procurement result has no canonical hash.");
            IReadOnlyList<GameplayOutcomeReplayIdentity> companions =
                purchase.RequireExactCompanionOutcomes();
            Require(
                companions.Count == 1
                && companions[0].HasCanonicalPayloadHash,
                "Wishlist procurement lost the exact facility-purchase companion.");
            IReadOnlyDictionary<string, object> after = purchase.CaptureState();
            Require(
                (int)after["facilityProcurement.stage"] == 2
                && (int)after["facilityProcurement.money"] < InitialMoney
                && (int)after["facilityProcurement.lastProcessedDay"]
                    == PurchaseDay
                && (int)after["facilityProcurement.resultCount"] == 1
                && (int)after["facilityProcurement.purchasedResultCount"] == 1
                && (int)after["facilityProcurement.buildingUnlockedCount"] == 1
                && (int)after["facilityProcurement.installationKitCount"] == 1
                && (int)after["facilityProcurement.autoOutcomeCount"] == 2
                && (int)after["facilityProcurement.facilityOutcomeCount"] == 1
                && (int)after["facilityProcurement.exactOutcomeCount"] == 3,
                "Wishlist procurement did not retain money, unlock, kit and both outcomes.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)purchase)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-facility-wishlist:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture blocked = fixtures.Get(
                BudgetBlockedActionId);
            IProductionCausalActionFixture purchase = fixtures.Get(
                PurchaseWishlistActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)blocked)
                .CaptureOwnerCheckpoint();
            Require(!blocked.Execute() && !purchase.Execute(),
                "Terminal facility wishlist owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)blocked)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected facility wishlist replay mutated its checkpoint.");
        }

        JObject dayTamper = (JObject)blockedCheckpoint.DeepClone();
        ((JObject)dayTamper["domainState"])["lastProcessedDay"] = 0;
        Require(RejectCheckpoint(OwnerIdValue, dayTamper, "day-tamper"),
            "A blocked procurement checkpoint with a false day was accepted.");

        JObject unlockTamper = (JObject)terminalCheckpoint.DeepClone();
        ((JArray)((JObject)unlockTamper["domainState"])
            ["researchUnlockedBuildingIds"]).Clear();
        Require(RejectCheckpoint(
                OwnerIdValue,
                unlockTamper,
                "unlock-tamper"),
            "A purchased facility without its research unlock was accepted.");

        JObject ledgerTamper = (JObject)terminalCheckpoint.DeepClone();
        ((JArray)((JObject)ledgerTamper["outcomeLedger"])["exactOutcomes"])
            .RemoveAt(1);
        Require(RejectCheckpoint(
                OwnerIdValue,
                ledgerTamper,
                "ledger-tamper"),
            "A wishlist purchase without its exact facility result was accepted.");

        if (log)
        {
            Debug.Log(
                "Blocked auto-procurement -> restored wishlist facility purchase with physical kit causal owner family PASS");
        }
        return true;
    }

    public static bool RunCausalOwnerFamilyFocused(bool log = false) =>
        RunCausalOwnerFamily(log);

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Facility wishlist procurement owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunOrThrow()
    {
        RunScenarios();
    }

    private static JObject RunScenarios(JArray lifecycle = null)
    {
        JObject spendRejection = VerifyRejectedSpendRoundTrip();
        CaptureLifecycle(lifecycle, "spend-rejection-round-trip", spendRejection);
        const string deliveryOwner = "owner:facility-fulfillment-t02-t06:delivery";
        JObject pendingDelivery;
        using (var context = new Context("run:t02-t06:delivery", deliveryOwner, null))
        {
            Require(context.ProcessBlockedBudget(), "The budget baseline failed.");
            Require(context.PurchaseWishlistWithBlockedDelivery(),
                "A committed child purchase was not retained as pending delivery.");
            pendingDelivery = context.CaptureOwnerCheckpoint();
            CaptureLifecycle(lifecycle, "kit-delivery-pending", pendingDelivery);
        }
        JObject delivered;
        using (var context = new Context("run:t02-t06:retry-delivery", deliveryOwner,
                   pendingDelivery))
        {
            CaptureLifecycle(lifecycle, "kit-restored-before-delivery",
                context.CaptureOwnerCheckpoint);
            Require(context.CompletePendingDelivery(),
                "Restored fulfillment did not deliver and commit its parent once.");
            delivered = context.CaptureOwnerCheckpoint();
            CaptureLifecycle(lifecycle, "kit-delivered", delivered);
        }
        using (var context = new Context("run:t02-t06:terminal", deliveryOwner,
                   delivered))
        {
            CaptureLifecycle(lifecycle, "kit-restored-terminal-before-retry",
                context.CaptureOwnerCheckpoint);
            Require(context.RetryTerminalWithoutMutation(),
                "Terminal fulfillment or parent replay changed a durable owner.");
            CaptureLifecycle(lifecycle, "kit-restored-terminal-after-noop",
                context.CaptureOwnerCheckpoint);
        }

        const string parentOwner = "owner:facility-fulfillment-t06:parent";
        JObject pendingParent;
        using (var context = new Context("run:t06:parent-fault", parentOwner, null))
        {
            Require(context.ProcessBlockedBudget(), "The parent fault baseline failed.");
            Require(context.PurchaseWishlistWithParentCommitRejected(),
                "A failed parent commit lost or duplicated its committed child.");
            pendingParent = context.CaptureOwnerCheckpoint();
            CaptureLifecycle(lifecycle, "parent-pending", pendingParent);
        }
        using (var context = new Context("run:t06:retry-parent", parentOwner,
                   pendingParent))
        {
            CaptureLifecycle(lifecycle, "parent-restored-before-retry",
                context.CaptureOwnerCheckpoint);
            Require(context.CompletePendingParent(),
                "Restored parent did not commit without a second child purchase.");
            CaptureLifecycle(lifecycle, "parent-committed",
                context.CaptureOwnerCheckpoint);
            Require(context.RetryTerminalWithoutMutation(),
                "Parent replay changed a terminal owner.");
            CaptureLifecycle(lifecycle, "parent-after-noop",
                context.CaptureOwnerCheckpoint);
        }

        VerifyBlueprintFulfillmentRoundTrip(lifecycle);
        VerifyPublishedBlueprintFulfillmentRoundTrip(lifecycle);
        return spendRejection;
    }

    private static void CaptureLifecycle(
        JArray lifecycle, string stage, Func<JObject> capture)
    {
        if (lifecycle != null)
            CaptureLifecycle(lifecycle, stage, capture());
    }

    private static void CaptureLifecycle(
        JArray lifecycle, string stage, JObject snapshot,
        string scope = "controlled-production-owner-checkpoint")
    {
        lifecycle?.Add(new JObject
        {
            ["stage"] = stage,
            ["scope"] = scope,
            ["snapshot"] = snapshot.DeepClone()
        });
    }

    public static void RunT02FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_T02_FULFILLMENT_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_T02_FULFILLMENT_REPORT_PATH is required.");
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string observationsPath = fullPath + ".observations.json";
        string lifecyclePath = fullPath + ".lifecycle.json";
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath)
            || File.Exists(observationsPath)
            || File.Exists(lifecyclePath))
        {
            throw new InvalidOperationException(
                "T02 fulfillment report must be a create-only path under Artifacts/QA.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        var lifecycle = new JArray();
        Exception scenarioFailure = null;
        JObject spendRejection;
        try
        {
            spendRejection = RunScenarios(lifecycle);
        }
        catch (Exception exception)
        {
            scenarioFailure = exception;
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            using FileStream lifecycleStream = new(
                lifecyclePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter lifecycleWriter = new(lifecycleStream);
            lifecycleWriter.Write(new JObject
            {
                ["schema"] = "dungeonstory-t02-t06-lifecycle-observations@1",
                ["status"] = scenarioFailure == null ? "SCENARIOS_PASSED" : "FAIL",
                ["failure"] = scenarioFailure == null
                    ? JValue.CreateNull() : new JValue(scenarioFailure.ToString()),
                ["stages"] = lifecycle,
                ["controlledFixture"] = true,
                ["wholeWorldRestoreClaimed"] = false,
                ["coldProcessRestoreClaimed"] = false,
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            }.ToString(Formatting.Indented));
        }
        using (FileStream observations = new(
                   observationsPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (StreamWriter observationWriter = new(observations))
            observationWriter.Write(spendRejection.ToString(Formatting.Indented));
        using FileStream stream = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(
            "U0_T02_FULFILLMENT=PASS\n"
            + "kit=purchase-committed,unlock-kept,delivery-pending,restore,retry-once\n"
            + "blueprint=payment-and-acquisition-kept,delivery-pending,json-restore,spawn-once\n"
            + "blueprintPublisher=actual-daily-shop,payment-transaction,canonical-result-key,ledger-parent-kept\n"
            + "terminal=physical-and-fulfillment-restored,retry-zero,duplicate-spawn-zero\n"
            + "parent=child-committed,parent-pending,restore,parent-once\n"
            + "spend-rejection=affordable-offer,attempt-once,failed-parent-once,child-zero,json-restore,retry-zero\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_T02_FULFILLMENT=PASS; report=" + fullPath);
    }

    private static JObject VerifyRejectedSpendRoundTrip()
    {
        const string owner = "owner:facility-fulfillment-t06:spend-rejected";
        JObject rejected;
        using (var context = new Context("run:t06:spend-rejected", owner, null))
        {
            Require(context.ProcessBlockedBudget(), "The spend rejection baseline failed.");
            Require(context.PurchaseWishlistWithSpendRejected(),
                "An affordable purchase did not preserve the actual rejected spend.");
            rejected = context.CaptureRejectedSpendEvidence();
        }

        // The rejection control is deliberately absent from this fresh composition.
        JObject checkpoint = JObject.Parse(
            rejected["checkpoint"].ToString(Formatting.None));
        using var restored = new Context("run:t06:restore-spend-rejected", owner, checkpoint);
        JObject before = restored.CaptureOwnerCheckpoint();
        ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(checkpoint, before);
        Require(restored.RetryRejectedSpendWithoutMutation(),
            "A restored rejected spend retried payment or changed its durable owner.");
        return new JObject
        {
            ["schema"] = "dungeonstory-t06-spend-rejection-observations@1",
            ["status"] = "PASS",
            ["fixture"] = "funded-money-port-rejects-one-actual-TrySpend",
            ["rejected"] = rejected,
            ["restoredBeforeRetry"] = before,
            ["restoredAfterRetry"] = restored.CaptureOwnerCheckpoint(),
            ["restoredSpendAttempts"] = restored.SpendAttempts,
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
    }

    private static void VerifyBlueprintFulfillmentRoundTrip(JArray lifecycle = null)
    {
        FacilityBlueprintSO blueprint = AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>(
            "Assets/Resources/SO/Blueprint/P1/BP_CommercialBasics.asset")
            ?? throw new InvalidOperationException(
                "The authored T02 blueprint fixture is missing.");
        var unlocks = new FacilityShopUnlockState();
        var game = new GameSessionState(500, PurchaseDay);
        string resultKey;
        DungeonResearchSaveData pendingResearch;
        DungeonPhysicalItemSaveData pendingPhysical;
        using (var source = new BlueprintFulfillmentFixture(
                   blueprint,
                   unlocks,
                   deliveryAvailable: false))
        {
            var offer = new FacilityBlueprintOffer(
                blueprint,
                100,
                blueprint.rarity,
                randomOffer: true);
            FacilityShopPurchaseResult committed = default;
            bool purchased = FacilityShopService.TryPurchaseOffer(
                new EditorGameMoneyAccount(game),
                offer,
                unlocks,
                new EconomyTransactionContext(
                    EconomyTransactionKind.ShopPurchase,
                    "t02-blueprint",
                    "blueprint:" + blueprint.id,
                    "T02 blueprint fulfillment"),
                DisabledDungeonDebugRuleQuery.Instance,
                out FacilityShopPurchaseResult serviceResult,
                purchase =>
                {
                    MigratedProducerOutcomeDefinition definition =
                        MigratedProducerOutcomeCatalog.Get(
                            MigratedProducerOutcomeKind.FacilityShopPurchaseResult);
                    GameplayResultKey committedKey = new(
                        definition.ProducerId,
                        new GameplayOperationId(
                            "facility-shop-purchase:t02-blueprint"),
                        1L,
                        0);
                    committed = new FacilityShopPurchaseResult(
                        true,
                        purchase.purchasedOffer,
                        purchase.cost,
                        purchase.message,
                        committedKey.ToString(),
                        "economy-transaction:t02-blueprint",
                        FacilityShopPurchaseFailureStage.None,
                        FacilityShopPurchaseOutcomePolicy
                            .DomainApplied,
                        purchase.balanceBefore,
                        purchase.balanceAfter);
                    Require(source.Runtime.TryAcceptPurchase(
                            FacilityShopFactEditorFixture.FromResult(committed),
                            committed.purchasePublicationKey,
                            out string acceptanceFailure),
                        "The controlled blueprint purchase was not accepted: "
                        + acceptanceFailure);
                });
            resultKey = committed.purchasePublicationKey;
            Require(
                purchased
                && serviceResult.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .DurableFactRequired
                && FacilityShopFactEditorFixture.ReadKey(committed).IsValid
                && committed.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .DomainApplied
                && unlocks.IsBlueprintAcquired(blueprint)
                && game.holdingMoney.Value == 400
                && source.ItemQuantity == 0
                && source.TryGetFulfillment(resultKey, out var pending)
                && pending.Phase == FacilityShopFulfillmentPhase.Pending,
                "A committed blueprint purchase did not remain paid/acquired while delivery was pending.");
            pendingResearch = RoundTrip(CaptureFulfillments(source.Runtime));
            pendingPhysical = RoundTrip(source.Items.Capture());
            CaptureBlueprintProtocolLifecycle(lifecycle, "blueprint-protocol-pending",
                pendingResearch, pendingPhysical);
        }

        DungeonResearchSaveData terminalResearch;
        DungeonPhysicalItemSaveData terminalPhysical;
        using (var restored = new BlueprintFulfillmentFixture(
                   blueprint,
                   unlocks,
                   deliveryAvailable: true,
                   research: pendingResearch,
                   physical: pendingPhysical))
        {
            Require(
                restored.Runtime.RetryPendingShopFulfillments() == 1
                && restored.ItemQuantity == 1
                && restored.TryGetFulfillment(resultKey, out var delivered)
                && delivered.Phase == FacilityShopFulfillmentPhase.Delivered
                && !string.IsNullOrWhiteSpace(delivered.ItemInstanceId),
                "The restored blueprint fulfillment did not spawn and deliver exactly once.");
            terminalResearch = RoundTrip(CaptureFulfillments(restored.Runtime));
            terminalPhysical = RoundTrip(restored.Items.Capture());
            CaptureBlueprintProtocolLifecycle(lifecycle, "blueprint-protocol-delivered",
                terminalResearch, terminalPhysical);
        }

        using var terminal = new BlueprintFulfillmentFixture(
            blueprint,
            unlocks,
            deliveryAvailable: true,
            research: terminalResearch,
            physical: terminalPhysical);
        Require(
            terminal.ItemQuantity == 1
            && terminal.Runtime.RetryPendingShopFulfillments() == 0
            && terminal.ItemQuantity == 1
            && terminal.TryGetFulfillment(resultKey, out var final)
            && final.Phase == FacilityShopFulfillmentPhase.Delivered,
            "A terminal blueprint fulfillment restore spawned a duplicate item.");
        CaptureBlueprintProtocolLifecycle(lifecycle, "blueprint-protocol-terminal",
            CaptureFulfillments(terminal.Runtime), terminal.Items.Capture());
    }

    private static void CaptureBlueprintProtocolLifecycle(
        JArray lifecycle, string stage, DungeonResearchSaveData research,
        DungeonPhysicalItemSaveData physical)
    {
        CaptureLifecycle(lifecycle, stage, new JObject
        {
            ["research"] = JObject.Parse(JsonUtility.ToJson(research)),
            ["physicalItems"] = JObject.Parse(JsonUtility.ToJson(physical))
        }, "controlled-committed-protocol-key-not-a-real-ledger-parent");
    }

    private static void VerifyPublishedBlueprintFulfillmentRoundTrip(JArray lifecycle)
    {
        FacilityBlueprintSO blueprint = AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>(
            "Assets/Resources/SO/Blueprint/P1/BP_CommercialBasics.asset")
            ?? throw new InvalidOperationException("The authored T02 blueprint is missing.");
        const int StartingMoney = 100000;
        var root = new DungeonRuntimeAggregateRootStore();
        var outcomes = new MigratedProducerOutcomeEditorFixture(
            "run:t02:actual-blueprint-publication", root);
        var probe = new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction, outcomes.Recorder);
        var game = new GameSessionState(StartingMoney, PurchaseDay);
        var session = new SessionStore(game);
        var treasury = new TreasuryEconomyAggregateStateStore(root);
        var payments = new EconomyTransactionLedgerRuntime(new UnityGameClock(), treasury);
        var events = new GameEventBus();
        var host = new GameObject("T02 Actual Blueprint Purchase Publisher");
        try
        {
            var shop = host.AddComponent<DailyFacilityShopRuntime>();
            shop.ConstructDailyFacilityShopRuntime(
                new BlueprintOnlyShopCatalog(blueprint),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IRunVariableRuntimeReader>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IMetaProgressionRuntimeReader>(),
                events,
                new EditorGameMoneyAccount(game, payments),
                autoProcurement: null,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                root,
                DisabledDungeonDebugRuleQuery.Instance);
            using var source = new BlueprintFulfillmentFixture(
                blueprint, shop.UnlockState, deliveryAvailable: false,
                aggregateRoot: root, events: events);
            using var purchaseFacts = new FacilityShopFactEditorFixture(
                outcomes,
                probe,
                source.Runtime);
            shop.ConstructPurchaseFacts(
                purchaseFacts.Journal,
                session,
                treasury);
            shop.Refresh(PurchaseDay, FacilityShopRefreshCause.Initialization);
            int offerIndex = Enumerable.Range(0, shop.CurrentDailyOffers.Count)
                .Where(index => shop.CurrentDailyOffers[index] is FacilityBlueprintOffer offer
                    && offer.Blueprint == blueprint)
                .DefaultIfEmpty(-1).Single();
            Require(offerIndex >= 0, "The real daily shop has no authored blueprint offer.");
            bool purchased = shop.TryPurchaseDailyOffer(offerIndex, game,
                out FacilityShopPurchaseResult purchase);
            CapturePublishedBlueprintLifecycle(lifecycle, "blueprint-purchase-attempt",
                purchase, game, shop, payments, outcomes, source);
            Require(purchased && purchase.success
                && purchase.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .DomainApplied
                && purchase.failureStage == FacilityShopPurchaseFailureStage.None
                && FacilityShopFactEditorFixture.ReadKey(purchase).IsValid
                && !string.IsNullOrWhiteSpace(purchase.paymentTransactionId)
                && purchase.cost > 0
                && purchase.balanceBefore == StartingMoney
                && purchase.balanceAfter == game.holdingMoney.Value
                && game.holdingMoney.Value == StartingMoney - purchase.cost
                && payments.Records.Count == 1
                && payments.Records[0].transactionId == purchase.paymentTransactionId
                && shop.UnlockState.PurchaseSequence == 1
                && shop.UnlockState.IsBlueprintAcquired(blueprint),
                "The real blueprint publisher lost its single payment/acquisition identity.");
            GameplayOutcomeReplayIdentity identity = MigratedProducerOutcomeE2EAssertions
                .RequireExact(outcomes, MigratedProducerOutcomeKind.FacilityShopPurchaseResult,
                    probe, "money=" + StartingMoney, "money=" + game.holdingMoney.Value);
            Require(identity.ResultKey.Equals(
                    FacilityShopFactEditorFixture.ReadKey(purchase))
                && identity.HasCanonicalPayloadHash
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && outcomes.Ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
                && exact.status == GameplayOutcomeStatus.Succeeded
                && exact.operationId == FacilityShopFactEditorFixture
                    .ReadKey(purchase).OperationId.Value
                && outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1
                && outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count == 0
                && exact.facts.Any(value => value.factId
                    == "facility-shop-payment-transaction-id"
                    && value.value == purchase.paymentTransactionId)
                && exact.facts.Any(value => value.factId
                    == "facility-shop-item-definition-id"
                    && value.value == blueprint.PhysicalItemId),
                "The blueprint fulfillment has no real canonical purchase/payment parent.");
            string resultKey = purchase.purchasePublicationKey;
            Require(source.ItemQuantity == 0
                && source.Runtime.State.CaptureShopFulfillments().Count == 1
                && source.TryGetFulfillment(resultKey, out var pending)
                && pending.Phase == FacilityShopFulfillmentPhase.Pending
                && pending.OfferDataId == blueprint.id
                && pending.ItemDefinitionId == blueprint.PhysicalItemId,
                "The real purchase bus did not retain its exact pending blueprint fulfillment.");
            CapturePublishedBlueprintLifecycle(lifecycle, "blueprint-published-pending",
                purchase, game, shop, payments, outcomes, source);
            DungeonResearchSaveData pendingResearch = RoundTrip(CaptureFulfillments(source.Runtime));
            DungeonPhysicalItemSaveData pendingPhysical = RoundTrip(source.Items.Capture());
            JObject authorityBefore = CaptureBlueprintPurchaseAuthority(game, shop, payments, outcomes);
            DungeonResearchSaveData deliveredResearch;
            DungeonPhysicalItemSaveData deliveredPhysical;
            using (var restored = new BlueprintFulfillmentFixture(
                       blueprint, shop.UnlockState, deliveryAvailable: true,
                       research: pendingResearch, physical: pendingPhysical))
            {
                CapturePublishedBlueprintLifecycle(lifecycle, "blueprint-published-restored-pending",
                    purchase, game, shop, payments, outcomes, restored);
                Require(restored.ItemQuantity == 0
                    && restored.TryGetFulfillment(resultKey, out var restoredPending)
                    && restoredPending.Phase == FacilityShopFulfillmentPhase.Pending,
                    "Fresh blueprint fulfillment did not restore the real pending purchase key.");
                Require(restored.Runtime.RetryPendingShopFulfillments() == 1
                    && restored.ItemQuantity == 1
                    && restored.TryGetFulfillment(resultKey, out var delivered)
                    && delivered.Phase == FacilityShopFulfillmentPhase.Delivered
                    && !string.IsNullOrWhiteSpace(delivered.StackId)
                    && !string.IsNullOrWhiteSpace(delivered.ItemInstanceId),
                    "Restored real-publisher blueprint fulfillment did not deliver exactly once.");
                Require(JToken.DeepEquals(authorityBefore,
                        CaptureBlueprintPurchaseAuthority(game, shop, payments, outcomes)),
                    "Blueprint delivery changed or repeated the retained purchase/payment parent.");
                CapturePublishedBlueprintLifecycle(lifecycle, "blueprint-published-delivered",
                    purchase, game, shop, payments, outcomes, restored);
                deliveredResearch = RoundTrip(CaptureFulfillments(restored.Runtime));
                deliveredPhysical = RoundTrip(restored.Items.Capture());
            }
            using var terminal = new BlueprintFulfillmentFixture(
                blueprint, shop.UnlockState, deliveryAvailable: true,
                research: deliveredResearch, physical: deliveredPhysical);
            JObject terminalBefore = new()
            {
                ["research"] = JObject.Parse(JsonUtility.ToJson(CaptureFulfillments(terminal.Runtime))),
                ["physicalItems"] = JObject.Parse(JsonUtility.ToJson(terminal.Items.Capture()))
            };
            CapturePublishedBlueprintLifecycle(lifecycle, "blueprint-published-restored-terminal-before-retry",
                purchase, game, shop, payments, outcomes, terminal);
            Require(terminal.ItemQuantity == 1
                && terminal.Runtime.RetryPendingShopFulfillments() == 0
                && terminal.ItemQuantity == 1
                && terminal.TryGetFulfillment(resultKey, out var final)
                && final.Phase == FacilityShopFulfillmentPhase.Delivered
                && final.StackId == deliveredResearch.shopFulfillments.Single().stackId
                && final.ItemInstanceId == deliveredResearch.shopFulfillments.Single().itemInstanceId,
                "Terminal real-publisher fulfillment lost identity or spawned another blueprint.");
            Require(JToken.DeepEquals(terminalBefore, new JObject
                {
                    ["research"] = JObject.Parse(JsonUtility.ToJson(CaptureFulfillments(terminal.Runtime))),
                    ["physicalItems"] = JObject.Parse(JsonUtility.ToJson(terminal.Items.Capture()))
                })
                && JToken.DeepEquals(authorityBefore,
                    CaptureBlueprintPurchaseAuthority(game, shop, payments, outcomes)),
                "Terminal blueprint no-op changed a physical, fulfillment or monetary owner.");
            CapturePublishedBlueprintLifecycle(lifecycle, "blueprint-published-restored-terminal-after-noop",
                purchase, game, shop, payments, outcomes, terminal);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static JObject CaptureBlueprintPurchaseAuthority(
        GameSessionState game, DailyFacilityShopRuntime shop,
        EconomyTransactionLedgerRuntime payments, MigratedProducerOutcomeEditorFixture outcomes) =>
        new()
        {
            ["session"] = JObject.Parse(JsonUtility.ToJson(GameSessionSaveData.From(game.Capture()))),
            ["facilityShop"] = CaptureFacilityShop(shop.CaptureState()),
            ["purchaseSequence"] = shop.UnlockState.PurchaseSequence,
            ["paymentLedger"] = JObject.Parse(JsonUtility.ToJson(payments.Capture())),
            ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(outcomes.Transaction.Capture())),
            ["ledger"] = JObject.Parse(JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()))
        };

    private static void CapturePublishedBlueprintLifecycle(
        JArray lifecycle, string stage, FacilityShopPurchaseResult purchase,
        GameSessionState game, DailyFacilityShopRuntime shop,
        EconomyTransactionLedgerRuntime payments, MigratedProducerOutcomeEditorFixture outcomes,
        BlueprintFulfillmentFixture fulfillment)
    {
        if (lifecycle == null)
            return;
        CaptureLifecycle(lifecycle, stage, new JObject
        {
            ["purchase"] = new JObject
            {
                ["success"] = purchase.success,
                ["cost"] = purchase.cost,
                ["balanceBefore"] = purchase.balanceBefore,
                ["balanceAfter"] = purchase.balanceAfter,
                ["paymentTransactionId"] = purchase.paymentTransactionId,
                ["purchaseResultKey"] = purchase.purchasePublicationKey,
                ["failureStage"] = purchase.failureStage.ToString(),
                ["outcomePolicy"] = purchase.outcomePolicy.ToString()
            },
            ["purchaseAuthority"] = CaptureBlueprintPurchaseAuthority(game, shop, payments, outcomes),
            ["research"] = JObject.Parse(JsonUtility.ToJson(CaptureFulfillments(fulfillment.Runtime))),
            ["physicalItems"] = JObject.Parse(JsonUtility.ToJson(fulfillment.Items.Capture())),
            ["purchaseAndMoneyOwnersRetainedDuringFulfillmentRestore"] = true
        }, "actual-daily-shop-publisher-fresh-fulfillment-owner-same-process");
    }

    private static DungeonResearchSaveData CaptureFulfillments(
        BlueprintResearchRuntime runtime) =>
        new()
        {
            shopFulfillments = runtime.State.CaptureShopFulfillments()
                .Select(value => new FacilityShopFulfillmentSaveData
                {
                    purchaseResultKey = value.PurchaseResultKey,
                    offerTypeId = value.OfferTypeId,
                    offerDataId = value.OfferDataId,
                    itemDefinitionId = value.ItemDefinitionId,
                    itemDisplayName = value.ItemDisplayName,
                    phase = value.Phase,
                    stackId = value.StackId,
                    itemInstanceId = value.ItemInstanceId
                })
                .ToList()
        };

    private static DungeonResearchSaveData RoundTrip(
        DungeonResearchSaveData value) =>
        JsonUtility.FromJson<DungeonResearchSaveData>(JsonUtility.ToJson(value));

    private static DungeonPhysicalItemSaveData RoundTrip(
        DungeonPhysicalItemSaveData value) =>
        JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(value));

    private static void RestoreFulfillments(
        BlueprintResearchRuntime runtime,
        DungeonResearchSaveData saved)
    {
        foreach (FacilityShopFulfillmentSaveData row in saved?.shopFulfillments
                     ?? new List<FacilityShopFulfillmentSaveData>())
        {
            runtime.State.RestoreShopFulfillment(new FacilityShopFulfillmentRecord
            {
                PurchaseResultKey = row.purchaseResultKey,
                OfferTypeId = row.offerTypeId,
                OfferDataId = row.offerDataId,
                ItemDefinitionId = row.itemDefinitionId,
                ItemDisplayName = row.itemDisplayName,
                Phase = row.phase,
                StackId = row.stackId,
                ItemInstanceId = row.itemInstanceId
            });
        }
    }

    private static IProductionCausalActionFixture Fixture(
        string actionId,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime,
        MigratedProducerOutcomeKind? companionKind = null,
        Func<GameplayOutcomeReplayIdentity> requireCompanion = null)
    {
        IEnumerable<MigratedProducerOutcomeKind> companions =
            companionKind.HasValue
                ? new[] { companionKind.Value }
                : Array.Empty<MigratedProducerOutcomeKind>();
        return new DelegateProductionCausalActionFixture(
            actionId,
            "economy",
            MigratedProducerOutcomeKind.AutoProcurementResult,
            context.CaptureState,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
            context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
            context.HasPersistentOwner ? context : null,
            companionOutcomeKinds: companions,
            requireExactCompanions: companionKind.HasValue
                ? () => new[] { requireCompanion() }
                : null);
    }

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-facility-wishlist:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly DomainFactDispatcher domainFacts;
        private readonly DungeonRuntimeAggregateRootStore aggregateRoot;
        private readonly SessionStore session;
        private readonly TreasuryEconomyAggregateStateStore treasury;
        private readonly GameEventBus events;
        private readonly BuildingSO building;
        private readonly SingleFacilityShopCatalog shopCatalog;
        private readonly WorldItemStackRuntime items;
        private readonly AutoProcurementRuntime procurement;
        private readonly GameObject host;
        private readonly DailyFacilityShopRuntime shop;
        private readonly RejectableShopMoneyAccount shopMoney;
        private readonly BlueprintResearchRuntime research;
        private readonly FacilityShopFactEditorFixture purchaseFacts;
        private readonly ToggleDropZone dropZone = new();
        private int facilityCost;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":facility-wishlist-procurement-ledger";
            aggregateRoot = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRoot);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            domainFacts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger);
            domainFacts.Subscribe(
                "fixture:causal-auto-procurement-completed",
                new MigratedProducerDomainFactProbeObserver<
                    AutoProcurementCompletedFact>(
                    AutoProcurementFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            session = new SessionStore(new GameSessionState(
                InitialMoney,
                BlockedDay));
            treasury = new TreasuryEconomyAggregateStateStore(aggregateRoot);
            events = new GameEventBus();
            building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset")
                ?? throw new InvalidOperationException(
                    "The authored facility wishlist building is missing.");
            shopCatalog = new SingleFacilityShopCatalog(building);
            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                new FixedGridProvider(CreateDropoffGrid()),
                out _,
                out _,
                out _,
                out _);

            procurement = new AutoProcurementRuntime(
                session,
                new AutoProcurementFinancialDependencies(
                    new EditorGameMoneyAccount(session.State),
                    CharacterAiEditorTestDependencies
                        .CreateNoEmploymentContractRuntime(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios
                        .DefaultProxy.Create<IPaidFacilityContractRuntime>()),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IRunVariableRuntimeReader>(),
                new AutoProcurementStockDependencies(
                    items,
                    CharacterAiEditorTestDependencies.AuthoredGameplay),
                treasury,
                domainFacts,
                session);

            host = new GameObject("Causal Facility Wishlist Procurement");
            shop = host.AddComponent<DailyFacilityShopRuntime>();
            shopMoney = new RejectableShopMoneyAccount(new EditorGameMoneyAccount(
                session.State,
                new EconomyTransactionLedgerRuntime(new UnityGameClock(), treasury)));
            shop.ConstructDailyFacilityShopRuntime(
                shopCatalog,
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IRunVariableRuntimeReader>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IMetaProgressionRuntimeReader>(),
                events,
                shopMoney,
                procurement,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                aggregateRoot,
                DisabledDungeonDebugRuleQuery.Instance);
            research = host.AddComponent<BlueprintResearchRuntime>();
            research.Construct(
                new FixedUnlockStateService(shop.UnlockState),
                shopCatalog,
                new FacilityCandidateCacheStore(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    frameWorkBudget: null),
                new DungeonWorkforceReplanService(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    facilityCandidateCache: null,
                    haulPlanningService: null),
                events,
                items,
                new BlueprintResearchProjectCoordinator(
                    new ResourceResearchProjectCatalog(
                        Array.Empty<ResearchProjectSO>()),
                    UnavailableResearchBlueprintArchiveQuery.Instance,
                    UnrestrictedResearchFacilityCapacityQuery.Instance),
                dropZone,
                aggregateRoot,
                DisabledDungeonDebugRuleQuery.Instance,
                new UnityUiClock());
            purchaseFacts = new FacilityShopFactEditorFixture(
                outcomes,
                probe,
                research);
            shop.ConstructPurchaseFacts(
                purchaseFacts.Journal,
                session,
                treasury);
            procurement.SetFulfillmentQuery(research);

            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                probe.ClearObservations();
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            AutoProcurementSaveData saved = procurement.Capture();
            return new Dictionary<string, object>
            {
                ["facilityProcurement.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-facility-wishlist-procurement",
                ["facilityProcurement.stage"] = Stage,
                ["facilityProcurement.absoluteDay"] = session.State.day.Value,
                ["facilityProcurement.money"] =
                    session.State.holdingMoney.Value,
                ["facilityProcurement.lastProcessedDay"] =
                    saved.lastProcessedDay,
                ["facilityProcurement.dailyBudget"] = saved.dailyBudget,
                ["facilityProcurement.minimumReserve"] = saved.minimumReserve,
                ["facilityProcurement.wishlistRuleCount"] =
                    saved.wishlistRules.Count,
                ["facilityProcurement.resultCount"] = saved.lastResults.Count,
                ["facilityProcurement.purchasedResultCount"] =
                    saved.lastResults.Count(value => value.purchased),
                ["facilityProcurement.processedOfferKeyCount"] =
                    saved.processedOfferKeys.Count,
                ["facilityProcurement.buildingId"] = building.id,
                ["facilityProcurement.facilityCost"] = facilityCost,
                ["facilityProcurement.buildingUnlockedCount"] =
                    research.State.IsBuildingUnlocked(building.id) ? 1 : 0,
                ["facilityProcurement.installationKitCount"] = KitQuantity,
                ["facilityProcurement.autoOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.AutoProcurementResult),
                ["facilityProcurement.facilityOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.FacilityShopPurchaseResult),
                ["facilityProcurement.migratedOwnerCount"] =
                    outcomes.Transaction.Capture().owners.Count,
                ["facilityProcurement.exactOutcomeCount"] =
                    ExactOutcomeCount()
            };
        }

        internal bool ProcessBlockedBudget()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            procurement.ConfigureBudget(0, 0);
            procurement.ProcessShopRefresh(
                BlockedDay,
                Array.Empty<FacilityShopOffer>(),
                shopRuntime: null);
            bool valid = Stage == 1;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool PurchaseWishlistFacility()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            ConfigureWishlistPurchase();
            int beforeMoney = session.State.holdingMoney.Value;
            shop.Refresh(PurchaseDay, FacilityShopRefreshCause.Initialization);
            facilityCost = beforeMoney - session.State.holdingMoney.Value;
            bool valid = Stage == 2;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool PurchaseWishlistWithBlockedDelivery()
        {
            if (Stage != 1)
                return false;
            dropZone.DeliveryAvailable = false;
            ConfigureWishlistPurchase();
            int beforeMoney = session.State.holdingMoney.Value;
            shop.Refresh(PurchaseDay, FacilityShopRefreshCause.Initialization);
            facilityCost = beforeMoney - session.State.holdingMoney.Value;
            ValidateState();
            return Stage == 3;
        }

        internal int SpendAttempts => shopMoney.SpendAttempts;

        internal bool PurchaseWishlistWithSpendRejected()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            ConfigureWishlistPurchase();
            shopMoney.RejectNextSpend = true;
            shop.Refresh(PurchaseDay, FacilityShopRefreshCause.Initialization);
            ValidateState();
            return Stage == 5 && shopMoney.SpendAttempts == 1
                && shopMoney.RejectedSpends == 1 && shopMoney.LastRequestedAmount > 0;
        }

        internal JObject CaptureRejectedSpendEvidence()
        {
            GameplayOutcomeReplayIdentity identity = RequireOutcome(
                MigratedProducerOutcomeKind.AutoProcurementResult,
                expectedSequence: 2, expectedDay: PurchaseDay, GameplayOutcomeStatus.Failed);
            Require(outcomes.Ledger.TryGetExact(identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact.metrics.Single(value => value.metricId
                    == MigratedProducerOutcomeIds.QuantityMetric.Value).value == 1
                && exact.metrics.Single(value => value.metricId
                    == MigratedProducerOutcomeIds.AmountMetric.Value).value
                    == shopMoney.LastRequestedAmount
                && exact.metrics.Single(value => value.metricId
                    == MigratedProducerOutcomeIds.CountMetric.Value).value == 0
                && exact.metrics.Single(value => value.metricId
                    == MigratedProducerOutcomeIds.ValueMetric.Value).value == 0,
                "The failed parent lost its attempted/committed typed metrics.");
            return new JObject
            {
                ["spendAttempts"] = shopMoney.SpendAttempts,
                ["rejectedSpends"] = shopMoney.RejectedSpends,
                ["requestedAmount"] = shopMoney.LastRequestedAmount,
                ["state"] = JObject.FromObject(CaptureState()),
                ["outcome"] = CaptureOutcomeEvidence(identity),
                ["checkpoint"] = CaptureOwnerCheckpoint()
            };
        }

        internal bool RetryRejectedSpendWithoutMutation()
        {
            if (Stage != 5 || shopMoney.RejectNextSpend)
                return false;
            JObject before = CaptureOwnerCheckpoint();
            if (research.RetryPendingShopFulfillments() != 0
                || procurement.RetryPendingWishlistParents() != 0)
                return false;
            shop.Refresh(PurchaseDay, FacilityShopRefreshCause.Initialization);
            return Stage == 5 && shopMoney.SpendAttempts == 0
                && JToken.DeepEquals(before, CaptureOwnerCheckpoint());
        }

        internal bool PurchaseWishlistWithParentCommitRejected()
        {
            if (Stage != 1)
                return false;
            ConfigureWishlistPurchase();
            probe.TargetKind = MigratedProducerOutcomeKind.AutoProcurementResult;
            probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            int beforeMoney = session.State.holdingMoney.Value;
            bool rejected = false;
            try { shop.Refresh(PurchaseDay, FacilityShopRefreshCause.Initialization); }
            catch (InvalidOperationException) { rejected = true; }
            finally
            {
                probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
                facilityCost = beforeMoney - session.State.holdingMoney.Value;
            }
            ValidateState();
            return rejected && Stage == 4;
        }

        internal bool CompletePendingDelivery()
        {
            if (Stage != 3)
                return false;
            dropZone.DeliveryAvailable = true;
            if (research.RetryPendingShopFulfillments() != 1
                || procurement.RetryPendingWishlistParents() != 1)
                return false;
            ValidateState();
            return Stage == 2;
        }

        internal bool CompletePendingParent()
        {
            if (Stage != 4 || procurement.RetryPendingWishlistParents() != 1)
                return false;
            ValidateState();
            return Stage == 2;
        }

        internal bool RetryTerminalWithoutMutation()
        {
            if (Stage != 2)
                return false;
            JObject before = CaptureOwnerCheckpoint();
            if (research.RetryPendingShopFulfillments() != 0
                || procurement.RetryPendingWishlistParents() != 0)
                return false;
            shop.Refresh(PurchaseDay, FacilityShopRefreshCause.Initialization);
            return Stage == 2
                && JToken.DeepEquals(before, CaptureOwnerCheckpoint());
        }

        private void ConfigureWishlistPurchase()
        {
            session.State.day.Initialize(PurchaseDay);
            procurement.ConfigureBudget(InitialMoney, 0);
            procurement.UpsertWishlistRule(new ProcurementWishlistRule
            {
                ruleId = WishlistRuleId,
                offerTypeId = FacilityShopOfferTypeIds.Building,
                dataId = building.id,
                maximumPrice = InitialMoney,
                maximumOwned = 1,
                priority = 100,
                enabled = true
            });
        }

        internal GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            int expectedSequence,
            int expectedDay,
            GameplayOutcomeStatus expectedStatus)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.absoluteDay != expectedDay
                || exact.status != expectedStatus
                || exact.ownerRevision != 1L
                || exact.commitRevision != 1L
                || !string.Equals(
                    exact.producerId,
                    definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || exact.subjects?.Count !=
                    (kind == MigratedProducerOutcomeKind.FacilityShopPurchaseResult
                        ? 2 : 1)
                || exact.facts == null
                || !exact.facts.Any(value => string.Equals(
                    value.factId,
                    MigratedProducerOutcomeIds.SummaryFact.Value,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Facility wishlist entry did not publish its exact outcome: "
                    + kind);
            }
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(
                outcomes.Ledger,
                identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral facility wishlist fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["stage"] = Stage,
                    ["facilityCost"] = facilityCost,
                    ["buildingId"] = building.id,
                    ["gameSession"] = JObject.Parse(JsonUtility.ToJson(
                        GameSessionSaveData.From(session.State.Capture()))),
                    ["autoProcurement"] = JObject.Parse(JsonUtility.ToJson(
                        procurement.Capture())),
                    ["facilityShop"] = CaptureFacilityShop(
                        shop.CaptureState()),
                    ["physicalItems"] = JObject.Parse(JsonUtility.ToJson(
                        items.Capture())),
                    ["researchUnlockedBuildingIds"] = new JArray(
                        research.State.UnlockedBuildingIds
                            .OrderBy(value => value)),
                    ["shopFulfillments"] = new JArray(
                        research.State.CaptureShopFulfillments()
                            .Select(value => new JObject
                            {
                                ["purchaseResultKey"] = value.PurchaseResultKey,
                                ["offerTypeId"] = value.OfferTypeId,
                                ["offerDataId"] = value.OfferDataId,
                                ["itemDefinitionId"] = value.ItemDefinitionId,
                                ["itemDisplayName"] = value.ItemDisplayName,
                                ["phase"] = (int)value.Phase,
                                ["stackId"] = value.StackId,
                                ["itemInstanceId"] = value.ItemInstanceId
                            })),
                    ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(
                        outcomes.Transaction.Capture()))
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            purchaseFacts?.Dispose();
            domainFacts.Dispose();
            if (host != null)
                Object.DestroyImmediate(host);
            items?.Dispose();
        }

        private int Stage
        {
            get
            {
                int auto = OutcomeCount(
                    MigratedProducerOutcomeKind.AutoProcurementResult);
                int facility = OutcomeCount(
                    MigratedProducerOutcomeKind.FacilityShopPurchaseResult);
                int exact = ExactOutcomeCount();
                int owners = outcomes.Transaction.Capture().owners.Count;
                if (auto == 0 && facility == 0 && exact == 0 && owners == 0)
                    return 0;
                if (auto == 1 && facility == 0 && exact == 1 && owners == 1)
                    return 1;
                if (auto == 2 && facility == 1 && exact == 3 && owners == 3)
                    return 2;
                if (auto == 2 && facility == 0 && exact == 2 && owners == 2)
                    return 5;
                if (auto == 1 && facility == 1 && exact == 2 && owners == 2
                    && procurement.Capture().parentResults.Count == 1)
                {
                    AutoProcurementParentSaveData parent =
                        procurement.Capture().parentResults[0];
                    if (parent.phase == AutoProcurementParentPhase.ChildCommitted
                        && KitQuantity == 0)
                        return 3;
                    if (parent.phase == AutoProcurementParentPhase.ParentPending
                        && KitQuantity == 1)
                        return 4;
                }
                return -1;
            }
        }

        private int KitQuantity => items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    FacilityInstallationKitItemIds.ForBuilding(building),
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        type,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes().exactOutcomes.Count;

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                outcomes.Ledger);
            RequireFields(
                state,
                "facility wishlist owner state",
                "schemaVersion",
                "schema",
                "stage",
                "facilityCost",
                "buildingId",
                "gameSession",
                "autoProcurement",
                "facilityShop",
                "physicalItems",
                "researchUnlockedBuildingIds",
                "shopFulfillments",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || (int)state["buildingId"] != building.id
                || state["gameSession"] is not JObject sessionJson
                || state["autoProcurement"] is not JObject procurementJson
                || state["facilityShop"] is not JObject shopJson
                || state["physicalItems"] is not JObject physicalJson
                || state["researchUnlockedBuildingIds"] is not JArray unlocks
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException(
                    "Facility wishlist checkpoint identity changed.");
            }

            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None))));
            session.Restore(JsonUtility.FromJson<GameSessionSaveData>(
                sessionJson.ToString(Formatting.None)).ToSnapshot());
            var restoredTreasury = new TreasuryEconomyAggregateState();
            procurement.PopulateRestoreState(
                restoredTreasury,
                JsonUtility.FromJson<AutoProcurementSaveData>(
                    procurementJson.ToString(Formatting.None)));
            treasury.Replace(restoredTreasury);
            IFacilityShopPersistence persistence = shop;
            persistence.PublishRestoreCandidate(
                persistence.BuildRestoreCandidate(
                    RestoreFacilityShop(shopJson)));
            items.Restore(JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                physicalJson.ToString(Formatting.None)));
            foreach (int unlockedId in unlocks.Values<int>())
                research.State.RestoreUnlockedBuildingId(unlockedId);
            foreach (JToken row in (state["shopFulfillments"] as JArray
                         ?? new JArray()))
                research.State.RestoreShopFulfillment(
                    new FacilityShopFulfillmentRecord
                    {
                        PurchaseResultKey = (string)row["purchaseResultKey"],
                        OfferTypeId = (string)row["offerTypeId"],
                        OfferDataId = (int)row["offerDataId"],
                        ItemDefinitionId = (string)row["itemDefinitionId"],
                        ItemDisplayName = (string)row["itemDisplayName"],
                        Phase = (FacilityShopFulfillmentPhase)(int)row["phase"],
                        StackId = (string)row["stackId"],
                        ItemInstanceId = (string)row["itemInstanceId"]
                    });
            facilityCost = (int)state["facilityCost"];
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Facility wishlist checkpoint stage disagrees with saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            AutoProcurementSaveData saved = procurement.Capture();
            bool shape = stage switch
            {
                0 => session.State.day.Value == BlockedDay
                    && session.State.holdingMoney.Value == InitialMoney
                    && saved.dailyBudget == 500
                    && saved.minimumReserve == 0
                    && saved.lastProcessedDay == 0
                    && saved.lastResults.Count == 0
                    && saved.wishlistRules.Count == 0
                    && saved.processedOfferKeys.Count == 0
                    && !research.State.IsBuildingUnlocked(building.id)
                    && KitQuantity == 0
                    && facilityCost == 0,
                1 => session.State.day.Value == BlockedDay
                    && session.State.holdingMoney.Value == InitialMoney
                    && saved.dailyBudget == 0
                    && saved.minimumReserve == 0
                    && saved.lastProcessedDay == BlockedDay
                    && saved.lastResults.Count == 1
                    && !saved.lastResults[0].purchased
                    && string.Equals(
                        saved.lastResults[0].ruleId,
                        "budget",
                        StringComparison.Ordinal)
                    && saved.wishlistRules.Count == 0
                    && saved.processedOfferKeys.Count == 0
                    && !research.State.IsBuildingUnlocked(building.id)
                    && KitQuantity == 0
                    && facilityCost == 0,
                2 => session.State.day.Value == PurchaseDay
                    && facilityCost > 0
                    && session.State.holdingMoney.Value
                        == InitialMoney - facilityCost
                    && saved.dailyBudget == InitialMoney
                    && saved.minimumReserve == 0
                    && saved.lastProcessedDay == PurchaseDay
                    && saved.lastResults.Count == 1
                    && saved.lastResults[0].purchased
                    && saved.lastResults[0].quantity == 1
                    && saved.lastResults[0].cost == facilityCost
                    && string.Equals(
                        saved.lastResults[0].ruleId,
                        WishlistRuleId,
                        StringComparison.Ordinal)
                    && saved.wishlistRules.Count == 1
                    && saved.processedOfferKeys.Count == 1
                    && research.State.IsBuildingUnlocked(building.id)
                    && KitQuantity == 1
                    && saved.parentResults.Count == 1
                    && saved.parentResults[0].phase
                        == AutoProcurementParentPhase.ParentCommitted
                    && saved.lastResults[0].attemptedQuantity == 1
                    && saved.lastResults[0].attemptedCost == facilityCost
                    && saved.lastResults[0].committedQuantity == 1
                    && saved.lastResults[0].committedCost == facilityCost
                    && string.Equals(
                        saved.lastResults[0].attemptedItemId,
                        FacilityInstallationKitItemIds.ForBuilding(building),
                        StringComparison.Ordinal)
                    && string.Equals(
                        saved.lastResults[0].committedItemId,
                        FacilityInstallationKitItemIds.ForBuilding(building),
                        StringComparison.Ordinal)
                    && string.Equals(
                        saved.lastResults[0].childPurchaseResultKey,
                        saved.parentResults[0].childPurchaseResultKey,
                        StringComparison.Ordinal)
                    && saved.lastResults[0].physicalStacks.Count == 1
                    && saved.lastResults[0].physicalStacks[0].quantity == 1
                    && !string.IsNullOrWhiteSpace(
                        saved.lastResults[0].physicalStacks[0].itemInstanceId)
                    && research.State.CaptureShopFulfillments().Count == 1
                    && research.State.CaptureShopFulfillments()[0].Phase
                        == FacilityShopFulfillmentPhase.Delivered
                    && string.Equals(
                        saved.lastResults[0].physicalStacks[0].stackId,
                        research.State.CaptureShopFulfillments()[0].StackId,
                        StringComparison.Ordinal),
                3 or 4 => session.State.day.Value == PurchaseDay
                    && facilityCost > 0
                    && session.State.holdingMoney.Value
                        == InitialMoney - facilityCost
                    && saved.lastProcessedDay == PurchaseDay
                    && saved.lastResults.Count == 0
                    && saved.wishlistRules.Count == 1
                    && saved.processedOfferKeys.Count == 1
                    && saved.parentResults.Count == 1
                    && saved.parentResults[0].result.attemptedQuantity == 1
                    && saved.parentResults[0].result.attemptedCost
                        == facilityCost
                    && saved.parentResults[0].result.committedQuantity == 1
                    && saved.parentResults[0].result.committedCost
                        == facilityCost
                    && saved.parentResults[0].phase
                        == (stage == 3
                            ? AutoProcurementParentPhase.ChildCommitted
                            : AutoProcurementParentPhase.ParentPending)
                    && research.State.IsBuildingUnlocked(building.id)
                    && KitQuantity == (stage == 3 ? 0 : 1)
                    && research.State.CaptureShopFulfillments().Count == 1
                    && research.State.CaptureShopFulfillments()[0].Phase
                        == (stage == 3
                            ? FacilityShopFulfillmentPhase.Pending
                            : FacilityShopFulfillmentPhase.Delivered),
                5 => session.State.day.Value == PurchaseDay
                    && session.State.holdingMoney.Value == InitialMoney
                    && facilityCost == 0 && KitQuantity == 0
                    && saved.dailyBudget == InitialMoney && saved.minimumReserve == 0
                    && saved.lastProcessedDay == PurchaseDay
                    && saved.wishlistRules.Count == 1
                    && saved.processedOfferKeys.Count == 1
                    && saved.parentResults.Count == 0
                    && saved.lastResults.Count == 1
                    && saved.lastResults[0].day == PurchaseDay
                    && saved.lastResults[0].ruleId == WishlistRuleId
                    && !saved.lastResults[0].purchased
                    && saved.lastResults[0].attemptedItemId
                        == FacilityInstallationKitItemIds.ForBuilding(building)
                    && saved.lastResults[0].attemptedQuantity == 1
                    && saved.lastResults[0].attemptedCost > 0
                    && saved.lastResults[0].attemptedCost <= InitialMoney
                    && saved.lastResults[0].quantity == 0 && saved.lastResults[0].cost == 0
                    && saved.lastResults[0].committedQuantity == 0
                    && saved.lastResults[0].committedCost == 0
                    && string.IsNullOrEmpty(saved.lastResults[0].committedItemId)
                    && string.IsNullOrEmpty(saved.lastResults[0].childPurchaseResultKey)
                    && saved.lastResults[0].physicalStacks.Count == 0
                    && saved.lastResults[0].reason == RejectableShopMoneyAccount.RejectionReason
                    && !shop.UnlockState.IsBasicPurchaseUnlocked(building.id)
                    && shop.UnlockState.PurchaseSequence == 0
                    && !research.State.IsBuildingUnlocked(building.id)
                    && research.State.CaptureShopFulfillments().Count == 0,
                _ => false
            };
            if (!shape)
            {
                throw new InvalidOperationException(
                    "Facility wishlist money, policy, unlock, kit and exact ledger state diverged: stage="
                    + stage
                    + "; day=" + session.State.day.Value
                    + "; money=" + session.State.holdingMoney.Value
                    + "; lastProcessed=" + saved.lastProcessedDay
                    + "; results=" + saved.lastResults.Count
                    + "; rules=" + saved.wishlistRules.Count
                    + "; kit=" + KitQuantity
                    + "; cost=" + facilityCost);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class RejectableShopMoneyAccount : IGameMoneyAccount
    {
        internal const string RejectionReason = "test-fixture:funded-spend-rejected";
        private readonly IGameMoneyAccount inner;
        internal RejectableShopMoneyAccount(IGameMoneyAccount inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        internal bool RejectNextSpend { get; set; }
        internal int SpendAttempts { get; private set; }
        internal int RejectedSpends { get; private set; }
        internal int LastRequestedAmount { get; private set; }
        public int Balance => inner.Balance;
        public bool CanSpend(int amount) => inner.CanSpend(amount);
        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);
        public bool TrySpend(int amount, EconomyTransactionContext context, out string reason)
        {
            SpendAttempts++;
            LastRequestedAmount = amount;
            if (RejectNextSpend)
            {
                Require(amount > 0 && inner.CanSpend(amount),
                    "Spend rejection must reach an affordable, positive payment.");
                RejectNextSpend = false;
                RejectedSpends++;
                reason = RejectionReason;
                return false;
            }
            return inner.TrySpend(amount, context, out reason);
        }
        public void Add(int amount) => inner.Add(amount);
        public void Add(int amount, EconomyTransactionContext context) => inner.Add(amount, context);
        public void SetBalance(int amount, EconomyTransactionContext context) => inner.SetBalance(amount, context);
    }

    private sealed class BlueprintFulfillmentFixture : IDisposable
    {
        private readonly FacilityBlueprintSO blueprint;
        private readonly GameObject host;
        private readonly ToggleDropZone dropZone = new();

        internal BlueprintFulfillmentFixture(
            FacilityBlueprintSO blueprint,
            FacilityShopUnlockState unlocks,
            bool deliveryAvailable,
            DungeonResearchSaveData research = null,
            DungeonPhysicalItemSaveData physical = null,
            DungeonRuntimeAggregateRootStore aggregateRoot = null,
            GameEventBus events = null)
        {
            this.blueprint = blueprint
                ?? throw new ArgumentNullException(nameof(blueprint));
            aggregateRoot ??= new DungeonRuntimeAggregateRootStore();
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                new FixedGridProvider(CreateDropoffGrid()),
                out _,
                out _,
                out _,
                out _);
            if (physical != null)
                Items.Restore(physical);
            dropZone.DeliveryAvailable = deliveryAvailable;
            host = new GameObject("T02 Blueprint Fulfillment");
            Runtime = host.AddComponent<BlueprintResearchRuntime>();
            Runtime.Construct(
                new FixedUnlockStateService(unlocks),
                new BlueprintOnlyShopCatalog(blueprint),
                new FacilityCandidateCacheStore(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    frameWorkBudget: null),
                new DungeonWorkforceReplanService(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    facilityCandidateCache: null,
                    haulPlanningService: null),
                events ?? new GameEventBus(),
                Items,
                new BlueprintResearchProjectCoordinator(
                    new ResourceResearchProjectCatalog(
                        Array.Empty<ResearchProjectSO>()),
                    UnavailableResearchBlueprintArchiveQuery.Instance,
                    UnrestrictedResearchFacilityCapacityQuery.Instance),
                dropZone,
                aggregateRoot,
                DisabledDungeonDebugRuleQuery.Instance,
                new UnityUiClock());
            RestoreFulfillments(Runtime, research);
        }

        internal BlueprintResearchRuntime Runtime { get; }
        internal WorldItemStackRuntime Items { get; }
        internal int ItemQuantity => Items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    blueprint.PhysicalItemId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        internal bool TryGetFulfillment(
            string key,
            out FacilityShopFulfillmentRecord record) =>
            Runtime.TryGetShopFulfillment(key, out record);

        public void Dispose()
        {
            if (host != null)
                Object.DestroyImmediate(host);
            Items?.Dispose();
        }
    }

    private sealed class SessionStore :
        IGameSessionStateStore,
        IGameSessionStateProvider
    {
        internal SessionStore(GameSessionState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        internal GameSessionState State { get; }

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = State;
            return true;
        }

        public void Restore(GameSessionSnapshot snapshot)
        {
            State.holdingMoney.Initialize(snapshot.Money);
            State.day.Initialize(snapshot.Day);
            State.gameSpeed.Initialize(snapshot.GameSpeed);
            State.curTime.Initialize(snapshot.ElapsedSeconds);
            State.hour.Initialize(snapshot.Hour);
            State.timeOfDay.Initialize(snapshot.TimeOfDay);
        }
    }

    private sealed class SingleFacilityShopCatalog : IFacilityShopCatalog
    {
        private readonly BuildingSO building;
        internal SingleFacilityShopCatalog(BuildingSO building) =>
            this.building = building
                ?? throw new ArgumentNullException(nameof(building));
        public IReadOnlyCollection<BuildingSO> Buildings =>
            new[] { building };
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int buildingId) =>
            building.id == buildingId ? building : null;
    }

    private sealed class BlueprintOnlyShopCatalog : IFacilityShopCatalog
    {
        private readonly FacilityBlueprintSO blueprint;
        internal BlueprintOnlyShopCatalog(FacilityBlueprintSO blueprint) =>
            this.blueprint = blueprint
                ?? throw new ArgumentNullException(nameof(blueprint));
        public IReadOnlyCollection<BuildingSO> Buildings =>
            Array.Empty<BuildingSO>();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            new[] { blueprint };
        public BuildingSO FindBuildingById(int buildingId) => null;
    }

    private sealed class FixedUnlockStateService :
        IFacilityShopUnlockStateService
    {
        private readonly FacilityShopUnlockState state;
        internal FixedUnlockStateService(FacilityShopUnlockState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        public FacilityShopUnlockState GetUnlockState() => state;
    }

    private sealed class ToggleDropZone : IWorldDropZoneQuery
    {
        internal bool DeliveryAvailable = true;
        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = Vector2Int.zero;
            return DeliveryAvailable;
        }
        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = Vector2Int.zero;
            return true;
        }
        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class FixedGridProvider : IGridSystemProvider
    {
        internal FixedGridProvider(Grid grid) =>
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        public GridSystemManager Manager => null;
        public Grid Grid { get; }
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }
        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return true;
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;
        internal FixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));
        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));
        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static Grid CreateDropoffGrid()
    {
        var grid = new Grid(1, 1);
        grid.SetAreaType(Vector2Int.zero, GridCellAreaType.DropZone);
        return grid;
    }

    private static JObject CaptureFacilityShop(
        FacilityShopStateSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        return new JObject
        {
            ["currentOfferDay"] = snapshot.CurrentOfferDay,
            ["basicPurchaseBuildingIds"] = new JArray(
                snapshot.BasicPurchaseBuildingIds),
            ["acquiredBlueprintIds"] = new JArray(
                snapshot.AcquiredBlueprintIds)
        };
    }

    private static FacilityShopStateSnapshot RestoreFacilityShop(
        JObject value)
    {
        RequireFields(
            value,
            "facility shop state",
            "currentOfferDay",
            "basicPurchaseBuildingIds",
            "acquiredBlueprintIds");
        if (value["basicPurchaseBuildingIds"] is not JArray basic
            || value["acquiredBlueprintIds"] is not JArray blueprints)
        {
            throw new InvalidOperationException(
                "Facility shop state lists are missing.");
        }
        return new FacilityShopStateSnapshot(
            (int)value["currentOfferDay"],
            basic.Values<int>(),
            blueprints.Values<int>());
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = value?.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        string[] orderedExpected = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(orderedExpected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields changed: " + string.Join(",", actual));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
