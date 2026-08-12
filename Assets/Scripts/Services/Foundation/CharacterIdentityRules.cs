using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterCommandOrigin
{
    Autonomous,
    DirectPlayerOrder,
    ScriptedForced
}

[Serializable]
public abstract class CharacterIdentityRule
{
    public string ruleId = string.Empty;
    public int priority;

    public virtual IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            yield return $"{GetType().Name} requires a stable rule id.";
    }
}

[Serializable]
public sealed class BehaviorUtilityRule : CharacterIdentityRule
{
    public string behaviorTag = string.Empty;
    [Range(-1f, 1f)] public float utilityDelta;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(behaviorTag))
            yield return $"Behavior rule '{ruleId}' requires a behavior tag.";
        if (Mathf.Approximately(utilityDelta, 0f))
            yield return $"Behavior rule '{ruleId}' requires a non-zero utility delta.";
    }
}

[Serializable]
public sealed class PersistentNeedRule : CharacterIdentityRule
{
    public string needId = string.Empty;
    public string satisfiedEventId = string.Empty;
    public string deprivedEventId = string.Empty;
    [Min(1)] public int deprivationDays = 1;
    [Range(-20f, 0f)] public float deprivedMoodDelta = -2f;
    [Range(0f, 20f)] public float satisfiedMoodDelta = 2f;
    [Min(1)] public int moodDurationDays = 1;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(needId))
            yield return $"Persistent need rule '{ruleId}' requires a need id.";
        if (string.IsNullOrWhiteSpace(satisfiedEventId)
            || string.IsNullOrWhiteSpace(deprivedEventId))
            yield return $"Persistent need rule '{ruleId}' requires satisfied and deprived event ids.";
        if (deprivationDays <= 0 || moodDurationDays <= 0)
            yield return $"Persistent need rule '{ruleId}' requires positive durations.";
    }
}

[Serializable]
public sealed class EventMoodRule : CharacterIdentityRule
{
    public string eventId = string.Empty;
    [Range(-20f, 20f)] public float moodDelta;
    [Min(1)] public int durationDays = 1;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(eventId))
            yield return $"Event mood rule '{ruleId}' requires an event id.";
        if (Mathf.Approximately(moodDelta, 0f))
            yield return $"Event mood rule '{ruleId}' requires a non-zero mood delta.";
        if (durationDays <= 0)
            yield return $"Event mood rule '{ruleId}' requires a positive duration.";
    }
}

[Serializable]
public sealed class MoodImmunityRule : CharacterIdentityRule
{
    public string eventId = string.Empty;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(eventId))
            yield return $"Mood immunity rule '{ruleId}' requires an event id.";
    }
}

[Serializable]
public sealed class MoodTransformRule : CharacterIdentityRule
{
    public string eventId = string.Empty;
    [Range(0f, 2f)] public float multiplier = 1f;
    [Range(-20f, 20f)] public float additiveDelta;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(eventId))
            yield return $"Mood transform rule '{ruleId}' requires an event id.";
        if (Mathf.Approximately(multiplier, 1f)
            && Mathf.Approximately(additiveDelta, 0f))
            yield return $"Mood transform rule '{ruleId}' has no effect.";
    }
}

[Serializable]
public sealed class PostActionConsequenceRule : CharacterIdentityRule
{
    public string actionTag = string.Empty;
    public bool directOrdersOnly;
    [Range(-20f, 20f)] public float moodDelta;
    [Range(-20f, 20f)] public float stressDelta;
    [Min(1)] public int durationDays = 1;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(actionTag))
            yield return $"Post-action rule '{ruleId}' requires an action tag.";
        if (Mathf.Approximately(moodDelta, 0f)
            && Mathf.Approximately(stressDelta, 0f))
            yield return $"Post-action rule '{ruleId}' has no consequence.";
        if (durationDays <= 0)
            yield return $"Post-action rule '{ruleId}' requires a positive duration.";
    }
}

[Serializable]
public sealed class RelationshipMemoryRule : CharacterIdentityRule
{
    public string eventId = string.Empty;
    [Range(-20f, 20f)] public float relationshipDelta = -4f;
    [Min(0f)] public float dailyDecay = 1f;
    public bool apologyCanClear = true;
    public bool restitutionRequired;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(eventId))
            yield return $"Relationship memory rule '{ruleId}' requires an event id.";
        if (Mathf.Approximately(relationshipDelta, 0f))
            yield return $"Relationship memory rule '{ruleId}' requires a non-zero relationship delta.";
        if (dailyDecay < 0f)
            yield return $"Relationship memory rule '{ruleId}' has a negative decay.";
        if (restitutionRequired && !apologyCanClear)
            yield return $"Relationship memory rule '{ruleId}' cannot require restitution when forgiveness is disabled.";
    }
}

[Serializable]
public sealed class AutonomousWorkRestrictionRule : CharacterIdentityRule
{
    public string actionTag = string.Empty;
    public string requiredConditionId = string.Empty;
    public string failureReason = string.Empty;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(actionTag)
            || string.IsNullOrWhiteSpace(requiredConditionId)
            || string.IsNullOrWhiteSpace(failureReason))
            yield return $"Autonomous work restriction '{ruleId}' requires action, condition, and failure reason.";
    }
}

[Serializable]
public sealed class IncidentWeightRule : CharacterIdentityRule
{
    public string incidentId = string.Empty;
    [Range(0.05f, 10f)] public float multiplier = 1f;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (string.IsNullOrWhiteSpace(incidentId))
            yield return $"Incident weight rule '{ruleId}' requires an incident id.";
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            yield return $"Incident weight rule '{ruleId}' requires a positive non-neutral multiplier.";
    }
}

