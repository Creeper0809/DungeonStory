#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResourceEconomyAssetBuilder
{
    public const int ExpectedItemCount = 108;
    public const int ExpectedRecipeCount = 109;
    public const int ExpectedCropCount = 8;
    public const int ExpectedMaterialCount = 12;
    public const int ExpectedSubstanceCount = 9;
    private const int ExpectedCoreSubstanceCount = 7;

    private const string Root = "Assets/Resources/SO/Economy";
    private const string ItemRoot = Root + "/Items";
    private const string RecipeRoot = Root + "/Recipes";
    private const string CropRoot = Root + "/Crops";
    private const string MaterialRoot = Root + "/Materials";
    private const string LegacySubstanceRoot = Root + "/Substances";

    [MenuItem("Tools/DungeonStory/Economy/Rebuild Resource Economy Content")]
    public static void Rebuild()
    {
        EnsureFolders(ItemRoot, RecipeRoot, CropRoot, MaterialRoot);
        ValidateNoLegacySubstanceAssets();
        ResourceItemDefinitionSO[] items = BuildItems();
        ProductionRecipeSO[] recipes = BuildRecipes();
        CropDefinitionSO[] crops = BuildCrops();
        CraftMaterialDefinitionSO[] materials = BuildMaterials();
        ProductionWorkshopContentAssetBuilder.EnsureAssets();

        RequireCount(items.Length, ExpectedItemCount, "items");
        RequireCount(recipes.Length, ExpectedRecipeCount, "recipes");
        RequireCount(crops.Length, ExpectedCropCount, "crops");
        RequireCount(materials.Length, ExpectedMaterialCount, "materials");
        RequireCount(
            items.Count(item => item.TryGetFeature(out SubstanceItemFeature _)),
            ExpectedCoreSubstanceCount,
            "core item substance features");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Resource economy rebuilt: {items.Length} items, "
            + $"{recipes.Length} recipes, {crops.Length} crops, "
            + $"{materials.Length} materials; substance definitions are projected from item features.");
    }

    private static void RequireCount(int actual, int expected, string contentKind)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Resource economy must contain {expected} {contentKind}, found {actual}.");
        }
    }

    private static ResourceItemDefinitionSO[] BuildItems()
    {
        ItemSpec[] specs =
        {
            I("resource:twilight-grain", "황혼곡", "죽, 빵, 술과 사료의 바탕이 되는 검푸른 곡물.", StockCategory.Food, ResourceItemKind.Raw, ResourceIngredientTag.Plant, 2, 0.35f, 75, "research:agriculture:field"),
            I("resource:ember-root", "잿불뿌리", "열량이 높고 보존에 유리한 뿌리 작물.", StockCategory.Food, ResourceItemKind.Raw, ResourceIngredientTag.Plant, 2, 0.45f, 75, "research:agriculture:field"),
            I("resource:night-grape", "밤포도", "시럽, 술과 호화식에 쓰는 단맛 강한 열매.", StockCategory.Food, ResourceItemKind.Raw, ResourceIngredientTag.Plant, 4, 0.25f, 75, "research:agriculture:field"),
            I("resource:cave-mushroom", "동굴버섯", "식물성 단백질과 약효 성분을 가진 균류.", StockCategory.Food, ResourceItemKind.Raw, ResourceIngredientTag.Fungus, 3, 0.25f, 75, "research:agriculture:gathering"),
            I("resource:bloodleaf", "혈엽", "소독제와 전투 촉진제에 쓰는 붉은 약초.", StockCategory.Medicine, ResourceItemKind.Raw, ResourceIngredientTag.Plant | ResourceIngredientTag.Blood, 5, 0.1f, 75, "research:pharmacology:herbalism"),
            I("resource:moonflower", "월화", "차, 약품과 마나 약물에 쓰는 야광 꽃.", StockCategory.Medicine, ResourceItemKind.Raw, ResourceIngredientTag.Plant | ResourceIngredientTag.Arcane, 7, 0.08f, 75, "research:pharmacology:herbalism"),
            I("resource:dreamleaf", "몽엽", "진통, 마취와 유흥 약물에 쓰는 잎.", StockCategory.Medicine, ResourceItemKind.Raw, ResourceIngredientTag.Plant, 6, 0.08f, 75, "research:pharmacology:herbalism"),
            I("resource:shade-fiber", "그늘섬유", "천, 붕대, 활시위와 깔짚의 원료.", StockCategory.General, ResourceItemKind.Raw, ResourceIngredientTag.Plant | ResourceIngredientTag.Fiber, 2, 0.12f, 75, "research:textile:fiber"),
            I("resource:grass-straw", "풀·짚", "사료, 퇴비, 깔짚과 저급 연료에 쓰는 섬유질.", StockCategory.General, ResourceItemKind.Raw, ResourceIngredientTag.Plant | ResourceIngredientTag.Fiber, 1, 0.08f, 100, "research:agriculture:gathering"),
            I("resource:log", "원목", "목재, 숯, 활과 가구의 원료.", StockCategory.General, ResourceItemKind.Raw, ResourceIngredientTag.Wood, 3, 1.8f, 40, "research:forestry:logging"),
            I("resource:dark-resin", "암흑수액", "접착제, 처리 목재, 연고와 향에 쓰는 수액.", StockCategory.General, ResourceItemKind.Raw, ResourceIngredientTag.Plant, 8, 0.2f, 50, "research:forestry:logging"),
            I("resource:stone", "석재", "벽, 바닥, 엄폐와 석재 장비에 쓰는 거친 돌.", StockCategory.General, ResourceItemKind.Raw, ResourceIngredientTag.Mineral, 1, 1.6f, 50, "research:mining:surface"),
            I("resource:coal", "석탄", "제강, 증류와 고열 시설에 쓰는 연료.", StockCategory.Fuel, ResourceItemKind.Raw, ResourceIngredientTag.Mineral | ResourceIngredientTag.Fuel, 3, 0.8f, 75, "research:mining:sorting"),
            I("resource:iron-ore", "철광석", "철괴와 제강 재료가 되는 광석.", StockCategory.General, ResourceItemKind.Raw, ResourceIngredientTag.Mineral, 4, 1.2f, 60, "research:mining:sorting"),
            I("resource:gold-ore", "금광석", "금괴, 장식과 권위 설비의 원료.", StockCategory.General, ResourceItemKind.Raw, ResourceIngredientTag.Mineral, 18, 1.3f, 40, "research:mining:sorting"),
            I("resource:saltstone", "소금석", "보존, 무두질과 소독에 쓰는 광물.", StockCategory.General, ResourceItemKind.Raw, ResourceIngredientTag.Mineral, 5, 0.5f, 60, "research:mining:surface"),
            I("resource:mana-crystal", "마나 결정", "흑강, 룬가죽, 고급 약품과 비전 연구의 핵심.", StockCategory.Mana, ResourceItemKind.Raw, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, 28, 0.35f, 30, "research:mining:mana"),
            I("resource:meat", "생고기", "육식·혼합식, 육포와 동물 사료의 원료.", StockCategory.Food, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Meat, 4, 0.7f, 50, "research:husbandry:capture"),
            I("resource:blood", "피", "약품, 촉진제와 의식 재료.", StockCategory.Biological, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Blood | ResourceIngredientTag.Forbidden, 7, 0.5f, 50, "research:husbandry:capture"),
            I("resource:fat", "지방", "요리, 비누, 양초, 연고와 연료의 원료.", StockCategory.Biological, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Fat, 4, 0.45f, 50, "research:husbandry:capture"),
            I("resource:hide", "생가죽", "무두질과 저급 침구에 쓰는 가죽.", StockCategory.General, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Fiber, 6, 0.9f, 40, "research:textile:tanning"),
            I("resource:wool", "털", "보온 천과 침구의 원료.", StockCategory.General, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Fiber, 6, 0.35f, 60, "research:husbandry:selective"),
            I("resource:feather", "깃털", "화살과 가벼운 침구의 원료.", StockCategory.General, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Fiber, 3, 0.05f, 100, "research:husbandry:selective"),
            I("resource:milk", "우유", "치즈, 채식식과 약품의 원료.", StockCategory.Food, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Milk, 4, 0.8f, 50, "research:husbandry:selective"),
            I("resource:egg", "알", "채식식, 제과와 약품의 원료.", StockCategory.Food, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Egg, 3, 0.25f, 60, "research:husbandry:selective"),
            I("resource:bone", "뼈", "무기 재질, 장신구, 퇴비와 연금 재료.", StockCategory.General, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Mineral, 3, 0.4f, 75, "research:husbandry:capture"),
            I("resource:horn", "뿔", "활, 장신구와 룬 탄두의 재료.", StockCategory.General, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Mineral, 8, 0.45f, 50, "research:husbandry:selective"),
            I("resource:fang", "송곳니", "장신구, 독소와 관통 탄두의 재료.", StockCategory.General, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Mineral, 9, 0.15f, 75, "research:husbandry:capture"),
            I("resource:rune-dust", "룬 가루", "고위험 야생동물에서 얻는 원정 길잡이 촉매.", StockCategory.Mana, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Arcane, 24, 0.05f, 50, "research:husbandry:capture"),
            I("resource:manure", "분뇨", "퇴비, 실내 재배 영양제와 저급 연료의 원료.", StockCategory.Biological, ResourceItemKind.AnimalProduct, ResourceIngredientTag.Spoiled, 0, 0.5f, 75, "research:husbandry:feed"),
            I("waste:plant-rot", "식물성 부패물", "상한 식물성 음식. 퇴비와 저급 사료로 쓸 수 있다.", StockCategory.General, ResourceItemKind.Waste, ResourceIngredientTag.Plant | ResourceIngredientTag.Spoiled, 0, 0.6f, 75, "research:agriculture:compost"),
            I("waste:animal-rot", "동물성 부패물", "상한 동물성 음식. 육식동물 사료와 연료로 쓸 수 있다.", StockCategory.Biological, ResourceItemKind.Waste, ResourceIngredientTag.Meat | ResourceIngredientTag.Spoiled, 0, 0.7f, 75, "research:agriculture:compost"),
            I("waste:mixed-rot", "혼합 부패물", "재료가 섞여 식성을 구분하기 어려운 부패물.", StockCategory.General, ResourceItemKind.Waste, ResourceIngredientTag.Plant | ResourceIngredientTag.Meat | ResourceIngredientTag.Spoiled, 0, 0.65f, 75, "research:agriculture:compost"),
            I("waste:forbidden-rot", "금기 부패물", "금기 재료가 섞인 오염된 부패물.", StockCategory.Biological, ResourceItemKind.Waste, ResourceIngredientTag.Meat | ResourceIngredientTag.Spoiled | ResourceIngredientTag.Forbidden, 0, 0.7f, 50, "research:control:blood-show"),

            I("material:flour", "밀가루", "빵, 파이와 고급식의 기초 중간재.", StockCategory.Food, ResourceItemKind.Intermediate, ResourceIngredientTag.Plant, 4, 0.3f, 75, "research:cuisine:milling"),
            I("material:starch", "전분", "보존식과 접착 가공에 쓰는 농산 중간재.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Plant, 3, 0.25f, 75, "research:cuisine:milling"),
            I("material:syrup", "밤포도 시럽", "호화식과 발효 음료에 쓰는 농축액.", StockCategory.Food, ResourceItemKind.Intermediate, ResourceIngredientTag.Plant, 7, 0.35f, 50, "research:cuisine:fermentation"),
            I("material:alcohol", "알코올", "술, 소독제와 연금 용매의 기반.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Plant, 6, 0.5f, 50, "research:cuisine:fermentation"),
            I("material:lumber", "목재", "건축, 가구, 활과 무기 자루에 쓰는 규격재.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Wood, 6, 1.2f, 50, "research:forestry:sawmill"),
            I("material:charcoal", "숯", "제강과 고온 가공에 적합한 정제 연료.", StockCategory.Fuel, ResourceItemKind.Intermediate, ResourceIngredientTag.Fuel, 5, 0.45f, 75, "research:forestry:charcoal"),
            I("material:stone-block", "석재 블록", "벽, 바닥, 엄폐와 석조 장식의 재료.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, 4, 1.3f, 60, "research:mining:stonecutting"),
            I("material:iron-ingot", "철괴", "철 장비와 강철의 주재료.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, 9, 0.9f, 50, "research:metallurgy:iron"),
            I("material:steel-ingot", "강철", "고급 무기와 방어구에 쓰는 강한 금속.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, 16, 0.85f, 50, "research:metallurgy:steel"),
            I("material:gold-ingot", "금괴", "권위 시설, 장식과 고가 장비의 재료.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, 60, 1.35f, 30, "research:metallurgy:precious"),
            I("material:blacksteel-ingot", "흑강", "마나를 머금은 최고급 금속.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, 70, 1f, 25, "research:metallurgy:blacksteel"),
            I("material:cloth", "천", "의복, 붕대와 가벼운 방어구의 주재료.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber, 7, 0.2f, 75, "research:textile:fiber"),
            I("material:leather", "가죽", "균형 잡힌 연갑과 장비의 주재료.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber, 10, 0.45f, 60, "research:textile:tanning"),
            I("material:rune-leather", "룬가죽", "고방어와 마법 저항을 제공하는 가죽.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber | ResourceIngredientTag.Arcane, 30, 0.4f, 40, "research:textile:rune-leather"),
            I("material:dreamweave", "몽직물", "초경량과 정신 저항을 제공하는 비전 직물.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber | ResourceIngredientTag.Arcane, 34, 0.12f, 40, "research:textile:dreamweave"),
            I("material:compost", "퇴비", "밭과 실내 재배에 쓰는 토양 영양제.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Plant | ResourceIngredientTag.Spoiled, 3, 0.7f, 75, "research:agriculture:compost"),
            I("material:alchemical-solvent", "연금 용매", "약효 성분과 마나를 안정적으로 섞는 용매.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Arcane, 12, 0.3f, 50, "research:pharmacology:distillation"),
            I("material:tallow", "정제 지방", "비누, 양초와 연고의 기초.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Fat, 6, 0.35f, 60, "research:cuisine:livestock"),
            I("material:bowstring", "활시위", "활과 석궁 제작에 쓰는 고장력 섬유.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber, 8, 0.08f, 50, "research:textile:fiber"),
            I("material:treated-lumber", "처리 목재", "수액으로 방습·강화한 목재.", StockCategory.General, ResourceItemKind.Intermediate, ResourceIngredientTag.Wood, 12, 1.15f, 40, "research:forestry:treated"),
            I("material:cheese", "치즈", "보존성이 좋은 채식 단백질 식재료.", StockCategory.Food, ResourceItemKind.Intermediate, ResourceIngredientTag.Milk, 8, 0.4f, 50, "research:cuisine:livestock"),
            I("material:low-fuel", "저급 연료", "부패물과 분뇨를 굳힌 냄새나는 연료.", StockCategory.Fuel, ResourceItemKind.Intermediate, ResourceIngredientTag.Spoiled | ResourceIngredientTag.Fuel, 1, 0.8f, 75, "research:agriculture:compost"),
            I("material:rot-toxin", "부패 독소", "금기 약물과 독성 의식에 쓰는 농축 독소.", StockCategory.Biological, ResourceItemKind.Intermediate, ResourceIngredientTag.Spoiled | ResourceIngredientTag.Forbidden, 12, 0.2f, 40, "research:arcane:alchemy"),

            M("food:grain-porridge", "황혼곡죽", "값싸고 속이 편한 비건 단순식.", ResourceIngredientTag.Plant, 5, 0.6f, 50, "research:cuisine:crops", MealQualityTier.Simple, 35f, 0f, 360f),
            M("food:root-stew", "잿불뿌리 스튜", "열량이 높은 비건 스튜.", ResourceIngredientTag.Plant, 7, 0.7f, 50, "research:cuisine:crops", MealQualityTier.Simple, 40f, 2f, 360f),
            M("food:mushroom-soup", "동굴버섯국", "단백질이 풍부한 비건 국.", ResourceIngredientTag.Fungus, 7, 0.65f, 50, "research:cuisine:crops", MealQualityTier.Simple, 36f, 1f, 330f),
            M("food:garden-meal", "정원 요리", "곡물과 뿌리, 버섯을 섞은 비건 고급식.", ResourceIngredientTag.Plant | ResourceIngredientTag.Fungus, 12, 0.75f, 40, "research:cuisine:vegan", MealQualityTier.Fine, 50f, 4f, 420f),
            M("food:egg-pancake", "달걀전", "알과 곡물로 만든 채식식.", ResourceIngredientTag.Egg | ResourceIngredientTag.Plant, 11, 0.65f, 40, "research:cuisine:livestock", MealQualityTier.Fine, 50f, 4f, 360f),
            M("food:cheese-mushroom", "치즈버섯찜", "치즈와 버섯을 곁들인 채식 고급식.", ResourceIngredientTag.Milk | ResourceIngredientTag.Fungus, 14, 0.7f, 40, "research:cuisine:livestock", MealQualityTier.Fine, 50f, 5f, 390f),
            M("food:roasted-meat", "고기구이", "간단히 구운 육식 단순식.", ResourceIngredientTag.Meat, 9, 0.75f, 40, "research:cuisine:livestock", MealQualityTier.Simple, 42f, 3f, 300f),
            M("food:boar-stew", "멧돼지 스튜", "고기와 뿌리를 끓인 혼합 고급식.", ResourceIngredientTag.Meat | ResourceIngredientTag.Plant, 15, 0.85f, 40, "research:cuisine:livestock", MealQualityTier.Fine, 55f, 7f, 390f),
            M("food:meat-pie", "고기 파이", "고기와 밀가루를 쓴 혼합 고급식.", ResourceIngredientTag.Meat | ResourceIngredientTag.Plant, 17, 0.8f, 40, "research:cuisine:livestock", MealQualityTier.Fine, 55f, 7f, 450f),
            M("food:jerky", "육포", "오래 보관 가능한 육식 보존식.", ResourceIngredientTag.Meat, 12, 0.45f, 50, "research:survival:preservation", MealQualityTier.Preserved, 35f, -1f, 1440f, true),
            M("food:lavish-vegan", "월야 비건 만찬", "세 가지 식물 재료군을 쓴 비건 호화식.", ResourceIngredientTag.Plant | ResourceIngredientTag.Fungus, 24, 0.9f, 30, "research:cuisine:lavish", MealQualityTier.Lavish, 60f, 10f, 480f),
            M("food:lavish-meat", "핏빛 호화식", "고기와 유제품, 과일을 쓴 호화식.", ResourceIngredientTag.Meat | ResourceIngredientTag.Milk | ResourceIngredientTag.Plant, 27, 1f, 30, "research:cuisine:lavish", MealQualityTier.Lavish, 65f, 12f, 480f),
            M("food:preserved-ration", "보존 배급식", "전분과 소금으로 수명을 늘린 원정 식량.", ResourceIngredientTag.Plant, 13, 0.5f, 60, "research:cuisine:lavish", MealQualityTier.Preserved, 40f, 0f, 1800f, true),
            I("feed:hay", "건초 사료", "초식동물용 기본 사료.", StockCategory.Food, ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, 2, 0.45f, 75, "research:husbandry:feed"),
            I("feed:dog-food", "개밥", "곡물과 동물성 부산물을 섞은 육식·잡식 사료.", StockCategory.Food, ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant | ResourceIngredientTag.Meat, 3, 0.55f, 75, "research:husbandry:feed"),
            I("husbandry:bedding", "깔짚", "축사 위생과 휴식을 유지하는 바닥재.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, 3, 0.5f, 60, "research:husbandry:feed"),
            I("craft:soap", "비누", "위생 작업과 목욕에 쓰는 소모품.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Fat, 7, 0.25f, 50, "research:survival:sanitation"),
            I("craft:candle", "양초", "조명과 의식에 쓰는 연료성 완제품.", StockCategory.Fuel, ResourceItemKind.FinishedGood, ResourceIngredientTag.Fat | ResourceIngredientTag.Fuel, 6, 0.2f, 50, "research:authority:ritual"),
            I("craft:resin-balm", "수액 연고", "피부 손상과 방어구 마찰을 줄이는 연고.", StockCategory.Medicine, ResourceItemKind.Medicine, ResourceIngredientTag.Plant | ResourceIngredientTag.Fat, 10, 0.15f, 40, "research:pharmacology:antiseptic"),
            I("craft:bone-charm", "뼈뿔 장신구", "흥행과 권위 장식에 쓰는 거친 장신구.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, 18, 0.2f, 40, "research:authority:prestige"),
            I("resource:trail-charm", "길잡이 부적", "숨겨진 원정지의 위험과 약점을 해독하는 부적.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, 60, 0.1f, 30, "research:husbandry:capture"),
            I("equipment:slime-warming-pad", "보온 점액 패드", "슬라임 전용 초기 저온 작업복.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, 18, 0.3f, 1, "research:environment:cold-work"),
            I("equipment:cold-work-suit", "방한 작업복", "8°C 냉장실 상시 근무용 작업복.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, 42, 0.9f, 1, "research:environment:cold-work"),
            I("equipment:rune-cold-suit", "룬 방한복", "2°C 장기 근무를 지원하되 치명선을 바꾸지 않는 작업복.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber | ResourceIngredientTag.Arcane, 95, 0.8f, 1, "research:environment:rune-insulation"),
            I("craft:gold-ornament", "금 장식", "권위 시설과 계약 납품에 쓰는 고가 장식.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, 95, 0.4f, 20, "research:metallurgy:precious"),
            I("craft:stone-ornament", "석조 장식", "방의 미관과 대형 사업에 쓰는 석조물.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, 12, 1.6f, 30, "research:mining:stonecutting"),
            I("craft:ritual-reagent", "혈액 의식재", "피와 독소를 안정화한 금기 의식 재료.", StockCategory.Biological, ResourceItemKind.FinishedGood, ResourceIngredientTag.Blood | ResourceIngredientTag.Forbidden, 24, 0.25f, 30, "research:control:blood-show"),
            I("craft:fang-poison", "송곳니 독액", "관통 무기와 사냥에 바르는 독액.", StockCategory.Biological, ResourceItemKind.FinishedGood, ResourceIngredientTag.Forbidden, 20, 0.12f, 30, "research:arcane:alchemy"),
            Med("medicine:herbal-poultice", "약초 찜질약", "가벼운 부상에 쓰는 기본 약품.", ResourceIngredientTag.Plant, 8, 0.2f, 50, "research:pharmacology:herbalism", true, 0.72f, 2f, 0f, 4f),
            Med("medicine:antiseptic", "소독제", "감염 위험을 낮추는 외용 약품.", ResourceIngredientTag.Plant, 12, 0.18f, 50, "research:pharmacology:antiseptic", true, 0.82f, 16f, 0f, 2f),
            Med("medicine:standard", "표준 약품", "치료 효율과 회복 속도를 높이는 약품.", ResourceIngredientTag.Plant | ResourceIngredientTag.Fungus, 20, 0.16f, 40, "research:pharmacology:distillation", true, 1f, 8f, 2f, 8f),
            Med("medicine:advanced", "고급 약품", "마나와 월화를 안정화한 고급 치료제.", ResourceIngredientTag.Plant | ResourceIngredientTag.Arcane, 42, 0.14f, 30, "research:pharmacology:advanced", true, 1.35f, 14f, 8f, 12f),
            Med("medicine:antidote", "해독제", "독소와 과다 복용 증상을 완화한다.", ResourceIngredientTag.Plant | ResourceIngredientTag.Arcane, 28, 0.12f, 30, "research:pharmacology:advanced", false, 0.6f, 2f, 30f, 0f),
            Med("medicine:anesthetic", "마취제", "수술과 중상 치료의 고통을 낮춘다.", ResourceIngredientTag.Plant, 24, 0.12f, 30, "research:pharmacology:anesthesia", false, 0.75f, 0f, 0f, 35f),

            I("drug:moonflower-tea", "월화차", "의존성 없이 집중과 기분을 조금 높이는 차.", StockCategory.Medicine, ResourceItemKind.Substance, ResourceIngredientTag.Plant | ResourceIngredientTag.Arcane, 9, 0.25f, 40, "research:pharmacology:herbalism"),
            I("drug:vitality-tonic", "활력 강장제", "피로를 줄이는 비중독성 강장제.", StockCategory.Medicine, ResourceItemKind.Substance, ResourceIngredientTag.Plant, 16, 0.2f, 40, "research:pharmacology:distillation"),
            I("drug:dreamleaf-analgesic", "몽엽 진통제", "통증을 크게 낮추지만 의존 위험이 있다.", StockCategory.Medicine, ResourceItemKind.Substance, ResourceIngredientTag.Plant, 18, 0.12f, 30, "research:pharmacology:anesthesia"),
            I("drug:blood-stimulant", "혈화 촉진제", "전투력을 끌어올리지만 중독과 과다 복용 위험이 높다.", StockCategory.Medicine, ResourceItemKind.Substance, ResourceIngredientTag.Blood | ResourceIngredientTag.Forbidden, 28, 0.1f, 25, "research:pharmacology:stimulants"),
            I("drug:mana-awakener", "마나 각성제", "연구와 비전 감각을 증폭하는 중독성 약물.", StockCategory.Medicine, ResourceItemKind.Substance, ResourceIngredientTag.Arcane, 34, 0.1f, 25, "research:pharmacology:stimulants"),
            I("drug:night-wine", "밤포도주", "기분과 사교성을 높이는 유흥성 술.", StockCategory.Food, ResourceItemKind.Substance, ResourceIngredientTag.Plant, 14, 0.5f, 40, "research:cuisine:fermentation"),
            I("drug:hallucinogenic-distillate", "환각균 증류액", "강한 환각과 오락 효과를 주는 유흥 약물.", StockCategory.Medicine, ResourceItemKind.Substance, ResourceIngredientTag.Fungus, 22, 0.15f, 25, "research:pharmacology:distillation"),

            I("ammo:arrow-bone", "뼈촉 화살", "가볍지만 관통이 낮은 화살.", StockCategory.Ammunition, ResourceItemKind.Ammunition, ResourceIngredientTag.Wood | ResourceIngredientTag.Mineral, 2, 0.08f, 100, "research:defense:ranged-positions"),
            I("ammo:arrow-iron", "철촉 화살", "표준 피해와 관통을 가진 화살.", StockCategory.Ammunition, ResourceItemKind.Ammunition, ResourceIngredientTag.Wood | ResourceIngredientTag.Mineral, 3, 0.09f, 100, "research:metallurgy:iron"),
            I("ammo:arrow-steel", "강철촉 화살", "높은 관통을 가진 고급 화살.", StockCategory.Ammunition, ResourceItemKind.Ammunition, ResourceIngredientTag.Wood | ResourceIngredientTag.Mineral, 5, 0.085f, 100, "research:metallurgy:steel"),
            I("ammo:arrow-rune", "룬촉 화살", "마나와 뿔을 새긴 비전 화살.", StockCategory.Ammunition, ResourceItemKind.Ammunition, ResourceIngredientTag.Wood | ResourceIngredientTag.Arcane, 9, 0.08f, 75, "research:arcane:advanced"),
            I("ammo:bolt-bone", "뼈촉 볼트", "가벼운 연습용 석궁 볼트.", StockCategory.Ammunition, ResourceItemKind.Ammunition, ResourceIngredientTag.Wood | ResourceIngredientTag.Mineral, 2, 0.1f, 100, "research:defense:ranged-positions"),
            I("ammo:bolt-iron", "철촉 볼트", "표준 석궁 볼트.", StockCategory.Ammunition, ResourceItemKind.Ammunition, ResourceIngredientTag.Wood | ResourceIngredientTag.Mineral, 4, 0.11f, 100, "research:metallurgy:iron"),
            I("ammo:bolt-steel", "강철촉 볼트", "중장갑을 겨냥한 고관통 볼트.", StockCategory.Ammunition, ResourceItemKind.Ammunition, ResourceIngredientTag.Wood | ResourceIngredientTag.Mineral, 6, 0.105f, 100, "research:metallurgy:steel"),
            I("ammo:bolt-rune", "룬촉 볼트", "비전 저항을 꿰뚫는 룬 볼트.", StockCategory.Ammunition, ResourceItemKind.Ammunition, ResourceIngredientTag.Wood | ResourceIngredientTag.Arcane, 10, 0.1f, 75, "research:arcane:advanced"),
            I("offense:unappraised-loot", "미감정 전리품", "원정에서 회수한 봉인 상자와 귀중품. 전리품거치대에서 감정해야 판매할 수 있다.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.None, 0, 0.05f, 100, string.Empty, 0f),
            I("offense:appraised-valuables", "감정된 귀중품", "출처와 가치를 확인한 원정 귀중품. 판매 정책으로 금고 자금화할 수 있다.", StockCategory.General, ResourceItemKind.FinishedGood, ResourceIngredientTag.None, 1, 0.05f, 100, string.Empty, 1f)
        };

        return specs.Select((spec, index) =>
        {
            ResourceItemDefinitionSO asset = GetOrCreate<ResourceItemDefinitionSO>(
                $"{ItemRoot}/{Sanitize(spec.Id)}.asset");
            asset.id = 8000 + index;
            asset.Configure(
                spec.Id,
                spec.Name,
                spec.Description,
                spec.Category,
                spec.Kind,
                spec.Tags,
                spec.Price,
                spec.Weight,
                spec.MaxStack,
                spec.ResearchId);
            (float fuelValue, float feedValue, bool feedEligible) = spec.Id switch
            {
                "resource:log" => (10f, 0f, false),
                "material:low-fuel" => (6f, 0f, false),
                "resource:coal" => (20f, 0f, false),
                "material:charcoal" => (24f, 0f, false),
                "feed:hay" => (0f, 8f, true),
                "feed:dog-food" => (0f, 14f, true),
                "resource:twilight-grain" => (0f, 6f, true),
                "resource:ember-root" => (0f, 6f, true),
                "resource:cave-mushroom" => (0f, 5f, true),
                "resource:meat" => (0f, 12f, true),
                "craft:candle" => (2f, 0f, false),
                _ => (0f, 0f, false)
            };
            asset.ConfigureFacilitySupply(
                fuelValue,
                feedValue,
                feedEligible,
                spec.Kind == ResourceItemKind.Intermediate);
            asset.ConfigureMarketSaleRate(spec.MarketSaleRate);
            if (spec.HasMealData)
            {
                asset.ConfigureMeal(
                    spec.MealQuality,
                    spec.Nutrition,
                    spec.MealMood,
                    spec.FreshnessSeconds,
                    spec.Preserved,
                    ResolveMealQualityBand(spec.Id),
                    ResolveMealServingRole(spec.Id));
            }
            if (spec.HasMedicineData)
            {
                asset.ConfigureMedicine(
                    spec.SupportsInjuryTreatment,
                    spec.TreatmentPotency,
                    spec.InfectionReduction,
                    spec.DetoxReduction,
                    spec.PainReduction);
            }
            ConfigureSubstanceFeature(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }).ToArray();
    }

    private static ProductionRecipeSO[] BuildRecipes()
    {
        List<RecipeSpec> specs = new List<RecipeSpec>
        {
            SourceWork("source:logging", "벌목 수확", "exterior-tree", "work:logging", "research:forestry:logging", O("resource:log", 5), O("resource:dark-resin", 1, 0.18f)),
            SourceWork("source:quarry", "심부 채석", "quarry", "work:quarry", "research:mining:quarry", O("resource:stone", 4), O("resource:coal", 1, 0.20f), O("resource:iron-ore", 1, 0.16f), O("resource:gold-ore", 1, 0.03f), O("resource:mana-crystal", 1, 0.01f)),
            SourceWork("source:saltstone", "노천 채석", "exterior-rock", "work:quarry", "research:mining:surface", O("resource:stone", 3), O("resource:saltstone", 1, 0.25f)),
            SourceWork("source:grass", "풀과 짚 채집", "exterior-grass", "work:gather", "research:agriculture:gathering", O("resource:grass-straw", 4)),
            Source("source:animal-meat", "도축 고기", "butcher", "research:husbandry:capture", O("resource:meat", 4)),
            Source("source:animal-blood", "도축 혈액", "butcher", "research:husbandry:capture", O("resource:blood", 2)),
            Source("source:animal-fat", "도축 지방", "butcher", "research:husbandry:capture", O("resource:fat", 2)),
            Source("source:animal-hide", "도축 가죽", "butcher", "research:husbandry:capture", O("resource:hide", 2)),
            Source("source:animal-wool", "가축 털깎기", "animal-pen", "research:husbandry:selective", O("resource:wool", 3)),
            Source("source:animal-feather", "가축 깃털", "animal-pen", "research:husbandry:selective", O("resource:feather", 4)),
            Source("source:animal-milk", "가축 젖짜기", "animal-pen", "research:husbandry:selective", O("resource:milk", 3)),
            Source("source:animal-egg", "가축 알 수거", "animal-pen", "research:husbandry:selective", O("resource:egg", 3)),
            Source("source:animal-bone", "도축 뼈", "butcher", "research:husbandry:capture", O("resource:bone", 2)),
            Source("source:animal-horn", "가축 뿔 수확", "animal-pen", "research:husbandry:selective", O("resource:horn", 1)),
            Source("source:animal-fang", "도축 송곳니", "butcher", "research:husbandry:capture", O("resource:fang", 1)),
            Source("source:animal-manure", "축사 분뇨", "animal-pen", "research:husbandry:feed", O("resource:manure", 2)),
            Source("source:spoilage-plant", "식물식 부패", "spoilage", "research:agriculture:compost", O("waste:plant-rot", 1)),
            Source("source:spoilage-animal", "동물식 부패", "spoilage", "research:agriculture:compost", O("waste:animal-rot", 1)),
            Source("source:spoilage-mixed", "혼합식 부패", "spoilage", "research:agriculture:compost", O("waste:mixed-rot", 1)),
            Source("source:spoilage-forbidden", "금기식 부패", "spoilage", "research:control:blood-show", O("waste:forbidden-rot", 1)),

            R("recipe:milling-flour", "황혼곡 제분", "mill", "work:craft", "research:cuisine:milling", 8, A("resource:twilight-grain", 3), O("material:flour", 2)),
            R("recipe:starch", "전분 추출", "mill", "work:craft", "research:cuisine:milling", 7, A("resource:twilight-grain", 2), O("material:starch", 2)),
            R("recipe:syrup", "밤포도 시럽", "brewery", "work:cook", "research:cuisine:fermentation", 10, A("resource:night-grape", 3), O("material:syrup", 2)),
            R("recipe:alcohol", "기초 알코올", "brewery", "work:cook", "research:cuisine:fermentation", 12, A("resource:twilight-grain", 2), A("resource:night-grape", 1), O("material:alcohol", 2)),
            R("recipe:sawmill-lumber", "목재 제재", "sawmill", "work:craft", "research:forestry:sawmill", 9, A("resource:log", 2), O("material:lumber", 3)),
            R("recipe:charcoal", "숯 굽기", "charcoal-kiln", "work:craft", "research:forestry:charcoal", 12, A("resource:log", 3), O("material:charcoal", 2)),
            R("recipe:stone-block", "석재 블록 절단", "stonecutter", "work:craft", "research:mining:stonecutting", 10, A("resource:stone", 3), O("material:stone-block", 2)),
            R("recipe:iron-ingot", "철 제련", "furnace", "work:craft", "research:metallurgy:iron", 14, A("resource:iron-ore", 2), A("resource:coal", 1), O("material:iron-ingot", 1)),
            R("recipe:iron-slag-block", "철광 슬래그 블록", "furnace", "work:craft", "research:metallurgy:iron", 9, A("resource:iron-ore", 2), O("material:stone-block", 1)),
            R("recipe:steel-ingot", "제강", "steelworks", "work:craft", "research:metallurgy:steel", 20, A("material:iron-ingot", 2), A("material:charcoal", 2), O("material:steel-ingot", 1)),
            R("recipe:gold-ingot", "금 제련", "furnace", "work:craft", "research:metallurgy:precious", 18, A("resource:gold-ore", 2), A("resource:coal", 1), O("material:gold-ingot", 1)),
            R("recipe:gold-leaf", "금박 세공", "jeweler", "work:craft", "research:metallurgy:precious", 14, A("resource:gold-ore", 1), A("resource:dark-resin", 1), O("craft:gold-ornament", 1)),
            R("recipe:blacksteel", "흑강 제련", "arcane-forge", "work:craft", "research:metallurgy:blacksteel", 32, A("material:steel-ingot", 2), A("resource:mana-crystal", 2), A("material:charcoal", 1), O("material:blacksteel-ingot", 1)),
            R("recipe:cloth", "그늘섬유 직조", "loom", "work:craft", "research:textile:fiber", 9, A("resource:shade-fiber", 3), O("material:cloth", 2)),
            R("recipe:wool-cloth", "모직 직조", "loom", "work:craft", "research:textile:fiber", 10, A("resource:wool", 3), O("material:cloth", 2)),
            R("recipe:leather", "가죽 무두질", "tannery", "work:craft", "research:textile:tanning", 12, A("resource:hide", 2), A("resource:saltstone", 1), O("material:leather", 2)),
            R("recipe:rune-leather", "룬가죽 각인", "alchemy", "work:craft", "research:textile:rune-leather", 22, A("material:leather", 2), A("resource:mana-crystal", 1), A("resource:dark-resin", 1), O("material:rune-leather", 1)),
            R("recipe:dreamweave", "몽직물 직조", "arcane-loom", "work:craft", "research:textile:dreamweave", 24, A("material:cloth", 2), A("resource:dreamleaf", 2), A("resource:mana-crystal", 1), O("material:dreamweave", 1)),
            R("recipe:compost-plant", "식물성 퇴비", "composter", "work:craft", "research:agriculture:compost", 8, A("waste:plant-rot", 2), O("material:compost", 1)),
            R("recipe:compost-animal", "동물성 퇴비", "composter", "work:craft", "research:agriculture:compost", 10, A("waste:animal-rot", 2), A("resource:bone", 1), O("material:compost", 1)),
            R("recipe:compost-mixed", "혼합 퇴비", "composter", "work:craft", "research:agriculture:compost", 11, A("waste:mixed-rot", 2), A("resource:grass-straw", 1), O("material:compost", 1)),
            R("recipe:compost-manure", "분뇨 퇴비", "composter", "work:craft", "research:agriculture:compost", 7, A("resource:manure", 2), A("resource:grass-straw", 1), O("material:compost", 1)),
            R("recipe:solvent", "연금 용매", "distillery", "work:craft", "research:pharmacology:distillation", 14, A("resource:dark-resin", 2), A("resource:coal", 1), A("resource:cave-mushroom", 1), O("material:alchemical-solvent", 1)),
            R("recipe:tallow", "지방 정제", "cookbench", "work:cook", "research:cuisine:livestock", 8, A("resource:fat", 2), O("material:tallow", 1)),
            R("recipe:bowstring-fiber", "섬유 활시위", "loom", "work:craft", "research:textile:fiber", 8, A("resource:shade-fiber", 2), O("material:bowstring", 1)),
            R("recipe:bowstring-sinew", "뿔 보강 활시위", "loom", "work:craft", "research:husbandry:selective", 10, A("resource:horn", 1), A("resource:hide", 1), O("material:bowstring", 1)),
            R("recipe:treated-lumber", "목재 처리", "workstation:v3:treated-lumber", "work:craft", "research:forestry:treated", 15, A("material:lumber", 2), A("resource:dark-resin", 1), O("material:treated-lumber", 2)),
            R("recipe:cheese", "치즈 숙성", "cookbench", "work:cook", "research:cuisine:livestock", 12, A("resource:milk", 3), A("resource:saltstone", 1), O("material:cheese", 2)),
            R("recipe:low-fuel-rot", "부패 연료", "composter", "work:craft", "research:agriculture:compost", 10, A("waste:mixed-rot", 4), A("resource:grass-straw", 1), O("material:low-fuel", 1)),
            R("recipe:low-fuel-plant", "식물성 부패 연료", "composter", "work:craft", "research:agriculture:compost", 9, A("waste:plant-rot", 4), A("resource:grass-straw", 1), O("material:low-fuel", 1)),
            R("recipe:low-fuel-animal", "동물성 부패 연료", "composter", "work:craft", "research:agriculture:compost", 11, A("waste:animal-rot", 4), A("resource:grass-straw", 1), O("material:low-fuel", 1)),
            R("recipe:low-fuel-manure", "분뇨 연료", "composter", "work:craft", "research:husbandry:feed", 9, A("resource:manure", 3), A("resource:grass-straw", 1), O("material:low-fuel", 1)),
            R("recipe:rot-toxin", "부패 독소", "alchemy", "work:craft", "research:arcane:alchemy", 16, A("waste:forbidden-rot", 1), A("resource:bloodleaf", 1), O("material:rot-toxin", 1)),
            R("recipe:incinerate-plant", "식물성 부패물 소각", "incinerator", "work:craft", "research:survival:sanitation", 5, A("waste:plant-rot", 1)),
            R("recipe:incinerate-animal", "동물성 부패물 소각", "incinerator", "work:craft", "research:survival:sanitation", 6, A("waste:animal-rot", 1)),
            R("recipe:incinerate-mixed", "혼합 부패물 소각", "incinerator", "work:craft", "research:survival:sanitation", 7, A("waste:mixed-rot", 1)),
            R("recipe:incinerate-forbidden", "금기 부패물 소각", "incinerator", "work:craft", "research:survival:sanitation", 9, A("waste:forbidden-rot", 1)),

            R("recipe:grain-porridge", "황혼곡죽", "cookbench", "work:cook", "research:cuisine:crops", 6, A("resource:twilight-grain", 2), O("food:grain-porridge", 2)),
            R("recipe:root-stew", "잿불뿌리 스튜", "cookbench", "work:cook", "research:cuisine:crops", 7, A("resource:ember-root", 2), O("food:root-stew", 2)),
            R("recipe:mushroom-soup", "동굴버섯국", "cookbench", "work:cook", "research:cuisine:crops", 7, A("resource:cave-mushroom", 2), O("food:mushroom-soup", 2)),
            R("recipe:garden-meal", "정원 요리", "cookbench", "work:cook", "research:cuisine:vegan", 11, A("resource:twilight-grain", 1), A("resource:ember-root", 1), A("resource:cave-mushroom", 1), O("food:garden-meal", 2)),
            R("recipe:egg-pancake", "달걀전", "cookbench", "work:cook", "research:cuisine:livestock", 10, A("resource:egg", 2), A("material:flour", 1), A("resource:milk", 1), O("food:egg-pancake", 2)),
            R("recipe:cheese-mushroom", "치즈버섯찜", "cookbench", "work:cook", "research:cuisine:livestock", 11, A("material:cheese", 1), A("resource:cave-mushroom", 2), O("food:cheese-mushroom", 2)),
            R("recipe:roasted-meat", "고기구이", "cookbench", "work:cook", "research:cuisine:livestock", 8, A("resource:meat", 2), O("food:roasted-meat", 2)),
            R("recipe:boar-stew", "멧돼지 스튜", "cookbench", "work:cook", "research:cuisine:livestock", 12, A("resource:meat", 2), A("resource:ember-root", 1), A("resource:fat", 1), O("food:boar-stew", 2)),
            R("recipe:meat-pie", "고기 파이", "cookbench", "work:cook", "research:cuisine:livestock", 13, A("resource:meat", 2), A("material:flour", 2), A("resource:egg", 1), O("food:meat-pie", 2)),
            R("recipe:jerky", "육포", "smoker", "work:cook", "research:survival:preservation", 12, A("resource:meat", 3), A("resource:saltstone", 1), O("food:jerky", 2)),
            R("recipe:lavish-vegan", "월야 비건 만찬", "cookbench", "work:cook", "research:cuisine:lavish", 18, A("material:flour", 2), A("material:syrup", 1), A("resource:cave-mushroom", 2), A("resource:ember-root", 1), O("food:lavish-vegan", 2)),
            R("recipe:lavish-meat", "핏빛 호화식", "cookbench", "work:cook", "research:cuisine:lavish", 20, A("resource:meat", 2), A("material:cheese", 1), A("resource:night-grape", 2), O("food:lavish-meat", 2)),
            R("recipe:preserved-ration", "보존 배급식", "smoker", "work:cook", "research:cuisine:lavish", 14, A("resource:ember-root", 2), A("material:starch", 1), A("resource:saltstone", 1), O("food:preserved-ration", 3)),
            R("recipe:hay-feed", "건초 사료", "feedbench", "work:craft", "research:husbandry:feed", 6, A("resource:grass-straw", 3), A("resource:twilight-grain", 1), O("feed:hay", 3)),
            R("recipe:dog-food", "개밥", "feedbench", "work:cook", "research:husbandry:feed", 10, A("waste:animal-rot", 1), A("resource:twilight-grain", 1), O("feed:dog-food", 2)),
            R("recipe:dog-food-fresh", "신선 개밥", "feedbench", "work:cook", "research:husbandry:feed", 9, A("resource:meat", 1), A("resource:twilight-grain", 1), O("feed:dog-food", 2)),
            R("recipe:bedding-straw", "짚 깔짚", "loom", "work:craft", "research:husbandry:feed", 6, A("resource:grass-straw", 2), A("resource:shade-fiber", 1), O("husbandry:bedding", 2)),
            R("recipe:bedding-animal", "털 깔짚", "loom", "work:craft", "research:husbandry:feed", 8, A("resource:wool", 1), A("resource:feather", 2), A("resource:hide", 1), O("husbandry:bedding", 2)),
            R("recipe:soap", "비누", "cookbench", "work:craft", "research:survival:sanitation", 9, A("material:tallow", 1), A("resource:dark-resin", 1), O("craft:soap", 2)),
            R("recipe:candle", "양초", "cookbench", "work:craft", "research:authority:ritual", 7, A("material:tallow", 1), A("resource:shade-fiber", 1), O("craft:candle", 2)),
            R("recipe:resin-balm", "수액 연고", "apothecary", "work:craft", "research:pharmacology:antiseptic", 10, A("resource:dark-resin", 1), A("material:tallow", 1), A("resource:moonflower", 1), O("craft:resin-balm", 1)),
            R("recipe:bone-charm", "뼈뿔 장신구", "jeweler", "work:craft", "research:authority:prestige", 12, A("resource:bone", 2), A("resource:horn", 1), A("resource:fang", 1), O("craft:bone-charm", 1)),
            R("recipe:trail-charm", "길잡이 부적", "jeweler", "work:craft", "research:husbandry:capture", 16, A("resource:rune-dust", 1), A("resource:fang", 1), O("resource:trail-charm", 1)),
            R("recipe:slime-warming-pad", "보온 점액 패드", "loom", "work:craft", "research:environment:cold-work", 10, A("material:cloth", 1), A("resource:dark-resin", 1), O("equipment:slime-warming-pad", 1)),
            R("recipe:cold-work-suit", "방한 작업복", "loom", "work:craft", "research:environment:cold-work", 18, A("material:cloth", 2), A("material:leather", 2), O("equipment:cold-work-suit", 1)),
            R("recipe:rune-cold-suit", "룬 방한복", "loom", "work:craft", "research:environment:rune-insulation", 28, A("material:rune-leather", 2), A("resource:mana-crystal", 1), A("resource:rune-dust", 1), O("equipment:rune-cold-suit", 1)),
            R("recipe:gold-ornament", "금 장식", "jeweler", "work:craft", "research:metallurgy:precious", 18, A("material:gold-ingot", 1), A("resource:mana-crystal", 1), O("craft:gold-ornament", 1)),
            R("recipe:stone-ornament", "석조 장식", "stonecutter", "work:craft", "research:mining:stonecutting", 12, A("material:stone-block", 2), A("resource:stone", 1), O("craft:stone-ornament", 1)),
            R("recipe:ritual-reagent", "혈액 의식재", "alchemy", "work:craft", "research:control:blood-show", 16, A("resource:blood", 2), A("material:rot-toxin", 1), O("craft:ritual-reagent", 1)),
            R("recipe:fang-poison", "송곳니 독액", "alchemy", "work:craft", "research:arcane:alchemy", 14, A("resource:fang", 2), A("material:alchemical-solvent", 1), O("craft:fang-poison", 1)),

            R("recipe:herbal-poultice", "약초 찜질약", "apothecary", "work:craft", "research:pharmacology:herbalism", 7, A("resource:moonflower", 1), A("resource:shade-fiber", 1), O("medicine:herbal-poultice", 2)),
            R("recipe:antiseptic", "소독제", "apothecary", "work:craft", "research:pharmacology:antiseptic", 9, A("resource:bloodleaf", 1), A("resource:saltstone", 1), O("medicine:antiseptic", 2)),
            R("recipe:standard-medicine", "표준 약품", "apothecary", "work:craft", "research:pharmacology:distillation", 14, A("resource:cave-mushroom", 1), A("resource:moonflower", 1), A("material:alchemical-solvent", 1), O("medicine:standard", 2)),
            R("recipe:advanced-medicine", "고급 약품", "alchemy", "work:craft", "research:pharmacology:advanced", 22, A("medicine:standard", 1), A("resource:mana-crystal", 1), A("resource:moonflower", 2), A("resource:milk", 1), O("medicine:advanced", 1)),
            R("recipe:antidote", "해독제", "alchemy", "work:craft", "research:pharmacology:advanced", 18, A("resource:bloodleaf", 1), A("resource:cave-mushroom", 1), A("material:alchemical-solvent", 1), O("medicine:antidote", 1)),
            R("recipe:anesthetic", "마취제", "apothecary", "work:craft", "research:pharmacology:anesthesia", 16, A("resource:dreamleaf", 2), A("material:alcohol", 1), O("medicine:anesthetic", 1)),

            R("recipe:moonflower-tea", "월화차", "cookbench", "work:cook", "research:pharmacology:herbalism", 5, A("resource:moonflower", 1), O("drug:moonflower-tea", 2)),
            R("recipe:vitality-tonic", "활력 강장제", "apothecary", "work:craft", "research:pharmacology:distillation", 11, A("resource:ember-root", 1), A("resource:bloodleaf", 1), A("material:syrup", 1), O("drug:vitality-tonic", 2)),
            R("recipe:dreamleaf-analgesic", "몽엽 진통제", "apothecary", "work:craft", "research:pharmacology:anesthesia", 12, A("resource:dreamleaf", 2), A("material:alchemical-solvent", 1), O("drug:dreamleaf-analgesic", 1)),
            R("recipe:blood-stimulant", "혈화 촉진제", "alchemy", "work:craft", "research:pharmacology:stimulants", 16, A("resource:bloodleaf", 2), A("resource:blood", 1), A("material:alchemical-solvent", 1), O("drug:blood-stimulant", 1)),
            R("recipe:mana-awakener", "마나 각성제", "alchemy", "work:craft", "research:pharmacology:stimulants", 18, A("resource:moonflower", 1), A("resource:mana-crystal", 1), A("material:alchemical-solvent", 1), O("drug:mana-awakener", 1)),
            R("recipe:night-wine", "밤포도주", "brewery", "work:cook", "research:cuisine:fermentation", 12, A("resource:night-grape", 3), A("material:alcohol", 1), O("drug:night-wine", 2)),
            R("recipe:hallucinogenic-distillate", "환각균 증류액", "distillery", "work:craft", "research:pharmacology:distillation", 15, A("resource:cave-mushroom", 2), A("material:alcohol", 1), O("drug:hallucinogenic-distillate", 1)),

            R("recipe:arrow-bone", "뼈촉 화살", "forge", "work:craft", "research:defense:ranged-positions", 8, A("material:lumber", 1), A("resource:bone", 1), A("resource:feather", 1), O("ammo:arrow-bone", 10)),
            R("recipe:arrow-iron", "철촉 화살", "forge", "work:craft", "research:metallurgy:iron", 10, A("material:lumber", 1), A("material:iron-ingot", 1), A("resource:feather", 1), O("ammo:arrow-iron", 10)),
            R("recipe:arrow-steel", "강철촉 화살", "forge", "work:craft", "research:metallurgy:steel", 12, A("material:lumber", 1), A("material:steel-ingot", 1), A("resource:feather", 1), O("ammo:arrow-steel", 10)),
            R("recipe:arrow-rune", "룬촉 화살", "arcane-forge", "work:craft", "research:arcane:advanced", 16, A("material:treated-lumber", 1), A("resource:horn", 1), A("resource:mana-crystal", 1), O("ammo:arrow-rune", 8)),
            R("recipe:bolt-bone", "뼈촉 볼트", "forge", "work:craft", "research:defense:ranged-positions", 8, A("material:lumber", 1), A("resource:bone", 1), A("resource:fang", 1), O("ammo:bolt-bone", 10)),
            R("recipe:bolt-iron", "철촉 볼트", "forge", "work:craft", "research:metallurgy:iron", 10, A("material:lumber", 1), A("material:iron-ingot", 1), A("resource:fang", 1), O("ammo:bolt-iron", 10)),
            R("recipe:bolt-steel", "강철촉 볼트", "forge", "work:craft", "research:metallurgy:steel", 12, A("material:lumber", 1), A("material:steel-ingot", 1), A("resource:fang", 1), O("ammo:bolt-steel", 10)),
            R("recipe:bolt-rune", "룬촉 볼트", "arcane-forge", "work:craft", "research:arcane:advanced", 16, A("material:treated-lumber", 1), A("resource:horn", 1), A("resource:mana-crystal", 1), O("ammo:bolt-rune", 8)),

            R("recipe:loot-appraisal", "원정 전리품 감정", "workstation:v3:appraisal", "work:craft", "research:equipment:relic-appraisal", 8, A("offense:unappraised-loot", 10), O("offense:appraised-valuables", 10))
        };

        foreach (string staleSinkPath in AssetDatabase.FindAssets(
                     "t:ProductionRecipeSO", new[] { RecipeRoot })
                 .Select(AssetDatabase.GUIDToAssetPath)
                 .Where(path => AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(path)
                     ?.RecipeId.StartsWith("sink:", StringComparison.Ordinal) == true))
        {
            AssetDatabase.DeleteAsset(staleSinkPath);
        }

        return specs.Select((spec, index) =>
        {
            ProductionRecipeSO asset = GetOrCreate<ProductionRecipeSO>(
                $"{RecipeRoot}/{Sanitize(spec.Id)}.asset");
            asset.id = 9000 + index;
            asset.Configure(
                spec.Id,
                spec.Name,
                spec.Description,
                spec.FacilityTag,
                spec.WorkTypeId,
                spec.ResearchId,
                spec.RequiredWork,
                spec.Inputs,
                spec.Outputs);
            asset.ConfigureFlowRole(spec.FlowRole);
            if (spec.FacilityTag.StartsWith(
                    "workstation:",
                    StringComparison.Ordinal))
            {
                asset.ConfigureWorkshop(
                    spec.FacilityTag,
                    Array.Empty<string>(),
                    ProductionProcessKind.WorkOnly);
            }
            asset.ConfigureProcessClass(spec.ProcessClass);
            asset.ConfigureBalanceWork(
                V23BalanceWorkCalculator.CalculateRecipeBaseWork(
                    asset,
                    spec.ProcessClass));
            EditorUtility.SetDirty(asset);
            return asset;
        }).ToArray();
    }

    private static ProductionFlowRole ResolveFlowRole(
        IReadOnlyCollection<ItemAmountDefinition> inputs,
        IReadOnlyCollection<ProductionOutputDefinition> outputs)
    {
        bool hasInputs = inputs != null && inputs.Count > 0;
        bool hasOutputs = outputs != null && outputs.Count > 0;
        if (!hasInputs && hasOutputs)
            return ProductionFlowRole.Source;
        if (hasInputs && !hasOutputs)
            return ProductionFlowRole.Sink;
        return ProductionFlowRole.Transform;
    }

    private static CropDefinitionSO[] BuildCrops()
    {
        CropSpec[] specs =
        {
            C("crop:twilight-grain", "황혼곡", "resource:twilight-grain", "research:agriculture:field", 36, 3, 6, 0.35f, 6, true, 4, 30),
            C("crop:ember-root", "잿불뿌리", "resource:ember-root", "research:agriculture:field", 42, 4, 7, 0.25f, 5, true, 2, 28),
            C("crop:night-grape", "밤포도", "resource:night-grape", "research:agriculture:irrigation", 54, 5, 8, 0.5f, 5, true, 8, 32),
            C("crop:cave-mushroom", "동굴버섯", "resource:cave-mushroom", "research:agriculture:gathering", 28, 3, 5, 0.2f, 5, true, 3, 26),
            C("crop:bloodleaf", "혈엽", "resource:bloodleaf", "research:pharmacology:herbalism", 46, 4, 7, 0.35f, 4, true, 8, 30),
            C("crop:moonflower", "월화", "resource:moonflower", "research:pharmacology:herbalism", 60, 5, 9, 0.4f, 3, true, 5, 24),
            C("crop:dreamleaf", "몽엽", "resource:dreamleaf", "research:pharmacology:anesthesia", 52, 5, 8, 0.3f, 4, true, 6, 26),
            C("crop:shade-fiber", "그늘섬유", "resource:shade-fiber", "research:textile:fiber", 40, 4, 7, 0.3f, 6, true, 5, 30)
        };

        return specs.Select((spec, index) =>
        {
            CropDefinitionSO asset = GetOrCreate<CropDefinitionSO>(
                $"{CropRoot}/{Sanitize(spec.Id)}.asset");
            asset.id = 10000 + index;
            asset.Configure(
                spec.Id,
                spec.Name,
                spec.ItemId,
                spec.ResearchId,
                spec.GrowthHours,
                spec.SowWork,
                spec.HarvestWork,
                spec.Water,
                spec.Yield,
                spec.Indoor,
                new Vector2(spec.MinTemperature, spec.MaxTemperature));
            EditorUtility.SetDirty(asset);
            return asset;
        }).ToArray();
    }

    private static CraftMaterialDefinitionSO[] BuildMaterials()
    {
        MaterialSpec[] specs =
        {
            M("material:wood", "material:lumber", "목재", CombatMaterialFamily.Wood, 0.85f, 0.55f, 0.65f, 0.55f, 0.55f, 0.2f, 0, 0, new Color(0.46f, 0.28f, 0.14f), false, "research:forestry:sawmill"),
            M("material:stone", "material:stone-block", "석재", CombatMaterialFamily.Stone, 0.95f, 0.75f, 0.85f, 1.35f, 0.50f, 0.1f, 0, 0, new Color(0.45f, 0.46f, 0.50f), false, "research:mining:stonecutting"),
            M("material:bone", "resource:bone", "뼈·뿔", CombatMaterialFamily.Bone, 0.95f, 0.80f, 0.75f, 0.65f, 0.90f, 0.1f, 0, 0.05f, new Color(0.78f, 0.73f, 0.58f), false, "research:metallurgy:primitive"),
            M("material:iron", "material:iron-ingot", "철", CombatMaterialFamily.Metal, 1f, 1f, 1f, 1f, 1f, 0.05f, 0, 0, new Color(0.40f, 0.42f, 0.48f), false, "research:metallurgy:iron"),
            M("material:steel", "material:steel-ingot", "강철", CombatMaterialFamily.Metal, 1.10f, 1.18f, 1.25f, 0.95f, 1.50f, 0.05f, 0, 0.05f, new Color(0.60f, 0.65f, 0.72f), false, "research:metallurgy:steel"),
            M("material:gold", "material:gold-ingot", "금", CombatMaterialFamily.Metal, 0.92f, 0.70f, 0.55f, 1.55f, 4f, 0, 0.05f, 0.05f, new Color(0.93f, 0.72f, 0.18f), true, "research:metallurgy:precious"),
            M("material:blacksteel", "material:blacksteel-ingot", "흑강", CombatMaterialFamily.Metal, 1.20f, 1.30f, 1.50f, 1.10f, 3f, 0.1f, 0.15f, 0.3f, new Color(0.18f, 0.13f, 0.28f), true, "research:metallurgy:blacksteel"),
            M("material:cloth", "material:cloth", "천", CombatMaterialFamily.Textile, 0.65f, 0.45f, 0.55f, 0.40f, 0.65f, 0.15f, 0.05f, 0, new Color(0.62f, 0.58f, 0.55f), false, "research:textile:fiber"),
            M("material:wool", "resource:wool", "털", CombatMaterialFamily.Textile, 0.70f, 0.50f, 0.70f, 0.55f, 0.85f, 0.45f, 0.05f, 0, new Color(0.72f, 0.69f, 0.62f), false, "research:husbandry:selective"),
            M("material:leather", "material:leather", "가죽", CombatMaterialFamily.Leather, 0.85f, 0.78f, 0.90f, 0.75f, 1.05f, 0.25f, 0.05f, 0.05f, new Color(0.38f, 0.25f, 0.18f), false, "research:textile:tanning"),
            M("material:rune-leather", "material:rune-leather", "룬가죽", CombatMaterialFamily.Leather, 1.02f, 1.16f, 1.25f, 0.72f, 2.4f, 0.30f, 0.10f, 0.35f, new Color(0.24f, 0.56f, 0.62f), true, "research:textile:rune-leather"),
            M("material:dreamweave", "material:dreamweave", "몽직물", CombatMaterialFamily.Textile, 0.90f, 0.82f, 0.90f, 0.28f, 2.7f, 0.2f, 0.5f, 0.15f, new Color(0.56f, 0.36f, 0.72f), true, "research:textile:dreamweave")
        };

        return specs.Select((spec, index) =>
        {
            CraftMaterialDefinitionSO asset = GetOrCreate<CraftMaterialDefinitionSO>(
                $"{MaterialRoot}/{Sanitize(spec.Id)}.asset");
            asset.id = 11000 + index;
            asset.Configure(
                spec.Id,
                spec.ItemId,
                spec.Name,
                spec.Family,
                new Vector4(spec.Damage, spec.Penetration, spec.Durability, spec.Weight),
                spec.Value,
                new Vector3(spec.Insulation, spec.MentalResistance, spec.ArcaneResistance),
                spec.Tint,
                spec.Rare,
                spec.ResearchId);
            EditorUtility.SetDirty(asset);
            return asset;
        }).ToArray();
    }

    private static void ConfigureSubstanceFeature(ResourceItemDefinitionSO item)
    {
        switch (item.ItemId)
        {
            case "drug:moonflower-tea":
                item.ConfigureSubstance("substance:moonflower-tea", SubstanceUseClass.NonAddictive,
                    0f, 0.002f, 0f, 0f, 3f, 0.04f, 0f, 180f);
                break;
            case "drug:vitality-tonic":
                item.ConfigureSubstance("substance:vitality-tonic", SubstanceUseClass.NonAddictive,
                    0f, 0.006f, 0f, 0f, 2f, 0.12f, 0f, 150f);
                break;
            case "drug:dreamleaf-analgesic":
                item.ConfigureSubstance("substance:dreamleaf-analgesic", SubstanceUseClass.Addictive,
                    0.08f, 0.02f, 0.12f, 0.02f, 4f, 0.05f, -0.03f, 240f);
                break;
            case "drug:blood-stimulant":
                item.ConfigureSubstance("substance:blood-stimulant", SubstanceUseClass.Addictive,
                    0.14f, 0.06f, 0.18f, 0.03f, 1f, 0.16f, 0.20f, 150f);
                break;
            case "drug:mana-awakener":
                item.ConfigureSubstance("substance:mana-awakener", SubstanceUseClass.Addictive,
                    0.11f, 0.05f, 0.16f, 0.025f, 2f, 0.18f, 0.08f, 180f);
                break;
            case "drug:night-wine":
                item.ConfigureSubstance("substance:night-wine", SubstanceUseClass.Recreational,
                    0.04f, 0.025f, 0.08f, 0.015f, 7f, -0.04f, -0.04f, 240f);
                break;
            case "drug:hallucinogenic-distillate":
                item.ConfigureSubstance("substance:hallucinogenic", SubstanceUseClass.Recreational,
                    0.09f, 0.04f, 0.13f, 0.025f, 10f, -0.12f, -0.08f, 210f);
                break;
            default:
                item.ClearSubstance();
                break;
        }
    }

    private static void ValidateNoLegacySubstanceAssets()
    {
        string[] legacyAssets = AssetDatabase.IsValidFolder(LegacySubstanceRoot)
            ? AssetDatabase.FindAssets("t:ScriptableObject", new[] { LegacySubstanceRoot })
            : Array.Empty<string>();
        if (legacyAssets.Length > 0)
        {
            throw new InvalidOperationException(
                "Legacy SubstanceDefinitionSO assets are forbidden. "
                + "Substance content must be authored only as SubstanceItemFeature on item definitions.");
        }
    }

    private static IReadOnlyList<string> ValidateContentGraph(
        IResourceEconomyContentCatalog catalog)
    {
        NullWorldItemStackRuntime nullRuntime = new NullWorldItemStackRuntime();
        ResourceGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        ResourceUsageIndex index = new ResourceUsageIndex(
            catalog,
            nullRuntime,
            new ResourceCombatEquipmentCatalog(content),
            content);
        return index.ValidateContentGraph();
    }

    private static T GetOrCreate<T>(string path)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        MonoScript assetScript = asset != null
            ? MonoScript.FromScriptableObject(asset)
            : null;
        if (asset != null
            && (assetScript == null
                || string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(assetScript))))
        {
            AssetDatabase.DeleteAsset(path);
            asset = null;
        }

        if (asset != null)
        {
            return asset;
        }

        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolders(params string[] targets)
    {
        foreach (string target in targets)
        {
            string current = "Assets";
            foreach (string segment in target.Substring("Assets/".Length).Split('/'))
            {
                string next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }
                current = next;
            }
        }
    }

    private static string Sanitize(string id)
    {
        return id.Replace(':', '_').Replace('-', '_').Replace('/', '_');
    }

    private static ItemSpec I(
        string id,
        string name,
        string description,
        StockCategory category,
        ResourceItemKind kind,
        ResourceIngredientTag tags,
        int price,
        float weight,
        int maxStack,
        string researchId,
        float marketSaleRate = 0.6f)
    {
        return new ItemSpec(
            id,
            name,
            description,
            category,
            kind,
            tags,
            price,
            weight,
            maxStack,
            researchId,
            marketSaleRate: marketSaleRate);
    }

    private static ItemSpec M(
        string id,
        string name,
        string description,
        ResourceIngredientTag tags,
        int price,
        float weight,
        int maxStack,
        string researchId,
        MealQualityTier quality,
        float nutrition,
        float mood,
        float freshnessSeconds,
        bool preserved = false)
    {
        return new ItemSpec(
            id,
            name,
            description,
            StockCategory.Food,
            ResourceItemKind.Food,
            tags,
            price,
            weight,
            maxStack,
            researchId,
            true,
            quality,
            nutrition,
            mood,
            freshnessSeconds,
            preserved);
    }

    private static MealQualityBand ResolveMealQualityBand(string itemId) =>
        itemId switch
        {
            "food:grain-porridge" or "food:mushroom-soup" or "food:jerky" => MealQualityBand.Poor,
            "food:root-stew" or "food:roasted-meat" or "food:preserved-ration" => MealQualityBand.Simple,
            "food:garden-meal" or "food:egg-pancake" or "food:cheese-mushroom" => MealQualityBand.Decent,
            "food:boar-stew" or "food:meat-pie" => MealQualityBand.Fine,
            "food:lavish-vegan" or "food:lavish-meat" => MealQualityBand.Lavish,
            _ => MealQualityBand.Simple
        };

    private static MealServingRole ResolveMealServingRole(string itemId) =>
        itemId is "food:jerky" or "food:preserved-ration"
            ? MealServingRole.FieldRation
            : MealServingRole.FullMeal;

    private static ItemSpec Med(
        string id,
        string name,
        string description,
        ResourceIngredientTag tags,
        int price,
        float weight,
        int maxStack,
        string researchId,
        bool supportsInjuryTreatment,
        float treatmentPotency,
        float infectionReduction,
        float detoxReduction,
        float painReduction)
    {
        return new ItemSpec(
            id,
            name,
            description,
            StockCategory.Medicine,
            ResourceItemKind.Medicine,
            tags,
            price,
            weight,
            maxStack,
            researchId,
            hasMedicineData: true,
            supportsInjuryTreatment: supportsInjuryTreatment,
            treatmentPotency: treatmentPotency,
            infectionReduction: infectionReduction,
            detoxReduction: detoxReduction,
            painReduction: painReduction);
    }

    private static ItemAmountDefinition A(string itemId, int amount) =>
        new ItemAmountDefinition(itemId, amount);

    private static ProductionOutputDefinition O(string itemId, int amount, float probability = 1f) =>
        new ProductionOutputDefinition(itemId, amount, probability);

    private static RecipeSpec R(
        string id,
        string name,
        string facility,
        string workType,
        string researchId,
        float requiredWork,
        params object[] parts)
    {
        ItemAmountDefinition[] inputs =
            parts.OfType<ItemAmountDefinition>().ToArray();
        ProductionOutputDefinition[] outputs =
            parts.OfType<ProductionOutputDefinition>().ToArray();
        ProductionFlowRole flowRole = ResolveFlowRole(inputs, outputs);
        return new RecipeSpec(
            id,
            name,
            $"{name} 생산 주문",
            facility,
            workType,
            researchId,
            requiredWork,
            inputs,
            outputs,
            flowRole,
            V23RecipeProcessClassAuthoring.Resolve(
                facility,
                workType,
                flowRole));
    }

    private static RecipeSpec Source(
        string id,
        string name,
        string facility,
        string researchId,
        params ProductionOutputDefinition[] outputs)
    {
        return new RecipeSpec(
            id,
            name,
            $"{name} 자원 산출",
            facility,
            "work:operate",
            researchId,
            10f,
            Array.Empty<ItemAmountDefinition>(),
            outputs,
            ProductionFlowRole.Source,
            ProductionProcessClass.Gathering);
    }

    private static RecipeSpec SourceWork(
        string id,
        string name,
        string facility,
        string workType,
        string researchId,
        params ProductionOutputDefinition[] outputs)
    {
        return new RecipeSpec(
            id,
            name,
            $"{name} 자원 산출",
            facility,
            workType,
            researchId,
            10f,
            Array.Empty<ItemAmountDefinition>(),
            outputs,
            ProductionFlowRole.Source,
            ProductionProcessClass.Gathering);
    }

    private static RecipeSpec Sink(
        string id,
        string name,
        string facility,
        string researchId,
        params ItemAmountDefinition[] inputs)
    {
        return new RecipeSpec(
            id,
            name,
            $"{name} 소비",
            facility,
            "work:operate",
            researchId,
            4f,
            inputs,
            Array.Empty<ProductionOutputDefinition>(),
            ProductionFlowRole.Sink,
            V23RecipeProcessClassAuthoring.Resolve(
                facility,
                "work:operate",
                ProductionFlowRole.Sink));
    }

    private static CropSpec C(
        string id,
        string name,
        string itemId,
        string researchId,
        float hours,
        float sow,
        float harvest,
        float water,
        int yield,
        bool indoor,
        float minTemperature,
        float maxTemperature)
    {
        return new CropSpec(id, name, itemId, researchId, hours, sow, harvest, water, yield, indoor, minTemperature, maxTemperature);
    }

    private static MaterialSpec M(
        string id,
        string itemId,
        string name,
        CombatMaterialFamily family,
        float damage,
        float penetration,
        float durability,
        float weight,
        float value,
        float insulation,
        float mentalResistance,
        float arcaneResistance,
        Color tint,
        bool rare,
        string researchId)
    {
        return new MaterialSpec(id, itemId, name, family, damage, penetration, durability, weight, value, insulation, mentalResistance, arcaneResistance, tint, rare, researchId);
    }

    private sealed class ItemSpec
    {
        public ItemSpec(
            string id,
            string name,
            string description,
            StockCategory category,
            ResourceItemKind kind,
            ResourceIngredientTag tags,
            int price,
            float weight,
            int maxStack,
            string researchId,
            bool hasMealData = false,
            MealQualityTier mealQuality = MealQualityTier.Simple,
            float nutrition = 0f,
            float mealMood = 0f,
            float freshnessSeconds = 0f,
            bool preserved = false,
            bool hasMedicineData = false,
            bool supportsInjuryTreatment = false,
            float treatmentPotency = 1f,
            float infectionReduction = 0f,
            float detoxReduction = 0f,
            float painReduction = 0f,
            float marketSaleRate = 0.6f)
        {
            Id = id; Name = name; Description = description; Category = category; Kind = kind;
            Tags = tags; Price = price; Weight = weight; MaxStack = maxStack; ResearchId = researchId;
            HasMealData = hasMealData; MealQuality = mealQuality; Nutrition = nutrition;
            MealMood = mealMood; FreshnessSeconds = freshnessSeconds; Preserved = preserved;
            HasMedicineData = hasMedicineData;
            SupportsInjuryTreatment = supportsInjuryTreatment;
            TreatmentPotency = treatmentPotency;
            InfectionReduction = infectionReduction;
            DetoxReduction = detoxReduction;
            PainReduction = painReduction;
            MarketSaleRate = Mathf.Clamp01(marketSaleRate);
        }
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public StockCategory Category { get; }
        public ResourceItemKind Kind { get; }
        public ResourceIngredientTag Tags { get; }
        public int Price { get; }
        public float Weight { get; }
        public int MaxStack { get; }
        public string ResearchId { get; }
        public bool HasMealData { get; }
        public MealQualityTier MealQuality { get; }
        public float Nutrition { get; }
        public float MealMood { get; }
        public float FreshnessSeconds { get; }
        public bool Preserved { get; }
        public bool HasMedicineData { get; }
        public bool SupportsInjuryTreatment { get; }
        public float TreatmentPotency { get; }
        public float InfectionReduction { get; }
        public float DetoxReduction { get; }
        public float PainReduction { get; }
        public float MarketSaleRate { get; }
    }

    private sealed class RecipeSpec
    {
        public RecipeSpec(
            string id,
            string name,
            string description,
            string facilityTag,
            string workTypeId,
            string researchId,
            float requiredWork,
            ItemAmountDefinition[] inputs,
            ProductionOutputDefinition[] outputs,
            ProductionFlowRole flowRole,
            ProductionProcessClass processClass)
        {
            Id = id; Name = name; Description = description; FacilityTag = facilityTag;
            WorkTypeId = workTypeId; ResearchId = researchId; RequiredWork = requiredWork;
            Inputs = inputs; Outputs = outputs;
            FlowRole = flowRole;
            ProcessClass = processClass;
        }
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string FacilityTag { get; }
        public string WorkTypeId { get; }
        public string ResearchId { get; }
        public float RequiredWork { get; }
        public ItemAmountDefinition[] Inputs { get; }
        public ProductionOutputDefinition[] Outputs { get; }
        public ProductionFlowRole FlowRole { get; }
        public ProductionProcessClass ProcessClass { get; }
    }

    private sealed class CropSpec
    {
        public CropSpec(string id, string name, string itemId, string researchId, float growthHours, float sowWork, float harvestWork, float water, int yield, bool indoor, float minTemperature, float maxTemperature)
        {
            Id = id; Name = name; ItemId = itemId; ResearchId = researchId; GrowthHours = growthHours;
            SowWork = sowWork; HarvestWork = harvestWork; Water = water; Yield = yield; Indoor = indoor;
            MinTemperature = minTemperature; MaxTemperature = maxTemperature;
        }
        public string Id { get; }
        public string Name { get; }
        public string ItemId { get; }
        public string ResearchId { get; }
        public float GrowthHours { get; }
        public float SowWork { get; }
        public float HarvestWork { get; }
        public float Water { get; }
        public int Yield { get; }
        public bool Indoor { get; }
        public float MinTemperature { get; }
        public float MaxTemperature { get; }
    }

    private sealed class MaterialSpec
    {
        public MaterialSpec(string id, string itemId, string name, CombatMaterialFamily family, float damage, float penetration, float durability, float weight, float value, float insulation, float mentalResistance, float arcaneResistance, Color tint, bool rare, string researchId)
        {
            Id = id; ItemId = itemId; Name = name; Family = family; Damage = damage;
            Penetration = penetration; Durability = durability; Weight = weight; Value = value;
            Insulation = insulation; MentalResistance = mentalResistance; ArcaneResistance = arcaneResistance;
            Tint = tint; Rare = rare; ResearchId = researchId;
        }
        public string Id { get; }
        public string ItemId { get; }
        public string Name { get; }
        public CombatMaterialFamily Family { get; }
        public float Damage { get; }
        public float Penetration { get; }
        public float Durability { get; }
        public float Weight { get; }
        public float Value { get; }
        public float Insulation { get; }
        public float MentalResistance { get; }
        public float ArcaneResistance { get; }
        public Color Tint { get; }
        public bool Rare { get; }
        public string ResearchId { get; }
    }

    private sealed class NullWorldItemStackRuntime : IWorldItemStackRuntime
    {
        public IDungeonItemCatalogProvider CatalogProvider => null;
        public IItemHaulingSettingsProvider HaulingSettingsProvider => null;
        public bool StoredItemMarkersVisible => false;
        public int ItemStackVersion => 0;
        public int HaulJobVersion => 0;
        public int GetCommittedHaulDeliveryQuantity(string destinationId, string itemId) => 0;
        public bool TryCommitHaulPickup(string ownerOperationId, CharacterCarryInventory inventory, out string failureReason) { failureReason = "null haul delivery authority unavailable"; return false; }
        public bool TryCaptureHaulDeliveryIntent(string ownerOperationId, out HaulDeliveryIntentSaveData intent) { intent = null; return false; }
        public bool ReleaseHaulDeliveryIntent(string ownerOperationId) => false;
        public DungeonPhysicalItemSaveData Capture() => new DungeonPhysicalItemSaveData();
        public void Restore(DungeonPhysicalItemSaveData snapshot) { }
        public void SetStoredItemMarkersVisible(bool visible) { }
        public bool SpawnItemAtDropoff(string itemId, int amount, string sourceLabel, out int spawned) { spawned = 0; return false; }
        public bool SpawnStockAtDropoff(StockCategory category, int amount, string sourceLabel, out int spawned) { spawned = 0; return false; }
        public bool SpawnStockAtDropoff(StockCategory category, int amount, string sourceLabel, WorldItemStackState state, string destinationId, out int spawned) { spawned = 0; return false; }
        public bool SpawnStockInWarehouse(IWarehouseFacility warehouse, StockCategory category, int amount, out int spawned) { spawned = 0; return false; }
        public bool SpawnItemAt(string itemId, int amount, Vector2Int position, WorldItemStackState state, string destinationId, out int spawned) { spawned = 0; return false; }
        public bool SpawnWasteAt(string itemId, int amount, Vector2Int position, WasteOriginKind wasteOrigin, float contamination, out int spawned) { spawned = 0; return false; }
        public bool SpawnUniqueItemAt(string itemId, Vector2Int position, WorldItemStackState state, string destinationId, out string stackId) { stackId = string.Empty; return false; }
        public bool SpawnUniqueItemAt(string itemId, Vector2Int position, WorldItemStackState state, string destinationId, Vector2Int destinationPosition, out string stackId) { stackId = string.Empty; return false; }
        public bool SpawnExistingUniqueItemAt(string itemId, ItemInstanceId itemInstanceId, Vector2Int position, WorldItemStackState state, string destinationId, out string stackId) { stackId = string.Empty; return false; }
        public bool TryAbsorbUniqueItemStack(string stackId, ItemInstanceId expectedInstanceId) => false;
        public bool SpawnHumanoidCorpse(CharacterActor source, Vector2Int position, string deathReason, out string stackId) { stackId = string.Empty; return false; }
        public bool TryRequestFacilityDelivery(StockCategory category, int amount, Vector2Int destinationPosition, string destinationId, out int requested, out string failureReason) { requested = 0; failureReason = string.Empty; return false; }
        public bool TryRequestItemDelivery(string itemId, int amount, Vector2Int destinationPosition, string destinationId, out int requested, out string failureReason) { requested = 0; failureReason = string.Empty; return false; }
        public bool TryRequestStackDelivery(string stackId, int amount, Vector2Int destinationPosition, string destinationId, out int requested, out string failureReason) { requested = 0; failureReason = string.Empty; return false; }
        public bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile) { pile = null; return false; }
        public bool TryGetPileTargetAt(Vector2Int position, out ItemPileInfoTarget target, out UnityEngine.Object markerObject) { target = null; markerObject = null; return false; }
        public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(Vector2Int position, bool includeStored = false) => Array.Empty<WorldItemStackSnapshot>();
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => Array.Empty<WorldItemStackSnapshot>();
        public bool TryFindNearestAvailableStock(Vector2Int origin, StockCategory category, bool preferStored, out WorldItemStackSnapshot stack) { stack = null; return false; }
        public void CopyAvailableStockCandidates(StockCategory category, List<WorldItemStockCandidate> destination) { destination?.Clear(); }
        public bool TryFindBestAvailableStack(Vector2Int origin, Func<string, int> rankSelector, out WorldItemStackSnapshot stack) { stack = null; return false; }
        public bool HasAvailableHaulJob(CharacterActor actor) => false;
        public bool TryReserveBestHaulPlan(CharacterActor actor, out WorldItemHaulPlan plan, out string failureReason) { plan = null; failureReason = string.Empty; return false; }
        public bool TryReserveStoredItemForDirectPickup(CharacterActor actor, string itemId, int quantity, out WorldItemReservedStackQuantity reservation, out Vector2Int pickupStandPosition, out string failureReason) { reservation = default; pickupStandPosition = default; failureReason = string.Empty; return false; }
        public bool TryReserveBestHaulJob(CharacterActor actor, out WorldItemHaulJob job, out string failureReason) { job = default; failureReason = string.Empty; return false; }
        public bool TryPickupReservedStackQuantity(CharacterActor actor, CharacterCarryInventory inventory, WorldItemReservedStackQuantity reservation, out int pickedUp, out string failureReason) { pickedUp = 0; failureReason = string.Empty; return false; }
        public bool TryPickupReservedStack(CharacterActor actor, CharacterCarryInventory inventory, WorldItemHaulJob job, out string failureReason) { failureReason = string.Empty; return false; }
        public bool TryDepositCarriedItems(CharacterActor actor, CharacterCarryInventory inventory, IWarehouseFacility warehouse, out string failureReason) { failureReason = string.Empty; return false; }
        public bool TryDepositCarriedItems(CharacterActor actor, CharacterCarryInventory inventory, IWarehouseFacility warehouse, IReadOnlyCollection<string> ownerOperationIds, out string failureReason) { failureReason = string.Empty; return false; }
        public bool TryDepositCarriedItemsToFacility(CharacterActor actor, CharacterCarryInventory inventory, Vector2Int destinationPosition, string destinationId, out string failureReason) { failureReason = string.Empty; return false; }
        public bool TryDepositCarriedItemsToFacility(CharacterActor actor, CharacterCarryInventory inventory, Vector2Int destinationPosition, string destinationId, IReadOnlyCollection<string> ownerOperationIds, out string failureReason) { failureReason = string.Empty; return false; }
        public bool TryConsumeFacilityBuffer(string destinationId, IReadOnlyDictionary<StockCategory, int> costs, out string failureReason) { failureReason = string.Empty; return false; }
        public bool TryConsumeFacilityItemBuffer(string destinationId, IReadOnlyDictionary<string, int> costs, out string failureReason) { failureReason = string.Empty; return false; }
        public bool TryStealLooseItem(CharacterActor actor, int searchRadius, out WorldItemStackSnapshot stolenItem, out string failureReason) { stolenItem = null; failureReason = string.Empty; return false; }
        public void ReleaseReservation(string stackId, string persistentId) { }
        public bool TryClearReservation(string stackId) => false;
        public bool SetForbidden(string stackId, bool forbidden) => false;
        public bool PrioritizeHaul(string stackId) => false;
        public bool TryRouteStackToDestination(string stackId, WorldItemStackState state, string destinationId, Vector2Int destinationPosition, out string failureReason) { failureReason = string.Empty; return false; }
        public bool DeleteStack(string stackId) => false;
        public bool TryConsumeStackQuantity(string stackId, int quantity, out WorldItemStackSnapshot consumed) { consumed = null; return false; }
        public bool TrySetInstanceComponent(string stackId, ItemInstanceComponentSaveData component) => false;
        public bool SetEmergencyButcheryAllowed(string stackId, bool allowed) => false;
        public int RemoveStacksByStateAndDestination(WorldItemStackState state, string destinationId) => 0;
        public int ReleaseStacksByDestination(string destinationId, Vector2Int releasePosition) => 0;
    }
}
#endif
