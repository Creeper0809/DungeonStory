#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

public static partial class ProductionPreparedOutputFullPersistenceDebugScenarios
{
    public static void VerifyBillOwnerCheckpointGcForBatch()
    {
        using RuntimeGraph graph = new("bill-owner-gc", 7216, SawmillScenario);
        graph.SeedOwnedCurrentFormatBill();
        graph.PrepareWip(graph.SingleBill);
        DungeonProductionBillSaveData terminal = graph.Production.Capture();
        ProductionBillSaveData bill = terminal.bills.Single();
        Require(ProductionGenericBillTerminalDrainCanonical.TryCreateWipTerminalReceipt(
            bill, out var receipt, out string failure), "WIP receipt fixture: " + failure);
        ProductionGenericBillTerminalDrainSaveData producer = CreateBillGcProducer(bill, receipt.commitId);
        var producers = new[] { producer };
        string activeBefore = JsonUtility.ToJson(graph.Production.Capture());
        Require(!graph.Production.TryPrepareCheckpointGarbageCollection(producers, out _, out failure)
            && failure == "production-wip-checkpoint-gc-lower-row-missing-or-conflicting"
            && JsonUtility.ToJson(graph.Production.Capture()) == activeBefore,
            "Missing WIP authority did not reject without mutation.");

        terminal.bills.Clear();
        terminal.wipTerminalReceipts.Add(receipt);
        graph.Production.Restore(graph.Production.BuildRestore(terminal));
        string before = JsonUtility.ToJson(graph.Production.Capture());
        int version = graph.Production.Version;
        Require(!graph.Production.TryPrepareCheckpointGarbageCollection(
            new[] { producer, producer }, out _, out failure)
            && failure == "production-wip-checkpoint-gc-producer-invalid",
            "Duplicate producer was accepted.");
        Require(graph.Production.TryPrepareCheckpointGarbageCollection(producers, out var candidate, out failure), failure);
        Require(!graph.Production.TryPrepareCheckpointGarbageCollection(producers, out _, out failure)
            && failure == "production-wip-checkpoint-gc-already-active", "Concurrent GC candidate was accepted.");

        using RuntimeGraph restored = new("bill-owner-gc-restored", 7217, SawmillScenario);
        restored.Production.Restore(restored.Production.BuildRestore(
            JsonUtility.FromJson<DungeonProductionBillSaveData>(before)));
        CommandMustThrow(() => restored.Production.TryPublishCheckpointGarbageCollection(candidate, out _));
        Require(graph.Production.TryPublishCheckpointGarbageCollection(candidate, out failure)
            && graph.Production.Capture().wipTerminalReceipts.Count == 0
            && graph.Production.Version == version + 1, "GC did not publish the exact receipt removal: " + failure);
        Require(graph.Production.TryPublishCheckpointGarbageCollection(candidate, out failure)
            && graph.Production.Version == version + 1, "GC replay changed version: " + failure);
        graph.Production.RollbackCheckpointGarbageCollection(candidate);
        Require(graph.Production.Version == version && JsonUtility.ToJson(graph.Production.Capture()) == before,
            "GC rollback did not restore the exact state and revision.");
        Require(graph.Production.TryPublishCheckpointGarbageCollection(candidate, out failure), failure);
        graph.Production.CompleteCheckpointGarbageCollection(candidate);
        CommandMustThrow(() => graph.Production.TryPublishCheckpointGarbageCollection(candidate, out _));

        // Restore replaces the aggregate root; the same owner must read the new receipt and revision.
        graph.Production.Restore(graph.Production.BuildRestore(
            JsonUtility.FromJson<DungeonProductionBillSaveData>(before)));
        Require(graph.Production.TryPrepareCheckpointGarbageCollection(producers, out var stale, out failure), failure);
        graph.Production.Restore(graph.Production.BuildRestore(
            JsonUtility.FromJson<DungeonProductionBillSaveData>(before)));
        string driftBefore = JsonUtility.ToJson(graph.Production.Capture());
        int driftVersion = graph.Production.Version;
        Require(!graph.Production.TryPublishCheckpointGarbageCollection(stale, out failure)
            && failure == "production-wip-checkpoint-gc-live-authority-changed"
            && graph.Production.Version == driftVersion
            && JsonUtility.ToJson(graph.Production.Capture()) == driftBefore,
            "GC published through a changed aggregate revision.");
        graph.Production.CompleteCheckpointGarbageCollection(stale);
        Require(graph.Production.TryPrepareCheckpointGarbageCollection(producers, out var retry, out failure)
            && graph.Production.TryPublishCheckpointGarbageCollection(retry, out failure), failure);
        graph.Production.CompleteCheckpointGarbageCollection(retry);

        Require(restored.Production.TryPrepareCheckpointGarbageCollection(producers, out var fresh, out failure)
            && restored.Production.TryPublishCheckpointGarbageCollection(fresh, out failure), failure);
        restored.Production.RollbackCheckpointGarbageCollection(fresh);
        Require(JsonUtility.ToJson(restored.Production.Capture()) == before, "Fresh-owner GC rollback lost the receipt.");
        restored.Production.CompleteCheckpointGarbageCollection(fresh);
        Debug.Log("ARCHITECTURE_PRODUCTION_BILL_GC=PASS; exact receipt; missing/duplicate; candidate identity; root/revision drift; rollback; JSON fresh owner; retry");
    }

