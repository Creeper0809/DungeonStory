#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class WarehouseFeatureCommandServiceT05DebugScenarios
{
    public static void RunOrThrow() => RunAll();

    public static void RunObserverBatch()
    {
        RunAll();
        Debug.Log("B116_STOCK_SUPPLY_COMMANDS=PASS");
        VerifyUnknownDelivery(false);
        VerifyUnknownDelivery(true);
        VerifyRealPhysicalDeliveryAndFreshOwnerRestore(new JArray());
        Debug.Log("B116_STOCK_SUPPLY_PHYSICAL_RESTORE=PASS");
        MigratedProducerEconomyEntryExtendedE2EDebugScenarios.RunStockSupplyObserverFocused();
    }

    private static void VerifyUnknownDelivery(bool afterCommit)
    {
        SupplyBundle saved;
        using (var original = new DeliveryScenario(4, true))
        {
            original.Transport.UnknownBefore = !afterCommit;
            original.Transport.UnknownAfter = afterCommit;
            original.Transport.BlockRecovery = true;
            bool checkedFence = false;
            original.Transport.DuringPublish = () =>
            {
                Reject(() => original.Journal.Capture());
                Require(!original.Commands.PurchaseDelivery(original.Offer).Succeeded,
                    "Reentrant supply command entered the active owner.");
                checkedFence = true;
            };
            Require(original.Commands.PurchaseDelivery(original.Offer).Succeeded && checkedFence,
                "Unknown acknowledgement rolled back a completed delivery.");
            Require(original.Money.Balance == 60 && original.Runtime.TotalQuantity == 4
                && ExactOutcomes(original).Count == (afterCommit ? 1 : 0), "Unknown delivery owner mismatch.");
            Require(!original.Commands.PurchaseDelivery(original.Offer).Succeeded
                && original.Money.Balance == 60 && original.Runtime.TotalQuantity == 4,
                "Pending delivery allowed a second purchase before recovery.");
            saved = original.Capture();
            Require(saved.owner.pending.Count == 1 && saved.owner.nextSequence == 2, "Pending receipt was not saved.");
            StockSupplyFactPreflight.ValidatePending(saved.owner, saved.ledger, saved.counters);
            var invalid = RoundTrip(saved);
            invalid.owner.pending[0].publicationKey += ":tampered";
            Reject(() => StockSupplyFactPreflight.ValidatePending(invalid.owner, invalid.ledger, invalid.counters));
            if (afterCommit)
            {
                invalid = RoundTrip(saved);
                invalid.owner.pending[0].fact.result.reason = "changed after commit";
                Reject(() => StockSupplyFactPreflight.ValidatePending(invalid.owner, invalid.ledger, invalid.counters));
                invalid = RoundTrip(saved);
                invalid.owner.pending[0].fact.itemId = "material:other";
                Reject(() => StockSupplyFactPreflight.ValidatePending(invalid.owner, invalid.ledger, invalid.counters));
            }
            var treasury = new TreasuryEconomySaveData { warehouseSupply = saved.owner };
            TreasuryEconomySaveValidation.Validate(treasury);
            string raw = DungeonCharacterWorldSaveCodec.SerializeFields(treasury);
            TreasuryEconomySaveSection.ValidateObservationJson(raw);
            var missing = JObject.Parse(raw);
            missing.Remove("warehouseSupply");
            Reject(() => TreasuryEconomySaveSection.ValidateObservationJson(missing.ToString()));
            var malformed = JObject.Parse(raw);
            ((JObject)malformed["warehouseSupply"]["pending"][0]["fact"]["result"]).Remove("netCost");
            Reject(() => TreasuryEconomySaveSection.ValidateObservationJson(malformed.ToString()));
            treasury.version--;
            Reject(() => TreasuryEconomySaveValidation.Validate(treasury));
            var copy = original.Treasury.Current.Copy();
            copy.WarehouseSupply.pending.Clear();
            Require(original.Journal.Capture().pending.Count == 1, "Treasury snapshot aliases its pending source.");
        }
        using (var restored = new DeliveryScenario(4, true, saved: saved))
        {
            Require(restored.Journal.RecoverPending(out string failure), "Fresh supply replay failed: " + failure);
            Require(restored.Money.Balance == 60 && restored.Runtime.TotalQuantity == 4
                && restored.Money.SuccessfulKinds.Count == 0 && ExactOutcomes(restored).Count == 1,
                "Fact recovery repeated money or physical delivery.");
            string before = JsonUtility.ToJson(restored.Capture());
            Require(restored.Journal.RecoverPending(out _) && before == JsonUtility.ToJson(restored.Capture()),
                "Duplicate recovery changed the completed owner.");
            var rewound = restored.Capture();
            rewound.owner.nextSequence = 1;
            Reject(() => StockSupplyFactPreflight.ValidatePending(rewound.owner, rewound.ledger, rewound.counters));
            Require(restored.Commands.PurchaseDelivery(restored.Offer).Succeeded
                && restored.Money.Balance == 20 && restored.Runtime.TotalQuantity == 8
                && ExactOutcomes(restored).Count == 2 && restored.Journal.Capture().nextSequence == 3,
                "Fresh repeated same-offer purchase did not get its own identity.");
            Require(restored.Runtime.Capture().stacks.Select(row => row.stackId).Distinct().Count() == 2,
                "Physical fixture reused a restored stack identity.");
            Require(ExactOutcomes(restored).Select(row => row.operationId).Distinct().Count() == 2,
                "Repeated purchase reused a native operation identity.");
        }
        Debug.Log(afterCommit ? "B116_STOCK_SUPPLY_ACK_AFTER=PASS" : "B116_STOCK_SUPPLY_ACK_BEFORE=PASS");
    }

    [Serializable]
    private sealed class SupplyBundle
    {
        public int money, day;
        public StockSupplyFactSaveData owner;
        public DungeonPhysicalItemSaveData physical;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
    }
    private static T RoundTrip<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("B116 expected an explicit boundary rejection.");
    }

    private sealed class SupplyTransport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal bool UnknownBefore, UnknownAfter, BlockRecovery;
        internal Action DuringPublish;
        internal SupplyTransport(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T value, out DomainFactLease lease, out string failure) => inner.TryReserve(value, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T value)
        {
            DuringPublish?.Invoke();
            if (UnknownBefore) return DomainFactDelivery.Uncertain("B116-controlled-unknown-before");
            var delivered = inner.Publish(lease, value);
            return UnknownAfter && delivered.MayBeCommitted ? DomainFactDelivery.Uncertain("B116-controlled-unknown-after") : delivered;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> values, DomainFactDelivery[] results, out string failure)
        {
            if (!BlockRecovery) return inner.ReplayBatch(values, results, out failure);
            failure = "B116-controlled-recovery-blocked";
            for (int index = 0; index < results.Length; index++) results[index] = DomainFactDelivery.Reject(failure);
            return false;
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> values, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(values, results, out failure);
        public DomainFactDelivery Replay<T>(in T value) => inner.Replay(value);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    public static bool RunAll(bool throwOnFailure = true)
        => RunAll(throwOnFailure, observations: null);

    private static bool RunAll(bool throwOnFailure, JArray observations)
    {
        try
        {
            VerifyFullDeliveryCommitsSucceededWithPhysicalSettlementEvidence(observations);
            VerifyPartialDeliveryCommitsPartialAndAllowsRetry(observations);
            VerifyZeroDeliveryCommitsFailedWithFullRefund(observations);
            VerifySpawnExceptionRefundsAndPublishesNoOutcome(observations);
            VerifyReservationRejectionRollsBackTheSameDeliveryTransaction(observations);
            VerifyCommitRejectionRollsBackTheSameDeliveryTransaction(observations);
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void RunFromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("T05 isolated evidence output is required.");
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        string directory = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string report = Path.Combine(directory, "warehouse-delivery-t05-report.json");
        if (!directory.StartsWith(qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) || File.Exists(report))
            throw new InvalidOperationException("T05 report must be create-only under Artifacts/QA.");

        var observations = new JArray();
        bool passed = false;
        string failureType = string.Empty;
        string failure = string.Empty;
        try
        {
            Require(RunAll(throwOnFailure: true, observations: observations),
                "Original T05 delivery scenarios did not pass.");
            VerifyRealPhysicalDeliveryAndFreshOwnerRestore(observations);
            passed = true;
        }
        catch (Exception exception)
        {
            failureType = exception.GetType().Name;
            failure = exception.Message;
            throw;
        }
        finally
        {
            Directory.CreateDirectory(directory);
            using var stream = new FileStream(report, FileMode.CreateNew,
                FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(new JObject
            {
                ["schema"] = "dungeonstory-t05-delivery-raw@2",
                ["passed"] = passed,
                ["failureType"] = failureType,
                ["failure"] = failure,
                ["observations"] = observations,
                ["controlledFixture"] = true,
                ["naturalGameplay"] = false,
                ["wholeWorldRestoreClaimed"] = false,
                ["coldProcessRestoreClaimed"] = false,
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            }.ToString(Formatting.Indented));
            writer.WriteLine();
        }
        Debug.Log("U0_T05_DELIVERY=PASS; report=" + report);
    }

    private static void Observe(JArray observations, string stage,
        DeliveryScenario scenario, bool? succeeded = null,
        string message = null, StockSupplyResult? exceptionResult = null)
    {
        if (observations == null)
            return;
        observations.Add(new JObject
        {
            ["stage"] = stage,
            ["scope"] = "controlled-physical-port-and-recording-money-original-fault-boundary",
            ["commandSucceeded"] = succeeded.HasValue ? new JValue(succeeded.Value) : JValue.CreateNull(),
            ["commandMessage"] = message ?? string.Empty,
            ["money"] = scenario.Money.Balance,
            ["moneyKinds"] = new JArray(scenario.Money.SuccessfulKinds.Select(kind => kind.ToString())),
            ["physicalItems"] = JObject.Parse(JsonUtility.ToJson(scenario.Runtime.Capture())),
            ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(scenario.Outcomes.Transaction.Capture())),
            ["outcomeLedger"] = JObject.FromObject(scenario.Outcomes.Ledger.CaptureGameplayOutcomes()),
            ["exceptionResult"] = exceptionResult.HasValue
                ? JObject.FromObject(exceptionResult.Value) : JValue.CreateNull()
        });
    }

    private static void VerifyRealPhysicalDeliveryAndFreshOwnerRestore(JArray observations)
    {
        const string ownerId = "owner:t05:real-physical-delivery";
        JObject checkpoint;
        JObject originalOutcome;
        object originalOwner;
        IReadOnlyList<IProductionCausalActionFixture> fixtures =
            StockSupplyTransformCausalProductionEntryDebugScenarios.CreateFixtures(
                "run:t05:real-physical-delivery", ownerId);
        try
        {
            IProductionCausalActionFixture supply = fixtures.Single(value =>
                value.ActionId == StockSupplyTransformCausalProductionEntryDebugScenarios.SupplyActionId);
            var owner = (IProductionCausalOwnerCheckpointFixture)supply;
            originalOwner = owner.OwnerRuntimeIdentity;
            ObserveRealDelivery(observations, "real-before-purchase", supply, owner, false);
            Require(supply.Execute(), "Real physical T05 warehouse delivery was rejected.");
            GameplayOutcomeReplayIdentity originalIdentity = supply.RequireExactOutcome();
            Require(originalIdentity.HasCanonicalPayloadHash,
                "Real physical T05 delivery has no canonical parent hash.");
            IReadOnlyDictionary<string, object> state = supply.CaptureState();
            Require((int)state["stockTransform.stage"] == 1
                && (int)state["stockTransform.money"] == 88
                && (int)state["stockTransform.sourceQuantity"] == 3
                && (int)state["stockTransform.exactOutcomeCount"] == 1,
                "Real physical T05 quantity, money or parent count diverged.");
            ObserveRealDelivery(observations, "real-delivered", supply, owner, true);
            checkpoint = JObject.Parse(owner.CaptureOwnerCheckpoint().ToString(Formatting.None));
            originalOutcome = RequireSingleSavedSupplyOutcome(checkpoint);
        }
        finally
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }

        fixtures = StockSupplyTransformCausalProductionEntryDebugScenarios.CreateFixtures(
            "run:t05:real-physical-restored", ownerId, checkpoint);
        try
        {
            IProductionCausalActionFixture supply = fixtures.Single(value =>
                value.ActionId == StockSupplyTransformCausalProductionEntryDebugScenarios.SupplyActionId);
            var owner = (IProductionCausalOwnerCheckpointFixture)supply;
            Require(!ReferenceEquals(originalOwner, owner.OwnerRuntimeIdentity),
                "T05 restore did not compose a fresh physical/financial owner.");
            JObject before = owner.CaptureOwnerCheckpoint();
            // Fresh restore intentionally clears execution-probe telemetry.
            // Compare the actual restored canonical DTO, not a new-commit counter.
            Require(JToken.DeepEquals(originalOutcome, RequireSingleSavedSupplyOutcome(before)),
                "Real T05 restore changed its exact canonical parent DTO or hash.");
            ObserveRealDelivery(observations, "real-restored-before-noop", supply, owner, true);
            Require(!supply.Execute(), "Restored real T05 delivery accepted a duplicate action.");
            JObject after = owner.CaptureOwnerCheckpoint();
            ObserveRealDelivery(observations, "real-restored-after-noop", supply, owner, true);
            Require(JToken.DeepEquals(before, after),
                "Restored real T05 no-op changed money, physical items or parent ledger.");
        }
        finally
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static void ObserveRealDelivery(JArray observations, string stage,
        IProductionCausalActionFixture supply,
        IProductionCausalOwnerCheckpointFixture owner, bool hasOutcome)
    {
        JObject checkpoint = owner.CaptureOwnerCheckpoint();
        observations.Add(new JObject
        {
            ["stage"] = stage,
            ["scope"] = "real-world-item-stack-runtime-and-fresh-causal-owner-same-process",
            ["state"] = JObject.FromObject(supply.CaptureState()),
            ["checkpoint"] = checkpoint,
            ["outcome"] = hasOutcome
                ? RequireSingleSavedSupplyOutcome(checkpoint).DeepClone() : JValue.CreateNull()
        });
    }

    private static JObject RequireSingleSavedSupplyOutcome(JObject checkpoint)
    {
        var ledger = checkpoint?["outcomeLedger"] as JObject;
        var exact = ledger?["exactOutcomes"] as JArray;
        var pending = ledger?["outbox"] as JArray;
        JObject parent = exact?.Count == 1 ? exact[0] as JObject : null;
        string hash = (string)parent?["immutablePayloadHash"];
        Require(parent != null && pending != null && pending.Count == 0
            && (string)parent["outcomeTypeId"] == MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.StockSupplyResult).OutcomeTypeId.Value
            && (int)parent["status"] == (int)GameplayOutcomeStatus.Succeeded
            && !string.IsNullOrWhiteSpace(hash) && hash.Length == 64,
            "Actual T05 checkpoint has no single canonical succeeded supply parent.");
        return parent;
    }

    private static void VerifyFullDeliveryCommitsSucceededWithPhysicalSettlementEvidence(JArray observations = null)
    {
        using var scenario = new DeliveryScenario(spawned: 4, spawnCompleted: true);

        WarehouseFeatureCommandResult result = scenario.Commands.PurchaseDelivery(
            scenario.Offer);

        Observe(observations, "full-delivery", scenario, result.Succeeded, result.Message);

        Require(result.Succeeded, "Full physical delivery did not report success.");
        Require(scenario.Money.Balance == 60
            && scenario.Runtime.TotalQuantity == 4
            && scenario.Money.SuccessfulKinds.SequenceEqual(new[]
            {
                EconomyTransactionKind.ShopPurchase
            }), "Full delivery money or physical state diverged.");
        RequireExact(
            scenario,
            expectedCount: 1,
            GameplayOutcomeStatus.Succeeded,
            "spawned=4",
            "spawnedStacks=t05-stack-1:4",
            "paid=40",
            "refunded=0",
            "netCost=40",
            "paymentTransaction=warehouse-delivery:1:payment");
    }

    private static void VerifyPartialDeliveryCommitsPartialAndAllowsRetry(JArray observations = null)
    {
        using var scenario = new DeliveryScenario(spawned: 2, spawnCompleted: false);

        WarehouseFeatureCommandResult partial = scenario.Commands.PurchaseDelivery(
            scenario.Offer);

        Observe(observations, "partial-delivery", scenario, partial.Succeeded, partial.Message);

        Require(!partial.Succeeded
            && scenario.Money.Balance == 80
            && scenario.Runtime.TotalQuantity == 2
            && scenario.Money.SuccessfulKinds.SequenceEqual(new[]
            {
                EconomyTransactionKind.ShopPurchase,
                EconomyTransactionKind.ShopPurchaseRefund
            }), "Partial delivery did not retain its physical item and proportional net cost.");
        RequireExact(
            scenario,
            expectedCount: 1,
            GameplayOutcomeStatus.PartiallySucceeded,
            "spawned=2",
            "spawnedStacks=t05-stack-1:2",
            "paid=40",
            "refunded=20",
            "netCost=20");

        DungeonMigratedProducerOutcomeSaveData savedOutcomeOwners =
            scenario.Outcomes.Transaction.Capture();
        Require(savedOutcomeOwners.owners.Count == 1
            && savedOutcomeOwners.owners[0].revision == 1L,
            "Partial delivery did not persist its committed outcome owner.");
        DungeonPhysicalItemSaveData savedPartial = scenario.Runtime.Capture();
        scenario.Runtime.Restore(savedPartial);
        Observe(observations, "partial-restored-before-new-purchase", scenario);
        scenario.Runtime.Configure(spawned: 4, spawnCompleted: true, throwOnSpawn: false);
        WarehouseFeatureCommandResult retry = scenario.Commands.PurchaseDelivery(
            scenario.Offer);

        Observe(observations, "partial-after-new-purchase", scenario, retry.Succeeded, retry.Message);

        Require(retry.Succeeded
            && scenario.Money.Balance == 40
            && scenario.Runtime.TotalQuantity == 6,
            "Retry after the saved partial delivery did not preserve the partial state.");
        RequireExactCountAndStatus(
            scenario,
            expectedCount: 2,
            expectedLastStatus: GameplayOutcomeStatus.Succeeded);
    }

    private static void VerifyZeroDeliveryCommitsFailedWithFullRefund(JArray observations = null)
    {
        using var scenario = new DeliveryScenario(spawned: 0, spawnCompleted: false);

        WarehouseFeatureCommandResult result = scenario.Commands.PurchaseDelivery(
            scenario.Offer);

        Observe(observations, "zero-delivery-refunded", scenario, result.Succeeded, result.Message);

        Require(!result.Succeeded
            && scenario.Money.Balance == 100
            && scenario.Runtime.TotalQuantity == 0,
            "Zero delivery changed money or physical stacks after its full refund.");
        RequireExact(
            scenario,
            expectedCount: 1,
            GameplayOutcomeStatus.Failed,
            "spawned=0",
            "paid=40",
            "refunded=40",
            "netCost=0");
    }

    private static void VerifySpawnExceptionRefundsAndPublishesNoOutcome(JArray observations = null)
    {
        using var scenario = new DeliveryScenario(
            spawned: 0,
            spawnCompleted: false,
            throwOnSpawn: true);

        try
        {
            scenario.Commands.PurchaseDelivery(scenario.Offer);
            throw new InvalidOperationException(
                "Spawn exception was converted into a normal warehouse result.");
        }
        catch (StockSupplyDeliveryException exception)
        {
            StockSupplyResult result = exception.Result;
            Observe(observations, "spawn-exception-refunded", scenario, exceptionResult: result);
            Require(result.paidAmount == 40
                && result.refundedAmount == 40
                && result.netCost == 0
                && result.spawnedStackQuantity == 0
                && result.exceptionType == nameof(InvalidOperationException),
                "Spawn exception did not retain its settlement result.");
        }

        Require(scenario.Money.Balance == 100
            && scenario.Runtime.TotalQuantity == 0
            && ExactOutcomes(scenario).Count == 0,
            "Spawn exception left money, physical stacks, or an outcome behind.");
    }

    private static void VerifyCommitRejectionRollsBackTheSameDeliveryTransaction(JArray observations = null)
    {
        using var scenario = new DeliveryScenario(
            spawned: 4,
            spawnCompleted: true,
            rejectCommit: true);

        WarehouseFeatureCommandResult result = scenario.Commands.PurchaseDelivery(
            scenario.Offer);

        Observe(observations, "commit-rejection-rolled-back", scenario, result.Succeeded, result.Message);

        Require(!result.Succeeded
            && scenario.Money.Balance == 100
            && scenario.Runtime.TotalQuantity == 0
            && ExactOutcomes(scenario).Count == 0,
            "Commit rejection did not exact-rollback the same delivery transaction.");
    }

    private static void VerifyReservationRejectionRollsBackTheSameDeliveryTransaction(JArray observations = null)
    {
        using var scenario = new DeliveryScenario(
            spawned: 4,
            spawnCompleted: true,
            rejectReservation: true);

        WarehouseFeatureCommandResult result = scenario.Commands.PurchaseDelivery(
            scenario.Offer);

        Observe(observations, "reservation-rejection-rolled-back", scenario, result.Succeeded, result.Message);

        Require(!result.Succeeded
            && scenario.Money.Balance == 100
            && scenario.Runtime.TotalQuantity == 0
            && ExactOutcomes(scenario).Count == 0,
            "Reservation rejection did not exact-rollback the same delivery transaction.");
    }

    private static void RequireExact(
        DeliveryScenario scenario,
        int expectedCount,
        GameplayOutcomeStatus expectedStatus,
        params string[] requiredSummaryFragments)
    {
        RequireExactCountAndStatus(scenario, expectedCount, expectedStatus);
        GameplayOutcomeSnapshot exact = ExactOutcomes(scenario).Last();
        string summary = exact.facts
            .Where(fact => string.Equals(
                fact.factId,
                MigratedProducerOutcomeIds.SummaryFact.Value,
                StringComparison.Ordinal))
            .Select(fact => fact.value)
            .SingleOrDefault() ?? string.Empty;
        Require(requiredSummaryFragments.All(fragment => summary.IndexOf(
                    fragment,
                    StringComparison.Ordinal) >= 0),
            "Stock supply exact outcome omitted physical or settlement evidence: "
            + summary);
    }

    private static void RequireExactCountAndStatus(
        DeliveryScenario scenario,
        int expectedCount,
        GameplayOutcomeStatus expectedLastStatus)
    {
        List<GameplayOutcomeSnapshot> exact = ExactOutcomes(scenario);
        Require(exact.Count == expectedCount
            && exact.Last().status == expectedLastStatus,
            "Stock supply outcome status did not match the completed delivery state.");
    }

    private static List<GameplayOutcomeSnapshot> ExactOutcomes(
        DeliveryScenario scenario) => scenario.Outcomes.Ledger
        .CaptureGameplayOutcomes()
        .exactOutcomes
        .Where(outcome => outcome != null
            && string.Equals(
                outcome.outcomeTypeId,
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.StockSupplyResult)
                    .OutcomeTypeId.Value,
                StringComparison.Ordinal))
        .OrderBy(outcome => outcome.sequence)
        .ToList();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class DeliveryScenario : IDisposable
    {
        private readonly SessionStore session;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly StockSupplyFactEditorFixture facts;

        internal DeliveryScenario(
            int spawned,
            bool spawnCompleted,
            bool throwOnSpawn = false,
            bool rejectReservation = false,
            bool rejectCommit = false,
            SupplyBundle saved = null)
        {
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:warehouse-t05-delivery");
            session = new SessionStore(new GameSessionState(
                startingMoney: saved?.money ?? 100,
                startingDay: saved?.day ?? 3));
            Money = new RecordingMoneyAccount(session.State);
            Runtime = DeliveryRuntimeProxy.Create(
                spawned,
                spawnCompleted,
                throwOnSpawn);
            probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind.StockSupplyResult,
                FaultMode = rejectReservation
                    ? MigratedProducerOutcomeFaultMode.RejectReservation
                    : rejectCommit
                        ? MigratedProducerOutcomeFaultMode.RejectCommit
                        : MigratedProducerOutcomeFaultMode.None
            };
            Treasury = new TreasuryEconomyAggregateStateStore(Outcomes.AggregateRootStore);
            facts = new StockSupplyFactEditorFixture(Outcomes, Treasury, probe);
            Transport = new SupplyTransport(facts.Dispatcher);
            Journal = new StockSupplyFactJournal(Treasury, Transport, Transport);
            if (saved != null)
            {
                StockSupplyFactPreflight.ValidatePending(saved.owner, saved.ledger, saved.counters);
                var candidate = Outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.ledger);
                Outcomes.Ledger.BeginRestoreCandidate(); Outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
                Outcomes.Ledger.PublishRestoreCandidate(); Outcomes.Ledger.CompleteRestoreCandidate();
                Outcomes.Transaction.Restore(Outcomes.Transaction.PrepareRestore(saved.counters));
                Treasury.Current.WarehouseSupply = StockSupplyFactSaveData.ValidateAndClone(saved.owner);
                Runtime.Restore(saved.physical);
            }
            Commands = new WarehouseFeatureCommandService(
                new WarehouseCommandSessionContext(
                    session,
                    Money,
                    new GameEventBus(),
                    DisabledDungeonDebugRuleQuery.Instance,
                    Journal,
                    session,
                    Treasury),
                new WarehouseCommandWorldContext(
                    Runtime.Contract,
                    EmptyWarehouseWorld.Instance,
                    EmptyBuildingWorld.Instance),
                new WarehouseCommandPlanningContext(
                    DefaultProxy.Create<IResourceStockPolicyRuntime>(),
                    DefaultProxy.Create<IRegionalSupplyContractRuntime>(),
                    DefaultProxy.Create<IGrandProjectRuntime>()));
            Offer = new StockDeliveryOffer(
                StockCategory.General,
                "material:t05",
                4,
                40,
                "T05 납품");
        }

        internal WarehouseFeatureCommandService Commands { get; }
        internal DeliveryRuntimeProxy Runtime { get; }
        internal RecordingMoneyAccount Money { get; }
        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal StockDeliveryOffer Offer { get; }
        internal TreasuryEconomyAggregateStateStore Treasury { get; }
        internal StockSupplyFactJournal Journal { get; }
        internal SupplyTransport Transport { get; }
        internal SupplyBundle Capture() => RoundTrip(new SupplyBundle
        {
            money = Money.Balance, day = session.State.day.Value, owner = Journal.Capture(),
            physical = Runtime.Capture(), ledger = Outcomes.Ledger.CaptureGameplayOutcomes(),
            counters = Outcomes.Transaction.Capture()
        });

        public void Dispose()
        {
            facts.Dispose();
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

    private sealed class RecordingMoneyAccount : IGameMoneyAccount
    {
        private readonly GameSessionState state;
        private readonly List<EconomyTransactionKind> successfulKinds = new();

        internal RecordingMoneyAccount(GameSessionState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));

        internal IReadOnlyList<EconomyTransactionKind> SuccessfulKinds => successfulKinds;
        public int Balance => Mathf.Max(0, state.holdingMoney.Value);
        public bool CanSpend(int amount) => Balance >= Mathf.Max(0, amount);

        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            int cost = Mathf.Max(0, amount);
            if (!CanSpend(cost))
            {
                reason = "insufficient funds";
                return false;
            }
            state.holdingMoney.Value -= cost;
            successfulKinds.Add(context.kind);
            reason = string.Empty;
            return true;
        }

        public void Add(int amount) => Add(amount, default);

        public void Add(int amount, EconomyTransactionContext context)
        {
            state.holdingMoney.Value += Mathf.Max(0, amount);
            successfulKinds.Add(context.kind);
        }

        public void SetBalance(int amount, EconomyTransactionContext context)
        {
            state.holdingMoney.Value = Mathf.Max(0, amount);
        }
    }

    public class DeliveryRuntimeProxy : DispatchProxy
    {
        private DungeonPhysicalItemSaveData state = new();
        private int spawned;
        private bool spawnCompleted;
        private bool throwOnSpawn;
        private int nextStackId = 1;

        public DeliveryRuntimeProxy()
        {
        }

        internal IWorldItemStackRuntime Contract { get; private set; }
        internal int TotalQuantity => (state.stacks ?? new List<WorldItemStackSaveData>())
            .Where(stack => stack != null)
            .Sum(stack => Mathf.Max(0, stack.quantity));

        internal static DeliveryRuntimeProxy Create(
            int spawned,
            bool spawnCompleted,
            bool throwOnSpawn)
        {
            IWorldItemStackRuntime contract = DispatchProxy.Create<
                IWorldItemStackRuntime,
                DeliveryRuntimeProxy>();
            var proxy = (DeliveryRuntimeProxy)(object)contract;
            proxy.Contract = contract;
            proxy.Configure(spawned, spawnCompleted, throwOnSpawn);
            return proxy;
        }

        internal void Configure(
            int spawned,
            bool spawnCompleted,
            bool throwOnSpawn)
        {
            this.spawned = Mathf.Max(0, spawned);
            this.spawnCompleted = spawnCompleted;
            this.throwOnSpawn = throwOnSpawn;
        }

        internal DungeonPhysicalItemSaveData Capture() => Clone(state);
        internal void Restore(DungeonPhysicalItemSaveData snapshot)
        {
            state = Clone(snapshot);
            nextStackId = state.stacks.Select(row => int.Parse(row.stackId.Substring("t05-stack-".Length)))
                .DefaultIfEmpty(0).Max() + 1;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == nameof(IWorldItemStackRuntime.Capture))
                return Capture();
            if (targetMethod.Name == nameof(IWorldItemStackRuntime.Restore))
            {
                Restore((DungeonPhysicalItemSaveData)arguments[0]);
                return null;
            }
            if (targetMethod.Name == nameof(IWorldItemStackRuntime.SpawnItemAtDropoff))
            {
                if (throwOnSpawn)
                    throw new InvalidOperationException("t05-injected-spawn-exception");
                int amount = (int)arguments[1];
                int delivered = Mathf.Clamp(spawned, 0, Mathf.Max(0, amount));
                arguments[3] = delivered;
                if (delivered > 0)
                {
                    state.stacks.Add(new WorldItemStackSaveData
                    {
                        stackId = "t05-stack-" + nextStackId++,
                        itemId = (string)arguments[0],
                        quantity = delivered
                    });
                }
                return spawnCompleted;
            }

            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                if (!parameters[index].IsOut && !parameters[index].ParameterType.IsByRef)
                    continue;
                Type type = parameters[index].ParameterType.IsByRef
                    ? parameters[index].ParameterType.GetElementType()
                    : parameters[index].ParameterType;
                arguments[index] = type != null && type.IsValueType
                    ? Activator.CreateInstance(type)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            return returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }

        private static DungeonPhysicalItemSaveData Clone(
            DungeonPhysicalItemSaveData source)
        {
            var copy = new DungeonPhysicalItemSaveData
            {
                version = source?.version ?? DungeonPhysicalItemSaveData.CurrentVersion,
                nextHaulOperationSequence = source?.nextHaulOperationSequence ?? 1
            };
            foreach (WorldItemStackSaveData stack in source?.stacks
                         ?? new List<WorldItemStackSaveData>())
            {
                if (stack == null)
                    continue;
                copy.stacks.Add(new WorldItemStackSaveData
                {
                    stackId = stack.stackId,
                    itemId = stack.itemId,
                    quantity = stack.quantity,
                    state = stack.state,
                    gridX = stack.gridX,
                    gridY = stack.gridY
                });
            }
            return copy;
        }
    }

    private sealed class EmptyWarehouseWorld : IWarehouseWorldQuery
    {
        internal static readonly EmptyWarehouseWorld Instance = new();
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses =>
            Array.Empty<IWarehouseFacility>();
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        internal static readonly EmptyBuildingWorld Instance = new();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    public class DefaultProxy : DispatchProxy
    {
        public DefaultProxy()
        {
        }

        internal static TContract Create<TContract>() where TContract : class =>
            DispatchProxy.Create<TContract, DefaultProxy>();

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                if (!parameters[index].IsOut && !parameters[index].ParameterType.IsByRef)
                    continue;
                Type type = parameters[index].ParameterType.IsByRef
                    ? parameters[index].ParameterType.GetElementType()
                    : parameters[index].ParameterType;
                arguments[index] = type != null && type.IsValueType
                    ? Activator.CreateInstance(type)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            if (returnType.IsArray)
                return Array.CreateInstance(returnType.GetElementType(), 0);
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }
}
#endif
