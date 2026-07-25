using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

public interface ICombatTacticalOverlayPresenter
{
    bool IsVisible { get; }
    int ActiveCellCount { get; }
    void Clear();
}

public sealed class CombatTacticalOverlayPresenter :
    ICombatTacticalOverlayPresenter,
    IStartable,
    ITickable,
    IDisposable
{
    private static readonly Color ContactColor = new Color(0.91f, 0.24f, 0.2f, 0.8f);
    private static readonly Color ShortColor = new Color(0.95f, 0.55f, 0.18f, 0.75f);
    private static readonly Color MediumColor = new Color(0.93f, 0.78f, 0.22f, 0.72f);
    private static readonly Color LongColor = new Color(0.24f, 0.72f, 0.92f, 0.72f);
    private static readonly Color ClearLineColor = new Color(0.2f, 0.9f, 0.55f, 0.85f);
    private static readonly Color RiskLineColor = new Color(0.95f, 0.2f, 0.22f, 0.9f);

    private readonly IGridSystemProvider gridProvider;
    private readonly IPlayerCombatCommandSource ownerCommands;
    private readonly ITmpKoreanFontService fontService;
    private readonly ICharacterCombatCommandRuntime commandRuntime;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICombatLineOfSightService lineOfSight;
    private readonly ICombatCoverQuery coverQuery;
    private readonly IDefenseTacticalCoordinator tacticalCoordinator;
    private DungeonDebugOverlayRenderer renderer;

    public CombatTacticalOverlayPresenter(
        IGridSystemProvider gridProvider,
        IPlayerCombatCommandSource ownerCommands,
        ITmpKoreanFontService fontService,
        ICharacterCombatCommandRuntime commandRuntime,
        ICombatEquipmentRuntime equipment,
        ICombatLineOfSightService lineOfSight,
        ICombatCoverQuery coverQuery,
        IDefenseTacticalCoordinator tacticalCoordinator)
    {
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.ownerCommands = ownerCommands
            ?? throw new ArgumentNullException(nameof(ownerCommands));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.commandRuntime = commandRuntime ?? throw new ArgumentNullException(nameof(commandRuntime));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.lineOfSight = lineOfSight ?? throw new ArgumentNullException(nameof(lineOfSight));
        this.coverQuery = coverQuery ?? throw new ArgumentNullException(nameof(coverQuery));
        this.tacticalCoordinator = tacticalCoordinator
            ?? throw new ArgumentNullException(nameof(tacticalCoordinator));
    }

    public bool IsVisible { get; private set; }
    public int ActiveCellCount => renderer?.ActiveCellCount ?? 0;

    public void Start()
    {
        Transform parent = DungeonRuntimeHierarchy.GetCategory(DungeonRuntimeHierarchy.Combat);
        renderer = new DungeonDebugOverlayRenderer(
            parent,
            fontService.Resolve(),
            "CombatTacticalWorldOverlay");
    }

    public void Tick()
    {
        if (renderer == null || !gridProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        IReadOnlyList<CharacterActor> selected = ownerCommands?.SelectedActors;
        if (selected == null || selected.Count == 0)
        {
            CharacterActor single = ownerCommands?.SelectedActor;
            selected = single != null
                ? new[] { single }
                : Array.Empty<CharacterActor>();
        }

        CharacterActor[] combatants = selected
            .Where(actor => actor != null
                && !actor.IsDead
                && commandRuntime.IsInCombatStance(actor))
            .Distinct()
            .ToArray();
        if (combatants.Length == 0)
        {
            Clear();
            return;
        }

        renderer.BeginFrame();
        foreach (CharacterActor actor in combatants)
        {
            RenderActor(grid, actor);
        }
        renderer.EndFrame();
        IsVisible = true;
    }

    public void Clear()
    {
        renderer?.Clear();
        IsVisible = false;
    }

    public void Dispose()
    {
        renderer?.Dispose();
        renderer = null;
        IsVisible = false;
    }

    private void RenderActor(Grid grid, CharacterActor actor)
    {
        string actorId = GetId(actor);
        equipment.TryGetActiveWeapon(actorId, out CombatWeaponSnapshot weapon);
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();
        Vector2Int origin = actor.GetNowXY();
        RenderRangeRing(grid, origin, 1, ContactColor);
        if (weapon.IsRanged)
        {
            RenderRangeRing(grid, origin, Mathf.Min(5, weapon.MaximumRange), ShortColor);
            if (weapon.MaximumRange >= 6)
            {
                RenderRangeRing(grid, origin, Mathf.Min(11, weapon.MaximumRange), MediumColor);
            }
            if (weapon.MaximumRange >= 12)
            {
                RenderRangeRing(grid, origin, weapon.MaximumRange, LongColor);
            }
        }

        if (tacticalCoordinator.TryGetReservation(
                actorId,
                out CombatPositionReservation reservation))
        {
            renderer.DrawCell(
                grid,
                reservation.Cell,
                reservation.kind == CombatPositionReservationKind.Cover
                    ? new Color(0.18f, 0.74f, 0.9f, 0.2f)
                    : new Color(0.25f, 0.85f, 0.5f, 0.18f),
                0.14f);
            renderer.DrawOutline(grid, reservation.Cell, ClearLineColor, 0.055f);
            renderer.DrawLabel(
                grid.GetWorldPos(reservation.Cell) + Vector3.up * (grid.CellWorldHeight + 0.18f),
                ReservationLabel(reservation.kind),
                Color.white,
                1.45f);
        }

        CharacterCombatCommand command = commandRuntime.ActiveCommands.FirstOrDefault(item =>
            item != null && string.Equals(item.actorId, actorId, StringComparison.Ordinal));
        if (command == null || !command.hasTargetCell || !grid.IsValidGridPos(command.TargetCell))
        {
            return;
        }

        CombatLineOfSightResult sight = lineOfSight.Evaluate(
            grid,
            origin,
            command.TargetCell,
            actorId,
            command.targetId);
        Color lineColor = sight.HasLineOfSight && !sight.FriendlyFireRisk
            ? ClearLineColor
            : RiskLineColor;
        Vector3 from = grid.GetWorldPos(origin) + Vector3.up * grid.CellWorldHeight * 0.65f;
        Vector3 to = grid.GetWorldPos(command.TargetCell) + Vector3.up * grid.CellWorldHeight * 0.65f;
        renderer.DrawLine(from, to, lineColor, 0.055f);
        foreach (Vector2Int cell in sight.TraversedCells)
        {
            renderer.DrawCell(grid, cell, new Color(lineColor.r, lineColor.g, lineColor.b, 0.09f), 0.24f);
        }

        if (!sight.HasLineOfSight && grid.IsValidGridPos(sight.BlockingCell))
        {
            renderer.DrawCell(grid, sight.BlockingCell, new Color(1f, 0.12f, 0.12f, 0.38f), 0.08f);
            renderer.DrawLabel(
                grid.GetWorldPos(sight.BlockingCell) + Vector3.up * (grid.CellWorldHeight + 0.2f),
                "차단",
                Color.white,
                1.4f);
        }

        foreach (CombatShotTraceOccupant occupant in sight.Trace.IntermediateOccupants)
        {
            if (!occupant.IsProtected)
            {
                continue;
            }

            renderer.DrawOutline(grid, occupant.Cell, RiskLineColor, 0.08f);
            renderer.DrawLabel(
                grid.GetWorldPos(occupant.Cell) + Vector3.up * (grid.CellWorldHeight + 0.22f),
                "오발 위험",
                RiskLineColor,
                1.35f);
        }

        CombatCoverSnapshot cover = coverQuery.GetCover(grid, origin, command.TargetCell);
        if (cover.Height != CombatCoverHeight.None)
        {
            float chance = cover.BaseBlockChance * cover.GetDirectionalMultiplier();
            renderer.DrawLabel(
                to + Vector3.up * 0.35f,
                $"엄폐 {Mathf.RoundToInt(chance * 100f)}%",
                chance > 0.5f ? LongColor : MediumColor,
                1.45f);
        }
    }

    private void RenderRangeRing(
        Grid grid,
        Vector2Int origin,
        int radius,
        Color color)
    {
        radius = Mathf.Max(1, radius);
        for (int dx = -radius; dx <= radius; dx++)
        {
            int dy = radius - Mathf.Abs(dx);
            DrawRingCell(grid, origin + new Vector2Int(dx, dy), color);
            if (dy != 0)
            {
                DrawRingCell(grid, origin + new Vector2Int(dx, -dy), color);
            }
        }
    }

    private void DrawRingCell(Grid grid, Vector2Int cell, Color color)
    {
        if (grid.IsValidGridPos(cell))
        {
            renderer.DrawOutline(grid, cell, color, 0.035f);
        }
    }

    private static string ReservationLabel(CombatPositionReservationKind kind)
    {
        return kind switch
        {
            CombatPositionReservationKind.Cover => "엄폐 위치",
            CombatPositionReservationKind.Melee => "근접 위치",
            CombatPositionReservationKind.Ranged => "사격 위치",
            CombatPositionReservationKind.Rescue => "구조 위치",
            _ => "이동 위치"
        };
    }

    private static string GetId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId
            ?? (actor != null ? $"character:{actor.GetInstanceID()}" : string.Empty);
    }
}
