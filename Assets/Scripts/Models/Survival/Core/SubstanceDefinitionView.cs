using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum SubstanceUseClass
{
    Medicine = 0,
    NonAddictive = 1,
    Addictive = 2,
    Recreational = 3
}

/// <summary>
/// Immutable runtime/query projection of an item's authored substance feature.
/// The mutable content authority remains the owning ItemDefinitionSO asset.
/// </summary>
public sealed class SubstanceDefinitionView
{
    public SubstanceDefinitionView(
        string substanceId,
        string itemId,
        string displayName,
        SubstanceUseClass useClass,
        float addictionChance,
        float overdoseChance,
        float toleranceGain,
        float withdrawalPerHour,
        float moodEffect,
        float workSpeedEffect,
        float combatEffect,
        float durationSeconds,
        string requiredResearchId)
    {
        SubstanceId = substanceId?.Trim() ?? string.Empty;
        ItemId = itemId?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? SubstanceId
            : displayName.Trim();
        UseClass = useClass;
        AddictionChance = Clamp01(addictionChance);
        OverdoseChance = Clamp01(overdoseChance);
        ToleranceGain = Math.Max(0f, toleranceGain);
        WithdrawalPerHour = Math.Max(0f, withdrawalPerHour);
        MoodEffect = moodEffect;
        WorkSpeedEffect = workSpeedEffect;
        CombatEffect = combatEffect;
        DurationSeconds = Math.Max(1f, durationSeconds);
        RequiredResearchId = requiredResearchId?.Trim() ?? string.Empty;
    }

    public string SubstanceId { get; }
    public string ItemId { get; }
    public string DisplayName { get; }
    public SubstanceUseClass UseClass { get; }
    public float AddictionChance { get; }
    public float OverdoseChance { get; }
    public float ToleranceGain { get; }
    public float WithdrawalPerHour { get; }
    public float MoodEffect { get; }
    public float WorkSpeedEffect { get; }
    public float CombatEffect { get; }
    public float DurationSeconds { get; }
    public string RequiredResearchId { get; }

    private static float Clamp01(float value) =>
        Math.Max(0f, Math.Min(1f, value));
}
