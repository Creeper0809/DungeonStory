using System;
using System.Collections.Generic;
using System.Linq;

public static class CharacterStatIds
{
    public const string Attack = "stat:attack";
    public const string Sales = "stat:sales";
    public const string Research = "stat:research";
    public const string MoveSpeed = "stat:move-speed";
    public const string Strength = "stat:strength";
    public const string Toughness = "stat:toughness";
    public const string Dexterity = "stat:dexterity";
    public const string Cleaning = "stat:cleaning";
    public const string Endurance = "stat:endurance";
    public const string Shooting = "stat:shooting";
    public const string Evasion = "stat:evasion";
    public const string Medical = "stat:medical";
}

public sealed class CharacterStatDefinition
{
    public CharacterStatDefinition(
        string id,
        string displayName,
        int sortOrder,
        CharacterStatType? legacyType = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Character stat id is required.", nameof(id));
        }

        Id = id.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        SortOrder = sortOrder;
        LegacyType = legacyType;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
    public CharacterStatType? LegacyType { get; }
}

/// <summary>
/// Fixed protocol mapping between stable stat IDs and the legacy enum.
/// The table is immutable after type initialization; gameplay content must not register globals.
/// </summary>
public static class CharacterStatCatalog
{
    private static readonly CharacterStatDefinition[] Definitions =
    {
        Definition(CharacterStatIds.Attack, "근접", 10, CharacterStatType.Attack),
        Definition(CharacterStatIds.Shooting, "사격", 20, CharacterStatType.Shooting),
        Definition(CharacterStatIds.Evasion, "회피", 30, CharacterStatType.Evasion),
        Definition(CharacterStatIds.Sales, "판매", 40, CharacterStatType.Sales),
        Definition(CharacterStatIds.Research, "연구", 50, CharacterStatType.Research),
        Definition(CharacterStatIds.MoveSpeed, "이동", 60, CharacterStatType.MoveSpeed),
        Definition(CharacterStatIds.Strength, "힘", 70, CharacterStatType.Strength),
        Definition(CharacterStatIds.Toughness, "강인함", 80, CharacterStatType.Toughness),
        Definition(CharacterStatIds.Dexterity, "민첩", 90, CharacterStatType.Dexterity),
        Definition(CharacterStatIds.Cleaning, "청소", 100, CharacterStatType.Cleaning),
        Definition(CharacterStatIds.Endurance, "지구력", 110, CharacterStatType.Endurance),
        Definition(CharacterStatIds.Medical, "의료", 120, CharacterStatType.Medical)
    };

    public static IReadOnlyList<CharacterStatDefinition> All => Definitions;

    public static bool TryGet(string id, out CharacterStatDefinition definition)
    {
        string normalized = id?.Trim() ?? string.Empty;
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, normalized, StringComparison.Ordinal));
        return definition != null;
    }

    public static bool TryGet(CharacterStatType type, out CharacterStatDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => candidate.LegacyType == type);
        return definition != null;
    }

    public static CharacterStatDefinition GetRequired(CharacterStatType type)
    {
        if (TryGet(type, out CharacterStatDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"No character stat definition exists for '{type}'.");
    }

    private static CharacterStatDefinition Definition(
        string id,
        string displayName,
        int sortOrder,
        CharacterStatType legacyType)
    {
        return new CharacterStatDefinition(id, displayName, sortOrder, legacyType);
    }
}
