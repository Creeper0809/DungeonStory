#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PhysicalVaccinationOutboxDebugScenarios
{
    private const string Character = "character:qa:vaccination";
    private const string Disease = "disease:qa:vaccination";
    private const string Facility = "facility:qa:vaccination";
    private const string Item = "medicine:vaccine:qa";

    [MenuItem("Dungeon Story/QA/V27/Physical Vaccination Outbox")]
    public static void RunFromMenu()
    {
        VerifyOutcomeSurvivesAcknowledgementFailure();
        VerifyUncommittedIntentClearsWithoutAdvancingSequence();
        Debug.Log(
            "[V27][PASS] Vaccination physical Sink, package tare, durable immunity outbox, acknowledgement and replay gates are exact.");
    }

    private static void VerifyOutcomeSurvivesAcknowledgementFailure()
    {
        ResourceItemDefinitionSO vaccine = CreateVaccine();
        try
        {
            FixedDiseaseCatalog diseases = new();
            TestPopulationHealthPersistence persistence = new(
                CreateHealthPayload(),
                diseases);
            WorldItemRepository repository = new(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                Item,
                2,
                WorldItemStackState.FacilityBuffer,
                position: new Vector2Int(8, 9),
                destinationId: Facility);
            FixedStockQuery stock = new(new WorldItemStackSnapshot
            {
                StackId = stackId,
                ItemId = Item,
                Quantity = 2,
                State = WorldItemStackState.FacilityBuffer,
                Position = new Vector2Int(8, 9),
                DestinationId = Facility,
                ReservedByPersistentId = string.Empty
            });
            PhysicalItemBatchDispositionService dispositions = new(
                repository,
                new FixedMassQuery(400L),
                EditorNullItemMarkerPresenter.Instance);
            PhysicalFacilityItemSinkGateway baseGateway = new(
                stock,
                dispositions);
            FailFirstAcknowledgeGateway faultGateway = new(baseGateway);
            RecordingPackageTareDisposition tare = new();
            PhysicalVaccinationRuntime runtime = CreateRuntime(
                vaccine,
                diseases,
                persistence,
                faultGateway,
                tare);

            Require(
                !runtime.TryVaccinate(
                    new CharacterId(Character),
                    Facility,
                    new Vector2Int(8, 9),
                    Item,
                    out DomainFailure failure)
                && failure.Code == FailureCode.VaccineDoseUnavailable,
                "Injected vaccination acknowledgement failure did not fail-loud.");

            PopulationHealthWorldSaveData published = persistence.Capture();
            Require(
                Mathf.Approximately(
                    persistence.Current.GetImmunity(
                        new CharacterId(Character),
                        Disease),
                    70f)
                && (VaccinationCommitPhase)published.pendingVaccination.phase
                    == VaccinationCommitPhase.OutcomePublished
                && published.nextVaccinationOperationSequence == 1
                && repository.GetEditorTestQuantity(stackId) == 1
                && repository.GetEditorPendingBatchDispositionCount() == 1
                && tare.CallCount == 1
                && tare.LastOutputPosition == new Vector2Int(8, 9),
                "Vaccination outcome was not durable before acknowledgement.");

            PhysicalVaccinationRuntime recoveredRuntime = CreateRuntime(
                vaccine,
                diseases,
                persistence,
                baseGateway,
                tare);
            Require(
                recoveredRuntime.TryRecoverPending(out failure),
                "Vaccination recovery failed: " + failure);
            PopulationHealthWorldSaveData terminal = persistence.Capture();
            Require(
                terminal.nextVaccinationOperationSequence == 2
                && (VaccinationCommitPhase)terminal.pendingVaccination.phase
                    == VaccinationCommitPhase.None
                && repository.GetEditorTestQuantity(stackId) == 1
                && repository.GetEditorPendingBatchDispositionCount() == 0
                && tare.CallCount == 1,
                "Vaccination recovery repeated Sink/tare or did not clear outbox.");

            Require(
                recoveredRuntime.TryRecoverPending(out failure)
                && repository.GetEditorTestQuantity(stackId) == 1
                && Mathf.Approximately(
                    persistence.Current.GetImmunity(
                        new CharacterId(Character),
                        Disease),
                    70f),
                "Terminal vaccination replay consumed or published twice.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(vaccine);
        }
    }

    private static void VerifyUncommittedIntentClearsWithoutAdvancingSequence()
    {
        ResourceItemDefinitionSO vaccine = CreateVaccine();
        try
        {
            FixedDiseaseCatalog diseases = new();
            PopulationHealthWorldSaveData payload = CreateHealthPayload();
            payload.pendingVaccination = new VaccinationCommitSaveData
            {
                phase = (int)VaccinationCommitPhase.IntentRecorded,
                operationSequence = 1,
                operationId = PhysicalVaccinationRuntime.FormatOperationId(
                    new CharacterId(Character),
                    Disease,
                    1),
                reasonCode = PhysicalVaccinationRuntime.DispositionReasonCode,
                characterId = Character,
                diseaseId = Disease,
                facilityInstanceId = Facility,
                outputGridX = 8,
                outputGridY = 9,
                itemId = Item,
                quantity = 1
            };
            TestPopulationHealthPersistence persistence = new(payload, diseases);
            WorldItemRepository repository = new(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemBatchDispositionService dispositions = new(
                repository,
                new FixedMassQuery(400L),
                EditorNullItemMarkerPresenter.Instance);
            PhysicalVaccinationRuntime runtime = CreateRuntime(
                vaccine,
                diseases,
                persistence,
                new PhysicalFacilityItemSinkGateway(
                    new FixedStockQuery(),
                    dispositions),
                new RecordingPackageTareDisposition());

            Require(
                runtime.TryRecoverPending(out DomainFailure failure),
                "Uncommitted vaccination intent recovery failed: " + failure);
            PopulationHealthWorldSaveData recovered = persistence.Capture();
            Require(
                recovered.nextVaccinationOperationSequence == 1
                && (VaccinationCommitPhase)recovered.pendingVaccination.phase
                    == VaccinationCommitPhase.None
                && Mathf.Approximately(
                    persistence.Current.GetImmunity(
                        new CharacterId(Character),
                        Disease),
                    0f),
                "Uncommitted vaccination intent advanced sequence or immunity.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(vaccine);
        }
    }

    private static PhysicalVaccinationRuntime CreateRuntime(
        ResourceItemDefinitionSO vaccine,
        IDiseaseDefinitionCatalog diseases,
        TestPopulationHealthPersistence persistence,
        IPhysicalFacilityItemSinkGateway gateway,
        IPackagedLotTareDispositionService packagedTare) => new(
        new FixedItemCatalog(vaccine),
        new FixedLifeQuery(),
        diseases,
        persistence,
        new NeutralDiseaseModifiers(),
        gateway,
        packagedTare);

    private static ResourceItemDefinitionSO CreateVaccine()
    {
        ResourceItemDefinitionSO vaccine =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        vaccine.Configure(
            Item,
            "QA vaccine",
            "QA vaccination outbox fixture.",
            StockCategory.Medicine,
            ResourceItemKind.Medicine,
            ResourceIngredientTag.None,
            1,
            0.4f,
            50,
            string.Empty);
        vaccine.ConfigureVaccine(Disease, 1);
        return vaccine;
    }

    private static PopulationHealthWorldSaveData CreateHealthPayload() => new()
    {
        currentAbsoluteDay = 1,
        nextFieldResponseOperationSequence = 1,
        nextVaccinationOperationSequence = 1,
        characters = new List<CharacterPopulationHealthSaveData>
        {
            new() { characterId = Character }
        }
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedDiseaseCatalog : IDiseaseDefinitionCatalog
    {
        private readonly DiseaseDefinition definition = new(
            Disease,
            "QA vaccination disease",
            DiseaseTransmissionRoute.Contact,
            0,
            1,
            0.1f,
            50f,
            DiseaseTargetSystem.Core,
            true,
            symptomProfileId: "symptom:qa:vaccination",
            fieldResponseIds: new[] { "response:vaccine:qa" });

        public IReadOnlyList<DiseaseDefinition> Definitions =>
            new[] { definition };
        public DiseaseDefinition Require(string diseaseId) =>
            string.Equals(diseaseId, Disease, StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(diseaseId);
    }

    private sealed class TestPopulationHealthPersistence :
        IPopulationHealthPersistence
    {
        private readonly IDiseaseDefinitionCatalog diseases;
        public TestPopulationHealthPersistence(
            PopulationHealthWorldSaveData initial,
            IDiseaseDefinitionCatalog diseases)
        {
            this.diseases = diseases;
            Current = PopulationHealthAggregateState.Restore(initial, diseases);
        }
        public PopulationHealthAggregateState Current { get; private set; }
        public PopulationHealthWorldSaveData Capture() => Current.Capture();
        public PopulationHealthAggregateState PrepareRestore(
            PopulationHealthWorldSaveData data) =>
            PopulationHealthAggregateState.Restore(data, diseases);
        public void PublishRestore(PopulationHealthAggregateState candidate) =>
            Current = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    private sealed class FixedItemCatalog : IItemDefinitionCatalog
    {
        private readonly ResourceItemDefinitionSO item;
        public FixedItemCatalog(ResourceItemDefinitionSO item) => this.item = item;
        public IReadOnlyList<ItemDefinitionSO> All => new[] { item };
        public bool TryGet(ItemDefinitionId itemId, out ItemDefinitionSO definition)
        {
            definition = string.Equals(
                    itemId.Value,
                    item.ItemId,
                    StringComparison.Ordinal)
                ? item
                : null;
            return definition != null;
        }
        public ItemDefinitionSO GetRequired(ItemDefinitionId itemId) =>
            TryGet(itemId, out ItemDefinitionSO result)
                ? result
                : throw new KeyNotFoundException(itemId.Value);
        public IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    private sealed class FixedLifeQuery : ICharacterLifeQuery
    {
        public int Version => 1;
        public IReadOnlyCollection<CharacterLifeRecord> Records =>
            Array.Empty<CharacterLifeRecord>();
        public bool TryGet(CharacterId characterId, out CharacterLifeRecord record)
        {
            record = null;
            return string.Equals(
                characterId.Value,
                Character,
                StringComparison.Ordinal);
        }
    }

    private sealed class FixedStockQuery : IStockQuery
    {
        private readonly IReadOnlyList<WorldItemStackSnapshot> stacks;
        public FixedStockQuery(params WorldItemStackSnapshot[] stacks) =>
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
        public FixedMassQuery(long grams) => unitMass = new PhysicalMassGrams(grams);
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
        public FailFirstAcknowledgeGateway(
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

    private sealed class RecordingPackageTareDisposition :
        IPackagedLotTareDispositionService
    {
        public int CallCount { get; private set; }
        public Vector2Int LastOutputPosition { get; private set; }
        public bool EnsureTerminalSinkOutputs(
            IReadOnlyDictionary<string, int> consumedItems,
            Vector2Int outputPosition,
            string parentCommitId,
            out PackagedLotTareOutputReceipt receipt,
            out string failureReason)
        {
            CallCount++;
            LastOutputPosition = outputPosition;
            receipt = default;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class NeutralDiseaseModifiers :
        IPopulationDiseaseModifierQuery
    {
        public PopulationDiseaseStatModifiers Resolve(
            CharacterId characterId,
            DiseaseDefinition disease) =>
            PopulationDiseaseStatModifiers.Neutral;
    }
}
#endif
