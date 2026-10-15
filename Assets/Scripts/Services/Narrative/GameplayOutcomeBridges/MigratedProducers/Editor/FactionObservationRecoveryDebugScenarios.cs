#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal sealed class FactionObservationTestTransport :
    IDomainFactPublisher,
    IDomainFactRecoveryPublisher
{
    private readonly DomainFactDispatcher inner;
    internal Type Target;
    internal int Mode = -1; // 0: unknown before commit; 1: unknown after commit; 2: throw after commit
    internal bool BlockRecovery;
    internal int ReplayBatchCalls;
    internal Action During;
    internal FactionObservationTestTransport(DomainFactDispatcher inner) => this.inner = inner;
    public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => inner.TryReserve(fact, out lease, out failure);
    public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
    {
        if (Target != typeof(T) || Mode < 0) return inner.Publish(lease, fact);
        During?.Invoke();
        if (Mode is 1 or 2)
        {
            DomainFactDelivery committed = inner.Publish(lease, fact);
            if (Mode == 2)
            {
                throw new InvalidOperationException(
                    "b130-controlled-after-commit-transport-exception");
            }
            if (!committed.MayBeCommitted) return committed;
        }
        return DomainFactDelivery.Uncertain("b120-controlled-ack-loss");
    }
    public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
    {
        if (Target != typeof(FactionRouteArrivedDomainFact) || Mode < 0) return inner.PublishBatch(facts, results, out failure);
        During?.Invoke();
        if (Mode == 1 && !inner.PublishBatch(facts, results, out failure)) return false;
        for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Uncertain("b120-controlled-arrival-ack-loss");
        failure = string.Empty;
        return true;
    }
    public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
    public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    public bool ReplayBatch(
        IReadOnlyList<DomainFactReplay> facts,
        DomainFactDelivery[] results,
        out string failure)
    {
        ReplayBatchCalls++;
        if (BlockRecovery)
        {
            failure = "b130-controlled-relationship-recovery-failure";
            return false;
        }
        return inner.ReplayBatch(facts, results, out failure);
    }
}

public static partial class FactionRouteCausalProductionEntryDebugScenarios
{
    public static void RunFactionObservationBatch()
    {
        MigratedProducerRunOffenseEntryE2EDebugScenarios.RunFactionObserverRemainderFocused();
        RunCargoObserverBatch();
        foreach (bool arrival in new[] { false, true })
        for (int mode = 0; mode < 2; mode++)
        {
            string owner = "faction-route:b120:" + arrival + ":" + mode;
            FactionObservationTestTransport transport = null;
            RouteObservationBundle saved;
            using (var context = new Context("run:b120", owner, null, dispatcher => transport = new FactionObservationTestTransport(dispatcher)))
                saved = context.ExerciseRouteAcknowledgementLoss(transport, arrival, mode);
            using (var restored = new Context("run:b120:restore", owner, null))
                restored.RecoverRouteObservation(saved, arrival);
        }
        Debug.Log("B120_FACTION_ROUTE_ACK_RESTORE=PASS");
        MigratedProducerRunOffenseEntryE2EDebugScenarios.RunGoodwillAcknowledgementRecovery();
        Debug.Log("B120_FACTION_OBSERVER_BATCH=PASS");
    }

    [Serializable]
    private sealed class RouteObservationBundle
    {
        public DungeonFactionSaveData faction;
        public FactionCampaignWorldSaveData campaign;
        public GameSessionSaveData session;
        public DungeonPhysicalItemSaveData items;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
    }

