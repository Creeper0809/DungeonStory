using System;
using System.Collections.Generic;
using DungeonStory.Foundation;

public readonly struct SettlementPopulationCapacitySnapshot
{
    public SettlementPopulationCapacitySnapshot(
        int residentCount,
        int sleepingSlotCount,
        int vacantSleepingSlotCount,
        int foodSupplyDays,
        int waterSupplyDays,
        float sanitationRisk,
        float diseaseRisk,
        int untreatedCount,
        SettlementThreatAlertLevel alertLevel,
        float emergencyReserveCoverage,
        float rollingPerCapitaNetWuIndex,
        long latestGuaranteedGrowthMilliWu)
    {
        ResidentCount = Math.Max(0, residentCount);
        SleepingSlotCount = Math.Max(0, sleepingSlotCount);
        VacantSleepingSlotCount = Math.Max(0, vacantSleepingSlotCount);
        FoodSupplyDays = Math.Max(0, foodSupplyDays);
        WaterSupplyDays = Math.Max(0, waterSupplyDays);
        SanitationRisk = Math.Max(0f, sanitationRisk);
        DiseaseRisk = Math.Max(0f, diseaseRisk);
        UntreatedCount = Math.Max(0, untreatedCount);
        AlertLevel = alertLevel;
        EmergencyReserveCoverage = Math.Max(0f, emergencyReserveCoverage);
        RollingPerCapitaNetWuIndex = Math.Max(0f, rollingPerCapitaNetWuIndex);
        LatestGuaranteedGrowthMilliWu = Math.Max(0L, latestGuaranteedGrowthMilliWu);
    }

    public int ResidentCount { get; }
    public int SleepingSlotCount { get; }
    public int VacantSleepingSlotCount { get; }
    public int FoodSupplyDays { get; }
    public int WaterSupplyDays { get; }
    public float SanitationRisk { get; }
    public float DiseaseRisk { get; }
    public int UntreatedCount { get; }
    public SettlementThreatAlertLevel AlertLevel { get; }
    public float EmergencyReserveCoverage { get; }
    public float RollingPerCapitaNetWuIndex { get; }
    public long LatestGuaranteedGrowthMilliWu { get; }
}

