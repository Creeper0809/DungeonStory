using System;

public readonly struct CharacterArchetypeId : IEquatable<CharacterArchetypeId>
{
    private readonly string value;

    public CharacterArchetypeId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public bool Equals(CharacterArchetypeId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is CharacterArchetypeId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator CharacterArchetypeId(string value) => new(value);
}
