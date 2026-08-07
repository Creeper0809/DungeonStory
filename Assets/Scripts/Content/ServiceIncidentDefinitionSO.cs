using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ServiceIncidentKind { Brawl, Theft, Contamination, CulturalInsult, ForbiddenMeal, MedicalCollapse, EnvoyConflict, Sabotage }

[CreateAssetMenu(fileName = "ServiceIncident", menuName = "DungeonStory/V20/Service Incident")]
public sealed class ServiceIncidentDefinitionSO : V20AuthoredContentSO
{
    public ServiceIncidentKind kind;
    public V20ContentRequirementSet triggerRequirements = new();
    public List<V20ChoiceDefinition> responses = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (responses == null || responses.Count < 2 || responses.Count > 4)
            errors.Add($"'{StableId}' requires two to four responses.");
        else foreach (V20ChoiceDefinition response in responses) errors.AddRange(response.Validate(StableId));
        return errors;
    }
}
