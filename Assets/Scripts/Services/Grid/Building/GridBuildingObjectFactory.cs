using UnityEngine;

public interface IGridBuildingObjectFactory
{
    BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos);
}

public sealed class GridBuildingObjectFactory : IGridBuildingObjectFactory
{
    private const float CellWorldHeight = 3f;
    private const string HallwaySortingLayer = "DungeonHallway";
    private const string MountedFixtureSortingLayer = "DungeonBackObject";
    private const int MountedFixtureSortingOrder = 80;

    public BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos)
    {
        if (grid == null || buildingData == null)
        {
            return null;
        }

        GridBuildingPlacement placement = buildingData.Placement;
        Vector3 evenOffset = new Vector2(0.5f, 0f);
        Vector2 instantiatePos = placement.HasEvenWidth
            ? grid.GetWorldPos(selectPos) + evenOffset
            : grid.GetWorldPos(selectPos);

        GameObject placedObject = new GameObject();
        placedObject.name = buildingData.objectName;
        placedObject.transform.position = instantiatePos;
        DungeonRuntimeHierarchy.Parent(placedObject, DungeonRuntimeHierarchy.Buildings);

        if (buildingData.UsesIndependentRenderer)
        {
            ConfigureIndependentRenderer(placedObject, buildingData);
        }

        if (!placement.IsWall)
        {
            BoxCollider2D collider = placedObject.AddComponent<BoxCollider2D>();
            float footprintWorldHeight = placement.Height * CellWorldHeight;
            collider.size = new Vector2(placement.Width, Mathf.Max(0.1f, footprintWorldHeight - 0.1f));
            collider.offset = new Vector2(0f, footprintWorldHeight * 0.5f);
            collider.isTrigger = buildingData.UsesIndependentRenderer;

            placedObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        }

        if (placedObject.AddComponent(buildingData.type) is BuildableObject buildableObject)
        {
            return buildableObject;
        }

        Debug.Log("Building data type must inherit from BuildableObject.");
        Object.Destroy(placedObject);
        return null;
    }

    private static void ConfigureIndependentRenderer(GameObject placedObject, BuildingSO buildingData)
    {
        if (placedObject == null || buildingData == null || buildingData.sprite == null)
        {
            return;
        }

        Sprite sprite = buildingData.sprite;
        Vector2 spriteSize = sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        Vector2 footprintSize = new Vector2(
            Mathf.Max(1, buildingData.width),
            Mathf.Max(1, buildingData.height) * CellWorldHeight);
        Vector2 visualSize = GridBuildingTileTransformCalculator.GetVisualFootprintSize(footprintSize);
        Vector3 scale = new Vector3(
            visualSize.x / spriteSize.x,
            visualSize.y / spriteSize.y,
            1f);

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(placedObject.transform, false);

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.sortingLayerName = buildingData.layer == GridLayer.FloorOverlay
            ? HallwaySortingLayer
            : MountedFixtureSortingLayer;
        renderer.sortingOrder = buildingData.layer switch
        {
            GridLayer.FloorOverlay => 110,
            _ => MountedFixtureSortingOrder
        };
        visualObject.transform.localScale = scale;

        Vector3 desiredCenter = new Vector3(
            0f,
            GetIndependentVisualCenterY(buildingData.layer, visualSize.y),
            0f);
        Vector3 scaledBoundsCenter = new Vector3(
            sprite.bounds.center.x * scale.x,
            sprite.bounds.center.y * scale.y,
            0f);
        visualObject.transform.localPosition = desiredCenter - scaledBoundsCenter;
    }

    private static float GetIndependentVisualCenterY(GridLayer layer, float visualHeight)
    {
        float halfHeight = Mathf.Max(0.05f, visualHeight * 0.5f);
        return layer switch
        {
            GridLayer.WallFixture => CellWorldHeight - halfHeight,
            GridLayer.CeilingFixture => CellWorldHeight - halfHeight,
            _ => halfHeight
        };
    }
}
