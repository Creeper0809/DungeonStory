using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class P1FacilitySynthesisAssetBuilder
{
    private const string BuildingFolder = "Assets/Resources/SO/Building/Modular";
    private const string RecipeFolder = "Assets/Resources/SO/Synthesis/P1";

    [MenuItem("DungeonStory/Debug/Synthesis/Ensure Modular Synthesis Assets")]
    public static void EnsureP1SynthesisAssetsFromMenu()
    {
        EnsureP1SynthesisAssets();
    }

    public static void EnsureP1SynthesisAssets()
    {
        AssetDatabase.Refresh();
        EnsureFolder(RecipeFolder);

        RecipeSpec[] specs = CreateRecipeSpecs();
        PruneStaleRecipeAssets(specs);
        foreach (RecipeSpec spec in specs)
        {
            EnsureRecipeAsset(spec);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureRecipeAsset(RecipeSpec spec)
    {
        string assetPath = $"{RecipeFolder}/{spec.AssetName}.asset";
        FacilitySynthesisRecipeSO recipe =
            AssetDatabase.LoadAssetAtPath<FacilitySynthesisRecipeSO>(assetPath);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<FacilitySynthesisRecipeSO>();
            AssetDatabase.CreateAsset(recipe, assetPath);
        }

        recipe.id = spec.Id;
        recipe.recipeId = spec.RecipeId;
        recipe.displayName = spec.DisplayName;
        recipe.description = spec.Description;
        recipe.resultBuilding = RequireBuilding(spec.ResultAssetName);
        recipe.materialBuildings = spec.MaterialAssetNames
            .Select(RequireBuilding)
            .ToArray();
        recipe.publicByDefault = spec.PublicByDefault;
        recipe.requiredResearchRecipeId = spec.RequiredResearchRecipeId;
        recipe.levelInheritanceRatio = spec.LevelInheritanceRatio;
        EditorUtility.SetDirty(recipe);
    }

    private static void PruneStaleRecipeAssets(IEnumerable<RecipeSpec> specs)
    {
        HashSet<string> allowedPaths = specs
            .Select(spec => $"{RecipeFolder}/{spec.AssetName}.asset")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in AssetDatabase.FindAssets("t:FacilitySynthesisRecipeSO", new[] { RecipeFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!allowedPaths.Contains(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static RecipeSpec[] CreateRecipeSpecs()
    {
        return new[]
        {
            new RecipeSpec(
                7001,
                "RS_CommercialGrill",
                "recipe_commercial_grill_1",
                "고기그릴 개량",
                "간이화덕과 조리손질대를 합쳐 처리량이 높은 고기그릴로 개량한다.",
                "D02_고기그릴",
                new[] { "D01_간이화덕", "D03_조리손질대" }),
            new RecipeSpec(
                7002,
                "RS_LogisticsShelf",
                "recipe_logistics_shelf_1",
                "대형보관선반 조립",
                "식재료선반과 상자더미를 묶어 대량 보관이 가능한 물류 선반으로 조립한다.",
                "L01_대형보관선반",
                new[] { "D10_식재료선반", "L02_상자더미" }),
            new RecipeSpec(
                7003,
                "RS_SecureDisplay",
                "recipe_commerce_secure_display_2",
                "잠금진열장 개조",
                "잡화 진열과 보관 부품을 결합해 고가 상품을 지키는 잠금진열장으로 개조한다.",
                "S03_잠금진열장",
                new[] { "S02_잡화진열선반", "S04_잡화상자" },
                publicByDefault: false,
                requiredResearchRecipeId: "recipe_commerce_secure_display_2",
                levelInheritanceRatio: 0.8f),

            new RecipeSpec(
                7011,
                "RS_PatrolBoard",
                "recipe_fortress_patrol_1",
                "순찰상황판 제작",
                "경보종의 신호 체계와 세력깃발의 표식을 합쳐 순찰상황판을 만든다.",
                "G03_순찰상황판",
                new[] { "G02_경보종", "E05_세력깃발" }),
            new RecipeSpec(
                7012,
                "RS_TacticalTable",
                "recipe_fortress_tactical_1",
                "전술지도탁자 제작",
                "경비초소의 기록과 중량훈련석의 훈련 자료를 모아 전술지도탁자를 만든다.",
                "G04_전술지도탁자",
                new[] { "G01_경비초소책상", "T03_중량훈련석" }),
            new RecipeSpec(
                7013,
                "RS_BattleBanner",
                "recipe_fortress_banner_2",
                "전투깃발 제작",
                "완성된 순찰 체계에 세력 표식을 더해 전투 집중도를 높이는 깃발을 만든다.",
                "G05_전투깃발",
                new[] { "G03_순찰상황판", "E05_세력깃발" },
                publicByDefault: false,
                requiredResearchRecipeId: "recipe_fortress_banner_2",
                levelInheritanceRatio: 0.8f),

            new RecipeSpec(
                7021,
                "RS_AlchemyBench",
                "recipe_arcane_alchemy_1",
                "연금술작업대 개조",
                "연구책상과 연구용책장을 결합해 실험과 기록을 함께 처리하는 작업대로 개조한다.",
                "Q02_연금술작업대",
                new[] { "Q01_연구책상", "Q03_연구용책장" }),
            new RecipeSpec(
                7022,
                "RS_ManaReservoir",
                "recipe_arcane_reservoir_1",
                "마력저장조 조립",
                "마력수정선반과 시약선반을 결합해 안정적인 대용량 마력저장조를 만든다.",
                "M02_마력저장조",
                new[] { "M01_마력수정선반", "Q04_시약선반" }),
            new RecipeSpec(
                7023,
                "RS_RitualFocus",
                "recipe_arcane_ritual_2",
                "의식초점석 조율",
                "룬안정기와 촛대의 의식 환경을 결합해 마력 흐름을 모으는 초점석을 조율한다.",
                "M04_의식초점석",
                new[] { "M03_룬안정기", "E07_촛대" },
                publicByDefault: false,
                requiredResearchRecipeId: "recipe_arcane_ritual_2",
                levelInheritanceRatio: 0.8f)
        };
    }

    private static BuildingSO RequireBuilding(string assetName)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            $"{BuildingFolder}/{assetName}.asset");
        if (building == null)
        {
            throw new InvalidOperationException($"Modular synthesis building is missing: {assetName}");
        }

        return building;
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private readonly struct RecipeSpec
    {
        public RecipeSpec(
            int id,
            string assetName,
            string recipeId,
            string displayName,
            string description,
            string resultAssetName,
            string[] materialAssetNames,
            bool publicByDefault = true,
            string requiredResearchRecipeId = "",
            float levelInheritanceRatio = 0.75f)
        {
            Id = id;
            AssetName = assetName;
            RecipeId = recipeId;
            DisplayName = displayName;
            Description = description;
            ResultAssetName = resultAssetName;
            MaterialAssetNames = materialAssetNames ?? Array.Empty<string>();
            PublicByDefault = publicByDefault;
            RequiredResearchRecipeId = requiredResearchRecipeId ?? string.Empty;
            LevelInheritanceRatio = Mathf.Clamp01(levelInheritanceRatio);
        }

        public int Id { get; }
        public string AssetName { get; }
        public string RecipeId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ResultAssetName { get; }
        public string[] MaterialAssetNames { get; }
        public bool PublicByDefault { get; }
        public string RequiredResearchRecipeId { get; }
        public float LevelInheritanceRatio { get; }
    }
}
