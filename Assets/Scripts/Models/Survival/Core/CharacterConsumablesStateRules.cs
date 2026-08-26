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
    internal readonly Dictionary<ConsumableDeliveryId, CharacterMealDeliveryState>
        PendingDeliveries = new();
    internal readonly Dictionary<MealDeliveryRoute, ConsumableDeliveryId>
        DeliveryByRoute = new();
    internal readonly Dictionary<ConsumableOperationId, CharacterConsumableOperationState>
        CompletedOperations = new();
    internal readonly Dictionary<CharacterId, float> MealFollowupCooldownUntil = new();
    internal readonly Dictionary<ConsumableOperationId, CharacterMealPlan>
        ActiveMealPlans = new();
    internal readonly Dictionary<ConsumableOperationId, CharacterSubstanceUsePlan>
        ActiveSubstanceUsePlans = new();
    internal long NextOperationSequence = 1;
    internal long NextDeliverySequence = 1;
    internal float NextDeliveryPruneAt;

    internal CharacterConsumablesAggregateState Clone()
    {
        CharacterConsumablesAggregateState clone = new()
        {
            NextOperationSequence = NextOperationSequence,
            NextDeliverySequence = NextDeliverySequence,
            NextDeliveryPruneAt = NextDeliveryPruneAt
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
        return clone;
    }
}

internal static class CharacterConsumablesStateRules
{
    internal static CharacterSubstanceUsePlan Clone(
        CharacterSubstanceUsePlan source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterSubstanceUsePlanPhase.ItemCommitted,
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
        becameAddicted = source?.becameAddicted ?? false
    };

    internal static CharacterSubstanceUsePlanSaveData ToSaveData(
        CharacterSubstanceUsePlan source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterSubstanceUsePlanPhase.ItemCommitted,
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
        becameAddicted = source?.becameAddicted ?? false
    };

    internal static CharacterSubstanceUsePlan FromSaveData(
        CharacterSubstanceUsePlanSaveData source) => new()
    {
        operationId = source?.operationId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        phase = source?.phase ?? CharacterSubstanceUsePlanPhase.ItemCommitted,
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
        becameAddicted = source?.becameAddicted ?? false
    };

    internal static CharacterMealPlan Clone(CharacterMealPlan source) => new()
    {
        planId = source?.planId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        transportStackId = source?.transportStackId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        mealQuantityLeaseId = source?.mealQuantityLeaseId ?? string.Empty,
        phase = source?.phase ?? CharacterMealPlanPhase.Aborted,
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
        committedContaminated = source?.committedContaminated ?? false
    };

    internal static CharacterMealPlanSaveData ToSaveData(
        CharacterMealPlan source) => new()
    {
        planId = source?.planId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        phase = source?.phase ?? CharacterMealPlanPhase.Aborted,
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
        committedContaminated = source?.committedContaminated ?? false
    };

    internal static CharacterMealPlan FromSaveData(
        CharacterMealPlanSaveData source) => new()
    {
        planId = source?.planId ?? string.Empty,
        characterId = source?.characterId ?? string.Empty,
        facilityInstanceId = source?.facilityInstanceId ?? string.Empty,
        sourceStackId = source?.sourceStackId ?? string.Empty,
        itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
        mealQuantityLeaseId = string.Empty,
        phase = source?.phase ?? CharacterMealPlanPhase.Aborted,
        createdAt = source?.createdAt ?? 0d,
        leaseExpiresAt = source?.leaseExpiresAt ?? 0d,
        expectedCompletionEta = source?.expectedCompletionEta ?? 0f,
        automaticOperation = source?.automaticOperation ?? false,
        beginContamination = source?.beginContamination ?? 0f,
        facilitySlotReserved = false,
        physicalConsumptionCommitted = source?.phase is
            CharacterMealPlanPhase.ItemCommitted or CharacterMealPlanPhase.EffectsPublished,
        physicalCommitOperationId = source?.physicalCommitOperationId ?? string.Empty,
        physicalCommitReasonCode = source?.physicalCommitReasonCode ?? string.Empty,
        physicalCommitId = source?.physicalCommitId ?? string.Empty,
        physicalCommitSourceStackIds = source?.physicalCommitSourceStackIds?.ToList()
            ?? new List<string>(),
        physicalCommitQuantity = source?.physicalCommitQuantity ?? 0,
        physicalCommitInputMassGrams = source?.physicalCommitInputMassGrams ?? 0L,
        committedPolicyViolation = source?.committedPolicyViolation ?? false,
        committedContaminated = source?.committedContaminated ?? false
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
            operationId = source?.operationId ?? string.Empty,
            characterId = source?.characterId ?? string.Empty,
            itemDefinitionId = source?.itemDefinitionId ?? string.Empty,
            itemStackId = source?.itemStackId ?? string.Empty,
            meal = source?.meal ?? false,
            policyViolation = source?.policyViolation ?? false,
            contaminated = source?.contaminated ?? false,
            completedAt = source?.completedAt ?? 0f
        };

