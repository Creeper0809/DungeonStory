using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityDestructiveDrainCoordinatorDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Economy/Run Production Facility Destructive Drain Coordinator Contracts")]
    public static void RunAll()
    {
        VerifyCommitAcknowledgeAndForwardWorldBoundary();
        VerifyResidualSensorAuthorityBlocksUpperAdvance();
        VerifyPreflightDefersBeforeJournalCreation();
        VerifyPlanFreezeDriftRejectsBeforeJournalCreation();
        VerifyAuthorityRevokeRebasesPersistedRetryBoundary();
        VerifyWorldRemovalContributorSetDriftRejects();
        Debug.Log(
            "Production facility destructive-drain coordinator contracts passed.");
    }

    private static void VerifyResidualSensorAuthorityBlocksUpperAdvance()
    {
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-destructive-sensor-authority";
        FakeLifecycle lifecycle = new(facilityId);
        FakeAuthorityStateQuery authorityState = new(lifecycle);
        ProductionFacilityDestructiveDrainParticipantRegistry registry =
            new(CreateParticipants(lifecycle));
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots, registry);
        ProductionFacilityDestructiveDrainCoordinator coordinator = new(
            new FakePreflight(ready: true),
            registry,
            journal,
            journal,
            lifecycle,
            authorityState);

        ProductionFacilityDestructiveDrainDriveResult driven =
            coordinator.DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId);
        Require(driven.Status ==
            ProductionFacilityDestructiveDrainDriveStatus.AwaitingAuthorityRevoke,
            "Sensor authority guard fixture did not reach revoke boundary.");
        lifecycle.AuthorityPresent = false;
        authorityState.SensorPresent = true;
        ProductionFacilityDestructiveDrainDriveResult rejected =
            coordinator.RecordAuthorityRevoked(driven.OperationId);
        Require(rejected.Status ==
                ProductionFacilityDestructiveDrainDriveStatus.Conflict
            && rejected.Phase ==
                ProductionFacilityDestructiveDrainPhase.AwaitingAuthorityRevoke
            && journal.TryGet(driven.OperationId, out var retained)
            && retained.revision == driven.Revision,
            "Upper coordinator advanced while the exact sensor pair remained.");
    }

    private static void VerifyCommitAcknowledgeAndForwardWorldBoundary()
    {
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-destructive-coordinator";
        FakeLifecycle lifecycle = new(facilityId);
        ProductionFacilityDestructiveDrainParticipantRegistry registry =
            new(CreateParticipants(lifecycle));
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots, registry);
        ProductionFacilityDestructiveDrainCoordinator coordinator = new(
            new FakePreflight(ready: true),
            registry,
            journal,
            journal,
            lifecycle,
            new FakeAuthorityStateQuery(lifecycle));

        ProductionFacilityDestructiveDrainDriveResult driven =
            coordinator.DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause.StructuralIntegrity,
                facilityId);
        Require(
            driven.Status == ProductionFacilityDestructiveDrainDriveStatus
                .AwaitingAuthorityRevoke
            && driven.Phase == ProductionFacilityDestructiveDrainPhase
                .AwaitingAuthorityRevoke
            && lifecycle.GenericState == 2,
            "coordinator did not commit and acknowledge the planned owner");
        Require(
            journal.TryGet(driven.OperationId, out var awaiting)
            && FindGenericOwner(awaiting).phase ==
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged,
            "journal did not persist the acknowledged participant receipt");

        long stableRevision = driven.Revision;
        ProductionFacilityDestructiveDrainDriveResult replay =
            coordinator.DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause.StructuralIntegrity,
                facilityId);
        Require(
            replay.Status == driven.Status
            && replay.Revision == stableRevision,
            "authority-boundary replay mutated the journal");

        lifecycle.AuthorityPresent = false;
        lifecycle.CapacityAuthorityPresent = false;
        ProductionFacilityDestructiveDrainDriveResult revoked =
            coordinator.RecordAuthorityRevoked(driven.OperationId);
        Require(
            revoked.Status == ProductionFacilityDestructiveDrainDriveStatus
                .AwaitingWorldRemoval
            && revoked.Phase == ProductionFacilityDestructiveDrainPhase
                .AwaitingWorldRemoval,
            "authority revoke acknowledgement did not advance the journal");
        ProductionFacilityDestructiveDrainDriveResult revokeReplay =
            coordinator.RecordAuthorityRevoked(driven.OperationId);
        Require(
            revokeReplay.Revision == revoked.Revision,
            "authority revoke replay changed journal revision");

        string preRemovalCapacityFingerprint = lifecycle.Contribution(
            ProductionFacilityDestructiveDrainParticipantIds
                .CapacityRoutingOutbox);
        lifecycle.WorldPresent = false;
        ProductionFacilityDestructiveDrainDriveResult removed =
            coordinator.RecordWorldRemoved(driven.OperationId);
        Require(
            removed.Status == ProductionFacilityDestructiveDrainDriveStatus
                .WorldRemovedAwaitingCheckpointGc
            && removed.Phase == ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc,
            "world removal acknowledgement did not reach terminal checkpoint");
        Require(
            journal.TryGet(removed.OperationId, out var terminal)
            && !string.Equals(
                preRemovalCapacityFingerprint,
                lifecycle.Contribution(
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CapacityRoutingOutbox),
                StringComparison.Ordinal)
            && string.Equals(
                terminal.participants.Single(value => string.Equals(
                        value.participantId,
                        ProductionFacilityDestructiveDrainParticipantIds
                            .CapacityRoutingOutbox,
                        StringComparison.Ordinal))
                    .expectedCurrentContributionFingerprint,
                lifecycle.Contribution(
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CapacityRoutingOutbox),
                StringComparison.Ordinal),
            "world removal did not rebase the final shared-state contribution");
        ProductionFacilityDestructiveDrainDriveResult removeReplay =
            coordinator.RecordWorldRemoved(driven.OperationId);
        Require(
            removeReplay.Revision == removed.Revision,
            "world removal replay changed journal revision");
        Require(
            !coordinator.TryCollectCheckpointed(
                removed.OperationId,
                removed.Revision,
                out string gcFailure)
            && string.Equals(
                gcFailure,
                "production-facility-destructive-drain-checkpoint-gc-not-atomic",
                StringComparison.Ordinal)
            && journal.TryGet(removed.OperationId, out _),
            "checkpoint GC removed the upper journal without participant GC: "
            + gcFailure);
    }

    private static void VerifyPreflightDefersBeforeJournalCreation()
    {
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-destructive-preflight";
        FakeLifecycle lifecycle = new(facilityId);
        ProductionFacilityDestructiveDrainParticipantRegistry registry =
            new(CreateParticipants(lifecycle));
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots, registry);
        ProductionFacilityDestructiveDrainCoordinator coordinator = new(
            new FakePreflight(ready: false),
            registry,
            journal,
            journal,
            lifecycle,
            new FakeAuthorityStateQuery(lifecycle));

        ProductionFacilityDestructiveDrainDriveResult result =
            coordinator.DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause.CombatCover,
                facilityId);
        Require(
            result.Status == ProductionFacilityDestructiveDrainDriveStatus.Deferred
            && journal.CaptureOpen().Count == 0,
            "deferred start preflight created a durable journal entry");
    }

    private static void VerifyPlanFreezeDriftRejectsBeforeJournalCreation()
    {
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-destructive-plan-freeze";
        FakeLifecycle lifecycle = new(facilityId)
        {
            DriftOnSecondCapture = true
        };
        ProductionFacilityDestructiveDrainParticipantRegistry registry =
            new(CreateParticipants(lifecycle));
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots, registry);
        ProductionFacilityDestructiveDrainCoordinator coordinator = new(
            new FakePreflight(ready: true),
            registry,
            journal,
            journal,
            lifecycle,
            new FakeAuthorityStateQuery(lifecycle));

        ProductionFacilityDestructiveDrainDriveResult result =
            coordinator.DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId);
        Require(
            result.Status == ProductionFacilityDestructiveDrainDriveStatus.Conflict
            && string.Equals(
                result.FailureReason,
                "production-facility-destructive-drain-plan-freeze-drift",
                StringComparison.Ordinal)
            && journal.CaptureOpen().Count == 0,
            "plan-freeze drift created a journal from stale participant plans");
    }

    private static void VerifyAuthorityRevokeRebasesPersistedRetryBoundary()
    {
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-destructive-revoke-rebase";
        FakeLifecycle lifecycle = new(facilityId);
        ProductionFacilityDestructiveDrainParticipantRegistry registry =
            new(CreateParticipants(lifecycle));
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots, registry);
        ProductionFacilityDestructiveDrainCoordinator coordinator = new(
            new FakePreflight(ready: true),
            registry,
            journal,
            journal,
            lifecycle,
            new FakeAuthorityStateQuery(lifecycle));

        ProductionFacilityDestructiveDrainDriveResult driven =
            coordinator.DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId);
        Require(
            journal.TryGet(driven.OperationId, out var before),
            "authority-rebase fixture did not persist the revoke boundary");
        ProductionFacilityDestructiveDrainParticipantSaveData beforeCapacity =
            before.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                StringComparison.Ordinal));

        lifecycle.AuthorityPresent = false;
        lifecycle.CapacityAuthorityPresent = false;
        string finalCapacity = lifecycle.Contribution(
            ProductionFacilityDestructiveDrainParticipantIds
                .CapacityRoutingOutbox);
        ProductionFacilityDestructiveDrainDriveResult revoked =
            coordinator.RecordAuthorityRevoked(driven.OperationId);

        Require(
            journal.TryGet(driven.OperationId, out var persisted),
            "authority revoke did not retain the persisted retry row");
        Require(
            revoked.Status ==
                ProductionFacilityDestructiveDrainDriveStatus.AwaitingWorldRemoval
            && revoked.Revision == driven.Revision + 1L
            && persisted.phase ==
                ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval,
            "authority revoke did not publish a persistable retry boundary");
        ProductionFacilityDestructiveDrainParticipantSaveData afterCapacity =
            persisted.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                StringComparison.Ordinal));
        Require(
            !string.Equals(
                beforeCapacity.expectedCurrentContributionFingerprint,
                finalCapacity,
                StringComparison.Ordinal)
            && string.Equals(
                afterCapacity.expectedCurrentContributionFingerprint,
                finalCapacity,
                StringComparison.Ordinal)
            && string.Equals(
                afterCapacity.preparedContributionFingerprint,
                beforeCapacity.preparedContributionFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                afterCapacity.planFingerprint,
                beforeCapacity.planFingerprint,
                StringComparison.Ordinal),
            "authority revoke rebased provenance instead of only current state");
    }

    private static void VerifyWorldRemovalContributorSetDriftRejects()
    {
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-destructive-world-set-drift";
        FakeLifecycle lifecycle = new(facilityId);
        ProductionFacilityDestructiveDrainParticipantRegistry registry =
            new(CreateParticipants(lifecycle));
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots, registry);
        ProductionFacilityDestructiveDrainCoordinator coordinator = new(
            new FakePreflight(ready: true),
            registry,
            journal,
            journal,
            lifecycle,
            new FakeAuthorityStateQuery(lifecycle));

        ProductionFacilityDestructiveDrainDriveResult driven =
            coordinator.DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId);
        lifecycle.AuthorityPresent = false;
        lifecycle.CapacityAuthorityPresent = false;
        ProductionFacilityDestructiveDrainDriveResult revoked =
            coordinator.RecordAuthorityRevoked(driven.OperationId);
        lifecycle.WorldPresent = false;
        lifecycle.OmitCapacityContribution = true;

        ProductionFacilityDestructiveDrainDriveResult rejected =
            coordinator.RecordWorldRemoved(driven.OperationId);
        Require(
            rejected.Status ==
                ProductionFacilityDestructiveDrainDriveStatus.Conflict
            && string.Equals(
                rejected.FailureReason,
                "production-facility-destructive-drain-world-remove-contribution-set-invalid",
                StringComparison.Ordinal)
            && journal.TryGet(driven.OperationId, out var retained)
            && retained.phase ==
                ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval
            && retained.revision == revoked.Revision,
            "world removal accepted a missing final lifecycle contributor");
    }

    private static IProductionFacilityDestructiveDrainParticipant[]
        CreateParticipants(FakeLifecycle lifecycle) => new[]
    {
        new FakeParticipant(
            lifecycle,
            ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
            Array.Empty<string>(),
            ownsOne: false),
        new FakeParticipant(
            lifecycle,
            ProductionFacilityDestructiveDrainParticipantIds.CapacityRoutingOutbox,
            new[]
            {
                ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting,
                ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills
            },
            ownsOne: false),
        new FakeParticipant(
            lifecycle,
            ProductionFacilityDestructiveDrainParticipantIds
                .CombatEquipmentCrafting,
            Array.Empty<string>(),
            ownsOne: false),
        new FakeParticipant(
            lifecycle,
            ProductionFacilityDestructiveDrainParticipantIds.GenericProductionBills,
            Array.Empty<string>(),
            ownsOne: true),
        new FakeParticipant(
            lifecycle,
            ProductionFacilityDestructiveDrainParticipantIds
                .PhysicalCustodyCarryRecovery,
            new[]
            {
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox
            },
            ownsOne: false),
        new FakeParticipant(
            lifecycle,
            ProductionFacilityDestructiveDrainParticipantIds
                .StockSensorEmbeddedSalvage,
            new[]
            {
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery
            },
            ownsOne: false)
    };

    private static ProductionFacilityDestructiveDrainOwnerSaveData
        FindGenericOwner(ProductionFacilityDestructiveDrainEntrySaveData entry) =>
        entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills,
                StringComparison.Ordinal))
            .owners.Single();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakePreflight :
        IProductionFacilityDestructiveDrainStartPreflight
    {
        private readonly bool ready;

        internal FakePreflight(bool ready) => this.ready = ready;

        public ProductionFacilityDestructiveDrainStartPreflightResult Assess(
            BuildingInstanceId facilityId) => new(
            ready
                ? ProductionFacilityDestructiveDrainStartPreflightStatus.Ready
                : ProductionFacilityDestructiveDrainStartPreflightStatus.Deferred,
            ready ? string.Empty : "qa-preflight-deferred",
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:preflight:" + facilityId.Value));
    }

    private sealed class FakeLifecycle :
        IProductionOutputDestinationLifecycleQuery
    {
        private static readonly string[] ParticipantIds =
        {
            ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
            ProductionFacilityDestructiveDrainParticipantIds.CapacityRoutingOutbox,
            ProductionFacilityDestructiveDrainParticipantIds
                .CombatEquipmentCrafting,
            ProductionFacilityDestructiveDrainParticipantIds.GenericProductionBills,
            ProductionFacilityDestructiveDrainParticipantIds
                .PhysicalCustodyCarryRecovery,
            ProductionFacilityDestructiveDrainParticipantIds
                .StockSensorEmbeddedSalvage
        };

        private readonly BuildingInstanceId facilityId;

        internal FakeLifecycle(BuildingInstanceId facilityId)
        {
            this.facilityId = facilityId;
        }

        internal bool AuthorityPresent { get; set; } = true;
        internal int GenericState { get; set; }
        internal bool DriftOnSecondCapture { get; set; }
        internal bool WorldPresent { get; set; } = true;
        internal bool CapacityAuthorityPresent { get; set; } = true;
        internal bool OmitCapacityContribution { get; set; }
        private int captureCount;

        internal string Contribution(string participantId) =>
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:coordinator:contribution:" + participantId + ":"
                + (string.Equals(
                        participantId,
                        ProductionFacilityDestructiveDrainParticipantIds
                            .GenericProductionBills,
                        StringComparison.Ordinal)
                        ? GenericState
                    : string.Equals(
                            participantId,
                            ProductionFacilityDestructiveDrainParticipantIds
                                .CapacityRoutingOutbox,
                            StringComparison.Ordinal)
                        ? (CapacityAuthorityPresent ? 2 : 0)
                            + (WorldPresent ? 1 : 0)
                        : 0));

        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId requestedFacilityId)
        {
            Require(
                requestedFacilityId.Equals(facilityId),
                "fake lifecycle received the wrong facility");
            captureCount++;
            if (DriftOnSecondCapture && captureCount == 2)
                GenericState = 99;
            ProductionOutputDestinationLifecycleContribution[] contributions =
                ParticipantIds.Where(participantId =>
                        !OmitCapacityContribution
                        || !string.Equals(
                            participantId,
                            ProductionFacilityDestructiveDrainParticipantIds
                                .CapacityRoutingOutbox,
                            StringComparison.Ordinal))
                    .Select(participantId =>
                {
                    bool generic = string.Equals(
                        participantId,
                        ProductionFacilityDestructiveDrainParticipantIds
                            .GenericProductionBills,
                        StringComparison.Ordinal);
                    IReadOnlyList<ProductionOutputLifecycleBlock> blocks =
                        generic && GenericState < 2
                            ? new[]
                            {
                                new ProductionOutputLifecycleBlock(
                                    ProductionOutputLifecycleBlockCode.GenericBill,
                                    1,
                                    1L,
                                    "qa-generic-owner")
                            }
                            : Array.Empty<ProductionOutputLifecycleBlock>();
                    string fingerprint = Contribution(participantId);
                    return new ProductionOutputDestinationLifecycleContribution(
                        participantId,
                        generic && AuthorityPresent,
                        0L,
                        generic && GenericState < 2 ? 1 : 0,
                        generic && GenericState < 2 ? 1L : 0L,
                        blocks,
                        fingerprint,
                        fingerprint);
                }).ToArray();
            string aggregate = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint(
                    "qa:coordinator:aggregate:"
                    + AuthorityPresent + ":" + GenericState + ":"
                    + CapacityAuthorityPresent + ":" + WorldPresent + ":"
                    + OmitCapacityContribution);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                contributions,
                aggregate,
                aggregate);
        }
    }

    private sealed class FakeAuthorityStateQuery :
        IProductionFacilityDestructiveDrainAuthorityStateQuery
    {
        private readonly FakeLifecycle lifecycle;

        internal FakeAuthorityStateQuery(FakeLifecycle lifecycle) =>
            this.lifecycle = lifecycle;

        internal bool SensorPresent { get; set; }

        public ProductionFacilityDestructiveDrainAuthoritySnapshot Capture(
            BuildingInstanceId facilityId)
        {
            ProductionFacilityDestructiveDrainAuthorityPairSnapshot sensor = new(
                "production-sensor:" + facilityId.Value,
                SensorPresent
                    ? ProductionFacilityDestructiveDrainAuthorityPairState.Exact
                    : ProductionFacilityDestructiveDrainAuthorityPairState.Absent,
                string.Empty);
            ProductionFacilityDestructiveDrainAuthorityPairSnapshot output = new(
                ProductionBillRuntime.OutputDestinationPrefix + facilityId.Value,
                lifecycle.AuthorityPresent
                    ? ProductionFacilityDestructiveDrainAuthorityPairState.Exact
                    : ProductionFacilityDestructiveDrainAuthorityPairState.Absent,
                string.Empty);
            return new ProductionFacilityDestructiveDrainAuthoritySnapshot(
                sensor,
                output);
        }
    }

    private sealed class FakeParticipant :
        IProductionFacilityDestructiveDrainParticipant
    {
        private readonly FakeLifecycle lifecycle;
        private readonly bool ownsOne;

        internal FakeParticipant(
            FakeLifecycle lifecycle,
            string participantId,
            IReadOnlyList<string> dependencies,
            bool ownsOne)
        {
            this.lifecycle = lifecycle;
            ParticipantId = participantId;
            DependsOnParticipantIds = dependencies;
            this.ownsOne = ownsOne;
        }

        public string ParticipantId { get; }
        public int ContractVersion => string.Equals(
                ParticipantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage,
                StringComparison.Ordinal)
            ? 2
            : 1;
        public IReadOnlyList<string> DependsOnParticipantIds { get; }

        public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
            ProductionFacilityDestructiveDrainPrepareContext context)
        {
            ProductionFacilityDestructiveDrainOwnerPlan[] owners = ownsOne
                ? new[]
                {
                    new ProductionFacilityDestructiveDrainOwnerPlan(
                        ProductionFacilityDestructiveDrainOwnerStableIds.GenericBill(
                            "qa-coordinator-bill"),
                        ProductionFacilityDestructiveDrainDisposition.Terminalize,
                        string.Empty,
                        ProductionFacilityDestructiveDrainCanonical
                            .ComputeFingerprint("qa:coordinator:request"))
                }
                : Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>();
            string contribution = lifecycle.Contribution(ParticipantId);
            return new ProductionFacilityDestructiveDrainParticipantPlan(
                ParticipantId,
                ContractVersion,
                contribution,
                ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                    "qa:coordinator:plan:" + ParticipantId),
                owners);
        }

        public ProductionFacilityDestructiveDrainStepResult TryCommit(
            ProductionFacilityDestructiveDrainStepContext context)
        {
            if (!ownsOne || lifecycle.GenericState != 0)
                return Conflict();
            lifecycle.GenericState = 1;
            return Applied();
        }

        public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
            ProductionFacilityDestructiveDrainStepContext context)
        {
            if (!ownsOne || lifecycle.GenericState != 1)
                return Conflict();
            lifecycle.GenericState = 2;
            return Applied();
        }

        public ProductionFacilityDestructiveDrainRecoveryResult Recover(
            ProductionFacilityDestructiveDrainStepContext context)
        {
            if (!ownsOne)
            {
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
                    Conflict());
            }
            if (context.Owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned
                && lifecycle.GenericState == 0)
            {
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    Deferred());
            }
            if (context.Owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase
                        .EffectCommittedAwaitingOwnerAck
                && lifecycle.GenericState == 1)
            {
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction
                        .ResumeAcknowledge,
                    Deferred());
            }
            if (context.Owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged
                && lifecycle.GenericState == 2)
            {
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction
                        .AlreadyAcknowledged,
                    Replay());
            }
            return new ProductionFacilityDestructiveDrainRecoveryResult(
                ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
                Conflict());
        }

        private ProductionFacilityDestructiveDrainStepResult Applied() => new(
            ProductionFacilityDestructiveDrainStepStatus.Applied,
            "qa-coordinator-commit",
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:coordinator:receipt"),
            lifecycle.Contribution(ParticipantId));

        private ProductionFacilityDestructiveDrainStepResult Replay() => new(
            ProductionFacilityDestructiveDrainStepStatus.Replay,
            "qa-coordinator-commit",
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:coordinator:receipt"),
            lifecycle.Contribution(ParticipantId));

        private ProductionFacilityDestructiveDrainStepResult Deferred() => new(
            ProductionFacilityDestructiveDrainStepStatus.Deferred,
            string.Empty,
            string.Empty,
            lifecycle.Contribution(ParticipantId));

        private ProductionFacilityDestructiveDrainStepResult Conflict() => new(
            ProductionFacilityDestructiveDrainStepStatus.Conflict,
            string.Empty,
            string.Empty,
            lifecycle.Contribution(ParticipantId));
    }
}
