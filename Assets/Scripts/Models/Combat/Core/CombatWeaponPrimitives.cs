using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class CombatAttackVerb
{
    [Min(0.1f)] public float attackTime = 1f;
    [Min(0f)] public float baseDamage = 10f;
    [Min(0f)] public float penetration = 5f;
    public CombatDamageType damageType = CombatDamageType.Slash;
    [Range(0f, 1f)] public float tracking = 0.05f;

    public abstract CombatEquipmentKind Kind { get; }
    public virtual bool ConsumesAmmo => false;
    public virtual bool DropsWeaponOnUse => false;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MeleeStrikeVerb : CombatAttackVerb
{
    public override CombatEquipmentKind Kind =>
        CombatEquipmentKind.MeleeWeapon;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProjectileVerb : CombatAttackVerb
{
    [Min(1f)] public float projectileSpeed = 12f;
    public override CombatEquipmentKind Kind =>
        CombatEquipmentKind.RangedWeapon;
    public override bool ConsumesAmmo => true;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RecoverableThrowVerb : CombatAttackVerb
{
    [Min(1f)] public float projectileSpeed = 9f;
    public override CombatEquipmentKind Kind =>
        CombatEquipmentKind.RecoverableThrowingWeapon;
    public override bool DropsWeaponOnUse => true;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatWeaponSnapshot
{
    public CombatWeaponSnapshot(
        string definitionId,
        string instanceId,
        CombatEquipmentKind kind,
        CombatAttackVerb verb,
        IReadOnlyList<CombatRangeProfile> ranges,
        int maximumRange,
        CombatEquipmentQuality quality,
        string ammunitionItemId,
        int magazineCapacity,
        int loadedAmmo,
        float reloadSeconds,
        bool supportsAimed,
        bool supportsRapid,
        bool supportsSuppressive,
        float materialDamageMultiplier = 1f,
        float materialPenetrationMultiplier = 1f,
        float evolutionAccuracyMultiplier = 1f,
        bool gunpowderWeapon = false,
        float durabilityRatio = 1f,
        float maximumMisfireChance = 0f,
        float smokeExposure = 0f,
        CombatEquipmentRoleFlags roleFlags = CombatEquipmentRoleFlags.None)
    {
        DefinitionId = definitionId ?? string.Empty;
        InstanceId = instanceId ?? string.Empty;
        Kind = kind;
        Verb = verb;
        Ranges = ranges ?? Array.Empty<CombatRangeProfile>();
        MaximumRange = Mathf.Max(1, maximumRange);
        Quality = quality;
        AmmunitionItemId = ammunitionItemId ?? string.Empty;
        MagazineCapacity = Mathf.Max(0, magazineCapacity);
        LoadedAmmo = Mathf.Clamp(loadedAmmo, 0, MagazineCapacity);
        ReloadSeconds = Mathf.Max(0f, reloadSeconds);
        SupportsAimed = supportsAimed;
        SupportsRapid = supportsRapid;
        SupportsSuppressive = supportsSuppressive;
        MaterialDamageMultiplier = Mathf.Max(0.01f, materialDamageMultiplier);
        MaterialPenetrationMultiplier =
            Mathf.Max(0.01f, materialPenetrationMultiplier);
        EvolutionAccuracyMultiplier =
            Mathf.Max(0.01f, evolutionAccuracyMultiplier);
        GunpowderWeapon = gunpowderWeapon;
        DurabilityRatio = Mathf.Clamp01(durabilityRatio);
        MaximumMisfireChance = Mathf.Clamp01(maximumMisfireChance);
        SmokeExposure = Mathf.Max(0f, smokeExposure);
        RoleFlags = roleFlags;
    }

    public string DefinitionId { get; }
    public string InstanceId { get; }
    public CombatEquipmentKind Kind { get; }
    public CombatAttackVerb Verb { get; }
    public IReadOnlyList<CombatRangeProfile> Ranges { get; }
    public int MaximumRange { get; }
    public CombatEquipmentQuality Quality { get; }
    public string AmmunitionItemId { get; }
    public int MagazineCapacity { get; }
    public int LoadedAmmo { get; }
    public float ReloadSeconds { get; }
    public bool SupportsAimed { get; }
    public bool SupportsRapid { get; }
    public bool SupportsSuppressive { get; }
    public float MaterialDamageMultiplier { get; }
    public float MaterialPenetrationMultiplier { get; }
    public float EvolutionAccuracyMultiplier { get; }
    public bool GunpowderWeapon { get; }
    public float DurabilityRatio { get; }
    public float MaximumMisfireChance { get; }
    public float SmokeExposure { get; }
    public CombatEquipmentRoleFlags RoleFlags { get; }

    public float MisfireChance => !GunpowderWeapon || DurabilityRatio >= 0.4f
        ? 0f
        : MaximumMisfireChance * (1f - DurabilityRatio / 0.4f);

    public bool IsRanged =>
        Kind == CombatEquipmentKind.RangedWeapon
        || Kind == CombatEquipmentKind.RecoverableThrowingWeapon;

    public bool RequiresAmmo => Verb != null && Verb.ConsumesAmmo;

    public float GetAccuracyMultiplier(CombatRangeBand band)
    {
        return Ranges.FirstOrDefault(
            item => item != null && item.band == band)
            ?.accuracyMultiplier * EvolutionAccuracyMultiplier ?? 0f;
    }

    public float GetDamageMultiplier(CombatRangeBand band)
    {
        return Ranges.FirstOrDefault(
            item => item != null && item.band == band)
            ?.damageMultiplier ?? 0f;
    }

    public static CombatWeaponSnapshot CreateUnarmed()
    {
        return new CombatWeaponSnapshot(
            "combat:unarmed",
            string.Empty,
            CombatEquipmentKind.MeleeWeapon,
            new MeleeStrikeVerb
            {
                attackTime = 1.05f,
                baseDamage = 4f,
                penetration = 0f,
                damageType = CombatDamageType.Blunt,
                tracking = 0.08f
            },
            new[]
            {
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Contact,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                }
            },
            1,
            CombatEquipmentQuality.Normal,
            string.Empty,
            0,
            0,
            0f,
            false,
            false,
            false);
    }
}

public readonly struct CombatAmmunitionProfile
{
    public CombatAmmunitionProfile(
        CombatSpecialEffectFlags effects,
        float damageMultiplier = 1f,
        float penetrationMultiplier = 1f,
        float suppressionMultiplier = 1f,
        float statusPotency = 0f,
        int statusTurns = 0,
        bool nonlethal = false,
        float targetAirborneExposure = 0f)
    {
        Effects = effects;
        DamageMultiplier = Mathf.Max(0f, damageMultiplier);
        PenetrationMultiplier = Mathf.Max(0f, penetrationMultiplier);
        SuppressionMultiplier = Mathf.Max(0f, suppressionMultiplier);
        StatusPotency = Mathf.Max(0f, statusPotency);
        StatusTurns = Mathf.Max(0, statusTurns);
        Nonlethal = nonlethal;
        TargetAirborneExposure = Mathf.Max(0f, targetAirborneExposure);
    }

    public CombatSpecialEffectFlags Effects { get; }
    public float DamageMultiplier { get; }
    public float PenetrationMultiplier { get; }
    public float SuppressionMultiplier { get; }
    public float StatusPotency { get; }
    public int StatusTurns { get; }
    public bool Nonlethal { get; }
    public float TargetAirborneExposure { get; }

    public static CombatAmmunitionProfile For(string ammunitionItemId) =>
        (ammunitionItemId?.Trim() ?? string.Empty) switch
        {
            "ammo:incendiary-arrow" => new(
                CombatSpecialEffectFlags.Burning,
                damageMultiplier: 1.05f,
                statusPotency: 2.5f,
                statusTurns: 3),
            "ammo:incendiary-bolt" => new(
                CombatSpecialEffectFlags.Burning,
                damageMultiplier: 1.08f,
                penetrationMultiplier: 1.1f,
                statusPotency: 3f,
                statusTurns: 3),
            "ammo:smoke-cartridge" => new(
                CombatSpecialEffectFlags.SmokeScreen,
                damageMultiplier: 0.2f,
                suppressionMultiplier: 1.8f,
                statusPotency: 20f,
                statusTurns: 2,
                targetAirborneExposure: 20f),
            "ammo:armor-piercing-cartridge" => new(
                CombatSpecialEffectFlags.None,
                damageMultiplier: 0.9f,
                penetrationMultiplier: 1.65f),
            "ammo:scatter-cartridge" => new(
                CombatSpecialEffectFlags.Scatter,
                suppressionMultiplier: 1.5f),
            "ammo:signal-flare" => new(
                CombatSpecialEffectFlags.SignalSupport,
                damageMultiplier: 0.35f,
                statusPotency: 0.12f,
                statusTurns: 2),
            "ammo:blacksteel-bolt" => new(
                CombatSpecialEffectFlags.ConstructPiercing,
                penetrationMultiplier: 1.4f),
            "ammo:rune-cartridge" => new(
                CombatSpecialEffectFlags.RuneDamage,
                damageMultiplier: 1.2f,
                penetrationMultiplier: 1.15f),
            "ammo:tranquilizer-dart" => new(
                CombatSpecialEffectFlags.Tranquilized,
                damageMultiplier: 0.25f,
                suppressionMultiplier: 2.25f,
                statusPotency: 0.35f,
                statusTurns: 3,
                nonlethal: true),
            "ammo:mana-disruptor-bolt" => new(
                CombatSpecialEffectFlags.ManaBlocked,
                damageMultiplier: 0.65f,
                statusPotency: 0.25f,
                statusTurns: 2),
            _ => new CombatAmmunitionProfile(CombatSpecialEffectFlags.None)
        };

    public float DamageMultiplierFor(
        CombatRangeBand band,
        bool defenderConstruct)
    {
        float multiplier = DamageMultiplier;
        if ((Effects & CombatSpecialEffectFlags.Scatter) != 0)
        {
            multiplier *= band switch
            {
                CombatRangeBand.Contact => 1.35f,
                CombatRangeBand.Near => 1.25f,
                CombatRangeBand.Medium => 0.8f,
                _ => 0.45f
            };
        }
        if (defenderConstruct
            && (Effects & CombatSpecialEffectFlags.ConstructPiercing) != 0)
        {
            multiplier *= 1.35f;
        }
        return multiplier;
    }

    public int PelletHitsFor(CombatRangeBand band) =>
        (Effects & CombatSpecialEffectFlags.Scatter) == 0
            ? 1
            : band switch
            {
                CombatRangeBand.Contact => 5,
                CombatRangeBand.Near => 4,
                CombatRangeBand.Medium => 2,
                _ => 1
            };
}
