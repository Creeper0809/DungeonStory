using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityDestructiveDrainAuthorityRevokerDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Economy/Run Destructive Drain Authority Revoker Contracts")]
    public static void RunAll()
    {
        VerifyBothAuthoritiesConvergeExactlyOnce();
        VerifyAlreadyAbsentSensorIsReplaySafe();
        VerifySensorCommittedDespiteFalseReturnContinuesForward();
        VerifyOutputCommittedDespiteFalseReturnClosesPostcondition();
        VerifySensorFailureDefersWithoutTouchingOutput();
        VerifyPartialForwardProgressRetriesWithoutSensorRollback();
        VerifyStaleJournalTokenMutatesNothing();
        VerifyPartialAuthorityPairFailsLoud();
        Debug.Log("Production destructive-drain authority revoker contracts passed.");
    }

    private static void VerifyBothAuthoritiesConvergeExactlyOnce()
    {
        using Fixture fixture = new(includeSensor: true, includeOutput: true);
        Require(fixture.TryConverge(out string failureReason),
            "Exact authority convergence failed: " + failureReason);
        Require(fixture.Sensor.RevokeCount == 1
            && fixture.Output.RevokeCount == 1
            && fixture.State.Claims.Count == 0
            && fixture.State.Profiles.Count == 0,
            "Exact convergence did not retire both claim/profile pairs once.");
    }

    private static void VerifyAlreadyAbsentSensorIsReplaySafe()
    {
        using Fixture fixture = new(includeSensor: false, includeOutput: true);
        Require(fixture.TryConverge(out string failureReason),
            "Absent sensor replay did not converge: " + failureReason);
        Require(fixture.Sensor.RevokeCount == 0
            && fixture.Output.RevokeCount == 1,
            "Replay-safe absent sensor path performed the wrong revocation.");
    }

    private static void VerifySensorCommittedDespiteFalseReturnContinuesForward()
    {
        using Fixture fixture = new(includeSensor: true, includeOutput: true);
        fixture.Sensor.ApplyThenReportFailure = true;

        Require(fixture.TryConverge(out string failureReason),
            "Committed sensor revoke did not continue to output in the same call: "
            + failureReason);
        Require(fixture.Sensor.RevokeCount == 1
            && fixture.Output.RevokeCount == 1
            && fixture.State.Claims.Count == 0
            && fixture.State.Profiles.Count == 0,
            "A false sensor return after commit advanced without closing output authority.");
    }

    private static void VerifyOutputCommittedDespiteFalseReturnClosesPostcondition()
    {
        using Fixture fixture = new(includeSensor: true, includeOutput: true);
        fixture.Output.ApplyThenReportFailure = true;

        Require(fixture.TryConverge(out string failureReason),
            "Committed output revoke did not close the final postcondition: "
            + failureReason);
        Require(fixture.Sensor.RevokeCount == 1
            && fixture.Output.RevokeCount == 1
            && fixture.State.Claims.Count == 0
            && fixture.State.Profiles.Count == 0,
            "A false output return after commit bypassed exact final convergence.");
    }

    private static void VerifySensorFailureDefersWithoutTouchingOutput()
    {
        using Fixture fixture = new(includeSensor: true, includeOutput: true);
        fixture.Sensor.FailNextRevoke = true;

        ProductionFacilityDestructiveDrainAuthorityConvergenceResult result =
            fixture.Revoker.TryConverge(
                fixture.Building,
                fixture.Cause,
                fixture.OperationId,
                fixture.Revision);
        Require(!result.Succeeded
            && result.Disposition ==
                ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                    .Deferred
            && result.FailureReason.Contains(
                "production-destructive-drain-sensor-revoke-failed",
                StringComparison.Ordinal)
            && fixture.Sensor.RevokeCount == 0
            && fixture.Output.RevokeCount == 0
            && fixture.State.HasDestination(fixture.SensorDestination)
            && fixture.State.HasDestination(fixture.OutputDestination),
            "Uncommitted sensor failure touched output or lost exact authority state.");
    }

    private static void VerifyPartialForwardProgressRetriesWithoutSensorRollback()
    {
        using Fixture fixture = new(includeSensor: true, includeOutput: true);
        fixture.Output.FailNextRevoke = true;
        Require(!fixture.TryConverge(out string firstFailure)
            && firstFailure.Contains(
                "production-destructive-drain-output-revoke-failed",
                StringComparison.Ordinal)
            && fixture.Sensor.RevokeCount == 1
            && fixture.Output.RevokeCount == 0
            && !fixture.State.HasDestination(fixture.SensorDestination)
            && fixture.State.HasDestination(fixture.OutputDestination),
            "Output failure rolled back or obscured committed sensor revocation.");

        Require(fixture.TryConverge(out string retryFailure),
            "Forward retry failed after partial convergence: " + retryFailure);
        Require(fixture.Sensor.RevokeCount == 1
            && fixture.Output.RevokeCount == 1
            && fixture.State.Claims.Count == 0
            && fixture.State.Profiles.Count == 0,
            "Forward retry repeated sensor effects or failed to close output authority.");
    }

    private static void VerifyStaleJournalTokenMutatesNothing()
    {
        using Fixture fixture = new(includeSensor: true, includeOutput: true);
        ProductionFacilityDestructiveDrainAuthorityConvergenceResult result =
            fixture.Revoker.TryConverge(
                fixture.Building,
                fixture.Cause,
                fixture.OperationId,
                fixture.Revision - 1L);
        Require(!result.Succeeded
            && result.Disposition ==
                ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                    .Conflict
            && result.FailureReason.Contains(
                "production-destructive-drain-authority-token-invalid",
                StringComparison.Ordinal)
            && fixture.Sensor.RevokeCount == 0
            && fixture.Output.RevokeCount == 0
            && fixture.State.Claims.Count == 2
            && fixture.State.Profiles.Count == 2,
            "Stale journal revision mutated authority state.");
    }

    private static void VerifyPartialAuthorityPairFailsLoud()
    {
        using Fixture fixture = new(includeSensor: false, includeOutput: true);
        fixture.State.AddClaimOnly(
            fixture.SensorDestination,
            ProductionStockSensorDestinationAuthorityRuntime.OwnerDomain,
            fixture.FacilityId);
        Require(!fixture.TryConverge(out string failureReason)
            && failureReason.Contains(
                "production-destructive-drain-authority-pair-cardinality-invalid",
                StringComparison.Ordinal)
            && fixture.Sensor.RevokeCount == 0
            && fixture.Output.RevokeCount == 0,
            "Partial claim/profile pair was silently accepted or mutated.");
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture(bool includeSensor, bool includeOutput)
        {
            FacilityId = (BuildingInstanceId)"building:qa-authority-revoker";
            OperationId = ProductionFacilityDestructiveDrainOperationId
                .FromFacility(FacilityId);
            Cause = ProductionFacilityDestructiveDrainCause.ExplicitDemolition;
            Revision = 7L;
            GameObject root = new("AuthorityRevokerFixture");
            Building = root.AddComponent<BuildableObject>();
            Building.RestorePersistentIdentity(FacilityId);
            Handle = new ProductionFacilityHandle(
                Building,
                FacilityId,
                new Vector2Int(4, 5),
                isDestroyed: false,
                ProductionBillRuntime.StockSensorItemId,
                allowsOverflowDump: false,
                default,
                "building-definition:qa-authority-revoker",
                "workstation:qa-authority-revoker",
                outputBufferCycleCapacity: 2,
                workstationLaneProfile:
                    ProductionFacilityWorkstationLaneCapacityProfile
                        .SingleManualWithDetachedBatchProcessors);
            SensorDestination = ProductionStockSensorRuntime.BuildDestinationId(
                FacilityId.Value);
            OutputDestination = ProductionBillRuntime.OutputDestinationPrefix
                + FacilityId.Value;
            State = new AuthorityState(Handle.Position);
            if (includeSensor)
            {
                State.AddPair(
                    SensorDestination,
                    ProductionStockSensorDestinationAuthorityRuntime.OwnerDomain,
                    FacilityId,
                    1_150L,
                    ProductionStockSensorDestinationAuthorityRuntime
                        .CapacitySchemaRevision);
            }
            if (includeOutput)
            {
                State.AddPair(
                    OutputDestination,
                    ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                    FacilityId,
                    8_000L,
                    ProductionOutputDestinationAuthorityRuntime
                        .CapacitySchemaRevision);
            }
            Sensor = new FakeSensorAuthority(State, Handle, SensorDestination);
            Output = new FakeOutputAuthority(State, Handle, OutputDestination);
            Journal = new FixedJournal(new ProductionFacilityDestructiveDrainEntrySaveData
            {
                operationId = OperationId.Value,
                initiatingMutationOperationId =
                    ProductionFacilityDestructiveDrainCanonical
                        .BuildInitiatingMutationOperationId(Cause, FacilityId),
                cause = Cause,
                facilityId = FacilityId.Value,
                destinationId = ProductionOutputDestinationId
                    .FromFacility(FacilityId).Value,
                phase = ProductionFacilityDestructiveDrainPhase
                    .AwaitingAuthorityRevoke,
                preparedLifecycleFingerprint = new string('a', 64),
                expectedCurrentLifecycleFingerprint = new string('b', 64),
                revision = Revision,
                participants = new List<
                    ProductionFacilityDestructiveDrainParticipantSaveData>()
            });
            Revoker = new ProductionFacilityDestructiveDrainAuthorityRevoker(
                new FixedFacilityQuery(Handle),
                new StateLifecycle(State, FacilityId),
                Output,
                Sensor,
                new EmptySensorRuntime(),
                new ProductionFacilityDestructiveDrainAuthorityStateQuery(
                    State,
                    State),
                Journal);
        }

        internal BuildableObject Building { get; }
        internal ProductionFacilityHandle Handle { get; }
        internal BuildingInstanceId FacilityId { get; }
        internal ProductionFacilityDestructiveDrainCause Cause { get; }
        internal ProductionFacilityDestructiveDrainOperationId OperationId { get; }
        internal long Revision { get; }
        internal string SensorDestination { get; }
        internal string OutputDestination { get; }
        internal AuthorityState State { get; }
        internal FakeSensorAuthority Sensor { get; }
        internal FakeOutputAuthority Output { get; }
        internal FixedJournal Journal { get; }
        internal ProductionFacilityDestructiveDrainAuthorityRevoker Revoker { get; }

        internal bool TryConverge(out string failureReason)
        {
            ProductionFacilityDestructiveDrainAuthorityConvergenceResult result =
                Revoker.TryConverge(
                Building,
                Cause,
                OperationId,
                Revision);
            failureReason = result.FailureReason;
            return result.Succeeded;
        }

        public void Dispose()
        {
            if (Building != null)
                UnityEngine.Object.DestroyImmediate(Building.gameObject);
        }
    }

    private sealed class AuthorityState :
        IFacilityBufferDestinationClaimAuthorityQuery,
        IFacilityBufferMassCapacityAuthorityQuery
    {
        private readonly Vector2Int position;

        internal AuthorityState(Vector2Int position) => this.position = position;
        internal List<FacilityBufferDestinationClaim> Claims { get; } = new();
        internal List<FacilityBufferCapacityProfile> Profiles { get; } = new();

        public IReadOnlyList<FacilityBufferDestinationClaim>
            CaptureAuthorityClaims() => Claims.ToArray();

        public bool TryGetAuthorityClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim claim)
        {
            claim = Claims.SingleOrDefault(value => string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && value.DropPosition == dropPosition);
            return claim != null;
        }

        public IReadOnlyList<FacilityBufferCapacityProfile>
            CaptureAuthorityProfiles() => Profiles.ToArray();

        internal bool HasDestination(string destinationId) => Claims.Any(value =>
            string.Equals(value.DestinationId, destinationId, StringComparison.Ordinal));

        internal void AddPair(
            string destinationId,
            string ownerDomain,
            BuildingInstanceId facilityId,
            long capacity,
            long revision)
        {
            AddClaimOnly(destinationId, ownerDomain, facilityId);
            Profiles.Add(new FacilityBufferCapacityProfile(
                destinationId,
                position,
                ownerDomain,
                destinationId,
                facilityId.Value,
                new PhysicalMassGrams(capacity),
                revision));
        }

        internal void AddClaimOnly(
            string destinationId,
            string ownerDomain,
            BuildingInstanceId facilityId) => Claims.Add(
            new FacilityBufferDestinationClaim(
                destinationId,
                position,
                ownerDomain,
                destinationId,
                facilityId.Value,
                FacilityBufferDestinationAnchorKind.LiveFacility));

        internal void Remove(string destinationId)
        {
            Claims.RemoveAll(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal));
            Profiles.RemoveAll(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal));
        }
    }

    private sealed class StateLifecycle :
        IProductionOutputDestinationLifecycleQuery
    {
        private readonly AuthorityState state;
        private readonly BuildingInstanceId facilityId;

        internal StateLifecycle(AuthorityState state, BuildingInstanceId facilityId)
        {
            this.state = state;
            this.facilityId = facilityId;
        }

        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId requested)
        {
            Require(requested.Equals(facilityId),
                "Authority lifecycle queried with the wrong facility.");
            bool hasAuthority = state.Claims.Count > 0 || state.Profiles.Count > 0;
            ProductionOutputDestinationLifecycleContribution contribution = new(
                "qa-authority-state",
                hasAuthority,
                state.Claims.Count + state.Profiles.Count,
                0,
                0L,
                Array.Empty<ProductionOutputLifecycleBlock>(),
                new string('c', 64));
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                new[] { contribution },
                new string('d', 64));
        }
    }

    private sealed class FakeOutputAuthority :
        IProductionOutputDestinationAuthorityRuntime
    {
        private readonly AuthorityState state;
        private readonly ProductionFacilityHandle handle;
        private readonly string destinationId;

        internal FakeOutputAuthority(
            AuthorityState state,
            ProductionFacilityHandle handle,
            string destinationId)
        {
            this.state = state;
            this.handle = handle;
            this.destinationId = destinationId;
        }

        internal bool FailNextRevoke { get; set; }
        internal bool ApplyThenReportFailure { get; set; }
        internal int RevokeCount { get; private set; }

        public bool TryEnsure(
            ProductionFacilityHandle facility,
            long minimumMassCapacityGrams,
            out FacilityBufferCapacityProfile profile,
            out string failureReason) =>
            TryValidate(facility, out profile, out failureReason);

        public bool TryValidate(
            ProductionFacilityHandle facility,
            out FacilityBufferCapacityProfile profile,
            out string failureReason)
        {
            profile = state.Profiles.SingleOrDefault(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal));
            failureReason = profile != null
                && ReferenceEquals(facility?.RuntimeObject, handle.RuntimeObject)
                ? string.Empty
                : "qa-output-authority-invalid";
            return failureReason.Length == 0;
        }

        public bool TryReplaceProjected(
            IReadOnlyList<ProductionFacilityHandle> facilities,
            IReadOnlyDictionary<string, long> capacityGramsByFacilityId,
            out string failureReason)
        {
            failureReason = "qa-not-supported";
            return false;
        }

        public bool TryRevoke(BuildingInstanceId facilityId, out string failureReason)
        {
            if (FailNextRevoke)
            {
                FailNextRevoke = false;
                failureReason = "qa-output-revoke-deferred";
                return false;
            }
            if (!facilityId.Equals(handle.InstanceId)
                || !state.HasDestination(destinationId))
            {
                failureReason = "qa-output-revoke-invalid";
                return false;
            }
            state.Remove(destinationId);
            RevokeCount++;
            if (ApplyThenReportFailure)
            {
                ApplyThenReportFailure = false;
                failureReason = "qa-output-revoke-committed-before-failure";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeSensorAuthority :
        IProductionStockSensorDestinationAuthorityRuntime
    {
        private readonly AuthorityState state;
        private readonly ProductionFacilityHandle handle;
        private readonly string destinationId;

        internal FakeSensorAuthority(
            AuthorityState state,
            ProductionFacilityHandle handle,
            string destinationId)
        {
            this.state = state;
            this.handle = handle;
            this.destinationId = destinationId;
        }

        internal bool FailNextRevoke { get; set; }
        internal bool ApplyThenReportFailure { get; set; }
        internal int RevokeCount { get; private set; }

        public bool TryEnsure(
            ProductionFacilityHandle facility,
            out long capacityMassGrams,
            out string failureReason)
        {
            bool valid = TryValidate(
                facility,
                out capacityMassGrams,
                out failureReason);
            return valid;
        }

        public bool TryValidate(
            ProductionFacilityHandle facility,
            out long capacityMassGrams,
            out string failureReason)
        {
            FacilityBufferCapacityProfile profile = state.Profiles
                .SingleOrDefault(value => string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
            capacityMassGrams = profile?.MaxMassGrams ?? 0L;
            failureReason = profile != null
                && ReferenceEquals(facility?.RuntimeObject, handle.RuntimeObject)
                ? string.Empty
                : "qa-sensor-authority-invalid";
            return failureReason.Length == 0;
        }

        public bool TryReplaceProjected(
            IReadOnlyList<ProductionFacilityHandle> facilities,
            out string failureReason)
        {
            failureReason = "qa-not-supported";
            return false;
        }

        public bool TryRequireEmpty(
            ProductionFacilityHandle facility,
            out string failureReason)
        {
            bool valid = TryValidate(facility, out _, out failureReason);
            return valid;
        }

        public bool TryRevoke(BuildingInstanceId facilityId, out string failureReason)
        {
            if (FailNextRevoke)
            {
                FailNextRevoke = false;
                failureReason = "qa-sensor-revoke-deferred";
                return false;
            }
            if (!facilityId.Equals(handle.InstanceId)
                || !state.HasDestination(destinationId))
            {
                failureReason = "qa-sensor-revoke-invalid";
                return false;
            }
            state.Remove(destinationId);
            RevokeCount++;
            if (ApplyThenReportFailure)
            {
                ApplyThenReportFailure = false;
                failureReason = "qa-sensor-revoke-committed-before-failure";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixedFacilityQuery : IProductionFacilityHandleQuery
    {
        private readonly ProductionFacilityHandle handle;
        internal FixedFacilityQuery(ProductionFacilityHandle handle) =>
            this.handle = handle;
        public ProductionFacilityHandle CaptureFacility(object runtimeObject) =>
            ReferenceEquals(runtimeObject, handle.RuntimeObject)
                ? handle
                : throw new InvalidOperationException("Unexpected facility object.");
    }

    private sealed class FixedJournal :
        IProductionFacilityDestructiveDrainJournalQuery
    {
        private readonly ProductionFacilityDestructiveDrainEntrySaveData entry;
        internal FixedJournal(ProductionFacilityDestructiveDrainEntrySaveData entry) =>
            this.entry = entry;
        public int Version => 1;
        public IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
            CaptureOpen() => new[] { entry.Clone() };
        public bool TryGet(
            ProductionFacilityDestructiveDrainOperationId operationId,
            out ProductionFacilityDestructiveDrainEntrySaveData found)
        {
            found = operationId.Value == entry.operationId ? entry.Clone() : null;
            return found != null;
        }
    }

    private sealed class EmptySensorRuntime : IProductionStockSensorRuntime
    {
        public int Version => 0;
        public IReadOnlyCollection<string> InstalledFacilityIds =>
            Array.Empty<string>();
        public IReadOnlyCollection<string> AcknowledgedFacilityIds =>
            Array.Empty<string>();
        public IReadOnlyCollection<ProductionStockSensorPhysicalCommitSaveData>
            PendingInstallations =>
            Array.Empty<ProductionStockSensorPhysicalCommitSaveData>();
        public IReadOnlyCollection<ProductionInstalledStockSensorSaveData>
            InstalledSensors =>
            Array.Empty<ProductionInstalledStockSensorSaveData>();
        public IReadOnlyCollection<ProductionStockSensorRemovalSaveData>
            PendingRemovals =>
            Array.Empty<ProductionStockSensorRemovalSaveData>();
        public bool Has(ProductionFacilityHandle facility) => false;
        public bool HasOwnedPhysicalState(ProductionFacilityHandle facility) => false;
        public bool IsAcknowledged(ProductionFacilityHandle facility) => false;
        public ProductionBillCommandResult RequestInstallation(
            ProductionFacilityHandle facility) => Failed();
        public ProductionBillCommandResult Remove(
            ProductionFacilityHandle facility) => Failed();
        public ProductionBillCommandResult Acknowledge(
            ProductionFacilityHandle facility) => Failed();
        public bool TryReconcileDestinationAuthorities(out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public void FinalizeDeliveredSensors()
        {
        }
        private static ProductionBillCommandResult Failed() =>
            ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionSupportUnavailable));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
