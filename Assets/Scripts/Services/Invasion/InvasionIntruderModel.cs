using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class InvasionIntruderSettings
{
    public string patternId = InvasionIntruderPatternIds.Hunter;
    [Min(0f)] public float rallyDurationSeconds = 12f;
    [Min(0.1f)] public float secondsToFullFocus = 30f;
    [Min(0.1f)] public float repathIntervalSeconds = 1.5f;
    [Min(0f)] public float facilityDamageIntervalSeconds = 5f;
    [Min(0f)] public float finalCombatDamage = 45f;
    [Min(0f)] public float finalCombatWindupSeconds = 0.7f;
    [Min(0.01f)] public float healthMultiplier = 1f;
    [Min(0.01f)] public float meleeDamageMultiplier = 1f;
    [Min(0.01f)] public float attackSpeedMultiplier = 1f;
}

public static class InvasionOwnerDamageTuning
{
    public const float DefaultNormalBreachDamage = 10f;
    public const float DefaultBossBreachDamage = 90f;

    public static float Resolve(
        float sourceDamage,
        float runAdjustedDamage,
        bool isBoss,
        float configuredNormalDamage,
        float configuredBossDamage)
    {
        if (sourceDamage <= 0f || runAdjustedDamage <= 0f)
        {
            return 0f;
        }

        float runMultiplier = Mathf.Max(0f, runAdjustedDamage / sourceDamage);
        float tunedDamage = isBoss
            ? ResolveConfigured(configuredBossDamage, DefaultBossBreachDamage)
            : ResolveConfigured(configuredNormalDamage, DefaultNormalBreachDamage);
        return Mathf.Max(0f, tunedDamage * runMultiplier);
    }

    private static float ResolveConfigured(float configured, float fallback)
    {
        return configured > 0f ? configured : fallback;
    }
}

public enum InvasionIntruderState
{
    None,
    Entering,
    Searching,
    MovingToOwner,
    MovingToFacility,
    DamagingFacility,
    InterceptPlanned,
    Engaged,
    FrontBroken,
    FinalCombat,
    Finished,
    Rallying
}

public sealed class InvasionIntruderPersistenceState
{
    public InvasionIntruderPersistenceState(
        int dataId,
        Vector3 worldPosition,
        Vector2Int gridPosition,
        InvasionIntruderState state,
        float elapsedSeconds,
        float damageDelayRemaining,
        int facilityDamageCount,
        float currentHealth,
        float injurySeverity,
        float baseMood,
        IReadOnlyDictionary<CharacterCondition, float> conditions,
        InvasionIntruderSettings settings,
        IEnumerable<DefenseStatusSnapshot> defenseStatuses,
        string runtimeId = "",
        float rallyRemainingSeconds = 0f,
        bool hasBreachedDungeonInterior = false)
    {
        RuntimeId = runtimeId?.Trim() ?? string.Empty;
        DataId = dataId;
        WorldPosition = worldPosition;
        GridPosition = gridPosition;
        State = state;
        ElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        DamageDelayRemaining = Mathf.Max(0f, damageDelayRemaining);
        FacilityDamageCount = Mathf.Max(0, facilityDamageCount);
        CurrentHealth = Mathf.Max(0f, currentHealth);
        InjurySeverity = Mathf.Clamp01(injurySeverity);
        BaseMood = Mathf.Clamp(baseMood, 0f, 100f);
        Conditions = new Dictionary<CharacterCondition, float>(
            conditions ?? new Dictionary<CharacterCondition, float>());
        Settings = CloneSettings(settings);
        DefenseStatuses = Array.AsReadOnly((defenseStatuses ?? Array.Empty<DefenseStatusSnapshot>()).ToArray());
        RallyRemainingSeconds = Mathf.Max(0f, rallyRemainingSeconds);
        HasBreachedDungeonInterior = hasBreachedDungeonInterior;
    }

    public int DataId { get; }
    public string RuntimeId { get; }
    public Vector3 WorldPosition { get; }
    public Vector2Int GridPosition { get; }
    public InvasionIntruderState State { get; }
    public float ElapsedSeconds { get; }
    public float DamageDelayRemaining { get; }
    public int FacilityDamageCount { get; }
    public float CurrentHealth { get; }
    public float InjurySeverity { get; }
    public float BaseMood { get; }
    public IReadOnlyDictionary<CharacterCondition, float> Conditions { get; }
    public InvasionIntruderSettings Settings { get; }
    public IReadOnlyList<DefenseStatusSnapshot> DefenseStatuses { get; }
    public float RallyRemainingSeconds { get; }
    public bool HasBreachedDungeonInterior { get; }

    public static InvasionIntruderSettings CloneSettings(InvasionIntruderSettings source)
    {
        source ??= new InvasionIntruderSettings();
        return new InvasionIntruderSettings
        {
            patternId = source.patternId,
            rallyDurationSeconds = Mathf.Max(0f, source.rallyDurationSeconds),
            secondsToFullFocus = source.secondsToFullFocus,
            repathIntervalSeconds = source.repathIntervalSeconds,
            facilityDamageIntervalSeconds = source.facilityDamageIntervalSeconds,
            finalCombatDamage = source.finalCombatDamage,
            finalCombatWindupSeconds = source.finalCombatWindupSeconds,
            healthMultiplier = Mathf.Max(0.01f, source.healthMultiplier),
            meleeDamageMultiplier = Mathf.Max(0.01f, source.meleeDamageMultiplier),
            attackSpeedMultiplier = Mathf.Max(0.01f, source.attackSpeedMultiplier)
        };
    }
}
