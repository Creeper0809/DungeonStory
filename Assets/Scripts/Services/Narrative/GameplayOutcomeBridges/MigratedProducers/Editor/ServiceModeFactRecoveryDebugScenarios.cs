#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using DungeonStory.ServiceRooms;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerEconomyServiceEntryE2EDebugScenarios
{
    public static string VerifyServiceModeFactRecoveryFocused()
    {
        VerifyServiceModeChangeEntry();
        VerifyModeReplyLoss(false);
        VerifyModeReplyLoss(true);
        return "service-mode-fact/actual-mode-command/rejection/unknown-before-and-after/json-new-owner/replay-only/retired-hub/canonical-join=PASS";
    }

    private static void VerifyModeReplyLoss(bool commitFirst)
    {
        const MigratedProducerOutcomeKind kind = MigratedProducerOutcomeKind.ServiceModeChangeResult;
        string hubId = "building:mode-fact-" + (commitFirst ? "after" : "before");
        using var source = new ServiceModeScenario(kind, unknownModeReply: commitFirst, fixedHubId: hubId);
        Require(source.SwitchToDirect().Succeeded && source.Mode == ServiceOperationMode.Direct,
            "Unknown mode publication response rolled back the actual mode change.");
        var saved = source.ModeAggregate.Capture();
        var ledger = source.Outcomes.Ledger.CaptureGameplayOutcomes();
        var counters = source.Outcomes.Transaction.Capture();
        Require(saved.pendingModeFacts.Count == 1 && source.ModeLeaseCount == 0
            && source.Outcomes.Ledger.GetDiagnostics().ReservedCount == 0,
            "Mode observation lost its pending source or leaked a reservation.");
        Require(source.SwitchToDirect().Succeeded && source.ModeAggregate.PendingModeFacts.Count == 1,
            "Repeating the already-applied mode duplicated its fact.");
        string json = JsonUtility.ToJson(saved);
        ServiceRoomsSaveSection.ValidateModeFactJson(json);
        var missing = JObject.Parse(json);
        ((JObject)missing["pendingModeFacts"][0]["fact"]).Remove("previousMode");
        bool rejected = false;
        try { ServiceRoomsSaveSection.ValidateModeFactJson(missing.ToString()); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Mode restore accepted missing frozen before-state.");
        CheckModeJoin(saved, ledger, counters, true);
        var tampered = JsonUtility.FromJson<ServiceRoomsSaveData>(json);
        tampered.pendingModeFacts[0].publicationKey += ":wrong";
        CheckModeJoin(tampered, ledger, counters, false);
        tampered = JsonUtility.FromJson<ServiceRoomsSaveData>(json);
        tampered.hubs.Single().mode = ServiceOperationMode.Managed;
        CheckModeJoin(tampered, ledger, counters, false);
        if (commitFirst)
        {
            tampered = JsonUtility.FromJson<ServiceRoomsSaveData>(json);
            tampered.pendingModeFacts[0].fact.hubName += "변조";
            CheckModeJoin(tampered, ledger, counters, false);
        }
        // Facility retirement does not erase the already-applied historical source.
        var retired = ServiceSessionAggregate.CreateRestored(JsonUtility.FromJson<ServiceRoomsSaveData>(json), 0);
        retired.RemoveHubModesExcept(new HashSet<string>(StringComparer.Ordinal));
        var retiredSave = retired.Capture();
        Require(retiredSave.hubs.Count == 0 && retiredSave.pendingModeFacts.Count == 1,
            "Retiring a service hub erased its pending mode fact.");
        CheckModeJoin(retiredSave, ledger, counters, true);

        using var resumed = new ServiceModeScenario(kind, fixedHubId: hubId);
        var candidate = resumed.Outcomes.Ledger.PrepareGameplayOutcomeRestore(ledger);
        resumed.Outcomes.Ledger.BeginRestoreCandidate(); resumed.Outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
        resumed.Outcomes.Ledger.PublishRestoreCandidate(); resumed.Outcomes.Ledger.CompleteRestoreCandidate();
        resumed.Outcomes.Transaction.Restore(resumed.Outcomes.Transaction.PrepareRestore(counters));
        resumed.ModeRuntime.PublishRestoreCandidate(resumed.ModeRuntime.PrepareRestoreCandidate(
            JsonUtility.FromJson<ServiceRoomsSaveData>(json)));
        int version = resumed.ModeRuntime.Version;
        resumed.ModeRuntime.RecoverPendingModeFacts();
        resumed.ModeRuntime.RecoverPendingModeFacts();
        Require(resumed.Mode == ServiceOperationMode.Direct && resumed.ModeRuntime.Version == version
            && resumed.ModeAggregate.PendingModeFacts.Count == 0
            && resumed.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
            "Mode replay repeated a domain mutation or duplicated/lost its record.");
        CheckModeJoin(saved, resumed.Outcomes.Ledger.CaptureGameplayOutcomes(), resumed.Outcomes.Transaction.Capture(), true);
    }

    private static void CheckModeJoin(ServiceRoomsSaveData services, GameplayOutcomeLedgerSaveData ledger,
        DungeonMigratedProducerOutcomeSaveData counters, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        ServiceModeFactPreflight.Validate(services, ledger, counters, report);
        Require(report.Success == expected, "Unexpected mode observation save join: " + string.Join(";", report.Errors));
    }

    private sealed class ModeReplyLoss : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly bool commitFirst;
        internal ModeReplyLoss(DomainFactDispatcher inner, bool commitFirst) { this.inner = inner; this.commitFirst = commitFirst; }
        public bool TryReserve<T>(in T proposed, out DomainFactLease lease, out string failure) => inner.TryReserve(proposed, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T actual)
        {
            if (commitFirst) inner.Publish(lease, actual);
            else inner.Cancel(lease);
            return DomainFactDelivery.Uncertain("injected-mode-reply-loss");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }
}
#endif
