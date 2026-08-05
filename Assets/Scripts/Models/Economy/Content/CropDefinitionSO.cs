using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(menuName = "DungeonStory/Economy/Crop Definition", order = 2)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CropDefinitionSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Economy/Crops";

    [SerializeField] private string cropId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private string harvestItemId = string.Empty;
    [SerializeField] private string requiredResearchId = string.Empty;
    [Min(1f), SerializeField] private float growthHours = 24f;
    [Min(0.1f), SerializeField] private float sowWork = 4f;
    [Min(0.1f), SerializeField] private float harvestWork = 6f;
    [Min(0f), SerializeField] private float dailyWater = 0.25f;
    [Min(1), SerializeField] private int yield = 4;
    [SerializeField] private bool indoorAllowed = true;
    [SerializeField] private Vector2 temperatureRange = new Vector2(5f, 30f);

    public string CropId => cropId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? CropId : displayName.Trim();
    public string HarvestItemId => harvestItemId?.Trim() ?? string.Empty;
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;
    public float GrowthHours => Mathf.Max(1f, growthHours);
    public float SowWork => Mathf.Max(0.1f, sowWork);
    public float HarvestWork => Mathf.Max(0.1f, harvestWork);
    public float DailyWater => Mathf.Max(0f, dailyWater);
    public int Yield => Mathf.Max(1, yield);
    public bool IndoorAllowed => indoorAllowed;
    public Vector2 TemperatureRange => new Vector2(
        Mathf.Min(temperatureRange.x, temperatureRange.y),
        Mathf.Max(temperatureRange.x, temperatureRange.y));

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string name,
        string itemId,
        string researchId,
        float hours,
        float sow,
        float harvest,
        float water,
        int harvestYield,
        bool allowIndoor,
        Vector2 temperatures)
    {
        cropId = stableId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        harvestItemId = itemId?.Trim() ?? string.Empty;
        requiredResearchId = researchId?.Trim() ?? string.Empty;
        growthHours = Mathf.Max(1f, hours);
        sowWork = Mathf.Max(0.1f, sow);
        harvestWork = Mathf.Max(0.1f, harvest);
        dailyWater = Mathf.Max(0f, water);
        yield = Mathf.Max(1, harvestYield);
        indoorAllowed = allowIndoor;
        temperatureRange = temperatures;
    }
#endif
}
