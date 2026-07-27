using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

public enum CombatPositionReservationKind
{
    Move = 0,
    Melee = 1,
    Ranged = 2,
    Cover = 3,
    Rescue = 4
}

[Serializable]
public sealed class CombatPositionReservation
{
    public string reservationId = string.Empty;
    public string actorId = string.Empty;
    public string targetId = string.Empty;
    public CombatPositionReservationKind kind;
    public int x;
    public int y;
    public float targetScore;

    public Vector2Int Cell
    {
        get => new Vector2Int(x, y);
        set
        {
            x = value.x;
            y = value.y;
        }
    }

    public CombatPositionReservation Clone()
    {
        return (CombatPositionReservation)MemberwiseClone();
    }
}

[Serializable]
public sealed class DefenseTacticalCoordinatorSaveData
{
    public List<CombatPositionReservation> reservations =
        new List<CombatPositionReservation>();
}

public interface IDefenseTacticalCoordinator
{
    IReadOnlyList<CombatPositionReservation> Reservations { get; }
    bool IsReservedForOther(string actorId, Vector2Int cell);
    bool CanAssignTarget(string actorId, string targetId, int maximumAttackers = 2);
    bool ShouldKeepTarget(
        string actorId,
        string currentTargetId,
        float currentScore,
        string candidateTargetId,
        float candidateScore);
    bool TryReserve(
        string actorId,
        string targetId,
        Vector2Int cell,
        CombatPositionReservationKind kind,
        float targetScore,
        out string failureReason);
    bool TryGetReservation(string actorId, out CombatPositionReservation reservation);
    void Release(string actorId);
    DefenseTacticalCoordinatorSaveData Capture();
    void Restore(DefenseTacticalCoordinatorSaveData saveData, IList<string> warnings);
}

