#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using DungeonStory.ServiceRooms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ServiceHubDayCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:service-hub-day";
    internal const string VisitActionId =
        "action:service-hub-visit";
    internal const string ModeActionId =
        "action:service-hub-switch-direct";
    internal const string CloseDayActionId =
        "action:service-operating-day-close";

    private const string OwnerKind = "owner-kind:service-hub-day";
    private const string DomainStateSchema =
        "dungeonstory-causal-service-hub-day-owner-state@2";
    private const string FacilityIdValue = "building:causal:service-hub";
    private const string VisitorIdValue =
        "character:causal:service-hub-visitor";
    private const string VisitorSpecies = "Slime";
    private const int AbsoluteDay = 17;
    private const int InitialMoney = 1000;

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
                "A service-hub checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            3,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                VisitActionId,
                MigratedProducerOutcomeKind.FacilityVisitEvent,
                context,
                context.CommitVisit,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.FacilityVisitEvent,
                    1),
                lifetime),
            Fixture(
                ModeActionId,
                MigratedProducerOutcomeKind.ServiceModeChangeResult,
                context,
                context.SwitchToDirect,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.ServiceModeChangeResult,
                    2),
                lifetime),
            Fixture(
                CloseDayActionId,
                MigratedProducerOutcomeKind.OperatingDayReportEvent,
                context,
                context.CloseOperatingDay,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind.OperatingDayReportEvent,
                    3),
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue = "service-hub:day:focused";
        JObject visitCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-service-hub:visit",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture visit = fixtures.Get(VisitActionId);
            Require((int)visit.CaptureState()["serviceHubDay.stage"] == 0,
                "Service-hub owner did not start before its visit.");
            Require(visit.Execute(), "The real facility visit failed.");
            Require(visit.RequireExactOutcome().HasCanonicalPayloadHash,
                "The facility-visit outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = visit.CaptureState();
            Require((int)after["serviceHubDay.stage"] == 1
                    && (int)after["serviceHubDay.completedUses"] == 1
                    && (int)after["serviceHubDay.settlementVisits"] == 1,
                "The facility visit did not persist facility and settlement state.");
            visitCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)visit)
                .CaptureOwnerCheckpoint();
        }

        JObject modeCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-service-hub:mode",
                   OwnerIdValue,
                   visitCheckpoint)))
        {
            IProductionCausalActionFixture visit = fixtures.Get(VisitActionId);
            IProductionCausalActionFixture mode = fixtures.Get(ModeActionId);
            Require(!visit.Execute(),
                "Fresh composition replayed the facility visit.");
            Require(mode.Execute(), "The real service-mode command failed.");
            Require(mode.RequireExactOutcome().HasCanonicalPayloadHash,
                "The service-mode outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = mode.CaptureState();
            Require((int)after["serviceHubDay.stage"] == 2
                    && string.Equals(
                        (string)after["serviceHubDay.serviceMode"],
                        ServiceOperationMode.Direct.ToString(),
                        StringComparison.Ordinal)
                    && (int)after["serviceHubDay.settlementVisits"] == 1,
                "The service-mode command did not preserve the earlier visit.");
            modeCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)mode)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-service-hub:close",
                   OwnerIdValue,
                   modeCheckpoint)))
        {
            IProductionCausalActionFixture close = fixtures.Get(CloseDayActionId);
            Require(close.Execute(), "The real operating-day close failed.");
            Require(close.RequireExactOutcome().HasCanonicalPayloadHash,
                "The operating-day outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = close.CaptureState();
            Require((int)after["serviceHubDay.stage"] == 3
                    && (int)after["serviceHubDay.reportCount"] == 1
                    && (int)after["serviceHubDay.latestReportVisits"] == 1,
                "The operating-day report did not retain the earlier visit.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)close)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-service-hub:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture visit = fixtures.Get(VisitActionId);
            IProductionCausalActionFixture mode = fixtures.Get(ModeActionId);
            IProductionCausalActionFixture close = fixtures.Get(CloseDayActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)close)
                .CaptureOwnerCheckpoint();
            Require(!visit.Execute() && !mode.Execute() && !close.Execute(),
                "Terminal service-hub owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)close)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected service-hub replay mutated the checkpoint.");
        }

        JObject useTamper = (JObject)visitCheckpoint.DeepClone();
        ((JObject)useTamper["domainState"])["facilityState"]![
            "completedUses"] = 0;
        Require(RejectCheckpoint(OwnerIdValue, useTamper, "use-tamper"),
            "A service-hub checkpoint with a false use count was accepted.");

        JObject modeTamper = (JObject)modeCheckpoint.DeepClone();
        ((JObject)modeTamper["domainState"])["serviceRooms"]!["hubs"]![0]![
            "mode"] = (int)ServiceOperationMode.Managed;
        Require(RejectCheckpoint(OwnerIdValue, modeTamper, "mode-tamper"),
            "A service-hub checkpoint with a false mode was accepted.");

        JObject visitTamper = (JObject)modeCheckpoint.DeepClone();
        ((JObject)visitTamper["domainState"])["settlement"]![
            "totalVisits"] = 0;
        Require(RejectCheckpoint(OwnerIdValue, visitTamper, "visit-tamper"),
            "A service-hub checkpoint with a false visit ledger was accepted.");

        if (log)
        {
            Debug.Log(
                "Facility visit -> restored service mode -> restored operating-day close causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Service-hub causal owner family returned false.");
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
            "service",
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
                "run:causal-service-hub:" + suffix,
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
        private readonly DomainFactDispatcher modeFacts;
        private readonly OperatingDayFactEditorFixture operatingDayFacts;
        private readonly EditorFacilityUseFactFixture facilityUseFacts;
        private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
        private readonly SessionStateStore session;
        private readonly TreasuryEconomyAggregateStateStore treasury;
        private readonly GameEventBus events;
        private readonly BuildableObject hub;
        private readonly BuildingSO hubData;
        private readonly CharacterActor visitor;
        private readonly CharacterSO visitorData;
        private readonly ServiceSessionRuntime serviceRooms;
        private readonly OperatingDaySettlementApplicationAdapter settlement;
        private readonly BuildingVisitEventPublisher visitPublisher;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":service-hub-ledger";
            aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            modeFacts = CreateModeFacts(outcomes, probe);
            operatingDayFacts = new OperatingDayFactEditorFixture(
                outcomes,
                probe);
            facilityUseFacts = new EditorFacilityUseFactFixture(
                outcomes,
                probe);
            events = new GameEventBus();
            session = new SessionStateStore(new GameSessionState(
                InitialMoney,
                AbsoluteDay));
            treasury = new TreasuryEconomyAggregateStateStore(
                aggregateRootStore);

            hubData = CreateHubData();
            hub = CreateHub(hubData);
            visitorData = CreateVisitorData();
            visitor = CreateVisitor(visitorData);
            var buildingWorld = new FixedBuildingWorld(hub);
            var restoreCandidates = new RestoreWorldCandidateIndex();
            restoreCandidates.SetFacilityCandidate(
                new Grid(2, 2),
                new[] { hub });
            restoreCandidates.SetCharacterCandidate(
                Array.Empty<CharacterActor>());

            CoreSessionRulesSO rulesAsset =
                AssetDatabase.LoadAssetAtPath<CoreSessionRulesSO>(
                    "Assets/Resources/SO/Content/CoreSessionRules.asset")
                ?? throw new InvalidOperationException(
                    "Authored core-session rules are missing.");
            serviceRooms = new ServiceSessionRuntime(
                new DungeonStory.Foundation.UnityUiClock(),
                buildingWorld,
                EmptyServiceLinks.Instance,
                EmptyServiceProcessCatalog.Instance,
                new ServiceSessionRuntime.Dependencies(
                    FixedClock.Instance,
                    new EditorGameMoneyAccount(session.State),
                    AlwaysPowered.Instance,
                    NoServiceResearch.Instance,
                    new FixedRulesProvider(
                        rulesAsset.CreateRuntimeDefinition()),
                    session,
                    modeFacts,
                    modeFacts),
                aggregateRootStore,
                restoreCandidates,
                EmptyPersistentCharacters.Instance);

            GameObject settlementObject = new("Causal Service Hub Settlement");
            settlement = settlementObject.AddComponent<
                OperatingDaySettlementApplicationAdapter>();
            settlement.Construct(
                EmptyBuildingWorld.Instance,
                EmptyCharacterWorld.Instance,
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IFacilityShopCatalog>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IRunVariableRuntimeReader>(),
                session,
                events,
                ZeroEmploymentRuntime.Instance,
                new EditorGameMoneyAccount(session.State),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IPaidFacilityContractRuntime>(),
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                aggregateRootStore);
            settlement.ConstructSettlementFacts(
                operatingDayFacts.Journal,
                session,
                treasury);
            visitPublisher = new BuildingVisitEventPublisher(
                events,
                session,
                facilityUseFacts.Facts,
                facilityUseFacts.Facts);

            try
            {
                if (checkpoint == null)
                {
                    var initialServiceRooms = new ServiceRoomsSaveData();
                    initialServiceRooms.hubs.Add(new ServiceHubModeSaveData
                    {
                        hubId = FacilityIdValue,
                        mode = ServiceOperationMode.Managed
                    });
                    serviceRooms.PublishRestoreCandidate(
                        serviceRooms.PrepareRestoreCandidate(
                            initialServiceRooms));
                    settlement.OnTriggerEvent(
                        new OperatingDayStartedEvent(AbsoluteDay));
                }
                else
                {
                    RestoreOwnerCheckpoint(checkpoint);
                }
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
        internal string PersistentOwnerKind =>
            HasPersistentOwner ? OwnerKind : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            OperatingDayReport latest = settlement.LatestReport;
            return new Dictionary<string, object>
            {
                ["serviceHubDay.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "service-hub-day:ephemeral",
                ["serviceHubDay.stage"] = Stage,
                ["serviceHubDay.facilityId"] = FacilityIdValue,
                ["serviceHubDay.visitorId"] = VisitorIdValue,
                ["serviceHubDay.calendarDay"] = session.State.day.Value,
                ["serviceHubDay.completedUses"] =
                    hub.FacilityState.completedUses,
                ["serviceHubDay.serviceMode"] =
                    serviceRooms.GetHubSnapshot(hub).Mode.ToString(),
                ["serviceHubDay.settlementDay"] = settlement.CurrentDay,
                ["serviceHubDay.settlementVisits"] =
                    settlement.CurrentVisits,
                ["serviceHubDay.reportCount"] =
                    settlement.ReportHistory.Count,
                ["serviceHubDay.latestReportDay"] = latest?.day ?? 0,
                ["serviceHubDay.latestReportVisits"] =
                    latest?.totalVisits ?? 0,
                ["serviceHubDay.money"] = session.State.holdingMoney.Value,
                ["serviceHubDay.visitOwnerRevision"] = OwnerRevision(
                    MigratedProducerOutcomeKind.FacilityVisitEvent,
                    VisitOwnerIdentity),
                ["serviceHubDay.modeOwnerRevision"] = OwnerRevision(
                    MigratedProducerOutcomeKind.ServiceModeChangeResult,
                    ModeOwnerIdentity),
                ["serviceHubDay.dayOwnerRevision"] = OwnerRevision(
                    MigratedProducerOutcomeKind.OperatingDayReportEvent,
                    DayOwnerIdentity),
                ["serviceHubDay.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool CommitVisit()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            visitPublisher.CommitVisit(
                CharacterActor.From(visitor),
                hub);
            bool valid = Stage == 1;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool SwitchToDirect()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            ServiceModeChangeResult result = serviceRooms.SwitchToDirect(hub);
            bool valid = result.Succeeded
                && result.PreviousMode == ServiceOperationMode.Managed
                && result.RequestedMode == ServiceOperationMode.Direct
                && Stage == 2;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool CloseOperatingDay()
        {
            ThrowIfDisposed();
            if (Stage != 2)
                return false;
            settlement.OnTriggerEvent(
                new OperatingDayEndedEvent(AbsoluteDay));
            bool valid = Stage == 3;
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
            int expectedSubjectCount =
                kind == MigratedProducerOutcomeKind.FacilityVisitEvent
                    ? 2
                    : 1;
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.commitRevision != 1L
                || exact.ownerRevision != 1L
                || exact.absoluteDay != AbsoluteDay
                || exact.status != GameplayOutcomeStatus.Succeeded
                || !string.Equals(
                    exact.producerId,
                    definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || exact.subjects?.Count != expectedSubjectCount
                || exact.participants?.Count != expectedSubjectCount
                || exact.facts == null
                || !exact.facts.Any(value => string.Equals(
                    value.factId,
                    MigratedProducerOutcomeIds.SummaryFact.Value,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Service-hub production entry did not publish its exact outcome: "
                    + kind);
            }
            if (kind == MigratedProducerOutcomeKind.FacilityVisitEvent)
            {
                Require(
                    exact.subjects.Any(value =>
                        string.Equals(
                            value.entityKindId,
                            MigratedProducerOutcomeIds.CharacterKind.Value,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.entityId,
                            VisitorIdValue,
                            StringComparison.Ordinal))
                    && exact.subjects.Any(value =>
                        string.Equals(
                            value.entityKindId,
                            MigratedProducerOutcomeIds.FacilityKind.Value,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.entityId,
                            FacilityIdValue,
                            StringComparison.Ordinal)),
                    "Facility visit did not retain its actor and facility subjects.");
                Require(
                    exact.participants.Any(value =>
                        string.Equals(
                            value.entityKindId,
                            MigratedProducerOutcomeIds.CharacterKind.Value,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.entityId,
                            VisitorIdValue,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.roleId,
                            MigratedProducerOutcomeIds.ActorRole.Value,
                            StringComparison.Ordinal))
                    && exact.participants.Any(value =>
                        string.Equals(
                            value.entityKindId,
                            MigratedProducerOutcomeIds.FacilityKind.Value,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.entityId,
                            FacilityIdValue,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.roleId,
                            MigratedProducerOutcomeIds.FacilityRole.Value,
                            StringComparison.Ordinal)),
                    "Facility visit did not retain its actor and facility participant roles.");
            }
            else if (kind == MigratedProducerOutcomeKind.ServiceModeChangeResult)
            {
                Require(string.Equals(
                        exact.subjects[0].entityId,
                        FacilityIdValue,
                        StringComparison.Ordinal),
                    "Service-hub outcome subject is not the stable facility.");
            }
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral service-hub fixtures have no checkpoint.");
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
                    ["facilityId"] = FacilityIdValue,
                    ["visitorId"] = VisitorIdValue,
                    ["session"] = new JObject
                    {
                        ["day"] = session.State.day.Value,
                        ["money"] = session.State.holdingMoney.Value
                    },
                    ["facilityState"] = JObject.Parse(
                        JsonUtility.ToJson(hub.FacilityState.Clone())),
                    ["serviceRooms"] = JObject.Parse(
                        JsonUtility.ToJson(serviceRooms.Capture())),
                    ["settlement"] = CaptureSettlement(
                        settlement.CapturePersistentState()),
                    ["treasury"] = CaptureTreasury(treasury.Current),
                    ["operatingDayFacts"] = JObject.Parse(
                        JsonUtility.ToJson(
                            operatingDayFacts.Journal.Capture())),
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
            serviceRooms?.Dispose();
            modeFacts?.Dispose();
            operatingDayFacts?.Dispose();
            facilityUseFacts?.Dispose();
            if (settlement != null)
                Object.DestroyImmediate(settlement.gameObject);
            if (visitorData != null)
                Object.DestroyImmediate(visitorData);
            if (visitor != null)
                Object.DestroyImmediate(visitor.gameObject);
            if (hub != null)
                Object.DestroyImmediate(hub.gameObject);
            if (hubData != null)
                Object.DestroyImmediate(hubData);
        }

        private static DomainFactDispatcher CreateModeFacts(
            MigratedProducerOutcomeEditorFixture outcomes,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            var dispatcher = new DomainFactDispatcher();
            try
            {
                dispatcher.Subscribe(
                    "fixture:causal-service-mode-changed",
                    new MigratedProducerDomainFactProbeObserver<
                        ServiceModeChangedFact>(
                        ServiceModeChangedFactMapping.Capture,
                        outcomes.Transaction,
                        new GameplayDomainFactCommitGroup(
                            outcomes.Recorder,
                            outcomes.Ledger),
                        probe));
                return dispatcher;
            }
            catch
            {
                dispatcher.Dispose();
                throw;
            }
        }

        private string VisitOwnerIdentity =>
            "facility-visit:facility=" + FacilityIdValue + ":use=1";
        private string ModeOwnerIdentity =>
            "service-mode-change:hub=" + FacilityIdValue
            + ":command=set-mode:Direct";
        private string DayOwnerIdentity =>
            "operating-day-report:day=" + AbsoluteDay;

        private int Stage
        {
            get
            {
                int visits = TypeCount(
                    MigratedProducerOutcomeKind.FacilityVisitEvent);
                int modes = TypeCount(
                    MigratedProducerOutcomeKind.ServiceModeChangeResult);
                int days = TypeCount(
                    MigratedProducerOutcomeKind.OperatingDayReportEvent);
                int exact = ExactOutcomeCount();
                long visitRevision = OwnerRevision(
                    MigratedProducerOutcomeKind.FacilityVisitEvent,
                    VisitOwnerIdentity);
                long modeRevision = OwnerRevision(
                    MigratedProducerOutcomeKind.ServiceModeChangeResult,
                    ModeOwnerIdentity);
                long dayRevision = OwnerRevision(
                    MigratedProducerOutcomeKind.OperatingDayReportEvent,
                    DayOwnerIdentity);
                if (visits == 0 && modes == 0 && days == 0 && exact == 0
                    && visitRevision == 0 && modeRevision == 0
                    && dayRevision == 0)
                {
                    return 0;
                }
                if (visits == 1 && modes == 0 && days == 0 && exact == 1
                    && visitRevision == 1 && modeRevision == 0
                    && dayRevision == 0)
                {
                    return 1;
                }
                if (visits == 1 && modes == 1 && days == 0 && exact == 2
                    && visitRevision == 1 && modeRevision == 1
                    && dayRevision == 0)
                {
                    return 2;
                }
                if (visits == 1 && modes == 1 && days == 1 && exact == 3
                    && visitRevision == 1 && modeRevision == 1
                    && dayRevision == 1)
                {
                    return 3;
                }
                return -1;
            }
        }

        private int TypeCount(MigratedProducerOutcomeKind kind)
        {
            string outcomeType = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        outcomeType,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() =>
            outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;

        private long OwnerRevision(
            MigratedProducerOutcomeKind kind,
            string identity)
        {
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            string expectedKey = definition.ProducerId + "|" + identity;
            MigratedProducerOwnerRevisionSaveData owner = outcomes.Transaction
                .Capture().owners.SingleOrDefault(value => string.Equals(
                    value.ownerKey,
                    expectedKey,
                    StringComparison.Ordinal));
            return owner?.revision ?? 0L;
        }

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
                "service-hub owner state",
                "schemaVersion",
                "schema",
                "stage",
                "facilityId",
                "visitorId",
                "session",
                "facilityState",
                "serviceRooms",
                "settlement",
                "treasury",
                "operatingDayFacts",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 2
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["facilityId"],
                    FacilityIdValue,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["visitorId"],
                    VisitorIdValue,
                    StringComparison.Ordinal)
                || state["session"] is not JObject savedSession
                || state["facilityState"] is not JObject savedFacility
                || state["serviceRooms"] is not JObject savedServiceRooms
                || state["settlement"] is not JObject savedSettlement
                || state["treasury"] is not JObject savedTreasury
                || state["operatingDayFacts"] is not JObject savedDayFacts
                || state["outcomeOwners"] is not JObject savedOwners)
            {
                throw new InvalidOperationException(
                    "Service-hub checkpoint identity changed.");
            }
            RequireFields(savedSession, "service-hub session", "day", "money");
            session.State.day.Initialize((int)savedSession["day"]);
            session.State.holdingMoney.Initialize((int)savedSession["money"]);
            hub.RestoreFacilityState(
                JsonUtility.FromJson<FacilityRuntimeState>(
                    savedFacility.ToString(Formatting.None)));
            serviceRooms.PublishRestoreCandidate(
                serviceRooms.PrepareRestoreCandidate(
                    JsonUtility.FromJson<ServiceRoomsSaveData>(
                        savedServiceRooms.ToString(Formatting.None))));
            settlement.RestorePersistentState(
                RestoreSettlement(savedSettlement));
            treasury.Replace(RestoreTreasury(savedTreasury));
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    savedOwners.ToString(Formatting.None))));
            operatingDayFacts.Journal.Restore(
                JsonUtility.FromJson<OperatingDayFactJournalData>(
                    savedDayFacts.ToString(Formatting.None)));
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Service-hub checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            ServiceOperationMode mode = serviceRooms.GetHubSnapshot(hub).Mode;
            OperatingDayReport latest = settlement.LatestReport;
            bool stageShape = stage switch
            {
                0 => hub.FacilityState.completedUses == 0
                    && mode == ServiceOperationMode.Managed
                    && settlement.CurrentVisits == 0
                    && settlement.ReportHistory.Count == 0,
                1 => hub.FacilityState.completedUses == 1
                    && mode == ServiceOperationMode.Managed
                    && settlement.CurrentVisits == 1
                    && settlement.ReportHistory.Count == 0,
                2 => hub.FacilityState.completedUses == 1
                    && mode == ServiceOperationMode.Direct
                    && settlement.CurrentVisits == 1
                    && settlement.ReportHistory.Count == 0,
                3 => hub.FacilityState.completedUses == 1
                    && mode == ServiceOperationMode.Direct
                    && settlement.ReportHistory.Count == 1
                    && latest != null
                    && latest.day == AbsoluteDay
                    && latest.totalVisits == 1,
                _ => false
            };
            if (!stageShape
                || !string.Equals(
                    hub.RequirePersistentInstanceId().Value,
                    FacilityIdValue,
                    StringComparison.Ordinal)
                || !string.Equals(
                    visitor.Identity.PersistentId,
                    VisitorIdValue,
                    StringComparison.Ordinal)
                || session.State.day.Value != AbsoluteDay
                || session.State.holdingMoney.Value != InitialMoney
                || settlement.CurrentDay != AbsoluteDay)
            {
                throw new InvalidOperationException(
                    "Service-hub facility, mode, settlement and ledger state diverged: "
                    + "stage=" + stage
                    + "; uses=" + hub.FacilityState.completedUses
                    + "; mode=" + mode
                    + "; visits=" + settlement.CurrentVisits
                    + "; reports=" + settlement.ReportHistory.Count
                    + "; money=" + session.State.holdingMoney.Value);
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

    private sealed class SessionStateStore :
        IGameSessionStateStore,
        IGameSessionStateProvider
    {
        internal SessionStateStore(GameSessionState state) =>
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

    private sealed class FixedBuildingWorld : IBuildingWorldQuery
    {
        internal FixedBuildingWorld(BuildableObject hub) =>
            Buildings = new[]
            {
                hub ?? throw new ArgumentNullException(nameof(hub))
            };
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        internal static readonly EmptyBuildingWorld Instance = new();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class EmptyCharacterWorld : ICharacterWorldQuery
    {
        internal static readonly EmptyCharacterWorld Instance = new();
        public int CharacterVersion => 0;
        public IReadOnlyList<CharacterActor> Characters =>
            Array.Empty<CharacterActor>();
    }

    private sealed class EmptyPersistentCharacters :
        ICharacterWorldPersistenceIdentityQuery
    {
        internal static readonly EmptyPersistentCharacters Instance = new();
        public IReadOnlyCollection<CharacterId> GetPersistentCharacterIds() =>
            Array.Empty<CharacterId>();
        public IReadOnlyCollection<CharacterId> GetPersistentActorIds() =>
            Array.Empty<CharacterId>();
    }

    private sealed class EmptyServiceProcessCatalog : IServiceProcessCatalog
    {
        internal static readonly EmptyServiceProcessCatalog Instance = new();
        public IReadOnlyList<ServiceProcessSO> All =>
            Array.Empty<ServiceProcessSO>();
        public bool TryGet(string processId, out ServiceProcessSO process)
        {
            process = null;
            return false;
        }
        public ServiceProcessSO Require(string processId) =>
            throw new KeyNotFoundException(processId ?? string.Empty);
    }

    private sealed class EmptyServiceLinks : IServiceRoomLinkRuntime
    {
        internal static readonly EmptyServiceLinks Instance = new();
        public int Version => 0;
        public IReadOnlyList<ServiceSupportLinkSnapshot> GetLinks(
            BuildableObject hub) => Array.Empty<ServiceSupportLinkSnapshot>();
        public bool TryGetLinkForSupport(
            BuildableObject support,
            out ServiceSupportLinkSnapshot link)
        {
            link = null;
            return false;
        }
        public bool HasFeatures(
            BuildableObject hub,
            IReadOnlyList<string> requiredFeatureTags,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return requiredFeatureTags == null
                || requiredFeatureTags.All(string.IsNullOrWhiteSpace);
        }
        public bool TryResolveFeature(
            BuildableObject hub,
            string featureTag,
            out BuildableObject support,
            out BuildingServiceSupportAbility ability)
        {
            support = null;
            ability = null;
            return false;
        }
    }

    private sealed class AlwaysPowered : IPowerInfrastructureQuery
    {
        internal static readonly AlwaysPowered Instance = new();
        public int Version => 0;
        public IReadOnlyList<PowerNetworkSnapshot> Networks =>
            Array.Empty<PowerNetworkSnapshot>();
        public bool IsPowered(BuildableObject building) => true;
        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }

    private sealed class NoServiceResearch : IServiceRoomResearchQuery
    {
        internal static readonly NoServiceResearch Instance = new();
        public bool IsCompleted(string researchId) => false;
    }

    private sealed class FixedRulesProvider : ICoreSessionRulesProvider
    {
        internal FixedRulesProvider(CoreSessionRulesDefinition rules) =>
            CoreSessionRules = rules
                ?? throw new ArgumentNullException(nameof(rules));
        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    private sealed class FixedClock : IGameClock
    {
        internal static readonly FixedClock Instance = new();
        public float DeltaTime => 1f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class ZeroEmploymentRuntime : IEmploymentContractRuntime
    {
        internal static readonly ZeroEmploymentRuntime Instance = new();
        public IReadOnlyList<EmployeeWageState> WageStates =>
            Array.Empty<EmployeeWageState>();
        public IReadOnlyList<MercenaryContract> MercenaryContracts =>
            Array.Empty<MercenaryContract>();
        public IEmploymentSettlementScope BeginSettlement() =>
            new NoopSettlementScope();
        public int ForecastCost(int days) => 0;
        public int GetDailyCost(string characterId) => 0;
        public int QuoteMercenaryDailyCost(
            string characterId,
            int level,
            int rolePremium) => 0;
        public EmploymentDailySettlement SettleDay(int day) => new()
        {
            day = Math.Max(1, day)
        };
        public bool TryHireMercenary(
            CharacterActor actor,
            int rolePremium,
            int day,
            out string failureReason)
        {
            failureReason = "not used";
            return false;
        }
        public bool SetEmployeeRolePremium(
            string characterId,
            int premium,
            out string failureReason)
        {
            failureReason = "not used";
            return false;
        }
        public EmploymentContractSaveData Capture() => new();

        private sealed class NoopSettlementScope :
            IEmploymentSettlementScope
        {
            public void Commit() { }
            public void Dispose() { }
        }
    }

    private static BuildingSO CreateHubData()
    {
        BuildingSO source = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/D04_배식카운터.asset")
            ?? throw new InvalidOperationException(
                "The authored Direct service-hub fixture asset is missing.");
        return Object.Instantiate(source);
    }

    private static BuildableObject CreateHub(BuildingSO data)
    {
        GameObject gameObject = new("Causal Service Hub");
        BuildableObject hub = gameObject.AddComponent<BuildableObject>();
        hub.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        CharacterAiEditorTestDependencies.Inject(hub);
        hub.Initialization(data, Vector2Int.zero);
        hub.RestorePersistentIdentity(new BuildingInstanceId(FacilityIdValue));
        return hub;
    }

    private static CharacterSO CreateVisitorData() =>
        CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
            CharacterType.Customer,
            "영업일 방문자",
            VisitorSpecies);

    private static CharacterActor CreateVisitor(CharacterSO data)
    {
        GameObject gameObject = new("Causal Service Hub Visitor");
        gameObject.AddComponent<SpriteRenderer>();
        CharacterAiEditorTestDependencies.EnsureCharacterProgression(
            gameObject);
        CharacterActor actor = gameObject.AddComponent<CharacterActor>();
        CharacterAiEditorTestDependencies.Inject(gameObject);
        actor.Initialization(data);
        actor.characterType = CharacterType.Customer;
        actor.Identity.SetPersistentId(VisitorIdValue);
        actor.stats[CharacterCondition.MOOD] = 90f;
        return actor;
    }

    private static JObject CaptureSettlement(
        OperatingDaySettlementPersistenceState state)
    {
        Require(state != null, "Operating-day persistence state is missing.");
        Require(state.StockSupplyResults.Count == 0
                && state.FacilityRevenue.Count == 0
                && state.ConsumedStock.Count == 0
                && state.Incidents.Count == 0
                && state.EventLog.Count == 0,
            "Unexpected operating-day state escaped the closed fixture.");
        return new JObject
        {
            ["currentDay"] = state.CurrentDay,
            ["totalRevenue"] = state.TotalRevenue,
            ["totalVisits"] = state.TotalVisits,
            ["restockFailureCount"] = state.RestockFailureCount,
            ["facilityDetails"] = new JArray(state.FacilityDetails.Select(value =>
                new JObject
                {
                    ["facilityId"] = value.FacilityId,
                    ["facilityName"] = value.FacilityName,
                    ["revenue"] = value.Revenue,
                    ["visitCount"] = value.VisitCount,
                    ["operatingCost"] = value.OperatingCost,
                    ["consumedStock"] = new JArray(value.ConsumedStock.Select(
                        pair => new JObject
                        {
                            ["category"] = StockCategoryPersistenceId.ToId(
                                (StockCategory)pair.Key),
                            ["amount"] = pair.Value
                        })),
                    ["visitorMoodSamples"] = new JArray(
                        value.VisitorMoodSamples)
                })),
            ["speciesVisits"] = new JObject(state.SpeciesVisits
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => new JProperty(value.Key, value.Value))),
            ["visitorMoodSamples"] = new JArray(state.VisitorMoodSamples),
            ["reports"] = new JArray(state.ReportHistory.Select(CaptureReport)),
            ["outstandingDebt"] = state.OutstandingDebt,
            ["consecutiveShortfallDays"] = state.ConsecutiveShortfallDays,
            ["emergencyFundingUsed"] = state.EmergencyFundingUsed
        };
    }

    private static OperatingDaySettlementPersistenceState RestoreSettlement(
        JObject state)
    {
        RequireFields(
            state,
            "service-hub settlement state",
            "currentDay",
            "totalRevenue",
            "totalVisits",
            "restockFailureCount",
            "facilityDetails",
            "speciesVisits",
            "visitorMoodSamples",
            "reports",
            "outstandingDebt",
            "consecutiveShortfallDays",
            "emergencyFundingUsed");
        var species = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (JProperty property in ((JObject)state["speciesVisits"])
                     .Properties())
        {
            if (string.IsNullOrWhiteSpace(property.Name)
                || !species.TryAdd(property.Name, (int)property.Value))
            {
                throw new InvalidOperationException(
                    "Service-hub species visit state is invalid.");
            }
        }
        OperatingDayFacilityLedgerEntry[] facilityDetails =
            RestoreFacilityDetails((JArray)state["facilityDetails"]);
        OperatingDayReport[] reports = ((JArray)state["reports"])
            .Cast<JObject>()
            .Select(RestoreReport)
            .ToArray();
        return new OperatingDaySettlementPersistenceState(
            (int)state["currentDay"],
            (int)state["totalRevenue"],
            (int)state["totalVisits"],
            (int)state["restockFailureCount"],
            facilityDetails,
            species,
            new Dictionary<StockCategory, int>(),
            ((JArray)state["visitorMoodSamples"])
                .Select(value => (float)value)
                .ToArray(),
            Array.Empty<StockSupplyResult>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            reports,
            (int)state["outstandingDebt"],
            (int)state["consecutiveShortfallDays"],
            (bool)state["emergencyFundingUsed"]);
    }

    private static OperatingDayFacilityLedgerEntry[] RestoreFacilityDetails(
        JArray values)
    {
        return (values ?? throw new InvalidOperationException(
                "Service-hub facility details are missing."))
            .Cast<JObject>()
            .Select(value =>
            {
                RequireFields(
                    value,
                    "service-hub facility detail",
                    "facilityId",
                    "facilityName",
                    "revenue",
                    "visitCount",
                    "operatingCost",
                    "consumedStock",
                    "visitorMoodSamples");
                Dictionary<int, int> consumed =
                    ((JArray)value["consumedStock"])
                    .Cast<JObject>()
                    .ToDictionary(
                        stock =>
                        {
                            RequireFields(
                                stock,
                                "service-hub facility stock",
                                "category",
                                "amount");
                            if (!StockCategoryPersistenceId.TryParse(
                                    (string)stock["category"],
                                    out StockCategory category))
                            {
                                throw new InvalidOperationException(
                                    "Service-hub facility stock category is invalid.");
                            }
                            return (int)category;
                        },
                        stock => (int)stock["amount"]);
                return new OperatingDayFacilityLedgerEntry(
                    (string)value["facilityId"],
                    (string)value["facilityName"],
                    (int)value["revenue"],
                    (int)value["visitCount"],
                    (int)value["operatingCost"],
                    consumed,
                    ((JArray)value["visitorMoodSamples"])
                        .Select(sample => (float)sample)
                        .ToArray());
            })
            .ToArray();
    }

    private static JObject CaptureReport(OperatingDayReport report)
    {
        Require(report != null, "Operating-day report is missing.");
        Require(report.facilityRevenues.Count == 0
                && report.incidents.Count == 0
                && report.damagedFacilities.Count == 0
                && report.stockShortageFacilities.Count == 0
                && report.staffComplaintEvents.Count == 0
                && report.eventLog.Count == 0
                && report.unlockedCodexInfo.Count == 0
                && report.warehouseStocks.Count == 0
                && report.stockConsumed.Count == 0
                && report.stockSupplyResults.Count == 0
                && report.refreshedFacilityShopOffers.Count == 0,
            "Unexpected report detail escaped the closed fixture: "
            + "facilityRevenue=" + report.facilityRevenues.Count
            + "; incidents=" + report.incidents.Count
            + "; damaged=" + report.damagedFacilities.Count
            + "; shortage=" + report.stockShortageFacilities.Count
            + "; complaints=" + report.staffComplaintEvents.Count
            + "; eventLog=" + report.eventLog.Count
            + "; codex=" + report.unlockedCodexInfo.Count
            + "; warehouses=" + report.warehouseStocks.Count
            + "; consumed=" + report.stockConsumed.Count
            + "; supply=" + report.stockSupplyResults.Count
            + "; dailyOffers=" + report.refreshedDailyShopOffers.Count
            + "; facilityOffers=" + report.refreshedFacilityShopOffers.Count);
        return new JObject
        {
            ["day"] = report.day,
            ["totalRevenue"] = report.totalRevenue,
            ["totalVisits"] = report.totalVisits,
            ["averageSatisfaction"] = report.averageSatisfaction,
            ["repairCost"] = report.repairCost,
            ["restockFailureCount"] = report.restockFailureCount,
            ["facilityDetails"] = new JArray(report.facilityDetails.Select(value =>
                new JObject
                {
                    ["facilityId"] = value.FacilityId,
                    ["facilityName"] = value.FacilityName,
                    ["revenue"] = value.Revenue,
                    ["visitCount"] = value.VisitCount,
                    ["operatingCost"] = value.OperatingCost,
                    ["consumedStock"] = new JArray(value.ConsumedStock.Select(
                        pair => new JObject
                        {
                            ["category"] = StockCategoryPersistenceId.ToId(
                                (StockCategory)pair.Key),
                            ["amount"] = pair.Value
                        })),
                    ["visitorMoodSamples"] = new JArray(
                        value.VisitorMoodSamples)
                })),
            ["speciesVisits"] = new JArray(report.speciesVisits.Select(value =>
                new JObject
                {
                    ["speciesTag"] = value.speciesTag,
                    ["visitCount"] = value.visitCount
                })),
            ["dailyOffers"] = new JArray(
                report.refreshedDailyShopOffers.Select(value => new JObject
                {
                    ["category"] = StockCategoryPersistenceId.ToId(
                        value.category),
                    ["itemId"] = value.itemId,
                    ["amount"] = value.amount,
                    ["cost"] = value.cost,
                    ["sourceLabel"] = value.sourceLabel ?? string.Empty
                })),
            ["staffCount"] = report.staffSummary.staffCount,
            ["workingCount"] = report.staffSummary.workingCount,
            ["offDutyCount"] = report.staffSummary.offDutyCount,
            ["averageSleep"] = report.staffSummary.averageSleep,
            ["averageMood"] = report.staffSummary.averageMood,
            ["maintenanceCost"] = report.maintenanceCost,
            ["payrollCost"] = report.payrollCost,
            ["previousDebt"] = report.previousDebt,
            ["paidOperatingCost"] = report.paidOperatingCost,
            ["unpaidOperatingCost"] = report.unpaidOperatingCost,
            ["closingBalance"] = report.closingBalance,
            ["consecutiveShortfallDays"] = report.consecutiveShortfallDays
        };
    }

    private static OperatingDayReport RestoreReport(JObject report)
    {
        RequireFields(
            report,
            "service-hub report",
            "day",
            "totalRevenue",
            "totalVisits",
            "averageSatisfaction",
            "repairCost",
            "restockFailureCount",
            "facilityDetails",
            "speciesVisits",
            "dailyOffers",
            "staffCount",
            "workingCount",
            "offDutyCount",
            "averageSleep",
            "averageMood",
            "maintenanceCost",
            "payrollCost",
            "previousDebt",
            "paidOperatingCost",
            "unpaidOperatingCost",
            "closingBalance",
            "consecutiveShortfallDays");
        OperatingDayFacilityLedgerEntry[] facilityDetails =
            RestoreFacilityDetails((JArray)report["facilityDetails"]);
        SpeciesVisitSummary[] species = ((JArray)report["speciesVisits"])
            .Cast<JObject>()
            .Select(value => new SpeciesVisitSummary(
                (string)value["speciesTag"],
                (int)value["visitCount"]))
            .ToArray();
        StockDeliveryOffer[] dailyOffers = ((JArray)report["dailyOffers"])
            .Cast<JObject>()
            .Select(value =>
            {
                RequireFields(
                    value,
                    "service-hub daily offer",
                    "category",
                    "itemId",
                    "amount",
                    "cost",
                    "sourceLabel");
                if (!StockCategoryPersistenceId.TryParse(
                        (string)value["category"],
                        out StockCategory category))
                {
                    throw new InvalidOperationException(
                        "Service-hub daily offer category is invalid.");
                }
                return new StockDeliveryOffer(
                    category,
                    (string)value["itemId"],
                    (int)value["amount"],
                    (int)value["cost"],
                    (string)value["sourceLabel"]);
            })
            .ToArray();
        return OperatingDayReport.Create(
            (int)report["day"],
            (int)report["totalRevenue"],
            (int)report["totalVisits"],
            (float)report["averageSatisfaction"],
            (int)report["repairCost"],
            (int)report["restockFailureCount"],
            speciesVisits: species,
            staffSummary: new StaffWorkSummary(
                (int)report["staffCount"],
                (int)report["workingCount"],
                (int)report["offDutyCount"],
                (float)report["averageSleep"],
                (float)report["averageMood"]),
            refreshedDailyShopOffers: dailyOffers,
            maintenanceCost: (int)report["maintenanceCost"],
            payrollCost: (int)report["payrollCost"],
            previousDebt: (int)report["previousDebt"],
            paidOperatingCost: (int)report["paidOperatingCost"],
            unpaidOperatingCost: (int)report["unpaidOperatingCost"],
            closingBalance: (int)report["closingBalance"],
            consecutiveShortfallDays:
                (int)report["consecutiveShortfallDays"],
            facilityDetails: facilityDetails);
    }

    private static JObject CaptureTreasury(TreasuryEconomyAggregateState state)
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
            "Unexpected treasury state escaped the closed fixture.");
        return new JObject
        {
            ["nextSequence"] = state.Ledger.NextSequence,
            ["dailyBudget"] = state.DailyBudget,
            ["minimumReserve"] = state.MinimumReserve,
            ["lastProcessedDay"] = state.LastProcessedDay
        };
    }

    private static TreasuryEconomyAggregateState RestoreTreasury(JObject state)
    {
        RequireFields(
            state,
            "service-hub treasury state",
            "nextSequence",
            "dailyBudget",
            "minimumReserve",
            "lastProcessedDay");
        return new TreasuryEconomyAggregateState
        {
            Ledger = new EconomyTransactionLedgerState
            {
                NextSequence = (int)state["nextSequence"]
            },
            DailyBudget = (int)state["dailyBudget"],
            MinimumReserve = (int)state["minimumReserve"],
            LastProcessedDay = (int)state["lastProcessedDay"]
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
        string[] required = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(required, StringComparer.Ordinal))
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
