#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class
    ProductionOutputDetachedFacilityCapacityRestoreGuardDebugScenarios
{
    private const string OwnerId = "production-output-owner:qa-detached";
    private const string FacilityId = "building:qa:detached-capacity";
    private static readonly string SourceDigest = new('a', 64);

    [MenuItem("DungeonStory/V27/Production/Run Detached Capacity Restore Guard")]
    public static void RunAll()
    {
        GameObject firstObject = new("Detached Capacity Candidate A");
        GameObject duplicateObject = new("Detached Capacity Candidate B");
        try
        {
            BuildableObject first = firstObject.AddComponent<BuildableObject>();
            first.RestorePersistentIdentity((BuildingInstanceId)FacilityId);
            BuildableObject duplicate =
                duplicateObject.AddComponent<BuildableObject>();
            duplicate.RestorePersistentIdentity((BuildingInstanceId)FacilityId);
            ProductionFacilityHandle handle = new(
                first,
                (BuildingInstanceId)FacilityId,
                new Vector2Int(4, 7),
                isDestroyed: false,
                stockSensorInstallationItemId: string.Empty,
                allowsOverflowDump: false,
                overflowOffset: Vector2Int.zero,
                definitionId: "building:qa:detached-capacity-definition",
                workstationTag: "workstation:qa-detached-capacity",
                outputBufferCycleCapacity: 4,
                processFluidProfile:
                    ProductionFacilityProcessFluidCapacityProfile.Empty);
            ProductionOutputBufferCapacitySourceSnapshot source = new(
                cycleCapacity: 4,
                maximumBatchMassGrams: 1_000L,
                projectedPortfolioCapacityGrams: 4_000L,
                batchMinimumCapacityGrams: 4_000L,
                requiredMinimumCapacityGrams: 4_000L,
                sourceDigest: SourceDigest);
            ProductionOutputBatchMaximumMassProof proof = Proof();

            FixedWorldCandidates world = new(new[] { first });
            ProductionOutputDetachedFacilityCapacityRestoreGuard guard = new(
                world,
                new FixedFacilityHandles(first, handle),
                new FixedCapacityProjector(source));
            ProductionOutputBufferCapacitySourceSnapshot validated =
                guard.Validate(
                    OwnerId,
                    FacilityId,
                    proof,
                    SourceDigest,
                    4_000L);
            Require(string.Equals(
                    validated.SourceDigest,
                    SourceDigest,
                    StringComparison.Ordinal)
                && validated.RequiredMinimumCapacityGrams == 4_000L,
                "Detached capacity source was not returned exactly.");

            ExpectMessage<InvalidOperationException>(
                () => guard.Validate(
                    OwnerId,
                    FacilityId,
                    proof,
                    new string('b', 64),
                    4_000L),
                "capacity source drifted");
            ExpectMessage<InvalidOperationException>(
                () => guard.Validate(
                    OwnerId,
                    FacilityId,
                    proof,
                    SourceDigest,
                    4_001L),
                "capacity source drifted");
            ExpectMessage<InvalidOperationException>(
                () => new ProductionOutputDetachedFacilityCapacityRestoreGuard(
                        new FixedWorldCandidates(Array.Empty<BuildableObject>()),
                        new FixedFacilityHandles(first, handle),
                        new FixedCapacityProjector(source))
                    .Validate(
                        OwnerId,
                        FacilityId,
                        proof,
                        SourceDigest,
                        4_000L),
                "resolve exactly one facility");
            ExpectMessage<InvalidOperationException>(
                () => new ProductionOutputDetachedFacilityCapacityRestoreGuard(
                        new FixedWorldCandidates(new[] { first, duplicate }),
                        new FixedFacilityHandles(first, handle),
                        new FixedCapacityProjector(source))
                    .Validate(
                        OwnerId,
                        FacilityId,
                        proof,
                        SourceDigest,
                        4_000L),
                "resolve exactly one facility");
            ExpectMessage<InvalidOperationException>(
                () => new ProductionOutputDetachedFacilityCapacityRestoreGuard(
                        new FixedWorldCandidates(null),
                        new FixedFacilityHandles(first, handle),
                        new FixedCapacityProjector(source))
                    .Validate(
                        OwnerId,
                        FacilityId,
                        proof,
                        SourceDigest,
                        4_000L),
                "requires the facility-world candidate");

            VerifyApparelAdapter();

            Debug.Log(
                "[ProductionOutputDetachedFacilityCapacityRestoreGuard] focused scenarios passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(duplicateObject);
            UnityEngine.Object.DestroyImmediate(firstObject);
        }
    }

    private static void VerifyApparelAdapter()
    {
        ProductionOutputBatchMaximumMassProof proof = Proof();
        ProductionOutputCapabilitySaveData capability =
            ProductionOutputCapabilitySaveData.Freeze(
                proof.Projections[0].Descriptor);
        ApparelWorkOrderSaveData live = new()
        {
            orderId = "apparel-order:qa-live",
            kind = ApparelWorkOrderKind.Craft,
            facilityInstanceId = FacilityId,
            craftOutputCapability = capability,
            craftMaximumMassProofDigest = proof.SourceDigest,
            craftMaximumBatchMassGrams = proof.MaximumBatchMassGrams,
            craftCapacitySourceDigest = SourceDigest,
            craftRequiredMinimumCapacityGrams = 4_000L
        };
        RecordingDetachedCapacityGuard detached = new();
        ApparelOutputDetachedCapacityRestoreGuard guard = new(
            new FixedMaximumMassRegistry(proof.Projections[0]),
            detached);
        guard.Validate(
            new[] { live },
            new[]
            {
                new ApparelWorkOrderTerminalStateSaveData
                {
                    sourceOrder = null,
                    sourceTerminalReceipt =
                        new ProductionApparelOrderSourceTerminalReceipt()
                }
            });
        Require(detached.CallCount == 1
            && string.Equals(
                detached.LastOwnerStableId,
                "apparel-craft:apparel-order:qa-live",
                StringComparison.Ordinal),
            "Apparel adapter did not validate exactly the live capacity owner.");

        ApparelWorkOrderSaveData drift = live.CloneForDetachedCapacityQa();
        drift.craftMaximumBatchMassGrams++;
        ExpectMessage<InvalidOperationException>(
            () => guard.Validate(
                new[] { drift },
                Array.Empty<ApparelWorkOrderTerminalStateSaveData>()),
            "maximum proof drifted");
    }

    private static ProductionOutputBatchMaximumMassProof Proof()
    {
        ProductionOutputCapabilityDescriptor descriptor = new(
            "output:qa-detached-capacity",
            "item:qa-detached-capacity",
            ProductionOutputCapabilityIds.StandardDefinition,
            ProductionOutputCapabilityIds.StandardDefinitionVersion,
            ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                "output:qa-detached-capacity",
                "item:qa-detached-capacity",
                ProductionOutputCapabilityIds.StandardDefinition,
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion));
        return new ProductionOutputBatchMaximumMassProof(new[]
        {
            new ProductionOutputMaximumMassProjection(
                descriptor,
                maximumQuantity: 1,
                definitionUnitMassGrams: 1_000L,
                maximumMassGrams: 1_000L,
                massAuthorityRevision: 1L,
                sourceDigest: new string('c', 64))
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void ExpectMessage<T>(Action action, string token)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T exception)
        {
            Require(exception.Message.Contains(
                    token,
                    StringComparison.OrdinalIgnoreCase),
                "Failure did not include expected token '" + token
                + "': " + exception.Message);
            return;
        }
        throw new InvalidOperationException(
            "Expected " + typeof(T).Name + " containing '" + token + "'.");
    }

    private sealed class FixedWorldCandidates : IRestoreWorldCandidateQuery
    {
        private readonly IReadOnlyList<BuildableObject> buildings;

        internal FixedWorldCandidates(IReadOnlyList<BuildableObject> buildings) =>
            this.buildings = buildings;

        public int Revision => 1;
        public bool TryGetGrid(out Grid grid) { grid = null; return false; }
        public bool TryGetBuildings(out IReadOnlyList<BuildableObject> values)
        { values = buildings; return values != null; }
        public bool TryGetCharacters(out IReadOnlyList<CharacterActor> values)
        { values = null; return false; }
        public bool TryGetWildlife(out IReadOnlyList<WildlifeActor> values)
        { values = null; return false; }
        public bool TryGetExteriorZones(out IReadOnlyList<ExteriorZoneMarker> values)
        { values = null; return false; }
    }

    private sealed class FixedFacilityHandles : IProductionFacilityHandleQuery
    {
        private readonly object expected;
        private readonly ProductionFacilityHandle handle;

        internal FixedFacilityHandles(
            object expected,
            ProductionFacilityHandle handle)
        {
            this.expected = expected;
            this.handle = handle;
        }

        public ProductionFacilityHandle CaptureFacility(object runtimeObject)
        {
            if (!ReferenceEquals(runtimeObject, expected))
                throw new InvalidOperationException("Unexpected facility candidate.");
            return handle;
        }
    }

    private sealed class FixedCapacityProjector :
        IProductionOutputBufferCapacityProjector
    {
        private readonly ProductionOutputBufferCapacitySourceSnapshot source;

        internal FixedCapacityProjector(
            ProductionOutputBufferCapacitySourceSnapshot source) =>
            this.source = source;

        public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
            ProductionFacilityHandle facility,
            ProductionOutputBatchMaximumMassProof maximumMassProof) => source;
    }

    private sealed class FixedMaximumMassRegistry :
        IProductionOutputMaximumMassRegistry
    {
        private readonly ProductionOutputMaximumMassProjection projection;

        internal FixedMaximumMassRegistry(
            ProductionOutputMaximumMassProjection projection) =>
            this.projection = projection;

        public IReadOnlyList<string> CapabilityIds =>
            Array.Empty<string>();
        public IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
            CapabilityContracts =>
                Array.Empty<ProductionOutputCapabilityContractSnapshot>();
        public string RegistryFingerprint => new string('d', 64);
        public ProductionOutputMaximumMassProjection CaptureAutomatic(
            string outputLineId,
            string itemId,
            int maximumQuantity) =>
            throw new NotSupportedException();
        public ProductionOutputMaximumMassProjection CaptureDeclared(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity)
        {
            if (!string.Equals(
                    descriptor.Fingerprint,
                    projection.Descriptor.Fingerprint,
                    StringComparison.Ordinal)
                || maximumQuantity != projection.MaximumQuantity)
            {
                throw new InvalidOperationException(
                    "Unexpected Apparel maximum-mass projection request.");
            }
            return projection;
        }
    }

    private sealed class RecordingDetachedCapacityGuard :
        IProductionOutputDetachedFacilityCapacityRestoreGuard
    {
        public int CallCount { get; private set; }
        public string LastOwnerStableId { get; private set; }

        public ProductionOutputBufferCapacitySourceSnapshot Validate(
            string ownerStableId,
            string facilityInstanceId,
            ProductionOutputBatchMaximumMassProof maximumMassProof,
            string savedCapacitySourceDigest,
            long savedRequiredMinimumCapacityGrams)
        {
            CallCount++;
            LastOwnerStableId = ownerStableId;
            return new ProductionOutputBufferCapacitySourceSnapshot(
                cycleCapacity: 4,
                maximumBatchMassGrams:
                    maximumMassProof.MaximumBatchMassGrams,
                projectedPortfolioCapacityGrams: 4_000L,
                batchMinimumCapacityGrams: 4_000L,
                requiredMinimumCapacityGrams:
                    savedRequiredMinimumCapacityGrams,
                sourceDigest: savedCapacitySourceDigest);
        }
    }
}

internal static class ApparelDetachedCapacityQaClone
{
    internal static ApparelWorkOrderSaveData CloneForDetachedCapacityQa(
        this ApparelWorkOrderSaveData value) => new()
    {
        orderId = value.orderId,
        kind = value.kind,
        facilityInstanceId = value.facilityInstanceId,
        craftOutputCapability = value.craftOutputCapability.Clone(),
        craftMaximumMassProofDigest = value.craftMaximumMassProofDigest,
        craftMaximumBatchMassGrams = value.craftMaximumBatchMassGrams,
        craftCapacitySourceDigest = value.craftCapacitySourceDigest,
        craftRequiredMinimumCapacityGrams =
            value.craftRequiredMinimumCapacityGrams
    };
}
#endif
