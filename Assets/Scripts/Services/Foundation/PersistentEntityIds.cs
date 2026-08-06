using System;
using System.Globalization;

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
    private const string Kind = "character";
    private const string OwnerValue = "owner";
    public static readonly CharacterId Owner = new(OwnerValue);
    private readonly string value;
    public CharacterId(string value) => this.value = PersistentEntityId.Normalize(value);
    public string Value => value ?? string.Empty;
    public bool IsValid =>
        PersistentEntityId.Equals(Value, OwnerValue)
        || PersistentEntityId.IsKind(Value, Kind);
    public bool Equals(CharacterId other) => PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) => obj is CharacterId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator CharacterId(string value) => new(value);

    /// <summary>
    /// Creates a character identity while preserving an existing runtime entity ID as
    /// the stable suffix. The source ID remains usable in its own aggregate (for example,
    /// <c>invasion:...</c>) while the returned ID is unambiguously character-scoped.
    /// </summary>
    public static CharacterId FromStableSuffix(string stableSuffix)
    {
        string suffix = PersistentEntityId.Normalize(stableSuffix);
        if (string.IsNullOrWhiteSpace(suffix))
        {
            throw new ArgumentException(
                "A non-empty stable suffix is required for a CharacterId.",
                nameof(stableSuffix));
        }

        if (PersistentEntityId.IsKind(suffix, Kind)
            || PersistentEntityId.Equals(suffix, OwnerValue))
        {
            throw new ArgumentException(
                "The stable suffix must not already be a CharacterId.",
                nameof(stableSuffix));
        }

        return new CharacterId($"{Kind}:{suffix}");
    }

    /// <summary>
    /// Resolves CharacterId formats emitted by early V18 builds without
    /// weakening the canonical runtime contract. This method is for restore input
    /// only; newly captured state must already contain <c>owner</c> or
    /// <c>character:*</c>.
    /// </summary>
    public static bool TryCanonicalizeV18Restore(
        string restoreValue,
        out CharacterId canonicalId,
        out bool wasLegacy)
    {
        if (restoreValue == null
            || !string.Equals(
                restoreValue,
                restoreValue.Trim(),
                StringComparison.Ordinal))
        {
            canonicalId = default;
            wasLegacy = false;
            return false;
        }

        string normalized = restoreValue;
        CharacterId current = new CharacterId(normalized);
        if (current.IsValid)
        {
            canonicalId = current;
            wasLegacy = false;
            return true;
        }

        if (!IsLegacyV18GeneratedId(normalized))
        {
            canonicalId = default;
            wasLegacy = false;
            return false;
        }

        canonicalId = new CharacterId($"{Kind}:{normalized}");
        wasLegacy = true;
        return canonicalId.IsValid;
    }

    private static bool IsLegacyV18GeneratedId(string value)
    {
        if (IsLegacyOperationalActorId(value))
        {
            return true;
        }

        string[] segments = value?.Split(':') ?? Array.Empty<string>();
        if (segments.Length != 3
            || !int.TryParse(
                segments[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int runSeed))
        {
            return false;
        }

        if (string.Equals(segments[0], "world", StringComparison.Ordinal))
        {
            return int.TryParse(
                    segments[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int serial)
                && serial >= 0
                && string.Equals(
                    value,
                    $"world:{runSeed.ToString(CultureInfo.InvariantCulture)}:{serial:D6}",
                    StringComparison.Ordinal);
        }

        return string.Equals(segments[0], "staff", StringComparison.Ordinal)
            && int.TryParse(
                segments[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int index)
            && index > 0
            && string.Equals(
                value,
                $"staff:{runSeed.ToString(CultureInfo.InvariantCulture)}:{index:D2}",
                StringComparison.Ordinal);
    }

    private static bool IsLegacyOperationalActorId(string value)
    {
        string[] segments = value?.Split(':') ?? Array.Empty<string>();
        if (segments.Length == 2
            && string.Equals(segments[0], "invasion", StringComparison.Ordinal))
        {
            return Guid.TryParseExact(segments[1], "N", out Guid invasionId)
                && string.Equals(
                    value,
                    $"invasion:{invasionId:N}",
                    StringComparison.Ordinal);
        }

        if (segments.Length == 4
            && string.Equals(segments[0], "faction-route", StringComparison.Ordinal))
        {
            return IsExactPositiveInteger(segments[1])
                && string.Equals(segments[2], "ally", StringComparison.Ordinal)
                && IsExactPositiveInteger(segments[3]);
        }

        if (segments.Length == 4
            && string.Equals(segments[0], "return", StringComparison.Ordinal))
        {
            return IsExactPositiveInteger(segments[1])
                && string.Equals(segments[2], "prisoner", StringComparison.Ordinal)
                && IsExactPositiveInteger(segments[3]);
        }

        return segments.Length == 4
            && string.Equals(segments[0], "incident", StringComparison.Ordinal)
            && IsLegacyExteriorIncidentKind(segments[1])
            && IsExactPositiveInteger(segments[2])
            && string.Equals(segments[3], "actor", StringComparison.Ordinal);
    }

    private static bool IsExactPositiveInteger(string value) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int parsed)
        && parsed > 0
        && string.Equals(
            value,
            parsed.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    private static bool IsLegacyExteriorIncidentKind(string value) =>
        value is "MerchantCart"
            or "Informant"
            or "Thief"
            or "InjuredReturnee"
            or "PredatorApproach"
            or "CargoDamage";
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
