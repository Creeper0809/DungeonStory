using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class InvasionOwnerEvacuationService :
    IInvasionOwnerEvacuationService,
    IInitializable,
    ITickable,
    IDisposable
{
    private readonly IInvasionIntruderContext invasionContext;
    private readonly InvasionDirectorRuntime director;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IGameEventBus gameEventBus;
    private IDisposable invasionSpawnedSubscription;
    private CharacterActor owner;
    private Coroutine movementRoutine;
    private bool usedAdministrationRoom;
    private OwnerRestoreCandidate restoreCandidate;
    private bool restorePublicationPending;
    private bool previousProjectionRetired;

    private sealed class OwnerRestoreCandidate
    {
        internal bool Active;
        internal CharacterActor Owner;
        internal Vector2Int Target;
        internal bool UsedAdministrationRoom;
        internal string StatusText;
    }

    public InvasionOwnerEvacuationService(
        IInvasionIntruderContext invasionContext,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IRoomLayoutCache roomLayoutCache,
        IGameEventBus gameEventBus)
    {
        this.invasionContext = invasionContext ?? throw new ArgumentNullException(nameof(invasionContext));
        director = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes)))
            .Director
            ?? throw new InvalidOperationException(
                $"{nameof(InvasionOwnerEvacuationService)} requires a loaded {nameof(InvasionDirectorRuntime)}.");
        this.roomLayoutCache = roomLayoutCache ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public bool IsEvacuating { get; private set; }
    public bool HasReachedTarget => owner != null && owner.GetNowXY() == TargetCell;
    public CharacterActor Owner => owner;
    public Vector2Int TargetCell { get; private set; }
    public string StatusText { get; private set; } = string.Empty;

    public void Initialize()
    {
        invasionSpawnedSubscription =
            gameEventBus.Subscribe<InvasionSpawnedEvent>(OnInvasionSpawned);
    }

    public void Dispose()
    {
        invasionSpawnedSubscription?.Dispose();
        invasionSpawnedSubscription = null;
        ReleaseOwner();
    }

    public void Tick()
    {
        if (!IsEvacuating)
        {
            return;
        }

        if (director.ActiveIntruders.Count == 0)
        {
            ReleaseOwner();
        }
    }

    private void OnInvasionSpawned(InvasionSpawnedEvent eventType)
    {
        BeginEvacuation();
    }

    public OwnerEvacuationSaveSnapshot Capture()
    {
        return new OwnerEvacuationSaveSnapshot
        {
            active = IsEvacuating,
            targetX = TargetCell.x,
            targetY = TargetCell.y,
            usedAdministrationRoom = usedAdministrationRoom,
            statusText = StatusText
        };
    }

    public void PrepareRestoreCandidate(
        OwnerEvacuationSaveSnapshot snapshot,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (restoreCandidate != null)
        {
            report.AddError(
                "An owner evacuation restore candidate is already prepared.");
            return;
        }
        if (snapshot == null)
        {
            report.AddError("Owner evacuation restore payload is null.");
            return;
        }

        OwnerRestoreCandidate candidate = new OwnerRestoreCandidate
        {
            Active = snapshot.active,
            UsedAdministrationRoom = snapshot.usedAdministrationRoom,
            StatusText = snapshot.statusText
        };
        if (snapshot.active)
        {
            if (!invasionContext.TryGetOwner(out CharacterActor resolvedOwner)
                || resolvedOwner == null
                || resolvedOwner.IsDead
                || !invasionContext.TryGetGrid(out Grid grid))
            {
                report.AddError(
                    "Owner evacuation restore requires a living owner and active grid.");
                return;
            }

            Vector2Int target = new Vector2Int(snapshot.targetX, snapshot.targetY);
            if (!grid.IsValidGridPos(target) || !grid.IsWalkable(target))
            {
                report.AddError(
                    "Owner evacuation restore target is not a valid walkable cell.");
                return;
            }
            candidate.Owner = resolvedOwner;
            candidate.Target = target;
        }
        restoreCandidate = candidate;
    }

    public void PublishRestoreCandidate()
    {
        if (restoreCandidate == null || restorePublicationPending)
        {
            throw new InvalidOperationException(
                "No owner evacuation restore candidate is ready to publish.");
        }

        restorePublicationPending = true;
        previousProjectionRetired = false;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        restoreCandidate = null;
        restorePublicationPending = false;
        previousProjectionRetired = false;
    }

    public void RetirePreviousRestoreProjection()
    {
        if (!restorePublicationPending || previousProjectionRetired)
        {
            throw new InvalidOperationException(
                "No owner evacuation projection is ready to retire.");
        }

        ReleaseOwner();
        previousProjectionRetired = true;
    }

    public void ActivateRestoreProjection()
    {
        if (!restorePublicationPending || !previousProjectionRetired)
        {
            throw new InvalidOperationException(
                "The previous owner evacuation projection was not retired.");
        }

        OwnerRestoreCandidate candidate = restoreCandidate;
        restoreCandidate = null;
        restorePublicationPending = false;
        previousProjectionRetired = false;
        if (!candidate.Active)
        {
            return;
        }

        owner = candidate.Owner;
        usedAdministrationRoom = candidate.UsedAdministrationRoom;
        StartOwnerMovement(candidate.Target, candidate.StatusText);
    }

    public void CompleteRestoreCandidate()
    {
        RetirePreviousRestoreProjection();
        ActivateRestoreProjection();
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublicationPending)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }

        restoreCandidate = null;
        previousProjectionRetired = false;
    }

    private void BeginEvacuation()
    {
        if (!invasionContext.TryGetOwner(out CharacterActor resolvedOwner)
            || resolvedOwner == null
            || resolvedOwner.IsDead
            || !invasionContext.TryGetGrid(out Grid grid)
            || !TryResolveEvacuationCell(grid, resolvedOwner, out Vector2Int target, out bool administration))
        {
            return;
        }

        owner = resolvedOwner;
        usedAdministrationRoom = administration;
        string status = administration
            ? "사장실로 대피 중"
            : "사장실이 없어 임시 대피 중";
        StartOwnerMovement(target, status);
        if (!administration)
        {
            gameEventBus.RaiseAlert(
                "사장 임시 대피",
                "사용 가능한 사장실이 없어 입구에서 가장 먼 안전 칸으로 이동합니다.",
                EventAlertImportance.High,
                "방어");
        }
    }

    private void StartOwnerMovement(Vector2Int target, string status)
    {
        if (owner == null)
        {
            return;
        }

        if (movementRoutine != null)
        {
            owner.StopCoroutine(movementRoutine);
        }

        owner.GetAbility<AbilityWork>()?.ReleaseAssignedWorkTarget();
        owner.GetAbility<AbilityMove>()?.CancelActiveMovement();
        owner.Brain?.RequestImmediateReplan(clearFailures: false);
        owner.SetAiPaused(true);
        TargetCell = target;
        StatusText = string.IsNullOrWhiteSpace(status) ? "대피 중" : status;
        IsEvacuating = true;
        DefenseCombatPresentation.Ensure(owner)?.SetStatus(
            GetWorldStatusText(StatusText),
            combatActive: false);
        movementRoutine = owner.StartCoroutine(RunEvacuation());
    }

    private IEnumerator RunEvacuation()
    {
        AbilityMove move = owner != null ? owner.GetAbility<AbilityMove>() : null;
        if (move == null || !invasionContext.TryGetGrid(out Grid grid))
        {
            movementRoutine = null;
            yield break;
        }

        for (int attempt = 0; attempt < 3 && owner != null && !owner.IsDead; attempt++)
        {
            if (owner.GetNowXY() == TargetCell)
            {
                break;
            }

            Queue<GridMoveStep> path = grid.GetMovePathTo(owner.GetNowXY(), TargetCell);
            if (path == null || path.Count == 0)
            {
                break;
            }

            yield return move.MoveByPath(path);
        }

        if (owner != null && !owner.IsDead && owner.GetNowXY() == TargetCell)
        {
            StatusText = usedAdministrationRoom ? "사장실 대피 완료" : "임시 대피 완료";
            DefenseCombatPresentation.Ensure(owner)?.SetStatus("대피 완료", combatActive: false);
            owner.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Completed,
                StatusText,
                actionId: "invasion:owner-evacuation",
                sentiment: -0.15f,
                bubbleEligible: true));
        }
        else
        {
            StatusText = "대피 경로 막힘";
        }

        movementRoutine = null;
    }

    private static string GetWorldStatusText(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "대피 중";
        }

        return status.Contains("사장실이 없어", StringComparison.Ordinal)
            ? "임시 대피"
            : status.Contains("사장실", StringComparison.Ordinal)
                ? "사장실 대피"
                : "대피 중";
    }

    private bool TryResolveEvacuationCell(
        Grid grid,
        CharacterActor targetOwner,
        out Vector2Int target,
        out bool administration)
    {
        target = default;
        administration = false;
        GridPathSearchResult search = grid.SearchPath(targetOwner.GetNowXY());
        RoomInstance room = roomLayoutCache.GetLayout(grid).Rooms
            .Where(candidate => candidate != null
                && candidate.IsUsable
                && (candidate.Roles & FacilityRole.Administration) != 0)
            .OrderByDescending(candidate => candidate.Cells.Count)
            .FirstOrDefault(candidate => candidate.Cells.Any(search.ContainsPosition));
        if (room != null)
        {
            Vector2Int[] doorCells = room.Doors
                .Where(door => door != null && !door.isDestroy)
                .Select(door => door.centerPos)
                .ToArray();
            Vector2Int? roomTarget = room.Cells
                .Where(cell => grid.IsWalkable(cell) && search.ContainsPosition(cell))
                .OrderByDescending(cell => DistanceFromNearestDoor(cell, doorCells))
                .ThenByDescending(cell => cell.y)
                .Cast<Vector2Int?>()
                .FirstOrDefault();
            if (roomTarget.HasValue)
            {
                target = roomTarget.Value;
                administration = true;
                return true;
            }
        }

        Vector2Int entry = Vector2Int.zero;
        if (invasionContext.TryResolveEntry(out InvasionIntruderEntry resolvedEntry))
        {
            entry = resolvedEntry.GridPosition;
        }

        GridCell fallback = search.GetReachablePositions()
            .Select(grid.GetGridCell)
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.DungeonInterior
                && grid.IsWalkable(cell.Position))
            .OrderByDescending(cell => Manhattan(cell.Position, entry))
            .ThenByDescending(cell => cell.Position.y)
            .FirstOrDefault();
        if (fallback == null)
        {
            return false;
        }

        target = fallback.Position;
        return true;
    }

    private void ReleaseOwner()
    {
        CharacterActor releasedOwner = owner;
        if (movementRoutine != null && releasedOwner != null)
        {
            releasedOwner.StopCoroutine(movementRoutine);
        }

        movementRoutine = null;
        owner = null;
        IsEvacuating = false;
        usedAdministrationRoom = false;
        StatusText = string.Empty;
        if (releasedOwner == null || releasedOwner.IsDead)
        {
            return;
        }

        releasedOwner.GetAbility<AbilityMove>()?.CancelActiveMovement();
        DefenseCombatPresentation.Ensure(releasedOwner)?.SetStatus(string.Empty, combatActive: false);
        releasedOwner.SetAiPaused(false);
        releasedOwner.Brain?.RequestImmediateReplan(clearFailures: false);
    }

    private static int DistanceFromNearestDoor(Vector2Int cell, IReadOnlyList<Vector2Int> doors)
    {
        if (doors == null || doors.Count == 0)
        {
            return 0;
        }

        int minimum = int.MaxValue;
        for (int i = 0; i < doors.Count; i++)
        {
            minimum = Mathf.Min(minimum, Manhattan(cell, doors[i]));
        }

        return minimum;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
