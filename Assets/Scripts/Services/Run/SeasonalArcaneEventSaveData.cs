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
