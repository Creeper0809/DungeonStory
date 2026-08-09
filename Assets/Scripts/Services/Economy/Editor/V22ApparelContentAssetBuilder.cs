#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V22ApparelContentAssetBuilder
{
    private const int ApparelNumericIdBase = 23001;
    private const int TextileMaterialNumericIdBase = 23101;
    private const string ApparelRoot = "Assets/Resources/SO/Apparel/Definitions";
    private const string MaterialRoot = "Assets/Resources/SO/Apparel/Materials";
    private const string ItemRoot = "Assets/Resources/SO/Economy/Items/V22Apparel";
    private const string FacilityRoot = "Assets/Resources/SO/Building/V22Apparel";
    private const string CropRoot = "Assets/Resources/SO/Economy/Crops/V22Textiles";
    private const string GenomeRoot = "Assets/Resources/SO/Economy/CropGenomes/V22Textiles";
    private const string RecipeRoot = "Assets/Resources/SO/Economy/Recipes/V22Apparel";

    private readonly struct MaterialSpec
    {
        public MaterialSpec(
            string id,
            string itemId,
            string name,
            TextileMaterialTag tags,
            float warmth,
            float heat,
            float water,
            float air,
            float sterile,
            float durability,
            float weight,
            float drying,
            string research)
        {
            Id = id;
            ItemId = itemId;
            Name = name;
            Tags = tags;
            Warmth = warmth;
            Heat = heat;
            Water = water;
            Air = air;
            Sterile = sterile;
            Durability = durability;
            Weight = weight;
            Drying = drying;
            Research = research;
        }

        public string Id { get; }
        public string ItemId { get; }
        public string Name { get; }
        public TextileMaterialTag Tags { get; }
        public float Warmth { get; }
        public float Heat { get; }
        public float Water { get; }
        public float Air { get; }
        public float Sterile { get; }
        public float Durability { get; }
        public float Weight { get; }
        public float Drying { get; }
        public string Research { get; }
    }

    private readonly struct ApparelSpec
    {
        public ApparelSpec(
            string id,
            string itemId,
            string name,
            ApparelBodyForm body,
            ApparelLayer layer,
            ApparelFitMode fit,
            AnatomyAttachmentPoint required,
            AnatomyAttachmentPoint occupied,
            AnatomyAttachmentPoint sealedPoints,
            ApparelModificationKind modifications,
            ApparelUseTag tags,
            TextileMaterialTag materialTags,
            float coefficient,
            float weight,
            string research)
        {
            Id = id;
            ItemId = itemId;
            Name = name;
            Body = body;
            Layer = layer;
            Fit = fit;
            Required = required;
            Occupied = occupied;
            SealedPoints = sealedPoints;
            Modifications = modifications;
            Tags = tags;
            MaterialTags = materialTags;
            Coefficient = coefficient;
            Weight = weight;
            Research = research;
        }

        public string Id { get; }
        public string ItemId { get; }
        public string Name { get; }
        public ApparelBodyForm Body { get; }
        public ApparelLayer Layer { get; }
        public ApparelFitMode Fit { get; }
        public AnatomyAttachmentPoint Required { get; }
        public AnatomyAttachmentPoint Occupied { get; }
        public AnatomyAttachmentPoint SealedPoints { get; }
        public ApparelModificationKind Modifications { get; }
        public ApparelUseTag Tags { get; }
        public TextileMaterialTag MaterialTags { get; }
        public float Coefficient { get; }
        public float Weight { get; }
        public string Research { get; }
    }

    private readonly struct FacilitySpec
    {
        public FacilitySpec(
            int id,
            string name,
            string research,
            string workstation,
            ResearchFacilityCommandKind command,
            FacilityUseClassification classification)
        {
            Id = id;
            Name = name;
            Research = research;
            Workstation = workstation;
            Command = command;
            Classification = classification;
        }

        public int Id { get; }
        public string Name { get; }
        public string Research { get; }
        public string Workstation { get; }
        public ResearchFacilityCommandKind Command { get; }
        public FacilityUseClassification Classification { get; }
    }

    [MenuItem("Tools/DungeonStory/V22/Rebuild Apparel And Textile Content")]
    public static void EnsureAssets()
    {
        EnsureFolder(ApparelRoot);
        EnsureFolder(MaterialRoot);
        EnsureFolder(ItemRoot);
        EnsureFolder(FacilityRoot);
        EnsureFolder(CropRoot);
        EnsureFolder(GenomeRoot);
        EnsureFolder(RecipeRoot);
        BuildMaterials();
        BuildApparel();
        BuildMaintenanceItems();
        BuildTextileProductionChain();
        BuildFiberCrops();
        WireAnimalFiberProduction();
        BuildFacilities();
        WireExistingResearchUnlocks();
        GameContentCatalogAssetBuilder.ReindexItemDefinitions();
        GameContentCatalogAssetBuilder.ReindexV22ApparelDefinitions();
        AssetDatabase.SaveAssets();
    }

    public static IReadOnlyDictionary<string, int[]> GetFacilityUnlockIds() =>
        Facilities()
            .GroupBy(value => value.Research, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.Id).OrderBy(value => value).ToArray(),
                StringComparer.Ordinal);

    public static IReadOnlyList<string> ValidateAuthoredCounts()
    {
        List<string> errors = new();
        ApparelSpec[] apparel = Apparel();
        MaterialSpec[] materials = Materials();
        FacilitySpec[] facilities = Facilities();
        if (apparel.Length != 56) errors.Add($"V22 apparel count must be 56, found {apparel.Length}.");
        if (materials.Count(value => (value.Tags & TextileMaterialTag.Woven) != 0) != 10)
            errors.Add("V22 woven textile count must be 10.");
        if (materials.Count(value => (value.Tags & TextileMaterialTag.NonWoven) != 0) != 2)
            errors.Add("V22 non-woven apparel material count must be 2.");
        if (facilities.Length != 14) errors.Add($"V22 facility count must be 14, found {facilities.Length}.");
        if (apparel.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != apparel.Length
            || apparel.Select(value => value.ItemId).Distinct(StringComparer.Ordinal).Count() != apparel.Length)
            errors.Add("V22 apparel IDs and physical item IDs must be unique.");
        if (facilities.Select(value => value.Id).Distinct().Count() != 14
            || facilities.Any(value => value.Id < 9301 || value.Id > 9314))
            errors.Add("V22 facilities must occupy exactly IDs 9301..9314.");
        return errors;
    }

    private static void BuildMaterials()
    {
        MaterialSpec[] specs = Materials();
        for (int index = 0; index < specs.Length; index++)
        {
            MaterialSpec spec = specs[index];
            string path = $"{MaterialRoot}/{Sanitize(spec.Id)}.asset";
            TextileMaterialDefinitionSO asset = GetOrCreate<TextileMaterialDefinitionSO>(path);
            MonoScript script = MonoScript.FromScriptableObject(asset);
            string scriptPath = script == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(script).Replace('\\', '/');
            if (!string.Equals(
                    scriptPath,
                    "Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs",
                    StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(path);
                asset = GetOrCreate<TextileMaterialDefinitionSO>(path);
            }
            asset.id = TextileMaterialNumericIdBase + index;
            asset.Configure(
                spec.Id,
                spec.ItemId,
                spec.Name,
                $"{spec.Name}의 V22 주원단 정의. 등급과 상태 밴드만 물리 스택 병합 키에 사용한다.",
                spec.Tags,
                spec.Warmth,
                spec.Heat,
                spec.Water,
                spec.Air,
                spec.Sterile,
                spec.Durability,
                spec.Weight,
                spec.Drying,
                spec.Research);
            EditorUtility.SetDirty(asset);
            EnsurePhysicalItem(spec);
        }
    }

    private static void EnsurePhysicalItem(MaterialSpec spec)
    {
        ResourceItemDefinitionSO existing = AssetDatabase.FindAssets(
                "t:ResourceItemDefinitionSO", new[] { "Assets/Resources/SO/Economy" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
            .FirstOrDefault(value => value != null
                && string.Equals(value.ItemId, spec.ItemId, StringComparison.Ordinal));
        if (existing != null)
        {
            existing.ConfigureCore(
                existing.ItemId,
                existing.DisplayName,
                existing.Description,
                existing.StockCategory,
                existing.UnitPrice,
                existing.UnitWeight,
                100,
                existing.Sprite);
            EditorUtility.SetDirty(existing);
            return;
        }

        ResourceItemDefinitionSO item = GetOrCreate<ResourceItemDefinitionSO>(
            $"{ItemRoot}/{Sanitize(spec.ItemId)}.asset");
        item.Configure(
            spec.ItemId,
            spec.Name,
            $"{spec.Name} 물리 원단. V23에서는 재료 등급 없이 Ready/Wet/Contaminated 상태로 병합된다.",
            StockCategory.General,
            ResourceItemKind.Intermediate,
            ResourceIngredientTag.Fiber,
            10,
            0.2f * spec.Weight,
            100,
            spec.Research);
        EditorUtility.SetDirty(item);
    }

    private static void BuildApparel()
    {
        ApparelSpec[] specs = Apparel();
        for (int index = 0; index < specs.Length; index++)
        {
            ApparelSpec spec = specs[index];
            ApparelDefinitionSO asset = GetOrCreate<ApparelDefinitionSO>(
                $"{ApparelRoot}/{Sanitize(spec.Id)}.asset");
            asset.id = ApparelNumericIdBase + index;
            asset.Configure(
                spec.Id,
                spec.ItemId,
                spec.Name,
                $"{spec.Name}. 실제 부착점·크기·레이어·개조와 한 종류의 주원단으로 성능을 결정한다.",
                spec.Body,
                spec.Layer,
                spec.Fit,
                spec.Required,
                spec.Occupied,
                spec.SealedPoints,
                spec.Modifications,
                spec.Tags,
                spec.MaterialTags,
                spec.Coefficient,
                spec.Weight,
                spec.Research);
            EditorUtility.SetDirty(asset);

            ResourceItemDefinitionSO existing = AssetDatabase.FindAssets(
                    "t:ResourceItemDefinitionSO", new[] { "Assets/Resources/SO/Economy" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
                .FirstOrDefault(value => value != null
                    && string.Equals(value.ItemId, spec.ItemId, StringComparison.Ordinal));
            if (existing != null)
            {
                continue;
            }
            ResourceItemDefinitionSO item = GetOrCreate<ResourceItemDefinitionSO>(
                $"{ItemRoot}/{Sanitize(spec.ItemId)}.asset");
            item.Configure(
                spec.ItemId,
                spec.Name,
                $"{spec.Name} 물리 인스턴스. 크기·개조·재료 계보·위생·내구도를 보존한다.",
                StockCategory.General,
                ResourceItemKind.FinishedGood,
                ResourceIngredientTag.Fiber,
                20,
                spec.Weight,
                1,
                spec.Research);
            EditorUtility.SetDirty(item);
        }
    }

    private static void BuildTextileProductionChain()
    {
        int numericId = 22001;
        foreach (MaterialSpec material in Materials()
                     .Where(value => (value.Tags & TextileMaterialTag.Woven) != 0))
        {
            string slug = material.Id.Substring("textile:".Length);
            string rawItemId = RawFiberItemId(slug);
            string yarnItemId = "yarn:" + slug;
            EnsureFiberItem(rawItemId, material.Name + " 원섬유", 200, false);
            EnsureFiberItem(yarnItemId, material.Name + " 원사", 200, false);
            BuildRecipe(
                numericId++,
                "recipe:v22:spin:" + slug,
                material.Name + " 방적",
                "workstation:v22:manual-spinning",
                material.Research,
                18f,
                rawItemId,
                3,
                yarnItemId,
                2);
            BuildRecipe(
                numericId++,
                "recipe:v22:spin-powered:" + slug,
                material.Name + " 동력 방적",
                "workstation:v22:powered-spinning",
                "research:industry:assisted-processing",
                9f,
                rawItemId,
                3,
                yarnItemId,
                2);
            BuildRecipe(
                numericId++,
                "recipe:v22:weave:" + slug,
                material.Name + " 직조",
                "workstation:v22:powered-weaving",
                material.Research,
                24f,
                yarnItemId,
                3,
                material.ItemId,
                2);
        }

        foreach (ApparelSpec apparel in Apparel())
        {
            BuildRecipe(
                numericId++,
                "recipe:v22:apparel:" + apparel.Id.Substring("apparel:".Length),
                apparel.Name + " 재단",
                "workstation:v22:tailoring",
                apparel.Research,
                22f * Mathf.Max(.5f, apparel.Coefficient),
                "material:cloth",
                Mathf.Max(1, Mathf.CeilToInt(2f * apparel.Coefficient)),
                apparel.ItemId,
                1);
        }

        BuildRecipe(
            numericId++,
            "recipe:v22:sewing-thread",
            "재봉실",
            "workstation:v22:manual-spinning",
            "research:textile:fiber",
            8f,
            "yarn:shade-cloth",
            2,
            "material:sewing-thread",
            6);
        BuildRecipe(
            numericId++,
            "recipe:v22:mending-scrap",
            "범용 수선 조각",
            "workstation:v22:tailoring",
            "research:textile:tailoring",
            6f,
            "material:cloth",
            1,
            "material:mending-scrap",
            2);
        BuildRecipe(
            numericId,
            "recipe:v22:sewing-kit",
            "재봉 도구",
            "workstation:v22:tailoring",
            "research:textile:tailoring",
            12f,
            "component:machine-parts",
            1,
            "tool:sewing-kit",
            1);
    }

    private static void BuildMaintenanceItems()
    {
        EnsureMaintenanceItem(
            "tool:sewing-kit",
            "재봉 도구",
            ResourceItemKind.FinishedGood,
            1,
            18,
            "research:textile:tailoring");
        EnsureMaintenanceItem(
            "material:sewing-thread",
            "재봉실",
            ResourceItemKind.Intermediate,
            200,
            3,
            "research:textile:fiber");
        EnsureMaintenanceItem(
            "material:mending-scrap",
            "범용 수선 조각",
            ResourceItemKind.Intermediate,
            200,
            2,
            "research:textile:tailoring");
    }

    private static void EnsureMaintenanceItem(
        string itemId,
        string displayName,
        ResourceItemKind kind,
        int maxStack,
        int unitPrice,
        string researchId)
    {
        ResourceItemDefinitionSO item = AssetDatabase.FindAssets(
                "t:ResourceItemDefinitionSO", new[] { "Assets/Resources/SO/Economy" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
            .FirstOrDefault(value => value != null
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            ?? GetOrCreate<ResourceItemDefinitionSO>(
                $"{ItemRoot}/{Sanitize(itemId)}.asset");
        item.Configure(
            itemId,
            displayName,
            "V22 의복 제작과 수선 작업에서 예약·운반·소비하는 물리 물품.",
            StockCategory.General,
            kind,
            ResourceIngredientTag.Fiber,
            unitPrice,
            .05f,
            maxStack,
            researchId);
        EditorUtility.SetDirty(item);
    }

    private static void BuildRecipe(
        int numericId,
        string recipeId,
        string displayName,
        string workstationTag,
        string researchId,
        float work,
        string inputId,
        int inputAmount,
        string outputId,
        int outputAmount)
    {
        ProductionRecipeSO recipe = GetOrCreate<ProductionRecipeSO>(
            $"{RecipeRoot}/{Sanitize(recipeId)}.asset");
        recipe.id = numericId;
        recipe.Configure(
            recipeId,
            displayName,
            "원섬유→원사→원단→의복의 V22 물리 생산 단계다.",
            workstationTag,
            "work:craft",
            researchId,
            work,
            new[] { new ItemAmountDefinition(inputId, inputAmount) },
            new[] { new ProductionOutputDefinition(outputId, outputAmount) });
        recipe.ConfigureWorkshop(
            workstationTag,
            Array.Empty<string>(),
            ProductionProcessKind.WorkOnly);
        EditorUtility.SetDirty(recipe);
    }

    private static string RawFiberItemId(string slug) => slug switch
    {
        "frost-linen" => "fiber:frost-flax",
        "ember-cotton" => "fiber:ember-cotton",
        "mire-canvas" => "fiber:mire-reed",
        "spore-hemp" => "fiber:spore-hemp",
        "common-wool" => "resource:wool",
        "frost-wool" => "fiber:frost-wool",
        "deep-goat-wool" => "fiber:deep-goat-wool",
        "cave-silk" => "fiber:cave-silk",
        "dreamweave" => "resource:dreamleaf",
        _ => "resource:shade-fiber"
    };

    private static void BuildFacilities()
    {
        Sprite fallback = AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building/Modular" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(value => value != null && value.sprite != null)
            ?.sprite;
        foreach (FacilitySpec spec in Facilities())
        {
            BuildingSO asset = GetOrCreate<BuildingSO>(
                $"{FacilityRoot}/V22_{spec.Id}_{Sanitize(spec.Name)}.asset");
            asset.id = spec.Id;
            asset.objectName = spec.Name;
            asset.sprite = fallback;
            asset.icon = fallback;
            asset.width = 1;
            asset.height = 1;
            asset.layer = GridLayer.Building;
            asset.category = BuildingCategory.Crafting;
            asset.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
            asset.horizontalDraggable = false;
            asset.verticalDraggable = false;
            asset.unlocked = false;
            asset.ConfigureGameplayExecution(spec.Classification, spec.Command);
            BuildingAbilityCollection abilities = new();
            BuildingWorkAmountAbility workAmount = new()
            {
                constructionWorkRequired = spec.Id is 9305 or 9313 or 9314 ? 90 : 48,
                repairWorkRequired = 12f,
                cleanWorkRequired = 8f,
                researchWorkRequired = 6f,
                operateWorkRequired = 10f
            };
            workAmount.SetConstructionMaterials(spec.Id switch
            {
                9305 or 9313 or 9314 => new[]
                {
                    new ItemAmountDefinition("material:steel-ingot", 4),
                    new ItemAmountDefinition("component:machine-parts", 3),
                    new ItemAmountDefinition("material:lumber", 2)
                },
                9303 or 9310 => new[]
                {
                    new ItemAmountDefinition("material:stone-block", 4),
                    new ItemAmountDefinition("material:lumber", 2)
                },
                _ => new[]
                {
                    new ItemAmountDefinition("material:lumber", 4),
                    new ItemAmountDefinition("material:iron-ingot", 2)
                }
            });
            abilities.Add(workAmount);
            abilities.Add(new BuildingFacilityPartAbility { code = $"V22A{spec.Id - 9300:D2}" });
            abilities.Add(new BuildingSemanticTagsAbility
            {
                tags = new[] { "v22-apparel", spec.Research, spec.Workstation }
            });
            abilities.Add(new BuildingProductionWorkstationAbility
            {
                workstationTag = spec.Workstation,
                stockSensorInstallationItemId = "component:stock-sensor-panel"
            });
            abilities.Add(new BuildingProductionBufferAbility { defaultBatchCapacity = 12 });
            asset.ReplaceAbilities(abilities);
            EditorUtility.SetDirty(asset);
        }
    }

    private static void WireExistingResearchUnlocks()
    {
        Dictionary<string, ResearchProjectSO> projects = AssetDatabase.FindAssets(
                "t:ResearchProjectSO", new[] { "Assets/Resources/SO/Research/Projects" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(value => value != null)
            .ToDictionary(value => value.ProjectId.Value, StringComparer.Ordinal);
        foreach (KeyValuePair<string, int[]> pair in GetFacilityUnlockIds())
        {
            string owner = V21ResearchConsolidation.Normalize(pair.Key);
            if (!projects.TryGetValue(owner, out ResearchProjectSO project))
            {
                throw new InvalidOperationException(
                    $"V22 facility research owner '{owner}' does not exist.");
            }
            foreach (int buildingId in pair.Value)
            {
                if (!project.Unlocks.OfType<BlueprintBuildingUnlock>()
                        .Any(value => value.buildingId == buildingId))
                {
                    project.UnlockCollection.Add(
                        new BlueprintBuildingUnlock { buildingId = buildingId });
                }
            }
            EditorUtility.SetDirty(project);
        }

        ProductionRecipeSO[] recipes = AssetDatabase.FindAssets(
                "t:ProductionRecipeSO", new[] { RecipeRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>)
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        foreach (ProductionRecipeSO recipe in recipes)
        {
            string owner = V21ResearchConsolidation.Normalize(
                recipe.RequiredResearchId);
            if (!projects.TryGetValue(owner, out ResearchProjectSO project))
            {
                throw new InvalidOperationException(
                    $"V22 recipe research owner '{owner}' does not exist for '{recipe.RecipeId}'.");
            }
            if (!project.Unlocks.OfType<BlueprintRecipeUnlock>()
                    .Any(value => string.Equals(
                        value.recipeId,
                        recipe.RecipeId,
                        StringComparison.Ordinal)))
            {
                project.UnlockCollection.Add(
                    new BlueprintRecipeUnlock { recipeId = recipe.RecipeId });
                EditorUtility.SetDirty(project);
            }
        }
    }

    private static void BuildFiberCrops()
    {
        string[] slugs = { "frost-flax", "ember-cotton", "mire-reed", "spore-hemp" };
        string[] names = { "서리 아마", "잿불 목화", "습지 갈대", "포자 삼" };
        Vector2[] temperatures =
        {
            new(-8f, 20f), new(14f, 42f), new(8f, 34f), new(6f, 30f)
        };
        for (int index = 0; index < slugs.Length; index++)
        {
            string slug = slugs[index];
            string cropId = "crop:" + slug;
            CropGenomeDefinitionSO baseGenome = GetOrCreate<CropGenomeDefinitionSO>(
                $"{GenomeRoot}/genome_{slug}_base.asset");
            baseGenome.id = 9401 + index * 3;
            baseGenome.ConfigureCultivar(
                $"genome:{slug}:base",
                cropId,
                names[index] + " 기본종",
                "여섯 기존 좌위만 사용하는 V22 기본 섬유 품종.",
                Array.Empty<string>(),
                Loci(0, 0, 0, 0, 0, 0));
            EditorUtility.SetDirty(baseGenome);

            CropGenomeDefinitionSO climate = GetOrCreate<CropGenomeDefinitionSO>(
                $"{GenomeRoot}/genome_{slug}_climate.asset");
            climate.id = 9402 + index * 3;
            climate.ConfigureCultivar(
                $"genome:{slug}:climate",
                cropId,
                names[index] + " 기후내성종",
                "수확량 고점 대신 온도와 병 저항을 높인 고급 섬유용 품종.",
                new[] { "tradeoff:yield", "role:fine-fiber" },
                Loci(index == 1 ? 0 : 2, index == 1 ? 2 : 0, -1, -1, 2, 0));
            EditorUtility.SetDirty(climate);

            CropGenomeDefinitionSO bulk = GetOrCreate<CropGenomeDefinitionSO>(
                $"{GenomeRoot}/genome_{slug}_bulk.asset");
            bulk.id = 9403 + index * 3;
            bulk.ConfigureCultivar(
                $"genome:{slug}:bulk",
                cropId,
                names[index] + " 대량생산종",
                "성장·수확 좌위의 품질 비용을 지불하고 보통급 섬유를 대량 생산한다.",
                new[] { "tradeoff:quality", "role:bulk-fiber" },
                Loci(-1, -1, 2, 2, -1, 1));
            EditorUtility.SetDirty(bulk);

            EnsureFiberItem("fiber:" + slug, names[index] + " 원섬유", 200, false);
            EnsureFiberItem("seed-lot:" + slug, names[index] + " 종자 로트", 100, true);
            CropDefinitionSO crop = GetOrCreate<CropDefinitionSO>(
                $"{CropRoot}/crop_{slug}.asset");
            crop.id = 9351 + index;
            crop.Configure(
                cropId,
                names[index],
                "fiber:" + slug,
                "research:textile:fiber",
                84f + index * 8f,
                4f,
                7f,
                .35f + index * .05f,
                6,
                true,
                temperatures[index]);
            crop.ConfigureEcology(
                "seed-lot:" + slug,
                baseGenome,
                CropFamilyGroup.Fiber,
                CropDiseaseKind.GrainFiberRust);
            EditorUtility.SetDirty(crop);
        }
    }

    private static void WireAnimalFiberProduction()
    {
        (string SpeciesId, string ItemId, string Name, int Amount, float Interval)[] specs =
        {
            ("silk_spider", "fiber:cave-silk", "동굴 비단", 2, 3f),
            ("frost_ram", "fiber:frost-wool", "서리 양모", 3, 5f),
            ("deep_goat", "fiber:deep-goat-wool", "심층 염소모", 2, 4f)
        };
        WildlifeSpeciesSO[] wildlife = AssetDatabase.FindAssets(
                "t:WildlifeSpeciesSO", new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>)
            .Where(value => value != null)
            .ToArray();
        foreach (var spec in specs)
        {
            EnsureFiberItem(spec.ItemId, spec.Name, 200, false);
            WildlifeSpeciesSO species = wildlife.FirstOrDefault(value =>
                string.Equals(value.SpeciesId, spec.SpeciesId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"V22 animal fiber species '{spec.SpeciesId}' is missing.");
            List<WildlifeHusbandryProductDefinition> products = species.Husbandry.Products
                .Where(value => value != null
                    && !string.Equals(value.ItemId, spec.ItemId, StringComparison.Ordinal))
                .ToList();
            products.Add(new WildlifeHusbandryProductDefinition(
                spec.ItemId,
                spec.Amount,
                spec.Interval,
                false,
                true));
            species.ConfigureHusbandryProducts(products);
            EditorUtility.SetDirty(species);
        }
    }

    private static void EnsureFiberItem(
        string itemId,
        string name,
        int maxStack,
        bool seed)
    {
        ResourceItemDefinitionSO existing = AssetDatabase.FindAssets(
                "t:ResourceItemDefinitionSO", new[] { "Assets/Resources/SO/Economy" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
            .FirstOrDefault(value => value != null
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal));
        if (existing != null)
        {
            existing.ConfigureCore(
                existing.ItemId,
                existing.DisplayName,
                existing.Description,
                existing.StockCategory,
                existing.UnitPrice,
                existing.UnitWeight,
                maxStack,
                existing.Sprite);
            EditorUtility.SetDirty(existing);
            return;
        }
        ResourceItemDefinitionSO item = GetOrCreate<ResourceItemDefinitionSO>(
            $"{ItemRoot}/{Sanitize(itemId)}.asset");
        item.Configure(
            itemId,
            name,
            seed
                ? "작물·품종·품질·병원체 상태를 가진 물리 종자 로트."
                : "V23에서 등급 없이 상태 밴드로만 병합되는 원섬유.",
            StockCategory.General,
            ResourceItemKind.Raw,
            ResourceIngredientTag.Fiber | ResourceIngredientTag.Plant,
            seed ? 4 : 6,
            seed ? .02f : .08f,
            maxStack,
            "research:textile:fiber");
        EditorUtility.SetDirty(item);
    }

    private static IReadOnlyList<DiploidLocusSaveData> Loci(
        int cold,
        int heat,
        int growth,
        int yield,
        int disease,
        int seed) => new[]
    {
        L(CropGenomeLocus.ColdTolerance, cold),
        L(CropGenomeLocus.HeatTolerance, heat),
        L(CropGenomeLocus.GrowthSpeed, growth),
        L(CropGenomeLocus.Yield, yield),
        L(CropGenomeLocus.DiseaseResistance, disease),
        L(CropGenomeLocus.SeedYield, seed)
    };

    private static DiploidLocusSaveData L(CropGenomeLocus locus, int value) => new()
    {
        locus = locus,
        alleleA = Mathf.Clamp(value, -2, 2),
        alleleB = Mathf.Clamp(value, -2, 2)
    };

    private static MaterialSpec[] Materials() => new[]
    {
        M("textile:shade-cloth", "material:cloth", "그늘천", TextileMaterialTag.Woven | TextileMaterialTag.Plant, .45f,.35f,.25f,.2f,.15f,60f,1f,1f,"research:textile:fiber"),
        M("textile:frost-linen", "material:frost-linen", "서리 린넨", TextileMaterialTag.Woven | TextileMaterialTag.Plant | TextileMaterialTag.Cold | TextileMaterialTag.Light, .62f,.32f,.3f,.25f,.25f,65f,.82f,1.25f,"research:textile:fiber"),
        M("textile:ember-cotton", "material:ember-cotton", "잿불 면직물", TextileMaterialTag.Woven | TextileMaterialTag.Plant | TextileMaterialTag.Heat, .25f,.68f,.22f,.18f,.3f,58f,.88f,.45f,"research:textile:fiber"),
        M("textile:mire-canvas", "material:mire-canvas", "습지 캔버스", TextileMaterialTag.Woven | TextileMaterialTag.Plant | TextileMaterialTag.Wet | TextileMaterialTag.Durable, .38f,.38f,.72f,.3f,.2f,92f,1.35f,.75f,"research:textile:layered"),
        M("textile:spore-hemp", "material:spore-hemp", "포자 삼베", TextileMaterialTag.Woven | TextileMaterialTag.Plant | TextileMaterialTag.Airborne, .35f,.45f,.35f,.68f,.25f,78f,1.05f,1.1f,"research:textile:layered"),
        M("textile:common-wool", "material:common-wool", "일반 모직", TextileMaterialTag.Woven | TextileMaterialTag.Animal | TextileMaterialTag.Cold, .75f,.22f,.28f,.3f,.2f,70f,1.2f,.4f,"research:textile:fiber"),
        M("textile:frost-wool", "material:frost-wool", "서리 양모직", TextileMaterialTag.Woven | TextileMaterialTag.Animal | TextileMaterialTag.Cold | TextileMaterialTag.Durable, .92f,.18f,.38f,.35f,.2f,84f,1.28f,.55f,"research:textile:layered"),
        M("textile:deep-goat-wool", "material:deep-goat-wool", "심층 염소모직", TextileMaterialTag.Woven | TextileMaterialTag.Animal | TextileMaterialTag.Durable, .78f,.42f,.42f,.4f,.25f,96f,1.3f,.65f,"research:textile:layered"),
        M("textile:cave-silk", "material:cave-silk", "동굴 비단", TextileMaterialTag.Woven | TextileMaterialTag.Animal | TextileMaterialTag.Light | TextileMaterialTag.Sterile, .48f,.55f,.45f,.5f,.72f,74f,.55f,1.35f,"research:textile:tailoring"),
        M("textile:dreamweave", "material:dreamweave", "몽직물", TextileMaterialTag.Woven | TextileMaterialTag.Arcane | TextileMaterialTag.Light, .58f,.58f,.5f,.62f,.55f,88f,.62f,1.05f,"research:textile:tailoring"),
        M("textile:leather", "material:leather", "가죽", TextileMaterialTag.NonWoven | TextileMaterialTag.Animal | TextileMaterialTag.Durable, .52f,.48f,.62f,.42f,.18f,105f,1.45f,.6f,"research:textile:tailoring"),
        M("textile:rune-leather", "material:rune-leather", "룬가죽", TextileMaterialTag.NonWoven | TextileMaterialTag.Animal | TextileMaterialTag.Arcane | TextileMaterialTag.Durable, .72f,.7f,.74f,.68f,.52f,125f,1.38f,.75f,"research:textile:tailoring")
    };

    private static ApparelSpec[] Apparel()
    {
        const ApparelModificationKind allMods = ApparelModificationKind.TailOpening | ApparelModificationKind.WingSlits | ApparelModificationKind.HornClearance;
        const TextileMaterialTag all = TextileMaterialTag.Woven | TextileMaterialTag.NonWoven;
        const TextileMaterialTag woven = TextileMaterialTag.Woven;
        AnatomyAttachmentPoint torso = AnatomyAttachmentPoint.Torso;
        AnatomyAttachmentPoint lower = AnatomyAttachmentPoint.Pelvis;
        AnatomyAttachmentPoint legs = AnatomyAttachmentPoint.Pelvis | AnatomyAttachmentPoint.Legs;
        AnatomyAttachmentPoint full = AnatomyAttachmentPoint.Torso | AnatomyAttachmentPoint.Pelvis | AnatomyAttachmentPoint.Arms | AnatomyAttachmentPoint.Legs | AnatomyAttachmentPoint.Back;
        AnatomyAttachmentPoint appendages = AnatomyAttachmentPoint.OptionalAppendages;
        return new[]
        {
            A("lower-underwear","하의 속옷",ApparelBodyForm.Humanoid,ApparelLayer.Underwear,ApparelFitMode.Sized,lower,lower,AnatomyAttachmentPoint.Tail,ApparelModificationKind.TailOpening,ApparelUseTag.Underwear,woven,.75f,.18f,"research:textile:fiber"),
            A("loincloth-underwear","허리 두름 속옷",ApparelBodyForm.Humanoid,ApparelLayer.Underwear,ApparelFitMode.Adjustable,lower,lower,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Underwear,woven,.65f,.16f,"research:textile:fiber"),
            A("undershirt","속셔츠",ApparelBodyForm.Humanoid,ApparelLayer.Underwear,ApparelFitMode.Sized,torso,torso,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Underwear,woven,.78f,.2f,"research:textile:fiber"),
            A("chest-wrap","가슴 감개",ApparelBodyForm.Humanoid,ApparelLayer.Underwear,ApparelFitMode.Adjustable,torso,torso,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Underwear,woven,.65f,.15f,"research:textile:fiber"),
            A("long-underpants","내의 바지",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Sized,legs,legs,AnatomyAttachmentPoint.Tail,ApparelModificationKind.TailOpening,ApparelUseTag.Underwear|ApparelUseTag.Cold,woven,.9f,.35f,"research:textile:fiber"),
            A("socks","양말",ApparelBodyForm.Humanoid,ApparelLayer.Underwear,ApparelFitMode.Sized,AnatomyAttachmentPoint.Feet,AnatomyAttachmentPoint.Feet,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Underwear,woven,.72f,.12f,"research:textile:fiber"),
            A("footwraps","발싸개",ApparelBodyForm.Humanoid,ApparelLayer.Underwear,ApparelFitMode.Adjustable,AnatomyAttachmentPoint.Feet,AnatomyAttachmentPoint.Feet,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Underwear,woven,.62f,.1f,"research:textile:fiber"),
            A("sleep-top","잠옷 상의",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Sized,torso,torso|AnatomyAttachmentPoint.Arms,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Sleep,woven,.82f,.35f,"research:textile:tailoring"),
            A("sleep-bottom","잠옷 하의",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Sized,legs,legs,AnatomyAttachmentPoint.Tail,ApparelModificationKind.TailOpening,ApparelUseTag.Sleep,woven,.82f,.32f,"research:textile:tailoring"),
            A("golem-functional-lining","골렘 기능성 내피",ApparelBodyForm.Construct,ApparelLayer.Inner,ApparelFitMode.Sized,torso,full,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Protective,all,1f,.7f,"research:textile:layered"),

            A("tunic","튜닉",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Adjustable,torso,torso|AnatomyAttachmentPoint.Arms,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Daily,woven,.9f,.45f,"research:textile:tailoring"),
            A("blouse","블라우스",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Sized,torso,torso|AnatomyAttachmentPoint.Arms,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Daily,woven,.88f,.38f,"research:textile:tailoring"),
            A("work-shirt","작업 셔츠",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Adjustable,torso,torso|AnatomyAttachmentPoint.Arms,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Daily|ApparelUseTag.Work,woven,.95f,.48f,"research:textile:tailoring"),
            A("trousers","바지",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Sized,legs,legs,AnatomyAttachmentPoint.Tail,ApparelModificationKind.TailOpening,ApparelUseTag.Daily,woven,.92f,.5f,"research:textile:tailoring"),
            A("skirt","치마",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Adjustable,lower,lower|AnatomyAttachmentPoint.Legs,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Daily,woven,.82f,.42f,"research:textile:tailoring"),
            A("shorts","반바지",ApparelBodyForm.Humanoid,ApparelLayer.Inner,ApparelFitMode.Sized,lower,legs,AnatomyAttachmentPoint.Tail,ApparelModificationKind.TailOpening,ApparelUseTag.Daily|ApparelUseTag.Heat,woven,.76f,.32f,"research:textile:tailoring"),
            A("apron","앞치마",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso|lower,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Daily|ApparelUseTag.Work,all,.9f,.42f,"research:textile:tailoring"),
            A("vest","조끼",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Daily,woven,.9f,.4f,"research:textile:tailoring"),
            A("daily-robe","일상 로브",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,appendages,allMods,ApparelUseTag.Daily,woven,1f,.85f,"research:textile:tailoring"),
            A("hooded-robe","후드 로브",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full|AnatomyAttachmentPoint.Head,appendages,allMods,ApparelUseTag.Daily|ApparelUseTag.Cold,woven,1.05f,1f,"research:textile:tailoring"),
            A("cloak","망토",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso|AnatomyAttachmentPoint.Back,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Daily|ApparelUseTag.Cold,all,1f,.72f,"research:textile:tailoring"),
            A("raincoat","우비",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,appendages,allMods,ApparelUseTag.Daily|ApparelUseTag.Wet|ApparelUseTag.Protective,all,1.08f,1.1f,"research:textile:layered"),

            Existing("hauling-harness","tool:hauling-harness","운반 멜빵",ApparelLayer.Accessory,ApparelFitMode.Adjustable,torso,torso|AnatomyAttachmentPoint.Back,ApparelUseTag.Work|ApparelUseTag.Accessory,all,.9f,.9f,"research:commerce:logistics"),
            Existing("slime-warming-pad","equipment:slime-warming-pad","보온 점액 패드",ApparelLayer.Inner,ApparelFitMode.Adjustable,torso,torso,ApparelUseTag.Work|ApparelUseTag.Cold,woven,1.1f,.45f,"research:environment:cold-work"),
            Existing("cold-work-suit","equipment:cold-work-suit","방한 작업복",ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,ApparelUseTag.Work|ApparelUseTag.Cold|ApparelUseTag.Protective,all,1.18f,1.4f,"research:environment:cold-work",appendages,allMods),
            Existing("rune-cold-suit","equipment:rune-cold-suit","룬 방한복",ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,ApparelUseTag.Work|ApparelUseTag.Cold|ApparelUseTag.Protective,all,1.3f,1.55f,"research:environment:rune-insulation",appendages,allMods),
            A("heat-work-suit","내열 작업복",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,appendages,allMods,ApparelUseTag.Work|ApparelUseTag.Heat|ApparelUseTag.Protective,all,1.15f,1.35f,"research:textile:layered"),
            A("waterproof-work-suit","방수 작업복",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,appendages,allMods,ApparelUseTag.Work|ApparelUseTag.Wet|ApparelUseTag.Protective,all,1.15f,1.38f,"research:textile:layered"),
            A("spore-protection-hood","포자 방호 두건",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,AnatomyAttachmentPoint.Head|AnatomyAttachmentPoint.Face,AnatomyAttachmentPoint.Head|AnatomyAttachmentPoint.Face,AnatomyAttachmentPoint.HornSet,ApparelModificationKind.HornClearance,ApparelUseTag.Work|ApparelUseTag.Protective,woven,1.05f,.35f,"research:textile:layered"),
            A("smoke-protection-hood","연기 방호 두건",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,AnatomyAttachmentPoint.Head|AnatomyAttachmentPoint.Face,AnatomyAttachmentPoint.Head|AnatomyAttachmentPoint.Face,AnatomyAttachmentPoint.HornSet,ApparelModificationKind.HornClearance,ApparelUseTag.Work|ApparelUseTag.Protective,woven,1.08f,.38f,"research:textile:layered"),
            A("sterile-gown","무균 가운",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso|AnatomyAttachmentPoint.Arms,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Work|ApparelUseTag.Medical,woven,1f,.52f,"research:textile:tailoring"),
            A("surgical-apron","외과 앞치마",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso|lower,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Work|ApparelUseTag.Medical,all,1.05f,.62f,"research:textile:tailoring"),
            A("miner-workwear","광부 작업복",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Sized,torso,full,appendages,allMods,ApparelUseTag.Work|ApparelUseTag.Protective,all,1.08f,1.25f,"research:textile:layered"),
            A("smith-apron","대장장이 앞치마",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso|lower,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Work|ApparelUseTag.Heat|ApparelUseTag.Protective,all,1.12f,.9f,"research:textile:layered"),
            A("farmer-workwear","농부 작업복",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,appendages,allMods,ApparelUseTag.Work|ApparelUseTag.Wet,woven,1f,1f,"research:textile:tailoring"),
            A("keeper-coat","사육사 외투",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,appendages,allMods,ApparelUseTag.Work|ApparelUseTag.Protective,all,1.05f,1.15f,"research:textile:tailoring"),

            A("formal-coat","정장 외투",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Sized,torso,torso|AnatomyAttachmentPoint.Arms|AnatomyAttachmentPoint.Back,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Formal,woven,1.08f,.82f,"research:textile:tailoring"),
            A("ceremonial-dress","예복 드레스",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Sized,torso,full,appendages,allMods,ApparelUseTag.Formal|ApparelUseTag.Cultural,woven,1.12f,1.05f,"research:textile:tailoring"),
            A("ritual-robe","의식 로브",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,appendages,allMods,ApparelUseTag.Formal|ApparelUseTag.Cultural,woven,1.1f,.95f,"research:textile:tailoring"),
            A("mourning-clothes","상복",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,full,appendages,allMods,ApparelUseTag.Formal|ApparelUseTag.Cultural,woven,1f,.85f,"research:textile:tailoring"),
            A("festival-vest","축제 조끼",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Formal|ApparelUseTag.Cultural,woven,1f,.48f,"research:textile:tailoring"),
            A("envoy-coat","사절 외투",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Sized,torso,torso|AnatomyAttachmentPoint.Arms|AnatomyAttachmentPoint.Back,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Formal,woven,1.08f,.9f,"research:textile:tailoring"),
            A("contract-sash","계약 어깨띠",ApparelBodyForm.Humanoid,ApparelLayer.Accessory,ApparelFitMode.Accessory,torso,torso,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Formal|ApparelUseTag.Cultural|ApparelUseTag.Accessory,woven,.65f,.15f,"research:textile:tailoring"),
            A("weapon-vigil-cloak","무기 철야 망토",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso|AnatomyAttachmentPoint.Back,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Cultural|ApparelUseTag.Cold,woven,1.08f,.8f,"research:textile:tailoring"),
            A("sky-chorus-shawl","하늘 합창 숄",ApparelBodyForm.Humanoid,ApparelLayer.Accessory,ApparelFitMode.Accessory,torso,torso|AnatomyAttachmentPoint.Back,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Cultural|ApparelUseTag.Accessory,woven,.72f,.22f,"research:textile:tailoring"),
            A("spore-garden-cloak","포자 정원 망토",ApparelBodyForm.Humanoid,ApparelLayer.Outer,ApparelFitMode.Adjustable,torso,torso|AnatomyAttachmentPoint.Back,AnatomyAttachmentPoint.Wings,ApparelModificationKind.WingSlits,ApparelUseTag.Cultural|ApparelUseTag.Protective,woven,1.02f,.78f,"research:textile:tailoring"),

            A("belt","허리띠",ApparelBodyForm.Any,ApparelLayer.Accessory,ApparelFitMode.Accessory,lower,lower,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory,all,.62f,.2f,"research:textile:tailoring"),
            A("gloves","장갑",ApparelBodyForm.Any,ApparelLayer.Accessory,ApparelFitMode.Sized,AnatomyAttachmentPoint.Hands,AnatomyAttachmentPoint.Hands,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory|ApparelUseTag.Protective,all,.82f,.25f,"research:textile:tailoring"),
            A("boots","장화",ApparelBodyForm.Any,ApparelLayer.Accessory,ApparelFitMode.Sized,AnatomyAttachmentPoint.Feet,AnatomyAttachmentPoint.Feet,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory|ApparelUseTag.Protective,all,.9f,.65f,"research:textile:tailoring"),
            A("hat","모자",ApparelBodyForm.Any,ApparelLayer.Accessory,ApparelFitMode.Adjustable,AnatomyAttachmentPoint.Head,AnatomyAttachmentPoint.Head,AnatomyAttachmentPoint.HornSet,ApparelModificationKind.HornClearance,ApparelUseTag.Accessory,woven,.78f,.2f,"research:textile:tailoring"),
            A("scarf","목도리",ApparelBodyForm.Any,ApparelLayer.Accessory,ApparelFitMode.Accessory,AnatomyAttachmentPoint.Neck,AnatomyAttachmentPoint.Neck,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory|ApparelUseTag.Cold,woven,.82f,.18f,"research:textile:tailoring"),
            A("tail-ribbon","꼬리 리본",ApparelBodyForm.Humanoid,ApparelLayer.Accessory,ApparelFitMode.Accessory,AnatomyAttachmentPoint.Tail,AnatomyAttachmentPoint.Tail,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory|ApparelUseTag.Cultural,woven,.55f,.08f,"research:textile:tailoring"),
            A("tail-guard","꼬리 보호대",ApparelBodyForm.Humanoid,ApparelLayer.Accessory,ApparelFitMode.Accessory,AnatomyAttachmentPoint.Tail,AnatomyAttachmentPoint.Tail,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory|ApparelUseTag.Protective,all,.9f,.35f,"research:textile:layered"),
            A("horn-ring","뿔 고리",ApparelBodyForm.Humanoid,ApparelLayer.Accessory,ApparelFitMode.Accessory,AnatomyAttachmentPoint.HornSet,AnatomyAttachmentPoint.HornSet,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory|ApparelUseTag.Cultural,all,.5f,.1f,"research:textile:tailoring"),
            A("wing-harness","날개 멜빵",ApparelBodyForm.Humanoid,ApparelLayer.Accessory,ApparelFitMode.Accessory,AnatomyAttachmentPoint.Wings,AnatomyAttachmentPoint.Wings|AnatomyAttachmentPoint.Back,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory|ApparelUseTag.Work,all,.82f,.5f,"research:textile:tailoring"),
            A("wing-cloak","날개 망토",ApparelBodyForm.Humanoid,ApparelLayer.Accessory,ApparelFitMode.Accessory,AnatomyAttachmentPoint.Wings,AnatomyAttachmentPoint.Wings|AnatomyAttachmentPoint.Back,AnatomyAttachmentPoint.None,ApparelModificationKind.None,ApparelUseTag.Accessory|ApparelUseTag.Cold,woven,.9f,.6f,"research:textile:tailoring")
        };
    }

    private static FacilitySpec[] Facilities() => new[]
    {
        F(9301,"재단·재봉 작업대","research:textile:tailoring","workstation:v22:tailoring",ResearchFacilityCommandKind.ApparelTailoring,FacilityUseClassification.Production),
        F(9302,"문양·장식 작업대","research:textile:tailoring","workstation:v22:decoration",ResearchFacilityCommandKind.ApparelDecoration,FacilityUseClassification.Production),
        F(9303,"손세탁 수조","research:textile:fiber","workstation:v22:hand-laundry",ResearchFacilityCommandKind.HandLaundry,FacilityUseClassification.DomainCommand),
        F(9304,"실내 건조대","research:textile:fiber","workstation:v22:indoor-drying",ResearchFacilityCommandKind.IndoorDrying,FacilityUseClassification.DomainCommand),
        F(9305,"동력 세탁·건조기","research:industry:automatic-sanitation","workstation:v22:powered-laundry",ResearchFacilityCommandKind.PoweredLaundry,FacilityUseClassification.DomainCommand),
        F(9306,"의복 진열대","research:textile:tailoring","workstation:v22:apparel-display",ResearchFacilityCommandKind.ApparelDisplay,FacilityUseClassification.Storage),
        F(9307,"탈의 칸막이","research:textile:tailoring","workstation:v22:dressing",ResearchFacilityCommandKind.DressingChange,FacilityUseClassification.DomainCommand),
        F(9308,"수선 접수대","research:equipment:field-maintenance","workstation:v22:repair",ResearchFacilityCommandKind.ApparelRepair,FacilityUseClassification.DomainCommand),
        F(9309,"섬유 선별대","research:textile:fiber","workstation:v22:fiber-sorting",ResearchFacilityCommandKind.FiberSorting,FacilityUseClassification.Production),
        F(9310,"침지·정련조","research:textile:fiber","workstation:v22:fiber-scouring",ResearchFacilityCommandKind.FiberScouring,FacilityUseClassification.Production),
        F(9311,"수동 방적기","research:textile:fiber","workstation:v22:manual-spinning",ResearchFacilityCommandKind.ManualSpinning,FacilityUseClassification.Production),
        F(9312,"축융·마감대","research:textile:layered","workstation:v22:textile-finishing",ResearchFacilityCommandKind.TextileFinishing,FacilityUseClassification.Production),
        F(9313,"동력 방적기","research:industry:assisted-processing","workstation:v22:powered-spinning",ResearchFacilityCommandKind.PoweredSpinning,FacilityUseClassification.Production),
        F(9314,"동력 직조기","research:industry:assisted-processing","workstation:v22:powered-weaving",ResearchFacilityCommandKind.PoweredWeaving,FacilityUseClassification.Production)
    };

    private static MaterialSpec M(string id,string item,string name,TextileMaterialTag tags,float warmth,float heat,float water,float air,float sterile,float durability,float weight,float drying,string research) =>
        new(id,item,name,tags,warmth,heat,water,air,sterile,durability,weight,drying,research);
    private static ApparelSpec A(string slug,string name,ApparelBodyForm body,ApparelLayer layer,ApparelFitMode fit,AnatomyAttachmentPoint required,AnatomyAttachmentPoint occupied,AnatomyAttachmentPoint sealedPoints,ApparelModificationKind modifications,ApparelUseTag tags,TextileMaterialTag materials,float coefficient,float weight,string research) =>
        new($"apparel:{slug}",$"apparel:{slug}",name,body,layer,fit,required,occupied,sealedPoints,modifications,tags,materials,coefficient,weight,research);
    private static ApparelSpec Existing(string slug,string item,string name,ApparelLayer layer,ApparelFitMode fit,AnatomyAttachmentPoint required,AnatomyAttachmentPoint occupied,ApparelUseTag tags,TextileMaterialTag materials,float coefficient,float weight,string research,AnatomyAttachmentPoint sealedPoints = AnatomyAttachmentPoint.None,ApparelModificationKind modifications = ApparelModificationKind.None) =>
        new($"apparel:{slug}",item,name,ApparelBodyForm.Humanoid,layer,fit,required,occupied,sealedPoints,modifications,tags,materials,coefficient,weight,research);
    private static FacilitySpec F(int id,string name,string research,string workstation,ResearchFacilityCommandKind command,FacilityUseClassification classification) =>
        new(id,name,research,workstation,command,classification);

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Replace('\\', '/').Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }
            current = next;
        }
    }

    private static string Sanitize(string value) => string.Concat(
        (value ?? string.Empty).Select(character =>
            char.IsLetterOrDigit(character) ? character : '_'));
}
#endif
