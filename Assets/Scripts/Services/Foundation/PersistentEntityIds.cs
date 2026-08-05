using System;

public interface IPersistentEntityId
{
    string Value { get; }
    bool IsValid { get; }
}

public readonly struct ItemInstanceId : IPersistentEntityId, IEquatable<ItemInstanceId>
{
    private readonly string value;
    public ItemInstanceId(string value) => this.value = PersistentEntityId.Normalize(value);
    public string Value => value ?? string.Empty;
    public bool IsValid => PersistentEntityId.IsKind(Value, "item-instance");
    public bool Equals(ItemInstanceId other) => PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) => obj is ItemInstanceId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator ItemInstanceId(string value) => new(value);
}

public readonly struct ItemStackId : IPersistentEntityId, IEquatable<ItemStackId>
{
    private readonly string value;
    public ItemStackId(string value) => this.value = PersistentEntityId.Normalize(value);
    public string Value => value ?? string.Empty;
    public bool IsValid => PersistentEntityId.IsKind(Value, "stack");
    public bool Equals(ItemStackId other) => PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) => obj is ItemStackId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator ItemStackId(string value) => new(value);
}

public readonly struct CharacterId : IPersistentEntityId, IEquatable<CharacterId>
{
    public static readonly CharacterId Owner = new("owner");
    private readonly string value;
    public CharacterId(string value) => this.value = PersistentEntityId.Normalize(value);
    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public bool Equals(CharacterId other) => PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) => obj is CharacterId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator CharacterId(string value) => new(value);
}

public readonly struct BuildingInstanceId : IPersistentEntityId, IEquatable<BuildingInstanceId>
{
    private readonly string value;
    public BuildingInstanceId(string value) => this.value = PersistentEntityId.Normalize(value);
    public string Value => value ?? string.Empty;
    public bool IsValid => PersistentEntityId.IsKind(Value, "building");
    public bool Equals(BuildingInstanceId other) => PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) => obj is BuildingInstanceId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator BuildingInstanceId(string value) => new(value);
}

public readonly struct WildlifeHabitatPatchId : IPersistentEntityId, IEquatable<WildlifeHabitatPatchId>
{
    private readonly string value;
    public WildlifeHabitatPatchId(string value) => this.value = PersistentEntityId.Normalize(value);
    public string Value => value ?? string.Empty;
    public bool IsValid => PersistentEntityId.IsKind(Value, "wildlife-habitat");
    public bool Equals(WildlifeHabitatPatchId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is WildlifeHabitatPatchId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator WildlifeHabitatPatchId(string value) => new(value);
}

public static class PersistentEntityId
{
    public static string Normalize(string value) => value?.Trim() ?? string.Empty;

    public static bool IsKind(string value, string kind) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith(kind + ":", StringComparison.Ordinal)
        && value.Length > kind.Length + 1;

    public static bool Equals(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    public static int GetHashCode(string value) =>
        StringComparer.Ordinal.GetHashCode(value ?? string.Empty);

    /// <summary>
    /// Returns a process-independent FNV-1a hash for deterministic scheduling.
    /// Unlike <see cref="string.GetHashCode()"/>, this value is stable across runs.
    /// </summary>
    public static uint GetStableHash32(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return hash;
        }
    }

    public static uint GetStableHash32(CharacterId id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "A valid CharacterId is required for deterministic hashing.",
                nameof(id));
        }

        return GetStableHash32(id.Value);
    }

    public static float GetStableUnitFraction(CharacterId id) =>
        (GetStableHash32(id) & 0x00ffffffu) / 16777216f;
}
