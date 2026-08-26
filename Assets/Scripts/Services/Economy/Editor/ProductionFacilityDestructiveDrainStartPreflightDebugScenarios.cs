using System;
using System.Collections.Generic;
using System.Linq;

public static class ProductionFacilityDestructiveDrainStartPreflightDebugScenarios
{
    private static readonly BuildingInstanceId FacilityId =
        (BuildingInstanceId)"building-instance:qa-drain-preflight";
    private static readonly string DestinationId =
        ProductionOutputDestinationId.FromFacility(FacilityId).Value;

    public static string VerifyAll()
    {
        VerifySafePrepublicationPhases();
        VerifyLatentPhysicalPublicationDefers();
        VerifyCompletedRequiresExactDurableRoutingOwner();
        VerifyInvalidPendingMarkerConflicts();
        VerifyInputOrderDoesNotChangeFingerprint();
        return "PRODUCTION_DESTRUCTIVE_DRAIN_START_PREFLIGHT_PASS";
    }

    private static void VerifySafePrepublicationPhases()
    {
        Fixture fixture = new(
            Owner(1, ProductionPreparedOutputPhase.Unresolved),
            Owner(2, ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace),
            Owner(3, ProductionPreparedOutputPhase.PublicationPrepared));
        ProductionFacilityDestructiveDrainStartPreflightResult result =
            fixture.Preflight.Assess(FacilityId);
        Require(result.CanStart && result.ReasonCode.Length == 0,
            "Safe prepublication phases were rejected.");
    }

    private static void VerifyLatentPhysicalPublicationDefers()
    {
        ProductionFacilityDestructiveDrainPreparedOutputOwner publication =
            Owner(1, ProductionPreparedOutputPhase.PublicationPrepared);
        Fixture fixture = new(publication);
        fixture.Publication.SetPresent(publication.BatchCommitId);
        ProductionFacilityDestructiveDrainStartPreflightResult present =
            fixture.Preflight.Assess(FacilityId);
        Require(present.Status ==
                ProductionFacilityDestructiveDrainStartPreflightStatus.Deferred
            && present.ReasonCode.StartsWith(
                "prepared-output-publication-normalization-required:",
                StringComparison.Ordinal),
            "PublicationPrepared with a physical marker was not deferred.");

        Fixture pending = new(Owner(
            2,
            ProductionPreparedOutputPhase
                .PhysicalBatchCommittedPublicationPending));
        Require(pending.Preflight.Assess(FacilityId).Status ==
                ProductionFacilityDestructiveDrainStartPreflightStatus.Deferred,
            "Physical publication pending was not deferred.");
    }

    private static void VerifyCompletedRequiresExactDurableRoutingOwner()
    {
        ProductionFacilityDestructiveDrainPreparedOutputOwner completed =
            Owner(1, ProductionPreparedOutputPhase.Completed);
        Fixture missing = new(completed);
        Require(missing.Preflight.Assess(FacilityId).Status ==
                ProductionFacilityDestructiveDrainStartPreflightStatus.Deferred,
            "Completed output without routing owner was accepted.");

        Fixture exact = new(completed);
        exact.Routing.Set(Routing(completed));
        Require(exact.Preflight.Assess(FacilityId).CanStart,
            "Completed output with exact routing owner was rejected.");

        Fixture drift = new(completed);
        ProductionPreparedOutputRoutingBatchSnapshot mismatched =
            Routing(completed, outcomeFingerprint: Digest('f'));
        drift.Routing.Set(mismatched);
        Require(drift.Preflight.Assess(FacilityId).Status ==
                ProductionFacilityDestructiveDrainStartPreflightStatus.Conflict,
            "Routing owner fingerprint drift was not rejected.");
    }

    private static void VerifyInvalidPendingMarkerConflicts()
    {
        ProductionFacilityDestructiveDrainPreparedOutputOwner publication =
            Owner(1, ProductionPreparedOutputPhase.PublicationPrepared);
        Fixture fixture = new(publication);
        fixture.Publication.SetInvalid(publication.BatchCommitId);
        Require(fixture.Preflight.Assess(FacilityId).Status ==
                ProductionFacilityDestructiveDrainStartPreflightStatus.Conflict,
            "Malformed planned-output marker was not rejected.");
    }

    private static void VerifyInputOrderDoesNotChangeFingerprint()
    {
        ProductionFacilityDestructiveDrainPreparedOutputOwner first =
            Owner(1, ProductionPreparedOutputPhase.Unresolved);
        ProductionFacilityDestructiveDrainPreparedOutputOwner second =
            Owner(2, ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace);
        Fixture forward = new(first, second);
        Fixture reverse = new(second, first);
        string left = forward.Preflight.Assess(FacilityId).SourceFingerprint;
        string right = reverse.Preflight.Assess(FacilityId).SourceFingerprint;
        Require(string.Equals(left, right, StringComparison.Ordinal),
            "Prepared-output preflight fingerprint depends on query order.");
    }

