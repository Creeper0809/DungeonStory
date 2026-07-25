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
    [TextArea] public string shortDescription;
    [TextArea] public string description;
    public string[] preferredFacilityLabels = Array.Empty<string>();
    public string[] dislikedEnvironmentLabels = Array.Empty<string>();
    [Min(0f)] public float stayDurationMultiplier = 1f;
    [Min(0f)] public float crimeRiskMultiplier = 1f;
    public CharacterSpeciesIncidentType incidentType;
    public string incidentName;
    [TextArea] public string incidentDescription;
    public FacilityRole incidentMitigatingRoles;
    public CharacterStatBlock statBonus = new CharacterStatBlock();
    public CharacterModelModifiers modifiers = new CharacterModelModifiers();
    public CharacterCombatAbilityCollection combatAbilities = new CharacterCombatAbilityCollection();
}
