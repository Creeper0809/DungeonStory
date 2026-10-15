using System;
using System.Collections.Generic;
using System.Linq;

// Serializable gameplay evidence for another domain's durable consequence.
// It contains no ledger schema or presentation fields.
[Serializable]
public sealed class CharacterMealConsumedFactData
{
    public string operationId = "", characterId = "", facilityId = "", itemId = "", physicalCommitId = "";
    public int absoluteDay, quantity;
    public long massGrams;
    public string[] sourceStackIds = Array.Empty<string>();
    public float hungerBefore, hungerAfter, healthBefore, healthAfter, moodBefore, moodAfter;
    public bool policyViolation, contaminated, contaminationDamageApplied, recipientActive;
    public string effectsSkippedReason = "";

    public CharacterMealConsumedFactData Clone()
    {
        var copy = (CharacterMealConsumedFactData)MemberwiseClone();
        copy.sourceStackIds = sourceStackIds?.ToArray();
        return copy;
    }
    public static CharacterMealConsumedFactData Capture(CharacterMealConsumedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var evidence = fact.Evidence;
        var effects = evidence.Effects;
        return new CharacterMealConsumedFactData
        {
            operationId = fact.OperationId.Value, characterId = fact.CharacterId.Value, absoluteDay = fact.AbsoluteDay,
            facilityId = evidence.FacilityId.Value ?? "", itemId = evidence.ItemDefinitionId.Value,
            physicalCommitId = evidence.PhysicalCommitId, quantity = evidence.ConsumedQuantity,
            massGrams = evidence.ConsumedMassGrams, sourceStackIds = evidence.SourceStackIds.ToArray(),
            hungerBefore = effects.HungerBefore, hungerAfter = effects.HungerAfter,
            healthBefore = effects.HealthBefore, healthAfter = effects.HealthAfter,
            moodBefore = effects.MoodBefore, moodAfter = effects.MoodAfter,
            policyViolation = evidence.PolicyViolation, contaminated = evidence.Contaminated,
            contaminationDamageApplied = evidence.ContaminationDamageApplied,
            effectsSkippedReason = evidence.EffectsSkippedReason, recipientActive = fact.RecipientActiveAtCompletion
        };
    }
    public CharacterMealConsumedFact ToFact()
    {
        if (sourceStackIds == null || facilityId == null || effectsSkippedReason == null)
            throw new InvalidOperationException("Saved meal evidence has missing fields.");
        return new CharacterMealConsumedFact(new ConsumableOperationId(operationId), new CharacterId(characterId), absoluteDay,
            new CharacterConsumablesOutcomeEvidence(string.IsNullOrEmpty(facilityId) ? default : new BuildingInstanceId(facilityId),
                new ConsumableItemDefinitionId(itemId), sourceStackIds, physicalCommitId, quantity, massGrams,
                new CharacterConsumablesEffectEvidence(hungerBefore, hungerAfter, healthBefore, healthAfter, moodBefore, moodAfter),
                policyViolation, contaminated, contaminationDamageApplied, effectsSkippedReason), recipientActive);
    }
}

/// <summary>
/// Immutable survival-owned evidence that a meal was physically consumed.
/// </summary>
public sealed class CharacterMealConsumedFact
{
    public CharacterMealConsumedFact(
        ConsumableOperationId operationId,
        CharacterId characterId,
        int absoluteDay,
        in CharacterConsumablesOutcomeEvidence evidence,
        bool recipientActiveAtCompletion)
    {
        CharacterConsumptionFactValidation.RequireIdentity(
            operationId,
            characterId,
            absoluteDay);
        if (!evidence.ItemDefinitionId.IsValid
            || evidence.ConsumedQuantity <= 0
            || evidence.ConsumedMassGrams <= 0L
            || string.IsNullOrWhiteSpace(evidence.PhysicalCommitId))
        {
            throw new ArgumentException(
                "Complete meal consumption evidence is required.",
                nameof(evidence));
        }
        CharacterConsumptionFactValidation.RequireFinite(evidence.Effects);

        IReadOnlyList<string> source =
            evidence.SourceStackIds ?? Array.Empty<string>();
        var copiedSourceStackIds = new string[source.Count];
        var distinctSourceStackIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < source.Count; index++)
        {
            string value = source[index];
            copiedSourceStackIds[index] = value;
            if (!string.IsNullOrWhiteSpace(value))
                distinctSourceStackIds.Add(value.Trim());
        }
        int fixedSubjectCount = evidence.FacilityId.IsValid ? 3 : 2;
        if (distinctSourceStackIds.Count == 0
            || distinctSourceStackIds.Count + fixedSubjectCount > 16)
        {
            throw new ArgumentException(
                "Meal source stack evidence is missing or exceeds capacity.",
                nameof(evidence));
        }

