using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class OffenseDecisionChoiceDefinition
{
    public string choiceId = "choice";
    public string label = "선택";
    [TextArea] public string description;
    public string requiredTag;
    public string transformedLabel;
    [TextArea] public string transformedDescription;
    public string directionLabel = "변화";
    [Range(0, 3)] public int severity = 1;
    public bool mayStartCombat;
    public bool mayCauseInjury;
    public bool mayMoveExpedition;
    [SerializeReference]
    public List<OffenseDecisionEffectDefinition> effects =
        new List<OffenseDecisionEffectDefinition>();
}

[CreateAssetMenu(
    fileName = "OffenseDecisionCard",
    menuName = "DungeonStory/Offense/Decision Card")]
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
