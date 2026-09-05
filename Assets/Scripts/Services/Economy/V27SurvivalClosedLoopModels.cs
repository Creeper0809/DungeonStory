using System;

namespace DungeonStory.Balance
{
    [BalanceImmutableRecord]
    public sealed class SurvivalClosedLoopDefinition
    {
        public SurvivalClosedLoopDefinition(
            int population,
            long hungerMilliUnitsPerAdultDay,
            long thirstMilliUnitsPerAdultDay,
            long mealNutritionMilliUnits,
            int mealInputUnitsPerBatch,
            int mealOutputUnitsPerBatch,
            long mealBatchMilliWu,
            long cropGrowthMilliHours,
            long cropSowMilliWu,
            long cropHarvestMilliWu,
            int cropYieldUnits,
            long cropDailyWaterMilliUnits,
            int waterOutputUnitsPerBatch,
            long waterBatchMilliWu,
            long cookingWaterMilliUnitsPerBatch,
            int mealMaxStack,
            int grainMaxStack,
            int waterMaxStack,
            long mealUnitMassGrams,
            long grainUnitMassGrams,
            long waterUnitMassGrams)
        {
            Population = RequirePositive(population, nameof(population));
            HungerMilliUnitsPerAdultDay = RequirePositive(
                hungerMilliUnitsPerAdultDay,
                nameof(hungerMilliUnitsPerAdultDay));
            ThirstMilliUnitsPerAdultDay = RequirePositive(
                thirstMilliUnitsPerAdultDay,
                nameof(thirstMilliUnitsPerAdultDay));
            MealNutritionMilliUnits = RequirePositive(
                mealNutritionMilliUnits,
                nameof(mealNutritionMilliUnits));
            MealInputUnitsPerBatch = RequirePositive(
                mealInputUnitsPerBatch,
                nameof(mealInputUnitsPerBatch));
            MealOutputUnitsPerBatch = RequirePositive(
                mealOutputUnitsPerBatch,
                nameof(mealOutputUnitsPerBatch));
            MealBatchMilliWu = RequirePositive(mealBatchMilliWu, nameof(mealBatchMilliWu));
            CropGrowthMilliHours = RequirePositive(
                cropGrowthMilliHours,
                nameof(cropGrowthMilliHours));
            CropSowMilliWu = RequirePositive(cropSowMilliWu, nameof(cropSowMilliWu));
            CropHarvestMilliWu = RequirePositive(
                cropHarvestMilliWu,
                nameof(cropHarvestMilliWu));
            CropYieldUnits = RequirePositive(cropYieldUnits, nameof(cropYieldUnits));
            CropDailyWaterMilliUnits = RequireNonNegative(
                cropDailyWaterMilliUnits,
                nameof(cropDailyWaterMilliUnits));
            WaterOutputUnitsPerBatch = RequirePositive(
                waterOutputUnitsPerBatch,
                nameof(waterOutputUnitsPerBatch));
            WaterBatchMilliWu = RequirePositive(waterBatchMilliWu, nameof(waterBatchMilliWu));
            CookingWaterMilliUnitsPerBatch = RequireNonNegative(
                cookingWaterMilliUnitsPerBatch,
                nameof(cookingWaterMilliUnitsPerBatch));
            MealMaxStack = RequirePositive(mealMaxStack, nameof(mealMaxStack));
            GrainMaxStack = RequirePositive(grainMaxStack, nameof(grainMaxStack));
            WaterMaxStack = RequirePositive(waterMaxStack, nameof(waterMaxStack));
            MealUnitMassGrams = RequirePositive(
                mealUnitMassGrams,
                nameof(mealUnitMassGrams));
            GrainUnitMassGrams = RequirePositive(
                grainUnitMassGrams,
                nameof(grainUnitMassGrams));
            WaterUnitMassGrams = RequirePositive(
                waterUnitMassGrams,
                nameof(waterUnitMassGrams));
        }

