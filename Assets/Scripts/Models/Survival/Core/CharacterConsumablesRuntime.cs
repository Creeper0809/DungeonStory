using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public sealed class CharacterConsumablesRuntime :
    ICharacterConsumablesApplication,
    ICharacterConsumablesPersistence
{
    public const string FieldMealFacilityId = CharacterConsumableIdContract.FieldMealFacilityId;
    public const string MealPhysicalSinkReason = CharacterConsumableIdContract.MealPhysicalSinkReason;
    public const string SubstancePhysicalSinkReason =
        CharacterConsumableIdContract.SubstancePhysicalSinkReason;
    public const string DetoxPhysicalSinkReason =
        CharacterConsumableIdContract.DetoxPhysicalSinkReason;
    private const float MealFollowupCooldownSeconds = 15f;
    private const float MealActionSeconds = 4f;
    private const float DeliveryRetrySeconds = 45f;
    private const float MealDeliveryProbeSeconds = 1f;

    private readonly CharacterConsumablesPolicyRuntime policies;
    private readonly CharacterConsumablesOperationRuntime operations;
    private readonly CharacterConsumablesSubstanceRuntime substances;
    private readonly ICharacterConsumablesWorldPort world;
    private readonly ICharacterConsumablesInventoryPort inventory;
    private readonly ICharacterConsumablesEventPort events;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICharacterNeedBalanceRuntime needBalance;
    private readonly ICharacterConsumablesWorkforcePort workforce;
    private float nextMealDeliveryProbeAt;
    private readonly HashSet<ConsumableOperationId> mealEffectsInFlight = new();
#if UNITY_EDITOR
    private bool mealDeliveryPausedForDiagnostics;
    public Func<CharacterId, bool> DebugRejectMealFactPublication { get; set; }

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

    private bool NeedsRoutineMeal(CharacterConsumablesActorSnapshot actor) =>
        actor.Hunger <= RoutineHungerThreshold
        || actor.RequiresHungerRecovery
            && actor.Hunger < needBalance
                .GetResponse(CharacterCondition.HUNGER)
                .resumeTarget;

    private CharacterConsumablesAggregateState ReadState =>
        aggregateRootStore.GetOrCreate(() => new CharacterConsumablesAggregateState());
    private CharacterConsumablesAggregateState WriteState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new CharacterConsumablesAggregateState(),
            state => state.Clone());

    private float TimelineNow => ReadState.GetTimelineTime(clock.Time);

    public CharacterConsumablesRuntime(
        ICharacterConsumablesWorldPort world,
        ICharacterConsumablesInventoryPort inventory,
        ICharacterConsumablesEventPort events,
        IGameClock clock,
        IRandomStreamProvider randomStreams,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterNeedBalanceRuntime needBalance,
        ICharacterConsumablesWorkforcePort workforce = null,
        IDomainFactPublisher domainFacts = null,
        IGameCalendar calendar = null)
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
        policies = new CharacterConsumablesPolicyRuntime(
            this.world, this.inventory, this.aggregateRootStore, domainFacts, calendar);
        operations = new CharacterConsumablesOperationRuntime(this.aggregateRootStore, this.clock, domainFacts, calendar);
        substances = new CharacterConsumablesSubstanceRuntime(this.world, this.inventory, this.events,
            this.clock, random, this.aggregateRootStore, this.workforce, policies, operations);
    }

    public CharacterDietPolicyKind GetDietPolicy(CharacterId characterId) =>
        policies.GetDietPolicy(characterId);

    public CharacterConsumablesPolicyChangeResult SetDietPolicy(
        CharacterId characterId, CharacterDietPolicyKind policy, string source = "runtime-command") =>
        policies.SetDietPolicy(characterId, policy, source);

    public CharacterMealQualityLimit GetMealQualityLimit(CharacterId characterId) =>
        policies.GetMealQualityLimit(characterId);

    public CharacterConsumablesPolicyChangeResult SetMealQualityLimit(
        CharacterId characterId, CharacterMealQualityLimit qualityLimit, string source = "runtime-command") =>
        policies.SetMealQualityLimit(characterId, qualityLimit, source);

    public bool IsMealAllowed(CharacterId characterId, CharacterConsumablesMealDefinitionSnapshot meal) =>
        policies.IsMealAllowed(characterId, meal);

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
        if (!NeedsRoutineMeal(actor))
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
                return policies.IsWithinMealQualityLimit(actor.Id, meal.QualityBand)
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
        out CharacterConsumablesMealResult result) =>
        TryConsumeFieldMealInternal(
            characterId,
            stackId,
            primitiveSurvivalOutcomeRequired: false,
            out result);

    public bool TryConsumePrimitiveFieldMeal(
        CharacterId characterId,
        ItemStackId stackId,
        out CharacterConsumablesMealResult result) =>
        TryConsumeFieldMealInternal(
            characterId,
            stackId,
            primitiveSurvivalOutcomeRequired: true,
            out result);

    private bool TryConsumeFieldMealInternal(
        CharacterId characterId,
        ItemStackId stackId,
        bool primitiveSurvivalOutcomeRequired,
        out CharacterConsumablesMealResult result)
    {
        ConsumableOperationId operationId = operations.NewOperationId();
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
        if (!NeedsRoutineMeal(actor)
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
        if (!policies.IsWithinMealQualityLimit(actor.Id, meal.QualityBand))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                meal.Id.Value, "meal-quality-limit");
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
            inventory.ReleaseMealQuantity(operationId);
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
            createdAt = TimelineNow,
            leaseExpiresAt = TimelineNow,
            expectedCompletionEta = MealActionSeconds,
            physicalConsumptionCommitted = true,
            physicalContextFrozen = true,
            consumptionPosition = actor.Position,
            hungerBefore = actor.Hunger,
            hungerAfter = actor.Hunger,
            healthBefore = actor.Health,
            healthAfter = actor.Health,
            moodBefore = actor.Mood,
            moodAfter = actor.Mood,
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
            committedContaminated = contaminated,
            primitiveSurvivalOutcomeRequired =
                primitiveSurvivalOutcomeRequired
        };
        WriteState.ActiveMealPlans.Add(operationId, plan);
        ConsumeMealCommand command = new(
            operationId,
            characterId,
            fieldFacility,
            stackId);
        TryFinalizeCommittedMealPlan(command, plan);
        if (WriteState.CompletedOperations.TryGetValue(
                operationId,
                out CharacterConsumableOperationState completed)
            && !string.IsNullOrWhiteSpace(completed.outcomeResultKey))
        {
            result = CharacterConsumablesMealResult.Consumed(
                operationId,
                meal,
                stackId,
                !policyAllowed,
                contaminated,
                completed.outcomeResultKey);
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
        if (!NeedsRoutineMeal(actor))
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
                operations.NewOperationId(),
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

    public bool TryConsumePermittedMeal(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesMealResult result)
    {
        if (!operationId.IsValid
            || !CharacterConsumablesOperationRuntime.IsAllowedOperationId(operationId, automaticOperation: false)
            || !characterId.IsValid
            || !facilityId.IsValid)
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.InvalidCommand,
                operationId.Value);
            return false;
        }

        if (ReadState.CompletedOperations.TryGetValue(
                operationId,
                out CharacterConsumableOperationState completed))
        {
            if (completed == null
                || !completed.meal
                || !completed.CharacterId.Equals(characterId)
                || completed.policyViolation
                || completed.contaminated
                || !inventory.TryGetMeal(
                    completed.ItemDefinitionId,
                    out CharacterConsumablesMealDefinitionSnapshot meal))
            {
                result = CharacterConsumablesMealResult.Failed(
                    CharacterConsumablesFailureCode.AlreadyProcessed,
                    operationId.Value,
                    "permitted-meal-operation-payload-conflict");
                return false;
            }
            result = CharacterConsumablesMealResult.Consumed(
                operationId,
                meal,
                completed.ItemStackId,
                policyViolation: false,
                contaminated: false,
                completed.outcomeResultKey);
            return true;
        }

        if (ReadState.ActiveMealPlans.TryGetValue(
                operationId,
                out CharacterMealPlan active))
        {
            if (active == null
                || !string.Equals(
                    active.characterId,
                    characterId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    active.facilityInstanceId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            {
                result = CharacterConsumablesMealResult.Failed(
                    CharacterConsumablesFailureCode.AlreadyProcessed,
                    operationId.Value,
                    "permitted-meal-operation-payload-conflict");
                return false;
            }
            return TryGetMealOperationResult(operationId, out result)
                && result.Success;
        }

        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
            || !TryGetMealFacility(facilityId, out _))
        {
            result = CharacterConsumablesMealResult.Failed(
                !actor.Id.IsValid
                    ? CharacterConsumablesFailureCode.CharacterMissing
                    : CharacterConsumablesFailureCode.FacilityMissing,
                !actor.Id.IsValid ? characterId.Value : facilityId.Value);
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

        List<MealCandidate> candidates = GetMealCandidates(
            actor,
            facilityId,
            bufferOnly: true,
            emergency: false,
            out bool routePending);
        if (candidates.Count == 0)
        {
            bool deliveryRoutePending = false;
            if (routePending
                || TryRequestMealDelivery(
                    actor,
                    facilityId,
                    emergency: false,
                    out deliveryRoutePending)
                || deliveryRoutePending)
            {
                result = CharacterConsumablesMealResult.Pending(
                    operationId,
                    default,
                    default,
                    "permitted-meal-delivery-pending",
                    characterId.Value,
                    facilityId.Value);
                return false;
            }
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.ItemStackMissing,
                facilityId.Value,
                "permitted-clean-policy-meal-missing");
            return false;
        }

        return TryConsumeMeal(
            new ConsumeMealCommand(
                operationId,
                characterId,
                facilityId,
                candidates[0].Stack.StackId),
            automaticOperation: false,
            out result);
    }

    private bool TryConsumeMeal(
        ConsumeMealCommand command,
        bool automaticOperation,
        out CharacterConsumablesMealResult result)
    {
        if (!command.IsValid
            || !CharacterConsumablesOperationRuntime.IsAllowedOperationId(command.OperationId, automaticOperation))
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
        if (ReadState.MealFailures.TryGetValue(command.OperationId, out var previousFailure)
            && !previousFailure.recoveryPending)
        {
            result = previousFailure.characterId == command.CharacterId.Value
                && previousFailure.facilityInstanceId == command.FacilityId.Value
                ? CharacterConsumablesMealResult.Aborted(command.OperationId,
                    previousFailure.failureCode, previousFailure.detail)
                : CharacterConsumablesMealResult.Failed(CharacterConsumablesFailureCode.InvalidCommand,
                    command.OperationId.Value, "meal-operation-owner-mismatch");
            return false;
        }
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
            && !NeedsRoutineMeal(actor))
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
        if (!policies.IsWithinMealQualityLimit(actor.Id, meal.QualityBand))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PolicyForbidden,
                meal.Id.Value, "meal-quality-limit");
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
            createdAt = TimelineNow,
            leaseExpiresAt = TimelineNow + 15f,
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
            if (!string.IsNullOrEmpty(completed.mealEffectsSkippedReason))
            {
                result = CharacterConsumablesMealResult.Aborted(operationId,
                    CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                    completed.mealEffectsSkippedReason, completed.outcomeResultKey);
                return false;
            }
            result = CharacterConsumablesMealResult.Consumed(
                operationId,
                completedMeal,
                completed.ItemStackId,
                completed.policyViolation,
                completed.contaminated,
                completed.outcomeResultKey);
            return true;
        }

        result = ReadState.MealFailures.TryGetValue(operationId, out var failure)
            && !failure.recoveryPending
            ? CharacterConsumablesMealResult.Aborted(operationId, failure.failureCode, failure.detail)
            : CharacterConsumablesMealResult.Failed(CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                operationId.Value, "meal-operation-missing-or-aborted");
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
                or CharacterMealPlanPhase.EffectsApplied
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
                    if (TimelineNow >= plan.leaseExpiresAt)
                        AbortMealPlan(command, plan);
                    continue;
                }
                plan.mealQuantityLeaseId = reboundLeaseId;
                plan.transportStackId = reboundStackId.Value;
                plan.leaseExpiresAt = Math.Max(
                    plan.leaseExpiresAt,
                    TimelineNow + 15f);
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
            if (TimelineNow >= plan.leaseExpiresAt)
            {
                AbortMealPlan(command, plan);
                continue;
            }
            if (TimelineNow < plan.createdAt + MealActionSeconds)
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

        if (!policies.IsWithinMealQualityLimit(actor.Id, meal.QualityBand))
        {
            AbortMealPlan(command, plan,
                CharacterConsumablesFailureCode.PolicyForbidden,
                "meal-quality-limit-at-commit");
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
        plan.physicalContextFrozen = true;
        plan.consumptionPosition = actor.Position;
        plan.hungerBefore = plan.hungerAfter = actor.Hunger;
        plan.healthBefore = plan.healthAfter = actor.Health;
        plan.moodBefore = plan.moodAfter = actor.Mood;
        FreezeMealContaminationRoll(command.CharacterId, plan);
        plan.transportStackId = commitStackId.Value;
        return TryFinalizeCommittedMealPlan(command, plan);
    }

    private bool TryFinalizeCommittedMealPlan(
        ConsumeMealCommand command,
        CharacterMealPlan plan)
    {
        if (plan == null
            || plan.phase is not (CharacterMealPlanPhase.ItemCommitted
                or CharacterMealPlanPhase.EffectsApplied
                or CharacterMealPlanPhase.EffectsPublished))
        {
            return false;
        }

        if (plan.phase == CharacterMealPlanPhase.ItemCommitted)
        {
            if (!TryGetActor(command.CharacterId, out var currentActor) || !currentActor.Active)
                FreezeSkippedMealEffects(plan, "meal-actor-unavailable-after-physical");
        }

        if (plan.phase == CharacterMealPlanPhase.ItemCommitted)
        {
            CharacterConsumablesAggregateState readState = ReadState;
            if (readState.CompletedOperations.ContainsKey(command.OperationId))
            {
                WriteState.ActiveMealPlans[command.OperationId].phase =
                    CharacterMealPlanPhase.EffectsPublished;
            }
            else
            {
                if (!TryGetActor(
                        command.CharacterId,
                        out CharacterConsumablesActorSnapshot actor)
                    || !actor.Active
                    || !inventory.TryGetMeal(
                        new ConsumableItemDefinitionId(plan.itemDefinitionId),
                        out CharacterConsumablesMealDefinitionSnapshot meal))
                {
                    return false;
                }
                FreezeMealContaminationRoll(command.CharacterId, plan);
                CharacterConsumablesAggregateState rollback = readState.Clone();
                CharacterConsumablesAggregateState writableState = WriteState;
                if (!writableState.ActiveMealPlans.TryGetValue(
                        command.OperationId,
                        out CharacterMealPlan writablePlan)
                    || writablePlan == null)
                {
                    return false;
                }
                if (meal.ServingRole is MealServingRole.Snack
                    or MealServingRole.LightMeal)
                {
                    writableState.MealFollowupCooldownUntil[command.CharacterId] =
                        TimelineNow + MealFollowupCooldownSeconds;
                }
                CompleteDelivery(command.CharacterId, command.FacilityId, meal.Id);
                bool effectsApplied;
                CharacterConsumablesEffectEvidence effectEvidence;
                string effectFailure;
                mealEffectsInFlight.Add(command.OperationId);
                try
                {
                    effectsApplied = TryApplyMealEffects(
                        command,
                        meal,
                        writablePlan,
                        out effectEvidence,
                        out effectFailure);
                }
                finally
                {
                    mealEffectsInFlight.Remove(command.OperationId);
                }
                if (!effectsApplied)
                {
                    aggregateRootStore.Replace(rollback);
                    Debug.LogError(
                        "character-consumables-meal-effects-failed:"
                        + effectFailure);
                    return false;
                }
                writablePlan.effectEvidenceFrozen = true;
                writablePlan.effectsSkippedReason = string.Empty;
                writablePlan.hungerBefore = effectEvidence.HungerBefore;
                writablePlan.hungerAfter = effectEvidence.HungerAfter;
                writablePlan.healthBefore = effectEvidence.HealthBefore;
                writablePlan.healthAfter = effectEvidence.HealthAfter;
                writablePlan.moodBefore = effectEvidence.MoodBefore;
                writablePlan.moodAfter = effectEvidence.MoodAfter;
                writablePlan.phase = CharacterMealPlanPhase.EffectsApplied;
            }
        }

        if (plan.phase == CharacterMealPlanPhase.EffectsApplied)
        {
            if (!inventory.TryGetMeal(new ConsumableItemDefinitionId(plan.itemDefinitionId), out var meal)
                || !operations.TryGetConsumptionFactDay(out int day))
                return false;
            bool recipientActive = world.CanReceiveMealPostCommitObservers(command.CharacterId);
            var fact = new CharacterMealConsumedFact(command.OperationId, command.CharacterId, day,
                CreateMealConsumptionEvidence(command, plan), recipientActive);
            if (!operations.TryReserveConsumptionFact(fact, out var lease)) return false;
            bool publishPrimitive = plan.primitiveSurvivalOutcomeRequired
                && string.IsNullOrEmpty(plan.effectsSkippedReason);
            DomainFactLease primitiveLease = default;
            CharacterPrimitiveMealCompletedFact primitiveFact = null;
            if (publishPrimitive)
            {
                primitiveFact = new CharacterPrimitiveMealCompletedFact(
                    command.OperationId, command.CharacterId, day, meal.Nutrition);
                if (!operations.TryReserveConsumptionFact(primitiveFact, out primitiveLease))
                { operations.CancelFact(lease); return false; }
            }
            CharacterConsumablesAggregateState rollback = ReadState.Clone();
            bool publicationStarted = false;
            try
            {
                var writable = WriteState.ActiveMealPlans[command.OperationId];
                writable.factAbsoluteDay = day;
                writable.publicationKey = lease.PublicationKey;
                writable.primitivePublicationKey = primitiveLease.PublicationKey;
                writable.primitiveNutrition = publishPrimitive ? meal.Nutrition : 0f;
                writable.recipientActiveAtCompletion = recipientActive;
                writable.publicationPending = true;
                var stack = new ItemStackId(plan.physicalCommitSourceStackIds.First());
                operations.RecordCompletedOperation(command.OperationId, command.CharacterId, meal.Id, stack,
                    true, plan.committedPolicyViolation, plan.committedContaminated,
                    facilityId: command.FacilityId);
                operations.SetMealEffectsSkippedReason(command.OperationId, plan.effectsSkippedReason);
                writable.phase = CharacterMealPlanPhase.EffectsPublished;
                var emissions = publishPrimitive
                    ? new[] { DomainFactEmission.Create(lease, fact), DomainFactEmission.Create(primitiveLease, primitiveFact) }
                    : new[] { DomainFactEmission.Create(lease, fact) };
#if UNITY_EDITOR
                if (DebugRejectMealFactPublication?.Invoke(command.CharacterId) == true)
                {
                    operations.CancelFact(lease);
                    operations.CancelFact(primitiveLease);
                    aggregateRootStore.Replace(rollback);
                    Debug.LogError("character-consumables-fact-publication-rejected:debug-meal-publication-rejection");
                    return false;
                }
#endif
                publicationStarted = true;
                if (!operations.PublishConsumptionFacts(emissions, rollback)) return false;
                operations.SetOutcomeResultKey(command.OperationId, writable.publicationKey);
                writable.publicationPending = false;
            }
            catch
            {
                // Before publication starts cancellation proves no observation
                // committed. Afterwards retain the saved fact for exact recovery.
                if (!publicationStarted)
                {
                    operations.CancelFact(lease);
                    operations.CancelFact(primitiveLease);
                    aggregateRootStore.Replace(rollback);
                }
                throw;
            }
        }

        CharacterConsumablesAggregateState state = WriteState;
        if (!state.ActiveMealPlans.TryGetValue(
                command.OperationId,
                out CharacterMealPlan activePlan)
            || activePlan == null)
        {
            return false;
        }
        if (activePlan.publicationPending)
        {
            var fact = new CharacterMealConsumedFact(command.OperationId, command.CharacterId,
                activePlan.factAbsoluteDay, CreateMealConsumptionEvidence(command, activePlan),
                activePlan.recipientActiveAtCompletion);
            var replays = new List<DomainFactReplay> { DomainFactReplay.Create(activePlan.publicationKey, fact) };
            if (!string.IsNullOrEmpty(activePlan.primitivePublicationKey))
                replays.Add(DomainFactReplay.Create(activePlan.primitivePublicationKey,
                    new CharacterPrimitiveMealCompletedFact(command.OperationId, command.CharacterId,
                        activePlan.factAbsoluteDay, activePlan.primitiveNutrition)));
            if (!operations.ReplayConsumptionFacts(replays.ToArray())) return false;
            operations.SetOutcomeResultKey(command.OperationId, activePlan.publicationKey);
            activePlan.publicationPending = false;
        }
        if (!activePlan.postCommitEventDelivered)
        {
            if (!inventory.TryGetMeal(new ConsumableItemDefinitionId(activePlan.itemDefinitionId), out var meal))
                return false;
            // This existing live event is best effort, unlike durable fact
            // publication. Mark before callbacks to prevent reentrant repeats.
            activePlan.postCommitEventDelivered = true;
            if (string.IsNullOrEmpty(activePlan.effectsSkippedReason)
                && activePlan.recipientActiveAtCompletion
                && world.CanReceiveMealPostCommitObservers(command.CharacterId))
            {
                var result = CharacterConsumablesMealResult.Consumed(command.OperationId, meal,
                    new ItemStackId(activePlan.physicalCommitSourceStackIds.First()),
                    activePlan.committedPolicyViolation, activePlan.committedContaminated,
                    state.CompletedOperations[command.OperationId].outcomeResultKey);
                CharacterConsumablesOperationRuntime.PublishPostCommitObserver(() => events.Publish(new CharacterConsumablesMealConsumedEvent(
                    command.OperationId, command.CharacterId, command.FacilityId, result)),
                    "character-consumables-meal-post-commit-observer");
            }
            else if (string.IsNullOrEmpty(activePlan.effectsSkippedReason))
                Debug.Log("character-consumables-meal-live-observers-retired:recipient-retired:"
                    + command.OperationId.Value + ":" + command.CharacterId.Value);
        }
        // The physical meal and its canonical result are already committed.
        // Retain this saved plan until the fasting owner has accepted that exact
        // result; retries never repeat nutrition, consumption or the meal event.
        if (!activePlan.ritualFastingConsequenceDelivered)
        {
            if (string.IsNullOrEmpty(activePlan.effectsSkippedReason)
                && world.CanReceiveMealPostCommitObservers(command.CharacterId)
                && world is ICharacterRitualFastingMealPort fasting)
            {
                if (!state.CompletedOperations.TryGetValue(command.OperationId, out var completed)
                    || string.IsNullOrWhiteSpace(completed.outcomeResultKey))
                {
                    Debug.LogError("character-consumables-fasting-consequence-pending:committed-meal-result-key-missing");
                    return false;
                }
                string failureReason;
                try
                {
                    if (!fasting.RecordMealConsumed(command.CharacterId, !activePlan.automaticOperation,
                            new CharacterMealConsumedFact(command.OperationId, command.CharacterId,
                                activePlan.factAbsoluteDay, CreateMealConsumptionEvidence(command, activePlan),
                                activePlan.recipientActiveAtCompletion), completed.outcomeResultKey, out failureReason))
                    {
                        Debug.LogError("character-consumables-fasting-consequence-pending:" + failureReason);
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("character-consumables-fasting-consequence-pending:" + exception);
                    return false;
                }
            }
            activePlan.ritualFastingConsequenceDelivered = true;
        }
        if (!inventory.TryAcknowledgeMealConsumption(
                command.CharacterId,
                new ConsumableItemDefinitionId(activePlan.itemDefinitionId),
                activePlan.physicalCommitQuantity,
                activePlan.physicalCommitId,
                activePlan.consumptionPosition,
                out _))
        {
            return false;
        }

        activePlan.phase = CharacterMealPlanPhase.Completed;
        state.ActiveMealPlans.Remove(command.OperationId);
        state.MealFailures.Remove(command.OperationId);
        // Facility-slot ownership is transient and is deliberately not restored.
        // Releasing by the stable operation/facility pair is idempotent, and is
        // required to clear an owner retained by the pre-restore world adapter.
        // The virtual field-meal facility has no owner entry, so the same call is
        // also a safe no-op for primitive meals.
        world.ReleaseMealFacilitySlot(command.OperationId, command.FacilityId);
        activePlan.facilitySlotReserved = false;
        return true;
    }

    private void AbortMealPlan(
        ConsumeMealCommand command,
        CharacterMealPlan plan,
        CharacterConsumablesFailureCode failureCode =
            CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
        string failureDetail = "meal-operation-aborted")
    {
        if (plan != null
            && (plan.physicalConsumptionCommitted
                || plan.phase is CharacterMealPlanPhase.ItemCommitted
                    or CharacterMealPlanPhase.EffectsApplied
                    or CharacterMealPlanPhase.EffectsPublished))
        {
            if (plan.phase == CharacterMealPlanPhase.ItemCommitted
                && !mealEffectsInFlight.Contains(command.OperationId))
                FreezeSkippedMealEffects(plan, failureDetail);
            RememberMealFailure(command, failureCode,
                "post-physical-recovery-required:" + failureDetail, recoveryPending: true);
            world.ReleaseMealFacilitySlot(
                command.OperationId,
                command.FacilityId);
            plan.facilitySlotReserved = false;
            return;
        }
        if (plan != null)
        {
            plan.phase = CharacterMealPlanPhase.Aborted;
            if (!plan.physicalConsumptionCommitted)
                inventory.ReleaseMealQuantity(command.OperationId);
        }
        WriteState.ActiveMealPlans.Remove(command.OperationId);
        RememberMealFailure(command, failureCode, failureDetail, recoveryPending: false);
        world.ReleaseMealFacilitySlot(command.OperationId, command.FacilityId);
    }

    private static void FreezeSkippedMealEffects(CharacterMealPlan plan, string reason)
    {
        if (!plan.physicalContextFrozen)
            throw new InvalidOperationException("Meal cancellation requires its physical context.");
        // EffectsApplied is the durable effect-decision boundary. A skipped decision
        // carries zero deltas and an explicit partial outcome, never meal success.
        plan.effectsSkippedReason = reason;
        plan.effectEvidenceFrozen = true;
        plan.phase = CharacterMealPlanPhase.EffectsApplied;
    }

    private static bool CanRecoverMealWithoutActor(CharacterMealPlan plan) =>
        plan != null && plan.physicalContextFrozen && plan.effectEvidenceFrozen
        && plan.phase is CharacterMealPlanPhase.EffectsApplied or CharacterMealPlanPhase.EffectsPublished;

    private void RememberMealFailure(ConsumeMealCommand command,
        CharacterConsumablesFailureCode code, string detail, bool recoveryPending)
    {
        // A facility can still reference an aborted operation at a save boundary.
        // Like completed operations, retain its receipt for the persistent actor's
        // lifetime; unrelated failures must not evict it from a diagnostic cache.
        WriteState.MealFailures[command.OperationId] = new CharacterMealOperationFailureState
        {
            operationId = command.OperationId.Value,
            characterId = command.CharacterId.Value,
            facilityInstanceId = command.FacilityId.Value,
            failureCode = code,
            detail = detail,
            recoveryPending = recoveryPending
        };
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
        int cancelled = 0;
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
            if (!WriteState.ActiveMealPlans.ContainsKey(operationId))
            {
                cancelled++;
            }
        }

        return cancelled;
    }

    public CharacterSubstancePolicyState GetSubstancePolicy(CharacterId characterId, string substanceId) =>
        policies.GetSubstancePolicy(characterId, substanceId);

    public CharacterConsumablesPolicyChangeResult SetSubstancePolicy(
        CharacterId characterId, string substanceId, SubstancePolicyMode mode,
        float moodThreshold, int scheduledHour, string source = "runtime-command") =>
        policies.SetSubstancePolicy(characterId, substanceId, mode, moodThreshold, scheduledHour, source);

    public CharacterSubstanceState GetSubstanceState(
        CharacterId characterId,
        string substanceId) =>
        substances.GetSubstanceState(characterId, substanceId);

    public CharacterToxicityStatus GetToxicityStatus(CharacterId characterId) =>
        substances.GetToxicityStatus(characterId);

    public bool TryApplyDetoxTreatment(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterDetoxTreatmentResult result) =>
        substances.TryApplyDetoxTreatment(operationId, characterId, facilityId, out result);

    public bool TryGetPendingDetoxTreatment(
        CharacterId characterId,
        out BuildingInstanceId facilityId,
        out ConsumableOperationId operationId) =>
        substances.TryGetPendingDetoxTreatment(characterId, out facilityId, out operationId);

    public bool TryGetPendingDetoxTreatment(
        CharacterId characterId,
        out BuildingInstanceId facilityId,
        out ConsumableOperationId operationId,
        out ConsumableItemDefinitionId itemId) =>
        substances.TryGetPendingDetoxTreatment(characterId, out facilityId, out operationId, out itemId);

    public bool TryGetDetoxTreatmentOwner(
        ConsumableOperationId operationId,
        out CharacterId characterId,
        out BuildingInstanceId facilityId,
        out bool completed) =>
        substances.TryGetDetoxTreatmentOwner(operationId, out characterId, out facilityId, out completed);

    public bool TryAcknowledgeDetoxTreatment(
        ConsumableOperationId operationId) =>
        substances.TryAcknowledgeDetoxTreatment(operationId);

    public void ProcessOperatingDay(int day) =>
        substances.ProcessOperatingDay(day);

    public bool TryConsumeSubstance(
        CharacterId characterId,
        string substanceId,
        bool medicalContext,
        bool combatContext,
        out CharacterConsumablesSubstanceResult result) =>
        substances.TryConsumeSubstance(characterId, substanceId, medicalContext, combatContext, out result);

    public bool TryConsumeRecreationalSubstance(
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterConsumablesSubstanceResult result) =>
        substances.TryConsumeRecreationalSubstance(characterId, facilityId, out result);

    public bool TryConsumeSubstance(
        ConsumeSubstanceByIdCommand command,
        out CharacterConsumablesSubstanceResult result) =>
        substances.TryConsumeSubstance(command, out result);

    public bool TryGetAutomaticUseRequest(
        CharacterId characterId,
        out CharacterConsumablesUseRequest request) =>
        substances.TryGetAutomaticUseRequest(characterId, out request);

    public float GetWorkSpeedMultiplier(CharacterId characterId) =>
        substances.GetWorkSpeedMultiplier(characterId);
    public float GetCombatMultiplier(CharacterId characterId) =>
        substances.GetCombatMultiplier(characterId);
    public float GetFatigueAccumulationMultiplier(CharacterId characterId) =>
        substances.GetFatigueAccumulationMultiplier(characterId);
    public float GetResearchSpeedMultiplier(CharacterId characterId) =>
        substances.GetResearchSpeedMultiplier(characterId);
    public bool SuppressesPerceivedPain(CharacterId characterId) =>
        substances.SuppressesPerceivedPain(characterId);

    public void Tick()
    {
        if (!ResumePendingPolicyFact(out string policyFailure))
            throw new InvalidOperationException("Consumables policy publication pending: " + policyFailure);
        substances.AdvancePendingPlans();
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
        substances.AdvanceTimedEffects(deltaTime);
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
                && NeedsRoutineMeal(value)
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
                && !persistent.Contains(new CharacterId(pair.Value.characterId))
                && !(CanRecoverMealWithoutActor(pair.Value)
                    || pair.Value.phase == CharacterMealPlanPhase.ItemCommitted
                        && pair.Value.physicalContextFrozen))
            .Select(pair => pair.Key.Value)
            .Concat(snapshot.ActiveSubstanceUsePlans
                .Where(pair => pair.Value != null
                    && !persistent.Contains(new CharacterId(pair.Value.characterId)))
                .Select(pair => pair.Key.Value))
            .Concat(snapshot.ActiveDetoxTreatmentPlans
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

        // The physical child outlives its actor. Resolve only the still-unapplied
        // effect decision here; publication/ACK remain normal retryable commands.
        foreach (var pair in WriteState.ActiveMealPlans)
        {
            if (!persistent.Contains(new CharacterId(pair.Value.characterId))
                && pair.Value.phase == CharacterMealPlanPhase.ItemCommitted)
                FreezeSkippedMealEffects(pair.Value, "meal-owner-not-persistent-after-physical");
        }
        snapshot = ReadState;

        ConsumableDeliveryId[] orphanedDeliveries = snapshot.PendingDeliveries
            .Where(pair => pair.Value == null
                || !persistent.Contains(pair.Value.CharacterId))
            .Select(pair => pair.Key)
            .ToArray();
        policies.ReconcilePersistentActors(persistent);
        substances.ReconcilePersistentActors(persistent);
        operations.ReconcilePersistentActors(persistent, snapshot.ActiveMealPlans.Keys.ToArray());
        CharacterId[] orphanedCooldowns = snapshot.MealFollowupCooldownUntil.Keys
            .Where(id => !persistent.Contains(id))
            .ToArray();
        ConsumableOperationId[] orphanedMealFailures = snapshot.MealFailures
            .Where(pair => !persistent.Contains(pair.Value.CharacterId)
                && !snapshot.ActiveMealPlans.ContainsKey(pair.Key))
            .Select(pair => pair.Key).ToArray();

        if (orphanedDeliveries.Length == 0
            && orphanedMealFailures.Length == 0
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
        foreach (ConsumableOperationId id in orphanedMealFailures)
            state.MealFailures.Remove(id);
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
        return CharacterConsumablesStateRules.Capture(ReadState, TimelineNow);
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
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        candidate.State.BindRuntimeClock(clock.Time);
        aggregateRootStore.Replace(candidate.State);
    }

    private void FreezeMealContaminationRoll(
        CharacterId characterId,
        CharacterMealPlan plan)
    {
        if (plan == null || plan.contaminationRollFrozen)
        {
            return;
        }
        float poisoningChance = plan.committedContaminated
            ? Mathf.Clamp01(world.ProjectGameplayEffect(
                characterId,
                GameplayEffectTargetIds.FoodPoisoningChance,
                1f))
            : 0f;
        plan.resolvedContaminationDamage = plan.committedContaminated
            && random.Chance(poisoningChance);
        plan.contaminationRollFrozen = true;
    }

    private bool TryApplyMealEffects(
        ConsumeMealCommand command,
        CharacterConsumablesMealDefinitionSnapshot meal,
        CharacterMealPlan plan,
        out CharacterConsumablesEffectEvidence evidence,
        out string failureReason)
    {
        float mood = meal.Mood - (plan.committedPolicyViolation ? 9f : 0f);
        CharacterMealEffectCommand effect = new(
            command.OperationId,
            command.CharacterId,
            meal.Nutrition,
            mood,
            plan.committedPolicyViolation,
            plan.resolvedContaminationDamage,
            meal.Id.Value,
            meal.DisplayName);
        if (world is ICharacterConsumablesEffectTransactionPort transaction)
        {
            return transaction.TryApplyMealEffects(
                effect,
                out evidence,
                out failureReason);
        }
        world.TryGetActor(
            command.CharacterId,
            out CharacterConsumablesActorSnapshot before);
        try
        {
            world.RecoverHunger(command.CharacterId, meal.Nutrition);
            if (plan.committedPolicyViolation)
            {
                world.RecordNeedNarrative(
                    command.CharacterId,
                    "diet:emergency-violation",
                    meal.Id.Value,
                    "survived",
                    1f);
            }
            if (!Mathf.Approximately(mood, 0f))
            {
                world.ApplyBestMealMood(
                    command.CharacterId,
                    $"Ate {meal.DisplayName}",
                    mood,
                    180f);
            }
            if (plan.resolvedContaminationDamage)
            {
                world.ApplyContaminatedMealDamage(
                    command.CharacterId,
                    3f,
                    "contaminated meal");
            }
            world.TryGetActor(
                command.CharacterId,
                out CharacterConsumablesActorSnapshot after);
            evidence = new CharacterConsumablesEffectEvidence(
                before.Hunger,
                after.Hunger,
                before.Health,
                after.Health,
                before.Mood,
                after.Mood);
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            evidence = default;
            failureReason = "meal-effect-transaction-unavailable:"
                + exception.GetType().Name;
            return false;
        }
    }

    private static CharacterConsumablesOutcomeEvidence CreateMealConsumptionEvidence(
        ConsumeMealCommand command,
        CharacterMealPlan plan)
    {
        if (plan == null || !plan.effectEvidenceFrozen)
        {
            throw new InvalidOperationException(
                "Meal outcome cannot commit without frozen effect evidence.");
        }
        return new CharacterConsumablesOutcomeEvidence(
            command.FacilityId,
            new ConsumableItemDefinitionId(plan.itemDefinitionId),
            plan.physicalCommitSourceStackIds?.ToArray()
                ?? Array.Empty<string>(),
            plan.physicalCommitId,
            plan.physicalCommitQuantity,
            plan.physicalCommitInputMassGrams,
            new CharacterConsumablesEffectEvidence(
                plan.hungerBefore,
                plan.hungerAfter,
                plan.healthBefore,
                plan.healthAfter,
                plan.moodBefore,
                plan.moodAfter),
            plan.committedPolicyViolation,
            plan.committedContaminated,
            plan.resolvedContaminationDamage && string.IsNullOrEmpty(plan.effectsSkippedReason),
            plan.effectsSkippedReason);
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
                && TimelineNow < existing.retryAfter)
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
                requestedAt = TimelineNow,
                retryAfter = TimelineNow + DeliveryRetrySeconds
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
            if (!policies.IsWithinMealQualityLimit(actor.Id, meal.QualityBand))
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

    private bool IsMealFollowupCooldownActive(
        CharacterConsumablesActorSnapshot actor) =>
        actor.Id.IsValid
        && actor.Hunger > EmergencyHungerThreshold
        && ReadState.MealFollowupCooldownUntil.TryGetValue(
            actor.Id,
            out float until)
        && TimelineNow < until;

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
            || TimelineNow >= snapshot.NextDeliveryPruneAt;
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

        state.NextDeliveryPruneAt = TimelineNow + 1f;
        foreach (CharacterMealDeliveryState delivery in state.PendingDeliveries
                     .Values.Where(value => value != null
                         && TimelineNow >= value.retryAfter).ToArray())
        {
            string destinationId = GetMealDestinationId(
                delivery.BuildingInstanceId,
                delivery.ItemDefinitionId);
            if (HasRoutedItem(destinationId, delivery.ItemDefinitionId))
            {
                delivery.retryAfter = TimelineNow + DeliveryRetrySeconds;
                continue;
            }
            state.PendingDeliveries.Remove(delivery.DeliveryId);
            state.DeliveryByRoute.Remove(CharacterConsumablesStateRules.Route(delivery));
        }
    }

    public bool ResumePendingPolicyFact(out string failure) =>
        policies.ResumePendingPolicyFact(out failure);

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

    private bool HasRoutedItem(string destinationId, ConsumableItemDefinitionId itemId) =>
        inventory.GetAllStacks().Any(stack => stack.Quantity > 0
            && stack.ItemId.Equals(itemId)
            && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));

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
        CharacterConsumablesSubstanceRuntime.GetRecreationalSubstanceDestinationId(facilityId, itemId);
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

}
