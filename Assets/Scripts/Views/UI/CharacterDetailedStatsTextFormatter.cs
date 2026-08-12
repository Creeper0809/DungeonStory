using System;

/// <summary>
/// Owns locale-sensitive detailed-stat labels and wrappers. Calculation,
/// thresholds, stat IDs, and snapshot ownership remain in the runtime.
/// </summary>
public static class CharacterDetailedStatsTextFormatter
{
    public static string Get(string key, params object[] arguments) =>
        CharacterSummaryUiTextQuery.Get(key, arguments);

    public static string GameplayEffectTargetLabel(string targetId) => targetId switch
    {
        GameplayEffectTargetIds.RecoverySpeed => "\uC2E0\uCCB4 \uD68C\uBCF5 \uC18D\uB3C4",
        GameplayEffectTargetIds.DiseaseResistance => "\uC9C8\uBCD1 \uC800\uD56D\uB825",
        GameplayEffectTargetIds.DiseaseRecoverySpeed => "\uC9C8\uBCD1 \uD68C\uBCF5 \uC18D\uB3C4",
        GameplayEffectTargetIds.ImmunityGain => "\uBA74\uC5ED \uD68D\uB4DD\uB825",
        GameplayEffectTargetIds.ImmunityRetention => "\uBA74\uC5ED \uC720\uC9C0\uB825",
        _ => targetId ?? string.Empty
    };

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
        CharacterDetailedStatsTab.Proficiencies => Get("CharacterSummary.Detailed.Tab.Proficiencies"),
        _ => Get("CharacterSummary.Detailed.Tab.Modifiers")
    };

}
