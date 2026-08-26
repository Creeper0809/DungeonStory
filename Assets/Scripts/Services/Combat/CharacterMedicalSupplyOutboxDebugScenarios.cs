#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class CharacterMedicalSupplyOutboxDebugScenarios
{
    private const string Item = "medicine:qa:medical-supply";
    private const string Destination = "facility-input:medical:qa";

    [MenuItem("Dungeon Story/QA/V27/Character Medical Supply Outbox")]
    private static void RunFromMenu()
    {
        VerifyPublishedSupplyRecoversAcknowledgementOnly();
        VerifyUncommittedIntentClearsWithoutAdvancingSequence();
        VerifyExtractedBloodUsesExactPhysicalDefinition();
        Debug.Log(
            "[V27][PASS] Character medical supply Sink, package tare, order publication and acknowledgement recovery are exact.");
    }

    private static void VerifyPublishedSupplyRecoversAcknowledgementOnly()
    {
        ResourceItemDefinitionSO medicine = CreateMedicine();
        try
        {
            WorldItemRepository repository = new(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            string stackId = repository.AddEditorTestStack(
                Item,
                2,
                WorldItemStackState.FacilityBuffer,
                Destination,
                position: new Vector2Int(4, 6));
            WorldItemStackSnapshot stack = new()
            {
                StackId = stackId,
                ItemId = Item,
                Quantity = 2,
                State = WorldItemStackState.FacilityBuffer,
                Position = new Vector2Int(4, 6),
                DestinationId = Destination,
                ReservedByPersistentId = string.Empty
            };
            PhysicalItemBatchDispositionService dispositions = new(
                repository,
                new FixedMassQuery(140L),
                NullItemMarkerPresenter.Instance);
            PhysicalFacilityItemSinkGateway baseGateway = new(
                new FixedStockQuery(stack),
                dispositions);
            CharacterMedicalOrder order = CreateIntentOrder();
            Require(
                baseGateway.TryCommitSinkPending(
                    Destination,
                    Item,
                    1,
                    order.treatmentSupplyOperationId,
                    order.treatmentSupplyReasonCode,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out string commitFailure)
                && receipt.InputMassGrams == 140L
                && repository.GetEditorTestQuantity(stackId) == 1,
                "Medical supply fixture Sink failed: " + commitFailure);

            RecordingPackageTareDisposition tare = new();
            CharacterMedicalSupplyCoordinator coordinator = new(
                new FixedSupplyStockPort(stack),
                new FixedResourceCatalog(medicine),
                new FailFirstAcknowledgeGateway(baseGateway),
                tare);
            Require(
                !coordinator.TryRecoverPendingSupply(
                    order,
                    out string recoveryFailure)
                && recoveryFailure.Contains(
                    "injected",
                    StringComparison.Ordinal),
                "Medical supply acknowledgement fault did not fail-loud.");
            Require(
                order.treatmentSupplyConsumed
                && (CharacterMedicalSupplyCommitPhase)
                    order.treatmentSupplyCommitPhase
                    == CharacterMedicalSupplyCommitPhase.SupplyPublished
                && order.treatmentInputMassGrams == 140L
                && string.Equals(
                    order.treatmentPhysicalCommitId,
                    receipt.CommitId,
                    StringComparison.Ordinal)
                && repository.GetEditorPendingBatchDispositionCount() == 1
                && tare.CallCount == 1
                && tare.LastPosition == new Vector2Int(4, 6),
                "Medical supply outcome was not durable before acknowledgement.");
            DungeonGameRestoreReport publishedReport = new();
            CharacterMedicalSaveValidation.Validate(
                new DungeonCharacterMedicalSaveData
                {
                    orderSequence = 1,
                    orders = new List<CharacterMedicalOrder>
                    {
                        CharacterMedicalOrderPersistence.Clone(order)
                    }
                },
                publishedReport,
                new FixedResourceCatalog(medicine),
                new ResourceItemDefinitionCatalog(
                    new ItemDefinitionSO[] { medicine }));
            Require(
                publishedReport.Success,
                "Published medical supply provenance failed current-format validation: "
                + string.Join(" | ", publishedReport.Errors));

            CharacterMedicalSupplyCoordinator recovered = new(
                new FixedSupplyStockPort(stack),
                new FixedResourceCatalog(medicine),
                baseGateway,
                tare);
            Require(
                recovered.TryRecoverPendingSupply(order, out recoveryFailure)
                && (CharacterMedicalSupplyCommitPhase)
                    order.treatmentSupplyCommitPhase
                    == CharacterMedicalSupplyCommitPhase.None
                && order.treatmentSupplyOperationSequence == 2
                && order.treatmentSupplyConsumed
                && repository.GetEditorTestQuantity(stackId) == 1
                && repository.GetEditorPendingBatchDispositionCount() == 0
                && tare.CallCount == 1,
                "Medical supply acknowledgement-only recovery failed: "
                + recoveryFailure);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(medicine);
        }
    }

    private static void VerifyUncommittedIntentClearsWithoutAdvancingSequence()
    {
        ResourceItemDefinitionSO medicine = CreateMedicine();
        try
        {
            WorldItemRepository repository = new(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemBatchDispositionService dispositions = new(
                repository,
                new FixedMassQuery(140L),
                NullItemMarkerPresenter.Instance);
            CharacterMedicalOrder order = CreateIntentOrder();
            CharacterMedicalSupplyCoordinator coordinator = new(
                new FixedSupplyStockPort(),
                new FixedResourceCatalog(medicine),
                new PhysicalFacilityItemSinkGateway(
                    new FixedStockQuery(),
                    dispositions),
                new RecordingPackageTareDisposition());

            Require(
                coordinator.TryRecoverPendingSupply(
                    order,
                    out string failureReason)
                && (CharacterMedicalSupplyCommitPhase)
                    order.treatmentSupplyCommitPhase
                    == CharacterMedicalSupplyCommitPhase.None
                && order.treatmentSupplyOperationSequence == 1
                && !order.treatmentSupplyConsumed,
                "Uncommitted medical supply intent changed consumption/sequence: "
                + failureReason);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(medicine);
        }
    }

    private static void VerifyExtractedBloodUsesExactPhysicalDefinition()
    {
        ResourceItemDefinitionSO medicine = CreateMedicine();
        GenericItemDefinitionSO extractedBlood =
            ScriptableObject.CreateInstance<GenericItemDefinitionSO>();
        extractedBlood.ConfigureCore(
            CaptivityItemDefinitions.ExtractedBloodItemId,
            "QA extracted blood",
            "QA exact physical fallback fixture.",
            StockCategory.Biological,
            price: 22,
            weight: 0.5f,
            stackLimit: 20);
        try
        {
            RecordingSupplyStockPort stock = new();
            CharacterMedicalSupplyCoordinator coordinator = new(
                stock,
                new FixedResourceCatalog(medicine, exposeItems: false),
                new RejectingSinkGateway(),
                new RecordingPackageTareDisposition());
            CharacterMedicalOrder order = new()
            {
                orderId = "medical:2",
                patientId = "character:qa:extracted-blood",
                state = CharacterMedicalOrderState.Treating,
                statusCode = CharacterMedicalStatusCode.SupplyUnavailable,
                treatmentMaterialDestinationId = Destination
            };

            Require(
                coordinator.TryRequestExtractedBlood(
                    order,
                    new Vector2Int(9, 3))
                && string.Equals(
                    stock.RequestedItemId,
                    CaptivityItemDefinitions.ExtractedBloodItemId,
                    StringComparison.Ordinal)
                && stock.RequestedPosition == new Vector2Int(9, 3)
                && string.Equals(
                    stock.RequestedDestinationId,
                    Destination,
                    StringComparison.Ordinal)
                && order.treatmentSupply
                    == CharacterMedicalSupplyKind.ExtractedBlood
                && order.treatmentSupplyDeliveryRequested,
                "Extracted-blood fallback did not request its exact physical definition.");
            Require(
                coordinator.TryRequestExtractedBlood(
                    order,
                    new Vector2Int(10, 4))
                && stock.RequestCount == 1,
                "Extracted-blood fallback duplicated an active exact-item delivery request.");

            CharacterMedicalOrder pending =
                CharacterMedicalOrderPersistence.Clone(order);
            pending.treatmentSupplyCommitPhase =
                (int)CharacterMedicalSupplyCommitPhase.IntentRecorded;
            pending.treatmentSupplyOperationId =
                "character-medical-supply:medical:2:00000001";
            pending.treatmentSupplyReasonCode =
                CharacterMedicalSupplyCoordinator.DispositionReasonCode;
            pending.treatmentPhysicalItemId =
                CaptivityItemDefinitions.ExtractedBloodItemId;
            pending.treatmentPhysicalQuantity = 1;
            pending.treatmentOutputX = 9;
            pending.treatmentOutputY = 3;
            DungeonGameRestoreReport report = new();
            CharacterMedicalSaveValidation.Validate(
                new DungeonCharacterMedicalSaveData
                {
                    orderSequence = 2,
                    orders = new List<CharacterMedicalOrder> { pending }
                },
                report,
                new FixedResourceCatalog(medicine, exposeItems: false),
                new ResourceItemDefinitionCatalog(
                    new ItemDefinitionSO[] { medicine, extractedBlood }));
            Require(
                report.Success,
                "Generic extracted-blood physical intent failed current-format validation: "
                + string.Join(" | ", report.Errors));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(medicine);
            UnityEngine.Object.DestroyImmediate(extractedBlood);
        }
    }

    private static CharacterMedicalOrder CreateIntentOrder() => new()
    {
        orderId = "medical:1",
        patientId = "character:qa:medical-supply",
        state = CharacterMedicalOrderState.Treating,
        statusCode = CharacterMedicalStatusCode.MedicineReady,
        statusParameters = new List<string> { Item },
        treatmentSupply = CharacterMedicalSupplyKind.Medicine,
        treatmentSupplyConsumed = false,
        treatmentSupplyDeliveryRequested = true,
        treatmentItemId = Item,
        treatmentPotency = 1f,
        treatmentMaterialDestinationId = Destination,
        treatmentSupplyCommitPhase =
            (int)CharacterMedicalSupplyCommitPhase.IntentRecorded,
        treatmentSupplyOperationSequence = 1,
        treatmentSupplyOperationId =
            "character-medical-supply:medical:1:00000001",
        treatmentSupplyReasonCode =
            CharacterMedicalSupplyCoordinator.DispositionReasonCode,
        treatmentPhysicalItemId = Item,
        treatmentPhysicalQuantity = 1,
        treatmentOutputX = 4,
        treatmentOutputY = 6
    };

    private static ResourceItemDefinitionSO CreateMedicine()
    {
        ResourceItemDefinitionSO medicine =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        medicine.Configure(
            Item,
            "QA medical supply",
            "QA medical supply outbox fixture.",
            StockCategory.Medicine,
            ResourceItemKind.Medicine,
            ResourceIngredientTag.None,
            1,
            0.14f,
            30,
            string.Empty);
        medicine.ConfigureMedicine(true, 1f, 0f, 0f, 0f);
        return medicine;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedSupplyStockPort :
        ICharacterMedicalSupplyStockPort
    {
        private readonly IReadOnlyList<WorldItemStackSnapshot> stacks;
        internal FixedSupplyStockPort(params WorldItemStackSnapshot[] stacks) =>
            this.stacks = stacks ?? Array.Empty<WorldItemStackSnapshot>();
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => stacks;
        public bool TryRequestItemDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason = "not used";
            return false;
        }
    }

    private sealed class RecordingSupplyStockPort :
        ICharacterMedicalSupplyStockPort
    {
        public string RequestedItemId { get; private set; } = string.Empty;
        public Vector2Int RequestedPosition { get; private set; }
        public string RequestedDestinationId { get; private set; } = string.Empty;
        public int RequestCount { get; private set; }
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();
        public bool TryRequestItemDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            RequestedItemId = itemId ?? string.Empty;
            RequestedPosition = destinationPosition;
            RequestedDestinationId = destinationId ?? string.Empty;
            RequestCount++;
            requested = amount;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixedResourceCatalog :
        IResourceEconomyContentCatalog
    {
        private readonly ResourceItemDefinitionSO item;
        private readonly bool exposeItems;
        internal FixedResourceCatalog(
            ResourceItemDefinitionSO item,
            bool exposeItems = true)
        {
            this.item = item;
            this.exposeItems = exposeItems;
        }
        public IReadOnlyList<ResourceItemDefinitionSO> Items => exposeItems
            ? new[] { item }
            : Array.Empty<ResourceItemDefinitionSO>();
        public IReadOnlyList<ProductionRecipeSO> Recipes =>
            Array.Empty<ProductionRecipeSO>();
        public IReadOnlyList<CropDefinitionSO> Crops =>
            Array.Empty<CropDefinitionSO>();
        public IReadOnlyList<CraftMaterialDefinitionSO> Materials =>
            Array.Empty<CraftMaterialDefinitionSO>();
        public IReadOnlyList<SubstanceDefinitionView> Substances =>
            Array.Empty<SubstanceDefinitionView>();
        public bool TryGetItem(
            string itemId,
            out ResourceItemDefinitionSO definition)
        {
            definition = string.Equals(itemId, item.ItemId, StringComparison.Ordinal)
                ? item
                : null;
            return definition != null;
        }
        public bool TryGetRecipe(
            string recipeId,
            out ProductionRecipeSO definition)
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
            definition = default;
            return false;
        }
    }

    private sealed class FixedStockQuery : IStockQuery
    {
        private readonly IReadOnlyList<WorldItemStackSnapshot> stacks;
        internal FixedStockQuery(params WorldItemStackSnapshot[] stacks) =>
            this.stacks = stacks ?? Array.Empty<WorldItemStackSnapshot>();
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => stacks;
        public int GetGlobalQuantity(string itemDefinitionId) => stacks
            .Where(value => string.Equals(
                value.ItemId,
                itemDefinitionId,
                StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;
        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly PhysicalMassGrams unitMass;
        internal FixedMassQuery(long grams) =>
            unitMass = new PhysicalMassGrams(grams);
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            unitMass;
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => unitMass;
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => unitMass;
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            unitMass.Multiply(lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => unitMass.Multiply(quantity);
    }

    private sealed class FailFirstAcknowledgeGateway :
        IPhysicalFacilityItemSinkGateway
    {
        private readonly IPhysicalFacilityItemSinkGateway inner;
        private bool rejected;
        internal FailFirstAcknowledgeGateway(
            IPhysicalFacilityItemSinkGateway inner) => this.inner = inner;
        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitSinkPending(
            destinationId,
            itemId,
            quantity,
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
            if (!rejected)
            {
                rejected = true;
                failureReason = "injected acknowledgement failure";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }
    }

    private sealed class RejectingSinkGateway :
        IPhysicalFacilityItemSinkGateway
    {
        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "not used";
            return false;
        }
        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }
        public bool Acknowledge(string commitId, out string failureReason)
        {
            failureReason = "not used";
            return false;
        }
    }

    private sealed class RecordingPackageTareDisposition :
        IPackagedLotTareDispositionService
    {
        public int CallCount { get; private set; }
        public Vector2Int LastPosition { get; private set; }
        public bool EnsureTerminalSinkOutputs(
            IReadOnlyDictionary<string, int> consumedItems,
            Vector2Int outputPosition,
            string parentCommitId,
            out PackagedLotTareOutputReceipt receipt,
            out string failureReason)
        {
            CallCount++;
            LastPosition = outputPosition;
            receipt = default;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class NullItemMarkerPresenter : IItemMarkerPresenter
    {
        internal static readonly NullItemMarkerPresenter Instance = new();
        public void Initialize(IWorldItemMarkerDataSource dataSource) { }
        public void RefreshAll(IEnumerable<Vector2Int> positions) { }
        public void RefreshAt(Vector2Int position) { }
        public bool TryGetMarkerAt(
            Vector2Int position,
            out UnityEngine.Object marker)
        {
            marker = null;
            return false;
        }
        public void Clear() { }
    }
}
#endif
