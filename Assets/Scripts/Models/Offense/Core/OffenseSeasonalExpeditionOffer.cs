using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class OffenseSeasonalPhysicalRewardData
{
    public string itemId = string.Empty;
    public string displayLabel = string.Empty;
    public int exactQuantity;
}

[Serializable]
public sealed class OffenseSeasonalExpeditionOfferData
{
    public bool configured;
    public string occurrenceInstanceId = string.Empty;
    public string definitionId = string.Empty;
    public int offerDeadlineAbsoluteDay;
    public string description = string.Empty;
    public float recommendedDanger;
    public float durationSeconds;
    public int requiredMembers;
    public float recommendedPower;
    public int campaignOrder;
    public string authoredEncounterId = string.Empty;
    public string encounterPreviewText = string.Empty;
    public string encounterRewardPreviewText = string.Empty;
    public List<OffenseSeasonalPhysicalRewardData> physicalRewards = new();

    public bool IsConfigured => configured
        && !string.IsNullOrWhiteSpace(occurrenceInstanceId)
        && !string.IsNullOrWhiteSpace(definitionId);
    public bool IsValid => IsConfigured
        && Canonical(occurrenceInstanceId)
        && Canonical(definitionId)
        && offerDeadlineAbsoluteDay >= 1
        && Canonical(description)
        && recommendedDanger >= 0f
        && !float.IsNaN(recommendedDanger)
        && !float.IsInfinity(recommendedDanger)
        && durationSeconds > 0f
        && !float.IsNaN(durationSeconds)
        && !float.IsInfinity(durationSeconds)
        && requiredMembers is >= 1 and <= 5
        && recommendedPower >= 0f
        && !float.IsNaN(recommendedPower)
        && !float.IsInfinity(recommendedPower)
        && campaignOrder is >= 1 and <= 6
        && Canonical(authoredEncounterId)
        && Canonical(encounterPreviewText)
        && Canonical(encounterRewardPreviewText)
        && physicalRewards != null
        && physicalRewards.Count > 0
        && physicalRewards.All(value => value != null
            && Canonical(value.itemId)
            && Canonical(value.displayLabel)
            && value.exactQuantity > 0)
        && physicalRewards.Select(value => value.itemId)
            .Distinct(StringComparer.Ordinal).Count() == physicalRewards.Count;

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
