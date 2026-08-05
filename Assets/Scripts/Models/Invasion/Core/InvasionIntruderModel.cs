using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class InvasionIntruderSettings
{
    public string patternId = InvasionIntruderPatternIds.Hunter;
    [Min(0f)] public float rallyDurationSeconds = 12f;
    [Min(0.1f)] public float secondsToFullFocus = 30f;
    [Min(0.1f)] public float repathIntervalSeconds = 1.5f;
    [Min(0f)] public float facilityDamageIntervalSeconds = 5f;
    [Min(0.1f)] public float structureAttackIntervalSeconds = 1.25f;
    [Min(0f)] public float finalCombatDamage = 45f;
    [Min(0f)] public float finalCombatWindupSeconds = 0.7f;
    [Min(0.01f)] public float healthMultiplier = 1f;
    [Min(0.01f)] public float meleeDamageMultiplier = 1f;
    [Min(0.01f)] public float attackSpeedMultiplier = 1f;
    [Range(0f, 1f)] public float riskTolerance = 0.55f;
    [Min(0f)] public float routeCommitmentSeconds = 2f;
    [Min(0.01f)] public float structureDamageMultiplier = 1f;
    [Range(0f, 1f)] public float retreatHealthRatio;
    public InvasionOperationKind operationKind = InvasionOperationKind.FrontalAssault;
    public string raidId = string.Empty;
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
        bool hasBreachedDungeonInterior = false,
        int breachTargetBuildingId = -1,
        Vector2Int breachTargetPosition = default,
        Vector2Int breachAttackCell = default,
        float structureAttackDelayRemaining = 0f,
        float trappedSeconds = 0f,
        bool enragedBreach = false,
        DefenseRaidAwarenessSaveData raidAwareness = null,
        IEnumerable<BuildingInstanceId> damagedFacilityIds = null)
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
        BreachTargetBuildingId = breachTargetBuildingId;
        BreachTargetPosition = breachTargetPosition;
        BreachAttackCell = breachAttackCell;
        StructureAttackDelayRemaining = Mathf.Max(
            0f,
            structureAttackDelayRemaining);
        TrappedSeconds = Mathf.Max(0f, trappedSeconds);
        EnragedBreach = enragedBreach;
        RaidAwareness = raidAwareness;
        DamagedFacilityIds = Array.AsReadOnly((damagedFacilityIds
                ?? Array.Empty<BuildingInstanceId>())
            .Where(id => id.IsValid)
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray());
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
    public int BreachTargetBuildingId { get; }
    public Vector2Int BreachTargetPosition { get; }
    public Vector2Int BreachAttackCell { get; }
    public float StructureAttackDelayRemaining { get; }
    public float TrappedSeconds { get; }
    public bool EnragedBreach { get; }
    public DefenseRaidAwarenessSaveData RaidAwareness { get; }
    public IReadOnlyList<BuildingInstanceId> DamagedFacilityIds { get; }

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
            structureAttackIntervalSeconds = Mathf.Max(
                0.1f,
                source.structureAttackIntervalSeconds),
            finalCombatDamage = source.finalCombatDamage,
            finalCombatWindupSeconds = source.finalCombatWindupSeconds,
            healthMultiplier = Mathf.Max(0.01f, source.healthMultiplier),
            meleeDamageMultiplier = Mathf.Max(0.01f, source.meleeDamageMultiplier),
            attackSpeedMultiplier = Mathf.Max(0.01f, source.attackSpeedMultiplier)
            ,
            riskTolerance = Mathf.Clamp01(source.riskTolerance),
            routeCommitmentSeconds = Mathf.Max(0f, source.routeCommitmentSeconds),
            structureDamageMultiplier = Mathf.Max(
                0.01f,
                source.structureDamageMultiplier),
            retreatHealthRatio = Mathf.Clamp01(source.retreatHealthRatio),
            operationKind = source.operationKind,
            raidId = source.raidId?.Trim() ?? string.Empty
        };
    }
}
