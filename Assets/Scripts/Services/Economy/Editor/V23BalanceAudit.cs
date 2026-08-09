#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        BuildingSO[] buildings = source.GetAll<BuildingSO>()
            .Where(value => value != null)
            .OrderBy(value => value.id)
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

        List<string> failures = new();
        ValidateCounts(buildings, recipes, equipment, apparel, textiles, failures);
        ValidateBuildings(buildings, balance, failures);
        ValidateRecipes(recipes, balance, failures);
        ValidateEquipment(equipment, failures);
        ValidateApparel(apparel, textiles, failures);
        ValidateSalvage(buildings, salvage, failures);

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
            BuildReport(buildings, recipes, equipment, apparel, textiles, failures));
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
        if (recipes.Count != 354)
            failures.Add($"Expected 354 recipes, found {recipes.Count}.");
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
            if (recipe.Inputs.Count == 0 || recipe.Outputs.Count == 0)
                failures.Add($"Recipe '{recipe.RecipeId}' lacks concrete input/output items.");
            if (recipe.Inputs.Any(value => value == null
                    || value.Amount <= 0
                    || value.ItemId.StartsWith("stock-item:", StringComparison.Ordinal)))
                failures.Add($"Recipe '{recipe.RecipeId}' has an invalid or abstract input.");
            if (balance.CalculateRecipe(recipe) <= 0f)
                failures.Add($"Recipe '{recipe.RecipeId}' has no V23 work amount.");
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
        IReadOnlyCollection<ProductionRecipeSO> recipes,
        IReadOnlyCollection<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyCollection<ApparelDefinitionSO> apparel,
        IReadOnlyCollection<TextileMaterialDefinitionSO> textiles,
        IReadOnlyList<string> failures)
    {
        StringBuilder text = new();
        text.AppendLine("V23 BALANCE AUDIT");
        text.AppendLine($"buildings={buildings.Count}");
        text.AppendLine($"recipes={recipes.Count}");
        text.AppendLine($"equipment={equipment.Count}");
        text.AppendLine($"apparel={apparel.Count}");
        text.AppendLine($"textiles={textiles.Count}");
        text.AppendLine($"failures={failures.Count}");
        foreach (string failure in failures)
            text.AppendLine("FAIL " + failure);
        return text.ToString();
    }

    private static DismantleTargetKind ResolveDismantleKind(BuildingSO building)
    {
        ConstructionBalanceClass balanceClass =
            V23BalanceWorkCalculator.ResolveConstructionClass(building);
        return balanceClass == ConstructionBalanceClass.Arcane
            ? DismantleTargetKind.ArcaneFacility
            : balanceClass is ConstructionBalanceClass.Precision
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
