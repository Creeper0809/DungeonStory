using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class P1DefenseFacilityAssetBuilder
{
    private const string BuildingFolder = "Assets/Resources/SO/Building/P1";
    private const string EffectFolder = "Assets/Resources/SO/Defense/Effects/P1";
    private const string CanonicalTreasuryLauncherPath =
        BuildingFolder + "/P1_TreasuryBoltThrower.asset";
    private const string LegacyTreasuryLauncherPath =
        BuildingFolder + "/P1_TreasuryCrossbow.asset";
    private const string LegacyTreasuryLauncherEffectPath =
        EffectFolder + "/P1_TreasuryCrossbow_1_Damage.asset";
    private const string TacticalSpriteFolder =
        "Assets/Images/Placeholders/Defense/Tactical";
    private static readonly IReadOnlyDictionary<int, string>
        TacticalSpritePathByBuildingId =
            new Dictionary<int, string>
            {
                [30] = TacticalSpriteFolder + "/defense_spike_trap.png",
                [31] = TacticalSpriteFolder + "/defense_poison_pool.png",
                [32] = TacticalSpriteFolder + "/defense_fire_vent.png",
                [33] = TacticalSpriteFolder + "/defense_lightning_pillar.png",
                [34] = TacticalSpriteFolder + "/defense_ice_vent.png",
                [35] = TacticalSpriteFolder + "/defense_guard_room.png",
                [52] = TacticalSpriteFolder + "/defense_venom_spike.png",
                [53] = TacticalSpriteFolder + "/defense_alarm_coil.png",
                [54] = TacticalSpriteFolder + "/defense_barracks.png",
                [57] = TacticalSpriteFolder + "/defense_corrosion_freezer.png",
                [58] = TacticalSpriteFolder + "/defense_storm_fire.png",
                [59] = TacticalSpriteFolder + "/defense_war_barracks.png",
                [1800] = TacticalSpriteFolder + "/defense_corridor_detector.png",
                [1801] = TacticalSpriteFolder + "/defense_control_desk.png",
                [1802] = TacticalSpriteFolder + "/defense_supply_depot.png",
                [1803] = TacticalSpriteFolder + "/defense_maintenance_bench.png",
                [1804] = TacticalSpriteFolder + "/defense_linked_drop_gate.png",
                [1805] = TacticalSpriteFolder + "/defense_wall_launcher.png",
                [9961] = TacticalSpriteFolder + "/defense_treasury_ballista.png"
            };

    [MenuItem("DungeonStory/Debug/Defense/Ensure P1 Defense Assets")]
    public static void EnsureP1DefenseAssetsFromMenu()
    {
        EnsureP1DefenseAssets();
    }

    public static void EnsureP1DefenseAssets()
    {
        AssetDatabase.Refresh();
        EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_spike.png");
        EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_poison.png");
        EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_fire.png");
        EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_lightning.png");
        EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_ice.png");
        EnsureSpriteImport("Assets/Images/Placeholders/Defense/defense_guard_room.png");
        EnsureSpriteImport("Assets/Images/Placeholders/Items/item_weapon.png");
        foreach (string spritePath
                 in TacticalSpritePathByBuildingId.Values)
        {
            EnsureSpriteImport(spritePath, 64f);
        }

        System.IO.Directory.CreateDirectory(BuildingFolder);
        System.IO.Directory.CreateDirectory(EffectFolder);
        RemoveLegacyTreasuryLauncherDuplicate();
        foreach (DefenseAssetSpec spec in CreateSpecs())
        {
            EnsureAsset(spec);
        }

        EnhanceAllDefenseAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void RemoveLegacyTreasuryLauncherDuplicate()
    {
        BuildingSO canonical = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            CanonicalTreasuryLauncherPath);
        BuildingSO legacy = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            LegacyTreasuryLauncherPath);
        if (canonical == null
            || canonical.id != 9961
            || legacy == null
            || legacy.id != 36)
        {
            return;
        }

        AssetDatabase.DeleteAsset(LegacyTreasuryLauncherPath);
        AssetDatabase.DeleteAsset(LegacyTreasuryLauncherEffectPath);
    }

    private static void EnsureAsset(DefenseAssetSpec spec)
    {
        string assetPath = $"{BuildingFolder}/{spec.assetName}.asset";
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingSO>();
            AssetDatabase.CreateAsset(building, assetPath);
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spec.spritePath);
        building.id = spec.id;
        building.objectName = spec.displayName;
        building.sprite = sprite;
        building.icon = sprite;
        building.width = spec.width;
        building.height = 1;
        building.layer = spec.layer;
        building.category = spec.id == 1804
            ? BuildingCategory.Wall
            : BuildingCategory.Special;
        building.horizontalDraggable = false;
        building.verticalDraggable = false;
        building.runtimeArchetype = BuildingRuntimeArchetypeKind.DefenseFacility;
        building.tiles = null;
        building.movementAnchorOffset = Vector2.zero;
        BuildingEconomyAbility economy = building.GetAbility<BuildingEconomyAbility>();
        if (economy == null)
        {
            economy = new BuildingEconomyAbility();
            building.AbilityModules.Add(economy);
        }

        economy.constructionValue = spec.constructionCost;
        economy.constructionCost = spec.constructionCost;
        economy.maintenance = spec.maintenance;
        economy.unlockPhase = 1;
        economy.demolitionRefundRate = 0.5f;
        FacilityData facility = new FacilityData
        {
            roles = FacilityRole.None,
            capacity = 0,
            useDuration = 0f,
            requiredWorkers = spec.requiredWorkers,
            disabledWhenDamaged = true
        };
        facility.SetSupportedWorkTypeIds(ToWorkTypeIds(spec.workTypes));
        building.Facility = facility;
        DefenseEffectSO[] effectAssets = DefenseEffectAssetBuilder.EnsureEffects(
            $"{EffectFolder}/{spec.assetName}",
            spec.effectSpecs);
        building.Defense = new DefenseFacilityData
        {
            enabled = true,
            concept = spec.concept,
            triggerTimings = spec.trigger,
            targetRule = spec.target,
            cooldownSeconds = spec.cooldown,
            periodicIntervalSeconds = spec.period,
            range = 0,
            star = 1,
            combatLogText = spec.displayName,
            effectAssets = effectAssets
        };
        if (spec.id >= 1800 && spec.id <= 1805)
        {
            BuildingFacilityPartAbility part =
                building.GetAbility<BuildingFacilityPartAbility>();
            if (part == null)
            {
                part = new BuildingFacilityPartAbility();
                building.AbilityModules.Add(part);
            }

            part.code = $"DF{spec.id - 1799:00}";

            if (spec.id == 1802 || spec.id == 1803)
            {
                BuildingStorageAbility storage =
                    building.GetAbility<BuildingStorageAbility>();
                if (storage == null)
                {
                    storage = new BuildingStorageAbility();
                    building.AbilityModules.Add(storage);
                }

                storage.category = spec.id == 1802
                    ? StockCategory.Ammunition
                    : StockCategory.General;
                storage.capacity = spec.id == 1802 ? 24 : 12;
                storage.allCategories = spec.id == 1802;
            }

            if (spec.id == 1804)
            {
                BuildingStructuralIntegrityAbility structural =
                    building.GetAbility<BuildingStructuralIntegrityAbility>();
                if (structural == null)
                {
                    structural = new BuildingStructuralIntegrityAbility();
                    building.AbilityModules.Add(structural);
                }

                structural.maxHitPoints = 450f;
                structural.toughness = 24f;
                structural.repairHitPointsPerWork = 2f;
                structural.breachable = true;
            }
        }
        if (spec.treasuryPowered)
        {
            BuildingTreasuryPoweredDefenseAbility treasuryAbility =
                building.GetAbility<BuildingTreasuryPoweredDefenseAbility>();
            if (treasuryAbility == null)
            {
                treasuryAbility =
                    new BuildingTreasuryPoweredDefenseAbility();
                building.AbilityModules.Add(treasuryAbility);
            }

            treasuryAbility.shotCost = 30;
            treasuryAbility.defaultInvasionBudget = 300;
            treasuryAbility.defaultMinimumThreat = 0;
            treasuryAbility.defaultBossOnly = false;
            if (building.GetAbility<BuildingOverclockableAbility>() == null)
            {
                building.AbilityModules.Add(
                    new BuildingOverclockableAbility());
            }
        }

        BuildingWorkAmountAbility workAmount =
            building.GetAbility<BuildingWorkAmountAbility>();
        if (workAmount == null)
        {
            workAmount = new BuildingWorkAmountAbility();
            building.AbilityModules.Add(workAmount);
        }

        int constructionCells = Mathf.Max(1, spec.width);
        workAmount.constructionWorkRequired = Mathf.Clamp(
            12f + constructionCells * 6f + spec.constructionCost * 0.02f,
            12f,
            120f);
        workAmount.repairWorkRequired = Mathf.Clamp(
            8f + constructionCells * 2f,
            6f,
            35f);
        workAmount.cleanWorkRequired = Mathf.Clamp(
            5f + constructionCells * 1.25f,
            4f,
            24f);
        workAmount.researchWorkRequired = 6f;
        workAmount.operateWorkRequired = 10f;
        workAmount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition(
                "material:steel-ingot",
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(spec.constructionCost * 0.05f)))
        });

        building.unlocked = true;
        building.AbilityModules.EnsureStableIds();
        building.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(building);
    }

    private static void EnhanceAllDefenseAssets()
    {
        BuildingSO[] defenses = AssetDatabase
            .FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building =>
                building != null
                && building.Defense != null
                && building.Defense.IsDefenseFacility)
            .OrderBy(building => building.id)
            .ToArray();

        foreach (BuildingSO building in defenses)
        {
            if (TacticalSpritePathByBuildingId.TryGetValue(
                    building.id,
                    out string spritePath))
            {
                Sprite sprite =
                    AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                {
                    building.sprite = sprite;
                    building.icon = sprite;
                }
            }

            ConfigureOperationalContract(building);
            building.AbilityModules.EnsureStableIds();
            building.ValidateAbilitiesOrThrow();
            EditorUtility.SetDirty(building);
        }
    }

    private static void ConfigureOperationalContract(BuildingSO building)
    {
        DefenseFacilityData defense = building.Defense;
        defense.initialSupply = 1;
        defense.supplyPerActivation = 1;
        defense.conditionLossPerActivation = 1f;
        defense.baseJamChance = 0.01f;
        defense.baseMisfireChance = 0.005f;
        defense.growth ??= new DefenseFacilityGrowthData();

        if (building.GetAbility<BuildingTreasuryPoweredDefenseAbility>() != null)
        {
            SetSupply(
                defense,
                DefenseSupplyKind.Treasury,
                string.Empty,
                StockCategory.General,
                0);
            defense.facilityFamilyId = "defense:launcher";
            defense.affinityTags =
                new[] { "defense:ranged", "species:harpy", "species:kobold" };
            return;
        }

        switch (building.id)
        {
            case 30:
                SetSupply(
                    defense,
                    DefenseSupplyKind.Ammunition,
                    "ammo:trap-canister",
                    StockCategory.Ammunition,
                    6);
                defense.facilityFamilyId = "defense:scatter-trap";
                defense.affinityTags =
                    new[] { "defense:scatter", "species:kobold", "species:orc" };
                defense.conditionLossPerActivation = 1f;
                return;
            case 58:
                SetSupply(
                    defense,
                    DefenseSupplyKind.Ammunition,
                    "ammo:blasting-charge",
                    StockCategory.Ammunition,
                    4);
                defense.facilityFamilyId = "defense:blast-trap";
                defense.affinityTags =
                    new[] { "defense:blast", "species:kobold", "species:demon" };
                defense.conditionLossPerActivation = 1.4f;
                defense.baseMisfireChance = 0.02f;
                return;
            case 1800:
                SetSupply(
                    defense,
                    DefenseSupplyKind.None,
                    string.Empty,
                    StockCategory.General,
                    0);
                defense.range = Mathf.Max(5, defense.range);
                defense.facilityFamilyId = "defense:detection";
                defense.affinityTags =
                    new[] { "defense:alarm", "species:beastkin", "species:harpy" };
                defense.conditionLossPerActivation = 0.2f;
                return;
            case 1801:
                SetSupply(
                    defense,
                    DefenseSupplyKind.ElectricalCharge,
                    string.Empty,
                    StockCategory.Mana,
                    0);
                defense.requiresPower = true;
                defense.powerDemand = 2f;
                defense.range = Mathf.Max(6, defense.range);
                defense.facilityFamilyId = "defense:control";
                defense.affinityTags =
                    new[] { "defense:identification", "species:demon", "species:golem" };
                defense.conditionLossPerActivation = 0.1f;
                return;
            case 1802:
                SetSupply(
                    defense,
                    DefenseSupplyKind.None,
                    string.Empty,
                    StockCategory.Ammunition,
                    0);
                defense.facilityFamilyId = "defense:supply";
                defense.affinityTags =
                    new[] { "defense:reload", "species:beastkin", "species:kobold" };
                defense.conditionLossPerActivation = 0f;
                return;
            case 1803:
                SetSupply(
                    defense,
                    DefenseSupplyKind.None,
                    string.Empty,
                    StockCategory.General,
                    0);
                defense.facilityFamilyId = "defense:maintenance";
                defense.affinityTags =
                    new[] { "defense:repair", "species:kobold", "species:golem" };
                defense.conditionLossPerActivation = 0f;
                return;
            case 1804:
                SetSupply(
                    defense,
                    DefenseSupplyKind.MetalParts,
                    "material:iron-ingot",
                    StockCategory.General,
                    3);
                defense.facilityFamilyId = "defense:barrier";
                defense.affinityTags =
                    new[] { "defense:wall", "species:orc", "species:golem" };
                defense.conditionLossPerActivation = 2f;
                defense.baseJamChance = 0.025f;
                return;
            case 1805:
                SetSupply(
                    defense,
                    DefenseSupplyKind.Ammunition,
                    "ammo:bolt-iron",
                    StockCategory.Ammunition,
                    8);
                defense.facilityFamilyId = "defense:launcher";
                defense.affinityTags =
                    new[] { "defense:ranged", "species:harpy", "species:kobold" };
                defense.conditionLossPerActivation = 0.8f;
                return;
        }

        switch (defense.concept)
        {
            case DefenseAttackConcept.Poison:
                SetSupply(
                    defense,
                    DefenseSupplyKind.Toxin,
                    "craft:toxic-trap-coating",
                    StockCategory.Biological,
                    4);
                defense.facilityFamilyId = "defense:toxin";
                defense.affinityTags =
                    new[] { "defense:poison", "species:vampire", "species:myconid" };
                break;
            case DefenseAttackConcept.Fire:
                SetSupply(
                    defense,
                    DefenseSupplyKind.Fuel,
                    "resource:coal",
                    StockCategory.Fuel,
                    4);
                defense.facilityFamilyId = "defense:elemental";
                defense.affinityTags =
                    new[] { "defense:fire", "species:orc", "species:demon" };
                break;
            case DefenseAttackConcept.Lightning:
                SetPoweredElemental(
                    defense,
                    "defense:lightning",
                    "species:beastkin",
                    "species:demon");
                break;
            case DefenseAttackConcept.Ice:
                SetPoweredElemental(
                    defense,
                    "defense:ice",
                    "species:slime",
                    "species:vampire");
                break;
            case DefenseAttackConcept.Guard:
                SetSupply(
                    defense,
                    DefenseSupplyKind.MetalParts,
                    "material:iron-ingot",
                    StockCategory.General,
                    3);
                defense.facilityFamilyId = "defense:guard-post";
                defense.affinityTags =
                    new[] { "defense:blocking", "species:orc", "species:golem" };
                defense.conditionLossPerActivation = 0.5f;
                break;
            default:
                bool launcher = building.id == 36
                    || (building.objectName ?? string.Empty).IndexOf(
                        "crossbow",
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || (building.objectName ?? string.Empty).IndexOf(
                        "bolt",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                SetSupply(
                    defense,
                    launcher
                        ? DefenseSupplyKind.Ammunition
                        : DefenseSupplyKind.MetalParts,
                    launcher ? "ammo:bolt-iron" : "material:iron-ingot",
                    launcher ? StockCategory.Ammunition : StockCategory.General,
                    launcher ? 8 : 3);
                defense.facilityFamilyId =
                    launcher ? "defense:launcher" : "defense:mechanical-trap";
                defense.affinityTags = launcher
                    ? new[] { "defense:ranged", "species:harpy", "species:kobold" }
                    : new[] { "defense:mechanical", "species:kobold", "species:orc" };
                break;
        }
    }

    private static void SetPoweredElemental(
        DefenseFacilityData defense,
        string affinity,
        string firstSpecies,
        string secondSpecies)
    {
        SetSupply(
            defense,
            DefenseSupplyKind.ElectricalCharge,
            string.Empty,
            StockCategory.Mana,
            0);
        defense.requiresPower = true;
        defense.powerDemand = 3f;
        defense.facilityFamilyId = "defense:elemental";
        defense.affinityTags =
            new[] { affinity, firstSpecies, secondSpecies };
    }

    private static void SetSupply(
        DefenseFacilityData defense,
        DefenseSupplyKind kind,
        string itemId,
        StockCategory category,
        int capacity)
    {
        defense.supplyKind = kind;
        defense.supplyItemId = itemId ?? string.Empty;
        defense.supplyCategory = category;
        defense.supplyCapacity = Mathf.Max(0, capacity);
        defense.initialSupply = defense.supplyCapacity > 0 ? 1 : 0;
        defense.supplyPerActivation = defense.supplyCapacity > 0 ? 1 : 0;
        defense.requiresPower = false;
        defense.powerDemand = 0f;
    }

    private static IEnumerable<WorkTypeId> ToWorkTypeIds(FacilityWorkType workTypes)
    {
        if ((workTypes & FacilityWorkType.Guard) != 0) yield return BuiltInWorkTypeIds.Guard;
        if ((workTypes & FacilityWorkType.Repair) != 0) yield return BuiltInWorkTypeIds.Repair;
    }

    private static DefenseAssetSpec[] CreateSpecs()
    {
        return new[]
        {
            new DefenseAssetSpec(
                "P1_TreasuryCrossbow",
                36,
                "금고각인 쇠뇌대",
                "Assets/Images/Placeholders/Items/item_weapon.png",
                2,
                GridLayer.FloorOverlay,
                240,
                0,
                DefenseAttackConcept.Physical,
                DefenseTriggerTiming.OnEnter | DefenseTriggerTiming.Cooldown,
                DefenseTargetRule.EnteringIntruder,
                2.5f,
                0f,
                FacilityWorkType.Repair,
                0,
                new[]
                {
                    Effect<DefenseDamageEffectSO>(
                        34f,
                        0f,
                        1,
                        "고관통")
                },
                treasuryPowered: true),
            new DefenseAssetSpec(
                "P1_SpikeTrap",
                30,
                "1성 산탄 가시 함정",
                "Assets/Images/Placeholders/Defense/defense_spike.png",
                2,
                GridLayer.FloorOverlay,
                80,
                2,
                DefenseAttackConcept.Physical,
                DefenseTriggerTiming.OnEnter,
                DefenseTargetRule.EnteringIntruder,
                0f,
                0f,
                FacilityWorkType.Repair,
                0,
                new[] { Effect<DefenseDamageEffectSO>(14f, 0f, 1, "피해") }),
            new DefenseAssetSpec(
                "P1_PoisonPool",
                31,
                "1성 독 웅덩이",
                "Assets/Images/Placeholders/Defense/defense_poison.png",
                2,
                GridLayer.FloorOverlay,
                110,
                3,
                DefenseAttackConcept.Poison,
                DefenseTriggerTiming.OnEnter | DefenseTriggerTiming.Periodic,
                DefenseTargetRule.EnteringIntruder,
                1f,
                1f,
                FacilityWorkType.Repair,
                0,
                new[]
                {
                    Effect<DefenseDamageEffectSO>(6f, 0f, 1, "피해"),
                    Effect<DefenseCorrosionEffectSO>(0.25f, 8f, 1, "부식")
                }),
            new DefenseAssetSpec(
                "P1_FireVent",
                32,
                "1성 화염 분사구",
                "Assets/Images/Placeholders/Defense/defense_fire.png",
                2,
                GridLayer.FloorOverlay,
                140,
                4,
                DefenseAttackConcept.Fire,
                DefenseTriggerTiming.OnEnter | DefenseTriggerTiming.Cooldown,
                DefenseTargetRule.AllIntrudersInRoom,
                3f,
                0f,
                FacilityWorkType.Repair,
                0,
                new[]
                {
                    Effect<DefenseDamageEffectSO>(18f, 0f, 1, "피해"),
                    Effect<DefenseBurnEffectSO>(2f, 5f, 1, "연소")
                }),
            new DefenseAssetSpec(
                "P1_LightningPillar",
                33,
                "1성 번개 기둥",
                "Assets/Images/Placeholders/Defense/defense_lightning.png",
                2,
                GridLayer.FloorOverlay,
                130,
                4,
                DefenseAttackConcept.Lightning,
                DefenseTriggerTiming.OnEnter | DefenseTriggerTiming.Cooldown,
                DefenseTargetRule.EnteringIntruder,
                2.5f,
                0f,
                FacilityWorkType.Repair,
                0,
                new[]
                {
                    Effect<DefenseDamageEffectSO>(8f, 0f, 1, "피해"),
                    Effect<DefenseChargeEffectSO>(10f, 10f, 1, "축전")
                }),
            new DefenseAssetSpec(
                "P1_IceVent",
                34,
                "1성 냉기 분사구",
                "Assets/Images/Placeholders/Defense/defense_ice.png",
                2,
                GridLayer.FloorOverlay,
                100,
                3,
                DefenseAttackConcept.Ice,
                DefenseTriggerTiming.OnEnter | DefenseTriggerTiming.Periodic,
                DefenseTargetRule.AllIntrudersInRoom,
                1.5f,
                1.5f,
                FacilityWorkType.Repair,
                0,
                new[]
                {
                    Effect<DefenseDamageEffectSO>(5f, 0f, 1, "피해"),
                    Effect<DefenseSlowEffectSO>(0.7f, 4f, 1, "감속")
                }),
            new DefenseAssetSpec(
                "P1_GuardRoom",
                35,
                "1성 경비실",
                "Assets/Images/Placeholders/Defense/defense_guard_room.png",
                3,
                GridLayer.Building,
                180,
                6,
                DefenseAttackConcept.Guard,
                DefenseTriggerTiming.OnEnter | DefenseTriggerTiming.GuardResponse,
                DefenseTargetRule.GuardTarget,
                2f,
                0f,
                FacilityWorkType.Repair | FacilityWorkType.Guard,
                1,
                new[] { Effect<DefenseGuardAttackEffectSO>(10f, 0f, 1, "경비 교전") })
            ,
            new DefenseAssetSpec(
                "DefenseCorridorDetector",
                1800,
                "복도 침입 감지기",
                "Assets/Images/Placeholders/Defense/defense_guard_room.png",
                1,
                GridLayer.WallFixture,
                95,
                2,
                DefenseAttackConcept.Guard,
                DefenseTriggerTiming.OnEnter,
                DefenseTargetRule.EnteringIntruder,
                0.5f,
                0f,
                FacilityWorkType.Repair,
                0,
                Array.Empty<DefenseEffectAssetSpec>()),
            new DefenseAssetSpec(
                "DefenseControlDesk",
                1801,
                "방어 통제대",
                "Assets/Images/Placeholders/Defense/defense_guard_room.png",
                2,
                GridLayer.Building,
                210,
                5,
                DefenseAttackConcept.Guard,
                DefenseTriggerTiming.None,
                DefenseTargetRule.GuardTarget,
                0f,
                0f,
                FacilityWorkType.Repair | FacilityWorkType.Guard,
                1,
                Array.Empty<DefenseEffectAssetSpec>()),
            new DefenseAssetSpec(
                "DefenseSupplyDepot",
                1802,
                "탄약·촉매 보급고",
                "Assets/Images/Placeholders/Items/item_weapon.png",
                2,
                GridLayer.Building,
                170,
                4,
                DefenseAttackConcept.Guard,
                DefenseTriggerTiming.None,
                DefenseTargetRule.GuardTarget,
                0f,
                0f,
                FacilityWorkType.Repair,
                0,
                Array.Empty<DefenseEffectAssetSpec>()),
            new DefenseAssetSpec(
                "DefenseMaintenanceBench",
                1803,
                "함정 정비대",
                "Assets/Images/Placeholders/Defense/defense_spike.png",
                2,
                GridLayer.Building,
                155,
                3,
                DefenseAttackConcept.Guard,
                DefenseTriggerTiming.None,
                DefenseTargetRule.GuardTarget,
                0f,
                0f,
                FacilityWorkType.Repair,
                1,
                Array.Empty<DefenseEffectAssetSpec>()),
            new DefenseAssetSpec(
                "DefenseLinkedDropGate",
                1804,
                "문 연동 강화 낙하문",
                "Assets/Images/Placeholders/Defense/defense_spike.png",
                2,
                GridLayer.Building,
                260,
                7,
                DefenseAttackConcept.Physical,
                DefenseTriggerTiming.OnEnter | DefenseTriggerTiming.Cooldown,
                DefenseTargetRule.EnteringIntruder,
                4f,
                0f,
                FacilityWorkType.Repair,
                0,
                new[]
                {
                    Effect<DefenseDamageEffectSO>(24f, 0f, 1, "낙하문 충격"),
                    Effect<DefenseSlowEffectSO>(0.55f, 3f, 1, "통로 봉쇄")
                }),
            new DefenseAssetSpec(
                "DefenseWallLauncher",
                1805,
                "벽면 발사구",
                "Assets/Images/Placeholders/Items/item_weapon.png",
                1,
                GridLayer.WallFixture,
                225,
                5,
                DefenseAttackConcept.Physical,
                DefenseTriggerTiming.OnEnter | DefenseTriggerTiming.Cooldown,
                DefenseTargetRule.EnteringIntruder,
                2f,
                0f,
                FacilityWorkType.Repair,
                0,
                new[]
                {
                    Effect<DefenseDamageEffectSO>(28f, 0f, 1, "벽면 볼트")
                })
        }
        // The treasury-funded launcher already exists as
        // P1_TreasuryBoltThrower (building 9961). Keep that canonical asset
        // instead of generating the legacy building-36 duplicate.
        .Where(spec => spec.assetName != "P1_TreasuryCrossbow")
        .ToArray();
    }

    private static DefenseEffectAssetSpec Effect<TEffect>(float amount, float duration, int stacks, string logTag)
        where TEffect : DefenseEffectSO
    {
        return DefenseEffectAssetSpec.Create<TEffect>(amount, duration, stacks, logTag);
    }

    private static void EnsureSpriteImport(
        string path,
        float pixelsPerUnit = 16f)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private readonly struct DefenseAssetSpec
    {
        public DefenseAssetSpec(
            string assetName,
            int id,
            string displayName,
            string spritePath,
            int width,
            GridLayer layer,
            int constructionCost,
            int maintenance,
            DefenseAttackConcept concept,
            DefenseTriggerTiming trigger,
            DefenseTargetRule target,
            float cooldown,
            float period,
            FacilityWorkType workTypes,
            int requiredWorkers,
            DefenseEffectAssetSpec[] effectSpecs,
            bool treasuryPowered = false)
        {
            this.assetName = assetName;
            this.id = id;
            this.displayName = displayName;
            this.spritePath = spritePath;
            this.width = width;
            this.layer = layer;
            this.constructionCost = Mathf.Max(1, constructionCost);
            this.maintenance = Mathf.Max(0, maintenance);
            this.concept = concept;
            this.trigger = trigger;
            this.target = target;
            this.cooldown = cooldown;
            this.period = period;
            this.workTypes = workTypes;
            this.requiredWorkers = requiredWorkers;
            this.effectSpecs = effectSpecs ?? Array.Empty<DefenseEffectAssetSpec>();
            this.treasuryPowered = treasuryPowered;
        }

        public readonly string assetName;
        public readonly int id;
        public readonly string displayName;
        public readonly string spritePath;
        public readonly int width;
        public readonly GridLayer layer;
        public readonly int constructionCost;
        public readonly int maintenance;
        public readonly DefenseAttackConcept concept;
        public readonly DefenseTriggerTiming trigger;
        public readonly DefenseTargetRule target;
        public readonly float cooldown;
        public readonly float period;
        internal readonly FacilityWorkType workTypes;
        public readonly int requiredWorkers;
        public readonly DefenseEffectAssetSpec[] effectSpecs;
        public readonly bool treasuryPowered;
    }
}
