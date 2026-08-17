#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatBalanceCheckpoint
{
    public CombatBalanceCheckpoint(
        int campaign,
        int day,
        int totalMinimum,
        int totalMaximum,
        int workingMinimum,
        int workingMaximum,
        int dependentMinimum,
        int dependentMaximum,
        int combatReadyMinimum,
        int combatReadyMaximum,
        string targetId,
        string weaponId,
        string rangedWeaponId,
        string armorId,
        string shieldId,
        CombatEquipmentQuality quality)
    {
        Campaign = campaign;
        Day = day;
        TotalMinimum = totalMinimum;
        TotalMaximum = totalMaximum;
        WorkingMinimum = workingMinimum;
        WorkingMaximum = workingMaximum;
        DependentMinimum = dependentMinimum;
        DependentMaximum = dependentMaximum;
        CombatReadyMinimum = combatReadyMinimum;
        CombatReadyMaximum = combatReadyMaximum;
        TargetId = targetId ?? string.Empty;
        WeaponId = weaponId ?? string.Empty;
        RangedWeaponId = rangedWeaponId ?? string.Empty;
        ArmorId = armorId ?? string.Empty;
        ShieldId = shieldId ?? string.Empty;
        Quality = quality;
    }

    public int Campaign { get; }
    public int Day { get; }
    public int TotalMinimum { get; }
    public int TotalMaximum { get; }
    public int WorkingMinimum { get; }
    public int WorkingMaximum { get; }
    public int DependentMinimum { get; }
    public int DependentMaximum { get; }
    public int CombatReadyMinimum { get; }
    public int CombatReadyMaximum { get; }
    public string TargetId { get; }
    public string WeaponId { get; }
    public string RangedWeaponId { get; }
    public string ArmorId { get; }
    public string ShieldId { get; }
    public CombatEquipmentQuality Quality { get; }
}

public readonly struct CombatEncounterCalibration
{
    public CombatEncounterCalibration(
        int encounterNumber,
        float enemyHealthMultiplier,
        float enemyDamageMultiplier,
        float enemyAccuracyMultiplier,
        float objectiveHealthMultiplier,
        float objectiveControlResistanceMultiplier,
        int additionalEnemyCount,
        int objectiveRoundLimit)
    {
        EncounterNumber = encounterNumber;
        EnemyHealthMultiplier = enemyHealthMultiplier;
        EnemyDamageMultiplier = enemyDamageMultiplier;
        EnemyAccuracyMultiplier = enemyAccuracyMultiplier;
        ObjectiveHealthMultiplier = objectiveHealthMultiplier;
        ObjectiveControlResistanceMultiplier = objectiveControlResistanceMultiplier;
        AdditionalEnemyCount = additionalEnemyCount;
        ObjectiveRoundLimit = objectiveRoundLimit;
    }

    public int EncounterNumber { get; }
    public string EncounterId => $"encounter:{EncounterNumber:00}";
    public float EnemyHealthMultiplier { get; }
    public float EnemyDamageMultiplier { get; }
    public float EnemyAccuracyMultiplier { get; }
    public float ObjectiveHealthMultiplier { get; }
    public float ObjectiveControlResistanceMultiplier { get; }
    public int AdditionalEnemyCount { get; }
    public int ObjectiveRoundLimit { get; }
}

