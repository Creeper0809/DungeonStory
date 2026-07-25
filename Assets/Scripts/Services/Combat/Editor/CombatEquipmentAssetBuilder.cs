#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentAssetBuilder
{
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
            CombatItemDefinitions.ArrowItemId, 1, 0.75f, rapid: true, suppressive: true);
        BuildWeapon("W06_Longbow", "weapon:longbow", "장궁", 2.1f, 2, 18,
            Projectile(1.2f, 10f, 6f, 18f, 0.04f),
            Profiles(
                (CombatRangeBand.Contact, 0.2f, 0.5f),
                (CombatRangeBand.Near, 0.85f, 0.9f),
                (CombatRangeBand.Medium, 1f, 1f),
                (CombatRangeBand.Long, 0.72f, 0.9f)),
            CombatItemDefinitions.ArrowItemId, 1, 1f, suppressive: true);
        BuildWeapon("W07_Crossbow", "weapon:crossbow", "석궁", 3.8f, 2, 18,
            Projectile(1f, 14f, 12f, 20f, 0.03f),
            Profiles(
                (CombatRangeBand.Contact, 0.25f, 0.65f),
                (CombatRangeBand.Near, 1f, 1.05f),
                (CombatRangeBand.Medium, 1.05f, 1.05f),
                (CombatRangeBand.Long, 0.85f, 0.95f)),
            CombatItemDefinitions.BoltItemId, 1, 2.2f);
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
        EnsureForgeRecipes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Initial medieval combat equipment assets built.");
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
            float workSeconds = crafting.workSecondsPerCycle;
            building.AbilityModules.Remove<BuildingEquipmentCraftingAbility>();
            building.AbilityModules.Add(new BuildingEquipmentCraftingAbility
            {
                craftableEquipmentIds = mergedIds,
                workSecondsPerCycle = workSeconds
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
        string ammunitionItemId = "",
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
        serialized.FindProperty("ammunitionItemId").stringValue = ammunitionItemId;
        serialized.FindProperty("magazineCapacity").intValue = magazineCapacity;
        serialized.FindProperty("reloadSeconds").floatValue = reloadSeconds;
        serialized.FindProperty("supportsAimed").boolValue = true;
        serialized.FindProperty("supportsRapid").boolValue = rapid;
        serialized.FindProperty("supportsSuppressive").boolValue = suppressive;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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
        EditorUtility.SetDirty(asset);
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
        serialized.FindProperty("itemId").stringValue = DungeonItemCatalogSO.EquipmentItemId(id);
        serialized.FindProperty("weight").floatValue = weight;
        serialized.FindProperty("occupiedHands").intValue = hands;
        serialized.FindProperty("maxDurability").floatValue = durability;
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
