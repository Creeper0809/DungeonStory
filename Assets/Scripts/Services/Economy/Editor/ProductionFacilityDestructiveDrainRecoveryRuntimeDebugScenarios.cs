using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityDestructiveDrainRecoveryRuntimeDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Economy/Run Destructive Drain Recovery Runtime Contracts")]
    public static void RunAll()
    {
        VerifyRestoreQueuesWorldRemovalUntilTick();
        VerifyWorldRemovalAcknowledgementRetriesWithoutParticipantReplay();
        VerifyAuthorityConflictDoesNotBusyRetry();
        VerifyAuthorityDeferredRequeuesAndAdvances();
        Debug.Log("Production destructive-drain recovery runtime contracts passed.");
    }

    private static void VerifyAuthorityConflictDoesNotBusyRetry()
    {
        using AuthorityFixture fixture = new(
            ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                .Conflict);
        ProductionFacilityDestructiveRemovalResult result =
            fixture.Runtime.RequestAndDrive(
                fixture.Building,
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition);
        Require(result.Status ==
                ProductionFacilityDestructiveRemovalStatus.Conflict
            && fixture.Revoker.CallCount == 1,
            "Authority conflict was not surfaced as a terminal typed conflict.");
        fixture.Runtime.Tick();
        Require(fixture.Revoker.CallCount == 1,
            "Authority conflict was retried on the next Tick.");
    }

    private static void VerifyAuthorityDeferredRequeuesAndAdvances()
    {
        using AuthorityFixture fixture = new(
            ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                .Deferred);
        ProductionFacilityDestructiveRemovalResult first =
            fixture.Runtime.RequestAndDrive(
                fixture.Building,
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition);
        Require(first.Status ==
                ProductionFacilityDestructiveRemovalStatus.DeferredAccepted
            && fixture.Revoker.CallCount == 1,
            "Transient authority failure was not accepted for forward retry.");

        fixture.Revoker.Disposition =
            ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                .Applied;
        fixture.Runtime.Tick();
        Require(fixture.Revoker.CallCount == 2
            && fixture.Coordinator.AuthorityRecordCount == 1
            && fixture.Coordinator.WorldRecordCount == 1
            && fixture.Journal.Entry.phase ==
                ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc,
            "Deferred authority convergence did not resume through the world boundary.");
    }

    private static void VerifyRestoreQueuesWorldRemovalUntilTick()
    {
        Fixture fixture = new(failFirstWorldRecord: false);
        fixture.Runtime.OnRestoreCompleted();
        Require(fixture.World.CallCount == 0
            && fixture.Coordinator.WorldRecordCount == 0
            && fixture.Coordinator.DriveCount == 0,
            "Restore hook mutated the world instead of queueing recovery.");
        RequireThrows<InvalidOperationException>(
            fixture.Runtime.ValidateBeforeCapture,
            "Capture guard accepted an absent world before upper acknowledgement.");

        fixture.Runtime.Tick();
        Require(fixture.World.CallCount == 1
            && fixture.Coordinator.WorldRecordCount == 1
            && fixture.Coordinator.DriveCount == 0
            && fixture.Revoker.CallCount == 0
            && fixture.Journal.Entry.phase ==
                ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc,
            "AwaitingWorldRemoval replay touched participants or failed to acknowledge the world.");
        fixture.Runtime.ValidateBeforeCapture();

        fixture.Runtime.Tick();
        Require(fixture.World.CallCount == 1
            && fixture.Coordinator.WorldRecordCount == 1,
            "Terminal recovery replay repeated world mutation.");
    }

    private static void
        VerifyWorldRemovalAcknowledgementRetriesWithoutParticipantReplay()
    {
        Fixture fixture = new(failFirstWorldRecord: true);
        fixture.Runtime.Start();
        fixture.Runtime.Tick();
        Require(fixture.World.CallCount == 1
            && fixture.Coordinator.WorldRecordCount == 1
            && fixture.Journal.Entry.phase ==
                ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval
            && fixture.Coordinator.DriveCount == 0
            && fixture.Revoker.CallCount == 0,
            "First acknowledgement failure did not retain the forward world boundary.");

        fixture.Runtime.Tick();
        Require(fixture.World.CallCount == 2
            && fixture.World.AlreadyAppliedCount == 1
            && fixture.Coordinator.WorldRecordCount == 2
            && fixture.Journal.Entry.phase ==
                ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc
            && fixture.Coordinator.DriveCount == 0
            && fixture.Revoker.CallCount == 0,
            "Forward retry reran participants or failed to acknowledge already-applied world removal.");
    }

    private sealed class Fixture
    {
        internal Fixture(bool failFirstWorldRecord)
        {
            BuildingInstanceId facilityId =
                (BuildingInstanceId)"building:qa-drain-recovery";
            Journal = new FakeJournal(new ProductionFacilityDestructiveDrainEntrySaveData
            {
                operationId = ProductionFacilityDestructiveDrainOperationId
                    .FromFacility(facilityId).Value,
                initiatingMutationOperationId =
                    ProductionFacilityDestructiveDrainCanonical
                        .BuildInitiatingMutationOperationId(
                            ProductionFacilityDestructiveDrainCause
                                .ExplicitDemolition,
                            facilityId),
                cause = ProductionFacilityDestructiveDrainCause
                    .ExplicitDemolition,
                facilityId = facilityId.Value,
                destinationId = ProductionOutputDestinationId
                    .FromFacility(facilityId).Value,
                phase = ProductionFacilityDestructiveDrainPhase
                    .AwaitingWorldRemoval,
                preparedLifecycleFingerprint = new string('a', 64),
                expectedCurrentLifecycleFingerprint = new string('b', 64),
                revision = 7L,
                participants = new List<
                    ProductionFacilityDestructiveDrainParticipantSaveData>()
            });
            Coordinator = new FakeCoordinator(Journal, failFirstWorldRecord);
            Revoker = new FakeRevoker();
            World = new FakeWorldRemoval();
            Runtime = new ProductionFacilityDestructiveDrainRecoveryRuntime(
                Journal,
                Coordinator,
                Revoker,
                World,
                new EmptyBuildingWorld());
        }

        internal FakeJournal Journal { get; }
        internal FakeCoordinator Coordinator { get; }
        internal FakeRevoker Revoker { get; }
        internal FakeWorldRemoval World { get; }
        internal ProductionFacilityDestructiveDrainRecoveryRuntime Runtime { get; }
    }

    private sealed class AuthorityFixture : IDisposable
    {
        internal AuthorityFixture(
            ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                disposition)
        {
            BuildingInstanceId facilityId =
                (BuildingInstanceId)"building:qa-drain-authority-recovery";
            GameObject root = new("DrainAuthorityRecoveryFixture");
            Building = root.AddComponent<BuildableObject>();
            Building.RestorePersistentIdentity(facilityId);
            Journal = new FakeJournal(new ProductionFacilityDestructiveDrainEntrySaveData
            {
                operationId = ProductionFacilityDestructiveDrainOperationId
                    .FromFacility(facilityId).Value,
                initiatingMutationOperationId =
                    ProductionFacilityDestructiveDrainCanonical
                        .BuildInitiatingMutationOperationId(
                            ProductionFacilityDestructiveDrainCause
                                .ExplicitDemolition,
                            facilityId),
                cause = ProductionFacilityDestructiveDrainCause
                    .ExplicitDemolition,
                facilityId = facilityId.Value,
                destinationId = ProductionOutputDestinationId
                    .FromFacility(facilityId).Value,
                phase = ProductionFacilityDestructiveDrainPhase
                    .AwaitingAuthorityRevoke,
                preparedLifecycleFingerprint = new string('a', 64),
                expectedCurrentLifecycleFingerprint = new string('b', 64),
                revision = 7L,
                participants = new List<
                    ProductionFacilityDestructiveDrainParticipantSaveData>()
            });
            Coordinator = new FakeCoordinator(Journal, failFirstWorldRecord: false);
            Revoker = new FakeRevoker { Disposition = disposition };
            Runtime = new ProductionFacilityDestructiveDrainRecoveryRuntime(
                Journal,
                Coordinator,
                Revoker,
                new FakeWorldRemoval(),
                new SingleBuildingWorld(Building));
        }

        internal BuildableObject Building { get; }
        internal FakeJournal Journal { get; }
        internal FakeCoordinator Coordinator { get; }
        internal FakeRevoker Revoker { get; }
        internal ProductionFacilityDestructiveDrainRecoveryRuntime Runtime { get; }

        public void Dispose()
        {
            if (Building != null)
                UnityEngine.Object.DestroyImmediate(Building.gameObject);
        }
    }

    private sealed class FakeJournal :
        IProductionFacilityDestructiveDrainJournalQuery
    {
        internal FakeJournal(ProductionFacilityDestructiveDrainEntrySaveData entry) =>
            Entry = entry;

        internal ProductionFacilityDestructiveDrainEntrySaveData Entry { get; set; }
        public int Version { get; private set; } = 1;

        public IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
            CaptureOpen() => Entry == null
            ? Array.Empty<ProductionFacilityDestructiveDrainEntrySaveData>()
            : new[] { Entry.Clone() };

        public bool TryGet(
            ProductionFacilityDestructiveDrainOperationId operationId,
            out ProductionFacilityDestructiveDrainEntrySaveData entry)
        {
            entry = null;
            if (Entry == null
                || !string.Equals(
                    Entry.operationId,
                    operationId.Value,
                    StringComparison.Ordinal))
            {
                return false;
            }
            entry = Entry.Clone();
            return true;
        }

        internal void Replace(
            ProductionFacilityDestructiveDrainEntrySaveData entry)
        {
            Entry = entry;
            Version++;
        }
    }

    private sealed class FakeCoordinator :
        IProductionFacilityDestructiveDrainCoordinator
    {
        private readonly FakeJournal journal;
        private bool failFirstWorldRecord;

        internal FakeCoordinator(
            FakeJournal journal,
            bool failFirstWorldRecord)
        {
            this.journal = journal;
            this.failFirstWorldRecord = failFirstWorldRecord;
        }

        internal int DriveCount { get; private set; }
        internal int AuthorityRecordCount { get; private set; }
        internal int WorldRecordCount { get; private set; }

        public ProductionFacilityDestructiveDrainDriveResult
            DriveToAuthorityRevoke(
                ProductionFacilityDestructiveDrainCause cause,
                BuildingInstanceId facilityId)
        {
            DriveCount++;
            throw new InvalidOperationException(
                "AwaitingWorldRemoval must not replay participant driving.");
        }

        public ProductionFacilityDestructiveDrainDriveResult RecordAuthorityRevoked(
            ProductionFacilityDestructiveDrainOperationId operationId)
        {
            AuthorityRecordCount++;
            ProductionFacilityDestructiveDrainEntrySaveData current =
                journal.Entry.Clone();
            current.phase = ProductionFacilityDestructiveDrainPhase
                .AwaitingWorldRemoval;
            current.revision++;
            journal.Replace(current);
            return new ProductionFacilityDestructiveDrainDriveResult(
                ProductionFacilityDestructiveDrainDriveStatus.AwaitingWorldRemoval,
                operationId,
                current.phase,
                current.revision,
                string.Empty);
        }

        public ProductionFacilityDestructiveDrainDriveResult RecordWorldRemoved(
            ProductionFacilityDestructiveDrainOperationId operationId)
        {
            WorldRecordCount++;
            ProductionFacilityDestructiveDrainEntrySaveData current =
                journal.Entry.Clone();
            if (failFirstWorldRecord)
            {
                failFirstWorldRecord = false;
                return new ProductionFacilityDestructiveDrainDriveResult(
                    ProductionFacilityDestructiveDrainDriveStatus.Deferred,
                    operationId,
                    current.phase,
                    current.revision,
                    "qa-world-record-deferred");
            }
            current.phase = ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc;
            current.revision++;
            journal.Replace(current);
            return new ProductionFacilityDestructiveDrainDriveResult(
                ProductionFacilityDestructiveDrainDriveStatus
                    .WorldRemovedAwaitingCheckpointGc,
                operationId,
                current.phase,
                current.revision,
                string.Empty);
        }

        public bool TryCollectCheckpointed(
            ProductionFacilityDestructiveDrainOperationId operationId,
            long expectedRevision,
            out string failureReason)
        {
            failureReason = "qa-not-supported";
            return false;
        }
    }

    private sealed class FakeRevoker :
        IProductionFacilityDestructiveDrainAuthorityRevoker
    {
        internal int CallCount { get; private set; }
        internal ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
            Disposition { get; set; } =
            ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                .Applied;

        public ProductionFacilityDestructiveDrainAuthorityConvergenceResult
            TryConverge(
            BuildableObject facility,
            ProductionFacilityDestructiveDrainCause cause,
            ProductionFacilityDestructiveDrainOperationId operationId,
            long expectedRevision)
        {
            CallCount++;
            return new ProductionFacilityDestructiveDrainAuthorityConvergenceResult(
                Disposition,
                Disposition is
                    ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                        .Applied
                    or ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                        .AlreadyApplied
                    ? string.Empty
                    : "qa-authority-convergence-" + Disposition);
        }
    }

    private sealed class FakeWorldRemoval :
        IProductionFacilityDestructiveDrainWorldRemovalPort
    {
        internal int CallCount { get; private set; }
        internal int AlreadyAppliedCount { get; private set; }

        public ProductionFacilityWorldRemovalResult TryEnsureRemoved(
            BuildingInstanceId facilityId)
        {
            CallCount++;
            ProductionFacilityWorldRemovalDisposition disposition =
                CallCount == 1
                    ? ProductionFacilityWorldRemovalDisposition.Applied
                    : ProductionFacilityWorldRemovalDisposition.AlreadyApplied;
            if (disposition ==
                ProductionFacilityWorldRemovalDisposition.AlreadyApplied)
            {
                AlreadyAppliedCount++;
            }
            return new ProductionFacilityWorldRemovalResult(
                disposition,
                string.Empty);
        }
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class SingleBuildingWorld : IBuildingWorldQuery
    {
        private readonly BuildableObject building;
        internal SingleBuildingWorld(BuildableObject building) =>
            this.building = building;
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => new[] { building };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
