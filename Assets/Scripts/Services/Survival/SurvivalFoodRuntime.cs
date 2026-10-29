using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public interface ISurvivalRefuelCompletionCommand
{
    bool TryEnsureRefuelSupply(
        IBuildingVisitorPort actor,
        BuildableObject building,
        out bool completionOnly,
        out DomainFailure failure);

    bool TryApplyRefuelWork(
        IBuildingVisitorPort actor,
        BuildableObject building,
        out int amount,
        out DomainFailure failure);
}

public readonly struct SurvivalFacilityFuelSupplyPlan
{
    public SurvivalFacilityFuelSupplyPlan(
        string destinationId,
        string itemId,
        int requiredQuantity)
    {
        DestinationId = destinationId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        RequiredQuantity = requiredQuantity;
    }

    public string DestinationId { get; }
    public string ItemId { get; }
    public int RequiredQuantity { get; }
}

public interface ISurvivalRefuelSupplyQuery
{
    bool TryGetRefuelSupplyPlan(
        BuildableObject building,
        out SurvivalFacilityFuelSupplyPlan plan);
}

public interface ISurvivalFacilityFuelRetirement
{
    bool TryPrepareFacilityFuelRetirement(
        BuildableObject building,
        out string failureReason);
}

public sealed partial class SurvivalFoodRuntime :
    ISurvivalFoodQuery,
    ISurvivalFoodCommand,
    ISurvivalFoodPersistence,
    ISurvivalFoodDebugCommand,
    ICharacterNutritionRuntime,
    ISurvivalEnvironmentQuery,
    ISurvivalStorageEnvironmentSink,
    ISurvivalTreatmentCompletionCommand,
    ISurvivalRefuelCompletionCommand,
    ISurvivalRefuelSupplyQuery,
    ISurvivalFacilityFuelRetirement,
    ISurvivalTreatmentTerminalMaintenance,
    ISurvivalTreatmentSupplyQuery,
    IInitializable,
    ITickable,
    IDungeonSaveRestoreCompletedHook,
    IDisposable
{
    public const string TreatmentPhysicalSinkReason = SurvivalTreatmentIdentity.TreatmentPhysicalSinkReason;
    private const string CleanWaterItemId = "resource:clean-water";
    private const string CookedMealItemId = "survival:cooked_meal";
    private const string PreservedFoodItemId = "survival:preserved_food";

    private readonly IUiClock uiClock;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IGameCalendar calendar;
    private readonly ICharacterConsumablesApplication consumables;
    private readonly IClimateQuery climate;
    private readonly IClimateDefinitionCatalog climateDefinitions;
    private readonly SurvivalTreatmentRuntime treatment;
    private readonly SurvivalFoodStockRuntime stockRuntime;
    private readonly SurvivalFoodSpoilageRuntime spoilageRuntime;
    private readonly SurvivalFoodOverviewCache overviewCache;
    private readonly SurvivalMealLedger mealLedger;
    private readonly SurvivalEnvironmentRiskEvaluator environmentRisks;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IDomainFactPublisher mealMissedFacts;
    private IDomainFactRecoveryPublisher mealMissedRecovery;
    private readonly Queue<int> pendingMealMissedDays = new();
    private readonly HashSet<int> queuedMealMissedDays = new();
    private float nextMealMissedRecoveryTime;
    public string LastMealMissedRecoveryFailure { get; private set; } = string.Empty;
    private IDisposable operatingDayStartedSubscription;
    private IDisposable stockConsumedSubscription;
    private IDisposable physicalMealConsumedSubscription;

    private SurvivalWeatherType? debugWeatherOverride;
#if UNITY_EDITOR
    private bool debugIsolatedDailyNutritionHeld;
    private int debugHeldDailyNutritionChecks;
    public bool DebugIsolatedDailyNutritionHeld => debugIsolatedDailyNutritionHeld;
    public int DebugHeldDailyNutritionChecks => debugHeldDailyNutritionChecks;

    // Test preparation only: skip the daily meal/shortage calculation without
    // fabricating meals, changing health, stock, ledger or the saved frontier.
    // No such control exists in a player build or an interactive Editor.
    public IDisposable DebugHoldDailyNutritionForIsolatedPreparation()
    {
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification)
            throw new InvalidOperationException("Daily nutrition hold requires isolated batch test preparation.");
        if (debugIsolatedDailyNutritionHeld)
            throw new InvalidOperationException("Daily nutrition preparation already has an owner.");
        debugIsolatedDailyNutritionHeld = true;
        debugHeldDailyNutritionChecks = 0;
        return new IsolatedDailyNutritionHold(this);
    }

    private sealed class IsolatedDailyNutritionHold : IDisposable
    {
        private SurvivalFoodRuntime owner;
        public IsolatedDailyNutritionHold(SurvivalFoodRuntime owner) => this.owner = owner;
        public void Dispose()
        {
            if (owner == null) return;
            owner.debugIsolatedDailyNutritionHeld = false;
            owner = null;
        }
    }
