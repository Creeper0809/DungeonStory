using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterConsumablesApplicationPorts :
    ICharacterConsumablesWorldPort,
    ICharacterConsumablesInventoryPort,
    ICharacterConsumablesEventPort
{
    private readonly IItemDefinitionCatalog catalog;
    private readonly IWorldItemStackRuntime items;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IGameEventBus events;
    private readonly ICharacterCombatCommandRuntime combatCommands;

    public CharacterConsumablesApplicationPorts(
        IItemDefinitionCatalog catalog,
        IWorldItemStackRuntime items,
        ICharacterAiWorldRegistry world,
        IGameEventBus events,
        ICharacterCombatCommandRuntime combatCommands)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.combatCommands = combatCommands
            ?? throw new ArgumentNullException(nameof(combatCommands));
    }

    public IReadOnlyList<CharacterId> CharacterIds => world.AllCharacters
        .Where(actor => actor != null)
        .Select(CharacterPersistentIdentity.Require)
        .OrderBy(id => id.Value, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<BuildingInstanceId> FacilityIds => world.Buildings
        .Where(building => building != null)
        .Select(building => building.RequirePersistentInstanceId())
        .OrderBy(id => id.Value, StringComparer.Ordinal)
        .ToArray();

    public bool TryGetActor(
        CharacterId id,
        out CharacterConsumablesActorSnapshot snapshot)
    {
        CharacterActor actor = FindActor(id);
        if (actor == null)
        {
            snapshot = default;
            return false;
        }
        snapshot = new CharacterConsumablesActorSnapshot(
            id,
            !actor.IsDead && actor.CurrentLifecycleState == CharacterLifecycleState.Active,
            actor.CurrentHealth,
            actor.MaxHealth,
            actor.Mood.Value,
            GetNeed(actor, CharacterCondition.HUNGER),
            combatCommands.IsInCombatStance(actor));
        return true;
    }

    public bool TryGetFacility(
        BuildingInstanceId id,
        out CharacterConsumablesFacilitySnapshot snapshot)
    {
        BuildableObject facility = FindFacility(id);
        if (facility == null)
        {
            snapshot = default;
            return false;
        }
        snapshot = new CharacterConsumablesFacilitySnapshot(
            id,
            !facility.isDestroy && facility.SupportsFacilityRole(FacilityRole.Meal),
            facility.centerPos);
        return true;
    }

    public void RecoverHunger(CharacterId id, float amount)
    {
        CharacterActor actor = RequireActor(id);
        actor.Stats?.RecoverNeed(
            CharacterCondition.HUNGER,
            amount,
            CharacterNeedRecoverySource.Meal);
    }

    public void ApplyMood(
        CharacterId id,
        string sourceId,
        string label,
        float value,
        float durationSeconds) =>
        RequireActor(id).ApplyMoodFactor(
            sourceId,
            label,
            value,
            durationSeconds,
            1);

    public void ApplyDamage(CharacterId id, float amount, string reason) =>
        RequireActor(id).ApplyDamage(amount, reason);

    public void RecordNeedNarrative(
        CharacterId id,
        string factId,
        string subjectId,
        string outcome,
        float value) =>
        RequireActor(id).Progression?.RecordNarrative(
            CharacterNarrativeDomain.Need,
            factId,
            subjectId,
            outcome,
            value);

    public IReadOnlyList<CharacterConsumablesStackSnapshot> GetAllStacks() =>
        items.GetAllStacks()
            .Where(stack => stack != null)
            .Select(ToSnapshot)
            .OrderBy(stack => stack.StackId.Value, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<CharacterConsumablesSubstanceDefinitionSnapshot> GetSubstances() =>
        catalog.All
            .Where(item => item != null && item.TryGetFeature(out SubstanceItemFeature _))
            .Select(ToSubstanceSnapshot)
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToArray();

    public bool TryGetMeal(
        ConsumableItemDefinitionId id,
        out CharacterConsumablesMealDefinitionSnapshot meal)
    {
        if (id.IsValid
            && catalog.TryGet((ItemDefinitionId)id.Value, out ItemDefinitionSO item)
            && item is ResourceItemDefinitionSO resource
            && resource.TryGetFeature(out FoodItemFeature food))
        {
            meal = ToMealSnapshot(resource, food);
            return true;
        }
        meal = default;
        return false;
    }

    public bool TryResolveSubstance(
        string substanceOrItemId,
        out CharacterConsumablesSubstanceDefinitionSnapshot substance)
    {
        string normalized = substanceOrItemId?.Trim() ?? string.Empty;
        ItemDefinitionId itemId = (ItemDefinitionId)normalized;
        if (itemId.IsValid
            && TryResolveSubstance((ConsumableItemDefinitionId)itemId.Value, out substance))
        {
            return true;
        }
        foreach (ItemDefinitionSO item in catalog.All)
        {
            if (item != null
                && item.TryGetFeature(out SubstanceItemFeature feature)
                && string.Equals(feature.substanceId, normalized, StringComparison.Ordinal))
            {
                substance = ToSubstanceSnapshot(item, feature);
                return true;
            }
        }
        substance = default;
        return false;
    }

    public bool TryResolveSubstance(
        ConsumableItemDefinitionId id,
        out CharacterConsumablesSubstanceDefinitionSnapshot substance)
    {
        if (id.IsValid
            && catalog.TryGet((ItemDefinitionId)id.Value, out ItemDefinitionSO item)
            && item != null
            && item.TryGetFeature(out SubstanceItemFeature feature)
            && !string.IsNullOrWhiteSpace(feature.substanceId))
        {
            substance = ToSubstanceSnapshot(item, feature);
            return true;
        }
        substance = default;
        return false;
    }

    public bool TryConsume(ItemStackId stackId, int quantity) =>
        stackId.IsValid
        && quantity > 0
        && items.TryConsumeStackQuantity(stackId.Value, quantity, out WorldItemStackSnapshot consumed)
        && consumed != null;

    public bool TryRequestDelivery(
        ConsumableItemDefinitionId itemId,
        int quantity,
        Vector2Int position,
        string destinationId,
        out int requested) =>
        items.TryRequestItemDelivery(
            itemId.Value,
            quantity,
            position,
            destinationId,
            out requested,
            out _);

    public void Publish(CharacterConsumablesMealConsumedEvent consumedEvent)
    {
        CharacterActor actor = RequireActor(consumedEvent.CharacterId);
        BuildableObject facility = FindFacility(consumedEvent.FacilityId)
            ?? throw new InvalidOperationException(
                $"Consumables facility '{consumedEvent.FacilityId.Value}' vanished before event publication.");
        events.Publish(new PhysicalMealConsumedEvent(
            consumedEvent.OperationId,
            actor,
            facility,
            MealConsumptionResult.FromCore(consumedEvent.Result)));
    }

    internal static CharacterConsumablesMealDefinitionSnapshot ToMealSnapshot(
        ResourceItemDefinitionSO resource)
    {
        if (resource == null || !resource.TryGetFeature(out FoodItemFeature food))
        {
            return default;
        }
        return ToMealSnapshot(resource, food);
    }

    private static CharacterConsumablesMealDefinitionSnapshot ToMealSnapshot(
        ResourceItemDefinitionSO resource,
        FoodItemFeature food) =>
        new(
            (ConsumableItemDefinitionId)resource.ItemId,
            resource.DisplayName,
            resource.MealDietClass,
            food.quality,
            food.nutrition,
            food.mood,
            resource.UnitPrice,
            (resource.IngredientTags & ResourceIngredientTag.Forbidden) != 0);

    private CharacterActor FindActor(CharacterId id) =>
        id.IsValid
            ? world.AllCharacters.FirstOrDefault(actor => actor != null
                && CharacterPersistentIdentity.Require(actor).Equals(id))
            : null;

    private CharacterActor RequireActor(CharacterId id) =>
        FindActor(id) ?? throw new InvalidOperationException(
            $"Consumables actor '{id.Value}' is unavailable.");

    private BuildableObject FindFacility(BuildingInstanceId id) =>
        id.IsValid
            ? world.Buildings.FirstOrDefault(building => building != null
                && building.RequirePersistentInstanceId().Equals(id))
            : null;

    private CharacterConsumablesStackSnapshot ToSnapshot(
        WorldItemStackSnapshot stack)
    {
        float remaining = 0f;
        float lifetime = 1f;
        bool preserved = false;
        if (catalog.TryGet((ItemDefinitionId)stack.ItemId, out ItemDefinitionSO item)
            && item != null
            && item.TryGetFeature(out FoodItemFeature food))
        {
            remaining = Mathf.Max(0f, food.freshnessSeconds);
            lifetime = Mathf.Max(1f, food.freshnessSeconds);
            preserved = food.preserved;
        }
        ItemInstanceComponentSaveData component = stack.Components?.FirstOrDefault(value =>
            value != null && string.Equals(
                value.componentTypeId,
                ItemInstanceComponentIds.Freshness,
                StringComparison.Ordinal));
        if (component != null)
        {
            ItemStateValueSaveData remainingValue = component.values?.FirstOrDefault(value =>
                value != null && value.key == "remaining-seconds");
            ItemStateValueSaveData preservedValue = component.values?.FirstOrDefault(value =>
                value != null && value.key == "preserved");
            if (remainingValue?.kind == ItemStateValueKind.Decimal)
            {
                remaining = Mathf.Max(0f, (float)remainingValue.decimalValue);
            }
            if (preservedValue?.kind == ItemStateValueKind.Boolean)
            {
                preserved = preservedValue.booleanValue;
            }
        }
        return new CharacterConsumablesStackSnapshot(
            (ItemStackId)stack.StackId,
            (ConsumableItemDefinitionId)stack.ItemId,
            stack.Quantity,
            ToStackState(stack.State),
            stack.DestinationId,
            stack.Forbidden,
            stack.IsReserved,
            stack.Contamination,
            remaining / Mathf.Max(1f, lifetime),
            remaining,
            preserved);
    }

    private CharacterConsumablesSubstanceDefinitionSnapshot ToSubstanceSnapshot(
        ItemDefinitionSO item)
    {
        if (!item.TryGetFeature(out SubstanceItemFeature feature))
        {
            throw new InvalidOperationException(
                $"Item '{item.ItemId}' has no substance feature.");
        }
        return ToSubstanceSnapshot(item, feature);
    }

    private static CharacterConsumablesSubstanceDefinitionSnapshot ToSubstanceSnapshot(
        ItemDefinitionSO item,
        SubstanceItemFeature feature) =>
        new(
            (ConsumableItemDefinitionId)item.ItemId,
            new SubstanceDefinitionView(
                feature.substanceId,
                item.ItemId,
                item.DisplayName,
                feature.useClass,
                feature.addictionChance,
                feature.overdoseChance,
                feature.toleranceGain,
                feature.withdrawalPerHour,
                feature.moodEffect,
                feature.workSpeedEffect,
                feature.combatEffect,
                feature.durationSeconds,
                (item as ResourceItemDefinitionSO)?.RequiredResearchId
                    ?? string.Empty));

    private static CharacterConsumablesStackState ToStackState(WorldItemStackState state) =>
        state switch
        {
            WorldItemStackState.Loose => CharacterConsumablesStackState.Loose,
            WorldItemStackState.Stored => CharacterConsumablesStackState.Stored,
            WorldItemStackState.Carried => CharacterConsumablesStackState.Carried,
            WorldItemStackState.FacilityBuffer => CharacterConsumablesStackState.FacilityBuffer,
            _ => CharacterConsumablesStackState.Other
        };

    private static float GetNeed(CharacterActor actor, CharacterCondition condition) =>
        actor?.Stats?.Stats != null
        && actor.Stats.Stats.TryGetValue(condition, out float value)
            ? value
            : 100f;
}

public sealed class CharacterConsumablesCompatibilityAdapter :
    ICharacterConsumablesQuery,
    ICharacterConsumablesCommand,
    ICharacterDietPolicyRuntime,
    IMealConsumptionRuntime,
    ICharacterSubstanceRuntime,
    ITickable
{
    private readonly CharacterConsumablesRuntime runtime;

    public CharacterConsumablesCompatibilityAdapter(CharacterConsumablesRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public CharacterDietPolicyKind GetPolicy(CharacterActor actor) =>
        runtime.GetDietPolicy(GetCharacterId(actor));

    public void SetPolicy(CharacterActor actor, CharacterDietPolicyKind policy) =>
        runtime.SetDietPolicy(GetCharacterId(actor), policy);

    public bool IsAllowed(CharacterActor actor, ResourceItemDefinitionSO meal) =>
        meal != null && runtime.IsMealAllowed(
            GetCharacterId(actor),
            CharacterConsumablesApplicationPorts.ToMealSnapshot(meal));

    public bool HasMealAvailable(
        CharacterActor actor,
        BuildableObject facility,
        out CharacterConsumablesFailure failure) =>
        runtime.HasMealAvailable(
            GetCharacterId(actor),
            GetFacilityId(facility),
            out failure);

    public bool TryConsumeMeal(
        CharacterActor actor,
        BuildableObject facility,
        out MealConsumptionResult result)
    {
        bool success = runtime.TryConsumeMeal(
            GetCharacterId(actor),
            GetFacilityId(facility),
            out CharacterConsumablesMealResult coreResult);
        result = MealConsumptionResult.FromCore(coreResult);
        return success;
    }

    public bool TryConsumeMeal(
        ConsumeMealCommand command,
        out MealConsumptionResult result)
    {
        bool success = runtime.TryConsumeMeal(command, out CharacterConsumablesMealResult coreResult);
        result = MealConsumptionResult.FromCore(coreResult);
        return success;
    }

    public CharacterSubstancePolicyState GetPolicy(
        CharacterActor actor,
        string substanceId) =>
        runtime.GetSubstancePolicy(GetCharacterId(actor), substanceId);

    public void SetPolicy(
        CharacterActor actor,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold = 30f,
        int scheduledHour = 20) =>
        runtime.SetSubstancePolicy(
            GetCharacterId(actor),
            substanceId,
            mode,
            moodThreshold,
            scheduledHour);

    public CharacterSubstanceState GetState(
        CharacterActor actor,
        string substanceId) =>
        runtime.GetSubstanceState(GetCharacterId(actor), substanceId);

    public bool TryConsume(
        CharacterActor actor,
        string substanceId,
        bool medicalContext,
        bool combatContext,
        out SubstanceUseResult result)
    {
        bool success = runtime.TryConsumeSubstance(
            GetCharacterId(actor),
            substanceId,
            medicalContext,
            combatContext,
            out CharacterConsumablesSubstanceResult coreResult);
        result = SubstanceUseResult.FromCore(coreResult);
        return success;
    }

    public bool TryConsume(
        ConsumeSubstanceCommand command,
        out SubstanceUseResult result)
    {
        bool success = runtime.TryConsumeSubstance(
            new ConsumeSubstanceByIdCommand(
                command.OperationId,
                command.CharacterId,
                (ConsumableItemDefinitionId)command.ItemDefinitionId.Value,
                command.ItemStackId,
                command.MedicalContext,
                command.CombatContext),
            out CharacterConsumablesSubstanceResult coreResult);
        result = SubstanceUseResult.FromCore(coreResult);
        return success;
    }

    public bool TryGetAutomaticUseRequest(
        CharacterActor actor,
        out CharacterSubstanceUseRequest request)
    {
        if (!runtime.TryGetAutomaticUseRequest(
                GetCharacterId(actor),
                out CharacterConsumablesUseRequest coreRequest))
        {
            request = default;
            return false;
        }
        request = new CharacterSubstanceUseRequest(
            coreRequest.Substance.Definition.SubstanceId,
            (ItemDefinitionId)coreRequest.Substance.Id.Value,
            coreRequest.Substance.Definition.DisplayName,
            coreRequest.Urgency,
            coreRequest.MedicalContext,
            coreRequest.CombatContext,
            coreRequest.Reason);
        return true;
    }

    public float GetWorkSpeedMultiplier(CharacterActor actor) =>
        runtime.GetWorkSpeedMultiplier(GetCharacterId(actor));
    public float GetCombatMultiplier(CharacterActor actor) =>
        runtime.GetCombatMultiplier(GetCharacterId(actor));
    public void Tick() => runtime.Tick();

    private static CharacterId GetCharacterId(CharacterActor actor) =>
        actor == null ? default : CharacterPersistentIdentity.Require(actor);
    private static BuildingInstanceId GetFacilityId(BuildableObject facility) =>
        facility == null ? default : facility.RequirePersistentInstanceId();
}
