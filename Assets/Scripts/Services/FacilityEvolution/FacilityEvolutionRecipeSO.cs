using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Facility Evolution/Recipe", order = 0)]
public class FacilityEvolutionRecipeSO : DataScriptableObject
{
    public string evolutionId;
    public string displayName;
    [TextArea] public string description;

    [Header("Lineage")]
    public BuildingSO resultBuilding;
    public BuildingSO[] fromFacilities = Array.Empty<BuildingSO>();
    public string[] fromLineageTags = Array.Empty<string>();
    [Min(1)] public int requiredStarGrade = 1;
    [Min(1)] public int resultStarGrade = 2;

    [Header("Visibility")]
    public bool publicByDefault = true;
    public string requiredResearchRecipeId;

    [Header("Room")]
    public bool requireUsableRoom = true;
    public FacilityEvolutionMetricRequirement[] requiredRoomScores = Array.Empty<FacilityEvolutionMetricRequirement>();
    public FacilityEvolutionMetricRequirement[] requiredRoomMetrics = Array.Empty<FacilityEvolutionMetricRequirement>();
    public string[] requiredRoomTags = Array.Empty<string>();
    public BuildingSO[] requiredUniqueFixtures = Array.Empty<BuildingSO>();

    [Header("Records and Resources")]
    public FacilityEvolutionTokenRequirement[] requiredRecordTokens = Array.Empty<FacilityEvolutionTokenRequirement>();
    public FacilityEvolutionMaterialRequirement[] requiredMaterials = Array.Empty<FacilityEvolutionMaterialRequirement>();
    public string[] allowedMutationTags = Array.Empty<string>();
    public bool consumeRecordTokens;

    [Header("Identity Pressure")]
    public FacilityEvolutionValue[] identityPressureWeights = Array.Empty<FacilityEvolutionValue>();
    [Range(0f, 1f)] public float minimumIdentityScore;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public bool IsSpecial => !publicByDefault || !string.IsNullOrWhiteSpace(requiredResearchRecipeId);

    public bool HasValidData => resultBuilding != null
        && (!string.IsNullOrWhiteSpace(evolutionId) || !string.IsNullOrWhiteSpace(name))
        && (fromFacilities == null || fromFacilities.All((building) => building != null))
        && (fromFacilities != null && fromFacilities.Length > 0
            || fromLineageTags != null && fromLineageTags.Length > 0);

    public string EffectiveId => !string.IsNullOrWhiteSpace(evolutionId) ? evolutionId : name;

    public bool MatchesSource(BuildableObject facility, FacilityEvolutionStateComponent state)
    {
        if (facility == null)
        {
            return false;
        }

        if (fromFacilities != null
            && fromFacilities.Any((building) => building != null && facility.id == building.id))
        {
            return true;
        }

        if (fromLineageTags == null || fromLineageTags.Length == 0)
        {
            return false;
        }

        return state != null
            && state.LineageTags.Any((tag) => fromLineageTags.Contains(tag));
    }
}
