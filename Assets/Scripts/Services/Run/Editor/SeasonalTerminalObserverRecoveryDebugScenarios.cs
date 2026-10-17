#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class SeasonalTerminalObserverRecoveryDebugScenarios
{
    public static void RunForBatch()
    {
        SeasonalWildlifeArrivalOutcomeDebugScenarios.RunAll();
        Debug.Log("B122_SEASONAL_WILDLIFE_OWNER_REJECT_RETRY=PASS");
        SeasonalDriftCargoOutcomeDebugScenarios.RunAll();
        Debug.Log("B122_SEASONAL_CARGO_OWNER_REJECT_RETRY=PASS");
        foreach (bool cargo in new[] { false, true })
        foreach (bool commitFirst in new[] { false, true }) Verify(cargo, commitFirst);
        Debug.Log("B122_SEASONAL_OBSERVER_RECOVERY=PASS");
    }

    private static void Verify(bool cargo, bool commitFirst)
    {
        string checkpoint, physical;
        using (var source = new Context(cargo, commitFirst))
        {
            source.StartAndMaterialize();
            // Compare the owner's actual persistence representation on both sides.
            // Unity serializes null inline DTOs as empty objects; raw live DTOs are not save payloads.
            var capturedOwner = source.Capture().seasonal.activeEvents.Single();
            physical = Physical(capturedOwner);
            if (Physical(source.Owner) != physical)
            {
                var live = JObject.Parse(Physical(source.Owner));
                var captured = JObject.Parse(physical);
                Debug.Log("B122_CAPTURE_NORMALIZED_FIELDS=" + string.Join(",", live.Properties()
                    .Where(property => !JToken.DeepEquals(property.Value, captured[property.Name])).Select(property => property.Name)));
            }
            Require(!source.Observe(121) && source.transport.Calls == 1 && source.transport.Fences == 2,
                "Lost ACK did not leave the observation pending or publication allowed reentrant save/mutation.");
            var saved = source.Capture();
            var proof = cargo ? source.Owner.seasonalDriftCargo.publication : source.Owner.seasonalWildlifeArrival.publication;
            Require(proof.key.Length > 0 && proof.id.Length == 0 && Physical(saved.seasonal.activeEvents.Single()) == physical,
                "Pending observation lost its key or changed physical effects.");
            SeasonalTerminalFactPreflight.Validate(saved.seasonal, saved.ledger, saved.counters);
            checkpoint = JsonUtility.ToJson(saved);
            SeasonalWorldEventsSaveSection.ValidateObservationJson(JsonUtility.ToJson(saved.seasonal));
            var missing = JObject.Parse(JsonUtility.ToJson(saved.seasonal));
            ((JObject)missing["activeEvents"][0][cargo ? "seasonalDriftCargo" : "seasonalWildlifeArrival"]["observation"]).Remove("absoluteDay");
            Throws(() => SeasonalWorldEventsSaveSection.ValidateObservationJson(missing.ToString()));
        }
        using (var restored = new Context(cargo, null))
        {
            var saved = JsonUtility.FromJson<Checkpoint>(checkpoint);
            restored.Restore(saved);
            Require(restored.Observe(140), "Fresh owner could not recover the frozen seasonal observation.");
            var final = restored.Capture();
            SeasonalTerminalFactPreflight.Validate(final.seasonal, final.ledger, final.counters);
            var rows = final.ledger.exactOutcomes.Concat(final.ledger.outbox).ToArray();
            Require(rows.Length == 1, "Seasonal recovery record count changed: " + rows.Length);
            Require(rows[0].absoluteDay == 121, "Seasonal first observation day changed: " + rows[0].absoluteDay);
            Require(Physical(final.seasonal.activeEvents.Single()) == physical,
                "Recovery changed the physical owner persistence representation.");
            if (commitFirst)
            {
                var original = saved.ledger.exactOutcomes.Concat(saved.ledger.outbox).Single();
                Require(rows[0].sequence == original.sequence && rows[0].immutablePayloadHash == original.immutablePayloadHash
                    && rows[0].operationId == original.operationId && final.ledger.nextSequence == saved.ledger.nextSequence,
                    "Canonical identity changed after committed-but-unacknowledged replay.");
            }
            long next = final.ledger.nextSequence;
            Require(restored.Observe(150) && restored.Capture().ledger.nextSequence == next,
                "Completed seasonal retry duplicated the record.");
            foreach (string change in new[] { "name", "key", "fingerprint", "owner" })
            {
                var changed = JsonUtility.FromJson<SeasonalEventWorldSaveData>(JsonUtility.ToJson(final.seasonal));
                var owner = changed.activeEvents.Single();
                var proof = cargo ? owner.seasonalDriftCargo.publication : owner.seasonalWildlifeArrival.publication;
                if (change == "name")
                {
                    if (cargo) owner.seasonalDriftCargo.observation.itemDisplayName += " changed";
                    else owner.seasonalWildlifeArrival.observation.speciesDisplayName += " changed";
                }
                else if (change == "key") proof.key += "changed";
                else if (change == "fingerprint") proof.fingerprint = new string('0', 64);
                else if (cargo) owner.seasonalDriftCargo.securedQuantity--;
                else owner.seasonalWildlifeArrival.members[0].spawned = false;
                Throws(() => SeasonalTerminalFactPreflight.Validate(changed, final.ledger, final.counters));
            }
        }
        Debug.Log("B122_SEASONAL_" + (cargo ? "CARGO" : "WILDLIFE") + "_ACK_"
            + (commitFirst ? "AFTER" : "BEFORE") + "_JSON_FRESH_OWNER=PASS");
    }

    [Serializable]
    private sealed class Checkpoint
    {
        public SeasonalEventWorldSaveData seasonal;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
    }

    private sealed class Context : IDisposable
    {
        private readonly bool cargo;
        private readonly V20StoryContentCatalog catalog;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly DomainFactDispatcher facts;
        internal readonly LostAck transport;
        private readonly V20CampaignRuntime campaign;
        internal V20ActiveEventSaveData Owner => campaign.ActiveSeasonalEvents.Single();
        internal Context(bool cargo, bool? commitFirst)
        {
            this.cargo = cargo;
            catalog = new V20StoryContentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            outcomes = new MigratedProducerOutcomeEditorFixture("run:b122-seasonal-" + (cargo ? "cargo" : "wildlife"));
            facts = outcomes.CreateSeasonalTerminalFacts();
            transport = new LostAck(facts, commitFirst);
            campaign = new V20CampaignRuntime(outcomes.AggregateRootStore, catalog, null, null, null, transport, facts);
            transport.BeforePublish = () =>
            {
                Throws(() => campaign.CaptureSeasonal()); transport.Fences++;
                Throws(() => campaign.TryRecordSeasonalWildlifeArrivalFailure(Owner.instanceId, "reentrant")); transport.Fences++;
            };
        }
        internal void StartAndMaterialize()
        {
            string target = cargo ? "seasonal:spring-drift-cargo" : "seasonal:spring-migrant-herd";
            var state = campaign.CaptureSeasonal();
            state.cycle = 120 / GameCalendarRules.DaysPerYear;
            state.completedEventIds.AddRange(catalog.SeasonalEvents.Where(value => value.StableId != target).Select(value => value.StableId));
            campaign.PublishSeasonal(campaign.PrepareSeasonal(state));
            campaign.EvaluateDaily(new V20DailyEventContext { AbsoluteDay = 120, RunSeed = 110123, Season = Season.Spring });
            var owner = Owner;
            string failure;
            if (cargo)
            {
                Require(campaign.TryRecordSeasonalDriftCargoPlan(owner.instanceId, 51, 9, out failure), failure);
                Require(campaign.TryMarkSeasonalDriftCargoSpawned(owner.instanceId, new[] { "stack:b122-cargo" }, out failure), failure);
                Require(campaign.TryRecordSeasonalDriftCargoSecured(owner.instanceId, owner.seasonalDriftCargo.exactQuantity, out failure), failure);
                Require(campaign.TryExpireSeasonalDriftCargo(owner.instanceId, Array.Empty<PhysicalItemTransformInput>(), out failure), failure);
            }
            else
            {
                var plan = Enumerable.Range(0, owner.seasonalWildlifeArrival.exactCount).Select(index =>
                    new SeasonalWildlifeArrivalMemberSaveData { wildlifeId = "wild:" + (12200 + index), positionX = 40 + index, positionY = 7 }).ToArray();
                Require(campaign.TryRecordSeasonalWildlifeArrivalPlan(owner.instanceId, plan, out failure), failure);
                foreach (var member in plan)
                    Require(campaign.TryMarkSeasonalWildlifeArrivalSpawned(owner.instanceId, member.wildlifeId, out failure), failure);
            }
        }
        internal bool Observe(int day) => cargo
            ? campaign.TryObserveSeasonalDriftCargo(Owner.instanceId, day == 121 ? "목재" : "changed", 1500, day, out _)
            : campaign.TryObserveSeasonalWildlifeArrival(Owner.instanceId, day == 121 ? "산양" : "changed", day, out _);
        internal Checkpoint Capture() => new()
        { seasonal = campaign.CaptureSeasonal(), ledger = outcomes.Ledger.CaptureGameplayOutcomes(), counters = outcomes.Transaction.Capture() };
        internal void Restore(Checkpoint saved)
        {
            SeasonalTerminalFactPreflight.Validate(saved.seasonal, saved.ledger, saved.counters);
            var candidate = outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.ledger);
            outcomes.Ledger.BeginRestoreCandidate(); outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
            outcomes.Ledger.PublishRestoreCandidate(); outcomes.Ledger.CompleteRestoreCandidate();
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(saved.counters));
            campaign.PublishSeasonalRestore(campaign.PrepareSeasonalRestore(saved.seasonal));
        }
        public void Dispose() => facts.Dispose();
    }

    private sealed class LostAck : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly bool? commitFirst;
        internal Action BeforePublish;
        internal int Calls, Fences;
        internal LostAck(DomainFactDispatcher inner, bool? commitFirst) { this.inner = inner; this.commitFirst = commitFirst; }
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => inner.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            Calls++; BeforePublish?.Invoke();
            if (!commitFirst.HasValue) return inner.Publish(lease, fact);
            if (commitFirst.Value) Require(inner.Publish(lease, fact).Completed, "Fixture expected a real committed observation before hiding its ACK.");
            return DomainFactDelivery.Uncertain("fixture:lost-ack");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) => inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private static string Physical(V20ActiveEventSaveData owner)
    {
        var snapshot = JObject.FromObject(owner);
        foreach (string name in new[] { "seasonalWildlifeArrival", "seasonalDriftCargo" })
        foreach (string field in new[] { "observation", "publication", "outcomeResultKey" }) ((JObject)snapshot[name]).Remove(field);
        return snapshot.ToString();
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Throws(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Expected an explicit validation failure."); }
}
#endif
