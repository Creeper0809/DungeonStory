#if UNITY_EDITOR
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class EnvironmentCropProductionEntryDebugScenarios
{
    public static string VerifyCertifiedSeedFactRecovery()
    {
        ManualOutcomeFaultingRecorder fault = null;
        using Fixture fixture = new(ledgerRunId: "run:certified-seed-pending-fact",
            recorderDecorator: inner => fault = new ManualOutcomeFaultingRecorder(inner));
        VerifyCertifiedSeedCompletion(fixture, recorderFault: fault, verifyObserverPendingRecovery: true);
        return "certified-seed/frozen-fact/pending-delivery/json-owner-roundtrip/admission-session-reset/canonical-tamper/replay-dedup=PASS";
    }

    private static void VerifyCertifiedSeedPendingRecovery(Fixture fixture, CertifiedSeedRuntime runtime,
        ManualOutcomeFaultingRecorder fault, string availableSeedStack, string deliveredSeedStack,
        string deliveredKitStack, int certifiedBefore)
    {
        fault.FailCertifiedSeedDelivery = true;
        Require(runtime.CompleteDeliveredPlans(fixture.OperatingDay) == 0, "Pending observation retired its order.");
        var snapshot = runtime.Capture();
        var order = snapshot.orders.Single();
        Require(order.phase == CertifiedSeedOrderPhase.OutputPublished
            && order.outputPublication.observationExpected && !order.outputPublication.observationDelivered
            && !order.outputPublication.outputAcknowledged && order.completionFact?.IsEmpty == false,
            "Pending output lost its observation/fact/input owner.");
        string frozen = JsonUtility.ToJson(order.completionFact);
        string originalStack = order.outputPublication.stacks.Single().stackId;
        string reference = order.outputPublication.observationKey;
        Require(fixture.CountCertifiedSeedLots() == certifiedBefore + 1
            && fixture.Repository.GetEditorTestQuantity(deliveredSeedStack) == 0
            && fixture.Repository.GetEditorTestQuantity(deliveredKitStack) == 0
            && fixture.Repository.GetEditorTestQuantity(availableSeedStack) == 1,
            "Pending observation duplicated/rolled back production or consumed the wrong inputs.");

        var ledger = fixture.Ledger.CaptureGameplayOutcomes();
        Require(ledger.outbox.Count == 1 && ledger.exactOutcomes.Count == 0,
            "Pending delivery should retain one canonical outbox result.");
        CheckJoin(snapshot, ledger, true);
        var tampered = JsonUtility.FromJson<CertifiedSeedWorldSaveData>(JsonUtility.ToJson(snapshot));
        tampered.orders[0].completionFact.absoluteDay++;
        CheckJoin(tampered, ledger, false);
        tampered = JsonUtility.FromJson<CertifiedSeedWorldSaveData>(JsonUtility.ToJson(snapshot));
        tampered.orders[0].completionFact.outputMassGrams++;
        RequireThrows(() => runtime.BuildRestore(tampered), "Drifted frozen output mass was accepted.");
        tampered = JsonUtility.FromJson<CertifiedSeedWorldSaveData>(JsonUtility.ToJson(snapshot));
        tampered.orders[0].outputPublication.observationKey += ":wrong";
        CheckJoin(tampered, ledger, false);

        string json = JsonUtility.ToJson(snapshot);
        CertifiedSeedSaveSection.ValidatePublicationJson(json);
        JObject raw = JObject.Parse(json);
        ((JObject)raw["orders"][0]).Remove("completionFact");
        RequireThrows(() => CertifiedSeedSaveSection.ValidatePublicationJson(raw.ToString()),
            "Current seed save accepted a missing frozen fact.");
        raw = JObject.Parse(json);
        ((JObject)raw["orders"][0]["outputPublication"]).Remove("observationKey");
        RequireThrows(() => CertifiedSeedSaveSection.ValidatePublicationJson(raw.ToString()),
            "Current seed save accepted a missing publication key.");

        var unknown = JsonUtility.FromJson<CertifiedSeedWorldSaveData>(json);
        unknown.orders[0].outputPublication.observationId = string.Empty;
        unknown.orders[0].outputPublication.observationFingerprint = string.Empty;
        CheckJoin(unknown, new GameplayOutcomeLedgerSaveData(), true);
        CheckJoin(snapshot, new GameplayOutcomeLedgerSaveData(), false);

        // Exercise the real admission cache restore lifecycle. It deliberately discards
        // session-local tokens; physical inventory itself remains the same authoritative batch.
        fixture.outputAdmission.BeginRestoreCandidate();
        fixture.outputAdmission.PublishRestoreCandidate();
        fixture.outputAdmission.CompleteRestoreCandidate();
        Require(!fixture.outputAdmission.TryGetPlannedOutputToken(order.outputPublication.admissionTokenId, out _, out _),
            "Admission restore did not remove its transient token.");
        var resumed = fixture.CreateCertifiedSeedRuntime();
        resumed.Restore(resumed.BuildRestore(JsonUtility.FromJson<CertifiedSeedWorldSaveData>(json)));
        Require(resumed.CompleteDeliveredPlans(fixture.OperatingDay + 1) == 0, "Blocked delivery unexpectedly completed.");
        var pending = resumed.Capture().orders.Single();
        Require(JsonUtility.ToJson(pending.completionFact) == frozen
            && pending.outputPublication.stacks.Single().stackId == originalStack
            && pending.outputPublication.observationKey == reference
            && fixture.CountCertifiedSeedLots() == certifiedBefore + 1,
            "Recovery changed the frozen fact, reference, or physical output.");
        fault.FailCertifiedSeedDelivery = false;
        Require(resumed.CompleteDeliveredPlans(fixture.OperatingDay + 2) == 1
            && resumed.CompleteDeliveredPlans(fixture.OperatingDay + 3) == 0
            && resumed.Capture().orders.Count == 0
            && fixture.CountCertifiedSeedLots() == certifiedBefore + 1,
            "Recovery did not complete exactly once without producing another seed.");
        RequireOnlyCertifiedSeedCompletion(fixture);
        var result = fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Single();
        Require(result.absoluteDay == order.completionFact.absoluteDay,
            "Recovered observation used a new day instead of the original fact.");

        static void CheckJoin(CertifiedSeedWorldSaveData seed, GameplayOutcomeLedgerSaveData incoming, bool expected)
        {
            var report = new DungeonGameRestoreReport();
            CertifiedSeedFactPreflight.Validate(seed, incoming, report);
            Require(report.Success == expected, "Certified-seed canonical join returned an unexpected result.");
        }
    }
}
#endif
