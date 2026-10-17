using System;
using System.Collections.Generic;

[Serializable]
public sealed class CombatArmorHitFactData
{
    public string instanceId = string.Empty;
    public float damage;

    public CombatArmorHitFactData Clone() =>
        (CombatArmorHitFactData)MemberwiseClone();

    public bool IsValid => instanceId != null && IsFiniteNonnegative(damage);

    private static bool IsFiniteNonnegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}

[Serializable]
public sealed class CombatActionEvidenceData
{
    private const CombatSpecialEffectFlags AllSpecialEffects =
        CombatSpecialEffectFlags.Burning
        | CombatSpecialEffectFlags.SmokeScreen
        | CombatSpecialEffectFlags.Scatter
        | CombatSpecialEffectFlags.SignalSupport
        | CombatSpecialEffectFlags.ConstructPiercing
        | CombatSpecialEffectFlags.RuneDamage
        | CombatSpecialEffectFlags.Tranquilized
        | CombatSpecialEffectFlags.ManaBlocked;

    public bool requestKnown;
    public string weaponDefinitionId = string.Empty;
    public string weaponInstanceId = string.Empty;
    public string ammunitionItemId = string.Empty;
    public string coverSourceId = string.Empty;
    public CombatDamageType damageType;
    public CombatFireMode fireMode;
    public int ammunitionBefore;
    public int ammunitionConsumed;
    public int ammunitionAfter;
    public bool executed;
    public bool hit;
    public bool coverBlocked;
    public bool shieldBlocked;
    public bool evaded;
    public bool nonlethal;
    public CombatSpecialEffectFlags specialEffects;
    public float statusPotency;
    public float coverDamage;
    public int statusTurns;
    public CombatBodyPart bodyPart;
    public float appliedDamage;
    public float suppression;
    public List<CombatArmorHitFactData> armorHits = new();

    public CombatActionEvidenceData Clone()
    {
        var clone = (CombatActionEvidenceData)MemberwiseClone();
        if (armorHits == null)
        {
            clone.armorHits = null;
            return clone;
        }

        clone.armorHits = new List<CombatArmorHitFactData>(armorHits.Count);
        for (int index = 0; index < armorHits.Count; index++)
            clone.armorHits.Add(armorHits[index]?.Clone());
        return clone;
    }

    public bool IsValid => weaponDefinitionId != null
        && weaponInstanceId != null
        && ammunitionItemId != null
        && coverSourceId != null
        && Enum.IsDefined(typeof(CombatDamageType), damageType)
        && Enum.IsDefined(typeof(CombatFireMode), fireMode)
        && Enum.IsDefined(typeof(CombatBodyPart), bodyPart)
        && (specialEffects & ~AllSpecialEffects) == 0
        && ammunitionBefore >= 0
        && ammunitionConsumed >= 0
        && ammunitionConsumed <= ammunitionBefore
        && ammunitionAfter == ammunitionBefore - ammunitionConsumed
        && IsFiniteNonnegative(statusPotency)
        && IsFiniteNonnegative(coverDamage)
        && statusTurns >= 0
        && IsFiniteNonnegative(appliedDamage)
        && IsFiniteNonnegative(suppression)
        && ArmorHitsAreValid();

    public static CombatActionEvidenceData Capture(
        in CombatAttackRequest request,
        in CombatAttackResult result,
        CombatDamageType damageType)
    {
        CombatWeaponSnapshot weapon = request.Weapon;
        int ammunitionBefore = weapon?.LoadedAmmo ?? 0;
        int ammunitionConsumed = result.AmmunitionConsumed;
        string ammunitionItemId = !string.IsNullOrWhiteSpace(
                result.AmmunitionItemId)
            ? result.AmmunitionItemId
            : weapon?.AmmunitionItemId;
        return RequireValid(new CombatActionEvidenceData
        {
            requestKnown = true,
            weaponDefinitionId = weapon?.DefinitionId ?? string.Empty,
            weaponInstanceId = weapon?.InstanceId ?? string.Empty,
            ammunitionItemId = ammunitionItemId ?? string.Empty,
            coverSourceId = result.CoverSourceId ?? string.Empty,
            damageType = damageType,
            fireMode = request.FireMode,
            ammunitionBefore = ammunitionBefore,
            ammunitionConsumed = ammunitionConsumed,
            ammunitionAfter = ammunitionBefore - ammunitionConsumed,
            executed = result.Executed,
            hit = result.Hit,
            coverBlocked = result.CoverBlocked,
            shieldBlocked = result.ShieldBlocked,
            evaded = result.Evaded,
            nonlethal = result.Nonlethal,
            specialEffects = result.SpecialEffects,
            statusPotency = result.StatusPotency,
            coverDamage = result.CoverDamage,
            statusTurns = result.StatusTurns,
            bodyPart = result.BodyPart,
            appliedDamage = result.AppliedDamage,
            suppression = result.Suppression,
            armorHits = CaptureArmorHits(result.ArmorDurabilityHits)
        });
    }

    public static CombatActionEvidenceData CaptureUnknownRequest(
        in CombatAttackResult result,
        CombatDamageType damageType)
    {
        int ammunitionConsumed = result.AmmunitionConsumed;
        return RequireValid(new CombatActionEvidenceData
        {
            requestKnown = false,
            weaponDefinitionId = string.Empty,
            weaponInstanceId = string.Empty,
            ammunitionItemId = result.AmmunitionItemId ?? string.Empty,
            coverSourceId = result.CoverSourceId ?? string.Empty,
            damageType = damageType,
            fireMode = default,
            ammunitionBefore = ammunitionConsumed,
            ammunitionConsumed = ammunitionConsumed,
            ammunitionAfter = 0,
            executed = result.Executed,
            hit = result.Hit,
            coverBlocked = result.CoverBlocked,
            shieldBlocked = result.ShieldBlocked,
            evaded = result.Evaded,
            nonlethal = result.Nonlethal,
            specialEffects = result.SpecialEffects,
            statusPotency = result.StatusPotency,
            coverDamage = result.CoverDamage,
            statusTurns = result.StatusTurns,
            bodyPart = result.BodyPart,
            appliedDamage = result.AppliedDamage,
            suppression = result.Suppression,
            armorHits = CaptureArmorHits(result.ArmorDurabilityHits)
        });
    }

    private bool ArmorHitsAreValid()
    {
        if (armorHits == null)
            return false;
        for (int index = 0; index < armorHits.Count; index++)
        {
            if (armorHits[index] == null || !armorHits[index].IsValid)
                return false;
        }
        return true;
    }

    private static List<CombatArmorHitFactData> CaptureArmorHits(
        IReadOnlyList<CombatArmorDurabilityHit> values)
    {
        if (values == null || values.Count == 0)
            return new List<CombatArmorHitFactData>();
        var captured = new List<CombatArmorHitFactData>(values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            captured.Add(new CombatArmorHitFactData
            {
                instanceId = values[index].InstanceId ?? string.Empty,
                damage = values[index].Damage
            });
        }
        return captured;
    }

    private static CombatActionEvidenceData RequireValid(
        CombatActionEvidenceData value)
    {
        if (!value.IsValid)
            throw new ArgumentException("combat-action-evidence-invalid");
        return value;
    }

    private static bool IsFiniteNonnegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}