        public int Population { get; }
        public long HungerMilliUnitsPerAdultDay { get; }
        public long ThirstMilliUnitsPerAdultDay { get; }
        public long MealNutritionMilliUnits { get; }
        public int MealInputUnitsPerBatch { get; }
        public int MealOutputUnitsPerBatch { get; }
        public long MealBatchMilliWu { get; }
        public long CropGrowthMilliHours { get; }
        public long CropSowMilliWu { get; }
        public long CropHarvestMilliWu { get; }
        public int CropYieldUnits { get; }
        public long CropDailyWaterMilliUnits { get; }
        public int WaterOutputUnitsPerBatch { get; }
        public long WaterBatchMilliWu { get; }
        public long CookingWaterMilliUnitsPerBatch { get; }
        public int MealMaxStack { get; }
        public int GrainMaxStack { get; }
        public int WaterMaxStack { get; }
        public long MealUnitMassGrams { get; }
        public long GrainUnitMassGrams { get; }
        public long WaterUnitMassGrams { get; }

        private static int RequirePositive(int value, string name) => value > 0
            ? value
            : throw new ArgumentOutOfRangeException(name);

        private static long RequirePositive(long value, string name) => value > 0L
            ? value
            : throw new ArgumentOutOfRangeException(name);

        private static long RequireNonNegative(long value, string name) => value >= 0L
            ? value
            : throw new ArgumentOutOfRangeException(name);
    }

    [BalanceImmutableRecord]
    public sealed class SurvivalClosedLoopAssessment
    {
        internal SurvivalClosedLoopAssessment(
            long dailyFoodDemandMilliNutrition,
            long grossFoodTargetMilliNutrition,
            long netFoodTargetMilliNutrition,
            long grossFoodProducedMilliNutrition,
            long netFoodProducedMilliNutrition,
            int grossFoodCoveragePermille,
            int netFoodCoveragePermille,
            long grossMealMilliUnitsPerDay,
            int cropPlots,
            long grossGrainMilliUnitsPerDay,
            long cropMilliWuPerDay,
            long cookingMilliWuPerDay,
            long drinkingWaterDemandMilliUnitsPerDay,
            long grossDrinkingWaterMilliUnitsPerDay,
            int grossDrinkingWaterCoveragePermille,
            long totalWaterMilliUnitsPerDay,
            long waterMilliWuPerDay,
            long recurringMilliWuPerDay,
            int recurringSharePermille,
            int immediateMealUnits,
            int sevenDayGrainUnits,
            int sevenDayWaterUnits,
            long requiredStorageMassGrams,
            long maximumRelevantStackMassGrams,
            long grossGrainMassGramsPerDay,
            long grossMealMassGramsPerDay,
            bool passed,
            string failureCode)
        {
            DailyFoodDemandMilliNutrition = dailyFoodDemandMilliNutrition;
            GrossFoodTargetMilliNutrition = grossFoodTargetMilliNutrition;
            NetFoodTargetMilliNutrition = netFoodTargetMilliNutrition;
            GrossFoodProducedMilliNutrition = grossFoodProducedMilliNutrition;
            NetFoodProducedMilliNutrition = netFoodProducedMilliNutrition;
            GrossFoodCoveragePermille = grossFoodCoveragePermille;
            NetFoodCoveragePermille = netFoodCoveragePermille;
            GrossMealMilliUnitsPerDay = grossMealMilliUnitsPerDay;
            CropPlots = cropPlots;
            GrossGrainMilliUnitsPerDay = grossGrainMilliUnitsPerDay;
            CropMilliWuPerDay = cropMilliWuPerDay;
            CookingMilliWuPerDay = cookingMilliWuPerDay;
            DrinkingWaterDemandMilliUnitsPerDay = drinkingWaterDemandMilliUnitsPerDay;
            GrossDrinkingWaterMilliUnitsPerDay = grossDrinkingWaterMilliUnitsPerDay;
            GrossDrinkingWaterCoveragePermille = grossDrinkingWaterCoveragePermille;
            TotalWaterMilliUnitsPerDay = totalWaterMilliUnitsPerDay;
            WaterMilliWuPerDay = waterMilliWuPerDay;
            RecurringMilliWuPerDay = recurringMilliWuPerDay;
            RecurringSharePermille = recurringSharePermille;
            ImmediateMealUnits = immediateMealUnits;
            SevenDayGrainUnits = sevenDayGrainUnits;
            SevenDayWaterUnits = sevenDayWaterUnits;
            RequiredStorageMassGrams = requiredStorageMassGrams;
            MaximumRelevantStackMassGrams = maximumRelevantStackMassGrams;
            GrossGrainMassGramsPerDay = grossGrainMassGramsPerDay;
            GrossMealMassGramsPerDay = grossMealMassGramsPerDay;
            Passed = passed;
            FailureCode = failureCode ?? string.Empty;
        }

