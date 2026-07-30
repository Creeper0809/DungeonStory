using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

public enum IndustrialOverlayKind
{
    Power = 0,
    CleanWater = 1,
    Wastewater = 2,
    Conveyor = 3
}

public interface IIndustrialInfrastructureOverlayService
{
    bool IsVisible(IndustrialOverlayKind kind);
    void SetVisible(IndustrialOverlayKind kind, bool visible);
}

public sealed class IndustrialInfrastructureOverlayPresenter :
    IIndustrialInfrastructureOverlayService,
    IInitializable,
    ITickable,
    IDisposable
{
    private readonly IBuildingWorldQuery buildings;
    private readonly Dictionary<IndustrialOverlayKind, bool> visibility =
        new Dictionary<IndustrialOverlayKind, bool>();
    private readonly List<GameObject> markers = new List<GameObject>();
    private GameObject root;
    private Sprite markerSprite;
    private int renderedBuildingVersion = int.MinValue;
    private bool dirty = true;

    public IndustrialInfrastructureOverlayPresenter(
        IBuildingWorldQuery buildings)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        foreach (IndustrialOverlayKind kind in
                 Enum.GetValues(typeof(IndustrialOverlayKind)))
        {
            visibility[kind] = false;
        }
    }

    public void Initialize()
    {
        root = new GameObject("IndustrialInfrastructureOverlay");
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "IndustrialOverlayPixel",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        markerSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        markerSprite.name = "IndustrialOverlayMarker";
    }

    public bool IsVisible(IndustrialOverlayKind kind) =>
        visibility.TryGetValue(kind, out bool visible) && visible;

    public void SetVisible(IndustrialOverlayKind kind, bool visible)
    {
        if (IsVisible(kind) == visible)
        {
            return;
        }

        visibility[kind] = visible;
        dirty = true;
    }

    public void Tick()
    {
        if (root == null
            || !dirty
            && renderedBuildingVersion == buildings.BuildingVersion)
        {
            return;
        }

        Rebuild();
    }

    public void Dispose()
    {
        ClearMarkers();
        if (markerSprite != null)
        {
            UnityEngine.Object.Destroy(markerSprite.texture);
            UnityEngine.Object.Destroy(markerSprite);
        }

        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }
    }

    private void Rebuild()
    {
        ClearMarkers();
        renderedBuildingVersion = buildings.BuildingVersion;
        dirty = false;
        if (!AnyVisible())
        {
            return;
        }

        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building == null
                || building.IsGridDestroyed
                || building.BuildingData == null)
            {
                continue;
            }

            BuildingUtilityConnectionAbility utility =
                building.BuildingData
                    .GetAbility<BuildingUtilityConnectionAbility>();
            if (IsVisible(IndustrialOverlayKind.Power)
                && utility != null
                && (utility.channels & UtilityChannel.Power) != 0)
            {
                AddMarker(building, IndustrialOverlayKind.Power, -0.27f);
            }

            if (IsVisible(IndustrialOverlayKind.CleanWater)
                && utility != null
                && (utility.channels & UtilityChannel.CleanWater) != 0)
            {
                AddMarker(building, IndustrialOverlayKind.CleanWater, -0.09f);
            }

            if (IsVisible(IndustrialOverlayKind.Wastewater)
                && utility != null
                && (utility.channels & UtilityChannel.Wastewater) != 0)
            {
                AddMarker(building, IndustrialOverlayKind.Wastewater, 0.09f);
            }

            if (IsVisible(IndustrialOverlayKind.Conveyor)
                && (building.BuildingData.layer == GridLayer.Conveyor
                    || building.BuildingData
                            .GetAbility<BuildingConveyorPortAbility>()
                        != null))
            {
                AddMarker(building, IndustrialOverlayKind.Conveyor, 0.27f);
            }
        }
    }

    private void AddMarker(
        BuildableObject building,
        IndustrialOverlayKind kind,
        float xOffset)
    {
        GameObject marker = new GameObject($"Overlay_{kind}");
        marker.transform.SetParent(root.transform, false);
        marker.transform.position = building.transform.position
            + new Vector3(xOffset, 0.36f, 0f);
        marker.transform.localScale = new Vector3(0.14f, 0.72f, 1f);
        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = markerSprite;
        renderer.color = ResolveColor(kind);
        renderer.sortingLayerName = "DungeonFrontObject";
        renderer.sortingOrder = 85;
        markers.Add(marker);
    }

    private bool AnyVisible()
    {
        foreach (bool visible in visibility.Values)
        {
            if (visible)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearMarkers()
    {
        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i] != null)
            {
                UnityEngine.Object.Destroy(markers[i]);
            }
        }

        markers.Clear();
    }

    private static Color ResolveColor(IndustrialOverlayKind kind)
    {
        return kind switch
        {
            IndustrialOverlayKind.Power =>
                new Color(1f, 0.72f, 0.18f, 0.72f),
            IndustrialOverlayKind.CleanWater =>
                new Color(0.18f, 0.78f, 1f, 0.72f),
            IndustrialOverlayKind.Wastewater =>
                new Color(0.76f, 0.25f, 0.66f, 0.72f),
            _ => new Color(0.88f, 0.88f, 0.28f, 0.72f)
        };
    }
}
