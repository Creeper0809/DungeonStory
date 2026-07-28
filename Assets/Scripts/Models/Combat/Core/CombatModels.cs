using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatEquipmentKind
{
    MeleeWeapon,
    RangedWeapon,
    RecoverableThrowingWeapon,
    Armor,
    Shield
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatDamageType
{
    Slash,
    Pierce,
    Blunt
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatRangeBand
{
    Contact,
    Near,
    Medium,
    Long,
    OutOfRange
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatFireMode
{
    Aimed,
    Rapid,
    Suppressive
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatEquipmentQuality
{
    Awful,
    Poor,
    Normal,
    Good,
    Excellent,
    Masterwork,
    Legendary
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatBodyPart
{
    Head,
    Torso,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatArmorLayer
{
    Skin,
    Clothing,
    Mail,
    Plate,
    Outer
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatCoverHeight
{
    None,
    Low,
    High
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatEquipmentWorldState
{
    Stored,
    Carried,
    Equipped,
    Loose,
    ExpeditionPacked,
    MaintenanceBuffer,
    Lost
}

public enum CombatEquipmentLoadoutSlot
{
    Weapon = 0,
    Armor = 1
}

public static class CombatRangeRules
{
    public static CombatRangeBand GetBand(int distance)
    {
        if (distance <= 1) return CombatRangeBand.Contact;
        if (distance <= 5) return CombatRangeBand.Near;
        if (distance <= 11) return CombatRangeBand.Medium;
        if (distance <= 18) return CombatRangeBand.Long;
        return CombatRangeBand.OutOfRange;
    }
}

public static class CombatQualityRules
{
    public static float GetMultiplier(CombatEquipmentQuality quality)
    {
        return quality switch
        {
            CombatEquipmentQuality.Awful => 0.8f,
            CombatEquipmentQuality.Poor => 0.9f,
            CombatEquipmentQuality.Good => 1.1f,
            CombatEquipmentQuality.Excellent => 1.2f,
            CombatEquipmentQuality.Masterwork => 1.32f,
            CombatEquipmentQuality.Legendary => 1.48f,
            _ => 1f
        };
    }

    public static string GetDisplayName(CombatEquipmentQuality quality)
    {
        return quality switch
        {
            CombatEquipmentQuality.Awful => "조악",
            CombatEquipmentQuality.Poor => "형편없음",
            CombatEquipmentQuality.Good => "좋음",
            CombatEquipmentQuality.Excellent => "훌륭",
            CombatEquipmentQuality.Masterwork => "걸작",
            CombatEquipmentQuality.Legendary => "전설",
            _ => "보통"
        };
    }
}

public static class CombatBodyPartRules
{
    public static string GetDisplayName(CombatBodyPart bodyPart)
    {
        return bodyPart switch
        {
            CombatBodyPart.Head => "머리",
            CombatBodyPart.Torso => "몸통",
            CombatBodyPart.LeftArm => "왼팔",
            CombatBodyPart.RightArm => "오른팔",
            CombatBodyPart.LeftLeg => "왼다리",
            CombatBodyPart.RightLeg => "오른다리",
            _ => "몸통"
        };
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatRangeProfile
{
    public CombatRangeBand band;
    [Range(0f, 2f)] public float accuracyMultiplier = 1f;
    [Range(0f, 2f)] public float damageMultiplier = 1f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatArmorPartValue
{
    public CombatBodyPart bodyPart;
    [Min(0f)] public float slashDefense;
    [Min(0f)] public float pierceDefense;
    [Min(0f)] public float bluntDefense;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatEquipmentInstance
{
    public string instanceId = string.Empty;
    public string definitionId = string.Empty;
    public string materialId = string.Empty;
    public CombatEquipmentQuality quality = CombatEquipmentQuality.Normal;
    [Range(0f, 1f)] public float durabilityRatio = 1f;
    [Min(0)] public int loadedAmmo;
    public CombatEquipmentWorldState worldState = CombatEquipmentWorldState.Stored;
    public string ownerCharacterId = string.Empty;
    public string sourceStackId = string.Empty;
    public EquipmentEvolutionState evolution = new EquipmentEvolutionState();

    public CombatEquipmentInstance Clone()
    {
        return new CombatEquipmentInstance
        {
            instanceId = instanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            materialId = materialId ?? string.Empty,
            quality = quality,
            durabilityRatio = durabilityRatio,
            loadedAmmo = loadedAmmo,
            worldState = worldState,
            ownerCharacterId = ownerCharacterId ?? string.Empty,
            sourceStackId = sourceStackId ?? string.Empty,
            evolution = evolution?.Clone() ?? new EquipmentEvolutionState()
        };
    }
}

public readonly struct CombatEquipmentDerivedStats
{
    public CombatEquipmentDerivedStats(
        string definitionId,
        string materialId,
        string displayName,
        float weight,
        float maxDurability,
        float damageMultiplier,
        float penetrationDefenseMultiplier,
        float valueMultiplier,
        Color tint)
    {
        DefinitionId = definitionId ?? string.Empty;
        MaterialId = materialId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Weight = Mathf.Max(0f, weight);
        MaxDurability = Mathf.Max(1f, maxDurability);
        DamageMultiplier = Mathf.Max(0.01f, damageMultiplier);
        PenetrationDefenseMultiplier =
            Mathf.Max(0.01f, penetrationDefenseMultiplier);
        ValueMultiplier = Mathf.Max(0.01f, valueMultiplier);
        Tint = tint;
    }

    public string DefinitionId { get; }
    public string MaterialId { get; }
    public string DisplayName { get; }
    public float Weight { get; }
    public float MaxDurability { get; }
    public float DamageMultiplier { get; }
    public float PenetrationDefenseMultiplier { get; }
    public float ValueMultiplier { get; }
    public Color Tint { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatLoadoutProfile
{
    public string profileId = string.Empty;
    public string displayName = string.Empty;
    public List<string> weaponInstanceIds = new List<string>();
    public List<string> armorInstanceIds = new List<string>();
    public List<string> desiredWeaponDefinitionIds = new List<string>();
    public List<string> desiredArmorDefinitionIds = new List<string>();
    public string desiredShieldDefinitionId = string.Empty;
    public string shieldInstanceId = string.Empty;
    public string activeWeaponInstanceId = string.Empty;
    public CombatFireMode fireMode = CombatFireMode.Aimed;
    public bool holdFire;
    [Min(0)] public int desiredAmmo;

    public CharacterCombatLoadoutProfile Clone()
    {
        return new CharacterCombatLoadoutProfile
        {
            profileId = profileId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            weaponInstanceIds = weaponInstanceIds != null
                ? new List<string>(weaponInstanceIds)
                : new List<string>(),
            armorInstanceIds = armorInstanceIds != null
                ? new List<string>(armorInstanceIds)
                : new List<string>(),
            desiredWeaponDefinitionIds = desiredWeaponDefinitionIds != null
                ? new List<string>(desiredWeaponDefinitionIds)
                : new List<string>(),
            desiredArmorDefinitionIds = desiredArmorDefinitionIds != null
                ? new List<string>(desiredArmorDefinitionIds)
                : new List<string>(),
            desiredShieldDefinitionId = desiredShieldDefinitionId ?? string.Empty,
            shieldInstanceId = shieldInstanceId ?? string.Empty,
            activeWeaponInstanceId = activeWeaponInstanceId ?? string.Empty,
            fireMode = fireMode,
            holdFire = holdFire,
            desiredAmmo = Mathf.Max(0, desiredAmmo)
        };
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatLoadoutState
{
    public string characterId = string.Empty;
    public string activeProfileId = CombatLoadoutPresetIds.Peace;
    public List<CharacterCombatLoadoutProfile> profiles = new List<CharacterCombatLoadoutProfile>();
}

public static class CombatLoadoutPresetIds
{
    public const string Peace = "combat-loadout:peace";
    public const string Combat = "combat-loadout:combat";
    public const string Melee = "combat-loadout:preset:melee";
    public const string Archer = "combat-loadout:preset:archer";
    public const string Crossbow = "combat-loadout:preset:crossbow";
    public const string Skirmisher = "combat-loadout:preset:skirmisher";
}

public readonly struct CombatStatSnapshot
{
    public CombatStatSnapshot(
        float melee,
        float shooting,
        float evasion,
        float moveSpeed,
        float strength,
        float toughness,
        float dexterity,
        float healthMultiplier = 1f)
    {
        Melee = Mathf.Max(0f, melee);
        Shooting = Mathf.Max(0f, shooting);
        Evasion = Mathf.Max(0f, evasion);
        MoveSpeed = Mathf.Max(0f, moveSpeed);
        Strength = Mathf.Max(0f, strength);
        Toughness = Mathf.Max(0f, toughness);
        Dexterity = Mathf.Max(0f, dexterity);
        HealthMultiplier = Mathf.Max(0f, healthMultiplier);
    }

    public float Melee { get; }
    public float Shooting { get; }
    public float Evasion { get; }
    public float MoveSpeed { get; }
    public float Strength { get; }
    public float Toughness { get; }
    public float Dexterity { get; }
    public float HealthMultiplier { get; }
}

public readonly struct CombatCoverSnapshot
{
    public CombatCoverSnapshot(
        CombatCoverHeight height,
        float baseBlockChance,
        float incomingAngleDegrees,
        string sourceId = "",
        bool allowsCornerPeek = false)
    {
        Height = height;
        BaseBlockChance = Mathf.Clamp01(baseBlockChance);
        IncomingAngleDegrees = Mathf.Abs(incomingAngleDegrees);
        SourceId = sourceId ?? string.Empty;
        AllowsCornerPeek = allowsCornerPeek;
    }

    public CombatCoverHeight Height { get; }
    public float BaseBlockChance { get; }
    public float IncomingAngleDegrees { get; }
    public string SourceId { get; }
    public bool AllowsCornerPeek { get; }
    public bool BlocksLineOfSight => Height == CombatCoverHeight.High;

    public float GetDirectionalMultiplier()
    {
        if (IncomingAngleDegrees <= 15f) return 1f;
        if (IncomingAngleDegrees <= 35f) return 0.75f;
        if (IncomingAngleDegrees <= 55f) return 0.4f;
        return 0f;
    }
}

public readonly struct CombatArmorSnapshot
{
    public CombatArmorSnapshot(
        string instanceId,
        CombatBodyPart bodyPart,
        CombatArmorLayer layer,
        CombatEquipmentQuality quality,
        float durabilityRatio,
        float slashDefense,
        float pierceDefense,
        float bluntDefense,
        float materialDefenseMultiplier = 1f)
    {
        InstanceId = instanceId ?? string.Empty;
        BodyPart = bodyPart;
        Layer = layer;
        Quality = quality;
        DurabilityRatio = Mathf.Clamp01(durabilityRatio);
        SlashDefense = Mathf.Max(0f, slashDefense);
        PierceDefense = Mathf.Max(0f, pierceDefense);
        BluntDefense = Mathf.Max(0f, bluntDefense);
        MaterialDefenseMultiplier = Mathf.Max(0.01f, materialDefenseMultiplier);
    }

    public string InstanceId { get; }
    public CombatBodyPart BodyPart { get; }
    public CombatArmorLayer Layer { get; }
    public CombatEquipmentQuality Quality { get; }
    public float DurabilityRatio { get; }
    public float SlashDefense { get; }
    public float PierceDefense { get; }
    public float BluntDefense { get; }
    public float MaterialDefenseMultiplier { get; }

    public float GetDefense(CombatDamageType damageType)
    {
        float baseDefense = damageType switch
        {
            CombatDamageType.Pierce => PierceDefense,
            CombatDamageType.Blunt => BluntDefense,
            _ => SlashDefense
        };
        float wornMultiplier = Mathf.Lerp(0.35f, 1f, DurabilityRatio);
        return baseDefense
            * CombatQualityRules.GetMultiplier(Quality)
            * wornMultiplier
            * MaterialDefenseMultiplier;
    }
}

public readonly struct CombatArmorDurabilityHit
{
    public CombatArmorDurabilityHit(string instanceId, float damage)
    {
        InstanceId = instanceId ?? string.Empty;
        Damage = Mathf.Max(0f, damage);
    }

    public string InstanceId { get; }
    public float Damage { get; }
}

public readonly struct CombatShieldSnapshot
{
    public CombatShieldSnapshot(
        string instanceId,
        CombatEquipmentQuality quality,
        float durabilityRatio,
        float frontalBlockChance,
        float incomingAngleDegrees,
        float slashDefense,
        float pierceDefense,
        float bluntDefense,
        float materialDefenseMultiplier = 1f)
    {
        InstanceId = instanceId ?? string.Empty;
        Quality = quality;
        DurabilityRatio = Mathf.Clamp01(durabilityRatio);
        FrontalBlockChance = Mathf.Clamp01(frontalBlockChance);
        IncomingAngleDegrees = Mathf.Abs(incomingAngleDegrees);
        SlashDefense = Mathf.Max(0f, slashDefense);
        PierceDefense = Mathf.Max(0f, pierceDefense);
        BluntDefense = Mathf.Max(0f, bluntDefense);
        MaterialDefenseMultiplier = Mathf.Max(0.01f, materialDefenseMultiplier);
    }

    public string InstanceId { get; }
    public CombatEquipmentQuality Quality { get; }
    public float DurabilityRatio { get; }
    public float FrontalBlockChance { get; }
    public float IncomingAngleDegrees { get; }
    public float SlashDefense { get; }
    public float PierceDefense { get; }
    public float BluntDefense { get; }
    public float MaterialDefenseMultiplier { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(InstanceId) && DurabilityRatio > 0f;

    public float GetBlockChance()
    {
        float directionMultiplier = IncomingAngleDegrees switch
        {
            <= 35f => 1f,
            <= 70f => 0.5f,
            _ => 0f
        };
        return FrontalBlockChance
            * directionMultiplier
            * Mathf.Lerp(0.4f, 1f, DurabilityRatio)
            * CombatQualityRules.GetMultiplier(Quality);
    }

    public float GetDefense(CombatDamageType damageType)
    {
        float defense = damageType switch
        {
            CombatDamageType.Pierce => PierceDefense,
            CombatDamageType.Blunt => BluntDefense,
            _ => SlashDefense
        };
        return defense
            * Mathf.Lerp(0.35f, 1f, DurabilityRatio)
            * CombatQualityRules.GetMultiplier(Quality)
            * MaterialDefenseMultiplier;
    }
}

public readonly struct CombatAttackRequest
{
    public CombatAttackRequest(
        string eventId,
        string attackerId,
        string defenderId,
        CombatStatSnapshot attacker,
        CombatStatSnapshot defender,
        CombatWeaponSnapshot weapon,
        int distance,
        CombatFireMode fireMode,
        CombatCoverSnapshot cover,
        bool hasLineOfSight = true,
        bool friendlyFireRisk = false,
        bool forceFire = false,
        bool defenderDowned = false,
        bool defenderMeleeLocked = false,
        float attackerSuppression = 0f,
        float defenderSuppression = 0f,
        float lightMultiplier = 1f,
        float weatherMultiplier = 1f,
        float attackPowerMultiplier = 1f,
        IReadOnlyList<CombatArmorSnapshot> defenderArmor = null,
        CombatShieldSnapshot defenderShield = default)
    {
        EventId = eventId ?? string.Empty;
        AttackerId = attackerId ?? string.Empty;
        DefenderId = defenderId ?? string.Empty;
        Attacker = attacker;
        Defender = defender;
        Weapon = weapon;
        Distance = Mathf.Max(0, distance);
        FireMode = fireMode;
        Cover = cover;
        HasLineOfSight = hasLineOfSight;
        FriendlyFireRisk = friendlyFireRisk;
        ForceFire = forceFire;
        DefenderDowned = defenderDowned;
        DefenderMeleeLocked = defenderMeleeLocked;
        AttackerSuppression = Mathf.Clamp(attackerSuppression, 0f, 100f);
        DefenderSuppression = Mathf.Clamp(defenderSuppression, 0f, 100f);
        LightMultiplier = Mathf.Max(0f, lightMultiplier);
        WeatherMultiplier = Mathf.Max(0f, weatherMultiplier);
        AttackPowerMultiplier = Mathf.Max(0f, attackPowerMultiplier);
        DefenderArmor = defenderArmor ?? Array.Empty<CombatArmorSnapshot>();
        DefenderShield = defenderShield;
    }

    public string EventId { get; }
    public string AttackerId { get; }
    public string DefenderId { get; }
    public CombatStatSnapshot Attacker { get; }
    public CombatStatSnapshot Defender { get; }
    public CombatWeaponSnapshot Weapon { get; }
    public int Distance { get; }
    public CombatFireMode FireMode { get; }
    public CombatCoverSnapshot Cover { get; }
    public bool HasLineOfSight { get; }
    public bool FriendlyFireRisk { get; }
    public bool ForceFire { get; }
    public bool DefenderDowned { get; }
    public bool DefenderMeleeLocked { get; }
    public float AttackerSuppression { get; }
    public float DefenderSuppression { get; }
    public float LightMultiplier { get; }
    public float WeatherMultiplier { get; }
    public float AttackPowerMultiplier { get; }
    public IReadOnlyList<CombatArmorSnapshot> DefenderArmor { get; }
    public CombatShieldSnapshot DefenderShield { get; }
}

public readonly struct CombatAttackPreview
{
    public CombatAttackPreview(
        bool valid,
        string failureReason,
        CombatRangeBand rangeBand,
        float hitChance,
        float coverBlockChance,
        float shieldBlockChance,
        float evasionChance,
        float damageOnHit,
        float expectedDamage)
    {
        Valid = valid;
        FailureReason = failureReason ?? string.Empty;
        RangeBand = rangeBand;
        HitChance = Mathf.Clamp01(hitChance);
        CoverBlockChance = Mathf.Clamp01(coverBlockChance);
        ShieldBlockChance = Mathf.Clamp01(shieldBlockChance);
        EvasionChance = Mathf.Clamp01(evasionChance);
        DamageOnHit = Mathf.Max(0f, damageOnHit);
        ExpectedDamage = Mathf.Max(0f, expectedDamage);
    }

    public bool Valid { get; }
    public string FailureReason { get; }
    public CombatRangeBand RangeBand { get; }
    public float HitChance { get; }
    public float CoverBlockChance { get; }
    public float ShieldBlockChance { get; }
    public float EvasionChance { get; }
    public float DamageOnHit { get; }
    public float ExpectedDamage { get; }
}

public readonly struct CombatAttackResult
{
    public CombatAttackResult(
        bool executed,
        bool hit,
        bool coverBlocked,
        bool evaded,
        CombatBodyPart bodyPart,
        float rawDamage,
        float appliedDamage,
        float bleeding,
        float suppression,
        float armorDurabilityDamage,
        string armorInstanceId,
        string failureReason,
        bool shieldBlocked = false,
        string coverSourceId = "",
        float coverDamage = 0f,
        IReadOnlyList<CombatArmorDurabilityHit> armorDurabilityHits = null)
    {
        Executed = executed;
        Hit = hit;
        CoverBlocked = coverBlocked;
        Evaded = evaded;
        BodyPart = bodyPart;
        RawDamage = Mathf.Max(0f, rawDamage);
        AppliedDamage = Mathf.Max(0f, appliedDamage);
        Bleeding = Mathf.Max(0f, bleeding);
        Suppression = Mathf.Max(0f, suppression);
        ArmorDurabilityDamage = Mathf.Max(0f, armorDurabilityDamage);
        ArmorInstanceId = armorInstanceId ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
        ShieldBlocked = shieldBlocked;
        CoverSourceId = coverSourceId ?? string.Empty;
        CoverDamage = Mathf.Max(0f, coverDamage);
        ArmorDurabilityHits = armorDurabilityHits ?? Array.Empty<CombatArmorDurabilityHit>();
    }

    public bool Executed { get; }
    public bool Hit { get; }
    public bool CoverBlocked { get; }
    public bool Evaded { get; }
    public CombatBodyPart BodyPart { get; }
    public float RawDamage { get; }
    public float AppliedDamage { get; }
    public float Bleeding { get; }
    public float Suppression { get; }
    public float ArmorDurabilityDamage { get; }
    public string ArmorInstanceId { get; }
    public string FailureReason { get; }
    public bool ShieldBlocked { get; }
    public string CoverSourceId { get; }
    public float CoverDamage { get; }
    public IReadOnlyList<CombatArmorDurabilityHit> ArmorDurabilityHits { get; }
}
