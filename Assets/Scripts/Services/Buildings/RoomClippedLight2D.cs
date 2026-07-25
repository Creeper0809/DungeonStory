using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class RoomClippedLight2D : MonoBehaviour
{
    private const float CellWorldHeight = 3f;
    private const float EdgeInset = 0.08f;
    private const float PreferredFalloffSize = 0.75f;
    private const float MinimumSize = 0.15f;
    private const float MinimumCoreSize = 0.08f;
    private const int RoundedShapePointCount = 32;
    private const float CoreRadiusScale = 0.55f;
    private const float VerticalRadiusScale = 0.75f;

    private readonly RoomLayoutCache roomLayoutCache = new RoomLayoutCache();
    private BuildableObject source;
    private Light2D targetLight;
    private float configuredRadius = 1f;
    private int lastGridVersion = -1;
    private int lastRoomId = -1;
    private Vector3 lastLightPosition;
    private Rect currentWorldRect;
    private Rect currentCoreRect;
    private float currentFalloffSize;
    private int currentShapePointCount;
    private bool hasCurrentWorldRect;

    public Rect CurrentWorldRect => currentWorldRect;
    public Rect CurrentCoreRect => currentCoreRect;
    public float CurrentFalloffSize => currentFalloffSize;
    public int CurrentShapePointCount => currentShapePointCount;
    public bool HasCurrentWorldRect => hasCurrentWorldRect;

    public void Configure(BuildableObject sourceBuilding, Light2D light, float radius)
    {
        source = sourceBuilding;
        targetLight = light;
        configuredRadius = Mathf.Max(0.2f, radius);
        lastGridVersion = -1;
        lastRoomId = -1;
        lastLightPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        ForceRefresh();
    }

    public void ForceRefresh()
    {
        RefreshShape(force: true);
    }

    private void LateUpdate()
    {
        RefreshShape(force: false);
    }

    private void RefreshShape(bool force)
    {
        if (source == null || targetLight == null || source.Grid == null)
        {
            return;
        }

        Grid grid = source.Grid;
        RoomInstance room = ResolveRoom(grid);
        int roomId = room != null ? room.Id : -1;
        if (!force
            && lastGridVersion == grid.version
            && lastRoomId == roomId
            && lastLightPosition == transform.position)
        {
            return;
        }

        lastGridVersion = grid.version;
        lastRoomId = roomId;
        lastLightPosition = transform.position;

        Rect roomRect = room != null && room.IsUsable && !room.IsSelfContained
            ? GetRoomWorldRect(grid, room)
            : GetSourceCellWorldRect(grid);
        Rect clippedRect = ClipToRadius(roomRect, transform.position, configuredRadius);
        ApplyFreeformShape(clippedRect);
    }

    private RoomInstance ResolveRoom(Grid grid)
    {
        if (source == null || grid == null)
        {
            return null;
        }

        if (source.buildPoses != null)
        {
            foreach (Vector2Int position in source.buildPoses)
            {
                if (roomLayoutCache.TryGetRoom(grid, position, out RoomInstance room))
                {
                    return room;
                }
            }
        }

        return roomLayoutCache.TryGetRoom(grid, source.centerPos, out RoomInstance centerRoom)
            ? centerRoom
            : null;
    }

    private static Rect GetRoomWorldRect(Grid grid, RoomInstance room)
    {
        RectInt bounds = room.Bounds;
        int minGridX = bounds.xMin;
        int maxGridX = bounds.xMax - 1;
        int minGridY = bounds.yMin;
        int maxGridY = bounds.yMax - 1;

        float left = grid.GetWorldPos(new Vector2Int(maxGridX, minGridY)).x - 0.5f + EdgeInset;
        float right = grid.GetWorldPos(new Vector2Int(minGridX, minGridY)).x + 0.5f - EdgeInset;
        float bottom = grid.GetWorldPos(new Vector2Int(minGridX, minGridY)).y + EdgeInset;
        float top = grid.GetWorldPos(new Vector2Int(minGridX, maxGridY)).y + CellWorldHeight - EdgeInset;
        return Rect.MinMaxRect(left, bottom, right, top);
    }

    private Rect GetSourceCellWorldRect(Grid grid)
    {
        Vector2Int cell = source != null ? source.centerPos : grid.GetXY(transform.position);
        float centerX = grid.GetWorldPos(cell).x;
        float bottom = grid.GetWorldPos(cell).y;
        return Rect.MinMaxRect(
            centerX - 0.5f + EdgeInset,
            bottom + EdgeInset,
            centerX + 0.5f - EdgeInset,
            bottom + CellWorldHeight - EdgeInset);
    }

    private static Rect ClipToRadius(Rect bounds, Vector3 center, float radius)
    {
        float left = Mathf.Max(bounds.xMin, center.x - radius);
        float right = Mathf.Min(bounds.xMax, center.x + radius);
        float bottom = Mathf.Max(bounds.yMin, center.y - radius);
        float top = Mathf.Min(bounds.yMax, center.y + radius);

        if (right - left < MinimumSize)
        {
            float x = Mathf.Clamp(center.x, bounds.xMin, bounds.xMax);
            left = Mathf.Max(bounds.xMin, x - MinimumSize * 0.5f);
            right = Mathf.Min(bounds.xMax, x + MinimumSize * 0.5f);
        }

        if (top - bottom < MinimumSize)
        {
            float y = Mathf.Clamp(center.y, bounds.yMin, bounds.yMax);
            bottom = Mathf.Max(bounds.yMin, y - MinimumSize * 0.5f);
            top = Mathf.Min(bounds.yMax, y + MinimumSize * 0.5f);
        }

        return Rect.MinMaxRect(left, bottom, right, top);
    }

    private void ApplyFreeformShape(Rect worldRect)
    {
        float falloffSize = GetFalloffSize(worldRect);
        Rect coreLimit = InsetRect(worldRect, falloffSize);
        Vector2 origin = new Vector2(transform.position.x, transform.position.y);
        Vector3[] shape = BuildRoundedShape(coreLimit, falloffSize, origin);
        Rect coreRect = GetWorldBounds(shape, origin);

        targetLight.lightType = Light2D.LightType.Freeform;
        targetLight.shapeLightFalloffSize = falloffSize;
        targetLight.SetShapePath(shape);
        currentWorldRect = worldRect;
        currentCoreRect = coreRect;
        currentFalloffSize = falloffSize;
        currentShapePointCount = shape.Length;
        hasCurrentWorldRect = true;
    }

    private Vector3[] BuildRoundedShape(Rect coreLimit, float falloffSize, Vector2 origin)
    {
        Vector2 center = ClampPointToRect(origin, coreLimit);
        float naturalRadius = Mathf.Max(MinimumCoreSize, configuredRadius * CoreRadiusScale);
        float radiusX = Mathf.Min(naturalRadius, Mathf.Max(MinimumCoreSize, coreLimit.width * 0.5f));
        float radiusY = Mathf.Min(naturalRadius * VerticalRadiusScale, Mathf.Max(MinimumCoreSize, coreLimit.height * 0.5f));
        Vector3[] shape = new Vector3[RoundedShapePointCount];

        for (int i = 0; i < shape.Length; i++)
        {
            float angle = (Mathf.PI * 2f * i) / shape.Length;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float ellipseDistance = GetEllipseDistance(direction, radiusX, radiusY);
            float roomDistance = DistanceToRectAlongRay(center, direction, coreLimit);
            float distance = Mathf.Max(MinimumCoreSize, Mathf.Min(ellipseDistance, roomDistance));
            Vector2 worldPoint = center + direction * distance;
            worldPoint.x = Mathf.Clamp(worldPoint.x, coreLimit.xMin, coreLimit.xMax);
            worldPoint.y = Mathf.Clamp(worldPoint.y, coreLimit.yMin, coreLimit.yMax);
            shape[i] = new Vector3(worldPoint.x - origin.x, worldPoint.y - origin.y, 0f);
        }

        return shape;
    }

    private static Vector2 ClampPointToRect(Vector2 point, Rect rect)
    {
        return new Vector2(
            Mathf.Clamp(point.x, rect.xMin, rect.xMax),
            Mathf.Clamp(point.y, rect.yMin, rect.yMax));
    }

    private static float GetEllipseDistance(Vector2 direction, float radiusX, float radiusY)
    {
        float denominator = Mathf.Sqrt(
            (direction.x * direction.x) / (radiusX * radiusX)
            + (direction.y * direction.y) / (radiusY * radiusY));
        return denominator > 0.0001f
            ? 1f / denominator
            : MinimumCoreSize;
    }

    private static float DistanceToRectAlongRay(Vector2 origin, Vector2 direction, Rect rect)
    {
        float bestDistance = float.PositiveInfinity;

        if (Mathf.Abs(direction.x) > 0.0001f)
        {
            float xEdge = direction.x > 0f ? rect.xMax : rect.xMin;
            float distance = (xEdge - origin.x) / direction.x;
            if (distance > 0f)
            {
                bestDistance = Mathf.Min(bestDistance, distance);
            }
        }

        if (Mathf.Abs(direction.y) > 0.0001f)
        {
            float yEdge = direction.y > 0f ? rect.yMax : rect.yMin;
            float distance = (yEdge - origin.y) / direction.y;
            if (distance > 0f)
            {
                bestDistance = Mathf.Min(bestDistance, distance);
            }
        }

        return float.IsInfinity(bestDistance) || float.IsNaN(bestDistance)
            ? MinimumCoreSize
            : Mathf.Max(MinimumCoreSize, bestDistance);
    }

    private static Rect GetWorldBounds(Vector3[] shape, Vector2 origin)
    {
        if (shape == null || shape.Length == 0)
        {
            return Rect.MinMaxRect(origin.x, origin.y, origin.x, origin.y);
        }

        float minX = origin.x + shape[0].x;
        float maxX = minX;
        float minY = origin.y + shape[0].y;
        float maxY = minY;
        for (int i = 1; i < shape.Length; i++)
        {
            float x = origin.x + shape[i].x;
            float y = origin.y + shape[i].y;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static float GetFalloffSize(Rect worldRect)
    {
        float maxInset = Mathf.Max(
            0.01f,
            Mathf.Min(worldRect.width, worldRect.height) * 0.5f - MinimumCoreSize);
        return Mathf.Min(PreferredFalloffSize, maxInset);
    }

    private static Rect InsetRect(Rect rect, float inset)
    {
        float safeInsetX = Mathf.Min(inset, Mathf.Max(0f, rect.width * 0.5f - MinimumCoreSize));
        float safeInsetY = Mathf.Min(inset, Mathf.Max(0f, rect.height * 0.5f - MinimumCoreSize));
        return Rect.MinMaxRect(
            rect.xMin + safeInsetX,
            rect.yMin + safeInsetY,
            rect.xMax - safeInsetX,
            rect.yMax - safeInsetY);
    }
}
