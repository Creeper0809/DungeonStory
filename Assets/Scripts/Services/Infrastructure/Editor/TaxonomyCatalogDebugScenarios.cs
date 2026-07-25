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

        ResetCatalogs();
        try
        {
            Run("work_type_extension", VerifyWorkTypeExtension, report, errors);
            Run("need_extension", VerifyNeedExtension, report, errors);
            Run("room_role_unification", VerifyRoomRoleExtension, report, errors);
            Run("stock_category_extension", VerifyStockCategoryExtension, report, errors);
            Run("building_category_extension", VerifyBuildingCategoryExtension, report, errors);
        }
        finally
        {
            ResetCatalogs();
        }

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
        WorkTypeId customWorkTypeId = new WorkTypeId("work:archive");
        WorkTypeCatalog.Register(
            customWorkTypeId,
            "기록 정리",
            45,
            WorkPriorityLevel.Priority2,
            "building:archive");

        WorkPriorityProfile profile = WorkPriorityProfile.CreateDefault();
        Require(WorkTypeCatalog.TryGet(customWorkTypeId, out _), "registered work type is absent from task enumeration");
        Require(WorkTaskCatalog.GetDisplayName(customWorkTypeId) == "기록 정리", "work label did not come from definition");
        Require(CodexTextFormatter.FormatWorkTypes(new[] { customWorkTypeId }) == "기록 정리", "Codex omitted the registered work label");
        Require(profile.GetPriority(customWorkTypeId) == WorkPriorityLevel.Priority2, "definition default priority was ignored");

        profile.SetPriority(customWorkTypeId, WorkPriorityLevel.Priority1);
        WorkPriorityProfile clone = profile.Clone();
        Require(clone.GetPriority(customWorkTypeId) == WorkPriorityLevel.Priority1, "custom priority did not clone");

        string json = JsonUtility.ToJson(profile);
        WorkPriorityProfile restored = JsonUtility.FromJson<WorkPriorityProfile>(json);
        Require(restored.GetPriority(customWorkTypeId) == WorkPriorityLevel.Priority1, "custom priority did not serialize");
        Require(restored.Entries.Any((entry) => entry.WorkTypeId == "work:archive"), "stable work id was not stored");
        return $"count={WorkTypeCatalog.All.Count}; id=work:archive; priority={restored.GetPriority(customWorkTypeId)}";
    }

    private static string VerifyNeedExtension()
    {
        CharacterCondition customCondition = (CharacterCondition)777;
        CharacterNeedDefinition definition = new CharacterNeedDefinition(
            "need:focus",
            customCondition,
            "집중",
            25,
            100f,
            65f,
            FacilityRole.Research,
            CharacterNeedTag.Leisure | CharacterNeedTag.DirectorRoutine | CharacterNeedTag.MoodInteraction,
            0f,
            new CharacterNeedMoodProfile(
                15f, "집중이 완전히 흐트러짐", -7f,
                35f, "집중이 필요함", -3f,
                85f, "또렷하게 집중함", 2f));
        CharacterNeedCatalog.Register(definition);

        Require(!CharacterNeedCatalog.TryGet(CharacterCondition.MOOD, out _), "mood was incorrectly registered as a need");
        Require(CharacterNeedCatalog.All.Any((entry) => entry.Condition == customCondition), "custom need is absent from enumeration");

        Dictionary<CharacterCondition, float> stats = CharacterNeedCatalog.All
            .ToDictionary((entry) => entry.Condition, (entry) => entry.DefaultValue);
        stats[customCondition] = 10f;
        List<CharacterMoodFactorSnapshot> factors = CharacterMoodRules.BuildNeedFactors(stats);
        CharacterMoodFactorSnapshot factor = factors.FirstOrDefault((entry) => entry.Id == "need:focus");
        Require(factor != null && Mathf.Approximately(factor.Value, -7f), "custom need mood curve was not evaluated");
        return $"count={CharacterNeedCatalog.All.Count}; moodSeparate=true; factor={factor.Label}:{factor.Value}";
    }

    private static string VerifyRoomRoleExtension()
    {
        FacilityRole customRole = (FacilityRole)(1 << 20);
        Color expectedColor = new Color(0.25f, 0.8f, 0.75f, 1f);
        FacilityRoleCatalog.Register(new FacilityRoleDefinition(
            "role:archive",
            customRole,
            "기록",
            "기록실",
            45,
            expectedColor,
            "Archive"));

        Require(RoomEnvironmentPresentation.GetRoomName(customRole) == "기록실", "custom single room name was not resolved");
        string mixedName = RoomEnvironmentPresentation.GetRoomName(FacilityRole.Research | customRole);
        Require(mixedName.Contains("연구") && mixedName.Contains("기록"), "mixed room name omitted a registered role");
        Require(FacilityRoleCatalog.GetColor(customRole, Color.black) == expectedColor, "custom role color was not resolved");
        Require(CodexTextFormatter.FormatFacilityRoles(customRole) == "기록", "Codex omitted the registered role label");
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.Facility = new FacilityData { roles = customRole };
        Require(building.HasSemanticTag("Archive"), "custom role semantic tag was not emitted");
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;

        GameObject root = new GameObject("CustomRoleCandidate");
        try
        {
            BuildableObject instance = root.AddComponent<BuildableObject>();
            Vector2Int position = new Vector2Int(1, 1);
            Grid grid = new Grid(3, 3);
            instance.Initialization(building, position);
            instance.SetGrid(grid);
            Require(
                grid.RegisterOccupant(instance, GridLayer.Building, instance.buildPoses, false),
                "custom role fixture was not registered on the grid");

            IFacilityCandidateCache cache = new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry);
            IReadOnlyList<BuildableObject> candidates = cache.GetCandidates(
                grid,
                FacilityRole.Research | customRole);
            Require(candidates.Count == 1 && candidates[0] == instance, "candidate cache omitted a registered custom role");
            Require(!(candidates is IList<BuildableObject>), "candidate cache exposed its mutable backing list");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(building);
        }

        Require(!File.ReadAllText("Assets/Scripts/Models/Rooms/Core/RoomRole.cs").Contains("enum RoomRole"), "duplicate RoomRole enum still exists");
        return $"count={FacilityRoleCatalog.All.Count}; name={mixedName}; duplicateEnum=false";
    }

    private static string VerifyStockCategoryExtension()
    {
        StockCategory customCategory = (StockCategory)777;
        StockCategoryCatalog.Register(new StockCategoryDefinition(
            "stock:crystal",
            customCategory,
            "결정",
            "결",
            25,
            0.10f,
            5,
            12,
            2));

        WarehouseInventory inventory = WarehouseInventory.CreateSeeded(100);
        Require(inventory.GetStock(customCategory) > 0, "custom stock category was omitted from seeded inventory");
        Require(StockCategoryPersistenceId.ToId(customCategory) == "stock:crystal", "stable category id was not used");

        WarehouseInventory restored = new WarehouseInventory();
        Require(restored.TryApplySnapshot(inventory.CreateSnapshot(), out string restoreError), restoreError);
        Require(restored.GetStock(customCategory) == inventory.GetStock(customCategory), "custom stock did not round-trip");

        IReadOnlyList<StockDeliveryOffer> offers = StockSupplyService.CreateDailyDeliveryOffers(6, (_) => 1f);
        Require(offers.Any((offer) => offer.category == customCategory), "custom category is absent from daily offers");

        WarehouseManagementSummary summary = BuildingManagementSummaryQuery.FromWarehouses(
            new[] { new TestWarehouse(restored) });
        Require(summary.GetStock(customCategory) == restored.GetStock(customCategory), "management summary omitted custom stock");
        Require(StockCategoryCatalog.All.Any((entry) => entry.Category == customCategory), "stock UI catalog enumeration omitted custom category");
        return $"count={StockCategoryCatalog.All.Count}; id={StockCategoryPersistenceId.ToId(customCategory)}; amount={restored.GetStock(customCategory)}";
    }

    private static string VerifyBuildingCategoryExtension()
    {
        BuildingCategory customCategory = (BuildingCategory)777;
        BuildingCategoryCatalog.Register(new BuildingCategoryDefinition(
            "category:alchemy",
            customCategory,
            "연금",
            45,
            175));

        Require(
            BuildingCategoryCatalog.GetDisplayName(customCategory) == "연금",
            "custom building category display name was not resolved");
        Require(
            BuildingCategoryCatalog.GetShopCostWeight(customCategory) == 175,
            "custom building category shop weight was not resolved");
        Require(
            BuildingCategoryCatalog.TryResolve("category:alchemy", out BuildingCategoryDefinition byId)
                && byId.Category == customCategory,
            "custom building category stable ID was not resolved");
        Require(
            BuildingCategoryCatalog.TryResolve("연금", out BuildingCategoryDefinition byLabel)
                && byLabel.Category == customCategory,
            "custom building category display label was not resolved");
        return $"count={BuildingCategoryCatalog.All.Count}; id={byId.Id}; weight={byId.ShopCostWeight}";
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

    private static void ResetCatalogs()
    {
        WorkTypeCatalog.ResetToBuiltIns();
        CharacterNeedCatalog.ResetToBuiltIns();
        FacilityRoleCatalog.ResetToBuiltIns();
        StockCategoryCatalog.ResetToBuiltIns();
        BuildingCategoryCatalog.ResetToBuiltIns();
    }

    private sealed class TestWarehouse : IWarehouseFacility
    {
        public TestWarehouse(WarehouseInventory inventory)
        {
            Inventory = inventory;
        }

        public WarehouseInventory Inventory { get; }
        public bool HasWarehouseInventory => Inventory != null;
    }
}
