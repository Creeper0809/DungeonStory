using System;
using System.Collections.Generic;

public enum SettlementTechnologyStage
{
    NoResearch = 0,
    Early = 1,
    Middle = 2,
    Industrial = 3,
    Late = 4,
    Endless = 5
}

public enum ProjectScale
{
    SmallFacility = 0,
    MediumFacility = 1,
    IndustrialFacility = 2,
    GrandProject = 3,
    Landmark = 4,
    StandardResearch = 5,
    CollaborativeResearch = 6,
    MajorResearch = 7
}

public static class SettlementLaborBalanceRules
{
    public const float SecondsPerDay = 180f;
    public const float BaselineSleepSeconds = 50f;
    public const float BaselineMealSeconds = 10f;
    public const float BaselineDrinkSeconds = 4f;
    public const float BaselineHygieneSeconds = 6f;
    public const float BaselineRecreationSeconds = 10f;
    public const float BaselineActiveWorkSeconds = 100f;
    public const float WorkTransitionEfficiency = 0.99f;
    // Corrected five-day, three-seed live routine measurement with exact
    // consumable destinations produced 60.494 actual WU/adult-day. Keep the
    // authored 50 WU authority by normalizing the single runtime work-rate
    // boundary instead of retuning every content definition.
    public const float RuntimeLaborCalibrationMultiplier = 0.8265f;
    public const float ActualLaborUtilization =
        SettlementLaborAuthority.ActualWuPerAdultDay
        / SettlementLaborAuthority.HistoricalTheoreticalCapacityWuPerAdultDay;

    private static readonly float[] GeneralContributionCurve =
        { 1f, 0.85f, 0.75f, 0.65f, 0.55f, 0.45f, 0.40f, 0.35f };
    private static readonly float[] ResearchContributionCurve =
        { 1f, 0.70f, 0.45f, 0.25f };

    private static readonly TechnologyWuCheckpoint[] Checkpoints =
    {
        new TechnologyWuCheckpoint(1, 3, 50f, 0.90f, 0f, 45f),
        new TechnologyWuCheckpoint(30, 5, 54.5f, 0.90f, 0f, 49.05f),
        new TechnologyWuCheckpoint(120, 11, 62.5f, 0.90f, 0f, 56.25f),
        new TechnologyWuCheckpoint(240, 20, 74.5f, 0.90f, 0f, 67.05f),
        new TechnologyWuCheckpoint(400, 30, 85f, 0.90f, 0f, 76.5f),
        new TechnologyWuCheckpoint(960, 64, 100f, 0.90f, 0f, 90f)
    };

    public static IReadOnlyList<TechnologyWuCheckpoint> TechnologyCheckpoints =>
        Checkpoints;

    public static DailyLaborBudget CreateBaselineDailyBudget()
    {
        DailyLaborBudget result = new DailyLaborBudget(
            BaselineSleepSeconds,
            BaselineMealSeconds,
            BaselineDrinkSeconds,
            BaselineHygieneSeconds,
            BaselineRecreationSeconds,
            BaselineActiveWorkSeconds,
            WorkTransitionEfficiency);
        if (Math.Abs(result.TotalSeconds - SecondsPerDay) > 0.0001f
            || Math.Abs(
                result.NetLaborWu
                - SettlementLaborAuthority.HistoricalTheoreticalCapacityWuPerAdultDay)
                > 0.0001f)
        {
            throw new InvalidOperationException(
                "The authored daily schedule no longer equals 180 seconds and its historical 99-second work envelope.");
        }
        return result;
    }

