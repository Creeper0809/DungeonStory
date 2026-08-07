using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum LifeEventCategory { Childhood, Apprenticeship, PartnershipFamily, Career, ElderRetirement, DeathLegacy }
public enum LifeEventFrequencyRule { Repeatable, OncePerCharacter, OncePerGeneration, OncePerRun }

[CreateAssetMenu(fileName = "LifeEvent", menuName = "DungeonStory/V20/Life Event")]
public sealed class LifeEventDefinitionSO : V20AuthoredContentSO
{
    public LifeEventCategory category;
    public bool automatic;
    public bool emergency;
    [Min(1)] public int responseDeadlineDays = 3;
    [Min(0)] public int cooldownDays = 30;
    public LifeEventFrequencyRule frequencyRule = LifeEventFrequencyRule.Repeatable;
    public V20ContentRequirementSet triggerRequirements = new();
    public List<V20ChoiceDefinition> choices = new();
    public List<V20ContentEffect> automaticEffects = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        errors.AddRange((triggerRequirements ?? new()).Validate(StableId));
        if (automatic)
        {
            if (automaticEffects == null || automaticEffects.Count == 0 || automaticEffects.Any(value => value == null || !value.IsValid))
                errors.Add($"'{StableId}' automatic event requires effects.");
        }
        else
        {
            if (choices == null || choices.Count < 2 || choices.Count > 4)
                errors.Add($"'{StableId}' major event requires two to four choices.");
            else foreach (V20ChoiceDefinition choice in choices) errors.AddRange(choice.Validate(StableId));
        }
        return errors;
    }
}
