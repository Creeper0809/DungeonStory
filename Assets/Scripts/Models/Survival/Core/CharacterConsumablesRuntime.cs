using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class CharacterConsumablesRuntime :
    ICharacterConsumablesApplication,
    ICharacterConsumablesPersistence
{
    private const float EmergencyHungerThreshold = 10f;
    private const float DeliveryRetrySeconds = 45f;
    private const float GameHourSeconds = 60f;
    private const float MedicalUseHealthRatio = 0.82f;
    private const string FacilityInputDestinationPrefix = "facility-input:";

    private readonly ICharacterConsumablesWorldPort world;
    private readonly ICharacterConsumablesInventoryPort inventory;
    private readonly ICharacterConsumablesEventPort events;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

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
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
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

    public bool IsMealAllowed(
        CharacterId characterId,
        CharacterConsumablesMealDefinitionSnapshot meal) =>
        meal.Id.IsValid && CharacterConsumablesPolicyRules.AllowsMeal(
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
        if (GetMealCandidates(actor, facilityId, true, emergency).Count > 0)
        {
            return true;
        }
        if (GetMealCandidates(actor, facilityId, false, emergency).Count > 0)
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
        bool emergency = actor.Hunger <= EmergencyHungerThreshold;
        List<MealCandidate> candidates = GetMealCandidates(
            actor,
            facilityId,
            true,
            emergency);
        if (candidates.Count == 0)
        {
            if (TryRequestMealDelivery(actor, facilityId, emergency))
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
            out result);
    }

    public bool TryConsumeMeal(
        ConsumeMealCommand command,
        out CharacterConsumablesMealResult result)
    {
        if (!command.IsValid)
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
        if (!inventory.TryConsume(stack.StackId, 1))
        {
            result = CharacterConsumablesMealResult.Failed(
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                command.ItemStackId.Value);
            return false;
        }

        result = CharacterConsumablesMealResult.Consumed(
            meal,
            command.ItemStackId,
            !policyAllowed,
            contaminated);
        RecordCompletedOperation(
            command.OperationId,
            command.CharacterId,
            meal.Id,
            command.ItemStackId,
            true);
        CompleteDelivery(command.CharacterId, command.FacilityId, meal.Id);
        ApplyMealEffects(command, result);
        return true;
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
        CharacterConsumablesStackSnapshot stack = FindAvailableSubstanceStack(substance.Id);
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
            out result);
    }

    public bool TryConsumeSubstance(
        ConsumeSubstanceByIdCommand command,
        out CharacterConsumablesSubstanceResult result)
    {
        if (!command.IsValid)
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
        if (!stack.StackId.IsValid || stack.Quantity <= 0 || stack.Forbidden
            || stack.Reserved || !stack.ItemId.Equals(substance.Id))
        {
            result = FailedSubstance(
                CharacterConsumablesFailureCode.ItemStackMissing,
                substance,
                command.ItemStackId,
                command.ItemStackId.Value);
            return false;
        }
        if (!inventory.TryConsume(stack.StackId, 1))
        {
            result = FailedSubstance(
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                substance,
                command.ItemStackId,
                command.ItemStackId.Value);
            return false;
        }

        CharacterSubstanceState state = GetWritableSubstanceState(
            command.CharacterId,
            substance.Id);
        SubstanceDefinitionView definition = substance.Definition;
        float toleranceRatio = state.tolerance / 100f;
        bool wasAddicted = state.addicted;
        bool overdosed = random.Chance(
            Mathf.Clamp01(definition.OverdoseChance * (1f + toleranceRatio)));
        state.tolerance = Mathf.Clamp(
            state.tolerance + definition.ToleranceGain,
            0f,
            100f);
        state.addiction = Mathf.Clamp(
            state.addiction + definition.AddictionChance * 100f * (0.65f + toleranceRatio),
            0f,
            100f);
        state.addicted = state.addicted || state.addiction >= 60f
            || random.Chance(definition.AddictionChance * 0.2f);
        state.withdrawal = 0f;
        state.activeSeconds = Mathf.Max(1f, definition.DurationSeconds);
        state.secondsSinceLastDose = 0f;
        state.overdosed = overdosed;
        if (policy.mode == SubstancePolicyMode.Scheduled)
        {
            state.scheduledCooldownSeconds = GameHourSeconds * 24f;
        }
        RecordCompletedOperation(
            command.OperationId,
            command.CharacterId,
            substance.Id,
            command.ItemStackId,
            false);
        ApplySubstanceEffects(command.CharacterId, substance, state, toleranceRatio, overdosed);
        result = new CharacterConsumablesSubstanceResult(
            true,
            CharacterConsumablesFailureCode.None,
            substance,
            command.ItemStackId,
            state.tolerance,
            state.addiction,
            !wasAddicted && state.addicted,
            overdosed);
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
            if (!FindAvailableSubstanceStack(substance.Id).StackId.IsValid)
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
        float deltaTime = Mathf.Max(0f, clock.DeltaTime);
        if (deltaTime <= 0f)
        {
            return;
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
        PruneExpiredDeliveries();
    }

    public DungeonCharacterConsumablesSaveData Capture() =>
        CharacterConsumablesStateRules.Capture(ReadState);

    public CharacterConsumablesRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterConsumablesSaveData saveData)
    {
        DungeonGameRestoreReport report = new();
        CharacterConsumablesStateRules.Validate(
            saveData,
            report,
            world,
            inventory);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character consumables restore candidate rejected: "
                + string.Join(" | ", report.Errors));
        }
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
        if (result.Contaminated)
        {
            mood -= 7f;
            world.ApplyDamage(command.CharacterId, 3f, "contaminated meal");
        }
        if (!Mathf.Approximately(mood, 0f))
        {
            world.ApplyMood(
                command.CharacterId,
                $"meal:{result.Meal.Id.Value}",
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
        bool emergency)
    {
        if (!TryGetMealFacility(facilityId, out CharacterConsumablesFacilitySnapshot facility))
        {
            return false;
        }
        foreach (MealCandidate candidate in GetMealCandidates(actor, facilityId, false, emergency))
        {
            MealDeliveryRoute route = new(actor.Id, facilityId, candidate.Definition.Id);
            if (ReadState.DeliveryByRoute.TryGetValue(route, out ConsumableDeliveryId existingId)
                && ReadState.PendingDeliveries.TryGetValue(existingId, out CharacterMealDeliveryState existing)
                && clock.Time < existing.retryAfter)
            {
                return true;
            }
            string destinationId = GetMealDestinationId(facilityId);
            if (HasRoutedItem(destinationId, candidate.Definition.Id))
            {
                return true;
            }
            if (!inventory.TryRequestDelivery(
                    candidate.Definition.Id,
                    1,
                    facility.Position,
                    destinationId,
                    out int requested)
                || requested <= 0)
            {
                continue;
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
            return true;
        }
        return false;
    }

    private List<MealCandidate> GetMealCandidates(
        CharacterConsumablesActorSnapshot actor,
        BuildingInstanceId facilityId,
        bool bufferOnly,
        bool emergency)
    {
        string destinationId = GetMealDestinationId(facilityId);
        List<MealCandidate> result = new();
        foreach (CharacterConsumablesStackSnapshot stack in inventory.GetAllStacks())
        {
            if (stack.Quantity <= 0 || stack.Forbidden
                || !inventory.TryGetMeal(stack.ItemId, out CharacterConsumablesMealDefinitionSnapshot meal))
            {
                continue;
            }
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
            float score = (allowed ? 1000f : 0f)
                + (1f - stack.Freshness01) * 120f
                + meal.Nutrition
                + meal.Mood * 2f;
            result.Add(new MealCandidate(meal, stack, score));
        }
        return result.OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Definition.Id.Value, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Stack.StackId.Value, StringComparer.Ordinal)
            .ToList();
    }

    private float GetEffectMultiplier(CharacterId characterId, bool workEffect)
    {
        if (!characterId.IsValid)
        {
            return 1f;
        }
        float additive = 0f;
        float withdrawalPenalty = 0f;
        foreach (KeyValuePair<CharacterSubstanceKey, CharacterSubstanceState> pair
                 in ReadState.SubstanceStates)
        {
            if (!pair.Key.CharacterId.Equals(characterId)
                || !inventory.TryResolveSubstance(pair.Key.ItemId, out CharacterConsumablesSubstanceDefinitionSnapshot substance))
            {
                continue;
            }
            if (pair.Value.activeSeconds > 0f)
            {
                additive += workEffect
                    ? substance.Definition.WorkSpeedEffect
                    : substance.Definition.CombatEffect;
            }
            withdrawalPenalty += pair.Value.withdrawal * 0.0025f;
        }
        return Mathf.Clamp(1f + additive - withdrawalPenalty, 0.45f, 1.75f);
    }

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
        bool meal)
    {
        WriteState.CompletedOperations.Add(operationId, new CharacterConsumableOperationState
        {
            operationId = operationId.Value,
            characterId = characterId.Value,
            itemDefinitionId = itemId.Value,
            itemStackId = stackId.Value,
            meal = meal,
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

    private void PruneExpiredDeliveries()
    {
        CharacterConsumablesAggregateState state = WriteState;
        if (state.PendingDeliveries.Count == 0 || clock.Time < state.NextDeliveryPruneAt)
        {
            return;
        }
        state.NextDeliveryPruneAt = clock.Time + 1f;
        foreach (CharacterMealDeliveryState delivery in state.PendingDeliveries.Values
                     .Where(value => clock.Time >= value.retryAfter).ToArray())
        {
            string destinationId = GetMealDestinationId(delivery.BuildingInstanceId);
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

    private ConsumableOperationId NewOperationId() =>
        new($"consumable-operation:{WriteState.NextOperationSequence++:D16}");
    private ConsumableDeliveryId NewDeliveryId() =>
        new($"consumable-delivery:{WriteState.NextDeliverySequence++:D16}");

    private CharacterConsumablesStackSnapshot FindStack(ItemStackId stackId) =>
        stackId.IsValid
            ? inventory.GetAllStacks().FirstOrDefault(stack => stack.StackId.Equals(stackId))
            : default;

    private CharacterConsumablesStackSnapshot FindAvailableSubstanceStack(
        ConsumableItemDefinitionId itemId) =>
        inventory.GetAllStacks()
            .Where(stack => stack.Quantity > 0 && !stack.Forbidden && !stack.Reserved
                && stack.ItemId.Equals(itemId))
            .OrderBy(stack => stack.State == CharacterConsumablesStackState.Carried ? 0 : 1)
            .ThenBy(stack => stack.State)
            .ThenBy(stack => stack.StackId.Value, StringComparer.Ordinal)
            .FirstOrDefault();

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
    private static string GetMealDestinationId(BuildingInstanceId facilityId) =>
        FacilityInputDestinationPrefix + $"meal:{facilityId.Value}";
    private static bool IsAvailableMealBufferStack(
        CharacterConsumablesStackSnapshot stack,
        BuildingInstanceId facilityId) =>
        stack.StackId.IsValid && stack.Quantity > 0 && !stack.Forbidden
        && stack.State == CharacterConsumablesStackState.FacilityBuffer
        && string.Equals(
            stack.DestinationId,
            GetMealDestinationId(facilityId),
            StringComparison.Ordinal);

    private readonly struct MealCandidate
    {
        internal MealCandidate(
            CharacterConsumablesMealDefinitionSnapshot definition,
            CharacterConsumablesStackSnapshot stack,
            float score)
        {
            Definition = definition;
            Stack = stack;
            Score = score;
        }

        internal CharacterConsumablesMealDefinitionSnapshot Definition { get; }
        internal CharacterConsumablesStackSnapshot Stack { get; }
        internal float Score { get; }
    }
}
