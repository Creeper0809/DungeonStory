#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResearchOverhaulContentAssetBuilder
{
    private const string FacilityRoot =
        "Assets/Resources/SO/Building/ResearchOverhaul";
    private const string ItemRoot =
        "Assets/Resources/SO/Economy/Items/ResearchOverhaul";
    private const string RecipeRoot =
        "Assets/Resources/SO/Economy/Recipes/ResearchOverhaul";

    private readonly struct FacilitySpec
    {
        public FacilitySpec(string researchId, string name, string workstationTag)
        {
            ResearchId = researchId;
            Name = name;
            WorkstationTag = workstationTag;
        }

        public string ResearchId { get; }
        public string Name { get; }
        public string WorkstationTag { get; }
    }

    private readonly struct InputSpec
    {
        public InputSpec(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }

        public string ItemId { get; }
        public int Amount { get; }
    }

    private readonly struct ItemSpec
    {
        public ItemSpec(
            string researchId,
            string itemId,
            string name,
            ResourceItemKind kind,
            ResourceIngredientTag tags,
            string workstationTag,
            int outputAmount,
            bool sharedIntermediate,
            bool craftable,
            params InputSpec[] inputs)
        {
            ResearchId = researchId;
            ItemId = itemId;
            Name = name;
            Kind = kind;
            Tags = tags;
            WorkstationTag = workstationTag;
            OutputAmount = outputAmount;
            SharedIntermediate = sharedIntermediate;
            Craftable = craftable;
            Inputs = inputs ?? Array.Empty<InputSpec>();
        }

        public string ResearchId { get; }
        public string ItemId { get; }
        public string Name { get; }
        public ResourceItemKind Kind { get; }
        public ResourceIngredientTag Tags { get; }
        public string WorkstationTag { get; }
        public int OutputAmount { get; }
        public bool SharedIntermediate { get; }
        public bool Craftable { get; }
        public InputSpec[] Inputs { get; }
    }

    public static void EnsureAssets()
    {
        EnsureFolder(FacilityRoot);
        EnsureFolder(ItemRoot);
        EnsureFolder(RecipeRoot);
        BuildFacilities();
        BuildItemsAndRecipes();
    }

    public static IReadOnlyDictionary<string, int[]> GetFacilityUnlockIds() =>
        FacilitySpecs()
            .Select((spec, index) => new { spec.ResearchId, Id = 8801 + index })
            .GroupBy(entry => entry.ResearchId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Id).ToArray(),
                StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, int[]> GetExistingFacilityUnlockIds() =>
        new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            ["research:arcane:records"] = new[] { 0 },
            ["research:authority:office"] = new[] { 0 },
            ["research:authority:quarters"] = new[] { 0 },
            ["research:husbandry:capture"] = new[] { 0 },
            ["research:pharmacology:herbalism"] = new[] { 0 },
            ["research:defense:ranged-positions"] = new[] { 0 },
            ["research:defense:watch"] = new[] { 0 },
            ["research:equipment:engineering-drawing"] = new[] { 0 }
        };

    public static IReadOnlyDictionary<string, string> GetExistingFacilityCodes() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["research:arcane:records"] = "Q03",
            ["research:authority:office"] = "R07",
            ["research:authority:quarters"] = "R10",
            ["research:husbandry:capture"] = "Q05",
            ["research:pharmacology:herbalism"] = "Q04",
            ["research:defense:ranged-positions"] = "T02",
            ["research:defense:watch"] = "G02",
            ["research:equipment:engineering-drawing"] = "Q06"
        };

    private static void BuildFacilities()
    {
        Sprite fallbackSprite = AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building/Modular" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(building => building != null && building.sprite != null)
            ?.sprite;
        FacilitySpec[] specs = FacilitySpecs();
        HashSet<string> expected = specs
            .Select((spec, index) =>
                $"{FacilityRoot}/RF{index + 1:D2}_{Sanitize(spec.Name)}.asset")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DeleteStale<BuildingSO>(FacilityRoot, expected);

        for (int index = 0; index < specs.Length; index++)
        {
            FacilitySpec spec = specs[index];
            string code = $"RF{index + 1:D2}";
            string path = $"{FacilityRoot}/{code}_{Sanitize(spec.Name)}.asset";
            BuildingSO building = GetOrCreate<BuildingSO>(path);
            building.id = 8801 + index;
            building.objectName = spec.Name;
            building.sprite = fallbackSprite;
            building.icon = fallbackSprite;
            building.width = 1;
            building.height = 1;
            building.layer = GridLayer.Building;
            building.category = BuildingCategory.Crafting;
            building.horizontalDraggable = false;
            building.verticalDraggable = false;
            building.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
            building.tiles = null;
            building.movementAnchorOffset = Vector2.zero;
            building.movementTravelTime = 1.2f;
            building.unlocked = false;

            BuildingAbilityCollection abilities = new BuildingAbilityCollection();
            abilities.Add(new BuildingFacilityPartAbility { code = code });
            abilities.Add(new BuildingSemanticTagsAbility
            {
                tags = new[]
                {
                    "research-overhaul",
                    spec.ResearchId,
                    spec.WorkstationTag
                }
            });
            abilities.Add(new BuildingProductionWorkstationAbility
            {
                workstationTag = spec.WorkstationTag,
                stockSensorInstallationItemId = "component:stock-sensor-panel"
            });
            abilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 4
            });
            abilities.Add(new BuildingEconomyAbility
            {
                constructionCost = 80 + index * 4,
                maintenance = 1 + index / 12,
                unlockPhase = 1,
                demolitionRefundRate = 0.5f
            });
            abilities.Add(new BuildingFacilityAbility
            {
                settings = new FacilityData
                {
                    roles = FacilityRole.Research | FacilityRole.Logistics,
                    capacity = 1,
                    useDuration = 1.5f,
                    requiredWorkers = 1,
                    disabledWhenDamaged = true
                }
            });
            abilities.Add(new BuildingRoomRequirementAbility());
            BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
            {
                constructionWorkRequired = 90f + index * 3f,
                repairWorkRequired = 24f,
                cleanWorkRequired = 8f,
                operateWorkRequired = 12f
            };
            workAmount.SetConstructionMaterials(
                ResolveConstructionMaterials(spec, index));
            abilities.Add(workAmount);
            if (string.Equals(
                    spec.ResearchId,
                    "research:industry:maintenance",
                    StringComparison.Ordinal))
            {
                BuildingEquipmentMaintenanceAbility maintenance = new()
                {
                    workSpeedMultiplier = 1.2f,
                    simultaneousRepairSlots = 2
                };
                maintenance.ConfigureRepairSupply(
                    "tool:maintenance-kit",
                    1);
                abilities.Add(maintenance);
            }
            building.ReplaceAbilities(abilities);
            building.AbilityModules.EnsureStableIds();
            building.ValidateAbilitiesOrThrow();
            EditorUtility.SetDirty(building);
        }
    }

    private static string ResolveConstructionMaterialId(int index)
    {
        if (index >= 40)
        {
            return "component:precision-parts";
        }

        if (index >= 20)
        {
            return "component:machine-parts";
        }

        return "material:lumber";
    }

    private static IReadOnlyList<ItemAmountDefinition>
        ResolveConstructionMaterials(FacilitySpec spec, int index)
    {
        List<ItemAmountDefinition> materials = new()
        {
            new ItemAmountDefinition(
                ResolveConstructionMaterialId(index),
                4 + index / 10)
        };
        string[] installationItems = spec.ResearchId switch
        {
            "research:industry:rune-grid" =>
                new[] { "tool:alloy-crucible" },
            "research:industry:industrial-cooling" or
            "research:industry:line-balancing" or
            "research:industry:maintenance" or
            "research:industry:powered-tools" =>
                new[] { "component:factory-installation-plan" },
            "research:industry:precision" =>
                new[] { "tool:powered-tool-head" },
            "research:industry:rune-automation" =>
                new[]
                {
                    "component:rune-bus-coupler",
                    "tool:precision-gauge"
                },
            "research:equipment:precision-fitting" =>
                new[] { "tool:precision-gauge" },
            "research:equipment:modular-frames" =>
                new[] { "component:prototype-package" },
            "research:equipment:industrial-metrology" =>
                new[]
                {
                    "component:prototype-package",
                    "component:paper-paste",
                    "tool:powered-tool-head"
                },
            "research:medical:construct-core-engineering" =>
                new[] { "component:factory-installation-plan" },
            _ => Array.Empty<string>()
        };
        materials.AddRange(installationItems.Select(
            itemId => new ItemAmountDefinition(itemId, 1)));
        return materials;
    }

    private static void BuildItemsAndRecipes()
    {
        ItemSpec[] specs = ItemSpecs();
        HashSet<string> itemPaths = specs
            .Select((spec, index) => ItemPath(index, spec))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> recipePaths = specs
            .Select((spec, index) => new { spec, index })
            .Where(entry => entry.spec.Craftable)
            .Select(entry => RecipePath(entry.index, entry.spec))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DeleteStale<ResourceItemDefinitionSO>(ItemRoot, itemPaths);
        DeleteStale<ProductionRecipeSO>(RecipeRoot, recipePaths);

        for (int index = 0; index < specs.Length; index++)
        {
            ItemSpec spec = specs[index];
            ResourceItemDefinitionSO item =
                GetOrCreate<ResourceItemDefinitionSO>(ItemPath(index, spec));
            item.id = 8901 + index;
            item.Configure(
                spec.ItemId,
                spec.Name,
                $"분기형 생산망의 {spec.Name}.",
                spec.ItemId == "resource:clean-water"
                    ? StockCategory.Water
                    : CategoryFor(spec.Kind),
                spec.Kind,
                spec.Tags,
                12 + index * 2,
                0.25f + index * 0.03f,
                spec.ItemId == PhysicalItemIds.EquipmentModule
                    ? 1
                    : spec.Kind == ResourceItemKind.Ammunition ? 120 : 50,
                spec.ResearchId);
            item.ConfigureFacilitySupply(0f, false, spec.SharedIntermediate);
            if (spec.ItemId == "medical:sterile-bandage")
            {
                item.ConfigureMedicine(true, 0.85f, 10f, 0f, 8f);
            }
            EditorUtility.SetDirty(item);

            if (!spec.Craftable)
            {
                continue;
            }

            ProductionRecipeSO recipe =
                GetOrCreate<ProductionRecipeSO>(RecipePath(index, spec));
            recipe.id = 9101 + index;
            recipe.Configure(
                $"recipe:{spec.ItemId}",
                spec.Name,
                $"구체 재료를 사용해 {spec.Name}을(를) 생산한다.",
                spec.WorkstationTag,
                BuiltInWorkTypeIds.Craft.Value,
                spec.ResearchId,
                8f + index * 0.75f,
                spec.Inputs.Select(input =>
                    new ItemAmountDefinition(input.ItemId, input.Amount)),
                new[]
                {
                    new ProductionOutputDefinition(
                        spec.ItemId,
                        Mathf.Max(1, spec.OutputAmount))
                });
            recipe.ConfigureWorkshop(
                spec.WorkstationTag,
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly);
            EditorUtility.SetDirty(recipe);
        }
    }

    private static FacilitySpec[] FacilitySpecs() => new[]
    {
        F("research:agriculture:gathering", "채집 바구니 작업대", "workstation:v3:gathering"),
        F("research:agriculture:irrigation", "중력식 수문", "workstation:v3:irrigation"),
        F("research:agriculture:subterranean", "동굴 재배 선반", "workstation:v3:subterranean"),
        F("research:authority:prestige", "문장 깃발 제작대", "workstation:v3:heraldry"),
        F("research:authority:ritual", "의식 화로", "workstation:v3:ritual"),
        F("research:commerce:logistics", "운반 멜빵 걸이", "workstation:v3:logistics"),
        F("research:commerce:retail", "가격표 게시판", "workstation:v3:retail"),
        F("research:control:blood-show", "피의 무대 배수구", "workstation:v3:blood-stage"),
        F("research:control:labor", "포로 작업 도구함", "workstation:v3:prison-labor"),
        F("research:control:restraints", "강화 구속구 선반", "workstation:v3:restraint"),
        F("research:control:show", "공연 소품 보관대", "workstation:v3:show"),
        F("research:defense:alliance-signals", "동맹 신호기", "workstation:v3:signals"),
        F("research:forestry:fungal", "균사 재배 선반", "workstation:v3:fungal"),
        F("research:forestry:logging", "벌목 키트 걸이", "workstation:v3:logging"),
        F("research:forestry:tools", "쐐기 도끼 작업대", "workstation:v3:forestry-tools"),
        F("research:forestry:treated", "방부 처리 목재대", "workstation:v3:treated-lumber"),
        F("research:husbandry:breeding", "번식 장부대", "workstation:v3:breeding"),
        F("research:husbandry:selective", "혈통 촉진제 선반", "workstation:v3:selective"),
        F("research:husbandry:stable", "마구 선반", "workstation:v3:stable"),
        F("research:husbandry:taming", "조련용 고삐 걸이", "workstation:v3:taming"),
        F("research:industry:assisted-processing", "동력 공구날 연마대", "workstation:v3:machine-parts"),
        F("research:industry:automatic-sanitation", "자동 세척기", "workstation:v3:sanitation"),
        F("research:industry:rune-grid", "룬 버스 결합기", "workstation:v3:rune-conductor"),
        F("research:industry:defense-supply", "방어시설 장전기", "workstation:v3:defense-ammo"),
        F("research:equipment:prototype-engineering", "시제품 연구실", "workstation:v3:prototype"),
        F("research:equipment:material-testing", "재료 시험기", "workstation:v3:material-test"),
        F("research:industry:factory-layout", "기계 기초대", "workstation:v3:factory-layout"),
        F("research:industry:industrial-cooling", "냉각 매니폴드", "workstation:v3:cooling"),
        F("research:industry:line-balancing", "유량계", "workstation:v3:metering"),
        F("research:industry:maintenance", "정비 부품함", "workstation:v3:maintenance"),
        F("research:industry:powered-tools", "전동 선반", "workstation:v3:powered-tools"),
        F("research:industry:precision", "정밀 게이지", "workstation:v3:precision-parts"),
        F("research:industry:rune-automation", "룬 제어반", "workstation:v3:rune-control"),
        F("research:equipment:weapon-patterns", "무기 도면걸이", "workstation:v3:weapon-pattern"),
        F("research:equipment:armor-tailoring", "방어구 맞춤대", "workstation:v3:armor-tailoring"),
        F("research:equipment:bowyery", "궁시 지그", "workstation:v3:bow-jig"),
        F("research:equipment:mechanical-projectiles", "권양 작업대", "workstation:v3:windlass"),
        F("research:equipment:chain-weaving", "사슬 조립틀", "workstation:v3:chain"),
        F("research:equipment:articulated-plate", "관절 지그", "workstation:v3:plate-jig"),
        F("research:equipment:black-powder", "화약 분쇄소", "workstation:v3:powder-mill"),
        F("research:equipment:standard-ammunition", "탄약 압착기", "workstation:v3:ammo-press"),
        F("research:equipment:relic-appraisal", "부품 감정대", "workstation:v3:appraisal"),
        F("research:equipment:relic-restoration", "부품 복원 작업대", "workstation:v3:restoration"),
        F("research:equipment:precision-fitting", "정밀 장착대", "workstation:v3:precision-fitting"),
        F("research:equipment:modular-frames", "성장형 골격 지그", "workstation:v3:growth-frame"),
        F("research:equipment:industrial-metrology", "계측 작업대", "workstation:v3:metrology"),
        F("research:medical:construct-core-engineering", "구성체 핵 공학대", "workstation:v3:construct-core-engineering"),
        F("research:service:dining-operations", "배식 운영판", "workstation:v3:dining-operations")
    };

    private static ItemSpec[] ItemSpecs() => new[]
    {
        S("research:agriculture:irrigation", "resource:clean-water", "깨끗한 물", ResourceItemKind.Raw, ResourceIngredientTag.None, "workstation:v3:irrigation", 4, false, true),
        S("research:mining:surface", "resource:sulfur", "황", ResourceItemKind.Raw, ResourceIngredientTag.Mineral, "workstation:v3:material-test", 2, false, true),
        S("research:mining:surface", "resource:lead-ore", "납광석", ResourceItemKind.Raw, ResourceIngredientTag.Mineral, "workstation:v3:material-test", 2, false, true),
        S("research:equipment:black-powder", "material:niter", "초석", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:powder-mill", 2, true, true, A("resource:manure", 3), A("resource:clean-water", 1)),
        S("research:equipment:engineering-drawing", "material:paper", "종이", ResourceItemKind.Intermediate, ResourceIngredientTag.Plant, "workstation:v3:prototype", 4, true, true, A("material:lumber", 1), A("resource:clean-water", 1)),
        S("research:metallurgy:iron", "material:lead-ingot", "납괴", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:material-test", 1, true, true, A("resource:lead-ore", 2), A("material:charcoal", 1)),
        S("research:equipment:standard-ammunition", "material:lead-shot", "납탄", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:ammo-press", 12, true, true, A("material:lead-ingot", 1)),
        S("research:textile:fiber", "material:rope", "밧줄", ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber, "workstation:v3:bow-jig", 2, true, true, A("resource:shade-fiber", 3)),
        S("research:industry:assisted-processing", "component:machine-parts", "기계 부품", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:machine-parts", 1, true, true, A("material:iron-ingot", 2)),
        S("research:equipment:precision-fitting", "component:precision-parts", "정밀 부품", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:precision-parts", 1, true, true, A("material:steel-ingot", 2), A("material:iron-ingot", 1)),
        S("research:industry:rune-grid", "component:rune-conductor", "룬 도체", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, "workstation:v3:rune-conductor", 1, true, true, A("material:gold-ingot", 1), A("resource:mana-crystal", 1), A("resource:rune-dust", 1)),
        S("research:medical:surgery", "textile:sterile-cloth", "무균 천", ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber, "workstation:v3:armor-tailoring", 2, true, true, A("material:cloth", 2), A("resource:saltstone", 1), A("resource:clean-water", 1)),
        S("research:equipment:black-powder", "material:black-powder", "흑색화약", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:powder-mill", 6, true, true, A("material:charcoal", 2), A("resource:sulfur", 1), A("material:niter", 2)),
        S("research:textile:layered", "textile:quilted-liner", "층상 충전재", ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber, "workstation:v3:armor-tailoring", 1, true, true, A("material:cloth", 2), A("resource:wool", 1)),
        S("research:equipment:modular-frames", "component:growth-frame", "성장형 장비 골격", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral | ResourceIngredientTag.Wood, "workstation:v3:growth-frame", 1, true, true, A("material:steel-ingot", 2), A("component:machine-parts", 1), A("component:precision-parts", 1), A("material:treated-lumber", 1)),
        S("research:agriculture:compost", "supply:nitrate-fertilizer", "질산 비료", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v3:subterranean", 4, false, true, A("material:niter", 1), A("material:compost", 2)),
        S("research:equipment:engineering-drawing", "component:engineering-drawing", "공학 도면", ResourceItemKind.Intermediate, ResourceIngredientTag.Plant, "workstation:v3:prototype", 2, true, true, A("material:paper", 2), A("material:charcoal", 1)),
        S("research:mining:deep", "component:lead-counterweight", "납 균형추", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:windlass", 1, true, true, A("material:lead-ingot", 2)),
        S("research:mining:mana", "component:mana-shield-plate", "마나 차폐판", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, "workstation:v3:material-test", 1, true, true, A("material:lead-ingot", 1), A("resource:rune-dust", 1)),
        S("research:mining:deep", "ammo:blasting-charge", "발파 장약", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:defense-ammo", 2, false, true, A("material:black-powder", 2), A("material:paper", 1), A("material:rope", 1)),
        S("research:equipment:standard-ammunition", "ammo:trap-canister", "함정 산탄통", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:defense-ammo", 2, false, true, A("material:lead-shot", 6), A("material:black-powder", 1), A("material:paper", 1)),
        S("research:medical:surgery", "medical:sterile-bandage", "무균 붕대", ResourceItemKind.Medicine, ResourceIngredientTag.Fiber, "workstation:v3:restoration", 2, false, true, A("textile:sterile-cloth", 1), A("medicine:antiseptic", 1)),
        S("research:equipment:standard-ammunition", "ammo:paper-cartridge", "종이 탄약통", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:ammo-press", 12, false, true, A("material:lead-shot", 6), A("material:black-powder", 1), A("material:paper", 1)),
        S("research:industry:stock-sensors", "component:stock-sensor-panel", "재고 감지반", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:metrology", 1, false, true, A("component:machine-parts", 1), A("component:precision-parts", 1)),
        S("research:industry:maintenance", "tool:maintenance-kit", "정비 키트", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:maintenance", 1, false, true, A("component:machine-parts", 1), A("material:cloth", 1)),
        S("research:industry:powered-tools", "tool:powered-tool-head", "동력 공구날", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:powered-tools", 1, false, true, A("component:machine-parts", 1), A("material:steel-ingot", 1)),
        S("research:mining:mana", "tool:mana-probe", "마나 탐침", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, "workstation:v3:material-test", 1, false, true, A("component:precision-parts", 1), A("component:mana-shield-plate", 1)),
        S("research:equipment:industrial-metrology", "tool:precision-gauge", "정밀 게이지", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:metrology", 1, false, true, A("component:precision-parts", 1), A("component:engineering-drawing", 1)),
        S("research:equipment:prototype-engineering", "component:prototype-package", "시제품 설계 묶음", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v3:prototype", 1, false, true, A("component:engineering-drawing", 1), A("component:machine-parts", 1)),
        S("research:industry:factory-layout", "component:factory-installation-plan", "공장 설치 도면", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v3:factory-layout", 1, false, true, A("component:engineering-drawing", 1), A("material:paper", 1)),
        S("research:equipment:mechanical-projectiles", "component:siege-counterweight", "공성 균형추 조립품", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:windlass", 1, false, true, A("component:lead-counterweight", 1), A("component:machine-parts", 1)),
        S("research:equipment:rune-module-tuning", "component:rune-tuning-shield", "룬 조율 차폐판", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, "workstation:v3:precision-fitting", 1, false, true, A("component:mana-shield-plate", 1), A("component:rune-conductor", 1)),
        S("research:medical:mana-core-engineering", "medical:mana-core-case", "마핵 케이스", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, "workstation:v3:precision-fitting", 1, false, true, A("component:rune-conductor", 1), A("component:precision-parts", 1)),
        S("research:industry:rune-automation", "component:rune-control-panel", "룬 제어반", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, "workstation:v3:rune-control", 1, false, true, A("component:rune-conductor", 1), A("component:precision-parts", 1)),
        S("research:industry:rune-grid", "component:rune-bus-coupler", "룬 버스 결합기", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, "workstation:v3:rune-conductor", 1, false, true, A("component:rune-conductor", 1), A("material:gold-ingot", 1)),
        S("research:medical:mycelial-grafting", "medical:sterile-mycelium-graft", "무균 균사 이식편", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fungus | ResourceIngredientTag.Fiber, "workstation:v3:restoration", 1, false, true, A("textile:sterile-cloth", 1), A("resource:cave-mushroom", 2)),
        S("research:medical:slime-bioengineering", "medical:slime-coagulation-frame", "점액 응고틀", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, "workstation:v3:restoration", 1, false, true, A("textile:sterile-cloth", 1), A("material:alchemical-solvent", 1)),
        S("research:equipment:blast-protection", "component:blast-coat-shell", "방폭 외투 내피", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, "workstation:v3:armor-tailoring", 1, false, true, A("textile:sterile-cloth", 1), A("textile:quilted-liner", 1)),
        S("research:equipment:armor-tailoring", "component:brigandine-padding", "브리간딘 안감", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, "workstation:v3:armor-tailoring", 1, false, true, A("textile:quilted-liner", 1), A("material:leather", 1)),
        S("research:mining:deep", "tool:deep-shaft-hoist", "심부 승강기", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:windlass", 1, false, true, A("material:rope", 2), A("component:lead-counterweight", 1), A("component:machine-parts", 1)),
        S("research:mining:surface", "tool:prospecting-kit", "탐광 키트", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:material-test", 1, false, true, A("material:rope", 1), A("material:treated-lumber", 1)),
        S("research:metallurgy:advanced", "tool:alloy-crucible", "합금 도가니", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:material-test", 1, false, true, A("material:stone-block", 2), A("material:steel-ingot", 1)),
        S("research:equipment:field-maintenance", "tool:field-repair-kit", "야전 수리 키트", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral | ResourceIngredientTag.Fiber, "workstation:v3:maintenance", 1, false, true, A("component:machine-parts", 1), A("material:cloth", 1)),
        S("research:metallurgy:blacksteel", "component:blacksteel-defense-plate", "흑강 방어 장갑판", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:plate-jig", 1, false, true, A("material:blacksteel-ingot", 1), A("component:engineering-drawing", 1)),
        S("research:equipment:powered-armor", "component:powered-armor-joint", "동력 갑주 관절", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:plate-jig", 1, false, true, A("material:blacksteel-ingot", 1), A("component:machine-parts", 1)),
        S("research:textile:dreamweave", "component:dreamweave-rune-lining", "몽직물 룬 안감", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber | ResourceIngredientTag.Arcane, "workstation:v3:armor-tailoring", 1, false, true, A("material:dreamweave", 1), A("component:rune-conductor", 1)),
        S("research:authority:ritual", "craft:dreamweave-ritual-banner", "몽직물 의식 장식", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber | ResourceIngredientTag.Arcane, "workstation:v3:ritual", 1, false, true, A("material:dreamweave", 1), A("material:gold-ingot", 1)),
        S("research:textile:rune-leather", "component:rune-leather-lining", "룬가죽 장비 안감", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber | ResourceIngredientTag.Arcane, "workstation:v3:armor-tailoring", 1, false, true, A("material:rune-leather", 1), A("material:cloth", 1)),
        S("research:equipment:rune-module-tuning", "component:rune-leather-strap", "룬가죽 조율 끈", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber | ResourceIngredientTag.Arcane, "workstation:v3:precision-fitting", 1, false, true, A("material:rune-leather", 1), A("component:rune-conductor", 1)),
        S("research:arcane:alchemy", "craft:toxic-trap-coating", "독성 함정 도포제", ResourceItemKind.FinishedGood, ResourceIngredientTag.Forbidden, "workstation:v3:defense-ammo", 2, false, true, A("material:rot-toxin", 1), A("resource:dark-resin", 1)),
        S("research:cuisine:milling", "component:paper-paste", "종이 풀칠", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v3:prototype", 2, false, true, A("material:starch", 1), A("resource:clean-water", 1)),
        S("research:textile:layered", "component:textile-hardener", "직물 경화제", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v3:armor-tailoring", 2, false, true, A("material:starch", 1), A("resource:dark-resin", 1)),
        S("research:agriculture:subterranean", "supply:mushroom-substrate", "균사 재배 배지", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant | ResourceIngredientTag.Fungus, "workstation:v3:subterranean", 2, false, true, A("material:compost", 1), A("resource:cave-mushroom", 1)),
        S("research:equipment:lineage-binding", EquipmentProgressionItemIds.LineageSeal, "계보 인장", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, string.Empty, 1, false, false),
        S(string.Empty, PhysicalItemIds.EquipmentModule, "개량 부품", ResourceItemKind.FinishedGood, ResourceIngredientTag.None, string.Empty, 1, false, false)
    };

    private static FacilitySpec F(string researchId, string name, string tag) =>
        new FacilitySpec(researchId, name, tag);

    private static InputSpec A(string itemId, int amount) =>
        new InputSpec(itemId, amount);

    private static ItemSpec S(
        string researchId,
        string itemId,
        string name,
        ResourceItemKind kind,
        ResourceIngredientTag tags,
        string workstationTag,
        int output,
        bool shared,
        bool craftable,
        params InputSpec[] inputs) =>
        new ItemSpec(
            researchId,
            itemId,
            name,
            kind,
            tags,
            workstationTag,
            output,
            shared,
            craftable,
            inputs);

    private static StockCategory CategoryFor(ResourceItemKind kind) => kind switch
    {
        ResourceItemKind.Food => StockCategory.Food,
        ResourceItemKind.Medicine => StockCategory.Medicine,
        ResourceItemKind.Ammunition => StockCategory.Ammunition,
        _ => StockCategory.General
    };

    private static string ItemPath(int index, ItemSpec spec) =>
        $"{ItemRoot}/V3I{index + 1:D2}_{Sanitize(spec.Name)}.asset";

    private static string RecipePath(int index, ItemSpec spec) =>
        $"{RecipeRoot}/V3R{index + 1:D2}_{Sanitize(spec.Name)}.asset";

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void DeleteStale<T>(string root, ISet<string> expected)
        where T : UnityEngine.Object
    {
        foreach (string path in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(path => !expected.Contains(path)))
        {
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string segment in path.Substring("Assets/".Length).Split('/'))
        {
            string next = $"{current}/{segment}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segment);
            }
            current = next;
        }
    }

    private static string Sanitize(string value) => string.Concat(
        (value ?? string.Empty).Select(character =>
            char.IsLetterOrDigit(character) ? character : '_'));
}
#endif
