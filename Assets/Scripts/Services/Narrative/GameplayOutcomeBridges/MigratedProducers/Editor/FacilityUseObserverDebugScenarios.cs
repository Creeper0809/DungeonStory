#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerEconomyServiceEntryE2EDebugScenarios
{
    public static void RunFacilityUseObserverBatch()
    {
        VerifyFacilityPersonalName();
        Debug.Log("B134_FACILITY_PERSONAL_NAME=PASS");
        Require(FacilityUseExecutionStateDebugScenarios.RunAll(true), "Facility state checks failed.");
        VerifyUseAcknowledgements();
        Debug.Log("B118_FACILITY_ACK_RANGES=PASS");
        VerifyUsePublicationRecovery(false);
        VerifyUsePublicationRecovery(true);
        Debug.Log("B118_FACILITY_JSON_REPLAY=PASS");
        RunFacilityVisitObserverFocused();
        Debug.Log("B118_FACILITY_OBSERVER_BATCH=PASS");
    }

    private static void VerifyFacilityPersonalName()
    {
        using var value = new FacilityObservationScenario();
        CharacterActor actor = value.visitor;
        CharacterRole role = actor.Identity.Role;
        CharacterType type = actor.Identity.CharacterType;
        actor.Progression.ApplyPreparedIdentity("Leon", "Slime", Array.Empty<int>(),
            actor.Progression.GrowthState.potentialGrade, 134, false, ensureInitialDrafts: false);
        actor.gameObject.name = "슬라임 사장";
        Require(actor.Identity.DisplayName == "Leon" && actor.BuildingDisplayName == "Leon"
            && actor.BuildingVisitor.VisitorSnapshot.DisplayName == "Leon",
            "Facility name slot used the object/template name instead of personal identity.");
        Require(actor.Identity.Role == role && actor.Identity.CharacterType == type,
            "Personal-name presentation changed role or character type.");
        var receipt = value.Receipt();
        Require(value.Commit(receipt).DurablyCommitted, "Named facility visit did not commit.");
        var saved = CopyUseBundle(value.Capture());
        var row = saved.ledger.exactOutcomes.Concat(saved.ledger.outbox).Single();
        Require(row.participants.Single(p => p.entityKindId == "character").displayText == "Leon",
            "Facility observer did not preserve the captured personal name.");
        var evidence = new GameplayOutcomeNarrativeEvidenceQuery(value.Outcomes.Ledger)
            .GetForCharacter(actor.Identity.PersistentId, 1, 0f, false).Single();
        Require(evidence.CanonicalFactText.Contains("Leon")
            && !evidence.CanonicalFactText.Contains("슬라임 사장"),
            "Model evidence projection replaced the personal name with an object/role label.");
        ValidateUseBundle(saved, true);
        actor.gameObject.name = "Changed template";
        Require(!value.Commit(receipt).DurablyCommitted && value.NativeCount == 1,
            "Changing the object label duplicated the original visit.");
    }

    private static void VerifyUseAcknowledgements()
    {
        using var value = new FacilityObservationScenario();
        var receipts = new[] { value.Receipt(), value.Receipt(), value.Receipt() };
        foreach (int index in new[] { 2, 0, 1 })
            Require(value.Commit(receipts[index]).DurablyCommitted, "Out-of-order facility completion failed.");
        var state = value.Facility.FacilityState;
        Require(state.acknowledgedUseRanges.Count == 1 && state.acknowledgedUseRanges[0].first == 1
            && state.acknowledgedUseRanges[0].last == 3 && state.completedUses == 3
            && Mathf.Approximately(state.cleanliness, 95.5f) && value.Notifications == 3, "Completion range or effects repeated.");
        string before = JsonUtility.ToJson(value.Capture());
        Require(!value.Commit(receipts[0]).DurablyCommitted, "Retired facility use was accepted as new.");
        Require(before == JsonUtility.ToJson(value.Capture()) && value.NativeCount == 3 && value.Notifications == 3,
            "Duplicate terminal use changed native/domain state.");
        var saved = value.Capture();
        ValidateUseBundle(saved, true);
        var tampered = CopyUseBundle(saved);
        var json = JObject.Parse(tampered.state);
        json["acknowledgedUseRanges"] = new JArray();
        tampered.state = json.ToString();
        ValidateUseBundle(tampered, false);
        json = JObject.Parse(saved.state);
        json.Remove("acknowledgedUseRanges");
        RejectUse(() => FacilityUseSaveValidation.ValidateJson(json.ToString()));
    }

    private static void VerifyUsePublicationRecovery(bool afterCommit)
    {
        FacilityUseBundle saved;
        using (var original = new FacilityObservationScenario())
        {
            original.Transport.UnknownBefore = !afterCommit;
            original.Transport.UnknownAfter = afterCommit;
            var receipt = original.Receipt();
            int fences = 0;
            original.Transport.DuringPublish = () =>
            {
                RejectUse(() => original.Module.CaptureState());
                RejectUse(() => original.Facility.AllocateFacilityUseOperationSequence());
                RejectUse(() => original.Commit(receipt));
                fences++;
            };
            Require(!original.Commit(receipt).DurablyCommitted && fences == 1, "Unknown ACK was acknowledged or save fence missing.");
            Require(original.Facility.FacilityState.pendingUses.Count == 1 && original.Notifications == 0
                && original.Facility.FacilityState.completedUses == 1 && original.NativeCount == (afterCommit ? 1 : 0),
                "Deferred completion lost effects or fabricated native success.");
            saved = CopyUseBundle(original.Capture());
            ValidateUseBundle(saved, true);
            var changedReceipt = original.Receipt();
            changedReceipt.OperationSequence = receipt.OperationSequence;
            changedReceipt.OperationId = receipt.OperationId;
            changedReceipt.FailureCode = "changed-receipt";
            Require(!original.Commit(changedReceipt).DurablyCommitted, "Conflicting pending payload accepted.");
        }
        if (afterCommit)
        {
            var tampered = CopyUseBundle(saved);
            var state = JsonUtility.FromJson<FacilityRuntimeState>(tampered.state);
            state.pendingUses[0].healthAfter += 1f;
            tampered.state = JsonUtility.ToJson(state);
            ValidateUseBundle(tampered, false);
        }
        {
            var tampered = CopyUseBundle(saved);
            var state = JsonUtility.FromJson<FacilityRuntimeState>(tampered.state);
            state.pendingUses[0].publicationKey += "-wrong";
            tampered.state = JsonUtility.ToJson(state);
            ValidateUseBundle(tampered, false);
        }
        using var restored = new FacilityObservationScenario(saved, day: 99);
        Require(restored.Publisher.ReconcilePendingVisit(restored.Facility).DurablyCommitted, "Fresh observer replay failed.");
        Require(restored.Facility.FacilityState.completedUses == 1 && Mathf.Approximately(restored.Facility.FacilityState.cleanliness, 98.5f)
            && restored.Facility.FacilityState.pendingUses.Count == 0 && restored.NativeCount == 1 && restored.Notifications == 1,
            "Replay repeated gameplay effects or failed to retire pending publication.");
        var row = restored.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes
            .Concat(restored.Outcomes.Ledger.CaptureGameplayOutcomes().outbox).Single();
        Require(row.absoluteDay == 7, "Replay rebound the original visit to the current day.");
        string final = JsonUtility.ToJson(restored.Capture());
        restored.Publisher.ReconcilePendingVisit(restored.Facility);
        Require(JsonUtility.ToJson(restored.Capture()) == final && restored.Notifications == 1, "Repeated recovery changed state.");
        ValidateUseBundle(restored.Capture(), true);
    }

    [Serializable]
    private sealed class FacilityUseBundle
    {
        public string facilityId, state;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
    }
    private static FacilityUseBundle CopyUseBundle(FacilityUseBundle value) =>
        JsonUtility.FromJson<FacilityUseBundle>(JsonUtility.ToJson(value));

    private static void ValidateUseBundle(FacilityUseBundle saved, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        FacilityUseFactPreflight.Validate(new ModularFacilityWorldSaveData
        {
            buildings = new List<ModularFacilityBuildingSaveData> { new()
            {
                persistentInstanceId = saved.facilityId,
                stateModules = new List<BuildingStateModuleSaveData> { new()
                { moduleId = BuildingStateModuleIds.FacilityOperation, version = FacilityRuntimeStateModule.Version, payload = saved.state } }
            } }
        }, saved.ledger, saved.counters, report);
        Require(report.Success == expected, "Facility save join mismatch: " + string.Join(";", report.Errors));
    }

    private static void RejectUse(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected facility operation rejection.");
    }

    private sealed class FacilityObservationScenario : IDisposable
    {
        internal readonly BuildableObject Facility;
        internal readonly MigratedProducerOutcomeEditorFixture Outcomes;
        internal readonly BuildingVisitEventPublisher Publisher;
        internal readonly FacilityObservationTransport Transport;
        internal readonly FacilityRuntimeStateModule Module;
        internal readonly CharacterActor visitor;
        private readonly EditorFacilityUseFactFixture observation;
        private readonly IDisposable notification;
        internal int Notifications;
        internal int NativeCount => Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count
            + Outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count + Outcomes.Ledger.CaptureGameplayOutcomes().tombstones.Count;
        internal FacilityObservationScenario(FacilityUseBundle saved = null, int day = 7)
        {
            Outcomes = new MigratedProducerOutcomeEditorFixture(saved?.ledger.runId ?? "run:b118:" + Guid.NewGuid().ToString("N"));
            if (saved != null)
            {
                var candidate = Outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.ledger);
                Outcomes.Ledger.BeginRestoreCandidate(); Outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
                Outcomes.Ledger.PublishRestoreCandidate(); Outcomes.Ledger.CompleteRestoreCandidate();
                Outcomes.Transaction.Restore(Outcomes.Transaction.PrepareRestore(saved.counters));
            }
            observation = new EditorFacilityUseFactFixture(Outcomes);
            Transport = new FacilityObservationTransport(observation.Facts);
            var events = new GameEventBus();
            notification = events.Subscribe<FacilityVisitEvent>(_ => Notifications++);
            Publisher = new BuildingVisitEventPublisher(events, new FixedGameSessionStateProvider(day), Transport, observation.Facts);
            visitor = RegularCustomerScenario.CreateCustomerForEntryFixture();
            Facility = RegularCustomerScenario.CreateFacilityForEntryFixture();
            Module = new FacilityRuntimeStateModule(Facility);
            if (saved != null)
            {
                Facility.RestorePersistentIdentity(new BuildingInstanceId(saved.facilityId));
                Require(Module.TryRestoreState(Module.CurrentVersion, saved.state, out string error), "Facility restore failed:" + error);
                Facility.BuildingData.objectName = "Changed after restoration";
            }
        }
        internal CharacterFacilityUseOutcomeReceipt Receipt()
        {
            long sequence = Facility.AllocateFacilityUseOperationSequence();
            return new CharacterFacilityUseOutcomeReceipt
            {
                OperationSequence = sequence,
                OperationId = FacilityUseCompletedFact.OperationIdentity(Facility.RequirePersistentInstanceId().Value, sequence),
                ActorId = visitor.Identity.PersistentId, ActorDisplayName = visitor.BuildingDisplayName,
                FacilityRole = FacilityRole.None.ToString(), Status = CharacterFacilityUseOutcomeStatus.Completed,
                EffectsCommitted = true
            };
        }
        internal FacilityVisitCommitResult Commit(CharacterFacilityUseOutcomeReceipt receipt) => Publisher.CommitVisit(CharacterActor.From(visitor), Facility, receipt);
        internal FacilityUseBundle Capture() => new()
        {
            facilityId = Facility.RequirePersistentInstanceId().Value, state = Module.CaptureState(),
            ledger = Outcomes.Ledger.CaptureGameplayOutcomes(), counters = Outcomes.Transaction.Capture()
        };
        public void Dispose()
        {
            notification.Dispose(); observation.Dispose();
            if (Facility?.BuildingData != null) UnityEngine.Object.DestroyImmediate(Facility.BuildingData);
            if (Facility != null) UnityEngine.Object.DestroyImmediate(Facility.gameObject);
            if (visitor?.data != null) UnityEngine.Object.DestroyImmediate(visitor.data);
            if (visitor != null) UnityEngine.Object.DestroyImmediate(visitor.gameObject);
        }
    }

    private sealed class FacilityObservationTransport : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal bool UnknownBefore, UnknownAfter;
        internal Action DuringPublish;
        internal FacilityObservationTransport(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => inner.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            DuringPublish?.Invoke();
            if (UnknownBefore) return DomainFactDelivery.Uncertain("b118-before-commit");
            var delivered = inner.Publish(lease, fact);
            return UnknownAfter ? DomainFactDelivery.Uncertain("b118-after-commit") : delivered;
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }
}
#endif