    private static ProductionFacilityDestructiveDrainPreparedOutputOwner Owner(
        int ordinal,
        ProductionPreparedOutputPhase phase)
    {
        bool unresolved = phase == ProductionPreparedOutputPhase.Unresolved;
        return new ProductionFacilityDestructiveDrainPreparedOutputOwner(
            (ProductionBillId)$"production-bill:qa-drain-{ordinal}",
            FacilityId,
            "recipe:qa-drain",
            ordinal,
            DestinationId,
            phase,
            unresolved ? string.Empty : $"batch:qa-drain-{ordinal}",
            unresolved ? string.Empty : Digest((char)('a' + ordinal)));
    }

    private static ProductionPreparedOutputRoutingBatchSnapshot Routing(
        ProductionFacilityDestructiveDrainPreparedOutputOwner owner,
        string outcomeFingerprint = null) => new(
        owner.BatchCommitId,
        owner.BillId.Value,
        owner.RecipeId,
        owner.FacilityId.Value,
        owner.CycleSequence,
        outcomeFingerprint ?? owner.OutcomeFingerprint,
        Digest('e'),
        owner.DestinationId,
        Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>(),
        Array.Empty<ProductionPreparedOutputRouteRequestSnapshot>(),
        Array.Empty<ProductionPreparedOutputPhysicalRouteReceipt>(),
        false);

    private static string Digest(char value) => new(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal Fixture(
            params ProductionFacilityDestructiveDrainPreparedOutputOwner[] owners)
        {
            Query = new FixedOwnerQuery(owners);
            Routing = new FixedRoutingQuery();
            Publication = new FixedPublication();
            Preflight = new ProductionFacilityDestructiveDrainStartPreflight(
                Query,
                Routing,
                Publication);
        }

        internal FixedOwnerQuery Query { get; }
        internal FixedRoutingQuery Routing { get; }
        internal FixedPublication Publication { get; }
        internal ProductionFacilityDestructiveDrainStartPreflight Preflight { get; }
    }

    private sealed class FixedOwnerQuery :
        IProductionFacilityDestructiveDrainPreparedOutputQuery
    {
        private readonly ProductionFacilityDestructiveDrainPreparedOutputOwner[]
            values;

        internal FixedOwnerQuery(
            IEnumerable<ProductionFacilityDestructiveDrainPreparedOutputOwner>
                values) =>
            this.values = (values ?? Array.Empty<
                    ProductionFacilityDestructiveDrainPreparedOutputOwner>())
                .ToArray();

        public IReadOnlyList<
                ProductionFacilityDestructiveDrainPreparedOutputOwner>
            CapturePreparedOutputOwners(BuildingInstanceId facilityId) => values;
    }

    private sealed class FixedRoutingQuery :
        IProductionPreparedOutputRoutingBatchQuery
    {
        private readonly Dictionary<string,
            ProductionPreparedOutputRoutingBatchSnapshot> values =
            new(StringComparer.Ordinal);

        internal void Set(ProductionPreparedOutputRoutingBatchSnapshot value) =>
            values[value.BatchCommitId] = value;

        public bool TryCaptureBatch(
            string batchCommitId,
            out ProductionPreparedOutputRoutingBatchSnapshot snapshot) =>
            values.TryGetValue(batchCommitId, out snapshot);
    }

    private sealed class FixedPublication :
        IFacilityBufferPlannedOutputPublicationService
    {
        private readonly HashSet<string> present = new(StringComparer.Ordinal);
        private readonly HashSet<string> invalid = new(StringComparer.Ordinal);

        internal void SetPresent(string batchCommitId) => present.Add(batchCommitId);
        internal void SetInvalid(string batchCommitId) => invalid.Add(batchCommitId);

        public bool TryCapturePendingBatch(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            candidate = null;
            if (present.Contains(batchCommitId))
            {
                failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
                failureReason = string.Empty;
                return true;
            }
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode
                .ExistingPublicationConflict;
            failureReason = invalid.Contains(batchCommitId)
                ? "planned-output-marker-invalid:" + batchCommitId
                : "planned-output-batch-missing:" + batchCommitId;
            return false;
        }

        public bool TryPublishFullBatch(
            FacilityBufferPlannedOutputToken token,
            out FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => Unsupported(
            out receipt,
            out failureCode,
            out failureReason);

        public bool TryRollbackPublishedBatch(
            FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => Unsupported(
            out failureCode,
            out failureReason);

        public bool TryAcknowledgePublishedBatch(
            FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => Unsupported(
            out failureCode,
            out failureReason);

        public bool TryRollbackRestoreCandidate(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => Unsupported(
            out failureCode,
            out failureReason);

        public bool TryAcknowledgeRestoreCandidate(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => Unsupported(
            out failureCode,
            out failureReason);

        private static bool Unsupported(
            out FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            receipt = default;
            return Unsupported(out failureCode, out failureReason);
        }

        private static bool Unsupported(
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode
                .RepositoryTransactionFailed;
            failureReason = "unsupported-fixture-call";
            return false;
        }
    }
}
