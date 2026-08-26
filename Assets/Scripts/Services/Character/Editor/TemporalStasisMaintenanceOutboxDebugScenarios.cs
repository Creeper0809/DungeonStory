#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class TemporalStasisMaintenanceOutboxDebugScenarios
{
    private const string Character = "character:qa:temporal-stasis";
    private const string Facility = "facility:qa:temporal-stasis";

    [MenuItem("Dungeon Story/QA/V27/Temporal Stasis Maintenance Outbox")]
    public static void RunFromMenu()
    {
        VerifyTwoInputSinkAndAcknowledgementRecovery();
        VerifyMissingSecondInputDoesNotPartiallyConsume();
        VerifyUncommittedIntentClearsWithoutSequenceAdvance();
        Debug.Log(
            "[V27][PASS] Temporal-stasis maintenance exact two-input Sink, durable outcome, acknowledgement recovery and no-partial-debit gates are exact.");
    }

    private static void VerifyTwoInputSinkAndAcknowledgementRecovery()
    {
        CharacterLifeRuntime life = CreateLifeRuntime();
        CharacterLifeWorldSaveData intent = life.Capture();
        intent.pendingTemporalStasisMaintenance = CreateIntent();
        life.PublishRestore(life.PrepareRestore(intent));

        WorldItemRepository repository = CreateRepository();
        string runeStack = AddFacilityStack(
            repository,
            PhysicalAgeTreatmentRuntime.RuneConductorItemId,
            2,
            new Vector2Int(4, 5));
        string manaStack = AddFacilityStack(
            repository,
            PhysicalAgeTreatmentRuntime.ManaCrystalItemId,
            2,
            new Vector2Int(4, 6));
        FixedStockQuery stock = new(
            Snapshot(
                runeStack,
                PhysicalAgeTreatmentRuntime.RuneConductorItemId,
                2,
                new Vector2Int(4, 5)),
            Snapshot(
                manaStack,
                PhysicalAgeTreatmentRuntime.ManaCrystalItemId,
                2,
                new Vector2Int(4, 6)));
        PhysicalItemBatchDispositionService dispositions = new(
            repository,
            new FixedMassQuery(new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [PhysicalAgeTreatmentRuntime.RuneConductorItemId] = 700L,
                [PhysicalAgeTreatmentRuntime.ManaCrystalItemId] = 500L
            }),
            EditorNullItemMarkerPresenter.Instance);
        PhysicalFacilityItemSinkGateway baseGateway = new(stock, dispositions);
        string operationId = PhysicalAgeTreatmentRuntime.FormatOperationId(
            new CharacterId(Character),
            1);
        Require(
            baseGateway.TryCommitSinkPending(
                Facility,
                RequiredInputs(),
                operationId,
                PhysicalAgeTreatmentRuntime.DispositionReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "Two-input maintenance Sink failed: " + commitFailure);
        Require(
            receipt.Quantity == 2 && receipt.InputMassGrams == 1_200L,
            "Maintenance receipt did not preserve exact total quantity/mass.");

        PhysicalAgeTreatmentRuntime faulted = CreateRuntime(
            life,
            new FailFirstAcknowledgeBatchGateway(baseGateway));
        Require(
            !faulted.TryRecoverPending(out DomainFailure failure)
            && failure.Code
                == FailureCode.TemporalStasisMaintenanceUnavailable,
            "Injected maintenance acknowledgement failure did not fail-loud.");

        CharacterLifeWorldSaveData published = life.Capture();
        CharacterLifeRecordSaveData publishedRecord = published.characters.Single();
        Require(
            (TemporalStasisMaintenanceCommitPhase)
                published.pendingTemporalStasisMaintenance.phase
                == TemporalStasisMaintenanceCommitPhase.OutcomePublished
            && published.nextTemporalStasisMaintenanceOperationSequence == 1
            && publishedRecord.temporalStasisNextMaintenanceAbsoluteDay
                == 1 + GameCalendarRules.DaysPerSeason
            && published.pendingTemporalStasisMaintenance.inputQuantity == 2
            && published.pendingTemporalStasisMaintenance.inputMassGrams
                == 1_200L
            && repository.GetEditorTestQuantity(runeStack) == 1
            && repository.GetEditorTestQuantity(manaStack) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Maintenance outcome was not durable before acknowledgement.");

        PhysicalAgeTreatmentRuntime recovered = CreateRuntime(life, baseGateway);
        Require(
            recovered.TryRecoverPending(out failure),
            "Maintenance recovery failed: " + failure);
        CharacterLifeWorldSaveData terminal = life.Capture();
        Require(
            terminal.nextTemporalStasisMaintenanceOperationSequence == 2
            && (TemporalStasisMaintenanceCommitPhase)
                terminal.pendingTemporalStasisMaintenance.phase
                == TemporalStasisMaintenanceCommitPhase.None
            && repository.GetEditorTestQuantity(runeStack) == 1
            && repository.GetEditorTestQuantity(manaStack) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "Maintenance recovery repeated the Sink or did not clear provenance.");

        Require(
            recovered.TryRecoverPending(out failure)
            && repository.GetEditorTestQuantity(runeStack) == 1
            && repository.GetEditorTestQuantity(manaStack) == 1,
            "Terminal maintenance replay consumed materials twice.");
    }

    private static void VerifyMissingSecondInputDoesNotPartiallyConsume()
    {
        WorldItemRepository repository = CreateRepository();
        string runeStack = AddFacilityStack(
            repository,
            PhysicalAgeTreatmentRuntime.RuneConductorItemId,
            1,
            new Vector2Int(7, 8));
        PhysicalItemBatchDispositionService dispositions = new(
            repository,
            new FixedMassQuery(new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [PhysicalAgeTreatmentRuntime.RuneConductorItemId] = 700L,
                [PhysicalAgeTreatmentRuntime.ManaCrystalItemId] = 500L
            }),
            EditorNullItemMarkerPresenter.Instance);
        PhysicalFacilityItemSinkGateway gateway = new(
            new FixedStockQuery(Snapshot(
                runeStack,
                PhysicalAgeTreatmentRuntime.RuneConductorItemId,
                1,
                new Vector2Int(7, 8))),
            dispositions);

        Require(
            !gateway.TryCommitSinkPending(
                Facility,
                RequiredInputs(),
                "temporal-stasis-maintenance:qa:missing-second-input",
                PhysicalAgeTreatmentRuntime.DispositionReasonCode,
                out _,
                out _)
            && repository.GetEditorTestQuantity(runeStack) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "Missing mana crystal partially consumed the rune conductor.");
    }

    private static void VerifyUncommittedIntentClearsWithoutSequenceAdvance()
    {
        CharacterLifeRuntime life = CreateLifeRuntime();
        CharacterLifeWorldSaveData intent = life.Capture();
        intent.pendingTemporalStasisMaintenance = CreateIntent();
        life.PublishRestore(life.PrepareRestore(intent));
        PhysicalAgeTreatmentRuntime runtime = CreateRuntime(
            life,
            new PhysicalFacilityItemSinkGateway(
                new FixedStockQuery(),
                new PhysicalItemBatchDispositionService(
                    CreateRepository(),
                    new FixedMassQuery(
                        new Dictionary<string, long>(StringComparer.Ordinal)),
                    EditorNullItemMarkerPresenter.Instance)));

        Require(
            runtime.TryRecoverPending(out DomainFailure failure),
            "Uncommitted maintenance intent recovery failed: " + failure);
        CharacterLifeWorldSaveData recovered = life.Capture();
        Require(
            recovered.nextTemporalStasisMaintenanceOperationSequence == 1
            && (TemporalStasisMaintenanceCommitPhase)
                recovered.pendingTemporalStasisMaintenance.phase
                == TemporalStasisMaintenanceCommitPhase.None
            && recovered.characters.Single()
                .temporalStasisNextMaintenanceAbsoluteDay == 1,
            "Uncommitted maintenance intent advanced sequence or life state.");
    }

    private static PhysicalAgeTreatmentRuntime CreateRuntime(
        CharacterLifeRuntime life,
        IPhysicalFacilityItemBatchSinkGateway gateway) => new(
        gateway,
        life,
        life,
        life,
        new FixedCalendar(1),
        EmptyBuildingWorldQuery.Instance,
        EmptyPowerQuery.Instance);

    private static CharacterLifeRuntime CreateLifeRuntime()
    {
        DungeonRuntimeAggregateRootStore root = new();
        CharacterLifeRuntime life = new(
            root,
            FixedLifeDefinitions.Instance,
            new RandomStreamProvider(27));
        CharacterId characterId = new(Character);
        life.Register(
            characterId,
            FixedLifeDefinitions.SpeciesId,
            chronologicalAgeDays: 20 * GameCalendarRules.DaysPerYear,
            biologicalAgeDayUnits: 20 * GameCalendarRules.DaysPerYear,
            birthdayDayOfYear: 1);
        life.ConfigureTemporalStasis(
            characterId,
            Facility,
            operational: true,
            nextMaintenanceAbsoluteDay: 1);
        return life;
    }

    private static TemporalStasisMaintenanceCommitSaveData CreateIntent() =>
        new()
        {
            phase = (int)TemporalStasisMaintenanceCommitPhase.IntentRecorded,
            operationSequence = 1,
            operationId = PhysicalAgeTreatmentRuntime.FormatOperationId(
                new CharacterId(Character),
                1),
            reasonCode = PhysicalAgeTreatmentRuntime.DispositionReasonCode,
            characterId = Character,
            facilityInstanceId = Facility,
            runeConductorItemId =
                PhysicalAgeTreatmentRuntime.RuneConductorItemId,
            runeConductorQuantity = 1,
            manaCrystalItemId = PhysicalAgeTreatmentRuntime.ManaCrystalItemId,
            manaCrystalQuantity = 1,
            nextMaintenanceBeforeAbsoluteDay = 1,
            nextMaintenanceAfterAbsoluteDay =
                1 + GameCalendarRules.DaysPerSeason
        };

    private static IReadOnlyDictionary<string, int> RequiredInputs() =>
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [PhysicalAgeTreatmentRuntime.RuneConductorItemId] = 1,
            [PhysicalAgeTreatmentRuntime.ManaCrystalItemId] = 1
        };

    private static WorldItemRepository CreateRepository() => new(
        new GuidPersistentIdGenerator(),
        new DungeonRuntimeAggregateRootStore());

    private static string AddFacilityStack(
        WorldItemRepository repository,
        string itemId,
        int quantity,
        Vector2Int position) => WorldItemRepositoryEditorAccess.AddStack(
        repository,
        itemId,
        quantity,
        WorldItemStackState.FacilityBuffer,
        destinationId: Facility,
        position: position);

    private static WorldItemStackSnapshot Snapshot(
        string stackId,
        string itemId,
        int quantity,
        Vector2Int position) => new()
    {
        StackId = stackId,
        ItemId = itemId,
        Quantity = quantity,
        State = WorldItemStackState.FacilityBuffer,
        Position = position,
        DestinationId = Facility,
        ReservedByPersistentId = string.Empty
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedLifeDefinitions : ICharacterLifeDefinitionCatalog
    {
        internal static readonly FixedLifeDefinitions Instance = new();
        internal static readonly CharacterSpeciesId SpeciesId =
            new("species:qa:temporal-stasis");
        private static readonly SpeciesLifeHistoryDefinition History = new(
            SpeciesId,
            infantEndAgeYears: 2,
            adolescentStartAgeYears: 10,
            adultAgeYears: 18,
            elderAgeYears: 60,
            untreatedExpectedLifeYears: 80f,
            construct: false);
        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) => speciesId.Equals(SpeciesId)
            ? History
            : throw new KeyNotFoundException(speciesId.Value);
        public AgeConditionDefinition RequireAgeCondition(string conditionId) =>
            throw new KeyNotFoundException(conditionId);
        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(
            bool construct) => Array.Empty<AgeConditionDefinition>();
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
        private readonly IReadOnlyDictionary<string, long> gramsByItem;
        internal FixedMassQuery(IReadOnlyDictionary<string, long> gramsByItem) =>
            this.gramsByItem = gramsByItem;
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(gramsByItem.TryGetValue(itemId.Value, out long grams)
                ? grams
                : 1L);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(itemId);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetDefinitionUnitMass(lot.Subject.ItemId).Multiply(lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => GetDefinitionUnitMass(itemId).Multiply(quantity);
    }

    private sealed class FailFirstAcknowledgeBatchGateway :
        IPhysicalFacilityItemBatchSinkGateway
    {
        private readonly IPhysicalFacilityItemBatchSinkGateway inner;
        private bool rejected;
        internal FailFirstAcknowledgeBatchGateway(
            IPhysicalFacilityItemBatchSinkGateway inner) => this.inner = inner;
        public bool TryCommitSinkPending(
            string destinationId,
            IReadOnlyDictionary<string, int> itemQuantities,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitSinkPending(
            destinationId,
            itemQuantities,
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

    private sealed class FixedCalendar : IGameCalendar
    {
        internal FixedCalendar(int day) => Day = day;
        public int Day { get; private set; }
        public int Hour => 0;
        public int Year => 1;
        public int DayOfYear => Day;
        public Season Season => Season.Spring;
        public int DayOfSeason => Day;
        public long AbsoluteHour => (long)Day * 24L;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Morning;
        public bool IsRunning => true;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) => Current;
        public void Start() { }
        public void SetDateTime(int day, int hour) => Day = day;
    }

    private sealed class EmptyBuildingWorldQuery : IBuildingWorldQuery
    {
        internal static readonly EmptyBuildingWorldQuery Instance = new();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings { get; } =
            Array.Empty<BuildableObject>();
    }

    private sealed class EmptyPowerQuery : IPowerInfrastructureQuery
    {
        internal static readonly EmptyPowerQuery Instance = new();
        public int Version => 0;
        public IReadOnlyList<PowerNetworkSnapshot> Networks { get; } =
            Array.Empty<PowerNetworkSnapshot>();
        public bool IsPowered(BuildableObject building) => false;
        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
    }
}
#endif
