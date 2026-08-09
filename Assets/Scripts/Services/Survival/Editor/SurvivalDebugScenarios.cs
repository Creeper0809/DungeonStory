using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

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
        Run("survival_typed_work_failures", VerifySurvivalTypedWorkFailures, errors);
        Run("meal_diet_content", VerifyMealDietContent, errors);
        Run("medicine_and_substance_content", VerifyMedicineAndSubstanceContent, errors);
        Run("consumables_save_payload", VerifyConsumablesSavePayload, errors);
        Run("consumables_typed_failures", VerifyConsumablesTypedFailures, errors);
        Run("consumables_physical_exactly_once", VerifyConsumablesPhysicalExactlyOnce, errors);
        Run("consumables_strict_restore", VerifyConsumablesStrictRestore, errors);
        return errors;
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
        Require(DungeonGameSaveData.CurrentVersion == 23, "game save version is not V23");
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
            actor.Identity.SetPersistentId(new CharacterId("character:consumables-fixture"));
            world.RegisterCharacter(actor);
            world.RegisterCharacterLifetime(actor);

            itemRuntime = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture();
            IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            CharacterConsumablesApplicationPorts ports = new CharacterConsumablesApplicationPorts(
                itemCatalog,
                itemRuntime,
                world,
                new GameEventBus(),
                EmptyCombatCommands.Instance);
            CharacterConsumablesRuntime core = new CharacterConsumablesRuntime(
                ports,
                ports,
                ports,
                new UnityGameClock(),
                new RandomStreamProvider(90210),
                new DungeonRuntimeAggregateRootStore());
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
                new CharacterId("character:consumables-fixture"),
                new ItemDefinitionId("drug:vitality-tonic"),
                new ItemStackId(stack.StackId),
                medicalContext: false,
                combatContext: false);

            Require(runtime.TryConsume(command, out SubstanceUseResult first)
                    && first.Success
                    && first.ItemStackId.Equals(command.ItemStackId),
                $"first physical consume failed: {first.FailureCode}");
            int quantityAfterFirst = itemRuntime.GetAllStacks()
                .Where(value => value.ItemId == "drug:vitality-tonic")
                .Sum(value => value.Quantity);
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
            DungeonCharacterConsumablesSaveData captured = core.Capture();
            Require(quantityAfterFirst == 1
                    && quantityAfterDuplicate == 1
                    && captured.completedOperations.Count == 1
                    && captured.completedOperations.Single().itemStackId == stack.StackId,
                "physical quantity or operation ledger diverged after duplicate command");
            return $"stack={stack.StackId}; quantity=2->1->1; ledger=1";
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
                EmptyCombatCommands.Instance);
            CharacterConsumablesRuntime runtime = new CharacterConsumablesRuntime(
                ports,
                ports,
                ports,
                new UnityGameClock(),
                new RandomStreamProvider(7),
                root);
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
                    EmptyCombatCommands.Instance);
            CharacterConsumablesRuntime restoredRuntime = new CharacterConsumablesRuntime(
                restoredPorts,
                restoredPorts,
                restoredPorts,
                new UnityGameClock(),
                new RandomStreamProvider(7),
                new DungeonRuntimeAggregateRootStore());
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
                    captureRoot);
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
