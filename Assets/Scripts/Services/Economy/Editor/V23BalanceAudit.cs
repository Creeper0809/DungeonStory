#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;

public static class V23BalanceAudit
{
    private const string AppendixPath =
        "docs/generated/V23_BOM_Work_Quality_Appendix.md";
    private const string ReportPath =
        "Artifacts/QA/v23-balance-audit.txt";

    [MenuItem("DungeonStory/V23/Generate Balance Audit and Appendix")]
    public static void Generate()
    {
        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
            GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException(
                "The required root GameContentCatalogSO could not be loaded.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();
        ItemDefinitionCatalogSO items =
            root.GetItemDefinitions<ItemDefinitionCatalogSO>();
        EditorContentSource source = new(domain, items);
        ResourceMaterialEconomicProfileCatalog materialProfiles = new(source);
        V23BalanceWorkCalculator balance = new(materialProfiles);
        V23MaterialSalvageCalculator salvage = new(materialProfiles);

        BuildingSO[] allBuildings = source.GetAll<BuildingSO>()
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .ToArray();
        BuildingSO[] buildings = allBuildings
            .Where(value => value.id >= 0 && !value.IsDeprecatedCompatibilityAsset)
            .ToArray();
        BuildingSO[] compatibilityBuildings = allBuildings
            .Where(value => value.id >= 0 && value.IsDeprecatedCompatibilityAsset)
            .ToArray();
        BuildingSO[] runtimeArchetypes = allBuildings
            .Where(value => value.id < 0)
            .ToArray();
        ProductionRecipeSO[] recipes = source.GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        CombatEquipmentDefinitionSO[] equipment =
            source.GetAll<CombatEquipmentDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .ToArray();
        ApparelDefinitionSO[] apparel = source.GetAll<ApparelDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal)
            .ToArray();
        TextileMaterialDefinitionSO[] textiles =
            source.GetAll<TextileMaterialDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
            .ToArray();
        FactionContractDefinitionSO[] factionContracts =
            source.GetAll<FactionContractDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        GuestRequestDefinitionSO[] guestRequests =
            source.GetAll<GuestRequestDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        DungeonFactionDefinitionSO[] dungeonFactions =
            Resources.LoadAll<DungeonFactionDefinitionSO>(
                    DungeonFactionDefinitionSO.ResourcePath)
                .Where(value => value != null)
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ToArray();
        ItemDefinitionSO[] itemDefinitions = source.GetAll<ItemDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        SaleItem[] retailOffers = Resources.LoadAll<SaleItem>("SO/Stock/Item")
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .ToArray();
        StockInfo[] retailStockDefinitions = Resources.LoadAll<StockInfo>("SO/Stock")
            .Where(value => value != null)
            .OrderBy(value => value.shopId)
            .ThenBy(value => value.id)
            .ToArray();
        EmbeddedWorkValueSnapshot embeddedWork =
            new V23EmbeddedWorkValueCalculator(
                recipes,
                itemDefinitions,
                equipment,
                source.GetAll<CraftMaterialDefinitionSO>(),
                balance)
            .Calculate();

        List<string> failures = new();
        if (!Mathf.Approximately(
                AuthoredFactionContractBalanceRules.WorkUnitsPerAdultDay,
                SettlementLaborAuthority.EffectiveOutputWuPerAdultDay))
        {
            failures.Add(
                "Faction contract WU mirror does not match the measured settlement labor baseline.");
        }
        ValidateRuntimeArchetypes(runtimeArchetypes, failures);
        ValidateCompatibilityAssets(compatibilityBuildings, failures);
        ValidateCounts(buildings, recipes, equipment, apparel, textiles, failures);
        ValidateBuildings(buildings, balance, failures);
        ValidateRecipes(recipes, balance, failures);
        ValidateEquipment(equipment, failures);
        ValidateApparel(apparel, textiles, failures);
        ValidateSalvage(buildings, salvage, failures);
        ValidateEmbeddedWork(embeddedWork, failures);
        ValidateAuthoredFactionContracts(
            factionContracts,
            embeddedWork,
            failures);
        ValidateFactionRouteCargo(dungeonFactions, embeddedWork, failures);
        ValidateGuestRequests(
            guestRequests,
            itemDefinitions,
            embeddedWork,
            failures);
        ValidateProcurementCatalog(
            domain.StockCategories,
            itemDefinitions,
            embeddedWork,
            failures);
        ValidateDeliverySettlement(failures);
        ValidateRejectedQualitySalePricing(failures);
        ValidateMarketSaleCatalog(
            itemDefinitions,
            embeddedWork,
            failures,
            enforceRecoveryBand: true);
        ValidateRetailCatalog(
            retailOffers,
            retailStockDefinitions,
            itemDefinitions,
            embeddedWork,
            failures);
        ValidateRegionalContractEconomy(itemDefinitions, failures);
        ValidateDismantleEmbeddedWork(
            buildings,
            balance,
            salvage,
            embeddedWork,
            failures);

        string appendix = BuildV23Appendix(
            buildings,
            recipes,
            equipment,
            apparel,
            textiles,
            balance,
            salvage);
        WriteProjectFile(AppendixPath, appendix);
        WriteProjectFile(
            ReportPath,
            BuildReport(
                buildings,
                runtimeArchetypes,
                compatibilityBuildings,
                recipes,
                equipment,
                apparel,
                textiles,
                factionContracts,
                dungeonFactions,
                guestRequests,
                itemDefinitions,
                domain.StockCategories,
                retailOffers,
                retailStockDefinitions,
                balance,
                salvage,
                embeddedWork,
                failures));
        AssetDatabase.Refresh();

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "V23 balance audit failed:\n" + string.Join("\n", failures));
        }
        Debug.Log(
            $"V23 balance audit passed: {buildings.Length} buildings, "
            + $"{recipes.Length} recipes, {equipment.Length} equipment, "
            + $"{apparel.Length} apparel, {textiles.Length} textiles.");
    }

    private static void ValidateRuntimeArchetypes(
        IReadOnlyCollection<BuildingSO> runtimeArchetypes,
        ICollection<string> failures)
    {
        HashSet<int> ids = new();
        foreach (BuildingSO building in runtimeArchetypes)
        {
            if (!ids.Add(building.id))
                failures.Add($"Duplicate runtime building archetype ID {building.id}.");
        }
        if (!ids.Contains(RuntimeBuildingArchetypeIds.WorldResourceNode))
            failures.Add("World resource runtime building archetype is missing.");
        if (!ids.Contains(RuntimeBuildingArchetypeIds.WorldFilthWorkTarget))
            failures.Add("World filth runtime building archetype is missing.");
    }

    private static void ValidateCompatibilityAssets(
        IReadOnlyCollection<BuildingSO> compatibilityBuildings,
        ICollection<string> failures)
    {
        foreach (BuildingSO building in compatibilityBuildings)
        {
            if (building.unlocked)
                failures.Add($"Deprecated compatibility building {building.id} remains unlocked.");
        }
    }

    private static void ValidateCounts(
        IReadOnlyCollection<BuildingSO> buildings,
        IReadOnlyCollection<ProductionRecipeSO> recipes,
        IReadOnlyCollection<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyCollection<ApparelDefinitionSO> apparel,
        IReadOnlyCollection<TextileMaterialDefinitionSO> textiles,
        ICollection<string> failures)
    {
        if (buildings.Count == 0)
            failures.Add("Root catalog has no player building definitions.");
        if (recipes.Count != 355)
            failures.Add($"Expected 355 recipes, found {recipes.Count}.");
        if (equipment.Count != 61)
            failures.Add($"Expected 61 combat equipment definitions, found {equipment.Count}.");
        if (apparel.Count != 56)
            failures.Add($"Expected 56 apparel definitions, found {apparel.Count}.");
        if (textiles.Count != 12)
            failures.Add($"Expected 10 woven and 2 non-woven apparel materials, found {textiles.Count} definitions.");
        if (textiles.Count(value =>
                (value.Tags & TextileMaterialTag.Woven) != 0) != 10)
            failures.Add("Expected exactly 10 woven textile materials.");
    }

    private static void ValidateBuildings(
        IEnumerable<BuildingSO> buildings,
        IBalanceWorkCalculator balance,
        ICollection<string> failures)
    {
        HashSet<int> ids = new();
        foreach (BuildingSO building in buildings)
        {
            if (!ids.Add(building.id))
                failures.Add($"Duplicate player building ID {building.id}.");
            IReadOnlyList<ItemAmountDefinition> bom = building.GetConstructionMaterials();
            if (bom == null || bom.Count == 0)
                failures.Add($"Building {building.id} has no physical BOM.");
            if (balance.CalculateConstruction(building) <= 0f)
                failures.Add($"Building {building.id} has no construction work.");
            if (building.id is >= 9201 and <= 9209
                && V23BalanceWorkCalculator.ResolveConstructionClass(building)
                    != ConstructionBalanceClass.Landmark)
            {
                failures.Add($"Landmark {building.id} is not classified as a landmark.");
            }
        }
    }

    private static void ValidateRecipes(
        IEnumerable<ProductionRecipeSO> recipes,
        IRecipeBalanceWorkCalculator balance,
        ICollection<string> failures)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ProductionRecipeSO recipe in recipes)
        {
            if (string.IsNullOrWhiteSpace(recipe.RecipeId) || !ids.Add(recipe.RecipeId))
                failures.Add($"Recipe '{recipe.name}' has an empty/duplicate stable ID.");
            bool hasInputs = recipe.Inputs.Count > 0;
            bool hasOutputs = recipe.Outputs.Count > 0;
            bool validFlow = recipe.FlowRole switch
            {
                ProductionFlowRole.Source => !hasInputs && hasOutputs,
                ProductionFlowRole.Sink => hasInputs && !hasOutputs,
                ProductionFlowRole.Transform => hasInputs && hasOutputs,
                _ => false
            };
            if (!validFlow)
            {
                failures.Add(
                    $"Recipe '{recipe.RecipeId}' has flow role {recipe.FlowRole} "
                    + $"but input/output shape is {recipe.Inputs.Count}/{recipe.Outputs.Count}.");
            }
            if (recipe.Inputs.Any(value => value == null
                    || value.Amount <= 0
                    || value.ItemId.StartsWith("stock-item:", StringComparison.Ordinal)))
                failures.Add($"Recipe '{recipe.RecipeId}' has an invalid or abstract input.");
            if (recipe.Outputs.Any(value => value == null
                    || value.Amount <= 0
                    || value.Probability <= 0f
                    || value.Probability > 1f))
                failures.Add($"Recipe '{recipe.RecipeId}' has an invalid physical output.");
            if (!recipe.HasAuthoredProcessClass)
            {
                failures.Add(
                    $"Recipe '{recipe.RecipeId}' has no authored process class.");
                continue;
            }
            float calculatedWork = balance.CalculateRecipe(recipe);
            if (calculatedWork <= 0f)
                failures.Add($"Recipe '{recipe.RecipeId}' has no V23 work amount.");
            if (Mathf.Abs(recipe.RequiredWork - calculatedWork) > 0.011f)
            {
                failures.Add(
                    $"Recipe '{recipe.RecipeId}' stores {recipe.RequiredWork:0.##} work "
                    + $"but the V23 authority calculates {calculatedWork:0.##}.");
            }
        }
    }

    private static void ValidateEquipment(
        IEnumerable<CombatEquipmentDefinitionSO> definitions,
        ICollection<string> failures)
    {
        foreach (CombatEquipmentDefinitionSO definition in definitions)
        {
            if (definition.PrimaryMaterialAmount <= 0)
                failures.Add($"Equipment '{definition.EquipmentId}' has no primary material.");
            if (definition.RequiredCraftWork <= 6.001f)
                failures.Add($"Equipment '{definition.EquipmentId}' still uses placeholder craft work.");
        }
    }

    private static void ValidateApparel(
        IEnumerable<ApparelDefinitionSO> apparel,
        IReadOnlyList<TextileMaterialDefinitionSO> textiles,
        ICollection<string> failures)
    {
        foreach (ApparelDefinitionSO definition in apparel)
        {
            if (definition.RequiredPoints == 0 || definition.OccupiedPoints == 0)
                failures.Add($"Apparel '{definition.ApparelId}' has no anatomy contract.");
            if (!textiles.Any(material =>
                    (material.Tags & definition.AllowedMaterialTags) != 0))
                failures.Add($"Apparel '{definition.ApparelId}' has no legal primary material.");
        }
    }

    private static void ValidateSalvage(
        IEnumerable<BuildingSO> buildings,
        IMaterialSalvageCalculator salvage,
        ICollection<string> failures)
    {
        // Every player-authored facility participates in the repeat rebuild
        // pipeline, so sampling is insufficient: a single late-catalog BOM can
        // otherwise become an infinite material source.
        foreach (BuildingSO building in buildings)
        {
            IReadOnlyList<ItemAmountDefinition> bom = building.GetConstructionMaterials();
            MaterialSalvageResult result = salvage.Calculate(
                DismantleTargetKind.GeneralFacility,
                100f,
                bom,
                50f);
            Dictionary<string, int> input = bom
                .GroupBy(value => value.ItemId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(value => value.Amount),
                    StringComparer.Ordinal);
            if (result.RecoveredMaterials.Any(value =>
                    !input.TryGetValue(value.ItemId, out int amount)
                    || value.Amount > amount))
            {
                failures.Add($"Building {building.id} salvage creates excess material.");
            }
        }
    }

    private static string BuildAppendix(
        IReadOnlyList<BuildingSO> buildings,
        IReadOnlyList<ProductionRecipeSO> recipes,
        IReadOnlyList<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyList<ApparelDefinitionSO> apparel,
        IReadOnlyList<TextileMaterialDefinitionSO> textiles,
        IBalanceWorkCalculator balance,
        IMaterialSalvageCalculator salvage)
    {
        StringBuilder text = new();
        text.AppendLine("# V23 시설·제작품 BOM·작업량·품질 자동 부록");
        text.AppendLine();
        text.AppendLine("> 루트 콘텐츠 카탈로그에서 자동 생성한다. 수동 편집하지 않는다.");
        text.AppendLine();
        text.AppendLine("## 시설");
        text.AppendLine();
        text.AppendLine("| ID | 시설 | 분류 | BOM | 작업량 | 기본 작업자 | 보통 숙련 품질 확률 | 해체 작업/회수 |");
        text.AppendLine("|---:|---|---|---|---:|---|---|---|");
        foreach (BuildingSO building in buildings)
        {
            float work = balance.CalculateConstruction(building);
            DismantleTargetKind kind = ResolveDismantleKind(building);
            MaterialSalvageResult recovered = salvage.Calculate(
                kind, work, building.GetConstructionMaterials(), 50f);
            text.AppendLine($"| {building.id} | {Escape(building.objectName)} | "
                + $"{V23BalanceWorkCalculator.ResolveConstructionClass(building)} | "
                + $"{FormatV23Amounts(building.GetConstructionMaterials())} | {work:0.##} | "
                + $"Anyone / 지정 우선·예상 품질 | {FormatProbabilities(50f, 0f, work / 100f)} | "
                + $"{recovered.RequiredWork:0.##} / {FormatV23Amounts(recovered.RecoveredMaterials)} |");
        }

        text.AppendLine();
        text.AppendLine("## 일반 제작");
        text.AppendLine();
        text.AppendLine("| 조합식 ID | 공정 | 입력 | 출력 | V23 작업량 | 기본 작업자 |");
        text.AppendLine("|---|---|---|---|---:|---|");
        foreach (ProductionRecipeSO recipe in recipes)
        {
            text.AppendLine($"| {recipe.RecipeId} | "
                + $"{V23BalanceWorkCalculator.ResolveProductionProcessClass(recipe)} | "
                + $"{FormatV23Amounts(recipe.Inputs)} | {FormatV23Outputs(recipe.Outputs)} | "
                + $"{balance.CalculateRecipe(recipe):0.##} | Anyone / 최고 작업 속도 |");
        }

        text.AppendLine();
        text.AppendLine("## 전투 장비");
        text.AppendLine();
        text.AppendLine("| 장비 ID | 주재료 수량 | 부품 | 계산 작업량 범위 | 기본 작업자 | 숙련 50 품질 확률 |");
        text.AppendLine("|---|---:|---|---:|---|---|");
        foreach (CombatEquipmentDefinitionSO definition in equipment)
        {
            float minimumWork = balance.CalculateEquipment(definition, string.Empty);
            text.AppendLine($"| {definition.EquipmentId} | {definition.PrimaryMaterialAmount} | "
                + $"{FormatAmounts(definition.RequiredComponentInputs)} | {minimumWork:0.##} | "
                + $"Anyone / 최고 예상 품질 | {FormatProbabilities(50f, 0f, minimumWork / 20f)} |");
        }

        text.AppendLine();
        text.AppendLine("## 의복");
        text.AppendLine();
        text.AppendLine("| 의복 ID | 부착점 | 허용 재료 | S/M/L 작업량 범위 | 기본 작업자 | 숙련 50 품질 확률 |");
        text.AppendLine("|---|---|---|---|---|---|");
        foreach (ApparelDefinitionSO definition in apparel)
        {
            TextileMaterialDefinitionSO[] allowed = textiles.Where(material =>
                (material.Tags & definition.AllowedMaterialTags) != 0).ToArray();
            float min = allowed.Length == 0 ? 0f : allowed.Min(material =>
                balance.CalculateApparel(definition, material, ApparelSizeClass.Small, 0));
            float max = allowed.Length == 0 ? 0f : allowed.Max(material =>
                balance.CalculateApparel(
                    definition,
                    material,
                    ApparelSizeClass.Large,
                    definition.SupportedModifications));
            text.AppendLine($"| {definition.ApparelId} | {definition.RequiredPoints} | "
                + $"{string.Join(", ", allowed.Select(value => value.DisplayName))} | "
                + $"{min:0.##}–{max:0.##} | Anyone / 최고 예상 품질 | "
                + $"{FormatProbabilities(50f, 0f, Mathf.Max(0f, min / 20f))} |");
        }
        return text.ToString();
    }

    private static string BuildV23Appendix(
        IReadOnlyList<BuildingSO> buildings,
        IReadOnlyList<ProductionRecipeSO> recipes,
        IReadOnlyList<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyList<ApparelDefinitionSO> apparel,
        IReadOnlyList<TextileMaterialDefinitionSO> textiles,
        IBalanceWorkCalculator balance,
        IMaterialSalvageCalculator salvage)
    {
        StringBuilder result = new();
        result.AppendLine("# V23 시설·제작품 BOM·작업량·품질 자동 부록");
        result.AppendLine();
        result.AppendLine("> 루트 콘텐츠 카탈로그의 현재 에셋에서 자동 생성한다. 수동 편집하지 않는다.");
        result.AppendLine();
        result.AppendLine("## 시설");
        result.AppendLine();
        result.AppendLine("| ID | 시설 | 분류 | BOM | 작업량 | 기본 작업자 정책 | 숙련 50 품질 분포 | 해체 작업/회수 |");
        result.AppendLine("|---:|---|---|---|---:|---|---|---|");
        foreach (BuildingSO building in buildings)
        {
            float work = balance.CalculateConstruction(building);
            MaterialSalvageResult recovered = salvage.Calculate(
                ResolveDismantleKind(building),
                work,
                building.GetConstructionMaterials(),
                50f);
            result.AppendLine($"| {building.id} | {Escape(building.objectName)} | "
                + $"{V23BalanceWorkCalculator.ResolveConstructionClass(building)} | "
                + $"{FormatV23Amounts(building.GetConstructionMaterials())} | {work:0.##} | "
                + "지정 우선 후 최고 예상 품질 | "
                + $"{FormatProbabilities(50f, 0f, work / 100f)} | "
                + $"{recovered.RequiredWork:0.##} / {FormatV23Amounts(recovered.RecoveredMaterials)} |");
        }

        result.AppendLine();
        result.AppendLine("## 일반 제작");
        result.AppendLine();
        result.AppendLine("| 조합식 ID | 공정 | 입력 | 출력 | 작업량 | 기본 작업자 정책 |");
        result.AppendLine("|---|---|---|---|---:|---|");
        foreach (ProductionRecipeSO recipe in recipes)
        {
            result.AppendLine($"| {recipe.RecipeId} | "
                + $"{V23BalanceWorkCalculator.ResolveProductionProcessClass(recipe)} | "
                + $"{FormatV23Amounts(recipe.Inputs)} | {FormatV23Outputs(recipe.Outputs)} | "
                + $"{balance.CalculateRecipe(recipe):0.##} | 최고 작업 속도 |");
        }

        result.AppendLine();
        result.AppendLine("## 전투 장비");
        result.AppendLine();
        result.AppendLine("| 장비 ID | 주재료 수량 | 부품 | 기준 작업량 | 기본 작업자 정책 | 숙련 50 품질 분포 |");
        result.AppendLine("|---|---:|---|---:|---|---|");
        foreach (CombatEquipmentDefinitionSO definition in equipment)
        {
            float work = balance.CalculateEquipment(
                definition,
                definition.DefaultMaterialId);
            result.AppendLine($"| {definition.EquipmentId} | "
                + $"{definition.PrimaryMaterialAmount} | "
                + $"{FormatV23Amounts(definition.RequiredComponentInputs)} | {work:0.##} | "
                + "최고 예상 품질 | "
                + $"{FormatProbabilities(50f, 0f, work / 20f)} |");
        }

        result.AppendLine();
        result.AppendLine("## 의복");
        result.AppendLine();
        result.AppendLine("| 의복 ID | 부착점 | 허용 재료 | S~L 작업량 | 기본 작업자 정책 | 숙련 50 품질 분포 |");
        result.AppendLine("|---|---|---|---:|---|---|");
        foreach (ApparelDefinitionSO definition in apparel)
        {
            TextileMaterialDefinitionSO[] allowed = textiles.Where(material =>
                (material.Tags & definition.AllowedMaterialTags) != 0).ToArray();
            float minimum = allowed.Length == 0 ? 0f : allowed.Min(material =>
                balance.CalculateApparel(
                    definition,
                    material,
                    ApparelSizeClass.Small,
                    0));
            float maximum = allowed.Length == 0 ? 0f : allowed.Max(material =>
                balance.CalculateApparel(
                    definition,
                    material,
                    ApparelSizeClass.Large,
                    definition.SupportedModifications));
            result.AppendLine($"| {definition.ApparelId} | {definition.RequiredPoints} | "
                + $"{string.Join(", ", allowed.Select(value => value.DisplayName))} | "
                + $"{minimum:0.##}~{maximum:0.##} | 최고 예상 품질 | "
                + $"{FormatProbabilities(50f, 0f, minimum / 20f)} |");
        }
        return result.ToString();
    }

    private static string BuildReport(
        IReadOnlyCollection<BuildingSO> buildings,
        IReadOnlyCollection<BuildingSO> runtimeArchetypes,
        IReadOnlyCollection<BuildingSO> compatibilityBuildings,
        IReadOnlyCollection<ProductionRecipeSO> recipes,
        IReadOnlyCollection<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyCollection<ApparelDefinitionSO> apparel,
        IReadOnlyCollection<TextileMaterialDefinitionSO> textiles,
        IReadOnlyCollection<FactionContractDefinitionSO> factionContracts,
        IReadOnlyCollection<DungeonFactionDefinitionSO> dungeonFactions,
        IReadOnlyCollection<GuestRequestDefinitionSO> guestRequests,
        IReadOnlyCollection<ItemDefinitionSO> itemDefinitions,
        IReadOnlyCollection<AuthoredStockCategoryRecord> stockCategories,
        IReadOnlyCollection<SaleItem> retailOffers,
        IReadOnlyCollection<StockInfo> retailStockDefinitions,
        IBalanceWorkCalculator balance,
        IMaterialSalvageCalculator salvage,
        EmbeddedWorkValueSnapshot embeddedWork,
        IReadOnlyList<string> failures)
    {
        StringBuilder text = new();
        text.AppendLine("V23 BALANCE AUDIT");
        text.AppendLine($"player_buildings={buildings.Count}");
        text.AppendLine($"runtime_building_archetypes={runtimeArchetypes.Count}");
        text.AppendLine($"deprecated_compatibility_buildings={compatibilityBuildings.Count}");
        text.AppendLine($"recipes={recipes.Count}");
        text.AppendLine($"equipment={equipment.Count}");
        text.AppendLine($"apparel={apparel.Count}");
        text.AppendLine($"textiles={textiles.Count}");
        text.AppendLine($"faction_contracts={factionContracts.Count}");
        text.AppendLine($"dungeon_factions={dungeonFactions.Count}");
        text.AppendLine($"guest_requests={guestRequests.Count}");
        text.AppendLine();
        text.AppendLine("CONSTRUCTION WORK BY CLASS");
        foreach (IGrouping<ConstructionBalanceClass, BuildingSO> group in buildings
                     .GroupBy(V23BalanceWorkCalculator.ResolveConstructionClass)
                     .OrderBy(value => value.Key))
        {
            float[] work = group.Select(balance.CalculateConstruction)
                .OrderBy(value => value)
                .ToArray();
            int[] bomKinds = group.Select(value => value.GetConstructionMaterials()
                    .Select(material => material.ItemId)
                    .Distinct(StringComparer.Ordinal)
                    .Count())
                .OrderBy(value => value)
                .ToArray();
            int[] bomUnits = group.Select(value => value.GetConstructionMaterials()
                    .Sum(material => material.Amount))
                .OrderBy(value => value)
                .ToArray();
            text.AppendLine(
                $"{group.Key}: count={work.Length}, work={FormatDistribution(work)}, "
                + $"bom_kinds={FormatDistribution(bomKinds)}, "
                + $"bom_units={FormatDistribution(bomUnits)}");
        }
        text.AppendLine();
        text.AppendLine("FUNCTIONAL FACILITIES BELOW MINIMUM BOM DIVERSITY");
        foreach (BuildingSO building in buildings
                     .Where(value =>
                     {
                         ConstructionBalanceClass balanceClass =
                             V23BalanceWorkCalculator.ResolveConstructionClass(value);
                         int minimum = MinimumFunctionalBomKinds(value, balanceClass);
                         int actual = value.GetConstructionMaterials()
                             .Select(material => material.ItemId)
                             .Distinct(StringComparer.Ordinal)
                             .Count();
                         return minimum > 0 && actual < minimum;
                     })
                     .OrderBy(value => value.id))
        {
            ConstructionBalanceClass balanceClass =
                V23BalanceWorkCalculator.ResolveConstructionClass(building);
            int actual = building.GetConstructionMaterials()
                .Select(material => material.ItemId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            text.AppendLine(
                $"LOW_BOM id={building.id}, class={balanceClass}, "
                + $"kinds={actual}/{MinimumFunctionalBomKinds(building, balanceClass)}, "
                + $"asset={AssetDatabase.GetAssetPath(building)}, "
                + $"bom={FormatV23Amounts(building.GetConstructionMaterials())}");
        }
        text.AppendLine();
        text.AppendLine("RECIPE WORK BY FLOW");
        foreach (IGrouping<ProductionFlowRole, ProductionRecipeSO> group in recipes
                     .GroupBy(value => value.FlowRole)
                     .OrderBy(value => value.Key))
        {
            float[] work = group.Select(balance.CalculateRecipe)
                .OrderBy(value => value)
                .ToArray();
            text.AppendLine(
                $"{group.Key}: count={work.Length}, work={FormatDistribution(work)}");
        }
        text.AppendLine();
        text.AppendLine("LOW-WORK RECIPE REVIEW CANDIDATES");
        foreach (ProductionRecipeSO recipe in recipes
                     .Where(value => balance.CalculateRecipe(value) <= 8f)
                     .OrderBy(value => balance.CalculateRecipe(value))
                     .ThenBy(value => value.RecipeId, StringComparer.Ordinal))
        {
            ProductionProcessClass processClass =
                V23BalanceWorkCalculator.ResolveProductionProcessClass(recipe);
            text.AppendLine(
                $"LOW_WORK id={recipe.RecipeId}, flow={recipe.FlowRole}, "
                + $"process={processClass}, authored={recipe.RequiredWork:0.##}, "
                + $"calculated={balance.CalculateRecipe(recipe):0.##}, "
                + $"asset={AssetDatabase.GetAssetPath(recipe)}, "
                + $"inputs={FormatV23Amounts(recipe.Inputs)}, "
                + $"outputs={FormatV23Outputs(recipe.Outputs)}");
        }
        text.AppendLine();
        text.AppendLine("EMBEDDED WORK VALUE (EWU)");
        text.AppendLine(
            $"resolved_items={embeddedWork.ItemWork.Count}, "
            + $"external_seed_items={embeddedWork.ExternalSeedItemIds.Count}, "
            + $"unresolved_items={embeddedWork.UnresolvedItemIds.Count}, "
            + $"non_convergent_recipes={embeddedWork.NonConvergentRecipeIds.Count}");
        float[] itemEwu = embeddedWork.ItemWork.Values
            .OrderBy(value => value)
            .ToArray();
        text.AppendLine($"item_ewu={FormatDistribution(itemEwu)}");
        float[] goldPerEwu = itemDefinitions
            .Where(value => value.UnitPrice > 0
                && embeddedWork.TryGetItemWork(value.ItemId, out float ewu)
                && ewu > 0f)
            .Select(value => value.UnitPrice / embeddedWork.ItemWork[value.ItemId])
            .OrderBy(value => value)
            .ToArray();
        text.AppendLine($"authored_gold_per_ewu={FormatDistribution(goldPerEwu)}");
        foreach (string itemId in embeddedWork.ExternalSeedItemIds)
        {
            text.AppendLine(
                $"EXTERNAL_EWU item={itemId}, "
                + $"ewu={embeddedWork.ItemWork[itemId]:0.##}");
        }
        foreach (string itemId in embeddedWork.UnresolvedItemIds)
            text.AppendLine($"UNRESOLVED_EWU item={itemId}");
        foreach (string recipeId in embeddedWork.NonConvergentRecipeIds)
            text.AppendLine($"NON_CONVERGENT_EWU recipe={recipeId}");
        text.AppendLine();
        text.AppendLine("CONCRETE MARKET PROCUREMENT");
        foreach (AuthoredStockCategoryRecord stock in stockCategories
                     .Where(value => value != null && value.dailyBaseAmount > 0)
                     .OrderBy(value => value.sortOrder)
                     .ThenBy(value => value.id, StringComparer.Ordinal))
        {
            float ewu = embeddedWork.TryGetItemWork(stock.deliveryItemId, out float itemWork)
                ? itemWork
                : 0f;
            float procurementGoldPerEwu = ewu > 0f ? stock.dailyUnitCost / ewu : 0f;
            text.AppendLine(
                $"PROCUREMENT_EWU category={stock.category}, item={stock.deliveryItemId}, "
                + $"unit_cost={stock.dailyUnitCost:0.##}, ewu={ewu:0.##}, "
                + $"gold_per_ewu={procurementGoldPerEwu:0.###}");
        }
        text.AppendLine();
        text.AppendLine("RETAIL SERVICE MARGINS");
        Dictionary<string, ItemDefinitionSO> retailItems = itemDefinitions
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        float maximumFacilityPremium = retailStockDefinitions.Count == 0
            ? 1f
            : retailStockDefinitions.Max(value => Mathf.Max(1f, value.multifly));
        foreach (SaleItem offer in retailOffers)
        {
            if (!retailItems.TryGetValue(
                    offer.ItemDefinitionId.Value,
                    out ItemDefinitionSO item))
                continue;
            float ordinaryMargin = CalculateNetMargin(offer.cost, item.UnitPrice);
            float premiumRevenue = offer.cost
                * maximumFacilityPremium
                * GoldEconomyBalanceRules.MaximumWorkerRevenuePremium;
            float premiumMargin = CalculateNetMargin(premiumRevenue, item.UnitPrice);
            float ewu = embeddedWork.TryGetItemWork(item.ItemId, out float itemWork)
                ? itemWork
                : 0f;
            text.AppendLine(
                $"RETAIL_EWU sale={offer.id}, item={item.ItemId}, ewu={ewu:0.##}, "
                + $"internal={item.UnitPrice}, base={offer.cost}, "
                + $"ordinary_margin={ordinaryMargin:P1}, "
                + $"premium_margin={premiumMargin:P1}");
        }
        text.AppendLine();
        text.AppendLine("GUEST REQUEST PREMIUM MARGINS");
        Dictionary<string, ItemDefinitionSO> guestItems = itemDefinitions
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        foreach (GuestRequestDefinitionSO request in guestRequests)
        {
            int internalValue = CalculateRequirementInternalGold(
                request.serviceRequirements?.items,
                guestItems);
            float costEwu = CalculateRequirementEwu(
                request.serviceRequirements?.items,
                embeddedWork);
            int reward = Mathf.RoundToInt((request.successEffects ?? new())
                .Where(value => value != null
                    && value.kind == V20ContentEffectKind.Money)
                .Sum(value => value.amount));
            text.AppendLine(
                $"GUEST_REQUEST_EWU id={request.StableId}, kind={request.kind}, "
                + $"deadline={request.deadlineDays}, cost_ewu={costEwu:0.##}, "
                + $"internal={internalValue}, reward={reward}, "
                + $"net_margin={CalculateNetMargin(reward, internalValue):P1}, "
                + $"items={FormatContractRequirements(request.serviceRequirements?.items)}");
        }
        text.AppendLine();
        text.AppendLine("FACTION ROUTE CARGO VALUE");
        foreach (DungeonFactionDefinitionSO faction in dungeonFactions)
        {
            text.AppendLine(
                $"FACTION_ROUTE_EWU faction={faction.StableId}, kind=Trade, "
                + $"cargo_ewu={CalculateCargoEwu(faction.tradeCargo, embeddedWork):0.##}, "
                + $"cooldown={faction.tradeCooldownDays}, "
                + $"reference_share={CalculateCargoDailyShare(faction.tradeCargo, faction.tradeCooldownDays, embeddedWork):P1}, "
                + $"items={FormatCargo(faction.tradeCargo)}");
            text.AppendLine(
                $"FACTION_ROUTE_EWU faction={faction.StableId}, kind=Supply, "
                + $"cargo_ewu={CalculateCargoEwu(faction.supplyCargo, embeddedWork):0.##}, "
                + $"cooldown={faction.supplyCooldownDays}, "
                + $"reference_share={CalculateCargoDailyShare(faction.supplyCargo, faction.supplyCooldownDays, embeddedWork):P1}, "
                + $"items={FormatCargo(faction.supplyCargo)}");
        }
        text.AppendLine();
        text.AppendLine("AUTHORED FACTION CONTRACT BURDEN");
        foreach (FactionContractDefinitionSO contract in factionContracts)
        {
            float costEwu = 0f;
            List<string> exceptionalInputs = new();
            foreach (V20ItemAmountRequirement requirement in
                     contract.completionRequirements?.items
                     ?? new List<V20ItemAmountRequirement>())
            {
                if (requirement == null || !requirement.consume)
                    continue;
                string itemId = requirement.itemDefinitionId?.Trim() ?? string.Empty;
                if (string.Equals(
                        itemId,
                        EquipmentProgressionItemIds.LineageSeal,
                        StringComparison.Ordinal))
                {
                    exceptionalInputs.Add($"{itemId} x {requirement.amount} (irreversible)");
                    continue;
                }
                if (embeddedWork.TryGetItemWork(itemId, out float unitEwu))
                    costEwu += unitEwu * requirement.amount;
                else
                    exceptionalInputs.Add($"{itemId} x {requirement.amount} (unresolved)");
            }
            text.AppendLine(
                $"FACTION_CONTRACT_EWU id={contract.StableId}, kind={contract.kind}, "
                + $"deadline={contract.deadlineDays}, cost_ewu={costEwu:0.##}, "
                + $"cost_wd={costEwu / AuthoredFactionContractBalanceRules.WorkUnitsPerAdultDay:0.##}, "
                + $"reference_production={AuthoredFactionContractBalanceRules.CalculateReferenceProduction(contract.deadlineDays):0.##}, "
                + $"burden={costEwu / AuthoredFactionContractBalanceRules.CalculateReferenceProduction(contract.deadlineDays):P1}, "
                + $"items={FormatContractRequirements(contract.completionRequirements?.items)}, "
                + $"exceptional={string.Join("; ", exceptionalInputs)}");
        }
        text.AppendLine();
        text.AppendLine("REGIONAL SUPPLY CONTRACT MARGINS");
        foreach (RegionalContractBalanceRow row in BuildRegionalContractRows(
                     itemDefinitions))
        {
            text.AppendLine(
                $"CONTRACT_EWU item={row.ItemId}, kind={row.Kind}, amount={row.Amount}, "
                + $"internal={row.InternalValue}, reward={row.BaseReward}, "
                + $"ordinary_margin={row.BaseMargin:P1}, "
                + $"project_reward={row.ProjectReward}, "
                + $"project_margin={row.ProjectMargin:P1}");
        }
        text.AppendLine();
        text.AppendLine("MARKET SALE EWU RECOVERY");
        foreach (MarketSaleEwuRow row in BuildMarketSaleEwuRows(
                     itemDefinitions,
                     embeddedWork))
        {
            text.AppendLine(
                $"SALE_EWU item={row.Item.ItemId}, unit_price={row.Item.UnitPrice}, "
                + $"rate={row.Item.MarketSaleRate:0.###}, ewu={row.EmbeddedWork:0.##}, "
                + $"gold_per_ewu={row.GoldPerEmbeddedWork:0.###}, "
                + $"recovery={row.RecoveryRatio:P1}");
        }
        text.AppendLine();
        text.AppendLine("HIGHEST DISMANTLE EWU RATIOS (SKILL 100)");
        foreach (string row in BuildDismantleEwuRows(
                     buildings,
                     balance,
                     salvage,
                     embeddedWork)
                 .OrderByDescending(value => value.Ratio)
                 .ThenBy(value => value.Building.id)
                 .Take(20)
                 .Select(value =>
                     $"DISMANTLE_EWU id={value.Building.id}, "
                     + $"ratio={value.Ratio:P1}, invested={value.Invested:0.##}, "
                     + $"recovered={value.Recovered:0.##}, "
                     + $"asset={AssetDatabase.GetAssetPath(value.Building)}"))
        {
            text.AppendLine(row);
        }
        text.AppendLine();
        text.AppendLine($"failures={failures.Count}");
        foreach (string failure in failures)
            text.AppendLine("FAIL " + failure);
        return text.ToString();
    }

    private static void ValidateEmbeddedWork(
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures)
    {
        foreach (string itemId in embeddedWork.UnresolvedItemIds)
            failures.Add($"EWU could not be resolved for referenced item '{itemId}'.");
        foreach (string recipeId in embeddedWork.NonConvergentRecipeIds)
            failures.Add($"EWU propagation did not converge for recipe '{recipeId}'.");
    }

    private static void ValidateAuthoredFactionContracts(
        IReadOnlyCollection<FactionContractDefinitionSO> contracts,
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures)
    {
        if (contracts.Count != 18)
            failures.Add($"Expected 18 authored faction contracts, found {contracts.Count}.");

        foreach (FactionContractDefinitionSO contract in contracts)
        {
            float costEwu = 0f;
            foreach (V20ItemAmountRequirement requirement in
                     contract.completionRequirements?.items
                     ?? new List<V20ItemAmountRequirement>())
            {
                if (requirement == null || !requirement.consume)
                    continue;
                string itemId = requirement.itemDefinitionId?.Trim() ?? string.Empty;
                if (string.Equals(
                        itemId,
                        EquipmentProgressionItemIds.LineageSeal,
                        StringComparison.Ordinal))
                {
                    if (contract.kind != V20FactionContractKind.Strategic
                        || requirement.amount != 1)
                    {
                        failures.Add(
                            $"Contract '{contract.StableId}' must consume exactly one lineage seal "
                            + "as a strategic irreversible requirement.");
                    }
                    continue;
                }

                if (!embeddedWork.TryGetItemWork(itemId, out float unitEwu)
                    || unitEwu <= 0f)
                {
                    failures.Add(
                        $"Contract '{contract.StableId}' requirement '{itemId}' has no EWU.");
                    continue;
                }
                costEwu += unitEwu * requirement.amount;
            }

            if (costEwu <= 0f)
                continue;
            float referenceProduction =
                AuthoredFactionContractBalanceRules.CalculateReferenceProduction(
                    contract.deadlineDays);
            float burden = costEwu / referenceProduction;
            Vector2 band = AuthoredFactionContractBalanceRules.BurdenBand(contract.kind);
            if (burden < band.x || burden > band.y)
            {
                failures.Add(
                    $"Contract '{contract.StableId}' burden {burden:P1} is outside "
                    + $"{band.x:P0}..{band.y:P0} for {contract.kind}.");
            }
        }
    }

    private static void ValidateFactionRouteCargo(
        IReadOnlyCollection<DungeonFactionDefinitionSO> factions,
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures)
    {
        if (factions.Count != 6)
            failures.Add($"Expected 6 dungeon factions, found {factions.Count}.");
        foreach (DungeonFactionDefinitionSO faction in factions)
        {
            foreach (FactionCargoLine line in (faction.tradeCargo ?? new())
                         .Concat(faction.supplyCargo ?? new()))
            {
                if (line == null
                    || string.IsNullOrWhiteSpace(line.itemId)
                    || line.amount < 1)
                {
                    failures.Add($"Faction '{faction.StableId}' has invalid route cargo.");
                    continue;
                }
                if (!embeddedWork.TryGetItemWork(line.itemId, out float ewu)
                    || ewu <= 0f)
                {
                    failures.Add(
                        $"Faction '{faction.StableId}' cargo '{line.itemId}' has no EWU.");
                }
            }

            float tradeEwu = CalculateCargoEwu(faction.tradeCargo, embeddedWork);
            int expectedTrade = FactionRouteBalanceRules.CalculateCargoCooldownDays(
                tradeEwu,
                supply: false);
            if (faction.tradeCooldownDays != expectedTrade)
            {
                failures.Add(
                    $"Faction '{faction.StableId}' trade cooldown {faction.tradeCooldownDays} "
                    + $"must equal value-scaled target {expectedTrade} days.");
            }
            float supplyEwu = CalculateCargoEwu(faction.supplyCargo, embeddedWork);
            int expectedSupply = FactionRouteBalanceRules.CalculateCargoCooldownDays(
                supplyEwu,
                supply: true);
            if (faction.supplyCooldownDays != expectedSupply)
            {
                failures.Add(
                    $"Faction '{faction.StableId}' supply cooldown {faction.supplyCooldownDays} "
                    + $"must equal value-scaled target {expectedSupply} days.");
            }
            if (faction.reinforcementCooldownDays
                < FactionRouteBalanceRules.MinimumReinforcementCooldownDays)
            {
                failures.Add(
                    $"Faction '{faction.StableId}' reinforcement cooldown must be at least "
                    + $"{FactionRouteBalanceRules.MinimumReinforcementCooldownDays} days.");
            }
        }
    }

    private static void ValidateGuestRequests(
        IReadOnlyCollection<GuestRequestDefinitionSO> requests,
        IReadOnlyCollection<ItemDefinitionSO> itemDefinitions,
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures)
    {
        if (requests.Count != 14)
            failures.Add($"Expected 14 guest requests, found {requests.Count}.");
        Dictionary<string, ItemDefinitionSO> items = itemDefinitions
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        foreach (GuestRequestDefinitionSO request in requests)
        {
            IReadOnlyCollection<V20ItemAmountRequirement> requirements =
                request.serviceRequirements?.items
                ?? new List<V20ItemAmountRequirement>();
            if (requirements.Count == 0)
            {
                failures.Add($"Guest request '{request.StableId}' has no physical input.");
                continue;
            }
            foreach (V20ItemAmountRequirement requirement in requirements)
            {
                string itemId = requirement?.itemDefinitionId?.Trim() ?? string.Empty;
                if (!items.ContainsKey(itemId))
                    failures.Add($"Guest request '{request.StableId}' item '{itemId}' is missing.");
                if (!embeddedWork.TryGetItemWork(itemId, out float ewu) || ewu <= 0f)
                    failures.Add($"Guest request '{request.StableId}' item '{itemId}' has no EWU.");
            }

            int internalValue = CalculateRequirementInternalGold(requirements, items);
            int reward = Mathf.RoundToInt((request.successEffects ?? new())
                .Where(value => value != null
                    && value.kind == V20ContentEffectKind.Money)
                .Sum(value => value.amount));
            int expected = GoldEconomyBalanceRules.CalculatePremiumServiceReward(
                internalValue);
            if (reward != expected)
            {
                failures.Add(
                    $"Guest request '{request.StableId}' reward {reward} must equal "
                    + $"premium-service target {expected}.");
            }
            float margin = CalculateNetMargin(reward, internalValue);
            if (margin < GoldEconomyBalanceRules.MinimumPremiumServiceNetMargin
                || margin > GoldEconomyBalanceRules.MaximumPremiumServiceNetMargin)
            {
                failures.Add(
                    $"Guest request '{request.StableId}' margin {margin:P1} is outside "
                    + $"{GoldEconomyBalanceRules.MinimumPremiumServiceNetMargin:P0}.."
                    + $"{GoldEconomyBalanceRules.MaximumPremiumServiceNetMargin:P0}.");
            }
        }
    }

    private static int CalculateRequirementInternalGold(
        IEnumerable<V20ItemAmountRequirement> requirements,
        IReadOnlyDictionary<string, ItemDefinitionSO> items) =>
        (requirements ?? Array.Empty<V20ItemAmountRequirement>())
            .Where(value => value != null && value.consume)
            .Sum(value => items.TryGetValue(value.itemDefinitionId, out ItemDefinitionSO item)
                ? item.UnitPrice * value.amount
                : 0);

    private static float CalculateRequirementEwu(
        IEnumerable<V20ItemAmountRequirement> requirements,
        EmbeddedWorkValueSnapshot embeddedWork) =>
        (requirements ?? Array.Empty<V20ItemAmountRequirement>())
            .Where(value => value != null && value.consume)
            .Sum(value => embeddedWork.TryGetItemWork(value.itemDefinitionId, out float ewu)
                ? ewu * value.amount
                : 0f);

    private static float CalculateCargoEwu(
        IEnumerable<FactionCargoLine> cargo,
        EmbeddedWorkValueSnapshot embeddedWork) =>
        (cargo ?? Array.Empty<FactionCargoLine>())
            .Where(value => value != null)
            .Sum(value => embeddedWork.TryGetItemWork(value.itemId, out float ewu)
                ? ewu * value.amount
                : 0f);

    private static float CalculateCargoDailyShare(
        IEnumerable<FactionCargoLine> cargo,
        int cooldownDays,
        EmbeddedWorkValueSnapshot embeddedWork) =>
        CalculateCargoEwu(cargo, embeddedWork)
        / Mathf.Max(1, cooldownDays)
        / FactionRouteBalanceRules.ReferenceDailyProduction;

    private static void ValidateProcurementCatalog(
        IReadOnlyCollection<AuthoredStockCategoryRecord> stockCategories,
        IReadOnlyCollection<ItemDefinitionSO> itemDefinitions,
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures)
    {
        Dictionary<string, ItemDefinitionSO> items = itemDefinitions
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        foreach (AuthoredStockCategoryRecord stock in stockCategories
                     .Where(value => value != null && value.dailyBaseAmount > 0))
        {
            string itemId = stock.deliveryItemId?.Trim() ?? string.Empty;
            if (itemId.Length == 0)
            {
                failures.Add($"Stock category '{stock.id}' has no concrete delivery item.");
                continue;
            }

            if (!items.TryGetValue(itemId, out ItemDefinitionSO item))
            {
                failures.Add(
                    $"Stock category '{stock.id}' delivery item '{itemId}' is not authored.");
                continue;
            }

            if (item.StockCategory != stock.category)
            {
                failures.Add(
                    $"Stock category '{stock.id}' delivery item '{itemId}' has category "
                    + $"'{item.StockCategory}', expected '{stock.category}'.");
            }

            if (item.MaxStack <= 1)
            {
                failures.Add(
                    $"Stock category '{stock.id}' delivery item '{itemId}' is not stackable.");
            }

            if (!embeddedWork.TryGetItemWork(itemId, out float ewu) || ewu <= 0f)
            {
                failures.Add(
                    $"Stock category '{stock.id}' delivery item '{itemId}' has no positive EWU.");
                continue;
            }

            float goldPerEwu = stock.dailyUnitCost / ewu;
            float minimum = GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit
                * GoldEconomyBalanceRules.MinimumExternalPurchaseMarkup;
            float maximum = GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit
                * GoldEconomyBalanceRules.MaximumExternalPurchaseMarkup;
            if (goldPerEwu < minimum || goldPerEwu > maximum)
            {
                failures.Add(
                    $"Stock category '{stock.id}' delivery item '{itemId}' purchase value "
                    + $"{goldPerEwu:0.###} gold/EWU is outside {minimum:0.###}..{maximum:0.###}.");
            }
        }
    }

    private static void ValidateMarketSaleCatalog(
        IReadOnlyCollection<ItemDefinitionSO> itemDefinitions,
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures,
        bool enforceRecoveryBand)
    {
        string[] automaticSaleExclusions =
        {
            PhysicalItemIds.EquipmentModule,
            EquipmentProgressionItemIds.LineageSeal,
            "seed-lot:bloodleaf",
            "seed-lot:cave-mushroom",
            "seed-lot:dreamleaf",
            "seed-lot:ember-cotton",
            "seed-lot:ember-root",
            "seed-lot:frost-flax",
            "seed-lot:mire-reed",
            "seed-lot:moonflower",
            "seed-lot:night-grape",
            "seed-lot:shade-fiber",
            "seed-lot:spore-hemp",
            "seed-lot:twilight-grain"
        };
        Dictionary<string, ItemDefinitionSO> byId = itemDefinitions
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        foreach (string itemId in automaticSaleExclusions)
        {
            if (!byId.TryGetValue(itemId, out ItemDefinitionSO definition)
                || !(definition is ResourceItemDefinitionSO resource)
                || resource.CanSellToMarket)
            {
                failures.Add(
                    $"Automatic-sale exclusion '{itemId}' is missing or market-sellable.");
            }
        }

        foreach (MarketSaleEwuRow row in BuildMarketSaleEwuRows(
                     itemDefinitions,
                     embeddedWork))
        {
            if (row.EmbeddedWork <= 0f)
            {
                failures.Add(
                    $"Market-sellable item '{row.Item.ItemId}' has no positive EWU.");
                continue;
            }

            if (!enforceRecoveryBand)
            {
                continue;
            }

            if (row.RecoveryRatio < GoldEconomyBalanceRules.MinimumExternalSaleRecovery
                || row.RecoveryRatio > GoldEconomyBalanceRules.MaximumExternalSaleRecovery)
            {
                failures.Add(
                    $"Market item '{row.Item.ItemId}' EWU recovery {row.RecoveryRatio:P1} "
                    + $"is outside {GoldEconomyBalanceRules.MinimumExternalSaleRecovery:P0}.."
                    + $"{GoldEconomyBalanceRules.MaximumExternalSaleRecovery:P0}.");
            }
        }
    }

    private static void ValidateRetailCatalog(
        IReadOnlyCollection<SaleItem> retailOffers,
        IReadOnlyCollection<StockInfo> retailStockDefinitions,
        IReadOnlyCollection<ItemDefinitionSO> itemDefinitions,
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures)
    {
        Dictionary<string, ItemDefinitionSO> items = itemDefinitions
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        foreach (IGrouping<int, SaleItem> duplicate in retailOffers
                     .GroupBy(value => value.id)
                     .Where(value => value.Count() > 1))
        {
            failures.Add($"Retail sale id '{duplicate.Key}' is duplicated.");
        }

        foreach (StockInfo stock in retailStockDefinitions)
        {
            if (stock.multifly < 1f
                || stock.multifly > GoldEconomyBalanceRules.MaximumRetailFacilityPremium)
            {
                failures.Add(
                    $"Retail stock '{stock.name}' price multiplier {stock.multifly:0.###} "
                    + $"is outside 1..{GoldEconomyBalanceRules.MaximumRetailFacilityPremium:0.##}.");
            }
        }

        float maximumFacilityPremium = retailStockDefinitions.Count == 0
            ? 1f
            : retailStockDefinitions.Max(value => Mathf.Max(1f, value.multifly));
        foreach (SaleItem offer in retailOffers)
        {
            string itemId = offer.ItemDefinitionId.Value;
            if (!items.TryGetValue(itemId, out ItemDefinitionSO item))
            {
                failures.Add(
                    $"Retail sale '{offer.name}' references missing item '{itemId}'.");
                continue;
            }
            if (!embeddedWork.TryGetItemWork(itemId, out float ewu) || ewu <= 0f)
            {
                failures.Add(
                    $"Retail sale '{offer.name}' item '{itemId}' has no positive EWU.");
            }
            int expected = GoldEconomyBalanceRules.CalculateRetailBasePrice(item.UnitPrice);
            if (offer.cost != expected)
            {
                failures.Add(
                    $"Retail sale '{offer.name}' price {offer.cost} does not match "
                    + $"EWU-calibrated base price {expected} for '{itemId}'.");
            }
            if (offer.category != item.StockCategory)
            {
                failures.Add(
                    $"Retail sale '{offer.name}' category {offer.category} does not match "
                    + $"item '{itemId}' category {item.StockCategory}.");
            }

            float ordinaryMargin = CalculateNetMargin(offer.cost, item.UnitPrice);
            if (ordinaryMargin < 0.10f || ordinaryMargin > 0.20f)
            {
                failures.Add(
                    $"Retail sale '{offer.name}' ordinary margin {ordinaryMargin:P1} "
                    + "is outside 10%..20%.");
            }
            float premiumRevenue = offer.cost
                * maximumFacilityPremium
                * GoldEconomyBalanceRules.MaximumWorkerRevenuePremium;
            float premiumMargin = CalculateNetMargin(premiumRevenue, item.UnitPrice);
            if (premiumMargin < 0.20f || premiumMargin > 0.355f)
            {
                failures.Add(
                    $"Retail sale '{offer.name}' premium margin {premiumMargin:P1} "
                    + "is outside 20%..35%.");
            }
        }
    }

    private static void ValidateDeliverySettlement(ICollection<string> failures)
    {
        (int cost, int requested, int delivered, int expected)[] cases =
        {
            (40, 5, 0, 0),
            (40, 5, 1, 8),
            (40, 5, 2, 16),
            (40, 5, 5, 40),
            (1, 5, 1, 1),
            (1, 5, 5, 1),
            (40, 0, 5, 0),
            (-1, 5, 5, 0)
        };
        foreach ((int cost, int requested, int delivered, int expected) test in cases)
        {
            int actual = StockSupplyService.CalculateSettledDeliveryCost(
                test.cost,
                test.requested,
                test.delivered);
            if (actual != test.expected)
            {
                failures.Add(
                    $"Delivery settlement {test.cost}/{test.requested}/{test.delivered} "
                    + $"resolved {actual}, expected {test.expected}.");
            }
        }
    }

    private static void ValidateRejectedQualitySalePricing(
        ICollection<string> failures)
    {
        int previous = -1;
        foreach (CraftsmanshipQualityTier quality in Enum
                     .GetValues(typeof(CraftsmanshipQualityTier))
                     .Cast<CraftsmanshipQualityTier>())
        {
            int proceeds = ResourceStockPolicyRuntime
                .CalculateQualityRejectedSaleProceeds(
                    100,
                    GoldEconomyBalanceRules.TargetExternalSaleRecovery,
                    quality);
            if (proceeds < previous)
            {
                failures.Add(
                    $"Quality-rejected sale value decreased at {quality}: "
                    + $"{previous} -> {proceeds}.");
            }
            previous = proceeds;
        }

        int awful = ResourceStockPolicyRuntime.CalculateQualityRejectedSaleProceeds(
            100,
            GoldEconomyBalanceRules.TargetExternalSaleRecovery,
            CraftsmanshipQualityTier.Awful);
        int normal = ResourceStockPolicyRuntime.CalculateQualityRejectedSaleProceeds(
            100,
            GoldEconomyBalanceRules.TargetExternalSaleRecovery,
            CraftsmanshipQualityTier.Normal);
        int legendaryMaximum = ResourceStockPolicyRuntime
            .CalculateQualityRejectedSaleProceeds(
                100,
                GoldEconomyBalanceRules.MaximumExternalSaleRecovery,
                CraftsmanshipQualityTier.Legendary);
        if (awful != 42 || normal != 60 || legendaryMaximum != 98)
        {
            failures.Add(
                "Quality-rejected sale pricing boundary changed: "
                + $"awful={awful}, normal={normal}, legendaryMax={legendaryMaximum}.");
        }
        if (legendaryMaximum >= 100)
        {
            failures.Add(
                "Quality-rejected sale can recover the full internal item value.");
        }
        if (ResourceStockPolicyRuntime.CalculateQualityRejectedSaleProceeds(
                0,
                GoldEconomyBalanceRules.TargetExternalSaleRecovery,
                CraftsmanshipQualityTier.Normal) != 0
            || ResourceStockPolicyRuntime.CalculateQualityRejectedSaleProceeds(
                100,
                0f,
                CraftsmanshipQualityTier.Normal) != 0)
        {
            failures.Add(
                "Quality-rejected sale created proceeds for a zero-value or forbidden item.");
        }
    }

    private static float CalculateNetMargin(float revenue, float internalCost) =>
        revenue <= 0f ? 0f : (revenue - Mathf.Max(0f, internalCost)) / revenue;

    private static void ValidateRegionalContractEconomy(
        IReadOnlyCollection<ItemDefinitionSO> itemDefinitions,
        ICollection<string> failures)
    {
        foreach (RegionalContractBalanceRow row in BuildRegionalContractRows(
                     itemDefinitions))
        {
            if (row.BaseMargin < 0.10f || row.BaseMargin > 0.20f)
            {
                failures.Add(
                    $"Regional contract item '{row.ItemId}' ordinary margin "
                    + $"{row.BaseMargin:P1} is outside 10%..20%.");
            }
            if (row.ProjectMargin < 0.20f || row.ProjectMargin > 0.355f)
            {
                failures.Add(
                    $"Regional contract item '{row.ItemId}' project margin "
                    + $"{row.ProjectMargin:P1} is outside 20%..35%.");
            }
        }
    }

    private static IEnumerable<RegionalContractBalanceRow>
        BuildRegionalContractRows(IEnumerable<ItemDefinitionSO> itemDefinitions)
    {
        ResourceItemKind[] allowedKinds =
        {
            ResourceItemKind.Raw,
            ResourceItemKind.Intermediate,
            ResourceItemKind.FinishedGood,
            ResourceItemKind.Food,
            ResourceItemKind.Medicine,
            ResourceItemKind.Ammunition
        };
        HashSet<ResourceItemKind> allowed = new(allowedKinds);
        foreach (ResourceItemDefinitionSO item in itemDefinitions
                     .OfType<ResourceItemDefinitionSO>()
                     .Where(value => value != null
                         && allowed.Contains(value.Kind)
                         && value.UnitPrice >= RegionalSupplyContractSizing
                             .MinimumViableUnitPrice(value.Kind))
                     .OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            int amount = RegionalSupplyContractSizing.ResolveAmount(
                item.Kind,
                population: 3,
                completedResearchCount: 12,
                offerIndex: 0);
            int internalValue = item.UnitPrice * amount;
            int baseReward = GoldEconomyBalanceRules.CalculateRegionalContractReward(
                internalValue,
                1f);
            int projectReward = GoldEconomyBalanceRules.CalculateRegionalContractReward(
                internalValue,
                GoldEconomyBalanceRules.MaximumContractProjectMultiplier);
            yield return new RegionalContractBalanceRow(
                item.ItemId,
                item.Kind,
                amount,
                internalValue,
                baseReward,
                CalculateNetMargin(baseReward, internalValue),
                projectReward,
                CalculateNetMargin(projectReward, internalValue));
        }
    }

    private readonly struct RegionalContractBalanceRow
    {
        public RegionalContractBalanceRow(
            string itemId,
            ResourceItemKind kind,
            int amount,
            int internalValue,
            int baseReward,
            float baseMargin,
            int projectReward,
            float projectMargin)
        {
            ItemId = itemId;
            Kind = kind;
            Amount = amount;
            InternalValue = internalValue;
            BaseReward = baseReward;
            BaseMargin = baseMargin;
            ProjectReward = projectReward;
            ProjectMargin = projectMargin;
        }

        public string ItemId { get; }
        public ResourceItemKind Kind { get; }
        public int Amount { get; }
        public int InternalValue { get; }
        public int BaseReward { get; }
        public float BaseMargin { get; }
        public int ProjectReward { get; }
        public float ProjectMargin { get; }
    }

    private static IEnumerable<MarketSaleEwuRow> BuildMarketSaleEwuRows(
        IEnumerable<ItemDefinitionSO> itemDefinitions,
        EmbeddedWorkValueSnapshot embeddedWork)
    {
        foreach (ResourceItemDefinitionSO item in itemDefinitions
                     .OfType<ResourceItemDefinitionSO>()
                     .Where(value => value != null && value.CanSellToMarket)
                     .OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            float ewu = embeddedWork.TryGetItemWork(item.ItemId, out float itemWork)
                ? itemWork
                : 0f;
            float saleGold = item.UnitPrice * item.MarketSaleRate;
            float goldPerEwu = ewu > 0f ? saleGold / ewu : 0f;
            float recovery = GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit > 0f
                ? goldPerEwu / GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit
                : 0f;
            yield return new MarketSaleEwuRow(
                item,
                ewu,
                goldPerEwu,
                recovery);
        }
    }

    private static void ValidateDismantleEmbeddedWork(
        IReadOnlyCollection<BuildingSO> buildings,
        IBalanceWorkCalculator balance,
        IMaterialSalvageCalculator salvage,
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures)
    {
        foreach (DismantleEwuRow row in BuildDismantleEwuRows(
                     buildings,
                     balance,
                     salvage,
                     embeddedWork))
        {
            if (row.Ratio >= 0.85f)
            {
                failures.Add(
                    $"Building '{row.Building.id}' dismantle EWU ratio "
                    + $"{row.Ratio:P1} must remain below 85%.");
            }
        }
    }

    private static IEnumerable<DismantleEwuRow> BuildDismantleEwuRows(
        IReadOnlyCollection<BuildingSO> buildings,
        IBalanceWorkCalculator balance,
        IMaterialSalvageCalculator salvage,
        EmbeddedWorkValueSnapshot embeddedWork)
    {
        foreach (BuildingSO building in buildings)
        {
            ItemAmountDefinition[] materials = building.GetConstructionMaterials()
                .Where(value => value != null)
                .ToArray();
            if (materials.Any(value => !embeddedWork.ItemWork.ContainsKey(value.ItemId)))
                continue;
            float constructionWork = balance.CalculateConstruction(building);
            float invested = constructionWork + materials.Sum(value =>
                embeddedWork.ItemWork[value.ItemId] * value.Amount);
            MaterialSalvageResult result = salvage.Calculate(
                ResolveDismantleKind(building),
                constructionWork,
                materials,
                100f);
            if (result.RecoveredMaterials.Any(value =>
                    !embeddedWork.ItemWork.ContainsKey(value.ItemId)))
                continue;
            float recovered = result.RecoveredMaterials.Sum(value =>
                embeddedWork.ItemWork[value.ItemId] * value.Amount);
            yield return new DismantleEwuRow(
                building,
                invested,
                recovered);
        }
    }

    private readonly struct DismantleEwuRow
    {
        public DismantleEwuRow(
            BuildingSO building,
            float invested,
            float recovered)
        {
            Building = building;
            Invested = invested;
            Recovered = recovered;
        }

        public BuildingSO Building { get; }
        public float Invested { get; }
        public float Recovered { get; }
        public float Ratio => Invested <= 0f ? 0f : Recovered / Invested;
    }

    private readonly struct MarketSaleEwuRow
    {
        public MarketSaleEwuRow(
            ResourceItemDefinitionSO item,
            float embeddedWork,
            float goldPerEmbeddedWork,
            float recoveryRatio)
        {
            Item = item;
            EmbeddedWork = embeddedWork;
            GoldPerEmbeddedWork = goldPerEmbeddedWork;
            RecoveryRatio = recoveryRatio;
        }

        public ResourceItemDefinitionSO Item { get; }
        public float EmbeddedWork { get; }
        public float GoldPerEmbeddedWork { get; }
        public float RecoveryRatio { get; }
    }

    private static int MinimumFunctionalBomKinds(
        BuildingSO building,
        ConstructionBalanceClass balanceClass) => balanceClass switch
        {
            ConstructionBalanceClass.Storage => 1,
            ConstructionBalanceClass.Workstation => 2,
            ConstructionBalanceClass.Service => 1,
            ConstructionBalanceClass.Environment =>
                building.GetAbility<BuildingAutomationAbility>() != null
                || building.GetAbility<BuildingPowerProducerAbility>() != null
                || building.GetAbility<BuildingPowerConsumerAbility>() != null
                    ? 3
                    : building.GetAbility<BuildingUtilityConnectionAbility>() != null
                      || building.GetAbility<BuildingWaterFixtureAbility>() != null
                      || building.GetAbility<BuildingWastewaterProcessorAbility>() != null
                        ? 2
                        : 1,
            ConstructionBalanceClass.Defense =>
                building.GetAbility<BuildingDefenseAbility>() != null
                || building.GetAbility<BuildingTreasuryPoweredDefenseAbility>() != null
                    ? 2
                    : 1,
            ConstructionBalanceClass.Medical => 4,
            ConstructionBalanceClass.Precision => 4,
            ConstructionBalanceClass.Industrial => 3,
            ConstructionBalanceClass.Arcane => 5,
            ConstructionBalanceClass.Landmark => 4,
            _ => 0
        };

    private static string FormatDistribution(IReadOnlyList<float> sortedValues)
    {
        if (sortedValues == null || sortedValues.Count == 0)
            return "empty";
        return $"min {sortedValues[0]:0.##}, p50 {Percentile(sortedValues, 0.50f):0.##}, "
            + $"p90 {Percentile(sortedValues, 0.90f):0.##}, max {sortedValues[^1]:0.##}";
    }

    private static string FormatDistribution(IReadOnlyList<int> sortedValues)
    {
        if (sortedValues == null || sortedValues.Count == 0)
            return "empty";
        return $"min {sortedValues[0]}, p50 {Percentile(sortedValues, 0.50f):0.##}, "
            + $"p90 {Percentile(sortedValues, 0.90f):0.##}, max {sortedValues[^1]}";
    }

    private static float Percentile(IReadOnlyList<float> sortedValues, float percentile)
    {
        if (sortedValues.Count == 1)
            return sortedValues[0];
        float position = Mathf.Clamp01(percentile) * (sortedValues.Count - 1);
        int lower = Mathf.FloorToInt(position);
        int upper = Mathf.CeilToInt(position);
        return Mathf.Lerp(sortedValues[lower], sortedValues[upper], position - lower);
    }

    private static float Percentile(IReadOnlyList<int> sortedValues, float percentile)
    {
        if (sortedValues.Count == 1)
            return sortedValues[0];
        float position = Mathf.Clamp01(percentile) * (sortedValues.Count - 1);
        int lower = Mathf.FloorToInt(position);
        int upper = Mathf.CeilToInt(position);
        return Mathf.Lerp(sortedValues[lower], sortedValues[upper], position - lower);
    }

    private static DismantleTargetKind ResolveDismantleKind(BuildingSO building)
    {
        ConstructionBalanceClass balanceClass =
            V23BalanceWorkCalculator.ResolveConstructionClass(building);
        return balanceClass == ConstructionBalanceClass.Arcane
            ? DismantleTargetKind.ArcaneFacility
            : balanceClass is ConstructionBalanceClass.Precision
                or ConstructionBalanceClass.Medical
                or ConstructionBalanceClass.Industrial
                ? DismantleTargetKind.PrecisionIndustrialFacility
                : DismantleTargetKind.GeneralFacility;
    }

    private static string FormatProbabilities(
        float skill,
        float facility,
        float complexity)
    {
        int[] counts = new int[7];
        DeterministicCraftQualityResolver resolver = new();
        for (int a = -10; a <= 10; a++)
        for (int b = -10; b <= 10; b++)
        for (int c = -10; c <= 10; c++)
        {
            CraftQualityResolution result = resolver.Resolve(
                new CraftQualityRollSaveData
                {
                    randomA = a,
                    randomB = b,
                    randomC = c
                },
                skill,
                facility,
                0f,
                complexity);
            counts[(int)result.Tier]++;
        }
        const float total = 9261f;
        return string.Join(" / ", counts.Select((count, index) =>
            $"{(CraftsmanshipQualityTier)index} {count / total * 100f:0.#}%"));
    }

    private static string FormatV23Amounts(
        IEnumerable<ItemAmountDefinition> values) =>
        string.Join(", ", (values ?? Array.Empty<ItemAmountDefinition>())
            .Where(value => value != null)
            .Select(value => $"{value.ItemId} x {value.Amount}"));

    private static string FormatContractRequirements(
        IEnumerable<V20ItemAmountRequirement> values) =>
        string.Join(", ", (values ?? Array.Empty<V20ItemAmountRequirement>())
            .Where(value => value != null)
            .Select(value =>
                $"{value.itemDefinitionId} x {value.amount}"
                + (value.consume ? " consume" : " check")));

    private static string FormatCargo(IEnumerable<FactionCargoLine> values) =>
        string.Join(", ", (values ?? Array.Empty<FactionCargoLine>())
            .Where(value => value != null)
            .Select(value => $"{value.itemId} x {value.amount}"));

    private static string FormatV23Outputs(
        IEnumerable<ProductionOutputDefinition> values) =>
        string.Join(", ", (values ?? Array.Empty<ProductionOutputDefinition>())
            .Where(value => value != null)
            .Select(value =>
                $"{value.ItemId} x {value.Amount} @ {value.Probability:0.##}"));

    private static string FormatAmounts(IEnumerable<ItemAmountDefinition> values) =>
        string.Join(", ", (values ?? Array.Empty<ItemAmountDefinition>())
            .Where(value => value != null)
            .Select(value => $"{value.ItemId}×{value.Amount}"));

    private static string FormatOutputs(IEnumerable<ProductionOutputDefinition> values) =>
        string.Join(", ", (values ?? Array.Empty<ProductionOutputDefinition>())
            .Where(value => value != null)
            .Select(value => $"{value.ItemId}×{value.Amount}@{value.Probability:0.##}"));

    private static string Escape(string value) =>
        (value ?? string.Empty).Replace("|", "\\|");

    private static void WriteProjectFile(string relativePath, string content)
    {
        string absolute = Path.GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException("Output directory is invalid."));
        File.WriteAllText(absolute, content, new UTF8Encoding(false));
    }

    private sealed class EditorContentSource : IGameContentDefinitionSource
    {
        private readonly GameDomainContentCatalogSO domain;
        private readonly ItemDefinitionCatalogSO items;

        public EditorContentSource(
            GameDomainContentCatalogSO domain,
            ItemDefinitionCatalogSO items)
        {
            this.domain = domain ?? throw new ArgumentNullException(nameof(domain));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            if (typeof(T) == typeof(ItemDefinitionSO))
                return items.Definitions.Cast<T>().ToArray();
            return domain.GetAll<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            return values.Count == 1
                ? values[0]
                : throw new InvalidOperationException(
                    $"Expected one {typeof(T).Name}, found {values.Count}.");
        }
    }
}
#endif
