using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class ModularFacilityDebugScenarios
{
    private static readonly IWorldItemStackRuntime OperationalPhysicalItems =
        new EditorWarehouseStockRuntime();
    private const string BuildingFolder = "Assets/Resources/SO/Building/Modular";
    public const string ContractReportPath = "Temp/modular-facility-contract-report.tsv";
    public const string RecipeReportPath = "Temp/modular-facility-recipe-report.tsv";
    public const string EconomyReportPath = "Temp/modular-facility-economy-report.tsv";
    public const string AiReportPath = "Temp/modular-facility-ai-report.tsv";
    private static readonly IBlueprintResearchWorkService BlueprintResearchWorkService =
        new NoopBlueprintResearchWorkService();
    private static readonly IWorldInfoClickSelector WorldInfoClickSelector =
        new NoopWorldInfoClickSelector();
    private static readonly IFacilityCandidateCache FacilityCandidateCache =
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
    private static readonly IRoomFacilityPolicy RoomFacilityPolicy =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);
    private static readonly AssetDatabaseShopStockCatalog ShopStockCatalog =
        new AssetDatabaseShopStockCatalog();

    [MenuItem("DungeonStory/Debug/Facilities/Run Modular Facility Checks")]
    public static void RunAllFromMenu()
    {
        RunAll();
    }

    public static void RunAll()
    {
        Require(
            BuildingOccupancyAssignmentDebugScenarios.Verify(),
            "Building occupancy/assignment responsibility extraction failed.");
        VerifyCatalogAssets();
        ModularFacilityAssetBuilder.ValidateCriticalWorkTypeWiringAssets();
        VerifyAbilityAuthoringContract();
        VerifyOperationalContracts();
        VerifyProgressionEconomy();
        VerifyOperationalRuntimeOutcomes();
        VerifyRoomCapacityStorageAndRetail();
        VerifyAiAdmissionAndRoomInvalidation();
        VerifyEveryPartCanInstantiateAndRegister();
        VerifyIndependentGridSlots();
        VerifyMountedPartsDoNotProvideFloorSupport();
        VerifyExtendedRoomRoles();
        VerifyLegacyInitialPlacementRecipes();
        VerifyLegacyInitialRoomBoundariesRespectExterior();
        VerifyAllLegacyRecipesAsFormalRooms();
        VerifyModularRoomComposition();
        Debug.Log("ModularFacilityDebugScenarios passed: operational contracts, assets, runtime instances, migration, slots, support, and room composition.");
    }

    private static void VerifyAbilityAuthoringContract()
    {
        FieldInfo abilityField = typeof(BuildingSO).GetField(
            BuildingSO.AbilityModulesFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(abilityField != null, "BuildingSO ability list field is missing.");
        Require(abilityField.FieldType == typeof(BuildingAbilityCollection),
            "BuildingSO abilities must use BuildingAbilityCollection.");
        FieldInfo itemsField = typeof(BuildingAbilityCollection).GetField(
            "items",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(itemsField != null && itemsField.FieldType == typeof(List<BuildingAbility>),
            "BuildingAbilityCollection must own List<BuildingAbility>.");
        Require(itemsField.GetCustomAttribute<SerializeReference>() != null,
            "BuildingAbilityCollection items must use SerializeReference.");

        Type[] addableTypes = TypeCache.GetTypesDerivedFrom<BuildingAbility>()
            .Where(type => type.IsSerializable
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && type.GetConstructor(Type.EmptyTypes) != null)
            .ToArray();
        Require(addableTypes.Length >= 8, $"Expected at least 8 addable building abilities, found {addableTypes.Length}.");
        Require(addableTypes.All(type => type.IsSerializable),
            "Every addable building ability must be serializable.");
        Require(BuildingAbilityListDrawer.AddableTypes.SequenceEqual(
                addableTypes.OrderBy(BuildingAbilityListDrawer.GetDisplayName)),
            "Inspector add menu does not expose every addable ability type.");

        BuildingSO authoringFixture = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            SerializedObject serializedFixture = new SerializedObject(authoringFixture);
            SerializedProperty fixtureItems = serializedFixture
                .FindProperty(BuildingSO.AbilityModulesFieldName)
                ?.FindPropertyRelative("items");
            Require(fixtureItems != null, "Ability authoring fixture list was not serialized.");
            string propertyPath = fixtureItems.propertyPath;
            Require(BuildingAbilityListDrawer.AddAbility(
                    authoringFixture,
                    propertyPath,
                    typeof(BuildingLightingAbility)),
                "Inspector add operation did not create the selected ability type.");
            Require(authoringFixture.Abilities.Count == 1
                    && authoringFixture.Abilities[0] is BuildingLightingAbility,
                "Inspector add operation did not preserve the concrete managed-reference type.");
            Require(!BuildingAbilityListDrawer.AddAbility(
                    authoringFixture,
                    propertyPath,
                    typeof(BuildingLightingAbility)),
                "Inspector add operation allowed a duplicate ability type.");
            bool duplicateRejected = false;
            try
            {
                authoringFixture.AbilityModules.Add(new BuildingLightingAbility());
            }
            catch (InvalidOperationException)
            {
                duplicateRejected = true;
            }

            Require(duplicateRejected, "Programmatic ability add allowed a duplicate concrete type.");

            serializedFixture.Update();
            fixtureItems = serializedFixture.FindProperty(propertyPath);
            BuildingAbilityListDrawer drawer = new BuildingAbilityListDrawer();
            MethodInfo getOrCreateList = typeof(BuildingAbilityListDrawer).GetMethod(
                "GetOrCreateList",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(getOrCreateList != null,
                "Inspector ability list cache method is missing.");
            UnityEditorInternal.ReorderableList cachedList =
                getOrCreateList.Invoke(
                    drawer,
                    new object[] { fixtureItems }) as UnityEditorInternal.ReorderableList;
            Require(cachedList != null,
                "Inspector ability list cache did not create a list.");
            cachedList.index = 0;
            UnityEditorInternal.ReorderableList nextList =
                getOrCreateList.Invoke(
                    drawer,
                    new object[] { fixtureItems }) as UnityEditorInternal.ReorderableList;
            Require(ReferenceEquals(cachedList, nextList)
                    && nextList.index == 0,
                "Inspector ability selection did not survive its next draw pass.");
            Require(BuildingAbilityListDrawer.RemoveSelected(fixtureItems, 0),
                "Inspector remove operation rejected a valid selection.");
            Require(authoringFixture.Abilities.Count == 0,
                "Inspector remove operation did not clear the selected ability.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(authoringFixture);
        }

        VerifyEquipmentMaintenanceRequiresFacilityAuthority();

        FieldInfo legacyMaintenance = typeof(BuildingSO).GetField("maintenance");
        FieldInfo legacyFacility = typeof(BuildingSO).GetField("facility");
        FieldInfo legacyDefense = typeof(BuildingSO).GetField("defense");
        FieldInfo legacyEvolution = typeof(BuildingSO).GetField("evolution");
        Require(legacyMaintenance == null
                && legacyFacility == null
                && legacyDefense == null
                && legacyEvolution == null,
            "BuildingSO still serializes fixed maintenance/facility/defense/evolution fields.");

        foreach (BuildingSO asset in LoadEveryBuildingAsset())
        {
            string path = AssetDatabase.GetAssetPath(asset);
            Require(!SerializationUtility.HasManagedReferencesWithMissingTypes(asset),
                $"{path} contains a missing managed-reference ability type.");
            asset.ValidateAbilitiesOrThrow();
            IReadOnlyList<BuildingAbility> abilities = asset.Abilities;
            Require(abilities.All(ability => !string.IsNullOrWhiteSpace(ability.AbilityId)),
                $"{path} contains an ability without an explicit stable ID.");
            Require(abilities.All(ability => !ability.AbilityId.Contains(':')),
                $"{path} contains an ability ID with a reserved delimiter.");
            Require(abilities.Select(ability => ability.AbilityId).Distinct(StringComparer.Ordinal).Count()
                    == abilities.Count,
                $"{path} contains duplicate ability IDs.");
        }

        bool delimiterRejected = false;
        try
        {
            BuildingStateModuleIds.ForAbility("production:legacy", "test");
        }
        catch (ArgumentException)
        {
            delimiterRejected = true;
        }

        Require(delimiterRejected, "State module ID builder accepted an ambiguous ':' segment.");

        foreach (BuildingSO asset in LoadAll())
        {
            IReadOnlyList<BuildingAbility> abilities = asset.Abilities;
            Require(abilities.All(ability => ability != null),
                $"{asset.name} contains a null ability entry.");
            Require(abilities.Select(ability => ability.GetType()).Distinct().Count() == abilities.Count,
                $"{asset.name} contains duplicate ability types.");
            Require(abilities.All(ability => addableTypes.Contains(ability.GetType())),
                $"{asset.name} contains an ability unavailable from the Inspector add menu.");

            SerializedObject serializedAsset = new SerializedObject(asset);
            SerializedProperty serializedCollection = serializedAsset.FindProperty(
                BuildingSO.AbilityModulesFieldName);
            SerializedProperty serializedAbilities = serializedCollection?.FindPropertyRelative("items");
            Require(serializedAbilities != null && serializedAbilities.isArray,
                $"{asset.name} ability list is not available to Unity serialization.");
            Require(serializedAbilities.arraySize == abilities.Count,
                $"{asset.name} serialized/runtime ability counts differ.");
            for (int index = 0; index < serializedAbilities.arraySize; index++)
            {
                SerializedProperty element = serializedAbilities.GetArrayElementAtIndex(index);
                Require(element.propertyType == SerializedPropertyType.ManagedReference
                        && element.managedReferenceValue != null,
                    $"{asset.name} ability {index} is not a valid managed reference.");
            }
        }
    }

    private static void VerifyOperationalContracts()
    {
        Directory.CreateDirectory("Temp");
        BuildingSO[] assets = LoadAll();
        HashSet<string> catalogCodes = ModularFacilityAssetBuilder
            .GetCatalogCodes()
            .ToHashSet(StringComparer.Ordinal);
        List<string> rows = new List<string>
        {
            "id\tcode\tname\tlayer\tfootprint\truntime\troles\tworkTypes\tabilityCount\truntimeHandlers\tstateModules\tstockSignals\tstorageCapacity\tstorageMassGrams\tseats\ttables\tservice\tproduction\tworkOutput\tcost\tphase\trefundRate\tlightingAbility\tlightIntensity\tlightRadius\tresult"
        };

        Require(assets.Length == catalogCodes.Count, "Operational report asset/code count mismatch.");
        for (int index = 0; index < assets.Length; index++)
        {
            BuildingSO asset = assets[index];
            string code = asset.GetFacilityCode();
            BuildingEconomyAbility economyAbility = asset.GetAbility<BuildingEconomyAbility>();
            BuildingNeedRecoveryAbility recoveryAbility = asset.GetAbility<BuildingNeedRecoveryAbility>();
            BuildingStorageAbility storageAbility = asset.GetAbility<BuildingStorageAbility>();
            BuildingSeatingAbility seatingAbility = asset.GetAbility<BuildingSeatingAbility>();
            BuildingTableAbility tableAbility = asset.GetAbility<BuildingTableAbility>();
            BuildingServiceAbility serviceAbility = asset.GetAbility<BuildingServiceAbility>();
            BuildingProductionAbility productionAbility = asset.GetAbility<BuildingProductionAbility>();
            BuildingLightingAbility lightingAbility = asset.GetAbility<BuildingLightingAbility>();
            List<string> failures = new List<string>();
            CheckContract(asset.IsModularFacility(), "missing modular facility identity ability", failures);
            CheckContract(catalogCodes.Contains(code), "catalog code mismatch", failures);
            CheckContract(economyAbility != null, "missing economy ability", failures);
            CheckContract(asset.GetConstructionCost() > 0, "construction cost must be positive", failures);
            CheckContract(asset.GetUnlockPhase() >= 1 && asset.GetUnlockPhase() <= 3, "unlock phase out of range", failures);
            CheckContract(asset.GetDemolitionRefundRate() >= 0f && asset.GetDemolitionRefundRate() <= 1f, "refund rate out of range", failures);
            CheckContract(asset.runtimeArchetype.IsDefined(),
                "runtime archetype is not defined", failures);
            CheckContract(storageAbility == null || storageAbility.IsValid, "invalid storage ability", failures);
            CheckContract(storageAbility == null
                    || storageAbility.maxStoredMassGrams > 0L,
                "storage ability lacks positive gram authority", failures);
            CheckContract(seatingAbility == null || seatingAbility.IsValid, "invalid seating ability", failures);
            CheckContract(tableAbility == null || tableAbility.IsValid, "invalid table ability", failures);
            CheckContract(serviceAbility == null || serviceAbility.IsValid, "invalid service ability", failures);
            CheckContract(lightingAbility == null || lightingAbility.IsValid, "invalid lighting ability", failures);
            CheckContract(productionAbility == null || productionAbility.IsValid, "invalid production ability", failures);
            CheckContract(productionAbility == null
                    || (productionAbility is IBuildingWorkCompletionAbility
                        && productionAbility is IBuildingRuntimeStateAbility
                        && productionAbility is IBuildingStockCategorySignal),
                "production capability is missing its registered-dispatch contract", failures);
            CheckContract(lightingAbility == null || lightingAbility is IBuildingVisualRuntimeAbility,
                "lighting capability has no visual handler", failures);
            CheckContract(!asset.GetNeedRecovery().HasEffect
                    || (recoveryAbility != null && recoveryAbility.HasEffect),
                "recovery accessor must resolve BuildingNeedRecoveryAbility", failures);

            string[] runtimeHandlers = asset.Abilities
                .Where(ability => ability is IBuildingVisualRuntimeAbility
                    || ability is IBuildingUseCompletedRuntimeAbility
                    || ability is IBuildingWorkCompletionAbility)
                .Select(ability => ability.GetType().Name)
                .ToArray();
            string[] stateModules = asset.Abilities
                .OfType<IBuildingRuntimeStateAbility>()
                .Select(ability => ability.GetType().Name)
                .ToArray();
            StockCategory[] stockSignals = asset.GetStockCategorySignals().Distinct().ToArray();
            FacilityData facility = asset.Facility;

            string result = failures.Count == 0 ? "PASS" : string.Join(" | ", failures);
            rows.Add(string.Join("\t", new object[]
            {
                asset.id,
                code,
                asset.objectName,
                asset.Placement.Layer,
                $"{asset.width}x{asset.height}",
                asset.runtimeArchetype.ToString(),
                facility?.roles ?? FacilityRole.None,
                facility != null
                    ? CodexDomainTextFormatter.FormatWorkTypes(facility.SupportedWorkTypeIds)
                    : string.Empty,
                asset.Abilities.Count,
                string.Join(",", runtimeHandlers),
                string.Join(",", stateModules),
                string.Join(",", stockSignals),
                asset.GetStorageCapacity(),
                asset.GetStorageMassCapacityGrams(),
                asset.GetSeatCapacity(),
                asset.GetTableCapacity(),
                asset.GetServiceCapacity(),
                productionAbility != null ? productionAbility.outputCategory.ToString() : string.Empty,
                asset.GetWorkOutputAmount(),
                asset.GetConstructionCost(),
                asset.GetUnlockPhase(),
                asset.GetDemolitionRefundRate().ToString("0.##"),
                lightingAbility != null ? lightingAbility.AbilityId : string.Empty,
                lightingAbility != null ? lightingAbility.intensity.ToString("0.##") : string.Empty,
                lightingAbility != null ? lightingAbility.radius.ToString("0.##") : string.Empty,
                result
            }));
            Require(failures.Count == 0, $"{code} capability contract failed: {result}.");
        }

        File.WriteAllLines(ContractReportPath, rows);
        Require(rows.Count == assets.Length + 1,
            $"Expected report header plus {assets.Length} contracts, found {rows.Count} rows.");
    }

    private static void VerifyProgressionEconomy()
    {
        Directory.CreateDirectory("Temp");
        List<string> rows = new List<string> { "case\tresult\tdetail" };
        List<string> failures = new List<string>();
        List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        BuildingSO[] assets = LoadAll();
        BuildingSO phase1 = assets.First(asset => asset.GetUnlockPhase() == 1);
        BuildingSO phase2 = assets.First(asset => asset.GetUnlockPhase() == 2);
        BuildingSO phase3 = assets.First(asset => asset.GetUnlockPhase() == 3);
        GameSessionState gameData = CreateGameData();

        try
        {
            RecordEconomyCase(rows, failures, "phase_day_1",
                GetPhase(gameData, 1) == 1,
                $"day=1 phase={GetPhase(gameData, 1)}");
            RecordEconomyCase(rows, failures, "phase_day_3",
                GetPhase(gameData, 3) == 1,
                $"day=3 phase={GetPhase(gameData, 3)}");
            RecordEconomyCase(rows, failures, "phase_day_4",
                GetPhase(gameData, 4) == 2,
                $"day=4 phase={GetPhase(gameData, 4)}");
            RecordEconomyCase(rows, failures, "phase_day_6",
                GetPhase(gameData, 6) == 2,
                $"day=6 phase={GetPhase(gameData, 6)}");
            RecordEconomyCase(rows, failures, "phase_day_7",
                GetPhase(gameData, 7) == 3,
                $"day=7 phase={GetPhase(gameData, 7)}");

            gameData.day.Value = 1;
            gameData.holdingMoney.Value = 100000;
            RecordEconomyCase(rows, failures, "phase_1_unlocks_only_phase_1",
                FacilityProgression.IsUnlocked(phase1, gameData, null, DisabledDungeonDebugRuleQuery.Instance)
                && !FacilityProgression.IsUnlocked(phase2, gameData, null, DisabledDungeonDebugRuleQuery.Instance)
                && !FacilityProgression.IsUnlocked(phase3, gameData, null, DisabledDungeonDebugRuleQuery.Instance),
                $"p1={FacilityProgression.IsUnlocked(phase1, gameData, null, DisabledDungeonDebugRuleQuery.Instance)}; "
                + $"p2={FacilityProgression.IsUnlocked(phase2, gameData, null, DisabledDungeonDebugRuleQuery.Instance)}; "
                + $"p3={FacilityProgression.IsUnlocked(phase3, gameData, null, DisabledDungeonDebugRuleQuery.Instance)}");

            Grid phaseGrid = CreateSupportedGrid(20);
            GridBuildingPlacementService phaseService = CreateEconomyPlacementService(phaseGrid, phase2, gameData);
            int lockedMoney = gameData.holdingMoney.Value;
            bool lockedPlaced = phaseService.TryPlaceBuilding(phase2, new Vector2Int(5, 0), out string lockedReason);
            RecordEconomyCase(rows, failures, "locked_build_rejected_without_charge",
                !lockedPlaced
                && phaseGrid.GetGridCell(new Vector2Int(5, 0)).GetOccupant(phase2.layer) == null
                && gameData.holdingMoney.Value == lockedMoney
                && !string.IsNullOrWhiteSpace(lockedReason),
                $"placed={lockedPlaced}; money={lockedMoney}->{gameData.holdingMoney.Value}; reason={lockedReason}");

            gameData.day.Value = 7;
            gameData.holdingMoney.Value = 0;
            Grid poorGrid = CreateSupportedGrid(20);
            GridBuildingPlacementService poorService = CreateEconomyPlacementService(poorGrid, phase3, gameData);
            int poorMoney = gameData.holdingMoney.Value;
            bool poorPlaced = poorService.TryPlaceBuilding(phase3, new Vector2Int(6, 0), out string poorReason);
            BuildableObject poorBuilding = poorGrid.GetGridCell(new Vector2Int(6, 0))
                .GetOccupant(phase3.layer) as BuildableObject;
            RecordEconomyCase(rows, failures, "zero_gold_does_not_block_construction",
                poorPlaced
                && poorBuilding != null
                && gameData.holdingMoney.Value == poorMoney,
                $"placed={poorPlaced}; money={poorMoney}->{gameData.holdingMoney.Value}; reason={poorReason}");
            if (poorBuilding != null)
            {
                poorService.TryDestroyBuilding(poorBuilding, out _, out _);
            }

            gameData.day.Value = 4;
            gameData.holdingMoney.Value = 17;
            Grid successGrid = CreateSupportedGrid(20);
            GridBuildingPlacementService successService = CreateEconomyPlacementService(successGrid, phase2, gameData);
            Vector2Int successCell = new Vector2Int(8, 0);
            bool placed = successService.TryPlaceBuilding(phase2, successCell, out string placementReason);
            BuildableObject placedBuilding = successGrid.GetGridCell(successCell).GetOccupant(phase2.layer) as BuildableObject;
            int afterFirstBuild = gameData.holdingMoney.Value;
            bool duplicatePlaced = successService.TryPlaceBuilding(phase2, successCell, out _);
            int afterFailedDuplicate = gameData.holdingMoney.Value;
            RecordEconomyCase(rows, failures, "successful_build_preserves_gold",
                placed
                && placedBuilding != null
                && afterFirstBuild == 17
                && !duplicatePlaced
                && afterFailedDuplicate == 17,
                $"placed={placed}; reason={placementReason}; money=17->{afterFirstBuild}->{afterFailedDuplicate}");

            bool destroyed = successService.TryDestroyBuilding(placedBuilding, out _, out string destroyReason);
            RecordEconomyCase(rows, failures, "demolition_does_not_change_gold",
                destroyed && gameData.holdingMoney.Value == 17,
                $"destroyed={destroyed}; reason={destroyReason}; money={gameData.holdingMoney.Value}");

            BuildingSO modularCompatibility = CreateEconomyBuilding(
                -8101,
                "TEST_MODULAR_ECONOMY",
                constructionCost: 40,
                cleanup);
            AddLegacyMoneyCondition(modularCompatibility, 100);
            gameData.day.Value = 1;
            gameData.holdingMoney.Value = 40;
            Grid modularGrid = CreateSupportedGrid(10);
            GridBuildingPlacementService modularService = CreateEconomyPlacementService(
                modularGrid,
                modularCompatibility,
                gameData);
            bool modularPlaced = modularService.TryPlaceBuilding(
                modularCompatibility,
                new Vector2Int(4, 0),
                out string modularReason);
            BuildableObject modularBuilding = modularGrid.GetGridCell(new Vector2Int(4, 0))
                .GetOccupant(GridLayer.Building) as BuildableObject;
            RecordEconomyCase(rows, failures, "modular_legacy_money_condition_is_ignored",
                modularPlaced && modularBuilding != null && gameData.holdingMoney.Value == 40,
                $"placed={modularPlaced}; reason={modularReason}; money=40->{gameData.holdingMoney.Value}");
            if (modularBuilding != null)
            {
                modularService.TryDestroyBuilding(modularBuilding, out _, out _);
            }

            BuildingSO legacyCompatibility = CreateEconomyBuilding(
                -8102,
                string.Empty,
                constructionCost: 0,
                cleanup);
            AddLegacyMoneyCondition(legacyCompatibility, 25);
            gameData.holdingMoney.Value = 24;
            Grid legacyGrid = CreateSupportedGrid(10);
            GridBuildingPlacementService legacyService = CreateEconomyPlacementService(
                legacyGrid,
                legacyCompatibility,
                gameData);
            bool legacyPoorPlaced = legacyService.TryPlaceBuilding(
                legacyCompatibility,
                new Vector2Int(4, 0),
                out _);
            BuildableObject legacyBuilding = legacyGrid.GetGridCell(new Vector2Int(4, 0))
                .GetOccupant(GridLayer.Building) as BuildableObject;
            RecordEconomyCase(rows, failures, "legacy_money_condition_no_longer_charges",
                legacyPoorPlaced
                && legacyBuilding != null
                && gameData.holdingMoney.Value == 24,
                $"placed={legacyPoorPlaced}; money=24->{gameData.holdingMoney.Value}");
            if (legacyBuilding != null)
            {
                legacyService.TryDestroyBuilding(legacyBuilding, out _, out _);
            }
        }
        finally
        {
            foreach (UnityEngine.Object item in cleanup.Where(item => item != null))
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        File.WriteAllLines(EconomyReportPath, rows);
        Require(failures.Count == 0, "Economy verification failed: " + string.Join(" | ", failures));
    }

    private static int GetPhase(GameSessionState gameData, int day)
    {
        gameData.day.Value = day;
        return FacilityProgression.GetCurrentPhase(gameData);
    }

    private static Grid CreateSupportedGrid(int width)
    {
        Grid grid = new Grid(width, 1);
        for (int x = 0; x < width; x++)
        {
            Require(grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new[] { new Vector2Int(x, 0) },
                    false),
                $"Could not register economy hallway at {x}.");
        }

        return grid;
    }

    private static GridBuildingPlacementService CreateEconomyPlacementService(
        Grid grid,
        BuildingSO building,
        GameSessionState gameData)
    {
        return new GridBuildingPlacementService(
            grid,
            null,
            id => id == building.id ? building : null,
            new GridBuildingFactory(
                null,
                placed =>
                {
                    placed.ConstructPersistentIdentity(
                        new GuidPersistentIdGenerator());
                    placed.ConstructBuildableObject(
                        new BuildingResearchWorkPortAdapter(
                            BlueprintResearchWorkService),
                        FacilityCandidateCache,
                        RoomFacilityPolicy, combatEquipmentRuntime: null, worldRegistry: null, worldItemStackRuntime: null, abilityRuntimeDispatcher: null, gameClock: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
                    placed.ConstructBuildableObjectEventBus(
                        CharacterAiEditorTestDependencies.GameEvents,
                        new BuildingVisitEventPublisher(
                            CharacterAiEditorTestDependencies.GameEvents),
                        new BuildingInfoPresentationAdapter(
                            CharacterAiEditorTestDependencies.GameEvents));
                },
                new GridBuildingObjectFactory()),
            new BuildingPlacementValidator(
                new GridPlacementValidator(),
                () => new BuildingConditionContext(gameData)),
            workOrderRuntime: null,
            destructiveLoss: ImmediateBuildingDestructiveLossFixture.Instance);
    }

    private static BuildingSO CreateEconomyBuilding(
        int id,
        string code,
        int constructionCost,
        ICollection<UnityEngine.Object> cleanup)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = string.IsNullOrWhiteSpace(code) ? "Legacy Economy Fixture" : "Modular Economy Fixture";
        BuildingSO visualSource = Load("D01");
        building.sprite = visualSource.sprite;
        building.icon = visualSource.icon;
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;
        building.category = BuildingCategory.Shop;
        building.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
        building.unlocked = true;
        building.ReplaceAbilities(new BuildingAbilityCollection());
        building.Facility = new FacilityData();
        building.Evolution = new FacilityEvolutionContributionData();
        if (!string.IsNullOrWhiteSpace(code))
        {
            building.AbilityModules.Add(new BuildingFacilityPartAbility { code = code });
            BuildingEconomyAbility economyAbility = new BuildingEconomyAbility();
            economyAbility.constructionCost = constructionCost;
            economyAbility.unlockPhase = 1;
            economyAbility.demolitionRefundRate = 0.5f;
            building.AbilityModules.Add(economyAbility);
        }
        cleanup.Add(building);
        return building;
    }

    private static void AddLegacyMoneyCondition(BuildingSO building, int cost)
    {
        ConditionNeedMoney condition = new ConditionNeedMoney();
        typeof(ConditionNeedMoney)
            .GetField("cost", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(condition, cost);
        typeof(BuildingSO)
            .GetField("OnBuildCondition", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(building, new List<IBuildingCondition> { condition });
    }

    private static void RecordEconomyCase(
        ICollection<string> rows,
        ICollection<string> failures,
        string key,
        bool passed,
        string detail)
    {
        string cleanDetail = (detail ?? string.Empty).Replace('\t', ' ').Replace('\n', ' ');
        rows.Add($"{key}\t{(passed ? "PASS" : "FAIL")}\t{cleanDetail}");
        if (!passed)
        {
            failures.Add(key + ": " + cleanDetail);
        }
    }

    private static void CheckContract(bool condition, string failure, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }

    private static void VerifyOperationalRuntimeOutcomes()
    {
        Grid grid = new Grid(30, 1);
        List<BuildableObject> created = new List<BuildableObject>();
        List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        try
        {
            for (int x = 0; x < grid.width; x++)
            {
                Require(grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new[] { new Vector2Int(x, 0) },
                    false),
                    $"Could not register operational room hallway at {x}.");
            }

            CreateBoundary(grid, "Door", new Vector2Int(1, 0), BuildingCategory.Movement, created, cleanup);
            CreateBoundary(grid, "Wall", new Vector2Int(28, 0), BuildingCategory.Wall, created, cleanup);
            Facility hearth = (Facility)CreateAndRegister(grid, Load("D01"), new Vector2Int(3, 0), created);
            Facility foodShelf = (Facility)CreateAndRegister(grid, Load("D10"), new Vector2Int(4, 0), created);
            Facility forge = (Facility)CreateAndRegister(grid, Load("S08"), new Vector2Int(6, 0), created);
            Facility alchemy = (Facility)CreateAndRegister(grid, Load("Q02"), new Vector2Int(10, 0), created);
            Facility reagentShelf = (Facility)CreateAndRegister(grid, Load("Q04"), new Vector2Int(12, 0), created);
            Facility alarm = (Facility)CreateAndRegister(grid, Load("G02"), new Vector2Int(13, 0), created);
            Facility toilet = (Facility)CreateAndRegister(grid, Load("H01"), new Vector2Int(14, 0), created);
            Facility melee = (Facility)CreateAndRegister(grid, Load("T01"), new Vector2Int(15, 0), created);
            Facility ranged = (Facility)CreateAndRegister(grid, Load("T02"), new Vector2Int(16, 0), created);
            Facility strength = (Facility)CreateAndRegister(grid, Load("T03"), new Vector2Int(17, 0), created);
            Facility sink = (Facility)CreateAndRegister(grid, Load("H03"), new Vector2Int(18, 0), created);
            Facility bed = (Facility)CreateAndRegister(grid, Load("R01"), new Vector2Int(20, 0), created);
            Facility torch = (Facility)CreateAndRegister(grid, Load("E01"), new Vector2Int(22, 0), created);

            RoomInstance room = RoomDetector.Build(grid).Rooms
                .FirstOrDefault(candidate => candidate != null && candidate.ContainsPart(hearth));
            Require(room != null && room.IsUsable, "Operational fixture did not form a usable room.");

            BuildingCookingAbility hearthCooking =
                hearth.BuildingData.GetAbility<BuildingCookingAbility>();
            int expectedMeals = Mathf.Max(1, hearthCooking?.cookedMeals ?? 0);
            Require(hearthCooking != null
                    && ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                        null,
                        hearth,
                        BuiltInWorkTypeIds.Cook) == expectedMeals,
                "Meal production did not return the configured output.");
            Require(foodShelf.Inventory.GetStock(StockCategory.Food) == expectedMeals,
                "Meal production did not enter food storage.");
            Require(forge.SupportsWork(BuiltInWorkTypeIds.Craft)
                    && !forge.SupportsWork(BuiltInWorkTypeIds.Operate)
                    && forge.BuildingData.GetAbility<BuildingEquipmentCraftingAbility>() != null,
                "Blacksmith bench must be an equipment crafting work target, not a direct weapon producer.");
            Require(ModularFacilityRuntimeEffects.ApplyWorkCompleted(null, alchemy, BuiltInWorkTypeIds.Research) == 3,
                "Alchemy research did not return the configured mana output.");
            Require(reagentShelf.Inventory.GetStock(StockCategory.Mana) == 3,
                "Alchemy output did not enter mana storage.");

            ModularFacilityRuntimeEffects.ApplyWorkCompleted(null, alarm, BuiltInWorkTypeIds.Repair);
            BuildingSecurityAbility securityAbility = alarm.BuildingData.GetAbility<BuildingSecurityAbility>();
            BuildingSecurityStateModule securityState = alarm.RequireStateModule<BuildingSecurityStateModule>(
                BuildingStateModuleIds.ForAbility("security", securityAbility.AbilityId));
            Require(securityState.AlarmCharges == 0,
                "Repairing an alarm must not arm it.");
            for (int i = 0; i < 5; i++)
            {
                ModularFacilityRuntimeEffects.ApplyWorkCompleted(null, alarm, BuiltInWorkTypeIds.Guard);
            }
            Require(securityState.AlarmCharges == 3,
                "Alarm charges must cap at three guard completions.");

            foreach (BuildableObject part in hearth.GetRoomOperationalProfile()
                         .Parts
                         .OfType<BuildableObject>())
            {
                part.SetCleanliness(12f);
            }
            ModularFacilityRuntimeEffects.ApplyWorkCompleted(null, toilet, BuiltInWorkTypeIds.Clean);
            Require(hearth.GetRoomOperationalProfile().Parts
                    .OfType<BuildableObject>()
                    .All(part => Mathf.Approximately(
                        part.FacilityState.cleanliness,
                        100f)),
                "Cleaning did not restore every facility in the room.");

            Light2D light = torch.GetComponentInChildren<Light2D>();
            BuildingLightingAbility torchLighting = torch.BuildingData.GetAbility<BuildingLightingAbility>();
            RoomClippedLight2D clippedLight = torch.GetComponentInChildren<RoomClippedLight2D>();
            clippedLight?.ForceRefresh();
            Require(light != null
                    && torchLighting != null
                    && clippedLight != null
                    && clippedLight.HasCurrentWorldRect
                    && light.lightType == Light2D.LightType.Freeform
                    && clippedLight.CurrentFalloffSize > 0.25f
                    && Mathf.Approximately(light.intensity, torchLighting.intensity)
                    && Mathf.Approximately(light.pointLightOuterRadius, torchLighting.radius),
                "Lighting facility did not create its configured room-clipped runtime Light2D.");
            Require(light.targetSortingLayers != null
                    && !light.targetSortingLayers.Contains(SortingLayer.NameToID("Wall"))
                    && light.targetSortingLayers.Contains(SortingLayer.NameToID("DungeonBackObject")),
                "Lighting facility must not affect the Wall sorting layer used by ceilings.");
            Require(clippedLight.CurrentWorldRect.xMin >= grid.GetWorldPos(new Vector2Int(27, 0)).x - 0.5f,
                "Lighting facility leaked past the left room boundary.");
            Require(clippedLight.CurrentWorldRect.xMax <= grid.GetWorldPos(new Vector2Int(2, 0)).x + 0.5f,
                "Lighting facility leaked past the right room boundary.");
            Require(clippedLight.CurrentCoreRect.width < clippedLight.CurrentWorldRect.width
                    && clippedLight.CurrentCoreRect.height < clippedLight.CurrentWorldRect.height,
                "Lighting facility should fade out inside the clipped room instead of cutting off at the wall.");
            Require(clippedLight.CurrentShapePointCount >= 16,
                "Lighting facility should use a rounded freeform path instead of a visible rectangle.");

            GameObject actorObject = new GameObject("Modular Operational Outcome Actor");
            cleanup.Add(actorObject);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorObject.GetComponent<CharacterIdentity>()
                .SetPersistentId("character:test:modular-operational-outcome");
            actor.stats = new Dictionary<CharacterCondition, float>
            {
                { CharacterCondition.SLEEP, 0f },
                { CharacterCondition.MOOD, 50f },
                { CharacterCondition.FUN, 0f },
                { CharacterCondition.HUNGER, 0f },
                { CharacterCondition.EXCRETION, 0f },
                { CharacterCondition.HYGIENE, 0f }
            };
            IBuildingVisitorPort visitor = actor.BuildingVisitor;

            hearth.ApplyConfiguredUseRecovery(visitor);
            toilet.ApplyConfiguredUseRecovery(visitor);
            sink.ApplyConfiguredUseRecovery(visitor);
            bed.ApplyConfiguredUseRecovery(visitor);
            Require(actor.stats[CharacterCondition.HUNGER] >= 35f,
                "Meal use did not recover hunger.");
            Require(actor.stats[CharacterCondition.EXCRETION] >= 75f,
                "Toilet use did not recover excretion need.");
            Require(actor.stats[CharacterCondition.HYGIENE] >= 45f,
                "Sink use did not recover hygiene.");
            Require(actor.stats[CharacterCondition.SLEEP] >= 35f,
                "Bed use did not recover sleep.");

            ModularFacilityRuntimeEffects.ApplyUseCompleted(visitor, melee);
            ModularFacilityRuntimeEffects.ApplyUseCompleted(visitor, ranged);
            ModularFacilityRuntimeEffects.ApplyUseCompleted(visitor, strength);
            string[] trainingLabels = actor.Mood.Factors
                .Where(factor => factor.Kind == CharacterMoodFactorKind.Interaction)
                .Select(factor => factor.Label)
                .ToArray();
            Require(trainingLabels.Contains("근접 훈련 감각이 살아남")
                    && trainingLabels.Contains("과녁에 집중해 마음이 맑아짐")
                    && trainingLabels.Contains("묵직한 훈련으로 성취감이 남음"),
                "Training facilities did not create their distinct mood experiences.");
        }
        finally
        {
            DestroyCreated(created);
            foreach (UnityEngine.Object item in cleanup.Where(item => item != null))
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }

    private static void VerifyRoomCapacityStorageAndRetail()
    {
        List<BuildableObject> created = new List<BuildableObject>();
        List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        GameSessionState gameData = CreateGameData();
        NoopFloatingNumberFeedbackService numberFeedback = new NoopFloatingNumberFeedbackService();
        NoopWorkforceReplanService workforce = new NoopWorkforceReplanService();
        try
        {
            Grid foodGrid = CreateFormalRoomGrid(30, created, cleanup);
            Facility hearth = (Facility)CreateAndRegister(foodGrid, Load("D01"), new Vector2Int(3, 0), created);
            CreateAndRegister(foodGrid, Load("D03"), new Vector2Int(5, 0), created);
            CreateAndRegister(foodGrid, Load("D04"), new Vector2Int(7, 0), created);
            CreateAndRegister(foodGrid, Load("D05"), new Vector2Int(9, 0), created);
            CreateAndRegister(foodGrid, Load("D09"), new Vector2Int(11, 0), created);
            Facility foodShelf = (Facility)CreateAndRegister(foodGrid, Load("D10"), new Vector2Int(13, 0), created);
            Facility logistics = (Facility)CreateAndRegister(foodGrid, Load("L01"), new Vector2Int(15, 0), created);
            Shop foodShop = (Shop)CreateAndRegister(foodGrid, Load("S01"), new Vector2Int(18, 0), created);
            foodShop.ConstructShop(
                new EditorGameMoneyAccount(gameData),
                ShopStockCatalog,
                numberFeedback,
                workforce,
                FacilityCrimeEditorTestDependencies.Evaluator,
                new DungeonStory.Foundation.RandomStreamProvider(201), null, null, null,
                CharacterAiEditorTestDependencies.RetailStockPhysical,
                CharacterAiEditorTestDependencies.PhysicalStock);

            BuildingRoomOperationalSnapshot foodProfile =
                hearth.GetRoomOperationalProfile();
            Require(foodProfile.IsUsableRoom
                    && foodProfile.SeatCapacity == 2
                    && foodProfile.TableCapacity == 2
                    && foodProfile.ServiceCapacity == 2,
                $"Unexpected dining support profile: seats={foodProfile.SeatCapacity}, tables={foodProfile.TableCapacity}, service={foodProfile.ServiceCapacity}.");
            Require(hearth.EffectiveCapacity == 2,
                $"Dining capacity should be two, found {hearth.EffectiveCapacity}.");
            Require(foodShop.EffectiveCapacity == 3,
                $"Shop service capacity should be three, found {foodShop.EffectiveCapacity}.");
            Require(foodShop.ActiveStockCategory == StockCategory.Food,
                $"Food room specialized as {foodShop.ActiveStockCategory}.");
            Require(foodProfile.GetStorageCapacity(StockCategory.Food) == 76,
                $"Food storage should include food shelf 16 plus universal logistics 60, found {foodProfile.GetStorageCapacity(StockCategory.Food)}.");
            Require(foodShop.MaxInternalStock == 100,
                $"Food shop capacity should be 24 + 76, found {foodShop.MaxInternalStock}.");
            Require(foodShelf.Inventory.Accepts(StockCategory.Food)
                    && !foodShelf.Inventory.Accepts(StockCategory.Weapon)
                    && foodShelf.Inventory.SeedPhysicalStockForTest(StockCategory.Food, 1) == 1
                    && foodShelf.Inventory.SeedPhysicalStockForTest(StockCategory.Weapon, 1) == 0,
                "Food shelf did not enforce its category.");

            Require(logistics.Inventory.SeedPhysicalStockForTest(StockCategory.Food, 1) == 1
                    && logistics.Inventory.SeedPhysicalStockForTest(StockCategory.Weapon, 1) == 1
                    && logistics.Inventory.SeedPhysicalStockForTest(StockCategory.Mana, 1) == 1,
                "Universal logistics storage did not accept all categories.");
            VerifyShopProducts(foodShop, StockCategory.Food, "food specialization");

            Require(ShopStockCatalog.TryGetSaleItemByCategory(StockCategory.Weapon, out SaleItem weaponItem),
                "Could not load a weapon sale item for specialization rejection.");
            RetailStockLotSnapshot wrongCategoryLot = CreateScenarioRetailLot(
                weaponItem,
                "retail-source:fixture:wrong-category");
            Require(!foodShop.TryReceiveExactRetailLots(
                        new[] { wrongCategoryLot },
                        1,
                        out int rejectedQuantity,
                        out string rejectReason)
                    && rejectedQuantity == 0
                    && rejectReason == "retail-lot-definition-mismatch",
                $"Food shop accepted a weapon restock: {rejectReason}");

            Grid weaponGrid = CreateFormalRoomGrid(18, created, cleanup);
            CreateAndRegister(weaponGrid, Load("S05"), new Vector2Int(4, 0), created);
            CreateAndRegister(weaponGrid, Load("S06"), new Vector2Int(6, 0), created);
            CreateAndRegister(weaponGrid, Load("S07"), new Vector2Int(8, 0), created);
            Shop weaponShop = (Shop)CreateAndRegister(weaponGrid, Load("S01"), new Vector2Int(11, 0), created);
            weaponShop.ConstructShop(
                new EditorGameMoneyAccount(gameData),
                ShopStockCatalog,
                numberFeedback,
                workforce,
                FacilityCrimeEditorTestDependencies.Evaluator,
                new DungeonStory.Foundation.RandomStreamProvider(203), null, null, null,
                CharacterAiEditorTestDependencies.RetailStockPhysical,
                CharacterAiEditorTestDependencies.PhysicalStock);
            Require(weaponShop.ActiveStockCategory == StockCategory.Weapon,
                $"Weapon room specialized as {weaponShop.ActiveStockCategory}.");
            VerifyShopProducts(weaponShop, StockCategory.Weapon, "weapon specialization");

            Grid generalGrid = CreateFormalRoomGrid(14, created, cleanup);
            CreateAndRegister(generalGrid, Load("S02"), new Vector2Int(5, 0), created);
            Shop generalShop = (Shop)CreateAndRegister(generalGrid, Load("S01"), new Vector2Int(8, 0), created);
            generalShop.ConstructShop(
                new EditorGameMoneyAccount(gameData),
                ShopStockCatalog,
                numberFeedback,
                workforce,
                FacilityCrimeEditorTestDependencies.Evaluator,
                new DungeonStory.Foundation.RandomStreamProvider(205), null, null, null,
                CharacterAiEditorTestDependencies.RetailStockPhysical,
                CharacterAiEditorTestDependencies.PhysicalStock);
            Require(generalShop.ActiveStockCategory == StockCategory.General,
                $"General room specialized as {generalShop.ActiveStockCategory}.");
            VerifyShopProducts(generalShop, StockCategory.General, "general specialization");

            Grid manaGrid = CreateFormalRoomGrid(18, created, cleanup);
            CreateAndRegister(manaGrid, Load("Q02"), new Vector2Int(4, 0), created);
            CreateAndRegister(manaGrid, Load("M04"), new Vector2Int(7, 0), created);
            Shop manaShop = (Shop)CreateAndRegister(manaGrid, Load("S01"), new Vector2Int(11, 0), created);
            manaShop.ConstructShop(
                new EditorGameMoneyAccount(gameData),
                ShopStockCatalog,
                numberFeedback,
                workforce,
                FacilityCrimeEditorTestDependencies.Evaluator,
                new DungeonStory.Foundation.RandomStreamProvider(207), null, null, null,
                CharacterAiEditorTestDependencies.RetailStockPhysical,
                CharacterAiEditorTestDependencies.PhysicalStock);
            Require(manaShop.ActiveStockCategory == StockCategory.Mana,
                $"Mana room specialized as {manaShop.ActiveStockCategory}.");
        }
        finally
        {
            DestroyCreated(created);
            foreach (UnityEngine.Object item in cleanup.Where(item => item != null))
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }

    private static void VerifyAiAdmissionAndRoomInvalidation()
    {
        Directory.CreateDirectory("Temp");
        List<string> rows = new List<string> { "case\tresult\tdetail" };
        List<string> failures = new List<string>();
        List<BuildableObject> created = new List<BuildableObject>();
        List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        GameSessionState gameData = CreateGameData();

        try
        {
            Grid grid = CreateFormalRoomGrid(30, created, cleanup);
            Facility mealCore = (Facility)CreateAndRegister(grid, Load("D01"), new Vector2Int(4, 0), created);
            CreateAndRegister(grid, Load("D03"), new Vector2Int(6, 0), created);
            CreateAndRegister(grid, Load("D04"), new Vector2Int(8, 0), created);
            CreateAndRegister(grid, Load("D05"), new Vector2Int(10, 0), created);
            CreateAndRegister(grid, Load("D09"), new Vector2Int(12, 0), created);
            Shop shop = (Shop)CreateAndRegister(grid, Load("S01"), new Vector2Int(16, 0), created);
            shop.ConstructShop(
                new EditorGameMoneyAccount(gameData),
                ShopStockCatalog,
                new NoopFloatingNumberFeedbackService(),
                new NoopWorkforceReplanService(),
                FacilityCrimeEditorTestDependencies.Evaluator,
                new DungeonStory.Foundation.RandomStreamProvider(209), null, null, null,
                CharacterAiEditorTestDependencies.RetailStockPhysical,
                CharacterAiEditorTestDependencies.PhysicalStock);

            Require(shop.HasAvailableStock,
                "Authored exact retail stock was not materialized for the AI admission fixture.");

            CharacterActor[] visitors = Enumerable.Range(1, 4)
                .Select(index => CreateReservationActor($"Modular AI Visitor {index}", cleanup))
                .ToArray();

            bool mealFirst = mealCore.TryReserveVisit(visitors[0], out string mealFirstReason);
            bool mealSameVisitor = mealCore.TryReserveVisit(visitors[0], out string mealSameReason);
            bool mealSecond = mealCore.TryReserveVisit(visitors[1], out string mealSecondReason);
            bool mealThirdQueued = mealCore.TryReserveVisit(visitors[2], out string mealFullReason);
            RecordAiCase(rows, failures, "dining_reservation_fifo_queue",
                mealCore.EffectiveCapacity == 2
                && mealFirst
                && mealSameVisitor
                && mealSecond
                && mealThirdQueued
                && mealCore.ActiveVisitReservationCount == 3
                && mealCore.WaitingVisitReservationCount == 1
                && mealCore.GetVisitQueuePosition(visitors[2]) == 3,
                $"capacity={mealCore.EffectiveCapacity}; reservations={mealCore.ActiveVisitReservationCount}; "
                + $"first={mealFirst}:{mealFirstReason}; same={mealSameVisitor}:{mealSameReason}; "
                + $"second={mealSecond}:{mealSecondReason}; third={mealThirdQueued}:{mealFullReason}; "
                + $"waiting={mealCore.WaitingVisitReservationCount}; position={mealCore.GetVisitQueuePosition(visitors[2])}");

            mealCore.ReleaseVisitReservation(visitors[1]);
            bool mealThirdAfterRelease = mealCore.CanVisit(visitors[2], out string mealReopenReason);
            RecordAiCase(rows, failures, "dining_queue_reopens_after_release",
                mealThirdAfterRelease && mealCore.ActiveVisitReservationCount == 2,
                $"admitted={mealThirdAfterRelease}; reservations={mealCore.ActiveVisitReservationCount}; reason={mealReopenReason}");

            mealCore.ReleaseVisitReservation(visitors[0]);
            mealCore.ReleaseVisitReservation(visitors[2]);

            bool fifoOne = mealCore.TryReserveVisit(visitors[0], out _);
            bool fifoTwo = mealCore.TryReserveVisit(visitors[1], out _);
            bool fifoThree = mealCore.TryReserveVisit(visitors[2], out _);
            bool fifoFour = mealCore.TryReserveVisit(visitors[3], out _);
            int sequenceBeforeRefresh = mealCore.GetVisitQueuePosition(visitors[2]);
            bool refreshedSameVisitor = mealCore.TryReserveVisit(visitors[2], out _);
            int sequenceAfterRefresh = mealCore.GetVisitQueuePosition(visitors[2]);
            mealCore.ReleaseVisitReservation(visitors[1]);
            int thirdAfterMiddleCancel = mealCore.GetVisitQueuePosition(visitors[2]);
            int fourthAfterMiddleCancel = mealCore.GetVisitQueuePosition(visitors[3]);
            mealCore.ReleaseVisitReservation(visitors[0]);
            bool thirdAdmittedAfterHeadCancel = mealCore.CanVisit(visitors[2], out _);
            bool fourthAdmittedAfterHeadCancel = mealCore.CanVisit(visitors[3], out _);
            RecordAiCase(rows, failures, "dining_fifo_cancel_and_refresh_stability",
                fifoOne && fifoTwo && fifoThree && fifoFour
                && refreshedSameVisitor
                && sequenceBeforeRefresh == sequenceAfterRefresh
                && sequenceBeforeRefresh == 3
                && thirdAfterMiddleCancel == 2
                && fourthAfterMiddleCancel == 3
                && thirdAdmittedAfterHeadCancel
                && fourthAdmittedAfterHeadCancel,
                $"reserved={fifoOne}/{fifoTwo}/{fifoThree}/{fifoFour}; "
                + $"refresh={sequenceBeforeRefresh}->{sequenceAfterRefresh}; "
                + $"middleCancel={thirdAfterMiddleCancel}/{fourthAfterMiddleCancel}; "
                + $"headCancelAdmit={thirdAdmittedAfterHeadCancel}/{fourthAdmittedAfterHeadCancel}");
            foreach (CharacterActor visitor in visitors)
            {
                mealCore.ReleaseVisitReservation(visitor);
            }

            bool bridgeReserved = mealCore.TryReserveVisit(
                visitors[0],
                out string bridgeReserveReason);
            bool bridgeUse = mealCore.TryBeginUse(
                visitors[0].BuildingVisitor,
                out string bridgeUseReason);
            int completedBeforeBridgeAdmission = mealCore.FacilityState.completedUses;
            RecordAiCase(rows, failures, "dining_actor_adapter_identity",
                bridgeReserved
                && bridgeUse
                && mealCore.ActiveVisitReservationCount == 0
                && mealCore.CurrentUserCount == 1
                && mealCore.FacilityState.completedUses == completedBeforeBridgeAdmission,
                $"reserved={bridgeReserved}:{bridgeReserveReason}; "
                + $"use={bridgeUse}:{bridgeUseReason}; "
                 + $"reservations={mealCore.ActiveVisitReservationCount}; "
                 + $"users={mealCore.CurrentUserCount}; "
                 + $"completed={mealCore.FacilityState.completedUses}");
            bool bridgeCompleted = mealCore.CompleteUse(visitors[0].BuildingVisitor);
            RecordAiCase(rows, failures, "dining_completion_is_not_admission",
                bridgeCompleted
                && mealCore.CurrentUserCount == 0
                && mealCore.FacilityState.completedUses == completedBeforeBridgeAdmission + 1,
                $"completed={bridgeCompleted}; users={mealCore.CurrentUserCount}; "
                + $"uses={completedBeforeBridgeAdmission}->{mealCore.FacilityState.completedUses}");

            bool useFirst = mealCore.TryBeginUse(visitors[0], out string useFirstReason);
            bool useSecond = mealCore.TryBeginUse(visitors[1], out string useSecondReason);
            bool useThirdBlocked = !mealCore.TryBeginUse(visitors[2], out string useFullReason);
            mealCore.EndUse(visitors[0]);
            bool useThirdAfterExit = mealCore.TryBeginUse(visitors[2], out string useReopenReason);
            RecordAiCase(rows, failures, "dining_active_use_capacity",
                useFirst && useSecond && useThirdBlocked && useThirdAfterExit
                && mealCore.CurrentUserCount == 2,
                $"first={useFirst}:{useFirstReason}; second={useSecond}:{useSecondReason}; "
                + $"blocked={useThirdBlocked}:{useFullReason}; reopened={useThirdAfterExit}:{useReopenReason}; "
                + $"users={mealCore.CurrentUserCount}");
            mealCore.EndUse(visitors[1]);
            mealCore.EndUse(visitors[2]);

            bool[] shopReservations = visitors.Take(3)
                .Select(visitor => shop.TryReserveVisit(visitor, out _))
                .ToArray();
            bool shopFourthQueued = shop.TryReserveVisit(visitors[3], out string shopFullReason);
            RecordAiCase(rows, failures, "shop_service_reservation_queue",
                shop.HasAvailableStock
                && shop.EffectiveCapacity == 3
                && shopReservations.All(value => value)
                && shopFourthQueued
                && shop.ActiveVisitReservationCount == 4
                && shop.WaitingVisitReservationCount == 1,
                $"stock={shop.CurrentStock}; capacity={shop.EffectiveCapacity}; "
                + $"reservations={shop.ActiveVisitReservationCount}; waiting={shop.WaitingVisitReservationCount}; "
                + $"fourth={shopFourthQueued}:{shopFullReason}");
            foreach (CharacterActor visitor in visitors)
            {
                shop.ReleaseVisitReservation(visitor);
            }

            FacilityScoringContext scoringContext =
                FacilityScoringContext.WithoutReputationBiasForIsolatedTest(RoomFacilityPolicy);
            bool candidateBeforeDamage = FacilityCandidateScorer.IsCandidate(
                visitors[0], mealCore, FacilityRole.Meal, scoringContext, out string beforeDamageReason);
            mealCore.SetDamaged(true);
            bool candidateWhileDamaged = FacilityCandidateScorer.IsCandidate(
                visitors[0], mealCore, FacilityRole.Meal, scoringContext, out string damageReason);
            mealCore.SetDamaged(false);
            bool candidateAfterRepair = FacilityCandidateScorer.IsCandidate(
                visitors[0], mealCore, FacilityRole.Meal, scoringContext, out string afterRepairReason);
            RecordAiCase(rows, failures, "damaged_core_is_removed_from_ai_candidates",
                candidateBeforeDamage
                && !candidateWhileDamaged
                && damageReason.Contains("파손")
                && candidateAfterRepair,
                $"before={candidateBeforeDamage}:{beforeDamageReason}; damaged={candidateWhileDamaged}:{damageReason}; "
                + $"repaired={candidateAfterRepair}:{afterRepairReason}");

            BuildableObject door = created.First(item => item.BuildingData.category == BuildingCategory.Movement);
            Require(grid.RemoveOccupant(GridLayer.Building, door.buildPoses, false),
                "Could not remove the formal-room door for AI invalidation verification.");
            bool candidateWithoutDoor = FacilityCandidateScorer.IsCandidate(
                visitors[0], mealCore, FacilityRole.Meal, scoringContext, out string missingDoorReason);
            RecordAiCase(rows, failures, "missing_door_invalidates_room_candidate",
                !candidateWithoutDoor && !string.IsNullOrWhiteSpace(missingDoorReason),
                $"candidate={candidateWithoutDoor}; reason={missingDoorReason}; gridVersion={grid.version}");

            Require(grid.RegisterOccupant(door, GridLayer.Building, door.buildPoses, false),
                "Could not restore the formal-room door after AI invalidation verification.");
            bool candidateAfterDoorRestore = FacilityCandidateScorer.IsCandidate(
                visitors[0], mealCore, FacilityRole.Meal, scoringContext, out string doorRestoreReason);
            RecordAiCase(rows, failures, "restored_door_reenables_room_candidate",
                candidateAfterDoorRestore,
                $"candidate={candidateAfterDoorRestore}; reason={doorRestoreReason}; gridVersion={grid.version}");
        }
        finally
        {
            DestroyCreated(created);
            foreach (UnityEngine.Object item in cleanup.Where(item => item != null))
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        File.WriteAllLines(AiReportPath, rows);
        Require(failures.Count == 0, "Modular AI verification failed: " + string.Join(" | ", failures));
    }

    private static CharacterActor CreateReservationActor(
        string name,
        ICollection<UnityEngine.Object> cleanup)
    {
        GameObject actorObject = new GameObject(name);
        cleanup.Add(actorObject);
        CharacterActor actor = actorObject.AddComponent<CharacterActor>();
        actor.EnsureRuntimeState();
        actor.Identity.SetPersistentId(
            "character:modular-facility:" + name
                .ToLowerInvariant()
                .Replace(' ', '-'));
        // Visit reservations intentionally reject inactive, dead, or
        // transition-state characters. This edit-mode fixture has no normal
        // spawner lifecycle, so publish the same Active lifecycle state that
        // production admission requires before exercising the FIFO contract.
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        Require(
            actor.IsBuildingInteractionAvailable
            && actor.BuildingVisitor.IsBuildingInteractionAvailable,
            $"Reservation fixture '{name}' did not enter the production Active lifecycle.");
        return actor;
    }

    [MenuItem("Tools/DungeonStory/Validation/Run Equipment Maintenance Facility Contract")]
    public static void RunEquipmentMaintenanceFacilityContractFromMenu()
    {
        VerifyEquipmentMaintenanceRequiresFacilityAuthority();
        Debug.Log("Equipment maintenance facility authority contract passed.");
    }

    private static void VerifyEquipmentMaintenanceRequiresFacilityAuthority()
    {
        BuildingSO maintenanceOnly = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO nullSettings = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO valid = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            maintenanceOnly.name = "qa-maintenance-without-facility";
            maintenanceOnly.AbilityModules.Add(
                new BuildingEquipmentMaintenanceAbility());
            RequireInvalidAbilityContract(
                maintenanceOnly,
                "Maintenance ability without a facility authority was accepted.");

            nullSettings.name = "qa-maintenance-null-facility-settings";
            nullSettings.AbilityModules.Add(
                new BuildingFacilityAbility { settings = null });
            nullSettings.AbilityModules.Add(
                new BuildingEquipmentMaintenanceAbility());
            RequireInvalidAbilityContract(
                nullSettings,
                "Maintenance ability with null facility settings was accepted.");

            valid.name = "qa-maintenance-valid-facility";
            valid.AbilityModules.Add(
                new BuildingFacilityAbility { settings = new FacilityData() });
            valid.AbilityModules.Add(
                new BuildingEquipmentMaintenanceAbility());
            valid.ValidateAbilitiesOrThrow();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(maintenanceOnly);
            UnityEngine.Object.DestroyImmediate(nullSettings);
            UnityEngine.Object.DestroyImmediate(valid);
        }
    }

    private static void RequireInvalidAbilityContract(
        BuildingSO definition,
        string failureMessage)
    {
        bool rejected = false;
        try
        {
            definition.ValidateAbilitiesOrThrow();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, failureMessage);
    }

    private static void RecordAiCase(
        ICollection<string> rows,
        ICollection<string> failures,
        string key,
        bool passed,
        string detail)
    {
        string cleanDetail = (detail ?? string.Empty).Replace('\t', ' ').Replace('\n', ' ');
        rows.Add($"{key}\t{(passed ? "PASS" : "FAIL")}\t{cleanDetail}");
        if (!passed)
        {
            failures.Add(key + ": " + cleanDetail);
        }
    }

    private static Grid CreateFormalRoomGrid(
        int width,
        ICollection<BuildableObject> created,
        ICollection<UnityEngine.Object> cleanup)
    {
        Grid grid = new Grid(width, 1);
        for (int x = 0; x < width; x++)
        {
            Require(grid.RegisterOccupant(
                new TestHallwayOccupant(),
                GridLayer.Hallway,
                new[] { new Vector2Int(x, 0) },
                false),
                $"Could not register room hallway at {x}.");
        }

        CreateBoundary(grid, "Door", new Vector2Int(1, 0), BuildingCategory.Movement, created, cleanup);
        CreateBoundary(grid, "Wall", new Vector2Int(width - 2, 0), BuildingCategory.Wall, created, cleanup);
        return grid;
    }

    private static void VerifyShopProducts(Shop shop, StockCategory expected, string context)
    {
        RetailProductSnapshot[] products = shop.ProductSnapshots.ToArray();
        Require(products.Length > 0, $"{context} exposed no products.");
        Require(products.All(product => ShopStockCatalog.GetStockCategory(product.Id) == expected),
            $"{context} exposed a product outside {expected}.");
    }

    private static RetailStockLotSnapshot CreateScenarioRetailLot(
        SaleItem saleItem,
        string sourceOperationId)
    {
        ItemDefinitionId itemDefinitionId = default;
        long unitMassGrams = 0L;
        bool requiresUniqueInstance = false;
        bool hasDescriptor = saleItem != null
            && ShopStockCatalog.TryGetPhysicalDescriptor(
                    saleItem.id,
                    out itemDefinitionId,
                    out unitMassGrams,
                    out requiresUniqueInstance);
        Require(hasDescriptor,
            $"Sale item '{saleItem?.id}' has no exact physical descriptor.");
        return new RetailStockLotSnapshot
        {
            saleItemId = saleItem.id,
            itemDefinitionId = itemDefinitionId.Value,
            itemInstanceId = requiresUniqueInstance
                ? "fixture:retail-instance:wrong-category"
                : string.Empty,
            sourceStackId = requiresUniqueInstance
                ? "fixture:retail-stack:wrong-category"
                : string.Empty,
            quantity = 1,
            unitMassGrams = unitMassGrams,
            sourceOperationId = sourceOperationId
        };
    }

    private static GameSessionState CreateGameData()
    {
        GameSessionState gameData = new GameSessionState();
        gameData.gameSpeed.Initialize(1);
        gameData.holdingMoney.Initialize(5000);
        gameData.day.Initialize(7);
        gameData.curTime.Initialize(0f);
        gameData.hour.Initialize(0);
        gameData.timeOfDay.Initialize(TimeOfDay.Morning);
        return gameData;
    }

    private static void VerifyCatalogAssets()
    {
        BuildingSO[] assets = AssetDatabase.FindAssets("t:BuildingSO", new[] { BuildingFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where((asset) => asset != null)
            .OrderBy((asset) => asset.id)
            .ToArray();

        string[] codes = ModularFacilityAssetBuilder.GetCatalogCodes().ToArray();
        Require(assets.Length == codes.Length,
            $"Expected {codes.Length} modular BuildingSO assets, found {assets.Length}.");
        Require(assets.Select((asset) => asset.id).Distinct().Count() == assets.Length,
            "Modular building ids must be unique.");
        Require(assets.All(asset => !string.IsNullOrWhiteSpace(asset.GetFacilityCode())),
            "Every modular building needs a stable facility code.");
        Require(assets.Select(asset => asset.GetFacilityCode())
                .Distinct(StringComparer.Ordinal)
                .Count() == assets.Length,
            "Modular building facility codes must be unique.");
        Require(assets.All((asset) => asset.sprite != null && asset.icon != null), "Every modular facility needs a sprite and icon.");
        Require(assets.All((asset) => asset.runtimeArchetype.IsDefined()),
            "Every modular facility needs a defined runtime archetype.");
        HashSet<int> researchUnlocks = AssetDatabase.FindAssets(
                "t:ResearchProjectSO",
                new[] { "Assets/Resources/SO/Research" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null)
            .SelectMany(project => project.Unlocks.OfType<BlueprintBuildingUnlock>())
            .Select(unlock => unlock.buildingId)
            .ToHashSet();
        Require(assets.Where(asset => !asset.unlocked).All(asset => researchUnlocks.Contains(asset.id)),
            "Every locked modular facility needs a research project unlock.");
        Require(assets.All((asset) => asset.Facility == null || !asset.IsSelfContainedRoom()),
            "Modular facilities cannot claim self-contained rooms.");

        Require(codes.Length > 0
                && codes.Distinct(StringComparer.Ordinal).Count() == codes.Length,
            "Catalog codes must contain unique values.");
    }

    private static void VerifyIndependentGridSlots()
    {
        Grid grid = new Grid(8, 3);
        List<BuildableObject> created = new List<BuildableObject>();
        try
        {
            Vector2Int target = new Vector2Int(3, 0);
            BuildableObject floor = CreateAndRegister(grid, Load("D07"), target, created);
            BuildableObject wall = CreateAndRegister(grid, Load("D11"), target, created);
            BuildableObject ceiling = CreateAndRegister(grid, Load("E03"), target, created);
            BuildableObject overlay = CreateAndRegister(grid, Load("E04"), target, created);

            GridCell cell = grid.GetGridCell(target);
            Require(ReferenceEquals(cell.GetOccupant(GridLayer.Building), floor), "Floor facility did not occupy the Building slot.");
            Require(ReferenceEquals(cell.GetOccupant(GridLayer.WallFixture), wall), "Wall fixture did not occupy the WallFixture slot.");
            Require(ReferenceEquals(cell.GetOccupant(GridLayer.CeilingFixture), ceiling), "Ceiling fixture did not occupy the CeilingFixture slot.");
            Require(ReferenceEquals(cell.GetOccupant(GridLayer.FloorOverlay), overlay), "Floor overlay did not occupy the FloorOverlay slot.");
            Require(ReferenceEquals(cell.GetTopOccupant(), floor), "Floor facility must retain selection priority over mounted parts.");

            Require(wall.GetComponentInChildren<SpriteRenderer>() != null, "Wall fixture needs an independent SpriteRenderer.");
            Require(ceiling.GetComponentInChildren<SpriteRenderer>() != null, "Ceiling fixture needs an independent SpriteRenderer.");
            Require(overlay.GetComponentInChildren<SpriteRenderer>() != null, "Floor overlay needs an independent SpriteRenderer.");
            Require(wall.GetComponent<BoxCollider2D>()?.isTrigger == true, "Mounted fixture collider must not block character physics.");

            TestOccupant character = new TestOccupant(9999);
            Require(grid.RegisterOccupant(character, GridLayer.Character, new[] { target }, false), "Character registration failed.");
            Require(ReferenceEquals(cell.GetTopOccupant(), character), "Character must retain top selection priority.");
        }
        finally
        {
            DestroyCreated(created);
        }
    }

    private static void VerifyEveryPartCanInstantiateAndRegister()
    {
        BuildingSO[] assets = LoadAll();
        int requiredWidth = assets.Sum((asset) => Mathf.Max(1, asset.width) + 2) + 8;
        Grid grid = new Grid(requiredWidth, 1);
        List<BuildableObject> created = new List<BuildableObject>();
        try
        {
            int cursor = 4;
            foreach (BuildingSO asset in assets)
            {
                Vector2Int position = new Vector2Int(cursor + asset.width / 2, 0);
                BuildableObject instance = CreateAndRegister(grid, asset, position, created);
                Type expectedType = asset.runtimeArchetype.ToComponentType();
                Require(instance != null && instance.GetType() == expectedType,
                    $"{asset.name} created {instance?.GetType().Name ?? "<null>"}, expected {expectedType.Name}.");
                Require(instance.BuildingData == asset && instance.id == asset.id,
                    $"{asset.name} did not retain its BuildingSO identity.");

                if (asset.UsesIndependentRenderer)
                {
                    SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
                    Require(renderer != null && renderer.sprite == asset.sprite,
                        $"{asset.name} did not create its independent renderer.");
                    string expectedLayer = asset.layer == GridLayer.FloorOverlay
                        ? "DungeonHallway"
                        : "DungeonBackObject";
                    Require(renderer.sortingLayerName == expectedLayer,
                        $"{asset.name} uses unexpected sorting layer {renderer.sortingLayerName}.");
                    if (asset.layer == GridLayer.WallFixture || asset.layer == GridLayer.CeilingFixture)
                    {
                        Require(renderer.sortingOrder < 100,
                            $"{asset.name} mounted fixture should render behind room furniture, order {renderer.sortingOrder}.");
                        Require(renderer.transform.localPosition.y > 1.5f,
                            $"{asset.name} mounted fixture should attach to the upper wall/ceiling, local y {renderer.transform.localPosition.y}.");
                    }
                }

                cursor += Mathf.Max(1, asset.width) + 2;
            }

            Require(created.Count == assets.Length,
                $"Expected {assets.Length} runtime instances, created {created.Count}.");
            Facility[] runtimeWarehouses = created
                .OfType<Facility>()
                .Where(item => item.HasWarehouseInventory)
                .ToArray();
            int authoredWarehouseCount = assets.Count(asset =>
                asset.GetAbility<BuildingStorageAbility>()?.capacity > 0);
            Require(runtimeWarehouses.Length == authoredWarehouseCount,
                $"Expected {authoredWarehouseCount} runtime warehouses, "
                + $"created {runtimeWarehouses.Length}.");
            Require(runtimeWarehouses.All(item =>
                    item.Inventory.HasMassCapacityAuthority
                    && item.Inventory.MaxMassGrams > 0L),
                "A Modular runtime warehouse did not project positive gram authority.");
            Facility warehouse = created.FirstOrDefault((item) => item != null && item.id == 1050) as Facility;
            Require(warehouse != null && warehouse.HasWarehouseInventory,
                "L01 did not create its warehouse inventory.");
            Require(warehouse.Inventory.HasMassCapacityAuthority
                    && warehouse.Inventory.MaxMassGrams == 25_000L,
                "L01 warehouse inventory did not project its 25,000g authority.");
            Require(warehouse.Inventory.TotalStock == 0,
                "L01 warehouse must start empty; stock is derived from physical items only.");
            Facility cratePile = created.FirstOrDefault(
                (item) => item != null && item.id == 1051) as Facility;
            Require(cratePile != null && cratePile.HasWarehouseInventory,
                "L02 did not create its warehouse inventory.");
            Require(cratePile.Inventory.HasMassCapacityAuthority
                    && cratePile.Inventory.MaxMassGrams == 12_500L,
                "L02 runtime warehouse did not project its 12,500g authority.");
            Require(cratePile.Inventory.TotalStock == 0,
                "L02 warehouse must start empty; stock is derived from physical items only.");
            Require(created.FirstOrDefault((item) => item != null && item.id == 1012) is Shop,
                "S01 did not create a Shop runtime component.");
        }
        finally
        {
            DestroyCreated(created);
        }
    }

    private static void VerifyMountedPartsDoNotProvideFloorSupport()
    {
        Grid grid = new Grid(4, 3);
        List<BuildableObject> created = new List<BuildableObject>();
        try
        {
            Vector2Int baseCell = new Vector2Int(2, 0);
            CreateAndRegister(grid, Load("D11"), baseCell, created);
            GridPlacementValidator validator = new GridPlacementValidator();
            Require(
                !validator.HasSupportBelow(grid, new[] { baseCell + Vector2Int.up }),
                "A wall fixture must not provide structural support for the floor above.");

            CreateAndRegister(grid, Load("D07"), baseCell, created);
            Require(
                validator.HasSupportBelow(grid, new[] { baseCell + Vector2Int.up }),
                "A Building-layer occupant should continue to provide placement support.");
        }
        finally
        {
            DestroyCreated(created);
        }
    }

    private static void VerifyExtendedRoomRoles()
    {
        FacilityRole roles = (
            FacilityRole.Administration | FacilityRole.Security);
        Require((roles & FacilityRole.Administration) != 0, "Administration facility role did not create an administration room role.");
        Require((roles & FacilityRole.Security) != 0, "Security facility role did not create a security room role.");
        Require(RoomEnvironmentPresentation.GetRoomName(FacilityRole.Administration) == "사장실", "Administration room name is not connected.");
        Require(RoomEnvironmentPresentation.GetRoomName(FacilityRole.Security) == "경비실", "Security room name is not connected.");
    }

    private static void VerifyLegacyInitialPlacementRecipes()
    {
        Dictionary<int, BuildingSO> modularById = LoadAll().ToDictionary((asset) => asset.id);
        BuildingSO[] legacy = AssetDatabase.FindAssets("t:BuildingSO", new[]
            {
                "Assets/Resources/SO/Building"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(ModularFacilityInitialPlacementMigrator.IsLegacyMonolith)
            .Distinct()
            .ToArray();
        Require(legacy.Length == 21, $"Expected 21 legacy room recipes, found {legacy.Length}.");

        Vector2Int anchor = new Vector2Int(20, 0);
        foreach (BuildingSO monolith in legacy)
        {
            bool expanded = ModularFacilityInitialPlacementMigrator.TryExpand(
                new InitialBuildInfo { Position = anchor, Building = monolith },
                (id) => modularById.TryGetValue(id, out BuildingSO data) ? data : null,
                out IReadOnlyList<InitialBuildInfo> parts);
            Require(expanded && parts.Count >= 3, $"{monolith.name} did not expand to a usable modular recipe.");
            Require(parts.All((part) => part?.Building != null && part.Building.id >= 1000 && part.Building.id <= 1072),
                $"{monolith.name} recipe contains a non-modular part.");

            HashSet<Vector2Int> originalFootprint = monolith.GetGridPosList(anchor).ToHashSet();
            Require(parts.SelectMany((part) => part.Building.GetGridPosList(part.Position)).All(originalFootprint.Contains),
                $"{monolith.name} recipe extends outside its original footprint.");

            HashSet<string> occupiedSlots = new HashSet<string>(StringComparer.Ordinal);
            foreach (InitialBuildInfo part in parts)
            {
                foreach (Vector2Int cell in part.Building.GetGridPosList(part.Position))
                {
                    string key = $"{part.Building.layer}:{cell.x}:{cell.y}";
                    Require(occupiedSlots.Add(key), $"{monolith.name} recipe overlaps {key}.");
                }
            }
        }
    }

    private static void VerifyLegacyInitialRoomBoundariesRespectExterior()
    {
        Dictionary<int, BuildingSO> modularById = LoadAll().ToDictionary((asset) => asset.id);
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO stair = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Stair.asset");
        BuildingSO leftRoom = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset");
        BuildingSO rightRoom = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_ManaStorage.asset");
        Require(wall != null && wall.IsStructuralWall && !wall.IsInteriorDoor,
            "Initial room wall asset is not available.");
        Require(door != null && door.IsInteriorDoor, "Initial room door asset is not available.");
        Require(stair != null && stair.Placement.IsMovement, "Initial movement connector asset is not available.");
        Require(leftRoom != null, "Legacy research lab asset is not available.");
        Require(rightRoom != null, "Legacy mana storage asset is not available.");

        Vector2Int leftAnchor = new Vector2Int(20, 0);
        Vector2Int rightAnchor = new Vector2Int(26, 0);
        IReadOnlyList<InitialBuildInfo> expanded = ModularFacilityInitialPlacementMigrator.ExpandInitialRooms(
            new[]
            {
                new InitialBuildInfo { Position = leftAnchor, Building = leftRoom },
                new InitialBuildInfo { Position = rightAnchor, Building = rightRoom }
            },
            id => id == wall.id
                ? wall
                : id == door.id
                ? door
                : modularById.TryGetValue(id, out BuildingSO data)
                    ? data
                    : null);

        int leftStartX = leftAnchor.x - (leftRoom.width / 2);
        int rightStartX = rightAnchor.x - (rightRoom.width / 2);
        Vector2Int exteriorLeft = new Vector2Int(leftStartX - 1, leftAnchor.y);
        Vector2Int interiorLeftRoomRight = new Vector2Int(leftStartX + Mathf.Max(1, leftRoom.width), leftAnchor.y);
        Vector2Int interiorRightRoomLeft = new Vector2Int(rightStartX - 1, rightAnchor.y);
        Vector2Int exteriorRight = new Vector2Int(rightStartX + Mathf.Max(1, rightRoom.width), rightAnchor.y);

        Require(IsWallAt(expanded, exteriorLeft), "Initial row left exterior boundary must remain a wall.");
        Require(IsDoorAt(expanded, interiorLeftRoomRight), "Initial left room internal boundary must be a door.");
        Require(IsDoorAt(expanded, interiorRightRoomLeft), "Initial right room internal boundary must be a door.");
        Require(IsWallAt(expanded, exteriorRight), "Initial row right exterior boundary must remain a wall.");

        Vector2Int roomAnchor = new Vector2Int(20, 0);
        Vector2Int stairAnchor = new Vector2Int(25, 0);
        IReadOnlyList<InitialBuildInfo> expandedWithMovement = ModularFacilityInitialPlacementMigrator.ExpandInitialRooms(
            new[]
            {
                new InitialBuildInfo { Position = roomAnchor, Building = leftRoom },
                new InitialBuildInfo { Position = stairAnchor, Building = stair }
            },
            id => id == wall.id
                ? wall
                : id == door.id
                ? door
                : modularById.TryGetValue(id, out BuildingSO data)
                    ? data
                    : null);

        int roomStartX = roomAnchor.x - (leftRoom.width / 2);
        Vector2Int roomRightBoundary = new Vector2Int(roomStartX + Mathf.Max(1, leftRoom.width), roomAnchor.y);
        Require(IsDoorAt(expandedWithMovement, roomRightBoundary),
            "Initial room boundary facing a stair must be a door.");
    }

    private static bool IsWallAt(IEnumerable<InitialBuildInfo> placements, Vector2Int position)
    {
        BuildingSO building = placements.FirstOrDefault(item => item != null && item.Position == position)?.Building;
        return building != null && building.IsStructuralWall && !building.IsInteriorDoor;
    }

    private static bool IsDoorAt(IEnumerable<InitialBuildInfo> placements, Vector2Int position)
    {
        BuildingSO building = placements.FirstOrDefault(item => item != null && item.Position == position)?.Building;
        return building != null && building.IsInteriorDoor;
    }

    private static void VerifyAllLegacyRecipesAsFormalRooms()
    {
        Directory.CreateDirectory("Temp");
        Dictionary<int, BuildingSO> modularById = LoadAll().ToDictionary(asset => asset.id);
        BuildingSO[] legacy = AssetDatabase.FindAssets("t:BuildingSO", new[]
            {
                "Assets/Resources/SO/Building"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(ModularFacilityInitialPlacementMigrator.IsLegacyMonolith)
            .Distinct()
            .OrderBy(asset => asset.name, StringComparer.Ordinal)
            .ToArray();
        GameSessionState gameData = CreateGameData();
        NoopFloatingNumberFeedbackService numberFeedback = new NoopFloatingNumberFeedbackService();
        NoopWorkforceReplanService workforce = new NoopWorkforceReplanService();
        List<string> rows = new List<string>
        {
            "legacy\tparts\troomName\troles\tusable\thasDoor\treachableParts\tvisitableCores\tmountedParts\tresult"
        };
        List<string> failures = new List<string>();

        try
        {
            foreach (BuildingSO monolith in legacy)
            {
                List<BuildableObject> created = new List<BuildableObject>();
                List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
                int partCount = 0;
                int reachableCount = 0;
                int visitableCount = 0;
                int mountedCount = 0;
                string roomName = string.Empty;
                FacilityRole roles = FacilityRole.None;
                bool usable = false;
                bool hasDoor = false;
                string result = "PASS";
                try
                {
                    int width = Mathf.Max(14, monolith.width + 8);
                    Grid grid = CreateFormalRoomGrid(width, created, cleanup);
                    Vector2Int anchor = new Vector2Int(width / 2, 0);
                    Require(ModularFacilityInitialPlacementMigrator.TryExpand(
                            new InitialBuildInfo { Position = anchor, Building = monolith },
                            id => modularById.TryGetValue(id, out BuildingSO data) ? data : null,
                            out IReadOnlyList<InitialBuildInfo> recipe),
                        $"{monolith.name} did not expand.");
                    partCount = recipe.Count;

                    List<BuildableObject> parts = new List<BuildableObject>();
                    foreach (InitialBuildInfo placement in recipe)
                    {
                        BuildableObject part = CreateAndRegister(grid, placement.Building, placement.Position, created);
                        parts.Add(part);
                        if (part is Shop shop)
                        {
                            shop.ConstructShop(
                                new EditorGameMoneyAccount(gameData),
                                ShopStockCatalog,
                                numberFeedback,
                                workforce,
                                FacilityCrimeEditorTestDependencies.Evaluator,
                                new DungeonStory.Foundation.RandomStreamProvider(211), null, null, null,
                                CharacterAiEditorTestDependencies.RetailStockPhysical,
                                CharacterAiEditorTestDependencies.PhysicalStock);
                        }
                    }

                    RoomLayout layout = RoomRegistry.EditorCache.GetLayout(grid);
                    RoomInstance room = layout.Rooms.FirstOrDefault(candidate => candidate != null
                        && parts.Any(part => part.Facility != null
                            && part.Facility.roles != FacilityRole.None
                            && candidate.ContainsPart(part)));
                    Require(room != null, $"{monolith.name} did not produce a formal room.");
                    usable = room.IsUsable && !room.IsSelfContained;
                    hasDoor = room.HasDoor;
                    roles = room.Roles;
                    roomName = RoomEnvironmentPresentation.GetRoomName(roles);
                    FacilityRole facilityRoles = parts.Aggregate(
                        FacilityRole.None,
                        (current, part) => current | (part.Facility?.roles ?? FacilityRole.None));
                    FacilityRole expectedRoles = (facilityRoles);
                    Require(usable && hasDoor, $"{monolith.name} room is not closed and usable.");
                    Require(expectedRoles != FacilityRole.None && (roles & expectedRoles) == expectedRoles,
                        $"{monolith.name} roles {roles} do not contain {expectedRoles}.");
                    Require(!string.IsNullOrWhiteSpace(roomName), $"{monolith.name} has no room presentation name.");

                    List<IGridOccupant> reachable = grid.GetAllReachableOccupants(new Vector2Int(1, 0));
                    reachableCount = parts.Count(part => reachable.Contains(part));
                    Require(reachableCount == parts.Count,
                        $"{monolith.name} reachable parts {reachableCount}/{parts.Count}.");

                    BuildableObject[] visitorCores = parts
                        .Where(part => part.Facility != null && part.Facility.IsVisitorFacility)
                        .ToArray();
                    foreach (BuildableObject core in visitorCores)
                    {
                        Require(core.CanVisit(null, out string reason),
                            $"{monolith.name}/{core.BuildingData.objectName} is not usable: {reason}");
                    }
                    visitableCount = visitorCores.Length;

                    mountedCount = parts.Count(part => part.BuildingData.UsesIndependentRenderer);
                    foreach (BuildableObject mounted in parts.Where(part => part.BuildingData.UsesIndependentRenderer))
                    {
                        SpriteRenderer renderer = mounted.GetComponentInChildren<SpriteRenderer>();
                        Require(renderer != null && renderer.enabled && renderer.sprite == mounted.BuildingData.sprite,
                            $"{monolith.name}/{mounted.BuildingData.objectName} mounted renderer is invalid.");
                        Require(mounted.buildPoses.All(cell => ReferenceEquals(
                                grid.GetGridCell(cell).GetOccupant(mounted.BuildingData.layer),
                                mounted)),
                            $"{monolith.name}/{mounted.BuildingData.objectName} is not in its independent layer.");
                    }
                }
                catch (Exception exception)
                {
                    result = exception.Message.Replace('\t', ' ').Replace('\n', ' ');
                    failures.Add(monolith.name + ": " + result);
                }
                finally
                {
                    DestroyCreated(created);
                    foreach (UnityEngine.Object item in cleanup.Where(item => item != null))
                    {
                        UnityEngine.Object.DestroyImmediate(item);
                    }
                }

                rows.Add(string.Join("\t", new object[]
                {
                    monolith.name,
                    partCount,
                    roomName,
                    roles,
                    usable,
                    hasDoor,
                    reachableCount,
                    visitableCount,
                    mountedCount,
                    result
                }));
            }
        }
        finally
        {
        }

        File.WriteAllLines(RecipeReportPath, rows);
        Require(rows.Count == 22, $"Expected header plus 21 recipe rows, found {rows.Count}.");
        Require(failures.Count == 0, "Formal recipe failures: " + string.Join(" | ", failures));
    }

    private static void VerifyModularRoomComposition()
    {
        Grid grid = new Grid(12, 1);
        List<BuildableObject> created = new List<BuildableObject>();
        List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        try
        {
            for (int x = 0; x < grid.width; x++)
            {
                Require(grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new[] { new Vector2Int(x, 0) },
                    false),
                    $"Could not register room hallway at {x}.");
            }

            CreateBoundary(grid, "Door", new Vector2Int(1, 0), BuildingCategory.Movement, created, cleanup);
            CreateBoundary(grid, "Wall", new Vector2Int(10, 0), BuildingCategory.Wall, created, cleanup);
            BuildableObject researchDesk = CreateAndRegister(grid, Load("Q01"), new Vector2Int(4, 0), created);
            BuildableObject alchemyBench = CreateAndRegister(grid, Load("Q02"), new Vector2Int(7, 0), created);
            BuildableObject chandelier = CreateAndRegister(grid, Load("E03"), new Vector2Int(5, 0), created);

            RoomInstance room = RoomDetector.Build(grid).Rooms
                .FirstOrDefault((candidate) => candidate != null
                    && candidate.ContainsPart(researchDesk)
                    && candidate.ContainsPart(alchemyBench));
            Require(room != null && room.IsUsable && room.HasDoor,
                "Modular research room was not recognized as a usable formal room.");
            Require((room.Roles & FacilityRole.Research) != 0 && (room.Roles & FacilityRole.Mana) != 0,
                $"Unexpected modular room roles: {room.Roles}.");

            RoomEnvironmentSettingsSO settings = ScriptableObject.CreateInstance<RoomEnvironmentSettingsSO>();
            cleanup.Add(settings);
            RoomEnvironmentSnapshot snapshot = new RoomEnvironmentEvaluator(
                new TestRoomSettingsProvider(settings),
                new TestRecordProvider(), worldFilthQuery: null, survivalFoodRuntime: null, environmentalField: NoEnvironmentalFieldQuery.Instance)
                .Evaluate(grid, room);
            Require(snapshot.Status == RoomEnvironmentStatus.Usable && snapshot.IsEnvironmentActive,
                $"Unexpected modular room status: {snapshot.Status}.");
            Require(snapshot.Fixtures.Contains(chandelier) && snapshot.Luxury > 0f,
                "Ceiling fixture did not contribute to the modular room environment.");
            Require(snapshot.RoleContributions.Count == 2
                && snapshot.RoleContributions.First((entry) => entry.Role == FacilityRole.Research).Count == 2
                && snapshot.RoleContributions.First((entry) => entry.Role == FacilityRole.Mana).Count == 1,
                "Core modular facilities did not produce the expected composite role counts.");
            Require(snapshot.PrimaryRole == FacilityRole.Research && !snapshot.UsesMixedColor,
                "Research should be the unique primary role in the modular room.");
            Require(RoomEnvironmentPresentation.GetRoomName(snapshot.Roles) == "연구 + 마나",
                "Composite modular room name was not connected to the presentation layer.");
        }
        finally
        {
            DestroyCreated(created);
            foreach (UnityEngine.Object item in cleanup.Where((item) => item != null))
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }

    private static void CreateBoundary(
        Grid grid,
        string name,
        Vector2Int position,
        BuildingCategory category,
        ICollection<BuildableObject> created,
        ICollection<UnityEngine.Object> cleanup)
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.id = category == BuildingCategory.Wall ? -7002 : -7001;
        data.objectName = name;
        data.width = 1;
        data.height = 1;
        data.layer = GridLayer.Building;
        data.category = category;
        data.runtimeArchetype = category == BuildingCategory.Movement
            ? BuildingRuntimeArchetypeKind.Door
            : BuildingRuntimeArchetypeKind.Generic;
        data.Facility = new FacilityData();
        data.Evolution = new FacilityEvolutionContributionData();
        cleanup.Add(data);
        CreateAndRegister(grid, data, position, created);
    }

    private static BuildingSO[] LoadAll()
    {
        return AssetDatabase.FindAssets("t:BuildingSO", new[] { BuildingFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where((asset) => asset != null)
            .OrderBy((asset) => asset.id)
            .ToArray();
    }

    private static BuildingSO[] LoadEveryBuildingAsset()
    {
        return AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(asset => asset != null)
            .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static BuildingSO Load(string code)
    {
        string guid = AssetDatabase.FindAssets($"{code}_ t:BuildingSO", new[] { BuildingFolder })
            .FirstOrDefault();
        BuildingSO asset = !string.IsNullOrWhiteSpace(guid)
            ? AssetDatabase.LoadAssetAtPath<BuildingSO>(AssetDatabase.GUIDToAssetPath(guid))
            : null;
        if (asset == null)
        {
            throw new InvalidOperationException($"Could not load modular facility {code}.");
        }

        return asset;
    }

    private static BuildableObject CreateAndRegister(
        Grid grid,
        BuildingSO data,
        Vector2Int position,
        ICollection<BuildableObject> created)
    {
        GridBuildingObjectFactory factory = new GridBuildingObjectFactory();
        BuildableObject building = factory.Create(grid, data, position);
        Require(building != null, $"Failed to create {data.objectName}.");
        building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        building.ConstructDebugRules(DisabledDungeonDebugRuleQuery.Instance);
        building.ConstructBuildableObject(
            new BuildingResearchWorkPortAdapter(BlueprintResearchWorkService),
            FacilityCandidateCache,
            RoomFacilityPolicy,
            worldRegistry: (IBuildingWorldRegistryPort)
                CharacterAiEditorTestDependencies.WorldRegistry,
            abilityRuntimeDispatcher:
                CharacterAiEditorTestDependencies.BuildingAbilityRuntimeDispatcher,
            gameClock: CharacterAiEditorTestDependencies.GameClock,
            combatEquipmentRuntime: null,
            worldItemStackRuntime:
                new BuildingItemStackPortAdapter(OperationalPhysicalItems),
            paidFacilityContracts: null,
            evolutionState: new FacilityEvolutionStateComponentFactory());
        building.ConstructBuildableObjectEventBus(
            CharacterAiEditorTestDependencies.GameEvents,
            new BuildingVisitEventPublisher(
                CharacterAiEditorTestDependencies.GameEvents),
            new BuildingInfoPresentationAdapter(
                CharacterAiEditorTestDependencies.GameEvents));
        if (building is Facility facility)
        {
            facility.ConstructFacility(
                roomEnvironmentExperienceService: null,
                new EmptyStockQuery(),
                mealConsumptionRuntime: null,
                waterFixtureUseRuntime: null,
                wastewaterNetworkRuntime:
                    ModularFacilityWastewaterTransaction.Instance,
                serviceSessionRuntime: null,
                serviceRoomLinkRuntime: null,
                stockCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay);
        }
        building.SetGrid(grid);
        building.Initialization(data, position);
        Require(
            grid.RegisterOccupant(building, data.layer, data.GetGridPosList(position), false),
            $"Failed to register {data.objectName} in {data.layer}.");
        created.Add(building);
        return building;
    }

    private static void DestroyCreated(IEnumerable<BuildableObject> buildings)
    {
        foreach (BuildableObject building in buildings ?? Enumerable.Empty<BuildableObject>())
        {
            if (building != null)
            {
                UnityEngine.Object.DestroyImmediate(building.gameObject);
            }
        }
    }

    private sealed class ModularFacilityWastewaterTransaction :
        IFluidWastewaterTransaction
    {
        internal static readonly ModularFacilityWastewaterTransaction Instance =
            new ModularFacilityWastewaterTransaction();

        public bool TryAddWastewater(
            BuildableObject fixture,
            float amount,
            out float accepted,
            out DomainFailure failure)
        {
            accepted = Mathf.Max(0f, amount);
            failure = default;
            return true;
        }

        public bool TryConsumeWastewater(
            BuildableObject processor,
            float amount,
            out float consumed)
        {
            consumed = Mathf.Max(0f, amount);
            return true;
        }

        public bool CanAcceptWastewater(
            BuildableObject fixture,
            float amount,
            out DomainFailure failure)
        {
            failure = default;
            return amount >= 0f;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestOccupant : IGridOccupant
    {
        public TestOccupant(int id)
        {
            GridId = id;
        }

        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class EmptyStockQuery :
        IStockQuery,
        IWarehousePhysicalMassQueryPort
    {
        public int PhysicalItemStackVersion => 0;
        public long PhysicalMassAuthorityRevision => 0L;
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
        public long GetWarehouseStoredMassGrams(
            BuildingInstanceId warehouseId) => 0L;
        public long GetWarehouseStoredMassRevision(
            BuildingInstanceId warehouseId) => 0L;
        public long GetDefinitionUnitMassGrams(string itemDefinitionId) => 1L;
    }

    private sealed class TestRoomSettingsProvider : IRoomEnvironmentSettingsProvider
    {
        public TestRoomSettingsProvider(RoomEnvironmentSettingsSO settings)
        {
            Settings = settings;
        }

        public RoomEnvironmentSettingsSO Settings { get; }
    }

    private sealed class TestRecordProvider : IFacilityEvolutionRecordProvider
    {
        private readonly Dictionary<BuildableObject, FacilityEvolutionRecord> records =
            new Dictionary<BuildableObject, FacilityEvolutionRecord>();

        public FacilityEvolutionRecord GetRecord(BuildableObject facility)
        {
            if (facility == null)
            {
                return new FacilityEvolutionRecord();
            }

            if (!records.TryGetValue(facility, out FacilityEvolutionRecord record))
            {
                record = new FacilityEvolutionRecord();
                records[facility] = record;
            }

            return record;
        }
    }

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return false;
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            return new BlueprintResearchWorkResult(false, null, 0f, 0f, 1f, false, "No research runtime in modular fixture.");
        }

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits) =>
            ApplyResearchWork(researcher, researchFacility, approvedWorkUnits);
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick() => false;
        public bool TryTriggerCharacterUnderPointer() => false;

        public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacterAtScreenPosition(Vector3 screenPosition, Camera camera, out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
    }

    private sealed class AssetDatabaseShopStockCatalog : IShopStockCatalog
    {
        public bool TryGetStockInfoForShop(int shopId, out StockInfo stockInfo)
        {
            stockInfo = AssetDatabase.FindAssets("t:StockInfo", new[] { "Assets/Resources/SO/Stock" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StockInfo>)
                .FirstOrDefault(candidate => candidate != null && candidate.shopId == shopId);
            return stockInfo != null;
        }

        public bool TryGetSaleItem(int saleItemId, out SaleItem saleItem)
        {
            saleItem = LoadSaleItems().FirstOrDefault(candidate => candidate.id == saleItemId);
            return saleItem != null;
        }

        public bool TryGetPhysicalDescriptor(
            int saleItemId,
            out ItemDefinitionId itemDefinitionId,
            out long unitMassGrams,
            out bool requiresUniqueInstance)
        {
            itemDefinitionId = default;
            unitMassGrams = 0L;
            requiresUniqueInstance = false;
            if (!TryGetSaleItem(saleItemId, out SaleItem saleItem)) return false;
            ItemDefinitionSO definition = AssetDatabase
                .FindAssets("t:ItemDefinitionSO", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.StableId.Equals(saleItem.ItemDefinitionId));
            if (definition == null) return false;
            itemDefinitionId = definition.StableId;
            unitMassGrams = PhysicalMassGrams
                .FromCanonicalKilograms(definition.UnitWeight)
                .Value;
            requiresUniqueInstance = definition.MaxStack == 1;
            return true;
        }

        public bool TryGetSaleItemByCategory(StockCategory category, out SaleItem saleItem)
        {
            saleItem = LoadSaleItems().FirstOrDefault(candidate => candidate.category == category);
            return saleItem != null;
        }

        public StockCategory GetStockCategory(int saleItemId)
        {
            return TryGetSaleItem(saleItemId, out SaleItem saleItem)
                ? saleItem.category
                : StockCategory.General;
        }

        private static IEnumerable<SaleItem> LoadSaleItems()
        {
            return AssetDatabase.FindAssets("t:SaleItem", new[] { "Assets/Resources/SO/Stock" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SaleItem>)
                .Where(item => item != null);
        }
    }

    private sealed class FixedGameDataProvider : IGameSessionStateProvider
    {
        private readonly GameSessionState gameData;

        public FixedGameDataProvider(GameSessionState gameData)
        {
            this.gameData = gameData;
        }

        public bool TryGetSessionState(out GameSessionState resolvedGameData)
        {
            resolvedGameData = gameData;
            return resolvedGameData != null;
        }
    }

    private sealed class NoopFloatingNumberFeedbackService : IFloatingNumberFeedbackService
    {
        public bool TryShow(NumberCondition condition, Vector3 worldPosition, float value) => false;
    }

    private sealed class ImmediateBuildingDestructiveLossFixture :
        IBuildingDestructiveLossRuntime
    {
        internal static readonly ImmediateBuildingDestructiveLossFixture Instance =
            new ImmediateBuildingDestructiveLossFixture();

        private ImmediateBuildingDestructiveLossFixture()
        {
        }

        public BuildingDestructiveLossResult Apply(
            BuildableObject building,
            ProductionFacilityDestructiveDrainCause cause)
        {
            if (building == null
                || cause != ProductionFacilityDestructiveDrainCause
                    .ExplicitDemolition)
            {
                return new BuildingDestructiveLossResult(
                    BuildingDestructiveLossDisposition.Conflict,
                    "invalid modular facility demolition fixture request");
            }

            building.DestroySelf();
            return new BuildingDestructiveLossResult(
                BuildingDestructiveLossDisposition.RemovedAwaitingCheckpointGc,
                string.Empty);
        }
    }

    private sealed class NoopWorkforceReplanService : IWorkforceReplanService
    {
        public void RequestIdleWorkersToReplan(bool clearFailures = true)
        {
        }

        public void RequestOneWorkerToReplanFor(
            WorkTypeId workTypeId,
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
        }

        public void RequestOneHaulerToReplan(
            bool clearFailures = true,
            bool forceInterrupt = false,
            CharacterId protectedCharacterId = default,
            bool forcePriorityWakeFanout = false)
        {
        }

    }
}
