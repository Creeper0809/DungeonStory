using System;
using System.Collections.Generic;
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
        return errors;
    }
}
