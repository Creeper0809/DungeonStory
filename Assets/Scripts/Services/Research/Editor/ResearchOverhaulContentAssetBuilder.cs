#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResearchOverhaulContentAssetBuilder
{
    private static readonly string[] FuneralFacilityTags =
    {
        "facility:funeral:dissolution-pool",
        "facility:funeral:orc-vigil",
        "facility:funeral:blood-incense",
        "facility:funeral:pack-farewell",
        "facility:funeral:contract-burning",
        "facility:funeral:tool-burial",
        "facility:funeral:spore-garden",
        "facility:funeral:sky-burial",
        "facility:funeral:core-rest",
        "facility:funeral:adventurer-burial"
    };
    private const string FacilityRoot =
        "Assets/Resources/SO/Building/ResearchOverhaul";
    private const string ItemRoot =
        "Assets/Resources/SO/Economy/Items/ResearchOverhaul";
    private const string RecipeRoot =
        "Assets/Resources/SO/Economy/Recipes/ResearchOverhaul";

    private readonly struct FacilitySpec
    {
        public FacilitySpec(
            string researchId,
            string name,
            string workstationTag,
            FacilityBomProfile bomProfile)
        {
            ResearchId = researchId;
            Name = name;
            WorkstationTag = workstationTag;
            BomProfile = bomProfile;
        }

        public string ResearchId { get; }
        public string Name { get; }
        public string WorkstationTag { get; }
        public FacilityBomProfile BomProfile { get; }
    }

    private enum FacilityBomProfile
    {
        ARecordDesk,
        BWorkbench,
        CLivingRoom,
        DMedicalRoom,
        EIndustrialLab,
        FRuneBiolab,
        GGreenhouse,
        HObservationTower,
        IFieldStation,
        JWaterworks,
        KSecureFixture,
        LIndustrialMachine,
        MPrecisionWorkshop,
        NDefenseInstallation,
        OPowderWorkshop,
        PServiceStation
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
        GameContentCatalogAssetBuilder.ReindexItemDefinitions();
        V23RecipeProcessClassAuthoring.NormalizeRecipeWorkUnder(RecipeRoot);
        V23MarketValueCalibrator.Apply();
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
            ResolveGameplayExecution(
                building.id,
                out FacilityUseClassification classification,
                out ResearchFacilityCommandKind command);
            building.ConfigureGameplayExecution(classification, command);

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
                    .Concat(string.Equals(
                            spec.WorkstationTag,
                            "workstation:v19:memorial",
                            StringComparison.Ordinal)
                        ? FuneralFacilityTags
                        : Array.Empty<string>())
                    .ToArray()
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
            if (string.Equals(
                    spec.ResearchId,
                    "research:agriculture:greenhouse-horticulture",
                    StringComparison.Ordinal))
            {
                BuildingCropPlotAbility cropPlot = new();
                cropPlot.Configure(
                    isIndoor: true,
                    growthRate: 1.5f,
                    waterRate: 0.75f,
                    compost: 1,
                    fuel: 1,
                    supplies: new[]
                    {
                        new ItemAmountDefinition(
                            "supply:greenhouse-nutrient",
                            1)
                    });
                abilities.Add(cropPlot);
            }
            else if (string.Equals(
                         spec.ResearchId,
                         "research:forestry:fungal",
                         StringComparison.Ordinal))
            {
                BuildingCropPlotAbility cropPlot = new();
                cropPlot.Configure(
                    isIndoor: true,
                    growthRate: 1.15f,
                    waterRate: 0.8f,
                    compost: 1,
                    fuel: 0,
                    supplies: new[]
                    {
                        new ItemAmountDefinition("supply:inoculated-log", 1)
                    });
                abilities.Add(cropPlot);
            }
            IReadOnlyList<ItemAmountDefinition> constructionMaterials =
                ResolveConstructionMaterials(spec);
            abilities.Add(new BuildingEconomyAbility
            {
                constructionCost = ResolveConstructionValue(spec.BomProfile),
                maintenance = ResolveMaintenanceCost(spec.BomProfile),
                unlockPhase = 1,
                demolitionRefundRate = 0.5f
            });
            FacilityData facility = ResolveFacilityData(spec);
            abilities.Add(new BuildingFacilityAbility
            {
                settings = facility
            });
            if (IsAgeTreatmentFacility(spec.ResearchId))
            {
                facility.AddSupportedWorkTypeId(BuiltInWorkTypeIds.Plumbing);
                abilities.Add(new BuildingUtilityConnectionAbility
                {
                    channels = UtilityChannel.CleanWater
                        | UtilityChannel.Wastewater,
                    maxThroughput = 20f,
                    normallyOpen = true
                });
                abilities.Add(new BuildingProcessFluidAbility
                {
                    workTypeIds = new[]
                    {
                        BuiltInWorkTypeIds.Surgery.Value
                    },
                    cleanWaterPerCycle = 0.2f,
                    wastewaterPerCycle = 0.2f,
                    minimumQuality = WorldWaterQuality.Clean,
                    allowsManualWaterFallback = true
                });
                abilities.Add(new BuildingSurgeryTableAbility
                {
                    allowedProcedureTags = SurgeryFacilityTag.AgeTreatment,
                    successBonus = 0.12f,
                    workSpeedMultiplier = 1f,
                    baseSterility = 0.45f,
                    patientSlots = 1
                });
            }
            abilities.Add(new BuildingRoomRequirementAbility());
            BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
            {
                constructionWorkRequired = ResolveFallbackConstructionWork(
                    classification,
                    command,
                    constructionMaterials.Count),
                repairWorkRequired = ResolveRepairWork(spec.BomProfile),
                cleanWorkRequired = ResolveCleaningWork(spec.BomProfile),
                operateWorkRequired = 12f
            };
            workAmount.SetConstructionMaterials(constructionMaterials);
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
            ValidateCultivationInputs(spec, building);
            EditorUtility.SetDirty(building);
        }
    }

    private static void ValidateCultivationInputs(
        FacilitySpec spec,
        BuildingSO building)
    {
        string requiredSupply = spec.ResearchId switch
        {
            "research:agriculture:greenhouse-horticulture" =>
                "supply:greenhouse-nutrient",
            "research:forestry:fungal" => "supply:inoculated-log",
            _ => string.Empty
        };
        if (requiredSupply.Length == 0)
        {
            return;
        }

        BuildingCropPlotAbility cropPlot =
            building.GetAbility<BuildingCropPlotAbility>();
        if (cropPlot == null
            || !cropPlot.Indoor
            || cropPlot.CycleSupplyInputs.All(value =>
                value == null
                || !string.Equals(
                    value.ItemId,
                    requiredSupply,
                    StringComparison.Ordinal)
                || value.Amount <= 0))
        {
            throw new InvalidOperationException(
                $"Research facility '{building.id}' does not consume its intended cultivation supply '{requiredSupply}'.");
        }
    }

    private static FacilityData ResolveFacilityData(FacilitySpec spec)
    {
        FacilityRole roles;
        FacilityWorkType workTypes;
        switch (spec.BomProfile)
        {
            case FacilityBomProfile.ARecordDesk:
                roles = FacilityRole.Administration;
                workTypes = FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.BWorkbench:
                roles = FacilityRole.Logistics;
                workTypes = FacilityWorkType.Craft | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.CLivingRoom:
                roles = FacilityRole.Rest | FacilityRole.Administration;
                workTypes = FacilityWorkType.Rest | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.DMedicalRoom:
                roles = FacilityRole.Medical;
                workTypes = FacilityWorkType.Treat | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.EIndustrialLab:
                roles = FacilityRole.Research | FacilityRole.Logistics;
                workTypes = FacilityWorkType.Research | FacilityWorkType.Craft
                    | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.FRuneBiolab:
                roles = FacilityRole.Medical | FacilityRole.Research;
                workTypes = FacilityWorkType.Treat | FacilityWorkType.Research
                    | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.GGreenhouse:
                roles = FacilityRole.Logistics;
                workTypes = FacilityWorkType.Sow | FacilityWorkType.Harvest
                    | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.HObservationTower:
                roles = FacilityRole.Research;
                workTypes = FacilityWorkType.Research | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.IFieldStation:
                roles = FacilityRole.Logistics;
                workTypes = FacilityWorkType.Craft | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.JWaterworks:
                roles = FacilityRole.Logistics;
                workTypes = FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.KSecureFixture:
                roles = FacilityRole.Security | FacilityRole.Logistics;
                workTypes = FacilityWorkType.Craft | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.LIndustrialMachine:
                roles = FacilityRole.Logistics;
                workTypes = FacilityWorkType.Craft | FacilityWorkType.Repair
                    | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.MPrecisionWorkshop:
                roles = FacilityRole.Research | FacilityRole.Logistics;
                workTypes = FacilityWorkType.Research | FacilityWorkType.Craft
                    | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.NDefenseInstallation:
                roles = FacilityRole.Security | FacilityRole.Logistics;
                workTypes = FacilityWorkType.Craft | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.OPowderWorkshop:
                roles = FacilityRole.Logistics;
                workTypes = FacilityWorkType.Craft | FacilityWorkType.Operate;
                break;
            case FacilityBomProfile.PServiceStation:
                roles = FacilityRole.Administration | FacilityRole.Logistics;
                workTypes = FacilityWorkType.Operate;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(spec),
                    spec.BomProfile,
                    "Research facilities require an explicit semantic BOM profile.");
        }

        if (IsAgeTreatmentFacility(spec.ResearchId))
        {
            roles = FacilityRole.Medical;
            workTypes = FacilityWorkType.Surgery | FacilityWorkType.Treat;
        }

        FacilityData facility = new FacilityData
        {
            roles = roles,
            capacity = 1,
            useDuration = 1.5f,
            requiredWorkers = 1,
            disabledWhenDamaged = true
        };
        facility.SetSupportedWorkTypeIds(ToWorkTypeIds(workTypes));
        return facility;
    }

    private static IEnumerable<WorkTypeId> ToWorkTypeIds(FacilityWorkType mask)
    {
        if ((mask & FacilityWorkType.Operate) != 0) yield return BuiltInWorkTypeIds.Operate;
        if ((mask & FacilityWorkType.Rest) != 0) yield return BuiltInWorkTypeIds.Rest;
        if ((mask & FacilityWorkType.Craft) != 0) yield return BuiltInWorkTypeIds.Craft;
        if ((mask & FacilityWorkType.Repair) != 0) yield return BuiltInWorkTypeIds.Repair;
        if ((mask & FacilityWorkType.Research) != 0) yield return BuiltInWorkTypeIds.Research;
        if ((mask & FacilityWorkType.Treat) != 0) yield return BuiltInWorkTypeIds.Treat;
        if ((mask & FacilityWorkType.Sow) != 0) yield return BuiltInWorkTypeIds.Sow;
        if ((mask & FacilityWorkType.Harvest) != 0) yield return BuiltInWorkTypeIds.Harvest;
        if ((mask & FacilityWorkType.Surgery) != 0) yield return BuiltInWorkTypeIds.Surgery;
    }

    private static bool IsAgeTreatmentFacility(string researchId) =>
        string.Equals(researchId, "research:medical:organ-regeneration", StringComparison.Ordinal)
        || string.Equals(researchId, "research:medical:blood-rejuvenation", StringComparison.Ordinal)
        || string.Equals(researchId, "research:medical:rune-hibernation", StringComparison.Ordinal)
        || string.Equals(researchId, "research:medical:whole-body-regeneration", StringComparison.Ordinal)
        || string.Equals(researchId, "research:medical:temporal-stasis", StringComparison.Ordinal);

    private static IReadOnlyList<ItemAmountDefinition>
        ResolveConstructionMaterials(FacilitySpec spec)
    {
        IReadOnlyList<ItemAmountDefinition> authored = spec.BomProfile switch
        {
            FacilityBomProfile.ARecordDesk => Materials(
                ("material:lumber", 6),
                ("material:treated-lumber", 2),
                ("material:iron-ingot", 2),
                ("material:paper", 4)),
            FacilityBomProfile.BWorkbench => Materials(
                ("material:stone-block", 8),
                ("material:treated-lumber", 6),
                ("material:iron-ingot", 4),
                ("component:machine-parts", 2),
                ("component:engineering-drawing", 1)),
            FacilityBomProfile.CLivingRoom => Materials(
                ("material:lumber", 10),
                ("material:treated-lumber", 4),
                ("material:cloth", 6),
                ("material:stone-block", 4)),
            FacilityBomProfile.DMedicalRoom => Materials(
                ("material:stone-block", 10),
                ("material:treated-lumber", 6),
                ("material:steel-ingot", 4),
                ("textile:sterile-cloth", 4),
                ("component:machine-parts", 2),
                ("resource:clean-water", 8)),
            FacilityBomProfile.EIndustrialLab => Materials(
                ("material:stone-block", 12),
                ("material:steel-ingot", 8),
                ("component:machine-parts", 4),
                ("component:precision-parts", 4),
                ("component:engineering-drawing", 2)),
            FacilityBomProfile.FRuneBiolab => Materials(
                ("material:stone-block", 16),
                ("material:steel-ingot", 10),
                ("component:precision-parts", 6),
                ("component:rune-conductor", 4),
                ("component:mana-shield-plate", 2),
                ("resource:mana-crystal", 4),
                ("component:engineering-drawing", 2)),
            FacilityBomProfile.GGreenhouse => Materials(
                ("material:treated-lumber", 12),
                ("material:stone-block", 8),
                ("material:iron-ingot", 6),
                ("resource:clean-water", 12),
                ("component:machine-parts", 2),
                ("material:cloth", 4)),
            FacilityBomProfile.HObservationTower => Materials(
                ("material:treated-lumber", 8),
                ("material:stone-block", 4),
                ("material:iron-ingot", 6),
                ("material:cloth", 2),
                ("component:machine-parts", 1)),
            FacilityBomProfile.IFieldStation => Materials(
                ("material:lumber", 6),
                ("material:treated-lumber", 3),
                ("material:iron-ingot", 2),
                ("material:rope", 2),
                ("material:cloth", 2)),
            FacilityBomProfile.JWaterworks => Materials(
                ("material:stone-block", 8),
                ("material:treated-lumber", 4),
                ("material:iron-ingot", 4),
                ("resource:clean-water", 4)),
            FacilityBomProfile.KSecureFixture => Materials(
                ("material:treated-lumber", 5),
                ("material:iron-ingot", 4),
                ("material:hardened-leather", 2),
                ("material:chain-mesh", 1)),
            FacilityBomProfile.LIndustrialMachine => Materials(
                ("material:stone-block", 10),
                ("material:steel-ingot", 6),
                ("component:machine-parts", 4),
                ("component:precision-parts", 1),
                ("component:engineering-drawing", 1)),
            FacilityBomProfile.MPrecisionWorkshop => Materials(
                ("material:stone-block", 6),
                ("material:treated-lumber", 4),
                ("material:steel-ingot", 4),
                ("component:precision-parts", 3),
                ("component:engineering-drawing", 1)),
            FacilityBomProfile.NDefenseInstallation => Materials(
                ("material:stone-block", 8),
                ("material:steel-ingot", 6),
                ("component:machine-parts", 3),
                ("material:cloth", 2)),
            FacilityBomProfile.OPowderWorkshop => Materials(
                ("material:stone-block", 10),
                ("material:iron-ingot", 4),
                ("material:treated-lumber", 2),
                ("component:machine-parts", 2),
                ("resource:clean-water", 2)),
            FacilityBomProfile.PServiceStation => Materials(
                ("material:treated-lumber", 6),
                ("material:iron-ingot", 2),
                ("material:cloth", 2),
                ("material:paper", 2)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(spec),
                spec.BomProfile,
                "Research facilities require an authored physical BOM.")
        };
        return AppendV21InstallationMaterials(spec, authored);
    }

    private static int ResolveConstructionValue(FacilityBomProfile profile) =>
        profile switch
        {
            FacilityBomProfile.ARecordDesk => 120,
            FacilityBomProfile.BWorkbench => 220,
            FacilityBomProfile.CLivingRoom => 160,
            FacilityBomProfile.DMedicalRoom => 320,
            FacilityBomProfile.EIndustrialLab => 420,
            FacilityBomProfile.FRuneBiolab => 650,
            FacilityBomProfile.GGreenhouse => 260,
            FacilityBomProfile.HObservationTower => 240,
            FacilityBomProfile.IFieldStation => 130,
            FacilityBomProfile.JWaterworks => 200,
            FacilityBomProfile.KSecureFixture => 280,
            FacilityBomProfile.LIndustrialMachine => 440,
            FacilityBomProfile.MPrecisionWorkshop => 400,
            FacilityBomProfile.NDefenseInstallation => 360,
            FacilityBomProfile.OPowderWorkshop => 300,
            FacilityBomProfile.PServiceStation => 180,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };

    private static int ResolveMaintenanceCost(FacilityBomProfile profile) =>
        profile switch
        {
            FacilityBomProfile.ARecordDesk or FacilityBomProfile.CLivingRoom
                or FacilityBomProfile.IFieldStation
                or FacilityBomProfile.PServiceStation => 1,
            FacilityBomProfile.BWorkbench or FacilityBomProfile.GGreenhouse
                or FacilityBomProfile.HObservationTower
                or FacilityBomProfile.JWaterworks
                or FacilityBomProfile.KSecureFixture => 2,
            FacilityBomProfile.DMedicalRoom or FacilityBomProfile.NDefenseInstallation
                or FacilityBomProfile.OPowderWorkshop => 3,
            FacilityBomProfile.EIndustrialLab or FacilityBomProfile.LIndustrialMachine
                or FacilityBomProfile.MPrecisionWorkshop => 4,
            FacilityBomProfile.FRuneBiolab => 6,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };

    private static float ResolveFallbackConstructionWork(
        FacilityUseClassification classification,
        ResearchFacilityCommandKind command,
        int materialKinds)
    {
        float baseWork = command switch
        {
            ResearchFacilityCommandKind.ResonanceTuning => 360f,
            ResearchFacilityCommandKind.AgingAssessment
                or ResearchFacilityCommandKind.BiologicalAgeMeasurement
                or ResearchFacilityCommandKind.GeriatricCare
                or ResearchFacilityCommandKind.ChronicCare
                or ResearchFacilityCommandKind.PathogenDiagnosis
                or ResearchFacilityCommandKind.Serology => 200f,
            _ => classification switch
            {
                FacilityUseClassification.Structure => 20f,
                FacilityUseClassification.Storage => 48f,
                FacilityUseClassification.Production => 110f,
                FacilityUseClassification.Service => 130f,
                FacilityUseClassification.Environment => 160f,
                FacilityUseClassification.Logistics => 280f,
                FacilityUseClassification.Combat => 180f,
                FacilityUseClassification.DomainCommand => 230f,
                FacilityUseClassification.EventVenue => 130f,
                FacilityUseClassification.Decoration => 28f,
                _ => 32f
            }
        };
        float materialComplexity = Mathf.Clamp(
            1f + Mathf.Max(0, materialKinds - 1) * 0.05f,
            1f,
            1.25f);
        return Mathf.Round(baseWork * materialComplexity / 4f) * 4f;
    }

    private static float ResolveRepairWork(FacilityBomProfile profile) =>
        Mathf.Max(8f, Mathf.Round(ResolveConstructionValue(profile) * 0.08f));

    private static float ResolveCleaningWork(FacilityBomProfile profile) =>
        profile is FacilityBomProfile.DMedicalRoom
            or FacilityBomProfile.FRuneBiolab
            or FacilityBomProfile.GGreenhouse
            or FacilityBomProfile.JWaterworks
            or FacilityBomProfile.OPowderWorkshop
            ? 12f
            : 8f;

    private static IReadOnlyList<ItemAmountDefinition> AppendV21InstallationMaterials(
        FacilitySpec spec,
        IEnumerable<ItemAmountDefinition> source)
    {
        List<ItemAmountDefinition> result = source
            .Where(value => value != null)
            .Select(value => new ItemAmountDefinition(value.ItemId, value.Amount))
            .ToList();
        string[] installationItems = spec.ResearchId switch
        {
            "research:climate:environment-control" =>
                new[] { "component:climate-control-manifold" },
            "research:commerce:retail" =>
                new[] { "component:price-board" },
            "research:housing:family-quarters" =>
                new[] { "component:room-partition-kit" },
            "research:defense:corridor-mechanisms" =>
                new[] { "component:corridor-detonator" },
            "research:medical:construct-core-engineering" =>
                new[] { "component:golem-core-case" },
            "research:industry:electric-lighting" =>
                new[] { "component:insulated-wiring" },
            "research:plumbing:reuse" =>
                new[] { "component:reclaimed-water-filter" },
            "research:plumbing:rune-purification" =>
                new[] { "component:rune-purification-crystal" },
            "research:survival:seasonal-storage" =>
                new[] { "component:sealed-seasonal-container" },
            "research:defense:siege-fortification" =>
                new[] { "component:siege-reinforcement-kit" },
            "research:industry:waterwheel" =>
                new[] { "component:waterwheel-drive-shaft" },
            _ => Array.Empty<string>()
        };
        foreach (string itemId in installationItems)
        {
            if (result.All(value => !string.Equals(
                    value.ItemId,
                    itemId,
                    StringComparison.Ordinal)))
            {
                result.Add(new ItemAmountDefinition(itemId, 1));
            }
        }
        return result;
    }

    private static IReadOnlyList<ItemAmountDefinition> Materials(
        params (string ItemId, int Amount)[] values) =>
        values.Select(value => new ItemAmountDefinition(value.ItemId, value.Amount)).ToArray();

    private static void ResolveGameplayExecution(
        int buildingId,
        out FacilityUseClassification classification,
        out ResearchFacilityCommandKind command)
    {
        command = buildingId switch
        {
            8801 => ResearchFacilityCommandKind.GatheringPreparation,
            8808 => ResearchFacilityCommandKind.BloodStageDrainage,
            8814 => ResearchFacilityCommandKind.LoggingPreparation,
            8815 => ResearchFacilityCommandKind.DirectionalFelling,
            8818 => ResearchFacilityCommandKind.SelectiveBreeding,
            8819 => ResearchFacilityCommandKind.StableHarnessing,
            8820 => ResearchFacilityCommandKind.WildlifeTaming,
            8829 => ResearchFacilityCommandKind.FlowMetering,
            8834 => ResearchFacilityCommandKind.WeaponPatternAccess,
            8850 => ResearchFacilityCommandKind.CropCalendar,
            8852 => ResearchFacilityCommandKind.SoilDiagnostics,
            8855 => ResearchFacilityCommandKind.BreedingSchedule,
            8856 => ResearchFacilityCommandKind.ClimateControl,
            8857 => ResearchFacilityCommandKind.HouseholdRegistry,
            8858 => ResearchFacilityCommandKind.NurseryCare,
            8860 => ResearchFacilityCommandKind.ClassroomEducation,
            8861 => ResearchFacilityCommandKind.SupervisedApprenticeship,
            8862 => ResearchFacilityCommandKind.GenerationArchive,
            8863 => ResearchFacilityCommandKind.AgingAssessment,
            8864 => ResearchFacilityCommandKind.BiologicalAgeMeasurement,
            8865 => ResearchFacilityCommandKind.GeriatricCare,
            8866 => ResearchFacilityCommandKind.ChronicCare,
            8873 => ResearchFacilityCommandKind.PathogenDiagnosis,
            8875 => ResearchFacilityCommandKind.Serology,
            8877 => ResearchFacilityCommandKind.EpidemicBoard,
            8878 => ResearchFacilityCommandKind.GeneticArchive,
            8880 => ResearchFacilityCommandKind.GeneticCounseling,
            8883 => ResearchFacilityCommandKind.FamilyPartition,
            8884 => ResearchFacilityCommandKind.GuardianRegistry,
            8886 => ResearchFacilityCommandKind.CorpseCare,
            8888 => ResearchFacilityCommandKind.ClimateMapping,
            8889 => ResearchFacilityCommandKind.ChronometricNavigation,
            8890 => ResearchFacilityCommandKind.SeedSelection,
            8895 => ResearchFacilityCommandKind.RetireeCare,
            8896 => ResearchFacilityCommandKind.MentorAcademy,
            8897 => ResearchFacilityCommandKind.ResonanceTuning,
            8898 => ResearchFacilityCommandKind.SecureTradeVault,
            8899 => ResearchFacilityCommandKind.DefenseControl,
            _ => ResearchFacilityCommandKind.None
        };

        classification = buildingId switch
        {
            8808 => FacilityUseClassification.EventVenue,
            8801 or 8814 or 8815 or 8819 or 8820 =>
                FacilityUseClassification.Logistics,
            8829 or 8850 or 8852 or 8856 or 8888 or 8889 =>
                FacilityUseClassification.Environment,
            8899 => FacilityUseClassification.Combat,
            8883 => FacilityUseClassification.Structure,
            8858 or 8865 or 8866 or 8886 or 8895 or 8898 =>
                FacilityUseClassification.Service,
            _ when command != ResearchFacilityCommandKind.None =>
                FacilityUseClassification.DomainCommand,
            _ => FacilityUseClassification.Production
        };
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
                ResolveGeneratedUnitPrice(spec),
                ResolveGeneratedUnitWeight(spec),
                spec.ItemId == PhysicalItemIds.EquipmentModule
                    ? 1
                    : DurableToolItemRules.TryGetMaximumDurability(spec.ItemId, out _)
                        ? 1
                        : spec.Kind == ResourceItemKind.Ammunition ? 120 : 50,
                spec.ResearchId);
            if (spec.ItemId == PhysicalItemIds.EquipmentModule
                || spec.ItemId == EquipmentProgressionItemIds.LineageSeal)
            {
                item.ConfigureMarketSaleRate(0f);
            }
            item.ConfigureFacilitySupply(0f, false, spec.SharedIntermediate);
            if (spec.ItemId == "medical:sterile-bandage")
            {
                item.ConfigureMedicine(true, 0.85f, 10f, 0f, 8f);
            }
            if (spec.ItemId == "medical:regenerative-medium")
            {
                item.ConfigureMedicine(true, 0.7f, 6f, 0f, 4f);
            }
            if (spec.ItemId.StartsWith("sample:antigen:", StringComparison.Ordinal))
            {
                item.ConfigurePathogenSample(
                    "disease:" + spec.ItemId.Substring("sample:antigen:".Length));
            }
            if (spec.ItemId.StartsWith("medicine:vaccine:", StringComparison.Ordinal))
            {
                item.ConfigureMedicine(false, 0.1f, 0f, 0f, 0f);
                item.ConfigureVaccine(
                    "disease:" + spec.ItemId.Substring("medicine:vaccine:".Length),
                    1);
            }
            if (spec.ItemId == "medical:whole-body-regeneration-medium")
            {
                item.ConfigureMedicalProcedureSupply("procedure:whole-body-regeneration");
            }
            if (spec.ItemId == "component:temporal-stasis-seal")
            {
                item.ConfigureMedicalProcedureSupply("procedure:temporal-stasis");
            }
            if (spec.ItemId == "supply:pest-lure")
            {
                item.ConfigureCropTreatment(CropTreatmentKind.PestLure);
            }
            if (spec.ItemId == "supply:botanical-pesticide")
            {
                item.ConfigureCropTreatment(CropTreatmentKind.BotanicalPesticide);
            }
            if (spec.ItemId == "supply:fungicide")
            {
                item.ConfigureCropTreatment(CropTreatmentKind.Fungicide);
            }
            EditorUtility.SetDirty(item);

            if (!spec.Craftable)
            {
                continue;
            }

            ProductionRecipeSO recipe =
                GetOrCreate<ProductionRecipeSO>(RecipePath(index, spec));
            recipe.id = 19101 + index;
            recipe.Configure(
                $"recipe:{spec.ItemId}",
                spec.Name,
                $"구체 재료를 사용해 {spec.Name}을(를) 생산한다.",
                spec.WorkstationTag,
                BuiltInWorkTypeIds.Craft.Value,
                spec.ResearchId,
                10f,
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
            ProductionFlowRole flowRole =
                spec.Inputs.Length == 0
                    ? ProductionFlowRole.Source
                    : ProductionFlowRole.Transform;
            recipe.ConfigureFlowRole(flowRole);
            ProductionProcessClass processClass =
                V23RecipeProcessClassAuthoring.Resolve(
                    spec.WorkstationTag,
                    BuiltInWorkTypeIds.Craft.Value,
                    flowRole,
                    spec.ItemId);
            recipe.ConfigureProcessClass(processClass);
            recipe.ConfigureBalanceWork(
                V23BalanceWorkCalculator.CalculateRecipeBaseWork(
                    recipe,
                    processClass));
            EditorUtility.SetDirty(recipe);
        }
    }

    private static FacilitySpec[] FacilitySpecs() => new[]
    {
        F("research:agriculture:gathering", "채집 바구니 작업대", "workstation:v3:gathering", FacilityBomProfile.IFieldStation),
        F("research:agriculture:irrigation", "중력식 수문", "workstation:v3:irrigation", FacilityBomProfile.JWaterworks),
        F("research:agriculture:subterranean", "동굴 재배 선반", "workstation:v3:subterranean", FacilityBomProfile.GGreenhouse),
        F("research:authority:prestige", "문장 깃발 제작대", "workstation:v3:heraldry", FacilityBomProfile.BWorkbench),
        F("research:authority:ritual", "의식 화로", "workstation:v3:ritual", FacilityBomProfile.BWorkbench),
        F("research:commerce:logistics", "운반 멜빵 걸이", "workstation:v3:logistics", FacilityBomProfile.BWorkbench),
        F("research:commerce:retail", "가격표 게시판", "workstation:v3:retail", FacilityBomProfile.ARecordDesk),
        F("research:control:blood-show", "피의 무대 배수구", "workstation:v3:blood-stage", FacilityBomProfile.JWaterworks),
        F("research:control:labor", "포로 작업 도구함", "workstation:v3:prison-labor", FacilityBomProfile.KSecureFixture),
        F("research:control:restraints", "강화 구속구 선반", "workstation:v3:restraint", FacilityBomProfile.KSecureFixture),
        F("research:control:show", "공연 소품 보관대", "workstation:v3:show", FacilityBomProfile.PServiceStation),
        F("research:defense:alliance-signals", "동맹 신호기", "workstation:v3:signals", FacilityBomProfile.NDefenseInstallation),
        F("research:forestry:fungal", "균사 재배 선반", "workstation:v3:fungal", FacilityBomProfile.GGreenhouse),
        F("research:forestry:logging", "벌목 키트 걸이", "workstation:v3:logging", FacilityBomProfile.IFieldStation),
        F("research:forestry:tools", "쐐기 도끼 작업대", "workstation:v3:forestry-tools", FacilityBomProfile.BWorkbench),
        F("research:forestry:treated", "방부 처리 목재대", "workstation:v3:treated-lumber", FacilityBomProfile.BWorkbench),
        F("research:husbandry:breeding", "번식 장부대", "workstation:v3:breeding", FacilityBomProfile.ARecordDesk),
        F("research:husbandry:selective", "혈통 촉진제 선반", "workstation:v3:selective", FacilityBomProfile.BWorkbench),
        F("research:husbandry:stable", "마구 선반", "workstation:v3:stable", FacilityBomProfile.IFieldStation),
        F("research:husbandry:taming", "조련용 고삐 걸이", "workstation:v3:taming", FacilityBomProfile.IFieldStation),
        F("research:industry:assisted-processing", "동력 공구날 연마대", "workstation:v3:machine-parts", FacilityBomProfile.LIndustrialMachine),
        F("research:industry:automatic-sanitation", "자동 세척기", "workstation:v3:sanitation", FacilityBomProfile.LIndustrialMachine),
        F("research:industry:rune-grid", "룬 버스 결합기", "workstation:v3:rune-conductor", FacilityBomProfile.FRuneBiolab),
        F("research:industry:defense-supply", "방어시설 장전기", "workstation:v3:defense-ammo", FacilityBomProfile.NDefenseInstallation),
        F("research:equipment:prototype-engineering", "시제품 연구실", "workstation:v3:prototype", FacilityBomProfile.MPrecisionWorkshop),
        F("research:equipment:material-testing", "재료 시험기", "workstation:v3:material-test", FacilityBomProfile.MPrecisionWorkshop),
        F("research:industry:factory-layout", "기계 기초대", "workstation:v3:factory-layout", FacilityBomProfile.LIndustrialMachine),
        F("research:industry:industrial-cooling", "냉각 매니폴드", "workstation:v3:cooling", FacilityBomProfile.LIndustrialMachine),
        F("research:industry:line-balancing", "유량계", "workstation:v3:metering", FacilityBomProfile.MPrecisionWorkshop),
        F("research:industry:maintenance", "정비 부품함", "workstation:v3:maintenance", FacilityBomProfile.LIndustrialMachine),
        F("research:industry:powered-tools", "전동 선반", "workstation:v3:powered-tools", FacilityBomProfile.LIndustrialMachine),
        F("research:industry:precision", "정밀 게이지", "workstation:v3:precision-parts", FacilityBomProfile.MPrecisionWorkshop),
        F("research:industry:rune-automation", "룬 제어반", "workstation:v3:rune-control", FacilityBomProfile.FRuneBiolab),
        F("research:equipment:weapon-patterns", "무기 도면걸이", "workstation:v3:weapon-pattern", FacilityBomProfile.ARecordDesk),
        F("research:equipment:armor-tailoring", "방어구 맞춤대", "workstation:v3:armor-tailoring", FacilityBomProfile.BWorkbench),
        F("research:equipment:bowyery", "궁시 지그", "workstation:v3:bow-jig", FacilityBomProfile.BWorkbench),
        F("research:equipment:mechanical-projectiles", "권양 작업대", "workstation:v3:windlass", FacilityBomProfile.LIndustrialMachine),
        F("research:equipment:mail-weaving", "사슬 조립틀", "workstation:v3:chain", FacilityBomProfile.LIndustrialMachine),
        F("research:equipment:articulated-plate", "관절 지그", "workstation:v3:plate-jig", FacilityBomProfile.LIndustrialMachine),
        F("research:equipment:black-powder", "화약 분쇄소", "workstation:v3:powder-mill", FacilityBomProfile.OPowderWorkshop),
        F("research:equipment:standard-ammunition", "탄약 압착기", "workstation:v3:ammo-press", FacilityBomProfile.LIndustrialMachine),
        F("research:equipment:relic-appraisal", "부품 감정대", "workstation:v3:appraisal", FacilityBomProfile.MPrecisionWorkshop),
        F("research:equipment:relic-restoration", "부품 복원 작업대", "workstation:v3:restoration", FacilityBomProfile.MPrecisionWorkshop),
        F("research:equipment:precision-fitting", "정밀 장착대", "workstation:v3:precision-fitting", FacilityBomProfile.MPrecisionWorkshop),
        F("research:equipment:modular-frames", "성장형 골격 지그", "workstation:v3:growth-frame", FacilityBomProfile.LIndustrialMachine),
        F("research:equipment:industrial-metrology", "계측 작업대", "workstation:v3:metrology", FacilityBomProfile.MPrecisionWorkshop),
        F("research:medical:construct-core-engineering", "구성체 핵 공학대", "workstation:v3:construct-core-engineering", FacilityBomProfile.FRuneBiolab),
        F("research:service:dining-operations", "배식 운영판", "workstation:v3:dining-operations", FacilityBomProfile.PServiceStation),
        F("research:life:seasonal-calendar", "계절력 기록대", "workstation:v19:seasonal-calendar", FacilityBomProfile.ARecordDesk),
        F("research:agriculture:phenology", "작물 달력대", "workstation:v19:crop-calendar", FacilityBomProfile.ARecordDesk),
        F("research:climate:weather-observation", "기상 관측탑", "workstation:v19:weather-observation", FacilityBomProfile.HObservationTower),
        F("research:agriculture:soil-cycles", "토양 검사대", "workstation:v19:soil-test", FacilityBomProfile.BWorkbench),
        F("research:survival:seasonal-storage", "계절 저장 선반", "workstation:v19:seasonal-storage", FacilityBomProfile.CLivingRoom),
        F("research:agriculture:greenhouse-horticulture", "재배 온실", "workstation:v19:greenhouse", FacilityBomProfile.GGreenhouse),
        F("research:husbandry:seasonal-breeding", "번식 일정 축사", "workstation:v19:breeding-schedule", FacilityBomProfile.CLivingRoom),
        F("research:climate:environment-control", "기후 제어실", "workstation:v19:climate-control", FacilityBomProfile.EIndustrialLab),
        F("research:society:household-records", "가구 등록대", "workstation:v19:household-registry", FacilityBomProfile.ARecordDesk),
        F("research:life:infant-care", "보육실", "workstation:v19:nursery", FacilityBomProfile.CLivingRoom),
        F("research:medical:reproductive-medicine", "산과실", "workstation:v19:obstetrics", FacilityBomProfile.DMedicalRoom),
        F("research:society:child-education", "교실", "workstation:v19:classroom", FacilityBomProfile.CLivingRoom),
        F("research:society:apprenticeship", "도제 작업대", "workstation:v19:apprenticeship", FacilityBomProfile.BWorkbench),
        F("research:society:generation-management", "계보 관리실", "workstation:v19:generation-management", FacilityBomProfile.ARecordDesk),
        F("research:medical:gerontology", "노화 평가대", "workstation:v19:aging-assessment", FacilityBomProfile.DMedicalRoom),
        F("research:medical:biological-age-measurement", "연령 계측기", "workstation:v19:biological-age", FacilityBomProfile.EIndustrialLab),
        F("research:medical:geriatric-medicine", "노인 병상", "workstation:v19:geriatric-care", FacilityBomProfile.DMedicalRoom),
        F("research:medical:chronic-care", "만성 관리실", "workstation:v19:chronic-care", FacilityBomProfile.DMedicalRoom),
        F("research:medical:regenerative-culture", "재생 배양조", "workstation:v19:regenerative-culture", FacilityBomProfile.FRuneBiolab),
        F("research:medical:organ-regeneration", "장기 재생 수술실", "workstation:v19:organ-regeneration", FacilityBomProfile.FRuneBiolab),
        F("research:medical:blood-rejuvenation", "회춘 수혈실", "workstation:v19:blood-rejuvenation", FacilityBomProfile.FRuneBiolab),
        F("research:medical:rune-hibernation", "룬 동면실", "workstation:v19:rune-hibernation", FacilityBomProfile.FRuneBiolab),
        F("research:medical:whole-body-regeneration", "전신 재생조", "workstation:v19:whole-body-regeneration", FacilityBomProfile.FRuneBiolab),
        F("research:medical:temporal-stasis", "시간 고정실", "workstation:v19:temporal-stasis", FacilityBomProfile.FRuneBiolab),
        F("research:health:pathogen-observation", "감염 진단대", "workstation:v19:pathogen-diagnosis", FacilityBomProfile.DMedicalRoom),
        F("research:health:isolation-medicine", "격리 병동", "workstation:v19:isolation", FacilityBomProfile.DMedicalRoom),
        F("research:health:immunoserology", "혈청 검사대", "workstation:v19:serology", FacilityBomProfile.EIndustrialLab),
        F("research:health:vaccination", "백신 연구실", "workstation:v19:vaccine", FacilityBomProfile.EIndustrialLab),
        F("research:health:epidemic-control", "역학 상황판", "workstation:v19:epidemic-board", FacilityBomProfile.ARecordDesk),
        F("research:genetics:hereditary-records", "유전 기록고", "workstation:v19:genetic-archive", FacilityBomProfile.ARecordDesk),
        F("research:genetics:trait-analysis", "형질 분석기", "workstation:v19:trait-analysis", FacilityBomProfile.FRuneBiolab),
        F("research:genetics:controlled-heredity", "유전 상담실", "workstation:v19:genetic-counseling", FacilityBomProfile.DMedicalRoom),
        F("research:genetics:cross-lineage-stabilization", "교차계통 배양기", "workstation:v19:cross-lineage", FacilityBomProfile.FRuneBiolab),
        F("research:housing:room-assignment", "방 배정대", "workstation:v19:room-assignment", FacilityBomProfile.ARecordDesk),
        F("research:housing:family-quarters", "가족실 칸막이", "workstation:v19:family-quarters", FacilityBomProfile.CLivingRoom),
        F("research:housing:guardian-succession", "보호자 등록소", "workstation:v19:guardian-registry", FacilityBomProfile.ARecordDesk),
        F("research:medical:trauma-medicine", "상담실", "workstation:v19:counseling", FacilityBomProfile.CLivingRoom),
        F("research:society:corpse-care", "시신 처리대", "workstation:v19:corpse-care", FacilityBomProfile.DMedicalRoom),
        F("research:society:funeral-rites", "추모실", "workstation:v19:memorial", FacilityBomProfile.CLivingRoom),
        F("research:climate:regional-climatology", "기후 지도실", "workstation:v19:climate-map", FacilityBomProfile.EIndustrialLab),
        F("research:climate:chronometric-navigation", "원정 천문시계실", "workstation:v19:chronometric-navigation", FacilityBomProfile.FRuneBiolab),
        F("research:agriculture:seed-selection", "종자 선별대", "workstation:v19:seed-selection", FacilityBomProfile.BWorkbench),
        F("research:agriculture:pest-control", "방제 조제대", "workstation:v19:pest-control", FacilityBomProfile.BWorkbench),
        F("research:agriculture:crop-pathology", "작물 병리실", "workstation:v19:crop-pathology", FacilityBomProfile.DMedicalRoom),
        F("research:agriculture:cultivar-breeding", "육종 온실", "workstation:v19:cultivar-breeding", FacilityBomProfile.GGreenhouse),
        F("research:society:career-records", "경력 기록대", "workstation:v19:career-records", FacilityBomProfile.ARecordDesk),
        F("research:society:retirement", "은퇴자 휴게실", "workstation:v19:retirement", FacilityBomProfile.CLivingRoom),
        F("research:society:mentor-academy", "멘토 학원", "workstation:v19:mentor-academy", FacilityBomProfile.CLivingRoom),
        F("research:arcane:resonance", "공명 조율실", "workstation:v21:resonance-tuning", FacilityBomProfile.FRuneBiolab),
        F("research:commerce:secure-trade", "보안 거래 금고", "workstation:v21:secure-trade-vault", FacilityBomProfile.EIndustrialLab),
        F("research:defense:remote-control", "방어 제어반", "workstation:v21:defense-control", FacilityBomProfile.EIndustrialLab),
        F("research:equipment:ballistics", "탄도 시험장", "workstation:v21:ballistics-range", FacilityBomProfile.EIndustrialLab),
        F("research:industry:dark-foundry", "흑강 주조 보조로", "workstation:v21:blacksteel-annex", FacilityBomProfile.EIndustrialLab)
    };

    private static ItemSpec[] ItemSpecs() => new[]
    {
        S("research:agriculture:irrigation", "resource:clean-water", "깨끗한 물", ResourceItemKind.Raw, ResourceIngredientTag.None, "workstation:v3:irrigation", 8, false, true),
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
        S("research:industry:factory-layout", "component:factory-installation-plan", "공장 설치 도면", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v3:factory-layout", 1, false, true, A("component:engineering-drawing", 1), A("material:paper", 1), A("component:paper-paste", 1)),
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
        S("research:medical:whole-body-regeneration", "medical:whole-body-regeneration-medium", "전신 재생 배지", ResourceItemKind.Medicine, ResourceIngredientTag.Arcane, "workstation:v19:whole-body-regeneration", 1, false, true, A("medicine:advanced", 4), A("textile:sterile-cloth", 4), A("medicine:mycelial-culture-pack", 2), A("resource:mana-crystal", 2), A("resource:clean-water", 8)),
        S("research:medical:temporal-stasis", "component:temporal-stasis-seal", "시간 고정 인장", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, "workstation:v19:temporal-stasis", 1, false, true, A("component:precision-parts", 2), A("component:rune-conductor", 2), A("component:mana-shield-plate", 1), A("resource:mana-crystal", 2)),
        S("research:agriculture:pest-control", "supply:pest-lure", "해충 유인제", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v19:pest-control", 2, false, true, A("resource:dark-resin", 1), A("resource:meat", 1), A("material:paper", 1)),
        S("research:agriculture:pest-control", "supply:botanical-pesticide", "식물성 살충제", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v19:pest-control", 2, false, true, A("material:rot-toxin", 1), A("material:alcohol", 1), A("resource:clean-water", 2)),
        S("research:agriculture:crop-pathology", "supply:fungicide", "살균제", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v19:crop-pathology", 2, false, true, A("medicine:antiseptic", 1), A("material:charcoal", 1), A("resource:clean-water", 2)),

        S("research:health:pathogen-observation", "sample:antigen:cave-flu", "동굴 독감 항원 표본", ResourceItemKind.FinishedGood, ResourceIngredientTag.None, string.Empty, 1, false, false),
        S("research:health:pathogen-observation", "sample:antigen:red-fever", "적열병 항원 표본", ResourceItemKind.FinishedGood, ResourceIngredientTag.None, string.Empty, 1, false, false),
        S("research:health:pathogen-observation", "sample:antigen:gut-rot", "장부패증 항원 표본", ResourceItemKind.FinishedGood, ResourceIngredientTag.None, string.Empty, 1, false, false),
        S("research:health:pathogen-observation", "sample:antigen:spore-lung", "포자폐증 항원 표본", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fungus, string.Empty, 1, false, false),
        S("research:health:pathogen-observation", "sample:antigen:mana-pox", "마나두창 항원 표본", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, string.Empty, 1, false, false),
        S("research:health:pathogen-observation", "sample:antigen:blood-wasting", "혈액소모병 항원 표본", ResourceItemKind.FinishedGood, ResourceIngredientTag.None, string.Empty, 1, false, false),
        S("research:health:pathogen-observation", "sample:antigen:slime-blight", "점액역병 항원 표본", ResourceItemKind.FinishedGood, ResourceIngredientTag.None, string.Empty, 1, false, false),
        S("research:health:vaccination", "medicine:vaccine:cave-flu", "동굴 독감 백신", ResourceItemKind.Medicine, ResourceIngredientTag.None, "workstation:v19:vaccine", 4, false, true, A("sample:antigen:cave-flu", 1), A("medicine:advanced", 1), A("resource:clean-water", 1)),
        S("research:health:vaccination", "medicine:vaccine:red-fever", "적열병 백신", ResourceItemKind.Medicine, ResourceIngredientTag.None, "workstation:v19:vaccine", 4, false, true, A("sample:antigen:red-fever", 1), A("medicine:advanced", 1), A("resource:clean-water", 1)),
        S("research:health:vaccination", "medicine:vaccine:gut-rot", "장부패증 백신", ResourceItemKind.Medicine, ResourceIngredientTag.None, "workstation:v19:vaccine", 4, false, true, A("sample:antigen:gut-rot", 1), A("medicine:advanced", 1), A("resource:clean-water", 1)),
        S("research:health:vaccination", "medicine:vaccine:spore-lung", "포자폐증 백신", ResourceItemKind.Medicine, ResourceIngredientTag.Fungus, "workstation:v19:vaccine", 4, false, true, A("sample:antigen:spore-lung", 1), A("medicine:advanced", 1), A("resource:clean-water", 1)),
        S("research:health:vaccination", "medicine:vaccine:mana-pox", "마나두창 백신", ResourceItemKind.Medicine, ResourceIngredientTag.Arcane, "workstation:v19:vaccine", 4, false, true, A("sample:antigen:mana-pox", 1), A("medicine:advanced", 1), A("resource:clean-water", 1)),
        S("research:health:vaccination", "medicine:vaccine:blood-wasting", "혈액소모병 백신", ResourceItemKind.Medicine, ResourceIngredientTag.None, "workstation:v19:vaccine", 4, false, true, A("sample:antigen:blood-wasting", 1), A("medicine:advanced", 1), A("resource:clean-water", 1)),
        S("research:health:vaccination", "medicine:vaccine:slime-blight", "점액역병 백신", ResourceItemKind.Medicine, ResourceIngredientTag.None, "workstation:v19:vaccine", 4, false, true, A("sample:antigen:slime-blight", 1), A("medicine:advanced", 1), A("resource:clean-water", 1)),

        // V21 branched equipment materials.
        S("research:forestry:treated", "material:laminated-lumber", "적층 목재", ResourceItemKind.Intermediate, ResourceIngredientTag.Wood, "workstation:v3:bow-jig", 2, true, true, A("material:treated-lumber", 2), A("resource:dark-resin", 1)),
        S("research:equipment:armor-tailoring", "material:hardened-leather", "경화 가죽", ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber, "workstation:v3:armor-tailoring", 2, true, true, A("material:leather", 2), A("resource:dark-resin", 1)),
        S("research:equipment:mail-weaving", "material:chain-mesh", "사슬 망", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:chain", 2, true, true, A("material:iron-ingot", 2)),
        S("research:equipment:articulated-plate", "material:plate-blank", "판금 소재판", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:plate-jig", 2, true, true, A("material:steel-ingot", 2)),
        S("research:metallurgy:advanced", "material:spring-steel", "용수철강", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:material-test", 2, true, true, A("material:steel-ingot", 2), A("material:charcoal", 1)),
        S("research:equipment:pressure-barrels", "material:barrel-steel", "총열강", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v21:ballistics-range", 2, true, true, A("material:spring-steel", 1), A("material:steel-ingot", 2)),
        S("research:equipment:standard-ammunition", "material:granulated-powder", "과립 화약", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral, "workstation:v3:ammo-press", 6, true, true, A("material:black-powder", 2), A("material:paper", 1)),
        S("research:industry:powered-tools", "material:cartridge-paper", "탄약지", ResourceItemKind.Intermediate, ResourceIngredientTag.Plant, "workstation:v3:prototype", 6, true, true, A("material:paper", 2), A("material:starch", 1)),
        S("research:environment:cold-work", "textile:insulating-cloth", "절연 직물", ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber, "workstation:v3:armor-tailoring", 2, true, true, A("material:cloth", 2), A("material:leather", 1)),
        S("research:equipment:industrial-metrology", "component:precision-optics", "정밀 광학계", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, "workstation:v3:metrology", 1, true, true, A("material:gold-ingot", 1), A("resource:mana-crystal", 1)),
        S("research:industry:mana-power", "material:mana-alloy", "마나 합금", ResourceItemKind.Intermediate, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, "workstation:v3:rune-conductor", 2, true, true, A("material:steel-ingot", 1), A("material:gold-ingot", 1), A("resource:mana-crystal", 1)),
        S("research:medical:regenerative-culture", "material:sterile-composite", "무균 복합재", ResourceItemKind.Intermediate, ResourceIngredientTag.Fiber | ResourceIngredientTag.Arcane, "workstation:v19:regenerative-culture", 2, true, true, A("material:cloth", 2), A("resource:mana-crystal", 1), A("resource:clean-water", 2)),

        // V21 physical operation, installation, and medical supplies.
        S("research:agriculture:cultivar-breeding", "supply:certified-seed-kit", "인증 품종 종자 꾸러미", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v19:cultivar-breeding", 2, false, true, A("material:paper", 1), A("material:cloth", 1)),
        S("research:agriculture:greenhouse-horticulture", "supply:greenhouse-nutrient", "온실 영양액", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v19:greenhouse", 4, false, true, A("supply:nitrate-fertilizer", 1), A("resource:clean-water", 2)),
        S("research:climate:environment-control", "component:climate-control-manifold", "기후 제어 매니폴드", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:cooling", 1, false, true, A("component:machine-parts", 1), A("textile:insulating-cloth", 1)),
        S("research:climate:weather-observation", "tool:weather-observation-kit", "기상 관측 도구함", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v19:weather-observation", 1, false, true, A("component:precision-optics", 1), A("material:treated-lumber", 1)),
        S("research:life:seasonal-calendar", "book:seasonal-almanac", "계절력 책자", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v19:seasonal-calendar", 2, false, true, A("material:paper", 2), A("material:charcoal", 1)),
        S("research:forestry:fungal", "supply:inoculated-log", "접종 원목", ResourceItemKind.FinishedGood, ResourceIngredientTag.Wood | ResourceIngredientTag.Fungus, "workstation:v3:fungal", 2, false, true, A("material:treated-lumber", 1), A("resource:cave-mushroom", 1)),
        S("research:arcane:records", "record:arcane-index", "비전 색인철", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant | ResourceIngredientTag.Arcane, "workstation:v3:prototype", 1, false, true, A("material:paper", 2), A("resource:rune-dust", 1)),
        S("research:authority:office", "tool:administrative-seal", "행정 인장", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:heraldry", 1, false, true, A("material:iron-ingot", 1), A("material:paper", 1)),
        S("research:housing:room-assignment", "component:room-partition-kit", "방 칸막이 키트", ResourceItemKind.FinishedGood, ResourceIngredientTag.Wood | ResourceIngredientTag.Fiber, "workstation:v19:room-assignment", 1, false, true, A("material:treated-lumber", 2), A("material:cloth", 2)),
        S("research:society:career-records", "record:career-ledger", "경력 장부", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v19:career-records", 1, false, true, A("material:paper", 3), A("material:leather", 1)),
        S("research:husbandry:breeding", "record:breeding-ledger", "번식 장부", ResourceItemKind.FinishedGood, ResourceIngredientTag.Plant, "workstation:v3:breeding", 1, false, true, A("material:paper", 3), A("material:leather", 1)),
        S("research:society:corpse-care", "supply:funeral-preparation-kit", "종족 장례 준비품", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, "workstation:v19:memorial", 1, false, true, A("material:cloth", 2), A("material:paper", 1), A("resource:rune-dust", 1)),
        S("research:commerce:logistics", "tool:hauling-harness", "운반 멜빵", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, "workstation:v3:logistics", 1, false, true, A("material:leather", 2), A("material:rope", 1)),
        S("research:commerce:retail", "component:price-board", "가격표 게시판", ResourceItemKind.FinishedGood, ResourceIngredientTag.Wood, "workstation:v3:retail", 1, false, true, A("material:lumber", 1), A("material:paper", 1)),
        S("research:control:labor", "tool:prisoner-work-kit", "포로 작업 도구", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:prison-labor", 1, false, true, A("material:iron-ingot", 1), A("material:lumber", 1)),
        S("research:control:restraints", "tool:reinforced-restraint", "강화 구속구", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral | ResourceIngredientTag.Fiber, "workstation:v3:restraint", 1, false, true, A("material:hardened-leather", 1), A("material:chain-mesh", 1)),
        S("research:control:show", "supply:performance-prop-box", "공연 소품 상자", ResourceItemKind.FinishedGood, ResourceIngredientTag.Wood | ResourceIngredientTag.Fiber, "workstation:v3:show", 1, false, true, A("material:lumber", 2), A("material:cloth", 2)),
        S("research:service:dining-operations", "tool:banquet-cart", "연회 운반 수레", ResourceItemKind.FinishedGood, ResourceIngredientTag.Wood | ResourceIngredientTag.Mineral, "workstation:v3:dining-operations", 1, false, true, A("material:laminated-lumber", 2), A("component:machine-parts", 1)),
        S("research:survival:seasonal-storage", "component:sealed-seasonal-container", "밀폐형 계절 보관함", ResourceItemKind.FinishedGood, ResourceIngredientTag.Wood, "workstation:v19:seasonal-storage", 1, false, true, A("material:laminated-lumber", 2), A("material:hardened-leather", 1)),
        S("research:defense:alliance-signals", "supply:alliance-signal-kit", "동맹 신호 키트", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, "workstation:v3:signals", 1, false, true, A("material:cloth", 2), A("material:granulated-powder", 1)),
        S("research:defense:corridor-mechanisms", "component:corridor-detonator", "복도식 기폭 장치", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:defense-ammo", 1, false, true, A("material:spring-steel", 1), A("material:granulated-powder", 1)),
        S("research:defense:rune-identification", "tool:rune-identification-lens", "룬 식별 렌즈", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, "workstation:v3:material-test", 1, false, true, A("component:precision-optics", 1), A("resource:rune-dust", 1)),
        S("research:defense:siege-fortification", "component:siege-reinforcement-kit", "공성 보강 키트", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:plate-jig", 1, false, true, A("material:plate-blank", 2), A("component:machine-parts", 1)),
        S("research:defense:watch", "tool:watch-signal-horn", "경계 신호 나팔", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:signals", 1, false, true, A("material:iron-ingot", 1), A("material:leather", 1)),
        S("research:equipment:material-testing", "component:material-test-coupon", "재료 시험편", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:material-test", 2, false, true, A("material:steel-ingot", 1), A("material:plate-blank", 1)),
        S("research:equipment:precision-fitting", "tool:inspection-gauge", "검사 게이지", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:precision-fitting", 1, false, true, A("component:precision-parts", 1), A("material:spring-steel", 1)),
        S("research:industry:electric-lighting", "component:insulated-wiring", "절연 배선 묶음", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral | ResourceIngredientTag.Fiber, "workstation:v3:powered-tools", 2, false, true, A("material:gold-ingot", 1), A("textile:insulating-cloth", 1)),
        S("research:industry:waterwheel", "component:waterwheel-drive-shaft", "수차 구동축", ResourceItemKind.FinishedGood, ResourceIngredientTag.Wood | ResourceIngredientTag.Mineral, "workstation:v3:machine-parts", 1, false, true, A("material:laminated-lumber", 2), A("material:spring-steel", 1)),
        S("research:plumbing:reuse", "component:reclaimed-water-filter", "재생수 필터 카트리지", ResourceItemKind.FinishedGood, ResourceIngredientTag.Fiber, "workstation:v3:sanitation", 2, false, true, A("textile:sterile-cloth", 1), A("material:charcoal", 1)),
        S("research:plumbing:rune-purification", "component:rune-purification-crystal", "룬 정화 결정", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, "workstation:v3:rune-conductor", 1, false, true, A("material:mana-alloy", 1), A("resource:mana-crystal", 1)),
        S("research:genetics:hereditary-records", "medical:trait-analysis-kit", "형질 검사 키트", ResourceItemKind.Medicine, ResourceIngredientTag.None, "workstation:v19:trait-analysis", 1, false, true, A("material:sterile-composite", 1), A("material:paper", 1)),
        S("research:genetics:cross-lineage-stabilization", "medical:cross-lineage-medium", "교차계통 안정화 배지", ResourceItemKind.Medicine, ResourceIngredientTag.Arcane, "workstation:v19:cross-lineage", 1, false, true, A("material:sterile-composite", 1), A("component:rune-conductor", 1)),
        S("research:health:isolation-medicine", "medical:isolation-care-kit", "격리 치료 꾸러미", ResourceItemKind.Medicine, ResourceIngredientTag.Fiber, "workstation:v19:isolation", 1, false, true, A("medical:sterile-bandage", 1), A("medicine:antiseptic", 1)),
        S("research:medical:blood-rejuvenation", "medical:rejuvenation-serum", "회춘 혈청", ResourceItemKind.Medicine, ResourceIngredientTag.Arcane, "workstation:v19:blood-rejuvenation", 1, false, true, A("medicine:standard", 2), A("material:sterile-composite", 1), A("resource:mana-crystal", 1)),
        S("research:medical:construct-core-engineering", "component:golem-core-case", "골렘 핵 케이스", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral | ResourceIngredientTag.Arcane, "workstation:v3:construct-core-engineering", 1, false, true, A("material:sterile-composite", 1), A("component:rune-conductor", 1)),
        S("research:medical:organ-preservation", "medical:organ-preservation-canister", "장기 보존 용기", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:restoration", 1, false, true, A("material:sterile-composite", 1), A("component:machine-parts", 1)),
        S("research:medical:organ-regeneration", "medical:organ-regeneration-scaffold", "장기 재생 골격", ResourceItemKind.Medicine, ResourceIngredientTag.Fiber, "workstation:v19:organ-regeneration", 1, false, true, A("material:sterile-composite", 2), A("medicine:mycelial-culture-pack", 1)),
        S("research:medical:regenerative-culture", "medical:regenerative-medium", "재생 배양액", ResourceItemKind.Medicine, ResourceIngredientTag.Arcane, "workstation:v19:regenerative-culture", 2, false, true, A("medicine:advanced", 1), A("resource:clean-water", 2)),
        S("research:medical:reproductive-medicine", "medical:fertility-treatment", "생식 치료제", ResourceItemKind.Medicine, ResourceIngredientTag.None, "workstation:v19:obstetrics", 1, false, true, A("medicine:standard", 1), A("material:sterile-composite", 1)),
        S("research:medical:rune-hibernation", "medical:rune-hibernation-catalyst", "룬 동면 촉매", ResourceItemKind.Medicine, ResourceIngredientTag.Arcane, "workstation:v19:rune-hibernation", 1, false, true, A("component:rune-conductor", 1), A("material:sterile-composite", 1)),
        S("research:medical:trauma-medicine", "medical:trauma-care-kit", "트라우마 치료 꾸러미", ResourceItemKind.Medicine, ResourceIngredientTag.None, "workstation:v19:counseling", 1, false, true, A("medicine:advanced", 1), A("material:paper", 1)),

        // Ten physical ammunition definitions. The mixed defense box below is an installation supply, not an eleventh ammunition kind.
        S("research:equipment:black-powder", "ammo:incendiary-arrow", "소이 화살", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:defense-ammo", 6, false, true, A("ammo:arrow-steel", 6), A("material:granulated-powder", 1)),
        S("research:equipment:black-powder", "ammo:incendiary-bolt", "소이 볼트", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:defense-ammo", 6, false, true, A("ammo:bolt-steel", 6), A("material:granulated-powder", 1)),
        S("research:equipment:pressure-barrels", "ammo:smoke-cartridge", "연막 탄약", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:ammo-press", 6, false, true, A("material:granulated-powder", 1), A("material:cartridge-paper", 1)),
        S("research:equipment:pressure-barrels", "ammo:armor-piercing-cartridge", "철갑 탄약", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:ammo-press", 6, false, true, A("material:lead-shot", 4), A("material:granulated-powder", 1), A("material:cartridge-paper", 1)),
        S("research:equipment:standard-ammunition", "ammo:scatter-cartridge", "산탄 탄약", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:ammo-press", 6, false, true, A("material:lead-shot", 6), A("material:granulated-powder", 1), A("material:cartridge-paper", 1)),
        S("research:defense:alliance-signals", "ammo:signal-flare", "신호탄", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v3:signals", 4, false, true, A("material:granulated-powder", 1), A("material:cartridge-paper", 1), A("resource:rune-dust", 1)),
        S("research:industry:dark-foundry", "ammo:blacksteel-bolt", "흑강 볼트", ResourceItemKind.Ammunition, ResourceIngredientTag.Mineral, "workstation:v21:blacksteel-annex", 6, false, true, A("material:blacksteel-ingot", 1), A("material:laminated-lumber", 1)),
        S("research:equipment:rune-module-tuning", "ammo:rune-cartridge", "룬 탄약통", ResourceItemKind.Ammunition, ResourceIngredientTag.Arcane, "workstation:v3:rune-conductor", 6, false, true, A("material:mana-alloy", 1), A("material:granulated-powder", 1), A("material:cartridge-paper", 1)),
        S("research:pharmacology:anesthesia", "ammo:tranquilizer-dart", "진정 다트", ResourceItemKind.Ammunition, ResourceIngredientTag.None, "workstation:v3:ammo-press", 6, false, true, A("medicine:standard", 1), A("material:paper", 1)),
        S("research:defense:rune-identification", "ammo:mana-disruptor-bolt", "마나 차단 볼트", ResourceItemKind.Ammunition, ResourceIngredientTag.Arcane, "workstation:v3:defense-ammo", 6, false, true, A("ammo:bolt-steel", 6), A("material:mana-alloy", 1)),
        S("research:industry:line-balancing", "supply:defense-mixed-ammo-box", "방어시설용 혼합 탄약 상자", ResourceItemKind.FinishedGood, ResourceIngredientTag.Mineral, "workstation:v3:defense-ammo", 1, false, true, A("material:lead-shot", 8), A("material:granulated-powder", 2), A("material:cartridge-paper", 2)),

        S("research:equipment:lineage-binding", EquipmentProgressionItemIds.LineageSeal, "계보 인장", ResourceItemKind.FinishedGood, ResourceIngredientTag.Arcane, string.Empty, 1, false, false),
        S(string.Empty, PhysicalItemIds.EquipmentModule, "개량 부품", ResourceItemKind.FinishedGood, ResourceIngredientTag.None, string.Empty, 1, false, false)
    };

    private static FacilitySpec F(
        string researchId,
        string name,
        string tag,
        FacilityBomProfile bomProfile) =>
        new FacilitySpec(researchId, name, tag, bomProfile);

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

    private static int ResolveGeneratedUnitPrice(ItemSpec spec)
    {
        float kindValue = spec.Kind switch
        {
            ResourceItemKind.Raw => 6f,
            ResourceItemKind.Intermediate => 14f,
            ResourceItemKind.Food => 10f,
            ResourceItemKind.Medicine => 28f,
            ResourceItemKind.Substance => 18f,
            ResourceItemKind.AnimalProduct => 10f,
            ResourceItemKind.Waste => 2f,
            ResourceItemKind.Ammunition => 12f,
            ResourceItemKind.FinishedGood => 24f,
            _ => 8f
        };
        float tagValue = 0f;
        if ((spec.Tags & ResourceIngredientTag.Arcane) != 0) tagValue += 18f;
        if ((spec.Tags & ResourceIngredientTag.Mineral) != 0) tagValue += 8f;
        if ((spec.Tags & ResourceIngredientTag.Forbidden) != 0) tagValue += 10f;
        if ((spec.Tags & ResourceIngredientTag.Fiber) != 0) tagValue += 3f;
        if ((spec.Tags & ResourceIngredientTag.Wood) != 0) tagValue += 3f;
        int inputKinds = spec.Inputs
            .Select(value => value.ItemId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        int inputUnits = spec.Inputs.Sum(value => Mathf.Max(0, value.Amount));
        float complexityValue = inputKinds * 4f + inputUnits * 2f;
        float outputEconomy = Mathf.Pow(Mathf.Max(1, spec.OutputAmount), 0.45f);
        return Mathf.Clamp(
            Mathf.CeilToInt((kindValue + tagValue + complexityValue) / outputEconomy),
            2,
            200);
    }

    private static float ResolveGeneratedUnitWeight(ItemSpec spec)
    {
        if (spec.Kind == ResourceItemKind.Ammunition)
        {
            return 0.15f;
        }

        float weight = spec.Kind switch
        {
            ResourceItemKind.Medicine => 0.25f,
            ResourceItemKind.Food => 0.55f,
            ResourceItemKind.Waste => 0.75f,
            ResourceItemKind.FinishedGood => 0.9f,
            ResourceItemKind.Intermediate => 0.7f,
            _ => 0.5f
        };
        if ((spec.Tags & ResourceIngredientTag.Mineral) != 0) weight += 1.35f;
        if ((spec.Tags & ResourceIngredientTag.Wood) != 0) weight += 0.8f;
        if ((spec.Tags & ResourceIngredientTag.Fiber) != 0) weight += 0.15f;
        if ((spec.Tags & ResourceIngredientTag.Arcane) != 0) weight += 0.2f;
        if ((spec.Tags & ResourceIngredientTag.Plant) != 0) weight += 0.1f;
        return Mathf.Clamp(
            Mathf.Round((weight + spec.Inputs.Length * 0.05f) * 20f) / 20f,
            0.05f,
            8f);
    }

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
