using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Economy/Production Recipe", order = 1)]
public sealed class ProductionRecipeSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Economy/Recipes";

    [SerializeField] private string recipeId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private string facilityTag = string.Empty;
    [SerializeField] private string workTypeId = "work:craft";
    [SerializeField] private string requiredResearchId = string.Empty;
    [Min(0.1f), SerializeField] private float requiredWork = 10f;
    [SerializeField] private List<ItemAmountDefinition> inputs = new List<ItemAmountDefinition>();
    [SerializeField] private List<ProductionOutputDefinition> outputs =
        new List<ProductionOutputDefinition>();

    public string RecipeId => recipeId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? RecipeId : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public string FacilityTag => facilityTag?.Trim() ?? string.Empty;
    public WorkTypeId WorkTypeId => new WorkTypeId(
        string.IsNullOrWhiteSpace(workTypeId) ? "work:craft" : workTypeId);
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;
    public float RequiredWork => Mathf.Max(0.1f, requiredWork);
    public IReadOnlyList<ItemAmountDefinition> Inputs => inputs ??= new List<ItemAmountDefinition>();
    public IReadOnlyList<ProductionOutputDefinition> Outputs =>
        outputs ??= new List<ProductionOutputDefinition>();

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string name,
        string recipeDescription,
        string requiredFacilityTag,
        string requiredWorkTypeId,
        string researchId,
        float work,
        IEnumerable<ItemAmountDefinition> recipeInputs,
        IEnumerable<ProductionOutputDefinition> recipeOutputs)
    {
        recipeId = stableId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        description = recipeDescription?.Trim() ?? string.Empty;
        facilityTag = requiredFacilityTag?.Trim() ?? string.Empty;
        workTypeId = string.IsNullOrWhiteSpace(requiredWorkTypeId)
            ? "work:craft"
            : requiredWorkTypeId.Trim();
        requiredResearchId = researchId?.Trim() ?? string.Empty;
        requiredWork = Mathf.Max(0.1f, work);
        inputs = recipeInputs?.Where(input => input != null).ToList()
            ?? new List<ItemAmountDefinition>();
        outputs = recipeOutputs?.Where(output => output != null).ToList()
            ?? new List<ProductionOutputDefinition>();
    }
#endif
}
