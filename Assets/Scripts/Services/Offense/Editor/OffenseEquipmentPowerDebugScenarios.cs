using System;
using UnityEditor;
using UnityEngine;

public static class OffenseEquipmentPowerDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Offense/Validate Expedition Loadout Power")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        const float characterPower = 20f;
        CombatWeaponSnapshot unarmed = CombatWeaponSnapshot.CreateUnarmed();
        CombatWeaponSnapshot earlyWeapon = CreateWeapon(
            "equipment:spear",
            damage: 11f,
            penetration: 9f,
            attackTime: 1.15f,
            CombatEquipmentQuality.Normal,
            durability: 1f);
        CombatWeaponSnapshot wornWeapon = CreateWeapon(
            "equipment:spear",
            damage: 11f,
            penetration: 9f,
            attackTime: 1.15f,
            CombatEquipmentQuality.Poor,
            durability: 0.2f);
        CombatWeaponSnapshot advancedWeapon = CreateWeapon(
            "equipment:blacksteel-poleaxe",
            damage: 19f,
            penetration: 16f,
            attackTime: 1.25f,
            CombatEquipmentQuality.Excellent,
            durability: 1f,
            materialDamage: 1.15f,
            materialPenetration: 1.2f);
        CombatWeaponSnapshot loadedBow = CreateWeapon(
            "equipment:shortbow",
            damage: 8f,
            penetration: 4f,
            attackTime: 0.9f,
            CombatEquipmentQuality.Normal,
            durability: 1f,
            requiresAmmo: true,
            loadedAmmo: 1);
        CombatWeaponSnapshot emptyBow = CreateWeapon(
            "equipment:shortbow",
            damage: 8f,
            penetration: 4f,
            attackTime: 0.9f,
            CombatEquipmentQuality.Normal,
            durability: 1f,
            requiresAmmo: true,
            loadedAmmo: 0);

        Require(
            Mathf.Approximately(
                OffenseEquipmentPowerRules.CalculateWeaponContribution(unarmed),
                0f),
            "Unarmed combat incorrectly received equipment power.");
        float earlyWeaponPower =
            OffenseEquipmentPowerRules.CalculateWeaponContribution(earlyWeapon);
        Require(earlyWeaponPower > 0f, "An equipped early weapon added no power.");
        Require(
            OffenseEquipmentPowerRules.CalculateWeaponContribution(wornWeapon)
                < earlyWeaponPower,
            "Worn low-quality equipment did not lose projected power.");
        Require(
            OffenseEquipmentPowerRules.CalculateWeaponContribution(advancedWeapon)
                > earlyWeaponPower,
            "Advanced equipment did not improve projected power.");
        Require(
            OffenseEquipmentPowerRules.CalculateWeaponContribution(emptyBow)
                < OffenseEquipmentPowerRules.CalculateWeaponContribution(loadedBow),
            "Empty ammunition did not reduce ranged readiness.");

        CombatArmorSnapshot[] earlyArmor =
        {
            new(
                "armor:torso",
                CombatBodyPart.Torso,
                CombatArmorLayer.Clothing,
                CombatEquipmentQuality.Normal,
                1f,
                7f,
                5f,
                8f),
            new(
                "armor:left-arm",
                CombatBodyPart.LeftArm,
                CombatArmorLayer.Clothing,
                CombatEquipmentQuality.Normal,
                1f,
                4f,
                3f,
                5f),
            new(
                "armor:right-arm",
                CombatBodyPart.RightArm,
                CombatArmorLayer.Clothing,
                CombatEquipmentQuality.Normal,
                1f,
                4f,
                3f,
                5f)
        };
        CombatShieldSnapshot shield = new(
            "shield:wood",
            CombatEquipmentQuality.Normal,
            1f,
            0.28f,
            0f,
            10f,
            7f,
            7f);

        float weaponOnly = OffenseEquipmentPowerRules.CalculateLoadoutContribution(
            characterPower,
            earlyWeapon,
            Array.Empty<CombatArmorSnapshot>(),
            default);
        float fullEarlyLoadout = OffenseEquipmentPowerRules.CalculateLoadoutContribution(
            characterPower,
            earlyWeapon,
            earlyArmor,
            shield);
        Require(
            fullEarlyLoadout > weaponOnly,
            "Armor and shield did not improve expedition readiness.");
        Require(
            fullEarlyLoadout <= characterPower * OffenseEquipmentPowerRules.MaximumLoadoutRatio
                + 0.0001f,
            "Loadout contribution exceeded the character-relative cap.");

        CombatArmorSnapshot[] excessiveArmor = new CombatArmorSnapshot[24];
        for (int index = 0; index < excessiveArmor.Length; index++)
        {
            excessiveArmor[index] = new CombatArmorSnapshot(
                $"armor:excessive:{index}",
                (CombatBodyPart)(index % 6),
                CombatArmorLayer.Plate,
                CombatEquipmentQuality.Legendary,
                1f,
                100f,
                100f,
                100f);
        }

        float capped = OffenseEquipmentPowerRules.CalculateLoadoutContribution(
            characterPower,
            advancedWeapon,
            excessiveArmor,
            new CombatShieldSnapshot(
                "shield:excessive",
                CombatEquipmentQuality.Legendary,
                1f,
                1f,
                0f,
                100f,
                100f,
                100f));
        Require(
            Mathf.Approximately(
                capped,
                characterPower * OffenseEquipmentPowerRules.MaximumLoadoutRatio),
            "Extreme equipment did not stop at the 60% loadout cap.");

        return "PASS: equipped weapon, armor, shield, quality, durability and ammunition readiness use bounded expedition power.";
    }

    private static CombatWeaponSnapshot CreateWeapon(
        string definitionId,
        float damage,
        float penetration,
        float attackTime,
        CombatEquipmentQuality quality,
        float durability,
        float materialDamage = 1f,
        float materialPenetration = 1f,
        bool requiresAmmo = false,
        int loadedAmmo = 0)
    {
        CombatAttackVerb verb = requiresAmmo
            ? new ProjectileVerb
            {
                attackTime = attackTime,
                baseDamage = damage,
                penetration = penetration,
                damageType = CombatDamageType.Pierce
            }
            : new MeleeStrikeVerb
            {
                attackTime = attackTime,
                baseDamage = damage,
                penetration = penetration,
                damageType = CombatDamageType.Slash
            };
        return new CombatWeaponSnapshot(
            definitionId,
            $"instance:{definitionId}:{quality}:{durability:0.00}:{loadedAmmo}",
            requiresAmmo
                ? CombatEquipmentKind.RangedWeapon
                : CombatEquipmentKind.MeleeWeapon,
            verb,
            new[]
            {
                new CombatRangeProfile
                {
                    band = requiresAmmo
                        ? CombatRangeBand.Medium
                        : CombatRangeBand.Contact,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                }
            },
            requiresAmmo ? 11 : 1,
            quality,
            requiresAmmo ? "ammo:standard" : string.Empty,
            requiresAmmo ? 1 : 0,
            loadedAmmo,
            requiresAmmo ? 1.5f : 0f,
            supportsAimed: requiresAmmo,
            supportsRapid: false,
            supportsSuppressive: false,
            materialDamageMultiplier: materialDamage,
            materialPenetrationMultiplier: materialPenetration,
            durabilityRatio: durability,
            maximumMisfireChance: 0.15f,
            gunpowderWeapon: false);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
