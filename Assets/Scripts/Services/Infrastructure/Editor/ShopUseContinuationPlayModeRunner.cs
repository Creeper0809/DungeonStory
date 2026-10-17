#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Factions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Actual visitor/AI/shop/save authorities with explicitly labelled skill-response fixtures.</summary>
public sealed class ShopUseContinuationPlayModeRunner
{
    private sealed class Report
    {
        public string scope = "GameplayScene; controlled party/faction setup and legal skill-response fixtures; real visitor spawn, AIShopping, checkout and pre-commit visual feedback whole-world restores; exact retail stock/debit/revenue/outcomes and completed restore; one explicitly injected post-physical/pre-outcome purchase fault with whole-world recovery; independent cancel/disable branches and public occupied-shop demolition rejection. NOT unsupported forced shop destruction, model inference, natural play, meal/water or all failure boundaries.";
        public bool passed, completedOnce, completedRestoreExact, qualityContractChecks, visitorBindingChecks, nullPreservationChecks, testResponse = true;
        public bool modelInferenceRun, networkSent, humanApprovalClaimed, trainingEligible, fullAuditClosure;
        public string phase, failure, actorId, facilityId, operationId, serviceSessionId, lastSpawnRejection;
        public string fixtureFailure, skillDiagnostic;
        public NarrativeInferenceAuditRecord? lastSkillAudit;
        public int fixtureResponseCount, moneyBefore, moneyAfter, stockBefore, stockAfter;
        public float elapsedSeconds;
        public List<Checkpoint> checkpoints = new();
        public List<GameplayOutcomeSnapshot> purchases = new();
        public List<EconomyTransactionRecord> revenues = new();
        public List<object> rejectedRestores = new();
        public List<Termination> terminations = new();
        public List<object> rejectedDemolitions = new();
        public PendingPurchaseContinuation pendingPurchase;
        public CheckoutCleanupContinuation checkoutCleanup;
        public List<string> expectedFaults = new();
        public List<string> warnings = new(), errors = new();
    }
    private sealed class Checkpoint
    {
        public string phase;
        public int sections;
        public bool freshActor, freshShop, stateExact, sessionExact, stockExact, walletExact, rebound;
        public bool visitorProfileExact, visitorNotDuplicated, retiredOldActor;
        public ShopUseExecutionState before, after;
    }
    private sealed class Termination
    {
        public string checkpoint, caseId;
        public int purchaseCountAtInterruption;
        public bool stopped, walletExact, balanceExact, purchasesExact, stockExact, cancelledTerminal, repeatedCleanupExact, restoredExact;
        public bool restoredStockExact, restoredWalletExact, restoredBalanceExact, restoredPurchasesExact;
        public bool acknowledgedSessionExcluded, restoredReceiptEmpty, restoredSessionRetired, restoredOutcomeExact;
        public int walletBeforeRestore, walletAfterRestore, balanceBeforeRestore, balanceAfterRestore;
        public float restoredScale;
        public ServiceSessionSnapshot session;
        public ServiceSessionSnapshot restoredSession;
        public GameplayOutcomeSnapshot outcome;
        public GameplayOutcomeSnapshot restoredOutcome;
    }
    private sealed class PendingPurchaseContinuation
    {
        public bool passed, expectedFaultObserved, pendingExact, freshActor, freshShop, retiredOldActor, rebound;
        public bool stockExact, walletExact, outcomeExact, revenueExact, recoveryExact, completedOnce, completedRestoreExact;
        public bool physicalInstanceRemovedPending, physicalInstanceRemovedAfterRestore, physicalInstanceRemovedTerminal;
        public bool sessionCompleted, noReplay;
        public int sections, expectedFaultCount, probeReservations, probeCommitAttempts, probeCommits;
        public int stockBefore, stockPending, stockAfter, walletBefore, walletPending, walletAfter;
        public int balanceBefore, balancePending, balanceAfter, purchasesBefore, purchasesPending, purchasesAfter;
        public int finalQuantity, finalCost, price, cartCount;
        public string failure, operationId, shopUseOperationId, serviceSessionId, sourceOperationId, itemInstanceId;
        public RetailPurchaseRecoverySaveData pending;
        public ShopUseExecutionState pendingUse;
        public ServiceSessionSnapshot completedSession;
        public List<GameplayOutcomeSnapshot> purchases = new();
        public List<EconomyTransactionRecord> revenues = new();
    }
    private sealed class CheckoutCleanupContinuation
    {
        public bool passed, failureObserved, pendingExact, captureSucceeded, liveRecovered, restoreAccepted;
        public bool freshActor, freshShop, rebound, resumedCleanup, completedRestoreExact;
        public bool stockExact, walletExact, balanceExact, purchasesExact, terminalExact;
        public bool restoredWithoutOccupancy, acknowledgedSessionPruned, completedOutcomeExact;
        public int rejectedCleanupRestores;
        public int sections, faultCount, reservationCount, commitAttempts, commits, liveFrames;
        public float liveGameSeconds;
        public string captureFailure, restoreFailure;
        public string[] changedSectionsAfterRejectedRestore;
        public ShopUseExecutionState pending;
        public ServiceSessionSnapshot terminal;
        public GameplayOutcomeSnapshot terminalOutcome;
    }
    private readonly bool checkoutCleanupOnly = string.Equals(Environment.GetEnvironmentVariable(
        "DUNGEONSTORY_W01_CHECKOUT_CLEANUP_VERIFY"), "1", StringComparison.Ordinal);
    private bool expectCheckoutCleanupFault;
    private int checkoutCleanupFaultCount;
    private const string ExpectedPendingFaultDetail =
        "retail-outcome-commit-failed:migrated-producer-e2e-injected-commit-rejection";
    private const string ExpectedPendingException =
        "Retail purchase is durably pending recovery at phase 'PhysicalCommitted': "
        + ExpectedPendingFaultDetail;
    private readonly Report report = new();
    private DungeonRuntimeLifetimeScope scope;
    private ICharacterAiWorldRegistry world;
    private IDungeonGameSaveService saves;
    private IGameTimeScaleController timeScale;
    private IServiceSessionRuntime sessions;
    private CharacterSpawner spawner;
    private LocalLlmRequestQueue queue;
    private bool spawnerWasPaused;
    private float originalScale;
    private string output;
    private int captureSequence;
    private string checkoutWorld, feedbackWorld;
    private bool expectPendingPurchaseFault;
    private int expectedPendingPurchaseFaultCount;

    public ShopUseContinuationPlayModeRunner(IEnumerable<string> warnings, IEnumerable<string> errors)
    {
        report.warnings.AddRange(warnings);
        report.errors.AddRange(errors);
    }

