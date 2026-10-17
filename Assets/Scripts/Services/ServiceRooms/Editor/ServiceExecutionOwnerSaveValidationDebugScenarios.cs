#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.ServiceRooms;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Detached DTO-only coverage for service execution ownership joins.
/// It deliberately does not construct a world or claim full restore coverage.
/// </summary>
public static class ServiceExecutionOwnerSaveValidationDebugScenarios
{
    private const string SessionId = "service:owner-validation";
    private const string ActorId = "character:owner-validation";
    private const string HubId = "building:owner-validation";
    private const string MealOperationId =
        "consumable-operation:meal:owner-validation";

    [MenuItem("DungeonStory/QA/Service Rooms/Run Execution Owner Save Validation")]
    private static void RunFromMenu() => RunAll();

    public static bool RunAll(bool log = true)
    {
        try
        {
            VerifyTreatmentPhysicalOperationIdentity();
            VerifyFacilityOwnerCardinality();
            VerifyStandardTreatmentOwnerIdentity();
            VerifyCompletedStandardTreatmentDoesNotClaimActiveService();
            VerifyDetoxOwnerIdentityAcrossPhases();
            VerifyMealOwnerIdentityAcrossPhases();
            VerifyAllPreflightBoundariesAreNonMutating();
            if (log)
            {
                Debug.Log(
                    "SERVICE_EXECUTION_OWNER_SAVE_VALIDATION=PASS; "
                    + "scope=detached-dto-cardinality-and-identity; "
                    + "full-restore=not-covered");
            }
            return true;
        }
        catch (Exception exception)
        {
            if (log)
            {
                Debug.LogError(
                    nameof(ServiceExecutionOwnerSaveValidationDebugScenarios)
                    + " failed: " + exception);
            }
            return false;
        }
    }

    private static void VerifyTreatmentPhysicalOperationIdentity()
    {
        const string source = "consumable-operation:treatment:service:qa-a";
        const string expected = "survival-treatment-material:" + source;
        string first = SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(
            source);
        string second = SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(
            source);
        GameplayOperationId operationId = new(first);

        Require(first == expected && second == expected && operationId.IsValid,
            "Treatment physical operation ID was not direct, stable, and valid.");
        Require(first != SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(
                    "consumable-operation:treatment:service:qa-b"),
            "Distinct treatment operation IDs produced the same physical ID.");

        RequireArgumentException(
            () => SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(null),
            "Null treatment operation ID was accepted.");
        RequireArgumentException(
            () => SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(string.Empty),
            "Empty treatment operation ID was accepted.");
        RequireArgumentException(
            () => SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(
                "consumable-operation:treatment:service:%"),
            "Percent-encoded treatment operation ID was accepted.");
        RequireArgumentException(
            () => SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(
                "consumable-operation:treatment:service:치료"),
            "Non-ASCII treatment operation ID was accepted.");

        string sourceAtLimit = "t:" + new string('a', 190);
        Require(new GameplayOperationId(sourceAtLimit).IsValid,
            "Overlength-result fixture source is not a valid operation ID.");
        RequireArgumentException(
            () => SurvivalFoodRuntime.CreateTreatmentPhysicalOperationId(
                sourceAtLimit),
            "Overlength physical treatment operation ID was accepted.");
    }

    private static void VerifyFacilityOwnerCardinality()
    {
        OwnerInput exact = CreateActiveServiceInput();
        exact.Facilities = CreateFacilityWorld(CreateFacilityUse());
        RequireCore(exact, expectedSuccess: true,
            "exact active service and facility execution");

        RequireCore(CreateActiveServiceInput(), expectedSuccess: false,
            "orphan active service");

        OwnerInput doubleClaim = CreateActiveServiceInput();
        doubleClaim.Facilities = CreateFacilityWorld(CreateFacilityUse());
        doubleClaim.Survival.activeTreatmentPlans.Add(CreateTreatmentPlan(
            SurvivalTreatmentPlanPhase.EffectsPublished));
        RequireCore(doubleClaim, expectedSuccess: false,
            "facility and standard treatment double claim");
    }

