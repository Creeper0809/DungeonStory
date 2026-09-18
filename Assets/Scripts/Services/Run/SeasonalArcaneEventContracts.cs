using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class SeasonalManaLightningSaveData
{
    public bool configured;
    public List<int> eligibleBuildingDefinitionIds = new();
    public float minimumHeat;
    public float minimumFault;
    public float riskWindowSeconds;
    public float additionalIgnitionChancePerWindow;
    public float ignitionIntensity;

    public static SeasonalManaLightningSaveData FromProfile(
        SeasonalManaLightningProfile profile)
    {
        if (profile?.IsConfigured != true)
            return new SeasonalManaLightningSaveData();
        return new SeasonalManaLightningSaveData
        {
            configured = true,
            eligibleBuildingDefinitionIds = (profile.eligibleBuildingDefinitionIds
                    ?? new List<int>())
                .OrderBy(value => value)
                .ToList(),
            minimumHeat = profile.minimumHeat,
            minimumFault = profile.minimumFault,
            riskWindowSeconds = profile.riskWindowSeconds,
            additionalIgnitionChancePerWindow =
                profile.additionalIgnitionChancePerWindow,
            ignitionIntensity = profile.ignitionIntensity
        };
    }
}

[Serializable]
public sealed class SeasonalExpeditionPhysicalRewardSaveData
{
    public string itemId = string.Empty;
    public string displayLabel = string.Empty;
    public int exactQuantity;
}

[Serializable]
public sealed class SeasonalSpecialExpeditionSaveData
{
    public bool configured;
    public SeasonalSpecialExpeditionPurpose purpose;
    public string siteArchetypeId = string.Empty;
    public string title = string.Empty;
    public string description = string.Empty;
    public int minimumDistanceSteps;
    public int maximumDistanceSteps;
    public float recommendedDanger;
    public float durationSeconds;
    public int requiredMembers;
    public float recommendedPower;
    public int campaignOrder;
    public string authoredEncounterId = string.Empty;
    public string encounterPreviewText = string.Empty;
    public string encounterRewardPreviewText = string.Empty;
    public string factionId = string.Empty;
    public List<SeasonalExpeditionPhysicalRewardSaveData> physicalRewards = new();

    public static SeasonalSpecialExpeditionSaveData FromProfile(
        SeasonalSpecialExpeditionProfile profile)
    {
        if (profile?.IsConfigured != true)
            return new SeasonalSpecialExpeditionSaveData();
        return new SeasonalSpecialExpeditionSaveData
        {
            configured = true,
            purpose = profile.purpose,
            siteArchetypeId = profile.siteArchetypeId,
            title = profile.title,
            description = profile.description,
            minimumDistanceSteps = profile.minimumDistanceSteps,
            maximumDistanceSteps = profile.maximumDistanceSteps,
            recommendedDanger = profile.recommendedDanger,
            durationSeconds = profile.durationSeconds,
            requiredMembers = profile.requiredMembers,
            recommendedPower = profile.recommendedPower,
            campaignOrder = profile.campaignOrder,
            authoredEncounterId = profile.authoredEncounterId,
            encounterPreviewText = profile.encounterPreviewText,
            encounterRewardPreviewText = profile.encounterRewardPreviewText,
            factionId = profile.factionId,
            physicalRewards = (profile.physicalRewards
                    ?? new List<SeasonalExpeditionPhysicalRewardProfile>())
                .OrderBy(value => value.itemId, StringComparer.Ordinal)
                .Select(value => new SeasonalExpeditionPhysicalRewardSaveData
                {
                    itemId = value.itemId,
                    displayLabel = value.displayLabel,
                    exactQuantity = value.exactQuantity
                })
                .ToList()
        };
    }
}

public static class SeasonalArcaneEventRules
{
    public static void RequireValidFrozenState(
        SeasonalManaLightningSaveData frozen,
        SeasonalManaLightningProfile authored,
        string occurrenceInstanceId)
    {
        bool expected = authored?.IsConfigured == true;
        if (frozen == null || frozen.configured != expected)
            throw Invalid(occurrenceInstanceId, "mana-lightning configuration");
        if (!expected)
        {
            if (HasPayload(frozen))
                throw Invalid(occurrenceInstanceId, "empty mana-lightning state");
            return;
        }
        SeasonalManaLightningSaveData expectedState =
            SeasonalManaLightningSaveData.FromProfile(authored);
        if (!(frozen.eligibleBuildingDefinitionIds ?? new List<int>())
                .SequenceEqual(expectedState.eligibleBuildingDefinitionIds)
            || frozen.minimumHeat != expectedState.minimumHeat
            || frozen.minimumFault != expectedState.minimumFault
            || frozen.riskWindowSeconds != expectedState.riskWindowSeconds
            || frozen.additionalIgnitionChancePerWindow
                != expectedState.additionalIgnitionChancePerWindow
            || frozen.ignitionIntensity != expectedState.ignitionIntensity)
            throw Invalid(occurrenceInstanceId, "mana-lightning authored snapshot");
    }