    public IEnumerator Run()
    {
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.RequestPath);
        output = Path.GetDirectoryName(DungeonPersistencePaths.Root);
        Application.logMessageReceived += CaptureLog;
        float started = Time.realtimeSinceStartup;
        var stack = new Stack<IEnumerator>();
        stack.Push(Verify());
        while (stack.Count > 0)
        {
            bool moved;
            object next = null;
            try
            {
                Require(Time.realtimeSinceStartup - started < 240f, "Shop witness exceeded 240 real seconds at " + report.phase);
                Require(report.errors.Count == 0, "Runtime error: " + report.errors.FirstOrDefault());
                moved = stack.Peek().MoveNext();
                if (moved) next = stack.Peek().Current;
            }
            catch (Exception exception) { report.failure = exception.ToString(); break; }
            if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator child) stack.Push(child);
            else yield return next;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        report.elapsedSeconds = Time.realtimeSinceStartup - started;
        report.passed = string.IsNullOrEmpty(report.failure) && report.errors.Count == 0 && !report.networkSent
            && (checkoutCleanupOnly ? report.checkoutCleanup?.passed == true
                && !expectCheckoutCleanupFault && checkoutCleanupFaultCount == 1
            : report.completedOnce && report.completedRestoreExact && report.checkpoints.Count == 2
            && report.pendingPurchase?.passed == true && !expectPendingPurchaseFault
            && expectedPendingPurchaseFaultCount == 1
            && report.terminations.Count == 4 && report.terminations.All(value => value.restoredExact)
            && report.rejectedDemolitions.Count == 2);
        WriteNew("report.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        if (queue != null) queue.ClearIsolatedShopTestResponses();
        if (timeScale != null) timeScale.Scale = originalScale;
        if (spawner != null) spawner.ConfigureDeterministicSimulationForDiagnostics(paused: spawnerWasPaused);
        Application.logMessageReceived -= CaptureLog;
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private IEnumerator Verify()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Shop witness requires the owned isolated PlayMode entry.");
        report.phase = "bootstrap";
        if (checkoutCleanupOnly)
            report.scope = "Focused W01 checkout cleanup; controlled visitor eligibility/skill responses and crime selection; actual Brain/AIShopping/Shop interaction. Inject exactly one rejected crime reservation and one registry EndUse exception; preserve both failures, official pending whole-world save/fresh resume, live automatic cleanup, terminal receipt/no retail mutation and completed restore. No manual cleanup as recovery evidence, no natural crime/model inference/all checkout failures claim.";
        Require(ShopContinuationSkillResponseFixture.RunChecks(), "Skill fixture parser checks failed.");
        report.qualityContractChecks = NarrativeQualityContractFieldDebugScenarios.RunAll(log: true);
        Require(report.qualityContractChecks, "Narrative quality field contract checks failed.");
        report.visitorBindingChecks = VisitorPersistenceBindingDebugScenarios.RunAll(log: true);
        Require(report.visitorBindingChecks, "Visitor binding checks failed.");
        string[] codecFailures = CharacterSaveNullPreservationDebugScenarios.RunChecks();
        report.nullPreservationChecks = codecFailures.Length == 0;
        Require(report.nullPreservationChecks, "Character save null/value contract checks failed: " + string.Join(" | ", codecFailures));
        float deadline = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).FirstOrDefault(value => value != null && value.Container != null);
            if (scope == null) yield return null;
        }
        Require(scope != null, "Official world scope missing.");
        Require(scope.Container.Resolve<ICharacterSpawnerProvider>().TryGetSpawner(out spawner), "Spawner missing.");
        spawnerWasPaused = spawner.DeterministicSimulationPausedForDiagnostics;
        spawner.ConfigureDeterministicSimulationForDiagnostics(paused: true);
        queue = scope.Container.Resolve<ILocalLlmRuntimeProvider>().GetRequiredRuntime() as LocalLlmRequestQueue;
        Require(queue != null, "Isolated queue missing.");
        queue.ConfigureIsolatedRequestCapture(request =>
        {
            report.networkSent |= request.NetworkSent;
            WriteNew("request-" + (++captureSequence).ToString("D4") + ".json", request.WireBodyJson);
            WriteNew("request-" + captureSequence.ToString("D4") + "-internal.txt", request.Prompt);
        });
        queue.ConfigureIsolatedShopTestResponses(request =>
        {
            string response = ShopContinuationSkillResponseFixture.Build(request);
            if (response != null)
            {
                report.fixtureResponseCount++;
                NarrativeQualityResult quality = new NarrativeTextQualityGate().Evaluate(
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection, request.Prompt, response);
                if (!quality.IsAccepted) report.fixtureFailure = quality.Error;
                WriteNew("fixture-response-" + captureSequence.ToString("D4") + ".json",
                    JsonConvert.SerializeObject(new { testResponse = true, modelInferenceRun = false,
                        networkSent = false, trainingEligible = false, request.ProfileId, request.CorrelationId,
                        qualityAccepted = quality.IsAccepted, qualityError = quality.Error, response }, Formatting.Indented));
            }
            return response;
        });
        if (!scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out OwnerRunManager manager)
            || manager.CurrentOwnerActor == null)
        {
            StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            for (int frame = 0; frame < 10; frame++) yield return null;
        }
        Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(scope, out string setup), setup);
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        sessions = scope.Container.Resolve<IServiceSessionRuntime>();
        originalScale = timeScale.Scale;
        timeScale.Scale = 0f;
        PauseOthers(null);
        Require(world.TryGetGrid(out Grid grid), "Actual grid missing.");
        Shop shop = PrepareShop(grid);
        report.facilityId = shop.RequirePersistentInstanceId().Value;
        report.stockBefore = StockCount(shop);
        Require(report.stockBefore > 0, "Authored shop has no actual retail lots.");
        CharacterSO definition = (spawner.characters ?? Array.Empty<CharacterSO>()).Where(value => value != null
            && value.characterType == CharacterType.Customer && value.species != null
            && !value.species.ownerSelectable && !string.IsNullOrWhiteSpace(value.species.homeFactionId))
            .OrderBy(value => value.id).FirstOrDefault();
        Require(definition != null, "Authored external customer missing.");
        var factions = scope.Container.Resolve<IFactionRuntime>();
        for (int attempt = 0; attempt < 4 && !factions.IsContractUnlocked(definition.species.homeFactionId,
            FactionContractKind.Recruitment); attempt++)
            Require(factions.TryAdjustTrust(definition.species.homeFactionId, 100, "qa:shop-visitor-eligibility", out string reason), reason);
        Require(factions.IsContractUnlocked(definition.species.homeFactionId, FactionContractKind.Recruitment), "Visitor admission locked.");
        var previousIds = world.Characters.Where(value => value != null).Select(value => value.Identity.PersistentId).ToHashSet();
        CharacterActor actor = null;
        bool spawned = false;
        report.phase = "prepare-and-spawn-external-visitor";
        timeScale.Scale = 1f;
        deadline = Time.realtimeSinceStartup + 70f;
        while (actor == null && Time.realtimeSinceStartup < deadline)
        {
            var diagnostic = scope.Container.Resolve<ICharacterSkillGenerationDiagnostics>();
            report.skillDiagnostic = diagnostic.LastDiagnostic;
            report.lastSkillAudit = diagnostic.LastInferenceAudit;
            Require(string.IsNullOrEmpty(report.fixtureFailure), "Test response rejected by production quality gate: " + report.fixtureFailure);
            Require(!report.lastSkillAudit.HasValue || report.lastSkillAudit.Value.Succeeded,
                "Test skill response failed production parsing: " + report.lastSkillAudit?.ValidationError);
            if (!spawned)
            {
                spawned = spawner.TrySpawnCharacter(definition.id, out CharacterSpawnRejection rejected);
                report.lastSpawnRejection = rejected.ToString();
            }
            actor = world.Characters.FirstOrDefault(value => value != null && !previousIds.Contains(value.Identity.PersistentId)
                && value.Identity.Data.id == definition.id);
            if (actor == null) yield return null;
        }
        Require(actor != null, "Visitor was not prepared/spawned: " + report.lastSpawnRejection
            + "; fixture responses=" + report.fixtureResponseCount);
        report.actorId = actor.Identity.PersistentId;
        while (actor.CurrentLifecycleState != CharacterLifecycleState.Active && Time.realtimeSinceStartup < deadline) yield return null;
        Require(actor.CurrentLifecycleState == CharacterLifecycleState.Active && !Shop.IsInternalStaffUse(actor.BuildingVisitor),
            "Spawned visitor is not an active external customer.");
        queue.ClearIsolatedShopTestResponses();
        report.moneyBefore = actor.GetAbility<AbilityShopping>().HoldingMoney;
        PauseOthers(report.actorId);
        var scheduling = scope.Container.Resolve<ICharacterAiSchedulingService>();
        Require(actor.Brain.availableActions.Any(value => value?.actionset is AIShopping), "Visitor lacks authored AIShopping.");
        actor.Brain.PreferActionOnNextDecision<AIShopping>(180f);
        actor.Brain.RequestImmediateReplan(clearFailures: true);
        scheduling.RequestImmediateDecision(actor);
        report.phase = "checkout";
        deadline = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < deadline)
        {
            ShopUseExecutionState use = shop.FindActiveShopUse(report.actorId);
            if (use != null && use.phase == ShopUseExecutionPhase.Checkout && use.timerInitialized && use.remainingSeconds > 0f) break;
            if (use != null && use.phase == ShopUseExecutionPhase.Waiting && use.waitingForWorker && !shop.HasServingWorker)
                throw new InvalidOperationException("Actual managed checkout requires a worker; witness has not supplied one.");
            yield return null;
        }
        ShopUseExecutionState checkout = shop.FindActiveShopUse(report.actorId);
        Require(checkout != null && checkout.phase == ShopUseExecutionPhase.Checkout
            && checkout.timerInitialized && checkout.remainingSeconds > 0f,
            "No real checkout checkpoint: " + Describe(actor, shop));
        if (checkoutCleanupOnly)
        {
            yield return VerifyCheckoutCleanupContinuation();
            yield break;
        }
        RestoreCheckpoint("checkout");
        actor = Actor(); shop = FindShop();
        report.phase = "purchase-feedback";
        timeScale.Scale = 1f;
        deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline)
        {
            ShopUseExecutionState use = shop.FindActiveShopUse(report.actorId);
            if (use != null && use.phase == ShopUseExecutionPhase.Purchase && use.purchaseIndex < use.cart.Count
                && use.cart[use.purchaseIndex].feedbackStarted && use.cart[use.purchaseIndex].feedbackRemainingSeconds > 0f) break;
            yield return null;
        }
        ShopUseExecutionState feedback = shop.FindActiveShopUse(report.actorId);
        Require(feedback != null && feedback.phase == ShopUseExecutionPhase.Purchase
            && feedback.purchaseIndex < feedback.cart.Count && feedback.cart[feedback.purchaseIndex].feedbackStarted
            && feedback.cart[feedback.purchaseIndex].feedbackRemainingSeconds > 0f,
            "No purchase feedback checkpoint: " + Describe(actor, shop));
        RestoreCheckpoint("purchase-feedback");
        actor = Actor(); shop = FindShop();
        report.phase = "complete";
        timeScale.Scale = 1f;
        deadline = Time.realtimeSinceStartup + 30f;
        while (shop.FindActiveShopUse(report.actorId) != null && Time.realtimeSinceStartup < deadline) yield return null;
        timeScale.Scale = 0f;
        Require(shop.FindActiveShopUse(report.actorId) == null, "Actual shop execution did not finish: " + Describe(actor, shop));
        report.moneyAfter = actor.GetAbility<AbilityShopping>().HoldingMoney;
        report.stockAfter = StockCount(shop);
        report.purchases = ReadPurchases();
        Require(report.purchases.Count > 0 && report.purchases.Select(value => Fact(value, "retail-purchase-result-key")).Distinct().Count() == report.purchases.Count,
            "No unique actual retail purchase results.");
        int quantity = (int)report.purchases.Sum(value => Metric(value, "quantity"));
        int cost = (int)report.purchases.Sum(value => Metric(value, "amount"));
        Require(quantity == report.purchases.Count && report.stockBefore - report.stockAfter == quantity
            && report.moneyBefore - report.moneyAfter == cost && cost > 0, "Stock/debit/result mismatch.");
        var moneyQuery = scope.Container.Resolve<IGameMoneyAccount>() as IGameMoneyTransactionQuery;
        Require(moneyQuery != null, "Real money receipt query missing.");
        foreach (GameplayOutcomeSnapshot purchase in report.purchases)
        {
            string operation = Fact(purchase, "retail-buyer-debit-operation-id");
            Require(moneyQuery.TryGetSuccessfulTransaction(EconomyTransactionKind.GuestServiceIncome,
                "retail-revenue:" + operation, out EconomyTransactionRecord receipt)
                && receipt.amount == (int)Metric(purchase, "amount") && receipt.targetId == report.actorId,
                "Missing/mismatched exact retail revenue: " + operation);
            report.revenues.Add(receipt.Clone());
        }
        Require(sessions.TryGetSessionSnapshot(report.serviceSessionId, out ServiceSessionSnapshot terminal)
            && terminal.Stage == ServiceSessionStage.Completed && !string.IsNullOrEmpty(terminal.TerminalPublicationKey),
            "Shop service did not durably complete.");
        report.completedOnce = true;
        string complete = saves.ToJson(saves.Capture());
        WriteNew("completed-world.json", complete);
        string stockJson = JsonConvert.SerializeObject(shop.CreateStockSnapshot());
        string sessionJson = JsonConvert.SerializeObject(sessions.Capture());
        string purchasesJson = JsonConvert.SerializeObject(report.purchases);
        CharacterActor oldActor = actor; Shop oldShop = shop;
        Require(saves.TryRestore(saves.FromJson(complete), out DungeonGameRestoreReport restored), string.Join(" | ", restored.Errors));
        actor = Actor(); shop = FindShop();
        report.completedRestoreExact = !ReferenceEquals(oldActor, actor) && !ReferenceEquals(oldShop, shop)
            && shop.FindActiveShopUse(report.actorId) == null
            && actor.GetAbility<AbilityShopping>().HoldingMoney == report.moneyAfter
            && actor.GetAbility<AbilityShopping>().CapturePendingRetailPurchases().Count == 0
            && stockJson == JsonConvert.SerializeObject(shop.CreateStockSnapshot())
            && sessionJson == JsonConvert.SerializeObject(sessions.Capture())
            && purchasesJson == JsonConvert.SerializeObject(ReadPurchases());
        foreach (EconomyTransactionRecord expected in report.revenues)
            report.completedRestoreExact &= moneyQuery.TryGetSuccessfulTransaction(expected.kind, expected.sourceId, out EconomyTransactionRecord actual)
                && JsonConvert.SerializeObject(expected) == JsonConvert.SerializeObject(actual);
        Require(report.completedRestoreExact, "Completed whole-world restore replayed or lost retail effects.");
        WriteNew("completed-restored-world.json", saves.ToJson(saves.Capture()));
        yield return VerifyPendingPurchaseContinuation();
        yield return VerifyTerminationBranches();
        report.phase = "verified";
    }

    private IEnumerator VerifyCheckoutCleanupContinuation()
    {
        var witness = new CheckoutCleanupContinuation();
        report.checkoutCleanup = witness;
        report.phase = "checkout-cleanup-inject";
        timeScale.Scale = 0f;
        CharacterActor actor = Actor();
        Shop shop = FindShop();
        AbilityShopping shopping = actor.GetAbility<AbilityShopping>();
        ShopUseExecutionState checkout = shop.FindActiveShopUse(report.actorId)?.Clone();
        Require(checkout != null && checkout.createsRevenue && checkout.cart.Count > 0
            && !string.IsNullOrEmpty(checkout.serviceSessionId), "Live checkout/session/cart missing.");
        report.operationId = checkout.operationId;
        report.serviceSessionId = checkout.serviceSessionId;
        string stock = JsonConvert.SerializeObject(shop.CreateStockSnapshot());
        int wallet = shopping.HoldingMoney;
        var account = scope.Container.Resolve<IGameMoneyAccount>();
        int balance = account.Balance;
        string purchases = JsonConvert.SerializeObject(ReadPurchases());
        WriteNew("checkout-cleanup-before-world.json", saves.ToJson(saves.Capture()));
        var migrated = scope.Container.Resolve<MigratedProducerOutcomeRuntime>();
        var probe = new MigratedProducerOutcomeProbeTransaction(migrated)
        {
            TargetKind = MigratedProducerOutcomeKind.FacilityCrimeEvent,
            FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation
        };
        var session = scope.Container.Resolve<IGameSessionStateProvider>();
        var productionJournal = scope.Container.Resolve<ShopliftingFactJournal>();
        var registryField = typeof(BuildableObject).GetField("worldRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(registryField != null, "Exact registry fault seam missing.");
        var originalRegistry = registryField.GetValue(shop) as IBuildingWorldRegistryPort;
        Require(originalRegistry != null, "Actual shop registry missing.");
        var faultRegistry = new CheckoutCleanupFaultRegistry(originalRegistry, shop, actor.BuildingVisitor.BuildingCharacterId);
        var random = scope.Container.Resolve<IRandomStreamProvider>().Get("shop-runtime");
        ShopliftingFactEditorFixture faultFacts = null;
        try
        {
            faultFacts = new ShopliftingFactEditorFixture(
                migrated,
                scope.Container.Resolve<GameplayDomainFactCommitGroup>(),
                probe);
            shop.ConfigureCrimeForDiagnostics(new CheckoutCleanupCrimeEvaluator(), random);
            shop.ConstructShopCrimeFacts(faultFacts.Journal, session);
            registryField.SetValue(shop, faultRegistry);
            expectCheckoutCleanupFault = true;
            timeScale.Scale = 1f;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (checkoutCleanupFaultCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            timeScale.Scale = 0f;
            witness.faultCount = faultRegistry.FailureCount;
            witness.reservationCount = probe.ReservationCount(MigratedProducerOutcomeKind.FacilityCrimeEvent);
            witness.commitAttempts = probe.CommitAttemptCount(MigratedProducerOutcomeKind.FacilityCrimeEvent);
            witness.commits = probe.CommitCount(MigratedProducerOutcomeKind.FacilityCrimeEvent);
            witness.failureObserved = checkoutCleanupFaultCount == 1 && witness.faultCount == 1
                && witness.reservationCount == 1 && witness.commitAttempts == 0 && witness.commits == 0;
            witness.pending = shop.FindActiveShopUse(report.actorId)?.Clone();
            Require(sessions.TryGetSessionSnapshot(checkout.serviceSessionId, out witness.terminal),
                "Failed checkout lost service session.");
            var outcomes = scope.Container.Resolve<IGameplayOutcomeQuery>();
            Require(TryReadTerminal(outcomes, witness.terminal, out witness.terminalOutcome),
                "Failed checkout lacks exact terminal receipt.");
            witness.pendingExact = witness.pending != null && witness.pending.operationId == checkout.operationId
                && witness.pending.phase == ShopUseExecutionPhase.ServiceCompletion && !witness.pending.serviceSucceeded
                && witness.pending.serviceFailureReason.Contains("Shoplifting outcome reservation failed:", StringComparison.Ordinal)
                && JsonConvert.SerializeObject(witness.pending.cart) == JsonConvert.SerializeObject(checkout.cart)
                && !shop.IsActiveUser(actor.BuildingVisitor.BuildingCharacterId)
                && shopping.LastVisitOutcome == ShoppingVisitOutcome.Failed
                && !witness.terminal.IsActive && witness.terminal.Stage == ServiceSessionStage.Cancelled;
            witness.stockExact = stock == JsonConvert.SerializeObject(shop.CreateStockSnapshot());
            witness.walletExact = wallet == shopping.HoldingMoney;
            witness.balanceExact = balance == account.Balance;
            witness.purchasesExact = purchases == JsonConvert.SerializeObject(ReadPurchases());
            WriteNew("checkout-cleanup-fault-observed.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
            Require(witness.failureObserved && witness.pendingExact && witness.stockExact && witness.walletExact
                && witness.balanceExact && witness.purchasesExact, "Actual failed checkout/cleanup boundary mismatch.");
        }
        finally
        {
            try
            {
                // Remove only the injected dependencies. Never call EndUse/FinishRetail for recovery.
                registryField.SetValue(shop, originalRegistry);
                shop.ConstructShopCrimeFacts(productionJournal, session);
                shop.ConfigureCrimeForDiagnostics(
                    scope.Container.Resolve<IFacilityCrimeRiskEvaluator>(),
                    random);
            }
            finally
            {
                faultFacts?.Dispose();
            }
        }

        report.phase = "checkout-cleanup-pending-save";
        DungeonGameSaveData pending = null;
        try { pending = saves.Capture(); witness.captureSucceeded = true; }
        catch (Exception exception) { witness.captureFailure = exception.ToString(); }
        WriteNew("checkout-cleanup-capture-observed.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
        Require(witness.captureSucceeded, "Official pending capture failed: " + witness.captureFailure);
        witness.sections = pending.sections.Count;
        string pendingJson = saves.ToJson(pending);
        WriteNew("checkout-cleanup-pending-world.json", pendingJson);
        string terminalJson = JsonConvert.SerializeObject(witness.terminal);
        string terminalOutcomeJson = JsonConvert.SerializeObject(witness.terminalOutcome);
        VerifyCheckoutCleanupRejections(pendingJson, actor, shop);
        witness.rejectedCleanupRestores = report.rejectedRestores.Count;

        report.phase = "checkout-cleanup-live-recovery";
        var clock = scope.Container.Resolve<IGameClock>();
        float gameStarted = clock.Time;
        int frameStarted = Time.frameCount;
        timeScale.Scale = 1f;
        while (shop.FindActiveShopUse(report.actorId) != null && clock.Time - gameStarted < 5f) yield return null;
        timeScale.Scale = 0f;
        witness.liveGameSeconds = clock.Time - gameStarted;
        witness.liveFrames = Time.frameCount - frameStarted;
        witness.liveRecovered = shop.FindActiveShopUse(report.actorId) == null
            && !shopping.HasActiveShoppingRoutineForDiagnostics;
        WriteNew("checkout-cleanup-live-observed.json", JsonConvert.SerializeObject(witness, Formatting.Indented));

        report.phase = "checkout-cleanup-pending-restore";
        DungeonGameSaveData beforeRestore = saves.Capture();
        witness.restoreAccepted = saves.TryRestore(saves.FromJson(pendingJson), out DungeonGameRestoreReport restored);
        witness.restoreFailure = string.Join(" | ", restored.Errors);
        if (!witness.restoreAccepted)
        {
            DungeonGameSaveData afterRestore = saves.Capture();
            witness.changedSectionsAfterRejectedRestore = beforeRestore.sections.Where(value =>
                !afterRestore.sections.Any(other => other.sectionId == value.sectionId
                    && other.sectionVersion == value.sectionVersion && other.payloadJson == value.payloadJson
                    && other.optional == value.optional)).Select(value => value.sectionId).ToArray();
            WriteNew("checkout-cleanup-rejected-world.json", saves.ToJson(afterRestore));
        }
        WriteNew("checkout-cleanup-restore-observed.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
        Require(witness.restoreAccepted, "Official pending restore failed: " + witness.restoreFailure);
        CharacterActor fresh = Actor(); Shop replacement = FindShop();
        witness.freshActor = !ReferenceEquals(actor, fresh);
        witness.freshShop = !ReferenceEquals(shop, replacement);
        witness.rebound = fresh.GetAbility<AbilityShopping>().HasActiveShoppingRoutineForDiagnostics
            && fresh.Brain.bestAction?.destination == replacement;
        witness.restoredWithoutOccupancy = !replacement.IsActiveUser(fresh.BuildingVisitor.BuildingCharacterId)
            && JsonConvert.SerializeObject(replacement.FindActiveShopUse(report.actorId))
                == JsonConvert.SerializeObject(witness.pending);
        PauseOthers(report.actorId);
        report.phase = "checkout-cleanup-restored-recovery";
        gameStarted = clock.Time;
        timeScale.Scale = 1f;
        while (replacement.FindActiveShopUse(report.actorId) != null && clock.Time - gameStarted < 5f) yield return null;
        timeScale.Scale = 0f;
        witness.resumedCleanup = replacement.FindActiveShopUse(report.actorId) == null
            && !fresh.GetAbility<AbilityShopping>().HasActiveShoppingRoutineForDiagnostics;
        witness.stockExact &= stock == JsonConvert.SerializeObject(replacement.CreateStockSnapshot());
        witness.walletExact &= wallet == fresh.GetAbility<AbilityShopping>().HoldingMoney;
        witness.balanceExact &= balance == account.Balance;
        witness.purchasesExact &= purchases == JsonConvert.SerializeObject(ReadPurchases());
        witness.terminalExact = sessions.TryGetSessionSnapshot(checkout.serviceSessionId, out var terminal)
            && terminalJson == JsonConvert.SerializeObject(terminal)
            && TryReadTerminal(scope.Container.Resolve<IGameplayOutcomeQuery>(), terminal, out var outcome)
            && terminalOutcomeJson == JsonConvert.SerializeObject(outcome);
        WriteNew("checkout-cleanup-resumed-observed.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
        Require(witness.liveRecovered && witness.freshActor && witness.freshShop && witness.rebound
            && witness.restoredWithoutOccupancy && witness.rejectedCleanupRestores == 5
            && witness.resumedCleanup && witness.stockExact && witness.walletExact && witness.balanceExact
            && witness.purchasesExact && witness.terminalExact, "Checkout cleanup did not recover exactly through production execution.");
        string completed = saves.ToJson(saves.Capture());
        WriteNew("checkout-cleanup-completed-world.json", completed);
        Require(saves.TryRestore(saves.FromJson(completed), out restored), string.Join(" | ", restored.Errors));
        witness.completedRestoreExact = !ReferenceEquals(fresh, Actor()) && !ReferenceEquals(replacement, FindShop())
            && FindShop().FindActiveShopUse(report.actorId) == null
            && stock == JsonConvert.SerializeObject(FindShop().CreateStockSnapshot())
            && wallet == Actor().GetAbility<AbilityShopping>().HoldingMoney && balance == account.Balance
            && purchases == JsonConvert.SerializeObject(ReadPurchases());
        witness.acknowledgedSessionPruned = !sessions.TryGetSessionSnapshot(checkout.serviceSessionId, out _);
        witness.completedOutcomeExact = TryReadTerminal(scope.Container.Resolve<IGameplayOutcomeQuery>(),
            witness.terminal, out var completedOutcome)
            && terminalOutcomeJson == JsonConvert.SerializeObject(completedOutcome);
        witness.passed = witness.completedRestoreExact && witness.acknowledgedSessionPruned
            && witness.completedOutcomeExact;
        WriteNew("checkout-cleanup-witness.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
        Require(witness.passed, "Completed cleanup restore changed retail owners.");
        report.phase = "verified-checkout-cleanup";
    }

    private void VerifyCheckoutCleanupRejections(string pendingJson, CharacterActor actor, Shop shop)
    {
        foreach (string caseId in new[] { "missing-session", "wrong-reason", "forged-matching-reason", "wrong-digest", "orphan-ack" })
        {
            DungeonGameSaveData candidate = saves.FromJson(pendingJson);
            var serviceSection = candidate.sections.Single(value => value.sectionId == ServiceRoomsSaveSection.Id);
            var facilitySection = candidate.sections.Single(value => value.sectionId == ModularFacilityWorldSaveSection.Id);
            var services = DungeonSaveJson.Deserialize<ServiceRoomsSaveData>(serviceSection.payloadJson);
            var facilities = DungeonSaveJson.Deserialize<ModularFacilityWorldSaveData>(facilitySection.payloadJson);
            ServiceSessionSaveData terminal = services.sessions.Single(value => value.sessionId == report.serviceSessionId);
            var module = facilities.buildings.Single(value => value.persistentInstanceId == report.facilityId)
                .stateModules.Single(value => value.moduleId == BuildingStateModuleIds.ShopInteraction);
            var interaction = DungeonSaveJson.Deserialize<ShopInteractionState>(module.payload);
            ShopUseExecutionState execution = interaction.activeUses.Single(value => value.actorId == report.actorId);
            string expected = "Service execution owner mismatch (shop)";
            if (caseId == "missing-session") services.sessions.Remove(terminal);
            else if (caseId == "wrong-reason") terminal.cancellationReason += ":forged";
            else if (caseId == "forged-matching-reason")
            {
                terminal.cancellationReason += ":forged";
                execution.serviceFailureReason = terminal.cancellationReason;
                expected = "Shop cleanup terminal outcome mismatch";
            }
            else if (caseId == "wrong-digest")
            {
                terminal.terminalPublicationFingerprint = new string('0', 64);
                expected = "Shop cleanup terminal outcome mismatch";
            }
            else
            {
                interaction.activeUses.Remove(execution);
                expected = "Acknowledged service session has no pending cleanup owner";
            }
            serviceSection.payloadJson = JsonUtility.ToJson(services);
            // Match the production module codec: null patience/committed lot
            // must not become empty objects before the intended owner check.
            module.payload = JsonConvert.SerializeObject(interaction);
            Require(DungeonSaveJson.Deserialize<ShopInteractionState>(module.payload)
                .IsValid(report.facilityId, out _), "Cleanup rejection fixture corrupted the shop execution shape.");
            facilitySection.payloadJson = JsonUtility.ToJson(facilities);
            candidate.manifest = DungeonSaveManifest.Capture(candidate.sections);
            WriteNew("checkout-cleanup-reject-" + caseId + "-input.json", saves.ToJson(candidate));
            var root = scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
            int revision = root.PublishedRestoreRevision;
            var action = actor.Brain.bestAction;
            var liveUse = shop.FindActiveShopUse(report.actorId);
            var before = saves.Capture();
            bool accepted = saves.TryRestore(candidate, out var rejected);
            var after = saves.Capture();
            string reason = string.Join(" | ", rejected.Errors);
            string[] changed = before.sections.Where(value => !after.sections.Any(other => other.sectionId == value.sectionId
                && other.sectionVersion == value.sectionVersion && other.payloadJson == value.payloadJson && other.optional == value.optional))
                .Select(value => value.sectionId).ToArray();
            bool referencesExact = ReferenceEquals(actor, Actor()) && ReferenceEquals(shop, FindShop())
                && ReferenceEquals(action, actor.Brain.bestAction) && ReferenceEquals(liveUse, shop.FindActiveShopUse(report.actorId));
            bool rootExact = !root.IsRestoreStaging && root.PublishedRestoreRevision == revision && timeScale.Scale == 0f;
            var witness = new { caseId, accepted, reason, reasonMatched = reason.Contains(expected, StringComparison.Ordinal),
                sections = before.sections.Count, changed, referencesExact, rootExact };
            report.rejectedRestores.Add(witness);
            WriteNew("checkout-cleanup-reject-" + caseId + "-witness.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
            Require(!accepted && witness.reasonMatched && changed.Length == 0 && before.sections.Count == after.sections.Count
                && before.sections.Count == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count
                && referencesExact && rootExact, "Invalid cleanup restore changed live owners or failed at the wrong boundary: " + caseId);
        }
    }

    public static void RunCheckoutCleanupRegressionBatch()
    {
        Require(ShopUseExecutionStateDebugScenarios.RunAll(), "Shop execution DTO regression failed.");
        Require(ServiceExecutionOwnerSaveValidationDebugScenarios.RunAll(), "Service execution owner regression failed.");
        Require(ServiceSessionOutcomeDebugScenarios.RunAll(log: true), "Service terminal regression failed.");
        MigratedProducerEconomyEntryExtendedE2EDebugScenarios.RunCheckoutBoundaryRegressionBatch();
        Debug.Log("W01_CHECKOUT_CLEANUP_REGRESSION=PASS; dto, detached-owner, service-terminal, checkout-boundary");
    }

    private sealed class CheckoutCleanupCrimeEvaluator : IFacilityCrimeRiskEvaluator
    {
        public float CalculateShopliftingChance(FacilityCrimeRiskContext context) => 1f;
        public float CalculateOperationalRisk(FacilityCrimeRiskContext context) => 1f;
        public bool ShouldTriggerCrime(float chance, float roll) => true;
    }

    private sealed class CheckoutCleanupFaultRegistry : IBuildingWorldRegistryPort
    {
        internal const string FailureMessage = "w01-injected-checkout-end-use-failure";
        private readonly IBuildingWorldRegistryPort original;
        private readonly IBuildingWorldEntryPort target;
        private readonly CharacterId actor;
        internal int FailureCount { get; private set; }
        internal CheckoutCleanupFaultRegistry(IBuildingWorldRegistryPort original, IBuildingWorldEntryPort target, CharacterId actor)
        { this.original = original; this.target = target; this.actor = actor; }
        public int BuildingVersion => original.BuildingVersion;
        public IReadOnlyList<IBuildingWorldEntryPort> Buildings => original.Buildings;
        public void RegisterBuilding(IBuildingWorldEntryPort building) => original.RegisterBuilding(building);
        public void UnregisterBuilding(IBuildingWorldEntryPort building) => original.UnregisterBuilding(building);
        public bool TryReplaceBuilding(IBuildingWorldEntryPort expectedCurrent, IBuildingWorldEntryPort replacement, out string failureReason) =>
            original.TryReplaceBuilding(expectedCurrent, replacement, out failureReason);
        public bool TryRollbackBuildingReplacement(IBuildingWorldEntryPort expectedReplacement, IBuildingWorldEntryPort restored, out string failureReason) =>
            original.TryRollbackBuildingReplacement(expectedReplacement, restored, out failureReason);
        public void TrackTransientCharacterOwnership(IBuildingWorldEntryPort building, CharacterId characterId, BuildingTransientOwnershipKind kind) =>
            original.TrackTransientCharacterOwnership(building, characterId, kind);
        public void UntrackTransientCharacterOwnership(IBuildingWorldEntryPort building, CharacterId characterId, BuildingTransientOwnershipKind kind)
        {
            if (FailureCount == 0 && ReferenceEquals(building, target) && characterId.Equals(actor)
                && kind == BuildingTransientOwnershipKind.ActiveUse)
            { FailureCount++; throw new InvalidOperationException(FailureMessage); }
            original.UntrackTransientCharacterOwnership(building, characterId, kind);
        }
        public void UntrackAllTransientCharacterOwnership(IBuildingWorldEntryPort building) => original.UntrackAllTransientCharacterOwnership(building);
    }

    private void RestoreCheckpoint(string phase)
    {
        timeScale.Scale = 0f;
        CharacterActor actor = Actor(); Shop shop = FindShop();
        ShopUseExecutionState use = shop.FindActiveShopUse(report.actorId).Clone();
        report.operationId = use.operationId; report.serviceSessionId = use.serviceSessionId;
        Require(use.createsRevenue && use.cart.Count > 0, "Actual external retail cart missing.");
        int money = actor.GetAbility<AbilityShopping>().HoldingMoney;
        string stock = JsonConvert.SerializeObject(shop.CreateStockSnapshot());
        string service = JsonConvert.SerializeObject(sessions.Capture());
        var population = scope.Container.Resolve<ICharacterPopulationService>();
        WorldCharacterProfile profile = population.Profiles.Single(value => value.persistentId == report.actorId);
        Require(profile.isVisiting && profile.isAlive && !profile.isStaff, "Live visitor population owner is not visiting.");
        // Capture synchronizes profiles before sealing; compare that authority.
        DungeonGameSaveData captured = saves.Capture();
        string profileJson = JsonConvert.SerializeObject(profile);
        WriteNew(phase + "-profile-before.json", profileJson);
        Require(service == JsonConvert.SerializeObject(sessions.Capture())
            && sessions.TryGetSessionSnapshot(use.serviceSessionId, out ServiceSessionSnapshot stillActive) && stillActive.IsActive,
            "Capturing the world cancelled or changed an active visitor service.");
        string json = saves.ToJson(captured);
        WriteNew(phase + "-world.json", json);
        if (phase == "checkout") checkoutWorld = json;
        else feedbackWorld = json;
        if (phase == "checkout") VerifyVisitorRejections(json, actor, shop);
        Require(saves.TryRestore(saves.FromJson(json), out DungeonGameRestoreReport restored), string.Join(" | ", restored.Errors));
        CharacterActor fresh = Actor(); Shop replacement = FindShop();
        WriteNew(phase + "-profile-after.json", JsonConvert.SerializeObject(population.Profiles.Single(value => value.persistentId == report.actorId)));
        WriteNew(phase + "-restored-world.json", saves.ToJson(saves.Capture()));
        ShopUseExecutionState after = replacement.FindActiveShopUse(report.actorId)?.Clone();
        var witness = new Checkpoint { phase = phase, sections = captured.sections.Count, before = use, after = after,
            freshActor = !ReferenceEquals(actor, fresh), freshShop = !ReferenceEquals(shop, replacement),
            stateExact = JsonConvert.SerializeObject(use) == JsonConvert.SerializeObject(after),
            sessionExact = service == JsonConvert.SerializeObject(sessions.Capture()),
            stockExact = stock == JsonConvert.SerializeObject(replacement.CreateStockSnapshot()),
            walletExact = money == fresh.GetAbility<AbilityShopping>().HoldingMoney,
            visitorProfileExact = profileJson == JsonConvert.SerializeObject(population.Profiles.Single(value => value.persistentId == report.actorId)),
            visitorNotDuplicated = world.Characters.Count(value => value != null && value.Identity.PersistentId == report.actorId) == 1
                && population.Profiles.Single(value => value.persistentId == report.actorId).isVisiting,
            retiredOldActor = !actor.gameObject.activeInHierarchy && actor.CurrentLifecycleState == CharacterLifecycleState.Despawned,
            rebound = fresh.Brain.bestAction?.destination == replacement && fresh.GetAbility<AbilityShopping>().HasActiveShoppingRoutineForDiagnostics };
        report.checkpoints.Add(witness);
        WriteNew(phase + "-witness.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
        Require(witness.freshActor && witness.freshShop && witness.stateExact && witness.sessionExact && witness.stockExact
            && witness.walletExact && witness.rebound && witness.visitorProfileExact && witness.visitorNotDuplicated
            && witness.retiredOldActor, "Whole-world restore did not preserve/rebind actual shop use: " + phase);
        PauseOthers(report.actorId);
    }

    private IEnumerator VerifyPendingPurchaseContinuation()
    {
        var witness = new PendingPurchaseContinuation();
        report.pendingPurchase = witness;
        report.phase = "pending-purchase-prepare";
            Require(saves.TryRestore(saves.FromJson(feedbackWorld), out DungeonGameRestoreReport restored),
                "Pending purchase checkpoint restore: " + string.Join(" | ", restored.Errors));
            timeScale.Scale = 0f;
            PauseOthers(report.actorId);
            yield return null;
            yield return null;

            CharacterActor actor = Actor();
            Shop shop = FindShop();
            AbilityShopping shopping = actor.GetAbility<AbilityShopping>();
            ShopUseExecutionState use = shop.FindActiveShopUse(report.actorId)?.Clone();
            Require(use != null && use.phase == ShopUseExecutionPhase.Purchase
                && use.purchaseIndex >= 0 && use.purchaseIndex < use.cart.Count,
                "Pending purchase branch did not resume at a real cart line.");
            ShopUseCartLine line = use.cart[use.purchaseIndex];
            Require(line.feedbackStarted && line.feedbackRemainingSeconds > 0f
                && !line.Committed && string.IsNullOrEmpty(line.pendingPurchaseOperationId),
                "Pending purchase branch did not start from the pre-commit feedback checkpoint.");
            Require(shopping.CapturePendingRetailPurchases().Count == 0,
                "Pre-commit checkpoint already contains a pending purchase.");

            var account = scope.Container.Resolve<IGameMoneyAccount>();
            var moneyQuery = account as IGameMoneyTransactionQuery;
            Require(moneyQuery != null, "Real money receipt query missing for pending purchase branch.");
            List<GameplayOutcomeSnapshot> purchasesBefore = ReadPurchases();
            var purchaseKeysBefore = purchasesBefore.Select(value =>
                Fact(value, "retail-purchase-result-key")).ToHashSet(StringComparer.Ordinal);
            witness.stockBefore = StockCount(shop);
            witness.walletBefore = shopping.HoldingMoney;
            witness.balanceBefore = account.Balance;
            witness.purchasesBefore = purchasesBefore.Count;
            witness.price = line.price;
            witness.cartCount = use.cart.Count;
            witness.shopUseOperationId = use.operationId;
            witness.serviceSessionId = use.serviceSessionId;
            int committedCountBefore = shopping.CommittedPurchaseCountForDiagnostics;
            long committedMassBefore = shopping.CommittedPurchaseMassGramsForDiagnostics;

            var migrated = scope.Container.Resolve<MigratedProducerOutcomeRuntime>();
            var probe = new MigratedProducerOutcomeProbeTransaction(migrated)
            {
                TargetKind = MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult,
                FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit
            };
            using var purchaseFacts = new RetailPurchaseFactEditorFixture(
                migrated,
                scope.Container.Resolve<GameplayDomainFactCommitGroup>(),
                probe);
            shopping.ConstructRetailPurchaseFacts(
                purchaseFacts.Publisher,
                purchaseFacts.Recovery,
                scope.Container.Resolve<IGameSessionStateProvider>());
            expectPendingPurchaseFault = true;
            report.phase = "pending-purchase-injected-outcome-rejection";
            timeScale.Scale = 1f;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                IReadOnlyList<RetailPurchaseRecoverySaveData> pending =
                    shopping.CapturePendingRetailPurchases();
                if (pending.Count == 1
                    && pending[0].phase == RetailPurchaseRecoveryPhase.PhysicalCommitted
                    && expectedPendingPurchaseFaultCount == 1)
                    break;
                yield return null;
            }
            timeScale.Scale = 0f;
            witness.expectedFaultCount = expectedPendingPurchaseFaultCount;
            witness.expectedFaultObserved = !expectPendingPurchaseFault
                && expectedPendingPurchaseFaultCount == 1;
            Require(witness.expectedFaultObserved,
                "The exact injected retail outcome failure was not observed once.");

            IReadOnlyList<RetailPurchaseRecoverySaveData> pendingRows =
                shopping.CapturePendingRetailPurchases();
            Require(pendingRows.Count == 1, "Injected retail failure did not retain exactly one recovery row.");
            witness.pending = pendingRows[0].Clone();
            witness.operationId = witness.pending.operationId;
            witness.sourceOperationId = witness.pending.lot?.sourceOperationId ?? string.Empty;
            witness.itemInstanceId = witness.pending.lot?.itemInstanceId ?? string.Empty;
            Require(!string.IsNullOrEmpty(witness.itemInstanceId),
                "Actual pending purchase did not select a unique physical item instance.");
            witness.physicalInstanceRemovedPending = !scope.Container
                .Resolve<ICombatEquipmentRuntime>()
                .TryGetInstance(witness.itemInstanceId, out _);
            witness.pendingUse = shop.FindActiveShopUse(report.actorId)?.Clone();
            Require(witness.pendingUse != null
                && witness.pendingUse.purchaseIndex == witness.pending.shopUseCartIndex,
                "Pending retail row lost its live shop-use owner.");
            ShopUseCartLine pendingLine = witness.pendingUse.cart[witness.pending.shopUseCartIndex];
            witness.stockPending = StockCount(shop);
            witness.walletPending = shopping.HoldingMoney;
            witness.balancePending = account.Balance;
            witness.purchasesPending = ReadPurchases().Count;
            witness.probeReservations = probe.ReservationCount(
                MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult);
            witness.probeCommitAttempts = probe.CommitAttemptCount(
                MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult);
            witness.probeCommits = probe.CommitCount(
                MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult);
            RetailStockLotSnapshot lastLot = shopping.LastCommittedPurchaseLotForDiagnostics;
            bool revenueAbsent = !moneyQuery.TryGetSuccessfulTransaction(
                EconomyTransactionKind.GuestServiceIncome,
                "retail-revenue:" + witness.operationId,
                out _);
            witness.pendingExact = witness.pending.phase == RetailPurchaseRecoveryPhase.PhysicalCommitted
                && witness.pending.buyerId == report.actorId
                && witness.pending.facilityId == report.facilityId
                && witness.pending.shopUseOperationId == witness.shopUseOperationId
                && witness.pending.shopUseCartIndex == witness.pendingUse.purchaseIndex
                && witness.pending.purchaseCost == witness.price
                && pendingLine.pendingPurchaseOperationId == witness.operationId
                && !pendingLine.Committed && pendingLine.feedbackRemainingSeconds == 0f
                && witness.stockPending == witness.stockBefore - witness.pending.lot.quantity
                && witness.walletPending == witness.walletBefore - witness.pending.purchaseCost
                && witness.balancePending == witness.balanceBefore
                && witness.purchasesPending == witness.purchasesBefore
                && witness.probeReservations == 1 && witness.probeCommitAttempts == 1
                && witness.probeCommits == 0 && revenueAbsent
                && witness.physicalInstanceRemovedPending
                && shopping.CommittedPurchaseCountForDiagnostics == committedCountBefore + 1
                && shopping.CommittedPurchaseMassGramsForDiagnostics == committedMassBefore
                    + witness.pending.lot.unitMassGrams * witness.pending.lot.quantity
                && JsonConvert.SerializeObject(lastLot) == JsonConvert.SerializeObject(witness.pending.lot);
            Require(witness.pendingExact,
                "Post-physical/pre-outcome retail state was not retained exactly once.");

            DungeonGameSaveData pendingSave = saves.Capture();
            witness.sections = pendingSave.sections.Count;
            Require(witness.sections == report.checkpoints[0].sections,
                "Pending purchase world omitted an official save section.");
            string pendingWorld = saves.ToJson(pendingSave);
            WriteNew("pending-purchase-world.json", pendingWorld);
            WriteNew("pending-purchase-observed.json",
                JsonConvert.SerializeObject(witness, Formatting.Indented));

            CharacterActor oldActor = actor;
            Shop oldShop = shop;
            report.phase = "pending-purchase-restore";
            Require(saves.TryRestore(saves.FromJson(pendingWorld), out restored),
                "Pending purchase world restore: " + string.Join(" | ", restored.Errors));
            timeScale.Scale = 0f;
            actor = Actor();
            shop = FindShop();
            shopping = actor.GetAbility<AbilityShopping>();
            PauseOthers(report.actorId);
            witness.freshActor = !ReferenceEquals(oldActor, actor);
            witness.freshShop = !ReferenceEquals(oldShop, shop);
            witness.retiredOldActor = !oldActor.gameObject.activeInHierarchy
                && oldActor.CurrentLifecycleState == CharacterLifecycleState.Despawned;
            witness.rebound = shopping.HasActiveShoppingRoutineForDiagnostics
                && shop.FindActiveShopUse(report.actorId) != null;
            deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline
                && (shopping.CapturePendingRetailPurchases().Count != 0
                    || ReadPurchases().Count != witness.purchasesBefore + 1))
                yield return null;

            List<GameplayOutcomeSnapshot> recoveredPurchases = ReadPurchases();
            GameplayOutcomeSnapshot recovered = recoveredPurchases.SingleOrDefault(value =>
                Fact(value, "retail-buyer-debit-operation-id") == witness.operationId);
            bool recoveredRevenue = moneyQuery.TryGetSuccessfulTransaction(
                EconomyTransactionKind.GuestServiceIncome,
                "retail-revenue:" + witness.operationId,
                out EconomyTransactionRecord recoveredReceipt);
            witness.stockExact = StockCount(shop) == witness.stockPending;
            witness.walletExact = shopping.HoldingMoney == witness.walletPending;
            witness.outcomeExact = recovered != null
                && recoveredPurchases.Count == witness.purchasesBefore + 1
                && Fact(recovered, "retail-physical-disposition") == "retail-terminal-sink-committed"
                && Fact(recovered, "retail-source-operation-id") == witness.sourceOperationId
                && Fact(recovered, "retail-item-instance-id") ==
                    (string.IsNullOrEmpty(witness.itemInstanceId) ? "none" : witness.itemInstanceId);
            witness.revenueExact = recoveredRevenue
                && recoveredReceipt.amount == witness.pending.purchaseCost
                && recoveredReceipt.targetId == report.actorId
                && account.Balance == witness.balanceBefore + witness.pending.purchaseCost;
            witness.physicalInstanceRemovedAfterRestore = !scope.Container
                .Resolve<ICombatEquipmentRuntime>()
                .TryGetInstance(witness.itemInstanceId, out _);
            witness.recoveryExact = witness.freshActor && witness.freshShop
                && witness.retiredOldActor && witness.rebound
                && shopping.CapturePendingRetailPurchases().Count == 0
                && witness.stockExact && witness.walletExact
                && witness.outcomeExact && witness.revenueExact
                && witness.physicalInstanceRemovedAfterRestore;
            Require(witness.recoveryExact,
                "Fresh actor/shop recovery repeated or lost the retail sink, debit, outcome, or revenue.");

            report.phase = "pending-purchase-complete";
            timeScale.Scale = 1f;
            deadline = Time.realtimeSinceStartup + 35f;
            while (shop.FindActiveShopUse(report.actorId) != null
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            timeScale.Scale = 0f;
            Require(shop.FindActiveShopUse(report.actorId) == null,
                "Recovered retail use did not reach its real terminal state.");

            List<GameplayOutcomeSnapshot> allPurchases = ReadPurchases();
            List<GameplayOutcomeSnapshot> branchPurchases = allPurchases.Where(value =>
                !purchaseKeysBefore.Contains(Fact(value, "retail-purchase-result-key"))).ToList();
            witness.purchases = branchPurchases;
            witness.stockAfter = StockCount(shop);
            witness.walletAfter = shopping.HoldingMoney;
            witness.balanceAfter = account.Balance;
            witness.purchasesAfter = allPurchases.Count;
            witness.finalQuantity = (int)branchPurchases.Sum(value => Metric(value, "quantity"));
            witness.finalCost = (int)branchPurchases.Sum(value => Metric(value, "amount"));
            string[] operations = branchPurchases.Select(value =>
                Fact(value, "retail-buyer-debit-operation-id")).ToArray();
            foreach (GameplayOutcomeSnapshot purchase in branchPurchases)
            {
                string operation = Fact(purchase, "retail-buyer-debit-operation-id");
                Require(moneyQuery.TryGetSuccessfulTransaction(
                    EconomyTransactionKind.GuestServiceIncome,
                    "retail-revenue:" + operation,
                    out EconomyTransactionRecord receipt),
                    "Recovered branch is missing exact retail revenue: " + operation);
                Require(receipt.amount == (int)Metric(purchase, "amount")
                    && receipt.targetId == report.actorId,
                    "Recovered branch has mismatched retail revenue: " + operation);
                witness.revenues.Add(receipt.Clone());
            }
            witness.completedOnce = branchPurchases.Count > 0
                && operations.Distinct(StringComparer.Ordinal).Count() == operations.Length
                && operations.Count(value => value == witness.operationId) == 1
                && witness.stockBefore - witness.stockAfter == witness.finalQuantity
                && witness.walletBefore - witness.walletAfter == witness.finalCost
                && witness.balanceAfter - witness.balanceBefore == witness.finalCost
                && witness.purchasesAfter - witness.purchasesBefore == branchPurchases.Count
                && shopping.CapturePendingRetailPurchases().Count == 0;
            witness.sessionCompleted = sessions.TryGetSessionSnapshot(
                    witness.serviceSessionId, out ServiceSessionSnapshot terminal)
                && terminal.Stage == ServiceSessionStage.Completed
                && !string.IsNullOrEmpty(terminal.TerminalPublicationKey);
            witness.completedSession = terminal;
            Require(witness.completedOnce && witness.sessionCompleted,
                "Recovered retail use did not complete every cart line exactly once.");

            string completedStock = JsonConvert.SerializeObject(shop.CreateStockSnapshot());
            string completedSessions = JsonConvert.SerializeObject(sessions.Capture());
            string completedPurchases = JsonConvert.SerializeObject(allPurchases);
            string completedWorld = saves.ToJson(saves.Capture());
            WriteNew("pending-purchase-completed-world.json", completedWorld);
            oldActor = actor;
            oldShop = shop;
            Require(saves.TryRestore(saves.FromJson(completedWorld), out restored),
                "Completed pending purchase world restore: " + string.Join(" | ", restored.Errors));
            timeScale.Scale = 0f;
            PauseOthers(null);
            yield return null;
            yield return null;
            actor = Actor();
            shop = FindShop();
            shopping = actor.GetAbility<AbilityShopping>();
            witness.completedRestoreExact = !ReferenceEquals(oldActor, actor)
                && !ReferenceEquals(oldShop, shop)
                && !oldActor.gameObject.activeInHierarchy
                && oldActor.CurrentLifecycleState == CharacterLifecycleState.Despawned
                && shop.FindActiveShopUse(report.actorId) == null
                && shopping.CapturePendingRetailPurchases().Count == 0
                && shopping.HoldingMoney == witness.walletAfter
                && account.Balance == witness.balanceAfter
                && completedStock == JsonConvert.SerializeObject(shop.CreateStockSnapshot())
                && completedSessions == JsonConvert.SerializeObject(sessions.Capture())
                && completedPurchases == JsonConvert.SerializeObject(ReadPurchases());
            foreach (EconomyTransactionRecord expected in witness.revenues)
                witness.completedRestoreExact &= moneyQuery.TryGetSuccessfulTransaction(
                    expected.kind, expected.sourceId, out EconomyTransactionRecord actual)
                    && JsonConvert.SerializeObject(expected) == JsonConvert.SerializeObject(actual);
            witness.physicalInstanceRemovedTerminal = !scope.Container
                .Resolve<ICombatEquipmentRuntime>()
                .TryGetInstance(witness.itemInstanceId, out _);
            witness.completedRestoreExact &= witness.physicalInstanceRemovedTerminal;
            witness.noReplay = ReadPurchases().Count(value =>
                Fact(value, "retail-buyer-debit-operation-id") == witness.operationId) == 1
                && moneyQuery.TryGetSuccessfulTransaction(
                    EconomyTransactionKind.GuestServiceIncome,
                    "retail-revenue:" + witness.operationId,
                    out EconomyTransactionRecord restoredReceipt)
                && JsonConvert.SerializeObject(restoredReceipt)
                    == JsonConvert.SerializeObject(recoveredReceipt);
            witness.passed = witness.expectedFaultObserved && witness.pendingExact
                && witness.recoveryExact && witness.completedOnce
                && witness.sessionCompleted && witness.completedRestoreExact
                && witness.noReplay;
            WriteNew("pending-purchase-completed-restored-world.json", saves.ToJson(saves.Capture()));
            WriteNew("pending-purchase-witness.json",
                JsonConvert.SerializeObject(witness, Formatting.Indented));
            Require(witness.passed,
                "Pending retail purchase continuation replayed or lost an authoritative effect.");
    }

    private IEnumerator VerifyTerminationBranches()
    {
        // Rewind explicit test branches, never present these mutually exclusive
        // terminations as one natural play history or training source.
        foreach (string checkpoint in new[] { "checkout", "purchase-feedback" })
        foreach (string caseId in new[] { "cancel-action", "disable-shopping", "reject-demolition" })
        {
            report.phase = "termination-" + checkpoint + "-" + caseId;
            string prefix = report.phase;
            Require(saves.TryRestore(saves.FromJson(checkpoint == "checkout" ? checkoutWorld : feedbackWorld),
                out DungeonGameRestoreReport restored), "Termination checkpoint restore: " + string.Join(" | ", restored.Errors));
            timeScale.Scale = 0f;
            CharacterActor actor = Actor(); Shop shop = FindShop();
            if (caseId == "reject-demolition")
            {
                VerifyDemolitionRejection(prefix, actor, shop);
                continue;
            }
            AbilityShopping shopping = actor.GetAbility<AbilityShopping>();
            var account = scope.Container.Resolve<IGameMoneyAccount>();
            int wallet = shopping.HoldingMoney, balance = account.Balance;
            string stock = JsonConvert.SerializeObject(shop.CreateStockSnapshot());
            List<GameplayOutcomeSnapshot> priorPurchases = ReadPurchases();
            string purchases = JsonConvert.SerializeObject(priorPurchases);
            Require(shopping.HasActiveShoppingRoutineForDiagnostics && shop.FindActiveShopUse(report.actorId) != null,
                "Termination fixture is not a resumed active shop use.");
            Require(shopping.CapturePendingRetailPurchases().Count == 0,
                "These terminal branches do not cover unsettled purchase faults.");
            if (caseId == "cancel-action")
                Require(actor.Brain.StopCurrentActionForReplan("qa-shop-use-cancel"), "Public shop action cancellation rejected.");
            else shopping.enabled = false;

            var witness = new Termination
            {
                checkpoint = checkpoint, caseId = caseId,
                purchaseCountAtInterruption = priorPurchases.Count,
                stopped = !shopping.HasActiveShoppingRoutineForDiagnostics
                    && shop.FindActiveShopUse(report.actorId) == null
                    && !sessions.ActiveSessions.Any(value => value.SessionId == report.serviceSessionId),
                walletExact = wallet == shopping.HoldingMoney,
                balanceExact = balance == account.Balance,
                purchasesExact = purchases == JsonConvert.SerializeObject(ReadPurchases()),
                stockExact = stock == JsonConvert.SerializeObject(shop.CreateStockSnapshot())
            };
            var outcomes = scope.Container.Resolve<IGameplayOutcomeQuery>();
            witness.cancelledTerminal = sessions.TryGetSessionSnapshot(report.serviceSessionId, out ServiceSessionSnapshot terminal)
                && terminal.Stage == ServiceSessionStage.Cancelled && !terminal.PaymentCommitted
                && terminal.TerminalPhase == ServiceSessionTerminalPhase.Acknowledged
                && terminal.ActorId == report.actorId && terminal.HubId == report.facilityId
                && !string.IsNullOrEmpty(terminal.TerminalPublicationId);
            if (witness.cancelledTerminal)
            {
                witness.session = terminal;
                witness.cancelledTerminal = TryReadTerminal(outcomes, terminal, out GameplayOutcomeSnapshot result)
                    && result.outcomeTypeId == ServiceSessionOutcomeIds.Terminal.Value
                    && result.status == GameplayOutcomeStatus.Cancelled
                    && Fact(result, ServiceSessionOutcomeIds.SessionIdFact.Value) == report.serviceSessionId;
                witness.outcome = result;
            }
            string terminalJson = JsonConvert.SerializeObject(witness.session);
            string outcomeJson = JsonConvert.SerializeObject(witness.outcome);
            shopping.StopShopping("qa-repeat-shop-cleanup");
            witness.repeatedCleanupExact = sessions.TryGetSessionSnapshot(report.serviceSessionId, out ServiceSessionSnapshot repeated)
                && terminalJson == JsonConvert.SerializeObject(repeated)
                && witness.cancelledTerminal
                && TryReadTerminal(outcomes, repeated, out GameplayOutcomeSnapshot repeatedResult)
                && outcomeJson == JsonConvert.SerializeObject(repeatedResult)
                && purchases == JsonConvert.SerializeObject(ReadPurchases());
            report.terminations.Add(witness);
            WriteNew(prefix + "-observed.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
            Require(witness.stopped && witness.walletExact && witness.balanceExact && witness.purchasesExact
                && witness.stockExact && witness.cancelledTerminal && witness.repeatedCleanupExact,
                "Termination lost/repeated retail effects or retained its execution: " + prefix);
            DungeonGameSaveData terminalSave = saves.Capture();
            ServiceRoomsSaveData serviceSave = DungeonSaveJson.Deserialize<ServiceRoomsSaveData>(
                terminalSave.sections.Single(value => value.sectionId == "service.rooms").payloadJson);
            // Acknowledged terminal sessions are a retired execution outbox,
            // intentionally excluded by ServiceSessionAggregate.Capture. The
            // exact outcome remains authoritative in the persistent ledger.
            witness.acknowledgedSessionExcluded = !serviceSave.sessions.Any(value => value.sessionId == report.serviceSessionId);
            Require(witness.acknowledgedSessionExcluded, "Acknowledged service execution was unexpectedly persisted.");
            string terminalWorld = saves.ToJson(terminalSave);
            WriteNew(prefix + "-world.json", terminalWorld);
            Require(saves.TryRestore(saves.FromJson(terminalWorld), out restored),
                "Terminated shop world restore: " + prefix + "; " + string.Join(" | ", restored.Errors));
            timeScale.Scale = 0f;
            PauseOthers(null);
            yield return null;
            yield return null;
            bool shopStateExact = FindShop().FindActiveShopUse(report.actorId) == null
                && stock == JsonConvert.SerializeObject(FindShop().CreateStockSnapshot());
            witness.walletBeforeRestore = wallet;
            witness.walletAfterRestore = Actor().GetAbility<AbilityShopping>().HoldingMoney;
            witness.balanceBeforeRestore = balance;
            witness.balanceAfterRestore = account.Balance;
            witness.restoredScale = timeScale.Scale;
            witness.restoredStockExact = shopStateExact;
            witness.restoredWalletExact = witness.walletAfterRestore == wallet;
            witness.restoredBalanceExact = witness.balanceAfterRestore == balance;
            witness.restoredPurchasesExact = purchases == JsonConvert.SerializeObject(ReadPurchases());
            witness.restoredReceiptEmpty = Actor().GetAbility<AbilityShopping>().CapturePendingRetailPurchases().Count == 0;
            bool foundSession = sessions.TryGetSessionSnapshot(report.serviceSessionId, out witness.restoredSession);
            witness.restoredSessionRetired = !foundSession;
            witness.restoredOutcomeExact = TryReadTerminal(outcomes, witness.session, out witness.restoredOutcome)
                && outcomeJson == JsonConvert.SerializeObject(witness.restoredOutcome);
            witness.restoredExact = witness.restoredStockExact && witness.restoredWalletExact && witness.restoredBalanceExact
                && witness.restoredPurchasesExact && witness.restoredReceiptEmpty && witness.restoredSessionRetired && witness.restoredOutcomeExact;
            WriteNew(prefix + "-restored.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
            if (!witness.restoredExact)
            {
                WriteNew(prefix + "-changed-world.json", saves.ToJson(saves.Capture()));
                WriteNew(prefix + "-stock-before.json", stock);
                WriteNew(prefix + "-stock-after.json", JsonConvert.SerializeObject(FindShop().CreateStockSnapshot()));
            }
            Require(witness.restoredExact, "Terminated shop use revived or changed its effects: " + prefix);
        }

        // The final rejection branch deliberately leaves the restored shop use
        // active. Retire it while the restored service clock is still bound to
        // the live runtime. If PlayMode teardown destroys the visitor after
        // Time.time resets, the production release path correctly rejects that
        // reversed clock instead of silently inventing a cancellation time.
        // Keeping this cleanup inside the witness also makes those teardown
        // exceptions observable by the runner's Console capture.
        report.phase = "termination-fixture-cleanup";
        CharacterActor cleanupActor = Actor();
        Shop cleanupShop = FindShop();
        AbilityShopping cleanupShopping = cleanupActor.GetAbility<AbilityShopping>();
        Require(cleanupShopping.HasActiveShoppingRoutineForDiagnostics
            && cleanupShop.FindActiveShopUse(report.actorId) != null,
            "Final shop fixture did not retain the expected active use for cleanup.");
        Require(cleanupActor.Brain.StopCurrentActionForReplan(
                "qa-shop-verifier-cleanup"),
            "Final shop fixture action cleanup was rejected.");
        Require(!cleanupShopping.HasActiveShoppingRoutineForDiagnostics
            && cleanupShop.FindActiveShopUse(report.actorId) == null
            && !sessions.ActiveSessions.Any(value =>
                value.ActorId == report.actorId
                && value.HubId == report.facilityId),
            "Final shop fixture retained transient execution ownership.");
    }

    private void VerifyDemolitionRejection(string prefix, CharacterActor actor, Shop shop)
    {
        DungeonGameSaveData before = saves.Capture();
        AIAction action = actor.Brain.bestAction;
        string execution = JsonConvert.SerializeObject(shop.FindActiveShopUse(report.actorId));
        // Authored S01 is not structurally destructible. A raw teardown/loss
        // call would invent a capability absent from its gameplay definition.
        bool structurallyDestructible = scope.Container.Resolve<IBuildingStructuralIntegrityRuntime>().TryGet(shop, out _);
        bool accepted = scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller.TryDestroyBuilding(shop, out string reason);
        DungeonGameSaveData after = saves.Capture();
        string[] changed = before.sections.Where(value => !after.sections.Any(other => other.sectionId == value.sectionId
            && other.sectionVersion == value.sectionVersion && other.payloadJson == value.payloadJson && other.optional == value.optional))
            .Select(value => value.sectionId).ToArray();
        bool reasonMatched = reason?.Contains("재고·고객·직원·보충", StringComparison.Ordinal) == true;
        bool executionExact = ReferenceEquals(actor, Actor()) && ReferenceEquals(shop, FindShop()) && !shop.isDestroy
            && ReferenceEquals(action, actor.Brain.bestAction) && actor.GetAbility<AbilityShopping>().HasActiveShoppingRoutineForDiagnostics
            && execution == JsonConvert.SerializeObject(shop.FindActiveShopUse(report.actorId));
        var witness = new { prefix, accepted, reason, reasonMatched, structurallyDestructible,
            sections = before.sections.Count, changed, executionExact };
        report.rejectedDemolitions.Add(witness);
        WriteNew(prefix + "-witness.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
        Require(!accepted && reasonMatched && !structurallyDestructible && changed.Length == 0
            && before.sections.Count == after.sections.Count && executionExact,
            "Occupied shop demolition was not rejected without changing the live world: " + prefix);
    }

    private static bool TryReadTerminal(IGameplayOutcomeQuery outcomes, ServiceSessionSnapshot session,
        out GameplayOutcomeSnapshot result)
    {
        GameplayOutcomeSnapshot[] matches = outcomes.GetForOperation(new GameplayOperationId(session.SessionId)).Items
            .Where(value => value.Exact?.outcomeTypeId == ServiceSessionOutcomeIds.Terminal.Value)
            .Select(value => value.Exact).ToArray();
        result = matches.Length == 1 ? matches[0] : null;
        return result != null && new GameplayOutcomeId(new GameplayOutcomeRunId(result.runId), result.sequence).ToString()
            == session.TerminalPublicationId;
    }

    private void VerifyVisitorRejections(string originalJson, CharacterActor actor, Shop shop)
    {
        foreach (string caseId in new[] { "visitor-not-visiting", "visitor-wrong-definition", "visitor-traversal-unowned", "visitor-profile-missing" })
        {
            DungeonGameSaveData before = saves.Capture();
            AIAction action = actor.Brain.bestAction;
            DungeonGameSaveData candidate = saves.FromJson(originalJson);
            DungeonSaveSectionEnvelope section = candidate.sections.Single(value => value.sectionId == CharacterWorldSaveSection.Id);
            var characters = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(section.payloadJson);
            WorldCharacterProfile profile = characters.populationProfiles.Single(value => value.persistentId == report.actorId);
            DungeonCharacterSaveData savedActor = characters.actors.Single(value => value.persistentId == report.actorId);
            string expected = "Visitor population/actor owner mismatch";
            if (caseId == "visitor-not-visiting") profile.isVisiting = false;
            else if (caseId == "visitor-wrong-definition") profile.characterDataId = -1234;
            else if (caseId == "visitor-profile-missing")
            { characters.populationProfiles.Remove(profile); expected = "not owned by the visiting population"; }
            else { savedActor.lifecycleState = CharacterLifecycleState.EnteringDungeon; expected = "traversal continuation is not yet supported"; }
            section.payloadJson = DungeonCharacterWorldSaveCodec.Serialize(characters);
            candidate.manifest = DungeonSaveManifest.Capture(candidate.sections);
            WriteNew("rejection-" + caseId + "-input.json", saves.ToJson(candidate));
            bool accepted = saves.TryRestore(candidate, out DungeonGameRestoreReport rejected);
            DungeonGameSaveData after = saves.Capture();
            string reason = string.Join(" | ", rejected.Errors);
            string[] changed = before.sections.Where(value => !after.sections.Any(other => other.sectionId == value.sectionId
                && other.sectionVersion == value.sectionVersion && other.payloadJson == value.payloadJson && other.optional == value.optional))
                .Select(value => value.sectionId).ToArray();
            bool referencesUnchanged = ReferenceEquals(Actor(), actor) && ReferenceEquals(FindShop(), shop)
                && ReferenceEquals(actor.Brain.bestAction, action) && actor.GetAbility<AbilityShopping>().HasActiveShoppingRoutineForDiagnostics;
            var candidates = scope.Container.Resolve<IRestoreWorldCandidateQuery>();
            bool candidateCleared = !candidates.TryGetCharacters(out _)
                && candidates is IRestoreVisitingPopulationCandidateQuery visitorCandidates
                && !visitorCandidates.TryGetVisitingPopulationIds(out _);
            var witness = new { caseId, accepted, reason, reasonMatched = reason.Contains(expected, StringComparison.Ordinal),
                sections = before.sections.Count, changed, referencesUnchanged, candidateCleared };
            report.rejectedRestores.Add(witness);
            WriteNew("rejection-" + caseId + "-witness.json", JsonConvert.SerializeObject(witness, Formatting.Indented));
            Require(!accepted && witness.reasonMatched && changed.Length == 0 && before.sections.Count == after.sections.Count
                && referencesUnchanged && candidateCleared, "Visitor restore rejection changed live state or failed for an unrelated reason: " + caseId);
        }
    }

    private Shop PrepareShop(Grid grid)
    {
        BuildingSO definition = Resources.Load<BuildingSO>("SO/Building/Modular/S01_판매카운터");
        Require(definition != null, "Authored S01 missing.");
        Shop existing = world.Buildings.OfType<Shop>().FirstOrDefault(value => value != null && value.BuildingData == definition);
        if (existing != null) return existing;
        RoomLayout layout = scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid);
        foreach (RoomInstance room in layout.Rooms.Where(value => value != null && value.IsUsable).OrderByDescending(value => value.Cells.Count))
        {
            var cells = new HashSet<Vector2Int>(room.Cells);
            foreach (Vector2Int cell in room.Cells.OrderBy(value => value.x).ThenBy(value => value.y))
            {
                if (definition.GetGridPosList(cell).Any(position => !cells.Contains(position) || grid.GetGridCell(position) == null
                    || !grid.GetGridCell(position).CanBuildInArea(definition) || grid.GetGridCell(position).HasOccupantInLayer(definition.Placement.Layer))) continue;
                Require(scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller.TryPlaceInitialBuildings(
                    new[] { new InitialBuildInfo { Building = definition, Position = cell } }, out string failure), failure);
                scope.Container.Resolve<IRoomLayoutCache>().Clear();
                return world.Buildings.OfType<Shop>().Single(value => value != null && value.BuildingData == definition && value.buildPoses.Contains(cell));
            }
        }
        throw new InvalidOperationException("No usable room permits authored shop placement.");
    }
    private void PauseOthers(string except)
    {
        foreach (CharacterActor actor in world.Characters.Where(value => value != null && value.Identity.PersistentId != except))
        { actor.Brain?.StopCurrentActionForReplan("qa-shop-controlled-visitor"); actor.SetAiPaused(true); }
    }
    private CharacterActor Actor() => world.Characters.Single(value => value != null && value.Identity.PersistentId == report.actorId);
    private Shop FindShop() => world.Buildings.OfType<Shop>().Single(value => value != null && value.RequirePersistentInstanceId().Value == report.facilityId);
    private static int StockCount(Shop shop) => shop.CreateStockSnapshot().lots.Sum(value => value.quantity);
    private static string Describe(CharacterActor actor, Shop shop) => JsonConvert.SerializeObject(new {
        lifecycle = actor.CurrentLifecycleState.ToString(), outcome = actor.GetAbility<AbilityShopping>().LastVisitOutcome.ToString(),
        action = actor.Brain.bestAction?.actionset?.GetType().Name, use = shop.FindActiveShopUse(actor.Identity.PersistentId) });
    private List<GameplayOutcomeSnapshot> ReadPurchases()
    {
        var results = new List<GameplayOutcomeSnapshot>();
        var query = scope.Container.Resolve<IGameplayOutcomeQuery>();
        OutcomeCursor cursor = OutcomeCursor.FirstPage(200);
        while (true)
        {
            GameplayOutcomeQueryPage page = query.GetGlobal(cursor, OutcomeFilter.All);
            if (page.Items.Count == 0) break;
            results.AddRange(page.Items.Where(value => value.Exact != null && value.Exact.facts.Any(fact => fact.factId == "retail-purchase-result-key")
                && value.Exact.participants.Any(part => part.entityId == report.actorId)
                && value.Exact.participants.Any(part => part.entityId == report.facilityId)).Select(value => value.Exact));
            cursor = page.NextCursor;
        }
        return results.OrderBy(value => Fact(value, "retail-purchase-result-key"), StringComparer.Ordinal).ToList();
    }
    private static string Fact(GameplayOutcomeSnapshot outcome, string id) => outcome.facts.Single(value => value.factId == id).value;
    private static double Metric(GameplayOutcomeSnapshot outcome, string id) => outcome.metrics.Single(value => value.metricId == id).value;
    private void WriteNew(string name, string contents)
    {
        using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contents);
    }
    private void CaptureLog(string text, string stack, LogType type)
    {
        if (type == LogType.Warning) report.warnings.Add(text);
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            // Unity reports the inner exception as text and the aggregate
            // rethrow in stack. Match the exact injected boundary across both.
            string detail = text + "\n" + stack;
            if (expectCheckoutCleanupFault && type == LogType.Exception
                && detail.Contains("AggregateException: Shop checkout failed and terminal cleanup remains pending.", StringComparison.Ordinal)
                && detail.Contains("Shoplifting outcome reservation failed: migrated-producer-e2e-injected-reservation-rejection", StringComparison.Ordinal)
                && detail.Contains(CheckoutCleanupFaultRegistry.FailureMessage, StringComparison.Ordinal)
                && detail.Contains("ShopCustomerInteractionService", StringComparison.Ordinal))
            {
                expectCheckoutCleanupFault = false;
                checkoutCleanupFaultCount++;
                timeScale.Scale = 0f;
                report.expectedFaults.Add(detail);
                return;
            }
            if (expectPendingPurchaseFault
                && text?.Contains(ExpectedPendingException, StringComparison.Ordinal) == true)
            {
                expectPendingPurchaseFault = false;
                expectedPendingPurchaseFaultCount++;
                report.expectedFaults.Add(text);
                return;
            }
            report.errors.Add(text + "\n" + stack);
        }
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
}
#endif
