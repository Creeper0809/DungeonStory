using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SurvivalFoodRuntime :
    ISurvivalFoodRuntime,
    ICharacterNutritionRuntime,
    ISurvivalEnvironmentQuery,
    IInitializable,
    IDisposable
{
    private const float DefaultFreshnessSeconds = 360f;
    private const float PreservedFreshnessSeconds = 1440f;
    private const float FreshnessWarningThresholdSeconds = 90f;
    private const float OverviewRefreshIntervalSeconds = 0.5f;
    private const int DailyFuelDemand = 1;
    private const float TreatmentMedicineHeal = 16f;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IResourceEconomyContentCatalog resourceCatalog;
    private IDisposable operatingDayStartedSubscription;
    private IDisposable stockConsumedSubscription;
    private IDisposable physicalMealConsumedSubscription;
    private DungeonSurvivalSaveData state = new DungeonSurvivalSaveData();
    private IReadOnlyList<WorldItemStackSnapshot> cachedItemStacks =
        Array.Empty<WorldItemStackSnapshot>();
    private int cachedItemStackVersion = -1;
    private SurvivalFoodOverview cachedOverview;
    private int cachedOverviewFrame = -1;
    private int cachedOverviewItemVersion = -1;
    private int cachedOverviewCharacterVersion = -1;
    private int cachedOverviewBuildingVersion = -1;
    private float cachedOverviewTime = float.NegativeInfinity;
    private bool hasCachedOverview;
    private int cachedRiskBuildingVersion = int.MinValue;
    private float cachedVentilationBonus;
    private float cachedLightSafety;
    private long mealSequence;

    public SurvivalFoodRuntime(
        IGridSystemProvider gridSystemProvider,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IGameEventBus gameEventBus,
        ICharacterAiWorldRegistry worldRegistry = null,
        IWorldItemStackRuntime itemStackRuntime = null,
        IGameClock gameClock = null,
        IResourceEconomyContentCatalog resourceCatalog = null)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.worldRegistry = worldRegistry;
        this.itemStackRuntime = itemStackRuntime;
        this.gameClock = gameClock;
        this.resourceCatalog = resourceCatalog;
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public int GetStoredStockCount(StockCategory category)
    {
        return CountStoredStock(category);
    }

    public SurvivalEnvironmentSnapshot GetEnvironmentSnapshot()
    {
        return new SurvivalEnvironmentSnapshot(
            state.currentWeather,
            state.outdoorTemperature,
            state.exteriorNightDanger,
            state.sanitationRisk,
            state.diseaseRisk);
    }

    public int TryConsumeStoredStock(StockCategory category, int amount)
    {
        return WithdrawStock(category, Mathf.Max(0, amount));
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
        UpdateWeather(day);
        ProcessSpoilage(advanceTime: true);
        RefreshDailyFoodForecast(day);
        ConsumeDailyWater(day);
        ConsumeDailyFuel();
        RefreshSurvivalRisks();
        ApplyHealthConsequences();
        InvalidateOverviewCache();
    }

    public DungeonSurvivalSaveData Capture()
    {
        return new DungeonSurvivalSaveData
        {
            version = DungeonSurvivalSaveData.CurrentVersion,
            lastProcessedDay = state.lastProcessedDay,
            lastNeededFood = state.lastNeededFood,
            lastConsumedFood = state.lastConsumedFood,
            lastMissingFood = state.lastMissingFood,
            lastNeededWater = state.lastNeededWater,
            lastConsumedWater = state.lastConsumedWater,
            lastMissingWater = state.lastMissingWater,
            consecutiveFoodShortageDays = state.consecutiveFoodShortageDays,
            consecutiveWaterShortageDays = state.consecutiveWaterShortageDays,
            lastConsumedFuel = state.lastConsumedFuel,
            lastMissingFuel = state.lastMissingFuel,
            currentWeather = state.currentWeather,
            weatherDay = state.weatherDay,
            outdoorTemperature = state.outdoorTemperature,
            sanitationRisk = state.sanitationRisk,
            diseaseRisk = state.diseaseRisk,
            exteriorNightDanger = state.exteriorNightDanger,
            spoilage = (state.spoilage ?? new List<SurvivalFoodSpoilageSaveData>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.stackId))
                .Select(entry => new SurvivalFoodSpoilageSaveData
                {
                    stackId = entry.stackId,
                    itemId = entry.itemId,
                    remainingFreshnessSeconds = entry.remainingFreshnessSeconds,
                    preserved = entry.preserved,
                    contaminated = entry.contaminated
                })
                .ToList(),
            health = (state.health ?? new List<SurvivalHealthSaveData>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.persistentId))
                .Select(entry => new SurvivalHealthSaveData
                {
                    persistentId = entry.persistentId,
                    state = entry.state,
                    severity = entry.severity,
                    remainingSeconds = entry.remainingSeconds,
                    source = entry.source
                })
                .ToList(),
            mealLedger = (state.mealLedger ?? new List<CharacterMealLedgerSaveData>())
                .Where(entry => entry != null
                    && !string.IsNullOrWhiteSpace(entry.mealId)
                    && !string.IsNullOrWhiteSpace(entry.characterId))
                .Select(entry => new CharacterMealLedgerSaveData
                {
                    mealId = entry.mealId,
                    characterId = entry.characterId,
                    facilityId = entry.facilityId,
                    itemId = entry.itemId,
                    displayName = entry.displayName,
                    dietClass = entry.dietClass,
                    quality = entry.quality,
                    nutrition = entry.nutrition,
                    policyViolation = entry.policyViolation,
                    contaminated = entry.contaminated,
                    day = entry.day,
                    amount = entry.amount
                })
                .ToList()
        };
    }

    public void DebugSetWeather(SurvivalWeatherType weather)
    {
        EnsureStateLists();
        state.currentWeather = weather;
        state.outdoorTemperature = weather switch
        {
            SurvivalWeatherType.ColdSnap => -6f,
            SurvivalWeatherType.HeatWave => 34f,
            SurvivalWeatherType.Storm => 12f,
            SurvivalWeatherType.Rain => 14f,
            SurvivalWeatherType.Fog => 16f,
            _ => 18f
        };
        RefreshSurvivalRisks();
        InvalidateOverviewCache();
    }

    public void DebugAdvanceSpoilage(float seconds)
    {
        EnsureStateLists();
        float advance = Mathf.Max(0f, seconds);
        foreach (SurvivalFoodSpoilageSaveData entry in state.spoilage)
        {
            if (entry != null && !entry.preserved)
            {
                entry.remainingFreshnessSeconds = Mathf.Max(
                    0f,
                    entry.remainingFreshnessSeconds - advance);
            }
        }

        ProcessSpoilage(advanceTime: false);
        InvalidateOverviewCache();
    }

    public void DebugResetSpoilage()
    {
        EnsureStateLists();
        foreach (SurvivalFoodSpoilageSaveData entry in state.spoilage)
        {
            if (entry != null)
            {
                entry.remainingFreshnessSeconds = entry.preserved
                    ? PreservedFreshnessSeconds
                    : DefaultFreshnessSeconds;
                entry.contaminated = false;
            }
        }

        InvalidateOverviewCache();
    }

    public void Restore(DungeonSurvivalSaveData saveData)
    {
        state = saveData ?? new DungeonSurvivalSaveData();
        state.version = DungeonSurvivalSaveData.CurrentVersion;
        state.spoilage ??= new List<SurvivalFoodSpoilageSaveData>();
        state.health ??= new List<SurvivalHealthSaveData>();
        state.mealLedger ??= new List<CharacterMealLedgerSaveData>();
        mealSequence = state.mealLedger.Count;
        InvalidateOverviewCache();
    }

    public SurvivalFoodOverview GetOverview()
    {
        int frame = gameClock?.FrameCount ?? -1;
        int itemVersion = itemStackRuntime?.ItemStackVersion ?? -1;
        int characterVersion = worldRegistry?.CharacterVersion ?? -1;
        int buildingVersion = worldRegistry?.BuildingVersion ?? -1;
        float now = gameClock?.Time ?? 0f;
        bool refreshIntervalValid = gameClock != null
            ? now - cachedOverviewTime <= OverviewRefreshIntervalSeconds
            : cachedOverviewFrame == frame;
        if (hasCachedOverview
            && refreshIntervalValid
            && (gameClock != null || cachedOverviewFrame == frame)
            && cachedOverviewItemVersion == itemVersion
            && cachedOverviewCharacterVersion == characterVersion
            && cachedOverviewBuildingVersion == buildingVersion)
        {
            return cachedOverview;
        }

        EnsureStateLists();
        ProcessSpoilage();
        RefreshSurvivalRisks();

        int required = CountSurvivalConsumers();
        int stored = CountStoredFood();
        int looseFood = CountLooseFood();
        int carcasses = CountCarcasses(out int pendingFood);
        int shortageDays = required <= 0
            ? int.MaxValue
            : Mathf.FloorToInt((stored + looseFood + pendingFood) / (float)required);
        int storedWater = CountStoredStock(StockCategory.Water);
        int looseWater = CountLooseStock(StockCategory.Water);
        int storedFuel = CountStoredStock(StockCategory.Fuel);
        int storedMedicine = CountStoredStock(StockCategory.Medicine);
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
        int spoilageWarnings = CountSpoilageWarnings();
        SurvivalFoodOverview overview = new SurvivalFoodOverview(
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
            state.currentWeather,
            state.outdoorTemperature,
            state.sanitationRisk,
            state.diseaseRisk,
            state.exteriorNightDanger,
            sickCount,
            untreatedCount);
        if (frame >= 0)
        {
            cachedOverview = overview;
            cachedOverviewFrame = frame;
            cachedOverviewItemVersion = itemStackRuntime?.ItemStackVersion ?? -1;
            cachedOverviewCharacterVersion = worldRegistry?.CharacterVersion ?? -1;
            cachedOverviewBuildingVersion = worldRegistry?.BuildingVersion ?? -1;
            cachedOverviewTime = now;
            hasCachedOverview = true;
        }

        return overview;
    }

    private void InvalidateOverviewCache()
    {
        hasCachedOverview = false;
        cachedOverviewFrame = -1;
        cachedOverviewTime = float.NegativeInfinity;
    }

    public bool TryGetItemStatus(string stackId, string itemId, out SurvivalItemStatus status)
    {
        EnsureStateLists();
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedItemId)
            || !ShouldTrackSpoilage(normalizedItemId))
        {
            status = default;
            return false;
        }

        SurvivalFoodSpoilageSaveData entry = state.spoilage
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(candidate.stackId, normalizedStackId, StringComparison.Ordinal));
        if (entry == null)
        {
            entry = CreateSpoilageEntry(normalizedStackId, normalizedItemId);
            if (!string.IsNullOrWhiteSpace(normalizedStackId))
            {
                state.spoilage.Add(entry);
            }
        }

        float baseFreshness = GetBaseFreshnessSeconds(
            entry.itemId,
            entry.preserved);
        string label = entry.contaminated
            ? "오염됨"
            : entry.remainingFreshnessSeconds <= FreshnessWarningThresholdSeconds
                ? "부패 임박"
                : entry.preserved
                    ? "보존됨"
                    : "신선함";
        status = new SurvivalItemStatus(
            tracked: true,
            preserved: entry.preserved,
            contaminated: entry.contaminated,
            freshness01: entry.remainingFreshnessSeconds / Mathf.Max(1f, baseFreshness),
            remainingFreshnessSeconds: entry.remainingFreshnessSeconds,
            label: label);
        return true;
    }

    public bool TryGetCharacterStatus(CharacterActor actor, out SurvivalCharacterStatus status)
    {
        EnsureStateLists();
        status = default;
        if (actor == null)
        {
            return false;
        }

        string persistentId = actor.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            persistentId = actor.name;
        }

        List<SurvivalHealthSaveData> activeEntries = state.health
            .Where(entry => entry != null
                && entry.remainingSeconds > 0f
                && entry.state != SurvivalHealthState.Healthy
                && (string.Equals(entry.persistentId, persistentId, StringComparison.Ordinal)
                    || string.Equals(entry.persistentId, actor.name, StringComparison.Ordinal)))
            .OrderByDescending(entry => entry.state == SurvivalHealthState.Infected ? 3 : 0)
            .ThenByDescending(entry => entry.state == SurvivalHealthState.Sick ? 2 : 0)
            .ThenByDescending(entry => entry.state == SurvivalHealthState.Exposed ? 1 : 0)
            .ThenByDescending(entry => entry.severity)
            .ToList();

        SurvivalHealthSaveData primary = activeEntries.FirstOrDefault();
        float temperatureComfort = GetTemperatureComfort01(state.outdoorTemperature);
        status = new SurvivalCharacterStatus(
            hasStatus: primary != null || state.consecutiveWaterShortageDays > 0 || state.consecutiveFoodShortageDays > 0,
            primaryState: primary?.state ?? SurvivalHealthState.Healthy,
            severity01: primary?.severity ?? 0f,
            remainingSeconds: primary?.remainingSeconds ?? 0f,
            source: primary?.source ?? string.Empty,
            activeIssueCount: activeEntries.Count,
            temperatureComfort01: temperatureComfort,
            waterSummary: state.consecutiveWaterShortageDays > 0
                ? $"물 부족 {state.consecutiveWaterShortageDays}일"
                : "물 정상",
            foodSummary: state.consecutiveFoodShortageDays > 0
                ? $"식량 부족 {state.consecutiveFoodShortageDays}일"
                : "식량 정상");
        return true;
    }

    public bool TryApplySurvivalWork(
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId,
        out int amount,
        out string message)
    {
        EnsureStateLists();
        amount = 0;
        message = string.Empty;
        if (building == null)
        {
            message = "대상 시설이 없습니다.";
            return false;
        }

        switch (workTypeId)
        {
            case var id when id == BuiltInWorkTypeIds.DrawWater:
                return TryApplyDrawWater(actor, building, out amount, out message);
            case var id when id == BuiltInWorkTypeIds.Cook:
                return TryApplyCook(actor, building, out amount, out message);
            case var id when id == BuiltInWorkTypeIds.Treat:
                return TryApplyTreat(actor, building, out amount, out message);
            case var id when id == BuiltInWorkTypeIds.Refuel:
                return TryApplyRefuel(actor, building, out amount, out message);
            default:
                message = "생존 작업이 아닙니다.";
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
                && CountStoredStock(StockCategory.Food) >= Mathf.Max(1, cooking.inputFood)
                && (!cooking.requiresFuel || CountStoredStock(StockCategory.Fuel) > 0),
            var id when id == BuiltInWorkTypeIds.Treat => building.BuildingData.GetAbility<BuildingMedicalAbility>() != null
                && HasTreatableHealth()
                && (building.BuildingData.GetAbility<BuildingMedicalAbility>()?.requiresMedicine != true
                    || CountStoredStock(StockCategory.Medicine) > 0
                    || CountStoredStock(StockCategory.Biological) > 0),
            var id when id == BuiltInWorkTypeIds.Refuel => building.BuildingData.GetAbility<BuildingFuelConsumerAbility>() != null
                && CountStoredStock(StockCategory.Fuel) > 0,
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
            var id when id == BuiltInWorkTypeIds.Refuel => state.currentWeather == SurvivalWeatherType.ColdSnap
                ? 75f
                : Mathf.Clamp(overview.ExteriorNightDanger * 0.45f, 10f, 55f),
            _ => 0f
        };
    }

    public int GetMealsConsumed(int day)
    {
        EnsureStateLists();
        return state.mealLedger
            .Where(entry => entry != null && entry.day == day)
            .Sum(entry => Mathf.Max(0, entry.amount));
    }

    public int GetMealsConsumed(string characterId, int day)
    {
        EnsureStateLists();
        string normalizedId = characterId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return 0;
        }

        return state.mealLedger
            .Where(entry => entry != null
                && entry.day == day
                && string.Equals(entry.characterId, normalizedId, StringComparison.Ordinal))
            .Sum(entry => Mathf.Max(0, entry.amount));
    }

    public IReadOnlyList<CharacterMealLedgerSaveData> GetRecentMeals(int maximumCount = 30)
    {
        EnsureStateLists();
        return state.mealLedger
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.day)
            .ThenByDescending(entry => entry.mealId, StringComparer.Ordinal)
            .Take(Mathf.Clamp(maximumCount, 1, 100))
            .Select(entry => new CharacterMealLedgerSaveData
            {
                mealId = entry.mealId,
                characterId = entry.characterId,
                facilityId = entry.facilityId,
                itemId = entry.itemId,
                displayName = entry.displayName,
                dietClass = entry.dietClass,
                quality = entry.quality,
                nutrition = entry.nutrition,
                policyViolation = entry.policyViolation,
                contaminated = entry.contaminated,
                day = entry.day,
                amount = entry.amount
            })
            .ToArray();
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
        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
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
        IReadOnlyList<CharacterActor> actors =
            worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
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
        return CountStoredStock(StockCategory.Food);
    }

    private int CountLooseFood()
    {
        return CountLooseStock(StockCategory.Food);
    }

    private int CountCarcasses(out int pendingFood)
    {
        pendingFood = 0;
        if (itemStackRuntime == null)
        {
            return 0;
        }

        int count = 0;
        IReadOnlyList<WorldItemStackSnapshot> stacks =
            GetCachedItemStacks();
        string foodItemId =
            DungeonItemCatalogSO.StockItemId(StockCategory.Food);
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
                        && string.Equals(
                            yieldItem.itemId,
                            foodItemId,
                            StringComparison.Ordinal))
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
        state ??= new DungeonSurvivalSaveData();
        state.spoilage ??= new List<SurvivalFoodSpoilageSaveData>();
        state.health ??= new List<SurvivalHealthSaveData>();
        state.mealLedger ??= new List<CharacterMealLedgerSaveData>();
    }

    private void TrimMealLedger()
    {
        const int maximumMealEntries = 512;
        int removeCount = state.mealLedger.Count - maximumMealEntries;
        if (removeCount > 0)
        {
            state.mealLedger.RemoveRange(0, removeCount);
        }
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
        EnsureStateLists();
        string characterId = consumer.Identity?.PersistentId?.Trim();
        if (string.IsNullOrWhiteSpace(characterId))
        {
            characterId = $"scene-character:{consumer.GetInstanceID()}";
        }

        int day = Mathf.Max(1, state.lastProcessedDay);
        const int amount = 1;
        string facilityId =
            $"building:{facility.BuildingData?.id ?? facility.id}:{facility.centerPos.x}:{facility.centerPos.y}";
        string mealId = $"meal:{day}:{characterId}:{++mealSequence}";
        state.mealLedger.Add(new CharacterMealLedgerSaveData
        {
            mealId = mealId,
            characterId = characterId,
            facilityId = facilityId ?? string.Empty,
            itemId = itemId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            dietClass = dietClass,
            quality = quality,
            nutrition = Mathf.Max(0f, nutrition),
            policyViolation = policyViolation,
            contaminated = contaminated,
            day = day,
            amount = amount
        });
        TrimMealLedger();
        RefreshCurrentDayFoodSummary();
        InvalidateOverviewCache();

        gameEventBus.Publish(new CharacterMealConsumedEvent(
            mealId,
            characterId,
            facilityId,
            itemId,
            displayName,
            dietClass,
            quality,
            nutrition,
            policyViolation,
            contaminated,
            day,
            amount));
    }

    private void UpdateWeather(int day)
    {
        if (state.weatherDay == day)
        {
            return;
        }

        int roll = Mathf.Abs((day * 73) + 17) % 100;
        SurvivalWeatherType previous = state.currentWeather;
        state.currentWeather = roll switch
        {
            < 10 => SurvivalWeatherType.Storm,
            < 24 => SurvivalWeatherType.Rain,
            < 34 => SurvivalWeatherType.Fog,
            < 44 => SurvivalWeatherType.ColdSnap,
            < 54 => SurvivalWeatherType.HeatWave,
            _ => SurvivalWeatherType.Clear
        };
        state.weatherDay = day;
        state.outdoorTemperature = state.currentWeather switch
        {
            SurvivalWeatherType.ColdSnap => -6f,
            SurvivalWeatherType.HeatWave => 34f,
            SurvivalWeatherType.Storm => 12f,
            SurvivalWeatherType.Rain => 14f,
            SurvivalWeatherType.Fog => 16f,
            _ => 18f
        };

        if (state.currentWeather != previous
            && (state.currentWeather == SurvivalWeatherType.ColdSnap
                || state.currentWeather == SurvivalWeatherType.HeatWave
                || state.currentWeather == SurvivalWeatherType.Storm))
        {
            gameEventBus.RaiseAlert(
                "날씨가 위험해집니다",
                $"{FormatWeather(state.currentWeather)} 예보입니다. 연료, 조명, 외부 작업 상태를 확인하세요.",
                EventAlertImportance.Medium,
                "생존");
        }
    }

    private void ProcessSpoilage(bool advanceTime = false)
    {
        EnsureStateLists();
        if (itemStackRuntime == null)
        {
            state.spoilage.Clear();
            return;
        }

        WorldItemStackSnapshot[] stacks = GetCachedItemStacks()
            .Where(stack => stack != null && stack.State != WorldItemStackState.Carried)
            .ToArray();
        HashSet<string> validStackIds = new HashSet<string>(
            stacks.Select(stack => stack.StackId),
            StringComparer.Ordinal);

        foreach (WorldItemStackSnapshot stack in stacks)
        {
            if (ShouldTrackSpoilage(stack.ItemId))
            {
                TrackSpoilageIfNeeded(stack);
            }
        }

        state.spoilage.RemoveAll(entry => entry == null
            || string.IsNullOrWhiteSpace(entry.stackId)
            || !validStackIds.Contains(entry.stackId));

        if (!advanceTime)
        {
            return;
        }

        float weatherMultiplier = state.currentWeather == SurvivalWeatherType.HeatWave
            ? 1.35f
            : state.currentWeather == SurvivalWeatherType.ColdSnap
                ? 0.45f
                : 1f;
        float dailyDelta = 180f * weatherMultiplier;
        List<SurvivalFoodSpoilageSaveData> expired = null;
        foreach (SurvivalFoodSpoilageSaveData entry in state.spoilage)
        {
            if (entry.preserved)
            {
                entry.remainingFreshnessSeconds -= dailyDelta * 0.25f;
            }
            else
            {
                entry.remainingFreshnessSeconds -= dailyDelta;
            }

            if (entry.remainingFreshnessSeconds <= 0f || entry.contaminated)
            {
                expired ??= new List<SurvivalFoodSpoilageSaveData>();
                expired.Add(entry);
            }
        }

        if (expired == null)
        {
            return;
        }

        foreach (SurvivalFoodSpoilageSaveData entry in expired)
        {
            WorldItemStackSnapshot stack = stacks.FirstOrDefault(candidate =>
                string.Equals(candidate.StackId, entry.stackId, StringComparison.Ordinal));
            state.spoilage.Remove(entry);
            if (stack == null)
            {
                continue;
            }

            Vector2Int position = stack.Position;
            int rotAmount = Mathf.Max(1, stack.Quantity);
            ResolveSpoilageWaste(
                entry.itemId,
                out string wasteItemId,
                out WasteOriginKind wasteOrigin);
            float contamination = entry.contaminated ? 90f : 50f;
            itemStackRuntime.DeleteStack(stack.StackId);
            itemStackRuntime.SpawnWasteAt(
                wasteItemId,
                rotAmount,
                position,
                wasteOrigin,
                contamination,
                out _);
        }
    }

    private void ConsumeDailyWater(int day)
    {
        int need = GetSurvivalConsumers().Count();
        int available = CountStoredStock(StockCategory.Water) + CountLooseStock(StockCategory.Water);
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
        if (state.currentWeather == SurvivalWeatherType.ColdSnap)
        {
            need += 1;
        }

        int consumed = WithdrawStock(StockCategory.Fuel, need);
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
        int rotStacks = CountLooseRotStacks();
        RefreshBuildingRiskContributionsIfNeeded();
        state.sanitationRisk = Mathf.Clamp(
            (rotStacks * 12f)
            + (state.lastMissingWater * 8f)
            - cachedVentilationBonus,
            0f,
            100f);
        state.diseaseRisk = Mathf.Clamp(
            (state.sanitationRisk * 0.55f)
            + (state.consecutiveFoodShortageDays * 7f)
            + (state.consecutiveWaterShortageDays * 12f),
            0f,
            100f);
        float weatherDanger = state.currentWeather switch
        {
            SurvivalWeatherType.Storm => 35f,
            SurvivalWeatherType.Fog => 25f,
            SurvivalWeatherType.Rain => 18f,
            SurvivalWeatherType.ColdSnap => 16f,
            _ => 10f
        };
        state.exteriorNightDanger = Mathf.Clamp(
            weatherDanger
            + (state.lastMissingFuel * 18f)
            + (rotStacks * 4f)
            - cachedLightSafety,
            0f,
            100f);
    }

    private void RefreshBuildingRiskContributionsIfNeeded()
    {
        int buildingVersion = worldRegistry?.BuildingVersion ?? -1;
        if (cachedRiskBuildingVersion == buildingVersion)
        {
            return;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        cachedRiskBuildingVersion = buildingVersion;
        cachedVentilationBonus = 0f;
        cachedLightSafety = 0f;
        IReadOnlyList<BuildableObject> registeredBuildings =
            worldRegistry?.Buildings ?? Array.Empty<BuildableObject>();
        if (registeredBuildings.Count > 0)
        {
            for (int i = 0; i < registeredBuildings.Count; i++)
            {
                AccumulateBuildingRiskContribution(registeredBuildings[i], grid);
            }

            return;
        }

        foreach (IGridOccupant occupant in grid.FindAllOccupants(null))
        {
            if (occupant is BuildableObject building)
            {
                AccumulateBuildingRiskContribution(building, grid);
            }
        }
    }

    private void AccumulateBuildingRiskContribution(
        BuildableObject building,
        Grid grid)
    {
        if (building == null
            || building.Grid != grid
            || building.isDestroy
            || building.BuildingData == null)
        {
            return;
        }

        BuildingVentilationAbility ventilation =
            building.BuildingData.GetAbility<BuildingVentilationAbility>();
        if (ventilation != null)
        {
            cachedVentilationBonus += ventilation.hygieneRiskReduction;
        }

        BuildingFuelConsumerAbility fuelConsumer =
            building.BuildingData.GetAbility<BuildingFuelConsumerAbility>();
        if (fuelConsumer != null)
        {
            cachedLightSafety += fuelConsumer.lightSafety;
        }
    }

    private static float GetTemperatureComfort01(float temperature)
    {
        float distanceFromComfort = Mathf.Abs(temperature - 20f);
        return Mathf.Clamp01(1f - (distanceFromComfort / 22f));
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

        RegisterOrRefreshHealth(patient, SurvivalHealthState.Sick, state.diseaseRisk / 100f, 360f, "sanitation-risk");
        patient.ApplyMoodFactor(
            "survival:sick",
            "몸 상태가 좋지 않음",
            -4f,
            240f,
            1);
    }

    private bool TryApplyDrawWater(
        CharacterActor actor,
        BuildableObject building,
        out int amount,
        out string message)
    {
        amount = 0;
        BuildingWaterSourceAbility ability = building.BuildingData?.GetAbility<BuildingWaterSourceAbility>();
        if (ability == null)
        {
            message = "물을 얻을 수 있는 시설이 아닙니다.";
            return false;
        }

        if (!CanDrawWater(building))
        {
            message = "추위 때문에 물길이 막혔습니다.";
            return false;
        }

        amount = Mathf.Max(1, ability.waterPerWork);
        bool spawned = itemStackRuntime != null
            && itemStackRuntime.SpawnItemAt(
                DungeonItemCatalogSO.StockItemId(StockCategory.Water),
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

        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.DrawWater,
            amount > 0 ? CharacterActivityOutcomes.Completed : CharacterActivityOutcomes.Failed,
            amount > 0
                ? $"{GetBuildingName(building)}에서 물 {amount}개를 길었다."
                : "물을 담을 곳을 찾지 못했다.",
            building,
            reasonCode: amount > 0 ? "water-drawn" : "water-output-failed",
            quantity: amount,
            bubbleEligible: amount <= 0));
        message = amount > 0 ? "물을 길었습니다." : "물 생산 실패";
        return amount > 0;
    }

    private bool TryApplyCook(
        CharacterActor actor,
        BuildableObject building,
        out int amount,
        out string message)
    {
        amount = 0;
        BuildingCookingAbility cooking = building.BuildingData?.GetAbility<BuildingCookingAbility>();
        if (cooking == null)
        {
            message = "조리 가능한 시설이 아닙니다.";
            return false;
        }

        int input = Mathf.Max(1, cooking.inputFood);
        if (CountStoredStock(StockCategory.Food) < input)
        {
            message = "조리할 식재료가 부족합니다.";
            return false;
        }

        if (cooking.requiresFuel && CountStoredStock(StockCategory.Fuel) <= 0)
        {
            message = "조리에 쓸 연료가 부족합니다.";
            return false;
        }

        WithdrawStock(StockCategory.Food, input);
        if (cooking.requiresFuel)
        {
            WithdrawStock(StockCategory.Fuel, 1);
        }

        BuildingPreservationAbility preservation = FindRoomPreservationAbility(building);
        string outputId = preservation != null
            ? SurvivalItemDefinitions.PreservedFoodItemId
            : SurvivalItemDefinitions.CookedMealItemId;
        amount = preservation != null
            ? Mathf.Max(1, preservation.preservedMealsPerCook)
            : Mathf.Max(1, cooking.cookedMeals);
        bool spawned = itemStackRuntime != null
            && itemStackRuntime.SpawnItemAt(
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
        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Cook,
            CharacterActivityOutcomes.Completed,
            preservation != null
                ? $"{GetBuildingName(building)}에서 오래 둘 수 있는 보존 식량을 만들었다."
                : $"{GetBuildingName(building)}에서 따뜻한 식사를 만들었다.",
            building,
            reasonCode: preservation != null ? "food-preserved" : "food-cooked",
            quantity: amount));
        message = "조리를 완료했습니다.";
        return true;
    }

    private bool TryApplyTreat(
        CharacterActor actor,
        BuildableObject building,
        out int amount,
        out string message)
    {
        amount = 0;
        BuildingMedicalAbility medical = building.BuildingData?.GetAbility<BuildingMedicalAbility>();
        if (medical == null)
        {
            message = "치료 가능한 시설이 아닙니다.";
            return false;
        }

        SurvivalHealthSaveData patientEntry = FindMostSevereHealthEntry();
        if (patientEntry == null)
        {
            message = "치료할 대상이 없습니다.";
            return false;
        }

        bool usedBloodSubstitute = false;
        if (medical.requiresMedicine
            && !TryConsumeTreatmentMaterial(out usedBloodSubstitute))
        {
            message = "약품이 부족합니다.";
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

        CharacterActor patient = FindActorByPersistentId(patientEntry.persistentId);
        patient?.Heal(TreatmentMedicineHeal * treatmentEfficiency);
        if (usedBloodSubstitute && patient != null)
        {
            RegisterOrRefreshHealth(
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
        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Treat,
            CharacterActivityOutcomes.Completed,
            $"{GetBuildingName(building)}에서 {GetActorName(patient, patientEntry.persistentId)}의 상태를 살폈다.",
            building,
            reasonCode: "survival-treated"));
        amount = 1;
        message = "치료를 완료했습니다.";
        return true;
    }

    private bool TryApplyRefuel(
        CharacterActor actor,
        BuildableObject building,
        out int amount,
        out string message)
    {
        amount = 0;
        BuildingFuelConsumerAbility fuel = building.BuildingData?.GetAbility<BuildingFuelConsumerAbility>();
        if (fuel == null)
        {
            message = "연료를 쓰는 시설이 아닙니다.";
            return false;
        }

        int needed = Mathf.Max(1, fuel.fuelPerRefuel);
        amount = WithdrawStock(StockCategory.Fuel, needed);
        if (amount <= 0)
        {
            message = "연료가 부족합니다.";
            return false;
        }

        state.lastMissingFuel = 0;
        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Refuel,
            CharacterActivityOutcomes.Completed,
            $"{GetBuildingName(building)}에 연료를 보충했다.",
            building,
            reasonCode: "survival-refueled",
            quantity: amount));
        message = "연료를 보충했습니다.";
        return true;
    }

    private static BuildingPreservationAbility FindRoomPreservationAbility(BuildableObject building)
    {
        if (building == null)
        {
            return null;
        }

        try
        {
            return building.GetRoomOperationalProfile()
                .Parts
                .Where(part => part != null && part.BuildingData != null)
                .Select(part => part.BuildingData.GetAbility<BuildingPreservationAbility>())
                .FirstOrDefault(ability => ability != null);
        }
        catch (InvalidOperationException)
        {
            return building.BuildingData?.GetAbility<BuildingPreservationAbility>();
        }
    }

    private int CountStoredStock(StockCategory category)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return 0;
        }

        IReadOnlyList<IWarehouseFacility> warehouses =
            worldRegistry?.Warehouses ?? Array.Empty<IWarehouseFacility>();
        if (warehouses.Count > 0)
        {
            int total = 0;
            for (int index = 0; index < warehouses.Count; index++)
            {
                IWarehouseFacility warehouse = warehouses[index];
                if (IsWarehouseOnGrid(warehouse, grid)
                    && warehouse.HasWarehouseInventory
                    && warehouse.Inventory != null)
                {
                    total += warehouse.Inventory.GetStock(category);
                }
            }

            return total;
        }

        return grid.FindAllOccupants(null)
            .OfType<IWarehouseFacility>()
            .Where(warehouse => warehouse != null && warehouse.HasWarehouseInventory && warehouse.Inventory != null)
            .Sum(warehouse => warehouse.Inventory.GetStock(category));
    }

    private int CountLooseStock(StockCategory category)
    {
        if (itemStackRuntime == null)
        {
            return 0;
        }

        int total = 0;
        IReadOnlyList<WorldItemStackSnapshot> stacks =
            GetCachedItemStacks();
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (stack != null
                && !stack.Forbidden
                && stack.State != WorldItemStackState.Carried
                && DungeonItemCatalogSO.TryGetStockCategoryFromItemId(
                    stack.ItemId,
                    out StockCategory parsed)
                && parsed == category
                && !SurvivalItemDefinitions.IsContaminated(stack.ItemId))
            {
                total += stack.Quantity;
            }
        }

        return total;
    }

    private int WithdrawStock(StockCategory category, int amount)
    {
        if (amount <= 0 || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return 0;
        }

        int remaining = amount;
        int withdrawn = 0;
        IEnumerable<IWarehouseFacility> warehouses = worldRegistry != null
            && worldRegistry.Warehouses.Count > 0
            ? worldRegistry.Warehouses.Where(warehouse => IsWarehouseOnGrid(warehouse, grid))
            : grid.FindAllOccupants(null).OfType<IWarehouseFacility>();
        foreach (IWarehouseFacility warehouse in warehouses
                     .Where(warehouse => warehouse != null
                         && warehouse.HasWarehouseInventory
                         && warehouse.Inventory != null))
        {
            if (remaining <= 0)
            {
                break;
            }

            int amountFromWarehouse = warehouse.Inventory.Withdraw(category, remaining);
            remaining -= amountFromWarehouse;
            withdrawn += amountFromWarehouse;
        }

        return withdrawn;
    }

    private bool CanDrawWater(BuildableObject building)
    {
        BuildingWaterSourceAbility ability = building?.BuildingData?.GetAbility<BuildingWaterSourceAbility>();
        return ability != null
            && (!ability.blockedByFreezingWeather || state.currentWeather != SurvivalWeatherType.ColdSnap);
    }

    private bool TryConsumeTreatmentMaterial(out bool usedBloodSubstitute)
    {
        usedBloodSubstitute = false;
        if (WithdrawStock(StockCategory.Medicine, 1) > 0)
        {
            return true;
        }

        if (WithdrawStock(StockCategory.Biological, 1) <= 0)
        {
            return false;
        }

        usedBloodSubstitute = true;
        return true;
    }

    private bool HasTreatableHealth()
    {
        EnsureStateLists();
        return state.health.Any(entry => entry != null
            && entry.remainingSeconds > 0f
            && (entry.state == SurvivalHealthState.Sick
                || entry.state == SurvivalHealthState.Infected
                || entry.state == SurvivalHealthState.Exposed
                || entry.state == SurvivalHealthState.Recovering));
    }

    private SurvivalHealthSaveData FindMostSevereHealthEntry()
    {
        EnsureStateLists();
        return state.health
            .Where(entry => entry != null
                && entry.remainingSeconds > 0f
                && entry.state != SurvivalHealthState.Healthy)
            .OrderByDescending(entry => entry.state == SurvivalHealthState.Infected ? 1 : 0)
            .ThenByDescending(entry => entry.severity)
            .FirstOrDefault();
    }

    private void RegisterOrRefreshHealth(
        CharacterActor actor,
        SurvivalHealthState healthState,
        float severity,
        float durationSeconds,
        string source)
    {
        if (actor == null)
        {
            return;
        }

        EnsureStateLists();
        string persistentId = actor.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            persistentId = actor.name;
        }

        SurvivalHealthSaveData entry = state.health.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.persistentId, persistentId, StringComparison.Ordinal)
            && candidate.state == healthState);
        if (entry == null)
        {
            state.health.Add(new SurvivalHealthSaveData
            {
                persistentId = persistentId,
                state = healthState,
                severity = Mathf.Clamp01(severity),
                remainingSeconds = Mathf.Max(1f, durationSeconds),
                source = source ?? string.Empty
            });
            return;
        }

        entry.severity = Mathf.Clamp01(Mathf.Max(entry.severity, severity));
        entry.remainingSeconds = Mathf.Max(entry.remainingSeconds, durationSeconds);
        entry.source = source ?? entry.source;
    }

    private bool HasActiveHealth(CharacterActor actor, SurvivalHealthState healthState)
    {
        string persistentId = actor?.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return false;
        }

        return state.health.Any(entry => entry != null
            && entry.state == healthState
            && entry.remainingSeconds > 0f
            && string.Equals(entry.persistentId, persistentId, StringComparison.Ordinal));
    }

    private CharacterActor FindActorByPersistentId(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return null;
        }

        return GetSurvivalConsumers().FirstOrDefault(actor =>
            string.Equals(actor.Identity?.PersistentId, persistentId, StringComparison.Ordinal)
            || string.Equals(actor.name, persistentId, StringComparison.Ordinal));
    }

    private void TrackSpoilageIfNeeded(WorldItemStackSnapshot stack)
    {
        if (stack == null
            || string.IsNullOrWhiteSpace(stack.StackId)
            || state.spoilage.Any(entry => entry != null
                && string.Equals(entry.stackId, stack.StackId, StringComparison.Ordinal)))
        {
            return;
        }

        state.spoilage.Add(CreateSpoilageEntry(stack.StackId, stack.ItemId));
    }

    private SurvivalFoodSpoilageSaveData CreateSpoilageEntry(string stackId, string itemId)
    {
        bool preserved = resourceCatalog?.TryGetItem(
                itemId?.Trim() ?? string.Empty,
                out ResourceItemDefinitionSO definition) == true
            ? definition.Preserved
            : SurvivalItemDefinitions.IsPreserved(itemId);
        return new SurvivalFoodSpoilageSaveData
        {
            stackId = stackId ?? string.Empty,
            itemId = itemId ?? string.Empty,
            preserved = preserved,
            contaminated = SurvivalItemDefinitions.IsContaminated(itemId),
            remainingFreshnessSeconds = GetBaseFreshnessSeconds(itemId, preserved)
        };
    }

    private bool ShouldTrackSpoilage(string itemId)
    {
        bool isFood = resourceCatalog?.TryGetItem(
                itemId?.Trim() ?? string.Empty,
                out ResourceItemDefinitionSO definition) == true
            ? definition.IsMeal
            : SurvivalItemDefinitions.IsFoodLike(itemId);
        return isFood
            && !string.Equals(
                itemId?.Trim(),
                DungeonItemCatalogSO.StockItemId(StockCategory.Food),
                StringComparison.Ordinal)
            && !SurvivalItemDefinitions.IsContaminated(itemId);
    }

    private float GetBaseFreshnessSeconds(string itemId, bool preserved)
    {
        if (resourceCatalog?.TryGetItem(
                itemId?.Trim() ?? string.Empty,
                out ResourceItemDefinitionSO definition) == true
            && definition.FreshnessSeconds > 0f)
        {
            return definition.FreshnessSeconds;
        }

        return preserved ? PreservedFreshnessSeconds : DefaultFreshnessSeconds;
    }

    private int CountSpoilageWarnings()
    {
        EnsureStateLists();
        int count = 0;
        for (int index = 0; index < state.spoilage.Count; index++)
        {
            SurvivalFoodSpoilageSaveData entry =
                state.spoilage[index];
            if (entry != null
                && (entry.contaminated
                    || entry.remainingFreshnessSeconds
                        <= FreshnessWarningThresholdSeconds))
            {
                count++;
            }
        }

        return count;
    }

    private int CountLooseRotStacks()
    {
        if (itemStackRuntime == null)
        {
            return 0;
        }

        int count = 0;
        IReadOnlyList<WorldItemStackSnapshot> stacks =
            GetCachedItemStacks();
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (stack != null
                && !stack.Forbidden
                && stack.State != WorldItemStackState.Carried
                && (stack.IsWaste
                    || stack.ItemId.StartsWith(
                        "waste:",
                        StringComparison.Ordinal)
                    || string.Equals(
                        stack.ItemId,
                        WildlifeItemDefinitions.RotItemId,
                        StringComparison.Ordinal)))
            {
                count++;
            }
        }

        return count;
    }

    private void ResolveSpoilageWaste(
        string sourceItemId,
        out string wasteItemId,
        out WasteOriginKind origin)
    {
        ResourceIngredientTag tags = resourceCatalog?.TryGetItem(
                sourceItemId?.Trim() ?? string.Empty,
                out ResourceItemDefinitionSO definition) == true
            ? definition.IngredientTags
            : ResourceIngredientTag.None;
        bool forbidden = (tags & ResourceIngredientTag.Forbidden) != 0;
        bool plant = (tags & (ResourceIngredientTag.Plant
            | ResourceIngredientTag.Fungus)) != 0;
        bool animal = (tags & (ResourceIngredientTag.Meat
            | ResourceIngredientTag.Blood
            | ResourceIngredientTag.Fat
            | ResourceIngredientTag.Milk
            | ResourceIngredientTag.Egg)) != 0;

        if (forbidden)
        {
            origin = WasteOriginKind.Forbidden;
            wasteItemId = "waste:forbidden-rot";
            return;
        }

        if (plant && !animal)
        {
            origin = WasteOriginKind.Plant;
            wasteItemId = "waste:plant-rot";
            return;
        }

        if (animal && !plant)
        {
            origin = WasteOriginKind.Animal;
            wasteItemId = "waste:animal-rot";
            return;
        }

        origin = WasteOriginKind.Mixed;
        wasteItemId = "waste:mixed-rot";
    }

    private float SumBuildingAbilityValue<TAbility>(Func<TAbility, float> selector)
        where TAbility : BuildingAbility
    {
        if (selector == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return 0f;
        }

        IReadOnlyList<BuildableObject> registeredBuildings =
            worldRegistry?.Buildings ?? Array.Empty<BuildableObject>();
        IEnumerable<BuildableObject> buildings = registeredBuildings.Count > 0
            ? registeredBuildings.Where(building => building != null && building.Grid == grid)
            : grid.FindAllOccupants(null).OfType<BuildableObject>();

        return buildings
            .Where(building => building != null && !building.isDestroy && building.BuildingData != null)
            .Select(building => building.BuildingData.GetAbility<TAbility>())
            .Where(ability => ability != null)
            .Sum(selector);
    }

    private static bool IsWarehouseOnGrid(IWarehouseFacility warehouse, Grid grid)
    {
        if (warehouse == null)
        {
            return false;
        }

        BuildableObject building = warehouse as BuildableObject;
        return building == null || building.Grid == grid;
    }

    private IReadOnlyList<WorldItemStackSnapshot> GetCachedItemStacks()
    {
        IWorldItemStackRuntime runtime = itemStackRuntime;
        if (runtime == null)
        {
            cachedItemStackVersion = -1;
            cachedItemStacks = Array.Empty<WorldItemStackSnapshot>();
            return cachedItemStacks;
        }

        if (cachedItemStackVersion == runtime.ItemStackVersion)
        {
            return cachedItemStacks;
        }

        cachedItemStackVersion = runtime.ItemStackVersion;
        cachedItemStacks = runtime.GetAllStacks();
        return cachedItemStacks;
    }

    private static string FormatWeather(SurvivalWeatherType weather)
    {
        return weather switch
        {
            SurvivalWeatherType.Rain => "비",
            SurvivalWeatherType.Fog => "안개",
            SurvivalWeatherType.HeatWave => "폭염",
            SurvivalWeatherType.ColdSnap => "한파",
            SurvivalWeatherType.Storm => "폭우",
            _ => "맑음"
        };
    }

    private static string GetBuildingName(BuildableObject building)
    {
        return string.IsNullOrWhiteSpace(building?.BuildingData?.objectName)
            ? building != null ? building.name : "시설"
            : building.BuildingData.objectName;
    }

    private static string GetActorName(CharacterActor actor, string fallback)
    {
        return actor != null && !string.IsNullOrWhiteSpace(actor.name)
            ? actor.name
            : string.IsNullOrWhiteSpace(fallback) ? "대상" : fallback;
    }
}
