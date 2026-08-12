using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Effects/Gameplay Effect Condition", order = 1)]
public sealed class GameplayEffectConditionDefinitionSO : ScriptableObject
{
    [SerializeField] private int numericId;
    [SerializeField] private string conditionId = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;

    public string ConditionId => conditionId?.Trim() ?? string.Empty;
    public int NumericId => numericId;
    public string Description => description?.Trim() ?? string.Empty;

#if UNITY_EDITOR
    public void Configure(int numericId, string stableConditionId, string authoredDescription)
    {
        this.numericId = numericId;
        conditionId = stableConditionId?.Trim() ?? string.Empty;
        description = authoredDescription?.Trim() ?? string.Empty;
    }
#endif
}
