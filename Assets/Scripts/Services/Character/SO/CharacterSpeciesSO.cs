using System;
using UnityEngine;

public enum CharacterSpeciesIncidentType
{
    None,
    SlimeContamination,
    OrcRampage,
    VampireFear
}

[CreateAssetMenu(menuName = "DungeonStory/Character/Species", order = 0)]
public class CharacterSpeciesSO : DataScriptableObject
{
    public string speciesTag;
    public string displayName;
    public SpeciesOwnerSelectionPolicy ownerSelectionPolicy;
    public string homeFactionId;
    public string anatomyProfileId = "anatomy:humanoid";
    public SpeciesNeedProfile needs = new SpeciesNeedProfile();
    public SpeciesEnvironmentProfile environment = new SpeciesEnvironmentProfile();
    public string[] relationTags = Array.Empty<string>();
    public string[] defenseAffinityTags = Array.Empty<string>();
    public string[] strongWorkTypeIds = Array.Empty<string>();
    public string[] weakWorkTypeIds = Array.Empty<string>();
    [TextArea] public string shortDescription;
    [TextArea] public string description;
    public string[] preferredFacilityLabels = Array.Empty<string>();
    public string[] dislikedEnvironmentLabels = Array.Empty<string>();
    [Min(0f)] public float stayDurationMultiplier = 1f;
    [Min(0f)] public float crimeRiskMultiplier = 1f;
    public CharacterSpeciesIncidentType incidentType;
    public SpeciesIncidentDefinition incident = new SpeciesIncidentDefinition();
    public string incidentName;
    [TextArea] public string incidentDescription;
    public FacilityRole incidentMitigatingRoles;
    public CharacterStatBlock statBonus = new CharacterStatBlock();
    public CharacterModelModifiers modifiers = new CharacterModelModifiers();
    public SpeciesPassiveDefinition combatPassive = new SpeciesPassiveDefinition();
    public CharacterCombatAbilityCollection combatAbilities = new CharacterCombatAbilityCollection();

    public bool ownerSelectable =>
        ownerSelectionPolicy == SpeciesOwnerSelectionPolicy.Selectable;

    public string IncidentId => !string.IsNullOrWhiteSpace(incident?.StableId)
        ? incident.StableId
        : CharacterSpeciesIncidentIds.FromLegacy(incidentType);

    public string IncidentDisplayName =>
        !string.IsNullOrWhiteSpace(incident?.displayName)
            ? incident.displayName.Trim()
            : incidentName?.Trim() ?? string.Empty;

    public string IncidentDescription =>
        !string.IsNullOrWhiteSpace(incident?.description)
            ? incident.description.Trim()
            : incidentDescription?.Trim() ?? string.Empty;

    public FacilityRole IncidentMitigatingRoles =>
        incident != null && incident.mitigatingRoles != FacilityRole.None
            ? incident.mitigatingRoles
            : incidentMitigatingRoles;
}