    private static void VerifyStandardTreatmentOwnerIdentity()
    {
        OwnerInput exact = CreateActiveServiceInput();
        exact.Survival.activeTreatmentPlans.Add(CreateTreatmentPlan(
            SurvivalTreatmentPlanPhase.EffectsPublished));
        RequireCore(exact, expectedSuccess: true,
            "effects-published standard treatment exact identity");

        OwnerInput wrongOperation = CreateActiveServiceInput();
        SurvivalTreatmentPlanSaveData malformed = CreateTreatmentPlan(
            SurvivalTreatmentPlanPhase.EffectsPublished);
        malformed.operationId = "consumable-operation:wrong:" + SessionId;
        wrongOperation.Survival.activeTreatmentPlans.Add(malformed);
        RequireCore(wrongOperation, expectedSuccess: false,
            "standard treatment operation mismatch");

        OwnerInput wrongActor = CreateActiveServiceInput();
        SurvivalTreatmentPlanSaveData otherActor = CreateTreatmentPlan(
            SurvivalTreatmentPlanPhase.EffectsPublished);
        otherActor.patientId = "character:other-owner";
        wrongActor.Survival.activeTreatmentPlans.Add(otherActor);
        RequireCore(wrongActor, expectedSuccess: false,
            "standard treatment actor mismatch");

        OwnerInput wrongHub = CreateActiveServiceInput();
        SurvivalTreatmentPlanSaveData otherHub = CreateTreatmentPlan(
            SurvivalTreatmentPlanPhase.EffectsPublished);
        otherHub.facilityInstanceId = "building:other-owner";
        wrongHub.Survival.activeTreatmentPlans.Add(otherHub);
        RequireCore(wrongHub, expectedSuccess: false,
            "standard treatment hub mismatch");
    }

    private static void VerifyCompletedStandardTreatmentDoesNotClaimActiveService()
    {
        OwnerInput noActiveService = CreateEmptyInput();
        noActiveService.Survival.activeTreatmentPlans.Add(CreateTreatmentPlan(
            SurvivalTreatmentPlanPhase.ServiceCompleted));
        RequireCore(noActiveService, expectedSuccess: true,
            "completed standard treatment without an active service");

        OwnerInput stillActive = CreateActiveServiceInput();
        stillActive.Survival.activeTreatmentPlans.Add(CreateTreatmentPlan(
            SurvivalTreatmentPlanPhase.ServiceCompleted));
        RequireCore(stillActive, expectedSuccess: false,
            "completed standard treatment retaining an active service");
    }

    private static void VerifyDetoxOwnerIdentityAcrossPhases()
    {
        CharacterDetoxTreatmentPlanPhase[] phases =
        {
            CharacterDetoxTreatmentPlanPhase.ItemCommitted,
            CharacterDetoxTreatmentPlanPhase.EffectsPublished,
            CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged
        };
        foreach (CharacterDetoxTreatmentPlanPhase phase in phases)
        {
            OwnerInput exact = CreateActiveServiceInput();
            exact.Consumables.activeDetoxTreatmentPlans.Add(
                CreateDetoxPlan(phase));
            RequireCore(exact, expectedSuccess: true,
                "detox " + phase + " exact identity");
        }

        OwnerInput wrongPrefix = CreateActiveServiceInput();
        CharacterDetoxTreatmentPlanSaveData malformed = CreateDetoxPlan(
            CharacterDetoxTreatmentPlanPhase.ItemCommitted);
        malformed.operationId = "consumable-operation:not-detox:" + SessionId;
        wrongPrefix.Consumables.activeDetoxTreatmentPlans.Add(malformed);
        RequireCore(wrongPrefix, expectedSuccess: false,
            "detox prefix mismatch");

        OwnerInput missingSession = CreateActiveServiceInput();
        CharacterDetoxTreatmentPlanSaveData absent = CreateDetoxPlan(
            CharacterDetoxTreatmentPlanPhase.EffectsPublished);
        absent.operationId = SurvivalFoodRuntime.DetoxOperationPrefix
            + "service:missing-owner";
        missingSession.Consumables.activeDetoxTreatmentPlans.Add(absent);
        RequireCore(missingSession, expectedSuccess: false,
            "detox missing service session");

        OwnerInput wrongActor = CreateActiveServiceInput();
        CharacterDetoxTreatmentPlanSaveData otherActor = CreateDetoxPlan(
            CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged);
        otherActor.characterId = "character:other-owner";
        wrongActor.Consumables.activeDetoxTreatmentPlans.Add(otherActor);
        RequireCore(wrongActor, expectedSuccess: false,
            "detox actor mismatch");

        OwnerInput wrongHub = CreateActiveServiceInput();
        CharacterDetoxTreatmentPlanSaveData otherHub = CreateDetoxPlan(
            CharacterDetoxTreatmentPlanPhase.PhysicalAcknowledged);
        otherHub.facilityInstanceId = "building:other-owner";
        wrongHub.Consumables.activeDetoxTreatmentPlans.Add(otherHub);
        RequireCore(wrongHub, expectedSuccess: false,
            "detox hub mismatch");
    }