#endif
    private SurvivalFoodAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new SurvivalFoodAggregateState());
    private DungeonSurvivalSaveData state => aggregateState.Data;
    private long mealSequence
    {
        get => aggregateState.MealSequence;
        set => aggregateState.MealSequence = value;
    }

    public SurvivalFoodRuntime(
        IUiClock uiClock,
        SurvivalFoodRuntimeDependencies dependencies,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IGameEventBus gameEventBus,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        IGameCalendar calendar,
        IWorldThreatModifierQuery worldThreatModifiers,
        ISurvivalServiceSessionCapability serviceSessionRuntime,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterCarryInventoryRegistry carryInventories = null,
        ICharacterConsumablesApplication consumables = null)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        _ = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        this.speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.itemStackRuntime = dependencies.ItemStackRuntime;
        this.itemCatalog = dependencies.ItemCatalog;
        climate = dependencies.Climate;
        climateDefinitions = dependencies.ClimateDefinitions;
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        worldThreatModifiers = worldThreatModifiers
            ?? throw new ArgumentNullException(nameof(worldThreatModifiers));
        ISurvivalServiceSessionCapability treatmentSessions = serviceSessionRuntime
            ?? throw new ArgumentNullException(nameof(serviceSessionRuntime));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.consumables = consumables;
        stockRuntime = new SurvivalFoodStockRuntime(
            dependencies.GridSystemProvider,
            this.worldRegistry,
            this.itemStackRuntime,
            this.itemCatalog,
            dependencies.StockQuery,
            dependencies.PhysicalSinks,
            dependencies.PackagedTare,
            dependencies.Workforce,
            dependencies.TreatmentBufferCapacities,
            dependencies.TreatmentBufferOccupancy,
            dependencies.TreatmentBufferClaims,
            dependencies.FuelBufferLifecycle,
            dependencies.FuelBufferRelease,
            this.aggregateRootStore);
        spoilageRuntime = new SurvivalFoodSpoilageRuntime(
            this.itemStackRuntime,
            this.itemCatalog,
            stockRuntime,
            carryInventories);
        overviewCache = new SurvivalFoodOverviewCache(
            this.gameClock,
            this.worldRegistry,
            this.itemStackRuntime);
        mealLedger = new SurvivalMealLedger(this.gameEventBus);
        environmentRisks = new SurvivalEnvironmentRiskEvaluator(
            dependencies.GridSystemProvider,
            this.worldRegistry,
            worldThreatModifiers);
        treatment = new SurvivalTreatmentRuntime(
            this.worldRegistry, stockRuntime, treatmentSessions, this.consumables,
            this.gameEventBus, this.aggregateRootStore);
    }

    [Inject]
    public void ConstructMealMissedFacts(IDomainFactPublisher facts)
    {
        mealMissedFacts = facts ?? throw new ArgumentNullException(nameof(facts));
        mealMissedRecovery = facts as IDomainFactRecoveryPublisher
            ?? throw new ArgumentException("Missed meals require durable cohort recovery.", nameof(facts));
    }

    public int GetStoredStockCount(StockCategory category)
    {
        return stockRuntime.CountStoredStock(category);
    }

    public void ConfigureStorageEnvironment(
        IEnvironmentalFieldQuery fieldQuery)
    {
        spoilageRuntime.ConfigureStorageEnvironment(fieldQuery);
    }

    public bool HasFuelSupply(BuildableObject building) =>
        SurvivalFacilityWorkRules.IsEnvironmentalFuelConsumer(building)
        && building.HasFacilityFuelSupply;

    public float GetRemainingFuelGameSeconds(BuildableObject building) =>
        SurvivalFacilityWorkRules.IsEnvironmentalFuelConsumer(building)
            ? Mathf.Max(0f, building.FacilityState.remainingFuelGameSeconds)
            : 0f;

    public bool TryGetRefuelSupplyPlan(
        BuildableObject building,
        out SurvivalFacilityFuelSupplyPlan plan)
    {
        if (stockRuntime.TryGetFacilityFuelSupplyPlan(
                building,
                out string destinationId,
                out string itemId,
                out int requiredQuantity))
        {
            plan = new SurvivalFacilityFuelSupplyPlan(
                destinationId,
                itemId,
                requiredQuantity);
            return true;
        }

        plan = default;
        return false;
    }

    public SurvivalEnvironmentSnapshot GetEnvironmentSnapshot()
    {
        return environmentRisks.GetSnapshot(
            state,
            CurrentWeather,
            CurrentOutdoorTemperature);
    }

    public int TryConsumeStoredStock(StockCategory category, int amount)
    {
        return stockRuntime.WithdrawStock(category, Mathf.Max(0, amount));
    }

    public void Initialize()
    {
        stockRuntime.TryRefreshFacilityFuelAuthorities();
        if (!stockRuntime.TryRecoverFacilityFuelCommits(
                out string fuelRecoveryFailure))
        {
            throw new InvalidOperationException(
                "Facility fuel recovery failed: " + fuelRecoveryFailure);
        }
        operatingDayStartedSubscription ??=
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
        stockConsumedSubscription ??=
            gameEventBus.Subscribe<FacilityStockConsumedEvent>(OnTriggerEvent);
        physicalMealConsumedSubscription ??=
            gameEventBus.Subscribe<PhysicalMealConsumedEvent>(OnTriggerEvent);
        OnRestoreCompleted();
    }

    public void Dispose()
    {
#if UNITY_EDITOR
        debugIsolatedDailyNutritionHeld = false;
#endif
        operatingDayStartedSubscription?.Dispose();
        operatingDayStartedSubscription = null;
        stockConsumedSubscription?.Dispose();
        stockConsumedSubscription = null;
        physicalMealConsumedSubscription?.Dispose();
        physicalMealConsumedSubscription = null;
        pendingMealMissedDays.Clear();
        queuedMealMissedDays.Clear();
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
#if UNITY_EDITOR
        if (debugIsolatedDailyNutritionHeld)
        {
            debugHeldDailyNutritionChecks++;
            return;
        }
#endif
        if (eventType.day <= 0 || state.lastProcessedDay >= eventType.day)
        {
            return;
        }

        ProcessDailySurvival(eventType.day);
    }

    public void OnTriggerEvent(FacilityStockConsumedEvent eventType)
    {
        CharacterActor consumer = eventType.consumerActor;
        BuildableObject facility = eventType.facility;
        if (consumer == null
            || facility == null
            || eventType.category != StockCategory.Food
            || eventType.amount <= 0
            || !facility.SupportsFacilityRole(FacilityRole.Meal)
            || !IsSurvivalConsumer(consumer))
        {
            return;
        }

        RecordMeal(
            consumer,
            facility.RequirePersistentInstanceId(),
            itemId: string.Empty,
            displayName: string.Empty,
            dietClass: MealDietClass.Vegan,
            quality: MealQualityTier.Simple,
            nutrition: 0f,
            policyViolation: false,
            contaminated: false);
    }

    public void OnTriggerEvent(PhysicalMealConsumedEvent eventType)
    {
        if (eventType.Actor == null
            || !eventType.FacilityId.IsValid
            || eventType.Facility == null && !string.Equals(
                eventType.FacilityId.Value,
                CharacterConsumablesRuntime.FieldMealFacilityId,
                StringComparison.Ordinal)
            || !eventType.Result.Success
            || !IsSurvivalConsumer(eventType.Actor))
        {
            return;
        }

        RecordMeal(
            eventType.Actor,
            eventType.FacilityId,
            eventType.Result.ItemId,
            eventType.Result.DisplayName,
            eventType.Result.DietClass,
            eventType.Result.Quality,
            eventType.Result.Nutrition,
            eventType.Result.PolicyViolation,
            eventType.Result.Contaminated);
    }

    private void ProcessDailySurvival(int day)
    {
        EnsureStateLists();
        consumables?.ProcessOperatingDay(day);
        PublishWeatherSample();
        spoilageRuntime.Process(
            state,
            CurrentWeather,
            advanceTime: true);
        PublishMissedMealFactsAndRefreshFoodForecast(day);
        ConsumeDailyWater(day);
        RefreshSurvivalRisks();
        ApplyHealthConsequences();
        InvalidateOverviewCache();
    }

    public DungeonSurvivalSaveData Capture()
    {
        return SurvivalFoodStatePersistence.Capture(state);
    }

    public void DebugSetWeather(SurvivalWeatherType weather)
    {
        if (!Enum.IsDefined(typeof(SurvivalWeatherType), weather))
        {
            throw new ArgumentOutOfRangeException(nameof(weather));
        }

        debugWeatherOverride = weather;
        RefreshSurvivalRisks();
        InvalidateOverviewCache();
    }

    public void DebugAdvanceSpoilage(float seconds)
    {
        spoilageRuntime.DebugAdvance(state, seconds);
        InvalidateOverviewCache();
    }

    public void DebugResetSpoilage()
    {
        spoilageRuntime.DebugReset(state);
        InvalidateOverviewCache();
    }

    public SurvivalFoodRestoreCandidate BuildRestoreCandidate(
        DungeonSurvivalSaveData saveData)
    {
        DungeonGameRestoreReport validation = new();
        ValidateRestorePayload(saveData, validation);
        if (!validation.Success)
        {
            throw new InvalidOperationException(
                "Survival resources restore rejected an invalid current candidate: "
                + string.Join(" | ", validation.Errors));
        }

        DungeonSurvivalSaveData restored =
            SurvivalFoodStatePersistence.Restore(saveData);
        return new SurvivalFoodRestoreCandidate(
            new SurvivalFoodAggregateState
            {
                Data = restored,
                MealSequence = SurvivalFoodStatePersistence.GetMealSequence(restored)
            });
    }

    public void PublishRestoreCandidate(
        SurvivalFoodRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
        treatment.ClearWorkOperationsAfterRestore();
        stockRuntime.InvalidateFacilityFuelAuthorities();
        stockRuntime.TryRefreshFacilityFuelAuthorities();
        if (!stockRuntime.TryRecoverFacilityFuelCommits(
                out string fuelRecoveryFailure))
        {
            throw new InvalidOperationException(
                "Facility fuel restore recovery failed: "
                + fuelRecoveryFailure);
        }
        InvalidateOverviewCache();
    }

    public void ValidateRestorePayload(
        DungeonSurvivalSaveData payload,
        DungeonGameRestoreReport report) =>
        SurvivalFoodStatePersistence.Validate(payload, report, itemCatalog);

    public SurvivalFoodOverview GetOverview()
    {
        return overviewCache.GetOrCreate(BuildOverview);
    }

    private SurvivalFoodOverview BuildOverview()
    {
        EnsureStateLists();
        spoilageRuntime.Process(state, CurrentWeather);
        RefreshSurvivalRisks();

        int required = CountSurvivalConsumers();
        int stored = CountStoredFood();
        int looseFood = CountLooseFood();
        int carcasses = CountCarcasses(out int pendingFood);
        int shortageDays = required <= 0
            ? int.MaxValue
            : Mathf.FloorToInt((stored + looseFood + pendingFood) / (float)required);
        int storedWater = stockRuntime.CountStoredStock(StockCategory.Water);
        int looseWater = stockRuntime.CountLooseStock(StockCategory.Water);
        int storedFuel = stockRuntime.CountStoredStock(StockCategory.Fuel);
        int storedMedicine = stockRuntime.CountStoredStock(StockCategory.Medicine);
        int sickCount = 0;
        int untreatedCount = 0;
        IReadOnlyList<SurvivalHealthSaveData> healthEntries =
            state.health;
        for (int index = 0; index < healthEntries.Count; index++)
        {
            SurvivalHealthSaveData entry = healthEntries[index];
            if (entry == null
                || entry.remainingSeconds <= 0f
                || entry.state is not (
                    SurvivalHealthState.Sick
                    or SurvivalHealthState.Infected))
            {
                continue;
            }

            sickCount++;
            if (entry.severity >= 0.5f)
            {
                untreatedCount++;
            }
        }
        int spoilageWarnings = spoilageRuntime.CountWarnings(state);
        return new SurvivalFoodOverview(
            required,
            stored,
            looseFood,
            carcasses,
            pendingFood,
            shortageDays,
            required,
            storedWater,
            looseWater,
            storedFuel,
            storedMedicine,
            spoilageWarnings,
            CurrentWeather,
            CurrentOutdoorTemperature,
            state.sanitationRisk,
            state.diseaseRisk,
            state.exteriorNightDanger,
            sickCount,
            untreatedCount);
    }

    private void InvalidateOverviewCache()
    {
        overviewCache.Invalidate();
    }

    public bool TryGetItemStatus(string stackId, string itemId, out SurvivalItemStatus status)
    {
        return spoilageRuntime.TryGetItemStatus(state, stackId, itemId, out status);
    }

    public bool TryGetCharacterStatus(CharacterActor actor, out SurvivalCharacterStatus status)
    {
        return SurvivalHealthStateRules.TryGetStatus(
            state,
            actor,
            environmentRisks.GetEffectiveOutdoorTemperature(
                CurrentOutdoorTemperature),
            out status);
    }

    public bool TryApplySurvivalWork(
        IBuildingVisitorPort actor,
        BuildableObject building,
        WorkTypeId workTypeId,
        out int amount,
        out DomainFailure failure)
    {
        EnsureStateLists();
        amount = 0;
        if (building == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTargetFacilityMissing);
            return false;
        }

        switch (workTypeId)
        {
            case var id when id == BuiltInWorkTypeIds.DrawWater:
                return TryApplyDrawWater(actor, building, out amount, out failure);
            case var id when id == BuiltInWorkTypeIds.Cook:
                return TryApplyCook(actor, building, out amount, out failure);
            case var id when id == BuiltInWorkTypeIds.Treat:
                return treatment.TryApplyTreat(
                    actor,
                    building,
                    default,
                    out _,
                    out amount,
                    out failure);
            case var id when id == BuiltInWorkTypeIds.Refuel:
                return TryApplyRefuelWork(
                    actor,
                    building,
                    out amount,
                    out failure);
            default:
                failure = new DomainFailure(
                    FailureCode.SurvivalWorkUnsupported,
                    workTypeId.Value);
                return false;
        }
    }

    public bool HasSurvivalWorkAvailable(BuildableObject building, WorkTypeId workTypeId)
    {
        if (building?.BuildingData == null || building.isDestroy)
        {
            return false;
        }

        return workTypeId switch
        {
            var id when id == BuiltInWorkTypeIds.DrawWater => building.BuildingData.GetAbility<BuildingWaterSourceAbility>() != null
                && CanDrawWater(building),
            var id when id == BuiltInWorkTypeIds.Cook => building.BuildingData.GetAbility<BuildingCookingAbility>() is { } cooking
                && stockRuntime.CountStoredStock(StockCategory.Food) >= Mathf.Max(1, cooking.inputFood)
                && (!cooking.requiresFuel || stockRuntime.CountStoredStock(StockCategory.Fuel) > 0),
            var id when id == BuiltInWorkTypeIds.Treat =>
                treatment.HasAvailableTreatment(building),
            var id when id == BuiltInWorkTypeIds.Refuel =>
                SurvivalFacilityWorkRules.IsEnvironmentalFuelConsumer(building)
                && !building.HasFacilityFuelSupply
                && stockRuntime.HasPotentialFacilityFuel(building),
            _ => false
        };
    }

    public float GetSurvivalWorkUrgency(BuildableObject building, WorkTypeId workTypeId)
    {
        if (building == null || !HasSurvivalWorkAvailable(building, workTypeId))
        {
            return 0f;
        }

        SurvivalFoodOverview overview = GetOverview();
        return workTypeId switch
        {
            var id when id == BuiltInWorkTypeIds.DrawWater => Mathf.Clamp(80f - (overview.WaterShortageDays * 15f), 10f, 90f)
                + (state.lastMissingWater > 0 ? 25f : 0f),
            var id when id == BuiltInWorkTypeIds.Cook => Mathf.Clamp(70f - (overview.ShortageDays * 12f), 8f, 80f)
                + (overview.SpoilageWarningCount > 0 ? 15f : 0f),
            var id when id == BuiltInWorkTypeIds.Treat => 35f
                + (overview.UntreatedCount * 25f)
                + Mathf.Clamp(overview.DiseaseRisk * 0.35f, 0f, 35f)
                + Mathf.Clamp(
                    treatment.GetTreatmentToxicityUrgency(building),
                    0f,
                    35f),
            var id when id == BuiltInWorkTypeIds.Refuel => CurrentWeather == SurvivalWeatherType.ColdSnap
                ? 75f
                : Mathf.Clamp(overview.ExteriorNightDanger * 0.45f, 10f, 55f),
            _ => 0f
        };
    }

    public bool TryEnsureRefuelSupply(
        IBuildingVisitorPort actor,
        BuildableObject building,
        out bool completionOnly,
        out DomainFailure failure) => stockRuntime.TryEnsureFacilityFuelSupply(
        actor,
        building,
        out completionOnly,
        out failure);

    public bool TryApplyRefuelWork(
        IBuildingVisitorPort actor,
        BuildableObject building,
        out int amount,
        out DomainFailure failure)
    {
        if (!stockRuntime.TryCommitFacilityFuel(building, out amount, out failure))
            return false;
        if (amount > 0)
            PublishWorkResult(new SurvivalFacilityWorkResultEvent(
                SurvivalFacilityWorkResultKind.Refueling, actor, building, amount));
        failure = DomainFailure.None;
        return true;
    }

    public bool TryPrepareFacilityFuelRetirement(
        BuildableObject building,
        out string failureReason) => stockRuntime
        .TryPrepareFacilityFuelRetirement(building, out failureReason);

    public int GetMealsConsumed(int day)
    {
        return mealLedger.GetConsumed(state, day);
    }

    public int GetMealsConsumed(string characterId, int day)
    {
        return mealLedger.GetConsumed(state, characterId, day);
    }

    public IReadOnlyList<CharacterMealLedgerSaveData> GetRecentMeals(int maximumCount = 30)
    {
        return mealLedger.GetRecent(state, maximumCount);
    }

    private void RefreshDailyFoodForecast(int day)
    {
        if (state.lastProcessedDay > 0 && state.lastProcessedDay != day)
        {
            int previousConsumed = GetMealsConsumed(state.lastProcessedDay);
            state.consecutiveFoodShortageDays =
                previousConsumed < state.lastNeededFood
                    ? state.consecutiveFoodShortageDays + 1
                    : 0;
        }

        state.lastProcessedDay = day;
        RefreshCurrentDayFoodSummary();
    }

    private void RefreshCurrentDayFoodSummary()
    {
        int need = CountSurvivalConsumers();
        int consumed = GetMealsConsumed(state.lastProcessedDay);
        state.lastNeededFood = need;
        state.lastConsumedFood = consumed;
        state.lastMissingFood = Mathf.Max(0, need - consumed);
    }

    private IEnumerable<CharacterActor> GetSurvivalConsumers()
        => SurvivalConsumerSelection.GetSurvivalConsumers(worldRegistry);

    private int CountSurvivalConsumers()
        => SurvivalConsumerSelection.CountSurvivalConsumers(worldRegistry);

    private static bool IsSurvivalConsumer(CharacterActor actor)
        => SurvivalConsumerSelection.IsSurvivalConsumer(actor);

    private int CountStoredFood()
    {
        return stockRuntime.CountStoredStock(StockCategory.Food);
    }

    private int CountLooseFood()
    {
        return stockRuntime.CountLooseStock(StockCategory.Food);
    }

    private int CountCarcasses(out int pendingFood)
    {
        pendingFood = 0;
        int count = 0;
        IReadOnlyList<WorldItemStackSnapshot> stacks =
            stockRuntime.GetCachedItemStacks();
        for (int stackIndex = 0;
            stackIndex < stacks.Count;
            stackIndex++)
        {
            WorldItemStackSnapshot stack = stacks[stackIndex];
            if (stack == null
                || !WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(stack.ItemId, out string speciesId))
            {
                continue;
            }

            count++;
            if (speciesCatalog.TryGetSpecies(speciesId, out WildlifeSpeciesDefinition species))
            {
                IReadOnlyList<WildlifeButcherYield> yields =
                    species.ButcherYields;
                for (int yieldIndex = 0;
                    yieldIndex < yields.Count;
                    yieldIndex++)
                {
                    WildlifeButcherYield yieldItem = yields[yieldIndex];
                    if (yieldItem != null
                        && itemCatalog.TryGet(
                            (ItemDefinitionId)yieldItem.itemId,
                            out ItemDefinitionSO yieldDefinition)
                        && yieldDefinition.StockCategory == StockCategory.Food)
                    {
                        pendingFood += yieldItem.amount;
                    }
                }
            }
        }

        return count;
    }

    private void EnsureStateLists()
    {
        aggregateState.Data ??= new DungeonSurvivalSaveData();
        SurvivalFoodStatePersistence.EnsureLists(state);
    }

    private void RecordMeal(
        CharacterActor consumer,
        BuildingInstanceId facilityId,
        string itemId,
        string displayName,
        MealDietClass dietClass,
        MealQualityTier quality,
        float nutrition,
        bool policyViolation,
        bool contaminated)
    {
        bool unfamiliar = !string.IsNullOrWhiteSpace(itemId)
            && !state.mealLedger.Any(entry => entry != null
                && string.Equals(
                    entry.characterId,
                    consumer.Identity?.PersistentId,
                    StringComparison.Ordinal)
                && string.Equals(entry.itemId, itemId, StringComparison.Ordinal));
        long nextMealSequence = mealSequence;
        mealLedger.Record(
            state,
            ref nextMealSequence,
            consumer,
            facilityId,
            itemId,
            displayName,
            dietClass,
            quality,
            nutrition,
            policyViolation,
            contaminated);
        mealSequence = nextMealSequence;
        if (CharacterPersistentIdentity.TryGet(consumer, out CharacterId characterId))
        {
            string[] mealTags = ResolveMealIdentityTags(
                itemId,
                quality,
                unfamiliar);
            gameEventBus.Publish(new MealConsumedEvent(
                characterId,
                string.IsNullOrWhiteSpace(itemId) ? "meal:facility-stock" : itemId,
                mealTags,
                wasSufficient: true,
                CharacterCommandOrigin.Autonomous,
                calendar.Day));
        }
        RefreshCurrentDayFoodSummary();
        InvalidateOverviewCache();
    }

    private string[] ResolveMealIdentityTags(
        string itemId,
        MealQualityTier quality,
        bool unfamiliar)
    {
        if (string.IsNullOrWhiteSpace(itemId)
            || !itemCatalog.TryGet(
                (ItemDefinitionId)itemId,
                out ItemDefinitionSO definition)
            || definition is not ResourceItemDefinitionSO resource)
        {
            return Array.Empty<string>();
        }

        List<string> tags = new(4);
        if (quality == MealQualityTier.Lavish)
            tags.Add("luxury");
        ResourceIngredientTag authoredTags = resource.IngredientTags;
        if ((authoredTags & ResourceIngredientTag.Sweet) != 0)
            tags.Add("sweet");
        if ((authoredTags & ResourceIngredientTag.Salted) != 0)
            tags.Add("salted");
        if (unfamiliar)
            tags.Add("unfamiliar");
        return tags.ToArray();
    }

    private void PublishMissedMealFactsAndRefreshFoodForecast(int currentDay)
    {
        int completedDay = Math.Max(0, currentDay - 1);
        if (completedDay <= 0)
        {
            RefreshDailyFoodForecast(currentDay);
            return;
        }
        string[] missed = GetSurvivalConsumers()
            .Select(actor => CharacterPersistentIdentity.TryGet(actor, out CharacterId character)
                && GetMealsConsumed(character.Value, completedDay) <= 0 ? character.Value : null)
            .Where(character => character != null).Distinct(StringComparer.Ordinal)
            .OrderBy(character => character, StringComparer.Ordinal).ToArray();
        if (missed.Length == 0)
        {
            RefreshDailyFoodForecast(currentDay);
            return;
        }
        if (mealMissedFacts == null || mealMissedRecovery == null)
            throw new InvalidOperationException("Missed-meal fact publication is unavailable.");
        if (state.missedMealCohorts.Count >= SurvivalMealMissedCohortSaveData.MaximumCohorts
            || state.missedMealCohorts.Any(row => row.absoluteDay == currentDay))
            throw new InvalidOperationException("Missed-meal day is already recorded or cohort capacity is exhausted.");
        var facts = missed.Select((character, index) => new SurvivalMealMissedFact(new SurvivalMealMissedData
        {
            characterId = character, absoluteDay = currentDay, completedDay = completedDay,
            consecutiveMisses = 1, cohortIndex = index, cohortSize = missed.Length
        })).ToArray();
        var leases = new List<DomainFactLease>(facts.Length);
        var journal = new SurvivalMealMissedCohortSaveData { absoluteDay = currentDay };
        // Only these five forecast fields are changed here. Restoring an entire
        // aggregate would also rerun unrelated fuel/health restore side effects.
        var before = (state.lastProcessedDay, state.lastNeededFood, state.lastConsumedFood,
            state.lastMissingFood, state.consecutiveFoodShortageDays);
        bool mayBeCommitted = false;
        try
        {
            foreach (var fact in facts)
            {
                if (!mealMissedFacts.TryReserve(fact, out var lease, out string failure))
                    throw new InvalidOperationException("Missed-meal fact reservation failed: " + failure);
                leases.Add(lease);
                journal.members.Add(new SurvivalMealMissedPublicationSaveData
                { fact = fact.Snapshot(), publicationKey = lease.PublicationKey });
            }
            journal.Validate();
            state.missedMealCohorts.Add(journal);
            RefreshDailyFoodForecast(currentDay);
            var results = new DomainFactDelivery[facts.Length];
            bool accepted = mealMissedFacts.PublishBatch(facts.Select((fact, index) =>
                DomainFactEmission.Create(leases[index], fact)).ToArray(), results, out string publicationFailure);
            mayBeCommitted = results.Any(result => result.MayBeCommitted);
            if (!mayBeCommitted)
                throw new InvalidOperationException("Missed-meal cohort publication rejected: " + publicationFailure);
            journal.publicationConfirmed = accepted && results.All(result => result.Completed);
            if (!journal.publicationConfirmed) QueueMissedMealDay(currentDay);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            // By publisher contract, an escaped exception is pre-commit. Once a
            // result may be committed, preserve its applied state and frozen keys.
            if (!mayBeCommitted)
            {
                foreach (var lease in leases) mealMissedFacts.Cancel(lease);
                state.missedMealCohorts.Remove(journal);
                (state.lastProcessedDay, state.lastNeededFood, state.lastConsumedFood,
                    state.lastMissingFood, state.consecutiveFoodShortageDays) = before;
                InvalidateOverviewCache();
            }
            throw;
        }
        // These live domain notifications are not reissued by publication recovery.
        foreach (var fact in facts)
        {
            var data = fact.Snapshot();
            PublishPostCommitMealMissedEvent(new MealMissedEvent(
                new CharacterId(data.characterId), data.consecutiveMisses, data.absoluteDay));
        }
    }

    public void OnRestoreCompleted()
    {
        pendingMealMissedDays.Clear();
        queuedMealMissedDays.Clear();
        nextMealMissedRecoveryTime = 0f;
        LastMealMissedRecoveryFailure = string.Empty;
        foreach (var row in state.missedMealCohorts)
            if (!row.publicationConfirmed) QueueMissedMealDay(row.absoluteDay);
    }

    public void Tick()
    {
        if (pendingMealMissedDays.Count == 0 || uiClock.Time < nextMealMissedRecoveryTime) return;
        nextMealMissedRecoveryTime = uiClock.Time + 1f;
        RecoverPendingMealMissedFacts();
    }

    public void RecoverPendingMealMissedFacts()
    {
        if (pendingMealMissedDays.Count == 0) return;
        if (mealMissedRecovery == null)
            throw new InvalidOperationException("Missed-meal fact recovery is unavailable.");
        int count = Math.Min(4, pendingMealMissedDays.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            int day = pendingMealMissedDays.Dequeue();
            queuedMealMissedDays.Remove(day);
            var journal = state.missedMealCohorts.FirstOrDefault(row => row.absoluteDay == day);
            if (journal == null || journal.publicationConfirmed) continue;
            var replays = journal.members.Select(row => DomainFactReplay.Create(
                row.publicationKey, new SurvivalMealMissedFact(row.fact))).ToArray();
            var results = new DomainFactDelivery[replays.Length];
            bool completed = false;
            string cohortFailure = string.Empty;
            try
            {
                bool accepted = mealMissedRecovery.ReplayBatch(replays, results, out cohortFailure);
                completed = accepted && results.All(result => result.Completed);
                if (!completed && string.IsNullOrEmpty(cohortFailure))
                    cohortFailure = string.Join(" | ", results.Select(result => result.Detail));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            { cohortFailure = exception.Message; }
            if (completed) journal.publicationConfirmed = true;
            else
            {
                QueueMissedMealDay(day);
                if (string.IsNullOrEmpty(failure)) failure = cohortFailure;
            }
        }
        if (!string.IsNullOrEmpty(failure) && failure != LastMealMissedRecoveryFailure)
            Debug.LogWarning("Missed-meal fact recovery pending: " + failure);
        LastMealMissedRecoveryFailure = failure;
    }

    private void QueueMissedMealDay(int day)
    { if (queuedMealMissedDays.Add(day)) pendingMealMissedDays.Enqueue(day); }

    private void PublishPostCommitMealMissedEvent(in MealMissedEvent missed)
    {
        try { gameEventBus.Publish(missed); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            Debug.LogError("meal-missed-fact-post-commit-observer:" + exception.GetType().Name);
        }
    }

    private void PublishWeatherSample()
    {
        string currentFrontId = debugWeatherOverride.HasValue
            ? "debug:" + debugWeatherOverride.Value
            : climate.WeatherFrontId;
        try { gameEventBus.Publish(new SurvivalWeatherSampledEvent(currentFrontId, CurrentWeather)); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            Debug.LogWarning("survival-weather-observation:" + exception.GetType().Name + ":" + exception.Message);
        }
    }

    private void ConsumeDailyWater(int day)
    {
        int need = GetSurvivalConsumers().Count();
        int available = stockRuntime.CountStoredStock(StockCategory.Water)
            + stockRuntime.CountLooseStock(StockCategory.Water);
        int consumed = Mathf.Min(need, available);
        int missing = Mathf.Max(0, need - consumed);
        state.lastNeededWater = need;
        state.lastConsumedWater = consumed;
        state.lastMissingWater = missing;
        // Personal thirst is now restored only when each character actually drinks.
        // These daily values remain a stock forecast for the survival dashboard.
        state.consecutiveWaterShortageDays = missing > 0
            ? state.consecutiveWaterShortageDays + 1
            : 0;
    }

    private void RefreshSurvivalRisks()
    {
        int rotStacks = spoilageRuntime.CountLooseRotStacks();
        SurvivalRiskEvaluation evaluation = environmentRisks.Evaluate(
            state,
            rotStacks,
            CurrentWeather);
        state.sanitationRisk = evaluation.SanitationRisk;
        state.diseaseRisk = evaluation.DiseaseRisk;
        state.exteriorNightDanger = evaluation.ExteriorNightDanger;
    }

    private void ApplyHealthConsequences()
    {
        EnsureStateLists();
        foreach (SurvivalHealthSaveData entry in state.health)
        {
            entry.remainingSeconds -= 180f;
            entry.severity = Mathf.Clamp01(entry.severity - 0.05f);
        }

        state.health.RemoveAll(entry => entry == null
            || entry.remainingSeconds <= 0f
            || entry.severity <= 0.01f
            || entry.state == SurvivalHealthState.Healthy);

        if (state.diseaseRisk < 55f)
        {
            return;
        }

        CharacterActor patient = GetSurvivalConsumers()
            .OrderBy(actor => actor.Identity?.PersistentId ?? actor.name)
            .FirstOrDefault(actor => !HasActiveHealth(actor, SurvivalHealthState.Sick)
                && !HasActiveHealth(actor, SurvivalHealthState.Infected));
        if (patient == null)
        {
            return;
        }

        SurvivalHealthStateRules.RegisterOrRefresh(
            state,
            patient,
            SurvivalHealthState.Sick,
            state.diseaseRisk / 100f,
            360f,
            "sanitation-risk");
        patient.ApplyMoodFactor(
            "survival:sick",
            "몸 상태가 좋지 않음",
            -4f,
            240f,
            1);
    }

    private bool TryApplyDrawWater(
        IBuildingVisitorPort actor,
        BuildableObject building,
        out int amount,
        out DomainFailure failure)
    {
        amount = 0;
        BuildingWaterSourceAbility ability = building.BuildingData?.GetAbility<BuildingWaterSourceAbility>();
        if (ability == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalWaterSourceUnsupported,
                building.PersistentInstanceId.Value);
            return false;
        }

        amount = Mathf.Max(1, ability.waterPerWork);
        string waterItemId = RequireAuthoredItemId(
            CleanWaterItemId,
            definition => definition.StockCategory == StockCategory.Water,
            "water");
        bool spawned = itemStackRuntime.SpawnItemAt(
                waterItemId,
                amount,
                building.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawnedAmount)
            && spawnedAmount > 0;
        if (!spawned)
        {
            amount = ModularFacilityRuntimeEffects.Produce(building, StockCategory.Water, amount);
        }

        PublishWorkResult(new SurvivalFacilityWorkResultEvent(
            SurvivalFacilityWorkResultKind.WaterDrawing, actor, building, amount));
        failure = amount > 0
            ? DomainFailure.None
            : new DomainFailure(
                FailureCode.SurvivalOutputUnavailable,
                waterItemId);
        return amount > 0;
    }

    private bool TryApplyCook(
        IBuildingVisitorPort actor,
        BuildableObject building,
        out int amount,
        out DomainFailure failure)
    {
        amount = 0;
        BuildingCookingAbility cooking = building.BuildingData?.GetAbility<BuildingCookingAbility>();
        if (cooking == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalCookingUnsupported,
                building.PersistentInstanceId.Value);
            return false;
        }

        int input = Mathf.Max(1, cooking.inputFood);
        if (stockRuntime.CountStoredStock(StockCategory.Food) < input)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalFoodStockMissing,
                input.ToString());
            return false;
        }

        if (cooking.requiresFuel && stockRuntime.CountStoredStock(StockCategory.Fuel) <= 0)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalFuelStockMissing,
                "1");
            return false;
        }

        stockRuntime.WithdrawStock(StockCategory.Food, input);
        if (cooking.requiresFuel)
        {
            stockRuntime.WithdrawStock(StockCategory.Fuel, 1);
        }

        BuildingPreservationAbility preservation =
            SurvivalFacilityWorkRules.FindPreservationAbility(building);
        string outputId = RequireAuthoredItemId(
            preservation != null
                ? PreservedFoodItemId
                : CookedMealItemId,
            definition => definition.StockCategory == StockCategory.Food
                && definition.TryGetFeature(out FoodItemFeature food)
                && food.preserved == (preservation != null),
            preservation != null ? "preserved meal" : "cooked meal");
        amount = preservation != null
            ? Mathf.Max(1, preservation.preservedMealsPerCook)
            : Mathf.Max(1, cooking.cookedMeals);
        bool spawned = itemStackRuntime.SpawnItemAt(
                outputId,
                amount,
                building.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawnedAmount)
            && spawnedAmount > 0;
        if (!spawned)
        {
            ModularFacilityRuntimeEffects.Produce(building, StockCategory.Food, amount);
        }

        actor?.ApplyMoodFactor(
            "survival:cooked-meal-work",
            "따뜻한 식사를 준비함",
            2f,
            120f,
            1);
        PublishWorkResult(new SurvivalFacilityWorkResultEvent(
            SurvivalFacilityWorkResultKind.Cooking, actor, building, amount, preservedFood: preservation != null));
        failure = DomainFailure.None;
        return true;
    }

    private string RequireAuthoredItemId(
        string itemId,
        Func<ItemDefinitionSO, bool> predicate,
        string role)
    {
        if (!itemCatalog.TryGet(
                (ItemDefinitionId)itemId,
                out ItemDefinitionSO definition)
            || definition == null
            || !predicate(definition))
        {
            throw new InvalidOperationException(
                $"Authored survival item '{itemId}' does not satisfy role '{role}'.");
        }
        return definition.ItemId;
    }

    public static string CreateTreatmentPhysicalOperationId(
        string treatmentOperationId)
        => SurvivalTreatmentIdentity.CreateTreatmentPhysicalOperationId(treatmentOperationId);

    public bool TryGetTreatmentSupply(
        CharacterId patientId,
        out SurvivalTreatmentSupplySnapshot snapshot)
        => treatment.TryGetTreatmentSupply(patientId, out snapshot);

    public bool TryEnsureTreatmentSupply(
        IBuildingVisitorPort actor,
        BuildableObject building,
        int workRunId,
        out bool completionOnly,
        out DomainFailure failure)
        => treatment.TryEnsureTreatmentSupply(actor, building, workRunId, out completionOnly, out failure);

    public bool TryApplyTreatmentWork(
        IBuildingVisitorPort actor,
        BuildableObject building,
        int workRunId,
        out int amount,
        out DomainFailure failure)
        => treatment.TryApplyTreatmentWork(actor, building, workRunId, out amount, out failure);

    public bool TryReconcileLostTreatmentOwners(out string failureReason)
    {
        stockRuntime.TryRefreshFacilityFuelAuthorities();
        if (!treatment.TryReconcileLostTreatmentOwners(out failureReason))
            return false;
        return stockRuntime.TryRecoverFacilityFuelCommits(out failureReason);
    }

    internal const string DetoxOperationPrefix = SurvivalTreatmentIdentity.DetoxOperationPrefix;
    internal const string TreatmentOperationPrefix = SurvivalTreatmentIdentity.TreatmentOperationPrefix;

    private void PublishWorkResult(SurvivalFacilityWorkResultEvent fact)
    {
        try { gameEventBus.Publish(fact); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            Debug.LogWarning("survival-work-observation:" + exception.GetType().Name + ":" + exception.Message);
        }
    }

    private bool CanDrawWater(BuildableObject building)
    {
        return SurvivalFacilityWorkRules.CanDrawWater(building);
    }

    private SurvivalWeatherType CurrentWeather =>
        debugWeatherOverride ?? MapWeather(
            climateDefinitions,
            climate.WeatherFrontId);

    private float CurrentOutdoorTemperature => debugWeatherOverride.HasValue
        ? GetDebugOutdoorTemperature(debugWeatherOverride.Value)
        : climate.OutdoorTemperatureC;

    private static SurvivalWeatherType MapWeather(
        IClimateDefinitionCatalog definitions,
        string weatherFrontId)
    {
        WeatherFrontDefinition front = (definitions
            ?? throw new ArgumentNullException(nameof(definitions)))
            .RequireFront(weatherFrontId);
        return front.Kind switch
        {
            WeatherFrontKind.Rain => SurvivalWeatherType.Rain,
            WeatherFrontKind.Fog => SurvivalWeatherType.Fog,
            WeatherFrontKind.Heatwave => SurvivalWeatherType.HeatWave,
            WeatherFrontKind.ColdSnap => SurvivalWeatherType.ColdSnap,
            WeatherFrontKind.Storm => SurvivalWeatherType.Storm,
            WeatherFrontKind.Clear => SurvivalWeatherType.Clear,
            _ => throw new InvalidOperationException(
                $"Unsupported climate weather front kind '{front.Kind}' for '{front.Id}'.")
        };
    }

    private static float GetDebugOutdoorTemperature(
        SurvivalWeatherType weather) => weather switch
        {
            SurvivalWeatherType.ColdSnap => -6f,
            SurvivalWeatherType.HeatWave => 34f,
            SurvivalWeatherType.Storm => 12f,
            SurvivalWeatherType.Rain => 14f,
            SurvivalWeatherType.Fog => 16f,
            _ => 18f
        };

    private bool HasActiveHealth(CharacterActor actor, SurvivalHealthState healthState)
    {
        return SurvivalHealthStateRules.HasActive(state, actor, healthState);
    }

}