    private sealed partial class Context
    {
        internal RouteObservationBundle ExerciseRouteAcknowledgementLoss(FactionObservationTestTransport transport, bool arrival, int mode)
        {
            if (arrival) Require(RequestTrade(), "Arrival setup did not pay for its route.");
            transport.Target = arrival ? typeof(FactionRouteArrivedDomainFact) : typeof(FactionRouteSettlementDomainFact);
            transport.Mode = mode;
            int fences = 0;
            transport.During = () => { RejectCargo(() => runtime.Capture()); RejectCargo(() => runtime.Tick()); fences++; };
            dropZone.Enabled = false;
            if (arrival) { clock.Delta = 20f; runtime.Tick(); clock.Delta = 0f; }
            else Require(runtime.TryRequestTrade(FactionId, out _, out _), "A committed route debit was reported as rejected.");
            var route = runtime.Routes.Single();
            Require(fences == 1 && session.holdingMoney.Value == InitialMoney - PaymentGold && exactSources.Prepared == 0
                && (arrival ? route.arrivalPublicationId : route.settlementPublicationId) == string.Empty,
                "Unknown route ACK repeated effects, lacked its fence, or was acknowledged.");
            var saved = JsonUtility.FromJson<RouteObservationBundle>(JsonUtility.ToJson(new RouteObservationBundle
            {
                faction = runtime.Capture(), campaign = campaign.CaptureFactions(), session = GameSessionSaveData.From(session.Capture()),
                items = items.Capture(), ledger = outcomes.Ledger.CaptureGameplayOutcomes(), counters = outcomes.Transaction.Capture()
            }));
            CheckRouteObservation(saved, true);
            string json = JsonUtility.ToJson(saved);
            var tamper = JsonUtility.FromJson<RouteObservationBundle>(json);
            if (arrival) tamper.faction.routes[0].arrivalPublicationKey += ":wrong";
            else tamper.faction.routes[0].settlementPublicationKey += ":wrong";
            CheckRouteObservation(tamper, false);
            if (mode == 1)
            {
                tamper = JsonUtility.FromJson<RouteObservationBundle>(json);
                (arrival ? tamper.faction.routes[0].arrivalObservation : tamper.faction.routes[0].settlementObservation).after.factionDisplayName += " changed";
                CheckRouteObservation(tamper, false);
            }
            return saved;
        }

        internal void RecoverRouteObservation(RouteObservationBundle saved, bool arrival)
        {
            CheckRouteObservation(saved, true);
            FactionCargoSaveValidation.ValidateJson(JsonUtility.ToJson(saved.faction));
            var candidate = outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.ledger);
            outcomes.Ledger.BeginRestoreCandidate(); outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
            outcomes.Ledger.PublishRestoreCandidate(); outcomes.Ledger.CompleteRestoreCandidate();
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(saved.counters));
            campaign.PublishFactions(campaign.PrepareFactionsRestore(saved.campaign));
            sessionStore.Restore(saved.session.ToSnapshot()); items.Restore(saved.items);
            runtime.PublishRestoreCandidate(runtime.PrepareRestoreCandidate(saved.faction));
            dropZone.Enabled = false; clock.Delta = 0f;
            runtime.Tick();
            Require(Stage == (arrival ? 2 : 1) && session.holdingMoney.Value == InitialMoney - PaymentGold && exactSources.Prepared == 0,
                "Recovered route repeated payment/materialization or did not resume its frozen fact.");
            var route = runtime.Routes.Single();
            var kind = arrival ? MigratedProducerOutcomeKind.FactionRouteArrivedEvent : MigratedProducerOutcomeKind.FactionRouteSettlementReceipt;
            var definition = MigratedProducerOutcomeCatalog.Get(kind);
            string keyText = arrival ? route.arrivalPublicationKey : route.settlementPublicationKey;
            string publicationId = arrival ? route.arrivalPublicationId : route.settlementPublicationId;
            string fingerprint = arrival ? route.arrivalPublicationFingerprint : route.settlementPublicationFingerprint;
            Require(GameplayDomainFactDeliveryQuery.TryReadKey(keyText, out var key)
                && outcomes.Ledger.TryGetResultIdentity(key, out var proof) && proof.HasCanonicalPayloadHash
                && proof.OutcomeId.ToString() == publicationId && proof.CanonicalPayloadHash == fingerprint
                && outcomes.Ledger.TryGetExact(proof.OutcomeId, out var exact)
                && exact.absoluteDay == AbsoluteDay && exact.status == GameplayOutcomeStatus.Succeeded
                && exact.producerId == definition.ProducerId && exact.outcomeTypeId == definition.OutcomeTypeId.Value
                && HasRequiredRouteShape(exact, kind, arrival ? 2 : 1)
                && outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value => value.outcomeTypeId == definition.OutcomeTypeId.Value) == 1,
                "Recovered route lacked its single exact canonical outcome and matching opaque proof.");
            string before = JsonUtility.ToJson(runtime.Capture());
            runtime.Tick();
            Require(before == JsonUtility.ToJson(runtime.Capture()), "Repeated route recovery changed owner state.");
            ValidateCargoJoin(runtime.Capture(), true);
        }
    }

    private static void CheckRouteObservation(RouteObservationBundle saved, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        FactionObservationPreflight.Validate(saved.faction, saved.ledger, saved.counters, report);
        Require(report.Success == expected, "Route observation join mismatch: " + string.Join(";", report.Errors));
    }
}

