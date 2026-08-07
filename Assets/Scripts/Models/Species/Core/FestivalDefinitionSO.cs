using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Festival",
    menuName = "DungeonStory/Population/Festival")]
public sealed class FestivalDefinitionSO : ScriptableObject
{
    public string festivalId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int authoringRevision = 1;
    [TextArea] public string sourceNote = string.Empty;
    public Season season;
    [Range(1, GameCalendarRules.DaysPerSeason)]
    public int dayOfSeason = 1;
    public bool convertsActiveGrief;
    public string cultureId = string.Empty;
    public string requiredBuildingDefinitionId = string.Empty;
    public List<FestivalItemRequirement> requiredItems = new();
    [Min(1)] public int minimumParticipants = 1;
    public FestivalOutcomeDefinition successOutcome = new();
    public FestivalOutcomeDefinition partialOutcome = new();
    public FestivalOutcomeDefinition failureOutcome = new();

    public string StableId => festivalId?.Trim() ?? string.Empty;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (StableId.Length == 0) errors.Add("Festival id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) errors.Add($"'{StableId}' display name is required.");
        if (authoringRevision < 1) errors.Add($"'{StableId}' authoring revision must be positive.");
        if (requiredItems == null || requiredItems.Count == 0
            || requiredItems.Exists(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires concrete physical inputs.");
        if (string.IsNullOrWhiteSpace(requiredBuildingDefinitionId))
            errors.Add($"'{StableId}' requires a physical festival facility.");
        if (successOutcome == null || !successOutcome.IsValid
            || partialOutcome == null || !partialOutcome.IsValid
            || failureOutcome == null || !failureOutcome.IsValid)
            errors.Add($"'{StableId}' requires success, partial, and failure outcomes.");
        return errors;
    }
}

[Serializable]
public sealed class FestivalItemRequirement
{
    public string itemDefinitionId = string.Empty;
    [Min(1)] public int amount = 1;
    public bool IsValid => !string.IsNullOrWhiteSpace(itemDefinitionId) && amount > 0;
}

[Serializable]
public sealed class FestivalOutcomeDefinition
{
    [Range(-20f, 20f)] public float moodDelta;
    [Min(1)] public int moodDurationDays = 1;
    [Range(-100, 100)] public int factionRapportDelta;
    [Range(-20f, 20f)] public float griefConversionPercent;
    public bool IsValid => Mathf.Abs(moodDelta) > 0.0001f
        || factionRapportDelta != 0
        || Mathf.Abs(griefConversionPercent) > 0.0001f;
}

public interface IFestivalDefinitionCatalog
{
    IReadOnlyList<FestivalDefinitionSO> All { get; }
    FestivalDefinitionSO Require(string festivalId);
}
