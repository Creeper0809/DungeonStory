using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

public enum DefenseEffectResultKind { Succeeded = 1, Partial = 2, Failed = 3 }

[Serializable]
public sealed class DefenseActivationFactData
{
    public string facilityId = string.Empty, facilityName = string.Empty;
    public string targetId = string.Empty, targetName = string.Empty;
    public int activationCount, absoluteDay, supplyBefore, supplyAfter;
    public float conditionBefore, conditionAfter;
    public DefenseTriggerTiming timing;
    public DefenseAttackConcept concept;
    public DefenseFacilityOperationalState afterState;
    public bool jammed, misfired;
    public string blockedReason = string.Empty;
    public DefenseActivationFactData Clone() => (DefenseActivationFactData)MemberwiseClone();
    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(facilityId) || !DomainFactValueSyntax.IsIdentity(targetId)
            || !DomainFactValueSyntax.IsText(facilityName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || !DomainFactValueSyntax.IsText(targetName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || activationCount <= 0 || absoluteDay < 0 || supplyBefore < 0 || supplyAfter < 0
            || !float.IsFinite(conditionBefore) || conditionBefore < 0 || conditionBefore > 100
            || !float.IsFinite(conditionAfter) || conditionAfter < 0 || conditionAfter > 100
            || timing == DefenseTriggerTiming.None || !Enum.IsDefined(typeof(DefenseTriggerTiming), timing)
            || concept == DefenseAttackConcept.None || !Enum.IsDefined(typeof(DefenseAttackConcept), concept)
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), afterState) || jammed && misfired
            || !DomainFactValueSyntax.IsText(blockedReason, 1024, true))
            throw new ArgumentException("defense-activation-fact-invalid");
    }
    public bool SameHeader(DefenseActivationFactData other) => other != null
        && facilityId == other.facilityId && facilityName == other.facilityName
        && targetId == other.targetId && targetName == other.targetName && activationCount == other.activationCount
        && absoluteDay == other.absoluteDay && timing == other.timing && concept == other.concept
        && supplyBefore == other.supplyBefore && conditionBefore == other.conditionBefore;
    public bool SameAs(DefenseActivationFactData other) => SameHeader(other) && supplyAfter == other.supplyAfter
        && conditionAfter == other.conditionAfter && afterState == other.afterState && jammed == other.jammed
        && misfired == other.misfired && blockedReason == other.blockedReason;
}

public sealed class DefenseActivationFact
{
    private readonly DefenseActivationFactData data;
    public bool IsProposal { get; }
    public DefenseActivationFact(DefenseActivationFactData data, bool isProposal = false)
    { (data ?? throw new ArgumentNullException(nameof(data))).Validate(); this.data = data.Clone(); IsProposal = isProposal; }
    public DefenseActivationFactData Snapshot() => data.Clone();
    public bool MatchesProposal(DefenseActivationFact proposal) => proposal?.IsProposal == true && data.SameHeader(proposal.data);
}

[Serializable]
public sealed class DefenseEffectStatusFactData
{
    public DefenseStatusKind kind;
    public float value, remainingSeconds;
    public int stacks;
    public DefenseEffectStatusFactData Clone() => (DefenseEffectStatusFactData)MemberwiseClone();
}

[Serializable]
public sealed class DefenseEffectFactData
{
    public DefenseActivationFactData activation = new();
    public DefenseEffectResultKind result;
    public float damage, healthBefore, healthAfter, movementDelaySeconds, ignitionIntensity;
    public DefenseEffectStatusFactData[] statusesBefore = Array.Empty<DefenseEffectStatusFactData>();
    public DefenseEffectStatusFactData[] statusesAfter = Array.Empty<DefenseEffectStatusFactData>();
    public string[] effectTags = Array.Empty<string>();
    public string failure = string.Empty;
    public DefenseEffectFactData Clone()
    {
        var copy = (DefenseEffectFactData)MemberwiseClone(); copy.activation = activation?.Clone();
        copy.statusesBefore = statusesBefore?.Select(row => row?.Clone()).ToArray();
        copy.statusesAfter = statusesAfter?.Select(row => row?.Clone()).ToArray();
        copy.effectTags = effectTags?.ToArray(); return copy;
    }
    public void Validate()
    {
        (activation ?? throw new ArgumentException("defense-effect-activation-missing")).Validate();
        if (!Enum.IsDefined(typeof(DefenseEffectResultKind), result)
            || !Finite(damage) || !Finite(healthBefore) || !Finite(healthAfter)
            || !Finite(movementDelaySeconds) || !Finite(ignitionIntensity) || ignitionIntensity > 1
            || Math.Abs(damage - Math.Max(0, healthBefore - healthAfter)) > 0.001f
            || statusesBefore == null || statusesAfter == null || effectTags == null
            || effectTags.Any(tag => !DomainFactValueSyntax.IsText(tag, 1024, false))
            || effectTags.Distinct(StringComparer.Ordinal).Count() != effectTags.Length
            || !DomainFactValueSyntax.IsText(failure, 4096, false))
            throw new ArgumentException("defense-effect-fact-invalid");
        foreach (var rows in new[] { statusesBefore, statusesAfter })
            if (rows.Any(row => row == null || !Enum.IsDefined(typeof(DefenseStatusKind), row.kind)
                    || !float.IsFinite(row.value) || !Finite(row.remainingSeconds) || row.stacks < 0)
                || rows.Select(row => row.kind).Distinct().Count() != rows.Length)
                throw new ArgumentException("defense-effect-status-invalid");
    }
    private static bool Finite(float value) => float.IsFinite(value) && value >= 0;
}

public sealed class DefenseEffectFact
{
    private readonly DefenseEffectFactData data;
    public DefenseEffectFact(DefenseEffectFactData data)
    { (data ?? throw new ArgumentNullException(nameof(data))).Validate(); this.data = data.Clone(); }
    public DefenseEffectFactData Snapshot() => data.Clone();
}
