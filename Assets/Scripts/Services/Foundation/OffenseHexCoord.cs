using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct OffenseHexCoord :
    IEquatable<OffenseHexCoord>,
    IComparable<OffenseHexCoord>
{
    public OffenseHexCoord(int q, int r)
    {
        Q = q;
        R = r;
    }

    public int Q { get; }
    public int R { get; }
    public int S => -Q - R;

    public int DistanceTo(OffenseHexCoord other)
    {
        return (Mathf.Abs(Q - other.Q)
            + Mathf.Abs(R - other.R)
            + Mathf.Abs(S - other.S)) / 2;
    }

    public OffenseHexCoord Neighbor(int direction)
    {
        OffenseHexCoord offset = Directions[
            ((direction % Directions.Length) + Directions.Length)
            % Directions.Length];
        return new OffenseHexCoord(Q + offset.Q, R + offset.R);
    }

    public int CompareTo(OffenseHexCoord other)
    {
        int qComparison = Q.CompareTo(other.Q);
        return qComparison != 0 ? qComparison : R.CompareTo(other.R);
    }

    public bool Equals(OffenseHexCoord other) => Q == other.Q && R == other.R;
    public override bool Equals(object obj) =>
        obj is OffenseHexCoord other && Equals(other);
    public override int GetHashCode() => unchecked((Q * 397) ^ R);
    public override string ToString() => $"{Q},{R}";

    public static bool operator ==(
        OffenseHexCoord left,
        OffenseHexCoord right) => left.Equals(right);

    public static bool operator !=(
        OffenseHexCoord left,
        OffenseHexCoord right) => !left.Equals(right);

    public static readonly OffenseHexCoord[] Directions =
    {
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, 0),
        new(-1, 1),
        new(0, 1)
    };
}
