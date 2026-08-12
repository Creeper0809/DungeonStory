using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed partial class SurvivalFoodRuntime :
    ISurvivalFoodQuery,
    ISurvivalFoodCommand,
    ISurvivalFoodPersistence,
    ISurvivalFoodDebugCommand,
    ICharacterNutritionRuntime,
    ISurvivalEnvironmentQuery,
    ISurvivalStorageEnvironmentSink,
    IInitializable,
    IDisposable
{
    private const int DailyFuelDemand = 1;
    private const float TreatmentMedicineHeal = 16f;

    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IClimateQuery climate;
    private readonly ISurvivalServiceSessionCapability serviceSessionRuntime;
    private readonly SurvivalFoodStockRuntime stockRuntime;
    private readonly SurvivalFoodSpoilageRuntime spoilageRuntime;
    private readonly SurvivalFoodOverviewCache overviewCache;
    private readonly SurvivalMealLedger mealLedger;
    private readonly SurvivalEnvironmentRiskEvaluator environmentRisks;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IDisposable operatingDayStartedSubscription;
    private IDisposable stockConsumedSubscription;
    private IDisposable physicalMealConsumedSubscription;
    private SurvivalWeatherType? debugWeatherOverride;
    private string lastAnnouncedWeatherFrontId = string.Empty;
    private SurvivalFoodAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new SurvivalFoodAggregateState());
    private DungeonSurvivalSaveData state => aggregateState.Data;
    private long mealSequence
    {
        get => aggregateState.MealSequence;
        set => aggregateState.MealSequence = value;
    }

    public SurvivalFoodRuntime(
        SurvivalFoodRuntimeDependencies dependencies,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IGameEventBus gameEventBus,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        IWorldThreatModifierQuery worldThreatModifiers,
        ISurvivalServiceSessionCapability serviceSessionRuntime,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterCarryInventoryRegistry carryInventories = null)
    {
        _ = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        this.speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.itemStackRuntime = dependencies.ItemStackRuntime;
        this.itemCatalog = dependencies.ItemCatalog;
        climate = dependencies.Climate;
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        worldThreatModifiers = worldThreatModifiers
            ?? throw new ArgumentNullException(nameof(worldThreatModifiers));
        this.serviceSessionRuntime = serviceSessionRuntime
            ?? throw new ArgumentNullException(nameof(serviceSessionRuntime));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        stockRuntime = new SurvivalFoodStockRuntime(
            dependencies.GridSystemProvider,
            this.worldRegistry,
            this.itemStackRuntime,
            this.itemCatalog,
            dependencies.StockQuery);
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
        operatingDayStartedSubscription =
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
        stockConsumedSubscription =
            gameEventBus.Subscribe<FacilityStockConsumedEvent>(OnTriggerEvent);
        physicalMealConsumedSubscription =
            gameEventBus.Subscribe<PhysicalMealConsumedEvent>(OnTriggerEvent);
    }

    public void Dispose()
    {
        operatingDayStartedSubscription?.Dispose();
        operatingDayStartedSubscription = null;
        stockConsumedSubscription?.Dispose();
        stockConsumedSubscription = null;
        physicalMealConsumedSubscription?.Dispose();
        physicalMealConsumedSubscription = null;
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        if (eventType.day <= 0 || state.lastProcessedDay == eventType.day)
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
            facility,
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
            || eventType.Facility == null
            || !eventType.Result.Success
            || !IsSurvivalConsumer(eventType.Actor))
        {
            return;
        }

        RecordMeal(
            eventType.Actor,
            eventType.Facility,
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
        PublishMissedMealEvents(day);
        AnnounceDangerousWeatherIfChanged();
        spoilageRuntime.Process(
            state,
            CurrentWeather,
            advanceTime: true);
        RefreshDailyFoodForecast(day);
        ConsumeDailyWater(day);
        ConsumeDailyFuel();
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
                "Survival resources restore rejected an invalid V5 candidate: "
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
                return TryApplyTreat(actor, building, out amount, out failure);
            case var id when id == BuiltInWorkTypeIds.Refuel:
                return SurvivalFacilityWorkRules.TryApplyRefuel(
                    actor,
                    building,
                    stockRuntime,
                    state,
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
            var id when id == BuiltInWorkTypeIds.Treat => building.BuildingData.GetAbility<BuildingMedicalAbility>() != null
                && HasTreatableHealth()
                && (building.BuildingData.GetAbility<BuildingMedicalAbility>()?.requiresMedicine != true
                    || stockRuntime.CountStoredStock(StockCategory.Medicine) > 0
                    || stockRuntime.CountStoredStock(StockCategory.Biological) > 0),
            var id when id == BuiltInWorkTypeIds.Refuel => building.BuildingData.GetAbility<BuildingFuelConsumerAbility>() != null
                && stockRuntime.CountStoredStock(StockCategory.Fuel) > 0,
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
            var id when id == BuiltInWorkTypeIds.Treat => 35f + (overview.UntreatedCount * 25f) + Mathf.Clamp(overview.DiseaseRisk * 0.35f, 0f, 35f),
            var id when id == BuiltInWorkTypeIds.Refuel => CurrentWeather == SurvivalWeatherType.ColdSnap
                ? 75f
                : Mathf.Clamp(overview.ExteriorNightDanger * 0.45f, 10f, 55f),
            _ => 0f
        };
    }

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
    {
        IReadOnlyList<CharacterActor> actors = worldRegistry.Characters;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor != null
                && !actor.IsDead
                && IsSurvivalConsumer(actor))
            {
                yield return actor;
            }
        }
    }

    private int CountSurvivalConsumers()
    {
        int count = 0;
        IReadOnlyList<CharacterActor> actors = worldRegistry.Characters;
        for (int index = 0; index < actors.Count; index++)
        {
            CharacterActor actor = actors[index];
            if (actor != null
                && !actor.IsDead
                && IsSurvivalConsumer(actor))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsSurvivalConsumer(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && (actor.Role == CharacterRole.Owner
                || CharacterWorkRoleUtility.TryGetWork(actor, out _));
    }

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
        BuildableObject facility,
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
            facility,
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
                Mathf.Max(
                    0,
                    Mathf.FloorToInt(
                        gameClock.Time / GameCalendarRules.SecondsPerDay))));
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

    private void PublishMissedMealEvents(int currentDay)
    {
        int completedDay = Math.Max(0, currentDay - 1);
        if (completedDay <= 0)
            return;
        foreach (CharacterActor actor in GetSurvivalConsumers())
        {
            if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
                || GetMealsConsumed(characterId.Value, completedDay) > 0)
                continue;
            gameEventBus.Publish(new MealMissedEvent(
                characterId,
                consecutiveMisses: 1,
                currentDay));
        }
    }

    private void AnnounceDangerousWeatherIfChanged()
    {
        string currentFrontId = debugWeatherOverride.HasValue
            ? "debug:" + debugWeatherOverride.Value
            : climate.WeatherFrontId;
        if (string.Equals(
                currentFrontId,
                lastAnnouncedWeatherFrontId,
                StringComparison.Ordinal))
        {
            return;
        }

        lastAnnouncedWeatherFrontId = currentFrontId;
        SurvivalWeatherType weather = CurrentWeather;
        if (weather is SurvivalWeatherType.ColdSnap
            or SurvivalWeatherType.HeatWave
            or SurvivalWeatherType.Storm)
        {
            gameEventBus.RaiseAlert(
                "날씨가 위험해집니다",
                $"{SurvivalFacilityWorkRules.FormatWeather(weather)} 예보입니다. 연료, 조명, 외부 작업 상태를 확인하세요.",
                EventAlertImportance.Medium,
                "생존");
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

    private void ConsumeDailyFuel()
    {
        int need = DailyFuelDemand;
        if (CurrentWeather == SurvivalWeatherType.ColdSnap)
        {
            need += 1;
        }
        need = Mathf.CeilToInt(
            need * environmentRisks.GetThreatMultiplier(
                OffenseThreatModifierKind.FuelConsumption));

        int consumed = stockRuntime.WithdrawStock(StockCategory.Fuel, need);
        state.lastConsumedFuel = consumed;
        state.lastMissingFuel = Mathf.Max(0, need - consumed);
        if (state.lastMissingFuel <= 0)
        {
            return;
        }

        gameEventBus.RaiseAlert(
            "연료가 부족합니다",
            "조명과 난방이 약해집니다. 밤 외부 위험과 추위 위험이 함께 오릅니다.",
            EventAlertImportance.Medium,
            "생존");
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

        if (!CanDrawWater(building))
        {
            failure = new DomainFailure(
                FailureCode.SurvivalWaterFrozen,
                building.PersistentInstanceId.Value);
            return false;
        }

        amount = Mathf.Max(1, ability.waterPerWork);
        string waterItemId = RequireAuthoredItemId(
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

        RecordWorkActivity(
            actor,
            building,
            BuiltInWorkTypeIds.DrawWater,
            amount > 0 ? BuildingActivityOutcomes.Completed : BuildingActivityOutcomes.Failed,
            amount > 0
                ? $"{SurvivalFacilityWorkRules.GetBuildingName(building)}에서 물 {amount}개를 길었다."
                : "물을 담을 곳을 찾지 못했다.",
            amount > 0 ? "water-drawn" : "water-output-failed",
            amount,
            amount <= 0);
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
        RecordWorkActivity(
            actor,
            building,
            BuiltInWorkTypeIds.Cook,
            BuildingActivityOutcomes.Completed,
            preservation != null
                ? $"{SurvivalFacilityWorkRules.GetBuildingName(building)}에서 오래 둘 수 있는 보존 식량을 만들었다."
                : $"{SurvivalFacilityWorkRules.GetBuildingName(building)}에서 따뜻한 식사를 만들었다.",
            preservation != null ? "food-preserved" : "food-cooked",
            amount,
            false);
        failure = DomainFailure.None;
        return true;
    }

    private string RequireAuthoredItemId(
        Func<ItemDefinitionSO, bool> predicate,
        string role)
    {
        ItemDefinitionSO definition = itemCatalog.All
            .Where(candidate => candidate != null && predicate(candidate))
            .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        return definition != null
            ? definition.ItemId
            : throw new InvalidOperationException(
                $"No authored item definition satisfies survival role '{role}'.");
    }

    private bool TryApplyTreat(
        IBuildingVisitorPort actor,
        BuildableObject building,
        out int amount,
        out DomainFailure failure)
    {
        amount = 0;
        BuildingMedicalAbility medical = building.BuildingData?.GetAbility<BuildingMedicalAbility>();
        if (medical == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentUnsupported,
                building.PersistentInstanceId.Value);
            return false;
        }

        SurvivalHealthSaveData patientEntry = FindTreatmentEntry(building);
        if (patientEntry == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentTargetMissing,
                building.PersistentInstanceId.Value);
            return false;
        }

        bool usedBloodSubstitute = false;
        CharacterActor patient = SurvivalFoodStatePersistence.FindActor(
            GetSurvivalConsumers(), patientEntry.persistentId);
        ServiceSessionSnapshot serviceSession = null;
        BuildingServiceHubAbility serviceHub =
            building.GetServiceHubAbility();
        if (serviceHub != null
            && !serviceSessionRuntime.TryBeginSession(
                new ServiceSessionRequest
                {
                    Hub = building,
                    Actor = patient,
                    ProcessId = serviceHub.supportedProcessIds?
                        .FirstOrDefault() ?? string.Empty,
                    IsInternalActor = Shop.IsInternalStaffUse(patient?.BuildingVisitor),
                    AdvertisedDemand = !Shop.IsInternalStaffUse(patient?.BuildingVisitor)
                },
                out serviceSession,
                out DomainFailure serviceFailure))
        {
            failure = serviceFailure;
            return false;
        }
        if (medical.requiresMedicine
            && !stockRuntime.TryConsumeTreatmentMaterial(out usedBloodSubstitute))
        {
            if (serviceSession != null)
            {
                serviceSessionRuntime.CancelSession(
                    serviceSession.SessionId,
                    FailureCode.SurvivalTreatmentMaterialMissing.ToString());
            }
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentMaterialMissing);
            return false;
        }

        float treatmentEfficiency = usedBloodSubstitute ? 0.55f : 1f;
        patientEntry.severity = Mathf.Clamp01(
            patientEntry.severity
            - (Mathf.Max(0f, medical.severityReduction) * treatmentEfficiency));
        patientEntry.remainingSeconds = Mathf.Max(
            0f,
            patientEntry.remainingSeconds - (180f * treatmentEfficiency));
        if (patientEntry.severity <= 0.05f || patientEntry.remainingSeconds <= 0f)
        {
            state.health.Remove(patientEntry);
        }
        else
        {
            patientEntry.state = SurvivalHealthState.Recovering;
        }

        patient?.Heal(TreatmentMedicineHeal * treatmentEfficiency);
        if (usedBloodSubstitute && patient != null)
        {
            SurvivalHealthStateRules.RegisterOrRefresh(
                state,
                patient,
                SurvivalHealthState.Exposed,
                0.25f,
                240f,
                "불안정한 혈액 대체 치료");
            patient.ApplyMoodFactor(
                "survival:blood-treatment",
                "정체불명의 혈액으로 치료받음",
                -4f,
                240f,
                1);
        }
        patient?.ApplyMoodFactor(
            "survival:treated",
            "제때 치료받음",
            3f,
            180f,
            1);
        RecordWorkActivity(
            actor,
            building,
            BuiltInWorkTypeIds.Treat,
            BuildingActivityOutcomes.Completed,
            $"{SurvivalFacilityWorkRules.GetBuildingName(building)}에서 {SurvivalFacilityWorkRules.GetActorName(patient, patientEntry.persistentId)}의 상태를 살폈다.",
            "survival-treated",
            0,
            false);
        amount = 1;
        if (serviceSession != null
            && !serviceSessionRuntime.TryCompleteSession(
                serviceSession.SessionId,
                out _,
                out DomainFailure completionFailure))
        {
            serviceSessionRuntime.CancelSession(
                serviceSession.SessionId,
                completionFailure.Code.ToString());
        }
        failure = DomainFailure.None;
        return true;
    }

    private static void RecordWorkActivity(
        IBuildingVisitorPort actor,
        BuildableObject building,
        WorkTypeId workTypeId,
        string outcomeId,
        string factText,
        string reasonCode,
        int quantity,
        bool bubbleEligible)
    {
        actor?.RecordActivity(
            building,
            new BuildingActivitySnapshot(
                BuildingActivityKinds.Work,
                outcomeId,
                factText,
                workTypeId.Value,
                string.Empty,
                reasonCode,
                0f,
                quantity,
                bubbleEligible));
    }

    private bool CanDrawWater(BuildableObject building)
    {
        return SurvivalFacilityWorkRules.CanDrawWater(building, CurrentWeather);
    }

    private SurvivalWeatherType CurrentWeather =>
        debugWeatherOverride ?? MapWeather(climate.WeatherFrontId);

    private float CurrentOutdoorTemperature => debugWeatherOverride.HasValue
        ? GetDebugOutdoorTemperature(debugWeatherOverride.Value)
        : climate.OutdoorTemperatureC;

    private static SurvivalWeatherType MapWeather(string weatherFrontId) =>
        weatherFrontId switch
        {
            "weather:rain" => SurvivalWeatherType.Rain,
            "weather:fog" => SurvivalWeatherType.Fog,
            "weather:heatwave" => SurvivalWeatherType.HeatWave,
            "weather:cold-snap" => SurvivalWeatherType.ColdSnap,
            "weather:storm" => SurvivalWeatherType.Storm,
            "weather:clear" => SurvivalWeatherType.Clear,
            _ => throw new InvalidOperationException(
                $"Unsupported climate weather front '{weatherFrontId}'.")
        };

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

    private bool HasTreatableHealth()
    {
        return SurvivalHealthStateRules.HasTreatable(state);
    }

    private SurvivalHealthSaveData FindTreatmentEntry(
        BuildableObject building)
    {
        bool useFirstActive = building.GetServiceHubAbility() != null
            && serviceSessionRuntime.GetHubSnapshot(building).Mode
                == ServiceOperationMode.Direct;
        return SurvivalHealthStateRules.FindTreatmentEntry(state, useFirstActive);
    }

    private bool HasActiveHealth(CharacterActor actor, SurvivalHealthState healthState)
    {
        return SurvivalHealthStateRules.HasActive(state, actor, healthState);
    }

}
