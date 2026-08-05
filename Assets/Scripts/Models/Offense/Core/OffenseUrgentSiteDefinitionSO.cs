using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "OffenseUrgentSite",
    menuName = "DungeonStory/Offense/Urgent Site")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseUrgentSiteDefinitionSO : DataScriptableObject
{
    public string urgentSiteId = "urgent";
    public string displayName = "긴급 거점";
    [TextArea] public string description;
    public OffenseThreatModifierKind modifierKind;
    [Range(0f, 3f)] public float maximumStrength = 1f;
    [Range(0f, 1f)] public float maximumMitigation = 0.6f;
    public string mitigationWorkTypeId;
    public string mitigationItemId;
    [Min(0)] public int mitigationItemAmount;
    [Min(0f)] public float mitigationWork;
}
