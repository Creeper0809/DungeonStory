#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionWorkshopContentAssetBuilder
{
    public const int ExpectedWorkshopItemCount = 24;
    public const int ExpectedWorkshopRecipeCount = 24;
    public const int ExpectedSupportCount = 28;

    private const string ItemRoot =
        "Assets/Resources/SO/Economy/Items/Workshop";
    private const string RecipeRoot =
        "Assets/Resources/SO/Economy/Recipes/Workshop";
    private const string BuildingRoot =
        "Assets/Resources/SO/Building/ProductionSupport";

    private sealed class ItemSpec
    {
        public ItemSpec(
            string id,
            string name,
            ResourceItemKind kind,
            ResourceIngredientTag tags,
            int price,
            float weight,
            string researchId)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Tags = tags;
            Price = price;
            Weight = weight;
            ResearchId = researchId;
        }

        public string Id { get; }
        public string Name { get; }
        public ResourceItemKind Kind { get; }
        public ResourceIngredientTag Tags { get; }
        public int Price { get; }
        public float Weight { get; }
        public string ResearchId { get; }
    }

    private sealed class SupportSpec
    {
        public SupportSpec(
            int Id,
            string Code,
            string Name,
            string Feature,
            string[] Workstations,
            ProductionSupportKind Kind = ProductionSupportKind.Passive,
            int Capacity = 1,
            bool Power = false,
            float Water = 0f,
            float Wastewater = 0f,
            ProcessWastewaterComposition WastewaterKind =
                ProcessWastewaterComposition.None,
            bool ManualWater = false,
            bool Fuel = false)
        {
            this.Id = Id;
            this.Code = Code;
            this.Name = Name;
            this.Feature = Feature;
            this.Workstations = Workstations;
            this.Kind = Kind;
            this.Capacity = Capacity;
            this.Power = Power;
            this.Water = Water;
            this.Wastewater = Wastewater;
            this.WastewaterKind = WastewaterKind;
            this.ManualWater = ManualWater;
            this.Fuel = Fuel;
        }

        public int Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string Feature { get; }
        public string[] Workstations { get; }
        public ProductionSupportKind Kind { get; }
        public int Capacity { get; }
        public bool Power { get; }
        public float Water { get; }
        public float Wastewater { get; }
        public ProcessWastewaterComposition WastewaterKind { get; }
        public bool ManualWater { get; }
        public bool Fuel { get; }
    }

    [MenuItem("Tools/DungeonStory/Economy/Rebuild Production Workshops")]
    public static void Rebuild()
    {
        EnsureAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"Production workshops rebuilt: {ExpectedSupportCount} supports, "
            + $"{ExpectedWorkshopItemCount} intermediates/products and "
            + $"{ExpectedWorkshopRecipeCount} staged/work recipes.");
    }

    public static void EnsureAssets()
    {
        EnsureFolder(ItemRoot);
        EnsureFolder(RecipeRoot);
        EnsureFolder(BuildingRoot);
        BuildItems();
        BuildSupportBuildings();
        PatchWorkstations();
        PatchLegacyRecipes();
        BuildNewRecipes();
        V23RecipeProcessClassAuthoring.NormalizeRecipeWorkUnder(
            "Assets/Resources/SO/Economy/Recipes");
    }

    private static void BuildItems()
    {
        ItemSpec[] specs =
        {
            new("material:malt", "맥아", ResourceItemKind.Intermediate,
                ResourceIngredientTag.Plant, 5, 0.35f,
                "research:cuisine:fermentation"),
            new("material:grape-juice", "포도즙", ResourceItemKind.Intermediate,
                ResourceIngredientTag.Plant, 6, 0.375f,
                "research:cuisine:fermentation"),
            new("material:fermented-liquor", "발효액",
                ResourceItemKind.Intermediate, ResourceIngredientTag.Plant,
                9, 0.5f, "research:cuisine:fermentation"),
            new("material:young-wine", "어린 포도주",
                ResourceItemKind.Intermediate, ResourceIngredientTag.Plant,
                10, 0.35f, "research:cuisine:fermentation"),
            new("material:curd", "응유", ResourceItemKind.Intermediate,
                ResourceIngredientTag.Milk, 6, 0.45f,
                "research:cuisine:livestock"),
            new("material:brined-vegetable", "염지 채소",
                ResourceItemKind.Intermediate, ResourceIngredientTag.Plant,
                5, 0.5f, "research:survival:preservation"),
            new("food:twilight-beer", "황혼 맥주",
                ResourceItemKind.Substance, ResourceIngredientTag.Plant,
                13, 0.475f, "research:cuisine:fermentation"),
            new("food:night-spirit", "밤 증류주",
                ResourceItemKind.Substance, ResourceIngredientTag.Plant,
                24, 0.45f, "research:cuisine:distilling-aging"),
            new("food:fermented-pickle", "발효 절임",
                ResourceItemKind.Food, ResourceIngredientTag.Plant,
                9, 0.45f, "research:survival:preservation"),
            new("feed:silage", "사일리지", ResourceItemKind.FinishedGood,
                ResourceIngredientTag.Plant | ResourceIngredientTag.Fuel
                    | ResourceIngredientTag.Feed, 5, 0.23f,
                "research:husbandry:feed"),
            new("material:washed-vegetable", "세척 채소",
                ResourceItemKind.Intermediate, ResourceIngredientTag.Plant,
                4, 0.45f, "research:cuisine:kitchen-hygiene"),
            new("material:dough", "반죽", ResourceItemKind.Intermediate,
                ResourceIngredientTag.Plant | ResourceIngredientTag.Egg,
                6, 0.5f, "research:cuisine:baking"),
            new("material:seasoned-filling", "양념 속재료",
                ResourceItemKind.Intermediate,
                ResourceIngredientTag.Meat | ResourceIngredientTag.Plant,
                9, 0.65f, "research:cuisine:kitchen-hygiene"),
            new("material:salted-meat", "염지 고기",
                ResourceItemKind.Intermediate, ResourceIngredientTag.Meat,
                8, 0.65f, "research:survival:preservation"),
            new("material:ration-mixture", "배급식 혼합물",
                ResourceItemKind.Intermediate, ResourceIngredientTag.Plant,
                6, 0.55f, "research:cuisine:lavish"),
            new("food:malt-porridge", "맥아죽", ResourceItemKind.Food,
                ResourceIngredientTag.Plant, 6, 0.55f,
                "research:cuisine:crops"),
            new("food:grape-syrup", "포도 시럽", ResourceItemKind.Food,
                ResourceIngredientTag.Plant, 8, 0.175f,
                "research:cuisine:fermentation"),
            new("craft:fermented-vinegar", "발효 식초", ResourceItemKind.FinishedGood,
                ResourceIngredientTag.Plant, 9, 0.4f,
                "research:cuisine:fermentation"),
            new("food:fresh-curd", "신선 응유식", ResourceItemKind.Food,
                ResourceIngredientTag.Milk, 9, 0.225f,
                "research:cuisine:livestock"),
            new("food:vegetable-pie", "채소 파이", ResourceItemKind.Food,
                ResourceIngredientTag.Plant | ResourceIngredientTag.Egg, 12, 0.475f,
                "research:cuisine:baking"),
            new("food:stuffed-mushroom", "속 채운 버섯", ResourceItemKind.Food,
                ResourceIngredientTag.Meat | ResourceIngredientTag.Fungus, 13, 0.575f,
                "research:cuisine:kitchen-hygiene"),
            new("food:expedition-ration-pack", "원정 배급 꾸러미", ResourceItemKind.Food,
                ResourceIngredientTag.Meat | ResourceIngredientTag.Plant, 16, 0.75f,
                "research:survival:field-rations"),
            new("food:salted-meat-stew", "염장육 스튜", ResourceItemKind.Food,
                ResourceIngredientTag.Meat | ResourceIngredientTag.Plant, 14, 0.8f,
                "research:cuisine:livestock"),
            new("food:preserved-vegetable", "보존 채식", ResourceItemKind.Food,
                ResourceIngredientTag.Plant, 11, 0.55f,
                "research:cuisine:vegan")
        };

        AssetDatabase.DeleteAsset($"{ItemRoot}/{Sanitize("material:wort")}.asset");

        for (int index = 0; index < specs.Length; index++)
        {
            ItemSpec spec = specs[index];
            ResourceIngredientTag authoredTags = spec.Tags;
            if (spec.Id == "food:grape-syrup")
                authoredTags |= ResourceIngredientTag.Sweet;
            if (spec.Id == "material:brined-vegetable"
                || spec.Id == "food:fermented-pickle"
                || spec.Id == "material:salted-meat"
                || spec.Id == "food:salted-meat-stew")
            {
                authoredTags |= ResourceIngredientTag.Salted;
            }
            string assetPath = $"{ItemRoot}/{Sanitize(spec.Id)}.asset";
            ResourceItemDefinitionSO existing =
                AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(assetPath);
            int authoredUnitPrice = existing != null
                ? existing.UnitPrice
                : spec.Price;
            float authoredUnitWeight = existing != null
                ? existing.UnitWeight
                : spec.Weight;
            ResourceItemDefinitionSO asset =
                GetOrCreate<ResourceItemDefinitionSO>(assetPath);
            void ConfigureItem(ResourceItemDefinitionSO target)
            {
                target.id = 8300 + index;
                target.Configure(
                    spec.Id,
                    spec.Name,
                    "작업실의 다음 공정으로 실제 운반되는 물리 중간재.",
                    StockCategory.Food,
                    spec.Kind,
                    authoredTags,
                    authoredUnitPrice,
                    authoredUnitWeight,
                    50,
                    spec.ResearchId);
                if (spec.Kind == ResourceItemKind.Food)
                {
                    (MealQualityTier quality, MealQualityBand qualityBand,
                        MealServingRole servingRole, float nutrition, float mood,
                        float freshness, bool preserved) = ResolveMeal(spec.Id);
                    target.ConfigureMeal(
                        quality,
                        nutrition,
                        mood,
                        freshness,
                        preserved,
                        qualityBand,
                        servingRole);
                }
                if (spec.Id == "food:twilight-beer")
                {
                    target.ConfigureSubstance(
                        "substance:twilight-beer",
                        SubstanceUseClass.Recreational,
                        0.025f,
                        0.015f,
                        0.05f,
                        0.008f,
                        5f,
                        -0.02f,
                        -0.02f,
                        180f);
                }
                else if (spec.Id == "food:night-spirit")
                {
                    target.ConfigureSubstance(
                        "substance:night-spirit",
                        SubstanceUseClass.Recreational,
                        0.07f,
                        0.045f,
                        0.12f,
                        0.025f,
                        9f,
                        -0.08f,
                        -0.06f,
                        240f);
                }
                else
                {
                    target.ClearSubstance();
                }
                target.ConfigureFacilitySupply(
                    spec.Id == "feed:silage" ? 4f : 0f,
                    spec.Id == "feed:silage" ? 14f : 0f,
                    spec.Id == "feed:silage",
                    spec.Kind == ResourceItemKind.Intermediate);
            }

            if (!WouldChange(asset, ConfigureItem))
                continue;
            ConfigureItem(asset);
            EditorUtility.SetDirty(asset);
        }
    }

    private static (
        MealQualityTier quality,
        MealQualityBand qualityBand,
        MealServingRole servingRole,
        float nutrition,
        float mood,
        float freshness,
        bool preserved) ResolveMeal(string itemId) => itemId switch
    {
        "food:malt-porridge" => (MealQualityTier.Simple, MealQualityBand.Simple,
            MealServingRole.FullMeal, 40f, 2f, 360f, false),
        "food:grape-syrup" => (MealQualityTier.Fine, MealQualityBand.Fine,
            MealServingRole.Snack, 15f, 3f, 900f, true),
        "food:fermented-pickle" => (MealQualityTier.Preserved, MealQualityBand.Simple,
            MealServingRole.Snack, 15f, 2f, 1440f, true),
        "food:fresh-curd" => (MealQualityTier.Fine, MealQualityBand.Decent,
            MealServingRole.LightMeal, 25f, 3f, 240f, false),
        "food:vegetable-pie" => (MealQualityTier.Fine, MealQualityBand.Decent,
            MealServingRole.FullMeal, 50f, 5f, 450f, false),
        "food:stuffed-mushroom" => (MealQualityTier.Fine, MealQualityBand.Decent,
            MealServingRole.FullMeal, 50f, 5f, 360f, false),
        "food:expedition-ration-pack" => (MealQualityTier.Preserved, MealQualityBand.Decent,
            MealServingRole.FieldRation, 50f, 1f, 1800f, true),
        "food:salted-meat-stew" => (MealQualityTier.Fine, MealQualityBand.Fine,
            MealServingRole.FullMeal, 55f, 6f, 720f, true),
        "food:preserved-vegetable" => (MealQualityTier.Preserved, MealQualityBand.Simple,
            MealServingRole.FullMeal, 40f, 2f, 1440f, true),
        _ => throw new InvalidOperationException(
            $"Missing authored workshop meal profile for '{itemId}'.")
    };

    private static void BuildSupportBuildings()
    {
        SupportSpec[] specs =
        {
            new(1600, "WS01", "미세 체 선반", "support:fine-sieve",
                W("workstation:mill")),
            new(1601, "WS02", "담금·당화조", "support:mash-tun",
                W("workstation:brewery"), Water: 0.25f, ManualWater: true),
            new(1602, "WS03", "수동 발효조", "support:fermenter",
                W("workstation:brewery", "workstation:feedbench"),
                ProductionSupportKind.BatchProcessor),
            new(1603, "WS04", "온도 제어 발효조", "support:fermenter",
                W("workstation:brewery", "workstation:feedbench"),
                ProductionSupportKind.BatchProcessor, 1, true, 0.1f, 0.1f,
                ProcessWastewaterComposition.FermentationEffluent),
            new(1604, "WS05", "숙성 오크통", "support:aging-barrel",
                W("workstation:brewery"),
                ProductionSupportKind.BatchProcessor),
            new(1605, "WS06", "세척·병입대", "support:bottling",
                W("workstation:brewery"), Power: true,
                Water: 0.25f, Wastewater: 0.25f,
                WastewaterKind: ProcessWastewaterComposition.SanitaryWashwater),
            new(1606, "WS07", "분별 증류탑", "support:fractional-still",
                W("workstation:distillery"), Power: true,
                Water: 0.5f, Wastewater: 0.4f,
                WastewaterKind: ProcessWastewaterComposition.IndustrialEffluent),
            new(1607, "WS08", "화덕·가마솥", "support:hearth",
                W("workstation:cookbench", "workstation:kitchen-basic"),
                Fuel: true),
            new(1608, "WS09", "벽돌 오븐", "support:oven",
                W("workstation:cookbench"), Fuel: true),
            new(1609, "WS10", "전기 오븐", "support:oven",
                W("workstation:cookbench"), Power: true),
            new(1610, "WS11", "세척·전처리 싱크", "support:prep-sink",
                W("workstation:cookbench"), Water: 0.25f,
                Wastewater: 0.25f,
                WastewaterKind: ProcessWastewaterComposition.FoodProcessWashwater),
            new(1611, "WS12", "냉장 준비대", "support:cold-prep",
                W("workstation:cookbench"), Power: true),
            new(1612, "WS13", "향신료 선반", "support:spice-rack",
                W("workstation:cookbench")),
            new(1613, "WS14", "염장·절임조", "support:pickling-vat",
                W("workstation:cookbench"),
                ProductionSupportKind.BatchProcessor, 1, false, 0.2f, 0.2f,
                ProcessWastewaterComposition.Brine),
            new(1614, "WS15", "치즈 응고조", "support:cheese-vat",
                W("workstation:cookbench"), Water: 0.2f, Wastewater: 0.2f,
                WastewaterKind: ProcessWastewaterComposition.SanitaryWashwater),
            new(1615, "WS16", "치즈 숙성 선반", "support:cheese-rack",
                W("workstation:cookbench"),
                ProductionSupportKind.BatchProcessor),
            new(1616, "WS17", "영양 배합 저울", "support:feed-scale",
                W("workstation:feedbench")),
            new(1617, "WS18", "연기 포집 후드", "support:smoke-hood",
                W("workstation:smoker"), Power: true),
            new(1618, "WS19", "목재 처리조", "support:wood-treatment",
                W("workstation:sawmill"), Water: 0.15f, Wastewater: 0.1f,
                WastewaterKind: ProcessWastewaterComposition.IndustrialEffluent),
            new(1619, "WS20", "정밀 연마기", "support:precision-grinder",
                W("workstation:stonecutter"), Power: true),
            new(1620, "WS21", "도가니 선반", "support:crucible-rack",
                W("workstation:furnace", "workstation:steelworks")),
            new(1621, "WS22", "세공 도구함", "support:jeweler-tools",
                W("workstation:jeweler")),
            new(1622, "WS23", "마나 안정기", "support:mana-stabilizer",
                W("workstation:arcane-forge"), Power: true),
            new(1623, "WS24", "직조 보조 선반", "support:weaving-rack",
                W("workstation:loom", "workstation:arcane-loom")),
            new(1624, "WS25", "무균 약품 보관함", "support:sterile-cabinet",
                W("workstation:apothecary"), Power: true),
            new(1625, "WS26", "마나 응축기", "support:mana-condenser",
                W("workstation:alchemy"), Power: true),
            new(1626, "WS27", "대장 도구함", "support:smith-tools",
                W("workstation:forge")),
            new(1627, "WS28", "실내 생장 제어기", "support:growth-control",
                W("workstation:hydroponics"), Power: true,
                Water: 0.2f, Wastewater: 0.05f,
                WastewaterKind: ProcessWastewaterComposition.AgriculturalRunoff)
        };

        foreach (SupportSpec spec in specs)
        {
            BuildingSO asset = GetOrCreate<BuildingSO>(
                $"{BuildingRoot}/{spec.Code}_{Sanitize(spec.Name)}.asset");
            if (!WouldChange(
                    asset,
                    target => ConfigureSupportBuilding(target, spec)))
            {
                continue;
            }
            ConfigureSupportBuilding(asset, spec);
            EditorUtility.SetDirty(asset);
        }
    }

    private static void ConfigureSupportBuilding(
        BuildingSO asset,
        SupportSpec spec)
    {
        BuildingWorkAmountAbility approvedWorkAmount =
            asset.GetAbility<BuildingWorkAmountAbility>();
        asset.id = spec.Id;
        asset.objectName = spec.Name;
        asset.width = 1;
        asset.height = 1;
        asset.layer = GridLayer.Building;
        asset.category = BuildingCategory.Production;
        asset.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
        asset.horizontalDraggable = false;
        asset.verticalDraggable = false;
        asset.unlocked = false;
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingFacilityPartAbility { code = spec.Code });
        int constructionValue = spec.Power ? 60 : 40;
        abilities.Add(new BuildingEconomyAbility
        {
            constructionValue = constructionValue,
            maintenance = spec.Power ? 2 : 1,
            unlockPhase = spec.Power ? 3 : 2,
            demolitionRefundRate = 0.5f
        });
        BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
        {
            constructionWorkRequired = 18f + constructionValue * 0.02f,
            repairWorkRequired = 10f,
            cleanWorkRequired = 6.25f,
            researchWorkRequired = 6f,
            operateWorkRequired = 10f
        };
        workAmount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition(
                spec.Power ? "component:machine-parts" : "material:lumber",
                Mathf.Max(1, Mathf.CeilToInt(constructionValue * 0.05f)))
        });
        abilities.Add(approvedWorkAmount ?? workAmount);
        abilities.Add(new BuildingSemanticTagsAbility
        {
            tags = new[] { "production-support", spec.Feature }
        });
        abilities.Add(new BuildingProductionSupportAbility
        {
            supportId = $"production-support:{spec.Code.ToLowerInvariant()}",
            featureTags = new[] { spec.Feature },
            compatibleWorkstationTags = spec.Workstations,
            kind = spec.Kind,
            batchCapacity = spec.Capacity,
            requiresPower = spec.Power,
            cleanWaterPerCycle = spec.Water,
            wastewaterPerCycle = spec.Wastewater,
            wastewaterComposition = spec.WastewaterKind,
            allowsManualWaterFallback = spec.ManualWater,
            requiresFuel = spec.Fuel,
            fuelItemId = "resource:log",
            fuelPerCycle = 1
        });
        if (spec.Fuel)
        {
            abilities.Add(new BuildingFacilitySupplyAbility
            {
                profiles = new List<FacilitySupplyProfile>
                {
                    new FacilitySupplyProfile
                    {
                        kind = FacilitySupplyKind.Fuel,
                        requiredTags = ResourceIngredientTag.None,
                        minimumValue = 2f,
                        bufferCapacity = 24,
                        allowedItemIds = new List<string>
                        {
                            "resource:log", "material:low-fuel", "resource:coal",
                            "material:charcoal", "feed:silage", "craft:candle"
                        },
                        forbiddenItemIds = new List<string>
                        {
                            "material:black-powder"
                        },
                        priorityItemIds = new List<string>
                        {
                            "resource:log", "material:low-fuel", "resource:coal",
                            "material:charcoal", "feed:silage", "craft:candle"
                        }
                    }
                }
            });
            abilities.Add(new BuildingFuelConsumerAbility
            {
                fuelPerRefuel = 1,
                workSeconds = 0.8f,
                warmth = 8f,
                lightSafety = 4f
            });
        }
        abilities.EnsureStableIds();
        asset.ReplaceAbilities(abilities);
    }

    private static void PatchWorkstations()
    {
        foreach (BuildingSO building in LoadAll<BuildingSO>(
                     "Assets/Resources/SO/Building"))
        {
            string code = building
                .GetAbility<BuildingFacilityPartAbility>()?.code;
            string tag = WorkstationTagForCode(code);
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            void ConfigureWorkstation(BuildingSO target)
            {
                BuildingProductionWorkstationAbility workstation =
                    target.GetAbility<BuildingProductionWorkstationAbility>();
                if (workstation == null)
                {
                    workstation = new BuildingProductionWorkstationAbility();
                    target.AbilityModules.Add(workstation);
                }
                workstation.workstationTag = tag;
                workstation.stockSensorInstallationItemId =
                    "component:stock-sensor-panel";

                BuildingProductionBufferAbility buffer =
                    target.GetAbility<BuildingProductionBufferAbility>();
                if (buffer == null)
                {
                    buffer = new BuildingProductionBufferAbility();
                    target.AbilityModules.Add(buffer);
                }
                buffer.defaultBatchCapacity = 4;
                buffer.physicalOutputBufferCycleCapacity = 4;
                buffer.outputCapacities = new List<ProductionItemBufferCapacity>
                {
                    new() { itemId = "material:lead-ingot", capacity = 24 },
                    new() { itemId = "material:lead-shot", capacity = 96 },
                    new() { itemId = "material:black-powder", capacity = 48 },
                    new() { itemId = "material:paper", capacity = 64 },
                    new() { itemId = "ammo:paper-cartridge", capacity = 120 },
                    new() { itemId = "component:machine-parts", capacity = 16 },
                    new() { itemId = "component:precision-parts", capacity = 16 }
                };
                target.AbilityModules.EnsureStableIds();
            }

            if (!WouldChange(building, ConfigureWorkstation))
                continue;
            ConfigureWorkstation(building);
            EditorUtility.SetDirty(building);
        }
    }

    private static void PatchLegacyRecipes()
    {
        foreach (ProductionRecipeSO recipe in LoadAll<ProductionRecipeSO>(
                     "Assets/Resources/SO/Economy/Recipes"))
        {
            string workstation = WorkstationTagForFacility(recipe.FacilityTag);
            if (string.IsNullOrWhiteSpace(workstation))
            {
                continue;
            }

            string workType = recipe.FacilityTag is "brewery" or "smoker"
                or "feedbench"
                    ? "work:craft"
                    : recipe.WorkTypeId.Value;
            void ConfigureRecipe(ProductionRecipeSO target)
            {
                float approvedWork = target.RequiredWork;
                target.Configure(
                    target.RecipeId,
                    target.DisplayName,
                    target.Description,
                    target.FacilityTag,
                    workType,
                    target.RequiredResearchId,
                    approvedWork,
                    target.Inputs,
                    target.Outputs);
                target.ConfigureWorkshop(
                    workstation,
                    Array.Empty<string>(),
                    ProductionProcessKind.WorkOnly);
                ApplyRecipeBalanceAuthority(target, approvedWork);
            }

            if (!WouldChange(recipe, ConfigureRecipe))
                continue;
            ConfigureRecipe(recipe);
            EditorUtility.SetDirty(recipe);
        }

        ReconfigureExisting(
            "recipe:alcohol", "기초 알코올", "distillery", "work:craft",
            "research:cuisine:distilling-aging", 10f,
            A("material:fermented-liquor", 2),
            O("material:alcohol", 2),
            W("support:fractional-still"));
        ReconfigureExistingBatch(
            "recipe:cheese", "치즈 숙성", "cookbench", "work:cook",
            "research:cuisine:livestock", 4f, 2f, 16f,
            A("material:curd", 2), O("material:cheese", 2),
            "support:cheese-rack");
        ReconfigureExistingBatch(
            "recipe:night-wine", "밤포도주 숙성", "brewery", "work:craft",
            "research:cuisine:fermentation", 3f, 2f, 18f,
            A("material:young-wine", 2), O("drug:night-wine", 2),
            "support:aging-barrel");
        ReconfigureExisting(
            "recipe:jerky", "육포 훈연", "smoker", "work:craft",
            "research:survival:preservation", 8f,
            A("material:salted-meat", 2), O("food:jerky", 2),
            W("support:smoke-hood"));
        ReconfigureExisting(
            "recipe:meat-pie", "고기 파이 굽기", "cookbench", "work:cook",
            "research:cuisine:baking", 10f,
            new[]
            {
                A("material:dough", 1),
                A("material:seasoned-filling", 1)
            },
            O("food:meat-pie", 2),
            W("support:oven"));
        ReconfigureExisting(
            "recipe:preserved-ration", "보존 배급식 훈연", "smoker",
            "work:craft", "research:cuisine:lavish", 8f,
            A("material:ration-mixture", 2),
            O("food:preserved-ration", 3),
            W("support:smoke-hood"));
        ConfigureExistingRequirements(
            "recipe:grain-porridge",
            W("support:hearth"), 0.25f, 0.1f, true);
        ConfigureExistingRequirements(
            "recipe:root-stew",
            W("support:hearth"), 0.25f, 0.1f, true);
        ConfigureExistingRequirements(
            "recipe:mushroom-soup",
            W("support:hearth"), 0.25f, 0.1f, true);
        ConfigureExistingRequirements(
            "recipe:moonflower-tea",
            W("support:hearth"), 0.25f, 0.1f, true);
        ConfigureExistingRequirements(
            "recipe:roasted-meat",
            W("support:hearth"));
        ConfigureExistingRequirements(
            "recipe:garden-meal",
            W("support:prep-sink", "support:hearth"),
            0.25f, 0.2f);
        ConfigureExistingRequirements(
            "recipe:egg-pancake",
            W("support:prep-sink", "support:hearth"),
            0.2f, 0.15f);
        ConfigureExistingRequirements(
            "recipe:boar-stew",
            W("support:prep-sink", "support:hearth"),
            0.25f, 0.2f);
        ConfigureExistingRequirements(
            "recipe:lavish-vegan",
            W(
                "support:prep-sink",
                "support:cold-prep",
                "support:spice-rack",
                "support:hearth"),
            0.3f, 0.25f);
        ConfigureExistingRequirements(
            "recipe:lavish-meat",
            W(
                "support:prep-sink",
                "support:cold-prep",
                "support:spice-rack",
                "support:hearth"),
            0.3f, 0.25f);
        MoveRecipeToWorkstation("recipe:soap", "apothecary", "work:craft");
        MoveRecipeToWorkstation("recipe:candle", "apothecary", "work:craft");
    }

    private static void BuildNewRecipes()
    {
        AssetDatabase.DeleteAsset($"{RecipeRoot}/{Sanitize("recipe:wort")}.asset");
        CreateRecipe("recipe:malt", "맥아 만들기", "mill", "work:craft",
            "research:cuisine:milling", 6f,
            A("resource:twilight-grain", 2), O("material:malt", 2),
            W("support:fine-sieve"));
        CreateBatch("recipe:twilight-beer", "황혼 맥주 발효", "brewery",
            "research:cuisine:fermentation", 3f, 2f, 12f,
            A("material:fermented-liquor", 2), O("food:twilight-beer", 2),
            "support:fermenter");
        CreateRecipe("recipe:grape-juice", "밤포도 착즙", "brewery",
            "work:craft", "research:cuisine:fermentation", 6f,
            A("resource:night-grape", 3), O("material:grape-juice", 2),
            W("support:mash-tun"), 0.1f, 0.05f, true);
        CreateBatch("recipe:young-wine", "어린 포도주 발효", "brewery",
            "research:cuisine:fermentation", 3f, 2f, 18f,
            A("material:grape-juice", 2), O("material:young-wine", 2),
            "support:fermenter");
        CreateBatch("recipe:fermented-liquor", "증류용 발효액", "brewery",
            "research:cuisine:fermentation", 3f, 1f, 12f,
            A("material:malt", 2), O("material:fermented-liquor", 2),
            "support:fermenter", 0.7f);
        CreateBatch("recipe:night-spirit", "밤 증류주 오크 숙성", "brewery",
            "research:cuisine:distilling-aging", 3f, 2f, 12f,
            new[] { A("material:young-wine", 2), A("material:syrup", 1) },
            O("food:night-spirit", 2), "support:aging-barrel");
        CreateRecipe("recipe:curd", "응유 만들기", "cookbench", "work:cook",
            "research:cuisine:livestock", 6f,
            new[]
            {
                A("resource:milk", 3),
                A("resource:saltstone", 1)
            },
            O("material:curd", 2), W("support:cheese-vat"),
            0.2f, 4.2f, true);
        CreateRecipe("recipe:brined-vegetable", "채소 염지", "cookbench",
            "work:cook", "research:survival:preservation", 5f,
            new[]
            {
                A("material:washed-vegetable", 2),
                A("resource:saltstone", 1)
            },
            O("material:brined-vegetable", 2),
            W("support:pickling-vat"), 0.2f, 1f, true);
        CreateBatch("recipe:fermented-pickle", "발효 절임", "cookbench",
            "research:survival:preservation", 2f, 1f, 12f,
            new[]
            {
                A("material:brined-vegetable", 2),
                A("craft:fermented-vinegar", 1)
            },
            O("food:fermented-pickle", 2), "support:pickling-vat",
            0f, 1f);
        CreateBatch("recipe:silage", "사일리지 발효", "feedbench",
            "research:husbandry:feed", 3f, 1f, 12f,
            new[]
            {
                A("resource:grass-straw", 3),
                A("resource:twilight-grain", 1)
            },
            O("feed:silage", 3), "support:fermenter",
            0.2f, 0f, true);
        CreateRecipe("recipe:washed-vegetable", "채소 세척", "cookbench",
            "work:cook", "research:cuisine:kitchen-hygiene", 4f,
            A("resource:ember-root", 2),
            O("material:washed-vegetable", 2),
            W("support:prep-sink"), 0.25f, 0.25f);
        CreateRecipe("recipe:dough", "반죽 치대기", "cookbench",
            "work:cook", "research:cuisine:baking", 5f,
            new[]
            {
                A("material:flour", 2),
                A("resource:egg", 1)
            },
            O("material:dough", 2),
            W("support:prep-sink"), 0.15f, 0.1f, true);
        CreateRecipe("recipe:seasoned-filling", "양념 속재료", "cookbench",
            "work:cook", "research:cuisine:kitchen-hygiene", 6f,
            new[]
            {
                A("resource:meat", 2),
                A("material:washed-vegetable", 1)
            },
            O("material:seasoned-filling", 2),
            W("support:cold-prep", "support:spice-rack"));
        CreateRecipe("recipe:salted-meat", "고기 염지", "cookbench",
            "work:cook", "research:survival:preservation", 5f,
            new[]
            {
                A("resource:meat", 3),
                A("resource:saltstone", 1)
            },
            O("material:salted-meat", 2),
            W("support:prep-sink"), 0.1f, 0.1f, true);
        CreateRecipe("recipe:ration-mixture", "배급식 혼합", "cookbench",
            "work:cook", "research:cuisine:lavish", 5f,
            new[]
            {
                A("material:flour", 2),
                A("resource:saltstone", 1)
            },
            O("material:ration-mixture", 2),
            W("support:prep-sink"), 0.1f, 0.1f, true);
        CreateRecipe("recipe:malt-porridge", "맥아죽", "cookbench", "work:cook",
            "research:cuisine:crops", 5f,
            A("material:malt", 1), O("food:malt-porridge", 2),
            W("support:hearth"), 1.5f);
        CreateRecipe("recipe:grape-syrup", "포도 시럽", "cookbench", "work:cook",
            "research:cuisine:fermentation", 5f,
            A("material:grape-juice", 1), O("food:grape-syrup", 2),
            W("support:hearth"));
        CreateRecipe("recipe:fermented-vinegar", "발효 식초", "brewery", "work:craft",
            "research:cuisine:fermentation", 6f,
            A("material:fermented-liquor", 1), O("craft:fermented-vinegar", 2),
            W("support:fermenter"), 0.7f);
        CreateRecipe("recipe:fresh-curd", "신선 응유식", "cookbench", "work:cook",
            "research:cuisine:livestock", 5f,
            A("material:curd", 1), O("food:fresh-curd", 2),
            W("support:cheese-vat"));
        CreateRecipe("recipe:vegetable-pie", "채소 파이", "cookbench", "work:cook",
            "research:cuisine:baking", 8f,
            new[] { A("material:dough", 1), A("material:washed-vegetable", 1) },
            O("food:vegetable-pie", 2), W("support:oven"));
        CreateRecipe("recipe:stuffed-mushroom", "속 채운 버섯", "cookbench", "work:cook",
            "research:cuisine:kitchen-hygiene", 8f,
            new[] { A("material:seasoned-filling", 1), A("resource:cave-mushroom", 2) },
            O("food:stuffed-mushroom", 2), W("support:hearth"));
        CreateRecipe("recipe:expedition-ration-pack", "원정 배급 꾸러미", "smoker", "work:craft",
            "research:survival:field-rations", 8f,
            new[] { A("material:ration-mixture", 1), A("material:salted-meat", 1) },
            O("food:expedition-ration-pack", 2), W("support:smoke-hood"));
        CreateRecipe("recipe:salted-meat-stew", "염장육 스튜", "cookbench", "work:cook",
            "research:cuisine:livestock", 7f,
            new[] { A("material:salted-meat", 1), A("resource:ember-root", 1) },
            O("food:salted-meat-stew", 2), W("support:hearth"));
        CreateRecipe("recipe:preserved-vegetable", "보존 채식", "cookbench", "work:cook",
            "research:cuisine:vegan", 7f,
            new[]
            {
                A("material:brined-vegetable", 1),
                A("material:washed-vegetable", 1),
                A("craft:fermented-vinegar", 1)
            },
            O("food:preserved-vegetable", 2), W("support:hearth"));
    }

    private static void CreateBatch(
        string id,
        string name,
        string facility,
        string research,
        float prepare,
        float finish,
        float hours,
        ItemAmountDefinition input,
        ProductionOutputDefinition output,
        string batchSupport,
        float water = 0f,
        float wastewater = 0f,
        bool manualWater = false)
    {
        CreateBatch(
            id, name, facility, research, prepare, finish, hours,
            new[] { input }, output, batchSupport,
            water, wastewater, manualWater);
    }

    private static void CreateBatch(
        string id,
        string name,
        string facility,
        string research,
        float prepare,
        float finish,
        float hours,
        IEnumerable<ItemAmountDefinition> inputs,
        ProductionOutputDefinition output,
        string batchSupport,
        float water = 0f,
        float wastewater = 0f,
        bool manualWater = false)
    {
        string assetPath = $"{RecipeRoot}/{Sanitize(id)}.asset";
        ProductionRecipeSO existing =
            AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(assetPath);
        float? preservedRequiredWork = existing != null
            ? existing.RequiredWork
            : null;
        ProductionRecipeSO recipe = GetOrCreate<ProductionRecipeSO>(assetPath);
        recipe.id = ResolveWorkshopRecipeNumericId(id);
        recipe.Configure(
            id, name, "준비와 마감 사이에 게임 시간으로 처리되는 배치 공정.",
            facility, "work:craft", research, prepare, inputs,
            new[] { output });
        recipe.ConfigureWorkshop(
            WorkstationTagForFacility(facility),
            new[] { batchSupport },
            ProductionProcessKind.PassiveBatch,
            batchSupport,
            prepare,
            finish,
            hours,
            12f,
            24f,
            4f,
            32f,
            water,
            wastewater,
            manualWater,
            ResolveSpoilage(inputs),
            ResolveRecipeWastewaterComposition(id));
        ApplyRecipeBalanceAuthority(recipe, preservedRequiredWork);
        EditorUtility.SetDirty(recipe);
    }

    private static void CreateRecipe(
        string id,
        string name,
        string facility,
        string workType,
        string research,
        float work,
        ItemAmountDefinition input,
        ProductionOutputDefinition output,
        string[] supports,
        float water = 0f,
        float wastewater = 0f,
        bool manualWater = false)
    {
        CreateRecipe(
            id, name, facility, workType, research, work,
            new[] { input }, output, supports,
            water, wastewater, manualWater);
    }

    private static void CreateRecipe(
        string id,
        string name,
        string facility,
        string workType,
        string research,
        float work,
        IEnumerable<ItemAmountDefinition> inputs,
        ProductionOutputDefinition output,
        string[] supports,
        float water = 0f,
        float wastewater = 0f,
        bool manualWater = false)
    {
        string assetPath = $"{RecipeRoot}/{Sanitize(id)}.asset";
        ProductionRecipeSO existing =
            AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(assetPath);
        float? preservedRequiredWork = existing != null
            ? existing.RequiredWork
            : null;
        ProductionRecipeSO recipe = GetOrCreate<ProductionRecipeSO>(assetPath);
        recipe.id = ResolveWorkshopRecipeNumericId(id);
        recipe.Configure(
            id, name, "같은 방의 연결 시설을 사용하는 수동 생산 단계.",
            facility, workType, research, work, inputs, new[] { output });
        recipe.ConfigureWorkshop(
            WorkstationTagForFacility(facility),
            supports,
            ProductionProcessKind.WorkOnly,
            cleanWater: water,
            wastewater: wastewater,
            allowManualWater: manualWater,
            wastewaterKind: ResolveRecipeWastewaterComposition(id));
        ApplyRecipeBalanceAuthority(recipe, preservedRequiredWork);
        EditorUtility.SetDirty(recipe);
    }

    private static int ResolveWorkshopRecipeNumericId(string recipeId) =>
        recipeId switch
        {
            "recipe:malt" => 9900,
            "recipe:twilight-beer" => 9902,
            "recipe:grape-juice" => 9903,
            "recipe:young-wine" => 9904,
            "recipe:fermented-liquor" => 9905,
            "recipe:night-spirit" => 9906,
            "recipe:curd" => 9907,
            "recipe:brined-vegetable" => 9908,
            "recipe:fermented-pickle" => 9909,
            "recipe:silage" => 9910,
            "recipe:washed-vegetable" => 9911,
            "recipe:dough" => 9912,
            "recipe:seasoned-filling" => 9913,
            "recipe:salted-meat" => 9914,
            "recipe:ration-mixture" => 9915,
            "recipe:malt-porridge" => 9916,
            "recipe:grape-syrup" => 9917,
            "recipe:fermented-vinegar" => 9918,
            "recipe:fresh-curd" => 9919,
            "recipe:vegetable-pie" => 9920,
            "recipe:stuffed-mushroom" => 9921,
            "recipe:expedition-ration-pack" => 9922,
            "recipe:salted-meat-stew" => 9923,
            "recipe:preserved-vegetable" => 9924,
            _ => throw new ArgumentOutOfRangeException(
                nameof(recipeId),
                recipeId,
                "Unknown workshop recipe id.")
        };

    private static void ReconfigureExisting(
        string id,
        string name,
        string facility,
        string workType,
        string research,
        float work,
        ItemAmountDefinition input,
        ProductionOutputDefinition output,
        string[] supports)
    {
        ReconfigureExisting(
            id, name, facility, workType, research, work,
            new[] { input }, output, supports);
    }

    private static void ReconfigureExisting(
        string id,
        string name,
        string facility,
        string workType,
        string research,
        float work,
        IEnumerable<ItemAmountDefinition> inputs,
        ProductionOutputDefinition output,
        string[] supports)
    {
        ProductionRecipeSO recipe = FindRecipe(id);
        if (recipe == null)
        {
            return;
        }

        float preservedRequiredWork = recipe.RequiredWork;
        recipe.Configure(
            id, name, recipe.Description, facility, workType,
            research, work, inputs, new[] { output });
        recipe.ConfigureWorkshop(
            WorkstationTagForFacility(facility),
            supports,
            ProductionProcessKind.WorkOnly);
        ApplyRecipeBalanceAuthority(recipe, preservedRequiredWork);
        EditorUtility.SetDirty(recipe);
    }

    private static void ReconfigureExistingBatch(
        string id,
        string name,
        string facility,
        string workType,
        string research,
        float prepare,
        float finish,
        float hours,
        ItemAmountDefinition input,
        ProductionOutputDefinition output,
        string batchSupport)
    {
        ProductionRecipeSO recipe = FindRecipe(id);
        if (recipe == null)
        {
            return;
        }

        float preservedRequiredWork = recipe.RequiredWork;
        recipe.Configure(
            id, name, recipe.Description, facility, workType,
            research, prepare, new[] { input }, new[] { output });
        recipe.ConfigureWorkshop(
            WorkstationTagForFacility(facility),
            new[] { batchSupport },
            ProductionProcessKind.PassiveBatch,
            batchSupport,
            prepare,
            finish,
            hours,
            failedBatchItemId: ResolveSpoilage(new[] { input }));
        ApplyRecipeBalanceAuthority(recipe, preservedRequiredWork);
        EditorUtility.SetDirty(recipe);
    }

    private static void MoveRecipeToWorkstation(
        string id,
        string facility,
        string workType)
    {
        ProductionRecipeSO recipe = FindRecipe(id);
        if (recipe == null)
        {
            return;
        }

        recipe.Configure(
            recipe.RecipeId,
            recipe.DisplayName,
            recipe.Description,
            facility,
            workType,
            recipe.RequiredResearchId,
            recipe.RequiredWork,
            recipe.Inputs,
            recipe.Outputs);
        recipe.ConfigureWorkshop(
            WorkstationTagForFacility(facility),
            Array.Empty<string>(),
            ProductionProcessKind.WorkOnly);
        ApplyRecipeBalanceAuthority(recipe, recipe.RequiredWork);
        EditorUtility.SetDirty(recipe);
    }

    private static void ConfigureExistingRequirements(
        string id,
        string[] supports,
        float water = 0f,
        float wastewater = 0f,
        bool manualWater = false)
    {
        ProductionRecipeSO recipe = FindRecipe(id);
        if (recipe == null)
        {
            return;
        }

        recipe.ConfigureWorkshop(
            recipe.WorkstationTag,
            supports,
            ProductionProcessKind.WorkOnly,
            cleanWater: water,
            wastewater: wastewater,
            allowManualWater: manualWater,
            wastewaterKind: ResolveRecipeWastewaterComposition(id));
        ApplyRecipeBalanceAuthority(recipe, recipe.RequiredWork);
        EditorUtility.SetDirty(recipe);
    }

    private static void ApplyRecipeBalanceAuthority(
        ProductionRecipeSO recipe,
        float? preservedRequiredWork = null)
    {
        ProductionFlowRole flowRole = recipe.Inputs.Count == 0
            ? ProductionFlowRole.Source
            : recipe.Outputs.Count == 0
                ? ProductionFlowRole.Sink
                : ProductionFlowRole.Transform;
        recipe.ConfigureFlowRole(flowRole);
        ProductionProcessClass processClass =
            V23RecipeProcessClassAuthoring.Resolve(
                recipe.WorkstationTag,
                recipe.WorkTypeId.Value,
                flowRole);
        recipe.ConfigureProcessClass(processClass);
        recipe.ConfigureBalanceWork(
            preservedRequiredWork
            ?? V23BalanceWorkCalculator.CalculateRecipeBaseWork(
                recipe,
                processClass));
    }

    private static ProcessWastewaterComposition
        ResolveRecipeWastewaterComposition(string recipeId) => recipeId switch
        {
            "recipe:curd" => ProcessWastewaterComposition.Whey,
            "recipe:brined-vegetable" => ProcessWastewaterComposition.Brine,
            "recipe:fermented-pickle" => ProcessWastewaterComposition.Brine,
            "recipe:boar-stew" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:lavish-vegan" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:lavish-meat" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:root-stew" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:mushroom-soup" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:grain-porridge" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:moonflower-tea" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:garden-meal" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:egg-pancake" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:washed-vegetable" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:salted-meat" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:ration-mixture" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:grape-juice" => ProcessWastewaterComposition.FoodProcessWashwater,
            "recipe:dough" => ProcessWastewaterComposition.FoodProcessWashwater,
            _ => ProcessWastewaterComposition.None
        };

    private static ProductionRecipeSO FindRecipe(string id)
    {
        return LoadAll<ProductionRecipeSO>(
                "Assets/Resources/SO/Economy/Recipes")
            .FirstOrDefault(recipe => string.Equals(
                recipe.RecipeId, id, StringComparison.Ordinal));
    }

    private static string ResolveSpoilage(
        IEnumerable<ItemAmountDefinition> inputs)
    {
        ResourceIngredientTag tags = ResourceIngredientTag.None;
        Dictionary<string, ResourceItemDefinitionSO> items =
            LoadAll<ResourceItemDefinitionSO>(
                    "Assets/Resources/SO/Economy/Items")
                .GroupBy(item => item.ItemId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
        foreach (ItemAmountDefinition input in inputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (input != null
                && items.TryGetValue(
                    input.ItemId,
                    out ResourceItemDefinitionSO item))
            {
                tags |= item.IngredientTags;
            }
        }

        bool animal = (tags & (ResourceIngredientTag.Meat
            | ResourceIngredientTag.Milk
            | ResourceIngredientTag.Egg)) != 0;
        bool plant = (tags & (ResourceIngredientTag.Plant
            | ResourceIngredientTag.Fungus)) != 0;
        return animal && plant
            ? "waste:mixed-rot"
            : animal
                ? "waste:animal-rot"
                : "waste:plant-rot";
    }

    private static string WorkstationTagForFacility(string facility)
    {
        return facility switch
        {
            "mill" => "workstation:mill",
            "brewery" => "workstation:brewery",
            "sawmill" => "workstation:sawmill",
            "charcoal-kiln" => "workstation:charcoal-kiln",
            "stonecutter" => "workstation:stonecutter",
            "ore-sorter" => "workstation:ore-sorter",
            "furnace" => "workstation:furnace",
            "steelworks" => "workstation:steelworks",
            "jeweler" => "workstation:jeweler",
            "arcane-forge" => "workstation:arcane-forge",
            "loom" => "workstation:loom",
            "tannery" => "workstation:tannery",
            "composter" => "workstation:composter",
            "distillery" => "workstation:distillery",
            "cookbench" => "workstation:cookbench",
            "smoker" => "workstation:smoker",
            "feedbench" => "workstation:feedbench",
            "apothecary" => "workstation:apothecary",
            "alchemy" => "workstation:alchemy",
            "arcane-loom" => "workstation:arcane-loom",
            "forge" => "workstation:forge",
            "quarry" => "workstation:quarry",
            "crop-plot" => "workstation:crop-plot",
            _ => string.Empty
        };
    }

    private static string WorkstationTagForCode(string code)
    {
        return code switch
        {
            "D03" => "workstation:kitchen-basic",
            "D12" => "workstation:tavern-brewery",
            "Q02" => "workstation:alchemy-basic",
            "S08" => "workstation:forge-basic",
            "P01" => "workstation:mill",
            "P02" => "workstation:brewery",
            "P03" => "workstation:sawmill",
            "P04" => "workstation:charcoal-kiln",
            "P05" => "workstation:stonecutter",
            "P06" => "workstation:ore-sorter",
            "P07" => "workstation:furnace",
            "P08" => "workstation:steelworks",
            "P09" => "workstation:jeweler",
            "P10" => "workstation:arcane-forge",
            "P11" => "workstation:loom",
            "P12" => "workstation:tannery",
            "P13" => "workstation:composter",
            "P14" => "workstation:distillery",
            "P15" => "workstation:cookbench",
            "P16" => "workstation:smoker",
            "P17" => "workstation:feedbench",
            "P18" => "workstation:apothecary",
            "P19" => "workstation:alchemy",
            "P20" => "workstation:arcane-loom",
            "P21" => "workstation:forge",
            "P22" => "workstation:quarry",
            "P23" => "workstation:crop-plot",
            "P24" => "workstation:hydroponics",
            _ => string.Empty
        };
    }

    private static ItemAmountDefinition A(string id, int amount) =>
        new(id, amount);

    private static ProductionOutputDefinition O(string id, int amount) =>
        new(
            "output:main",
            ProductionOutputRole.Main,
            id,
            amount);

    private static string[] W(params string[] values) => values;

    private static bool WouldChange<T>(T asset, Action<T> configure)
        where T : ScriptableObject
    {
        if (asset == null)
            throw new ArgumentNullException(nameof(asset));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        string before = EditorJsonUtility.ToJson(asset);
        T candidate = UnityEngine.Object.Instantiate(asset);
        try
        {
            candidate.name = asset.name;
            configure(candidate);
            return !string.Equals(
                CanonicalizeSemanticJson(before),
                CanonicalizeSemanticJson(EditorJsonUtility.ToJson(candidate)),
                StringComparison.Ordinal);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(candidate);
        }
    }

    private static string CanonicalizeSemanticJson(string json) =>
        System.Text.RegularExpressions.Regex.Replace(
            json ?? string.Empty,
            "\\\"rid\\\"\\s*:\\s*-?[0-9]+",
            "\\\"rid\\\":0",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static T[] LoadAll<T>(string root)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }

    private static T GetOrCreate<T>(string path)
        where T : ScriptableObject
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

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = $"{current}/{parts[index]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }
            current = next;
        }
    }

    private static string Sanitize(string value)
    {
        return new string((value ?? string.Empty)
            .Select(character => char.IsLetterOrDigit(character)
                ? character
                : '_')
            .ToArray());
    }
}
#endif