    private static void VerifyMealOwnerIdentityAcrossPhases()
    {
        CharacterMealPlanPhase[] activeOnlyPhases =
        {
            CharacterMealPlanPhase.Eating,
            CharacterMealPlanPhase.ItemCommitted,
            CharacterMealPlanPhase.EffectsApplied
        };
        foreach (CharacterMealPlanPhase phase in activeOnlyPhases)
        {
            OwnerInput exact = CreateMealInput();
            exact.Consumables.activeMealPlans.Add(CreateMealPlan(phase));
            RequireCore(exact, expectedSuccess: true,
                "meal " + phase + " active owner");

            OwnerInput completedTooEarly = CreateMealInput();
            completedTooEarly.Consumables.activeMealPlans.Add(
                CreateMealPlan(phase));
            completedTooEarly.Consumables.completedOperations.Add(
                CreateCompletedMeal());
            RequireCore(completedTooEarly, expectedSuccess: false,
                "meal " + phase + " active/completed overlap");
        }

        OwnerInput effectsPublished = CreateMealInput();
        effectsPublished.Consumables.activeMealPlans.Add(CreateMealPlan(
            CharacterMealPlanPhase.EffectsPublished));
        effectsPublished.Consumables.completedOperations.Add(
            CreateCompletedMeal());
        RequireCore(effectsPublished, expectedSuccess: true,
            "effects-published meal active/completed owner");

        OwnerInput effectsPublishedWithoutCompletion = CreateMealInput();
        effectsPublishedWithoutCompletion.Consumables.activeMealPlans.Add(
            CreateMealPlan(CharacterMealPlanPhase.EffectsPublished));
        RequireCore(effectsPublishedWithoutCompletion, expectedSuccess: false,
            "effects-published meal without completed receipt");

        OwnerInput acknowledged = CreateMealInput();
        acknowledged.Consumables.completedOperations.Add(CreateCompletedMeal());
        RequireCore(acknowledged, expectedSuccess: true,
            "acknowledged meal completed owner");

        OwnerInput succeeded = CreateMealInput(
            effectsCommitted: true,
            mealSucceeded: true);
        succeeded.Consumables.completedOperations.Add(CreateCompletedMeal());
        RequireCore(succeeded, expectedSuccess: true,
            "successful facility meal completed owner");

        OwnerInput effectsCommitted = CreateMealInput(effectsCommitted: true);
        effectsCommitted.Consumables.completedOperations.Add(
            CreateCompletedMeal());
        RequireCore(effectsCommitted, expectedSuccess: true,
            "effects-committed facility meal completed owner");

        OwnerInput failed = CreateMealInput();
        failed.Consumables.failedMealOperations.Add(CreateMealFailure(
            recoveryPending: false));
        RequireCore(failed, expectedSuccess: true,
            "terminal failed meal owner");

        foreach (CharacterConsumablesFailureCode code in new[]
                 {
                     CharacterConsumablesFailureCode.ItemNotConsumable,
                     CharacterConsumablesFailureCode.PolicyForbidden
                 })
        {
            OwnerInput alternateFailure = CreateMealInput();
            CharacterMealOperationFailureState failure = CreateMealFailure(
                recoveryPending: false);
            failure.failureCode = code;
            alternateFailure.Consumables.failedMealOperations.Add(failure);
            RequireCore(alternateFailure, expectedSuccess: true,
                "terminal failed meal owner " + code);
        }

        OwnerInput recovery = CreateMealInput();
        recovery.Consumables.activeMealPlans.Add(CreateMealPlan(
            CharacterMealPlanPhase.EffectsApplied));
        recovery.Consumables.failedMealOperations.Add(CreateMealFailure(
            recoveryPending: true));
        RequireCore(recovery, expectedSuccess: true,
            "post-physical meal recovery owner");

        OwnerInput missing = CreateMealInput();
        RequireCore(missing, expectedSuccess: false,
            "meal operation without consumables owner");

        OwnerInput wrongActiveId = CreateMealInput();
        CharacterMealPlanSaveData activeId = CreateMealPlan(
            CharacterMealPlanPhase.Eating);
        activeId.planId = "consumable-operation:meal:wrong-owner";
        wrongActiveId.Consumables.activeMealPlans.Add(activeId);
        RequireCore(wrongActiveId, expectedSuccess: false,
            "meal active operation mismatch");

        OwnerInput wrongActiveActor = CreateMealInput();
        CharacterMealPlanSaveData activeActor = CreateMealPlan(
            CharacterMealPlanPhase.Eating);
        activeActor.characterId = "character:other-owner";
        wrongActiveActor.Consumables.activeMealPlans.Add(activeActor);
        RequireCore(wrongActiveActor, expectedSuccess: false,
            "meal active actor mismatch");

        OwnerInput wrongActiveFacility = CreateMealInput();
        CharacterMealPlanSaveData activeFacility = CreateMealPlan(
            CharacterMealPlanPhase.Eating);
        activeFacility.facilityInstanceId = "building:other-owner";
        wrongActiveFacility.Consumables.activeMealPlans.Add(activeFacility);
        RequireCore(wrongActiveFacility, expectedSuccess: false,
            "meal active facility mismatch");

        OwnerInput wrongCompletedId = CreateMealInput();
        CharacterConsumableOperationState completedId = CreateCompletedMeal();
        completedId.operationId = "consumable-operation:meal:wrong-owner";
        wrongCompletedId.Consumables.completedOperations.Add(completedId);
        RequireCore(wrongCompletedId, expectedSuccess: false,
            "completed meal operation mismatch");

        OwnerInput wrongCompletedActor = CreateMealInput();
        CharacterConsumableOperationState completedActor =
            CreateCompletedMeal();
        completedActor.characterId = "character:other-owner";
        wrongCompletedActor.Consumables.completedOperations.Add(
            completedActor);
        RequireCore(wrongCompletedActor, expectedSuccess: false,
            "completed meal actor mismatch");

        OwnerInput wrongCompletedFacility = CreateMealInput();
        CharacterConsumableOperationState completedFacility =
            CreateCompletedMeal();
        completedFacility.facilityInstanceId = "building:other-owner";
        wrongCompletedFacility.Consumables.completedOperations.Add(
            completedFacility);
        RequireCore(wrongCompletedFacility, expectedSuccess: false,
            "completed meal facility mismatch");

        OwnerInput nonMealCompletion = CreateMealInput();
        CharacterConsumableOperationState nonMeal = CreateCompletedMeal();
        nonMeal.meal = false;
        nonMealCompletion.Consumables.completedOperations.Add(nonMeal);
        RequireCore(nonMealCompletion, expectedSuccess: false,
            "completed operation without meal marker");

        OwnerInput wrongFailureId = CreateMealInput();
        CharacterMealOperationFailureState failureId = CreateMealFailure(
            recoveryPending: false);
        failureId.operationId = "consumable-operation:meal:wrong-owner";
        wrongFailureId.Consumables.failedMealOperations.Add(failureId);
        RequireCore(wrongFailureId, expectedSuccess: false,
            "failed meal operation mismatch");

        OwnerInput wrongFailureActor = CreateMealInput();
        CharacterMealOperationFailureState failureActor = CreateMealFailure(
            recoveryPending: false);
        failureActor.characterId = "character:other-owner";
        wrongFailureActor.Consumables.failedMealOperations.Add(failureActor);
        RequireCore(wrongFailureActor, expectedSuccess: false,
            "failed meal actor mismatch");

        OwnerInput wrongFailureFacility = CreateMealInput();
        CharacterMealOperationFailureState failureFacility =
            CreateMealFailure(recoveryPending: false);
        failureFacility.facilityInstanceId = "building:other-owner";
        wrongFailureFacility.Consumables.failedMealOperations.Add(
            failureFacility);
        RequireCore(wrongFailureFacility, expectedSuccess: false,
            "failed meal facility mismatch");

        OwnerInput completedAndFailed = CreateMealInput();
        completedAndFailed.Consumables.completedOperations.Add(
            CreateCompletedMeal());
        completedAndFailed.Consumables.failedMealOperations.Add(
            CreateMealFailure(recoveryPending: false));
        RequireCore(completedAndFailed, expectedSuccess: false,
            "completed and failed meal overlap");

        OwnerInput activeAndTerminalFailure = CreateMealInput();
        activeAndTerminalFailure.Consumables.activeMealPlans.Add(
            CreateMealPlan(CharacterMealPlanPhase.Eating));
        activeAndTerminalFailure.Consumables.failedMealOperations.Add(
            CreateMealFailure(recoveryPending: false));
        RequireCore(activeAndTerminalFailure, expectedSuccess: false,
            "active and terminal failed meal overlap");

        OwnerInput recoveryWithoutActive = CreateMealInput();
        recoveryWithoutActive.Consumables.failedMealOperations.Add(
            CreateMealFailure(recoveryPending: true));
        RequireCore(recoveryWithoutActive, expectedSuccess: false,
            "meal recovery failure without active post-physical plan");

        OwnerInput recoveryBeforePhysical = CreateMealInput();
        recoveryBeforePhysical.Consumables.activeMealPlans.Add(CreateMealPlan(
            CharacterMealPlanPhase.Eating));
        recoveryBeforePhysical.Consumables.failedMealOperations.Add(
            CreateMealFailure(recoveryPending: true));
        RequireCore(recoveryBeforePhysical, expectedSuccess: false,
            "meal recovery failure before physical commit");

        foreach (CharacterConsumablesFailureCode code in new[]
                 {
                     CharacterConsumablesFailureCode.None,
                     CharacterConsumablesFailureCode.DeliveryPending,
                     (CharacterConsumablesFailureCode)999
                 })
        {
            OwnerInput invalidFailureCode = CreateMealInput();
            CharacterMealOperationFailureState failure = CreateMealFailure(
                recoveryPending: false);
            failure.failureCode = code;
            invalidFailureCode.Consumables.failedMealOperations.Add(failure);
            RequireCore(invalidFailureCode, expectedSuccess: false,
                "terminal meal invalid failure code " + code);
        }

        OwnerInput missingFailureDetail = CreateMealInput();
        CharacterMealOperationFailureState failureDetail = CreateMealFailure(
            recoveryPending: false);
        failureDetail.detail = string.Empty;
        missingFailureDetail.Consumables.failedMealOperations.Add(failureDetail);
        RequireCore(missingFailureDetail, expectedSuccess: false,
            "terminal meal failure without detail");

        OwnerInput terminalMissingCompletion = CreateMealInput(
            effectsCommitted: true,
            mealSucceeded: true);
        RequireCore(terminalMissingCompletion, expectedSuccess: false,
            "successful facility meal without completed receipt");

        OwnerInput terminalWithActive = CreateMealInput(
            effectsCommitted: true,
            mealSucceeded: true);
        terminalWithActive.Consumables.activeMealPlans.Add(CreateMealPlan(
            CharacterMealPlanPhase.EffectsPublished));
        terminalWithActive.Consumables.completedOperations.Add(
            CreateCompletedMeal());
        RequireCore(terminalWithActive, expectedSuccess: false,
            "successful facility meal retaining active plan");

        OwnerInput terminalWithFailure = CreateMealInput(
            effectsCommitted: true,
            mealSucceeded: true);
        terminalWithFailure.Consumables.completedOperations.Add(
            CreateCompletedMeal());
        terminalWithFailure.Consumables.failedMealOperations.Add(
            CreateMealFailure(recoveryPending: false));
        RequireCore(terminalWithFailure, expectedSuccess: false,
            "successful facility meal retaining failure receipt");

        OwnerInput duplicateUse = CreateActiveServiceInput();
        FacilityUseExecutionState firstUse = CreateMealUse();
        FacilityUseExecutionState secondUse = CreateMealUse();
        secondUse.operationSequence = 6L;
        secondUse.operationId = "facility-visit:facility=" + HubId + ":use=6";
        secondUse.actorId = "character:other-owner";
        secondUse.actorDisplayName = "Other Owner Validation Visitor";
        secondUse.before.actorId = secondUse.actorId;
        secondUse.before.displayName = secondUse.actorDisplayName;
        secondUse.serviceSessionId = string.Empty;
        duplicateUse.Facilities = CreateFacilityWorld(firstUse, secondUse);
        duplicateUse.Consumables.activeMealPlans.Add(CreateMealPlan(
            CharacterMealPlanPhase.Eating));
        RequireCore(duplicateUse, expectedSuccess: false,
            "duplicate facility meal operation use");
    }

