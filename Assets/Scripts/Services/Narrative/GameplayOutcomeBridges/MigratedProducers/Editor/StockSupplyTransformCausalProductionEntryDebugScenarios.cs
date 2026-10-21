#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class StockSupplyTransformCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:stock-supply-transform";
    internal const string SupplyActionId =
        "action:stock-supply-purchase-lumber";
    internal const string TransformActionId =
        "action:stock-supply-transform-lumber";

    private const string OwnerKind = "owner-kind:stock-supply-transform";
    private const string DomainStateSchema =
        "dungeonstory-causal-stock-supply-transform-owner-state@2";
    private const string SourceItemId = "material:lumber";
    private const string TransformOperationId =
        "causal:stock-supply-transform:lumber";
    private const string TransformReasonCode =
        "causal-stock-supply-transform";
    private const int AbsoluteDay = 3;
    private const int InitialMoney = 100;
    private const int SupplyQuantity = 3;
    private const int SupplyCost = 12;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A stock supply/transform checkpoint requires an owner ID.",
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
                SupplyActionId,
                MigratedProducerOutcomeKind.StockSupplyResult,
                context,
                context.PurchaseLumber,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.StockSupplyResult,
                    expectedSequence: 1),
                lifetime),
            Fixture(
                TransformActionId,
                MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
                context,
                context.TransformLumber,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
                    expectedSequence: 2),
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue =
            "owner:causal-stock-supply-transform:focused";
        JObject supplyCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-stock-supply-transform:supply",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture supply = fixtures.Get(
                SupplyActionId);
            IReadOnlyDictionary<string, object> before =
                supply.CaptureState();
            Require(
                (int)before["stockTransform.stage"] == 0
                && (int)before["stockTransform.money"] == InitialMoney
                && (int)before["stockTransform.sourceQuantity"] == 0
                && (int)before["stockTransform.exactOutcomeCount"] == 0,
                "Stock supply/transform owner did not start empty.");
            Require(supply.Execute(),
                "The real warehouse delivery purchase was rejected.");
            Require(supply.RequireExactOutcome().HasCanonicalPayloadHash,
                "The stock-supply result has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = supply.CaptureState();
            Require(
                (int)after["stockTransform.stage"] == 1
                && (int)after["stockTransform.money"]
                    == InitialMoney - SupplyCost
                && (int)after["stockTransform.sourceQuantity"]
                    == SupplyQuantity
                && (int)after["stockTransform.outputQuantity"] == 0
                && (int)after["stockTransform.exactOutcomeCount"] == 1,
                "Stock delivery did not retain money, lumber and outcome state.");
            supplyCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)supply)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-stock-supply-transform:transform",
                   OwnerIdValue,
                   supplyCheckpoint)))
        {
            IProductionCausalActionFixture supply = fixtures.Get(
                SupplyActionId);
            IProductionCausalActionFixture transform = fixtures.Get(
                TransformActionId);
            Require(
                (int)supply.CaptureState()["stockTransform.stage"] == 1
                && !supply.Execute(),
                "Fresh composition restore lost or replayed stock delivery.");
            Require(transform.Execute(),
                "The real physical transform was rejected.");
            Require(transform.RequireExactOutcome().HasCanonicalPayloadHash,
                "The physical-transform result has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after =
                transform.CaptureState();
            Require(
                (int)after["stockTransform.stage"] == 2
                && (int)after["stockTransform.money"]
                    == InitialMoney - SupplyCost
                && (int)after["stockTransform.sourceQuantity"]
                    == SupplyQuantity - 1
                && (int)after["stockTransform.outputQuantity"] == 1
                && (int)after["stockTransform.exactOutcomeCount"] == 2,
                "Physical transform did not consume the supplied lumber exactly once.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)transform)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-stock-supply-transform:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture supply = fixtures.Get(
                SupplyActionId);
            IProductionCausalActionFixture transform = fixtures.Get(
                TransformActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)supply)
                .CaptureOwnerCheckpoint();
            Require(!supply.Execute() && !transform.Execute(),
                "Terminal stock supply/transform owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)supply)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected stock supply/transform replay mutated its checkpoint.");
        }

        JObject moneyTamper = (JObject)supplyCheckpoint.DeepClone();
        ((JObject)moneyTamper["domainState"])["money"] = InitialMoney;
        Require(RejectCheckpoint(OwnerIdValue, moneyTamper, "money-tamper"),
            "A delivered stock checkpoint with an unpaid balance was accepted.");

        JObject physicalTamper = (JObject)supplyCheckpoint.DeepClone();
        ((JObject)physicalTamper["domainState"])["sourceStackId"] =
            "stack:tampered-stock-supply";
        Require(RejectCheckpoint(
                OwnerIdValue,
                physicalTamper,
                "physical-tamper"),
            "A stock checkpoint with a false source stack was accepted.");

        JObject ledgerTamper = (JObject)supplyCheckpoint.DeepClone();
        ((JArray)((JObject)ledgerTamper["outcomeLedger"])["exactOutcomes"])
            .Clear();
        Require(RejectCheckpoint(OwnerIdValue, ledgerTamper, "ledger-tamper"),
            "A supplied physical stack without its exact outcome was accepted.");

        if (log)
        {
            Debug.Log(
                "Stock delivery purchase -> restored physical lumber transform causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Stock supply/transform causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static IProductionCausalActionFixture Fixture(
        string actionId,
        MigratedProducerOutcomeKind kind,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "inventory",
            MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId,
            context.CaptureState,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
            context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
            context.HasPersistentOwner ? context : null);

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-stock-supply-transform:" + suffix,
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
        private readonly SessionStore session;
        private readonly TreasuryEconomyAggregateStateStore treasury;
        private readonly StockSupplyFactEditorFixture supplyFacts;
        private readonly WorldItemStackRuntime items;
        private readonly WorldItemRepository repository;
        private readonly WarehouseFeatureCommandService warehouse;
        private readonly DomainFactDispatcher physicalFacts;
        private readonly PhysicalItemTransformService transforms;
        private readonly string outputItemId;
        private string sourceStackId = string.Empty;
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
                : causalRunId + ":stock-supply-transform-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            session = new SessionStore(new GameSessionState(
                InitialMoney,
                AbsoluteDay));
            treasury = new TreasuryEconomyAggregateStateStore(
                aggregateRootStore);
            supplyFacts = new StockSupplyFactEditorFixture(
                outcomes,
                treasury,
                probe);
            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                new FixedGridProvider(CreateDropoffGrid()),
                out repository,
                out _,
                out _,
                out _);
            IDungeonItemCatalogProvider catalog = items.CatalogProvider;
            outputItemId = SelectOutputItem(catalog, items.MassQuery);
            warehouse = new WarehouseFeatureCommandService(
                new WarehouseCommandSessionContext(
                    session,
                    new EditorGameMoneyAccount(session.State),
                    new GameEventBus(),
                    DisabledDungeonDebugRuleQuery.Instance,
                    supplyFacts.Journal,
                    session,
                    treasury),
                new WarehouseCommandWorldContext(
                    items,
                    EmptyWorld.Instance,
                    EmptyWorld.Instance),
                new WarehouseCommandPlanningContext(
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios
                        .DefaultProxy.Create<IResourceStockPolicyRuntime>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios
                        .DefaultProxy.Create<IRegionalSupplyContractRuntime>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios
                        .DefaultProxy.Create<IGrandProjectRuntime>()));
            IItemMarkerPresenter markers = EditorNullItemMarkerPresenter.Instance;
            physicalFacts = outcomes.CreatePhysicalTransformationFacts(probe);
            transforms = new PhysicalItemTransformService(
                repository,
                new WorldItemSpawner(catalog, repository, markers),
                items.MassQuery,
                catalog,
                markers,
                session,
                physicalFacts, clock: new DungeonStory.Foundation.UnityUiClock());

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
            WorldItemStackSnapshot source = SourceStack;
            return new Dictionary<string, object>
            {
                ["stockTransform.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-stock-supply-transform",
                ["stockTransform.stage"] = Stage,
                ["stockTransform.absoluteDay"] = AbsoluteDay,
                ["stockTransform.money"] = session.State.holdingMoney.Value,
                ["stockTransform.sourceItemId"] = SourceItemId,
                ["stockTransform.sourceStackId"] = sourceStackId,
                ["stockTransform.sourceQuantity"] = source?.Quantity ?? 0,
                ["stockTransform.sourceX"] = source?.Position.x ?? 0,
                ["stockTransform.sourceY"] = source?.Position.y ?? 0,
                ["stockTransform.outputItemId"] = outputItemId,
                ["stockTransform.outputQuantity"] = OutputQuantity,
                ["stockTransform.stackCount"] = items.GetAllStacks().Count,
                ["stockTransform.supplyOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.StockSupplyResult),
                ["stockTransform.transformOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.PhysicalItemTransformReceipt),
                ["stockTransform.migratedOwnerCount"] =
                    outcomes.Transaction.Capture().owners.Count,
                ["stockTransform.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool PurchaseLumber()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            WarehouseFeatureCommandResult result = warehouse.PurchaseDelivery(
                new StockDeliveryOffer(
                    StockCategory.General,
                    SourceItemId,
                    SupplyQuantity,
                    SupplyCost,
                    "장기 원장 목재 납품"));
            WorldItemStackSnapshot source = items.GetAllStacks()
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.ItemId,
                        SourceItemId,
                        StringComparison.Ordinal));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Warehouse delivery failed: " + result.Message);
            }
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Warehouse delivery succeeded without a physical lumber stack.");
            }
            sourceStackId = source.StackId;
            bool valid = Stage == 1;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool TransformLumber()
        {
            ThrowIfDisposed();
            if (Stage != 1 || SourceStack == null)
                return false;
            WorldItemStackSnapshot source = SourceStack;
            bool transformed = transforms.TryTransformQuantity(
                sourceStackId,
                1,
                new[]
                {
                    new PhysicalItemTransformOutput(
                        outputItemId,
                        1,
                        source.Position)
                },
                TransformOperationId,
                TransformReasonCode,
                out PhysicalItemTransformReceipt receipt,
                out PhysicalItemTransformFailureCode failureCode,
                out _);
            bool valid = transformed
                && receipt.IsCommitted
                && failureCode == PhysicalItemTransformFailureCode.None
                && Stage == 2;
            if (valid)
                ValidateState();
            return valid;
        }

        internal GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            int expectedSequence)
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
                || exact.absoluteDay != AbsoluteDay
                || exact.status != GameplayOutcomeStatus.Succeeded
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
                || exact.facts == null
                || !exact.facts.Any(value => string.Equals(
                    value.factId,
                    MigratedProducerOutcomeIds.SummaryFact.Value,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Stock supply/transform entry did not publish its exact outcome: "
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
                    "Ephemeral stock supply/transform fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 2,
                    ["schema"] = DomainStateSchema,
                    ["stage"] = Stage,
                    ["absoluteDay"] = AbsoluteDay,
                    ["money"] = session.State.holdingMoney.Value,
                    ["sourceItemId"] = SourceItemId,
                    ["sourceStackId"] = sourceStackId,
                    ["outputItemId"] = outputItemId,
                    ["physicalItems"] = JObject.Parse(
                        JsonUtility.ToJson(items.Capture())),
                    ["treasury"] = CaptureTreasury(
                        treasury.Current,
                        supplyFacts.Journal.Capture()),
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
            supplyFacts?.Dispose();
            physicalFacts?.Dispose();
            items?.Dispose();
        }

        private int Stage
        {
            get
            {
                int supplies = OutcomeCount(
                    MigratedProducerOutcomeKind.StockSupplyResult);
                int transformsCount = OutcomeCount(
                    MigratedProducerOutcomeKind.PhysicalItemTransformReceipt);
                int exact = ExactOutcomeCount();
                int owners = outcomes.Transaction.Capture().owners.Count;
                if (supplies == 0 && transformsCount == 0
                    && exact == 0 && owners == 0)
                    return 0;
                if (supplies == 1 && transformsCount == 0
                    && exact == 1 && owners == 1)
                    return 1;
                if (supplies == 1 && transformsCount == 1
                    && exact == 2 && owners == 2)
                    return 2;
                return -1;
            }
        }

        private WorldItemStackSnapshot SourceStack =>
            string.IsNullOrWhiteSpace(sourceStackId)
                ? null
                : items.GetAllStacks().SingleOrDefault(value => value != null
                    && string.Equals(
                        value.StackId,
                        sourceStackId,
                        StringComparison.Ordinal));

        private int OutputQuantity => items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    outputItemId,
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
            .CaptureGameplayOutcomes()
            .exactOutcomes.Count;

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
                "stock supply/transform owner state",
                "schemaVersion",
                "schema",
                "stage",
                "absoluteDay",
                "money",
                "sourceItemId",
                "sourceStackId",
                "outputItemId",
                "physicalItems",
                "treasury",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 2
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || (int)state["absoluteDay"] != AbsoluteDay
                || !string.Equals(
                    (string)state["sourceItemId"],
                    SourceItemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["outputItemId"],
                    outputItemId,
                    StringComparison.Ordinal)
                || state["physicalItems"] is not JObject physical
                || state["treasury"] is not JObject savedTreasury
                || state["outcomeOwners"] is not JObject savedOwners)
            {
                throw new InvalidOperationException(
                    "Stock supply/transform checkpoint identity changed.");
            }
            session.State.day.Initialize((int)state["absoluteDay"]);
            session.State.holdingMoney.Initialize((int)state["money"]);
            sourceStackId = (string)state["sourceStackId"] ?? string.Empty;
            items.Restore(JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                physical.ToString(Formatting.None)));
            treasury.Replace(RestoreTreasury(savedTreasury));
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    savedOwners.ToString(Formatting.None))));
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Stock supply/transform checkpoint stage disagrees with saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            WorldItemStackSnapshot source = SourceStack;
            bool shape = stage switch
            {
                0 => session.State.holdingMoney.Value == InitialMoney
                    && string.IsNullOrWhiteSpace(sourceStackId)
                    && items.GetAllStacks().Count == 0
                    && OutputQuantity == 0,
                1 => session.State.holdingMoney.Value
                        == InitialMoney - SupplyCost
                    && source != null
                    && string.Equals(
                        source.ItemId,
                        SourceItemId,
                        StringComparison.Ordinal)
                    && source.Quantity == SupplyQuantity
                    && items.GetAllStacks().Count == 1
                    && OutputQuantity == 0,
                2 => session.State.holdingMoney.Value
                        == InitialMoney - SupplyCost
                    && source != null
                    && source.Quantity == SupplyQuantity - 1
                    && OutputQuantity == 1
                    && items.GetAllStacks().Count == 2,
                _ => false
            };
            if (!shape || session.State.day.Value != AbsoluteDay)
            {
                throw new InvalidOperationException(
                    "Stock supply, money, physical transform and exact ledger state diverged: stage="
                    + stage
                    + "; money=" + session.State.holdingMoney.Value
                    + "; source=" + (source?.Quantity ?? 0)
                    + "; output=" + OutputQuantity
                    + "; stacks=" + items.GetAllStacks().Count);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
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

    private sealed class EmptyWorld :
        IWarehouseWorldQuery,
        IBuildingWorldQuery
    {
        internal static readonly EmptyWorld Instance = new();
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses =>
            Array.Empty<IWarehouseFacility>();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
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

    private static string SelectOutputItem(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery)
    {
        long sourceMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)SourceItemId).Value;
        return catalog.All
            .Where(definition => definition != null
                && definition.MaxStack > 1
                && !string.Equals(
                    definition.ItemId,
                    SourceItemId,
                    StringComparison.Ordinal)
                && !PhysicalItemIds.TryGetEquipmentDefinitionId(
                    definition.ItemId,
                    out _)
                && !PhysicalItemIds.IsEquipmentModule(definition.ItemId)
                && massQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)definition.ItemId).Value <= sourceMass)
            .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
            .Select(definition => definition.ItemId)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No bounded stock-transform output definition exists.");
    }

    private static Grid CreateDropoffGrid()
    {
        var grid = new Grid(1, 1);
        grid.SetAreaType(Vector2Int.zero, GridCellAreaType.DropZone);
        return grid;
    }

    private static JObject CaptureTreasury(
        TreasuryEconomyAggregateState state,
        StockSupplyFactSaveData warehouseSupply)
    {
        Require(state != null, "Treasury state is missing.");
        Require(state.Ledger.Records.Count == 0
                && state.Wages.Count == 0
                && state.Mercenaries.Count == 0
                && state.FacilityContracts.Count == 0
                && state.ChargedFacilityOrderKeys.Count == 0
                && state.FacilityFailures.Count == 0
                && state.StockRules.Count == 0
                && state.WishlistRules.Count == 0
                && state.ProcurementResults.Count == 0
                && state.ProcessedOfferKeys.Count == 0
                && state.OverclockStates.Count == 0
                && state.DefensePolicies.Count == 0
                && state.DefenseSpending.Count == 0
                && state.DefenseFailures.Count == 0,
            "Unexpected treasury state escaped the closed stock fixture.");
        return new JObject
        {
            ["nextSequence"] = state.Ledger.NextSequence,
            ["dailyBudget"] = state.DailyBudget,
            ["minimumReserve"] = state.MinimumReserve,
            ["lastProcessedDay"] = state.LastProcessedDay,
            ["warehouseSupply"] = JObject.Parse(JsonUtility.ToJson(
                StockSupplyFactSaveData.ValidateAndClone(
                    warehouseSupply)))
        };
    }

    private static TreasuryEconomyAggregateState RestoreTreasury(JObject state)
    {
        RequireFields(
            state,
            "stock supply/transform treasury state",
            "nextSequence",
            "dailyBudget",
            "minimumReserve",
            "lastProcessedDay",
            "warehouseSupply");
        if (state["warehouseSupply"] is not JObject warehouseSupply)
        {
            throw new InvalidOperationException(
                "Stock supply journal state is missing.");
        }
        return new TreasuryEconomyAggregateState
        {
            Ledger = new EconomyTransactionLedgerState
            {
                NextSequence = (int)state["nextSequence"]
            },
            DailyBudget = (int)state["dailyBudget"],
            MinimumReserve = (int)state["minimumReserve"],
            LastProcessedDay = (int)state["lastProcessedDay"],
            WarehouseSupply = StockSupplyFactSaveData.ValidateAndClone(
                JsonUtility.FromJson<StockSupplyFactSaveData>(
                    warehouseSupply.ToString(Formatting.None)))
        };
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
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields are not closed: "
                + string.Join(",", actual));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