    public static TechnologyDailyRoutineSnapshot EvaluateTechnologyDailyRoutine(
        SettlementTechnologyStage stage)
    {
        TechnologyRoutineSavings savings = GetTechnologyRoutineSavings(stage);
        float activeWorkSeconds = BaselineActiveWorkSeconds + savings.TotalSeconds;
        DailyLaborBudget budget = new DailyLaborBudget(
            BaselineSleepSeconds - savings.SleepSeconds,
            BaselineMealSeconds - savings.MealSeconds,
            BaselineDrinkSeconds - savings.DrinkSeconds,
            BaselineHygieneSeconds - savings.HygieneSeconds,
            BaselineRecreationSeconds - savings.RecreationSeconds,
            activeWorkSeconds,
            WorkTransitionEfficiency);
        TechnologyWuCheckpoint checkpoint = Checkpoints[(int)stage];
        float unmodifiedActualLaborWu = budget.NetLaborWu * ActualLaborUtilization;
        float activeWorkPerformance = checkpoint.ActualLaborWu
            / unmodifiedActualLaborWu;
        float actualLaborWu = unmodifiedActualLaborWu * activeWorkPerformance;
        if (Math.Abs(budget.TotalSeconds - SecondsPerDay) > 0.0001f)
        {
            throw new InvalidOperationException(
                $"Technology routine '{stage}' no longer totals {SecondsPerDay:0.###} seconds.");
        }

        return new TechnologyDailyRoutineSnapshot(
            stage,
            savings,
            budget,
            activeWorkPerformance,
            actualLaborWu);
    }

    public static TechnologyRoutineSavings GetTechnologyRoutineSavings(
        SettlementTechnologyStage stage) => stage switch
    {
        SettlementTechnologyStage.NoResearch => new TechnologyRoutineSavings(
            mealSeconds: 0f,
            sleepSeconds: 0f,
            drinkSeconds: 0f,
            hygieneSeconds: 0f,
            recreationSeconds: 0f),
        SettlementTechnologyStage.Early => new TechnologyRoutineSavings(
            mealSeconds: 1f,
            sleepSeconds: 1.5f,
            drinkSeconds: 0.25f,
            hygieneSeconds: 0.5f,
            recreationSeconds: 0.75f),
        SettlementTechnologyStage.Middle => new TechnologyRoutineSavings(
            mealSeconds: 2f,
            sleepSeconds: 4f,
            drinkSeconds: 1f,
            hygieneSeconds: 1f,
            recreationSeconds: 2f),
        SettlementTechnologyStage.Industrial => new TechnologyRoutineSavings(
            mealSeconds: 3f,
            sleepSeconds: 7f,
            drinkSeconds: 1.5f,
            hygieneSeconds: 2.5f,
            recreationSeconds: 3f),
        SettlementTechnologyStage.Late or SettlementTechnologyStage.Endless =>
            new TechnologyRoutineSavings(
                mealSeconds: 4f,
                sleepSeconds: 10f,
                drinkSeconds: 2f,
                hygieneSeconds: 3f,
                recreationSeconds: 4f),
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
    };

