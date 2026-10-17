using System;


public readonly struct CharacterBackgroundId : IEquatable<CharacterBackgroundId>
{
    public CharacterBackgroundId(string value) => Value = value?.Trim() ?? string.Empty;
    public string Value { get; }
    public bool IsValid => Value.StartsWith("background:", StringComparison.Ordinal);
    public bool Equals(CharacterBackgroundId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is CharacterBackgroundId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
}

public readonly struct CharacterAmbitionId : IEquatable<CharacterAmbitionId>
{
    public CharacterAmbitionId(string value) => Value = value?.Trim() ?? string.Empty;
    public string Value { get; }
    public bool IsValid => Value != null
        && Value.StartsWith("ambition:", StringComparison.Ordinal);
    public bool Equals(CharacterAmbitionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is CharacterAmbitionId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
}

public readonly struct SpeciesCultureId : IEquatable<SpeciesCultureId>
{
    public SpeciesCultureId(string value) => Value = value?.Trim() ?? string.Empty;
    public string Value { get; }
    public bool IsValid => Value != null
        && Value.StartsWith("culture:", StringComparison.Ordinal);
    public bool Equals(SpeciesCultureId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is SpeciesCultureId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
}

public readonly struct NarrativeEventId : IEquatable<NarrativeEventId>
{
    public NarrativeEventId(string value) => Value = value?.Trim() ?? string.Empty;
    public string Value { get; }
    public bool IsValid => Value.StartsWith("life-event:", StringComparison.Ordinal);
    public bool Equals(NarrativeEventId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is NarrativeEventId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
}
