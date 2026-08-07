using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum FactionChapterKind { FirstContact, InternalProblem, RivalConflict, Intervention, CrisisOrBetrayal, Resolution }

[CreateAssetMenu(fileName = "FactionChapter", menuName = "DungeonStory/V20/Faction Chapter")]
public sealed class FactionChapterDefinitionSO : V20AuthoredContentSO
{
    public string factionId = string.Empty;
    [Range(1, 6)] public int chapterNumber = 1;
    public FactionChapterKind kind;
    public string crossFactionId = string.Empty;
    public V20ContentRequirementSet triggerRequirements = new();
    public List<V20ChoiceDefinition> choices = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (string.IsNullOrWhiteSpace(factionId)) errors.Add($"'{StableId}' requires a faction id.");
        if (choices == null || choices.Count < 2 || choices.Count > 3)
            errors.Add($"'{StableId}' requires two or three outcomes.");
        else foreach (V20ChoiceDefinition choice in choices) errors.AddRange(choice.Validate(StableId));
        return errors;
    }
}
