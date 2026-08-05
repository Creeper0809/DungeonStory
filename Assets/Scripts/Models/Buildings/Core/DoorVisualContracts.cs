using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class DoorVisualMaterial
{
    public const string ShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    public static void Apply(SpriteRenderer renderer, Material material)
    {
        if (renderer == null)
        {
            return;
        }

        if (material != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class DungeonDoorVisualLayout
{
    public const string VisualObjectName = "DungeonDoorVisual";
    public const string SortingLayerName = "Wall";
    public const int SortingOrder = 99;
    public const string CeilingSortingLayerName = "Wall";
    public const int CeilingSortingOrder = 100;
    public const string TraversalSortingLayerName = "DungeonMiddleObject";
    public const string DefaultCharacterSortingLayerName = "Default";
    public const float VisualWidth = 3f;
    public const float VisualHeight = 3f;
    public static readonly Vector2 TraversalColliderSize = new Vector2(3f, 1f);
    public static readonly Vector2 TraversalColliderOffset = new Vector2(2f, 0.5f);

    public static Vector3 CalculateScale(Sprite sprite)
    {
        if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
        {
            return Vector3.one;
        }

        return new Vector3(
            VisualWidth / sprite.bounds.size.x,
            VisualHeight / sprite.bounds.size.y,
            1f);
    }
}
