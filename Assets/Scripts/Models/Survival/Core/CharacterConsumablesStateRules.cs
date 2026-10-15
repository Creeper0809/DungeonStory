using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal readonly struct CharacterSubstanceKey : IEquatable<CharacterSubstanceKey>
{
    internal CharacterSubstanceKey(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId)
    {
        CharacterId = characterId;
        ItemId = itemId;
    }

    internal CharacterId CharacterId { get; }
    internal ConsumableItemDefinitionId ItemId { get; }
    public bool Equals(CharacterSubstanceKey other) =>
        CharacterId.Equals(other.CharacterId) && ItemId.Equals(other.ItemId);
    public override bool Equals(object obj) =>
        obj is CharacterSubstanceKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(CharacterId, ItemId);
}

internal readonly struct MealDeliveryRoute : IEquatable<MealDeliveryRoute>
{
    internal MealDeliveryRoute(
        CharacterId characterId,
        BuildingInstanceId buildingId,
        ConsumableItemDefinitionId itemId)
    {
        CharacterId = characterId;
        BuildingId = buildingId;
        ItemId = itemId;
    }

    internal CharacterId CharacterId { get; }
    internal BuildingInstanceId BuildingId { get; }
    internal ConsumableItemDefinitionId ItemId { get; }
    public bool Equals(MealDeliveryRoute other) =>
        CharacterId.Equals(other.CharacterId)
        && BuildingId.Equals(other.BuildingId)
        && ItemId.Equals(other.ItemId);
    public override bool Equals(object obj) =>
        obj is MealDeliveryRoute other && Equals(other);
    public override int GetHashCode() =>
        HashCode.Combine(CharacterId, BuildingId, ItemId);
}

internal sealed class CharacterConsumablesAggregateState
{
    internal readonly Dictionary<CharacterId, CharacterDietPolicyState> DietPolicies = new();
    internal readonly Dictionary<CharacterId, CharacterMealQualityPolicyState>
        MealQualityPolicies = new();
    internal readonly Dictionary<CharacterSubstanceKey, CharacterSubstancePolicyState>
        SubstancePolicies = new();
    internal readonly Dictionary<CharacterSubstanceKey, CharacterSubstanceState>
        SubstanceStates = new();
    internal readonly Dictionary<CharacterId, CharacterToxicityState>
        ToxicityStates = new();
    internal readonly Dictionary<ConsumableDeliveryId, CharacterMealDeliveryState>
        PendingDeliveries = new();
    internal readonly Dictionary<MealDeliveryRoute, ConsumableDeliveryId>
        DeliveryByRoute = new();
    internal readonly Dictionary<ConsumableOperationId, CharacterConsumableOperationState>
        CompletedOperations = new();
    internal readonly Dictionary<ConsumableOperationId, CharacterMealOperationFailureState>
        MealFailures = new();
    internal readonly Dictionary<CharacterId, float> MealFollowupCooldownUntil = new();
    internal readonly Dictionary<ConsumableOperationId, CharacterMealPlan>
        ActiveMealPlans = new();
    internal readonly Dictionary<ConsumableOperationId, CharacterSubstanceUsePlan>
        ActiveSubstanceUsePlans = new();
    internal readonly Dictionary<ConsumableOperationId, CharacterDetoxTreatmentPlan>
        ActiveDetoxTreatmentPlans = new();
    internal long NextOperationSequence = 1;
    internal long NextDeliverySequence = 1;
    internal long PolicyRevision;
    internal CharacterConsumablesPolicyChangedFact PendingPolicyFact;
    internal float NextDeliveryPruneAt;
    internal float TimelineTime;
    internal bool ClockNeedsBinding;
    private float runtimeClockAnchor;
    private float timelineClockAnchor;
    private bool clockBound;

    // Kept on the aggregate root: a failed restore rolls back the epoch with
    // its data, without rewriting operation timestamps or extending deadlines.
    internal void BindRuntimeClock(float runtimeNow)
    {
        RequireClock(runtimeNow);
        if (!ClockNeedsBinding || clockBound)
            throw new InvalidOperationException("Consumables clock can only bind once on a restored root.");
        runtimeClockAnchor = runtimeNow;
        timelineClockAnchor = TimelineTime;
        clockBound = true;
        ClockNeedsBinding = false;
    }

    internal float GetTimelineTime(float runtimeNow)
    {
        RequireClock(runtimeNow);
        if (ClockNeedsBinding)
            throw new InvalidOperationException("Restored consumables clock has not been bound.");
        if (clockBound && runtimeNow < runtimeClockAnchor)
            throw new InvalidOperationException("Consumables source clock moved before its restore anchor.");
        float now = clockBound
            ? (float)((double)timelineClockAnchor + ((double)runtimeNow - runtimeClockAnchor))
            : runtimeNow;
        RequireClock(now);
        if (now < TimelineTime)
            throw new InvalidOperationException("Consumables clock moved before its captured timeline.");
        return now;
    }

    private static void RequireClock(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            throw new InvalidOperationException("Consumables clock must be finite and nonnegative.");
    }

    internal CharacterConsumablesAggregateState Clone()
    {
        CharacterConsumablesAggregateState clone = new()
        {
            NextOperationSequence = NextOperationSequence,
            NextDeliverySequence = NextDeliverySequence,
            PolicyRevision = PolicyRevision,
            PendingPolicyFact = PendingPolicyFact,
            NextDeliveryPruneAt = NextDeliveryPruneAt,
            TimelineTime = TimelineTime,
            ClockNeedsBinding = ClockNeedsBinding,
            runtimeClockAnchor = runtimeClockAnchor,
            timelineClockAnchor = timelineClockAnchor,
            clockBound = clockBound
        };
        foreach (KeyValuePair<CharacterId, CharacterDietPolicyState> pair in DietPolicies)
        {
            clone.DietPolicies.Add(pair.Key, CharacterConsumablesStateRules.Clone(pair.Value));
        }
        foreach (KeyValuePair<CharacterId, CharacterMealQualityPolicyState> pair in
                 MealQualityPolicies)
        {
            clone.MealQualityPolicies.Add(
                pair.Key,
                CharacterConsumablesStateRules.Clone(pair.Value));
        }
        foreach (KeyValuePair<CharacterSubstanceKey, CharacterSubstancePolicyState> pair in SubstancePolicies)
        {
            clone.SubstancePolicies.Add(pair.Key, CharacterConsumablesStateRules.Clone(pair.Value));
        }
        foreach (KeyValuePair<CharacterSubstanceKey, CharacterSubstanceState> pair in SubstanceStates)
        {
            clone.SubstanceStates.Add(pair.Key, CharacterConsumablesStateRules.Clone(pair.Value));
        }
        foreach (KeyValuePair<CharacterId, CharacterToxicityState> pair in ToxicityStates)
        {
            clone.ToxicityStates.Add(
                pair.Key,
                CharacterConsumablesStateRules.Clone(pair.Value));
        }
        foreach (KeyValuePair<ConsumableDeliveryId, CharacterMealDeliveryState> pair in PendingDeliveries)
        {
            CharacterMealDeliveryState delivery = CharacterConsumablesStateRules.Clone(pair.Value);
            clone.PendingDeliveries.Add(pair.Key, delivery);
            clone.DeliveryByRoute.Add(CharacterConsumablesStateRules.Route(delivery), pair.Key);
        }
        foreach (KeyValuePair<ConsumableOperationId, CharacterConsumableOperationState> pair in CompletedOperations)
        {
            clone.CompletedOperations.Add(pair.Key, CharacterConsumablesStateRules.Clone(pair.Value));
        }
        foreach (var pair in MealFailures)
            clone.MealFailures.Add(pair.Key, CharacterConsumablesStateRules.Clone(pair.Value));
        foreach (KeyValuePair<CharacterId, float> pair in MealFollowupCooldownUntil)
            clone.MealFollowupCooldownUntil.Add(pair.Key, pair.Value);
        foreach (KeyValuePair<ConsumableOperationId, CharacterMealPlan> pair in
                 ActiveMealPlans)
        {
            clone.ActiveMealPlans.Add(
                pair.Key,
                CharacterConsumablesStateRules.Clone(pair.Value));
        }
        foreach (KeyValuePair<ConsumableOperationId, CharacterSubstanceUsePlan> pair in
                 ActiveSubstanceUsePlans)
        {
            clone.ActiveSubstanceUsePlans.Add(
                pair.Key,
                CharacterConsumablesStateRules.Clone(pair.Value));
        }
        foreach (KeyValuePair<ConsumableOperationId, CharacterDetoxTreatmentPlan> pair in
                 ActiveDetoxTreatmentPlans)
        {
            clone.ActiveDetoxTreatmentPlans.Add(
                pair.Key,
                CharacterConsumablesStateRules.Clone(pair.Value));
        }
        return clone;
    }
}