    private static void VerifyAllPreflightBoundariesAreNonMutating()
    {
        DungeonGameSaveData canonical = CreateEnvelope(
            CreateFacilityOwnedActiveServiceInput());
        RequireAllPreflightResults(canonical, expectedSuccess: true,
            "canonical four-section service execution owner envelope");

        OwnerInput meal = CreateMealInput();
        meal.Consumables.completedOperations.Add(CreateCompletedMeal());
        RequireAllPreflightResults(CreateEnvelope(meal), expectedSuccess: true,
            "canonical four-section meal execution owner envelope");

        RequireAllPreflightResults(
            CreateEnvelope(CreateMealInput()),
            expectedSuccess: false,
            "missing meal owner across public preflight boundaries");

        DungeonGameSaveData missing = Clone(canonical);
        missing.sections.RemoveAll(section =>
            section.sectionId == CharacterConsumablesSaveSection.Id);
        RequireAllPreflightResults(missing, expectedSuccess: false,
            "missing character-consumables section");

        DungeonGameSaveData wrongVersion = Clone(canonical);
        DungeonSaveSectionEnvelope consumables = wrongVersion.sections.Single(
            section => section.sectionId == CharacterConsumablesSaveSection.Id);
        consumables.sectionVersion--;
        RequireAllPreflightResults(wrongVersion, expectedSuccess: false,
            "wrong character-consumables section version");
    }

