using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class P1FacilityShopAssetBuilder
{
    private const string BlueprintFolder = "Assets/Resources/SO/Blueprint/P1";

    private static readonly BlueprintSpec[] BlueprintSpecs =
    {
        new BlueprintSpec(
            6101,
            "BP_CommercialBasics",
            "상업 확장 설계도",
            "연회 식당과 전문 제작을 위한 2단계 상업 파츠를 즉시 해금한다.",
            FacilityShopRarity.Common,
            120,
            18f,
            new[]
            {
                "D06_대형연회식탁",
                "D08_푹신한의자",
                "D11_고기걸이",
                "D12_술음료장",
                "S08_대장작업대"
            }),
        new BlueprintSpec(
            6102,
            "BP_DefenseBasics",
            "요새화 설계도",
            "경비 동선과 병영을 강화하는 전술·보안 파츠를 즉시 해금한다.",
            FacilityShopRarity.Common,
            140,
            22f,
            new[]
            {
                "G03_순찰상황판",
                "G04_전술지도탁자",
                "G06_전리품거치대",
                "L06_무기로커"
            }),
        new BlueprintSpec(
            6103,
            "BP_SupportBasics",
            "생활 지원 설계도",
            "숙소·위생·식재료 물류를 개선하는 2단계 생활 파츠를 즉시 해금한다.",
            FacilityShopRarity.Common,
            130,
            20f,
            new[]
            {
                "R02_정식침대",
                "R03_이층침대",
                "R05_협탁",
                "R06_옷장",
                "H04_목욕통",
                "H07_바닥배수구",
                "L05_식재료저장함"
            }),
        new BlueprintSpec(
            6104,
            "BP_ArcaneBasics",
            "비전 연구 설계도",
            "전문 연구와 의식·마력 물류를 위한 2단계 비전 파츠를 즉시 해금한다.",
            FacilityShopRarity.Common,
            130,
            24f,
            new[]
            {
                "Q05_표본보관장",
                "Q06_설계판",
                "L07_마력보관함",
                "R10_침실용책장"
            }),
        new BlueprintSpec(
            6191,
            "BP_BattleDining",
            "상권 통합 설계도",
            "진열과 보관 부품을 결합해 잠금진열장으로 개조하는 희귀 조합식이다.",
            FacilityShopRarity.Rare,
            260,
            45f,
            Array.Empty<string>(),
            unlockRecipeIds: new[] { "recipe_commerce_secure_display_2" }),
        new BlueprintSpec(
            6192,
            "BP_TrapChain",
            "전술 지휘 설계도",
            "순찰 체계와 세력 표식을 결합해 전투깃발을 만드는 희귀 조합식이다.",
            FacilityShopRarity.Rare,
            280,
            50f,
            Array.Empty<string>(),
            unlockRecipeIds: new[] { "recipe_fortress_banner_2" }),
        new BlueprintSpec(
            6193,
            "BP_StormFireTrap",
            "비전 공명 설계도",
            "룬 안정과 의식 조명을 결합해 의식초점석을 조율하는 희귀 조합식이다.",
            FacilityShopRarity.Rare,
            340,
            60f,
            Array.Empty<string>(),
            unlockRecipeIds: new[] { "recipe_arcane_ritual_2" })
    };

    [MenuItem("DungeonStory/Debug/Facility Shop/Ensure P1 Facility Shop Assets")]
    public static void EnsureP1FacilityShopAssets()
    {
        EnsureFolder(BlueprintFolder);

        foreach (BlueprintSpec spec in BlueprintSpecs)
        {
            FacilityBlueprintSO blueprint = LoadOrCreateBlueprint(spec);
            blueprint.id = spec.Id;
            blueprint.blueprintName = spec.DisplayName;
            blueprint.description = spec.Description;
            blueprint.rarity = spec.Rarity;
            blueprint.defaultCost = spec.Cost;
            blueprint.researchWorkRequired = spec.ResearchWorkRequired;
            blueprint.unlocks = CreateUnlocks(spec);
            EditorUtility.SetDirty(blueprint);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static FacilityBlueprintSO LoadOrCreateBlueprint(BlueprintSpec spec)
    {
        string path = $"{BlueprintFolder}/{spec.AssetName}.asset";
        FacilityBlueprintSO blueprint = AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>(path);
        if (blueprint != null)
        {
            return blueprint;
        }

        blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        AssetDatabase.CreateAsset(blueprint, path);
        return blueprint;
    }

    private static int[] ResolveBuildingIds(IEnumerable<string> assetNames)
    {
        return assetNames?
            .Select(LoadBuilding)
            .Where((building) => building != null)
            .Select((building) => building.id)
            .Distinct()
            .ToArray()
            ?? Array.Empty<int>();
    }

    private static BlueprintUnlockCollection CreateUnlocks(BlueprintSpec spec)
    {
        BlueprintUnlockCollection unlocks = new BlueprintUnlockCollection();
        foreach (int buildingId in ResolveBuildingIds(spec.ConstructionUnlockAssetNames))
        {
            unlocks.Add(new BlueprintBuildingUnlock { buildingId = buildingId });
        }

        foreach (int buildingId in ResolveBuildingIds(spec.BasicPurchaseBuildingAssetNames))
        {
            unlocks.Add(new BlueprintBasicPurchaseUnlock { buildingId = buildingId });
        }

        foreach (string recipeId in spec.UnlockRecipeIds
            .Where(recipeId => !string.IsNullOrWhiteSpace(recipeId))
            .Distinct())
        {
            unlocks.Add(new BlueprintRecipeUnlock { recipeId = recipeId });
        }

        return unlocks;
    }

    private static BuildingSO LoadBuilding(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        BuildingSO modular = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            $"Assets/Resources/SO/Building/Modular/{assetName}.asset");
        return modular != null
            ? modular
            : AssetDatabase.LoadAssetAtPath<BuildingSO>($"Assets/Resources/SO/Building/P1/{assetName}.asset");
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

    private readonly struct BlueprintSpec
    {
        public readonly int Id;
        public readonly string AssetName;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly FacilityShopRarity Rarity;
        public readonly int Cost;
        public readonly float ResearchWorkRequired;
        public readonly string[] ConstructionUnlockAssetNames;
        public readonly string[] BasicPurchaseBuildingAssetNames;
        public readonly string[] UnlockRecipeIds;

        public BlueprintSpec(
            int id,
            string assetName,
            string displayName,
            string description,
            FacilityShopRarity rarity,
            int cost,
            float researchWorkRequired,
            string[] constructionUnlockAssetNames,
            string[] basicPurchaseBuildingAssetNames = null,
            string[] unlockRecipeIds = null)
        {
            Id = id;
            AssetName = assetName;
            DisplayName = displayName;
            Description = description;
            Rarity = rarity;
            Cost = Mathf.Max(0, cost);
            ResearchWorkRequired = Mathf.Max(1f, researchWorkRequired);
            ConstructionUnlockAssetNames = constructionUnlockAssetNames ?? Array.Empty<string>();
            BasicPurchaseBuildingAssetNames = basicPurchaseBuildingAssetNames ?? Array.Empty<string>();
            UnlockRecipeIds = unlockRecipeIds ?? Array.Empty<string>();
        }
    }
}
