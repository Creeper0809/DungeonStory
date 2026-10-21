#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class FactionRouteCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:faction-route-progression";
    internal const string SettlementActionId =
        "action:faction-route-settlement";
    internal const string ArrivalActionId =
        "action:faction-route-arrival";
    internal const string CargoActionId =
        "action:faction-route-cargo-delivery";

    private const string OwnerKind = "owner-kind:faction-route-progression";
    private const string DomainStateSchema =
        "dungeonstory-causal-faction-route-owner-state@1";
    private const string FactionId = "faction:causal:trade-route";
    private const string ItemId = "material:causal-route-cargo";
    private const string RouteId = "faction-route:1";
    private const int AbsoluteDay = 6;
    private const int InitialMoney = 1000;
    private const int PaymentGold = 10;

    private static DomainFactDispatcher CreateFactionFacts(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(
            outcomes.Recorder,
            outcomes.Ledger);
        var factionMapping = new FactionDomainFactMapping(outcomes.Ledger);
        try
        {
            dispatcher.Subscribe(
                "fixture:faction-trust-changed",
                new MigratedProducerDomainFactProbeObserver<
                    FactionTrustChangedDomainFact>(
                    factionMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "fixture:faction-betrayal",
                new MigratedProducerDomainFactProbeObserver<
                    FactionBetrayalDomainFact>(
                    factionMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "fixture:faction-route-settled",
                new MigratedProducerDomainFactProbeObserver<
                    FactionRouteSettlementDomainFact>(
                    factionMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "fixture:faction-route-arrived",
                new MigratedProducerDomainFactProbeObserver<
                    FactionRouteArrivedDomainFact>(
                    factionMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            var goodwillMapping = new FactionGoodwillFactMapping(
                outcomes.Ledger);
            dispatcher.Subscribe(
                "fixture:faction-goodwill-transferred",
                new MigratedProducerDomainFactProbeObserver<
                    FactionGoodwillTransferredFact>(
                    goodwillMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            var cargoMapping = new FactionCargoFactMapping(outcomes.Ledger);
            dispatcher.Subscribe(
                "fixture:faction-cargo-delivered",
                new MigratedProducerDomainFactProbeObserver<
                    FactionCargoDeliveredFact>(
                    cargoMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }
    private const int CargoQuantity = 2;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A faction-route checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(3, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                SettlementActionId,
                MigratedProducerOutcomeKind.FactionRouteSettlementReceipt,
                context,
                context.RequestTrade,
                context.RequireSettlementOutcome,
                lifetime),
            Fixture(
                ArrivalActionId,
                MigratedProducerOutcomeKind.FactionRouteArrivedEvent,
                context,
                context.AdvanceToArrival,
                context.RequireArrivalOutcome,
                lifetime),
            Fixture(
                CargoActionId,
                MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt,
                context,
                context.DeliverCargo,
                context.RequireCargoOutcome,
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue = "faction-route:causal-progression:focused";
        JObject settlementCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-faction-route:settlement",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture action = fixtures.Get(SettlementActionId);
            IReadOnlyDictionary<string, object> before = action.CaptureState();
            Require((int)before["factionRoute.stage"] == 0
                    && (int)before["factionRoute.routeCount"] == 0
                    && (int)before["factionRoute.sessionMoney"] == InitialMoney,
                "Faction-route owner did not start at the trade request boundary.");
            Require(action.Execute(), "The real trade request did not settle.");
            Require(action.RequireExactOutcome().HasCanonicalPayloadHash,
                "The settlement outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = action.CaptureState();
            Require((int)after["factionRoute.stage"] == 1
                    && string.Equals((string)after["factionRoute.routeStatus"],
                        FactionRouteStatus.Traveling.ToString(),
                        StringComparison.Ordinal)
                    && (int)after["factionRoute.sessionMoney"]
                        == InitialMoney - PaymentGold,
                "Trade settlement did not create the paid traveling route.");
            settlementCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)action)
                .CaptureOwnerCheckpoint();
        }

        JObject arrivalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-faction-route:arrival",
                   OwnerIdValue,
                   settlementCheckpoint)))
        {
            IProductionCausalActionFixture settlement = fixtures.Get(SettlementActionId);
            IProductionCausalActionFixture arrival = fixtures.Get(ArrivalActionId);
            Require(!settlement.Execute(),
                "Fresh composition restore replayed the paid route request.");
            Require(arrival.Execute(), "The paid route did not arrive.");
            Require(arrival.RequireExactOutcome().HasCanonicalPayloadHash,
                "The arrival outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = arrival.CaptureState();
            Require((int)after["factionRoute.stage"] == 2
                    && string.Equals((string)after["factionRoute.routeStatus"],
                        FactionRouteStatus.Arrived.ToString(),
                        StringComparison.Ordinal)
                    && string.Equals(
                        (string)after["factionRoute.cargoDeliveryState"],
                        FactionRouteCargoDeliveryState.Ready.ToString(),
                        StringComparison.Ordinal),
                "Arrival did not preserve ready cargo for restored delivery.");
            arrivalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)arrival)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-faction-route:cargo",
                   OwnerIdValue,
                   arrivalCheckpoint)))
        {
            IProductionCausalActionFixture arrival = fixtures.Get(ArrivalActionId);
            IProductionCausalActionFixture cargo = fixtures.Get(CargoActionId);
            Require(!arrival.Execute(),
                "Fresh composition restore replayed route arrival.");
            Require(cargo.Execute(), "The restored ready cargo was not delivered.");
            Require(cargo.RequireExactOutcome().HasCanonicalPayloadHash,
                "The cargo outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = cargo.CaptureState();
            Require((int)after["factionRoute.stage"] == 3
                    && string.Equals(
                        (string)after["factionRoute.cargoDeliveryState"],
                        FactionRouteCargoDeliveryState.Delivered.ToString(),
                        StringComparison.Ordinal)
                    && (int)after["factionRoute.cargoDeliveredQuantity"]
                        == CargoQuantity,
                "Cargo delivery did not retain its exact stack receipt.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)cargo)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-faction-route:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture settlement = fixtures.Get(SettlementActionId);
            IProductionCausalActionFixture arrival = fixtures.Get(ArrivalActionId);
            IProductionCausalActionFixture cargo = fixtures.Get(CargoActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)cargo)
                .CaptureOwnerCheckpoint();
            Require(!settlement.Execute() && !arrival.Execute() && !cargo.Execute(),
                "Terminal faction-route owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)cargo)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected faction-route replay mutated the owner checkpoint.");
        }

        JObject balanceTamper = (JObject)settlementCheckpoint.DeepClone();
        ((JObject)balanceTamper["domainState"])["gameSession"]!["money"] =
            InitialMoney;
        Require(RejectCheckpoint(OwnerIdValue, balanceTamper, "balance-tamper"),
            "A paid route checkpoint with an unpaid balance was accepted.");

        JObject cargoTamper = (JObject)arrivalCheckpoint.DeepClone();
        ((JObject)cargoTamper["domainState"])["faction"]!["routes"]![0]![
            "cargoDelivery"]!["state"] =
            (int)FactionRouteCargoDeliveryState.Delivered;
        Require(RejectCheckpoint(OwnerIdValue, cargoTamper, "cargo-tamper"),
            "An arrival checkpoint with fabricated cargo delivery was accepted.");

        JObject outcomeIdentityTamper = (JObject)arrivalCheckpoint.DeepClone();
        ((JObject)outcomeIdentityTamper["domainState"])["faction"]![
            "routes"]![0]!["arrivalPublicationId"] =
            "run:faction-route-arrival:conflicting/1";
        Require(RejectCheckpoint(
                OwnerIdValue,
                outcomeIdentityTamper,
                "outcome-identity-tamper"),
            "A route checkpoint with a valid-looking but conflicting arrival outcome identity was accepted.");

        if (log)
        {
            Debug.Log(
                "Faction route settlement -> restored arrival -> restored cargo delivery causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Faction-route causal owner family returned false.");
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
            "faction",
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
                "run:causal-faction-route:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed partial class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly DomainFactDispatcher factionFacts;
        private readonly MutableGameClock clock;
        private readonly MutableDropZone dropZone;
        private readonly EditorWarehouseStockRuntime items;
        private readonly FactionCampaignDouble campaign;
        private readonly GameSessionState session;
        private readonly SessionStore sessionStore;
        private readonly FactionRuntimeApplicationAdapter runtime;
        private readonly ExactSourcePublicationDouble exactSources;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint,
            Func<DomainFactDispatcher, IDomainFactPublisher> wrapPublisher = null)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":faction-route-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            clock = new MutableGameClock();
            dropZone = new MutableDropZone();
            items = new EditorWarehouseStockRuntime();
            campaign = new FactionCampaignDouble(FactionId, 35);
            session = new GameSessionState(InitialMoney, AbsoluteDay);
            sessionStore = new SessionStore(session);
            var money = new SessionMoneyAccount(session);
            var treasury = new TreasuryEconomyAggregateStateStore(
                aggregateRootStore);
            var allianceBudget =
                new ResourceFactionAllianceBenefitBudgetApplicationAdapter(
                    CharacterAiEditorTestDependencies.ContentDefinitions);
            factionFacts = CreateFactionFacts(outcomes, probe);
            exactSources = new ExactSourcePublicationDouble();
            runtime = new FactionRuntimeApplicationAdapter(
                CreateInitialState(allianceBudget),
                clock,
                new GameEventBus(),
                money,
                campaign,
                campaign,
                campaign,
                wrapPublisher?.Invoke(factionFacts) ?? factionFacts,
                factionFacts,
                aggregateRootStore,
                dropZones: dropZone,
                exactSources: exactSources,
                itemRuntime: items,
                definitions: new[] { CreateDefinition() },
                world: new FactionWorldDouble(),
                routeEconomicPolicies: new FactionEconomicPolicyDouble(),
                gameSessionState: sessionStore,
                treasuryState: treasury,
                allianceBenefitBudget: allianceBudget);
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
            FactionRouteState route = runtime.Routes.SingleOrDefault();
            FactionRouteSettlementReceipt settlement = route?.settlement;
            FactionRouteCargoDeliveryReceipt cargo = route?.cargoDelivery;
            return new Dictionary<string, object>
            {
                ["factionRoute.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "faction-route:ephemeral-progression",
                ["factionRoute.stage"] = Stage,
                ["factionRoute.absoluteDay"] = AbsoluteDay,
                ["factionRoute.factionId"] = FactionId,
                ["factionRoute.routeId"] = route?.routeId ?? string.Empty,
                ["factionRoute.routeCount"] = runtime.Routes.Count,
                ["factionRoute.routeStatus"] = route?.status.ToString() ?? "None",
                ["factionRoute.pathIndex"] = route?.pathIndex ?? 0,
                ["factionRoute.segmentProgress"] = route?.segmentProgress ?? 0f,
                ["factionRoute.settlementState"] =
                    settlement?.state.ToString() ?? "None",
                ["factionRoute.settlementPaymentGold"] =
                    settlement?.paymentGold ?? 0,
                ["factionRoute.settlementBalanceBefore"] =
                    settlement?.balanceBefore ?? InitialMoney,
                ["factionRoute.settlementBalanceAfter"] =
                    settlement?.balanceAfter ?? InitialMoney,
                ["factionRoute.cargoDeliveryState"] =
                    cargo?.state.ToString() ?? "None",
                ["factionRoute.cargoStackCount"] = cargo?.stacks?.Count ?? 0,
                ["factionRoute.cargoDeliveredQuantity"] =
                    cargo?.stacks?.Sum(value => value.quantity) ?? 0,
                ["factionRoute.sessionMoney"] = session.holdingMoney.Value,
                ["factionRoute.settlementOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteSettlementReceipt),
                ["factionRoute.arrivalOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteArrivedEvent),
                ["factionRoute.cargoOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt),
                ["factionRoute.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool RequestTrade()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            bool created = runtime.TryRequestTrade(
                FactionId,
                out string routeId,
                out _);
            bool valid = created
                && string.Equals(routeId, RouteId, StringComparison.Ordinal)
                && Stage == 1;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool AdvanceToArrival()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            dropZone.Enabled = false;
            clock.Delta = 20f;
            runtime.Tick();
            clock.Delta = 0f;
            bool valid = Stage == 2;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool DeliverCargo()
        {
            ThrowIfDisposed();
            if (Stage != 2)
                return false;
            dropZone.Enabled = true;
            clock.Delta = 0f;
            runtime.Tick();
            bool valid = Stage == 3;
            if (valid)
                ValidateState();
            return valid;
        }

        internal GameplayOutcomeReplayIdentity RequireSettlementOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.FactionRouteSettlementReceipt,
                expectedSequence: 1);

        internal GameplayOutcomeReplayIdentity RequireArrivalOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.FactionRouteArrivedEvent,
                expectedSequence: 2);

        internal GameplayOutcomeReplayIdentity RequireCargoOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt,
                expectedSequence: 3);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral faction-route fixtures have no checkpoint.");
            }
            ValidateState();
            ValidateCargoJoin(runtime.Capture(), true);
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["stage"] = Stage,
                    ["factionId"] = FactionId,
                    ["routeId"] = Stage == 0 ? string.Empty : RouteId,
                    ["faction"] = JObject.Parse(
                        JsonUtility.ToJson(runtime.Capture())),
                    ["campaign"] = JObject.Parse(
                        JsonUtility.ToJson(campaign.CaptureFactions())),
                    ["gameSession"] = JObject.Parse(JsonUtility.ToJson(
                        GameSessionSaveData.From(session.Capture()))),
                    ["physicalItems"] = JObject.Parse(
                        JsonUtility.ToJson(items.Capture())),
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
            factionFacts.Dispose();
        }

        private int Stage
        {
            get
            {
                int settlements = OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteSettlementReceipt);
                int arrivals = OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteArrivedEvent);
                int deliveries = OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt);
                FactionRouteState route = runtime.Routes.SingleOrDefault();
                if (settlements == 0 && arrivals == 0 && deliveries == 0
                    && route == null
                    && session.holdingMoney.Value == InitialMoney)
                    return 0;
                if (settlements == 1 && arrivals == 0 && deliveries == 0
                    && IsPaidRoute(route, FactionRouteStatus.Traveling)
                    && route.cargoDelivery.state
                        == FactionRouteCargoDeliveryState.Ready)
                    return 1;
                if (settlements == 1 && arrivals == 1 && deliveries == 0
                    && IsPaidRoute(route, FactionRouteStatus.Arrived)
                    && route.pathIndex == 1
                    && Math.Abs(route.segmentProgress) < 0.001f
                    && route.cargoDelivery.state
                        == FactionRouteCargoDeliveryState.Ready)
                    return 2;
                if (settlements == 1 && arrivals == 1 && deliveries == 1
                    && IsPaidRoute(route, FactionRouteStatus.Arrived)
                    && route.cargoDelivery.state
                        == FactionRouteCargoDeliveryState.Delivered
                    && route.cargoDelivery.stacks != null
                    && route.cargoDelivery.stacks.Count == 1
                    && route.cargoDelivery.stacks.Sum(value => value.quantity)
                        == CargoQuantity)
                    return 3;
                return -1;
            }
        }

        private bool IsPaidRoute(
            FactionRouteState route,
            FactionRouteStatus status) =>
            route != null
            && string.Equals(route.routeId, RouteId, StringComparison.Ordinal)
            && string.Equals(route.factionId, FactionId, StringComparison.Ordinal)
            && route.kind == FactionRouteKind.TradeCaravan
            && route.status == status
            && route.settlement != null
            && route.settlement.state == FactionRouteSettlementState.Paid
            && route.settlement.paymentGold == PaymentGold
            && route.settlement.balanceBefore == InitialMoney
            && route.settlement.balanceAfter == InitialMoney - PaymentGold
            && session.holdingMoney.Value == InitialMoney - PaymentGold;

        private GameplayOutcomeReplayIdentity RequireOutcome(
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
                || !string.Equals(exact.producerId, definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || !HasRequiredRouteShape(exact, kind, expectedSequence))
            {
                throw new InvalidOperationException(
                    "Faction-route production entry did not publish its exact outcome: "
                    + kind);
            }
            if (kind
                == MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt)
            {
                RequireCargoPerspective(identity, MigratedProducerOutcomeIds.RouteKind, RouteId);
                RequireCargoPerspective(identity, MigratedProducerOutcomeIds.FactionKind, FactionId);
                RequireCargoPerspective(
                    identity,
                    MigratedProducerOutcomeIds.WorldLocationKind,
                    "hex:2:3", expectedExperience: false);
                RequireCargoPerspective(
                    identity,
                    MigratedProducerOutcomeIds.WorldLocationKind,
                    "hex:0:0", expectedExperience: false);
                RequireCargoPerspective(
                    identity,
                    MigratedProducerOutcomeIds.ItemDefinitionKind,
                    ItemId, expectedExperience: false);
            }
            return identity;
        }

        private static bool HasRequiredRouteShape(
            GameplayOutcomeSnapshot exact,
            MigratedProducerOutcomeKind kind,
            int expectedSequence)
        {
            const int ExpectedSubjects = 5;
            if (exact.subjects?.Count != ExpectedSubjects
                || exact.participants?.Count != ExpectedSubjects
                || !HasParticipant(exact, "route", RouteId, "route")
                || !HasParticipant(exact, "faction", FactionId, "faction")
                || !HasParticipant(exact, "world-location", "hex:2:3", "source")
                || !HasParticipant(exact, "world-location", "hex:0:0", "destination")
                || !string.Equals(exact.rootOperationId, RouteId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return kind switch
            {
                MigratedProducerOutcomeKind.FactionRouteSettlementReceipt =>
                    expectedSequence == 1
                    && exact.parentSequence == 0L
                    && string.Equals(exact.causationRelationId,
                        "route-settlement-started", StringComparison.Ordinal)
                    && HasParticipant(exact, "item-definition", ItemId, "item")
                    && HasMetric(exact, "quantity", ItemId, CargoQuantity),
                MigratedProducerOutcomeKind.FactionRouteArrivedEvent =>
                    expectedSequence == 2
                    && exact.parentSequence == 1L
                    && string.Equals(exact.causationRelationId,
                        "route-arrival-from-settlement", StringComparison.Ordinal)
                    && HasParticipant(exact, "item-definition", ItemId, "item")
                    && HasMetric(exact, "strength-before", RouteId, 100d)
                    && HasMetric(exact, "strength-after", RouteId, 100d),
                MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt =>
                    expectedSequence == 3
                    && exact.parentSequence == 2L
                    && string.Equals(exact.causationRelationId,
                        "route-cargo-from-arrival", StringComparison.Ordinal)
                    && HasParticipant(exact, "item-definition", ItemId, "item")
                    && HasMetric(exact, "quantity", ItemId, CargoQuantity)
                    && HasMetric(exact, "value", ItemId,
                        CargoQuantity * PaymentGold / CargoQuantity),
                _ => false
            };
        }

        private static bool HasParticipant(
            GameplayOutcomeSnapshot exact,
            string kind,
            string id,
            string role) => exact.participants.Any(value =>
            value != null
            && string.Equals(value.entityKindId, kind, StringComparison.Ordinal)
            && string.Equals(value.entityId, id, StringComparison.Ordinal)
            && string.Equals(value.roleId, role, StringComparison.Ordinal));

        private static bool HasMetric(
            GameplayOutcomeSnapshot exact,
            string metric,
            string referenceId,
            double value) => exact.metrics.Any(current =>
            current != null
            && string.Equals(current.metricId, metric, StringComparison.Ordinal)
            && string.Equals(current.referenceId, referenceId,
                StringComparison.Ordinal)
            && Math.Abs(current.value - value) < 0.001d);

        private void RequireCargoPerspective(
            GameplayOutcomeReplayIdentity identity,
            GameplayEntityKindId kind,
            string id,
            bool expectedExperience = true)
        {
            var subject = new GameplayEntityId(kind, id);
            var raw = outcomes.Ledger.GetForEntity(subject, OutcomeCursor.FirstPage(10), OutcomeFilter.All);
            Require(raw.Items.Any(value => value.Exact != null
                    && value.Exact.runId == identity.OutcomeId.RunId.Value
                    && value.Exact.sequence == identity.OutcomeId.Sequence),
                "Cargo raw ledger reference missing: " + subject);
            // Current cargo policy grants experience to route/faction only.
            // Locations/items stay queryable facts, not invented experiencing subjects.
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence =
                GameplayOutcomeEvidenceFormulaProjection.Capture(
                    new GameplayOutcomeNarrativeEvidenceQuery(outcomes.Ledger)
                        .GetForEntity(subject, 10, minimumSalience: 0f, includeCompacted: true));
            Require(evidence.Any(value => value != null
                    && string.Equals(value.outcomeRunId,
                        identity.OutcomeId.RunId.Value,
                        StringComparison.Ordinal)
                    && value.outcomeSequence == identity.OutcomeId.Sequence) == expectedExperience,
                "Cargo experience eligibility disagrees with current policy for " + kind.Value
                + ":" + id + ".");
        }

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(value.outcomeTypeId, type,
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
                "faction-route owner state",
                "schemaVersion",
                "schema",
                "stage",
                "factionId",
                "routeId",
                "faction",
                "campaign",
                "gameSession",
                "physicalItems",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals((string)state["schema"], DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["factionId"], FactionId,
                    StringComparison.Ordinal)
                || state["faction"] is not JObject factionJson
                || state["campaign"] is not JObject campaignJson
                || state["gameSession"] is not JObject sessionJson
                || state["physicalItems"] is not JObject physicalJson
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException(
                    "Faction-route checkpoint identity changed.");
            }

            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None))));
            campaign.PublishFactions(campaign.PrepareFactionsRestore(
                JsonUtility.FromJson<FactionCampaignWorldSaveData>(
                    campaignJson.ToString(Formatting.None))));
            sessionStore.Restore(
                JsonUtility.FromJson<GameSessionSaveData>(
                    sessionJson.ToString(Formatting.None)).ToSnapshot());
            items.Restore(JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                physicalJson.ToString(Formatting.None)));
            DungeonFactionSaveData faction =
                JsonUtility.FromJson<DungeonFactionSaveData>(
                    factionJson.ToString(Formatting.None));
            FactionCargoSaveValidation.ValidateJson(factionJson.ToString(Formatting.None));
            ValidateCargoJoin(faction, true);
            runtime.PublishRestoreCandidate(
                runtime.PrepareRestoreCandidate(faction));
            int expectedStage = (int)state["stage"];
            string expectedRouteId = (string)state["routeId"];
            if (expectedStage != Stage
                || !string.Equals(
                    expectedRouteId,
                    Stage == 0 ? string.Empty : RouteId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Faction-route checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            bool valid = stage is >= 0 and <= 3
                && runtime.Factions.Count == 1
                && string.Equals(runtime.Factions[0].factionId,
                    FactionId,
                    StringComparison.Ordinal)
                && ExactOutcomeCount() == stage
                && OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteSettlementReceipt)
                    == (stage == 0 ? 0 : 1)
                && OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteArrivedEvent)
                    == (stage >= 2 ? 1 : 0)
                && OutcomeCount(
                    MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt)
                    == (stage == 3 ? 1 : 0);
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Faction route, economy, outcome owner and exact ledger diverged.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures ?? throw new ArgumentNullException(nameof(fixtures));

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

    private sealed class MutableGameClock : IGameClock
    {
        internal float Delta { get; set; }
        public float DeltaTime => Delta;
        public float Time => 1f;
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class SessionMoneyAccount : IIdempotentGameMoneyAccount
    {
        private readonly GameSessionState state;
        private readonly Dictionary<string, EconomyTransactionRecord> debits =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> credits = new(StringComparer.Ordinal);

        internal SessionMoneyAccount(GameSessionState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));

        public int Balance => state.holdingMoney.Value;
        public bool CanSpend(int amount) => amount >= 0 && Balance >= amount;
        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (!CanSpend(amount))
            {
                reason = "insufficient fixture balance";
                return false;
            }
            state.holdingMoney.Value = Balance - amount;
            reason = string.Empty;
            return true;
        }

        public void Add(int amount) => state.holdingMoney.Value = Balance + amount;
        public void Add(int amount, EconomyTransactionContext context) => Add(amount);
        public void SetBalance(int amount, EconomyTransactionContext context) =>
            state.holdingMoney.Value = Math.Max(0, amount);

        public bool TrySpendOnce(
            int amount,
            EconomyTransactionContext context,
            out EconomyTransactionRecord receipt,
            out string reason)
        {
            string key = context.sourceId ?? string.Empty;
            if (debits.TryGetValue(key, out receipt))
            {
                reason = string.Empty;
                return receipt.succeeded;
            }
            int before = Balance;
            bool spent = TrySpend(amount, context, out reason);
            receipt = new EconomyTransactionRecord
            {
                transactionId = "transaction:" + key,
                kind = context.kind,
                sourceId = context.sourceId,
                targetId = context.targetId,
                amount = spent ? -amount : 0,
                balanceBefore = before,
                balanceAfter = Balance,
                succeeded = spent
            };
            debits[key] = receipt;
            return spent;
        }

        public bool TryCreditOnce(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (credits.Add(context.sourceId ?? string.Empty))
                Add(amount);
            reason = string.Empty;
            return true;
        }
    }

    private sealed class SessionStore : IGameSessionStateStore
    {
        private readonly GameSessionState state;
        internal SessionStore(GameSessionState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        public bool TryGetSessionState(out GameSessionState session)
        {
            session = state;
            return true;
        }
        public void Restore(GameSessionSnapshot snapshot)
        {
            state.holdingMoney.Initialize(snapshot.Money);
            state.day.Initialize(snapshot.Day);
            state.gameSpeed.Initialize(snapshot.GameSpeed);
            state.curTime.Initialize(snapshot.ElapsedSeconds);
            state.hour.Initialize(snapshot.Hour);
            state.timeOfDay.Initialize(snapshot.TimeOfDay);
        }
    }

    private sealed class FactionEconomicPolicyDouble :
        IFactionRouteEconomicPolicyRegistry
    {
        public bool TryCreateQuote(
            FactionDefinitionSnapshot definition,
            FactionRouteKind routeKind,
            out FactionRouteQuoteSnapshot quote,
            out string failureReason)
        {
            quote = new FactionRouteQuoteSnapshot(
                definition.StableId,
                routeKind,
                definition.TradeEconomicPolicy.capabilityId,
                definition.TradeEconomicPolicy.capabilityVersion,
                20,
                PaymentGold,
                new[]
                {
                    new FactionRouteQuoteLineReceipt
                    {
                        itemId = ItemId,
                        amount = CargoQuantity,
                        unitPriceGold = PaymentGold / CargoQuantity
                    }
                },
                "source-digest:causal-faction-route",
                "quote-digest:causal-faction-route");
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FactionWorldDouble : IOffenseWorldSimulation
    {
        public int WorldSeed => 1;
        public int WorldDay => AbsoluteDay;
        public float WorldHour => 0f;
        public OffenseHexCoord DungeonCoord => new(0, 0);
        public IReadOnlyCollection<OffenseHexTileState> Tiles =>
            Array.Empty<OffenseHexTileState>();
        public IReadOnlyCollection<OffenseWorldSiteStateData> Sites =>
            Array.Empty<OffenseWorldSiteStateData>();
        public IReadOnlyCollection<OffenseUrgentSiteStateData> UrgentSites =>
            Array.Empty<OffenseUrgentSiteStateData>();
        public event Action Changed { add { } remove { } }
        public void Initialize(int runSeed) { }
        public void AdvanceHours(float hours) { }
        public bool TryGetTile(OffenseHexCoord coord, out OffenseHexTileState tile)
        {
            tile = null;
            return false;
        }
        public bool TryGetSite(string siteId, out OffenseWorldSiteStateData site)
        {
            site = null;
            return false;
        }
        public bool TryGetUrgentSite(
            string siteId,
            out OffenseUrgentSiteStateData site)
        {
            site = null;
            return false;
        }
        public bool TryRevealSite(string siteId) => false;
        public bool TryEngageSite(string siteId) => false;
        public bool TryResolveSite(string siteId) => false;
        public bool TryRegisterStrategicSite(OffenseWorldSiteStateData site) => true;
        public bool TrySpawnUrgentSite(
            string definitionId,
            OffenseHexCoord coord,
            out string siteId)
        {
            siteId = string.Empty;
            return false;
        }
        public bool TryMitigateUrgentSite(string siteId, float amount) => false;
        public bool TryDestroyUrgentSite(string siteId) => false;
        public bool TryFindPath(
            OffenseHexCoord start,
            OffenseHexCoord goal,
            OffenseTravelProfile profile,
            out IReadOnlyList<OffenseHexCoord> path,
            out float totalCost)
        {
            path = new[] { start, goal };
            totalCost = 1f;
            return true;
        }
        public int GetMinimumStepDistance(
            OffenseHexCoord start,
            OffenseHexCoord goal) => 1;
        public OffenseWorldSaveData Capture() => new();
        public void Restore(OffenseWorldSaveData snapshot) { }
    }

    private sealed class MutableDropZone : IWorldDropZoneQuery
    {
        internal bool Enabled { get; set; }
        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = new Vector2Int(4, 7);
            return Enabled;
        }
        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }
        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class ExactSourcePublicationDouble :
        IPhysicalItemExactSourcePublicationService
    {
        private PhysicalItemExactSourcePublicationPlan plan;
        internal int Prepared, Released;
        internal bool BlockRelease;

        public bool TryPrepare(
            PhysicalItemExactSourcePublicationPlan requested,
            out PhysicalItemExactSourcePublicationTransaction transaction,
            out string failureReason)
        {
            plan = requested ?? throw new ArgumentNullException(nameof(requested));
            Prepared++;
            FacilityBufferPublishedOutputStackReceipt[] stacks = plan.Outputs
                .Select((output, index) =>
                    new FacilityBufferPublishedOutputStackReceipt(
                        "stack:faction-causal-route:" + index,
                        output.OutputLineId,
                        output.ItemDefinitionId,
                        output.Quantity,
                        new PhysicalMassGrams(output.Quantity * 100L),
                        string.Empty))
                .ToArray();
            transaction = new PhysicalItemExactSourcePublicationTransaction(
                plan.BatchCommitId,
                plan.DestinationId,
                stacks);
            failureReason = string.Empty;
            return true;
        }

        public bool TryCommitRetained(
            PhysicalItemExactSourcePublicationTransaction transaction,
            out PhysicalItemExactSourcePublicationReceipt receipt,
            out string failureReason) =>
            Commit(transaction, retained: true, out receipt, out failureReason);

        public bool TryCommitReleased(
            PhysicalItemExactSourcePublicationTransaction transaction,
            Vector2Int releasePosition,
            string reasonCode,
            out PhysicalItemExactSourcePublicationReceipt receipt,
            out string failureReason) =>
            Commit(transaction, retained: false, out receipt, out failureReason);

        public bool TryCommitReleased(
            PhysicalItemExactSourcePublicationTransaction transaction,
            FacilityBufferAcknowledgedOutputReleaseTarget target,
            string reasonCode,
            out PhysicalItemExactSourcePublicationReceipt receipt,
            out string failureReason) =>
            Commit(transaction, retained: false, out receipt, out failureReason);

        public bool TryRollback(
            PhysicalItemExactSourcePublicationTransaction transaction,
            string reasonCode,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryReleaseRetained(
            PhysicalItemExactSourcePublicationPlan requested,
            Vector2Int releasePosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            releasedQuantity = 0;
            failureReason = "unsupported fixture operation";
            return false;
        }

        public bool TrySinkRetained(
            PhysicalItemExactSourcePublicationPlan requested,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt disposition,
            out string failureReason)
        {
            disposition = default;
            failureReason = "unsupported fixture operation";
            return false;
        }

        private bool Commit(
            PhysicalItemExactSourcePublicationTransaction transaction,
            bool retained,
            out PhysicalItemExactSourcePublicationReceipt receipt,
            out string failureReason)
        {
            Require(plan != null,
                "Exact-source fixture was committed before prepare.");
            if (!retained && BlockRelease)
            {
                receipt = default;
                failureReason = "b119-controlled-release-block";
                return false;
            }
            if (!retained) Released++;
            var publication = new FacilityBufferPlannedOutputPublicationReceipt(
                "admission:faction-causal-route",
                transaction.BatchCommitId,
                plan.OutcomeFingerprint,
                transaction.DestinationId,
                plan.DropPosition,
                plan.OwnerDomain,
                plan.OwnerOperationId,
                plan.AuthorityOwnerId,
                1L,
                plan.OutcomeFingerprint,
                transaction.PreparedStacks);
            receipt = new PhysicalItemExactSourcePublicationReceipt(
                publication,
                transaction.PreparedStacks.Sum(value => value.MassGrams),
                retained);
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FactionCampaignDouble :
        IFactionCampaignQuery,
        IFactionCampaignCommand,
        IV20CampaignPersistence
    {
        private FactionCampaignStateSaveData relationship;

        internal FactionCampaignDouble(string factionId, int rapport)
        {
            relationship = new FactionCampaignStateSaveData
            {
                factionId = factionId,
                rapport = rapport
            };
        }

        public IReadOnlyList<FactionCampaignStateSaveData> Factions =>
            new[] { relationship };

        public bool TryGetFaction(
            string factionId,
            out FactionCampaignStateSaveData state)
        {
            state = string.Equals(
                factionId,
                relationship.factionId,
                StringComparison.Ordinal)
                ? relationship
                : null;
            return state != null;
        }

        public void ApplyFactionChange(
            string factionId,
            int rapportDelta,
            int grievanceDelta,
            int obligationDelta)
        {
            Require(string.Equals(
                    factionId,
                    relationship.factionId,
                    StringComparison.Ordinal),
                "Faction campaign fixture received an unknown faction.");
            relationship.rapport = Math.Clamp(
                relationship.rapport + rapportDelta,
                -100,
                100);
            relationship.grievance = Math.Clamp(
                relationship.grievance + grievanceDelta,
                0,
                100);
            relationship.obligationTokens = Math.Clamp(
                relationship.obligationTokens + obligationDelta,
                0,
                5);
        }

        public FactionCampaignWorldSaveData CaptureFactions() =>
            CloneFactions(relationship);

        public FactionCampaignAggregateState PrepareFactions(
            FactionCampaignWorldSaveData data) =>
            new() { Data = CloneFactions(data?.factions?.SingleOrDefault()) };

        public FactionCampaignAggregateState PrepareFactionsPreflight(
            FactionCampaignWorldSaveData data) => PrepareFactions(data);

        public FactionCampaignAggregateState PrepareFactionsRestore(
            FactionCampaignWorldSaveData data) => PrepareFactions(data);

        public void PublishFactions(FactionCampaignAggregateState state)
        {
            relationship = CloneRelationship(
                state?.Data?.factions?.SingleOrDefault());
        }

        public bool TryResolveChapter(
            string factionId,
            string choiceId,
            RunMilestoneEvaluationSnapshot requirements,
            out V20ResolvedEventResult result,
            out string failure) => Unsupported(out result, out failure);

        public bool TryAcceptContract(
            string factionId,
            string contractId,
            int absoluteDay,
            out string failure)
        {
            failure = "unsupported fixture command";
            return false;
        }

        public bool TryResolveContract(
            string factionId,
            bool success,
            RunMilestoneEvaluationSnapshot requirements,
            out V20ResolvedEventResult result,
            out string failure) => Unsupported(out result, out failure);

        public SeasonalEventWorldSaveData CaptureSeasonal() => new();
        public SocietyEventWorldSaveData CaptureSociety() => new();
        public RunMilestoneWorldSaveData CaptureMilestones() => new();
        public SeasonalEventAggregateState PrepareSeasonal(
            SeasonalEventWorldSaveData data) => new();
        public SeasonalEventAggregateState PrepareSeasonalRestore(
            SeasonalEventWorldSaveData data) => new();
        public SocietyEventAggregateState PrepareSociety(
            SocietyEventWorldSaveData data) => new();
        public RunMilestoneAggregateState PrepareMilestones(
            RunMilestoneWorldSaveData data) => new();
        public void PublishSeasonal(SeasonalEventAggregateState state) { }
        public void PublishSeasonalRestore(SeasonalEventAggregateState state) { }
        public void PublishSociety(SocietyEventAggregateState state) { }
        public void PublishMilestones(RunMilestoneAggregateState state) { }

        private static bool Unsupported(
            out V20ResolvedEventResult result,
            out string failure)
        {
            result = default;
            failure = "unsupported fixture command";
            return false;
        }

        private static FactionCampaignWorldSaveData CloneFactions(
            FactionCampaignStateSaveData state)
        {
            var data = new FactionCampaignWorldSaveData();
            if (state != null)
                data.factions.Add(CloneRelationship(state));
            return data;
        }

        private static FactionCampaignStateSaveData CloneRelationship(
            FactionCampaignStateSaveData source) => new()
        {
            factionId = source?.factionId ?? string.Empty,
            rapport = source?.rapport ?? 0,
            grievance = source?.grievance ?? 0,
            obligationTokens = source?.obligationTokens ?? 0
        };
    }

    private static FactionAggregateState CreateInitialState(
        ResourceFactionAllianceBenefitBudgetApplicationAdapter allianceBudget)
    {
        if (allianceBudget == null)
            throw new ArgumentNullException(nameof(allianceBudget));
        var state = new FactionAggregateState
        {
            CurrentDay = AbsoluteDay,
            AllianceBenefitBalanceMilliEwu = allianceBudget.CapacityMilliEwu,
            AllianceBenefitRefillRemainder = 0L,
            AllianceBenefitLastRefillDay = AbsoluteDay,
            AllianceBenefitAuthorityDigest = allianceBudget.AuthorityDigest
        };
        state.Factions.Add(
            FactionId,
            new DungeonFactionState
            {
                factionId = FactionId,
                discovered = true,
                homeQ = 2,
                homeR = 3
            });
        return state;
    }

    private static FactionDefinitionSnapshot CreateDefinition() => new(
        FactionId,
        "인과 교역 세력",
        "human",
        "causal faction route fixture",
        Array.Empty<string>(),
        Array.Empty<string>(),
        string.Empty,
        new[]
        {
            new FactionCargoLine
            {
                itemId = ItemId,
                amount = CargoQuantity
            }
        },
        Array.Empty<FactionCargoLine>(),
        FactionRouteEconomicPolicyDescriptor.Create(
            "faction-economy:causal-route-trade",
            1),
        FactionRouteEconomicPolicyDescriptor.Create(
            "faction-economy:causal-route-supply",
            1),
        7,
        20,
        10);

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
