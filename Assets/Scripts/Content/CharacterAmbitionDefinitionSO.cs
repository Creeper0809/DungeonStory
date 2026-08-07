using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CharacterAmbitionCategory { Mastery, Family, Status, Community, Faction, VengeanceOrDiscovery }

[CreateAssetMenu(fileName = "CharacterAmbition", menuName = "DungeonStory/V20/Character Ambition")]
public sealed class CharacterAmbitionDefinitionSO : V20AuthoredContentSO
{
    public CharacterAmbitionCategory category;
    [Min(1)] public int targetProgress = 100;
    public V20ContentRequirementSet activationRequirements = new();
    public V20ContentRequirementSet failureConditions = new();
    public List<V20ContentEffect> completionRewards = new();
    public List<V20WeightedId> relatedEventWeights = new();
    public List<string> cooperationAmbitionIds = new();
    public List<string> conflictAmbitionIds = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        errors.AddRange((activationRequirements ?? new()).Validate(StableId));
        if (targetProgress < 1) errors.Add($"'{StableId}' target progress must be positive.");
        if (completionRewards == null || completionRewards.Count == 0 || completionRewards.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires valid completion rewards.");
        return errors;
    }
}
