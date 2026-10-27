using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

// 방문 진행과 분리한 소비 실행. 진행값은 매번 현재 시설 저장 권위에서 읽는다.
internal sealed class FacilityUseEffectsExecution
{
    private readonly Facility facility;
    private readonly IBuildingVisitorPort actor;
    private readonly Func<FacilityUseExecutionState> currentState;
    private readonly IMealConsumptionRuntime meals;
    private readonly IWaterFixtureUseRuntime water;
    private readonly IRoomEnvironmentExperienceService rooms;
    private readonly IGameClock clock;
    private readonly string label;
    private FacilityUseExecutionState State => currentState();
    internal BuildingMealUseSnapshot Meal { get; private set; }
    internal bool Succeeded { get; private set; }
    internal bool Cancelled { get; private set; }
    internal bool ResourceFailure { get; private set; }
    internal string FailureCode { get; private set; } = string.Empty;
    internal string FailureDetail { get; private set; } = string.Empty;
    internal string ActivityDetail { get; private set; } = string.Empty;

    internal FacilityUseEffectsExecution(Facility facility, IBuildingVisitorPort actor,
        Func<FacilityUseExecutionState> currentState, IMealConsumptionRuntime meals,
        IWaterFixtureUseRuntime water, IRoomEnvironmentExperienceService rooms,
        IGameClock clock, string label)
    {
        this.facility = facility ?? throw new ArgumentNullException(nameof(facility));
        this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
        this.currentState = currentState ?? throw new ArgumentNullException(nameof(currentState));
        this.meals = meals; this.water = water; this.rooms = rooms;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.label = label;
        Meal = State.mealSucceeded
            ? new BuildingMealUseSnapshot(true, string.Empty, State.mealDisplayName, 0) : default;
    }

    internal static bool UsesPhysicalMeal(Facility facility) =>
        facility.BuildingData?.GetAbility<BuildingRecreationalSubstanceServiceAbility>()?.IsValid != true
        && facility.Facility != null && facility.Facility.SupportsRole(FacilityRole.Meal);

