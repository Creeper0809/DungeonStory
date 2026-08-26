using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EquipmentRepairMaterialOutboxFixture
{
    private const string MaterialItemId = "resource:dark-resin";

    public static bool Run()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        if (!VerifyFacilityBufferAuthorityLifecycle()
            || !VerifyMissingInputIsAtomic(catalog))
        {
            return false;
        }

        TestContext context = new(catalog, failAcknowledgement: true);
        CombatEquipmentRepairOrder order = CreateOrder("qa");
        string equipmentStack = context.Add(
            MaterialItemId,
            1,
            order.FacilityDestinationId);
        string materialA = context.Add(
            MaterialItemId,
            1,
            order.FacilityDestinationId);
        string materialB = context.Add(
            MaterialItemId,
            1,
            order.FacilityDestinationId);

        if (!EquipmentRepairMaterialOutbox.TryCommitOrResume(
                order,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                0.25f,
                0.9f,
                out _)
            || !order.materialsConsumed
            || order.materialTransferInputs.Count != 2
            || context.Repository.GetEditorTestQuantity(equipmentStack) != 1
            || context.Repository.GetEditorTestQuantity(materialA) != 0
            || context.Repository.GetEditorTestQuantity(materialB) != 0
            || !context.Service.TryGetPending(
                order.materialTransferOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return false;
        }

        order.repairOutcomePublished = true;
        if (EquipmentRepairMaterialOutbox.TryAcknowledgeOutcome(
                order,
                context.Service,
                out _)
            || order.materialTransferAcknowledged)
        {
            return false;
        }

        CombatEquipmentRepairOrder restored =
            JsonUtility.FromJson<CombatEquipmentRepairOrder>(
                JsonUtility.ToJson(order));
        if (!EquipmentRepairMaterialOutbox.TryCommitOrResume(
                restored,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                0.25f,
                0.9f,
                out _)
            || !EquipmentRepairMaterialOutbox.TryAcknowledgeOutcome(
                restored,
                context.Service,
                out _)
            || !restored.materialTransferAcknowledged
            || !EquipmentRepairMaterialOutbox.TryCommitOrResume(
                restored,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                0.25f,
                0.9f,
                out _)
            || context.Repository.GetEditorTestQuantity(equipmentStack) != 1
            || context.Repository.GetEditorTestQuantity(materialA) != 0
            || context.Repository.GetEditorTestQuantity(materialB) != 0)
        {
            return false;
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            order.materialTransferRequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        EquipmentRepairMaterialRestoreGuard.ValidateOwnerSet(
            new[] { order },
            new CandidateQuery(candidate));
        if (!Reject(new[] { order }, new CandidateQuery())
            || !Reject(
                Array.Empty<CombatEquipmentRepairOrder>(),
                new CandidateQuery(candidate))
            || !Reject(
                new[] { order },
                new CandidateQuery(Copy(
                    candidate,
                    inputMassGrams: candidate.InputMassGrams + 1)))
            || !Reject(
                new[] { order },
                new CandidateQuery(Copy(
                    candidate,
                    requestFingerprint:
                        candidate.RequestFingerprint + ":mismatch")))
            || !Reject(
                new[] { order },
                new CandidateQuery(Copy(
                    candidate,
                    sourceStackIds: candidate.SourceStackIds
                        .Select((value, index) => index == 0
                            ? value + ":mismatch"
                            : value)
                        .ToArray()))))
        {
            return false;
        }

        EquipmentRepairMaterialRestoreGuard.ValidateOwnerSet(
            new[] { restored },
            new CandidateQuery());
        return Reject(new[] { restored }, new CandidateQuery(candidate));
    }

    private static bool VerifyFacilityBufferAuthorityLifecycle()
    {
        const string ownerDomain = "combat.equipment-maintenance";
        const string ownerOperation = "equipment-repair:fixture";
        const string ownerFacility = "building:repair:fixture";
        const string destination = "equipment-repair:equipment:fixture";
        const long capacityRevision = 1L;
        const long exactMassGrams = 2450L;
        Vector2Int dropPosition = new(2, 3);

        FacilityBufferDestinationClaimRegistry claims = new();
        FixtureOccupancy occupancy = new(exactMassGrams);
        FacilityBufferMassAdmissionService admission = new(claims, occupancy);
        FacilityBufferDestinationLifecycleService lifecycle = new(
            claims,
            claims,
            admission,
            admission);
        FacilityBufferDestinationClaim claim = new(
            destination,
            dropPosition,
            ownerDomain,
            ownerOperation,
            ownerFacility,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile profile = new(
            destination,
            dropPosition,
            ownerDomain,
            ownerOperation,
            ownerFacility,
            new PhysicalMassGrams(exactMassGrams),
            capacityRevision);
        if (!lifecycle.TryReplaceOwnedAuthorities(
                ownerDomain,
                new[] { claim },
                new[] { profile },
                out _))
        {
            return false;
        }

        FacilityBufferMassLotSlice slice = new(
            "equipment-repair-fixture-stack",
            1,
            0L);
        FacilityBufferMassAdmissionRequest request = new(
            "equipment-repair-fixture-transfer",
            destination,
            dropPosition,
            ownerDomain,
            ownerOperation,
            ownerFacility,
            capacityRevision,
            new[] { slice });
        if (!admission.TryReserveExactLot(
                request,
                out FacilityBufferMassAdmissionToken token,
                out _,
                out _)
            || token.ReservedMassGrams != exactMassGrams
            || !admission.TryCommitRouted(
                token,
                occupancy.Fingerprint,
                exactMassGrams,
                out FacilityBufferMassAdmissionReceipt receipt,
                out _,
                out _)
            || receipt.CommittedMassGrams != exactMassGrams)
        {
            return false;
        }

        if (!lifecycle.TryReplaceOwnedAuthorities(
                ownerDomain,
                Array.Empty<FacilityBufferDestinationClaim>(),
                Array.Empty<FacilityBufferCapacityProfile>(),
                out _)
            || claims.CaptureClaims().Count != 0
            || admission.CaptureProfiles().Count != 0
            || admission.TryReserveExactLot(
                request,
                out _,
                out FacilityBufferMassAdmissionFailureCode terminalFailure,
                out _)
            || terminalFailure != FacilityBufferMassAdmissionFailureCode.ProfileMissing)
        {
            return false;
        }

        claims.BeginRestoreCandidate();
        admission.BeginRestoreCandidate();
        if (!lifecycle.TryReplaceOwnedAuthorities(
                ownerDomain,
                new[] { claim },
                new[] { profile },
                out _))
        {
            return false;
        }
        claims.PublishRestoreCandidate();
        admission.PublishRestoreCandidate();
        claims.CompleteRestoreCandidate();
        admission.CompleteRestoreCandidate();
        return claims.CaptureClaims().Count == 1
            && admission.CaptureProfiles().Count == 1
            && admission.CaptureProfiles()[0].MaxMassGrams == exactMassGrams
            && lifecycle.TryReplaceOwnedAuthorities(
                ownerDomain,
                Array.Empty<FacilityBufferDestinationClaim>(),
                Array.Empty<FacilityBufferCapacityProfile>(),
                out _)
            && claims.CaptureClaims().Count == 0
            && admission.CaptureProfiles().Count == 0;
    }

    private static bool VerifyMissingInputIsAtomic(
        IDungeonItemCatalogProvider catalog)
    {
        TestContext context = new(catalog);
        CombatEquipmentRepairOrder order = CreateOrder("missing");
        string equipmentStack = context.Add(
            MaterialItemId,
            1,
            order.FacilityDestinationId);
        string material = context.Add(
            MaterialItemId,
            1,
            order.FacilityDestinationId);
        return !EquipmentRepairMaterialOutbox.TryCommitOrResume(
                order,
                context.Query.GetAllStacks(),
                context.Service,
                equipmentStack,
                0.2f,
                0.9f,
                out _)
            && context.Repository.GetEditorTestQuantity(equipmentStack) == 1
            && context.Repository.GetEditorTestQuantity(material) == 1
            && string.IsNullOrEmpty(order.materialTransferOperationId)
            && !order.materialsConsumed;
    }

    private static CombatEquipmentRepairOrder CreateOrder(string suffix) =>
        new()
        {
            orderId = "equipment-repair:" + suffix,
            equipmentInstanceId = "equipment:" + suffix,
            facilityBuildingId = "building:repair:qa",
            materialItemId = MaterialItemId,
            requiredMaterialAmount = 2,
            requiredWork = 10f,
            completedWork = 10f,
            targetDurability = 0.9f,
            state = CombatEquipmentRepairOrderState.InProgress
        };

    private static bool Reject(
        IReadOnlyList<CombatEquipmentRepairOrder> orders,
        CandidateQuery query)
    {
        try
        {
            EquipmentRepairMaterialRestoreGuard.ValidateOwnerSet(
                orders,
                query);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot Copy(
        PhysicalItemRestoreCandidateDispositionSnapshot source,
        string requestFingerprint = null,
        IReadOnlyList<string> sourceStackIds = null,
        long? inputMassGrams = null) => new(
        source.Kind,
        source.OperationId,
        source.ReasonCode,
        requestFingerprint ?? source.RequestFingerprint,
        sourceStackIds ?? source.SourceStackIds,
        source.Quantity,
        inputMassGrams ?? source.InputMassGrams,
        source.CommitId);

    private sealed class TestContext
    {
        internal TestContext(
            IDungeonItemCatalogProvider catalog,
            bool failAcknowledgement = false)
        {
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemMassQuery mass = new(catalog);
            PhysicalItemBatchDispositionService inner = new(
                Repository,
                mass,
                EditorNullItemMarkerPresenter.Instance);
            Service = new FailOnce(inner)
            {
                FailNext = failAcknowledgement
            };
            Query = new WorldItemQueryService(
                catalog,
                mass,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
        }

        internal WorldItemRepository Repository { get; }
        internal WorldItemQueryService Query { get; }
        internal FailOnce Service { get; }

        internal string Add(
            string itemId,
            int quantity,
            string destinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                itemId,
                quantity,
                WorldItemStackState.FacilityBuffer,
                position: Vector2Int.zero,
                destinationId: destinationId);
    }

    private sealed class CandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> values;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values)
        {
            this.values = values;
        }

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

    private sealed class FixtureOccupancy :
        IFacilityBufferPhysicalOccupancyQuery
    {
        private readonly long exactMassGrams;

        internal FixtureOccupancy(long exactMassGrams)
        {
            this.exactMassGrams = exactMassGrams;
        }

        internal string Fingerprint =>
            $"equipment-repair-fixture-lot:{exactMassGrams}";

        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            if (slices == null
                || slices.Count != 1
                || !string.Equals(
                    slices[0].StackId,
                    "equipment-repair-fixture-stack",
                    StringComparison.Ordinal)
                || slices[0].Quantity != 1
                || slices[0].ExpectedReservationRevision != 0L)
            {
                lot = default;
                failureReason = "equipment-repair-fixture-lot-invalid";
                return false;
            }
            lot = new FacilityBufferExactLotSnapshot(
                Fingerprint,
                new PhysicalMassGrams(exactMassGrams));
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FailOnce : IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        internal FailOnce(IPhysicalItemBatchDispositionService inner) =>
            this.inner = inner;

        internal bool FailNext { get; set; }

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommit(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitPending(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            if (FailNext)
            {
                FailNext = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }
    }
}