    private static OwnerInput CreateFacilityOwnedActiveServiceInput()
    {
        OwnerInput input = CreateActiveServiceInput();
        input.Facilities = CreateFacilityWorld(CreateFacilityUse());
        return input;
    }

    private static OwnerInput CreateActiveServiceInput()
    {
        OwnerInput input = CreateEmptyInput();
        input.Services.timelineTime = 105f;
        input.Consumables.timelineTime = 105f;
        input.Services.sessions.Add(new ServiceSessionSaveData
        {
            sessionId = SessionId,
            hubId = HubId,
            actorId = ActorId,
            processId = "service:owner-validation",
            category = ServiceCategory.Medical,
            stage = ServiceSessionStage.Service,
            startedAt = 100f,
            stageStartedAt = 100f,
            contract = new ServiceSessionContractSnapshot
            {
                mode = ServiceOperationMode.Direct,
                activeStages = ServiceProcessStageMask.Service
                    | ServiceProcessStageMask.Payment,
                serviceSeconds = 1f,
                paymentSeconds = 1f,
                paymentRequired = false,
                supportIds = Array.Empty<string>()
            }
        });
        return input;
    }

    private static OwnerInput CreateEmptyInput() => new()
    {
        Services = new ServiceRoomsSaveData(),
        Facilities = new ModularFacilityWorldSaveData(),
        Survival = new DungeonSurvivalSaveData(),
        Consumables = new DungeonCharacterConsumablesSaveData
        {
            timelineTime = 0f
        }
    };

