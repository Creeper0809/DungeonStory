using System;

/// <summary>
/// Owns locale-sensitive detailed-stat labels and wrappers. Calculation,
/// thresholds, stat IDs, and snapshot ownership remain in the runtime.
/// </summary>
public static class CharacterDetailedStatsTextFormatter
{
    public static string Get(string key, params object[] arguments) =>
        CharacterSummaryUiTextQuery.Get(key, arguments);

    public static string RecoveryLabel(PartRecoveryPolicy value) => value switch
    {
        PartRecoveryPolicy.Natural => Get("CharacterSummary.Detailed.Recovery.Natural"),
        PartRecoveryPolicy.AssistedRegeneration => Get("CharacterSummary.Detailed.Recovery.AssistedRegeneration"),
        PartRecoveryPolicy.MaintenanceOnly => Get("CharacterSummary.Detailed.Recovery.MaintenanceOnly"),
        _ => Get("CharacterSummary.Detailed.Recovery.ReplaceOnFailure")
    };

    public static string TabLabel(CharacterDetailedStatsTab value) => value switch
    {
        CharacterDetailedStatsTab.Summary => Get("CharacterSummary.Detailed.Tab.Summary"),
        CharacterDetailedStatsTab.BaseStats => Get("CharacterSummary.Detailed.Tab.BaseStats"),
        CharacterDetailedStatsTab.Work => Get("CharacterSummary.Detailed.Tab.Work"),
        CharacterDetailedStatsTab.CombatEquipment => Get("CharacterSummary.Detailed.Tab.CombatEquipment"),
        CharacterDetailedStatsTab.HealthAnatomy => Get("CharacterSummary.Detailed.Tab.HealthAnatomy"),
        _ => Get("CharacterSummary.Detailed.Tab.Modifiers")
    };

    public static string ActivityLabel(AnatomyActivityId value) => value switch
    {
        AnatomyActivityId.Movement => Get("CharacterSummary.Detailed.Activity.Movement"),
        AnatomyActivityId.Accuracy => Get("CharacterSummary.Detailed.Activity.Accuracy"),
        AnatomyActivityId.Evasion => Get("CharacterSummary.Detailed.Activity.Evasion"),
        AnatomyActivityId.Work => Get("CharacterSummary.Detailed.Activity.Work"),
        AnatomyActivityId.Carry => Get("CharacterSummary.Detailed.Activity.Carry"),
        AnatomyActivityId.MeleePower => Get("CharacterSummary.Detailed.Activity.MeleePower"),
        AnatomyActivityId.Treatment => Get("CharacterSummary.Detailed.Activity.Treatment"),
        AnatomyActivityId.Recovery => Get("CharacterSummary.Detailed.Activity.Recovery"),
        _ => Get("CharacterSummary.Detailed.Activity.Overclock")
    };

    public static string AxisLabel(AnatomyActionAxisId value) => value switch
    {
        AnatomyActionAxisId.Awareness => Get("CharacterSummary.Detailed.Axis.Awareness"),
        AnatomyActionAxisId.Handling => Get("CharacterSummary.Detailed.Axis.Handling"),
        AnatomyActionAxisId.Locomotion => Get("CharacterSummary.Detailed.Axis.Locomotion"),
        AnatomyActionAxisId.Sustain => Get("CharacterSummary.Detailed.Axis.Sustain"),
        _ => Get("CharacterSummary.Detailed.Axis.Recovery")
    };
}
