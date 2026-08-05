using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "OffenseDecisionCard",
    menuName = "DungeonStory/Offense/Decision Card")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseDecisionCardSO : DataScriptableObject
{
    public string cardId = "card";
    public OffenseDecisionStage stage;
    public string title = "사건";
    [TextArea] public string situation;
    public List<string> requiredWorldTags = new List<string>();
    public List<OffenseDecisionChoiceDefinition> choices =
        new List<OffenseDecisionChoiceDefinition>
        {
            new OffenseDecisionChoiceDefinition { choiceId = "left", label = "왼쪽 선택" },
            new OffenseDecisionChoiceDefinition { choiceId = "right", label = "오른쪽 선택" }
        };
}
