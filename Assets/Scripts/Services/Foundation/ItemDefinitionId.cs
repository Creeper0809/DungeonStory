using System;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Stable physical-item definition identity shared by content and runtime domains.
/// The constructor intentionally preserves the pre-move trimming behavior; strict
/// pre-canonicalized construction is a later, separately verified migration.
/// </summary>
[Serializable]
[MovedFrom(true, sourceAssembly: "DungeonStory.Economy")]
public readonly struct ItemDefinitionId : IEquatable<ItemDefinitionId>
{
    private readonly string value;

    public ItemDefinitionId(string value)
    {
        this.value = Normalize(value);
    }

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public bool Equals(ItemDefinitionId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is ItemDefinitionId other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static explicit operator ItemDefinitionId(string value) => new(value);

    public static string Normalize(string value) =>
        value?.Trim() ?? string.Empty;
}
