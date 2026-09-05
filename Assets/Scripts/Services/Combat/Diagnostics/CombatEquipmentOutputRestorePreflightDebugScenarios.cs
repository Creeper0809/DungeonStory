#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentOutputRestorePreflightDebugScenarios
{
    private const string FacilityId = "building:qa:combat-output";
    private static readonly string CapacityDigest = new('a', 64);

    [MenuItem("DungeonStory/V27/Combat/Run Output Restore Preflight")]
    public static void RunAll()
    {
        ProductionOutputMaximumMassProjection projection = Projection();
        ProductionOutputBatchMaximumMassProof proof = new(new[] { projection });

        VerifySuccess(projection, proof);
        VerifyOwnerTamper(projection, proof);
        VerifyLateOwnerAtomicity(projection, proof);
        Debug.Log("[CombatEquipmentOutputRestorePreflight] focused scenarios passed.");
    }

    private static void VerifySuccess(
        ProductionOutputMaximumMassProjection projection,
        ProductionOutputBatchMaximumMassProof proof)
    {
        CombatEquipmentCraftOrderSaveData order = Order("0001", projection, proof);
        RecordingJoin join = new();
        RecordingDetachedCapacity detached = new();
        IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement> result =
            CombatEquipmentOutputRestorePreflight
                .ValidateAndBuildAcknowledgements(
                    new[] { order },
                    new FixedMaximumMassRegistry(projection),
                    detached,
                    join);
        Require(result.Count == 1
            && join.AdoptCount == 1
            && join.RequireCount == 0
            && detached.CallCount == 1
            && order.outputPhase == CombatEquipmentCraftOutputPhase
                .RestoredOutputAwaitingInputAcknowledgement
            && order.outputPublication.outputAcknowledged
            && order.outputPublication.restoredInCurrentTransaction,
            "Valid Combat output owner did not adopt exactly once.");
    }

    private static void VerifyOwnerTamper(
        ProductionOutputMaximumMassProjection projection,
        ProductionOutputBatchMaximumMassProof proof)
    {
        MutateAndRequireNoJoin(projection, proof,
            order => order.outputPublication.ownerFacilityId += ":drift",
            "provenance drifted");
        MutateAndRequireNoJoin(projection, proof,
            order => order.outputPublication.outcomeFingerprint = new string('b', 64),
            "provenance drifted");
        MutateAndRequireNoJoin(projection, proof,
            order => order.outputPublication.maximumMassProofDigest =
                new string('c', 64),
            "proof is stale");
        MutateAndRequireNoJoin(projection, proof,
            order => order.outputPublication.maximumBatchMassGrams++,
            "proof is stale");
        MutateAndRequireNoJoin(projection, proof,
            order => order.outputPublication.capacitySourceDigest =
                new string('d', 64),
            "capacity source drifted");
        MutateAndRequireNoJoin(projection, proof,
            order => order.outputPublication.requiredMinimumCapacityGrams++,
            "capacity source drifted");
    }

    private static void VerifyLateOwnerAtomicity(
        ProductionOutputMaximumMassProjection projection,
        ProductionOutputBatchMaximumMassProof proof)
    {
        CombatEquipmentCraftOrderSaveData first = Order("0001", projection, proof);
        CombatEquipmentCraftOrderSaveData second = Order("0002", projection, proof);
        second.outputPublication.maximumBatchMassGrams++;
        RecordingJoin join = new();
        RecordingDetachedCapacity detached = new();
        ExpectMessage<InvalidOperationException>(
            () => CombatEquipmentOutputRestorePreflight
                .ValidateAndBuildAcknowledgements(
                    new[] { first, second },
                    new FixedMaximumMassRegistry(projection),
                    detached,
                    join),
            "proof is stale");
        Require(join.AdoptCount == 0
            && join.RequireCount == 0
            && !first.outputPublication.outputAcknowledged
            && first.outputPhase == CombatEquipmentCraftOutputPhase
                .PublishedAwaitingInputAcknowledgement,
            "Late Combat owner tamper partially adopted an earlier owner.");
    }

    private static void MutateAndRequireNoJoin(
        ProductionOutputMaximumMassProjection projection,
        ProductionOutputBatchMaximumMassProof proof,
        Action<CombatEquipmentCraftOrderSaveData> mutate,
        string token)
    {
        CombatEquipmentCraftOrderSaveData order = Order("0001", projection, proof);
        mutate(order);
        RecordingJoin join = new();
        ExpectMessage<InvalidOperationException>(
            () => CombatEquipmentOutputRestorePreflight
                .ValidateAndBuildAcknowledgements(
                    new[] { order },
                    new FixedMaximumMassRegistry(projection),
                    new RecordingDetachedCapacity(),
                    join),
            token);
        Require(join.AdoptCount == 0 && join.RequireCount == 0,
            "Combat tamper reached the physical restore join.");
    }

    private static CombatEquipmentCraftOrderSaveData Order(
        string suffix,
        ProductionOutputMaximumMassProjection projection,
        ProductionOutputBatchMaximumMassProof proof)
    {
        CombatEquipmentCraftOrderSaveData order = new()
        {
            orderId = "combat-craft-order:qa:" + suffix,
            definitionId = "ammo:qa",
            facilityPersistentId = FacilityId,
            outputItemId = projection.Descriptor.ItemId,
            outputQuantity = 1,
            outputCapability = ProductionOutputCapabilitySaveData.Freeze(
                projection.Descriptor),
            outputPhase = CombatEquipmentCraftOutputPhase
                .PublishedAwaitingInputAcknowledgement
        };
        order.outputPublication = new ProductionDomainOutputPublicationSaveData
        {
            publicationAttempt = 1,
            batchCommitId = CombatEquipmentCraftOutputTransaction.BatchCommitId(order),
            outcomeFingerprint = CombatEquipmentCraftOutputTransaction
                .CaptureOutcomeFingerprint(order),
            maximumMassProofDigest = proof.SourceDigest,
            maximumBatchMassGrams = proof.MaximumBatchMassGrams,
            capacitySourceDigest = CapacityDigest,
            requiredMinimumCapacityGrams = 4_000L,
            ownerFacilityId = FacilityId
        };
        return order;
    }

    private static ProductionOutputMaximumMassProjection Projection()
    {
        ProductionOutputCapabilityDescriptor descriptor = new(
            "output:qa-combat-ammunition",
            "ammo:qa",
            ProductionOutputCapabilityIds.CombatAmmunitionCraft,
            ProductionOutputCapabilityIds.CombatAmmunitionCraftVersion,
            ProductionOutputCapabilityIds.CombatAmmunitionStateCodec,
            ProductionOutputCapabilityIds.CombatAmmunitionStateCodecVersion,
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                "output:qa-combat-ammunition",
                "ammo:qa",
                ProductionOutputCapabilityIds.CombatAmmunitionCraft,
                ProductionOutputCapabilityIds.CombatAmmunitionCraftVersion,
                ProductionOutputCapabilityIds.CombatAmmunitionStateCodec,
                ProductionOutputCapabilityIds.CombatAmmunitionStateCodecVersion));
        return new ProductionOutputMaximumMassProjection(
            descriptor,
            1,
            1_000L,
            1_000L,
            1L,
            new string('e', 64));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ExpectMessage<T>(Action action, string token)
        where T : Exception
    {
        try { action(); }
        catch (T exception)
        {
            Require(exception.Message.Contains(
                    token,
                    StringComparison.OrdinalIgnoreCase),
                "Unexpected failure: " + exception.Message);
            return;
        }
        throw new InvalidOperationException(
            "Expected " + typeof(T).Name + " containing '" + token + "'.");
    }

    private sealed class FixedMaximumMassRegistry :
        IProductionOutputMaximumMassRegistry
    {
        private readonly ProductionOutputMaximumMassProjection projection;
        internal FixedMaximumMassRegistry(
            ProductionOutputMaximumMassProjection projection) =>
            this.projection = projection;
        public IReadOnlyList<string> CapabilityIds => Array.Empty<string>();
        public IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
            CapabilityContracts =>
                Array.Empty<ProductionOutputCapabilityContractSnapshot>();
        public string RegistryFingerprint => new string('f', 64);
        public ProductionOutputMaximumMassProjection CaptureAutomatic(
            string outputLineId,
            string itemId,
            int maximumQuantity) => throw new NotSupportedException();
        public ProductionOutputMaximumMassProjection CaptureDeclared(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity)
        {
            if (descriptor.Fingerprint != projection.Descriptor.Fingerprint
                || maximumQuantity != 1)
                throw new InvalidOperationException("Unexpected maximum request.");
            return projection;
        }
    }

    private sealed class RecordingDetachedCapacity :
        IProductionOutputDetachedFacilityCapacityRestoreGuard
    {
        public int CallCount { get; private set; }
        public ProductionOutputBufferCapacitySourceSnapshot Validate(
            string ownerStableId,
            string facilityInstanceId,
            ProductionOutputBatchMaximumMassProof maximumMassProof,
            string savedCapacitySourceDigest,
            long savedRequiredMinimumCapacityGrams)
        {
            CallCount++;
            if (facilityInstanceId != FacilityId
                || savedCapacitySourceDigest != CapacityDigest
                || savedRequiredMinimumCapacityGrams != 4_000L)
                throw new InvalidOperationException(
                    "Combat detached capacity source drifted.");
            return new ProductionOutputBufferCapacitySourceSnapshot(
                4,
                maximumMassProof.MaximumBatchMassGrams,
                4_000L,
                4_000L,
                4_000L,
                CapacityDigest);
        }
    }

    private sealed class RecordingJoin : IProductionDomainOutputRestoreJoin
    {
        public int AdoptCount { get; private set; }
        public int RequireCount { get; private set; }
        public ProductionDomainOutputRestoreAcknowledgement AdoptPending(
            ProductionDomainOutputPublicationSaveData owner)
        {
            AdoptCount++;
            return new ProductionDomainOutputRestoreAcknowledgement(
                new FacilityBufferPlannedOutputRestoreBatchSnapshot(
                    owner.batchCommitId,
                    owner.outcomeFingerprint,
                    new string('1', 64),
                    1,
                    1_000L,
                    Array.Empty<
                        FacilityBufferPlannedOutputRestoreStackSnapshot>()),
                FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned);
        }
        public void RequireNoPending(
            ProductionDomainOutputPublicationSaveData owner) => RequireCount++;
        public void Acknowledge(
            IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement> candidates)
        {
        }
    }
}
#endif
