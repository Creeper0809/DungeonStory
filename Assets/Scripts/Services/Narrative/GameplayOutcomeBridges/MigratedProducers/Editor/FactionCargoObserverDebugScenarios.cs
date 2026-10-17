#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class FactionRouteCausalProductionEntryDebugScenarios
{
    public static void RunCargoObserverBatch()
    {
        MigratedProducerRunOffenseEntryE2EDebugScenarios.RunFactionCargoObserverFocused();
        Require(RunCausalOwnerFamily(), "Faction route causal save roundtrip failed.");
        Debug.Log("B119_FACTION_CARGO_CAUSAL_RESTORE=PASS");
        for (int mode = 0; mode < 3; mode++)
        {
            CargoObservationTransport transport = null;
            string owner = "faction-route:b119:recovery:" + mode;
            JObject checkpoint;
            using (var context = new Context("run:b119:" + mode, owner, null, dispatcher =>
                   transport = new CargoObservationTransport(dispatcher)))
            {
                context.ExerciseCargoRecovery(transport, mode);
                checkpoint = JObject.Parse(context.CaptureOwnerCheckpoint().ToString());
            }
            using (var restored = new Context("run:b119:restored:" + mode, owner, checkpoint))
                restored.VerifyCargoRestored();
            Debug.Log("B119_FACTION_CARGO_RECOVERY_" + mode + "=PASS");
        }
        Debug.Log("B119_FACTION_CARGO_BATCH=PASS");
    }

    private sealed partial class Context
    {
        internal void ExerciseCargoRecovery(CargoObservationTransport transport, int mode)
        {
            Require(RequestTrade() && AdvanceToArrival(), "Cargo recovery setup did not reach actual arrival.");
            transport.Mode = mode;
            int fences = 0;
            transport.DuringCargo = () =>
            {
                RejectCargo(() => runtime.Capture());
                RejectCargo(() => runtime.Tick());
                fences++;
            };
            dropZone.Enabled = true;
            if (mode == 2) RejectCargo(() => runtime.Tick());
            else runtime.Tick();
            Require(fences == 1 && exactSources.Prepared == 1 && exactSources.Released == 0
                && runtime.Routes.Single().cargoDelivery.state == FactionRouteCargoDeliveryState.Publishing
                && OutcomeCount(MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt) == (mode == 0 ? 0 : 1),
                "Unknown acknowledgement lost its staging candidate or released cargo early.");
            RejectCargo(() => runtime.Capture());
            transport.DuringCargo = null;
            transport.Mode = -1;
            exactSources.BlockRelease = true;
            runtime.Tick();
            Require(exactSources.Prepared == 1 && exactSources.Released == 0
                && OutcomeCount(MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt) == 1,
                "Fact replay repeated preparation or failed to preserve the committed record.");
            RejectCargo(() => runtime.Capture());
            exactSources.BlockRelease = false;
            runtime.Tick();
            Require(Stage == 3 && exactSources.Prepared == 1 && exactSources.Released == 1,
                "Cargo recovery did not release exactly once.");
            RequireCargoOutcome();
            var saved = runtime.Capture();
            ValidateCargoJoin(saved, true);
            string before = JsonUtility.ToJson(saved);
            runtime.Tick();
            Require(before == JsonUtility.ToJson(runtime.Capture()) && exactSources.Released == 1,
                "Repeated tick republished completed cargo.");
            foreach (Action<FactionRouteState> tamper in new Action<FactionRouteState>[]
            {
                route => route.cargoPublicationKey += ":wrong",
                route => route.cargoPublicationId += ":wrong",
                route => route.cargoPublicationFingerprint = new string('0', 64),
                route => route.cargoFactionDisplayName += " changed",
                route => route.cargoDelivery.stacks[0].quantity++,
                route => route.cargoObservationDay++
            })
            {
                var changed = JsonUtility.FromJson<DungeonFactionSaveData>(before);
                tamper(changed.routes.Single());
                ValidateCargoJoin(changed, false);
            }
            var raw = JObject.Parse(before);
            ((JObject)raw["routes"][0]).Remove("cargoPublicationId");
            RejectCargo(() => FactionCargoSaveValidation.ValidateJson(raw.ToString()));
        }

        internal void VerifyCargoRestored()
        {
            Require(Stage == 3, "Completed cargo restore lost its domain state.");
            string before = JsonUtility.ToJson(runtime.Capture());
            runtime.Tick();
            Require(!DeliverCargo() && exactSources.Prepared == 0 && exactSources.Released == 0
                && before == JsonUtility.ToJson(runtime.Capture()), "Fresh restore delivered cargo again.");
            ValidateCargoJoin(runtime.Capture(), true);
        }

        private void ValidateCargoJoin(DungeonFactionSaveData saved, bool expected)
        {
            var report = new DungeonGameRestoreReport();
            FactionCargoFactPreflight.Validate(saved, outcomes.Ledger.CaptureGameplayOutcomes(),
                outcomes.Transaction.Capture(), report);
            FactionObservationPreflight.Validate(saved, outcomes.Ledger.CaptureGameplayOutcomes(),
                outcomes.Transaction.Capture(), report);
            Require(report.Success == expected, "Faction cargo save join mismatch: " + string.Join(";", report.Errors));
        }
    }

    private static void RejectCargo(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected cargo operation rejection.");
    }

    private sealed class CargoObservationTransport : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal int Mode = -1;
        internal Action DuringCargo;
        internal CargoObservationTransport(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => inner.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            if (fact is not FactionCargoDeliveredFact) return inner.Publish(lease, fact);
            DuringCargo?.Invoke();
            if (Mode == 0) return DomainFactDelivery.Uncertain("b119-before-commit");
            var delivered = inner.Publish(lease, fact);
            if (Mode == 2) throw new InvalidOperationException("b119-after-commit-transport-exception");
            return Mode == 1 ? DomainFactDelivery.Uncertain("b119-after-commit") : delivered;
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }
}
#endif
