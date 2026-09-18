using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct InvasionThreatSubjectSnapshot
{
    public InvasionThreatSubjectSnapshot(CharacterId characterId, string displayName)
    {
        CharacterId = characterId;
        DisplayName = displayName ?? string.Empty;
    }

    public CharacterId CharacterId { get; }
    public string DisplayName { get; }
}

public interface IInvasionThreatSubject
{
    InvasionThreatSubjectSnapshot CaptureInvasionThreatSubject();
}

public struct InvasionThreatWarningEvent
{
    public InvasionThreatSnapshot snapshot;

    public InvasionThreatWarningEvent(InvasionThreatSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

public struct InvasionCandidateEvent
{
    public InvasionThreatSnapshot snapshot;

    public InvasionCandidateEvent(InvasionThreatSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

public struct InvasionStartedEvent
{
    public string runtimeId;
    public InvasionThreatSnapshot snapshot;

    public InvasionStartedEvent(InvasionThreatSnapshot snapshot)
        : this(string.Empty, snapshot)
    {
    }

    public InvasionStartedEvent(
        string runtimeId,
        InvasionThreatSnapshot snapshot)
    {
        this.runtimeId = runtimeId?.Trim() ?? string.Empty;
        this.snapshot = snapshot;
    }
}

/// <summary>
/// The side of the entrance where a committed intruder is currently staging.
/// This is derived only from the entry geometry already selected for execution;
/// it never selects an entrance for presentation.
/// </summary>
public enum InvasionApproachDirection
{
    Unknown = 0,
    East = 1,
    West = 2,
    North = 3,
    South = 4
}

/// <summary>
/// Immutable, non-persistent information from a runtime intrusion that has
/// already been committed. It deliberately contains no projected facility or
/// owner target: those stay unknown until the intruder actually chooses one.
/// </summary>
public readonly struct InvasionCommittedWarningProjection
{
    private const float GeometryEpsilon = 0.0001f;

    public InvasionCommittedWarningProjection(
        string raidId,
        InvasionOperationKind operationKind,
        string enemyArchetypeId,
        string enemyNature,
        Vector2Int entryGridPosition,
        Vector3 outsidePosition,
        Vector3 doorPosition,
        bool hasEntryGeometry)
    {
        RaidId = raidId?.Trim() ?? string.Empty;
        OperationKind = operationKind;
        EnemyArchetypeId = enemyArchetypeId?.Trim() ?? string.Empty;
        EnemyNature = enemyNature?.Trim() ?? string.Empty;
        EntryGridPosition = entryGridPosition;
        OutsidePosition = outsidePosition;
        DoorPosition = doorPosition;
        HasEntryGeometry = hasEntryGeometry
            && IsFinite(outsidePosition)
            && IsFinite(doorPosition);
    }

    public string RaidId { get; }
    public InvasionOperationKind OperationKind { get; }
    public string EnemyArchetypeId { get; }
    public string EnemyNature { get; }
    public Vector2Int EntryGridPosition { get; }
    public Vector3 OutsidePosition { get; }
    public Vector3 DoorPosition { get; }
    public bool HasEntryGeometry { get; }

    public InvasionApproachDirection ApproachDirection
    {
        get
        {
            if (!HasEntryGeometry)
            {
                return InvasionApproachDirection.Unknown;
            }

            float horizontal = OutsidePosition.x - DoorPosition.x;
            float vertical = OutsidePosition.y - DoorPosition.y;
            float horizontalMagnitude = Mathf.Abs(horizontal);
            float verticalMagnitude = Mathf.Abs(vertical);
            if (horizontalMagnitude <= GeometryEpsilon
                && verticalMagnitude <= GeometryEpsilon)
            {
                return InvasionApproachDirection.Unknown;
            }

            if (horizontalMagnitude >= verticalMagnitude)
            {
                return horizontal >= 0f
                    ? InvasionApproachDirection.East
                    : InvasionApproachDirection.West;
            }

            return vertical >= 0f
                ? InvasionApproachDirection.North
                : InvasionApproachDirection.South;
        }
    }

    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x)
        && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y)
        && !float.IsInfinity(value.y)
        && !float.IsNaN(value.z)
        && !float.IsInfinity(value.z);
}

public struct InvasionResolvedEvent
{
    public string runtimeId;
    public bool defended;
    public float residualRisk;

    public InvasionResolvedEvent(bool defended, float residualRisk)
        : this(string.Empty, defended, residualRisk)
    {
    }

    public InvasionResolvedEvent(
        string runtimeId,
        bool defended,
        float residualRisk)
    {
        this.runtimeId = runtimeId?.Trim() ?? string.Empty;
        this.defended = defended;
        this.residualRisk = Mathf.Max(0f, residualRisk);
    }

}

public static class InvasionThreatCalculator
{
    public static float CalculateRisePerSecond(InvasionThreatSettings settings, InvasionThreatFactors factors)
    {
        return CalculateRisePerSecond(settings, factors, 1f);
    }

    public static float CalculateRisePerSecond(InvasionThreatSettings settings, InvasionThreatFactors factors, float runMultiplier)
    {
        if (settings == null)
        {
            return 0f;
        }

        float raw = settings.baseRisePerSecond
            + (factors.dungeonValue * settings.dungeonValueRiseWeight)
            + (factors.reputation * settings.reputationRiseWeight)
            + (factors.time * settings.timeRiseWeight)
            + (factors.risk * settings.riskRiseWeight);

        return Mathf.Max(0f, raw * settings.GetDifficultyMultiplier() * Mathf.Max(0.05f, runMultiplier));
    }

    public static string BuildWarningDetail(InvasionThreatSnapshot snapshot)
    {
        List<string> reasons = new List<string>();
        InvasionThreatFactors factors = snapshot.factors;

        if (factors.dungeonValue >= 3f)
        {
            reasons.Add("던전 가치 상승");
        }

        if (factors.reputation >= 2f)
        {
            reasons.Add("소문 증가");
        }

        if (factors.time >= 1f)
        {
            reasons.Add("마지막 침입 이후 시간 경과");
        }

        if (factors.risk >= 1f)
        {
            reasons.Add("취약한 운영 흔적");
        }

        string reasonText = reasons.Count > 0
            ? string.Join(", ", reasons)
            : "주변 정찰 활동 증가";

        return $"모험가들의 소문이 늘고 있습니다.\n징후: {reasonText}";
    }

    public static string BuildCandidateDetail(InvasionThreatSnapshot snapshot)
    {
        return "수상한 정찰대가 던전 근처에서 목격되었습니다.\n침입이 임박한 것 같습니다.";
    }

    public static string BuildCommittedCandidateDetail(
        InvasionCommittedWarningProjection projection,
        float rallySecondsRemaining,
        string operationObjective,
        float? intelligenceConfidence = null)
    {
        string entry = projection.HasEntryGeometry
            ? projection.EntryGridPosition.ToString()
            : "미상";
        string direction = projection.ApproachDirection switch
        {
            InvasionApproachDirection.East => "동쪽",
            InvasionApproachDirection.West => "서쪽",
            InvasionApproachDirection.North => "북쪽",
            InvasionApproachDirection.South => "남쪽",
            _ => "미상"
        };
        string enemyNature = !string.IsNullOrWhiteSpace(projection.EnemyNature)
            ? projection.EnemyNature
            : "미상";
        string objective = string.IsNullOrWhiteSpace(operationObjective)
            ? "미상"
            : operationObjective.Trim();
        string rallyPhase = rallySecondsRemaining > 0f
            ? $"집결 종료/진입 시도 예상 {Mathf.CeilToInt(rallySecondsRemaining)}초"
            : "진입 중";
        string confidence = intelligenceConfidence.HasValue
            ? $" / 정보 신뢰도 {Mathf.Clamp01(intelligenceConfidence.Value) * 100f:0}%"
            : string.Empty;
        return
            $"입구 {entry} / 접근 방향 {direction}\n" +
            $"{rallyPhase} / 적 성격 {enemyNature}\n" +
            $"작전 {projection.OperationKind} / 목표 {objective}{confidence} / 실제 표적 미상";
    }
}

public readonly struct BossInvasionStartedEvent
{
    public IInvasionThreatSubject Intruder { get; }
    public InvasionThreatSubjectSnapshot IntruderIdentity { get; }
    public InvasionThreatSnapshot Snapshot { get; }

    public BossInvasionStartedEvent(
        IInvasionThreatSubject intruder,
        InvasionThreatSnapshot snapshot)
    {
        Intruder = intruder;
        IntruderIdentity = intruder != null
            ? intruder.CaptureInvasionThreatSubject()
            : default;
        Snapshot = snapshot;
    }
}