public static partial class MigratedProducerRunOffenseEntryE2EDebugScenarios
{
    internal static void RunGoodwillAcknowledgementRecovery()
    {
        for (int mode = 0; mode < 2; mode++)
        {
            string owner = "faction:b120:goodwill:" + mode;
            string run = "run:b120:goodwill:" + mode;
            JObject checkpoint;
            using (var source = CreateGoodwillD10Scenario(run, owner, MigratedProducerOutcomeFaultMode.None))
            {
                source.ObservationTransport.Target = typeof(FactionGoodwillTransferredFact);
                source.ObservationTransport.Mode = mode;
                int fences = 0;
                source.ObservationTransport.During = () =>
                {
                    RequireObservationRejection(() => source.CaptureFactionObservationState());
                    RequireObservationRejection(source.RecoverFactionObservations); fences++;
                };
                source.Execute();
                Require(fences == 1 && source.RequireFaction().goodwillTransferCompleted && !source.RequireFaction().goodwillObserved
                    && source.RemainingGoodwillValue() == 560, "Unknown goodwill ACK lost the single physical debit.");
                checkpoint = ProductionCausalOwnerCheckpoint.Capture(owner, "faction-relationship", "action:adjust-faction-trust",
                    source.CaptureOwnerCheckpoint(), source.Outcomes.Ledger, source.Outcomes.Transaction);
            }
            using (var restored = CreateGoodwillD10Scenario(run, owner, MigratedProducerOutcomeFaultMode.None))
            {
                JObject state = ProductionCausalOwnerCheckpoint.RestoreCommon(JObject.Parse(checkpoint.ToString()), owner,
                    "faction-relationship", "action:adjust-faction-trust", restored.Outcomes.Ledger, restored.Outcomes.Transaction);
                restored.RestoreOwnerCheckpoint(state);
                Require(!FactionGoodwillOutbox.HasProvenance(restored.RequireFaction()) && restored.RequireFaction().trust == 42
                    && restored.RemainingGoodwillValue() == 560, "Goodwill recovery repeated goods or trust effects.");
                string before = restored.CaptureOwnerCheckpoint().ToString();
                restored.RecoverFactionObservations();
                Require(before == restored.CaptureOwnerCheckpoint().ToString(), "Repeated goodwill recovery changed owner state.");
                string type = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FactionTrustChangedEvent).OutcomeTypeId.Value;
                Require(restored.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value => value.outcomeTypeId == type) == 1,
                    "Goodwill recovery produced duplicate trust outcomes.");
            }
        }
        Debug.Log("B120_FACTION_GOODWILL_ACK_RESTORE=PASS");
    }

    private static void RequireObservationRejection(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected observation capture/reentry rejection.");
    }
}
#endif
