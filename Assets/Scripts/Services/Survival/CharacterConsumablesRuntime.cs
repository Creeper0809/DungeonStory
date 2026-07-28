using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterConsumablesRuntime :
    ICharacterConsumablesRuntime,
    ITickable
{
    private const float EmergencyHungerThreshold = 10f;
    private const float DeliveryRetrySeconds = 45f;
    private const float GameHourSeconds = 60f;
    private const float MedicalUseHealthRatio = 0.82f;

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWorldItemStackRuntime items;
    private readonly ISurvivalFoodRuntime survival;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IGridSystemProvider grids;
    private readonly IGameEventBus events;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly ICharacterCombatCommandRuntime combatCommands;
    private readonly IDefenseEngagementRuntime defenseEngagements;
    private readonly Dictionary<string, CharacterDietPolicyState> dietPolicies =
        new Dictionary<string, CharacterDietPolicyState>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterSubstancePolicyState> substancePolicies =
        new Dictionary<string, CharacterSubstancePolicyState>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterSubstanceState> substanceStates =
        new Dictionary<string, CharacterSubstanceState>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> pendingMealDeliveries =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly List<string> expiredMealDeliveryKeys = new List<string>();
    private readonly Dictionary<string, int> availableSubstanceItems =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private int availableSubstanceItemVersion = -1;
    private float nextDeliveryPruneAt;

    public CharacterConsumablesRuntime(
        IResourceEconomyContentCatalog catalog,
        IWorldItemStackRuntime items,
        ISurvivalFoodRuntime survival,
        ICharacterAiWorldRegistry world,
        IGridSystemProvider grids,
        IGameEventBus events,
        IGameClock clock,
        IRandomStreamProvider randomStreams,
        ICharacterCombatCommandRuntime combatCommands = null,
        IDefenseEngagementRuntime defenseEngagements = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.survival = survival ?? throw new ArgumentNullException(nameof(survival));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.grids = grids ?? throw new ArgumentNullException(nameof(grids));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.combatCommands = combatCommands;
        this.defenseEngagements = defenseEngagements;
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("character-consumables");
    }

    public CharacterDietPolicyKind GetPolicy(CharacterActor actor)
    {
        string characterId = GetCharacterId(actor);
        return !string.IsNullOrWhiteSpace(characterId)
            && dietPolicies.TryGetValue(characterId, out CharacterDietPolicyState state)
                ? state.policy
                : CharacterDietPolicyKind.Free;
    }

    public void SetPolicy(CharacterActor actor, CharacterDietPolicyKind policy)
    {
        string characterId = GetCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return;
        }

        dietPolicies[characterId] = new CharacterDietPolicyState
        {
            characterId = characterId,
            policy = policy
        };
    }

    public bool IsAllowed(CharacterActor actor, ResourceItemDefinitionSO meal)
    {
        return meal != null
            && meal.IsMeal
            && ResourceMealClassification.IsAllowed(
                GetPolicy(actor),
                meal.MealDietClass,
                (meal.IngredientTags & ResourceIngredientTag.Forbidden) != 0);
    }

    public bool HasMealAvailable(
        CharacterActor actor,
        BuildableObject facility,
        out string reason)
    {
        reason = string.Empty;
        if (!IsValidMealFacility(facility))
        {
            reason = "식사 시설이 아님";
            return false;
        }

        bool emergency = GetNeed(actor, CharacterCondition.HUNGER) <=
            EmergencyHungerThreshold;
        if (GetMealCandidates(actor, facility, bufferOnly: true, emergency).Count > 0)
        {
            return true;
        }

        if (GetMealCandidates(actor, facility, bufferOnly: false, emergency).Count > 0)
        {
            reason = "메뉴 운반 필요";
            return true;
        }

        reason = emergency
            ? "먹을 수 있는 음식이 없음"
            : "식단에 맞는 음식이 없음";
        return false;
    }

    public bool TryConsumeMeal(
        CharacterActor actor,
        BuildableObject facility,
        out MealConsumptionResult result)
    {
        if (actor == null || !IsValidMealFacility(facility))
        {
            result = MealConsumptionResult.Failed("유효한 식사 대상이 아님");
            return false;
        }

        bool emergency = GetNeed(actor, CharacterCondition.HUNGER) <=
            EmergencyHungerThreshold;
        List<MealCandidate> candidates = GetMealCandidates(
            actor,
            facility,
            bufferOnly: true,
            emergency);
        if (candidates.Count == 0)
        {
            if (TryRequestMealDelivery(actor, facility, emergency, out string requestReason))
            {
                result = MealConsumptionResult.Failed("메뉴 운반 대기");
                return false;
            }

            result = MealConsumptionResult.Failed(requestReason);
            return false;
        }

        MealCandidate selected = candidates[0];
        string destinationId = GetMealDestinationId(facility);
        Dictionary<string, int> cost = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [selected.Definition.ItemId] = 1
        };
        if (!items.TryConsumeFacilityItemBuffer(destinationId, cost, out string failureReason))
        {
            result = MealConsumptionResult.Failed(
                string.IsNullOrWhiteSpace(failureReason)
                    ? "메뉴 소비 실패"
                    : failureReason);
            return false;
        }

        pendingMealDeliveries.Remove(
            GetDeliveryKey(destinationId, selected.Definition.ItemId));
        bool policyViolation = !selected.PolicyAllowed;
        result = MealConsumptionResult.Consumed(
            selected.Definition,
            policyViolation,
            selected.Status.Contaminated);

        actor.ChangesStat(CharacterCondition.HUNGER, result.Nutrition);
        float mood = result.Mood;
        string moodLabel = $"{result.DisplayName}을 먹음";
        if (policyViolation)
        {
            mood -= 9f;
            moodLabel = $"살기 위해 식단을 어기고 {result.DisplayName}을 먹음";
            actor.Progression?.RecordNarrative(
                CharacterNarrativeDomain.Need,
                "diet:emergency-violation",
                result.ItemId,
                "survived",
                1f);
        }

        if (result.Contaminated)
        {
            mood -= 7f;
            actor.ApplyDamage(3f, "오염된 식사");
        }

        if (!Mathf.Approximately(mood, 0f))
        {
            actor.ApplyMoodFactor(
                $"meal:{result.ItemId}",
                moodLabel,
                mood,
                180f,
                1);
        }

        events.Publish(new PhysicalMealConsumedEvent(actor, facility, result));
        return true;
    }

    public CharacterSubstancePolicyState GetPolicy(
        CharacterActor actor,
        string substanceId)
    {
        string characterId = GetCharacterId(actor);
        string normalizedSubstanceId = substanceId?.Trim() ?? string.Empty;
        string key = GetSubstanceKey(characterId, normalizedSubstanceId);
        if (substancePolicies.TryGetValue(key, out CharacterSubstancePolicyState state))
        {
            return ClonePolicy(state);
        }

        SubstancePolicyMode mode = catalog.TryGetSubstance(
                normalizedSubstanceId,
                out SubstanceDefinitionSO definition)
            ? GetDefaultPolicy(definition.UseClass)
            : SubstancePolicyMode.Forbidden;
        return new CharacterSubstancePolicyState
        {
            characterId = characterId,
            substanceId = normalizedSubstanceId,
            mode = mode,
            moodThreshold = 30f,
            scheduledHour = 20
        };
    }

    public void SetPolicy(
        CharacterActor actor,
        string substanceId,
        SubstancePolicyMode mode,
        float moodThreshold = 30f,
        int scheduledHour = 20)
    {
        string characterId = GetCharacterId(actor);
        string normalizedSubstanceId = substanceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(characterId)
            || string.IsNullOrWhiteSpace(normalizedSubstanceId))
        {
            return;
        }

        CharacterSubstancePolicyState state = new CharacterSubstancePolicyState
        {
            characterId = characterId,
            substanceId = normalizedSubstanceId,
            mode = mode,
            moodThreshold = Mathf.Clamp(moodThreshold, 0f, 100f),
            scheduledHour = Mathf.Clamp(scheduledHour, 0, 23)
        };
        substancePolicies[GetSubstanceKey(characterId, normalizedSubstanceId)] =
            state;
    }

    public CharacterSubstanceState GetState(
        CharacterActor actor,
        string substanceId)
    {
        string characterId = GetCharacterId(actor);
        string normalizedSubstanceId = substanceId?.Trim() ?? string.Empty;
        string key = GetSubstanceKey(characterId, normalizedSubstanceId);
        return substanceStates.TryGetValue(key, out CharacterSubstanceState state)
            ? CloneState(state)
            : new CharacterSubstanceState
            {
                characterId = characterId,
                substanceId = normalizedSubstanceId
            };
    }

    public bool TryConsume(
        CharacterActor actor,
        string substanceId,
        bool medicalContext,
        bool combatContext,
        out SubstanceUseResult result)
    {
        result = new SubstanceUseResult(
            false,
            "약물 정보 없음",
            substanceId,
            string.Empty,
            0f,
            0f,
            false,
            false);
        if (actor == null
            || !catalog.TryGetSubstance(
                substanceId?.Trim() ?? string.Empty,
                out SubstanceDefinitionSO definition))
        {
            return false;
        }

        CharacterSubstancePolicyState policy = GetPolicy(actor, definition.SubstanceId);
        if (!PolicyAllows(actor, policy, medicalContext, combatContext))
        {
            result = new SubstanceUseResult(
                false,
                "복용 정책에서 금지됨",
                definition.SubstanceId,
                definition.DisplayName,
                0f,
                0f,
                false,
                false);
            return false;
        }

        if (!TryConsumePhysicalSubstance(actor, definition.ItemId))
        {
            result = new SubstanceUseResult(
                false,
                "소지한 약물이 없음",
                definition.SubstanceId,
                definition.DisplayName,
                0f,
                0f,
                false,
                false);
            return false;
        }

        string characterId = GetCharacterId(actor);
        string key = GetSubstanceKey(characterId, definition.SubstanceId);
        if (!substanceStates.TryGetValue(key, out CharacterSubstanceState state))
        {
            state = new CharacterSubstanceState
            {
                characterId = characterId,
                substanceId = definition.SubstanceId
            };
            substanceStates.Add(key, state);
        }

        float toleranceRatio = state.tolerance / 100f;
        bool wasAddicted = state.addicted;
        bool overdosed = random.Chance(
            Mathf.Clamp01(definition.OverdoseChance * (1f + toleranceRatio)));
        state.tolerance = Mathf.Clamp(
            state.tolerance + definition.ToleranceGain,
            0f,
            100f);
        state.addiction = Mathf.Clamp(
            state.addiction
                + (definition.AddictionChance * 100f * (0.65f + toleranceRatio)),
            0f,
            100f);
        state.addicted = state.addicted
            || state.addiction >= 60f
            || random.Chance(definition.AddictionChance * 0.2f);
        state.withdrawal = 0f;
        state.activeSeconds = definition.DurationSeconds;
        state.secondsSinceLastDose = 0f;
        state.overdosed = overdosed;
        if (policy.mode == SubstancePolicyMode.Scheduled)
        {
            state.scheduledCooldownSeconds = GameHourSeconds * 24f;
        }

        float effectiveMood = definition.MoodEffect * (1f - toleranceRatio * 0.55f);
        if (!Mathf.Approximately(effectiveMood, 0f))
        {
            actor.ApplyMoodFactor(
                $"substance:{definition.SubstanceId}",
                $"{definition.DisplayName} 효과",
                effectiveMood,
                definition.DurationSeconds,
                1);
        }

        if (overdosed)
        {
            actor.ApplyDamage(
                Mathf.Max(4f, actor.MaxHealth * 0.12f),
                $"{definition.DisplayName} 과다 복용");
            actor.ApplyMoodFactor(
                $"substance:overdose:{definition.SubstanceId}",
                $"{definition.DisplayName} 과다 복용",
                -12f,
                300f,
                1);
        }

        actor.Progression?.RecordNarrative(
            CharacterNarrativeDomain.Need,
            $"substance:{definition.SubstanceId}",
            definition.ItemId,
            overdosed ? "overdose" : "consumed",
            state.tolerance);
        result = new SubstanceUseResult(
            true,
            string.Empty,
            definition.SubstanceId,
            definition.DisplayName,
            state.tolerance,
            state.addiction,
            !wasAddicted && state.addicted,
            overdosed);
        return true;
    }

    public bool TryGetAutomaticUseRequest(
        CharacterActor actor,
        out CharacterSubstanceUseRequest request)
    {
        request = default;
        if (actor == null
            || actor.IsDead
            || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            return false;
        }

        bool medicalContext = actor.CurrentHealth
            < Mathf.Max(1f, actor.MaxHealth) * MedicalUseHealthRatio;
        bool combatContext = IsCombatContext(actor);
        float mood = actor.Mood.Value;
        int scheduleHour = GetCurrentScheduleHour();
        RefreshAvailableSubstanceItems();

        CharacterSubstanceUseRequest best = default;
        foreach (SubstanceDefinitionSO definition in catalog.Substances)
        {
            if (definition == null
                || string.IsNullOrWhiteSpace(definition.SubstanceId)
                || string.IsNullOrWhiteSpace(definition.ItemId)
                || !HasPhysicalSubstance(actor, definition.ItemId))
            {
                continue;
            }

            CharacterSubstancePolicyState policy =
                GetPolicy(actor, definition.SubstanceId);
            CharacterSubstanceState state =
                GetState(actor, definition.SubstanceId);
            if (state.activeSeconds > 0.01f)
            {
                continue;
            }

            float urgency = 0f;
            string reason = string.Empty;
            switch (policy.mode)
            {
                case SubstancePolicyMode.MedicalOnly:
                    if (medicalContext)
                    {
                        urgency = Mathf.Lerp(
                            0.62f,
                            1f,
                            1f - actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
                        reason = "부상 치료 보조";
                    }
                    break;
                case SubstancePolicyMode.CombatOnly:
                    if (combatContext)
                    {
                        urgency = 0.96f;
                        reason = "전투 대비";
                    }
                    break;
                case SubstancePolicyMode.MoodThreshold:
                    if (mood <= policy.moodThreshold)
                    {
                        urgency = Mathf.Lerp(
                            0.55f,
                            0.9f,
                            1f - mood / Mathf.Max(1f, policy.moodThreshold));
                        reason = $"기분 {mood:0}";
                    }
                    break;
                case SubstancePolicyMode.Scheduled:
                    if (scheduleHour >= policy.scheduledHour
                        && state.scheduledCooldownSeconds <= 0f)
                    {
                        urgency = 0.48f;
                        reason = $"{policy.scheduledHour:00}시 예약 복용";
                    }
                    break;
            }

            if (state.addicted && state.withdrawal >= 20f)
            {
                urgency = Mathf.Max(
                    urgency,
                    Mathf.Lerp(0.7f, 1f, state.withdrawal / 100f));
                reason = $"{definition.DisplayName} 금단";
            }

            if (urgency <= best.Urgency)
            {
                continue;
            }

            best = new CharacterSubstanceUseRequest(
                definition.SubstanceId,
                definition.ItemId,
                definition.DisplayName,
                urgency,
                medicalContext,
                combatContext,
                reason);
        }

        request = best;
        return request.IsValid;
    }

    public float GetWorkSpeedMultiplier(CharacterActor actor)
    {
        return GetEffectMultiplier(actor, workEffect: true);
    }

    public float GetCombatMultiplier(CharacterActor actor)
    {
        return GetEffectMultiplier(actor, workEffect: false);
    }

    public void Tick()
    {
        float deltaTime = Mathf.Max(0f, clock.DeltaTime);
        if (deltaTime <= 0f)
        {
            return;
        }

        foreach (CharacterSubstanceState state in substanceStates.Values)
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

            if (!state.addicted
                || state.secondsSinceLastDose < GameHourSeconds
                || !catalog.TryGetSubstance(state.substanceId, out SubstanceDefinitionSO definition))
            {
                continue;
            }

            state.withdrawal = Mathf.Clamp(
                state.withdrawal
                    + definition.WithdrawalPerHour * (deltaTime / GameHourSeconds),
                0f,
                100f);
            CharacterActor actor = FindActor(state.characterId);
            if (actor != null && state.withdrawal >= 20f)
            {
                actor.ApplyMoodFactor(
                    $"substance:withdrawal:{state.substanceId}",
                    $"{definition.DisplayName} 금단",
                    -Mathf.Lerp(3f, 14f, state.withdrawal / 100f),
                    2f,
                    1);
            }
        }

        PruneExpiredDeliveryRequests();
    }

    public DungeonCharacterConsumablesSaveData Capture()
    {
        return new DungeonCharacterConsumablesSaveData
        {
            version = DungeonCharacterConsumablesSaveData.CurrentVersion,
            dietPolicies = dietPolicies.Values
                .Select(CloneDietPolicy)
                .OrderBy(entry => entry.characterId, StringComparer.Ordinal)
                .ToList(),
            substancePolicies = substancePolicies.Values
                .Select(ClonePolicy)
                .OrderBy(entry => entry.characterId, StringComparer.Ordinal)
                .ThenBy(entry => entry.substanceId, StringComparer.Ordinal)
                .ToList(),
            substanceStates = substanceStates.Values
                .Select(CloneState)
                .OrderBy(entry => entry.characterId, StringComparer.Ordinal)
                .ThenBy(entry => entry.substanceId, StringComparer.Ordinal)
                .ToList()
        };
    }

    public void Restore(DungeonCharacterConsumablesSaveData saveData)
    {
        dietPolicies.Clear();
        substancePolicies.Clear();
        substanceStates.Clear();
        pendingMealDeliveries.Clear();
        availableSubstanceItems.Clear();
        availableSubstanceItemVersion = -1;
        if (saveData == null)
        {
            return;
        }

        foreach (CharacterDietPolicyState state in
                 saveData.dietPolicies ?? new List<CharacterDietPolicyState>())
        {
            if (state == null || string.IsNullOrWhiteSpace(state.characterId))
            {
                continue;
            }

            CharacterDietPolicyState clone = CloneDietPolicy(state);
            dietPolicies[clone.characterId] = clone;
        }

        foreach (CharacterSubstancePolicyState state in
                 saveData.substancePolicies ?? new List<CharacterSubstancePolicyState>())
        {
            if (state == null
                || string.IsNullOrWhiteSpace(state.characterId)
                || string.IsNullOrWhiteSpace(state.substanceId))
            {
                continue;
            }

            CharacterSubstancePolicyState clone = ClonePolicy(state);
            substancePolicies[
                GetSubstanceKey(clone.characterId, clone.substanceId)] = clone;
        }

        foreach (CharacterSubstanceState state in
                 saveData.substanceStates ?? new List<CharacterSubstanceState>())
        {
            if (state == null
                || string.IsNullOrWhiteSpace(state.characterId)
                || string.IsNullOrWhiteSpace(state.substanceId))
            {
                continue;
            }

            CharacterSubstanceState clone = CloneState(state);
            substanceStates[
                GetSubstanceKey(clone.characterId, clone.substanceId)] = clone;
        }
    }

    private bool TryRequestMealDelivery(
        CharacterActor actor,
        BuildableObject facility,
        bool emergency,
        out string reason)
    {
        reason = "먹을 수 있는 음식이 없음";
        List<MealCandidate> sourceCandidates = GetMealCandidates(
            actor,
            facility,
            bufferOnly: false,
            emergency);
        if (sourceCandidates.Count == 0)
        {
            return false;
        }

        string destinationId = GetMealDestinationId(facility);
        foreach (MealCandidate candidate in sourceCandidates)
        {
            string key = GetDeliveryKey(destinationId, candidate.Definition.ItemId);
            if (HasRoutedMeal(destinationId, candidate.Definition.ItemId)
                || (pendingMealDeliveries.TryGetValue(key, out float requestedAt)
                    && clock.Time - requestedAt < DeliveryRetrySeconds))
            {
                reason = "메뉴 운반 대기";
                return true;
            }

            if (items.TryRequestItemDelivery(
                    candidate.Definition.ItemId,
                    1,
                    facility.centerPos,
                    destinationId,
                    out int requested,
                    out _)
                && requested > 0)
            {
                pendingMealDeliveries[key] = clock.Time;
                reason = "메뉴 운반 대기";
                return true;
            }
        }

        return false;
    }

    private List<MealCandidate> GetMealCandidates(
        CharacterActor actor,
        BuildableObject facility,
        bool bufferOnly,
        bool emergency)
    {
        string destinationId = GetMealDestinationId(facility);
        List<MealCandidate> result = new List<MealCandidate>();
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack == null
                || stack.Quantity <= 0
                || stack.Forbidden
                || !catalog.TryGetItem(stack.ItemId, out ResourceItemDefinitionSO definition)
                || definition == null
                || !definition.IsMeal)
            {
                continue;
            }

            bool isBuffer = stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal);
            if (bufferOnly != isBuffer)
            {
                continue;
            }

            if (!bufferOnly
                && stack.State != WorldItemStackState.Stored
                && stack.State != WorldItemStackState.Loose)
            {
                continue;
            }

            bool allowed = IsAllowed(actor, definition);
            if (!allowed && !emergency)
            {
                continue;
            }

            survival.TryGetItemStatus(
                stack.StackId,
                stack.ItemId,
                out SurvivalItemStatus status);
            if (status.Contaminated && !emergency)
            {
                continue;
            }

            float score = (allowed ? 1000f : 0f)
                + (1f - status.Freshness01) * 120f
                + definition.Nutrition
                + definition.MealMood * 2f
                + (definition.MealQuality == MealQualityTier.Lavish ? 8f : 0f);
            result.Add(new MealCandidate(definition, stack, status, allowed, score));
        }

        result.Sort((left, right) =>
        {
            int score = right.Score.CompareTo(left.Score);
            return score != 0
                ? score
                : string.Compare(
                    left.Definition.ItemId,
                    right.Definition.ItemId,
                    StringComparison.Ordinal);
        });
        return result;
    }

    private bool TryConsumePhysicalSubstance(
        CharacterActor actor,
        string itemId)
    {
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        if (inventory != null && inventory.TryConsumeItem(itemId, 1))
        {
            return true;
        }

        if (!grids.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int position = grid.GetXY(actor.transform.position);
        WorldItemStackSnapshot stack = items
            .GetStacksAt(position, includeStored: true)
            .FirstOrDefault(candidate => candidate != null
                && candidate.Quantity > 0
                && !candidate.Forbidden
                && string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal));
        return stack != null
            && items.TryConsumeStackQuantity(stack.StackId, 1, out _);
    }

    private bool PolicyAllows(
        CharacterActor actor,
        CharacterSubstancePolicyState policy,
        bool medicalContext,
        bool combatContext)
    {
        return policy.mode switch
        {
            SubstancePolicyMode.MedicalOnly => medicalContext,
            SubstancePolicyMode.CombatOnly => combatContext,
            SubstancePolicyMode.MoodThreshold =>
                GetNeed(actor, CharacterCondition.MOOD) <= policy.moodThreshold,
            SubstancePolicyMode.Scheduled => true,
            _ => false
        };
    }

    private float GetEffectMultiplier(CharacterActor actor, bool workEffect)
    {
        string characterId = GetCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return 1f;
        }

        float additive = 0f;
        float withdrawalPenalty = 0f;
        foreach (CharacterSubstanceState state in substanceStates.Values)
        {
            if (!string.Equals(state.characterId, characterId, StringComparison.Ordinal)
                || !catalog.TryGetSubstance(state.substanceId, out SubstanceDefinitionSO definition))
            {
                continue;
            }

            if (state.activeSeconds > 0f)
            {
                additive += workEffect
                    ? definition.WorkSpeedEffect
                    : definition.CombatEffect;
            }

            withdrawalPenalty += state.withdrawal * 0.0025f;
        }

        return Mathf.Clamp(1f + additive - withdrawalPenalty, 0.45f, 1.75f);
    }

    private bool IsCombatContext(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (combatCommands?.IsInCombatStance(actor) == true)
        {
            return true;
        }

        return defenseEngagements != null
            && defenseEngagements.TryGetActorDefenseStatus(
                actor,
                out _,
                out _,
                out _);
    }

    private bool HasPhysicalSubstance(CharacterActor actor, string itemId)
    {
        CharacterCarryInventory inventory =
            actor != null ? CharacterCarryInventory.Ensure(actor) : null;
        if (inventory != null && inventory.CountItem(itemId) > 0)
        {
            return true;
        }

        return availableSubstanceItems.TryGetValue(itemId, out int available)
            && available > 0;
    }

    private void RefreshAvailableSubstanceItems()
    {
        if (availableSubstanceItemVersion == items.ItemStackVersion)
        {
            return;
        }

        availableSubstanceItems.Clear();
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack == null
                || stack.Quantity <= 0
                || stack.Forbidden
                || stack.State != WorldItemStackState.Stored
                || !string.IsNullOrWhiteSpace(stack.ReservedByPersistentId))
            {
                continue;
            }

            availableSubstanceItems.TryGetValue(stack.ItemId, out int current);
            availableSubstanceItems[stack.ItemId] = current + stack.Quantity;
        }

        availableSubstanceItemVersion = items.ItemStackVersion;
    }

    private int GetCurrentScheduleHour()
    {
        float elapsedGameHours = Mathf.Max(0f, clock.Time) / GameHourSeconds;
        return Mathf.FloorToInt(elapsedGameHours) % 24;
    }

    private CharacterActor FindActor(string characterId)
    {
        IReadOnlyList<CharacterActor> actors = world.AllCharacters;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor != null
                && string.Equals(
                    GetCharacterId(actor),
                    characterId,
                    StringComparison.Ordinal))
            {
                return actor;
            }
        }

        return null;
    }

    private bool HasRoutedMeal(string destinationId, string itemId)
    {
        IReadOnlyList<WorldItemStackSnapshot> stacks = items.GetAllStacks();
        for (int i = 0; i < stacks.Count; i++)
        {
            WorldItemStackSnapshot stack = stacks[i];
            if (stack != null
                && stack.Quantity > 0
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void PruneExpiredDeliveryRequests()
    {
        float now = clock.Time;
        if (pendingMealDeliveries.Count == 0 || now < nextDeliveryPruneAt)
        {
            return;
        }

        nextDeliveryPruneAt = now + 1f;
        expiredMealDeliveryKeys.Clear();
        foreach (KeyValuePair<string, float> pair in pendingMealDeliveries)
        {
            if (now - pair.Value >= DeliveryRetrySeconds)
            {
                expiredMealDeliveryKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < expiredMealDeliveryKeys.Count; i++)
        {
            pendingMealDeliveries.Remove(expiredMealDeliveryKeys[i]);
        }
    }

    private static bool IsValidMealFacility(BuildableObject facility)
    {
        return facility != null
            && !facility.isDestroy
            && facility.SupportsFacilityRole(FacilityRole.Meal);
    }

    private static string GetMealDestinationId(BuildableObject facility)
    {
        return WorldItemStackRuntime.FacilityInputDestinationPrefix
            + $"meal:{facility.BuildingData?.id ?? facility.id}:"
            + $"{facility.centerPos.x}:{facility.centerPos.y}";
    }

    private static string GetDeliveryKey(string destinationId, string itemId) =>
        $"{destinationId}|{itemId}";

    private static string GetCharacterId(CharacterActor actor)
    {
        if (actor == null)
        {
            return string.Empty;
        }

        string persistentId = actor.Identity?.PersistentId?.Trim();
        return !string.IsNullOrWhiteSpace(persistentId)
            ? persistentId
            : $"scene-character:{actor.GetInstanceID()}";
    }

    private static float GetNeed(
        CharacterActor actor,
        CharacterCondition condition)
    {
        return actor?.Stats?.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? value
                : 100f;
    }

    private static string GetSubstanceKey(
        string characterId,
        string substanceId) =>
        $"{characterId?.Trim()}|{substanceId?.Trim()}";

    private static SubstancePolicyMode GetDefaultPolicy(
        SubstanceUseClass useClass)
    {
        return useClass switch
        {
            SubstanceUseClass.Medicine => SubstancePolicyMode.MedicalOnly,
            SubstanceUseClass.NonAddictive => SubstancePolicyMode.MoodThreshold,
            SubstanceUseClass.Recreational => SubstancePolicyMode.MoodThreshold,
            _ => SubstancePolicyMode.Forbidden
        };
    }

    private static CharacterDietPolicyState CloneDietPolicy(
        CharacterDietPolicyState source) =>
        new CharacterDietPolicyState
        {
            characterId = source?.characterId?.Trim() ?? string.Empty,
            policy = source?.policy ?? CharacterDietPolicyKind.Free
        };

    private static CharacterSubstancePolicyState ClonePolicy(
        CharacterSubstancePolicyState source) =>
        new CharacterSubstancePolicyState
        {
            characterId = source?.characterId?.Trim() ?? string.Empty,
            substanceId = source?.substanceId?.Trim() ?? string.Empty,
            mode = source?.mode ?? SubstancePolicyMode.Forbidden,
            moodThreshold = Mathf.Clamp(source?.moodThreshold ?? 30f, 0f, 100f),
            scheduledHour = Mathf.Clamp(source?.scheduledHour ?? 20, 0, 23)
        };

    private static CharacterSubstanceState CloneState(
        CharacterSubstanceState source) =>
        new CharacterSubstanceState
        {
            characterId = source?.characterId?.Trim() ?? string.Empty,
            substanceId = source?.substanceId?.Trim() ?? string.Empty,
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

    private readonly struct MealCandidate
    {
        public MealCandidate(
            ResourceItemDefinitionSO definition,
            WorldItemStackSnapshot stack,
            SurvivalItemStatus status,
            bool policyAllowed,
            float score)
        {
            Definition = definition;
            Stack = stack;
            Status = status;
            PolicyAllowed = policyAllowed;
            Score = score;
        }

        public ResourceItemDefinitionSO Definition { get; }
        public WorldItemStackSnapshot Stack { get; }
        public SurvivalItemStatus Status { get; }
        public bool PolicyAllowed { get; }
        public float Score { get; }
    }
}
