using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseEnemyArchetypeEntry
{
    public string enemyArchetypeId;
    [Min(1)] public int minimumCount = 1;
    [Min(1)] public int maximumCount = 1;
}

public enum OffenseEncounterObjective
{
    DefeatAll,
    SurviveRounds,
    ProtectTarget,
    SabotageTarget,
    Escape,
    CaptureLeader
}

[CreateAssetMenu(
    fileName = "OffenseEncounter",
    menuName = "DungeonStory/Offense/Encounter")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseEncounterSO : DataScriptableObject
{
    public string encounterId = "encounter";
    public string displayName = "교전";
    [Min(1)] public int minimumSiteStrength = 1;
    [Min(1)] public int maximumSiteStrength = 10;
    public bool elite;
    public bool boss;
    [Range(0.1f, 64f)] public float enemyHealthMultiplier = 1f;
    [Range(0.1f, 8f)] public float enemyDamageMultiplier = 1f;
    [Range(0.1f, 8f)] public float enemyAccuracyMultiplier = 1f;
    [Range(0, 4)] public int additionalEnemyCount;
    [Range(0.02f, 32f)] public float objectiveHealthMultiplier = 1f;
    [Range(0.1f, 4f)] public float objectiveControlResistanceMultiplier = 1f;
    public OffenseEncounterObjective objective;
    [Min(0)] public int objectiveRoundLimit;
    public string objectiveTargetId = string.Empty;
    public List<string> battlefieldModifierIds = new();
    public List<string> counterTags = new();
    public List<string> rewardItemIds = new();
    public List<OffenseEnemyArchetypeEntry> enemies =
        new List<OffenseEnemyArchetypeEntry>();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(encounterId)) errors.Add("Encounter id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) errors.Add($"'{encounterId}' display name is required.");
        if (minimumSiteStrength < 1 || maximumSiteStrength < minimumSiteStrength)
            errors.Add($"'{encounterId}' strength range is invalid.");
        if (!float.IsFinite(enemyHealthMultiplier)
            || enemyHealthMultiplier < 0.1f
            || enemyHealthMultiplier > 64f)
            errors.Add($"'{encounterId}' enemy health multiplier is invalid.");
        if (!float.IsFinite(enemyDamageMultiplier)
            || enemyDamageMultiplier < 0.1f
            || enemyDamageMultiplier > 8f)
            errors.Add($"'{encounterId}' enemy damage multiplier is invalid.");
        if (!float.IsFinite(enemyAccuracyMultiplier)
            || enemyAccuracyMultiplier < 0.1f
            || enemyAccuracyMultiplier > 8f)
            errors.Add($"'{encounterId}' enemy accuracy multiplier is invalid.");
        if (additionalEnemyCount < 0 || additionalEnemyCount > 4)
            errors.Add($"'{encounterId}' additional enemy count is invalid.");
        if (!float.IsFinite(objectiveHealthMultiplier)
            || objectiveHealthMultiplier < 0.02f
            || objectiveHealthMultiplier > 32f)
            errors.Add($"'{encounterId}' objective health multiplier is invalid.");
        if (!float.IsFinite(objectiveControlResistanceMultiplier)
            || objectiveControlResistanceMultiplier < 0.1f
            || objectiveControlResistanceMultiplier > 4f)
            errors.Add($"'{encounterId}' objective control resistance multiplier is invalid.");
        if (enemies == null || enemies.Count == 0
            || enemies.Exists(value => value == null
                || string.IsNullOrWhiteSpace(value.enemyArchetypeId)
                || value.minimumCount < 1
                || value.maximumCount < value.minimumCount))
            errors.Add($"'{encounterId}' requires valid enemy entries.");
        if (objective != OffenseEncounterObjective.DefeatAll
            && objectiveRoundLimit < 1)
            errors.Add($"'{encounterId}' non-elimination objective requires a round limit.");
        if ((objective is OffenseEncounterObjective.ProtectTarget
                or OffenseEncounterObjective.SabotageTarget
                or OffenseEncounterObjective.CaptureLeader)
            && string.IsNullOrWhiteSpace(objectiveTargetId))
            errors.Add($"'{encounterId}' objective requires a target id.");
        if (objective == OffenseEncounterObjective.CaptureLeader
            && !enemies.Exists(value => string.Equals(
                value.enemyArchetypeId,
                objectiveTargetId,
                StringComparison.Ordinal)))
            errors.Add($"'{encounterId}' capture target must be part of its authored composition.");
        if (counterTags == null || counterTags.Count == 0
            || counterTags.Exists(string.IsNullOrWhiteSpace)
            || counterTags.Distinct(StringComparer.Ordinal).Count() != counterTags.Count)
            errors.Add($"'{encounterId}' requires unique gameplay counter tags.");
        if (rewardItemIds == null || rewardItemIds.Count == 0
            || rewardItemIds.Exists(string.IsNullOrWhiteSpace)
            || rewardItemIds.Distinct(StringComparer.Ordinal).Count() != rewardItemIds.Count)
            errors.Add($"'{encounterId}' requires unique physical reward item ids.");
        return errors;
    }
}
