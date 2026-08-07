using System;

public readonly struct CharacterTraitId : IEquatable<CharacterTraitId>
{
    private readonly string value;

    public CharacterTraitId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public bool Equals(CharacterTraitId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is CharacterTraitId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator CharacterTraitId(string value) => new(value);
}
