#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused physical transaction tests for quality-rejected apparel dismantle.
/// </summary>
public static class ApparelRejectedDismantlePhysicalTransactionDebugScenarios
{
    private const string SourceItemId = "item:qa:rejected-apparel";
    private const string RecoveryItemId = "item:qa:recovered-textile";
    private const string FillerItemId = "item:qa:recovery-buffer-filler";
    private const string ApparelDefinitionId = "apparel:qa:rejected-shirt";
    private const string MaterialDefinitionId = "textile:qa:recovery-cloth";
    private const string FacilityId = "building:qa:apparel-dismantle";
    private const string FacilityDefinitionId =
        "building:qa:apparel-dismantle-definition";
    private const string WorkstationTag = "workstation:qa:apparel-dismantle";
    private const int ExpectedRecoveryQuantity = 3;
    private static readonly Vector2Int FacilityPosition = new(9, 6);

    [MenuItem(
        "DungeonStory/Debug/Infrastructure/Run Apparel Rejected Planned Transaction Focused")]
    public static void RunAll()
    {
        VerifyCapacityFailurePreservesRejectedGarment();
        VerifyExactPendingPublicationAndAcknowledgements();
        VerifyOwnerRestoreReplayIsIdempotent();
        VerifyMaximumMassProofDriftRejectsBeforeMutation();
        VerifyPartialPublicationRetryKeepsFrozenOutcome();
        VerifyNoRawSpawnOrDeleteRollback();
        Debug.Log(
            "[V27 Rejected Apparel Planned Transaction] PASS: capacity/source, "
            + "pending/publication/ack, restore replay, atomic retry, raw mutation.");
    }

    private static void VerifyCapacityFailurePreservesRejectedGarment()
    {
        using Fixture fixture = new("capacity-full");
        ApparelWorkOrderSaveData order = fixture.CreateOrder();
        string sourceStackId = order.rejectedOutputStackId;
        fixture.FillRecoveryCapacity();
        int sourceBefore = fixture.Repository.GetEditorTestQuantity(sourceStackId);
        int pendingBefore = fixture.Repository.GetEditorPendingBatchDispositionCount();
        int reservedBefore = fixture.Reservations.GetReservedQuantity(
            new ItemStackId(sourceStackId));
        Require(reservedBefore == 1,
            "Capacity fixture did not begin with the exact garment lease.");

        ApparelPhysicalTransactionResult result = fixture.Execute(order);

        Require(
            result.Status == ApparelPhysicalTransactionStatus.WaitingForOutputSpace,
            "Full recovery gram capacity did not wait before input debit: "
            + result.FailureReason);
        Require(
            fixture.Repository.GetEditorTestQuantity(sourceStackId) == sourceBefore
            && fixture.Repository.GetEditorPendingBatchDispositionCount()
                == pendingBefore
            && fixture.Reservations.GetReservedQuantity(
                new ItemStackId(sourceStackId)) == reservedBefore
            && !order.rejectedOutputConsumed
            && string.IsNullOrEmpty(order.rejectedDismantleCommitId),
            "Capacity rejection consumed or leased the rejected garment.");
        Require(
            fixture.RecoveryQuantity == 0,
            "Capacity rejection published a recovery fragment.");
    }

