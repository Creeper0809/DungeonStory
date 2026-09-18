using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class SurvivalDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Survival/Run Survival Scenarios")]
    public static void RunFromMenu()
    {
        List<string> errors = RunAll();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Survival scenarios failed:\n" + string.Join("\n", errors));
        }

        Debug.Log("Survival scenarios passed.");
    }

    public static List<string> RunAll()
    {
        List<string> errors = new List<string>();
        Run("save_v19_contract", VerifySaveContract, errors);
        Run("stock_categories", VerifyStockCategories, errors);
        Run("work_types", VerifyWorkTypes, errors);
        Run("survival_item_definitions", VerifySurvivalItemDefinitions, errors);
        Run("ability_modules", VerifyAbilityModules, errors);
        Run("room_snapshot_survival_metrics", VerifyRoomSnapshotMetrics, errors);
        Run("physical_meal_authority", VerifyPhysicalMealAuthority, errors);
        Run("physical_freshness_authority", VerifyPhysicalFreshnessAuthority, errors);
        Run("survival_resources_strict_restore", VerifySurvivalResourcesStrictRestore, errors);
        Run("water_forecast_health_attribution", VerifyWaterForecastHealthAttribution, errors);
        Run("survival_typed_work_failures", VerifySurvivalTypedWorkFailures, errors);
        Run("meal_diet_content", VerifyMealDietContent, errors);
        Run("medicine_and_substance_content", VerifyMedicineAndSubstanceContent, errors);
        Run("consumables_save_payload", VerifyConsumablesSavePayload, errors);
        Run("consumables_typed_failures", VerifyConsumablesTypedFailures, errors);
        Run("consumables_physical_exactly_once", VerifyConsumablesPhysicalExactlyOnce, errors);
        Run("packaged_consumable_missing_tare_recovery", VerifyPackagedConsumableMissingTareRecovery, errors);
        Run("stale_meal_need_is_benign_cancellation", VerifyStaleMealNeedClassification, errors);
        Run("meal_four_second_commit_and_spoil_abort", VerifyMealFourSecondCommitAndSpoilAbort, errors);
        Run("tavern_recreational_substance_service", VerifyTavernRecreationalSubstanceService, errors);
        Run("consumables_strict_restore", VerifyConsumablesStrictRestore, errors);
        Run("consumables_persistent_actor_capture_closure",
            VerifyConsumablesPersistentActorCaptureClosure,
            errors);
        return errors;
    }

    public static string RunMealV7PendingOutboxFocused() =>
        VerifyMealFourSecondCommitAndSpoilAbort();

    public static string RunWim035MealQualityFocused() =>
        VerifyMealFourSecondCommitAndSpoilAbort("food:lavish-meat", true)
        + "\n" + VerifyMealFourSecondCommitAndSpoilAbort("food:lavish-vegan", true);

    public static string RunSubstanceV8PendingOutboxFocused() =>
        VerifyConsumablesPhysicalExactlyOnce();

    public static string RunPackagedConsumableTareRecoveryFocused() =>
        VerifyPackagedConsumableMissingTareRecovery();

    public static string RunPersistentActorCaptureClosureFocused() =>
        VerifyConsumablesPersistentActorCaptureClosure();

    public static string RunFacilityFuelDeliveryFocused() =>
        VerifyFacilityFuelDelivery();

    [MenuItem("DungeonStory/Debug/Survival/Run Water Forecast Health Attribution")]
    public static void RunWaterForecastHealthAttributionFromMenu() =>
        Debug.Log(RunWaterForecastHealthAttributionFocused());

    public static string RunWaterForecastHealthAttributionFocused()
    {
        const string reportPath =
            "Artifacts/QA/wim-implementation/wim-7.4.1-water-health-attribution.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        string result = VerifyWaterForecastHealthAttribution();
        File.WriteAllText(reportPath, result + "\n");
        return result;
    }

    private static string VerifyWaterForecastHealthAttribution()
    {
        DungeonSurvivalSaveData forecastOnly = new()
        {
            lastMissingWater = 99,
            consecutiveWaterShortageDays = 30,
            consecutiveFoodShortageDays = 0
        };
        Type evaluatorType = typeof(SurvivalFoodRuntime).Assembly.GetType(
            "SurvivalEnvironmentRiskEvaluator",
            throwOnError: true);
        ConstructorInfo evaluatorConstructor = evaluatorType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            new[]
            {
                typeof(IGridSystemProvider),
                typeof(ICharacterAiWorldRegistry),
                typeof(IWorldThreatModifierQuery)
            },
            modifiers: null)
            ?? throw new MissingMethodException(
                evaluatorType.FullName,
                ".ctor");
        object evaluator = evaluatorConstructor.Invoke(new object[]
        {
            new EmptyGridSystemProvider(),
            CharacterAiEditorTestDependencies.WorldRegistry,
            EmptyWorldThreatModifiers.Instance
        });
        MethodInfo evaluate = evaluatorType.GetMethod(
            "Evaluate",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                evaluatorType.FullName,
                "Evaluate");

        object Evaluate(DungeonSurvivalSaveData state, int rotStacks) =>
            evaluate.Invoke(evaluator, new object[]
            {
                state,
                rotStacks,
                SurvivalWeatherType.Clear
            });
        float Risk(object result, string propertyName) =>
            (float)(result.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(
                    result.GetType().FullName,
                    propertyName))
                .GetValue(result);

        object clean = Evaluate(forecastOnly, 0);
        Require(Mathf.Approximately(Risk(clean, "SanitationRisk"), 0f)
                && Mathf.Approximately(Risk(clean, "DiseaseRisk"), 0f),
            "Stored/Loose water forecast created sanitation or disease risk without an actual character exposure.");
        Require(forecastOnly.lastMissingWater == 99
                && forecastOnly.consecutiveWaterShortageDays == 30,
            "Health projection mutated the retained water planning forecast.");

        object actualSanitation = Evaluate(forecastOnly, 5);
        Require(Mathf.Approximately(
                    Risk(actualSanitation, "SanitationRisk"), 60f)
                && Mathf.Approximately(
                    Risk(actualSanitation, "DiseaseRisk"), 33f),
            "Removing forecast-based sickness also removed the existing physical sanitation exposure path.");

        forecastOnly.consecutiveFoodShortageDays = 1;
        object actualFoodShortage = Evaluate(forecastOnly, 0);
        Require(Mathf.Approximately(
                Risk(actualFoodShortage, "DiseaseRisk"), 7f),
            "Removing forecast-based water sickness also removed the existing food-shortage health path.");

        return "result=PASS\n"
            + "forecast=lastMissingWater:99,consecutiveWaterShortageDays:30,healthRisk:0\n"
            + "physical-sanitation=rotStacks:5,sanitationRisk:60,diseaseRisk:33\n"
            + "food-shortage=days:1,diseaseRisk:7\n"
            + "actual-thirst-owner=CharacterDeprivationRuntime\n"
            + "actual-water-exposure-owner=CharacterWaterConsumedEvent->PopulationHealthRuntime";
    }

    [MenuItem("DungeonStory/Debug/Survival/Run WIM-036 Substance Effects Verification")]
    public static void RunWim036SubstanceEffectsFromMenu() =>
        RunWim036SubstanceEffectsFocused();

    public static string RunWim036SubstanceEffectsFocused()
    {
        const string reportPath =
            "Artifacts/QA/wim-implementation/wim-036-substance-effects.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        List<string> report = new()
        {
            "WIM-036 substance-specific effects focused verification",
            $"utc={DateTime.UtcNow:O}"
        };
        try
        {
            report.Add("[PASS] PHYSICAL_EFFECTS "
                + VerifyWim036SubstanceEffects());
            report.Add("failures=0");
            File.WriteAllLines(reportPath, report);
            Debug.Log(string.Join("\n", report));
            return string.Join("\n", report);
        }
        catch (Exception exception)
        {
            report.Add("[FAIL] " + exception.GetType().Name + ": "
                + exception.Message);
            report.Add("failures=1");
            File.WriteAllLines(reportPath, report);
            throw;
        }
    }

    [MenuItem("DungeonStory/Debug/Survival/Run WIM-028 Toxicity Verification")]
    public static void RunWim028ToxicityFromMenu() =>
        RunWim028ToxicityFocused();

    public static string RunWim028ToxicityFocused()
    {
        const string reportPath =
            "Artifacts/QA/wim-implementation/wim-028-toxicity-antidote.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        List<string> report = new()
        {
            "WIM-028 toxicity / antidote focused verification",
            $"utc={DateTime.UtcNow:O}"
        };
        try
        {
            report.Add("[PASS] OVERDOSE_POLICY " + VerifyWim028OverdosePolicy());
            report.Add("[PASS] TREAT_BRIDGE_RETRY " + VerifyWim028TreatBridgeRetry());
            report.Add("[PASS] LIVE_HEALTH_PANEL " + VerifyWim028LiveHealthPanel());
            report.Add("failures=0");
            File.WriteAllLines(reportPath, report);
            Debug.Log(string.Join("\n", report));
            return string.Join("\n", report);
        }
        catch (Exception exception)
        {
            report.Add("[FAIL] " + exception.GetType().Name + ": " + exception.Message);
            report.Add("failures=1");
            File.WriteAllLines(reportPath, report);
            throw;
        }
    }

    [MenuItem("DungeonStory/Debug/Survival/Run WIM-062 Treatment Delivery Verification")]
    public static void RunWim062TreatmentDeliveryFromMenu() =>
        RunWim062TreatmentDeliveryFocused();

    public static string RunWim062TreatmentDeliveryFocused()
    {
        const string reportPath =
            "Artifacts/QA/wim-implementation/wim-062-treatment-delivery-focused.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        List<string> report = new()
        {
            "WIM-062 treatment delivery / physical commit focused verification",
            $"utc={DateTime.UtcNow:O}",
            "route=controlled work-handler / physical-item fixture; natural-haul=NOT_RUN"
        };
        try
        {
            report.Add("[PASS] TREAT_DELIVERY_RETRY " + VerifyWim028TreatBridgeRetry());
            report.Add("[PASS] SUPPLY_QUERY StockMissing/CapacityUnavailable/AwaitingRequest/Requested/InTransit/Ready/Processing; repeated reads preserve physical/treatment/consumables/session captures");
            report.Add("supply-query-real-NoPath=NOT_RUN; health-ui=NOT_RUN");
            report.Add("failures=0");
            File.WriteAllLines(reportPath, report);
            Debug.Log(string.Join("\n", report));
            return string.Join("\n", report);
        }
        catch (Exception exception)
        {
            report.Add("[FAIL] " + exception.GetType().Name + ": " + exception.Message);
            report.Add("failures=1");
            File.WriteAllLines(reportPath, report);
            throw;
        }
    }

    private static string VerifyWim028LiveHealthPanel()
    {
        Require(Application.isPlaying,
            "WIM028 live Character Summary observation requires Play Mode");
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate?.Container != null);
        CharacterSummaryInfo summary = UnityEngine.Object
            .FindObjectsByType<CharacterSummaryInfo>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault();
        Require(scope?.Container != null && summary != null,
            "live gameplay container or CharacterSummaryInfo is unavailable");

        ICharacterAiWorldRegistry world =
            scope.Container.Resolve<ICharacterAiWorldRegistry>();
        ICharacterConsumablesPersistence persistence =
            scope.Container.Resolve<ICharacterConsumablesPersistence>();
        ICharacterConsumablesQuery query =
            scope.Container.Resolve<ICharacterConsumablesQuery>();
        CharacterActor actor = world.Characters
            .Where(candidate => candidate != null
                && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
                && CharacterPersistentIdentity.TryGet(candidate, out _))
            .OrderBy(candidate => candidate.Identity.PersistentId,
                StringComparer.Ordinal)
            .FirstOrDefault();
        Require(actor != null,
            "live Character Summary toxicity observation has no active registered character");

        DungeonCharacterConsumablesSaveData baseline = persistence.Capture();
        try
        {
            DungeonCharacterConsumablesSaveData projected = persistence.Capture();
            string actorId = CharacterPersistentIdentity.Require(actor).Value;
            projected.toxicityStates.RemoveAll(state => state != null
                && string.Equals(
                    state.characterId,
                    actorId,
                    StringComparison.Ordinal));
            projected.toxicityStates.Add(new CharacterToxicityState
            {
                characterId = actorId,
                toxicity = CharacterToxicityPolicy.MaximumToxicity,
                lastNaturalRecoveryDay = 1
            });
            persistence.PublishRestoreCandidate(
                persistence.BuildRestoreCandidate(projected));

            CharacterToxicityStatus status = query.GetToxicityStatus(actor);
            Require(Mathf.Approximately(
                        status.Toxicity,
                        CharacterToxicityPolicy.MaximumToxicity)
                    && Mathf.Approximately(
                        status.PerformancePenalty,
                        CharacterToxicityPolicy.MaximumPerformancePenalty),
                "live consumables query did not expose toxicity 100 / penalty 20%");
            summary.OnTriggerEvent(new InfoFeedEvent(actor));
            summary.ShowHealthTab();
            TMP_Text healthText = summary.UI?.transform.Find(
                    "CharacterSummaryGeneratedView/Content/HealthContent/HealthContentViewport/HealthSummaryText")
                ?.GetComponent<TMP_Text>();
            string treatmentReason =
                CharacterSummaryHealthStatusTextFormatter.ToxicityTreatment(status);
            string expectedRow = CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Consumables.ToxicityRow",
                status.Toxicity,
                status.PerformancePenalty * 100f,
                treatmentReason);
            Require(summary.UI != null
                    && summary.UI.activeInHierarchy
                    && healthText != null
                    && healthText.gameObject.activeInHierarchy
                    && healthText.text.Contains(expectedRow,
                        StringComparison.Ordinal),
                "live Character Summary health panel did not render the exact toxicity value, penalty contribution, and reason");
            return $"actor={actorId}; toxicity={status.Toxicity:0.#}; "
                + $"penalty={status.PerformancePenalty * 100f:0.#}%; "
                + $"reason={status.TreatmentUnavailableReason}; "
                + $"rendered={expectedRow}";
        }
        finally
        {
            summary.RequestClose();
            persistence.PublishRestoreCandidate(
                persistence.BuildRestoreCandidate(baseline));
        }
    }

    private static string VerifyConsumablesPersistentActorCaptureClosure()
    {
        GameObject actorObject = new("ConsumablesPersistentActorFixture");
        GameObject facilityObject = new("ConsumablesPersistentFacilityFixture");
        CharacterActor actor = null;
        BuildableObject facility = null;
        BuildingSO buildingData = null;
        WorldItemStackRuntime itemRuntime = null;
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        try
        {
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(
                new CharacterId("character:consumables-persistent-fixture"));
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);

            facility = facilityObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            buildingData.id = 99143;
            buildingData.objectName = "Consumables persistent fixture";
            buildingData.width = 1;
            buildingData.height = 1;
            buildingData.category = BuildingCategory.Shop;
            buildingData.Facility = new FacilityData
            {
                roles = FacilityRole.Meal,
                capacity = 1
            };
            facility.Initialization(buildingData, Vector2Int.zero);
            world.RegisterBuilding(facility);

            itemRuntime = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture();
            IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            CharacterConsumablesApplicationPorts ports = new(
                itemCatalog,
                itemRuntime,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance);
            CharacterConsumablesRuntime runtime = new(
                ports,
                ports,
                ports,
                new UnityGameClock(),
                new RandomStreamProvider(711),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);

            DungeonCharacterConsumablesSaveData payload = runtime.Capture();
            payload.pendingMealDeliveries.Add(new CharacterMealDeliveryState
            {
                deliveryId =
                    "consumable-delivery:auto:v1:0000000000000001",
                characterId = actor.Identity.PersistentId,
                buildingInstanceId = facility.RequirePersistentInstanceId().Value,
                itemDefinitionId = "food:preserved-ration",
                requestedAt = 0f,
                retryAfter = 30f
            });
            payload.nextDeliverySequence = 2;
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(payload));
            Require(runtime.Capture().pendingMealDeliveries.Count == 1,
                "fixture could not publish a valid pending meal delivery");

            runtime.ReconcilePersistentActorReferences(Array.Empty<CharacterId>());
            DungeonCharacterConsumablesSaveData reconciled = runtime.Capture();
            Require(reconciled.pendingMealDeliveries.Count == 0
                    && reconciled.dietPolicies.Count == 0
                    && reconciled.mealQualityPolicies.Count == 0
                    && reconciled.substancePolicies.Count == 0
                    && reconciled.substanceStates.Count == 0
                    && reconciled.completedOperations.Count == 0
                    && reconciled.mealFollowupCooldowns.Count == 0
                    && reconciled.activeMealPlans.Count == 0
                    && reconciled.activeSubstanceUsePlans.Count == 0,
                "capture reconciliation retained an actor-owned reference outside characters.world");
            runtime.ValidateRestorePayload(reconciled, requireWorldReferences: true);
            return "pending-delivery=1->0; persistent-reference-closure=exact";
        }
        finally
        {
            if (facility != null)
                world.UnregisterBuilding(facility);
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            itemRuntime?.Dispose();
            if (buildingData != null)
                UnityEngine.Object.DestroyImmediate(buildingData);
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string VerifyStaleMealNeedClassification()
    {
        BuildingMealUseSnapshot noLongerHungry = new(
            false,
            CharacterConsumablesFailureCode.PolicyForbidden.ToString(),
            string.Empty,
            0,
            failureDetail: "owner,not-hungry",
            isNoLongerNeeded: true);
        BuildingMealUseSnapshot followupCooldown = new(
            false,
            CharacterConsumablesFailureCode.PolicyForbidden.ToString(),
            string.Empty,
            0,
            failureDetail: "owner,meal-followup-cooldown",
            isNoLongerNeeded: true);
        BuildingMealUseSnapshot ritualFast = new(
            false,
            CharacterConsumablesFailureCode.PolicyForbidden.ToString(),
            string.Empty,
            0,
            failureDetail: "owner,ritual-fast");
        BuildingMealUseSnapshot physicalFailure = new(
            false,
            CharacterConsumablesFailureCode.PhysicalConsumptionFailed.ToString(),
            string.Empty,
            0,
            failureDetail: "meal-lease-invalid-at-commit");

        Require(noLongerHungry.IsNoLongerNeeded,
            "a satisfied hunger need was still classified as an execution failure");
        Require(followupCooldown.IsNoLongerNeeded,
            "an already-satisfied snack follow-up was still classified as an execution failure");
        Require(!ritualFast.IsNoLongerNeeded,
            "an authored ritual policy rejection was hidden as a benign cancellation");
        Require(!physicalFailure.IsNoLongerNeeded,
            "a physical meal commit failure was hidden as a benign cancellation");
        return "not-hungry/followup=benign; ritual/physical=typed-failure";
    }

    private static string VerifyMealFourSecondCommitAndSpoilAbort() =>
        VerifyMealFourSecondCommitAndSpoilAbort("food:preserved-ration", false);

    private static string VerifyMealFourSecondCommitAndSpoilAbort(
        string FoodId, bool verifyQualityPolicy)
    {
        GameObject actorObject = new("MealActionActor");
        GameObject facilityObject = new("MealActionFacility");
        BuildingSO buildingData = null;
        WorldItemStackRuntime items = null;
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        CharacterActor actor = null;
        BuildableObject facility = null;
        try
        {
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            CharacterSO characterData = CharacterAiEditorTestDependencies
                .ContentDefinitions.GetAll<CharacterSO>()
                .Where(value => value != null
                    && value.characterType == CharacterType.NPC
                    && value.role != CharacterRole.Owner
                    && value.DefinitionId.IsValid)
                .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored NPC archetype is available for the meal V7 fixture.");
            actor.data = characterData;
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId("character:meal-action-test");
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (actor.IsUnpublishedComposition)
                actor.PublishComposition();
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);

            facility = facilityObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            buildingData.id = 99142;
            buildingData.objectName = "Meal action fixture";
            buildingData.width = 1;
            buildingData.height = 1;
            buildingData.category = BuildingCategory.Shop;
            buildingData.Facility = new FacilityData
            {
                roles = FacilityRole.Meal,
                capacity = 1
            };
            facility.Initialization(buildingData, Vector2Int.zero);
            world.RegisterBuilding(facility);

            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out _,
                out _,
                out ItemQuantityReservationService reservations,
                out IReservedItemTransferService reservedTransfers,
                out IReservedPhysicalItemBatchDispositionService reservedBatch,
                out IPhysicalItemBatchDispositionService batch);
            FailFirstAcknowledgeBatchDisposition failFirstAcknowledge = new(batch);
            IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            GameEventBus eventBus = new();
            CharacterConsumablesApplicationPorts ports = new(
                itemCatalog,
                items,
                world,
                eventBus,
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: failFirstAcknowledge);
            MutableConsumablesClock clock = new();
            CharacterConsumablesRuntime runtime = new(
                ports,
                ports,
                ports,
                clock,
                new RandomStreamProvider(4204),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            string destination = CharacterConsumablesRuntime.GetMealDestinationId(
                facility.RequirePersistentInstanceId(),
                new ConsumableItemDefinitionId(FoodId));

            Require(items.SpawnItemAt(
                    FoodId,
                    1,
                    Vector2Int.zero,
                    WorldItemStackState.FacilityBuffer,
                    destination,
                    out int firstSpawned)
                && firstSpawned == 1,
                "meal action fixture failed to spawn first serving");
            WorldItemStackSnapshot firstStack = items.GetAllStacks().Single(stack =>
                stack.ItemId == FoodId);
            actor.stats[CharacterCondition.HUNGER] = 50f;
            if (verifyQualityPolicy)
            {
                CharacterConsumablesCompatibilityAdapter policy = new(runtime);
                Require(policy.GetMealQualityLimit(actor) == CharacterMealQualityLimit.Inherit,
                    "new actor meal cap did not preserve Inherit default");
                Require(!policy.HasMealAvailable(actor, facility, out _),
                    "default Fine cap accepted Lavish food");
                policy.SetMealQualityLimit(actor, CharacterMealQualityLimit.Lavish);
                Require(policy.GetMealQualityLimit(actor) == CharacterMealQualityLimit.Lavish
                        && policy.HasMealAvailable(actor, facility, out _),
                    "application adapter did not open existing Lavish candidate policy");
                policy.SetMealQualityLimit(actor, CharacterMealQualityLimit.Fine);
                Require(!policy.HasMealAvailable(actor, facility, out _)
                        && items.GetAllStacks().Single(stack => stack.StackId == firstStack.StackId).Quantity == 1,
                    "lowered meal cap did not block Lavish or consumed stock during policy change");
                policy.SetMealQualityLimit(actor, CharacterMealQualityLimit.Lavish);
            }
            actor.PathSearchBroker?.BeginFrame(
                0,
                enforceBudget: true,
                searchTimeBudgetMilliseconds: 0d);
            Require(runtime.HasMealAvailable(
                    CharacterPersistentIdentity.Require(actor),
                    facility.RequirePersistentInstanceId(),
                    out CharacterConsumablesFailure bufferedAvailability)
                && !bufferedAvailability.IsFailure,
                "physical buffer meal availability incorrectly depended on transient path-search budget");
            actor.PathSearchBroker?.BeginFrame(int.MaxValue, enforceBudget: false);
            ConsumeMealCommand firstCommand = new(
                new ConsumableOperationId("consumable-operation:meal-action-success"),
                CharacterPersistentIdentity.Require(actor),
                facility.RequirePersistentInstanceId(),
                new ItemStackId(firstStack.StackId));
            Require(!runtime.TryConsumeMeal(firstCommand, out CharacterConsumablesMealResult pending)
                && pending.FailureCode == CharacterConsumablesFailureCode.DeliveryPending
                && items.GetAllStacks().Single(stack => stack.StackId == firstStack.StackId).ReservedQuantity == 1,
                "meal was not held as a four-second reserved action");
            int quantityBeforeStaleAttempt = items.GetAllStacks()
                .Single(stack => stack.StackId == firstStack.StackId)
                .Quantity;
            actor.stats[CharacterCondition.HUNGER] = 80f;
            Require(!runtime.TryConsumeMeal(
                    CharacterPersistentIdentity.Require(actor),
                    facility.RequirePersistentInstanceId(),
                    out CharacterConsumablesMealResult staleNeed)
                && staleNeed.FailureCode
                    == CharacterConsumablesFailureCode.PolicyForbidden
                && staleNeed.Parameters.Contains("not-hungry")
                && items.GetAllStacks()
                    .Single(stack => stack.StackId == firstStack.StackId)
                    .Quantity == quantityBeforeStaleAttempt,
                "a stale already-satisfied meal decision consumed stock or lost its typed cancellation reason");
            actor.stats[CharacterCondition.HUNGER] = 50f;
            DungeonCharacterConsumablesSaveData activeSave = runtime.Capture();
            Require(activeSave.activeMealPlans.Count == 1
                && activeSave.activeMealPlans[0].planId == firstCommand.OperationId.Value,
                "active MealPlan was not captured without a runtime lease ID");
            CharacterConsumablesRuntime restoredRuntime = new(
                ports,
                ports,
                ports,
                clock,
                new RandomStreamProvider(4204),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            restoredRuntime.PublishRestoreCandidate(
                restoredRuntime.BuildRestoreCandidate(activeSave));
            runtime = restoredRuntime;
            if (verifyQualityPolicy)
                Require(new CharacterConsumablesCompatibilityAdapter(runtime)
                        .GetMealQualityLimit(actor) == CharacterMealQualityLimit.Lavish,
                    "Lavish policy did not survive active-meal save/restore");
            clock.Advance(3.9f);
            runtime.Tick();
            Require(items.GetAllStacks().Any(stack => stack.StackId == firstStack.StackId),
                "meal committed before four seconds elapsed");
            clock.Advance(0.2f);
            runtime.Tick();
            DungeonCharacterConsumablesSaveData pendingAckSave = runtime.Capture();
            bool firstStackRemoved = !items.GetAllStacks().Any(
                stack => stack.StackId == firstStack.StackId);
            bool pendingResultAvailable = runtime.TryGetMealOperationResult(
                firstCommand.OperationId,
                out CharacterConsumablesMealResult pendingOperationResult);
            Require(firstStackRemoved
                    && pendingAckSave.completedOperations.Count == 1
                    && pendingAckSave.activeMealPlans.Count == 1
                    && pendingAckSave.activeMealPlans[0].phase
                        == CharacterMealPlanPhase.EffectsPublished,
                "meal did not retain its V7 effects-published outbox after the injected ack failure; "
                + $"stackRemoved={firstStackRemoved}; "
                + $"activePlans={pendingAckSave.activeMealPlans.Count}; "
                + $"completed={pendingAckSave.completedOperations.Count}; "
                + $"actorCanRun={actor.CanRunAi}; "
                + $"resultAvailable={pendingResultAvailable}; "
                + $"failure={pendingOperationResult.FailureCode}; "
                + $"parameters=[{string.Join(",", pendingOperationResult.Parameters)}]");
            DungeonCharacterConsumablesSaveData mismatchedReceipt =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    JsonUtility.ToJson(pendingAckSave));
            mismatchedReceipt.activeMealPlans[0].physicalCommitInputMassGrams++;
            RequireThrows<InvalidOperationException>(
                () => runtime.BuildRestoreCandidate(mismatchedReceipt),
                "meal V7 restore accepted mismatched pending receipt mass");
            Require(batch.TryGetPending(firstCommand.OperationId.Value, out _),
                "failed meal V7 restore validation mutated the pending receipt");
            float hungerAfterEffects = actor.stats[CharacterCondition.HUNGER];
            CharacterConsumablesRuntime ackRestoredRuntime = new(
                ports,
                ports,
                ports,
                clock,
                new RandomStreamProvider(4204),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            ackRestoredRuntime.PublishRestoreCandidate(
                ackRestoredRuntime.BuildRestoreCandidate(pendingAckSave));
            runtime = ackRestoredRuntime;
            runtime.Tick();
            DungeonCharacterConsumablesSaveData completedSave = runtime.Capture();
            bool completedResultAvailable = runtime.TryGetMealOperationResult(
                firstCommand.OperationId,
                out CharacterConsumablesMealResult completedOperationResult);
            Require(completedSave.activeMealPlans.Count == 0
                    && completedSave.completedOperations.Count == 1
                    && completedResultAvailable
                    && completedOperationResult.Success
                    && Mathf.Approximately(
                        actor.stats[CharacterCondition.HUNGER],
                        hungerAfterEffects)
                    && !batch.TryGetPending(firstCommand.OperationId.Value, out _),
                "meal pending receipt did not acknowledge exactly once after restore, or effects replayed");
            DungeonCharacterConsumablesSaveData missingPending =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    JsonUtility.ToJson(pendingAckSave));
            missingPending.activeMealPlans[0].phase =
                CharacterMealPlanPhase.ItemCommitted;
            missingPending.completedOperations.Clear();
            RequireThrows<InvalidOperationException>(
                () => runtime.BuildRestoreCandidate(missingPending),
                "meal V7 restore accepted ItemCommitted without its physical pending receipt");

            Require(items.SpawnItemAt(
                    FoodId,
                    1,
                    Vector2Int.zero,
                    WorldItemStackState.FacilityBuffer,
                    destination,
                    out int secondSpawned)
                && secondSpawned == 1,
                "meal action fixture failed to spawn spoil serving");
            WorldItemStackSnapshot secondStack = items.GetAllStacks().Single(stack =>
                stack.ItemId == FoodId);
            ConsumeMealCommand spoilCommand = new(
                new ConsumableOperationId("consumable-operation:meal-action-spoil"),
                CharacterPersistentIdentity.Require(actor),
                facility.RequirePersistentInstanceId(),
                new ItemStackId(secondStack.StackId));
            Require(!runtime.TryConsumeMeal(spoilCommand, out CharacterConsumablesMealResult spoilPending)
                && spoilPending.FailureCode == CharacterConsumablesFailureCode.DeliveryPending,
                "spoil action did not begin");
            ItemInstanceComponentSaveData freshness = secondStack.Components
                .FirstOrDefault(component =>
                component != null
                && component.componentTypeId == ItemInstanceComponentIds.Freshness)
                ?.Clone()
                ?? new ItemInstanceComponentSaveData
                {
                    componentTypeId = ItemInstanceComponentIds.Freshness,
                    schemaVersion = 2,
                    affectsStacking = true,
                    values = new List<ItemStateValueSaveData>()
                };
            freshness.values ??= new List<ItemStateValueSaveData>();
            ItemStateValueSaveData remaining = freshness.values.FirstOrDefault(value =>
                value != null && value.key == "remaining-seconds");
            if (remaining == null)
            {
                remaining = new ItemStateValueSaveData { key = "remaining-seconds" };
                freshness.values.Add(remaining);
            }
            remaining.kind = ItemStateValueKind.Decimal;
            remaining.decimalValue = 0d;
            Require(items.TrySetInstanceComponent(secondStack.StackId, freshness),
                "meal action fixture failed to apply spoiled freshness state");
            clock.Advance(4.1f);
            runtime.Tick();
            WorldItemStackSnapshot spoiled = items.GetAllStacks().Single(stack =>
                stack.StackId == secondStack.StackId);
            Require(spoiled.Quantity == 1
                && spoiled.ReservedQuantity == 0
                && runtime.Capture().completedOperations.Count == 1,
                "spoiled meal was consumed, leaked its lease, or recorded completion");
            Require(!runtime.TryGetMealOperationResult(
                    spoilCommand.OperationId,
                    out CharacterConsumablesMealResult spoiledResult)
                && spoiledResult.FailureCode
                    == CharacterConsumablesFailureCode.ItemNotConsumable
                && spoiledResult.Parameters.Contains(
                    "meal-spoiled-before-commit"),
                "spoiled meal abort did not retain its typed diagnostic reason: "
                + $"code={spoiledResult.FailureCode}; "
                + $"parameters={string.Join(",", spoiledResult.Parameters)}");

            if (verifyQualityPolicy)
            {
                Require(items.SpawnItemAt(FoodId, 1, Vector2Int.zero,
                        WorldItemStackState.FacilityBuffer, destination, out int spawnedPolicy)
                        && spawnedPolicy == 1, "could not create policy-change serving");
                WorldItemStackSnapshot policyStack = items.GetAllStacks().Single(value =>
                    value.ItemId == FoodId && value.State == WorldItemStackState.FacilityBuffer
                    && value.StackId != secondStack.StackId);
                ConsumeMealCommand policyCommand = new(
                    new ConsumableOperationId("consumable-operation:meal-policy-change"),
                    CharacterPersistentIdentity.Require(actor), facility.RequirePersistentInstanceId(),
                    new ItemStackId(policyStack.StackId));
                Require(!runtime.TryConsumeMeal(policyCommand, out CharacterConsumablesMealResult policyPending)
                        && policyPending.FailureCode == CharacterConsumablesFailureCode.DeliveryPending,
                    "policy-change serving did not start");
                new CharacterConsumablesCompatibilityAdapter(runtime)
                    .SetMealQualityLimit(actor, CharacterMealQualityLimit.Fine);
                clock.Advance(4.1f);
                runtime.Tick();
                WorldItemStackSnapshot retained = items.GetAllStacks().Single(value => value.StackId == policyStack.StackId);
                Require(retained.Quantity == 1 && retained.ReservedQuantity == 0
                        && runtime.Capture().completedOperations.Count == 1
                        && !runtime.TryGetMealOperationResult(policyCommand.OperationId, out CharacterConsumablesMealResult denied)
                        && denied.FailureCode == CharacterConsumablesFailureCode.PolicyForbidden
                        && denied.Parameters.Contains("meal-quality-limit-at-commit"),
                    "policy change during meal consumed stock or leaked its lease");
            }

            FailFirstAcknowledgeBatchDisposition fieldAck = new(batch);
            CharacterConsumablesApplicationPorts fieldPorts = new(
                itemCatalog,
                items,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: fieldAck);
            CharacterConsumablesRuntime fieldRuntime = new(
                fieldPorts,
                fieldPorts,
                fieldPorts,
                clock,
                new RandomStreamProvider(4205),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            actor.stats[CharacterCondition.HUNGER] = 50f;
            Require(items.SpawnItemAt(
                    FoodId,
                    1,
                    new Vector2Int(2, 0),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int fieldSpawned)
                && fieldSpawned == 1,
                "field-meal V7 fixture failed to spawn its physical serving");
            WorldItemStackSnapshot fieldStack = items.GetAllStacks()
                .Single(value => value.ItemId == FoodId
                    && value.State == WorldItemStackState.Loose);
            int fieldQuantityBefore = items.GetAllStacks()
                .Where(value => value.ItemId == FoodId)
                .Sum(value => value.Quantity);
            if (verifyQualityPolicy)
            {
                CharacterConsumablesCompatibilityAdapter policy = new(fieldRuntime);
                Require(!policy.TryConsumeFieldMeal(actor, new ItemStackId(fieldStack.StackId), out _)
                        && items.GetAllStacks().Single(value => value.StackId == fieldStack.StackId).Quantity == 1,
                    "default field meal cap consumed Lavish food");
                policy.SetMealQualityLimit(actor, CharacterMealQualityLimit.Lavish);
            }
            Require(fieldRuntime.TryConsumeFieldMeal(
                    CharacterPersistentIdentity.Require(actor),
                    new ItemStackId(fieldStack.StackId),
                    out CharacterConsumablesMealResult fieldResult)
                && fieldResult.Success,
                "field meal did not publish its result after the pending Sink commit");
            DungeonCharacterConsumablesSaveData fieldPending = fieldRuntime.Capture();
            Require(fieldPending.activeMealPlans.Count == 1
                    && fieldPending.activeMealPlans[0].phase
                        == CharacterMealPlanPhase.EffectsPublished
                    && fieldPending.completedOperations.Count == 1
                    && batch.TryGetPending(
                        fieldPending.activeMealPlans[0].planId,
                        out _)
                    && items.GetAllStacks()
                        .Where(value => value.ItemId == FoodId)
                        .Sum(value => value.Quantity) == fieldQuantityBefore - 1,
                "field meal did not preserve its exact pending receipt after ack failure");
            float hungerAfterFieldMeal = actor.stats[CharacterCondition.HUNGER];
            CharacterConsumablesRuntime restoredFieldRuntime = new(
                fieldPorts,
                fieldPorts,
                fieldPorts,
                clock,
                new RandomStreamProvider(4205),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            restoredFieldRuntime.PublishRestoreCandidate(
                restoredFieldRuntime.BuildRestoreCandidate(fieldPending));
            restoredFieldRuntime.Tick();
            DungeonCharacterConsumablesSaveData fieldCompleted =
                restoredFieldRuntime.Capture();
            Require(fieldCompleted.activeMealPlans.Count == 0
                    && fieldCompleted.completedOperations.Count == 1
                    && Mathf.Approximately(
                        actor.stats[CharacterCondition.HUNGER],
                        hungerAfterFieldMeal)
                    && !batch.TryGetPending(
                        fieldPending.activeMealPlans[0].planId,
                        out _),
                "field meal restore replayed effects or failed to acknowledge its receipt");
            return $"item={FoodId}; qualityPolicy={verifyQualityPolicy}; facility=pending/restore-exact; field=pending/restore-exact; spoiled=abort; leaseReleased=True";
        }
        finally
        {
            items?.Dispose();
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            if (facility != null)
                world.UnregisterBuilding(facility);
            UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(facilityObject);
            if (buildingData != null)
                UnityEngine.Object.DestroyImmediate(buildingData);
        }
    }

    private static void Run(string name, Func<string> scenario, List<string> errors)
    {
        try
        {
            Debug.Log($"[Survival] {name}: {scenario()}");
        }
        catch (Exception ex)
        {
            errors.Add($"{name}: {ex.Message}");
        }
    }

    private static string VerifySaveContract()
    {
        Require(DungeonGameSaveData.CurrentVersion == 24, "game save version is not V24");
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonSaveSectionPayload.Write(
            save,
            SurvivalResourcesSaveSection.Id,
            DungeonSurvivalSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            new DungeonSurvivalSaveData());
        DungeonSurvivalSaveData survival =
            DungeonSaveSectionPayload.ReadOrNew<DungeonSurvivalSaveData>(
                save,
                SurvivalResourcesSaveSection.Id);
        Require(save.version == DungeonGameSaveData.CurrentVersion, "new save did not default to V19");
        Require(survival.version == DungeonSurvivalSaveData.CurrentVersion, "survival save version mismatch");
        return $"game={save.version}; survival={survival.version}";
    }

    private static string VerifyStockCategories()
    {
        IStockCategoryDefinitionCatalog catalog = CharacterAiEditorTestDependencies.AuthoredGameplay;
        Require(catalog.TryGet(StockCategory.Water, out StockCategoryDefinition water)
            && water.DisplayName == "물", "water stock category missing");
        Require(catalog.TryGet(StockCategory.Medicine, out _), "medicine stock category missing");
        Require(catalog.TryGet(StockCategory.Fuel, out _), "fuel stock category missing");
        Require(StockCategoryPersistenceId.TryParse("stock:water", out StockCategory parsed)
            && parsed == StockCategory.Water, "water persistence id did not parse");
        return string.Join(", ", catalog.All.Select(definition => definition.Id));
    }

    private static string VerifyWorkTypes()
    {
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.DrawWater, out WorkTypeDefinition water)
            && water.DisplayName == "급수", "draw water work type missing");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Cook, out _), "cook work type missing");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Treat, out _), "treat work type missing");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Refuel, out _), "refuel work type missing");
        return $"tasks={WorkTypeCatalog.All.Count}";
    }

    private static string VerifySurvivalItemDefinitions()
    {
        IItemDefinitionCatalog catalog = new ResourceItemDefinitionCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        ItemDefinitionSO cooked = catalog.All.FirstOrDefault(definition => definition != null
            && definition.StockCategory == StockCategory.Food
            && definition.TryGetFeature(out FoodItemFeature food)
            && !food.preserved);
        ItemDefinitionSO preserved = catalog.All.FirstOrDefault(definition => definition != null
            && definition.StockCategory == StockCategory.Food
            && definition.TryGetFeature(out FoodItemFeature food)
            && food.preserved);
        Require(cooked != null, "authored cooked meal definition missing");
        Require(preserved != null, "authored preserved food definition missing");
        SurvivalFoodOverview overview = new SurvivalFoodOverview(
            3,
            4,
            2,
            1,
            3,
            2,
            3,
            1,
            2,
            5,
            1,
            1,
            SurvivalWeatherType.Storm,
            12f,
            140f,
            -5f,
            200f,
            2,
            1);
        Require(Mathf.Approximately(overview.SanitationRisk, 100f), "sanitation risk did not clamp");
        Require(Mathf.Approximately(overview.DiseaseRisk, 0f), "disease risk did not clamp");
        Require(Mathf.Approximately(overview.ExteriorNightDanger, 100f), "night danger did not clamp");
        return cooked.DisplayName;
    }

    private static string VerifyAbilityModules()
    {
        Require(typeof(BuildingWaterSourceAbility).IsSerializable, "water source ability is not serializable");
        Require(typeof(BuildingCookingAbility).IsSerializable, "cooking ability is not serializable");
        Require(typeof(BuildingMedicalAbility).IsSerializable, "medical ability is not serializable");
        Require(typeof(BuildingFuelConsumerAbility).IsSerializable, "fuel consumer ability is not serializable");
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            building.AbilityModules.Add(new BuildingWaterSourceAbility());
            building.AbilityModules.Add(new BuildingFuelConsumerAbility());
            FacilityWorkType workTypes = FacilityWorkType.None;
            if (building.GetAbility<BuildingWaterSourceAbility>() != null)
            {
                workTypes |= FacilityWorkType.DrawWater;
            }

            if (building.GetAbility<BuildingFuelConsumerAbility>() != null)
            {
                workTypes |= FacilityWorkType.Refuel;
            }

            Require((workTypes & FacilityWorkType.DrawWater) != 0, "water ability did not expose DrawWater");
            Require((workTypes & FacilityWorkType.Refuel) != 0, "fuel ability did not expose Refuel");
            return workTypes.ToString();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static string VerifyRoomSnapshotMetrics()
    {
        RoomEnvironmentSnapshot snapshot = new RoomEnvironmentSnapshot(
            null,
            null,
            RoomEnvironmentStatus.Usable,
            Array.Empty<BuildableObject>(),
            Array.Empty<RoomRoleContribution>(),
            FacilityRole.None,
            false,
            0,
            0f,
            0f,
            0,
            0,
            50f,
            50f,
            50f,
            50f,
            150f,
            -20f,
            65f,
            80f);
        Require(Mathf.Approximately(snapshot.Shelter, 100f), "shelter did not clamp high");
        Require(Mathf.Approximately(snapshot.Temperature, 0f), "temperature did not clamp low");
        Require(Mathf.Approximately(snapshot.Ventilation, 65f), "ventilation changed unexpectedly");
        Require(Mathf.Approximately(snapshot.Lighting, 80f), "lighting changed unexpectedly");
        return $"shelter={snapshot.Shelter}; temp={snapshot.Temperature}";
    }

    private static string VerifyPhysicalMealAuthority()
    {
        GameEventBus events = new GameEventBus();
        IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        SurvivalFoodRuntime runtime = new SurvivalFoodRuntime(
            new SurvivalFoodRuntimeDependencies(
                new EmptyGridSystemProvider(),
                new EditorWarehouseStockRuntime(),
                itemCatalog,
                new EmptyStockQuery(),
                FixedClimateQuery.Instance),
            new EmptyWildlifeSpeciesCatalog(),
            events,
            CharacterAiEditorTestDependencies.WorldRegistry,
            new FixedGameClock(),
            EmptyWorldThreatModifiers.Instance,
            EmptySurvivalServiceSessions.Instance,
            aggregateRootStore: new DungeonRuntimeAggregateRootStore());
        GameObject actorObject = null;
        GameObject facilityObject = null;
        BuildingSO buildingData = null;
        IDisposable mealSubscription = null;

        try
        {
            runtime.Initialize();
            int publishedMeals = 0;
            int publishedAmount = 0;
            mealSubscription = events.Subscribe<CharacterMealConsumedEvent>(gameEvent =>
            {
                publishedMeals++;
                publishedAmount += gameEvent.Amount;
            });

            actorObject = new GameObject("SurvivalMealWorker_Test");
            actorObject.AddComponent<AbilityWork>();
            CharacterSO characterData = CharacterAiEditorTestDependencies
                .ContentDefinitions.GetAll<CharacterSO>()
                .Where(value => value != null
                    && value.characterType == CharacterType.NPC
                    && value.DefinitionId.IsValid
                    && value.species != null
                    && value.species.DefinitionId.IsValid)
                .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored NPC archetype is available for the meal fixture.");
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.data = characterData;
            actor.characterType = CharacterType.NPC;
            actor.RefreshAbilityCache();
            actor.Identity.SetPersistentId("character:survival:meal:test");

            facilityObject = new GameObject("SurvivalMealFacility_Test");
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            buildingData.objectName = "검증 식당";
            buildingData.width = 1;
            buildingData.height = 1;
            buildingData.category = BuildingCategory.Shop;
            buildingData.Facility = new FacilityData
            {
                roles = FacilityRole.Meal,
                capacity = 1
            };
            facility.Initialization(buildingData, Vector2Int.zero);

            events.Publish(new OperatingDayStartedEvent(1));
            Require(
                runtime.GetRecentMeals().Count == 0,
                "day transition created an abstract meal");

            events.Publish(new FacilityStockConsumedEvent(
                actor,
                facility,
                StockCategory.Food,
                7));
            IReadOnlyList<CharacterMealLedgerSaveData> meals =
                runtime.GetRecentMeals();
            Require(meals.Count == 1, "completed meal was not recorded exactly once");
            Require(
                meals[0].amount == 1
                && runtime.GetMealsConsumed(actor.Identity.PersistentId, 1) == 1,
                "one meal did not resolve to one food consumption record");
            Require(
                publishedMeals == 1 && publishedAmount == 1,
                "meal event did not publish one physical serving");

            events.Publish(new OperatingDayStartedEvent(2));
            Require(
                runtime.GetRecentMeals().Count == 1
                && runtime.GetMealsConsumed(actor.Identity.PersistentId, 2) == 0,
                "day transition duplicated or consumed another meal");
            return $"ledger={meals.Count}; published={publishedMeals}; day2=0";
        }
        finally
        {
            mealSubscription?.Dispose();
            runtime.Dispose();
            if (actorObject != null)
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
            }
            if (facilityObject != null)
            {
                UnityEngine.Object.DestroyImmediate(facilityObject);
            }
            if (buildingData != null)
            {
                UnityEngine.Object.DestroyImmediate(buildingData);
            }
        }
    }

    private static string VerifyPhysicalFreshnessAuthority()
    {
        Require(
            typeof(DungeonSurvivalSaveData).GetField("spoilage") == null,
            "survival save still owns a spoilage side table");
        Require(
            typeof(DungeonSurvivalSaveData).Assembly
                .GetType("SurvivalFood" + "SpoilageSaveData") == null,
            "legacy survival spoilage DTO still exists");
        Type spoilageRuntime = typeof(SurvivalFoodRuntime).Assembly
            .GetType("SurvivalFoodSpoilageRuntime", throwOnError: true);
        int componentSchema = Convert.ToInt32(
            spoilageRuntime.GetField(
                    "FreshnessSchemaVersion",
                    System.Reflection.BindingFlags.Static
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)
                ?.GetRawConstantValue());
        Require(componentSchema == 2, "physical freshness component schema is not V2");

        IWorldItemStackRuntime physicalItems = new EditorWarehouseStockRuntime();
        IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        SurvivalFoodRuntime runtime = new SurvivalFoodRuntime(
            new SurvivalFoodRuntimeDependencies(
                new EmptyGridSystemProvider(),
                physicalItems,
                itemCatalog,
                new EmptyStockQuery(),
                FixedClimateQuery.Instance),
            new EmptyWildlifeSpeciesCatalog(),
            new GameEventBus(),
            CharacterAiEditorTestDependencies.WorldRegistry,
            new FixedGameClock(),
            EmptyWorldThreatModifiers.Instance,
            EmptySurvivalServiceSessions.Instance,
            aggregateRootStore: new DungeonRuntimeAggregateRootStore());
        int itemVersionBefore = physicalItems.ItemStackVersion;
        DungeonSurvivalSaveData captured = runtime.Capture();
        int itemVersionAfter = physicalItems.ItemStackVersion;
        string payload = JsonUtility.ToJson(captured);
        Require(
            itemVersionBefore == itemVersionAfter,
            "capturing survival state mutated physical item state");
        Require(
            payload.IndexOf("spoilage", StringComparison.OrdinalIgnoreCase) < 0,
            "survival payload still serialized spoilage state");
        return $"save=v{captured.version}; freshness-component=v{componentSchema}";
    }

    private static string VerifySurvivalResourcesStrictRestore()
    {
        IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        DungeonRuntimeAggregateRootStore root = new DungeonRuntimeAggregateRootStore();
        SurvivalFoodRuntime runtime = new SurvivalFoodRuntime(
            new SurvivalFoodRuntimeDependencies(
                new EmptyGridSystemProvider(),
                new EditorWarehouseStockRuntime(),
                itemCatalog,
                new EmptyStockQuery(),
                FixedClimateQuery.Instance),
            new EmptyWildlifeSpeciesCatalog(),
            new GameEventBus(),
            CharacterAiEditorTestDependencies.WorldRegistry,
            new FixedGameClock(),
            EmptyWorldThreatModifiers.Instance,
            EmptySurvivalServiceSessions.Instance,
            aggregateRootStore: root);
        SurvivalResourcesSaveSection section =
            new SurvivalResourcesSaveSection(runtime);
        Require(section is IDungeonRollbackFreeSaveSection
                && section.SectionVersion == DungeonSurvivalSaveData.CurrentVersion,
            "survival resources section is not rollback-free exact V5");

        DungeonSurvivalSaveData valid = runtime.Capture();
        valid.lastProcessedDay = 1;
        valid.mealLedger.Add(new CharacterMealLedgerSaveData
        {
            mealId = "meal:1:character:survival-restore:513",
            characterId = "character:survival-restore",
            facilityId = "building:survival-kitchen",
            day = 1,
            amount = 1
        });
        valid.lastConsumedFood = 1;
        string validJson = JsonUtility.ToJson(valid);
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        section.Restore(validJson, DungeonSurvivalSaveData.CurrentVersion, validReport);
        Require(validReport.Success
                && JsonUtility.ToJson(runtime.Capture()) == validJson
                && runtime.Capture().mealLedger.Single().mealId.EndsWith(
                    ":513",
                    StringComparison.Ordinal),
            "valid survival resources payload did not restore canonically with its persisted sequence");

        string before = JsonUtility.ToJson(runtime.Capture());
        DungeonSurvivalSaveData invalid = JsonUtility.FromJson<DungeonSurvivalSaveData>(before);
        invalid.version = DungeonSurvivalSaveData.CurrentVersion - 1;
        invalid.health = null;
        invalid.lastMissingWater = 4;
        DungeonGameRestoreReport invalidReport = new DungeonGameRestoreReport();
        bool invalidStageRejected = false;
        try
        {
            section.StageRestore(
                JsonUtility.ToJson(invalid),
                DungeonSurvivalSaveData.CurrentVersion,
                invalidReport);
        }
        catch (InvalidOperationException)
        {
            invalidStageRejected = true;
        }
        Require(invalidStageRejected,
            "invalid survival resources candidate was accepted");
        Require(JsonUtility.ToJson(runtime.Capture()) == before
                && root.PublishedRestoreRevision == 0,
            "invalid survival resources candidate mutated the live aggregate");

        bool directRestoreRejected = false;
        try
        {
            runtime.BuildRestoreCandidate(invalid);
        }
        catch (InvalidOperationException)
        {
            directRestoreRejected = true;
        }
        Require(directRestoreRejected
                && JsonUtility.ToJson(runtime.Capture()) == before,
            "direct runtime restore bypassed survival resources validation");
        return "v5=exact; roundtrip=canonical; invalid=no-mutation; sequence=513";
    }

    private static string VerifySurvivalTypedWorkFailures()
    {
        IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        SurvivalFoodRuntimeDependencies dependencies =
            new SurvivalFoodRuntimeDependencies(
                new EmptyGridSystemProvider(),
                new EditorWarehouseStockRuntime(),
                itemCatalog,
                new EmptyStockQuery(),
                FixedClimateQuery.Instance);
        ICharacterAiWorldRegistry world =
            CharacterAiEditorTestDependencies.WorldRegistry;
        IGameClock clock = new FixedGameClock();
        IWorldThreatModifierQuery threats =
            EmptyWorldThreatModifiers.Instance;
        ISurvivalServiceSessionCapability sessions =
            EmptySurvivalServiceSessions.Instance;

        SurvivalFoodRuntime runtime = new SurvivalFoodRuntime(
            dependencies,
            new EmptyWildlifeSpeciesCatalog(),
            new GameEventBus(),
            world,
            clock,
            threats,
            sessions,
            new DungeonRuntimeAggregateRootStore());
        Require(!runtime.TryApplySurvivalWork(
                    actor: null,
                    building: null,
                    BuiltInWorkTypeIds.Cook,
                    out int amount,
                    out DomainFailure failure)
                && amount == 0
                && failure.Code == FailureCode.SurvivalTargetFacilityMissing
                && failure.Parameters.Length == 0,
            "missing survival target did not return a stable typed failure");

        RequireThrows<ArgumentNullException>(() => new SurvivalFoodRuntime(
                dependencies,
                new EmptyWildlifeSpeciesCatalog(),
                new GameEventBus(),
                worldRegistry: null,
                clock,
                threats,
                sessions,
                new DungeonRuntimeAggregateRootStore()),
            "survival runtime accepted a missing world authority");
        RequireThrows<ArgumentNullException>(() => new SurvivalFoodRuntime(
                dependencies,
                new EmptyWildlifeSpeciesCatalog(),
                new GameEventBus(),
                world,
                gameClock: null,
                threats,
                sessions,
                new DungeonRuntimeAggregateRootStore()),
            "survival runtime accepted a missing clock authority");
        RequireThrows<ArgumentNullException>(() => new SurvivalFoodRuntime(
                dependencies,
                new EmptyWildlifeSpeciesCatalog(),
                new GameEventBus(),
                world,
                clock,
                worldThreatModifiers: null,
                sessions,
                new DungeonRuntimeAggregateRootStore()),
            "survival runtime accepted a missing threat authority");
        RequireThrows<ArgumentNullException>(() => new SurvivalFoodRuntime(
                dependencies,
                new EmptyWildlifeSpeciesCatalog(),
                new GameEventBus(),
                world,
                clock,
                threats,
                serviceSessionRuntime: null,
                new DungeonRuntimeAggregateRootStore()),
            "survival runtime accepted a missing service-session capability");

        return "failure=SurvivalTargetFacilityMissing; required-di=4/4";
    }

    private static string VerifyMealDietContent()
    {
        IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        ResourceItemDefinitionSO[] meals = itemCatalog.All
            .OfType<ResourceItemDefinitionSO>()
            .Where(item => item != null && item.IsMeal)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
        Require(meals.Length >= 13, $"expected at least 13 authored meals, found {meals.Length}");
        Require(meals.All(item => item.Nutrition > 0f),
            "a meal has no nutrition");
        Require(meals.All(item => item.FreshnessSeconds > 0f),
            "a meal has no shelf life");

        Dictionary<MealDietClass, int> counts = meals
            .GroupBy(item => item.MealDietClass)
            .ToDictionary(group => group.Key, group => group.Count());
        Require(GetCount(counts, MealDietClass.Vegan) >= 6,
            "fewer than six vegan meals are authored");
        Require(GetCount(counts, MealDietClass.Vegetarian) >= 2,
            "fewer than two vegetarian meals are authored");
        Require(GetCount(counts, MealDietClass.Mixed) >= 3,
            "fewer than three mixed meals are authored");
        Require(GetCount(counts, MealDietClass.Carnivore) >= 2,
            "fewer than two carnivore meals are authored");

        Require(ResourceMealClassification.IsAllowed(
                CharacterDietPolicyKind.Vegan,
                MealDietClass.Vegan,
                false)
            && !ResourceMealClassification.IsAllowed(
                CharacterDietPolicyKind.Vegan,
                MealDietClass.Vegetarian,
                false),
            "vegan policy matrix mismatch");
        Require(ResourceMealClassification.IsAllowed(
                CharacterDietPolicyKind.Vegetarian,
                MealDietClass.Vegetarian,
                false)
            && !ResourceMealClassification.IsAllowed(
                CharacterDietPolicyKind.Vegetarian,
                MealDietClass.Mixed,
                false),
            "vegetarian policy matrix mismatch");
        Require(ResourceMealClassification.IsAllowed(
                CharacterDietPolicyKind.CarnivorePreferred,
                MealDietClass.Carnivore,
                false)
            && !ResourceMealClassification.IsAllowed(
                CharacterDietPolicyKind.CarnivorePreferred,
                MealDietClass.Vegan,
                false),
            "carnivore-preferred policy matrix mismatch");
        Require(!ResourceMealClassification.IsAllowed(
                CharacterDietPolicyKind.StrictTaboo,
                MealDietClass.Vegan,
                true),
            "strict taboo policy accepted a forbidden ingredient");

        ResourceItemDefinitionSO ration = meals.Single(item =>
            item.ItemId == "food:preserved-ration");
        Require(ration.Preserved
            && ration.MealQuality == MealQualityTier.Preserved
            && ration.FreshnessSeconds == meals.Max(item => item.FreshnessSeconds),
            "preserved ration metadata mismatch");
        return $"meals={meals.Length}; vegan={GetCount(counts, MealDietClass.Vegan)}; preserved={ration.FreshnessSeconds:0}s";
    }

    private static string VerifyMedicineAndSubstanceContent()
    {
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ResourceItemDefinitionSO.ResourcePath);
        ResourceItemDefinitionSO[] medicines = items
            .Where(item => item != null && item.Kind == ResourceItemKind.Medicine)
            .ToArray();
        (ResourceItemDefinitionSO Item, SubstanceItemFeature Feature)[] substances = items
            .Where(item => item != null
                && item.TryGetFeature(out SubstanceItemFeature _))
            .Select(item => (
                item,
                item.GetFeatureOrDefault<SubstanceItemFeature>()))
            .ToArray();
        Require(medicines.Length >= 6, $"medicine definitions={medicines.Length}");
        Require(medicines.Count(item => item.SupportsInjuryTreatment) >= 4,
            "fewer than four medicines can treat injuries");

        ResourceItemDefinitionSO herbal = medicines.Single(item =>
            item.ItemId == "medicine:herbal-poultice");
        ResourceItemDefinitionSO antiseptic = medicines.Single(item =>
            item.ItemId == "medicine:antiseptic");
        ResourceItemDefinitionSO standard = medicines.Single(item =>
            item.ItemId == "medicine:standard");
        ResourceItemDefinitionSO advanced = medicines.Single(item =>
            item.ItemId == "medicine:advanced");
        ResourceItemDefinitionSO antidote = medicines.Single(item =>
            item.ItemId == "medicine:antidote");
        ResourceItemDefinitionSO anesthetic = medicines.Single(item =>
            item.ItemId == "medicine:anesthetic");
        Require(advanced.TreatmentPotency > standard.TreatmentPotency
            && standard.TreatmentPotency > herbal.TreatmentPotency,
            "medicine treatment potency progression mismatch");
        Require(antiseptic.InfectionReduction > herbal.InfectionReduction,
            "antiseptic does not reduce more infection than herbal treatment");
        Require(antidote.DetoxReduction > 0f && !antidote.SupportsInjuryTreatment,
            "antidote role metadata mismatch");
        Require(anesthetic.PainReduction > 0f && !anesthetic.SupportsInjuryTreatment,
            "anesthetic role metadata mismatch");

        Require(substances.Length >= 9, $"substances={substances.Length}");
        Require(substances
                .Select(substance => substance.Feature.substanceId)
                .Distinct(StringComparer.Ordinal)
                .Count() == substances.Length,
            "substance IDs are duplicated");
        Require(substances.All(substance => substance.Item.StableId.IsValid
                && !string.IsNullOrWhiteSpace(substance.Feature.substanceId)),
            "a substance feature has no authored physical item or stable ID");
        Require(substances
            .Where(substance => substance.Feature.useClass == SubstanceUseClass.NonAddictive)
            .All(substance => Mathf.Approximately(substance.Feature.addictionChance, 0f)),
            "a non-addictive substance has addiction chance");
        Require(substances
            .Where(substance => substance.Feature.useClass == SubstanceUseClass.Addictive)
            .All(substance => substance.Feature.toleranceGain > 0f
                && substance.Feature.withdrawalPerHour > 0f),
            "an addictive substance has no tolerance or withdrawal");
        return $"medicines={medicines.Length}; substances={substances.Length}";
    }

    private static string VerifyConsumablesSavePayload()
    {
        DungeonCharacterConsumablesSaveData source =
            new DungeonCharacterConsumablesSaveData
            {
                dietPolicies = new List<CharacterDietPolicyState>
                {
                    new CharacterDietPolicyState
                    {
                        characterId = "character:test",
                        policy = CharacterDietPolicyKind.Vegan
                    }
                },
                substancePolicies = new List<CharacterSubstancePolicyState>
                {
                    new CharacterSubstancePolicyState
                    {
                        characterId = "character:test",
                        itemDefinitionId = "drug:blood-stimulant",
                        mode = SubstancePolicyMode.CombatOnly
                    }
                },
                substanceStates = new List<CharacterSubstanceState>
                {
                    new CharacterSubstanceState
                    {
                        characterId = "character:test",
                        itemDefinitionId = "drug:blood-stimulant",
                        tolerance = 22f,
                        addiction = 64f,
                        withdrawal = 17f,
                        activeSeconds = 31f,
                        scheduledCooldownSeconds = 640f,
                        addicted = true
                    }
                },
                pendingMealDeliveries = new List<CharacterMealDeliveryState>
                {
                    new CharacterMealDeliveryState
                    {
                        deliveryId = "consumable-delivery:0001",
                        characterId = "character:test",
                        buildingInstanceId = "building:test",
                        itemDefinitionId = "food:preserved-ration",
                        requestedAt = 10f,
                        retryAfter = 55f
                    }
                },
                completedOperations = new List<CharacterConsumableOperationState>
                {
                    new CharacterConsumableOperationState
                    {
                        operationId = "consumable-operation:0001",
                        characterId = "character:test",
                        itemDefinitionId = "drug:blood-stimulant",
                        itemStackId = "stack:test",
                        completedAt = 12f
                    }
                }
            };
        string json = JsonUtility.ToJson(source);
        DungeonCharacterConsumablesSaveData restored =
            JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(json);
        Require(restored != null
            && restored.version == DungeonCharacterConsumablesSaveData.CurrentVersion
            && restored.dietPolicies.Single().policy == CharacterDietPolicyKind.Vegan
            && restored.substancePolicies.Single().mode == SubstancePolicyMode.CombatOnly
            && restored.pendingMealDeliveries.Single().DeliveryId.IsValid
            && restored.completedOperations.Single().OperationId.IsValid,
            "consumables policy save round-trip mismatch");
        CharacterSubstanceState state = restored.substanceStates.Single();
        Require(state.addicted
            && Mathf.Approximately(state.tolerance, 22f)
            && Mathf.Approximately(state.addiction, 64f)
            && Mathf.Approximately(state.withdrawal, 17f)
            && Mathf.Approximately(state.activeSeconds, 31f)
            && Mathf.Approximately(state.scheduledCooldownSeconds, 640f),
            "substance state save round-trip mismatch");
        return $"version={restored.version}; tolerance={state.tolerance:0.#}; withdrawal={state.withdrawal:0.#}";
    }

    private static string VerifyConsumablesTypedFailures()
    {
        string[] mutableParameters = { "stack:canonical" };
        MealConsumptionResult canonical = MealConsumptionResult.Failed(
            CharacterConsumablesFailureCode.ItemStackMissing,
            mutableParameters);
        mutableParameters[0] = "stack:mutated";
        Require(canonical.FailureCode == CharacterConsumablesFailureCode.ItemStackMissing
                && canonical.Parameters.Count == 1
                && canonical.Parameters[0] == "stack:canonical"
                && canonical.Parameters is not string[],
            "consumables failure parameters are not immutable");
        Require(typeof(MealConsumptionResult).GetProperty("FailureReason") == null
                && typeof(SubstanceUseResult).GetProperty("FailureReason") == null,
            "a consumables result still exposes sentence-shaped FailureReason state");

        MealConsumptionResult policy = MealConsumptionResult.Failed(
            CharacterConsumablesFailureCode.PolicyForbidden,
            "food:test",
            CharacterDietPolicyKind.Vegan.ToString());
        MealConsumptionResult missing = MealConsumptionResult.Failed(
            CharacterConsumablesFailureCode.ItemDefinitionMissing,
            "food:missing");
        Require(policy.Parameters.SequenceEqual(new[] { "food:test", "Vegan" })
                && missing.Parameters.SequenceEqual(new[] { "food:missing" }),
            "policy or missing-item failure parameters are not canonical");

        UnityEngine.Object sharedAsset = AssetDatabase.LoadMainAssetAtPath(
            "Assets/Localization/DomainFailures Shared Data.asset");
        UnityEngine.Object koreanAsset = AssetDatabase.LoadMainAssetAtPath(
            "Assets/Localization/DomainFailures_ko.asset");
        Require(sharedAsset != null && koreanAsset != null,
            "DomainFailures localization assets are missing");
        SerializedProperty sharedEntries = new SerializedObject(sharedAsset)
            .FindProperty("m_Entries");
        SerializedProperty localizedEntries = new SerializedObject(koreanAsset)
            .FindProperty("m_TableData");
        Require(sharedEntries != null && localizedEntries != null,
            "DomainFailures localization serialization layout changed");

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        HashSet<long> sharedIds = new HashSet<long>();
        for (int index = 0; index < sharedEntries.arraySize; index++)
        {
            SerializedProperty entry = sharedEntries.GetArrayElementAtIndex(index);
            keys.Add(entry.FindPropertyRelative("m_Key").stringValue);
            sharedIds.Add(entry.FindPropertyRelative("m_Id").longValue);
        }
        HashSet<long> localizedIds = new HashSet<long>();
        for (int index = 0; index < localizedEntries.arraySize; index++)
        {
            localizedIds.Add(localizedEntries.GetArrayElementAtIndex(index)
                .FindPropertyRelative("m_Id").longValue);
        }
        string[] requiredKeys = Enum.GetValues(typeof(CharacterConsumablesFailureCode))
            .Cast<CharacterConsumablesFailureCode>()
            .Where(code => code != CharacterConsumablesFailureCode.None)
            .Select(code => code.ToString())
            .ToArray();
        string[] requiredDomainKeys = Enum.GetValues(typeof(FailureCode))
            .Cast<FailureCode>()
            .Where(code => code != FailureCode.None)
            .Select(code => code.ToString())
            .ToArray();
        HashSet<string> requiredAllKeys = new(
            requiredDomainKeys
                .Concat(requiredKeys)
                .Concat(Enum.GetValues(typeof(SurgeryStatusCode))
                    .Cast<SurgeryStatusCode>()
                    .Where(code => code != SurgeryStatusCode.None)
                    .Select(code => code.ToString()))
                .Concat(Enum.GetValues(typeof(SurgeryRiskSummaryCode))
                    .Cast<SurgeryRiskSummaryCode>()
                    .Where(code => code != SurgeryRiskSummaryCode.None)
                    .Select(code => code.ToString()))
                .Concat(Enum.GetValues(typeof(CharacterMedicalStatusCode))
                    .Cast<CharacterMedicalStatusCode>()
                    .Where(code => code != CharacterMedicalStatusCode.Unknown)
                    .Select(code => "CharacterMedicalStatus" + code))
                .Concat(Enum.GetValues(typeof(InfrastructureStatusCode))
                    .Cast<InfrastructureStatusCode>()
                    .Where(code => code != InfrastructureStatusCode.None)
                    .Select(code => "InfrastructureStatus" + code))
                .Concat(Enum.GetValues(typeof(RunResultTextId))
                    .Cast<RunResultTextId>()
                    .Select(code => code switch
                    {
                        RunResultTextId.EmptyResult => "RunResultEmpty",
                        RunResultTextId.NextRun => "RunResultNextRun",
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(code),
                            code,
                            null)
                    })),
            StringComparer.Ordinal);
        bool keysMatch = keys.SetEquals(requiredAllKeys);
        bool localizedIdsMatch = sharedIds.SetEquals(localizedIds);
        Require(keysMatch && localizedIdsMatch,
            "DomainFailures shared/Korean keys no longer exactly match the typed failure enums; "
            + $"keys={keys.Count}/{requiredAllKeys.Count}, ids={sharedIds.Count}/{localizedIds.Count}, "
            + $"missingKeys=[{string.Join(",", requiredAllKeys.Except(keys).OrderBy(value => value))}], "
            + $"extraKeys=[{string.Join(",", keys.Except(requiredAllKeys).OrderBy(value => value))}], "
            + $"missingLocalizedIds=[{string.Join(",", sharedIds.Except(localizedIds).OrderBy(value => value))}], "
            + $"extraLocalizedIds=[{string.Join(",", localizedIds.Except(sharedIds).OrderBy(value => value))}]");
        return $"codes={requiredKeys.Length}; parameters=immutable; localization={sharedIds.Count}/{localizedIds.Count}";
    }

    private static string VerifyConsumablesPhysicalExactlyOnce()
    {
        GameObject actorObject = new GameObject("ConsumablesExactlyOnceActor");
        WorldItemStackRuntime itemRuntime = null;
        CharacterActor actor = null;
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        try
        {
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            CharacterSO characterData = CharacterAiEditorTestDependencies
                .ContentDefinitions.GetAll<CharacterSO>()
                .Where(value => value != null
                    && value.characterType == CharacterType.NPC
                    && value.DefinitionId.IsValid)
                .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored NPC archetype is available for the substance V8 fixture.");
            actor.data = characterData;
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(new CharacterId("character:consumables-fixture"));
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (actor.IsUnpublishedComposition)
                actor.PublishComposition();
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);

            itemRuntime = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService reservations,
                out IReservedItemTransferService reservedTransfers,
                out IReservedPhysicalItemBatchDispositionService reservedBatch,
                out IPhysicalItemBatchDispositionService batch);
            FailFirstAcknowledgeBatchDisposition failFirstAcknowledge = new(batch);
            IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            CharacterConsumablesApplicationPorts ports = new CharacterConsumablesApplicationPorts(
                itemCatalog,
                itemRuntime,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: failFirstAcknowledge);
            CharacterConsumablesRuntime core = new CharacterConsumablesRuntime(
                ports,
                ports,
                ports,
                new UnityGameClock(),
                new RandomStreamProvider(90210),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            CharacterConsumablesCompatibilityAdapter runtime =
                new CharacterConsumablesCompatibilityAdapter(core);
            runtime.SetPolicy(
                actor,
                "substance:vitality-tonic",
                SubstancePolicyMode.MoodThreshold,
                moodThreshold: 100f);
            Require(itemRuntime.SpawnItemAt(
                    "drug:vitality-tonic",
                    2,
                    Vector2Int.zero,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 2,
                "fixture did not spawn two physical consumable items");
            WorldItemStackSnapshot stack = itemRuntime.GetAllStacks()
                .Single(value => value.ItemId == "drug:vitality-tonic");
            ConsumeSubstanceCommand command = new ConsumeSubstanceCommand(
                new ConsumableOperationId("consumable-operation:fixture-once"),
                CharacterPersistentIdentity.Require(actor),
                new ItemDefinitionId("drug:vitality-tonic"),
                new ItemStackId(stack.StackId),
                medicalContext: false,
                combatContext: false);

            Require(ports.TryGetActor(command.CharacterId, out _),
                "substance V8 fixture actor was not visible through the production port; "
                + $"actual={CharacterPersistentIdentity.Require(actor).Value}; "
                + $"world=[{string.Join(",", ports.CharacterIds.Select(id => id.Value))}]");

            Require(runtime.TryConsume(command, out SubstanceUseResult first)
                    && first.Success
                    && first.ItemStackId.Equals(command.ItemStackId),
                $"first physical consume failed: {first.FailureCode}");
            int quantityAfterFirst = itemRuntime.GetAllStacks()
                .Where(value => value.ItemId == "drug:vitality-tonic")
                .Sum(value => value.Quantity);
            DungeonCharacterConsumablesSaveData pendingSave = core.Capture();
            Require(pendingSave.version
                    == DungeonCharacterConsumablesSaveData.CurrentVersion
                    && pendingSave.activeSubstanceUsePlans.Count == 1
                    && pendingSave.activeSubstanceUsePlans[0].phase
                        == CharacterSubstanceUsePlanPhase.EffectsPublished
                    && pendingSave.completedOperations.Count == 1
                    && batch.TryGetPending(command.OperationId.Value, out _),
                "substance V8 did not retain its effects-published pending outbox");
            DungeonCharacterConsumablesSaveData mismatchedSubstanceReceipt =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    JsonUtility.ToJson(pendingSave));
            mismatchedSubstanceReceipt.activeSubstanceUsePlans[0]
                .physicalCommitInputMassGrams++;
            RequireThrows<InvalidOperationException>(
                () => core.BuildRestoreCandidate(mismatchedSubstanceReceipt),
                "substance V8 restore accepted mismatched pending receipt mass");
            Require(batch.TryGetPending(command.OperationId.Value, out _),
                "failed substance V8 receipt validation mutated the pending disposition");
            CharacterSubstanceState stateAfterEffects = core.GetSubstanceState(
                command.CharacterId,
                command.ItemDefinitionId.Value);
            Require(!runtime.TryConsume(command, out SubstanceUseResult duplicate)
                    && duplicate.FailureCode
                        == CharacterConsumablesFailureCode.AlreadyProcessed
                    && duplicate.Parameters.SequenceEqual(new[] { command.OperationId.Value }),
                "duplicate operation was not rejected exactly once");
            int quantityAfterDuplicate = itemRuntime.GetAllStacks()
                .Where(value => value.ItemId == "drug:vitality-tonic")
                .Sum(value => value.Quantity);
            runtime.SetPolicy(
                actor,
                "substance:vitality-tonic",
                SubstancePolicyMode.Forbidden);
            ConsumeSubstanceCommand policyCommand = new ConsumeSubstanceCommand(
                new ConsumableOperationId("consumable-operation:fixture-policy"),
                command.CharacterId,
                command.ItemDefinitionId,
                command.ItemStackId,
                medicalContext: false,
                combatContext: false);
            Require(!runtime.TryConsume(policyCommand, out SubstanceUseResult policyFailure)
                    && policyFailure.FailureCode
                        == CharacterConsumablesFailureCode.PolicyForbidden
                    && policyFailure.Parameters.Count == 2
                    && policyFailure.Parameters[0] == command.ItemDefinitionId.Value,
                "forbidden substance policy did not return a typed failure");
            runtime.SetPolicy(
                actor,
                "substance:vitality-tonic",
                SubstancePolicyMode.MoodThreshold,
                moodThreshold: 100f);
            ConsumeSubstanceCommand missingCommand = new ConsumeSubstanceCommand(
                new ConsumableOperationId("consumable-operation:fixture-missing"),
                command.CharacterId,
                command.ItemDefinitionId,
                new ItemStackId("stack:missing"),
                medicalContext: false,
                combatContext: false);
            Require(!runtime.TryConsume(missingCommand, out SubstanceUseResult missingFailure)
                    && missingFailure.FailureCode
                        == CharacterConsumablesFailureCode.ItemStackMissing
                    && missingFailure.Parameters.SequenceEqual(new[] { "stack:missing" }),
                "missing physical substance did not return a typed failure");
            CharacterConsumablesRuntime restored = new CharacterConsumablesRuntime(
                ports,
                ports,
                ports,
                new UnityGameClock(),
                new RandomStreamProvider(777777),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(pendingSave));
            restored.Tick();
            DungeonCharacterConsumablesSaveData captured = restored.Capture();
            CharacterSubstanceState restoredState = restored.GetSubstanceState(
                command.CharacterId,
                command.ItemDefinitionId.Value);
            Require(quantityAfterFirst == 1
                    && quantityAfterDuplicate == 1
                    && captured.completedOperations.Count == 1
                    && captured.completedOperations.Single().itemStackId == stack.StackId
                    && captured.activeSubstanceUsePlans.Count == 0
                    && !batch.TryGetPending(command.OperationId.Value, out _)
                    && Mathf.Approximately(
                        restoredState.tolerance,
                        stateAfterEffects.tolerance)
                    && Mathf.Approximately(
                        restoredState.addiction,
                        stateAfterEffects.addiction)
                    && restoredState.addicted == stateAfterEffects.addicted
                    && restoredState.overdosed == stateAfterEffects.overdosed,
                "physical quantity or operation ledger diverged after duplicate command");
            DungeonCharacterConsumablesSaveData missingSubstanceReceipt =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    JsonUtility.ToJson(pendingSave));
            missingSubstanceReceipt.activeSubstanceUsePlans[0].phase =
                CharacterSubstanceUsePlanPhase.ItemCommitted;
            missingSubstanceReceipt.completedOperations.Clear();
            RequireThrows<InvalidOperationException>(
                () => restored.BuildRestoreCandidate(missingSubstanceReceipt),
                "substance V8 restore accepted ItemCommitted without its pending receipt");

            WorldItemRepositoryEditorAccess.RemoveStack(repository, stack.StackId);
            string carriedStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                "drug:vitality-tonic",
                1,
                WorldItemStackState.Carried,
                destinationId: command.CharacterId.Value,
                position: actor.GetNowXY());
            CharacterCarryInventory carry =
                actorObject.GetComponent<CharacterCarryInventory>()
                ?? actorObject.AddComponent<CharacterCarryInventory>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            carry.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new()
                    {
                        carriedStackId = carriedStackId,
                        sourceStackId = carriedStackId,
                        ownerOperationId = "consumable-operation:fixture-carried",
                        itemId = "drug:vitality-tonic",
                        quantity = 1
                    }
                }
            });
            FailFirstAcknowledgeBatchDisposition carriedAck = new(batch);
            CharacterConsumablesApplicationPorts carriedPorts = new(
                itemCatalog,
                itemRuntime,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: carriedAck);
            CharacterConsumablesRuntime carriedCore = new(
                carriedPorts,
                carriedPorts,
                carriedPorts,
                new UnityGameClock(),
                new RandomStreamProvider(90211),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            CharacterConsumablesCompatibilityAdapter carriedRuntime = new(carriedCore);
            carriedRuntime.SetPolicy(
                actor,
                "substance:vitality-tonic",
                SubstancePolicyMode.MoodThreshold,
                moodThreshold: 100f);
            ConsumeSubstanceCommand carriedCommand = new(
                new ConsumableOperationId("consumable-operation:fixture-carried"),
                command.CharacterId,
                command.ItemDefinitionId,
                new ItemStackId(carriedStackId),
                medicalContext: false,
                combatContext: false);
            Require(carriedRuntime.TryConsume(
                    carriedCommand,
                    out SubstanceUseResult carriedResult)
                    && carriedResult.Success
                    && carry.Items.Count == 0
                    && !itemRuntime.GetAllStacks().Any(value =>
                        string.Equals(
                            value.StackId,
                            carriedStackId,
                            StringComparison.Ordinal))
                    && batch.TryGetPending(carriedCommand.OperationId.Value, out _),
                "carried substance did not atomically leave carry/world custody; "
                + $"success={carriedResult.Success};failure={carriedResult.FailureCode};"
                + $"parameters=[{string.Join(",", carriedResult.Parameters)}];"
                + $"carry={carry.Items.Count};"
                + $"world={itemRuntime.GetAllStacks().Count(value => string.Equals(value.StackId, carriedStackId, StringComparison.Ordinal))};"
                + $"worldDetail={string.Join("|", itemRuntime.GetAllStacks().Where(value => string.Equals(value.StackId, carriedStackId, StringComparison.Ordinal)).Select(value => $"{value.State}/q{value.Quantity}/r{value.ReservedQuantity}/a{value.AvailableQuantity}/d{value.DestinationId}"))};"
                + $"pending={batch.TryGetPending(carriedCommand.OperationId.Value, out _)}");
            DungeonCharacterConsumablesSaveData carriedPending = carriedCore.Capture();
            CharacterConsumablesRuntime carriedRestored = new(
                carriedPorts,
                carriedPorts,
                carriedPorts,
                new UnityGameClock(),
                new RandomStreamProvider(123456),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            carriedRestored.PublishRestoreCandidate(
                carriedRestored.BuildRestoreCandidate(carriedPending));
            carriedRestored.Tick();
            Require(carriedRestored.Capture().activeSubstanceUsePlans.Count == 0
                    && !batch.TryGetPending(carriedCommand.OperationId.Value, out _)
                    && carry.Items.Count == 0,
                "carried substance restore did not acknowledge without reminting cargo");
            return $"stack={stack.StackId}; quantity=2->1->1; ledger=1; V8=ack-replay; carried=exact";
        }
        finally
        {
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            itemRuntime?.Dispose();
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string VerifyPackagedConsumableMissingTareRecovery()
    {
        const string ItemId = "drug:vitality-tonic";
        const string ContainerId = "container:medical-vial";
        const string OperationId =
            "consumable-operation:fixture-packaged-missing-tare";
        GameObject actorObject = new("Packaged Consumable Missing Tare Actor");
        WorldItemStackRuntime itemRuntime = null;
        CharacterActor actor = null;
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        try
        {
            CharacterId characterId = new(
                "character:consumables-packaged-missing-tare");
            PackagedConsumablesTestCatalog physicalCatalog = new();
            itemRuntime = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    physicalCatalog,
                    out _,
                    out _,
                    out ItemQuantityReservationService reservations,
                    out IReservedItemTransferService reservedTransfers,
                    out IReservedPhysicalItemBatchDispositionService reservedBatch,
                    out IPhysicalItemBatchDispositionService batch);

            // Build the scenario actor after the shared physical-item fixture;
            // that fixture composes its own temporary equipment owner and must
            // never be allowed to capture this actor's persistent identity.
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.data = CharacterAiEditorTestDependencies.ContentDefinitions
                .GetAll<CharacterSO>()
                .Where(value => value != null
                    && value.characterType == CharacterType.NPC
                    && value.role != CharacterRole.Owner
                    && value.DefinitionId.IsValid)
                .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored NPC archetype is available for the packaged consumable fixture.");
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(characterId);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (actor.IsUnpublishedComposition)
                actor.PublishComposition();
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);
            IPackagedLotDefinitionQuery packagedLots =
                itemRuntime.MassQuery as IPackagedLotDefinitionQuery;
            Require(packagedLots != null
                    && packagedLots.TryGetPackagedLot(
                        (ItemDefinitionId)ItemId,
                        out PackagedLotDefinitionSnapshot packagedLot)
                    && packagedLot.TareMass.Value == 30L
                    && packagedLot.TareDisposition
                        == PackageTareDisposition.ReusableContainerReturn
                    && packagedLot.ContainerItemId.Value == ContainerId,
                "fixture vitality tonic was not captured as a reusable packaged lot");

            IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            CharacterConsumablesApplicationPorts missingTarePorts = new(
                itemCatalog,
                itemRuntime,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: batch,
                tareDispositions: null);
            CharacterConsumablesRuntime core = new(
                missingTarePorts,
                missingTarePorts,
                missingTarePorts,
                new UnityGameClock(),
                new RandomStreamProvider(90212),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            CharacterConsumablesCompatibilityAdapter runtime = new(core);
            runtime.SetPolicy(
                actor,
                "substance:vitality-tonic",
                SubstancePolicyMode.MoodThreshold,
                moodThreshold: 100f);
            Require(itemRuntime.SpawnItemAt(
                    ItemId,
                    1,
                    actor.GetNowXY(),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 1,
                "fixture did not spawn its packaged physical consumable");
            WorldItemStackSnapshot source = itemRuntime.GetAllStacks()
                .Single(value => value.ItemId == ItemId);
            ConsumeSubstanceCommand command = new(
                new ConsumableOperationId(OperationId),
                characterId,
                new ItemDefinitionId(ItemId),
                new ItemStackId(source.StackId),
                medicalContext: false,
                combatContext: false);

            Require(missingTarePorts.TryGetActor(
                    characterId,
                    out CharacterConsumablesActorSnapshot actorSnapshot),
                "packaged consumable fixture actor is not visible through the production port; "
                + $"requested={characterId.Value};"
                + $"identity={CharacterPersistentIdentity.Require(actor).Value};"
                + $"world=[{string.Join(",", missingTarePorts.CharacterIds.Select(id => id.Value))}]");
            Require(actorSnapshot.Active,
                "packaged consumable fixture actor is visible but inactive");
            Require(runtime.TryConsume(command, out SubstanceUseResult first)
                    && first.Success,
                $"packaged consumable did not publish its first effect: {first.FailureCode}");
            DungeonCharacterConsumablesSaveData pending = core.Capture();
            CharacterSubstanceState stateAfterEffects = core.GetSubstanceState(
                characterId,
                ItemId);
            float toleranceAfterEffects = stateAfterEffects.tolerance;
            float addictionAfterEffects = stateAfterEffects.addiction;
            Require(batch.TryGetPending(
                    OperationId,
                    out PhysicalItemBatchDispositionReceipt receipt),
                "missing tare service discarded the pending physical receipt");
            Require(pending.activeSubstanceUsePlans.Count == 1
                    && pending.activeSubstanceUsePlans[0].phase
                        == CharacterSubstanceUsePlanPhase.EffectsPublished
                    && pending.completedOperations.Count == 1
                    && !itemRuntime.GetAllStacks().Any(value =>
                        value.ItemId == ItemId)
                    && !itemRuntime.GetAllStacks().Any(value =>
                        value.ItemId == ContainerId),
                "missing tare service did not retain the effects-published physical outbox");
            Require(!missingTarePorts.TryAcknowledgeSubstanceConsumption(
                        characterId,
                        new ConsumableItemDefinitionId(ItemId),
                        1,
                        receipt.CommitId,
                        out string missingFailure)
                    && missingFailure
                        == "substance-packaged-tare-service-missing"
                    && batch.TryGetPending(OperationId, out _),
                "packaged Sink bypassed the missing tare service gate");

            core.Tick();
            core.Tick();
            DungeonCharacterConsumablesSaveData afterMissingRetries = core.Capture();
            CharacterSubstanceState afterRetryState = core.GetSubstanceState(
                characterId,
                ItemId);
            Require(afterMissingRetries.activeSubstanceUsePlans.Count == 1
                    && afterMissingRetries.completedOperations.Count == 1
                    && batch.TryGetPending(OperationId, out _)
                    && Mathf.Approximately(
                        afterRetryState.tolerance,
                        toleranceAfterEffects)
                    && Mathf.Approximately(
                        afterRetryState.addiction,
                        addictionAfterEffects),
                "missing tare retries duplicated effects or discarded the pending receipt");

            IPackagedLotTareDispositionService tare =
                new PackagedLotTareDispositionService(
                    packagedLots,
                    new PackagedLotTareOutputGateway(itemRuntime));
            CharacterConsumablesApplicationPorts restoredPorts = new(
                itemCatalog,
                itemRuntime,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: batch,
                tareDispositions: tare);
            CharacterConsumablesRuntime restored = new(
                restoredPorts,
                restoredPorts,
                restoredPorts,
                new UnityGameClock(),
                new RandomStreamProvider(90213),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            restored.PublishRestoreCandidate(
                restored.BuildRestoreCandidate(afterMissingRetries));
            restored.Tick();
            DungeonCharacterConsumablesSaveData recovered = restored.Capture();
            CharacterSubstanceState recoveredState = restored.GetSubstanceState(
                characterId,
                ItemId);
            WorldItemStackSnapshot[] tareOutputs = itemRuntime.GetAllStacks()
                .Where(value => value.ItemId == ContainerId)
                .ToArray();
            Require(recovered.activeSubstanceUsePlans.Count == 0
                    && recovered.completedOperations.Count == 1
                    && !batch.TryGetPending(OperationId, out _)
                    && tareOutputs.Length == 1
                    && tareOutputs[0].Quantity == 1
                    && tareOutputs[0].State == WorldItemStackState.Loose
                    && Mathf.Approximately(
                        recoveredState.tolerance,
                        toleranceAfterEffects)
                    && Mathf.Approximately(
                        recoveredState.addiction,
                        addictionAfterEffects),
                "restored tare service did not publish one container and close the outbox");

            restored.Tick();
            Require(itemRuntime.GetAllStacks()
                        .Where(value => value.ItemId == ContainerId)
                        .Sum(value => value.Quantity) == 1
                    && restored.Capture().completedOperations.Count == 1,
                "completed packaged Sink replay duplicated its tare or gameplay result");
            return "missing-service=pending; effects=1; restored-tare=1; replay=0";
        }
        finally
        {
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            itemRuntime?.Dispose();
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string VerifyTavernRecreationalSubstanceService()
    {
        const string actorId = "character:tavern-recreation-fixture";
        const string beverageId = "food:twilight-beer";
        GameObject actorObject = new GameObject("TavernRecreationActor");
        GameObject facilityObject = new GameObject("TavernRecreationFacility");
        WorldItemStackRuntime itemRuntime = null;
        CharacterActor actor = null;
        Facility facility = null;
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        try
        {
            BuildingSO d12 = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Modular/D12_술음료장.asset");
            BuildingRecreationalSubstanceServiceAbility service =
                d12?.GetAbility<BuildingRecreationalSubstanceServiceAbility>();
            Require(d12 != null
                    && service?.IsValid == true
                    && d12.Facility.SupportsRole(FacilityRole.Entertainment)
                    && !d12.Facility.SupportsRole(FacilityRole.Meal)
                    && d12.GetAbility<BuildingNeedRecoveryAbility>() == null,
                "D12 is not authored as an entertainment-only physical beverage service");

            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.data = CharacterAiEditorTestDependencies.ContentDefinitions
                .GetAll<CharacterSO>()
                .Where(value => value != null
                    && value.characterType == CharacterType.NPC
                    && value.DefinitionId.IsValid)
                .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored NPC archetype is available for the tavern fixture.");
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(new CharacterId(actorId));
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (actor.IsUnpublishedComposition)
                actor.PublishComposition();
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);

            facility = facilityObject.AddComponent<Facility>();
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(d12, new Vector2Int(4, 4));

            itemRuntime = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out _,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out IReservedItemTransferService reservedTransfers,
                out IReservedPhysicalItemBatchDispositionService reservedBatch,
                out IPhysicalItemBatchDispositionService batch);
            IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            CharacterConsumablesApplicationPorts ports = new CharacterConsumablesApplicationPorts(
                itemCatalog,
                itemRuntime,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: quantityReservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: batch);
            CharacterConsumablesRuntime core = new CharacterConsumablesRuntime(
                ports,
                ports,
                ports,
                new UnityGameClock(),
                new RandomStreamProvider(154),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            CharacterConsumablesCompatibilityAdapter runtime =
                new CharacterConsumablesCompatibilityAdapter(core);
            BuildingInstanceId facilityId = facility.RequirePersistentInstanceId();
            string destination = CharacterConsumablesRuntime
                .GetRecreationalSubstanceDestinationId(
                    facilityId,
                    new ConsumableItemDefinitionId(beverageId));

            runtime.SetPolicy(
                actor,
                "substance:twilight-beer",
                SubstancePolicyMode.MoodThreshold,
                moodThreshold: 100f);
            Require(itemRuntime.SpawnItemAt(
                    beverageId,
                    1,
                    facility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    destination,
                    out int spawned)
                && spawned == 1,
                "fixture could not author one physical tavern beverage");
            Require(runtime.TryConsumeAtFacility(actor, facility, out SubstanceUseResult served)
                    && served.Success
                    && served.ItemDefinitionId.Value == beverageId
                    && runtime.GetState(actor, "substance:twilight-beer").activeSeconds > 0f
                    && runtime.GetWorkSpeedMultiplier(actor) < 1f,
                $"tavern beverage did not use the substance authority: {served.FailureCode}");
            int remainingAfterSuccess = itemRuntime.GetAllStacks()
                .Where(stack => stack.ItemId == beverageId)
                .Sum(stack => stack.Quantity);
            Require(remainingAfterSuccess == 0,
                "successful tavern service did not consume exactly one physical beverage");

            runtime.SetPolicy(
                actor,
                "substance:twilight-beer",
                SubstancePolicyMode.Forbidden);
            Require(itemRuntime.SpawnItemAt(
                    beverageId,
                    1,
                    facility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    destination,
                    out spawned)
                && spawned == 1,
                "fixture could not respawn the policy-failure beverage");
            Require(!runtime.TryConsumeAtFacility(actor, facility, out SubstanceUseResult forbidden)
                    && forbidden.FailureCode == CharacterConsumablesFailureCode.PolicyForbidden
                    && itemRuntime.GetAllStacks()
                        .Where(stack => stack.ItemId == beverageId)
                        .Sum(stack => stack.Quantity) == 1,
                "forbidden tavern use consumed stock or returned the wrong typed failure");

            return $"D12=entertainment; item=1->0; policy=preserved; fun={service.funRecovery:0.#}; substance=active";
        }
        finally
        {
            if (facility != null)
            {
                world.UnregisterBuilding(facility);
            }
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            itemRuntime?.Dispose();
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string VerifyConsumablesStrictRestore()
    {
        GameObject actorObject = new GameObject("ConsumablesRestoreActor");
        WorldItemStackRuntime itemRuntime = null;
        CharacterActor actor = null;
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        try
        {
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(new CharacterId("character:consumables-restore"));
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);
            itemRuntime = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture();
            IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            DungeonRuntimeAggregateRootStore root = new DungeonRuntimeAggregateRootStore();
            CharacterConsumablesApplicationPorts ports = new CharacterConsumablesApplicationPorts(
                itemCatalog,
                itemRuntime,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance);
            CharacterConsumablesRuntime runtime = new CharacterConsumablesRuntime(
                ports,
                ports,
                ports,
                new UnityGameClock(),
                new RandomStreamProvider(7),
                root,
                DefaultCharacterNeedBalanceRuntime.Instance);
            CharacterConsumablesCompatibilityAdapter compatibility =
                new CharacterConsumablesCompatibilityAdapter(runtime);
            compatibility.SetPolicy(actor, CharacterDietPolicyKind.Vegan);
            DungeonCharacterConsumablesSaveData valid = runtime.Capture();
            string validJson = JsonUtility.ToJson(valid);
            CharacterConsumablesApplicationPorts restoredPorts =
                new CharacterConsumablesApplicationPorts(
                    itemCatalog,
                    itemRuntime,
                    world,
                    new GameEventBus(),
                    EmptyCombatCommands.Instance,
                    CharacterAiEditorTestDependencies.NeutralPerformance);
            CharacterConsumablesRuntime restoredRuntime = new CharacterConsumablesRuntime(
                restoredPorts,
                restoredPorts,
                restoredPorts,
                new UnityGameClock(),
                new RandomStreamProvider(7),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            restoredRuntime.PublishRestoreCandidate(
                restoredRuntime.BuildRestoreCandidate(valid));
            Require(JsonUtility.ToJson(restoredRuntime.Capture()) == validJson,
                "valid consumables payload did not round-trip canonically");

            DungeonCharacterConsumablesSaveData invalid = JsonUtility.FromJson<
                DungeonCharacterConsumablesSaveData>(validJson);
            invalid.dietPolicies[0].characterId = "character:missing";
            invalid.pendingMealDeliveries.Add(new CharacterMealDeliveryState
            {
                deliveryId = "consumable-delivery:duplicate",
                characterId = "character:missing",
                buildingInstanceId = "building:missing",
                itemDefinitionId = "item:missing"
            });
            string before = JsonUtility.ToJson(runtime.Capture());
            bool threw = false;
            try
            {
                runtime.BuildRestoreCandidate(invalid);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            Require(threw
                    && JsonUtility.ToJson(runtime.Capture()) == before
                    && root.PublishedRestoreRevision == 0,
                "invalid consumables restore mutated live aggregate state");

            DungeonCharacterConsumablesSaveData whitespaceCharacterId =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validJson);
            whitespaceCharacterId.dietPolicies[0].characterId =
                " character:consumables-restore ";
            bool whitespaceRejected = false;
            try
            {
                runtime.BuildRestoreCandidate(whitespaceCharacterId);
            }
            catch (InvalidOperationException)
            {
                whitespaceRejected = true;
            }
            Require(
                whitespaceRejected,
                "consumables restore accepted a whitespace-padded CharacterId");

            void RequireSequenceRejected(
                DungeonCharacterConsumablesSaveData candidate,
                string expectedError,
                string message)
            {
                string sourceBefore = JsonUtility.ToJson(candidate);
                string liveBefore = JsonUtility.ToJson(runtime.Capture());
                string failure = string.Empty;
                try
                {
                    runtime.BuildRestoreCandidate(candidate);
                }
                catch (InvalidOperationException exception)
                {
                    failure = exception.Message;
                }
                Require(
                    failure.IndexOf(expectedError, StringComparison.Ordinal) >= 0
                    && string.Equals(
                        sourceBefore,
                        JsonUtility.ToJson(candidate),
                        StringComparison.Ordinal)
                    && string.Equals(
                        liveBefore,
                        JsonUtility.ToJson(runtime.Capture()),
                        StringComparison.Ordinal)
                    && root.PublishedRestoreRevision == 0,
                    message);
            }

            CharacterConsumableOperationState GeneratedOperation(long sequence) =>
                new()
                {
                    operationId = $"consumable-operation:auto:v1:{sequence:D16}",
                    characterId = "character:consumables-restore",
                    itemDefinitionId = "food:preserved-ration",
                    itemStackId = "stack:sequence-fixture",
                    meal = true,
                    completedAt = 1f
                };
            CharacterConsumableOperationState LegacyGeneratedOperation(long sequence) =>
                new()
                {
                    operationId = $"consumable-operation:{sequence:D16}",
                    characterId = "character:consumables-restore",
                    itemDefinitionId = "food:preserved-ration",
                    itemStackId = "stack:sequence-fixture",
                    meal = true,
                    completedAt = 1f
                };
            CharacterConsumableOperationState ExternalOperation(string operationId) =>
                new()
                {
                    operationId = operationId,
                    characterId = "character:consumables-restore",
                    itemDefinitionId = "food:preserved-ration",
                    itemStackId = "stack:sequence-fixture",
                    meal = true,
                    completedAt = 1f
                };

            DungeonCharacterConsumablesSaveData validWatermark =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validJson);
            validWatermark.completedOperations.Add(LegacyGeneratedOperation(6));
            validWatermark.completedOperations.Add(GeneratedOperation(7));
            validWatermark.nextOperationSequence = 8;
            string validWatermarkBefore = JsonUtility.ToJson(validWatermark);
            Require(
                runtime.BuildRestoreCandidate(validWatermark) != null
                && string.Equals(
                    validWatermarkBefore,
                    JsonUtility.ToJson(validWatermark),
                    StringComparison.Ordinal),
                "consumables restore rejected or mutated a valid generated-ID watermark");

            DungeonCharacterConsumablesSaveData externalOperationIds =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validJson);
            externalOperationIds.completedOperations.Add(
                ExternalOperation("consumable-operation:+partner-key"));
            externalOperationIds.completedOperations.Add(
                ExternalOperation("consumable-operation:123"));
            string externalOperationIdsBefore = JsonUtility.ToJson(externalOperationIds);
            Require(
                runtime.BuildRestoreCandidate(externalOperationIds) != null
                && externalOperationIds.nextOperationSequence == 1L
                && string.Equals(
                    externalOperationIdsBefore,
                    JsonUtility.ToJson(externalOperationIds),
                    StringComparison.Ordinal),
                "consumables restore rejected, counted, or mutated external idempotency IDs");

            string operationIngressBefore = JsonUtility.ToJson(runtime.Capture());
            bool externalOperationSucceeded = runtime.TryConsumeMeal(
                new ConsumeMealCommand(
                    new ConsumableOperationId("consumable-operation:123"),
                    new CharacterId("character:consumables-restore"),
                    new BuildingInstanceId("building:missing"),
                    new ItemStackId("stack:sequence-fixture")),
                out CharacterConsumablesMealResult externalOperationResult);
            bool automaticOperationSucceeded = runtime.TryConsumeMeal(
                new ConsumeMealCommand(
                    new ConsumableOperationId(
                        "consumable-operation:auto:v1:0000000000000001"),
                    new CharacterId("character:consumables-restore"),
                    new BuildingInstanceId("building:missing"),
                    new ItemStackId("stack:sequence-fixture")),
                out CharacterConsumablesMealResult automaticOperationResult);
            Require(
                !externalOperationSucceeded
                && externalOperationResult.FailureCode
                    == CharacterConsumablesFailureCode.FacilityMissing
                && !automaticOperationSucceeded
                && automaticOperationResult.FailureCode
                    == CharacterConsumablesFailureCode.InvalidCommand
                && string.Equals(
                    operationIngressBefore,
                    JsonUtility.ToJson(runtime.Capture()),
                    StringComparison.Ordinal),
                "consumables public ingress did not distinguish external and reserved automatic operation IDs");

            DungeonRuntimeAggregateRootStore captureRoot = new();
            CharacterConsumablesRuntime captureRuntime =
                new CharacterConsumablesRuntime(
                    ports,
                    ports,
                    ports,
                    new UnityGameClock(),
                    new RandomStreamProvider(8),
                    captureRoot,
                    DefaultCharacterNeedBalanceRuntime.Instance);
            captureRuntime.PublishRestoreCandidate(
                runtime.BuildRestoreCandidate(validWatermark));
            System.Reflection.PropertyInfo writeStateProperty =
                typeof(CharacterConsumablesRuntime).GetProperty(
                    "WriteState",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?? throw new MissingMemberException(
                    typeof(CharacterConsumablesRuntime).FullName,
                    "WriteState");
            object captureState = writeStateProperty.GetValue(captureRuntime);
            System.Reflection.FieldInfo nextOperationField =
                captureState.GetType().GetField(
                    "NextOperationSequence",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?? throw new MissingFieldException(
                    captureState.GetType().FullName,
                    "NextOperationSequence");
            nextOperationField.SetValue(captureState, 7L);
            long captureRevisionBefore = captureRoot.PublishedRestoreRevision;
            string captureFailure = string.Empty;
            try
            {
                captureRuntime.Capture();
            }
            catch (InvalidOperationException exception)
            {
                captureFailure = exception.Message;
            }
            Require(
                captureFailure.IndexOf(
                    "does not exceed existing generated sequence 7",
                    StringComparison.Ordinal) >= 0
                && (long)nextOperationField.GetValue(captureState) == 7L
                && captureRoot.PublishedRestoreRevision == captureRevisionBefore,
                "consumables capture accepted or mutated a stale operation watermark");
            nextOperationField.SetValue(captureState, 8L);
            Require(
                captureRuntime.Capture().nextOperationSequence == 8L,
                "consumables capture did not recover after fixture state restoration");
            System.Reflection.MethodInfo newOperationId =
                typeof(CharacterConsumablesRuntime).GetMethod(
                    "NewOperationId",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?? throw new MissingMethodException(
                    typeof(CharacterConsumablesRuntime).FullName,
                    "NewOperationId");
            ConsumableOperationId generatedOperationId =
                (ConsumableOperationId)newOperationId.Invoke(captureRuntime, null);
            Require(
                string.Equals(
                    generatedOperationId.Value,
                    "consumable-operation:auto:v1:0000000000000008",
                    StringComparison.Ordinal)
                && (long)nextOperationField.GetValue(captureState) == 9L,
                "consumables runtime did not emit the versioned automatic operation ID");
            nextOperationField.SetValue(captureState, 8L);
            nextOperationField.SetValue(captureState, long.MaxValue);
            bool generationOverflowRejected = false;
            try
            {
                newOperationId.Invoke(captureRuntime, null);
            }
            catch (System.Reflection.TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                generationOverflowRejected = true;
            }
            Require(
                generationOverflowRejected
                && (long)nextOperationField.GetValue(captureState)
                    == long.MaxValue,
                "consumables generated an overflowing operation ID or mutated its sequence");
            nextOperationField.SetValue(captureState, 8L);

            System.Reflection.FieldInfo nextDeliveryField =
                captureState.GetType().GetField(
                    "NextDeliverySequence",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?? throw new MissingFieldException(
                    captureState.GetType().FullName,
                    "NextDeliverySequence");
            System.Reflection.MethodInfo newDeliveryId =
                typeof(CharacterConsumablesRuntime).GetMethod(
                    "NewDeliveryId",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?? throw new MissingMethodException(
                    typeof(CharacterConsumablesRuntime).FullName,
                    "NewDeliveryId");
            nextDeliveryField.SetValue(captureState, 1L);
            ConsumableDeliveryId generatedDeliveryId =
                (ConsumableDeliveryId)newDeliveryId.Invoke(captureRuntime, null);
            Require(
                string.Equals(
                    generatedDeliveryId.Value,
                    "consumable-delivery:auto:v1:0000000000000001",
                    StringComparison.Ordinal)
                && (long)nextDeliveryField.GetValue(captureState) == 2L,
                "consumables runtime did not emit the versioned automatic delivery ID");
            nextDeliveryField.SetValue(captureState, long.MaxValue);
            bool deliveryGenerationOverflowRejected = false;
            try
            {
                newDeliveryId.Invoke(captureRuntime, null);
            }
            catch (System.Reflection.TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                deliveryGenerationOverflowRejected = true;
            }
            Require(
                deliveryGenerationOverflowRejected
                && (long)nextDeliveryField.GetValue(captureState)
                    == long.MaxValue,
                "consumables generated an overflowing delivery ID or mutated its sequence");
            nextDeliveryField.SetValue(captureState, 1L);

            DungeonCharacterConsumablesSaveData staleOperationWatermark =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validWatermarkBefore);
            staleOperationWatermark.nextOperationSequence = 7;
            RequireSequenceRejected(
                staleOperationWatermark,
                "does not exceed existing generated sequence 7",
                "consumables restore accepted or mutated a stale operation sequence");

            DungeonCharacterConsumablesSaveData staleDeliveryWatermark =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validJson);
            staleDeliveryWatermark.pendingMealDeliveries.Add(
                new CharacterMealDeliveryState
                {
                    deliveryId = "consumable-delivery:0000000000000009",
                    characterId = "character:consumables-restore",
                    buildingInstanceId = "building:sequence-fixture",
                    itemDefinitionId = "food:preserved-ration"
                });
            staleDeliveryWatermark.nextDeliverySequence = 9;
            RequireSequenceRejected(
                staleDeliveryWatermark,
                "does not exceed existing generated sequence 9",
                "consumables restore accepted or mutated a stale delivery sequence");

            DungeonCharacterConsumablesSaveData malformedGeneratedId =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validJson);
            malformedGeneratedId.completedOperations.Add(GeneratedOperation(1));
            malformedGeneratedId.completedOperations[0].operationId =
                "consumable-operation:auto:v1:0001";
            RequireSequenceRejected(
                malformedGeneratedId,
                "malformed or overflowing generated sequence",
                "consumables restore accepted or mutated a malformed generated ID");

            DungeonCharacterConsumablesSaveData overflowingGeneratedId =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validJson);
            overflowingGeneratedId.pendingMealDeliveries.Add(
                new CharacterMealDeliveryState
                {
                    deliveryId =
                        "consumable-delivery:auto:v1:999999999999999999999999",
                    characterId = "character:consumables-restore",
                    buildingInstanceId = "building:sequence-fixture",
                    itemDefinitionId = "food:preserved-ration"
                });
            RequireSequenceRejected(
                overflowingGeneratedId,
                "malformed or overflowing generated sequence",
                "consumables restore accepted or mutated an overflowing generated ID");

            DungeonCharacterConsumablesSaveData duplicateGeneratedId =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validJson);
            duplicateGeneratedId.completedOperations.Add(GeneratedOperation(3));
            duplicateGeneratedId.completedOperations.Add(GeneratedOperation(3));
            duplicateGeneratedId.nextOperationSequence = 4;
            RequireSequenceRejected(
                duplicateGeneratedId,
                "is duplicated",
                "consumables restore accepted or mutated a duplicate generated ID");

            DungeonCharacterConsumablesSaveData exhaustedSequence =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    validJson);
            exhaustedSequence.completedOperations.Add(
                GeneratedOperation(long.MaxValue));
            exhaustedSequence.nextOperationSequence = long.MaxValue;
            RequireSequenceRejected(
                exhaustedSequence,
                $"does not exceed existing generated sequence {long.MaxValue}",
                "consumables restore accepted or mutated an exhausted sequence");

            DungeonCharacterConsumablesSaveData legacy = JsonUtility.FromJson<
                DungeonCharacterConsumablesSaveData>(validJson);
            legacy.version = DungeonCharacterConsumablesSaveData.CurrentVersion - 1;
            bool legacyRejected = false;
            try
            {
                runtime.BuildRestoreCandidate(legacy);
            }
            catch (InvalidOperationException)
            {
                legacyRejected = true;
            }
            Require(legacyRejected,
                "legacy consumables payload version was accepted");
            Require(typeof(CharacterConsumablesRuntime)
                    .GetConstructors()
                    .Single()
                    .GetParameters()
                    .All(parameter => parameter.ParameterType
                        != typeof(IResourceEconomyContentCatalog)),
                "consumables runtime still uses the projection catalog as classification authority");
            Require(typeof(ICharacterConsumablesApplication)
                    .IsAssignableFrom(typeof(CharacterConsumablesRuntime))
                && typeof(ICharacterConsumablesPersistence)
                    .IsAssignableFrom(typeof(CharacterConsumablesRuntime))
                && typeof(ICharacterConsumablesQuery)
                    .IsAssignableFrom(typeof(CharacterConsumablesCompatibilityAdapter))
                && typeof(ICharacterConsumablesCommand)
                    .IsAssignableFrom(typeof(CharacterConsumablesCompatibilityAdapter))
                && typeof(ICharacterSubstanceRuntime)
                    .IsAssignableFrom(typeof(CharacterConsumablesCompatibilityAdapter)),
                "consumables core/adapter facets are not separated correctly");
            return "roundtrip=canonical; invalid=no-mutation; sequences=guarded; legacy=rejected; catalog=item-definition; facets=split";
        }
        finally
        {
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            itemRuntime?.Dispose();
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static int GetCount(
        IReadOnlyDictionary<MealDietClass, int> counts,
        MealDietClass key)
    {
        return counts.TryGetValue(key, out int count) ? count : 0;
    }

    private static string VerifyWim036SubstanceEffects()
    {
        const string actorValue = "character:wim036:substance-effects";
        GameObject actorObject = new("WIM036_SubstanceEffectsActor");
        WorldItemStackRuntime items = null;
        CharacterActor actor = null;
        ICharacterAiWorldRegistry world =
            CharacterAiEditorTestDependencies.WorldRegistry;
        try
        {
            actor = CreateWim028Actor(actorObject, actorValue);
            CharacterGrowthState growth = actor.Progression.GrowthState;
            growth.initialized = true;
            growth.traitSelectionAuthorityVersion =
                CharacterGrowthState.CurrentTraitSelectionAuthorityVersion;
            growth.traitSelectionAuthorityOrigin =
                CharacterTraitSelectionAuthorityOrigin.PreparedSelection;
            growth.traitIds = new List<int> { 204 };
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);

            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out _,
                out _,
                out ItemQuantityReservationService reservations,
                out IReservedItemTransferService reservedTransfers,
                out IReservedPhysicalItemBatchDispositionService reservedBatch,
                out IPhysicalItemBatchDispositionService batch);
            FailFirstAcknowledgeBatchDisposition failFirst = new(batch);
            IItemDefinitionCatalog catalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            CharacterConsumablesApplicationPorts ports = new(
                catalog,
                items,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: failFirst);
            MutableConsumablesClock clock = new();

            int seed = Enumerable.Range(1, 100000).First(candidate =>
            {
                IRandomStream stream = new RandomStreamProvider(candidate)
                    .Get("character-consumables");
                return !stream.Chance(0.006f)
                    && !stream.Chance(0.05f)
                    && !stream.Chance(0.022f)
                    && !stream.Chance(0.02f)
                    && !stream.Chance(0.016f);
            });
            CharacterConsumablesRuntime core = new(
                ports,
                ports,
                ports,
                clock,
                new RandomStreamProvider(seed),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            CharacterConsumablesCompatibilityAdapter runtime = new(core);
            CharacterStatsProjectionService projection =
                CreateWim036Projection(runtime, clock);
            SetWim036Projection(actor.Stats, projection);
            CharacterId actorId = CharacterPersistentIdentity.Require(actor);

            Require(catalog.TryGet(
                    (ItemDefinitionId)"drug:vitality-tonic",
                    out ItemDefinitionSO vitalityItem)
                && vitalityItem.TryGetFeature(out SubstanceItemFeature vitality)
                && Mathf.Approximately(vitality.workSpeedEffect, 0f)
                && Mathf.Approximately(
                    vitality.fatigueAccumulationReduction,
                    0.12f)
                && Mathf.Approximately(vitality.durationSeconds, 150f)
                && catalog.TryGet(
                    (ItemDefinitionId)"drug:mana-awakener",
                    out ItemDefinitionSO manaItem)
                && manaItem.TryGetFeature(out SubstanceItemFeature mana)
                && Mathf.Approximately(mana.workSpeedEffect, 0f)
                && Mathf.Approximately(mana.researchSpeedEffect, 0.18f)
                && Mathf.Approximately(mana.combatEffect, 0.08f)
                && Mathf.Approximately(mana.durationSeconds, 180f)
                && catalog.TryGet(
                    (ItemDefinitionId)"drug:dreamleaf-analgesic",
                    out ItemDefinitionSO dreamItem)
                && dreamItem.TryGetFeature(out SubstanceItemFeature dream)
                && Mathf.Approximately(dream.workSpeedEffect, 0f)
                && dream.suppressesPerceivedPain
                && Mathf.Approximately(dream.moodEffect, 4f)
                && Mathf.Approximately(dream.combatEffect, -0.03f)
                && Mathf.Approximately(dream.durationSeconds, 240f),
                "WIM036 authored fields do not match the approved three-item contract");

            string[] itemIds =
            {
                "drug:vitality-tonic",
                "drug:mana-awakener",
                "drug:dreamleaf-analgesic"
            };
            foreach (string itemId in itemIds)
            {
                Require(items.SpawnItemAt(
                        itemId,
                        1,
                        Vector2Int.zero,
                        WorldItemStackState.Loose,
                        string.Empty,
                        out int spawned)
                    && spawned == 1,
                    "WIM036 fixture could not spawn one physical " + itemId);
            }

            ConsumeSubstanceCommand Command(
                string itemId,
                string operationSuffix,
                bool medicalContext)
            {
                WorldItemStackSnapshot stack = items.GetAllStacks()
                    .Single(value => string.Equals(
                        value.ItemId,
                        itemId,
                        StringComparison.Ordinal));
                return new ConsumeSubstanceCommand(
                    new ConsumableOperationId(
                        "consumable-operation:wim036:" + operationSuffix),
                    actorId,
                    (ItemDefinitionId)itemId,
                    new ItemStackId(stack.StackId),
                    medicalContext,
                    combatContext: false);
            }

            actor.stats[CharacterCondition.SLEEP] = 100f;
            float projectionSleepBefore = actor.Stats.GetConditionValue(
                CharacterCondition.SLEEP,
                100f);
            float projectionExcretionBefore = actor.Stats.GetConditionValue(
                CharacterCondition.EXCRETION,
                100f);
            float projectionHygieneBefore = actor.Stats.GetConditionValue(
                CharacterCondition.HYGIENE,
                100f);
            float runtimeWorkBefore = runtime.GetWorkSpeedMultiplier(actor);
            float runtimeResearchBefore =
                runtime.GetResearchSpeedMultiplier(actor);
            float runtimeCombatBefore = runtime.GetCombatMultiplier(actor);
            float fatigueAccumulationBefore =
                runtime.GetFatigueAccumulationMultiplier(actor);
            CharacterToxicityStatus toxicityBefore =
                runtime.GetToxicityStatus(actor);
            CharacterSubstanceState vitalityStateBefore = runtime.GetState(
                actor,
                "substance:vitality-tonic");
            CharacterSubstanceState manaStateBefore = runtime.GetState(
                actor,
                "substance:mana-awakener");
            CharacterSubstanceState dreamStateBefore = runtime.GetState(
                actor,
                "substance:dreamleaf-analgesic");
            float withdrawalBefore = vitalityStateBefore.withdrawal
                + manaStateBefore.withdrawal
                + dreamStateBefore.withdrawal;
            bool painSuppressedBefore =
                runtime.SuppressesPerceivedPain(actor);
            float researchBefore = actor.Stats.GetWorkContextMultiplier(
                BuiltInWorkTypeIds.Research);
            float ordinaryBefore = actor.Stats.GetWorkContextMultiplier(
                BuiltInWorkTypeIds.Clean);
            float combatBefore = actor.Stats.GetCombatPowerMultiplier();
            float arcaneBefore = actor.Stats.EvaluatePerformance(
                CharacterPerformanceFormulaIds.ArcanePower).Value;
            actor.Stats.ApplyWorkNeedDepletion(10f);
            float sleepAfterBaselineWork = actor.Stats.GetConditionValue(
                CharacterCondition.SLEEP,
                100f);
            float baselineFatigueLoss = 100f
                - sleepAfterBaselineWork;
            Require(baselineFatigueLoss > 0f,
                "WIM036 fatigue fixture did not produce baseline work depletion");

            runtime.SetPolicy(
                actor,
                "substance:mana-awakener",
                SubstancePolicyMode.Forbidden);
            ConsumeSubstanceCommand rejectedMana = Command(
                "drug:mana-awakener",
                "admission-reject",
                medicalContext: true);
            Require(!runtime.TryConsume(rejectedMana, out SubstanceUseResult rejected)
                    && rejected.FailureCode
                        == CharacterConsumablesFailureCode.PolicyForbidden
                    && items.GetAllStacks().Single(value =>
                        value.ItemId == "drug:mana-awakener").Quantity == 1
                    && Mathf.Approximately(
                        runtime.GetState(actor, "substance:mana-awakener")
                            .activeSeconds,
                        0f),
                "WIM036 admission rejection consumed stock or activated an effect");

            runtime.SetPolicy(
                actor,
                "substance:vitality-tonic",
                SubstancePolicyMode.MoodThreshold,
                moodThreshold: 100f);
            ConsumeSubstanceCommand vitalityCommand = Command(
                "drug:vitality-tonic",
                "vitality",
                medicalContext: false);
            float sleepBeforeVitalityConsume = actor.Stats.GetConditionValue(
                CharacterCondition.SLEEP,
                100f);
            Require(runtime.TryConsume(vitalityCommand, out SubstanceUseResult vitalityUse)
                    && vitalityUse.Success
                    && Mathf.Approximately(
                        runtime.GetState(actor, "substance:vitality-tonic")
                            .activeSeconds,
                        150f)
                    && core.Capture().activeSubstanceUsePlans.Count == 1
                    && batch.TryGetPending(vitalityCommand.OperationId.Value, out _),
                "WIM036 vitality use did not publish once behind its pending receipt");
            Require(Mathf.Approximately(
                    actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 100f),
                    sleepBeforeVitalityConsume),
                "WIM036 vitality use restored SLEEP immediately instead of only reducing later accumulation");
            int vitalityMoodCount = actor.Stats.GetMoodSnapshot().Factors.Count(
                factor => factor.Id == "substance:drug:vitality-tonic");
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.Stats.ApplyWorkNeedDepletion(10f);
            float sleepAfterTonicWork = actor.Stats.GetConditionValue(
                CharacterCondition.SLEEP,
                100f);
            float tonicFatigueLoss = 100f
                - sleepAfterTonicWork;
            Require(Mathf.Abs(
                    tonicFatigueLoss / baselineFatigueLoss - 0.88f) < 0.001f,
                $"WIM036 vitality fatigue accumulation ratio is not 0.88; "
                    + $"sleep={projectionSleepBefore:0.######}"
                    + $"->{sleepAfterBaselineWork:0.######} baselineLoss="
                    + $"{baselineFatigueLoss:0.######}; tonic=100"
                    + $"->{sleepAfterTonicWork:0.######} tonicLoss="
                    + $"{tonicFatigueLoss:0.######}; ratio="
                    + $"{tonicFatigueLoss / baselineFatigueLoss:0.######}");
            core.Tick();
            Require(core.Capture().activeSubstanceUsePlans.Count == 0
                    && !batch.TryGetPending(vitalityCommand.OperationId.Value, out _)
                    && actor.Stats.GetMoodSnapshot().Factors.Count(factor =>
                        factor.Id == "substance:drug:vitality-tonic")
                        == vitalityMoodCount
                    && vitalityMoodCount == 1,
                "WIM036 receipt acknowledgement replayed vitality effects");

            // The fatigue proof above intentionally mutates all three work-depleted
            // needs. Restore the exact pre-work fixture state so the before/active
            // projection comparison isolates substance effects rather than comparing
            // rested values with a post-work actor.
            actor.stats[CharacterCondition.SLEEP] = projectionSleepBefore;
            actor.stats[CharacterCondition.EXCRETION] =
                projectionExcretionBefore;
            actor.stats[CharacterCondition.HYGIENE] = projectionHygieneBefore;

            runtime.SetPolicy(
                actor,
                "substance:mana-awakener",
                SubstancePolicyMode.MedicalOnly);
            ConsumeSubstanceCommand manaCommand = Command(
                "drug:mana-awakener",
                "mana",
                medicalContext: true);
            Require(runtime.TryConsume(manaCommand, out SubstanceUseResult manaUse)
                    && manaUse.Success,
                "WIM036 mana use failed after its policy was authorized");
            float projectionSleepActive = actor.Stats.GetConditionValue(
                CharacterCondition.SLEEP,
                100f);
            float projectionExcretionActive = actor.Stats.GetConditionValue(
                CharacterCondition.EXCRETION,
                100f);
            float projectionHygieneActive = actor.Stats.GetConditionValue(
                CharacterCondition.HYGIENE,
                100f);
            float runtimeWorkActive = runtime.GetWorkSpeedMultiplier(actor);
            float runtimeResearchActive =
                runtime.GetResearchSpeedMultiplier(actor);
            float runtimeCombatActive = runtime.GetCombatMultiplier(actor);
            float fatigueAccumulationActive =
                runtime.GetFatigueAccumulationMultiplier(actor);
            CharacterToxicityStatus toxicityActive =
                runtime.GetToxicityStatus(actor);
            CharacterSubstanceState vitalityStateActive = runtime.GetState(
                actor,
                "substance:vitality-tonic");
            CharacterSubstanceState manaStateActive = runtime.GetState(
                actor,
                "substance:mana-awakener");
            CharacterSubstanceState dreamStateActive = runtime.GetState(
                actor,
                "substance:dreamleaf-analgesic");
            float withdrawalActive = vitalityStateActive.withdrawal
                + manaStateActive.withdrawal
                + dreamStateActive.withdrawal;
            bool painSuppressedActive =
                runtime.SuppressesPerceivedPain(actor);
            float researchActive = actor.Stats.GetWorkContextMultiplier(
                BuiltInWorkTypeIds.Research);
            float ordinaryActive = actor.Stats.GetWorkContextMultiplier(
                BuiltInWorkTypeIds.Clean);
            float combatActive = actor.Stats.GetCombatPowerMultiplier();
            float arcaneActive = actor.Stats.EvaluatePerformance(
                CharacterPerformanceFormulaIds.ArcanePower).Value;
            float researchCommonBefore = researchBefore
                / (runtimeWorkBefore * runtimeResearchBefore);
            float researchCommonActive = researchActive
                / (runtimeWorkActive * runtimeResearchActive);
            float ordinaryCommonBefore = ordinaryBefore / runtimeWorkBefore;
            float ordinaryCommonActive = ordinaryActive / runtimeWorkActive;
            float combatCommonBefore = combatBefore / runtimeCombatBefore;
            float combatCommonActive = combatActive / runtimeCombatActive;
            string projectionDetail =
                $"sleep={projectionSleepBefore:0.######}"
                + $"->{sleepAfterBaselineWork:0.######}/tonic100"
                + $"->{sleepAfterTonicWork:0.######}/projection"
                + $"->{projectionSleepActive:0.######}; "
                + $"otherNeeds=excretion{projectionExcretionBefore:0.######}"
                + $"->{projectionExcretionActive:0.######},hygiene"
                + $"{projectionHygieneBefore:0.######}"
                + $"->{projectionHygieneActive:0.######}; "
                + $"research={researchBefore:0.######}"
                + $"->{researchActive:0.######} ratio="
                + $"{researchActive / researchBefore:0.######} "
                + $"specific={runtimeResearchBefore:0.######}"
                + $"->{runtimeResearchActive:0.######} common="
                + $"{researchCommonBefore:0.######}"
                + $"->{researchCommonActive:0.######}; "
                + $"ordinary={ordinaryBefore:0.######}"
                + $"->{ordinaryActive:0.######} ratio="
                + $"{ordinaryActive / ordinaryBefore:0.######} "
                + $"genericWork={runtimeWorkBefore:0.######}"
                + $"->{runtimeWorkActive:0.######} common="
                + $"{ordinaryCommonBefore:0.######}"
                + $"->{ordinaryCommonActive:0.######}; "
                + $"combat={combatBefore:0.######}"
                + $"->{combatActive:0.######} ratio="
                + $"{combatActive / combatBefore:0.######} "
                + $"genericCombat={runtimeCombatBefore:0.######}"
                + $"->{runtimeCombatActive:0.######} common="
                + $"{combatCommonBefore:0.######}"
                + $"->{combatCommonActive:0.######}; "
                + $"arcane={arcaneBefore:0.######}"
                + $"->{arcaneActive:0.######} ratio="
                + $"{arcaneActive / arcaneBefore:0.######}; "
                + $"fatigueAccumulation={fatigueAccumulationBefore:0.######}"
                + $"->{fatigueAccumulationActive:0.######}; "
                + $"withdrawalTotal={withdrawalBefore:0.######}"
                + $"->{withdrawalActive:0.######} activeByDrug="
                + $"v{vitalityStateActive.withdrawal:0.######}/"
                + $"m{manaStateActive.withdrawal:0.######}/"
                + $"d{dreamStateActive.withdrawal:0.######}; "
                + $"toleranceActive=v{vitalityStateActive.tolerance:0.######}/"
                + $"m{manaStateActive.tolerance:0.######}/"
                + $"d{dreamStateActive.tolerance:0.######}; "
                + $"toxicity={toxicityBefore.Toxicity:0.######}"
                + $"->{toxicityActive.Toxicity:0.######} penalty="
                + $"{toxicityBefore.PerformancePenalty:0.######}"
                + $"->{toxicityActive.PerformancePenalty:0.######}; "
                + $"painSuppressed={painSuppressedBefore}"
                + $"->{painSuppressedActive}";
            Require(Mathf.Approximately(
                        projectionSleepActive,
                        projectionSleepBefore)
                    && Mathf.Approximately(
                        projectionExcretionActive,
                        projectionExcretionBefore)
                    && Mathf.Approximately(
                        projectionHygieneActive,
                        projectionHygieneBefore),
                "WIM036 projection fixture did not restore pre-work needs; "
                + projectionDetail);
            Require(Mathf.Abs(
                    researchActive / researchBefore - 1.18f) < 0.001f,
                "WIM036 mana research projection ratio is not 1.18; "
                + projectionDetail);
            Require(Mathf.Abs(
                    ordinaryActive / ordinaryBefore - 1f) < 0.001f,
                "WIM036 mana changed ordinary work projection; "
                + projectionDetail);
            Require(Mathf.Abs(
                    combatActive / combatBefore - 1.08f) < 0.001f,
                "WIM036 mana combat projection ratio is not 1.08; "
                + projectionDetail);
            Require(Mathf.Abs(
                    arcaneActive / arcaneBefore - 1f) < 0.001f,
                "WIM036 mana changed ArcanePower instead of applying combat once; "
                + projectionDetail);
            Require(Mathf.Approximately(runtimeCombatActive, 1.08f)
                    && Mathf.Abs(
                        researchCommonActive / researchCommonBefore - 1f)
                        < 0.001f
                    && Mathf.Abs(
                        ordinaryCommonActive / ordinaryCommonBefore - 1f)
                        < 0.001f
                    && Mathf.Abs(
                        combatCommonActive / combatCommonBefore - 1f)
                        < 0.001f,
                "WIM036 non-substance common projection contributions changed; "
                + projectionDetail);
            VerifyWim036CombatDamageBoundaries(
                actorId,
                world,
                combatBefore,
                combatActive,
                arcaneBefore,
                arcaneActive);

            actor.Stats.SetInjurySeverity(0.5f);
            float healthBeforeDream = actor.CurrentHealth;
            float injuryBeforeDream = actor.Stats.InjurySeverity;
            CharacterStatsProjectionContext painContext = new(
                actor,
                actor.Identity,
                actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 100f),
                actor.Stats.InjurySeverity);
            GameplayEffectProjectionResult painBefore = projection.ProjectDetailedStat(
                painContext,
                GameplayEffectTargetIds.CombatPower,
                1f);
            GameplayEffectContribution painTraitBefore = painBefore.Contributions
                .FirstOrDefault(contribution =>
                    contribution.Source.SourceId == "character-trait:204");
            Require(painTraitBefore != null && !painTraitBefore.Suppressed,
                "WIM036 fixture did not activate authored pain-conditioned trait 204; "
                + $"injury={injuryBeforeDream:0.######}; "
                    + $"painSuppressed={runtime.SuppressesPerceivedPain(actor)}; "
                    + $"projection={painBefore.Value:0.######}; "
                    + $"traitFound={painTraitBefore != null}");

            runtime.SetPolicy(
                actor,
                "substance:dreamleaf-analgesic",
                SubstancePolicyMode.MedicalOnly);
            ConsumeSubstanceCommand dreamCommand = Command(
                "drug:dreamleaf-analgesic",
                "dreamleaf",
                medicalContext: true);
            Require(runtime.TryConsume(dreamCommand, out SubstanceUseResult dreamUse)
                    && dreamUse.Success,
                "WIM036 dreamleaf use failed");
            GameplayEffectProjectionResult painSuppressed =
                projection.ProjectDetailedStat(
                    painContext,
                    GameplayEffectTargetIds.CombatPower,
                    1f);
            CharacterMoodFactorSnapshot dreamMood = actor.Stats.GetMoodSnapshot()
                .Factors.Single(factor =>
                    factor.Id == "substance:drug:dreamleaf-analgesic");
            GameplayEffectContribution painTraitSuppressed =
                painSuppressed.Contributions.FirstOrDefault(contribution =>
                    contribution.Source.SourceId == "character-trait:204");
            CharacterSubstanceState dreamStateAfterUse = runtime.GetState(
                actor,
                "substance:dreamleaf-analgesic");
            CharacterToxicityStatus toxicityAfterDream =
                runtime.GetToxicityStatus(actor);
            string painDetail =
                $"painProjection={painBefore.Value:0.######}"
                + $"->{painSuppressed.Value:0.######}; traitApplied="
                + $"{painTraitBefore.AppliedValue:0.######}->"
                + $"{(painTraitSuppressed?.AppliedValue ?? float.NaN):0.######}; "
                + $"traitSuppressed={painTraitBefore.Suppressed}->"
                + $"{painTraitSuppressed?.Suppressed}; perceivedPainSuppressed="
                + $"{painSuppressedActive}->"
                + $"{runtime.SuppressesPerceivedPain(actor)}; "
                + $"hp={healthBeforeDream:0.######}"
                + $"->{actor.CurrentHealth:0.######}; injury="
                + $"{injuryBeforeDream:0.######}"
                + $"->{actor.Stats.InjurySeverity:0.######}; "
                + $"withdrawalDream={dreamStateActive.withdrawal:0.######}"
                + $"->{dreamStateAfterUse.withdrawal:0.######}; toxicity="
                + $"{toxicityActive.Toxicity:0.######}"
                + $"->{toxicityAfterDream.Toxicity:0.######} penalty="
                + $"{toxicityActive.PerformancePenalty:0.######}"
                + $"->{toxicityAfterDream.PerformancePenalty:0.######}";
            Require(painTraitSuppressed != null
                    && painTraitSuppressed.Suppressed
                    && painTraitSuppressed.SuppressionReason
                        == "condition is inactive"
                    && Mathf.Approximately(actor.CurrentHealth, healthBeforeDream)
                    && Mathf.Approximately(
                        actor.Stats.InjurySeverity,
                        injuryBeforeDream)
                    && Mathf.Approximately(dreamMood.Value, 4f)
                    && Mathf.Approximately(
                        runtime.GetCombatMultiplier(actor),
                        1.05f),
                "WIM036 analgesia changed HP/injury state or failed pain/mood/combat semantics; "
                + painDetail);

            Require(itemIds.All(itemId => items.GetAllStacks()
                        .Where(value => value.ItemId == itemId)
                        .Sum(value => value.Quantity) == 0)
                    && core.Capture().completedOperations.Count == 3,
                "WIM036 did not consume exactly three physical doses exactly once");

            DungeonCharacterConsumablesSaveData activeSave = core.Capture();
            Require(activeSave.version == 9
                    && activeSave.activeSubstanceUsePlans.Count == 0
                    && activeSave.substanceStates.Count(state =>
                        state.activeSeconds > 0f) == 3,
                "WIM036 active effects did not use the unchanged V9 state contract");
            DungeonCharacterConsumablesSaveData roundTrippedActiveSave =
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    JsonUtility.ToJson(activeSave));
            Require(roundTrippedActiveSave != null
                    && roundTrippedActiveSave.version == 9
                    && roundTrippedActiveSave.substanceStates.Count(state =>
                        state.activeSeconds > 0f) == 3,
                "WIM036 active V9 state did not survive an actual JsonUtility round trip");
            CharacterConsumablesRuntime restored = new(
                ports,
                ports,
                ports,
                clock,
                new RandomStreamProvider(seed),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            restored.PublishRestoreCandidate(
                restored.BuildRestoreCandidate(roundTrippedActiveSave));
            CharacterConsumablesCompatibilityAdapter restoredRuntime = new(restored);
            CharacterStatsProjectionService restoredProjection =
                CreateWim036Projection(restoredRuntime, clock);
            SetWim036Projection(actor.Stats, restoredProjection);
            Require(Mathf.Approximately(
                    restoredRuntime.GetFatigueAccumulationMultiplier(actor),
                    0.88f)
                    && Mathf.Approximately(
                        restoredRuntime.GetResearchSpeedMultiplier(actor),
                        1.18f)
                    && restoredRuntime.SuppressesPerceivedPain(actor),
                "WIM036 active effects did not restore from V9 activeSeconds");

            string beforeCompletedReplay = JsonUtility.ToJson(restored.Capture());
            int stockBeforeCompletedReplay = itemIds.Sum(itemId =>
                items.GetAllStacks()
                    .Where(value => value.ItemId == itemId)
                    .Sum(value => value.Quantity));
            int moodBeforeCompletedReplay = actor.Stats.GetMoodSnapshot().Factors
                .Count(factor =>
                    factor.Id == "substance:drug:vitality-tonic");
            Require(!restoredRuntime.TryConsume(
                        vitalityCommand,
                        out SubstanceUseResult completedReplay)
                    && completedReplay.FailureCode
                        == CharacterConsumablesFailureCode.AlreadyProcessed
                    && string.Equals(
                        JsonUtility.ToJson(restored.Capture()),
                        beforeCompletedReplay,
                        StringComparison.Ordinal)
                    && itemIds.Sum(itemId => items.GetAllStacks()
                        .Where(value => value.ItemId == itemId)
                        .Sum(value => value.Quantity))
                        == stockBeforeCompletedReplay
                    && actor.Stats.GetMoodSnapshot().Factors.Count(factor =>
                        factor.Id == "substance:drug:vitality-tonic")
                        == moodBeforeCompletedReplay,
                "WIM036 restored completed operation replay changed stock, effect state, or applied effects");

            clock.Advance(151f);
            restored.Tick();
            Require(Mathf.Approximately(
                    restoredRuntime.GetFatigueAccumulationMultiplier(actor),
                    1f)
                    && Mathf.Approximately(
                        restoredRuntime.GetResearchSpeedMultiplier(actor),
                        1.18f)
                    && restoredRuntime.SuppressesPerceivedPain(actor),
                "WIM036 vitality did not expire independently at 150 seconds");
            clock.Advance(30f);
            restored.Tick();
            Require(Mathf.Approximately(
                    restoredRuntime.GetResearchSpeedMultiplier(actor),
                    1f)
                    && restoredRuntime.SuppressesPerceivedPain(actor),
                "WIM036 mana did not expire independently at 180 seconds");
            clock.Advance(60f);
            restored.Tick();
            GameplayEffectProjectionResult painReturned = restoredProjection
                .ProjectDetailedStat(
                    painContext,
                    GameplayEffectTargetIds.CombatPower,
                    1f);
            GameplayEffectContribution painTraitReturned =
                painReturned.Contributions.FirstOrDefault(contribution =>
                    contribution.Source.SourceId == "character-trait:204");
            Require(!restoredRuntime.SuppressesPerceivedPain(actor)
                    && painTraitReturned != null
                    && !painTraitReturned.Suppressed,
                "WIM036 pain condition did not return after dreamleaf expiry; "
                + $"painSuppressed={restoredRuntime.SuppressesPerceivedPain(actor)}; "
                    + $"projection={painReturned.Value:0.######}; "
                    + $"traitFound={painTraitReturned != null}; "
                    + $"traitSuppressed={painTraitReturned?.Suppressed}");

            DungeonCharacterConsumablesSaveData toxicityOnly = JsonUtility
                .FromJson<DungeonCharacterConsumablesSaveData>(
                    JsonUtility.ToJson(activeSave));
            foreach (CharacterSubstanceState state in toxicityOnly.substanceStates)
            {
                state.activeSeconds = 0f;
                state.withdrawal = 0f;
            }
            toxicityOnly.toxicityStates.Clear();
            toxicityOnly.toxicityStates.Add(new CharacterToxicityState
            {
                characterId = actorId.Value,
                toxicity = 100f,
                lastNaturalRecoveryDay = 0
            });
            CharacterConsumablesRuntime toxicityCore = new(
                ports,
                ports,
                ports,
                new FixedGameClock(),
                new RandomStreamProvider(seed),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            toxicityCore.PublishRestoreCandidate(
                toxicityCore.BuildRestoreCandidate(toxicityOnly));
            Require(Mathf.Approximately(
                    toxicityCore.GetWorkSpeedMultiplier(actorId),
                    0.8f)
                    && Mathf.Approximately(
                        toxicityCore.GetCombatMultiplier(actorId),
                        0.8f)
                    && Mathf.Approximately(
                        toxicityCore.GetFatigueAccumulationMultiplier(actorId),
                        1f)
                    && Mathf.Approximately(
                        toxicityCore.GetResearchSpeedMultiplier(actorId),
                        1f)
                    && !toxicityCore.SuppressesPerceivedPain(actorId),
                "WIM036 projection changed the existing toxicity-only 0.8/0.8 result");

            return "physical=3; admission-reject=stock-preserved/effect0; "
                + "ack=once; replay=already-processed/invariant; active=v9-json-roundtrip; "
                + "fatigue=0.88; work=research1.18/ordinary1; "
                + "combat=active-world+detached ordinary/arcane1.08 once, dreamleaf-0.03; "
                + "pain=trait204-suppressed; body=hp/injury-unchanged; "
                + "disease=not-injected; expiry=150/180/240; toxicity=0.8/0.8; "
                + projectionDetail + "; " + painDetail;
        }
        finally
        {
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            items?.Dispose();
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static void VerifyWim036CombatDamageBoundaries(
        CharacterId actorId,
        ICharacterWorldQuery world,
        float baselineCombatMultiplier,
        float activeCombatMultiplier,
        float baselineArcaneMultiplier,
        float activeArcaneMultiplier)
    {
        Require(baselineCombatMultiplier > 0f
                && activeCombatMultiplier > 0f
                && baselineArcaneMultiplier > 0f
                && activeArcaneMultiplier > 0f,
            "WIM036 combat boundary requires positive projected multipliers");
        float expectedRatio = activeCombatMultiplier / baselineCombatMultiplier;
        CombatWeaponSnapshot ordinary = CreateWim036CombatWeapon(
            "weapon:wim036-ordinary");
        CombatWeaponSnapshot arcane = CreateWim036CombatWeapon(
            "weapon:rune-blade");
        CombatStatSnapshot attacker = new(
            10f,
            10f,
            0f,
            5f,
            8f,
            5f,
            8f);
        CombatStatSnapshot defender = new(
            5f,
            5f,
            0f,
            0f,
            5f,
            0f,
            5f);
        CombatResolutionService activeWorld = new(
            Wim036CombatRandom.Instance,
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: world,
            environmentExposure:
                NoOpCharacterEnvironmentExposureCommand.Instance);

        float ActiveWorldDamage(
            CombatWeaponSnapshot weapon,
            float combatMultiplier,
            string suffix)
        {
            CombatAttackPreview preview = activeWorld.Preview(
                new CombatAttackRequest(
                    "wim036:active-world:" + suffix,
                    actorId.Value,
                    "character:wim036:combat-target",
                    attacker,
                    defender,
                    weapon,
                    1,
                    CombatFireMode.Aimed,
                    default,
                    defenderMeleeLocked: true,
                    attackPowerMultiplier: combatMultiplier));
            Require(preview.Valid && preview.DamageOnHit > 0f,
                "WIM036 active-world combat preview failed for " + suffix);
            return preview.DamageOnHit;
        }

        float activeOrdinaryBaseline = ActiveWorldDamage(
            ordinary,
            baselineCombatMultiplier,
            "ordinary-baseline");
        float activeOrdinaryBoosted = ActiveWorldDamage(
            ordinary,
            activeCombatMultiplier,
            "ordinary-active");
        float activeArcaneBaseline = ActiveWorldDamage(
            arcane,
            baselineCombatMultiplier,
            "arcane-baseline");
        float activeArcaneBoosted = ActiveWorldDamage(
            arcane,
            activeCombatMultiplier,
            "arcane-active");
        Require(Mathf.Abs(
                    activeOrdinaryBoosted / activeOrdinaryBaseline
                    - expectedRatio) < 0.001f
                && Mathf.Abs(
                    activeArcaneBoosted / activeArcaneBaseline
                    - expectedRatio) < 0.001f
                && Mathf.Abs(
                    activeArcaneBaseline / activeOrdinaryBaseline - 1f)
                    < 0.001f
                && Mathf.Abs(
                    activeArcaneBoosted / activeOrdinaryBoosted - 1f)
                    < 0.001f,
            "WIM036 active-world ordinary/arcane damage did not apply the generic combat multiplier exactly once");

        ICombatResolutionService detachedResolution =
            new CombatResolutionService(
                Wim036CombatRandom.Instance,
                evolution: null,
                overclock: null,
                environmentStatus: null,
                environmentalField: NoEnvironmentalFieldQuery.Instance,
                characters: null,
                environmentExposure:
                    NoOpCharacterEnvironmentExposureCommand.Instance);
        ICombatEquipmentRuntime detachedEquipment =
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();

        (float Damage, int AttackModifierCount, float AttackModifierTotal)
            DetachedDamage(
            CombatWeaponSnapshot weapon,
            float combatMultiplier,
            float arcaneMultiplier,
            string suffix)
        {
            OffenseBattleCombatant source = new(
                "ally:wim036:" + suffix,
                "WIM036 Attacker",
                "human",
                OffenseBattleTeam.Allies,
                new OffenseBattleStats(100f, 10f, 8f, 5f, 8f, 5f),
                100f,
                formation: OffenseFormationSlot.Front);
            source.SetCombatEquipment(
                weapon,
                Array.Empty<CombatArmorSnapshot>());
            source.SetArcanePowerMultiplier(arcaneMultiplier);
            OffenseBattleCombatant target = new(
                "enemy:wim036:" + suffix,
                "WIM036 Target",
                "human",
                OffenseBattleTeam.Enemies,
                new OffenseBattleStats(100f, 5f, 5f, 0f, 5f, 5f),
                100f,
                formation: OffenseFormationSlot.Front);
            OffenseBattleSession session = new(
                "battle:wim036:" + suffix,
                "expedition:wim036",
                "target:wim036",
                "WIM036 Combat Boundary",
                DungeonDifficulty.Normal,
                new[] { source, target },
                detachedResolution,
                detachedEquipment);
            // Session construction prepares the first actor and consumes one turn
            // from existing statuses. Add the projected combat contribution after
            // that lifecycle boundary so this preview observes the intended turn.
            source.AddStatus(new OffenseBattleStatus(
                "wim036:projected-combat:" + suffix,
                OffenseBattleStatusType.AttackModifier,
                combatMultiplier - 1f,
                1,
                actorId.Value));
            OffenseBattleStatus[] attackModifiers = source.Statuses
                .Where(status =>
                    status.Type == OffenseBattleStatusType.AttackModifier)
                .ToArray();
            CombatAttackPreview preview = session.PreviewBasicAttack(
                source,
                target);
            Require(preview.Valid && preview.DamageOnHit > 0f,
                "WIM036 detached combat preview failed for " + suffix);
            return (
                preview.DamageOnHit,
                attackModifiers.Length,
                attackModifiers.Sum(status => status.Value));
        }

        (float Damage, int AttackModifierCount, float AttackModifierTotal)
            detachedOrdinaryBaseline = DetachedDamage(
            ordinary,
            baselineCombatMultiplier,
            baselineArcaneMultiplier,
            "ordinary-baseline");
        (float Damage, int AttackModifierCount, float AttackModifierTotal)
            detachedOrdinaryBoosted = DetachedDamage(
            ordinary,
            activeCombatMultiplier,
            activeArcaneMultiplier,
            "ordinary-active");
        (float Damage, int AttackModifierCount, float AttackModifierTotal)
            detachedArcaneBaseline = DetachedDamage(
            arcane,
            baselineCombatMultiplier,
            baselineArcaneMultiplier,
            "arcane-baseline");
        (float Damage, int AttackModifierCount, float AttackModifierTotal)
            detachedArcaneBoosted = DetachedDamage(
            arcane,
            activeCombatMultiplier,
            activeArcaneMultiplier,
            "arcane-active");
        float detachedOrdinaryRatio = detachedOrdinaryBoosted.Damage
            / detachedOrdinaryBaseline.Damage;
        float detachedArcaneRatio = detachedArcaneBoosted.Damage
            / detachedArcaneBaseline.Damage;
        float detachedArcaneInputRatio = activeArcaneMultiplier
            / baselineArcaneMultiplier;
        float detachedArcaneOrdinaryBaseline = detachedArcaneBaseline.Damage
            / detachedOrdinaryBaseline.Damage;
        float detachedArcaneOrdinaryActive = detachedArcaneBoosted.Damage
            / detachedOrdinaryBoosted.Damage;
        string detachedDetail =
            $"combatInput={baselineCombatMultiplier:0.######}"
            + $"->{activeCombatMultiplier:0.######} ratio="
            + $"{expectedRatio:0.######}; arcaneInput="
            + $"{baselineArcaneMultiplier:0.######}"
            + $"->{activeArcaneMultiplier:0.######} ratio="
            + $"{detachedArcaneInputRatio:0.######}; "
            + $"ordinaryDamage={detachedOrdinaryBaseline.Damage:0.######}"
            + $"->{detachedOrdinaryBoosted.Damage:0.######} ratio="
            + $"{detachedOrdinaryRatio:0.######}; arcaneDamage="
            + $"{detachedArcaneBaseline.Damage:0.######}"
            + $"->{detachedArcaneBoosted.Damage:0.######} ratio="
            + $"{detachedArcaneRatio:0.######}; arcaneOverOrdinary="
            + $"{detachedArcaneOrdinaryBaseline:0.######}"
            + $"->{detachedArcaneOrdinaryActive:0.######}; "
            + $"attackModifier=ordinary["
            + $"{detachedOrdinaryBaseline.AttackModifierCount}/"
            + $"{detachedOrdinaryBaseline.AttackModifierTotal:0.######}"
            + $"->{detachedOrdinaryBoosted.AttackModifierCount}/"
            + $"{detachedOrdinaryBoosted.AttackModifierTotal:0.######}],"
            + $"arcane[{detachedArcaneBaseline.AttackModifierCount}/"
            + $"{detachedArcaneBaseline.AttackModifierTotal:0.######}"
            + $"->{detachedArcaneBoosted.AttackModifierCount}/"
            + $"{detachedArcaneBoosted.AttackModifierTotal:0.######}]";
        Require(detachedOrdinaryBaseline.AttackModifierCount == 1
                && detachedOrdinaryBoosted.AttackModifierCount == 1
                && detachedArcaneBaseline.AttackModifierCount == 1
                && detachedArcaneBoosted.AttackModifierCount == 1
                && Mathf.Abs(
                    1f + detachedOrdinaryBaseline.AttackModifierTotal
                    - baselineCombatMultiplier) < 0.001f
                && Mathf.Abs(
                    1f + detachedOrdinaryBoosted.AttackModifierTotal
                    - activeCombatMultiplier) < 0.001f
                && Mathf.Abs(
                    1f + detachedArcaneBaseline.AttackModifierTotal
                    - baselineCombatMultiplier) < 0.001f
                && Mathf.Abs(
                    1f + detachedArcaneBoosted.AttackModifierTotal
                    - activeCombatMultiplier) < 0.001f,
            "WIM036 detached preview lost or changed its projected combat contribution; "
            + detachedDetail);
        Require(Mathf.Abs(
                    detachedOrdinaryRatio - expectedRatio) < 0.001f,
            "WIM036 detached ordinary damage did not apply generic combat exactly once; "
            + detachedDetail);
        Require(Mathf.Abs(
                    detachedArcaneRatio - expectedRatio) < 0.001f,
            "WIM036 detached arcane damage did not apply generic combat exactly once; "
            + detachedDetail);
        Require(Mathf.Abs(detachedArcaneInputRatio - 1f) < 0.001f,
            "WIM036 mana changed the detached ArcanePower input; "
            + detachedDetail);
        Require(Mathf.Abs(
                    detachedArcaneOrdinaryActive
                    / detachedArcaneOrdinaryBaseline - 1f) < 0.001f,
            "WIM036 detached arcane damage gained an additional combat contribution; "
            + detachedDetail);
    }

    private static CombatWeaponSnapshot CreateWim036CombatWeapon(
        string definitionId) => new(
        definitionId,
        string.Empty,
        CombatEquipmentKind.MeleeWeapon,
        new MeleeStrikeVerb
        {
            attackTime = 1f,
            baseDamage = 10f,
            penetration = 0f,
            damageType = CombatDamageType.Slash,
            tracking = 0.05f
        },
        new[]
        {
            new CombatRangeProfile
            {
                band = CombatRangeBand.Contact,
                accuracyMultiplier = 1f,
                damageMultiplier = 1f
            }
        },
        1,
        CombatEquipmentQuality.Normal,
        string.Empty,
        0,
        0,
        0f,
        true,
        false,
        false);

    private static CharacterStatsProjectionService CreateWim036Projection(
        ICharacterSubstanceRuntime substances,
        IGameClock clock) => new(
        Wim036StaffDiscontent.Instance,
        Wim036MetaProgression.Instance,
        NoCharacterDeprivationBoundary.Instance,
        substances,
        NeutralCharacterEnvironmentStatusQuery.Instance,
        NeutralExternalCombatInfluenceQuery.Instance,
        NeutralContentWorkDelayQuery.Instance,
        NeutralDiseaseSymptomEffectQuery.Instance,
        NeutralCharacterCombatSpecialStatusQuery.Instance,
        NeutralCombatEquipmentBurdenQuery.Instance,
        CharacterAiEditorTestDependencies.GameCalendar,
        new CharacterDerivedStatsSnapshotProjector(
            CharacterAiEditorTestDependencies.ContentDefinitions,
            Wim036EmptyEquipmentEffects.Instance,
            Wim036EmptyTransientEffects.Instance,
            new ExtremeTraitRuntime(new CharacterIdentityStateStore()),
            clock),
        performance: CharacterAiEditorTestDependencies.NeutralPerformance);

    private static void SetWim036Projection(
        CharacterStats stats,
        CharacterStatsProjectionService projection)
    {
        System.Reflection.FieldInfo field = typeof(CharacterStats).GetField(
            "projectionService",
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
        Require(field != null && stats != null && projection != null,
            "WIM036 could not bind the focused stats projection");
        field.SetValue(stats, projection);
    }

    private static string VerifyWim028OverdosePolicy()
    {
        GameObject actorObject = new("WIM028_OverdoseActor");
        WorldItemStackRuntime items = null;
        CharacterActor actor = null;
        ICharacterAiWorldRegistry world =
            CharacterAiEditorTestDependencies.WorldRegistry;
        try
        {
            actor = CreateWim028Actor(
                actorObject,
                "character:wim028:overdose");
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);
            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out _,
                out _,
                out ItemQuantityReservationService reservations,
                out IReservedItemTransferService reservedTransfers,
                out IReservedPhysicalItemBatchDispositionService reservedBatch,
                out IPhysicalItemBatchDispositionService batch);
            IItemDefinitionCatalog catalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            CharacterConsumablesApplicationPorts ports = new(
                catalog,
                items,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: batch);
            const float firstOverdoseChance = 0.06f;
            const float addictionRollChance = 0.028f;
            const float secondOverdoseChance = 0.0602f;
            int seed = Enumerable.Range(1, 100000).First(candidate =>
            {
                DungeonStory.Foundation.IRandomStream stream =
                    new RandomStreamProvider(candidate)
                        .Get("character-consumables");
                bool first = stream.Chance(firstOverdoseChance);
                _ = stream.Chance(addictionRollChance);
                bool second = stream.Chance(secondOverdoseChance);
                return first && !second;
            });
            CharacterConsumablesRuntime core = new(
                ports,
                ports,
                ports,
                new FixedGameClock(),
                new RandomStreamProvider(seed),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            CharacterId actorId = CharacterPersistentIdentity.Require(actor);
            core.SetSubstancePolicy(
                actorId,
                "substance:blood-stimulant",
                SubstancePolicyMode.MedicalOnly,
                0f,
                0);
            Require(items.SpawnItemAt(
                    "drug:blood-stimulant",
                    2,
                    Vector2Int.zero,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 2,
                "WIM028 overdose fixture did not spawn two physical doses");
            WorldItemStackSnapshot stack = items.GetAllStacks().Single(value =>
                value.ItemId == "drug:blood-stimulant");
            ConsumableOperationId overdoseOperation = new(
                "consumable-operation:wim028:overdose");
            ConsumeSubstanceByIdCommand overdose = new(
                overdoseOperation,
                actorId,
                new ConsumableItemDefinitionId("drug:blood-stimulant"),
                new ItemStackId(stack.StackId),
                medicalContext: true,
                combatContext: false);
            float healthBefore = actor.CurrentHealth;
            Require(core.TryConsumeSubstance(overdose, out CharacterConsumablesSubstanceResult first)
                    && first.Success
                    && first.Overdosed
                    && Mathf.Approximately(
                        core.GetToxicityStatus(actorId).Toxicity,
                        CharacterToxicityPolicy.OverdoseGain)
                    && actor.CurrentHealth < healthBefore,
                "confirmed overdose did not preserve immediate damage and add exactly 30 toxicity");
            int afterFirst = items.GetAllStacks()
                .Where(value => value.ItemId == "drug:blood-stimulant")
                .Sum(value => value.Quantity);
            Require(!core.TryConsumeSubstance(overdose, out CharacterConsumablesSubstanceResult duplicate)
                    && duplicate.FailureCode
                        == CharacterConsumablesFailureCode.AlreadyProcessed
                    && afterFirst == 1
                    && Mathf.Approximately(
                        core.GetToxicityStatus(actorId).Toxicity,
                        30f),
                "duplicate overdose operation changed toxicity or physical stock");
            ConsumeSubstanceByIdCommand normal = new(
                new ConsumableOperationId(
                    "consumable-operation:wim028:normal-dose"),
                actorId,
                new ConsumableItemDefinitionId("drug:blood-stimulant"),
                new ItemStackId(stack.StackId),
                medicalContext: true,
                combatContext: false);
            Require(core.TryConsumeSubstance(normal, out CharacterConsumablesSubstanceResult second)
                    && second.Success
                    && !second.Overdosed
                    && Mathf.Approximately(
                        core.GetToxicityStatus(actorId).Toxicity,
                        30f),
                "normal substance use changed toxicity burden");

            DungeonCharacterConsumablesSaveData capped = core.Capture();
            CharacterToxicityState toxicity = capped.toxicityStates.Single();
            toxicity.toxicity = 100f;
            toxicity.lastNaturalRecoveryDay = 0;
            foreach (CharacterSubstanceState substance in capped.substanceStates)
            {
                substance.activeSeconds = 0f;
                substance.withdrawal = 0f;
            }
            core.PublishRestoreCandidate(core.BuildRestoreCandidate(capped));
            CharacterToxicityStatus cappedStatus = core.GetToxicityStatus(actorId);
            float cappedWork = core.GetWorkSpeedMultiplier(actorId);
            float cappedCombat = core.GetCombatMultiplier(actorId);
            Require(Mathf.Approximately(cappedStatus.Toxicity, 100f)
                    && Mathf.Approximately(cappedStatus.PerformancePenalty, 0.2f)
                    && Mathf.Approximately(cappedWork, 0.8f)
                    && Mathf.Approximately(cappedCombat, 0.8f),
                "toxicity-only tuple differs: toxicity="
                + cappedStatus.Toxicity.ToString("0.###")
                + "; penalty="
                + cappedStatus.PerformancePenalty.ToString("0.###")
                + "; work=" + cappedWork.ToString("0.###")
                + "; combat=" + cappedCombat.ToString("0.###"));
            string treatment =
                CharacterSummaryHealthStatusTextFormatter.ToxicityTreatment(cappedStatus);
            string row = CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Consumables.ToxicityRow",
                cappedStatus.Toxicity,
                cappedStatus.PerformancePenalty * 100f,
                treatment);
            Require(!string.IsNullOrWhiteSpace(row)
                    && row.Contains("100")
                    && row.Contains("20"),
                "health UI did not project toxicity and its actual post-clamp contribution");
            core.ProcessOperatingDay(1);
            Require(Mathf.Approximately(core.GetToxicityStatus(actorId).Toxicity, 70f),
                "first operating day did not recover 30 toxicity");
            core.ProcessOperatingDay(1);
            Require(Mathf.Approximately(core.GetToxicityStatus(actorId).Toxicity, 70f),
                "duplicate operating day recovered toxicity twice");
            core.ProcessOperatingDay(3);
            Require(Mathf.Approximately(core.GetToxicityStatus(actorId).Toxicity, 10f),
                "elapsed operating days did not recover 30 toxicity per day");
            core.ProcessOperatingDay(4);
            Require(Mathf.Approximately(core.GetToxicityStatus(actorId).Toxicity, 0f),
                "natural toxicity recovery did not clamp at zero");

            DungeonCharacterConsumablesSaveData invalid = core.Capture();
            invalid.toxicityStates.Single().toxicity = float.NaN;
            string beforeInvalid = JsonUtility.ToJson(core.Capture());
            RequireThrows<InvalidOperationException>(
                () => core.BuildRestoreCandidate(invalid),
                "toxicity restore accepted NaN");
            Require(string.Equals(
                    beforeInvalid,
                    JsonUtility.ToJson(core.Capture()),
                    StringComparison.Ordinal),
                "invalid toxicity restore mutated live state");
            return $"seed={seed}; overdose=+30 once; normal=+0; cap=100; day=30; multipliers=0.8/0.8; ui=actual-contribution; invalid=no-mutation";
        }
        finally
        {
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            items?.Dispose();
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string VerifyWim028TreatBridgeRetry()
    {
        GameObject actorObject = new("WIM028_TreatActor");
        GameObject facilityObject = new("WIM028_TreatFacility");
        GameObject otherFacilityObject = new("WIM028_OtherFacility");
        GameObject warehouseObject = new("WIM062_TreatmentWarehouse");
        BuildingSO warehouseDefinition = null;
        Wim062Warehouse warehouse = null;
        WorldItemStackRuntime items = null;
        CharacterActor actor = null;
        BuildableObject facility = null;
        BuildableObject otherFacility = null;
        SurvivalFoodRuntime food = null;
        SurvivalFoodRuntime restoredFood = null;
        ICharacterAiWorldRegistry world =
            CharacterAiEditorTestDependencies.WorldRegistry;
        world.TryGetGrid(out Grid previousGrid);
        Grid fixtureGrid = new(20, 4);
        for (int y = 0; y < fixtureGrid.height; y++)
            for (int x = 0; x < fixtureGrid.width; x++)
                fixtureGrid.SetAreaType(new Vector2Int(x, y), GridCellAreaType.ExteriorPath);
        Wim062GridProvider gridProvider = new(fixtureGrid);
        try
        {
            world.SetGrid(fixtureGrid);
            actorObject.AddComponent<AbilityWork>();
            actor = CreateWim028Actor(
                actorObject,
                "character:wim028:treat");
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);
            actor.GetComponent<CharacterLifecycle>().ConstructCharacterLifecycle(gridProvider);
            actor.transform.position = fixtureGrid.GetWorldPos(new Vector2Int(0, 1));
            BuildingSO medical = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Medical/M01_응급처치대.asset");
            Require(medical != null
                    && medical.GetAbility<BuildingMedicalAbility>() != null
                    && medical.GetAbility<BuildingServiceHubAbility>() != null,
                "authored M01 medical/service abilities are missing");
            facility = facilityObject.AddComponent<BuildableObject>();
            facility.RestorePersistentIdentity(new BuildingInstanceId("building:wim062:a-medical"));
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.SetGrid(fixtureGrid);
            facility.Initialization(medical, new Vector2Int(7, 1));
            world.RegisterBuilding(facility);
            otherFacility = otherFacilityObject.AddComponent<BuildableObject>();
            otherFacility.RestorePersistentIdentity(new BuildingInstanceId("building:wim062:b-medical"));
            CharacterAiEditorTestDependencies.Inject(otherFacility);
            otherFacility.SetGrid(fixtureGrid);
            otherFacility.Initialization(medical, new Vector2Int(16, 1));
            world.RegisterBuilding(otherFacility);

            warehouseDefinition = ScriptableObject.CreateInstance<BuildingSO>();
            warehouseDefinition.id = 99062;
            warehouseDefinition.objectName = "WIM062 treatment stock fixture";
            warehouseDefinition.width = 1;
            warehouseDefinition.height = 1;
            warehouseDefinition.category = BuildingCategory.Shop;
            warehouse = warehouseObject.AddComponent<Wim062Warehouse>();
            CharacterAiEditorTestDependencies.Inject(warehouse);
            warehouse.SetGrid(fixtureGrid);
            warehouse.Initialization(warehouseDefinition, new Vector2Int(2, 1));
            world.RegisterBuilding(warehouse);
            Require(fixtureGrid.RegisterOccupant(warehouse,
                    GridLayer.Building, warehouse.buildPoses, false),
                "medical warehouse was not registered on the physical grid");

            items = PhysicalItemDebugScenarios.CreateDeliveryRuntimeForCrossDomainFixture(
                gridProvider,
                out WorldItemRepository repository,
                out ItemQuantityReservationService reservations,
                out IReservedItemTransferService reservedTransfers,
                out IReservedPhysicalItemBatchDispositionService reservedBatch,
                out IPhysicalItemBatchDispositionService batch,
                out FacilityBufferDestinationClaimRegistry destinationClaims);
            IStockQuery physicalStock = new PhysicalStockQuery(
                repository, items.CatalogProvider, items.MassQuery);
            warehouse.Inventory.BindPhysicalStock(physicalStock,
                warehouse.RequirePersistentInstanceId(),
                CharacterAiEditorTestDependencies.AuthoredGameplay);
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? actorObject.AddComponent<CharacterCarryInventory>();
            carry.Configure(items.CatalogProvider, items.MassQuery,
                items.HaulingSettingsProvider, new CharacterCarryInventoryRegistry());
            FacilityBufferPhysicalOccupancyQuery treatmentOccupancy =
                new(repository, items.MassQuery, reservations);
            FacilityBufferMassAdmissionService capacities = new(
                destinationClaims, treatmentOccupancy, items.MassQuery);
            FacilityBufferDestinationLifecycleService lifecycle = new(
                destinationClaims, destinationClaims, capacities, capacities);
            CharacterConsumablesInputOwnerProjection owners =
                CharacterConsumablesInputOwnerAuthority.BuildProjection(
                    new[] { facility, otherFacility }.Select(value =>
                        new CharacterConsumablesInputOwnerDescriptor(
                            CharacterConsumablesInputKind.MedicalTreatment,
                            value.RequirePersistentInstanceId().Value,
                            value.centerPos,
                            "medicine:antidote")), items.MassQuery);
            Require(lifecycle.TryReplaceOwnedAuthorities(
                    CharacterConsumablesInputOwnerAuthority.OwnerDomain,
                    owners.Claims, owners.Profiles, out string ownerFailure),
                "medical input-owner publication failed: " + ownerFailure);
            items.Start();
            FailFirstAcknowledgeBatchDisposition failFirst = new(batch);
            IPhysicalFacilityItemSinkGateway treatmentSinks =
                new PhysicalFacilityItemSinkGateway(physicalStock, failFirst);
            Require(items.MassQuery is IPackagedLotDefinitionQuery,
                "treatment fixture has no authored packaging query");
            IPackagedLotTareDispositionService treatmentTare =
                new PackagedLotTareDispositionService(
                    (IPackagedLotDefinitionQuery)items.MassQuery,
                    new PackagedLotTareOutputGateway(items));
            IItemDefinitionCatalog catalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            GameEventBus events = new();
            CharacterConsumablesApplicationPorts ports = new(
                catalog,
                items,
                world,
                events,
                EmptyCombatCommands.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance,
                quantityReservations: reservations,
                reservedTransfers: reservedTransfers,
                reservedBatchDispositions: reservedBatch,
                batchDispositions: failFirst,
                tareDispositions: treatmentTare);
            CharacterConsumablesRuntime core = new(
                ports,
                ports,
                ports,
                new FixedGameClock(),
                new RandomStreamProvider(28028),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            CharacterId actorId = CharacterPersistentIdentity.Require(actor);
            DungeonCharacterConsumablesSaveData toxic = core.Capture();
            toxic.toxicityStates.Add(new CharacterToxicityState
            {
                characterId = actorId.Value,
                toxicity = 60f,
                lastNaturalRecoveryDay = 1
            });
            core.PublishRestoreCandidate(core.BuildRestoreCandidate(toxic));
            RetryServiceSessions sessions = new();
            food = CreateWim028Food(items, catalog, events, world, sessions, core,
                gridProvider, physicalStock, treatmentSinks, treatmentTare,
                capacities, treatmentOccupancy, destinationClaims);
            VerifyWim062SupplyObservation(food, items, core, sessions, actorId, facility,
                "medicine:antidote", SurvivalTreatmentKind.Detox,
                SurvivalTreatmentSupplyState.StockMissing);
            Require(items.SpawnItemAt(
                    "medicine:antidote",
                    1,
                    warehouse.centerPos,
                    WorldItemStackState.Stored,
                    WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    out int spawned)
                && spawned == 1,
                "WIM028 Treat fixture did not spawn one authored antidote");
            SurvivalWorkExecutionHandler handler = CreateWim028WorkHandler(food);
            float healthBefore = actor.CurrentHealth;
            Require(catalog.TryGet(
                    (ItemDefinitionId)"medicine:antidote",
                    out ItemDefinitionSO antidote)
                    && antidote.TryGetFeature(out MedicineItemFeature medicine)
                    && Mathf.Approximately(
                        medicine.detoxReduction,
                        CharacterToxicityPolicy.NaturalRecoveryPerDay)
                    && !medicine.supportsInjuryTreatment
                    && core.GetToxicityStatus(actorId).TreatmentAvailable,
                "authored antidote is not the exact available DetoxReduction 30 treatment");

            VerifyWim062SupplyObservation(food, items, core, sessions, actorId, facility,
                "medicine:antidote", SurvivalTreatmentKind.Detox,
                SurvivalTreatmentSupplyState.AwaitingRequest);

            // Use real fixture-owned admission authority, not a fabricated UI failure.
            FacilityBufferCapacityProfile[] originalProfiles = capacities.CaptureProfiles().ToArray();
            FacilityBufferCapacityProfile[] constrainedProfiles = originalProfiles.Select(profile =>
                new FacilityBufferCapacityProfile(profile.DestinationId, profile.DropPosition,
                    profile.OwnerDomain, profile.OwnerOperationId, profile.OwnerFacilityId,
                    new PhysicalMassGrams(1L), profile.CapacityRevision + 1,
                    profile.AuthorityDigest)).ToArray();
            Require(capacities.TryReplaceOwnedProfiles(
                    CharacterConsumablesInputOwnerAuthority.OwnerDomain, constrainedProfiles,
                    out _, out string capacityFailure),
                "failed to apply controlled one-gram capacity: " + capacityFailure);
            try
            {
                VerifyWim062SupplyObservation(food, items, core, sessions, actorId, facility,
                    "medicine:antidote", SurvivalTreatmentKind.Detox,
                    SurvivalTreatmentSupplyState.CapacityUnavailable);
            }
            finally
            {
                Require(capacities.TryReplaceOwnedProfiles(
                        CharacterConsumablesInputOwnerAuthority.OwnerDomain, originalProfiles,
                        out _, out string capacityRestoreFailure),
                    "fixture capacity restoration failed: " + capacityRestoreFailure);
            }

            WorkExecutionResult cancelled = ExecuteWim028Treat(
                handler,
                actor,
                facility,
                runId: 2800,
                canContinue: false);
            Require(!cancelled.CompletedSuccessfully
                    && CountItem(items, "medicine:antidote") == 1
                    && Mathf.Approximately(
                        core.GetToxicityStatus(actorId).Toxicity,
                        60f)
                    && sessions.ActiveSessions.Count == 0,
                "pre-completion Treat cancellation consumed medicine, changed toxicity, or opened a session");

            WorkExecutionResult warehouseOnly = ExecuteWim028Treat(
                handler,
                actor,
                facility,
                runId: 2799,
                canContinue: true);
            Require(!warehouseOnly.CompletedSuccessfully
                    && CountItem(items, "medicine:antidote") == 1
                    && items.GetAllStacks().Any(stack =>
                        stack.ItemId == "medicine:antidote"
                        && stack.State == WorldItemStackState.Stored
                        && stack.Quantity == 1)
                    && Mathf.Approximately(
                        core.GetToxicityStatus(actorId).Toxicity,
                        60f)
                    && Mathf.Approximately(actor.CurrentHealth, healthBefore)
                    && sessions.CompletedSessionCount == 0
                    && sessions.ActiveSessions.Count == 0
                    && core.Capture().activeDetoxTreatmentPlans.Count == 0,
                "warehouse-only Treat consumed remote medicine, applied an effect, or completed billing before physical arrival");

            VerifyWim062SupplyObservation(food, items, core, sessions, actorId, facility,
                "medicine:antidote", SurvivalTreatmentKind.Detox,
                SurvivalTreatmentSupplyState.Requested);
            DeliverWim062TreatmentStock(items, reservations, actor, carry, facility,
                afterPickup: () => VerifyWim062SupplyObservation(food, items, core, sessions,
                    actorId, facility, "medicine:antidote", SurvivalTreatmentKind.Detox,
                    SurvivalTreatmentSupplyState.InTransit));
            VerifyWim062SupplyObservation(food, items, core, sessions, actorId, facility,
                "medicine:antidote", SurvivalTreatmentKind.Detox,
                SurvivalTreatmentSupplyState.Ready);

            WorkExecutionResult pending = ExecuteWim028Treat(
                handler,
                actor,
                facility,
                runId: 2801,
                canContinue: true);
            DungeonCharacterConsumablesSaveData pendingSave = core.Capture();
            ServiceRoomsSaveData sessionSave = sessions.Capture();
            Require(!pending.CompletedSuccessfully
                    && CountItem(items, "medicine:antidote") == 0
                    && Mathf.Approximately(
                        core.GetToxicityStatus(actorId).Toxicity,
                        30f)
                    && pendingSave.activeDetoxTreatmentPlans.Count == 1
                    && pendingSave.activeDetoxTreatmentPlans[0].phase
                        == CharacterDetoxTreatmentPlanPhase.EffectsPublished
                    && sessionSave.sessions.Count == 1
                    && string.Equals(
                        pendingSave.activeDetoxTreatmentPlans[0].operationId,
                        "consumable-operation:detox:"
                        + sessionSave.sessions[0].sessionId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        pendingSave.activeDetoxTreatmentPlans[0].characterId,
                        sessionSave.sessions[0].actorId,
                        StringComparison.Ordinal),
                "committed Treat did not retain the exact patient/service operation through its pending receipt");
            VerifyWim062SupplyObservation(food, items, core, sessions, actorId, facility,
                "medicine:antidote", SurvivalTreatmentKind.Detox,
                SurvivalTreatmentSupplyState.Processing);
            DungeonPhysicalItemSaveData physicalPending = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(items.Capture()));
            PhysicalItemBatchDispositionSaveData physicalReceipt =
                physicalPending.pendingBatchDispositions.Single(value =>
                    value.operationId == pendingSave.activeDetoxTreatmentPlans[0].operationId);
            Require(physicalReceipt.quantity == 1
                    && physicalReceipt.commitId == pendingSave.activeDetoxTreatmentPlans[0].physicalCommitId
                    && physicalReceipt.inputMassGrams > 0,
                "pending detox receipt was not captured by the physical-item save authority");
            items.Restore(physicalPending);
            bool otherFacilityAccepted = core.TryApplyDetoxTreatment(
                new ConsumableOperationId(
                    "consumable-operation:wim028:other-facility"),
                actorId,
                otherFacility.RequirePersistentInstanceId(),
                out CharacterDetoxTreatmentResult otherFacilityResult);
            Require(!otherFacilityAccepted
                    && otherFacilityResult.FailureCode
                        == CharacterConsumablesFailureCode.DeliveryPending
                    && core.Capture().activeDetoxTreatmentPlans.Count == 1,
                "a second facility created a competing detox plan for the same patient");

            CharacterConsumablesRuntime restoredCore = new(
                ports,
                ports,
                ports,
                new FixedGameClock(),
                new RandomStreamProvider(28029),
                new DungeonRuntimeAggregateRootStore(),
                DefaultCharacterNeedBalanceRuntime.Instance);
            restoredCore.PublishRestoreCandidate(
                restoredCore.BuildRestoreCandidate(pendingSave));
            RetryServiceSessions restoredSessions = new(sessionSave);
            restoredFood = CreateWim028Food(
                items,
                catalog,
                events,
                world,
                restoredSessions,
                restoredCore,
                gridProvider,
                physicalStock,
                treatmentSinks,
                treatmentTare,
                capacities, treatmentOccupancy, destinationClaims);
            SurvivalWorkExecutionHandler restoredHandler =
                CreateWim028WorkHandler(restoredFood);
            WorkExecutionResult resumed = ExecuteWim028Treat(
                restoredHandler,
                actor,
                facility,
                runId: 3801,
                canContinue: true);
            Require(resumed.CompletedSuccessfully
                    && resumed.CompletionEffectsAlreadyApplied
                    && CountItem(items, "medicine:antidote") == 0
                    && Mathf.Approximately(
                        restoredCore.GetToxicityStatus(actorId).Toxicity,
                        30f)
                    && restoredCore.Capture().activeDetoxTreatmentPlans.Count == 0
                    && restoredSessions.ActiveSessions.Count == 0
                    && Mathf.Approximately(actor.CurrentHealth, healthBefore),
                "saved Treat retry changed toxicity twice, consumed again, left ownership active, or applied free HP");

            Require(items.SpawnItemAt(
                    "medicine:antidote",
                    1,
                    warehouse.centerPos,
                    WorldItemStackState.Stored,
                    WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    out spawned)
                && spawned == 1,
                "WIM028 live replay fixture could not stage a sentinel antidote");
            WorkExecutionResult sameWorkReplay = ExecuteWim028Treat(
                restoredHandler,
                actor,
                facility,
                runId: 3801,
                canContinue: true);
            Require(sameWorkReplay.CompletedSuccessfully
                    && sameWorkReplay.CompletionEffectsAlreadyApplied
                    && CountItem(items, "medicine:antidote") == 1
                    && Mathf.Approximately(
                        restoredCore.GetToxicityStatus(actorId).Toxicity,
                        30f)
                    && restoredSessions.ActiveSessions.Count == 0,
                "same live Treat work replay created a new service operation or consumed a second medicine");
            DungeonSurvivalSaveData foodSave = restoredFood.Capture();
            restoredFood.PublishRestoreCandidate(
                restoredFood.BuildRestoreCandidate(foodSave));
            WorkExecutionResult secondDeliveryWait = ExecuteWim028Treat(
                restoredHandler, actor, facility, runId: 4801, canContinue: true);
            Require(!secondDeliveryWait.CompletedSuccessfully
                    && CountItem(items, "medicine:antidote") == 1
                    && Mathf.Approximately(restoredCore.GetToxicityStatus(actorId).Toxicity, 30f),
                "new post-restore treatment bypassed its second physical delivery");
            DeliverWim062TreatmentStock(items, reservations, actor, carry, facility);
            WorkExecutionResult postRestoreNewWork = ExecuteWim028Treat(
                restoredHandler,
                actor,
                facility,
                runId: 3801,
                canContinue: true);
            Require(postRestoreNewWork.CompletedSuccessfully
                    && CountItem(items, "medicine:antidote") == 0
                    && Mathf.Approximately(
                        restoredCore.GetToxicityStatus(actorId).Toxicity,
                        0f)
                    && restoredSessions.ActiveSessions.Count == 0,
                "successful restore retained ephemeral RunId correlation or blocked a new persisted service operation");
            ItemDefinitionSO ordinaryMedicine = catalog.All
                .Where(value => value != null
                    && value.StockCategory == StockCategory.Medicine
                    && value.TryGetFeature(out MedicineItemFeature feature)
                    && feature.supportsInjuryTreatment)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .First();
            CharacterConsumablesInputOwnerProjection normalOwners =
                CharacterConsumablesInputOwnerAuthority.BuildProjection(new[]
                {
                    new CharacterConsumablesInputOwnerDescriptor(
                        CharacterConsumablesInputKind.MedicalTreatment,
                        facility.RequirePersistentInstanceId().Value,
                        facility.centerPos, "medicine:antidote"),
                    new CharacterConsumablesInputOwnerDescriptor(
                        CharacterConsumablesInputKind.MedicalTreatment,
                        otherFacility.RequirePersistentInstanceId().Value,
                        otherFacility.centerPos, "medicine:antidote"),
                    new CharacterConsumablesInputOwnerDescriptor(
                        CharacterConsumablesInputKind.MedicalTreatment,
                        facility.RequirePersistentInstanceId().Value,
                        facility.centerPos, ordinaryMedicine.ItemId)
                }, items.MassQuery);
            Require(lifecycle.TryReplaceOwnedAuthorities(
                    CharacterConsumablesInputOwnerAuthority.OwnerDomain,
                    normalOwners.Claims, normalOwners.Profiles, out string normalOwnerFailure),
                "normal-treatment owner publication failed: " + normalOwnerFailure);
            Require(items.SpawnItemAt(ordinaryMedicine.ItemId, 1,
                    warehouse.centerPos, WorldItemStackState.Stored,
                    WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    out int ordinarySpawned) && ordinarySpawned == 1
                    && restoredFood.GetStoredStockCount(StockCategory.Medicine) == 1,
                "normal-treatment fixture is not backed by a real visible warehouse medicine");
            DungeonSurvivalSaveData ordinaryState = restoredFood.Capture();
            ordinaryState.health.Add(new SurvivalHealthSaveData
            {
                persistentId = actorId.Value,
                state = SurvivalHealthState.Sick,
                severity = 0.8f,
                remainingSeconds = 360f,
                source = "wim062-controlled-treatment-issue"
            });
            restoredFood.PublishRestoreCandidate(restoredFood.BuildRestoreCandidate(ordinaryState));
            int completionsBeforeOrdinary = restoredSessions.CompletedSessionCount;
            WorkExecutionResult ordinaryWait = ExecuteWim028Treat(
                restoredHandler, actor, facility, runId: 6201, canContinue: true);
            Require(!ordinaryWait.CompletedSuccessfully
                    && CountItem(items, ordinaryMedicine.ItemId) == 1
                    && Mathf.Approximately(restoredFood.Capture().health.Single(
                        value => value.persistentId == actorId.Value).severity, 0.8f)
                    && restoredSessions.CompletedSessionCount == completionsBeforeOrdinary,
                "ordinary treatment bypassed physical delivery or applied early health/billing effects");
            VerifyWim062SupplyObservation(restoredFood, items, restoredCore, restoredSessions,
                actorId, facility, ordinaryMedicine.ItemId, SurvivalTreatmentKind.Standard,
                SurvivalTreatmentSupplyState.Requested);
            DeliverWim062TreatmentStock(items, reservations, actor, carry, facility,
                ordinaryMedicine.ItemId,
                afterPickup: () => VerifyWim062SupplyObservation(restoredFood, items,
                    restoredCore, restoredSessions, actorId, facility, ordinaryMedicine.ItemId,
                    SurvivalTreatmentKind.Standard, SurvivalTreatmentSupplyState.InTransit));
            VerifyWim062SupplyObservation(restoredFood, items, restoredCore, restoredSessions,
                actorId, facility, ordinaryMedicine.ItemId, SurvivalTreatmentKind.Standard,
                SurvivalTreatmentSupplyState.Ready);
            failFirst.FailNextAcknowledge();
            WorkExecutionResult ordinaryPending = ExecuteWim028Treat(
                restoredHandler, actor, facility, runId: 6201, canContinue: true);
            float expectedSeverity = Mathf.Max(0f,
                0.8f - medical.GetAbility<BuildingMedicalAbility>().severityReduction);
            DungeonSurvivalSaveData normalPending = JsonUtility.FromJson<DungeonSurvivalSaveData>(
                JsonUtility.ToJson(restoredFood.Capture()));
            Require(!ordinaryPending.CompletedSuccessfully
                    && CountItem(items, ordinaryMedicine.ItemId) == 0
                    && Mathf.Approximately(normalPending.health.Single(
                        value => value.persistentId == actorId.Value).severity, expectedSeverity)
                    && normalPending.activeTreatmentPlans.Count == 1
                    && normalPending.activeTreatmentPlans[0].phase
                        == SurvivalTreatmentPlanPhase.ServiceCompleted
                    && restoredSessions.CompletedSessionCount == completionsBeforeOrdinary + 1,
                "ordinary medicine acknowledgment failure lost its committed treatment owner or repeated effects");
            VerifyWim062SupplyObservation(restoredFood, items, restoredCore, restoredSessions,
                actorId, facility, ordinaryMedicine.ItemId, SurvivalTreatmentKind.Standard,
                SurvivalTreatmentSupplyState.Processing);
            SurvivalTreatmentPlanSaveData normalPlan = normalPending.activeTreatmentPlans[0];
            DungeonPhysicalItemSaveData normalPhysical = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(items.Capture()));
            PhysicalItemBatchDispositionSaveData normalReceipt = normalPhysical.pendingBatchDispositions.Single(
                value => value.operationId == normalPlan.physicalCommitOperationId);
            Require(normalReceipt.commitId == normalPlan.physicalCommitId
                    && normalReceipt.quantity == 1
                    && normalReceipt.inputMassGrams == normalPlan.physicalCommitInputMassGrams
                    && normalReceipt.inputMassGrams > 0,
                "ordinary treatment save did not reference its exact pending physical receipt");

            string beforeRejectedRestore = JsonUtility.ToJson(restoredFood.Capture());
            DungeonSurvivalSaveData wrongDestination = JsonUtility.FromJson<DungeonSurvivalSaveData>(
                JsonUtility.ToJson(normalPending));
            wrongDestination.activeTreatmentPlans[0].destinationId =
                CharacterConsumablesInputDestinationIdentity.Build(
                    CharacterConsumablesInputKind.MedicalTreatment,
                    otherFacility.RequirePersistentInstanceId(),
                    new ConsumableItemDefinitionId(ordinaryMedicine.ItemId));
            bool invalidOwnerRejected = false;
            try
            {
                restoredFood.BuildRestoreCandidate(wrongDestination);
            }
            catch (InvalidOperationException)
            {
                invalidOwnerRejected = true;
            }
            Require(invalidOwnerRejected
                    && JsonUtility.ToJson(restoredFood.Capture()) == beforeRejectedRestore
                    && batch.TryGetPending(normalPlan.physicalCommitOperationId, out _),
                "invalid treatment destination restore was accepted or changed live state/receipt");
            RestoreWorldCandidateIndex treatmentWorld = new();
            treatmentWorld.SetFacilityCandidate(fixtureGrid, new[] { facility, otherFacility, warehouse });
            treatmentWorld.SetCharacterCandidate(new[] { actor });
            SurvivalResourcesSaveSection missingReceiptSection = new(
                restoredFood, catalog, treatmentWorld,
                new Wim062PhysicalCandidates(Array.Empty<PhysicalItemBatchDispositionSaveData>()),
                restoredSessions);
            DungeonGameRestoreReport missingReceiptReport = new();
            bool missingReceiptRejected = false;
            try
            {
                missingReceiptSection.Restore(JsonUtility.ToJson(normalPending),
                    DungeonSurvivalSaveData.CurrentVersion, missingReceiptReport);
                missingReceiptRejected = !missingReceiptReport.Success;
            }
            catch (InvalidOperationException)
            {
                missingReceiptRejected = true;
            }
            Require(missingReceiptRejected
                    && JsonUtility.ToJson(restoredFood.Capture()) == beforeRejectedRestore
                    && batch.TryGetPending(normalPlan.physicalCommitOperationId, out _),
                "treatment save section accepted a missing physical candidate receipt or partially published state");
            SurvivalResourcesSaveSection treatmentSection = new(
                restoredFood, catalog, treatmentWorld,
                new Wim062PhysicalCandidates(normalPhysical.pendingBatchDispositions), restoredSessions);
            DungeonSurvivalSaveData orphanReceipt = JsonUtility.FromJson<DungeonSurvivalSaveData>(
                JsonUtility.ToJson(normalPending));
            orphanReceipt.activeTreatmentPlans.Clear();
            DungeonGameRestoreReport orphanReceiptReport = new();
            bool orphanReceiptRejected = false;
            try
            {
                treatmentSection.Restore(JsonUtility.ToJson(orphanReceipt),
                    DungeonSurvivalSaveData.CurrentVersion, orphanReceiptReport);
                orphanReceiptRejected = !orphanReceiptReport.Success;
            }
            catch (InvalidOperationException)
            {
                orphanReceiptRejected = true;
            }
            Require(orphanReceiptRejected
                    && JsonUtility.ToJson(restoredFood.Capture()) == beforeRejectedRestore
                    && batch.TryGetPending(normalPlan.physicalCommitOperationId, out _),
                "treatment section accepted an orphan pending receipt after removing its last plan");
            items.Restore(normalPhysical);
            DungeonGameRestoreReport normalRestoreReport = new();
            treatmentSection.Restore(JsonUtility.ToJson(normalPending),
                DungeonSurvivalSaveData.CurrentVersion, normalRestoreReport);
            Require(normalRestoreReport.Success,
                "valid treatment plan/physical receipt/world/session candidate joins did not restore");
            WorkExecutionResult ordinaryCompleted = ExecuteWim028Treat(
                restoredHandler, actor, facility, runId: 6201, canContinue: true);
            Require(ordinaryCompleted.CompletedSuccessfully
                    && CountItem(items, ordinaryMedicine.ItemId) == 0
                    && Mathf.Approximately(restoredFood.Capture().health.Single(
                        value => value.persistentId == actorId.Value).severity, expectedSeverity)
                    && restoredFood.Capture().activeTreatmentPlans.Count == 0
                    && !batch.TryGetPending(normalPlan.physicalCommitOperationId, out _)
                    && restoredSessions.CompletedSessionCount == completionsBeforeOrdinary + 1,
                "ordinary pending treatment restore duplicated medicine, effects, or service completion");
            WorkExecutionResult ordinaryReplay = ExecuteWim028Treat(
                restoredHandler, actor, facility, runId: 6201, canContinue: true);
            Require(ordinaryReplay.CompletedSuccessfully
                    && CountItem(items, ordinaryMedicine.ItemId) == 0
                    && Mathf.Approximately(restoredFood.Capture().health.Single(
                        value => value.persistentId == actorId.Value).severity, expectedSeverity)
                    && restoredSessions.CompletedSessionCount == completionsBeforeOrdinary + 1,
                "same ordinary-treatment work replay repeated effects or billing");

            Require(items.SpawnItemAt(ordinaryMedicine.ItemId, 1,
                    warehouse.centerPos, WorldItemStackState.Stored,
                    WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    out int terminalSpawned) && terminalSpawned == 1,
                "terminal treatment fixture could not stock its second real medicine");
            WorkExecutionResult terminalWait = ExecuteWim028Treat(
                restoredHandler, actor, facility, runId: 6202, canContinue: true);
            Require(!terminalWait.CompletedSuccessfully && CountItem(items, ordinaryMedicine.ItemId) == 1,
                "second treatment did not wait for its own physical delivery");
            DeliverWim062TreatmentStock(items, reservations, actor, carry, facility, ordinaryMedicine.ItemId);
            failFirst.FailNextAcknowledge();
            WorkExecutionResult terminalPending = ExecuteWim028Treat(
                restoredHandler, actor, facility, runId: 6202, canContinue: true);
            DungeonSurvivalSaveData beforeOwnerLoss = restoredFood.Capture();
            Require(!terminalPending.CompletedSuccessfully
                    && beforeOwnerLoss.activeTreatmentPlans.Count == 1
                    && beforeOwnerLoss.activeTreatmentPlans[0].phase == SurvivalTreatmentPlanPhase.ServiceCompleted
                    && CountItem(items, ordinaryMedicine.ItemId) == 0
                    && restoredSessions.CompletedSessionCount == completionsBeforeOrdinary + 2,
                "owner-loss setup did not reach exactly one committed treatment awaiting acknowledgment: "
                + $"completed={terminalPending.CompletedSuccessfully}, failure={terminalPending.Failure.Code}, "
                + $"plans={beforeOwnerLoss.activeTreatmentPlans.Count}, "
                + $"phases={string.Join(",", beforeOwnerLoss.activeTreatmentPlans.Select(value => value.phase.ToString()))}, "
                + $"medicine={CountItem(items, ordinaryMedicine.ItemId)}, "
                + $"sessions={restoredSessions.CompletedSessionCount}/{completionsBeforeOrdinary + 2}, "
                + $"health={string.Join("|", beforeOwnerLoss.health.Select(JsonUtility.ToJson))}");
            string terminalOperation = beforeOwnerLoss.activeTreatmentPlans[0].physicalCommitOperationId;
            string healthAtOwnerLoss = string.Join("|", beforeOwnerLoss.health.Select(JsonUtility.ToJson));
            world.UnregisterBuilding(facility);
            Require(restoredFood is ISurvivalTreatmentTerminalMaintenance,
                "normal treatment has no typed lost-owner maintenance capability");
            ISurvivalTreatmentTerminalMaintenance maintenance = restoredFood;
            failFirst.FailNextAcknowledge();
            bool firstTerminal = maintenance.TryReconcileLostTreatmentOwners(out _);
            Require(!firstTerminal
                    && restoredFood.Capture().activeTreatmentPlans.Count == 1
                    && batch.TryGetPending(terminalOperation, out _)
                    && CountItem(items, ordinaryMedicine.ItemId) == 0,
                "failed owner-loss acknowledgment released the plan/receipt or refunded consumed medicine");
            Require(maintenance.TryReconcileLostTreatmentOwners(out _)
                    && maintenance.TryReconcileLostTreatmentOwners(out _)
                    && restoredFood.Capture().activeTreatmentPlans.Count == 0
                    && !batch.TryGetPending(terminalOperation, out _)
                    && CountItem(items, ordinaryMedicine.ItemId) == 0
                    && string.Join("|", restoredFood.Capture().health.Select(JsonUtility.ToJson)) == healthAtOwnerLoss
                    && restoredSessions.CompletedSessionCount == completionsBeforeOrdinary + 2,
                "lost-facility terminal retry stranded ownership or repeated consumption, treatment, or billing");
            return "warehouse-only=wait/no-effect/no-billing; controlled-haul=reserved/picked/committed/deposited; cancel=0; detox=ack-failure+restore-once; ordinary=ack-failure+section-restore+invalid-owner/missing/orphan-receipt-reject+replay-once; owner-loss=ack-retry/no-refund/no-duplicate; natural-movement/AI=NOT_RUN";
        }
        finally
        {
            food?.Dispose();
            restoredFood?.Dispose();
            if (otherFacility != null)
                world.UnregisterBuilding(otherFacility);
            if (facility != null)
                world.UnregisterBuilding(facility);
            if (warehouse != null)
                world.UnregisterBuilding(warehouse);
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            items?.Dispose();
            world.SetGrid(previousGrid);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
            UnityEngine.Object.DestroyImmediate(warehouseDefinition);
            UnityEngine.Object.DestroyImmediate(otherFacilityObject);
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string VerifyFacilityFuelDelivery()
    {
        Require(!Application.isPlaying,
            "facility fuel controlled fixture requires stopped main Editor");
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        world.TryGetGrid(out Grid previousGrid);
        Grid grid = new(20, 4);
        for (int y = 0; y < grid.height; y++)
            for (int x = 0; x < grid.width; x++)
                grid.SetAreaType(new Vector2Int(x, y), GridCellAreaType.ExteriorPath);
        Wim062GridProvider gridProvider = new(grid);
        GameObject actorObject = new("WIM_Fuel_Carrier");
        GameObject firstObject = new("WIM_Fuel_A");
        GameObject secondObject = new("WIM_Fuel_B");
        GameObject warehouseObject = new("WIM_Fuel_Warehouse");
        BuildingSO warehouseDefinition = null;
        CharacterActor actor = null;
        BuildableObject first = null;
        BuildableObject second = null;
        Wim062Warehouse warehouse = null;
        WorldItemStackRuntime items = null;
        SurvivalFoodRuntime food = null;
        try
        {
            world.SetGrid(grid);
            actorObject.AddComponent<AbilityWork>();
            actor = CreateWim028Actor(actorObject, "character:wim:fuel-carrier");
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);
            actor.GetComponent<CharacterLifecycle>().ConstructCharacterLifecycle(gridProvider);
            actor.transform.position = grid.GetWorldPos(new Vector2Int(0, 1));

            BuildingSO torch = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Modular/E01_벽횃불.asset");
            BuildingFuelConsumerAbility authored = torch?.GetAbility<BuildingFuelConsumerAbility>();
            Require(authored != null && authored.fuelPerRefuel == 1
                    && Mathf.Approximately(authored.fuelSecondsPerRefuel, 180f)
                    && !string.IsNullOrEmpty(authored.fuelItemId),
                "E01 must author one exact fuel item for 180 running game-seconds");
            BuildableObject Place(GameObject target, string id, Vector2Int position)
            {
                BuildableObject value = target.AddComponent<BuildableObject>();
                value.RestorePersistentIdentity(new BuildingInstanceId(id));
                CharacterAiEditorTestDependencies.Inject(value);
                value.SetGrid(grid);
                value.Initialization(torch, position);
                Require(grid.RegisterOccupant(value, torch.Placement.Layer,
                        value.buildPoses, torch.Placement.IsMovement),
                    "fuel facility is not registered on the actual physical grid");
                world.RegisterBuilding(value);
                return value;
            }
            first = Place(firstObject, "building:wim:fuel-a", new Vector2Int(7, 1));
            second = Place(secondObject, "building:wim:fuel-b", new Vector2Int(16, 1));
            warehouseDefinition = ScriptableObject.CreateInstance<BuildingSO>();
            warehouseDefinition.id = 99174;
            warehouseDefinition.objectName = "WIM facility fuel stock fixture";
            warehouseDefinition.width = 1;
            warehouseDefinition.height = 1;
            warehouseDefinition.category = BuildingCategory.Shop;
            warehouse = warehouseObject.AddComponent<Wim062Warehouse>();
            CharacterAiEditorTestDependencies.Inject(warehouse);
            warehouse.SetGrid(grid);
            warehouse.Initialization(warehouseDefinition, new Vector2Int(2, 1));
            world.RegisterBuilding(warehouse);
            Require(grid.RegisterOccupant(warehouse, GridLayer.Building, warehouse.buildPoses, false),
                "fuel warehouse is not registered on the actual physical grid");

            items = PhysicalItemDebugScenarios.CreateDeliveryRuntimeForCrossDomainFixture(
                gridProvider, out WorldItemRepository repository,
                out ItemQuantityReservationService reservations,
                out IReservedItemTransferService reservedTransfers, out _,
                out IPhysicalItemBatchDispositionService batch,
                out FacilityBufferDestinationClaimRegistry claims);
            IStockQuery stock = new PhysicalStockQuery(repository, items.CatalogProvider, items.MassQuery);
            warehouse.Inventory.BindPhysicalStock(stock, warehouse.RequirePersistentInstanceId(),
                CharacterAiEditorTestDependencies.AuthoredGameplay);
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? actorObject.AddComponent<CharacterCarryInventory>();
            carry.Configure(items.CatalogProvider, items.MassQuery,
                items.HaulingSettingsProvider, new CharacterCarryInventoryRegistry());
            FacilityBufferPhysicalOccupancyQuery occupancy = new(repository, items.MassQuery, reservations);
            FacilityBufferMassAdmissionService capacities = new(claims, occupancy, items.MassQuery);
            FacilityBufferDestinationLifecycleService lifecycle = new(claims, claims, capacities, capacities);
            Require(reservedTransfers is IItemTransferService,
                "fixture reserved transfer does not expose its physical transfer service");
            FacilityBufferDestinationReleaseService release = new(
                items, (IItemTransferService)reservedTransfers, world);
            FailFirstAcknowledgeBatchDisposition failFirst = new(batch);
            IPhysicalFacilityItemSinkGateway sinks = new PhysicalFacilityItemSinkGateway(stock, failFirst);
            IItemDefinitionCatalog catalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            GameEventBus events = new();
            items.Start();
            food = new SurvivalFoodRuntime(
                new SurvivalFoodRuntimeDependencies(gridProvider, items, catalog, stock,
                    FixedClimateQuery.Instance, sinks,
                    treatmentBufferCapacities: capacities,
                    treatmentBufferOccupancy: occupancy,
                    treatmentBufferClaims: claims,
                    fuelBufferLifecycle: lifecycle, fuelBufferRelease: release),
                new EmptyWildlifeSpeciesCatalog(), events, world, new FixedGameClock(),
                EmptyWorldThreatModifiers.Instance, EmptySurvivalServiceSessions.Instance,
                new DungeonRuntimeAggregateRootStore());
            food.Initialize();
            Require(food.TryGetRefuelSupplyPlan(first, out SurvivalFacilityFuelSupplyPlan plan)
                    && plan.ItemId == authored.fuelItemId && plan.RequiredQuantity == 1,
                "actual facility fuel supply plan does not match the authored item/quantity");
            Require(items.SpawnItemAt(plan.ItemId, 2, warehouse.centerPos,
                    WorldItemStackState.Stored, WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    out int spawned) && spawned == 2,
                "fixture failed to stock two exact fuel units");
            events.Publish(new OperatingDayStartedEvent(1));
            events.Publish(new OperatingDayStartedEvent(2));
            Require(CountItem(items, plan.ItemId) == 2
                    && !food.HasFuelSupply(first) && !food.HasFuelSupply(second),
                "operating day consumed global fuel or remote stock powered a facility");
            Require(!food.TryEnsureRefuelSupply(null, first, out bool initiallyComplete, out _)
                    && !initiallyComplete
                    && !food.TryApplyRefuelWork(null, first, out int remoteConsumed, out _)
                    && remoteConsumed == 0 && CountItem(items, plan.ItemId) == 2
                    && first.FacilityState.remainingFuelGameSeconds == 0f,
                "warehouse-only refuel applied charge or consumed remote stock");

            int PendingFuelQuantity() => items.GetAllStacks()
                .Where(stack => stack.ItemId == plan.ItemId
                    && stack.DestinationId == plan.DestinationId
                    && stack.State is WorldItemStackState.Stored or WorldItemStackState.Loose)
                .Sum(stack => stack.Quantity);
            Require(PendingFuelQuantity() == 1,
                "initial refuel request must own one physical pre-pickup unit");
            string beforeRepeatedSupply = JsonUtility.ToJson(items.Capture());
            for (int attempt = 0; attempt < 3; attempt++)
                Require(!food.TryEnsureRefuelSupply(null, first, out bool completedEarly, out _)
                        && !completedEarly && PendingFuelQuantity() == 1
                        && JsonUtility.ToJson(items.Capture()) == beforeRepeatedSupply,
                    "pre-pickup refuel retry requested duplicate fuel or consumed unarrived stock");

            SurvivalWorkExecutionHandler handler = CreateWim028WorkHandler(food);
            WorkExecutionResult cancelled = new();
            WorkExecutionContext cancelledContext = new(7410, actor.GetComponent<AbilityWork>(),
                actor, first, BuiltInWorkTypeIds.Refuel, (_, _, _) => EmptyWorkRoutine(), () => false);
            DrainRoutine(handler.Execute(cancelledContext, cancelled));
            Require(!cancelled.CompletedSuccessfully && CountItem(items, plan.ItemId) == 2
                    && !food.HasFuelSupply(first) && PendingFuelQuantity() == 1
                    && JsonUtility.ToJson(items.Capture()) == beforeRepeatedSupply,
                "cancelled refuel changed the pending shipment, consumed or charged before completion");

            // Exact physical pickup/deposit, with controlled movement; this does
            // not claim a naturally scheduled or timed haul.
            DeliverWim062TreatmentStock(items, reservations, actor, carry, first,
                plan.ItemId, afterPickup: () =>
                {
                    string inTransitSnapshot = JsonUtility.ToJson(items.Capture());
                    Require(!food.TryEnsureRefuelSupply(null, first, out bool chargedInTransit, out _)
                            && !chargedInTransit && !food.HasFuelSupply(first)
                            && CountItem(items, plan.ItemId) == 2
                            && JsonUtility.ToJson(items.Capture()) == inTransitSnapshot,
                        "carried fuel retry duplicated a request, consumed or charged before arrival");
                }, exactDestination: plan.DestinationId, expectedTotalQuantity: 2);
            Require(food.TryEnsureRefuelSupply(null, first, out bool completionOnly, out _)
                    && !completionOnly,
                "arrived fuel was not available for actual refuel work");
            Require(!food.TryApplyRefuelWork(null, first, out _, out _),
                "injected first acknowledgment failure was not surfaced");
            FacilityFuelCommitState pending = first.FacilityState.pendingFuel.Clone();
            Require(pending.phase == (int)FacilityFuelCommitPhase.OutcomePublished
                    && batch.TryGetPending(pending.operationId, out _)
                    && Mathf.Approximately(first.FacilityState.remainingFuelGameSeconds, 180f)
                    && second.FacilityState.remainingFuelGameSeconds == 0f
                    && CountItem(items, plan.ItemId) == 1,
                "failed acknowledgment lost receipt, duplicated stock, or powered another facility");
            FacilityRuntimeStateModule module = new(first);
            string fuelSnapshot = module.CaptureState();
            failFirst.FailNextAcknowledge();
            var forbiddenTextureAccess = new WimFuelNoTextureAccess();
            var removal = new ProductionFacilityDestructiveDrainWorldRemovalPort(
                world, forbiddenTextureAccess, food);
            ProductionFacilityWorldRemovalResult deferred = removal.TryEnsureRemoved(
                first.RequirePersistentInstanceId());
            Require(deferred.Disposition == ProductionFacilityWorldRemovalDisposition.Deferred
                    && forbiddenTextureAccess.ReadCount == 0 && !first.isDestroy
                    && first.buildPoses.All(position => ReferenceEquals(
                        grid.GetGridCell(position).GetOccupant(torch.Placement.Layer), first))
                    && world.Buildings.Contains(first) && module.CaptureState() == fuelSnapshot
                    && batch.TryGetPending(pending.operationId, out _)
                    && CountItem(items, plan.ItemId) == 1,
                "ACK failure during removal lost facility/grid/receipt ownership or reached visual removal");
            DungeonPhysicalItemSaveData physicalSnapshot = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(items.Capture()));
            items.Restore(physicalSnapshot);
            Require(module.TryRestoreState(module.CurrentVersion, fuelSnapshot, out string restoreFailure),
                "current fuel module restore failed: " + restoreFailure);
            Require(food.TryEnsureRefuelSupply(null, first, out bool restoredCompletionOnly, out _)
                    && restoredCompletionOnly
                    && !batch.TryGetPending(pending.operationId, out _)
                    && first.FacilityState.pendingFuel.phase == (int)FacilityFuelCommitPhase.None
                    && first.FacilityState.nextFuelOperationSequence == pending.operationSequence + 1
                    && Mathf.Approximately(first.FacilityState.remainingFuelGameSeconds, 180f)
                    && CountItem(items, plan.ItemId) == 1,
                "restored pending refuel failed to acknowledge without replaying charge or consumption");
            Require(food.TryApplyRefuelWork(null, first, out int replayConsumed, out _)
                    && replayConsumed == 0 && CountItem(items, plan.ItemId) == 1,
                "already-charged refuel replay consumed a second unit");
            events.Publish(new OperatingDayStartedEvent(3));
            Require(CountItem(items, plan.ItemId) == 1
                    && Mathf.Approximately(first.FacilityState.remainingFuelGameSeconds, 180f)
                    && second.FacilityState.remainingFuelGameSeconds == 0f,
                "day transition expired local charge or resumed global debit");
            return "operating-day-global-debit=0; remote-stock-charge=0; cancel=0; "
                + "controlled-reserved-pickup/deposit=PASS; input2-consumed1-remaining1; "
                + "only-A=180s; pending-ack-removal=Deferred/world-preserved; "
                + "pending-ack+physical/building-current-json+retry=once; "
                + "natural-haul/full-world-section-restore/6adult=NOT_RUN";
        }
        finally
        {
            food?.Dispose();
            if (first != null) world.UnregisterBuilding(first);
            if (second != null) world.UnregisterBuilding(second);
            if (warehouse != null) world.UnregisterBuilding(warehouse);
            if (actor != null)
            {
                world.UnregisterCharacter(actor);
                world.UnregisterCharacterLifetime(actor);
            }
            items?.Dispose();
            world.SetGrid(previousGrid);
            UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
            if (warehouseDefinition != null) UnityEngine.Object.DestroyImmediate(warehouseDefinition);
        }
    }

    private sealed class WimFuelNoTextureAccess : IGridTextureProvider
    {
        internal int ReadCount { get; private set; }
        public GridTexture Texture
        {
            get
            {
                ReadCount++;
                throw new InvalidOperationException("Deferred fuel retirement must not remove visuals.");
            }
        }
    }

    private static void DeliverWim062TreatmentStock(
        WorldItemStackRuntime items,
        ItemQuantityReservationService reservations,
        CharacterActor carrier,
        CharacterCarryInventory carry,
        BuildableObject facility,
        string itemId = "medicine:antidote",
        Action afterPickup = null,
        string exactDestination = null,
        int expectedTotalQuantity = 1)
    {
        string destination = exactDestination ?? CharacterConsumablesInputDestinationIdentity.Build(
            CharacterConsumablesInputKind.MedicalTreatment,
            facility.RequirePersistentInstanceId(),
            new ConsumableItemDefinitionId(itemId));
        Require(items.TryReserveBestHaulPlan(carrier,
                out WorldItemHaulPlan plan, out string planFailure),
            "medical delivery haul plan unavailable: " + planFailure);
        Require(plan.PrimaryDestinationId == destination
                && plan.ReservedStackQuantities.Count == 1
                && plan.ReservedStackQuantities[0].Quantity == 1,
            "physical haul plan did not own exactly one requested unit: expected=" + destination
                + "; actual=" + plan.PrimaryDestinationId
                + "; quantities=" + string.Join(",", plan.ReservedStackQuantities.Select(leg => leg.Quantity)));
        WorldItemReservedStackQuantity leg = plan.ReservedStackQuantities[0];
        Require(items.TryPickupReservedStackQuantity(carrier, carry, leg,
                out int picked, out string pickupFailure) && picked == 1,
            "medical pickup failed: " + pickupFailure);
        Require(items.TryCommitHaulPickup(leg.OwnerOperationId, carry,
                out string commitFailure),
            "medical pickup commit failed: " + commitFailure);
        Require(CountItem(items, itemId) == expectedTotalQuantity
                && items.GetAllStacks().Any(stack =>
                    stack.ItemId == itemId
                    && stack.State == WorldItemStackState.Carried
                    && stack.Quantity == 1),
            "medical pickup did not preserve one physical carried unit");
        afterPickup?.Invoke();
        Require(items.TryDepositCarriedItemsToFacility(carrier, carry,
                facility.centerPos, destination, new[] { leg.OwnerOperationId },
                out string depositFailure),
            "medical facility deposit failed: " + depositFailure);
        Require(reservations.ReleaseByOwner(leg.OwnerOperationId,
                    ItemReservationReleaseReason.Completed) > 0
                && items.ReleaseHaulDeliveryIntent(leg.OwnerOperationId),
            "completed medical delivery retained lease or intent ownership");
        Require(carry.Items.Count == 0
                && CountItem(items, itemId) == expectedTotalQuantity
                && items.GetAllStacks().Any(stack =>
                    stack.ItemId == itemId
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && stack.DestinationId == destination
                    && stack.Position == facility.centerPos
                    && stack.Quantity == 1),
            "medical delivery did not publish exactly one unit at the owned facility");
    }

    private static void VerifyWim062SupplyObservation(
        SurvivalFoodRuntime food,
        WorldItemStackRuntime items,
        CharacterConsumablesRuntime consumables,
        RetryServiceSessions sessions,
        CharacterId patientId,
        BuildableObject facility,
        string itemId,
        SurvivalTreatmentKind kind,
        SurvivalTreatmentSupplyState expected)
    {
        string beforePhysical = JsonUtility.ToJson(items.Capture());
        string beforeFood = JsonUtility.ToJson(food.Capture());
        string beforeConsumables = JsonUtility.ToJson(consumables.Capture());
        string beforeSessions = JsonUtility.ToJson(sessions.Capture());
        string expectedDestination = CharacterConsumablesInputDestinationIdentity.Build(
            CharacterConsumablesInputKind.MedicalTreatment,
            facility.RequirePersistentInstanceId(), new ConsumableItemDefinitionId(itemId));
        ISurvivalTreatmentSupplyQuery query = food;
        for (int read = 0; read < 3; read++)
        {
            Require(query.TryGetTreatmentSupply(patientId, out SurvivalTreatmentSupplySnapshot value)
                    && value.PatientId.Equals(patientId)
                    && value.FacilityId.Equals(facility.RequirePersistentInstanceId())
                    && value.ItemId.Value == itemId
                    && value.DestinationId == expectedDestination
                    && value.Kind == kind
                    && value.State == expected,
                $"medical supply observation mismatch: expected={expected}; actual={value.State}; "
                + $"patient={value.PatientId}; facility={value.FacilityId}; item={value.ItemId}; kind={value.Kind}");
        }
        Require(beforePhysical == JsonUtility.ToJson(items.Capture())
                && beforeFood == JsonUtility.ToJson(food.Capture())
                && beforeConsumables == JsonUtility.ToJson(consumables.Capture())
                && beforeSessions == JsonUtility.ToJson(sessions.Capture()),
            "medical supply query mutated physical stock, treatment/consumables, or sessions");
    }

    private sealed class Wim062Warehouse : BuildableObject, IWarehouseFacility
    {
        public WarehouseInventory Inventory { get; } = new(
            200_000L, StockCategory.Medicine, restrictCategory: false);
        public bool HasWarehouseInventory => true;
    }

    private sealed class Wim062GridProvider : IGridSystemProvider
    {
        internal Wim062GridProvider(Grid grid) => Grid = grid;
        public GridSystemManager Manager => null;
        public Grid Grid { get; }
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }
        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return grid != null;
        }
    }

    private static CharacterActor CreateWim028Actor(
        GameObject actorObject,
        string persistentId)
    {
        if (actorObject.GetComponent<AbilityWork>() == null)
            actorObject.AddComponent<AbilityWork>();
        CharacterActor actor = actorObject.AddComponent<CharacterActor>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        CharacterSO data = CharacterAiEditorTestDependencies.ContentDefinitions
            .GetAll<CharacterSO>()
            .Where(value => value != null
                && value.characterType == CharacterType.NPC
                && value.DefinitionId.IsValid)
            .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
            .First();
        actor.data = data;
        actor.characterType = CharacterType.NPC;
        actor.RefreshAbilityCache();
        actor.EnsureRuntimeState();
        actor.Identity.SetPersistentId(new CharacterId(persistentId));
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        if (actor.IsUnpublishedComposition)
            actor.PublishComposition();
        foreach (CharacterCondition condition in
                 Enum.GetValues(typeof(CharacterCondition)))
        {
            actor.stats[condition] = 100f;
        }
        return actor;
    }

    private static SurvivalFoodRuntime CreateWim028Food(
        IWorldItemStackRuntime items,
        IItemDefinitionCatalog catalog,
        IGameEventBus events,
        ICharacterAiWorldRegistry world,
        ISurvivalServiceSessionCapability sessions,
        ICharacterConsumablesApplication consumables,
        IGridSystemProvider gridProvider = null,
        IStockQuery stock = null,
        IPhysicalFacilityItemSinkGateway physicalSinks = null,
        IPackagedLotTareDispositionService packagedTare = null,
        IFacilityBufferMassCapacityQuery treatmentBufferCapacities = null,
        IFacilityBufferPhysicalOccupancyQuery treatmentBufferOccupancy = null,
        IFacilityBufferDestinationClaimQuery treatmentBufferClaims = null)
    {
        SurvivalFoodRuntime runtime = new(
            new SurvivalFoodRuntimeDependencies(
                gridProvider ?? new EmptyGridSystemProvider(),
                items,
                catalog,
                stock ?? new EmptyStockQuery(),
                FixedClimateQuery.Instance,
                physicalSinks,
                packagedTare,
                treatmentBufferCapacities: treatmentBufferCapacities,
                treatmentBufferOccupancy: treatmentBufferOccupancy,
                treatmentBufferClaims: treatmentBufferClaims),
            new EmptyWildlifeSpeciesCatalog(),
            events,
            world,
            new FixedGameClock(),
            EmptyWorldThreatModifiers.Instance,
            sessions,
            new DungeonRuntimeAggregateRootStore(),
            consumables: consumables);
        runtime.Initialize();
        return runtime;
    }

    private static SurvivalWorkExecutionHandler CreateWim028WorkHandler(
        ISurvivalFoodQuery food) => new(
        food,
        EmptyProductionBillWorkExecution.Instance,
        EmptyProcessFluidUseRuntime.Instance,
        EmptyCharacterSpeciesRechargeService.Instance,
        EmptyCropPlotRuntime.Instance);

    private static WorkExecutionResult ExecuteWim028Treat(
        SurvivalWorkExecutionHandler handler,
        CharacterActor actor,
        BuildableObject facility,
        int runId,
        bool canContinue)
    {
        WorkExecutionResult result = new();
        WorkExecutionContext context = new(
            runId,
            actor.GetComponent<AbilityWork>(),
            actor,
            facility,
            BuiltInWorkTypeIds.Treat,
            (_, _, _) => EmptyWorkRoutine(),
            () => canContinue);
        DrainRoutine(handler.Execute(context, result));
        return result;
    }

    private static IEnumerator EmptyWorkRoutine()
    {
        yield break;
    }

    private static void DrainRoutine(IEnumerator routine)
    {
        while (routine != null && routine.MoveNext())
        {
            if (routine.Current is IEnumerator nested)
                DrainRoutine(nested);
        }
    }

    private static int CountItem(
        IWorldItemStackRuntime items,
        string itemId) => items.GetAllStacks()
        .Where(stack => stack != null
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
        .Sum(stack => stack.Quantity);

    private sealed class EmptyProductionBillWorkExecution :
        IProductionBillWorkExecution
    {
        internal static readonly EmptyProductionBillWorkExecution Instance = new();

        public ProductionWorkAvailabilityResult CheckWorkAvailability(
            BuildableObject facility,
            WorkTypeId workTypeId) => new(
            false,
            new DomainFailure(FailureCode.ProductionBillMissing));

        public ProductionWorkBeginResult BeginWork(
            CharacterActor worker,
            BuildableObject facility,
            WorkTypeId workTypeId) => new(
            null,
            new DomainFailure(FailureCode.ProductionBillMissing));

        public ProductionWorkExecutionResult ExecuteWork(
            CharacterActor worker,
            BuildableObject facility,
            ProductionBillId billId,
            float amount) => default;

        public bool TrySetEmergencyProduction(
            CharacterActor worker,
            ProductionBillId billId,
            bool enabled,
            out string failureReason)
        {
            failureReason = "not-applicable";
            return false;
        }
    }

    private sealed class EmptyProcessFluidUseRuntime : IProcessFluidUseRuntime
    {
        internal static readonly EmptyProcessFluidUseRuntime Instance = new();

        public bool EnsureCycleSupply(
            BuildableObject facility,
            WorkTypeId workTypeId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryConsumeCycle(
            BuildableObject facility,
            WorkTypeId workTypeId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryConsumeCycle(
            BuildableObject facility,
            WorkTypeId workTypeId,
            float cleanWater,
            float wastewater,
            bool allowsManualWaterFallback,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryConsumeBatch(
            IReadOnlyList<ProcessFluidCycleDemand> demands,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryConsumeBatch(
            IReadOnlyList<ProcessFluidCycleDemand> demands,
            string operationId,
            out IReadOnlyList<ManualWaterTransferReceipt> manualTransfers,
            out IReadOnlyList<ProcessWastewaterComponent> wastewaterComponents,
            out DomainFailure failure)
        {
            manualTransfers = Array.Empty<ManualWaterTransferReceipt>();
            wastewaterComponents = Array.Empty<ProcessWastewaterComponent>();
            failure = DomainFailure.None;
            return true;
        }

        public bool AcknowledgeManualTransfers(
            IReadOnlyList<string> operationIds,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }
    }

    private sealed class EmptyCharacterSpeciesRechargeService :
        ICharacterSpeciesRechargeService
    {
        internal static readonly EmptyCharacterSpeciesRechargeService Instance = new();

        public bool IsRechargeAvailable(
            CharacterActor actor,
            BuildableObject facility,
            out string reason)
        {
            reason = "not-applicable";
            return false;
        }

        public float GetRechargeUrgency(
            CharacterActor actor,
            BuildableObject facility) => 0f;

        public bool TryBeginRecharge(
            CharacterActor actor,
            BuildableObject facility,
            out float completedWork,
            out DomainFailure failure)
        {
            completedWork = 0f;
            failure = new DomainFailure(FailureCode.SurvivalWorkUnsupported);
            return false;
        }

        public bool TryApplyRechargeWork(
            CharacterActor actor,
            BuildableObject facility,
            float work,
            out bool completed,
            out DomainFailure failure)
        {
            completed = false;
            failure = new DomainFailure(FailureCode.SurvivalWorkUnsupported);
            return false;
        }

        public void CancelRecharge(CharacterId characterId)
        {
        }
    }

    private sealed class EmptyCropPlotRuntime : ICropPlotRuntime
    {
        internal static readonly EmptyCropPlotRuntime Instance = new();

        public int Version => 0;
        public IReadOnlyList<CropPlotSnapshot> Plots =>
            Array.Empty<CropPlotSnapshot>();
        public void CopyVisualStates(List<CropPlotVisualState> destination)
        {
        }
        public bool TrySetCrop(
            BuildableObject plot,
            string cropId,
            out string message) => Unavailable(out message);
        public bool CanScheduleTreatment(
            BuildableObject plot,
            string treatmentItemId,
            out string reason) => Unavailable(out reason);
        public bool TryScheduleTreatment(
            BuildableObject plot,
            string treatmentItemId,
            out string message) => Unavailable(out message);
        public bool TryCancelTreatment(
            BuildableObject plot,
            out string message) => Unavailable(out message);
        public bool TryGetWork(
            BuildableObject plot,
            WorkTypeId workTypeId,
            out CropPlotWorkSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
        public bool ApplyWork(
            BuildableObject plot,
            WorkTypeId workTypeId,
            float amount,
            out bool cycleCompleted)
        {
            cycleCompleted = false;
            return false;
        }
        public bool ApplyWork(
            BuildableObject plot,
            WorkTypeId workTypeId,
            float amount,
            CharacterActor worker,
            out bool cycleCompleted)
        {
            cycleCompleted = false;
            return false;
        }
        public bool TryScheduleGoldenHarvest(
            BuildableObject plot,
            CharacterActor harvester,
            out string failureReason) => Unavailable(out failureReason);
        public bool IsGoldenHarvestWorkerEligible(
            BuildableObject plot,
            CharacterActor harvester,
            out string failureReason) => Unavailable(out failureReason);
        public bool TryGetGoldenHarvestDelay(
            BuildableObject plot,
            CharacterActor harvester,
            out float remainingSeconds)
        {
            remainingSeconds = 0f;
            return false;
        }
        private static bool Unavailable(out string detail)
        {
            detail = "not-applicable";
            return false;
        }
    }

    private sealed class RetryServiceSessions : IServiceSessionRuntime,
        ISurvivalServiceSessionCapability
    {
        private readonly List<ServiceSessionSnapshot> sessions = new();
        private int nextSequence = 1;

        internal RetryServiceSessions()
        {
        }

        internal RetryServiceSessions(ServiceRoomsSaveData save)
        {
            foreach (ServiceSessionSaveData source in save?.sessions
                         ?? new List<ServiceSessionSaveData>())
            {
                sessions.Add(source.ToSnapshot());
                string suffix = source.sessionId?
                    .Split(':')
                    .LastOrDefault();
                if (int.TryParse(suffix, out int parsed))
                    nextSequence = Math.Max(nextSequence, parsed + 1);
            }
        }

        public int Version { get; private set; }
        internal int CompletedSessionCount => sessions.Count(session =>
            session != null && session.Stage == ServiceSessionStage.Completed);
        public IReadOnlyList<ServiceSessionSnapshot> ActiveSessions => sessions
            .Where(session => session != null && session.IsActive)
            .OrderBy(session => session.SessionId, StringComparer.Ordinal)
            .ToArray();

        public ServiceHubSnapshot GetHubSnapshot(BuildableObject hub) => new()
        {
            HubId = hub.RequirePersistentInstanceId().Value,
            Hub = hub,
            Category = ServiceCategory.Medical,
            Mode = ServiceOperationMode.Direct,
            State = ServiceOperatingState.Direct,
            Capacity = 1,
            ActiveSessions = ActiveSessions.Count
        };

        public ServiceModeChangeResult SetMode(
            BuildableObject hub,
            ServiceOperationMode mode) => new()
        {
            Succeeded = mode == ServiceOperationMode.Direct,
            PreviousMode = ServiceOperationMode.Direct,
            RequestedMode = mode,
            Failure = mode == ServiceOperationMode.Direct
                ? DomainFailure.None
                : new DomainFailure(FailureCode.ServiceModeUnsupported)
        };

        public ServiceModeChangeResult SwitchToDirect(BuildableObject hub) =>
            SetMode(hub, ServiceOperationMode.Direct);

        public bool TryBeginSession(
            ServiceSessionRequest request,
            out ServiceSessionSnapshot session,
            out DomainFailure failure)
        {
            session = null;
            failure = DomainFailure.None;
            if (request?.Hub == null || request.Actor == null)
            {
                failure = new DomainFailure(FailureCode.ServiceSessionMissing);
                return false;
            }
            session = new ServiceSessionSnapshot
            {
                SessionId = $"service-session:wim028:{nextSequence++:D4}",
                HubId = request.Hub.RequirePersistentInstanceId().Value,
                ActorId = request.Actor.BuildingCharacterId.Value,
                ProcessId = request.ProcessId,
                Category = ServiceCategory.Medical,
                Stage = ServiceSessionStage.Service,
                Contract = new ServiceSessionContractSnapshot
                {
                    mode = ServiceOperationMode.Direct,
                    activeStages = ServiceProcessStageMask.Service,
                    paymentRequired = false,
                    internalActor = true
                }
            };
            sessions.Add(session);
            Version++;
            return true;
        }

        public bool TrySetStage(
            string sessionId,
            ServiceSessionStage stage,
            out DomainFailure failure)
        {
            ServiceSessionSnapshot session = FindActive(sessionId);
            if (session == null)
            {
                failure = new DomainFailure(FailureCode.ServiceSessionMissing);
                return false;
            }
            session.Stage = stage;
            Version++;
            failure = DomainFailure.None;
            return true;
        }

        public bool TryCompleteSession(
            string sessionId,
            out ServiceSessionSnapshot completed,
            out DomainFailure failure)
        {
            ServiceSessionSnapshot session = FindActive(sessionId);
            if (session == null)
            {
                completed = null;
                failure = new DomainFailure(FailureCode.ServiceSessionMissing);
                return false;
            }
            session.Stage = ServiceSessionStage.Completed;
            completed = session;
            failure = DomainFailure.None;
            Version++;
            return true;
        }

        public bool CancelSession(string sessionId, string reason)
        {
            ServiceSessionSnapshot session = FindActive(sessionId);
            if (session == null)
                return false;
            session.Stage = ServiceSessionStage.Cancelled;
            session.CancellationReason = reason ?? string.Empty;
            Version++;
            return true;
        }

        public ServiceRoomsSaveData Capture() => new()
        {
            sessions = ActiveSessions
                .Select(ServiceRoomsSaveData.FromSnapshot)
                .ToList()
        };

        public ServiceRoomsRestoreCandidate PrepareRestoreCandidate(
            ServiceRoomsSaveData saveData) => throw new NotSupportedException();

        public void PublishRestoreCandidate(
            ServiceRoomsRestoreCandidate candidate) =>
            throw new NotSupportedException();

        public ServiceAvailabilitySnapshot GetAvailability(
            ServiceCategory category) => new()
        {
            Category = category,
            State = ServiceOperatingState.Direct,
            OperationalHubCount = 1,
            Capacity = 1,
            ActiveSessions = ActiveSessions.Count
        };
        public bool ShouldAcceptDemand(ServiceCategory category) => true;
        public bool ShouldRecordUnservedDemand(
            ServiceCategory category,
            bool demandWasAdvertised) => false;
        public bool IsAdvertisingEnabled(ServiceCategory category) => false;
        public void SetAdvertisingEnabled(ServiceCategory category, bool enabled)
        {
        }

        private ServiceSessionSnapshot FindActive(string sessionId) => sessions
            .FirstOrDefault(session => session != null
                && session.IsActive
                && string.Equals(
                    session.SessionId,
                    sessionId,
                    StringComparison.Ordinal));
    }

    private sealed class EmptyGridSystemProvider : IGridSystemProvider
    {
        public GridSystemManager Manager =>
            throw new InvalidOperationException("No grid is available in this contract.");
        public Grid Grid =>
            throw new InvalidOperationException("No grid is available in this contract.");
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
    }

    private sealed class EmptyWildlifeSpeciesCatalog :
        IWildlifeSpeciesCatalogProvider
    {
        public IReadOnlyList<WildlifeSpeciesDefinition> All =>
            Array.Empty<WildlifeSpeciesDefinition>();

        public bool TryGetSpecies(
            string speciesId,
            out WildlifeSpeciesDefinition species)
        {
            species = null;
            return false;
        }

        public WildlifeSpeciesDefinition GetRandomSpecies(
            IRandomStream randomStream)
        {
            return WildlifeTestFixtures.CaveRat;
        }
    }

    private sealed class EmptyStockQuery : IStockQuery
    {
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();
        public int GetGlobalQuantity(string itemDefinitionId) => 0;

        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;

        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;

        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
    }

    private sealed class FixedGameClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class Wim036CombatRandom : ICombatRandomSource
    {
        internal static readonly Wim036CombatRandom Instance = new();
        public float Next01() => 0.5f;
    }

    private sealed class Wim036StaffDiscontent :
        IStaffDiscontentRuntimeService
    {
        internal static readonly Wim036StaffDiscontent Instance = new();
        public float GetWorkEfficiencyMultiplier(CharacterActor staff) => 1f;
        public bool ShouldBlockWork(CharacterActor staff, out string reason)
        {
            reason = string.Empty;
            return false;
        }
        public bool IsRebellionTarget(CharacterActor target) => false;
        public bool ResolveSuppressedRebel(
            CharacterActor rebel,
            CharacterActor defender) => false;
    }

    private sealed class Wim036MetaProgression :
        IMetaProgressionRuntimeReader
    {
        internal static readonly Wim036MetaProgression Instance = new();
        public int GetStartingFacilityCandidateBonus() => 0;
        public int GetStartingOwnerTraitCandidateBonus() => 0;
        public float GetOwnerMaxHealthMultiplier() => 1f;
        public float GetInvasionWarningThresholdMultiplier() => 1f;
        public float GetCommerceStockCostMultiplier(StockCategory category) => 1f;
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => 1f;
        public float GetArcaneResearchWorkMultiplier() => 1f;
        public bool IsRecipePreserved(string recipeId) => false;
        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(
            IEnumerable<BuildingSO> buildings) => Array.Empty<int>();
    }

    private sealed class Wim036EmptyEquipmentEffects :
        ICharacterEquipmentGameplayEffectSourceQuery
    {
        internal static readonly Wim036EmptyEquipmentEffects Instance = new();
        public IReadOnlyList<IGameplayEffectSource> GetEquipmentSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
    }

    private sealed class Wim036EmptyTransientEffects :
        ICharacterTransientGameplayEffectSourceQuery
    {
        internal static readonly Wim036EmptyTransientEffects Instance = new();
        public IReadOnlyList<IGameplayEffectSource> GetStatusSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
        public IReadOnlyList<IGameplayEffectSource> GetCompletedResearchSources(
            CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
    }

    private sealed class MutableConsumablesClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;

        public void Advance(float seconds)
        {
            DeltaTime = Mathf.Max(0f, seconds);
            Time += DeltaTime;
            FrameCount++;
        }
    }

    private sealed class Wim062PhysicalCandidates : IPhysicalItemRestoreCandidateQuery
    {
        internal Wim062PhysicalCandidates(IEnumerable<PhysicalItemBatchDispositionSaveData> values)
        {
            PendingBatchDispositions = values.Select(value =>
                new PhysicalItemRestoreCandidateDispositionSnapshot(value)).ToArray();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> PendingBatchDispositions { get; }

        public bool TryGetPendingBatchDisposition(string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = PendingBatchDispositions.FirstOrDefault(value =>
                string.Equals(value.OperationId, operationId, StringComparison.Ordinal));
            return disposition != null;
        }
    }

    private sealed class FailFirstAcknowledgeBatchDisposition :
        IPhysicalItemBatchDispositionService,
        ICarriedPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;
        private bool failed;

        internal FailFirstAcknowledgeBatchDisposition(
            IPhysicalItemBatchDispositionService inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal void FailNextAcknowledge() => failed = false;

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommit(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitPending(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            if (!failed)
            {
                failed = true;
                failureReason = "qa-injected-ack-failure";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);

        public bool TryCommitCarriedSinkPending(
            string stackId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            if (inner is not ICarriedPhysicalItemBatchDispositionService carried)
            {
                receipt = default;
                failureReason = "qa-carried-pending-service-missing";
                return false;
            }
            return carried.TryCommitCarriedSinkPending(
                stackId,
                quantity,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);
        }
    }

    private sealed class PackagedConsumablesTestCatalog :
        IDungeonItemCatalogProvider
    {
        private const string PackagedItemId = "drug:vitality-tonic";
        private const string ContainerItemId = "container:medical-vial";
        private readonly Dictionary<string, DungeonItemDefinition> definitions =
            new(StringComparer.Ordinal);

        internal PackagedConsumablesTestCatalog()
        {
            foreach (DungeonItemDefinition source in
                     EditorItemCatalogFactory.Create().All)
            {
                DungeonItemDefinition captured = source;
                if (string.Equals(
                        source.ItemId,
                        PackagedItemId,
                        StringComparison.Ordinal))
                {
                    captured = new DungeonItemDefinition(
                        source.ItemId,
                        source.DisplayName,
                        source.Description,
                        source.StockCategory,
                        source.UnitPrice,
                        source.Sprite,
                        source.UnitWeight,
                        source.MaxStack,
                        source.EquipmentId,
                        source.ResourceKind,
                        packageTareGrams: 30,
                        packageTareDisposition:
                            PackageTareDisposition.ReusableContainerReturn,
                        packageContainerItemId: ContainerItemId);
                }
                definitions.Add(captured.ItemId, captured);
            }

            if (!definitions.ContainsKey(PackagedItemId)
                || !definitions.TryGetValue(
                    ContainerItemId,
                    out DungeonItemDefinition container)
                || PhysicalMassGrams.FromCanonicalKilograms(
                        container.UnitWeight).Value != 30L)
            {
                throw new InvalidOperationException(
                    "Packaged consumable fixture requires vitality tonic and a 30g medical vial.");
            }
        }

        public IReadOnlyList<DungeonItemDefinition> All =>
            definitions.Values
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();

        public DungeonItemDefinition GetDefinition(string itemId) =>
            TryGetDefinition(itemId, out DungeonItemDefinition definition)
                ? definition
                : throw new KeyNotFoundException(
                    $"Unknown packaged consumable fixture item '{itemId}'.");

        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition definition) =>
            definitions.TryGetValue(itemId ?? string.Empty, out definition);
    }

    private sealed class FixedClimateQuery : IClimateQuery
    {
        internal static readonly FixedClimateQuery Instance = new();

        public int Version => 1;
        public int AbsoluteDay => 1;
        public string ClimateZoneId => "climate:temperate-cave";
        public string WeatherFrontId => "weather:clear";
        public int FrontRemainingDays => 1;
        public float OutdoorTemperatureC => 18f;
    }

    private sealed class EmptyWorldThreatModifiers : IWorldThreatModifierQuery
    {
        internal static readonly EmptyWorldThreatModifiers Instance = new();

        public OffenseThreatModifierSnapshot GetModifier(
            OffenseThreatModifierKind kind) => new(kind, 0f, 0f, 0f, 0);

        public float GetMultiplier(OffenseThreatModifierKind kind) => 1f;

        public IReadOnlyList<OffenseThreatModifierSnapshot> GetActiveModifiers() =>
            Array.Empty<OffenseThreatModifierSnapshot>();
    }

    private sealed class EmptySurvivalServiceSessions :
        ISurvivalServiceSessionCapability
    {
        internal static readonly EmptySurvivalServiceSessions Instance = new();

        public ServiceHubSnapshot GetHubSnapshot(BuildableObject hub) => new()
        {
            Hub = hub,
            Mode = ServiceOperationMode.Managed,
            State = ServiceOperatingState.Closed
        };

        public bool TryBeginSession(
            ServiceSessionRequest request,
            out ServiceSessionSnapshot session,
            out DomainFailure failure)
        {
            session = null;
            failure = new DomainFailure(FailureCode.ServiceClosed);
            return false;
        }

        public bool TryCompleteSession(
            string sessionId,
            out ServiceSessionSnapshot completed,
            out DomainFailure failure)
        {
            completed = null;
            failure = new DomainFailure(
                FailureCode.ServiceSessionMissing,
                sessionId);
            return false;
        }

        public bool CancelSession(string sessionId, string reason) => false;
    }

    private sealed class EmptyCombatCommands : ICharacterCombatCommandRuntime
    {
        internal static readonly EmptyCombatCommands Instance = new EmptyCombatCommands();
        public IReadOnlyList<CharacterCombatCommand> ActiveCommands =>
            Array.Empty<CharacterCombatCommand>();
        public bool IsInCombatStance(CharacterActor actor) => false;
        public bool SetCombatStance(CharacterActor actor, bool enabled, out string message) =>
            Unavailable(out message);
        public bool TryIssueMove(CharacterActor actor, Vector2Int destination, out string message) =>
            Unavailable(out message);
        public bool TryIssueMoveToCover(CharacterActor actor, Vector2Int destination, out string message) =>
            Unavailable(out message);
        public bool TryIssueAttack(CharacterActor actor, CombatParticipantRef target, bool forceFire, out string message) =>
            Unavailable(out message);
        public bool TryIssueForceFireAtCell(CharacterActor actor, Vector2Int targetCell, out string message) =>
            Unavailable(out message);
        public bool TryIssueReload(CharacterActor actor, out string message) => Unavailable(out message);
        public bool TryIssueSwitchWeapon(CharacterActor actor, out string message) => Unavailable(out message);
        public bool TrySetFireMode(CharacterActor actor, CombatFireMode mode, out string message) =>
            Unavailable(out message);
        public bool TrySetHoldFire(CharacterActor actor, bool holdFire, out string message) =>
            Unavailable(out message);
        public bool TryIssueRescue(CharacterActor rescuer, CharacterActor patient, out string message) =>
            Unavailable(out message);
        public bool TryGetCommand(CharacterActor actor, out CharacterCombatCommand command)
        {
            command = null;
            return false;
        }
        public void CancelCommand(CharacterActor actor, string reason)
        {
        }
        public CharacterCombatCommandSaveData Capture() => new CharacterCombatCommandSaveData();
        public CharacterCombatCommandRestoreCandidate PrepareRestore(
            CharacterCombatCommandSaveData saveData)
        {
            throw new NotSupportedException();
        }
        public void PublishRestore(CharacterCombatCommandRestoreCandidate candidate) { }
        private static bool Unavailable(out string message)
        {
            message = "unavailable";
            return false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