/// <summary>
/// Single editor authority for the population/proficiency/loadout checkpoints
/// used by both readiness and multi-seed combat calibration. The authored
/// campaign requiredPower is deliberately not an input to member projection.
/// </summary>
public static class CombatBalanceCheckpointAuthority
{
    private static readonly CombatBalanceCheckpoint[] checkpoints =
    {
        Point(1, 1, 3, 3, 3, 3, 0, 0, 2, 2,
            "food_farm", "weapon:spear", "weapon:shortbow",
            "armor:cloth-hood", string.Empty,
            CombatEquipmentQuality.Normal),
        Point(2, 30, 3, 6, 3, 6, 0, 2, 2, 4,
            "merchant_road", "weapon:falchion", "weapon:crossbow",
            "armor:leather", "shield:wood",
            CombatEquipmentQuality.Normal),
        Point(3, 120, 6, 14, 5, 12, 1, 4, 3, 7,
            "old_armory", "weapon:mace", "weapon:windlass-crossbow",
            "armor:mail-shirt", "shield:wood",
            CombatEquipmentQuality.Normal),
        Point(4, 240, 12, 28, 8, 20, 4, 12, 5, 12,
            "mana_ruins", "weapon:estoc", "weapon:arquebus",
            "armor:articulated-plate", "shield:iron",
            CombatEquipmentQuality.Normal),
        Point(5, 400, 25, 60, 15, 40, 10, 25, 10, 24,
            "rival_dungeon", "weapon:powered-striking-gauntlet",
            "weapon:repeating-crossbow",
            "armor:powered-harness", "shield:powered",
            CombatEquipmentQuality.Normal),
        Point(6, 960, 80, 220, 55, 160, 25, 70, 25, 70,
            "truth_core", "weapon:rune-blade", "weapon:rune-bow",
            "armor:rune-ward-mail", "shield:rune",
            CombatEquipmentQuality.Good)
    };

