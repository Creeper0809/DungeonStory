using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

internal sealed class CropPlotVisual
{
    public GameObject Root;
    public SpriteRenderer[] Renderers = Array.Empty<SpriteRenderer>();
}

public sealed class CropPlotVisualPresenter :
    ITickable,
    IDisposable
{
    private const float RefreshInterval = 0.25f;
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CropPlotVisualPresenter.Tick");

    private readonly ICropPlotRuntime cropPlots;
    private readonly IUiClock uiClock;
    private readonly Dictionary<string, CropPlotVisual> visuals =
        new Dictionary<string, CropPlotVisual>(StringComparer.Ordinal);
    private readonly List<CropPlotVisualState> visualStates =
        new List<CropPlotVisualState>();
    private readonly HashSet<string> seen =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> removedIds = new List<string>();
    private Sprite markerSprite;
    private float nextRefreshTime;

    public CropPlotVisualPresenter(
        ICropPlotRuntime cropPlots,
        IUiClock uiClock)
    {
        this.cropPlots = cropPlots ?? throw new ArgumentNullException(nameof(cropPlots));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            if (uiClock.Time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = uiClock.Time + RefreshInterval;
            Synchronize();
        }
    }

    public void Dispose()
    {
        foreach (CropPlotVisual visual in visuals.Values)
        {
            DestroyObject(visual.Root);
        }

        visuals.Clear();
        DestroyObject(markerSprite);
        markerSprite = null;
    }

    private void Synchronize()
    {
        cropPlots.CopyVisualStates(visualStates);
        seen.Clear();
        for (int index = 0; index < visualStates.Count; index++)
        {
            CropPlotVisualState plot = visualStates[index];
            BuildableObject building = plot.Building;
            if (building == null || building.isDestroy)
            {
                continue;
            }

            seen.Add(plot.PlotId);
            if (!visuals.TryGetValue(plot.PlotId, out CropPlotVisual visual)
                || visual.Root == null)
            {
                visual = CreateVisual(building, plot.PlotId);
                visuals[plot.PlotId] = visual;
            }

            UpdateVisual(visual, plot);
        }

        removedIds.Clear();
        foreach (string plotId in visuals.Keys)
        {
            if (!seen.Contains(plotId))
            {
                removedIds.Add(plotId);
            }
        }

        for (int index = 0; index < removedIds.Count; index++)
        {
            string removedId = removedIds[index];
            DestroyObject(visuals[removedId].Root);
            visuals.Remove(removedId);
        }
    }

    private CropPlotVisual CreateVisual(
        BuildableObject building,
        string plotId)
    {
        GameObject root = new GameObject($"CropVisual_{plotId}");
        root.transform.SetParent(building.transform, false);
        root.transform.localPosition = new Vector3(0f, 0.12f, -0.03f);
        int count = Mathf.Clamp(building.BuildingData?.width ?? 3, 2, 5);
        SpriteRenderer[] renderers = new SpriteRenderer[count];
        for (int index = 0; index < count; index++)
        {
            GameObject sprout = new GameObject($"Sprout_{index + 1}");
            sprout.transform.SetParent(root.transform, false);
            float spacing = 0.42f;
            sprout.transform.localPosition = new Vector3(
                (index - ((count - 1) * 0.5f)) * spacing,
                0f,
                0f);
            SpriteRenderer renderer = sprout.AddComponent<SpriteRenderer>();
            renderer.sprite = GetMarkerSprite();
            renderer.sortingLayerName = "DungeonBackObject";
            renderer.sortingOrder = 112;
            renderers[index] = renderer;
        }

        return new CropPlotVisual
        {
            Root = root,
            Renderers = renderers
        };
    }

    private static void UpdateVisual(
        CropPlotVisual visual,
        CropPlotVisualState plot)
    {
        float growth = plot.Phase switch
        {
            CropPlotPhase.Growing => Mathf.Max(0.08f, plot.GrowthProgress),
            CropPlotPhase.ReadyToHarvest => 1f,
            CropPlotPhase.Harvesting => 1f,
            _ => 0f
        };
        bool visible = growth > 0f;
        Color baseColor = ResolveCropColor(plot.CropId);
        for (int index = 0; index < visual.Renderers.Length; index++)
        {
            SpriteRenderer renderer = visual.Renderers[index];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = visible;
            if (!visible)
            {
                continue;
            }

            float variation = 0.9f + (index % 2) * 0.12f;
            renderer.transform.localScale = new Vector3(
                0.16f + growth * 0.12f,
                (0.14f + growth * 0.52f) * variation,
                1f);
            renderer.transform.localPosition = new Vector3(
                renderer.transform.localPosition.x,
                growth * 0.18f,
                0f);
            renderer.color = new Color(
                baseColor.r,
                baseColor.g,
                baseColor.b,
                0.65f + growth * 0.35f);
        }
    }

    private Sprite GetMarkerSprite()
    {
        if (markerSprite != null)
        {
            return markerSprite;
        }

        markerSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0f),
            1f);
        markerSprite.name = "CropPlotMarkerSprite";
        markerSprite.hideFlags = HideFlags.HideAndDontSave;
        return markerSprite;
    }

    private static Color ResolveCropColor(string cropId)
    {
        int hash = StringComparer.Ordinal.GetHashCode(cropId ?? string.Empty);
        float hue = Mathf.Repeat(Mathf.Abs(hash % 997) / 997f, 1f);
        return Color.HSVToRGB(hue, 0.62f, 0.88f);
    }

    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
