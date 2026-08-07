using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum V20FactionContractKind { Supply, CrisisResponse, Strategic }

[CreateAssetMenu(fileName = "FactionContract", menuName = "DungeonStory/V20/Faction Contract")]
public sealed class FactionContractDefinitionSO : V20AuthoredContentSO
{
    public string factionId = string.Empty;
    public V20FactionContractKind kind;
    [Min(1)] public int deadlineDays = 10;
    public V20ContentRequirementSet completionRequirements = new();
    public List<V20ContentEffect> successEffects = new();
    public List<V20ContentEffect> failureEffects = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (string.IsNullOrWhiteSpace(factionId)) errors.Add($"'{StableId}' requires a faction id.");
        errors.AddRange((completionRequirements ?? new()).Validate(StableId));
        if (successEffects == null || successEffects.Count == 0 || successEffects.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires success effects.");
        return errors;
    }
}