public readonly struct SettlementPopulationAcceptance
{
    public SettlementPopulationAcceptance(
        bool accepted,
        string failureCode,
        string message)
    {
        Accepted = accepted;
        FailureCode = failureCode ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public bool Accepted { get; }
    public string FailureCode { get; }
    public string Message { get; }
}

public interface ISettlementPopulationCapacityQuery
{
    SettlementPopulationCapacitySnapshot CapturePopulationCapacity();
    SettlementPopulationAcceptance EvaluateImmigration(
        SettlementImmigrationPolicy policy);
}

/// <summary>
/// Pure acceptance rules. Policies change the admission threshold only; they
/// never add candidates or compare the live population to a target population.
/// </summary>
public static class SettlementPopulationAcceptanceRules
{
    private const long OnboardingGuaranteedMilliWu =
        3L * EmergencyWuUnits.UnitsPerWu;

    public static SettlementPopulationAcceptance Evaluate(
        in SettlementPopulationCapacitySnapshot capacity,
        SettlementImmigrationPolicy policy)
    {
        if (!Enum.IsDefined(typeof(SettlementImmigrationPolicy), policy))
        {
            return Reject(
                "ImmigrationPolicyInvalid",
                "The settlement immigration policy is invalid.");
        }

        ResolveThresholds(
            policy,
            out int supplyDays,
            out float maximumSanitationRisk,
            out float maximumDiseaseRisk,
            out float minimumReserveCoverage,
            out float minimumPerCapitaIndex);

        if (capacity.VacantSleepingSlotCount < 1)
        {
            return Reject(
                "ImmigrationSleepingSlotUnavailable",
                "At least one unoccupied sleeping slot is required.");
        }
        if (capacity.FoodSupplyDays < supplyDays)
        {
            return Reject(
                "ImmigrationFoodForecastInsufficient",
                $"Food coverage is {capacity.FoodSupplyDays} days; policy requires {supplyDays}.");
        }
        if (capacity.WaterSupplyDays < supplyDays)
        {
            return Reject(
                "ImmigrationWaterForecastInsufficient",
                $"Water coverage is {capacity.WaterSupplyDays} days; policy requires {supplyDays}.");
        }
        if (capacity.SanitationRisk > maximumSanitationRisk)
        {
            return Reject(
                "ImmigrationSanitationCapacityInsufficient",
                $"Sanitation risk {capacity.SanitationRisk:0.#} exceeds {maximumSanitationRisk:0.#}.");
        }
        if (capacity.DiseaseRisk > maximumDiseaseRisk
            || capacity.UntreatedCount > 0)
        {
            return Reject(
                "ImmigrationMedicalCapacityInsufficient",
                "Disease pressure or untreated patients exceed the admission policy.");
        }
        if (capacity.AlertLevel == SettlementThreatAlertLevel.Red)
        {
            return Reject(
                "ImmigrationActiveEmergency",
                "Recruitment cannot finish during a committed red alert.");
        }
        if (capacity.EmergencyReserveCoverage < minimumReserveCoverage)
        {
            return Reject(
                "ImmigrationEmergencyReserveInsufficient",
                $"Emergency reserve coverage {capacity.EmergencyReserveCoverage:0.00} is below {minimumReserveCoverage:0.00}.");
        }
        if (capacity.RollingPerCapitaNetWuIndex > 0f
            && capacity.RollingPerCapitaNetWuIndex < minimumPerCapitaIndex)
        {
            return Reject(
                "ImmigrationProductivityInsufficient",
                $"Per-capita net WU {capacity.RollingPerCapitaNetWuIndex:0.00} is below {minimumPerCapitaIndex:0.00}.");
        }
        if (capacity.LatestGuaranteedGrowthMilliWu > 0L
            && capacity.LatestGuaranteedGrowthMilliWu
                < OnboardingGuaranteedMilliWu)
        {
            return Reject(
                "ImmigrationOnboardingWuInsufficient",
                "The latest guaranteed growth budget cannot cover 3 onboarding WU.");
        }

        return new SettlementPopulationAcceptance(
            true,
            string.Empty,
            "The settlement can safely receive one recruit under the current policy.");
    }

    private static void ResolveThresholds(
        SettlementImmigrationPolicy policy,
        out int supplyDays,
        out float maximumSanitationRisk,
        out float maximumDiseaseRisk,
        out float minimumReserveCoverage,
        out float minimumPerCapitaIndex)
    {
        switch (policy)
        {
            case SettlementImmigrationPolicy.Conservative:
                supplyDays = 30;
                maximumSanitationRisk = 25f;
                maximumDiseaseRisk = 20f;
                minimumReserveCoverage = 1.25f;
                minimumPerCapitaIndex = 1.00f;
                break;
            case SettlementImmigrationPolicy.Open:
                supplyDays = 7;
                maximumSanitationRisk = 60f;
                maximumDiseaseRisk = 50f;
                minimumReserveCoverage = 0.85f;
                minimumPerCapitaIndex = 0.80f;
                break;
            default:
                supplyDays = 14;
                maximumSanitationRisk = 40f;
                maximumDiseaseRisk = 35f;
                minimumReserveCoverage = 1.00f;
                minimumPerCapitaIndex = 0.90f;
                break;
        }
    }

    private static SettlementPopulationAcceptance Reject(
        string code,
        string message) =>
        new SettlementPopulationAcceptance(false, code, message);
}

/// <summary>
/// Reads only live physical capacity and settled performance. It has no target
/// population input, no rubber-banding probability and no candidate generator.
/// </summary>
public sealed class SettlementPopulationCapacityRuntime :
    ISettlementPopulationCapacityQuery
{
    private static readonly HashSet<string> SleepingFacilityCodes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "R01",
            "R02",
            "R03"
        };

    private readonly ICharacterWorldQuery characters;
    private readonly IBuildingWorldQuery buildings;
    private readonly ISurvivalFoodQuery survival;
    private readonly ISettlementAlertService alerts;
    private readonly ISettlementLaborAccountingService labor;

    public SettlementPopulationCapacityRuntime(
        ICharacterWorldQuery characters,
        IBuildingWorldQuery buildings,
        ISurvivalFoodQuery survival,
        ISettlementAlertService alerts,
        ISettlementLaborAccountingService labor)
    {
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.survival = survival
            ?? throw new ArgumentNullException(nameof(survival));
        this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        this.labor = labor ?? throw new ArgumentNullException(nameof(labor));
    }

    public SettlementPopulationCapacitySnapshot CapturePopulationCapacity()
    {
        int residents = CountResidents();
        int sleepingSlots = CountSleepingSlots();
        SurvivalFoodOverview food = survival.GetOverview();
        SettlementAlertSnapshot alert = alerts.Capture();
        SettlementLaborAccountingSnapshot laborSnapshot = labor.Capture();
        return new SettlementPopulationCapacitySnapshot(
            residents,
            sleepingSlots,
            Math.Max(0, sleepingSlots - residents),
            food.ShortageDays,
            food.WaterShortageDays,
            food.SanitationRisk,
            food.DiseaseRisk,
            food.UntreatedCount,
            alert.CommittedLevel,
            alert.ReserveCoverage,
            laborSnapshot.RollingPerCapitaNetWuMedian,
            laborSnapshot.LatestDay.GuaranteedGrowthMilliWu);
    }

    public SettlementPopulationAcceptance EvaluateImmigration(
        SettlementImmigrationPolicy policy)
    {
        SettlementPopulationCapacitySnapshot capacity =
            CapturePopulationCapacity();
        return SettlementPopulationAcceptanceRules.Evaluate(
            in capacity,
            policy);
    }

    private int CountResidents()
    {
        int count = 0;
        IReadOnlyList<CharacterActor> current = characters.Characters;
        for (int index = 0; index < current.Count; index++)
        {
            CharacterActor actor = current[index];
            if (actor != null
                && !actor.IsDead
                && (actor.Role == CharacterRole.Owner
                    || CharacterWorkRoleUtility.TryGetWork(actor, out _)))
            {
                count++;
            }
        }
        return count;
    }

    private int CountSleepingSlots()
    {
        int count = 0;
        IReadOnlyList<BuildableObject> current = buildings.Buildings;
        for (int index = 0; index < current.Count; index++)
        {
            BuildableObject building = current[index];
            if (building == null
                || building.isDestroy
                || !SleepingFacilityCodes.Contains(
                    building.BuildingData.GetFacilityCode()))
            {
                continue;
            }

            count = checked(count + Math.Max(
                1,
                building.BuildingData?.Facility?.capacity ?? 1));
        }
        return count;
    }
}