internal static class CharacterConsumablesStateRules
{
    internal static CharacterMealOperationFailureState Clone(CharacterMealOperationFailureState source) => new()
    {
        operationId = source.operationId, characterId = source.characterId,
        facilityInstanceId = source.facilityInstanceId, failureCode = source.failureCode,
        detail = source.detail, recoveryPending = source.recoveryPending
    };
    internal static CharacterDetoxTreatmentPlan Clone(
        CharacterDetoxTreatmentPlan source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterDetoxTreatmentPlanPhase.ItemCommitted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        detoxReduction = source?.detoxReduction ?? 0f,
        appliedReduction = source?.appliedReduction ?? 0f,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L
    };

    internal static CharacterDetoxTreatmentPlanSaveData ToSaveData(
        CharacterDetoxTreatmentPlan source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterDetoxTreatmentPlanPhase.ItemCommitted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        detoxReduction = source?.detoxReduction ?? 0f,
        appliedReduction = source?.appliedReduction ?? 0f,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L
    };

    internal static CharacterDetoxTreatmentPlan FromSaveData(
        CharacterDetoxTreatmentPlanSaveData source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterDetoxTreatmentPlanPhase.ItemCommitted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        detoxReduction = source?.detoxReduction ?? 0f,
        appliedReduction = source?.appliedReduction ?? 0f,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L
    };

    internal static CharacterSubstanceUsePlan Clone(
        CharacterSubstanceUsePlan source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterSubstanceUsePlanPhase.ItemCommitted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        automaticOperation = source?.automaticOperation ?? false,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L,
        resolvedTolerance = source?.resolvedTolerance ?? 0f,
        resolvedAddiction = source?.resolvedAddiction ?? 0f,
        resolvedWithdrawal = source?.resolvedWithdrawal ?? 0f,
        resolvedActiveSeconds = source?.resolvedActiveSeconds ?? 0f,
        resolvedSecondsSinceLastDose = source?.resolvedSecondsSinceLastDose ?? 0f,
        resolvedScheduledCooldownSeconds =
            source?.resolvedScheduledCooldownSeconds ?? 0f,
        effectToleranceRatio = source?.effectToleranceRatio ?? 0f,
        resolvedAddicted = source?.resolvedAddicted ?? false,
        resolvedOverdosed = source?.resolvedOverdosed ?? false,
        becameAddicted = source?.becameAddicted ?? false,
        resolvedEffectMood = source?.resolvedEffectMood ?? 0f,
        resolvedOverdoseDamage = source?.resolvedOverdoseDamage ?? 0f
    };

    internal static CharacterSubstanceUsePlanSaveData ToSaveData(
        CharacterSubstanceUsePlan source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterSubstanceUsePlanPhase.ItemCommitted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        automaticOperation = source?.automaticOperation ?? false,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L,
        resolvedTolerance = source?.resolvedTolerance ?? 0f,
        resolvedAddiction = source?.resolvedAddiction ?? 0f,
        resolvedWithdrawal = source?.resolvedWithdrawal ?? 0f,
        resolvedActiveSeconds = source?.resolvedActiveSeconds ?? 0f,
        resolvedSecondsSinceLastDose = source?.resolvedSecondsSinceLastDose ?? 0f,
        resolvedScheduledCooldownSeconds =
            source?.resolvedScheduledCooldownSeconds ?? 0f,
        effectToleranceRatio = source?.effectToleranceRatio ?? 0f,
        resolvedAddicted = source?.resolvedAddicted ?? false,
        resolvedOverdosed = source?.resolvedOverdosed ?? false,
        becameAddicted = source?.becameAddicted ?? false,
        resolvedEffectMood = source?.resolvedEffectMood ?? 0f,
        resolvedOverdoseDamage = source?.resolvedOverdoseDamage ?? 0f
    };

    internal static CharacterSubstanceUsePlan FromSaveData(
        CharacterSubstanceUsePlanSaveData source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterSubstanceUsePlanPhase.ItemCommitted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        automaticOperation = source?.automaticOperation ?? false,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L,
        resolvedTolerance = source?.resolvedTolerance ?? 0f,
        resolvedAddiction = source?.resolvedAddiction ?? 0f,
        resolvedWithdrawal = source?.resolvedWithdrawal ?? 0f,
        resolvedActiveSeconds = source?.resolvedActiveSeconds ?? 0f,
        resolvedSecondsSinceLastDose = source?.resolvedSecondsSinceLastDose ?? 0f,
        resolvedScheduledCooldownSeconds =
            source?.resolvedScheduledCooldownSeconds ?? 0f,
        effectToleranceRatio = source?.effectToleranceRatio ?? 0f,
        resolvedAddicted = source?.resolvedAddicted ?? false,
        resolvedOverdosed = source?.resolvedOverdosed ?? false,
        becameAddicted = source?.becameAddicted ?? false,
        resolvedEffectMood = source?.resolvedEffectMood ?? 0f,
        resolvedOverdoseDamage = source?.resolvedOverdoseDamage ?? 0f
    };

    internal static CharacterMealPlan Clone(CharacterMealPlan source) => new()
    {
        ritualFastingConsequenceDelivered = source?.ritualFastingConsequenceDelivered ?? false,
        physicalContextFrozen = source?.physicalContextFrozen ?? false,
        consumptionPosition = source?.consumptionPosition ?? default,
        effectsSkippedReason = source?.effectsSkippedReason ?? string.Empty,
        planId = source?.planId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        transportStackId = source?.transportStackId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        mealQuantityLeaseId = source?.mealQuantityLeaseId ?? string.Empty,
        phase = source?.phase ?? CharacterMealPlanPhase.Aborted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        createdAt = source?.createdAt ?? 0d,
        leaseExpiresAt = source?.leaseExpiresAt ?? 0d,
        expectedCompletionEta = source?.expectedCompletionEta ?? 0f,
        physicalConsumptionCommitted = source?.physicalConsumptionCommitted ?? false,
        automaticOperation = source?.automaticOperation ?? false,
        beginContamination = source?.beginContamination ?? 0f,
        facilitySlotReserved = source?.facilitySlotReserved ?? false,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L,
        committedPolicyViolation = source?.committedPolicyViolation ?? false,
        committedContaminated = source?.committedContaminated ?? false,
        contaminationRollFrozen = source?.contaminationRollFrozen ?? false,
        resolvedContaminationDamage =
            source?.resolvedContaminationDamage ?? false,
        primitiveSurvivalOutcomeRequired =
            source?.primitiveSurvivalOutcomeRequired ?? false,
        primitivePublicationKey = source?.primitivePublicationKey ?? string.Empty,
        primitiveNutrition = source?.primitiveNutrition ?? 0f,
        recipientActiveAtCompletion = source?.recipientActiveAtCompletion ?? false,
        postCommitEventDelivered = source?.postCommitEventDelivered ?? false,
        effectEvidenceFrozen = source?.effectEvidenceFrozen ?? false,
        hungerBefore = source?.hungerBefore ?? 0f,
        hungerAfter = source?.hungerAfter ?? 0f,
        healthBefore = source?.healthBefore ?? 0f,
        healthAfter = source?.healthAfter ?? 0f,
        moodBefore = source?.moodBefore ?? 0f,
        moodAfter = source?.moodAfter ?? 0f
    };

    internal static CharacterMealPlanSaveData ToSaveData(
        CharacterMealPlan source) => new()
    {
        ritualFastingConsequenceDelivered = source?.ritualFastingConsequenceDelivered ?? false,
        physicalContextFrozen = source?.physicalContextFrozen ?? false,
        consumptionPosition = source?.consumptionPosition ?? default,
        effectsSkippedReason = source?.effectsSkippedReason ?? string.Empty,
        planId = source?.planId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        phase = source?.phase ?? CharacterMealPlanPhase.Aborted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        createdAt = source?.createdAt ?? 0d,
        leaseExpiresAt = source?.leaseExpiresAt ?? 0d,
        expectedCompletionEta = source?.expectedCompletionEta ?? 0f,
        automaticOperation = source?.automaticOperation ?? false,
        beginContamination = source?.beginContamination ?? 0f,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L,
        committedPolicyViolation = source?.committedPolicyViolation ?? false,
        committedContaminated = source?.committedContaminated ?? false,
        contaminationRollFrozen = source?.contaminationRollFrozen ?? false,
        resolvedContaminationDamage =
            source?.resolvedContaminationDamage ?? false,
        primitiveSurvivalOutcomeRequired =
            source?.primitiveSurvivalOutcomeRequired ?? false,
        primitivePublicationKey = source?.primitivePublicationKey ?? string.Empty,
        primitiveNutrition = source?.primitiveNutrition ?? 0f,
        recipientActiveAtCompletion = source?.recipientActiveAtCompletion ?? false,
        postCommitEventDelivered = source?.postCommitEventDelivered ?? false,
        effectEvidenceFrozen = source?.effectEvidenceFrozen ?? false,
        hungerBefore = source?.hungerBefore ?? 0f,
        hungerAfter = source?.hungerAfter ?? 0f,
        healthBefore = source?.healthBefore ?? 0f,
        healthAfter = source?.healthAfter ?? 0f,
        moodBefore = source?.moodBefore ?? 0f,
        moodAfter = source?.moodAfter ?? 0f
    };

