using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class CharacterConsumablesRuntime :
    ICharacterConsumablesApplication,
    ICharacterConsumablesPersistence
{
    public const string FieldMealFacilityId = "building:primitive-field-meal";
    public const string MealPhysicalSinkReason = "character-meal-consumed";
    public const string SubstancePhysicalSinkReason =
        "character-substance-consumed";
    private const float MealFollowupCooldownSeconds = 15f;
    private const float MealActionSeconds = 4f;
    private const float DeliveryRetrySeconds = 45f;
    private const float MealDeliveryProbeSeconds = 1f;
    private const float GameHourSeconds = 60f;
    private const float MedicalUseHealthRatio = 0.82f;

    private readonly struct MealOperationFailureState
    {
        internal MealOperationFailureState(
            CharacterConsumablesFailureCode code,
            string detail)
        {
            Code = code;
            Detail = detail ?? string.Empty;
        }

        internal CharacterConsumablesFailureCode Code { get; }
        internal string Detail { get; }
    }

    private readonly ICharacterConsumablesWorldPort world;
    private readonly ICharacterConsumablesInventoryPort inventory;
    private readonly ICharacterConsumablesEventPort events;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICharacterNeedBalanceRuntime needBalance;
    private readonly ICharacterConsumablesWorkforcePort workforce;
    private readonly Dictionary<ConsumableOperationId, MealOperationFailureState>
        mealOperationFailures = new();
    private readonly Queue<ConsumableOperationId> mealOperationFailureOrder = new();
    private readonly HashSet<ConsumableOperationId> queuedMealOperationFailures = new();
    private const int MaximumRememberedMealOperationFailures = 64;
    private float nextMealDeliveryProbeAt;
#if UNITY_EDITOR
    private bool mealDeliveryPausedForDiagnostics;

    public bool MealDeliveryPausedForDiagnostics =>
        mealDeliveryPausedForDiagnostics;

    public void ConfigureMealDeliveryForDiagnostics(bool paused)
    {
        mealDeliveryPausedForDiagnostics = paused;
    }
#endif
    public long MealDeliveryProbeCount { get; private set; }
    public string LastMealDeliveryProbeDetail { get; private set; } = "not-run";
    public string LastMealDeliveryRequestFailure { get; private set; } = "not-run";

    private float RoutineHungerThreshold => needBalance
        .GetResponse(CharacterCondition.HUNGER)
        .routineStart;
    private float EmergencyHungerThreshold => needBalance
        .GetResponse(CharacterCondition.HUNGER)
        .emergencyStart;

    private CharacterConsumablesAggregateState ReadState =>
        aggregateRootStore.GetOrCreate(() => new CharacterConsumablesAggregateState());
    private CharacterConsumablesAggregateState WriteState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new CharacterConsumablesAggregateState(),
            state => state.Clone());

    public CharacterConsumablesRuntime(
        ICharacterConsumablesWorldPort world,
        ICharacterConsumablesInventoryPort inventory,
        ICharacterConsumablesEventPort events,
        IGameClock clock,
        IRandomStreamProvider randomStreams,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterNeedBalanceRuntime needBalance,
        ICharacterConsumablesWorkforcePort workforce = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.needBalance = needBalance
            ?? throw new ArgumentNullException(nameof(needBalance));
        this.workforce = workforce;
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("character-consumables");
    }

    public CharacterDietPolicyKind GetDietPolicy(CharacterId characterId) =>
        characterId.IsValid
        && ReadState.DietPolicies.TryGetValue(characterId, out CharacterDietPolicyState state)
            ? state.policy
            : CharacterDietPolicyKind.Free;

    public void SetDietPolicy(CharacterId characterId, CharacterDietPolicyKind policy)
    {
        if (!characterId.IsValid)
        {
            return;
        }
        WriteState.DietPolicies[characterId] = new CharacterDietPolicyState
        {
            characterId = characterId.Value,
            policy = policy
        };
    }

    public CharacterMealQualityLimit GetMealQualityLimit(CharacterId characterId) =>
        characterId.IsValid
        && ReadState.MealQualityPolicies.TryGetValue(
            characterId,
            out CharacterMealQualityPolicyState state)
            ? state.maximumQuality
            : CharacterMealQualityLimit.Inherit;

    public void SetMealQualityLimit(
        CharacterId characterId,
        CharacterMealQualityLimit qualityLimit)
    {
        if (!characterId.IsValid
            || !Enum.IsDefined(typeof(CharacterMealQualityLimit), qualityLimit))
        {
            return;
        }
        WriteState.MealQualityPolicies[characterId] =
            new CharacterMealQualityPolicyState
            {
                characterId = characterId.Value,
                maximumQuality = qualityLimit
            };
    }

    public bool IsMealAllowed(
        CharacterId characterId,
        CharacterConsumablesMealDefinitionSnapshot meal) =>
        meal.Id.IsValid
        && world.GetCultureMealPreference(characterId, meal.Id)
            != CharacterCultureMealPreference.Forbidden
        && CharacterConsumablesPolicyRules.AllowsMeal(
            GetDietPolicy(characterId),
            meal.DietClass,
            meal.ForbiddenIngredient);

    public bool HasMealAvailable(
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesFailure failure)
    {
        failure = CharacterConsumablesFailure.None;
        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
            || !TryGetMealFacility(facilityId, out _))
        {
            failure = new CharacterConsumablesFailure(
                !actor.Id.IsValid
                    ? CharacterConsumablesFailureCode.CharacterMissing
                    : CharacterConsumablesFailureCode.FacilityMissing);
            return false;
        }

        bool emergency = actor.Hunger <= EmergencyHungerThreshold;
        if (IsMealFollowupCooldownActive(actor) && !emergency)
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.PolicyForbidden,
                "meal-followup-cooldown");
            return false;
        }
        if (GetMealCandidates(
                actor,
                facilityId,
                true,
                emergency,
                out _,
                requireExactRoute: false).Count > 0)
        {
            return true;
        }
        if (GetMealCandidates(
                actor,
                facilityId,
                false,
                emergency,
                out bool sourceRoutePending).Count > 0
            || sourceRoutePending)
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.DeliveryPending,
                characterId.Value,
                facilityId.Value);
            return true;
        }
        failure = new CharacterConsumablesFailure(
            emergency
                ? CharacterConsumablesFailureCode.ItemStackMissing
                : CharacterConsumablesFailureCode.PolicyForbidden,
            facilityId.Value);
        return false;
    }

    public bool TryFindFieldMeal(
        CharacterId characterId,
        out ItemStackId stackId,
        out Vector2Int position,
        out CharacterConsumablesFailure failure)
    {
        stackId = default;
        position = default;
        failure = CharacterConsumablesFailure.None;
        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
            || !actor.Active)
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.CharacterMissing,
                characterId.Value);
            return false;
        }
        if (world is ICharacterRitualFastingMealPort fasting
            && fasting.IsRitualFasting(characterId))
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId.Value,
                "ritual-fast");
            return false;
        }
        if (actor.Hunger > RoutineHungerThreshold)
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId.Value,
                "not-hungry");
            return false;
        }
        bool emergency = actor.Hunger <= EmergencyHungerThreshold;
        if (IsMealFollowupCooldownActive(actor) && !emergency)
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId.Value,
                "meal-followup-cooldown");
            return false;
        }

        CharacterMealQualityLimit authoredLimit = GetMealQualityLimit(actor.Id);
        MealQualityBand maximumQuality = authoredLimit == CharacterMealQualityLimit.Inherit
            ? MealQualityBand.Fine
            : (MealQualityBand)(int)authoredLimit;
        float baseMood = world.GetBaseMoodForMealChoice(actor.Id);
        CharacterConsumablesStackSnapshot selected = inventory.GetAllStacks()
            .Where(stack => stack.AvailableQuantity > 0
                && !stack.Forbidden
                && stack.State is CharacterConsumablesStackState.Loose
                    or CharacterConsumablesStackState.Stored
                && stack.RemainingFreshnessSeconds > MealActionSeconds + 0.25f
                && inventory.TryGetMeal(
                    stack.ItemId,
                    out CharacterConsumablesMealDefinitionSnapshot meal)
                && (meal.ServingRole != MealServingRole.EmergencyOnly
                    || emergency))
            .Where(stack =>
            {
                inventory.TryGetMeal(stack.ItemId, out CharacterConsumablesMealDefinitionSnapshot meal);
                return meal.QualityBand <= maximumQuality
                    && (emergency || IsMealAllowed(actor.Id, meal)
                        && stack.Contamination <= 0.01f);
            })
            .OrderByDescending(stack =>
            {
                inventory.TryGetMeal(stack.ItemId, out CharacterConsumablesMealDefinitionSnapshot meal);
                float quality = (baseMood < 35f ? 1f : -1f)
                    * (int)meal.QualityBand * 100f;
                float culture = world.GetCultureMealPreference(actor.Id, meal.Id)
                    == CharacterCultureMealPreference.Preferred ? 1000f : 0f;
                return culture + quality + meal.Nutrition + meal.Mood * 2f;
            })
            .ThenBy(stack => ManhattanSeconds(actor.Position, stack.Position))
            .ThenBy(stack => stack.StackId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (!selected.StackId.IsValid)
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.ItemStackMissing,
                characterId.Value,
                "field-meal");
            return false;
        }

        stackId = selected.StackId;
        position = selected.Position;
        return true;
    }

    public bool TryConsumeFieldMeal(
        CharacterId characterId,
        ItemStackId stackId,
        out CharacterConsumablesMealResult result)
    {
        ConsumableOperationId operationId = NewOperationId();
        BuildingInstanceId fieldFacility = new(FieldMealFacilityId);
        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
            || !actor.Active)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.CharacterMissing,
                characterId.Value);
            return false;
        }
        if (world is ICharacterRitualFastingMealPort fasting
            && fasting.IsRitualFasting(characterId))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId.Value,
                "ritual-fast");
            return false;
        }
        bool emergency = actor.Hunger <= EmergencyHungerThreshold;
        if (actor.Hunger > RoutineHungerThreshold
            || IsMealFollowupCooldownActive(actor) && !emergency)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId.Value,
                "field-meal-not-needed");
            return false;
        }

        CharacterConsumablesStackSnapshot stack = FindStack(stackId);
        if (!stack.StackId.IsValid
            || stack.AvailableQuantity <= 0
            || stack.Forbidden
            || stack.State is not CharacterConsumablesStackState.Loose
                and not CharacterConsumablesStackState.Stored
            || stack.RemainingFreshnessSeconds <= 0f
            || !inventory.TryGetMeal(
                stack.ItemId,
                out CharacterConsumablesMealDefinitionSnapshot meal))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.ItemNotConsumable,
                stackId.Value,
                "field-meal-invalid");
            return false;
        }
        bool policyAllowed = IsMealAllowed(actor.Id, meal);
        bool contaminated = stack.Contamination > 0.01f;
        if ((!policyAllowed || contaminated) && !emergency)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                meal.Id.Value);
            return false;
        }
        if (!inventory.TryReserveMealQuantity(
                operationId,
                characterId,
                fieldFacility,
                stackId,
                out string leaseId))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.ItemStackMissing,
                stackId.Value,
                "field-meal-reservation-failed");
            return false;
        }

        string physicalFailure = "meal-lease-invalid";
        if (!inventory.RevalidateMealQuantity(leaseId, stackId)
            || !inventory.TryCommitReservedMealQuantityPending(
                operationId,
                leaseId,
                1,
                out CharacterMealPhysicalCommitSnapshot physicalCommit,
                out physicalFailure))
        {
            inventory.ReleaseMealQuantity(leaseId);
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                stackId.Value,
                "field-meal-pending-commit-failed:" + physicalFailure);
            return false;
        }

        CharacterMealPlan plan = new()
        {
            planId = operationId.Value,
            characterId = characterId.Value,
            facilityInstanceId = FieldMealFacilityId,
            sourceStackId = stackId.Value,
            transportStackId = stackId.Value,
            itemDefinitionId = meal.Id.Value,
            mealQuantityLeaseId = leaseId,
            phase = CharacterMealPlanPhase.ItemCommitted,
            createdAt = clock.Time,
            leaseExpiresAt = clock.Time,
            expectedCompletionEta = MealActionSeconds,
            physicalConsumptionCommitted = true,
            automaticOperation = true,
            beginContamination = stack.Contamination,
            facilitySlotReserved = false,
            physicalCommitOperationId = physicalCommit.OperationId,
            physicalCommitReasonCode = physicalCommit.ReasonCode,
            physicalCommitId = physicalCommit.CommitId,
            physicalCommitSourceStackIds = physicalCommit.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            physicalCommitQuantity = physicalCommit.Quantity,
            physicalCommitInputMassGrams = physicalCommit.InputMassGrams,
            committedPolicyViolation = !policyAllowed,
            committedContaminated = contaminated
        };
        WriteState.ActiveMealPlans.Add(operationId, plan);
        ConsumeMealCommand command = new(
            operationId,
            characterId,
            fieldFacility,
            stackId);
        TryFinalizeCommittedMealPlan(command, plan);
        if (WriteState.CompletedOperations.ContainsKey(operationId))
        {
            result = CharacterConsumablesMealResult.Consumed(
                operationId,
                meal,
                stackId,
                !policyAllowed,
                contaminated);
            return true;
        }

        result = CharacterConsumablesMealResult.Failed(
            CharacterConsumablesFailureCode.DeliveryPending,
            operationId.Value,
            "field-meal-publication-pending");
        return false;
    }

    public bool TryConsumeMeal(
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesMealResult result)
    {
        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
            || !TryGetMealFacility(facilityId, out _))
        {
            result = CharacterConsumablesMealResult.Failed(
                !actor.Id.IsValid
                    ? CharacterConsumablesFailureCode.CharacterMissing
                    : CharacterConsumablesFailureCode.FacilityMissing);
            return false;
        }
        if (world is ICharacterRitualFastingMealPort ritualFasting
            && ritualFasting.IsRitualFasting(characterId))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId.Value,
                "ritual-fast");
            return false;
        }
        if (actor.Hunger > RoutineHungerThreshold)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId.Value,
                "not-hungry");
            return false;
        }
        bool emergency = actor.Hunger <= EmergencyHungerThreshold;
        if (IsMealFollowupCooldownActive(actor) && !emergency)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId.Value,
                "meal-followup-cooldown");
            return false;
        }
        List<MealCandidate> candidates = GetMealCandidates(
            actor,
            facilityId,
            true,
            emergency,
            out bool routePending);
        if (candidates.Count == 0)
        {
            bool deliveryRoutePending = false;
            if (routePending
                || TryRequestMealDelivery(
                    actor,
                    facilityId,
                    emergency,
                    out deliveryRoutePending)
                || deliveryRoutePending)
            {
                result = CharacterConsumablesMealResult.Failed(
                    CharacterConsumablesFailureCode.DeliveryPending,
                    characterId.Value,
                    facilityId.Value);
                return false;
            }
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.ItemStackMissing,
                facilityId.Value);
            return false;
        }
        return TryConsumeMeal(
            new ConsumeMealCommand(
                NewOperationId(),
                characterId,
                facilityId,
                candidates[0].Stack.StackId),
            automaticOperation: true,
            out result);
    }

    public bool TryConsumeMeal(
        ConsumeMealCommand command,
        out CharacterConsumablesMealResult result) =>
        TryConsumeMeal(command, automaticOperation: false, out result);

    private bool TryConsumeMeal(
        ConsumeMealCommand command,
        bool automaticOperation,
        out CharacterConsumablesMealResult result)
    {
        if (!command.IsValid
            || !IsAllowedOperationId(command.OperationId, automaticOperation))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.InvalidCommand,
                command.OperationId.Value);
            return false;
        }
        if (ReadState.CompletedOperations.ContainsKey(command.OperationId))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.AlreadyProcessed,
                command.OperationId.Value);
            return false;
        }
        mealOperationFailures.Remove(command.OperationId);
        if (ReadState.ActiveMealPlans.ContainsKey(command.OperationId))
        {
            return TryGetMealOperationResult(command.OperationId, out result)
                && result.Success;
        }
        if (!TryGetActor(command.CharacterId, out CharacterConsumablesActorSnapshot actor)
            || !TryGetMealFacility(command.FacilityId, out _))
        {
            result = CharacterConsumablesMealResult.Failed(
                !actor.Id.IsValid
                    ? CharacterConsumablesFailureCode.CharacterMissing
                    : CharacterConsumablesFailureCode.FacilityMissing,
                !actor.Id.IsValid ? command.CharacterId.Value : command.FacilityId.Value);
            return false;
        }

        CharacterConsumablesStackSnapshot stack = FindStack(command.ItemStackId);
        if (!IsAvailableMealBufferStack(stack, command.FacilityId))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.ItemStackMissing,
                command.ItemStackId.Value);
            return false;
        }
        if (!inventory.TryGetMeal(stack.ItemId, out CharacterConsumablesMealDefinitionSnapshot meal))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.ItemNotConsumable,
                stack.ItemId.Value);
            return false;
        }

        bool emergency = actor.Hunger <= EmergencyHungerThreshold;
        if (automaticOperation
            && actor.Hunger > RoutineHungerThreshold)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                command.CharacterId.Value,
                "not-hungry");
            return false;
        }
        if (automaticOperation
            && IsMealFollowupCooldownActive(actor)
            && !emergency)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                command.CharacterId.Value,
                "meal-followup-cooldown");
            return false;
        }
        bool policyAllowed = IsMealAllowed(actor.Id, meal);
        bool contaminated = stack.Contamination > 0.01f;
        if ((!policyAllowed || contaminated) && !emergency)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                meal.Id.Value,
                GetDietPolicy(actor.Id).ToString());
            return false;
        }

        if (!world.TryReserveMealFacilitySlot(
                command.OperationId,
                command.CharacterId,
                command.FacilityId))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.DeliveryPending,
                command.FacilityId.Value,
                "meal-facility-slot-reserved");
            return false;
        }
        if (!inventory.TryReserveMealQuantity(
                command.OperationId,
                command.CharacterId,
                command.FacilityId,
                command.ItemStackId,
                out string mealLeaseId))
        {
            world.ReleaseMealFacilitySlot(command.OperationId, command.FacilityId);
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.ItemStackMissing,
                command.ItemStackId.Value,
                "meal-quantity-reservation-failed");
            return false;
        }

        CharacterMealPlan plan = new()
        {
            planId = command.OperationId.Value,
            characterId = command.CharacterId.Value,
            facilityInstanceId = command.FacilityId.Value,
            sourceStackId = command.ItemStackId.Value,
            itemDefinitionId = meal.Id.Value,
            mealQuantityLeaseId = mealLeaseId,
            phase = CharacterMealPlanPhase.Reserved,
            createdAt = clock.Time,
            leaseExpiresAt = clock.Time + 15f,
            expectedCompletionEta = 4f
        };
        plan.automaticOperation = automaticOperation;
        plan.facilitySlotReserved = true;
        WriteState.ActiveMealPlans[command.OperationId] = plan;

        // Begin-use validation: the item must still be an edible buffer item and
        // survive the authored four-second eating action with a small safety margin.
        ItemStackId beginStackId = inventory.TryResolveMealQuantityStack(
                mealLeaseId,
                out ItemStackId resolvedBeginStackId)
            ? resolvedBeginStackId
            : command.ItemStackId;
        CharacterConsumablesStackSnapshot beginStack = FindStack(beginStackId);
        if (!IsAvailableMealBufferStack(beginStack, command.FacilityId)
            || beginStack.RemainingFreshnessSeconds <= 4.25f)
        {
            AbortMealPlan(command, plan);
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.ItemNotConsumable,
                command.ItemStackId.Value,
                "meal-invalid-before-use");
            return false;
        }
        plan.beginContamination = beginStack.Contamination;
        plan.phase = CharacterMealPlanPhase.Eating;
        result = CharacterConsumablesMealResult.Pending(
            command.OperationId,
            meal,
            command.ItemStackId,
            "meal-eating");
        return false;
    }

    public bool TryGetMealOperationResult(
        ConsumableOperationId operationId,
        out CharacterConsumablesMealResult result)
    {
        if (!operationId.IsValid)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.InvalidCommand);
            return false;
        }

        if (ReadState.ActiveMealPlans.TryGetValue(operationId, out CharacterMealPlan plan)
            && plan != null
            && inventory.TryGetMeal(
                new ConsumableItemDefinitionId(plan.itemDefinitionId),
                out CharacterConsumablesMealDefinitionSnapshot activeMeal))
        {
            ItemStackId activeStack = inventory.TryResolveMealQuantityStack(
                    plan.mealQuantityLeaseId,
                    out ItemStackId resolvedActiveStack)
                ? resolvedActiveStack
                : new ItemStackId(plan.sourceStackId);
            result = CharacterConsumablesMealResult.Pending(
                operationId,
                activeMeal,
                activeStack,
                "meal-eating");
            return true;
        }

        if (ReadState.CompletedOperations.TryGetValue(
                operationId,
                out CharacterConsumableOperationState completed)
            && completed != null
            && completed.meal
            && inventory.TryGetMeal(
                completed.ItemDefinitionId,
                out CharacterConsumablesMealDefinitionSnapshot completedMeal))
        {
            result = CharacterConsumablesMealResult.Consumed(
                operationId,
                completedMeal,
                completed.ItemStackId,
                completed.policyViolation,
                completed.contaminated);
            return true;
        }

        bool hasFailure = mealOperationFailures.TryGetValue(
            operationId,
            out MealOperationFailureState failure);
        result = CharacterConsumablesMealResult.Failed(
            hasFailure
                ? failure.Code
                : CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
            operationId.Value,
            hasFailure
                ? failure.Detail
                : "meal-operation-missing-or-aborted");
        return false;
    }

    private void AdvanceMealPlans()
    {
        CharacterConsumablesAggregateState state = WriteState;
        KeyValuePair<ConsumableOperationId, CharacterMealPlan>[] active =
            state.ActiveMealPlans
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .ToArray();
        foreach (KeyValuePair<ConsumableOperationId, CharacterMealPlan> pair in active)
        {
            CharacterMealPlan plan = pair.Value;
            if (plan == null)
                continue;
            ConsumeMealCommand command = new(
                pair.Key,
                new CharacterId(plan.characterId),
                new BuildingInstanceId(plan.facilityInstanceId),
                new ItemStackId(plan.sourceStackId));
            if (!command.IsValid)
            {
                AbortMealPlan(command, plan);
                continue;
            }
            if (plan.phase is CharacterMealPlanPhase.ItemCommitted
                or CharacterMealPlanPhase.EffectsPublished)
            {
                TryFinalizeCommittedMealPlan(command, plan);
                continue;
            }
            if (plan.phase != CharacterMealPlanPhase.Eating)
                continue;
            if (string.IsNullOrWhiteSpace(plan.mealQuantityLeaseId))
            {
                if (!inventory.TryRebindMealQuantityLease(
                        pair.Key,
                        out string reboundLeaseId,
                        out ItemStackId reboundStackId))
                {
                    if (clock.Time >= plan.leaseExpiresAt)
                        AbortMealPlan(command, plan);
                    continue;
                }
                plan.mealQuantityLeaseId = reboundLeaseId;
                plan.transportStackId = reboundStackId.Value;
                plan.leaseExpiresAt = Math.Max(
                    plan.leaseExpiresAt,
                    clock.Time + 15f);
            }
            if (!plan.facilitySlotReserved)
            {
                if (!world.TryReserveMealFacilitySlot(
                        pair.Key,
                        command.CharacterId,
                        command.FacilityId))
                {
                    continue;
                }
                plan.facilitySlotReserved = true;
            }
            if (clock.Time >= plan.leaseExpiresAt)
            {
                AbortMealPlan(command, plan);
                continue;
            }
            if (clock.Time < plan.createdAt + MealActionSeconds)
                continue;
            TryCommitMealPlan(command, plan);
        }
    }

    private bool TryCommitMealPlan(
        ConsumeMealCommand command,
        CharacterMealPlan plan)
    {
        if (!TryGetActor(
                command.CharacterId,
                out CharacterConsumablesActorSnapshot actor))
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.CharacterMissing,
                "meal-actor-missing-at-commit");
            return false;
        }
        if (!actor.Active)
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.CharacterMissing,
                "meal-actor-inactive-at-commit");
            return false;
        }
        if (!TryGetMealFacility(command.FacilityId, out _))
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.FacilityMissing,
                "meal-facility-missing-at-commit");
            return false;
        }
        if (!inventory.TryGetMeal(
                new ConsumableItemDefinitionId(plan.itemDefinitionId),
                out CharacterConsumablesMealDefinitionSnapshot meal))
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.ItemDefinitionMissing,
                "meal-definition-missing-at-commit");
            return false;
        }

        ItemStackId commitStackId = inventory.TryResolveMealQuantityStack(
                plan.mealQuantityLeaseId,
                out ItemStackId resolvedCommitStackId)
            ? resolvedCommitStackId
            : command.ItemStackId;
        CharacterConsumablesStackSnapshot commitStack = FindStack(commitStackId);
        bool policyAllowed = IsMealAllowed(actor.Id, meal);
        bool contaminated = commitStack.Contamination > 0.01f;
        bool emergency = actor.Hunger <= EmergencyHungerThreshold;
        if ((!policyAllowed || contaminated) && !emergency)
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.PolicyForbidden,
                "meal-policy-forbidden-at-commit");
            return false;
        }
        if (!commitStack.StackId.IsValid)
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.ItemStackMissing,
                "meal-stack-missing-at-commit");
            return false;
        }
        if (commitStack.RemainingFreshnessSeconds <= 0f)
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.ItemNotConsumable,
                "meal-spoiled-before-commit");
            return false;
        }
        if (commitStack.Contamination > plan.beginContamination + 0.01f)
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.ItemNotConsumable,
                "meal-contamination-changed-at-commit");
            return false;
        }
        if (!inventory.RevalidateMealQuantity(
                plan.mealQuantityLeaseId,
                commitStackId))
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                "meal-lease-invalid-at-commit");
            return false;
        }
        if (!IsAvailableMealBufferStack(commitStack, command.FacilityId))
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.ItemNotConsumable,
                "meal-buffer-invalid-at-commit");
            return false;
        }
        if (!inventory.TryCommitReservedMealQuantityPending(
                command.OperationId,
                plan.mealQuantityLeaseId,
                1,
                out CharacterMealPhysicalCommitSnapshot physicalCommit,
                out string physicalFailure))
        {
            AbortMealPlan(
                command,
                plan,
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                "meal-quantity-pending-commit-failed:" + physicalFailure);
            return false;
        }
        plan.physicalConsumptionCommitted = true;
        plan.phase = CharacterMealPlanPhase.ItemCommitted;
        plan.physicalCommitOperationId = physicalCommit.OperationId;
        plan.physicalCommitReasonCode = physicalCommit.ReasonCode;
        plan.physicalCommitId = physicalCommit.CommitId;
        plan.physicalCommitSourceStackIds = physicalCommit.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        plan.physicalCommitQuantity = physicalCommit.Quantity;
        plan.physicalCommitInputMassGrams = physicalCommit.InputMassGrams;
        plan.committedPolicyViolation = !policyAllowed;
        plan.committedContaminated = contaminated;
        plan.transportStackId = commitStackId.Value;
        return TryFinalizeCommittedMealPlan(command, plan);
    }

    private bool TryFinalizeCommittedMealPlan(
        ConsumeMealCommand command,
        CharacterMealPlan plan)
    {
        if (plan == null
            || plan.phase is not (CharacterMealPlanPhase.ItemCommitted
                or CharacterMealPlanPhase.EffectsPublished))
        {
            return false;
        }

        CharacterConsumablesAggregateState state = WriteState;
        if (plan.phase == CharacterMealPlanPhase.ItemCommitted)
        {
            if (!state.CompletedOperations.ContainsKey(command.OperationId))
            {
                if (!TryGetActor(
                        command.CharacterId,
                        out CharacterConsumablesActorSnapshot actor)
                    || !actor.Active
                    || !inventory.TryGetMeal(
                        new ConsumableItemDefinitionId(plan.itemDefinitionId),
                        out CharacterConsumablesMealDefinitionSnapshot meal))
                {
                    // Physical custody is already a durable pending receipt.
                    // Retain the plan and retry rather than deleting or minting
                    // the serving through the ordinary abort path.
                    return false;
                }

                ItemStackId committedStackId = new(
                    plan.physicalCommitSourceStackIds?.FirstOrDefault()
                    ?? plan.sourceStackId);
                CharacterConsumablesMealResult result =
                    CharacterConsumablesMealResult.Consumed(
                        command.OperationId,
                        meal,
                        committedStackId,
                        plan.committedPolicyViolation,
                        plan.committedContaminated);
                if (meal.ServingRole is MealServingRole.Snack
                    or MealServingRole.LightMeal)
                {
                    state.MealFollowupCooldownUntil[command.CharacterId] =
                        clock.Time + MealFollowupCooldownSeconds;
                }
                CompleteDelivery(command.CharacterId, command.FacilityId, meal.Id);
                (world as ICharacterRitualFastingMealPort)?.RecordMealConsumed(
                    command.CharacterId,
                    directPlayerOrder: !plan.automaticOperation);
                ApplyMealEffects(command, result);
                RecordCompletedOperation(
                    command.OperationId,
                    command.CharacterId,
                    meal.Id,
                    committedStackId,
                    true,
                    plan.committedPolicyViolation,
                    plan.committedContaminated);
            }
            plan.phase = CharacterMealPlanPhase.EffectsPublished;
        }

        if (!inventory.TryAcknowledgeMealConsumption(
                command.CharacterId,
                new ConsumableItemDefinitionId(plan.itemDefinitionId),
                plan.physicalCommitQuantity,
                plan.physicalCommitId,
                out _))
        {
            return false;
        }

        plan.phase = CharacterMealPlanPhase.Completed;
        state.ActiveMealPlans.Remove(command.OperationId);
        mealOperationFailures.Remove(command.OperationId);
        // Facility-slot ownership is transient and is deliberately not restored.
        // Releasing by the stable operation/facility pair is idempotent, and is
        // required to clear an owner retained by the pre-restore world adapter.
        // The virtual field-meal facility has no owner entry, so the same call is
        // also a safe no-op for primitive meals.
        world.ReleaseMealFacilitySlot(command.OperationId, command.FacilityId);
        plan.facilitySlotReserved = false;
        return true;
    }

    private void AbortMealPlan(
        ConsumeMealCommand command,
        CharacterMealPlan plan,
        CharacterConsumablesFailureCode failureCode =
            CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
        string failureDetail = "meal-operation-aborted")
    {
        if (plan != null)
        {
            plan.phase = CharacterMealPlanPhase.Aborted;
            if (!plan.physicalConsumptionCommitted)
                inventory.ReleaseMealQuantity(plan.mealQuantityLeaseId);
        }
        WriteState.ActiveMealPlans.Remove(command.OperationId);
        if (command.OperationId.IsValid)
        {
            mealOperationFailures[command.OperationId] =
                new MealOperationFailureState(failureCode, failureDetail);
            if (queuedMealOperationFailures.Add(command.OperationId))
            {
                mealOperationFailureOrder.Enqueue(command.OperationId);
            }
            while (mealOperationFailureOrder.Count
                   > MaximumRememberedMealOperationFailures)
            {
                ConsumableOperationId expired = mealOperationFailureOrder.Dequeue();
                queuedMealOperationFailures.Remove(expired);
                mealOperationFailures.Remove(expired);
            }
        }
        world.ReleaseMealFacilitySlot(command.OperationId, command.FacilityId);
    }

    public int CancelActiveMealOperations(CharacterId characterId, string reason)
    {
        if (!characterId.IsValid)
        {
            return 0;
        }

        KeyValuePair<ConsumableOperationId, CharacterMealPlan>[] active =
            WriteState.ActiveMealPlans
                .Where(pair => pair.Value != null
                    && string.Equals(
                        pair.Value.characterId,
                        characterId.Value,
                        StringComparison.Ordinal))
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .ToArray();
        for (int index = 0; index < active.Length; index++)
        {
            ConsumableOperationId operationId = active[index].Key;
            CharacterMealPlan plan = active[index].Value;
            AbortMealPlan(
                new ConsumeMealCommand(
                    operationId,
                    characterId,
                    new BuildingInstanceId(plan.facilityInstanceId),
                    new ItemStackId(plan.sourceStackId)),
                plan,
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                string.IsNullOrWhiteSpace(reason)
                    ? "character-lifecycle-ended"
                    : reason.Trim());
        }

        return active.Length;
    }

    public CharacterSubstancePolicyState GetSubstancePolicy(
        CharacterId characterId,
        string substanceId)
    {
        if (!inventory.TryResolveSubstance(
                substanceId,
                out CharacterConsumablesSubstanceDefinitionSnapshot substance))
        {
            return new CharacterSubstancePolicyState
            {
                characterId = characterId.Value,
                mode = SubstancePolicyMode.Forbidden
            };
        }
        CharacterSubstanceKey key = new(characterId, substance.Id);
        return ReadState.SubstancePolicies.TryGetValue(key, out CharacterSubstancePolicyState state)
            ? CharacterConsumablesStateRules.Clone(state)
            : new CharacterSubstancePolicyState
            {
                characterId = characterId.Value,
                itemDefinitionId = substance.Id.Value,
                mode = CharacterConsumablesPolicyRules.GetDefaultSubstancePolicy(
                    substance.Definition.UseClass),
                moodThreshold = 30f,
                scheduledHour = 20
            };
    }

    public void SetSubstancePolicy(
        CharacterId characterId,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold,
        int scheduledHour)
    {
        if (!characterId.IsValid
            || !inventory.TryResolveSubstance(
                substanceId,
                out CharacterConsumablesSubstanceDefinitionSnapshot substance))
        {
            return;
        }
        CharacterSubstancePolicyState state = new()
        {
            characterId = characterId.Value,
            itemDefinitionId = substance.Id.Value,
            mode = mode,
            moodThreshold = Mathf.Clamp(moodThreshold, 0f, 100f),
            scheduledHour = Mathf.Clamp(scheduledHour, 0, 23)
        };
        WriteState.SubstancePolicies[new CharacterSubstanceKey(characterId, substance.Id)] = state;
    }

    public CharacterSubstanceState GetSubstanceState(
        CharacterId characterId,
        string substanceId)
    {
        if (!inventory.TryResolveSubstance(
                substanceId,
                out CharacterConsumablesSubstanceDefinitionSnapshot substance))
        {
            return new CharacterSubstanceState { characterId = characterId.Value };
        }
        CharacterSubstanceKey key = new(characterId, substance.Id);
        return ReadState.SubstanceStates.TryGetValue(key, out CharacterSubstanceState state)
            ? CharacterConsumablesStateRules.Clone(state)
            : new CharacterSubstanceState
            {
                characterId = characterId.Value,
                itemDefinitionId = substance.Id.Value
            };
    }

    public bool TryConsumeSubstance(
        CharacterId characterId,
        string substanceId,
        bool medicalContext,
        bool combatContext,
        out CharacterConsumablesSubstanceResult result)
    {
        if (!TryGetActor(characterId, out _))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.CharacterMissing,
                characterId.Value);
            return false;
        }
        if (!inventory.TryResolveSubstance(
                substanceId,
                out CharacterConsumablesSubstanceDefinitionSnapshot substance))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.ItemDefinitionMissing,
                substanceId);
            return false;
        }
        CharacterConsumablesStackSnapshot stack = FindAvailableSubstanceStack(
            characterId,
            substance.Id);
        if (!stack.StackId.IsValid)
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.ItemStackMissing,
                substance.Id.Value);
            return false;
        }
        return TryConsumeSubstance(
            new ConsumeSubstanceByIdCommand(
                NewOperationId(),
                characterId,
                substance.Id,
                stack.StackId,
                medicalContext,
                combatContext),
            automaticOperation: true,
            out result);
    }

    public bool TryConsumeRecreationalSubstance(
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesSubstanceResult result)
    {
        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
            || !TryGetRecreationalSubstanceFacility(
                facilityId,
                out CharacterConsumablesFacilitySnapshot facility))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                !actor.Id.IsValid
                    ? CharacterConsumablesFailureCode.CharacterMissing
                    : CharacterConsumablesFailureCode.FacilityMissing,
                !actor.Id.IsValid ? characterId.Value : facilityId.Value);
            return false;
        }

        List<RecreationalSubstanceCandidate> buffered =
            GetRecreationalSubstanceCandidates(actor, facilityId, bufferOnly: true);
        if (buffered.Count > 0)
        {
            RecreationalSubstanceCandidate selected = buffered[0];
            return TryConsumeSubstance(
                new ConsumeSubstanceByIdCommand(
                    NewOperationId(),
                    characterId,
                    selected.Substance.Id,
                    selected.Stack.StackId,
                    medicalContext: false,
                    combatContext: false),
                automaticOperation: true,
                out result,
                allowedFacilityDestinationId:
                    GetRecreationalSubstanceDestinationId(
                        facilityId,
                        selected.Substance.Id));
        }

        List<RecreationalSubstanceCandidate> deliverable =
            GetRecreationalSubstanceCandidates(actor, facilityId, bufferOnly: false);
        if (deliverable.Count > 0)
        {
            RecreationalSubstanceCandidate selected = deliverable[0];
            string destinationId = GetRecreationalSubstanceDestinationId(
                facilityId,
                selected.Substance.Id);
            if (HasRoutedItem(destinationId, selected.Substance.Id))
            {
                result = CharacterConsumablesSubstanceResult.Failed(
                    CharacterConsumablesFailureCode.DeliveryPending,
                    characterId.Value,
                    facilityId.Value,
                    selected.Substance.Id.Value);
                return false;
            }
            if (inventory.TryRequestDelivery(
                    selected.Substance.Id,
                    1,
                    facility.Position,
                    destinationId,
                    out int requested,
                    out _) && requested > 0)
            {
                workforce?.RequestOneHaulerToReplan(characterId);
                result = CharacterConsumablesSubstanceResult.Failed(
                    CharacterConsumablesFailureCode.DeliveryPending,
                    characterId.Value,
                    facilityId.Value,
                    selected.Substance.Id.Value);
                return false;
            }
        }

        bool hasRecreationalStock = false;
        bool hasPolicyAllowedRecreationalStock = false;
        foreach (CharacterConsumablesStackSnapshot stack in inventory.GetAllStacks())
        {
            if (stack.AvailableQuantity <= 0 || stack.Forbidden
                || !inventory.TryResolveSubstance(
                    stack.ItemId,
                    out CharacterConsumablesSubstanceDefinitionSnapshot substance)
                || substance.Definition.UseClass != SubstanceUseClass.Recreational)
            {
                continue;
            }
            hasRecreationalStock = true;
            CharacterSubstancePolicyState policy = GetSubstancePolicy(
                actor.Id,
                substance.Definition.SubstanceId);
            if (CharacterConsumablesPolicyRules.AllowsSubstance(
                    policy,
                    medicalContext: false,
                    combatContext: false,
                    actor.Mood))
            {
                hasPolicyAllowedRecreationalStock = true;
                break;
            }
        }
        result = CharacterConsumablesSubstanceResult.Failed(
            hasRecreationalStock && !hasPolicyAllowedRecreationalStock
                ? CharacterConsumablesFailureCode.PolicyForbidden
                : CharacterConsumablesFailureCode.ItemStackMissing,
            characterId.Value,
            facilityId.Value);
        return false;
    }

    public bool TryConsumeSubstance(
        ConsumeSubstanceByIdCommand command,
        out CharacterConsumablesSubstanceResult result) =>
        TryConsumeSubstance(command, automaticOperation: false, out result);

    private bool TryConsumeSubstance(
        ConsumeSubstanceByIdCommand command,
        bool automaticOperation,
        out CharacterConsumablesSubstanceResult result,
        string allowedFacilityDestinationId = null)
    {
        if (!command.IsValid
            || !IsAllowedOperationId(command.OperationId, automaticOperation))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.InvalidCommand,
                command.OperationId.Value);
            return false;
        }
        if (ReadState.CompletedOperations.ContainsKey(command.OperationId))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.AlreadyProcessed,
                command.OperationId.Value);
            return false;
        }
        if (ReadState.ActiveSubstanceUsePlans.TryGetValue(
                command.OperationId,
                out CharacterSubstanceUsePlan pendingPlan))
        {
            TryFinalizeSubstanceUsePlan(command.OperationId, pendingPlan);
            result = CharacterConsumablesSubstanceResult.Failed(
                ReadState.CompletedOperations.ContainsKey(command.OperationId)
                    ? CharacterConsumablesFailureCode.AlreadyProcessed
                    : CharacterConsumablesFailureCode.DeliveryPending,
                command.OperationId.Value);
            return false;
        }
        if (!TryGetActor(command.CharacterId, out CharacterConsumablesActorSnapshot actor))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.CharacterMissing,
                command.CharacterId.Value);
            return false;
        }
        if (!inventory.TryResolveSubstance(
                command.ItemDefinitionId,
                out CharacterConsumablesSubstanceDefinitionSnapshot substance))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.ItemDefinitionMissing,
                command.ItemDefinitionId.Value);
            return false;
        }

        CharacterSubstancePolicyState policy = GetSubstancePolicy(
            actor.Id,
            substance.Definition.SubstanceId);
        if (!CharacterConsumablesPolicyRules.AllowsSubstance(
                policy,
                command.MedicalContext,
                command.CombatContext,
                actor.Mood))
        {
            result = FailedSubstance(
                CharacterConsumablesFailureCode.PolicyForbidden,
                substance,
                command.ItemStackId,
                substance.Id.Value,
                policy.mode.ToString());
            return false;
        }
        CharacterConsumablesStackSnapshot stack = FindStack(command.ItemStackId);
        bool availableToCharacter = IsSubstanceStackAvailableToCharacter(
            stack,
            command.CharacterId);
        bool availableAtAuthorizedFacility =
            !string.IsNullOrWhiteSpace(allowedFacilityDestinationId)
            && stack.StackId.IsValid
            && stack.AvailableQuantity > 0
            && !stack.Forbidden
            && stack.State == CharacterConsumablesStackState.FacilityBuffer
            && string.Equals(
                stack.DestinationId,
                allowedFacilityDestinationId,
                StringComparison.Ordinal);
        if ((!availableToCharacter && !availableAtAuthorizedFacility)
            || !stack.ItemId.Equals(substance.Id))
        {
            result = FailedSubstance(
                CharacterConsumablesFailureCode.ItemStackMissing,
                substance,
                command.ItemStackId,
                command.ItemStackId.Value);
            return false;
        }
        CharacterSubstanceState currentState = GetWritableSubstanceState(
            command.CharacterId,
            substance.Id);
        SubstanceDefinitionView definition = substance.Definition;
        float toleranceRatio = currentState.tolerance / 100f;
        bool wasAddicted = currentState.addicted;
        bool overdosed = random.Chance(
            Mathf.Clamp01(definition.OverdoseChance * (1f + toleranceRatio)));
        float resolvedTolerance = Mathf.Clamp(
            currentState.tolerance + definition.ToleranceGain,
            0f,
            100f);
        float resolvedAddiction = Mathf.Clamp(
            currentState.addiction
                + definition.AddictionChance * 100f * (0.65f + toleranceRatio),
            0f,
            100f);
        bool resolvedAddicted = currentState.addicted || resolvedAddiction >= 60f
            || random.Chance(definition.AddictionChance * 0.2f);
        float resolvedScheduledCooldown = policy.mode == SubstancePolicyMode.Scheduled
            ? GameHourSeconds * 24f
            : currentState.scheduledCooldownSeconds;
        if (!inventory.TryCommitSubstanceConsumptionPending(
                command.OperationId,
                command.CharacterId,
                stack.StackId,
                out CharacterSubstancePhysicalCommitSnapshot physicalCommit,
                out string physicalFailure))
        {
            result = FailedSubstance(
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                substance,
                command.ItemStackId,
                command.ItemStackId.Value,
                physicalFailure);
            return false;
        }

        CharacterSubstanceUsePlan plan = new()
        {
            operationId = command.OperationId.Value,
            characterId = command.CharacterId.Value,
            itemDefinitionId = substance.Id.Value,
            sourceStackId = stack.StackId.Value,
            phase = CharacterSubstanceUsePlanPhase.ItemCommitted,
            automaticOperation = automaticOperation,
            physicalCommitOperationId = physicalCommit.OperationId,
            physicalCommitReasonCode = physicalCommit.ReasonCode,
            physicalCommitId = physicalCommit.CommitId,
            physicalCommitSourceStackIds = physicalCommit.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            physicalCommitQuantity = physicalCommit.Quantity,
            physicalCommitInputMassGrams = physicalCommit.InputMassGrams,
            resolvedTolerance = resolvedTolerance,
            resolvedAddiction = resolvedAddiction,
            resolvedWithdrawal = 0f,
            resolvedActiveSeconds = Mathf.Max(1f, definition.DurationSeconds),
            resolvedSecondsSinceLastDose = 0f,
            resolvedScheduledCooldownSeconds = resolvedScheduledCooldown,
            effectToleranceRatio = toleranceRatio,
            resolvedAddicted = resolvedAddicted,
            resolvedOverdosed = overdosed,
            becameAddicted = !wasAddicted && resolvedAddicted
        };
        WriteState.ActiveSubstanceUsePlans.Add(command.OperationId, plan);
        TryFinalizeSubstanceUsePlan(command.OperationId, plan);
        if (!WriteState.CompletedOperations.ContainsKey(command.OperationId))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.DeliveryPending,
                command.OperationId.Value,
                "substance-effects-publication-pending");
            return false;
        }
        result = new CharacterConsumablesSubstanceResult(
            true,
            CharacterConsumablesFailureCode.None,
            substance,
            command.ItemStackId,
            resolvedTolerance,
            resolvedAddiction,
            !wasAddicted && resolvedAddicted,
            overdosed);
        return true;
    }

    private void AdvanceSubstanceUsePlans()
    {
        KeyValuePair<ConsumableOperationId, CharacterSubstanceUsePlan>[] active =
            WriteState.ActiveSubstanceUsePlans
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .ToArray();
        foreach (KeyValuePair<ConsumableOperationId, CharacterSubstanceUsePlan> pair
                 in active)
        {
            TryFinalizeSubstanceUsePlan(pair.Key, pair.Value);
        }
    }

    private bool TryFinalizeSubstanceUsePlan(
        ConsumableOperationId operationId,
        CharacterSubstanceUsePlan plan)
    {
        if (!operationId.IsValid
            || plan == null
            || !string.Equals(
                plan.operationId,
                operationId.Value,
                StringComparison.Ordinal))
        {
            return false;
        }

        CharacterId characterId = new(plan.characterId);
        ConsumableItemDefinitionId itemId = new(plan.itemDefinitionId);
        if (plan.phase == CharacterSubstanceUsePlanPhase.ItemCommitted)
        {
            if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
                || !actor.Active
                || !inventory.TryResolveSubstance(
                    itemId,
                    out CharacterConsumablesSubstanceDefinitionSnapshot substance))
            {
                return false;
            }

            if (!WriteState.CompletedOperations.ContainsKey(operationId))
            {
                CharacterSubstanceState state = GetWritableSubstanceState(
                    characterId,
                    itemId);
                state.tolerance = plan.resolvedTolerance;
                state.addiction = plan.resolvedAddiction;
                state.withdrawal = plan.resolvedWithdrawal;
                state.activeSeconds = plan.resolvedActiveSeconds;
                state.secondsSinceLastDose = plan.resolvedSecondsSinceLastDose;
                state.scheduledCooldownSeconds =
                    plan.resolvedScheduledCooldownSeconds;
                state.addicted = plan.resolvedAddicted;
                state.overdosed = plan.resolvedOverdosed;
                ApplySubstanceEffects(
                    characterId,
                    substance,
                    state,
                    plan.effectToleranceRatio,
                    plan.resolvedOverdosed);
                RecordCompletedOperation(
                    operationId,
                    characterId,
                    itemId,
                    new ItemStackId(plan.sourceStackId),
                    false);
            }
            plan.phase = CharacterSubstanceUsePlanPhase.EffectsPublished;
        }

        if (!inventory.TryAcknowledgeSubstanceConsumption(
                characterId,
                itemId,
                plan.physicalCommitQuantity,
                plan.physicalCommitId,
                out _))
        {
            return false;
        }
        WriteState.ActiveSubstanceUsePlans.Remove(operationId);
        return true;
    }

    public bool TryGetAutomaticUseRequest(
        CharacterId characterId,
        out CharacterConsumablesUseRequest request)
    {
        request = default;
        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
            || !actor.Active)
        {
            return false;
        }
        bool medical = actor.Health < actor.MaxHealth * MedicalUseHealthRatio;
        int scheduleHour = GetCurrentScheduleHour();
        CharacterConsumablesUseRequest best = default;
        foreach (CharacterConsumablesSubstanceDefinitionSnapshot substance in inventory.GetSubstances())
        {
            if (!FindAvailableSubstanceStack(
                    characterId,
                    substance.Id).StackId.IsValid)
            {
                continue;
            }
            CharacterSubstancePolicyState policy = GetSubstancePolicy(
                actor.Id,
                substance.Definition.SubstanceId);
            CharacterSubstanceState state = GetSubstanceState(
                actor.Id,
                substance.Definition.SubstanceId);
            if (state.activeSeconds > 0.01f)
            {
                continue;
            }
            (float urgency, string reason) = GetUseUrgency(
                actor,
                substance,
                policy,
                state,
                medical,
                scheduleHour);
            if (urgency > best.Urgency)
            {
                best = new CharacterConsumablesUseRequest(
                    substance,
                    urgency,
                    medical,
                    actor.CombatStance,
                    reason);
            }
        }
        request = best;
        return request.IsValid;
    }

    public float GetWorkSpeedMultiplier(CharacterId characterId) =>
        GetEffectMultiplier(characterId, true);
    public float GetCombatMultiplier(CharacterId characterId) =>
        GetEffectMultiplier(characterId, false);

    public void Tick()
    {
        AdvanceSubstanceUsePlans();
        AdvanceMealPlans();
        PruneExpiredDeliveries(forceExpirySweep: true);
        float deltaTime = Mathf.Max(0f, clock.DeltaTime);
        if (deltaTime <= 0f)
        {
            return;
        }
#if UNITY_EDITOR
        if (!mealDeliveryPausedForDiagnostics)
#endif
        {
            PrimeHungryActorMealDelivery();
        }
        foreach (CharacterSubstanceState state in WriteState.SubstanceStates.Values)
        {
            state.activeSeconds = Mathf.Max(0f, state.activeSeconds - deltaTime);
            state.secondsSinceLastDose += deltaTime;
            state.scheduledCooldownSeconds = Mathf.Max(
                0f,
                state.scheduledCooldownSeconds - deltaTime);
            if (state.activeSeconds <= 0f)
            {
                state.tolerance = Mathf.Max(0f, state.tolerance - deltaTime * 0.004f);
            }
            if (!state.addicted || state.secondsSinceLastDose < GameHourSeconds
                || !inventory.TryResolveSubstance(
                    state.ItemDefinitionId,
                    out CharacterConsumablesSubstanceDefinitionSnapshot substance))
            {
                continue;
            }
            state.withdrawal = Mathf.Clamp(
                state.withdrawal
                + substance.Definition.WithdrawalPerHour * (deltaTime / GameHourSeconds),
                0f,
                100f);
            if (state.withdrawal >= 20f && world.TryGetActor(state.CharacterId, out _))
            {
                world.ApplyMood(
                    state.CharacterId,
                    $"substance:withdrawal:{substance.Id.Value}",
                    $"{substance.Definition.DisplayName} withdrawal",
                    -Mathf.Lerp(3f, 14f, state.withdrawal / 100f),
                    2f);
            }
        }
    }

    private void PrimeHungryActorMealDelivery()
    {
        float now = clock.Time;
        if (now < nextMealDeliveryProbeAt
            && nextMealDeliveryProbeAt - now <= MealDeliveryProbeSeconds * 2f)
        {
            return;
        }
        nextMealDeliveryProbeAt = now + MealDeliveryProbeSeconds;
        MealDeliveryProbeCount++;

        CharacterConsumablesActorSnapshot actor = world.CharacterIds
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .Select(id => world.TryGetActor(id, out CharacterConsumablesActorSnapshot value)
                ? value
                : default)
            .FirstOrDefault(value => value.Id.IsValid
                && value.Active
                && value.Hunger <= RoutineHungerThreshold
                && !(world is ICharacterRitualFastingMealPort fasting
                    && fasting.IsRitualFasting(value.Id)));
        if (!actor.Id.IsValid)
        {
            LastMealDeliveryProbeDetail = "no-hungry-actor";
            return;
        }

        bool emergency = actor.Hunger <= EmergencyHungerThreshold;
        int deliveryCandidateCount = 0;
        int reachableMealFacilityCount = 0;
        foreach ((BuildingInstanceId facilityId, float travelSeconds) in world.FacilityIds
                     .OrderBy(id => id.Value, StringComparer.Ordinal)
                     .Select(id =>
                     {
                         if (!TryGetMealFacility(id, out CharacterConsumablesFacilitySnapshot facility)
                             || world.GetMealRouteStatus(
                                 actor.Id,
                                 actor.Position,
                                 facility.Position,
                                 out float travelSeconds) != CharacterMealRouteStatus.Reachable)
                         {
                             return (id, float.PositiveInfinity);
                         }
                         return (id, travelSeconds);
                     })
                     .Where(candidate => !float.IsPositiveInfinity(candidate.Item2))
                     .OrderBy(candidate => candidate.Item2)
                     .ThenBy(candidate => candidate.id.Value, StringComparer.Ordinal))
        {
            reachableMealFacilityCount++;
            deliveryCandidateCount += GetMealCandidates(
                actor,
                facilityId,
                false,
                emergency,
                out _,
                requireExactRoute: false).Count;
            if (TryRequestMealDelivery(
                    actor,
                    facilityId,
                    emergency,
                    out _))
            {
                LastMealDeliveryProbeDetail =
                    $"requested actor={actor.Id.Value};facility={facilityId.Value};"
                    + $"hunger={actor.Hunger:0.###};emergency={emergency}";
                return;
            }
        }

        LastMealDeliveryProbeDetail =
            $"no-delivery-candidate actor={actor.Id.Value};"
            + $"hunger={actor.Hunger:0.###};emergency={emergency};"
            + $"facilities={world.FacilityIds.Count};"
            + $"reachableMealFacilities={reachableMealFacilityCount};"
            + $"eligibleSources={deliveryCandidateCount};"
            + $"lastRequestFailure={LastMealDeliveryRequestFailure}";
    }

    public void ReconcilePersistentActorReferences(
        IReadOnlyCollection<CharacterId> persistentActorIds)
    {
        if (persistentActorIds == null)
        {
            throw new ArgumentNullException(nameof(persistentActorIds));
        }

        HashSet<CharacterId> persistent = persistentActorIds
            .Where(id => id.IsValid)
            .ToHashSet();
        CharacterConsumablesAggregateState snapshot = ReadState;
        string[] activeOperations = snapshot.ActiveMealPlans
            .Where(pair => pair.Value != null
                && !persistent.Contains(new CharacterId(pair.Value.characterId)))
            .Select(pair => pair.Key.Value)
            .Concat(snapshot.ActiveSubstanceUsePlans
                .Where(pair => pair.Value != null
                    && !persistent.Contains(new CharacterId(pair.Value.characterId)))
                .Select(pair => pair.Key.Value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (activeOperations.Length > 0)
        {
            throw new InvalidOperationException(
                "Character consumables capture found active physical operations "
                + "owned by non-persistent actors: "
                + string.Join(",", activeOperations));
        }

        ConsumableDeliveryId[] orphanedDeliveries = snapshot.PendingDeliveries
            .Where(pair => pair.Value == null
                || !persistent.Contains(pair.Value.CharacterId))
            .Select(pair => pair.Key)
            .ToArray();
        CharacterId[] orphanedDietPolicies = snapshot.DietPolicies.Keys
            .Where(id => !persistent.Contains(id))
            .ToArray();
        CharacterId[] orphanedQualityPolicies = snapshot.MealQualityPolicies.Keys
            .Where(id => !persistent.Contains(id))
            .ToArray();
        CharacterSubstanceKey[] orphanedSubstancePolicies =
            snapshot.SubstancePolicies.Keys
                .Where(key => !persistent.Contains(key.CharacterId))
                .ToArray();
        CharacterSubstanceKey[] orphanedSubstanceStates = snapshot.SubstanceStates.Keys
            .Where(key => !persistent.Contains(key.CharacterId))
            .ToArray();
        ConsumableOperationId[] orphanedCompletedOperations =
            snapshot.CompletedOperations
                .Where(pair => pair.Value == null
                    || !persistent.Contains(pair.Value.CharacterId))
                .Select(pair => pair.Key)
                .ToArray();
        CharacterId[] orphanedCooldowns = snapshot.MealFollowupCooldownUntil.Keys
            .Where(id => !persistent.Contains(id))
            .ToArray();

        if (orphanedDeliveries.Length == 0
            && orphanedDietPolicies.Length == 0
            && orphanedQualityPolicies.Length == 0
            && orphanedSubstancePolicies.Length == 0
            && orphanedSubstanceStates.Length == 0
            && orphanedCompletedOperations.Length == 0
            && orphanedCooldowns.Length == 0)
        {
            return;
        }

        CharacterConsumablesAggregateState state = WriteState;
        foreach (ConsumableDeliveryId deliveryId in orphanedDeliveries)
        {
            if (state.PendingDeliveries.Remove(
                    deliveryId,
                    out CharacterMealDeliveryState delivery)
                && delivery != null)
            {
                state.DeliveryByRoute.Remove(
                    CharacterConsumablesStateRules.Route(delivery));
            }
        }
        foreach (CharacterId id in orphanedDietPolicies)
            state.DietPolicies.Remove(id);
        foreach (CharacterId id in orphanedQualityPolicies)
            state.MealQualityPolicies.Remove(id);
        foreach (CharacterSubstanceKey key in orphanedSubstancePolicies)
            state.SubstancePolicies.Remove(key);
        foreach (CharacterSubstanceKey key in orphanedSubstanceStates)
            state.SubstanceStates.Remove(key);
        foreach (ConsumableOperationId id in orphanedCompletedOperations)
            state.CompletedOperations.Remove(id);
        foreach (CharacterId id in orphanedCooldowns)
            state.MealFollowupCooldownUntil.Remove(id);
    }

    public DungeonCharacterConsumablesSaveData Capture()
    {
        // Character/customer and facility lifecycles can retire between the
        // last simulation tick and a save request. A pending meal route is not
        // durable without both owners, so reconcile that transient edge before
        // serializing rather than emitting a current-format payload that strict
        // restore must reject.
        PruneExpiredDeliveries();
        return CharacterConsumablesStateRules.Capture(ReadState);
    }

    public void ValidateRestorePayload(
        DungeonCharacterConsumablesSaveData saveData,
        bool requireWorldReferences)
    {
        DungeonGameRestoreReport report = new();
        CharacterConsumablesStateRules.Validate(
            saveData,
            report,
            world,
            inventory,
            requireWorldReferences);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character consumables restore payload rejected: "
                + string.Join(" | ", report.Errors));
        }
    }

    public CharacterConsumablesRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterConsumablesSaveData saveData)
    {
        ValidateRestorePayload(saveData, requireWorldReferences: true);
        return new CharacterConsumablesRestoreCandidate(
            CharacterConsumablesStateRules.Build(saveData));
    }

    public void PublishRestoreCandidate(
        CharacterConsumablesRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
    }

    private void ApplyMealEffects(
        ConsumeMealCommand command,
        CharacterConsumablesMealResult result)
    {
        world.RecoverHunger(command.CharacterId, result.Meal.Nutrition);
        float mood = result.Meal.Mood;
        if (result.PolicyViolation)
        {
            mood -= 9f;
            world.RecordNeedNarrative(
                command.CharacterId,
                "diet:emergency-violation",
                result.Meal.Id.Value,
                "survived",
                1f);
        }
        float poisoningChance = result.Contaminated
            ? Mathf.Clamp01(world.ProjectGameplayEffect(
                command.CharacterId,
                GameplayEffectTargetIds.FoodPoisoningChance,
                1f))
            : 0f;
        if (result.Contaminated && random.Chance(poisoningChance))
        {
            mood -= 7f;
            world.ApplyDamage(command.CharacterId, 3f, "contaminated meal");
        }
        if (!Mathf.Approximately(mood, 0f))
        {
            world.ApplyBestMealMood(
                command.CharacterId,
                $"Ate {result.Meal.DisplayName}",
                mood,
                180f);
        }
        events.Publish(new CharacterConsumablesMealConsumedEvent(
            command.OperationId,
            command.CharacterId,
            command.FacilityId,
            result));
    }

    private void ApplySubstanceEffects(
        CharacterId characterId,
        CharacterConsumablesSubstanceDefinitionSnapshot substance,
        CharacterSubstanceState state,
        float toleranceRatio,
        bool overdosed)
    {
        SubstanceDefinitionView definition = substance.Definition;
        float effectiveMood = definition.MoodEffect * (1f - toleranceRatio * 0.55f);
        if (!Mathf.Approximately(effectiveMood, 0f))
        {
            world.ApplyMood(
                characterId,
                $"substance:{substance.Id.Value}",
                definition.DisplayName,
                effectiveMood,
                definition.DurationSeconds);
        }
        if (overdosed)
        {
            if (!world.TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor))
            {
                throw new InvalidOperationException(
                    $"Consumables actor '{characterId.Value}' vanished during overdose effects.");
            }
            world.ApplyDamage(
                characterId,
                Mathf.Max(4f, actor.MaxHealth * 0.12f),
                $"{definition.DisplayName} overdose");
            world.ApplyMood(
                characterId,
                $"substance:overdose:{substance.Id.Value}",
                $"{definition.DisplayName} overdose",
                -12f,
                300f);
        }
        world.RecordNeedNarrative(
            characterId,
            $"substance:{substance.Id.Value}",
            substance.Id.Value,
            overdosed ? "overdose" : "consumed",
            state.tolerance);
    }

    private bool TryRequestMealDelivery(
        CharacterConsumablesActorSnapshot actor,
        BuildingInstanceId facilityId,
        bool emergency,
        out bool routePending)
    {
        routePending = false;
        LastMealDeliveryRequestFailure = "not-attempted";
        if (!TryGetMealFacility(facilityId, out CharacterConsumablesFacilitySnapshot facility))
        {
            LastMealDeliveryRequestFailure = "meal-facility-missing";
            return false;
        }
        // Delivery authoring only needs a valid source item and destination.
        // A stored stack's cell can be occupied by its warehouse building, so
        // testing that cell with the consumer actor's route authority rejects
        // deliveries that the haul planner can lawfully service from an access
        // stand. The warehouse request and haul planner own that route check.
        foreach (MealCandidate candidate in GetMealCandidates(
                     actor,
                     facilityId,
                     false,
                     emergency,
                     out routePending,
                     requireExactRoute: false))
        {
            MealDeliveryRoute route = new(actor.Id, facilityId, candidate.Definition.Id);
            ConsumableDeliveryId expiredDeliveryId = default;
            if (ReadState.DeliveryByRoute.TryGetValue(route, out ConsumableDeliveryId existingId)
                && ReadState.PendingDeliveries.TryGetValue(existingId, out CharacterMealDeliveryState existing)
                && clock.Time < existing.retryAfter)
            {
                return true;
            }
            if (ReadState.DeliveryByRoute.TryGetValue(route, out existingId))
            {
                expiredDeliveryId = existingId;
            }
            string destinationId = GetMealDestinationId(
                facilityId,
                candidate.Definition.Id);
            if (HasRoutedItem(destinationId, candidate.Definition.Id))
            {
                return true;
            }
            if (!inventory.TryRequestDelivery(
                    candidate.Definition.Id,
                    1,
                    facility.Position,
                    destinationId,
                    out int requested,
                    out string failureReason)
                || requested <= 0)
            {
                LastMealDeliveryRequestFailure = string.IsNullOrWhiteSpace(failureReason)
                    ? "delivery-request-returned-zero"
                    : failureReason;
                continue;
            }
            LastMealDeliveryRequestFailure = "none";
            if (expiredDeliveryId.IsValid)
            {
                // The route index is a single-owner authority. Replacing an
                // expired request without deleting its old ledger row leaves
                // two pending deliveries for one route, which current-format
                // restore correctly rejects. Preserve the old row until the
                // replacement request has succeeded, then swap atomically.
                WriteState.PendingDeliveries.Remove(expiredDeliveryId);
                WriteState.DeliveryByRoute.Remove(route);
            }
            CharacterMealDeliveryState delivery = new()
            {
                deliveryId = NewDeliveryId().Value,
                characterId = actor.Id.Value,
                buildingInstanceId = facilityId.Value,
                itemDefinitionId = candidate.Definition.Id.Value,
                requestedAt = clock.Time,
                retryAfter = clock.Time + DeliveryRetrySeconds
            };
            WriteState.PendingDeliveries.Add(delivery.DeliveryId, delivery);
            WriteState.DeliveryByRoute[route] = delivery.DeliveryId;
            workforce?.RequestOneHaulerToReplan(actor.Id);
            return true;
        }
        return false;
    }

    private List<MealCandidate> GetMealCandidates(
        CharacterConsumablesActorSnapshot actor,
        BuildingInstanceId facilityId,
        bool bufferOnly,
        bool emergency,
        out bool routePending,
        bool requireExactRoute = true)
    {
        routePending = false;
        List<MealCandidate> result = new();
        if (!TryGetMealFacility(
                facilityId,
                out CharacterConsumablesFacilitySnapshot facility))
        {
            return result;
        }
        foreach (CharacterConsumablesStackSnapshot stack in inventory.GetAllStacks())
        {
            if (stack.AvailableQuantity <= 0 || stack.Forbidden
                || !inventory.TryGetMeal(stack.ItemId, out CharacterConsumablesMealDefinitionSnapshot meal))
            {
                continue;
            }
            string destinationId = GetMealDestinationId(
                facilityId,
                stack.ItemId);
            bool isBuffer = stack.State == CharacterConsumablesStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal);
            if (bufferOnly != isBuffer || !bufferOnly
                && stack.State is not CharacterConsumablesStackState.Stored
                    and not CharacterConsumablesStackState.Loose)
            {
                continue;
            }
            bool allowed = IsMealAllowed(actor.Id, meal);
            bool contaminated = stack.Contamination > 0.01f;
            if ((!allowed || contaminated) && !emergency)
            {
                continue;
            }
            if (meal.ServingRole == MealServingRole.EmergencyOnly && !emergency)
                continue;
            CharacterMealQualityLimit authoredLimit = GetMealQualityLimit(actor.Id);
            MealQualityBand maximumQuality = authoredLimit == CharacterMealQualityLimit.Inherit
                ? MealQualityBand.Fine
                : (MealQualityBand)(int)authoredLimit;
            if (meal.QualityBand > maximumQuality)
                continue;
            float actorToFacility = ManhattanSeconds(
                actor.Position,
                facility.Position);
            float foodToFacility = isBuffer
                ? 0f
                : ManhattanSeconds(stack.Position, facility.Position) + 2f;
            float completionEta = Mathf.Max(actorToFacility, foodToFacility) + 4f;
            if (stack.RemainingFreshnessSeconds <= completionEta + 1f)
                continue;
            float baseMood = world.GetBaseMoodForMealChoice(actor.Id);
            float qualityPreference = (baseMood < 35f ? 1f : -1f)
                * (int)meal.QualityBand * 100f;
            float score = (allowed ? 10000f : 0f)
                + (world.GetCultureMealPreference(actor.Id, meal.Id)
                    == CharacterCultureMealPreference.Preferred
                        ? 1000f
                        : 0f)
                + qualityPreference
                + (1f - stack.Freshness01) * 120f
                + meal.Nutrition
                + meal.Mood * 2f;
            List<string> semanticTags = new(3);
            if (meal.Quality == MealQualityTier.Lavish)
                semanticTags.Add("consume:luxury");
            if (meal.Sweet) semanticTags.Add("food:sweet");
            if (meal.Salted) semanticTags.Add("food:salted");
            bool unfamiliar = !ReadState.CompletedOperations.Values.Any(
                operation => operation != null
                    && operation.meal
                    && string.Equals(
                        operation.characterId,
                        actor.Id.Value,
                        StringComparison.Ordinal)
                    && string.Equals(
                        operation.itemDefinitionId,
                        meal.Id.Value,
                        StringComparison.Ordinal));
            if (unfamiliar) semanticTags.Add("food:unfamiliar");
            score *= world.GetBehaviorUtilityMultiplier(
                actor.Id,
                semanticTags);
            if (contaminated)
            {
                float detection = Math.Max(
                    0f,
                    world.ProjectGameplayEffect(
                        actor.Id,
                        "food:spoilage-detection",
                        1f));
                score -= 500f * detection;
            }
            result.Add(new MealCandidate(meal, stack, score, completionEta));
        }
        float bestCompletionEta = result.Count == 0
            ? float.PositiveInfinity
            : result.Min(candidate => candidate.CompletionEta);
        List<MealCandidate> preciseCandidates = result
            .Where(candidate => candidate.CompletionEta <= bestCompletionEta + 8f)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Definition.Id.Value, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Stack.StackId.Value, StringComparer.Ordinal)
            .Take(7)
            .ToList();
        if (preciseCandidates.Count == 0)
            return preciseCandidates;

        // A facility-availability query answers whether this exact buffer owns
        // a physically consumable serving. It must not turn a transient path
        // budget deferral into DeliveryPending; navigation is revalidated by
        // candidate commit and by the meal action itself.
        if (!requireExactRoute)
            return preciseCandidates;

        CharacterMealRouteStatus actorRouteStatus = world.GetMealRouteStatus(
            actor.Id,
            actor.Position,
            facility.Position,
            out float actorToFacilityExact);
        if (actorRouteStatus == CharacterMealRouteStatus.Pending)
        {
            routePending = true;
            return new List<MealCandidate>();
        }
        if (actorRouteStatus != CharacterMealRouteStatus.Reachable)
            return new List<MealCandidate>();

        List<MealCandidate> exact = new(preciseCandidates.Count);
        foreach (MealCandidate candidate in preciseCandidates)
        {
            bool isBuffer = candidate.Stack.State
                == CharacterConsumablesStackState.FacilityBuffer;
            float foodToFacilityExact = 0f;
            if (!isBuffer)
            {
                CharacterMealRouteStatus foodRouteStatus = world.GetMealRouteStatus(
                    actor.Id,
                    candidate.Stack.Position,
                    facility.Position,
                    out foodToFacilityExact);
                if (foodRouteStatus == CharacterMealRouteStatus.Pending)
                {
                    routePending = true;
                    continue;
                }
                if (foodRouteStatus != CharacterMealRouteStatus.Reachable)
                    continue;
                foodToFacilityExact += 2f;
            }

            float completionEta = Mathf.Max(
                    actorToFacilityExact,
                    foodToFacilityExact)
                + 4f;
            if (candidate.Stack.RemainingFreshnessSeconds
                <= completionEta + 1f)
            {
                continue;
            }
            float rotRescueBonus = 8f * Mathf.Clamp01(
                1f - candidate.Stack.RemainingFreshnessSeconds / 18f);
            float finalScore = candidate.Score
                - (completionEta - rotRescueBonus) * 10f;
            exact.Add(new MealCandidate(
                candidate.Definition,
                candidate.Stack,
                finalScore,
                completionEta));
        }
        return exact
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Definition.Id.Value, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Stack.StackId.Value, StringComparer.Ordinal)
            .ToList();
    }

    private static float ManhattanSeconds(Vector2Int from, Vector2Int to) =>
        Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);

    private float GetEffectMultiplier(CharacterId characterId, bool workEffect)
    {
        if (!characterId.IsValid)
        {
            return 1f;
        }
        double additive = 0d;
        double withdrawalPenalty = 0d;
        foreach (KeyValuePair<CharacterSubstanceKey, CharacterSubstanceState> pair
                 in ReadState.SubstanceStates
                     .Where(value => value.Key.CharacterId.Equals(characterId))
                     .OrderBy(
                         value => value.Key.ItemId.Value,
                         StringComparer.Ordinal))
        {
            if (!inventory.TryResolveSubstance(
                    pair.Key.ItemId,
                    out CharacterConsumablesSubstanceDefinitionSnapshot substance))
            {
                continue;
            }
            if (pair.Value.activeSeconds > 0f)
            {
                double effect = workEffect
                    ? substance.Definition.WorkSpeedEffect
                    : substance.Definition.CombatEffect;
                if (double.IsNaN(effect) || double.IsInfinity(effect))
                    throw new InvalidOperationException(
                        "Character substance effect must be finite: "
                        + pair.Key.ItemId.Value);
                additive += effect;
            }
            if (float.IsNaN(pair.Value.withdrawal)
                || float.IsInfinity(pair.Value.withdrawal)
                || pair.Value.withdrawal < 0f)
            {
                throw new InvalidOperationException(
                    "Character substance withdrawal must be finite and "
                    + "nonnegative: " + pair.Key.ItemId.Value);
            }
            withdrawalPenalty += pair.Value.withdrawal
                * CharacterSubstanceEffectMultiplierAuthority
                    .WithdrawalPenaltyPerPoint;
        }
        return CharacterSubstanceEffectMultiplierAuthority.Resolve(
            additive,
            withdrawalPenalty);
    }

    private bool IsMealFollowupCooldownActive(
        CharacterConsumablesActorSnapshot actor) =>
        actor.Id.IsValid
        && actor.Hunger > EmergencyHungerThreshold
        && ReadState.MealFollowupCooldownUntil.TryGetValue(
            actor.Id,
            out float until)
        && clock.Time < until;

    private CharacterSubstanceState GetWritableSubstanceState(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId)
    {
        CharacterSubstanceKey key = new(characterId, itemId);
        if (!WriteState.SubstanceStates.TryGetValue(key, out CharacterSubstanceState state))
        {
            state = new CharacterSubstanceState
            {
                characterId = characterId.Value,
                itemDefinitionId = itemId.Value
            };
            WriteState.SubstanceStates.Add(key, state);
        }
        return state;
    }

    private void RecordCompletedOperation(
        ConsumableOperationId operationId,
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        ItemStackId stackId,
        bool meal,
        bool policyViolation = false,
        bool contaminated = false)
    {
        WriteState.CompletedOperations.Add(operationId, new CharacterConsumableOperationState
        {
            operationId = operationId.Value,
            characterId = characterId.Value,
            itemDefinitionId = itemId.Value,
            itemStackId = stackId.Value,
            meal = meal,
            policyViolation = policyViolation,
            contaminated = contaminated,
            completedAt = clock.Time
        });
    }

    private void CompleteDelivery(
        CharacterId characterId,
        BuildingInstanceId buildingId,
        ConsumableItemDefinitionId itemId)
    {
        MealDeliveryRoute route = new(characterId, buildingId, itemId);
        if (WriteState.DeliveryByRoute.Remove(route, out ConsumableDeliveryId deliveryId))
        {
            WriteState.PendingDeliveries.Remove(deliveryId);
        }
    }

    private void PruneExpiredDeliveries(bool forceExpirySweep = false)
    {
        CharacterConsumablesAggregateState snapshot = ReadState;
        if (snapshot.PendingDeliveries.Count == 0
            && snapshot.DeliveryByRoute.Count == 0)
        {
            return;
        }

        HashSet<CharacterId> liveCharacters = world.CharacterIds
            .Where(id => id.IsValid)
            .ToHashSet();
        HashSet<BuildingInstanceId> liveFacilities = world.FacilityIds
            .Where(id => id.IsValid)
            .ToHashSet();
        ConsumableDeliveryId[] orphanedDeliveries = snapshot.PendingDeliveries
            .Where(pair => pair.Value == null
                || !liveCharacters.Contains(pair.Value.CharacterId)
                || !liveFacilities.Contains(pair.Value.BuildingInstanceId))
            .Select(pair => pair.Key)
            .ToArray();
        MealDeliveryRoute[] staleRoutes = snapshot.DeliveryByRoute
            .Where(pair => !snapshot.PendingDeliveries.TryGetValue(
                    pair.Value,
                    out CharacterMealDeliveryState delivery)
                || delivery == null
                || !CharacterConsumablesStateRules.Route(delivery)
                    .Equals(pair.Key))
            .Select(pair => pair.Key)
            .ToArray();
        bool expiryDue = forceExpirySweep
            || clock.Time >= snapshot.NextDeliveryPruneAt;
        if (orphanedDeliveries.Length == 0
            && staleRoutes.Length == 0
            && !expiryDue)
        {
            return;
        }

        CharacterConsumablesAggregateState state = WriteState;
        foreach (ConsumableDeliveryId deliveryId in orphanedDeliveries)
        {
            if (state.PendingDeliveries.Remove(
                    deliveryId,
                    out CharacterMealDeliveryState orphaned))
            {
                state.DeliveryByRoute.Remove(
                    CharacterConsumablesStateRules.Route(orphaned));
            }
        }
        foreach (MealDeliveryRoute route in staleRoutes)
        {
            state.DeliveryByRoute.Remove(route);
        }
        if (!expiryDue)
        {
            return;
        }

        state.NextDeliveryPruneAt = clock.Time + 1f;
        foreach (CharacterMealDeliveryState delivery in state.PendingDeliveries
                     .Values.Where(value => value != null
                         && clock.Time >= value.retryAfter).ToArray())
        {
            string destinationId = GetMealDestinationId(
                delivery.BuildingInstanceId,
                delivery.ItemDefinitionId);
            if (HasRoutedItem(destinationId, delivery.ItemDefinitionId))
            {
                delivery.retryAfter = clock.Time + DeliveryRetrySeconds;
                continue;
            }
            state.PendingDeliveries.Remove(delivery.DeliveryId);
            state.DeliveryByRoute.Remove(CharacterConsumablesStateRules.Route(delivery));
        }
    }

    private CharacterConsumablesSubstanceResult FailedSubstance(
        CharacterConsumablesFailureCode code,
        CharacterConsumablesSubstanceDefinitionSnapshot substance,
        ItemStackId stackId,
        params string[] parameters) =>
        new(false, code, substance, stackId, 0f, 0f, false, false, parameters);

    private static bool IsAllowedOperationId(
        ConsumableOperationId operationId,
        bool automaticOperation) =>
        automaticOperation
            ? CharacterConsumableIdContract.IsCurrentAutomaticOperation(
                operationId.Value)
            : CharacterConsumableIdContract.IsExternalOperation(operationId.Value);

    private ConsumableOperationId NewOperationId()
    {
        CharacterConsumablesAggregateState state = WriteState;
        long sequence = state.NextOperationSequence;
        if (sequence < 1L || sequence == long.MaxValue)
        {
            throw new InvalidOperationException(
                "Character consumables operation sequence is exhausted.");
        }
        state.NextOperationSequence = sequence + 1L;
        return CharacterConsumableIdContract.CreateAutomaticOperation(sequence);
    }

    private ConsumableDeliveryId NewDeliveryId()
    {
        CharacterConsumablesAggregateState state = WriteState;
        long sequence = state.NextDeliverySequence;
        if (sequence < 1L || sequence == long.MaxValue)
        {
            throw new InvalidOperationException(
                "Character consumables delivery sequence is exhausted.");
        }
        state.NextDeliverySequence = sequence + 1L;
        return CharacterConsumableIdContract.CreateAutomaticDelivery(sequence);
    }

    private CharacterConsumablesStackSnapshot FindStack(ItemStackId stackId) =>
        stackId.IsValid
            ? inventory.GetAllStacks().FirstOrDefault(stack => stack.StackId.Equals(stackId))
            : default;

    private CharacterConsumablesStackSnapshot FindAvailableSubstanceStack(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId) =>
        inventory.GetAllStacks()
            .Where(stack => stack.AvailableQuantity > 0 && !stack.Forbidden
                && stack.ItemId.Equals(itemId)
                && IsSubstanceStackAvailableToCharacter(stack, characterId))
            .OrderBy(stack => stack.State == CharacterConsumablesStackState.Carried ? 0 : 1)
            .ThenBy(stack => stack.State)
            .ThenBy(stack => stack.StackId.Value, StringComparer.Ordinal)
            .FirstOrDefault();

    private static bool IsSubstanceStackAvailableToCharacter(
        CharacterConsumablesStackSnapshot stack,
        CharacterId characterId) =>
        stack.StackId.IsValid
        && stack.AvailableQuantity > 0
        && !stack.Forbidden
        && (stack.State is CharacterConsumablesStackState.Loose
                or CharacterConsumablesStackState.Stored
            || stack.State == CharacterConsumablesStackState.Carried
            && string.Equals(
                stack.DestinationId,
                characterId.Value,
                StringComparison.Ordinal));

    private List<RecreationalSubstanceCandidate> GetRecreationalSubstanceCandidates(
        CharacterConsumablesActorSnapshot actor,
        BuildingInstanceId facilityId,
        bool bufferOnly)
    {
        List<RecreationalSubstanceCandidate> result = new();
        foreach (CharacterConsumablesStackSnapshot stack in inventory.GetAllStacks())
        {
            if (stack.AvailableQuantity <= 0 || stack.Forbidden
                || !inventory.TryResolveSubstance(
                    stack.ItemId,
                    out CharacterConsumablesSubstanceDefinitionSnapshot substance)
                || substance.Definition.UseClass != SubstanceUseClass.Recreational)
            {
                continue;
            }

            string destinationId = GetRecreationalSubstanceDestinationId(
                facilityId,
                stack.ItemId);

            bool isFacilityBuffer = stack.State == CharacterConsumablesStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal);
            if (bufferOnly != isFacilityBuffer
                || !bufferOnly && stack.State is not CharacterConsumablesStackState.Stored
                    and not CharacterConsumablesStackState.Loose)
            {
                continue;
            }

            CharacterSubstancePolicyState policy = GetSubstancePolicy(
                actor.Id,
                substance.Definition.SubstanceId);
            if (!CharacterConsumablesPolicyRules.AllowsSubstance(
                    policy,
                    medicalContext: false,
                    combatContext: false,
                    actor.Mood))
            {
                continue;
            }

            float score = substance.Definition.MoodEffect
                - substance.Definition.AddictionChance * 20f
                - substance.Definition.OverdoseChance * 30f;
            result.Add(new RecreationalSubstanceCandidate(substance, stack, score));
        }

        return result
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Substance.Id.Value, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Stack.StackId.Value, StringComparer.Ordinal)
            .ToList();
    }

    private bool TryGetActor(
        CharacterId id,
        out CharacterConsumablesActorSnapshot actor)
    {
        if (id.IsValid && world.TryGetActor(id, out actor))
        {
            return true;
        }
        actor = default;
        return false;
    }

    private bool TryGetMealFacility(
        BuildingInstanceId id,
        out CharacterConsumablesFacilitySnapshot facility)
    {
        if (id.IsValid && world.TryGetFacility(id, out facility) && facility.MealFacility)
        {
            return true;
        }
        facility = default;
        return false;
    }

    private bool TryGetRecreationalSubstanceFacility(
        BuildingInstanceId id,
        out CharacterConsumablesFacilitySnapshot facility)
    {
        if (id.IsValid
            && world.TryGetFacility(id, out facility)
            && facility.RecreationalSubstanceFacility)
        {
            return true;
        }
        facility = default;
        return false;
    }

    private bool HasRoutedItem(string destinationId, ConsumableItemDefinitionId itemId) =>
        inventory.GetAllStacks().Any(stack => stack.Quantity > 0
            && stack.ItemId.Equals(itemId)
            && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));

    private (float Urgency, string Reason) GetUseUrgency(
        CharacterConsumablesActorSnapshot actor,
        CharacterConsumablesSubstanceDefinitionSnapshot substance,
        CharacterSubstancePolicyState policy,
        CharacterSubstanceState state,
        bool medicalContext,
        int scheduleHour)
    {
        float urgency = 0f;
        string reason = string.Empty;
        if (policy.mode == SubstancePolicyMode.MedicalOnly && medicalContext)
        {
            urgency = Mathf.Lerp(0.62f, 1f, 1f - actor.Health / actor.MaxHealth);
            reason = "medical-support";
        }
        else if (policy.mode == SubstancePolicyMode.CombatOnly && actor.CombatStance)
        {
            urgency = 0.96f;
            reason = "combat-preparation";
        }
        else if (policy.mode == SubstancePolicyMode.MoodThreshold
                 && actor.Mood <= policy.moodThreshold)
        {
            urgency = Mathf.Lerp(
                0.55f,
                0.9f,
                1f - actor.Mood / Mathf.Max(1f, policy.moodThreshold));
            reason = "mood-threshold";
        }
        else if (policy.mode == SubstancePolicyMode.Scheduled
                 && scheduleHour >= policy.scheduledHour
                 && state.scheduledCooldownSeconds <= 0f)
        {
            urgency = 0.48f;
            reason = "scheduled-dose";
        }
        if (state.addicted && state.withdrawal >= 20f)
        {
            urgency = Mathf.Max(urgency, Mathf.Lerp(0.7f, 1f, state.withdrawal / 100f));
            reason = $"{substance.Definition.DisplayName}-withdrawal";
        }
        return (urgency, reason);
    }

    private int GetCurrentScheduleHour() =>
        Mathf.FloorToInt(Mathf.Max(0f, clock.Time) / GameHourSeconds) % 24;
    public static string GetMealDestinationId(
        BuildingInstanceId facilityId,
        ConsumableItemDefinitionId itemId) =>
        CharacterConsumablesInputDestinationIdentity.Build(
            CharacterConsumablesInputKind.Meal,
            facilityId,
            itemId);
    public static string GetRecreationalSubstanceDestinationId(
        BuildingInstanceId facilityId,
        ConsumableItemDefinitionId itemId) =>
        CharacterConsumablesInputDestinationIdentity.Build(
            CharacterConsumablesInputKind.RecreationalSubstance,
            facilityId,
            itemId);
    private static bool IsAvailableMealBufferStack(
        CharacterConsumablesStackSnapshot stack,
        BuildingInstanceId facilityId) =>
        stack.StackId.IsValid && stack.Quantity > 0 && !stack.Forbidden
        && stack.State == CharacterConsumablesStackState.FacilityBuffer
        && string.Equals(
            stack.DestinationId,
            GetMealDestinationId(facilityId, stack.ItemId),
            StringComparison.Ordinal);

    private readonly struct MealCandidate
    {
        internal MealCandidate(
            CharacterConsumablesMealDefinitionSnapshot definition,
            CharacterConsumablesStackSnapshot stack,
            float score,
            float completionEta)
        {
            Definition = definition;
            Stack = stack;
            Score = score;
            CompletionEta = Mathf.Max(0f, completionEta);
        }

        internal CharacterConsumablesMealDefinitionSnapshot Definition { get; }
        internal CharacterConsumablesStackSnapshot Stack { get; }
        internal float Score { get; }
        internal float CompletionEta { get; }
    }


    private readonly struct RecreationalSubstanceCandidate
    {
        internal RecreationalSubstanceCandidate(
            CharacterConsumablesSubstanceDefinitionSnapshot substance,
            CharacterConsumablesStackSnapshot stack,
            float score)
        {
            Substance = substance;
            Stack = stack;
            Score = score;
        }

        internal CharacterConsumablesSubstanceDefinitionSnapshot Substance { get; }
        internal CharacterConsumablesStackSnapshot Stack { get; }
        internal float Score { get; }
    }
}