    internal static MealDeliveryRoute Route(CharacterMealDeliveryState delivery) =>
        new(delivery.CharacterId, delivery.BuildingInstanceId, delivery.ItemDefinitionId);

    internal static DungeonCharacterConsumablesSaveData Capture(
        CharacterConsumablesAggregateState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        DungeonCharacterConsumablesSaveData payload = new()
        {
            version = DungeonCharacterConsumablesSaveData.CurrentVersion,
            nextOperationSequence = state.NextOperationSequence,
            nextDeliverySequence = state.NextDeliverySequence,
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
            pendingMealDeliveries = state.PendingDeliveries.Values.Select(Clone)
                .OrderBy(value => value.deliveryId, StringComparer.Ordinal).ToList(),
            completedOperations = state.CompletedOperations.Values.Select(Clone)
                .OrderBy(value => value.operationId, StringComparer.Ordinal).ToList(),
            activeMealPlans = state.ActiveMealPlans.Values
                .Select(ToSaveData)
                .OrderBy(value => value.planId, StringComparer.Ordinal)
                .ToList(),
            activeSubstanceUsePlans = state.ActiveSubstanceUsePlans.Values
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
        if (payload.nextOperationSequence < 1 || payload.nextDeliverySequence < 1)
        {
            report.AddError("Character consumables sequences must be positive.");
        }
        if (payload.dietPolicies == null || payload.substancePolicies == null
            || payload.substanceStates == null || payload.pendingMealDeliveries == null
            || payload.completedOperations == null
            || payload.mealFollowupCooldowns == null
            || payload.mealQualityPolicies == null
            || payload.activeMealPlans == null
            || payload.activeSubstanceUsePlans == null)
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
        HashSet<ConsumableDeliveryId> deliveryIds = new();
        HashSet<MealDeliveryRoute> deliveryRoutes = new();
        HashSet<ConsumableOperationId> completedOperationIds = new();
        HashSet<ConsumableOperationId> activePlanIds = new();
        HashSet<ConsumableOperationId> activeSubstancePlanIds = new();
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
                && IsFiniteNonNegative(delivery.requestedAt);
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
                ? inventory.TryGetMeal(operation.ItemDefinitionId, out _)
                : inventory.TryResolveSubstance(operation.ItemDefinitionId, out _));
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
                || !validItem || !completedOperationIds.Add(operation.OperationId)
                || !IsFiniteNonNegative(operation.completedAt)
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
            if (plan == null
                || !plan.OperationId.IsValid
                || !IsExactValue(plan.planId, plan.OperationId.Value)
                || !IsExactCharacterId(plan.characterId, plan.CharacterId)
                || requireWorldReferences && !characters.Contains(plan.CharacterId)
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
                || !plan.SourceStackId.IsValid
                || !IsExactValue(plan.sourceStackId, plan.SourceStackId.Value)
                || !plan.ItemDefinitionId.IsValid
                || !IsExactValue(
                    plan.itemDefinitionId,
                    plan.ItemDefinitionId.Value)
                || !validMeal
                || plan.phase is not (CharacterMealPlanPhase.Eating
                    or CharacterMealPlanPhase.ItemCommitted
                    or CharacterMealPlanPhase.EffectsPublished)
                || !activePlanIds.Add(plan.OperationId)
                || !completedOperationMatchesPhase
                || !ValidPhysicalCommit(plan, inventory, requireWorldReferences)
                || !IsFiniteNonNegative(plan.createdAt)
                || !IsFiniteNonNegative(plan.leaseExpiresAt)
                || plan.leaseExpiresAt < plan.createdAt
                || !IsFiniteNonNegative(plan.expectedCompletionEta)
                || plan.expectedCompletionEta <= 0f
                || !IsFiniteNonNegative(plan.beginContamination)
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
                && IsFiniteRange(plan.effectToleranceRatio, 0f, 1f);
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
                    or CharacterSubstanceUsePlanPhase.EffectsPublished)
                || !activeSubstancePlanIds.Add(plan.OperationId)
                || activePlanIds.Contains(plan.OperationId)
                || !completedMatches
                || !finiteTargets
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

    internal static CharacterConsumablesAggregateState Build(
        DungeonCharacterConsumablesSaveData payload)
    {
        CharacterConsumablesAggregateState state = new()
        {
            NextOperationSequence = payload.nextOperationSequence,
            NextDeliverySequence = payload.nextDeliverySequence
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

    private static bool ValidPhysicalCommit(
        CharacterMealPlanSaveData plan,
        ICharacterConsumablesInventoryPort inventory,
        bool requirePhysicalJoin)
    {
        bool committed = plan.phase is CharacterMealPlanPhase.ItemCommitted
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
    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    private static bool IsFiniteNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    private static bool InRange(float value, float minimum, float maximum) =>
        IsFiniteNonNegative(value) && value >= minimum && value <= maximum;
}
