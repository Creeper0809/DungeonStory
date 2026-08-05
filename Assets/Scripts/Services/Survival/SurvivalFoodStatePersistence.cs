using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class SurvivalFoodAggregateState
{
    internal DungeonSurvivalSaveData Data { get; set; } = new();
    internal long MealSequence { get; set; }
}

public sealed class SurvivalFoodRestoreCandidate
{
    internal SurvivalFoodRestoreCandidate(SurvivalFoodAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal SurvivalFoodAggregateState State { get; }
}

internal static class SurvivalFoodStatePersistence
{
    public static void EnsureLists(DungeonSurvivalSaveData state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.health ??= new List<SurvivalHealthSaveData>();
        state.mealLedger ??= new List<CharacterMealLedgerSaveData>();
    }

    public static DungeonSurvivalSaveData Restore(DungeonSurvivalSaveData saveData)
    {
        if (saveData == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        return Clone(saveData);
    }

    public static DungeonSurvivalSaveData Capture(DungeonSurvivalSaveData state)
    {
        EnsureLists(state);
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
            health = state.health
                .Select(CloneHealth)
                .OrderBy(entry => entry.persistentId, StringComparer.Ordinal)
                .ThenBy(entry => entry.state)
                .ToList(),
            mealLedger = state.mealLedger
                .Select(CloneMeal)
                .OrderBy(entry => entry.day)
                .ThenBy(entry => GetRequiredMealSequence(entry))
                .ToList()
        };
    }

    public static void Validate(
        DungeonSurvivalSaveData payload,
        DungeonGameRestoreReport report,
        IItemDefinitionCatalog itemCatalog)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (itemCatalog == null)
        {
            throw new ArgumentNullException(nameof(itemCatalog));
        }
        if (payload == null)
        {
            report.AddError("Survival resources payload is null.");
            return;
        }
        if (payload.version != DungeonSurvivalSaveData.CurrentVersion)
        {
            report.AddError(
                $"Survival resources payload V{payload.version} is unsupported; "
                + $"expected V{DungeonSurvivalSaveData.CurrentVersion}.");
        }
        if (payload.health == null || payload.mealLedger == null)
        {
            report.AddError(
                "Survival resources payload has a null required collection.");
            return;
        }

        ValidateSummary(payload, report);
        ValidateHealth(payload.health, report);
        ValidateMeals(payload, report, itemCatalog);
    }

    public static long GetMealSequence(DungeonSurvivalSaveData payload)
    {
        if (payload?.mealLedger == null || payload.mealLedger.Count == 0)
        {
            return 0L;
        }

        long maximum = 0L;
        foreach (CharacterMealLedgerSaveData meal in payload.mealLedger)
        {
            if (TryParseMealSequence(meal, out long sequence))
            {
                maximum = Math.Max(maximum, sequence);
            }
        }
        return maximum;
    }

