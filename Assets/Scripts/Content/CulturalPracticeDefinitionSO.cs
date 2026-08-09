using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CulturalPracticeKind { DailyRoutine, Food, Room, Social, ComingOfAge, Partnership, Funeral, WorkRest }

[CreateAssetMenu(fileName = "CulturalPractice", menuName = "DungeonStory/V20/Cultural Practice")]
public sealed class CulturalPracticeDefinitionSO : V20AuthoredContentSO
{
    public string cultureId = string.Empty;
    public CulturalPracticeKind kind;
    public V20ContentRequirementSet requirements = new();
    public List<V20ContentEffect> successEffects = new();
    public List<V20ContentEffect> neglectedEffects = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (string.IsNullOrWhiteSpace(cultureId)) errors.Add($"'{StableId}' requires a culture id.");
        errors.AddRange((requirements ?? new()).Validate(StableId));
        if (successEffects == null || successEffects.Count == 0 || successEffects.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires success effects.");
        if (neglectedEffects == null || neglectedEffects.Count == 0 || neglectedEffects.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires neglect effects.");
        return errors;
    }
}