[Serializable]
public sealed class ExtremeCraftInspirationRule : CharacterIdentityRule
{
    [Range(0f, 1f)] public float mythicChance = 0.03f;
    [Range(0f, 1f)] public float minimumContributionShare = 0.60f;
    [Min(1)] public int repetitionFreeCount = 2;
    [Range(-20f, 0f)] public float repetitionMoodStep = -2f;
    [Range(-20f, 0f)] public float repetitionMoodMinimum = -10f;
    [Min(1)] public int resetAfterHours = 48;
    [Range(0f, 20f)] public float mythicMoodDelta = 10f;
    [Min(1)] public int mythicMoodDurationDays = 2;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (mythicChance <= 0f || mythicChance > 1f)
            yield return $"Extreme craft rule '{ruleId}' has an invalid mythic chance.";
        if (minimumContributionShare <= 0f || minimumContributionShare > 1f)
            yield return $"Extreme craft rule '{ruleId}' has an invalid contribution share.";
        if (repetitionFreeCount <= 0 || resetAfterHours <= 0 || mythicMoodDurationDays <= 0)
            yield return $"Extreme craft rule '{ruleId}' requires positive counters and durations.";
        if (repetitionMoodStep >= 0f || repetitionMoodMinimum >= 0f)
            yield return $"Extreme craft rule '{ruleId}' requires negative repetition penalties.";
    }
}

[Serializable]
public sealed class LastStandRule : CharacterIdentityRule
{
    [Range(0.01f, 1f)] public float healthThreshold = 0.20f;
    [Min(1)] public int aftermathDays = 2;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (healthThreshold <= 0f || healthThreshold >= 1f)
            yield return $"Last stand rule '{ruleId}' has an invalid health threshold.";
        if (aftermathDays <= 0)
            yield return $"Last stand rule '{ruleId}' requires a positive aftermath duration.";
    }
}

[Serializable]
public sealed class ForbiddenResearchLeapRule : CharacterIdentityRule
{
    [Range(0f, 1f)] public float breakthroughChance = 0.10f;
    [Range(0f, 1f)] public float setbackChance = 0.20f;
    [Range(0f, 1f)] public float breakthroughProgress = 0.25f;
    [Range(0f, 1f)] public float setbackProgress = 0.10f;
    [Min(1)] public int aftermathDays = 1;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (breakthroughChance < 0f || setbackChance < 0f
            || breakthroughChance + setbackChance > 1f)
            yield return $"Forbidden research rule '{ruleId}' has invalid outcome probabilities.";
        if (breakthroughProgress <= 0f || setbackProgress <= 0f
            || aftermathDays <= 0)
            yield return $"Forbidden research rule '{ruleId}' has invalid progress or aftermath values.";
    }
}

[Serializable]
public sealed class MiracleSurgeryRule : CharacterIdentityRule
{
    [Range(0f, 1f)] public float miracleChance = 0.12f;
    [Range(0f, 1f)] public float complicationChance = 0.18f;
    [Min(1)] public int aftermathDays = 1;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (miracleChance < 0f || complicationChance < 0f
            || miracleChance + complicationChance > 1f)
            yield return $"Miracle surgery rule '{ruleId}' has invalid outcome probabilities.";
        if (aftermathDays <= 0)
            yield return $"Miracle surgery rule '{ruleId}' has invalid aftermath values.";
    }
}

[Serializable]
public sealed class GoldenHarvestRule : CharacterIdentityRule
{
    [Min(1)] public int delayHours = 24;
    [Range(0f, 1f)] public float jackpotChance = 0.12f;
    [Range(0f, 1f)] public float lossChance = 0.18f;
    [Range(0f, 1f)] public float failureYieldMultiplier = 0.50f;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (jackpotChance < 0f || lossChance < 0f
            || jackpotChance + lossChance > 1f)
            yield return $"Golden harvest rule '{ruleId}' has invalid outcome probabilities.";
        if (delayHours <= 0
            || failureYieldMultiplier <= 0f || failureYieldMultiplier >= 1f)
            yield return $"Golden harvest rule '{ruleId}' has invalid delay or yield values.";
    }
}

[Serializable]
public sealed class ProductionLimitBreakRule : CharacterIdentityRule
{
    [Min(1)] public int aftermathDays = 1;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (aftermathDays <= 0)
            yield return $"Production limit break rule '{ruleId}' requires a positive aftermath duration.";
    }
}

[Serializable]
public sealed class ArcaneOverchargeRule : CharacterIdentityRule
{
    [Range(0f, 1f)] public float manaThreshold = 0.30f;
    [Min(1)] public int durationSeconds = 20;
    [Range(0f, 1f)] public float selfDamageFraction = 0.15f;
    [Range(0f, 1f)] public float equipmentDurabilityFraction = 0.25f;
    [Min(1)] public int aftermathDays = 1;

    public override IEnumerable<string> Validate()
    {
        foreach (string error in base.Validate()) yield return error;
        if (manaThreshold <= 0f || manaThreshold >= 1f
            || durationSeconds <= 0
            || selfDamageFraction <= 0f || selfDamageFraction >= 1f
            || equipmentDurabilityFraction <= 0f || equipmentDurabilityFraction >= 1f
            || aftermathDays <= 0)
            yield return $"Arcane overcharge rule '{ruleId}' has invalid activation or aftermath values.";
    }
}