    private static ModularFacilityWorldSaveData CreateFacilityWorld(
        params FacilityUseExecutionState[] uses) => new()
    {
        buildings = new List<ModularFacilityBuildingSaveData>
        {
            new()
            {
                persistentInstanceId = HubId,
                stateModules = new List<BuildingStateModuleSaveData>
                {
                    new()
                    {
                        moduleId = BuildingStateModuleIds.FacilityOperation,
                        version = FacilityRuntimeStateModule.Version,
                        payload = JsonUtility.ToJson(new FacilityRuntimeState
                        {
                            nextUseOperationSequence = 8L,
                            activeUses = new List<FacilityUseExecutionState>(
                                uses)
                        })
                    }
                }
            }
        }
    };

    private static FacilityUseExecutionState CreateFacilityUse() => new()
    {
        operationSequence = 7L,
        operationId = "facility-visit:facility=" + HubId + ":use=7",
        actorId = ActorId,
        actorDisplayName = "Owner Validation Visitor",
        facilityRole = FacilityRole.Rest.ToString(),
        serviceSessionId = SessionId,
        actionType = nameof(AIRest),
        actionBranch = (int)CharacterAiBranch.Rest,
        phase = FacilityUseExecutionPhase.Payment,
        timerInitialized = true,
        remainingSeconds = 1.75f,
        serviceSeconds = 3.5f,
        before = new FacilityUseVisitorState
        {
            actorId = ActorId,
            displayName = "Owner Validation Visitor",
            position = new Vector3(4.5f, 2.25f, 0f),
            runtimeActive = true,
            internalStaff = false,
            canMove = true,
            stayMultiplier = 1.25f,
            revenueMultiplier = 1.1f,
            personalityPatience = 0.75f,
            modelPatience = 0.8f,
            crimeRisk = 0.2f,
            productionMultiplier = 1.05f,
            productionBonus = 2,
            stress = 3f,
            mood = 4f,
            hunger = 5f,
            fun = 6f,
            sleep = 7f,
            excretion = 8f,
            hygiene = 9f,
            health = 10f,
            maximumHealth = 12f,
            injury = 0.5f,
            conditions = new[] { "condition:rested", "condition:well-fed" }
        },
        effectsCommitted = true,
        childResultKeys = new List<string>
        {
            "outcome:room-experience",
            "outcome:need-recovery"
        },
        waterPrepared = true,
        waterFixtureId = "building:water-fixture",
        waterSupplyKind = WaterFixtureSupplyKind.Piped,
        wastewaterAmount = 0.5f
    };