        public long DailyFoodDemandMilliNutrition { get; }
        public long GrossFoodTargetMilliNutrition { get; }
        public long NetFoodTargetMilliNutrition { get; }
        public long GrossFoodProducedMilliNutrition { get; }
        public long NetFoodProducedMilliNutrition { get; }
        public int GrossFoodCoveragePermille { get; }
        public int NetFoodCoveragePermille { get; }
        public long GrossMealMilliUnitsPerDay { get; }
        public int CropPlots { get; }
        public long GrossGrainMilliUnitsPerDay { get; }
        public long CropMilliWuPerDay { get; }
        public long CookingMilliWuPerDay { get; }
        public long DrinkingWaterDemandMilliUnitsPerDay { get; }
        public long GrossDrinkingWaterMilliUnitsPerDay { get; }
        public int GrossDrinkingWaterCoveragePermille { get; }
        public long TotalWaterMilliUnitsPerDay { get; }
        public long WaterMilliWuPerDay { get; }
        public long RecurringMilliWuPerDay { get; }
        public int RecurringSharePermille { get; }
        public int ImmediateMealUnits { get; }
        public int SevenDayGrainUnits { get; }
        public int SevenDayWaterUnits { get; }
        public long RequiredStorageMassGrams { get; }
        public long MaximumRelevantStackMassGrams { get; }
        public long GrossGrainMassGramsPerDay { get; }
        public long GrossMealMassGramsPerDay { get; }
        public bool Passed { get; }
        public string FailureCode { get; }
    }

    [BalanceCaptureFactory]
    public static class SurvivalClosedLoopCalculator
    {
        private const long EffectiveMilliWuPerAdultDay = 45000L;
        private const long SafeDrinkRecoveryMilliUnits = 65000L;

