using System;
using UnityEngine;

public enum DoorAccessOverrideKind
{
    None = 0,
    DirectCommand = 1,
    EscortPass = 2,
    CaptiveEscape = 3,
    IntruderBreach = 4
}

public readonly struct GridTraversalContext : IEquatable<GridTraversalContext>
{
    public GridTraversalContext(
        UnityEngine.Object subject,
        DoorAccessOverrideKind overrideKind = DoorAccessOverrideKind.None)
    {
        Subject = subject;
        OverrideKind = overrideKind;
    }

    public UnityEngine.Object Subject { get; }
    public DoorAccessOverrideKind OverrideKind { get; }
    public bool HasSubject => Subject != null;

    public static GridTraversalContext ForCharacter(
        UnityEngine.Object actor,
        DoorAccessOverrideKind overrideKind = DoorAccessOverrideKind.None)
    {
        return new GridTraversalContext(actor, overrideKind);
    }

    public static GridTraversalContext ForWildlife(
        UnityEngine.Object actor,
        DoorAccessOverrideKind overrideKind = DoorAccessOverrideKind.None)
    {
        return new GridTraversalContext(actor, overrideKind);
    }

    public bool Equals(GridTraversalContext other)
    {
        return ReferenceEquals(Subject, other.Subject)
            && OverrideKind == other.OverrideKind;
    }

    public override bool Equals(object obj)
    {
        return obj is GridTraversalContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Subject != null ? Subject.GetInstanceID() : 0;
            return (hash * 397) ^ (int)OverrideKind;
        }
    }
}

public interface IGridTraversalAccessQuery
{
    int DoorAccessVersion { get; }
    bool CanTraverse(
        Grid grid,
        Vector2Int position,
        GridTraversalContext context,
        out string denialReason);
}

public interface IGridPathPerformanceRecorder
{
    bool DetailedCollectionEnabled { get; }
    void RecordGridPathSearch(double elapsedMilliseconds);
}
