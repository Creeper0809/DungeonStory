#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class WorkCompletionIdentityCausalProductionEntryDebugScenarios
{
    public static void RunWorkIdentityObservationBatch()
    {
        var failures = new List<Exception>();
        void Run(string name, Action check)
        {
            try { check(); }
            catch (Exception error) { Debug.LogException(error); failures.Add(new InvalidOperationException(name, error)); }
        }
        Run("cursor", () => Require(WorkCompletionIdentityOutcomeDebugScenarios.RunAll(true), "Work identity cursor tests failed."));
        Run("causal owner", () => Require(RunCausalOwnerFamily(true), "Work identity production family failed."));
        Run("surgery completion", () =>
        {
            WorkTerminalIdentityOwnershipDebugScenarios.RunCompletionObserverFocused();
            Debug.Log("B121 surgery completion observer boundaries PASS");
        });
        Run("husbandry completion", () => Debug.Log(VerifyHusbandryCompletionFactFocused()));
        Run("lost ACK before commit", () => Context.VerifyObservationRecovery(commitBeforeLoss: false));
        Run("lost ACK after commit", () => Context.VerifyObservationRecovery(commitBeforeLoss: true));
        if (failures.Count != 0) throw new AggregateException("B121 work identity observer checks failed", failures);
        Debug.Log("B121 work identity observer batch PASS");
    }

    private sealed partial class Context
    {
        private void ValidateObservationSave(CharacterNarrativeWorldSaveData narrative, GameplayOutcomeLedgerSaveData incoming)
        {
            var report = new DungeonGameRestoreReport();
            WorkIdentityObservationPreflight.Validate(narrative, incoming, registry, report);
            Require(report.Success, string.Join(";", report.Errors));
        }

        internal static void VerifyObservationRecovery(bool commitBeforeLoss)
        {
            string owner = "owner:work-observation-recovery:" + (commitBeforeLoss ? "after" : "before");
            JObject saved;
            using (var source = new Context("work-observation-recovery", owner, null,
                inner => new LostObservationAck(inner, commitBeforeLoss)))
            {
                EnvironmentCropProductionEntryDebugScenarios.RunHarvestForWorkIdentity(source.registry, source.ledger,
                    source.ChildKey(0).OperationId.Value, source.PlotId(0), source.actorFixture.Actor, FirstDay);
                var request = source.CreateRequest(0);
                var before = JToken.FromObject(source.identityStates.Capture());
                var result = source.adapter.EnsureApplied(request);
                Require(result.Status == WorkCompletionIdentityDeliveryStatus.Deferred,
                    "Unknown observation ACK must retain effects and defer: " + result.FailureReason);
                var narrative = new CharacterNarrativeWorldSaveData
                {
                    identityStates = source.identityStates.Capture().ToList(),
                    workCompletionDeliveries = source.deliveries.Capture().ToList()
                };
                Require(narrative.workCompletionDeliveries.Single().disposition ==
                    WorkCompletionIdentityDeliveryDisposition.EffectsAppliedPendingObservation,
                    "Unknown ACK lost its pending observation.");
                Require(!JToken.DeepEquals(before, JToken.FromObject(narrative.identityStates)),
                    "Real identity effects were not applied before lost ACK.");
                var outcomes = source.ledger.CaptureGameplayOutcomes();
                source.ValidateObservationSave(narrative, outcomes);
                saved = JObject.Parse(new JObject
                {
                    ["narrative"] = JToken.FromObject(narrative),
                    ["outcomes"] = JToken.FromObject(outcomes),
                    ["stats"] = CaptureStats(source.actorFixture.Actor)
                }.ToString(Formatting.None));
            }
            using (var restored = new Context("work-observation-recovery", owner, null))
            {
                var narrative = saved["narrative"].ToObject<CharacterNarrativeWorldSaveData>();
                var outcomes = saved["outcomes"].ToObject<GameplayOutcomeLedgerSaveData>();
                restored.ValidateObservationSave(narrative, outcomes);
                var candidate = restored.ledger.PrepareGameplayOutcomeRestore(outcomes);
                restored.ledger.BeginRestoreCandidate();
                restored.ledger.PublishGameplayOutcomeRestore(candidate);
                restored.ledger.PublishRestoreCandidate();
                restored.ledger.CompleteRestoreCandidate();
                restored.identityStates.RestoreTrustedTransactionSnapshot(narrative.identityStates);
                restored.deliveries.Restore(narrative.workCompletionDeliveries);
                RestoreStats(restored.actorFixture.Actor, saved["stats"]);
                var identityBefore = JToken.FromObject(restored.identityStates.Capture());
                var statsBefore = CaptureStats(restored.actorFixture.Actor);
                restored.adapter.Tick();
                var cursor = restored.deliveries.Capture().Single();
                Require(cursor.disposition == WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome,
                    "Restored pending observation did not converge.");
                Require(JToken.DeepEquals(identityBefore, JToken.FromObject(restored.identityStates.Capture()))
                    && JToken.DeepEquals(statsBefore, CaptureStats(restored.actorFixture.Actor)),
                    "Observation recovery reapplied gameplay effects.");
                restored.RequireOutcome(restored.CreateRequest(0), 1, true);
                Require(restored.adapter.EnsureApplied(restored.CreateRequest(0)).Status ==
                    WorkCompletionIdentityDeliveryStatus.AlreadyApplied, "Completed retry was not deduplicated.");
                restored.adapter.Tick();
                narrative.workCompletionDeliveries = restored.deliveries.Capture().ToList();
                outcomes = restored.ledger.CaptureGameplayOutcomes();
                restored.ValidateObservationSave(narrative, outcomes);
                foreach (string field in new[] { "characterDisplayName", "publicationKey", "publicationFingerprint" })
                {
                    var tampered = JToken.FromObject(narrative).ToObject<CharacterNarrativeWorldSaveData>();
                    var row = tampered.workCompletionDeliveries.Single();
                    if (field == "characterDisplayName") row.observation.characterDisplayName += " changed";
                    else if (field == "publicationKey") row.publicationKey += "changed";
                    else row.publicationFingerprint = new string('0', 64);
                    var report = new DungeonGameRestoreReport();
                    WorkIdentityObservationPreflight.Validate(tampered, outcomes, restored.registry, report);
                    Require(!report.Success, "Changed observation save was accepted: " + field);
                }
                Require(restored.Apply(1, 1), "Recovered stream could not apply its next real harvest.");
            }
            Debug.Log("B121 work identity lost ACK " + (commitBeforeLoss ? "after" : "before")
                + " commit -> JSON fresh-owner recovery -> no duplicate effects PASS");
        }
    }

    private sealed class LostObservationAck : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly bool commitBeforeLoss;
        internal LostObservationAck(IDomainFactStagingPublisher inner, bool commitBeforeLoss)
        { this.inner = inner; this.commitBeforeLoss = commitBeforeLoss; }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out var original, out failure)) { stage = null; return false; }
            stage = new Stage(original, commitBeforeLoss);
            return true;
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly IDomainFactStage inner;
            private readonly bool commitBeforeLoss;
            internal Stage(IDomainFactStage inner, bool commitBeforeLoss)
            { this.inner = inner; this.commitBeforeLoss = commitBeforeLoss; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit()
            {
                if (commitBeforeLoss) inner.Commit();
                return DomainFactDelivery.Uncertain("fixture:lost-observation-ack");
            }
            public void Cancel() => inner.Cancel();
        }
    }
}
#endif