        OperationId = operationId;
        CharacterId = characterId;
        AbsoluteDay = absoluteDay;
        Evidence = new CharacterConsumablesOutcomeEvidence(
            evidence.FacilityId,
            evidence.ItemDefinitionId,
            Array.AsReadOnly(copiedSourceStackIds),
            evidence.PhysicalCommitId,
            evidence.ConsumedQuantity,
            evidence.ConsumedMassGrams,
            evidence.Effects,
            evidence.PolicyViolation,
            evidence.Contaminated,
            evidence.ContaminationDamageApplied,
            evidence.EffectsSkippedReason);
        RecipientActiveAtCompletion = recipientActiveAtCompletion;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public int AbsoluteDay { get; }
    public CharacterConsumablesOutcomeEvidence Evidence { get; }
    public bool RecipientActiveAtCompletion { get; }
}

/// <summary>
/// Immutable survival-owned result of a completed substance consumption.
/// </summary>
public sealed class CharacterSubstanceConsumedFact
{
    public CharacterSubstanceConsumedFact(
        ConsumableOperationId operationId,
        CharacterId characterId,
        int absoluteDay,
        ConsumableItemDefinitionId itemId,
        float tolerance,
        float addiction,
        bool overdosed)
    {
        CharacterConsumptionFactValidation.RequireIdentity(
            operationId,
            characterId,
            absoluteDay);
        if (!itemId.IsValid)
            throw new ArgumentException("Substance item ID is invalid.", nameof(itemId));
        if (!CharacterConsumptionFactValidation.IsFinite(tolerance)
            || tolerance < 0f
            || tolerance > 100f)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
        if (!CharacterConsumptionFactValidation.IsFinite(addiction)
            || addiction < 0f
            || addiction > 100f)
        {
            throw new ArgumentOutOfRangeException(nameof(addiction));
        }

        OperationId = operationId;
        CharacterId = characterId;
        AbsoluteDay = absoluteDay;
        ItemId = itemId;
        Tolerance = tolerance;
        Addiction = addiction;
        Overdosed = overdosed;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public int AbsoluteDay { get; }
    public ConsumableItemDefinitionId ItemId { get; }
    public float Tolerance { get; }
    public float Addiction { get; }
    public bool Overdosed { get; }
}

/// <summary>
/// Immutable survival-owned result of an applied detox treatment.
/// </summary>
public sealed class CharacterDetoxAppliedFact
{
    public CharacterDetoxAppliedFact(
        ConsumableOperationId operationId,
        CharacterId characterId,
        int absoluteDay,
        ConsumableItemDefinitionId itemId,
        float appliedReduction)
    {
        CharacterConsumptionFactValidation.RequireIdentity(
            operationId,
            characterId,
            absoluteDay);
        if (!itemId.IsValid)
            throw new ArgumentException("Detox item ID is invalid.", nameof(itemId));
        if (!CharacterConsumptionFactValidation.IsFinite(appliedReduction)
            || appliedReduction < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(appliedReduction));
        }

        OperationId = operationId;
        CharacterId = characterId;
        AbsoluteDay = absoluteDay;
        ItemId = itemId;
        AppliedReduction = appliedReduction;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public int AbsoluteDay { get; }
    public ConsumableItemDefinitionId ItemId { get; }
    public float AppliedReduction { get; }
}

/// <summary>
/// Immutable survival-owned completion of the primitive field-meal action.
/// </summary>
public sealed class CharacterPrimitiveMealCompletedFact
{
    public CharacterPrimitiveMealCompletedFact(
        ConsumableOperationId operationId,
        CharacterId characterId,
        int absoluteDay,
        float nutrition)
    {
        CharacterConsumptionFactValidation.RequireIdentity(
            operationId,
            characterId,
            absoluteDay);
        if (!CharacterConsumptionFactValidation.IsFinite(nutrition)
            || nutrition < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(nutrition));
        }

        OperationId = operationId;
        CharacterId = characterId;
        AbsoluteDay = absoluteDay;
        Nutrition = nutrition;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId CharacterId { get; }
    public int AbsoluteDay { get; }
    public float Nutrition { get; }
}

internal static class CharacterConsumptionFactValidation
{
    internal static void RequireIdentity(
        ConsumableOperationId operationId,
        CharacterId characterId,
        int absoluteDay)
    {
        if (!operationId.IsValid)
            throw new ArgumentException("Consumable operation ID is invalid.", nameof(operationId));
        if (!characterId.IsValid)
            throw new ArgumentException("Character ID is invalid.", nameof(characterId));
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
    }

    internal static void RequireFinite(
        in CharacterConsumablesEffectEvidence effects)
    {
        if (!IsFinite(effects.HungerBefore)
            || !IsFinite(effects.HungerAfter)
            || !IsFinite(effects.HealthBefore)
            || !IsFinite(effects.HealthAfter)
            || !IsFinite(effects.MoodBefore)
            || !IsFinite(effects.MoodAfter))
        {
            throw new ArgumentException(
                "Meal effect evidence must be finite.",
                nameof(effects));
        }
    }

    internal static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