    internal static CharacterMealPlan FromSaveData(
        CharacterMealPlanSaveData source) => new()
    {
        ritualFastingConsequenceDelivered = source?.ritualFastingConsequenceDelivered ?? false,
        physicalContextFrozen = source?.physicalContextFrozen ?? false,
        consumptionPosition = source?.consumptionPosition ?? default,
        effectsSkippedReason = source?.effectsSkippedReason ?? string.Empty,
        planId = source?.planId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        mealQuantityLeaseId = string.Empty,
        phase = source?.phase ?? CharacterMealPlanPhase.Aborted,
        factAbsoluteDay = source?.factAbsoluteDay ?? 0,
        publicationKey = source?.publicationKey ?? string.Empty,
        publicationPending = source?.publicationPending ?? false,
        createdAt = source?.createdAt ?? 0d,
        leaseExpiresAt = source?.leaseExpiresAt ?? 0d,
        expectedCompletionEta = source?.expectedCompletionEta ?? 0f,
        automaticOperation = source?.automaticOperation ?? false,
        beginContamination = source?.beginContamination ?? 0f,
        facilitySlotReserved = false,
        physicalConsumptionCommitted = source?.phase is
            CharacterMealPlanPhase.ItemCommitted
            or CharacterMealPlanPhase.EffectsApplied
            or CharacterMealPlanPhase.EffectsPublished,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L,
        committedPolicyViolation = source?.committedPolicyViolation ?? false,
        committedContaminated = source?.committedContaminated ?? false,
        contaminationRollFrozen = source?.contaminationRollFrozen ?? false,
        resolvedContaminationDamage =
            source?.resolvedContaminationDamage ?? false,
        primitiveSurvivalOutcomeRequired =
            source?.primitiveSurvivalOutcomeRequired ?? false,
        primitivePublicationKey = source?.primitivePublicationKey ?? string.Empty,
        primitiveNutrition = source?.primitiveNutrition ?? 0f,
        recipientActiveAtCompletion = source?.recipientActiveAtCompletion ?? false,
        postCommitEventDelivered = source?.postCommitEventDelivered ?? false,
        effectEvidenceFrozen = source?.effectEvidenceFrozen ?? false,
        hungerBefore = source?.hungerBefore ?? 0f,
        hungerAfter = source?.hungerAfter ?? 0f,
        healthBefore = source?.healthBefore ?? 0f,
        healthAfter = source?.healthAfter ?? 0f,
        moodBefore = source?.moodBefore ?? 0f,
        moodAfter = source?.moodAfter ?? 0f
    };

    internal static CharacterDietPolicyState Clone(CharacterDietPolicyState source) =>
        new()
        {
            characterId = source?.characterId ?? string.Empty,
            policy = source?.policy ?? CharacterDietPolicyKind.Free
        };

    internal static CharacterMealQualityPolicyState Clone(
        CharacterMealQualityPolicyState source) => new()
        {
            characterId = source?.characterId ?? string.Empty,
            maximumQuality = source?.maximumQuality
                ?? CharacterMealQualityLimit.Inherit
        };

    internal static CharacterSubstancePolicyState Clone(
        CharacterSubstancePolicyState source) =>
        new()
        {
            characterId = source?.characterId ?? string.Empty,
            itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
            mode = source?.mode ?? SubstancePolicyMode.Forbidden,
            moodThreshold = Mathf.Clamp(source?.moodThreshold ?? 30f, 0f, 100f),
            scheduledHour = Mathf.Clamp(source?.scheduledHour ?? 20, 0, 23)
        };

    internal static CharacterSubstanceState Clone(CharacterSubstanceState source) =>
        new()
        {
            characterId = source?.characterId ?? string.Empty,
            itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
            tolerance = Mathf.Clamp(source?.tolerance ?? 0f, 0f, 100f),
            addiction = Mathf.Clamp(source?.addiction ?? 0f, 0f, 100f),
            withdrawal = Mathf.Clamp(source?.withdrawal ?? 0f, 0f, 100f),
            activeSeconds = Mathf.Max(0f, source?.activeSeconds ?? 0f),
            secondsSinceLastDose = Mathf.Max(0f, source?.secondsSinceLastDose ?? 0f),
            scheduledCooldownSeconds = Mathf.Max(
                0f,
                source?.scheduledCooldownSeconds ?? 0f),
            addicted = source?.addicted ?? false,
            overdosed = source?.overdosed ?? false
        };

    internal static CharacterToxicityState Clone(CharacterToxicityState source) =>
        new()
        {
            characterId = source?.characterId ?? string.Empty,
            toxicity = CharacterToxicityPolicy.Clamp(source?.toxicity ?? 0f),
            lastNaturalRecoveryDay = source?.lastNaturalRecoveryDay ?? 0
        };

    internal static CharacterMealDeliveryState Clone(CharacterMealDeliveryState source) =>
        new()
        {
            deliveryId = source?.deliveryId ?? string.Empty,
            characterId = source?.characterId ?? string.Empty,
            buildingInstanceId = source?.buildingInstanceId ?? string.Empty,
            itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
            requestedAt = source?.requestedAt ?? 0f,
            retryAfter = source?.retryAfter ?? 0f
        };

    internal static CharacterConsumableOperationState Clone(
        CharacterConsumableOperationState source) =>
        new()
        {
            mealEffectsSkippedReason = source?.mealEffectsSkippedReason ?? string.Empty,
            operationId = source?.operationId ?? string.Empty,
            characterId = source?.characterId ?? string.Empty,
            itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
            itemStackId = source?.itemStackId ?? string.Empty,
            facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
            meal = source?.meal ?? false,
            detox = source?.detox ?? false,
            policyViolation = source?.policyViolation ?? false,
            contaminated = source?.contaminated ?? false,
            completedAt = source?.completedAt ?? 0f,
            appliedEffect = source?.appliedEffect ?? 0f,
            outcomeResultKey = source?.outcomeResultKey ?? string.Empty
        };

    internal static MealDeliveryRoute Route(CharacterMealDeliveryState delivery) =>
        new(delivery.CharacterId, delivery.BuildingInstanceId, delivery.ItemDefinitionId);

