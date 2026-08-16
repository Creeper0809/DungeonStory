using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ModularFacilityAssetBuilder
{
    public const int FirstBuildingId = 1000;
    private static readonly IReadOnlyDictionary<string, int> EnvironmentBuildingIds =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["L08"] = 1500,
            ["E10"] = 1501,
            ["E11"] = 1502,
            ["E12"] = 1503,
            ["E13"] = 1504,
            ["E14"] = 1505
        };
    private const string SpriteFolder = "Assets/Images/ModularFacilities";
    private const string BuildingFolder = "Assets/Resources/SO/Building/Modular";
    private const string StockFolder = "Assets/Resources/SO/Stock/Modular";
    private const string GeneralStockSource = "Assets/Resources/SO/Stock/P1/P1_GeneralStoreStock.asset";
    private const string SaleItemFolder = "Assets/Resources/SO/Stock/Item";
    private static readonly string[] DefaultCraftableEquipmentIds =
    {
        "weapon:dagger",
        "weapon:longsword",
        "weapon:spear",
        "weapon:mace",
        "weapon:shortbow",
        "weapon:longbow",
        "weapon:crossbow",
        "weapon:javelin",
        "weapon:throwing-axe",
        "armor:cloth-hood",
        "armor:gambeson",
        "armor:leather-cap",
        "armor:leather",
        "armor:mail-coif",
        "armor:mail-shirt",
        "armor:iron-helmet",
        "armor:breastplate",
        "shield:wood",
        "shield:iron",
        "craft:ammo:arrow",
        "craft:ammo:bolt"
    };

    private static readonly string[] LegacyRoomAssetPaths =
    {
        "Assets/Resources/SO/Building/LordBedroom.asset",
        "Assets/Resources/SO/Building/HamburgerStore.asset",
        "Assets/Resources/SO/Building/WeaponStore.asset",
        "Assets/Resources/SO/Building/P1/P1_LowFoodShop.asset",
        "Assets/Resources/SO/Building/P1/P1_MeatRestaurant.asset",
        "Assets/Resources/SO/Building/P1/P1_PremiumMeatRestaurant.asset",
        "Assets/Resources/SO/Building/P1/P1_BattleDining.asset",
        "Assets/Resources/SO/Building/P1/P1_BattlefieldDining.asset",
        "Assets/Resources/SO/Building/P1/P1_NobleDining.asset",
        "Assets/Resources/SO/Building/P1/P1_GeneralStore.asset",
        "Assets/Resources/SO/Building/P1/P1_WeaponShop.asset",
        "Assets/Resources/SO/Building/P1/P1_RestRoom.asset",
        "Assets/Resources/SO/Building/P1/P1_TrainingRoom.asset",
        "Assets/Resources/SO/Building/P1/P1_GuardRoom.asset",
        "Assets/Resources/SO/Building/P1/P1_Barracks.asset",
        "Assets/Resources/SO/Building/P1/P1_WarBarracks.asset",
        "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset",
        "Assets/Resources/SO/Building/P1/P1_ManaStorage.asset",
        "Assets/Resources/SO/Building/P1/P1_Warehouse.asset",
        "Assets/Resources/SO/Building/P1/P1_Toilet.asset",
        "Assets/Resources/SO/Building/P1/P1_Washroom.asset"
    };

    [MenuItem("DungeonStory/Content/Build All Modular Facilities")]
    public static void BuildAllFromMenu()
    {
        BuildAll();
    }

    [MenuItem("DungeonStory/Content/Patch Combat Equipment Assets")]
    public static void PatchCombatEquipmentAssetsFromMenu()
    {
        PatchCombatEquipmentAssets();
    }

    [MenuItem("DungeonStory/Content/Patch Survival Facility Abilities")]
    public static void PatchSurvivalFacilityAbilitiesFromMenu()
    {
        PatchSurvivalFacilityAbilities();
    }

    [MenuItem("DungeonStory/Content/Patch D03/R07 Work Wiring")]
    public static void PatchCriticalWorkTypeWiringFromMenu()
    {
        PatchCriticalWorkTypeWiring();
    }

    [MenuItem("DungeonStory/Content/Build Environment Facilities")]
    public static void BuildEnvironmentFacilities()
    {
        FacilityPartSpec[] specs = CreateSpecs();
        HashSet<string> environmentCodes = new HashSet<string>(
            new[] { "L08", "E10", "E11", "E12", "E13", "E14" },
            StringComparer.Ordinal);
        EnsureFolder(SpriteFolder);
        EnsureFolder(BuildingFolder);
        for (int index = 0; index < specs.Length; index++)
        {
            FacilityPartSpec spec = specs[index];
            if (!environmentCodes.Contains(spec.Code))
            {
                continue;
            }

            string spritePath = $"{SpriteFolder}/{spec.Code}.png";
            WriteSprite(spec, spritePath);
            ConfigureSpriteImport(spritePath);
            EnsureBuildingAsset(
                spec,
                ResolveBuildingId(specs, index),
                spritePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built six environmental facility assets.");
    }

    public static void PatchCombatEquipmentAssets()
    {
        EnsureFolder("Assets/Resources/Config");
        PatchEquipmentFacilityAbilities();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Combat equipment definitions and facility abilities patched.");
    }

    public static void PatchSurvivalFacilityAbilities()
    {
        foreach (string path in AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            if (building == null)
            {
                continue;
            }

            string code = building.GetFacilityCode();
            if (string.IsNullOrWhiteSpace(code))
            {
                code = Path.GetFileNameWithoutExtension(path);
            }

            bool changed = false;
            changed |= ReplaceAbility(building, CreateWaterSourceAbility(code));
            changed |= ReplaceAbility(building, CreateCookingAbility(code));
            changed |= ReplaceAbility(building, CreateButcherAbility(code));
            changed |= ReplaceAbility(building, CreatePreservationAbility(code));
            changed |= ReplaceAbility(building, CreateMedicalAbility(code));
            changed |= ReplaceAbility(building, CreateFuelConsumerAbility(code));
            BuildingGolemRechargeAbility rechargeAbility =
                CreateGolemRechargeAbility(code)
                ?? CreateGolemRechargeAbility(Path.GetFileNameWithoutExtension(path));
            changed |= ReplaceAbility(building, rechargeAbility);
            changed |= ReplaceAbility(building, CreateTemperatureAbility(code));
            changed |= ReplaceAbility(building, CreateVentilationAbility(code));

            FacilityWorkType fallbackTypes = GetSurvivalFallbackWorkTypes(building);
            if (fallbackTypes != FacilityWorkType.None)
            {
                FacilityData facility = building.Facility ?? new FacilityData();
                facility.AddSupportedWorkTypeIds(ToWorkTypeIds(fallbackTypes));
                facility.requiredWorkers = Mathf.Max(1, facility.requiredWorkers);
                building.Facility = facility;
                changed = true;
            }

            if (!changed)
            {
                continue;
            }

            building.AbilityModules.EnsureStableIds();
            building.ValidateAbilitiesOrThrow();
            EditorUtility.SetDirty(building);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Survival facility abilities patched.");
    }

    /// <summary>
    /// Rebuilds only the two authored facilities whose work contracts are
    /// required by the butcher and grand-project production pipelines.
    /// </summary>
    public static void PatchCriticalWorkTypeWiring()
    {
        FacilityPartSpec[] specs = CreateSpecs();
        string[] targetCodes = { "D03", "R07" };
        for (int targetIndex = 0; targetIndex < targetCodes.Length; targetIndex++)
        {
            string code = targetCodes[targetIndex];
            int specIndex = Array.FindIndex(
                specs,
                candidate => string.Equals(candidate.Code, code, StringComparison.Ordinal));
            if (specIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Critical facility '{code}' is missing from the modular catalog.");
            }

            FacilityPartSpec spec = specs[specIndex];
            string spritePath = $"{SpriteFolder}/{spec.Code}.png";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(spritePath) == null)
            {
                throw new InvalidOperationException(
                    $"Critical facility '{code}' is missing its authored sprite at '{spritePath}'.");
            }

            EnsureBuildingAsset(spec, ResolveBuildingId(specs, specIndex), spritePath);
        }

        ValidateCriticalWorkTypeWiringAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Patched and validated D03 butcher and R07 grand-project work wiring.");
    }

    public static void BuildAll()
    {
        FacilityPartSpec[] specs = CreateSpecs();
        if (specs.Length != 104)
        {
            throw new InvalidOperationException($"Modular facility catalog must contain 104 parts, found {specs.Length}.");
        }

        EnsureFolder(SpriteFolder);
        EnsureFolder(BuildingFolder);
        EnsureFolder(StockFolder);

        for (int index = 0; index < specs.Length; index++)
        {
            FacilityPartSpec spec = specs[index];
            int buildingId = ResolveBuildingId(specs, index);
            string spritePath = $"{SpriteFolder}/{spec.Code}.png";
            WriteSprite(spec, spritePath);
            ConfigureSpriteImport(spritePath);
            EnsureBuildingAsset(spec, buildingId, spritePath);

        }

        ValidateCriticalWorkTypeWiringAssets();

        NormalizeAbilityLists();
        ServiceRoomContentAssetBuilder.EnsureAssets();
        int shopIndex = Array.FindIndex(
            specs,
            spec => spec.Code == "S01");
        EnsureShopStock(ResolveBuildingId(specs, shopIndex));

        HideLegacyRoomAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Built {specs.Length} modular facility sprites and BuildingSO assets.");
    }

    public static void ValidateCriticalWorkTypeWiringAssets()
    {
        BuildingSO preparationTable = LoadCriticalFacility("D03", "조리손질대");
        RequirePreparationTableWorkTypes(preparationTable);
        BuildingButcherAbility butcherAbility =
            preparationTable.GetAbility<BuildingButcherAbility>();
        if (butcherAbility == null
            || !Mathf.Approximately(butcherAbility.workSeconds, 1f))
        {
            throw new InvalidOperationException(
                "D03 조리손질대 must author exactly one 1 WU BuildingButcherAbility "
                + "for the production butcher pipeline.");
        }

        BuildingSO lordOffice = LoadCriticalFacility("R07", "영주집무책상");
        RequireExactWorkTypes(
            lordOffice,
            BuiltInWorkTypeIds.Operate,
            BuiltInWorkTypeIds.Repair,
            BuiltInWorkTypeIds.GrandProject);
    }

    private static void RequirePreparationTableWorkTypes(BuildingSO building)
    {
        bool hasProcessFluid =
            building.GetAbility<BuildingProcessFluidAbility>() != null;
        BuildingUtilityConnectionAbility utility =
            building.GetAbility<BuildingUtilityConnectionAbility>();
        bool hasCleanWater = utility != null
            && (utility.channels & UtilityChannel.CleanWater) != 0;
        bool hasWastewater = utility != null
            && (utility.channels & UtilityChannel.Wastewater) != 0;
        bool hasAnyFluidOverlay =
            hasProcessFluid || hasCleanWater || hasWastewater;
        bool hasCompleteFluidOverlay =
            hasProcessFluid && hasCleanWater && hasWastewater;

        if (hasAnyFluidOverlay && !hasCompleteFluidOverlay)
        {
            throw new InvalidOperationException(
                "D03 조리손질대 process-fluid overlay must author "
                + "BuildingProcessFluidAbility with both clean-water and "
                + "wastewater utility channels.");
        }

        if (hasCompleteFluidOverlay)
        {
            RequireExactWorkTypes(
                building,
                BuiltInWorkTypeIds.Cook,
                BuiltInWorkTypeIds.Butcher,
                BuiltInWorkTypeIds.Plumbing);
            return;
        }

        RequireExactWorkTypes(
            building,
            BuiltInWorkTypeIds.Cook,
            BuiltInWorkTypeIds.Butcher);
    }

    private static BuildingSO LoadCriticalFacility(string code, string assetName)
    {
        string path = $"{BuildingFolder}/{code}_{assetName}.asset";
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        if (building == null)
        {
            throw new InvalidOperationException(
                $"Critical modular facility '{code}' is missing at '{path}'.");
        }

        building.ValidateAbilitiesOrThrow();
        return building;
    }

    private static void RequireExactWorkTypes(
        BuildingSO building,
        params WorkTypeId[] expected)
    {
        FacilityData facility = building.Facility;
        if (facility == null)
        {
            throw new InvalidOperationException(
                $"Critical modular facility '{building.name}' has no facility work contract.");
        }

        WorkTypeId[] actual = facility.SupportedWorkTypeIds
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        WorkTypeId[] normalizedExpected = expected
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(normalizedExpected))
        {
            throw new InvalidOperationException(
                $"Critical modular facility '{building.name}' work contract drifted. "
                + $"Expected [{string.Join(", ", normalizedExpected.Select(value => value.Value))}], "
                + $"actual [{string.Join(", ", actual.Select(value => value.Value))}].");
        }
    }

    private static int ResolveBuildingId(
        IReadOnlyList<FacilityPartSpec> specs,
        int index)
    {
        if (index < 0 || index >= specs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        FacilityPartSpec spec = specs[index];
        if (EnvironmentBuildingIds.TryGetValue(spec.Code, out int dedicatedId))
        {
            return dedicatedId;
        }

        int insertedEnvironmentParts = 0;
        for (int candidate = 0; candidate < index; candidate++)
        {
            if (EnvironmentBuildingIds.ContainsKey(specs[candidate].Code))
            {
                insertedEnvironmentParts++;
            }
        }

        return FirstBuildingId + index - insertedEnvironmentParts;
    }

    private static void NormalizeAbilityLists()
    {
        string[] guids = AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            if (building == null)
            {
                continue;
            }

            if (SerializationUtility.HasManagedReferencesWithMissingTypes(building))
            {
                throw new InvalidOperationException(
                    $"Cannot normalize '{path}' because one or more building ability types are missing.");
            }

            bool isLegacyDoor = string.Equals(
                path.Replace('\\', '/'),
                "Assets/Resources/SO/Building/Door.asset",
                StringComparison.OrdinalIgnoreCase);
            int removedNulls = building.AbilityModules.RemoveNullEntries();
            int stabilizedIds = building.AbilityModules.EnsureStableIds();
            if (removedNulls > 0 || stabilizedIds > 0 || isLegacyDoor)
            {
                EditorUtility.SetDirty(building);
            }

            building.ValidateAbilitiesOrThrow();
        }
    }

    private static void PatchEquipmentFacilityAbilities()
    {
        foreach (string path in AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath))
        {
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            if (building == null)
            {
                continue;
            }

            string code = building.GetFacilityCode();
            string recoveryKey = !string.IsNullOrWhiteSpace(code)
                ? code
                : Path.GetFileNameWithoutExtension(path);
            bool changed = false;

            if (string.Equals(code, "S08", StringComparison.Ordinal))
            {
                changed |= building.AbilityModules.Remove<BuildingProductionAbility>() > 0;
                changed |= building.AbilityModules.Remove<BuildingRetailAbility>() > 0;
                changed |= building.AbilityModules.Remove<BuildingEquipmentCraftingAbility>() > 0;
                building.AbilityModules.Add(new BuildingEquipmentCraftingAbility
                {
                    craftableEquipmentIds = DefaultCraftableEquipmentIds.ToArray(),
                    workUnitsPerCycle = 1f
                });
                FacilityData facility = building.Facility ?? new FacilityData();
                facility.SetSupportedWorkTypeIds(new[]
                {
                    BuiltInWorkTypeIds.Craft,
                    BuiltInWorkTypeIds.Repair
                });
                facility.requiredWorkers = Mathf.Max(1, facility.requiredWorkers);
                building.Facility = facility;
                changed = true;
            }

            BuildingExpeditionRecoveryAbility recovery = CreateExpeditionRecoveryAbility(recoveryKey);
            if (recovery != null)
            {
                changed |= building.AbilityModules.Remove<BuildingExpeditionRecoveryAbility>() > 0;
                building.AbilityModules.Add(recovery);
                changed = true;
            }

            if (!changed)
            {
                continue;
            }

            building.AbilityModules.EnsureStableIds();
            building.ValidateAbilitiesOrThrow();
            EditorUtility.SetDirty(building);
        }
    }

    private static BuildingExpeditionRecoveryAbility CreateExpeditionRecoveryAbility(string code)
    {
        return code switch
        {
            "R01" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.12f,
                injuryReduction = 0.03f,
                stressRecovery = 12f
            },
            "R02" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.22f,
                injuryReduction = 0.08f,
                stressRecovery = 22f
            },
            "R03" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.18f,
                injuryReduction = 0.05f,
                stressRecovery = 18f
            },
            "H04" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.08f,
                injuryReduction = 0.12f,
                stressRecovery = 26f
            },
            "P1_RestRoom" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.18f,
                injuryReduction = 0.1f,
                stressRecovery = 18f
            },
            "P1_Washroom" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.08f,
                injuryReduction = 0.08f,
                stressRecovery = 24f
            },
            _ => null
        };
    }

    public static IReadOnlyList<string> GetCatalogCodes()
    {
        return CreateSpecs().Select((spec) => spec.Code).ToArray();
    }

    private static void EnsureBuildingAsset(FacilityPartSpec spec, int buildingId, string spritePath)
    {
        string assetPath = $"{BuildingFolder}/{spec.Code}_{spec.AssetName}.asset";
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingSO>();
            AssetDatabase.CreateAsset(building, assetPath);
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        building.id = buildingId;
        building.objectName = spec.DisplayName;
        building.sprite = sprite;
        building.icon = sprite;
        building.width = spec.Width;
        building.height = 1;
        building.layer = spec.Layer;
        building.category = spec.Category;
        building.horizontalDraggable = false;
        building.verticalDraggable = false;
        building.runtimeArchetype =
            BuildingRuntimeArchetypeKindExtensions.FromComponentType(spec.RuntimeType);
        building.tiles = null;
        building.movementAnchorOffset = Vector2.zero;
        building.movementTravelTime = 2f;
        building.ReplaceAbilities(CreateAbilities(spec));
        building.ConfigureCompatibilityStatus(false);
        building.unlocked = !IsResearchLockedFacility(spec.Code);
        EditorUtility.SetDirty(building);
    }

    private static bool IsResearchProductionStation(string code)
    {
        return !string.IsNullOrWhiteSpace(code)
            && code.StartsWith("P", StringComparison.Ordinal)
            && code.Length == 3;
    }

    private static bool IsResearchLockedFacility(string code)
    {
        return IsResearchProductionStation(code)
            || code is "L08"
                or "E10"
                or "E11"
                or "E12"
                or "E13"
                or "E14";
    }

    private static BuildingAbilityCollection CreateAbilities(FacilityPartSpec spec)
    {
        BuildingAbilityCollection abilities = new BuildingAbilityCollection();
        bool hasSurvivalWork = GetSurvivalWorkTypes(spec.Code) != FacilityWorkType.None;
        AddAbility(abilities, new BuildingFacilityPartAbility { code = spec.Code });
        string[] productionTags = GetProductionFacilityTags(spec.Code);
        AddAbility(abilities, productionTags.Length > 0
            ? new BuildingSemanticTagsAbility { tags = productionTags }
            : null);
        AddAbility(abilities, CreateProductionWorkstationAbility(spec.Code));
        AddAbility(abilities, EnsureEconomyAbility(spec));
        AddAbility(abilities, CreateWorkAmountAbility(spec));
        AddAbility(abilities, spec.Core || hasSurvivalWork
            ? new BuildingFacilityAbility { settings = CreateFacilityData(spec) }
            : null);
        AddAbility(abilities, EnsureInternalStockAbility(spec));
        AddAbility(abilities, spec.RuntimeType == typeof(Shop)
            ? new BuildingRequiresStockAbility()
            : null);
        AddAbility(abilities, spec.RuntimeType == typeof(Shop)
            ? new BuildingStaffedServiceAbility()
            : null);
        AddAbility(abilities, spec.Core && spec.Roles != FacilityRole.None
            ? new BuildingRoomRequirementAbility()
            : null);
        AddAbility(abilities, new BuildingEvolutionAbility { settings = CreateEvolutionData(spec) });
        AddAbility(abilities, EnsureNeedRecoveryAbility(spec));
        AddAbility(abilities, EnsureRecreationalSubstanceServiceAbility(spec));
        AddAbility(abilities, EnsureStorageAbility(spec));
        AddAbility(abilities, EnsureSeatingAbility(spec));
        AddAbility(abilities, EnsureTableAbility(spec));
        AddAbility(abilities, EnsureServiceAbility(spec));
        AddAbility(abilities, EnsureProductionAbility(spec));
        BuildingLightingAbility lighting = EnsureLightingAbility(spec);
        AddAbility(abilities, lighting);
        AddAbility(abilities, EnsureRetailAbility(spec));
        AddAbility(abilities, EnsureTrainingAbility(spec));
        AddAbility(abilities, EnsureCleaningAbility(spec));
        AddAbility(abilities, EnsureSecurityAbility(spec));
        AddAbility(abilities, EnsureEquipmentCraftingAbility(spec));
        AddAbility(abilities, EnsureExpeditionRecoveryAbility(spec));
        AddAbility(abilities, EnsureMercenaryHiringAbility(spec));
        AddAbility(abilities, CreateWaterSourceAbility(spec.Code));
        AddAbility(abilities, CreateCookingAbility(spec.Code));
        AddAbility(abilities, CreateButcherAbility(spec.Code));
        AddAbility(abilities, CreatePreservationAbility(spec.Code));
        AddAbility(abilities, CreateMedicalAbility(spec.Code));
        AddAbility(abilities, CreateFuelConsumerAbility(spec.Code));
        AddAbility(abilities, CreateGolemRechargeAbility(spec.Code));
        AddAbility(abilities, CreateTemperatureAbility(spec.Code));
        AddAbility(abilities, CreateVentilationAbility(spec.Code));
        AddAbility(abilities, CreateEnvironmentThermalAbility(spec.Code));
        AddAbility(abilities, CreateEnvironmentAirAbility(spec.Code));
        AddAbility(abilities, CreateEnvironmentDuctAbility(spec.Code));
        AddAbility(abilities, CreateProtectiveEquipmentLockerAbility(spec.Code));
        AddAbility(abilities, CreateEnvironmentPowerConnectionAbility(spec.Code));
        AddAbility(abilities, CreateEnvironmentPowerConsumerAbility(spec.Code));
        AddAbility(abilities, CreateCropPlotAbility(spec.Code));

        return abilities;
    }

    private static BuildingProductionWorkstationAbility
        CreateProductionWorkstationAbility(string code)
    {
        string tag = code switch
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
        return string.IsNullOrWhiteSpace(tag)
            ? null
            : new BuildingProductionWorkstationAbility
            {
                workstationTag = tag,
                stockSensorInstallationItemId =
                    "component:stock-sensor-panel"
            };
    }

    private static BuildingMercenaryHiringAbility
        EnsureMercenaryHiringAbility(FacilityPartSpec spec)
    {
        return string.Equals(spec.Code, "D12", StringComparison.Ordinal)
            ? new BuildingMercenaryHiringAbility
            {
                rolePremium = 0,
                minimumCandidateSatisfaction = 65f
            }
            : null;
    }

    private static string[] GetProductionFacilityTags(string code)
    {
        return code switch
        {
            "D03" or "P15" => new[] { "cookbench" },
            "D12" or "P02" => new[] { "brewery" },
            "Q02" or "P19" => new[] { "alchemy" },
            "S08" or "P21" => new[] { "forge" },
            "G06" => new[] { "loot-appraisal" },
            "P01" => new[] { "mill" },
            "P03" => new[] { "sawmill" },
            "P04" => new[] { "charcoal-kiln" },
            "P05" => new[] { "stonecutter" },
            "P06" => new[] { "ore-sorter" },
            "P07" => new[] { "furnace" },
            "P08" => new[] { "steelworks" },
            "P09" => new[] { "jeweler" },
            "P10" => new[] { "arcane-forge" },
            "P11" => new[] { "loom" },
            "P12" => new[] { "tannery" },
            "P13" => new[] { "composter" },
            "P14" => new[] { "distillery" },
            "P16" => new[] { "smoker" },
            "P17" => new[] { "feedbench" },
            "P18" => new[] { "apothecary" },
            "P20" => new[] { "arcane-loom" },
            "P22" => new[] { "quarry" },
            "P23" => new[] { "crop-plot", "crop-outdoor" },
            "P24" => new[] { "crop-plot", "crop-indoor" },
            "P25" => new[] { "incinerator" },
            "R07" => new[] { "grand-project-office" },
            _ => Array.Empty<string>()
        };
    }

    private static BuildingCropPlotAbility CreateCropPlotAbility(string code)
    {
        BuildingCropPlotAbility ability;
        switch (code)
        {
            case "P23":
                ability = new BuildingCropPlotAbility();
                ability.Configure(
                    isIndoor: false,
                    growthRate: 1f,
                    waterRate: 1f,
                    compost: 0,
                    fuel: 0,
                    supplies: new[]
                    {
                        new ItemAmountDefinition(
                            "supply:nitrate-fertilizer",
                            1)
                    });
                return ability;
            case "P24":
                ability = new BuildingCropPlotAbility();
                ability.Configure(
                    isIndoor: true,
                    growthRate: 1.25f,
                    waterRate: 0.85f,
                    compost: 1,
                    fuel: 1,
                    supplies: new[]
                    {
                        new ItemAmountDefinition(
                            "supply:nitrate-fertilizer",
                            1),
                        new ItemAmountDefinition(
                            "supply:mushroom-substrate",
                            1)
                    });
                return ability;
            default:
                return null;
        }
    }

    private static void AddAbility(BuildingAbilityCollection abilities, BuildingAbility ability)
    {
        if (ability != null)
        {
            abilities.Add(ability);
        }
    }

    private static bool ReplaceAbility(BuildingSO building, BuildingAbility ability)
    {
        if (building == null || ability == null)
        {
            return false;
        }

        _ = ability switch
        {
            BuildingWaterSourceAbility => building.AbilityModules.Remove<BuildingWaterSourceAbility>(),
            BuildingCookingAbility => building.AbilityModules.Remove<BuildingCookingAbility>(),
            BuildingButcherAbility => building.AbilityModules.Remove<BuildingButcherAbility>(),
            BuildingPreservationAbility => building.AbilityModules.Remove<BuildingPreservationAbility>(),
            BuildingMedicalAbility => building.AbilityModules.Remove<BuildingMedicalAbility>(),
            BuildingFuelConsumerAbility => building.AbilityModules.Remove<BuildingFuelConsumerAbility>(),
            BuildingGolemRechargeAbility => building.AbilityModules.Remove<BuildingGolemRechargeAbility>(),
            BuildingTemperatureAbility => building.AbilityModules.Remove<BuildingTemperatureAbility>(),
            BuildingVentilationAbility => building.AbilityModules.Remove<BuildingVentilationAbility>(),
            _ => 0
        };

        building.AbilityModules.Add(ability);
        return true;
    }

    private static BuildingEconomyAbility EnsureEconomyAbility(FacilityPartSpec spec)
    {
        return new BuildingEconomyAbility
        {
            constructionValue = GetConstructionCost(spec),
            maintenance = spec.Core ? 2 + spec.Phase : spec.Phase,
            unlockPhase = Mathf.Clamp(spec.Phase, 1, 3),
            demolitionRefundRate = 0.5f
        };
    }

    private static BuildingWorkAmountAbility CreateWorkAmountAbility(
        FacilityPartSpec spec)
    {
        int cells = Mathf.Max(1, spec.Width);
        float baseWork = ResolveConstructionBaseWork(spec);
        float footprint = Mathf.Clamp(1f + 0.30f * (cells - 1), 1f, 2.5f);
        BuildingWorkAmountAbility ability = new BuildingWorkAmountAbility
        {
            constructionWorkRequired = RoundTo(baseWork * footprint, 4f),
            repairWorkRequired = RoundTo(baseWork * footprint * 0.25f, 2f),
            cleanWorkRequired = RoundTo(
                Mathf.Clamp(baseWork * 0.06f, 4f, 24f),
                2f),
            researchWorkRequired = 6f,
            operateWorkRequired = 10f
        };
        ability.SetConstructionProjectScale(spec.Phase switch
        {
            <= 1 => ProjectScale.SmallFacility,
            2 => ProjectScale.MediumFacility,
            _ => ProjectScale.IndustrialFacility
        });
        List<ItemAmountDefinition> constructionMaterials =
            ResolveConstructionMaterials(spec);
        string[] installationItems = spec.Code switch
        {
            "M04" => new[] { "craft:dreamweave-ritual-banner" },
            "P08" => new[] { "tool:alloy-crucible" },
            "P10" => new[] { "tool:mana-probe" },
            "P21" => new[] { "tool:powered-tool-head" },
            "P22" => new[]
            {
                "tool:deep-shaft-hoist",
                "tool:prospecting-kit"
            },
            _ => Array.Empty<string>()
        };
        foreach (string itemId in installationItems)
            AddOrMergeMaterial(constructionMaterials, itemId, 1);
        ability.SetConstructionMaterials(constructionMaterials);
        return ability;
    }

    private static List<ItemAmountDefinition> ResolveConstructionMaterials(
        FacilityPartSpec spec)
    {
        List<ItemAmountDefinition> result = new();
        void Add(string itemId, int amount) =>
            AddOrMergeMaterial(result, itemId, amount);

        switch (spec.Form)
        {
            case VisualForm.Hearth:
                Add("material:stone-block", 6);
                Add("material:iron-ingot", 2);
                break;
            case VisualForm.Grill:
                Add("material:stone-block", 4);
                Add("material:iron-ingot", 3);
                break;
            case VisualForm.Counter:
                Add("material:lumber", 5);
                Add("material:iron-ingot", 1);
                break;
            case VisualForm.Table:
                Add("material:lumber", 4 + Mathf.Max(0, spec.Width - 1) * 2);
                break;
            case VisualForm.Chair:
                Add("material:lumber", 3);
                if (HasTrait(spec, FacilityEvolutionTerms.Luxury))
                    Add("material:cloth", 2);
                break;
            case VisualForm.Bench:
                Add("material:lumber", 5);
                break;
            case VisualForm.Shelf:
                Add("material:lumber", 4);
                if (spec.InternalStockCapacity > 0 || HasTrait(spec, FacilityEvolutionTerms.Security))
                    Add("material:iron-ingot", 1);
                break;
            case VisualForm.WallRack:
                Add("material:lumber", 3);
                Add("material:iron-ingot", 1);
                break;
            case VisualForm.Cabinet:
                Add("material:treated-lumber", 4);
                Add("material:iron-ingot", 1);
                break;
            case VisualForm.Workbench:
                Add("material:treated-lumber", 5 + Mathf.Max(0, spec.Width - 1));
                Add("material:iron-ingot", 2);
                Add("material:stone-block", 2);
                break;
            case VisualForm.ArmorStand:
                Add("material:lumber", 3);
                Add("material:iron-ingot", 2);
                break;
            case VisualForm.Bed:
                Add("material:lumber", 5);
                Add("material:cloth", 3);
                break;
            case VisualForm.BunkBed:
                Add("material:lumber", 8);
                Add("material:cloth", 5);
                Add("material:iron-ingot", 2);
                break;
            case VisualForm.Sofa:
                Add("material:lumber", 5);
                Add("material:cloth", 5);
                break;
            case VisualForm.Desk:
                Add("material:treated-lumber", 5);
                Add("material:iron-ingot", 1);
                break;
            case VisualForm.Rune:
                Add("material:stone-block", 6);
                Add("component:rune-conductor", 2);
                break;
            case VisualForm.Dummy:
            case VisualForm.Target:
                Add("material:lumber", 5);
                Add("material:cloth", 2);
                break;
            case VisualForm.Weight:
                Add("material:stone-block", 6);
                Add("material:iron-ingot", 1);
                break;
            case VisualForm.Mat:
            case VisualForm.Rug:
                Add("material:cloth", 4 + Mathf.Max(0, spec.Width - 1));
                break;
            case VisualForm.Bell:
                Add("material:iron-ingot", 4);
                Add("material:lumber", 1);
                break;
            case VisualForm.Board:
            case VisualForm.MapTable:
                Add("material:treated-lumber", 5);
                Add("component:engineering-drawing", 1);
                break;
            case VisualForm.Flag:
                Add("material:cloth", 3);
                Add("material:lumber", 2);
                break;
            case VisualForm.Crates:
                Add("material:lumber", 4);
                break;
            case VisualForm.Barrels:
                Add("material:treated-lumber", 5);
                Add("material:iron-ingot", 1);
                break;
            case VisualForm.Sacks:
                Add("material:cloth", 4);
                break;
            case VisualForm.Toilet:
            case VisualForm.Sink:
            case VisualForm.Drain:
                Add("material:stone-block", 4);
                Add("material:iron-ingot", 2);
                break;
            case VisualForm.Partition:
                Add("material:lumber", 4);
                Add("material:cloth", 2);
                break;
            case VisualForm.Bath:
                Add("material:treated-lumber", 6);
                Add("material:iron-ingot", 2);
                break;
            case VisualForm.Torch:
                Add("material:lumber", 2);
                Add("material:iron-ingot", 1);
                break;
            case VisualForm.Chandelier:
                Add("material:iron-ingot", 5);
                Add("material:cloth", 1);
                break;
            case VisualForm.Portrait:
                Add("material:lumber", 2);
                Add("material:cloth", 2);
                break;
            case VisualForm.Candlestick:
                Add("material:iron-ingot", 2);
                break;
            case VisualForm.Skull:
                Add("material:stone-block", 3);
                break;
            case VisualForm.Sign:
                Add("material:lumber", 3);
                break;
            default:
                Add("material:lumber", 4);
                break;
        }

        if (HasTrait(spec, "Production") && spec.Core)
        {
            Add("material:iron-ingot", 2);
            Add("component:machine-parts", spec.Phase >= 2 ? 2 : 1);
            if (spec.Phase >= 3)
                Add("component:precision-parts", 1);
        }
        if (HasTrait(spec, FacilityEvolutionTerms.Mana))
        {
            Add("component:rune-conductor", 1);
            if (spec.Phase >= 3)
                Add("material:mana-alloy", 1);
        }
        if (HasTrait(spec, FacilityEvolutionTerms.Research) && spec.Core)
            Add("component:engineering-drawing", 1);
        if (HasTrait(spec, FacilityEvolutionTerms.Security)
            || HasTrait(spec, FacilityEvolutionTerms.Combat))
            Add("material:iron-ingot", 1);
        if (HasTrait(spec, FacilityEvolutionTerms.Hygiene))
        {
            Add("material:stone-block", 2);
            Add("material:iron-ingot", 1);
        }
        if (HasTrait(spec, "Cooling")
            || HasTrait(spec, "Climate")
            || HasTrait(spec, "Ventilation"))
        {
            Add("component:machine-parts", 2);
            Add("material:iron-ingot", 2);
        }
        return result;
    }

    private static void AddOrMergeMaterial(
        IList<ItemAmountDefinition> materials,
        string itemId,
        int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;
        for (int index = 0; index < materials.Count; index++)
        {
            ItemAmountDefinition existing = materials[index];
            if (!string.Equals(existing.ItemId, itemId, StringComparison.Ordinal))
                continue;
            materials[index] = new ItemAmountDefinition(itemId, existing.Amount + amount);
            return;
        }
        materials.Add(new ItemAmountDefinition(itemId, amount));
    }

    private static bool HasTrait(FacilityPartSpec spec, string trait) =>
        spec.Traits != null
        && spec.Traits.Any(value => string.Equals(value, trait, StringComparison.Ordinal));

    private static float ResolveConstructionBaseWork(FacilityPartSpec spec)
    {
        if (HasTrait(spec, FacilityEvolutionTerms.Mana) && spec.Core)
            return 360f;
        if (spec.Code == "P25")
            return 280f;
        if (HasTrait(spec, "Cooling")
            || HasTrait(spec, "Climate")
            || HasTrait(spec, "Ventilation")
            || HasTrait(spec, FacilityEvolutionTerms.Hygiene))
            return 160f;
        if (HasTrait(spec, FacilityEvolutionTerms.Security)
            || (spec.Roles & FacilityRole.Security) != 0)
            return 180f;
        if (HasTrait(spec, "Production") || (spec.WorkTypes & FacilityWorkType.Craft) != 0)
            return 110f;
        if (spec.InternalStockCapacity > 0
            || HasTrait(spec, FacilityEvolutionTerms.Storage)
            || (spec.Roles & FacilityRole.Logistics) != 0)
            return 48f;
        if (spec.Core)
            return 130f;
        return spec.ContributesToRoom ? 32f : 28f;
    }

    private static float RoundTo(float value, float step) =>
        Mathf.Max(step, Mathf.Round(value / step) * step);

    private static BuildingNeedRecoveryAbility EnsureNeedRecoveryAbility(FacilityPartSpec spec)
    {
        FacilityNeedRecoveryData recovery = GetRecovery(spec.Code);
        if (!recovery.HasEffect)
        {
            return null;
        }

        return new BuildingNeedRecoveryAbility
        {
            recovery = recovery
        };
    }

    private static BuildingRecreationalSubstanceServiceAbility
        EnsureRecreationalSubstanceServiceAbility(FacilityPartSpec spec)
    {
        return string.Equals(spec.Code, "D12", StringComparison.Ordinal)
            ? new BuildingRecreationalSubstanceServiceAbility
            {
                funRecovery = 8f,
                facilitySentiment = 0.25f
            }
            : null;
    }

    private static BuildingInternalStockAbility EnsureInternalStockAbility(FacilityPartSpec spec)
    {
        if (spec.InternalStockCapacity <= 0)
        {
            return null;
        }

        return new BuildingInternalStockAbility
        {
            capacity = spec.InternalStockCapacity,
            restockRequestThreshold = Mathf.Max(1, spec.InternalStockCapacity / 4)
        };
    }

    private static BuildingStorageAbility EnsureStorageAbility(FacilityPartSpec spec)
    {
        int capacity = GetStorageCapacity(spec.Code);
        if (capacity <= 0)
        {
            return null;
        }

        return new BuildingStorageAbility
        {
            category = GetStorageCategory(spec.Code),
            capacity = capacity,
            allCategories = spec.Code == "L01"
        };
    }

    private static BuildingSeatingAbility EnsureSeatingAbility(FacilityPartSpec spec)
    {
        int capacity = GetSeatCapacity(spec.Code);
        if (capacity <= 0)
        {
            return null;
        }

        return new BuildingSeatingAbility
        {
            capacity = capacity
        };
    }

    private static BuildingTableAbility EnsureTableAbility(FacilityPartSpec spec)
    {
        int capacity = GetTableCapacity(spec.Code);
        if (capacity <= 0)
        {
            return null;
        }

        return new BuildingTableAbility
        {
            capacity = capacity
        };
    }

    private static BuildingServiceAbility EnsureServiceAbility(FacilityPartSpec spec)
    {
        int capacity = GetServiceCapacity(spec.Code);
        if (capacity <= 0)
        {
            return null;
        }

        return new BuildingServiceAbility
        {
            capacity = capacity,
            contributesStockCategory = spec.Code is "D03" or "D04" or "D12",
            stockCategory = StockCategory.Food
        };
    }

    private static BuildingProductionAbility EnsureProductionAbility(FacilityPartSpec spec)
    {
        int amount = GetWorkOutput(spec.Code);
        if (amount <= 0)
        {
            return null;
        }

        return new BuildingProductionAbility
        {
            outputCategory = GetProductionCategory(spec.Code),
            amount = amount
        };
    }

    private static BuildingLightingAbility EnsureLightingAbility(FacilityPartSpec spec)
    {
        float intensity = GetLightIntensity(spec.Code);
        float radius = GetLightRadius(spec.Code);
        if (intensity <= 0f || radius <= 0f)
        {
            return null;
        }

        return new BuildingLightingAbility
        {
            intensity = intensity,
            radius = radius
        };
    }

    private static BuildingRetailAbility EnsureRetailAbility(FacilityPartSpec spec)
    {
        if (spec.Code is "S01" or "S02" or "S03" or "S04")
        {
            return new BuildingRetailAbility { category = StockCategory.General };
        }

        if (spec.Code is "S05" or "S06" or "S07")
        {
            return new BuildingRetailAbility { category = StockCategory.Weapon };
        }

        return null;
    }

    private static BuildingTrainingAbility EnsureTrainingAbility(FacilityPartSpec spec)
    {
        return spec.Code switch
        {
            "T01" or "T04" => new BuildingTrainingAbility
            {
                moodLabel = "근접 훈련 감각이 살아남",
                moodAmount = 4f
            },
            "T02" => new BuildingTrainingAbility
            {
                moodLabel = "과녁에 집중해 마음이 맑아짐",
                moodAmount = 3f
            },
            "T03" => new BuildingTrainingAbility
            {
                moodLabel = "묵직한 훈련으로 성취감이 남음",
                moodAmount = 5f
            },
            _ => null
        };
    }

    private static BuildingCleaningAbility EnsureCleaningAbility(FacilityPartSpec spec)
    {
        return spec.Code is "H01" or "H02" or "H03" or "H04" or "H05" or "H06" or "H07"
            ? new BuildingCleaningAbility { restoredCleanliness = 100f }
            : null;
    }

    private static BuildingSecurityAbility EnsureSecurityAbility(FacilityPartSpec spec)
    {
        return spec.Code is "G01" or "G02" or "G03" or "G04" or "G05" or "G06"
            ? new BuildingSecurityAbility { maxAlarmCharges = 3, chargesPerGuardWork = 1 }
            : null;
    }

    private static BuildingEquipmentCraftingAbility EnsureEquipmentCraftingAbility(FacilityPartSpec spec)
    {
        return spec.Code == "S08"
            ? new BuildingEquipmentCraftingAbility
            {
                craftableEquipmentIds = DefaultCraftableEquipmentIds.ToArray(),
                workUnitsPerCycle = 1f
            }
            : null;
    }

    private static BuildingExpeditionRecoveryAbility EnsureExpeditionRecoveryAbility(FacilityPartSpec spec)
    {
        return spec.Code switch
        {
            "R01" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.12f,
                injuryReduction = 0.03f,
                stressRecovery = 12f
            },
            "R02" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.22f,
                injuryReduction = 0.08f,
                stressRecovery = 22f
            },
            "R03" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.18f,
                injuryReduction = 0.05f,
                stressRecovery = 18f
            },
            "H04" => new BuildingExpeditionRecoveryAbility
            {
                healthHealRatio = 0.08f,
                injuryReduction = 0.12f,
                stressRecovery = 26f
            },
            _ => null
        };
    }

    private static BuildingWaterSourceAbility CreateWaterSourceAbility(string code)
    {
        return code switch
        {
            "H03" => new BuildingWaterSourceAbility { waterPerWork = 4, workSeconds = 0.9f },
            "H04" => new BuildingWaterSourceAbility { waterPerWork = 6, workSeconds = 1.2f },
            "L03" => new BuildingWaterSourceAbility { waterPerWork = 3, workSeconds = 1.1f, blockedByFreezingWeather = false },
            _ => null
        };
    }

    private static BuildingCookingAbility CreateCookingAbility(string code)
    {
        return code switch
        {
            "D01" => new BuildingCookingAbility { inputFood = 1, cookedMeals = 2, workSeconds = 1.1f, requiresFuel = true },
            "D02" => new BuildingCookingAbility { inputFood = 1, cookedMeals = 3, workSeconds = 1.3f, requiresFuel = true },
            "D03" => new BuildingCookingAbility { inputFood = 1, cookedMeals = 1, workSeconds = 1f, requiresFuel = false },
            _ => null
        };
    }

    private static BuildingButcherAbility CreateButcherAbility(string code)
    {
        return string.Equals(code, "D03", StringComparison.Ordinal)
            ? new BuildingButcherAbility { workSeconds = 1f }
            : null;
    }

    private static BuildingPreservationAbility CreatePreservationAbility(string code)
    {
        return code switch
        {
            "D10" => new BuildingPreservationAbility { freshnessMultiplier = 3f, preservedMealsPerCook = 1 },
            "L05" => new BuildingPreservationAbility { freshnessMultiplier = 5f, preservedMealsPerCook = 2 },
            _ => null
        };
    }

    private static BuildingMedicalAbility CreateMedicalAbility(string code)
    {
        return code switch
        {
            "R01" => new BuildingMedicalAbility { workSeconds = 1.4f, severityReduction = 0.22f, requiresMedicine = true },
            "R02" => new BuildingMedicalAbility { workSeconds = 1.2f, severityReduction = 0.38f, requiresMedicine = true },
            "R03" => new BuildingMedicalAbility { workSeconds = 1.3f, severityReduction = 0.3f, requiresMedicine = true },
            "H04" => new BuildingMedicalAbility { workSeconds = 1.1f, severityReduction = 0.28f, requiresMedicine = false },
            _ => null
        };
    }

    private static BuildingFuelConsumerAbility CreateFuelConsumerAbility(string code)
    {
        return code switch
        {
            "D01" => new BuildingFuelConsumerAbility { fuelPerRefuel = 1, workSeconds = 0.8f, warmth = 8f, lightSafety = 6f },
            "D02" => new BuildingFuelConsumerAbility { fuelPerRefuel = 1, workSeconds = 0.9f, warmth = 10f, lightSafety = 7f },
            "E01" => new BuildingFuelConsumerAbility { fuelPerRefuel = 1, workSeconds = 0.6f, warmth = 2f, lightSafety = 14f },
            "E02" => new BuildingFuelConsumerAbility { fuelPerRefuel = 1, workSeconds = 0.8f, warmth = 14f, lightSafety = 10f },
            "E03" => new BuildingFuelConsumerAbility { fuelPerRefuel = 1, workSeconds = 0.8f, warmth = 3f, lightSafety = 18f },
            "E07" => new BuildingFuelConsumerAbility { fuelPerRefuel = 1, workSeconds = 0.5f, warmth = 1f, lightSafety = 8f },
            _ => null
        };
    }

    private static BuildingGolemRechargeAbility CreateGolemRechargeAbility(
        string code) => code is "M02" or "P1_ManaStorage"
            ? new BuildingGolemRechargeAbility
            {
                materialItemId = "resource:mana-crystal",
                materialQuantity = 1,
                requiredWork = 100f,
                restoredCharge = 50f
            }
            : null;

    private static BuildingTemperatureAbility CreateTemperatureAbility(string code)
    {
        return code switch
        {
            "D01" => new BuildingTemperatureAbility { roomTemperatureOffset = 4f, coldProtection = 6f, heatProtection = 1f },
            "D02" => new BuildingTemperatureAbility { roomTemperatureOffset = 5f, coldProtection = 8f, heatProtection = 1f },
            "E02" => new BuildingTemperatureAbility { roomTemperatureOffset = 6f, coldProtection = 12f, heatProtection = 0f },
            "R02" => new BuildingTemperatureAbility { roomTemperatureOffset = 1f, coldProtection = 5f, heatProtection = 3f },
            "R03" => new BuildingTemperatureAbility { roomTemperatureOffset = 1f, coldProtection = 4f, heatProtection = 2f },
            _ => null
        };
    }

    private static BuildingVentilationAbility CreateVentilationAbility(string code)
    {
        return code switch
        {
            "H03" => new BuildingVentilationAbility { hygieneRiskReduction = 10f, smokeRiskReduction = 4f },
            "H04" => new BuildingVentilationAbility { hygieneRiskReduction = 16f, smokeRiskReduction = 6f },
            "H06" => new BuildingVentilationAbility { hygieneRiskReduction = 12f, smokeRiskReduction = 4f },
            "H07" => new BuildingVentilationAbility { hygieneRiskReduction = 18f, smokeRiskReduction = 8f },
            "E09" => new BuildingVentilationAbility { hygieneRiskReduction = 3f, smokeRiskReduction = 3f },
            _ => null
        };
    }

    private static BuildingThermalEmitterAbility
        CreateEnvironmentThermalAbility(string code)
    {
        return code switch
        {
            "E10" => new BuildingThermalEmitterAbility
            {
                mode = ThermalEmitterMode.Cool,
                targetTemperatureC = 8f,
                playerConfigurable = true,
                minimumTargetTemperatureC = 2f,
                maximumTargetTemperatureC = 8f,
                degreesPerSecond = 3f,
                radius = 3,
                requiresPower = true,
                exhaustOffset = Vector2Int.right,
                exhaustHeatMultiplier = 1.15f
            },
            "E11" => new BuildingThermalEmitterAbility
            {
                mode = ThermalEmitterMode.Thermostat,
                targetTemperatureC = 22f,
                playerConfigurable = true,
                minimumTargetTemperatureC = 2f,
                maximumTargetTemperatureC = 30f,
                degreesPerSecond = 2.5f,
                radius = 4,
                requiresPower = true
            },
            _ => null
        };
    }

    private static BuildingAirExchangeAbility
        CreateEnvironmentAirAbility(string code)
    {
        return code switch
        {
            "E11" => new BuildingAirExchangeAbility
            {
                targetAirQuality = 100f,
                qualityPerSecond = 6f,
                radius = 4,
                requiresPower = true
            },
            "E13" => new BuildingAirExchangeAbility
            {
                targetAirQuality = 100f,
                qualityPerSecond = 8f,
                radius = 3,
                requiresPower = true
            },
            "E14" => new BuildingAirExchangeAbility
            {
                targetAirQuality = 100f,
                qualityPerSecond = 12f,
                radius = 4,
                requiresPower = true
            },
            _ => null
        };
    }

    private static BuildingAirDuctAbility
        CreateEnvironmentDuctAbility(string code)
    {
        return code is "E12" or "E13" or "E14"
            ? new BuildingAirDuctAbility { exchangeRate = 0.65f }
            : null;
    }

    private static BuildingProtectiveEquipmentLockerAbility
        CreateProtectiveEquipmentLockerAbility(string code)
    {
        return code == "L08"
            ? new BuildingProtectiveEquipmentLockerAbility
            {
                capacity = 4,
                serviceRadius = 12
            }
            : null;
    }

    private static BuildingUtilityConnectionAbility
        CreateEnvironmentPowerConnectionAbility(string code)
    {
        return code is "E10" or "E11" or "E13" or "E14"
            ? new BuildingUtilityConnectionAbility
            {
                channels = UtilityChannel.Power,
                maxThroughput = 20f,
                normallyOpen = true
            }
            : null;
    }

    private static BuildingPowerConsumerAbility
        CreateEnvironmentPowerConsumerAbility(string code)
    {
        return code switch
        {
            "E10" => new BuildingPowerConsumerAbility
            {
                demandPerSecond = 8f,
                priority = PowerPriority.Essential,
                minimumSupplyFraction = 0.75f
            },
            "E11" => new BuildingPowerConsumerAbility
            {
                demandPerSecond = 10f,
                priority = PowerPriority.Essential,
                minimumSupplyFraction = 0.75f
            },
            "E13" => new BuildingPowerConsumerAbility
            {
                demandPerSecond = 3f,
                priority = PowerPriority.Essential,
                minimumSupplyFraction = 0.5f
            },
            "E14" => new BuildingPowerConsumerAbility
            {
                demandPerSecond = 5f,
                priority = PowerPriority.Essential,
                minimumSupplyFraction = 0.5f
            },
            _ => null
        };
    }

    private static FacilityData CreateFacilityData(FacilityPartSpec spec)
    {
        FacilityWorkType workTypes = spec.WorkTypes | GetSurvivalWorkTypes(spec.Code);

        FacilityData facility = new FacilityData
        {
            roles = spec.Roles,
            capacity = spec.Core ? Mathf.Max(1, spec.Capacity) : 0,
            useDuration = spec.Core ? Mathf.Max(0.5f, spec.UseDuration) : 0f,
            requiredWorkers = workTypes == FacilityWorkType.None ? 0 : 1,
            disabledWhenDamaged = true
        };
        facility.SetSupportedWorkTypeIds(ToWorkTypeIds(workTypes));
        return facility;
    }

    private static FacilityWorkType GetSurvivalWorkTypes(string code)
    {
        FacilityWorkType result = FacilityWorkType.None;
        if (string.Equals(code, "G06", StringComparison.Ordinal))
        {
            result |= FacilityWorkType.Craft;
        }
        if (CreateWaterSourceAbility(code) != null) result |= FacilityWorkType.DrawWater;
        if (CreateCookingAbility(code) != null) result |= FacilityWorkType.Cook;
        if (CreateButcherAbility(code) != null) result |= FacilityWorkType.Butcher;
        if (CreateMedicalAbility(code) != null) result |= FacilityWorkType.Treat;
        if (CreateFuelConsumerAbility(code) != null) result |= FacilityWorkType.Refuel;
        if (CreateGolemRechargeAbility(code) != null) result |= FacilityWorkType.Refuel;
        return result;
    }

    private static IEnumerable<WorkTypeId> ToWorkTypeIds(FacilityWorkType workTypes)
    {
        if ((workTypes & FacilityWorkType.Operate) != 0) yield return BuiltInWorkTypeIds.Operate;
        if ((workTypes & FacilityWorkType.Restock) != 0) yield return BuiltInWorkTypeIds.Restock;
        if ((workTypes & FacilityWorkType.Construct) != 0) yield return BuiltInWorkTypeIds.Construct;
        if ((workTypes & FacilityWorkType.Repair) != 0) yield return BuiltInWorkTypeIds.Repair;
        if ((workTypes & FacilityWorkType.Clean) != 0) yield return BuiltInWorkTypeIds.Clean;
        if ((workTypes & FacilityWorkType.Research) != 0) yield return BuiltInWorkTypeIds.Research;
        if ((workTypes & FacilityWorkType.Guard) != 0) yield return BuiltInWorkTypeIds.Guard;
        if ((workTypes & FacilityWorkType.Reception) != 0) yield return BuiltInWorkTypeIds.Reception;
        if ((workTypes & FacilityWorkType.Rescue) != 0) yield return BuiltInWorkTypeIds.Rescue;
        if ((workTypes & FacilityWorkType.Rest) != 0) yield return BuiltInWorkTypeIds.Rest;
        if ((workTypes & FacilityWorkType.Craft) != 0) yield return BuiltInWorkTypeIds.Craft;
        if ((workTypes & FacilityWorkType.Haul) != 0) yield return BuiltInWorkTypeIds.Haul;
        if ((workTypes & FacilityWorkType.Hunt) != 0) yield return BuiltInWorkTypeIds.Hunt;
        if ((workTypes & FacilityWorkType.Butcher) != 0) yield return BuiltInWorkTypeIds.Butcher;
        if ((workTypes & FacilityWorkType.DrawWater) != 0) yield return BuiltInWorkTypeIds.DrawWater;
        if ((workTypes & FacilityWorkType.Cook) != 0) yield return BuiltInWorkTypeIds.Cook;
        if ((workTypes & FacilityWorkType.Treat) != 0) yield return BuiltInWorkTypeIds.Treat;
        if ((workTypes & FacilityWorkType.Refuel) != 0) yield return BuiltInWorkTypeIds.Refuel;
        if ((workTypes & FacilityWorkType.Gather) != 0) yield return BuiltInWorkTypeIds.Gather;
        if ((workTypes & FacilityWorkType.Sow) != 0) yield return BuiltInWorkTypeIds.Sow;
        if ((workTypes & FacilityWorkType.Harvest) != 0) yield return BuiltInWorkTypeIds.Harvest;
        if ((workTypes & FacilityWorkType.Logging) != 0) yield return BuiltInWorkTypeIds.Logging;
        if ((workTypes & FacilityWorkType.Quarry) != 0) yield return BuiltInWorkTypeIds.Quarry;
        if ((workTypes & FacilityWorkType.AnimalCare) != 0) yield return BuiltInWorkTypeIds.AnimalCare;
        if ((workTypes & FacilityWorkType.GrandProject) != 0) yield return BuiltInWorkTypeIds.GrandProject;
    }

    private static FacilityWorkType GetSurvivalFallbackWorkTypes(BuildingSO building)
    {
        FacilityWorkType result = FacilityWorkType.None;
        if (building == null)
        {
            return result;
        }

        if (building.GetAbility<BuildingWaterSourceAbility>() != null) result |= FacilityWorkType.DrawWater;
        if (building.GetAbility<BuildingCookingAbility>() != null) result |= FacilityWorkType.Cook;
        if (building.GetAbility<BuildingButcherAbility>() != null) result |= FacilityWorkType.Butcher;
        if (building.GetAbility<BuildingMedicalAbility>() != null) result |= FacilityWorkType.Treat;
        if (building.GetAbility<BuildingFuelConsumerAbility>() != null) result |= FacilityWorkType.Refuel;
        if (building.GetAbility<BuildingGolemRechargeAbility>() != null) result |= FacilityWorkType.Refuel;
        return result;
    }

    private static FacilityEvolutionContributionData CreateEvolutionData(FacilityPartSpec spec)
    {
        string[] traits = spec.Traits ?? Array.Empty<string>();
        return new FacilityEvolutionContributionData
        {
            contributesToRoomProfile = spec.ContributesToRoom,
            tags = traits.Distinct(StringComparer.Ordinal).ToArray(),
            scores = traits
                .Distinct(StringComparer.Ordinal)
                .Select((trait) => new FacilityEvolutionValue(trait, GetTraitScore(trait)))
                .ToArray(),
            metrics = spec.Metrics ?? Array.Empty<FacilityEvolutionValue>()
        };
    }

    private static FacilityNeedRecoveryData GetRecovery(string code)
    {
        return code switch
        {
            "D01" => Recovery(hunger: 35f, mood: 4f),
            "D02" => Recovery(hunger: 48f, mood: 8f),
            "D04" => Recovery(hunger: 25f, mood: 3f),
            "D12" => default,
            "R01" => Recovery(sleep: 35f, mood: 4f),
            "R02" => Recovery(sleep: 52f, mood: 10f),
            "R03" => Recovery(sleep: 42f, mood: 3f),
            "R04" => Recovery(sleep: 25f, mood: 6f, fun: 8f),
            "Q01" => Recovery(mood: 4f, fun: 10f),
            "Q02" => Recovery(mood: 7f, fun: 12f),
            "M01" => Recovery(mood: 6f),
            "M02" => Recovery(mood: 10f),
            "M04" => Recovery(mood: 12f, fun: 5f),
            "T01" => Recovery(mood: 4f, fun: 18f),
            "T02" => Recovery(mood: 3f, fun: 16f),
            "T03" => Recovery(mood: 5f, fun: 12f),
            "H01" => Recovery(excretion: 75f, mood: 2f),
            "H03" => Recovery(hygiene: 45f, mood: 3f),
            "H04" => Recovery(sleep: 12f, hygiene: 85f, mood: 10f),
            _ => default
        };
    }

    private static FacilityNeedRecoveryData Recovery(
        float sleep = 0f,
        float mood = 0f,
        float fun = 0f,
        float hunger = 0f,
        float excretion = 0f,
        float hygiene = 0f)
    {
        return new FacilityNeedRecoveryData
        {
            sleep = sleep,
            mood = mood,
            fun = fun,
            hunger = hunger,
            excretion = excretion,
            hygiene = hygiene
        };
    }

    private static int GetStorageCapacity(string code)
    {
        return code switch
        {
            "D10" => 16,
            "S04" => 12,
            "S07" => 16,
            "R06" => 8,
            "R09" => 6,
            "Q03" => 10,
            "Q04" => 12,
            "Q05" => 8,
            "M01" => 18,
            "M02" => 36,
            "L01" => 60,
            "L02" => 16,
            "L03" => 14,
            "L04" => 12,
            "L05" or "L06" or "L07" => 20,
            "H06" => 8,
            _ => 0
        };
    }

    private static StockCategory GetStorageCategory(string code)
    {
        return code switch
        {
            "D10" or "L03" or "L04" or "L05" => StockCategory.Food,
            "S07" or "L06" => StockCategory.Weapon,
            "Q04" or "M01" or "M02" or "L07" => StockCategory.Mana,
            _ => StockCategory.General
        };
    }

    private static int GetSeatCapacity(string code)
    {
        return code switch { "D07" or "D08" or "R08" => 1, "D09" => 2, _ => 0 };
    }

    private static int GetTableCapacity(string code)
    {
        return code switch { "D05" => 2, "D06" => 6, _ => 0 };
    }

    private static int GetServiceCapacity(string code)
    {
        return code switch { "S02" => 1, "S03" => 1, "D03" or "D04" or "D12" => 1, _ => 0 };
    }

    private static int GetWorkOutput(string code)
    {
        return code switch
        {
            "D01" => 4,
            "D02" => 6,
            "Q02" => 3,
            "M01" => 2,
            "M02" => 4,
            "M04" => 5,
            _ => 0
        };
    }

    private static int GetConstructionCost(FacilityPartSpec spec)
    {
        return 20 + spec.Width * 15 + spec.Phase * 25 + (spec.Core ? 35 : 0);
    }

    private static StockCategory GetProductionCategory(string code)
    {
        if (code is "Q02" or "M01" or "M02" or "M04")
        {
            return StockCategory.Mana;
        }

        return StockCategory.Food;
    }

    private static float GetLightIntensity(string code)
    {
        return code switch { "E01" => 0.75f, "E02" => 0.9f, "E03" => 1.15f, "E07" => 0.5f, _ => 0f };
    }

    private static float GetLightRadius(string code)
    {
        return code switch { "E01" => 2.8f, "E02" => 3.2f, "E03" => 4.2f, "E07" => 2.1f, _ => 0f };
    }

    private static float GetTraitScore(string trait)
    {
        return trait switch
        {
            FacilityEvolutionTerms.Luxury => 3f,
            FacilityEvolutionTerms.Hygiene => 3f,
            FacilityEvolutionTerms.Service => 2.5f,
            FacilityEvolutionTerms.Storage => 2.5f,
            FacilityEvolutionTerms.Security => 2.5f,
            _ => 2f
        };
    }

    private static void EnsureShopStock(int shopId)
    {
        const string assetPath = StockFolder + "/S01_SalesCounterStock.asset";
        StockInfo stock = AssetDatabase.LoadAssetAtPath<StockInfo>(assetPath);
        if (stock == null)
        {
            stock = ScriptableObject.CreateInstance<StockInfo>();
            AssetDatabase.CreateAsset(stock, assetPath);
        }

        StockInfo source = AssetDatabase.LoadAssetAtPath<StockInfo>(GeneralStockSource);
        SaleItem generalItem = EnsureGeneralSaleItem();
        SaleItem foodItem = AssetDatabase.LoadAssetAtPath<SaleItem>(SaleItemFolder + "/햄버거.asset");
        SaleItem swordItem = AssetDatabase.LoadAssetAtPath<SaleItem>(SaleItemFolder + "/도란검.asset");
        SaleItem shieldItem = AssetDatabase.LoadAssetAtPath<SaleItem>(SaleItemFolder + "/도란방패.asset");
        stock.id = 3000;
        stock.shopId = shopId;
        stock.stocks = new[]
            {
                new Tuple<SaleItem, int>(generalItem, 12),
                new Tuple<SaleItem, int>(foodItem, 12),
                new Tuple<SaleItem, int>(swordItem, 8),
                new Tuple<SaleItem, int>(shieldItem, 8)
            }
            .Where((tuple) => tuple.Item1 != null)
            .ToList();
        stock.multifly = source != null ? source.multifly : 1f;
        EditorUtility.SetDirty(stock);
    }

    private static SaleItem EnsureGeneralSaleItem()
    {
        const string assetPath = SaleItemFolder + "/모험용품.asset";
        SaleItem item = AssetDatabase.LoadAssetAtPath<SaleItem>(assetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<SaleItem>();
            AssetDatabase.CreateAsset(item, assetPath);
        }

        item.SetItemDefinitionId("tool:field-repair-kit");


        item.id = 4000;
        item.itemName = "모험용품";
        item.category = StockCategory.General;
        ItemDefinitionSO definition = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
            ?.GetItemDefinitions<ItemDefinitionCatalogSO>()
            ?.Definitions
            ?.FirstOrDefault(value => value != null
                && string.Equals(
                    value.ItemId,
                    "tool:field-repair-kit",
                    StringComparison.Ordinal));
        item.cost = GoldEconomyBalanceRules.CalculateRetailBasePrice(
            definition != null ? definition.UnitPrice : 1);
        item.itemSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/S02.png");
        item.buyevent = Array.Empty<OnBuyItemSO>();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void HideLegacyRoomAssets()
    {
        foreach (string assetPath in LegacyRoomAssetPaths)
        {
            BuildingSO legacy = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
            if (legacy == null)
            {
                continue;
            }

            legacy.unlocked = false;
            legacy.ConfigureCompatibilityStatus(true);
            EditorUtility.SetDirty(legacy);
        }
    }

    private static void WriteSprite(FacilityPartSpec spec, string spritePath)
    {
        int width = Mathf.Max(24, spec.Width * 24);
        const int height = 48;
        PixelCanvas canvas = new PixelCanvas(width, height);
        DrawProp(canvas, spec);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = spec.Code;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(canvas.Pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(spritePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static void ConfigureSpriteImport(string spritePath)
    {
        AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer == null)
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

    private static FacilityPartSpec[] CreateSpecs()
    {
        return new[]
        {
            Core("D01", "간이화덕", 1, GridLayer.Building, BuildingCategory.Shop, FacilityRole.Meal, FacilityWorkType.Operate | FacilityWorkType.Clean | FacilityWorkType.Repair, VisualForm.Hearth, Traits("Cooking")),
            Core("D02", "고기그릴", 2, GridLayer.Building, BuildingCategory.Shop, FacilityRole.Meal, FacilityWorkType.Operate | FacilityWorkType.Clean | FacilityWorkType.Repair, VisualForm.Grill, Traits("Cooking", "Meat")),
            Support("D03", "조리손질대", 2, GridLayer.Building, BuildingCategory.Shop, VisualForm.Workbench, Traits("Cooking", FacilityEvolutionTerms.Service)),
            Core("D04", "배식카운터", 2, GridLayer.Building, BuildingCategory.Shop, FacilityRole.Meal, FacilityWorkType.Operate | FacilityWorkType.Clean, VisualForm.Counter, Traits(FacilityEvolutionTerms.Service), Metrics((FacilityEvolutionTerms.CounterCount, 1f))),
            Support("D05", "소형식탁", 2, GridLayer.Building, BuildingCategory.Shop, VisualForm.Table, Traits(FacilityEvolutionTerms.Dining), Metrics((FacilityEvolutionTerms.TableCount, 1f))),
            Support("D06", "대형연회식탁", 3, GridLayer.Building, BuildingCategory.Shop, VisualForm.Table, Traits(FacilityEvolutionTerms.Dining, FacilityEvolutionTerms.Luxury), Metrics((FacilityEvolutionTerms.LargeTableCount, 1f)), phase: 2),
            Support("D07", "목제의자", 1, GridLayer.Building, BuildingCategory.Shop, VisualForm.Chair, Traits(FacilityEvolutionTerms.Dining), Metrics((FacilityEvolutionTerms.SeatCount, 1f))),
            Support("D08", "푹신한의자", 1, GridLayer.Building, BuildingCategory.Shop, VisualForm.Chair, Traits(FacilityEvolutionTerms.Dining, FacilityEvolutionTerms.Luxury), Metrics((FacilityEvolutionTerms.SeatCount, 1f), (FacilityEvolutionTerms.PrivateSeatCount, 1f)), phase: 2),
            Support("D09", "긴벤치", 2, GridLayer.Building, BuildingCategory.Shop, VisualForm.Bench, Traits(FacilityEvolutionTerms.Dining, FacilityEvolutionTerms.Crowd), Metrics((FacilityEvolutionTerms.SeatCount, 2f))),
            Support("D10", "식재료선반", 1, GridLayer.Building, BuildingCategory.Shop, VisualForm.Shelf, Traits(FacilityEvolutionTerms.Storage, "Cooking")),
            Support("D11", "고기걸이", 1, GridLayer.WallFixture, BuildingCategory.Shop, VisualForm.WallRack, Traits("Meat", FacilityEvolutionTerms.Brutal), phase: 2),
            Core("D12", "술음료장", 1, GridLayer.Building, BuildingCategory.Shop, FacilityRole.Entertainment, FacilityWorkType.Operate | FacilityWorkType.Clean | FacilityWorkType.Repair, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Service, FacilityEvolutionTerms.Luxury), phase: 2),

            Core("S01", "판매카운터", 2, GridLayer.Building, BuildingCategory.Shop, FacilityRole.Purchase, FacilityWorkType.Operate | FacilityWorkType.Restock | FacilityWorkType.Repair, VisualForm.Counter, Traits(FacilityEvolutionTerms.Service), Metrics((FacilityEvolutionTerms.CounterCount, 1f)), runtimeType: typeof(Shop), internalStockCapacity: 24),
            Support("S02", "잡화진열선반", 2, GridLayer.Building, BuildingCategory.Shop, VisualForm.Shelf, Traits("Shop", FacilityEvolutionTerms.Service)),
            Support("S03", "잠금진열장", 2, GridLayer.Building, BuildingCategory.Shop, VisualForm.Cabinet, Traits("Shop", FacilityEvolutionTerms.Luxury, FacilityEvolutionTerms.Security), phase: 2),
            Support("S04", "잡화상자", 1, GridLayer.Building, BuildingCategory.Shop, VisualForm.Crates, Traits(FacilityEvolutionTerms.Storage)),
            Support("S05", "무기거치대", 1, GridLayer.WallFixture, BuildingCategory.Shop, VisualForm.WallRack, Traits(FacilityEvolutionTerms.Combat)),
            Support("S06", "갑옷거치대", 1, GridLayer.Building, BuildingCategory.Shop, VisualForm.ArmorStand, Traits(FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Defense)),
            Support("S07", "무기보관함", 1, GridLayer.Building, BuildingCategory.Shop, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Storage, FacilityEvolutionTerms.Security)),
            Core("S08", "대장작업대", 2, GridLayer.Building, BuildingCategory.Shop, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits(FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Service), phase: 2),

            Core("R01", "간이침대", 2, GridLayer.Building, BuildingCategory.Special, FacilityRole.Rest, FacilityWorkType.Rest | FacilityWorkType.Repair, VisualForm.Bed, Traits(FacilityEvolutionTerms.Rest)),
            Core("R02", "정식침대", 2, GridLayer.Building, BuildingCategory.Special, FacilityRole.Rest, FacilityWorkType.Rest | FacilityWorkType.Repair, VisualForm.Bed, Traits(FacilityEvolutionTerms.Rest, FacilityEvolutionTerms.Luxury), phase: 2),
            Core("R03", "이층침대", 2, GridLayer.Building, BuildingCategory.Special, FacilityRole.Rest, FacilityWorkType.Rest | FacilityWorkType.Repair, VisualForm.BunkBed, Traits(FacilityEvolutionTerms.Rest, FacilityEvolutionTerms.Crowd), capacity: 2, phase: 2),
            Core("R04", "휴식용소파", 2, GridLayer.Building, BuildingCategory.Special, FacilityRole.Rest, FacilityWorkType.Rest | FacilityWorkType.Repair, VisualForm.Sofa, Traits(FacilityEvolutionTerms.Rest), capacity: 2),
            Support("R05", "협탁", 1, GridLayer.Building, BuildingCategory.Special, VisualForm.Table, Traits(FacilityEvolutionTerms.Luxury, FacilityEvolutionTerms.Quiet), phase: 2),
            Support("R06", "옷장", 1, GridLayer.Building, BuildingCategory.Special, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Storage, FacilityEvolutionTerms.Luxury), phase: 2),
            Core("R07", "영주집무책상", 2, GridLayer.Building, BuildingCategory.Special, FacilityRole.Administration, FacilityWorkType.Operate | FacilityWorkType.Repair | FacilityWorkType.GrandProject, VisualForm.Desk, Traits("Administration", FacilityEvolutionTerms.Service)),
            Support("R08", "지휘의자", 1, GridLayer.Building, BuildingCategory.Special, VisualForm.Chair, Traits("Administration", FacilityEvolutionTerms.Noble, FacilityEvolutionTerms.Luxury)),
            Support("R09", "개인보관함", 1, GridLayer.Building, BuildingCategory.Special, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Storage)),
            Support("R10", "침실용책장", 1, GridLayer.Building, BuildingCategory.Special, VisualForm.Shelf, Traits(FacilityEvolutionTerms.Quiet, FacilityEvolutionTerms.Research), phase: 2),

            Core("Q01", "연구책상", 2, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.Research, FacilityWorkType.Research | FacilityWorkType.Repair, VisualForm.Desk, Traits(FacilityEvolutionTerms.Research)),
            Core("Q02", "연금술작업대", 2, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.Research | FacilityRole.Mana, FacilityWorkType.Research | FacilityWorkType.Operate | FacilityWorkType.Repair, VisualForm.Workbench, Traits(FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Mana, FacilityEvolutionTerms.Ritual)),
            Support("Q03", "연구용책장", 1, GridLayer.Building, BuildingCategory.Crafting, VisualForm.Shelf, Traits(FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Storage)),
            Support("Q04", "시약선반", 1, GridLayer.Building, BuildingCategory.Crafting, VisualForm.Shelf, Traits(FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Mana)),
            Support("Q05", "표본보관장", 1, GridLayer.Building, BuildingCategory.Crafting, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Fear), phase: 2),
            Support("Q06", "설계판", 1, GridLayer.WallFixture, BuildingCategory.Crafting, VisualForm.Board, Traits(FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Service), phase: 2),

            Core("M01", "마력수정선반", 1, GridLayer.Building, BuildingCategory.Resource, FacilityRole.Mana, FacilityWorkType.Operate | FacilityWorkType.Repair, VisualForm.Shelf, Traits(FacilityEvolutionTerms.Mana, FacilityEvolutionTerms.Storage)),
            Core("M02", "마력저장조", 2, GridLayer.Building, BuildingCategory.Resource, FacilityRole.Mana, FacilityWorkType.Operate | FacilityWorkType.Repair, VisualForm.Rune, Traits(FacilityEvolutionTerms.Mana, FacilityEvolutionTerms.Storage), capacity: 2),
            Support("M03", "룬안정기", 1, GridLayer.Building, BuildingCategory.Resource, VisualForm.Rune, Traits(FacilityEvolutionTerms.Mana, FacilityEvolutionTerms.Security)),
            Core("M04", "의식초점석", 1, GridLayer.Building, BuildingCategory.Resource, FacilityRole.Mana, FacilityWorkType.Operate | FacilityWorkType.Research | FacilityWorkType.Repair, VisualForm.Rune, Traits(FacilityEvolutionTerms.Mana, FacilityEvolutionTerms.Ritual), phase: 2),

            Core("T01", "훈련허수아비", 1, GridLayer.Building, BuildingCategory.Special, FacilityRole.Training, FacilityWorkType.Operate | FacilityWorkType.Repair, VisualForm.Dummy, Traits(FacilityEvolutionTerms.Training, FacilityEvolutionTerms.Combat)),
            Core("T02", "사격과녁", 1, GridLayer.Building, BuildingCategory.Special, FacilityRole.Training, FacilityWorkType.Operate | FacilityWorkType.Repair, VisualForm.Target, Traits(FacilityEvolutionTerms.Training, FacilityEvolutionTerms.Combat)),
            Core("T03", "중량훈련석", 1, GridLayer.Building, BuildingCategory.Special, FacilityRole.Training, FacilityWorkType.Operate | FacilityWorkType.Repair, VisualForm.Weight, Traits(FacilityEvolutionTerms.Training)),
            Support("T04", "대련매트", 2, GridLayer.FloorOverlay, BuildingCategory.Special, VisualForm.Mat, Traits(FacilityEvolutionTerms.Training, FacilityEvolutionTerms.Combat)),

            Core("G01", "경비초소책상", 2, GridLayer.Building, BuildingCategory.Special, FacilityRole.Security, FacilityWorkType.Guard | FacilityWorkType.Operate | FacilityWorkType.Repair, VisualForm.Desk, Traits("Guard", FacilityEvolutionTerms.Defense, FacilityEvolutionTerms.Security)),
            Core("G02", "경보종", 1, GridLayer.WallFixture, BuildingCategory.Special, FacilityRole.Security, FacilityWorkType.Guard | FacilityWorkType.Repair, VisualForm.Bell, Traits(FacilityEvolutionTerms.Defense, FacilityEvolutionTerms.Security)),
            Support("G03", "순찰상황판", 1, GridLayer.WallFixture, BuildingCategory.Special, VisualForm.Board, Traits(FacilityEvolutionTerms.Security, FacilityEvolutionTerms.Service), phase: 2),
            Core("G04", "전술지도탁자", 2, GridLayer.Building, BuildingCategory.Special, FacilityRole.Security, FacilityWorkType.Guard | FacilityWorkType.Operate | FacilityWorkType.Repair, VisualForm.MapTable, Traits(FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Defense, FacilityEvolutionTerms.Service), phase: 2),
            Support("G05", "전투깃발", 1, GridLayer.WallFixture, BuildingCategory.Special, VisualForm.Flag, Traits(FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Brutal), phase: 2),
            Support("G06", "전리품거치대", 1, GridLayer.WallFixture, BuildingCategory.Special, VisualForm.WallRack, Traits(FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Luxury, FacilityEvolutionTerms.Fear), phase: 3),

            Core("L01", "대형보관선반", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.Logistics, FacilityWorkType.Restock | FacilityWorkType.Repair, VisualForm.Shelf, Traits(FacilityEvolutionTerms.Logistics, FacilityEvolutionTerms.Storage), capacity: 1, internalStockCapacity: 60),
            Support("L02", "상자더미", 1, GridLayer.Building, BuildingCategory.Production, VisualForm.Crates, Traits(FacilityEvolutionTerms.Storage)),
            Support("L03", "통더미", 1, GridLayer.Building, BuildingCategory.Production, VisualForm.Barrels, Traits(FacilityEvolutionTerms.Storage)),
            Support("L04", "자루더미", 1, GridLayer.Building, BuildingCategory.Production, VisualForm.Sacks, Traits(FacilityEvolutionTerms.Storage)),
            Support("L05", "식재료저장함", 1, GridLayer.Building, BuildingCategory.Production, VisualForm.Crates, Traits(FacilityEvolutionTerms.Logistics, "Cooking"), phase: 2),
            Support("L06", "무기로커", 1, GridLayer.Building, BuildingCategory.Production, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Logistics, FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Security), phase: 2),
            Support("L07", "마력보관함", 1, GridLayer.Building, BuildingCategory.Production, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Logistics, FacilityEvolutionTerms.Mana), phase: 2),
            Support("L08", "보호장비보관함", 1, GridLayer.Building, BuildingCategory.Production, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Logistics, FacilityEvolutionTerms.Storage, FacilityEvolutionTerms.Security), phase: 2),

            Core("H01", "변기", 1, GridLayer.Building, BuildingCategory.Special, FacilityRole.Toilet, FacilityWorkType.Clean | FacilityWorkType.Repair, VisualForm.Toilet, Traits("Sanitation")),
            Support("H02", "화장실칸막이", 1, GridLayer.Building, BuildingCategory.Special, VisualForm.Partition, Traits("Toilet", FacilityEvolutionTerms.Luxury)),
            Core("H03", "세면대", 1, GridLayer.Building, BuildingCategory.Special, FacilityRole.Hygiene, FacilityWorkType.Clean | FacilityWorkType.Repair, VisualForm.Sink, Traits(FacilityEvolutionTerms.Hygiene)),
            Core("H04", "목욕통", 2, GridLayer.Building, BuildingCategory.Special, FacilityRole.Hygiene | FacilityRole.Rest, FacilityWorkType.Clean | FacilityWorkType.Repair, VisualForm.Bath, Traits(FacilityEvolutionTerms.Hygiene, FacilityEvolutionTerms.Rest), phase: 2),
            Support("H05", "수건걸이", 1, GridLayer.WallFixture, BuildingCategory.Special, VisualForm.WallRack, Traits(FacilityEvolutionTerms.Hygiene, FacilityEvolutionTerms.Service)),
            Support("H06", "청소도구함", 1, GridLayer.Building, BuildingCategory.Special, VisualForm.Cabinet, Traits(FacilityEvolutionTerms.Hygiene, FacilityEvolutionTerms.Storage)),
            Support("H07", "바닥배수구", 1, GridLayer.FloorOverlay, BuildingCategory.Special, VisualForm.Drain, Traits(FacilityEvolutionTerms.Hygiene), phase: 2),

            Support("E01", "벽횃불", 1, GridLayer.WallFixture, BuildingCategory.Resource, VisualForm.Torch, Traits("Light")),
            Support("E02", "바닥화로", 1, GridLayer.Building, BuildingCategory.Resource, VisualForm.Hearth, Traits("Light", FacilityEvolutionTerms.Rest), phase: 2),
            Support("E03", "샹들리에", 2, GridLayer.CeilingFixture, BuildingCategory.Resource, VisualForm.Chandelier, Traits("Light", FacilityEvolutionTerms.Luxury), phase: 2),
            Support("E04", "소형러그", 2, GridLayer.FloorOverlay, BuildingCategory.Resource, VisualForm.Rug, Traits(FacilityEvolutionTerms.Luxury, FacilityEvolutionTerms.Quiet)),
            Support("E05", "세력깃발", 1, GridLayer.WallFixture, BuildingCategory.Resource, VisualForm.Flag, Traits(FacilityEvolutionTerms.Combat)),
            Support("E06", "액자초상화", 1, GridLayer.WallFixture, BuildingCategory.Resource, VisualForm.Portrait, Traits(FacilityEvolutionTerms.Luxury, FacilityEvolutionTerms.Noble), phase: 2),
            Support("E07", "촛대", 1, GridLayer.Building, BuildingCategory.Resource, VisualForm.Candlestick, Traits(FacilityEvolutionTerms.Quiet, FacilityEvolutionTerms.Luxury)),
            Support("E08", "해골피장식", 1, GridLayer.WallFixture, BuildingCategory.Resource, VisualForm.Skull, Traits(FacilityEvolutionTerms.Brutal, FacilityEvolutionTerms.Fear), phase: 3),
            Support("E09", "방표지판", 1, GridLayer.WallFixture, BuildingCategory.Resource, VisualForm.Sign, Array.Empty<string>(), contributesToRoom: false, phase: 2),
            Core("E10", "냉각기", 2, GridLayer.Building, BuildingCategory.Resource, FacilityRole.Logistics, FacilityWorkType.Repair, VisualForm.Workbench, Traits(FacilityEvolutionTerms.Logistics, "Cooling"), phase: 2),
            Core("E11", "공조기", 2, GridLayer.Building, BuildingCategory.Resource, FacilityRole.None, FacilityWorkType.Repair, VisualForm.Workbench, Traits(FacilityEvolutionTerms.Hygiene, "Climate"), phase: 3),
            Support("E12", "환기덕트", 1, GridLayer.WallFixture, BuildingCategory.Resource, VisualForm.WallRack, Traits("Ventilation"), contributesToRoom: false, phase: 2),
            Support("E13", "송풍구", 1, GridLayer.WallFixture, BuildingCategory.Resource, VisualForm.WallRack, Traits(FacilityEvolutionTerms.Hygiene, "Ventilation"), phase: 2),
            Support("E14", "배기팬", 1, GridLayer.WallFixture, BuildingCategory.Resource, VisualForm.WallRack, Traits(FacilityEvolutionTerms.Hygiene, "Ventilation"), phase: 2),

            Core("P01", "제분소", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Grain"), phase: 1),
            Core("P02", "양조장", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Barrels, Traits("Production", "Fermentation"), phase: 2),
            Core("P03", "제재소", 3, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Wood"), phase: 1),
            Core("P04", "숯가마", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Hearth, Traits("Production", "Fuel"), phase: 2),
            Core("P05", "석재 절단대", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Stone"), phase: 1),
            Core("P06", "광석 선별대", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Table, Traits("Production", "Ore"), phase: 2),
            Core("P07", "용광로", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Hearth, Traits("Production", "Metal"), phase: 2),
            Core("P08", "제강로", 3, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Hearth, Traits("Production", "Steel"), phase: 2),
            Core("P09", "귀금 세공대", 2, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", FacilityEvolutionTerms.Luxury), phase: 3),
            Core("P10", "비전 단조대", 3, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.Mana, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Rune, Traits("Production", FacilityEvolutionTerms.Mana), phase: 3),
            Core("P11", "직조기", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Textile"), phase: 1),
            Core("P12", "무두질대", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Leather"), phase: 1),
            Core("P13", "퇴비장", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Crates, Traits("Production", FacilityEvolutionTerms.Hygiene), phase: 1),
            Core("P14", "증류기", 2, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Rune, Traits("Production", "Distillation"), phase: 2),
            Core("P15", "조리대", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Cook | FacilityWorkType.Craft | FacilityWorkType.Clean | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Cooking"), phase: 1),
            Core("P16", "훈연대", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Cook | FacilityWorkType.Repair, VisualForm.Hearth, Traits("Production", "Preservation"), phase: 2),
            Core("P17", "사료 배합대", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Feed"), phase: 1),
            Core("P18", "약제대", 2, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Cabinet, Traits("Production", "Medicine"), phase: 2),
            Core("P19", "연금대", 2, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.Research | FacilityRole.Mana, FacilityWorkType.Craft | FacilityWorkType.Research | FacilityWorkType.Repair, VisualForm.Rune, Traits("Production", FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Mana), phase: 2),
            Core("P20", "몽직기", 2, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.Mana, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Rune, Traits("Production", "Textile", FacilityEvolutionTerms.Mana), phase: 3),
            Core("P21", "대장간", 3, GridLayer.Building, BuildingCategory.Crafting, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", FacilityEvolutionTerms.Combat), phase: 1),
            Core("P22", "심부 채석장", 3, GridLayer.Building, BuildingCategory.Resource, FacilityRole.None, FacilityWorkType.Quarry | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Mining"), phase: 2),
            Core("P23", "야외 경작지", 3, GridLayer.FloorOverlay, BuildingCategory.Resource, FacilityRole.None, FacilityWorkType.Sow | FacilityWorkType.Harvest | FacilityWorkType.Repair, VisualForm.Mat, Traits("Production", "Agriculture"), phase: 1),
            Core("P24", "실내 재배조", 3, GridLayer.Building, BuildingCategory.Resource, FacilityRole.None, FacilityWorkType.Sow | FacilityWorkType.Harvest | FacilityWorkType.Repair, VisualForm.Workbench, Traits("Production", "Agriculture"), phase: 2),
            Core("P25", "폐기 소각로", 2, GridLayer.Building, BuildingCategory.Production, FacilityRole.None, FacilityWorkType.Craft | FacilityWorkType.Clean | FacilityWorkType.Repair, VisualForm.Hearth, Traits("Production", "Waste", FacilityEvolutionTerms.Hygiene), phase: 1)
        };
    }
    private static FacilityPartSpec Core(
        string code,
        string displayName,
        int width,
        GridLayer layer,
        BuildingCategory category,
        FacilityRole roles,
        FacilityWorkType workTypes,
        VisualForm form,
        string[] traits,
        FacilityEvolutionValue[] metrics = null,
        int capacity = 1,
        int internalStockCapacity = 0,
        int phase = 1,
        Type runtimeType = null)
    {
        return new FacilityPartSpec(
            code,
            displayName,
            width,
            layer,
            category,
            true,
            roles,
            workTypes,
            runtimeType ?? typeof(Facility),
            form,
            traits,
            metrics,
            capacity,
            internalStockCapacity,
            phase,
            true);
    }

    private static FacilityPartSpec Support(
        string code,
        string displayName,
        int width,
        GridLayer layer,
        BuildingCategory category,
        VisualForm form,
        string[] traits,
        FacilityEvolutionValue[] metrics = null,
        int phase = 1,
        bool contributesToRoom = true)
    {
        return new FacilityPartSpec(
            code,
            displayName,
            width,
            layer,
            category,
            false,
            FacilityRole.None,
            FacilityWorkType.None,
            typeof(Facility),
            form,
            traits,
            metrics,
            0,
            0,
            phase,
            contributesToRoom);
    }

    private static string[] Traits(params string[] values)
    {
        return values ?? Array.Empty<string>();
    }

    private static FacilityEvolutionValue[] Metrics(params (string key, float value)[] values)
    {
        return values?.Select((value) => new FacilityEvolutionValue(value.key, value.value)).ToArray()
            ?? Array.Empty<FacilityEvolutionValue>();
    }

    private static void DrawProp(PixelCanvas canvas, FacilityPartSpec spec)
    {
        Palette palette = Palette.ForCode(spec.Code);
        int variant = spec.Code[1] * 31 + spec.Code[2];
        int left = 2;
        int right = canvas.Width - 3;
        int center = canvas.Width / 2;

        if (spec.Layer == GridLayer.FloorOverlay)
        {
            canvas.FillRect(left, 3, right - left + 1, 8, palette.Dark);
            canvas.FillRect(left + 1, 4, right - left - 1, 6, palette.Accent);
        }

        switch (spec.Form)
        {
            case VisualForm.Hearth:
                DrawCabinet(canvas, left + 2, 5, right - left - 3, 16, palette);
                canvas.FillRect(center - 4, 8, 9, 8, palette.Dark);
                DrawFlame(canvas, center, 13, palette);
                break;
            case VisualForm.Grill:
                DrawCabinet(canvas, left + 1, 5, right - left - 1, 14, palette);
                canvas.FillRect(left + 3, 20, right - left - 5, 3, palette.Metal);
                canvas.Line(left + 5, 23, right - 5, 23, palette.Light);
                for (int x = left + 7; x < right - 4; x += 7) DrawFlame(canvas, x, 16, palette);
                break;
            case VisualForm.Counter:
                DrawCabinet(canvas, left + 1, 5, right - left - 1, 22, palette);
                canvas.FillRect(left, 27, right - left + 1, 4, palette.Light);
                canvas.FillRect(center - 2, 15, 5, 4, palette.Accent);
                break;
            case VisualForm.Table:
                canvas.FillRect(left + 1, 20, right - left - 1, 6, palette.Light);
                canvas.FillRect(left + 3, 17, right - left - 5, 3, palette.Wood);
                canvas.FillRect(left + 5, 6, 4, 14, palette.Dark);
                canvas.FillRect(right - 8, 6, 4, 14, palette.Dark);
                break;
            case VisualForm.Chair:
                canvas.FillRect(center - 7, 21, 14, 5, palette.Accent);
                canvas.FillRect(center - 7, 8, 4, 16, palette.Dark);
                canvas.FillRect(center + 3, 8, 4, 16, palette.Dark);
                canvas.OutlineRect(center - 8, 25, 16, 14, palette.Dark, palette.Light);
                break;
            case VisualForm.Bench:
                canvas.FillRect(left + 2, 19, right - left - 3, 6, palette.Light);
                canvas.FillRect(left + 5, 7, 4, 15, palette.Dark);
                canvas.FillRect(right - 8, 7, 4, 15, palette.Dark);
                canvas.FillRect(left + 3, 27, right - left - 5, 8, palette.Accent);
                break;
            case VisualForm.Shelf:
                DrawShelf(canvas, left + 2, 5, right - left - 3, 36, palette, variant);
                break;
            case VisualForm.WallRack:
                canvas.FillRect(left + 3, 34, right - left - 5, 4, palette.Wood);
                for (int x = left + 6; x < right - 3; x += 6)
                {
                    canvas.Line(x, 33, x - 2 + (variant % 3), 13, palette.Metal);
                    canvas.FillRect(x - 2, 10, 5, 5, palette.Accent);
                }
                break;
            case VisualForm.Cabinet:
                DrawCabinet(canvas, left + 3, 5, right - left - 5, 36, palette);
                canvas.Line(center, 7, center, 38, palette.Dark);
                canvas.FillRect(center - 3, 22, 2, 2, palette.Light);
                canvas.FillRect(center + 2, 22, 2, 2, palette.Light);
                break;
            case VisualForm.Workbench:
                canvas.FillRect(left + 1, 22, right - left - 1, 6, palette.Light);
                DrawCabinet(canvas, left + 4, 5, right - left - 7, 18, palette);
                canvas.Line(left + 6, 31, center - 2, 38, palette.Metal);
                canvas.Circle(right - 9, 33, 5, palette.Accent);
                break;
            case VisualForm.ArmorStand:
                canvas.FillRect(center - 2, 7, 5, 31, palette.Wood);
                canvas.FillRect(center - 8, 7, 17, 4, palette.Dark);
                canvas.OutlineRect(center - 8, 23, 17, 14, palette.Dark, palette.Metal);
                canvas.FillRect(center - 5, 36, 11, 5, palette.Accent);
                break;
            case VisualForm.Bed:
                canvas.FillRect(left + 1, 6, right - left - 1, 8, palette.Dark);
                canvas.FillRect(left + 2, 14, right - left - 3, 14, palette.Accent);
                canvas.FillRect(left + 4, 23, 12, 6, palette.Light);
                canvas.FillRect(left + 1, 5, 4, 25, palette.Wood);
                canvas.FillRect(right - 4, 5, 4, 12, palette.Wood);
                break;
            case VisualForm.BunkBed:
                DrawBedTier(canvas, left + 2, right - 2, 7, palette);
                DrawBedTier(canvas, left + 2, right - 2, 25, palette);
                canvas.FillRect(left, 4, 4, 39, palette.Wood);
                canvas.FillRect(right - 3, 4, 4, 39, palette.Wood);
                break;
            case VisualForm.Sofa:
                canvas.OutlineRect(left + 2, 10, right - left - 3, 18, palette.Dark, palette.Accent);
                canvas.FillRect(left + 4, 23, right - left - 7, 12, palette.Light);
                canvas.FillRect(left, 10, 6, 20, palette.Dark);
                canvas.FillRect(right - 5, 10, 6, 20, palette.Dark);
                break;
            case VisualForm.Desk:
                canvas.FillRect(left + 1, 21, right - left - 1, 6, palette.Light);
                DrawCabinet(canvas, left + 4, 5, 14, 18, palette);
                DrawCabinet(canvas, right - 17, 5, 14, 18, palette);
                canvas.FillRect(center - 6, 29, 12, 7, palette.Accent);
                break;
            case VisualForm.Rune:
                canvas.OutlineRect(left + 3, 5, right - left - 5, 31, palette.Dark, palette.Metal);
                canvas.Circle(center, 23, Mathf.Min(10, canvas.Width / 4), palette.Accent);
                canvas.Circle(center, 23, Mathf.Min(5, canvas.Width / 7), palette.Light);
                canvas.Line(center, 34, center, 42, palette.Light);
                break;
            case VisualForm.Dummy:
                canvas.FillRect(center - 2, 5, 5, 34, palette.Wood);
                canvas.FillRect(center - 9, 29, 19, 5, palette.Wood);
                canvas.Circle(center, 38, 6, palette.Accent);
                canvas.FillRect(center - 8, 5, 17, 4, palette.Dark);
                break;
            case VisualForm.Target:
                canvas.FillRect(center - 2, 5, 5, 28, palette.Wood);
                canvas.Circle(center, 31, 11, palette.Light);
                canvas.Circle(center, 31, 7, palette.Accent);
                canvas.Circle(center, 31, 3, palette.Dark);
                canvas.FillRect(center - 9, 5, 19, 4, palette.Dark);
                break;
            case VisualForm.Weight:
                canvas.FillRect(center - 2, 7, 5, 25, palette.Metal);
                canvas.FillRect(center - 10, 26, 21, 5, palette.Light);
                canvas.FillRect(center - 11, 18, 5, 18, palette.Dark);
                canvas.FillRect(center + 7, 18, 5, 18, palette.Dark);
                canvas.FillRect(center - 10, 5, 21, 4, palette.Dark);
                break;
            case VisualForm.Mat:
            case VisualForm.Rug:
                canvas.OutlineRect(left + 1, 4, right - left - 1, 12, palette.Dark, palette.Accent);
                for (int x = left + 5; x < right - 3; x += 7) canvas.FillRect(x, 8, 3, 3, palette.Light);
                break;
            case VisualForm.Bell:
                canvas.FillRect(center - 2, 28, 5, 13, palette.Wood);
                canvas.FillRect(center - 9, 38, 19, 4, palette.Wood);
                canvas.Circle(center, 25, 10, palette.Light);
                canvas.FillRect(center - 11, 18, 23, 5, palette.Accent);
                canvas.FillRect(center - 2, 14, 5, 6, palette.Dark);
                break;
            case VisualForm.Board:
            case VisualForm.Portrait:
                canvas.OutlineRect(left + 3, 15, right - left - 5, 25, palette.Dark, palette.Wood);
                canvas.FillRect(left + 6, 18, right - left - 11, 19, palette.Accent);
                canvas.Line(left + 8, 20, right - 8, 34, palette.Light);
                canvas.Line(right - 8, 20, left + 8, 34, palette.Light);
                break;
            case VisualForm.MapTable:
                canvas.FillRect(left + 1, 18, right - left - 1, 8, palette.Wood);
                canvas.FillRect(left + 4, 22, right - left - 7, 6, palette.Accent);
                canvas.Line(left + 8, 23, center, 27, palette.Light);
                canvas.Line(center, 27, right - 8, 23, palette.Light);
                canvas.FillRect(left + 6, 5, 4, 15, palette.Dark);
                canvas.FillRect(right - 9, 5, 4, 15, palette.Dark);
                break;
            case VisualForm.Flag:
                canvas.FillRect(left + 4, 9, 3, 34, palette.Metal);
                canvas.FillRect(left + 7, 23, right - left - 9, 18, palette.Accent);
                canvas.Triangle(right - 2, 23, right - 2, 41, right - 10, 32, palette.Dark);
                canvas.FillRect(center - 2, 29, 5, 5, palette.Light);
                break;
            case VisualForm.Crates:
                DrawCrate(canvas, left + 2, 5, Mathf.Min(18, canvas.Width - 5), 18, palette);
                if (canvas.Width > 30) DrawCrate(canvas, center, 8, Mathf.Min(20, right - center), 20, palette);
                DrawCrate(canvas, center - 8, 23, 18, 17, palette);
                break;
            case VisualForm.Barrels:
                DrawBarrel(canvas, center - (canvas.Width > 30 ? 12 : 0), 5, palette);
                if (canvas.Width > 30) DrawBarrel(canvas, center + 12, 5, palette);
                break;
            case VisualForm.Sacks:
                canvas.Circle(center - 5, 15, 10, palette.Wood);
                canvas.Circle(center + 6, 13, 9, palette.Accent);
                canvas.FillRect(center - 8, 23, 7, 5, palette.Dark);
                canvas.FillRect(center + 3, 21, 6, 5, palette.Dark);
                break;
            case VisualForm.Toilet:
                canvas.OutlineRect(center - 7, 7, 15, 13, palette.Dark, palette.Light);
                canvas.OutlineRect(center - 9, 19, 19, 8, palette.Dark, palette.Metal);
                canvas.FillRect(center - 7, 27, 15, 12, palette.Light);
                canvas.FillRect(center - 9, 37, 19, 4, palette.Dark);
                break;
            case VisualForm.Partition:
                canvas.OutlineRect(center - 9, 5, 19, 37, palette.Dark, palette.Accent);
                canvas.FillRect(center - 1, 8, 3, 31, palette.Light);
                canvas.FillRect(center + 4, 22, 3, 3, palette.Dark);
                break;
            case VisualForm.Sink:
                canvas.FillRect(center - 2, 5, 5, 20, palette.Metal);
                canvas.OutlineRect(center - 10, 22, 21, 9, palette.Dark, palette.Light);
                canvas.Line(center, 31, center, 39, palette.Metal);
                canvas.Line(center, 39, center + 6, 39, palette.Metal);
                canvas.FillRect(center + 4, 35, 3, 5, palette.Accent);
                break;
            case VisualForm.Bath:
                canvas.OutlineRect(left + 1, 7, right - left - 1, 18, palette.Dark, palette.Wood);
                canvas.FillRect(left + 4, 12, right - left - 7, 10, palette.Accent);
                canvas.Line(right - 8, 25, right - 8, 36, palette.Metal);
                canvas.Line(right - 8, 36, right - 3, 36, palette.Metal);
                break;
            case VisualForm.Drain:
                canvas.OutlineRect(center - 8, 4, 17, 10, palette.Dark, palette.Metal);
                for (int x = center - 5; x <= center + 5; x += 5) canvas.Line(x, 6, x, 11, palette.Dark);
                break;
            case VisualForm.Torch:
                canvas.Line(center, 13, center, 33, palette.Wood);
                canvas.Line(center - 5, 18, center + 5, 28, palette.Metal);
                DrawFlame(canvas, center, 37, palette);
                break;
            case VisualForm.Chandelier:
                canvas.Line(center, 45, center, 31, palette.Metal);
                canvas.Line(left + 7, 25, right - 7, 25, palette.Metal);
                canvas.Line(left + 7, 25, center, 31, palette.Metal);
                canvas.Line(right - 7, 25, center, 31, palette.Metal);
                DrawFlame(canvas, left + 7, 29, palette);
                DrawFlame(canvas, right - 7, 29, palette);
                DrawFlame(canvas, center, 34, palette);
                break;
            case VisualForm.Candlestick:
                canvas.FillRect(center - 7, 5, 15, 4, palette.Metal);
                canvas.FillRect(center - 2, 8, 5, 24, palette.Light);
                DrawFlame(canvas, center, 37, palette);
                break;
            case VisualForm.Skull:
                canvas.Circle(center, 29, 10, palette.Light);
                canvas.FillRect(center - 7, 17, 15, 10, palette.Light);
                canvas.FillRect(center - 6, 29, 4, 5, palette.Dark);
                canvas.FillRect(center + 3, 29, 4, 5, palette.Dark);
                canvas.FillRect(center - 1, 24, 3, 4, palette.Dark);
                canvas.Line(left + 4, 12, right - 4, 39, palette.Accent);
                break;
            case VisualForm.Sign:
                canvas.FillRect(center - 2, 9, 5, 30, palette.Wood);
                canvas.OutlineRect(left + 3, 23, right - left - 5, 17, palette.Dark, palette.Light);
                canvas.Triangle(right - 5, 26, right - 5, 36, right, 31, palette.Accent);
                break;
        }

        if (spec.Layer == GridLayer.Building)
        {
            canvas.FillRect(left, 3, right - left + 1, 2, palette.Shadow);
        }
        canvas.Set(2 + variant % Mathf.Max(1, canvas.Width - 4), 44, palette.Accent);
    }

    private static void DrawShelf(PixelCanvas canvas, int x, int y, int width, int height, Palette palette, int variant)
    {
        canvas.OutlineRect(x, y, width, height, palette.Dark, palette.Wood);
        for (int shelfY = y + 10; shelfY < y + height - 2; shelfY += 10)
        {
            canvas.FillRect(x + 2, shelfY, width - 4, 3, palette.Dark);
            for (int itemX = x + 4; itemX < x + width - 4; itemX += 5)
            {
                Color32 color = ((itemX + shelfY + variant) & 1) == 0 ? palette.Accent : palette.Light;
                canvas.FillRect(itemX, shelfY + 3, 3, 5 + ((itemX + variant) % 4), color);
            }
        }
    }

    private static void DrawCabinet(PixelCanvas canvas, int x, int y, int width, int height, Palette palette)
    {
        canvas.OutlineRect(x, y, width, height, palette.Dark, palette.Wood);
        canvas.FillRect(x + 2, y + height - 5, width - 4, 3, palette.Light);
        canvas.FillRect(x + width / 2 - 1, y + height / 2, 3, 3, palette.Accent);
    }

    private static void DrawFlame(PixelCanvas canvas, int x, int y, Palette palette)
    {
        canvas.Triangle(x, y + 7, x - 5, y - 2, x + 5, y - 2, palette.Accent);
        canvas.Triangle(x, y + 4, x - 2, y - 2, x + 3, y - 2, palette.Light);
    }

    private static void DrawBedTier(PixelCanvas canvas, int left, int right, int y, Palette palette)
    {
        canvas.FillRect(left, y, right - left + 1, 5, palette.Dark);
        canvas.FillRect(left + 1, y + 5, right - left - 1, 9, palette.Accent);
        canvas.FillRect(left + 3, y + 9, 9, 5, palette.Light);
    }

    private static void DrawCrate(PixelCanvas canvas, int x, int y, int width, int height, Palette palette)
    {
        canvas.OutlineRect(x, y, width, height, palette.Dark, palette.Wood);
        canvas.Line(x + 2, y + 2, x + width - 3, y + height - 3, palette.Light);
        canvas.Line(x + width - 3, y + 2, x + 2, y + height - 3, palette.Light);
    }

    private static void DrawBarrel(PixelCanvas canvas, int center, int y, Palette palette)
    {
        canvas.OutlineRect(center - 8, y + 3, 17, 29, palette.Dark, palette.Wood);
        canvas.FillRect(center - 9, y + 8, 19, 4, palette.Metal);
        canvas.FillRect(center - 9, y + 24, 19, 4, palette.Metal);
        canvas.FillRect(center - 6, y + 4, 13, 3, palette.Light);
        canvas.FillRect(center - 6, y + 29, 13, 3, palette.Light);
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
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

    private sealed class FacilityPartSpec
    {
        public FacilityPartSpec(
            string code,
            string displayName,
            int width,
            GridLayer layer,
            BuildingCategory category,
            bool core,
            FacilityRole roles,
            FacilityWorkType workTypes,
            Type runtimeType,
            VisualForm form,
            string[] traits,
            FacilityEvolutionValue[] metrics,
            int capacity,
            int internalStockCapacity,
            int phase,
            bool contributesToRoom)
        {
            Code = code;
            DisplayName = displayName;
            AssetName = displayName.Replace(" ", string.Empty);
            Width = width;
            Layer = layer;
            Category = category;
            Core = core;
            Roles = roles;
            WorkTypes = workTypes;
            RuntimeType = runtimeType;
            Form = form;
            Traits = traits;
            Metrics = metrics;
            Capacity = capacity;
            InternalStockCapacity = internalStockCapacity;
            Phase = phase;
            ContributesToRoom = contributesToRoom;
            UseDuration = core ? 1.5f : 0f;
        }

        public string Code { get; }
        public string DisplayName { get; }
        public string AssetName { get; }
        public int Width { get; }
        public GridLayer Layer { get; }
        public BuildingCategory Category { get; }
        public bool Core { get; }
        public FacilityRole Roles { get; }
        internal FacilityWorkType WorkTypes { get; }
        public Type RuntimeType { get; }
        public VisualForm Form { get; }
        public string[] Traits { get; }
        public FacilityEvolutionValue[] Metrics { get; }
        public int Capacity { get; }
        public int InternalStockCapacity { get; }
        public int Phase { get; }
        public bool ContributesToRoom { get; }
        public float UseDuration { get; }
    }

    private enum VisualForm
    {
        Hearth,
        Grill,
        Counter,
        Table,
        Chair,
        Bench,
        Shelf,
        WallRack,
        Cabinet,
        Workbench,
        ArmorStand,
        Bed,
        BunkBed,
        Sofa,
        Desk,
        Rune,
        Dummy,
        Target,
        Weight,
        Mat,
        Bell,
        Board,
        MapTable,
        Flag,
        Crates,
        Barrels,
        Sacks,
        Toilet,
        Partition,
        Sink,
        Bath,
        Drain,
        Torch,
        Chandelier,
        Rug,
        Portrait,
        Candlestick,
        Skull,
        Sign
    }

    private readonly struct Palette
    {
        public Palette(Color32 dark, Color32 shadow, Color32 wood, Color32 metal, Color32 accent, Color32 light)
        {
            Dark = dark;
            Shadow = shadow;
            Wood = wood;
            Metal = metal;
            Accent = accent;
            Light = light;
        }

        public Color32 Dark { get; }
        public Color32 Shadow { get; }
        public Color32 Wood { get; }
        public Color32 Metal { get; }
        public Color32 Accent { get; }
        public Color32 Light { get; }

        public static Palette ForCode(string code)
        {
            Color32 accent = code[0] switch
            {
                'D' => new Color32(190, 84, 48, 255),
                'S' => new Color32(197, 154, 56, 255),
                'R' => new Color32(66, 137, 147, 255),
                'Q' => new Color32(59, 151, 112, 255),
                'M' => new Color32(117, 79, 177, 255),
                'T' => new Color32(170, 66, 58, 255),
                'G' => new Color32(92, 109, 135, 255),
                'L' => new Color32(123, 107, 66, 255),
                'H' => new Color32(63, 157, 171, 255),
                _ => new Color32(186, 136, 73, 255)
            };

            return new Palette(
                new Color32(27, 25, 37, 255),
                new Color32(13, 16, 25, 180),
                new Color32(103, 71, 54, 255),
                new Color32(105, 113, 126, 255),
                accent,
                new Color32(218, 202, 164, 255));
        }
    }

    private sealed class PixelCanvas
    {
        private readonly Color32[] pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
        }

        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels => pixels;

        public void Set(int x, int y, Color32 color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }

            pixels[y * Width + x] = color;
        }

        public void FillRect(int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    Set(px, py, color);
                }
            }
        }

        public void OutlineRect(int x, int y, int width, int height, Color32 outline, Color32 fill)
        {
            FillRect(x, y, width, height, outline);
            FillRect(x + 2, y + 2, Mathf.Max(0, width - 4), Mathf.Max(0, height - 4), fill);
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                Set(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int twice = 2 * error;
                if (twice >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (twice <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        public void Circle(int centerX, int centerY, int radius, Color32 color)
        {
            int radiusSquared = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radiusSquared)
                    {
                        Set(centerX + x, centerY + y, color);
                    }
                }
            }
        }

        public void Triangle(int x0, int y0, int x1, int y1, int x2, int y2, Color32 color)
        {
            int minX = Mathf.Min(x0, Mathf.Min(x1, x2));
            int maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
            int minY = Mathf.Min(y0, Mathf.Min(y1, y2));
            int maxY = Mathf.Max(y0, Mathf.Max(y1, y2));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int a = (x1 - x0) * (y - y0) - (y1 - y0) * (x - x0);
                    int b = (x2 - x1) * (y - y1) - (y2 - y1) * (x - x1);
                    int c = (x0 - x2) * (y - y2) - (y0 - y2) * (x - x2);
                    if ((a >= 0 && b >= 0 && c >= 0) || (a <= 0 && b <= 0 && c <= 0))
                    {
                        Set(x, y, color);
                    }
                }
            }
        }
    }
}
