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

/// <summary>
/// Immutable mapping for the fixed FacilityRole bit protocol.
/// </summary>
public static class FacilityRoleCatalog
{
    private static readonly FacilityRoleDefinition[] Definitions =
    {
        Definition("role:meal", FacilityRole.Meal, "식사", "식당", 10, new Color(0.84f, 0.65f, 0.29f, 1f), "Meal"),
        Definition("role:purchase", FacilityRole.Purchase, "상점", "상점", 20, new Color(0.91f, 0.78f, 0.36f, 1f), "Purchase"),
        Definition("role:rest", FacilityRole.Rest, "휴식", "휴게실", 30, new Color(0.31f, 0.65f, 0.78f, 1f), "Rest"),
        Definition("role:training", FacilityRole.Training, "훈련", "훈련실", 40, new Color(0.79f, 0.36f, 0.36f, 1f), "Training"),
        Definition("role:research", FacilityRole.Research, "연구", "연구실", 50, new Color(0.31f, 0.69f, 0.46f, 1f), "Research"),
        Definition("role:mana", FacilityRole.Mana, "마나", "마나실", 60, new Color(0.60f, 0.42f, 0.78f, 1f), "Mana"),
        Definition("role:logistics", FacilityRole.Logistics, "창고", "창고", 70, new Color(0.53f, 0.56f, 0.61f, 1f), "Logistics"),
        Definition("role:toilet", FacilityRole.Toilet, "화장실", "화장실", 80, new Color(0.31f, 0.51f, 0.72f, 1f), "Toilet"),
        Definition("role:hygiene", FacilityRole.Hygiene, "위생", "세면실", 90, new Color(0.33f, 0.72f, 0.63f, 1f), "Hygiene"),
        Definition("role:administration", FacilityRole.Administration, "집무", "사장실", 100, new Color(0.74f, 0.57f, 0.32f, 1f), "Administration"),
        Definition("role:security", FacilityRole.Security, "경비", "경비실", 110, new Color(0.67f, 0.33f, 0.31f, 1f), "Security"),
        Definition("role:entertainment", FacilityRole.Entertainment, "흥행", "서커스장", 120, new Color(0.72f, 0.29f, 0.42f, 1f), "Entertainment"),
        Definition("role:medical", FacilityRole.Medical, "의료", "의무실", 130, new Color(0.40f, 0.69f, 0.64f, 1f), "Medical")
    };

    public static IReadOnlyList<FacilityRoleDefinition> All => Definitions;

    public static bool TryGet(FacilityRole role, out FacilityRoleDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => candidate.Role == role);
        return definition != null;
    }

    public static bool TryGet(string id, out FacilityRoleDefinition definition)
    {
        string normalized = id?.Trim() ?? string.Empty;
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, normalized, StringComparison.Ordinal));
        return definition != null;
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
        return Definitions.Where(definition => (roles & definition.Role) != 0);
    }

    private static FacilityRoleDefinition Definition(
        string id,
        FacilityRole role,
        string roomLabel,
        string roomName,
        int sortOrder,
        Color color,
        string semanticTag)
    {
        return new FacilityRoleDefinition(
            id,
            role,
            roomLabel,
            roomName,
            sortOrder,
            color,
            semanticTag);
    }
}
