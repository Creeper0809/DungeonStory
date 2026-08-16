using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterConsumablesApplicationPorts :
    ICharacterConsumablesWorldPort,
    ICharacterConsumablesInventoryPort,
    ICharacterConsumablesEventPort,
    ICharacterRitualFastingMealPort
{
    private readonly IItemDefinitionCatalog catalog;
    private readonly IWorldItemStackRuntime items;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IGameEventBus events;
    private readonly ICharacterCombatStanceQuery combatStance;
    private readonly ICharacterPerformanceQuery performance;
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly ICharacterRitualFastingQuery ritualFastingQuery;
    private readonly ICharacterRitualFastingCommand ritualFastingCommand;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IReservedItemTransferService reservedTransfers;
    private readonly Dictionary<string, HashSet<string>> mealFacilitySlotOwners =
        new(StringComparer.Ordinal);

    public CharacterConsumablesApplicationPorts(
        IItemDefinitionCatalog catalog,
        IWorldItemStackRuntime items,
        ICharacterAiWorldRegistry world,
        IGameEventBus events,
        ICharacterCombatStanceQuery combatStance,
        ICharacterPerformanceQuery performance,
        ICharacterNarrativeQuery narratives = null,
        ICharacterNarrativeCatalog narrativeCatalog = null,
        ICharacterRitualFastingQuery ritualFastingQuery = null,
        ICharacterRitualFastingCommand ritualFastingCommand = null,
        IItemQuantityReservationService quantityReservations = null,
        IReservedItemTransferService reservedTransfers = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.combatStance = combatStance
            ?? throw new ArgumentNullException(nameof(combatStance));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.narratives = narratives;
        this.narrativeCatalog = narrativeCatalog;
        this.ritualFastingQuery = ritualFastingQuery;
        this.ritualFastingCommand = ritualFastingCommand;
        this.quantityReservations = quantityReservations;
        this.reservedTransfers = reservedTransfers;
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
            combatStance.IsInCombatStance(actor),
            actor.GetNowXY());
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
            !facility.isDestroy
                && facility.BuildingData?
                    .GetAbility<BuildingRecreationalSubstanceServiceAbility>()?
                    .IsValid == true,
            facility.centerPos);
        return true;
    }

    public CharacterCultureMealPreference GetCultureMealPreference(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId)
    {
        if (narratives == null
            || narrativeCatalog == null
            || !characterId.IsValid
            || !itemId.IsValid
            || !narratives.TryGet(
                characterId,
                out CharacterNarrativeSnapshot narrative))
        {
            return CharacterCultureMealPreference.Neutral;
        }
        SpeciesCultureDefinitionSO culture = narrativeCatalog.Require(
            narrative.CultureId);
        if ((culture.forbiddenItemIds ?? new List<string>()).Contains(
                itemId.Value,
                StringComparer.Ordinal))
        {
            return CharacterCultureMealPreference.Forbidden;
        }
        return (culture.preferredItemIds ?? new List<string>()).Contains(
                itemId.Value,
                StringComparer.Ordinal)
            ? CharacterCultureMealPreference.Preferred
            : CharacterCultureMealPreference.Neutral;
    }

    public CharacterMealRouteStatus GetMealRouteStatus(
        CharacterId characterId,
        Vector2Int from,
        Vector2Int to,
        out float travelSeconds)
    {
        travelSeconds = 0f;
        CharacterActor actor = FindActor(characterId);
        IGridPathSearchBroker broker = actor?.PathSearchBroker;
        if (actor == null
            || broker == null
            || !world.TryGetGrid(out Grid grid)
            || grid == null)
        {
            return CharacterMealRouteStatus.Unreachable;
        }

        GridPathRequestStatus status = broker.RequestMovePathTo(
            grid,
            from,
            to,
            out Queue<GridMoveStep> path,
            GridPathSearchPriority.Normal);
        if (status == GridPathRequestStatus.Reachable)
            travelSeconds = path?.Count ?? 0f;
        return status switch
        {
            GridPathRequestStatus.Pending => CharacterMealRouteStatus.Pending,
            GridPathRequestStatus.Reachable => CharacterMealRouteStatus.Reachable,
            _ => CharacterMealRouteStatus.Unreachable
        };
    }

    public float ProjectGameplayEffect(
        CharacterId characterId,
        string targetId,
        float baseValue)
    {
        CharacterActor actor = FindActor(characterId);
        if (actor != null
            && string.Equals(
                targetId,
                GameplayEffectTargetIds.FoodPoisoningChance,
                StringComparison.Ordinal))
        {
            return baseValue * performance.Evaluate(
                actor,
                "performance:survival:food-poisoning").Value;
        }
        return actor != null
            ? actor.ProjectDetailedStat(targetId, baseValue).Value
            : baseValue;
    }

    public float GetBehaviorUtilityMultiplier(
        CharacterId characterId,
        IReadOnlyCollection<string> semanticTags)
    {
        CharacterActor actor = FindActor(characterId);
        CharacterRuntimeProfile profile = actor?.Progression != null
            ? actor.Progression.GetEffectiveRuntimeProfile()
            : actor?.Identity?.Profile;
        return profile?.GetBehaviorUtilityMultiplier(semanticTags) ?? 1f;
    }

    public float GetBaseMoodForMealChoice(CharacterId characterId)
    {
        CharacterMoodSnapshot mood = RequireActor(characterId).Stats.GetMoodSnapshot();
        float mealOffset = mood.Factors
            .Where(factor => factor != null
                && string.Equals(
                    factor.Id,
                    "meal:best-active",
                    StringComparison.Ordinal))
            .Sum(factor => factor.Value);
        return Mathf.Clamp(mood.Value - mealOffset, 0f, 100f);
    }

    public bool TryReserveMealFacilitySlot(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId)
    {
        if (!operationId.IsValid || !characterId.IsValid || !facilityId.IsValid)
            return false;
        string facility = facilityId.Value;
        if (!mealFacilitySlotOwners.TryGetValue(
                facility,
                out HashSet<string> owners))
        {
            owners = new HashSet<string>(StringComparer.Ordinal);
            mealFacilitySlotOwners.Add(facility, owners);
        }

        if (owners.Contains(operationId.Value))
            return true;

        BuildableObject building = FindFacility(facilityId);
        int capacity = Mathf.Max(1, building?.EffectiveCapacity ?? 1);
        return owners.Count < capacity && owners.Add(operationId.Value);
    }

    public void ReleaseMealFacilitySlot(
        ConsumableOperationId operationId,
        BuildingInstanceId facilityId)
    {
        if (facilityId.IsValid
            && mealFacilitySlotOwners.TryGetValue(
                facilityId.Value,
                out HashSet<string> owners)
            && owners.Remove(operationId.Value)
            && owners.Count == 0)
        {
            mealFacilitySlotOwners.Remove(facilityId.Value);
        }
    }

    public void ApplyBestMealMood(
        CharacterId characterId,
        string label,
        float value,
        float durationSeconds)
    {
        CharacterActor actor = RequireActor(characterId);
        CharacterMoodFactorSnapshot active = actor.Stats.GetMoodSnapshot().Factors
            .FirstOrDefault(factor => factor != null
                && string.Equals(
                    factor.Id,
                    "meal:best-active",
                    StringComparison.Ordinal));
        if (active != null && active.Value > value)
            return;
        actor.ApplyMoodFactor(
            "meal:best-active",
            label,
            value,
            durationSeconds,
            1);
    }

    public bool IsRitualFasting(CharacterId characterId)
    {
        CharacterActor actor = FindActor(characterId);
        return actor != null
            && ritualFastingQuery?.GetStatus(actor).Phase
                == CharacterRitualFastPhase.Fasting;
    }

    public void RecordMealConsumed(
        CharacterId characterId,
        bool directPlayerOrder)
    {
        CharacterActor actor = RequireActor(characterId);
        ritualFastingCommand?.RecordMealConsumed(actor, directPlayerOrder);
    }

    public void RecoverHunger(CharacterId id, float amount)
    {
        CharacterActor actor = RequireActor(id);
        CharacterPerformanceSnapshot nutrition = performance.Evaluate(
            actor,
            CharacterPerformanceFormulaIds.NutritionEfficiency);
        if (!nutrition.IsApplicable)
            throw new InvalidOperationException(
                nutrition.Failure?.Message ?? "Nutrition efficiency is unavailable.");
        float recovered = amount * nutrition.Value;
        actor.Stats?.RecoverNeed(
            CharacterCondition.HUNGER,
            recovered,
            CharacterNeedRecoverySource.Meal);
        CharacterPerformanceExecutionTrace.Record(
            CharacterPerformanceFormulaIds.NutritionEfficiency,
            "CharacterConsumablesApplicationPorts.RecoverHunger",
            amount,
            recovered,
            id.Value);
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

    public bool TryConsumeForCharacter(
        CharacterId characterId,
        ItemStackId stackId,
        int quantity)
    {
        if (!stackId.IsValid || quantity <= 0)
            return false;
        WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.StackId, stackId.Value, StringComparison.Ordinal));
        if (stack == null)
            return false;
        if (stack.State != WorldItemStackState.Carried)
            return TryConsume(stackId, quantity);

        CharacterActor actor = FindActor(characterId);
        CharacterCarryInventory carry = actor != null
            ? CharacterCarryInventory.Ensure(actor)
            : null;
        if (carry == null
            || carry.Items.Where(item => item != null
                    && string.Equals(
                        item.carriedStackId,
                        stackId.Value,
                        StringComparison.Ordinal)
                    && string.Equals(item.itemId, stack.ItemId, StringComparison.Ordinal))
                .Sum(item => item.quantity) < quantity)
        {
            return false;
        }

        if (!TryConsume(stackId, quantity))
            return false;
        if (!carry.TryConsumeCarriedStack(stackId.Value, stack.ItemId, quantity))
        {
            throw new InvalidOperationException(
                $"Carried stack '{stackId.Value}' was consumed physically but remained in character '{characterId.Value}' inventory.");
        }
        return true;
    }

    public bool TryReserveMealQuantity(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        ItemStackId stackId,
        out string leaseId)
    {
        leaseId = string.Empty;
        if (quantityReservations == null)
            return true;
        WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(value =>
            value != null && string.Equals(
                value.StackId,
                stackId.Value,
                StringComparison.Ordinal));
        if (stack == null)
            return false;
        if (!quantityReservations.TryReserve(
                operationId.Value,
                characterId.Value,
                ItemReservationPurpose.Meal,
                $"meal:{facilityId.Value}:{stack.ItemId}",
                new ItemQuantityReservationRequest(
                    stackId,
                    1,
                    stack.ReservationSignature),
                out ItemQuantityLease lease,
                out _))
        {
            return false;
        }
        leaseId = lease.leaseId;
        return true;
    }

    public bool RevalidateMealQuantity(string leaseId, ItemStackId stackId)
    {
        if (quantityReservations == null)
            return string.IsNullOrWhiteSpace(leaseId);
        return quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out _)
            && lease.slices.Any(slice => slice != null
                && slice.quantity >= 1);
    }

    public bool TryResolveMealQuantityStack(
        string leaseId,
        out ItemStackId stackId)
    {
        stackId = default;
        if (quantityReservations == null
            || !quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out _))
        {
            return false;
        }
        ItemLeaseSlice slice = lease.slices
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (slice == null)
            return false;
        stackId = new ItemStackId(slice.stackId);
        return stackId.IsValid;
    }

    public bool TryRebindMealQuantityLease(
        ConsumableOperationId operationId,
        out string leaseId,
        out ItemStackId stackId)
    {
        leaseId = string.Empty;
        stackId = default;
        if (quantityReservations == null
            || !operationId.IsValid
            || !quantityReservations.TryGetLeasesByOwner(
                operationId.Value,
                out IReadOnlyList<ItemQuantityLease> leases))
        {
            return false;
        }
        ItemQuantityLease lease = leases
            .Where(value => value != null
                && value.purpose == ItemReservationPurpose.Meal
                && value.remainingQuantity >= 1)
            .OrderBy(value => value.leaseId, StringComparer.Ordinal)
            .FirstOrDefault();
        ItemLeaseSlice slice = lease?.slices
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (lease == null || slice == null)
            return false;
        leaseId = lease.leaseId;
        stackId = new ItemStackId(slice.stackId);
        return stackId.IsValid;
    }

    public bool TryConsumeReservedMealQuantity(
        string leaseId,
        ItemStackId stackId,
        int quantity)
    {
        if (quantityReservations == null || reservedTransfers == null)
            return string.IsNullOrWhiteSpace(leaseId) && TryConsume(stackId, quantity);
        return reservedTransfers.TryConsumeReservedQuantity(
            leaseId,
            quantity,
            out _);
    }

    public void ReleaseMealQuantity(string leaseId)
    {
        if (quantityReservations != null && !string.IsNullOrWhiteSpace(leaseId))
        {
            quantityReservations.Release(
                leaseId,
                ItemReservationReleaseReason.Cancelled);
        }
    }

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
        BuildableObject facility = string.Equals(
                consumedEvent.FacilityId.Value,
                CharacterConsumablesRuntime.FieldMealFacilityId,
                StringComparison.Ordinal)
            ? null
            : FindFacility(consumedEvent.FacilityId)
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
            (resource.IngredientTags & ResourceIngredientTag.Forbidden) != 0,
            (resource.IngredientTags & ResourceIngredientTag.Sweet) != 0,
            (resource.IngredientTags & ResourceIngredientTag.Salted) != 0,
            food.qualityBand,
            food.servingRole);

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
            stack.ReservedQuantity,
            stack.Contamination,
            remaining / Mathf.Max(1f, lifetime),
            remaining,
            preserved,
            stack.Position);
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
    IFieldMealConsumptionCommand,
    ICharacterDietPolicyRuntime,
    IMealConsumptionRuntime,
    ICharacterMealOperationCancellation,
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

    public int CancelActiveMealOperations(CharacterActor actor, string reason) =>
        runtime.CancelActiveMealOperations(
            GetCharacterId(actor),
            reason);

    public bool TryFindFieldMeal(
        CharacterActor actor,
        out ItemStackId stackId,
        out Vector2Int position,
        out CharacterConsumablesFailure failure) =>
        runtime.TryFindFieldMeal(
            GetCharacterId(actor),
            out stackId,
            out position,
            out failure);

    public bool TryConsumeFieldMeal(
        CharacterActor actor,
        ItemStackId stackId,
        out MealConsumptionResult result)
    {
        bool success = runtime.TryConsumeFieldMeal(
            GetCharacterId(actor),
            stackId,
            out CharacterConsumablesMealResult coreResult);
        result = MealConsumptionResult.FromCore(coreResult);
        return success;
    }

    public bool TryGetMealOperationResult(
        ConsumableOperationId operationId,
        out MealConsumptionResult result)
    {
        bool found = runtime.TryGetMealOperationResult(
            operationId,
            out CharacterConsumablesMealResult coreResult);
        result = MealConsumptionResult.FromCore(coreResult);
        return found;
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

    public bool TryConsumeAtFacility(
        CharacterActor actor,
        BuildableObject facility,
        out SubstanceUseResult result)
    {
        bool success = runtime.TryConsumeRecreationalSubstance(
            GetCharacterId(actor),
            GetFacilityId(facility),
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
            coreRequest.Substance.Definition.UseClass,
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
