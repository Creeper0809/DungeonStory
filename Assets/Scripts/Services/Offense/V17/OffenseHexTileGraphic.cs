using UnityEngine;
using UnityEngine.UI;

public sealed class OffenseHexTileGraphic : MaskableGraphic
{
    [SerializeField] private Color borderColor =
        new Color(0.08f, 0.09f, 0.1f, 0.9f);
    [SerializeField, Range(0f, 0.45f)] private float borderRatio = 0.12f;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radiusX = rect.width * 0.5f;
        float radiusY = rect.height * 0.5f;

        AddHex(vertexHelper, center, radiusX, radiusY, borderColor);
        AddHex(
            vertexHelper,
            center,
            radiusX * (1f - borderRatio),
            radiusY * (1f - borderRatio),
            color);
    }

    private static void AddHex(
        VertexHelper vertexHelper,
        Vector2 center,
        float radiusX,
        float radiusY,
        Color tint)
    {
        int centerIndex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(center, tint, Vector2.zero);
        for (int index = 0; index < 6; index++)
        {
            float angle = Mathf.Deg2Rad * (60f * index);
            Vector2 point = center + new Vector2(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY);
            vertexHelper.AddVert(point, tint, Vector2.zero);
        }

        for (int index = 0; index < 6; index++)
        {
            vertexHelper.AddTriangle(
                centerIndex,
                centerIndex + 1 + index,
                centerIndex + 1 + (index + 1) % 6);
        }
    }
}
