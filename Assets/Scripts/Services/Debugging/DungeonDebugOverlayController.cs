using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using VContainer.Unity;

public sealed class DungeonDebugDelegateOverlayProvider : IDungeonDebugOverlayProvider
{
    private readonly Action<DungeonDebugOverlayRenderContext> render;

    public DungeonDebugDelegateOverlayProvider(
        DungeonDebugOverlayKind kind,
        Action<DungeonDebugOverlayRenderContext> render)
    {
        Kind = kind;
        this.render = render ?? throw new ArgumentNullException(nameof(render));
    }

    public DungeonDebugOverlayKind Kind { get; }

    public void Render(DungeonDebugOverlayRenderContext context)
    {
        render(context);
    }
}

public sealed class DungeonDebugWorldOverlayController :
    IStartable,
    ITickable,
    IDisposable
{
    private readonly IDungeonDebugModeService modeService;
    private readonly DungeonDebugPaletteUiController palette;
    private readonly IGridSystemProvider gridProvider;
    private readonly IMainCameraProvider cameraProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWildlifeRuntime wildlifeRuntime;
    private readonly IWildlifeEcosystemRuntime ecosystemRuntime;
    private readonly IWorldWaterQuery waterQuery;
    private readonly IWorldFilthQuery filthQuery;
    private readonly IDefenseEngagementRuntime defenseRuntime;
    private readonly List<IDungeonDebugOverlayProvider> providers =
        new List<IDungeonDebugOverlayProvider>();
    private DungeonDebugOverlayRenderer renderer;

    public DungeonDebugWorldOverlayController(
        IDungeonDebugModeService modeService,
        DungeonDebugPaletteUiController palette,
        IGridSystemProvider gridProvider,
        IMainCameraProvider cameraProvider,
        ITmpKoreanFontService fontService,
        IRoomLayoutCache roomLayoutCache,
        IWorldItemStackRuntime itemRuntime,
        IWildlifeRuntime wildlifeRuntime,
        IWildlifeEcosystemRuntime ecosystemRuntime,
        IWorldWaterQuery waterQuery,
        IWorldFilthQuery filthQuery,
        IDefenseEngagementRuntime defenseRuntime)
    {
        this.modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
        this.palette = palette ?? throw new ArgumentNullException(nameof(palette));
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.cameraProvider = cameraProvider ?? throw new ArgumentNullException(nameof(cameraProvider));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.roomLayoutCache = roomLayoutCache ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.wildlifeRuntime = wildlifeRuntime ?? throw new ArgumentNullException(nameof(wildlifeRuntime));
        this.ecosystemRuntime = ecosystemRuntime ?? throw new ArgumentNullException(nameof(ecosystemRuntime));
        this.waterQuery = waterQuery ?? throw new ArgumentNullException(nameof(waterQuery));
        this.filthQuery = filthQuery ?? throw new ArgumentNullException(nameof(filthQuery));
        this.defenseRuntime = defenseRuntime ?? throw new ArgumentNullException(nameof(defenseRuntime));
    }

    public void Start()
    {
        Transform parent = DungeonRuntimeHierarchy.GetCategory(DungeonRuntimeHierarchy.Debug);
        renderer = new DungeonDebugOverlayRenderer(parent, fontService.Resolve());
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.Grid, RenderGrid));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.GridOccupancy, RenderOccupancy));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.Rooms, RenderRooms));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.BuildingRanges, RenderBuilding));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.Lighting, RenderLighting));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.CharacterAi, RenderCharacterAi));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.Hauling, RenderHauling));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.Wildlife, RenderWildlife));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.WaterAndFilth, RenderWaterAndFilth));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.ExteriorZones, RenderExterior));
        providers.Add(new DungeonDebugDelegateOverlayProvider(DungeonDebugOverlayKind.Defense, RenderDefense));
    }

    public void Tick()
    {
        if (renderer == null
            || !modeService.IsDeveloperModeEnabled
            || !gridProvider.TryGetGrid(out Grid grid))
        {
            renderer?.Clear();
            return;
        }

        Camera camera = cameraProvider.Camera;
        renderer.BeginFrame();
        DungeonDebugOverlayRenderContext context = new DungeonDebugOverlayRenderContext
        {
            Grid = grid,
            Camera = camera,
            Scope = modeService.OverlayScope,
            Selection = palette.CurrentSelection,
            Renderer = renderer
        };
        foreach (IDungeonDebugOverlayProvider provider in providers)
        {
            if (modeService.IsOverlayEnabled(provider.Kind))
            {
                provider.Render(context);
            }
        }
        renderer.EndFrame();
    }

    public void Dispose()
    {
        renderer?.Dispose();
        renderer = null;
        providers.Clear();
    }

    private void RenderGrid(DungeonDebugOverlayRenderContext context)
    {
        foreach (GridCell cell in EnumerateCells(context))
        {
            context.Renderer.DrawOutline(
                context.Grid,
                cell.Position,
                AreaColor(cell.AreaType, 0.72f),
                0.035f);
            context.Renderer.DrawLabel(
                context.Grid.GetWorldPos(cell.Position) + Vector3.up * 0.28f,
                $"{cell.Position.x},{cell.Position.y}",
                DungeonUiTheme.TextPrimary,
                1.6f);
        }
    }

    private void RenderOccupancy(DungeonDebugOverlayRenderContext context)
    {
        foreach (GridCell cell in EnumerateCells(context))
        {
            int count = cell.GetAllOccupants().Count;
            if (count <= 0)
            {
                continue;
            }
            context.Renderer.DrawCell(
                context.Grid,
                cell.Position,
                new Color(0.86f, 0.27f, 0.24f, Mathf.Clamp(0.14f + count * 0.07f, 0.14f, 0.45f)));
            context.Renderer.DrawLabel(
                context.Grid.GetWorldPos(cell.Position) + Vector3.up * (context.Grid.CellWorldHeight - 0.3f),
                $"{count}",
                DungeonUiTheme.Warning,
                1.8f);
        }
    }

    private void RenderRooms(DungeonDebugOverlayRenderContext context)
    {
        IEnumerable<RoomInstance> rooms;
        if (context.Scope == DungeonDebugOverlayScope.SelectedOnly
            && context.Selection != null
            && context.Selection.HasGridPosition
            && roomLayoutCache.TryGetRoom(
                context.Grid,
                context.Selection.GridPosition,
                out RoomInstance selectedRoom))
        {
            rooms = new[] { selectedRoom };
        }
        else
        {
            rooms = roomLayoutCache.GetLayout(context.Grid).Rooms;
        }

        foreach (RoomInstance room in rooms.Where(room => room != null))
        {
            Color color = room.IsClosed
                ? new Color(0.25f, 0.84f, 0.58f, 0.18f)
                : new Color(0.95f, 0.28f, 0.25f, 0.2f);
            foreach (Vector2Int cell in room.Cells)
            {
                if (IsVisible(context, cell))
                {
                    context.Renderer.DrawCell(context.Grid, cell, color, 0.13f);
                    context.Renderer.DrawOutline(context.Grid, cell, WithAlpha(color, 0.82f), 0.045f);
                }
            }
        }
    }

    private void RenderBuilding(DungeonDebugOverlayRenderContext context)
    {
        BuildableObject building = context.Selection?.Building;
        if (building == null || building.BuildingData == null)
        {
            return;
        }

        GridBuildingPlacement placement = building.BuildingData.Placement;
        for (int x = 0; x < placement.Width; x++)
        {
            for (int y = 0; y < placement.Height; y++)
            {
                Vector2Int cell = building.centerPos + new Vector2Int(x, y);
                context.Renderer.DrawCell(context.Grid, cell, new Color(0.97f, 0.64f, 0.18f, 0.2f));
                context.Renderer.DrawOutline(context.Grid, cell, DungeonUiTheme.Warning, 0.055f);
            }
        }
    }

    private void RenderLighting(DungeonDebugOverlayRenderContext context)
    {
        BuildableObject building = context.Selection?.Building;
        if (building == null)
        {
            return;
        }

        const int radius = 3;
        foreach (GridCell cell in context.Grid.GetCells())
        {
            int distance = Mathf.Abs(cell.Position.x - building.centerPos.x)
                + Mathf.Abs(cell.Position.y - building.centerPos.y);
            if (distance <= radius)
            {
                float alpha = Mathf.Lerp(0.08f, 0.28f, 1f - distance / (float)(radius + 1));
                context.Renderer.DrawCell(context.Grid, cell.Position, new Color(1f, 0.78f, 0.22f, alpha));
            }
        }
    }

    private void RenderCharacterAi(DungeonDebugOverlayRenderContext context)
    {
        CharacterActor actor = context.Selection?.Character;
        if (actor == null)
        {
            return;
        }

        Vector2Int cell = context.Grid.GetXY(actor.transform.position);
        context.Renderer.DrawOutline(context.Grid, cell, DungeonUiTheme.Accent, 0.07f);
        string action = actor.Brain != null && !string.IsNullOrWhiteSpace(actor.Brain.CurrentActionDebugLabel)
            ? actor.Brain.CurrentActionDebugLabel
            : "대기";
        context.Renderer.DrawLabel(
            actor.transform.position + Vector3.up * 2.1f,
            action,
            DungeonUiTheme.Accent,
            2f);
    }

    private void RenderHauling(DungeonDebugOverlayRenderContext context)
    {
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks())
        {
            if (stack == null || stack.State == WorldItemStackState.Stored)
            {
                continue;
            }
            if (context.Scope == DungeonDebugOverlayScope.SelectedOnly
                && context.Selection?.ItemStack?.StackId != stack.StackId)
            {
                continue;
            }
            if (IsVisible(context, stack.Position))
            {
                context.Renderer.DrawOutline(context.Grid, stack.Position, new Color(0.95f, 0.73f, 0.24f, 0.9f), 0.06f);
            }
        }
    }

    private void RenderWildlife(DungeonDebugOverlayRenderContext context)
    {
        if (context.Scope == DungeonDebugOverlayScope.SelectedOnly
            && context.Selection?.Wildlife != null)
        {
            WildlifeActor actor = context.Selection.Wildlife;
            context.Renderer.DrawOutline(context.Grid, actor.GridPosition, DungeonUiTheme.Warning, 0.07f);
            return;
        }

        foreach (WildlifeHabitatPatch patch in ecosystemRuntime.Patches)
        {
            if (patch == null)
            {
                continue;
            }
            Color color = patch.HabitatType == WildlifeHabitatType.Water
                ? new Color(0.2f, 0.66f, 0.96f, 0.16f)
                : new Color(0.34f, 0.82f, 0.34f, 0.14f);
            foreach (GridCell cell in context.Grid.GetCells())
            {
                if (patch.Contains(cell.Position) && IsVisible(context, cell.Position))
                {
                    context.Renderer.DrawCell(context.Grid, cell.Position, color, 0.16f);
                }
            }
        }

        foreach (WildlifeActor actor in wildlifeRuntime.Wildlife.Where(actor => actor != null))
        {
            if (IsVisible(context, actor.GridPosition))
            {
                context.Renderer.DrawOutline(context.Grid, actor.GridPosition, DungeonUiTheme.Warning, 0.05f);
            }
        }
    }

    private void RenderWaterAndFilth(DungeonDebugOverlayRenderContext context)
    {
        foreach (WorldWaterSourceSnapshot water in waterQuery.GetAllSources())
        {
            if (IsVisible(context, water.Position))
            {
                context.Renderer.DrawCell(context.Grid, water.Position, new Color(0.18f, 0.56f, 1f, 0.28f));
                context.Renderer.DrawOutline(context.Grid, water.Position, new Color(0.42f, 0.78f, 1f, 0.92f));
            }
        }

        foreach (WorldFilthSnapshot filth in filthQuery.GetAll())
        {
            if (IsVisible(context, filth.Position))
            {
                context.Renderer.DrawCell(context.Grid, filth.Position, new Color(0.54f, 0.22f, 0.1f, 0.34f));
            }
        }
    }

    private void RenderExterior(DungeonDebugOverlayRenderContext context)
    {
        foreach (GridCell cell in EnumerateCells(context))
        {
            if (cell.AreaType == GridCellAreaType.DungeonInterior)
            {
                continue;
            }
            context.Renderer.DrawCell(context.Grid, cell.Position, AreaColor(cell.AreaType, 0.2f), 0.1f);
            context.Renderer.DrawOutline(context.Grid, cell.Position, AreaColor(cell.AreaType, 0.8f), 0.04f);
        }
    }

    private void RenderDefense(DungeonDebugOverlayRenderContext context)
    {
        foreach (DefenseEngagement engagement in defenseRuntime.ActiveEngagements
                     .Where(engagement => engagement != null && engagement.IsActive))
        {
            context.Renderer.DrawCell(context.Grid, engagement.IntruderStopCell, new Color(0.92f, 0.2f, 0.18f, 0.32f));
            context.Renderer.DrawCell(context.Grid, engagement.GuardCell, new Color(0.2f, 0.76f, 0.5f, 0.32f));
            if (engagement.HasReserveCell)
            {
                context.Renderer.DrawOutline(context.Grid, engagement.ReserveCell, DungeonUiTheme.Warning, 0.06f);
            }
        }
    }

    private static IEnumerable<GridCell> EnumerateCells(DungeonDebugOverlayRenderContext context)
    {
        if (context.Scope == DungeonDebugOverlayScope.SelectedOnly
            && context.Selection != null
            && context.Selection.HasGridPosition)
        {
            GridCell selected = context.Grid.GetGridCell(context.Selection.GridPosition);
            if (selected != null)
            {
                yield return selected;
            }
            yield break;
        }

        foreach (GridCell cell in context.Grid.GetCells())
        {
            if (IsVisible(context, cell.Position))
            {
                yield return cell;
            }
        }
    }

    private static bool IsVisible(DungeonDebugOverlayRenderContext context, Vector2Int position)
    {
        if (context.Camera == null)
        {
            return true;
        }

        Vector3 center = context.Grid.GetWorldPos(position)
            + Vector3.up * (context.Grid.CellWorldHeight * 0.5f);
        Vector3 viewport = context.Camera.WorldToViewportPoint(center);
        return viewport.z >= 0f
            && viewport.x >= -0.08f
            && viewport.x <= 1.08f
            && viewport.y >= -0.08f
            && viewport.y <= 1.08f;
    }

    private static Color AreaColor(GridCellAreaType areaType, float alpha)
    {
        Color color = areaType switch
        {
            GridCellAreaType.DungeonInterior => new Color(0.25f, 0.78f, 0.86f),
            GridCellAreaType.Entrance => new Color(0.28f, 0.9f, 0.5f),
            GridCellAreaType.DropZone => new Color(1f, 0.68f, 0.2f),
            GridCellAreaType.ExteriorPath => new Color(0.28f, 0.58f, 1f),
            GridCellAreaType.BlockedExterior => new Color(0.92f, 0.22f, 0.2f),
            _ => Color.white
        };
        return WithAlpha(color, alpha);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
