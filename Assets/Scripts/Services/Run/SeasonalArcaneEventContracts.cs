using System;
using System.Collections.Generic;
using System.Linq;

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
