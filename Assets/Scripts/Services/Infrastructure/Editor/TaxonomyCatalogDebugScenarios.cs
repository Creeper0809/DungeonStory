using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TaxonomyCatalogDebugScenarios
{
    public const string ReportPath = "Temp/taxonomy-catalog-report.tsv";

    [MenuItem("DungeonStory/Debug/Architecture/Run Taxonomy Catalog Contracts")]
    public static void RunAll()
    {
        Directory.CreateDirectory("Temp");
        List<string> report = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("work_type_protocol", VerifyWorkTypeExtension, report, errors);
        Run("authored_needs", VerifyAuthoredNeeds, report, errors);
        Run("room_role_protocol", VerifyRoomRoleExtension, report, errors);
        Run("authored_stock_categories", VerifyStockCategoryExtension, report, errors);
        Run("authored_building_categories", VerifyBuildingCategoryExtension, report, errors);

        File.WriteAllLines(ReportPath, report);
        if (errors.Count == 0)
        {
            Debug.Log($"Taxonomy catalog contracts PASS. Report: {ReportPath}");
            return;
        }

        Debug.LogError(
            $"Taxonomy catalog contracts FAIL ({errors.Count}): {string.Join(" | ", errors)}. "
            + $"Report: {ReportPath}");
    }

    private static string VerifyWorkTypeExtension()
    {
        WorkPriorityProfile profile = WorkPriorityProfile.CreateDefault();
        WorkTypeId guardId = BuiltInWorkTypeIds.Guard;
        Require(WorkTypeCatalog.TryGet(guardId, out WorkTypeDefinition guard),
            "fixed guard work protocol is missing");
        Require(guard.CapabilityId == "building:security",
            "guard ID no longer maps to its fixed capability");
        Require(profile.GetPriority(guardId) == guard.DefaultPriority,
            "definition default priority was ignored");

        profile.SetPriority(guardId, WorkPriorityLevel.Priority1);
        WorkPriorityProfile clone = profile.Clone();
        Require(clone.GetPriority(guardId) == WorkPriorityLevel.Priority1,
            "work priority did not clone");

        string json = JsonUtility.ToJson(profile);
        WorkPriorityProfile restored = JsonUtility.FromJson<WorkPriorityProfile>(json);
        Require(restored.GetPriority(guardId) == WorkPriorityLevel.Priority1,
            "work priority did not serialize");
        Require(restored.Entries.Any(entry => entry.WorkTypeId == guardId.Value),
            "stable work id was not stored");
        return $"count={WorkTypeCatalog.All.Count}; id={guardId.Value}; priority={restored.GetPriority(guardId)}";
    }

    private static string VerifyAuthoredNeeds()
    {
        ICharacterNeedDefinitionCatalog catalog = CharacterAiEditorTestDependencies.AuthoredGameplay;
        Require(catalog.All.Count == 6, "authored need count changed");
        Require(!catalog.TryGet(CharacterCondition.MOOD, out _), "mood was incorrectly authored as a need");
        Require(catalog.TryGet(CharacterCondition.HUNGER, out CharacterNeedDefinition hunger),
            "authored hunger need is missing");

        Dictionary<CharacterCondition, float> stats = catalog.All
            .ToDictionary((entry) => entry.Condition, (entry) => entry.DefaultValue);
        stats[CharacterCondition.HUNGER] = 10f;
        List<CharacterMoodFactorSnapshot> factors = CharacterMoodRules.BuildNeedFactors(stats, catalog);
        CharacterMoodFactorSnapshot factor = factors.FirstOrDefault((entry) => entry.Id == "need:hunger");
        Require(factor != null && Mathf.Approximately(factor.Value, -18f),
            "authored hunger mood curve was not evaluated");
        return $"count={catalog.All.Count}; moodSeparate=true; factor={factor.Label}:{factor.Value}";
    }

    private static string VerifyRoomRoleExtension()
    {
        Require(FacilityRoleCatalog.TryGet(FacilityRole.Research, out FacilityRoleDefinition research),
            "fixed research role protocol is missing");
        Require(research.Id == "role:research" && research.SemanticTag == "Research",
            "research role ID or semantic tag changed");
        string mixedName = RoomEnvironmentPresentation.GetRoomName(
            FacilityRole.Research | FacilityRole.Medical);
        Require(mixedName.Contains("연구") && mixedName.Contains("의료"),
            "mixed room name omitted a fixed role");
        Require(!File.ReadAllText("Assets/Scripts/Models/Rooms/Core/RoomRole.cs").Contains("enum RoomRole"),
            "duplicate RoomRole enum still exists");
        return $"count={FacilityRoleCatalog.All.Count}; name={mixedName}; duplicateEnum=false";
    }

    private static string VerifyStockCategoryExtension()
    {
        IStockCategoryDefinitionCatalog catalog = CharacterAiEditorTestDependencies.AuthoredGameplay;
        Require(catalog.All.Count == 11, "authored stock category count changed");
        Require(catalog.TryGet(StockCategory.General, out StockCategoryDefinition general)
                && general.Id == "stock:general",
            "authored general stock category is missing");

        WarehouseInventory inventory = new WarehouseInventory(
            100L, StockCategory.General, restrictCategory: false);
        inventory.SeedPhysicalStockForTest(StockCategory.General, 10);
        Require(StockCategoryPersistenceId.ToId(StockCategory.General) == "stock:general",
            "stable category id was not used");

        WarehouseInventory restored = new WarehouseInventory(
            100L, StockCategory.General, restrictCategory: false);
        Require(restored.TryApplySnapshot(inventory.CreateSnapshot(), out string restoreError), restoreError);
        restored.SeedPhysicalStockForTest(StockCategory.General, 0);
        Require(restored.GetStock(StockCategory.General) == 0,
            "derived warehouse stock was persisted outside physical items");

        IReadOnlyList<StockDeliveryOffer> offers = StockSupplyService.CreateDailyDeliveryOffers(
            6,
            (_) => 1f,
            catalog);
        Require(offers.Any((offer) => offer.category == StockCategory.General),
            "authored general category is absent from daily offers");

        WarehouseManagementSummary summary = BuildingManagementSummaryQuery.FromWarehouses(
            new[]
            {
                new WarehouseManagementSnapshot(
                    restored.TotalStock,
                    restored.EnumerateStock().ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value),
                    restored.StoredMassGrams,
                    restored.MaxMassGrams)
            });
        Require(summary.GetStock(StockCategory.General) == 0,
            "management summary fabricated stock");
        return $"count={catalog.All.Count}; id={general.Id}; amount={restored.GetStock(StockCategory.General)}";
    }

    private static string VerifyBuildingCategoryExtension()
    {
        IBuildingCategoryDefinitionCatalog catalog =
            CharacterAiEditorTestDependencies.AuthoredGameplay;
        Require(catalog.All.Count == 8, "authored building category count changed");

        Require(
            catalog.GetDisplayName(BuildingCategory.Crafting) == "제작",
            "authored building category display name was not resolved");
        Require(
            catalog.GetShopCostWeight(BuildingCategory.Crafting) == 120,
            "authored building category shop weight was not resolved");
        Require(
            catalog.TryResolve("category:crafting", out BuildingCategoryDefinition byId)
                && byId.Category == BuildingCategory.Crafting,
            "authored building category stable ID was not resolved");
        Require(
            catalog.TryResolve("제작", out BuildingCategoryDefinition byLabel)
                && byLabel.Category == BuildingCategory.Crafting,
            "authored building category display label was not resolved");
        return $"count={catalog.All.Count}; id={byId.Id}; weight={byId.ShopCostWeight}";
    }

    private static void Run(
        string caseName,
        Func<string> scenario,
        ICollection<string> report,
        ICollection<string> errors)
    {
        try
        {
            string details = scenario();
            report.Add($"{caseName}\tPASS\t{details}");
        }
        catch (Exception exception)
        {
            string details = exception.GetBaseException().Message.Replace('\t', ' ');
            report.Add($"{caseName}\tFAIL\t{details}");
            errors.Add($"{caseName}: {details}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestWarehouse : IWarehouseFacility
    {
        public TestWarehouse(WarehouseInventory inventory)
        {
            Inventory = inventory;
        }

        public WarehouseInventory Inventory { get; }
        public BuildingInstanceId PersistentInstanceId =>
            (BuildingInstanceId)"building:test-taxonomy-warehouse";
        public bool HasWarehouseInventory => Inventory != null;
    }
}
