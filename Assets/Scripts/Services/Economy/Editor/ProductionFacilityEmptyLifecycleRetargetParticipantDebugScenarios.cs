#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityEmptyLifecycleRetargetParticipantDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Empty Lifecycle Retarget Guard")]
    public static void VerifyFromMenu()
    {
        BuildingInstanceId sourceA =
            (BuildingInstanceId)"building:qa:empty-retarget:a";
        BuildingInstanceId sourceB =
            (BuildingInstanceId)"building:qa:empty-retarget:b";
        MutableLifecycle lifecycle = new();
        ProductionFacilityEmptyLifecycleRetargetParticipant participant =
            new(lifecycle);
        ProductionFacilityRetargetRequest[] requests =
        {
            new(Facility(sourceB, "source-b"),
                ProductionFacilityMutationKind.Synthesis),
            new(Facility(sourceA, "source-a"),
                ProductionFacilityMutationKind.Synthesis)
        };

        Require(participant.TryPrepare(
                requests,
                "qa:empty-retarget",
                out ProductionFacilityRetargetParticipantPlan plan,
                out string prepareFailure),
            "Empty lifecycle prepare failed: " + prepareFailure);
        ProductionFacilityHandle target = Facility(sourceA, "candidate");
        ProductionFacilityRetargetBinding[] bindings =
        {
            new(sourceB, target),
            new(sourceA, target)
        };
        Require(participant.TryCommit(
                plan,
                bindings,
                out string committed,
                out string commitFailure),
            "Empty lifecycle commit failed: " + commitFailure);
        Require(participant.TryCaptureCurrentFingerprint(
                plan,
                out string current,
                out string captureFailure)
            && string.Equals(committed, current, StringComparison.Ordinal),
            "Committed lifecycle fingerprint drifted: " + captureFailure);
        Require(participant.TryRollback(
                plan,
                out string rolledBack,
                out string rollbackFailure)
            && string.Equals(
                rolledBack,
                plan.PreparedFingerprint,
                StringComparison.Ordinal),
            "Empty lifecycle rollback was not exact: " + rollbackFailure);

        lifecycle.MarkActive(sourceB);
        Require(!participant.TryPrepare(
                requests,
                "qa:active-retarget",
                out _,
                out string activeFailure)
            && activeFailure.Contains("active-authority"),
            "Active bill/WIP/custody was accepted by the empty-only guard.");

        Debug.Log(
            "[V27][PASS] Empty production lifecycle retarget guard preserves stable source ordering/fingerprints, exact rollback, and rejects active authority.");
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

    private sealed class MutableLifecycle :
        IProductionOutputDestinationLifecycleQuery
    {
        private readonly HashSet<BuildingInstanceId> active = new();

        internal void MarkActive(BuildingInstanceId facilityId) =>
            active.Add(facilityId);

        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId facilityId)
        {
            string fingerprint = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint(
                    "qa:empty-retarget:" + facilityId.Value + ":"
                    + (active.Contains(facilityId) ? "active" : "empty"));
            IReadOnlyList<ProductionOutputDestinationLifecycleContribution>
                contributions = active.Contains(facilityId)
                    ? new[]
                    {
                        new ProductionOutputDestinationLifecycleContribution(
                            "qa-active-bill",
                            true,
                            1L,
                            1,
                            0L,
                            new[]
                            {
                                new ProductionOutputLifecycleBlock(
                                    ProductionOutputLifecycleBlockCode.GenericBill,
                                    1,
                                    0L)
                            },
                            fingerprint)
                    }
                    : Array.Empty<ProductionOutputDestinationLifecycleContribution>();
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                contributions,
                fingerprint);
        }
    }
}
#endif
