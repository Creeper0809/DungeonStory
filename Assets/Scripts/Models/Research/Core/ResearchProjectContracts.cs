using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResearchBlueprintRule
{
    None = 0,
    Required = 1,
    Shortcut = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResearchField
{
    LifeAndSurvival = 0,
    CommerceAndCraft = 1,
    DefenseAndTactics = 2,
    RecordsAndArcane = 3,
    CaptivityAndEntertainment = 4,
    AuthorityAndHousing = 5,
    Agriculture = 6,
    Forestry = 7,
    Mining = 8,
    Husbandry = 9,
    Metallurgy = 10,
    Textiles = 11,
    Cuisine = 12,
    Pharmacology = 13,
    SurgeryAndTransplant = 14,
    IndustryAndAutomation = 15,
    WaterAndSanitation = 16
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResearchNodeState
{
    Locked = 0,
    Available = 1,
    Queued = 2,
    Active = 3,
    Suspended = 4,
    BlueprintInTransit = 5,
    ShortcutAvailable = 6,
    Completed = 7
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ResearchProjectId : IEquatable<ResearchProjectId>
{
    [SerializeField] private readonly string value;

    public ResearchProjectId(string value)
    {
        this.value = Normalize(value);
    }

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public bool Equals(ResearchProjectId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is ResearchProjectId other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static implicit operator ResearchProjectId(string value) =>
        new ResearchProjectId(value);
    public static implicit operator string(ResearchProjectId id) => id.Value;

    public static string Normalize(string candidate) =>
        candidate?.Trim() ?? string.Empty;
}

public interface IResearchProjectDefinition
{
    ResearchProjectId ProjectId { get; }
    string DisplayName { get; }
    float RequiredWork { get; }
    ResearchBlueprintRule BlueprintRule { get; }
    IReadOnlyList<ResearchProjectId> PrerequisiteIds { get; }
    IReadOnlyList<ResearchFacilityRequirement> FacilityRequirements { get; }
    int BlueprintId { get; }
}

public readonly struct ResearchProjectDefinitionSnapshot : IResearchProjectDefinition
{
    public ResearchProjectDefinitionSnapshot(
        ResearchProjectId projectId,
        string displayName,
        float requiredWork,
        ResearchBlueprintRule blueprintRule,
        IReadOnlyList<ResearchProjectId> prerequisiteIds,
        IReadOnlyList<ResearchFacilityRequirement> facilityRequirements,
        int blueprintId)
    {
        ProjectId = projectId;
        DisplayName = displayName?.Trim() ?? string.Empty;
        RequiredWork = Mathf.Max(1f, requiredWork);
        BlueprintRule = blueprintRule;
        PrerequisiteIds = prerequisiteIds ?? Array.Empty<ResearchProjectId>();
        FacilityRequirements = facilityRequirements
            ?? Array.Empty<ResearchFacilityRequirement>();
        BlueprintId = blueprintId;
    }

    public ResearchProjectId ProjectId { get; }
    public string DisplayName { get; }
    public float RequiredWork { get; }
    public ResearchBlueprintRule BlueprintRule { get; }
    public IReadOnlyList<ResearchProjectId> PrerequisiteIds { get; }
    public IReadOnlyList<ResearchFacilityRequirement> FacilityRequirements { get; }
    public int BlueprintId { get; }
}
