using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Buildings/Ability Settings/Lighting", order = 0)]
public sealed class BuildingLightingSettingsSO : ScriptableObject
{
    public const float DefaultInnerRadiusRatio = 0.35f;
    public const float DefaultFalloffIntensity = 0.65f;

    public static readonly Color DefaultColor = new Color(1f, 0.72f, 0.38f, 1f);

    public static readonly string[] DefaultTargetSortingLayers =
    {
        "DungeonHallway",
        "DungeonBackObject",
        "DungeonMiddleObject",
        "DungeonFrontObject"
    };

    [Range(0.05f, 0.95f)] public float innerRadiusRatio = DefaultInnerRadiusRatio;
    [Range(0f, 1f)] public float falloffIntensity = DefaultFalloffIntensity;
    public Color color = DefaultColor;
    public string[] targetSortingLayers = DefaultTargetSortingLayers.ToArray();
}