    private static readonly CombatEncounterCalibration[] encounterCalibrations =
    {
        Encounter(1, 1f, 1f, 1f, 1f, 1f, 0, 0),
        Encounter(2, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(3, 1f, 1f, 1f, 2.6f, 1f, 0, 6),
        Encounter(4, 0.122f, 1f, 1f, 0.8f, 1f, 0, 9),
        Encounter(5, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(6, 2.53f, 1f, 1f, 1f, 1.5f, 0, 6),
        Encounter(7, 1f, 1f, 1f, 1f, 1f, 0, 0),
        Encounter(8, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(9, 1f, 1f, 1f, 1f, 1f, 0, 7),
        Encounter(10, 0.503f, 1f, 1f, 0.25f, 1f, 0, 7),
        Encounter(11, 1f, 0.632f, 1f, 1f, 1f, 0, 7),
        Encounter(12, 0.224f, 0.2f, 1f, 1f, 0.25f, 0, 8),
        Encounter(13, 1f, 1f, 1f, 1f, 1f, 0, 0),
        Encounter(14, 1f, 0.632f, 1f, 1f, 1f, 0, 8),
        Encounter(15, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(16, 0.411f, 1f, 1f, 0.95f, 1f, 0, 7),
        Encounter(17, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(18, 1f, 1f, 1f, 1f, 1.2f, 0, 6),
        Encounter(19, 1f, 1f, 1f, 1f, 1f, 0, 0),
        Encounter(20, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(21, 2.53f, 2f, 1f, 1f, 1f, 0, 7),
        Encounter(22, 1.45f, 0.8f, 1f, 1f, 1f, 0, 7),
        Encounter(23, 1f, 1f, 1f, 1f, 1f, 0, 7),
        Encounter(24, 1f, 1f, 1f, 1f, 1f, 0, 6),
        Encounter(25, 2.53f, 4f, 1f, 1f, 1f, 0, 0),
        Encounter(26, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(27, 2f, 2f, 1f, 1f, 1f, 0, 7),
        Encounter(28, 3.2f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(29, 1f, 1f, 1f, 1f, 1f, 0, 7),
        Encounter(30, 2.53f, 1f, 1f, 1f, 2f, 0, 7),
        Encounter(31, 1f, 1f, 1f, 1f, 1f, 0, 0),
        Encounter(32, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(33, 1.1f, 7f, 8f, 1f, 1f, 0, 8),
        Encounter(34, 3.5f, 1f, 1f, 1f, 1f, 0, 7),
        Encounter(35, 1f, 1f, 1f, 1f, 1f, 0, 8),
        Encounter(36, 2.53f, 1f, 1f, 1f, 0.5f, 0, 6)
    };

    public static IReadOnlyList<CombatBalanceCheckpoint> All => checkpoints;
    public static IReadOnlyList<CombatEncounterCalibration> AllEncounters =>
        encounterCalibrations;

    public static CombatBalanceCheckpoint RequireCampaign(int campaign)
    {
        if (campaign < 1 || campaign > checkpoints.Length)
        {
            throw new InvalidOperationException(
                $"Combat checkpoint campaign '{campaign}' is outside 1-{checkpoints.Length}.");
        }

        return checkpoints[campaign - 1];
    }

    public static CombatEncounterCalibration RequireEncounter(int encounterNumber)
    {
        if (encounterNumber < 1 || encounterNumber > encounterCalibrations.Length)
        {
            throw new InvalidOperationException(
                $"Combat encounter calibration '{encounterNumber}' is outside 1-{encounterCalibrations.Length}.");
        }

        CombatEncounterCalibration value = encounterCalibrations[encounterNumber - 1];
        if (value.EncounterNumber != encounterNumber)
        {
            throw new InvalidOperationException(
                $"Combat encounter calibration index drift at '{encounterNumber}'.");
        }
        return value;
    }

    public static float CalculateProjectedBasePower(int day)
    {
        long Milli(float experience) => checked((long)Math.Round(
            Math.Max(0f, experience) * 1000f,
            MidpointRounding.AwayFromZero));
        float fieldExperience = 30f + day * 1.20f;
        float constructionExperience = 30f + day * 0.30f;
        float foodExperience = 30f + day * 0.30f;
        float meleeExperience = 30f + day * 2.00f;
        float rangedExperience = 30f + day * 0.25f;

        return OffenseExpeditionService.CalculateProjectedProficiencyPower(id =>
        {
            if (id == BuiltInCharacterProficiencyIds.Fieldwork)
            {
                return Milli(fieldExperience);
            }
            if (id == BuiltInCharacterProficiencyIds.ConstructionEngineering)
            {
                return Milli(constructionExperience);
            }
            if (id == BuiltInCharacterProficiencyIds.FoodProduction)
            {
                return Milli(foodExperience);
            }
            if (id == BuiltInCharacterProficiencyIds.MeleeCombat)
            {
                return Milli(meleeExperience);
            }
            if (id == BuiltInCharacterProficiencyIds.RangedCombat)
            {
                return Milli(rangedExperience);
            }
            return Milli(30f);
        });
    }

    private static CombatBalanceCheckpoint Point(
        int campaign,
        int day,
        int totalMin,
        int totalMax,
        int workingMin,
        int workingMax,
        int dependentMin,
        int dependentMax,
        int combatMin,
        int combatMax,
        string targetId,
        string weaponId,
        string rangedWeaponId,
        string armorId,
        string shieldId,
        CombatEquipmentQuality quality) => new(
            campaign,
            day,
            totalMin,
            totalMax,
            workingMin,
            workingMax,
            dependentMin,
            dependentMax,
            combatMin,
            combatMax,
            targetId,
            weaponId,
            rangedWeaponId,
            armorId,
            shieldId,
            quality);

    private static CombatEncounterCalibration Encounter(
        int encounterNumber,
        float enemyHealthMultiplier,
        float enemyDamageMultiplier,
        float enemyAccuracyMultiplier,
        float objectiveHealthMultiplier,
        float objectiveControlResistanceMultiplier,
        int additionalEnemyCount,
        int objectiveRoundLimit) => new(
            encounterNumber,
            enemyHealthMultiplier,
            enemyDamageMultiplier,
            enemyAccuracyMultiplier,
            objectiveHealthMultiplier,
            objectiveControlResistanceMultiplier,
            additionalEnemyCount,
            objectiveRoundLimit);
}
#endif