    private static OwnerInput CreateMealInput(
        bool effectsCommitted = false,
        bool mealSucceeded = false)
    {
        OwnerInput input = CreateActiveServiceInput();
        input.Services.sessions.Single().category = ServiceCategory.Dining;
        input.Facilities = CreateFacilityWorld(CreateMealUse(
            effectsCommitted,
            mealSucceeded));
        return input;
    }

    private static FacilityUseExecutionState CreateMealUse(
        bool effectsCommitted = false,
        bool mealSucceeded = false)
    {
        FacilityUseExecutionState use = CreateFacilityUse();
        use.facilityRole = FacilityRole.Meal.ToString();
        use.actionType = nameof(AIEat);
        use.actionBranch = (int)CharacterAiBranch.Eat;
        use.phase = effectsCommitted
            ? FacilityUseExecutionPhase.Payment
            : FacilityUseExecutionPhase.Effects;
        use.effectsCommitted = effectsCommitted;
        use.childResultKeys.Clear();
        use.waterPrepared = false;
        use.waterFixtureId = string.Empty;
        use.waterSupplyKind = WaterFixtureSupplyKind.None;
        use.wastewaterAmount = 0f;
        use.mealOperationId = MealOperationId;
        use.mealWaitElapsedSeconds = 2.5f;
        use.mealSucceeded = mealSucceeded;
        use.mealDisplayName = mealSucceeded
            ? "Owner Validation Meal"
            : string.Empty;
        return use;
    }

    private static CharacterMealPlanSaveData CreateMealPlan(
        CharacterMealPlanPhase phase) => new()
    {
        planId = MealOperationId,
        characterId = ActorId,
        facilityInstanceId = HubId,
        sourceStackId = "stack:meal-owner-validation",
        itemDefinitionId = "food:meal-owner-validation",
        phase = phase,
        createdAt = 100d,
        leaseExpiresAt = 120d,
        expectedCompletionEta = 4f
    };

    private static CharacterConsumableOperationState CreateCompletedMeal() =>
        new()
        {
            operationId = MealOperationId,
            characterId = ActorId,
            itemDefinitionId = "food:meal-owner-validation",
            itemStackId = "stack:meal-owner-validation",
            facilityInstanceId = HubId,
            meal = true,
            completedAt = 104f,
            appliedEffect = 5f,
            outcomeResultKey = "outcome:meal-owner-validation"
        };

    private static CharacterMealOperationFailureState CreateMealFailure(
        bool recoveryPending) => new()
    {
        operationId = MealOperationId,
        characterId = ActorId,
        facilityInstanceId = HubId,
        failureCode = CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
        detail = "meal-owner-validation-failure",
        recoveryPending = recoveryPending
    };

