using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DefenseFacilityPhysicalTransactionFixture
{
    private const string FacilityId = "building:defense:qa";
    private const string SupplyDestination =
        "facility-input:defense:building:defense:qa";
    private const string MaintenanceDestination =
        "facility-input:defense-maintenance:building:defense:qa";
    private const string SupplyItemId = "ammo:bolt-iron";

    public static bool Run()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        if (!VerifyMissingInputAtomic(catalog)
            || !VerifyMaintenanceSink(catalog))
        {
            return false;
        }

        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        DefenseFacilityState owner = NewState();
        string sourceA = gateway.Add(SupplyItemId, 2, SupplyDestination);
        string sourceB = gateway.Add(SupplyItemId, 2, SupplyDestination);
        if (!DefenseFacilityPhysicalTransactionOutbox.TryCommitOrResume(
                owner.pendingSupply,
                DefenseFacilityPhysicalCommitKind.SupplyTransfer,
                FacilityId,
                owner.nextSupplyOperationSequence,
                SupplyDestination,
                SupplyItemId,
                4,
                owner.supply,
                4,
                gateway,
                out PhysicalItemBatchDispositionReceipt receipt,
                out _)
            || gateway.Quantity(sourceA) != 0
            || gateway.Quantity(sourceB) != 0
            || receipt.Kind != PhysicalItemDispositionKind.Transfer)
        {
            return false;
        }

        owner.supply = owner.pendingSupply.supplyAfter;
        owner.pendingSupply.phase =
            DefenseFacilityPhysicalCommitPhase.OutcomePublished;
        if (DefenseFacilityPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                owner.pendingSupply,
                gateway,
                out _))
        {
            return false;
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate = ToCandidate(
            owner.pendingSupply,
            receipt);
        DefenseFacilitySaveData serialized = new()
        {
            facilities = new List<DefenseFacilityRecordSaveData>
            {
                ToRecord(owner)
            }
        };
        DefenseFacilitySaveData restoredPayload =
            JsonUtility.FromJson<DefenseFacilitySaveData>(
                JsonUtility.ToJson(serialized));
        DefenseFacilityState restored = FromRecord(
            restoredPayload.facilities.Single());
        Validate(new[] { owner }, candidate);
        if (!Reject(new[] { owner })
            || !Reject(Array.Empty<DefenseFacilityState>(), candidate)
            || !Reject(
                new[] { owner },
                Copy(candidate, mass: candidate.InputMassGrams + 1L))
            || !DefenseFacilityPhysicalTransactionOutbox.TryCommitOrResume(
                restored.pendingSupply,
                DefenseFacilityPhysicalCommitKind.SupplyTransfer,
                FacilityId,
                restored.nextSupplyOperationSequence,
                SupplyDestination,
                SupplyItemId,
                4,
                1,
                4,
                gateway,
                out _,
                out _)
            || !DefenseFacilityPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                restored.pendingSupply,
                gateway,
                out _))
        {
            return false;
        }

        DefenseFacilityPhysicalTransactionOutbox.Clear(restored.pendingSupply);
        restored.nextSupplyOperationSequence++;
        Validate(new[] { restored });
        return Reject(new[] { restored }, candidate)
            && gateway.Quantity(sourceA) == 0
            && gateway.Quantity(sourceB) == 0
            && restored.supply == 5;
    }

    private static bool VerifyMissingInputAtomic(
        IDungeonItemCatalogProvider catalog)
    {
        FixtureGateway gateway = new(catalog);
        DefenseFacilityState state = NewState();
        string source = gateway.Add(SupplyItemId, 3, SupplyDestination);
        return !DefenseFacilityPhysicalTransactionOutbox.TryCommitOrResume(
                state.pendingSupply,
                DefenseFacilityPhysicalCommitKind.SupplyTransfer,
                FacilityId,
                0,
                SupplyDestination,
                SupplyItemId,
                4,
                state.supply,
                4,
                gateway,
                out _,
                out _)
            && gateway.Quantity(source) == 3
            && state.pendingSupply.phase
                == DefenseFacilityPhysicalCommitPhase.None;
    }

    private static bool VerifyMaintenanceSink(
        IDungeonItemCatalogProvider catalog)
    {
        FixtureGateway gateway = new(catalog);
        DefenseFacilityState state = NewState();
        string source = gateway.Add(
            DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId,
            1,
            MaintenanceDestination);
        if (!DefenseFacilityPhysicalTransactionOutbox.TryCommitOrResume(
                state.pendingMaintenance,
                DefenseFacilityPhysicalCommitKind.MaintenanceSink,
                FacilityId,
                state.nextMaintenanceOperationSequence,
                MaintenanceDestination,
                DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId,
                1,
                state.supply,
                0,
                gateway,
                out PhysicalItemBatchDispositionReceipt receipt,
                out _)
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || gateway.Quantity(source) != 0)
        {
            return false;
        }
        state.pendingMaintenance.phase =
            DefenseFacilityPhysicalCommitPhase.OutcomePublished;
        return DefenseFacilityPhysicalTransactionOutbox.TryAcknowledgeOutcome(
            state.pendingMaintenance,
            gateway,
            out _);
    }

    private static DefenseFacilityState NewState() => new()
    {
        facilityPersistentId = FacilityId,
        supply = 1,
        operationalState = DefenseFacilityOperationalState.Jammed
    };

    private static DefenseFacilityRecordSaveData ToRecord(
        DefenseFacilityState state) => new()
    {
        facilityPersistentId = state.facilityPersistentId,
        buildingId = 1,
        condition = 100f,
        supply = state.supply,
        operationalState = state.operationalState,
        allowedPersistentIds = new List<string>(),
        growth = new DefenseFacilityGrowthSaveData(),
        pendingMaintenance = state.pendingMaintenance.DeepClone(),
        pendingSupply = state.pendingSupply.DeepClone(),
        nextMaintenanceOperationSequence =
            state.nextMaintenanceOperationSequence,
        nextSupplyOperationSequence = state.nextSupplyOperationSequence
    };

    private static DefenseFacilityState FromRecord(
        DefenseFacilityRecordSaveData record) => new()
    {
        facilityPersistentId = record.facilityPersistentId,
        supply = record.supply,
        operationalState = record.operationalState,
        pendingMaintenance = record.pendingMaintenance.DeepClone(),
        pendingSupply = record.pendingSupply.DeepClone(),
        nextMaintenanceOperationSequence =
            record.nextMaintenanceOperationSequence,
        nextSupplyOperationSequence = record.nextSupplyOperationSequence
    };

    private static PhysicalItemRestoreCandidateDispositionSnapshot ToCandidate(
        DefenseFacilityPhysicalCommitSaveData owner,
        PhysicalItemBatchDispositionReceipt receipt) => new(
        receipt.Kind,
        receipt.OperationId,
        receipt.ReasonCode,
        owner.requestFingerprint,
        receipt.SourceStackIds,
        receipt.Quantity,
        receipt.InputMassGrams,
        receipt.CommitId);

    private static PhysicalItemRestoreCandidateDispositionSnapshot Copy(
        PhysicalItemRestoreCandidateDispositionSnapshot value,
        long? mass = null) => new(
        value.Kind,
        value.OperationId,
        value.ReasonCode,
        value.RequestFingerprint,
        value.SourceStackIds,
        value.Quantity,
        mass ?? value.InputMassGrams,
        value.CommitId);

    private static void Validate(
        IReadOnlyCollection<DefenseFacilityState> states,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts) =>
        DefenseFacilityPhysicalRestoreGuard.ValidateOwnerSet(
            states,
            new CandidateQuery(receipts));

    private static bool Reject(
        IReadOnlyCollection<DefenseFacilityState> states,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
    {
        try
        {
            Validate(states, receipts);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            values;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values) =>
            this.values = values
                ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot value)
        {
            value = values.FirstOrDefault(candidate => string.Equals(
                candidate.OperationId,
                operationId,
                StringComparison.Ordinal));
            return value != null;
        }
    }

    private sealed class FixtureGateway : IDefenseFacilityPhysicalItemGateway
    {
        private readonly WorldItemRepository repository;
        private readonly WorldItemQueryService query;
        private readonly IPhysicalItemBatchDispositionService dispositions;

        internal FixtureGateway(IDungeonItemCatalogProvider catalog)
        {
            repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemMassQuery mass = new(catalog);
            query = new WorldItemQueryService(
                catalog,
                mass,
                repository,
                EditorNullItemMarkerPresenter.Instance);
            dispositions = new PhysicalItemBatchDispositionService(
                repository,
                mass,
                EditorNullItemMarkerPresenter.Instance);
        }

        internal bool FailNextAcknowledgement { get; set; }

        internal string Add(string itemId, int quantity, string destinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                quantity,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId);

        internal int Quantity(string stackId) =>
            repository.GetEditorTestQuantity(stackId);

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            query.GetAllStacks();

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => dispositions.TryCommitPending(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            dispositions.TryGetPending(operationId, out receipt);

        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }
            return dispositions.Acknowledge(commitId, out failureReason);
        }
    }
}
