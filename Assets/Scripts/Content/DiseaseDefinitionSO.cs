using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DiseaseDefinition",
    menuName = "DungeonStory/Population/Disease")]
public sealed class DiseaseDefinitionSO : ScriptableObject
{
    public string stableId = string.Empty;
    public string displayName = string.Empty;
    public DiseaseTransmissionRoute routes;
    [Min(0)] public int incubationDays;
    [Min(0)] public int contagiousDays;
    [Range(0f, 1f)] public float baseInfectionProbability;
    [Range(0f, 100f)] public float baseSeverity;
    public DiseaseTargetSystem targetSystem;
    public bool vaccineAllowed = true;
    [Tooltip("Persistent non-contagious condition removed only by an explicit treatment command.")]
    public bool chronic;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int authoringRevision = 1;
    [TextArea] public string sourceNote = string.Empty;
    public string symptomProfileId = string.Empty;
    public List<string> fieldResponseIds = new();

    public DiseaseDefinition CreateRuntimeDefinition() => new(
        stableId,
        displayName,
        routes,
        incubationDays,
        contagiousDays,
        baseInfectionProbability,
        baseSeverity,
        targetSystem,
        vaccineAllowed,
        chronic);

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (!CreateRuntimeDefinition().IsValid) errors.Add("Disease definition is invalid.");
        if (authoringRevision < 1) errors.Add($"'{stableId}' authoring revision must be positive.");
        if (string.IsNullOrWhiteSpace(symptomProfileId))
            errors.Add($"'{stableId}' requires a unique symptom profile.");
        if (fieldResponseIds == null || fieldResponseIds.Count == 0
            || fieldResponseIds.Exists(string.IsNullOrWhiteSpace))
            errors.Add($"'{stableId}' requires at least one field response.");
        return errors;
    }
}
