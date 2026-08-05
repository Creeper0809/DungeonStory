using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class DefenseTacticalWorldOverlayController :
    IInitializable,
    ITickable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly InvasionDirectorRuntime director;
    private readonly IDefenseRaidAwarenessRuntime awareness;
    private readonly IDefenseFacilityNetworkRuntime facilityNetwork;
    private readonly IGridSystemProvider gridProvider;
    private readonly List<LineRenderer> linkLines =
        new List<LineRenderer>();
    private readonly List<SpriteRenderer> riskMarkers =
        new List<SpriteRenderer>();
    private IDisposable infoSubscription;
    private GameObject root;
    private Material lineMaterial;
    private LineRenderer pathLine;
    private LineRenderer rangeLine;
    private SpriteRenderer breachMarker;
    private InvasionIntruderRuntime selectedIntruder;
    private DefenseFacility selectedFacility;
    private int renderedAwarenessVersion = int.MinValue;
    private int renderedNetworkVersion = int.MinValue;

    public DefenseTacticalWorldOverlayController(
        IGameEventBus events,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IDefenseRaidAwarenessRuntime awareness,
        IDefenseFacilityNetworkRuntime facilityNetwork,
        IGridSystemProvider gridProvider)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        director = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes)))
            .Director
            ?? throw new InvalidOperationException(
                $"{nameof(DefenseTacticalWorldOverlayController)} requires a loaded {nameof(InvasionDirectorRuntime)}.");
        this.awareness = awareness
            ?? throw new ArgumentNullException(nameof(awareness));
        this.facilityNetwork = facilityNetwork
            ?? throw new ArgumentNullException(nameof(facilityNetwork));
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
    }

    public void Initialize()
    {
        infoSubscription = events.Subscribe<InfoFeedEvent>(OnInfoSelected);
        EnsureRoot();
        SetVisible(false);
    }

    public void Tick()
    {
        if (selectedIntruder != null)
        {
            DefenseRaidAwarenessSnapshot snapshot =
                awareness.GetSnapshot(selectedIntruder.RaidId);
            if (snapshot.Version != renderedAwarenessVersion)
            {
                RenderIntruder(snapshot);
            }
        }
        else if (selectedFacility != null
                 && facilityNetwork.Version != renderedNetworkVersion)
        {
            RenderFacility();
        }
    }

    public void Dispose()
    {
        infoSubscription?.Dispose();
        if (lineMaterial != null)
        {
            UnityEngine.Object.Destroy(lineMaterial);
        }
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }
    }

    private void OnInfoSelected(InfoFeedEvent eventType)
    {
        selectedIntruder = null;
        selectedFacility = null;
        if (eventType.Target is CharacterActor actor
            && director != null)
        {
            selectedIntruder = director.ActiveIntruders
                .FirstOrDefault(value =>
                    value != null
                    && value.IntruderActor == actor);
        }
        else if (eventType.Target is BuildingInfoTarget buildingTarget)
        {
            selectedFacility =
                buildingTarget.Building as DefenseFacility;
        }
        else if (eventType.Target is DefenseFacility facility)
        {
            selectedFacility = facility;
        }

        renderedAwarenessVersion = int.MinValue;
        renderedNetworkVersion = int.MinValue;
        SetVisible(selectedIntruder != null || selectedFacility != null);
        if (selectedIntruder != null)
        {
            RenderIntruder(
                awareness.GetSnapshot(selectedIntruder.RaidId));
        }
        else if (selectedFacility != null)
        {
            RenderFacility();
        }
    }

    private void RenderIntruder(
        DefenseRaidAwarenessSnapshot snapshot)
    {
        ClearFacilityLines();
        SetLine(
            pathLine,
            snapshot.ExpectedPath,
            new Color(0.18f, 0.82f, 1f, 0.9f));
        SetLine(rangeLine, Array.Empty<Vector2Int>(), Color.clear);
        EnsureRiskMarkerCount(snapshot.KnownRisks.Count);
        int index = 0;
        foreach (KeyValuePair<Vector2Int, float> risk
                 in snapshot.KnownRisks
                    .OrderBy(pair => pair.Key.y)
                    .ThenBy(pair => pair.Key.x))
        {
            SpriteRenderer marker = riskMarkers[index++];
            marker.gameObject.SetActive(true);
            marker.transform.position =
                GridWorldPosition(risk.Key) + Vector3.back * 0.02f;
            marker.color = new Color(
                1f,
                Mathf.Lerp(0.55f, 0.15f, Mathf.Clamp01(risk.Value / 25f)),
                0.12f,
                0.62f);
        }
        for (; index < riskMarkers.Count; index++)
        {
            riskMarkers[index].gameObject.SetActive(false);
        }

        BuildableObject breach = selectedIntruder.CurrentBreachTarget
            ?? snapshot.BreachTarget;
        breachMarker.gameObject.SetActive(
            breach != null && !breach.isDestroy);
        if (breach != null && !breach.isDestroy)
        {
            breachMarker.transform.position =
                GridWorldPosition(breach.centerPos)
                + Vector3.back * 0.03f;
            breachMarker.color =
                new Color(1f, 0.72f, 0.08f, 0.9f);
            breachMarker.transform.localScale =
                Vector3.one * 1.45f;
        }

        renderedAwarenessVersion = snapshot.Version;
    }

    private void RenderFacility()
    {
        ClearIntruderMarkers();
        if (selectedFacility == null || selectedFacility.isDestroy)
        {
            SetVisible(false);
            return;
        }

        DefenseFacilityNetworkSnapshot snapshot =
            facilityNetwork.GetSnapshot(selectedFacility);
        DefenseFacility[] links =
        {
            snapshot.Detector,
            snapshot.ControlDesk,
            snapshot.SupplyDepot,
            snapshot.MaintenanceBench
        };
        Color[] colors =
        {
            new Color(0.2f, 0.9f, 1f, 0.9f),
            new Color(0.72f, 0.35f, 1f, 0.9f),
            new Color(1f, 0.74f, 0.2f, 0.9f),
            new Color(0.35f, 1f, 0.45f, 0.9f)
        };
        EnsureLinkLineCount(links.Length);
        for (int index = 0; index < links.Length; index++)
        {
            DefenseFacility linked = links[index];
            LineRenderer line = linkLines[index];
            line.gameObject.SetActive(
                linked != null && !linked.isDestroy);
            if (linked == null || linked.isDestroy)
            {
                continue;
            }
            line.startColor = colors[index];
            line.endColor = colors[index];
            line.positionCount = 2;
            line.SetPosition(
                0,
                GridWorldPosition(selectedFacility.centerPos));
            line.SetPosition(
                1,
                GridWorldPosition(linked.centerPos));
        }

        int range = Mathf.Max(
            1,
            selectedFacility.Defense?.range ?? 1);
        Vector2Int center = selectedFacility.centerPos;
        SetLine(
            rangeLine,
            new[]
            {
                center + Vector2Int.up * range,
                center + Vector2Int.right * range,
                center + Vector2Int.down * range,
                center + Vector2Int.left * range,
                center + Vector2Int.up * range
            },
            new Color(0.35f, 0.9f, 1f, 0.45f));
        renderedNetworkVersion = facilityNetwork.Version;
    }

    private void ClearIntruderMarkers()
    {
        pathLine.positionCount = 0;
        breachMarker.gameObject.SetActive(false);
        foreach (SpriteRenderer marker in riskMarkers)
        {
            marker.gameObject.SetActive(false);
        }
    }

    private void ClearFacilityLines()
    {
        foreach (LineRenderer line in linkLines)
        {
            line.gameObject.SetActive(false);
        }
    }

    private void SetLine(
        LineRenderer line,
        IEnumerable<Vector2Int> positions,
        Color color)
    {
        Vector2Int[] cells = positions?.ToArray()
            ?? Array.Empty<Vector2Int>();
        line.positionCount = cells.Length;
        line.startColor = color;
        line.endColor = color;
        for (int index = 0; index < cells.Length; index++)
        {
            line.SetPosition(index, GridWorldPosition(cells[index]));
        }
    }

    private Vector3 GridWorldPosition(Vector2Int cell)
    {
        Grid grid = gridProvider.Grid;
        return grid != null
            ? grid.GetWorldPos(cell) + Vector3.up * 0.08f
            : new Vector3(cell.x, cell.y, 0f);
    }

    private void EnsureRoot()
    {
        if (root != null)
        {
            return;
        }

        root = new GameObject("Defense Tactical World Overlay");
        DungeonRuntimeHierarchy.Parent(
            root,
            DungeonRuntimeHierarchy.WorldUi);
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        pathLine = CreateLine("Expected Path", 0.08f);
        rangeLine = CreateLine("Detection Range", 0.045f);
        GameObject marker = new GameObject("Breach Target");
        marker.transform.SetParent(root.transform, false);
        breachMarker = marker.AddComponent<SpriteRenderer>();
        breachMarker.sprite = GetMarkerSprite();
        breachMarker.sortingLayerName = "UI";
        breachMarker.sortingOrder = 14;
    }

    private LineRenderer CreateLine(string name, float width)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(root.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.textureMode = LineTextureMode.Tile;
        line.sortingLayerName = "UI";
        line.sortingOrder = 13;
        return line;
    }

    private void EnsureLinkLineCount(int count)
    {
        while (linkLines.Count < count)
        {
            linkLines.Add(CreateLine(
                $"Facility Link {linkLines.Count + 1}",
                0.055f));
        }
    }

    private void EnsureRiskMarkerCount(int count)
    {
        while (riskMarkers.Count < count)
        {
            GameObject marker = new GameObject(
                $"Known Risk {riskMarkers.Count + 1}");
            marker.transform.SetParent(root.transform, false);
            SpriteRenderer renderer =
                marker.AddComponent<SpriteRenderer>();
            renderer.sprite = GetMarkerSprite();
            renderer.sortingLayerName = "UI";
            renderer.sortingOrder = 12;
            riskMarkers.Add(renderer);
        }
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.SetActive(visible);
        }
    }

    [RuntimeRebuildableCache] private static Sprite markerSprite;

    private static Sprite GetMarkerSprite()
    {
        if (markerSprite != null)
        {
            return markerSprite;
        }

        Texture2D texture = new Texture2D(
            7,
            7,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Defense Tactical Marker"
        };
        Color[] pixels = new Color[49];
        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                bool border = x == 0 || y == 0 || x == 6 || y == 6;
                pixels[y * 7 + x] = border
                    ? Color.white
                    : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        markerSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 7f, 7f),
            new Vector2(0.5f, 0.5f),
            7f);
        return markerSprite;
    }
}