    internal IEnumerator Apply()
    {
        WaterFixtureUseTicket ticket = default;
        BuildingWaterFixtureAbility fixture = facility.BuildingData?.GetAbility<BuildingWaterFixtureAbility>();
        // 수동 급수와 폐수도 기존 물 사용 권위의 원자 명령을 따른다.
        if (!State.waterPrepared && water != null && fixture != null
            && !water.TryBeginUse(facility, actor.BuildingCharacterId, out ticket, out DomainFailure failure))
        {
            ResourceFailure = true;
            FailureCode = failure.Code.ToString();
            yield break;
        }
        if (!State.waterPrepared)
        {
            State.waterPrepared = true;
            State.waterFixtureId = ticket.IsValid ? ticket.FixtureId.Value : string.Empty;
            State.waterSupplyKind = ticket.SupplyKind;
            State.wastewaterAmount = ticket.WastewaterAmount;
        }
        else if (State.waterSupplyKind != WaterFixtureSupplyKind.None)
            ticket = new WaterFixtureUseTicket(new BuildingInstanceId(State.waterFixtureId),
                State.waterSupplyKind, State.wastewaterAmount);

        var recreation = facility.BuildingData?.GetAbility<BuildingRecreationalSubstanceServiceAbility>();
        if (recreation?.IsValid == true)
        {
            if (!actor.TryConsumeRecreationalSubstance(facility, out BuildingRecreationalSubstanceUseSnapshot result))
            {
                FailureCode = string.IsNullOrWhiteSpace(result.FailureCode)
                    ? CharacterConsumablesFailureCode.InvalidCommand.ToString() : result.FailureCode;
                ActivityDetail = FailureCode;
                yield break;
            }
            var recoveryKeys = ApplyRecovery(facility, actor, rooms, label,
                0f, 0f, recreation.funRecovery, 0f, 0f, 0f);
            State.childResultKeys.AddRange(recoveryKeys);
            float sentiment = recreation.facilitySentiment;
            string socialDetail = $"{result.DisplayName}을(를) 즐김";
            actor.RememberFacilityExperience(facility, sentiment, socialDetail);
            actor.RecordActivity(facility, new BuildingActivitySnapshot(
                BuildingActivityKinds.Social, BuildingActivityOutcomes.Completed, socialDetail,
                actionId: "social:recreational-drink",
                reasonCode: result.Overdosed ? "recreational-drink-overdose"
                    : result.BecameAddicted ? "recreational-drink-addiction" : "recreational-drink",
                value: recreation.funRecovery,
                sentiment: result.Overdosed ? -0.5f : sentiment, bubbleEligible: true));
            if (!string.IsNullOrWhiteSpace(result.OutcomeResultKey)) State.childResultKeys.Add(result.OutcomeResultKey);
            State.effectsCommitted = true;
        }
        else if (UsesPhysicalMeal(facility))
        {
            bool consumed;
            BuildingMealUseSnapshot meal;
            if (!string.IsNullOrEmpty(State.mealOperationId))
            {
                bool found = actor.TryGetMealConsumptionResult(meals, State.mealOperationId, out meal);
                if (!found && !string.Equals(meal.OperationId, State.mealOperationId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Restored facility meal operation is missing: " + State.mealOperationId);
                consumed = meal.Success;
            }
            else
            {
                meal = default;
                consumed = meals != null && actor.TryConsumeMeal(meals, facility, out meal);
                State.mealOperationId = meal.OperationId ?? string.Empty;
            }
            if (!consumed && meal.AcceptedPending)
            {
                actor.SetActionPhase("식사 중", facility, "4s");
                while (State.mealWaitElapsedSeconds < 15f)
                {
                    // 호출자가 취소 여부를 확인한 뒤 다음 MoveNext로 돌아온다.
                    yield return null;
                    State.mealWaitElapsedSeconds = Mathf.Min(15f, State.mealWaitElapsedSeconds + clock.DeltaTime);
                    if (!actor.TryGetMealConsumptionResult(meals, meal.OperationId, out BuildingMealUseSnapshot next))
                    { meal = next; break; }
                    meal = next;
                    if (meal.Success) { consumed = true; break; }
                    if (!meal.AcceptedPending) break;
                }
                if (!consumed && meal.AcceptedPending)
                    meal = new BuildingMealUseSnapshot(false,
                        CharacterConsumablesFailureCode.PhysicalConsumptionFailed.ToString(),
                        string.Empty, 0, failureDetail: "meal-action-timeout");
            }
            Meal = meal;
            if (!consumed)
            {
                if (meal.IsRetryableUnavailable || meal.IsNoLongerNeeded)
                {
                    Cancelled = true;
                    FailureCode = meal.IsRetryableUnavailable ? "meal-supply-retry" : "meal-no-longer-needed";
                    ActivityDetail = meal.IsRetryableUnavailable
                        ? $"{label} meal supply changed before commit; retrying selection."
                        : $"{label} meal plan retired because the need was already satisfied.";
                }
                else
                {
                    FailureCode = meals == null ? "InvalidCommand"
                        : string.IsNullOrWhiteSpace(meal.FailureCode)
                            ? CharacterConsumablesFailureCode.PhysicalConsumptionFailed.ToString() : meal.FailureCode;
                    FailureDetail = meal.FailureDetail;
                    ActivityDetail = string.IsNullOrWhiteSpace(FailureDetail) ? FailureCode : $"{FailureCode}:{FailureDetail}";
                }
                yield break;
            }
            if (!string.IsNullOrWhiteSpace(meal.OutcomeResultKey)) State.childResultKeys.Add(meal.OutcomeResultKey);
            State.effectsCommitted = true;
            State.mealSucceeded = true;
            State.mealDisplayName = meal.DisplayName;
        }
        else
        {
            var recoveryKeys = ApplyConfiguredRecovery(facility, actor, rooms, label);
            State.childResultKeys.AddRange(recoveryKeys);
        }

        water?.CompleteUse(facility, ticket);
        actor.ApplyFacilityUseCompleted(facility);
        State.effectsCommitted = true;
        var roomKeys = actor.ApplyRoomExperienceDetailed(rooms, facility, "facility-use").ChildResultKeys;
        State.childResultKeys.AddRange(roomKeys);
        Succeeded = true;
    }

    internal static IReadOnlyList<string> ApplyConfiguredRecovery(
        Facility facility, IBuildingVisitorPort actor, IRoomEnvironmentExperienceService rooms, string label)
    {
        if (actor == null || facility.Facility == null)
        {
            return Array.Empty<string>();
        }

        FacilityNeedRecoveryData configured = facility.BuildingData != null
            ? facility.BuildingData.GetNeedRecovery()
            : default;
        float sleep = configured.sleep;
        float mood = configured.mood;
        float fun = configured.fun;
        float hunger = configured.hunger;
        float excretion = configured.excretion;
        float hygiene = configured.hygiene;

        if (configured.HasEffect)
        {
            return ApplyRecovery(facility, actor, rooms, label,
                sleep,
                mood,
                fun,
                hunger,
                excretion,
                hygiene);
        }

        if (facility.Facility.SupportsRole(FacilityRole.Rest))
        {
            sleep += 35f;
            mood += 12f;
        }

        if (facility.Facility.SupportsRole(FacilityRole.Training))
        {
            fun += 15f;
            mood += 5f;
        }

        if (facility.Facility.SupportsRole(FacilityRole.Research))
        {
            fun += 10f;
            mood += 8f;
        }

        if (facility.Facility.SupportsRole(FacilityRole.Mana))
        {
            mood += 10f;
        }

        if (facility.Facility.SupportsRole(FacilityRole.Logistics))
        {
            mood += 3f;
        }

        if (facility.Facility.SupportsRole(FacilityRole.Meal))
        {
            hunger += 35f;
            mood += 5f;
        }

        if (facility.Facility.SupportsRole(FacilityRole.Toilet))
        {
            excretion += 70f;
            mood += 2f;
        }

        if (facility.Facility.SupportsRole(FacilityRole.Hygiene))
        {
            hygiene += 60f;
            mood += 4f;
        }

        return ApplyRecovery(facility, actor, rooms, label,
            sleep,
            mood,
            fun,
            hunger,
            excretion,
            hygiene);
    }

    private static IReadOnlyList<string> ApplyRecovery(
        Facility facility, IBuildingVisitorPort actor, IRoomEnvironmentExperienceService rooms, string label,
        float sleep,
        float mood,
        float fun,
        float hunger,
        float excretion,
        float hygiene)
    {
        if (sleep == 0f
            && mood == 0f
            && fun == 0f
            && hunger == 0f
            && excretion == 0f
            && hygiene == 0f)
        {
            return Array.Empty<string>();
        }

        BuildingNeedRecoveryResult result = actor.ApplyNeedRecoveryDetailed(
            new BuildingNeedRecoverySnapshot(
            sleep,
            mood,
            fun,
            hunger,
            excretion,
            hygiene,
            $"facility:{facility.RequirePersistentInstanceId().Value}",
            rooms?.GetActiveConditionIds(facility),
            $"{label} 이용"));
        return result.ChildResultKeys;
    }

}
