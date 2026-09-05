#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedRoutingIdentityRetargetAdapterDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Prepared Routing Identity Retarget")]
    public static void VerifyFromMenu()
    {
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa:prepared-routing-retarget";
        ActiveRoutingLifecycle lifecycle = new(facilityId);
        ProductionPreparedRoutingIdentityRetargetAdapter adapter = new(lifecycle);
        ProductionFacilityHandle source = Facility(facilityId, Vector2Int.zero);
        ProductionFacilityRetargetRequest[] requests =
        {
            new(source, ProductionFacilityMutationKind.Evolution)
        };
        Require(adapter.TryStage(
                requests,
                "qa:prepared-routing-retarget",
                out ProductionFacilityRetargetAuthorityPlan plan,
                out string stageFailure),
            "Prepared routing stage failed: " + stageFailure);

        ProductionFacilityHandle exactTarget = Facility(
            facilityId,
            Vector2Int.zero);
        bool identityPublished = adapter.TryPublish(
                plan,
                new[]
                {
                    new ProductionFacilityRetargetBinding(facilityId, exactTarget)
                },
                out string published,
                out string publishFailure);
        bool identityCaptured = adapter.TryCaptureCurrentFingerprint(
            plan,
            out string current,
            out string captureFailure);
        Require(identityPublished
            && identityCaptured
            && string.Equals(published, current, StringComparison.Ordinal),
            "Prepared routing identity publish drifted: "
            + publishFailure + "|" + captureFailure);
        Require(adapter.TryRollback(
                plan,
                out string rolledBack,
                out string rollbackFailure)
            && string.Equals(
                rolledBack,
                plan.StagedFingerprint,
                StringComparison.Ordinal),
            "Prepared routing identity rollback was not exact: "
            + rollbackFailure);

        Require(adapter.TryStage(
                requests,
                "qa:prepared-routing-position-reject",
                out ProductionFacilityRetargetAuthorityPlan changedPlan,
                out stageFailure),
            "Prepared routing changed-position stage failed: " + stageFailure);
        ProductionFacilityHandle movedTarget = Facility(
            facilityId,
            new Vector2Int(1, 0));
        bool changedRejected = !adapter.TryPublish(
                changedPlan,
                new[]
                {
                    new ProductionFacilityRetargetBinding(facilityId, movedTarget)
                },
                out _,
                out string changedFailure);
        bool changedRolledBack = adapter.TryRollback(
            changedPlan,
            out string changedRollback,
            out string changedRollbackFailure);
        Require(changedRejected
            && changedFailure.Contains("reauthor-adapter-required")
            && changedRolledBack
            && string.Equals(
                changedRollback,
                changedPlan.StagedFingerprint,
                StringComparison.Ordinal),
            "Changed routing subject did not fail and roll back exactly: "
            + changedFailure + "|" + changedRollbackFailure);

        Debug.Log(
            "[V27][PASS] Prepared routing and exact-route outbox retain a stable fingerprint through same-subject identity handoff; changed position fails before mutation.");
    }

    private static ProductionFacilityHandle Facility(
        BuildingInstanceId id,
        Vector2Int position) => new(
        new FixtureFacility(),
        id,
        position,
        false,
        string.Empty,
        false,
        default,
        "building-definition:qa-routing",
        "workstation:qa-routing",
        2);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixtureFacility
    {
    }

    private sealed class ActiveRoutingLifecycle :
        IProductionOutputDestinationLifecycleQuery
    {
        private readonly BuildingInstanceId facilityId;

        internal ActiveRoutingLifecycle(BuildingInstanceId facilityId) =>
            this.facilityId = facilityId;

        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId requested)
        {
            bool active = requested.Equals(facilityId);
            string contributionFingerprint =
                ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                    "qa:prepared-routing-exact-route|" + requested.Value + "|"
                    + (active ? "active" : "empty"));
            ProductionOutputDestinationLifecycleContribution routing = new(
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                active,
                active ? 3L : 0L,
                active ? 2 : 0,
                active ? 4200L : 0L,
                active
                    ? new[]
                    {
                        new ProductionOutputLifecycleBlock(
                            ProductionOutputLifecycleBlockCode.RoutingLine,
                            1,
                            2100L),
                        new ProductionOutputLifecycleBlock(
                            ProductionOutputLifecycleBlockCode.ExactRouteOutbox,
                            1,
                            2100L)
                    }
                    : Array.Empty<ProductionOutputLifecycleBlock>(),
                contributionFingerprint,
                contributionFingerprint);
            string aggregate = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint(
                    "qa:prepared-routing-lifecycle|" + requested.Value + "|"
                    + contributionFingerprint);
            return new ProductionOutputDestinationLifecycleSnapshot(
                requested,
                ProductionOutputDestinationId.FromFacility(requested),
                new[] { routing },
                aggregate,
                aggregate);
        }
    }
}
#endif