    public static int GetMaximumWorkers(ProjectScale scale) => scale switch
    {
        ProjectScale.SmallFacility => 2,
        ProjectScale.MediumFacility => 3,
        ProjectScale.IndustrialFacility => 4,
        ProjectScale.GrandProject => 6,
        ProjectScale.Landmark => 8,
        ProjectScale.StandardResearch => 1,
        ProjectScale.CollaborativeResearch => 2,
        ProjectScale.MajorResearch => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, null)
    };

    public static bool TryGetMaintenanceChannel(
        WorkTypeId workTypeId,
        out SettlementLaborContributionChannel channel)
    {
        if (workTypeId == BuiltInWorkTypeIds.DrawWater
            || workTypeId == BuiltInWorkTypeIds.Clean
            || workTypeId == BuiltInWorkTypeIds.Cook
            || workTypeId == BuiltInWorkTypeIds.Rescue
            || workTypeId == BuiltInWorkTypeIds.Treat
            || workTypeId == BuiltInWorkTypeIds.Surgery)
        {
            channel = SettlementLaborContributionChannel.EssentialMaintenance;
            return true;
        }
        if (workTypeId == BuiltInWorkTypeIds.Repair
            || workTypeId == BuiltInWorkTypeIds.Refuel
            || workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            channel = SettlementLaborContributionChannel.EquipmentFacilityMaintenance;
            return true;
        }

        channel = default;
        return false;
    }

    public static int GetDefaultAutomaticWorkerLimit(ProjectScale scale) =>
        scale == ProjectScale.Landmark
            ? 5
            : GetMaximumWorkers(scale);

    public static float GetWorkerContribution(ProjectScale scale, int zeroBasedWorkerIndex)
    {
        int maximumWorkers = GetMaximumWorkers(scale);
        if (zeroBasedWorkerIndex < 0 || zeroBasedWorkerIndex >= maximumWorkers)
        {
            return 0f;
        }

        bool research = scale is ProjectScale.StandardResearch
            or ProjectScale.CollaborativeResearch
            or ProjectScale.MajorResearch;
        float[] curve = research ? ResearchContributionCurve : GeneralContributionCurve;
        return curve[zeroBasedWorkerIndex];
    }

    public static ProjectContributionSnapshot EvaluateProject(
        ProjectScale scale,
        IReadOnlyList<float> workerWuPerSecond,
        float remainingWu)
    {
        if (workerWuPerSecond == null)
            throw new ArgumentNullException(nameof(workerWuPerSecond));
        if (float.IsNaN(remainingWu) || float.IsInfinity(remainingWu) || remainingWu < 0f)
            throw new ArgumentOutOfRangeException(nameof(remainingWu));

        int maximumWorkers = GetMaximumWorkers(scale);
        int appliedWorkers = Math.Min(maximumWorkers, workerWuPerSecond.Count);
        float effectiveRate = 0f;
        for (int index = 0; index < appliedWorkers; index++)
        {
            float rate = workerWuPerSecond[index];
            if (float.IsNaN(rate) || float.IsInfinity(rate) || rate < 0f)
                throw new ArgumentOutOfRangeException(nameof(workerWuPerSecond));
            effectiveRate += rate * GetWorkerContribution(scale, index);
        }

        float currentSeconds = effectiveRate > 0f
            ? remainingWu / effectiveRate
            : float.PositiveInfinity;
        float nextContribution = appliedWorkers < maximumWorkers
            ? GetWorkerContribution(scale, appliedWorkers)
            : 0f;
        float referenceRate = workerWuPerSecond.Count > 0
            ? Math.Max(0f, workerWuPerSecond[0])
            : 0f;
        float nextEffectiveRate = effectiveRate + referenceRate * nextContribution;
        float nextSeconds = nextEffectiveRate > 0f
            ? remainingWu / nextEffectiveRate
            : float.PositiveInfinity;
        return new ProjectContributionSnapshot(
            scale,
            workerWuPerSecond.Count,
            appliedWorkers,
            maximumWorkers,
            effectiveRate,
            currentSeconds,
            nextContribution,
            nextSeconds,
            Math.Max(0f, currentSeconds - nextSeconds));
    }

    public static InvestmentReturnSnapshot EvaluateInvestmentReturn(
        SettlementTechnologyStage stage,
        float researchWu,
        float embodiedBomWu,
        float constructionWu,
        float initialLogisticsWu,
        float savedLaborWuPerDay,
        float preventedLossWuPerDay,
        float netAutomationWuPerDay,
        float fuelMaintenanceReplacementWuPerDay)
    {
        float investment = ValidateNonNegative(researchWu, nameof(researchWu))
            + ValidateNonNegative(embodiedBomWu, nameof(embodiedBomWu))
            + ValidateNonNegative(constructionWu, nameof(constructionWu))
            + ValidateNonNegative(initialLogisticsWu, nameof(initialLogisticsWu));
        float dailyReturn = ValidateNonNegative(savedLaborWuPerDay, nameof(savedLaborWuPerDay))
            + ValidateNonNegative(preventedLossWuPerDay, nameof(preventedLossWuPerDay))
            + ValidateNonNegative(netAutomationWuPerDay, nameof(netAutomationWuPerDay))
            - ValidateNonNegative(
                fuelMaintenanceReplacementWuPerDay,
                nameof(fuelMaintenanceReplacementWuPerDay));
        float paybackDays = dailyReturn > 0f
            ? investment / dailyReturn
            : float.PositiveInfinity;
        (float minimum, float maximum) = GetTargetPaybackBand(stage);
        return new InvestmentReturnSnapshot(
            investment,
            dailyReturn,
            paybackDays,
            minimum,
            maximum,
            paybackDays >= minimum && paybackDays <= maximum);
    }

    public static SettlementLaborSnapshot EvaluateSettlementLabor(
        float actualWorkSeconds,
        float averagePerformance,
        float convertedProcessOutputWu,
        float netDomainAutomationWu,
        float fuelMaintenanceAccidentSpoilageLossWu,
        float essentialMaintenanceWu,
        float equipmentFacilityMaintenanceWu,
        float emergencyReserveWu)
    {
        float actualLaborWu = ValidateNonNegative(actualWorkSeconds, nameof(actualWorkSeconds))
            * ValidateNonNegative(averagePerformance, nameof(averagePerformance));
        float transferableOutputWu = actualLaborWu
            + ValidateNonNegative(convertedProcessOutputWu, nameof(convertedProcessOutputWu))
            - ValidateNonNegative(
                fuelMaintenanceAccidentSpoilageLossWu,
                nameof(fuelMaintenanceAccidentSpoilageLossWu));
        transferableOutputWu = Math.Max(0f, transferableOutputWu);
        float outputEquivalentWu = transferableOutputWu
            + ValidateNonNegative(
                netDomainAutomationWu,
                nameof(netDomainAutomationWu));
        float realizedGrowthWu = Math.Max(
            0f,
            transferableOutputWu
            - ValidateNonNegative(essentialMaintenanceWu, nameof(essentialMaintenanceWu))
            - ValidateNonNegative(
                equipmentFacilityMaintenanceWu,
                nameof(equipmentFacilityMaintenanceWu)));
        float guaranteedGrowthWu = Math.Max(
            0f,
            realizedGrowthWu
            - ValidateNonNegative(emergencyReserveWu, nameof(emergencyReserveWu)));
        return new SettlementLaborSnapshot(
            actualLaborWu,
            outputEquivalentWu,
            realizedGrowthWu,
            guaranteedGrowthWu);
    }

    public static DisasterShadowSimulationSnapshot EvaluateDisasterShadow(
        in DisasterShadowScenarioInput input)
    {
        if (input.ProductiveAdultCount < 0
            || input.UnavailableAdultCount < 0
            || input.EmergencyResponderCount < 0
            || input.RecoveredAdultsByDaySeven < 0
            || input.FoodSupplyDays < 0
            || input.WaterSupplyDays < 0
            || input.CrisisDurationDays < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                "Disaster shadow counts and duration must be non-negative.");
        }

        float adultWuPerDay = ValidateNonNegative(
            input.AdultWuPerDay,
            nameof(input.AdultWuPerDay));
        float essentialWuPerDay = ValidateNonNegative(
            input.EssentialWuPerDay,
            nameof(input.EssentialWuPerDay));
        int availableAdults = Math.Max(
            0,
            input.ProductiveAdultCount
                - input.UnavailableAdultCount
                - input.EmergencyResponderCount);
        float availableWuPerDay = availableAdults * adultWuPerDay;
        float essentialCoverage = essentialWuPerDay > 0f
            ? availableWuPerDay / essentialWuPerDay
            : float.PositiveInfinity;
        float growthWuPerDay = Math.Max(0f, availableWuPerDay - essentialWuPerDay);
        float essentialDeficitWuPerDay = Math.Max(
            0f,
            essentialWuPerDay - availableWuPerDay);
        int foodDaysAfterCrisis = Math.Max(
            0,
            input.FoodSupplyDays - input.CrisisDurationDays);
        int waterDaysAfterCrisis = Math.Max(
            0,
            input.WaterSupplyDays - input.CrisisDurationDays);
        int daySevenAdults = Math.Min(
            input.ProductiveAdultCount,
            availableAdults + input.RecoveredAdultsByDaySeven);
        float daySevenCoverage = essentialWuPerDay > 0f
            ? daySevenAdults * adultWuPerDay / essentialWuPerDay
            : float.PositiveInfinity;
        bool survivesCrisisWindow = essentialCoverage >= 1f
            && input.FoodSupplyDays >= input.CrisisDurationDays
            && input.WaterSupplyDays >= input.CrisisDurationDays;
        bool recoversByDaySeven = daySevenCoverage >= 1.10f;

        return new DisasterShadowSimulationSnapshot(
            availableAdults,
            availableWuPerDay,
            essentialCoverage,
            growthWuPerDay,
            essentialDeficitWuPerDay,
            foodDaysAfterCrisis,
            waterDaysAfterCrisis,
            daySevenAdults,
            daySevenCoverage,
            survivesCrisisWindow,
            recoversByDaySeven);
    }

    private static (float minimum, float maximum) GetTargetPaybackBand(
        SettlementTechnologyStage stage) => stage switch
    {
        SettlementTechnologyStage.NoResearch => (4f, 12f),
        SettlementTechnologyStage.Early => (12f, 30f),
        SettlementTechnologyStage.Middle => (30f, 60f),
        SettlementTechnologyStage.Industrial => (60f, 120f),
        SettlementTechnologyStage.Late => (120f, 240f),
        SettlementTechnologyStage.Endless => (120f, 240f),
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
    };

    private static float ValidateNonNegative(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and non-negative.");
        return value;
    }
}