    private static void VerifyExactPendingPublicationAndAcknowledgements()
    {
        using Fixture fixture = new("success");
        ApparelWorkOrderSaveData order = fixture.CreateOrder();
        string sourceStackId = order.rejectedOutputStackId;

        ApparelPhysicalTransactionResult result = fixture.Execute(order);

        Require(result.IsCompleted,
            "Rejected dismantle transaction did not complete: " + result.FailureReason);
        Require(
            fixture.Repository.GetEditorTestQuantity(sourceStackId) == 0
            && order.rejectedOutputConsumed
            && order.rejectedDismantleInputMassGrams == 2_000L,
            "Rejected garment did not enter one exact pending Transfer.");
        Require(
            order.rejectedRecoveryPublished
            && fixture.RecoveryQuantity == ExpectedRecoveryQuantity
            && fixture.RecoveryStacks.Count == 2
            && fixture.RecoveryMassGrams == 1_500L,
            "Recovery full batch was not atomically split, counted, and weighed.");
        Require(
            order.rejectedRecoveryMaximumMassProofDigest?.Length == 64
            && order.rejectedRecoveryMaximumBatchMassGrams == 1_500L
            && order.rejectedRecoveryRequiredMinimumCapacityGrams == 6_000L,
            "Rejected recovery did not freeze the declared batch maximum and four-cycle capacity proof.");
        Require(
            order.rejectedDismantleAcknowledged
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && order.rejectedRecoveryOutputAcknowledged,
            "Input/output terminal acknowledgements did not both close.");
        Require(
            fixture.RecoveryStacks.All(value =>
                value.State == WorldItemStackState.FacilityOutputBuffer
                && value.DestinationId == fixture.DestinationId),
            "Recovery escaped the admitted FacilityBuffer destination.");
    }

    private static void VerifyOwnerRestoreReplayIsIdempotent()
    {
        using Fixture fixture = new("restore-replay");
        ApparelWorkOrderSaveData live = fixture.CreateOrder();
        ApparelPhysicalTransactionResult first = fixture.Execute(live);
        Require(first.IsCompleted,
            "Restore replay fixture initial transaction failed: " + first.FailureReason);
        ApparelWorkOrderSaveData restored = JsonUtility.FromJson<ApparelWorkOrderSaveData>(
            JsonUtility.ToJson(live));
        int recoveryBefore = fixture.RecoveryQuantity;
        long recoveryMassBefore = fixture.RecoveryMassGrams;
        int pendingBefore = fixture.Repository.GetEditorPendingBatchDispositionCount();

        ApparelPhysicalTransactionResult replay = fixture.Execute(restored);

        Require(replay.IsCompleted,
            "Restored rejected transaction did not replay-join: " + replay.FailureReason);
        Require(
            fixture.RecoveryQuantity == recoveryBefore
            && fixture.RecoveryMassGrams == recoveryMassBefore
            && fixture.Repository.GetEditorPendingBatchDispositionCount()
                == pendingBefore,
            "Restore replay re-debited input or duplicated recovery output.");
        Require(
            restored.rejectedDismantleCommitId == live.rejectedDismantleCommitId
            && restored.rejectedRecoveryCommitId == live.rejectedRecoveryCommitId
            && restored.rejectedRecoveryMaximumMassProofDigest
                == live.rejectedRecoveryMaximumMassProofDigest
            && restored.rejectedRecoveryMaximumBatchMassGrams
                == live.rejectedRecoveryMaximumBatchMassGrams,
            "Restore replay changed frozen input/output commit identity.");
    }

    private static void VerifyMaximumMassProofDriftRejectsBeforeMutation()
    {
        using Fixture fixture = new("maximum-proof-drift");
        ApparelWorkOrderSaveData order = fixture.CreateOrder();
        ApparelPhysicalTransactionResult first = fixture.Execute(order);
        Require(first.IsCompleted, "Rejected maximum-proof fixture did not complete.");
        int quantityBefore = fixture.RecoveryQuantity;
        long massBefore = fixture.RecoveryMassGrams;

        order.rejectedRecoveryMaximumMassProofDigest = new string('0', 64);
        ApparelPhysicalTransactionResult replay = fixture.Execute(order);

        Require(
            replay.Status == ApparelPhysicalTransactionStatus.Conflict
            && replay.FailureReason.Contains(
                "maximum-mass-proof-drift",
                StringComparison.Ordinal),
            "Rejected-recovery maximum-mass proof drift was not rejected.");
        Require(
            fixture.RecoveryQuantity == quantityBefore
            && fixture.RecoveryMassGrams == massBefore,
            "Rejected-recovery proof drift mutated physical output.");
    }

