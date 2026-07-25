using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class FacilityRoleDefinition
{
    public FacilityRoleDefinition(
        string id,
        FacilityRole role,
        string roomLabel,
        string roomName,
        int sortOrder,
        Color color,
        string semanticTag = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Facility role id is required.", nameof(id));
        }

        if (role == FacilityRole.None || !IsSingleBit(role))
        {
            throw new ArgumentException("A facility role definition must use one non-zero flag bit.", nameof(role));
        }

        Id = id.Trim();
        Role = role;
        RoomLabel = string.IsNullOrWhiteSpace(roomLabel) ? Id : roomLabel.Trim();
        RoomName = string.IsNullOrWhiteSpace(roomName) ? RoomLabel : roomName.Trim();
        SortOrder = sortOrder;
        Color = color;
        SemanticTag = string.IsNullOrWhiteSpace(semanticTag) ? Id : semanticTag.Trim();
    }

    public string Id { get; }
    public FacilityRole Role { get; }
    public string RoomLabel { get; }
    public string RoomName { get; }
    public int SortOrder { get; }
    public Color Color { get; }
    public string SemanticTag { get; }

    private static bool IsSingleBit(FacilityRole role)
    {
        int value = (int)role;
        return (value & (value - 1)) == 0;
    }
}

public static class FacilityRoleCatalog
{
    private static readonly Dictionary<string, FacilityRoleDefinition> ById =
        new Dictionary<string, FacilityRoleDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<FacilityRole, FacilityRoleDefinition> ByRole =
        new Dictionary<FacilityRole, FacilityRoleDefinition>();
    private static bool initialized;

    public static IReadOnlyList<FacilityRoleDefinition> All
    {
        get
        {
            EnsureInitialized();
            return ById.Values
                .OrderBy((definition) => definition.SortOrder)
                .ThenBy((definition) => definition.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static void Register(FacilityRoleDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        EnsureInitialized();
        if (ById.TryGetValue(definition.Id, out FacilityRoleDefinition existingById)
            && existingById.Role != definition.Role)
        {
            throw new InvalidOperationException(
                $"Facility role id '{definition.Id}' is already assigned to {existingById.Role}.");
        }

        if (ByRole.TryGetValue(definition.Role, out FacilityRoleDefinition existingByRole)
            && !string.Equals(existingByRole.Id, definition.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Facility role flag '{definition.Role}' is already assigned to '{existingByRole.Id}'.");
        }

        ById[definition.Id] = definition;
        ByRole[definition.Role] = definition;
    }

    public static bool TryGet(FacilityRole role, out FacilityRoleDefinition definition)
    {
        EnsureInitialized();
        return ByRole.TryGetValue(role, out definition);
    }

    public static bool TryGet(string id, out FacilityRoleDefinition definition)
    {
        EnsureInitialized();
        return ById.TryGetValue(id?.Trim() ?? string.Empty, out definition);
    }

    public static string GetRoomLabel(FacilityRole role)
    {
        return TryGet(role, out FacilityRoleDefinition definition)
            ? definition.RoomLabel
            : role.ToString();
    }

    public static string GetRoomName(FacilityRole role)
    {
        return TryGet(role, out FacilityRoleDefinition definition)
            ? definition.RoomName
            : role.ToString();
    }

    public static Color GetColor(FacilityRole role, Color fallback)
    {
        return TryGet(role, out FacilityRoleDefinition definition)
            ? definition.Color
            : fallback;
    }

    public static IEnumerable<FacilityRoleDefinition> Enumerate(FacilityRole roles)
    {
        return All.Where((definition) => (roles & definition.Role) != 0);
    }

    public static void ResetToBuiltIns()
    {
        ById.Clear();
        ByRole.Clear();
        initialized = true;
        RegisterBuiltIns();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ById.Clear();
        ByRole.Clear();
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
        RegisterBuiltIn("role:meal", FacilityRole.Meal, "식사", "식당", 10, new Color(0.84f, 0.65f, 0.29f, 1f), "Meal");
        RegisterBuiltIn("role:purchase", FacilityRole.Purchase, "상점", "상점", 20, new Color(0.91f, 0.78f, 0.36f, 1f), "Purchase");
        RegisterBuiltIn("role:rest", FacilityRole.Rest, "휴식", "휴게실", 30, new Color(0.31f, 0.65f, 0.78f, 1f), "Rest");
        RegisterBuiltIn("role:training", FacilityRole.Training, "훈련", "훈련실", 40, new Color(0.79f, 0.36f, 0.36f, 1f), "Training");
        RegisterBuiltIn("role:research", FacilityRole.Research, "연구", "연구실", 50, new Color(0.31f, 0.69f, 0.46f, 1f), "Research");
        RegisterBuiltIn("role:mana", FacilityRole.Mana, "마나", "마나실", 60, new Color(0.60f, 0.42f, 0.78f, 1f), "Mana");
        RegisterBuiltIn("role:logistics", FacilityRole.Logistics, "창고", "창고", 70, new Color(0.53f, 0.56f, 0.61f, 1f), "Logistics");
        RegisterBuiltIn("role:toilet", FacilityRole.Toilet, "화장실", "화장실", 80, new Color(0.31f, 0.51f, 0.72f, 1f), "Toilet");
        RegisterBuiltIn("role:hygiene", FacilityRole.Hygiene, "위생", "세면실", 90, new Color(0.33f, 0.72f, 0.63f, 1f), "Hygiene");
        RegisterBuiltIn("role:administration", FacilityRole.Administration, "집무", "사장실", 100, new Color(0.74f, 0.57f, 0.32f, 1f), "Administration");
        RegisterBuiltIn("role:security", FacilityRole.Security, "경비", "경비실", 110, new Color(0.67f, 0.33f, 0.31f, 1f), "Security");
    }

    private static void RegisterBuiltIn(
        string id,
        FacilityRole role,
        string roomLabel,
        string roomName,
        int sortOrder,
        Color color,
        string semanticTag)
    {
        FacilityRoleDefinition definition = new FacilityRoleDefinition(
            id,
            role,
            roomLabel,
            roomName,
            sortOrder,
            color,
            semanticTag);
        ById.Add(definition.Id, definition);
        ByRole.Add(definition.Role, definition);
    }
}