public readonly struct DailyLaborBudget
{
    public DailyLaborBudget(
        float sleepSeconds,
        float mealSeconds,
        float drinkSeconds,
        float hygieneSeconds,
        float recreationSeconds,
        float activeWorkSeconds,
        float transitionEfficiency)
    {
        SleepSeconds = sleepSeconds;
        MealSeconds = mealSeconds;
        DrinkSeconds = drinkSeconds;
        HygieneSeconds = hygieneSeconds;
        RecreationSeconds = recreationSeconds;
        ActiveWorkSeconds = activeWorkSeconds;
        TransitionEfficiency = transitionEfficiency;
    }

    public float SleepSeconds { get; }
    public float MealSeconds { get; }
    public float DrinkSeconds { get; }
    public float HygieneSeconds { get; }
    public float RecreationSeconds { get; }
    public float ActiveWorkSeconds { get; }
    public float TransitionEfficiency { get; }
    public float TotalSeconds => SleepSeconds + MealSeconds + DrinkSeconds
        + HygieneSeconds + RecreationSeconds + ActiveWorkSeconds;
    public float NetLaborWu => ActiveWorkSeconds * TransitionEfficiency;
}

public readonly struct TechnologyRoutineSavings
{
    public TechnologyRoutineSavings(
        float mealSeconds,
        float sleepSeconds,
        float drinkSeconds,
        float hygieneSeconds,
        float recreationSeconds)
    {
        MealSeconds = Validate(mealSeconds, nameof(mealSeconds));
        SleepSeconds = Validate(sleepSeconds, nameof(sleepSeconds));
        DrinkSeconds = Validate(drinkSeconds, nameof(drinkSeconds));
        HygieneSeconds = Validate(hygieneSeconds, nameof(hygieneSeconds));
        RecreationSeconds = Validate(recreationSeconds, nameof(recreationSeconds));
    }

    public float MealSeconds { get; }
    public float SleepSeconds { get; }
    public float DrinkSeconds { get; }
    public float HygieneSeconds { get; }
    public float RecreationSeconds { get; }
    public float TotalSeconds => MealSeconds
        + SleepSeconds
        + DrinkSeconds
        + HygieneSeconds
        + RecreationSeconds;

    private static float Validate(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Routine savings must be finite and non-negative.");
        }
        return value;
    }
}

