using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Facility Synthesis/Recipe", order = 0)]
public class FacilitySynthesisRecipeSO : DataScriptableObject
{
    public string recipeId;
    public string displayName;
    [TextArea] public string description;
    public BuildingSO resultBuilding;
    public BuildingSO[] materialBuildings = Array.Empty<BuildingSO>();
    public bool publicByDefault = true;
    public string requiredResearchRecipeId;
    [Range(0f, 1f)] public float levelInheritanceRatio = 0.75f;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public bool IsSpecial => !publicByDefault || !string.IsNullOrWhiteSpace(requiredResearchRecipeId);

    public int[] MaterialBuildingIds => materialBuildings?
        .Where((building) => building != null)
        .Select((building) => building.id)
        .ToArray()
        ?? Array.Empty<int>();

    public bool HasValidData => resultBuilding != null
        && materialBuildings != null
        && materialBuildings.Length >= 2
        && materialBuildings.All((building) => building != null);
}