    private static SurvivalTreatmentPlanSaveData CreateTreatmentPlan(
        SurvivalTreatmentPlanPhase phase) => new()
    {
        operationId = SurvivalFoodRuntime.TreatmentOperationPrefix + SessionId,
        patientId = ActorId,
        facilityInstanceId = HubId,
        serviceSessionId = SessionId,
        phase = phase
    };

    private static CharacterDetoxTreatmentPlanSaveData CreateDetoxPlan(
        CharacterDetoxTreatmentPlanPhase phase) => new()
    {
        operationId = SurvivalFoodRuntime.DetoxOperationPrefix + SessionId,
        characterId = ActorId,
        facilityInstanceId = HubId,
        phase = phase
    };

    private static void RequireCore(
        OwnerInput input,
        bool expectedSuccess,
        string scenario)
    {
        DungeonGameRestoreReport report = new();
        string before = SerializeInput(input);
        ServiceExecutionOwnerSaveValidation.ValidateCore(
            input.Services,
            input.Facilities,
            input.Survival,
            input.Consumables,
            report);
        Require(report.Success == expectedSuccess,
            scenario + " verdict was " + report.Success
            + "; errors=" + string.Join(" | ", report.Errors));
        Require(SerializeInput(input) == before,
            scenario + " mutated detached DTO inputs.");
    }

    private static DungeonGameSaveData CreateEnvelope(OwnerInput input) => new()
    {
        sections = new List<DungeonSaveSectionEnvelope>
        {
            CreateSection(
                ServiceRoomsSaveSection.Id,
                ServiceRoomsSaveData.CurrentVersion,
                input.Services),
            CreateSection(
                ModularFacilityWorldSaveSection.Id,
                ModularFacilityWorldSaveSection.CurrentSectionVersion,
                input.Facilities),
            CreateSection(
                SurvivalResourcesSaveSection.Id,
                DungeonSurvivalSaveData.CurrentVersion,
                input.Survival),
            CreateSection(
                CharacterConsumablesSaveSection.Id,
                DungeonCharacterConsumablesSaveData.CurrentVersion,
                input.Consumables)
        }
    };

    private static DungeonSaveSectionEnvelope CreateSection<T>(
        string id,
        int version,
        T payload)
        where T : class => new()
        {
            sectionId = id,
            sectionVersion = version,
            restorePhase = DungeonSaveRestorePhase.RuntimeState,
            optional = false,
            payloadJson = JsonUtility.ToJson(payload)
        };

    private static void RequireAllPreflightResults(
        DungeonGameSaveData save,
        bool expectedSuccess,
        string scenario)
    {
        string before = JsonUtility.ToJson(save);
        ServiceExecutionOwnerSaveValidation validator = new();
        DungeonGameRestoreReport capturedReport = new();
        DungeonGameRestoreReport restoreReport = new();
        DungeonGameRestoreReport registryReport = new();

        ((IDungeonCapturedSavePreflightValidator)validator).Validate(
            save,
            capturedReport);
        ((IDungeonSavePreflightValidator)validator).Validate(
            save,
            restoreReport);
        ((IDungeonSaveRegistryPreflightValidator)validator).Validate(
            save.sections.ToDictionary(
                section => section.sectionId,
                StringComparer.Ordinal),
            registryReport);

        Require(capturedReport.Success == expectedSuccess
                && restoreReport.Success == expectedSuccess
                && registryReport.Success == expectedSuccess,
            scenario + " had inconsistent captured/restore/registry verdicts: "
            + capturedReport.Success + "/" + restoreReport.Success + "/"
            + registryReport.Success + ".");
        Require(JsonUtility.ToJson(save) == before,
            scenario + " preflight validation mutated saved JSON.");
    }

    private static DungeonGameSaveData Clone(DungeonGameSaveData source) =>
        JsonUtility.FromJson<DungeonGameSaveData>(JsonUtility.ToJson(source));

    private static string SerializeInput(OwnerInput input) =>
        JsonUtility.ToJson(CreateEnvelope(input));

    private sealed class OwnerInput
    {
        internal ServiceRoomsSaveData Services { get; set; }
        internal ModularFacilityWorldSaveData Facilities { get; set; }
        internal DungeonSurvivalSaveData Survival { get; set; }
        internal DungeonCharacterConsumablesSaveData Consumables { get; set; }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireArgumentException(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
#endif