public readonly struct TechnologyDailyRoutineSnapshot
{
    public TechnologyDailyRoutineSnapshot(
        SettlementTechnologyStage stage,
        TechnologyRoutineSavings savings,
        DailyLaborBudget budget,
        float activeWorkPerformance,
        float actualLaborWu)
    {
        Stage = stage;
        Savings = savings;
        Budget = budget;
        ActiveWorkPerformance = activeWorkPerformance;
        ActualLaborWu = actualLaborWu;
    }

    public SettlementTechnologyStage Stage { get; }
    public TechnologyRoutineSavings Savings { get; }
    public DailyLaborBudget Budget { get; }
    public float ActiveWorkPerformance { get; }
    public float ActualLaborWu { get; }
}

public readonly struct TechnologyWuCheckpoint
{
    public TechnologyWuCheckpoint(
        int absoluteDay,
        int medianPopulation,
        float actualLaborWu,
        float processConversion,
        float automationWu,
        float outputEquivalentWu)
    {
        AbsoluteDay = absoluteDay;
        MedianPopulation = medianPopulation;
        ActualLaborWu = actualLaborWu;
        ProcessConversion = processConversion;
        AutomationWu = automationWu;
        OutputEquivalentWu = outputEquivalentWu;
    }

    public int AbsoluteDay { get; }
    public int MedianPopulation { get; }
    public float ActualLaborWu { get; }
    public float ProcessConversion { get; }
    public float AutomationWu { get; }
    public float OutputEquivalentWu { get; }
    public float Index => OutputEquivalentWu
        / SettlementLaborAuthority.EffectiveOutputWuPerAdultDay;
}

