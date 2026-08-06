using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatPositionReservationKind
{
    Move = 0,
    Melee = 1,
    Ranged = 2,
    Cover = 3,
    Rescue = 4
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefenseTacticalCoordinatorSaveData
{
    public List<CombatPositionReservation> reservations =
        new List<CombatPositionReservation>();
    public int sequence;
}

public readonly struct DefenseTacticalActorSnapshot
{
    public DefenseTacticalActorSnapshot(string actorId, bool isAvailable)
    {
        ActorId = actorId ?? string.Empty;
        IsAvailable = isAvailable;
    }

    public string ActorId { get; }
    public bool IsAvailable { get; }
}

public interface IDefenseTacticalWorldQuery
{
    bool IsOperationalCellWalkable(Vector2Int cell);
    bool HasRestoreGrid { get; }
    bool IsRestoreCellWalkable(Vector2Int cell);
    IReadOnlyList<DefenseTacticalActorSnapshot> CaptureActors();
    IReadOnlyCollection<string> CaptureTargetIds();
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
    DefenseTacticalRestoreCandidate PrepareRestore(
        DefenseTacticalCoordinatorSaveData saveData);
    void PublishRestore(DefenseTacticalRestoreCandidate candidate);
}

public sealed class DefenseTacticalCoordinator :
    IDefenseTacticalCoordinator,
    IInitializable,
    ITickable,
    IDisposable
{
    private const float TargetSwitchThreshold = 25f;
    private readonly IDefenseTacticalWorldQuery worldQuery;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly List<string> tickActorIds = new List<string>();
    private IReadOnlyList<CombatPositionReservation> view =
        Array.Empty<CombatPositionReservation>();
    private bool viewDirty = true;
    private DefenseTacticalAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new DefenseTacticalAggregateState());
    private DefenseTacticalAggregateState writableAggregateState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new DefenseTacticalAggregateState(),
            state => state.Clone());
    private Dictionary<string, CombatPositionReservation> byActor =>
        writableAggregateState.ByActor;
    private int sequence
    {
        get => aggregateState.Sequence;
        set => writableAggregateState.Sequence = value;
    }

    public DefenseTacticalCoordinator(
        IDefenseTacticalWorldQuery worldQuery,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.worldQuery = worldQuery ?? throw new ArgumentNullException(nameof(worldQuery));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
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

        IReadOnlyList<DefenseTacticalActorSnapshot> actors = worldQuery.CaptureActors();
        for (int index = 0; index < tickActorIds.Count; index++)
        {
            string actorId = tickActorIds[index];
            DefenseTacticalActorSnapshot actor = actors.FirstOrDefault(candidate =>
                string.Equals(candidate.ActorId, actorId, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(actor.ActorId) || !actor.IsAvailable)
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
        if (byActor.Count == 0)
        {
            return false;
        }

        foreach (CombatPositionReservation reservation in byActor.Values)
        {
            if (reservation != null
                && reservation.Cell == cell
                && !string.Equals(
                    reservation.actorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool CanAssignTarget(string actorId, string targetId, int maximumAttackers = 2)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return true;
        }

        int assigned = 0;
        foreach (CombatPositionReservation reservation in byActor.Values)
        {
            if (reservation != null
                && string.Equals(
                    reservation.targetId,
                    targetId,
                    StringComparison.Ordinal)
                && !string.Equals(
                    reservation.actorId,
                    actorId,
                    StringComparison.Ordinal))
            {
                assigned++;
            }
        }

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
        string rawActorId = actorId ?? string.Empty;
        CharacterId typedActorId = (CharacterId)rawActorId;
        if (!typedActorId.IsValid
            || !string.Equals(
                typedActorId.Value,
                rawActorId,
                StringComparison.Ordinal))
        {
            failureReason = "전술 위치를 예약할 캐릭터가 없습니다.";
            return false;
        }

        if (!worldQuery.IsOperationalCellWalkable(cell))
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

        CombatPositionReservation reservation;
        if (!aggregateState.ByActor.ContainsKey(actorId))
        {
            if (!TryTakeNextSequence(out int nextSequence, out failureReason))
            {
                return false;
            }
            reservation = new CombatPositionReservation
            {
                reservationId = $"combat-position:{nextSequence}",
                actorId = actorId
            };
            byActor[actorId] = reservation;
        }
        else
        {
            reservation = byActor[actorId];
        }

        reservation.targetId = targetId ?? string.Empty;
        reservation.Cell = cell;
        reservation.kind = kind;
        reservation.targetScore = targetScore;
        viewDirty = true;
        return true;
    }

    private bool TryTakeNextSequence(
        out int nextSequence,
        out string failureReason)
    {
        nextSequence = 0;
        if (sequence == int.MaxValue)
        {
            failureReason = "전술 위치 예약 ID를 더 발급할 수 없습니다.";
            return false;
        }

        nextSequence = checked(sequence + 1);
        sequence = nextSequence;
        failureReason = string.Empty;
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
            sequence = sequence,
            reservations = aggregateState.ByActor.Values
                .Select(item => item.Clone())
                .ToList()
        };
    }

    public DefenseTacticalRestoreCandidate PrepareRestore(
        DefenseTacticalCoordinatorSaveData saveData)
    {
        DungeonGameRestoreReport report = new();
        DefenseTacticalSaveValidation.Validate(
            saveData,
            report,
            worldQuery);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Defense-tactical restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        return new DefenseTacticalRestoreCandidate(
            DefenseTacticalSaveValidation.CreateState(saveData));
    }

    public void PublishRestore(DefenseTacticalRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        aggregateRootStore.Replace(candidate.State);
        view = Array.Empty<CombatPositionReservation>();
        viewDirty = true;
    }

}
