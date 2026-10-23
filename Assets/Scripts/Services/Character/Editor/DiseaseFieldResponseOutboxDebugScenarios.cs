#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class DiseaseFieldResponseOutboxDebugScenarios
{
    private const string Character = "character:qa:field-response";
    private const string Disease = "disease:qa:field-response";
    private const string Response = "response:wet-cleaning";
    private const string VaccineResponse = "response:vaccine:" + Disease;
    private const string UnknownResponse = "response:qa:unregistered";
    private const string Facility = "building:qa:field-response";
    private const string Item = "resource:clean-water";
    private const string VaccineItem = "medicine:vaccine:qa:field-response";

    [MenuItem("Dungeon Story/QA/V27/Disease Field Response Outbox")]
    public static void RunFromMenu()
    {
        VerifyCommittedSinkRecoversExactlyOnce();
        VerifyUncommittedIntentClearsWithoutAdvancingSequence();
        VerifyVaccineSelectionAndInvalidAuthoringRejection();
        Debug.Log(
            "[V27][PASS] Disease field-response physical Sink, explicit vaccine selection, registered response rejection, durable outcome publication, acknowledgement and replay gates are exact.");
    }

    private static void VerifyCommittedSinkRecoversExactlyOnce()
    {
        FixedDiseaseCatalog diseases = new();
        TestPopulationHealthPersistence persistence = new(
            CreateHealthPayload(withIntent: true),
            diseases);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            Item,
            3,
            WorldItemStackState.FacilityBuffer,
            position: new Vector2Int(5, 5),
            destinationId: Facility);
        FixedStockQuery stock = new(new WorldItemStackSnapshot
        {
            StackId = stackId,
            ItemId = Item,
            Quantity = 3,
            State = WorldItemStackState.FacilityBuffer,
            DestinationId = Facility,
            ReservedByPersistentId = string.Empty
        });
        PhysicalItemBatchDispositionService dispositions = new(
            repository,
            new FixedMassQuery(750L),
            EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
        PhysicalFacilityItemSinkGateway gateway = new(stock, dispositions);
        DiseaseFieldResponseCommitSaveData intent =
            persistence.Capture().pendingFieldResponse;
        Require(
            gateway.TryCommitSinkPending(
                Facility,
                Item,
                2,
                intent.operationId,
                intent.reasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure)
            && receipt.IsCommitted
            && receipt.Kind == PhysicalItemDispositionKind.Sink
            && receipt.Quantity == 2
            && receipt.InputMassGrams == 1500L
            && repository.GetEditorTestQuantity(stackId) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Field-response Sink fixture did not commit exact quantity/mass: "
            + commitFailure);

        RecordingPackageTareDisposition tare = new();
        DiseaseFieldResponseRuntime runtime = CreateRuntime(
            diseases,
            persistence,
            gateway,
            tare);
        Require(
            runtime.TryRecoverPending(out DomainFailure recoveryFailure),
            "Field-response outbox recovery failed: " + recoveryFailure);

        PopulationHealthWorldSaveData terminal = persistence.Capture();
        ActiveDiseaseSaveData active = terminal.characters.Single()
            .activeDiseases.Single();
        Require(
            Mathf.Approximately(active.severity, 36f)
            && terminal.nextFieldResponseOperationSequence == 2
            && (DiseaseFieldResponseCommitPhase)terminal.pendingFieldResponse.phase
                == DiseaseFieldResponseCommitPhase.None
            && repository.GetEditorTestQuantity(stackId) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && tare.CallCount == 1
            && string.Equals(
                tare.LastParentCommitId,
                receipt.CommitId,
                StringComparison.Ordinal)
            && tare.LastOutputPosition == new Vector2Int(5, 5),
            "Field-response outbox did not publish once then acknowledge exactly.");

        Require(
            runtime.TryRecoverPending(out recoveryFailure)
            && Mathf.Approximately(
                persistence.Capture().characters.Single()
                    .activeDiseases.Single().severity,
                36f)
            && repository.GetEditorTestQuantity(stackId) == 1,
            "Field-response terminal replay applied the outcome or Sink twice: "
            + recoveryFailure);
    }

    private static void VerifyUncommittedIntentClearsWithoutAdvancingSequence()
    {
        FixedDiseaseCatalog diseases = new();
        TestPopulationHealthPersistence persistence = new(
            CreateHealthPayload(withIntent: true),
            diseases);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService dispositions = new(
            repository,
            new FixedMassQuery(750L),
            EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
        DiseaseFieldResponseRuntime runtime = CreateRuntime(
            diseases,
            persistence,
            new PhysicalFacilityItemSinkGateway(
                new FixedStockQuery(),
                dispositions),
            new RecordingPackageTareDisposition());

        Require(
            runtime.TryRecoverPending(out DomainFailure failure),
            "Uncommitted field-response intent recovery failed: " + failure);
        PopulationHealthWorldSaveData recovered = persistence.Capture();
        Require(
            recovered.nextFieldResponseOperationSequence == 1
            && (DiseaseFieldResponseCommitPhase)recovered.pendingFieldResponse.phase
                == DiseaseFieldResponseCommitPhase.None
            && Mathf.Approximately(
                recovered.characters.Single().activeDiseases.Single().severity,
                50f),
            "Uncommitted intent advanced sequence or mutated health.");
    }

    private static void VerifyVaccineSelectionAndInvalidAuthoringRejection()
    {
        ResourceItemDefinitionSO vaccine = CreateVaccine();
        GameObject facilityObject = new("QA Disease Field Response Facility");
        facilityObject.SetActive(false);
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        try
        {
            facility.RestorePersistentIdentity((BuildingInstanceId)Facility);
            FixedFacilityQuery facilities = new(facility);
            RecordingVaccinationService vaccinations = new();
            RecordingSinkGateway physicalItems = new();
            FixedItemDefinitionCatalog itemCatalog = new(vaccine);

            SingleDiseaseCatalog validDiseases = new(CreateDisease(
                VaccineResponse,
                VaccineItem));
            TestPopulationHealthPersistence validPersistence = new(
                CreateHealthPayload(withIntent: false),
                validDiseases);
            DiseaseFieldResponseRuntime validRuntime = CreateRuntime(
                validDiseases,
                validPersistence,
                facilities,
                itemCatalog,
                physicalItems,
                vaccinations);
            Require(
                validRuntime.TryApply(
                    new CharacterId(Character),
                    Disease,
                    VaccineResponse,
                    Facility,
                    out DomainFailure failure)
                && vaccinations.CallCount == 1
                && string.Equals(
                    vaccinations.LastVaccineItemId,
                    VaccineItem,
                    StringComparison.Ordinal),
                "Field-response vaccination did not use the explicitly authored item: "
                + failure);

            SingleDiseaseCatalog emptyVaccineDiseases = new(CreateDisease(
                VaccineResponse,
                string.Empty));
            TestPopulationHealthPersistence emptyVaccinePersistence = new(
                CreateHealthPayload(withIntent: false),
                emptyVaccineDiseases);
            Require(
                !CreateRuntime(
                        emptyVaccineDiseases,
                        emptyVaccinePersistence,
                        facilities,
                        itemCatalog,
                        physicalItems,
                        vaccinations)
                    .TryApply(
                        new CharacterId(Character),
                        Disease,
                        VaccineResponse,
                        Facility,
                        out failure)
                && failure.Code == FailureCode.VaccineDefinitionMissing
                && vaccinations.CallCount == 1,
                "An empty authored vaccine item reached the vaccination service.");

            SingleDiseaseCatalog missingVaccineDiseases = new(CreateDisease(
                VaccineResponse,
                "medicine:vaccine:qa:missing"));
            TestPopulationHealthPersistence missingVaccinePersistence = new(
                CreateHealthPayload(withIntent: false),
                missingVaccineDiseases);
            Require(
                !CreateRuntime(
                        missingVaccineDiseases,
                        missingVaccinePersistence,
                        facilities,
                        itemCatalog,
                        physicalItems,
                        vaccinations)
                    .TryApply(
                        new CharacterId(Character),
                        Disease,
                        VaccineResponse,
                        Facility,
                        out failure)
                && failure.Code == FailureCode.VaccineDefinitionMissing
                && vaccinations.CallCount == 1,
                "An unregistered authored vaccine item reached the vaccination service.");

            SingleDiseaseCatalog unknownResponseDiseases = new(CreateDisease(
                UnknownResponse,
                string.Empty,
                vaccineAllowed: false));
            TestPopulationHealthPersistence unknownResponsePersistence = new(
                CreateHealthPayload(withIntent: false),
                unknownResponseDiseases);
            string saveBefore = JsonUtility.ToJson(
                unknownResponsePersistence.Capture());
            Require(
                !CreateRuntime(
                        unknownResponseDiseases,
                        unknownResponsePersistence,
                        facilities,
                        itemCatalog,
                        physicalItems,
                        vaccinations)
                    .TryApply(
                        new CharacterId(Character),
                        Disease,
                        UnknownResponse,
                        Facility,
                        out failure)
                && failure.Code == FailureCode.VaccineDiseaseMismatch
                && physicalItems.CommitCalls == 0
                && vaccinations.CallCount == 1
                && string.Equals(
                    saveBefore,
                    JsonUtility.ToJson(unknownResponsePersistence.Capture()),
                    StringComparison.Ordinal),
                "An unregistered authored response consumed stock or changed save state.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(vaccine);
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private static DiseaseDefinition CreateDisease(
        string responseId,
        string vaccineItemId,
        bool vaccineAllowed = true) => new(
        Disease,
        "QA disease",
        DiseaseTransmissionRoute.Contact,
        0,
        1,
        0.1f,
        50f,
        DiseaseTargetSystem.Core,
        vaccineAllowed,
        symptomProfileId: "symptom:qa",
        fieldResponseIds: new[] { responseId },
        vaccineItemId: vaccineItemId);

    private static ResourceItemDefinitionSO CreateVaccine()
    {
        ResourceItemDefinitionSO vaccine =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        vaccine.Configure(
            VaccineItem,
            "QA field-response vaccine",
            "Verifies explicit authored field-response vaccine selection.",
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

    private static DiseaseFieldResponseRuntime CreateRuntime(
        IDiseaseDefinitionCatalog diseases,
        TestPopulationHealthPersistence persistence,
        IPhysicalFacilityItemSinkGateway gateway,
        IPackagedLotTareDispositionService packagedTare) => new(
        diseases,
        persistence,
        persistence,
        new EmptyFacilityQuery(),
        new EmptyItemDefinitionCatalog(),
        gateway,
        packagedTare,
        new RejectingVaccinationService(),
        new NeutralDiseaseModifiers());

    private static DiseaseFieldResponseRuntime CreateRuntime(
        IDiseaseDefinitionCatalog diseases,
        TestPopulationHealthPersistence persistence,
        IFacilityCapabilityQuery facilities,
        IItemDefinitionCatalog items,
        IPhysicalFacilityItemSinkGateway gateway,
        IPhysicalVaccinationService vaccination) => new(
        diseases,
        persistence,
        persistence,
        facilities,
        items,
        gateway,
        new RecordingPackageTareDisposition(),
        vaccination,
        new NeutralDiseaseModifiers());

    private static PopulationHealthWorldSaveData CreateHealthPayload(
        bool withIntent)
    {
        CharacterId characterId = new(Character);
        PopulationHealthWorldSaveData payload = new()
        {
            currentAbsoluteDay = 1,
            nextFieldResponseOperationSequence = 1,
            characters = new List<CharacterPopulationHealthSaveData>
            {
                new()
                {
                    characterId = Character,
                    activeDiseases = new List<ActiveDiseaseSaveData>
                    {
                        new()
                        {
                            diseaseId = Disease,
                            infectionDay = 1,
                            symptomDay = 1,
                            recoveryDay = 10,
                            severity = 50f,
                            diagnosed = true
                        }
                    }
                }
            }
        };
        if (withIntent)
        {
            payload.pendingFieldResponse = new DiseaseFieldResponseCommitSaveData
            {
                phase = (int)DiseaseFieldResponseCommitPhase.IntentRecorded,
                operationSequence = 1,
                operationId = DiseaseFieldResponseRuntime.FormatOperationId(
                    characterId,
                    Disease,
                    Response,
                    1),
                reasonCode = DiseaseFieldResponseRuntime.DispositionReasonCode,
                characterId = Character,
                diseaseId = Disease,
                responseId = Response,
                facilityInstanceId = Facility,
                outputGridX = 5,
                outputGridY = 5,
                itemId = Item,
                quantity = 2,
                severityReduction = 14f
            };
        }
        return payload;
    }

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
            "QA disease",
            DiseaseTransmissionRoute.Contact,
            0,
            1,
            0.1f,
            50f,
            DiseaseTargetSystem.Core,
            false,
            symptomProfileId: "symptom:qa",
            fieldResponseIds: new[] { Response });

        public IReadOnlyList<DiseaseDefinition> Definitions =>
            new[] { definition };

        public DiseaseDefinition Require(string diseaseId) =>
            string.Equals(diseaseId, definition.Id, StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(diseaseId);
    }

    private sealed class SingleDiseaseCatalog : IDiseaseDefinitionCatalog
    {
        private readonly DiseaseDefinition definition;
        public SingleDiseaseCatalog(DiseaseDefinition definition) =>
            this.definition = definition;
        public IReadOnlyList<DiseaseDefinition> Definitions =>
            new[] { definition };
        public DiseaseDefinition Require(string diseaseId) =>
            string.Equals(diseaseId, definition.Id, StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(diseaseId);
    }

    private sealed class TestPopulationHealthPersistence :
        IPopulationHealthPersistence,
        IPopulationHealthQuery
    {
        private readonly IDiseaseDefinitionCatalog diseases;
        private PopulationHealthAggregateState current;

        public TestPopulationHealthPersistence(
            PopulationHealthWorldSaveData initial,
            IDiseaseDefinitionCatalog diseases)
        {
            this.diseases = diseases;
            current = PopulationHealthAggregateState.Restore(initial, diseases);
        }

        public int Version => 1;
        public PopulationHealthWorldSaveData Capture() => current.Capture();
        public PopulationHealthAggregateState PrepareRestore(
            PopulationHealthWorldSaveData data) =>
            PopulationHealthAggregateState.Restore(data, diseases);
        public void PublishRestore(PopulationHealthAggregateState candidate) =>
            current = candidate ?? throw new ArgumentNullException(nameof(candidate));
        public float GetImmunity(CharacterId characterId, string diseaseId) =>
            current.GetImmunity(characterId, diseaseId);
        public bool TryGetCharacterSnapshot(
            CharacterId characterId,
            out PopulationCharacterHealthSnapshot snapshot) =>
            current.TryGetCharacterSnapshot(characterId, out snapshot);
        public IReadOnlyList<EpidemicSnapshot> GetEpidemics(bool declaredOnly) =>
            current.GetEpidemics(declaredOnly);
        public IReadOnlyList<ContagiousDiseaseSnapshot> GetContagious() =>
            current.GetContagious(diseases);
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

    private sealed class EmptyFacilityQuery : IFacilityCapabilityQuery
    {
        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") => Array.Empty<BuildableObject>();
        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) => Array.Empty<BuildableObject>();
    }

    private sealed class FixedFacilityQuery : IFacilityCapabilityQuery
    {
        private readonly BuildableObject facility;
        public FixedFacilityQuery(BuildableObject facility) =>
            this.facility = facility;
        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") =>
            capability == FacilityCapabilityKind.Medical
                ? new[] { facility }
                : Array.Empty<BuildableObject>();
        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) =>
            Array.Empty<BuildableObject>();
    }

    private sealed class EmptyItemDefinitionCatalog : IItemDefinitionCatalog
    {
        public IReadOnlyList<ItemDefinitionSO> All => Array.Empty<ItemDefinitionSO>();
        public bool TryGet(ItemDefinitionId itemId, out ItemDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public ItemDefinitionSO GetRequired(ItemDefinitionId itemId) =>
            throw new KeyNotFoundException(itemId.Value);
        public IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    private sealed class FixedItemDefinitionCatalog : IItemDefinitionCatalog
    {
        private readonly ItemDefinitionSO item;
        public FixedItemDefinitionCatalog(ItemDefinitionSO item) =>
            this.item = item;
        public IReadOnlyList<ItemDefinitionSO> All => new[] { item };
        public bool TryGet(
            ItemDefinitionId itemId,
            out ItemDefinitionSO definition)
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
            TryGet(itemId, out ItemDefinitionSO definition)
                ? definition
                : throw new KeyNotFoundException(itemId.Value);
        public IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    private sealed class RecordingSinkGateway :
        IPhysicalFacilityItemSinkGateway
    {
        public int CommitCalls { get; private set; }
        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            CommitCalls++;
            receipt = default;
            failureReason = "unexpected field-response stock consumption";
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
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingVaccinationService :
        IPhysicalVaccinationService
    {
        public int CallCount { get; private set; }
        public string LastVaccineItemId { get; private set; } = string.Empty;
        public bool TryVaccinate(
            CharacterId characterId,
            string medicalFacilityDestinationId,
            Vector2Int outputPosition,
            string vaccineItemId,
            out DomainFailure failure)
        {
            CallCount++;
            LastVaccineItemId = vaccineItemId ?? string.Empty;
            failure = DomainFailure.None;
            return true;
        }
    }

    private sealed class RejectingVaccinationService : IPhysicalVaccinationService
    {
        public bool TryVaccinate(
            CharacterId characterId,
            string medicalFacilityDestinationId,
            Vector2Int outputPosition,
            string vaccineItemId,
            out DomainFailure failure)
        {
            failure = new DomainFailure(FailureCode.VaccineDefinitionMissing);
            return false;
        }
    }

    private sealed class RecordingPackageTareDisposition :
        IPackagedLotTareDispositionService
    {
        public int CallCount { get; private set; }
        public string LastParentCommitId { get; private set; } = string.Empty;
        public Vector2Int LastOutputPosition { get; private set; }

        public bool EnsureTerminalSinkOutputs(
            IReadOnlyDictionary<string, int> consumedItems,
            Vector2Int outputPosition,
            string parentCommitId,
            out PackagedLotTareOutputReceipt receipt,
            out string failureReason)
        {
            CallCount++;
            LastParentCommitId = parentCommitId ?? string.Empty;
            LastOutputPosition = outputPosition;
            receipt = default;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class NeutralDiseaseModifiers : IPopulationDiseaseModifierQuery
    {
        public PopulationDiseaseStatModifiers Resolve(
            CharacterId characterId,
            DiseaseDefinition disease) => PopulationDiseaseStatModifiers.Neutral;
    }
}
#endif
