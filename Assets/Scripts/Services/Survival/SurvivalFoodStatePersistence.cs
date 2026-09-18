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
        state.activeTreatmentPlans ??=
            new List<SurvivalTreatmentPlanSaveData>();
        state.completedTreatmentOperationIds ??= new List<string>();
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
                .ToList(),
            activeTreatmentPlans = state.activeTreatmentPlans
                .Select(CloneTreatmentPlan)
                .OrderBy(entry => entry.operationId, StringComparer.Ordinal)
                .ToList(),
            completedTreatmentOperationIds = state
                .completedTreatmentOperationIds
                .OrderBy(value => value, StringComparer.Ordinal)
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
        if (payload.health == null
            || payload.mealLedger == null
            || payload.activeTreatmentPlans == null
            || payload.completedTreatmentOperationIds == null)
        {
            report.AddError(
                "Survival resources payload has a null required collection.");
            return;
        }

        ValidateSummary(payload, report);
        ValidateHealth(payload.health, report);
        ValidateMeals(payload, report, itemCatalog);
        ValidateTreatmentPlans(payload, report, itemCatalog);
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

    public static SurvivalTreatmentPlanSaveData CloneTreatmentPlan(
        SurvivalTreatmentPlanSaveData entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }
        return new SurvivalTreatmentPlanSaveData
        {
            operationId = entry.operationId,
            patientId = entry.patientId,
            facilityInstanceId = entry.facilityInstanceId,
            serviceSessionId = entry.serviceSessionId,
            itemDefinitionId = entry.itemDefinitionId,
            sourceStackId = entry.sourceStackId,
            destinationId = entry.destinationId,
            usedBloodSubstitute = entry.usedBloodSubstitute,
            phase = entry.phase,
            physicalCommitOperationId = entry.physicalCommitOperationId,
            physicalCommitReasonCode = entry.physicalCommitReasonCode,
            physicalCommitId = entry.physicalCommitId,
            physicalCommitSourceStackIds = (entry.physicalCommitSourceStackIds
                    ?? new List<string>())
                .ToList(),
            physicalCommitQuantity = entry.physicalCommitQuantity,
            physicalCommitInputMassGrams = entry.physicalCommitInputMassGrams,
            physicalCommitPositionX = entry.physicalCommitPositionX,
            physicalCommitPositionY = entry.physicalCommitPositionY
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
        if (state.health == null
            || state.mealLedger == null
            || state.activeTreatmentPlans == null
            || state.completedTreatmentOperationIds == null)
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
            sanitationRisk = state.sanitationRisk,
            diseaseRisk = state.diseaseRisk,
            exteriorNightDanger = state.exteriorNightDanger,
            health = state.health.Select(CloneHealth).ToList(),
            mealLedger = state.mealLedger.Select(CloneMeal).ToList(),
            activeTreatmentPlans = state.activeTreatmentPlans
                .Select(CloneTreatmentPlan)
                .ToList(),
            completedTreatmentOperationIds = state
                .completedTreatmentOperationIds.ToList()
        };
    }

    private static void ValidateSummary(
        DungeonSurvivalSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload.lastProcessedDay < 0
            || payload.lastNeededFood < 0
            || payload.lastConsumedFood < 0
            || payload.lastMissingFood < 0
            || payload.lastNeededWater < 0
            || payload.lastConsumedWater < 0
            || payload.lastMissingWater < 0
            || payload.consecutiveFoodShortageDays < 0
            || payload.consecutiveWaterShortageDays < 0)
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
        if (!IsFiniteInRange(payload.sanitationRisk, 0f, 100f)
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

    private static void ValidateTreatmentPlans(
        DungeonSurvivalSaveData payload,
        DungeonGameRestoreReport report,
        IItemDefinitionCatalog itemCatalog)
    {
        if (payload.activeTreatmentPlans.Count > 64
            || payload.completedTreatmentOperationIds.Count > 512)
        {
            report.AddError(
                "Survival treatment operation authority exceeds its bound.");
        }

        HashSet<string> active = new(StringComparer.Ordinal);
        string previousOperationId = null;
        foreach (SurvivalTreatmentPlanSaveData plan in
                 payload.activeTreatmentPlans)
        {
            string operationId = plan?.operationId ?? string.Empty;
            CharacterId patientId = new(plan?.patientId ?? string.Empty);
            BuildingInstanceId facilityId = new(
                plan?.facilityInstanceId ?? string.Empty);
            ItemDefinitionId itemId = new(
                plan?.itemDefinitionId ?? string.Empty);
            ItemStackId sourceStackId = new(
                plan?.sourceStackId ?? string.Empty);
            bool primaryValid = plan != null
                && new ConsumableOperationId(operationId).IsValid
                && patientId.IsValid
                && facilityId.IsValid
                && itemId.IsValid
                && sourceStackId.IsValid
                && active.Add(operationId)
                && (previousOperationId == null
                    || string.CompareOrdinal(previousOperationId, operationId) < 0)
                && Enum.IsDefined(typeof(SurvivalTreatmentPlanPhase), plan.phase)
                && IsCanonicalOptional(plan.serviceSessionId)
                && itemCatalog.TryGet(itemId, out ItemDefinitionSO definition)
                && definition != null
                && (definition.StockCategory is StockCategory.Medicine
                    or StockCategory.Biological)
                && plan.usedBloodSubstitute
                    == (definition.StockCategory == StockCategory.Biological)
                && string.Equals(
                    plan.destinationId,
                    CharacterConsumablesInputDestinationIdentity.Build(
                        CharacterConsumablesInputKind.MedicalTreatment,
                        facilityId,
                        new ConsumableItemDefinitionId(itemId.Value)),
                    StringComparison.Ordinal)
                && string.Equals(
                    plan.physicalCommitOperationId,
                    SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(
                        operationId),
                    StringComparison.Ordinal)
                && string.Equals(
                    plan.physicalCommitReasonCode,
                    SurvivalFoodRuntime.TreatmentPhysicalSinkReason,
                    StringComparison.Ordinal);
            if (!primaryValid)
            {
                report.AddError(
                    "Survival treatment plans contain a null, non-canonical, duplicate, unordered, or incompatible owner row.");
                continue;
            }
            previousOperationId = operationId;

            bool commitPublished = plan.phase >=
                SurvivalTreatmentPlanPhase.ItemCommitted;
            List<string> sourceIds = plan.physicalCommitSourceStackIds;
            bool commitFieldsValid = commitPublished
                ? sourceIds != null
                    && sourceIds.Count > 0
                    && sourceIds.All(value => new ItemStackId(value).IsValid)
                    && sourceIds.SequenceEqual(
                        sourceIds.OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal)
                    && sourceIds.Distinct(StringComparer.Ordinal).Count()
                        == sourceIds.Count
                    && sourceIds.Contains(plan.sourceStackId, StringComparer.Ordinal)
                    && plan.physicalCommitQuantity == 1
                    && plan.physicalCommitInputMassGrams > 0L
                    && IsCanonicalRequired(plan.physicalCommitId)
                : sourceIds != null
                    && sourceIds.Count == 0
                    && string.IsNullOrEmpty(plan.physicalCommitId)
                    && plan.physicalCommitQuantity == 0
                    && plan.physicalCommitInputMassGrams == 0L;
            if (!commitFieldsValid)
            {
                report.AddError(
                    $"Survival treatment plan '{operationId}' has inconsistent physical commit state.");
            }
        }

        string previousCompleted = null;
        HashSet<string> completed = new(StringComparer.Ordinal);
        foreach (string operationId in payload.completedTreatmentOperationIds)
        {
            if (!new ConsumableOperationId(operationId).IsValid
                || !IsCanonicalRequired(operationId)
                || !completed.Add(operationId)
                || active.Contains(operationId)
                || previousCompleted != null
                    && string.CompareOrdinal(previousCompleted, operationId) >= 0)
            {
                report.AddError(
                    "Survival completed treatment operations contain a non-canonical, duplicate, active, or unordered ID.");
                continue;
            }
            previousCompleted = operationId;
        }
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalOptional(string value) =>
        value != null
        && (value.Length == 0 || IsCanonicalRequired(value));

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
