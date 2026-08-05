using System;

public enum CircusCombatantKind
{
    Character,
    Wildlife
}

public readonly struct CircusCombatantIdentity : IEquatable<CircusCombatantIdentity>
{
    public CircusCombatantIdentity(
        CircusCombatantKind kind,
        string id)
    {
        Kind = kind;
        Id = id?.Trim() ?? string.Empty;
        if (Id.Length == 0)
        {
            throw new ArgumentException(
                "A stable combatant ID is required.",
                nameof(id));
        }
    }

    public CircusCombatantKind Kind { get; }
    public string Id { get; }

    public bool Equals(CircusCombatantIdentity other) =>
        Kind == other.Kind
        && string.Equals(Id, other.Id, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is CircusCombatantIdentity other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Kind;
            foreach (char character in Id)
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }
}
