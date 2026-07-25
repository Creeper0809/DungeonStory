using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BuildingStatePersistenceDebugScenarios
{
    public const string ReportPath = "Temp/building-state-persistence-report.tsv";

    [MenuItem("DungeonStory/Debug/Modular Facilities/Run State Persistence Contracts")]
    public static void RunAll()
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("generic_stock_categories", VerifyGenericStockCategories, lines, errors);
        Run("component_module_round_trip", VerifyComponentModuleRoundTrip, lines, errors);
        Run("unlisted_ability_dispatch", VerifyUnlistedAbilityDispatch, lines, errors);
        Run("module_restore_diagnostics", VerifyModuleRestoreDiagnostics, lines, errors);
        Run("world_v1_migration", VerifyWorldV1Migration, lines, errors);
        Run("legacy_shared_state_split", VerifyLegacySharedStateSplit, lines, errors);
        Run("v2_writer_schema", VerifyV2WriterSchema, lines, errors);

        File.WriteAllLines(ReportPath, lines);
        if (errors.Count == 0)
        {
            Debug.Log($"Building state persistence contracts PASS. Report: {ReportPath}");
        }
        else
        {
            Debug.LogError(
                $"Building state persistence contracts FAIL ({errors.Count}): {string.Join(" | ", errors)}. "
                + $"Report: {ReportPath}");
        }
    }

    private static string VerifyGenericStockCategories()
    {
        StockCategory customCategory = (StockCategory)777;
        WarehouseInventory source = new WarehouseInventory(200);
        source.AddStock(StockCategory.Food, 11);
        source.AddStock(customCategory, 37);

        string json = JsonUtility.ToJson(source.CreateSnapshot());
        WarehouseInventorySnapshot parsed = JsonUtility.FromJson<WarehouseInventorySnapshot>(json);
        WarehouseInventory restored = new WarehouseInventory();
        Require(restored.TryApplySnapshot(parsed, out string restoreError), restoreError);
        Require(restored.GetStock(StockCategory.Food) == 11, "known stock category did not round-trip");
        Require(restored.GetStock(customCategory) == 37, "custom stock category did not round-trip");
        Require(parsed.stocks.Count == 2, $"expected 2 stock entries, got {parsed.stocks.Count}");

        WarehouseInventorySnapshot invalid = source.CreateSnapshot();
        invalid.stocks.Add(new StockAmountSnapshot { categoryId = "not-a-stock-id", amount = 9 });
        Require(!restored.TryApplySnapshot(invalid, out string invalidError), "invalid category id was accepted");
        Require(invalidError.Contains("not-a-stock-id"), "invalid category diagnostic omitted the id");
        Require(restored.GetStock(customCategory) == 37, "failed restore mutated existing inventory");
        return $"entries={parsed.stocks.Count}; customId={StockCategoryPersistenceId.ToId(customCategory)}; amount=37";
    }

    private static string VerifyComponentModuleRoundTrip()
    {
        GameObject sourceObject = new GameObject("StateModuleSource");
        GameObject targetObject = new GameObject("StateModuleTarget");
        try
        {
            BuildableObject source = sourceObject.AddComponent<BuildableObject>();
            PersistenceContractStateModule sourceModule = sourceObject.AddComponent<PersistenceContractStateModule>();
            sourceModule.Value = 73;
            List<BuildingStateModuleSaveData> snapshots = BuildingStateModulePersistence.Capture(source);
            Require(snapshots.Count == 1, $"expected one discovered component module, got {snapshots.Count}");
            Require(snapshots[0].moduleId == PersistenceContractStateModule.Id, "unexpected component module id");

            snapshots[0].version = 1;
            snapshots[0].payload = JsonUtility.ToJson(new PersistenceContractStateModule.LegacyPayload { legacyValue = 73 });

            BuildableObject target = targetObject.AddComponent<BuildableObject>();
            PersistenceContractStateModule targetModule = targetObject.AddComponent<PersistenceContractStateModule>();
            BuildingStateModuleRestoreResult result = BuildingStateModulePersistence.Restore(target, snapshots);
            Require(result.Success, string.Join(" | ", result.errors));
            Require(targetModule.Value == 73, $"migrated value was {targetModule.Value}");
            Require(result.restoredModuleIds.SequenceEqual(new[] { PersistenceContractStateModule.Id }), "restored module id missing");
            return $"module={snapshots[0].moduleId}; v1->v{targetModule.CurrentVersion}; value={targetModule.Value}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(targetObject);
        }
    }

    private static string VerifyModuleRestoreDiagnostics()
    {
        GameObject targetObject = new GameObject("StateModuleDiagnostics");
        try
        {
            BuildableObject target = targetObject.AddComponent<BuildableObject>();
            targetObject.AddComponent<PersistenceContractStateModule>();

            BuildingStateModuleRestoreResult missing = BuildingStateModulePersistence.Restore(
                target,
                Array.Empty<BuildingStateModuleSaveData>());
            Require(missing.Success, "missing current module should retain defaults with a warning");
            Require(missing.warnings.Any(message => message.Contains(PersistenceContractStateModule.Id)), "missing-module warning omitted module id");

            BuildingStateModuleSaveData unknown = new BuildingStateModuleSaveData
            {
                moduleId = "test.unknown",
                version = 1,
                payload = "{}"
            };
            BuildingStateModuleRestoreResult unknownResult = BuildingStateModulePersistence.Restore(target, new[] { unknown });
            Require(!unknownResult.Success, "unknown saved module was accepted");
            Require(unknownResult.errors.Any(message => message.Contains("test.unknown")), "unknown-module error omitted module id");

            BuildingStateModuleSaveData duplicate = new BuildingStateModuleSaveData
            {
                moduleId = PersistenceContractStateModule.Id,
                version = 2,
                payload = JsonUtility.ToJson(new PersistenceContractStateModule.CurrentPayload { value = 1 })
            };
            BuildingStateModuleRestoreResult duplicateResult = BuildingStateModulePersistence.Restore(
                target,
                new[] { duplicate, duplicate });
            Require(!duplicateResult.Success, "duplicate saved module was accepted");
            Require(duplicateResult.errors.Any(message => message.Contains("duplicate")), "duplicate-module error was not explicit");
            return $"missingWarnings={missing.warnings.Count}; unknownErrors={unknownResult.errors.Count}; duplicateErrors={duplicateResult.errors.Count}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
        }
    }

    private static string VerifyUnlistedAbilityDispatch()
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject gameObject = new GameObject("UnlistedAbilityDispatch");
        try
        {
            data.objectName = "Unlisted Ability Fixture";
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = BuildingCategory.Shop;
            data.type = typeof(BuildableObject);
            data.ReplaceAbilities(new BuildingAbilityCollection());
            UnlistedWorkAbility ability = UnlistedWorkAbility.Create();
            data.AbilityModules.Add(ability);

            BuildableObject building = gameObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            SetAbilityDispatcher(
                building,
                new BuildingAbilityRuntimeDispatcher(
                    new IBuildingAbilityWorkCompletedHandler[]
                    {
                        new UnlistedWorkAbilityHandler()
                    },
                    Array.Empty<IBuildingWorkCompletionFallbackHandler>()));
            building.Initialization(data, Vector2Int.zero);
            int output = ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                null,
                building,
                BuiltInWorkTypeIds.Operate);
            UnlistedWorkStateModule state = building.RequireStateModule<UnlistedWorkStateModule>(
                BuildingStateModuleIds.ForAbility("contract", ability.AbilityId));

            Require(output == 7, $"unlisted ability output={output}");
            Require(state.ExecutionCount == 1, $"unlisted ability executions={state.ExecutionCount}");
            Require(BuildingStateModulePersistence.Capture(building)
                    .Any(module => module.moduleId == state.ModuleId),
                "unlisted ability state did not enter persistence");
            return $"output={output}; executions={state.ExecutionCount}; module={state.ModuleId}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static string VerifyWorldV1Migration()
    {
        LegacyWorldV1 legacy = new LegacyWorldV1
        {
            version = 1,
            gridWidth = 12,
            gridHeight = 3,
            buildings = new List<LegacyBuildingV1>
            {
                new LegacyBuildingV1
                {
                    buildingId = 42,
                    objectName = "Legacy Facility",
                    layer = GridLayer.Building,
                    centerX = 4,
                    centerY = 1,
                    facilityLevel = 2,
                    operationalState = new LegacyFacilityOperationalStateV1
                    {
                        completedUses = 8,
                        completedWorkCycles = 3,
                        producedStock = 6,
                        alarmCharges = 2,
                        cleanliness = 44f
                    },
                    hasWarehouseSnapshot = true,
                    warehouseSnapshot = new WarehouseInventorySnapshotV1
                    {
                        maxCapacity = 50,
                        restrictCategory = false,
                        acceptedCategory = StockCategory.General,
                        food = 4,
                        general = 5,
                        weapon = 6,
                        mana = 7
                    },
                    hasShopStockSnapshot = true,
                    shopStockSnapshot = new ShopStockStateSnapshot
                    {
                        items = new List<ShopStockItemSnapshot>
                        {
                            new ShopStockItemSnapshot { saleItemId = 9, amount = 3 }
                        }
                    }
                }
            }
        };

        ModularFacilityWorldSaveService service = new ModularFacilityWorldSaveService(
            _ => null,
            new GridBuildingFactory(_ => { }));
        ModularFacilityWorldSaveData migrated = service.FromJson(JsonUtility.ToJson(legacy));
        Require(migrated.version == ModularFacilityWorldSaveService.CurrentVersion, $"migrated version={migrated.version}");
        Require(migrated.migratedFromVersion == 1, $"migratedFrom={migrated.migratedFromVersion}");
        Require(migrated.migrationWarnings.Count > 0, "migration did not report itself");
        Require(migrated.buildings.Count == 1, $"migrated buildings={migrated.buildings.Count}");

        Dictionary<string, BuildingStateModuleSaveData> modules = migrated.buildings[0].stateModules
            .ToDictionary(module => module.moduleId, StringComparer.Ordinal);
        Require(modules.Count == 3, $"migrated modules={modules.Count}");
        Require(modules.ContainsKey(BuildingStateModuleIds.FacilityOperation), "operational module missing");
        Require(modules.ContainsKey(BuildingStateModuleIds.WarehouseInventory), "warehouse module missing");
        Require(modules.ContainsKey(BuildingStateModuleIds.ShopStock), "shop module missing");

        WarehouseInventorySnapshot warehouse = JsonUtility.FromJson<WarehouseInventorySnapshot>(
            modules[BuildingStateModuleIds.WarehouseInventory].payload);
        WarehouseInventory restored = new WarehouseInventory();
        Require(restored.TryApplySnapshot(warehouse, out string error), error);
        Require(restored.GetStock(StockCategory.Food) == 4, "legacy food stock migration failed");
        Require(restored.GetStock(StockCategory.General) == 5, "legacy general stock migration failed");
        Require(restored.GetStock(StockCategory.Weapon) == 6, "legacy weapon stock migration failed");
        Require(restored.GetStock(StockCategory.Mana) == 7, "legacy mana stock migration failed");
        return $"v1->v{migrated.version}; modules={modules.Count}; warnings={migrated.migrationWarnings.Count}";
    }

    private static string VerifyV2WriterSchema()
    {
        ModularFacilityWorldSaveService service = new ModularFacilityWorldSaveService(
            _ => null,
            new GridBuildingFactory(_ => { }));
        ModularFacilityWorldSaveData snapshot = new ModularFacilityWorldSaveData
        {
            buildings = new List<ModularFacilityBuildingSaveData>
            {
                new ModularFacilityBuildingSaveData
                {
                    buildingId = 1,
                    stateModules = new List<BuildingStateModuleSaveData>
                    {
                        new BuildingStateModuleSaveData
                        {
                            moduleId = "test.module",
                            version = 1,
                            payload = "{\"value\":2}"
                        }
                    }
                }
            }
        };
        string json = service.ToJson(snapshot);
        Require(json.Contains("\"stateModules\""), "v2 writer omitted stateModules");
        Require(!json.Contains("operationalState"), "v2 writer still emitted fixed operationalState");
        Require(!json.Contains("hasWarehouseSnapshot"), "v2 writer still emitted fixed warehouse flag");
        Require(!json.Contains("hasShopStockSnapshot"), "v2 writer still emitted fixed shop flag");
        return $"jsonLength={json.Length}; fixedFields=0";
    }

    private static string VerifyLegacySharedStateSplit()
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject gameObject = new GameObject("LegacySharedStateSplit");
        try
        {
            data.objectName = "State Split Fixture";
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = BuildingCategory.Shop;
            data.type = typeof(BuildableObject);
            data.ReplaceAbilities(new BuildingAbilityCollection());
            BuildingProductionAbility productionAbility = new BuildingProductionAbility
            {
                outputCategory = StockCategory.General,
                amount = 1
            };
            BuildingSecurityAbility securityAbility = new BuildingSecurityAbility
            {
                maxAlarmCharges = 3,
                chargesPerGuardWork = 1
            };
            data.AbilityModules.Add(productionAbility);
            data.AbilityModules.Add(securityAbility);

            BuildableObject building = gameObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.Initialization(data, Vector2Int.zero);

            LegacyFacilityOperationalStateV1 legacy = new LegacyFacilityOperationalStateV1
            {
                completedUses = 9,
                completedWorkCycles = 4,
                producedStock = 12,
                alarmCharges = 2,
                cleanliness = 38f
            };
            BuildingStateModuleRestoreResult result = BuildingStateModulePersistence.Restore(
                building,
                new[]
                {
                    new BuildingStateModuleSaveData
                    {
                        moduleId = BuildingStateModuleIds.FacilityOperation,
                        version = 1,
                        payload = JsonUtility.ToJson(legacy)
                    }
                });

            Require(result.Success, string.Join(" | ", result.errors));
            Require(building.FacilityState.completedUses == 9, "legacy completed uses were not restored");
            Require(building.FacilityState.completedWorkCycles == 4, "legacy work cycles were not restored");
            Require(Mathf.Approximately(building.FacilityState.cleanliness, 38f), "legacy cleanliness was not restored");

            BuildingProductionStateModule production = building.RequireStateModule<BuildingProductionStateModule>(
                BuildingStateModuleIds.ForAbility("production", productionAbility.AbilityId));
            BuildingSecurityStateModule security = building.RequireStateModule<BuildingSecurityStateModule>(
                BuildingStateModuleIds.ForAbility("security", securityAbility.AbilityId));
            Require(production.ProducedStock == 12, $"legacy produced stock={production.ProducedStock}");
            Require(security.AlarmCharges == 2, $"legacy alarm charges={security.AlarmCharges}");
            return $"core=9/4/38; production={production.ProducedStock}; security={security.AlarmCharges}; warnings={result.warnings.Count}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static void Run(
        string name,
        Func<string> scenario,
        List<string> lines,
        List<string> errors)
    {
        try
        {
            string details = scenario();
            lines.Add($"{name}\tPASS\t{Sanitize(details)}");
        }
        catch (Exception ex)
        {
            string details = Sanitize(ex.Message);
            lines.Add($"{name}\tFAIL\t{details}");
            errors.Add($"{name}: {details}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string Sanitize(string value)
    {
        return (value ?? string.Empty).Replace('\t', ' ').Replace(Environment.NewLine, " ");
    }

    private static void SetAbilityDispatcher(
        BuildableObject building,
        IBuildingAbilityRuntimeDispatcher dispatcher)
    {
        FieldInfo field = typeof(BuildableObject).GetField(
            "abilityRuntimeDispatcher",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new MissingFieldException(
                typeof(BuildableObject).FullName,
                "abilityRuntimeDispatcher");
        }

        field.SetValue(building, dispatcher);
    }

    [Serializable]
    private sealed class LegacyWorldV1
    {
        public int version = 1;
        public int gridWidth;
        public int gridHeight;
        public ModularFacilityGameDataSaveData gameData = new ModularFacilityGameDataSaveData();
        public List<LegacyBuildingV1> buildings = new List<LegacyBuildingV1>();
    }

    [Serializable]
    private sealed class UnlistedWorkAbility : BuildingAbility,
        IBuildingWorkCompletionAbility,
        IBuildingRuntimeStateAbility
    {
        private UnlistedWorkAbility()
        {
        }

        public static UnlistedWorkAbility Create()
        {
            return new UnlistedWorkAbility();
        }

        public IBuildingStateModule CreateStateModule(BuildableObject building)
        {
            return new UnlistedWorkStateModule(AbilityId);
        }

    }

    private sealed class UnlistedWorkAbilityHandler :
        BuildingAbilityWorkCompletedHandler<UnlistedWorkAbility>
    {
        protected override int Apply(
            UnlistedWorkAbility ability,
            BuildingAbilityWorkContext context)
        {
            if (context.WorkTypeId != BuiltInWorkTypeIds.Operate)
            {
                return 0;
            }

            UnlistedWorkStateModule state =
                context.Building.RequireStateModule<UnlistedWorkStateModule>(
                    BuildingStateModuleIds.ForAbility("contract", ability.AbilityId));
            state.Increment();
            return 7;
        }
    }

    private sealed class UnlistedWorkStateModule : IBuildingStateModule
    {
        [Serializable]
        private sealed class State
        {
            public int executionCount;
        }

        private readonly State state = new State();

        public UnlistedWorkStateModule(string abilityId)
        {
            ModuleId = BuildingStateModuleIds.ForAbility("contract", abilityId);
        }

        public string ModuleId { get; }
        public int CurrentVersion => 1;
        public int ExecutionCount => state.executionCount;

        public void Increment()
        {
            state.executionCount++;
        }

        public string CaptureState()
        {
            return JsonUtility.ToJson(state);
        }

        public bool TryRestoreState(int version, string payload, out string error)
        {
            if (version != CurrentVersion)
            {
                error = $"unsupported version {version}";
                return false;
            }

            State restored = JsonUtility.FromJson<State>(payload);
            state.executionCount = Mathf.Max(0, restored?.executionCount ?? 0);
            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    private sealed class LegacyBuildingV1
    {
        public int buildingId;
        public string code;
        public string objectName;
        public GridLayer layer;
        public int centerX;
        public int centerY;
        public int width;
        public int height;
        public bool isDamaged;
        public int facilityLevel = 1;
        public LegacyFacilityOperationalStateV1 operationalState = new LegacyFacilityOperationalStateV1();
        public bool hasWarehouseSnapshot;
        public WarehouseInventorySnapshotV1 warehouseSnapshot;
        public bool hasShopStockSnapshot;
        public ShopStockStateSnapshot shopStockSnapshot;
    }
}

internal sealed class PersistenceContractStateModule : MonoBehaviour, IBuildingStateModule
{
    public const string Id = "test.open-state-module";
    public int Value { get; set; }
    public string ModuleId => Id;
    public int CurrentVersion => 2;

    public string CaptureState()
    {
        return JsonUtility.ToJson(new CurrentPayload { value = Value });
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        if (version == 1)
        {
            LegacyPayload legacy = JsonUtility.FromJson<LegacyPayload>(payload);
            Value = legacy?.legacyValue ?? 0;
            error = string.Empty;
            return true;
        }

        if (version == CurrentVersion)
        {
            CurrentPayload current = JsonUtility.FromJson<CurrentPayload>(payload);
            Value = current?.value ?? 0;
            error = string.Empty;
            return true;
        }

        error = $"unsupported test module version {version}";
        return false;
    }

    [Serializable]
    public sealed class LegacyPayload
    {
        public int legacyValue;
    }

    [Serializable]
    public sealed class CurrentPayload
    {
        public int value;
    }
}
