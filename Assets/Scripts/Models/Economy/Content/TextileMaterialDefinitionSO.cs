using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "TextileMaterial",
    menuName = "DungeonStory/Apparel/Textile Material")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class TextileMaterialDefinitionSO : DataScriptableObject
{
    [SerializeField] private string materialId = string.Empty;
    [SerializeField] private string physicalItemId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private TextileMaterialTag tags = TextileMaterialTag.Woven;
    [Range(0f, 1f), SerializeField] private float warmth = 0.45f;
    [Range(0f, 1f), SerializeField] private float heatResistance = 0.35f;
    [Range(0f, 1f), SerializeField] private float waterResistance = 0.25f;
    [Range(0f, 1f), SerializeField] private float airborneResistance = 0.2f;
    [Range(0f, 1f), SerializeField] private float sterility = 0.15f;
    [Min(1f), SerializeField] private float durability = 60f;
    [Min(0.01f), SerializeField] private float weightMultiplier = 1f;
    [Range(0.05f, 2f), SerializeField] private float dryingRate = 1f;
    [SerializeField] private string requiredResearchId = string.Empty;

    public string MaterialId => materialId?.Trim() ?? string.Empty;
    public string PhysicalItemId => physicalItemId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public TextileMaterialTag Tags => tags;
    public float Warmth => Mathf.Clamp01(warmth);
    public float HeatResistance => Mathf.Clamp01(heatResistance);
    public float WaterResistance => Mathf.Clamp01(waterResistance);
    public float AirborneResistance => Mathf.Clamp01(airborneResistance);
    public float Sterility => Mathf.Clamp01(sterility);
    public float Durability => Mathf.Max(1f, durability);
    public float WeightMultiplier => Mathf.Max(0.01f, weightMultiplier);
    public float DryingRate => Mathf.Clamp(dryingRate, 0.05f, 2f);
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string itemId,
        string name,
        string details,
        TextileMaterialTag materialTags,
        float warmthValue,
        float heatValue,
        float waterValue,
        float airValue,
        float sterilityValue,
        float durabilityValue,
        float weight,
        float drying,
        string researchId)
    {
        materialId = stableId?.Trim() ?? string.Empty;
        physicalItemId = itemId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        description = details?.Trim() ?? string.Empty;
        tags = materialTags;
        warmth = Mathf.Clamp01(warmthValue);
        heatResistance = Mathf.Clamp01(heatValue);
        waterResistance = Mathf.Clamp01(waterValue);
        airborneResistance = Mathf.Clamp01(airValue);
        sterility = Mathf.Clamp01(sterilityValue);
        durability = Mathf.Max(1f, durabilityValue);
        weightMultiplier = Mathf.Max(0.01f, weight);
        dryingRate = Mathf.Clamp(drying, 0.05f, 2f);
        requiredResearchId = researchId?.Trim() ?? string.Empty;
    }
#endif
}
