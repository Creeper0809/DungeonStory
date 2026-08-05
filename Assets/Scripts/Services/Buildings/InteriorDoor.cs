using UnityEngine;

public static class InteriorDoorVisualLayout
{
    public const string VisualObjectName = "InteriorDoorVisual";
    public const string SortingLayerName = "Wall";
    public const int SortingOrder = 101;
    public const float VisualWidth = 1f;
    public const float VisualHeight = 3f;
    public const float TransparentBottomRatio = 0.25f;
    public const float GroundingOffsetY = -VisualHeight * TransparentBottomRatio;
    public const float OpaqueHeight = VisualHeight * (1f - TransparentBottomRatio);

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

public sealed class InteriorDoor : Door
{
    public override bool IsDungeonEntrance => false;
    protected override bool ChangesCharacterLayerDuringTraversal => false;

    public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)
    {
        base.Initialization(buildingSO, buildPos);
        ConfigureVisual(buildingSO);
    }

    private void ConfigureVisual(BuildingSO buildingSO)
    {
        Sprite sprite = buildingSO != null
            ? buildingSO.sprite != null ? buildingSO.sprite : buildingSO.icon
            : null;
        Transform visualTransform = transform.Find(InteriorDoorVisualLayout.VisualObjectName);
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject(InteriorDoorVisualLayout.VisualObjectName);
            visualTransform = visualObject.transform;
            visualTransform.SetParent(transform, false);
        }

        VisualRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (VisualRenderer == null)
        {
            VisualRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        visualTransform.localPosition = new Vector3(0f, InteriorDoorVisualLayout.GroundingOffsetY, 0f);
        visualTransform.localRotation = Quaternion.identity;
        visualTransform.localScale = InteriorDoorVisualLayout.CalculateScale(sprite);
        VisualRenderer.sprite = sprite;
        VisualRenderer.color = Color.white;
        DoorVisualMaterial.Apply(VisualRenderer, DoorVisualMaterialAsset);
        VisualRenderer.sortingLayerName = InteriorDoorVisualLayout.SortingLayerName;
        VisualRenderer.sortingOrder = InteriorDoorVisualLayout.SortingOrder;
        VisualRenderer.enabled = sprite != null;
    }
}
