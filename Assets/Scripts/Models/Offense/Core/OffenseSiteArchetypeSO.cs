using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "OffenseSiteArchetype",
    menuName = "DungeonStory/Offense/Site Archetype")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseSiteArchetypeSO : DataScriptableObject
{
    public string siteTypeId = "site";
    public string displayName = "거점";
    [TextArea] public string description;
    public string factionId = "human";
    public StrategicPressureAxis pressureAxis;
    [Min(0f)] public float pressureAmount = 15f;
    [Min(1)] public int minimumStrength = 1;
    [Min(1)] public int maximumStrength = 5;
    [Min(1)] public int minimumLifetimeDays = 2;
    [Min(1)] public int maximumLifetimeDays = 6;
    public bool hiddenUntilDiscovered = true;
    public bool canMove;
    public bool dynamicSpawnEligible = true;
    public List<OffenseSiteRewardDefinition> rewards = new();
}
