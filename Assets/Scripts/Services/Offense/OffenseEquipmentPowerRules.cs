using System;
using System.Collections.Generic;
using UnityEngine;

public static class OffenseEquipmentPowerRules
{
    public const float MaximumLoadoutRatio = 0.60f;
    public const float MaximumWeaponRatio = 0.35f;
    public const float MaximumArmorRatio = 0.30f;
    public const float MaximumShieldRatio = 0.15f;
    public const float EmptyAmmunitionReadiness = 0.50f;

    private const float UnarmedDamagePerSecond = 4f / 1.05f;

    public static float CalculateLoadoutContribution(
        float characterPower,
        CombatWeaponSnapshot weapon,
        IReadOnlyList<CombatArmorSnapshot> armor,
        CombatShieldSnapshot shield)
    {
        float normalizedCharacterPower = Mathf.Max(0f, characterPower);
        if (normalizedCharacterPower <= 0f)
        {
            return 0f;
        }

        float weaponPower = Mathf.Min(
            CalculateWeaponContribution(weapon),
            normalizedCharacterPower * MaximumWeaponRatio);
        float armorPower = Mathf.Min(
            CalculateArmorContribution(armor),
            normalizedCharacterPower * MaximumArmorRatio);
        float shieldPower = Mathf.Min(
            CalculateShieldContribution(shield),
            normalizedCharacterPower * MaximumShieldRatio);

        return Mathf.Min(
            weaponPower + armorPower + shieldPower,
            normalizedCharacterPower * MaximumLoadoutRatio);
    }

    public static float CalculateWeaponContribution(CombatWeaponSnapshot weapon)
    {
        if (weapon == null
            || weapon.Verb == null
            || string.Equals(
                weapon.DefinitionId,
                "combat:unarmed",
                StringComparison.Ordinal))
        {
            return 0f;
        }

        float quality = CombatQualityRules.GetMultiplier(weapon.Quality);
        float durability = Mathf.Lerp(0.35f, 1f, weapon.DurabilityRatio);
        float rangeEfficiency = CalculateBestRangeEfficiency(weapon);
        float cycleSeconds = Mathf.Max(0.1f, weapon.Verb.attackTime);
        if (weapon.RequiresAmmo && weapon.MagazineCapacity > 0)
        {
            cycleSeconds += weapon.ReloadSeconds / weapon.MagazineCapacity;
        }

        float damagePerSecond = weapon.Verb.baseDamage
            * weapon.MaterialDamageMultiplier
            * quality
            * durability
            * rangeEfficiency
            / cycleSeconds;
        float penetration = weapon.Verb.penetration
            * weapon.MaterialPenetrationMultiplier
            * quality
            * durability;
        float readiness = weapon.RequiresAmmo && weapon.LoadedAmmo <= 0
            ? EmptyAmmunitionReadiness
            : 1f;
        float reliability = 1f - weapon.MisfireChance;

        return Mathf.Max(
            0f,
            ((damagePerSecond - UnarmedDamagePerSecond) * 0.45f
                + penetration * 0.08f)
            * readiness
            * reliability);
    }

    public static float CalculateArmorContribution(
        IReadOnlyList<CombatArmorSnapshot> armor)
    {
        if (armor == null || armor.Count == 0)
        {
            return 0f;
        }

        float head = 0f;
        float torso = 0f;
        float leftArm = 0f;
        float rightArm = 0f;
        float leftLeg = 0f;
        float rightLeg = 0f;

        for (int index = 0; index < armor.Count; index++)
        {
            CombatArmorSnapshot piece = armor[index];
            float defense = AverageDefense(piece);
            switch (piece.BodyPart)
            {
                case CombatBodyPart.Head:
                    head += defense;
                    break;
                case CombatBodyPart.Torso:
                    torso += defense;
                    break;
                case CombatBodyPart.LeftArm:
                    leftArm += defense;
                    break;
                case CombatBodyPart.RightArm:
                    rightArm += defense;
                    break;
                case CombatBodyPart.LeftLeg:
                    leftLeg += defense;
                    break;
                case CombatBodyPart.RightLeg:
                    rightLeg += defense;
                    break;
            }
        }

        float coverageWeightedDefense =
            head * 0.15f
            + torso * 0.35f
            + leftArm * 0.125f
            + rightArm * 0.125f
            + leftLeg * 0.125f
            + rightLeg * 0.125f;
        return Mathf.Max(0f, coverageWeightedDefense * 0.22f);
    }

    public static float CalculateShieldContribution(CombatShieldSnapshot shield)
    {
        if (!shield.IsValid)
        {
            return 0f;
        }

        float blockChance = shield.GetBlockChance();
        float averageDefense = (
            shield.GetDefense(CombatDamageType.Slash)
            + shield.GetDefense(CombatDamageType.Pierce)
            + shield.GetDefense(CombatDamageType.Blunt)) / 3f;
        return Mathf.Max(
            0f,
            averageDefense * blockChance * 0.45f + blockChance * 2f);
    }

    private static float CalculateBestRangeEfficiency(CombatWeaponSnapshot weapon)
    {
        float best = 0f;
        IReadOnlyList<CombatRangeProfile> ranges = weapon.Ranges;
        for (int index = 0; index < ranges.Count; index++)
        {
            CombatRangeProfile profile = ranges[index];
            if (profile == null)
            {
                continue;
            }

            float efficiency = Mathf.Max(0f, profile.accuracyMultiplier)
                * Mathf.Max(0f, profile.damageMultiplier)
                * weapon.EvolutionAccuracyMultiplier;
            best = Mathf.Max(best, efficiency);
        }

        return Mathf.Clamp(best, 0.25f, 1.50f);
    }

    private static float AverageDefense(CombatArmorSnapshot armor)
    {
        return (
            armor.GetDefense(CombatDamageType.Slash)
            + armor.GetDefense(CombatDamageType.Pierce)
            + armor.GetDefense(CombatDamageType.Blunt)) / 3f;
    }
}
