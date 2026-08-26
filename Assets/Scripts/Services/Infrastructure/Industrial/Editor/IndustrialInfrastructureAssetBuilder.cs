#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class IndustrialInfrastructureAssetBuilder
{
    private const string BuildingRoot =
        "Assets/Resources/SO/Building/Industrial";
    private const string SpriteRoot =
        "Assets/Images/IndustrialInfrastructure";

    private sealed class Spec
    {
        public string Code;
        public int Id;
        public string Name;
        public int Width = 1;
        public GridLayer Layer = GridLayer.Building;
        public BuildingCategory Category = BuildingCategory.Resource;
        public Type RuntimeType = typeof(BuildableObject);
        public string ResearchId;
        public Color32 BaseColor;
        public Color32 AccentColor;
        public Func<IEnumerable<BuildingAbility>> CreateAbilities;
    }

    [MenuItem("DungeonStory/Content/Build Industrial Infrastructure")]
    public static void BuildAll()
    {
        EnsureAssets();
        ResearchProjectAssetBuilder.Rebuild();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Industrial infrastructure content rebuilt.");
    }

    public static void EnsureAssets()
    {
        EnsureFolder(BuildingRoot);
        EnsureFolder(SpriteRoot);
        foreach (Spec spec in CreateSpecs())
        {
            string spritePath = $"{SpriteRoot}/{spec.Code}.png";
            WriteSprite(spec, spritePath);
            ConfigureSprite(spritePath);
            EnsureBuilding(spec, spritePath);
        }

        PatchSanitationFixtures();
        PatchProductionFacilities();
        PatchProcessFluidConsumers();
        AssetDatabase.SaveAssets();
    }

    public static IReadOnlyDictionary<string, string[]> GetResearchUnlockCodes()
    {
        return CreateSpecs()
            .Where(spec => !string.IsNullOrWhiteSpace(spec.ResearchId))
            .GroupBy(spec => spec.ResearchId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(spec => spec.Code)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private static void EnsureBuilding(Spec spec, string spritePath)
    {
        string path = $"{BuildingRoot}/{spec.Code}_{Sanitize(spec.Name)}.asset";
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingSO>();
            AssetDatabase.CreateAsset(building, path);
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        building.id = spec.Id;
        building.objectName = spec.Name;
        building.sprite = sprite;
        building.icon = sprite;
        building.width = Mathf.Max(1, spec.Width);
        building.height = 1;
        building.layer = spec.Layer;
        building.category = spec.Category;
        building.horizontalDraggable = false;
        building.verticalDraggable = false;
        building.runtimeArchetype =
            BuildingRuntimeArchetypeKindExtensions.FromComponentType(spec.RuntimeType);
        building.tiles = null;
        building.unlocked = false;

        BuildingAbilityCollection abilities = new BuildingAbilityCollection();
        abilities.Add(new BuildingFacilityPartAbility { code = spec.Code });
        abilities.Add(new BuildingEconomyAbility
        {
            constructionValue = 60 + spec.Width * 35,
            maintenance = 0,
            unlockPhase = 3,
            demolitionRefundRate = 0.5f
        });
        BuildingAbility[] authoredAbilities = spec.CreateAbilities?.Invoke()
            ?.Where(ability => ability != null)
            .ToArray()
            ?? Array.Empty<BuildingAbility>();
        float baseWork = ResolveConstructionBaseWork(authoredAbilities);
        float footprint = Mathf.Clamp(
            1f + 0.30f * (Mathf.Max(1, spec.Width) - 1),
            1f,
            2.5f);
        float capability = Mathf.Clamp(
            1f + 0.10f * Mathf.Max(0, authoredAbilities.Length - 1),
            1f,
            1.5f);
        float constructionWork = RoundTo(baseWork * footprint * capability, 4f);
        BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
        {
            constructionWorkRequired = constructionWork,
            repairWorkRequired = RoundTo(constructionWork * 0.30f, 2f),
            cleanWorkRequired = RoundTo(
                Mathf.Clamp(constructionWork * 0.05f, 6f, 28f),
                2f),
            operateWorkRequired = 10f
        };
        workAmount.SetConstructionMaterials(
            ResolveConstructionMaterials(spec, authoredAbilities));
        abilities.Add(workAmount);
        foreach (BuildingAbility ability in authoredAbilities)
        {
            abilities.Add(ability);
        }

        abilities.EnsureStableIds();
        building.ReplaceAbilities(abilities);
        EnsureFacility(building)
            .AddSupportedWorkTypeId(BuiltInWorkTypeIds.Repair);
        if (building.GetAbility<BuildingUtilityConnectionAbility>() is
            BuildingUtilityConnectionAbility utility
            && (utility.channels
                    & (UtilityChannel.CleanWater
                       | UtilityChannel.Wastewater))
                != 0)
        {
            EnsureFacility(building)
                .AddSupportedWorkTypeId(BuiltInWorkTypeIds.Plumbing);
        }

        if (building.GetAbility<BuildingPowerProducerAbility>() is
            BuildingPowerProducerAbility producer
            && producer.requiresFuel)
        {
            EnsureFacility(building)
                .AddSupportedWorkTypeId(BuiltInWorkTypeIds.Refuel);
        }

        building.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(building);
    }

    private static IReadOnlyList<ItemAmountDefinition> ResolveConstructionMaterials(
        Spec spec,
        IReadOnlyList<BuildingAbility> authoredAbilities)
    {
        List<ItemAmountDefinition> result = new();
        void Add(string itemId, int amount)
        {
            if (amount <= 0)
                return;
            int index = result.FindIndex(value =>
                string.Equals(value.ItemId, itemId, StringComparison.Ordinal));
            if (index >= 0)
            {
                ItemAmountDefinition existing = result[index];
                result[index] = new ItemAmountDefinition(
                    itemId,
                    existing.Amount + amount);
            }
            else
            {
                result.Add(new ItemAmountDefinition(itemId, amount));
            }
        }

        BuildingUtilityConnectionAbility utility = authoredAbilities
            .OfType<BuildingUtilityConnectionAbility>()
            .FirstOrDefault();
        bool conveyor = authoredAbilities.Any(ability =>
            ability is BuildingConveyorSegmentAbility
                or BuildingConveyorPortAbility
                or BuildingConveyorOverflowAbility);
        bool automation = authoredAbilities.Any(ability =>
            ability is BuildingAutomationAbility);
        if (conveyor)
        {
            Add("material:steel-ingot", 2 + Mathf.Max(1, spec.Width));
            Add("material:iron-ingot", 2);
            Add("component:machine-parts", 1);
            if (authoredAbilities.Any(ability =>
                    ability is BuildingConveyorOverflowAbility)
                || spec.Width > 1)
                Add("component:precision-parts", 1);
        }
        else if (utility != null)
        {
            Add("material:stone-block", 2 + Mathf.Max(1, spec.Width));
            Add("material:iron-ingot", 2 + Mathf.Max(0, spec.Width - 1));
            if ((utility.channels & UtilityChannel.Power) != 0)
                Add("material:cloth", 1);
            if ((utility.channels & (UtilityChannel.CleanWater | UtilityChannel.Wastewater))
                == (UtilityChannel.CleanWater | UtilityChannel.Wastewater))
                Add("component:machine-parts", 1);
        }
        else
        {
            Add("material:stone-block", 6 + Mathf.Max(0, spec.Width - 1) * 2);
            Add("material:steel-ingot", 4 + Mathf.Max(1, spec.Width));
            Add("component:machine-parts", 3);
            if (authoredAbilities.Any(ability =>
                    ability is BuildingPowerProducerAbility
                        or BuildingPowerStorageAbility
                        or BuildingCircuitBreakerAbility)
                || automation)
                Add("component:precision-parts", 1);
        }
        if (automation)
        {
            Add("component:precision-parts", 2);
            Add("component:engineering-drawing", 1);
        }
        if (string.Equals(spec.Code, "I13", StringComparison.Ordinal)
            || string.Equals(spec.Code, "I17", StringComparison.Ordinal)
            || string.Equals(spec.Code, "I03", StringComparison.Ordinal))
        {
            Add("component:rune-conductor", 2);
            Add("material:mana-alloy", 1);
        }
        return result;
    }

    private static float ResolveConstructionBaseWork(
        IReadOnlyList<BuildingAbility> authoredAbilities)
    {
        return authoredAbilities.Any(ability =>
                ability is BuildingUtilityConnectionAbility
                    or BuildingWaterFixtureAbility)
            ? 160f
            : 280f;
    }

    private static float RoundTo(float value, float step) =>
        Mathf.Max(step, Mathf.Round(value / step) * step);

    private static void PatchSanitationFixtures()
    {
        PatchBuilding("H01", building =>
        {
            MergeUtilityChannels(
                building,
                UtilityChannel.CleanWater | UtilityChannel.Wastewater);
            Replace(building, new BuildingWaterFixtureAbility
            {
                cleanWaterPerUse = 0.25f,
                wastewaterPerUse = 0.25f,
                minimumQuality = WorldWaterQuality.Clean,
                allowsManualWaterFallback = true,
                allowsDryFallback = true,
                manualWasteItemId = "resource:manure"
            });
        });
        PatchBuilding("H03", building =>
        {
            MergeUtilityChannels(
                building,
                UtilityChannel.CleanWater | UtilityChannel.Wastewater);
            Replace(building, new BuildingWaterFixtureAbility
            {
                cleanWaterPerUse = 0.15f,
                wastewaterPerUse = 0.15f,
                minimumQuality = WorldWaterQuality.Clean,
                allowsManualWaterFallback = true,
                manualWasteItemId = IndustrialItemDefinitions.SludgeId
            });
        });
        PatchBuilding("H04", building =>
        {
            MergeUtilityChannels(
                building,
                UtilityChannel.CleanWater | UtilityChannel.Wastewater);
            Replace(building, new BuildingWaterFixtureAbility
            {
                cleanWaterPerUse = 1f,
                wastewaterPerUse = 1f,
                minimumQuality = WorldWaterQuality.Clean,
                allowsManualWaterFallback = false
            });
        });
        PatchBuilding("H07", building =>
        {
            MergeUtilityChannels(building, UtilityChannel.Wastewater);
        });
    }

    private static void PatchProductionFacilities()
    {
        foreach (BuildingSO building in LoadAllBuildings()
                     .Where(building =>
                         building.Facility?.SupportsWork(
                             BuiltInWorkTypeIds.Craft) == true
                         || building.Facility?.SupportsWork(
                             BuiltInWorkTypeIds.Cook) == true
                         || building.Facility?.SupportsWork(
                             BuiltInWorkTypeIds.Butcher) == true))
        {
            Replace(building, new BuildingUtilityConnectionAbility
            {
                channels = UtilityChannel.Power,
                maxThroughput = 25f
            });
            Replace(building, new BuildingPowerConsumerAbility
            {
                demandPerSecond = 5f,
                priority = PowerPriority.Production,
                minimumSupplyFraction = 0.75f
            });
            Replace(building, new BuildingAutomationAbility
            {
                maximumMode = AutomationMode.Automatic,
                assistedPowerDemand = 2f,
                automaticPowerDemand = 5f,
                assistedWorkMultiplier = 1.35f,
                automaticWorkPerSecond = 1f,
                automaticQualityCap = 0.75f,
                maintenancePerGameHour = 1f
            });
            Replace(building, new BuildingConveyorPortAbility
            {
                mode = ConveyorPortMode.Both,
                destinationId = string.Empty,
                capacity = 4
            });
            FinalizeBuilding(building);
        }
    }

    private static void PatchProcessFluidConsumers()
    {
        foreach (BuildingSO building in LoadAllBuildings())
        {
            if (!ApplyProcessFluidConsumerOverlay(building))
            {
                continue;
            }

            FinalizeBuilding(building);
        }
    }

    public static bool ApplyProcessFluidConsumerOverlay(BuildingSO building)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        bool cooking = building.Facility?.SupportsWork(
                BuiltInWorkTypeIds.Cook)
            == true
            || building.GetAbility<BuildingCookingAbility>() != null;
        bool surgery = building.Facility?.SupportsWork(
                BuiltInWorkTypeIds.Surgery)
            == true
            || building.GetAbility<BuildingSurgeryTableAbility>() != null
            || building.GetAbility<BuildingAnatomyTableAbility>() != null
            || building.GetAbility<BuildingTransplantSupportAbility>() != null
            || building.GetAbility<BuildingArcaneSurgeryAbility>() != null;
        if (!cooking && !surgery)
        {
            return false;
        }

        MergeUtilityChannels(
            building,
            UtilityChannel.CleanWater | UtilityChannel.Wastewater);
        List<string> workTypeIds = new List<string>();
        if (cooking)
        {
            workTypeIds.Add(BuiltInWorkTypeIds.Cook.Value);
        }

        if (surgery)
        {
            workTypeIds.Add(BuiltInWorkTypeIds.Surgery.Value);
        }

        BuildingProcessFluidAbility processFluid =
            building.GetAbility<BuildingProcessFluidAbility>();
        if (processFluid == null)
        {
            processFluid = new BuildingProcessFluidAbility();
            building.AbilityModules.Add(processFluid);
        }

        processFluid.workTypeIds = workTypeIds.ToArray();
        processFluid.cleanWaterPerCycle = surgery && !cooking ? 0.2f : 0.25f;
        processFluid.wastewaterPerCycle = surgery && !cooking ? 0.2f : 0.25f;
        processFluid.wastewaterComposition = surgery && !cooking
            ? ProcessWastewaterComposition.MedicalEffluent
            : ProcessWastewaterComposition.SanitaryWashwater;
        processFluid.minimumQuality = WorldWaterQuality.Clean;
        processFluid.allowsManualWaterFallback = true;
        EnsureFacility(building)
            .AddSupportedWorkTypeId(BuiltInWorkTypeIds.Plumbing);
        return true;
    }

    private static void MergeUtilityChannels(
        BuildingSO building,
        UtilityChannel channels)
    {
        BuildingUtilityConnectionAbility utility =
            building.GetAbility<BuildingUtilityConnectionAbility>();
        if (utility == null)
        {
            utility = new BuildingUtilityConnectionAbility
            {
                maxThroughput = 20f,
                normallyOpen = true
            };
            building.AbilityModules.Add(utility);
        }

        utility.channels |= channels;
        if ((channels
                & (UtilityChannel.CleanWater
                   | UtilityChannel.Wastewater))
            != 0)
        {
            EnsureFacility(building)
                .AddSupportedWorkTypeId(BuiltInWorkTypeIds.Plumbing);
        }
    }

    private static FacilityData EnsureFacility(BuildingSO building)
    {
        FacilityData facility = building.Facility ?? new FacilityData();
        if (building.Facility == null)
        {
            building.Facility = facility;
        }

        return facility;
    }

    private static void PatchBuilding(
        string code,
        Action<BuildingSO> patch)
    {
        BuildingSO building = LoadAllBuildings().FirstOrDefault(candidate =>
            string.Equals(
                candidate.GetAbility<BuildingFacilityPartAbility>()?.code,
                code,
                StringComparison.Ordinal));
        if (building == null)
        {
            return;
        }

        patch(building);
        FinalizeBuilding(building);
    }

    private static void Replace<TAbility>(
        BuildingSO building,
        TAbility ability)
        where TAbility : BuildingAbility
    {
        building.AbilityModules.Remove<TAbility>();
        building.AbilityModules.Add(ability);
    }

    private static void FinalizeBuilding(BuildingSO building)
    {
        building.AbilityModules.EnsureStableIds();
        building.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(building);
    }

    private static BuildingSO[] LoadAllBuildings()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
    }

    private static IReadOnlyList<Spec> CreateSpecs()
    {
        Color32 copper = new Color32(174, 103, 55, 255);
        Color32 iron = new Color32(97, 112, 122, 255);
        Color32 water = new Color32(53, 150, 190, 255);
        Color32 waste = new Color32(100, 126, 72, 255);
        Color32 mana = new Color32(115, 70, 168, 255);
        Color32 belt = new Color32(64, 72, 79, 255);
        Color32 warning = new Color32(218, 157, 49, 255);
        return new[]
        {
            Utility("U01", 9801, "전력선", "research:industry:distribution",
                copper, warning, UtilityChannel.Power),
            Utility("U02", 9802, "상수관", "research:plumbing:basics",
                water, new Color32(129, 220, 241, 255), UtilityChannel.CleanWater),
            Utility("U03", 9803, "하수관", "research:plumbing:sewer",
                waste, new Color32(173, 190, 92, 255), UtilityChannel.Wastewater),
            Utility("U04", 9804, "통합 기반 덕트", "research:industry:safety",
                iron, mana, UtilityChannel.Power
                    | UtilityChannel.CleanWater
                    | UtilityChannel.Wastewater),

            Machine("I01", 9810, "증기 발전기", 3, "research:industry:steam-power",
                iron, copper,
                new BuildingPowerProducerAbility
                {
                    productionPerSecond = 18f,
                    requiresFuel = true,
                    secondsPerFuel = 60f
                }),
            Machine("I02", 9811, "수차 발전기", 3, "research:industry:waterwheel",
                water, copper,
                new BuildingPowerProducerAbility
                {
                    productionPerSecond = 10f,
                    requiresFuel = false
                }),
            Machine("I03", 9812, "마나 발전기", 2, "research:industry:mana-power",
                mana, new Color32(80, 220, 213, 255),
                new BuildingPowerProducerAbility
                {
                    productionPerSecond = 32f,
                    requiresFuel = true,
                    fuelItemId = "resource:mana-crystal",
                    secondsPerFuel = 90f
                }),
            Machine("I04", 9813, "축전지", 2, "research:industry:storage",
                iron, warning,
                new BuildingPowerStorageAbility
                {
                    capacity = 240f,
                    transferPerSecond = 30f,
                    efficiency = 0.92f
                }),
            Machine("I05", 9814, "회로 차단기", 1, "research:industry:breakers",
                copper, warning,
                new BuildingCircuitBreakerAbility
                {
                    overloadTolerance = 1.15f,
                    tripHeat = 100f
                }),
            Machine("I06", 9815, "변압 제어반", 2, "research:industry:transformers",
                iron, warning,
                new BuildingCircuitBreakerAbility
                {
                    overloadTolerance = 1.3f,
                    tripHeat = 130f
                }),
            Machine("I07", 9816, "전동 양수 펌프", 2, "research:plumbing:pumped-water",
                water, copper,
                new BuildingPowerConsumerAbility
                {
                    demandPerSecond = 4f,
                    priority = PowerPriority.Critical
                },
                new BuildingWaterProducerAbility
                {
                    quality = WorldWaterQuality.Clean,
                    productionPerSecond = 0.75f,
                    requiresPower = true
                }),
            Machine("I08", 9817, "상수 탱크", 2, "research:plumbing:storage-valves",
                water, iron,
                new BuildingWaterStorageAbility
                {
                    channels = UtilityChannel.CleanWater,
                    cleanWaterCapacity = 120f,
                    wastewaterCapacity = 0f
                }),
            Machine("I09", 9818, "오수 탱크", 2, "research:plumbing:sewer",
                waste, iron,
                new BuildingWaterStorageAbility
                {
                    channels = UtilityChannel.Wastewater,
                    cleanWaterCapacity = 0f,
                    wastewaterCapacity = 140f
                }),
            Machine("I10", 9819, "물통 충전소", 2, "research:plumbing:pumped-water",
                water, warning,
                new BuildingUtilityConnectionAbility
                {
                    channels =
                        UtilityChannel.Power | UtilityChannel.CleanWater,
                    maxThroughput = 10f
                },
                new BuildingPowerConsumerAbility
                {
                    demandPerSecond = 1.5f,
                    priority = PowerPriority.Critical
                },
                new BuildingWaterContainerTransferAbility
                {
                    waterPerBatch = 1f,
                    secondsPerBatch = 4f,
                    bottleTargetStock = 10,
                    requiresPower = true
                }),
            Processor("I11", 9820, "오수 침전조", "research:plumbing:settling",
                waste, water, 10f, 6f, WorldWaterQuality.Unsafe, false),
            Processor("I12", 9821, "소독 정수기", "research:plumbing:reuse",
                water, new Color32(218, 226, 217, 255),
                10f, 8.5f, WorldWaterQuality.Clean, true),
            Processor("I13", 9822, "룬 정화 시설", "research:plumbing:rune-purification",
                mana, water, 10f, 9f, WorldWaterQuality.Clean, true),
            Shower("I14", 9823, "샤워 시설", "research:plumbing:flush-sanitation",
                water, iron),
            Machine("I15", 9824, "전기 아크등", 1,
                "research:industry:electric-lighting", iron, warning,
                new BuildingUtilityConnectionAbility
                {
                    channels = UtilityChannel.Power,
                    maxThroughput = 4f
                },
                new BuildingPowerConsumerAbility
                {
                    demandPerSecond = 1.5f,
                    priority = PowerPriority.Essential
                },
                new BuildingLightingAbility
                {
                    intensity = 1.2f,
                    radius = 5.5f
                }),
            Machine("I16", 9825, "전기 제련 도가니", 2,
                "research:industry:electric-smelting", iron, copper,
                new BuildingUtilityConnectionAbility
                {
                    channels = UtilityChannel.Power,
                    maxThroughput = 16f
                },
                new BuildingPowerConsumerAbility
                {
                    demandPerSecond = 7f,
                    priority = PowerPriority.Production
                },
                new BuildingProductionAbility
                {
                    outputCategory = StockCategory.General,
                    amount = 1
                }),
            Machine("I17", 9826, "룬 조율실", 2,
                "research:equipment:rune-module-tuning", mana, warning,
                new BuildingUtilityConnectionAbility
                {
                    channels = UtilityChannel.Power,
                    maxThroughput = 20f
                },
                new BuildingPowerConsumerAbility
                {
                    demandPerSecond = 9f,
                    priority = PowerPriority.Production
                },
                new BuildingSemanticTagsAbility
                {
                    tags = new[]
                    {
                        "industrial-infrastructure",
                        "research:equipment:rune-module-tuning",
                        "workstation:v3:rune-tuning"
                    }
                },
                new BuildingProductionWorkstationAbility
                {
                    workstationTag = "workstation:v3:rune-tuning",
                    stockSensorInstallationItemId =
                        "component:stock-sensor-panel"
                },
                new BuildingProductionBufferAbility
                {
                    defaultBatchCapacity = 4,
                    physicalOutputBufferCycleCapacity = 4
                },
                new BuildingFacilityAbility
                {
                    settings = new FacilityData
                    {
                        roles = FacilityRole.Research,
                        capacity = 1,
                        useDuration = 2f,
                        requiredWorkers = 1,
                        disabledWhenDamaged = true
                    }
                }),
            Machine("I18", 9827, "계보 기록실", 2,
                "research:equipment:lineage-binding", iron, mana,
                new BuildingSemanticTagsAbility
                {
                    tags = new[]
                    {
                        "industrial-infrastructure",
                        "research:equipment:lineage-binding",
                        "workstation:v3:lineage-archive"
                    }
                },
                new BuildingProductionWorkstationAbility
                {
                    workstationTag = "workstation:v3:lineage-archive",
                    stockSensorInstallationItemId =
                        "component:stock-sensor-panel"
                },
                new BuildingProductionBufferAbility
                {
                    defaultBatchCapacity = 4,
                    physicalOutputBufferCycleCapacity = 4
                },
                new BuildingFacilityAbility
                {
                    settings = new FacilityData
                    {
                        roles = FacilityRole.Research,
                        capacity = 1,
                        useDuration = 2f,
                        requiredWorkers = 1,
                        disabledWhenDamaged = true
                    }
                }),

            Belt("C01R", 9840, "컨베이어 우향", "research:industry:conveyor",
                Vector2Int.right, 1f, belt, warning),
            Belt("C01L", 9841, "컨베이어 좌향", "research:industry:conveyor",
                Vector2Int.left, 1f, belt, warning),
            Belt("C01U", 9842, "컨베이어 상향", "research:industry:conveyor",
                Vector2Int.up, 1f, belt, warning),
            Belt("C01D", 9843, "컨베이어 하향", "research:industry:conveyor",
                Vector2Int.down, 1f, belt, warning),
            ConveyorPort("C02", 9844, "컨베이어 입력기", "research:industry:ports",
                ConveyorPortMode.Input, Vector2Int.right, belt, water),
            ConveyorPort("C03", 9845, "컨베이어 출력기", "research:industry:ports",
                ConveyorPortMode.Output, Vector2Int.zero, belt, copper),
            ConveyorSegment("C04", 9846, "컨베이어 분배기",
                "research:industry:junctions", 1f, 2,
                new[] { Vector2Int.right, Vector2Int.up }, belt, warning),
            ConveyorSegment("C05", 9847, "컨베이어 합류기",
                "research:industry:junctions", 1f, 2,
                new[] { Vector2Int.right }, belt, copper),
            ConveyorSegment("C06", 9848, "컨베이어 필터",
                "research:industry:filters", 1f, 1,
                new[] { Vector2Int.right }, belt, water),
            ConveyorSegment("C07", 9849, "우선순위 게이트",
                "research:industry:priority-gates", 1f, 1,
                new[] { Vector2Int.right }, belt, warning),
            ConveyorSegment("C08", 9850, "층간 물류 리프트",
                "research:industry:lifts", 0.8f, 2,
                new[] { Vector2Int.up }, iron, mana),
            Overflow("C09", 9851, "오버플로 배출 게이트",
                "research:industry:overflow", belt, warning),
            ConveyorSegment("C10", 9852, "고속 컨베이어",
                "research:industry:high-speed-belts", 2f, 2,
                new[] { Vector2Int.right }, iron, warning),
            Machine("A01", 9860, "자동화 제어반", 2,
                "research:industry:automatic-bills", iron, mana,
                new BuildingPowerConsumerAbility
                {
                    demandPerSecond = 3f,
                    priority = PowerPriority.Production
                })
        };
    }

    private static Spec Utility(
        string code,
        int id,
        string name,
        string research,
        Color32 baseColor,
        Color32 accent,
        UtilityChannel channels)
    {
        Spec spec = BaseSpec(code, id, name, research, baseColor, accent,
            GridLayer.Utility, BuildingCategory.Resource,
            new BuildingUtilityConnectionAbility
            {
                channels = channels,
                maxThroughput = 100f
            });
        // Utility lines own repair/plumbing worker slots. A generic
        // BuildableObject cannot enter the production IWorkableFacility
        // candidate/admission path even when its FacilityData advertises the
        // work type, leaving real maintenance demand permanently invisible.
        spec.RuntimeType = typeof(Facility);
        return spec;
    }

    private static Spec Machine(
        string code,
        int id,
        string name,
        int width,
        string research,
        Color32 baseColor,
        Color32 accent,
        params BuildingAbility[] abilities)
    {
        Spec spec = BaseSpec(code, id, name, research, baseColor, accent,
            GridLayer.Building, BuildingCategory.Resource, abilities);
        spec.Width = width;
        return spec;
    }

    private static Spec Processor(
        string code,
        int id,
        string name,
        string research,
        Color32 baseColor,
        Color32 accent,
        float input,
        float output,
        WorldWaterQuality quality,
        bool powered)
    {
        List<BuildingAbility> abilities = new List<BuildingAbility>
        {
            new BuildingUtilityConnectionAbility
            {
                channels = UtilityChannel.CleanWater
                    | UtilityChannel.Wastewater
                    | (powered ? UtilityChannel.Power : UtilityChannel.None)
            },
            new BuildingWastewaterProcessorAbility
            {
                wastewaterInput = input,
                waterOutput = output,
                outputQuality = quality,
                requiresPower = powered,
                sludgeItemId = "industrial:sludge",
                sludgeAmount = 1,
                secondsPerBatch = powered ? 8f : 14f
            }
        };
        if (powered)
        {
            abilities.Add(new BuildingPowerConsumerAbility
            {
                demandPerSecond = 4f,
                priority = PowerPriority.Essential
            });
        }

        return Machine(code, id, name, 3, research, baseColor, accent,
            abilities.ToArray());
    }

    private static Spec Shower(
        string code,
        int id,
        string name,
        string research,
        Color32 baseColor,
        Color32 accent)
    {
        Spec spec = Machine(code, id, name, 1, research, baseColor, accent,
            new BuildingUtilityConnectionAbility
            {
                channels =
                    UtilityChannel.CleanWater | UtilityChannel.Wastewater,
                maxThroughput = 10f
            },
            new BuildingWaterFixtureAbility
            {
                cleanWaterPerUse = 0.45f,
                wastewaterPerUse = 0.45f,
                minimumQuality = WorldWaterQuality.Clean,
                allowsManualWaterFallback = false
            },
            new BuildingFacilityAbility
            {
                settings = CreateShowerFacilityData()
            },
            new BuildingNeedRecoveryAbility
            {
                recovery = new FacilityNeedRecoveryData
                {
                    hygiene = 72f,
                    mood = 4f
                }
            });
        spec.RuntimeType = typeof(Facility);
        spec.Category = BuildingCategory.Special;
        return spec;
    }

    private static FacilityData CreateShowerFacilityData()
    {
        FacilityData data = new FacilityData
        {
            roles = FacilityRole.Hygiene,
            capacity = 1,
            useDuration = 1.5f,
            requiredWorkers = 0
        };
        data.AddSupportedWorkTypeIds(new[]
        {
            BuiltInWorkTypeIds.Clean,
            BuiltInWorkTypeIds.Repair,
            BuiltInWorkTypeIds.Plumbing
        });
        return data;
    }

    private static Spec Belt(
        string code,
        int id,
        string name,
        string research,
        Vector2Int direction,
        float speed,
        Color32 baseColor,
        Color32 accent)
    {
        return ConveyorSegment(code, id, name, research, speed, 1,
            new[] { direction }, baseColor, accent);
    }

    private static Spec ConveyorPort(
        string code,
        int id,
        string name,
        string research,
        ConveyorPortMode mode,
        Vector2Int direction,
        Color32 baseColor,
        Color32 accent)
    {
        List<BuildingAbility> abilities = ConveyorBaseAbilities(1f, 2,
            direction == Vector2Int.zero
                ? Array.Empty<Vector2Int>()
                : new[] { direction });
        abilities.Add(new BuildingConveyorPortAbility
        {
            mode = mode,
            capacity = 4
        });
        return BaseSpec(code, id, name, research, baseColor, accent,
            GridLayer.Conveyor, BuildingCategory.Production,
            abilities.ToArray());
    }

    private static Spec ConveyorSegment(
        string code,
        int id,
        string name,
        string research,
        float speed,
        int capacity,
        Vector2Int[] directions,
        Color32 baseColor,
        Color32 accent)
    {
        return BaseSpec(code, id, name, research, baseColor, accent,
            GridLayer.Conveyor, BuildingCategory.Production,
            ConveyorBaseAbilities(speed, capacity, directions).ToArray());
    }

    private static Spec Overflow(
        string code,
        int id,
        string name,
        string research,
        Color32 baseColor,
        Color32 accent)
    {
        List<BuildingAbility> abilities = ConveyorBaseAbilities(
            1f,
            2,
            Array.Empty<Vector2Int>());
        abilities.Add(new BuildingConveyorOverflowAbility
        {
            defaultPolicy =
                ConveyorOverflowPolicy.ReserveWarehouseThenLoose,
            stallSeconds = 30f
        });
        return BaseSpec(code, id, name, research, baseColor, accent,
            GridLayer.Conveyor, BuildingCategory.Production,
            abilities.ToArray());
    }

    private static List<BuildingAbility> ConveyorBaseAbilities(
        float speed,
        int capacity,
        Vector2Int[] directions)
    {
        return new List<BuildingAbility>
        {
            new BuildingUtilityConnectionAbility
            {
                channels = UtilityChannel.Power,
                maxThroughput = 10f
            },
            new BuildingPowerConsumerAbility
            {
                demandPerSecond = Mathf.Max(0.25f, speed * 0.5f),
                priority = PowerPriority.Production,
                minimumSupplyFraction = 0.5f
            },
            new BuildingConveyorSegmentAbility
            {
                speed = speed,
                capacity = capacity,
                outputDirections = directions ?? Array.Empty<Vector2Int>(),
                requiresPower = true
            }
        };
    }

    private static Spec BaseSpec(
        string code,
        int id,
        string name,
        string research,
        Color32 baseColor,
        Color32 accent,
        GridLayer layer,
        BuildingCategory category,
        params BuildingAbility[] abilities)
    {
        return new Spec
        {
            Code = code,
            Id = id,
            Name = name,
            ResearchId = research,
            Layer = layer,
            Category = category,
            BaseColor = baseColor,
            AccentColor = accent,
            CreateAbilities = () => abilities ?? Array.Empty<BuildingAbility>()
        };
    }

    private static void WriteSprite(Spec spec, string path)
    {
        int width = Mathf.Max(16, spec.Width * 16);
        const int height = 32;
        Color32[] pixels = Enumerable.Repeat(
                new Color32(0, 0, 0, 0),
                width * height)
            .ToArray();
        void Set(int x, int y, Color32 color)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                pixels[y * width + x] = color;
            }
        }

        int inset = spec.Layer is GridLayer.Utility or GridLayer.Conveyor
            ? 3
            : 2;
        int top = spec.Layer is GridLayer.Utility or GridLayer.Conveyor
            ? 12
            : 28;
        for (int y = inset; y <= top; y++)
        {
            for (int x = inset; x < width - inset; x++)
            {
                bool edge = x == inset || x == width - inset - 1
                    || y == inset || y == top;
                Set(x, y, edge ? new Color32(28, 31, 36, 255) : spec.BaseColor);
            }
        }

        int marker = Math.Abs(spec.Code.GetHashCode()) % 4;
        for (int x = inset + 3 + marker; x < width - inset - 2; x += 6)
        {
            for (int y = inset + 3; y < top - 2; y += 5)
            {
                Set(x, y, spec.AccentColor);
                Set(x + 1, y, spec.AccentColor);
            }
        }

        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false);
        texture.name = spec.Code;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void ConfigureSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
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

    private static string Sanitize(string value)
    {
        return string.Concat((value ?? string.Empty)
            .Select(character => char.IsLetterOrDigit(character)
                ? character
                : '_'));
    }
}
#endif
