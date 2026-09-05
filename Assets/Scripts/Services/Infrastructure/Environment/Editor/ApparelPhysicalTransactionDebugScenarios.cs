#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ApparelPhysicalTransactionDebugScenarios
{
    private const string OutputItemId = "item:qa:apparel-output";
    private const string MaterialItemId = "item:qa:apparel-material";
    private const string FillerItemId = "item:qa:apparel-buffer-filler";
    private const string ApparelDefinitionId = "apparel:qa:work-shirt";
    private const string MaterialDefinitionId = "textile:qa:linen";
    private const string FacilityId = "building:qa:apparel-bench";
    private const string FacilityDefinitionId = "building:qa:apparel-bench-definition";
    private const string WorkstationTag = "workstation:qa:apparel";
    private static readonly Vector2Int FacilityPosition = new(7, 5);

    [MenuItem("Tools/DungeonStory/QA/V27 Apparel Physical Transaction")]
    public static void RunAll()
    {
        VerifyCapacityFailurePreservesMaterialSource();
        VerifySuccessfulUniqueOutputAndTerminalAcknowledgements();
        VerifyPendingPublicationRetryReleasesForNaturalHaul();
        VerifyCompletedReplayIsIdempotent();
        VerifyCapabilityDriftRejectsBeforeMutation();
        VerifyMaximumMassProofDriftRejectsBeforeMutation();
        VerifyRejectedSaleRouteRemainsPhysicalFacilityOutput();
        Debug.Log(
            "[V27 Apparel Physical Transaction] PASS: capacity/source, "
            + "declared capability freeze/drift, unique component/mass, "
            + "pending acknowledgements, replay, market route.");
    }

    private static void VerifyCapacityFailurePreservesMaterialSource()
    {
        using Fixture fixture = new("capacity-full");
        string sourceStackId = fixture.AddMaterialSource(quantity: 4);
        ApparelWorkOrderSaveData order = fixture.CreateOrder(sourceStackId, quantity: 2);
        ItemInstanceComponentSaveData component = CreateApparelComponent();

        fixture.FillOutputCapacity(component);
        int beforeQuantity = fixture.Repository.GetEditorTestQuantity(sourceStackId);
        int beforePending = fixture.Repository.GetEditorPendingBatchDispositionCount();

        ApparelPhysicalTransactionResult result = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                component,
                markForSale: false);

        Require(
            result.Status == ApparelPhysicalTransactionStatus.WaitingForOutputSpace,
            "A full output buffer did not produce WaitingForOutputSpace: "
            + result.FailureReason);
        Require(
            fixture.Repository.GetEditorTestQuantity(sourceStackId) == beforeQuantity,
            "Capacity rejection debited the material source.");
        Require(
            fixture.Repository.GetEditorPendingBatchDispositionCount() == beforePending
            && !order.craftInputPending
            && string.IsNullOrEmpty(order.craftInputCommitId),
            "Capacity rejection created a pending material disposition.");
        Require(
            fixture.OutputStacks.Count == 0,
            "Capacity rejection published a partial apparel output.");
    }

    private static void VerifySuccessfulUniqueOutputAndTerminalAcknowledgements()
    {
        using Fixture fixture = new("success");
        string sourceStackId = fixture.AddMaterialSource(quantity: 4);
        ApparelWorkOrderSaveData order = fixture.CreateOrder(sourceStackId, quantity: 2);
        ItemInstanceComponentSaveData component = CreateApparelComponent();

        ApparelPhysicalTransactionResult result = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                component,
                markForSale: false);

        Require(result.IsCompleted, "Successful craft did not complete: " + result.FailureReason);
        WorldItemStackSnapshot output = fixture.OutputStacks.Single();
        Require(
            output.Quantity == 1
            && output.State == WorldItemStackState.Loose
            && string.IsNullOrEmpty(output.DestinationId)
            && output.ItemInstanceId.Length > 0
            && output.ItemInstanceId == result.OutputInstanceId
            && output.StackId == result.OutputStackId,
            "Craft output was not one exact unique physical instance.");
        Require(
            ApparelItemStateCodec.TryRead(
                output.Components,
                out ApparelInstanceState decoded)
            && decoded.apparelDefinitionId == ApparelDefinitionId
            && decoded.primaryMaterialId == MaterialDefinitionId
            && component.ToCanonicalString()
                == output.Components.Single(value => value != null
                    && value.componentTypeId == ItemInstanceComponentIds.Apparel)
                    .ToCanonicalString(),
            "Craft output lost or changed its frozen Apparel component.");

        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            fixture.WorldItems.MassQuery,
            (ItemDefinitionId)OutputItemId,
            output.ItemInstanceId,
            output.Components);
        long measuredMass = fixture.WorldItems.MassQuery.GetQuantityMass(
            (ItemDefinitionId)OutputItemId,
            subject,
            output.Quantity).Value;
        Require(
            measuredMass == result.OutputMassGrams
            && measuredMass == order.craftOutputMassGrams
            && measuredMass == 2_000L,
            "Unique apparel output mass disagreed with the physical mass authority.");
        Require(
            order.craftMaximumMassProofDigest?.Length == 64
            && order.craftMaximumBatchMassGrams == 4_000L
            && order.craftRequiredMinimumCapacityGrams == 16_000L,
            "Apparel craft did not freeze the declared one-batch maximum and four-cycle capacity proof.");
        Require(
            fixture.Repository.GetEditorTestQuantity(sourceStackId) == 2,
            "Successful craft did not debit the exact material quantity.");
        Require(
            order.craftInputPending
            && order.craftInputAcknowledged
            && order.craftOutputPublished
            && order.craftAdmissionCommitted
            && order.craftOutputAcknowledged
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "Successful craft did not close material/output pending receipts.");
        RequireFrozenCapability(order);
        RequireNoRawSpawnOrDeleteRollback();
    }

    private static void VerifyCapabilityDriftRejectsBeforeMutation()
    {
        using Fixture fixture = new("capability-drift");
        string sourceStackId = fixture.AddMaterialSource(quantity: 4);
        ApparelWorkOrderSaveData order = fixture.CreateOrder(sourceStackId, quantity: 2);
        ItemInstanceComponentSaveData component = CreateApparelComponent();
        ApparelPhysicalTransactionResult first = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                component,
                markForSale: false);
        Require(first.IsCompleted, "Capability drift fixture did not complete.");

        int materialBefore = fixture.Repository.GetEditorTestQuantity(sourceStackId);
        string outputStackId = first.OutputStackId;
        order.craftOutputCapability.capabilityVersion++;
        ApparelPhysicalTransactionResult replay = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                component,
                markForSale: false);

        Require(
            replay.Status == ApparelPhysicalTransactionStatus.Conflict
            && replay.FailureReason.Contains(
                "output-capability",
                StringComparison.Ordinal),
            "Capability version drift was not rejected before replay.");
        Require(
            fixture.Repository.GetEditorTestQuantity(sourceStackId) == materialBefore
            && fixture.OutputStacks.Single().StackId == outputStackId,
            "Capability drift mutated physical input or output authority.");
    }

    private static void VerifyPendingPublicationRetryReleasesForNaturalHaul()
    {
        OneShotAcknowledgementFault fault = new();
        using Fixture fixture = new("pending-release-retry", fault);
        string sourceStackId = fixture.AddMaterialSource(quantity: 4);
        ApparelWorkOrderSaveData order = fixture.CreateOrder(sourceStackId, quantity: 2);
        ItemInstanceComponentSaveData component = CreateApparelComponent();

        ApparelPhysicalTransactionResult first = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                component,
                markForSale: false);
        Require(
            first.Status == ApparelPhysicalTransactionStatus.PendingFinalization
            && order.craftOutputPublished
            && order.craftAdmissionCommitted
            && !order.craftOutputAcknowledged
            && fixture.OutputStacks.Single().State
                == WorldItemStackState.FacilityOutputBuffer,
            "Injected acknowledgement interruption did not preserve the exact pending output batch.");

        ApparelPhysicalTransactionResult retry = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                component,
                markForSale: false);
        WorldItemStackSnapshot released = fixture.OutputStacks.Single();
        Require(
            retry.IsCompleted
            && order.craftOutputAcknowledged
            && released.StackId == retry.OutputStackId
            && released.State == WorldItemStackState.Loose
            && string.IsNullOrEmpty(released.DestinationId)
            && fixture.Repository.GetEditorTestQuantity(sourceStackId) == 2,
            "Pending publication retry did not atomically release the original output for natural haul.");
    }

    private static void VerifyMaximumMassProofDriftRejectsBeforeMutation()
    {
        using Fixture fixture = new("maximum-proof-drift");
        string sourceStackId = fixture.AddMaterialSource(quantity: 4);
        ApparelWorkOrderSaveData order = fixture.CreateOrder(sourceStackId, quantity: 2);
        ItemInstanceComponentSaveData component = CreateApparelComponent();
        ApparelPhysicalTransactionResult first = fixture.Transaction.ExecuteCraftOrResume(
            order,
            fixture.Facility,
            OutputItemId,
            component,
            markForSale: false);
        Require(first.IsCompleted, "Maximum-proof drift fixture did not complete.");

        int materialBefore = fixture.Repository.GetEditorTestQuantity(sourceStackId);
        string outputStackId = first.OutputStackId;
        order.craftMaximumMassProofDigest = new string('0', 64);
        ApparelPhysicalTransactionResult replay = fixture.Transaction.ExecuteCraftOrResume(
            order,
            fixture.Facility,
            OutputItemId,
            component,
            markForSale: false);

        Require(
            replay.Status == ApparelPhysicalTransactionStatus.Conflict
            && replay.FailureReason.Contains("frozen-output-drift", StringComparison.Ordinal),
            "Apparel maximum-mass proof drift was not rejected before replay.");
        Require(
            fixture.Repository.GetEditorTestQuantity(sourceStackId) == materialBefore
            && fixture.OutputStacks.Single().StackId == outputStackId,
            "Apparel maximum-mass proof drift mutated physical authority.");
    }

    private static void VerifyCompletedReplayIsIdempotent()
    {
        using Fixture fixture = new("replay");
        string sourceStackId = fixture.AddMaterialSource(quantity: 5);
        ApparelWorkOrderSaveData order = fixture.CreateOrder(sourceStackId, quantity: 2);
        ItemInstanceComponentSaveData component = CreateApparelComponent();
        ApparelPhysicalTransactionResult first = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                component,
                markForSale: false);
        Require(first.IsCompleted, "Replay fixture initial craft failed: " + first.FailureReason);

        int materialAfterFirst = fixture.Repository.GetEditorTestQuantity(sourceStackId);
        int outputCountAfterFirst = fixture.OutputStacks.Count;
        int pendingAfterFirst = fixture.Repository.GetEditorPendingBatchDispositionCount();
        ApparelPhysicalTransactionResult replay = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                component,
                markForSale: false);

        Require(replay.IsCompleted, "Completed craft replay did not join: " + replay.FailureReason);
        Require(
            replay.OutputStackId == first.OutputStackId
            && replay.OutputInstanceId == first.OutputInstanceId,
            "Completed replay changed exact output identity.");
        Require(
            fixture.Repository.GetEditorTestQuantity(sourceStackId) == materialAfterFirst
            && fixture.OutputStacks.Count == outputCountAfterFirst
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == pendingAfterFirst,
            "Completed replay duplicated output or debited materials twice.");
    }

    private static void VerifyRejectedSaleRouteRemainsPhysicalFacilityOutput()
    {
        using Fixture fixture = new("market-route");
        string sourceStackId = fixture.AddMaterialSource(quantity: 3);
        ApparelWorkOrderSaveData order = fixture.CreateOrder(sourceStackId, quantity: 1);

        ApparelPhysicalTransactionResult result = fixture.Transaction
            .ExecuteCraftOrResume(
                order,
                fixture.Facility,
                OutputItemId,
                CreateApparelComponent(),
                markForSale: true);

        Require(result.IsCompleted, "Mark-for-sale craft did not complete: " + result.FailureReason);
        WorldItemStackSnapshot output = fixture.OutputStacks.Single();
        Require(
            output.State == WorldItemStackState.FacilityOutputBuffer
            && output.DestinationId == QualityRejectedOutputRules.MarketDestinationId
            && order.craftMarketRouted,
            "Mark-for-sale output did not remain physical in FacilityBuffer with market destination.");
        Require(
            ApparelItemStateCodec.TryRead(output.Components, out _),
            "Mark-for-sale routing stripped the Apparel component.");
    }

    private static ItemInstanceComponentSaveData CreateApparelComponent() =>
        ApparelItemStateCodec.Create(new ApparelInstanceState
        {
            apparelDefinitionId = ApparelDefinitionId,
            primaryMaterialId = MaterialDefinitionId,
            craftsmanshipQuality = CraftsmanshipQualityTier.Normal,
            sourceKind = TextileSourceKind.Crop,
            sourceDefinitionId = MaterialDefinitionId,
            size = ApparelSizeClass.Medium,
            modifications = ApparelModificationKind.None,
            durability = 100f,
            craftedAbsoluteDay = 1,
            deterministicBatchHash = 0xA771UL
        });

    private static void RequireFrozenCapability(ApparelWorkOrderSaveData order)
    {
        ProductionOutputCapabilitySaveData capability =
            order?.craftOutputCapability;
        Require(
            capability != null
            && !capability.IsEmpty
            && capability.outputLineId == ApparelPhysicalTransaction.OutputLineId
            && capability.itemId == OutputItemId
            && capability.capabilityId
                == ProductionOutputCapabilityIds.ApparelWorkOrder
            && capability.capabilityVersion
                == ProductionOutputCapabilityIds.ApparelWorkOrderVersion
            && capability.componentCodecId
                == ProductionOutputCapabilityIds.ApparelStateCodec
            && capability.componentCodecVersion
                == ProductionOutputCapabilityIds.ApparelStateCodecVersion
            && capability.fingerprint
                == ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    capability.outputLineId,
                    capability.itemId,
                    capability.capabilityId,
                    capability.capabilityVersion,
                    capability.componentCodecId,
                    capability.componentCodecVersion),
            "Apparel output did not freeze the exact declared capability.");
    }

    private static void RequireNoRawSpawnOrDeleteRollback()
    {
        string path = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "Scripts/Services/Infrastructure/Environment/ApparelPhysicalTransaction.cs"));
        string source = File.ReadAllText(path);
        Require(
            !source.Contains("SpawnUniqueItemAt(", StringComparison.Ordinal)
            && !source.Contains("DeleteStack(", StringComparison.Ordinal),
            "Apparel physical transaction contains raw spawn/delete rollback.");
    }

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
            IFacilityBufferPlannedOutputAcknowledgementFaultInjector
                acknowledgementFaultInjector = null)
        {
            Catalog = new FixedCatalog();
            WorldItems = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                Catalog,
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out _,
                out _,
                out IPhysicalItemBatchDispositionService dispositions);
            Repository = repository;

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
            FacilityBufferPlannedOutputPublicationService publication = new(
                repository,
                Catalog,
                WorldItems.MassQuery,
                admission,
                acknowledgementFaultInjector: acknowledgementFaultInjector);

            facilityObject = new GameObject("Apparel Physical Transaction " + suffix)
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
            StaticFacilityHandleQuery handles = new(handle);
            ApparelFixtureStandardOutputHandler standardCapability = new();
            FixedApparelOutputCapability apparelCapability = new();
            ProductionOutputMaximumMassRegistry maximumMassRegistry = new(
                new IProductionOutputMaximumMassCapability[]
                {
                    standardCapability,
                    apparelCapability
                },
                WorldItems.MassQuery);
            ProductionOutputHandlerRegistry outputCapabilityRegistry = new(
                new IProductionOutputCapability[]
                {
                    standardCapability,
                    apparelCapability
                });
            ProductionFacilityOutputCapacityContributorRegistry contributors = new(
                new IProductionFacilityOutputCapacityContributor[]
                {
                    new ApparelFixtureFacilityOutputCapacityContributor()
                },
                maximumMassRegistry);
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
                maximumMassRegistry.CaptureDeclared,
                contributors,
                clearanceProfiles:
                    new ProductionOutputClearanceNaturalBootstrapProfileSource());
            CapacityProjector = capacity;
            Destinations = destinations;
            MaximumMassRegistry = maximumMassRegistry;
            ApparelDescriptor = outputCapabilityRegistry.CaptureDeclaredDescriptor(
                ApparelPhysicalTransaction.OutputLineId,
                OutputItemId,
                ProductionOutputCapabilityIds.ApparelWorkOrder);
            Transaction = new ApparelPhysicalTransaction(
                WorldItems,
                dispositions,
                (IReservedPhysicalItemBatchDispositionService)dispositions,
                quantityReservations,
                handles,
                destinations,
                capacity,
                admission,
                publication,
                repository,
                outputCapabilityRegistry,
                maximumMassRegistry);
        }

        internal FixedCatalog Catalog { get; }
        internal WorldItemStackRuntime WorldItems { get; }
        internal WorldItemRepository Repository { get; }
        internal BuildableObject Facility { get; }
        internal ApparelPhysicalTransaction Transaction { get; }
        internal ProductionOutputBufferCapacityProjector CapacityProjector { get; }
        internal ProductionOutputDestinationAuthorityRuntime Destinations { get; }
        internal ProductionOutputMaximumMassRegistry MaximumMassRegistry { get; }
        internal ProductionOutputCapabilityDescriptor ApparelDescriptor { get; }
        internal string DestinationId => ProductionBillRuntime.OutputDestinationPrefix + FacilityId;
        internal IReadOnlyList<WorldItemStackSnapshot> OutputStacks => WorldItems
            .GetAllStacks()
            .Where(value => value != null
                && value.ItemId == OutputItemId)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();

        internal string AddMaterialSource(int quantity) => Repository.AddEditorTestStack(
            MaterialItemId,
            quantity,
            WorldItemStackState.Loose,
            position: new Vector2Int(2, 2));

        internal ApparelWorkOrderSaveData CreateOrder(
            string sourceStackId,
            int quantity) => new()
        {
            orderId = "apparel-order:qa:" + Guid.NewGuid().ToString("N"),
            kind = ApparelWorkOrderKind.Craft,
            state = ApparelWorkOrderState.InProgress,
            apparelDefinitionId = ApparelDefinitionId,
            materialDefinitionId = MaterialDefinitionId,
            qualityAttemptIndex = 0,
            facilityInstanceId = FacilityId,
            materialStackIds = new List<string> { sourceStackId },
            materialStackAmounts = new List<int> { quantity }
        };

        internal void FillOutputCapacity(ItemInstanceComponentSaveData component)
        {
            string instanceId = ((IItemInstanceRepository)Repository)
                .AllocateItemInstanceId().Value;
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                WorldItems.MassQuery,
                (ItemDefinitionId)OutputItemId,
                instanceId,
                new[] { component });
            _ = subject;
            ProductionOutputBatchMaximumMassProof proof = new(
                new[]
                {
                    MaximumMassRegistry.CaptureDeclared(ApparelDescriptor, 1)
                });
            ProductionOutputBufferCapacitySourceSnapshot source =
                CapacityProjector.CaptureSource(handle, proof);
            Require(
                source.MaximumBatchMassGrams == 4_000L
                && source.RequiredMinimumCapacityGrams == 16_000L
                && IsLowercaseSha256(source.ClearanceProfileDigest)
                && IsLowercaseSha256(source.ClearanceGateDigest)
                && IsLowercaseSha256(source.ClearanceAuthorityDigest),
                "Capacity fixture did not bind its exact maximum and clearance authority.");
            Require(
                Destinations.TryEnsureCapacitySource(
                    handle,
                    source,
                    out FacilityBufferCapacityProfile profile,
                    out string failure),
                "Capacity fixture could not publish authority: " + failure);
            int fillerQuantity = checked((int)(profile.MaxMassGrams / 1_000L));
            Require(
                fillerQuantity > 0
                && fillerQuantity * 1_000L == profile.MaxMassGrams,
                "Capacity fixture expected an integral 1kg filler quantity.");
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

    private sealed class OneShotAcknowledgementFault :
        IFacilityBufferPlannedOutputAcknowledgementFaultInjector
    {
        private bool fired;

        public bool FailBeforeRepositoryMutation(int mutationIndex)
        {
            if (fired || mutationIndex != 0)
                return false;
            fired = true;
            return true;
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

    private sealed class FixedCatalog : IDungeonItemCatalogProvider
    {
        private readonly IReadOnlyList<DungeonItemDefinition> all;
        private readonly Dictionary<string, DungeonItemDefinition> byId;

        internal FixedCatalog()
        {
            all = new[]
            {
                new DungeonItemDefinition(
                    OutputItemId,
                    "QA Apparel",
                    "Unique apparel output",
                    StockCategory.General,
                    10,
                    null,
                    2f,
                    1),
                new DungeonItemDefinition(
                    MaterialItemId,
                    "QA Textile",
                    "Apparel input",
                    StockCategory.General,
                    1,
                    null,
                    .5f,
                    75),
                new DungeonItemDefinition(
                    FillerItemId,
                    "QA Buffer Filler",
                    "Capacity filler",
                    StockCategory.General,
                    1,
                    null,
                    1f,
                    75)
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

    private sealed class ApparelFixtureFacilityOutputCapacityContributor :
        IProductionFacilityOutputCapacityContributor
    {
        private const string ContributorIdValue =
            "production-facility-output-capacity:qa-apparel";

        public string ContributorId => ContributorIdValue;
        public int ContractVersion => 1;

        public ProductionFacilityOutputCapacityContribution Capture(
            ProductionFacilityCapacitySubject subject)
        {
            bool applies = string.Equals(
                    subject.DefinitionId,
                    FacilityDefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    subject.WorkstationTag,
                    WorkstationTag,
                    StringComparison.Ordinal);
            if (!applies)
            {
                return new ProductionFacilityOutputCapacityContribution(
                    ContributorIdValue,
                    1,
                    false,
                    Array.Empty<ProductionFacilityOutputCapacityBranch>());
            }

            return new ProductionFacilityOutputCapacityContribution(
                ContributorIdValue,
                1,
                true,
                new[]
                {
                    new ProductionFacilityOutputCapacityBranch(
                        "qa-apparel:craft",
                        new[]
                        {
                            new ProductionFacilityOutputMaximumMassRequest(
                                ApparelPhysicalTransaction.OutputLineId,
                                OutputItemId,
                                ProductionOutputCapabilityIds.ApparelWorkOrder,
                                1)
                        })
                });
        }
    }

    private static bool IsLowercaseSha256(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            if (!(c is >= '0' and <= '9') && !(c is >= 'a' and <= 'f'))
                return false;
        }
        return true;
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
            OutputItemId,
            StringComparison.Ordinal);

        public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity,
            IPhysicalItemMassQuery massQuery) => new(
                descriptor,
                maximumQuantity,
                4_000L,
                checked(4_000L * maximumQuantity),
                massQuery.AuthorityRevision,
                new string('a', 64));
    }
}

internal sealed class ApparelFixtureStandardOutputHandler :
    IProductionOutputCapability,
    IProductionOutputMaximumMassCapability
{
    public string CapabilityId =>
        ProductionOutputCapabilityIds.StandardDefinition;
    public int ContractVersion =>
        ProductionOutputCapabilityIds.StandardDefinitionVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.DefinitionOnlyCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion;
    public bool SupportsAutomaticSelection => true;
    public bool CanHandle(string itemId) => !string.IsNullOrEmpty(itemId)
        && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal);

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
#endif
