#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class EmploymentSettlementG02DebugScenarios
{
    public static void RunEmploymentObserversForBatch()
    {
        EmploymentContractAlertObserverDebugScenarios.RunOrThrow();
        Debug.Log("B104_EMPLOYMENT_ALERT_OBSERVER_LIFETIME=PASS");
        VerifySettlementEffects(procurement: false);
        Debug.Log("B104_OPERATING_DAY_CHILD_FAILURE_AND_RETRY=PASS");
        VerifySettlementEffects(procurement: true);
        Debug.Log("B104_PROCUREMENT_CHILD_FAILURE_AND_RETRY=PASS");
        MigratedProducerEconomyEntryExtendedE2EDebugScenarios.RunOperatingDayObserversForBatch();
        Debug.Log("B104_EMPLOYMENT_OBSERVERS=PASS");
    }

    private static void VerifySettlementEffects(bool procurement)
    {
        var actors = new List<CharacterActor>();
        try
        {
            var paid = CreateActor(4221, PaidEmployeeId, "B104 Paid", actors);
            var unpaid = CreateActor(4222, UnpaidEmployeeId, "B104 Unpaid", actors);
            var renewed = CreateActor(4223, RenewedMercenaryId, "B104 Renewed", actors);
            var departing = CreateActor(4224, DepartingMercenaryId, "B104 Departing", actors);
            // Controlled actor fixture: exercise the existing lifecycle command,
            // not an unproven EditMode movement coroutine or natural AI walk-out.
            Object.DestroyImmediate(departing.GetComponent<AbilityMove>());
            departing.RefreshAbilityCache();
            var world = new MutableCharacterWorld(actors, actors.Select(a => a.Identity.PersistentId));
            var root = new DungeonRuntimeAggregateRootStore();
            var session = new GameSessionState(96, 2, 1);
            using var scenario = new G02Scenario("b104-" + procurement, world, root, session, true);
            using var facts = new DomainFactDispatcher();
            var delivery = new PostCommitReentryPublisher(facts);
            var auto = procurement ? scenario.CreateProcurement(facts, delivery) : null;
            bool reentryRejected = false;
            if (procurement)
                delivery.AfterCommit = () =>
                {
                    try { auto.ProcessShopRefresh(4, Array.Empty<FacilityShopOffer>(), null); }
                    catch (InvalidOperationException error)
                    { reentryRejected = error.Message == "Auto-procurement refresh is already active."; }
                    Require(reentryRejected, "B104 reentry touched another call's durable boundary.");
                };
            void Execute()
            {
                if (procurement) auto.ProcessShopRefresh(3, Array.Empty<FacilityShopOffer>(), null);
                else scenario.Settle(2);
            }

            string contractsBefore = JsonUtility.ToJson(scenario.Employment.Capture());
            string moodBefore = JsonConvert.SerializeObject(unpaid.Stats.CaptureMoodDeliveryTransactionState());
            int contractFacts = 0, moodFacts = 0, admissionChecks = 0;
            bool saveRejected = false;
            var moodBus = (IGameEventBus)typeof(CharacterStats).GetField("gameEventBus",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(unpaid.Stats);
            using var moodSubscription = moodBus.Subscribe<CharacterMoodFactorAppliedEvent>(value =>
            {
                if (value.Character.Value == UnpaidEmployeeId && value.FactorId == "economy:unpaid-wages")
                    moodFacts++;
            });
            using var alert = new EmploymentContractAlertObserver(scenario.Events);
            alert.Initialize();
            using var contractSubscription = scenario.Events.Subscribe<MercenaryContractEndedEvent>(value =>
            {
                Require(value.CharacterId == DepartingMercenaryId && value.SettlementDay == 2
                    && scenario.LedgerRecords.Any(row => row.transactionId == value.FinanceTransactionId && !row.succeeded),
                    "B104 terminal fact lost its exact failed finance identity.");
                contractFacts++;
                throw new InvalidOperationException("b104-intentional-contract-observer-failure");
            });

            scenario.PaidFault.BeforeSettlement = () =>
            {
                admissionChecks++;
                Require(unpaid.Mood.Factors.Any(value => value.Id == "economy:unpaid-wages")
                    && departing.CurrentLifecycleState == CharacterLifecycleState.Active
                    && contractFacts == 0 && moodFacts == 0,
                    "B104 effects were published or irreversible exit started before the parent committed.");
                try { scenario.Employment.Capture(); }
                catch (InvalidOperationException error)
                { saveRejected = error.Message == "Cannot save during an employment settlement."; }
                Require(saveRejected, "B104 allowed a partial settlement save.");
            };
            scenario.PaidFault.FailNextSettlement = true;
            bool rejected = false;
            try { Execute(); }
            catch (InvalidOperationException error)
            { rejected = error.Message == "b104-intentional-paid-settlement-failure"; }
            Require(rejected && admissionChecks == 1 && scenario.Money == 96
                && scenario.LedgerRecords.Count == 0 && scenario.ReportCount == 0
                && contractFacts == 0 && moodFacts == 0
                && JsonUtility.ToJson(scenario.Employment.Capture()) == contractsBefore
                && JsonConvert.SerializeObject(unpaid.Stats.CaptureMoodDeliveryTransactionState()) == moodBefore
                && departing.CurrentLifecycleState == CharacterLifecycleState.Active
                && (!procurement || auto.Capture().lastProcessedDay == 0),
                "B104 parent failure left partial finance, contract, mood, exit or observation state.");

            bool publicationPending = false;
            try { Execute(); }
            catch (InvalidOperationException error) when (procurement
                && error.Message.StartsWith("Auto-procurement fact publication pending:", StringComparison.Ordinal))
            { publicationPending = true; }
            Require(admissionChecks == 2 && scenario.Money == 0 && scenario.LedgerRecords.Count == 4
                && contractFacts == 1 && moodFacts == 1
                && unpaid.Mood.Factors.Count(value => value.Id == "economy:unpaid-wages") == 1
                && departing.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon
                && scenario.Employment.WageStates.Single(value => value.characterId == DepartingMercenaryId).departed
                && scenario.Employment.LastSettlementEffectFailure.Contains("b104-intentional-contract-observer-failure", StringComparison.Ordinal),
                "B104 successful retry or post-commit observer-failure isolation is incorrect.");
            if (procurement)
                Require(reentryRejected && publicationPending && auto.Capture().pendingFacts.Count == 1,
                    "B104 unknown ACK after rejected reentry did not retain the durable purchase boundary.");
            string contractsCommitted = JsonUtility.ToJson(scenario.Employment.Capture());
            float committedMood = unpaid.Mood.Value;
            Execute();
            Require(admissionChecks == 2 && scenario.Money == 0 && scenario.LedgerRecords.Count == 4
                && contractFacts == 1 && moodFacts == 1 && Mathf.Approximately(unpaid.Mood.Value, committedMood)
                && JsonUtility.ToJson(scenario.Employment.Capture()) == contractsCommitted,
                "B104 retry repeated an already-committed effect.");
            if (procurement)
                Require(auto.Capture().pendingFacts.Count == 0 && auto.LastResults.Count == 1,
                    "B104 pending procurement observation did not recover exactly once.");
            if (!procurement)
            {
                var line = RequireLine(scenario.LatestReport, DepartingMercenaryId);
                Require(line.departureAttempted && line.departurePending && !line.departureStarted
                    && line.departureFailureReason == "employment-departure-awaiting-settlement",
                    "B104 report claimed exit before its actual commit boundary.");
                scenario.RestoreSettlementPayload(scenario.CaptureSettlementPayload());
                Require(RequireLine(scenario.LatestReport, DepartingMercenaryId).departurePending,
                    "B104 admission-time report was rewritten by save/restore.");
            }
            var roundTrip = JsonUtility.FromJson<EmploymentContractSaveData>(contractsCommitted);
            var savedDeparture = roundTrip.wageStates.Single(value => value.characterId == DepartingMercenaryId);
            Require(savedDeparture.departed && !savedDeparture.departurePending
                && savedDeparture.departureFinanceTransactionId.Length > 0,
                "B104 committed departure/finance state did not round-trip.");
        }
        finally { foreach (var actor in actors) DestroyActor(actor); }
    }

    private sealed class PostCommitReentryPublisher : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal Action AfterCommit;
        internal PostCommitReentryPublisher(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T proposed, out DomainFactLease lease, out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T actual)
        {
            var result = inner.Publish(lease, actual);
            if (AfterCommit == null) return result;
            Require(result.MayBeCommitted, "B104 expected an actual committed observation before hiding ACK.");
            var callback = AfterCommit;
            AfterCommit = null;
            callback();
            return DomainFactDelivery.Uncertain("b104-reentry-after-commit-ack-hidden");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results,
            out string failure) => inner.ReplayBatch(facts, results, out failure);
    }

    public class PaidSettlementFaultProxy : DispatchProxy
    {
        internal IPaidFacilityContractRuntime Port { get; private set; }
        private IPaidFacilityContractRuntime target;
        internal Action BeforeSettlement;
        internal bool FailNextSettlement;

        internal static IPaidFacilityContractRuntime Create(IPaidFacilityContractRuntime target,
            out PaidSettlementFaultProxy fault)
        {
            var port = DispatchProxy.Create<IPaidFacilityContractRuntime, PaidSettlementFaultProxy>();
            fault = (PaidSettlementFaultProxy)(object)port;
            fault.Port = port;
            fault.target = target;
            return port;
        }

        protected override object Invoke(MethodInfo method, object[] args)
        {
            if (method.Name == nameof(IPaidFacilityContractRuntime.SettleDay))
            {
                BeforeSettlement?.Invoke();
                if (FailNextSettlement)
                {
                    FailNextSettlement = false;
                    throw new InvalidOperationException("b104-intentional-paid-settlement-failure");
                }
            }
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
#endif