public readonly struct SettlementLaborSnapshot
{
    public SettlementLaborSnapshot(
        float actualLaborWu,
        float outputEquivalentWu,
        float realizedGrowthWu,
        float guaranteedGrowthWu)
    {
        ActualLaborWu = actualLaborWu;
        OutputEquivalentWu = outputEquivalentWu;
        RealizedGrowthWu = realizedGrowthWu;
        GuaranteedGrowthWu = guaranteedGrowthWu;
    }

    public float ActualLaborWu { get; }
    public float OutputEquivalentWu { get; }
    public float RealizedGrowthWu { get; }
    public float GuaranteedGrowthWu { get; }
}

public readonly struct ProjectContributionSnapshot
{
    public ProjectContributionSnapshot(
        ProjectScale scale,
        int requestedWorkers,
        int appliedWorkers,
        int maximumWorkers,
        float effectiveWuPerSecond,
        float estimatedSeconds,
        float nextWorkerContribution,
        float estimatedSecondsWithNextWorker,
        float nextWorkerTimeSavedSeconds)
    {
        Scale = scale;
        RequestedWorkers = requestedWorkers;
        AppliedWorkers = appliedWorkers;
        MaximumWorkers = maximumWorkers;
        EffectiveWuPerSecond = effectiveWuPerSecond;
        EstimatedSeconds = estimatedSeconds;
        NextWorkerContribution = nextWorkerContribution;
        EstimatedSecondsWithNextWorker = estimatedSecondsWithNextWorker;
        NextWorkerTimeSavedSeconds = nextWorkerTimeSavedSeconds;
    }

    public ProjectScale Scale { get; }
    public int RequestedWorkers { get; }
    public int AppliedWorkers { get; }
    public int MaximumWorkers { get; }
    public float EffectiveWuPerSecond { get; }
    public float EstimatedSeconds { get; }
    public float NextWorkerContribution { get; }
    public float EstimatedSecondsWithNextWorker { get; }
    public float NextWorkerTimeSavedSeconds { get; }
}

