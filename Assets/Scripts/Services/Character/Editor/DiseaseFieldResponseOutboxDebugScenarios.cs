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
    private const string Facility = "facility:qa:field-response";
    private const string Item = "resource:clean-water";

    [MenuItem("Dungeon Story/QA/V27/Disease Field Response Outbox")]
    public static void RunFromMenu()
    {
        VerifyCommittedSinkRecoversExactlyOnce();
        VerifyUncommittedIntentClearsWithoutAdvancingSequence();
        Debug.Log(
            "[V27][PASS] Disease field-response physical Sink, durable outcome publication, acknowledgement and replay gates are exact.");
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
            EditorNullItemMarkerPresenter.Instance);
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
            EditorNullItemMarkerPresenter.Instance);
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
