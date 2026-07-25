using System;
using System.Collections.Generic;
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
        Run("save_v10_contract", VerifySaveContract, errors);
        Run("stock_categories", VerifyStockCategories, errors);
        Run("work_types", VerifyWorkTypes, errors);
        Run("survival_item_definitions", VerifySurvivalItemDefinitions, errors);
        Run("ability_modules", VerifyAbilityModules, errors);
        Run("room_snapshot_survival_metrics", VerifyRoomSnapshotMetrics, errors);
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
        Require(DungeonGameSaveData.CurrentVersion == 15, "game save version is not V15");
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
        Require(save.version == DungeonGameSaveData.CurrentVersion, "new save did not default to V15");
        Require(survival.version == DungeonSurvivalSaveData.CurrentVersion, "survival save version mismatch");
        return $"game={save.version}; survival={survival.version}";
    }

    private static string VerifyStockCategories()
    {
        StockCategoryCatalog.ResetToBuiltIns();
        Require(StockCategoryCatalog.TryGet(StockCategory.Water, out StockCategoryDefinition water)
            && water.DisplayName == "물", "water stock category missing");
        Require(StockCategoryCatalog.TryGet(StockCategory.Medicine, out _), "medicine stock category missing");
        Require(StockCategoryCatalog.TryGet(StockCategory.Fuel, out _), "fuel stock category missing");
        Require(StockCategoryPersistenceId.TryParse("stock:water", out StockCategory parsed)
            && parsed == StockCategory.Water, "water persistence id did not parse");
        return string.Join(", ", StockCategoryCatalog.All);
    }

    private static string VerifyWorkTypes()
    {
        WorkTypeCatalog.ResetToBuiltIns();
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.DrawWater, out WorkTypeDefinition water)
            && water.DisplayName == "급수", "draw water work type missing");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Cook, out _), "cook work type missing");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Treat, out _), "treat work type missing");
        Require(WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Refuel, out _), "refuel work type missing");
        return $"tasks={WorkTypeCatalog.All.Count}";
    }

    private static string VerifySurvivalItemDefinitions()
    {
        Require(SurvivalItemDefinitions.TryGetDefinition(
            SurvivalItemDefinitions.CookedMealItemId,
            out DungeonItemDefinition cooked)
            && cooked.StockCategory == StockCategory.Food,
            "cooked meal definition missing");
        Require(DungeonItemCatalogSO.TryGetStockCategoryFromItemId(
            SurvivalItemDefinitions.PreservedFoodItemId,
            out StockCategory parsed)
            && parsed == StockCategory.Food,
            "preserved food did not aggregate to Food");
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
