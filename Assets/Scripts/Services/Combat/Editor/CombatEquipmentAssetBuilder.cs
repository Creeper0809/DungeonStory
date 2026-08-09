#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentAssetBuilder
{
    private static readonly IReadOnlyList<string> BowAmmunitionItemIds =
        new[]
        {
            CombatItemDefinitions.ArrowItemId,
            "ammo:arrow-bone",
            "ammo:arrow-iron",
            "ammo:arrow-steel",
            "ammo:arrow-rune",
            "ammo:incendiary-arrow"
        };
    private static readonly IReadOnlyList<string> CrossbowAmmunitionItemIds =
        new[]
        {
            CombatItemDefinitions.BoltItemId,
            "ammo:bolt-bone",
            "ammo:bolt-iron",
            "ammo:bolt-steel",
            "ammo:bolt-rune",
            "ammo:incendiary-bolt",
            "ammo:blacksteel-bolt",
            "ammo:mana-disruptor-bolt",
            "ammo:tranquilizer-dart"
        };
    private static readonly IReadOnlyList<string> GunpowderAmmunitionItemIds =
        new[]
        {
            "ammo:paper-cartridge",
            "ammo:smoke-cartridge",
            "ammo:armor-piercing-cartridge",
            "ammo:scatter-cartridge",
            "ammo:rune-cartridge",
            "ammo:signal-flare"
        };

    private const string Root = "Assets/Resources/SO/Combat/Equipment";

    [MenuItem("Tools/DungeonStory/Combat/Build Initial Equipment")]
    public static void BuildAll()
    {
        EnsureFolders();

        BuildWeapon("W01_Dagger", "weapon:dagger", "단검", 0.7f, 1, 1,
            Melee(0.7f, 7f, 4f, CombatDamageType.Slash, 0.14f),
            Profiles((CombatRangeBand.Contact, 1.1f, 0.9f)));
        BuildWeapon("W02_Longsword", "weapon:longsword", "장검", 1.8f, 1, 1,
            Melee(1.05f, 10f, 7f, CombatDamageType.Slash, 0.08f),
            Profiles((CombatRangeBand.Contact, 1f, 1f)));
        BuildWeapon("W03_Spear", "weapon:spear", "창", 2.4f, 2, 1,
            Melee(1.15f, 11f, 9f, CombatDamageType.Pierce, 0.05f),
            Profiles((CombatRangeBand.Contact, 1.05f, 1f)));
        BuildWeapon("W04_Mace", "weapon:mace", "철퇴", 2.8f, 1, 1,
            Melee(1.25f, 12f, 4f, CombatDamageType.Blunt, 0.04f),
            Profiles((CombatRangeBand.Contact, 0.92f, 1.15f)));
        BuildWeapon("W05_Shortbow", "weapon:shortbow", "단궁", 1.4f, 2, 11,
            Projectile(0.9f, 8f, 4f, 15f, 0.06f),
            Profiles(
                (CombatRangeBand.Contact, 0.35f, 0.55f),
                (CombatRangeBand.Near, 1f, 0.95f),
                (CombatRangeBand.Medium, 0.82f, 0.85f)),
            BowAmmunitionItemIds, 1, 0.75f, rapid: true, suppressive: true);
        BuildWeapon("W06_Longbow", "weapon:longbow", "장궁", 2.1f, 2, 18,
            Projectile(1.2f, 10f, 6f, 18f, 0.04f),
            Profiles(
                (CombatRangeBand.Contact, 0.2f, 0.5f),
                (CombatRangeBand.Near, 0.85f, 0.9f),
                (CombatRangeBand.Medium, 1f, 1f),
                (CombatRangeBand.Long, 0.72f, 0.9f)),
            BowAmmunitionItemIds, 1, 1f, suppressive: true);
        BuildWeapon("W07_Crossbow", "weapon:crossbow", "석궁", 3.8f, 2, 18,
            Projectile(1f, 14f, 12f, 20f, 0.03f),
            Profiles(
                (CombatRangeBand.Contact, 0.25f, 0.65f),
                (CombatRangeBand.Near, 1f, 1.05f),
                (CombatRangeBand.Medium, 1.05f, 1.05f),
                (CombatRangeBand.Long, 0.85f, 0.95f)),
            CrossbowAmmunitionItemIds, 1, 2.2f);
        BuildWeapon("W08_Javelin", "weapon:javelin", "투창", 2f, 1, 11,
            Throw(1.1f, 12f, 8f, 11f, 0.05f),
            Profiles(
                (CombatRangeBand.Contact, 0.75f, 0.75f),
                (CombatRangeBand.Near, 1f, 1f),
                (CombatRangeBand.Medium, 0.75f, 0.85f)));
        BuildWeapon("W09_ThrowingAxe", "weapon:throwing-axe", "투척도끼", 1.4f, 1, 5,
            Throw(0.9f, 11f, 5f, 9f, 0.08f, CombatDamageType.Slash),
            Profiles(
                (CombatRangeBand.Contact, 0.9f, 0.8f),
                (CombatRangeBand.Near, 0.92f, 1f)));

        BuildArmor("A01_ClothHood", "armor:cloth-hood", "천 후드", 0.4f,
            CombatArmorLayer.Clothing, "headwear", Part(CombatBodyPart.Head, 2f, 1f, 1f));
        BuildArmor("A02_Gambeson", "armor:gambeson", "누비옷", 3.5f,
            CombatArmorLayer.Clothing, "torso-clothing",
            Part(CombatBodyPart.Torso, 7f, 5f, 8f),
            Part(CombatBodyPart.LeftArm, 5f, 3f, 5f),
            Part(CombatBodyPart.RightArm, 5f, 3f, 5f));
        BuildArmor("A03_LeatherCap", "armor:leather-cap", "가죽 모자", 0.8f,
            CombatArmorLayer.Clothing, "headwear", Part(CombatBodyPart.Head, 5f, 4f, 3f));
        BuildArmor("A04_LeatherArmor", "armor:leather", "가죽 갑옷", 4.2f,
            CombatArmorLayer.Outer, "torso-outer", Part(CombatBodyPart.Torso, 10f, 8f, 5f));
        BuildArmor("A05_MailCoif", "armor:mail-coif", "사슬 두건", 1.6f,
            CombatArmorLayer.Mail, "headwear-mail", Part(CombatBodyPart.Head, 11f, 10f, 5f));
        BuildArmor("A06_MailShirt", "armor:mail-shirt", "사슬 갑옷", 7.5f,
            CombatArmorLayer.Mail, "torso-mail",
            Part(CombatBodyPart.Torso, 16f, 15f, 7f),
            Part(CombatBodyPart.LeftArm, 10f, 9f, 5f),
            Part(CombatBodyPart.RightArm, 10f, 9f, 5f));
        BuildArmor("A07_IronHelmet", "armor:iron-helmet", "철 투구", 2.8f,
            CombatArmorLayer.Plate, "headwear-plate", Part(CombatBodyPart.Head, 20f, 18f, 13f));
        BuildArmor("A08_Breastplate", "armor:breastplate", "철 흉갑", 9f,
            CombatArmorLayer.Plate, "torso-plate", Part(CombatBodyPart.Torso, 25f, 22f, 16f));
        BuildShield("S01_WoodShield", "shield:wood", "나무 방패", 3.5f, 0.28f, 10f, 7f, 7f);
        BuildShield("S02_IronShield", "shield:iron", "철 방패", 6f, 0.38f, 18f, 15f, 13f);
        BuildExpansionEquipment();
        BuildEquipmentModules();
        EnsureForgeRecipes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Initial medieval combat equipment assets built.");
    }

    private static void BuildExpansionEquipment()
    {
        BuildWeapon("W10_Falchion", "weapon:falchion", "팔시온", 1.7f, 1, 1,
            Melee(0.95f, 12f, 7f, CombatDamageType.Slash, 0.09f), Profiles((CombatRangeBand.Contact, 1.05f, 1.05f)));
        BuildWeapon("W11_Warhammer", "weapon:warhammer", "전투망치", 3.1f, 2, 1,
            Melee(1.35f, 16f, 11f, CombatDamageType.Blunt, 0.04f), Profiles((CombatRangeBand.Contact, 0.9f, 1.2f)));
        BuildWeapon("W12_Halberd", "weapon:halberd", "할버드", 4.2f, 2, 2,
            Melee(1.25f, 17f, 13f, CombatDamageType.Pierce, 0.05f), Profiles((CombatRangeBand.Contact, 0.95f, 1.15f)));
        BuildWeapon("W13_Greatsword", "weapon:greatsword", "대검", 4.6f, 2, 1,
            Melee(1.4f, 20f, 12f, CombatDamageType.Slash, 0.04f), Profiles((CombatRangeBand.Contact, 0.88f, 1.25f)));
        BuildWeapon("W14_CompositeBow", "weapon:composite-bow", "복합궁", 1.8f, 2, 20,
            Projectile(0.9f, 12f, 8f, 20f, 0.05f),
            Profiles((CombatRangeBand.Near, 1f, 0.95f), (CombatRangeBand.Medium, 1.08f, 1f), (CombatRangeBand.Long, 0.82f, 0.9f)),
            BowAmmunitionItemIds, 1, 0.85f, rapid: true, suppressive: true);
        BuildWeapon("W15_WindlassCrossbow", "weapon:windlass-crossbow", "권양 석궁", 5.2f, 2, 22,
            Projectile(1.1f, 20f, 18f, 22f, 0.025f),
            Profiles((CombatRangeBand.Near, 1f, 1f), (CombatRangeBand.Medium, 1.08f, 1.1f), (CombatRangeBand.Long, 0.92f, 1f)),
            CrossbowAmmunitionItemIds, 1, 4.2f);
        BuildWeapon("W16_Handgonne", "weapon:handgonne", "수총", 4.8f, 2, 16,
            Projectile(1.6f, 28f, 25f, 14f, 0.02f),
            Profiles((CombatRangeBand.Near, 0.72f, 1.25f), (CombatRangeBand.Medium, 0.5f, 1f)),
            GunpowderAmmunitionItemIds, 1, 7.5f, suppressive: true);
        BuildWeapon("W17_MatchlockPistol", "weapon:matchlock-pistol", "화승 권총", 2.6f, 1, 14,
            Projectile(1.4f, 24f, 22f, 14f, 0.025f),
            Profiles((CombatRangeBand.Near, 0.8f, 1.2f), (CombatRangeBand.Medium, 0.48f, 0.9f)),
            GunpowderAmmunitionItemIds, 1, 6f, suppressive: true);
        BuildWeapon("W18_Arquebus", "weapon:arquebus", "아쿼버스", 5.6f, 2, 24,
            Projectile(1.7f, 34f, 31f, 18f, 0.018f),
            Profiles((CombatRangeBand.Near, 0.82f, 1.15f), (CombatRangeBand.Medium, 1f, 1.2f), (CombatRangeBand.Long, 0.7f, 1f)),
            GunpowderAmmunitionItemIds, 1, 8.5f, suppressive: true);
        BuildWeapon("W19_SiegeArbalest", "weapon:siege-arbalest", "공성 쇠뇌", 8.5f, 2, 26,
            Projectile(2f, 38f, 34f, 16f, 0.015f),
            Profiles((CombatRangeBand.Medium, 0.9f, 1.25f), (CombatRangeBand.Long, 0.86f, 1.2f)),
            CrossbowAmmunitionItemIds, 1, 9f, suppressive: true);
        BuildWeapon("W20_RuneBlade", "weapon:rune-blade", "룬검", 2.2f, 1, 1,
            Melee(0.9f, 23f, 20f, CombatDamageType.Slash, 0.08f), Profiles((CombatRangeBand.Contact, 1.12f, 1.2f)));
        BuildWeapon("W21_ManaLance", "weapon:mana-lance", "마나 랜스", 4.9f, 2, 2,
            Melee(1.2f, 27f, 26f, CombatDamageType.Pierce, 0.05f), Profiles((CombatRangeBand.Contact, 1f, 1.25f)));

        BuildWeapon("W22_RepeatingCrossbow", "weapon:repeating-crossbow", "반복 석궁", 4.4f, 2, 14,
            Projectile(0.72f, 11f, 8f, 18f, 0.04f),
            Profiles((CombatRangeBand.Near, 1.08f, 0.92f), (CombatRangeBand.Medium, 0.82f, 0.82f)),
            CrossbowAmmunitionItemIds, 5, 0.85f, rapid: true, suppressive: true);
        BuildWeapon("W23_MatchlockLongGun", "weapon:matchlock-long-gun", "화승 장총", 6.4f, 2, 30,
            Projectile(1.75f, 36f, 31f, 22f, 0.016f),
            Profiles((CombatRangeBand.Medium, 0.92f, 1.08f), (CombatRangeBand.Long, 1.08f, 1.12f)),
            GunpowderAmmunitionItemIds, 1, 10f, suppressive: true);
        BuildWeapon("W24_SniperArquebus", "weapon:sniper-arquebus", "저격 아쿼버스", 6.8f, 2, 34,
            Projectile(1.85f, 40f, 37f, 24f, 0.012f),
            Profiles((CombatRangeBand.Medium, 0.72f, 1f), (CombatRangeBand.Long, 1.18f, 1.2f)),
            GunpowderAmmunitionItemIds, 1, 11.5f);
        BuildWeapon("W25_HeavyMatchlock", "weapon:heavy-matchlock", "중화승총", 8.4f, 2, 28,
            Projectile(2.15f, 48f, 46f, 18f, 0.01f),
            Profiles((CombatRangeBand.Medium, 1f, 1.28f), (CombatRangeBand.Long, 0.82f, 1.12f)),
            GunpowderAmmunitionItemIds, 1, 14f, suppressive: true);
        BuildWeapon("W26_Shotgun", "weapon:shotgun", "산탄총", 6.1f, 2, 12,
            Projectile(1.55f, 30f, 19f, 12f, 0.035f),
            Profiles((CombatRangeBand.Contact, 0.9f, 1.35f), (CombatRangeBand.Near, 1.2f, 1.28f), (CombatRangeBand.Medium, 0.34f, 0.72f)),
            new[] { "ammo:scatter-cartridge", "ammo:smoke-cartridge" }, 1, 8f, suppressive: true);
        BuildWeapon("W27_PoweredStrikingGauntlet", "weapon:powered-striking-gauntlet", "동력 타격장갑", 7.2f, 1, 1,
            Melee(1.6f, 32f, 28f, CombatDamageType.Blunt, 0.025f),
            Profiles((CombatRangeBand.Contact, 1.05f, 1.42f)));
        BuildWeapon("W28_BlacksteelPoleaxe", "weapon:blacksteel-poleaxe", "흑강 폴액스", 6.8f, 2, 2,
            Melee(1.45f, 34f, 35f, CombatDamageType.Pierce, 0.025f),
            Profiles((CombatRangeBand.Contact, 0.94f, 1.38f)));
        BuildWeapon("W29_Estoc", "weapon:estoc", "에스터크", 2.2f, 1, 1,
            Melee(0.86f, 13f, 19f, CombatDamageType.Pierce, 0.075f),
            Profiles((CombatRangeBand.Contact, 1.12f, 1.05f)));
        BuildWeapon("W30_Pollaxe", "weapon:pollaxe", "폴액스", 4.8f, 2, 2,
            Melee(1.3f, 22f, 21f, CombatDamageType.Blunt, 0.04f),
            Profiles((CombatRangeBand.Contact, 0.92f, 1.32f)));
        BuildWeapon("W31_RuneBow", "weapon:rune-bow", "룬 활", 2.4f, 2, 24,
            Projectile(0.92f, 20f, 18f, 22f, 0.035f),
            Profiles((CombatRangeBand.Near, 0.92f, 1f), (CombatRangeBand.Medium, 1.1f, 1.08f), (CombatRangeBand.Long, 0.9f, 1f)),
            BowAmmunitionItemIds.Concat(new[] { "ammo:rune-cartridge" }).ToArray(), 1, 1.05f, rapid: true);

        BuildArmor("A09_Brigandine", "armor:brigandine", "브리간딘", 6.2f, CombatArmorLayer.Outer, "torso-brigandine", Part(CombatBodyPart.Torso, 18f, 15f, 11f));
        BuildArmor("A10_ScaleCoat", "armor:scale-coat", "비늘 외투", 8.2f, CombatArmorLayer.Mail, "torso-scale", Part(CombatBodyPart.Torso, 23f, 19f, 13f));
        BuildArmor("A11_ClosedPlateHelm", "armor:closed-plate-helm", "폐쇄형 판금 투구", 3.8f, CombatArmorLayer.Plate, "headwear-closed-plate", Part(CombatBodyPart.Head, 28f, 25f, 19f));
        BuildArmor("A12_ArticulatedPlate", "armor:articulated-plate", "관절식 판금갑", 13f, CombatArmorLayer.Plate, "torso-articulated-plate", Part(CombatBodyPart.Torso, 34f, 31f, 23f), Part(CombatBodyPart.LeftArm, 22f, 20f, 16f), Part(CombatBodyPart.RightArm, 22f, 20f, 16f));
        BuildArmor("A13_BlastCoat", "armor:blast-coat", "방폭 외투", 7f, CombatArmorLayer.Outer, "torso-blast", Part(CombatBodyPart.Torso, 16f, 18f, 26f));
        BuildArmor("A14_SmokeHood", "armor:smoke-hood", "연기 두건", 1.1f, CombatArmorLayer.Clothing, "headwear-smoke", Part(CombatBodyPart.Head, 7f, 6f, 12f));
        BuildArmor("A15_PoweredHarness", "armor:powered-harness", "동력 보조 갑주", 18f, CombatArmorLayer.Plate, "torso-powered", Part(CombatBodyPart.Torso, 42f, 40f, 34f));
        BuildArmor("A16_RuneWardMail", "armor:rune-ward-mail", "룬 수호 사슬갑옷", 9f, CombatArmorLayer.Mail, "torso-rune-mail", Part(CombatBodyPart.Torso, 30f, 31f, 20f));
        BuildArmor("A17_BlacksteelCarapace", "armor:blacksteel-carapace", "흑강 갑각", 15f, CombatArmorLayer.Plate, "torso-blacksteel", Part(CombatBodyPart.Torso, 48f, 46f, 38f));
        BuildArmor("A18_JackOfPlates", "armor:jack-of-plates", "잭 오브 플레이트", 5.4f, CombatArmorLayer.Outer, "torso-jack-of-plates", Part(CombatBodyPart.Torso, 17f, 14f, 10f));
        BuildArmor("A19_PowderCuirass", "armor:powder-cuirass", "화약수 흉갑", 8.2f, CombatArmorLayer.Outer, "torso-powder-cuirass", Part(CombatBodyPart.Torso, 14f, 18f, 31f));
        BuildArmor("A20_PaddedHood", "armor:padded-hood", "누비 두건", 0.8f, CombatArmorLayer.Clothing, "headwear-padded", Part(CombatBodyPart.Head, 6f, 4f, 9f));
        BuildArmor("A21_HardenedLeatherCoat", "armor:hardened-leather-coat", "경화 가죽 외투", 4.8f, CombatArmorLayer.Outer, "torso-hardened-leather", Part(CombatBodyPart.Torso, 14f, 11f, 8f));
        BuildShield("S03_Buckler", "shield:buckler", "버클러", 1.8f, 0.32f, 12f, 10f, 9f);
        BuildShield("S04_TowerShield", "shield:tower", "대형 방패", 10f, 0.52f, 28f, 25f, 22f);
        BuildShield("S05_RuneShield", "shield:rune", "룬 방패", 6.5f, 0.48f, 30f, 31f, 25f);
        BuildShield("S06_PoweredShield", "shield:powered", "동력 방패", 13f, 0.62f, 39f, 38f, 33f);
        BuildShield("S07_BlacksteelShield", "shield:blacksteel", "흑강 방패", 11f, 0.56f, 42f, 40f, 25f);
        BuildShield("S08_ManaBuckler", "shield:mana-buckler", "마나 버클러", 2.2f, 0.34f, 14f, 24f, 18f);
        BuildShield("S09_Pavise", "shield:pavise", "파비스", 12f, 0.68f, 34f, 31f, 26f);
    }

    private static void BuildEquipmentModules()
    {
        const string root = "Assets/Resources/SO/Combat/EquipmentModules";
        EnsureFolder("Assets/Resources/SO/Combat", "EquipmentModules");
        (string id, string name, EquipmentLineageKind kind)[] specs =
        {
            ("module:weapon:balanced-core", "균형 심재", EquipmentLineageKind.Weapon),
            ("module:weapon:penetrator", "관통 촉", EquipmentLineageKind.Weapon),
            ("module:weapon:precision-sight", "정밀 조준기", EquipmentLineageKind.Weapon),
            ("module:weapon:quick-action", "속동 기구", EquipmentLineageKind.Weapon),
            ("module:weapon:suppression-coil", "제압 코일", EquipmentLineageKind.Weapon),
            ("module:weapon:execution-edge", "처형 날", EquipmentLineageKind.Weapon),
            ("module:weapon:endurance-binding", "내구 결속", EquipmentLineageKind.Weapon),
            ("module:weapon:mana-conduit", "마나 도관", EquipmentLineageKind.Weapon),
            ("module:armor:impact-liner", "충격 내피", EquipmentLineageKind.Armor),
            ("module:armor:plate-web", "판금 연결망", EquipmentLineageKind.Armor),
            ("module:armor:smoke-filter", "연기 여과기", EquipmentLineageKind.Armor),
            ("module:armor:thermal-layer", "열 차폐층", EquipmentLineageKind.Armor),
            ("module:armor:mobility-joint", "기동 관절", EquipmentLineageKind.Armor),
            ("module:armor:self-seal", "자가 봉합재", EquipmentLineageKind.Armor),
            ("module:armor:rune-ward", "룬 수호편", EquipmentLineageKind.Armor),
            ("module:armor:blacksteel-rib", "흑강 늑골", EquipmentLineageKind.Armor),
            ("module:shield:reinforced-rim", "강화 테두리", EquipmentLineageKind.Shield),
            ("module:shield:shock-grip", "충격 손잡이", EquipmentLineageKind.Shield),
            ("module:shield:ward-emitter", "수호 방출기", EquipmentLineageKind.Shield),
            ("module:shield:anchor-core", "고정 핵", EquipmentLineageKind.Shield)
        };
        for (int index = 0; index < specs.Length; index++)
        {
            string path = $"{root}/EM{index + 1:D2}.asset";
            EquipmentModuleDefinitionSO module =
                AssetDatabase.LoadAssetAtPath<EquipmentModuleDefinitionSO>(path);
            if (module == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
                module = ScriptableObject.CreateInstance<EquipmentModuleDefinitionSO>();
                AssetDatabase.CreateAsset(module, path);
            }
            SerializedObject serialized = new SerializedObject(module);
            serialized.FindProperty("moduleId").stringValue = specs[index].id;
            serialized.FindProperty("displayName").stringValue = specs[index].name;
            serialized.FindProperty("description").stringValue = $"원정에서 회수하는 {specs[index].name} 개량 부품";
            serialized.FindProperty("lineageKind").enumValueIndex = (int)specs[index].kind;
            serialized.FindProperty("minimumEra").enumValueIndex = index < 8 ? 1 : index < 16 ? 2 : 3;
            serialized.FindProperty("powerPerGrade").floatValue = 0.035f + index % 4 * 0.005f;
            serialized.FindProperty("utilityPerGrade").floatValue = 0.025f + index % 3 * 0.005f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);
        }
    }

    private static void EnsureForgeRecipes()
    {
        string[] combatIds = Resources
            .LoadAll<CombatEquipmentDefinitionSO>(
                ResourceCombatEquipmentCatalog.ResourcePath)
            .Where(definition => definition != null
                && !string.IsNullOrWhiteSpace(definition.EquipmentId))
            .Select(definition => definition.EquipmentId)
            .Append(CombatItemDefinitions.ArrowBundleRecipeId)
            .Append(CombatItemDefinitions.BoltBundleRecipeId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        foreach (string path in AssetDatabase.FindAssets(
                     "t:BuildingSO",
                     new[] { "Assets/Resources/SO/Building/Modular" })
                 .Select(AssetDatabase.GUIDToAssetPath))
        {
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            if (building == null
                || !building.name.StartsWith("S08", StringComparison.OrdinalIgnoreCase)
                || building.GetAbility<BuildingEquipmentCraftingAbility>() is not { } crafting)
            {
                continue;
            }

            string[] mergedIds = crafting.CraftableEquipmentIds
                .Concat(combatIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            float workUnits = crafting.workUnitsPerCycle;
            building.AbilityModules.Remove<BuildingEquipmentCraftingAbility>();
            building.AbilityModules.Add(new BuildingEquipmentCraftingAbility
            {
                craftableEquipmentIds = mergedIds,
                workUnitsPerCycle = workUnits
            });
            EditorUtility.SetDirty(building);
        }
    }

    private static void BuildWeapon(
        string fileName,
        string id,
        string displayName,
        float weight,
        int hands,
        int maximumRange,
        CombatAttackVerb verb,
        List<CombatRangeProfile> profiles,
        IReadOnlyList<string> compatibleAmmunitionItemIds = null,
        int magazineCapacity = 0,
        float reloadSeconds = 0f,
        bool rapid = false,
        bool suppressive = false)
    {
        CombatWeaponSO asset = GetOrCreate<CombatWeaponSO>(fileName);
        SerializedObject serialized = new SerializedObject(asset);
        SetBase(serialized, id, displayName, weight, hands, 100f);
        SetManagedList(serialized.FindProperty("verbs"), verb);
        SetRangeProfiles(serialized.FindProperty("rangeProfiles"), profiles);
        serialized.FindProperty("maximumRange").intValue = maximumRange;
        SerializedProperty ammunitionIds =
            serialized.FindProperty("compatibleAmmunitionItemIds");
        int ammunitionCount = compatibleAmmunitionItemIds?.Count ?? 0;
        ammunitionIds.arraySize = ammunitionCount;
        for (int index = 0; index < ammunitionCount; index++)
        {
            ammunitionIds.GetArrayElementAtIndex(index).stringValue =
                compatibleAmmunitionItemIds[index];
        }
        serialized.FindProperty("magazineCapacity").intValue = magazineCapacity;
        serialized.FindProperty("reloadSeconds").floatValue = reloadSeconds;
        serialized.FindProperty("supportsAimed").boolValue = true;
        serialized.FindProperty("supportsRapid").boolValue = rapid;
        serialized.FindProperty("supportsSuppressive").boolValue = suppressive;
        bool gunpowder = id is "weapon:handgonne"
            or "weapon:matchlock-pistol"
            or "weapon:arquebus"
            or "weapon:matchlock-long-gun"
            or "weapon:sniper-arquebus"
            or "weapon:heavy-matchlock"
            or "weapon:shotgun";
        serialized.FindProperty("gunpowderWeapon").boolValue = gunpowder;
        serialized.FindProperty("maximumMisfireChance").floatValue = gunpowder ? 0.2f : 0f;
        serialized.FindProperty("smokeExposure").floatValue = gunpowder ? 10f : 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetV23BaselineCraftWork(asset);
        EditorUtility.SetDirty(asset);
    }

    private static void BuildArmor(
        string fileName,
        string id,
        string displayName,
        float weight,
        CombatArmorLayer layer,
        string collisionTag,
        params CombatArmorPartValue[] parts)
    {
        CombatArmorSO asset = GetOrCreate<CombatArmorSO>(fileName);
        SerializedObject serialized = new SerializedObject(asset);
        SetBase(serialized, id, displayName, weight, 0, 120f);
        serialized.FindProperty("layer").enumValueIndex = (int)layer;
        serialized.FindProperty("collisionTag").stringValue = collisionTag;
        SerializedProperty list = serialized.FindProperty("bodyPartDefense");
        list.arraySize = parts.Length;
        for (int i = 0; i < parts.Length; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("bodyPart").enumValueIndex = (int)parts[i].bodyPart;
            element.FindPropertyRelative("slashDefense").floatValue = parts[i].slashDefense;
            element.FindPropertyRelative("pierceDefense").floatValue = parts[i].pierceDefense;
            element.FindPropertyRelative("bluntDefense").floatValue = parts[i].bluntDefense;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetV23BaselineCraftWork(asset);
        EditorUtility.SetDirty(asset);
    }

    private static void BuildShield(
        string fileName,
        string id,
        string displayName,
        float weight,
        float blockChance,
        float slash,
        float pierce,
        float blunt)
    {
        CombatShieldSO asset = GetOrCreate<CombatShieldSO>(fileName);
        SerializedObject serialized = new SerializedObject(asset);
        SetBase(serialized, id, displayName, weight, 1, 160f);
        serialized.FindProperty("frontalBlockChance").floatValue = blockChance;
        serialized.FindProperty("slashDefense").floatValue = slash;
        serialized.FindProperty("pierceDefense").floatValue = pierce;
        serialized.FindProperty("bluntDefense").floatValue = blunt;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetV23BaselineCraftWork(asset);
        EditorUtility.SetDirty(asset);
    }

    private static void SetV23BaselineCraftWork(
        CombatEquipmentDefinitionSO definition)
    {
        float form = definition.Era == EquipmentEra.RuneAbyssal
            ? 150f
            : definition switch
            {
                CombatWeaponSO weapon when weapon.GunpowderWeapon => 70f,
                CombatWeaponSO weapon
                    when weapon.Kind == CombatEquipmentKind.RangedWeapon =>
                    definition.OccupiedHands >= 2 || definition.Weight >= 4f
                        ? 55f
                        : 40f,
                CombatWeaponSO when definition.Weight <= 1.2f => 20f,
                CombatWeaponSO
                    when definition.OccupiedHands >= 2
                        || definition.Weight >= 4f => 55f,
                CombatWeaponSO => 30f,
                CombatShieldSO => definition.Weight >= 6f ? 110f : 55f,
                CombatArmorSO => definition.Weight < 4f
                    ? 45f
                    : definition.Weight < 8f ? 75f : 110f,
                _ => 30f
            };
        int componentUnits = definition.RequiredComponentInputs.Sum(value =>
            value.Amount);
        int precisionStages = Mathf.Clamp(
            definition.Tier
            + (definition.Era >= EquipmentEra.MatureIndustrial ? 1 : 0)
            + (definition.Era == EquipmentEra.RuneAbyssal ? 1 : 0),
            0,
            4);
        float baseline = Mathf.Max(
            4f,
            Mathf.Round((form
                + definition.PrimaryMaterialAmount * 8f
                + componentUnits * 12f
                + precisionStages * 16f) / 4f) * 4f);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("requiredCraftWork").floatValue = baseline;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBase(
        SerializedObject serialized,
        string id,
        string displayName,
        float weight,
        int hands,
        float durability)
    {
        serialized.FindProperty("equipmentId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = $"{displayName} 전투 장비";
        serialized.FindProperty("itemId").stringValue = PhysicalItemIds.ForEquipment(id);
        serialized.FindProperty("weight").floatValue = weight;
        serialized.FindProperty("occupiedHands").intValue = hands;
        serialized.FindProperty("maxDurability").floatValue = durability;
        SetProgressionRules(serialized, id);
        SetMaterialRules(serialized, id, weight);
        SetComponentRules(serialized, id);
    }

    private static void SetComponentRules(
        SerializedObject serialized,
        string equipmentId)
    {
        List<(string itemId, int amount)> components = new();
        bool growth = equipmentId is
            "weapon:longsword" or "armor:gambeson" or "shield:iron" or
            "weapon:halberd" or "weapon:greatsword" or "weapon:windlass-crossbow" or
            "weapon:matchlock-pistol" or "weapon:siege-arbalest" or "weapon:rune-blade" or
            "weapon:repeating-crossbow" or "weapon:sniper-arquebus" or
            "weapon:heavy-matchlock" or "weapon:blacksteel-poleaxe" or "weapon:rune-bow" or
            "armor:scale-coat" or "armor:articulated-plate" or "armor:powered-harness" or
            "armor:rune-ward-mail" or "armor:blacksteel-carapace" or
            "shield:buckler" or "shield:rune" or "shield:powered";
        if (growth)
        {
            components.Add(("component:growth-frame", 1));
        }

        switch (equipmentId)
        {
            case "weapon:shortbow":
            case "weapon:longbow":
            case "weapon:composite-bow":
                components.Add(("material:bowstring", 1));
                components.Add(("material:rope", 1));
                if (equipmentId == "weapon:composite-bow")
                {
                    components.Add(("material:laminated-lumber", 1));
                }
                break;
            case "weapon:crossbow":
            case "weapon:windlass-crossbow":
            case "weapon:repeating-crossbow":
                components.Add(("material:bowstring", 1));
                components.Add(("component:machine-parts", 1));
                components.Add(("material:laminated-lumber", 1));
                if (equipmentId == "weapon:windlass-crossbow")
                {
                    components.Add(("component:lead-counterweight", 1));
                }
                if (equipmentId == "weapon:repeating-crossbow")
                {
                    components.Add(("material:spring-steel", 1));
                }
                break;
            case "weapon:siege-arbalest":
                components.Add(("material:bowstring", 2));
                components.Add(("component:siege-counterweight", 1));
                components.Add(("component:engineering-drawing", 1));
                components.Add(("component:prototype-package", 1));
                break;
            case "weapon:handgonne":
            case "weapon:matchlock-pistol":
            case "weapon:arquebus":
            case "weapon:matchlock-long-gun":
            case "weapon:sniper-arquebus":
            case "weapon:heavy-matchlock":
            case "weapon:shotgun":
                components.Add(("component:machine-parts", 1));
                components.Add(("component:precision-parts", 1));
                components.Add(("component:engineering-drawing", 1));
                if (equipmentId is "weapon:matchlock-long-gun" or "weapon:sniper-arquebus"
                    or "weapon:heavy-matchlock" or "weapon:shotgun")
                {
                    components.Add(("material:barrel-steel", 1));
                }
                if (equipmentId == "weapon:sniper-arquebus")
                {
                    components.Add(("component:precision-optics", 1));
                }
                break;
            case "weapon:rune-blade":
                components.Add(("component:rune-conductor", 1));
                components.Add(("component:rune-control-panel", 1));
                break;
            case "weapon:mana-lance":
                components.Add(("component:rune-conductor", 1));
                break;
            case "weapon:rune-bow":
                components.Add(("material:laminated-lumber", 1));
                components.Add(("material:mana-alloy", 1));
                components.Add(("material:bowstring", 1));
                break;
            case "weapon:powered-striking-gauntlet":
                components.Add(("material:plate-blank", 1));
                components.Add(("component:machine-parts", 2));
                break;
            case "weapon:blacksteel-poleaxe":
                components.Add(("material:blacksteel-ingot", 2));
                components.Add(("material:spring-steel", 1));
                break;
            case "armor:mail-coif":
            case "armor:mail-shirt":
            case "armor:scale-coat":
                components.Add(("material:chain-mesh", 1));
                break;
            case "armor:gambeson":
                components.Add(("textile:quilted-liner", 1));
                break;
            case "armor:brigandine":
                components.Add(("textile:quilted-liner", 1));
                components.Add(("component:brigandine-padding", 1));
                components.Add(("component:textile-hardener", 1));
                break;
            case "armor:jack-of-plates":
                components.Add(("material:hardened-leather", 1));
                components.Add(("material:plate-blank", 1));
                components.Add(("textile:quilted-liner", 1));
                break;
            case "armor:blast-coat":
                components.Add(("textile:quilted-liner", 1));
                components.Add(("component:blast-coat-shell", 1));
                components.Add(("textile:insulating-cloth", 1));
                break;
            case "armor:smoke-hood":
                components.Add(("textile:sterile-cloth", 1));
                components.Add(("textile:insulating-cloth", 1));
                break;
            case "armor:powder-cuirass":
                components.Add(("material:plate-blank", 1));
                components.Add(("textile:insulating-cloth", 1));
                break;
            case "armor:padded-hood":
                components.Add(("textile:quilted-liner", 1));
                break;
            case "armor:hardened-leather-coat":
                components.Add(("material:hardened-leather", 2));
                break;
            case "armor:articulated-plate":
                components.Add(("component:textile-hardener", 1));
                components.Add(("material:plate-blank", 2));
                break;
            case "armor:powered-harness":
                components.Add(("component:machine-parts", 2));
                components.Add(("component:precision-parts", 2));
                components.Add(("component:powered-armor-joint", 2));
                components.Add(("component:prototype-package", 1));
                break;
            case "armor:rune-ward-mail":
                components.Add(("material:chain-mesh", 1));
                components.Add(("material:mana-alloy", 1));
                components.Add(("component:rune-conductor", 1));
                components.Add(("component:dreamweave-rune-lining", 1));
                components.Add(("component:rune-leather-lining", 1));
                break;
            case "armor:blacksteel-carapace":
                components.Add(("component:blacksteel-defense-plate", 2));
                break;
            case "shield:rune":
                components.Add(("component:rune-conductor", 1));
                components.Add(("component:rune-tuning-shield", 1));
                components.Add(("component:rune-leather-strap", 1));
                components.Add(("component:rune-control-panel", 1));
                break;
            case "shield:powered":
                components.Add(("material:plate-blank", 2));
                components.Add(("component:machine-parts", 2));
                break;
            case "shield:blacksteel":
                components.Add(("material:blacksteel-ingot", 2));
                break;
            case "shield:mana-buckler":
                components.Add(("material:mana-alloy", 1));
                components.Add(("component:rune-conductor", 1));
                break;
            case "shield:pavise":
                components.Add(("material:laminated-lumber", 2));
                components.Add(("material:plate-blank", 1));
                break;
        }

        SerializedProperty list = serialized.FindProperty("requiredComponentInputs");
        list.arraySize = components.Count;
        for (int index = 0; index < components.Count; index++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("itemId").stringValue = components[index].itemId;
            entry.FindPropertyRelative("amount").intValue = components[index].amount;
        }
    }

    private static void SetProgressionRules(SerializedObject serialized, string equipmentId)
    {
        string requiredResearchId = equipmentId switch
        {
            "weapon:dagger" or "weapon:spear" or "weapon:javelin" or
            "armor:cloth-hood" or "armor:leather-cap" or "shield:wood" => string.Empty,
            "weapon:longsword" or "weapon:throwing-axe" or "weapon:falchion" or
            "weapon:halberd" or "shield:buckler" => "research:equipment:weapon-patterns",
            "weapon:mace" or "weapon:warhammer" => "research:metallurgy:iron",
            "weapon:shortbow" or "weapon:longbow" or "weapon:composite-bow" => "research:equipment:bowyery",
            "weapon:crossbow" or "weapon:windlass-crossbow" => "research:equipment:mechanical-projectiles",
            "weapon:repeating-crossbow" => "research:equipment:mechanical-projectiles",
            "weapon:greatsword" => "research:metallurgy:steel",
            "weapon:estoc" or "weapon:pollaxe" => "research:metallurgy:steel",
            "weapon:handgonne" or "weapon:matchlock-pistol" => "research:equipment:ignition-mechanisms",
            "weapon:arquebus" => "research:equipment:ballistics",
            "weapon:matchlock-long-gun" or "weapon:sniper-arquebus" => "research:equipment:ballistics",
            "weapon:siege-arbalest" => "research:equipment:pressure-barrels",
            "weapon:heavy-matchlock" or "weapon:shotgun" => "research:equipment:pressure-barrels",
            "weapon:rune-blade" or "weapon:mana-lance" or
            "weapon:rune-bow" or "armor:rune-ward-mail" or "shield:rune" or
            "shield:mana-buckler" => "research:equipment:rune-module-tuning",
            "armor:gambeson" or "armor:padded-hood" => "research:textile:tailoring",
            "armor:leather" or "armor:hardened-leather-coat" => "research:textile:tanning",
            "armor:mail-coif" or "armor:mail-shirt" or "armor:scale-coat" => "research:equipment:mail-weaving",
            "armor:iron-helmet" or "armor:breastplate" or
            "armor:closed-plate-helm" or "armor:articulated-plate" => "research:equipment:articulated-plate",
            "armor:brigandine" or "armor:jack-of-plates" => "research:equipment:armor-tailoring",
            "armor:blast-coat" or "armor:smoke-hood" or "armor:powder-cuirass" => "research:equipment:pressure-barrels",
            "armor:powered-harness" or "weapon:powered-striking-gauntlet" or
            "shield:powered" => "research:equipment:powered-armor",
            "armor:blacksteel-carapace" or "weapon:blacksteel-poleaxe" or
            "shield:blacksteel" => "research:industry:dark-foundry",
            "shield:iron" => "research:metallurgy:iron",
            "shield:tower" => "research:defense:fortification",
            "shield:pavise" => "research:defense:siege-fortification",
            _ => "research:equipment:weapon-patterns"
        };
        bool growth = equipmentId is
            "weapon:longsword" or "armor:gambeson" or "shield:iron" or
            "weapon:halberd" or "weapon:greatsword" or "weapon:windlass-crossbow" or
            "weapon:matchlock-pistol" or "weapon:siege-arbalest" or "weapon:rune-blade" or
            "weapon:repeating-crossbow" or "weapon:sniper-arquebus" or
            "weapon:heavy-matchlock" or "weapon:blacksteel-poleaxe" or "weapon:rune-bow" or
            "armor:scale-coat" or "armor:articulated-plate" or "armor:powered-harness" or
            "armor:rune-ward-mail" or "armor:blacksteel-carapace" or
            "shield:buckler" or "shield:rune" or "shield:powered";
        bool fourSlots = equipmentId is "weapon:siege-arbalest" or "weapon:rune-blade"
            or "armor:powered-harness" or "armor:blacksteel-carapace" or "shield:rune"
            or "shield:powered" or "weapon:blacksteel-poleaxe";
        EquipmentEra era = string.IsNullOrWhiteSpace(requiredResearchId)
            ? EquipmentEra.Starting
            : requiredResearchId.Contains("ignition", StringComparison.Ordinal)
                || requiredResearchId.Contains("ballistics", StringComparison.Ordinal)
                ? EquipmentEra.EarlyIndustrial
            : requiredResearchId.Contains("pressure", StringComparison.Ordinal)
                || requiredResearchId.Contains("blast", StringComparison.Ordinal)
                ? EquipmentEra.MatureIndustrial
            : requiredResearchId.Contains("rune", StringComparison.Ordinal)
                || requiredResearchId.Contains("powered-armor", StringComparison.Ordinal)
                || requiredResearchId.Contains("dark-foundry", StringComparison.Ordinal)
                ? EquipmentEra.RuneAbyssal
                : EquipmentEra.Medieval;
        EquipmentLineageKind lineage = equipmentId.StartsWith("armor:", StringComparison.Ordinal)
            ? EquipmentLineageKind.Armor
            : equipmentId.StartsWith("shield:", StringComparison.Ordinal)
                ? EquipmentLineageKind.Shield
                : EquipmentLineageKind.Weapon;
        serialized.FindProperty("requiredResearchId").stringValue = requiredResearchId;
        serialized.FindProperty("era").enumValueIndex = (int)era;
        serialized.FindProperty("tier").intValue = (int)era;
        serialized.FindProperty("slotProfile").intValue = (int)(fourSlots
            ? EquipmentSlotProfile.GrowthFour
            : growth ? EquipmentSlotProfile.GrowthThree
            : string.IsNullOrWhiteSpace(requiredResearchId)
                ? EquipmentSlotProfile.None
                : EquipmentSlotProfile.StandardOne);
        serialized.FindProperty("lineageKind").enumValueIndex = (int)lineage;
        serialized.FindProperty("growthEquipment").boolValue = growth;
        serialized.FindProperty("growthBaseStatMultiplier").floatValue = growth ? 0.88f : 1f;
    }

    private static void SetMaterialRules(
        SerializedObject serialized,
        string equipmentId,
        float weight)
    {
        (string defaultMaterial, CombatMaterialFamily[] families) = equipmentId switch
        {
            "weapon:dagger" or "weapon:longsword" =>
                ("material:iron", new[]
                {
                    CombatMaterialFamily.Bone,
                    CombatMaterialFamily.Metal
                }),
            "weapon:spear" or "weapon:javelin" =>
                ("material:wood", new[]
                {
                    CombatMaterialFamily.Wood,
                    CombatMaterialFamily.Bone,
                    CombatMaterialFamily.Metal
                }),
            "weapon:mace" or "weapon:throwing-axe" =>
                ("material:iron", new[]
                {
                    CombatMaterialFamily.Stone,
                    CombatMaterialFamily.Bone,
                    CombatMaterialFamily.Metal
                }),
            "weapon:shortbow" or "weapon:longbow" =>
                ("material:wood", new[]
                {
                    CombatMaterialFamily.Wood,
                    CombatMaterialFamily.Bone
                }),
            "armor:cloth-hood" or "armor:gambeson" =>
                ("material:cloth", new[]
                {
                    CombatMaterialFamily.Textile,
                    CombatMaterialFamily.Leather
                }),
            "armor:leather-cap" or "armor:leather" =>
                ("material:leather", new[]
                {
                    CombatMaterialFamily.Leather
                }),
            "armor:mail-coif" or "armor:mail-shirt"
                or "armor:iron-helmet" or "armor:breastplate" =>
                ("material:iron", new[]
                {
                    CombatMaterialFamily.Metal
                }),
            "shield:wood" =>
                ("material:wood", new[]
                {
                    CombatMaterialFamily.Wood,
                    CombatMaterialFamily.Bone,
                    CombatMaterialFamily.Metal
                }),
            "shield:iron" =>
                ("material:iron", new[]
                {
                    CombatMaterialFamily.Wood,
                    CombatMaterialFamily.Bone,
                    CombatMaterialFamily.Metal
                }),
            _ => ("material:iron", new[] { CombatMaterialFamily.Metal })
        };

        serialized.FindProperty("defaultMaterialId").stringValue =
            defaultMaterial;
        serialized.FindProperty("primaryMaterialAmount").intValue =
            Mathf.Clamp(Mathf.CeilToInt(weight * 0.75f), 1, 10);
        SerializedProperty allowed =
            serialized.FindProperty("allowedMaterialFamilies");
        allowed.arraySize = families.Length;
        for (int index = 0; index < families.Length; index++)
        {
            allowed.GetArrayElementAtIndex(index).enumValueIndex =
                (int)families[index];
        }
    }

    private static void SetManagedList(SerializedProperty list, CombatAttackVerb verb)
    {
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).managedReferenceValue = verb;
    }

    private static void SetRangeProfiles(
        SerializedProperty list,
        IReadOnlyList<CombatRangeProfile> profiles)
    {
        list.arraySize = profiles.Count;
        for (int i = 0; i < profiles.Count; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("band").enumValueIndex = (int)profiles[i].band;
            element.FindPropertyRelative("accuracyMultiplier").floatValue = profiles[i].accuracyMultiplier;
            element.FindPropertyRelative("damageMultiplier").floatValue = profiles[i].damageMultiplier;
        }
    }

    private static MeleeStrikeVerb Melee(
        float time,
        float damage,
        float penetration,
        CombatDamageType type,
        float tracking)
    {
        return new MeleeStrikeVerb
        {
            attackTime = time,
            baseDamage = damage,
            penetration = penetration,
            damageType = type,
            tracking = tracking
        };
    }

    private static ProjectileVerb Projectile(
        float time,
        float damage,
        float penetration,
        float projectileSpeed,
        float tracking)
    {
        return new ProjectileVerb
        {
            attackTime = time,
            baseDamage = damage,
            penetration = penetration,
            damageType = CombatDamageType.Pierce,
            projectileSpeed = projectileSpeed,
            tracking = tracking
        };
    }

    private static RecoverableThrowVerb Throw(
        float time,
        float damage,
        float penetration,
        float projectileSpeed,
        float tracking,
        CombatDamageType damageType = CombatDamageType.Pierce)
    {
        return new RecoverableThrowVerb
        {
            attackTime = time,
            baseDamage = damage,
            penetration = penetration,
            damageType = damageType,
            projectileSpeed = projectileSpeed,
            tracking = tracking
        };
    }

    private static CombatArmorPartValue Part(
        CombatBodyPart part,
        float slash,
        float pierce,
        float blunt)
    {
        return new CombatArmorPartValue
        {
            bodyPart = part,
            slashDefense = slash,
            pierceDefense = pierce,
            bluntDefense = blunt
        };
    }

    private static List<CombatRangeProfile> Profiles(
        params (CombatRangeBand band, float accuracy, float damage)[] values)
    {
        List<CombatRangeProfile> result = new List<CombatRangeProfile>();
        foreach ((CombatRangeBand band, float accuracy, float damage) value in values)
        {
            result.Add(new CombatRangeProfile
            {
                band = value.band,
                accuracyMultiplier = value.accuracy,
                damageMultiplier = value.damage
            });
        }

        return result;
    }

    private static T GetOrCreate<T>(string fileName) where T : ScriptableObject
    {
        string path = $"{Root}/{fileName}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Resources", "SO");
        EnsureFolder("Assets/Resources/SO", "Combat");
        EnsureFolder("Assets/Resources/SO/Combat", "Equipment");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
