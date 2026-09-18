using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public enum CropLightProfile
{
    Unspecified = 0,
    LightIndependent = 1,
    Shade = 2,
    Medium = 3,
    High = 4
}

public readonly struct CropLightRequirement
{
    public CropLightRequirement(
        CropLightProfile profile,
        float stopLight,
        float sufficientLight)
    {
        if (profile == CropLightProfile.Unspecified
            || !System.Enum.IsDefined(typeof(CropLightProfile), profile))
        {
            throw new System.ArgumentOutOfRangeException(nameof(profile));
        }
        if (!float.IsFinite(stopLight)
            || !float.IsFinite(sufficientLight)
            || stopLight < 0f
            || sufficientLight < stopLight
            || sufficientLight > 100f
            || (profile == CropLightProfile.LightIndependent)
                != (stopLight == 0f && sufficientLight == 0f))
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(sufficientLight),
                "Crop light thresholds must be finite, ordered, and match the named profile.");
        }

        Profile = profile;
        StopLight = stopLight;
        SufficientLight = sufficientLight;
    }

    public CropLightProfile Profile { get; }
    public float StopLight { get; }
    public float SufficientLight { get; }
    public bool IsLightIndependent =>
        Profile == CropLightProfile.LightIndependent;
}

public static class CropLightProfileRules
{
    public static CropLightRequirement Resolve(CropLightProfile profile) =>
        profile switch
        {
            CropLightProfile.LightIndependent =>
                new CropLightRequirement(profile, 0f, 0f),
            CropLightProfile.Shade =>
                new CropLightRequirement(profile, 0f, 25f),
            CropLightProfile.Medium =>
                new CropLightRequirement(profile, 5f, 40f),
            CropLightProfile.High =>
                new CropLightRequirement(profile, 5f, 50f),
            _ => throw new System.InvalidOperationException(
                $"Crop light profile '{profile}' is not authored.")
        };
}

[CreateAssetMenu(menuName = "DungeonStory/Economy/Crop Definition", order = 2)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CropDefinitionSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Economy/Crops";

    [SerializeField] private string cropId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private string harvestItemId = string.Empty;
    [SerializeField] private string seedItemId = string.Empty;
    [SerializeField] private CropGenomeDefinitionSO baseGenome;
    [SerializeField] private CropFamilyGroup familyGroup;
    [SerializeField] private CropDiseaseKind endemicDisease;
    [SerializeField] private string requiredResearchId = string.Empty;
    [Min(1f), SerializeField] private float growthHours = 24f;
    [Min(0.1f), SerializeField] private float sowWork = 4f;
    [Min(0.1f), SerializeField] private float harvestWork = 6f;
    [Min(0f), SerializeField] private float dailyWater = 0.25f;
    [Min(1), SerializeField] private int yield = 4;
    [SerializeField] private bool indoorAllowed = true;
    [SerializeField] private Vector2 temperatureRange = new Vector2(5f, 30f);
    [SerializeField] private CropLightProfile lightProfile =
        CropLightProfile.Unspecified;

    public string CropId => cropId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? CropId : displayName.Trim();
    public string HarvestItemId => harvestItemId?.Trim() ?? string.Empty;
    public string SeedItemId => seedItemId?.Trim() ?? string.Empty;
    public CropGenomeDefinitionSO BaseGenome => baseGenome;
    public CropFamilyGroup FamilyGroup => familyGroup;
    public CropDiseaseKind EndemicDisease => endemicDisease;
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
    public CropLightProfile LightProfile => lightProfile;
    public CropLightRequirement LightRequirement =>
        CropLightProfileRules.Resolve(lightProfile);

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
        // Editor-created verification crops use the ordinary high-light profile
        // unless their author explicitly selects another named profile below.
        lightProfile = CropLightProfile.High;
    }

    public void ConfigureLightProfile(CropLightProfile profile)
    {
        _ = CropLightProfileRules.Resolve(profile);
        lightProfile = profile;
    }

    public void ConfigureEcology(
        string physicalSeedItemId,
        CropGenomeDefinitionSO authoredBaseGenome,
        CropFamilyGroup group,
        CropDiseaseKind disease)
    {
        seedItemId = physicalSeedItemId?.Trim() ?? string.Empty;
        baseGenome = authoredBaseGenome;
        familyGroup = group;
        endemicDisease = disease;
    }
#endif
}
