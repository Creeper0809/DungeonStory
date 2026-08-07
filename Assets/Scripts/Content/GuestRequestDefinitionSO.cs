using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GuestRequestKind { LuxuryMeal, Medical, Trade, Spectacle, Refuge, Research, Armament }

[CreateAssetMenu(fileName = "GuestRequest", menuName = "DungeonStory/V20/Guest Request")]
public sealed class GuestRequestDefinitionSO : V20AuthoredContentSO
{
    public GuestRequestKind kind;
    [Min(1)] public int deadlineDays = 5;
    public V20ContentRequirementSet serviceRequirements = new();
    public List<V20ContentEffect> successEffects = new();
    public List<V20ContentEffect> failureEffects = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        errors.AddRange((serviceRequirements ?? new()).Validate(StableId));
        if (successEffects == null || successEffects.Count == 0 || successEffects.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires success effects.");
        return errors;
    }
}