public readonly struct InvestmentReturnSnapshot
{
    public InvestmentReturnSnapshot(
        float investmentWu,
        float dailyNetReturnWu,
        float paybackDays,
        float targetMinimumDays,
        float targetMaximumDays,
        bool withinTarget)
    {
        InvestmentWu = investmentWu;
        DailyNetReturnWu = dailyNetReturnWu;
        PaybackDays = paybackDays;
        TargetMinimumDays = targetMinimumDays;
        TargetMaximumDays = targetMaximumDays;
        WithinTarget = withinTarget;
    }

    public float InvestmentWu { get; }
    public float DailyNetReturnWu { get; }
    public float PaybackDays { get; }
    public float TargetMinimumDays { get; }
    public float TargetMaximumDays { get; }
    public bool WithinTarget { get; }
}

public readonly struct DisasterShadowScenarioInput
{
    public DisasterShadowScenarioInput(
        int productiveAdultCount,
        int unavailableAdultCount,
        int emergencyResponderCount,
        float adultWuPerDay,
        float essentialWuPerDay,
        int foodSupplyDays,
        int waterSupplyDays,
        int crisisDurationDays,
        int recoveredAdultsByDaySeven)
    {
        ProductiveAdultCount = productiveAdultCount;
        UnavailableAdultCount = unavailableAdultCount;
        EmergencyResponderCount = emergencyResponderCount;
        AdultWuPerDay = adultWuPerDay;
        EssentialWuPerDay = essentialWuPerDay;
        FoodSupplyDays = foodSupplyDays;
        WaterSupplyDays = waterSupplyDays;
        CrisisDurationDays = crisisDurationDays;
        RecoveredAdultsByDaySeven = recoveredAdultsByDaySeven;
    }

    public int ProductiveAdultCount { get; }
    public int UnavailableAdultCount { get; }
    public int EmergencyResponderCount { get; }
    public float AdultWuPerDay { get; }
    public float EssentialWuPerDay { get; }
    public int FoodSupplyDays { get; }
    public int WaterSupplyDays { get; }
    public int CrisisDurationDays { get; }
    public int RecoveredAdultsByDaySeven { get; }
}

public readonly struct DisasterShadowSimulationSnapshot
{
    public DisasterShadowSimulationSnapshot(
        int availableAdults,
        float availableWuPerDay,
        float essentialCoverage,
        float growthWuPerDay,
        float essentialDeficitWuPerDay,
        int foodDaysAfterCrisis,
        int waterDaysAfterCrisis,
        int daySevenAvailableAdults,
        float daySevenEssentialCoverage,
        bool survivesCrisisWindow,
        bool recoversByDaySeven)
    {
        AvailableAdults = availableAdults;
        AvailableWuPerDay = availableWuPerDay;
        EssentialCoverage = essentialCoverage;
        GrowthWuPerDay = growthWuPerDay;
        EssentialDeficitWuPerDay = essentialDeficitWuPerDay;
        FoodDaysAfterCrisis = foodDaysAfterCrisis;
        WaterDaysAfterCrisis = waterDaysAfterCrisis;
        DaySevenAvailableAdults = daySevenAvailableAdults;
        DaySevenEssentialCoverage = daySevenEssentialCoverage;
        SurvivesCrisisWindow = survivesCrisisWindow;
        RecoversByDaySeven = recoversByDaySeven;
    }

    public int AvailableAdults { get; }
    public float AvailableWuPerDay { get; }
    public float EssentialCoverage { get; }
    public float GrowthWuPerDay { get; }
    public float EssentialDeficitWuPerDay { get; }
    public int FoodDaysAfterCrisis { get; }
    public int WaterDaysAfterCrisis { get; }
    public int DaySevenAvailableAdults { get; }
    public float DaySevenEssentialCoverage { get; }
    public bool SurvivesCrisisWindow { get; }
    public bool RecoversByDaySeven { get; }
    public bool Passed => SurvivesCrisisWindow && RecoversByDaySeven;
}
