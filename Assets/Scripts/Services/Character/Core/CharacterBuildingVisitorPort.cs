using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CharacterBuildingVisitorAdapter : IBuildingVisitorPort
{
    private readonly CharacterActor actor;
    private IBuildingShoppingVisitorPort buildingShoppingVisitorPort;

    public CharacterBuildingVisitorAdapter(CharacterActor actor)
    {
        this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    internal CharacterActor Actor => actor;

    CharacterId IBuildingCharacterPort.BuildingCharacterId =>
        actor.BuildingCharacterId;
    string IBuildingCharacterPort.BuildingDisplayName =>
        actor.BuildingDisplayName;
    bool IBuildingCharacterPort.IsBuildingInteractionAvailable =>
        actor.IsBuildingInteractionAvailable;

    internal static bool TryResolve(
        object runtimeObject,
        out IBuildingVisitorPort visitor)
    {
        switch (runtimeObject)
        {
            case CharacterActor character:
                visitor = character.BuildingVisitor;
                return true;
            case IBuildingVisitorPort existing:
                visitor = existing;
                return true;
            default:
                visitor = null;
                return false;
        }
    }

    internal static bool TryGetActor(
        IBuildingCharacterPort visitor,
        out CharacterActor actor)
    {
        actor = visitor switch
        {
            CharacterActor character => character,
            CharacterBuildingVisitorAdapter adapter => adapter.Actor,
            _ => null
        };
        return actor != null;
    }

    internal static CharacterActor GetActorOrNull(
        IBuildingCharacterPort visitor) =>
        TryGetActor(visitor, out CharacterActor actor) ? actor : null;

    private CharacterIdentity Identity => actor.Identity;
    private CharacterStats Stats => actor.Stats;
    private CharacterLifecycle Lifecycle => actor.Lifecycle;
    private CharacterProgression Progression => actor.Progression;
    private CharacterSocialMemory SocialMemory => actor.SocialMemory;
    private AIBrain Brain => actor.Brain;
    private Transform transform => actor.transform;
    private GameObject gameObject => actor.gameObject;
    private string name => actor.name;
    private IWorldItemStackRuntime WorldItemStackRuntime =>
        actor.WorldItemStackRuntime;

    private T GetAbility<T>() where T : CharacterAbility =>
        actor.GetAbility<T>();

    private bool TryGetAbility<T>(out T ability) where T : CharacterAbility =>
        actor.TryGetAbility(out ability);

    private float GetCrimeRiskMultiplier() => actor.GetCrimeRiskMultiplier();
    private void HideForTraversal(float delay) => actor.HideForTraversal(delay);
    private void RestoreTraversalVisibility() =>
        actor.RestoreTraversalVisibility();
    private void ChangeLayer(string layerName) => actor.ChangeLayer(layerName);
    private void Flip(CharacterFacing facing) => actor.Flip(facing);
    private void ApplyMoodFactor(
        string sourceId,
        string description,
        float amount,
        float duration,
        int stackLimit) =>
        actor.ApplyMoodFactor(
            sourceId,
            description,
            amount,
            duration,
            stackLimit);
    private void AddActivity(CharacterActivityEvent activity) =>
        actor.AddActivity(activity);
    private void ChangesStat(CharacterCondition condition, float amount) =>
        actor.ChangesStat(condition, amount);

    BuildingVisitorSnapshot IBuildingVisitorPort.VisitorSnapshot
    {
        get
        {
            bool internalStaff = CharacterWorkRoleUtility.TryGetWork(actor, out _);
            CharacterAiPersonality personality = Identity?.Data?.aiPersonality;
            return new BuildingVisitorSnapshot(
                Identity?.PersistentId ?? string.Empty,
                Identity?.DisplayName ?? name,
                transform.position,
                gameObject != null
                    && gameObject.scene.IsValid()
                    && gameObject.activeInHierarchy,
                internalStaff,
                GetAbility<AbilityMove>() != null,
                Stats?.GetStayDurationMultiplier() ?? 1f,
                CharacterSkillRuntimeEffects.GetRevenueMultiplier(actor),
                personality?.patience ?? 1f,
                Stats?.GetWaitPatienceMultiplier() ?? 1f,
                GetCrimeRiskMultiplier(),
                CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(actor),
                CharacterSkillRuntimeEffects.GetStockProductionBonus(actor),
                Lifecycle?.ExpeditionRecovery.stress ?? 0f,
                GetNeed(CharacterCondition.MOOD, 50f),
                GetNeed(CharacterCondition.HUNGER, 50f),
                GetNeed(CharacterCondition.FUN, 50f),
                GetNeed(CharacterCondition.SLEEP, 50f),
                GetNeed(CharacterCondition.EXCRETION, 50f),
                GetNeed(CharacterCondition.HYGIENE, 50f));
        }
    }

    IBuildingShoppingVisitorPort IBuildingVisitorPort.Shopping =>
        GetAbility<AbilityShopping>() == null
            ? null
            : buildingShoppingVisitorPort ??= new ShoppingVisitorPort(actor);

    object IBuildingVisitorPort.CurrentActionToken => Brain?.bestAction;
    bool IBuildingVisitorPort.IsCurrentActionEnded =>
        Brain == null || Brain.isBestActionEnd;

    bool IBuildingVisitorPort.IsCurrentAction(object expectedAction) =>
        expectedAction == null || ReferenceEquals(Brain?.bestAction, expectedAction);

    void IBuildingVisitorPort.SetActionPhase(
        string phase,
        IBuildingWorldEntryPort destination,
        string detail)
    {
        Brain?.SetActionPhase(phase, destination as BuildableObject, detail);
    }

    void IBuildingVisitorPort.ReportInteractionFailure(
        BuildingInteractionFailureKind failureKind,
        string detail,
        IBuildingWorldEntryPort destination)
    {
        AIActionFailureKind aiFailureKind = failureKind switch
        {
            BuildingInteractionFailureKind.FacilityDestroyed =>
                AIActionFailureKind.Destroyed,
            BuildingInteractionFailureKind.ActorUnavailable =>
                AIActionFailureKind.CannotStart,
            BuildingInteractionFailureKind.ActionReplaced =>
                AIActionFailureKind.CannotStart,
            BuildingInteractionFailureKind.AdmissionRejected =>
                AIActionFailureKind.FacilityAdmissionRejected,
            BuildingInteractionFailureKind.ServiceUnavailable =>
                AIActionFailureKind.FacilityServiceUnavailable,
            BuildingInteractionFailureKind.ResourceUnavailable =>
                AIActionFailureKind.ResourceUnavailable,
            BuildingInteractionFailureKind.ConsumptionFailed =>
                AIActionFailureKind.ConsumptionFailed,
            _ => AIActionFailureKind.Unknown
        };
        Brain?.ReportRuntimeActionFailure(
            AIActionFailure.Create(
                aiFailureKind,
                detail,
                destination as BuildableObject),
            requestImmediateReplan: true);
    }

    void IBuildingVisitorPort.ReportInteractionCancellation(
        BuildingInteractionFailureKind failureKind,
        string detail,
        IBuildingWorldEntryPort destination)
    {
        if (failureKind == BuildingInteractionFailureKind.ActionReplaced)
        {
            Brain?.NotifyInteractionActionReplaced(
                detail,
                destination as BuildableObject);
        }
    }

    IEnumerator IBuildingVisitorPort.MoveTo(
        Vector3 position,
        float speed,
        object expectedAction)
    {
        AbilityMove movement = GetAbility<AbilityMove>();
        return movement != null
            ? movement.Move2PosBySpeed(
                position,
                speed,
                expectedAction as AIAction)
            : EmptyBuildingPortRoutine();
    }

    IEnumerator IBuildingVisitorPort.MoveToGrid(Vector2Int position)
    {
        AbilityMove movement = GetAbility<AbilityMove>();
        return movement != null
            ? movement.Move2GridPosition(position)
            : EmptyBuildingPortRoutine();
    }

    void IBuildingVisitorPort.SetWorldPosition(Vector3 position) =>
        transform.position = position;

    void IBuildingVisitorPort.HideForTraversal(float failSafeDelay) =>
        HideForTraversal(failSafeDelay);

    void IBuildingVisitorPort.RestoreTraversalVisibility() =>
        RestoreTraversalVisibility();

    void IBuildingVisitorPort.ChangeLayer(string layerName) =>
        ChangeLayer(layerName);

    void IBuildingVisitorPort.FaceRight() => Flip(CharacterFacing.RIGHT);

    void IBuildingVisitorPort.ApplyMoodFactor(
        string sourceId,
        string description,
        float amount,
        float duration,
        int stackLimit) =>
        ApplyMoodFactor(sourceId, description, amount, duration, stackLimit);

    void IBuildingVisitorPort.RecordActivity(
        IBuildingWorldEntryPort facility,
        BuildingActivitySnapshot activity)
    {
        if (string.Equals(
                activity.KindId,
                BuildingActivityKinds.Work,
                StringComparison.Ordinal))
        {
            AddActivity(CharacterActivityEvent.Work(
                new WorkTypeId(activity.WorkTypeId),
                activity.OutcomeId,
                activity.FactText,
                facility as BuildableObject,
                reasonCode: activity.ReasonCode,
                value: activity.Value,
                quantity: activity.Quantity,
                bubbleEligible: activity.BubbleEligible));
            return;
        }

        AddActivity(CharacterActivityEvent.Facility(
            activity.KindId,
            activity.OutcomeId,
            activity.FactText,
            facility as BuildableObject,
            actionId: activity.ActionId,
            reasonCode: activity.ReasonCode,
            value: activity.Value,
            quantity: activity.Quantity,
            bubbleEligible: activity.BubbleEligible));
    }

    void IBuildingVisitorPort.RememberFacilityExperience(
        IBuildingWorldEntryPort facility,
        float sentiment,
        string detail)
    {
        SocialMemory?.RememberFacilityExperience(
            facility as BuildableObject,
            sentiment,
            detail);
    }

    void IBuildingVisitorPort.ApplyNeedRecovery(
        BuildingNeedRecoverySnapshot recovery)
    {
        if (recovery.Mood != 0f)
        {
            ApplyMoodFactor(
                recovery.SourceId,
                recovery.SourceName,
                recovery.Mood,
                180f,
                2);
        }

        if (TryGetAbility(out AbilityWork work))
        {
            work.RecoverOffDuty(
                recovery.Sleep,
                0f,
                recovery.Fun,
                recovery.Hunger,
                recovery.Excretion,
                recovery.Hygiene,
                recovery.ActiveConditionIds);
            return;
        }

        if (recovery.Sleep != 0f)
        {
            Stats?.RecoverNeed(
                CharacterCondition.SLEEP,
                recovery.Sleep,
                CharacterNeedRecoverySource.Rest,
                recovery.ActiveConditionIds);
        }
        if (recovery.Fun != 0f)
        {
            ChangesStat(CharacterCondition.FUN, recovery.Fun);
        }
        if (recovery.Hunger != 0f)
        {
            Stats?.RecoverNeed(
                CharacterCondition.HUNGER,
                recovery.Hunger,
                CharacterNeedRecoverySource.Meal);
        }
        if (recovery.Excretion != 0f)
        {
            Stats?.RecoverNeed(
                CharacterCondition.EXCRETION,
                recovery.Excretion,
                CharacterNeedRecoverySource.Toilet);
        }
        if (recovery.Hygiene != 0f)
        {
            Stats?.RecoverNeed(
                CharacterCondition.HYGIENE,
                recovery.Hygiene,
                CharacterNeedRecoverySource.Hygiene);
        }
    }

    bool IBuildingVisitorPort.TryConsumeMeal(
        object mealRuntime,
        IBuildingWorldEntryPort facility,
        out BuildingMealUseSnapshot result)
    {
        if (mealRuntime is IMealConsumptionRuntime runtime
            && facility is BuildableObject building)
        {
            bool consumed = runtime.TryConsumeMeal(
                actor,
                building,
                out MealConsumptionResult meal);
            result = new BuildingMealUseSnapshot(
                consumed,
                consumed ? string.Empty : meal.FailureCode.ToString(),
                meal.DisplayName,
                meal.UnitPrice,
                meal.IsAcceptedPending,
                meal.OperationId.Value,
                string.Join(",", meal.Parameters),
                IsBenignSatisfiedMealResult(meal),
                IsRetryableUnavailableMealResult(meal));
            return consumed;
        }

        string failureCode = mealRuntime == null
            ? CharacterConsumablesFailureCode.InvalidCommand.ToString()
            : "meal-consumption-failed";
        result = new BuildingMealUseSnapshot(false, failureCode, string.Empty, 0);
        return false;
    }

    bool IBuildingVisitorPort.TryGetMealConsumptionResult(
        object mealRuntime,
        string operationId,
        out BuildingMealUseSnapshot result)
    {
        if (mealRuntime is IMealConsumptionRuntime runtime)
        {
            bool resolvedSuccessfully = runtime.TryGetMealOperationResult(
                new ConsumableOperationId(operationId),
                out MealConsumptionResult meal);
            result = new BuildingMealUseSnapshot(
                meal.Success,
                meal.Success ? string.Empty : meal.FailureCode.ToString(),
                meal.DisplayName,
                meal.UnitPrice,
                meal.IsAcceptedPending,
                meal.OperationId.Value,
                string.Join(",", meal.Parameters),
                IsBenignSatisfiedMealResult(meal),
                IsRetryableUnavailableMealResult(meal));
            // A false return is a resolved operation failure, not a missing
            // result. Preserve its typed parameters so the facility, AI
            // blackboard, and activity trace expose the actual commit cause.
            return resolvedSuccessfully;
        }

        result = new BuildingMealUseSnapshot(
            false,
            CharacterConsumablesFailureCode.PhysicalConsumptionFailed.ToString(),
            string.Empty,
            0);
        return false;
    }

    private static bool IsBenignSatisfiedMealResult(MealConsumptionResult meal)
    {
        if (meal.Success
            || meal.FailureCode != CharacterConsumablesFailureCode.PolicyForbidden)
        {
            return false;
        }

        IReadOnlyList<string> parameters = meal.Parameters;
        for (int index = 0; index < parameters.Count; index++)
        {
            string parameter = parameters[index];
            if (string.Equals(parameter, "not-hungry", StringComparison.Ordinal)
                || string.Equals(
                    parameter,
                    "meal-followup-cooldown",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsRetryableUnavailableMealResult(
        MealConsumptionResult meal) =>
        !meal.Success
        && !meal.IsAcceptedPending
        && meal.FailureCode == CharacterConsumablesFailureCode.DeliveryPending;

    bool IBuildingVisitorPort.TryConsumeRecreationalSubstance(
        IBuildingWorldEntryPort facility,
        out BuildingRecreationalSubstanceUseSnapshot result)
    {
        ICharacterSubstanceRuntime runtime = actor.SubstanceRuntime;
        if (runtime != null && facility is BuildableObject building)
        {
            bool consumed = runtime.TryConsumeAtFacility(
                actor,
                building,
                out SubstanceUseResult substance);
            result = new BuildingRecreationalSubstanceUseSnapshot(
                consumed,
                consumed ? string.Empty : substance.FailureCode.ToString(),
                substance.DisplayName,
                substance.BecameAddicted,
                substance.Overdosed);
            return consumed;
        }

        result = new BuildingRecreationalSubstanceUseSnapshot(
            false,
            CharacterConsumablesFailureCode.InvalidCommand.ToString(),
            string.Empty,
            false,
            false);
        return false;
    }

    void IBuildingVisitorPort.ApplyRoomExperience(
        object roomExperienceRuntime,
        IBuildingWorldEntryPort facility,
        string activityId)
    {
        if (roomExperienceRuntime is not IRoomEnvironmentExperienceService runtime
            || facility is not BuildableObject building)
        {
            return;
        }

        RoomExperienceActivity activity = string.Equals(
            activityId,
            "shopping",
            StringComparison.Ordinal)
                ? RoomExperienceActivity.Shopping
                : RoomExperienceActivity.FacilityUse;
        runtime.Apply(new RoomEnvironmentExperienceEvent(actor, building, activity));
    }

    void IBuildingVisitorPort.ApplyFacilityUseCompleted(
        IBuildingWorldEntryPort facility)
    {
        if (facility is BuildableObject building)
        {
            ModularFacilityRuntimeEffects.ApplyUseCompleted(this, building);
            if (actor.TryGetAbility(out AbilityWork work))
            {
                work.AwardCompletedCombatTraining(building);
                work.NotifyRoutineNeedServiceCompleted();
            }
        }
    }

    void IBuildingVisitorPort.ApplyExpeditionRecovery(
        float healthHealRatio,
        float injuryReduction,
        float stressRecovery)
    {
        Lifecycle?.ApplyExpeditionRecovery(
            healthHealRatio,
            injuryReduction,
            stressRecovery);
    }

    void IBuildingVisitorPort.AddExperience(int amount)
    {
        Progression?.AddExperience(Mathf.Max(0, amount));
    }

    void IBuildingVisitorPort.ApplyNeedDelta(string needId, float amount)
    {
        ICharacterNeedDefinitionCatalog needCatalog = Stats?.NeedDefinitionCatalog
            ?? throw new InvalidOperationException(
                "Building purchase effect requires the authored character-need catalog.");
        if (!needCatalog.TryGet(needId, out CharacterNeedDefinition definition))
        {
            throw new InvalidOperationException(
                $"Building purchase effect targets unknown character need '{needId}'.");
        }

        ChangesStat(definition.Condition, amount);
    }

    void IBuildingVisitorPort.AddCarriedItem(
        string sourceId,
        string itemDefinitionId,
        int quantity)
    {
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        IWorldItemStackRuntime itemRuntime = WorldItemStackRuntime;
        if (inventory == null || itemRuntime == null || quantity <= 0)
        {
            return;
        }

        ItemDefinitionId itemId = new(itemDefinitionId);
        if (!itemId.IsValid
            || !itemRuntime.CatalogProvider.TryGetDefinition(itemId.Value, out _))
        {
            throw new InvalidOperationException(
                $"Building visitor received unknown physical item '{itemDefinitionId}'.");
        }

        inventory.TryAdd(
            sourceId,
            itemId.Value,
            quantity,
            itemRuntime.CatalogProvider,
            itemRuntime.HaulingSettingsProvider,
            out _);
    }

    private float GetNeed(CharacterCondition condition, float defaultValue)
    {
        return Stats != null
            && Stats.Stats.TryGetValue(condition, out float value)
                ? Mathf.Clamp(value, 0f, 100f)
                : defaultValue;
    }

    private static IEnumerator EmptyBuildingPortRoutine()
    {
        yield break;
    }

    private sealed class ShoppingVisitorPort : IBuildingShoppingVisitorPort
    {
        private readonly CharacterActor actor;

        public ShoppingVisitorPort(CharacterActor actor)
        {
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }

        private AbilityShopping Shopping => actor.GetAbility<AbilityShopping>();

        public BuildingVisitOutcome LastVisitOutcome =>
            (BuildingVisitOutcome)(Shopping?.LastVisitOutcome
                ?? ShoppingVisitOutcome.None);

        public int GetShoppingCount() => Shopping?.GetShoppingCount() ?? 0;

        public int SelectOffer(IReadOnlyList<BuildingRetailOfferSnapshot> offers)
        {
            if (Shopping == null || offers == null)
            {
                return -1;
            }

            List<Stock> stocks = new List<Stock>(offers.Count);
            for (int index = 0; index < offers.Count; index++)
            {
                stocks.Add(new Stock(offers[index].ItemId, offers[index].Cost));
            }
            return Shopping.DetermineBuyingItem(stocks).id;
        }

        public bool CanPay(int amount) => Shopping?.CanPayAmount(amount) == true;

        public IEnumerator Purchase(
            object stockToken,
            int cost,
            object expectedAction,
            IBuildingWorldEntryPort expectedFacility,
            BuildingRetailPurchaseCommitResult commitResult) =>
            stockToken is RemainStock stock && Shopping != null
                ? Shopping.BuyItem(
                    stock,
                    cost,
                    expectedAction as AIAction,
                    expectedFacility as BuildableObject,
                    commitResult)
                : RejectPurchase(commitResult, "shopping-port-unavailable");

        private static IEnumerator RejectPurchase(
            BuildingRetailPurchaseCommitResult commitResult,
            string failureCode)
        {
            commitResult?.Reject(failureCode);
            yield break;
        }

        public IEnumerator PayForService(
            int amount,
            object expectedAction,
            IBuildingWorldEntryPort expectedFacility) =>
            Shopping?.PayForService(
                amount,
                expectedAction as AIAction,
                expectedFacility as BuildableObject)
            ?? EmptyBuildingPortRoutine();

        public void SetVisitOutcome(
            IBuildingWorldEntryPort building,
            BuildingVisitOutcome outcome)
        {
            Shopping?.SetVisitOutcome(
                building as BuildableObject,
                (ShoppingVisitOutcome)outcome);
        }
    }
}
