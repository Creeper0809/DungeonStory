#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProductionGenericBillIdentityRetargetAdapterDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Active Generic Bill Identity Retarget")]
    public static void VerifyFromMenu()
    {
        BuildingInstanceId sourceId =
            (BuildingInstanceId)"building:qa:active-bill-retarget";
        MutableBillQuery bills = new();
        bills.Set(sourceId, billCount: 1, activeWipCount: 1);
        ProductionGenericBillIdentityRetargetAdapter adapter = new(bills);
        ProductionFacilityRetargetAuthorityParticipant participant = new(
            new IProductionFacilityRetargetAuthorityAdapter[] { adapter });
        ProductionFacilityMutationEpochRuntime epochs = new();
        ProductionFacilityRetargetTransaction transaction = new(
            new ProductionFacilityRetargetParticipantRegistry(
                new IProductionFacilityRetargetParticipant[] { participant }),
            epochs);

        ProductionFacilityHandle source = Facility(sourceId, "source");
        ProductionFacilityHandle sameIdentityTarget = Facility(sourceId, "candidate");
        ProductionFacilityRetargetRequest[] requests =
        {
            new(source, ProductionFacilityMutationKind.Evolution)
        };
        Require(transaction.TryBegin(
                requests,
                "qa:active-bill-identity-retarget",
                out ProductionFacilityRetargetTransactionState state,
                out string beginFailure),
            "Active bill retarget prepare failed: " + beginFailure);
        Require(transaction.TryCommit(
                state,
                new[]
                {
                    new ProductionFacilityRetargetBinding(
                        sourceId,
                        sameIdentityTarget)
                },
                out string commitFailure),
            "Active bill/WIP identity retarget failed: " + commitFailure);
        Require(transaction.TryComplete(state, out string completeFailure)
            && !epochs.IsFrozen(sourceId)
            && bills.CaptureFacilityLifecycle(sourceId).BillCount == 1
            && bills.CaptureFacilityLifecycle(sourceId).ActiveWipCount == 1,
            "Active bill/WIP identity was not preserved exactly: "
            + completeFailure);

        BuildingInstanceId mergedSourceId =
            (BuildingInstanceId)"building:qa:active-bill-retarget:merged";
        bills.Set(mergedSourceId, billCount: 1, activeWipCount: 1);
        ProductionFacilityHandle mergedSource = Facility(
            mergedSourceId,
            "merged-source");
        ProductionFacilityRetargetRequest[] mergeRequests =
        {
            new(source, ProductionFacilityMutationKind.Synthesis),
            new(mergedSource, ProductionFacilityMutationKind.Synthesis)
        };
        Require(transaction.TryBegin(
                mergeRequests,
                "qa:active-bill-reauthor-reject",
                out ProductionFacilityRetargetTransactionState rejected,
                out beginFailure),
            "Active bill reauthor rejection could not prepare: " + beginFailure);
        bool rejectedCommit = !transaction.TryCommit(
                rejected,
                new[]
                {
                    new ProductionFacilityRetargetBinding(
                        sourceId,
                        sameIdentityTarget),
                    new ProductionFacilityRetargetBinding(
                        mergedSourceId,
                        sameIdentityTarget)
                },
                out string rejection);
        bool rolledBack = transaction.TryRollback(
            rejected,
            out string rollbackFailure);
        Require(rejectedCommit
            && rejection.Contains("reauthor-adapter-required")
            && rolledBack
            && !epochs.IsFrozen(sourceId)
            && !epochs.IsFrozen(mergedSourceId)
            && bills.CaptureFacilityLifecycle(sourceId).BillCount == 1
            && bills.CaptureFacilityLifecycle(sourceId).ActiveWipCount == 1
            && bills.CaptureFacilityLifecycle(mergedSourceId).BillCount == 1
            && bills.CaptureFacilityLifecycle(mergedSourceId).ActiveWipCount == 1,
            "Unsupported N-to-one bill reauthor did not fail and roll back exactly: "
            + rejection + "|" + rollbackFailure);

        Debug.Log(
            "[V27][PASS] Active generic bill and embedded WIP survive stable-ID facility replacement; unsupported identity reauthor fails before authority mutation and releases the epoch.");
    }

    private static ProductionFacilityHandle Facility(
        BuildingInstanceId id,
        string label) => new(
        new FixtureFacility(label),
        id,
        default,
        false,
        string.Empty,
        false,
        default,
        "building-definition:" + label,
        "workstation:" + label,
        2);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixtureFacility
    {
        internal FixtureFacility(string label) => Label = label;
        internal string Label { get; }
    }

    private sealed class MutableBillQuery : IProductionBillCoreQuery
    {
        private readonly Dictionary<BuildingInstanceId, (int Bills, int Wip)>
            byFacility = new();

        internal void Set(
            BuildingInstanceId facilityId,
            int billCount,
            int activeWipCount)
        {
            byFacility[facilityId] = (billCount, activeWipCount);
        }

        public int Version => 7;

        public IReadOnlyList<ProductionBillSnapshot> GetBills(
            ProductionFacilityHandle facility) =>
            Array.Empty<ProductionBillSnapshot>();

        public ProductionFacilityBillLifecycleSnapshot CaptureFacilityLifecycle(
            BuildingInstanceId requested)
        {
            byFacility.TryGetValue(requested, out (int Bills, int Wip) owned);
            int ownedBills = owned.Bills;
            int ownedWip = owned.Wip;
            string fingerprint = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint(
                    "qa:active-generic-bill|" + requested.Value + "|"
                    + ownedBills + "|" + ownedWip);
            return new ProductionFacilityBillLifecycleSnapshot(
                requested,
                ownedBills,
                ownedWip,
                0,
                0,
                0,
                Version,
                fingerprint,
                fingerprint);
        }

        public bool HasStockSensor(ProductionFacilityHandle facility) => false;
    }
}
#endif
