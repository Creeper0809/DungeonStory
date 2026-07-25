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
        bool supportsSuppressive)
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

    public bool IsRanged =>
        Kind == CombatEquipmentKind.RangedWeapon
        || Kind == CombatEquipmentKind.RecoverableThrowingWeapon;

    public bool RequiresAmmo => Verb != null && Verb.ConsumesAmmo;

    public float GetAccuracyMultiplier(CombatRangeBand band)
    {
        return Ranges.FirstOrDefault(
            item => item != null && item.band == band)
            ?.accuracyMultiplier ?? 0f;
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