    public static void RequireValidFrozenState(
        SeasonalSpecialExpeditionSaveData frozen,
        SeasonalSpecialExpeditionProfile authored,
        string occurrenceInstanceId)
    {
        bool expected = authored?.IsConfigured == true;
        if (frozen == null || frozen.configured != expected)
            throw Invalid(occurrenceInstanceId, "special-expedition configuration");
        if (!expected)
        {
            if (HasPayload(frozen))
                throw Invalid(occurrenceInstanceId, "empty special-expedition state");
            return;
        }
        SeasonalSpecialExpeditionSaveData expectedState =
            SeasonalSpecialExpeditionSaveData.FromProfile(authored);
        if (frozen.purpose != expectedState.purpose
            || frozen.siteArchetypeId != expectedState.siteArchetypeId
            || frozen.title != expectedState.title
            || frozen.description != expectedState.description
            || frozen.minimumDistanceSteps != expectedState.minimumDistanceSteps
            || frozen.maximumDistanceSteps != expectedState.maximumDistanceSteps
            || frozen.recommendedDanger != expectedState.recommendedDanger
            || frozen.durationSeconds != expectedState.durationSeconds
            || frozen.requiredMembers != expectedState.requiredMembers
            || frozen.recommendedPower != expectedState.recommendedPower
            || frozen.campaignOrder != expectedState.campaignOrder
            || frozen.authoredEncounterId != expectedState.authoredEncounterId
            || frozen.encounterPreviewText != expectedState.encounterPreviewText
            || frozen.encounterRewardPreviewText
                != expectedState.encounterRewardPreviewText
            || frozen.factionId != expectedState.factionId
            || !SameRewards(frozen.physicalRewards, expectedState.physicalRewards))
            throw Invalid(occurrenceInstanceId, "special-expedition authored snapshot");
    }

    public static void RequireMatchingOffer(
        OffenseWorldSiteStateData site,
        V20ActiveEventSaveData occurrence)
    {
        SeasonalSpecialExpeditionSaveData expected =
            occurrence?.seasonalSpecialExpedition;
        OffenseSeasonalExpeditionOfferData actual = site?.seasonalOffer;
        if (expected?.configured != true
            || actual?.IsConfigured != true
            || actual.occurrenceInstanceId != occurrence.instanceId
            || actual.definitionId != occurrence.definitionId
            || actual.offerDeadlineAbsoluteDay
                != occurrence.deadlineAbsoluteDay
            || site.factionId != expected.factionId
            || actual.description != expected.description
            || actual.recommendedDanger != expected.recommendedDanger
            || actual.durationSeconds != expected.durationSeconds
            || actual.requiredMembers != expected.requiredMembers
            || actual.recommendedPower != expected.recommendedPower
            || actual.campaignOrder != expected.campaignOrder
            || actual.authoredEncounterId != expected.authoredEncounterId
            || actual.encounterPreviewText != expected.encounterPreviewText
            || actual.encounterRewardPreviewText
                != expected.encounterRewardPreviewText
            || !SameRewards(actual.physicalRewards, expected.physicalRewards))
        {
            throw Invalid(
                occurrence?.instanceId,
                "special-expedition occurrence offer join");
        }
    }

    private static bool SameRewards(
        IReadOnlyList<SeasonalExpeditionPhysicalRewardSaveData> left,
        IReadOnlyList<SeasonalExpeditionPhysicalRewardSaveData> right)
    {
        left ??= Array.Empty<SeasonalExpeditionPhysicalRewardSaveData>();
        right ??= Array.Empty<SeasonalExpeditionPhysicalRewardSaveData>();
        return left.Count == right.Count && !left.Where((value, index) =>
            value == null
            || right[index] == null
            || value.itemId != right[index].itemId
            || value.displayLabel != right[index].displayLabel
            || value.exactQuantity != right[index].exactQuantity).Any();
    }

    private static bool SameRewards(
        IReadOnlyList<OffenseSeasonalPhysicalRewardData> left,
        IReadOnlyList<SeasonalExpeditionPhysicalRewardSaveData> right)
    {
        left ??= Array.Empty<OffenseSeasonalPhysicalRewardData>();
        right ??= Array.Empty<SeasonalExpeditionPhysicalRewardSaveData>();
        return left.Count == right.Count && !left.Where((value, index) =>
            value == null
            || right[index] == null
            || value.itemId != right[index].itemId
            || value.displayLabel != right[index].displayLabel
            || value.exactQuantity != right[index].exactQuantity).Any();
    }

    private static bool HasPayload(SeasonalManaLightningSaveData value) =>
        value != null
        && ((value.eligibleBuildingDefinitionIds?.Count ?? 0) != 0
            || value.minimumHeat != 0f
            || value.minimumFault != 0f
            || value.riskWindowSeconds != 0f
            || value.additionalIgnitionChancePerWindow != 0f
            || value.ignitionIntensity != 0f);

    private static bool HasPayload(SeasonalSpecialExpeditionSaveData value) =>
        value != null
        && (value.purpose != SeasonalSpecialExpeditionPurpose.None
            || !string.IsNullOrEmpty(value.siteArchetypeId)
            || !string.IsNullOrEmpty(value.title)
            || !string.IsNullOrEmpty(value.description)
            || value.minimumDistanceSteps != 0
            || value.maximumDistanceSteps != 0
            || value.recommendedDanger != 0f
            || value.durationSeconds != 0f
            || value.requiredMembers != 0
            || value.recommendedPower != 0f
            || value.campaignOrder != 0
            || !string.IsNullOrEmpty(value.authoredEncounterId)
            || !string.IsNullOrEmpty(value.encounterPreviewText)
            || !string.IsNullOrEmpty(value.encounterRewardPreviewText)
            || !string.IsNullOrEmpty(value.factionId)
            || (value.physicalRewards?.Count ?? 0) != 0);

    private static InvalidOperationException Invalid(
        string occurrenceInstanceId,
        string field) => new(
        $"Seasonal occurrence '{occurrenceInstanceId}' has invalid {field}.");
}
