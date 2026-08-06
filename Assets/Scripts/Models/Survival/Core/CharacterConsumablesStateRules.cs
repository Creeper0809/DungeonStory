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
        return clone;
    }
}

internal static class CharacterConsumablesStateRules
{
    internal static CharacterDietPolicyState Clone(CharacterDietPolicyState source) =>
        new()
        {
            characterId = source?.characterId ?? string.Empty,
            policy = source?.policy ?? CharacterDietPolicyKind.Free
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
            substancePolicies = state.SubstancePolicies.Values.Select(Clone)
                .OrderBy(value => value.characterId, StringComparer.Ordinal)
                .ThenBy(value => value.itemDefinitionId, StringComparer.Ordinal).ToList(),
            substanceStates = state.SubstanceStates.Values.Select(Clone)
                .OrderBy(value => value.characterId, StringComparer.Ordinal)
                .ThenBy(value => value.itemDefinitionId, StringComparer.Ordinal).ToList(),
            pendingMealDeliveries = state.PendingDeliveries.Values.Select(Clone)
                .OrderBy(value => value.deliveryId, StringComparer.Ordinal).ToList(),
            completedOperations = state.CompletedOperations.Values.Select(Clone)
                .OrderBy(value => value.operationId, StringComparer.Ordinal).ToList()
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
        ICharacterConsumablesInventoryPort inventory)
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
            || payload.completedOperations == null)
        {
            report.AddError("Character consumables payload contains a null collection.");
            return;
        }

        ValidateSequenceWatermarks(payload, report);

        HashSet<CharacterId> characters = world.CharacterIds.Where(id => id.IsValid).ToHashSet();
        HashSet<BuildingInstanceId> facilities = world.FacilityIds.Where(id => id.IsValid).ToHashSet();
        HashSet<CharacterId> dietIds = new();
        HashSet<CharacterSubstanceKey> policyKeys = new();
        HashSet<CharacterSubstanceKey> stateKeys = new();
        HashSet<ConsumableDeliveryId> deliveryIds = new();
        HashSet<MealDeliveryRoute> deliveryRoutes = new();
        HashSet<ConsumableOperationId> operationIds = new();

        string previous = null;
        foreach (CharacterDietPolicyState state in payload.dietPolicies)
        {
            if (state == null || !IsExactCharacterId(state.characterId, state.CharacterId)
                || !characters.Contains(state.CharacterId)
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
        foreach (CharacterSubstancePolicyState state in payload.substancePolicies)
        {
            CharacterSubstanceKey key = state == null ? default : new(state.CharacterId, state.ItemDefinitionId);
            string orderKey = state == null ? string.Empty : state.characterId + "\n" + state.itemDefinitionId;
            if (state == null || !IsExactCharacterId(state.characterId, state.CharacterId)
                || !IsExactValue(state.itemDefinitionId, state.ItemDefinitionId.Value)
                || !ValidSubstance(state.CharacterId, state.ItemDefinitionId, characters, inventory)
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
                || !ValidSubstance(state.CharacterId, state.ItemDefinitionId, characters, inventory)
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
            if (delivery == null
                || !delivery.DeliveryId.IsValid
                || !IsExactValue(delivery.deliveryId, delivery.DeliveryId.Value)
                || !IsExactCharacterId(delivery.characterId, delivery.CharacterId)
                || !delivery.BuildingInstanceId.IsValid
                || !IsExactValue(
                    delivery.buildingInstanceId,
                    delivery.BuildingInstanceId.Value)
                || !delivery.ItemDefinitionId.IsValid
                || !IsExactValue(
                    delivery.itemDefinitionId,
                    delivery.ItemDefinitionId.Value)
                || !characters.Contains(delivery.CharacterId)
                || !facilities.Contains(delivery.BuildingInstanceId)
                || !inventory.TryGetMeal(delivery.ItemDefinitionId, out _)
                || !deliveryIds.Add(delivery.DeliveryId) || !deliveryRoutes.Add(route)
                || !IsFiniteNonNegative(delivery.requestedAt)
                || !IsFiniteNonNegative(delivery.retryAfter)
                || delivery.retryAfter < delivery.requestedAt
                || !IsAfter(previous, delivery.deliveryId))
            {
                report.AddError("Character consumables deliveries contain an invalid, unknown, duplicate, or unordered delivery ID/reference.");
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
                || !characters.Contains(operation.CharacterId)
                || !validItem || !operationIds.Add(operation.OperationId)
                || !IsFiniteNonNegative(operation.completedAt)
                || !IsAfter(previous, operation.operationId))
            {
                report.AddError("Character consumables operation ledger contains an invalid, unknown, duplicate, or unordered operation/reference.");
                break;
            }
            previous = operation.operationId;
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
        return state;
    }

    private static bool ValidSubstance(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        ISet<CharacterId> characters,
        ICharacterConsumablesInventoryPort inventory) =>
        characterId.IsValid && characters.Contains(characterId) && itemId.IsValid
        && inventory.TryResolveSubstance(itemId, out _);

    private static bool IsAfter(string previous, string value) =>
        value != null && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && (previous == null || string.CompareOrdinal(previous, value) < 0);
    private static bool IsExactCharacterId(string raw, CharacterId id) =>
        id.IsValid
        && string.Equals(id.Value, raw ?? string.Empty, StringComparison.Ordinal);
    private static bool IsExactValue(string raw, string typedValue) =>
        string.Equals(typedValue, raw ?? string.Empty, StringComparison.Ordinal);

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
    private static bool InRange(float value, float minimum, float maximum) =>
        IsFiniteNonNegative(value) && value >= minimum && value <= maximum;
}