    internal static DungeonCharacterConsumablesSaveData Capture(
        CharacterConsumablesAggregateState state,
        float timelineTime)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        DungeonCharacterConsumablesSaveData payload = new()
        {
            version = DungeonCharacterConsumablesSaveData.CurrentVersion,
            timelineTime = timelineTime,
            nextOperationSequence = state.NextOperationSequence,
            nextDeliverySequence = state.NextDeliverySequence,
            policyRevision = state.PolicyRevision,
            pendingPolicyFacts = state.PendingPolicyFact == null
                ? new() : new() { state.PendingPolicyFact.Capture() },
            dietPolicies = state.DietPolicies.Values.Select(Clone)
                .OrderBy(value => value.characterId, StringComparer.Ordinal).ToList(),
            mealQualityPolicies = state.MealQualityPolicies.Values.Select(Clone)
                .OrderBy(value => value.characterId, StringComparer.Ordinal).ToList(),
            substancePolicies = state.SubstancePolicies.Values.Select(Clone)
                .OrderBy(value => value.characterId, StringComparer.Ordinal)
                .ThenBy(value => value.itemDefinitionId, StringComparer.Ordinal).ToList(),
            substanceStates = state.SubstanceStates.Values.Select(Clone)
                .OrderBy(value => value.characterId, StringComparer.Ordinal)
                .ThenBy(value => value.itemDefinitionId, StringComparer.Ordinal).ToList(),
            toxicityStates = state.ToxicityStates.Values.Select(Clone)
                .OrderBy(value => value.characterId, StringComparer.Ordinal).ToList(),
            pendingMealDeliveries = state.PendingDeliveries.Values.Select(Clone)
                .OrderBy(value => value.deliveryId, StringComparer.Ordinal).ToList(),
            completedOperations = state.CompletedOperations.Values.Select(Clone)
                .OrderBy(value => value.operationId, StringComparer.Ordinal).ToList(),
            failedMealOperations = state.MealFailures.Values.Select(Clone)
                .OrderBy(value => value.operationId, StringComparer.Ordinal).ToList(),
            activeMealPlans = state.ActiveMealPlans.Values
                .Select(ToSaveData)
                .OrderBy(value => value.planId, StringComparer.Ordinal)
                .ToList(),
            activeSubstanceUsePlans = state.ActiveSubstanceUsePlans.Values
                .Select(ToSaveData)
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .ToList(),
            activeDetoxTreatmentPlans = state.ActiveDetoxTreatmentPlans.Values
                .Select(ToSaveData)
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .ToList(),
            mealFollowupCooldowns = state.MealFollowupCooldownUntil
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(pair => new CharacterMealFollowupCooldownSaveData
                {
                    characterId = pair.Key.Value,
                    untilGameSeconds = pair.Value
                })
                .ToList()
        };
        DungeonGameRestoreReport report = new();
        ValidateSequenceWatermarks(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character consumables capture rejected invalid ID sequences: "
                + string.Join(" | ", report.Errors));
        }
        return payload;
    }

    internal static void Validate(
        DungeonCharacterConsumablesSaveData payload,
        DungeonGameRestoreReport report,
        ICharacterConsumablesWorldPort world,
        ICharacterConsumablesInventoryPort inventory,
        bool requireWorldReferences = true)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (payload == null)
        {
            report.AddError("Character consumables payload is null.");
            return;
        }
        if (payload.version != DungeonCharacterConsumablesSaveData.CurrentVersion)
        {
            report.AddError($"Character consumables payload V{payload.version} is unsupported; expected V{DungeonCharacterConsumablesSaveData.CurrentVersion}.");
        }
        if (!IsFiniteNonNegative(payload.timelineTime))
            report.AddError("Character consumables timeline must be finite and nonnegative.");
        if (payload.nextOperationSequence < 1 || payload.nextDeliverySequence < 1)
        {
            report.AddError("Character consumables sequences must be positive.");
        }
        if (payload.dietPolicies == null || payload.substancePolicies == null
            || payload.pendingPolicyFacts == null
            || payload.substanceStates == null || payload.pendingMealDeliveries == null
            || payload.toxicityStates == null
            || payload.completedOperations == null
            || payload.failedMealOperations == null
            || payload.mealFollowupCooldowns == null
            || payload.mealQualityPolicies == null
            || payload.activeMealPlans == null
            || payload.activeSubstanceUsePlans == null
            || payload.activeDetoxTreatmentPlans == null)
        {
            report.AddError("Character consumables payload contains a null collection.");
            return;
        }

        ValidateSequenceWatermarks(payload, report);

        HashSet<CharacterId> characters = world.CharacterIds.Where(id => id.IsValid).ToHashSet();
        HashSet<BuildingInstanceId> facilities = world.FacilityIds.Where(id => id.IsValid).ToHashSet();
        HashSet<CharacterId> dietIds = new();
        HashSet<CharacterId> mealQualityIds = new();
        HashSet<CharacterSubstanceKey> policyKeys = new();
        HashSet<CharacterSubstanceKey> stateKeys = new();
        HashSet<CharacterId> toxicityIds = new();
        HashSet<ConsumableDeliveryId> deliveryIds = new();
        HashSet<MealDeliveryRoute> deliveryRoutes = new();
        HashSet<ConsumableOperationId> completedOperationIds = new();
        HashSet<ConsumableOperationId> activePlanIds = new();
        HashSet<ConsumableOperationId> activeSubstancePlanIds = new();
        HashSet<ConsumableOperationId> activeDetoxPlanIds = new();
        HashSet<CharacterId> cooldownIds = new();

        string previous = null;
        foreach (CharacterDietPolicyState state in payload.dietPolicies)
        {
            if (state == null || !IsExactCharacterId(state.characterId, state.CharacterId)
                || requireWorldReferences && !characters.Contains(state.CharacterId)
                || !dietIds.Add(state.CharacterId)
                || !Enum.IsDefined(typeof(CharacterDietPolicyKind), state.policy)
                || !IsAfter(previous, state.characterId))
            {
                report.AddError("Character consumables diet policies contain an invalid, unknown, duplicate, or unordered CharacterId.");
                break;
            }
            previous = state.characterId;
        }

        previous = null;
        foreach (CharacterMealQualityPolicyState state in payload.mealQualityPolicies)
        {
            if (state == null
                || !IsExactCharacterId(state.characterId, state.CharacterId)
                || requireWorldReferences && !characters.Contains(state.CharacterId)
                || !mealQualityIds.Add(state.CharacterId)
                || !Enum.IsDefined(typeof(CharacterMealQualityLimit), state.maximumQuality)
                || !IsAfter(previous, state.characterId))
            {
                report.AddError(
                    "Character consumables meal-quality policies contain invalid, duplicate, or unordered state.");
                break;
            }
            previous = state.characterId;
        }

        previous = null;
        foreach (CharacterSubstancePolicyState state in payload.substancePolicies)
        {
            CharacterSubstanceKey key = state == null ? default : new(state.CharacterId, state.ItemDefinitionId);
            string orderKey = state == null ? string.Empty : state.characterId + "\n" + state.itemDefinitionId;
            if (state == null || !IsExactCharacterId(state.characterId, state.CharacterId)
                || !IsExactValue(state.itemDefinitionId, state.ItemDefinitionId.Value)
                || !ValidSubstance(
                    state.CharacterId,
                    state.ItemDefinitionId,
                    characters,
                    inventory,
                    requireWorldReferences)
                || !policyKeys.Add(key) || !Enum.IsDefined(typeof(SubstancePolicyMode), state.mode)
                || !InRange(state.moodThreshold, 0f, 100f) || state.scheduledHour < 0
                || state.scheduledHour > 23 || !IsAfter(previous, orderKey))
            {
                report.AddError("Character consumables substance policies contain an invalid, unknown, duplicate, or unordered reference.");
                break;
            }
            previous = orderKey;
        }

        previous = null;
        foreach (CharacterSubstanceState state in payload.substanceStates)
        {
            CharacterSubstanceKey key = state == null ? default : new(state.CharacterId, state.ItemDefinitionId);
            string orderKey = state == null ? string.Empty : state.characterId + "\n" + state.itemDefinitionId;
            if (state == null || !IsExactCharacterId(state.characterId, state.CharacterId)
                || !IsExactValue(state.itemDefinitionId, state.ItemDefinitionId.Value)
                || !ValidSubstance(
                    state.CharacterId,
                    state.ItemDefinitionId,
                    characters,
                    inventory,
                    requireWorldReferences)
                || !stateKeys.Add(key) || !InRange(state.tolerance, 0f, 100f)
                || !InRange(state.addiction, 0f, 100f) || !InRange(state.withdrawal, 0f, 100f)
                || !IsFiniteNonNegative(state.activeSeconds)
                || !IsFiniteNonNegative(state.secondsSinceLastDose)
                || !IsFiniteNonNegative(state.scheduledCooldownSeconds)
                || !IsAfter(previous, orderKey))
            {
                report.AddError("Character consumables substance states contain invalid, unknown, duplicate, unordered, or non-finite state.");
                break;
            }
            previous = orderKey;
        }
        if (payload.policyRevision < 0L)
        {
            report.AddError("Character consumables policy revision cannot be negative.");
        }
        ValidatePendingPolicyFact(payload, report);

        previous = null;
        foreach (CharacterToxicityState state in payload.toxicityStates)
        {
            if (state == null
                || !IsExactCharacterId(state.characterId, state.CharacterId)
                || requireWorldReferences && !characters.Contains(state.CharacterId)
                || !toxicityIds.Add(state.CharacterId)
                || !IsFiniteRange(
                    state.toxicity,
                    0f,
                    CharacterToxicityPolicy.MaximumToxicity)
                || state.lastNaturalRecoveryDay < 0
                || !IsAfter(previous, state.characterId))
            {
                report.AddError(
                    "Character consumables toxicity states contain invalid, duplicate, unordered, or non-finite state.");
                break;
            }
            previous = state.characterId;
        }

        previous = null;
        foreach (CharacterMealDeliveryState delivery in payload.pendingMealDeliveries)
        {
            MealDeliveryRoute route = delivery == null ? default : Route(delivery);
            bool deliveryIdValid = delivery?.DeliveryId.IsValid == true;
            bool deliveryIdExact = delivery != null
                && IsExactValue(delivery.deliveryId, delivery.DeliveryId.Value);
            bool characterIdExact = delivery != null
                && IsExactCharacterId(delivery.characterId, delivery.CharacterId);
            bool buildingIdValid = delivery?.BuildingInstanceId.IsValid == true;
            bool buildingIdExact = delivery != null
                && IsExactValue(
                    delivery.buildingInstanceId,
                    delivery.BuildingInstanceId.Value);
            bool itemIdValid = delivery?.ItemDefinitionId.IsValid == true;
            bool itemIdExact = delivery != null
                && IsExactValue(
                    delivery.itemDefinitionId,
                    delivery.ItemDefinitionId.Value);
            bool characterKnown = delivery != null
                && (!requireWorldReferences
                    || characters.Contains(delivery.CharacterId));
            bool facilityKnown = delivery != null
                && (!requireWorldReferences
                    || facilities.Contains(delivery.BuildingInstanceId));
            bool mealKnown = delivery != null
                && inventory.TryGetMeal(delivery.ItemDefinitionId, out _);
            bool uniqueId = delivery != null
                && deliveryIds.Add(delivery.DeliveryId);
            bool uniqueRoute = delivery != null && deliveryRoutes.Add(route);
            bool requestedAtValid = delivery != null
                && IsFiniteNonNegative(delivery.requestedAt)
                && delivery.requestedAt <= payload.timelineTime;
            bool retryAfterValid = delivery != null
                && IsFiniteNonNegative(delivery.retryAfter)
                && delivery.retryAfter >= delivery.requestedAt;
            bool ordered = delivery != null
                && IsAfter(previous, delivery.deliveryId);
            if (delivery == null
                || !deliveryIdValid
                || !deliveryIdExact
                || !characterIdExact
                || !buildingIdValid
                || !buildingIdExact
                || !itemIdValid
                || !itemIdExact
                || !characterKnown
                || !facilityKnown
                || !mealKnown
                || !uniqueId
                || !uniqueRoute
                || !requestedAtValid
                || !retryAfterValid
                || !ordered)
            {
                report.AddError(
                    "Character consumables deliveries contain an invalid, unknown, duplicate, or unordered delivery ID/reference: "
                    + $"id={delivery?.deliveryId ?? "<null>"}; "
                    + $"character={delivery?.characterId ?? "<null>"}; "
                    + $"building={delivery?.buildingInstanceId ?? "<null>"}; "
                    + $"item={delivery?.itemDefinitionId ?? "<null>"}; "
                    + $"idValid={deliveryIdValid}/{deliveryIdExact}; "
                    + $"characterExactKnown={characterIdExact}/{characterKnown}; "
                    + $"buildingValidExactKnown={buildingIdValid}/{buildingIdExact}/{facilityKnown}; "
                    + $"itemValidExactMeal={itemIdValid}/{itemIdExact}/{mealKnown}; "
                    + $"unique={uniqueId}/{uniqueRoute}; "
                    + $"time={requestedAtValid}/{retryAfterValid}; ordered={ordered}." );
                break;
            }
            previous = delivery.deliveryId;
        }

        previous = null;
        foreach (CharacterConsumableOperationState operation in payload.completedOperations)
        {
            bool validItem = operation != null && (operation.meal
                ? !operation.detox
                    && inventory.TryGetMeal(operation.ItemDefinitionId, out _)
                : operation.detox
                    ? inventory.TryResolveDetoxMedicine(
                        operation.ItemDefinitionId,
                        out _)
                    : inventory.TryResolveSubstance(
                        operation.ItemDefinitionId,
                        out _));
            bool validFacility = operation != null
                && (operation.meal || operation.detox
                    ? operation.FacilityId.IsValid
                        && IsExactValue(
                            operation.facilityInstanceId,
                            operation.FacilityId.Value)
                    : string.IsNullOrEmpty(operation.facilityInstanceId));
            bool validAppliedEffect = operation != null && (operation.detox
                ? IsFiniteRange(
                    operation.appliedEffect,
                    0f,
                    CharacterToxicityPolicy.MaximumToxicity)
                : operation.appliedEffect == 0f);
            if (operation == null
                || !operation.OperationId.IsValid
                || !IsExactValue(operation.operationId, operation.OperationId.Value)
                || !IsExactCharacterId(operation.characterId, operation.CharacterId)
                || !operation.ItemDefinitionId.IsValid
                || !IsExactValue(
                    operation.itemDefinitionId,
                    operation.ItemDefinitionId.Value)
                || !operation.ItemStackId.IsValid
                || !IsExactValue(operation.itemStackId, operation.ItemStackId.Value)
                || requireWorldReferences
                    && !characters.Contains(operation.CharacterId)
                    && !payload.activeMealPlans.Any(plan => CanRecoverWithoutActor(plan)
                        && plan.phase == CharacterMealPlanPhase.EffectsPublished
                        && plan.planId == operation.operationId
                        && plan.characterId == operation.characterId
                        && plan.itemDefinitionId == operation.itemDefinitionId
                        && plan.facilityInstanceId == operation.facilityInstanceId)
                || !validItem || !completedOperationIds.Add(operation.OperationId)
                || !validFacility
                || !validAppliedEffect
                || !string.IsNullOrEmpty(operation.mealEffectsSkippedReason)
                    && (!operation.meal || string.IsNullOrWhiteSpace(operation.mealEffectsSkippedReason)
                        || operation.mealEffectsSkippedReason != operation.mealEffectsSkippedReason.Trim())
                || !IsFiniteNonNegative(operation.completedAt)
                || operation.completedAt > payload.timelineTime
                || !IsAfter(previous, operation.operationId))
            {
                report.AddError("Character consumables operation ledger contains an invalid, unknown, duplicate, or unordered operation/reference.");
                break;
            }
            previous = operation.operationId;
        }

        previous = null;
        foreach (CharacterMealPlanSaveData plan in payload.activeMealPlans)
        {
            bool validMeal = plan != null
                && inventory.TryGetMeal(plan.ItemDefinitionId, out _);
            bool phaseAllowsCompletedOperation = plan != null
                && plan.phase == CharacterMealPlanPhase.EffectsPublished;
            bool completedOperationMatchesPhase = plan != null
                && completedOperationIds.Contains(plan.OperationId)
                == phaseAllowsCompletedOperation;
            bool completedReceiptMatches = !phaseAllowsCompletedOperation
                || payload.completedOperations.Any(operation => operation != null && operation.meal
                    && operation.operationId == plan.planId && operation.characterId == plan.characterId
                    && operation.facilityInstanceId == plan.facilityInstanceId
                    && operation.itemDefinitionId == plan.itemDefinitionId
                    && operation.itemStackId == plan.physicalCommitSourceStackIds?.FirstOrDefault()
                    && operation.mealEffectsSkippedReason == plan.effectsSkippedReason);
            if (plan == null
                || !plan.OperationId.IsValid
                || !IsExactValue(plan.planId, plan.OperationId.Value)
                || !IsExactCharacterId(plan.characterId, plan.CharacterId)
                || requireWorldReferences && !characters.Contains(plan.CharacterId)
                    && !CanRecoverWithoutActor(plan)
                || !plan.FacilityId.IsValid
                || !IsExactValue(
                    plan.facilityInstanceId,
                    plan.FacilityId.Value)
                || requireWorldReferences
                    && !string.Equals(
                        plan.facilityInstanceId,
                        CharacterConsumablesRuntime.FieldMealFacilityId,
                        StringComparison.Ordinal)
                    && !facilities.Contains(plan.FacilityId)
                    && !CanRecoverWithoutActor(plan)
                || !plan.SourceStackId.IsValid
                || !IsExactValue(plan.sourceStackId, plan.SourceStackId.Value)
                || !plan.ItemDefinitionId.IsValid
                || !IsExactValue(
                    plan.itemDefinitionId,
                    plan.ItemDefinitionId.Value)
                || !validMeal
                || plan.phase is not (CharacterMealPlanPhase.Eating
                    or CharacterMealPlanPhase.ItemCommitted
                    or CharacterMealPlanPhase.EffectsApplied
                    or CharacterMealPlanPhase.EffectsPublished)
                || !activePlanIds.Add(plan.OperationId)
                || !completedOperationMatchesPhase
                || !completedReceiptMatches
                || !ValidFactPublication(payload, plan.planId, plan.factAbsoluteDay, plan.publicationKey,
                    plan.publicationPending, phaseAllowsCompletedOperation)
                || !ValidMealFactPublication(plan, phaseAllowsCompletedOperation)
                || !ValidPhysicalCommit(plan, inventory, requireWorldReferences)
                || !ValidMealEffectDecision(plan)
                || !IsFiniteNonNegative(plan.createdAt)
                || plan.createdAt > payload.timelineTime
                || !IsFiniteNonNegative(plan.leaseExpiresAt)
                || plan.leaseExpiresAt < plan.createdAt
                || !IsFiniteNonNegative(plan.expectedCompletionEta)
                || plan.expectedCompletionEta <= 0f
                || !IsFiniteNonNegative(plan.beginContamination)
                || plan.resolvedContaminationDamage
                    && (!plan.contaminationRollFrozen
                        || !plan.committedContaminated)
                || !IsAfter(previous, plan.planId))
            {
                report.AddError(
                    "Character consumables active meal plans contain an invalid, duplicate, unordered, or unknown reservation intent.");
                break;
            }
            previous = plan.planId;
        }

        previous = null;
        foreach (CharacterSubstanceUsePlanSaveData plan in
                 payload.activeSubstanceUsePlans)
        {
            bool validSubstance = plan != null
                && inventory.TryResolveSubstance(plan.ItemDefinitionId, out _);
            bool effectsPublished = plan != null
                && plan.phase == CharacterSubstanceUsePlanPhase.EffectsPublished;
            bool completedMatches = plan != null
                && completedOperationIds.Contains(plan.OperationId) == effectsPublished;
            bool finiteTargets = plan != null
                && IsFiniteRange(plan.resolvedTolerance, 0f, 100f)
                && IsFiniteRange(plan.resolvedAddiction, 0f, 100f)
                && IsFiniteRange(plan.resolvedWithdrawal, 0f, 100f)
                && IsFiniteNonNegative(plan.resolvedActiveSeconds)
                && IsFiniteNonNegative(plan.resolvedSecondsSinceLastDose)
                && IsFiniteNonNegative(plan.resolvedScheduledCooldownSeconds)
                && IsFiniteRange(plan.effectToleranceRatio, 0f, 1f)
                && IsFinite(plan.resolvedEffectMood)
                && IsFiniteNonNegative(plan.resolvedOverdoseDamage)
                && (plan.resolvedOverdosed
                    ? plan.resolvedOverdoseDamage > 0f
                    : plan.resolvedOverdoseDamage == 0f);
            if (plan == null
                || !plan.OperationId.IsValid
                || !IsExactValue(plan.operationId, plan.OperationId.Value)
                || !IsExactCharacterId(plan.characterId, plan.CharacterId)
                || requireWorldReferences && !characters.Contains(plan.CharacterId)
                || !plan.ItemDefinitionId.IsValid
                || !IsExactValue(
                    plan.itemDefinitionId,
                    plan.ItemDefinitionId.Value)
                || !plan.SourceStackId.IsValid
                || !IsExactValue(plan.sourceStackId, plan.SourceStackId.Value)
                || !validSubstance
                || plan.phase is not (CharacterSubstanceUsePlanPhase.ItemCommitted
                    or CharacterSubstanceUsePlanPhase.EffectsApplied
                    or CharacterSubstanceUsePlanPhase.EffectsPublished)
                || !activeSubstancePlanIds.Add(plan.OperationId)
                || activePlanIds.Contains(plan.OperationId)
                || !completedMatches
                || !finiteTargets
                || !ValidFactPublication(payload, plan.operationId, plan.factAbsoluteDay, plan.publicationKey,
                    plan.publicationPending, effectsPublished)
                || plan.becameAddicted
                    && !plan.resolvedAddicted
                || !ValidSubstancePhysicalCommit(
                    plan,
                    inventory,
                    requireWorldReferences)
                || !IsAfter(previous, plan.operationId))
            {
                report.AddError(
                    "Character consumables active substance plans contain an invalid, duplicate, unordered, or unknown pending disposition.");
                break;
            }
            previous = plan.operationId;
        }

        previous = null;
        foreach (CharacterDetoxTreatmentPlanSaveData plan in
                 payload.activeDetoxTreatmentPlans)
        {
            bool validMedicine = plan != null
                && inventory.TryResolveDetoxMedicine(
                    plan.ItemDefinitionId,
                    out CharacterDetoxMedicineDefinitionSnapshot medicine)
                && plan.detoxReduction.Equals(medicine.DetoxReduction);
            bool effectsPublished = plan != null
                && plan.phase is CharacterDetoxTreatmentPlanPhase.EffectsPublished
                    or CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged;
            bool completedMatches = plan != null
                && completedOperationIds.Contains(plan.OperationId) == effectsPublished;
            if (plan == null
                || !plan.OperationId.IsValid
                || !IsExactValue(plan.operationId, plan.OperationId.Value)
                || !IsExactCharacterId(plan.characterId, plan.CharacterId)
                || requireWorldReferences && !characters.Contains(plan.CharacterId)
                || !plan.FacilityId.IsValid
                || !IsExactValue(
                    plan.facilityInstanceId,
                    plan.FacilityId.Value)
                || requireWorldReferences && !facilities.Contains(plan.FacilityId)
                || !plan.ItemDefinitionId.IsValid
                || !IsExactValue(
                    plan.itemDefinitionId,
                    plan.ItemDefinitionId.Value)
                || !plan.SourceStackId.IsValid
                || !IsExactValue(plan.sourceStackId, plan.SourceStackId.Value)
                || !validMedicine
                || !IsFiniteRange(
                    plan.detoxReduction,
                    float.Epsilon,
                    CharacterToxicityPolicy.MaximumToxicity)
                || !IsFiniteRange(
                    plan.appliedReduction,
                    0f,
                    plan.detoxReduction)
                || (!effectsPublished && plan.appliedReduction != 0f)
                || plan.phase is not (CharacterDetoxTreatmentPlanPhase.ItemCommitted
                    or CharacterDetoxTreatmentPlanPhase.EffectsPublished
                    or CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged)
                || !activeDetoxPlanIds.Add(plan.OperationId)
                || activePlanIds.Contains(plan.OperationId)
                || activeSubstancePlanIds.Contains(plan.OperationId)
                || !completedMatches
                || !ValidDetoxPhysicalCommit(
                    plan,
                    inventory,
                    requireWorldReferences)
                || !ValidFactPublication(payload, plan.operationId, plan.factAbsoluteDay, plan.publicationKey,
                    plan.publicationPending, effectsPublished)
                || plan.publicationPending && plan.phase == CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged
                || !IsAfter(previous, plan.operationId))
            {
                report.AddError(
                    "Character consumables active detox plans contain an invalid, duplicate, unordered, or unknown pending disposition.");
                break;
            }
            previous = plan.operationId;
        }

        previous = null;
        ValidateMealFailures(payload, report, characters, requireWorldReferences);
        foreach (CharacterMealFollowupCooldownSaveData cooldown in
                 payload.mealFollowupCooldowns)
        {
            if (cooldown == null
                || !IsExactCharacterId(cooldown.characterId, cooldown.CharacterId)
                || requireWorldReferences
                    && !characters.Contains(cooldown.CharacterId)
                || !cooldownIds.Add(cooldown.CharacterId)
                || !IsFiniteNonNegative(cooldown.untilGameSeconds)
                || !IsAfter(previous, cooldown.characterId))
            {
                report.AddError(
                    "Character meal follow-up cooldowns contain invalid, duplicate, or unordered state.");
                break;
            }
            previous = cooldown.characterId;
        }
    }

    private static void ValidateMealFailures(DungeonCharacterConsumablesSaveData payload,
        DungeonGameRestoreReport report, HashSet<CharacterId> characters, bool requireWorldReferences)
    {
        string previous = null;
        foreach (CharacterMealOperationFailureState failure in payload.failedMealOperations)
        {
            if (failure == null || !failure.OperationId.IsValid
                || !IsExactValue(failure.operationId, failure.OperationId.Value)
                || !IsExactCharacterId(failure.characterId, failure.CharacterId)
                || !failure.FacilityId.IsValid
                || !IsExactValue(failure.facilityInstanceId, failure.FacilityId.Value)
                || requireWorldReferences && !characters.Contains(failure.CharacterId)
                    && !payload.activeMealPlans.Any(plan => CanRecoverWithoutActor(plan)
                        && plan.planId == failure.operationId && plan.characterId == failure.characterId
                        && plan.facilityInstanceId == failure.facilityInstanceId)
                || !Enum.IsDefined(typeof(CharacterConsumablesFailureCode), failure.failureCode)
                || failure.failureCode is CharacterConsumablesFailureCode.None
                    or CharacterConsumablesFailureCode.DeliveryPending
                || string.IsNullOrWhiteSpace(failure.detail) || failure.detail != failure.detail.Trim()
                || !IsAfter(previous, failure.operationId))
            {
                report.AddError("Character meal failure has invalid, duplicate, unordered identity or reason.");
                continue;
            }
            previous = failure.operationId;
            CharacterMealPlanSaveData plan = payload.activeMealPlans.FirstOrDefault(value =>
                value != null && value.planId == failure.operationId);
            CharacterConsumableOperationState completed = payload.completedOperations.FirstOrDefault(value =>
                value != null && value.operationId == failure.operationId);
            bool validPhase = failure.recoveryPending
                ? plan != null && (plan.phase is CharacterMealPlanPhase.ItemCommitted
                    or CharacterMealPlanPhase.EffectsApplied or CharacterMealPlanPhase.EffectsPublished)
                    && plan.characterId == failure.characterId
                    && plan.facilityInstanceId == failure.facilityInstanceId
                : plan == null && completed == null;
            if (!validPhase
                || payload.activeSubstanceUsePlans.Any(value => value != null && value.operationId == failure.operationId)
                || payload.activeDetoxTreatmentPlans.Any(value => value != null && value.operationId == failure.operationId))
                report.AddError("Character meal failure conflicts with its active/completed operation owner.");
        }
    }

    private static void ValidatePendingPolicyFact(
        DungeonCharacterConsumablesSaveData payload, DungeonGameRestoreReport report)
    {
        if (payload.pendingPolicyFacts.Count > 1)
        { report.AddError("Consumables policy has more than one unresolved transition."); return; }
        if (payload.pendingPolicyFacts.Count == 0) return;
        try
        {
            CharacterConsumablesPolicyChangedFact fact =
                CharacterConsumablesPolicyChangedFact.Restore(payload.pendingPolicyFacts[0]);
            string actual = fact.PolicyId switch
            {
                "character-diet" => payload.dietPolicies.FirstOrDefault(value =>
                    value?.characterId == fact.CharacterId.Value)?.policy.ToString().ToLowerInvariant(),
                "character-meal-quality" => payload.mealQualityPolicies.FirstOrDefault(value =>
                    value?.characterId == fact.CharacterId.Value)?.maximumQuality.ToString().ToLowerInvariant(),
                "character-substance" => SubstanceValue(),
                _ => null
            };
            if (fact.AfterRevision != payload.policyRevision || actual != fact.AfterValue)
                report.AddError("Pending consumables policy fact disagrees with the authoritative policy/revision.");

            string SubstanceValue()
            {
                CharacterSubstancePolicyState policy = payload.substancePolicies.FirstOrDefault(value =>
                    value?.characterId == fact.CharacterId.Value && value.itemDefinitionId == fact.ScopeId);
                return policy == null ? null : CharacterConsumablesPolicyChangedFact.CanonicalSubstanceValue(policy);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("Invalid pending consumables policy fact: " + exception.Message); }
    }

    internal static CharacterConsumablesAggregateState Build(
        DungeonCharacterConsumablesSaveData payload)
    {
        CharacterConsumablesAggregateState state = new()
        {
            TimelineTime = payload.timelineTime,
            ClockNeedsBinding = true,
            NextOperationSequence = payload.nextOperationSequence,
            NextDeliverySequence = payload.nextDeliverySequence,
            PolicyRevision = payload.policyRevision,
            PendingPolicyFact = payload.pendingPolicyFacts.Count == 0 ? null
                : CharacterConsumablesPolicyChangedFact.Restore(payload.pendingPolicyFacts[0])
        };
        foreach (CharacterDietPolicyState source in payload.dietPolicies)
        {
            CharacterDietPolicyState clone = Clone(source);
            state.DietPolicies.Add(clone.CharacterId, clone);
        }
        foreach (CharacterMealQualityPolicyState source in payload.mealQualityPolicies)
        {
            CharacterMealQualityPolicyState clone = Clone(source);
            state.MealQualityPolicies.Add(clone.CharacterId, clone);
        }
        foreach (CharacterSubstancePolicyState source in payload.substancePolicies)
        {
            CharacterSubstancePolicyState clone = Clone(source);
            state.SubstancePolicies.Add(new(clone.CharacterId, clone.ItemDefinitionId), clone);
        }
        foreach (CharacterSubstanceState source in payload.substanceStates)
        {
            CharacterSubstanceState clone = Clone(source);
            state.SubstanceStates.Add(new(clone.CharacterId, clone.ItemDefinitionId), clone);
        }
        foreach (CharacterToxicityState source in payload.toxicityStates)
        {
            CharacterToxicityState clone = Clone(source);
            state.ToxicityStates.Add(clone.CharacterId, clone);
        }
        foreach (CharacterMealDeliveryState source in payload.pendingMealDeliveries)
        {
            CharacterMealDeliveryState clone = Clone(source);
            state.PendingDeliveries.Add(clone.DeliveryId, clone);
            state.DeliveryByRoute.Add(Route(clone), clone.DeliveryId);
        }
        foreach (CharacterConsumableOperationState source in payload.completedOperations)
        {
            CharacterConsumableOperationState clone = Clone(source);
            state.CompletedOperations.Add(clone.OperationId, clone);
        }
        foreach (CharacterMealOperationFailureState source in payload.failedMealOperations)
            state.MealFailures.Add(source.OperationId, Clone(source));
        foreach (CharacterMealPlanSaveData source in payload.activeMealPlans)
        {
            CharacterMealPlan plan = FromSaveData(source);
            state.ActiveMealPlans.Add(source.OperationId, plan);
        }
        foreach (CharacterSubstanceUsePlanSaveData source in
                 payload.activeSubstanceUsePlans)
        {
            CharacterSubstanceUsePlan plan = FromSaveData(source);
            state.ActiveSubstanceUsePlans.Add(source.OperationId, plan);
        }
        foreach (CharacterDetoxTreatmentPlanSaveData source in
                 payload.activeDetoxTreatmentPlans)
        {
            CharacterDetoxTreatmentPlan plan = FromSaveData(source);
            state.ActiveDetoxTreatmentPlans.Add(source.OperationId, plan);
        }
        foreach (CharacterMealFollowupCooldownSaveData source in
                 payload.mealFollowupCooldowns)
        {
            state.MealFollowupCooldownUntil.Add(
                source.CharacterId,
                source.untilGameSeconds);
        }
        return state;
    }

    private static bool ValidSubstance(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        ISet<CharacterId> characters,
        ICharacterConsumablesInventoryPort inventory,
        bool requireWorldReferences) =>
        characterId.IsValid
        && (!requireWorldReferences || characters.Contains(characterId))
        && itemId.IsValid
        && inventory.TryResolveSubstance(itemId, out _);

    private static bool IsAfter(string previous, string value) =>
        value != null && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && (previous == null || string.CompareOrdinal(previous, value) < 0);
    private static bool IsExactCharacterId(string raw, CharacterId id) =>
        id.IsValid
        && string.Equals(id.Value, raw ?? string.Empty, StringComparison.Ordinal);
    private static bool IsExactValue(string raw, string typedValue) =>
        string.Equals(typedValue, raw ?? string.Empty, StringComparison.Ordinal);

    private static bool IsFiniteRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;

    private static bool ValidDetoxPhysicalCommit(
        CharacterDetoxTreatmentPlanSaveData plan,
        ICharacterConsumablesInventoryPort inventory,
        bool requirePhysicalJoin)
    {
        IReadOnlyList<string> sourceIds = plan.physicalCommitSourceStackIds != null
            ? plan.physicalCommitSourceStackIds
            : Array.Empty<string>();
        bool canonicalSources = sourceIds.Count > 0;
        string previous = null;
        for (int index = 0; index < sourceIds.Count; index++)
        {
            if (!IsAfter(previous, sourceIds[index]))
            {
                canonicalSources = false;
                break;
            }
            previous = sourceIds[index];
        }
        bool structurallyValid = string.Equals(
                plan.physicalCommitOperationId,
                plan.operationId,
                StringComparison.Ordinal)
            && string.Equals(
                plan.physicalCommitReasonCode,
                CharacterConsumablesRuntime.DetoxPhysicalSinkReason,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(plan.physicalCommitId)
            && plan.physicalCommitId.StartsWith(
                "physical-batch-disposition:",
                StringComparison.Ordinal)
            && canonicalSources
            && plan.physicalCommitQuantity == 1
            && plan.physicalCommitInputMassGrams > 0L;
        if (!structurallyValid || !requirePhysicalJoin)
            return structurallyValid;

        if (!inventory.TryGetPendingDetoxConsumption(
                plan.OperationId,
                out CharacterDetoxPhysicalCommitSnapshot pending))
        {
            return plan.phase is CharacterDetoxTreatmentPlanPhase.EffectsPublished
                or CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged;
        }
        return string.Equals(
                plan.physicalCommitOperationId,
                pending.OperationId,
                StringComparison.Ordinal)
            && string.Equals(
                plan.physicalCommitReasonCode,
                pending.ReasonCode,
                StringComparison.Ordinal)
            && string.Equals(
                plan.physicalCommitId,
                pending.CommitId,
                StringComparison.Ordinal)
            && plan.physicalCommitQuantity == pending.Quantity
            && plan.physicalCommitInputMassGrams == pending.InputMassGrams
            && sourceIds.SequenceEqual(
                pending.SourceStackIds ?? Array.Empty<string>());
    }

    private static bool ValidSubstancePhysicalCommit(
        CharacterSubstanceUsePlanSaveData plan,
        ICharacterConsumablesInventoryPort inventory,
        bool requirePhysicalJoin)
    {
        IReadOnlyList<string> sourceIds = plan.physicalCommitSourceStackIds != null
            ? plan.physicalCommitSourceStackIds
            : Array.Empty<string>();
        bool canonicalSources = sourceIds.Count > 0;
        string previous = null;
        for (int index = 0; index < sourceIds.Count; index++)
        {
            if (!IsAfter(previous, sourceIds[index]))
            {
                canonicalSources = false;
                break;
            }
            previous = sourceIds[index];
        }
        bool structurallyValid = string.Equals(
                plan.physicalCommitOperationId,
                plan.operationId,
                StringComparison.Ordinal)
            && string.Equals(
                plan.physicalCommitReasonCode,
                CharacterConsumablesRuntime.SubstancePhysicalSinkReason,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(plan.physicalCommitId)
            && plan.physicalCommitId.StartsWith(
                "physical-batch-disposition:",
                StringComparison.Ordinal)
            && canonicalSources
            && plan.physicalCommitQuantity == 1
            && plan.physicalCommitInputMassGrams > 0L;
        if (!structurallyValid || !requirePhysicalJoin)
            return structurallyValid;

        if (!inventory.TryGetPendingSubstanceConsumption(
                plan.OperationId,
                out CharacterSubstancePhysicalCommitSnapshot pending))
        {
            return plan.phase == CharacterSubstanceUsePlanPhase.EffectsPublished;
        }
        return string.Equals(
                plan.physicalCommitOperationId,
                pending.OperationId,
                StringComparison.Ordinal)
            && string.Equals(
                plan.physicalCommitReasonCode,
                pending.ReasonCode,
                StringComparison.Ordinal)
            && string.Equals(
                plan.physicalCommitId,
                pending.CommitId,
                StringComparison.Ordinal)
            && plan.physicalCommitQuantity == pending.Quantity
            && plan.physicalCommitInputMassGrams == pending.InputMassGrams
            && sourceIds.SequenceEqual(pending.SourceStackIds ?? Array.Empty<string>());
    }

    private static bool CanRecoverWithoutActor(CharacterMealPlanSaveData plan) =>
        plan != null && plan.physicalContextFrozen && plan.effectEvidenceFrozen
        && plan.phase is CharacterMealPlanPhase.EffectsApplied or CharacterMealPlanPhase.EffectsPublished;

    private static bool ValidFactPublication(DungeonCharacterConsumablesSaveData payload,
        string operationId, int day, string key, bool pending, bool published)
    {
        if (!published) return day == 0 && string.IsNullOrEmpty(key) && !pending;
        if (day < 1 || !ValidOpaquePublicationKey(key)) return false;
        var completed = payload.completedOperations.FirstOrDefault(value => value?.operationId == operationId);
        return completed != null && (pending ? string.IsNullOrEmpty(completed.outcomeResultKey)
            : completed.outcomeResultKey == key);
    }

    private static bool ValidOpaquePublicationKey(string key) => !string.IsNullOrWhiteSpace(key)
        && key.Length <= 1024 && key == key.Trim() && key.IndexOfAny(new[] { '\r', '\n', '\0' }) < 0;

    private static bool ValidMealFactPublication(CharacterMealPlanSaveData plan, bool published)
    {
        if (!published) return string.IsNullOrEmpty(plan.primitivePublicationKey)
            && plan.primitiveNutrition == 0f && !plan.recipientActiveAtCompletion
            && !plan.postCommitEventDelivered;
        bool primitive = plan.primitiveSurvivalOutcomeRequired && string.IsNullOrEmpty(plan.effectsSkippedReason);
        return (primitive ? ValidOpaquePublicationKey(plan.primitivePublicationKey)
                && plan.primitivePublicationKey != plan.publicationKey && IsFiniteNonNegative(plan.primitiveNutrition)
                : string.IsNullOrEmpty(plan.primitivePublicationKey) && plan.primitiveNutrition == 0f)
            && (!plan.publicationPending || !plan.postCommitEventDelivered && !plan.ritualFastingConsequenceDelivered);
    }

    private static bool ValidMealEffectDecision(CharacterMealPlanSaveData plan)
    {
        bool committed = plan.phase != CharacterMealPlanPhase.Eating;
        bool decided = plan.phase is CharacterMealPlanPhase.EffectsApplied or CharacterMealPlanPhase.EffectsPublished;
        bool skipped = !string.IsNullOrEmpty(plan.effectsSkippedReason);
        return (!plan.ritualFastingConsequenceDelivered || plan.phase == CharacterMealPlanPhase.EffectsPublished)
            && plan.physicalContextFrozen == committed
            && plan.effectEvidenceFrozen == decided
            && IsFiniteNonNegative(plan.hungerBefore) && IsFiniteNonNegative(plan.hungerAfter)
            && IsFiniteNonNegative(plan.healthBefore) && IsFiniteNonNegative(plan.healthAfter)
            && !float.IsNaN(plan.moodBefore) && !float.IsInfinity(plan.moodBefore)
            && !float.IsNaN(plan.moodAfter) && !float.IsInfinity(plan.moodAfter)
            && (!skipped || decided
                && !string.IsNullOrWhiteSpace(plan.effectsSkippedReason)
                && plan.effectsSkippedReason == plan.effectsSkippedReason.Trim()
                && plan.hungerBefore == plan.hungerAfter
                && plan.healthBefore == plan.healthAfter
                && plan.moodBefore == plan.moodAfter);
    }

    private static bool ValidPhysicalCommit(
        CharacterMealPlanSaveData plan,
        ICharacterConsumablesInventoryPort inventory,
        bool requirePhysicalJoin)
    {
        bool committed = plan.phase is CharacterMealPlanPhase.ItemCommitted
            or CharacterMealPlanPhase.EffectsApplied
            or CharacterMealPlanPhase.EffectsPublished;
        IReadOnlyList<string> sourceIds = plan.physicalCommitSourceStackIds != null
            ? plan.physicalCommitSourceStackIds
            : Array.Empty<string>();
        if (!committed)
        {
            return string.IsNullOrEmpty(plan.physicalCommitOperationId)
                && string.IsNullOrEmpty(plan.physicalCommitReasonCode)
                && string.IsNullOrEmpty(plan.physicalCommitId)
                && sourceIds.Count == 0
                && plan.physicalCommitQuantity == 0
                && plan.physicalCommitInputMassGrams == 0L
                && !plan.committedPolicyViolation
                && !plan.committedContaminated;
        }

        bool canonicalSources = sourceIds.Count > 0;
        string previous = null;
        for (int index = 0; index < sourceIds.Count; index++)
        {
            string sourceId = sourceIds[index];
            if (!IsAfter(previous, sourceId))
            {
                canonicalSources = false;
                break;
            }
            previous = sourceId;
        }
        bool structurallyValid = string.Equals(
                plan.physicalCommitOperationId,
                plan.planId,
                StringComparison.Ordinal)
            && string.Equals(
                plan.physicalCommitReasonCode,
                CharacterConsumablesRuntime.MealPhysicalSinkReason,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(plan.physicalCommitId)
            && plan.physicalCommitId.StartsWith(
                "physical-batch-disposition:",
                StringComparison.Ordinal)
            && canonicalSources
            && plan.physicalCommitQuantity == 1
            && plan.physicalCommitInputMassGrams > 0L;
        if (!structurallyValid || !requirePhysicalJoin)
            return structurallyValid;

        if (!inventory.TryGetPendingMealConsumption(
                plan.OperationId,
                out CharacterMealPhysicalCommitSnapshot pending))
        {
            // Effects may have been published and the idempotent physical ack
            // may already have completed immediately before the save boundary.
            return plan.phase == CharacterMealPlanPhase.EffectsPublished;
        }
        return PhysicalCommitEquals(plan, pending);
    }

    private static bool PhysicalCommitEquals(
        CharacterMealPlanSaveData plan,
        CharacterMealPhysicalCommitSnapshot pending) =>
        string.Equals(
            plan.physicalCommitOperationId,
            pending.OperationId,
            StringComparison.Ordinal)
        && string.Equals(
            plan.physicalCommitReasonCode,
            pending.ReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            plan.physicalCommitId,
            pending.CommitId,
            StringComparison.Ordinal)
        && plan.physicalCommitQuantity == pending.Quantity
        && plan.physicalCommitInputMassGrams == pending.InputMassGrams
        && (plan.physicalCommitSourceStackIds ?? new List<string>())
            .SequenceEqual(pending.SourceStackIds ?? Array.Empty<string>());

    private static void ValidateSequenceWatermarks(
        DungeonCharacterConsumablesSaveData payload,
        DungeonGameRestoreReport report)
    {
        long highestOperation = ValidateGeneratedIds(
            payload.completedOperations,
            value => value?.operationId,
            CharacterConsumableIdContract.ClassifyOperation,
            "operation",
            report);
        highestOperation = Math.Max(
            highestOperation,
            ValidateGeneratedIds(
                payload.activeMealPlans,
                value => value?.planId,
                CharacterConsumableIdContract.ClassifyOperation,
                "active meal operation",
                report));
        highestOperation = Math.Max(
            highestOperation,
            ValidateGeneratedIds(
                payload.activeSubstanceUsePlans,
                value => value?.operationId,
                CharacterConsumableIdContract.ClassifyOperation,
                "active substance operation",
                report));
        highestOperation = Math.Max(
            highestOperation,
            ValidateGeneratedIds(
                payload.activeDetoxTreatmentPlans,
                value => value?.operationId,
                CharacterConsumableIdContract.ClassifyOperation,
                "active detox operation",
                report));
        highestOperation = Math.Max(highestOperation, ValidateGeneratedIds(
            payload.failedMealOperations, value => value?.operationId,
            CharacterConsumableIdContract.ClassifyOperation, "failed meal operation", report));
        long highestDelivery = ValidateGeneratedIds(
            payload.pendingMealDeliveries,
            value => value?.deliveryId,
            CharacterConsumableIdContract.ClassifyDelivery,
            "delivery",
            report);

        ValidateNextSequence(
            payload.nextOperationSequence,
            highestOperation,
            "operation",
            report);
        ValidateNextSequence(
            payload.nextDeliverySequence,
            highestDelivery,
            "delivery",
            report);
    }

    private static long ValidateGeneratedIds<T>(
        IEnumerable<T> values,
        Func<T, string> selectId,
        TryClassifyGeneratedId classify,
        string label,
        DungeonGameRestoreReport report)
        where T : class
    {
        long highest = 0L;
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (T value in values ?? Enumerable.Empty<T>())
        {
            string id = selectId(value);
            if (id == null)
            {
                continue;
            }
            if (!ids.Add(id))
            {
                report.AddError(
                    $"Character consumables {label} ID '{id}' is duplicated.");
                continue;
            }

            ConsumableGeneratedIdKind kind = classify(id, out long sequence);
            if (kind == ConsumableGeneratedIdKind.Malformed)
            {
                report.AddError(
                    $"Character consumables {label} ID '{id}' has a malformed or overflowing generated sequence.");
            }
            else if (kind == ConsumableGeneratedIdKind.Generated)
            {
                highest = Math.Max(highest, sequence);
            }
        }
        return highest;
    }

    private static void ValidateNextSequence(
        long next,
        long highest,
        string label,
        DungeonGameRestoreReport report)
    {
        if (next < 1L)
        {
            report.AddError(
                $"Character consumables next {label} sequence must be positive.");
            return;
        }
        if (next <= highest)
        {
            report.AddError(
                $"Character consumables next {label} sequence {next} does not exceed existing generated sequence {highest}.");
        }
    }

    private delegate ConsumableGeneratedIdKind TryClassifyGeneratedId(
        string id,
        out long sequence);
    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    private static bool IsFiniteNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    private static bool InRange(float value, float minimum, float maximum) =>
        IsFiniteNonNegative(value) && value >= minimum && value <= maximum;
}