public sealed class DefenseTacticalCoordinator :
    IDefenseTacticalCoordinator,
    IInitializable,
    ITickable,
    IDisposable
{
    private const float TargetSwitchThreshold = 25f;
    private readonly IGridSystemProvider gridProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly Dictionary<string, CombatPositionReservation> byActor =
        new Dictionary<string, CombatPositionReservation>(StringComparer.Ordinal);
    private readonly List<string> tickActorIds = new List<string>();
    private IReadOnlyList<CombatPositionReservation> view =
        Array.Empty<CombatPositionReservation>();
    private bool viewDirty = true;
    private int sequence;

    public DefenseTacticalCoordinator(
        IGridSystemProvider gridProvider,
        ICharacterAiWorldRegistry worldRegistry)
    {
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
    }

    public IReadOnlyList<CombatPositionReservation> Reservations
    {
        get
        {
            if (viewDirty)
            {
                view = byActor.Values
                    .OrderBy(item => item.actorId, StringComparer.Ordinal)
                    .Select(item => item.Clone())
                    .ToArray();
                viewDirty = false;
            }

            return view;
        }
    }

    public void Initialize()
    {
    }

    public void Tick()
    {
        if (byActor.Count == 0)
        {
            return;
        }

        tickActorIds.Clear();
        foreach (string actorId in byActor.Keys)
        {
            tickActorIds.Add(actorId);
        }

        for (int index = 0; index < tickActorIds.Count; index++)
        {
            string actorId = tickActorIds[index];
            CharacterActor actor = FindCharacter(actorId);
            if (actor == null || actor.IsDead
                || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
            {
                Release(actorId);
            }
        }
    }

    public void Dispose()
    {
        byActor.Clear();
        tickActorIds.Clear();
        viewDirty = true;
    }

    public bool IsReservedForOther(string actorId, Vector2Int cell)
    {
        return byActor.Values.Any(item =>
            item != null
            && item.Cell == cell
            && !string.Equals(item.actorId, actorId, StringComparison.Ordinal));
    }

    public bool CanAssignTarget(string actorId, string targetId, int maximumAttackers = 2)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return true;
        }

        int assigned = byActor.Values.Count(item =>
            item != null
            && string.Equals(item.targetId, targetId, StringComparison.Ordinal)
            && !string.Equals(item.actorId, actorId, StringComparison.Ordinal));
        return assigned < Mathf.Max(1, maximumAttackers);
    }

    public bool ShouldKeepTarget(
        string actorId,
        string currentTargetId,
        float currentScore,
        string candidateTargetId,
        float candidateScore)
    {
        if (string.IsNullOrWhiteSpace(currentTargetId)
            || string.Equals(currentTargetId, candidateTargetId, StringComparison.Ordinal))
        {
            return true;
        }

        return candidateScore < currentScore + TargetSwitchThreshold;
    }

    public bool TryReserve(
        string actorId,
        string targetId,
        Vector2Int cell,
        CombatPositionReservationKind kind,
        float targetScore,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(actorId))
        {
            failureReason = "전술 위치를 예약할 캐릭터가 없습니다.";
            return false;
        }

        if (!gridProvider.TryGetGrid(out Grid grid)
            || !grid.IsValidGridPos(cell)
            || !grid.IsWalkable(cell))
        {
            failureReason = "전술 위치로 사용할 수 없는 칸입니다.";
            return false;
        }

        if (IsReservedForOther(actorId, cell))
        {
            failureReason = "다른 전투원이 이미 예약한 위치입니다.";
            return false;
        }

        if (!CanAssignTarget(actorId, targetId))
        {
            failureReason = "해당 목표에는 이미 충분한 전투원이 배치되었습니다.";
            return false;
        }

        if (!byActor.TryGetValue(actorId, out CombatPositionReservation reservation))
        {
            reservation = new CombatPositionReservation
            {
                reservationId = $"combat-position:{++sequence}",
                actorId = actorId
            };
            byActor[actorId] = reservation;
        }

        reservation.targetId = targetId ?? string.Empty;
        reservation.Cell = cell;
        reservation.kind = kind;
        reservation.targetScore = targetScore;
        viewDirty = true;
        return true;
    }

    public bool TryGetReservation(
        string actorId,
        out CombatPositionReservation reservation)
    {
        reservation = null;
        if (string.IsNullOrWhiteSpace(actorId)
            || !byActor.TryGetValue(actorId, out CombatPositionReservation stored))
        {
            return false;
        }

        reservation = stored.Clone();
        return true;
    }

    public void Release(string actorId)
    {
        if (!string.IsNullOrWhiteSpace(actorId) && byActor.Remove(actorId))
        {
            viewDirty = true;
        }
    }

    public DefenseTacticalCoordinatorSaveData Capture()
    {
        return new DefenseTacticalCoordinatorSaveData
        {
            reservations = byActor.Values
                .Select(item => item.Clone())
                .ToList()
        };
    }

    public void Restore(
        DefenseTacticalCoordinatorSaveData saveData,
        IList<string> warnings)
    {
        byActor.Clear();
        if (saveData == null || !gridProvider.TryGetGrid(out Grid grid))
        {
            viewDirty = true;
            return;
        }

        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        foreach (CombatPositionReservation source in saveData.reservations
            ?? new List<CombatPositionReservation>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.actorId)
                || FindCharacter(source.actorId) == null
                || !grid.IsValidGridPos(source.Cell)
                || !grid.IsWalkable(source.Cell)
                || !cells.Add(source.Cell)
                || byActor.ContainsKey(source.actorId))
            {
                warnings?.Add("무효하거나 중복된 전술 위치 예약을 해제했습니다.");
                continue;
            }

            CombatPositionReservation restored = source.Clone();
            restored.reservationId = string.IsNullOrWhiteSpace(source.reservationId)
                ? $"combat-position:{++sequence}"
                : source.reservationId;
            byActor[restored.actorId] = restored;
        }

        viewDirty = true;
    }

    private CharacterActor FindCharacter(string actorId)
    {
        return worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.Identity?.PersistentId ?? $"character:{actor.GetInstanceID()}",
                actorId,
                StringComparison.Ordinal));
    }
}
