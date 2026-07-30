using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class P1DefenseFacilityAssetBuilder
{
    private const string BuildingFolder = "Assets/Resources/SO/Building/P1";
    private const string EffectFolder = "Assets/Resources/SO/Defense/Effects/P1";

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

        System.IO.Directory.CreateDirectory(BuildingFolder);
        System.IO.Directory.CreateDirectory(EffectFolder);
        foreach (DefenseAssetSpec spec in CreateSpecs())
        {
            EnsureAsset(spec);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
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
        building.category = BuildingCategory.Special;
        building.horizontalDraggable = false;
        building.verticalDraggable = false;
        building.type = typeof(DefenseFacility);
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

        building.unlocked = true;
        EditorUtility.SetDirty(building);
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
                "1성 가시 함정",
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
        };
    }

    private static DefenseEffectAssetSpec Effect<TEffect>(float amount, float duration, int stacks, string logTag)
        where TEffect : DefenseEffectSO
    {
        return DefenseEffectAssetSpec.Create<TEffect>(amount, duration, stacks, logTag);
    }

    private static void EnsureSpriteImport(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
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