    private static ProductionGenericBillTerminalDrainSaveData CreateBillGcProducer(
        ProductionBillSaveData bill, string wipCommit)
    {
        // The lower child receipt is controlled input; this verifies the bill owner's GC boundary.
        var producer = new ProductionGenericBillTerminalDrainSaveData
        {
            parentOperationId = "qa:bill-gc:parent",
            stepOperationId = "qa:bill-gc:step",
            ownerStableId = ProductionFacilityDestructiveDrainOwnerStableIds.GenericBill(bill.billId),
            billId = bill.billId,
            facilityId = bill.buildingInstanceId,
            inputDestinationId = bill.materialDestinationId,
            sourceBill = ProductionGenericBillTerminalDrainCanonical.CloneBill(bill),
            sourceBillFingerprint = ProductionGenericBillTerminalDrainCanonical.CreateSourceBillFingerprint(bill),
            inputDestinationDrainStepOperationId = "qa:bill-gc:child",
            inputDestinationDrainRequestFingerprint = new string('a', 64),
            inputDestinationDrainCommitId = "qa:bill-gc:child-commit",
            inputDestinationDrainReceiptFingerprint = new string('b', 64),
            wipTerminalCommitId = wipCommit,
            phase = ProductionGenericBillTerminalDrainPhase.OwnerAcknowledgedAwaitingCheckpointGc
        };
        producer.requestFingerprint = ProductionGenericBillTerminalDrainCanonical.CreateRequestFingerprint(
            producer.parentOperationId, producer.stepOperationId, producer.ownerStableId, producer.sourceBill,
            producer.inputDestinationDrainStepOperationId, producer.inputDestinationDrainRequestFingerprint);
        producer.billTerminalEffectFingerprint = ProductionGenericBillTerminalDrainCanonical.CreateBillTerminalEffectFingerprint(
            producer.requestFingerprint, producer.inputDestinationDrainReceiptFingerprint, producer.wipTerminalCommitId);
        producer.commitId = ProductionGenericBillTerminalDrainCanonical.CreateCommitId(
            producer.stepOperationId, producer.requestFingerprint);
        producer.receiptFingerprint = ProductionGenericBillTerminalDrainCanonical.CreateReceiptFingerprint(
            producer.requestFingerprint, producer.inputDestinationDrainReceiptFingerprint,
            producer.billTerminalEffectFingerprint, producer.commitId);
        Require(ProductionGenericBillTerminalDrainCanonical.IsValidSave(producer), "GC producer fixture is invalid.");
        return producer;
    }
}
#endif