        public static SurvivalClosedLoopAssessment Assess(
            SurvivalClosedLoopDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            long dailyFood = checked(
                definition.Population * definition.HungerMilliUnitsPerAdultDay);
            long grossFood = CeilRatio(dailyFood, 5L, 4L);
            long netFood = CeilRatio(dailyFood, 11L, 10L);
            long grossMeals = CeilRatio(
                grossFood,
                1000L,
                definition.MealNutritionMilliUnits);
            long grainPerMealNumerator = definition.MealInputUnitsPerBatch;
            long grainPerMealDenominator = definition.MealOutputUnitsPerBatch;
            long requiredGrain = CeilRatio(
                grossMeals,
                grainPerMealNumerator,
                grainPerMealDenominator);
            long cropDailyYieldMilli = checked(
                definition.CropYieldUnits * 24000000L
                / definition.CropGrowthMilliHours);
            int plots = checked((int)CeilRatio(
                requiredGrain,
                1L,
                cropDailyYieldMilli));
            long grossGrain = checked(plots * cropDailyYieldMilli);

            long cropWu = checked(plots * CeilRatio(
                definition.CropSowMilliWu + definition.CropHarvestMilliWu,
                24000L,
                definition.CropGrowthMilliHours));
            long mealCyclesMilli = CeilRatio(
                grossMeals,
                1L,
                definition.MealOutputUnitsPerBatch);
            long cookWu = CeilRatio(
                mealCyclesMilli,
                definition.MealBatchMilliWu,
                1000L);

            long drinkingWaterDemand = CeilRatio(
                definition.Population * definition.ThirstMilliUnitsPerAdultDay,
                1000L,
                SafeDrinkRecoveryMilliUnits);
            long grossDrinkingWater = CeilRatio(
                drinkingWaterDemand,
                5L,
                4L);
            long cropWater = checked(plots * definition.CropDailyWaterMilliUnits);
            long cookingWater = CeilRatio(
                mealCyclesMilli,
                definition.CookingWaterMilliUnitsPerBatch,
                1000L);
            long totalWater = checked(grossDrinkingWater + cropWater + cookingWater);
            long waterCyclesMilli = CeilRatio(
                totalWater,
                1L,
                definition.WaterOutputUnitsPerBatch);
            long waterWu = CeilRatio(
                waterCyclesMilli,
                definition.WaterBatchMilliWu,
                1000L);

            long recurring = checked(cropWu + cookWu + waterWu);
            long available = checked(
                definition.Population * EffectiveMilliWuPerAdultDay);
            int share = checked((int)CeilRatio(recurring, 1000L, available));
            int mealsPerDay = checked((int)CeilRatio(
                dailyFood,
                1L,
                definition.MealNutritionMilliUnits));
            int immediateMeals = checked((int)CeilRatio(
                mealsPerDay,
                1L,
                definition.MealOutputUnitsPerBatch)
                * definition.MealOutputUnitsPerBatch);
            int sevenDayGrain = checked((int)CeilRatio(
                dailyFood * 7L,
                definition.MealInputUnitsPerBatch,
                definition.MealNutritionMilliUnits
                    * definition.MealOutputUnitsPerBatch));
            int sevenDayWater = checked((int)CeilRatio(totalWater * 7L, 1L, 1000L));
            long requiredStorageMassGrams = checked(
                sevenDayGrain * definition.GrainUnitMassGrams
                + immediateMeals * definition.MealUnitMassGrams
                + sevenDayWater * definition.WaterUnitMassGrams);
            long maximumRelevantStackMassGrams = Math.Max(
                checked(definition.MealMaxStack * definition.MealUnitMassGrams),
                Math.Max(
                    checked(definition.GrainMaxStack * definition.GrainUnitMassGrams),
                    checked(definition.WaterMaxStack * definition.WaterUnitMassGrams)));
            long grossGrainMassGramsPerDay = checked(
                CeilRatio(grossGrain, 1L, 1000L)
                * definition.GrainUnitMassGrams);
            long grossMealMassGramsPerDay = checked(
                CeilRatio(grossMeals, 1L, 1000L)
                * definition.MealUnitMassGrams);

            long grossNutritionProduced = checked(
                grossGrain * definition.MealOutputUnitsPerBatch
                / definition.MealInputUnitsPerBatch
                * definition.MealNutritionMilliUnits
                / 1000L);
            long netNutritionProduced = checked(grossNutritionProduced * 95L / 100L);
            int grossFoodCoverage = checked((int)CeilRatio(
                grossNutritionProduced,
                1000L,
                dailyFood));
            int netFoodCoverage = checked((int)CeilRatio(
                netNutritionProduced,
                1000L,
                dailyFood));
            int grossWaterCoverage = checked((int)CeilRatio(
                grossDrinkingWater,
                1000L,
                drinkingWaterDemand));
            string failure = grossNutritionProduced < grossFood
                ? "V27_SURVIVAL_FOOD_GROSS_BELOW_125"
                : netNutritionProduced < netFood
                    ? "V27_SURVIVAL_FOOD_NET_BELOW_110"
                    : string.Empty;
            return new SurvivalClosedLoopAssessment(
                dailyFood,
                grossFood,
                netFood,
                grossNutritionProduced,
                netNutritionProduced,
                grossFoodCoverage,
                netFoodCoverage,
                grossMeals,
                plots,
                grossGrain,
                cropWu,
                cookWu,
                drinkingWaterDemand,
                grossDrinkingWater,
                grossWaterCoverage,
                totalWater,
                waterWu,
                recurring,
                share,
                immediateMeals,
                sevenDayGrain,
                sevenDayWater,
                requiredStorageMassGrams,
                maximumRelevantStackMassGrams,
                grossGrainMassGramsPerDay,
                grossMealMassGramsPerDay,
                failure.Length == 0,
                failure);
        }

        private static long CeilRatio(long value, long numerator, long denominator)
        {
            if (value < 0L || numerator < 0L || denominator <= 0L)
                throw new ArgumentOutOfRangeException(nameof(value));
            long scaled = checked(value * numerator);
            return checked((scaled + denominator - 1L) / denominator);
        }
    }
}
