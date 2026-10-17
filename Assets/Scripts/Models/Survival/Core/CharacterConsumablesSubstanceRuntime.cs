using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>Owns substance and detox plans, toxicity, and time-dependent effects.</summary>
internal sealed class CharacterConsumablesSubstanceRuntime
{
    private const float GameHourSeconds = 60f;
    private const float MedicalUseHealthRatio = 0.82f;
    private readonly ICharacterConsumablesWorldPort world;
    private readonly ICharacterConsumablesInventoryPort inventory;
    private readonly ICharacterConsumablesEventPort events;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICharacterConsumablesWorkforcePort workforce;
    private readonly CharacterConsumablesPolicyRuntime policies;
    private readonly CharacterConsumablesOperationRuntime operations;

    internal CharacterConsumablesSubstanceRuntime(
        ICharacterConsumablesWorldPort world, ICharacterConsumablesInventoryPort inventory,
        ICharacterConsumablesEventPort events, IGameClock clock, IRandomStream random,
        DungeonRuntimeAggregateRootStore aggregateRootStore, ICharacterConsumablesWorkforcePort workforce,
        CharacterConsumablesPolicyRuntime policies, CharacterConsumablesOperationRuntime operations)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.random = random;
        this.aggregateRootStore = aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.workforce = workforce;
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    private CharacterConsumablesAggregateState ReadState =>
        aggregateRootStore.GetOrCreate(() => new CharacterConsumablesAggregateState());
    private CharacterConsumablesAggregateState WriteState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new CharacterConsumablesAggregateState(), state => state.Clone());
    private float TimelineNow => ReadState.GetTimelineTime(clock.Time);

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

    public CharacterToxicityStatus GetToxicityStatus(CharacterId characterId)
    {
        float toxicity = GetToxicity(characterId);
        bool pending = ReadState.ActiveDetoxTreatmentPlans.Values.Any(plan =>
            plan != null
            && string.Equals(
                plan.characterId,
                characterId.Value,
                StringComparison.Ordinal));
        bool available = toxicity > 0f
            && !pending
            && HasPotentialDetoxMedicineSupply();
        string reason = toxicity <= 0f
            ? "toxicity-zero"
            : pending
                ? "detox-treatment-pending"
            : available
                ? string.Empty
                : inventory.GetDetoxMedicines().Count == 0
                    ? "detox-medicine-undefined"
                    : "detox-medicine-unavailable";
        return new CharacterToxicityStatus(
            toxicity,
            available,
            pending,
            reason);
    }

    public bool TryApplyDetoxTreatment(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out CharacterDetoxTreatmentResult result)
    {
        result = default;
        if (ReadState.MealFailures.ContainsKey(operationId))
        {
            result = FailedDetox(CharacterConsumablesFailureCode.AlreadyProcessed,
                characterId, operationId.Value);
            return false;
        }
        if (!operationId.IsValid
            || !CharacterConsumablesOperationRuntime.IsAllowedOperationId(operationId, automaticOperation: false)
            || !characterId.IsValid
            || !facilityId.IsValid)
        {
            result = FailedDetox(
                CharacterConsumablesFailureCode.InvalidCommand,
                characterId,
                facilityId.Value);
            return false;
        }
        if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
            || !actor.Active)
        {
            result = FailedDetox(
                CharacterConsumablesFailureCode.CharacterMissing,
                characterId,
                characterId.Value);
            return false;
        }
        if (!world.FacilityIds.Contains(facilityId))
        {
            result = FailedDetox(
                CharacterConsumablesFailureCode.FacilityMissing,
                characterId,
                facilityId.Value);
            return false;
        }

        if (ReadState.CompletedOperations.TryGetValue(
                operationId,
                out CharacterConsumableOperationState completed)
            && !ReadState.ActiveDetoxTreatmentPlans.ContainsKey(operationId))
        {
            if (!completed.detox
                || !completed.CharacterId.Equals(characterId)
                || !completed.FacilityId.Equals(facilityId))
            {
                result = FailedDetox(
                    CharacterConsumablesFailureCode.AlreadyProcessed,
                    characterId,
                    operationId.Value);
                return false;
            }
            result = SuccessfulDetox(completed);
            return true;
        }

        KeyValuePair<ConsumableOperationId, CharacterDetoxTreatmentPlan> existing =
            ReadState.ActiveDetoxTreatmentPlans
                .Where(pair => pair.Value != null
                    && string.Equals(
                        pair.Value.characterId,
                        characterId.Value,
                        StringComparison.Ordinal))
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .FirstOrDefault();
        if (existing.Key.IsValid)
        {
            if (!existing.Key.Equals(operationId)
                || !string.Equals(
                    existing.Value.facilityInstanceId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            {
                result = FailedDetox(
                    CharacterConsumablesFailureCode.DeliveryPending,
                    characterId,
                    existing.Key.Value);
                return false;
            }
            TryFinalizeDetoxTreatmentPlan(existing.Key, existing.Value);
            if (ReadState.CompletedOperations.ContainsKey(existing.Key)
                && existing.Value.phase ==
                    CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged)
            {
                result = SuccessfulDetox(existing.Key, existing.Value);
                return true;
            }
            result = FailedDetox(
                CharacterConsumablesFailureCode.DeliveryPending,
                characterId,
                existing.Key.Value);
            return false;
        }

        if (GetToxicity(characterId) <= 0f)
        {
            result = FailedDetox(
                CharacterConsumablesFailureCode.PolicyForbidden,
                characterId,
                "toxicity-zero");
            return false;
        }

        DetoxMedicineCandidate candidate = FindAvailableDetoxMedicine(facilityId);
        if (!candidate.Stack.StackId.IsValid)
        {
            TryEnsureDetoxMedicineDelivery(
                characterId,
                facilityId,
                out string deliveryDetail);
            result = FailedDetox(
                inventory.GetDetoxMedicines().Count == 0
                    ? CharacterConsumablesFailureCode.ItemDefinitionMissing
                    : CharacterConsumablesFailureCode.DeliveryPending,
                characterId,
                deliveryDetail);
            return false;
        }

        if (!inventory.TryCommitDetoxConsumptionPending(
                operationId,
                candidate.Stack.StackId,
                out CharacterDetoxPhysicalCommitSnapshot physicalCommit,
                out string physicalFailure))
        {
            result = FailedDetox(
                CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
                characterId,
                physicalFailure);
            return false;
        }
        CharacterDetoxTreatmentPlan plan = new()
        {
            operationId = operationId.Value,
            characterId = characterId.Value,
            facilityInstanceId = facilityId.Value,
            itemDefinitionId = candidate.Medicine.Id.Value,
            sourceStackId = candidate.Stack.StackId.Value,
            phase = CharacterDetoxTreatmentPlanPhase.ItemCommitted,
            detoxReduction = candidate.Medicine.DetoxReduction,
            physicalCommitOperationId = physicalCommit.OperationId,
            physicalCommitReasonCode = physicalCommit.ReasonCode,
            physicalCommitId = physicalCommit.CommitId,
            physicalCommitSourceStackIds = physicalCommit.SourceStackIds.ToList(),
            physicalCommitQuantity = physicalCommit.Quantity,
            physicalCommitInputMassGrams = physicalCommit.InputMassGrams
        };
        WriteState.ActiveDetoxTreatmentPlans.Add(operationId, plan);
        TryFinalizeDetoxTreatmentPlan(operationId, plan);
        if (!ReadState.CompletedOperations.ContainsKey(operationId)
            || plan.phase !=
                CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged)
        {
            result = FailedDetox(
                CharacterConsumablesFailureCode.DeliveryPending,
                characterId,
                operationId.Value);
            return false;
        }
        result = SuccessfulDetox(operationId, plan);
        return true;
    }

    public bool TryGetPendingDetoxTreatment(
        CharacterId characterId,
        out BuildingInstanceId facilityId,
        out ConsumableOperationId operationId)
    {
        return TryGetPendingDetoxTreatment(
            characterId,
            out facilityId,
            out operationId,
            out _);
    }

    public bool TryGetPendingDetoxTreatment(
        CharacterId characterId,
        out BuildingInstanceId facilityId,
        out ConsumableOperationId operationId,
        out ConsumableItemDefinitionId itemId)
    {
        KeyValuePair<ConsumableOperationId, CharacterDetoxTreatmentPlan> pending =
            ReadState.ActiveDetoxTreatmentPlans
                .Where(pair => pair.Value != null
                    && string.Equals(
                        pair.Value.characterId,
                        characterId.Value,
                        StringComparison.Ordinal))
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .FirstOrDefault();
        facilityId = pending.Value == null
            ? default
            : new BuildingInstanceId(pending.Value.facilityInstanceId);
        itemId = pending.Value == null
            ? default
            : new ConsumableItemDefinitionId(pending.Value.itemDefinitionId);
        operationId = pending.Key;
        return operationId.IsValid && facilityId.IsValid && itemId.IsValid;
    }

    public bool TryGetDetoxTreatmentOwner(
        ConsumableOperationId operationId,
        out CharacterId characterId,
        out BuildingInstanceId facilityId,
        out bool completed)
    {
        characterId = default;
        facilityId = default;
        completed = false;
        if (!operationId.IsValid)
            return false;
        if (ReadState.ActiveDetoxTreatmentPlans.TryGetValue(
                operationId,
                out CharacterDetoxTreatmentPlan plan)
            && plan != null)
        {
            characterId = new CharacterId(plan.characterId);
            facilityId = new BuildingInstanceId(plan.facilityInstanceId);
            return characterId.IsValid && facilityId.IsValid;
        }
        if (!ReadState.CompletedOperations.TryGetValue(
                operationId,
                out CharacterConsumableOperationState operation)
            || operation == null
            || !operation.detox)
        {
            return false;
        }
        characterId = operation.CharacterId;
        facilityId = operation.FacilityId;
        completed = true;
        return characterId.IsValid && facilityId.IsValid;
    }

    public bool TryAcknowledgeDetoxTreatment(
        ConsumableOperationId operationId)
    {
        if (!operationId.IsValid
            || !WriteState.ActiveDetoxTreatmentPlans.TryGetValue(
                operationId,
                out CharacterDetoxTreatmentPlan plan)
            || plan == null
            || plan.phase !=
                CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged
            || !WriteState.CompletedOperations.ContainsKey(operationId))
        {
            return false;
        }
        return WriteState.ActiveDetoxTreatmentPlans.Remove(operationId);
    }

    public void ProcessOperatingDay(int day)
    {
        if (day <= 0)
            throw new ArgumentOutOfRangeException(nameof(day));
        foreach (KeyValuePair<CharacterId, CharacterToxicityState> pair in
                 WriteState.ToxicityStates
                     .OrderBy(value => value.Key.Value, StringComparer.Ordinal)
                     .ToArray())
        {
            CharacterToxicityState toxicity = pair.Value;
            if (toxicity == null || day <= toxicity.lastNaturalRecoveryDay)
                continue;
            int elapsedDays = toxicity.lastNaturalRecoveryDay <= 0
                ? 1
                : day - toxicity.lastNaturalRecoveryDay;
            toxicity.toxicity = CharacterToxicityPolicy.Clamp(
                toxicity.toxicity
                - CharacterToxicityPolicy.NaturalRecoveryPerDay * elapsedDays);
            toxicity.lastNaturalRecoveryDay = day;
        }
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
                operations.NewOperationId(),
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
                    operations.NewOperationId(),
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
            CharacterSubstancePolicyState policy = policies.GetSubstancePolicy(
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
            || !CharacterConsumablesOperationRuntime.IsAllowedOperationId(command.OperationId, automaticOperation))
        {
            result = CharacterConsumablesSubstanceResult.Failed(
                CharacterConsumablesFailureCode.InvalidCommand,
                command.OperationId.Value);
            return false;
        }
        if (ReadState.CompletedOperations.ContainsKey(command.OperationId)
            || ReadState.MealFailures.ContainsKey(command.OperationId))
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

        CharacterSubstancePolicyState policy = policies.GetSubstancePolicy(
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
        CharacterSubstanceState currentState = GetSubstanceState(
            command.CharacterId,
            substance.Id.Value);
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
            becameAddicted = !wasAddicted && resolvedAddicted,
            resolvedEffectMood = definition.MoodEffect
                * (1f - toleranceRatio * 0.55f),
            resolvedOverdoseDamage = overdosed
                ? Mathf.Max(4f, actor.MaxHealth * 0.12f)
                : 0f
        };
        WriteState.ActiveSubstanceUsePlans.Add(command.OperationId, plan);
        TryFinalizeSubstanceUsePlan(command.OperationId, plan);
        if (!WriteState.CompletedOperations.TryGetValue(command.OperationId, out var completedSubstance)
            || string.IsNullOrWhiteSpace(completedSubstance.outcomeResultKey))
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
            overdosed,
            WriteState.CompletedOperations[command.OperationId]
                .outcomeResultKey);
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

            CharacterConsumablesAggregateState readState = ReadState;
            if (readState.CompletedOperations.ContainsKey(operationId))
            {
                WriteState.ActiveSubstanceUsePlans[operationId].phase =
                    CharacterSubstanceUsePlanPhase.EffectsPublished;
            }
            else
            {
                CharacterConsumablesAggregateState rollback = readState.Clone();
                CharacterConsumablesAggregateState writableState = WriteState;
                if (!writableState.ActiveSubstanceUsePlans.TryGetValue(
                        operationId,
                        out CharacterSubstanceUsePlan writablePlan)
                    || writablePlan == null)
                {
                    return false;
                }
                CharacterSubstanceState stateForItem = GetWritableSubstanceState(
                    characterId,
                    itemId);
                stateForItem.tolerance = writablePlan.resolvedTolerance;
                stateForItem.addiction = writablePlan.resolvedAddiction;
                stateForItem.withdrawal = writablePlan.resolvedWithdrawal;
                stateForItem.activeSeconds = writablePlan.resolvedActiveSeconds;
                stateForItem.secondsSinceLastDose =
                    writablePlan.resolvedSecondsSinceLastDose;
                stateForItem.scheduledCooldownSeconds =
                    writablePlan.resolvedScheduledCooldownSeconds;
                stateForItem.addicted = writablePlan.resolvedAddicted;
                stateForItem.overdosed = writablePlan.resolvedOverdosed;
                if (writablePlan.resolvedOverdosed)
                {
                    AddToxicity(
                        characterId,
                        CharacterToxicityPolicy.OverdoseGain);
                }
                if (!TryApplySubstanceEffects(
                        operationId,
                        characterId,
                        substance,
                        writablePlan,
                        out string effectFailure))
                {
                    aggregateRootStore.Replace(rollback);
                    Debug.LogError(
                        "character-consumables-substance-effects-failed:"
                        + effectFailure);
                    return false;
                }
                writablePlan.phase =
                    CharacterSubstanceUsePlanPhase.EffectsApplied;
            }
        }

        if (plan.phase == CharacterSubstanceUsePlanPhase.EffectsApplied)
        {
            if (!operations.TryGetConsumptionFactDay(out int day)) return false;
            var fact = new CharacterSubstanceConsumedFact(operationId, characterId, day, itemId,
                plan.resolvedTolerance, plan.resolvedAddiction, plan.resolvedOverdosed);
            if (!operations.TryReserveConsumptionFact(fact, out var lease)) return false;
            CharacterConsumablesAggregateState rollback = ReadState.Clone();
            bool publicationStarted = false;
            try
            {
                var writable = WriteState.ActiveSubstanceUsePlans[operationId];
                writable.factAbsoluteDay = day;
                writable.publicationKey = lease.PublicationKey;
                writable.publicationPending = true;
                operations.RecordCompletedOperation(operationId, characterId, itemId,
                    new ItemStackId(plan.sourceStackId), false);
                writable.phase = CharacterSubstanceUsePlanPhase.EffectsPublished;
                publicationStarted = true;
                if (!operations.PublishConsumptionFacts(new[] { DomainFactEmission.Create(lease, fact) }, rollback))
                    return false;
                operations.SetOutcomeResultKey(operationId, writable.publicationKey);
                writable.publicationPending = false;
            }
            catch
            {
                if (!publicationStarted)
                { operations.CancelFact(lease); aggregateRootStore.Replace(rollback); }
                throw;
            }
        }

        CharacterConsumablesAggregateState state = WriteState;
        if (!state.ActiveSubstanceUsePlans.TryGetValue(
                operationId,
                out CharacterSubstanceUsePlan activePlan)
            || activePlan == null)
        {
            return false;
        }
        if (activePlan.phase != CharacterSubstanceUsePlanPhase.EffectsPublished)
        {
            return false;
        }
        if (activePlan.publicationPending)
        {
            var fact = new CharacterSubstanceConsumedFact(operationId, characterId,
                activePlan.factAbsoluteDay, itemId, activePlan.resolvedTolerance,
                activePlan.resolvedAddiction, activePlan.resolvedOverdosed);
            if (!operations.ReplayConsumptionFacts(new[] { DomainFactReplay.Create(activePlan.publicationKey, fact) }))
                return false;
            operations.SetOutcomeResultKey(operationId, activePlan.publicationKey);
            activePlan.publicationPending = false;
        }
        if (!inventory.TryAcknowledgeSubstanceConsumption(
                characterId,
                itemId,
                activePlan.physicalCommitQuantity,
                activePlan.physicalCommitId,
                out _))
        {
            return false;
        }
        state.ActiveSubstanceUsePlans.Remove(operationId);
        return true;
    }

    private void AdvanceDetoxTreatmentPlans()
    {
        KeyValuePair<ConsumableOperationId, CharacterDetoxTreatmentPlan>[] active =
            WriteState.ActiveDetoxTreatmentPlans
                .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .ToArray();
        foreach (KeyValuePair<ConsumableOperationId, CharacterDetoxTreatmentPlan> pair
                 in active)
        {
            TryFinalizeDetoxTreatmentPlan(pair.Key, pair.Value);
        }
    }

    private bool TryFinalizeDetoxTreatmentPlan(
        ConsumableOperationId operationId,
        CharacterDetoxTreatmentPlan plan)
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
        ConsumableItemDefinitionId medicineId = new(plan.itemDefinitionId);
        if (plan.phase == CharacterDetoxTreatmentPlanPhase.ItemCommitted)
        {
            if (!TryGetActor(characterId, out CharacterConsumablesActorSnapshot actor)
                || !actor.Active
                || !inventory.TryResolveDetoxMedicine(
                    medicineId,
                    out CharacterDetoxMedicineDefinitionSnapshot medicine)
                || !medicine.DetoxReduction.Equals(plan.detoxReduction))
            {
                return false;
            }
            CharacterConsumablesAggregateState readState = ReadState;
            if (!readState.CompletedOperations.ContainsKey(operationId))
            {
                if (!operations.TryGetConsumptionFactDay(out int day)) return false;
                float reduction = Mathf.Min(GetToxicity(characterId), plan.detoxReduction);
                var fact = new CharacterDetoxAppliedFact(operationId, characterId, day, medicineId, reduction);
                if (!operations.TryReserveConsumptionFact(fact, out var lease)) return false;
                CharacterConsumablesAggregateState rollback = readState.Clone();
                bool publicationStarted = false;
                try
                {
                    var writable = WriteState.ActiveDetoxTreatmentPlans[operationId];
                    writable.factAbsoluteDay = day;
                    writable.publicationKey = lease.PublicationKey;
                    writable.publicationPending = true;
                    float before = GetToxicity(characterId);
                    AddToxicity(characterId, -writable.detoxReduction);
                    writable.appliedReduction = before - GetToxicity(characterId);
                    if (!writable.appliedReduction.Equals(reduction))
                        throw new InvalidOperationException("Detox effect changed after reservation.");
                    operations.RecordCompletedOperation(operationId, characterId, medicineId,
                        new ItemStackId(writable.sourceStackId), meal: false, detox: true,
                        facilityId: new BuildingInstanceId(writable.facilityInstanceId),
                        appliedEffect: writable.appliedReduction);
                    writable.phase = CharacterDetoxTreatmentPlanPhase.EffectsPublished;
                    publicationStarted = true;
                    if (!operations.PublishConsumptionFacts(new[] { DomainFactEmission.Create(lease, fact) }, rollback))
                        return false;
                    operations.SetOutcomeResultKey(operationId, writable.publicationKey);
                    writable.publicationPending = false;
                }
                catch
                {
                    if (!publicationStarted)
                    { operations.CancelFact(lease); aggregateRootStore.Replace(rollback); }
                    throw;
                }
            }
        }
        CharacterConsumablesAggregateState activeState = WriteState;
        if (!activeState.ActiveDetoxTreatmentPlans.TryGetValue(
                operationId,
                out CharacterDetoxTreatmentPlan activePlan)
            || activePlan == null)
        {
            return false;
        }
        if (activePlan.phase == CharacterDetoxTreatmentPlanPhase.EffectsPublished)
        {
            if (activePlan.publicationPending)
            {
                var fact = new CharacterDetoxAppliedFact(operationId, characterId,
                    activePlan.factAbsoluteDay, medicineId, activePlan.appliedReduction);
                if (!operations.ReplayConsumptionFacts(new[] { DomainFactReplay.Create(activePlan.publicationKey, fact) }))
                    return false;
                operations.SetOutcomeResultKey(operationId, activePlan.publicationKey);
                activePlan.publicationPending = false;
            }
            if (!inventory.TryAcknowledgeDetoxConsumption(
                    characterId,
                    medicineId,
                    activePlan.physicalCommitQuantity,
                    activePlan.physicalCommitId,
                    out _))
            {
                return false;
            }
            activePlan.phase =
                CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged;
        }
        return activePlan.phase
            == CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged;
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
            CharacterSubstancePolicyState policy = policies.GetSubstancePolicy(
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

    public float GetFatigueAccumulationMultiplier(CharacterId characterId) =>
        (float)Math.Clamp(
            1d - GetActiveSpecificEffectTotal(
                characterId,
                definition => definition.FatigueAccumulationReduction),
            0d,
            1d);

    public float GetResearchSpeedMultiplier(CharacterId characterId) =>
        CharacterSubstanceEffectMultiplierAuthority.Resolve(
            GetActiveSpecificEffectTotal(
                characterId,
                definition => definition.ResearchSpeedEffect),
            0d);

    public bool SuppressesPerceivedPain(CharacterId characterId) =>
        HasActiveSpecificEffect(
            characterId,
            definition => definition.SuppressesPerceivedPain);

    private bool TryApplySubstanceEffects(
        ConsumableOperationId operationId,
        CharacterId characterId,
        CharacterConsumablesSubstanceDefinitionSnapshot substance,
        CharacterSubstanceUsePlan plan,
        out string failureReason)
    {
        SubstanceDefinitionView definition = substance.Definition;
        CharacterSubstanceEffectCommand effect = new(
            operationId,
            characterId,
            substance.Id.Value,
            definition.DisplayName,
            plan.resolvedEffectMood,
            definition.DurationSeconds,
            plan.resolvedTolerance,
            plan.resolvedOverdosed,
            plan.resolvedOverdoseDamage);
        if (world is ICharacterConsumablesEffectTransactionPort transaction)
        {
            return transaction.TryApplySubstanceEffects(
                effect,
                out failureReason);
        }
        try
        {
            if (!Mathf.Approximately(plan.resolvedEffectMood, 0f))
            {
                world.ApplyMood(
                    characterId,
                    $"substance:{substance.Id.Value}",
                    definition.DisplayName,
                    plan.resolvedEffectMood,
                    definition.DurationSeconds);
            }
            world.RecordNeedNarrative(
                characterId,
                $"substance:{substance.Id.Value}",
                substance.Id.Value,
                plan.resolvedOverdosed ? "overdose" : "consumed",
                plan.resolvedTolerance);
            if (plan.resolvedOverdosed)
            {
                world.ApplyMood(
                    characterId,
                    $"substance:overdose:{substance.Id.Value}",
                    $"{definition.DisplayName} overdose",
                    -12f,
                    300f);
                world.ApplyDamage(
                    characterId,
                    plan.resolvedOverdoseDamage,
                    $"{definition.DisplayName} overdose");
            }
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            failureReason = "substance-effect-transaction-unavailable:"
                + exception.GetType().Name;
            return false;
        }
    }

    private readonly struct DetoxMedicineCandidate
    {
        internal DetoxMedicineCandidate(
            CharacterDetoxMedicineDefinitionSnapshot medicine,
            CharacterConsumablesStackSnapshot stack)
        {
            Medicine = medicine;
            Stack = stack;
        }

        internal CharacterDetoxMedicineDefinitionSnapshot Medicine { get; }
        internal CharacterConsumablesStackSnapshot Stack { get; }
    }

    private DetoxMedicineCandidate FindAvailableDetoxMedicine(
        BuildingInstanceId facilityId)
    {
        IReadOnlyDictionary<ConsumableItemDefinitionId,
            CharacterDetoxMedicineDefinitionSnapshot> medicines =
            inventory.GetDetoxMedicines()
                .ToDictionary(value => value.Id);
        foreach (CharacterConsumablesStackSnapshot stack in inventory.GetAllStacks()
                     .Where(value => value.StackId.IsValid
                         && value.AvailableQuantity > 0
                         && !value.Forbidden
                         && value.State ==
                            CharacterConsumablesStackState.FacilityBuffer)
                     .OrderBy(value => value.ItemId.Value, StringComparer.Ordinal)
                     .ThenBy(value => value.StackId.Value, StringComparer.Ordinal))
        {
            if (medicines.TryGetValue(
                    stack.ItemId,
                    out CharacterDetoxMedicineDefinitionSnapshot medicine)
                && string.Equals(
                    stack.DestinationId,
                    CharacterConsumablesInputDestinationIdentity.Build(
                        CharacterConsumablesInputKind.MedicalTreatment,
                        facilityId,
                        medicine.Id),
                    StringComparison.Ordinal))
            {
                return new DetoxMedicineCandidate(medicine, stack);
            }
        }
        return default;
    }

    private bool HasPotentialDetoxMedicineSupply()
    {
        HashSet<ConsumableItemDefinitionId> medicines = inventory
            .GetDetoxMedicines()
            .Select(value => value.Id)
            .ToHashSet();
        return medicines.Count > 0
            && inventory.GetAllStacks().Any(stack =>
                stack.StackId.IsValid
                && stack.Quantity > 0
                && !stack.Forbidden
                && medicines.Contains(stack.ItemId)
                && (((stack.State == CharacterConsumablesStackState.Stored
                            || stack.State ==
                                CharacterConsumablesStackState.Loose)
                        && stack.AvailableQuantity > 0)
                    || CharacterConsumablesInputDestinationIdentity
                        .IsDestinationForKind(
                            stack.DestinationId,
                            CharacterConsumablesInputKind.MedicalTreatment)));
    }

    private bool TryEnsureDetoxMedicineDelivery(
        CharacterId characterId,
        BuildingInstanceId facilityId,
        out string detail)
    {
        detail = string.Empty;
        if (!world.TryGetFacility(
                facilityId,
                out CharacterConsumablesFacilitySnapshot facility))
        {
            detail = "detox-treatment-facility-missing:" + facilityId.Value;
            return false;
        }
        CharacterDetoxMedicineDefinitionSnapshot[] medicines = inventory
            .GetDetoxMedicines()
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();
        if (medicines.Length == 0)
        {
            detail = "detox-medicine-undefined";
            return false;
        }
        IReadOnlyList<CharacterConsumablesStackSnapshot> stacks =
            inventory.GetAllStacks();
        foreach (CharacterDetoxMedicineDefinitionSnapshot medicine in medicines)
        {
            string destinationId =
                CharacterConsumablesInputDestinationIdentity.Build(
                    CharacterConsumablesInputKind.MedicalTreatment,
                    facilityId,
                    medicine.Id);
            if (stacks.Any(stack => stack.StackId.IsValid
                && stack.Quantity > 0
                && !stack.Forbidden
                && stack.ItemId.Equals(medicine.Id)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)))
            {
                detail = "detox-medicine-delivery-in-transit:"
                    + destinationId;
                return false;
            }
        }
        string lastFailure = string.Empty;
        foreach (CharacterDetoxMedicineDefinitionSnapshot medicine in medicines)
        {
            string destinationId =
                CharacterConsumablesInputDestinationIdentity.Build(
                    CharacterConsumablesInputKind.MedicalTreatment,
                    facilityId,
                    medicine.Id);
            if (!inventory.TryRequestDelivery(
                    medicine.Id,
                    1,
                    facility.Position,
                    destinationId,
                    out int requested,
                    out string failureReason)
                || requested != 1)
            {
                if (!string.IsNullOrWhiteSpace(failureReason))
                {
                    lastFailure = failureReason.Trim();
                }
                continue;
            }
            workforce?.RequestOneHaulerToReplan(characterId);
            detail = "detox-medicine-delivery-requested:" + destinationId;
            return false;
        }
        detail = "detox-medicine-stock-missing"
            + (lastFailure.Length > 0 ? ":" + lastFailure : string.Empty);
        return false;
    }

    private float GetToxicity(CharacterId characterId) =>
        characterId.IsValid
        && ReadState.ToxicityStates.TryGetValue(
            characterId,
            out CharacterToxicityState state)
            ? CharacterToxicityPolicy.Clamp(state.toxicity)
            : 0f;

    private void AddToxicity(CharacterId characterId, float delta)
    {
        if (!characterId.IsValid
            || float.IsNaN(delta)
            || float.IsInfinity(delta))
        {
            throw new ArgumentOutOfRangeException(nameof(delta));
        }
        if (!WriteState.ToxicityStates.TryGetValue(
                characterId,
                out CharacterToxicityState state))
        {
            state = new CharacterToxicityState
            {
                characterId = characterId.Value
            };
            WriteState.ToxicityStates.Add(characterId, state);
        }
        state.toxicity = CharacterToxicityPolicy.Clamp(state.toxicity + delta);
    }

    private CharacterDetoxTreatmentResult SuccessfulDetox(
        ConsumableOperationId operationId,
        CharacterDetoxTreatmentPlan plan) => new(
        true,
        CharacterConsumablesFailureCode.None,
        operationId,
        new ConsumableItemDefinitionId(plan.itemDefinitionId),
        new ItemStackId(plan.sourceStackId),
        plan.appliedReduction,
        GetToxicity(new CharacterId(plan.characterId)),
        string.Empty);

    private CharacterDetoxTreatmentResult SuccessfulDetox(
        CharacterConsumableOperationState completed) => new(
        true,
        CharacterConsumablesFailureCode.None,
        completed.OperationId,
        completed.ItemDefinitionId,
        completed.ItemStackId,
        completed.appliedEffect,
        GetToxicity(completed.CharacterId),
        string.Empty);

    private CharacterDetoxTreatmentResult FailedDetox(
        CharacterConsumablesFailureCode code,
        CharacterId characterId,
        string detail) => new(
        false,
        code,
        default,
        default,
        default,
        0f,
        GetToxicity(characterId),
        detail);

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
        withdrawalPenalty += CharacterToxicityPolicy.GetPerformancePenalty(
            GetToxicity(characterId));
        return CharacterSubstanceEffectMultiplierAuthority.Resolve(
            additive,
            withdrawalPenalty);
    }

    private double GetActiveSpecificEffectTotal(
        CharacterId characterId,
        Func<SubstanceDefinitionView, float> selector)
    {
        if (!characterId.IsValid)
            return 0d;
        if (selector == null)
            throw new ArgumentNullException(nameof(selector));

        double total = 0d;
        foreach (KeyValuePair<CharacterSubstanceKey, CharacterSubstanceState> pair
                 in ReadState.SubstanceStates
                     .Where(value => value.Key.CharacterId.Equals(characterId))
                     .OrderBy(
                         value => value.Key.ItemId.Value,
                         StringComparer.Ordinal))
        {
            if (pair.Value.activeSeconds <= 0f
                || !inventory.TryResolveSubstance(
                    pair.Key.ItemId,
                    out CharacterConsumablesSubstanceDefinitionSnapshot substance))
            {
                continue;
            }
            float effect = selector(substance.Definition);
            if (float.IsNaN(effect) || float.IsInfinity(effect) || effect < 0f)
            {
                throw new InvalidOperationException(
                    "Character substance-specific effect must be finite and "
                    + "nonnegative: " + pair.Key.ItemId.Value);
            }
            total += effect;
        }
        return total;
    }

    private bool HasActiveSpecificEffect(
        CharacterId characterId,
        Func<SubstanceDefinitionView, bool> selector)
    {
        if (!characterId.IsValid)
            return false;
        if (selector == null)
            throw new ArgumentNullException(nameof(selector));

        foreach (KeyValuePair<CharacterSubstanceKey, CharacterSubstanceState> pair
                 in ReadState.SubstanceStates
                     .Where(value => value.Key.CharacterId.Equals(characterId))
                     .OrderBy(
                         value => value.Key.ItemId.Value,
                         StringComparer.Ordinal))
        {
            if (pair.Value.activeSeconds > 0f
                && inventory.TryResolveSubstance(
                    pair.Key.ItemId,
                    out CharacterConsumablesSubstanceDefinitionSnapshot substance)
                && selector(substance.Definition))
            {
                return true;
            }
        }
        return false;
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

    private CharacterConsumablesSubstanceResult FailedSubstance(
        CharacterConsumablesFailureCode code,
        CharacterConsumablesSubstanceDefinitionSnapshot substance,
        ItemStackId stackId,
        params string[] parameters) =>
        new(false, code, substance, stackId, 0f, 0f, false, false,
            string.Empty, parameters);

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

            CharacterSubstancePolicyState policy = policies.GetSubstancePolicy(
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
        Mathf.FloorToInt(TimelineNow / GameHourSeconds) % 24;

    public static string GetRecreationalSubstanceDestinationId(
        BuildingInstanceId facilityId,
        ConsumableItemDefinitionId itemId) =>
        CharacterConsumablesInputDestinationIdentity.Build(
            CharacterConsumablesInputKind.RecreationalSubstance,
            facilityId,
            itemId);

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

    internal void AdvancePendingPlans()
    {
        AdvanceSubstanceUsePlans();
        AdvanceDetoxTreatmentPlans();
    }

    internal void AdvanceTimedEffects(float deltaTime)
    {
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

    internal void ReconcilePersistentActors(IReadOnlyCollection<CharacterId> persistent)
    {
        CharacterConsumablesAggregateState snapshot = ReadState;
        CharacterSubstanceKey[] substances = snapshot.SubstanceStates.Keys
            .Where(key => !persistent.Contains(key.CharacterId)).ToArray();
        CharacterId[] toxicity = snapshot.ToxicityStates.Keys.Where(id => !persistent.Contains(id)).ToArray();
        if (substances.Length == 0 && toxicity.Length == 0) return;
        CharacterConsumablesAggregateState state = WriteState;
        foreach (CharacterSubstanceKey key in substances) state.SubstanceStates.Remove(key);
        foreach (CharacterId id in toxicity) state.ToxicityStates.Remove(id);
    }
}
