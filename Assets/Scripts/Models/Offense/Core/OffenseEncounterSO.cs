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
    public List<OffenseEnemyArchetypeEntry> enemies =
        new List<OffenseEnemyArchetypeEntry>();
}
