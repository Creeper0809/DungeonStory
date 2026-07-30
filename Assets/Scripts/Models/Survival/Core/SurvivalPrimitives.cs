using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum MealDietClass
{
    Vegan = 0,
    Vegetarian = 1,
    Mixed = 2,
    Carnivore = 3
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum MealQualityTier
{
    Simple = 0,
    Fine = 1,
    Lavish = 2,
    Preserved = 3
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonSurvivalSaveData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public int lastProcessedDay;
    public int lastNeededFood;
    public int lastConsumedFood;
    public int lastMissingFood;
    public int lastNeededWater;
    public int lastConsumedWater;
    public int lastMissingWater;
    public int consecutiveFoodShortageDays;
    public int consecutiveWaterShortageDays;
    public int lastConsumedFuel;
    public int lastMissingFuel;
    public SurvivalWeatherType currentWeather = SurvivalWeatherType.Clear;
    public int weatherDay;
    public float outdoorTemperature = 18f;
    public float sanitationRisk;
    public float diseaseRisk;
    public float exteriorNightDanger;
    public List<SurvivalFoodSpoilageSaveData> spoilage =
        new List<SurvivalFoodSpoilageSaveData>();
    public List<SurvivalHealthSaveData> health =
        new List<SurvivalHealthSaveData>();
    public List<CharacterMealLedgerSaveData> mealLedger =
        new List<CharacterMealLedgerSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterMealLedgerSaveData
{
    public string mealId = string.Empty;
    public string characterId = string.Empty;
    public string facilityId = string.Empty;
    public string itemId = string.Empty;
    public string displayName = string.Empty;
    public MealDietClass dietClass = MealDietClass.Vegan;
    public MealQualityTier quality = MealQualityTier.Simple;
    public float nutrition;
    public bool policyViolation;
    public bool contaminated;
    public int day;
    public int amount = 1;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterMealConsumedEvent
{
    public CharacterMealConsumedEvent(
        string mealId,
        string characterId,
        string facilityId,
        int day,
        int amount)
        : this(
            mealId,
            characterId,
            facilityId,
            string.Empty,
            string.Empty,
            MealDietClass.Vegan,
            MealQualityTier.Simple,
            0f,
            false,
            false,
            day,
            amount)
    {
    }

    public CharacterMealConsumedEvent(
        string mealId,
        string characterId,
        string facilityId,
        string itemId,
        string displayName,
        MealDietClass dietClass,
        MealQualityTier quality,
        float nutrition,
        bool policyViolation,
        bool contaminated,
        int day,
        int amount)
    {
        MealId = mealId ?? string.Empty;
        CharacterId = characterId ?? string.Empty;
        FacilityId = facilityId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        DietClass = dietClass;
        Quality = quality;
        Nutrition = Mathf.Max(0f, nutrition);
        PolicyViolation = policyViolation;
        Contaminated = contaminated;
        Day = Mathf.Max(1, day);
        Amount = Mathf.Max(1, amount);
    }

    public string MealId { get; }
    public string CharacterId { get; }
    public string FacilityId { get; }
    public string ItemId { get; }
    public string DisplayName { get; }
    public MealDietClass DietClass { get; }
    public MealQualityTier Quality { get; }
    public float Nutrition { get; }
    public bool PolicyViolation { get; }
    public bool Contaminated { get; }
    public int Day { get; }
    public int Amount { get; }
}

public readonly struct SurvivalEnvironmentSnapshot
{
    public SurvivalEnvironmentSnapshot(
        SurvivalWeatherType weather,
        float outdoorTemperature,
        float exteriorNightDanger,
        float sanitationRisk,
        float diseaseRisk)
    {
        Weather = weather;
        OutdoorTemperature = outdoorTemperature;
        ExteriorNightDanger = Mathf.Clamp(exteriorNightDanger, 0f, 100f);
        SanitationRisk = Mathf.Clamp(sanitationRisk, 0f, 100f);
        DiseaseRisk = Mathf.Clamp(diseaseRisk, 0f, 100f);
    }

    public SurvivalWeatherType Weather { get; }
    public float OutdoorTemperature { get; }
    public float ExteriorNightDanger { get; }
    public float SanitationRisk { get; }
    public float DiseaseRisk { get; }

    public float WeatherPressure01 => Weather switch
    {
        SurvivalWeatherType.Storm => 0.9f,
        SurvivalWeatherType.HeatWave => 0.8f,
        SurvivalWeatherType.ColdSnap => 0.8f,
        SurvivalWeatherType.Rain => 0.55f,
        SurvivalWeatherType.Fog => 0.45f,
        _ => 0.1f
    };
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum SurvivalWeatherType
{
    Clear = 0,
    Rain = 1,
    Fog = 2,
    HeatWave = 3,
    ColdSnap = 4,
    Storm = 5
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum SurvivalHealthState
{
    Healthy = 0,
    Thirsty = 1,
    Hungry = 2,
    Exposed = 3,
    Sick = 4,
    Infected = 5,
    Recovering = 6
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SurvivalFoodSpoilageSaveData
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public float remainingFreshnessSeconds;
    public bool preserved;
    public bool contaminated;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SurvivalHealthSaveData
{
    public string persistentId = string.Empty;
    public SurvivalHealthState state = SurvivalHealthState.Healthy;
    public float severity;
    public float remainingSeconds;
    public string source = string.Empty;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct SurvivalFoodOverview
{
    public SurvivalFoodOverview(
        int todayRequired,
        int storedFood,
        int looseFood,
        int carcassCount,
        int butcherPendingFood,
        int shortageDays)
        : this(
            todayRequired,
            storedFood,
            looseFood,
            carcassCount,
            butcherPendingFood,
            shortageDays,
            todayRequired,
            0,
            0,
            0,
            0,
            0,
            SurvivalWeatherType.Clear,
            18f,
            0f,
            0f,
            0f,
            0,
            0)
    {
    }

    public SurvivalFoodOverview(
        int todayRequired,
        int storedFood,
        int looseFood,
        int carcassCount,
        int butcherPendingFood,
        int shortageDays,
        int todayRequiredWater,
        int storedWater,
        int looseWater,
        int storedFuel,
        int storedMedicine,
        int spoilageWarningCount,
        SurvivalWeatherType weather,
        float outdoorTemperature,
        float sanitationRisk,
        float diseaseRisk,
        float exteriorNightDanger,
        int sickCount,
        int untreatedCount)
    {
        TodayRequired = todayRequired;
        StoredFood = storedFood;
        LooseFood = looseFood;
        CarcassCount = carcassCount;
        ButcherPendingFood = butcherPendingFood;
        ShortageDays = shortageDays;
        TodayRequiredWater = Mathf.Max(0, todayRequiredWater);
        StoredWater = Mathf.Max(0, storedWater);
        LooseWater = Mathf.Max(0, looseWater);
        StoredFuel = Mathf.Max(0, storedFuel);
        StoredMedicine = Mathf.Max(0, storedMedicine);
        SpoilageWarningCount = Mathf.Max(0, spoilageWarningCount);
        Weather = weather;
        OutdoorTemperature = outdoorTemperature;
        SanitationRisk = Mathf.Clamp(sanitationRisk, 0f, 100f);
        DiseaseRisk = Mathf.Clamp(diseaseRisk, 0f, 100f);
        ExteriorNightDanger = Mathf.Clamp(exteriorNightDanger, 0f, 100f);
        SickCount = Mathf.Max(0, sickCount);
        UntreatedCount = Mathf.Max(0, untreatedCount);
    }

    public int TodayRequired { get; }
    public int StoredFood { get; }
    public int LooseFood { get; }
    public int CarcassCount { get; }
    public int ButcherPendingFood { get; }
    public int ShortageDays { get; }
    public int TodayRequiredWater { get; }
    public int StoredWater { get; }
    public int LooseWater { get; }
    public int StoredFuel { get; }
    public int StoredMedicine { get; }
    public int SpoilageWarningCount { get; }
    public SurvivalWeatherType Weather { get; }
    public float OutdoorTemperature { get; }
    public float SanitationRisk { get; }
    public float DiseaseRisk { get; }
    public float ExteriorNightDanger { get; }
    public int SickCount { get; }
    public int UntreatedCount { get; }
    public int WaterShortageDays => TodayRequiredWater <= 0
        ? int.MaxValue
        : Mathf.FloorToInt(
            (StoredWater + LooseWater) / (float)TodayRequiredWater);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct SurvivalItemStatus
{
    public SurvivalItemStatus(
        bool tracked,
        bool preserved,
        bool contaminated,
        float freshness01,
        float remainingFreshnessSeconds,
        string label)
    {
        Tracked = tracked;
        Preserved = preserved;
        Contaminated = contaminated;
        Freshness01 = Mathf.Clamp01(freshness01);
        RemainingFreshnessSeconds = Mathf.Max(0f, remainingFreshnessSeconds);
        Label = label ?? string.Empty;
    }

    public bool Tracked { get; }
    public bool Preserved { get; }
    public bool Contaminated { get; }
    public float Freshness01 { get; }
    public float RemainingFreshnessSeconds { get; }
    public string Label { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct SurvivalCharacterStatus
{
    public SurvivalCharacterStatus(
        bool hasStatus,
        SurvivalHealthState primaryState,
        float severity01,
        float remainingSeconds,
        string source,
        int activeIssueCount,
        float temperatureComfort01,
        string waterSummary,
        string foodSummary)
    {
        HasStatus = hasStatus;
        PrimaryState = primaryState;
        Severity01 = Mathf.Clamp01(severity01);
        RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        Source = source ?? string.Empty;
        ActiveIssueCount = Mathf.Max(0, activeIssueCount);
        TemperatureComfort01 = Mathf.Clamp01(temperatureComfort01);
        WaterSummary = waterSummary ?? string.Empty;
        FoodSummary = foodSummary ?? string.Empty;
    }

    public bool HasStatus { get; }
    public SurvivalHealthState PrimaryState { get; }
    public float Severity01 { get; }
    public float RemainingSeconds { get; }
    public string Source { get; }
    public int ActiveIssueCount { get; }
    public float TemperatureComfort01 { get; }
    public string WaterSummary { get; }
    public string FoodSummary { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum DeprivationKind
{
    Hunger = 0,
    Thirst = 1,
    Bladder = 2,
    Contamination = 3,
    Exhaustion = 4,
    MentalInstability = 5
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterBreakdownKind
{
    None = 0,
    DesperateRelief = 1,
    DesperateDrink = 2,
    DesperateEat = 3,
    Collapse = 4,
    ViolentImpulse = 5
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldFilthType
{
    Waste = 0,
    Blood = 1,
    Rot = 2,
    Stain = 3,
    Sewage = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldWaterQuality
{
    Clean = 0,
    Unsafe = 1,
    Foul = 2
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DeprivationBurdenSaveData
{
    public DeprivationKind kind;
    public float burden;
    public float maximumHeldSeconds;
    public float nextBreakdownCheckAt;
    public float nextDamageAt;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterBreakdownState
{
    public bool active;
    public CharacterBreakdownKind kind;
    public DeprivationKind cause;
    public string targetId = string.Empty;
    public int targetGridX;
    public int targetGridY;
    public float startedAt;
    public float suppressionResistance;
    public string lastReplanReason = string.Empty;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterDeprivationState
{
    public string persistentId = string.Empty;
    public List<DeprivationBurdenSaveData> burdens =
        new List<DeprivationBurdenSaveData>();
    public CharacterBreakdownState breakdown = new CharacterBreakdownState();
    public List<string> tabooMemories = new List<string>();
    public float infectionBurden;
    public float lastUpdatedAt;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldFilthSaveData
{
    public string filthId = string.Empty;
    public WorldFilthType type;
    public float amount;
    public int gridX;
    public int gridY;
    public string sourceCharacterId = string.Empty;
    public float infectionRisk;
    public bool wallStain;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldWaterSourceSaveData
{
    public string sourceId = string.Empty;
    public int gridX;
    public int gridY;
    public GridCellTerrainType terrainType = GridCellTerrainType.ShallowWater;
    public WorldWaterQuality quality = WorldWaterQuality.Unsafe;
    public float capacity = 12f;
    public float remaining = 12f;
    public float regenerationPerSecond = 0.02f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonDarkSurvivalSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int nextFilthSequence = 1;
    public int nextWaterSequence = 1;
    public List<CharacterDeprivationState> characters =
        new List<CharacterDeprivationState>();
    public List<WorldFilthSaveData> filth = new List<WorldFilthSaveData>();
    public List<WorldWaterSourceSaveData> waterSources =
        new List<WorldWaterSourceSaveData>();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterDeprivationSnapshot
{
    public CharacterDeprivationSnapshot(
        IReadOnlyDictionary<DeprivationKind, float> burdens,
        CharacterBreakdownState breakdown,
        float infectionBurden,
        IReadOnlyList<string> tabooMemories)
    {
        Burdens = burdens;
        Breakdown = breakdown;
        InfectionBurden = Mathf.Clamp(infectionBurden, 0f, 100f);
        TabooMemories = tabooMemories ?? Array.Empty<string>();
    }

    public IReadOnlyDictionary<DeprivationKind, float> Burdens { get; }
    public CharacterBreakdownState Breakdown { get; }
    public float InfectionBurden { get; }
    public IReadOnlyList<string> TabooMemories { get; }

    public float HighestBurden
    {
        get
        {
            float highest = 0f;
            if (Burdens != null)
            {
                foreach (float burden in Burdens.Values)
                {
                    highest = Mathf.Max(highest, burden);
                }
            }

            return highest;
        }
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterDeprivationDisplayState
{
    public CharacterDeprivationDisplayState(
        float highestBurden,
        CharacterBreakdownKind breakdownKind,
        bool breakdownActive)
    {
        HighestBurden = Mathf.Clamp(highestBurden, 0f, 100f);
        BreakdownKind = breakdownKind;
        BreakdownActive = breakdownActive;
    }

    public float HighestBurden { get; }
    public CharacterBreakdownKind BreakdownKind { get; }
    public bool BreakdownActive { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct WorldFilthSnapshot
{
    public WorldFilthSnapshot(
        string filthId,
        WorldFilthType type,
        float amount,
        Vector2Int position,
        string sourceCharacterId,
        float infectionRisk,
        bool wallStain)
    {
        FilthId = filthId ?? string.Empty;
        Type = type;
        Amount = Mathf.Max(0f, amount);
        Position = position;
        SourceCharacterId = sourceCharacterId ?? string.Empty;
        InfectionRisk = Mathf.Clamp01(infectionRisk);
        WallStain = wallStain;
    }

    public string FilthId { get; }
    public WorldFilthType Type { get; }
    public float Amount { get; }
    public Vector2Int Position { get; }
    public string SourceCharacterId { get; }
    public float InfectionRisk { get; }
    public bool WallStain { get; }
    public float RequiredCleaningWork => Mathf.Max(5f, Amount * 12f);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct WorldWaterSourceSnapshot
{
    public WorldWaterSourceSnapshot(
        string sourceId,
        Vector2Int position,
        GridCellTerrainType terrainType,
        WorldWaterQuality quality,
        float capacity,
        float remaining,
        float regenerationPerSecond)
    {
        SourceId = sourceId ?? string.Empty;
        Position = position;
        TerrainType = terrainType;
        Quality = quality;
        Capacity = Mathf.Max(0f, capacity);
        Remaining = Mathf.Clamp(remaining, 0f, Capacity);
        RegenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
    }

    public string SourceId { get; }
    public Vector2Int Position { get; }
    public GridCellTerrainType TerrainType { get; }
    public WorldWaterQuality Quality { get; }
    public float Capacity { get; }
    public float Remaining { get; }
    public float RegenerationPerSecond { get; }
    public bool CanDrink => Remaining > 0.05f;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IWorldFilthQuery
{
    int StateVersion { get; }
    IReadOnlyList<WorldFilthSnapshot> GetAll();
    IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position);
    WorldFilthSnapshot AddFilth(
        WorldFilthType type,
        Vector2Int position,
        float amount,
        string sourceCharacterId,
        float infectionRisk,
        bool wallStain = false);
    bool Clean(string filthId, float workAmount, out float remainingAmount);
    float GetCleanlinessPenalty(Vector2Int position, int radius = 0);
    List<WorldFilthSaveData> CaptureFilth();
    void RestoreFilth(
        IEnumerable<WorldFilthSaveData> saveData,
        int nextSequence);
    int NextFilthSequence { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IWorldWaterQuery
{
    IReadOnlyList<WorldWaterSourceSnapshot> GetAllSources();
    bool TryGetSource(
        string sourceId,
        out WorldWaterSourceSnapshot source);
    bool TryFindDrinkSource(
        Vector2Int origin,
        bool allowFoul,
        out WorldWaterSourceSnapshot source);
    bool TryDrink(
        string sourceId,
        float amount,
        out WorldWaterQuality quality,
        out float consumed);
    List<WorldWaterSourceSaveData> CaptureWaterSources();
    void RestoreWaterSources(
        IEnumerable<WorldWaterSourceSaveData> saveData,
        int nextSequence);
    int NextWaterSequence { get; }
    bool DebugCreateSource(
        Vector2Int position,
        WorldWaterQuality quality,
        float capacity,
        GridCellTerrainType terrainType,
        out string sourceId);
    bool DebugSetSource(
        string sourceId,
        WorldWaterQuality quality,
        float capacity,
        float remaining);
}
