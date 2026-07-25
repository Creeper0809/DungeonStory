using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

public static class CharacterStatCatalog
{
    private static readonly Dictionary<string, CharacterStatDefinition> ById =
        new Dictionary<string, CharacterStatDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<CharacterStatType, CharacterStatDefinition> ByLegacyType =
        new Dictionary<CharacterStatType, CharacterStatDefinition>();
    private static bool initialized;

    public static IReadOnlyList<CharacterStatDefinition> All
    {
        get
        {
            EnsureInitialized();
            return ById.Values
                .OrderBy(definition => definition.SortOrder)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static void Register(CharacterStatDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        EnsureInitialized();
        if (ById.TryGetValue(definition.Id, out CharacterStatDefinition existingById)
            && existingById.LegacyType != definition.LegacyType)
        {
            throw new InvalidOperationException(
                $"Character stat id '{definition.Id}' is already registered with another legacy mapping.");
        }

        if (definition.LegacyType.HasValue
            && ByLegacyType.TryGetValue(definition.LegacyType.Value, out CharacterStatDefinition existingByType)
            && !string.Equals(existingByType.Id, definition.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Character stat type '{definition.LegacyType.Value}' is already assigned to '{existingByType.Id}'.");
        }

        ById[definition.Id] = definition;
        if (definition.LegacyType.HasValue)
        {
            ByLegacyType[definition.LegacyType.Value] = definition;
        }
    }

    public static bool TryGet(string id, out CharacterStatDefinition definition)
    {
        EnsureInitialized();
        return ById.TryGetValue(id?.Trim() ?? string.Empty, out definition);
    }

    public static bool TryGet(CharacterStatType type, out CharacterStatDefinition definition)
    {
        EnsureInitialized();
        return ByLegacyType.TryGetValue(type, out definition);
    }

    public static CharacterStatDefinition GetRequired(CharacterStatType type)
    {
        if (TryGet(type, out CharacterStatDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"No character stat definition is registered for '{type}'.");
    }

    public static void ResetToBuiltIns()
    {
        ById.Clear();
        ByLegacyType.Clear();
        initialized = true;
        RegisterBuiltIns();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ById.Clear();
        ByLegacyType.Clear();
        initialized = false;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        RegisterBuiltIns();
    }

    private static void RegisterBuiltIns()
    {
        RegisterBuiltIn(CharacterStatIds.Attack, "근접", 10, CharacterStatType.Attack);
        RegisterBuiltIn(CharacterStatIds.Shooting, "사격", 20, CharacterStatType.Shooting);
        RegisterBuiltIn(CharacterStatIds.Evasion, "회피", 30, CharacterStatType.Evasion);
        RegisterBuiltIn(CharacterStatIds.Sales, "판매", 40, CharacterStatType.Sales);
        RegisterBuiltIn(CharacterStatIds.Research, "연구", 50, CharacterStatType.Research);
        RegisterBuiltIn(CharacterStatIds.MoveSpeed, "이동", 60, CharacterStatType.MoveSpeed);
        RegisterBuiltIn(CharacterStatIds.Strength, "힘", 70, CharacterStatType.Strength);
        RegisterBuiltIn(CharacterStatIds.Toughness, "강인함", 80, CharacterStatType.Toughness);
        RegisterBuiltIn(CharacterStatIds.Dexterity, "민첩", 90, CharacterStatType.Dexterity);
        RegisterBuiltIn(CharacterStatIds.Cleaning, "청소", 100, CharacterStatType.Cleaning);
        RegisterBuiltIn(CharacterStatIds.Endurance, "지구력", 110, CharacterStatType.Endurance);
    }

    private static void RegisterBuiltIn(
        string id,
        string displayName,
        int sortOrder,
        CharacterStatType legacyType)
    {
        CharacterStatDefinition definition = new CharacterStatDefinition(
            id,
            displayName,
            sortOrder,
            legacyType);
        ById.Add(definition.Id, definition);
        ByLegacyType.Add(legacyType, definition);
    }
}
