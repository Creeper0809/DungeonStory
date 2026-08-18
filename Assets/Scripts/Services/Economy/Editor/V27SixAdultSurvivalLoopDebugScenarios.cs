#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

[BalanceCaptureFactory]
public static class V27SixAdultSurvivalLoopDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-six-adult-food-water-loop.txt";
    public const string ContinuityPath =
        "Artifacts/QA/v27-balance-service-continuity.csv";
    public const string StagePortfolioPath =
        "Artifacts/QA/v27-balance-stage-portfolios.csv";

    private const string MealRecipePath =
        "Assets/Resources/SO/Economy/Recipes/recipe_grain_porridge.asset";
    private const string WaterRecipePath =
        "Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R01_깨끗한_물.asset";
    private const string CropPath =
        "Assets/Resources/SO/Economy/Crops/crop_twilight_grain.asset";
    private const string SurvivalSettingsPath =
        "Assets/Resources/SO/Survival/SurvivalBalanceSettings.asset";

    [MenuItem("DungeonStory/V27/Verify Six Adult Food Water Closed Loop")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        Debug.Log(report);
    }

    public static string RunAll()
    {
        ProductionRecipeSO mealRecipe = RequireAsset<ProductionRecipeSO>(MealRecipePath);
        ProductionRecipeSO waterRecipe = RequireAsset<ProductionRecipeSO>(WaterRecipePath);
        CropDefinitionSO crop = RequireAsset<CropDefinitionSO>(CropPath);
        SurvivalBalanceSettingsSO settings =
            RequireAsset<SurvivalBalanceSettingsSO>(SurvivalSettingsPath);
        ResourceEconomyContentCatalog content = new(
            LoadAll<ResourceItemDefinitionSO>(),
            LoadAll<ProductionRecipeSO>(),
            LoadAll<CropDefinitionSO>(),
            LoadAll<CraftMaterialDefinitionSO>());

        ResourceItemDefinitionSO meal = RequireItem(content, "food:grain-porridge");
        ResourceItemDefinitionSO grain = RequireItem(content, "resource:twilight-grain");
        ResourceItemDefinitionSO water = RequireItem(content, "resource:clean-water");
        ItemAmountDefinition mealInput = RequireSingle(mealRecipe.Inputs, "meal input");
        ProductionOutputDefinition mealOutput = RequireSingle(
            mealRecipe.Outputs,
            "meal output");
        ProductionOutputDefinition waterOutput = RequireSingle(
            waterRecipe.Outputs,
            "water output");
        Require(mealInput.ItemId == grain.ItemId && mealInput.Amount == 6,
            "Grain porridge must consume exactly six twilight grain per batch.");
        Require(mealOutput.ItemId == meal.ItemId && mealOutput.Amount == 6,
            "Grain porridge must produce exactly six physical meals per batch.");
        Require(Exact(mealRecipe.RequiredWork, 28f),
            "Grain porridge recurring work must be 28 WU per six-meal batch.");
        Require(waterRecipe.Inputs.Count == 0
            && waterOutput.ItemId == water.ItemId
            && waterOutput.Amount == 8
            && Exact(waterRecipe.RequiredWork, 10f),
            "Clean-water source must produce eight units for 10 recurring WU.");
        Require(Exact(crop.GrowthHours, 36f)
            && Exact(crop.SowWork, 3f)
            && Exact(crop.HarvestWork, 6f)
            && Exact(crop.DailyWater, 0.35f)
            && crop.Yield == 6,
            "Twilight-grain crop authority drifted from its recurring-throughput target.");
        Require(settings.TryGetNeed(
                CharacterCondition.HUNGER,
                out CharacterNeedBalanceEntry hunger),
            "Hunger depletion authority is missing.");
        Require(settings.TryGetNeed(
                CharacterCondition.THIRST,
                out CharacterNeedBalanceEntry thirst),
            "Thirst depletion authority is missing.");

        SurvivalClosedLoopDefinition sixAdults = Definition(
            6,
            hunger,
            thirst,
            mealRecipe,
            mealInput,
            mealOutput,
            waterRecipe,
            waterOutput,
            crop,
            meal,
            grain,
            water);
        SurvivalClosedLoopAssessment result =
            SurvivalClosedLoopCalculator.Assess(sixAdults);
        VerifySixAdultResult(result);

        SurvivalContinuityCatalogQuery continuity = new(
            content,
            new SettingsNeedRuntime(settings));
        IReadOnlyList<SurvivalContinuityPathSnapshot> paths =
            continuity.CapturePaths(new PopulationStageContext(6, "tier:0"));
        VerifyContinuity(paths);

        WriteContinuity(paths);
        WriteStages(
            new[] { 1, 3, 6, 12, 18, 24 },
            hunger,
            thirst,
            mealRecipe,
            mealInput,
            mealOutput,
            waterRecipe,
            waterOutput,
            crop,
            meal,
            grain,
            water);
        string report = BuildReport(result, paths);
        WriteText(ReportPath, report);
        return report;
    }

    public static SurvivalClosedLoopAssessment CapturePopulationStage(
        int population)
    {
        ProductionRecipeSO mealRecipe = RequireAsset<ProductionRecipeSO>(MealRecipePath);
        ProductionRecipeSO waterRecipe = RequireAsset<ProductionRecipeSO>(WaterRecipePath);
        CropDefinitionSO crop = RequireAsset<CropDefinitionSO>(CropPath);
        SurvivalBalanceSettingsSO settings =
            RequireAsset<SurvivalBalanceSettingsSO>(SurvivalSettingsPath);
        ResourceEconomyContentCatalog content = new(
            LoadAll<ResourceItemDefinitionSO>(),
            LoadAll<ProductionRecipeSO>(),
            LoadAll<CropDefinitionSO>(),
            LoadAll<CraftMaterialDefinitionSO>());
        ResourceItemDefinitionSO meal = RequireItem(content, "food:grain-porridge");
        ResourceItemDefinitionSO grain = RequireItem(content, "resource:twilight-grain");
        ResourceItemDefinitionSO water = RequireItem(content, "resource:clean-water");
        if (!settings.TryGetNeed(CharacterCondition.HUNGER, out CharacterNeedBalanceEntry hunger)
            || !settings.TryGetNeed(CharacterCondition.THIRST, out CharacterNeedBalanceEntry thirst))
        {
            throw new InvalidOperationException(
                "Population-stage survival need authority is incomplete.");
        }
        return SurvivalClosedLoopCalculator.Assess(Definition(
            population,
            hunger,
            thirst,
            mealRecipe,
            RequireSingle(mealRecipe.Inputs, "meal input"),
            RequireSingle(mealRecipe.Outputs, "meal output"),
            waterRecipe,
            RequireSingle(waterRecipe.Outputs, "water output"),
            crop,
            meal,
            grain,
            water));
    }

    public static IReadOnlyList<SurvivalContinuityPathSnapshot>
        CaptureContinuityPaths(int population)
    {
        SurvivalBalanceSettingsSO settings =
            RequireAsset<SurvivalBalanceSettingsSO>(SurvivalSettingsPath);
        ResourceEconomyContentCatalog content = new(
            LoadAll<ResourceItemDefinitionSO>(),
            LoadAll<ProductionRecipeSO>(),
            LoadAll<CropDefinitionSO>(),
            LoadAll<CraftMaterialDefinitionSO>());
        return new SurvivalContinuityCatalogQuery(
                content,
                new SettingsNeedRuntime(settings))
            .CapturePaths(new PopulationStageContext(
                population,
                "tier:" + PopulationStagePortfolioCatalog
                    .TierForPopulation(population)
                    .ToString(CultureInfo.InvariantCulture)));
    }

    private static SurvivalClosedLoopDefinition Definition(
        int population,
        CharacterNeedBalanceEntry hunger,
        CharacterNeedBalanceEntry thirst,
        ProductionRecipeSO mealRecipe,
        ItemAmountDefinition mealInput,
        ProductionOutputDefinition mealOutput,
        ProductionRecipeSO waterRecipe,
        ProductionOutputDefinition waterOutput,
        CropDefinitionSO crop,
        ResourceItemDefinitionSO meal,
        ResourceItemDefinitionSO grain,
        ResourceItemDefinitionSO water) => new(
        population,
        Milli(hunger.dailyDepletion),
        Milli(thirst.dailyDepletion),
        Milli(meal.Nutrition),
        mealInput.Amount,
        mealOutput.Amount,
        Milli(mealRecipe.RequiredWork),
        Milli(crop.GrowthHours),
        Milli(crop.SowWork),
        Milli(crop.HarvestWork),
        crop.Yield,
        Milli(crop.DailyWater),
        waterOutput.Amount,
        Milli(waterRecipe.RequiredWork),
        Milli(mealRecipe.CleanWaterPerCycle),
        meal.MaxStack,
        grain.MaxStack,
        water.MaxStack);

    private static void VerifySixAdultResult(SurvivalClosedLoopAssessment value)
    {
        Require(value.Passed, value.FailureCode);
        Require(value.DailyFoodDemandMilliNutrition == 300000
            && value.GrossFoodTargetMilliNutrition == 375000
            && value.NetFoodTargetMilliNutrition == 330000
            && value.GrossFoodProducedMilliNutrition == 420000
            && value.NetFoodProducedMilliNutrition == 399000
            && value.GrossFoodCoveragePermille == 1400
            && value.NetFoodCoveragePermille == 1330,
            "Six-adult food demand targets are not exact.");
        Require(value.GrossMealMilliUnitsPerDay == 10715
            && value.CropPlots == 3
            && value.GrossGrainMilliUnitsPerDay == 12000,
            "Six-adult crop and meal throughput is not exact.");
        Require(value.CropMilliWuPerDay == 18000
            && value.CookingMilliWuPerDay == 50008
            && value.WaterMilliWuPerDay == 10530
            && value.RecurringMilliWuPerDay == 78538
            && value.RecurringSharePermille == 291,
            "Six-adult recurring WU closure drifted.");
        Require(value.DrinkingWaterDemandMilliUnitsPerDay == 5539
            && value.GrossDrinkingWaterMilliUnitsPerDay == 6924
            && value.GrossDrinkingWaterCoveragePermille == 1251
            && value.TotalWaterMilliUnitsPerDay == 8421,
            "Six-adult clean-water demand or gross target drifted.");
        Require(value.ImmediateMealUnits == 12
            && value.SevenDayGrainUnits == 60
            && value.SevenDayWaterUnits == 59
            && value.StorageCells == 4,
            "Seven-day physical reserve or immediate meal buffer drifted.");
    }

    private static void VerifyContinuity(
        IReadOnlyList<SurvivalContinuityPathSnapshot> paths)
    {
        Require(paths.Count == 10,
            "Five survival services must each expose primary and fallback paths.");
        foreach (IGrouping<string, SurvivalContinuityPathSnapshot> service in
                 paths.GroupBy(value => value.ServiceId, StringComparer.Ordinal))
        {
            Require(service.Count() == 2
                && service.Count(value => value.IsPrimitive) == 1,
                $"Survival service {service.Key} lacks an independent primitive path.");
        }
        SurvivalContinuityPathSnapshot fieldMeal = paths.Single(
            value => value.PathId == "survival:field-meal");
        SurvivalContinuityPathSnapshot bucketWash = paths.Single(
            value => value.PathId == "survival:bucket-wash");
        Require(fieldMeal.RequiredPhysicalItemIds.SequenceEqual(
                new[] { "food:grain-porridge" }, StringComparer.Ordinal)
            && fieldMeal.PhysicalInputQuantity == 1,
            "Field-meal fallback is not backed by one physical meal.");
        Require(bucketWash.RequiredPhysicalItemIds.SequenceEqual(
                new[] { "resource:clean-water" }, StringComparer.Ordinal)
            && bucketWash.PhysicalInputQuantity == 1,
            "Bucket-wash fallback is not backed by one clean-water item.");
    }

    private static string BuildReport(
        SurvivalClosedLoopAssessment value,
        IReadOnlyList<SurvivalContinuityPathSnapshot> paths)
    {
        StringBuilder builder = new();
        builder.AppendLine("RESULT=PASS; population=6; effectiveMilliWu=270000; recurringMilliWu="
            + Invariant(value.RecurringMilliWuPerDay)
            + "; recurringSharePermille=" + Invariant(value.RecurringSharePermille));
        builder.AppendLine("PASS V27_SIX_ADULT_FOOD_GROSS_125 demand=300000 gross=375000");
        builder.AppendLine("PASS V27_SIX_ADULT_FOOD_NET_110 target=330000 grossProduced=420000");
        builder.AppendLine("PASS V27_SIX_ADULT_WATER_GROSS_125 demand=5539 gross=6924 totalWithProduction=8421");
        builder.AppendLine("PASS V27_SIX_ADULT_RECURRING_WU_35 crop=18000 cooking=50008 water=10530 total=78538");
        builder.AppendLine("PASS V27_SEVEN_DAY_PHYSICAL_RESERVE grain=60 immediateMeals=12 cleanWater=59 storageCells=4");
        builder.AppendLine("PASS V27_SURVIVAL_NPLUSONE paths=" + Invariant(paths.Count));
        return builder.ToString();
    }

    private static void WriteContinuity(
        IReadOnlyList<SurvivalContinuityPathSnapshot> paths)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(ContinuityPath, stream =>
        {
            using StreamWriter writer = Writer(stream);
            Row(writer, "serviceId", "pathId", "primitive", "capacityPermille",
                "recurringMilliWuPerDay", "requiredPhysicalItems", "physicalInputQuantity",
                "actionDurationMilliseconds", "recoveryMilliUnits", "moodDeltaMilliUnits",
                "hygieneDeltaMilliUnits", "wasteMilliUnits", "stainMilliUnits");
            foreach (SurvivalContinuityPathSnapshot path in paths)
            {
                Row(writer,
                    path.ServiceId,
                    path.PathId,
                    path.IsPrimitive ? "true" : "false",
                    Invariant(path.CapacityPermille),
                    Invariant(path.RecurringMilliWuPerDay),
                    string.Join("|", path.RequiredPhysicalItemIds),
                    Invariant(path.PhysicalInputQuantity),
                    Invariant(path.ActionDurationMilliseconds),
                    Invariant(path.RecoveryMilliUnits),
                    Invariant(path.MoodDeltaMilliUnits),
                    Invariant(path.HygieneDeltaMilliUnits),
                    Invariant(path.WasteMilliUnits),
                    Invariant(path.StainMilliUnits));
            }
            writer.Flush();
        });
    }

    private static void WriteStages(
        IEnumerable<int> populations,
        CharacterNeedBalanceEntry hunger,
        CharacterNeedBalanceEntry thirst,
        ProductionRecipeSO mealRecipe,
        ItemAmountDefinition mealInput,
        ProductionOutputDefinition mealOutput,
        ProductionRecipeSO waterRecipe,
        ProductionOutputDefinition waterOutput,
        CropDefinitionSO crop,
        ResourceItemDefinitionSO meal,
        ResourceItemDefinitionSO grain,
        ResourceItemDefinitionSO water)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(StagePortfolioPath, stream =>
        {
            using StreamWriter writer = Writer(stream);
            Row(writer, "population", "tier", "effectiveMilliWu",
                "dailyFoodDemandMilliNutrition", "grossFoodTargetMilliNutrition",
                "netFoodTargetMilliNutrition", "grossFoodProducedMilliNutrition",
                "netFoodProducedMilliNutrition", "grossFoodCoveragePermille",
                "netFoodCoveragePermille", "cropPlots", "grossMealMilliUnits",
                "drinkingWaterDemandMilliUnits", "grossDrinkingWaterMilliUnits",
                "grossWaterCoveragePermille", "totalWaterMilliUnits",
                "recurringMilliWu", "recurringSharePermille",
                "logisticsReservePermille", "emergencyReservePermille",
                "growthAvailablePermille", "immediateMealUnits",
                "sevenDayGrainUnits", "sevenDayWaterUnits", "storageCells",
                "stageGate", "failureCode");
            foreach (int population in populations.OrderBy(value => value))
            {
                SurvivalClosedLoopAssessment value = SurvivalClosedLoopCalculator.Assess(
                    Definition(population, hunger, thirst, mealRecipe, mealInput, mealOutput,
                        waterRecipe, waterOutput, crop, meal, grain, water));
                const int logisticsReservePermille = 150;
                const int emergencyReservePermille = 100;
                int growthAvailablePermille = 1000
                    - value.RecurringSharePermille
                    - logisticsReservePermille
                    - emergencyReservePermille;
                bool recurringTargetWarning = value.RecurringSharePermille > 350;
                bool passed = value.Passed
                    && value.GrossFoodCoveragePermille >= 1250
                    && value.NetFoodCoveragePermille >= 1100
                    && value.GrossDrinkingWaterCoveragePermille >= 1250
                    && growthAvailablePermille >= 350;
                string failureCode = passed
                    ? string.Empty
                    : !value.Passed
                        ? value.FailureCode
                        : growthAvailablePermille < 350
                                ? "V27_STAGE_GROWTH_BELOW_35_PERCENT"
                                : "V27_STAGE_SURVIVAL_COVERAGE_BELOW_TARGET";
                Row(writer,
                    Invariant(population),
                    Invariant(PopulationTier(population)),
                    Invariant(population * 45000L),
                    Invariant(value.DailyFoodDemandMilliNutrition),
                    Invariant(value.GrossFoodTargetMilliNutrition),
                    Invariant(value.NetFoodTargetMilliNutrition),
                    Invariant(value.GrossFoodProducedMilliNutrition),
                    Invariant(value.NetFoodProducedMilliNutrition),
                    Invariant(value.GrossFoodCoveragePermille),
                    Invariant(value.NetFoodCoveragePermille),
                    Invariant(value.CropPlots),
                    Invariant(value.GrossMealMilliUnitsPerDay),
                    Invariant(value.DrinkingWaterDemandMilliUnitsPerDay),
                    Invariant(value.GrossDrinkingWaterMilliUnitsPerDay),
                    Invariant(value.GrossDrinkingWaterCoveragePermille),
                    Invariant(value.TotalWaterMilliUnitsPerDay),
                    Invariant(value.RecurringMilliWuPerDay),
                    Invariant(value.RecurringSharePermille),
                    Invariant(logisticsReservePermille),
                    Invariant(emergencyReservePermille),
                    Invariant(growthAvailablePermille),
                    Invariant(value.ImmediateMealUnits),
                    Invariant(value.SevenDayGrainUnits),
                    Invariant(value.SevenDayWaterUnits),
                    Invariant(value.StorageCells),
                    passed ? "PASS" : "FAIL",
                    recurringTargetWarning
                        ? "WARNING:V27_STAGE_RECURRING_WU_ABOVE_35_PERCENT"
                        : failureCode);
            }
            writer.Flush();
        });
    }

    private static int PopulationTier(int population) => population <= 6
        ? 0
        : population <= 12 ? 1 : population <= 18 ? 2 : 3;

    private static StreamWriter Writer(Stream stream) => new(
        stream,
        new UTF8Encoding(false, true),
        4096,
        leaveOpen: true);

    private static void Row(StreamWriter writer, params string[] fields)
    {
        for (int index = 0; index < fields.Length; index++)
        {
            if (index != 0)
                writer.Write(',');
            V27BalanceCsvSerializer.WriteEscapedField(
                writer,
                (fields[index] ?? string.Empty).AsSpan());
        }
        writer.Write('\r');
        writer.Write('\n');
    }

    private static void WriteText(string path, string value)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
        {
            using StreamWriter writer = Writer(stream);
            writer.NewLine = "\n";
            writer.Write(value);
            writer.Flush();
        });
    }

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException($"V27 authority asset is missing: {path}.");

    private static T[] LoadAll<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .ToArray();

    private static ResourceItemDefinitionSO RequireItem(
        IResourceEconomyContentCatalog content,
        string itemId) => content.TryGetItem(itemId, out ResourceItemDefinitionSO value)
        ? value
        : throw new InvalidOperationException($"V27 item authority is missing: {itemId}.");

    private static T RequireSingle<T>(IReadOnlyList<T> values, string label)
    {
        if (values == null || values.Count != 1)
            throw new InvalidOperationException($"Expected one {label}.");
        return values[0];
    }

    private static long Milli(float value) => checked((long)decimal.Round(
        (decimal)value * 1000m,
        0,
        MidpointRounding.AwayFromZero));

    private static bool Exact(float value, float expected) =>
        Mathf.Abs(value - expected) <= 0.0001f;

    private static string Invariant(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class SettingsNeedRuntime : ICharacterNeedBalanceRuntime
    {
        private readonly SurvivalBalanceSettingsSO settings;

        public SettingsNeedRuntime(SurvivalBalanceSettingsSO settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public DungeonSurvivalPressure Pressure => DungeonSurvivalPressure.Standard;
        public float DayLengthSeconds => settings.DayLengthSeconds;
        public float ForcedBreakdownDelaySeconds =>
            settings.GetPressure(Pressure).forcedBreakdownDelaySeconds;
        public float HighBurdenDamageIntervalSeconds =>
            settings.GetPressure(Pressure).highBurdenDamageIntervalSeconds;
        public float GetDailyDepletion(CharacterCondition condition) =>
            settings.TryGetNeed(condition, out CharacterNeedBalanceEntry entry)
                ? entry.dailyDepletion
                : 0f;
        public float GetTimedDepletion(
            CharacterCondition condition,
            float elapsedSeconds,
            float speciesMultiplier = 1f,
            float personaMultiplier = 1f) => GetDailyDepletion(condition)
                * Mathf.Max(0f, speciesMultiplier)
                * Mathf.Max(0f, personaMultiplier)
                * Mathf.Max(0f, elapsedSeconds)
                / DayLengthSeconds;
        public float GetWorkDepletion(
            CharacterCondition condition,
            float elapsedSeconds = 1f) =>
            settings.TryGetNeed(condition, out CharacterNeedBalanceEntry entry)
                ? Mathf.Max(0f, entry.workDepletionPerSecond)
                    * Mathf.Max(0f, elapsedSeconds)
                : 0f;
        public CharacterNeedResponseProfile GetResponse(CharacterCondition condition) =>
            settings.TryGetNeed(condition, out CharacterNeedBalanceEntry entry)
                ? entry.response
                : new CharacterNeedResponseProfile(0f, 0f, 100f);
        public float ApplyRecoveryMultiplier(
            CharacterCondition condition,
            float amount,
            CharacterNeedRecoverySource source) => amount;
        public float ApplyPersonalContinuousWaterMultiplier(float amount) => amount;
        public float GetDeprivationBurdenMultiplier(bool recovering) => 1f;
    }
}
#endif
