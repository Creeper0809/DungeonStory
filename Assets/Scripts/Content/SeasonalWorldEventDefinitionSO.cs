using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SeasonalWorldEvent", menuName = "DungeonStory/V20/Seasonal World Event")]
public sealed class SeasonalWorldEventDefinitionSO : V20AuthoredContentSO
{
    public Season season;
    [Min(1)] public int minimumDurationDays = 1;
    [Min(1)] public int maximumDurationDays = 1;
    public List<string> affectedDomainIds = new();
    public V20ContentRequirementSet triggerRequirements = new();
    public List<V20ContentEffect> startEffects = new();
    public List<V20ContentEffect> dailyEffects = new();
    public List<V20ContentEffect> endEffects = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (minimumDurationDays < 1 || maximumDurationDays < minimumDurationDays)
            errors.Add($"'{StableId}' duration range is invalid.");
        if ((affectedDomainIds ?? new()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Count() < 2)
            errors.Add($"'{StableId}' must affect at least two domains.");
        if (!(startEffects ?? new()).Concat(dailyEffects ?? new()).Concat(endEffects ?? new()).Any(value => value != null && value.IsValid))
            errors.Add($"'{StableId}' requires a mechanical effect.");
        return errors;
    }
}