    private static void VerifyPartialPublicationRetryKeepsFrozenOutcome()
    {
        FailOnceAtSecondStack fault = new();
        using Fixture fixture = new("publish-retry", fault);
        ApparelWorkOrderSaveData order = fixture.CreateOrder();

        ApparelPhysicalTransactionResult interrupted = fixture.Execute(order);
        Require(
            interrupted.Status == ApparelPhysicalTransactionStatus.PendingFinalization,
            "Injected partial publication did not stop in pending finalization: "
            + interrupted.FailureReason);
        Require(
            fixture.RecoveryQuantity == 0
            && order.rejectedOutputConsumed
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Failed full-batch publication leaked recovery or rolled back input WIP.");
        string inputOperationId = ApparelRejectedDismantleOutbox.FormatOperationId(
            order.orderId,
            order.qualityAttemptIndex);
        Require(
            fixture.Dispositions.TryGetPending(
                inputOperationId,
                out PhysicalItemBatchDispositionReceipt pending)
            && pending.Kind == PhysicalItemDispositionKind.Transfer
            && pending.Quantity == 1
            && pending.InputMassGrams == 2_000L
            && pending.SourceStackIds.Count == 1,
            "Interrupted recovery did not retain one exact pending Transfer receipt.");
        string frozenOutcome = order.rejectedRecoveryOutcomeFingerprint;
        string frozenItem = order.rejectedRecoveryItemId;
        int frozenQuantity = order.rejectedMaterialAmount;
        Require(
            !string.IsNullOrEmpty(frozenOutcome)
            && frozenItem == RecoveryItemId
            && frozenQuantity == ExpectedRecoveryQuantity,
            "Interrupted recovery did not retain its frozen output authority.");

        ApparelPhysicalTransactionResult retry = fixture.Execute(order);

        Require(retry.IsCompleted,
            "Recovery retry did not complete: " + retry.FailureReason);
        Require(
            fixture.RecoveryQuantity == ExpectedRecoveryQuantity
            && order.rejectedRecoveryItemId == frozenItem
            && order.rejectedMaterialAmount == frozenQuantity
            && order.rejectedRecoveryOutcomeFingerprint == frozenOutcome,
            "Publication retry rerolled or changed the frozen recovery outcome.");
        Require(
            fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && order.rejectedDismantleAcknowledged,
            "Recovery retry did not close the retained input pending receipt.");
    }

    private static void VerifyNoRawSpawnOrDeleteRollback()
    {
        string path = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "Scripts/Services/Infrastructure/Environment/ApparelPhysicalTransaction.cs"));
        string source = File.ReadAllText(path);
        Require(
            !source.Contains("SpawnItemAt(", StringComparison.Ordinal)
            && !source.Contains("SpawnItemAtWithComponents(", StringComparison.Ordinal)
            && !source.Contains("SpawnUniqueItemAt(", StringComparison.Ordinal)
            && !source.Contains("DeleteStack(", StringComparison.Ordinal),
            "ApparelPhysicalTransaction retained raw spawn/delete rollback.");
    }

    private static ItemInstanceComponentSaveData CreateApparelComponent() =>
        ApparelItemStateCodec.Create(new ApparelInstanceState
        {
            apparelDefinitionId = ApparelDefinitionId,
            primaryMaterialId = MaterialDefinitionId,
            craftsmanshipQuality = CraftsmanshipQualityTier.Poor,
            sourceKind = TextileSourceKind.Crop,
            sourceDefinitionId = MaterialDefinitionId,
            size = ApparelSizeClass.Medium,
            modifications = ApparelModificationKind.None,
            durability = 100f,
            craftedAbsoluteDay = 1,
            deterministicBatchHash = 0xD15AUL
        });

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject facilityObject;
        private readonly ProductionFacilityHandle handle;

        internal Fixture(
            string suffix,
            IFacilityBufferPlannedOutputPublicationFaultInjector fault = null)
        {
            Catalog = new FixedCatalog();
            WorldItems = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                Catalog,
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out _,
                out IReservedPhysicalItemBatchDispositionService reservedDispositions,
                out IPhysicalItemBatchDispositionService dispositions);
            Repository = repository;
            Reservations = quantityReservations;
            Dispositions = dispositions;
            FacilityBufferDestinationClaimRegistry claims = new();
            FacilityBufferPhysicalOccupancyQuery occupancy = new(
                repository,
                WorldItems.MassQuery,
                quantityReservations);
            FacilityBufferMassAdmissionService admission = new(
                claims,
                occupancy,
                WorldItems.MassQuery);
            FacilityBufferDestinationLifecycleService lifecycle = new(
                claims,
                claims,
                admission,
                admission);
            ProductionOutputDestinationAuthorityRuntime destinations = new(
                claims,
                admission,
                claims,
                admission,
                lifecycle);
            Publication = new FacilityBufferPlannedOutputPublicationService(
                repository,
                Catalog,
                WorldItems.MassQuery,
                admission,
                fault);
            facilityObject = new GameObject(
                "Rejected Apparel Planned Transaction " + suffix)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Facility = facilityObject.AddComponent<BuildableObject>();
            handle = new ProductionFacilityHandle(
                Facility,
                (BuildingInstanceId)FacilityId,
                FacilityPosition,
                isDestroyed: false,
                stockSensorInstallationItemId: string.Empty,
                allowsOverflowDump: false,
                overflowOffset: default,
                definitionId: FacilityDefinitionId,
                workstationTag: WorkstationTag,
                outputBufferCycleCapacity: 4,
                workstationLaneProfile:
                    ProductionFacilityWorkstationLaneCapacityProfile
                        .SingleManualWithDetachedBatchProcessors);
            ApparelFixtureStandardOutputHandler standardCapability = new();
            FixedApparelOutputCapability apparelCapability = new();
            ProductionOutputMaximumMassRegistry maximumMassRegistry = new(
                new IProductionOutputMaximumMassCapability[]
                {
                    standardCapability,
                    apparelCapability
                },
                WorldItems.MassQuery);
            MaximumMassRegistry = maximumMassRegistry;
            ProductionOutputBufferCapacityProjector capacity = new(
                new EmptyEconomyCatalog(),
                new ProductionMaximumOutputFactorCatalog(Array.Empty<BuildingSO>()),
                new ProductionPreparedOutputComponentCodec(),
                WorldItems.MassQuery,
                _ => handle.OutputBufferCycleCapacity,
                (_, recipe) => string.Equals(
                    recipe?.WorkstationTag,
                    handle.WorkstationTag,
                    StringComparison.Ordinal),
                maximumMassRegistry.CaptureAutomatic,
                maximumMassRegistry.CaptureDeclared);
            CapacityProjector = capacity;
            Destinations = destinations;
            Transaction = new ApparelPhysicalTransaction(
                WorldItems,
                dispositions,
                reservedDispositions,
                quantityReservations,
                new StaticFacilityHandleQuery(handle),
                destinations,
                capacity,
                admission,
                Publication,
                repository,
                new ProductionOutputHandlerRegistry(new IProductionOutputCapability[]
                {
                    standardCapability,
                    apparelCapability
                }),
                maximumMassRegistry);
        }

        internal FixedCatalog Catalog { get; }
        internal WorldItemStackRuntime WorldItems { get; }
        internal WorldItemRepository Repository { get; }
        internal ItemQuantityReservationService Reservations { get; }
        internal IPhysicalItemBatchDispositionService Dispositions { get; }
        internal BuildableObject Facility { get; }
        internal ApparelPhysicalTransaction Transaction { get; }
        internal ProductionOutputBufferCapacityProjector CapacityProjector { get; }
        internal ProductionOutputMaximumMassRegistry MaximumMassRegistry { get; }
        internal ProductionOutputDestinationAuthorityRuntime Destinations { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication { get; }
        internal string DestinationId => ProductionBillRuntime.OutputDestinationPrefix
            + FacilityId;
        internal IReadOnlyList<WorldItemStackSnapshot> RecoveryStacks => WorldItems
            .GetAllStacks()
            .Where(value => value != null
                && value.ItemId == RecoveryItemId
                && value.State == WorldItemStackState.FacilityOutputBuffer)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        internal int RecoveryQuantity => RecoveryStacks.Sum(value => value.Quantity);
        internal long RecoveryMassGrams => RecoveryStacks.Sum(value =>
            WorldItems.MassQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)value.ItemId).Multiply(value.Quantity).Value);

        internal ApparelPhysicalTransactionResult Execute(
            ApparelWorkOrderSaveData order) =>
            Transaction.ExecuteRejectedDismantleOrResume(
                order,
                Facility,
                RecoveryItemId);

        internal ApparelWorkOrderSaveData CreateOrder()
        {
            string orderId = "apparel-order:qa:rejected:"
                + Guid.NewGuid().ToString("N");
            string instanceId = ((IItemInstanceRepository)Repository)
                .AllocateItemInstanceId().Value;
            ItemInstanceComponentSaveData apparel = CreateApparelComponent();
            string stackId = Repository.AddEditorTestStack(
                SourceItemId,
                1,
                WorldItemStackState.FacilityOutputBuffer,
                destinationId: DestinationId,
                components: new[] { apparel },
                position: FacilityPosition,
                itemInstanceId: instanceId);
            string operationId = ApparelRejectedDismantleOutbox.FormatOperationId(
                orderId,
                0);
            Require(
                Reservations.TryReserve(
                    operationId,
                    string.Empty,
                    ItemReservationPurpose.ProductionInput,
                    "apparel-rejected-dismantle",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(stackId),
                        1,
                        ItemStackSignature.Create(
                            SourceItemId,
                            new[] { apparel })),
                    out ItemQuantityLease lease,
                    out DomainFailure reserveFailure),
                "Rejected garment exact lease failed: " + reserveFailure);
            return new ApparelWorkOrderSaveData
            {
                orderId = orderId,
                kind = ApparelWorkOrderKind.Craft,
                state = ApparelWorkOrderState.InProgress,
                apparelDefinitionId = ApparelDefinitionId,
                materialDefinitionId = MaterialDefinitionId,
                qualityAttemptIndex = 0,
                facilityInstanceId = FacilityId,
                dismantlingRejectedOutput = true,
                rejectedOutputStackId = stackId,
                rejectedOutputInstanceId = instanceId,
                rejectedOutputLeaseId = lease.leaseId,
                rejectedMaterialAmount = ExpectedRecoveryQuantity,
                rejectedRecoveryItemId = RecoveryItemId
            };
        }

        internal void FillRecoveryCapacity()
        {
            ProductionOutputBatchMaximumMassProof maximumMassProof = new(
                new[]
                {
                    MaximumMassRegistry.CaptureAutomatic(
                        "output:apparel-rejected-recovery",
                        RecoveryItemId,
                        ExpectedRecoveryQuantity)
                });
            ProductionOutputBufferCapacitySourceSnapshot source =
                CapacityProjector.CaptureSource(handle, maximumMassProof);
            Require(
                Destinations.TryEnsure(
                    handle,
                    source.RequiredMinimumCapacityGrams,
                    out FacilityBufferCapacityProfile profile,
                    out string failure),
                "Recovery fixture could not publish capacity: " + failure);
            int fillerQuantity = checked((int)(profile.MaxMassGrams / 1_000L));
            Require(
                fillerQuantity > 0
                && fillerQuantity * 1_000L == profile.MaxMassGrams,
                "Recovery capacity is not an integral 1kg filler quantity.");
            Repository.AddEditorTestStack(
                FillerItemId,
                fillerQuantity,
                WorldItemStackState.FacilityOutputBuffer,
                destinationId: DestinationId,
                position: FacilityPosition);
        }

        public void Dispose()
        {
            WorldItems?.Dispose();
            if (facilityObject != null)
                UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private sealed class StaticFacilityHandleQuery : IProductionFacilityHandleQuery
    {
        private readonly ProductionFacilityHandle handle;
        internal StaticFacilityHandleQuery(ProductionFacilityHandle handle) =>
            this.handle = handle ?? throw new ArgumentNullException(nameof(handle));
        public ProductionFacilityHandle CaptureFacility(object runtimeObject)
        {
            Require(ReferenceEquals(runtimeObject, handle.RuntimeObject),
                "Fixture received an unexpected facility object.");
            return handle;
        }
    }

    private sealed class FailOnceAtSecondStack :
        IFacilityBufferPlannedOutputPublicationFaultInjector
    {
        private bool failed;
        public bool FailBeforeRepositoryAdd(int zeroBasedStackIndex)
        {
            if (failed || zeroBasedStackIndex != 1)
                return false;
            failed = true;
            return true;
        }
    }

    private sealed class FixedCatalog : IDungeonItemCatalogProvider
    {
        private readonly IReadOnlyList<DungeonItemDefinition> all;
        private readonly Dictionary<string, DungeonItemDefinition> byId;

        internal FixedCatalog()
        {
            all = new[]
            {
                new DungeonItemDefinition(
                    SourceItemId, "Rejected QA Apparel", "Unique garment",
                    StockCategory.General, 10, null, 2f, 1),
                new DungeonItemDefinition(
                    RecoveryItemId, "Recovered QA Textile", "Recovery output",
                    StockCategory.General, 1, null, .5f, 2),
                new DungeonItemDefinition(
                    FillerItemId, "Recovery Filler", "Capacity filler",
                    StockCategory.General, 1, null, 1f, 75)
            };
            byId = all.ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        }

        public IReadOnlyList<DungeonItemDefinition> All => all;
        public DungeonItemDefinition GetDefinition(string itemId) =>
            byId.TryGetValue(itemId ?? string.Empty, out DungeonItemDefinition value)
                ? value
                : null;
        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition definition) =>
            byId.TryGetValue(itemId ?? string.Empty, out definition);
    }

    private sealed class FixedApparelOutputCapability :
        IProductionOutputCapability,
        IProductionOutputMaximumMassCapability
    {
        public string CapabilityId =>
            ProductionOutputCapabilityIds.ApparelWorkOrder;
        public int ContractVersion =>
            ProductionOutputCapabilityIds.ApparelWorkOrderVersion;
        public string ComponentCodecId =>
            ProductionOutputCapabilityIds.ApparelStateCodec;
        public int ComponentCodecVersion =>
            ProductionOutputCapabilityIds.ApparelStateCodecVersion;
        public bool SupportsAutomaticSelection => false;
        public bool CanHandle(string itemId) => string.Equals(
            itemId,
            SourceItemId,
            StringComparison.Ordinal);

        public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity,
            IPhysicalItemMassQuery massQuery) =>
            ProductionOutputDefinitionMaximumMassProjection.Capture(
                this,
                descriptor,
                maximumQuantity,
                massQuery);
    }

    private sealed class EmptyEconomyCatalog : IResourceEconomyContentCatalog
    {
        public IReadOnlyList<ResourceItemDefinitionSO> Items =>
            Array.Empty<ResourceItemDefinitionSO>();
        public IReadOnlyList<ProductionRecipeSO> Recipes =>
            Array.Empty<ProductionRecipeSO>();
        public IReadOnlyList<CropDefinitionSO> Crops => Array.Empty<CropDefinitionSO>();
        public IReadOnlyList<CraftMaterialDefinitionSO> Materials =>
            Array.Empty<CraftMaterialDefinitionSO>();
        public IReadOnlyList<SubstanceDefinitionView> Substances =>
            Array.Empty<SubstanceDefinitionView>();
        public bool TryGetItem(string itemId, out ResourceItemDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetRecipe(string recipeId, out ProductionRecipeSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetCrop(string cropId, out CropDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetMaterial(
            string materialId,
            out CraftMaterialDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetSubstance(
            string substanceId,
            out SubstanceDefinitionView definition)
        {
            definition = null;
            return false;
        }
    }
}
#endif
