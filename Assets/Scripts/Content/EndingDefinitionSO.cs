using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RunMilestoneTier { Legacy, Grand }

[CreateAssetMenu(fileName = "EndingMilestone", menuName = "DungeonStory/V20/Ending Milestone")]
public sealed class EndingDefinitionSO : V20AuthoredContentSO
{
    public RunMilestoneTier tier;
    public V20ContentRequirementSet completionRequirements = new();
    public string landmarkBuildingId = string.Empty;
    public List<V20ContentEffect> permanentRewards = new();
    public List<V20ContentEffect> counterPressures = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        errors.AddRange((completionRequirements ?? new()).Validate(StableId));
        if (string.IsNullOrWhiteSpace(landmarkBuildingId)) errors.Add($"'{StableId}' requires a landmark building id.");
        if (permanentRewards == null || permanentRewards.Count != 1 || permanentRewards.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires exactly one permanent reward.");
        if (counterPressures == null || counterPressures.Count != 1 || counterPressures.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires exactly one counter-pressure.");
        return errors;
    }
}