    public static CharacterMealLedgerSaveData CloneMeal(CharacterMealLedgerSaveData entry)
    {
        return new CharacterMealLedgerSaveData
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
        };
    }

    public static CharacterActor FindActor(
        IEnumerable<CharacterActor> actors,
        string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return null;
        }

        return (actors ?? Enumerable.Empty<CharacterActor>()).FirstOrDefault(actor =>
            string.Equals(
                actor?.Identity?.PersistentId,
                persistentId,
                StringComparison.Ordinal));
    }

    private static DungeonSurvivalSaveData Clone(DungeonSurvivalSaveData state)
    {
        if (state.health == null || state.mealLedger == null)
        {
            throw new InvalidOperationException(
                "Survival resources cannot clone a payload with null collections.");
        }

        return new DungeonSurvivalSaveData
        {
            version = state.version,
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
            health = state.health.Select(CloneHealth).ToList(),
            mealLedger = state.mealLedger.Select(CloneMeal).ToList()
        };
    }

    private static void ValidateSummary(
        DungeonSurvivalSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload.lastProcessedDay < 0
            || payload.weatherDay < 0
            || payload.weatherDay > payload.lastProcessedDay
                && payload.lastProcessedDay > 0
            || payload.lastNeededFood < 0
            || payload.lastConsumedFood < 0
            || payload.lastMissingFood < 0
            || payload.lastNeededWater < 0
            || payload.lastConsumedWater < 0
            || payload.lastMissingWater < 0
            || payload.consecutiveFoodShortageDays < 0
            || payload.consecutiveWaterShortageDays < 0
            || payload.lastConsumedFuel < 0
            || payload.lastMissingFuel < 0)
        {
            report.AddError("Survival resources payload has invalid negative or future day/count state.");
        }
        if (payload.lastMissingFood
                != Math.Max(0, payload.lastNeededFood - payload.lastConsumedFood)
            || payload.lastConsumedWater > payload.lastNeededWater
            || payload.lastMissingWater
                != payload.lastNeededWater - payload.lastConsumedWater)
        {
            report.AddError("Survival resources food or water summary is arithmetically inconsistent.");
        }
        if (!Enum.IsDefined(typeof(SurvivalWeatherType), payload.currentWeather)
            || !IsFiniteInRange(payload.outdoorTemperature, -100f, 100f)
            || !IsFiniteInRange(payload.sanitationRisk, 0f, 100f)
            || !IsFiniteInRange(payload.diseaseRisk, 0f, 100f)
            || !IsFiniteInRange(payload.exteriorNightDanger, 0f, 100f))
        {
            report.AddError("Survival resources payload has invalid weather or risk state.");
        }
    }

    private static void ValidateHealth(
        IReadOnlyList<SurvivalHealthSaveData> health,
        DungeonGameRestoreReport report)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        string previousKey = null;
        foreach (SurvivalHealthSaveData entry in health)
        {
            string rawCharacterId = entry?.persistentId ?? string.Empty;
            CharacterId characterId = new(rawCharacterId);
            int stateValue = entry == null ? 0 : (int)entry.state;
            string key = $"{rawCharacterId}\u001f{stateValue}";
            if (entry == null
                || !characterId.IsValid
                || !string.Equals(characterId.Value, rawCharacterId, StringComparison.Ordinal)
                || !keys.Add(key)
                || previousKey != null
                    && string.CompareOrdinal(previousKey, key) >= 0)
            {
                report.AddError(
                    "Survival health entries contain a null, non-canonical, duplicate, or unordered character/state key.");
                continue;
            }
            previousKey = key;

            if (!Enum.IsDefined(typeof(SurvivalHealthState), entry.state)
                || !IsFiniteInRange(entry.severity, 0f, 1f)
                || !IsFiniteInRange(entry.remainingSeconds, 0f, float.MaxValue)
                || entry.source == null
                || !string.Equals(entry.source, entry.source.Trim(), StringComparison.Ordinal))
            {
                report.AddError(
                    $"Survival health entry '{rawCharacterId}' has invalid state, numeric data, or source.");
            }
        }
    }

    private static void ValidateMeals(
        DungeonSurvivalSaveData payload,
        DungeonGameRestoreReport report,
        IItemDefinitionCatalog itemCatalog)
    {
        if (payload.mealLedger.Count > 512)
        {
            report.AddError("Survival meal ledger exceeds its 512-entry bound.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        int previousDay = -1;
        long previousSequence = 0L;
        foreach (CharacterMealLedgerSaveData meal in payload.mealLedger)
        {
            string rawCharacterId = meal?.characterId ?? string.Empty;
            string rawFacilityId = meal?.facilityId ?? string.Empty;
            string rawItemId = meal?.itemId ?? string.Empty;
            CharacterId characterId = new(rawCharacterId);
            BuildingInstanceId facilityId = new(rawFacilityId);
            ItemDefinitionId itemId = new(rawItemId);
            bool parsed = TryParseMealSequence(meal, out long sequence);
            if (meal == null
                || string.IsNullOrWhiteSpace(meal.mealId)
                || !string.Equals(meal.mealId, meal.mealId.Trim(), StringComparison.Ordinal)
                || !ids.Add(meal.mealId)
                || !parsed
                || !characterId.IsValid
                || !string.Equals(characterId.Value, rawCharacterId, StringComparison.Ordinal)
                || !facilityId.IsValid
                || !string.Equals(facilityId.Value, rawFacilityId, StringComparison.Ordinal)
                || meal.day < previousDay
                || sequence <= previousSequence)
            {
                report.AddError(
                    "Survival meal ledger contains a null, non-canonical, duplicate, malformed, or unordered entry.");
                continue;
            }
            previousDay = meal.day;
            previousSequence = sequence;

            if (meal.day < 1
                || meal.day > payload.lastProcessedDay
                || meal.amount < 1
                || !Enum.IsDefined(typeof(MealDietClass), meal.dietClass)
                || !Enum.IsDefined(typeof(MealQualityTier), meal.quality)
                || !IsFiniteInRange(meal.nutrition, 0f, float.MaxValue)
                || meal.displayName == null
                || !string.Equals(
                    meal.displayName,
                    meal.displayName.Trim(),
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Survival meal '{meal.mealId}' has invalid day, amount, enum, nutrition, or display data.");
            }
            if (!string.IsNullOrEmpty(rawItemId)
                && (!itemId.IsValid
                    || !string.Equals(itemId.Value, rawItemId, StringComparison.Ordinal)
                    || !itemCatalog.TryGet(itemId, out _)))
            {
                report.AddError(
                    $"Survival meal '{meal.mealId}' references unknown item '{rawItemId}'.");
            }
        }
    }

    private static bool TryParseMealSequence(
        CharacterMealLedgerSaveData meal,
        out long sequence)
    {
        sequence = 0L;
        if (meal == null || meal.day < 1)
        {
            return false;
        }

        string prefix = $"meal:{meal.day}:{meal.characterId}:";
        if (string.IsNullOrWhiteSpace(meal.mealId)
            || !meal.mealId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string rawSequence = meal.mealId.Substring(prefix.Length);
        return long.TryParse(
                rawSequence,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out sequence)
            && sequence > 0L
            && string.Equals(
                rawSequence,
                sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static long GetRequiredMealSequence(CharacterMealLedgerSaveData meal)
    {
        if (!TryParseMealSequence(meal, out long sequence))
        {
            throw new InvalidOperationException(
                $"Survival meal '{meal?.mealId ?? "<null>"}' has an invalid persistent ID.");
        }
        return sequence;
    }

    private static bool IsFiniteInRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;

    private static SurvivalHealthSaveData CloneHealth(SurvivalHealthSaveData entry)
    {
        return new SurvivalHealthSaveData
        {
            persistentId = entry.persistentId,
            state = entry.state,
            severity = entry.severity,
            remainingSeconds = entry.remainingSeconds,
            source = entry.source
        };
    }
}
